"""Calico enforcement probe for the staging foundation (Story 4.14).

Emits the probe manifests, the kubectl argv sequence and a result evaluator.
It never executes anything: no cluster call, no process spawning.  The probe
passes only when the allowed connection succeeds AND the denied connection is
blocked, which proves NetworkPolicy is enforced by the CNI.
"""

import argparse
import json
from pathlib import Path
import re
import sys

NAMESPACE = "hexalith-cni-probe"
CONTEXT = "jpiquot@local"
PORT = 8080
LABELS = {"app.kubernetes.io/managed-by": "hexalith-platform", "hexalith.com/purpose": "cni-enforcement-probe"}
DIGEST = re.compile(r"^[^\s@]+@sha256:[0-9a-f]{64}$")


class ProbeInputError(Exception):
    pass


def _security():
    return {"allowPrivilegeEscalation": False, "runAsNonRoot": True, "runAsUser": 65532, "runAsGroup": 65532,
            "capabilities": {"drop": ["ALL"]}, "seccompProfile": {"type": "RuntimeDefault"}, "readOnlyRootFilesystem": True}


def _pod(name, role, image, command, restart, ready=False):
    container = {"name": "probe", "image": image, "command": command, "securityContext": _security(),
                 "volumeMounts": [{"name": "tmp", "mountPath": "/tmp"}],
                 "resources": {"requests": {"cpu": "10m", "memory": "16Mi"}, "limits": {"cpu": "50m", "memory": "32Mi"}}}
    if ready:
        container["readinessProbe"] = {"tcpSocket": {"port": PORT}, "periodSeconds": 2, "failureThreshold": 30}
    return {"apiVersion": "v1", "kind": "Pod",
            "metadata": {"name": name, "namespace": NAMESPACE, "labels": {**LABELS, "role": role}},
            "spec": {"restartPolicy": restart, "automountServiceAccountToken": False,
                     "volumes": [{"name": "tmp", "emptyDir": {"sizeLimit": "8Mi"}}], "containers": [container]}}


def _service(name, role):
    return {"apiVersion": "v1", "kind": "Service", "metadata": {"name": name, "namespace": NAMESPACE, "labels": LABELS},
            "spec": {"selector": {"role": role}, "ports": [{"port": PORT, "targetPort": PORT, "protocol": "TCP"}]}}


def _policy(name, spec):
    return {"apiVersion": "networking.k8s.io/v1", "kind": "NetworkPolicy",
            "metadata": {"name": name, "namespace": NAMESPACE, "labels": LABELS}, "spec": spec}


def _client_script():
    return ("for t in allowed-server denied-server; do "
            "if nc -z -w 3 $t %d; then r=0; else r=1; fi; echo PROBE_$t=$r; done" % PORT)


def manifests(image, dns_namespace, dns_pod_labels):
    """image must be digest pinned and busybox-compatible (httpd, nc). No default is supplied."""
    if not isinstance(image, str) or not DIGEST.match(image):
        raise ProbeInputError("probe image must be digest pinned (name@sha256:<64 hex>)")
    if not dns_namespace or not dns_pod_labels:
        raise ProbeInputError("DNS namespace and pod labels are required")
    namespace = {"apiVersion": "v1", "kind": "Namespace", "metadata": {"name": NAMESPACE, "labels": {
        **LABELS, "pod-security.kubernetes.io/enforce": "restricted", "pod-security.kubernetes.io/enforce-version": "latest"}}}
    server_cmd = ["httpd", "-f", "-p", str(PORT), "-h", "/tmp"]
    docs = [
        namespace,
        _policy("default-deny-all", {"podSelector": {}, "policyTypes": ["Ingress", "Egress"]}),
        _policy("allow-client-dns", {"podSelector": {"matchLabels": {"role": "client"}}, "policyTypes": ["Egress"], "egress": [
            {"to": [{"namespaceSelector": {"matchLabels": {"kubernetes.io/metadata.name": dns_namespace}},
                     "podSelector": {"matchLabels": dns_pod_labels}}],
             "ports": [{"protocol": "UDP", "port": 53}, {"protocol": "TCP", "port": 53}]}]}),
        _policy("allow-client-to-allowed-server", {"podSelector": {"matchLabels": {"role": "client"}}, "policyTypes": ["Egress"], "egress": [
            {"to": [{"podSelector": {"matchLabels": {"role": "allowed-server"}}}], "ports": [{"protocol": "TCP", "port": PORT}]}]}),
        _policy("allow-allowed-server-ingress", {"podSelector": {"matchLabels": {"role": "allowed-server"}}, "policyTypes": ["Ingress"], "ingress": [
            {"from": [{"podSelector": {"matchLabels": {"role": "client"}}}], "ports": [{"protocol": "TCP", "port": PORT}]}]}),
        _service("allowed-server", "allowed-server"),
        _service("denied-server", "denied-server"),
        _pod("allowed-server", "allowed-server", image, server_cmd, "Always", ready=True),
        _pod("denied-server", "denied-server", image, server_cmd, "Always", ready=True),
    ]
    client = _pod("client", "client", image, ["sh", "-c", _client_script()], "Never")
    return docs, client


