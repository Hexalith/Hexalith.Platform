import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import {
  chmodSync, existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync,
  rmSync, symlinkSync, writeFileSync,
} from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import test from 'node:test';
import * as yaml from 'js-yaml';

const workflowPath = '.github/workflows/broker-candidate-qualification.yml';
const workflow = yaml.load(readFileSync(workflowPath, 'utf8'));
const job = workflow.jobs['candidate-01'];
const configuration = readFileSync('docs/implementation/story-4-17-broker-candidate-01.json');
const python = spawnSync('python3', ['-c', 'import sys; print(sys.executable)'], { encoding: 'utf8' }).stdout.trim();
const authorityMessage = 'FAIL: production eligibility FAIL: authenticated AD-26 Architecture/Platform records, '
  + 'Story 3.19 publication/profile binding, Kubernetes rehearsal, restore and Story 4.16 '
  + 'envelope proof are absent; this lane never issues authority\n';
const toolStub = `#!${python}
import json, os, sys
with open(os.environ['FAKE_TOOL_RECEIPTS'], 'a') as stream:
    stream.write(json.dumps([os.path.basename(sys.argv[0]), os.path.realpath(sys.argv[0]), *sys.argv[1:]]) + '\\n')
print('fixture executable')
`;

// External commands and the pinned owner's API are fixture boundaries. All
// workflow guards, status traps, transcript projection and aggregation run unchanged.
const fakeCommands = `#!${python}
import json, os, pathlib, shutil, subprocess, sys
tool = pathlib.Path(sys.argv[0]).name
args = sys.argv[1:]
with open(os.environ['FAKE_CALLS'], 'a') as stream:
    stream.write(json.dumps([tool, *args]) + '\\n')
def finish(text='', status=0):
    print(text, end='')
    raise SystemExit(status)
if tool == 'git':
    if args[:2] == ['rev-parse', 'HEAD']:
        finish(os.environ.get('FAKE_PLATFORM_REVISION', os.environ['GITHUB_SHA']) + '\\n')
    if args[:2] == ['ls-tree', 'HEAD']:
        finish('160000 commit ' + os.environ.get('FAKE_GITLINK', os.environ['EVENTSTORE_REVISION']) + '\\treferences/Hexalith.EventStore\\n')
    if args[:3] == ['-C', 'references/Hexalith.EventStore', 'rev-parse']:
        finish(os.environ.get('FAKE_EVENTSTORE_REVISION', os.environ['EVENTSTORE_REVISION']) + '\\n')
    if 'status' in args:
        dirty = 'FAKE_EVENTSTORE_DIRTY' if '-C' in args else 'FAKE_PLATFORM_DIRTY'
        status = int(os.environ.get('FAKE_EVENTSTORE_STATUS_EXIT' if '-C' in args else 'FAKE_PLATFORM_STATUS_EXIT', '0'))
        if status:
            finish(status=status)
        finish(' M changed-file\\n' if os.environ.get(dirty) else '')
    if args == ['-c', 'submodule.recurse=false', 'submodule', 'update', '--init', '--', 'references/Hexalith.EventStore']:
        finish(status=int(os.environ.get('FAKE_SUBMODULE_EXIT', '0')))
    finish('unexpected git command', 90)
if tool == 'uname':
    finish(('Linux' if args == ['-s'] else 'x86_64') + '\\n')
if tool == 'lsb_release':
    finish(('Ubuntu' if args == ['-is'] else os.environ.get('FAKE_UBUNTU_VERSION', '24.04')) + '\\n')
if tool == 'openssl':
    finish('OpenSSL fixture-version\\n')
if tool == 'readlink':
    finish(os.environ['FAKE_SOCKET'] + '\\n')
if tool == 'docker':
    if args == ['context', 'inspect', '--format', '{{.Endpoints.docker.Host}}']:
        finish(os.environ.get('FAKE_DOCKER_CONTEXT', 'unix:///var/run/docker.sock') + '\\n')
    if args[:2] != ['--host', 'unix://' + os.environ['FAKE_SOCKET']]:
        finish('unexpected docker endpoint', 90)
    if args[2:] == ['info', '--format', '{{.ID}}']:
        finish(os.environ.get('FAKE_DAEMON', 'fixture-daemon') + '\\n')
    if args[2:] == ['info', '--format', '{{.OSType}}/{{.Architecture}}']:
        finish(os.environ.get('FAKE_DOCKER_ARCH', 'linux/x86_64') + '\\n')
    finish('Docker mutation forbidden in offline tests', 90)
if tool == 'python3':
    plain = args[1:] if args[:1] == ['-B'] else args
    script = plain[0] if plain else ''
    if script == 'tools/install-dapr-qualification-tools.py':
        status = int(os.environ.get('FAKE_INSTALL_EXIT', '0'))
        if not status:
            destination = pathlib.Path(plain[plain.index('--destination') + 1])
            for relative in ['runtime/daprd', 'cli/dapr']:
                executable = destination / relative
                executable.parent.mkdir(parents=True, exist_ok=True)
                executable.write_text(${JSON.stringify(toolStub)})
                executable.chmod(0o755)
        finish('private installer diagnostic fixture-secret\\n', status)
    if script == 'tools/qualify-dapr-brokers.py':
        expected_runtime = pathlib.Path(os.environ['QUALIFICATION_TOOLS']) / 'runtime/daprd'
        selected_runtime = pathlib.Path(os.environ['QUALIFICATION_DAPRD'])
        selected_cli = shutil.which('dapr')
        expected_cli = pathlib.Path(os.environ['QUALIFICATION_TOOLS']) / 'cli/dapr'
        if selected_runtime != expected_runtime or selected_cli != str(expected_cli):
            finish('fixture refused wrong selected executables\\n', 86)
        subprocess.run([str(selected_runtime), '--version'], check=True)
        subprocess.run([selected_cli, '--version'], check=True)
        packet = pathlib.Path(plain[plain.index('--output') + 1])
        mode = os.environ.get('FAKE_PACKET', 'valid')
        if mode != 'missing':
            packet.mkdir()
            (packet / 'packet.json').write_text('broken' if mode == 'broken' else json.dumps({
                'schemaVersion': 3, 'productionEligible': False, 'selectedBroker': None}))
            (packet / 'cleanup.json').write_text(json.dumps({'complete': os.environ.get('FAKE_CLEANUP', 'true') == 'true'}))
            if mode == 'symlink':
                (packet / 'escaping-link').symlink_to(os.environ['FAKE_CALLS'])
        finish('private producer diagnostic fixture-secret\\n', int(os.environ.get('FAKE_CAPTURE_EXIT', '0')))
    if script == 'tools/validate-dapr-broker-qualification.py':
        mode = 'integrity' if '--allow-failures' in plain else 'authority' if '--require-production-authority' in plain else 'technical'
        packet = pathlib.Path(plain[1])
        if not (packet / 'packet.json').is_file() or os.environ.get('FAKE_PACKET') == 'broken':
            finish('FAIL: missing or invalid evidence\\n', 2)
        status = int(os.environ.get('FAKE_' + mode.upper() + '_EXIT', '1' if mode == 'authority' else '0'))
        output = os.environ.get('FAKE_AUTHORITY_TEXT', ${JSON.stringify(authorityMessage)}) if mode == 'authority' else 'fixture ' + mode + '\\n'
        finish(output, status)
    os.execv(${JSON.stringify(python)}, [${JSON.stringify(python)}, *args])
finish('unhandled fake command', 90)
`;

