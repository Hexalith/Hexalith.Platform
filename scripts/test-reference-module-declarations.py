#!/usr/bin/env python3
"""Validate the three owner declarations as one atomic reference set."""

from __future__ import annotations

import copy
import json
import subprocess
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BUILDS = ROOT / "references/Hexalith.Builds"
CLI = BUILDS / "src/libraries/Hexalith.Builds.Module.Cli/Hexalith.Builds.Module.Cli.csproj"
PATHS = [ROOT / f"references/Hexalith.{name}/module/hexalith.module-manifest.v2.json"
         for name in ("EventStore", "Memories", "Tenants")]
APPROVED = {
    "memories-falkordb-graph",
    "memories-redis-search-vector",
    "memories-set-nx-preflight-dedup",
    "memories-redis-coordination",
}


def policy_diagnostics(paths: list[Path]) -> list[dict[str, str]]:
    """Apply the approved Memories exception inventory beyond the v2 schema enum."""
    diagnostics = []
    for path in paths:
        for index, module in enumerate(json.loads(path.read_text())["modules"]):
            if module["identity"]["moduleId"] != "memories":
                continue
            exceptions = module["runtime"].get("providerExceptions", [])
            names = [item["name"] for item in exceptions]
            for position, item in enumerate(exceptions):
                field = f"modules[{index}].runtime.providerExceptions[{position}]"
                if item["name"] not in APPROVED:
                    diagnostics.append(dict(source=str(path), field=f"{field}.name"))
                if item["name"] == "memories-redis-coordination" and not item["transitional"]:
                    diagnostics.append(dict(source=str(path), field=f"{field}.transitional"))
                if item["name"] != "memories-redis-coordination" and item["transitional"]:
                    diagnostics.append(dict(source=str(path), field=f"{field}.transitional"))
            if len(exceptions) != 4 or set(names) != APPROVED or len(set(names)) != len(names):
                diagnostics.append(dict(source=str(path), field=f"modules[{index}].runtime.providerExceptions"))
    return diagnostics


def validate(paths: list[Path]) -> tuple[bool, list[dict[str, str]]]:
    command = ["dotnet", "run", "--no-build", "--project", str(CLI), "--", "validate"]
    for path in paths:
        command.extend(("--manifest", str(path)))
    command.extend(("--output", "json"))
    result = subprocess.run(command, cwd=BUILDS, capture_output=True, text=True, check=False)
    try:
        report = json.loads(result.stdout)
    except json.JSONDecodeError as error:
        raise AssertionError(f"Validator did not return JSON: {result.stdout} {result.stderr}") from error
    diagnostics = report["diagnostics"] + policy_diagnostics(paths)
    accepted = result.returncode == 0 and report["status"] == "validated" and not diagnostics
    return accepted, diagnostics


def inputs(module: dict) -> set[str]:
    return {item["key"] for extension in module["runtime"]["extensions"]
            for item in extension["configurationInputs"]}


