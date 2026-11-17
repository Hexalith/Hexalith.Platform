import assert from 'node:assert/strict';
import { readFileSync, writeFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

const directory = process.argv[2];
const original = await import(pathToFileURL(`${directory}/original-gate.mjs`));
const proposed = await import(pathToFileURL(`${directory}/gate.mjs`));
const release = await import(pathToFileURL(`${directory}/release-gate.mjs`));
const repository = original.repository;
const source = 'a'.repeat(40);

function fixture(branch, manual = false) {
  const values = new Map();
  values.set(`/repos/${repository}/git/ref/heads/${branch}`, {
    ref: `refs/heads/${branch}`, url: `https://api.github.com/repos/${repository}/git/refs/heads/${branch}`,
    object: { type: 'commit', sha: source, url: `https://api.github.com/repos/${repository}/git/commits/${source}` },
  });
  let callback;
  for (const [index, workflow] of original.workflows.entries()) {
    const path = `.github/workflows/${workflow.file}`;
    const run = { id: 10 + index, run_number: 20 + index, run_attempt: 3, workflow_id: 100 + index,
      path, name: workflow.name, repository: { full_name: repository }, head_repository: { full_name: repository },
      event: 'push', head_branch: branch, head_sha: source, pull_requests: [], status: 'completed', conclusion: 'success' };
    if (!callback) callback = structuredClone(run);
    values.set(`/repos/${repository}/actions/workflows/${workflow.file}`, { id: run.workflow_id, path, name: run.name, state: 'active' });
    values.set(`/repos/${repository}/actions/workflows/${workflow.file}/runs?event=push&branch=${branch}&head_sha=${source}&per_page=100`,
      { total_count: 1, workflow_runs: [run] });
    values.set(`/repos/${repository}/actions/runs/${run.id}`, run);
    values.set(`/repos/${repository}/actions/runs/${run.id}/attempts/3/jobs?per_page=100`, {
      total_count: workflow.jobs.length,
      jobs: workflow.jobs.map((name, offset) => ({ id: 1000 + index * 100 + offset, name, run_id: run.id,
        head_sha: source, status: 'completed', conclusion: 'success' })),
    });
  }
  const inputs = { eventName: 'workflow_run', event: { action: 'completed', repository: { full_name: repository }, workflow_run: callback },
    ownerRepository: repository, source, workflowRef: `${repository}/.github/workflows/source-release.yml@refs/heads/main`,
    workflowSha: source, checkout: source, job: 'verify',
    api: async path => { assert.ok(values.has(path), `Unexpected API path: ${path}`); return structuredClone(values.get(path)); } };
  if (manual) {
    const publisher = { id: 900, run_attempt: 2, workflow_id: 777, name: 'Qualified Platform source release',
      path: '.github/workflows/source-release.yml', event: 'workflow_dispatch', head_branch: branch, head_sha: source,
      repository: { full_name: repository }, head_repository: { full_name: repository }, status: 'in_progress', conclusion: null };
    values.set(`/repos/${repository}/actions/workflows/source-release.yml`, { id: publisher.workflow_id, name: publisher.name, path: publisher.path, state: 'active' });
    values.set(`/repos/${repository}/actions/runs/900`, publisher);
    const base = '0f4e27f1f78310bd731c10c19dc51bb1e0345d44';
    values.set(`/repos/${repository}/compare/${base}...${source}`, { base_commit: { sha: base }, merge_base_commit: { sha: base }, status: 'ahead', behind_by: 0 });
    Object.assign(inputs, { eventName: 'workflow_dispatch', event: {}, ref: `refs/heads/${branch}`,
      workflowRef: `${repository}/.github/workflows/source-release.yml@refs/heads/${branch}`, runId: '900', runAttempt: '2' });
  }
  return { inputs, values };
}

const results = [];
const oldSuccess = await original.qualify(fixture('main').inputs);
const newSuccess = await proposed.qualify(fixture('main').inputs);
assert.deepEqual(newSuccess, oldSuccess);
results.push({ case: 'original-main-public-route-identical-success-receipt', pass: true });

for (const module of [original, proposed]) {
  const { inputs, values } = fixture('main');
  values.get(`/repos/${repository}/git/ref/heads/main`).object.sha = 'b'.repeat(40);
  await assert.rejects(module.qualify(inputs), error => error instanceof module.GateError && error.message === 'main-advanced');
}
results.push({ case: 'original-main-stale-head-refusal-preserved', pass: true });

const { inputs, values } = fixture('1.7.x', true);
const receipt = await proposed.qualify(inputs);
assert.equal(receipt.eligible, true);
assert.equal(receipt.nativeAcceptance, false);
assert.deepEqual(receipt.dispatch, { run: 900, attempt: 2, workflow: '.github/workflows/source-release.yml' });
results.push({ case: 'public-maintenance-route-qualifies-without-native-credit', pass: true });

let saved;
const publishedInputs = { ...inputs, job: 'publish' };
const checked = await release.recheck(receipt, () => proposed.qualify(publishedInputs), value => { saved = value; });
assert.equal(checked.status, 'ready-to-tag');
assert.deepEqual(saved, checked);
results.push({ case: 'actual-release-hook-recheck-binds-verify-to-publish', pass: true });

values.get(`/repos/${repository}/actions/runs/900`).run_attempt = 3;
await assert.rejects(proposed.qualify(inputs), error => error instanceof proposed.GateError && error.message === 'wrong-maintenance-publisher-run');
results.push({ case: 'public-maintenance-route-preserves-typed-refusal-code', pass: true });
let invalidSave = false;
await assert.rejects(release.recheck(receipt, () => proposed.qualify(publishedInputs), () => { invalidSave = true; }));
assert.equal(invalidSave, false);
results.push({ case: 'actual-release-hook-refuses-rerun-before-save', pass: true });

const hashes = JSON.parse(readFileSync(`${directory}/input-hashes.json`, 'utf8'));
const report = { classification: 'effect-free API fixtures; no actual source CI, publication or runtime acceptance',
  originalSource: '0f4e27f1f78310bd731c10c19dc51bb1e0345d44', inputHashes: hashes, results };
writeFileSync(process.argv[3], JSON.stringify(report, null, 2) + '\n', { flag: 'wx' });
console.log(JSON.stringify({ pass: results.length, fail: 0, runtimeAcceptance: false }));
