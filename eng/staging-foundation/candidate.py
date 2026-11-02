"""Additive Story 4.14 offline assessment, rendering and byte-bound blocked plans.

No process, network, credential issuance or executable dispatcher exists here.
Evidence references establish local byte provenance only, never authentication.
The historical renderer and all Story 4.15 source pins remain unchanged.
"""

import argparse
import copy
from datetime import datetime, timedelta, timezone
import hashlib
import ipaddress
import json
from pathlib import Path, PurePosixPath
import re
import sys
from uuid import UUID

import preflight
import render as historical

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
APP, DATA = historical.APP_NS, historical.DATA_NS
CLASS = "hexalith-staging"
ERROR = historical.InputError
SHA = re.compile(r"^[0-9a-f]{64}$")
NAME = re.compile(r"(?=.{1,253}$)[a-z0-9](?:[-a-z0-9]{0,61}[a-z0-9])?(?:\.[a-z0-9](?:[-a-z0-9]{0,61}[a-z0-9])?)*")
MUTATE = ["create", "update", "patch", "delete", "deletecollection"]
READ = ["get", "list", "watch"]
DEPENDENCIES = ("4.1", "4.2", "4.3", "4.27", "4.28")
FILES = ("00-access-cluster.yaml", "10-admission.yaml", "20-shared-infrastructure.yaml", "25-access-namespaced.yaml", "30-environment-layer.yaml")
PROTECTED = {"resourcequotas", "limitranges", "networkpolicies", "configmaps", "roles", "rolebindings"}
SOURCE_PINS = {
    "APPROVAL-PACKAGE.md": "4ffff30842efb4f7bd86000e9f0643abc9c2c8563abda130672d9da979ee4eba",
    "inputs.resolved.json": "a9fea6fd88ea6fbeffa8a5b2ee3f67856ec9ef57e6200abdbf550c9c60f03a00",
    "MISSING-INPUTS.md": "00db84e7976015c52fbd574236a02c0e6a6f67eee24b9ba7943e3a14c3ca6267",
    "DEPENDENCY-DIRECTION.md": "bbde8ac2e5593c7d9a06ee019e2e68e1d3f36c228d35fa9287b606ae02092f8a",
    "render.py": "b614c1364b80c238c6ae8042e37832cd5555131e69038c424fd1cf1c3e61abdc",
    "apply-plan.json": "475411c7096fc807a5b2b33a531c2d11501965e34db3914ac47c8423f2c91351",
}


def encoded(value):
    return (json.dumps(value, sort_keys=True, indent=2) + "\n").encode()


def digest(value):
    return hashlib.sha256(value).hexdigest()


def decode_json(raw):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"duplicate JSON key {key!r}")
            result[key] = value
        return result
    return json.loads(raw, object_pairs_hook=unique)


def _read(path):
    raw = Path(path).read_bytes()
    return raw, decode_json(raw)


def safe_file(path, root):
    """Allow regular repository files only; reject traversal and symlink ancestry."""
    root = Path(root).resolve()
    if not isinstance(path, str) or not path or Path(path).is_absolute() or ".." in Path(path).parts:
        raise ValueError("must be a repository-relative path without traversal")
    node = root
    for part in Path(path).parts:
        node = node / part
        if node.is_symlink():
            raise ValueError("symlink evidence is refused")
    if not node.is_file() or not node.resolve().is_relative_to(root):
        raise ValueError("must reference an existing regular repository file")
    return node


def evidence(ref, path, problems, root):
    if not isinstance(ref, dict) or set(ref) != {"path", "sha256"} or not isinstance(ref.get("sha256"), str) or not SHA.fullmatch(ref["sha256"]):
        problems.append(f"{path}: required local evidence object with path and sha256 (unverified provenance only)")
        return
    try:
        actual = digest(safe_file(ref["path"], root).read_bytes())
        if actual != ref["sha256"]:
            raise ValueError("evidence hash mismatch")
    except (OSError, ValueError) as error:
        problems.append(f"{path}: {error}")


def pinned_sources(root=PROJECT):
    """Check all six foundation pins against the preserved Story 4.15 index."""
    index_path = safe_file("eng/staging-secrets/source-evidence.json", root)
    index = json.loads(index_path.read_bytes())
    pins = [source for source in index["sources"] if source["id"].startswith("foundation-")]
    expected = {"eng/staging-foundation/" + name: sha for name, sha in SOURCE_PINS.items()}
    if {pin["path"]: pin["sha256"] for pin in pins} != expected or len(pins) != 6:
        raise ERROR(["historical source index: expected all six foundation pins"])
    problems = []
    for pin in pins:
        evidence({"path": pin["path"], "sha256": pin["sha256"]}, pin["id"], problems, root)
    if problems:
        raise ERROR(problems)
    receipts = [{"path": str(index_path.relative_to(Path(root).resolve())), "sha256": digest(index_path.read_bytes())}]
    for name in ("gates.assessed.json", "unblocking.assessed.json"):
        path = safe_file("eng/staging-foundation/" + name, root)
        receipts.append({"path": str(path.relative_to(Path(root).resolve())), "sha256": digest(path.read_bytes())})
    return {"foundationPins": pins, "sourceReceipts": receipts, "authenticated": False}


def timestamp(value):
    if not isinstance(value, str):
        raise ValueError("timezone-aware timestamp required")
    result = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if result.tzinfo is None or result.utcoffset() is None:
        raise ValueError("timezone-aware timestamp required")
    return result


TARGET_FIELDS = {"context", "server", "clusterUid", "caSha256", "credentialPublicIdentitySha256", "identityEvidence", "attemptId", "observedAt", "expiresAt", "kubectl"}
TOOL_FIELDS = {"path", "sha256", "version"}
SERVICE_FIELDS = {"name", "podLabels", "clientPodLabels", "tlsPort", "nativeTlsRequired", "providerContractEvidence"}
DAPR_APPLICATION = {"configurations", "workflowaccesspolicies", "subscriptions", "resiliencies"}
DAPR_ENVIRONMENT = {"components", "httpendpoints", "mcpservers"}
DNS_LABEL = re.compile(r"[a-z0-9](?:[-a-z0-9]{0,61}[a-z0-9])?")
LABEL_PART = re.compile(r"[A-Za-z0-9](?:[-A-Za-z0-9_.]{0,61}[A-Za-z0-9])?")


def namespace_name(value):
    return isinstance(value, str) and len(value) <= 63 and DNS_LABEL.fullmatch(value) is not None


def host_name(value):
    """DNS host or literal IP; numeric dotted hosts must be valid IPv4."""
    if not isinstance(value, str) or not value or "%" in value:
        return False
    try:
        address = ipaddress.ip_address(value)
        return not address.is_unspecified and not address.is_multicast
    except ValueError:
        return not re.fullmatch(r"[0-9.]+", value) and NAME.fullmatch(value.lower()) is not None