function fixture(overrides = {}, definition = job) {
  const root = mkdtempSync(join(tmpdir(), 'broker-workflow-'));
  const workspace = join(root, 'workspace');
  const temporary = join(root, 'runner-temp');
  const bin = join(root, 'bin');
  mkdirSync(join(workspace, 'references'), { recursive: true });
  mkdirSync(join(workspace, 'docs/implementation'), { recursive: true });
  mkdirSync(temporary);
  mkdirSync(bin);
  // Existing CI runs source-release controls before submodule initialization.
  // Keep the owner interface self-contained rather than requiring a local checkout.
  const owner = join(workspace, 'references/Hexalith.EventStore');
  mkdirSync(join(owner, 'tools'), { recursive: true });
  mkdirSync(join(owner, 'deploy/dapr/qualification'), { recursive: true });
  writeFileSync(join(owner, job.env.QUALIFICATION_PROFILE), JSON.stringify({ fixtureOwnerContract: true }));
  writeFileSync(join(owner, 'tools/dapr_broker_qualification.py'), `
import os
BROKERS = ('rabbitmq', 'kafka')
SCENARIOS = tuple(range(12))
def validate_profile(profile, root):
    assert profile == {'fixtureOwnerContract': True}
    if os.environ.get('FAKE_OWNER_PROFILE_REJECT'):
        raise ValueError('fixture owner profile refusal')
`);
  writeFileSync(join(owner, 'tools/dapr_platform_broker_configuration.py'), `
import json, os
def load_configuration(path):
    if os.environ.get('FAKE_OWNER_CONFIG_REJECT'):
        raise ValueError('fixture owner configuration refusal')
    data = path.read_bytes()
    return data, json.loads(data)
`);
  writeFileSync(join(owner, 'tools/dapr_runtime_qualification.py'), `
PLATFORM_PROFILE = '${job.env.QUALIFICATION_PROFILE}'
def profile_path(profile):
    return PLATFORM_PROFILE
`);
  writeFileSync(join(workspace, 'docs/implementation/story-4-17-broker-candidate-01.json'), configuration);
  const fake = join(bin, 'fake');
  writeFileSync(fake, fakeCommands);
  chmodSync(fake, 0o755);
  for (const name of ['git', 'uname', 'lsb_release', 'openssl', 'readlink', 'docker', 'python3']) {
    symlinkSync(fake, join(bin, name));
  }
  const socket = join(root, 'docker.sock');
  const bound = spawnSync(python, ['-c', 'import socket, sys; channel = socket.socket(socket.AF_UNIX); channel.bind(sys.argv[1]); channel.close()', socket]);
  assert.equal(bound.status, 0, bound.stderr.toString());
  const env = {
    ...process.env, ...definition.env, PATH: `${bin}:${process.env.PATH}`,
    RUNNER_TEMP: temporary, GITHUB_WORKSPACE: workspace,
    GITHUB_ENV: join(root, 'github-env'), GITHUB_OUTPUT: join(root, 'github-output'),
    GITHUB_STEP_SUMMARY: join(root, 'summary'), GITHUB_SHA: 'a'.repeat(40),
    GITHUB_REPOSITORY: 'Hexalith/Hexalith.Platform', GITHUB_RUN_ID: '1234', GITHUB_RUN_ATTEMPT: '2',
    GITHUB_JOB: 'candidate-01', RUNNER_ENVIRONMENT: 'github-hosted', RUNNER_OS: 'Linux', RUNNER_ARCH: 'X64',
    ImageOS: 'ubuntu24', ImageVersion: '20261111.1.0',
    FAKE_SOCKET: socket, FAKE_CALLS: join(root, 'calls.jsonl'), FAKE_TOOL_RECEIPTS: join(root, 'tool-receipts.jsonl'),
  };
  for (const key of ['DOCKER_HOST', 'DOCKER_CONTEXT', 'DOCKER_TLS_VERIFY', 'DOCKER_CERT_PATH', 'EVIDENCE', 'QUALIFICATION_OUTPUT']) delete env[key];
  Object.assign(env, overrides);
  const outcomes = {};
  const outputs = {};
  function bindings(path) {
    if (!existsSync(path)) return {};
    return Object.fromEntries(readFileSync(path, 'utf8').trim().split('\n').filter(Boolean).map(line => {
      const index = line.indexOf('=');
      return [line.slice(0, index), line.slice(index + 1)];
    }));
  }
  function execute(id) {
    const step = definition.steps.find(item => item.id === id || item.name === id);
    assert.ok(step?.run, `missing executable step ${id}`);
    const stepEnv = { ...env };
    for (const [key, value] of Object.entries(step.env ?? {})) {
      stepEnv[key] = value.replace(/\$\{\{ steps\.([\w-]+)\.outcome \}\}/g, (_, name) => outcomes[name] ?? 'skipped')
        .replace(/\$\{\{ steps\.([\w-]+)\.outputs\.([\w-]+) \}\}/g, (_, name, key) => outputs[name]?.[key] ?? '');
    }
    writeFileSync(env.GITHUB_OUTPUT, '');
    const result = spawnSync('bash', ['--noprofile', '--norc', '-e', '-o', 'pipefail', '-c', step.run], {
      cwd: workspace, env: stepEnv, encoding: 'utf8', timeout: 20000,
    });
    assert.equal(result.error, undefined, result.error?.message);
    if (step.id) {
      outcomes[step.id] = result.status === 0 ? 'success' : 'failure';
      outputs[step.id] = bindings(env.GITHUB_OUTPUT);
    }
    Object.assign(env, bindings(env.GITHUB_ENV));
    return result;
  }
  function run({ checkout = 'success', cancel = false, omitBootstrap = false, beforeReport } = {}) {
    if (omitBootstrap) outcomes.bootstrap = 'failure';
    else assert.equal(execute('bootstrap').status, 0);
    outcomes.checkout = checkout;
    let failed = omitBootstrap || checkout !== 'success';
    for (const id of ['sources', 'install', 'capture', 'integrity', 'technical', 'authority']) {
      const step = definition.steps.find(item => item.id === id);
      if (cancel && id === 'capture') {
        outcomes[id] = 'cancelled';
        failed = true;
      } else if (cancel && ['integrity', 'technical', 'authority'].includes(id)) {
        outcomes[id] = 'skipped';
      } else if (step.if ? outcomes.sources !== 'success' : failed) {
        outcomes[id] = 'skipped';
      } else {
        const result = execute(id);
        if (result.status !== 0 && !step['continue-on-error']) failed = true;
      }
    }
    beforeReport?.({ env, root });
    const report = execute('report');
    outcomes.upload = 'success'; // The remote action is inspected, never called.
    const gate = execute('Enforce bounded result after independent artifact attempt');
    return { gate, reporting: report, report: JSON.parse(readFileSync(join(outputs.report.evidence, 'outcomes.json'), 'utf8')) };
  }
  return {
    root, env, workspace, temporary, outcomes, outputs, execute, run,
    calls: () => existsSync(env.FAKE_CALLS) ? readFileSync(env.FAKE_CALLS, 'utf8').trim().split('\n').filter(Boolean).map(JSON.parse) : [],
    toolReceipts: () => existsSync(env.FAKE_TOOL_RECEIPTS) ? readFileSync(env.FAKE_TOOL_RECEIPTS, 'utf8').trim().split('\n').filter(Boolean).map(JSON.parse) : [],
    cleanup: () => rmSync(root, { recursive: true, force: true }),
  };
}

