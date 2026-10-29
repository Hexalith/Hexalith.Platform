"""Offline renderer for the staging foundation (Story 4.14).

Builds shared-infrastructure and environment-layer manifests from explicit
inputs and builds the (never executed) apply plan.  Stdlib only; it performs no
cluster call and imports no process-spawning module.  Unknown facts are never
defaulted: every missing or placeholder input is reported and nothing is written.
"""

import argparse
import copy
import json
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
APP_NS = "hexalith-staging"
DATA_NS = "hexalith-staging-data"
PRIORITY_CLASS = "hexalith-staging"
EXPECTED_CONTEXT = "jpiquot@local"
EXPECTED_UID = "b5860674-1d1e-4a3b-b413-e1bb3b9a4557"
PLACEHOLDERS = {"", "todo", "tbd", "unknown", "changeme", "replace", "replace-me", "null", "none", "n/a", "example"}
QUOTA_KEYS = ["requestsCpu", "requestsMemory", "limitsCpu", "limitsMemory", "pods", "persistentVolumeClaims", "requestsStorage"]
LIMIT_KEYS = ["defaultRequestCpu", "defaultRequestMemory", "defaultLimitCpu", "defaultLimitMemory", "pvcMinStorage", "pvcMaxStorage"]

# dotted path -> kind (str | int | list | map)
REQUIRED = {
    "storage.provisioner": "str",
    "storage.parameters": "map",
    "storage.reclaimPolicy": "str",
    "storage.stagingStorageClassName": "str",
    "storage.productionStorageClassName": "str",
    "storage.encryptionAttestation.evidenceRef": "str",
    "storage.encryptionAttestation.attestedBy": "str",
    "custody.holders": "list",
    "priority.stagingValue": "int",
    "priority.productionValue": "int",
    "priority.productionClassName": "str",
    "deployIdentities.sharedInfrastructure": "str",
    "deployIdentities.environmentLayer": "str",
    "deployIdentities.jobs": "list",
    "network.dns.namespace": "str",
    "network.dns.podLabels": "map",
    "network.dataServices": "list",
}
REQUIRED.update({f"limitRange.{k}": "str" for k in LIMIT_KEYS})
for _scope in ("application", "data"):
    for _k in QUOTA_KEYS:
        REQUIRED[f"quotas.{_scope}.{_k}"] = "str"


class InputError(Exception):
    def __init__(self, problems):
        super().__init__("; ".join(problems))
        self.problems = problems


def _get(data, path):
    node = data
    for part in path.split("."):
        if not isinstance(node, dict) or part not in node:
            return None
        node = node[part]
    return node


def _placeholder(value):
    return isinstance(value, str) and (value.strip().lower() in PLACEHOLDERS or (value.startswith("<") and value.endswith(">")))


