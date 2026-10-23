"""Local safety checks for the Story 4.1 diagnostic recorder."""

import importlib.util
from datetime import datetime, timezone
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
        for name in prepare.SOURCE_FILES:
            target = self.project / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text("sanitized fixture\n")
        self.status = self.project / prepare.ARTIFACTS / "sprint-status.yaml"
        self.status.write_text(f"development_status:\n  {prepare.STORY_40}: in-progress\n")
        self.target_review = self.project / prepare.TARGET_REVIEW
        self.target_review.write_text(json.dumps({
            "checkedAt": "2026-10-04T09:52:00Z", "acceptedExecutionPins": True,
            "workloadHop": {"tag": "v1.35.9"}, "workloadTarget": {"tag": "v1.36.5"},
        }))
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
        self.assertEqual(set(result["workloadOutcomes"]),
                         {"keycloak", "openbao", "memories", "forgejo", "forgejo-runner"})
        self.assertTrue(all(value == "not-run" for value in result["workloadOutcomes"].values()))
        self.assertIsNone(result["additionalWorkloadOutcomesFromFreshCensus"])
        self.assertIsNone(result["collectionRequirements"]["signedSanitizedSummarySha256"])

    def test_both_dated_hops_have_separate_closed_gates_and_hashed_lineage(self):
        before = {str(name): prepare.sha256(self.project / name) for name in prepare.SOURCE_FILES}
        directory = self.run_preparation()
        attempt = json.loads((directory / "attempt.json").read_text())
        self.assertEqual([(v["from"], v["to"], v["targetPatch"]) for v in attempt["intendedMinorHops"]],
                         [("1.34", "1.35", "v1.35.9"), ("1.35", "1.36", "v1.36.5")])
        self.assertIsNone(attempt["liveSourceVersion"])
        self.assertEqual(attempt["evidenceHashes"], before)
        for suffix, target, field in (("1.34-to-1.35", "v1.35.9", "workloadHop.tag"),
                                      ("1.35-to-1.36", "v1.36.5", "workloadTarget.tag")):
            with self.subTest(hop=suffix):
                hop = json.loads((directory / f"hop-gate-{suffix}.json").read_text())
                self.assertEqual(hop["exactTargetPatch"], target)
                self.assertEqual(hop["gateState"], "closed")
                self.assertFalse(hop["signed"])
                self.assertFalse(hop["mutationAuthorized"])
                self.assertEqual(hop["evidenceHashes"], before)
                self.assertEqual(hop["targetLineage"]["selectionField"], field)
                self.assertEqual(hop["targetLineage"]["sha256"], before[str(prepare.TARGET_REVIEW)])
                self.assertEqual(hop["targetLineage"]["checkedAt"], "2026-10-04T09:52:00Z")
                self.assertEqual(hop["targetLineage"]["currencyValidation"], "not-validated")
                self.assertEqual(hop["targetLineage"]["compatibilityValidation"], "not-validated")
                self.assertFalse(hop["targetLineage"]["executionPinsAccepted"])
                self.assertEqual(set(hop["refreshRequirements"]), set(hop["prerequisites"]))
                self.assertIsNone(hop["freshPrerequisiteRecordHashes"])
                self.assertEqual(hop["prerequisites"]["fresh-authenticated-protected-workload-health"], "not-run")
                self.assertIn("fresh-authenticated-protected-workload-health", hop["refreshRequirements"])
                self.assertEqual(hop["preUpgradeHealth"], {"record": "pre-upgrade-health.json", "sha256": None,
                                                         "verificationResult": "not-run"})
                lineage = hop["procedureLineage"]
                self.assertEqual(lineage["historicalMaintenance"]["sha256"], before[str(prepare.MAINTENANCE)])
                self.assertEqual(lineage["retirementProcedure"]["sha256"], before[str(prepare.RETIREMENT_PROCEDURE)])
                self.assertEqual(lineage["successorMaintenance"]["state"],
                                 "blocked-until-accepted-retirement-and-fresh-census")
                self.assertIsNone(lineage["successorMaintenance"]["sha256"])
                outcome = json.loads((directory / f"minor-hop-{suffix}.json").read_text())
                self.assertEqual(outcome["exactTargetPatch"], target)
                self.assertEqual(outcome["commands"], [])
                self.assertIsNone(outcome["resultingComponentVersions"])
                self.assertEqual(outcome["verificationResult"], "not-run")
                self.assertFalse(outcome["mutationAuthorized"])
                self.assertEqual(outcome["approvedGateRecord"], f"hop-gate-{suffix}.json")
                self.assertIsNone(outcome["approvedGateSha256"])
                self.assertIsNone(outcome["approvedSuccessorMaintenanceSha256"])
                self.assertEqual(outcome["postflightEvidenceHashes"],
                                 {"post-upgrade-health.json": None, "dynamic-storage-proof.json": None})
                postflight = outcome["preUncordonRequirements"]
                self.assertTrue(postflight["instructionsOnly"])
                self.assertFalse(postflight["uncordonAuthorized"])
                self.assertEqual(postflight["verificationResult"], "not-run")
                self.assertIsNone(postflight["acceptedEvidenceSha256"])
        result = json.loads((directory / "upgrade-result.json").read_text())
        self.assertIsNone(result["actualFinalComponentVersions"])
        self.assertEqual(before, {str(name): prepare.sha256(self.project / name) for name in prepare.SOURCE_FILES})

    def test_missing_required_source_refuses_before_publishing_attempt(self):
        for name in prepare.SOURCE_FILES:
            with self.subTest(source=name):
                source = self.project / name
                original = source.read_bytes()
                source.unlink()
                with self.assertRaises(FileNotFoundError):
                    self.run_preparation()
                self.assertFalse(self.evidence.exists())
                source.write_bytes(original)

    def test_invalid_or_nonsequential_target_review_refuses_before_publication(self):
        for review in ([], {}, {"checkedAt": "undated"},
                       {"checkedAt": "2026-10-04T09:52:00Z", "workloadHop": {"tag": "v1.36.5"}},
                       {"checkedAt": "2026-10-04T09:52:00Z", "workloadHop": {"tag": "v1.35.9"},
                        "workloadTarget": {"tag": "v1.37.1"}}):
            with self.subTest(review=review):
                self.target_review.write_text(json.dumps(review))
                with self.assertRaises(ValueError):
                    self.run_preparation()
                self.assertFalse(self.evidence.exists())

    def test_targets_are_read_from_retained_selection_not_independent_literals(self):
        review = json.loads(self.target_review.read_text())
        review["workloadHop"]["tag"] = "v1.35.10"
        review["workloadTarget"]["tag"] = "v1.36.6"
        self.target_review.write_text(json.dumps(review))
        directory = self.run_preparation()
        attempt = json.loads((directory / "attempt.json").read_text())
        self.assertEqual([v["targetPatch"] for v in attempt["intendedMinorHops"]], ["v1.35.10", "v1.36.6"])

    def test_noncanonical_patch_tags_in_either_correct_minor_refuse_before_publication(self):
        for field, minor in (("workloadHop", "1.35"), ("workloadTarget", "1.36")):
            for suffix in (".*", "", ".", ".9-rc.1", ".09", ".00"):
                with self.subTest(field=field, suffix=suffix):
                    review = {"checkedAt": "2026-10-04T09:52:00Z", "workloadHop": {"tag": "v1.35.9"},
                              "workloadTarget": {"tag": "v1.36.5"}}
                    review[field]["tag"] = f"v{minor}{suffix}"
                    self.target_review.write_text(json.dumps(review))
                    with self.assertRaises(ValueError):
                        self.run_preparation()
                    self.assertFalse(self.evidence.exists())

    def test_duplicate_review_keys_refuse_before_publication(self):
        for review in (
            '{"checkedAt":"2026-10-04T09:52:00Z","checkedAt":"2026-10-04T09:52:00Z",'
            '"workloadHop":{"tag":"v1.35.9"},"workloadTarget":{"tag":"v1.36.5"}}',
            '{"checkedAt":"2026-10-04T09:52:00Z","workloadHop":{"tag":"v1.35.9","tag":"v1.35.10"},'
            '"workloadTarget":{"tag":"v1.36.5"}}',
            '{"checkedAt":"2026-10-04T09:52:00Z","workloadHop":{"tag":"v1.35.9"},'
            '"workloadTarget":{"tag":"v1.36.5","tag":"v1.36.6"}}',
        ):
            with self.subTest(review=review):
                self.target_review.write_text(review)
                with self.assertRaisesRegex(ValueError, "duplicate JSON keys"):
                    self.run_preparation()
                self.assertFalse(self.evidence.exists())

    def test_future_and_utc_overflow_review_dates_refuse_before_publication(self):
        captured_at = datetime(2026, 10, 14, 6, 43, 46, tzinfo=timezone.utc)
        for checked_at in ("2026-10-14T06:43:47Z", "9999-12-31T23:59:59-01:00", "0001-01-01T00:00:00+01:00"):
            with self.subTest(checked_at=checked_at):
                review = json.loads(self.target_review.read_text())
                review["checkedAt"] = checked_at
                self.target_review.write_text(json.dumps(review))
                with patch.object(prepare, "datetime") as clock:
                    clock.now.return_value = captured_at
                    clock.fromisoformat.side_effect = datetime.fromisoformat
                    with self.assertRaisesRegex(ValueError, "checkedAt no later than preparation"):
                        self.run_preparation()
                self.assertFalse(self.evidence.exists())

    def test_target_selection_hash_binds_consumed_snapshot_despite_source_replacement(self):
        original = prepare.sha256(self.target_review)
        select = prepare.selected_hops

        def replace_after_capture(review_bytes, captured_at):
            changed = json.loads(review_bytes)
            changed["workloadHop"]["tag"] = "v1.35.10"
            changed["workloadTarget"]["tag"] = "v1.36.6"
            self.target_review.write_text(json.dumps(changed))
            return select(review_bytes, captured_at)

        with patch.object(prepare, "selected_hops", side_effect=replace_after_capture):
            directory = self.run_preparation()
        attempt = json.loads((directory / "attempt.json").read_text())
        self.assertEqual([v["targetPatch"] for v in attempt["intendedMinorHops"]], ["v1.35.9", "v1.36.5"])
        self.assertEqual(attempt["targetLineage"]["sha256"], original)
        self.assertNotEqual(prepare.sha256(self.target_review), original)

    def test_second_hop_requires_accepted_first_outcome_and_new_fresh_gates(self):
        directory = self.run_preparation()
        first = json.loads((directory / "hop-gate-1.34-to-1.35.json").read_text())
        second = json.loads((directory / "hop-gate-1.35-to-1.36.json").read_text())
        self.assertIsNone(first["previousHop"])
        self.assertEqual(second["prerequisites"]["accepted-first-hop-outcome"], "not-accepted")
        self.assertEqual(second["prerequisites"]["new-recovery-preflight-and-approval-after-first-hop"], "not-run")
        self.assertEqual(second["previousHop"]["outcomeRecord"], "minor-hop-1.34-to-1.35.json")
        self.assertEqual(second["previousHop"]["acceptance"], "not-validated")
        self.assertIsNone(second["previousHop"]["acceptedOutcomeSha256"])
        self.assertIsNone(second["previousHop"]["actualResultingComponentVersions"])
        self.assertFalse(second["mutationAuthorized"])

    def test_done_and_claimed_passing_files_cannot_authorize_mutation(self):
        self.status.write_text(f"development_status:\n  {prepare.STORY_40}: done\n  {prepare.STORY_427}: done\n")
        bundle = self.root / "bundle"
        for name in (*prepare.PROOF_FILES, prepare.VALIDATION_FILE, "retirement-acceptance.json",
                     "successor-maintenance.json", "minor-hop-1.34-to-1.35.json"):
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
        for suffix in ("1.34-to-1.35", "1.35-to-1.36"):
            hop = json.loads((directory / f"hop-gate-{suffix}.json").read_text())
            self.assertEqual(hop["gateState"], "closed")
            self.assertFalse(hop["mutationAuthorized"])
            self.assertEqual(hop["prerequisites"]["accepted-4.27-signed-retirement-preservation-and-private-native-access"],
                             "not-validated")
            self.assertEqual(hop["prerequisites"]["fresh-post-retirement-recovery-and-configuration-census"], "not-run")
            self.assertEqual(hop["prerequisites"]["exact-successor-maintenance-procedure-and-outage-approval"], "not-approved")
            self.assertEqual(hop["prerequisites"]["fresh-per-hop-target-currency-and-compatibility"], "not-validated")
            self.assertEqual(hop["procedureLineage"]["successorMaintenance"]["approval"], "not-approved")

    def test_collection_scope_requires_fresh_complete_evidence_for_both_hops(self):
        directory = self.run_preparation()
        for name in prepare.OPERATIONAL_FILES:
            with self.subTest(record=name):
                record = json.loads((directory / name).read_text())
                plan = record["collectionRequirements"]
                self.assertTrue(plan["instructionsOnly"])
                self.assertTrue(plan["freshEvidenceRequiredForEveryHop"])
                self.assertEqual([(item["fromMinor"], item["toMinor"]) for item in plan["perHopEvidence"]],
                                 [("1.34", "1.35"), ("1.35", "1.36")])
                # A retained review claiming accepted pins never supplies live target or proof acceptance.
                for item in plan["perHopEvidence"]:
                    self.assertIsNone(item["acceptedTargetPatch"])
                    self.assertIsNone(item["evidenceSha256"])
                    self.assertEqual(item["verificationResult"], "not-run")
                self.assertEqual(record["verificationResult"], "not-run")
                self.assertFalse(record["mutationAuthorized"])
        for name, required_bindings in (
                ("preflight.json", ("liveComponentVersions", "acceptedExecutionIdentitiesSha256")),
                ("external-etcd-recovery-point.json", ("sourceMemberCensusSha256", "snapshotIntegrityEvidenceSha256",
                                                      "immutableValidatorReadbackSha256", "signedSourceComparisonSha256")),
                ("external-etcd-isolated-restore.json", ("sourceRecoveryPointSha256", "isolationEvidenceSha256",
                                                       "comparisonEvidenceSha256", "signedCleanupEvidenceSha256")),
                ("node-recovery-bundle.json", ("sourceRecoveryPointSha256", "matchingConfigurationCensusSha256",
                                              "immutableValidatorReadbackSha256", "testedRecoveryProcedureSha256"))):
            with self.subTest(recovery_or_preflight=name):
                plan = json.loads((directory / name).read_text())["collectionRequirements"]
                self.assertTrue(plan["requiredChecks"])
                for binding in required_bindings:
                    self.assertIsNone(plan[binding])
        preflight = json.loads((directory / "preflight.json").read_text())["collectionRequirements"]
        self.assertEqual(preflight["versionSkewResult"], "not-run")
        self.assertEqual(preflight["kubeadmPlanResult"], "not-run")
        compatibility = json.loads((directory / "api-addon-compatibility.json").read_text())["collectionRequirements"]
        self.assertFalse(compatibility["completeLiveAndDesiredInventoryEstablished"])
        self.assertIsNone(compatibility["additionalInstalledOperatorsAddonsAndWebhooks"])
        self.assertEqual(set(compatibility["componentResults"]),
                         {"Calico/CNI", "CoreDNS", "kube-proxy", "OpenEBS/CSI/storage",
                          "CloudNativePG", "Dapr", "Traefik", "cert-manager"})
        for phase in ("pre", "post"):
            health = json.loads((directory / f"{phase}-upgrade-health.json").read_text())["collectionRequirements"]
            self.assertEqual(set(health["workloads"]), {"keycloak", "openbao", "memories", "forgejo", "forgejo-runner"})
            self.assertIsNone(health["additionalWorkloadsFromFreshOutageCensus"])
            self.assertTrue(all(item["verificationResult"] == "not-run" and
                                item["ownerApprovedExpectationSha256"] is None
                                for item in health["workloads"].values()))
        storage = json.loads((directory / "dynamic-storage-proof.json").read_text())["collectionRequirements"]
        self.assertIsNone(storage["requiredDynamicStorageClassesFromFreshCensus"])
        transition = json.loads((directory / "maintenance-transition.json").read_text())
        self.assertEqual(transition["quiesce"]["state"], "not-started")
        self.assertIsNone(transition["collectionRequirements"]["approvedSuccessorProcedureSha256"])
        self.assertIsNone(transition["collectionRequirements"]["accountableStopRecoveryDecisionSha256"])

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
