"""Collect bounded, sanitized native observations using the existing named context.

No object mutation, Secret request, credential export, domain call, or approval is
implemented. SelfSubjectRulesReview via kubectl auth is non-persistent inspection.
The existing inline client credential is frozen in sealed anonymous memory for
the child clients; no credential bytes are printed or written to disk/evidence.
"""

import argparse
import base64
from contextlib import contextmanager
from datetime import datetime, timezone
import hashlib
import fcntl
import json
import os
from pathlib import Path
import selectors
import socket
import ssl
import subprocess
import time

CONTEXT = "jpiquot@local"
SERVER = "https://192.168.1.30:6443"
CLUSTER_UID = "b5860674-1d1e-4a3b-b413-e1bb3b9a4557"
MAX_BYTES = 16 * 1024 * 1024
QUERIES = (
    ("cluster", ["get", "namespace", "kube-system", "-o", "json"]),
    ("identity", ["auth", "whoami", "-o", "json"]),
    ("rights", ["auth", "can-i", "--list", "--namespace", "hexalith-staging"]),
    ("namespaces", ["get", "namespaces", "-o", "json"]),
    ("workloads", ["get", "deployments,statefulsets,daemonsets,services", "-A", "-o", "json"]),
    ("pods", ["get", "pods", "-A", "-o", "json"]),
    ("ingress", ["get", "ingresses.networking.k8s.io", "-A", "-o", "json"]),
    ("gateways", ["get", "gateways.gateway.networking.k8s.io", "-A", "-o", "json"]),
    ("routes", ["get", "httproutes.gateway.networking.k8s.io", "-A", "-o", "json"]),
    ("components", ["get", "components.dapr.io", "-A", "-o", "json"]),
    ("subscriptions", ["get", "subscriptions.dapr.io", "-A", "-o", "json"]),
    ("daprConfigurations", ["get", "configurations.dapr.io", "-A", "-o", "json"]),
    ("bindings", ["get", "clusterrolebindings,rolebindings", "-A", "-o", "json"]),
)


def now():
    return datetime.now(timezone.utc).isoformat()


def run(arguments, *, pass_fds=()):
    deadline = time.monotonic() + 30
    environment = {key: value for key, value in os.environ.items()
                   if key.lower() not in ("http_proxy", "https_proxy", "all_proxy", "no_proxy")}
    process = subprocess.Popen(arguments, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                               pass_fds=pass_fds, env=environment)
    output = {"stdout": bytearray(), "stderr": bytearray()}
    try:
        with selectors.DefaultSelector() as selector:
            selector.register(process.stdout, selectors.EVENT_READ, "stdout")
            selector.register(process.stderr, selectors.EVENT_READ, "stderr")
            while selector.get_map():
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    raise subprocess.TimeoutExpired(arguments, 30)
                for key, _ in selector.select(min(remaining, 1)):
                    block = os.read(key.fileobj.fileno(), min(65536, MAX_BYTES + 1 - len(output[key.data])))
                    if not block:
                        selector.unregister(key.fileobj)
                        continue
                    output[key.data].extend(block)
                    if len(output[key.data]) > MAX_BYTES:
                        raise ValueError("Native observation exceeds the bound; no partial evidence accepted")
        code = process.wait(timeout=max(0.001, deadline - time.monotonic()))
        return subprocess.CompletedProcess(arguments, code, bytes(output["stdout"]), bytes(output["stderr"]))
    finally:
        if process.poll() is None:
            process.kill()
            process.wait()
        process.stdout.close()
        process.stderr.close()


def identity(metadata):
    return {key: metadata[key] for key in ("name", "namespace", "uid", "resourceVersion", "generation") if key in metadata}


