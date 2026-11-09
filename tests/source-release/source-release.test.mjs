import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';
import yaml from 'js-yaml';
import { analyzeCommits } from '@semantic-release/commit-analyzer';
import { generateNotes as renderReleaseNotes } from '@semantic-release/release-notes-generator';
import * as github from '@semantic-release/github';
import config from '../../release.config.mjs';
import { repository, workflows, qualify, githubReadApi, binding } from '../../eng/source-release/gate.mjs';
import { recheck, verifyRelease, generateNotes } from '../../eng/source-release/release-gate.mjs';
import { runRelease, recordCliFailure } from '../../eng/source-release/run.mjs';

const source = 'a'.repeat(40), foreign = 'b'.repeat(40);
const clone = value => structuredClone(value);
function fixture(trigger = 0) {
  const runs = workflows.map((w, n) => ({ id: 101 + n, workflow_id: 11 + n, run_number: 21 + n, run_attempt: 1,
    name: w.name, path: `.github/workflows/${w.file}`, head_sha: source, event: 'push', head_branch: 'main',
    repository: { full_name: repository }, head_repository: { full_name: repository }, pull_requests: [], status: 'completed', conclusion: 'success' }));
  const state = { main: source, runs, jobs: workflows.map((w, n) => w.jobs.map((name, j) => ({ name, id: 1000 + n * 10 + j,
    run_id: runs[n].id, head_sha: source, status: 'completed', conclusion: 'success' }))), missing: -1, extra: [], reads: [] };
  const event = { action: 'completed', repository: { full_name: repository }, workflow_run: clone(runs[trigger]) };
  const api = async path => {
    state.reads.push(path);
    if (path.endsWith('/git/ref/heads/main')) return { object: { sha: state.main } };
    for (const [n, w] of workflows.entries()) {
      const base = `/repos/${repository}/actions/workflows/${w.file}`;
      if (path === base) return { id: 11 + n, name: w.name, path: `.github/workflows/${w.file}`, state: 'active' };
      if (path.startsWith(base + '/runs?')) {
        assert.ok(path.includes(`event=push&branch=main&head_sha=${source}`));
        const result = state.missing === n ? [] : [state.runs[n], ...state.extra.filter(x => x.workflow_id === 11 + n)];
        return clone({ total_count: result.length, workflow_runs: result });
      }
      if (path === `/repos/${repository}/actions/runs/${state.runs[n].id}`) return clone(state.runs[n]);
      if (path === `/repos/${repository}/actions/runs/${state.runs[n].id}/attempts/${state.runs[n].run_attempt}/jobs?per_page=100`)
        return clone({ total_count: state.jobs[n].length, jobs: state.jobs[n] });
    }
    throw new Error('Unexpected fixture request');
  };
  return { state, inputs: { event, eventName: 'workflow_run', ownerRepository: repository, checkout: source, source, workflowRef: `${repository}/.github/workflows/source-release.yml@refs/heads/main`, workflowSha: source, job: 'verify', api } };
}

