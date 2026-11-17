// The only additional source-release context is the reviewed 1.7.x maintenance lane.
export const maintenance = Object.freeze({ branch: '1.7.x', base: '0f4e27f1f78310bd731c10c19dc51bb1e0345d44', baseTag: 'v1.7.1', range: '1.7.x' });
const positive = value => Number.isSafeInteger(value) && value > 0;
const revision = value => typeof value === 'string' && /^[0-9a-f]{40}$/.test(value);

export async function qualifyMaintenance(inputs, { repository, workflows, GateError = Error }) {
  const requireValue = (condition, code) => { if (!condition) throw new GateError(code); };
  const { eventName, ownerRepository, checkout, source, workflowRef, workflowSha, job, ref, runId, runAttempt, api } = inputs;
  const branchRef = `refs/heads/${maintenance.branch}`;
  requireValue(eventName === 'workflow_dispatch' && ownerRepository === repository && ref === branchRef,
    'unsupported-maintenance-dispatch');
  requireValue(revision(source) && source === workflowSha && source === checkout && ['verify', 'publish'].includes(job)
    && workflowRef === `${repository}/.github/workflows/source-release.yml@${branchRef}`
    && /^[1-9][0-9]*$/.test(runId || '') && /^[1-9][0-9]*$/.test(runAttempt || ''),
  'wrong-maintenance-executing-source');
  const registeredPublisher = await api(`/repos/${repository}/actions/workflows/source-release.yml`);
  requireValue(positive(registeredPublisher.id) && registeredPublisher.path === '.github/workflows/source-release.yml'
    && registeredPublisher.name === 'Qualified Platform source release' && registeredPublisher.state === 'active',
  'unregistered-maintenance-publisher');
  const ownRunPath = `/repos/${repository}/actions/runs/${runId}`;
  const dispatch = await api(ownRunPath);
  const dispatchMatches = value => positive(value.id) && value.id.toString() === runId
    && positive(value.run_attempt) && value.run_attempt.toString() === runAttempt
    && value.workflow_id === registeredPublisher.id && value.name === registeredPublisher.name
    && value.path === registeredPublisher.path && value.event === 'workflow_dispatch'
    && value.head_branch === maintenance.branch && value.head_sha === source
    && value.repository?.full_name === repository && value.head_repository?.full_name === repository
    && value.status === 'in_progress' && value.conclusion === null;
  requireValue(dispatchMatches(dispatch), 'wrong-maintenance-publisher-run');
  const headPath = `/repos/${repository}/git/ref/heads/${maintenance.branch}`;
  const live = async () => {
    const head = await api(headPath);
    requireValue(head.ref === branchRef && head.url === `https://api.github.com/repos/${repository}/git/${branchRef}`
      && head.object?.type === 'commit' && head.object.sha === source
      && head.object.url === `https://api.github.com/repos/${repository}/git/commits/${source}`,
    'maintenance-source-advanced');
  };
  await live();
  const comparison = await api(`/repos/${repository}/compare/${maintenance.base}...${source}`);
  requireValue(comparison.base_commit?.sha === maintenance.base && comparison.merge_base_commit?.sha === maintenance.base
    && ['ahead', 'identical'].includes(comparison.status) && comparison.behind_by === 0,
  'maintenance-source-outside-approved-base');
  const receipt = { schema: 'hexalith.platform.source-release.v1', source, eligible: false, status: 'deferred',
    maintenance, executingWorkflow: { ref: workflowRef, sha: workflowSha, checkout, job },
    dispatch: { run: Number(runId), attempt: Number(runAttempt), workflow: registeredPublisher.path },
    qualifyingRuns: [], computedVersion: null, nativeAcceptance: false, candidateAcceptance: false, storyAcceptance: false };
  for (const workflow of workflows) {
    const path = `.github/workflows/${workflow.file}`;
    const registered = await api(`/repos/${repository}/actions/workflows/${workflow.file}`);
    requireValue(positive(registered.id) && registered.path === path && registered.name === workflow.name
      && registered.state === 'active', 'unknown-maintenance-source-workflow');
    const identity = run => positive(run.id) && positive(run.run_number) && positive(run.run_attempt)
      && run.workflow_id === registered.id && run.path === path && run.name === workflow.name
      && run.repository?.full_name === repository && run.head_repository?.full_name === repository
      && run.head_sha === source && run.head_branch === maintenance.branch && run.event === 'push'
      && Array.isArray(run.pull_requests) && run.pull_requests.length === 0;
    const listPath = `/repos/${repository}/actions/workflows/${workflow.file}/runs?event=push&branch=${maintenance.branch}&head_sha=${source}&per_page=100`;
    const list = await api(listPath);
    requireValue(positive(list.total_count) && list.total_count <= 100 && Array.isArray(list.workflow_runs)
      && list.workflow_runs.length === list.total_count && list.workflow_runs.every(identity)
      && new Set(list.workflow_runs.map(run => run.run_number)).size === list.total_count,
    'missing-truncated-or-foreign-maintenance-source-runs');
    const latest = [...list.workflow_runs].sort((left, right) => right.run_number - left.run_number)[0];
    const runPath = `/repos/${repository}/actions/runs/${latest.id}`;
    const current = await api(runPath);
    requireValue(identity(current) && current.run_number === latest.run_number && current.run_attempt === latest.run_attempt
      && current.status === 'completed' && current.conclusion === 'success', 'latest-maintenance-source-attempt-not-successful');
    const jobs = await api(`${runPath}/attempts/${current.run_attempt}/jobs?per_page=100`);
    requireValue(jobs.total_count === workflow.jobs.length && Array.isArray(jobs.jobs) && jobs.jobs.length === workflow.jobs.length
      && new Set(jobs.jobs.map(value => value.name)).size === workflow.jobs.length
      && workflow.jobs.every(name => jobs.jobs.some(value => value.name === name)), 'maintenance-required-jobs-changed');
    requireValue(jobs.jobs.every(value => positive(value.id) && value.run_id === current.id && value.head_sha === source
      && value.status === 'completed' && value.conclusion === 'success'), 'maintenance-required-job-not-successful');
    const after = await api(runPath);
    requireValue(identity(after) && after.run_number === current.run_number && after.run_attempt === current.run_attempt
      && after.status === 'completed' && after.conclusion === 'success', 'maintenance-source-attempt-changed');
    requireValue(JSON.stringify(await api(listPath)) === JSON.stringify(list), 'maintenance-source-run-discovery-changed');
    requireValue(JSON.stringify(await api(`/repos/${repository}/actions/workflows/${workflow.file}`)) === JSON.stringify(registered),
      'maintenance-source-workflow-registration-changed');
    receipt.qualifyingRuns.push({ workflow: path, run: current.id, number: current.run_number, attempt: current.run_attempt,
      url: `https://github.com/${repository}/actions/runs/${current.id}`,
      jobs: jobs.jobs.map(value => ({ name: value.name, id: value.id })).sort((left, right) => left.name.localeCompare(right.name)) });
  }
  await live();
  requireValue(dispatchMatches(await api(ownRunPath)), 'maintenance-publisher-attempt-changed');
  return { ...receipt, eligible: true, status: 'qualified' };
}