def summarize(kind, payload):
    if kind == "cluster":
        return {"identity": identity(payload["metadata"])}
    if kind == "identity":
        user = payload["status"]["userInfo"]
        return {"username": user["username"], "groups": user.get("groups", []),
                "credentialIds": user.get("extra", {}).get("authentication.kubernetes.io/credential-id", [])}
    rows = []
    for item in payload["items"]:
        row = {"kind": item.get("kind"), **identity(item["metadata"])}
        spec = item.get("spec", {})
        if kind in ("workloads", "pods"):
            pod = spec.get("template", {}).get("spec", spec)
            row["images"] = [container["image"] for field in ("initContainers", "containers") for container in pod.get(field, [])]
            row["serviceAccount"] = pod.get("serviceAccountName")
            if item.get("kind") == "Service":
                row.update({"selector": spec.get("selector", {}), "type": spec.get("type"),
                            "ports": [{key: p[key] for key in ("name", "port", "targetPort", "protocol") if key in p} for p in spec.get("ports", [])]})
            else:
                row["observedGeneration"] = item.get("status", {}).get("observedGeneration")
                row["readyReplicas"] = item.get("status", {}).get("readyReplicas")
                pod_metadata = spec.get("template", {}).get("metadata", item.get("metadata", {}))
                row["dapr"] = {key: value for key, value in pod_metadata.get("annotations", {}).items()
                               if key in ("dapr.io/enabled", "dapr.io/app-id", "dapr.io/app-port", "dapr.io/config")}
                row["imageIds"] = [c.get("imageID") for field in ("containerStatuses", "initContainerStatuses") for c in item.get("status", {}).get(field, [])]
        elif kind == "components":
            row.update({"type": spec.get("type"), "scopes": item.get("scopes", []),
                        "metadataNames": [m["name"] for m in spec.get("metadata", [])]})
        elif kind == "subscriptions":
            row.update({key: spec.get(key) for key in ("pubsubname", "topic", "routes")})
            row["scopes"] = item.get("scopes", [])
        elif kind in ("gateways", "routes", "ingress"):
            row["hostnames"] = spec.get("hostnames", [r.get("host") for r in spec.get("rules", []) if "host" in r])
            row["parentRefs"] = spec.get("parentRefs", [])
            row["listeners"] = [{key: listener[key] for key in ("name", "hostname", "protocol", "port") if key in listener} for listener in spec.get("listeners", [])]
            row["backends"] = [{key: backend[key] for key in ("group", "kind", "name", "namespace", "port", "weight") if key in backend}
                               for rule in spec.get("rules", []) for backend in rule.get("backendRefs", [])]
            row["backends"] += [{"name": service["name"], "port": service.get("port")}
                                for rule in spec.get("rules", []) for path in rule.get("http", {}).get("paths", [])
                                if (service := path.get("backend", {}).get("service"))]
            if service := spec.get("defaultBackend", {}).get("service"):
                row["backends"].append({"name": service["name"], "port": service.get("port")})
            row["conditions"] = []
            condition_sets = [item.get("status", {})] + item.get("status", {}).get("parents", []) + item.get("status", {}).get("listeners", [])
            for status in condition_sets:
                owner = {key: status[key] for key in ("parentRef", "name", "controllerName") if key in status}
                row["conditions"] += [{**{key: condition[key] for key in ("type", "status", "reason", "observedGeneration") if key in condition},
                                       "owner": owner} for condition in status.get("conditions", [])]
        elif kind == "daprConfigurations":
            acl = spec.get("accessControl", {})
            row["mtlsEnabled"] = spec.get("mtls", {}).get("enabled")
            row["accessControl"] = {key: acl[key] for key in ("defaultAction", "trustDomain") if key in acl}
            row["accessControl"]["policies"] = [{**{key: policy[key] for key in ("appId", "namespace", "trustDomain", "defaultAction") if key in policy},
                "operations": [{key: operation[key] for key in ("name", "httpVerb", "action") if key in operation} for operation in policy.get("operations", [])]}
                for policy in acl.get("policies", [])]
        elif kind == "bindings":
            # Keep bindings for the actual observed user and authenticated group only.
            subjects = [s for s in item.get("subjects", []) if (s.get("kind"), s.get("name")) in (("User", "jpiquot"), ("Group", "system:authenticated"))]
            if not subjects:
                continue
            row.update({"subjects": subjects, "roleRef": item.get("roleRef")})
        rows.append(row)
    return {"count": len(payload["items"]), "records": rows}


def certificate(pem):
    der = ssl.PEM_cert_to_DER_cert(pem.decode("ascii"))
    result = subprocess.run(["openssl", "x509", "-noout", "-subject", "-issuer", "-serial", "-dates"], input=pem, capture_output=True, timeout=10, check=True)
    return {"derSha256": hashlib.sha256(der).hexdigest(), "publicDetails": result.stdout.decode().splitlines()}


