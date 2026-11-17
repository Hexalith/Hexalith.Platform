"""Local controller and workflow controls for the Story 2.8 review fixes; none are hosted qualification evidence."""
from datetime import datetime, timedelta, timezone
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess  # nosec B404 -- fixed interpreter, this repository's controller and temporary fixtures only.
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / 'scripts' / 'qualify-disposable-ci.py'
SPEC = importlib.util.spec_from_file_location('review_fix_qualification', SCRIPT)
Q = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(Q)
OBSERVER_SPEC = importlib.util.spec_from_file_location('review_fix_observer', ROOT / 'scripts' / 'observe-current-owner-startup.py')
O = importlib.util.module_from_spec(OBSERVER_SPEC)
OBSERVER_SPEC.loader.exec_module(O)
WORKFLOW = ROOT / '.github/workflows/ci-lifecycle-qualification.yml'


def stamp(seconds):
    return (datetime(2026, 11, 9, tzinfo=timezone.utc) + timedelta(seconds=seconds)).isoformat()


class WithdrawalTests(unittest.TestCase):
    """The real withdrawn() reads the durable withdrawal document; no mock replaces it."""

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)
        self.controller = object.__new__(Q.Qualification)
        self.controller.diagnostics = lambda item: self.directory
        self.controller.issued = lambda item: {'runId': 'a' * 32}
        self.observed = []
        self.controller.observe = lambda kind, **values: self.observed.append((kind, values))
        self.admitted = {'originalAttachmentRequestId': 'b' * 32, 'attachedRunId': 'c' * 32, 'acceptedAtUtc': stamp(0),
                         'expiresAtUtc': stamp(240), 'effectiveHoldSeconds': 240}

    def publish(self, **changes):
        attachment = {**self.admitted, 'ownerRunId': 'a' * 32, 'environmentId': 'd' * 32, 'requestedHoldSeconds': 240,
                      'outcome': 'withdrawn', 'endReason': 'ci-job-completed', 'endedAtUtc': stamp(10), **changes}
        (self.directory / 'attachments.json').write_text(json.dumps({'attachments': [attachment]}))

    def test_job_completion_and_job_cancellation_withdrawals_are_accepted(self):
        for reason in ('ci-job-completed', 'ci-job-cancelled'):
            with self.subTest(reason=reason):
                self.publish(endReason=reason)
                self.assertEqual(self.controller.withdrawn({}, self.admitted, reason=reason)['endReason'], reason)
                self.assertEqual(self.controller.withdrawn({}, self.admitted)['endReason'], reason)
        self.assertEqual(self.observed[-1][0], 'actual-withdrawal')

    def test_expiry_open_holds_late_ends_changed_lineage_and_other_reasons_refuse(self):
        refusals = {
            'expired': {'outcome': 'expired'},
            'still-active': {'outcome': None, 'endedAtUtc': None},
            'no-outcome': {'outcome': None},
            'at-expiry': {'endedAtUtc': stamp(240)},
            'after-expiry': {'endedAtUtc': stamp(241)},
            'changed-acceptance': {'acceptedAtUtc': stamp(1)},
            'changed-expiry': {'expiresAtUtc': stamp(480)},
            'changed-lineage': {'originalAttachmentRequestId': 'e' * 32},
            'owner-cancelled': {'endReason': 'owner-cancelled'},
            'hold-expired': {'endReason': 'expired'},
        }
        for name, changes in refusals.items():
            with self.subTest(name=name):
                self.publish(**changes)
                with self.assertRaises((ValueError, KeyError, TypeError)):
                    self.controller.withdrawn({}, self.admitted)
        self.publish(endReason='ci-job-completed')
        with self.assertRaises(ValueError):
            self.controller.withdrawn({}, self.admitted, reason='ci-job-cancelled')


class LockRefusalTests(unittest.TestCase):
    def test_each_injected_lock_counts_only_as_its_own_runtime_refusal(self):
        controller = object.__new__(Q.Qualification)
        controller.issued = lambda item: {'runId': 'a' * 32}
        controller.containers = lambda item: []
        remaining = {key: [] for key in ('containerIds', 'processIds', 'workspaceIds', 'claimIds', 'discoveryFailures')}
        remaining['unverified'] = True
        cases = {('withdrawal-lock-refusal', 'HXR037'): True, ('withdrawal-lock-refusal', 'HXR023'): False,
                 ('provisioning-lock-refusal', 'HXR023'): True, ('provisioning-lock-refusal', 'HXR037'): False,
                 ('withdrawal-lock-refusal', 'HXR024'): False, ('provisioning-lock-refusal', None): False,
                 ('other-refusal', 'HXR037'): False, ('other-refusal', 'HXR023'): False}
        for (tag, rule), accepted in cases.items():
            result = {'runId': 'a' * 32, 'outcome': {'exitCode': 'TopologyOrLifecycle', 'ruleId': rule}, 'remainingResources': remaining}
            controller.tool = lambda item, arguments, tag, environment=None, value=result: (subprocess.CompletedProcess([], 3), value)
            with self.subTest(tag=tag, rule=rule):
                if accepted:
                    self.assertEqual(controller.down({}, tag, expected=False), result)
                else:
                    with self.assertRaises(ValueError):
                        controller.down({}, tag, expected=False)
        self.assertEqual(Q.LOCK_REFUSALS, {'withdrawal-lock-refusal': 'HXR037', 'provisioning-lock-refusal': 'HXR023'})


