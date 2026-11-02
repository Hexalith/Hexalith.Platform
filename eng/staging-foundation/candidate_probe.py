"""Offline phase-separated CNI proposal and untrusted-log consistency evaluator.

Every result is unverified and liveAcceptance=false, including a consistent
synthetic result. No caller-provided log authenticates an observation or action.
"""

import argparse
import copy
from datetime import datetime, timedelta, timezone
import json
from pathlib import Path
import sys
from uuid import UUID

import candidate
import cni_probe

PHASES = ("baseline", "ingress-deny", "ingress-restored", "egress-deny", "final-restored")
PEERS = ("allowed-server", "denied-server")
MAX_LIFETIME = timedelta(hours=1)
OBSERVER_ROLE = "cni-probe-observer"


def attempt_times(created_at, deadline, now=None, fresh=False):
    now = now or datetime.now(timezone.utc)
    try:
        created = candidate.timestamp(created_at)
        end = cni_probe.deadline_for(deadline, now)
    except ValueError as error:
        raise cni_probe.ProbeInputError(str(error)) from None
    if not created <= now < end <= created + MAX_LIFETIME or (fresh and created < now - timedelta(minutes=5)):
        raise cni_probe.ProbeInputError("active finite attempt required; creation proposals must be at most five minutes old and cleanup within one hour")
    return created, end


def binding(target, observer, attempt_id, now=None):
    """Require explicit public target and observer facts without authenticating them."""
    try:
        candidate.validate_target(target, now=now)
    except candidate.ERROR as error:
        raise cni_probe.ProbeInputError("; ".join(error.problems)) from None
    problems = []
    candidate.principal(observer, "observer", problems)
    if isinstance(observer, str) and observer.startswith("system:serviceaccount:" + cni_probe.namespace_for(attempt_id) + ":"):
        problems.append("observer: ServiceAccount cannot reside in its own probe namespace")
    if target["attemptId"] != attempt_id:
        problems.append("target.attemptId: must equal this probe attempt ID")
    if problems:
        raise cni_probe.ProbeInputError("; ".join(problems))


def read_argv(target, *args):
    return [target["kubectl"]["path"], "--context", target["context"], "--server", target["server"], *args]


def identity_read(target, attempt_id):
    return read_argv(target, "get", "namespace", cni_probe.namespace_for(attempt_id), "-o", "json")


def observer_documents(namespace, ownership, observer):
    """Per-attempt namespaced observation only; never grants exec or mutation."""
    role = {"apiVersion": "rbac.authorization.k8s.io/v1", "kind": "Role",
            "metadata": {"name": OBSERVER_ROLE, "namespace": namespace, "labels": ownership},
            "rules": [{"apiGroups": [""], "resources": ["pods", "services", "endpoints"], "verbs": candidate.READ},
                      {"apiGroups": [""], "resources": ["pods/log"], "resourceNames": ["client", *PEERS], "verbs": ["get"]},
                      {"apiGroups": ["discovery.k8s.io"], "resources": ["endpointslices"], "verbs": candidate.READ},
                      {"apiGroups": ["networking.k8s.io"], "resources": ["networkpolicies"], "verbs": candidate.READ}]}
    grant = {"apiVersion": "rbac.authorization.k8s.io/v1", "kind": "RoleBinding",
             "metadata": {"name": OBSERVER_ROLE, "namespace": namespace, "labels": ownership},
             "roleRef": {"apiGroup": "rbac.authorization.k8s.io", "kind": "Role", "name": OBSERVER_ROLE},
             "subjects": [{"apiGroup": "rbac.authorization.k8s.io", "kind": "User", "name": observer}]}
    return [role, grant]


def uuid(value, field):
    try:
        parsed = str(UUID(value))
        if parsed != value:
            raise ValueError()
        return parsed
    except (ValueError, TypeError, AttributeError):
        raise cni_probe.ProbeInputError(field + ": canonical creation-time UID required") from None