def image_reference(value):
    """Practical pinned OCI subset: DNS registry[:port], lower-case path, optional tag."""
    if not isinstance(value, str) or value.count("@") != 1:
        return False
    repository, digest_part = value.split("@")
    if not re.fullmatch(r"sha256:[0-9a-f]{64}", digest_part) or not repository or len(repository) > 255:
        return False
    parts = repository.split("/")
    if len(parts) > 1 and ("." in parts[0] or ":" in parts[0] or parts[0] == "localhost"):
        registry = parts.pop(0).split(":")
        if len(registry) > 2 or not host_name(registry[0]) or (len(registry) == 2 and (not registry[1].isdigit() or not 1 <= int(registry[1]) <= 65535)):
            return False
    final = parts[-1].split(":")
    if len(final) > 2 or (len(final) == 2 and re.fullmatch(r"[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}", final[1]) is None):
        return False
    parts[-1] = final[0]
    return all(re.fullmatch(r"[a-z0-9]+(?:[._-][a-z0-9]+)*", part) is not None for part in parts)


def principal(value, path, problems):
    if not isinstance(value, str) or historical._placeholder(value) or any(c.isspace() for c in value) or value.startswith("system:masters") or value == "system:anonymous":
        problems.append(f"{path}: exact individual principal without whitespace required")
        return
    if value.startswith("system:serviceaccount:"):
        parts = value.split(":")
        if len(parts) != 4 or not namespace_name(parts[2]) or not isinstance(parts[3], str) or not NAME.fullmatch(parts[3]) or len(parts[3]) > 253:
            problems.append(f"{path}: canonical ServiceAccount principal required")
        elif parts[2] in (APP, DATA):
            problems.append(f"{path}: privileged identities cannot reside in either workload namespace")


def validate_target(target, root=PROJECT, now=None):
    """Check only public metadata and local bytes, never authenticate a target."""
    problems = []
    now = now or datetime.now(timezone.utc)
    get = lambda path: historical._get({"target": target}, path)
    for prefix, allowed_fields in (("target", TARGET_FIELDS), ("target.kubectl", TOOL_FIELDS)):
        obj = get(prefix)
        if not isinstance(obj, dict):
            problems.append(f"{prefix}: required public metadata object")
        else:
            for key in obj.keys() - allowed_fields:
                problems.append(f"{prefix}.{key}: only public target/tool binding metadata is permitted")
    evidence(get("target.identityEvidence"), "target.identityEvidence", problems, root)
    for path in ("target.context", "target.server", "target.clusterUid", "target.caSha256", "target.credentialPublicIdentitySha256", "target.kubectl.path", "target.kubectl.sha256", "target.kubectl.version", "target.attemptId"):
        value = get(path)
        if not isinstance(value, str) or value != value.strip() or historical._placeholder(value):
            problems.append(f"{path}: required non-placeholder str")
    if isinstance(get("target.attemptId"), str) and re.fullmatch(r"[a-z0-9](?:[a-z0-9-]{0,38}[a-z0-9])?", get("target.attemptId")) is None:
        problems.append("target.attemptId: fresh DNS-safe unique attempt ID of 1..40 characters required")
    server = get("target.server")
    if isinstance(server, str):
        origin = re.fullmatch(r"https://(\[[^\]]+\]|[^:/?#@\s]+)(?::([0-9]{1,5}))?", server)
        if origin is None or not host_name(origin[1][1:-1] if origin[1].startswith("[") else origin[1]) or (origin[1].startswith("[") and ":" not in origin[1]) or (origin[2] and not 1 <= int(origin[2]) <= 65535):
            problems.append("target.server: valid exact HTTPS API host and port required (no credentials, query or path)")
    for path in ("target.caSha256", "target.credentialPublicIdentitySha256", "target.kubectl.sha256"):
        if isinstance(get(path), str) and not SHA.fullmatch(get(path)):
            problems.append(f"{path}: lowercase SHA-256 required")
    if isinstance(get("target.kubectl.path"), str) and not Path(get("target.kubectl.path")).is_absolute():
        problems.append("target.kubectl.path: absolute tool path required")
    if isinstance(get("target.clusterUid"), str):
        try:
            if str(UUID(get("target.clusterUid"))) != get("target.clusterUid"):
                raise ValueError()
        except ValueError:
            problems.append("target.clusterUid: canonical UUID required")
    try:
        observed, expires = timestamp(get("target.observedAt")), timestamp(get("target.expiresAt"))
        if not now - timedelta(minutes=15) <= observed <= now or not now < expires <= observed + timedelta(hours=1):
            raise ValueError("stale/expired target attempt; fresh authorized observations required")
    except ValueError as error:
        problems.append(f"target.observedAt/expiresAt: {error}")

    if problems:
        raise ERROR(problems)


