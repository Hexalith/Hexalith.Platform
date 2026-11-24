import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import test from 'node:test';
import { spawnSync } from 'node:child_process';
import * as yaml from 'js-yaml';

const raw = readFileSync('.github/workflows/tuple-ci.yml', 'utf8');
const workflow = yaml.load(raw);
const sourceSteps = workflow.jobs['resolve-sources'].steps;
const intakeIndex = sourceSteps.findIndex(step => step.name === 'Check the append-only module intake manifest');
const intake = sourceSteps[intakeIndex];

test('all triggers execute candidate-only resolution, current execution and verdict', () => {
  assert.deepEqual(workflow.on.push.branches, ['main']);
  assert.deepEqual(workflow.on.pull_request.branches, ['main']);
  assert.deepEqual(Object.keys(workflow.on.workflow_dispatch.inputs), ['candidate-sha']);
  assert.equal(workflow.on.workflow_dispatch.inputs['candidate-sha'].required, true);
  assert.deepEqual(Object.keys(workflow.jobs), ['resolve-sources', 'tuple', 'tuple-verdict']);
  assert.deepEqual(workflow.jobs.tuple.needs, ['resolve-sources']);
  const resolution = workflow.jobs['resolve-sources'];
  assert.deepEqual(resolution.outputs, { 'current-source': '${{ steps.sources.outputs.current-source }}' });
  assert.ok(resolution.steps.some(step => step.run?.includes("read_selection(Path.cwd(), os.environ['PLATFORM_CANDIDATE_SHA'])")));
  assert.ok(!JSON.stringify(resolution).includes('submodule update'));
  assert.equal(workflow.jobs.tuple['continue-on-error'], undefined);
  assert.ok(workflow.jobs.tuple.steps.some(step => step.run?.includes('eng/run_tuple_ci.py')));
  assert.equal(workflow.jobs.tuple.env.PLATFORM_CANDIDATE_SHA, '${{ needs.resolve-sources.outputs.current-source }}');
});