def phase_policies(namespace, ownership, phase):
    """Independent denied directions; the opposite direction stays allowed."""
    ingress_peers = ["allowed-server"] if phase == "ingress-deny" else list(PEERS)
    egress_peers = ["allowed-server"] if phase == "egress-deny" else list(PEERS)
    output = [cni_probe._policy(namespace, ownership, "client-egress", {
        "podSelector": {"matchLabels": {"role": "client"}}, "policyTypes": ["Egress"],
        "egress": [{"to": [{"podSelector": {"matchLabels": {"role": peer}}} for peer in egress_peers], "ports": [{"protocol": "TCP", "port": cni_probe.PORT}]}]})]
    for peer in PEERS:
        output.append(cni_probe._policy(namespace, ownership, peer + "-ingress", {
            "podSelector": {"matchLabels": {"role": peer}}, "policyTypes": ["Ingress"],
            "ingress": [{"from": [{"podSelector": {"matchLabels": {"role": "client"}}}], "ports": [{"protocol": "TCP", "port": cni_probe.PORT}]}] if peer in ingress_peers else []}))
    return output


def policy_spec(spec):
    """Normalize only API omission of empty rules for a declared policy direction."""
    if not isinstance(spec, dict):
        raise cni_probe.ProbeInputError("NetworkPolicy spec must be an object")
    result = copy.deepcopy(spec)
    if not isinstance(result.get("policyTypes"), list):
        raise cni_probe.ProbeInputError("NetworkPolicy policyTypes must be an explicit list")
    for direction in ("Ingress", "Egress"):
        if direction in result.get("policyTypes", []) and result.get(direction.lower()) is None:
            result[direction.lower()] = []
    return result


def spec_digest(spec):
    return candidate.digest(candidate.encoded(policy_spec(spec)))


def policy_fingerprints(namespace, ownership, phase):
    return [{"namespace": namespace, "name": doc["metadata"]["name"], "specSha256": spec_digest(doc["spec"])}
            for doc in phase_policies(namespace, ownership, phase)]


def deadline_command(command, cleanup_deadline):
    """Propose an absolute UTC guard; no generated command is executed here."""
    epoch = int(candidate.timestamp(cleanup_deadline).timestamp())
    script = ('remaining=$((%d - $(date -u +%%s))); [ "$remaining" -gt 0 ] || exit 124; '
              'exec timeout -s KILL "$remaining" "$@"') % epoch
    return ["sh", "-c", script, "absolute-deadline", *command]


def phase_client_command(attempt_id, phase, cleanup_deadline):
    script = ("echo PROBE_ATTEMPT=%s; echo PROBE_PHASE=%s; "
              "for peer in allowed-server denied-server; do "
              'if timeout -s KILL 5 nc -z -w 3 "$peer" 8080; then result=connected; else result=blocked; fi; '
              "printf 'PROBE_%%s=%%s\\n' \"$peer\" \"$result\"; done") % (attempt_id, phase)
    return deadline_command(["timeout", "-s", "KILL", "15", "sh", "-c", script], cleanup_deadline)


