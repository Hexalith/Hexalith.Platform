"""Private attempt destinations cannot be redirected into forbidden custody."""
import base64
import json
from pathlib import Path
import shlex
import stat
import tempfile
import unittest
from unittest.mock import patch

from evidence import Attempt, digest, file_digest, ssh_recipient_tag


def fake_age(directory, body, plaintext='PRIVATE'):
    path = directory / 'fake-age'
    path.write_text('#!/bin/sh\ncat >/dev/null\n'
                    'if [ "$1" = "--decrypt" ]; then printf %s ' + shlex.quote(plaintext) + '; exit 0; fi\n' + body)
    path.chmod(0o700)
    return path


ADMINISTRATOR = 'ssh-ed25519 ' + base64.b64encode(b'administrator-public-test-blob').decode()
READBACK = 'ssh-ed25519 ' + base64.b64encode(b'readback-public-test-blob').decode()
SSH_STANZAS = [{'type': 'ssh-ed25519', 'tag': ssh_recipient_tag(value)} for value in (ADMINISTRATOR, READBACK)]
SSH_HEADER = 'age-encryption.org/v1\n' + ''.join(
    '-> ssh-ed25519 ' + value['tag'] + ' ssh-ephemeral-share\nwrapped-key\n' for value in SSH_STANZAS)
SSH = 'printf %s ' + shlex.quote(SSH_HEADER + '--- mac\nciphertext') + '\n'
VALID = 'printf %s ' + shlex.quote(SSH_HEADER + '-> X25519 ephemeral-share\nwrapped-key\n--- mac\nciphertext') + '\n'