test('either final callback requires both latest successful main lanes and exact three/six jobs', async () => {
  for (const trigger of [0, 1]) {
    const { inputs } = fixture(trigger); const result = await qualify(inputs);
    assert.equal(result.eligible, true); assert.equal(result.source, source); assert.deepEqual(result.qualifyingRuns.map(x => x.jobs.length), [3, 6]);
    assert.deepEqual([result.nativeAcceptance, result.candidateAcceptance, result.storyAcceptance], [false, false, false]);
    assert.ok(Buffer.byteLength(JSON.stringify(result)) < 16384);
  }
});
test('earlier callback defers pending/missing counterpart; later callback can proceed', async () => {
  const f = fixture(); f.state.runs[1].status = 'in_progress'; f.state.runs[1].conclusion = null;
  assert.equal((await qualify(f.inputs)).reason, 'counterpart-pending');
  f.state.missing = 1; assert.equal((await qualify(f.inputs)).reason, 'counterpart-missing');
  f.state.missing = -1; f.state.runs[1].status = 'completed'; f.state.runs[1].conclusion = 'success';
  f.inputs.event.workflow_run = clone(f.state.runs[1]); assert.equal((await qualify(f.inputs)).eligible, true);
});
test('latest failed/cancelled/timed-out/skipped/neutral counterpart never borrows an earlier success', async () => {
  for (const conclusion of ['failure', 'cancelled', 'timed_out', 'skipped', 'neutral']) {
    const f = fixture(); f.state.runs[1].conclusion = conclusion;
    f.state.extra = [{ ...clone(f.state.runs[1]), id: 91, run_number: 1, conclusion: 'success' }];
    await assert.rejects(qualify(f.inputs), /unsuccessful-latest-run/);
  }
});
test('superseded callback, primary attempt race and latest pending attempt refuse', async () => {
  const f = fixture(); f.state.runs[0].run_attempt = 2;
  await assert.rejects(qualify(f.inputs), /superseded-callback/);
  const g = fixture(); const api = g.inputs.api;
  g.inputs.api = async path => { const result = await api(path); if (path.endsWith('/actions/runs/101')) result.run_attempt++; return result; };
  await assert.rejects(qualify(g.inputs), /superseded-attempt/);
  const h = fixture(); h.state.runs[1].run_attempt = 2; h.state.runs[1].status = 'queued';
  assert.equal((await qualify(h.inputs)).eligible, false);
});
test('required job failure/cancellation/skip, missing/unknown and duplicate names refuse', async () => {
  for (const conclusion of ['failure', 'cancelled', 'skipped']) {
    const f = fixture(); f.state.jobs[1][2].conclusion = conclusion; await assert.rejects(qualify(f.inputs), /unsuccessful-required-job/);
  }
  const a = fixture(); a.state.jobs[1].pop(); await assert.rejects(qualify(a.inputs), /missing-or-extra-jobs/);
  const b = fixture(); b.state.jobs[1][0].name = 'unknown'; await assert.rejects(qualify(b.inputs), /wrong-required-jobs/);
  const c = fixture(); c.state.jobs[1][0].name = c.state.jobs[1][1].name; await assert.rejects(qualify(c.inputs), /wrong-required-jobs/);
});
test('wrong event/repository/branch/SHA/checkout/unknown workflow and incomplete API discovery refuse', async () => {
  for (const change of [
    f => f.inputs.eventName = 'pull_request', f => f.inputs.event.action = 'requested',
    f => f.inputs.ownerRepository = 'foreign/repo', f => f.inputs.event.repository.full_name = 'foreign/repo',
    f => f.inputs.event.workflow_run.head_repository.full_name = 'foreign/repo',
    f => f.inputs.event.workflow_run.repository.full_name = 'foreign/repo',
    f => f.inputs.event.workflow_run.workflow_id = 999,
    f => f.inputs.event.workflow_run.event = 'pull_request', f => f.inputs.event.workflow_run.head_branch = 'feature',
    f => f.inputs.event.workflow_run.head_sha = foreign, f => f.inputs.checkout = foreign,
    f => f.inputs.workflowRef = `${repository}/.github/workflows/source-release.yml@refs/heads/feature`,
    f => f.inputs.workflowSha = foreign, f => f.inputs.job = 'foreign',
    f => f.inputs.event.workflow_run.path = '.github/workflows/unknown.yml',
    f => f.state.runs[1].repository.full_name = 'foreign/repo', f => f.state.runs[1].head_sha = foreign,
    f => f.state.runs[1].event = 'workflow_dispatch', f => f.state.runs[1].status = 'unknown', f => f.state.runs[1].pull_requests = [{}],
  ]) { const f = fixture(); change(f); await assert.rejects(qualify(f.inputs)); }
  const f = fixture(); const api = f.inputs.api;
  f.inputs.api = async path => { const x = await api(path); return path.includes('/runs?') ? { ...x, total_count: 101 } : x; };
  await assert.rejects(qualify(f.inputs), /incomplete-run-discovery/);
});
test('main advancing during collection or immediately before mutation refuses without saving a ready phase', async () => {
  const f = fixture(); f.state.main = foreign; await assert.rejects(qualify(f.inputs), /main-advanced/);
  const g = fixture(); const api = g.inputs.api; let mainReads = 0;
  g.inputs.api = async path => path.endsWith('/heads/main') && ++mainReads === 2 ? { object: { sha: foreign } } : api(path);
  await assert.rejects(qualify(g.inputs), /main-advanced/);
  const h = fixture(); const receipt = await qualify(h.inputs); h.state.main = foreign; let writes = 0;
  await assert.rejects(recheck(receipt, () => qualify(h.inputs), () => writes++), /main-advanced/); assert.equal(writes, 0);
});
test('changed qualifying attempt cannot publish notes carrying the earlier run bindings', async () => {
  const f = fixture(); const receipt = await qualify(f.inputs); const fresh = clone(receipt); fresh.qualifyingRuns[1].attempt++;
  let writes = 0; await assert.rejects(recheck(receipt, async () => fresh, () => writes++), /qualification-changed/); assert.equal(writes, 0);
  assert.notEqual(binding(receipt), binding(fresh));
});
test('duplicate callbacks and standard no-release outcome do not force a publication', async () => {
  const f = fixture(); let published = 0, receipt;
  for (let i = 0; i < 2; i++) {
    const result = await runRelease({ qualify: () => qualify(f.inputs), release: async () => false, save: x => receipt = x, previous: () => receipt });
    assert.equal(result.status, 'no-release'); assert.equal(result.computedVersion, null);
    if (result.status === 'published') published++;
  }
  assert.equal(published, 0);
  let invoked = false; const pending = await runRelease({ qualify: async () => ({ eligible: false, status: 'deferred' }), save() {}, release: async () => { invoked = true; } });
  assert.equal(pending.status, 'deferred'); assert.equal(invoked, false);
});
test('no-release return after main advance is refused and computed-version failure diagnostics survive', async () => {
  const f = fixture(); let receipt;
  await assert.rejects(runRelease({ qualify: () => qualify(f.inputs), save: x => receipt = x, previous: () => receipt,
    release: async () => { receipt.computedVersion = '1.2.4'; f.state.main = foreign; return false; } }), /main-advanced/);
  assert.equal(receipt.status, 'failed'); assert.equal(receipt.computedVersion, '1.2.4');
});
test('standard real analyzer computes patch/minor/major and declines build/docs/empty commits', async () => {
  const options = config.plugins[0][1], logger = { log() {} };
  for (const [message, expected] of [['fix: correct cleanup', 'patch'], ['feat: add source release', 'minor'],
    ['feat!: change the contract', 'major'], ['fix: change API\n\nBREAKING CHANGE: old API removed', 'major'],
    ['build: qualify a new producer', null], ['docs: explain releases', null], ['', null]]) {
    assert.equal(await analyzeCommits(options, { cwd: process.cwd(), commits: [{ hash: source, message }], logger }), expected);
  }
  assert.equal(await analyzeCommits(options, { cwd: process.cwd(), commits: [], logger }), null);
  assert.equal(options.releaseRules, undefined);
});
test('explicit source-only plugins omit package publishing and source-main mutations', () => {
  assert.deepEqual(config.branches, ['main']); assert.equal(config.tagFormat, 'v${version}');
  assert.equal(config.repositoryUrl, `https://github.com/${repository}.git`);
  assert.deepEqual(config.plugins.map(x => Array.isArray(x) ? x[0] : x), ['@semantic-release/commit-analyzer', '@semantic-release/release-notes-generator', './eng/source-release/release-gate.mjs', '@semantic-release/github']);
  assert.deepEqual(config.plugins[3][1].assets, []);
  const lock = JSON.parse(readFileSync('package-lock.json'));
  for (const [name, version] of [['semantic-release', '25.0.9'], ['@semantic-release/github', '12.0.10'],
    ['@semantic-release/commit-analyzer', '13.0.1'], ['@semantic-release/release-notes-generator', '14.1.1'], ['@commitlint/cli', '21.2.4']])
    assert.equal(lock.packages[`node_modules/${name}`].version, version);
});
test('actual pinned GitHub hooks make no issue, PR, label, package or registry calls', async () => {
  const calls = [];
  class Octokit { async request(route, args) {
    calls.push(route);
    if (route === 'GET /repos/{owner}/{repo}') return { data: { full_name: repository, clone_url: `https://github.com/${repository}.git`, permissions: { push: true } } };
    assert.equal(route, 'POST /repos/{owner}/{repo}/releases'); assert.equal(args.tag_name, 'v1.2.4');
    return { data: { html_url: `https://github.com/${repository}/releases/tag/v1.2.4`, id: 123 } };
  } }
  const context = { env: { GITHUB_TOKEN: 'inert-unit-fixture' }, cwd: process.cwd(), options: config,
    logger: { log() {}, warn() {} }, branch: { name: 'main', type: 'release', main: true }, commits: [{ hash: source, message: 'fix: correction' }],
    nextRelease: { name: 'v1.2.4', gitTag: 'v1.2.4', version: '1.2.4', notes: 'Source only' }, releases: [] };
  const options = config.plugins[3][1];
  await github.verifyConditions(options, context, { Octokit });
  await github.publish(options, context, { Octokit });
  await github.success(options, context, { Octokit });
  await github.fail(options, context, { Octokit });
  assert.ok(calls.includes('POST /repos/{owner}/{repo}/releases'));
  assert.ok(calls.every(x => ['GET /repos/{owner}/{repo}', 'POST /repos/{owner}/{repo}/releases'].includes(x)));
});
test('GET transport binds official API and refuses redirects, errors and oversized bodies', async () => {
  let observed;
  const api = githubReadApi('inert-unit-fixture', async (url, options) => { observed = { url, options }; return { ok: true, text: async () => '{}' }; });
  await api(`/repos/${repository}/git/ref/heads/main`);
  assert.equal(observed.url, `https://api.github.com/repos/${repository}/git/ref/heads/main`); assert.equal(observed.options.method, 'GET'); assert.equal(observed.options.redirect, 'error');
  await assert.rejects(api('/repos/foreign/repo/actions'), /unexpected-api-path/);
  await assert.rejects(githubReadApi('inert', async () => ({ ok: false }))(`/repos/${repository}/anything`), /github-read-failed/);
  await assert.rejects(githubReadApi('inert', async () => ({ ok: true, text: async () => 'x'.repeat(2_000_001) }))(`/repos/${repository}/anything`), /oversized/);
});
test('workflow separates read-only verification from serialized contents-write publisher and binds callback checkout', () => {
  const workflow = yaml.load(readFileSync('.github/workflows/source-release.yml', 'utf8'));
  assert.deepEqual(workflow.on.workflow_run.workflows, workflows.map(w => w.name)); assert.deepEqual(workflow.on.workflow_run.types, ['completed']);
  assert.deepEqual(workflow.permissions, { contents: 'read', actions: 'read' });
  assert.deepEqual(workflow.jobs.publish.permissions, { contents: 'write', actions: 'read' }); assert.equal(workflow.jobs.verify.permissions, undefined);
  assert.deepEqual(workflow.jobs.publish.concurrency, { group: 'platform-source-release', 'cancel-in-progress': false });
  assert.equal(workflow.jobs.verify.steps[0].with.ref, '${{ github.workflow_sha }}');
  assert.equal(workflow.jobs.publish.steps[0].with.ref, '${{ needs.verify.outputs.source }}');
  assert.equal(workflow.jobs.publish.steps[0].with['persist-credentials'], false);
  assert.equal(workflow.jobs.publish.if, "needs.verify.outputs.eligible == 'true'");
  const serialized = JSON.stringify(workflow); assert.ok(!serialized.includes('NUGET') && !serialized.includes('REGISTRY') && !serialized.includes('secrets.') && !serialized.includes('GITHUB_SHA'));
  const ci = yaml.load(readFileSync('.github/workflows/tuple-ci.yml', 'utf8'));
  assert.ok(ci.jobs.tuple.steps.some(s => s.run?.includes('npm run test:source-release')));
});
test('real entry point refuses local execution without a positive CI identity or any network/write probe', () => {
  const temp = mkdtempSync(join(tmpdir(), 'source-release-negative-'));
  try {
    const env = { ...process.env, GITHUB_ACTIONS: 'false', SOURCE_RELEASE_RECEIPT: join(temp, 'receipt.json') };
    delete env.GITHUB_TOKEN; delete env.GH_TOKEN;
    const result = spawnSync(process.execPath, ['eng/source-release/run.mjs', '--check'], { env, encoding: 'utf8' });
    assert.equal(result.status, 1); assert.equal(result.stderr, '');
    const receipt = JSON.parse(readFileSync(env.SOURCE_RELEASE_RECEIPT)); assert.equal(receipt.reason, 'hosted-callback-required');
  } finally { rmSync(temp, { recursive: true }); }
});

