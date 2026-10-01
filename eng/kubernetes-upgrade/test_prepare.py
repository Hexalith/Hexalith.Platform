"""Local safety checks for the Story 4.1 diagnostic recorder."""

import importlib.util
import json
import os
from pathlib import Path
import stat
import tempfile
import unittest
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location("upgrade_prepare", Path(__file__).with_name("prepare.py"))
prepare = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(prepare)


class PreparationTests(unittest.TestCase):
    def setUp(self):
        self.scratch = tempfile.TemporaryDirectory()
        self.addCleanup(self.scratch.cleanup)
        self.root = Path(self.scratch.name)
        self.project = self.root / "project"
        self.project.mkdir()
        for name in (
            prepare.ARTIFACTS / f"{prepare.STORY_41}.md",
            prepare.ARTIFACTS / "epic-4-context.md", prepare.BASELINE,
            prepare.ARTIFACTS / f"{prepare.STORY_40}.md",
            Path("eng/kubernetes-upgrade/prepare.py"), Path("eng/kubernetes-upgrade/README.md"),
        ):
            target = self.project / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text("sanitized fixture\n")
        self.status = self.project / prepare.ARTIFACTS / "sprint-status.yaml"
        self.status.write_text(f"development_status:\n  {prepare.STORY_40}: in-progress\n")
        self.evidence = self.root / "evidence"

    def run_preparation(self, bundle=None):
        return prepare.prepare(self.project, self.evidence, "test-attempt", "test operator", bundle)

    def test_no_operational_or_network_call_and_closed_gate(self):
        with patch("subprocess.run", side_effect=AssertionError("external command")), \
             patch("socket.socket", side_effect=AssertionError("network")), \
             patch("os.system", side_effect=AssertionError("shell")):
            directory = self.run_preparation()
        gate = json.loads((directory / "gate-validation.json").read_text())
        self.assertEqual(gate["gateState"], "closed")
        self.assertEqual(gate["checks"][0]["result"], "fail")
        self.assertFalse(gate["mutationAuthorized"])
        self.assertFalse(gate["liveEvidenceCollected"])
        result = json.loads((directory / "upgrade-result.json").read_text())
        self.assertFalse(result["complete"])
        self.assertEqual(result["executedMinorHops"], [])
        self.assertTrue(all(value == "not-run" for value in result["workloadOutcomes"].values()))

    def test_done_and_claimed_passing_files_cannot_authorize_mutation(self):
        self.status.write_text(f"development_status:\n  {prepare.STORY_40}: done\n")
        bundle = self.root / "bundle"
        for name in (*prepare.PROOF_FILES, prepare.VALIDATION_FILE):
            path = bundle / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(json.dumps({"verificationResult": "pass", "expiresAt": "2999-01-01T00:00:00Z"}))
            Path(str(path) + ".sig").write_text("not a valid signature")
        directory = self.run_preparation(bundle)
        gate = json.loads((directory / "gate-validation.json").read_text())
        self.assertEqual(gate["checks"][0]["result"], "pass")
        self.assertEqual(gate["gateState"], "closed")
        proof_check = gate["checks"][1]
        self.assertEqual(proof_check["result"], "not-validated")
        self.assertTrue(all(item["signatureValidation"] == "not-performed"
                            for item in proof_check["localInputInventory"]))
        validation_check = gate["checks"][2]
        self.assertEqual(validation_check["localInputInventory"]["relativePath"], prepare.VALIDATION_FILE)
        hop = json.loads((directory / "hop-gate-1.34-to-1.35.json").read_text())
        self.assertEqual(hop["prerequisites"][validation_check["condition"]], "not-validated")

    def test_malformed_and_expired_inputs_do_not_expose_values(self):
        bundle = self.root / "bundle"
        path = bundle / "gate/backup-gate.json"
        path.parent.mkdir(parents=True)
        path.write_text('{"secret":"DO-NOT-RETAIN", broken}')
        path = bundle / "keycloak/keycloak-restore-proof.json"
        path.parent.mkdir(parents=True)
        path.write_text(json.dumps({"verificationResult": "pass", "expiresAt": "2000-01-01T00:00:00Z",
                                    "privateData": "DO-NOT-RETAIN"}))
        directory = self.run_preparation(bundle)
        gate_text = (directory / "gate-validation.json").read_text()
        self.assertNotIn("DO-NOT-RETAIN", gate_text)
        proofs = json.loads(gate_text)["checks"][1]["localInputInventory"]
        self.assertEqual(proofs[0]["state"], "malformed")
        self.assertFalse(proofs[1]["unexpiredTimestamp"])
        self.assertFalse(proofs[1]["signaturePresent"])

    def test_private_modes_and_checksums_under_permissive_umask(self):
        old = os.umask(0)
        try:
            directory = self.run_preparation()
        finally:
            os.umask(old)
        for path in [directory, *directory.parents]:
            if path == self.root:
                break
            self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o700)
        for path in directory.iterdir():
            self.assertEqual(stat.S_IMODE(path.stat().st_mode), 0o600)
        for line in (directory / "SHA256SUMS").read_text().splitlines():
            digest, name = line.split("  ")
            self.assertEqual(prepare.sha256(directory / name), digest)

    def test_refuse_reuse_unsafe_id_and_repository_output(self):
        directory = self.run_preparation()
        hashes = {path.name: prepare.sha256(path) for path in directory.iterdir()}
        with self.assertRaises(ValueError):
            self.run_preparation()
        self.assertEqual(hashes, {path.name: prepare.sha256(path) for path in directory.iterdir()})
        with self.assertRaises(ValueError):
            prepare.prepare(self.project, self.evidence, "../escape", "test")
        with self.assertRaises(ValueError):
            prepare.prepare(self.project, self.project / "evidence", "safe", "test")

    def test_duplicate_story_status_is_unresolved(self):
        self.status.write_text(f"development_status:\n  {prepare.STORY_40}: done\n  {prepare.STORY_40}: done\n")
        self.assertEqual(prepare.story_status(self.status), "unresolved")

    def test_refuse_shared_evidence_directory(self):
        self.evidence.mkdir(mode=0o755)
        self.evidence.chmod(0o755)
        with self.assertRaises(ValueError):
            self.run_preparation()

    def test_recovery_bundle_is_read_only_and_cannot_be_output(self):
        bundle = self.root / "bundle"
        bundle.mkdir()
        source = bundle / "unchanged.json"
        source.write_text('{"mustRemain":"unchanged"}')
        before = {path.relative_to(bundle): prepare.sha256(path) for path in bundle.rglob("*") if path.is_file()}
        for output in (bundle, bundle / "descendant"):
            with self.assertRaises(ValueError):
                prepare.prepare(self.project, output, "test", "test", bundle)
        self.run_preparation(bundle)
        after = {path.relative_to(bundle): prepare.sha256(path) for path in bundle.rglob("*") if path.is_file()}
        self.assertEqual(before, after)


if __name__ == "__main__":
    unittest.main()
