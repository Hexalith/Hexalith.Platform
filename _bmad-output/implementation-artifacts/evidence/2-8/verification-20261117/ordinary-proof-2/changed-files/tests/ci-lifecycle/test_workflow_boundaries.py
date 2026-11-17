"""Source contract controls only; passing these does not qualify a hosted environment."""
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import tempfile
import time
import unittest
from xml.etree import ElementTree

ROOT = Path(__file__).resolve().parents[2]
# A stand-in for the controller: it records the first runner signal it actually receives.
SIGNAL_PROBE = """#!/bin/sh
trap 'printf "%s\\n" "$*" > "$SIGNAL_MARKER"; exit 1' INT TERM
: > "$STARTED_MARKER"
while :; do sleep 0.05; done
"""


class WorkflowBoundaryTests(unittest.TestCase):
    def test_baseline_keeps_every_existing_isolated_project_consumer_and_native_gate(self):
        ordinary = (ROOT / '.github/workflows/ci.yml').read_text()
        workflow = (ROOT / '.github/workflows/ci-lifecycle-qualification.yml').read_text()
        baseline = workflow.split('  qualification:', 1)[0]
        def isolated(text):
            return re.search(r'unit-test-projects: \|\n((?:        tests/[^\n]+\n)+)', text).group(1).split()
        self.assertEqual(isolated(baseline), isolated(ordinary))
        self.assertEqual(len(isolated(baseline)), 15)
        for setting in ('integration-test-profiles: platform-package', 'run-consumer-validation: true'):
            self.assertIn(setting, baseline)
        reference = r'Hexalith/Hexalith.Builds/.github/workflows/domain-ci-v2.yml@[0-9a-f]{40}'
        self.assertEqual(re.search(reference, baseline).group(),
                         'Hexalith/Hexalith.Builds/.github/workflows/domain-ci-v2.yml@6ad5854aa14a9950e1fbfc32d7e27953a1c4b82b')
        self.assertEqual(re.search(reference, ordinary).group(),
                         'Hexalith/Hexalith.Builds/.github/workflows/domain-ci-v2.yml@3c8096180ecf2aa539ac454815efbfceb8a92822')
        self.assertIn('    needs: baseline\n', workflow)
        self.assertNotIn('continue-on-error', workflow)
        self.assertNotIn('--filter-trait', workflow)
        self.assertNotIn('--filter-not-trait', workflow)

    def test_reader_permissions_and_independent_finalizer_remain_explicit(self):
        workflow = (ROOT / '.github/workflows/ci-lifecycle-qualification.yml').read_text()
        self.assertIn('permissions:\n  contents: read\n', workflow)
        self.assertEqual(set(re.findall(r'secrets\.([A-Z_]+)', workflow)),
                         {'HEXALITH_CI_REGISTRY_USERNAME', 'HEXALITH_CI_REGISTRY_PASSWORD'})
        self.assertNotIn('secrets: inherit', workflow)
        self.assertNotIn('--recursive', workflow)
        self.assertIn('cancel-in-progress: false', workflow)
        finalizer = workflow.split('- name: Independently finalize every exact same-job owner', 1)[1]
        self.assertTrue(finalizer.lstrip().startswith('if: always()'))
        self.assertIn('ACTUAL_JOB_STATUS: ${{ job.status }}', finalizer)
        self.assertIn('finalize --job-status "$ACTUAL_JOB_STATUS"', finalizer)
        self.assertLess(workflow.index('name: lifecycle-cancellation-ready'),
                        workflow.index('- name: Await parent cancellation'))
        self.assertNotIn('gh run cancel', workflow)
        reader_cleanup = workflow.split('- name: Remove the private reader file', 1)[1].split('- name:', 1)[0]
        self.assertTrue(reader_cleanup.lstrip().startswith('if: always()'))
        self.assertIn('python3 -B scripts/qualify-disposable-ci.py remove-reader', reader_cleanup)

    def test_cancellation_wait_receives_the_runner_signal_sent_only_to_its_step_process(self):
        # The hosted runner cancels a step by signalling only the step's own shell process.
        # Run 42524190110 lost that signal behind a non-forwarding bash wrapper.
        workflow = (ROOT / '.github/workflows/ci-lifecycle-qualification.yml').read_text()
        body = re.search(r'- name: Await parent cancellation of this exact ready run\n'
                         r'        if: [^\n]+\n        run: ([^\n]+)\n', workflow).group(1)
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            probe = root / 'bin' / 'python3'
            probe.parent.mkdir()
            probe.write_text(SIGNAL_PROBE)
            probe.chmod(0o700)
            (root / 'step.sh').write_text(body + '\n')
            markers = {'SIGNAL_MARKER': str(root / 'signal'), 'STARTED_MARKER': str(root / 'started')}
            environment = {**os.environ, **markers, 'PATH': str(probe.parent) + os.pathsep + os.environ['PATH']}
            step = subprocess.Popen(['bash', '-e', str(root / 'step.sh')], cwd=root, env=environment,
                                    stdin=subprocess.DEVNULL, start_new_session=True)
            try:
                deadline = time.monotonic() + 10
                while not (root / 'started').exists():
                    self.assertLess(time.monotonic(), deadline, 'The wait step never started its controller')
                    time.sleep(0.02)
                os.kill(step.pid, signal.SIGINT)
                try:
                    step.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    self.fail('The controller never received the runner signal sent to the step process')
            finally:
                if step.poll() is None:
                    os.killpg(step.pid, signal.SIGKILL)
                    step.wait(timeout=5)
            self.assertEqual((root / 'signal').read_text().split(),
                             ['-B', 'scripts/qualify-disposable-ci.py', 'wait-cancellation'])

    def test_qualification_is_explicit_package_suite_excluded_from_ordinary_solution_build(self):
        manifest = json.loads((ROOT / 'module/hexalith.module-manifest.v2.json').read_text())
        suites = {suite['name']: suite for suite in manifest['modules'][0]['tests']}
        self.assertIn('platform-source', suites)
        self.assertIn('platform-package', suites)
        suite = suites['lifecycle-package']
        self.assertEqual(suite['mode'], 'package')
        self.assertEqual(suite['executor'], 'mtp')
        self.assertEqual(suite['executionTimeoutSeconds'], 300)
        self.assertEqual(suite['stopTimeoutSeconds'], 90)
        for solution in ['Hexalith.Parties.slnx', 'Hexalith.Parties.Standalone.slnx']:
            project = ElementTree.parse(ROOT / solution).find(f'.//Project[@Path="{suite["entry"]}"]')
            self.assertIsNotNone(project)
            self.assertEqual(project.find('Build').attrib['Project'], 'false')
        source = (ROOT / 'tests/Hexalith.Parties.Platform.LifecycleTests/LifecycleQualificationTests.cs').read_text()
        assertion = 'RealDependencies_CreateQueryAndPersistMatchingPartyEvents()'
        self.assertLess(source.index(assertion), source.index('realPersistenceAssertionsPassed = true'))
        self.assertEqual(source.count(assertion), 1)
        recheck = 'new PartyPersistenceAssertions(fixture, output).RecheckAsync()'
        self.assertEqual(source.count(recheck), 1)
        self.assertGreater(source.index(recheck), source.index('if (action == "pass")'))
        self.assertNotIn('Skip', source)


if __name__ == '__main__':
    unittest.main()