def inspect_inventory(documents: list[dict]) -> None:
    modules = [document["modules"][0] for document in documents]
    by_name = {module["identity"]["moduleId"]: module for module in modules}
    assert set(by_name) == {"eventstore", "memories", "tenants"}
    assert len({server["appId"] for module in modules for server in module["identity"]["servers"]}) == 6
    assert len({server["resourceId"] for module in modules for server in module["identity"]["servers"]}) == 6
    assert by_name["eventstore"]["identity"]["dependencies"] == []
    assert by_name["memories"]["identity"]["dependencies"] == ["eventstore"]
    assert by_name["tenants"]["identity"]["dependencies"] == ["eventstore"]
    for module in modules:
        assert module["lifecycle"]["tasks"] == []
        ready = {item["server"] for item in module["lifecycle"]["readiness"]
                 if item["kind"] == "http" and item["endpoint"] == "/ready"}
        assert ready == {server["id"] for server in module["identity"]["servers"] if server["required"]}
        assert all({"role", "capability", "recoveryClass", "configurationKey"} <= role.keys()
                   for role in module["runtime"]["dapr"])
    memories = by_name["memories"]
    assert {server["appId"] for server in memories["identity"]["servers"]} == {
        "memories", "memories-access-telemetry", "memories-access-telemetry-clock"}
    assert {"Memories:LlmComponentPath", "AccessTelemetryLifecycle:StateStoreComponentPath",
            "AccessTelemetryLifecycle:ConfigurationStoreComponentPath", "Clock:SecretStoreName",
            "Clock:SigningKeySecretName", "Clock:Sources", "Clock:AllowDevelopmentSources",
            "AccessTelemetryLifecycle:MarkerKeyReference", "Embedding:CredentialNamespace",
            "NaturalLanguage:RetryWorkerEnabled", "ConnectionStrings:redis",
            "ConnectionStrings:falkordb"} <= inputs(memories)
    assert {"clock-signing-key", "telemetry-marker-key"} <= {
        item["name"] for item in memories["integration"]["secrets"]}
    assert {"embedding-credentials"} <= {
        item["name"] for item in memories["integration"]["dynamicSecretNamespaces"]}
    assert {"embedding-retry", "telemetry-delivery", "telemetry-heartbeat",
            "telemetry-retention"} <= {item["name"] for item in memories["integration"]["workers"]}
    assert not any(interface["protocol"] == "mcp" for interface in memories["surfaces"]["interfaces"])
    tenants = by_name["tenants"]
    assert {"Tenants:DaprConfigPath", "Oidc:Authority", "Jwt:SigningKey"} <= inputs(tenants)
    assert {"/api/tenants", "/api/global-administrators", "/api/users/{userId}/tenants"} <= {
        interface["routePrefix"] for interface in tenants["surfaces"]["interfaces"]}
    assert {"process", "query", "project", "replay-state"} <= {
        operation["name"] for interface in tenants["surfaces"]["interfaces"]
        for operation in interface["operations"]}


def main() -> None:
    documents = [json.loads(path.read_text()) for path in PATHS]
    inspect_inventory(documents)
    accepted, diagnostics = validate(PATHS)
    assert accepted, diagnostics
    with tempfile.TemporaryDirectory(prefix="hexalith-declarations-") as directory:
        temp = Path(directory)
        for scenario, mutate, field in (
            ("duplicate identity", lambda docs: docs[2]["modules"][0]["identity"].update(moduleId="memories"), "identity.moduleId"),
            ("absent readiness", lambda docs: docs[0]["modules"][0]["lifecycle"].update(readiness=[]), "lifecycle.readiness"),
            ("unapproved exception", lambda docs: docs[1]["modules"][0]["runtime"]["providerExceptions"][0].update(name="unapproved-provider"), "providerExceptions[0].name"),
            ("fifth exception", lambda docs: docs[1]["modules"][0]["runtime"]["providerExceptions"].append(copy.deepcopy(docs[1]["modules"][0]["runtime"]["providerExceptions"][0])), "runtime.providerExceptions"),
            ("unmarked coordination", lambda docs: docs[1]["modules"][0]["runtime"]["providerExceptions"][3].update(transitional=False), "providerExceptions[3].transitional"),
        ):
            variant = copy.deepcopy(documents)
            mutate(variant)
            variant_paths = []
            for index, document in enumerate(variant):
                path = temp / f"{scenario.replace(' ', '-')}-{index}.json"
                path.write_text(json.dumps(document))
                variant_paths.append(path)
            accepted, diagnostics = validate(variant_paths)
            assert not accepted, f"{scenario} yielded a partial declaration set"
            assert any(Path(item.get("source", "")).name in {path.name for path in variant_paths}
                       and field in item.get("field", "")
                       for item in diagnostics), f"{scenario}: {diagnostics}"
            print(f"PASS {scenario}: atomic refusal with source and field")
    print("PASS joint declaration set, readiness, routes, inventories and empty tasks")


if __name__ == "__main__":
    main()
