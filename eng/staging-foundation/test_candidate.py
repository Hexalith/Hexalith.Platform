"""Offline candidate evidence/manifest boundaries; static CEL is not CEL execution."""

import ast
import contextlib
import copy
from datetime import datetime, timedelta, timezone
import io
import json
from pathlib import Path
import socket
import subprocess
import tempfile
import unittest
from unittest import mock

import candidate

HERE = Path(__file__).resolve().parent


def set_path(data, path, value):
    node = data
    parts = path.split(".")
    for part in parts[:-1]:
        node = node[part]
    node[parts[-1]] = value


def complete_inputs(now=None):
    """Synthetic values exist only in tests; never in the candidate input file."""
    now = now or datetime.now(timezone.utc)
    data = json.loads((HERE / "inputs.candidate.json").read_bytes())
    ref_path = "eng/staging-foundation/gates.assessed.json"
    ref = {"path": ref_path, "sha256": candidate.digest((candidate.PROJECT / ref_path).read_bytes())}
    for path, value in {
        "storage.productionStorageClassName": "synthetic-production", "storage.basePath": "/synthetic-staging/volumes", "storage.productionBasePath": "/synthetic-production/volumes", "storage.backingDevice": "/dev/synthetic-staging", "storage.productionBackingDevice": "/dev/synthetic-production", "storage.administrator": "synthetic-custodian-admin", "storage.deputy": "synthetic-custodian-deputy", "storage.provisionerPrincipal": "system:serviceaccount:synthetic-openebs:provisioner", "storage.binderPrincipal": "system:kube-controller-manager", "priority.productionClassName": "synthetic-production",
        "identities.administrator": "synthetic-administrator", "identities.sharedInfrastructure": "synthetic-shared", "identities.environmentLayer": "synthetic-environment", "identities.applicationDeploy": "synthetic-application", "identities.observer": "synthetic-observer",
        "target.context": "synthetic", "target.server": "https://cluster.invalid:6443", "target.clusterUid": "00000000-0000-4000-8000-000000000001", "target.caSha256": "a" * 64, "target.credentialPublicIdentitySha256": "b" * 64, "target.kubectl.path": "/synthetic/kubectl", "target.kubectl.sha256": "c" * 64, "target.kubectl.version": "synthetic-only", "target.attemptId": "synthetic-attempt", "target.observedAt": (now - timedelta(seconds=5)).isoformat(), "target.expiresAt": (now + timedelta(minutes=30)).isoformat(),
        "network.dns.resolverAddresses": ["169.254.20.10"], "network.dns.peers": [{"ipBlock": {"cidr": "169.254.20.10/32"}}, {"namespaceSelector": {"matchLabels": {"kubernetes.io/metadata.name": "synthetic-dns"}}, "podSelector": {"matchLabels": {"synthetic-app": "dns"}}}],
        "network.dataServices": [{"name": "synthetic-store", "podLabels": {"synthetic-app": "store"}, "clientPodLabels": {"synthetic-client": "store"}, "tlsPort": 15432, "nativeTlsRequired": True, "providerContractEvidence": ref}, {"name": "synthetic-keys", "podLabels": {"synthetic-app": "keys"}, "clientPodLabels": {"synthetic-client": "keys"}, "tlsPort": 18200, "nativeTlsRequired": True, "providerContractEvidence": ref}],
        "network.additionalFlows": [{"name": "synthetic-replication", "sourceNamespace": candidate.DATA, "destinationNamespace": candidate.DATA, "sourcePodLabels": {"synthetic-app": "store"}, "destinationPodLabels": {"synthetic-app": "store"}, "protocol": "TCP", "port": 15433, "purpose": "Synthetic native TLS replication only"}],
        "rbac.providerResources": [{"apiGroup": "synthetic-provider.invalid", "resources": ["syntheticstores"]}], "rbac.applicationPackageResources": [{"apiGroup": "apps", "resources": ["deployments", "statefulsets"]}, {"apiGroup": "", "resources": ["services", "configmaps", "serviceaccounts"]}, {"apiGroup": "gateway.networking.k8s.io", "resources": ["httproutes"]}],
        "capacity.allocatableCpu": "16", "capacity.allocatableMemory": "32Gi", "capacity.allocatableEphemeralStorage": "16Gi", "capacity.stagingUsableStorage": "100Gi", "probe.image": "registry.invalid/probe@sha256:" + "d" * 64,
    }.items():
        set_path(data, path, value)
    for category in data["network"]["flowReview"]:
        data["network"]["flowReview"][category] = "Synthetic reviewed non-applicability or explicit staged flow; not a real operational fact"
    for prefix, key in (("storage", "compatibilityEvidence"), ("storage", "encryptionEvidence"), ("storage", "custodyEvidence"), ("priority", "productionEvidence"), ("identities", "qualificationEvidence"), ("target", "identityEvidence"), ("network", "qualificationEvidence"), ("network", "flowReviewEvidence"), ("capacity", "compositionEvidence"), ("probe", "imageQualificationEvidence")):
        data[prefix][key] = copy.deepcopy(ref)
    data["network"]["dns"]["qualificationEvidence"] = copy.deepcopy(ref)
    data["rbac"]["inventoryEvidence"] = copy.deepcopy(ref)
    return data