def validate(inputs):
    """Collect every problem in one pass; raise InputError if any."""
    problems = []
    bad = set()

    def refuse(path, message):
        problems.append(f"{path}: {message}")
        bad.add(path)

    for path, kind in REQUIRED.items():
        value = _get(inputs, path)
        if value is None:
            refuse(path, "required input is missing")
        elif kind == "str" and (not isinstance(value, str) or _placeholder(value)):
            refuse(path, "must be a real non-placeholder string")
        elif kind == "int" and (not isinstance(value, int) or isinstance(value, bool)):
            refuse(path, "must be an integer")
        elif kind == "map" and (not isinstance(value, dict) or not value):
            refuse(path, "must be a non-empty object")
        elif kind == "list" and (not isinstance(value, list) or not value):
            refuse(path, "must be a non-empty list")

    def strings(path, forbid_comma=False):
        if path in bad:
            return
        seen = set()
        for index, item in enumerate(_get(inputs, path)):
            if not isinstance(item, str) or _placeholder(item) or item != item.strip():
                refuse(path, f"entry {index} must be a real trimmed string")
            elif forbid_comma and "," in item:
                refuse(path, f"entry {index} must not contain a comma")
            elif item in seen:
                refuse(path, f"duplicate entry {item!r}")
            else:
                seen.add(item)

    strings("custody.holders", forbid_comma=True)
    strings("deployIdentities.jobs")

    if "storage.parameters" not in bad:
        for key, value in _get(inputs, "storage.parameters").items():
            if not isinstance(key, str) or not isinstance(value, str):
                refuse("storage.parameters", f"key {key!r} and its value must be strings")
    if "network.dns.podLabels" not in bad:
        for key, value in _get(inputs, "network.dns.podLabels").items():
            if not isinstance(value, str):
                refuse("network.dns.podLabels", f"value of {key!r} must be a string")
    if "network.dataServices" not in bad:
        names = set()
        for index, service in enumerate(_get(inputs, "network.dataServices")):
            where = f"network.dataServices[{index}]"
            if not isinstance(service, dict):
                refuse("network.dataServices", f"entry {index} must be an object with name and tlsPort")
                continue
            name, port = service.get("name"), service.get("tlsPort")
            if not isinstance(name, str) or _placeholder(name):
                refuse(where + ".name", "must be a real non-placeholder string")
            elif name in names:
                refuse(where + ".name", f"duplicate service name {name!r}")
            else:
                names.add(name)
            if not isinstance(port, int) or isinstance(port, bool) or not 1 <= port <= 65535:
                refuse(where + ".tlsPort", "must be an integer in 1..65535")

    if "storage.reclaimPolicy" not in bad and _get(inputs, "storage.reclaimPolicy") not in ("Retain", "Delete"):
        refuse("storage.reclaimPolicy", "must be Retain or Delete")
    if not {"storage.stagingStorageClassName", "storage.productionStorageClassName"} & bad and \
            _get(inputs, "storage.stagingStorageClassName") == _get(inputs, "storage.productionStorageClassName"):
        refuse("storage.stagingStorageClassName", "must differ from the production StorageClass")
    if not {"priority.stagingValue", "priority.productionValue"} & bad and \
            _get(inputs, "priority.stagingValue") >= _get(inputs, "priority.productionValue"):
        refuse("priority.stagingValue", "must be lower than priority.productionValue")
    if "priority.productionClassName" not in bad and _get(inputs, "priority.productionClassName") == PRIORITY_CLASS:
        refuse("priority.productionClassName", "must differ from the staging PriorityClass")
    shared, env = _get(inputs, "deployIdentities.sharedInfrastructure"), _get(inputs, "deployIdentities.environmentLayer")
    layer_paths = {"deployIdentities.sharedInfrastructure", "deployIdentities.environmentLayer"}
    if not layer_paths & bad and shared == env:
        refuse("deployIdentities", "shared-infrastructure and environment-layer identities must differ")
    if "deployIdentities.jobs" not in bad and not layer_paths & bad and set(_get(inputs, "deployIdentities.jobs")) & {shared, env}:
        refuse("deployIdentities.jobs", "job identities must differ from the layer deploy identities")
    if problems:
        raise InputError(problems)


def _labels(layer, extra=None):
    labels = {"app.kubernetes.io/managed-by": "hexalith-platform", "hexalith.com/environment": "staging", "hexalith.com/layer": layer}
    labels.update(extra or {})
    return labels


def _psa():
    return {
        "pod-security.kubernetes.io/enforce": "restricted", "pod-security.kubernetes.io/enforce-version": "latest",
        "pod-security.kubernetes.io/audit": "restricted", "pod-security.kubernetes.io/audit-version": "latest",
        "pod-security.kubernetes.io/warn": "restricted", "pod-security.kubernetes.io/warn-version": "latest",
    }


def shared_infrastructure(inputs):
    s = inputs["storage"]
    holders = ",".join(sorted(inputs["custody"]["holders"]))
    layer = "shared-infrastructure"
    namespaces = []
    for name, role in ((APP_NS, "application"), (DATA_NS, "data")):
        labels = _labels(layer, {"kubernetes.io/metadata.name": name, "hexalith.com/role": role})
        labels.update(_psa())
        namespaces.append({"apiVersion": "v1", "kind": "Namespace", "metadata": {"name": name, "labels": labels}})
    storage_class = {
        "apiVersion": "storage.k8s.io/v1", "kind": "StorageClass",
        "metadata": {
            "name": s["stagingStorageClassName"], "labels": _labels(layer),
            "annotations": {
                "hexalith.com/encryption-evidence": s["encryptionAttestation"]["evidenceRef"],
                "hexalith.com/encryption-attested-by": s["encryptionAttestation"]["attestedBy"],
                "hexalith.com/key-custody-holders": holders,
            },
        },
        "provisioner": s["provisioner"], "parameters": copy.deepcopy(s["parameters"]),
        "reclaimPolicy": s["reclaimPolicy"], "volumeBindingMode": "WaitForFirstConsumer", "allowVolumeExpansion": False,
    }
    priority = {
        "apiVersion": "scheduling.k8s.io/v1", "kind": "PriorityClass",
        "metadata": {"name": PRIORITY_CLASS, "labels": _labels(layer)},
        "value": inputs["priority"]["stagingValue"], "globalDefault": False, "preemptionPolicy": "Never",
        "description": "Staging workloads; always lower than the production PriorityClass.",
    }
    return namespaces + [storage_class, priority]