def proposal(image, dns_peers, attempt_id, created_at, cleanup_deadline, collision_observation, target, observer, now=None):
    _, end = attempt_times(created_at, cleanup_deadline, now, fresh=True)
    binding(target, observer, attempt_id, now)
    namespace = cni_probe.namespace_for(attempt_id)
    problems = []
    candidate.peers(dns_peers, "dnsPeers", problems)
    if problems:
        raise cni_probe.ProbeInputError("; ".join(problems))
    if not candidate.image_reference(image):
        raise cni_probe.ProbeInputError("qualified digest-pinned sh/date/timeout/httpd/nc image with a valid repository reference required")
    if collision_observation != {"checkedNamespace": namespace, "exists": False}:
        raise cni_probe.ProbeInputError("namespace collision or unverified absence; creation refused")
    creation = {"attemptId": attempt_id, "namespace": namespace, "absenceObservation": collision_observation,
                "requiresLiveCollisionCheck": identity_read(target, attempt_id), "requiredObserver": observer,
                "observationVerified": False, "creationActionQualified": False,
                "executable": False, "approvalGranted": False, "liveAcceptance": False}
    ownership = cni_probe._labels(attempt_id)
    dns_spec = {"podSelector": {"matchLabels": {"role": "client"}}, "policyTypes": ["Egress"], "egress": [{"to": dns_peers, "ports": [{"protocol": "TCP", "port": 53}, {"protocol": "UDP", "port": 53}]}]}
    ns = {"apiVersion": "v1", "kind": "Namespace", "metadata": {"name": namespace,
        "labels": {**ownership, **candidate.historical._psa()}, "annotations": {"hexalith.com/cleanup-deadline": cleanup_deadline, "hexalith.com/probe-created-at": created_at,
        "hexalith.com/probe-dns-spec-sha256": spec_digest(dns_spec)}}}
    docs = [ns, *observer_documents(namespace, ownership, observer), cni_probe._policy(namespace, ownership, "default-deny-all", {"podSelector": {}, "policyTypes": ["Ingress", "Egress"]}),
            cni_probe._policy(namespace, ownership, "client-dns", dns_spec)]
    for peer in PEERS:
        docs += [cni_probe._service(namespace, ownership, peer, peer), cni_probe._pod(namespace, ownership, peer, peer, image, ["httpd", "-f", "-p", str(cni_probe.PORT), "-h", "/tmp"], "Always", ready=True)]
    # A long-lived client preserves one creation-time Pod UID through every phase.
    docs.append(cni_probe._pod(namespace, ownership, "client", "client", image, ["sh", "-c", "sleep 3600"], "Never"))
    for doc in docs:
        if doc["kind"] == "Pod":
            doc["spec"].update({"priorityClassName": candidate.CLASS, "preemptionPolicy": "Never",
                                "securityContext": {"fsGroup": 65532, "fsGroupChangePolicy": "OnRootMismatch"}})
            container = doc["spec"]["containers"][0]
            container["command"] = deadline_command(container["command"], cleanup_deadline)
            doc["spec"]["activeDeadlineSeconds"] = max(1, int((end - (now or datetime.now(timezone.utc))).total_seconds()))
            doc["spec"]["containers"][0]["resources"]["requests"]["ephemeral-storage"] = "8Mi"
            doc["spec"]["containers"][0]["resources"]["limits"]["ephemeral-storage"] = "16Mi"
    phases = [{"phase": phase, "ordinal": index, "policyDocuments": phase_policies(namespace, ownership, phase),
               "expectedPolicyFingerprints": policy_fingerprints(namespace, ownership, phase),
               "clientMeasurement": {"command": phase_client_command(attempt_id, phase, cleanup_deadline),
                   "execArgv": read_argv(target, "-n", namespace, "exec", "pod/client", "-c", "probe", "--", *phase_client_command(attempt_id, phase, cleanup_deadline)),
                   "requiredAuthority": "Separately authorized active-measurement identity, never the read-only observer; exact client UID checked before and after exec",
                   "maximumSeconds": 15, "executable": False},
               "observerCollection": {"podIdentityAndHealth": read_argv(target, "-n", namespace, "get", "pods", "client", *PEERS, "-o", "json"),
                   "servicesAndEndpoints": read_argv(target, "-n", namespace, "get", "services,endpoints,endpointslices.discovery.k8s.io", "-o", "json"),
                   "policies": read_argv(target, "-n", namespace, "get", "networkpolicies", "client-egress", "allowed-server-ingress", "denied-server-ingress", "-o", "json"),
                   "clientLifecycleLog": read_argv(target, "-n", namespace, "logs", "pod/client", "-c", "probe"), "executable": False},
               "requiredBefore": "Immediate namespace/client/peer UID and Ready+endpoint health checks; exact target/attempt still current",
               "expectedResults": {"allowed-server": "connected", "denied-server": "blocked" if phase in ("ingress-deny", "egress-deny") else "connected"},
               "controlRequirements": "Both peers must be Ready, unchanged and locally listening in every phase. Re-probe fresh connections; policy convergence is independently observed.",
               "executable": False} for index, phase in enumerate(PHASES)]
    return {"kind": "staging-cni-offline-proposal-v1", "attemptId": attempt_id, "namespace": namespace, "createdAt": created_at, "cleanupDeadline": cleanup_deadline,
            "targetBinding": copy.deepcopy(target), "observer": observer, "targetAuthenticated": False, "observerAuthenticated": False,
            "creation": creation, "initialDocuments": docs, "phases": phases,
            "observationAuthority": "Exact observer credentials only; no impersonation. Administrator separately installs the per-attempt read-only Role/Binding after namespace creation. Independent local-listener and mutation evidence requires separately authorized authority.",
            "collectionProcedure": "An independently authorized policy writer applies each exact phase and records convergence. The read-only observer collects target/namespace/Pod/Service/endpoint identity, readiness, policy UIDs and canonical spec hashes before/after each measurement. A separately authorized active-measurement principal checks local listeners and captures the finite exec command stdout/stderr/exit status; exec stdout is not a Pod log. Bind the marked output, policy fingerprints and health to the same phase/UIDs/time. The observer receives no exec or mutation grant.",
            "imageCompatibilityRequired": "Qualified non-root sh, UTC date +%s, timeout -s KILL, nc -z -w, httpd -f and writable fsGroup-backed emptyDir; qualify absolute-deadline behavior and clock accuracy live",
            "requiredLiveBinding": "Fresh authorized target server/CA/UID/tool/public credential binding plus namespace/client/peer creation-time UIDs; no context-name trust",
            "resultEnvelope": {"attemptId": attempt_id, "namespace": namespace, "targetBinding": copy.deepcopy(target), "observer": observer, "namespaceUid": "creation-time UID required", "podUids": {name: "creation-time UID required" for name in ("client", *PEERS)}, "phases": "Exactly five ordered phases with all UIDs, observedAt, peerHealth, results, marked clientLog and policies[{namespace,name,uid,specSha256}] matching expected fingerprints; policy UIDs stay stable"},
            "cleanup": {"uidPreconditionRequired": True, "contentInventoryRequired": True, "immediateOwnershipRecheckRequired": True, "separateOperationalAuthorizationRequired": True, "cleanupQualified": False},
            "creationActionQualified": False, "evidenceAuthenticated": False, "approvalGranted": False, "liveAcceptance": False, "executable": False}