class SignalOrderingTests(unittest.TestCase):
    def test_wait_cancellation_installs_handlers_before_constructing_the_controller(self):
        forwarded = []

        class Constructed:
            def __init__(self, finalizing=False):
                # A runner signal arriving while the controller verifies its tool must not be lost.
                signal.raise_signal(signal.SIGTERM)

            def owner(self, label):
                return {'label': label, 'pid': 1, 'birth': '1'}

            def cancellation_snapshot(self, item):
                item['cancellationSnapshot'] = {'observed': True}

            def diagnostics(self, item):
                return Path(tempfile.gettempdir()) / 'review-fix-absent-diagnostics'

            def save(self):
                pass

            def observe(self, kind, **values):
                pass

            def resume_host(self, item):
                pass

            def process_signal(self, item, number):
                forwarded.append(number)

        previous = {number: signal.getsignal(number) for number in (signal.SIGINT, signal.SIGTERM)}
        Q.CANCELLED.clear()
        try:
            with patch.object(Q, 'Qualification', Constructed), patch.object(sys, 'argv', ['qualify', 'wait-cancellation']):
                self.assertEqual(Q.main(), 1)
        finally:
            for number, handler in previous.items():
                signal.signal(number, handler)
        self.assertEqual([item['signal'] for item in Q.CANCELLED], [signal.SIGTERM])
        self.assertEqual(forwarded, [signal.SIGINT])
        Q.CANCELLED.clear()


class ReaderRemovalTests(unittest.TestCase):
    def test_remove_reader_never_loads_the_builds_helper(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / 'scripts').mkdir()
            shutil.copy2(SCRIPT, root / 'scripts' / SCRIPT.name)
            runner = root / 'runner-temp'
            reader = runner / 'hexalith-lifecycle-registry.reader'
            reader.mkdir(parents=True, mode=0o700)
            (reader / 'config.json').write_text('{"auths": {}}')
            environment = {**os.environ, 'RUNNER_TEMP': str(runner), 'HEXALITH_LIFECYCLE_REGISTRY_CONFIG': str(reader)}
            # This copy has no references/Hexalith.Builds, so loading the helper would fail.
            completed = subprocess.run([sys.executable, '-B', str(root / 'scripts' / SCRIPT.name), 'remove-reader'],  # nosec B603
                                       cwd=root, env=environment, capture_output=True, text=True, timeout=60, check=False)
            self.assertEqual(completed.returncode, 0, completed.stderr)
            self.assertFalse(reader.exists())
            missing = subprocess.run([sys.executable, '-B', str(root / 'scripts' / SCRIPT.name), 'remove-reader'],  # nosec B603
                                     cwd=root, env=environment, capture_output=True, text=True, timeout=60, check=False)
            self.assertEqual(missing.returncode, 0, missing.stderr)
        self.assertIsInstance(Q.HELPER.SCHEMA, str)


class ObserverLayoutTests(unittest.TestCase):
    def test_observer_reads_legacy_and_per_attempt_helper_evidence(self):
        execution = {'runId': 'a' * 32, 'outcome': {'exitCode': 'Success', 'phase': 'None'}}
        cleanup = {**execution, 'remainingResources': {'unverified': False}}
        with tempfile.TemporaryDirectory() as temporary:
            evidence = Path(temporary)
            (evidence / 'execution.json').write_text(json.dumps(execution))
            (evidence / 'cleanup.json').write_text(json.dumps(cleanup))
            self.assertEqual(list(O.helper_phase_documents(evidence)), [('execution.json', execution), ('cleanup.json', cleanup)])
            (evidence / 'cleanup.json').unlink()
            (evidence / 'execution.json').write_text(json.dumps({'schema': 'hexalith.module.ci-integration-execution.v1', 'result': execution}))
            for number, status in ((1, 'completed'), (2, 'completed')):
                (evidence / f'cleanup-{number}.json').write_text(json.dumps({'schema': 'hexalith.module.ci-integration-cleanup.v1',
                                                                             'status': status, 'result': dict(cleanup, attempt=number)}))
            documents = list(O.helper_phase_documents(evidence))
            self.assertEqual(documents, [('execution.json', execution), ('cleanup.json', dict(cleanup, attempt=2))])
            (evidence / 'cleanup-1.json').unlink()
            with self.assertRaises(ValueError):
                list(O.helper_phase_documents(evidence))


