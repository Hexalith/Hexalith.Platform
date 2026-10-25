import copy
import hashlib
import os
import subprocess
import time
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import prepare_acceptance as prepare
import qualify as q
from test_qualify import Fixture


class AcceptancePreparationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.fixture = Fixture(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def assert_only_cohort_refusal(self, invoke):
        with self.assertRaises(q.Unavailable) as caught:
            invoke()
        self.assertEqual(set(q.IMPLEMENTATIONS) | {"OQ-5.4-1.AuthoritativeCohortRecord", "OQ-5.4-1.JointLivePhaseRunner"},
                         set(caught.exception.inputs))

    def test_missing_installation_refuses_complete_packet_without_accepting_candidate(self):
        output = Path(self.temp.name) / "packet"
        result = prepare.prepare(self.fixture.root, self.fixture.path, output)
        self.assertFalse(result["packetPrepared"])
        self.assertFalse(result["completeReplacementPacket"])
        self.assertIsNone(result["targetCommit"])
        self.assertIn("ExplicitCoordinatedInstallationContext", result["missingInputs"])
        self.assertFalse((output / "replacement-acceptance-packet.json").exists())
        self.assertEqual([], self.fixture.calls)

    def test_dirty_checkout_yields_only_blocked_report(self):
        (self.fixture.root / "uncommitted").write_text("changed")
        output = Path(self.temp.name) / "dirty"
        result = prepare.prepare(self.fixture.root, self.fixture.path, output)
        self.assertIn("CleanImmutableCandidateCheckout", result["missingInputs"])
        self.assertFalse((output / "replacement-acceptance-packet.json").exists())
        self.assertEqual(0, q.read_json(output / "blocked-inputs.json")["qualificationEffects"])

    def test_packet_retains_authoritative_cohort_blockers_and_register_hash(self):
        with self.fixture.register.open("a") as stream:
            stream.write("\n### OQ-5.4-1 — Bounded Owner-Qualification Cohort\n")
        output = Path(self.temp.name) / "cohort-packet"
        report = prepare.prepare(self.fixture.root, self.fixture.path, output, register=self.fixture.register)
        self.assertFalse(report["packetPrepared"])
        self.assertIn("OQ-5.4-1.IndependentCurrentCohortBindingAndOwners", report["missingInputs"])
        self.assertIn("OQ-5.4-1.CommittedProtectionOwner", report["missingInputs"])
        self.assertFalse((output / "replacement-acceptance-packet.json").exists())
        self.assertEqual([], self.fixture.calls)

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
        packet = q.read_json(self.fixture.packet_path)
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

    def test_rejected_configuration_only_writes_sanitized_zero_effect_report(self):
        private_marker = "DO-NOT-RETAIN-PRIVATE-VALUE"
        shapes = [[], 42, None, {"runtime": {"privateKey": private_marker}},
                  self.fixture.config | {"unknownSecretField": private_marker}]
        wrong = copy.deepcopy(self.fixture.config)
        wrong["runtime"]["callTimeout"] = private_marker
        shapes.append(wrong)
        for index, value in enumerate(shapes):
            with self.subTest(index=index):
                path = Path(self.temp.name) / f"invalid-{index}.json"
                path.write_bytes(q.canonical(value))
                output = Path(self.temp.name) / f"blocked-{index}"
                report = prepare.prepare(self.fixture.root, path, output)
                self.assertFalse(report["packetPrepared"])
                self.assertEqual(0, report["qualificationEffects"])
                self.assertEqual(["ValidPublicRuntimeReferenceConfiguration"], report["missingInputs"])
                self.assertEqual(["blocked-inputs.json"], [p.name for p in output.iterdir()])
                self.assertNotIn(private_marker, (output / "blocked-inputs.json").read_text())
                self.assertNotIn("privateKey", (output / "blocked-inputs.json").read_text())

    def test_missing_malformed_duplicate_or_nonobject_config_blocks_without_values(self):
        for index, content in enumerate((None, b'{"runtime":', b'{"schema":1,"schema":2}', b'"sensitive-value"')):
            with self.subTest(index=index):
                path = Path(self.temp.name) / f"broken-{index}.json"
                if content is not None:
                    path.write_bytes(content)
                output = Path(self.temp.name) / f"blocked-broken-{index}"
                report = prepare.prepare(self.fixture.root, path, output)
                self.assertEqual(0, report["qualificationEffects"])
                self.assertFalse(report["packetPrepared"])
                self.assertEqual(["blocked-inputs.json"], [p.name for p in output.iterdir()])

    def test_quoted_paths_do_not_bypass_missing_installed_context(self):
        output = Path(self.temp.name) / "packet directory;$(never-run)'quoted"
        report = prepare.prepare(self.fixture.root, self.fixture.path, output,
            "../event store;$(never-run)", "/tmp/artifacts space'quoted;$()", "/tmp/evidence space`quoted`")
        self.assertFalse(report["packetPrepared"])
        self.assertEqual(["blocked-inputs.json"], [item.name for item in output.iterdir()])
        self.assertEqual([], self.fixture.calls)

    def test_unknown_packet_schemas_never_use_legacy_acceptance(self):
        self.assert_only_cohort_refusal(self.fixture.preflight)
        packet = q.read_json(self.fixture.packet_path)
        for schema in (None, "EXT-SECRETS-1.replacement-acceptance.v3", "unexpected"):
            with self.subTest(schema=schema):
                changed = dict(packet, schema=schema)
                self.fixture.packet_path.write_bytes(q.canonical(changed))
                with self.assertRaises(q.ReplacementAcceptancePacket):
                    self.fixture.preflight()
        self.assertEqual([], self.fixture.calls)

    def test_empty_packet_never_clears_preflight(self):
        self.fixture.packet_path.write_bytes(q.canonical({}))
        with self.assertRaises(q.Unavailable) as caught:
            self.fixture.preflight()
        self.assertIn("DetachedImmutableCandidateAcceptancePacket", caught.exception.inputs)
        self.assertEqual([], self.fixture.calls)

    def test_preflight_rejects_secret_bearing_configuration_without_exposing_values(self):
        self.fixture.config["runtime"]["privateKey"] = "PRIVATE-MARKER"
        self.fixture.path.write_bytes(q.canonical(self.fixture.config))
        with self.assertRaises(q.Unavailable) as caught:
            self.fixture.preflight()
        self.assertIn("ValidPublicRuntimeReferenceConfiguration", caught.exception.inputs)
        self.assertNotIn("PRIVATE-MARKER", str(caught.exception))
        self.assertEqual([], self.fixture.calls)

    def test_anchor_files_are_bounded_and_special_files_do_not_block(self):
        oversize = Path(self.temp.name) / "oversized.pem"
        oversize.write_bytes(b"a" * (4096 + 1))
        fifo = Path(self.temp.name) / "public-anchor.fifo"
        os.mkfifo(fifo)
        for path in (oversize, fifo):
            with self.subTest(path=path.name):
                started = time.monotonic()
                with self.assertRaises(ValueError):
                    q.read_public_anchor(path)
                self.assertLess(time.monotonic() - started, 1)

    def test_embedded_public_host_schema_matches_complete_canonical_plan(self):
        expected = {"planSha256": q.plan_digest(), "cases": [{"id": case.id,
                    "metrics": sorted(set(q.GLOBAL) | set(case.expected)), "outcomes": list(case.outcomes), "boundary": case.boundary} for case in q.plan()]}
        self.assertEqual(expected, q.read_json(Path(__file__).parent / "public-case-schemas.json"))

    def test_relative_anchor_does_not_create_an_installed_packet(self):
        self.fixture.config["qualification"]["executor"]["publicAnchorFile"] = "executor.public.pem"
        self.fixture.path.write_bytes(q.canonical(self.fixture.config))
        output = Path(self.temp.name) / "relative-anchor-packet"
        report = prepare.prepare(self.fixture.root, self.fixture.path, output)
        self.assertFalse(report["packetPrepared"])
        self.assertFalse((output / "runtime-reference.json").exists())
        self.assertEqual([], self.fixture.calls)

    def test_preparation_reuses_all_named_transport_and_role_version_blockers(self):
        self.fixture.config["qualification"].update(daprEndpoint="http://foreign-host:3500", requestTimeoutSeconds=0,
            daprApiCredentialReference="file:/missing-PRIVATE-MARKER/api-token")
        for role in ("executor", "observer", "enrollment"):
            self.fixture.config["qualification"][role]["version"] = "invalid$version"
        self.fixture.path.write_bytes(q.canonical(self.fixture.config))
        output = Path(self.temp.name) / "transport-blocked"
        report = prepare.prepare(self.fixture.root, self.fixture.path, output)
        expected = {"LocalEnrolledDaprSidecar", "BoundedQualificationCalls", "MountedQualificationDaprCredential"} | {
            f"qualification.{role}.version" for role in ("executor", "observer", "enrollment")}
        self.assertTrue(expected <= set(report["missingInputs"]))
        with self.assertRaises(q.Unavailable) as caught:
            self.fixture.preflight()
        self.assertTrue(expected <= set(caught.exception.inputs))
        self.assertEqual(0, report["qualificationEffects"])
        self.assertNotIn("PRIVATE-MARKER", (output / "blocked-inputs.json").read_text())
        self.assertEqual([], self.fixture.calls)

    def test_git_failures_only_publish_sanitized_zero_effect_candidate_blockers(self):
        failures = [(FileNotFoundError("PRIVATE-MARKER"), "CandidateGitExecutable"),
                    (subprocess.CalledProcessError(128, "PRIVATE-MARKER"), "ExactImmutableCandidateCheckout"),
                    (subprocess.TimeoutExpired("PRIVATE-MARKER", 10), "BoundedCandidateGitInspection")]
        for index, (failure, expected) in enumerate(failures):
            with self.subTest(expected=expected), patch.object(q, "git", side_effect=failure):
                output = Path(self.temp.name) / f"git-blocked-{index}"
                report = prepare.prepare(self.fixture.root, self.fixture.path, output)
                self.assertFalse(report["packetPrepared"])
                self.assertEqual(0, report["qualificationEffects"])
                self.assertIn(expected, report["missingInputs"])
                self.assertEqual(["blocked-inputs.json"], [path.name for path in output.iterdir()])
                self.assertNotIn("PRIVATE-MARKER", (output / "blocked-inputs.json").read_text())


if __name__ == "__main__":
    unittest.main()
