"""Probe consistency/refusal tests. No test authenticates runtime evidence."""

import ast
import contextlib
import copy
from datetime import datetime, timedelta, timezone
import json
import io
from pathlib import Path
import socket
import subprocess
import tempfile
import unittest
from unittest import mock

import candidate_probe as probe
import cni_probe
from test_candidate import complete_inputs

HERE = Path(__file__).resolve().parent
ATTEMPT = "synthetic-cni-attempt"
IMAGE = "registry.invalid/probe@sha256:" + "a" * 64
NS_UID = "00000000-0000-4000-8000-000000000001"
POD_UIDS = {name: "00000000-0000-4000-8000-00000000000" + str(index) for index, name in enumerate(("client", *probe.PEERS), 2)}
DNS = [{"ipBlock": {"cidr": "169.254.20.10/32"}}]


class ProbeTests(unittest.TestCase):
    def setUp(self):
        self.now = datetime.now(timezone.utc)
        self.created = (self.now - timedelta(minutes=1)).isoformat()
        self.deadline = (self.now + timedelta(minutes=20)).isoformat()
        self.target = complete_inputs(self.now)["target"]
        self.target["attemptId"] = ATTEMPT
        self.observer = "synthetic-observer"
        self.namespace = cni_probe.namespace_for(ATTEMPT)
        self.observed = {"metadata": {"name": self.namespace, "uid": NS_UID, "labels": cni_probe._labels(ATTEMPT), "annotations": {"hexalith.com/cleanup-deadline": self.deadline, "hexalith.com/probe-created-at": self.created}}}
        self.observed["metadata"]["annotations"].update(self.proposal()["initialDocuments"][0]["metadata"]["annotations"])
        self.records = {"attemptId": ATTEMPT, "namespace": self.namespace, "namespaceUid": NS_UID, "podUids": POD_UIDS.copy(), "targetBinding": copy.deepcopy(self.target), "observer": self.observer, "phases": []}
        for ordinal, phase in enumerate(probe.PHASES):
            observed_at = (self.now - timedelta(seconds=50 - ordinal * 5)).isoformat()
            results = {"allowed-server": "connected", "denied-server": "blocked" if phase in ("ingress-deny", "egress-deny") else "connected"}
            policies = [{**item, "uid": "00000000-0000-4000-8001-" + str(index).zfill(12)} for index, item in enumerate(probe.policy_fingerprints(self.namespace, cni_probe._labels(ATTEMPT), phase))]
            self.records["phases"].append({"phase": phase, "ordinal": ordinal, "attemptId": ATTEMPT, "namespaceUid": NS_UID, "podUids": POD_UIDS.copy(), "observedAt": observed_at, "policies": policies,
                "peerHealth": {peer: {"podUid": POD_UIDS[peer], "ready": True, "localListenerHealthy": True, "endpointPodUid": POD_UIDS[peer], "observedAt": observed_at} for peer in probe.PEERS},
                "results": results, "clientLog": "\n".join(["PROBE_ATTEMPT=" + ATTEMPT, "PROBE_PHASE=" + phase] + ["PROBE_" + peer + "=" + results[peer] for peer in probe.PEERS]) + "\n"})

    def evaluate(self, records=None, **overrides):
        args = {"records": self.records if records is None else records, "attempt_id": ATTEMPT, "created_at": self.created, "cleanup_deadline": self.deadline, "namespace_uid": NS_UID, "pod_uids": POD_UIDS.copy(), "observed_namespace": self.observed, "target": self.target, "observer": self.observer, "now": self.now}
        args.update(overrides)
        return probe.evaluate(**args)

    def proposal(self, **overrides):
        args = {"image": IMAGE, "dns_peers": DNS, "attempt_id": ATTEMPT, "created_at": self.created, "cleanup_deadline": self.deadline, "collision_observation": {"checkedNamespace": self.namespace, "exists": False}, "target": self.target, "observer": self.observer, "now": self.now}
        args.update(overrides)
        return probe.proposal(**args)

    def test_complete_synthetic_results_are_only_offline_consistent(self):
        result = self.evaluate()
        self.assertTrue(result["offlineConsistent"], result)
        for flag in ("passed", "evidenceAuthenticated", "approvalGranted", "liveAcceptance", "creationActionQualified", "cleanupQualified"):
            self.assertIs(result[flag], False)

    def test_both_peers_positive_before_between_and_after_independent_denials(self):
        proposed = self.proposal()
        self.assertEqual([phase["phase"] for phase in proposed["phases"]], list(probe.PHASES))
        for phase in proposed["phases"]:
            policies = {doc["metadata"]["name"]: doc["spec"] for doc in phase["policyDocuments"]}
            destinations = {peer["podSelector"]["matchLabels"]["role"] for peer in policies["client-egress"]["egress"][0]["to"]}
            if phase["phase"] == "ingress-deny":
                self.assertEqual(destinations, set(probe.PEERS))
                self.assertEqual(policies["denied-server-ingress"]["ingress"], [])
            elif phase["phase"] == "egress-deny":
                self.assertEqual(destinations, {"allowed-server"})
                self.assertTrue(policies["denied-server-ingress"]["ingress"])
            else:
                self.assertEqual(destinations, set(probe.PEERS))
                self.assertTrue(policies["denied-server-ingress"]["ingress"])
                self.assertEqual(phase["expectedResults"], {peer: "connected" for peer in probe.PEERS})
        self.assertTrue(all(phase["executable"] is False for phase in proposed["phases"]))

    def test_wrong_attempt_namespace_or_any_uid_refuses(self):
        for key, value in (("attemptId", "other-attempt"), ("namespace", "other-namespace"), ("namespaceUid", POD_UIDS["client"]), ("podUids", {**POD_UIDS, "client": NS_UID})):
            data = copy.deepcopy(self.records)
            data[key] = value
            self.assertFalse(self.evaluate(data)["offlineConsistent"], key)
        for ordinal in range(5):
            data = copy.deepcopy(self.records)
            data["phases"][ordinal]["podUids"]["denied-server"] = NS_UID
            self.assertFalse(self.evaluate(data)["offlineConsistent"])
        for args in ({"namespace_uid": "no-uid"}, {"pod_uids": {}}, {"observed_namespace": {}}):
            self.assertFalse(self.evaluate(**args)["offlineConsistent"])

    def test_missing_replayed_duplicate_or_out_of_order_phase_refuses(self):
        for mode in ("missing", "duplicate", "reordered", "ordinal", "replay"):
            data = copy.deepcopy(self.records)
            if mode == "missing":
                data["phases"].pop()
            elif mode == "duplicate":
                data["phases"][2] = copy.deepcopy(data["phases"][1])
            elif mode == "reordered":
                data["phases"][1], data["phases"][3] = data["phases"][3], data["phases"][1]
            elif mode == "ordinal":
                data["phases"][1]["ordinal"] = 0
            else:
                data["phases"][2]["observedAt"] = data["phases"][1]["observedAt"]
            self.assertFalse(self.evaluate(data)["offlineConsistent"], mode)

    def test_missing_control_unhealthy_peer_or_missing_restoration_refuses(self):
        for ordinal in range(5):
            for peer in probe.PEERS:
                for failure in ("missing-health", "unready", "listener-dead", "wrong-endpoint", "wrong-health-phase", "wrong-result"):
                    data = copy.deepcopy(self.records)
                    phase = data["phases"][ordinal]
                    if failure == "missing-health":
                        phase["peerHealth"].pop(peer)
                    elif failure == "unready":
                        phase["peerHealth"][peer]["ready"] = False
                    elif failure == "listener-dead":
                        phase["peerHealth"][peer]["localListenerHealthy"] = False
                    elif failure == "wrong-endpoint":
                        phase["peerHealth"][peer]["endpointPodUid"] = NS_UID
                    elif failure == "wrong-health-phase":
                        phase["peerHealth"][peer]["observedAt"] = self.created
                    else:
                        phase["results"][peer] = "connected" if phase["results"][peer] == "blocked" else "blocked"
                    self.assertFalse(self.evaluate(data)["offlineConsistent"], (ordinal, peer, failure))

    def test_expired_historical_future_or_overlong_attempt_refuses(self):
        for args in ({"cleanup_deadline": (self.now - timedelta(seconds=1)).isoformat()}, {"created_at": (self.now - timedelta(days=1)).isoformat()}, {"created_at": (self.now + timedelta(seconds=1)).isoformat()}, {"cleanup_deadline": (self.now + timedelta(hours=2)).isoformat()}, {"cleanup_deadline": "2026-11-01T23:00:00Z"}, {"created_at": self.now.replace(tzinfo=None).isoformat()}):
            self.assertFalse(self.evaluate(**args)["offlineConsistent"], args)

    def test_probe_proposal_refuses_collision_unknown_dns_unpinned_image_or_stale_attempt(self):
        for args in ({"collision_observation": {"checkedNamespace": self.namespace, "exists": True}}, {"collision_observation": None}, {"dns_peers": [{"ipBlock": {"cidr": "0.0.0.0/0"}}]}, {"image": "registry.invalid/probe:latest"}, {"attempt_id": "not safe"}, {"created_at": (self.now - timedelta(hours=1)).isoformat()}):
            with self.assertRaises(cni_probe.ProbeInputError, msg=str(args)):
                self.proposal(**args)

    def test_pods_are_restricted_owned_deadlined_and_have_finite_scratch(self):
        proposed = self.proposal()
        pods = [doc for doc in proposed["initialDocuments"] if doc["kind"] == "Pod"]
        self.assertEqual({doc["metadata"]["name"] for doc in pods}, {"client", *probe.PEERS})
        for pod in pods:
            self.assertEqual(pod["metadata"]["labels"]["hexalith.com/probe-attempt"], ATTEMPT)
            self.assertFalse(pod["spec"]["automountServiceAccountToken"])
            self.assertEqual(pod["spec"]["priorityClassName"], probe.candidate.CLASS)
            self.assertEqual(pod["spec"]["preemptionPolicy"], "Never")
            self.assertLessEqual(pod["spec"]["activeDeadlineSeconds"], 1200)
            self.assertEqual(pod["spec"]["volumes"][0]["emptyDir"]["sizeLimit"], "8Mi")
            self.assertEqual(pod["spec"]["securityContext"], {"fsGroup": 65532, "fsGroupChangePolicy": "OnRootMismatch"})
            command = pod["spec"]["containers"][0]["command"]
            self.assertEqual(command[:2], ["sh", "-c"])
            self.assertIn(str(int(probe.candidate.timestamp(self.deadline).timestamp())), command[2])
            self.assertIn('$(date -u +%s)', command[2])
            self.assertIn('[ "$remaining" -gt 0 ] || exit 124', command[2])
            self.assertIn('timeout -s KILL "$remaining"', command[2])
            security = pod["spec"]["containers"][0]["securityContext"]
            self.assertTrue(security["runAsNonRoot"])
            self.assertFalse(security["allowPrivilegeEscalation"])
            self.assertEqual(security["capabilities"]["drop"], ["ALL"])

    def test_phase_payload_is_bounded_marked_and_uses_separate_active_authority(self):
        proposed = self.proposal()
        for phase in proposed["phases"]:
            measurement = phase["clientMeasurement"]
            self.assertFalse(measurement["executable"])
            self.assertEqual(measurement["maximumSeconds"], 15)
            self.assertIn("never the read-only observer", measurement["requiredAuthority"])
            command = measurement["command"]
            self.assertEqual(command[4:10], ["timeout", "-s", "KILL", "15", "sh", "-c"])
            self.assertIn("PROBE_ATTEMPT=" + ATTEMPT, command[-1])
            self.assertIn("PROBE_PHASE=" + phase["phase"], command[-1])
            self.assertIn('timeout -s KILL 5 nc -z -w 3 "$peer" 8080', command[-1])
            self.assertEqual(measurement["execArgv"][-len(command):], command)
            self.assertIn("pod/client", measurement["execArgv"])
            for key, argv in phase["observerCollection"].items():
                if key != "executable":
                    self.assertNotIn("exec", argv)
                    self.assertNotIn("--as", argv)
            self.assertEqual(phase["expectedPolicyFingerprints"], [{key: item[key] for key in ("namespace", "name", "specSha256")} for item in self.records["phases"][phase["ordinal"]]["policies"]])

    def test_policy_observations_bind_exact_directions_identity_and_stable_uids(self):
        for mode in ("missing", "wrong-direction", "duplicate", "replacement", "duplicate-uid", "namespace", "bad-hash"):
            records = copy.deepcopy(self.records)
            policies = records["phases"][1]["policies"]
            if mode == "missing":
                policies.pop()
            elif mode == "wrong-direction":
                policies[-1]["specSha256"] = records["phases"][0]["policies"][-1]["specSha256"]
            elif mode == "duplicate":
                policies[-1] = copy.deepcopy(policies[0])
            elif mode == "replacement":
                policies[0]["uid"] = "00000000-0000-4000-8001-999999999999"
            elif mode == "duplicate-uid":
                policies[0]["uid"] = policies[1]["uid"]
            elif mode == "namespace":
                policies[0]["namespace"] = "other-namespace"
            else:
                policies[0]["specSha256"] = "bad"
            self.assertFalse(self.evaluate(records)["offlineConsistent"], mode)
        for marker in ("PROBE_ATTEMPT=" + ATTEMPT, "PROBE_PHASE=baseline", "PROBE_allowed-server=connected"):
            records = copy.deepcopy(self.records)
            records["phases"][0]["clientLog"] = records["phases"][0]["clientLog"].replace(marker, "PROBE_WRONG=wrong")
            self.assertFalse(self.evaluate(records)["offlineConsistent"])

    def test_explicit_target_and_exact_observer_bind_all_read_proposals(self):
        result = self.proposal()
        expected = [self.target["kubectl"]["path"], "--context", self.target["context"], "--server", self.target["server"], "get", "namespace", self.namespace, "-o", "json"]
        self.assertEqual(result["targetBinding"], self.target)
        self.assertEqual(result["observer"], self.observer)
        self.assertEqual(result["creation"]["requiresLiveCollisionCheck"], expected)
        self.assertEqual(result["creation"]["requiredObserver"], self.observer)
        cleanup = self.cleanup()
        self.assertEqual(cleanup["guardArgv"], expected)
        self.assertEqual(cleanup["targetBinding"], self.target)
        self.assertEqual(cleanup["requiredObserver"], self.observer)
        self.assertNotIn("jpiquot@local", json.dumps([result, cleanup]))
        self.assertNotIn("--as", expected)
        for field, value in (("server", "https://other.invalid"), ("credentialPublicIdentitySha256", "f" * 64)):
            records = copy.deepcopy(self.records)
            records["targetBinding"][field] = value
            self.assertFalse(self.evaluate(records)["offlineConsistent"])
        records = copy.deepcopy(self.records)
        records["observer"] = "other-observer"
        self.assertFalse(self.evaluate(records)["offlineConsistent"])

    def test_per_attempt_observer_grants_exact_read_resources_and_binding(self):
        documents = self.proposal()["initialDocuments"]
        roles = [doc for doc in documents if doc["kind"] == "Role"]
        bindings = [doc for doc in documents if doc["kind"] == "RoleBinding"]
        self.assertEqual(len(roles), 1)
        self.assertEqual(len(bindings), 1)
        role, grant = roles[0], bindings[0]
        for doc in (role, grant):
            self.assertEqual(doc["metadata"]["namespace"], self.namespace)
            self.assertEqual(doc["metadata"]["name"], probe.OBSERVER_ROLE)
            self.assertEqual(doc["metadata"]["labels"], cni_probe._labels(ATTEMPT))
        self.assertEqual(grant["roleRef"], {"apiGroup": "rbac.authorization.k8s.io", "kind": "Role", "name": probe.OBSERVER_ROLE})
        self.assertEqual(grant["subjects"], [{"apiGroup": "rbac.authorization.k8s.io", "kind": "User", "name": self.observer}])
        self.assertEqual(role["rules"], [
            {"apiGroups": [""], "resources": ["pods", "services", "endpoints"], "verbs": ["get", "list", "watch"]},
            {"apiGroups": [""], "resources": ["pods/log"], "resourceNames": ["client", *probe.PEERS], "verbs": ["get"]},
            {"apiGroups": ["discovery.k8s.io"], "resources": ["endpointslices"], "verbs": ["get", "list", "watch"]},
            {"apiGroups": ["networking.k8s.io"], "resources": ["networkpolicies"], "verbs": ["get", "list", "watch"]}])
        self.assertFalse(any(doc["kind"] in ("ClusterRole", "ClusterRoleBinding") for doc in documents))

    def test_target_and_observer_missing_malformed_secret_or_stale_refuse_every_path(self):
        invalid_targets = [None, [], {**self.target, "token": "never-accepted"}, {**self.target, "kubectl": []}, {**self.target, "attemptId": "other-attempt"}, {**self.target, "expiresAt": (self.now - timedelta(seconds=1)).isoformat()}, {**self.target, "observedAt": (self.now - timedelta(hours=1)).isoformat()}]
        for target in invalid_targets:
            self.assertFalse(self.evaluate(target=target)["offlineConsistent"], target)
            with self.assertRaises(cni_probe.ProbeInputError):
                self.proposal(target=target)
            with self.assertRaises(cni_probe.ProbeInputError):
                self.cleanup(target=target)
        for observer in (None, [], "", "bad observer", "system:anonymous", f"system:serviceaccount:{probe.candidate.DATA}:observer", f"system:serviceaccount:{self.namespace}:observer"):
            self.assertFalse(self.evaluate(observer=observer)["offlineConsistent"], observer)
            with self.assertRaises(cni_probe.ProbeInputError):
                self.proposal(observer=observer)
            with self.assertRaises(cni_probe.ProbeInputError):
                self.cleanup(observer=observer)
        with self.assertRaises(cni_probe.ProbeInputError):
            self.proposal(image="!@sha256:" + "a" * 64)

    def test_existing_attempt_over_five_minutes_remains_consistent_within_its_window(self):
        created = (self.now - timedelta(minutes=6)).isoformat()
        observed = copy.deepcopy(self.observed)
        observed["metadata"]["annotations"]["hexalith.com/probe-created-at"] = created
        self.assertTrue(self.evaluate(created_at=created, observed_namespace=observed)["offlineConsistent"])
        with self.assertRaises(cni_probe.ProbeInputError):
            self.proposal(created_at=created)

    def test_malformed_nested_namespace_or_inventory_metadata_refuses_without_crash(self):
        invalid_observations = [{"metadata": []}, {"metadata": None}, {"metadata": "bad"}]
        for field in ("labels", "annotations"):
            for malformed in ([], None, "bad"):
                observed = copy.deepcopy(self.observed)
                observed["metadata"][field] = malformed
                invalid_observations.append(observed)
        for observed in invalid_observations:
            self.assertFalse(self.evaluate(observed_namespace=observed)["offlineConsistent"], observed)
            with self.assertRaises(cni_probe.ProbeInputError):
                self.cleanup(observed_namespace=observed)
        for malformed in ([], None, "bad"):
            inventory = self.inventory()
            inventory[0]["metadata"] = malformed
            with self.assertRaises(cni_probe.ProbeInputError):
                self.cleanup(inventory)

    def inventory(self):
        proposed = self.proposal()
        docs = proposed["initialDocuments"] + proposed["phases"][0]["policyDocuments"]
        inventory = [copy.deepcopy(doc) for doc in docs if doc["kind"] != "Namespace"]
        for index, obj in enumerate(inventory):
            obj["metadata"]["uid"] = POD_UIDS[obj["metadata"]["name"]] if obj["kind"] == "Pod" else "00000000-0000-4000-9000-" + str(index).zfill(12)
        return inventory

    def controller_inventory(self):
        inventory = self.inventory()
        known = {(obj["kind"], obj["metadata"]["name"]): obj for obj in inventory}
        def metadata(name, serial):
            return {"name": name, "namespace": self.namespace, "uid": "00000000-0000-4000-9001-" + str(serial).zfill(12), "creationTimestamp": (self.now - timedelta(seconds=30)).isoformat()}
        def reference(kind, name):
            obj = known[(kind, name)]
            return {"kind": kind, "name": name, "namespace": self.namespace, "uid": obj["metadata"]["uid"], "apiVersion": obj["apiVersion"]}
        service = reference("Service", "allowed-server")
        service.pop("namespace")
        service["controller"] = True
        # Ordinary controller objects carry no attempt labels.
        inventory += [{"apiVersion": "v1", "kind": "ServiceAccount", "metadata": metadata("default", 1)},
                      {"apiVersion": "v1", "kind": "ConfigMap", "metadata": metadata("kube-root-ca.crt", 2), "data": {"ca.crt": "synthetic-public-CA"}},
                      {"apiVersion": "v1", "kind": "Endpoints", "metadata": metadata("allowed-server", 3), "subsets": [{"addresses": [{"ip": "192.0.2.10", "targetRef": reference("Pod", "allowed-server")}], "ports": [{"port": 8080, "protocol": "TCP"}]}]},
                      {"apiVersion": "discovery.k8s.io/v1", "kind": "EndpointSlice", "metadata": {**metadata("allowed-server-synthetic", 4), "labels": {"kubernetes.io/service-name": "allowed-server"}, "ownerReferences": [service]}, "addressType": "IPv4", "ports": [{"port": 8080, "protocol": "TCP", "name": ""}], "endpoints": [{"addresses": ["192.0.2.10"], "targetRef": reference("Pod", "allowed-server"), "conditions": {"ready": True}}]},
                      {"apiVersion": "v1", "kind": "Event", "metadata": metadata("client.scheduling", 5), "involvedObject": reference("Pod", "client")},
                      {"apiVersion": "events.k8s.io/v1", "kind": "Event", "metadata": metadata("allowed-server.startup", 6), "regarding": reference("Service", "allowed-server")}]
        return inventory

    def cleanup(self, inventory=None, **overrides):
        args = {"attempt_id": ATTEMPT, "created_at": self.created, "cleanup_deadline": self.deadline, "namespace_uid": NS_UID, "pod_uids": POD_UIDS.copy(), "observed_namespace": self.observed, "inventory": self.inventory() if inventory is None else inventory, "observed_at": self.now.isoformat(), "target": self.target, "observer": self.observer, "now": self.now}
        args.update(overrides)
        return probe.cleanup_proposal(**args)

    def test_cleanup_records_uid_guard_but_never_qualifies_or_emits_delete(self):
        result = self.cleanup()
        self.assertEqual(result["requiredAtomicNamespaceUidPrecondition"], NS_UID)
        self.assertFalse(result["cleanupQualified"])
        self.assertFalse(result["liveAcceptance"])
        self.assertFalse(result["inventoryAuthenticated"])
        self.assertNotIn("delete", json.dumps(result))

    def test_normal_controller_inventory_is_attributed_separately_and_never_qualified(self):
        result = self.cleanup(self.controller_inventory())
        self.assertEqual(len(result["controllerObjects"]), 6)
        self.assertTrue(all(obj["attemptOwned"] is False for obj in result["controllerObjects"]))
        self.assertFalse(result["controllerAttributionAuthenticated"])
        self.assertFalse(result["savedProposalDnsFingerprintAuthenticated"])
        self.assertTrue(result["requiresLiveSavedProposalAnnotationVerification"])
        self.assertFalse(result["cleanupQualified"])
        self.assertFalse(result["namespaceContentsQualified"])
        self.assertFalse(result["liveAcceptance"])

    def test_controller_inventory_refuses_foreign_namespace_uid_and_unattributed_content(self):
        for mode in ("wrong-namespace", "bad-uid", "reused-uid", "foreign-default", "foreign-ca", "wrong-service-owner", "wrong-pod-reference", "foreign-event", "self-event", "future-creation", "extra-configmap"):
            inventory = self.controller_inventory()
            controllers = inventory[-6:]
            if mode == "wrong-namespace":
                controllers[0]["metadata"]["namespace"] = "other-namespace"
            elif mode == "bad-uid":
                controllers[0]["metadata"]["uid"] = "not-uid"
            elif mode == "reused-uid":
                controllers[0]["metadata"]["uid"] = NS_UID
            elif mode == "foreign-default":
                controllers[0]["metadata"]["name"] = "foreign"
            elif mode == "foreign-ca":
                controllers[1]["data"]["extra"] = "foreign"
            elif mode == "wrong-service-owner":
                controllers[3]["metadata"]["ownerReferences"][0]["uid"] = NS_UID
            elif mode == "wrong-pod-reference":
                controllers[2]["subsets"][0]["addresses"][0]["targetRef"]["uid"] = NS_UID
            elif mode == "foreign-event":
                controllers[4]["involvedObject"]["uid"] = NS_UID
            elif mode == "self-event":
                controllers[4]["involvedObject"].update({"kind": "Event", "name": controllers[4]["metadata"]["name"], "uid": controllers[4]["metadata"]["uid"]})
            elif mode == "future-creation":
                controllers[0]["metadata"]["creationTimestamp"] = (self.now + timedelta(seconds=1)).isoformat()
            else:
                inventory.append({"apiVersion": "v1", "kind": "ConfigMap", "metadata": {**copy.deepcopy(controllers[1]["metadata"]), "name": "foreign", "uid": "00000000-0000-4000-9001-999999999999"}, "data": {}})
            with self.assertRaises((cni_probe.ProbeInputError, ValueError), msg=mode):
                self.cleanup(inventory)

    def test_cleanup_refuses_grant_service_and_policy_content_drift(self):
        for mode in ("role", "binding-subject", "binding-role", "service-selector", "service-port", "service-type", "dns-policy", "default-deny", "phase-policy", "mixed-phases"):
            inventory = self.inventory()
            known = {(obj["kind"], obj["metadata"]["name"]): obj for obj in inventory}
            if mode == "role":
                known[("Role", probe.OBSERVER_ROLE)]["rules"][0]["verbs"].append("create")
            elif mode == "binding-subject":
                known[("RoleBinding", probe.OBSERVER_ROLE)]["subjects"][0]["name"] = "other-principal"
            elif mode == "binding-role":
                known[("RoleBinding", probe.OBSERVER_ROLE)]["roleRef"]["name"] = "other-role"
            elif mode.startswith("service-"):
                spec = known[("Service", "allowed-server")]["spec"]
                if mode == "service-selector":
                    spec["selector"] = {"foreign": "pod"}
                elif mode == "service-port":
                    spec["ports"][0]["port"] = 9999
                else:
                    spec["type"] = "NodePort"
            elif mode == "dns-policy":
                known[("NetworkPolicy", "client-dns")]["spec"]["egress"][0]["to"] = [{"ipBlock": {"cidr": "0.0.0.0/0"}}]
            elif mode == "default-deny":
                known[("NetworkPolicy", "default-deny-all")]["spec"]["podSelector"] = {"matchLabels": {"foreign": "pod"}}
            elif mode == "phase-policy":
                known[("NetworkPolicy", "client-egress")]["spec"]["egress"][0]["ports"][0]["port"] = 9999
            else:
                for phase in ("ingress-deny", "egress-deny"):
                    docs = probe.phase_policies(self.namespace, cni_probe._labels(ATTEMPT), phase)
                    chosen = next(doc for doc in docs if doc["metadata"]["name"] == ("denied-server-ingress" if phase == "ingress-deny" else "client-egress"))
                    known[("NetworkPolicy", chosen["metadata"]["name"])]["spec"] = chosen["spec"]
            with self.assertRaises(cni_probe.ProbeInputError, msg=mode):
                self.cleanup(inventory)

    def test_cleanup_tolerates_api_metadata_service_defaults_and_empty_rule_omission(self):
        inventory = self.controller_inventory()
        for obj in inventory:
            obj["metadata"].update({"resourceVersion": "123", "managedFields": []})
            if obj["kind"] == "Service":
                obj["spec"].update({"clusterIP": "192.0.2.20", "clusterIPs": ["192.0.2.20"], "type": "ClusterIP", "sessionAffinity": "None", "ipFamilies": ["IPv4"], "ipFamilyPolicy": "SingleStack", "internalTrafficPolicy": "Cluster"})
            if obj["kind"] == "NetworkPolicy" and obj["metadata"]["name"] == "default-deny-all":
                obj["spec"].update({"ingress": [], "egress": []})
        self.assertFalse(self.cleanup(inventory)["cleanupQualified"])
        for phase in probe.PHASES:
            by_name = {doc["metadata"]["name"]: doc for doc in probe.phase_policies(self.namespace, cni_probe._labels(ATTEMPT), phase)}
            phase_inventory = copy.deepcopy(inventory)
            for obj in phase_inventory:
                if obj["kind"] == "NetworkPolicy" and obj["metadata"]["name"] in by_name:
                    obj["spec"] = copy.deepcopy(by_name[obj["metadata"]["name"]]["spec"])
                    if obj["spec"].get("ingress") == []:
                        del obj["spec"]["ingress"]
            self.assertFalse(self.cleanup(phase_inventory)["cleanupQualified"], phase)

    def test_cleanup_refuses_missing_or_changed_saved_dns_fingerprint(self):
        for value in (None, "0" * 64, "invalid"):
            observed = copy.deepcopy(self.observed)
            observed["metadata"]["annotations"]["hexalith.com/probe-dns-spec-sha256"] = value
            with self.assertRaises(cni_probe.ProbeInputError):
                self.cleanup(observed_namespace=observed)

    def test_cleanup_refuses_foreign_missing_duplicate_or_replaced_resources(self):
        for mode in ("foreign", "missing", "duplicate", "replaced", "bad-object"):
            inventory = self.inventory()
            if mode == "foreign":
                inventory[0]["metadata"]["labels"]["hexalith.com/probe-attempt"] = "foreign"
            elif mode == "missing":
                inventory.pop()
            elif mode == "duplicate":
                inventory.append(copy.deepcopy(inventory[0]))
            elif mode == "replaced":
                next(obj for obj in inventory if obj["kind"] == "Pod")["metadata"]["uid"] = NS_UID
            else:
                inventory.append(None)
            with self.assertRaises(cni_probe.ProbeInputError, msg=mode):
                self.cleanup(inventory)
        with self.assertRaises(cni_probe.ProbeInputError):
            self.cleanup(observed_at=(self.now - timedelta(minutes=2)).isoformat())

    def test_cleanup_requires_created_before_inventory_and_allows_expired_attempt(self):
        observed = copy.deepcopy(self.observed)
        future_created = (self.now + timedelta(minutes=5)).isoformat()
        observed["metadata"]["annotations"]["hexalith.com/probe-created-at"] = future_created
        with self.assertRaises(cni_probe.ProbeInputError):
            self.cleanup(created_at=future_created, observed_namespace=observed)
        with self.assertRaises(cni_probe.ProbeInputError):
            self.cleanup(observed_at=(self.now + timedelta(seconds=1)).isoformat())
        created = (self.now - timedelta(minutes=45)).isoformat()
        deadline = (self.now - timedelta(minutes=1)).isoformat()
        observed["metadata"]["annotations"].update({"hexalith.com/probe-created-at": created, "hexalith.com/cleanup-deadline": deadline})
        result = self.cleanup(created_at=created, cleanup_deadline=deadline, observed_namespace=observed)
        self.assertFalse(result["cleanupQualified"])
        self.assertFalse(result["liveAcceptance"])

    def test_direct_cli_evaluation_success_failure_and_malformed_requests(self):
        request = {"records": self.records, "attempt_id": ATTEMPT, "created_at": self.created, "cleanup_deadline": self.deadline,
                   "namespace_uid": NS_UID, "pod_uids": POD_UIDS, "observed_namespace": self.observed, "target": self.target, "observer": self.observer}
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "request.json"
            cases = [(request, 0), ({**request, "records": {}}, 2), ([], 2), ({"unexpected": "argument"}, 2)]
            for supplied, expected in cases:
                source.write_text(json.dumps(supplied))
                stdout, stderr = io.StringIO(), io.StringIO()
                with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                    self.assertEqual(probe.main(["evaluate", "--request", str(source)]), expected)
                result = json.loads(stdout.getvalue())
                self.assertIs(result["offlineConsistent"], expected == 0)
                for flag in ("passed", "evidenceAuthenticated", "creationActionQualified", "cleanupQualified", "approvalGranted", "liveAcceptance", "executable"):
                    self.assertIs(result[flag], False)
                if not isinstance(supplied, dict) or "unexpected" in supplied:
                    self.assertEqual(result["kind"], "staging-cni-offline-refusal-v1")
                    self.assertIn("refused:", stderr.getvalue())
            source.write_text("{bad JSON")
            with contextlib.redirect_stdout(io.StringIO()) as stdout, contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(2, probe.main(["proposal", "--request", str(source)]))
            self.assertFalse(json.loads(stdout.getvalue())["liveAcceptance"])

    def test_direct_cli_proposal_and_cleanup_remain_blocked(self):
        proposal_request = {"image": IMAGE, "dns_peers": DNS, "attempt_id": ATTEMPT, "created_at": self.created, "cleanup_deadline": self.deadline,
                            "collision_observation": {"checkedNamespace": self.namespace, "exists": False}, "target": self.target, "observer": self.observer}
        cleanup_request = {"attempt_id": ATTEMPT, "created_at": self.created, "cleanup_deadline": self.deadline, "namespace_uid": NS_UID, "pod_uids": POD_UIDS,
                           "observed_namespace": self.observed, "inventory": self.controller_inventory(), "observed_at": self.now.isoformat(), "target": self.target, "observer": self.observer}
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "request.json"
            for command, request in (("proposal", proposal_request), ("cleanup-proposal", cleanup_request)):
                source.write_text(json.dumps(request))
                with contextlib.redirect_stdout(io.StringIO()) as stdout:
                    self.assertEqual(0, probe.main([command, "--request", str(source)]))
                result = json.loads(stdout.getvalue())
                self.assertFalse(result["executable"])
                self.assertFalse(result["approvalGranted"])
                self.assertFalse(result["liveAcceptance"])
                if command == "cleanup-proposal":
                    self.assertFalse(result["cleanupQualified"])

    def test_no_process_network_or_executable_dispatcher(self):
        with mock.patch.object(subprocess, "Popen", side_effect=AssertionError("process")), mock.patch.object(socket, "socket", side_effect=AssertionError("network")):
            self.proposal()
            self.evaluate()
            self.cleanup()
        tree = ast.parse((HERE / "candidate_probe.py").read_text())
        imports = {node.module.split(".")[0] for node in ast.walk(tree) if isinstance(node, ast.ImportFrom) and node.module}
        imports |= {alias.name.split(".")[0] for node in ast.walk(tree) if isinstance(node, ast.Import) for alias in node.names}
        self.assertFalse(imports & {"subprocess", "socket", "urllib", "requests", "http", "os"})


if __name__ == "__main__":
    unittest.main()
