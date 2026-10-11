#!/usr/bin/env python3
"""Validate the three owner declarations as one atomic reference set."""

from __future__ import annotations

import copy
import json
import re
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
ACL = ROOT / "references/Hexalith.EventStore/deploy/dapr/accesscontrol.yaml"


def policy_diagnostics(paths: list[Path]) -> list[dict[str, str]]:
    """Apply the approved reference inventory beyond the v2 schema."""
    diagnostics = []
    for path in paths:
        for index, module in enumerate(json.loads(path.read_text())["modules"]):
            module_id = module["identity"]["moduleId"]
            expected_dependencies = {"eventstore": [], "memories": ["eventstore"],
                                     "tenants": ["eventstore"]}.get(module_id)
            if expected_dependencies is not None and module["identity"].get("dependencies") != expected_dependencies:
                diagnostics.append(dict(source=str(path), field=f"modules[{index}].identity.dependencies"))
            if module_id != "memories":
                continue
            exceptions = module["runtime"].get("providerExceptions", [])
            names = [item.get("name") for item in exceptions]
            for position, item in enumerate(exceptions):
                field = f"modules[{index}].runtime.providerExceptions[{position}]"
                name = item.get("name")
                transitional = item.get("transitional")
                if name not in APPROVED:
                    diagnostics.append(dict(source=str(path), field=f"{field}.name"))
                if transitional is None:
                    diagnostics.append(dict(source=str(path), field=f"{field}.transitional"))
                elif name == "memories-redis-coordination" and not transitional:
                    diagnostics.append(dict(source=str(path), field=f"{field}.transitional"))
                elif name != "memories-redis-coordination" and transitional:
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


def input_details(module: dict) -> dict[str, tuple[str, str]]:
    return {item["key"]: (item["source"], item["reference"])
            for extension in module["runtime"]["extensions"]
            for item in extension["configurationInputs"]}