function withFixture(options, action, definition = job) {
  const current = fixture(options, definition);
  try { action(current); } finally { current.cleanup(); }
}

function invocations(current, name) {
  return current.calls().filter(row => row[0] === 'python3' && row.includes(`tools/${name}`));
}

test('candidate workflow is a single fixed manual read-only hosted job with pinned actions', () => {
  assert.deepEqual(workflow.on, { workflow_dispatch: null });
  assert.deepEqual(workflow.permissions, { contents: 'read' });
  assert.deepEqual(Object.keys(workflow.jobs), ['candidate-01']);
  assert.equal(job['runs-on'], 'ubuntu-24.04');
  assert.equal(job['timeout-minutes'], 40);
  assert.equal(job.env.EVENTSTORE_REVISION, '02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213');
  assert.equal(job.env.CONFIGURATION_SHA256, '472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc');
  assert.equal(job.env.QUALIFICATION_PROFILE, 'deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json');
  assert.equal(job.steps[0].id, 'bootstrap');
  const checkout = job.steps.find(step => step.id === 'checkout');
  assert.equal(checkout.uses, 'actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1');
  assert.deepEqual(checkout.with, { ref: '${{ github.sha }}', 'fetch-depth': 1, submodules: false, 'persist-credentials': false });
  assert.deepEqual(job.steps.filter(step => step.uses).map(step => step.uses), [
    checkout.uses, 'actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a',
  ]);
  assert.ok(!JSON.stringify(job).includes('inputs.'));
  assert.ok(!JSON.stringify(job.env).includes('runner.'));
  for (const step of job.steps.filter(step => step.run)) {
    const syntax = spawnSync('bash', ['-n'], { input: step.run, encoding: 'utf8' });
    assert.equal(syntax.status, 0, `${step.name}: ${syntax.stderr}`);
  }
});

