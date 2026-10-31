#!/usr/bin/env python3
"""Prepare public guarded patches for one CI reader; never apply or read credentials."""

import argparse
import copy
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "_bmad-output/implementation-artifacts/evidence/2-8/release-phase/registry-reader"
KUBECTL = ["kubectl", "--kubeconfig", "/home/administrator/.kube/config", "--context", "jpiquot@local", "--request-timeout=15s"]
NAMESPACE = "registry-distribution"
CLUSTER_UID = "b5860674-1d1e-4a3b-b413-e1bb3b9a4557"
CM_UID = "d00eeb4b-f507-440f-8328-fb838a7d5da3"
CM_RV = "27899151"
CONFIG_SHA = "90805228cf4ac16752d3bb1ca15b47b92f8c24f1a572b5275f1e54c9eecb0317"
DEPLOY_UID = "20bb657c-b622-4e1b-81a2-f97bc353a811"
DEPLOY_RV = "27899261"
TEMPLATE_SHA = "f2c8c48ee11042922234497d46ac8cec0fae2f9d4e91a4be8cd39f03acfd792d"
SECRET_UID = "dfe9dfb9-0872-4421-a9ea-b9d1a24e0b59"
SECRET_RV = "27899149"
ZOT_IMAGE = "ghcr.io/project-zot/zot-linux-amd64@sha256:95a837a0afacf5b7edc0c92493f04beee6891989b8d2fd50a00cf65a1e6d4fd5"
POLICY = {"users": ["ci-reader"], "actions": ["read"]}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(value):
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"))


def read_kubernetes(arguments):
    # Only the fixed GET calls below reach this executor. No apply/patch/exec mode exists.
    result = subprocess.run(KUBECTL + arguments, capture_output=True, text=True, timeout=20, check=False)
    require(result.returncode == 0, "Pinned Kubernetes read failed; response withheld.")
    return result.stdout


def public_configuration(value, path="config"):
    """Reject literal credential material before retaining configuration source."""
    if isinstance(value, dict):
        for key, child in value.items():
            normalized = key.lower().replace("_", "").replace("-", "")
            sensitive = normalized in {"password", "clientsecret", "token", "accesstoken", "secretkey", "privatekey", "apikey"}
            require(not (sensitive and isinstance(child, str) and child), "Literal credential field refused: " + path + "." + key)
            public_configuration(child, path + "." + key)
    elif isinstance(value, list):
        for child in value:
            public_configuration(child, path)
    elif isinstance(value, str):
        require(not re.search(r"\$2[aby]\$[0-9]{2}\$|-----BEGIN .*PRIVATE KEY-----", value), "Credential-shaped configuration refused.")


def configuration_patch(cm):
    require(cm["metadata"]["uid"] == CM_UID and cm["metadata"]["resourceVersion"] == CM_RV, "ConfigMap identity/resourceVersion drift.")
    require(set(cm["data"]) == {"config.json"}, "ConfigMap key drift.")
    original = cm["data"]["config.json"]
    require(digest(original) == CONFIG_SHA, "ConfigMap configuration drift.")
    config = json.loads(original)
    public_configuration(config)
    access = config["http"]["accessControl"]
    require(set(access["repositories"]) == {"**"}, "Repository-policy scope drift.")
    require("ci-reader" not in canonical(access), "CI reader already appears in policy; refuse overwrite.")
    replacement = copy.deepcopy(config)
    replacement["http"]["accessControl"]["repositories"]["**"]["policies"].append(copy.deepcopy(POLICY))
    comparison = copy.deepcopy(replacement)
    comparison["http"]["accessControl"]["repositories"]["**"]["policies"].pop()
    require(comparison == config, "Existing configuration changed.")
    updated = json.dumps(replacement, indent=2) + "\n"
    patch = [
        {"op": "test", "path": "/metadata/uid", "value": CM_UID},
        {"op": "test", "path": "/metadata/resourceVersion", "value": CM_RV},
        {"op": "test", "path": "/data/config.json", "value": original},
        {"op": "replace", "path": "/data/config.json", "value": updated},
    ]
    return original, updated, patch


def deployment_patch(deployment, config_sha):
    metadata, spec = deployment["metadata"], deployment["spec"]
    require(metadata["uid"] == DEPLOY_UID and metadata["resourceVersion"] == DEPLOY_RV, "Deployment identity/resourceVersion drift.")
    require(spec["replicas"] == 1 and spec["strategy"]["type"] == "Recreate", "Deployment availability/strategy drift.")
    template = spec["template"]
    require(digest(canonical(template)) == TEMPLATE_SHA, "Deployment template drift.")
    containers = template["spec"]["containers"]
    require(len(containers) == 1 and containers[0]["name"] == "zot" and containers[0]["image"] == ZOT_IMAGE, "Registry container drift.")
    mounts = containers[0]["volumeMounts"]
    require(any(m.get("name") == "registry-config" and m.get("subPath") == "config.json" and m.get("mountPath") == "/etc/zot/config.json" for m in mounts), "Config subPath projection drift.")
    annotations = copy.deepcopy(template["metadata"].get("annotations", {}))
    public_configuration(annotations, "annotations")
    require("hexalith.com/ci-reader-config-sha256" not in annotations, "Reload annotation already exists.")
    annotations["hexalith.com/ci-reader-config-sha256"] = config_sha
    return [
        {"op": "test", "path": "/metadata/uid", "value": DEPLOY_UID},
        {"op": "test", "path": "/metadata/resourceVersion", "value": DEPLOY_RV},
        {"op": "test", "path": "/spec/replicas", "value": 1},
        {"op": "test", "path": "/spec/strategy/type", "value": "Recreate"},
        {"op": "test", "path": "/spec/template/spec/containers/0/image", "value": ZOT_IMAGE},
        {"op": "test", "path": "/spec/template/spec/containers/0/volumeMounts", "value": mounts},
        {"op": "test", "path": "/spec/template/spec/volumes", "value": template["spec"]["volumes"]},
        {"op": "add", "path": "/spec/template/metadata/annotations", "value": annotations},
    ]


