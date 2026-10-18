"""Offline fault, safety and evidence checks before disposable backend execution."""

import json
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import probe_raft_non_revival as probe


CONTAINER = 'a' * 64
REVISION = 'b' * 40
SENSITIVE = 'private-response-do-not-retain'


class NativeBackend:
    """Only models native responses; its results never qualify production."""

    def __init__(self):
        self.run_id = None
        self.calls = []
        self.keys = set()
        self.initialized = False
        self.sealed = True
        self.removed = False
        self.version = probe.VERSION
        self.revive = True
        self.control_available = True
        self.foreign_cleanup = False
        self.cleanup_fails = False
        self.snapshot_size = b'4096\n'
        self.cipher_plain = {}
        self.restore_requested = False
        self.restore_inventory_delay = 0
        self.restore_target_delay = 0
        self.restore_control_delay = 0
        self.wrong_plaintext_keys = set()
        self.wrong_plaintext_once_keys = set()
        self.refusal_error = b'Code: 400. Errors:\n* encryption key not found '

    def inspected(self):
        return {'Id': CONTAINER, 'Config': {'Labels': {probe.LABEL: self.run_id},
                'User': '65532:65532'}, 'HostConfig': {
                    'NetworkMode': 'none', 'ReadonlyRootfs': True,
                    'LogConfig': {'Type': 'none'}, 'Privileged': False,
                    'CapDrop': ['ALL'], 'SecurityOpt': ['no-new-privileges'],
                    'Tmpfs': {path: 'rw,noexec,nosuid,nodev' for path in probe.MOUNTS}},
                'Mounts': [{'Type': 'tmpfs', 'Destination': path} for path in probe.MOUNTS]}

    def __call__(self, args, data=None):
        self.calls.append((args, data))

        def result(value=None, code=0, error=b''):
            out = value if isinstance(value, bytes) else json.dumps(value).encode()
            return subprocess.CompletedProcess(args, code, out, error)

        if args[:2] == ['image', 'inspect']:
            return result([{'Id': 'sha256:' + 'c' * 64, 'RepoDigests': [
                'quay.io/openbao/openbao@' + probe.IMAGE.split('@')[1]],
                'Architecture': 'amd64', 'Os': 'linux', 'Config': {
                    'Labels': {'org.opencontainers.image.revision': REVISION},
                    'Volumes': {'/openbao/file': {}, '/openbao/logs': {}}}}])
        if args[0] == 'create':
            self.run_id = args[args.index('--label') + 1].split('=')[1]
            return result((CONTAINER + '\n').encode())
        if args[0] == 'inspect':
            if self.removed or self.run_id is None:
                return result(b'[]', 1, b'error: no such object: ' + CONTAINER.encode())
            value = self.inspected()
            if self.foreign_cleanup:
                value['Config']['Labels'][probe.LABEL] = 'foreign'
            return result([value])
        if args[0] == 'start':
            return result(b'')
        if args[0] == 'rm':
            if self.cleanup_fails:
                return result(b'', 1, SENSITIVE.encode())
            self.removed = True
            return result(b'')
        if args[:3] == ['exec', CONTAINER, 'stat']:
            return result(self.snapshot_size)
        native = args[7:]
        if native == ['version']:
            return result(('OpenBao v' + self.version + ' (' + REVISION + '), built 2026-08-18').encode())
        if native[:3] == ['read', '-format=json', 'sys/seal-status']:
            return result({'data': {'initialized': self.initialized, 'sealed': self.sealed,
                                    'storage_type': 'raft'}})
        if native[:3] == ['read', '-format=json', 'sys/leader']:
            return result({'data': {'ha_enabled': True, 'is_self': True}})
        if native[:2] == ['operator', 'init']:
            self.initialized = True
            return result({'root_token': SENSITIVE, 'unseal_keys_b64': [SENSITIVE]})
        if native[:2] == ['operator', 'raft']:
            if native[3] == 'restore':
                self.restore_requested = True
                if self.revive and not self.restore_inventory_delay:
                    self.keys.add('target')
            return result(b'')
        if native[0] == 'secrets':
            return result(b'')
        if native[0] == 'list':
            if self.restore_requested and self.revive:
                if self.restore_inventory_delay:
                    self.restore_inventory_delay -= 1
                else:
                    self.keys.add('target')
            return result(sorted(self.keys))
        if native[0] == 'delete':
            self.keys.remove('target')
            return result(b'')
        if native[0] == 'write':
            path = native[2]
            payload = json.loads(data.split(b'\n', 1)[1])
            if path == 'sys/unseal':
                self.sealed = False
                return result({'sealed': False})
            key = path.rsplit('/', 1)[-1]
            if path.startswith('probe-transit/keys/'):
                if key != 'config':
                    self.keys.add(key)
                return result({'data': {}})
            if '/encrypt/' in path:
                cipher = 'vault:v1:' + key + '-private-ciphertext'
                self.cipher_plain[cipher] = payload['plaintext']
                return result({'data': {'ciphertext': cipher}})
            if '/decrypt/' in path:
                if key not in self.keys:
                    return result(b'', 2, self.refusal_error + SENSITIVE.encode())
                if key == 'control' and not self.control_available:
                    return result(b'', 2, b'Code: 503. Errors: ' + SENSITIVE.encode())
                if self.restore_requested:
                    delay = 'restore_' + key + '_delay'
                    if getattr(self, delay):
                        setattr(self, delay, getattr(self, delay) - 1)
                        return result(b'', 2, b'Code: 503. Errors: restoring')
                    if key in self.wrong_plaintext_keys or key in self.wrong_plaintext_once_keys:
                        self.wrong_plaintext_once_keys.discard(key)
                        return result({'data': {'plaintext': 'different-original-plaintext'}})
                return result({'data': {'plaintext': self.cipher_plain[payload['ciphertext']]}})
        raise AssertionError('Unmodeled native operation')