test('actual release hook binds computed version/source and retains bounded public run notes', async () => {
  const temp = mkdtempSync(join(tmpdir(), 'source-release-receipt-')); const original = process.env.SOURCE_RELEASE_RECEIPT;
  try {
    process.env.SOURCE_RELEASE_RECEIPT = join(temp, 'receipt.json');
    const f = fixture(), receipt = await qualify(f.inputs); writeFileSync(process.env.SOURCE_RELEASE_RECEIPT, JSON.stringify(receipt));
    await assert.rejects(verifyRelease({}, { nextRelease: { version: '1.2.4', gitHead: foreign } }), /wrong-release-source/);
    await assert.rejects(verifyRelease({}, { nextRelease: { version: 'forced', gitHead: source } }), /wrong-release-source/);
    assert.equal(JSON.parse(readFileSync(process.env.SOURCE_RELEASE_RECEIPT)).computedVersion, null);
    await verifyRelease({}, { nextRelease: { version: '1.2.4', gitHead: source } });
    const recorded = JSON.parse(readFileSync(process.env.SOURCE_RELEASE_RECEIPT)); assert.equal(recorded.computedVersion, '1.2.4');
    const notes = await generateNotes(); assert.ok(notes.includes(source) && notes.includes('/actions/runs/101') && notes.includes('/actions/runs/102'));
    assert.ok(notes.includes('Native, candidate and Story acceptance: false'));
  } finally {
    if (original === undefined) delete process.env.SOURCE_RELEASE_RECEIPT; else process.env.SOURCE_RELEASE_RECEIPT = original;
    rmSync(temp, { recursive: true });
  }
});

