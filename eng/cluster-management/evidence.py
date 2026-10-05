"""Restricted immutable attempts and explicit projections; no side effects on import."""
from datetime import datetime, timezone
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess


def now():
    return datetime.now(timezone.utc).isoformat(timespec='seconds').replace('+00:00', 'Z')


def digest(data):
    return hashlib.sha256(data).hexdigest()


def canonical(record):
    return (json.dumps(record, indent=2, sort_keys=True) + '\n').encode()


def file_digest(path):
    return digest(Path(path).read_bytes())


def write_new(path, data):
    fd = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, 'wb') as stream:
        stream.write(data)


STANZA_TYPE = re.compile(r'[A-Za-z0-9][A-Za-z0-9_.+-]{0,63}')
SSH_TAG = re.compile(r'[A-Za-z0-9+/]{6}')


def recipient_stanzas(ciphertext):
    """Each age header stanza's type and, for SSH recipients, the public-key tag; never shares or key material."""
    header, terminator, _ = ciphertext.partition(b'\n---')
    stanzas = []
    for line in header.split(b'\n')[1:]:
        if not line.startswith(b'-> '):
            continue
        args = line[3:].decode('ascii', errors='replace').split(' ')
        ssh = args[0] in ('ssh-ed25519', 'ssh-rsa')
        if not STANZA_TYPE.fullmatch(args[0]) or (ssh and (len(args) < 2 or not SSH_TAG.fullmatch(args[1]))):
            raise ValueError('export-encryption-failed')
        # An X25519 argument is a per-file ephemeral share, not a recipient identity.
        stanzas.append({'type': args[0], 'tag': args[1] if ssh else None})
    if not terminator or not stanzas:
        raise ValueError('export-encryption-failed')
    return stanzas


def ssh_recipient_tag(recipient):
    parts = recipient.split()
    if len(parts) < 2 or parts[0] != 'ssh-ed25519':
        raise ValueError('readback-requires-ssh-ed25519-recipients')
    try:
        return base64.b64encode(hashlib.sha256(base64.b64decode(parts[1], validate=True)).digest()[:4]).decode().rstrip('=')
    except ValueError:
        raise ValueError('readback-requires-ssh-ed25519-recipients') from None


