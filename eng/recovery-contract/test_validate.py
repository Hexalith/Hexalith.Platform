"""Synthetic, offline protocol conformance tests; no recovery is executed."""

from copy import deepcopy
from decimal import Decimal
from fractions import Fraction
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

from jsonschema import Draft202012Validator, FormatChecker

import validate


ROOT = Path(__file__).resolve().parent
FIXTURES = ROOT / "fixtures"


def lowercase_timestamps(value):
    if isinstance(value, dict):
        for key, item in value.items():
            if key in ("validatedAt", "issuedAt", "expiresAt", "invokedAt", "completedAt"):
                value[key] = item.lower()
            else:
                lowercase_timestamps(item)
    elif isinstance(value, list):
        for item in value:
            lowercase_timestamps(item)


class ContractTests(unittest.TestCase):
    def setUp(self):
        self.chain = validate.load_json(FIXTURES / "valid-chain.json")
        self.context = validate.load_json(FIXTURES / "expected-context.json")
        self.commit = validate.load_json(FIXTURES / "expected-commit-context.json")

    def check(self):
        return validate.validate_chain(self.chain, self.context, self.commit)

    def refused(self, code):
        with self.assertRaises(validate.Refusal) as caught:
            self.check()
        self.assertEqual(code, caught.exception.code)

    def pair(self, module="keys", hook="external-effect-reconciliation"):
        invocation = next(i for i in self.chain["invocations"]
                          if i["moduleId"] == module and i["hookId"] == hook)
        result = next(r for r in self.chain["results"] if r["invocationId"] == invocation["invocationId"])
        return invocation, result

    def truncate(self, invocation):
        index = self.chain["invocations"].index(invocation)
        self.chain["invocations"] = self.chain["invocations"][:index + 1]
        retained = {i["invocationId"] for i in self.chain["invocations"]}
        self.chain["results"] = [r for r in self.chain["results"] if r["invocationId"] in retained]

    def inplace(self):
        for context in (self.context, self.commit):
            context.update(recoveryMode="in-place", survivingAuthorityReissue="not-applicable")
            context.pop("priorIssuerGenerationId")
            context["issuer"].update(origin="current-environment", keySource="live")
        fence_tasks = {i["taskId"] for i in self.chain["invocations"] if i["hookId"] == "fence-custody"}
        self.chain["invocations"] = [i for i in self.chain["invocations"] if i["taskId"] not in fence_tasks]
        self.chain["results"] = [r for r in self.chain["results"] if r["taskId"] not in fence_tasks]
        for record in self.chain["invocations"] + self.chain["results"]:
            record["recoveryMode"] = "in-place"
        for invocation in self.chain["invocations"]:
            invocation["dependsOn"] = [d for d in invocation["dependsOn"] if d not in fence_tasks]

    def test_schema_self_validation(self):
        Draft202012Validator.check_schema(validate.load_json(validate.SCHEMA))

    def test_required_date_time_checker_is_installed(self):
        self.assertIn("date-time", FormatChecker().checkers)

    def test_missing_date_time_checker_fails_closed(self):
        checker = FormatChecker()
        checker.checkers.pop("date-time")
        with patch("validate.FormatChecker", return_value=checker):
            with self.assertRaises(validate.Refusal) as caught:
                validate.schema_validator()
        self.assertEqual("date-time-validator-unavailable", caught.exception.code)

    def test_schema_rejects_invalid_timestamps_at_every_boundary(self):
        examples = (("expected-context.json", ("validatedAt",)),
                    ("invocation.json", ("invokedAt",)),
                    ("invocation.json", ("authorization", "issuedAt")),
                    ("invocation.json", ("authorization", "expiresAt")),
                    ("result.json", ("completedAt",)))
        for filename, path in examples:
            for invalid in ("SYNTHETIC-SENSITIVE-TIMESTAMP", "2026-02-30T12:00:00Z",
                            "2026-10-07T25:00:00Z", "2026-10-07T12:00:00"):
                with self.subTest(filename=filename, path=path, invalid=invalid):
                    record = validate.load_json(FIXTURES / filename)
                    container = record
                    for key in path[:-1]:
                        container = container[key]
                    container[path[-1]] = invalid
                    with self.assertRaises(validate.Refusal) as caught:
                        validate.validate_shape(record)
                    self.assertEqual("invalid-shape", caught.exception.code)

    def test_lowercase_rfc3339_timestamps_are_normalized(self):
        for record in (self.chain, self.context, self.commit):
            lowercase_timestamps(record)
        self.assertEqual("complete", self.check()["outcome"])
        lower = validate.timestamp("2026-10-07t12:00:00z")
        offset = validate.timestamp("2026-10-07T14:00:00+02:00")
        self.assertEqual(lower, offset)
        self.assertIsInstance(lower, Fraction)

    def test_timestamp_preserves_arbitrary_fractional_precision(self):
        before = validate.timestamp("2026-10-07t12:00:02.0000000000000000001z")
        after = validate.timestamp("2026-10-07T14:00:02.0000000000000000009+02:00")
        self.assertEqual(Fraction(8, 10 ** 19), after - before)

    def test_timestamp_parse_errors_are_sanitized_refusals(self):
        for invalid in ("SYNTHETIC-SENSITIVE-TIMESTAMP", "2026-10-07T12:00:00"):
            with self.subTest(invalid=invalid):
                with self.assertRaises(validate.Refusal) as caught:
                    validate.timestamp(invalid)
                self.assertEqual("invalid-timestamp", caught.exception.code)

    def test_schema_record_variants(self):
        for name in ("declaration.json", "invocation.json", "result.json", "expected-context.json",
                     "valid-chain.json", "incomplete-chain.json", "failed-chain.json"):
            with self.subTest(name=name):
                validate.validate_shape(validate.load_json(FIXTURES / name))

    def test_valid_chain(self):
        self.assertEqual({"conformant": True, "outcome": "complete", "reopeningEligible": True,
                          "runtimeQualified": False}, self.check())

    def test_incomplete_chain_preserves_outcome(self):
        self.chain = validate.load_json(FIXTURES / "incomplete-chain.json")
        self.assertEqual("incomplete", self.check()["outcome"])
        self.assertFalse(self.check()["reopeningEligible"])

    def test_failed_integrity_preserves_outcome(self):
        self.chain = validate.load_json(FIXTURES / "failed-chain.json")
        self.assertEqual("failed", self.check()["outcome"])
        self.assertFalse(self.check()["reopeningEligible"])

    def test_unknown_shape_and_unsupported_version(self):
        for record in (validate.load_json(FIXTURES / "invalid-shape.json"),
                       dict(self.context, contractVersion="2.0.0")):
            with self.subTest(record_type=record["recordType"]):
                with self.assertRaises(validate.Refusal):
                    validate.validate_shape(record)

    def test_duplicate_json_keys_and_nonfinite_numbers(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "input.json"
            for content, code in (('{"a":1,"a":2}', "duplicate-json-key"),
                                  ('{"a":NaN}', "nonfinite-json-number"),
                                  ('{"a":1e999}', "nonfinite-json-number"),
                                  ('{"a":', "invalid-json")):
                with self.subTest(content=content):
                    path.write_text(content)
                    with self.assertRaises(validate.Refusal) as caught:
                        validate.load_json(path)
                    self.assertEqual(code, caught.exception.code)

    def test_json_loader_preserves_fractional_epoch_precision(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "input.json"
            path.write_text('{"epoch":7.000000000000000001,"integral":7.000000000000000000}')
            loaded = validate.load_json(path)
            self.assertEqual(Decimal("7.000000000000000001"), loaded["epoch"])
            self.assertNotEqual(7, loaded["epoch"])
            self.assertEqual(7, loaded["integral"])
            path.write_text(json.dumps(self.chain).replace('"epoch": 7', '"epoch": 7.000000000000000001', 1))
            self.chain = validate.load_json(path)
            self.refused("invalid-shape")

    def test_schema_refuses_newline_in_ids_and_digests(self):
        for field in ("moduleId", "releaseId"):
            with self.subTest(field=field):
                declaration = validate.load_json(FIXTURES / "declaration.json")
                declaration[field] += "\n"
                with self.assertRaises(validate.Refusal) as caught:
                    validate.validate_shape(declaration)
                self.assertEqual("invalid-shape", caught.exception.code)

    def test_unknown_fields_at_every_wire_boundary(self):
        for field in ("authorization", "execution", "cut"):
            with self.subTest(field=field):
                chain = deepcopy(self.chain)
                chain["invocations"][0][field]["unknown"] = "synthetic"
                with self.assertRaises(validate.Refusal):
                    validate.validate_shape(chain)

    def test_missing_cut_class_and_named_positions(self):
        for kind in ("eventstore-position", "memories-register-sequence", "module-owned-position"):
            with self.subTest(kind=kind):
                context = deepcopy(self.context)
                context["cut"]["positions"] = [p for p in context["cut"]["positions"] if p["kind"] != kind]
                with self.assertRaises(validate.Refusal):
                    validate.validate_shape(context)

    def test_duplicate_ids(self):
        for key in ("declarations", "invocations", "results"):
            with self.subTest(key=key):
                chain = deepcopy(self.chain)
                chain[key].append(deepcopy(chain[key][0]))
                with self.assertRaises(validate.Refusal):
                    validate.validate_chain(chain, self.context, self.commit)

    def test_duplicate_hook_ids(self):
        self.chain["declarations"][0]["hooks"].append(deepcopy(self.chain["declarations"][0]["hooks"][0]))
        self.refused("duplicate-hook-id")

    def test_duplicate_position_ids(self):
        for context in (self.context, self.commit):
            context["cut"]["positions"].append(deepcopy(context["cut"]["positions"][0]))
        self.refused("duplicate-position-id")

    def test_missing_declared_position(self):
        self.chain["declarations"][0]["positionDefinitions"][0]["scope"] = "another-scope"
        self.refused("position-definition-mismatch")

    def test_wrong_builtin_position_owner(self):
        for context in (self.context, self.commit):
            context["cut"]["positions"][0]["ownerModule"] = "keys"
        self.refused("invalid-shape")

    def test_schema_constrains_builtin_position_owners(self):
        for module, index in (("eventstore", 0), ("memories", 1)):
            with self.subTest(module=module):
                declaration = deepcopy(self.chain["declarations"][index])
                declaration["positionDefinitions"][0]["ownerModule"] = "keys"
                context = deepcopy(self.context)
                context["cut"]["positions"][index]["ownerModule"] = "keys"
                for record in (declaration, context):
                    with self.assertRaises(validate.Refusal) as caught:
                        validate.validate_shape(record)
                    self.assertEqual("invalid-shape", caught.exception.code)

    def test_module_dependency_cycle_and_unknown_dependency(self):
        self.chain["declarations"][0]["dependencies"] = ["keys"]
        self.refused("module-dependency-cycle")
        self.chain["declarations"][0]["dependencies"] = ["absent"]
        self.refused("unknown-module-dependency")

    def test_task_dependency_cycle(self):
        first, second = self.chain["invocations"][:2]
        first["dependsOn"] = [second["taskId"]]
        self.refused("task-dependency-cycle")

    def test_missing_phase_and_same_phase_dependency(self):
        invocation, _ = self.pair()
        invocation["dependsOn"] = []
        self.refused("missing-phase-dependency")

    def test_missing_same_phase_dependency_preserves_earlier_phase_edges(self):
        invocation, _ = self.pair(module="memories", hook="admission-purge")
        earlier_phase = [dep for dep in invocation["dependsOn"] if dep != "eventstore-admission-purge"]
        self.assertIn("keys-fence-custody", earlier_phase)
        invocation["dependsOn"] = earlier_phase
        self.refused("missing-phase-dependency")

    def test_dependency_must_finish_before_invocation(self):
        self.chain["invocations"][1]["invokedAt"] = self.chain["invocations"][0]["invokedAt"]
        self.refused("dependency-order-mismatch")

    def test_dependency_order_uses_full_fractional_precision(self):
        self.chain["results"][0]["completedAt"] = "2026-10-07T12:00:02.0000009Z"
        self.chain["invocations"][1]["invokedAt"] = "2026-10-07T12:00:02.0000001Z"
        self.refused("dependency-order-mismatch")

    def test_independent_authority_mismatch(self):
        for key, value in (("environmentId", "other-environment"), ("instanceId", "other-instance"),
                           ("attemptId", "other-attempt"), ("epoch", 8), ("recoveryMode", "in-place"),
                           ("releaseId", "sha256:" + "2" * 64)):
            with self.subTest(key=key):
                chain = deepcopy(self.chain)
                chain["invocations"][0][key] = value
                with self.assertRaises(validate.Refusal) as caught:
                    validate.validate_chain(chain, self.context, self.commit)
                self.assertEqual("context-binding-mismatch", caught.exception.code)

    def test_epoch_takeover_between_invocation_and_commit(self):
        self.commit = validate.load_json(FIXTURES / "takeover-context.json")
        self.refused("commit-context-changed")

    def test_changed_commit_issuer_and_principal(self):
        self.commit["recoveryPrincipal"] = "other-principal"
        self.refused("commit-authority-changed")

    def test_changed_prior_issuer_generation_is_refused(self):
        self.commit["priorIssuerGenerationId"] = "another-prior-issuer"
        self.refused("commit-authority-changed")

    def test_cut_bindings(self):
        self.chain["invocations"][0]["cut"]["cutId"] = "other-cut"
        self.refused("cut-binding-mismatch")

    def test_result_identity_bindings(self):
        _, result = self.pair()
        result["taskId"] = "other-task"
        self.refused("result-binding-mismatch")

    def test_result_cut_bindings(self):
        _, result = self.pair()
        result["cut"]["positions"][0]["value"] = "other-opaque-position"
        self.refused("cut-binding-mismatch")

    def test_target_coverage(self):
        invocation, _ = self.pair()
        invocation["targetPositions"][0]["value"] = "wrong-position"
        self.refused("target-position-mismatch")

    def test_noncomparable_positions(self):
        for key, value in (("scope", "unrelated-register"), ("encoding", "unrelated-v9"),
                           ("recoveryClass", "rebuild-only")):
            with self.subTest(key=key):
                chain = deepcopy(self.chain)
                chain["results"][-1]["achievedPositions"][0]["position"][key] = value
                with self.assertRaises(validate.Refusal) as caught:
                    validate.validate_chain(chain, self.context, self.commit)
                self.assertEqual("noncomparable-position", caught.exception.code)

    def test_positions_are_never_numerically_compared(self):
        # "9001" and "partition:a/offset:450" belong to different encodings and scopes.
        self.assertEqual("complete", self.check()["outcome"])
        _, result = self.pair()
        result["achievedPositions"][0]["position"]["value"] = "999999999999999999999999"
        self.refused("achieved-target-mismatch")

    def test_unreached_position_cannot_succeed(self):
        _, result = self.pair()
        result["achievedPositions"][0]["targetReached"] = False
        self.refused("success-with-unreached-position")
        result.update(status="incomplete", reasonCode="synthetic-position-pending", reopeningEligible=False)
        result["achievedPositions"][0]["position"]["value"] = "owner-opaque-earlier-position"
        self.assertEqual("incomplete", self.check()["outcome"])

    def test_missing_implementation_is_unqualified(self):
        self.chain = validate.load_json(FIXTURES / "incomplete-chain.json")
        self.chain["declarations"][0]["hooks"][0].update(implementation="missing", reasonCode="not-built")
        self.assertEqual("unqualified", self.check()["outcome"])
        self.assertFalse(self.check()["reopeningEligible"])

    def test_unimplemented_hook_cannot_be_invoked(self):
        self.chain["declarations"][0]["hooks"][0].update(implementation="missing", reasonCode="not-built")
        self.refused("hook-not-implemented")

    def test_unknown_external_effect_cannot_succeed(self):
        _, result = self.pair()
        result["externalEffects"] = {"status": "unknown"}
        self.refused("success-with-unknown-external-effect")
        result.update(status="incomplete", reasonCode="synthetic-effect-unknown", reopeningEligible=False)
        self.assertEqual("incomplete", self.check()["outcome"])

    def test_required_integrity_cannot_be_skipped(self):
        _, result = self.pair(hook="integrity")
        result["integrity"] = {"status": "not-run"}
        self.refused("integrity-not-passed")

    def test_replay_blocks_success_without_exception(self):
        invocation, result = self.pair(module="memories", hook="rebuild")
        result["replay"]["status"] = "pending"
        self.refused("pending-replay-gates-not-satisfied")
        self.truncate(invocation)
        result.update(status="incomplete", reasonCode="synthetic-replay-pending", reopeningEligible=False)
        self.assertEqual("incomplete", self.check()["outcome"])

    def test_declared_replay_exception(self):
        self.chain["declarations"][1]["reopenBeforeReplay"] = True
        for result in self.chain["results"]:
            if result["moduleId"] == "memories":
                result["replay"]["status"] = "pending"
        self.assertTrue(self.check()["reopeningEligible"])

    def test_pending_replay_requires_gates_even_when_eligibility_is_false(self):
        self.chain["declarations"][1]["reopenBeforeReplay"] = True
        _, result = self.pair(module="memories", hook="rebuild")
        result["replay"]["status"] = "pending"
        result["reopeningEligible"] = False
        for incomplete_gates in (("integrity",), ("externalEffects",),
                                 ("integrity", "externalEffects")):
            with self.subTest(incomplete_gates=incomplete_gates):
                chain = deepcopy(self.chain)
                candidate = next(r for r in chain["results"] if r["invocationId"] == result["invocationId"])
                for gate in incomplete_gates:
                    candidate[gate] = {"status": "not-run"}
                with self.assertRaises(validate.Refusal) as caught:
                    validate.validate_chain(chain, self.context, self.commit)
                self.assertEqual("pending-replay-gates-not-satisfied", caught.exception.code)

    def test_unsafe_reopening_claim(self):
        _, result = self.pair()
        result["replay"]["status"] = "not-run"
        self.refused("rebuild-replay-incomplete")

    def test_final_gate_cannot_hide_incomplete_integrity(self):
        _, result = self.pair()
        result["integrity"] = {"status": "not-run"}
        result["reopeningEligible"] = False
        self.refused("final-integrity-not-passed")

    def test_final_gate_cannot_hide_incomplete_replay(self):
        _, result = self.pair()
        result["replay"]["status"] = "pending"
        result["reopeningEligible"] = False
        self.refused("pending-replay-gates-not-satisfied")

    def test_reissue_pending_blocks_reopening(self):
        self.commit["survivingAuthorityReissue"] = "pending"
        self.assertEqual("complete", self.check()["outcome"])
        self.assertFalse(self.check()["reopeningEligible"])

    def test_credential_bindings_and_fresh_issuer(self):
        self.chain["invocations"][0]["authorization"]["epoch"] = 8
        self.refused("credential-binding-mismatch")
        self.chain["invocations"][0]["authorization"]["epoch"] = 7
        for context in (self.context, self.commit):
            context["issuer"]["generationId"] = context["priorIssuerGenerationId"]
        self.refused("issuer-not-fresh")

    def test_restored_issuer_is_forbidden_in_dr(self):
        self.context["issuer"]["keySource"] = "live"
        self.refused("invalid-shape")

    def test_credential_expiry_at_admission_and_commit(self):
        invocation, result = self.pair()
        invocation["authorization"]["expiresAt"] = invocation["invokedAt"]
        self.refused("credential-time-invalid")
        invocation["authorization"]["expiresAt"] = result["completedAt"]
        self.refused("credential-expired-at-commit")

    def test_credential_issuance_uses_full_fractional_precision(self):
        invocation, _ = self.pair()
        whole = invocation["invokedAt"][:-1]
        invocation["invokedAt"] = whole + ".0000001Z"
        invocation["authorization"]["issuedAt"] = whole + ".0000009Z"
        self.refused("credential-time-invalid")

    def test_wrong_recovery_principal(self):
        self.chain["invocations"][0]["authorization"]["principal"] = "ordinary-user"
        self.refused("credential-authority-mismatch")

    def test_wrong_credential_audience(self):
        self.chain["invocations"][0]["authorization"]["audience"] = "another-audience"
        self.refused("credential-authority-mismatch")

    def test_fence_sandbox_and_custodian_authority(self):
        self.chain["invocations"][0]["execution"]["authorityId"] = "unrelated-authority"
        self.refused("fence-execution-mismatch")

    def test_ordinary_workload_execution(self):
        invocation, _ = self.pair()
        invocation["execution"]["workloadId"] = "unrelated-workload"
        self.refused("workload-mismatch")

    def test_internal_endpoint(self):
        self.chain["invocations"][0]["endpoint"] = "/public/recovery"
        self.refused("invalid-shape")

    def test_schema_only_requires_fixed_internal_endpoint(self):
        invocation = validate.load_json(FIXTURES / "invocation.json")
        invocation["endpoint"] = "/public/recovery"
        with self.assertRaises(validate.Refusal) as caught:
            validate.validate_shape(invocation)
        self.assertEqual("invalid-shape", caught.exception.code)

    def test_inplace_omits_fence_and_reissue(self):
        self.inplace()
        self.assertTrue(self.check()["reopeningEligible"])

    def test_inplace_cannot_invoke_fence(self):
        for context in (self.context, self.commit):
            context.update(recoveryMode="in-place", survivingAuthorityReissue="not-applicable")
            context.pop("priorIssuerGenerationId")
            context["issuer"].update(origin="current-environment", keySource="live")
        for record in self.chain["invocations"] + self.chain["results"]:
            record["recoveryMode"] = "in-place"
        self.refused("in-place-fence-forbidden")

    def test_retry_rechecks_epoch_and_preserves_identity(self):
        invocation, result = self.pair()
        result.update(status="incomplete", reasonCode="synthetic-retryable", reopeningEligible=False)
        retry = deepcopy(invocation)
        retry.update(invocationId=invocation["invocationId"] + "-retry", retry=1,
                     retryOf=invocation["invocationId"], invokedAt="2026-10-07T12:01:00Z")
        retry_result = deepcopy(result)
        retry_result.update(invocationId=retry["invocationId"], status="succeeded", reopeningEligible=True,
                            completedAt="2026-10-07T12:01:01Z")
        self.chain["invocations"].append(retry)
        self.chain["results"].append(retry_result)
        self.assertEqual("complete", self.check()["outcome"])
        retry["dependsOn"].append("eventstore-external-effect-reconciliation")
        self.refused("retry-identity-mismatch")
        retry["dependsOn"].pop()
        retry["authorization"]["epoch"] += 1
        self.refused("credential-binding-mismatch")
        retry["authorization"]["epoch"] -= 1
        retry["retryOf"] = "unrelated-delivery"
        self.refused("retry-predecessor-mismatch")

    def test_missing_result_is_incomplete(self):
        self.chain["results"].pop()
        self.assertEqual("incomplete", self.check()["outcome"])


class CliTests(unittest.TestCase):
    def run_cli(self, *args):
        return subprocess.run([sys.executable, str(ROOT / "validate.py"), *map(str, args)],
                              text=True, capture_output=True, check=False)

    def chain_cli(self, fixture, commit="expected-commit-context.json"):
        return self.run_cli(FIXTURES / fixture, "--context", FIXTURES / "expected-context.json",
                            "--commit-context", FIXTURES / commit)

    def test_stable_success_output(self):
        result = self.chain_cli("valid-chain.json")
        self.assertEqual(0, result.returncode)
        self.assertEqual('', result.stderr)
        self.assertEqual('{"conformant":true,"outcome":"complete","reopeningEligible":true,'
                         '"runtimeQualified":false}\n', result.stdout)

    def test_matrix_outcomes(self):
        for fixture, code, outcome in (("incomplete-chain.json", 3, "incomplete"),
                                       ("failed-chain.json", 3, "failed")):
            with self.subTest(fixture=fixture):
                result = self.chain_cli(fixture)
                self.assertEqual(code, result.returncode)
                self.assertEqual(outcome, json.loads(result.stdout)["outcome"])
        result = self.chain_cli("valid-chain.json", "takeover-context.json")
        self.assertEqual(2, result.returncode)
        self.assertEqual("commit-context-changed", json.loads(result.stdout)["error"])

    def test_independent_contexts_are_required(self):
        result = self.run_cli(FIXTURES / "valid-chain.json")
        self.assertEqual(2, result.returncode)
        self.assertEqual("independent-contexts-required", json.loads(result.stdout)["error"])

    def test_shape_only_has_no_reopening_claim(self):
        result = self.run_cli(FIXTURES / "result.json", "--schema-only")
        self.assertEqual(0, result.returncode)
        self.assertEqual("shape-valid", json.loads(result.stdout)["outcome"])
        self.assertFalse(json.loads(result.stdout)["reopeningEligible"])

    def test_invalid_arguments_are_sanitized(self):
        result = self.run_cli("--unknown-sensitive-argument")
        self.assertEqual(2, result.returncode)
        self.assertEqual("invalid-arguments", json.loads(result.stdout)["error"])
        self.assertNotIn("sensitive", result.stdout + result.stderr)

    def test_malformed_timestamps_refuse_without_disclosure(self):
        marker = "SYNTHETIC-SENSITIVE-TIMESTAMP"
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "input.json"
            invocation = validate.load_json(FIXTURES / "invocation.json")
            invocation["invokedAt"] = marker
            path.write_text(json.dumps(invocation))
            shape = self.run_cli(path, "--schema-only")
            chain = validate.load_json(FIXTURES / "valid-chain.json")
            chain["invocations"][0]["invokedAt"] = marker
            path.write_text(json.dumps(chain))
            full = self.run_cli(path, "--context", FIXTURES / "expected-context.json",
                                "--commit-context", FIXTURES / "expected-commit-context.json")
            for result in (shape, full):
                self.assertEqual(2, result.returncode)
                self.assertEqual("invalid-shape", json.loads(result.stdout)["error"])
                self.assertNotIn(marker, result.stdout + result.stderr)
                self.assertEqual('', result.stderr)

    def test_lowercase_rfc3339_chain_succeeds(self):
        with tempfile.TemporaryDirectory() as directory:
            paths = []
            for name in ("valid-chain.json", "expected-context.json", "expected-commit-context.json"):
                record = validate.load_json(FIXTURES / name)
                lowercase_timestamps(record)
                path = Path(directory) / name
                path.write_text(json.dumps(record))
                paths.append(path)
            result = self.run_cli(paths[0], "--context", paths[1], "--commit-context", paths[2])
            self.assertEqual(0, result.returncode)
            self.assertEqual("complete", json.loads(result.stdout)["outcome"])
            self.assertEqual('', result.stderr)

    def test_pending_replay_cannot_bypass_its_gate_prerequisites(self):
        chain = validate.load_json(FIXTURES / "valid-chain.json")
        chain["declarations"][1]["reopenBeforeReplay"] = True
        result = next(r for r in chain["results"] if r["moduleId"] == "memories" and r["hookId"] == "rebuild")
        result.update(integrity={"status": "not-run"}, externalEffects={"status": "not-run"},
                      reopeningEligible=False)
        result["replay"]["status"] = "pending"
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "input.json"
            path.write_text(json.dumps(chain))
            report = self.run_cli(path, "--context", FIXTURES / "expected-context.json",
                                  "--commit-context", FIXTURES / "expected-commit-context.json")
            self.assertEqual(2, report.returncode)
            self.assertEqual("pending-replay-gates-not-satisfied", json.loads(report.stdout)["error"])
            self.assertFalse(json.loads(report.stdout)["reopeningEligible"])
            self.assertEqual('', report.stderr)

    def test_diagnostics_never_echo_input_or_paths(self):
        marker = "SYNTHETIC-SENSITIVE-INPUT-MARKER"
        with tempfile.TemporaryDirectory(prefix=marker) as directory:
            path = Path(directory) / "bad.json"
            for content in ('{"contractVersion":"' + marker + '"}',
                            '{"' + marker + '":1,"' + marker + '":2}',
                            '{"contractVersion":"1.0.0","recordType":"' + marker + '"}',
                            '{"' + marker + '":'):
                with self.subTest(content=content):
                    path.write_text(content)
                    result = self.run_cli(path, "--schema-only")
                    self.assertEqual(2, result.returncode)
                    self.assertNotIn(marker, result.stdout + result.stderr)
                    self.assertEqual('', result.stderr)
            result = self.run_cli(Path(directory) / "missing", "--schema-only")
            self.assertEqual(2, result.returncode)
            self.assertNotIn(marker, result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
