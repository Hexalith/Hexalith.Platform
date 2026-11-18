import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { spawnSync } from 'node:child_process';
import * as yaml from 'js-yaml';

const raw = readFileSync('.github/workflows/tuple-ci.yml', 'utf8');
const workflow = yaml.load(raw);

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
  assert.doesNotMatch(raw, /git .*fetch|legacy-source|pre-extension|reader-workspace|CURRENT_PLATFORM_SHA|qualification\//);
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
