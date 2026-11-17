"""Real private Git histories for bounded, truthful legacy/current source orchestration."""
import copy
import hashlib
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
        for name in ("src/Owner.cs", "tests/Control.cs", "DaprSelfHostedMtls.cs", "Directory.Build.props", ".github/workflows/tuple-ci.yml", "eng/other.json", "README.md", "docs/current-tuple-preparation.md"):
            self.write(name, b"unchanged committed source\n")
        self.write("eng/tuple-ci-suites.json", self.profile)
        self.write(".config/dotnet-tools.json", json.dumps({"tools": {"hexalith.builds.module.cli": {"version": "4.44.3", "rollForward": False}}}).encode())
        self.descriptor = dict(schema="hexalith.platform.legacy-tuple-source.v1", purpose="same-code-legacy-tuple-regression", sourceRevision=None,
            buildsSource=legacy.BUILDS, toolVersion="4.44.3", profileSha256=legacy.PROFILE_HASH, requiredCount=349, qualificationCredit=False)
        self.write_descriptor()
        # Authentic bytes from committed legacy source c9c54d859d701a468d1c73090067cb6a65a00191;
        # never reconstruct this authority from whichever provenance the checkout selects.
        fixture = Path(__file__).parents[1] / "tests/Hexalith.Platform.Composition.Tests/Fixtures/public-702-provenance.json"
        self.provenance = fixture.read_bytes()
        historical_hash = "3e9b04109c5ddb34a0ac6801f5cdb36e1cbc102c65bfccb54b77c4d408091a61"
        self.assertEqual(hashlib.sha256(self.provenance).hexdigest(), historical_hash)
        self.assertEqual(legacy.LEGACY_PROVENANCE_HASH, historical_hash)
        self.write(legacy.PROVENANCE, self.provenance)
        (self.root / "references/Hexalith.Builds").mkdir(parents=True)
        self.git("update-index", "--add", "--cacheinfo", "160000," + legacy.BUILDS + ",references/Hexalith.Builds")
        self.old = self.commit()
        self.descriptor["sourceRevision"] = self.old
        self.write_descriptor()
        self.write("eng/tuple-ci-suites.json", b'{"groups": [{"current": true}]}\n')
        current = copy.deepcopy(json.loads(self.provenance))
        current["tenants"].update(version="7.0.4", commit=legacy.TENANTS_CURRENT_SOURCE)
        current["memories"].update(version="3.0.25", commit=legacy.MEMORIES_CURRENT_SOURCE)
        self.current_provenance = (json.dumps(current, indent=2) + "\n").encode()
        self.write(legacy.PROVENANCE, self.current_provenance)
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
        self.assertEqual(legacy.resolve(self.root, self.current), self.old)
        self.assertEqual(legacy.resolve(self.root, self.current, self.old), self.old)
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
        self.git("checkout", "-q", self.current)
        self.write(legacy.PROVENANCE, self.provenance)
        original_provenance_current = self.commit()
        self.git("checkout", "-q", self.old)
        self.assertEqual(legacy.verify(self.root, original_provenance_current, self.old)["sourceEquivalence"], "verified")

    def test_only_exact_eighth_provenance_path_is_added_and_old_body_remains_bound(self):
        self.assertEqual(len(legacy.ALLOWED), 8)
        self.git("checkout", "-q", self.old)
        result = legacy.verify(self.root, self.current, self.old)
        self.assertIn(legacy.PROVENANCE, result["allowedMetadataDifferences"])
        self.assertEqual(json.loads(self.current_provenance)["tenants"]["version"], "7.0.4")
        self.assertEqual(json.loads(self.provenance)["tenants"]["version"], "7.0.2")
        self.assertEqual(json.loads(self.current_provenance)["memories"]["version"], "3.0.25")
        self.assertEqual(json.loads(self.provenance)["memories"]["version"], "3.0.23")
        self.write(legacy.PROVENANCE, self.provenance + b" ")
        altered = self.commit()
        self.descriptor["sourceRevision"] = altered
        self.write_descriptor()
        changed = self.commit()
        self.git("checkout", "-q", altered)
        with self.assertRaisesRegex(ValueError, "historical Git body"):
            legacy.verify(self.root, changed, altered)

    def test_current_provenance_refuses_foreign_fields_or_partial_tenants_adoption(self):
        mutations = (("eventstore", "version", "9.9.9"), ("memories", "commit", "f" * 40),
            ("tenants", "sha256", "0" * 64), ("tenants", "source", "different.json"),
            ("tenants", "repository", "Foreign.Owner"), ("tenants", "commit", "143cec60a3f5bb1831fa57f35c510880ceb788d8"),
            ("tenants", "version", "7.0.2"),
            ("tenants", "unknown", True), ("memories", "version", "3.0.23"), ("memories", "version", "3.0.24"),
            ("memories", "commit", "82c0aa0a7046d7bd91569ac1c9a38a3ed0386d0f"),
            ("memories", "sha256", "0" * 64), ("unknown", None, {}))
        for owner, field, value in mutations:
            with self.subTest(owner=owner, field=field):
                self.git("checkout", "-q", self.current)
                document = json.loads(self.current_provenance)
                if field is None: document[owner] = value
                else: document[owner][field] = value
                self.write(legacy.PROVENANCE, json.dumps(document).encode())
                altered = self.commit()
                self.git("checkout", "-q", self.old)
                with self.assertRaisesRegex(ValueError, "more than the authenticated"):
                    legacy.verify(self.root, altered, self.old)

    def test_exact_published_705_tuple_is_accepted_and_crossed_version_sources_refuse(self):
        document = json.loads(self.current_provenance)
        document["tenants"].update(version="7.0.5", commit=legacy.TENANTS_705_SOURCE)
        self.write(legacy.PROVENANCE, json.dumps(document).encode())
        released = self.commit()
        self.git("checkout", "-q", self.old)
        self.assertEqual(legacy.verify(self.root, released, self.old)["requiredCount"], 349)
        for version, source in (("7.0.5", legacy.TENANTS_CURRENT_SOURCE), ("7.0.4", legacy.TENANTS_705_SOURCE),
                ("7.0.2", legacy.TENANTS_705_SOURCE), ("7.0.5", "143cec60a3f5bb1831fa57f35c510880ceb788d8")):
            with self.subTest(version=version, source=source):
                self.git("checkout", "-q", released)
                changed = copy.deepcopy(document)
                changed["tenants"].update(version=version, commit=source)
                self.write(legacy.PROVENANCE, json.dumps(changed).encode())
                crossed = self.commit()
                self.git("checkout", "-q", self.old)
                with self.assertRaisesRegex(ValueError, "more than the authenticated"):
                    legacy.verify(self.root, crossed, self.old)

    def test_current_checkout_cannot_claim_the_legacy_source(self):
        with self.assertRaises(ValueError): legacy.verify(self.root, self.current, self.old)
        self.assertEqual(self.git("rev-parse", "HEAD"), self.current)

    def test_production_helpers_tests_build_instructions_and_workflow_changes_refuse(self):
        for name in ("src/Owner.cs", "tests/Control.cs", "DaprSelfHostedMtls.cs", "Directory.Build.props", ".github/workflows/tuple-ci.yml", "eng/other.json", "README.md", "docs/current-tuple-preparation.md"):
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

    def patched_pair(self):
        floor = (Path(__file__).parent / "legacy-security-floor.props").read_bytes()
        packages = ('<Project><Import Project="$(MSBuildThisFileDirectory)' + legacy.SECURITY_FLOOR + '" /></Project>\n').encode()
        self.git("checkout", "-q", self.old)
        self.write(legacy.SECURITY_FLOOR, floor)
        self.write("Directory.Packages.props", packages)
        patched_legacy = self.commit()
        self.git("checkout", "-q", self.current)
        self.write(legacy.SECURITY_FLOOR, floor)
        self.write("Directory.Packages.props", packages)
        self.descriptor.update(schema="hexalith.platform.legacy-tuple-source.v2",
            purpose="same-code-security-patched-legacy-tuple-regression", sourceRevision=patched_legacy,
            securityBaseline={"originalSourceRevision": legacy.ORIGINAL_LEGACY, "propsPath": legacy.SECURITY_FLOOR,
                "propsSha256": hashlib.sha256(floor).hexdigest(), "minimumPackageVersions": copy.deepcopy(legacy.SECURITY_MINIMUMS),
                "auditException": False})
        self.write_descriptor()
        patched_current = self.commit()
        self.git("checkout", "-q", patched_legacy)
        return patched_current, patched_legacy

    def test_security_patched_pair_retains_original_controls_and_discloses_dependency_change(self):
        current, old = self.patched_pair()
        before = self.git("status", "--porcelain")
        result = legacy.verify(self.root, current, old)
        self.assertEqual(result["dependencyBaseline"], "security-patched")
        self.assertFalse(result["originalDependenciesPreserved"])
        self.assertFalse(result["securityBaseline"]["auditException"])
        self.assertFalse(result["nativeQualificationCredit"])
        self.assertEqual(result["requiredCount"], 349)
        self.assertEqual(len(legacy.ALLOWED), 8)
        self.assertEqual(hashlib.sha256((self.root / "eng/tuple-ci-suites.json").read_bytes()).hexdigest(), legacy.PROFILE_HASH)
        self.assertEqual(self.git("status", "--porcelain"), before)

    def test_security_floor_cannot_be_labelled_as_original_unpatched_dependencies(self):
        current, _ = self.patched_pair()
        self.git("checkout", "-q", current)
        self.descriptor.pop("securityBaseline")
        self.descriptor.update(schema="hexalith.platform.legacy-tuple-source.v1", purpose="same-code-legacy-tuple-regression")
        self.write_descriptor()
        changed = self.commit()
        with self.assertRaisesRegex(ValueError, "must declare the changed"):
            legacy.resolve(self.root, changed)

    def test_security_baseline_refuses_exception_stale_hash_or_widened_package_set(self):
        current, _ = self.patched_pair()
        for key, value in [("auditException", True), ("propsSha256", "0" * 64), ("propsPath", "other.props"),
                ("originalSourceRevision", "f" * 40), ("unexpected", True),
                ("minimumPackageVersions", {**legacy.SECURITY_MINIMUMS, "Foreign.Package": "1.0.0"}),
                ("minimumPackageVersions", {**legacy.SECURITY_MINIMUMS, "Aspire.Hosting": "13.6.4"})]:
            with self.subTest(key=key, value=value):
                self.git("checkout", "-q", current)
                self.descriptor = json.loads(self.git("show", current + ":" + legacy.DESCRIPTOR))
                self.descriptor["securityBaseline"][key] = value
                self.write_descriptor()
                changed = self.commit()
                with self.assertRaises(ValueError): legacy.resolve(self.root, changed)

    def test_security_floor_and_import_remain_bound_even_if_a_changed_body_is_rehashed(self):
        current, _ = self.patched_pair()
        for target in [legacy.SECURITY_FLOOR, "Directory.Packages.props"]:
            with self.subTest(target=target):
                self.git("checkout", "-q", current)
                self.descriptor = json.loads(self.git("show", current + ":" + legacy.DESCRIPTOR))
                path = self.root / target
                changed_body = (path.read_bytes().replace(b'>13.6.5</Version>', b'>13.6.4</Version>')
                    if target == legacy.SECURITY_FLOOR else b'<Project />\n')
                self.write(target, changed_body)
                if target == legacy.SECURITY_FLOOR:
                    self.descriptor["securityBaseline"]["propsSha256"] = hashlib.sha256(changed_body).hexdigest()
                self.write_descriptor()
                changed = self.commit()
                with self.assertRaises(ValueError): legacy.resolve(self.root, changed)


if __name__ == "__main__":
    unittest.main()