def validate(inputs, root=PROJECT, now=None):
    """Collect exact missing/unsafe facts; never infer facts from fixture values."""
    problems = []
    if not isinstance(inputs, dict):
        raise ERROR(["inputs: JSON object required"])
    get = lambda path: historical._get(inputs, path)
    now = now or datetime.now(timezone.utc)
    allowed = {"kind", "decisionProvenance", "storage", "priority", "identities", "rbac", "target", "network", "limitRange", "quotas", "capacity", "probe", "dependencies", "liveAuthorization", "approvalGranted", "liveAcceptance"}
    for key in inputs.keys() - allowed:
        problems.append(f"inputs.{key}: unknown field refused")

    def required(path, kind=str):
        value = get(path)
        good = isinstance(value, kind) and not isinstance(value, bool)
        if kind is str:
            good = good and value == value.strip() and not historical._placeholder(value)
        if kind in (list, dict):
            good = good and bool(value)
        if not good:
            problems.append(f"{path}: required non-placeholder {kind.__name__}")
            return None
        return value

    def selected(path, value):
        if get(path) != value or type(get(path)) is not type(value):
            problems.append(f"{path}: approved candidate value must be {value!r}")

    selected("kind", "staging-foundation-offline-candidate-v1")
    for path, value in {"storage.provisioner": "openebs.io/local", "storage.parameters": {},
                        "storage.stagingStorageClassName": CLASS, "storage.reclaimPolicy": "Retain",
                        "storage.volumeBindingMode": "WaitForFirstConsumer", "storage.filesystem": "xfs",
                        "storage.mountOptions": ["prjquota"], "storage.projectQuota": {"name": "XFSQuota", "enabled": "true", "data": {"softLimitGrace": "0", "hardLimitGrace": "0"}},
                        "priority.stagingValue": -1000, "priority.productionValue": 1000,
                        "network.cni": "Calico", "approvalGranted": False, "liveAcceptance": False}.items():
        selected(path, value)
    required("decisionProvenance", dict)
    for path in ("storage.productionStorageClassName", "priority.productionClassName"):
        value = required(path)
        if value and (not NAME.fullmatch(value) or len(value) > 253 or value == CLASS):
            problems.append(f"{path}: must be a valid production name distinct from staging")
    paths = {}
    for path in ("storage.basePath", "storage.productionBasePath", "storage.backingDevice", "storage.productionBackingDevice"):
        value = required(path)
        if value:
            if not value.startswith("/") or str(PurePosixPath(value)) != value or ".." in PurePosixPath(value).parts or value == "/":
                problems.append(f"{path}: canonical absolute non-root path required")
            else:
                paths[path] = PurePosixPath(value)
    for first, second in (("storage.basePath", "storage.productionBasePath"), ("storage.backingDevice", "storage.productionBackingDevice")):
        if first in paths and second in paths and (paths[first].is_relative_to(paths[second]) or paths[second].is_relative_to(paths[first])):
            problems.append(f"{first}: staging and production backing paths/devices must be separate and non-overlapping")
    principals = []
    for path in ["storage.administrator", "storage.deputy", "storage.provisionerPrincipal", "storage.binderPrincipal"] + ["identities." + key for key in ("administrator", "sharedInfrastructure", "environmentLayer", "applicationDeploy", "observer")]:
        value = required(path)
        if value:
            principal(value, path, problems)
            principals.append((path, value))
    # Storage Administrator and identity Administrator may be the same person.
    role_principals = [(path, value) for path, value in principals if path not in ("storage.administrator", "storage.deputy")]
    if len({value for _, value in role_principals}) != len(role_principals):
        problems.append("identities: every deploy/observer/Administrator/controller principal must be distinct")
    if get("storage.administrator") and get("storage.administrator") == get("storage.deputy"):
        problems.append("storage.deputy: independent Administrator/deputy custody required")

    for path in ("storage.compatibilityEvidence", "storage.encryptionEvidence", "storage.custodyEvidence", "priority.productionEvidence", "identities.qualificationEvidence", "rbac.inventoryEvidence", "network.qualificationEvidence", "network.dns.qualificationEvidence", "network.flowReviewEvidence", "capacity.compositionEvidence", "probe.imageQualificationEvidence"):
        evidence(get(path), path, problems, root)
    for path in ("rbac.providerResources", "rbac.applicationPackageResources"):
        inventory = get(path)
        if not isinstance(inventory, list):
            problems.append(path + ": explicit reviewed resource inventory required; no provider CRD is inferred")
            continue
        seen = set()
        for entry in inventory:
            if not isinstance(entry, dict) or set(entry) != {"apiGroup", "resources"} or not isinstance(entry.get("apiGroup"), str) or "*" in entry["apiGroup"] or not isinstance(entry.get("resources"), list) or not entry["resources"]:
                problems.append(path + ": exact apiGroup/resources entries required")
                continue
            for resource_name in entry["resources"]:
                key = (entry["apiGroup"], resource_name) if isinstance(resource_name, str) else None
                forbidden = {"namespaces", "persistentvolumes", "persistentvolumeclaims", "secrets", "roles", "rolebindings", "clusterroles", "clusterrolebindings", "resourcequotas", "limitranges", "networkpolicies", "gateways", "referencegrants", "storageclasses", "priorityclasses", "customresourcedefinitions", "validatingadmissionpolicies", "validatingadmissionpolicybindings", "mutatingwebhookconfigurations", "validatingwebhookconfigurations", "certificatesigningrequests", "tokenreviews", "subjectaccessreviews"}
                if not isinstance(resource_name, str) or re.fullmatch(r"[a-z][a-z0-9]*", resource_name) is None or resource_name in forbidden or key in seen or entry["apiGroup"] in ("rbac.authorization.k8s.io", "admissionregistration.k8s.io", "apiextensions.k8s.io", "authentication.k8s.io", "authorization.k8s.io", "certificates.k8s.io"):
                    problems.append(path + ": unsafe, duplicate, privileged or subresource grant refused")
                elif path.endswith("providerResources") and entry["apiGroup"] in ("", "apps", "batch", "gateway.networking.k8s.io", "dapr.io"):
                    problems.append(path + ": provider additions must be independently qualified namespaced CRDs, not built-in grants")
                elif path.endswith("applicationPackageResources") and key not in {("", r) for r in ("pods", "services", "configmaps", "serviceaccounts")} | {("apps", r) for r in ("deployments", "statefulsets", "daemonsets", "replicasets")} | {("batch", r) for r in ("jobs", "cronjobs")} | {("gateway.networking.k8s.io", "httproutes"), ("policy", "poddisruptionbudgets"), ("autoscaling", "horizontalpodautoscalers")} | {("dapr.io", r) for r in DAPR_APPLICATION}:
                    problems.append(path + ": unsupported application-package kind requires a reviewed forward grant; environment Dapr/provider resources cannot be granted")
                else:
                    seen.add(key)
        if path.endswith("applicationPackageResources"):
            mandatory = {("apps", "deployments"), ("", "services"), ("gateway.networking.k8s.io", "httproutes")}
            if not mandatory <= seen:
                problems.append(path + ": must cover the approved chart deployment, Service and HTTPRoute kinds")
    env_maps = required("rbac.environmentConfigMaps", list)
    if env_maps and ("data-service-tls-contract" not in env_maps or any(not isinstance(name, str) or NAME.fullmatch(name) is None for name in env_maps) or len(set(env_maps)) != len(env_maps)):
        problems.append("rbac.environmentConfigMaps: unique exact environment-owned ConfigMap names including the TLS contract required")
    try:
        validate_target(get("target"), root, now)
    except ERROR as error:
        problems.extend(error.problems)

    dns = get("network.dns.peers")
    peers(dns, "network.dns.peers", problems)
    addresses = required("network.dns.resolverAddresses", list)
    if addresses:
        for index, address in enumerate(addresses):
            try:
                if not isinstance(address, str) or "%" in address:
                    raise ValueError()
                parsed = ipaddress.ip_address(address)
                if parsed.is_unspecified or parsed.is_multicast:
                    raise ValueError()
            except (ValueError, TypeError):
                problems.append(f"network.dns.resolverAddresses[{index}]: literal resolver IP required")
        if isinstance(dns, list) and dns and all(isinstance(peer, dict) and set(peer) == {"ipBlock"} for peer in dns):
            try:
                actual = {ipaddress.ip_network(peer["ipBlock"]["cidr"], strict=True).network_address for peer in dns}
                expected = {ipaddress.ip_address(address) for address in addresses if isinstance(address, str)}
                if actual != expected or len(actual) != len(dns) or len(expected) != len(addresses):
                    problems.append("network.dns.peers: literal-only peers must cover exactly the declared resolver addresses without duplicates")
            except (ValueError, TypeError, KeyError):
                pass  # The field-specific peer/address checks report malformed values.
    services = required("network.dataServices", list)
    names = set()
    if services:
        for index, service in enumerate(services):
            prefix = f"network.dataServices[{index}]"
            if not isinstance(service, dict):
                problems.append(prefix + ": service object required")
                continue
            for key in service.keys() - SERVICE_FIELDS:
                problems.append(prefix + "." + key + ": only public native-TLS contract fields permitted")
            name = service.get("name")
            if not isinstance(name, str) or not NAME.fullmatch(name) or len(name) > 50 or name in names:
                problems.append(prefix + ".name: unique DNS-safe name of at most 50 characters required")
            else:
                names.add(name)
            labels(service.get("podLabels"), prefix + ".podLabels", problems)
            labels(service.get("clientPodLabels"), prefix + ".clientPodLabels", problems)
            port = service.get("tlsPort")
            if type(port) is not int or not 1 <= port <= 65535:
                problems.append(prefix + ".tlsPort: integer port in 1..65535 required")
            if service.get("nativeTlsRequired") is not True:
                problems.append(prefix + ".nativeTlsRequired: provider-enforced TLS must be required")
            evidence(service.get("providerContractEvidence"), prefix + ".providerContractEvidence", problems, root)
    flows = get("network.additionalFlows")
    if not isinstance(flows, list):
        problems.append("network.additionalFlows: explicit reviewed list required (empty only with flowReview reasons)")
    else:
        flow_names = set()
        for index, flow in enumerate(flows):
            prefix = f"network.additionalFlows[{index}]"
            if not isinstance(flow, dict):
                problems.append(prefix + ": flow object required")
                continue
            name = flow.get("name")
            if not isinstance(name, str) or not NAME.fullmatch(name) or len(name) > 40 or name in flow_names or name in names:
                problems.append(prefix + ".name: unique DNS-safe flow name required")
            else:
                flow_names.add(name)
            if flow.get("sourceNamespace") == APP and flow.get("destinationNamespace") == DATA:
                problems.append(prefix + ": APP-to-DATA flows must be declared only in the authoritative native-TLS dataServices inventory")
            if flow.get("sourceNamespace") not in (APP, DATA) or flow.get("destinationNamespace") not in (APP, DATA):
                problems.append(prefix + ": additional flows must stay inside staging namespaces")
            for key in ("sourcePodLabels", "destinationPodLabels"):
                labels(flow.get(key), prefix + "." + key, problems)
            if type(flow.get("port")) is not int or not 1 <= flow["port"] <= 65535 or flow.get("protocol") not in ("TCP", "UDP"):
                problems.append(prefix + ": valid specific protocol/port required")
            if not isinstance(flow.get("purpose"), str) or historical._placeholder(flow["purpose"]):
                problems.append(prefix + ".purpose: reviewed purpose required")
    for category in ("replication", "ha", "monitoring", "backup", "custody"):
        required("network.flowReview." + category)

    values = {}
    quota_fields = {**preflight.QUOTA_FIELDS, "requestsEphemeralStorage": "storage", "limitsEphemeralStorage": "storage"}
    limit_fields = {**preflight.LIMIT_FIELDS, "defaultRequestEphemeralStorage": "storage", "defaultLimitEphemeralStorage": "storage"}
    for prefix, fields in (("limitRange", limit_fields), ("quotas.application", quota_fields), ("quotas.data", quota_fields), ("capacity", {"allocatableCpu": "cpu", "allocatableMemory": "memory", "allocatableEphemeralStorage": "storage", "stagingUsableStorage": "storage"})):
        for field, kind in fields.items():
            path = prefix + "." + field
            value = required(path)
            if value:
                try:
                    parsed = preflight.quantity(value, kind)
                    if kind in ("storage", "memory") and parsed != parsed.to_integral_value():
                        raise ValueError("whole-byte quantity required")
                    values[path] = parsed
                except ValueError as error:
                    problems.append(f"{path}: {error}")
    for resource in ("Cpu", "Memory", "EphemeralStorage"):
        low, high = "limitRange.defaultRequest" + resource, "limitRange.defaultLimit" + resource
        if low in values and high in values and (values[low] <= 0 or values[low] > values[high]):
            problems.append(low + ": must be positive and not exceed its limit")
        for scope in ("application", "data"):
            low, high = f"quotas.{scope}.requests{resource}", f"quotas.{scope}.limits{resource}"
            if low in values and high in values and values[low] > values[high]:
                problems.append(low + ": exceeds matching limit")
            for qkind, default in (("requests", "defaultRequest"), ("limits", "defaultLimit")):
                qpath, dpath = f"quotas.{scope}.{qkind}{resource}", f"limitRange.{default}{resource}"
                if qpath in values and dpath in values and values[qpath] < values[dpath]:
                    problems.append(qpath + ": cannot fit one default container")
        capacity_field = {"Cpu": "allocatableCpu", "Memory": "allocatableMemory", "EphemeralStorage": "allocatableEphemeralStorage"}[resource]
        paths_to_sum = [f"quotas.{scope}.limits{resource}" for scope in ("application", "data")]
        cap = "capacity." + capacity_field
        if all(p in values for p in [cap] + paths_to_sum) and sum(values[p] for p in paths_to_sum) > values[cap]:
            problems.append(cap + ": insufficient declared capacity for both namespace limits")
    for path, expected in {"limitRange.defaultRequestCpu": "100m", "limitRange.defaultRequestMemory": "128Mi", "limitRange.defaultLimitCpu": "500m", "limitRange.defaultLimitMemory": "512Mi", "limitRange.pvcMinStorage": "1Gi", "limitRange.pvcMaxStorage": "50Gi"}.items():
        selected(path, expected)
    for scope, pods, claims, storage in (("application", "30", "0", "0"), ("data", "20", "12", "100Gi")):
        for key, expected in {"requestsCpu": "4", "requestsMemory": "8Gi", "limitsCpu": "8", "limitsMemory": "16Gi", "pods": pods, "persistentVolumeClaims": claims, "requestsStorage": storage}.items():
            selected(f"quotas.{scope}.{key}", expected)
    if "capacity.stagingUsableStorage" in values and "quotas.data.requestsStorage" in values and values["capacity.stagingUsableStorage"] < values["quotas.data.requestsStorage"]:
        problems.append("capacity.stagingUsableStorage: must cover data storage quota with independent reserve qualification")
    image = required("probe.image")
    if image and not image_reference(image):
        problems.append("probe.image: qualified digest-pinned image with a valid registry/repository reference required")
    try:
        pinned_sources(root)
    except (ERROR, OSError, ValueError, KeyError) as error:
        problems.extend(error.problems if isinstance(error, ERROR) else [f"source receipts: {error}"])
    if problems:
        raise ERROR(problems)


