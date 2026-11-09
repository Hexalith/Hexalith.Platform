import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { spawnSync } from 'node:child_process';
import yaml from 'js-yaml';

const workflow = yaml.load(readFileSync('.github/workflows/tuple-ci.yml', 'utf8'));

test('current and both genuine historical executions fan out from source resolution', () => {
  const jobs = workflow.jobs;
  assert.deepEqual(jobs.tuple.needs, ['resolve-sources']);
  assert.deepEqual(jobs['legacy-public-cohort'].needs, ['resolve-sources']);
  assert.deepEqual(jobs['pre-extension-public-cohort'].needs, ['resolve-sources']);
  const resolution = jobs['resolve-sources'];
  assert.ok(resolution.steps.some(step => step.run?.includes('eng/legacy_tuple_source.py --current')));
  assert.ok(resolution.steps.some(step => step.run?.includes('eng/pre_extension_tuple_source.py --current')));
  assert.ok(!JSON.stringify(resolution).includes('submodule update'));
  for (const lane of ['tuple', 'legacy-public-cohort', 'pre-extension-public-cohort']) {
    assert.equal(jobs[lane]['continue-on-error'], undefined);
    assert.ok(jobs[lane].steps.some(step => step.run?.includes('eng/run_tuple_ci.py')));
  }
  assert.equal(jobs.tuple.env.PLATFORM_CANDIDATE_SHA, '${{ needs.resolve-sources.outputs.current-source }}');
  assert.equal(jobs['legacy-public-cohort'].steps[0].with.ref, '${{ needs.resolve-sources.outputs.legacy-source }}');
  assert.equal(jobs['legacy-public-cohort'].name, 'Same changed source against the legacy public tuple');
  assert.equal(jobs['pre-extension-public-cohort'].steps[0].with.ref, '${{ needs.resolve-sources.outputs.pre-extension-source }}');
  assert.equal(jobs['pre-extension-public-cohort'].name, 'Same changed source against the pre-extension public tuple');
});

test('overall tuple verdict refuses failed cancelled skipped or missing lanes', () => {
  const verdict = workflow.jobs['tuple-verdict'];
  assert.deepEqual(verdict.needs, ['resolve-sources', 'tuple', 'legacy-public-cohort', 'pre-extension-public-cohort']);
  assert.equal(verdict.if, 'always()');
  const step = verdict.steps[0];
  assert.equal(step.env.SOURCE_RESULT, '${{ needs.resolve-sources.result }}');
  assert.equal(step.env.CURRENT_RESULT, '${{ needs.tuple.result }}');
  assert.equal(step.env.LEGACY_RESULT, '${{ needs.legacy-public-cohort.result }}');
  assert.equal(step.env.PRE_EXTENSION_RESULT, '${{ needs.pre-extension-public-cohort.result }}');
  assert.ok(step.run.includes('test "$SOURCE_RESULT" = success'));
  assert.ok(step.run.includes('test "$CURRENT_RESULT" = success'));
  assert.ok(step.run.includes('test "$LEGACY_RESULT" = success'));
  assert.ok(step.run.includes('test "$PRE_EXTENSION_RESULT" = success'));
});

test('actual aggregate step exits nonzero for every unsuccessful or absent lane result', () => {
  const script = workflow.jobs['tuple-verdict'].steps[0].run;
  const statuses = ['success', 'failure', 'cancelled', 'skipped', ''];
  for (const source of statuses) for (const current of statuses) for (const legacy of statuses) for (const preExtension of statuses) {
    const result = spawnSync('bash', ['-e', '-c', script], { env: { SOURCE_RESULT: source, CURRENT_RESULT: current, LEGACY_RESULT: legacy, PRE_EXTENSION_RESULT: preExtension } });
    assert.equal(result.status === 0, [source, current, legacy, preExtension].every(value => value === 'success'));
  }
});