def ownership(attempt_id, namespace_uid, pod_uids, observed_namespace):
    name = cni_probe.namespace_for(attempt_id)
    uid = uuid(namespace_uid, "namespaceUid")
    if not isinstance(pod_uids, dict) or set(pod_uids) != {"client", *PEERS}:
        raise cni_probe.ProbeInputError("podUids: exactly one creation-time UID for client and both peers required")
    for pod, value in pod_uids.items():
        uuid(value, "podUids." + pod)
    if len({uid, *pod_uids.values()}) != 4:
        raise cni_probe.ProbeInputError("namespace and Pod UIDs must be distinct")
    metadata = observed_namespace.get("metadata", {}) if isinstance(observed_namespace, dict) else {}
    if not isinstance(metadata, dict) or metadata.get("name") != name or metadata.get("uid") != uid or not isinstance(metadata.get("labels"), dict) or any(metadata["labels"].get(k) != v for k, v in cni_probe._labels(attempt_id).items()):
        raise cni_probe.ProbeInputError("fresh namespace ownership/UID observation does not match attempt")
    if not isinstance(metadata.get("annotations"), dict):
        raise cni_probe.ProbeInputError("namespace annotations must be an attempt timing object")


def evaluate(records, attempt_id, created_at, cleanup_deadline, namespace_uid, pod_uids, observed_namespace, target, observer, now=None):
    """Check consistency only; passed/qualification are always false offline."""
    base = {"attemptId": attempt_id, "passed": False, "offlineConsistent": False, "evidenceAuthenticated": False,
            "creationActionQualified": False, "cleanupQualified": False, "approvalGranted": False, "liveAcceptance": False, "executable": False}
    try:
        binding(target, observer, attempt_id, now)
        created, end = attempt_times(created_at, cleanup_deadline, now)
        ownership(attempt_id, namespace_uid, pod_uids, observed_namespace)
        namespace = cni_probe.namespace_for(attempt_id)
        annotations = observed_namespace["metadata"].get("annotations", {})
        if annotations.get("hexalith.com/cleanup-deadline") != cleanup_deadline or annotations.get("hexalith.com/probe-created-at") != created_at:
            raise cni_probe.ProbeInputError("namespace attempt timing metadata mismatch")
        if not isinstance(records, dict) or records.get("targetBinding") != target or records.get("observer") != observer or records.get("attemptId") != attempt_id or records.get("namespace") != namespace or records.get("namespaceUid") != namespace_uid or records.get("podUids") != pod_uids:
            raise cni_probe.ProbeInputError("result envelope attempt/namespace/Pod UID mismatch")
        phases = records.get("phases")
        if not isinstance(phases, list) or len(phases) != len(PHASES):
            raise cni_probe.ProbeInputError("exactly five ordered independent phases with restored controls required")
        previous = created
        policy_uids = None
        for ordinal, (expected, record) in enumerate(zip(PHASES, phases)):
            if not isinstance(record, dict) or record.get("phase") != expected or type(record.get("ordinal")) is not int or record["ordinal"] != ordinal or record.get("attemptId") != attempt_id or record.get("namespaceUid") != namespace_uid or record.get("podUids") != pod_uids:
                raise cni_probe.ProbeInputError("phase identity/order/UID mismatch, duplicate or replayed phase")
            observed = candidate.timestamp(record.get("observedAt"))
            if not previous < observed < end or observed > (now or datetime.now(timezone.utc)):
                raise cni_probe.ProbeInputError("phase timestamp is stale, replayed or outside the current attempt")
            previous = observed
            expected_policies = {item["name"]: item for item in policy_fingerprints(namespace, cni_probe._labels(attempt_id), expected)}
            observed_policies = record.get("policies")
            if not isinstance(observed_policies, list) or len(observed_policies) != len(expected_policies):
                raise cni_probe.ProbeInputError("exact phase policy identity/fingerprint observations required")
            current_uids = {}
            for item in observed_policies:
                if not isinstance(item, dict) or set(item) != {"namespace", "name", "uid", "specSha256"} or not isinstance(item.get("name"), str) or item["name"] not in expected_policies or item["name"] in current_uids or any(item.get(key) != value for key, value in expected_policies[item["name"]].items()):
                    raise cni_probe.ProbeInputError("missing, duplicate or wrong-direction phase policy fingerprint")
                current_uids[item["name"]] = uuid(item["uid"], "phase policy UID")
            if len(set(current_uids.values())) != len(current_uids) or set(current_uids.values()) & {namespace_uid, *pod_uids.values()} or (policy_uids is not None and current_uids != policy_uids):
                raise cni_probe.ProbeInputError("phase policy UIDs must be distinct and unchanged across phases")
            policy_uids = current_uids
            health = record.get("peerHealth")
            if not isinstance(health, dict) or set(health) != set(PEERS):
                raise cni_probe.ProbeInputError("both independent peer health controls required in every phase")
            for peer in PEERS:
                state = health[peer]
                if not isinstance(state, dict) or state.get("podUid") != pod_uids[peer] or state.get("ready") is not True or state.get("localListenerHealthy") is not True or state.get("endpointPodUid") != pod_uids[peer]:
                    raise cni_probe.ProbeInputError("peer health/endpoint control failed or wrong peer UID")
                if candidate.timestamp(state.get("observedAt")) != observed:
                    raise cni_probe.ProbeInputError("peer health must belong to this exact phase observation")
            expected_results = {"allowed-server": "connected", "denied-server": "blocked" if expected in ("ingress-deny", "egress-deny") else "connected"}
            if record.get("results") != expected_results:
                raise cni_probe.ProbeInputError("connection results failed positive controls or independent denial phase")
            log = record.get("clientLog")
            expected_log = ["PROBE_ATTEMPT=" + attempt_id, "PROBE_PHASE=" + expected] + ["PROBE_" + peer + "=" + expected_results[peer] for peer in PEERS]
            if not isinstance(log, str) or [line for line in log.splitlines() if line.startswith("PROBE_")] != expected_log:
                raise cni_probe.ProbeInputError("client log attempt/phase/result markers missing, duplicated or inconsistent")
        return {**base, "offlineConsistent": True, "reason": "Caller-supplied records are internally consistent only; authenticate no logs, qualify no live isolation/action/cleanup"}
    except (cni_probe.ProbeInputError, ValueError, TypeError, KeyError) as error:
        return {**base, "reason": str(error)}


