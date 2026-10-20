#!/usr/bin/env python3
"""Offline conformance checks; never an authenticator or recovery executor."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
from decimal import Decimal, InvalidOperation
from fractions import Fraction
import json
import math
from pathlib import Path
import re
import sys

from jsonschema import Draft202012Validator, FormatChecker


VERSION = "1.0.0"
SCHEMA = Path(__file__).resolve().parents[2] / "schemas/hexalith.recovery-hook.v1.json"
PHASES = ("fence", "admission-purge", "credentials", "rebuild", "integrity",
          "external-effect-reconciliation")
BINDINGS = ("contractVersion", "releaseId", "environmentId", "instanceId", "attemptId",
            "attemptKind", "epoch", "recoveryMode")
DESCRIPTOR = ("positionId", "ownerModule", "scope", "kind", "recoveryClass", "encoding")


class Refusal(Exception):
    """Codes are constants: never interpolate untrusted input into diagnostics."""

    def __init__(self, code: str):
        self.code = code
        super().__init__(code)


class SafeArgumentParser(argparse.ArgumentParser):
    def error(self, message):
        raise Refusal("invalid-arguments")


def require(condition, code):
    if not condition:
        raise Refusal(code)


def _object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "duplicate-json-key")
        result[key] = value
    return result


def _constant(_value):
    raise Refusal("nonfinite-json-number")


def _float(value):
    try:
        number = Decimal(value)
        require(number.is_finite() and math.isfinite(number), "nonfinite-json-number")
        return int(number) if number == number.to_integral_value() else number
    except InvalidOperation:
        raise Refusal("nonfinite-json-number") from None


def load_json(path):
    try:
        return json.loads(Path(path).read_text(encoding="utf-8"),
                          object_pairs_hook=_object, parse_constant=_constant, parse_float=_float)
    except (OSError, UnicodeError):
        raise Refusal("unreadable-json") from None
    except (ValueError, RecursionError):
        raise Refusal("invalid-json") from None


def schema_validator():
    schema = load_json(SCHEMA)
    Draft202012Validator.check_schema(schema)
    checker = FormatChecker()
    require("date-time" in checker.checkers, "date-time-validator-unavailable")
    return Draft202012Validator(schema, format_checker=checker)


def validate_shape(record, validator=None):
    require(isinstance(record, dict), "invalid-shape")
    require(record.get("contractVersion") == VERSION, "unsupported-version")
    validator = validator or schema_validator()
    # Library messages include property names and instance values, so never emit them.
    require(next(validator.iter_errors(record), None) is None, "invalid-shape")


def indexed(items, key, code="duplicate-id"):
    result = {}
    for item in items:
        require(item[key] not in result, code)
        result[item[key]] = item
    return result


def positions(items):
    result = indexed(items, "positionId", "duplicate-position-id")
    scopes = [tuple(p[k] for k in DESCRIPTOR[1:]) for p in items]
    require(len(scopes) == len(set(scopes)), "duplicate-position-scope")
    for p in items:
        if p["kind"] == "eventstore-position":
            require(p["ownerModule"] == "eventstore", "position-owner-mismatch")
        if p["kind"] == "memories-register-sequence":
            require(p["ownerModule"] == "memories", "position-owner-mismatch")
    return result


def same_cut(left, right):
    return (left["cutId"] == right["cutId"]
            and positions(left["positions"]) == positions(right["positions"]))


def bind(record, context):
    require(all(record[k] == context[k] for k in BINDINGS), "context-binding-mismatch")
    require(same_cut(record["cut"], context["cut"]), "cut-binding-mismatch")


def timestamp(value):
    try:
        parts = re.fullmatch(r"([0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2})"
                             r"(?:\.([0-9]+))?(Z|[+-][0-9]{2}:[0-9]{2})", value.upper())
        require(parts is not None, "invalid-timestamp")
        parsed = datetime.fromisoformat(parts[1] + parts[3].replace("Z", "+00:00"))
        elapsed = parsed.astimezone(timezone.utc) - datetime(1970, 1, 1, tzinfo=timezone.utc)
        fraction = Fraction(Decimal("0." + parts[2])) if parts[2] else Fraction(0)
        return Fraction(elapsed.days * 86400 + elapsed.seconds) + fraction
    except (ValueError, TypeError, OverflowError):
        raise Refusal("invalid-timestamp") from None


def validate_context(context):
    require(context["recordType"] == "context", "expected-context")
    positions(context["cut"]["positions"])
    if context["recoveryMode"] == "disaster-recovery":
        require(context["issuer"]["generationId"] != context["priorIssuerGenerationId"],
                "issuer-not-fresh")
    quarantine = context["quarantine"]
    sources = ([quarantine["executorId"]] + quarantine["recoveryWorkloadIds"]
               + quarantine["probeSourceIds"])
    require(len(sources) == len(set(sources)), "ambiguous-quarantine-source")


def acyclic(graph, code):
    remaining = {key: set(value) for key, value in graph.items()}
    while remaining:
        ready = {key for key, deps in remaining.items() if not deps}
        require(ready, code)
        remaining = {key: deps - ready for key, deps in remaining.items() if key not in ready}


def declarations_for(chain, context):
    declarations = indexed(chain["declarations"], "moduleId", "duplicate-module-id")
    cut = positions(context["cut"]["positions"])
    declared_positions = {}
    for module, declaration in declarations.items():
        require(declaration["releaseId"] == context["releaseId"], "release-binding-mismatch")
        require(all(dep in declarations and dep != module for dep in declaration["dependencies"]),
                "unknown-module-dependency")
        definitions = positions(declaration["positionDefinitions"])
        for name, definition in definitions.items():
            require(definition["ownerModule"] == module, "position-owner-mismatch")
            require(name not in declared_positions, "duplicate-position-id")
            require(name in cut and all(definition[k] == cut[name][k] for k in DESCRIPTOR),
                    "position-definition-mismatch")
            declared_positions[name] = definition
        hooks = indexed(declaration["hooks"], "hookId", "duplicate-hook-id")
        ordinary = [h["phase"] for h in hooks.values() if h["phase"] != "fence"]
        require(sorted(ordinary) == sorted(PHASES[1:]), "phase-coverage-mismatch")
        require(all(h["implementation"] != "not-applicable" for h in hooks.values()
                    if h["phase"] in ("integrity", "external-effect-reconciliation", "fence")),
                "required-hook-not-applicable")
        authorities = indexed(declaration["survivingAuthorities"], "authorityId",
                              "duplicate-authority-id")
        fences = [h for h in hooks.values() if h["phase"] == "fence"]
        require(len(fences) == len(authorities), "fence-coverage-mismatch")
        for authority in authorities.values():
            hook = hooks.get(authority["fenceHookId"])
            require(hook is not None and hook["phase"] == "fence"
                    and hook["authorityId"] == authority["authorityId"], "fence-binding-mismatch")
    require(set(declared_positions) == set(cut), "position-coverage-mismatch")
    acyclic({m: d["dependencies"] for m, d in declarations.items()}, "module-dependency-cycle")
    return declarations


def validate_invocation(invocation, declaration, hook, context):
    bind(invocation, context)
    require(invocation["endpoint"] == declaration["endpoint"], "endpoint-mismatch")
    require(hook["implementation"] == "implemented", "hook-not-implemented")
    targets = positions(invocation["targetPositions"])
    owned = {p["positionId"]: p for p in context["cut"]["positions"]
             if p["ownerModule"] == declaration["moduleId"]}
    require(targets == owned, "target-position-mismatch")
    execution = invocation["execution"]
    require(execution["executorId"] == context["quarantine"]["executorId"], "executor-mismatch")
    if hook["phase"] == "fence":
        require(context["recoveryMode"] == "disaster-recovery", "in-place-fence-forbidden")
        require(execution["location"] == "recovery-executor-sandbox"
                and execution["authorityId"] == hook["authorityId"], "fence-execution-mismatch")
    else:
        require(execution["location"] == "owning-workload", "ordinary-execution-mismatch")
        require(declaration["workloadId"] in context["quarantine"]["recoveryWorkloadIds"]
                and execution["workloadId"] == declaration["workloadId"], "workload-mismatch")
    authorization = invocation["authorization"]
    require(all(authorization[k] == context[k] for k in
                ("environmentId", "instanceId", "attemptId", "attemptKind", "epoch")),
            "credential-binding-mismatch")
    require(authorization["principal"] == context["recoveryPrincipal"]
            and authorization["audience"] == context["audience"]
            and authorization["issuerId"] == context["issuer"]["issuerId"]
            and authorization["issuerGenerationId"] == context["issuer"]["generationId"],
            "credential-authority-mismatch")
    require(timestamp(authorization["issuedAt"]) <= timestamp(invocation["invokedAt"])
            < timestamp(authorization["expiresAt"]), "credential-time-invalid")
    require(timestamp(context["validatedAt"]) <= timestamp(invocation["invokedAt"]),
            "invocation-context-time-invalid")


def validate_result(result, invocation, declaration, hook, commit_context):
    bind(result, commit_context)
    require(all(result[k] == invocation[k] for k in BINDINGS
                + ("moduleId", "hookId", "taskId", "invocationId")), "result-binding-mismatch")
    require(same_cut(result["cut"], invocation["cut"]), "result-cut-mismatch")
    completed = timestamp(result["completedAt"])
    require(timestamp(invocation["invokedAt"]) <= completed
            <= timestamp(commit_context["validatedAt"]), "result-time-invalid")
    require(completed < timestamp(invocation["authorization"]["expiresAt"]),
            "credential-expired-at-commit")
    achieved = positions([item["position"] for item in result["achievedPositions"]])
    targets = positions(invocation["targetPositions"])
    require(set(achieved) == set(targets), "achieved-position-coverage-mismatch")
    for item in result["achievedPositions"]:
        position = item["position"]
        target = targets[position["positionId"]]
        require(all(position[k] == target[k] for k in DESCRIPTOR), "noncomparable-position")
        # Opaque encodings belong to modules; this validator never orders them.
        if item["targetReached"]:
            require(position == target, "achieved-target-mismatch")
    integrity = result["integrity"]["status"]
    external = result["externalEffects"]["status"]
    replay = result["replay"]["status"]
    reached = all(p["targetReached"] for p in result["achievedPositions"])
    if result["status"] == "succeeded":
        require(reached, "success-with-unreached-position")
        require(integrity not in ("failed", "unknown"), "success-with-uncertain-integrity")
        require(external != "unknown", "success-with-unknown-external-effect")
        if replay == "pending":
            require(declaration["reopenBeforeReplay"] and integrity == "passed"
                    and external in ("reconciled", "not-applicable"),
                    "pending-replay-gates-not-satisfied")
        if hook["phase"] == "integrity":
            require(integrity == "passed", "integrity-not-passed")
        if hook["phase"] == "external-effect-reconciliation":
            require(integrity == "passed", "final-integrity-not-passed")
            require(external in ("reconciled", "not-applicable"), "external-effects-not-reconciled")
        if hook["phase"] in ("rebuild", "external-effect-reconciliation"):
            require(replay in ("complete", "not-applicable")
                    or (replay == "pending" and declaration["reopenBeforeReplay"]),
                    "rebuild-replay-incomplete")
    eligible = (result["status"] == "succeeded" and reached and integrity == "passed"
                and external in ("reconciled", "not-applicable")
                and (replay in ("complete", "not-applicable")
                     or (replay == "pending" and declaration["reopenBeforeReplay"])))
    require(not result["reopeningEligible"] or eligible, "unsafe-reopening-claim")


def validate_chain(chain, context, commit_context):
    validator = schema_validator()
    for record in (chain, context, commit_context):
        validate_shape(record, validator)
    require(chain["recordType"] == "chain", "expected-chain")
    validate_context(context)
    validate_context(commit_context)
    # A takeover invalidates the entire chain, even if the submitted result repeats the old epoch.
    require(all(context[k] == commit_context[k] for k in BINDINGS)
            and same_cut(context["cut"], commit_context["cut"]), "commit-context-changed")
    require(context["issuer"] == commit_context["issuer"]
            and context.get("priorIssuerGenerationId") == commit_context.get("priorIssuerGenerationId")
            and context["recoveryPrincipal"] == commit_context["recoveryPrincipal"]
            and context["audience"] == commit_context["audience"]
            and context["quarantine"] == commit_context["quarantine"], "commit-authority-changed")
    require(timestamp(context["validatedAt"]) <= timestamp(commit_context["validatedAt"]),
            "context-time-invalid")
    declarations = declarations_for(chain, context)
    invocations = indexed(chain["invocations"], "invocationId", "duplicate-invocation-id")
    results = indexed(chain["results"], "invocationId", "duplicate-result-id")
    require(set(results) <= set(invocations), "result-without-invocation")
    tasks = {}
    latest = {}
    for invocation in invocations.values():
        declaration = declarations.get(invocation["moduleId"])
        require(declaration is not None, "unknown-module")
        hooks = {h["hookId"]: h for h in declaration["hooks"]}
        hook = hooks.get(invocation["hookId"])
        require(hook is not None, "unknown-hook")
        validate_invocation(invocation, declaration, hook, context)
        task = tasks.setdefault(invocation["taskId"], [])
        task.append(invocation)
        key = (invocation["moduleId"], invocation["hookId"])
        previous_task = latest.get(key)
        require(previous_task is None or previous_task == invocation["taskId"], "duplicate-hook-task")
        latest[key] = invocation["taskId"]
        if invocation["invocationId"] in results:
            validate_result(results[invocation["invocationId"]], invocation, declaration, hook,
                            commit_context)
    final = {}
    for task_id, attempts in tasks.items():
        attempts.sort(key=lambda i: i["retry"])
        require([i["retry"] for i in attempts] == list(range(len(attempts))), "invalid-retry-sequence")
        original = attempts[0]
        for previous, current in zip(attempts, attempts[1:]):
            require(current["retryOf"] == previous["invocationId"], "retry-predecessor-mismatch")
            immutable = BINDINGS + ("moduleId", "hookId", "taskId", "endpoint", "targetPositions",
                                    "dependsOn", "execution", "cut")
            require(all(current[k] == original[k] for k in immutable), "retry-identity-mismatch")
            require(previous["invocationId"] in results, "retry-before-outcome")
            prior_result = results[previous["invocationId"]]
            require(prior_result["status"] in ("failed", "incomplete"), "retry-after-terminal-outcome")
            require(timestamp(prior_result["completedAt"]) <= timestamp(current["invokedAt"]),
                    "retry-time-invalid")
        final[task_id] = attempts[-1]
    graph = {task: invocation["dependsOn"] for task, invocation in final.items()}
    require(all(dep in final and dep != task for task, deps in graph.items() for dep in deps),
            "unknown-task-dependency")
    acyclic(graph, "task-dependency-cycle")
    for invocation in invocations.values():
        for dep in invocation["dependsOn"]:
            predecessor = results.get(final[dep]["invocationId"])
            require(predecessor is not None and predecessor["status"] == "succeeded",
                    "dependency-not-succeeded")
            require(timestamp(predecessor["completedAt"]) <= timestamp(invocation["invokedAt"]),
                    "dependency-order-mismatch")
    # Every lower phase is a global barrier. Dependencies add same-phase module ordering.
    for invocation in invocations.values():
        declaration = declarations[invocation["moduleId"]]
        hook = next(h for h in declaration["hooks"] if h["hookId"] == invocation["hookId"])
        for module, other_declaration in declarations.items():
            for other_hook in other_declaration["hooks"]:
                if other_hook["implementation"] != "implemented":
                    continue
                if context["recoveryMode"] == "in-place" and other_hook["phase"] == "fence":
                    continue
                required = (PHASES.index(other_hook["phase"]) < PHASES.index(hook["phase"])
                            or (module in declaration["dependencies"]
                                and other_hook["phase"] == hook["phase"]))
                if required:
                    predecessor = latest.get((module, other_hook["hookId"]))
                    require(predecessor in invocation["dependsOn"], "missing-phase-dependency")
    statuses = [r["status"] for r in results.values()
                if final[r["taskId"]]["invocationId"] == r["invocationId"]]
    missing_implementation = any(
        h["implementation"] == "missing"
        for d in declarations.values() for h in d["hooks"]
        if not (context["recoveryMode"] == "in-place" and h["phase"] == "fence"))
    required_hooks = [(m, h["hookId"]) for m, d in declarations.items() for h in d["hooks"]
                      if h["implementation"] == "implemented"
                      and not (context["recoveryMode"] == "in-place" and h["phase"] == "fence")]
    missing_result = any(key not in latest or final[latest[key]]["invocationId"] not in results
                         for key in required_hooks)
    if missing_implementation or "unqualified" in statuses:
        outcome = "unqualified"
    elif "failed" in statuses:
        outcome = "failed"
    elif missing_result or "incomplete" in statuses:
        outcome = "incomplete"
    else:
        outcome = "complete"
    eligible = outcome == "complete" and commit_context["survivingAuthorityReissue"] != "pending"
    for module, declaration in declarations.items():
        reconciliation = next(h for h in declaration["hooks"]
                              if h["phase"] == "external-effect-reconciliation")
        key = (module, reconciliation["hookId"])
        eligible = eligible and key in latest
        if key in latest:
            terminal = results.get(final[latest[key]]["invocationId"])
            eligible = eligible and terminal is not None and terminal["reopeningEligible"]
    return {"conformant": True, "outcome": outcome, "reopeningEligible": bool(eligible),
            "runtimeQualified": False}


def main(argv=None):
    parser = SafeArgumentParser(description=__doc__)
    parser.add_argument("record", type=Path)
    parser.add_argument("--context", type=Path, help="independent invocation authority snapshot")
    parser.add_argument("--commit-context", type=Path, help="independent current commit snapshot")
    parser.add_argument("--schema-only", action="store_true", help="shape check only; no chain qualification")
    try:
        args = parser.parse_args(argv)
        record = load_json(args.record)
        if args.schema_only:
            validate_shape(record)
            report = {"conformant": True, "outcome": "shape-valid", "reopeningEligible": False,
                      "runtimeQualified": False}
        else:
            require(args.context is not None and args.commit_context is not None,
                    "independent-contexts-required")
            report = validate_chain(record, load_json(args.context), load_json(args.commit_context))
        print(json.dumps(report, sort_keys=True, separators=(",", ":")))
        return 0 if report["outcome"] in ("complete", "shape-valid") else 3
    except Refusal as exc:
        print(json.dumps({"conformant": False, "error": exc.code, "reopeningEligible": False,
                          "runtimeQualified": False}, sort_keys=True, separators=(",", ":")))
        return 2


if __name__ == "__main__":
    sys.exit(main())
