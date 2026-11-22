#!/usr/bin/env python3
"""Story 2.10 local lifecycle scenarios on Parties with the pinned released hexalith-module.

Stdlib only. Drives Parties' committed lifecycle fixtures through their control.json files, acts only
on run ids this driver issued, and runs `down` for every issued owner on every exit path.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import signal
import socket
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

MANIFEST = 'module/hexalith.module-manifest.v2.json'
OWNER, ATTACHED = 'lifecycle-package', 'attached-lifecycle-package'
SUITES = ('tests/Hexalith.Parties.Platform.LifecycleTests/Hexalith.Parties.Platform.LifecycleTests.csproj',
          'tests/Hexalith.Parties.Platform.AttachedLifecycleTests/Hexalith.Parties.Platform.AttachedLifecycleTests.csproj')
SCENARIOS = ('success', 'failure', 'startup-timeout', 'cancellation', 'collision', 'owner-finish-hold',
             'owner-cancel-attached', 'attached-failure-expiry', 'failure-then-cancellation')
CI_RUNS = tuple('https://github.com/Hexalith/Hexalith.Parties/actions/runs/' + run
                for run in ('42521615234', '42526256372', '42528046681'))
DATA = Path.home() / '.local/share/hexalith-builds'  # 6.3.0 scrubs XDG_DATA_HOME from children: never relocate.
STATE, CONTROLS = DATA / 'g4-runs', DATA / 'ci-qualification'
DIAGNOSTICS = STATE / 'diagnostics'
SHORT_HOLD = 45


class Failure(Exception):
    """A scenario step that could not be observed; the message never carries a path or tool text."""


class Refused(Failure):
    """An owner exited before execution: a product refusal or missing prerequisite stops the campaign."""


class InventoryFailure(Failure):
    """Docker or the runner could not supply a verifiable inventory."""


# ---- Pure projections and verdicts (unit-tested) ----

def now():
    return datetime.now(timezone.utc).isoformat()


def read_json(path):
    try:
        return json.loads(Path(path).read_text())
    except (OSError, ValueError):
        return None  # Missing, or a producer's partial write; the caller polls again.


def last_json(text):
    for line in reversed((text or '').splitlines()):
        try:
            value = json.loads(line)
        except ValueError:
            continue
        if isinstance(value, dict):
            return value
    return {}


def rule_ids(output):
    rules = {item.get('ruleId') for item in output.get('diagnostics') or [] if isinstance(item, dict)}
    rules.add((output.get('outcome') or {}).get('ruleId'))
    return sorted(rule for rule in rules if rule)


def reasons(output):
    return sorted({item.get('field') for item in output.get('diagnostics') or [] if isinstance(item, dict) and item.get('field')})


def extract(doc):
    """Project the bounded public fields of one hexalith.test-result.v1 document."""
    if not isinstance(doc, dict):
        return None
    bindings = doc.get('bindings') or {}
    run, tool = bindings.get('run') or {}, bindings.get('tool') or {}
    startup = (bindings.get('deadlines') or {}).get('startup') or {}
    terminal, cleanup, retention = doc.get('terminal') or {}, doc.get('cleanup') or {}, doc.get('retention') or {}
    return {'runId': run.get('runId'), 'role': run.get('role'), 'ownerRunId': run.get('ownerRunId'),
            'environmentId': run.get('environmentId'), 'attemptId': run.get('attemptId'),
            'toolVersion': tool.get('version'), 'toolContentIdentity': tool.get('contentIdentity'),
            'outcome': terminal.get('outcome'), 'cause': terminal.get('cause'), 'recordedAt': terminal.get('recordedAt'),
            'diagnosticIds': terminal.get('diagnosticIds') or [],
            'startup': {'effectiveSeconds': startup.get('effectiveSeconds'),
                        'actualDurationSeconds': startup.get('actualDurationSeconds'),
                        'override': (startup.get('override') or {}).get('state')},
            'retention': retention.get('disposition'), 'cleanup': cleanup.get('disposition'),
            'cleanupObservedAt': cleanup.get('observedAt'), 'leftovers': cleanup.get('leftoverResourceIds') or []}


def hold_view(item):
    if not isinstance(item, dict):
        return None
    keys = ('attachedRunId', 'originalAttachmentRequestId', 'requestedHoldSeconds', 'effectiveHoldSeconds',
            'acceptedAtUtc', 'expiresAtUtc', 'endedAtUtc', 'endReason', 'outcome')
    return {**{key: item.get(key) for key in keys}, 'kind': 'hold-only' if item.get('execution') is None else 'attached-suite'}


def utc(value):
    return datetime.fromisoformat(str(value).replace('Z', '+00:00'))


def _run(row, role):
    return next((run for run in row.get('runs', []) if run.get('role') == role), {})


def _is(role, key, *values):
    return lambda row: _run(row, role).get(key) in values


def _fact(key, value=True):
    return lambda row: row.get('facts', {}).get(key) == value


def _clean(role):
    def check(row):
        run = _run(row, role)
        return (run.get('cleanup') == 'complete' and run.get('leftovers') == [] and run.get('containersAfter') == 0
                and run.get('listedAfter') is False)
    return check


def _startup(row):
    startup = _run(row, 'owner').get('startup') or {}
    actual, effective = startup.get('actualDurationSeconds'), startup.get('effectiveSeconds')
    return effective == 600 and isinstance(actual, (int, float)) and actual >= effective


CHECKS = {
    'success': {'exit-0': _is('owner', 'exitCode', 0), 'passed': _is('owner', 'outcome', 'passed'),
                'tests-completed': _is('owner', 'cause', 'tests-completed'), 'clean': _clean('owner')},
    'failure': {'exit-4': _is('owner', 'exitCode', 4), 'failed': _is('owner', 'outcome', 'failed'),
                'failure-retained': _is('owner', 'retentionAtExit', 'failure-retained'),
                'resources-retained': lambda row: (_run(row, 'owner').get('containersAtExit') or 0) > 0,
                'clean-after-down': _clean('owner')},
    'startup-timeout': {'HXR021': lambda row: 'HXR021' in _run(row, 'owner').get('ruleIds', []),
                        'startup-timeout': _is('owner', 'cause', 'startup-timeout'),
                        'default-600-elapsed': _startup,
                        'not-overridden': lambda row: (_run(row, 'owner').get('startup') or {}).get('override') == 'not-overridden',
                        'unready-recorded': _fact('startupFailureRecorded'),
                        'partial-retained': lambda row: (_run(row, 'owner').get('retentionAtExit') == 'failure-retained'
                                                         and (_run(row, 'owner').get('containersAtExit') or 0) > 0),
                        'first-outcome-stable': _is('owner', 'firstOutcomeStable', True),
                        'clean-after-down': _clean('owner')},
    'cancellation': {'exit-130': _is('owner', 'exitCode', 130), 'cancelled': _is('owner', 'outcome', 'cancelled'),
                     'no-leftovers-at-exit': _is('owner', 'containersAtExit', 0), 'clean': _clean('owner')},
    'collision': {'refused-HXR031': lambda row: 'HXR031' in _run(row, 'peer').get('ruleIds', []),
                  'names-first-run': _fact('namesFirstRun'), 'first-unaffected': _fact('firstUnaffected'),
                  'peer-not-listed': lambda row: _run(row, 'peer').get('listedAfter') in (False, None),
                  'first-passed': _is('owner', 'outcome', 'passed'), 'first-exit-0': _is('owner', 'exitCode', 0),
                  'clean': _clean('owner')},
    'owner-finish-hold': {'hold-admitted': _fact('holdAdmitted'), 'passed-at-once': _fact('passedWhileHeld'),
                          'cleanup-waits': _fact('cleanupPending'), 'refused-owner-passed': _fact('refusal', ['HXR035', 'owner-passed']),
                          'hold-ended': _fact('holdEnded'), 'exit-0': _is('owner', 'exitCode', 0),
                          'passed': _is('owner', 'outcome', 'passed'), 'clean': _clean('owner')},
    'owner-cancel-attached': {'attached-withdrawn': _fact('attachedEnd', ['withdrawn', 'owner-cancelled']),
                              'withdrawn-before-teardown': _fact('withdrawnBeforeTeardown'),
                              'owner-cancelled': _is('owner', 'outcome', 'cancelled'), 'owner-exit-130': _is('owner', 'exitCode', 130),
                              'clean': _clean('owner')},
    'attached-failure-expiry': {'attached-failed': _is('attached', 'outcome', 'failed'),
                                'environment-retained': _fact('retainedAfterAttachedFailure'),
                                'owner-untouched': _fact('ownerUntouched'),
                                'expiry-withdrawn': _fact('expiryEnd', ['withdrawn', 'expired']),
                                'hold-expired': _is('expiring', 'cause', 'hold-expired'),
                                'expiry-unchanged': _fact('expiryUnchanged'), 'retry-HXR036': _fact('retryRefused'),
                                'clean-after-down': _clean('owner')},
    'failure-then-cancellation': {'attached-failed': _is('attached', 'outcome', 'failed'),
                                  'failure-unchanged': _fact('attachmentUnchanged'),
                                  'retention-unchanged': _fact('retentionUnchanged'),
                                  'second-end-first-outcome': _fact('secondEndFirstOutcome'),
                                  'owner-cancelled': _is('owner', 'outcome', 'cancelled'), 'clean-after-down': _clean('owner')},
}


def verdict(scenario, row, version):
    """Return the failed check names of one recorded row."""
    if row.get('error'):
        return ['driver:' + row['error']]
    failed = [name for name, check in CHECKS[scenario].items() if not check(row)]
    if row.get('foreign', {}).get('unchanged') is not True:
        failed.append('foreign-unchanged')
    if any(run.get('toolVersion') not in (None, version) for run in row.get('runs', [])):
        failed.append('tool-version')
    return failed


def render(summary):
    lines = ['# Story 2.10 local lifecycle scenarios (Builds ' + summary['tool']['version'] + ')', '',
             'Parties `' + summary['source']['revision'] + '`, nested Builds `' + summary['source']['buildsRevision']
             + '`, package mode, local execution context. Tool content identity `' + str(summary['tool']['contentIdentity']) + '`.',
             'Generated by `eng/lifecycle-qualification/scenarios.py`; `summary.json` holds every field.', '',
             '| Scenario | Verdict | Owner run | Environment | First outcome (cause) | Startup effective / actual / override | Retention at exit → cleanup | Leftovers | Foreign unchanged |',
             '|---|---|---|---|---|---|---|---|---|']
    for row in summary['rows']:
        owner = _run(row, 'owner')
        startup = owner.get('startup') or {}
        actual = startup.get('actualDurationSeconds')
        lines.append('| {} | {}{} | `{}` | `{}` | {} ({}) | {} / {} / {} | {} → {} | {} | {} |'.format(
            row['scenario'], row['verdict'], ' (flaky)' if row.get('flaky') else '', owner.get('runId'), owner.get('environmentId'),
            owner.get('outcome'), owner.get('cause'), startup.get('effectiveSeconds'),
            None if actual is None else round(actual, 1), startup.get('override'), owner.get('retentionAtExit'),
            owner.get('cleanup'), len(owner.get('leftovers') or []), row.get('foreign', {}).get('unchanged')))
    lines += ['', 'Failed checks: ' + ('; '.join(row['scenario'] + ': ' + ', '.join(row['failedChecks'])
                                                for row in summary['rows'] if row['failedChecks']) or 'none') + '.', '']
    if error := summary.get('residue', {}).get('error'):
        lines += ['Final cleanup inventory unavailable. ' + error + '.', '']
    lines += ['CI identities are counted, not re-run: the Story 2.8 hosted Parties runs '
              + ', '.join('[{}]({})'.format(url.rsplit('/', 1)[1], url) for url in CI_RUNS) + '.',
              'The CI part of Story 2.10 and the Platform-accepted tool record (and pins) wait for Story 2.14 and CiCd story 21.', '']
    return '\n'.join(lines)


# ---- Live driver ----

class Driver:
    def __init__(self, checkout, work):
        self.checkout, self.work = checkout, work
        self.env = {key: value for key, value in os.environ.items()
                    if key not in ('CI', 'GITHUB_ACTIONS', 'XDG_DATA_HOME') and not key.startswith(('GITHUB_', 'RUNNER_'))}
        self.env['PATH'] = str(Path.home() / '.dotnet/tools') + os.pathsep + self.env.get('PATH', '')  # pwsh for Builds props.
        self.env.update(MSBUILDDISABLENODEREUSE='1', DOTNET_CLI_USE_MSBUILD_SERVER='0', DOTNET_NOLOGO='1', NO_COLOR='1')
        self.ids, self.owners, self.downed, self.procs, self.paused, self.sequence = set(), [], set(), [], [], 0
        self.current = {}

    def git(self, *args, cwd=None):
        return subprocess.run(['git', '-c', 'core.commitGraph=false', *args], cwd=cwd or self.checkout, env=self.env,
                              capture_output=True, text=True, timeout=60, check=True).stdout.strip()

    def preflight(self):
        gitlink = self.git('rev-parse', 'HEAD:references/Hexalith.Builds')
        if self.git('status', '--porcelain', '--untracked-files=no'):
            raise SystemExit('The checkout has tracked changes.')
        if self.git('rev-parse', 'HEAD', cwd=self.checkout / 'references/Hexalith.Builds') != gitlink:
            raise SystemExit('Initialize references/Hexalith.Builds at its gitlink first.')
        version = read_json(self.checkout / '.config/dotnet-tools.json')['tools']['hexalith.builds.module.cli']['version']
        subprocess.run(['dotnet', 'tool', 'restore'], cwd=self.checkout, env=self.env, capture_output=True, timeout=900, check=True)
        packages = Path(self.env.get('NUGET_PACKAGES') or Path.home() / '.nuget/packages')
        dlls = sorted((packages / 'hexalith.builds.module.cli' / version / 'tools').glob('*/any/Hexalith.Builds.Module.Cli.dll'))
        if len(dlls) != 1:
            raise SystemExit('Expected exactly one restored hexalith-module entry assembly.')
        self.dll = dlls[0]  # Launched directly so the owner tool is its own process-group leader.
        build = subprocess.run(['dotnet', str(self.dll), '--version'], cwd=self.checkout, env=self.env,
                               capture_output=True, text=True, timeout=60).stdout.strip()
        if build.split('+')[0] != version:
            raise SystemExit('The restored tool does not report the manifest version.')
        self.manifest_hash = hashlib.sha256((self.checkout / MANIFEST).read_bytes()).hexdigest()
        return {'revision': self.git('rev-parse', 'HEAD'), 'buildsRevision': gitlink, 'manifestSha256': self.manifest_hash}, \
            {'version': version, 'build': build}

    def prepare(self):
        """Prepare package-mode imports and build both suites before any service starts."""
        code, output = self.tool('prepare-build', '--manifest', MANIFEST, '--mode', 'package',
                                 '--workspace', str(self.work / 'prepared'), timeout=900)
        if code != 0:
            raise SystemExit('prepare-build refused: ' + ', '.join(rule_ids(output)))
        preparation = output['buildPreparation']
        environment = {**self.env, **preparation['environment']}
        for suite in SUITES:
            log = self.work / ('build-' + Path(suite).stem + '.log')
            with log.open('w') as stream:
                result = subprocess.run(['dotnet', 'build', suite, '-c', 'Release', '-nodeReuse:false',
                                         '-p:UseHexalithProjectReferences=false', '-p:UseNuGetDeps=true'],
                                        cwd=self.checkout, env=environment, stdout=stream, stderr=subprocess.STDOUT, timeout=1800)
            if result.returncode != 0:  # A product defect: stop before any service starts, and report it.
                codes = sorted(set(re.findall(r'error ([A-Z]+[0-9]+)', log.read_text())))
                raise SystemExit(f'{Path(suite).stem} does not build in package mode ({", ".join(codes)}); see {log.name}.')
        return preparation['mappingHash']

    def tool(self, *args, timeout=300):
        result = subprocess.run(['dotnet', str(self.dll), *args, '--output', 'json'], cwd=self.checkout, env=self.env,
                                stdin=subprocess.DEVNULL, capture_output=True, text=True, timeout=timeout)
        return result.returncode, last_json(result.stdout)

    def launch(self, label, *args):
        self.sequence += 1
        if label in self.current:
            raise Failure('duplicate role ' + label)
        known = self.states() if label in ('owner', 'peer') else set()
        stem = self.work / f'{self.sequence:02d}-{label}'
        with open(str(stem) + '.json', 'w') as stdout, open(str(stem) + '.log', 'w') as stderr:
            proc = subprocess.Popen(['dotnet', str(self.dll), *args, '--output', 'json'], cwd=self.checkout, env=self.env,
                                    stdin=subprocess.DEVNULL, stdout=stdout, stderr=stderr, start_new_session=True)
        self.procs.append(proc)
        self.current[label] = {'label': label, 'proc': proc, 'stdout': Path(str(stem) + '.json'), 'knownStates': known}
        return self.current[label]

    def poll(self, check, seconds, proc=None, what='condition'):
        end = time.monotonic() + seconds
        while time.monotonic() < end:
            exited = proc is not None and proc.poll() is not None
            value = check()
            if value:
                return value
            if exited:
                raise Failure(what + ': process exited first')
            time.sleep(0.5)
        raise Failure(what + ': timed out')

    def states(self):
        return {path.stem for path in STATE.glob('*.json')}

    def group(self, pid):
        try:
            return os.getpgid(pid) if pid else None
        except ProcessLookupError:
            return None

    def provisioning_ids(self, proc):
        """Read the provisioning locks opened by this exact CLI process."""
        if proc.poll() is not None:
            return set()
        locks = set()
        try:
            for descriptor in Path(f'/proc/{proc.pid}/fd').iterdir():
                try:
                    target = descriptor.readlink()
                except FileNotFoundError:  # The process closed this descriptor during the scan.
                    continue
                if target.name == 'provisioning.lock' and target.parent.parent == DIAGNOSTICS:
                    locks.add(target.parent.name)
        except FileNotFoundError:  # The launcher exited during the scan.
            pass
        return locks if proc.poll() is None else set()

    def bind_reported_owner(self, run, output):
        run_id = output.get('runId')
        if (not run.get('runId') and isinstance(run_id, str) and re.fullmatch('[0-9a-f]{32}', run_id)
                and run_id not in self.ids and run_id not in run['knownStates']):
            state = read_json(STATE / (run_id + '.json')) or {}
            self.bind(run, run_id, state.get('environmentId'))

    def discover(self, proc, known):
        if proc.poll() is not None:
            return None
        provisioning = self.provisioning_ids(proc)
        found = []
        for stem in self.states() - known - self.ids:
            state = read_json(STATE / (stem + '.json'))
            if (state and state.get('runId') == stem and str(state.get('manifestHash', '')).lower() == self.manifest_hash
                    and (state.get('executionContext') or {}).get('kind') == 'local'
                    and (stem in provisioning or self.group(state.get('appHostProcessId')) == proc.pid)):
                found.append(state)
        return found[0] if len(found) == 1 else None

    def bind(self, run, run_id, environment_id, owner=True):
        run.update(runId=run_id, environmentId=environment_id)
        self.ids.add(run_id)
        if owner:
            self.owners.append(run_id)

    def control(self, run, action):
        directory = CONTROLS / run['runId']
        directory.mkdir(parents=True, exist_ok=True, mode=0o700)
        temporary = directory / '.control.json.tmp'
        temporary.write_text(json.dumps({'runId': run['runId'], **run.get('lineage', {}), 'action': action}))
        os.replace(temporary, directory / 'control.json')

    def owner(self, label='owner', *extra):
        run = self.launch(label, 'test', '--manifest', MANIFEST, '--mode', 'package', '--profile', OWNER, *extra)
        state = self.starting(run, lambda: self.discover(run['proc'], run['knownStates']), 300, ' run id')
        self.bind(run, state['runId'], state.get('environmentId'))
        self.control(run, 'wait')  # The fixture requires its control file when the suite starts.
        return run

    def starting(self, run, check, seconds, what):
        try:
            return self.poll(check, seconds, run['proc'], run['label'] + what)
        except Failure:
            if run['label'] != 'owner' or run.get('executing'):
                raise
            try:  # An owner that stops before execution and exits nonzero is a product refusal.
                code = run['proc'].wait(150)
            except subprocess.TimeoutExpired:
                raise Failure(run['label'] + what + ': failed while still running') from None
            if code == 0:
                raise
            rules = rule_ids(last_json(run['stdout'].read_text())) or ['no rule id']
            raise Refused(run['label'] + ' refused before execution: ' + ', '.join(rules)) from None

    def ready(self, run, seconds=900):
        self.starting(run, lambda: (CONTROLS / run['runId'] / 'execution-ready.json').exists(), seconds, ' execution')
        run['executing'] = True

    def attachments(self, owner):
        return (read_json(DIAGNOSTICS / owner['runId'] / 'attachments.json') or {}).get('attachments') or []

    def attachment(self, owner, run):
        return next((item for item in self.attachments(owner) if item.get('attachedRunId') == run['runId']), None)

    def attached(self, owner, hold, label='attached'):
        known = {item.get('attachedRunId') for item in self.attachments(owner)}
        run = self.launch(label, 'test', '--manifest', MANIFEST, '--mode', 'package', '--profile', ATTACHED,
                          '--attach-to', owner['runId'], '--hold-seconds', str(hold))
        entry = self.poll(lambda: next((item for item in self.attachments(owner) if item.get('execution')
                                        and item.get('attachedRunId') not in known), None), 120, run['proc'], label + ' run id')
        self.bind(run, entry['attachedRunId'], entry['environmentId'], owner=False)
        run.update(admitted=hold_view(entry), lineage={'ownerRunId': owner['runId'], 'environmentId': entry['environmentId'],
                                                       'originalAttachmentRequestId': entry['originalAttachmentRequestId']})
        self.control(run, 'wait')
        return run

    def admit(self, owner, hold, *extra):
        return self.tool('attach', '--owner-run-id', owner['runId'], '--mode', 'package',
                         '--composition-identity', self.manifest_hash, '--hold-seconds', str(hold), *extra)

    def docker_inventory(self, *args):
        try:
            return subprocess.run(['docker', 'ps', *args], capture_output=True, text=True, timeout=60,
                                  env=self.env, check=True).stdout.splitlines()
        except (OSError, subprocess.SubprocessError) as error:
            raise InventoryFailure('Docker inventory failed (' + type(error).__name__ + ')') from None

    def containers(self, run_id):
        return self.docker_inventory('-aq', '--no-trunc', '--filter', 'label=hexalith.g4.run=' + run_id)

    def listing(self, run_id=None):
        try:
            code, output = self.tool('list')
        except (OSError, subprocess.SubprocessError) as error:
            raise InventoryFailure('Runner inventory failed (' + type(error).__name__ + ')') from None
        if code != 0:
            detail = ', '.join(rule_ids(output)) or f'exit {code}'
            raise InventoryFailure('Runner inventory failed (' + detail + ')')
        entries = output.get('environments')
        if not isinstance(entries, list) or any(not isinstance(entry, dict) or not entry.get('ownerRunId') for entry in entries):
            raise InventoryFailure('Runner inventory JSON is invalid')
        if run_id is None:
            return entries
        return next(({key: entry.get(key) for key in ('retention', 'cleanup', 'failureRunIds', 'attachedRunIds')}
                     for entry in entries if entry.get('ownerRunId') == run_id), None)

    def foreign(self):
        rows = self.docker_inventory('-a', '--no-trunc', '--format', '{{.ID}}\t{{.Label "hexalith.g4.run"}}')
        containers = sorted(row.split('\t')[0] for row in rows if row.split('\t')[-1] not in self.ids)
        entries = sorted((entry.get('ownerRunId'), entry.get('retention'), entry.get('cleanup'))
                         for entry in self.listing() if entry.get('ownerRunId') not in self.ids)
        return {'containers': containers, 'list': entries}

    def result(self, run_id):
        return extract(read_json(DIAGNOSTICS / run_id / 'test-result.json'))

    def recorded(self, run_id):
        """The first terminal outcome, published before cleanup and while holds still run."""
        status = (read_json(DIAGNOSTICS / run_id / 'selected-test.json') or {}).get('terminalStatus')
        return {'completed': 'passed', 'failed': 'failed', 'cancelled': 'cancelled'}.get(status)

    def settle(self, run, seconds=900):
        try:
            run['exitCode'] = run['proc'].wait(seconds)
        except subprocess.TimeoutExpired:
            raise Failure(run['label'] + ' did not exit') from None
        output = last_json(run['stdout'].read_text())
        run.update(output=output, ruleIds=rule_ids(output), reasons=reasons(output))
        if run['label'] in ('owner', 'peer'):
            self.bind_reported_owner(run, output)
        if run.get('runId'):
            run['first'] = self.result(run['runId'])
            if not run.get('environmentId'):
                run['environmentId'] = (run['first'] or {}).get('environmentId')
            if run['runId'] in self.owners:
                run['containersAtExit'] = len(self.containers(run['runId']))
                run['listedAtExit'] = self.listing(run['runId'])
        return run

    def close(self, run):
        code, _ = self.tool('down', '--run-id', run['runId'], timeout=600)
        if code == 0:
            self.downed.add(run['runId'])
        run.update(downExitCode=code, final=self.result(run['runId']), containersAfter=len(self.containers(run['runId'])),
                   listedAfter=self.listing(run['runId']) is not None)
        return run

    def sigint(self, run, group=False):
        (os.killpg if group else os.kill)(run['proc'].pid, signal.SIGINT)  # SIGINT is the only cooperative signal.

    def pause(self, run):
        def starting():
            state = read_json(STATE / (run['runId'] + '.json'))
            if state is None:
                return None
            if str(state.get('status')).lower() != 'starting':
                raise Failure('startup window missed')
            pid = state.get('appHostProcessId')
            return pid if pid and self.containers(run['runId']) else None
        pid = self.starting(run, starting, 600, ' partial startup')
        tag = ('HEXALITH_G4_RUN_ID=' + run['runId']).encode()
        if self.group(pid) != run['proc'].pid or tag not in Path(f'/proc/{pid}/environ').read_bytes().split(b'\0'):
            raise Failure('the starting AppHost is not owned by this run')
        os.kill(pid, signal.SIGSTOP)
        self.paused.append(pid)
        return pid

    def resume(self):
        for pid in self.paused:
            try:
                os.kill(pid, signal.SIGCONT)
            except ProcessLookupError:
                pass
        self.paused.clear()

    def release(self):
        """Stop every launched process and run down for every issued owner; safe to repeat."""
        blocked = signal.pthread_sigmask(signal.SIG_BLOCK, {signal.SIGINT, signal.SIGTERM, signal.SIGHUP})
        try:
            self._release()
        finally:
            signal.pthread_sigmask(signal.SIG_SETMASK, blocked)

    def _release(self):
        self.resume()
        owners = [self.current[role] for role in ('owner', 'peer') if role in self.current]
        for run in owners:
            if not run.get('runId') and (state := self.discover(run['proc'], run['knownStates'])):
                self.bind(run, state['runId'], state.get('environmentId'))
        alive = [proc for proc in self.procs if proc.poll() is None]
        for proc in alive:
            os.killpg(proc.pid, signal.SIGINT)
        for proc in alive:
            try:
                proc.wait(180)
            except subprocess.TimeoutExpired:
                os.killpg(proc.pid, signal.SIGKILL)
                proc.wait(30)
        self.procs.clear()
        for run in owners:
            if not run.get('runId'):
                self.bind_reported_owner(run, last_json(run['stdout'].read_text()))
        for run_id in self.owners:
            if run_id not in self.downed:
                try:
                    code, _ = self.tool('down', '--run-id', run_id, timeout=600)
                    if code == 0:
                        self.downed.add(run_id)
                except subprocess.TimeoutExpired:
                    pass  # Reported through residue; a later release retries.

    # ---- The nine rows. Each registers its runs by role and returns its facts. ----

    def success(self):
        owner = self.owner()
        self.ready(owner)
        self.control(owner, 'pass')
        self.close(self.settle(owner))
        return {}

    def failure(self):
        owner = self.owner()
        self.ready(owner)
        self.control(owner, 'fail')
        self.close(self.settle(owner))
        return {}

    def startup_timeout(self):
        owner = self.owner()
        self.pause(owner)
        self.settle(owner, 1200)  # The runner's own default 600 s deadline decides; no override.
        failure = read_json(DIAGNOSTICS / owner['runId'] / 'startup-failure.json') or {}
        facts = {'startupFailureRecorded': failure.get('ruleId') == 'HXR021' and isinstance(failure.get('unreadyResources'), list),
                 'failingResource': failure.get('resource'), 'unreadyResources': failure.get('unreadyResources')}
        self.resume()
        self.close(owner)
        return facts

    def cancellation(self):
        owner = self.owner()
        self.ready(owner)
        self.sigint(owner, group=True)
        self.close(self.settle(owner, 600))
        return {}

    def collision(self):
        with socket.socket() as probe:
            probe.bind(('127.0.0.1', 0))
            port = str(probe.getsockname()[1])
        owner = self.owner('owner', '--eventstore-port', port)
        self.ready(owner)
        peer = self.settle(self.launch('peer', 'test', '--manifest', MANIFEST, '--mode', 'package', '--profile', OWNER,
                                       '--eventstore-port', port), 900)
        if peer.get('runId'):
            self.close(peer)
        names = any(item.get('ruleId') == 'HXR031' and owner['runId'] in str(item.get('message'))
                    for item in peer['output'].get('diagnostics') or [])
        untouched = (owner['proc'].poll() is None and self.recorded(owner['runId']) is None
                     and str((read_json(STATE / (owner['runId'] + '.json')) or {}).get('status')).lower() == 'ready')
        self.control(owner, 'pass')
        self.close(self.settle(owner))
        return {'namesFirstRun': names, 'firstUnaffected': untouched, 'peerIssuedRuns': int('runId' in peer)}

    def owner_finish_hold(self):
        owner = self.owner()
        self.ready(owner)
        code, admitted = self.admit(owner, 600)
        hold = admitted.get('attachment') or {}
        self.control(owner, 'pass')
        passed = self.poll(lambda: self.recorded(owner['runId']), 300, owner['proc'], 'owner first outcome')
        held = owner['proc'].poll() is None and (self.listing(owner['runId']) or {}).get('cleanup') == 'pending'
        _, refused = self.admit(owner, 60)
        time.sleep(5)
        still = owner['proc'].poll() is None and bool(self.containers(owner['runId']))
        ended_code, ended = self.tool('attach', '--owner-run-id', owner['runId'], '--end',
                                      '--attached-run-id', str(hold.get('attachedRunId')), '--outcome', 'passed')
        self.settle(owner)
        facts = {'holdAdmitted': code == 0 and hold.get('ownerRunId') == owner['runId'], 'passedWhileHeld': passed == 'passed',
                 'cleanupPending': held and still, 'refusal': rule_ids(refused) + reasons(refused),
                 'holdEnded': ended_code == 0 and (ended.get('attachment') or {}).get('outcome') == 'passed',
                 'hold': hold_view(ended.get('attachment'))}
        self.close(owner)
        return facts

    def owner_cancel_attached(self):
        owner = self.owner()
        self.ready(owner)
        attached = self.attached(owner, 600)
        self.ready(attached)
        self.sigint(owner)  # The owner tool alone, so the runner orders withdrawal before teardown itself.
        self.settle(attached, 600)
        self.settle(owner, 600)
        end = hold_view(self.attachment(owner, attached)) or {}
        teardown = (owner.get('first') or {}).get('cleanupObservedAt')
        facts = {'attachedEnd': [end.get('outcome'), end.get('endReason')], 'hold': end,
                 'withdrawnBeforeTeardown': bool(end.get('endedAtUtc') and teardown and utc(end['endedAtUtc']) <= utc(teardown))}
        self.close(owner)
        return facts

    def attached_failure_expiry(self):
        owner = self.owner()
        self.ready(owner)
        attached = self.attached(owner, 600)
        expiring = self.attached(owner, SHORT_HOLD, 'expiring')
        self.ready(attached)
        self.control(attached, 'fail')
        self.settle(attached, 600)
        listed = self.listing(owner['runId']) or {}
        untouched = (owner['proc'].poll() is None and self.recorded(owner['runId']) is None
                     and str((read_json(STATE / (owner['runId'] + '.json')) or {}).get('status')).lower() == 'ready')
        self.settle(expiring, SHORT_HOLD + 400)
        end = hold_view(self.attachment(owner, expiring)) or {}
        _, retry = self.admit(owner, SHORT_HOLD, '--attachment-request-id', expiring['lineage']['originalAttachmentRequestId'])
        self.control(owner, 'pass')
        self.settle(owner)
        facts = {'retainedAfterAttachedFailure': listed.get('retention') == 'failure-retained'
                 and attached['runId'] in (listed.get('failureRunIds') or []),
                 'ownerUntouched': untouched, 'expiryEnd': [end.get('outcome'), end.get('endReason')],
                 'expiryUnchanged': end.get('expiresAtUtc') == expiring['admitted']['expiresAtUtc'],
                 'retryRefused': 'HXR036' in rule_ids(retry), 'retryReasons': reasons(retry), 'hold': end}
        self.close(owner)
        return facts

    def failure_then_cancellation(self):
        owner = self.owner()
        self.ready(owner)
        attached = self.attached(owner, 600)
        self.ready(attached)
        self.control(attached, 'fail')
        self.settle(attached, 600)
        before, retained = hold_view(self.attachment(owner, attached)), (self.listing(owner['runId']) or {}).get('retention')
        self.sigint(owner)
        self.settle(owner, 600)
        after, kept = hold_view(self.attachment(owner, attached)), (self.listing(owner['runId']) or {}).get('retention')
        _, second = self.tool('attach', '--owner-run-id', owner['runId'], '--end', '--attached-run-id', attached['runId'],
                              '--outcome', 'cancelled')
        facts = {'attachmentUnchanged': before is not None and before == after and before.get('outcome') == 'failed',
                 'retentionUnchanged': retained == kept == 'failure-retained',
                 'secondEndFirstOutcome': hold_view(second.get('attachment')) == before, 'hold': after}
        self.close(owner)
        return facts

    def row(self, scenario, revision, version):
        before, after = None, None
        started, self.current, facts, error, refused = now(), {}, {}, None, False
        try:
            before = self.foreign()
            facts = getattr(self, scenario.replace('-', '_'))()
        except Exception as failure:  # Recorded as a failed row; the campaign continues unless refused.
            error = str(failure) if isinstance(failure, Failure) else type(failure).__name__
            refused = isinstance(failure, Refused)
        finally:
            self.release()
        if error:
            for run in self.current.values():
                self.observe(run)
        try:
            after = self.foreign()
        except InventoryFailure as failure:
            error = error or str(failure)
        row = {'scenario': scenario, 'startedAt': started, 'finishedAt': now(), 'sourceRevision': revision,
               'runs': [summarize(role, run) for role, run in self.current.items()], 'facts': facts}
        if error:
            row.update(error=error, refusal=refused)
        row['foreign'] = {'unchanged': before == after if before is not None and after is not None else None,
                          'containers': len(after['containers']) if after is not None else None,
                          'listEntries': len(after['list']) if after is not None else None}
        row['failedChecks'] = verdict(scenario, row, version)
        row['verdict'] = 'failed' if row['failedChecks'] else 'passed'
        return row

    def observe(self, run):
        """Complete a run's record after an interrupted row; release already stopped it and ran down."""
        try:
            if 'exitCode' not in run:
                self.settle(run, 30)
            if run.get('runId') in self.owners and 'final' not in run:
                self.close(run)
        except Exception:  # noqa: BLE001 - best effort; the row is already failed.
            pass

    def residue(self):
        listed = {entry.get('ownerRunId') for entry in self.listing()}
        return {'listedIssuedRuns': sorted(listed & self.ids),
                'issuedContainers': sum(len(self.containers(run_id)) for run_id in sorted(self.ids))}