def public_certificates():
    # Select only public certificate fields, never the config's private key/token.
    template = '{.clusters[0].cluster.certificate-authority-data}{"\\n"}{.users[0].user.client-certificate-data}'
    cmd = ["kubectl", "--context", CONTEXT, "config", "view", "--minify", "--raw", "-o", "jsonpath=" + template]
    result = run(cmd)
    if result.returncode:
        raise ValueError("Public certificate selection failed")
    ca_data, client_data = result.stdout.split(b"\n")
    ca, client = base64.b64decode(ca_data, validate=True), base64.b64decode(client_data, validate=True)
    return ca, client


@contextmanager
def frozen_context():
    """Use one immutable existing client configuration without an on-disk copy."""
    response = run(["kubectl", "--context", CONTEXT, "config", "view", "--minify", "--raw", "-o", "json"])
    if response.returncode:
        raise ValueError("Existing client context unavailable")
    source = json.loads(response.stdout)
    if any(len(source.get(key, [])) != 1 for key in ("clusters", "users", "contexts")):
        raise ValueError("Exactly one existing client context is required")
    cluster, user = source["clusters"][0]["cluster"], source["users"][0]["user"]
    if (cluster.get("server") != SERVER or cluster.get("insecure-skip-tls-verify", False)
            or cluster.get("proxy-url") or cluster.get("tls-server-name") not in (None, "", "192.168.1.30")
            or not cluster.get("certificate-authority-data")
            or set(user) != {"client-certificate-data", "client-key-data"}
            or not all(user.values())):
        raise ValueError("Existing inline certificate context cannot be safely frozen")
    public_identity = (base64.b64decode(cluster["certificate-authority-data"], validate=True),
                       base64.b64decode(user["client-certificate-data"], validate=True))
    # Retain only the selected inline certificate authentication. Exec, external
    # file references, proxy routing, impersonation and other identities cannot run.
    context = {"apiVersion": "v1", "kind": "Config", "current-context": CONTEXT,
               "clusters": [{"name": "fixed", "cluster": {"server": SERVER, "certificate-authority-data": cluster["certificate-authority-data"]}}],
               "users": [{"name": "fixed", "user": user}],
               "contexts": [{"name": CONTEXT, "context": {"cluster": "fixed", "user": "fixed"}}]}
    descriptor = os.memfd_create("timesheets-native-context", os.MFD_ALLOW_SEALING | os.MFD_CLOEXEC)
    try:
        os.fchmod(descriptor, 0o600)
        raw = json.dumps(context).encode()
        offset = 0
        while offset < len(raw):
            offset += os.write(descriptor, raw[offset:])
        fcntl.fcntl(descriptor, fcntl.F_ADD_SEALS,
                    fcntl.F_SEAL_WRITE | fcntl.F_SEAL_GROW | fcntl.F_SEAL_SHRINK | fcntl.F_SEAL_SEAL)
        yield descriptor, public_identity
    finally:
        os.close(descriptor)


def tls_observation(public_identity=None):
    ca, client = public_identity or public_certificates()
    context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
    context.load_verify_locations(cadata=ca.decode("ascii"))
    context.minimum_version = ssl.TLSVersion.TLSv1_2
    context.set_alpn_protocols(["http/1.1"])
    with socket.create_connection(("192.168.1.30", 6443), timeout=15) as connection:
        with context.wrap_socket(connection, server_hostname="192.168.1.30") as transport:
            peer = transport.getpeercert()
            return {"observedAt": now(), "server": SERVER, "ca": certificate(ca), "client": certificate(client),
                    "servedPeerDerSha256": hashlib.sha256(transport.getpeercert(binary_form=True)).hexdigest(),
                    "peerSubject": peer.get("subject"), "peerIssuer": peer.get("issuer"),
                    "peerSubjectAltName": peer.get("subjectAltName"), "peerNotBefore": peer.get("notBefore"), "peerNotAfter": peer.get("notAfter"),
                    "tlsVersion": transport.version(), "cipher": transport.cipher(), "alpn": transport.selected_alpn_protocol(),
                    "verifiedCaAndIpSan": True, "clientPresentedInThisHandshake": False,
                    "authenticatedClientEvidence": "identity query through existing kubectl client",
                    "revocationHistory": "not established; this observation proves current authentication only",
                    "runtime": {"pythonSsl": ssl.OPENSSL_VERSION}}