def service_matches(obj, expected):
    """Compare requested Service routing fields; tolerate ordinary API allocation/defaults."""
    spec = obj.get("spec")
    allowed = {"selector", "ports", "type", "clusterIP", "clusterIPs", "ipFamilies", "ipFamilyPolicy", "sessionAffinity", "internalTrafficPolicy", "publishNotReadyAddresses", "trafficDistribution", "externalIPs", "sessionAffinityConfig"}
    if not isinstance(spec, dict) or spec.keys() - allowed or spec.get("selector") != expected["spec"]["selector"] or spec.get("type", "ClusterIP") != "ClusterIP" or spec.get("sessionAffinity", "None") != "None" or spec.get("internalTrafficPolicy", "Cluster") != "Cluster" or spec.get("publishNotReadyAddresses", False) is not False or spec.get("externalIPs") or spec.get("trafficDistribution") or spec.get("sessionAffinityConfig"):
        return False
    ports = spec.get("ports")
    if not isinstance(ports, list) or len(ports) != 1 or not isinstance(ports[0], dict):
        return False
    port = ports[0]
    return not port.keys() - {"port", "targetPort", "protocol", "name", "appProtocol"} and not port.get("name") and not port.get("appProtocol") and {"port": port.get("port"), "targetPort": port.get("targetPort", port.get("port")), "protocol": port.get("protocol", "TCP")} == expected["spec"]["ports"][0]