test('every Platform checkout is shallow and selects only the candidate', () => {
  const checkouts = Object.values(workflow.jobs).flatMap(job => job.steps).filter(step => step.uses?.startsWith('actions/checkout@'));
  assert.equal(checkouts.length, 2);
  for (const checkout of checkouts) {
    assert.equal(checkout.with.ref, '${{ inputs.candidate-sha || github.sha }}');
    assert.equal(checkout.with['fetch-depth'], 1);
    assert.equal(checkout.with.submodules, false);
    assert.equal(checkout.with['persist-credentials'], false);
  }
  assert.doesNotMatch(raw, /legacy-source|pre-extension|reader-workspace|CURRENT_PLATFORM_SHA|qualification\//);
  const fetch = 'git fetch --depth=1 --no-tags --no-write-fetch-head --no-recurse-submodules --refmap= origin "$MODULE_INTAKE_BASE"';
  assert.deepEqual(raw.match(/^.*\bgit\b[^\n]*\bfetch\b[^\n]*$/gm)?.map(line => line.trim()), [fetch]);
  const guard = '[[ "$MODULE_INTAKE_BASE" =~ ^[0-9a-f]{40}$ && "$MODULE_INTAKE_BASE" != "$PLATFORM_CANDIDATE_SHA" ]]';
  assert.ok(intake.run.includes(`${guard}\n${fetch}\n`));
});

test('intake runs before the Builds gate and dispatch uses the first parent from a shallow candidate', () => {
  assert.ok(intakeIndex > sourceSteps.findIndex(step => step.id === 'sources'));
  assert.ok(intakeIndex < sourceSteps.findIndex(step => step.run?.includes('eng/builds_publication.py')));
  assert.equal(intake.env.MODULE_INTAKE_BASE, '${{ github.event.pull_request.base.sha || github.event.before }}');

  const scratch = mkdtempSync(join(tmpdir(), 'tuple-intake-'));
  try {
    const source = join(scratch, 'origin');
    const checkout = join(scratch, 'candidate');
    const bin = join(scratch, 'bin');
    const log = join(scratch, 'intake.log');
    mkdirSync(source);
    mkdirSync(bin);
    writeFileSync(join(bin, 'python3'), '#!/bin/bash\nprintf "%s\n" "$*" >> "$INTAKE_LOG"\n', { mode: 0o755 });
    const git = (cwd, ...args) => {
      const result = spawnSync('git', ['-c', 'user.name=Intake test', '-c', 'user.email=intake@example.com', ...args], { cwd, encoding: 'utf8' });
      assert.equal(result.status, 0, result.stderr);
      return result.stdout.trim();
    };
    git(source, 'init', '-q');
    git(source, 'commit', '--allow-empty', '-qm', 'First parent');
    const parent = git(source, 'rev-parse', 'HEAD');
    git(source, 'commit', '--allow-empty', '-qm', 'Candidate');
    const candidate = git(source, 'rev-parse', 'HEAD');
    git(scratch, 'clone', '-q', '--depth=1', pathToFileURL(source).href, checkout);
    assert.equal(git(checkout, 'rev-parse', '--is-shallow-repository'), 'true');
    assert.notEqual(spawnSync('git', ['cat-file', '-e', parent], { cwd: checkout }).status, 0);
    const refs = git(checkout, 'show-ref');

    const env = {
      ...process.env, PATH: `${bin}:${process.env.PATH}`, INTAKE_LOG: log,
      GITHUB_EVENT_NAME: 'workflow_dispatch', PLATFORM_CANDIDATE_SHA: candidate, MODULE_INTAKE_BASE: '',
    };
    const dispatch = spawnSync('bash', ['-c', intake.run], { cwd: checkout, env, encoding: 'utf8' });
    assert.equal(dispatch.status, 0, dispatch.stderr);
    assert.equal(readFileSync(log, 'utf8'), `-B eng/test_module_intake.py\n-B eng/module_intake.py --check --against ${parent}\n`);
    assert.equal(git(checkout, 'rev-parse', 'HEAD'), candidate);
    assert.equal(git(checkout, 'show-ref'), refs);

    writeFileSync(log, '');
    const noParent = spawnSync('bash', ['-c', intake.run], {
      cwd: checkout, env: { ...env, PLATFORM_CANDIDATE_SHA: parent }, encoding: 'utf8',
    });
    assert.notEqual(noParent.status, 0);
    assert.equal(readFileSync(log, 'utf8'), '-B eng/test_module_intake.py\n');
  } finally {
    rmSync(scratch, { recursive: true, force: true });
  }
});

test('overall verdict requires both actual prerequisites even when they fail or skip', () => {
  const verdict = workflow.jobs['tuple-verdict'];
  assert.deepEqual(verdict.needs, ['resolve-sources', 'tuple']);
  assert.equal(verdict.if, 'always()');
  assert.deepEqual(verdict.steps[0].env, {
    SOURCE_RESULT: '${{ needs.resolve-sources.result }}',
    CURRENT_RESULT: '${{ needs.tuple.result }}',
  });
});

test('executable aggregate refuses every failed cancelled skipped or absent result', () => {
  const script = workflow.jobs['tuple-verdict'].steps[0].run;
  const statuses = ['success', 'failure', 'cancelled', 'skipped', ''];
  for (const source of statuses) for (const current of statuses) {
    const result = spawnSync('bash', ['-c', script], { env: { SOURCE_RESULT: source, CURRENT_RESULT: current } });
    assert.equal(result.status === 0, source === 'success' && current === 'success');
  }
  assert.notEqual(spawnSync('bash', ['-c', script], { env: {} }).status, 0);
});

test('candidate execution authenticates initialized owners and package identities first', () => {
  const steps = workflow.jobs.tuple.steps;
  const check = steps.findIndex(step => step.name === 'Recheck the exact initialized Parties source and declaration');
  assert.ok(check > 0);
  assert.ok(steps.slice(0, check).some(step => step.run?.includes('submodule update --init')));
  assert.ok(steps[check].run.includes("verify_checkout(Path.cwd(), os.environ['PLATFORM_CANDIDATE_SHA'])"));
  assert.ok(check < steps.findIndex(step => step.run?.includes('eng/run_tuple_ci.py')));
});

test('source resolution refuses Builds tag drift and execution binds the checkout to the recorded gitlink', () => {
  const resolution = workflow.jobs['resolve-sources'].steps;
  const tag = resolution.findIndex(step => step.run?.includes('eng/builds_publication.py --revision "$PLATFORM_CANDIDATE_SHA"'));
  assert.ok(tag > resolution.findIndex(step => step.id === 'sources'));
  assert.equal(resolution[tag].env.GH_TOKEN, '${{ github.token }}');
  const dependencies = workflow.jobs.tuple.steps.find(step => step.id === 'dependencies').run;
  assert.match(dependencies, /builds_source="\$\(python3 -B eng\/builds_tuple\.py\)"\n\s*git submodule update --init\n\s*test "\$\(git -C references\/Hexalith\.Builds rev-parse HEAD\)" = "\$builds_source"/);
  const controls = workflow.jobs.tuple.steps.find(step => step.name === 'Verify automatic source release controls').run;
  assert.ok(controls.includes('python3 -B eng/test_builds_tool_lock.py'));
});
