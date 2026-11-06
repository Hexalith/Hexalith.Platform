import { readFileSync, writeFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';

export const repository = 'Hexalith/Hexalith.Platform';
export const workflows = [
  { file: 'tuple-ci.yml', name: 'Platform tuple CI preflight', jobs: ['tuple', 'Same changed source against the legacy public tuple'] },
  { file: 'catalog-builder.yml', name: 'Catalog builder consumer binding', jobs: ['3.130.0', '3.130.1', '3.130.2', '3.132.1', '4.1.0'].map(sdk => `consumer-binding (${sdk})`) },
];
export class GateError extends Error {}
const requireValue = (condition, code) => { if (!condition) throw new GateError(code); };
const positive = n => Number.isSafeInteger(n) && n > 0;
const sha = value => typeof value === 'string' && /^[a-f0-9]{40}$/.test(value);
const pathOf = workflow => `.github/workflows/${workflow.file}`;
const urlOf = run => `https://github.com/${repository}/actions/runs/${run.id}`;

function identity(run, workflow, source, workflowId) {
  requireValue(positive(run.id) && positive(run.run_number) && positive(run.run_attempt), 'invalid-run-identity');
  requireValue(run.name === workflow.name && run.path === pathOf(workflow) && run.workflow_id === workflowId, 'wrong-workflow');
  requireValue(run.repository?.full_name === repository && run.head_repository?.full_name === repository, 'foreign-run');
  requireValue(run.event === 'push' && run.head_branch === 'main' && run.head_sha === source, 'wrong-run-source');
  requireValue(Array.isArray(run.pull_requests) && run.pull_requests.length === 0, 'pr-run');
}

// API inputs are injectable data for resource-free tests; production always uses GitHub's authenticated GET API.
export async function qualify({ event, eventName, ownerRepository, checkout, source, workflowRef, workflowSha, job, api }) {
  requireValue(eventName === 'workflow_run' && event.action === 'completed', 'wrong-callback-event');
  requireValue(ownerRepository === repository && event.repository?.full_name === repository, 'foreign-callback');
  const callback = event.workflow_run;
  requireValue(callback && sha(source) && callback.head_sha === source, 'wrong-callback-sha');
  requireValue(workflowRef === `${repository}/.github/workflows/source-release.yml@refs/heads/main` && sha(workflowSha) && ['verify', 'publish'].includes(job), 'wrong-executing-workflow');
  requireValue(checkout === (job === 'verify' ? workflowSha : source), 'wrong-checkout');
  const trigger = workflows.find(w => callback.path === pathOf(w) && callback.name === w.name);
  requireValue(trigger, 'unknown-callback-workflow');
  requireValue(callback.event === 'push' && callback.head_branch === 'main' && callback.head_repository?.full_name === repository && callback.repository?.full_name === repository, 'wrong-callback-source');
  requireValue(callback.status === 'completed' && callback.conclusion === 'success' && positive(callback.id) && positive(callback.run_attempt) && positive(callback.run_number) && positive(callback.workflow_id), 'unsuccessful-callback');
  const main = () => api(`/repos/${repository}/git/ref/heads/main`);
  requireValue((await main()).object?.sha === source, 'main-advanced');
  requireValue(workflowSha === source, 'workflow-source-changed');
  const receipt = { schema: 'hexalith.platform.source-release.v1', source, eligible: false, status: 'deferred',
    executingWorkflow: { ref: workflowRef, sha: workflowSha, checkout, job },
    callback: { run: callback.id, attempt: callback.run_attempt, workflow: callback.path, url: urlOf(callback) },
    qualifyingRuns: [], computedVersion: null, nativeAcceptance: false, candidateAcceptance: false, storyAcceptance: false };
  for (const workflow of workflows) {
    const registered = await api(`/repos/${repository}/actions/workflows/${workflow.file}`);
    requireValue(positive(registered.id) && registered.path === pathOf(workflow) && registered.name === workflow.name && registered.state === 'active', 'unknown-workflow');
    const list = await api(`/repos/${repository}/actions/workflows/${workflow.file}/runs?event=push&branch=main&head_sha=${source}&per_page=100`);
    requireValue(Number.isSafeInteger(list.total_count) && list.total_count >= 0 && list.total_count <= 100 && Array.isArray(list.workflow_runs) && list.workflow_runs.length === list.total_count, 'incomplete-run-discovery');
    for (const run of list.workflow_runs) identity(run, workflow, source, registered.id);
    if (!list.workflow_runs.length) return { ...receipt, reason: 'counterpart-missing' };
    const ordered = [...list.workflow_runs].sort((a, b) => b.run_number - a.run_number);
    requireValue(new Set(ordered.map(r => r.run_number)).size === ordered.length, 'ambiguous-latest-run');
    const latest = await api(`/repos/${repository}/actions/runs/${ordered[0].id}`);
    identity(latest, workflow, source, registered.id);
    requireValue(latest.id === ordered[0].id && latest.run_number === ordered[0].run_number && latest.run_attempt === ordered[0].run_attempt, 'superseded-attempt');
    if (workflow === trigger) requireValue(callback.id === latest.id && callback.run_attempt === latest.run_attempt && callback.run_number === latest.run_number && callback.workflow_id === registered.id, 'superseded-callback');
    requireValue(['requested', 'queued', 'pending', 'in_progress', 'waiting', 'completed'].includes(latest.status), 'unknown-run-status');
    if (latest.status !== 'completed') return { ...receipt, reason: 'counterpart-pending' };
    requireValue(latest.conclusion === 'success', 'unsuccessful-latest-run');
    const jobs = await api(`/repos/${repository}/actions/runs/${latest.id}/attempts/${latest.run_attempt}/jobs?per_page=100`);
    requireValue(jobs.total_count === workflow.jobs.length && Array.isArray(jobs.jobs) && jobs.jobs.length === workflow.jobs.length, 'missing-or-extra-jobs');
    requireValue(new Set(jobs.jobs.map(j => j.name)).size === workflow.jobs.length && workflow.jobs.every(name => jobs.jobs.some(j => j.name === name)), 'wrong-required-jobs');
    for (const job of jobs.jobs) requireValue(positive(job.id) && job.run_id === latest.id && job.head_sha === source && job.status === 'completed' && job.conclusion === 'success', 'unsuccessful-required-job');
    const after = await api(`/repos/${repository}/actions/runs/${latest.id}`);
    identity(after, workflow, source, registered.id);
    requireValue(after.run_attempt === latest.run_attempt && after.status === 'completed' && after.conclusion === 'success', 'superseded-attempt');
    receipt.qualifyingRuns.push({ workflow: pathOf(workflow), run: latest.id, number: latest.run_number, attempt: latest.run_attempt,
      url: urlOf(latest), jobs: jobs.jobs.map(j => ({ name: j.name, id: j.id })).sort((a, b) => a.name.localeCompare(b.name)) });
  }
  requireValue((await main()).object?.sha === source, 'main-advanced');
  return { ...receipt, eligible: true, status: 'qualified' };
}

export function githubReadApi(token, request = fetch) {
  requireValue(typeof token === 'string' && token.length > 0, 'missing-actions-token');
  return async path => {
    requireValue(path.startsWith(`/repos/${repository}/`) && !path.includes('..'), 'unexpected-api-path');
    const response = await request(`https://api.github.com${path}`, { method: 'GET', redirect: 'error', signal: AbortSignal.timeout(30_000),
      headers: { authorization: `Bearer ${token}`, accept: 'application/vnd.github+json', 'x-github-api-version': '2022-11-28' } });
    requireValue(response.ok, 'github-read-failed');
    const text = await response.text();
    requireValue(Buffer.byteLength(text) <= 2_000_000, 'oversized-github-response');
    return JSON.parse(text);
  };
}

export function actualInputs(env = process.env) {
  requireValue(env.GITHUB_ACTIONS === 'true' && env.GITHUB_EVENT_NAME === 'workflow_run', 'hosted-callback-required');
  const event = JSON.parse(readFileSync(env.GITHUB_EVENT_PATH, 'utf8'));
  return { event, eventName: env.GITHUB_EVENT_NAME, ownerRepository: env.GITHUB_REPOSITORY,
    source: env.SOURCE_RELEASE_SHA || event.workflow_run?.head_sha,
    workflowRef: env.GITHUB_WORKFLOW_REF, workflowSha: env.GITHUB_WORKFLOW_SHA, job: env.GITHUB_JOB,
    checkout: execFileSync('git', ['rev-parse', 'HEAD'], { encoding: 'utf8' }).trim(), api: githubReadApi(env.GITHUB_TOKEN) };
}
export const qualifyActual = () => qualify(actualInputs());
export function saveReceipt(receipt, file = process.env.SOURCE_RELEASE_RECEIPT) {
  const text = JSON.stringify(receipt, null, 2) + '\n';
  requireValue(Buffer.byteLength(text) <= 16_384 && typeof file === 'string', 'invalid-receipt');
  writeFileSync(file, text);
}
export function binding(receipt) { return JSON.stringify([receipt.source, receipt.executingWorkflow?.ref, receipt.executingWorkflow?.sha, receipt.callback, receipt.qualifyingRuns]); }
