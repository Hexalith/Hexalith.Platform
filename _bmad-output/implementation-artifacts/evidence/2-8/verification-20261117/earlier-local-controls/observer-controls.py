"""Controlled process checks for the unchanged historical hosted observer."""
import ast
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

OBSERVER = Path(sys.argv.pop(1))
WORKFLOW = Path(sys.argv.pop(1)).read_text()


class ObserverControls(unittest.TestCase):
    def test_coarse_roles_and_tags_exclude_argument_and_environment_secrets(self):
        tree = ast.parse(OBSERVER.read_bytes())
        definitions = [node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name in ('role', 'tagged')]
        namespace = {'os': os, 'pathlib': __import__('pathlib'), 'TAG': b'HEXALITH_G4_RUN_ID='}
        exec(compile(ast.Module(body=definitions, type_ignores=[]), 'observer-functions', 'exec'), namespace)
        with patch.object(Path, 'read_bytes', return_value=b'/usr/bin/dotnet\0/work/AppHost.dll\0--mode\0password=do-not-retain\0'):
            self.assertEqual(namespace['role'](99), 'apphost')
        with patch.object(Path, 'read_bytes', return_value=b'PASSWORD=do-not-retain\0HEXALITH_G4_RUN_ID=0123456789abcdef0123456789abcdef\0'):
            self.assertEqual(namespace['tagged'](99), '0123456789abcdef0123456789abcdef')

    def test_both_modes_stop_and_flush_with_fake_service_commands(self):
        for mode in ('lifecycle', 'v2'):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as temporary:
                base = Path(temporary)
                workspace, runner, binaries = (base / name for name in ('workspace', 'runner', 'bin'))
                for directory in (workspace, runner, binaries):
                    directory.mkdir()
                for name, body in [('sudo', '#!/bin/sh\nexit 1\n'), ('docker', '#!/bin/sh\nprintf \'%s\\n\' \'{"Type":"container","Action":"test-only"}\'\n')]:
                    command = binaries / name
                    command.write_text(body)
                    command.chmod(0o700)
                evidence = workspace / 'TestResults' / ('ci-lifecycle' if mode == 'lifecycle' else 'module-integration')
                evidence.mkdir(parents=True)
                if mode == 'lifecycle':
                    (evidence / 'cancel-independent-finalizer.json').write_text('{}')
                else:
                    (evidence / 'cleanup-1.json').write_text('{"jobStatus":"cancelled","status":"completed"}')
                environment = os.environ | {'PATH': str(binaries) + os.pathsep + os.environ['PATH'], 'GITHUB_WORKSPACE': str(workspace), 'RUNNER_TEMP': str(runner), 'XDG_DATA_HOME': str(base / 'data')}
                result = subprocess.run(['python3', '-B', str(OBSERVER), mode], env=environment, capture_output=True, text=True, timeout=15)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                mirror = workspace / 'TestResults' / ('module-integration' if mode == 'v2' else 'story-2-8-proof-observer') / 'story-2-8-proof'
                events = [json.loads(line) for line in (mirror / 'observer.jsonl').read_text().splitlines()]
                for kind in ('trace-unavailable', 'finalizer-observed', 'observer-stopping'):
                    self.assertTrue(any(event['kind'] == kind for event in events))
                self.assertIn('test-only', (mirror / 'docker-events.jsonl').read_text())

    def test_workflow_orders_observation_and_preserves_early_and_final_evidence(self):
        self.assertLess(WORKFLOW.index('Start the Story 2.8 proof event observer'), WORKFLOW.index('Observe actual execution cancellation readiness'))
        self.assertIn('name: lifecycle-cancellation-ready\n          path: |\n            TestResults/ci-lifecycle\n            TestResults/story-2-8-proof-observer', WORKFLOW)
        self.assertIn('Wait for the Story 2.8 proof observer flush', WORKFLOW)
        self.assertIn('name: story-2-8-proof-observer', WORKFLOW)


if __name__ == '__main__':
    unittest.main(verbosity=2)