def _quota(namespace, q, prod_sc):
    hard = {
        "requests.cpu": q["requestsCpu"], "requests.memory": q["requestsMemory"],
        "limits.cpu": q["limitsCpu"], "limits.memory": q["limitsMemory"],
        "pods": q["pods"], "persistentvolumeclaims": q["persistentVolumeClaims"], "requests.storage": q["requestsStorage"],
        f"{prod_sc}.storageclass.storage.k8s.io/requests.storage": "0",
        f"{prod_sc}.storageclass.storage.k8s.io/persistentvolumeclaims": "0",
    }
    return {"apiVersion": "v1", "kind": "ResourceQuota",
            "metadata": {"name": "staging-quota", "namespace": namespace, "labels": _labels("environment")},
            "spec": {"hard": hard}}


def _priority_denial(namespace, prod_class):
    # A PriorityClass-scoped quota counts only pods using that class; pods=0 makes the
    # production class unusable here.  (Scoping a quota to the staging class would limit,
    # not prevent, use of the production class, so the denial targets the production name.)
    return {"apiVersion": "v1", "kind": "ResourceQuota",
            "metadata": {"name": "deny-production-priority", "namespace": namespace, "labels": _labels("environment")},
            "spec": {"hard": {"pods": "0"},
                     "scopeSelector": {"matchExpressions": [
                         {"scopeName": "PriorityClass", "operator": "In", "values": [prod_class]}]}}}


def _limit_range(namespace, lr):
    return {"apiVersion": "v1", "kind": "LimitRange",
            "metadata": {"name": "staging-limits", "namespace": namespace, "labels": _labels("environment")},
            "spec": {"limits": [
                {"type": "Container", "defaultRequest": {"cpu": lr["defaultRequestCpu"], "memory": lr["defaultRequestMemory"]},
                 "default": {"cpu": lr["defaultLimitCpu"], "memory": lr["defaultLimitMemory"]}},
                {"type": "PersistentVolumeClaim", "min": {"storage": lr["pvcMinStorage"]}, "max": {"storage": lr["pvcMaxStorage"]}},
            ]}}


def _netpol(namespace, name, spec):
    return {"apiVersion": "networking.k8s.io/v1", "kind": "NetworkPolicy",
            "metadata": {"name": name, "namespace": namespace, "labels": _labels("environment")}, "spec": spec}


def _ns_selector(name):
    return {"matchLabels": {"kubernetes.io/metadata.name": name}}


def environment_layer(inputs):
    dns = inputs["network"]["dns"]
    services = sorted(inputs["network"]["dataServices"], key=lambda x: (x["name"], x["tlsPort"]))
    ports = sorted({svc["tlsPort"] for svc in services})
    port_rules = [{"protocol": "TCP", "port": p} for p in ports]
    out = []
    for ns in (APP_NS, DATA_NS):
        q = inputs["quotas"]["application" if ns == APP_NS else "data"]
        out.append(_quota(ns, q, inputs["storage"]["productionStorageClassName"]))
        out.append(_priority_denial(ns, inputs["priority"]["productionClassName"]))
        out.append(_limit_range(ns, inputs["limitRange"]))
        out.append(_netpol(ns, "default-deny-all", {"podSelector": {}, "policyTypes": ["Ingress", "Egress"]}))
        out.append(_netpol(ns, "allow-dns-egress", {
            "podSelector": {}, "policyTypes": ["Egress"],
            "egress": [{"to": [{"namespaceSelector": _ns_selector(dns["namespace"]), "podSelector": {"matchLabels": dns["podLabels"]}}],
                        "ports": [{"protocol": "UDP", "port": 53}, {"protocol": "TCP", "port": 53}]}]}))
    out.append(_netpol(APP_NS, "allow-egress-to-data-tls", {
        "podSelector": {}, "policyTypes": ["Egress"],
        "egress": [{"to": [{"namespaceSelector": _ns_selector(DATA_NS)}], "ports": port_rules}]}))
    out.append(_netpol(DATA_NS, "allow-ingress-from-application-tls", {
        "podSelector": {}, "policyTypes": ["Ingress"],
        "ingress": [{"from": [{"namespaceSelector": _ns_selector(APP_NS)}], "ports": port_rules}]}))
    out.append({"apiVersion": "v1", "kind": "ConfigMap",
                "metadata": {"name": "data-service-tls-contract", "namespace": DATA_NS, "labels": _labels("environment")},
                "data": {"requireTls": "true", "services": json.dumps(services, sort_keys=True),
                         "note": "Data services accept only these TLS ports; certificates and keys are issued by Stories 4.15/4.16, never here."}})
    return out


def to_yaml(documents):
    """JSON is valid YAML; deterministic multi-document stream."""
    return "".join("---\n" + json.dumps(d, indent=2, sort_keys=True) + "\n" for d in documents)


def render(inputs):
    validate(inputs)
    return {"shared-infrastructure.yaml": to_yaml(shared_infrastructure(inputs)),
            "environment-layer.yaml": to_yaml(environment_layer(inputs))}