class WorkflowControlTests(unittest.TestCase):
    def setUp(self):
        self.workflow = WORKFLOW.read_text()
        self.qualification = self.workflow.split('  qualification:', 1)[1]

    def step(self, name):
        return self.qualification.split('      - name: ' + name + '\n', 1)[1].split('\n      - ', 1)[0]

    def test_controller_suite_runs_first_and_checkout_keeps_no_credentials(self):
        steps = re.findall(r'(?m)^      - (?:name: (.+)|uses: (\S+)(?: #.*)?)$', self.qualification)
        self.assertEqual(steps[0][1].split('@')[0], 'actions/checkout')
        self.assertEqual(steps[1][0], 'Initialize only direct owner dependencies')
        self.assertEqual(steps[2][0], 'Test the lifecycle controller before any service')
        self.assertIn('run: python3 -B -m unittest discover -s tests/ci-lifecycle -v', self.step('Test the lifecycle controller before any service'))
        checkout = self.qualification.split('      - uses: actions/checkout@', 1)[1].split('\n      - ', 1)[0]
        self.assertIn('persist-credentials: false', checkout)
        ordinary = (ROOT / '.github/workflows/ci.yml').read_text()
        self.assertIn('run: python3 -B -m unittest discover -s tests/ci-lifecycle -v', ordinary)

    def test_registry_reader_is_scoped_to_owner_starting_and_removal_steps(self):
        self.assertNotIn('DOCKER_CONFIG=', self.workflow)
        self.assertNotIn('GITHUB_ENV', self.step('Configure only the dedicated pull reader'))
        reader = '${{ runner.temp }}/hexalith-lifecycle-registry.reader'
        for name in ('Exercise real passing failing partial-startup and exact retry scenarios',
                     'Observe actual startup cancellation readiness', 'Observe actual execution cancellation readiness and active hold'):
            self.assertIn('DOCKER_CONFIG: ' + reader, self.step(name))
        self.assertIn('HEXALITH_LIFECYCLE_REGISTRY_CONFIG: ' + reader, self.step('Remove the private reader file'))
        self.assertEqual(self.workflow.count('DOCKER_CONFIG:'), 3)

    def test_toolchain_versions_and_digests_refuse_before_any_download(self):
        body = self.step('Read verified catalog toolchain versions').split("python3 - <<'PY'\n", 1)[1].split('\n          PY', 1)[0]
        script = '\n'.join(line[10:] for line in body.splitlines())
        catalog = ('<Project><PropertyGroup><HexalithAspireAppHostSdkVersion>13.6.4</HexalithAspireAppHostSdkVersion>'
                   '<HexalithDaprRuntimeVersion>1.18.7</HexalithDaprRuntimeVersion><HexalithDaprCliVersion>1.18.2</HexalithDaprCliVersion>'
                   '</PropertyGroup></Project>')
        digests = {'schema': 'hexalith.builds.dapr-toolchain-sha256.v1',
                   'cli': {'1.18.2': {'dapr_linux_amd64.tar.gz': 'a' * 64}},
                   'runtime': {'1.18.7': {'daprd_linux_amd64.tar.gz': 'b' * 64, 'placement_linux_amd64.tar.gz': 'c' * 64,
                                          'scheduler_linux_amd64.tar.gz': 'd' * 64}}}
        cases = {
            'pinned': (catalog, digests, True),
            'no-digest-file': (catalog, None, False),
            'missing-runtime-digest': (catalog, {**digests, 'runtime': {'1.18.7': {'daprd_linux_amd64.tar.gz': 'b' * 64}}}, False),
            'malformed-digest': (catalog, {**digests, 'cli': {'1.18.2': {'dapr_linux_amd64.tar.gz': 'not-a-digest'}}}, False),
            'missing-catalog-property': (catalog.replace('<HexalithDaprCliVersion>1.18.2</HexalithDaprCliVersion>', ''), digests, False),
        }
        for name, (props, document, accepted) in cases.items():
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                (root / '.hexalith/builds-execution/Props').mkdir(parents=True)
                (root / '.hexalith/builds-execution/Props/Directory.Packages.props').write_text(props)
                if document is not None:
                    (root / '.hexalith/builds-execution/Props/dapr-toolchain-sha256.json').write_text(json.dumps(document))
                environment = root / 'github-env'
                environment.write_text('')
                completed = subprocess.run([sys.executable, '-I', '-c', script], cwd=root, capture_output=True, text=True,  # nosec B603
                                           env={**os.environ, 'GITHUB_ENV': str(environment)}, timeout=60, check=False)
                self.assertEqual(completed.returncode == 0, accepted, completed.stderr)
                exported = environment.read_text()
                if accepted:
                    self.assertIn('CI_DAPR_CLI_SHA256=' + 'a' * 64, exported)
                    self.assertIn('CI_SCHEDULER_SHA256=' + 'd' * 64, exported)
                else:
                    self.assertEqual(exported, '')

    def test_every_dapr_archive_is_verified_before_its_extraction(self):
        install = self.step('Install toolchain without provisioning services')
        lines = [line.strip() for line in install.splitlines()]
        extractions = [index for index, line in enumerate(lines) if line.startswith('tar -xzf')]
        self.assertEqual(len(extractions), 2)
        for index in extractions:
            self.assertTrue(lines[index - 1].startswith('verify_archive '), lines[index - 1])
        self.assertIn("sha256sum --check --strict --quiet -", install)


if __name__ == '__main__':
    unittest.main()