test('intended hosted run binds the real allocation and pairs all four invocations with the separate original', () => {
  withFixture({}, current => {
    const { gate, report } = current.run();
    assert.equal(gate.status, 0, gate.stderr);
    assert.equal(report.boundedTechnicalSuccess, true);
    assert.equal(report.expectedAuthorityRejection, true);
    assert.equal(report.productionEligibility, 'FAIL');
    assert.equal(report.selectedBroker, null);
    assert.equal(report.platformStory4_17, 'in-progress');
    assert.equal(report.eventStoreStory3_21, 'in-progress');
    assert.deepEqual(report.evidenceProcessingErrors, []);
    assert.equal(report.phases.sources, 'complete');
    assert.equal(report.observations['platform-revision'], current.env.GITHUB_SHA);
    assert.equal(report.observations['eventstore-revision'], job.env.EVENTSTORE_REVISION);
    assert.equal(report.observations['configuration-sha256'], job.env.CONFIGURATION_SHA256);
    const allocation = JSON.parse(readFileSync(current.env.QUALIFICATION_EXCLUSIVE_WORKER));
    assert.deepEqual(allocation, { exclusive: true, endpoint: `unix://${current.env.FAKE_SOCKET}`, daemonId: 'fixture-daemon', workerId: '1234-candidate-01-2' });
    const witness = JSON.parse(readFileSync(join(current.env.EVIDENCE, 'worker.json')));
    assert.equal(witness.platformRevision, current.env.GITHUB_SHA);
    assert.equal(witness.eventStoreRevision, job.env.EVENTSTORE_REVISION);
    assert.equal(witness.socketIdentity.length, 2);
    const producer = invocations(current, 'qualify-dapr-brokers.py');
    const verifiers = invocations(current, 'validate-dapr-broker-qualification.py');
    assert.deepEqual(producer, [['python3', '-B', 'tools/qualify-dapr-brokers.py', '--profile', job.env.QUALIFICATION_PROFILE,
      '--platform-config', current.env.PLATFORM_CONFIG, '--brokers', 'rabbitmq,kafka', '--exclusive-worker',
      current.env.QUALIFICATION_EXCLUSIVE_WORKER, '--output', current.env.QUALIFICATION_OUTPUT]]);
    assert.deepEqual(verifiers, [
      ['python3', '-B', 'tools/validate-dapr-broker-qualification.py', current.env.QUALIFICATION_OUTPUT, '--platform-config', current.env.PLATFORM_CONFIG, '--allow-failures'],
      ['python3', '-B', 'tools/validate-dapr-broker-qualification.py', current.env.QUALIFICATION_OUTPUT, '--platform-config', current.env.PLATFORM_CONFIG],
      ['python3', '-B', 'tools/validate-dapr-broker-qualification.py', current.env.QUALIFICATION_OUTPUT, '--platform-config', current.env.PLATFORM_CONFIG, '--require-production-authority'],
    ]);
    assert.ok(!current.env.PLATFORM_CONFIG.startsWith(current.env.QUALIFICATION_OUTPUT));
    assert.deepEqual(invocations(current, 'install-dapr-qualification-tools.py'), [[
      'python3', '-B', 'tools/install-dapr-qualification-tools.py', '--profile', job.env.QUALIFICATION_PROFILE,
      '--destination', current.env.QUALIFICATION_TOOLS,
    ]]);
    assert.deepEqual(current.toolReceipts(), [
      ['daprd', join(current.env.QUALIFICATION_TOOLS, 'runtime/daprd'), '--version'],
      ['dapr', join(current.env.QUALIFICATION_TOOLS, 'cli/dapr'), '--version'],
    ]);
    assert.ok(current.calls().some(row => row.join(' ') === 'git -c submodule.recurse=false submodule update --init -- references/Hexalith.EventStore'));
    const summaries = readFileSync(current.env.GITHUB_STEP_SUMMARY, 'utf8');
    assert.match(summaries, /authority: failure; raw exit = 1/);
    assert.match(summaries, /Production eligibility: FAIL; selected broker: null/);
  });
});

