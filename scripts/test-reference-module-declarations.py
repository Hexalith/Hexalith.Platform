#!/usr/bin/env python3
"""Validate the three owner declarations as one atomic reference set."""

from __future__ import annotations

import copy
import ast
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
TENANTS_ACL = ROOT / "references/Hexalith.Tenants/deploy/dapr/accesscontrol.eventstore.yaml"
EXPECTED_DEPENDENCIES = {"eventstore": [], "memories": ["eventstore"], "tenants": ["eventstore"]}
EXPECTED_SECURITY_INVENTORY = {
    "eventstore": {
        "secrets": [
            {"name": "app-channel-token", "configurationKey": "EventStore:AppChannelToken"},
            {"name": "gateway-jwt-signing-key", "configurationKey": "Authentication:JwtBearer:SigningKey"},
            {"name": "workload-issuer-client-secret", "configurationKey": "Authentication:WorkloadIssuer:ClientSecret"},
        ],
        "egress": [
            {"name": "gateway-oidc-metadata", "protocol": "https", "destination": "Authentication:JwtBearer:Authority/.well-known/openid-configuration"},
            {"name": "gateway-oidc-jwks", "protocol": "https", "destination": "Authentication:JwtBearer:Authority -> metadata.jwks_uri"},
            {"name": "workload-issuer-explicit-token", "protocol": "https", "destination": "Authentication:WorkloadIssuer:TokenEndpoint (when configured)"},
            {"name": "workload-issuer-discovered-token", "protocol": "https", "destination": "Authentication:JwtBearer:Authority -> metadata.token_endpoint (when TokenEndpoint omitted)"},
        ],
    },
    "memories": {
        "secrets": [
            {"name": "clock-signing-key", "configurationKey": "Clock:SigningKeySecretName"},
            {"name": "telemetry-marker-key", "configurationKey": "AccessTelemetryLifecycle:MarkerKeyReference"},
            {"name": "conversation-provider-api-key", "configurationKey": "Conversation:ProviderApiKey"},
            {"name": "server-jwt-signing-key", "configurationKey": "Authentication:JwtBearer:SigningKey"},
            {"name": "app-channel-token", "configurationKey": "Memories:AppChannelToken"},
            {"name": "dapr-api-token", "configurationKey": "Memories:DaprApiToken"},
        ],
        "egress": [
            {"name": "authenticated-utc", "protocol": "https", "destination": "Clock:Sources:<index>:Endpoint"},
            {"name": "server-oidc-metadata", "protocol": "https", "destination": "Authentication:JwtBearer:Authority/.well-known/openid-configuration"},
            {"name": "server-oidc-jwks", "protocol": "https", "destination": "Authentication:JwtBearer:Authority -> metadata.jwks_uri"},
            {"name": "conversation-provider", "protocol": "https", "destination": "Memories:LlmComponentPath -> conversation.openai provider endpoint"},
            {"name": "embedding-google-provider", "protocol": "tcp", "destination": "EmbeddingProviders:Google:ApiBaseUrl"},
            {"name": "embedding-ollama-provider", "protocol": "tcp", "destination": "EmbeddingProviders:Ollama:BaseUrl"},
            {"name": "embedding-ollama-oidc-token", "protocol": "https", "destination": "EmbeddingProviders:Ollama:OidcTokenEndpoint (OIDC client-credential mode only)"},
        ],
    },
    "tenants": {
        "secrets": [
            {"name": "api-signing-key", "configurationKey": "Jwt:SigningKey"},
            {"name": "bootstrap-delegated-password", "configurationKey": "EventStore:Authentication:Password"},
            {"name": "api-eventstore-signing-key", "configurationKey": "EventStore:Authentication:SigningKey"},
            {"name": "domain-jwt-signing-key", "configurationKey": "Authentication:JwtBearer:SigningKey"},
            {"name": "domain-app-channel-token", "configurationKey": "Tenants:AppChannelToken"},
            {"name": "api-dapr-token", "configurationKey": "Tenants:ApiDaprToken"},
        ],
        "egress": [
            {"name": "bootstrap-oidc-token", "protocol": "oidc", "destination": "EventStore:Authentication:Authority/protocol/openid-connect/token"},
            {"name": "domain-oidc-metadata", "protocol": "https", "destination": "Authentication:JwtBearer:Authority/.well-known/openid-configuration"},
            {"name": "domain-oidc-jwks", "protocol": "https", "destination": "Authentication:JwtBearer:Authority -> metadata.jwks_uri"},
            {"name": "public-api-oidc-metadata", "protocol": "https", "destination": "Oidc:Authority/.well-known/openid-configuration"},
            {"name": "public-api-oidc-jwks", "protocol": "https", "destination": "Oidc:Authority -> metadata.jwks_uri"},
            {"name": "public-api-eventstore-oidc-metadata", "protocol": "https", "destination": "EventStore:Authentication:Authority/.well-known/openid-configuration"},
            {"name": "public-api-eventstore-oidc-jwks", "protocol": "https", "destination": "EventStore:Authentication:Authority -> metadata.jwks_uri"},
        ],
    },
}


