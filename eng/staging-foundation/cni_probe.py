"""Offline CNI probe proposals for Story 4.14; never execute cluster calls."""

import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import re
import sys
from uuid import UUID

NAMESPACE_PREFIX = "hexalith-cni-probe-"
CONTEXT = "jpiquot@local"
PORT = 8080
LABELS = {"app.kubernetes.io/managed-by": "hexalith-platform", "hexalith.com/purpose": "cni-enforcement-probe"}
DIGEST = re.compile(r"^[^\s@]+@sha256:[0-9a-f]{64}$")
ATTEMPT = re.compile(r"^[a-z0-9](?:[a-z0-9-]{0,38}[a-z0-9])?$")
RESULT = re.compile(r"^PROBE_(allowed-server|denied-server)=([01])$")


class ProbeInputError(Exception):
    pass


def namespace_for(attempt_id):
    if not isinstance(attempt_id, str) or ATTEMPT.fullmatch(attempt_id) is None:
        raise ProbeInputError("attempt ID must be a lowercase DNS-safe value of 1..40 characters")
    return NAMESPACE_PREFIX + attempt_id


def deadline_for(cleanup_deadline, now=None, require_future=True):
    if not isinstance(cleanup_deadline, str) or not cleanup_deadline:
        raise ProbeInputError("finite cleanup deadline with timezone is required")
    try:
        deadline = datetime.fromisoformat(cleanup_deadline.replace("Z", "+00:00"))
    except ValueError:
        raise ProbeInputError("cleanup deadline must be an ISO 8601 timestamp") from None
    if deadline.tzinfo is None or deadline.utcoffset() is None:
        raise ProbeInputError("cleanup deadline must include a timezone")
    if require_future and deadline <= (now or datetime.now(timezone.utc)):
        raise ProbeInputError("cleanup deadline must be in the future")
    return deadline


def _labels(attempt_id):
    return {**LABELS, "hexalith.com/probe-attempt": attempt_id}


def _security():
    return {"allowPrivilegeEscalation": False, "runAsNonRoot": True, "runAsUser": 65532, "runAsGroup": 65532,
            "capabilities": {"drop": ["ALL"]}, "seccompProfile": {"type": "RuntimeDefault"}, "readOnlyRootFilesystem": True}


def _pod(namespace, labels, name, role, image, command, restart, ready=False):
    container = {"name": "probe", "image": image, "command": command, "securityContext": _security(),
                 "volumeMounts": [{"name": "tmp", "mountPath": "/tmp"}],
                 "resources": {"requests": {"cpu": "10m", "memory": "16Mi"}, "limits": {"cpu": "50m", "memory": "32Mi"}}}
    if ready:
        container["readinessProbe"] = {"tcpSocket": {"port": PORT}, "periodSeconds": 2, "failureThreshold": 30}
    return {"apiVersion": "v1", "kind": "Pod",
            "metadata": {"name": name, "namespace": namespace, "labels": {**labels, "role": role}},
            "spec": {"restartPolicy": restart, "automountServiceAccountToken": False,
                     "volumes": [{"name": "tmp", "emptyDir": {"sizeLimit": "8Mi"}}], "containers": [container]}}


def _service(namespace, labels, name, role):
    return {"apiVersion": "v1", "kind": "Service", "metadata": {"name": name, "namespace": namespace, "labels": labels},
            "spec": {"selector": {"role": role}, "ports": [{"port": PORT, "targetPort": PORT, "protocol": "TCP"}]}}


def _policy(namespace, labels, name, spec):
    return {"apiVersion": "networking.k8s.io/v1", "kind": "NetworkPolicy",
            "metadata": {"name": name, "namespace": namespace, "labels": labels}, "spec": spec}


def _client_script(attempt_id):
    return ("echo PROBE_ATTEMPT=%s; for t in allowed-server denied-server; do "
            "if nc -z -w 3 $t %d; then r=0; else r=1; fi; echo PROBE_$t=$r; done" % (attempt_id, PORT))


