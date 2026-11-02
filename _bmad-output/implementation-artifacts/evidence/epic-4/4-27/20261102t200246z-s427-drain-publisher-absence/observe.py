"""Read-only 4.27 drain-publisher absence observation. Issues only kubectl `get`/`version`/`config view` calls."""
import datetime
import hashlib
import json
import pathlib
import subprocess
import sys

KUBECTL = "/tmp/mcpcli-418-tools-c8b9570/kubectl"
KUBECTL_SHA256 = "73bb6f5063caadae1e73a39de018d8ad21755984bea35358484db817859e7634"
CONTEXT = "jpiquot@local"
NAMESPACES = {
    "mcpcli-417-20261101-00c6a713": "2f5b7d0a-4988-4380-a026-7b1e1c39855e",
    "mcpcli-417-20261102-6383083d": "2ebf8335-fe6c-4ba9-913c-4207675c7f6d",
}
OWNED_PVS = ["pvc-bda2e9b2-4c98-40a6-9c12-a8a96501ed9b"]
SELECTOR = "dapr.io/app-id=eventstore"
argv_log = []


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def run(*args, allow_notfound=False):
    assert args[0] in ("get", "version", "config"), args
    argv = [KUBECTL, f"--context={CONTEXT}", "--request-timeout=30s", *args]
    argv_log.append(argv[1:])
    r = subprocess.run(argv, capture_output=True, text=True, timeout=60)
    if r.returncode != 0:
        if allow_notfound and "NotFound" in r.stderr:
            return None
        sys.exit(f"read failed: {argv} -> {r.returncode} {r.stderr.strip()}")
    return r.stdout


def main(out_path):
    digest = hashlib.sha256(pathlib.Path(KUBECTL).read_bytes()).hexdigest()
    if digest != KUBECTL_SHA256:
        sys.exit(f"kubectl digest mismatch: {digest}")
    started = now()
    server = run("config", "view", "--minify", "-o", "jsonpath={.clusters[0].cluster.server}")
    version = json.loads(run("version", "-o", "json"))
    kube_system_uid = json.loads(run("get", "namespace", "kube-system", "-o", "json"))["metadata"]["uid"]
    sample_1 = json.loads(run("get", "pods", "-A", "-l", SELECTOR, "-o", "json"))["items"]
    namespaces = {}
    for name, uid in NAMESPACES.items():
        raw = run("get", "namespace", name, "-o", "json", allow_notfound=True)
        namespaces[name] = {"recordedUid": uid, "absent": raw is None,
                            "observedUid": None if raw is None else json.loads(raw)["metadata"]["uid"]}
    all_ns = json.loads(run("get", "namespaces", "-o", "json"))["items"]
    mcpcli_ns = sorted(n["metadata"]["name"] for n in all_ns if n["metadata"]["name"].startswith("mcpcli-"))
    pvs = json.loads(run("get", "pv", "-o", "json"))["items"]
    pv_names = {p["metadata"]["name"] for p in pvs}
    mcpcli_claim_pvs = sorted(p["metadata"]["name"] for p in pvs
                              if (p["spec"].get("claimRef") or {}).get("namespace", "").startswith("mcpcli-"))
    sample_2 = json.loads(run("get", "pods", "-A", "-l", SELECTOR, "-o", "json"))["items"]
    record = {
        "kind": "s427-drain-publisher-absence-observation",
        "observationStartedAt": started,
        "observationEndedAt": now(),
        "readOnly": True,
        "mutationRequests": 0,
        "kubectl": {"path": KUBECTL, "sha256": digest, "clientVersion": version["clientVersion"]["gitVersion"]},
        "target": {"context": CONTEXT, "server": server, "serverVersion": version["serverVersion"]["gitVersion"],
                   "kubeSystemNamespaceUid": kube_system_uid},
        "publisherSelector": SELECTOR,
        "publisherSelectorSource": "/home/administrator/hexalith-recovery-evidence/4-0/20261101t131121z/tools-complete-v3/mem.py:109",
        "globalPublisherPodsFirstRead": [f'{p["metadata"]["namespace"]}/{p["metadata"]["name"]}' for p in sample_1],
        "globalPublisherPodsLastRead": [f'{p["metadata"]["namespace"]}/{p["metadata"]["name"]}' for p in sample_2],
        "recordedPublisherNamespaces": namespaces,
        "mcpcliNamespacesPresent": mcpcli_ns,
        "ownedPvAbsent": {name: name not in pv_names for name in OWNED_PVS},
        "pvsClaimedFromMcpcliNamespaces": mcpcli_claim_pvs,
        "readArgv": argv_log,
        "qualification": ("Two reads of the unchanged mem.py selector seconds apart; this is not the drain predicate, "
                          "which needs two samples 65 s apart inside a signed successor attempt, plus every other zero-backlog check."),
    }
    pathlib.Path(out_path).write_text(json.dumps(record, indent=2) + "\n")
    print(json.dumps({k: record[k] for k in ("observationStartedAt", "globalPublisherPodsFirstRead", "globalPublisherPodsLastRead",
                                            "recordedPublisherNamespaces", "mcpcliNamespacesPresent", "ownedPvAbsent",
                                            "pvsClaimedFromMcpcliNamespaces", "target")}, indent=1))


if __name__ == "__main__":
    main(sys.argv[1])
