"""Failure regressions use local fixtures and never contact a Kubernetes cluster."""
from contextlib import contextmanager
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import subprocess
import tempfile
import threading
import time
import unittest
from unittest.mock import patch

import qualify


class QualifierFailureTests(unittest.TestCase):
    @contextmanager
    def qualification(self):
        with tempfile.TemporaryDirectory(prefix='h45-failure-evidence-') as directory:
            q = qualify.Qualification(Path(directory))
            try:
                yield q
            finally:
                if q.temp.exists():
                    q.created = False
                    q.cleanup()

    def test_private_files_removed_when_cluster_delete_or_inventory_fails(self):
        for failure_call in (1, 2, 3):
            with self.subTest(failure_call=failure_call), self.qualification() as q:
                q.created = True
                q.kubeconfig.write_text('invocation-private-credential')
                calls = []
                def run(args, **kwargs):
                    calls.append(args)
                    if len(calls) == failure_call:
                        raise RuntimeError('demonstrated cleanup failure')
                    return subprocess.CompletedProcess(args, 0, '', '')
                with patch.object(q, 'run', side_effect=run):
                    with self.assertRaisesRegex(RuntimeError, 'demonstrated cleanup failure'):
                        q.cleanup()
                self.assertFalse(q.temp.exists())
                self.assertFalse(q.record['cleanup']['complete'])
                self.assertTrue(q.record['cleanup']['temporaryKubeconfigRemoved'])
                self.assertTrue(q.record['cleanup']['temporaryDirectoryRemoved'])
                self.assertIn('demonstrated cleanup failure', q.record['cleanup']['error'])
                self.assertEqual(calls[0][0:4], ['kind', 'delete', 'cluster', '--name'])
                self.assertEqual(calls[0][4], q.cluster)

    def test_cleanup_failure_stays_failed_in_durable_result(self):
        with self.qualification() as q:
            q.created = True
            q.kubeconfig.write_text('private')
            with patch.multiple(q, preflight=lambda: None, native_probe=lambda: None,
                                static_checks=lambda: None,
                                environment=lambda: None, admission_and_ssa=lambda: None,
                                verify_source=lambda: None), patch.object(q, 'create', lambda: None), patch.object(q, 'run', side_effect=RuntimeError('cannot delete owned cluster')):
                with self.assertRaisesRegex(RuntimeError, 'cannot delete owned cluster'):
                    q.execute()
            import json
            record = json.loads((q.evidence / 'qualification.json').read_text())
            self.assertEqual(record['status'], 'failed')
            self.assertFalse(record['cleanup']['complete'])
            self.assertTrue(record['cleanup']['temporaryKubeconfigRemoved'])

    def test_source_inventory_detects_added_deleted_and_changed_files(self):
        with self.qualification() as q, tempfile.TemporaryDirectory(prefix='h45-inventory-') as directory:
            root = Path(directory)
            chart = root / 'eng/helm-qualification/chart'
            chart.mkdir(parents=True)
            original = chart / 'original.yaml'
            for scenario in ('added', 'removed', 'modified'):
                with self.subTest(scenario=scenario):
                    original.write_text('before')
                    added = chart / 'new-template.yaml'
                    added.unlink(missing_ok=True)
                    with patch.multiple(qualify, ROOT=root, CHART=chart, HERE=chart.parent):
                        q.record['source'] = {'files': q.source_inventory()}
                        if scenario == 'added': added.write_text('new object')
                        elif scenario == 'removed': original.unlink()
                        else: original.write_text('after')
                        with self.assertRaisesRegex(AssertionError, 'source inventory or contents changed'):
                            q.verify_source()
                    expected = added if scenario == 'added' else original
                    self.assertEqual(q.record['sourceChangedDuringRun'][scenario], [str(expected.relative_to(root))])

    def test_trickled_download_has_a_complete_transfer_deadline(self):
        class Trickle(BaseHTTPRequestHandler):
            def log_message(self, *args): pass
            def do_GET(self):
                self.send_response(200)
                self.end_headers()
                try:
                    for _ in range(100):
                        self.wfile.write(b'x')
                        self.wfile.flush()
                        time.sleep(0.05)
                except (BrokenPipeError, ConnectionResetError): pass
        server = ThreadingHTTPServer(('127.0.0.1', 0), Trickle)
        server.daemon_threads = True
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            with self.qualification() as q:
                q.DOWNLOAD_TIMEOUT_SECONDS = 0.25
                q.tools['trickle'] = {'url': f'http://127.0.0.1:{server.server_port}/data', 'sha256': 'never-completes'}
                started = time.monotonic()
                with self.assertRaisesRegex(RuntimeError, 'command failed|bounded command timed out'):
                    q.download('trickle', 'partial')
                self.assertLess(time.monotonic() - started, 2)
                self.assertNotIn('trickle', q.record.get('downloads', {}))
        finally:
            server.shutdown()
            server.server_close()
            thread.join(timeout=2)

    def test_command_logs_are_visible_to_version_control(self):
        log = qualify.DEFAULT_EVIDENCE / 'qualification-command.log'
        result = subprocess.run(['git', 'check-ignore', '-q', str(log)], cwd=qualify.ROOT)
        self.assertEqual(result.returncode, 1, 'qualification logs must be version-control-visible')


if __name__ == '__main__':
    unittest.main()
