"""Adversarial source packet checks; no installed/native/physical evidence is generated."""
import copy
import hashlib
import json
from pathlib import Path
import shutil
import tempfile
import unittest

from check_f1_packets import PACKETS, HOLDER_CLASSES, check_packets, check_transport, check_termination


class F1PacketTests(unittest.TestCase):
    def setUp(self):
        self.transport = json.loads((PACKETS / "transport-profile.json").read_text())
        self.termination = json.loads((PACKETS / "termination-protocol.json").read_text())

    def test_complete_proposed_packets_stay_unavailable_and_key_delivery_closed(self):
        result = check_packets()
        for flag in ("DependencyAvailable", "LiveReady", "KeyDeliveryEnabled", "CompletionPermission", "physicalCompletionProved"):
            self.assertIs(result[flag], False)

    def test_missing_holder_class_is_rejected_in_requirements_and_inventory(self):
        for cls in HOLDER_CLASSES:
            for location in ("requiredHolderClasses", "admissionInventory", "observedInventory"):
                packet = copy.deepcopy(self.termination)
                if location == "requiredHolderClasses":
                    packet[location].remove(cls)
                else:
                    packet["syntheticWitness"][location] = [h for h in packet["syntheticWitness"][location] if h["holderClass"] != cls]
                with self.subTest(cls=cls, location=location), self.assertRaises(ValueError):
                    check_termination(packet)

    def test_replacement_cannot_substitute_old_process_transport_host_or_lineage(self):
        for field in ("processIncarnation", "transportIncarnation", "hostIncarnation", "copyLineage"):
            packet = copy.deepcopy(self.termination)
            packet["syntheticWitness"]["observedInventory"][3][field] = "synthetic-replacement"
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, "substituted incarnation"):
                check_termination(packet)
        packet = copy.deepcopy(self.termination)
        packet["syntheticWitness"]["observedInventory"] = packet["syntheticWitness"]["observedInventory"][1:]
        with self.assertRaisesRegex(ValueError, "Missing/replaced holder"):
            check_termination(packet)

    def test_unsealed_late_queue_stale_watermark_or_new_admission_blocks_closure(self):
        for field, value in (("sealed", False), ("sealedThroughSequence", 11), ("observedThroughSequence", 11), ("newAdmissionRejected", False)):
            packet = copy.deepcopy(self.termination)
            packet["syntheticWitness"]["queueSeals"][-1][field] = value
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, "Unsealed queue"):
                check_termination(packet)
        packet = copy.deepcopy(self.termination)
        packet["syntheticWitness"]["observedHighWatermark"] = 11
        with self.assertRaisesRegex(ValueError, "watermark"):
            check_termination(packet)

    def test_cancellation_restart_duplicate_and_lost_ack_cannot_reset_original(self):
        for field in ("preserveOldIncarnations", "preserveSendCommitted", "preserveInventoryWatermarks", "authorityOriginalsAndWitnessesOutsideBoundary"):
            packet = copy.deepcopy(self.termination)
            packet["recovery"][field] = False
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, "Unsafe recovery"):
                check_termination(packet)
        packet = copy.deepcopy(self.termination)
        packet["syntheticWitness"]["observedInventory"][0]["originalIdentity"]["intentId"] = "new-intent"
        with self.assertRaises(ValueError):
            check_termination(packet)

    def test_missing_verifier_and_synthetic_witness_cannot_establish_completion(self):
        for field, value in (("phase", "Closed"), ("completionPermission", True), ("terminationObserved", True), ("sanitizationVerified", True)):
            packet = copy.deepcopy(self.termination)
            packet["syntheticWitness"][field] = value
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, "cannot authorize"):
                check_termination(packet)
        packet = copy.deepcopy(self.termination)
        packet["installedIndependentVerifier"] = "proposed-is-not-installed"
        with self.assertRaisesRegex(ValueError, "Missing installed verifier"):
            check_termination(packet)

    def test_synthetic_availability_physical_and_key_claims_are_rejected(self):
        for original, checker in ((self.transport, check_transport), (self.termination, check_termination)):
            for flag in ("DependencyAvailable", "LiveReady", "KeyDeliveryEnabled", "CompletionPermission", "PhysicalEvidence"):
                for value in (True, None, 0):
                    packet = copy.deepcopy(original); packet[flag] = value
                    with self.subTest(flag=flag, value=value), self.assertRaisesRegex(ValueError, "cannot claim"):
                        checker(packet)

    def test_one_body_setting_missing_metadata_header_units_or_backend_claim_cannot_qualify_stack(self):
        for branch, field, value in (("limits", "componentReceiveBytes", 4194304), ("limits", "responseWireBytesExclusive", 4194304),
                                     ("headers", "pinnedGrpcHeaderListBytes", 65536), ("headers", "deploymentMeasuredBytes", 65536),
                                     ("forwardedMetadata", "installedEnforcement", "proposed"), ("headerPatch", "sha256", "0" * 64)):
            packet = copy.deepcopy(self.transport); packet[branch][field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                check_transport(packet)
        for field in ("installedComponentArtifact", "backendProfile"):
            packet = copy.deepcopy(self.transport); packet[field] = "synthetic"
            with self.assertRaises(ValueError):
                check_transport(packet)

    def test_sdk_capture_cannot_substitute_bytes_call_count_or_sidecar_claim(self):
        for field, value in (("DataBytes", 1), ("SdkCalls", 2), ("RealSidecarPathExecuted", True), ("RealSidecarPathExecuted", 0)):
            packet = copy.deepcopy(self.transport)
            packet["syntheticSdkCaptures"]["RetainedMaximumManifest"][field] = value
            with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                check_transport(packet)

    def test_source_and_dependency_pins_and_outstanding_proof_are_required(self):
        for change in ("sourceManifestSha256", "sourcePins", "dependencies", "outstandingProof"):
            packet = copy.deepcopy(self.transport); packet[change] = None if change != "outstandingProof" else []
            with self.subTest(change=change), self.assertRaises(ValueError):
                check_transport(packet)

    def test_retry_full_response_backend_and_capture_summary_prerequisites_are_pinned(self):
        for field in ("retry", "completeResponseEnforcement"):
            for remove in (False, True):
                packet = copy.deepcopy(self.transport)
                if remove:
                    packet["proposedSettings"].pop(field)
                else:
                    packet["proposedSettings"][field] = "unqualified replacement"
                with self.subTest(field=field, remove=remove), self.assertRaises(ValueError):
                    check_transport(packet)
        packet = copy.deepcopy(self.transport); packet.pop("backendRequirements")
        with self.assertRaises(ValueError):
            check_transport(packet)
        for field in self.transport["backendRequirements"]:
            for remove in (False, True):
                packet = copy.deepcopy(self.transport)
                if remove:
                    packet["backendRequirements"].pop(field)
                else:
                    packet["backendRequirements"][field] = 1 if isinstance(packet["backendRequirements"][field], int) else "unqualified"
                with self.subTest(field=field, remove=remove), self.assertRaises(ValueError):
                    check_transport(packet)
        for field in set(self.transport["syntheticSdkCaptures"]) - {"RetainedMaximumManifest", "MaximumPendingTransition"}:
            for remove in (False, True):
                packet = copy.deepcopy(self.transport)
                if remove:
                    packet["syntheticSdkCaptures"].pop(field)
                else:
                    value = packet["syntheticSdkCaptures"][field]
                    packet["syntheticSdkCaptures"][field] = not value if type(value) is bool else value + 1
                with self.subTest(field=field, remove=remove), self.assertRaises(ValueError):
                    check_transport(packet)

    def test_coherent_patch_path_hash_or_content_replacement_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            shutil.copy2(PACKETS / "research-source-manifest.json", root)
            approved_path = self.transport["headerPatch"]["path"]
            replacement = b"unrelated replacement patch\n"
            replacement_hash = hashlib.sha256(replacement).hexdigest()
            for path, digest in (("unrelated.patch", replacement_hash), (approved_path, replacement_hash),
                                 (approved_path, self.transport["headerPatch"]["sha256"])):
                packet = copy.deepcopy(self.transport)
                (root / path).write_bytes(replacement)
                packet["headerPatch"].update(path=path, sha256=digest)
                with self.subTest(path=path, digest=digest), self.assertRaisesRegex(ValueError, "Header patch"):
                    check_transport(packet, root)

    @staticmethod
    def replace_fixture_hash(packet):
        witness = packet["syntheticWitness"]
        fixture = {key: witness[key] for key in ("originalIdentity", "admissionInventory", "queueInventory", "admissionHighWatermark")}
        witness["originalSyntheticFixtureSha256"] = hashlib.sha256(json.dumps(fixture, sort_keys=True, separators=(",", ":")).encode()).hexdigest()

    def test_coordinated_old_sidecar_drop_and_lineage_relabel_cannot_replace_original_fixture(self):
        packet = copy.deepcopy(self.termination); witness = packet["syntheticWitness"]
        for inventory in ("admissionInventory", "observedInventory"):
            witness[inventory] = [h for h in witness[inventory] if h["holderId"] != "synthetic-daprd-grpc-go"]
        self.replace_fixture_hash(packet)
        with self.assertRaises(ValueError):
            check_termination(packet)
        # Consistent, structurally valid copy ID relabeling must still fail the immutable pin.
        packet = copy.deepcopy(self.termination); witness = packet["syntheticWitness"]
        for inventory in ("admissionInventory", "observedInventory"):
            for holder in witness[inventory]:
                lineage = holder["copyLineage"]
                lineage["copyId"] = "replacement-" + lineage["copyId"]
                if lineage["parentCopyId"] is not None:
                    lineage["parentCopyId"] = "replacement-" + lineage["parentCopyId"]
        self.replace_fixture_hash(packet)
        with self.assertRaisesRegex(ValueError, "Immutable original synthetic fixture"):
            check_termination(packet)

    def test_copy_parent_relationships_are_concrete_original_and_acyclic(self):
        for field, value in (("parentHolderId", "missing-holder"), ("parentCopyId", "unrelated-copy"),
                             ("parentHolderId", "synthetic-caller-owned"), ("admissionSequence", 1)):
            packet = copy.deepcopy(self.termination)
            for inventory in ("admissionInventory", "observedInventory"):
                packet["syntheticWitness"][inventory][0]["copyLineage"][field] = value
            self.replace_fixture_hash(packet)
            with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                check_termination(packet)

    def test_every_original_queue_including_both_sidecars_needs_exact_seal(self):
        for queue in self.termination["syntheticWitness"]["queueInventory"]:
            packet = copy.deepcopy(self.termination)
            witness = packet["syntheticWitness"]
            witness["queueSeals"] = [q for q in witness["queueSeals"] if q["queueId"] != queue["queueId"]]
            with self.subTest(queue=queue["queueId"]), self.assertRaises(ValueError):
                check_termination(packet)
            # Dropping the admitted queue and its seal together cannot create a new baseline.
            witness["queueInventory"] = [q for q in witness["queueInventory"] if q["queueId"] != queue["queueId"]]
            self.replace_fixture_hash(packet)
            with self.assertRaisesRegex(ValueError, "Immutable original synthetic fixture"):
                check_termination(packet)
        for field in ("queueId", "processIncarnation", "transportIncarnation", "hostIncarnation"):
            for coordinated in (False, True):
                packet = copy.deepcopy(self.termination)
                collections = ("queueSeals", "queueInventory") if coordinated else ("queueSeals",)
                for collection in collections:
                    queue = packet["syntheticWitness"][collection][1]
                    if field == "queueId":
                        queue[field] = "replacement-queue"
                    else:
                        queue["participants"][0][field] = "replacement-incarnation"
                self.replace_fixture_hash(packet)
                with self.subTest(field=field, coordinated=coordinated), self.assertRaises(ValueError):
                    check_termination(packet)

    def test_original_scope_generations_and_digest_are_bound_across_inventory_and_seals(self):
        fields = ("tenantId", "authorityIncarnation", "rootGeneration", "sourceGeneration", "destinationGeneration")
        for field in fields:
            for collection in ("admissionInventory", "observedInventory", "queueInventory", "queueSeals"):
                packet = copy.deepcopy(self.termination)
                packet["syntheticWitness"][collection][0]["originalIdentity"][field] = "substituted"
                with self.subTest(field=field, collection=collection), self.assertRaises(ValueError):
                    check_termination(packet)
        for field, value in [(f, "replacement") for f in fields] + [("requestDigest", d) for d in ("not-a-digest", "G" * 64, "A" * 63)]:
            packet = copy.deepcopy(self.termination); witness = packet["syntheticWitness"]
            witness["originalIdentity"][field] = value
            for collection in ("admissionInventory", "observedInventory", "queueInventory", "queueSeals"):
                for entry in witness[collection]:
                    entry["originalIdentity"][field] = value
            self.replace_fixture_hash(packet)
            with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                check_termination(packet)

    def test_missing_verifier_profile_or_capacity_disclosure_is_not_explicit_null(self):
        for field in ("installedIndependentVerifier", "acceptedTerminationProfile", "measuredCapacity"):
            packet = copy.deepcopy(self.termination)
            (packet["admissionCapacity"] if field == "measuredCapacity" else packet).pop(field)
            with self.subTest(field=field), self.assertRaises(ValueError):
                check_termination(packet)


if __name__ == "__main__":
    unittest.main()
