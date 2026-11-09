import assert from 'node:assert/strict';
import { readFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import test from 'node:test';
import yaml from 'js-yaml';

const workflow = yaml.load(readFileSync('.github/workflows/test-extension-release.yml', 'utf8'));
const expected = [
  ['qualify', 'Bind job-local proof output', 'FIXTURE_PROOF_OUTPUT', 'fixture-proof.json'],
  ['publish', 'Bind downloaded qualification proof', 'FIXTURE_EXPECTED_PROOF', 'fixture-package/fixture-proof.json'],
];

function proofBindings(document) {
  return expected.map(([job, name, key, relative]) => {
    assert.ok(!JSON.stringify(document.jobs[job].env).match(/\$\{\{\s*runner\./), 'runner context is unavailable in job-level env');
    const step = document.jobs[job].steps[0];
    assert.equal(step.name, name, 'proof paths must be bound before any checkout or gate');
    assert.equal(step.shell, 'bash');
    assert.ok(step.run.includes('"$RUNNER_TEMP"'));
    assert.ok(step.run.includes('"$GITHUB_ENV"'));
    return { step, key, relative };
  });
}

test('fixture proof paths bind through the actual runtime environment before use', () => {
  for (const { step, key, relative } of proofBindings(workflow)) {
    const temporary = mkdtempSync(join(tmpdir(), 'fixture-workflow-path-'));
    try {
      const environment = join(temporary, 'environment');
      const result = spawnSync('bash', ['-e', '-c', step.run], {
        env: { RUNNER_TEMP: temporary, GITHUB_ENV: environment }, encoding: 'utf8',
      });
      assert.equal(result.status, 0, result.stderr);
      assert.equal(readFileSync(environment, 'utf8'), `${key}=${temporary}/${relative}\n`);
    } finally {
      rmSync(temporary, { recursive: true, force: true });
    }
  }
});

test('fixture workflow refuses the observed unsupported job runner context', () => {
  const changed = structuredClone(workflow);
  changed.jobs.qualify.env.FIXTURE_PROOF_OUTPUT = '${{ runner.temp }}/fixture-proof.json';
  assert.throws(() => proofBindings(changed), /runner context is unavailable/);
});

test('fixture workflow refuses proof path setup after its first use', () => {
  const changed = structuredClone(workflow);
  changed.jobs.publish.steps.push(changed.jobs.publish.steps.shift());
  assert.throws(() => proofBindings(changed), /proof paths must be bound/);
});