def labels(value, path, problems):
    if not isinstance(value, dict) or not value:
        problems.append(path + ": nonempty exact pod-label map required")
        return
    for key, val in value.items():
        valid_key = isinstance(key, str) and key.count("/") <= 1
        if valid_key:
            prefix, separator, name = key.rpartition("/")
            valid_key = bool(LABEL_PART.fullmatch(name))
            if separator:
                valid_key = valid_key and len(prefix) <= 253 and all(DNS_LABEL.fullmatch(part) for part in prefix.split("."))
        if not valid_key or not isinstance(val, str) or len(val) > 63 or (val != "" and LABEL_PART.fullmatch(val) is None):
            problems.append(path + ": Kubernetes label key/value syntax required")


def peers(values, path, problems):
    if not isinstance(values, list) or not values:
        problems.append(path + ": actual DNS path requires explicit narrow peers, including NodeLocal/host-network where applicable")
        return
    for index, peer in enumerate(values):
        prefix = f"{path}[{index}]"
        if not isinstance(peer, dict):
            problems.append(prefix + ": peer object required")
        elif set(peer) == {"ipBlock"} and isinstance(peer["ipBlock"], dict) and set(peer["ipBlock"]) == {"cidr"}:
            try:
                if not isinstance(peer["ipBlock"]["cidr"], str) or "/" not in peer["ipBlock"]["cidr"] or "%" in peer["ipBlock"]["cidr"]:
                    raise ValueError()
                block = ipaddress.ip_network(peer["ipBlock"]["cidr"], strict=True)
                if block.prefixlen != block.max_prefixlen or block.is_multicast or block.is_unspecified:
                    raise ValueError()
            except (ValueError, TypeError):
                problems.append(prefix + ": DNS ipBlock must be one resolver host (/32 or /128)")
        elif set(peer) == {"namespaceSelector", "podSelector"}:
            ns = peer["namespaceSelector"]
            if not isinstance(ns, dict) or set(ns) != {"matchLabels"} or not isinstance(ns["matchLabels"], dict) or set(ns["matchLabels"]) != {"kubernetes.io/metadata.name"} or not namespace_name(ns["matchLabels"]["kubernetes.io/metadata.name"]):
                problems.append(prefix + ": exact namespace identity selector required")
            pod = peer["podSelector"]
            labels(pod.get("matchLabels") if isinstance(pod, dict) and set(pod) == {"matchLabels"} else None, prefix + ".podSelector", problems)
        else:
            problems.append(prefix + ": narrow namespace+pod or literal resolver-host peer required")


