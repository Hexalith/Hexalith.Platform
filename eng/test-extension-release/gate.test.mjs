import assert from 'node:assert/strict';
import test from 'node:test';
import { qualify } from './gate.mjs';
import { repository, workflows } from '../source-release/gate.mjs';

function fixture() {
  const source = 'a'.repeat(40);
  const base = `/repos/${repository}`;
  const data = new Map([
    [`${base}/git/ref/heads/main`, { object: { sha: source } }],
    [`${base}/git/ref/tags/v1.9.0`, { object: { type: 'commit', sha: source } }],
    [`${base}/releases/tags/v1.9.0`, { tag_name: 'v1.9.0', draft: false, prerelease: false,
      html_url: `https://github.com/${repository}/releases/tag/v1.9.0` }],
  ]);
  workflows.forEach((workflow, index) => {
    const run = { id: index + 100, workflow_id: index + 1, run_number: 12, run_attempt: 1,
      name: workflow.name, path: `.github/workflows/${workflow.file}`, head_sha: source,
      head_branch: 'main', event: 'push', repository: { full_name: repository },
      head_repository: { full_name: repository }, pull_requests: [], status: 'completed', conclusion: 'success' };
    data.set(`${base}/actions/workflows/${workflow.file}`, { id: run.workflow_id, name: workflow.name, path: run.path, state: 'active' });
    data.set(`${base}/actions/workflows/${workflow.file}/runs?event=push&branch=main&head_sha=${source}&per_page=100`, { total_count: 1, workflow_runs: [run] });
    data.set(`${base}/actions/runs/${run.id}`, run);
    data.set(`${base}/actions/runs/${run.id}/attempts/1/jobs?per_page=100`, { total_count: workflow.jobs.length,
      jobs: workflow.jobs.map((name, job) => ({ id: index * 10 + job + 1, run_id: run.id, head_sha: source, name, status: 'completed', conclusion: 'success' })) });
  });
  const options = { source, version: '1.9.0', checkout: source, workflowSha: source, ownerRepository: repository,
    workflowRef: `${repository}/.github/workflows/test-extension-release.yml@refs/heads/main`, job: 'qualify',
    api: async path => { assert(data.has(path), `Unexpected API path ${path}`); return structuredClone(data.get(path)); } };
  return { data, options, base };
}

test('exact normal source qualifications authorize only the named fixture', async () => {
  const { options } = fixture();
  const result = await qualify(options);
  assert.equal(result.packageId, 'Hexalith.Platform.TestExtension');
  assert.equal(result.runs.length, 2);
  assert.equal(result.storyAcceptance, false);
});

for (const [name, change] of [
  ['foreign repository', ({ options }) => { options.ownerRepository = 'foreign/repo'; }],
  ['different checkout', ({ options }) => { options.checkout = 'b'.repeat(40); }],
  ['foreign workflow', ({ options }) => { options.workflowRef = `${repository}/.github/workflows/other.yml@refs/heads/main`; }],
  ['arbitrary version', ({ options }) => { options.version = '1.9.0-candidate'; }],
  ['advanced main', ({ data, base }) => { data.get(`${base}/git/ref/heads/main`).object.sha = 'b'.repeat(40); }],
  ['different source tag', ({ data, base }) => { data.get(`${base}/git/ref/tags/v1.9.0`).object.sha = 'b'.repeat(40); }],
  ['draft release', ({ data, base }) => { data.get(`${base}/releases/tags/v1.9.0`).draft = true; }],
  ['latest tuple failed', ({ data, base }) => { data.get(`${base}/actions/runs/100`).conclusion = 'failure'; }],
  ['latest tuple pending', ({ data, base }) => { data.get(`${base}/actions/runs/100`).status = 'in_progress'; }],
  ['foreign push proof', ({ data, base }) => { data.get(`${base}/actions/runs/100`).head_repository.full_name = 'foreign/repo'; }],
  ['missing legacy source job', ({ data, base }) => { data.get(`${base}/actions/runs/100/attempts/1/jobs?per_page=100`).jobs.pop(); }],
  ['failed catalog job', ({ data, base }) => { data.get(`${base}/actions/runs/101/attempts/1/jobs?per_page=100`).jobs[0].conclusion = 'failure'; }],
  ['incomplete proof discovery', ({ data, base, options }) => { data.get(`${base}/actions/workflows/tuple-ci.yml/runs?event=push&branch=main&head_sha=${options.source}&per_page=100`).total_count = 101; }],
]) test(`refuses ${name}`, async () => {
  const state = fixture(); change(state);
  await assert.rejects(qualify(state.options));
});