def manifests(image, dns_namespace, dns_pod_labels, attempt_id, cleanup_deadline):
    """Return manifests for one named attempt and a finite cleanup deadline."""
    namespace = namespace_for(attempt_id)
    deadline_for(cleanup_deadline)
    if not isinstance(image, str) or not DIGEST.fullmatch(image):
        raise ProbeInputError("probe image must be digest pinned (name@sha256:<64 hex>)")
    if not isinstance(dns_namespace, str) or not dns_namespace or not isinstance(dns_pod_labels, dict) or not dns_pod_labels:
        raise ProbeInputError("DNS namespace and pod labels are required")
    if any(not isinstance(k, str) or not k or not isinstance(v, str) or not v for k, v in dns_pod_labels.items()):
        raise ProbeInputError("DNS pod labels must contain nonempty string keys and values")
    labels = _labels(attempt_id)
    ns = {"apiVersion": "v1", "kind": "Namespace", "metadata": {"name": namespace, "labels": {
        **labels, "pod-security.kubernetes.io/enforce": "restricted", "pod-security.kubernetes.io/enforce-version": "latest"},
        "annotations": {"hexalith.com/cleanup-deadline": cleanup_deadline}}}
    server_cmd = ["httpd", "-f", "-p", str(PORT), "-h", "/tmp"]
    docs = [
        ns,
        _policy(namespace, labels, "default-deny-all", {"podSelector": {}, "policyTypes": ["Ingress", "Egress"]}),
        _policy(namespace, labels, "allow-client-dns", {"podSelector": {"matchLabels": {"role": "client"}}, "policyTypes": ["Egress"], "egress": [
            {"to": [{"namespaceSelector": {"matchLabels": {"kubernetes.io/metadata.name": dns_namespace}},
                     "podSelector": {"matchLabels": dns_pod_labels}}],
             "ports": [{"protocol": "UDP", "port": 53}, {"protocol": "TCP", "port": 53}]}]}),
        _policy(namespace, labels, "allow-client-to-allowed-server", {"podSelector": {"matchLabels": {"role": "client"}}, "policyTypes": ["Egress"], "egress": [
            {"to": [{"podSelector": {"matchLabels": {"role": "allowed-server"}}}], "ports": [{"protocol": "TCP", "port": PORT}]}]}),
        _policy(namespace, labels, "allow-allowed-server-ingress", {"podSelector": {"matchLabels": {"role": "allowed-server"}}, "policyTypes": ["Ingress"], "ingress": [
            {"from": [{"podSelector": {"matchLabels": {"role": "client"}}}], "ports": [{"protocol": "TCP", "port": PORT}]}]}),
        _service(namespace, labels, "allowed-server", "allowed-server"),
        _service(namespace, labels, "denied-server", "denied-server"),
        _pod(namespace, labels, "allowed-server", "allowed-server", image, server_cmd, "Always", ready=True),
        _pod(namespace, labels, "denied-server", "denied-server", image, server_cmd, "Always", ready=True),
    ]
    client = _pod(namespace, labels, "client", "client", image, ["sh", "-c", _client_script(attempt_id)], "Never")
    return docs, client


def _kubectl(*args):
    return ["kubectl", "--context", CONTEXT, *args]


def namespace_check_argv(attempt_id):
    return _kubectl("get", "namespace", namespace_for(attempt_id), "-o", "json")


def creation_proposal(attempt_id, cleanup_deadline, collision_observation):
    """Record an absence claim without qualifying any creation action."""
    name = namespace_for(attempt_id)
    deadline_for(cleanup_deadline)
    if collision_observation != {"checkedNamespace": name, "exists": False}:
        raise ProbeInputError("namespace collision or unverified absence; creation refused")
    return {"attemptId": attempt_id, "namespace": name, "absenceObservation": collision_observation,
            "observationVerified": False, "creationActionQualified": False,
            "executable": False, "approvalGranted": False, "liveAcceptance": False,
            "requiresLiveCollisionCheck": namespace_check_argv(attempt_id)}


def cleanup_proposal(attempt_id, cleanup_deadline, created_uid, observed_namespace):
    """Require a fresh matching namespace observation and its creation-time UID."""
    name = namespace_for(attempt_id)
    deadline_for(cleanup_deadline, require_future=False)
    try:
        uid = str(UUID(created_uid))
    except (ValueError, TypeError, AttributeError):
        raise ProbeInputError("creation-time namespace UID is required") from None
    metadata = observed_namespace.get("metadata") if isinstance(observed_namespace, dict) else None
    if not isinstance(metadata, dict) or metadata.get("name") != name or metadata.get("uid") != uid:
        raise ProbeInputError("observed namespace identity or UID mismatch; cleanup refused")
    labels, annotations = metadata.get("labels"), metadata.get("annotations")
    if not isinstance(labels, dict) or any(labels.get(k) != v for k, v in _labels(attempt_id).items()) or \
            not isinstance(annotations, dict) or annotations.get("hexalith.com/cleanup-deadline") != cleanup_deadline:
        raise ProbeInputError("observed namespace attempt ownership mismatch; cleanup refused")
    return {"attemptId": attempt_id, "namespace": name, "observedUid": uid,
            "guardArgv": namespace_check_argv(attempt_id),
            "uidPrecondition": uid, "observationVerified": False, "cleanupQualified": False,
            "executable": False, "approvalGranted": False, "liveAcceptance": False,
            "requiresImmediateRecheck": True}