def _kubectl(*args):
    return ["kubectl", "--context", CONTEXT, *args]


PROBE_SELECTOR = "hexalith.com/purpose=cni-enforcement-probe"


def cleanup_guard_argv():
    """Read-only: must print the namespace (carrying the probe label) before cleanup is run."""
    return _kubectl("get", "namespace", NAMESPACE, "-l", PROBE_SELECTOR, "-o", "name")


def cleanup_argv():
    # The label selector makes the delete a no-op for a namespace lacking the probe label.
    return _kubectl("delete", "namespace", "-l", PROBE_SELECTOR, f"--field-selector=metadata.name={NAMESPACE}",
                    "--wait=true", "--ignore-not-found=true")


def argv_sequence(manifest_file, client_file):
    return {
        "namespaceCreate": _kubectl("apply", "-f", manifest_file),
        "waitServers": _kubectl("-n", NAMESPACE, "wait", "--for=condition=Ready", "pod/allowed-server", "pod/denied-server", "--timeout=120s"),
        "runClient": _kubectl("apply", "-f", client_file),
        "waitClient": _kubectl("-n", NAMESPACE, "wait", "--for=jsonpath={.status.phase}=Succeeded", "pod/client", "--timeout=120s"),
        "readResult": _kubectl("-n", NAMESPACE, "logs", "pod/client"),
        "cleanupGuard": cleanup_guard_argv(),
        "cleanup": cleanup_argv(),
    }


def evaluate(client_log):
    """Pass only if allowed connects (control) and denied is blocked. Always carries cleanup argv."""
    found = re.findall(r"^PROBE_(allowed-server|denied-server)=([01])\r?$", client_log, re.M)
    result = {"cleanupGuardArgv": cleanup_guard_argv(), "cleanupArgv": cleanup_argv()}
    names = [n for n, _ in found]
    if sorted(names) != ["allowed-server", "denied-server"]:
        return {**result, "passed": False, "reason": "inconclusive: missing, duplicate or unreadable probe output"}
    values = dict(found)
    if values["allowed-server"] != "0":
        return {**result, "passed": False, "reason": "inconclusive: control (allowed) connection failed"}
    if values["denied-server"] != "1":
        return {**result, "passed": False, "reason": "denied peer was reachable; CNI does not enforce NetworkPolicy"}
    return {**result, "passed": True, "reason": "allowed connected and denied was blocked"}


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__)
    sub = p.add_subparsers(dest="command", required=True)
    m = sub.add_parser("manifests")
    m.add_argument("--image", required=True)
    m.add_argument("--dns-namespace", required=True)
    m.add_argument("--dns-pod-label", action="append", required=True, help="key=value, repeatable")
    m.add_argument("--output", required=True)
    e = sub.add_parser("evaluate")
    e.add_argument("--log-file", required=True)
    args = p.parse_args(argv)
    if args.command == "manifests":
        try:
            labels = dict(item.split("=", 1) for item in args.dns_pod_label)
            docs, client = manifests(args.image, args.dns_namespace, labels)
        except (ProbeInputError, ValueError) as error:
            print(f"refused: {error}", file=sys.stderr)
            return 2
        out = Path(args.output)
        out.mkdir(parents=True, exist_ok=True)
        (out / "probe.yaml").write_text("".join("---\n" + json.dumps(d, indent=2, sort_keys=True) + "\n" for d in docs))
        (out / "client.yaml").write_text("---\n" + json.dumps(client, indent=2, sort_keys=True) + "\n")
        print(json.dumps(argv_sequence(str(out / "probe.yaml"), str(out / "client.yaml")), indent=2))
        return 0
    try:
        log = Path(args.log_file).read_text()
    except (OSError, ValueError) as error:
        print(f"refused: log unreadable: {error}", file=sys.stderr)
        return 2
    result = evaluate(log)
    print(json.dumps(result, indent=2))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