class ProbeTests(unittest.TestCase):
    def make_probe(self, backend=None):
        backend = backend or NativeBackend()
        return probe.Probe(probe.IMAGE, runner=backend, sleep=lambda _: None), backend

    def test_complete_negative_control_is_exit_two_and_never_qualifies(self):
        instance, backend = self.make_probe()
        result, code = instance.run()
        self.assertEqual(2, code, result)
        self.assertEqual('ObservedRevival', result['outcome'])
        self.assertTrue(backend.removed)
        self.assertTrue(result['cleanup']['complete'])
        for field in ('productionQualified', 'g1Qualified', 'transactionProofExecuted',
                      'productionEngineTested', 'independentErasureAuthorityTested'):
            self.assertIs(result[field], False)
        for stage in probe.REQUIRED:
            self.assertTrue(result['observations'][stage]['verified'], stage)

    def test_each_incomplete_stage_blocks_and_cleans_up(self):
        # Exercise the complete orchestration, rather than just its evaluator.
        stages = ('image-inspect', 'container-create', 'container-inspect', 'container-start',
                  'native-version', 'startup-raft', 'initialize', 'unsealed', 'raft-leader',
                  'transit-enabled', 'target-created', 'control-created', 'target-encrypted',
                  'control-encrypted', 'target-before-delete', 'control-before-snapshot',
                  'snapshot-saved', 'snapshot-present', 'control-after-snapshot',
                  'target-deletion-enabled', 'target-deleted', 'control-after-delete',
                  'target-absent', 'target-decrypt-refused', 'control-before-restore',
                  'snapshot-restored', 'restore-unsealed', 'restore-raft-leader',
                  'control-after-restore', 'target-present-after-restore',
                  'target-decrypt-after-restore')
        for stage in stages:
            with self.subTest(stage=stage):
                instance, backend = self.make_probe()
                original = instance.command

                def fail(selected, *args, **kwargs):
                    if selected == stage:
                        raise probe.Blocked('CommandTimeout')
                    return original(selected, *args, **kwargs)

                instance.command = fail
                result, code = instance.run()
                self.assertEqual(1, code)
                self.assertEqual('BlockedUnknown', result['outcome'])
                self.assertEqual(stage != 'container-create', result['cleanup']['complete'])

    def test_version_mismatch_stops_before_initialization_or_key_creation(self):
        instance, backend = self.make_probe()
        backend.version = '2.6.0'
        result, code = instance.run()
        self.assertEqual(1, code)
        self.assertEqual('NativeVersionMismatch', result['failure'])
        self.assertFalse(backend.initialized)
        self.assertFalse(backend.keys)
        self.assertTrue(backend.removed)

    def test_only_discovered_exact_image_pin_accepted_before_docker(self):
        for image in ('openbao/openbao:latest', probe.IMAGE.replace('2.6.2', '2.6.0'),
                      probe.IMAGE.replace('11fd73', '22fd73'), 'http://production:8200'):
            with self.subTest(image=image):
                backend = NativeBackend()
                result, code = probe.Probe(image, backend).run()
                self.assertEqual(1, code)
                self.assertEqual([], backend.calls)
                self.assertIsNone(result['image'])

    def test_daemon_pin_no_network_no_persistence_no_implicit_pull(self):
        args = probe.create_args(probe.IMAGE, 'public-run', 'public-name')
        self.assertEqual(['docker', '--host', 'unix:///var/run/docker.sock'], probe.DOCKER)
        for setting in ('--pull=never', '--network=none', '--read-only', '--log-driver=none',
                        '--cap-drop=ALL', '--security-opt=no-new-privileges'):
            self.assertIn(setting, args)
        self.assertFalse(any(arg in args for arg in ('--volume', '-v', '-p', '--publish')))
        self.assertIn('127.0.0.1:8200', probe.CONFIG)
        self.assertNotIn('0.0.0.0', probe.CONFIG)
        self.assertNotIn('disable_mlock', probe.CONFIG)
        self.assertNotIn('-dev', args)
        self.assertEqual(3, args.count('--tmpfs'))

    def test_inspected_unsafe_boundaries_rejected(self):
        backend = NativeBackend()
        backend.run_id = 'owned'
        for mutate in (
                lambda v: v['HostConfig'].update(NetworkMode='host'),
                lambda v: v['HostConfig'].update(Binds=['/production:/data']),
                lambda v: v['HostConfig'].update(PortBindings={'8200/tcp': [{}]}),
                lambda v: v['HostConfig'].update(Privileged=True),
                lambda v: v['HostConfig'].update(ReadonlyRootfs=False),
                lambda v: v['HostConfig'].update(LogConfig={'Type': 'json-file'}),
                lambda v: v['HostConfig'].update(Tmpfs={}),
                lambda v: v['Mounts'].append({'Type': 'volume', 'Destination': '/data'})):
            value = backend.inspected()
            mutate(value)
            with self.assertRaises(probe.Blocked):
                probe.require_isolation(value, 'owned', CONTAINER)

    def test_result_has_no_token_unseal_key_ciphertext_plaintext_or_raw_error(self):
        instance, backend = self.make_probe()
        result, code = instance.run()
        self.assertEqual(2, code)
        public = json.dumps(result)
        self.assertNotIn(SENSITIVE, public)
        self.assertNotIn('vault:v1:', public)
        for plaintext in backend.cipher_plain.values():
            self.assertNotIn(plaintext, public)
        for args, _ in backend.calls:
            self.assertNotIn(SENSITIVE, ' '.join(args))
        self.assertEqual('', instance.token)
        self.assertEqual('', instance.unseal_key)
        self.assertEqual({}, instance.ciphertexts)
        self.assertEqual({}, instance.plaintexts)

    def test_outage_cannot_be_read_as_deletion_or_revival(self):
        instance, backend = self.make_probe()
        backend.control_available = False
        result, code = instance.run()
        self.assertEqual(1, code)
        self.assertNotIn('target-decrypt-refused', result['observations'])
        self.assertNotIn('snapshot-restored', result['observations'])

    def test_nonrevival_is_unknown_and_cannot_qualify_erasure(self):
        instance, backend = self.make_probe()
        backend.revive = False
        result, code = instance.run()
        self.assertEqual(1, code)
        self.assertEqual('BlockedUnknown', result['outcome'])
        self.assertFalse(result['g1Qualified'])

    def test_delayed_restore_polls_inventory_target_and_control_without_repeating_effects(self):
        instance, backend = self.make_probe()
        backend.restore_inventory_delay = 3
        backend.restore_target_delay = 2
        backend.restore_control_delay = 2
        sleeps = []
        instance.sleep = sleeps.append
        result, code = instance.run()
        self.assertEqual(2, code, result)
        self.assertTrue(result['cleanup']['complete'])
        self.assertGreaterEqual(len(sleeps), 7)
        for stage in ('target-present-after-restore', 'target-decrypt-after-restore',
                      'control-after-restore'):
            self.assertTrue(result['observations'][stage]['verified'])
        for stage in ('snapshot-restored', 'target-deleted'):
            self.assertEqual(1, sum(command['stage'] == stage for command in result['commands']))

    def test_restored_target_or_control_wrong_plaintext_never_verifies_revival(self):
        for key, stage in (('target', 'target-decrypt-after-restore'),
                           ('control', 'control-after-restore')):
            with self.subTest(key=key):
                instance, backend = self.make_probe()
                backend.wrong_plaintext_once_keys.add(key)
                result, code = instance.run()
                self.assertEqual(1, code)
                self.assertEqual('BlockedUnknown', result['outcome'])
                self.assertNotIn(stage, result['observations'])
                self.assertEqual('PlaintextMatchUnverified', result['failure'])
                self.assertTrue(result['cleanup']['complete'])
                self.assertEqual(1, sum(c['stage'] == stage for c in result['commands']))
                self.assertEqual(1, sum(c['stage'] == 'snapshot-restored' for c in result['commands']))

    def test_malformed_restored_inventory_is_terminal_even_if_later_read_would_succeed(self):
        instance, backend = self.make_probe()
        original = instance.runner
        corrupted = False

        def corrupt_first_restored_inventory(args, data=None):
            nonlocal corrupted
            response = original(args, data=data)
            if backend.restore_requested and args[7:8] == ['list'] and not corrupted:
                corrupted = True
                return subprocess.CompletedProcess(args, 0, b'{malformed', b'')
            return response

        instance.runner = corrupt_first_restored_inventory
        result, code = instance.run()
        self.assertEqual(1, code)
        self.assertEqual('MalformedNativeResponse', result['failure'])
        self.assertNotIn('target-present-after-restore', result['observations'])
        self.assertTrue(result['cleanup']['complete'])
        self.assertEqual(1, sum(c['stage'] == 'target-present-after-restore' for c in result['commands']))

    def test_healthy_control_does_not_turn_other_target_errors_into_missing_key(self):
        errors = (b'Code: 403. Errors: encryption key not found ',
                  b'Code: 503. Errors: encryption key not found ',
                  b'Code: 400. Errors: permission denied ',
                  b'Errors: encryption key not found ')
        for error in errors:
            with self.subTest(error=error):
                instance, backend = self.make_probe()
                backend.refusal_error = error
                result, code = instance.run()
                self.assertEqual(1, code)
                self.assertTrue(result['observations']['control-after-delete']['verified'])
                self.assertNotIn('target-decrypt-refused', result['observations'])
                self.assertTrue(result['cleanup']['complete'])
                self.assertFalse(backend.restore_requested)
                self.assertFalse(any(c['stage'] == 'snapshot-restored' for c in result['commands']))

    def test_zero_or_unverifiable_snapshot_blocks_before_delete(self):
        for size in (b'0', b'unknown', str(129 * 1024 * 1024).encode()):
            instance, backend = self.make_probe()
            backend.snapshot_size = size
            result, code = instance.run()
            self.assertEqual(1, code)
            self.assertNotIn('target-deleted', result['observations'])
            self.assertTrue(result['cleanup']['complete'])

    def test_cleanup_failure_overrides_complete_observation(self):
        instance, backend = self.make_probe()
        backend.cleanup_fails = True
        result, code = instance.run()
        self.assertTrue(result['observations']['target-decrypt-after-restore']['verified'])
        self.assertEqual(1, code)
        self.assertEqual('BlockedUnknown', result['outcome'])
        self.assertFalse(result['cleanup']['complete'])
        self.assertNotIn(SENSITIVE, json.dumps(result))

    def test_cleanup_never_removes_foreign_container(self):
        instance, backend = self.make_probe()
        backend.foreign_cleanup = True
        result, code = instance.run()
        self.assertEqual(1, code)
        self.assertFalse(result['cleanup']['complete'])
        self.assertFalse(any(args[0] == 'rm' for args, _ in backend.calls))

    def test_ambiguous_create_recovers_only_owned_container_for_cleanup(self):
        instance, backend = self.make_probe()
        original = instance.runner

        def timeout_after_create(args, data=None):
            response = original(args, data=data)
            if args[0] == 'create':
                raise probe.Blocked('CommandTimeout')
            return response

        instance.runner = timeout_after_create
        result, code = instance.run()
        self.assertEqual(1, code)
        self.assertTrue(result['cleanup']['complete'])
        self.assertTrue(backend.removed)

    def test_delayed_unacknowledged_create_remains_cleanup_unknown_after_absent_name(self):
        instance, backend = self.make_probe()
        original = instance.runner

        def delayed_create(args, data=None):
            if args[0] == 'create':
                raise probe.Blocked('CommandTimeout')
            return original(args, data=data)

        instance.runner = delayed_create
        result, code = instance.run()
        self.assertEqual(1, code)
        self.assertFalse(result['cleanup']['complete'])
        self.assertEqual('CreateCompletionUnknown', result['cleanup']['failure'])
        self.assertFalse(any(args[0] == 'rm' for args, _ in backend.calls))
        # The daemon can still publish the original container after that read.
        backend.run_id = instance.run_id
        self.assertEqual(0, backend(['inspect', instance.name]).returncode)
        self.assertFalse(backend.removed)

    def test_public_recovery_identity_is_flushed_before_first_docker_effect(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'result.json'
            instance, backend = self.make_probe()

            def check_prepared(args, data=None):
                prepared = json.loads(output.read_text())
                self.assertEqual(instance.run_id, prepared['runId'])
                self.assertEqual(instance.name, prepared['containerName'])
                self.assertEqual({probe.LABEL: instance.run_id}, prepared['ownershipLabel'])
                self.assertEqual('Prepared', prepared['phase'])
                self.assertTrue(flushed.called)
                return backend(args, data=data)

            instance.runner = check_prepared
            with patch.object(probe, 'Probe', return_value=instance), \
                    patch.object(probe.os, 'fsync', wraps=probe.os.fsync) as flushed:
                self.assertEqual(2, probe.main(['--image', probe.IMAGE, '--output', str(output)]))
            final = json.loads(output.read_text())
            self.assertEqual(instance.run_id, final['runId'])
            self.assertEqual('ObservedRevival', final['outcome'])

    def test_interrupted_interpreter_leaves_prepared_recovery_identity(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'result.json'
            instance, backend = self.make_probe()
            with patch.object(probe, 'Probe', return_value=instance), \
                    patch.object(instance, 'run', side_effect=SystemExit(99)):
                with self.assertRaises(SystemExit):
                    probe.main(['--image', probe.IMAGE, '--output', str(output)])
            prepared = json.loads(output.read_text())
            self.assertEqual(instance.name, prepared['containerName'])
            self.assertEqual({probe.LABEL: instance.run_id}, prepared['ownershipLabel'])
            self.assertEqual('Prepared', prepared['phase'])
            self.assertFalse(prepared['cleanup']['complete'])
            self.assertEqual([], backend.calls)

    def test_malformed_native_response_or_exception_text_is_sanitized(self):
        for error in (ValueError(SENSITIVE), probe.Blocked('MalformedNativeResponse')):
            instance, _ = self.make_probe()
            instance.execute = lambda: (_ for _ in ()).throw(error)
            result, code = instance.run()
            self.assertEqual(1, code)
            self.assertNotIn(SENSITIVE, json.dumps(result))
        with self.assertRaisesRegex(probe.Blocked, '^MalformedNativeResponse$'):
            probe.document(SENSITIVE)

    def test_local_command_failures_do_not_expose_private_subprocess_bytes(self):
        for exception in (subprocess.TimeoutExpired('docker', 20, output=SENSITIVE), OSError(SENSITIVE)):
            with patch.object(probe.subprocess, 'run', side_effect=exception):
                with self.assertRaises(probe.Blocked) as error:
                    probe.run_command(['inspect', 'owned'])
                self.assertNotIn(SENSITIVE, str(error.exception))

    def test_output_is_never_overwritten_and_no_runtime_or_token_arguments_exist(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'result.json'
            output.write_text('original')
            with patch.object(probe, 'Probe') as factory:
                code = probe.main(['--image', probe.IMAGE, '--output', str(output)])
            self.assertEqual(1, code)
            self.assertEqual('original', output.read_text())
            factory.assert_not_called()
        with self.assertRaises(SystemExit) as error:
            probe.main(['--image', probe.IMAGE, '--output', '/unused', '--token', SENSITIVE])
        self.assertEqual(1, error.exception.code)


if __name__ == '__main__':
    unittest.main()