def exact_records(actual: list, expected: list[dict]) -> bool:
    """Compare complete records, retaining duplicate detection while ignoring order."""
    return (isinstance(actual, list) and
            sorted(json.dumps(item, sort_keys=True) for item in actual) ==
            sorted(json.dumps(item, sort_keys=True) for item in expected))


def policy_diagnostics(paths: list[Path]) -> list[dict[str, str]]:
    """Apply the approved reference inventory beyond the v2 schema."""
    diagnostics = []
    owner_entries = []
    for path in paths:
        try:
            document = json.loads(path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, UnicodeDecodeError):
            continue  # The public validator owns malformed document diagnostics.
        if not isinstance(document, dict):
            continue  # The public validator owns root-shape diagnostics.
        modules = document.get("modules", [])
        if not isinstance(modules, list):
            continue  # The public validator reports the malformed document.
        if len(modules) != 1:
            diagnostics.append(dict(source=str(path), field="modules"))
        for index, module in enumerate(modules):
            if not isinstance(module, dict):
                continue
            identity = module.get("identity")
            if not isinstance(identity, dict):
                continue
            module_id = identity.get("moduleId")
            if not isinstance(module_id, str):
                continue
            owner_entries.append((module_id, path, index))
            expected_dependencies = EXPECTED_DEPENDENCIES.get(module_id)
            if expected_dependencies is not None and identity.get("dependencies") != expected_dependencies:
                diagnostics.append(dict(source=str(path), field=f"modules[{index}].identity.dependencies"))
            integration = module.get("integration")
            if isinstance(integration, dict) and module_id in EXPECTED_SECURITY_INVENTORY:
                for inventory, expected in EXPECTED_SECURITY_INVENTORY[module_id].items():
                    actual = integration.get(inventory)
                    if isinstance(actual, list) and not exact_records(actual, expected):
                        diagnostics.append(dict(source=str(path),
                                                field=f"modules[{index}].integration.{inventory}"))
            if module_id != "memories":
                continue
            runtime = module.get("runtime")
            if not isinstance(runtime, dict):
                continue
            exceptions = runtime.get("providerExceptions", [])
            if not isinstance(exceptions, list):
                continue
            names = []
            for position, item in enumerate(exceptions):
                field = f"modules[{index}].runtime.providerExceptions[{position}]"
                if not isinstance(item, dict):
                    diagnostics.append(dict(source=str(path), field=field))
                    continue
                name = item.get("name")
                names.append(name if isinstance(name, str) else None)
                transitional = item.get("transitional")
                if not isinstance(name, str) or name not in APPROVED:
                    diagnostics.append(dict(source=str(path), field=f"{field}.name"))
                if transitional is None:
                    diagnostics.append(dict(source=str(path), field=f"{field}.transitional"))
                elif name == "memories-redis-coordination" and not transitional:
                    diagnostics.append(dict(source=str(path), field=f"{field}.transitional"))
                elif name != "memories-redis-coordination" and transitional:
                    diagnostics.append(dict(source=str(path), field=f"{field}.transitional"))
            if len(exceptions) != 4 or set(names) != APPROVED or len(set(names)) != len(names):
                diagnostics.append(dict(source=str(path), field=f"modules[{index}].runtime.providerExceptions"))
    if len(owner_entries) == len(paths) == len(EXPECTED_DEPENDENCIES):
        owner_ids = [entry[0] for entry in owner_entries]
        if sorted(owner_ids) != sorted(EXPECTED_DEPENDENCIES):
            for module_id, path, index in owner_entries:
                if module_id not in EXPECTED_DEPENDENCIES or owner_ids.count(module_id) > 1:
                    diagnostics.append(dict(source=str(path), field=f"modules[{index}].identity.moduleId"))
    return diagnostics


def validate(paths: list[Path]) -> tuple[bool, list[dict[str, str]], list[dict[str, str]]]:
    command = ["dotnet", "run", "--no-build", "--project", str(CLI), "--", "validate"]
    for path in paths:
        command.extend(("--manifest", str(path)))
    command.extend(("--output", "json"))
    result = subprocess.run(command, cwd=BUILDS, capture_output=True, text=True, check=False)
    try:
        report = json.loads(result.stdout)
    except json.JSONDecodeError as error:
        raise AssertionError(f"Validator did not return JSON: {result.stdout} {result.stderr}") from error
    public_diagnostics = report["diagnostics"]
    diagnostics = public_diagnostics + policy_diagnostics(paths)
    accepted = result.returncode == 0 and report["status"] == "validated" and not diagnostics
    return accepted, diagnostics, public_diagnostics


