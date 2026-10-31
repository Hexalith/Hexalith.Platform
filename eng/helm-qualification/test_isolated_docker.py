"""Actual local filesystem tests; no daemon, host setup or calibration attempt."""
import configparser
import hashlib
import json
import os
from pathlib import Path
import socket
import stat
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import isolated_docker as isolated


class IsolatedDockerTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='isolated-docker-offline-test-')
        self.addCleanup(self.temporary.cleanup)
        self.parent = Path(self.temporary.name)
        self.parent.chmod(0o700)
        self.output = self.parent / 'proposal'

    def snapshot(self, root):
        return {str(path.relative_to(root)): (path.read_bytes(), stat.S_IMODE(path.stat().st_mode))
            for path in root.rglob('*') if path.is_file()}

    def test_private_real_files_and_hashes_even_with_permissive_umask(self):
        old_umask = os.umask(0)
        try:
            receipt = isolated.render(self.output)
        finally:
            os.umask(old_umask)
        self.assertEqual(0o700, stat.S_IMODE(self.output.stat().st_mode))
        self.assertEqual(receipt, json.loads((self.output / 'receipt.json').read_bytes()))
        self.assertEqual(set(receipt['files']) | {'receipt.json'}, set(self.snapshot(self.output)))
        for path in self.output.rglob('*'):
            self.assertFalse(path.is_symlink())
            self.assertEqual(0o700 if path.is_dir() else 0o600, stat.S_IMODE(path.stat().st_mode))
        for name, expected in receipt['files'].items():
            content = (self.output / name).read_bytes()
            self.assertGreater(len(content), 0)
            self.assertEqual({'sha256': hashlib.sha256(content).hexdigest(),
                'sizeBytes': len(content), 'mode': '0600'}, expected)
        self.assertEqual(hashlib.sha256(Path(isolated.__file__).read_bytes()).hexdigest(), receipt['generatorSha256'])

    def test_reproducible_across_paths_and_umasks(self):
        isolated.render(self.output)
        other = self.parent / 'other'
        old_umask = os.umask(0o027)
        try:
            isolated.render(other)
        finally:
            os.umask(old_umask)
        self.assertEqual(self.snapshot(self.output), self.snapshot(other))

    def test_restrictive_umask_is_restored_and_cannot_make_bundle_unreadable(self):
        original = os.umask(0o777)
        try:
            isolated.render(self.output)
            self.assertEqual(0o777, os.umask(0o777))
            self.assertEqual(0o600, stat.S_IMODE((self.output / 'receipt.json').stat().st_mode))
            with self.assertRaises(FileExistsError):
                isolated.render(self.output)
            self.assertEqual(0o777, os.umask(0o777))
        finally:
            os.umask(original)

    def test_existing_bundle_refuses_and_preserves_every_byte_and_mode(self):
        isolated.render(self.output)
        before = self.snapshot(self.output)
        with self.assertRaises(FileExistsError):
            isolated.render(self.output)
        self.assertEqual(before, self.snapshot(self.output))

    def test_existing_empty_directory_and_regular_file_refuse(self):
        self.output.mkdir(mode=0o750)
        self.output.chmod(0o750)
        with self.assertRaises(FileExistsError):
            isolated.render(self.output)
        self.assertEqual([], list(self.output.iterdir()))
        self.assertEqual(0o750, stat.S_IMODE(self.output.stat().st_mode))
        prior_file = self.parent / 'prior'
        prior_file.write_bytes(b'prior bytes\x00\xff')
        prior_file.chmod(0o640)
        with self.assertRaises(FileExistsError):
            isolated.render(prior_file)
        self.assertEqual(b'prior bytes\x00\xff', prior_file.read_bytes())
        self.assertEqual(0o640, stat.S_IMODE(prior_file.stat().st_mode))

    def test_output_symlinks_including_dangling_refuse_without_target_writes(self):
        target = self.parent / 'target'
        target.mkdir(mode=0o700)
        (target / 'prior').write_bytes(b'protected')
        before = self.snapshot(target)
        self.output.symlink_to(target, target_is_directory=True)
        with self.assertRaises(FileExistsError):
            isolated.render(self.output)
        self.assertTrue(self.output.is_symlink())
        self.assertEqual(before, self.snapshot(target))
        dangling = self.parent / 'dangling'
        absent = self.parent / 'absent'
        dangling.symlink_to(absent, target_is_directory=True)
        with self.assertRaises(FileExistsError):
            isolated.render(dangling)
        self.assertTrue(dangling.is_symlink())
        self.assertFalse(absent.exists())

    def test_symlink_parent_and_ancestor_refuse_without_redirecting_writes(self):
        actual = self.parent / 'actual'
        actual.mkdir(mode=0o700)
        nested = actual / 'nested'
        nested.mkdir(mode=0o700)
        alias = self.parent / 'alias'
        alias.symlink_to(actual, target_is_directory=True)
        for output in (alias / 'bundle', alias / 'nested' / 'bundle'):
            with self.subTest(output=output), self.assertRaises(OSError):
                isolated.render(output)
        self.assertEqual([], list(nested.iterdir()))
        self.assertEqual([nested], list(actual.iterdir()))

    def test_nonprivate_parent_refuses_without_chmod_or_creation(self):
        self.parent.chmod(0o755)
        with self.assertRaisesRegex(ValueError, 'private-and-owned'):
            isolated.render(self.output)
        self.assertFalse(self.output.exists())
        self.assertEqual(0o755, stat.S_IMODE(self.parent.stat().st_mode))

    def test_wrong_owner_parent_refuses_without_creation(self):
        with patch.object(os, 'geteuid', return_value=self.parent.stat().st_uid + 1):
            with self.assertRaisesRegex(ValueError, 'private-and-owned'):
                isolated.render(self.output)
        self.assertFalse(self.output.exists())

    def test_missing_parent_and_relative_or_dot_paths_refuse(self):
        for output in ('relative', str(self.parent / 'missing' / 'bundle'),
                str(self.parent) + '/./bundle', str(self.parent) + '/nested/../bundle'):
            with self.subTest(output=output), self.assertRaises((OSError, ValueError)):
                isolated.render(output)
        self.assertEqual([], list(self.parent.iterdir()))

    def test_offline_render_never_opens_network_or_executes_any_process(self):
        def forbidden(*args, **kwargs):
            self.fail('render attempted network/process contact')
        with patch.object(socket, 'socket', side_effect=forbidden), \
                patch.object(subprocess, 'Popen', side_effect=forbidden), \
                patch.object(os, 'system', side_effect=forbidden), \
                patch.object(os, 'fork', side_effect=forbidden):
            receipt = isolated.render(self.output)
        self.assertFalse(receipt['hostContact'])
        self.assertTrue(receipt['complete'])

    def test_partial_write_failure_has_no_complete_receipt_or_prior_changes(self):
        protected = self.parent / 'protected'
        protected.write_bytes(b'prior')
        original = isolated._write
        writes = []

        def fail_second(parent_fd, name, content):
            writes.append(name)
            if len(writes) == 2:
                raise OSError('fictional-full-disk')
            return original(parent_fd, name, content)

        with patch.object(isolated, '_write', side_effect=fail_second):
            with self.assertRaisesRegex(OSError, 'full-disk'):
                isolated.render(self.output)
        self.assertTrue(self.output.is_dir())
        self.assertFalse((self.output / 'receipt.json').exists())
        self.assertEqual(b'prior', protected.read_bytes())
        with self.assertRaises(FileExistsError):
            isolated.render(self.output)

    def test_receipt_fsync_failure_never_publishes_complete_receipt(self):
        protected = self.parent / 'protected'
        protected.write_bytes(b'prior bytes\x00\xff')
        protected.chmod(0o640)
        original_fsync = os.fsync
        failed = []

        def fail_receipt_fsync(fd):
            pending = self.output / '.receipt.json.pending'
            if pending.exists() and os.fstat(fd).st_ino == pending.stat().st_ino:
                failed.append(True)
                raise OSError('fictional-receipt-fsync-failure')
            original_fsync(fd)

        with patch.object(os, 'fsync', side_effect=fail_receipt_fsync):
            with self.assertRaisesRegex(OSError, 'receipt-fsync-failure'):
                isolated.render(self.output)
        self.assertEqual([True], failed)
        self.assertFalse((self.output / 'receipt.json').exists())
        self.assertEqual(b'prior bytes\x00\xff', protected.read_bytes())
        self.assertEqual(0o640, stat.S_IMODE(protected.stat().st_mode))
        self.assertEqual(0o700, stat.S_IMODE(self.output.stat().st_mode))
        self.assertTrue((self.output / 'proposal.json').is_file())

    def test_cli_refusal_exit_status_stdout_and_preservation(self):
        isolated.render(self.output)
        invalid_parent = self.parent / 'invalid-parent'
        invalid_parent.mkdir(mode=0o755)
        invalid_parent.chmod(0o755)
        protected = invalid_parent / 'protected'
        protected.write_bytes(b'prior bytes\x00\xff')
        protected.chmod(0o640)
        before = self.snapshot(self.parent)
        for output in (self.output, invalid_parent / 'new'):
            with self.subTest(output=output):
                result = subprocess.run([sys.executable, '-B', str(Path(isolated.__file__).resolve()),
                    '--output', str(output)], capture_output=True, text=True, timeout=10)
                self.assertEqual(1, result.returncode, result.stderr)
                self.assertEqual('', result.stdout)
                self.assertIn('isolated-docker-preparation-refused:', result.stderr)
                self.assertEqual(before, self.snapshot(self.parent))
                self.assertEqual(0o755, stat.S_IMODE(invalid_parent.stat().st_mode))
                self.assertEqual(0o700, stat.S_IMODE(self.output.stat().st_mode))
        self.assertFalse((invalid_parent / 'new').exists())

    def test_generated_proposal_cannot_claim_runtime_authority_or_readiness(self):
        isolated.render(self.output)
        proposal = json.loads((self.output / 'proposal.json').read_bytes())
        self.assertEqual('offline-host-setup-proposal', proposal['purpose'])
        self.assertTrue(all(value is None for value in proposal['observations'].values()))
        self.assertIsNone(proposal['account']['uid'])
        self.assertIsNone(proposal['account']['subuid'])
        self.assertEqual('existing-accepted-unused', proposal['authority']['calibrationApproval'])
        self.assertEqual('consumed-do-not-repeat', proposal['authority']['hostCapture'])
        self.assertFalse(proposal['authority']['hostChangesAuthorized'])
        self.assertFalse(proposal['authority']['launchReady'])
        self.assertEqual(0, proposal['authority']['calibrationAttemptsConsumed'])
        self.assertFalse(proposal['authority']['qualificationCredit'])
        self.assertFalse(proposal['authority']['a41Closed'])
        self.assertTrue(proposal['paths']['socket'].startswith('unix:///run/user/@UID@/'))

    def test_entire_user_slice_caps_workload_plus_engine_without_reservation_claim(self):
        isolated.render(self.output)
        limits = json.loads((self.output / 'proposal.json').read_bytes())['ceilings']
        self.assertEqual((10250, 19 * isolated.GIB, 2000, 4 * isolated.GIB),
            (limits['workloadCpuMilli'], limits['workloadMemoryBytes'],
             limits['postgresCpuMilli'], limits['postgresMemoryBytes']))
        self.assertEqual(limits['workloadCpuMilli'] + limits['engineAllowanceCpuMilli'], limits['userSliceCpuMilli'])
        self.assertEqual(limits['workloadMemoryBytes'] + limits['engineAllowanceMemoryBytes'], limits['userSliceMemoryBytes'])
        config = configparser.ConfigParser(interpolation=None)
        config.read_string((self.output / 'system/user-@UID@.slice.d/50-calibration.conf.in').read_text())
        self.assertEqual('1225%', config['Slice']['CPUQuota'])
        self.assertEqual(str(23 * isolated.GIB), config['Slice']['MemoryMax'])
        self.assertEqual('0', config['Slice']['MemorySwapMax'])
        self.assertEqual(str(limits['userSliceTasks']), config['Slice']['TasksMax'])
        self.assertNotIn('containerPidsDefault', limits)
        self.assertEqual(24 * isolated.GIB, limits['additionalScratchBytes'])
        self.assertEqual(200, limits['scratchMinimumFreePermille'])
        for key in ('IOReadBandwidthMax', 'IOWriteBandwidthMax', 'IOReadIOPSMax', 'IOWriteIOPSMax'):
            self.assertIn('@IO_DEVICE@', config['Slice'][key])

    def test_daemon_configuration_requires_private_storage_network_and_bounded_logs(self):
        isolated.render(self.output)
        daemon = json.loads((self.output / 'config/daemon.json.in').read_bytes())
        self.assertEqual('overlay2', daemon['storage-driver'])
        self.assertFalse(daemon['features']['containerd-snapshotter'])
        self.assertEqual(['native.cgroupdriver=systemd'], daemon['exec-opts'])
        self.assertEqual('private', daemon['default-cgroupns-mode'])
        self.assertNotIn('default-pids-limit', daemon)
        self.assertEqual(isolated.BIN + '/docker-proxy', daemon['userland-proxy-path'])
        self.assertEqual({'max-size': '10m', 'max-file': '3'}, daemon['log-opts'])
        self.assertEqual('127.0.0.1', daemon['ip'])
        self.assertEqual('127.0.0.1', daemon['default-network-opts']['bridge']['com.docker.network.bridge.host_binding_ipv4'])
        self.assertIn('@', daemon['bip'])
        self.assertNotIn('hosts', daemon)
        self.assertNotIn('containerd', daemon)
        self.assertEqual({'auths': {}}, json.loads((self.output / 'config/docker-client.json').read_bytes()))

    def test_user_service_has_explicit_rootless_routes_no_restart_or_install_target(self):
        isolated.render(self.output)
        service = (self.output / ('user/' + isolated.UNIT + '.in')).read_text()
        start = next(line for line in service.splitlines() if line.startswith('ExecStart='))
        self.assertIn(isolated.BIN + '/dockerd-rootless.sh', start)
        self.assertIn('Environment="PATH=' + isolated.BIN + ':/usr/bin:/bin:/usr/sbin:/sbin"', service)
        self.assertIn('Environment="DOCKERD=' + isolated.BIN + '/dockerd"', service)
        unset = next(line for line in service.splitlines() if line.startswith('UnsetEnvironment=')).split('=', 1)[1].split()
        self.assertIn('_DOCKERD_ROOTLESS_CHILD', unset)
        self.assertIn('DOCKERD_ROOTLESS_ROOTLESSKIT_FLAGS', unset)
        self.assertNotIn('DOCKERD', unset)
        for flag in ('--host=unix://%t/hexalith-calibration/docker.sock',
                '--data-root=' + isolated.SCRATCH + '/docker',
                '--exec-root=%t/hexalith-calibration/exec', '--pidfile=%t/hexalith-calibration/dockerd.pid'):
            self.assertIn(flag, start)
        self.assertIn('DISABLE_HOST_LOOPBACK=true', service)
        self.assertIn('NET=slirp4netns', service)
        self.assertIn('DETACH_NETNS=false', service)
        self.assertIn('Restart=no', service)
        self.assertIn('Delegate=yes', service)
        self.assertIn('UMask=0077', service)
        self.assertIn('StandardOutput=null', service)
        self.assertIn('StandardError=null', service)
        self.assertNotIn('[Install]', service)
        self.assertNotIn('tcp://', service)
        self.assertNotIn('/var/run/docker.sock', service)
        self.assertNotIn('--containerd=', start)
        self.assertNotIn('NoNewPrivileges=', service)

    def test_bundle_recipe_is_exact_owner_recipe_and_not_an_installer(self):
        isolated.render(self.output)
        self.assertEqual(Path(isolated.__file__).with_name('ISOLATED-DOCKER.md').read_bytes(),
            (self.output / 'OPERATIONS.md').read_bytes())
        names = set(self.snapshot(self.output))
        self.assertEqual(8, len(names))
        self.assertFalse(any(name.endswith(('.sh', '.py')) for name in names))


if __name__ == '__main__':
    unittest.main()