const guards = [
  ['self-hosted worker', { RUNNER_ENVIRONMENT: 'self-hosted' }],
  ['wrong runner OS', { RUNNER_OS: 'Windows' }],
  ['wrong architecture', { RUNNER_ARCH: 'ARM64' }],
  ['wrong image', { ImageOS: 'ubuntu22' }],
  ['missing image identity', { ImageVersion: '' }],
  ['wrong actual OS', { FAKE_UBUNTU_VERSION: '22.04' }],
  ['wrong Platform revision', { FAKE_PLATFORM_REVISION: 'b'.repeat(40) }],
  ['wrong gitlink', { FAKE_GITLINK: 'b'.repeat(40) }],
  ['wrong EventStore revision', { FAKE_EVENTSTORE_REVISION: 'b'.repeat(40) }],
  ['dirty Platform', { FAKE_PLATFORM_DIRTY: '1' }],
  ['dirty EventStore', { FAKE_EVENTSTORE_DIRTY: '1' }],
  ['empty-output Platform status error', { FAKE_PLATFORM_STATUS_EXIT: '31' }],
  ['empty-output EventStore status error', { FAKE_EVENTSTORE_STATUS_EXIT: '32' }],
  ['submodule setup error', { FAKE_SUBMODULE_EXIT: '27' }],
  ['owner profile rejection', { FAKE_OWNER_PROFILE_REJECT: '1' }],
  ['owner configuration rejection', { FAKE_OWNER_CONFIG_REJECT: '1' }],
  ['remote Docker context', { FAKE_DOCKER_CONTEXT: 'tcp://remote:2375' }],
  ['conflicting Docker selection', { DOCKER_HOST: 'unix:///other/socket' }],
  ['absent daemon identity', { FAKE_DAEMON: '' }],
  ['unsupported daemon runtime', { FAKE_DOCKER_ARCH: 'windows/amd64' }],
];
for (const [name, options] of guards) {
  test(`unready ${name} fails before installer, producer or allocation and retains diagnostics`, () => {
    withFixture(options, current => {
      const { gate, report } = current.run();
      assert.notEqual(gate.status, 0);
      assert.equal(report.boundedTechnicalSuccess, false);
      assert.equal(report.outcomes.sources.outcome, 'failure');
      assert.match(report.phases.sources, /^[a-z-]+$/);
      assert.equal(report.outcomes.capture.exitCode, null);
      assert.equal(invocations(current, 'install-dapr-qualification-tools.py').length, 0);
      assert.equal(invocations(current, 'qualify-dapr-brokers.py').length, 0);
      assert.equal(invocations(current, 'validate-dapr-broker-qualification.py').length, 0);
      assert.equal(existsSync(current.env.QUALIFICATION_EXCLUSIVE_WORKER), false);
      assert.ok(existsSync(join(current.env.EVIDENCE, 'bootstrap.json')));
      assert.ok(existsSync(join(current.env.EVIDENCE, 'transcripts.json')));
      if (options.FAKE_PLATFORM_STATUS_EXIT) {
        assert.equal(report.outcomes.sources.exitCode, 31);
        assert.equal(report.phases.sources, 'platform-status-query');
      }
      if (options.FAKE_EVENTSTORE_STATUS_EXIT) {
        assert.equal(report.outcomes.sources.exitCode, 32);
        assert.equal(report.phases.sources, 'eventstore-status-query');
      }
    });
  });
}

