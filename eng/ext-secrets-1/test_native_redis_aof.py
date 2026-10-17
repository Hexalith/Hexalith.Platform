"""Offline Redis 7.4 transaction/AOF regression; never connects to the installed registry."""
import os
from pathlib import Path
import secrets
import subprocess
from tempfile import TemporaryDirectory
import time
import unittest

from registry_redis import INTERNAL_REPLAY_ACL, native_acl


class NativeRedisAofTests(unittest.TestCase):
    def test_shipped_acl_replays_original_transactions_and_denies_default_login(self):
        image = 'redis/redis-stack-server@sha256:798ab84d9f266936b034ab11c4d04a2b8e4b441884c5aa7d17ac951eefdf742a'
        name = 'ext-secrets-1-offline-aof-' + secrets.token_hex(6)
        with TemporaryDirectory() as directory:
            path = Path(directory)
            (path / 'users.acl').write_text(INTERNAL_REPLAY_ACL + '\n' + native_acl('eventstore', 'offline-app-password') + '\n'
                + native_acl('tenants', 'offline-tenant-password') + '\n'
                + 'user fixture-maintenance on >offline-maintenance-password resetkeys resetchannels -@all +ping +acl|dryrun\n')
            def docker(*args, data=None):
                return subprocess.run(['docker', *args], input=data, capture_output=True, check=True, timeout=30).stdout.decode()
            def cli(user, password, commands):
                script = 'IFS= read -r REDISCLI_AUTH || exit 65; export REDISCLI_AUTH; exec redis-cli --no-auth-warning --raw --user "$1"'
                return docker('exec', '-i', name, 'sh', '-c', script, 'sh', user, data=(password + '\n' + commands).encode())
            def ready():
                for _ in range(60):
                    try:
                        if 'PONG' in cli('fixture-maintenance', 'offline-maintenance-password', 'PING\n'): return
                    except subprocess.CalledProcessError: pass
                    time.sleep(0.1)
                self.fail('offline Redis did not start')
            try:
                docker('run', '-d', '--name', name, '--network=none', '--user', str(os.getuid()) + ':' + str(os.getgid()),
                       '-v', directory + ':/data', '--entrypoint=redis-server', image, '--dir', '/data', '--aclfile', '/data/users.acl', '--appendonly', 'yes', '--appendfsync', 'always')
                ready()
                response = cli('eventstore', 'offline-app-password', 'MULTI\nHSET eventstore||offline-aof-one value offline-one\nHSET eventstore||offline-aof-two value offline-two\nHINCRBY eventstore||offline-aof-one etag 1\nEXEC\n')
                self.assertNotIn('NOPERM', response)
                docker('stop', '--time=2', name)
                self.assertTrue(list((path / 'appendonlydir').glob('*.aof')))
                docker('start', name); ready()
                self.assertEqual('offline-one\noffline-two\n', cli('eventstore', 'offline-app-password', 'HGET eventstore||offline-aof-one value\nHGET eventstore||offline-aof-two value\n'))
                self.assertEqual('1\n', cli('eventstore', 'offline-app-password', 'HGET eventstore||offline-aof-one etag\n'))
                self.assertNotIn('AOF-loading-client', docker('logs', name))
                denied = cli('default', 'offline-invalid-default-password', 'PING\n')
                self.assertIn('NOAUTH', denied)
                checks = cli('fixture-maintenance', 'offline-maintenance-password', 'ACL DRYRUN eventstore HGET tenants||foreign value\nACL DRYRUN tenants HGET eventstore||foreign value\nACL DRYRUN eventstore PUBLISH foreign-channel value\n')
                self.assertNotIn('OK', checks)
            finally:
                subprocess.run(['docker', 'rm', '-f', name], capture_output=True, check=False)


if __name__ == '__main__': unittest.main()