def assess(inputs, root=PROJECT, now=None):
    problems = []
    try:
        validate(inputs, root, now)
    except ERROR as error:
        problems = error.problems
    dependencies = []
    for story in DEPENDENCIES:
        declared = inputs.get("dependencies") if isinstance(inputs, dict) else None
        item = declared.get(story, {}) if isinstance(declared, dict) else {}
        if not isinstance(item, dict):
            item = {}
        ref_problems = []
        evidence(item.get("evidence"), "dependencies." + story, ref_problems, root)
        dependencies.append({"story": story, "claimedAccepted": item.get("accepted") is True, "sourceBytesValid": not ref_problems,
                             "acceptedForLiveApply": False, "reason": "Independent entitled acceptance and current scope must be verified outside this offline tool", "problems": ref_problems})
    return {"kind": "staging-foundation-offline-assessment-v1", "offlineComplete": not problems, "problems": problems,
            "dependencies": dependencies, "namespaceCreationQualified": False, "applicationDeploymentAuthority": "withheld",
            "approvalGranted": False, "liveAcceptance": False, "evidenceAuthenticated": False,
            "status": "BLOCKED_NOT_FOR_EXECUTION", "parentStoryStatus": "in-progress"}


def resource(api, kind, name, layer, namespace=None, **body):
    metadata = {"name": name, "labels": historical._labels(layer)}
    if namespace:
        metadata["namespace"] = namespace
    return {"apiVersion": api, "kind": kind, "metadata": metadata, **body}


def ns_peer(namespace, pod_labels):
    return {"namespaceSelector": historical._ns_selector(namespace), "podSelector": {"matchLabels": pod_labels}}


def access_layer(inputs):
    """Administrator installs exact grants; never grants application deployment."""
    ids = inputs["identities"]
    output = []

    def rule(group, resources, verbs, names=None):
        value = {"apiGroups": [group], "resources": resources, "verbs": verbs}
        if names:
            value["resourceNames"] = names
        return value

    def cluster_role(name, rules, principal=None):
        output.append(resource("rbac.authorization.k8s.io/v1", "ClusterRole", name, "shared-infrastructure", rules=rules))
        if principal:
            output.append(resource("rbac.authorization.k8s.io/v1", "ClusterRoleBinding", name, "shared-infrastructure",
                                   roleRef={"apiGroup": "rbac.authorization.k8s.io", "kind": "ClusterRole", "name": name}, subjects=[{"kind": "User", "apiGroup": "rbac.authorization.k8s.io", "name": principal}]))

    shared_rules = []
    for group, res, names in (("", ["namespaces"], [APP, DATA]), ("storage.k8s.io", ["storageclasses"], [CLASS]), ("scheduling.k8s.io", ["priorityclasses"], [CLASS]), ("admissionregistration.k8s.io", ["validatingadmissionpolicies", "validatingadmissionpolicybindings"], policy_names())):
        shared_rules += [rule(group, res, ["get", "update", "patch"], names), rule(group, res, ["create"])]
    cluster_role("staging-shared-deploy", shared_rules, ids["sharedInfrastructure"])
    for ns in (APP, DATA):
        rules = [rule("", ["resourcequotas", "limitranges", "configmaps"], READ + ["create", "update", "patch"]),
                 rule("networking.k8s.io", ["networkpolicies"], READ + ["create", "update", "patch"])]
        if ns == DATA:
            rules += [rule("", ["pods", "services", "persistentvolumeclaims", "secrets", "serviceaccounts"], READ + ["create", "update", "patch"]),
                      rule("apps", ["deployments", "statefulsets"], READ + ["create", "update", "patch"]),
                      rule("batch", ["jobs"], READ + ["create", "update", "patch"])]
            rules += [rule(entry["apiGroup"], entry["resources"], READ + ["create", "update", "patch"]) for entry in inputs["rbac"]["providerResources"]]
        rules.append(rule("dapr.io", sorted(DAPR_ENVIRONMENT), READ + ["create", "update", "patch"]))
        observer_rules = [rule("", ["pods", "services", "persistentvolumeclaims", "resourcequotas", "limitranges", "configmaps", "serviceaccounts"], READ), rule("networking.k8s.io", ["networkpolicies"], READ), rule("rbac.authorization.k8s.io", ["roles", "rolebindings"], READ), rule("apps", ["deployments", "statefulsets"], READ), rule("batch", ["jobs"], READ), rule("dapr.io", sorted(DAPR_ENVIRONMENT | DAPR_APPLICATION), READ)]
        if ns == DATA:
            observer_rules += [rule(entry["apiGroup"], entry["resources"], READ) for entry in inputs["rbac"]["providerResources"]]
        for role_name, role_rules, principal in (("staging-environment-deploy", rules, ids["environmentLayer"]),
                                                 ("staging-observer", observer_rules, ids["observer"])):
            output.append(resource("rbac.authorization.k8s.io/v1", "Role", role_name, "shared-infrastructure", ns, rules=role_rules))
            output.append(resource("rbac.authorization.k8s.io/v1", "RoleBinding", role_name, "shared-infrastructure", ns,
                                   roleRef={"apiGroup": "rbac.authorization.k8s.io", "kind": "Role", "name": role_name}, subjects=[{"kind": "User", "apiGroup": "rbac.authorization.k8s.io", "name": principal}]))
    # Definition is reviewable; no binding exists at any candidate stage.
    output.append(resource("rbac.authorization.k8s.io/v1", "Role", "staging-application-deploy", "shared-infrastructure", APP,
                           rules=[rule(entry["apiGroup"], entry["resources"], READ + ["create", "update", "patch"]) for entry in inputs["rbac"]["applicationPackageResources"]]))
    cluster_role("staging-verification-observer", [rule("", ["namespaces", "persistentvolumes"], READ), rule("storage.k8s.io", ["storageclasses"], READ), rule("scheduling.k8s.io", ["priorityclasses"], READ), rule("admissionregistration.k8s.io", ["validatingadmissionpolicies", "validatingadmissionpolicybindings"], READ), rule("rbac.authorization.k8s.io", ["clusterroles", "clusterrolebindings"], READ), rule("authorization.k8s.io", ["subjectaccessreviews"], ["create"])], ids["observer"])
    return output


def policy_names():
    return ["staging-pvc", "staging-pv", "staging-pod", "staging-owned-objects"]