class Attempt:
    """Never reuse an attempt, follow a symlink, or write into recovery custody."""
    def __init__(self, project, root, attempt_id, category='qualification', *,
                 readback_recipient=None, readback_identity=None):
        self.project = Path(project).resolve()
        root = Path(root).absolute()
        if not re.fullmatch(r'[a-z0-9][a-z0-9-]{0,79}', attempt_id):
            raise ValueError('invalid-attempt-id')
        if category not in ('qualification', 'rehearsal', 'retirement'):
            raise ValueError('invalid-attempt-category')
        self.directory = root / category / attempt_id
        for part in [self.directory, *self.directory.parents]:
            if part.is_symlink():
                raise ValueError('symlink-in-evidence-path')
        resolved = self.directory.resolve()
        if resolved == self.project or self.project in resolved.parents:
            raise ValueError('private-evidence-must-be-outside-git')
        # Evidence from 4.0/4.1 is read-only even if a caller selects its path.
        recovery_roots = (Path.home() / 'hexalith-upgrade-evidence',
                          Path.home() / 'hexalith-recovery-evidence')
        if any(resolved == p.resolve() or p.resolve() in resolved.parents for p in recovery_roots):
            raise ValueError('must-not-write-into-existing-recovery-evidence')
        for part in [self.directory, *self.directory.parents]:
            if part == root.parent:
                break
            if part.exists() and (part.stat().st_uid != os.getuid()
                                  or stat.S_IMODE(part.stat().st_mode) & 0o077):
                raise ValueError('evidence-custody-must-be-owner-only')
        if bool(readback_recipient) != bool(readback_identity):
            raise ValueError('readback-recipient-and-identity-required-together')
        self.readback_recipient = readback_recipient
        self.readback_identity = Path(readback_identity) if readback_identity else None
        if self.readback_identity:
            identity = self.readback_identity.absolute()
            if (identity.is_symlink() or not identity.is_file()
                    or identity.stat().st_uid != os.getuid() or stat.S_IMODE(identity.stat().st_mode) & 0o077
                    or self.project in identity.resolve().parents or root.resolve() in identity.resolve().parents):
                raise ValueError('readback-identity-custody-invalid')
        old = os.umask(0o077)
        try:
            self.directory.mkdir(parents=True, mode=0o700, exist_ok=False)
        finally:
            os.umask(old)
        self.attempt_id = attempt_id
        self.category = category
        self.records = {}
        self.exports = []
        self.signatures = {}

    def record(self, name, record):
        if not re.fullmatch(r'[a-z0-9-]+\.json', name):
            raise ValueError('invalid-record-name')
        data = canonical(record)
        write_new(self.directory / name, data)
        self.records[name] = record
        return digest(data)

    def encrypt(self, name, plaintext, age, recipient):
        if not re.fullmatch(r'[a-z0-9-]+', name):
            raise ValueError('invalid-export-name')
        if not self.readback_recipient or not self.readback_identity:
            raise ValueError('export-readback-configuration-required')
        # Encryption runs before any export bytes reach the filesystem.
        if self.readback_recipient == recipient:
            raise ValueError('readback-recipient-must-be-distinct')
        argv = [str(age), '--encrypt', '--recipient', recipient, '--recipient', self.readback_recipient]
        result = subprocess.run(argv,
                                input=plaintext, capture_output=True, timeout=120)
        if result.returncode or not result.stdout.startswith(b'age-encryption.org/v1\n'):
            raise ValueError('export-encryption-failed')
        # Recorded so Git readers can check which recipient each export names.
        stanzas = recipient_stanzas(result.stdout)
        tags = {s['tag'] for s in stanzas if s['type'] == 'ssh-ed25519'}
        expected_tags = {ssh_recipient_tag(recipient), ssh_recipient_tag(self.readback_recipient)}
        if len(expected_tags) != 2 or not expected_tags <= tags:
            raise ValueError('export-second-recipient-missing')
        readback = subprocess.run([str(age), '--decrypt', '--identity', str(self.readback_identity)],
                                  input=result.stdout, capture_output=True, timeout=120)
        if readback.returncode or digest(readback.stdout) != digest(plaintext):
            raise ValueError('export-readback-failed')
        filename = name + '.age'
        write_new(self.directory / filename, result.stdout)
        entry = {'file': filename, 'plaintextSha256': digest(plaintext),
                 'ciphertextSha256': digest(result.stdout), 'ciphertextBytes': len(result.stdout),
                 'recipientStanzas': stanzas, 'readbackVerified': True}
        self.exports.append(entry)
        return entry

    def signed_record(self, name, record, ssh_keygen, signing_key, namespace='hexalith-retirement', *,
                      allowed_signers=None, principal=None, before_record=None):
        """Sign the exact sanitized receipt bytes; retain detached signatures with their records."""
        data = canonical(record)
        result = subprocess.run([str(ssh_keygen), '-Y', 'sign', '-n', namespace, '-f', str(signing_key)],
                                input=data, capture_output=True, timeout=30)
        if result.returncode or not result.stdout.startswith(b'-----BEGIN SSH SIGNATURE-----'):
            raise ValueError('receipt-signing-failed')
        if allowed_signers or principal:
            if not allowed_signers or not principal or not re.fullmatch(r'[a-z0-9-]+\.json', name):
                raise ValueError('receipt-verification-inputs-required')
            signature = self.directory / (name + '.sig')
            write_new(signature, result.stdout)
            verified = subprocess.run([str(ssh_keygen), '-Y', 'verify', '-f', str(allowed_signers),
                '-I', principal, '-n', namespace, '-s', str(signature)], input=data, capture_output=True, timeout=30)
            if verified.returncode:
                raise ValueError('receipt-signature-unverified')
            # Never persist an accepted outcome before its exact signature passes.
            if before_record:
                before_record()
            sha = self.record(name, record)
        else:
            sha = self.record(name, record)
            write_new(self.directory / (name + '.sig'), result.stdout)
        self.signatures[name] = result.stdout
        return sha

    def finish(self, publish=True):
        # Plaintext digests stay in private custody; only ciphertext identities are published.
        write_new(self.directory / 'private-export-digests.json', canonical({'schemaVersion': 1, 'attemptId': self.attempt_id,
                  'exports': self.exports, 'published': False}))
        self.record('encrypted-exports.json', {'schemaVersion': 1, 'attemptId': self.attempt_id,
                    'encryption': 'age', 'exports': [{k: e[k] for k in ('file', 'ciphertextSha256', 'ciphertextBytes',
                                                                        'recipientStanzas', 'readbackVerified')}
                                                     for e in self.exports],
                    'plaintextDigests': 'private-export-digests.json (private custody only)',
                    'readbackVerified': bool(self.exports) and all(e['readbackVerified'] for e in self.exports),
                    'readbackScope': 'agent-held second recipient; does not prove off-node custody',
                    'independentReadbackVerified': False, 'offNodeCustodyAccepted': False})
        sums = ''.join(f'{file_digest(p)}  {p.name}\n' for p in sorted(self.directory.iterdir()) if p.is_file())
        write_new(self.directory / 'SHA256SUMS', sums.encode())
        if not publish:
            return
        story = '4-27' if self.category == 'retirement' else '4-26'
        target = self.project / '_bmad-output/implementation-artifacts/evidence/epic-4' / story / self.attempt_id
        target.mkdir(parents=True, exist_ok=False)
        # Only records explicitly built from allowlisted fields are projected.
        for name, record in self.records.items():
            write_new(target / name, canonical(record))
            if name in self.signatures:
                write_new(target / (name + '.sig'), self.signatures[name])
        write_new(target / 'SHA256SUMS', ''.join(
            f'{file_digest(p)}  {p.name}\n' for p in sorted(target.iterdir())).encode())
