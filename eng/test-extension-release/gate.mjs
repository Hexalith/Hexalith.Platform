import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import { githubReadApi, repository, workflows } from '../source-release/gate.mjs';

const requireValue = (condition, reason) => { if (!condition) throw new Error(`fixture-publication: ${reason}`); };
const sha = value => typeof value === 'string' && /^[a-f0-9]{40}$/.test(value);
const positive = value => Number.isSafeInteger(value) && value > 0;

// Publication inherits the normal tuple/catalog gates and a source release for
// this exact current main. It never makes a source release or grants story credit.
export async function qualify({ source, version, checkout, workflowRef, workflowSha, ownerRepository, job, api }) {
  requireValue(ownerRepository === repository && sha(source) && checkout === source && workflowSha === source, 'wrong source identity');
  requireValue(workflowRef === `${repository}/.github/workflows/test-extension-release.yml@refs/heads/main`
    && ['qualify', 'publish'].includes(job), 'wrong executing workflow');
  requireValue(typeof version === 'string' && /^[1-9][0-9]*\.[0-9]+\.[0-9]+$/.test(version), 'require a stable source-release version');
  requireValue((await api(`/repos/${repository}/git/ref/heads/main`)).object?.sha === source, 'main advanced');
  const release = await api(`/repos/${repository}/releases/tags/v${version}`);
  requireValue(release.tag_name === `v${version}` && release.draft === false && release.prerelease === false
    && release.html_url === `https://github.com/${repository}/releases/tag/v${version}`, 'source release missing');
  let tag = (await api(`/repos/${repository}/git/ref/tags/v${version}`)).object;
  if (tag?.type === 'tag' && sha(tag.sha)) tag = (await api(`/repos/${repository}/git/tags/${tag.sha}`)).object;
  requireValue(tag?.type === 'commit' && tag.sha === source, 'source tag differs');
  const runs = [];
  for (const workflow of workflows) {
    const path = `.github/workflows/${workflow.file}`;
    const registered = await api(`/repos/${repository}/actions/workflows/${workflow.file}`);
    requireValue(positive(registered.id) && registered.path === path && registered.name === workflow.name && registered.state === 'active', 'wrong required workflow');
    const list = await api(`/repos/${repository}/actions/workflows/${workflow.file}/runs?event=push&branch=main&head_sha=${source}&per_page=100`);
    requireValue(Array.isArray(list.workflow_runs) && Number.isSafeInteger(list.total_count) && list.total_count > 0
      && list.total_count <= 100 && list.workflow_runs.length === list.total_count, 'incomplete source proofs');
    for (const run of list.workflow_runs) requireValue(positive(run.id) && positive(run.run_number) && positive(run.run_attempt)
      && run.workflow_id === registered.id && run.name === workflow.name && run.path === path && run.head_sha === source
      && run.head_branch === 'main' && run.event === 'push' && run.repository?.full_name === repository
      && run.head_repository?.full_name === repository && Array.isArray(run.pull_requests) && run.pull_requests.length === 0, 'foreign source proof');
    const ordered = [...list.workflow_runs].sort((a, b) => b.run_number - a.run_number);
    requireValue(new Set(ordered.map(run => run.run_number)).size === ordered.length, 'ambiguous latest source proof');
    const run = await api(`/repos/${repository}/actions/runs/${ordered[0].id}`);
    requireValue(run.id === ordered[0].id && run.run_number === ordered[0].run_number && run.run_attempt === ordered[0].run_attempt
      && run.workflow_id === registered.id && run.path === path && run.name === workflow.name
      && run.head_sha === source && run.head_branch === 'main' && run.event === 'push'
      && run.repository?.full_name === repository && run.head_repository?.full_name === repository
      && Array.isArray(run.pull_requests) && run.pull_requests.length === 0 && run.status === 'completed' && run.conclusion === 'success', 'latest source proof failed or changed');
    const jobs = await api(`/repos/${repository}/actions/runs/${run.id}/attempts/${run.run_attempt}/jobs?per_page=100`);
    requireValue(jobs.total_count === workflow.jobs.length && Array.isArray(jobs.jobs) && jobs.jobs.length === workflow.jobs.length
      && new Set(jobs.jobs.map(item => item.name)).size === workflow.jobs.length
      && workflow.jobs.every(name => jobs.jobs.some(item => item.name === name)), 'required source jobs differ');
    for (const item of jobs.jobs) requireValue(positive(item.id) && item.run_id === run.id && item.head_sha === source
      && item.status === 'completed' && item.conclusion === 'success', 'required source job failed');
    const after = await api(`/repos/${repository}/actions/runs/${run.id}`);
    requireValue(after.run_attempt === run.run_attempt && after.status === 'completed' && after.conclusion === 'success', 'source proof rerun');
    runs.push({ workflow: path, run: run.id, attempt: run.run_attempt });
  }
  requireValue((await api(`/repos/${repository}/git/ref/heads/main`)).object?.sha === source, 'main advanced');
  return { schema: 'hexalith.platform.test-extension-publication.v1', source, version,
    packageId: 'Hexalith.Platform.TestExtension', sourceRelease: release.html_url, runs,
    candidateAcceptance: false, storyAcceptance: false };
}

export async function actual(env = process.env) {
  requireValue(env.GITHUB_ACTIONS === 'true' && env.GITHUB_EVENT_NAME === 'workflow_dispatch'
    && env.GITHUB_REF === 'refs/heads/main', 'hosted main dispatch required');
  return qualify({ source: env.GITHUB_SHA, version: env.FIXTURE_VERSION, ownerRepository: env.GITHUB_REPOSITORY,
    checkout: execFileSync('git', ['rev-parse', 'HEAD'], { encoding: 'utf8' }).trim(),
    workflowRef: env.GITHUB_WORKFLOW_REF, workflowSha: env.GITHUB_WORKFLOW_SHA, job: env.GITHUB_JOB,
    api: githubReadApi(env.GH_TOKEN) });
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  try {
    const receipt = await actual();
    if (process.env.FIXTURE_EXPECTED_PROOF) requireValue(JSON.stringify(receipt)
      === readFileSync(process.env.FIXTURE_EXPECTED_PROOF, 'utf8').trim(), 'qualified source binding changed');
    if (process.env.FIXTURE_PROOF_OUTPUT) writeFileSync(process.env.FIXTURE_PROOF_OUTPUT, `${JSON.stringify(receipt)}\n`);
    console.log(JSON.stringify(receipt));
  } catch (error) { console.error(error.message); process.exitCode = 1; }
}
