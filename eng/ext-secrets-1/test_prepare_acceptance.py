import hashlib
from pathlib import Path
import tempfile
import unittest

import prepare_acceptance as prepare
import qualify as q
from test_qualify import Fixture


class AcceptancePreparationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.fixture = Fixture(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def test_detached_packet_pins_real_anchors_without_accepting_candidate(self):
        output = Path(self.temp.name) / "packet"
        result = prepare.prepare(self.fixture.root, self.fixture.path, output)
        packet = q.read_json(output / "replacement-acceptance-packet.json")
        self.assertTrue(result["packetPrepared"])
        self.assertEqual(self.fixture.target, packet["candidateCommit"])
        self.assertIsNone(packet["acceptedReplacementCommit"])
        self.assertFalse(packet["dependencyAvailable"])
        self.assertIn("OwnerAcceptedTargetRuntimeQualificationBindingAndExactCommand", result["missingInputs"])
        for role in ("executor", "observer", "enrollment"):
            data = Path(q.get(self.fixture.config, f"qualification.{role}.publicAnchorFile")).read_bytes()
            self.assertEqual(hashlib.sha256(data).hexdigest(), packet[f"{role}AnchorSha256"])
        self.assertEqual([], q.replacement_packet_inputs(packet, self.fixture.config, self.fixture.target, self.fixture.target, self.fixture.binding))
        self.assertIn(str(output), packet["exactCommand"])
        self.assertFalse((self.fixture.root / "eng/ext-secrets-1/replacement-acceptance-packet.json").exists())

    def test_dirty_checkout_yields_only_blocked_report(self):
        (self.fixture.root / "uncommitted").write_text("changed")
        output = Path(self.temp.name) / "dirty"
        result = prepare.prepare(self.fixture.root, self.fixture.path, output)
        self.assertIn("CleanImmutableCandidateCheckout", result["missingInputs"])
        self.assertFalse((output / "replacement-acceptance-packet.json").exists())
        self.assertEqual(0, q.read_json(output / "blocked-inputs.json")["qualificationEffects"])

    def test_candidate_cannot_contain_its_packet_or_overwrite_existing_packet(self):
        with self.assertRaises(ValueError):
            prepare.prepare(self.fixture.root, self.fixture.path, self.fixture.root / "packet")
        output = Path(self.temp.name) / "packet"
        prepare.prepare(self.fixture.root, self.fixture.path, output)
        with self.assertRaises(ValueError):
            prepare.prepare(self.fixture.root, self.fixture.path, output)

    def test_null_or_substituted_anchor_never_clears_readiness(self):
        output = Path(self.temp.name) / "packet"
        prepare.prepare(self.fixture.root, self.fixture.path, output)
        packet = q.read_json(output / "replacement-acceptance-packet.json")
        packet["observerAnchorSha256"] = "a" * 64
        anchors = {f"{r}AnchorSha256": packet[f"{r}AnchorSha256"] for r in ("executor", "observer", "enrollment")}
        packet["publicAnchorSha256"] = q.digest(anchors)
        self.assertIn("PinnedPublicAnchorSha256", q.replacement_packet_inputs(packet, self.fixture.config, self.fixture.target, self.fixture.target, self.fixture.binding))

    def test_missing_reference_and_key_are_reported_without_transport(self):
        self.fixture.config["qualification"]["observer"]["publicAnchorFile"] = None
        self.fixture.path.write_bytes(q.canonical(self.fixture.config))
        output = Path(self.temp.name) / "packet"
        report = prepare.prepare(self.fixture.root, self.fixture.path, output)
        self.assertIn("qualification.observer.installedP256PublicAnchor", report["missingInputs"])
        self.assertEqual([], self.fixture.calls)


if __name__ == "__main__":
    unittest.main()