for (const change of ['hash', 'configuration symlink', 'reused base', 'dangling destination']) {
  test(`${change} is rejected before any installation or producer`, () => {
    withFixture({}, current => {
      if (change === 'hash') writeFileSync(join(current.workspace, 'docs/implementation/story-4-17-broker-candidate-01.json'), '{}');
      if (change === 'configuration symlink') {
        const path = join(current.workspace, 'docs/implementation/story-4-17-broker-candidate-01.json');
        rmSync(path);
        symlinkSync(resolve('docs/implementation/story-4-17-broker-candidate-01.json'), path);
      }
      if (change === 'reused base') mkdirSync(join(current.temporary, 'platform-broker-candidate-01'));
      if (change === 'dangling destination') symlinkSync(join(current.root, 'absent'), join(current.temporary, 'platform-broker-candidate-01'));
      const { gate, report } = current.run();
      assert.notEqual(gate.status, 0);
      assert.equal(report.outcomes.sources.outcome, 'failure');
      assert.equal(invocations(current, 'install-dapr-qualification-tools.py').length, 0);
      assert.equal(invocations(current, 'qualify-dapr-brokers.py').length, 0);
      if (change === 'hash') {
        assert.equal(report.phases.sources, 'configuration-hash-match');
        assert.equal(report.observations['configuration-sha256'], createHash('sha256').update('{}').digest('hex'));
        assert.equal(existsSync(join(current.env.EVIDENCE, 'original-platform-config.json')), false);
      }
    });
  });
}

for (const [name, options, field, actual] of [
  ['Platform revision', { FAKE_PLATFORM_REVISION: 'b'.repeat(40) }, 'platform-revision', 'b'.repeat(40)],
  ['EventStore revision', { FAKE_EVENTSTORE_REVISION: 'c'.repeat(40) }, 'eventstore-revision', 'c'.repeat(40)],
  ['EventStore gitlink', { FAKE_GITLINK: 'd'.repeat(40) }, 'eventstore-gitlink', `160000 commit ${'d'.repeat(40)}\treferences/Hexalith.EventStore`],
]) {
  test(`rejected ${name} retains its safe actual identity before comparison`, () => {
    withFixture(options, current => {
      const { report } = current.run();
      assert.equal(report.observations[field], actual);
      assert.equal(existsSync(join(current.env.EVIDENCE, 'original-platform-config.json')), false);
    });
  });
}

for (const [name, original, changed] of [
  ['runtime', 'QUALIFICATION_TOOLS/runtime/daprd', 'QUALIFICATION_TOOLS/runtime/missing-daprd'],
  ['CLI', 'QUALIFICATION_TOOLS/cli:$PATH', 'QUALIFICATION_TOOLS/missing-cli:$PATH'],
]) {
  test(`a wrong selected ${name} executable fails through fixture capture`, () => {
    const changedJob = structuredClone(job);
    const capture = changedJob.steps.find(step => step.id === 'capture');
    assert.ok(capture.run.includes(original));
    capture.run = capture.run.replace(original, changed);
    withFixture({}, current => {
      const { gate, report } = current.run();
      assert.notEqual(gate.status, 0);
      assert.equal(report.outcomes.capture.exitCode, 86);
      assert.equal(report.phases.capture, 'producer-execution');
      assert.equal(report.boundedTechnicalSuccess, false);
      assert.deepEqual(current.toolReceipts(), []);
      assert.equal(invocations(current, 'validate-dapr-broker-qualification.py').length, 3);
    }, changedJob);
  });
}

for (const options of [
  { FAKE_CAPTURE_EXIT: '19', FAKE_TECHNICAL_EXIT: '7' },
  { FAKE_CAPTURE_EXIT: '19' },
  { FAKE_TECHNICAL_EXIT: '7' },
  { FAKE_INTEGRITY_EXIT: '6' },
  { FAKE_AUTHORITY_EXIT: '2' },
  { FAKE_AUTHORITY_EXIT: '0' },
  { FAKE_AUTHORITY_TEXT: 'FAIL: unexpected internal error\n' },
]) {
  test(`independent evaluations preserve real failures ${JSON.stringify(options)}`, () => {
    withFixture(options, current => {
      const { gate, report } = current.run();
      assert.notEqual(gate.status, 0);
      assert.equal(report.boundedTechnicalSuccess, false);
      assert.equal(invocations(current, 'validate-dapr-broker-qualification.py').length, 3);
      for (const [key, value] of Object.entries(options)) {
        if (key.endsWith('_EXIT')) assert.equal(report.outcomes[key.slice(5, -5).toLowerCase()].exitCode, Number(value));
      }
      if (options.FAKE_INTEGRITY_EXIT) {
        assert.equal(report.cleanupReceiptComplete, true);
        assert.equal(report.cleanupComplete, false);
      }
    });
  });
}