def documents(files):
    return {name: [json.loads(doc) for doc in content.decode().split("---\n") if doc.strip()] for name, content in files.items()}


class CandidateTests(unittest.TestCase):
    def setUp(self):
        self.now = datetime.now(timezone.utc)
        self.inputs = complete_inputs(self.now)

    def test_current_candidate_refuses_exact_missing_facts_without_output(self):
        current = json.loads((HERE / "inputs.candidate.json").read_bytes())
        result = candidate.assess(current, now=self.now)
        self.assertFalse(result["offlineComplete"])
        for field in ("storage.basePath", "storage.productionStorageClassName", "storage.binderPrincipal", "identities.applicationDeploy", "network.dataServices", "network.dns.peers", "probe.image", "capacity.allocatableCpu"):
            self.assertTrue(any(field in problem for problem in result["problems"]), field)
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp) / "not-created"
            with contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(2, candidate.main(["render", "--inputs", str(HERE / "inputs.candidate.json"), "--output", str(output)]))
            self.assertFalse(output.exists())

    def test_complete_synthetic_candidate_is_deterministic_and_remains_blocked(self):
        candidate.validate(self.inputs, now=self.now)
        first = candidate.render(self.inputs, now=self.now)
        self.assertEqual(first, candidate.render(copy.deepcopy(self.inputs), now=self.now))
        self.assertEqual(tuple(first), candidate.FILES)
        assessed = candidate.assess(self.inputs, now=self.now)
        self.assertTrue(assessed["offlineComplete"])
        for key in ("approvalGranted", "liveAcceptance", "namespaceCreationQualified", "evidenceAuthenticated"):
            self.assertIs(assessed[key], False)
        self.assertEqual(assessed["parentStoryStatus"], "in-progress")

    def test_every_required_operational_fact_refuses_when_removed(self):
        for path in ("storage.productionStorageClassName", "storage.basePath", "storage.productionBasePath", "storage.backingDevice", "storage.productionBackingDevice", "storage.compatibilityEvidence", "storage.encryptionEvidence", "storage.custodyEvidence", "storage.administrator", "storage.deputy", "storage.provisionerPrincipal", "storage.binderPrincipal", "priority.productionClassName", "priority.productionEvidence", "identities.administrator", "identities.sharedInfrastructure", "identities.environmentLayer", "identities.applicationDeploy", "identities.observer", "identities.qualificationEvidence", "target.server", "target.clusterUid", "target.caSha256", "target.credentialPublicIdentitySha256", "target.kubectl.path", "target.kubectl.sha256", "target.kubectl.version", "target.identityEvidence", "target.attemptId", "network.dns.peers", "network.dns.resolverAddresses", "network.dataServices", "network.flowReviewEvidence", "capacity.compositionEvidence", "probe.image", "probe.imageQualificationEvidence"):
            with self.subTest(path=path):
                data = copy.deepcopy(self.inputs)
                set_path(data, path, None)
                self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"])

    def test_unsafe_storage_identity_or_capacity_relationships_refuse(self):
        cases = [("storage.basePath", "/synthetic-production/volumes/child"), ("storage.backingDevice", "/dev/synthetic-production"), ("storage.basePath", "/mnt/../production"), ("storage.productionStorageClassName", candidate.CLASS), ("storage.deputy", self.inputs["storage"]["administrator"]), ("storage.binderPrincipal", self.inputs["identities"]["applicationDeploy"]), ("identities.sharedInfrastructure", self.inputs["identities"]["environmentLayer"]), ("capacity.allocatableCpu", "15"), ("capacity.allocatableMemory", "31Gi"), ("capacity.stagingUsableStorage", "99Gi"), ("capacity.allocatableEphemeralStorage", "15Gi"), ("priority.stagingValue", 1001)]
        for path, value in cases:
            with self.subTest(path=path, value=value):
                data = copy.deepcopy(self.inputs)
                set_path(data, path, value)
                self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"])

    def test_quantity_and_budget_refusals(self):
        for path, values in {"quotas.application.requestsEphemeralStorage": ["-1", "1e99999", "1wat", "1m", "9Gi"], "quotas.application.limitsEphemeralStorage": ["1Mi", "0", "1e-1"], "limitRange.defaultRequestEphemeralStorage": ["0", "512Mi"], "quotas.data.pods": ["1.5", "1"]}.items():
            for value in values:
                with self.subTest(path=path, value=value):
                    data = copy.deepcopy(self.inputs)
                    set_path(data, path, value)
                    self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"])

    def test_dns_services_and_flows_require_narrow_actual_definitions(self):
        cases = [("network.dns.peers", [{"ipBlock": {"cidr": "0.0.0.0/0"}}]), ("network.dns.peers", [{"namespaceSelector": {}}]), ("network.dns.resolverAddresses", ["resolver.invalid"]), ("network.additionalFlows", None), ("network.flowReview.backup", None), ("probe.image", "registry.invalid/probe:latest")]
        for path, value in cases:
            with self.subTest(path=path):
                data = copy.deepcopy(self.inputs)
                set_path(data, path, value)
                self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"])
        for key, value in (("podLabels", {}), ("tlsPort", True), ("nativeTlsRequired", False), ("clientPodLabels", {})):
            data = copy.deepcopy(self.inputs)
            data["network"]["dataServices"][0][key] = value
            self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"])
        data = copy.deepcopy(self.inputs)
        data["network"]["additionalFlows"][0]["destinationNamespace"] = "production"
        self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"])

    def test_historical_target_or_expired_attempt_cannot_be_current(self):
        for key, value in (("observedAt", (self.now - timedelta(days=1)).isoformat()), ("expiresAt", (self.now - timedelta(seconds=1)).isoformat()), ("observedAt", "2026-10-29T17:30:45.207505+00:00"), ("expiresAt", "2026-11-01T23:00:00Z"), ("observedAt", self.now.replace(tzinfo=None).isoformat()), ("server", "https://user:password@cluster.invalid/path")):
            with self.subTest(key=key):
                data = copy.deepcopy(self.inputs)
                data["target"][key] = value
                self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"])

    def test_evidence_hash_traversal_and_symlinks_refuse(self):
        for ref in ({"path": "eng/staging-foundation/gates.assessed.json", "sha256": "0" * 64}, {"path": "../outside", "sha256": "0" * 64}, {"path": "/etc/passwd", "sha256": "0" * 64}):
            data = copy.deepcopy(self.inputs)
            data["storage"]["encryptionEvidence"] = ref
            self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"])
        with tempfile.TemporaryDirectory(dir=HERE) as temp:
            link = Path(temp) / "linked"
            link.symlink_to(HERE / "gates.assessed.json")
            data = copy.deepcopy(self.inputs)
            data["storage"]["encryptionEvidence"] = {"path": str(link.relative_to(candidate.PROJECT)), "sha256": self.inputs["storage"]["encryptionEvidence"]["sha256"]}
            self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"])

    def test_all_dependency_claims_and_auth_flags_never_grant_live_acceptance(self):
        for story in candidate.DEPENDENCIES:
            data = copy.deepcopy(self.inputs)
            data["dependencies"][story] = {"accepted": True, "evidence": data["storage"]["encryptionEvidence"]}
            data["liveAuthorization"] = data["storage"]["encryptionEvidence"]
            result = candidate.assess(data, now=self.now)
            self.assertTrue(result["offlineComplete"])
            self.assertTrue(any(item["claimedAccepted"] for item in result["dependencies"]))
            self.assertFalse(any(item["acceptedForLiveApply"] for item in result["dependencies"]))
            self.assertFalse(result["approvalGranted"])
            self.assertFalse(result["liveAcceptance"])
        for flag in ("approvalGranted", "liveAcceptance"):
            data = copy.deepcopy(self.inputs)
            data[flag] = True
            self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"])

    def test_unknown_target_secret_fields_and_duplicate_json_refuse(self):
        data = copy.deepcopy(self.inputs)
        data["target"]["token"] = "synthetic-token-must-not-be-emitted"
        result = candidate.assess(data, now=self.now)
        self.assertFalse(result["offlineComplete"])
        self.assertIn("target.token", "\n".join(result["problems"]))
        with self.assertRaises(ValueError):
            candidate.decode_json('{"kind": "first", "kind": "second"}')

    def test_provider_application_resource_inventory_refuses_privilege_paths(self):
        for path, entries in (("rbac.providerResources", None), ("rbac.applicationPackageResources", []), ("rbac.providerResources", [{"apiGroup": "rbac.authorization.k8s.io", "resources": ["roles"]}]), ("rbac.providerResources", [{"apiGroup": "synthetic-provider.invalid", "resources": ["syntheticstores/status"]}]), ("rbac.applicationPackageResources", [{"apiGroup": "dapr.io", "resources": ["components"]}]), ("rbac.applicationPackageResources", [{"apiGroup": "", "resources": ["persistentvolumeclaims"]}])):
            with self.subTest(path=path, entries=entries):
                data = copy.deepcopy(self.inputs)
                set_path(data, path, entries)
                self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"])

    def test_real_cli_complete_synthetic_render_and_plan(self):
        with tempfile.TemporaryDirectory() as temp:
            source = Path(temp) / "inputs.json"
            source.write_bytes(candidate.encoded(self.inputs))
            output = Path(temp) / "manifests"
            self.assertEqual(0, candidate.main(["render", "--inputs", str(source), "--output", str(output)]))
            stream = io.StringIO()
            with contextlib.redirect_stdout(stream):
                self.assertEqual(0, candidate.main(["plan", "--inputs", str(source), "--manifest-dir", str(output)]))
            plan = json.loads(stream.getvalue())
            self.assertFalse(plan["liveAcceptance"])
            self.assertEqual(plan["stages"][4]["proposedOperations"][0]["operation"], "server-dry-run")
            with contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(2, candidate.main(["render", "--inputs", str(source), "--output", str(output)]))

    def test_empty_reviewed_provider_crds_and_additional_flows_are_explicit(self):
        data = copy.deepcopy(self.inputs)
        data["rbac"]["providerResources"] = []
        data["network"]["additionalFlows"] = []
        self.assertTrue(candidate.assess(data, now=self.now)["offlineComplete"])
        data["network"]["flowReview"]["backup"] = None
        self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"])

    def test_plan_binds_input_manifest_target_source_and_ordered_identity_bytes(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            files = candidate.render(self.inputs, now=self.now)
            for name, content in files.items():
                (directory / name).write_bytes(content)
            raw = candidate.encoded(self.inputs)
            plan = candidate.build_plan(self.inputs, raw, directory, now=self.now)
            self.assertEqual(plan["inputSha256"], candidate.digest(raw))
            self.assertEqual(plan["targetBinding"], self.inputs["target"])
            self.assertEqual(len(plan["sources"]["foundationPins"]), 6)
            self.assertEqual(plan["orderedIdentities"], [{"stage": item["id"], "identity": item["identity"]} for item in plan["stages"]])
            ids = self.inputs["identities"]
            self.assertEqual([(item["order"], item["id"], item["identity"], item.get("manifest")) for item in plan["stages"]], [
                (1, "access-bootstrap", ids["administrator"], "00-access-cluster.yaml"),
                (2, "admission", ids["sharedInfrastructure"], "10-admission.yaml"),
                (3, "shared-infrastructure", ids["sharedInfrastructure"], "20-shared-infrastructure.yaml"),
                (4, "namespace-access-bootstrap", ids["administrator"], "25-access-namespaced.yaml"),
                (5, "environment-layer", ids["environmentLayer"], "30-environment-layer.yaml"),
                (6, "foundation-verification", ids["observer"], None),
                (7, "bounded-provider-bootstrap", ids["environmentLayer"], None),
                (8, "final-provider-acceptance", ids["observer"], None)])
            for item in plan["manifests"]:
                self.assertEqual(item["sha256"], candidate.digest(files[item["path"]]))
            self.assertTrue(all(item["executable"] is False for item in plan["stages"]))
            self.assertEqual(plan["assessment"]["parentStoryStatus"], "in-progress")
            self.assertFalse(plan["executable"])
            alternate = json.dumps(self.inputs).encode()
            self.assertNotEqual(plan["planSha256"], candidate.build_plan(self.inputs, alternate, directory, now=self.now)["planSha256"])
            with self.assertRaises(candidate.ERROR):
                candidate.build_plan(self.inputs, b"{}", directory, now=self.now)

    def test_modified_missing_extra_or_linked_manifests_refuse_plan(self):
        for mode in ("modified", "missing", "extra", "symlink"):
            with self.subTest(mode=mode), tempfile.TemporaryDirectory() as temp:
                directory = Path(temp)
                for name, content in candidate.render(self.inputs, now=self.now).items():
                    (directory / name).write_bytes(content)
                first = directory / candidate.FILES[0]
                if mode == "modified":
                    first.write_bytes(first.read_bytes() + b"\n")
                elif mode == "missing":
                    first.unlink()
                elif mode == "extra":
                    (directory / "extra.yaml").write_text("{}")
                else:
                    first.unlink()
                    first.symlink_to(HERE / "inputs.candidate.json")
                with self.assertRaises(candidate.ERROR):
                    candidate.build_plan(self.inputs, candidate.encoded(self.inputs), directory, now=self.now)

    def test_historical_pins_and_sprint_status_are_preserved(self):
        pins = candidate.pinned_sources()
        for name, sha in candidate.SOURCE_PINS.items():
            self.assertEqual(sha, candidate.digest((HERE / name).read_bytes()))
        self.assertEqual(6, len(pins["foundationPins"]))
        sprint = (candidate.PROJECT / "_bmad-output/implementation-artifacts/sprint-status.yaml").read_text()
        self.assertIn("4-14-build-staging-namespaces-storage-and-pod-and-network-isolati: in-progress", sprint)

    def test_no_process_or_network_calls_and_no_dispatcher(self):
        with mock.patch.object(subprocess, "Popen", side_effect=AssertionError("process")), mock.patch.object(socket, "socket", side_effect=AssertionError("network")):
            candidate.render(self.inputs, now=self.now)
            candidate.assess(self.inputs, now=self.now)
        tree = ast.parse((HERE / "candidate.py").read_text())
        imports = {node.module.split(".")[0] for node in ast.walk(tree) if isinstance(node, ast.ImportFrom) and node.module}
        imports |= {alias.name.split(".")[0] for node in ast.walk(tree) if isinstance(node, ast.Import) for alias in node.names}
        self.assertFalse(imports & {"subprocess", "socket", "urllib", "requests", "http", "os"})

    def test_privileged_serviceaccounts_cannot_reside_in_either_workload_namespace(self):
        for identity in ("administrator", "sharedInfrastructure", "environmentLayer", "applicationDeploy", "observer"):
            for namespace in (candidate.APP, candidate.DATA):
                data = copy.deepcopy(self.inputs)
                data["identities"][identity] = f"system:serviceaccount:{namespace}:privileged"
                assessed = candidate.assess(data, now=self.now)
                self.assertFalse(assessed["offlineComplete"], (identity, namespace))
                self.assertTrue(any("identities." + identity in problem and "workload namespace" in problem for problem in assessed["problems"]))
        data = copy.deepcopy(self.inputs)
        data["identities"]["observer"] = "system:serviceaccount:synthetic-observation:observer"
        self.assertTrue(candidate.assess(data, now=self.now)["offlineComplete"])

    def test_closed_service_contract_refuses_credentials_before_render(self):
        data = copy.deepcopy(self.inputs)
        data["network"]["dataServices"][0]["password"] = "sentinel-must-never-reach-a-configmap"
        result = candidate.assess(data, now=self.now)
        self.assertFalse(result["offlineComplete"])
        self.assertTrue(any("dataServices[0].password" in problem for problem in result["problems"]))
        with self.assertRaises(candidate.ERROR):
            candidate.render(data, now=self.now)
        # Direct layer construction also projects a fixed public contract only.
        tls = next(doc for doc in candidate.environment_layer(data) if doc["kind"] == "ConfigMap")
        services = json.loads(tls["data"]["services"])
        self.assertTrue(all(set(service) == candidate.SERVICE_FIELDS for service in services))
        self.assertNotIn("sentinel", json.dumps(tls))

    def test_additional_app_to_data_allowance_cannot_bypass_native_tls_inventory(self):
        data = copy.deepcopy(self.inputs)
        flow = data["network"]["additionalFlows"][0]
        flow.update({"sourceNamespace": candidate.APP, "destinationNamespace": candidate.DATA, "port": 12345})
        self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"])
        # Even a duplicate TLS port belongs only to the authoritative service inventory.
        flow["port"] = data["network"]["dataServices"][0]["tlsPort"]
        self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"])

    def test_kubernetes_label_and_namespace_syntax_refuses_invalid_selectors(self):
        for label in ({"bad key": "value"}, {"key": "bad value"}, {"Bad.Domain/key": "value"}, {"a" * 64: "v"}, {"key": "v" * 64}, {"a/b/c": "value"}):
            data = copy.deepcopy(self.inputs)
            data["network"]["dataServices"][0]["clientPodLabels"] = label
            self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"], label)
        for namespace in (123, True, "bad namespace", "with.dot", "a" * 64, "-bad"):
            data = copy.deepcopy(self.inputs)
            data["network"]["dns"]["peers"][1]["namespaceSelector"]["matchLabels"]["kubernetes.io/metadata.name"] = namespace
            self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"], namespace)
        data = copy.deepcopy(self.inputs)
        data["network"]["dataServices"][0]["podLabels"] = {"example.invalid/App_Name": "Value.1", "empty": ""}
        self.assertTrue(candidate.assess(data, now=self.now)["offlineComplete"])

    def test_literal_dns_peers_cover_declared_resolvers_exactly(self):
        data = copy.deepcopy(self.inputs)
        data["network"]["dns"]["peers"] = [{"ipBlock": {"cidr": "169.254.20.10/32"}}]
        self.assertTrue(candidate.assess(data, now=self.now)["offlineComplete"])
        for addresses, peers in ((["169.254.20.11"], [{"ipBlock": {"cidr": "169.254.20.10/32"}}]), (["169.254.20.10", "169.254.20.11"], data["network"]["dns"]["peers"]), (["169.254.20.10"], data["network"]["dns"]["peers"] * 2), ([123], data["network"]["dns"]["peers"])):
            failed = copy.deepcopy(data)
            failed["network"]["dns"].update({"resolverAddresses": addresses, "peers": peers})
            self.assertFalse(candidate.assess(failed, now=self.now)["offlineComplete"])

    def test_application_dapr_inventory_accepts_only_architecture_owned_kinds(self):
        data = copy.deepcopy(self.inputs)
        data["rbac"]["applicationPackageResources"].append({"apiGroup": "dapr.io", "resources": sorted(candidate.DAPR_APPLICATION)})
        self.assertTrue(candidate.assess(data, now=self.now)["offlineComplete"])
        roles = [doc for doc in candidate.access_layer(data) if doc["kind"] == "Role"]
        app = next(doc for doc in roles if doc["metadata"]["name"] == "staging-application-deploy")
        self.assertEqual(next(rule["resources"] for rule in app["rules"] if rule["apiGroups"] == ["dapr.io"]), sorted(candidate.DAPR_APPLICATION))
        for env in (doc for doc in roles if doc["metadata"]["name"] == "staging-environment-deploy"):
            self.assertEqual(next(rule["resources"] for rule in env["rules"] if rule["apiGroups"] == ["dapr.io"]), sorted(candidate.DAPR_ENVIRONMENT))
        for forbidden in candidate.DAPR_ENVIRONMENT:
            failed = copy.deepcopy(data)
            failed["rbac"]["applicationPackageResources"][-1]["resources"].append(forbidden)
            self.assertFalse(candidate.assess(failed, now=self.now)["offlineComplete"])

    def test_anonymous_principals_refuse_all_existing_authority_fields(self):
        for path in ["identities." + identity for identity in ("administrator", "sharedInfrastructure", "environmentLayer", "applicationDeploy", "observer")] + ["storage." + identity for identity in ("administrator", "deputy", "provisionerPrincipal", "binderPrincipal")]:
            data = copy.deepcopy(self.inputs)
            set_path(data, path, "system:anonymous")
            self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"], path)

    def test_host_subdomain_resolver_and_cidr_types_are_explicit(self):
        for host in ("https://a..b", "https://-bad.invalid", "https://bad_.invalid", "https://999.1.2.3", "https://[no-ipv6]", "https://[1234]", "https://[fe80::1%eth0]", "https://cluster.invalid:0", "https://cluster.invalid:65536"):
            data = copy.deepcopy(self.inputs)
            data["target"]["server"] = host
            self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"], host)
        for host in ("https://cluster.invalid:6443", "https://192.0.2.1", "https://[2001:db8::1]:6443"):
            data = copy.deepcopy(self.inputs)
            data["target"]["server"] = host
            self.assertTrue(candidate.assess(data, now=self.now)["offlineComplete"], host)
        for path in ("storage.productionStorageClassName", "priority.productionClassName"):
            data = copy.deepcopy(self.inputs)
            set_path(data, path, "a..b")
            self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"], path)
        for cidr in (123, "169.254.20.10", "::1", "fe80::1%eth0/128", None):
            data = copy.deepcopy(self.inputs)
            data["network"]["dns"]["peers"] = [{"ipBlock": {"cidr": cidr}}]
            self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"], cidr)
        for address in ("0.0.0.0", "::", "224.0.0.1", "ff02::1", "fe80::1%eth0", 123):
            data = copy.deepcopy(self.inputs)
            data["network"]["dns"]["resolverAddresses"] = [address]
            self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"], address)

    def test_documented_practical_pinned_image_reference_subset(self):
        for reference in ("busybox", "busybox:1.36", "ghcr.io/hexalith/probe", "localhost:5000/test/probe:v1", "registry.invalid:443/group/probe_name:Tag-1"):
            data = copy.deepcopy(self.inputs)
            data["probe"]["image"] = reference + "@sha256:" + "a" * 64
            self.assertTrue(candidate.assess(data, now=self.now)["offlineComplete"], reference)
        for reference in ("!", "/probe", "a//b", "registry.invalid/UPPER", "a..b/probe", "registry.invalid:99999/probe", "probe:", "a/../probe", "probe@@sha256:"):
            data = copy.deepcopy(self.inputs)
            data["probe"]["image"] = reference + "@sha256:" + "a" * 64
            self.assertFalse(candidate.assess(data, now=self.now)["offlineComplete"], reference)


class DefinitionTests(unittest.TestCase):
    def setUp(self):
        self.inputs = complete_inputs()
        self.layers = documents(candidate.render(self.inputs))
        self.all = [doc for layer in self.layers.values() for doc in layer]
        self.policies = {doc["metadata"]["name"]: doc for doc in self.all if doc["kind"] == "ValidatingAdmissionPolicy"}

    def expressions(self, name):
        return "\n".join(item["expression"] for item in self.policies[name]["spec"]["validations"])

    def test_openebs_config_is_annotation_based_with_separate_qualified_mount(self):
        sc = next(doc for doc in self.all if doc["kind"] == "StorageClass")
        self.assertEqual(sc["parameters"], {})
        config = json.loads(sc["metadata"]["annotations"]["cas.openebs.io/config"])
        self.assertEqual(config[1], {"name": "BasePath", "value": self.inputs["storage"]["basePath"]})
        self.assertEqual(config[2]["name"], "XFSQuota")
        self.assertEqual(sc["reclaimPolicy"], "Retain")
        self.assertEqual(sc["volumeBindingMode"], "WaitForFirstConsumer")
        self.assertFalse(sc["allowVolumeExpansion"])

    def test_static_pvc_other_default_omitted_selector_clone_and_user_prebinding_paths(self):
        expression = self.expressions("staging-pvc")
        for clause in ("has(object.spec.storageClassName)", 'object.spec.storageClassName == "hexalith-staging"', "!has(object.spec.selector)", "!has(object.spec.dataSource)", "!has(object.spec.dataSourceRef)", "request.operation == 'CREATE'", "oldObject.spec.volumeName", self.inputs["storage"]["binderPrincipal"]):
            self.assertIn(clause, expression)
        self.assertIn(candidate.DATA, expression)
        # The qualified controller may set an initially empty volumeName; all
        # later bound updates retain it. This is static coverage, not CEL proof.
        self.assertIn("oldObject.spec.volumeName != ''", expression)

    def test_static_pv_class_claim_uid_mount_and_controller_binding_boundaries(self):
        expression = self.expressions("staging-pv")
        scope = self.policies["staging-pv"]["spec"]["matchConditions"][0]["expression"]
        for clause in ("has(object.spec.claimRef.uid)", "object.spec.claimRef.namespace", "object.spec.claimRef.name", "oldObject.spec.claimRef.uid", "object.spec.local.path.startsWith", "persistentVolumeReclaimPolicy == 'Retain'", self.inputs["storage"]["binderPrincipal"], self.inputs["storage"]["provisionerPrincipal"]):
            self.assertIn(clause, expression)
        self.assertIn("oldObject.spec.storageClassName", scope)
        self.assertIn("has(object.spec.storageClassName)", scope)
        self.assertNotIn("object.spec.claimRef == oldObject.spec.claimRef", expression)

    def test_static_priority_and_temporary_volume_bypass_paths_are_explicit(self):
        expression = self.expressions("staging-pod")
        for clause in ("has(object.spec.priorityClassName)", 'object.spec.priorityClassName == "hexalith-staging"', "object.spec.priority == -1000", "object.spec.preemptionPolicy == 'Never'", "has(v.emptyDir.sizeLimit)", "quantity(v.emptyDir.sizeLimit).compareTo(quantity('0')) > 0", "quantity(v.emptyDir.sizeLimit).compareTo(quantity('256Mi')) <= 0", "v.emptyDir.medium == ''", "has(v.persistentVolumeClaim)"):
            self.assertIn(clause, expression)
        self.assertNotIn("has(v.hostPath)", expression)  # Whitelist, not an incomplete blacklist.
        priority = next(doc for doc in self.all if doc["kind"] == "PriorityClass")
        self.assertEqual(priority["value"], -1000)
        self.assertEqual(priority["preemptionPolicy"], "Never")

    def test_static_protection_uses_resource_identities_and_fail_closed_bindings(self):
        protected = self.policies["staging-owned-objects"]["spec"]
        scope = protected["matchConditions"][0]["expression"]
        self.assertIn("request.name", scope)  # Exact identities survive removed labels.
        for res in candidate.PROTECTED | {"namespaces", "priorityclasses", "storageclasses"}:
            self.assertIn(res, scope)
        self.assertIn(self.inputs["identities"]["sharedInfrastructure"], scope)
        for doc in self.policies.values():
            self.assertEqual(doc["spec"]["failurePolicy"], "Fail")
            for rule in doc["spec"]["matchConstraints"]["resourceRules"]:
                self.assertFalse(set(rule["resources"]) & {"validatingadmissionpolicies", "validatingadmissionpolicybindings"})
        for doc in self.all:
            if doc["kind"] == "ValidatingAdmissionPolicyBinding":
                self.assertEqual(doc["spec"]["validationActions"], ["Deny"])

    def test_access_roles_separate_shared_environment_application_and_observer(self):
        access = [doc for doc in self.all if doc["kind"] in ("Role", "ClusterRole", "RoleBinding", "ClusterRoleBinding")]
        bindings = [doc for doc in access if doc["kind"].endswith("Binding")]
        subjects = [subject["name"] for doc in bindings for subject in doc["subjects"]]
        self.assertNotIn(self.inputs["identities"]["applicationDeploy"], subjects)
        shared = next(doc for doc in access if doc["metadata"]["name"] == "staging-shared-deploy" and doc["kind"] == "ClusterRole")
        self.assertFalse(any("secrets" in rule["resources"] or "pods" in rule["resources"] for rule in shared["rules"]))
        env = [doc for doc in access if doc["kind"] == "Role" and doc["metadata"]["name"] == "staging-environment-deploy"]
        app_env = next(doc for doc in env if doc["metadata"]["namespace"] == candidate.APP)
        self.assertFalse(any(set(rule["resources"]) & {"secrets", "pods", "persistentvolumeclaims"} for rule in app_env["rules"]))
        for doc in access:
            for rule in doc.get("rules", []):
                self.assertNotIn("*", rule["verbs"])
                self.assertNotIn("bind", rule["verbs"])
                self.assertNotIn("escalate", rule["verbs"])
                self.assertNotIn("delete", rule["verbs"])
                self.assertNotIn("impersonate", rule["verbs"])
            if doc["kind"] in ("Role", "ClusterRole") and "observer" in doc["metadata"]["name"]:
                self.assertFalse(any("secrets" in rule["resources"] for rule in doc["rules"]))
                for rule in doc["rules"]:
                    if "create" in rule["verbs"]:
                        self.assertEqual(rule, {"apiGroups": ["authorization.k8s.io"], "resources": ["subjectaccessreviews"], "verbs": ["create"]})

    def test_exact_binding_role_and_principal_mappings(self):
        actual = {(doc["kind"], doc["metadata"].get("namespace"), doc["metadata"]["name"]): (doc["roleRef"], doc["subjects"]) for doc in self.all if doc["kind"] in ("RoleBinding", "ClusterRoleBinding")}
        expected = {}
        for name, identity in (("staging-shared-deploy", "sharedInfrastructure"), ("staging-verification-observer", "observer")):
            expected[("ClusterRoleBinding", None, name)] = ({"apiGroup": "rbac.authorization.k8s.io", "kind": "ClusterRole", "name": name}, [{"apiGroup": "rbac.authorization.k8s.io", "kind": "User", "name": self.inputs["identities"][identity]}])
        for namespace in (candidate.APP, candidate.DATA):
            for name, identity in (("staging-environment-deploy", "environmentLayer"), ("staging-observer", "observer")):
                expected[("RoleBinding", namespace, name)] = ({"apiGroup": "rbac.authorization.k8s.io", "kind": "Role", "name": name}, [{"apiGroup": "rbac.authorization.k8s.io", "kind": "User", "name": self.inputs["identities"][identity]}])
        self.assertEqual(actual, expected)

    def test_layer_files_are_owned_and_namespaces_precede_namespaced_grants(self):
        self.assertTrue(all("namespace" not in doc["metadata"] for doc in self.layers[candidate.FILES[0]]))
        self.assertTrue(all("namespace" in doc["metadata"] for doc in self.layers[candidate.FILES[3]]))
        self.assertTrue(all(doc["kind"] in ("Namespace", "StorageClass", "PriorityClass") for doc in self.layers[candidate.FILES[2]]))
        for ns in (doc for doc in self.all if doc["kind"] == "Namespace"):
            for key in ("enforce", "audit", "warn"):
                self.assertEqual(ns["metadata"]["labels"]["pod-security.kubernetes.io/" + key], "restricted")

    def test_service_networks_never_aggregate_unrelated_ports_or_whole_namespaces(self):
        policies = [doc for doc in self.all if doc["kind"] == "NetworkPolicy"]
        for service in self.inputs["network"]["dataServices"]:
            egress = next(doc for doc in policies if doc["metadata"]["name"] == "allow-" + service["name"] + "-egress")
            self.assertEqual(egress["spec"]["podSelector"]["matchLabels"], service["clientPodLabels"])
            rule = egress["spec"]["egress"][0]
            self.assertEqual(rule["ports"], [{"protocol": "TCP", "port": service["tlsPort"]}])
            self.assertEqual(rule["to"], [candidate.ns_peer(candidate.DATA, service["podLabels"])])
            self.assertEqual(egress["metadata"]["namespace"], candidate.APP)
            ingress = next(doc for doc in policies if doc["metadata"]["name"] == "allow-" + service["name"] + "-ingress")
            self.assertEqual(ingress["metadata"]["namespace"], candidate.DATA)
            self.assertEqual(ingress["spec"], {"podSelector": {"matchLabels": service["podLabels"]}, "policyTypes": ["Ingress"], "ingress": [{"from": [candidate.ns_peer(candidate.APP, service["clientPodLabels"])], "ports": [{"protocol": "TCP", "port": service["tlsPort"]}]}]})
        for ns in (candidate.APP, candidate.DATA):
            deny = next(doc for doc in policies if doc["metadata"]["namespace"] == ns and doc["metadata"]["name"] == "default-deny-all")
            self.assertEqual(deny["spec"], {"podSelector": {}, "policyTypes": ["Ingress", "Egress"]})
            dns = next(doc for doc in policies if doc["metadata"]["namespace"] == ns and doc["metadata"]["name"] == "allow-qualified-dns")
            self.assertEqual(dns["spec"]["egress"][0]["to"], self.inputs["network"]["dns"]["peers"])

    def test_every_additional_flow_has_exact_source_and_destination_policies(self):
        for flow in self.inputs["network"]["additionalFlows"]:
            for direction, namespace, selected, peer, peer_labels in (("egress", flow["sourceNamespace"], flow["sourcePodLabels"], flow["destinationNamespace"], flow["destinationPodLabels"]), ("ingress", flow["destinationNamespace"], flow["destinationPodLabels"], flow["sourceNamespace"], flow["sourcePodLabels"])):
                policy = next(doc for doc in self.all if doc["kind"] == "NetworkPolicy" and doc["metadata"]["name"] == "allow-" + flow["name"] + "-" + direction)
                self.assertEqual(policy["metadata"]["namespace"], namespace)
                self.assertEqual(policy["spec"], {"podSelector": {"matchLabels": selected}, "policyTypes": [direction.title()], direction: [{"to" if direction == "egress" else "from": [candidate.ns_peer(peer, peer_labels)], "ports": [{"protocol": flow["protocol"], "port": flow["port"]}]}]})

    def test_approved_quotas_include_temporary_storage_and_application_storage_zero(self):
        for scope, namespace, pod_count, claims, storage in (("application", candidate.APP, "30", "0", "0"), ("data", candidate.DATA, "20", "12", "100Gi")):
            quota = next(doc for doc in self.all if doc["kind"] == "ResourceQuota" and doc["metadata"]["namespace"] == namespace)["spec"]["hard"]
            self.assertEqual((quota["pods"], quota["persistentvolumeclaims"], quota["requests.storage"]), (pod_count, claims, storage))
            self.assertEqual(quota["requests.ephemeral-storage"], "2Gi")
            self.assertEqual(quota["limits.ephemeral-storage"], "8Gi")
            limit = next(doc for doc in self.all if doc["kind"] == "LimitRange" and doc["metadata"]["namespace"] == namespace)["spec"]["limits"][0]
            self.assertEqual(limit["defaultRequest"]["ephemeral-storage"], "64Mi")
            self.assertEqual(limit["default"]["ephemeral-storage"], "256Mi")


if __name__ == "__main__":
    unittest.main()
