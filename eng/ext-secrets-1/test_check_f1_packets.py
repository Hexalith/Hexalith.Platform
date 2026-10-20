"""Adversarial source packet checks; no installed/native/physical evidence is generated."""
import copy
import json
from pathlib import Path
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


if __name__ == "__main__":
    unittest.main()