def collect_snapshot(config_cmd, config_result, descriptor, public_identity):
    result = {"schemaVersion": 1, "kind": "timesheets-native-read-only-observation", "startedAt": now(),
              "context": CONTEXT, "expectedServer": SERVER, "commands": [], "observations": {},
              "operationalApprovalGranted": False, "story36Accepted": False}
    result["commands"].append({"arguments": config_cmd, "exitCode": 0, "rawRetained": False})
    initial_config = config_result.stdout
    target_flags = ["--server=" + SERVER, "--insecure-skip-tls-verify=false", "--tls-server-name=192.168.1.30"]
    context_flags = ["--kubeconfig=/proc/self/fd/" + str(descriptor), "--context", CONTEXT]
    for kind, arguments in QUERIES:
        command = ["kubectl", *context_flags, *target_flags, "--request-timeout=15s", *arguments]
        response = run(command, pass_fds=(descriptor,))
        entry = {"arguments": command, "observedAt": now(), "exitCode": response.returncode, "rawRetained": False}
        result["commands"].append(entry)
        if response.returncode:
            if kind in ("cluster", "identity"):
                raise ValueError("Cluster or authenticated identity unavailable; stopped inspection")
            # Do not store arbitrary CLI diagnostics: plugins/errors may contain credentials.
            error = response.stderr.decode(errors="replace")
            result["observations"][kind] = {"status": "unavailable", "reason": "resource-type-absent" if "doesn't have a resource type" in error else "request-refused-or-failed"}
            continue
        if kind == "rights":
            result["observations"][kind] = {"status": "observed-current", "rulesTable": response.stdout.decode().splitlines(), "historicalRevocationsProved": False}
        else:
            result["observations"][kind] = summarize(kind, json.loads(response.stdout))
        if kind == "cluster" and result["observations"][kind]["identity"]["uid"] != CLUSTER_UID:
            raise ValueError("Cluster incarnation differs; stop further inspection")
        if kind == "identity" and result["observations"][kind]["username"] != "jpiquot":
            raise ValueError("Authenticated principal differs; stop further inspection")
    if run(config_cmd).stdout != initial_config or public_certificates() != public_identity:
        raise ValueError("Native context or public certificate changed during inspection")
    recheck_cmd = ["kubectl", *context_flags, *target_flags, "--request-timeout=15s", *QUERIES[0][1]]
    recheck = run(recheck_cmd, pass_fds=(descriptor,))
    result["commands"].append({"arguments": recheck_cmd, "observedAt": now(), "exitCode": recheck.returncode, "rawRetained": False})
    if recheck.returncode or json.loads(recheck.stdout)["metadata"]["uid"] != CLUSTER_UID:
        raise ValueError("Cluster incarnation unavailable or changed at end of inspection")
    result["observations"]["tls"] = tls_observation(public_identity)
    if "X509SHA256=" + result["observations"]["tls"]["client"]["derSha256"] not in result["observations"]["identity"]["credentialIds"]:
        raise ValueError("Authenticated client differs from selected public certificate")
    result["completedAt"] = now()
    result["credentialCustody"] = "Existing inline client identity frozen in sealed anonymous memory, inherited only by fixed kubectl child clients; no on-disk credential copy"
    result["limits"] = ["Discovery is limited to named resource kinds and the selected context.",
                         "Native rights and live peer verification are observations at their recorded cuts.",
                         "No legacy issuance history, maintenance receipt, application authorization or deployed acceptance is inferred."]
    return result


def collect():
    config_cmd = ["kubectl", "--context", CONTEXT, "config", "view", "--minify", "-o", "json"]
    config_result = run(config_cmd)
    if config_result.returncode:
        raise ValueError("Context configuration unavailable")
    config = json.loads(config_result.stdout)["clusters"][0]["cluster"]
    if config.get("server") != SERVER or config.get("insecure-skip-tls-verify", False):
        raise ValueError("Unexpected target or disabled TLS verification; stopped before network")
    with frozen_context() as (descriptor, public_identity):
        return collect_snapshot(config_cmd, config_result, descriptor, public_identity)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists() or args.output.is_symlink():
        parser.error("Output must be a new file")
    result = collect()
    with os.fdopen(os.open(args.output, os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW, 0o600), "w", encoding="utf-8") as output:
        json.dump(result, output, indent=2)
        output.write("\n")
    print(json.dumps({"output": str(args.output), "completedAt": result["completedAt"], "story36Accepted": False}))


if __name__ == "__main__":
    main()
