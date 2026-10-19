import copy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import qualify as q
import reconcile_inputs as r


class InputReconciliationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.original = self.root / "original-live-evidence.json"
        self.configuration = self.root / "runtime-reference.json"
        self.config = q.read_public_configuration(Path(__file__).with_name("runtime-reference.example.json"))
        self.original_value = {"code": "DependencyNotAvailable", "dependency": "EXT-SECRETS-1",
                               "dependencyAvailable": False, "effectsStarted": False, "qualificationEffects": 0,
                               "missingInputs": sorted(r.HISTORICAL_INPUTS)}
        self.original.write_bytes(q.canonical(self.original_value))
        self.assertEqual(r.HISTORICAL_EVIDENCE_SHA256, hashlib.sha256(self.original.read_bytes()).hexdigest())
        self.configuration.write_bytes(q.canonical(self.config))

    def tearDown(self):
        self.temp.cleanup()

    def prepare(self, name="packet", **kwargs):
        return r.reconcile(self.original, self.configuration, self.root / name, observed_at=1792368000, **kwargs)

    def test_all_original_67_names_retained_once_with_owners_actions_and_dependencies(self):
        report = self.prepare()
        rows = report["inputs"]
        self.assertEqual(67, len(rows))
        self.assertEqual(50, len(r.HISTORICAL_REFERENCES))
        self.assertEqual(17, len(r.NAMED_GROUPS))
        self.assertEqual(set(self.original_value["missingInputs"]), {row["input"] for row in rows})
        self.assertEqual(67, sum(report["resolutionKindCounts"].values()))
        for row in rows:
            self.assertTrue(row["owner"])
            self.assertTrue(row["action"])
            self.assertTrue(row["sourceContracts"])
            self.assertTrue(set(row["dependsOnGroups"]) <= set(r.GROUPS))
            self.assertFalse(row["resolvedByThisPacket"])
        self.assertEqual("BlockedArchitectureAndImplementation", next(row for row in rows if row["input"] == "QualifiedAllCopyLifecycleAdapter")["resolutionKind"])

    def test_public_configuration_and_source_bytes_are_hash_bound_without_values(self):
        source = self.root / "public-source.cs"
        source.write_text("// reviewed public source\n")
        report = self.prepare(source_evidence=[source])
        hashes = {entry["path"]: entry["sha256"] for entry in report["sourceEvidence"]}
        for path in (self.original, self.configuration, source):
            self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), hashes[str(path)])
        packet = self.root / "packet"
        self.assertEqual(self.config, q.read_json(packet / "runtime-reference.pending.json"))
        requests = q.read_json(packet / "input-resolution-provisioning-requests.json")
        self.assertFalse(requests["apply"])
        self.assertFalse(requests["productionMutationAllowed"])
        self.assertEqual("Open", requests["acceptedPolicy"]["overlappingExportDisposition"])
        requested_names = [name for group in requests["groups"] for name, value in group["requestedPublicReferenceValues"].items() if value is None]
        self.assertEqual(set(report["currentMissingPublicReferences"]), set(requested_names))
        self.assertEqual(len(requested_names), len(set(requested_names)))

    def test_filled_reference_never_becomes_installed_or_qualified(self):
        self.config["runtime"]["currentInventoryAuthorityReference"] = "dapr:independent-authority@v1"
        self.config["runtime"]["profileNotBefore"] = "2026-10-01T00:00:00Z"
        self.config["runtime"]["profileValidUntil"] = "2026-11-01T00:00:00Z"
        self.configuration.write_bytes(q.canonical(self.config))
        report = self.prepare()
        rows = {row["input"]: row for row in report["inputs"]}
        self.assertEqual("PublicReferenceSuppliedNotVerified", rows["runtime.currentInventoryAuthorityReference"]["state"])
        self.assertEqual("ConfiguredWindowCurrentNotIndependentlyVerified", rows["CurrentProfileValidity"]["state"])
        self.assertEqual("IndependentInstallationOrAcceptanceEvidenceRequired", rows["IndependentCurrentInventoryAuthority"]["state"])
        self.assertFalse(report["dependencyAvailable"])
        self.assertFalse(report["qualificationPassed"])
        self.assertFalse(report["installationInferredFromReferences"])

    def test_new_current_missing_inputs_are_supplemental_not_historical_rewrites(self):
        report = self.prepare()
        self.assertEqual(sorted(set(q.REQUIRED) - r.HISTORICAL_REFERENCES), report["additionalCurrentMissingPublicReferences"])
        self.assertEqual(67, len(report["inputs"]))
        self.assertEqual(set(q.REQUIRED), set(report["currentMissingPublicReferences"]))
        self.assertEqual(4, len(report["supplementalCohortPrerequisites"]))

    def test_never_reads_or_inspects_mounted_credentials_or_calls_a_runtime(self):
        credential = self.root / "private-credential"
        credential.write_text("SENTINEL-PRIVATE-CREDENTIAL")
        self.config["qualification"]["daprApiCredentialReference"] = "file:" + str(credential)
        self.configuration.write_bytes(q.canonical(self.config))
        actual = q.bounded_public_file
        def only_public(path, limit, **kwargs):
            self.assertNotEqual(credential, Path(path))
            return actual(path, limit, **kwargs)
        with patch.object(q, "bounded_public_file", side_effect=only_public), \
                patch.object(q, "qualification_prerequisite_inputs", side_effect=AssertionError("must not inspect private mounts")), \
                patch.object(q, "DaprTransport", side_effect=AssertionError("must not contact runtime")), \
                patch.object(q, "git", side_effect=AssertionError("must not change acceptance")):
            report = self.prepare()
        self.assertFalse(report["credentialsInspected"])
        self.assertFalse(report["runtimeContacted"])
        for path in (self.root / "packet").iterdir():
            self.assertNotIn("SENTINEL-PRIVATE-CREDENTIAL", path.read_text())

    def test_rejects_wrong_duplicate_unknown_or_effectful_historical_report_before_output(self):
        bad = []
        for field, value in (("effectsStarted", True), ("qualificationEffects", True), ("qualificationEffects", 1), ("missingInputs", [])):
            bad.append(dict(self.original_value, **{field: value}))
        unknown = copy.deepcopy(self.original_value)
        unknown["missingInputs"][0] = "UNTRUSTED-INPUT-MARKER"
        bad.append(unknown)
        duplicated = copy.deepcopy(self.original_value)
        duplicated["missingInputs"][0] = duplicated["missingInputs"][1]
        bad.append(duplicated)
        for index, value in enumerate(bad):
            with self.subTest(index=index):
                self.original.write_bytes(q.canonical(value))
                with self.assertRaises(ValueError):
                    self.prepare(name=f"bad-{index}")
                self.assertFalse((self.root / f"bad-{index}").exists())

    def test_same_67_names_with_substituted_metadata_rejected_by_original_byte_pin(self):
        changed = dict(self.original_value, fabricatedMetadata="UNTRUSTED-METADATA")
        self.original.write_bytes(q.canonical(changed))
        with self.assertRaisesRegex(ValueError, "ExactHistoricalEvidenceSha256Required"):
            self.prepare()
        self.assertFalse((self.root / "packet").exists())

    def test_interpretation_and_source_hashes_use_same_single_captured_input_bytes(self):
        register = self.root / "register.md"
        target = "a" * 40
        register.write_text("### EXT-SECRETS-1 — Platform Secret Resolution\n"
                            "| `TargetVersionOrCommit` | `" + target + "` |\n"
                            "| `AcceptedStatus` | `Committed` |\n"
                            "| `TargetIntegrationDate` | `2026-10-08` |\n"
                            '| `CompatibilityContractAndVerificationCommand` | Command: `test "' + target + '" && status --porcelain` |\n'
                            "### EXT-EXPORT-STORE-1\n")
        captured = {path: path.read_bytes() for path in (self.original, self.configuration, register)}
        counts = {path: 0 for path in captured}
        actual = q.bounded_public_file
        def replace_after_capture(path, limit, **kwargs):
            path = Path(path)
            data = actual(path, limit, **kwargs)
            if path in captured:
                counts[path] += 1
                path.write_bytes(b"SUBSTITUTED-AFTER-CAPTURE")
            return data
        with patch.object(q, "bounded_public_file", side_effect=replace_after_capture):
            report = self.prepare(register=register, source_evidence=list(captured))
        hashes = {entry["path"]: entry["sha256"] for entry in report["sourceEvidence"]}
        self.assertEqual({path: 1 for path in captured}, counts)
        for path, data in captured.items():
            self.assertEqual(hashlib.sha256(data).hexdigest(), hashes[str(path)])
        self.assertEqual(target, report["checkedRegisterTarget"])
        self.assertEqual(self.config, q.read_json(self.root / "packet/runtime-reference.pending.json"))

    def test_explicit_private_credential_sources_and_inode_aliases_rejected_without_read(self):
        credential = self.root / "private-token"
        credential.write_text("SENTINEL-DO-NOT-READ")
        hardlink = self.root / "apparently-public-proof.json"
        os.link(credential, hardlink)
        symlink = self.root / "apparently-public-proof-link.json"
        symlink.symlink_to(credential)
        actual = q.bounded_public_file
        for field in ("qualification.daprApiCredentialReference", "qualification.enrollmentCredentialReference",
                      "runtime.hmacCredentialReference", "runtime.independentSignerCredentialReference"):
            for source in (credential, hardlink, symlink):
                with self.subTest(field=field, source=source.name):
                    config = copy.deepcopy(self.config)
                    section, name = field.split(".")
                    config[section][name] = "file:" + str(credential)
                    self.configuration.write_bytes(q.canonical(config))
                    reads = []
                    def only_public(path, limit, **kwargs):
                        reads.append(Path(path))
                        self.assertNotIn(Path(path), (credential, hardlink, symlink))
                        return actual(path, limit, **kwargs)
                    with patch.object(q, "bounded_public_file", side_effect=only_public):
                        with self.assertRaisesRegex(ValueError, "PublicEvidenceCannot"):
                            self.prepare(source_evidence=[source])
                    self.assertEqual([self.configuration], reads)
                    self.assertFalse((self.root / "packet").exists())

    def test_source_swapped_to_private_hardlink_at_open_is_rejected_before_read_or_hash(self):
        credential = self.root / "private-token"
        secret = b"SENTINEL-CREDENTIAL-MUST-NEVER-BE-READ-OR-HASHED"
        credential.write_bytes(secret)
        source = self.root / "public-proof.json"
        source.write_text("public proof before race")
        self.config["runtime"]["hmacCredentialReference"] = "file:" + str(credential)
        self.configuration.write_bytes(q.canonical(self.config))
        private_stat = credential.stat()
        private_inode = (private_stat.st_dev, private_stat.st_ino)
        actual_read, actual_capture, actual_digest = q.os.read, q.bounded_public_file, r.hashlib.sha256
        swapped, private_reads, private_digests = [], [], []
        def swap_immediately_before_open(path, limit, **kwargs):
            if Path(path) == source:
                source.unlink()
                os.link(credential, source)
                swapped.append(True)
            return actual_capture(path, limit, **kwargs)
        def never_read_private(descriptor, size):
            metadata = os.fstat(descriptor)
            if (metadata.st_dev, metadata.st_ino) == private_inode:
                private_reads.append(True)
                self.fail("credential descriptor reached os.read")
            return actual_read(descriptor, size)
        def never_hash_secret(data=b"", **kwargs):
            if data == secret:
                private_digests.append(True)
                self.fail("credential contents reached SHA-256")
            return actual_digest(data, **kwargs)
        with patch.object(q, "bounded_public_file", side_effect=swap_immediately_before_open), \
                patch.object(q.os, "read", side_effect=never_read_private), \
                patch.object(r.hashlib, "sha256", side_effect=never_hash_secret), \
                patch.object(r, "public_hash", wraps=r.public_hash) as hashes:
            with self.assertRaisesRegex(ValueError, "configured credential"):
                self.prepare(source_evidence=[source])
        self.assertEqual([True], swapped)
        self.assertEqual([], private_reads)
        self.assertEqual([], private_digests)
        self.assertNotIn(source, [Path(call.args[0]) for call in hashes.call_args_list])
        self.assertFalse((self.root / "packet").exists())

    def test_secret_bearing_configuration_rejected_and_existing_output_preserved(self):
        self.config["runtime"]["privateKey"] = "PRIVATE-MARKER"
        self.configuration.write_bytes(q.canonical(self.config))
        with self.assertRaises(q.Unavailable):
            self.prepare()
        self.assertFalse((self.root / "packet").exists())
        del self.config["runtime"]["privateKey"]
        self.configuration.write_bytes(q.canonical(self.config))
        self.prepare()
        before = {path.name: path.read_bytes() for path in (self.root / "packet").iterdir()}
        with self.assertRaises(ValueError):
            self.prepare()
        self.assertEqual(before, {path.name: path.read_bytes() for path in (self.root / "packet").iterdir()})

    def test_missing_public_source_prevents_partial_packet_and_does_not_copy_inputs(self):
        with self.assertRaises(OSError):
            self.prepare(source_evidence=[self.root / "missing-public-evidence.json"])
        self.assertFalse((self.root / "packet").exists())
        self.assertEqual(self.original_value, q.read_json(self.original))

    def test_cohort_missing_set_uses_explicit_register_and_candidate_without_rewriting_history(self):
        register = self.root / "register.md"
        accepted, candidate = "a" * 40, "b" * 40
        register.write_text("### EXT-PROTECTION-1\n| `AcceptedStatus` | `Uncommitted` |\n"
                            "### EXT-HOST-1\n| `TargetVersionOrCommit` | `" + accepted + "` |\n"
                            "| `CompatibilityContractAndVerificationCommand` | Command: `verify-host` |\n"
                            "### OQ-5.4-1\nAccepted bounded cohort.\n")
        report = self.prepare(register=register, target=candidate)
        self.assertEqual(candidate, report["checkedRegisterTarget"])
        self.assertEqual(set(r.COHORT_OBLIGATIONS), set(report["currentCohortMissingInputs"]))
        self.assertEqual(67, len(report["inputs"]))
        self.assertIn(str(register), {entry["path"] for entry in report["sourceEvidence"]})
        self.assertTrue(all(row["state"] == "RegisterOrImplementationPrerequisiteMissing" for row in report["supplementalCohortPrerequisites"]))
        with self.assertRaises(ValueError):
            self.prepare(name="candidate-without-register", target=candidate)

    def test_new_required_fields_are_explicit_nulls_in_pending_template(self):
        field = "runtime.replayRegistrarReference"
        with patch.object(q, "REQUIRED", q.REQUIRED + ((field,) if field not in q.REQUIRED else ())):
            self.config["runtime"].pop("replayRegistrarReference", None)
            self.configuration.write_bytes(q.canonical(self.config))
            report = self.prepare()
        self.assertIn(field, report["additionalCurrentMissingPublicReferences"])
        pending = q.read_json(self.root / "packet/runtime-reference.pending.json")
        self.assertIn("replayRegistrarReference", pending["runtime"])
        self.assertIsNone(pending["runtime"]["replayRegistrarReference"])

    def test_cli_malformed_fifo_and_oversized_registers_return_sanitized_blocked_result(self):
        malformed = self.root / "malformed-register.md"
        malformed.write_text("PRIVATE-MARKER missing required sections")
        missing_target = self.root / "missing-target-register.md"
        missing_target.write_text("### EXT-SECRETS-1 — Platform Secret Resolution\nPRIVATE-MARKER\n")
        fifo = self.root / "register.fifo"
        os.mkfifo(fifo)
        oversized = self.root / "oversized-register.md"
        oversized.write_bytes(b"PRIVATE-MARKER" + b"x" * (1024 * 1024))
        for index, register in enumerate((malformed, missing_target, fifo, oversized)):
            with self.subTest(register=register.name):
                output = self.root / f"cli-blocked-{index}"
                result = subprocess.run([sys.executable, str(Path(r.__file__).resolve()),
                    "--original-evidence", str(self.original), "--configuration", str(self.configuration),
                    "--register", str(register), "--output", str(output)], capture_output=True, text=True, timeout=3)
                self.assertEqual(2, result.returncode)
                self.assertEqual({"code": "PublicInputReconciliationBlocked", "effects": 0,
                                  "dependencyAvailable": False}, json.loads(result.stdout))
                self.assertEqual("", result.stderr)
                self.assertNotIn("PRIVATE-MARKER", result.stdout)
                self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main()
