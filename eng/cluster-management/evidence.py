"""Restricted immutable attempts and explicit projections; no side effects on import."""
from datetime import datetime, timezone
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


class Attempt:
    """Never reuse an attempt, follow a symlink, or write into recovery custody."""
    def __init__(self, project, root, attempt_id, category='qualification'):
        self.project = Path(project).resolve()
        root = Path(root).absolute()
        if not re.fullmatch(r'[a-z0-9][a-z0-9-]{0,79}', attempt_id):
            raise ValueError('invalid-attempt-id')
        if category not in ('qualification', 'rehearsal'):
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
        old = os.umask(0o077)
        try:
            self.directory.mkdir(parents=True, mode=0o700, exist_ok=False)
        finally:
            os.umask(old)
        self.attempt_id = attempt_id
        self.records = {}
        self.exports = []

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
        # Encryption runs before any export bytes reach the filesystem.
        result = subprocess.run([str(age), '--encrypt', '--recipient', recipient],
                                input=plaintext, capture_output=True, timeout=120)
        if result.returncode or not result.stdout.startswith(b'age-encryption.org/v1\n'):
            raise ValueError('export-encryption-failed')
        filename = name + '.age'
        write_new(self.directory / filename, result.stdout)
        entry = {'file': filename, 'plaintextSha256': digest(plaintext),
                 'ciphertextSha256': digest(result.stdout), 'ciphertextBytes': len(result.stdout)}
        self.exports.append(entry)
        return entry

    def finish(self, publish=True):
        # Plaintext digests stay in private custody; only ciphertext identities are published.
        write_new(self.directory / 'private-export-digests.json', canonical({'schemaVersion': 1, 'attemptId': self.attempt_id,
                  'exports': self.exports, 'published': False}))
        self.record('encrypted-exports.json', {'schemaVersion': 1, 'attemptId': self.attempt_id,
                    'encryption': 'age', 'exports': [{k: e[k] for k in ('file', 'ciphertextSha256', 'ciphertextBytes')}
                                                     for e in self.exports],
                    'plaintextDigests': 'private-export-digests.json (private custody only)',
                    'independentReadbackVerified': False, 'offNodeCustodyAccepted': False})
        sums = ''.join(f'{file_digest(p)}  {p.name}\n' for p in sorted(self.directory.iterdir()) if p.is_file())
        write_new(self.directory / 'SHA256SUMS', sums.encode())
        if not publish:
            return
        target = self.project / '_bmad-output/implementation-artifacts/evidence/epic-4/4-26' / self.attempt_id
        target.mkdir(parents=True, exist_ok=False)
        # Only records explicitly built from allowlisted fields are projected.
        for name, record in self.records.items():
            write_new(target / name, canonical(record))
        write_new(target / 'SHA256SUMS', ''.join(
            f'{file_digest(p)}  {p.name}\n' for p in sorted(target.iterdir())).encode())
