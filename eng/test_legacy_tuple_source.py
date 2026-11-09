"""Real private Git histories for bounded, truthful legacy/current source orchestration."""
import copy
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

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
        self.assertIn("  push:\n    branches: [main]", workflow)
        self.assertIn("PLATFORM_CANDIDATE_SHA: ${{ inputs.candidate-sha || github.sha }}", workflow)
        self.assertIn("ref: ${{ inputs.candidate-sha || github.sha }}", workflow)
        self.assertIn("python3 -B eng/legacy_tuple_source.py --current", workflow)
        self.assertIn("  legacy-public-cohort:\n    needs: [resolve-sources]", workflow)
        self.assertIn("ref: ${{ needs.resolve-sources.outputs.legacy-source }}", workflow)
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

    def test_exact_metadata_allowance_stays_bounded_and_old_body_remains_bound(self):
        self.assertEqual(len(legacy.ALLOWED), 10)
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

    def test_sdk42_source_and_provenance_transform_is_exact(self):
        self.git("checkout", "-q", self.old)
        (self.root / "references/Hexalith.EventStore").mkdir(parents=True)
        self.git("update-index", "--add", "--cacheinfo", "160000,cab8363c2b6e2c0588efc8ca9c2e09fab0006841,references/Hexalith.EventStore")
        (self.root / legacy.PARTIES_PATH).mkdir(parents=True, exist_ok=True)
        self.git("update-index", "--add", "--cacheinfo", "160000," + legacy.LEGACY_PARTIES_SOURCE + "," + legacy.PARTIES_PATH)
        historical = self.commit()
        self.git("checkout", "-q", self.current)
        self.descriptor["sourceRevision"] = historical
        self.write_descriptor()
        selected = json.loads(self.current_provenance)
        selected["tenants"].update(version="7.0.5", commit=legacy.TENANTS_705_SOURCE)
        selected["eventstore"].update(version="4.2.0", commit="aa16227bd07f0564a3026d619a5abd973e02656f")
        self.write(legacy.PROVENANCE, json.dumps(selected).encode())
        (self.root / "references/Hexalith.EventStore").mkdir(parents=True, exist_ok=True)
        self.git("update-index", "--add", "--cacheinfo", "160000,aa16227bd07f0564a3026d619a5abd973e02656f,references/Hexalith.EventStore")
        (self.root / legacy.PARTIES_PATH).mkdir(parents=True, exist_ok=True)
        self.git("update-index", "--add", "--cacheinfo", "160000," + legacy.CURRENT_PARTIES_SOURCE + "," + legacy.PARTIES_PATH)
        current = self.commit()
        self.git("checkout", "-q", historical)
        self.assertEqual(legacy.verify(self.root, current, historical)["requiredCount"], 349)
        self.git("checkout", "-q", current)
        self.git("update-index", "--cacheinfo", "160000," + "e" * 40 + ",references/Hexalith.EventStore")
        changed = self.commit()
        self.git("checkout", "-q", historical)
        with self.assertRaisesRegex(ValueError, "exact old source"):
            legacy.verify(self.root, changed, historical)

    def test_sdk42_parties_sources_refuse_arbitrary_missing_or_reversed_bindings(self):
        self.git("checkout", "-q", self.old)
        for path, source in [("references/Hexalith.EventStore", "cab8363c2b6e2c0588efc8ca9c2e09fab0006841"),
                             (legacy.PARTIES_PATH, legacy.LEGACY_PARTIES_SOURCE)]:
            (self.root / path).mkdir(parents=True, exist_ok=True)
            self.git("update-index", "--add", "--cacheinfo", "160000," + source + "," + path)
        historical = self.commit()
        for old_source, current_source in [("e" * 40, legacy.CURRENT_PARTIES_SOURCE),
                (None, legacy.CURRENT_PARTIES_SOURCE), (legacy.CURRENT_PARTIES_SOURCE, legacy.LEGACY_PARTIES_SOURCE),
                (legacy.LEGACY_PARTIES_SOURCE, "e" * 40), (legacy.LEGACY_PARTIES_SOURCE, None)]:
            with self.subTest(old=old_source, current=current_source):
                self.git("checkout", "-q", historical)
                if old_source is None:
                    self.git("update-index", "--force-remove", legacy.PARTIES_PATH)
                else:
                    self.git("update-index", "--cacheinfo", "160000," + old_source + "," + legacy.PARTIES_PATH)
                selected_old = historical if old_source == legacy.LEGACY_PARTIES_SOURCE else self.commit()
                self.git("checkout", "-q", self.current)
                self.descriptor["sourceRevision"] = selected_old
                self.write_descriptor()
                selected = json.loads(self.current_provenance)
                selected["tenants"].update(version="7.0.5", commit=legacy.TENANTS_705_SOURCE)
                selected["eventstore"].update(version="4.2.0", commit=legacy.EVENTSTORE_420_SOURCE)
                self.write(legacy.PROVENANCE, json.dumps(selected).encode())
                (self.root / "references/Hexalith.EventStore").mkdir(parents=True, exist_ok=True)
                (self.root / legacy.PARTIES_PATH).mkdir(parents=True, exist_ok=True)
                self.git("update-index", "--add", "--cacheinfo", "160000," + legacy.EVENTSTORE_420_SOURCE + ",references/Hexalith.EventStore")
                if current_source is None:
                    self.git("update-index", "--force-remove", legacy.PARTIES_PATH)
                else:
                    self.git("update-index", "--add", "--cacheinfo", "160000," + current_source + "," + legacy.PARTIES_PATH)
                selected_current = self.commit()
                self.git("checkout", "-q", selected_old)
                with self.assertRaisesRegex(ValueError, "Parties source"):
                    legacy.verify(self.root, selected_current, selected_old)

    def test_old_provenance_cannot_hide_an_arbitrary_parties_source_change(self):
        self.git("checkout", "-q", self.old)
        (self.root / legacy.PARTIES_PATH).mkdir(parents=True, exist_ok=True)
        self.git("update-index", "--add", "--cacheinfo", "160000," + legacy.LEGACY_PARTIES_SOURCE + "," + legacy.PARTIES_PATH)
        historical = self.commit()
        self.git("checkout", "-q", self.current)
        self.descriptor["sourceRevision"] = historical
        self.write_descriptor()
        (self.root / legacy.PARTIES_PATH).mkdir(parents=True, exist_ok=True)
        self.git("update-index", "--add", "--cacheinfo", "160000," + "e" * 40 + "," + legacy.PARTIES_PATH)
        changed = self.commit()
        self.git("checkout", "-q", historical)
        with self.assertRaisesRegex(ValueError, "exact historical BB"):
            legacy.verify(self.root, changed, historical)

    def test_new_replay_descriptor_is_finite_metadata_not_a_source_exemption(self):
        from pre_extension_tuple_source import DESCRIPTOR, BUILDS, EVENTSTORE, PROFILE_HASH
        document = dict(schema="hexalith.platform.pre-extension-tuple-source.v1", purpose="same-code-sdk41-tuple-regression",
                        sourceRevision=self.old, buildsSource=BUILDS, eventStoreSource=EVENTSTORE,
                        toolVersion="4.45.0", profileSha256=PROFILE_HASH, requiredCount=412, qualificationCredit=False)
        self.git("checkout", "-q", self.old)
        self.write(DESCRIPTOR, json.dumps(document).encode())
        historical = self.commit()
        self.git("checkout", "-q", self.current)
        self.descriptor["sourceRevision"] = historical
        self.write_descriptor()
        document["sourceRevision"] = historical
        self.write(DESCRIPTOR, json.dumps(document).encode())
        current = self.commit()
        self.git("checkout", "-q", historical)
        self.assertEqual(legacy.verify(self.root, current, historical)["requiredCount"], 349)
        for field, value in [("requiredCount", 411), ("unknown", "ignored-source"), ("qualificationCredit", True)]:
            self.git("checkout", "-q", current)
            changed_document = dict(document)
            changed_document[field] = value
            self.write(DESCRIPTOR, json.dumps(changed_document).encode())
            changed = self.commit()
            self.git("checkout", "-q", historical)
            with self.subTest(field=field), self.assertRaises(ValueError):
                legacy.verify(self.root, changed, historical)

    def test_current_checkout_cannot_claim_the_legacy_source(self):
        with self.assertRaises(ValueError): legacy.verify(self.root, self.current, self.old)
        self.assertEqual(self.git("rev-parse", "HEAD"), self.current)

    def test_exact_sdk420_tuple_is_accepted_without_relaxing_legacy_or_crossed_provenance(self):
        self.git("checkout", "-q", self.old)
        (self.root / "references/Hexalith.EventStore").mkdir(parents=True, exist_ok=True)
        self.git("update-index", "--add", "--cacheinfo", "160000,cab8363c2b6e2c0588efc8ca9c2e09fab0006841,references/Hexalith.EventStore")
        (self.root / legacy.PARTIES_PATH).mkdir(parents=True, exist_ok=True)
        self.git("update-index", "--add", "--cacheinfo", "160000," + legacy.LEGACY_PARTIES_SOURCE + "," + legacy.PARTIES_PATH)
        historical = self.commit()
        self.git("checkout", "-q", self.current)
        self.descriptor["sourceRevision"] = historical
        self.write_descriptor()
        (self.root / "references/Hexalith.EventStore").mkdir(parents=True, exist_ok=True)
        document = json.loads(self.current_provenance)
        document["tenants"].update(version="7.0.5", commit=legacy.TENANTS_705_SOURCE)
        document["eventstore"].update(version="4.2.0", commit=legacy.EVENTSTORE_420_SOURCE)
        self.write(legacy.PROVENANCE, json.dumps(document).encode())
        self.git("update-index", "--add", "--cacheinfo", "160000," + legacy.EVENTSTORE_420_SOURCE + ",references/Hexalith.EventStore")
        (self.root / legacy.PARTIES_PATH).mkdir(parents=True, exist_ok=True)
        self.git("update-index", "--add", "--cacheinfo", "160000," + legacy.CURRENT_PARTIES_SOURCE + "," + legacy.PARTIES_PATH)
        released = self.commit()
        self.git("checkout", "-q", historical)
        result = legacy.verify(self.root, released, historical)
        self.assertEqual(result["requiredCount"], 349)
        self.assertFalse(result["qualificationCredit"])
        self.assertFalse(result["binaryEquivalenceClaimed"])
        mutations = [("eventstore", "version", value) for value in
                     ("4.2.0-preview.1", "4.2.0+metadata", "4.2.0.0", "4.2.1", "4.3.0", "4.1.0")]
        mutations += [("eventstore", "commit", "dbc6e7e08d605ba6c6c790e3e08a15dc8bbdec8a"),
                      ("eventstore", "sha256", "0" * 64), ("tenants", "version", "7.0.4"),
                      ("tenants", "commit", legacy.TENANTS_CURRENT_SOURCE), ("memories", "version", "3.0.24")]
        for owner, field, value in mutations:
            with self.subTest(owner=owner, field=field, value=value):
                self.git("checkout", "-q", released)
                changed = copy.deepcopy(document)
                changed[owner][field] = value
                self.write(legacy.PROVENANCE, json.dumps(changed).encode())
                crossed = self.commit()
                self.git("checkout", "-q", historical)
                with self.assertRaisesRegex(ValueError, "more than the authenticated"):
                    legacy.verify(self.root, crossed, historical)

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


class PartiesCheckoutTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="finite-parties-checkout-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.owner = self.root / legacy.PARTIES_PATH
        self.owner.mkdir(parents=True)
        for repository in [self.root, self.owner]:
            self.git(repository, "init", "-q")
            self.git(repository, "config", "user.name", "Finite Parties checkout control")
            self.git(repository, "config", "user.email", "fixture@invalid")
        self.manifest = self.owner / "module/hexalith.module-manifest.v2.json"
        self.manifest.parent.mkdir()
        self.manifest.write_bytes(b'{"scope":"synthetic checkout control only"}\n')
        self.git(self.owner, "add", ".")
        self.git(self.owner, "commit", "-q", "-m", "test(tuple): record isolated owner")
        self.source = self.git(self.owner, "rev-parse", "HEAD")
        declaration = hashlib.sha256(self.manifest.read_bytes()).hexdigest()
        self.git(self.root, "update-index", "--add", "--cacheinfo", "160000," + self.source + "," + legacy.PARTIES_PATH)
        self.git(self.root, "commit", "-q", "-m", "test(tuple): bind isolated owner")
        # Synthetic constants are scoped to this genuine local Git control, never publication inputs.
        constants = patch.multiple(legacy, LEGACY_PARTIES_SOURCE=self.source, CURRENT_PARTIES_SOURCE=self.source,
                                   LEGACY_PARTIES_DECLARATION=declaration, CURRENT_PARTIES_DECLARATION=declaration)
        constants.start()
        self.addCleanup(constants.stop)

    def git(self, root, *arguments):
        return subprocess.run(["git", *arguments], cwd=root, capture_output=True, check=True).stdout.decode().strip()

    def test_actual_initialized_owner_and_manifest_match_both_finite_roles(self):
        for historical in [False, True]:
            result = legacy.verify_parties_checkout(self.root, historical)
            self.assertEqual(result["source"], self.source)
            self.assertFalse(result["qualificationCredit"])

    def test_wrong_recorded_gitlink_refuses(self):
        self.git(self.root, "update-index", "--cacheinfo", "160000," + "e" * 40 + "," + legacy.PARTIES_PATH)
        self.git(self.root, "commit", "-q", "-m", "test(tuple): change recorded owner")
        with self.assertRaises(ValueError):
            legacy.verify_parties_checkout(self.root, False)

    def test_wrong_initialized_owner_head_refuses(self):
        (self.owner / "changed.txt").write_text("different actual owner revision\n")
        self.git(self.owner, "add", ".")
        self.git(self.owner, "commit", "-q", "-m", "test(tuple): change initialized owner")
        with self.assertRaises(ValueError):
            legacy.verify_parties_checkout(self.root, False)

    def test_changed_manifest_bytes_refuse(self):
        self.manifest.write_bytes(self.manifest.read_bytes() + b" ")
        with self.assertRaises(ValueError):
            legacy.verify_parties_checkout(self.root, False)

    def test_missing_manifest_refuses(self):
        self.manifest.unlink()
        with self.assertRaises(ValueError):
            legacy.verify_parties_checkout(self.root, False)


if __name__ == "__main__":
    unittest.main()