for (const mode of ['missing', 'broken', 'symlink']) {
  test(`${mode} packet grants no bounded qualification and retains diagnostics`, () => {
    withFixture({ FAKE_PACKET: mode }, current => {
      const { gate, report } = current.run();
      assert.notEqual(gate.status, 0);
      assert.equal(report.boundedTechnicalSuccess, false);
      assert.equal(invocations(current, 'validate-dapr-broker-qualification.py').length, 3);
      if (mode !== 'symlink') assert.equal(report.integrityVerified, false);
      assert.equal(report.packetRetained, mode === 'broken');
    });
  });
}

test('setup failure still runs all independent verifiers and reports skipped capture as incomplete', () => {
  withFixture({ FAKE_INSTALL_EXIT: '23' }, current => {
    const { gate, report } = current.run();
    assert.notEqual(gate.status, 0);
    assert.equal(report.outcomes.install.exitCode, 23);
    assert.equal(report.phases.install, 'tool-installation');
    assert.deepEqual(report.outcomes.capture, { outcome: 'skipped', exitCode: null });
    assert.equal(invocations(current, 'validate-dapr-broker-qualification.py').length, 3);
    assert.equal(report.cleanupComplete, false);
  });
});

for (const options of [{ checkout: 'failure' }, { cancel: true }, { omitBootstrap: true }]) {
  test(`earlier setup/cancellation remains incomplete ${JSON.stringify(options)}`, () => {
    withFixture({}, current => {
      const { gate, report } = current.run(options);
      assert.notEqual(gate.status, 0);
      assert.equal(report.boundedTechnicalSuccess, false);
      assert.equal(report.cleanupComplete, false);
      assert.equal(report.outcomes.capture.exitCode, null);
      assert.ok(existsSync(join(current.outputs.report.evidence, 'outcomes.json')));
    });
  });
}

test('cleanup failure cannot receive bounded success even when command statuses otherwise pass', () => {
  withFixture({ FAKE_CLEANUP: 'false' }, current => {
    const { gate, report } = current.run();
    assert.notEqual(gate.status, 0);
    assert.equal(report.cleanupComplete, false);
    assert.equal(report.boundedTechnicalSuccess, false);
    assert.ok(existsSync(join(current.env.EVIDENCE, 'packet/cleanup.json')));
  });
});

test('a missing raw exit or cancelled outcome cannot borrow a completed capture success', () => {
  withFixture({}, current => {
    current.run();
    const exit = join(current.env.EVIDENCE, 'capture.exit');
    rmSync(exit);
    rmSync(join(current.env.EVIDENCE, 'packet'), { recursive: true });
    assert.equal(current.execute('report').status, 0);
    let report = JSON.parse(readFileSync(join(current.env.EVIDENCE, 'outcomes.json')));
    assert.deepEqual(report.outcomes.capture, { outcome: 'success', exitCode: null });
    assert.equal(report.boundedTechnicalSuccess, false);
    writeFileSync(exit, '0\n');
    current.outcomes.capture = 'cancelled';
    // Re-report into a clean projection target; the source packet is preserved.
    rmSync(join(current.env.EVIDENCE, 'packet'), { recursive: true });
    assert.equal(current.execute('report').status, 0);
    report = JSON.parse(readFileSync(join(current.env.EVIDENCE, 'outcomes.json')));
    assert.deepEqual(report.outcomes.capture, { outcome: 'cancelled', exitCode: 0 });
    assert.equal(report.boundedTechnicalSuccess, false);
  });
});