test('verifier uses the reviewed default-branch code to refuse an older callback before its helper existed', async () => {
  const f = fixture(); f.inputs.workflowSha = foreign; f.inputs.checkout = foreign; f.state.main = foreign;
  await assert.rejects(qualify(f.inputs), /main-advanced/);
  assert.ok(f.state.reads.some(x => x.endsWith('/heads/main')));
  const p = fixture(); p.inputs.job = 'publish'; p.inputs.checkout = foreign;
  await assert.rejects(qualify(p.inputs), /wrong-checkout/);
});
test('outer CLI failure preserves actual post-tag release failure phase/version/reason and prior published outcome', async () => {
  const f = fixture(); let receipt;
  await assert.rejects(runRelease({ qualify: () => qualify(f.inputs), save: x => receipt = x, previous: () => receipt,
    release: async () => { receipt = { ...receipt, status: 'ready-to-release', computedVersion: '1.2.4' }; throw new Error('private client failure'); } }));
  assert.equal(receipt.status, 'failed'); assert.equal(receipt.lastCompletedPhase, 'ready-to-release'); assert.equal(receipt.computedVersion, '1.2.4');
  const first = clone(receipt);
  recordCliFailure(new Error('different wrapper failure'), { read: () => receipt, save: x => receipt = x, source });
  assert.deepEqual(receipt, first); assert.equal(receipt.reason, 'semantic-release-failed');
  const published = { ...first, status: 'published', releaseUrls: ['https://github.com/Hexalith/Hexalith.Platform/releases/tag/v1.2.4'] };
  assert.deepEqual(recordCliFailure(new Error('later failure'), { read: () => published, save() {}, source }), published);
});