class DestinationTests(unittest.TestCase):
    def test_category_symlink_cannot_write_into_git_or_recovery(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            project = base / 'repo'
            project.mkdir(mode=0o700)
            fake_home = base / 'home'
            fake_home.mkdir(mode=0o700)
            recovery = fake_home / 'hexalith-recovery-evidence'
            recovery.mkdir(mode=0o700)
            for category, target in [('qualification', project), ('rehearsal', recovery)]:
                with self.subTest(category=category):
                    root = base / category
                    root.mkdir(mode=0o700)
                    (root / category).symlink_to(target)
                    with patch('evidence.Path.home', return_value=fake_home):
                        with self.assertRaisesRegex(ValueError, 'symlink'):
                            Attempt(project, root, 'attempt', category)
                    self.assertFalse((target / 'attempt').exists())

    def test_attempt_symlink_is_refused_before_touching_existing_custody(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            project = base / 'repo'
            project.mkdir(mode=0o700)
            root = base / 'private'
            category = root / 'qualification'
            category.mkdir(parents=True, mode=0o700)
            root.chmod(0o700)
            retained = base / 'retained'
            retained.mkdir(mode=0o700)
            marker = retained / 'marker'
            marker.write_text('original')
            (category / 'attempt').symlink_to(retained)
            with self.assertRaisesRegex(ValueError, 'symlink'):
                Attempt(project, root, 'attempt')
            self.assertEqual(marker.read_text(), 'original')
            self.assertEqual(list(retained.iterdir()), [marker])

    def test_dangling_category_symlink_is_refused(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            project = base / 'repo'
            project.mkdir(mode=0o700)
            root = base / 'private'
            root.mkdir(mode=0o700)
            destination = base / 'missing'
            (root / 'qualification').symlink_to(destination)
            with self.assertRaisesRegex(ValueError, 'symlink'):
                Attempt(project, root, 'attempt')
            self.assertFalse(destination.exists())



class AttemptPublicationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.base = Path(self.temp.name)
        self.project = self.base / 'repo'
        self.project.mkdir(mode=0o700)
        home = self.base / 'home'
        home.mkdir(mode=0o700)
        self.home = patch('evidence.Path.home', return_value=home)
        self.home.start()
        self.identity = self.base / 'readback-test-identity'
        self.identity.write_bytes(b'synthetic private test identity')
        self.identity.chmod(0o600)

    def configured_attempt(self, attempt_id):
        return Attempt(self.project, self.base / 'private', attempt_id,
                       readback_recipient=READBACK, readback_identity=self.identity)

    def tearDown(self):
        self.home.stop()
        self.temp.cleanup()

    def test_finish_publishes_exactly_records_and_matching_sums_without_plaintext_digests(self):
        age = fake_age(self.base, VALID, 'PRIVATE-PLAINTEXT')
        attempt = self.configured_attempt('publish-test')
        attempt.record('observation.json', {'accepted': False})
        attempt.encrypt('raw-export', b'PRIVATE-PLAINTEXT', age, ADMINISTRATOR)
        (attempt.directory / 'Dockerfile').write_text('fixture-only file')
        attempt.finish()
        published = self.project / '_bmad-output/implementation-artifacts/evidence/epic-4/4-26/publish-test'
        names = sorted(p.name for p in published.iterdir())
        self.assertEqual(names, ['SHA256SUMS', 'encrypted-exports.json', 'observation.json'])
        self.assertFalse(any(name.endswith('.age') or name in ('Dockerfile', 'private-export-digests.json') for name in names))
        sums = dict(reversed(line.split('  ')) for line in (published / 'SHA256SUMS').read_text().splitlines())
        self.assertEqual(set(sums), {'encrypted-exports.json', 'observation.json'})
        self.assertTrue(all(file_digest(published / name) == value for name, value in sums.items()))
        exports = json.loads((published / 'encrypted-exports.json').read_text())
        self.assertEqual(set(exports['exports'][0]), {'file', 'ciphertextSha256', 'ciphertextBytes', 'recipientStanzas', 'readbackVerified'})
        self.assertNotIn('plaintextSha256', json.dumps(exports))
        self.assertNotIn(digest(b'PRIVATE-PLAINTEXT'), ''.join(p.read_text() for p in published.iterdir()))
        private = json.loads((attempt.directory / 'private-export-digests.json').read_text())
        self.assertEqual(private['exports'][0]['plaintextSha256'], digest(b'PRIVATE-PLAINTEXT'))
        private_sums = (attempt.directory / 'SHA256SUMS').read_text()
        for name in ('raw-export.age', 'Dockerfile', 'private-export-digests.json'):
            self.assertIn(name, private_sums)

    def test_encrypt_writes_valid_output_and_refuses_failed_or_headerless_output(self):
        attempt = self.configured_attempt('encrypt-test')
        entry = attempt.encrypt('valid-export', b'PRIVATE', fake_age(self.base, VALID), ADMINISTRATOR)
        written = attempt.directory / 'valid-export.age'
        self.assertTrue(written.read_bytes().startswith(b'age-encryption.org/v1\n'))
        self.assertEqual(stat.S_IMODE(written.stat().st_mode), 0o600)
        self.assertEqual(attempt.exports, [entry])
        self.assertEqual(entry['ciphertextSha256'], file_digest(written))
        for name, body in (('failed', 'printf "age-encryption.org/v1\\npartial"\nexit 1\n'),
                           ('headerless', 'printf "PRIVATE"\n')):
            with self.subTest(case=name):
                age = self.base / ('age-' + name)
                age.write_text('#!/bin/sh\ncat >/dev/null\n' + body);age.chmod(0o700)
                with self.assertRaisesRegex(ValueError, 'export-encryption-failed'):
                    attempt.encrypt(name + '-export', b'PRIVATE', age, ADMINISTRATOR)
                self.assertFalse((attempt.directory / (name + '-export.age')).exists())
                self.assertEqual(attempt.exports, [entry])

    def test_recipient_stanza_type_and_ssh_tag_are_recorded_and_published_without_shares(self):
        attempt = self.configured_attempt('recipient-test')
        ssh = attempt.encrypt('ssh-export', b'PRIVATE', fake_age(self.base, SSH), ADMINISTRATOR)
        self.assertEqual(ssh['recipientStanzas'], SSH_STANZAS)
        native = attempt.encrypt('x25519-export', b'PRIVATE', fake_age(self.base, VALID), ADMINISTRATOR)
        self.assertEqual(native['recipientStanzas'], SSH_STANZAS + [{'type': 'X25519', 'tag': None}])
        attempt.finish()
        published = (self.project / '_bmad-output/implementation-artifacts/evidence/epic-4/4-26/recipient-test'
                     / 'encrypted-exports.json').read_text()
        self.assertEqual([e['recipientStanzas'] for e in json.loads(published)['exports']],
                         [SSH_STANZAS, SSH_STANZAS + [{'type': 'X25519', 'tag': None}]])
        # Ephemeral shares and wrapped keys never leave the ciphertext.
        for value in ('ephemeral-share', 'wrapped-key'):
            self.assertNotIn(value, published)

    def test_headers_without_recipient_stanza_terminator_or_valid_ssh_tag_are_refused(self):
        attempt = self.configured_attempt('stanza-test')
        for name, body in (('no-stanza', 'printf "age-encryption.org/v1\\n--- mac\\nciphertext"\n'),
                           ('no-terminator', 'printf "age-encryption.org/v1\\n-> X25519 share\\nciphertext"\n'),
                           ('no-tag', 'printf "age-encryption.org/v1\\n-> ssh-ed25519\\nwrapped\\n--- mac\\nx"\n'),
                           ('bad-tag', 'printf "age-encryption.org/v1\\n-> ssh-rsa not-a-tag!\\nwrapped\\n--- mac\\nx"\n')):
            with self.subTest(case=name):
                age = self.base / ('age-' + name)
                age.write_text('#!/bin/sh\ncat >/dev/null\n' + body);age.chmod(0o700)
                with self.assertRaisesRegex(ValueError, 'export-encryption-failed'):
                    attempt.encrypt(name, b'PRIVATE', age, ADMINISTRATOR)
                self.assertFalse((attempt.directory / (name + '.age')).exists())
        self.assertEqual(attempt.exports, [])

    def test_direct_library_export_requires_readback_but_metadata_only_attempt_is_allowed(self):
        attempt = Attempt(self.project, self.base / 'private', 'metadata-only-test')
        with patch('evidence.subprocess.run') as run:
            with self.assertRaisesRegex(ValueError, 'export-readback-configuration-required'):
                attempt.encrypt('raw', b'PRIVATE', 'age', ADMINISTRATOR)
            run.assert_not_called()
        self.assertFalse((attempt.directory / 'raw.age').exists())
        self.assertEqual(attempt.exports, [])
        attempt.record('observation.json', {'metadataOnly': True})
        attempt.finish()
        self.assertEqual(json.loads((attempt.directory / 'encrypted-exports.json').read_text())['exports'], [])

    def test_existing_upgrade_and_recovery_evidence_roots_are_refused(self):
        home = self.base / 'home'  # Path.home() is patched to this directory in setUp
        for name in ('hexalith-upgrade-evidence', 'hexalith-recovery-evidence'):
            with self.subTest(root=name):
                retained = home / name
                retained.mkdir(mode=0o700)
                with self.assertRaisesRegex(ValueError, 'must-not-write-into-existing-recovery-evidence'):
                    Attempt(self.project, retained / 'new-root', 'attempt')
                self.assertEqual(list(retained.iterdir()), [])

    def test_invalid_attempt_ids_and_categories_are_refused_before_allocation(self):
        root = self.base / 'private'
        for bad in ('', 'Upper', '../escape', 'nested/attempt', '-leading', 'x' * 81, 'space id'):
            with self.subTest(attempt_id=bad), self.assertRaisesRegex(ValueError, 'invalid-attempt-id'):
                Attempt(self.project, root, bad)
        with self.assertRaisesRegex(ValueError, 'invalid-attempt-category'):
            Attempt(self.project, root, 'valid-id', 'recovery')
        self.assertFalse(root.exists())

    def test_existing_root_or_category_that_is_not_owner_only_is_refused(self):
        for case in ('root', 'category'):
            with self.subTest(case=case):
                root = self.base / ('private-' + case)
                category = root / 'qualification'
                category.mkdir(parents=True, mode=0o700)
                root.chmod(0o755 if case == 'root' else 0o700)
                category.chmod(0o750 if case == 'category' else 0o700)
                with self.assertRaisesRegex(ValueError, 'owner-only'):
                    Attempt(self.project, root, 'attempt')
                self.assertFalse((category / 'attempt').exists())

if __name__ == '__main__':
    unittest.main()