def summarize(role, run):
    first = run.get('first') or {}
    final = run.get('final') or first
    terminal = ('outcome', 'cause', 'recordedAt')
    return {'role': role, 'runId': run.get('runId'), 'environmentId': run.get('environmentId') or first.get('environmentId'),
            'attemptId': first.get('attemptId'), 'toolVersion': first.get('toolVersion'),
            'toolContentIdentity': first.get('toolContentIdentity'), 'exitCode': run.get('exitCode'),
            'ruleIds': run.get('ruleIds', []), **{key: first.get(key) for key in terminal},
            'diagnosticIds': first.get('diagnosticIds', []), 'startup': first.get('startup'),
            'retentionAtExit': (run.get('listedAtExit') or {}).get('retention') or first.get('retention'),
            'containersAtExit': run.get('containersAtExit'), 'retention': final.get('retention'),
            'cleanup': final.get('cleanup'), 'leftovers': final.get('leftovers'), 'containersAfter': run.get('containersAfter'),
            'listedAfter': run.get('listedAfter'),
            'firstOutcomeStable': bool(first) and all(first.get(key) == final.get(key) for key in terminal)}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--checkout', required=True, type=Path, help='Parties worktree at the Platform gitlink.')
    parser.add_argument('--evidence', required=True, type=Path, help='Directory for summary.json and README.md.')
    parser.add_argument('--work', type=Path, help='Private scratch directory for logs and prepared imports.')
    parser.add_argument('--scenario', action='append', choices=SCENARIOS, help='Run only these rows (repeatable).')
    parser.add_argument('--skip-prepare', action='store_true', help='Reuse an earlier package preparation.')
    arguments = parser.parse_args(argv)
    checkout = arguments.checkout.resolve()
    work = (arguments.work or checkout.parent / (checkout.name + '-lifecycle-work')).resolve()
    work.mkdir(parents=True, exist_ok=True, mode=0o700)
    if work.joinpath('prepared').exists() and not arguments.skip_prepare:
        shutil.rmtree(work / 'prepared')
    driver = Driver(checkout, work)
    for number in (signal.SIGTERM, signal.SIGHUP):
        signal.signal(number, signal.default_int_handler)  # Every exit path reaches the finally below.
    source, tool = driver.preflight()
    source['mappingHash'] = None if arguments.skip_prepare else driver.prepare()
    rows, selected = [], arguments.scenario or list(SCENARIOS)
    refused = False
    try:
        for scenario in selected:
            if refused:
                rows.append({'scenario': scenario, 'verdict': 'not-run', 'failedChecks': ['not-run'], 'runs': []})
                continue
            row = driver.row(scenario, source['revision'], tool['version'])
            if row['failedChecks'] and not row.get('refusal'):  # One re-run of a non-reproducing failure; flaky.
                retry = driver.row(scenario, source['revision'], tool['version'])
                retry.update(flaky=not retry['failedChecks'], firstAttempt={key: row.get(key) for key in (
                    'failedChecks', 'error', 'runs', 'facts', 'foreign')})
                row = retry
            refused = row.get('refusal', False)
            rows.append(row)
            print(json.dumps({'scenario': scenario, 'verdict': row['verdict'], 'failedChecks': row['failedChecks']}), flush=True)
    finally:
        driver.release()
        try:
            residue = driver.residue()
        except InventoryFailure as failure:
            residue = {'listedIssuedRuns': None, 'issuedContainers': None, 'error': str(failure)}
        for run_id in driver.ids:
            shutil.rmtree(CONTROLS / run_id, ignore_errors=True)
    identities = {(run.get('toolVersion'), run.get('toolContentIdentity')) for row in rows for run in row['runs'] if run.get('toolContentIdentity')}
    summary = {'schema': 'hexalith.platform.story-2-10-local-lifecycle.v1', 'generatedAt': now(), 'executionContext': 'local',
               'mode': 'package', 'profiles': [OWNER, ATTACHED], 'source': source,
               'tool': {**tool, 'contentIdentity': next(iter(identities))[1] if len(identities) == 1 else sorted(map(str, identities))},
               'ciRuns': list(CI_RUNS), 'deferred': 'CI part and Platform-accepted tool record wait for Story 2.14 and CiCd story 21.',
               'rows': rows, 'residue': residue,
               'passed': sum(row['verdict'] == 'passed' for row in rows), 'total': len(rows)}
    evidence = arguments.evidence.resolve()
    evidence.mkdir(parents=True, exist_ok=True)
    (evidence / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
    (evidence / 'README.md').write_text(render(summary))
    if 'error' in residue:
        print(residue['error'], file=sys.stderr)
    clean = residue['listedIssuedRuns'] == [] and residue['issuedContainers'] == 0
    return 0 if clean and summary['passed'] == len(rows) == len(selected) else 1


if __name__ == '__main__':
    raise SystemExit(main())