def policy(name, rules, conditions, expressions):
    policy_doc = resource("admissionregistration.k8s.io/v1", "ValidatingAdmissionPolicy", name, "shared-infrastructure", spec={
        "failurePolicy": "Fail", "matchConstraints": {"resourceRules": rules},
        "matchConditions": [{"name": "scope", "expression": conditions}],
        "validations": [{"expression": expression, "message": message} for expression, message in expressions]})
    binding = resource("admissionregistration.k8s.io/v1", "ValidatingAdmissionPolicyBinding", name, "shared-infrastructure", spec={"policyName": name, "validationActions": ["Deny"]})
    return [policy_doc, binding]


def cel(value):
    return json.dumps(value, sort_keys=True)


def admission_layer(inputs):
    ids, storage = inputs["identities"], inputs["storage"]
    cas_config = json.dumps([{"name": "StorageType", "value": "hostpath"}, {"name": "BasePath", "value": storage["basePath"]}, storage["projectQuota"]], sort_keys=True)
    staging = f"request.namespace in {cel([APP, DATA])}"
    output = []
    pvc_rules = [{"apiGroups": [""], "apiVersions": ["v1"], "operations": ["CREATE", "UPDATE"], "resources": ["persistentvolumeclaims"]}]
    output += policy("staging-pvc", pvc_rules, staging, [
        (f"request.namespace == {cel(DATA)}", "Application namespace cannot hold PVCs"),
        (f"has(object.spec.storageClassName) && object.spec.storageClassName == {cel(CLASS)}", "Explicit staging StorageClass required; omitted/default/other classes denied"),
        ("!has(object.spec.selector) && !has(object.spec.dataSource) && !has(object.spec.dataSourceRef)", "Static selectors and volume population/cloning sources are not authorized"),
        (f"request.operation == 'CREATE' ? (!has(object.spec.volumeName) || object.spec.volumeName == '') : ((has(oldObject.spec.volumeName) && oldObject.spec.volumeName != '') ? (has(object.spec.volumeName) && object.spec.volumeName == oldObject.spec.volumeName) : ((!has(object.spec.volumeName) || object.spec.volumeName == '') || request.userInfo.username == {cel(storage['binderPrincipal'])}))", "User prebinding denied; only the qualified binding controller may set volumeName, and bound identity cannot change"),
    ])
    pv_ref = f"(has(object.spec.claimRef) && object.spec.claimRef.namespace in {cel([APP, DATA])})"
    old_pv_ref = f"(oldObject != null && has(oldObject.spec.claimRef) && oldObject.spec.claimRef.namespace in {cel([APP, DATA])})"
    scope = f"(has(object.spec.storageClassName) && object.spec.storageClassName == {cel(CLASS)}) || {pv_ref} || (oldObject != null && has(oldObject.spec.storageClassName) && oldObject.spec.storageClassName == {cel(CLASS)}) || {old_pv_ref}"
    output += policy("staging-pv", [{"apiGroups": [""], "apiVersions": ["v1"], "operations": ["CREATE", "UPDATE"], "resources": ["persistentvolumes"]}], scope, [
        (f"object.spec.storageClassName == {cel(CLASS)} && has(object.spec.claimRef) && object.spec.claimRef.namespace == {cel(DATA)} && has(object.spec.claimRef.uid) && object.spec.claimRef.uid != ''", "Staging PV must be provisioned for an identified staging-data claim"),
        (f"has(object.spec.local) && object.spec.local.path.startsWith({cel(storage['basePath'] + '/')}) && !object.spec.local.path.contains('/../') && !object.spec.local.path.endsWith('/..') && object.spec.persistentVolumeReclaimPolicy == 'Retain'", "Only retained local volumes beneath the qualified staging filesystem are allowed"),
        (f"request.operation == 'CREATE' ? request.userInfo.username == {cel(storage['provisionerPrincipal'])} : request.userInfo.username in {cel([storage['provisionerPrincipal'], storage['binderPrincipal']])}", "Only qualified provisioning/binding controllers may create or update staging PVs"),
        ("oldObject == null || (object.spec.storageClassName == oldObject.spec.storageClassName && object.spec.local == oldObject.spec.local && (!has(oldObject.spec.claimRef) || (object.spec.claimRef.namespace == oldObject.spec.claimRef.namespace && object.spec.claimRef.name == oldObject.spec.claimRef.name && object.spec.claimRef.uid == oldObject.spec.claimRef.uid)))", "Existing staging volume identity and claim ownership cannot be moved; controller binding metadata may be added"),
    ])
    output += policy("staging-pod", [{"apiGroups": [""], "apiVersions": ["v1"], "operations": ["CREATE", "UPDATE"], "resources": ["pods", "pods/ephemeralcontainers"]}], staging, [
        (f"has(object.spec.priorityClassName) && object.spec.priorityClassName == {cel(CLASS)} && has(object.spec.priority) && object.spec.priority == -1000 && has(object.spec.preemptionPolicy) && object.spec.preemptionPolicy == 'Never'", "Every staging Pod requires the staging priority and Never preemption, including controller-created Pods"),
        ("!has(object.spec.volumes) || object.spec.volumes.all(v, (has(v.configMap) || has(v.secret) || has(v.projected) || has(v.downwardAPI) || has(v.persistentVolumeClaim) || has(v.emptyDir)) && (!has(v.emptyDir) || ((!has(v.emptyDir.medium) || v.emptyDir.medium == '') && has(v.emptyDir.sizeLimit) && quantity(v.emptyDir.sizeLimit).compareTo(quantity('0')) > 0 && quantity(v.emptyDir.sizeLimit).compareTo(quantity('256Mi')) <= 0)))", "Only PVC, configuration and bounded disk emptyDir volumes are permitted; all direct external/inline or memory volumes denied"),
    ])
    # Scope by object identity/resource, never by removable ownership labels.
    ns_expr = f"request.resource.resource == 'namespaces' && request.name in {cel([APP, DATA])}"
    cluster_expr = f"(request.resource.resource in ['storageclasses', 'priorityclasses'] && request.name == {cel(CLASS)}) || (request.resource.resource in ['clusterroles', 'clusterrolebindings'] && request.name in ['staging-shared-deploy', 'staging-verification-observer'])"
    layer_label = "((object != null && has(object.metadata.labels) && 'hexalith.com/layer' in object.metadata.labels && object.metadata.labels['hexalith.com/layer'] == 'environment') || (oldObject != null && has(oldObject.metadata.labels) && 'hexalith.com/layer' in oldObject.metadata.labels && oldObject.metadata.labels['hexalith.com/layer'] == 'environment'))"
    env_expr = f"request.namespace in {cel([APP, DATA])} && (request.resource.resource in {cel(sorted(PROTECTED - {'configmaps'}))} || (request.resource.resource == 'configmaps' && (request.name in {cel(inputs['rbac']['environmentConfigMaps'])} || {layer_label})))"
    shared_expr = f"({ns_expr}) || ({cluster_expr}) || (request.namespace in {cel([APP, DATA])} && request.resource.resource in ['roles', 'rolebindings'])"
    shared_scope = f"request.userInfo.username == {cel(ids['sharedInfrastructure'])} && request.resource.resource in ['namespaces', 'storageclasses', 'priorityclasses']"
    # Kubernetes excludes admission policies/bindings from native policy matching.
    # Their ownership is RBAC + the exact approved shared deployment workflow.
    rules = [{"apiGroups": ["", "storage.k8s.io", "scheduling.k8s.io", "rbac.authorization.k8s.io", "networking.k8s.io"], "apiVersions": ["*"], "operations": ["CREATE", "UPDATE", "DELETE"], "resources": ["namespaces", "storageclasses", "priorityclasses", "clusterroles", "clusterrolebindings", *sorted(PROTECTED)]}]
    output += policy("staging-owned-objects", rules, f"({ns_expr}) || ({cluster_expr}) || ({env_expr}) || ({shared_scope})", [
        (f"request.operation != 'DELETE'", "Foundation objects require reviewed forward convergence; deletion is blocked"),
        (f"request.userInfo.username != {cel(ids['sharedInfrastructure'])} || ({ns_expr}) || ({cluster_expr})", "Shared create permission is confined to the exact staging resource identities"),
        (f"({shared_expr}) ? request.userInfo.username in {cel([ids['administrator'], ids['sharedInfrastructure']])} : request.userInfo.username == {cel(ids['environmentLayer'])}", "AR-32 owner identity required for protected environment objects"),
        (f"!({ns_expr}) || (has(object.metadata.labels) && object.metadata.labels['hexalith.com/environment'] == 'staging' && object.metadata.labels['pod-security.kubernetes.io/enforce'] == 'restricted' && object.metadata.labels['pod-security.kubernetes.io/audit'] == 'restricted' && object.metadata.labels['pod-security.kubernetes.io/warn'] == 'restricted' && object.metadata.labels['pod-security.kubernetes.io/enforce-version'] == 'latest' && object.metadata.labels['pod-security.kubernetes.io/audit-version'] == 'latest' && object.metadata.labels['pod-security.kubernetes.io/warn-version'] == 'latest')", "Staging namespace Pod Security and environment identity cannot be weakened"),
        ("request.resource.resource != 'priorityclasses' || (object.value == -1000 && object.preemptionPolicy == 'Never' && object.globalDefault == false)", "Staging priority is fixed below planned production priority"),
        (f"request.resource.resource != 'storageclasses' || (object.provisioner == 'openebs.io/local' && object.reclaimPolicy == 'Retain' && object.volumeBindingMode == 'WaitForFirstConsumer' && (!has(object.metadata.annotations) || !('storageclass.kubernetes.io/is-default-class' in object.metadata.annotations) || object.metadata.annotations['storageclass.kubernetes.io/is-default-class'] != 'true'))", "Staging class must remain retained, delayed-binding and non-default"),
        (f"request.resource.resource != 'storageclasses' || (has(object.metadata.annotations) && object.metadata.annotations['cas.openebs.io/config'] == {cel(cas_config)} && (!('storageclass.beta.kubernetes.io/is-default-class' in object.metadata.annotations) || object.metadata.annotations['storageclass.beta.kubernetes.io/is-default-class'] != 'true') && (!has(object.parameters) || object.parameters.size() == 0) && (!has(object.allowVolumeExpansion) || object.allowVolumeExpansion == false))", "StorageClass BasePath/project-quota annotations must retain the exact qualified staging filesystem configuration"),
    ])
    return output


