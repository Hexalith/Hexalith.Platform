"""Synthetic-only static preflight checks; never open the actual kubeconfig."""

import base64
from datetime import datetime
from copy import deepcopy
import fcntl
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import stat
import sys
import tempfile
import unittest
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location('static_preflight', Path(__file__).with_name('static_transport_preflight.py'))
guard = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(guard)
REAL_POPEN = subprocess.Popen
REAL_KILLPG = os.killpg


class StaticTransportTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.config = {'kind': 'Config', 'apiVersion': 'v1', 'current-context': guard.CONTEXT,
            'preferences': {}, 'contexts': [{'name': guard.CONTEXT, 'context': {'cluster': 'local', 'user': 'jpiquot'}}],
            'clusters': [{'name': 'local', 'cluster': {'server': guard.ENDPOINT,
                'certificate-authority-data': base64.b64encode(b'-----BEGIN CERTIFICATE-----\nsynthetic-public\n-----END CERTIFICATE-----').decode()}}],
            'users': [{'name': 'jpiquot', 'user': {'token': 'synthetic-secret-never-output'}}]}
        self.raw = b'synthetic sealed input; not a real kubeconfig'
        self.paths = {}
        for name in ('SOURCE', 'PYTHON', 'KUBECTL', 'KUBECONFIG'):
            path = self.root / name
            path.write_bytes(self.raw if name == 'KUBECONFIG' else b'inert synthetic binary; never execute')
            path.chmod(0o600)
            self.paths[name] = path
        self.patches = [patch.object(guard, key, value) for key, value in self.paths.items()]
        self.patches += [patch.object(guard, 'EXPECTED_CONFIG_SHA256', guard.sha(self.raw)),
                         patch.object(guard, 'EXPECTED_KUBECTL_SHA256', guard.sha(self.paths['KUBECTL'].read_bytes()))]
        for value in self.patches:
            value.start()
            self.addCleanup(value.stop)
        self.binding = {'kind': 'hexalith.local-static-transport-preflight', 'schemaVersion': 1,
            'kubeconfig': {'path': str(guard.KUBECONFIG), 'sha256': guard.EXPECTED_CONFIG_SHA256,
                          'context': guard.CONTEXT, 'endpoint': guard.ENDPOINT},
            **{name: {'path': str(self.paths[role]), 'sha256': guard.sha(self.paths[role].read_bytes()),
                      'bytes': self.paths[role].stat().st_size}
               for name, role in [('source', 'SOURCE'), ('interpreter', 'PYTHON'), ('kubectl', 'KUBECTL')]},
            'limits': {'maximumConfigBytes': guard.MAX_CONFIG_BYTES, 'maximumOutputBytes': guard.MAX_OUTPUT_BYTES,
                       'timeoutSeconds': guard.TIMEOUT_SECONDS, 'networkCallsAllowed': False, 'authenticationAllowed': False}}

    def test_synthetic_static_config_passes_without_network_or_authentication(self):
        with patch.object(guard, 'run_view', return_value=json.dumps(self.config).encode()) as view:
            result = guard.preflight(self.binding)
        self.assertEqual(view.call_args.args, (self.raw,))
        self.assertEqual(result['operationalGate'], 'closed')
        self.assertFalse(result['authenticationPerformed'])
        self.assertFalse(result['authorityAccepted'])

    def test_exact_sealed_input_fixed_argv_clear_environment_and_no_shell(self):
        observed = {}
        output = json.dumps(self.config).encode()
        def fake(argv, **kwargs):
            observed.update(argv=argv, kwargs=kwargs)
            fd = kwargs['pass_fds'][0]
            self.assertEqual(os.fstat(fd).st_uid, os.getuid())
            self.assertEqual(stat.S_IMODE(os.fstat(fd).st_mode), 0o600)
            self.assertEqual(os.read(fd, 4096), self.raw)
            self.assertTrue(fcntl.fcntl(fd, fcntl.F_GET_SEALS) & fcntl.F_SEAL_WRITE)
            with self.assertRaises(OSError): os.write(fd, b'tamper')
            return REAL_POPEN([sys.executable, '-I', '-S', '-B', '-c',
                'import sys;sys.stdout.buffer.write(' + repr(output) + ')'],
                stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                start_new_session=True, env={'LANG': 'C.UTF-8'})
        with patch.object(guard.subprocess, 'Popen', side_effect=fake):
            self.assertEqual(guard.run_view(self.raw), output)
        argv, kwargs = observed['argv'], observed['kwargs']
        self.assertEqual(argv[0], str(guard.KUBECTL))
        self.assertEqual(argv[2], f"/proc/self/fd/{kwargs['pass_fds'][0]}")
        self.assertEqual(argv[3:], ['--context', guard.CONTEXT, 'config', 'view', '--raw', '-o', 'json'])
        self.assertEqual(kwargs['env'], {'LANG': 'C.UTF-8'})
        self.assertFalse(kwargs['shell'])
        self.assertTrue(kwargs['start_new_session'])

    def test_exec_auth_provider_file_credentials_proxy_and_insecure_tls_refused(self):
        changes = [('exec', {'command': 'arbitrary-command'}), ('auth-provider', {'name': 'oidc'}),
                   ('tokenFile', '/private/token'), ('client-key', '/private/id_rsa'),
                   ('username', 'administrator'), ('password', 'secret')]
        for key, value in changes:
            with self.subTest(user=key):
                config = deepcopy(self.config)
                config['users'][0]['user'][key] = value
                with self.assertRaises(guard.Refusal): guard.validate_schema(config)
        for key, value in [('proxy-url', 'http://proxy'), ('insecure-skip-tls-verify', True),
                           ('certificate-authority', '/private/ca'), ('tls-server-name', 'other')]:
            with self.subTest(cluster=key):
                config = deepcopy(self.config)
                config['clusters'][0]['cluster'][key] = value
                with self.assertRaises(guard.Refusal): guard.validate_schema(config)

    def test_wrong_endpoint_context_or_missing_ca_refused(self):
        for mode in ('endpoint', 'context', 'ca'):
            with self.subTest(mode=mode):
                config = deepcopy(self.config)
                if mode == 'endpoint': config['clusters'][0]['cluster']['server'] = 'https://unapproved.invalid'
                elif mode == 'context': config['current-context'] = 'other'
                else: del config['clusters'][0]['cluster']['certificate-authority-data']
                with self.assertRaises(guard.Refusal): guard.validate_schema(config)

    def test_wrong_input_digest_mode_and_symlink_refused_before_parser(self):
        for mode in ('digest', 'mode', 'symlink'):
            with self.subTest(mode=mode):
                path = guard.KUBECONFIG
                if path.is_symlink(): path.unlink()
                path.write_bytes(self.raw)
                path.chmod(0o600)
                if mode == 'digest': path.write_bytes(b'wrong bytes')
                elif mode == 'mode': path.chmod(0o644)
                else:
                    path.unlink()
                    path.symlink_to(guard.SOURCE)
                with patch.object(guard, 'run_view') as view:
                    with self.assertRaises(guard.Refusal): guard.preflight(self.binding)
                    view.assert_not_called()

    def test_existing_nonwritable0755_config_parent_passes_but_writable_parent_refuses(self):
        directory = self.root / 'existing-kube-directory'
        directory.mkdir(mode=0o755)
        config = directory / 'existing-config'
        config.write_bytes(self.raw)
        config.chmod(0o600)
        self.binding['kubeconfig']['path'] = str(config)
        with patch.object(guard, 'KUBECONFIG', config), patch.object(guard, 'run_view', return_value=json.dumps(self.config).encode()) as view:
            self.assertEqual(guard.preflight(self.binding)['operationalGate'], 'closed')
            directory.chmod(0o777)
            view.reset_mock()
            with self.assertRaisesRegex(guard.Refusal, 'writable local custody'): guard.preflight(self.binding)
            view.assert_not_called()

    def test_fresh_binding_parent_still_requires0700(self):
        directory = self.root / 'fresh-binding'
        directory.mkdir(mode=0o755)
        binding = directory / 'binding.json'
        binding.write_text(json.dumps(self.binding))
        binding.chmod(0o600)
        output = io.StringIO()
        with patch.object(guard, 'BINDING', binding), patch('sys.argv', ['preflight', '--binding-sha256', guard.sha(binding.read_bytes())]), \
                patch.object(guard, 'preflight') as preflight, patch('sys.stdout', output):
            self.assertEqual(guard.main(), 1)
            preflight.assert_not_called()
        self.assertEqual(json.loads(output.getvalue())['operationalGate'], 'closed')

    def test_failure_output_is_sanitized_and_never_contains_secrets(self):
        binding_path = self.root / 'binding.json'
        binding_path.write_text(json.dumps(self.binding))
        binding_path.chmod(0o600)
        output = io.StringIO()
        with patch.object(guard, 'BINDING', binding_path), patch('sys.argv', ['preflight', '--binding-sha256', guard.sha(binding_path.read_bytes())]), \
                patch.object(guard, 'run_view', side_effect=ValueError('synthetic-secret-never-output')), patch('sys.stdout', output):
            self.assertEqual(guard.main(), 1)
        self.assertNotIn('synthetic-secret', output.getvalue())
        self.assertEqual(json.loads(output.getvalue())['operationalGate'], 'closed')

    def test_successful_cli_receipt_binds_exact_inputs_and_utc_time(self):
        binding_path = self.root / 'binding.json'
        binding_path.write_text(json.dumps(self.binding))
        binding_path.chmod(0o600)
        output = io.StringIO()
        with patch.object(guard, 'BINDING', binding_path), patch('sys.argv', ['preflight', '--binding-sha256', guard.sha(binding_path.read_bytes())]), \
                patch.object(guard, 'run_view', return_value=json.dumps(self.config).encode()), patch('sys.stdout', output):
            self.assertEqual(guard.main(), 0)
        result = json.loads(output.getvalue())
        self.assertEqual(result['inputs'], {'bindingSha256': guard.sha(binding_path.read_bytes()),
            'kubeconfigSha256': guard.EXPECTED_CONFIG_SHA256, 'sourceSha256': self.binding['source']['sha256'],
            'kubectlSha256': guard.EXPECTED_KUBECTL_SHA256, 'interpreterSha256': self.binding['interpreter']['sha256']})
        self.assertTrue(result['observedAt'].endswith('Z'))
        self.assertIsNotNone(datetime.fromisoformat(result['observedAt']).tzinfo)
        self.assertEqual(result['apiCalls'], 0)
        self.assertFalse(result['authenticationPerformed'])
        self.assertFalse(result['authorityAccepted'])
        self.assertEqual(result['operationalGate'], 'closed')
        self.assertNotIn('synthetic-secret', output.getvalue())

    def test_invalid_hash_and_unrecognized_arguments_are_not_echoed(self):
        binding = self.root / 'synthetic-invalid-invocation-binding.json'
        binding.write_text(json.dumps(self.binding))
        binding.chmod(0o600)
        for argv in [['preflight', '--binding-sha256', 'synthetic-secret-invalid-hash'],
                     ['preflight', '--unexpected-synthetic-secret-argument']]:
            output, errors = io.StringIO(), io.StringIO()
            with patch.object(guard, 'BINDING', binding), patch('sys.argv', argv), patch('sys.stdout', output), patch('sys.stderr', errors):
                self.assertEqual(guard.main(), 1)
            self.assertNotIn('synthetic-secret', output.getvalue() + errors.getvalue())
            self.assertEqual(json.loads(output.getvalue())['operationalGate'], 'closed')

    def test_child_failure_and_timeout_do_not_emit_child_diagnostics(self):
        for timed in (False, True):
            code = ('import time;time.sleep(20)' if timed else
                    'import sys;sys.stdout.write("synthetic-secret-output");sys.stderr.write("synthetic-secret-error");sys.exit(1)')
            children = []
            def fake(*args, **kwargs):
                child = REAL_POPEN([sys.executable, '-I', '-S', '-B', '-c', code],
                    stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                    start_new_session=True, env={'LANG': 'C.UTF-8'})
                children.append(child)
                return child
            with self.subTest(timed=timed), patch.object(guard.subprocess, 'Popen', side_effect=fake), \
                    patch.object(guard, 'TIMEOUT_SECONDS', 0.1), patch.object(guard.os, 'killpg', wraps=REAL_KILLPG) as kill:
                with self.assertRaises(guard.Refusal) as error: guard.run_view(self.raw)
                self.assertNotIn('secret', str(error.exception))
                self.assertEqual(kill.called, timed)
                self.assertIsNotNone(children[0].returncode)

    def test_oversized_stdout_or_stderr_is_killed_and_reaped_without_raw_output(self):
        for descriptor, limit in [(1, guard.MAX_OUTPUT_BYTES), (2, guard.MAX_ERROR_BYTES)]:
            with self.subTest(pipe=descriptor):
                children = []
                code = f'import os,time;os.write({descriptor},b"x"*{limit+1});time.sleep(20)'
                def fake(*args, **kwargs):
                    child = REAL_POPEN([sys.executable, '-I', '-S', '-B', '-c', code],
                        stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                        start_new_session=True, env={'LANG': 'C.UTF-8'})
                    children.append(child)
                    return child
                with patch.object(guard.subprocess, 'Popen', side_effect=fake), \
                        patch.object(guard.os, 'killpg', wraps=REAL_KILLPG) as kill:
                    with self.assertRaisesRegex(guard.Refusal, 'exceeded bounds'): guard.run_view(self.raw)
                kill.assert_called_once()
                self.assertEqual(kill.call_args.args[0], children[0].pid)
                self.assertIsNotNone(children[0].returncode)


if __name__ == '__main__':
    unittest.main(verbosity=2)