def prepare():
    cluster_uid = read_kubernetes(["get", "namespace", "kube-system", "-o", "jsonpath={.metadata.uid}"]).strip()
    require(cluster_uid == CLUSTER_UID, "Cluster identity drift.")
    namespace = ["-n", NAMESPACE]
    cm = json.loads(read_kubernetes(namespace + ["get", "configmap", "distribution-registry-config", "-o", "json"]))
    original, updated, cm_patch = configuration_patch(cm)
    # This template emits only metadata and map keys. No Secret value is decoded, retained or returned.
    template = '{{.metadata.uid}}{{"\\n"}}{{.metadata.resourceVersion}}{{"\\n"}}{{range $key, $ignored := .data}}{{$key}}{{"\\n"}}{{end}}'
    secret = read_kubernetes(namespace + ["get", "secret", "distribution-registry-htpasswd", "-o", "go-template=" + template]).splitlines()
    require(secret == [SECRET_UID, SECRET_RV, "htpasswd"], "Secret metadata/key drift.")
    deployment = json.loads(read_kubernetes(namespace + ["get", "deployment", "distribution-registry", "-o", "json"]))
    reload_patch = deployment_patch(deployment, digest(updated))
    pods = json.loads(read_kubernetes(namespace + ["get", "pods", "-l", "app.kubernetes.io/name=distribution-registry", "-o", "json"]))["items"]
    require(len(pods) == 1, "Registry pod count drift.")
    pod = pods[0]
    owner = pod["metadata"]["ownerReferences"]
    require(len(owner) == 1 and owner[0]["kind"] == "ReplicaSet", "Registry pod ownership drift.")
    rs = json.loads(read_kubernetes(namespace + ["get", "replicaset", owner[0]["name"], "-o", "json"]))
    require(rs["metadata"]["uid"] == owner[0]["uid"] and any(o.get("uid") == DEPLOY_UID for o in rs["metadata"].get("ownerReferences", [])), "ReplicaSet ownership drift.")
    statuses = pod["status"].get("containerStatuses", [])
    require(len(statuses) == 1 and statuses[0]["ready"] and statuses[0]["imageID"] == ZOT_IMAGE, "Registry pod readiness/image drift.")
    plan = {
        "preparationOnly": True, "clusterUid": cluster_uid, "context": "jpiquot@local", "namespace": NAMESPACE,
        "configMap": {"name": "distribution-registry-config", "uid": CM_UID, "resourceVersion": CM_RV, "oldSha256": CONFIG_SHA, "newSha256": digest(updated)},
        "secretMetadataOnly": {"name": "distribution-registry-htpasswd", "uid": SECRET_UID, "resourceVersion": SECRET_RV, "keys": ["htpasswd"]},
        "deployment": {"name": "distribution-registry", "uid": DEPLOY_UID, "resourceVersion": DEPLOY_RV, "templateSha256": TEMPLATE_SHA, "replicas": 1, "strategy": "Recreate", "image": ZOT_IMAGE},
        "pod": {"name": pod["metadata"]["name"], "uid": pod["metadata"]["uid"], "replicaSetUid": rs["metadata"]["uid"], "imageID": statuses[0]["imageID"]},
        "principal": "ci-reader", "policy": POLICY, "anonymousAndExistingPoliciesUnchanged": True,
        "externalKustomizeSourceLocated": False, "externalSourceUpdated": False,
        "credentialMaterialReadOrGenerated": False, "clusterOrRegistryMutationPerformed": False,
        "knownReadProbe": {"repository": "eventstore", "version": "3.130.1", "source": "1de42b706f52af0da6513f0b7663002606d17156", "indexDigest": "sha256:cbaed729ae671eff9ecf69b40c8f646e0a27aeae62fe0481ce6be98cfa840c86", "amd64Digest": "sha256:ed507d6478ee211a9da77f1d9affe3364859906584391417b782a6fa28fe64ca"},
    }
    return {"configuration.current.json": original, "configuration.ci-reader.json": updated, "configmap.patch.json": cm_patch, "reload.patch.json": reload_patch, "plan.json": plan}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Revalidate the reviewed artifacts without rewriting them.")
    args = parser.parse_args()
    artifacts = prepare()
    if args.check:
        for name, value in artifacts.items():
            expected = value if isinstance(value, str) else json.dumps(value, indent=2) + "\n"
            require((OUTPUT / name).read_text() == expected, "Reviewed artifact drift: " + name)
    else:
        require(not OUTPUT.exists(), "Preparation evidence already exists; use --check or retain/review a separate attempt.")
        OUTPUT.mkdir(parents=True)
        for name, value in artifacts.items():
            text = value if isinstance(value, str) else json.dumps(value, indent=2) + "\n"
            (OUTPUT / name).write_text(text)
    print("Pinned registry preparation verified; no credential or cluster mutation.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, subprocess.TimeoutExpired, json.JSONDecodeError):
        raise SystemExit("Registry preparation refused; no apply performed. Inspect pinned identities locally; do not rebase silently.") from None