def _k(inputs_identity, *args):
    return ["kubectl", "--context", EXPECTED_CONTEXT, "--as", inputs_identity, *args]


def _can_i(identity, verb, resource, ns):
    argv = ["kubectl", "--context", EXPECTED_CONTEXT, "auth", "can-i", verb, resource, "--as", identity]
    if ns:
        argv += ["-n", ns.replace("{app}", APP_NS).replace("{data}", DATA_NS)]
    return argv


def build_apply_plan(inputs, observed_context, observed_uid, manifest_dir="."):
    """Return the concrete BLOCKED plan; refuses on a wrong target or bad inputs."""
    validate(inputs)
    if observed_context != EXPECTED_CONTEXT or observed_uid != EXPECTED_UID:
        raise InputError([f"target mismatch: expected context {EXPECTED_CONTEXT} and cluster UID {EXPECTED_UID}"])
    template = json.loads((HERE / "apply-plan.json").read_text())
    ids = inputs["deployIdentities"]
    sc = inputs["storage"]["stagingStorageClassName"]
    files = {"shared": f"{manifest_dir}/shared-infrastructure.yaml", "environment": f"{manifest_dir}/environment-layer.yaml"}
    stages = []
    for key, ident, file, order in (("shared-infrastructure", ids["sharedInfrastructure"], files["shared"], 1),
                                     ("environment-layer", ids["environmentLayer"], files["environment"], 2)):
        stages.append({"id": key, "order": order, "identity": ident,
                       "dryRunArgv": _k(ident, "apply", "--dry-run=server", "-f", file),
                       "applyArgv": _k(ident, "apply", "-f", file),
                       "executable": False,
                       **({"validOnlyAfter": "shared-infrastructure apply (namespaces must exist for the server dry-run)"} if order == 2 else {})})
    verification = [
        ["kubectl", "--context", EXPECTED_CONTEXT, "get", "namespace", "kube-system", "-o", "jsonpath={.metadata.uid}"],
        ["kubectl", "--context", EXPECTED_CONTEXT, "get", "namespace", APP_NS, DATA_NS, "-o", "json"],
        ["kubectl", "--context", EXPECTED_CONTEXT, "get", "storageclass", sc, inputs["storage"]["productionStorageClassName"], "-o", "json"],
        ["kubectl", "--context", EXPECTED_CONTEXT, "get", "priorityclass", PRIORITY_CLASS, "-o", "json"],
    ]
    for ns in (APP_NS, DATA_NS):
        verification.append(["kubectl", "--context", EXPECTED_CONTEXT, "-n", ns, "get", "resourcequota,limitrange,networkpolicy", "-o", "json"])
    denials = []
    for job in ids["jobs"]:
        for verb, resource, ns in template["jobDenialChecks"]:
            denials.append({"identity": job, "argv": _can_i(job, verb, resource, ns), "expectedAnswer": "no", "expectedExitCode": 1})
    cross = []
    for layer_key, checks in template["crossIdentityDenialChecks"].items():
        for verb, resource, ns in checks:
            cross.append({"identity": ids[layer_key], "argv": _can_i(ids[layer_key], verb, resource, ns),
                          "expectedAnswer": "no", "expectedExitCode": 1})
    plan = {k: v for k, v in template.items() if k not in ("jobDenialChecks", "crossIdentityDenialChecks")}
    plan.update({"liveTargetCheck": "verification[0] must return the target clusterUid immediately before and after every stage",
                 "crossIdentityDenialChecks": cross, "stages": stages, "readOnlyVerification": verification, "jobIdentityDenialChecks": denials})
    return plan


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__)
    sub = p.add_subparsers(dest="command", required=True)
    r = sub.add_parser("render")
    r.add_argument("--inputs", required=True)
    r.add_argument("--output", required=True)
    a = sub.add_parser("plan")
    a.add_argument("--inputs", required=True)
    a.add_argument("--observed-context", required=True)
    a.add_argument("--observed-cluster-uid", required=True)
    a.add_argument("--manifest-dir", default=".")
    args = p.parse_args(argv)
    try:
        try:
            inputs = json.loads(Path(args.inputs).read_text())
        except (OSError, ValueError) as error:
            raise InputError([f"--inputs unreadable or not valid JSON: {error}"])
        if args.command == "render":
            files = render(inputs)
            out = Path(args.output)
            out.mkdir(parents=True, exist_ok=True)
            for name, text in files.items():
                (out / name).write_text(text)
        else:
            print(json.dumps(build_apply_plan(inputs, args.observed_context, args.observed_cluster_uid, args.manifest_dir), indent=2))
    except InputError as error:
        for problem in error.problems:
            print(f"refused: {problem}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