test('actual pinned notes generator renders feature fix breaking notes and repository links', async () => {
  const commits = [
    { hash: '1'.repeat(40), message: 'feat(ci): describe fixture feature' },
    { hash: '2'.repeat(40), message: 'fix(release): describe fixture correction' },
    { hash: '3'.repeat(40), message: 'feat(api)!: replace fixture contract\n\nBREAKING CHANGE: fixture callers must use version two.' },
  ];
  const options = config.plugins[1][1];
  assert.equal(options.preset, 'conventionalcommits');
  const notes = await renderReleaseNotes(options, {
    cwd: process.cwd(), commits, options: { repositoryUrl: config.repositoryUrl },
    lastRelease: { gitTag: 'v1.2.3', gitHead: '4'.repeat(40) },
    nextRelease: { version: '2.0.0', gitTag: 'v2.0.0', gitHead: '5'.repeat(40) },
    logger: { log() {} },
  });
  assert.ok(notes.trim().length > 0);
  for (const text of ['Features', 'Bug Fixes', 'BREAKING CHANGES', 'describe fixture feature',
    'describe fixture correction', 'replace fixture contract', 'fixture callers must use version two.'])
    assert.ok(notes.includes(text), `Missing rendered fixture content: ${text}`);
  const repositoryUrl = config.repositoryUrl.replace(/\.git$/, '');
  assert.ok(notes.includes(`${repositoryUrl}/compare/v1.2.3...v2.0.0`));
  for (const commit of commits) assert.ok(notes.includes(`${repositoryUrl}/commit/${commit.hash}`));
  const lock = JSON.parse(readFileSync('package-lock.json'));
  assert.equal(lock.packages['node_modules/conventional-changelog-conventionalcommits'].version, '9.3.1');
  assert.equal(lock.packages['node_modules/conventional-changelog-writer'].version, '8.4.0');
});