def validated_documents(paths: list[Path]) -> list[dict]:
    """Keep the public source/field report authoritative before owner JSON parsing."""
    accepted, diagnostics, _ = validate(paths)
    assert accepted, diagnostics
    return [json.loads(path.read_text(encoding="utf-8")) for path in paths]


def inputs(module: dict) -> set[str]:
    return {item["key"] for extension in module["runtime"]["extensions"]
            for item in extension["configurationInputs"]}


def input_details(module: dict) -> dict[str, tuple[str, str]]:
    return {item["key"]: (item["source"], item["reference"])
            for extension in module["runtime"]["extensions"]
            for item in extension["configurationInputs"]}


def _acl_yaml_lines(content: str) -> list[tuple[int, str]]:
    """Tokenize the policy's supported YAML subset, ignoring comments outside quotes."""
    lines = []
    for raw in content.splitlines():
        assert "\t" not in raw, "ACL YAML tabs are unsupported"
        quote = None
        for position, character in enumerate(raw):
            if character in "\"'" and (position == 0 or raw[position - 1] != "\\"):
                quote = None if quote == character else character if quote is None else quote
            elif character == "#" and quote is None and (position == 0 or raw[position - 1].isspace()):
                raw = raw[:position]
                break
        if raw.strip():
            lines.append((len(raw) - len(raw.lstrip(" ")), raw.strip()))
    return lines


def _acl_pair(text: str) -> tuple[str, object]:
    key, separator, raw_value = text.partition(":")
    assert separator and re.fullmatch(r"[A-Za-z][A-Za-z0-9]*", key), f"Unsupported ACL YAML field: {text}"
    raw_value = raw_value.strip()
    if raw_value.startswith(("[", "'", '"')):
        value = ast.literal_eval(raw_value)
    else:
        assert not raw_value.startswith(("&", "*", "!", "{", "}")), f"Unsupported ACL YAML value: {text}"
        value = raw_value
    return key, value


def _acl_set(target: dict, key: str, value: object) -> None:
    assert key not in target, f"Duplicate ACL YAML field: {key}"
    target[key] = value


def parse_effective_acl(content: str) -> dict:
    """Parse the ACL's map/list YAML shape without adding a checkout dependency."""
    lines = _acl_yaml_lines(content)
    assert sum(indent == 0 and text.startswith("spec:") for indent, text in lines) == 1, "Duplicate or missing ACL spec"
    assert sum(indent == 2 and text.startswith("accessControl:") for indent, text in lines) == 1, (
        "Duplicate or missing spec.accessControl")
    assert (0, "spec:") in lines, "Unsupported ACL spec mapping"
    spec_index = lines.index((0, "spec:"))
    assert (2, "accessControl:") in lines[spec_index + 1:], "Missing spec.accessControl"
    start = lines.index((2, "accessControl:"), spec_index + 1) + 1
    acl: dict = {}
    policy = None
    operation = None
    for indent, text in lines[start:]:
        if indent <= 2:
            break
        if indent == 4:
            key, value = _acl_pair(text)
            if key == "policies":
                assert value == "", "ACL policies must be a sequence"
                value = []
            _acl_set(acl, key, value)
            policy = operation = None
        elif indent == 6:
            assert text.startswith("- ") and isinstance(acl.get("policies"), list)
            key, value = _acl_pair(text[2:])
            assert key == "appId"
            policy = {key: value}
            acl["policies"].append(policy)
            operation = None
        elif indent == 8:
            assert policy is not None
            key, value = _acl_pair(text)
            if key == "operations":
                assert value == "", "ACL operations must be a sequence"
                value = []
            _acl_set(policy, key, value)
            operation = None
        elif indent == 10:
            assert policy is not None and isinstance(policy.get("operations"), list) and text.startswith("- ")
            key, value = _acl_pair(text[2:])
            assert key == "name"
            operation = {key: value}
            policy["operations"].append(operation)
        elif indent == 12:
            assert operation is not None
            key, value = _acl_pair(text)
            _acl_set(operation, key, value)
        else:
            raise AssertionError(f"Unsupported ACL YAML indentation: {indent}: {text}")
    return acl