def inspect_acl() -> None:
    """Check the production EventStore sidecar keeps caller-specific default deny rules."""
    content = ACL.read_text()
    assert "defaultAction: deny" in content
    caller_blocks = dict(re.findall(r"^      - appId: ([\w-]+)\n(.*?)(?=^      - appId: |\Z)",
                                    content, flags=re.MULTILINE | re.DOTALL))
    domain = caller_blocks["tenants"]
    assert "defaultAction: deny" in domain
    assert re.findall(r"^          - name: (\S+)\n            httpVerb: (\[[^\n]+\])\n            action: allow$",
                      domain, flags=re.MULTILINE) == [("/api/v1/commands", "['POST']")]
    api = caller_blocks["tenants-api"]
    assert {("/ready", "['GET']"), ("/api/v1/queries", "['POST']"),
            ("/api/v1/commands", "['POST']")} <= set(re.findall(
                r"^          - name: (\S+)\n            httpVerb: (\[[^\n]+\])\n            action: allow$",
                api, flags=re.MULTILINE))


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
    eventstore = by_name["eventstore"]
    assert {"EventStore:PubSubComponentPath", "EventStore:AppChannelToken",
            "Authentication:JwtBearer:Authority", "Authentication:JwtBearer:Issuer",
            "Authentication:JwtBearer:Audience", "Authentication:JwtBearer:ValidAudiences",
            "Authentication:JwtBearer:AllowedAlgorithms", "Authentication:JwtBearer:SigningKey",
            "Authentication:JwtBearer:RequireHttpsMetadata",
            "Authentication:JwtBearer:AllowInsecureSymmetricKey"} <= inputs(eventstore)
    assert {"app-channel-token", "gateway-jwt-signing-key"} <= {
        item["name"] for item in eventstore["integration"]["secrets"]}
    assert input_details(eventstore)["EventStore:AppChannelToken"] == ("secret", "app-channel-token")
    memories = by_name["memories"]
    assert {server["appId"] for server in memories["identity"]["servers"]} == {
        "memories", "memories-access-telemetry", "memories-access-telemetry-clock"}
    assert {"Memories:LlmComponentPath", "AccessTelemetryLifecycle:StateStoreComponentPath",
            "AccessTelemetryLifecycle:ConfigurationStoreComponentPath", "Clock:SecretStoreName",
            "Clock:SigningKeySecretName", "Clock:Sources", "Clock:AllowDevelopmentSources",
            "AccessTelemetryLifecycle:MarkerKeyReference", "Embedding:CredentialNamespace",
            "NaturalLanguage:RetryWorkerEnabled", "ConnectionStrings:redis",
            "ConnectionStrings:falkordb", "AccessTelemetryLifecycle:DeploymentId",
            "AccessTelemetryLifecycle:ConfigurationEpoch", "AccessTelemetryLifecycle:ComponentProfileHash",
            "AccessTelemetryLifecycle:AttestationVerificationKey", "AccessTelemetryLifecycle:MarkerKeyGeneration",
            "AccessTelemetryLifecycle:CapacityEvidenceId", "AccessTelemetryLifecycle:PhysicalReclamationEvidenceId",
            "AccessTelemetryLifecycle:PhysicalReclamationReporterImageDigest",
            "AccessTelemetryLifecycle:RetentionSource", "retentionSeconds",
            "AccessTelemetryLifecycle:ClockSignerKeyEpoch", "Clock:SignerKeyEpoch",
            "Clock:SourceAuthenticationTokens", "TenantEventRouting:AutoProvisionRoutedTenants",
            "Conversation:ProviderApiKey", "AccessTelemetryLifecycle:Enabled"} <= inputs(memories)
    assert input_details(memories)["retentionSeconds"] == ("configuration", "access-telemetry-config")
    assert {"clock-signing-key", "telemetry-marker-key", "conversation-provider-api-key"} <= {
        item["name"] for item in memories["integration"]["secrets"]}
    assert {"embedding-credentials", "clock-source-authentication-tokens"} <= {
        item["name"] for item in memories["integration"]["dynamicSecretNamespaces"]}
    assert input_details(memories)["Clock:SourceAuthenticationTokens"] == (
        "dynamic-secret", "clock-source-authentication-tokens")
    assert input_details(memories)["Conversation:ProviderApiKey"] == (
        "secret", "conversation-provider-api-key")
    assert any(item == {"name": "authenticated-utc", "protocol": "https",
                        "destination": "Clock:Sources:<index>:Endpoint"}
               for item in memories["integration"]["egress"])
    assert {"embedding-retry", "telemetry-delivery", "telemetry-heartbeat",
            "telemetry-retention"} <= {item["name"] for item in memories["integration"]["workers"]}
    assert not any(interface["protocol"] == "mcp" for interface in memories["surfaces"]["interfaces"])
    tenants = by_name["tenants"]
    assert {"Tenants:DaprConfigPath", "Oidc:Authority", "Jwt:SigningKey",
            "Tenants:BootstrapGlobalAdminEnabled", "Tenants:BootstrapGlobalAdminUserId",
            "EventStore:Authentication:Authority", "EventStore:Authentication:ClientId",
            "EventStore:Authentication:Username", "EventStore:Authentication:Password",
            "EventStore:DataProtection:PersistToStateStore", "Tenants:AppChannelToken",
            "Authentication:JwtBearer:Authority", "Authentication:JwtBearer:Issuer",
            "Authentication:JwtBearer:Audience", "Authentication:JwtBearer:SigningKey",
            "Authentication:JwtBearer:ValidAudiences", "Authentication:JwtBearer:AllowedAlgorithms",
            "Authentication:JwtBearer:RequireHttpsMetadata", "Authentication:JwtBearer:AllowInsecureSymmetricKey",
            "Authentication:Workload:Audience", "Authentication:Workload:AllowedCallers",
            "Authentication:Workload:MaximumLifetimeSeconds", "EventStore:Authentication:Issuer",
            "EventStore:Authentication:Audience", "EventStore:Authentication:RequireHttpsMetadata"} <= inputs(tenants)
    assert {"bootstrap-delegated-password", "domain-app-channel-token"} <= {
        item["name"] for item in tenants["integration"]["secrets"]}
    assert input_details(tenants)["EventStore:Authentication:Password"] == (
        "secret", "bootstrap-delegated-password")
    assert input_details(tenants)["Tenants:AppChannelToken"] == (
        "secret", "domain-app-channel-token")
    assert any(item == {"name": "bootstrap-oidc-token", "protocol": "oidc",
                        "destination": "EventStore:Authentication:Authority/protocol/openid-connect/token"}
               for item in tenants["integration"]["egress"])
    assert any(item == {"name": "global-administrator-bootstrap", "kind": "external-effect",
                        "disableControl": {"configurationKey": "Tenants:BootstrapGlobalAdminEnabled",
                                           "disabledValue": False}}
               for item in tenants["integration"]["workers"])
    assert {"/api/tenants", "/api/global-administrators", "/api/users/{userId}/tenants"} <= {
        interface["routePrefix"] for interface in tenants["surfaces"]["interfaces"]}
    assert {"process", "query", "project", "replay-state"} <= {
        operation["name"] for interface in tenants["surfaces"]["interfaces"]
        for operation in interface["operations"]}


def main() -> None:
    documents = [json.loads(path.read_text()) for path in PATHS]
    inspect_inventory(documents)
    inspect_acl()
    build = subprocess.run(["dotnet", "build", str(CLI)], cwd=BUILDS,
                           capture_output=True, text=True, check=False)
    assert build.returncode == 0, f"Validator build failed: {build.stdout} {build.stderr}"
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
            ("missing exception name", lambda docs: docs[1]["modules"][0]["runtime"]["providerExceptions"][0].pop("name"), "providerExceptions[0].name"),
            ("missing exception transitional", lambda docs: docs[1]["modules"][0]["runtime"]["providerExceptions"][3].pop("transitional"), "providerExceptions[3].transitional"),
            ("invented domain dependency", lambda docs: docs[1]["modules"][0]["identity"]["dependencies"].append("parties"), "identity.dependencies"),
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