def attributed_reference(reference, known, namespace, namespace_uid):
    """Match a concrete object reference to the exact observed inventory UID."""
    if not isinstance(reference, dict) or not isinstance(reference.get("kind"), str) or not isinstance(reference.get("name"), str) or reference["kind"] == "Event":
        return False
    if reference.get("kind") == "Namespace":
        return reference.get("name") == namespace and reference.get("uid") == namespace_uid and reference.get("apiVersion") == "v1" and reference.get("namespace", "") in ("", namespace)
    obj = known.get((reference["kind"], reference["name"]))
    return obj is not None and reference.get("uid") == obj["metadata"]["uid"] and reference.get("namespace") == namespace and reference.get("apiVersion") == obj["apiVersion"]


def endpoint_attribution(obj, known, namespace):
    """Service identity and exact peer Pod references attribute controller endpoints."""
    metadata = obj["metadata"]
    is_slice = obj["kind"] == "EndpointSlice"
    labels = metadata.get("labels", {})
    name = labels.get("kubernetes.io/service-name") if is_slice and isinstance(labels, dict) else metadata["name"]
    service = known.get(("Service", name)) if isinstance(name, str) else None
    if service is None or name not in PEERS:
        return False
    owners = metadata.get("ownerReferences", [])
    if not isinstance(owners, list) or (is_slice and not owners):
        return False
    if owners and (len(owners) != 1 or not isinstance(owners[0], dict) or any(owners[0].get(key) != value for key, value in {"apiVersion": "v1", "kind": "Service", "name": name, "uid": service["metadata"]["uid"]}.items())):
        return False

    def target(reference):
        return attributed_reference(reference, known, namespace, "") and reference["kind"] == "Pod" and reference["name"] == name

    def addresses(values):
        if not isinstance(values, list):
            return False
        for value in values:
            if not isinstance(value, str):
                return False
            try:
                address = candidate.ipaddress.ip_address(value)
                if address.is_multicast or address.is_unspecified:
                    return False
            except ValueError:
                return False
        return True

    def ports(values):
        return isinstance(values, list) and all(isinstance(port, dict) and type(port.get("port")) is int and port["port"] == cni_probe.PORT and port.get("protocol", "TCP") == "TCP" and not port.get("name") and not port.get("appProtocol") for port in values)

    if is_slice:
        entries = obj.get("endpoints")
        return obj.get("addressType") in ("IPv4", "IPv6") and ports(obj.get("ports", [])) and isinstance(entries, list) and all(isinstance(entry, dict) and target(entry.get("targetRef")) and addresses(entry.get("addresses")) and all(candidate.ipaddress.ip_address(address).version == (4 if obj["addressType"] == "IPv4" else 6) for address in entry["addresses"]) for entry in entries)
    subsets = obj.get("subsets", [])
    return isinstance(subsets, list) and all(isinstance(subset, dict) and ports(subset.get("ports", [])) and all(isinstance(subset.get(field, []), list) and all(isinstance(entry, dict) and target(entry.get("targetRef")) and addresses([entry.get("ip")]) for entry in subset.get(field, [])) for field in ("addresses", "notReadyAddresses")) for subset in subsets)