def shared_layer(inputs):
    output = []
    for ns, role in ((APP, "application"), (DATA, "data")):
        doc = resource("v1", "Namespace", ns, "shared-infrastructure")
        doc["metadata"]["labels"].update({**historical._psa(), "hexalith.com/role": role, "kubernetes.io/metadata.name": ns})
        output.append(doc)
    storage = inputs["storage"]
    config = [{"name": "StorageType", "value": "hostpath"}, {"name": "BasePath", "value": storage["basePath"]}, storage["projectQuota"]]
    sc = resource("storage.k8s.io/v1", "StorageClass", CLASS, "shared-infrastructure", provisioner="openebs.io/local", parameters={}, reclaimPolicy="Retain", volumeBindingMode="WaitForFirstConsumer", allowVolumeExpansion=False)
    sc["metadata"]["annotations"] = {"openebs.io/cas-type": "local", "cas.openebs.io/config": json.dumps(config, sort_keys=True), "storageclass.kubernetes.io/is-default-class": "false",
                                         "hexalith.com/encryption-evidence-sha256": storage["encryptionEvidence"]["sha256"], "hexalith.com/custody-evidence-sha256": storage["custodyEvidence"]["sha256"]}
    output += [sc, resource("scheduling.k8s.io/v1", "PriorityClass", CLASS, "shared-infrastructure", value=-1000, globalDefault=False, preemptionPolicy="Never", description="Staging; planned production priority is 1000; actual qualification remains required.")]
    return output


def environment_layer(inputs):
    output = []
    lr = inputs["limitRange"]
    for ns, scope in ((APP, "application"), (DATA, "data")):
        quota = historical._quota(ns, inputs["quotas"][scope], inputs["storage"]["productionStorageClassName"])
        hard = quota["spec"]["hard"]
        hard.update({"requests.ephemeral-storage": inputs["quotas"][scope]["requestsEphemeralStorage"], "limits.ephemeral-storage": inputs["quotas"][scope]["limitsEphemeralStorage"]})
        limits = historical._limit_range(ns, lr)
        limits["spec"]["limits"][0]["defaultRequest"]["ephemeral-storage"] = lr["defaultRequestEphemeralStorage"]
        limits["spec"]["limits"][0]["default"]["ephemeral-storage"] = lr["defaultLimitEphemeralStorage"]
        output += [quota, limits, historical._netpol(ns, "default-deny-all", {"podSelector": {}, "policyTypes": ["Ingress", "Egress"]}), historical._netpol(ns, "allow-qualified-dns", {"podSelector": {}, "policyTypes": ["Egress"], "egress": [{"to": copy.deepcopy(inputs["network"]["dns"]["peers"]), "ports": [{"protocol": "UDP", "port": 53}, {"protocol": "TCP", "port": 53}]}]})]
    flows = [{"name": service["name"], "sourceNamespace": APP, "destinationNamespace": DATA, "sourcePodLabels": service["clientPodLabels"], "destinationPodLabels": service["podLabels"], "protocol": "TCP", "port": service["tlsPort"]} for service in inputs["network"]["dataServices"]] + inputs["network"]["additionalFlows"]
    for flow in sorted(flows, key=lambda value: value["name"]):
        ports = [{"protocol": flow["protocol"], "port": flow["port"]}]
        output += [historical._netpol(flow["sourceNamespace"], "allow-" + flow["name"] + "-egress", {"podSelector": {"matchLabels": flow["sourcePodLabels"]}, "policyTypes": ["Egress"], "egress": [{"to": [ns_peer(flow["destinationNamespace"], flow["destinationPodLabels"])], "ports": ports}]}),
                   historical._netpol(flow["destinationNamespace"], "allow-" + flow["name"] + "-ingress", {"podSelector": {"matchLabels": flow["destinationPodLabels"]}, "policyTypes": ["Ingress"], "ingress": [{"from": [ns_peer(flow["sourceNamespace"], flow["sourcePodLabels"])], "ports": ports}]})]
    output.append(resource("v1", "ConfigMap", "data-service-tls-contract", "environment", DATA, data={"nativeTlsRequired": "true", "services": json.dumps([{key: service[key] for key in sorted(SERVICE_FIELDS)} for service in inputs["network"]["dataServices"]], sort_keys=True), "qualification": "Provider-native TLS handshakes, certificate identity and plaintext denial are separately required; policy ports are not TLS proof."}))
    return output