def inspect_acl(content: str | None = None, expected_resource_name: str = "accesscontrol") -> None:
    """Check the production Configuration identity and exact caller policy."""
    content = ACL.read_text() if content is None else content
    lines = _acl_yaml_lines(content)
    root_fields = [_acl_pair(value) for indent, value in lines if indent == 0]
    assert [value for key, value in root_fields if key == "apiVersion"] == ["dapr.io/v1alpha1"]
    assert [value for key, value in root_fields if key == "kind"] == ["Configuration"]
    metadata_positions = [index for index, line in enumerate(lines) if line == (0, "metadata:")]
    assert len(metadata_positions) == 1
    metadata_start = metadata_positions[0] + 1
    metadata_end = next((index for index in range(metadata_start, len(lines))
                         if lines[index][0] == 0), len(lines))
    metadata_names = [_acl_pair(value)[1] for indent, value in lines[metadata_start:metadata_end]
                      if indent == 2 and _acl_pair(value)[0] == "name"]
    assert metadata_names == [expected_resource_name]
    acl = parse_effective_acl(content)
    trust_domain = "{env:DAPR_TRUST_DOMAIN|hexalith.io}"
    namespace = "{env:DAPR_NAMESPACE|hexalith}"
    assert acl["defaultAction"] == "deny"
    assert acl["trustDomain"] == trust_domain
    policies = acl["policies"]
    assert len({item["appId"] for item in policies}) == len(policies)
    by_app = {item["appId"]: item for item in policies}
    assert set(by_app) == {"eventstore-admin", "tenants-api", "tenants"}
    for app_id, expected_operations in (
        ("eventstore-admin", {("/**", ("GET", "POST", "PUT"))}),
        ("tenants-api", {("/ready", ("GET",)), ("/api/v1/queries", ("POST",)),
                         ("/api/v1/commands", ("POST",))}),
        ("tenants", {("/api/v1/commands", ("POST",))}),
    ):
        caller = by_app[app_id]
        assert caller["defaultAction"] == "deny", app_id
        assert caller["trustDomain"] == trust_domain, app_id
        assert caller["namespace"] == namespace, app_id
        operations = caller["operations"]
        assert all(set(item) == {"name", "httpVerb", "action"} and item["action"] == "allow"
                   and isinstance(item["httpVerb"], list) for item in operations), app_id
        actual = [(item["name"], tuple(item["httpVerb"])) for item in operations]
        assert len(actual) == len(expected_operations) and set(actual) == expected_operations, app_id