def argv_sequence(attempt_id, cleanup_deadline, manifest_file, client_file):
    namespace = namespace_for(attempt_id)
    deadline_for(cleanup_deadline)
    return {"executable": False, "approvalGranted": False, "liveAcceptance": False,
            "collisionCheck": namespace_check_argv(attempt_id),
            "creationActionQualified": False,
            "creationProposalRequires": {"checkedNamespace": namespace, "exists": False},
            "manifestFile": manifest_file,
            "postCreateUidCheck": namespace_check_argv(attempt_id),
            "waitServers": _kubectl("-n", namespace, "wait", "--for=condition=Ready", "pod/allowed-server", "pod/denied-server", "--timeout=120s"),
            "runClient": _kubectl("apply", "-f", client_file),
            "waitClient": _kubectl("-n", namespace, "wait", "--for=jsonpath={.status.phase}=Succeeded", "pod/client", "--timeout=120s"),
            "readResult": _kubectl("-n", namespace, "logs", "pod/client"),
            "cleanupIdentityCheck": namespace_check_argv(attempt_id),
            "cleanupQualified": False}


def evaluate(client_log, attempt_id):
    """Pass only a single attempt marker, working control and blocked denied peer."""
    namespace = namespace_for(attempt_id)
    found = []
    markers = []
    for line in client_log.splitlines():
        line = line.rstrip("\r")
        if not line.startswith("PROBE_"):
            continue
        if line.startswith("PROBE_ATTEMPT="):
            markers.append(line.removeprefix("PROBE_ATTEMPT="))
        else:
            match = RESULT.fullmatch(line)
            if match is None:
                return {"attemptId": attempt_id, "namespace": namespace, "passed": False,
                        "reason": "inconclusive: malformed probe output", "cleanupQualified": False,
                        "liveAcceptance": False}
            found.append(match.groups())
    base = {"attemptId": attempt_id, "namespace": namespace, "cleanupQualified": False,
            "liveAcceptance": False,
            "cleanupIdentityCheck": namespace_check_argv(attempt_id)}
    if markers != [attempt_id] or sorted(name for name, _ in found) != ["allowed-server", "denied-server"]:
        return {**base, "passed": False, "reason": "inconclusive: missing, duplicate or wrong-attempt probe output"}
    values = dict(found)
    if values["allowed-server"] != "0":
        return {**base, "passed": False, "reason": "inconclusive: control (allowed) connection failed"}
    if values["denied-server"] != "1":
        return {**base, "passed": False, "reason": "denied peer was reachable; CNI does not enforce NetworkPolicy"}
    return {**base, "passed": True, "reason": "allowed connected and denied was blocked"}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    m = sub.add_parser("manifests")
    m.add_argument("--image", required=True)
    m.add_argument("--dns-namespace", required=True)
    m.add_argument("--dns-pod-label", action="append", required=True, help="key=value, repeatable")
    m.add_argument("--attempt-id", required=True)
    m.add_argument("--cleanup-deadline", required=True)
    m.add_argument("--output", required=True)
    e = sub.add_parser("evaluate")
    e.add_argument("--log-file", required=True)
    e.add_argument("--attempt-id", required=True)
    cr = sub.add_parser("creation-proposal")
    cr.add_argument("--attempt-id", required=True)
    cr.add_argument("--cleanup-deadline", required=True)
    cr.add_argument("--collision-observation", required=True, help="fresh checked-absence JSON file")
    c = sub.add_parser("cleanup-proposal")
    c.add_argument("--attempt-id", required=True)
    c.add_argument("--cleanup-deadline", required=True)
    c.add_argument("--created-uid", required=True)
    c.add_argument("--observed-namespace", required=True, help="fresh kubectl get namespace JSON file")
    args = parser.parse_args(argv)
    try:
        if args.command == "manifests":
            labels = dict(item.split("=", 1) for item in args.dns_pod_label)
            if len(labels) != len(args.dns_pod_label):
                raise ProbeInputError("duplicate DNS pod label")
            docs, client = manifests(args.image, args.dns_namespace, labels, args.attempt_id, args.cleanup_deadline)
            out = Path(args.output)
            sequence = argv_sequence(args.attempt_id, args.cleanup_deadline,
                                     str(out / "probe.yaml"), str(out / "client.yaml"))
            out.mkdir(parents=True, exist_ok=True)
            (out / "probe.yaml").write_text("".join("---\n" + json.dumps(d, indent=2, sort_keys=True) + "\n" for d in docs))
            (out / "client.yaml").write_text("---\n" + json.dumps(client, indent=2, sort_keys=True) + "\n")
            print(json.dumps(sequence, indent=2))
            return 0
        if args.command == "creation-proposal":
            observed = json.loads(Path(args.collision_observation).read_text())
            print(json.dumps(creation_proposal(args.attempt_id, args.cleanup_deadline, observed), indent=2))
            return 0
        if args.command == "cleanup-proposal":
            observed = json.loads(Path(args.observed_namespace).read_text())
            print(json.dumps(cleanup_proposal(args.attempt_id, args.cleanup_deadline, args.created_uid, observed), indent=2))
            return 0
        log = Path(args.log_file).read_text()
        result = evaluate(log, args.attempt_id)
        print(json.dumps(result, indent=2))
        return 0 if result["passed"] else 1
    except (ProbeInputError, ValueError, OSError) as error:
        print(f"refused: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