def render(inputs, root=PROJECT, now=None):
    validate(inputs, root, now)
    access = access_layer(inputs)
    layers = ([doc for doc in access if "namespace" not in doc["metadata"]], admission_layer(inputs), shared_layer(inputs),
              [doc for doc in access if "namespace" in doc["metadata"]], environment_layer(inputs))
    return {name: historical.to_yaml(docs).encode() for name, docs in zip(FILES, layers)}


def build_plan(inputs, input_bytes, manifest_dir, root=PROJECT, now=None):
    expected = render(inputs, root, now)
    if decode_json(input_bytes) != inputs:
        raise ERROR(["inputs: supplied bytes do not describe the assessed inputs"])
    directory = Path(manifest_dir)
    if directory.is_symlink() or not directory.is_dir() or any(parent.is_symlink() for parent in directory.absolute().parents):
        raise ERROR(["manifestDir: existing non-symlink directory required"])
    actual_names = {item.name for item in directory.iterdir()}
    if actual_names != set(FILES):
        raise ERROR(["manifestDir: exact generated manifest set required; missing/extra files refused"])
    manifests = []
    for name, content in expected.items():
        path = directory / name
        if path.is_symlink() or not path.is_file() or path.read_bytes() != content:
            raise ERROR([f"manifestDir.{name}: exact rendered bytes required; modified/symlink file refused"])
        manifests.append({"path": name, "sha256": digest(content), "bytes": len(content)})
    ids = inputs["identities"]
    stages = [
        {"order": 1, "id": "access-bootstrap", "identity": ids["administrator"], "manifest": FILES[0], "scope": "Install cluster grants only; namespace Roles/bindings wait until shared namespaces exist"},
        {"order": 2, "id": "admission", "identity": ids["sharedInfrastructure"], "manifest": FILES[1], "requires": "access-bootstrap cluster grants"},
        {"order": 3, "id": "shared-infrastructure", "identity": ids["sharedInfrastructure"], "manifest": FILES[2], "requires": "all five dependencies independently accepted; admission qualified and exact namespace creation approval"},
        {"order": 4, "id": "namespace-access-bootstrap", "identity": ids["administrator"], "manifest": FILES[3], "scope": "Install deferred namespace Roles/bindings only", "requires": "shared-infrastructure"},
        {"order": 5, "id": "environment-layer", "identity": ids["environmentLayer"], "manifest": FILES[4], "requires": "namespace-access-bootstrap; namespace existence before server dry-run"},
        {"order": 6, "id": "foundation-verification", "identity": ids["observer"], "requires": "positive/negative admission, RBAC, quotas, storage binding, encryption/custody, DNS and CNI proof; no provider/application acceptance yet"},
        {"order": 7, "id": "bounded-provider-bootstrap", "identity": ids["environmentLayer"], "requires": "separate exact provider bytes, digests, service accounts, TLS credentials, deadline and operational authorization; never supplied by fixture"},
        {"order": 8, "id": "final-provider-acceptance", "identity": ids["observer"], "requires": "actual provider native TLS identity/handshakes, plaintext/cross-environment denial, controls and independently accepted live foundation evidence"},
    ]
    for stage in stages:
        stage.update({"executable": False, "targetIdentityCheck": "Immediately before/after: actual API server + CA/SAN + kube-system UID + public credential identity + tool digest/version + attempt expiry must match targetBinding; stop on drift", "applicationDeploymentAuthority": "withheld"})
        if "manifest" in stage:
            stage["proposedOperations"] = [{"operation": "server-dry-run", "manifest": stage["manifest"], "executable": False}, {"operation": "apply-exact-reviewed-bytes", "manifest": stage["manifest"], "executable": False}]
    sources = pinned_sources(root)
    sources["implementation"] = [{"path": "eng/staging-foundation/" + name, "sha256": digest(safe_file("eng/staging-foundation/" + name, root).read_bytes())} for name in ("candidate.py", "candidate_probe.py", "preflight.py", "cni_probe.py")]
    bound = {"kind": "staging-foundation-blocked-plan-v1", "inputSha256": digest(input_bytes), "inputBytes": len(input_bytes), "manifests": manifests,
             "sources": sources, "targetBinding": copy.deepcopy(inputs["target"]), "orderedIdentities": [{"stage": stage["id"], "identity": stage["identity"]} for stage in stages], "stages": stages,
             "assessment": assess(inputs, root, now), "verification": {"observer": ids["observer"], "applicationBindingExists": False, "permissionCheckMethod": "Observer submits SubjectAccessReview queries for exact deploy users and qualified group context; it is never granted impersonation", "deny": ["application data namespace: every write verb on pods/deployments/statefulsets/jobs/cronjobs/PVCs/services/serviceaccounts/configmaps/secrets", "application Secret get/list/watch; deploy/observer identities bind/escalate/impersonate/groups/token requests", "application environment objects in either namespace, shared objects, any RBAC or admission edit", "shared environment workload/data access; environment shared infrastructure/RBAC changes", "deploy/observer identities namespace/PVC/PV deletion; mismatched/default/static/cloned storage and direct inline volumes", "missing/other/system/default priority; privilege/host namespace requests; unbounded temporary storage"], "positive": ["Administrator exact grant bootstrap without issuing credentials", "shared namespace/SC/priority/admission apply", "environment quotas/limits/netpol and bounded data-provider resources", "observer SubjectAccessReview queries and read-only object inspection; no Secret reads or deploy authority", "provisioner PV creation and binder PVC update using qualified actual controller usernames"], "runtimeQualification": False},
             "approvalGranted": False, "liveAcceptance": False, "executable": False, "namespaceCreationQualified": False, "parentStoryStatus": "in-progress", "recovery": "Forward convergence with reviewed exact bytes; immutable storage/priority changes require new names. No namespace/PVC/PV deletion, credential/data rewind or Helm rollback."}
    return {**bound, "planSha256": digest(encoded(bound))}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    for command in ("assess", "render", "plan"):
        sub = commands.add_parser(command)
        sub.add_argument("--inputs", required=True)
        if command == "render":
            sub.add_argument("--output", required=True)
        if command == "plan":
            sub.add_argument("--manifest-dir", required=True)
    args = parser.parse_args(argv)
    try:
        raw, inputs = _read(args.inputs)
        if args.command == "assess":
            result = assess(inputs)
            print(encoded(result).decode(), end="")
            return 0 if result["offlineComplete"] else 2
        if args.command == "plan":
            print(encoded(build_plan(inputs, raw, args.manifest_dir)).decode(), end="")
            return 0
        files = render(inputs)
        output = Path(args.output)
        # Validate every destination before any write; do not overwrite a prior package.
        if output.exists() or output.is_symlink() or any(parent.is_symlink() for parent in output.absolute().parents):
            raise ERROR(["output: new directory without symlink ancestry required"])
        output.mkdir(parents=True)
        for name, content in files.items():
            (output / name).write_bytes(content)
        return 0
    except (ERROR, OSError, ValueError, TypeError, KeyError) as error:
        for problem in error.problems if isinstance(error, ERROR) else [str(error)]:
            print(f"refused: {problem}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