def inspect_inventory(documents: list[dict]) -> None:
    modules = [document["modules"][0] for document in documents]
    by_name = {module["identity"]["moduleId"]: module for module in modules}
    assert set(by_name) == {"eventstore", "memories", "tenants"}
    assert len({server["appId"] for module in modules for server in module["identity"]["servers"]}) == 6
    assert len({server["resourceId"] for module in modules for server in module["identity"]["servers"]}) == 6
    assert by_name["eventstore"]["identity"]["dependencies"] == []
    assert by_name["memories"]["identity"]["dependencies"] == ["eventstore"]
    assert by_name["tenants"]["identity"]["dependencies"] == ["eventstore"]
    for module_id, module in by_name.items():
        for inventory, expected in EXPECTED_SECURITY_INVENTORY[module_id].items():
            assert exact_records(module["integration"][inventory], expected), (module_id, inventory)
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
    for key in ("Authentication:DaprInternal", "Authentication:DaprInternal:Audience",
                "Authentication:DaprInternal:AllowedCallers",
                "Authentication:DaprInternal:MaximumLifetimeSeconds",
                "Authentication:WorkloadIssuer", "Authentication:WorkloadIssuer:Workload",
                "Authentication:WorkloadIssuer:LifetimeSeconds",
                "Authentication:WorkloadIssuer:AudienceScopePrefix",
                "Authentication:WorkloadIssuer:OperationScopePrefix"):
        assert input_details(eventstore)[key][0] == "configuration", key
    assert input_details(eventstore)["Authentication:WorkloadIssuer:ClientId"] == (
        "configuration", "workloadissuerclientid")
    assert input_details(eventstore)["Authentication:WorkloadIssuer:ClientSecret"] == (
        "secret", "workload-issuer-client-secret")
    assert input_details(eventstore)["Authentication:WorkloadIssuer:TokenEndpoint"] == (
        "configuration", "workloadissuertokenendpoint")
    assert {"name": "workload-issuer-client-secret",
            "configurationKey": "Authentication:WorkloadIssuer:ClientSecret"} in eventstore["integration"]["secrets"]
    assert {"workload-issuer-explicit-token":
            "Authentication:WorkloadIssuer:TokenEndpoint (when configured)",
            "workload-issuer-discovered-token":
            "Authentication:JwtBearer:Authority -> metadata.token_endpoint (when TokenEndpoint omitted)"} == {
                item["name"]: item["destination"] for item in eventstore["integration"]["egress"]
                if item["name"].startswith("workload-issuer-")}
    assert all(item["protocol"] == "https" for item in eventstore["integration"]["egress"]
               if item["name"].startswith("workload-issuer-"))
    assert {"EventStore:Projections", "EventStore:Projections:WorkersEnabled",
            "EventStore:Projections:CheckpointStateStoreName",
            "EventStore:Projections:ReadModelStateStoreName",
            "EventStore:Projections:RebuildIndexCleanupCadenceSeconds",
            "EventStore:ProjectionDispatch", "EventStore:ProjectionDispatch:MaxRetryAttempts",
            "EventStore:ProjectionDispatch:RetryWorkerInterval",
            "EventStore:ProjectionDeliveryIdempotency",
            "EventStore:ProjectionDeliveryIdempotency:CompletedReceiptLimit"} <= inputs(eventstore)
    assert {"projection-delivery-retry", "projection-activation", "projection-polling",
            "projection-active-index-cleanup"} == {
                worker["name"] for worker in eventstore["integration"]["workers"]}
    assert all(worker == {"name": worker["name"], "kind": "external-effect",
                          "disableControl": {"configurationKey": "EventStore:Projections:WorkersEnabled",
                                             "disabledValue": False}}
               for worker in eventstore["integration"]["workers"])
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
    assert {"EmbeddingProviders:Google:ApiBaseUrl", "EmbeddingProviders:Ollama:BaseUrl",
            "EmbeddingProviders:Ollama:OidcTokenEndpoint",
            "EmbeddingProviders:Ollama:OidcClientId", "EmbeddingProviders:Ollama:OidcScope"} <= inputs(memories)
    assert input_details(memories)["EmbeddingProviders:Ollama:OidcClientId"] == (
        "configuration", "oidcclientid")
    assert input_details(memories)["EmbeddingProviders:Ollama:OidcScope"] == (
        "configuration", "oidcscope")
    assert {"embedding-google-provider": "EmbeddingProviders:Google:ApiBaseUrl",
            "embedding-ollama-provider": "EmbeddingProviders:Ollama:BaseUrl",
            "embedding-ollama-oidc-token":
                "EmbeddingProviders:Ollama:OidcTokenEndpoint (OIDC client-credential mode only)"} == {
                    item["name"]: item["destination"] for item in memories["integration"]["egress"]
                    if item["name"].startswith("embedding-")}
    embedding_protocols = {item["name"]: item["protocol"] for item in memories["integration"]["egress"]
                           if item["name"].startswith("embedding-")}
    assert embedding_protocols == {"embedding-google-provider": "tcp",
                                   "embedding-ollama-provider": "tcp",
                                   "embedding-ollama-oidc-token": "https"}
    assert input_details(memories)["retentionSeconds"] == ("configuration", "access-telemetry-config")
    assert {"clock-signing-key", "telemetry-marker-key", "conversation-provider-api-key"} <= {
        item["name"] for item in memories["integration"]["secrets"]}
    assert {"embedding-credentials", "clock-source-authentication-tokens"} <= {
        item["name"] for item in memories["integration"]["dynamicSecretNamespaces"]}
    assert input_details(memories)["Clock:SourceAuthenticationTokens"] == (
        "dynamic-secret", "clock-source-authentication-tokens")
    assert input_details(memories)["Conversation:ProviderApiKey"] == (
        "secret", "conversation-provider-api-key")
    assert {"Authentication:JwtBearer:Authority", "Authentication:JwtBearer:SigningKey",
            "Authentication:JwtBearer:Issuer", "Authentication:JwtBearer:Audience",
            "Authentication:JwtBearer:RequireHttpsMetadata", "Authentication:JwtBearer:TenantClaimName",
            "Authentication:JwtBearer:ValidAlgorithms", "Memories:AppChannelToken",
            "Memories:DaprApiToken"} <= inputs(memories)
    for key, reference in (("Authentication:JwtBearer:SigningKey", "server-jwt-signing-key"),
                           ("Memories:AppChannelToken", "app-channel-token"),
                           ("Memories:DaprApiToken", "dapr-api-token")):
        assert input_details(memories)[key] == ("secret", reference)
        assert {"name": reference, "configurationKey": key} in memories["integration"]["secrets"]
    for key in ("Authentication:JwtBearer:Authority", "Authentication:JwtBearer:Issuer",
                "Authentication:JwtBearer:Audience", "Authentication:JwtBearer:RequireHttpsMetadata",
                "Authentication:JwtBearer:TenantClaimName", "Authentication:JwtBearer:ValidAlgorithms"):
        assert input_details(memories)[key][0] == "configuration"
    server_deployment = (ROOT / "references/Hexalith.Memories/deploy/kubernetes/base/server-deployment.yaml").read_text()
    telemetry_deployment = (ROOT / "references/Hexalith.Memories/deploy/kubernetes/base/access-telemetry-deployments.yaml").read_text()
    for token in ("APP_API_TOKEN", "DAPR_API_TOKEN"):
        assert len(re.findall(rf"^\s+- name: {token}$", server_deployment, flags=re.MULTILINE)) == 1
        assert len(re.findall(rf"^\s+- name: {token}$", telemetry_deployment, flags=re.MULTILINE)) == 2
    for field in ("Authority", "Issuer", "Audience", "RequireHttpsMetadata",
                  "TenantClaimName", "ValidAlgorithms"):
        assert f"Authentication__JwtBearer__{field}" in server_deployment
    conversation_component = (ROOT / "references/Hexalith.Memories/deploy/kubernetes/base/dapr/conversation-openai.yaml").read_text()
    assert "type: conversation.openai" in conversation_component
    assert "name: llm-secret" in conversation_component
    assert "key: OPENAI_API_KEY" in conversation_component
    assert any(item == {"name": "authenticated-utc", "protocol": "https",
                        "destination": "Clock:Sources:<index>:Endpoint"}
               for item in memories["integration"]["egress"])
    assert {"name": "conversation-provider", "protocol": "https",
            "destination": "Memories:LlmComponentPath -> conversation.openai provider endpoint"} in memories["integration"]["egress"]
    assert {"embedding-retry", "telemetry-delivery", "telemetry-heartbeat",
            "telemetry-retention", "telemetry-capability-probe",
            "workflow-replay-safety", "routed-tenant-provisioning",
            "orphan-semantic-index-reconciliation", "is-stub-backfill"} == {
                item["name"] for item in memories["integration"]["workers"]}
    assert {"name": "telemetry-capability-probe", "kind": "external-effect",
            "disableControl": {"configurationKey": "AccessTelemetryLifecycle:Enabled",
                               "disabledValue": False}} in memories["integration"]["workers"]
    assert {"name": "workflow-replay-safety", "kind": "external-effect",
            "disableControl": {"configurationKey": "WorkflowReplaySafety:Enabled",
                               "disabledValue": False}} in memories["integration"]["workers"]
    assert {"WorkflowReplaySafety:Enabled", "AccessTelemetryLifecycle:CapabilityEvidence",
            "AccessTelemetryLifecycle:CapabilityEvidence:ExactVersionPinned",
            "AccessTelemetryLifecycle:CapabilityEvidence:ValidUntilUtc"} <= inputs(memories)
    for key in ("WorkflowReplaySafety:Enabled", "AccessTelemetryLifecycle:CapabilityEvidence",
                "AccessTelemetryLifecycle:CapabilityEvidence:ExactVersionPinned",
                "AccessTelemetryLifecycle:CapabilityEvidence:ValidUntilUtc"):
        assert input_details(memories)[key][0] == "configuration"
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
    assert input_details(tenants)["Tenants:ApiDaprToken"] == ("secret", "api-dapr-token")
    assert {"name": "api-dapr-token", "configurationKey": "Tenants:ApiDaprToken"} in tenants["integration"]["secrets"]
    assert {"bootstrap-delegated-password", "domain-app-channel-token"} <= {
        item["name"] for item in tenants["integration"]["secrets"]}
    assert input_details(tenants)["EventStore:Authentication:Password"] == (
        "secret", "bootstrap-delegated-password")
    assert input_details(tenants)["Tenants:AppChannelToken"] == (
        "secret", "domain-app-channel-token")
    assert any(item == {"name": "bootstrap-oidc-token", "protocol": "oidc",
                        "destination": "EventStore:Authentication:Authority/protocol/openid-connect/token"}
               for item in tenants["integration"]["egress"])
    for module, authority in ((eventstore, "Authentication:JwtBearer:Authority"),
                              (memories, "Authentication:JwtBearer:Authority"),
                              (tenants, "Authentication:JwtBearer:Authority"),
                              (tenants, "Oidc:Authority"),
                              (tenants, "EventStore:Authentication:Authority")):
        egress = module["integration"]["egress"]
        assert any(item["protocol"] == "https" and
                   item["destination"] == authority + "/.well-known/openid-configuration"
                   for item in egress)
        assert any(item["protocol"] == "https" and
                   item["destination"] == authority + " -> metadata.jwks_uri" for item in egress)
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
    build = subprocess.run(["dotnet", "build", str(CLI)], cwd=BUILDS,
                           capture_output=True, text=True, check=False)
    assert build.returncode == 0, f"Validator build failed: {build.stdout} {build.stderr}"
    documents = validated_documents(PATHS)
    inspect_inventory(documents)
    production_acl = ACL.read_text(encoding="utf-8")
    tenants_acl = TENANTS_ACL.read_text(encoding="utf-8")
    inspect_acl(production_acl)
    inspect_acl(tenants_acl, expected_resource_name="accesscontrol-eventstore")
    api_prefix, marker, after_api = production_acl.partition("      - appId: tenants-api\n")
    assert marker
    api_body, domain_marker, domain_body = after_api.partition("      - appId: tenants\n")
    assert domain_marker
    acl_variants = {
        "comment-only deny": production_acl.replace(
            "defaultAction: deny  # Deny-by-default security posture (D4, AC #4)",
            "defaultAction: allow  # defaultAction: deny", 1),
        "reordered broad allow": production_acl.replace(
            "      # Domain bootstrap",
            "          - name: /**\n            action: allow\n            httpVerb: ['GET']\n"
            "      # Domain bootstrap", 1),
        "caller namespace drift": api_prefix + marker + api_body.replace(
            'namespace: "{env:DAPR_NAMESPACE|hexalith}"', 'namespace: "other"', 1)
            + domain_marker + domain_body,
        "caller trust-domain drift": api_prefix + marker + api_body.replace(
            'trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"', 'trustDomain: "other"', 1)
            + domain_marker + domain_body,
        "extra caller": production_acl + (
            "      - appId: unexpected-caller\n"
            "        defaultAction: deny\n"
            '        trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"\n'
            '        namespace: "{env:DAPR_NAMESPACE|hexalith}"\n'
            "        operations:\n"
            "          - name: /ready\n"
            "            httpVerb: ['GET']\n"
            "            action: allow\n"),
        "widened admin rule": production_acl.replace(
            "httpVerb: ['GET', 'POST', 'PUT']",
            "httpVerb: ['GET', 'POST', 'PUT', 'DELETE']", 1),
        "admin default drift": production_acl.replace(
            "defaultAction: deny  # Deny any operations not explicitly listed",
            "defaultAction: allow  # Deny any operations not explicitly listed", 1),
        "admin namespace drift": production_acl.replace(
            'namespace: "{env:DAPR_NAMESPACE|hexalith}"', 'namespace: "other"', 1),
        "duplicate spec": production_acl + "\nspec:\n  accessControl:\n    defaultAction: allow\n",
        "duplicate access control": production_acl + "\n  accessControl:\n    defaultAction: allow\n",
        "resource kind drift": production_acl.replace("kind: Configuration", "kind: Service", 1),
        "resource name drift": production_acl.replace(
            "  name: accesscontrol\n", "  name: another-resource\n", 1),
    }
    for scenario, policy in acl_variants.items():
        assert policy != production_acl, scenario
        try:
            inspect_acl(policy)
        except AssertionError:
            print(f"PASS {scenario}: effective ACL refusal")
        else:
            raise AssertionError(f"{scenario} escaped the effective ACL check")
    for scenario, policy in (
        ("Tenants copy resource kind drift", tenants_acl.replace("kind: Configuration", "kind: Service", 1)),
        ("Tenants copy resource name drift", tenants_acl.replace(
            "  name: accesscontrol-eventstore\n", "  name: another-resource\n", 1)),
        ("Tenants copy extra caller", tenants_acl + (
            "      - appId: unexpected-caller\n"
            "        defaultAction: deny\n"
            '        trustDomain: "{env:DAPR_TRUST_DOMAIN|hexalith.io}"\n'
            '        namespace: "{env:DAPR_NAMESPACE|hexalith}"\n'
            "        operations:\n"
            "          - name: /ready\n"
            "            httpVerb: ['GET']\n"
            "            action: allow\n")),
        ("Tenants copy widened bootstrap", (
            "          - name: /**\n            httpVerb: ['POST']\n            action: allow\n").join(
                tenants_acl.rsplit(
                    "          - name: /api/v1/commands\n            httpVerb: ['POST']\n            action: allow\n",
                    1))),
    ):
        assert policy != tenants_acl, scenario
        try:
            inspect_acl(policy, expected_resource_name="accesscontrol-eventstore")
        except AssertionError:
            print(f"PASS {scenario}: effective ACL refusal")
        else:
            raise AssertionError(f"{scenario} escaped the effective ACL check")
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
            ("array exception name", lambda docs: docs[1]["modules"][0]["runtime"]["providerExceptions"][0].update(name=["invalid"]), "providerExceptions[0].name"),
            ("object exception name", lambda docs: docs[1]["modules"][0]["runtime"]["providerExceptions"][0].update(name={"invalid": True}), "providerExceptions[0].name"),
            ("missing identity", lambda docs: docs[1]["modules"][0].pop("identity"), "modules[0].identity"),
            ("missing runtime", lambda docs: docs[1]["modules"][0].pop("runtime"), "modules[0].runtime"),
            ("null provider exception", lambda docs: docs[1]["modules"][0]["runtime"]["providerExceptions"].__setitem__(0, None), "providerExceptions[0]"),
            ("scalar provider exception", lambda docs: docs[1]["modules"][0]["runtime"]["providerExceptions"].__setitem__(0, "unexpected"), "providerExceptions[0]"),
            ("renamed owner", lambda docs: docs[1]["modules"][0]["identity"].update(moduleId="different-memories"), "identity.moduleId"),
            ("invented domain dependency", lambda docs: docs[1]["modules"][0]["identity"]["dependencies"].append("parties"), "identity.dependencies"),
        ):
            variant = copy.deepcopy(documents)
            mutate(variant)
            variant_paths = []
            for index, document in enumerate(variant):
                path = temp / f"{scenario.replace(' ', '-')}-{index}.json"
                path.write_text(json.dumps(document))
                variant_paths.append(path)
            accepted, diagnostics, public_diagnostics = validate(variant_paths)
            assert not accepted, f"{scenario} yielded a partial declaration set"
            assert any(Path(item.get("source", "")).name in {path.name for path in variant_paths}
                       and field in item.get("field", "")
                       for item in diagnostics), f"{scenario}: {diagnostics}"
            if scenario in {"missing identity", "missing runtime", "null provider exception",
                            "scalar provider exception", "array exception name",
                            "object exception name"}:
                assert any(Path(item.get("source", "")).name in {path.name for path in variant_paths}
                           and field in item.get("field", "")
                           for item in public_diagnostics), (
                               f"{scenario} lost public validator diagnostics: {public_diagnostics}")
            print(f"PASS {scenario}: atomic refusal with source and field")
        for owner in range(3):
            for inventory, addition in (
                ("secrets", {"name": "unexpected-secret", "configurationKey": "Unexpected:Secret"}),
                ("egress", {"name": "unexpected-outbound", "protocol": "https",
                            "destination": "Unexpected:Endpoint"}),
            ):
                variant = copy.deepcopy(documents)
                variant[owner]["modules"][0]["integration"][inventory].append(addition)
                variant_paths = []
                for index, document in enumerate(variant):
                    path = temp / f"extra-{inventory}-{owner}-{index}.json"
                    path.write_text(json.dumps(document))
                    variant_paths.append(path)
                accepted, diagnostics, public_diagnostics = validate(variant_paths)
                assert not accepted and not public_diagnostics, (owner, inventory, diagnostics)
                assert any(item.get("source") == str(variant_paths[owner]) and
                           item.get("field") == f"modules[0].integration.{inventory}"
                           for item in diagnostics), (owner, inventory, diagnostics)
                print(f"PASS extra {inventory} in {owner}: atomic policy refusal")
        for scenario, malformed, field in (
            ("malformed json", "{", "$"),
            ("non-object root", "[]", "schema"),
            ("invalid utf-8", b"\xff", "$"),
        ):
            variant_paths = []
            for index, document in enumerate(documents):
                path = temp / f"{scenario.replace(' ', '-')}-{index}.json"
                content = malformed if index == 1 else json.dumps(document)
                path.write_bytes(content if isinstance(content, bytes) else content.encode("utf-8"))
                variant_paths.append(path)
            accepted, diagnostics, public_diagnostics = validate(variant_paths)
            assert not accepted, f"{scenario} yielded a partial declaration set"
            assert any(Path(item.get("source", "")).name == variant_paths[1].name
                       and field in item.get("field", "") for item in public_diagnostics), (
                           f"{scenario} lost public source/field diagnostics: {public_diagnostics}")
            assert public_diagnostics == diagnostics, f"{scenario} altered validator diagnostics"
            try:
                validated_documents(variant_paths)
            except AssertionError as error:
                assert str(variant_paths[1]) in str(error) and field in str(error), error
            else:
                raise AssertionError(f"{scenario} reached canonical owner parsing")
            print(f"PASS {scenario}: atomic refusal with public source and field")
    print("PASS joint declaration set, readiness, routes, inventories and empty tasks")


if __name__ == "__main__":
    main()