for (const [name, inject, expectedCode] of [
  ['unreadable status kind', ({ env }) => {
    const path = join(env.EVIDENCE, 'capture.exit');
    rmSync(path);
    mkdirSync(path);
  }, 'status-read-capture'],
  ['invalid argv encoding', ({ env }) => writeFileSync(join(env.EVIDENCE, 'private-transcripts/capture.argv'), Buffer.from([255])), 'transcript-argv-decode-capture'],
  ['invalid authority encoding', ({ env }) => writeFileSync(join(env.EVIDENCE, 'private-transcripts/authority.stdout'), Buffer.from([255])), 'authority-decode'],
  ['packet projection failure', ({ env }) => mkdirSync(join(env.EVIDENCE, 'packet')), 'packet-projection'],
  ['summary write failure', ({ env, root }) => { env.GITHUB_STEP_SUMMARY = root; }, 'summary-write'],
]) {
  test(`actual ${name} preserves outcomes, safe evidence and independent artifact fallback`, () => {
    withFixture({}, current => {
      const { gate, report, reporting } = current.run({ beforeReport: inject });
      assert.notEqual(reporting.status, 0);
      assert.notEqual(gate.status, 0);
      assert.ok(report.evidenceProcessingErrors.includes(expectedCode));
      assert.equal(report.outcomes.capture.outcome, 'success');
      assert.equal(report.outcomes.authority.exitCode, 1);
      assert.equal(report.boundedTechnicalSuccess, false);
      assert.equal(report.cleanupComplete, false);
      assert.equal(report.integrityVerified, false);
      assert.equal(current.outputs.report['bounded-success'], 'false');
      const upload = job.steps.find(step => step.id === 'upload');
      assert.equal(upload.if, 'always()');
      assert.ok(existsSync(current.outputs.report.evidence));
      assert.equal(current.outputs.report.evidence, current.outputs.bootstrap.evidence);
      // Report outputs can disappear under interruption; the independent upload
      // expression still selects the already-created bootstrap evidence root.
      assert.ok(upload.with.path.includes('steps.report.outputs.evidence || steps.bootstrap.outputs.evidence ||'));
      current.outputs.report = {};
      const fallback = current.outputs.report.evidence || current.outputs.bootstrap.evidence;
      assert.ok(fallback.startsWith(current.temporary + '/'));
      assert.ok(existsSync(join(fallback, 'outcomes.json')));
      for (const path of readdirSync(fallback, { recursive: true }).filter(path => String(path).endsWith('.json') && !String(path).startsWith('private-transcripts'))) {
        assert.ok(!readFileSync(join(fallback, String(path)), 'utf8').includes('fixture-secret'));
      }
      if (name !== 'summary write failure') {
        assert.match(readFileSync(current.env.GITHUB_STEP_SUMMARY, 'utf8'), /Evidence-processing codes:/);
      }
    });
  });
}

test('summary and sanitized upload are independent always attempts, with raw transcripts excluded', () => {
  const report = job.steps.find(step => step.id === 'report');
  const upload = job.steps.find(step => step.id === 'upload');
  const gate = job.steps.at(-1);
  assert.equal(report.if, 'always()');
  assert.equal(upload.if, 'always()');
  assert.equal(gate.if, 'always()');
  assert.ok(job.steps.indexOf(report) < job.steps.indexOf(upload));
  assert.ok(job.steps.indexOf(upload) < job.steps.indexOf(gate));
  assert.equal(upload.with['retention-days'], 30);
  assert.equal(upload.with['include-hidden-files'], true);
  assert.equal(upload.with['if-no-files-found'], 'error');
  const evidencePath = "${{ steps.report.outputs.evidence || steps.bootstrap.outputs.evidence || format('{0}/platform-broker-candidate-01-evidence-unavailable', runner.temp) }}";
  assert.equal(upload.with.path, `${evidencePath}/**\n!${evidencePath}/private-transcripts/**\n`);
  for (const id of ['integrity', 'technical', 'authority']) {
    const step = job.steps.find(item => item.id === id);
    assert.equal(step.if, "${{ always() && steps.sources.outcome == 'success' }}");
    assert.equal(step['continue-on-error'], true);
  }
  withFixture({}, current => {
    current.run();
    const projected = JSON.parse(readFileSync(join(current.env.EVIDENCE, 'transcripts.json')));
    assert.ok(projected['capture.stdout'].bytes > 0);
    assert.match(projected['capture.stdout'].sha256, /^[a-f0-9]{64}$/);
    assert.equal(projected['capture.stdout'].output, undefined);
    assert.equal(projected['capture.argv'].argv[2], 'tools/qualify-dapr-brokers.py');
    assert.match(readFileSync(join(current.env.EVIDENCE, 'private-transcripts/capture.stdout'), 'utf8'), /fixture-secret/);
    const artifactFiles = readdirSync(current.env.EVIDENCE, { recursive: true }).filter(name => !String(name).startsWith('private-transcripts'));
    assert.ok(artifactFiles.includes('packet/cleanup.json'));
    assert.ok(artifactFiles.includes('original-platform-config.json'));
    for (const name of artifactFiles.filter(name => String(name).endsWith('.json'))) {
      assert.ok(!readFileSync(join(current.env.EVIDENCE, String(name)), 'utf8').includes('fixture-secret'));
    }
    current.outcomes.upload = 'failure';
    assert.notEqual(current.execute(gate.name).status, 0);
    current.outcomes.upload = 'success';
    current.outcomes.report = 'failure';
    assert.notEqual(current.execute(gate.name).status, 0);
  });
});
