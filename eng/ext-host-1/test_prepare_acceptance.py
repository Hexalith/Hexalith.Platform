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
            packet = json.loads((output / "replacement-acceptance-packet.json").read_text())
            self.assertEqual(prepare.SCHEMA, packet["schema"])
            self.assertIsNone(packet["acceptedReplacementCommit"])
            self.assertFalse(packet["availability"])
            self.assertEqual(head, packet["candidate"])
            self.assertEqual(head, packet["candidateCommit"])
            self.assertFalse(packet["dependencyAvailable"])
            self.assertEqual(0, packet["effects"])
            self.assertIn(head, packet["exactCommand"])
            self.assertFalse(packet["liveCompatibilityAuthorized"])
            self.assertFalse(packet["productionMutationAllowed"])
            self.assertEqual(prepare.digest(packet["profile"]), packet["profileSha256"])
            self.assertEqual(prepare.digest(prepare.plan()), packet["planSha256"])
            for name in ("replay", "recorder", "worker", "spoolBackend", "replicaCount", "partiesBinding", "conversationsBinding"):
                self.assertIn(name, packet["missingReferences"])
            self.assertEqual(0, report["effects"])
            self.assertIsNone(report["acceptedReplacementCommit"])

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


if __name__ == "__main__":
    unittest.main()
