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

    def test_generated_v2_packet_round_trips_preflight_with_quoted_metacharacter_paths(self):
        output = Path(self.temp.name) / "packet directory;$(never-run)'quoted"
        eventstore = "../event store;$(never-run)"
        artifacts = "/tmp/artifacts space'quoted;$()"
        evidence = "/tmp/evidence space`quoted`"
        prepare.prepare(self.fixture.root, self.fixture.path, output, eventstore, artifacts, evidence)
        packet = q.read_json(output / "replacement-acceptance-packet.json")
        self.fixture.write_register()
        text = self.fixture.register.read_text()
        original = text.split("Command: `", 1)[1].split("` |", 1)[0]
        self.fixture.register.write_text(text.replace(original, packet["exactCommand"]))
        config, target, binding = q.preflight(self.fixture.root, output / "runtime-reference.json", self.fixture.register,
            self.fixture.agents, eventstore, artifacts, evidence, output / "replacement-acceptance-packet.json")
        self.assertEqual(self.fixture.target, target)
        self.assertEqual(self.fixture.config, config)
        self.assertEqual(self.fixture.binding, binding)
        self.assertTrue(packet["exactCommand"].endswith(q.command_suffix(eventstore, artifacts, evidence)))
        self.assertEqual([], self.fixture.calls)

    def test_unknown_packet_schemas_never_use_legacy_acceptance(self):
        self.fixture.preflight()
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

    def test_relative_anchor_is_pinned_absolutely_before_agents_working_directory_change(self):
        self.fixture.config["qualification"]["executor"]["publicAnchorFile"] = "executor.public.pem"
        self.fixture.path.write_bytes(q.canonical(self.fixture.config))
        output = Path(self.temp.name) / "relative-anchor-packet"
        prepare.prepare(self.fixture.root, self.fixture.path, output, "../eventstore", "/tmp/artifacts", "/tmp/evidence")
        copied = q.read_json(output / "runtime-reference.json")
        self.assertEqual(str(Path(self.temp.name) / "executor.public.pem"), copied["qualification"]["executor"]["publicAnchorFile"])
        packet = q.read_json(output / "replacement-acceptance-packet.json")
        text = self.fixture.register.read_text()
        original = text.split("Command: `", 1)[1].split("` |", 1)[0]
        self.fixture.register.write_text(text.replace(original, packet["exactCommand"]))
        previous = Path.cwd()
        try:
            os.chdir(self.fixture.agents)
            actual, _, _ = q.preflight(self.fixture.root, output / "runtime-reference.json", self.fixture.register, self.fixture.agents,
                "../eventstore", "/tmp/artifacts", "/tmp/evidence", output / "replacement-acceptance-packet.json")
        finally:
            os.chdir(previous)
        self.assertEqual(copied, actual)
        self.assertEqual(q.digest(copied), packet["configurationSha256"])

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
