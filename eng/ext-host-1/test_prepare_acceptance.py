import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path

import prepare_acceptance as prepare


def commit(root):
    subprocess.run(["git", "-C", str(root), "init", "-q"], check=True)
    env = os.environ | {
        "GIT_AUTHOR_NAME": "candidate", "GIT_AUTHOR_EMAIL": "candidate@example.com",
        "GIT_COMMITTER_NAME": "candidate", "GIT_COMMITTER_EMAIL": "candidate@example.com",
    }
    subprocess.run(["git", "-C", str(root), "commit", "--allow-empty", "-q", "-m", "candidate"], check=True, env=env)
    return subprocess.run(["git", "-C", str(root), "rev-parse", "HEAD"], check=True, capture_output=True, text=True).stdout.strip()


class PrepareAcceptanceTests(unittest.TestCase):
    def test_detached_packet_names_empty_slots_and_accepts_nothing(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "candidate"
            root.mkdir()
            head = commit(root)
            output = Path(temp) / "packet"
            report = prepare.prepare(root, output)
            self.assertFalse(report["packetPrepared"])
            self.assertFalse(report["completeReplacementPacket"])
            self.assertIsNone(report["targetCommit"])
            self.assertIn("ExplicitCoordinatedInstallationContext", report["missingReferences"])
            for name in ("replay", "recorder", "worker", "spoolBackend", "replicaCount", "partiesBinding", "conversationsBinding"):
                self.assertIn(name, report["missingReferences"])
            self.assertFalse((output / "replacement-acceptance-packet.json").exists())
            self.assertEqual(0, report["effects"])

    def test_pending_is_missing_and_secrets_are_case_insensitive(self):
        profile = prepare.default_profile()
        profile["partiesBinding"] = "PeNdInG"
        profile["conversationsBinding"] = "PENDING"
        missing = prepare.missing_references(profile)
        self.assertIn("partiesBinding", missing)
        self.assertIn("conversationsBinding", missing)
        profile["replay"] = "OPENBAO/SECRET/OPENBAO-SEAL"
        with self.assertRaises(ValueError) as captured:
            prepare.missing_references(profile)
        self.assertNotIn("OPENBAO/SECRET/OPENBAO-SEAL", str(captured.exception))


class ProfileSnapshotTests(unittest.TestCase):
    def test_actual_prepare_acquires_profile_once_before_checkout_guard(self):
        from unittest.mock import patch
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp)/'candidate';root.mkdir();commit(root)
            profile=Path(temp)/'profile';raw=prepare.canonical(prepare.default_profile());profile.write_bytes(raw)
            real=prepare.profile_snapshot;reads=[]
            def snapshot(path):
                reads.append(path)
                value=real(path)
                profile.write_text('{"substituted":true}')
                return value
            with patch.object(prepare,'profile_snapshot',side_effect=snapshot):
                report=prepare.prepare(root,Path(temp)/'output',str(profile))
            self.assertEqual([str(profile)],reads);self.assertIn('replay',report['missingReferences'])
            for kind in ('fifo','link','oversize','duplicate'):
                profile.unlink()
                if kind=='fifo':os.mkfifo(profile)
                elif kind=='link':profile.symlink_to(root)
                elif kind=='oversize':profile.write_bytes(b'x'*(1024*1024+1))
                else:profile.write_bytes(raw.replace(b'"host":',b'"host":"192.168.1.30","host":'))
                with self.assertRaises((ValueError,OSError)):
                    prepare.prepare(root,Path(temp)/('output-'+kind),str(profile))


if __name__ == "__main__":
    unittest.main()