def cleanup_proposal(attempt_id, created_at, cleanup_deadline, namespace_uid, pod_uids, observed_namespace, inventory, observed_at, target, observer, now=None):
    """Require exact finite owned inventory; emit no delete action."""
    binding(target, observer, attempt_id, now)
    ownership(attempt_id, namespace_uid, pod_uids, observed_namespace)
    annotations = observed_namespace["metadata"]["annotations"]
    if annotations.get("hexalith.com/probe-created-at") != created_at or annotations.get("hexalith.com/cleanup-deadline") != cleanup_deadline:
        raise cni_probe.ProbeInputError("cleanup creation time differs from namespace attempt ownership")
    created, end, observed = candidate.timestamp(created_at), candidate.timestamp(cleanup_deadline), candidate.timestamp(observed_at)
    now = now or datetime.now(timezone.utc)
    if not created < end <= created + MAX_LIFETIME or not created <= observed <= now or observed < now - timedelta(minutes=1):
        raise cni_probe.ProbeInputError("finite attempt and immediate fresh inventory/namespace recheck required for cleanup")
    expected = {("Pod", name): uid for name, uid in pod_uids.items()}
    actual, known = {}, {}
    if not isinstance(inventory, list):
        raise cni_probe.ProbeInputError("complete owned namespace inventory required")
    for obj in inventory:
        if not isinstance(obj, dict):
            raise cni_probe.ProbeInputError("inventory entries must be owned resource objects")
        metadata = obj.get("metadata", {}) if isinstance(obj, dict) else {}
        if not isinstance(metadata, dict) or not isinstance(metadata.get("name"), str) or not isinstance(obj.get("kind"), str):
            raise cni_probe.ProbeInputError("inventory metadata, name and kind must be resource identity objects/strings")
        key = (obj.get("kind"), metadata.get("name")) if isinstance(obj, dict) else (None, None)
        versions = {"Pod": {"v1"}, "Service": {"v1"}, "NetworkPolicy": {"networking.k8s.io/v1"}, "Role": {"rbac.authorization.k8s.io/v1"}, "RoleBinding": {"rbac.authorization.k8s.io/v1"}, "ServiceAccount": {"v1"}, "ConfigMap": {"v1"}, "Endpoints": {"v1"}, "EndpointSlice": {"discovery.k8s.io/v1"}, "Event": {"v1", "events.k8s.io/v1"}}
        if key in actual or obj.get("apiVersion") not in versions.get(obj["kind"], set()) or metadata.get("namespace") != cni_probe.namespace_for(attempt_id):
            raise cni_probe.ProbeInputError("foreign, duplicate or unknown namespace content; cleanup refused")
        actual[key] = uuid(metadata.get("uid"), "inventory UID")
        known[key] = obj
    expected_keys = set(expected) | {("Role", OBSERVER_ROLE), ("RoleBinding", OBSERVER_ROLE)} | {("Service", name) for name in PEERS} | {("NetworkPolicy", name) for name in ("default-deny-all", "client-dns", "client-egress", "allowed-server-ingress", "denied-server-ingress")}
    if not expected_keys <= set(actual) or any(actual.get(key) != value for key, value in expected.items()) or len(set(actual.values())) != len(actual) or namespace_uid in actual.values():
        raise cni_probe.ProbeInputError("inventory must contain exactly the unchanged attempt Pods, Services and policies")
    namespace = cni_probe.namespace_for(attempt_id)
    ownership_labels = cni_probe._labels(attempt_id)
    for key in expected_keys:
        metadata = known[key]["metadata"]
        if not isinstance(metadata.get("labels"), dict) or any(metadata["labels"].get(k) != v for k, v in ownership_labels.items()):
            raise cni_probe.ProbeInputError("attempt object ownership labels changed; cleanup refused")
    for doc in observer_documents(namespace, ownership_labels, observer):
        current = known[(doc["kind"], OBSERVER_ROLE)]
        for field in ("rules",) if doc["kind"] == "Role" else ("roleRef", "subjects"):
            if current.get(field) != doc[field]:
                raise cni_probe.ProbeInputError("observer grant rules/role/subjects drifted from reviewed proposal")
    for peer in PEERS:
        if not service_matches(known[("Service", peer)], cni_probe._service(namespace, ownership_labels, peer, peer)):
            raise cni_probe.ProbeInputError("Service selector/port/type drifted from reviewed proposal")
    dns_hash = annotations.get("hexalith.com/probe-dns-spec-sha256")
    if not isinstance(dns_hash, str) or candidate.SHA.fullmatch(dns_hash) is None or spec_digest(known[("NetworkPolicy", "client-dns")].get("spec")) != dns_hash:
        raise cni_probe.ProbeInputError("DNS policy differs from saved proposal Namespace fingerprint; provenance remains unverified")
    if policy_spec(known[("NetworkPolicy", "default-deny-all")].get("spec")) != {"podSelector": {}, "policyTypes": ["Ingress", "Egress"], "ingress": [], "egress": []}:
        raise cni_probe.ProbeInputError("default-deny policy drifted from reviewed proposal")
    if not any(all(policy_spec(known[("NetworkPolicy", doc["metadata"]["name"])].get("spec")) == policy_spec(doc["spec"]) for doc in phase_policies(namespace, ownership_labels, phase)) for phase in PHASES):
        raise cni_probe.ProbeInputError("policy contents do not match any complete independent-direction phase")
    controller_objects = []
    for key in known.keys() - expected_keys:
        obj = known[key]
        metadata = obj["metadata"]
        if not created <= candidate.timestamp(metadata.get("creationTimestamp")) <= observed:
            raise cni_probe.ProbeInputError("controller object creation must belong to this namespace attempt window")
        attribution = None
        if key == ("ServiceAccount", "default") and not obj.get("secrets") and not obj.get("imagePullSecrets") and not metadata.get("ownerReferences"):
            attribution = "namespace controller default ServiceAccount (fixed name, namespace, observed UID and time; not attempt-owned)"
        elif key == ("ConfigMap", "kube-root-ca.crt") and isinstance(obj.get("data"), dict) and set(obj["data"]) == {"ca.crt"} and isinstance(obj["data"]["ca.crt"], str) and obj["data"]["ca.crt"] and not obj.get("binaryData") and not metadata.get("ownerReferences"):
            attribution = "namespace controller public root CA ConfigMap (fixed name, namespace, observed UID and time; not attempt-owned)"
        elif obj["kind"] in ("Endpoints", "EndpointSlice") and endpoint_attribution(obj, known, namespace):
            attribution = "known Service identity and exact peer Pod references; EndpointSlice requires Service owner UID"
        elif obj["kind"] == "Event" and attributed_reference(obj.get("involvedObject" if obj["apiVersion"] == "v1" else "regarding"), known, namespace, namespace_uid) and (obj.get("related") is None or attributed_reference(obj["related"], known, namespace, namespace_uid)):
            attribution = "event attributed to an exact known namespace/object identity and UID"
        if attribution is None:
            raise cni_probe.ProbeInputError("foreign or unattributed controller/namespace content; cleanup refused")
        controller_objects.append({"kind": key[0], "name": key[1], "uid": actual[key], "attribution": attribution, "attemptOwned": False})
    return {"attemptId": attempt_id, "namespace": cni_probe.namespace_for(attempt_id), "observedUid": namespace_uid,
            "targetBinding": copy.deepcopy(target), "observer": observer, "targetAuthenticated": False, "observerAuthenticated": False,
            "guardArgv": identity_read(target, attempt_id), "requiredObserver": observer, "uidPrecondition": namespace_uid,
            "requiresImmediateRecheck": True, "observationVerified": False, "approvalGranted": False, "executable": False,
            "inventory": inventory, "inventoryObservedAt": observed_at, "inventoryAuthenticated": False, "namespaceContentsQualified": False,
            "controllerObjects": sorted(controller_objects, key=lambda item: (item["kind"], item["name"])), "controllerAttributionAuthenticated": False,
            "savedProposalDnsFingerprintAuthenticated": False, "requiresLiveSavedProposalAnnotationVerification": True,
            "requiredAtomicNamespaceUidPrecondition": namespace_uid, "requiresLiveCompleteInventory": True, "cleanupQualified": False, "liveAcceptance": False}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("proposal", "evaluate", "cleanup-proposal"))
    parser.add_argument("--request", required=True, help="JSON arguments; no request is executed")
    args = parser.parse_args(argv)
    try:
        _, request = candidate._read(args.request)
        function = {"proposal": proposal, "evaluate": evaluate, "cleanup-proposal": cleanup_proposal}[args.command]
        result = function(**request)
        print(candidate.encoded(result).decode(), end="")
        return 0 if args.command != "evaluate" or result["offlineConsistent"] else 2
    except (cni_probe.ProbeInputError, OSError, ValueError, TypeError, KeyError) as error:
        print(f"refused: {error}", file=sys.stderr)
        print(candidate.encoded({"kind": "staging-cni-offline-refusal-v1", "reason": str(error), "offlineConsistent": False,
              "passed": False, "evidenceAuthenticated": False, "creationActionQualified": False, "cleanupQualified": False,
              "approvalGranted": False, "liveAcceptance": False, "executable": False}).decode(), end="")
        return 2


if __name__ == "__main__":
    sys.exit(main())
