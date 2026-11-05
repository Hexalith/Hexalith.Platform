"""Real private Git histories for bounded, truthful legacy/current source orchestration."""
import copy
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

import legacy_tuple_source as legacy


class LegacyTupleSourceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.git("init", "-q")
        self.git("config", "user.name", "Legacy qualification fixture")
        self.git("config", "user.email", "fixture@invalid")
        self.profile = (Path(__file__).parent / "tuple-ci-legacy-suites.json").read_bytes()
        for name in ("src/Owner.cs", "tests/Control.cs", "DaprSelfHostedMtls.cs", "Directory.Build.props", ".github/workflows/tuple-ci.yml"):
            self.write(name, b"unchanged committed source\n")
        self.write("eng/tuple-ci-suites.json", self.profile)
        self.write(".config/dotnet-tools.json", json.dumps({"tools": {"hexalith.builds.module.cli": {"version": "4.44.3", "rollForward": False}}}).encode())
        self.descriptor = dict(schema="hexalith.platform.legacy-tuple-source.v1", purpose="same-code-legacy-tuple-regression", sourceRevision=None,
            buildsSource=legacy.BUILDS, toolVersion="4.44.3", profileSha256=legacy.PROFILE_HASH, requiredCount=349, qualificationCredit=False)
        self.write_descriptor()
        (self.root / "references/Hexalith.Builds").mkdir(parents=True)
        self.git("update-index", "--add", "--cacheinfo", "160000," + legacy.BUILDS + ",references/Hexalith.Builds")
        self.old = self.commit()
        self.descriptor["sourceRevision"] = self.old
        self.write_descriptor()
        self.write("eng/tuple-ci-suites.json", b'{"groups": [{"current": true}]}\n')
        self.current = self.commit()

    def git(self, *arguments):
        return subprocess.run(["git", *arguments], cwd=self.root, capture_output=True, check=True).stdout.decode().strip()

    def write(self, name, body):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(body)

    def write_descriptor(self):
        self.write(legacy.DESCRIPTOR, json.dumps(self.descriptor).encode())

    def commit(self):
        self.git("add", ".")
        self.git("commit", "-q", "-m", "test(legacy): record private source fixture")
        return self.git("rev-parse", "HEAD")

    def test_workflow_uses_actual_automatic_source_and_committed_legacy_identity(self):
        workflow = (Path(__file__).parents[1] / ".github/workflows/tuple-ci.yml").read_text()
        self.assertIn("  push:\n    branches: [main]", workflow)
        self.assertIn("PLATFORM_CANDIDATE_SHA: ${{ inputs.candidate-sha || github.sha }}", workflow)
        self.assertIn("ref: ${{ inputs.candidate-sha || github.sha }}", workflow)
        self.assertIn("python3 -B eng/legacy_tuple_source.py --current", workflow)
        self.assertIn("  legacy-public-cohort:\n    needs: tuple", workflow)
        self.assertIn("ref: ${{ needs.tuple.outputs.legacy-source }}", workflow)
        self.assertIn("--requested \"$PLATFORM_CANDIDATE_SHA\" --verify", workflow)
        self.assertNotIn("GITHUB_SHA:", workflow)
        self.assertNotIn("continue-on-error:", workflow)
        self.assertIn("Run all original 349 controls", workflow)

    def test_duplicate_descriptor_json_fields_are_refused(self):
        with self.assertRaises(ValueError): legacy.decode(b'{"sourceRevision":null,"sourceRevision":"hidden"}')

    def test_same_changed_code_and_explicit_metadata_difference_are_honest(self):
        self.assertEqual(legacy.resolve(self.root, self.current), self.old)
        self.git("checkout", "-q", self.old)
        before = self.git("status", "--porcelain")
        result = legacy.verify(self.root, self.current, self.old)
        self.assertEqual(result["sourceEquivalence"], "verified")
        self.assertEqual(result["requiredCount"], 349)
        self.assertFalse(result["qualificationCredit"])
        self.assertFalse(result["binaryEquivalenceClaimed"])
        self.assertEqual(result["legacySource"], self.old)
        self.assertEqual(result["currentSource"], self.current)
        self.assertEqual(self.git("rev-parse", "HEAD"), self.old)
        self.assertEqual(self.git("status", "--porcelain"), before)
        self.assertFalse((self.root / "references/Hexalith.Builds/.git").exists())

    def test_current_checkout_cannot_claim_the_legacy_source(self):
        with self.assertRaises(ValueError): legacy.verify(self.root, self.current, self.old)
        self.assertEqual(self.git("rev-parse", "HEAD"), self.current)

    def test_production_helpers_tests_build_instructions_and_workflow_changes_refuse(self):
        for name in ("src/Owner.cs", "tests/Control.cs", "DaprSelfHostedMtls.cs", "Directory.Build.props", ".github/workflows/tuple-ci.yml"):
            with self.subTest(name=name):
                self.git("checkout", "-q", self.current)
                self.write(name, b"different source\n")
                changed = self.commit()
                self.git("checkout", "-q", self.old)
                with self.assertRaises(ValueError): legacy.verify(self.root, changed, self.old)

    def test_pending_wrong_dispatch_or_expanded_descriptor_grants_no_authority(self):
        with self.assertRaises(ValueError): legacy.resolve(self.root, self.current, "f" * 40)
        for key, value in (("sourceRevision", None), ("qualificationCredit", True), ("requiredCount", 348), ("unexpected", True)):
            with self.subTest(key=key):
                self.git("checkout", "-q", self.current)
                self.descriptor = json.loads(self.git("show", self.current + ":" + legacy.DESCRIPTOR))
                self.descriptor[key] = value
                self.write_descriptor()
                changed = self.commit()
                with self.assertRaises(ValueError): legacy.resolve(self.root, changed)

    def test_changed_legacy_control_inventory_or_tool_is_refused(self):
        for name, value in (("eng/tuple-ci-suites.json", b'{"groups": []}'),
                (".config/dotnet-tools.json", b'{"tools":{"hexalith.builds.module.cli":{"version":"4.44.4","rollForward":false}}}')):
            with self.subTest(name=name):
                self.git("checkout", "-q", self.old)
                self.write(name, value)
                bad_old = self.commit()
                self.descriptor = json.loads(self.git("show", self.current + ":" + legacy.DESCRIPTOR))
                self.descriptor["sourceRevision"] = bad_old
                self.write_descriptor()
                new = self.commit()
                self.git("checkout", "-q", bad_old)
                with self.assertRaises(ValueError): legacy.verify(self.root, new, bad_old)


if __name__ == "__main__":
    unittest.main()
