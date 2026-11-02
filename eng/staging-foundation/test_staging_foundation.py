"""Offline tests for the staging foundation; no cluster call is possible or made."""

import ast
import contextlib
import copy
from datetime import datetime, timedelta, timezone
import hashlib
import io
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import unittest
from unittest import mock

import cni_probe
import preflight
import render

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parents[1]
DIGEST_IMAGE = "registry.invalid/probe@sha256:" + "a" * 64
ATTEMPT_ID = "attempt-42"


def future_deadline():
    return (datetime.now(timezone.utc) + timedelta(hours=1)).isoformat()


def complete_inputs():
    q = {"requestsCpu": "4", "requestsMemory": "8Gi", "limitsCpu": "8", "limitsMemory": "16Gi",
         "pods": "40", "persistentVolumeClaims": "10", "requestsStorage": "100Gi"}
    return {
        "storage": {"provisioner": "csi.example.invalid", "parameters": {"encrypted": "true"}, "reclaimPolicy": "Retain",
                    "stagingStorageClassName": "staging-encrypted", "productionStorageClassName": "prod-encrypted",
                    "encryptionAttestation": {"evidenceRef": "evidence/enc-1", "attestedBy": "owner"}},
        "custody": {"holders": ["holder-b", "holder-a"]},
        "priority": {"stagingValue": 1000, "productionValue": 100000, "productionClassName": "prod-priority"},
        "deployIdentities": {"sharedInfrastructure": "sa-infra", "environmentLayer": "sa-env", "jobs": ["sa-job-1", "sa-job-2"]},
        "network": {"dns": {"namespace": "kube-system", "podLabels": {"k8s-app": "kube-dns"}},
                    "dataServices": [{"name": "redis", "tlsPort": 6379}, {"name": "pg", "tlsPort": 5432}]},
        "limitRange": {"defaultRequestCpu": "50m", "defaultRequestMemory": "64Mi", "defaultLimitCpu": "500m",
                       "defaultLimitMemory": "512Mi", "pvcMinStorage": "1Gi", "pvcMaxStorage": "50Gi"},
        "quotas": {"application": dict(q), "data": {**q, "requestsCpu": "2", "pods": "20", "requestsStorage": "200Gi"}},
    }


class RenderTests(unittest.TestCase):
    def setUp(self):
        self.inputs = complete_inputs()
        self.files = render.render(self.inputs)
        self.shared = [json.loads(p) for p in self.files["shared-infrastructure.yaml"].split("---\n") if p.strip()]
        self.env = [json.loads(p) for p in self.files["environment-layer.yaml"].split("---\n") if p.strip()]

    def kinds(self, docs, kind):
        return [d for d in docs if d["kind"] == kind]

    def test_example_inputs_are_all_null_and_refused(self):
        example = json.loads((HERE / "inputs.example.json").read_text())
        with self.assertRaises(render.InputError) as ctx:
            render.render(example)
        text = "\n".join(ctx.exception.problems)
        for field in ("storage.provisioner", "storage.encryptionAttestation.evidenceRef", "custody.holders",
                      "storage.productionStorageClassName", "deployIdentities.sharedInfrastructure"):
            self.assertIn(field, text)

    def test_every_missing_field_is_named_and_nothing_written(self):
        for path in render.REQUIRED:
            data = complete_inputs()
            node = data
            parts = path.split(".")
            for part in parts[:-1]:
                node = node[part]
            del node[parts[-1]]
            with self.assertRaises(render.InputError, msg=path) as ctx:
                render.render(data)
            self.assertTrue(any(path in p for p in ctx.exception.problems), path)

    def test_placeholder_values_refused(self):
        for value in ("TODO", "", "<provisioner>", "unknown"):
            data = complete_inputs()
            data["storage"]["provisioner"] = value
            with self.assertRaises(render.InputError):
                render.render(data)

    def test_cli_refusal_writes_nothing(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "out"
            code = render.main(["render", "--inputs", str(HERE / "inputs.example.json"), "--output", str(out)])
            self.assertNotEqual(code, 0)
            self.assertFalse(out.exists())

    def test_cli_renders_complete_inputs_deterministically(self):
        with tempfile.TemporaryDirectory() as tmp:
            src = Path(tmp) / "in.json"
            src.write_text(json.dumps(self.inputs))
            for name in ("a", "b"):
                self.assertEqual(render.main(["render", "--inputs", str(src), "--output", str(Path(tmp) / name)]), 0)
            for f in self.files:
                self.assertEqual((Path(tmp) / "a" / f).read_text(), (Path(tmp) / "b" / f).read_text())
                self.assertEqual((Path(tmp) / "a" / f).read_text(), self.files[f])

    def test_layer_split_by_identity(self):
        self.assertEqual({d["kind"] for d in self.shared}, {"Namespace", "StorageClass", "PriorityClass"})
        self.assertTrue({"ResourceQuota", "LimitRange", "NetworkPolicy", "ConfigMap"} >= {d["kind"] for d in self.env})
        self.assertTrue({"ResourceQuota", "LimitRange", "NetworkPolicy"} <= {d["kind"] for d in self.env})

    def test_namespaces_enforce_restricted(self):
        names = {}
        for ns in self.kinds(self.shared, "Namespace"):
            names[ns["metadata"]["name"]] = ns
            self.assertEqual(ns["metadata"]["labels"]["pod-security.kubernetes.io/enforce"], "restricted")
        self.assertEqual(set(names), {"hexalith-staging", "hexalith-staging-data"})

    def test_default_deny_both_directions_in_both_namespaces(self):
        for ns in ("hexalith-staging", "hexalith-staging-data"):
            deny = [d for d in self.kinds(self.env, "NetworkPolicy")
                    if d["metadata"]["namespace"] == ns and d["metadata"]["name"] == "default-deny-all"]
            self.assertEqual(len(deny), 1)
            self.assertEqual(deny[0]["spec"]["podSelector"], {})
            self.assertEqual(deny[0]["spec"]["policyTypes"], ["Ingress", "Egress"])
            self.assertNotIn("ingress", deny[0]["spec"])
            self.assertNotIn("egress", deny[0]["spec"])

    def test_allow_policies_are_narrow(self):
        for d in self.kinds(self.env, "NetworkPolicy"):
            for rule in d["spec"].get("egress", []) + d["spec"].get("ingress", []):
                self.assertTrue(rule.get("ports"), d["metadata"]["name"])
                self.assertNotIn({}, rule.get("to", []) + rule.get("from", []))
        data_policy = [d for d in self.env if d["metadata"]["name"] == "allow-ingress-from-application-tls"][0]
        self.assertEqual({p["port"] for p in data_policy["spec"]["ingress"][0]["ports"]}, {5432, 6379})

    def test_quota_limits_and_production_storage_denial(self):
        quotas = {q["metadata"]["namespace"]: q for q in self.kinds(self.env, "ResourceQuota") if q["metadata"]["name"] == "staging-quota"}
        prod = "prod-encrypted.storageclass.storage.k8s.io/"
        base = {"limits.cpu": "8", "limits.memory": "16Gi", "persistentvolumeclaims": "10", "requests.memory": "8Gi",
                prod + "persistentvolumeclaims": "0", prod + "requests.storage": "0"}
        self.assertEqual(quotas["hexalith-staging"]["spec"]["hard"],
                         {**base, "requests.cpu": "4", "pods": "40", "requests.storage": "100Gi"})
        self.assertEqual(quotas["hexalith-staging-data"]["spec"]["hard"],
                         {**base, "requests.cpu": "2", "pods": "20", "requests.storage": "200Gi"})
        limit = [{"type": "Container", "defaultRequest": {"cpu": "50m", "memory": "64Mi"},
                  "default": {"cpu": "500m", "memory": "512Mi"}},
                 {"type": "PersistentVolumeClaim", "min": {"storage": "1Gi"}, "max": {"storage": "50Gi"}}]
        ranges = self.kinds(self.env, "LimitRange")
        self.assertEqual(len(ranges), 2)
        for lr in ranges:
            self.assertEqual(lr["spec"]["limits"], limit)

    def test_production_priority_class_denied_by_scoped_quota(self):
        denials = [q for q in self.kinds(self.env, "ResourceQuota") if q["metadata"]["name"] == "deny-production-priority"]
        self.assertEqual({q["metadata"]["namespace"] for q in denials}, {"hexalith-staging", "hexalith-staging-data"})
        for q in denials:
            self.assertEqual(q["spec"]["hard"], {"pods": "0"})
            self.assertEqual(q["spec"]["scopeSelector"]["matchExpressions"],
                             [{"scopeName": "PriorityClass", "operator": "In", "values": ["prod-priority"]}])

    def test_psa_audit_warn_and_tls_contract(self):
        for ns in self.kinds(self.shared, "Namespace"):
            for mode in ("enforce", "audit", "warn"):
                self.assertEqual(ns["metadata"]["labels"][f"pod-security.kubernetes.io/{mode}"], "restricted")
        cm = self.kinds(self.env, "ConfigMap")[0]
        self.assertEqual(cm["data"]["requireTls"], "true")
        self.assertEqual(json.loads(cm["data"]["services"]), [{"name": "pg", "tlsPort": 5432}, {"name": "redis", "tlsPort": 6379}])

    def test_priority_lower_and_storage_class_properties(self):
        self.assertLess(self.kinds(self.shared, "PriorityClass")[0]["value"], self.inputs["priority"]["productionValue"])
        sc = self.kinds(self.shared, "StorageClass")[0]
        self.assertEqual(sc["metadata"]["name"], "staging-encrypted")
        self.assertEqual(sc["provisioner"], "csi.example.invalid")
        self.assertEqual(sc["metadata"]["annotations"]["hexalith.com/key-custody-holders"], "holder-a,holder-b")

    def test_policy_invariants_refused(self):
        cases = []
        d = complete_inputs(); d["priority"]["stagingValue"] = 100000; cases.append(d)
        d = complete_inputs(); d["storage"]["stagingStorageClassName"] = "prod-encrypted"; cases.append(d)
        d = complete_inputs(); d["storage"]["reclaimPolicy"] = "Recycle"; cases.append(d)
        d = complete_inputs(); d["deployIdentities"]["jobs"] = ["sa-infra"]; cases.append(d)
        d = complete_inputs(); d["custody"]["holders"] = []; cases.append(d)
        for data in cases:
            with self.assertRaises(render.InputError):
                render.render(data)


class ValidationTableTests(unittest.TestCase):
    CASES = [
        ("storage.parameters", lambda d: d["storage"].update(parameters={"a": 1})),
        ("storage.parameters", lambda d: d["storage"].update(parameters={"a": None})),
        ("network.dataServices[0].name", lambda d: d["network"].update(dataServices=[{"name": "", "tlsPort": 1}])),
        ("network.dataServices[0].name", lambda d: d["network"].update(dataServices=[{"name": "TODO", "tlsPort": 1}])),
        ("network.dataServices[0].tlsPort", lambda d: d["network"].update(dataServices=[{"name": "a", "tlsPort": 0}])),
        ("network.dataServices[0].tlsPort", lambda d: d["network"].update(dataServices=[{"name": "a", "tlsPort": 70000}])),
        ("network.dataServices[0].tlsPort", lambda d: d["network"].update(dataServices=[{"name": "a", "tlsPort": True}])),
        ("network.dataServices[1].name", lambda d: d["network"].update(dataServices=[{"name": "a", "tlsPort": 1}, {"name": "a", "tlsPort": 2}])),
        ("network.dataServices", lambda d: d["network"].update(dataServices=["redis"])),
        ("deployIdentities", lambda d: d["deployIdentities"].update(environmentLayer="sa-infra")),
        ("deployIdentities.jobs", lambda d: d["deployIdentities"].update(jobs=[{"name": "x"}])),
        ("deployIdentities.jobs", lambda d: d["deployIdentities"].update(jobs=["a", "a"])),
        ("deployIdentities.jobs", lambda d: d["deployIdentities"].update(jobs=["sa-env"])),
        ("custody.holders", lambda d: d["custody"].update(holders=[{"name": "x"}])),
        ("custody.holders", lambda d: d["custody"].update(holders=["TODO"])),
        ("custody.holders", lambda d: d["custody"].update(holders=["a", "a"])),
        ("custody.holders", lambda d: d["custody"].update(holders=["a,b"])),
        ("priority.productionClassName", lambda d: d["priority"].update(productionClassName="hexalith-staging")),
        ("priority.stagingValue", lambda d: d["priority"].update(stagingValue=100000)),
        ("storage.reclaimPolicy", lambda d: d["storage"].update(reclaimPolicy="Recycle")),
    ]

    def test_each_case_refused_naming_the_field(self):
        for field, mutate in self.CASES:
            data = complete_inputs()
            mutate(data)
            with self.assertRaises(render.InputError, msg=field) as ctx:
                render.validate(data)
            self.assertTrue(any(p.startswith(field) for p in ctx.exception.problems), (field, ctx.exception.problems))

    def test_all_problems_collected_in_one_pass(self):
        data = complete_inputs()
        data["storage"]["parameters"] = {"a": 1}
        data["custody"]["holders"] = ["a", "a"]
        data["network"]["dataServices"] = [{"name": "x", "tlsPort": 0}]
        data["storage"]["provisioner"] = None
        with self.assertRaises(render.InputError) as ctx:
            render.validate(data)
        text = "\n".join(ctx.exception.problems)
        for field in ("storage.parameters", "custody.holders", "dataServices[0].tlsPort", "storage.provisioner"):
            self.assertIn(field, text)

    def test_unreadable_or_invalid_inputs_exit_2_without_output(self):
        with tempfile.TemporaryDirectory() as tmp:
            bad = Path(tmp) / "bad.json"
            bad.write_text("{not json")
            out = Path(tmp) / "out"
            for source in (Path(tmp) / "missing.json", bad):
                with contextlib.redirect_stderr(io.StringIO()) as err:
                    code = render.main(["render", "--inputs", str(source), "--output", str(out)])
                self.assertEqual(code, 2)
                self.assertIn("refused", err.getvalue())
                self.assertFalse(out.exists())
            with contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(render.main(["plan", "--inputs", str(bad), "--observed-context", "x", "--observed-cluster-uid", "y"]), 2)


class ApplyPlanTests(unittest.TestCase):
    def test_static_plan_is_blocked(self):
        plan = json.loads((HERE / "apply-plan.json").read_text())
        self.assertIs(plan["approvalGranted"], False)
        self.assertIs(plan["executableDispatcherExists"], False)
        self.assertEqual(plan["status"], "BLOCKED_NOT_FOR_EXECUTION")
        self.assertEqual({d["story"] for d in plan["unmetDependencies"]}, {"4-1", "4-2", "4-3", "4-27", "4-28"})
        self.assertIn("helm rollback", plan["recovery"]["never"])

    def test_historical_pins_and_status_boundary(self):
        assessment = json.loads((HERE / "unblocking.assessed.json").read_text())
        self.assertFalse(assessment["approvalGranted"])
        self.assertFalse(assessment["liveAcceptance"])
        self.assertEqual(assessment["storyStatus"], "in-progress")
        self.assertIn("4-14-build-staging-namespaces-storage-and-pod-and-network-isolati: in-progress",
                      (PROJECT / "_bmad-output/implementation-artifacts/sprint-status.yaml").read_text())
        source_index = json.loads((PROJECT / "eng/staging-secrets/source-evidence.json").read_text())
        pins = [source for source in source_index["sources"] if source.get("id", "").startswith("foundation-")]
        self.assertEqual(len(pins), 6)
        for pin in pins:
            self.assertEqual(hashlib.sha256((PROJECT / pin["path"]).read_bytes()).hexdigest(), pin["sha256"])

    def test_concrete_plan(self):
        plan = render.build_apply_plan(complete_inputs(), render.EXPECTED_CONTEXT, render.EXPECTED_UID)
        self.assertIs(plan["approvalGranted"], False)
        self.assertEqual([s["id"] for s in plan["stages"]], ["shared-infrastructure", "environment-layer"])
        for stage in plan["stages"]:
            self.assertIn("--dry-run=server", stage["dryRunArgv"])
            self.assertNotIn("--dry-run=server", stage["applyArgv"])
            self.assertIn(stage["identity"], stage["applyArgv"])
            self.assertFalse(stage["executable"])
        self.assertEqual(plan["stages"][0]["identity"], "sa-infra")
        self.assertEqual(plan["stages"][1]["identity"], "sa-env")
        denials = plan["jobIdentityDenialChecks"]
        self.assertEqual({d["identity"] for d in denials}, {"sa-job-1", "sa-job-2"})
        for d in denials:
            self.assertEqual(d["expectedAnswer"], "no")
            self.assertEqual(d["argv"][:5], ["kubectl", "--context", "jpiquot@local", "auth", "can-i"])
        self.assertEqual(plan["readOnlyVerification"][0], ["kubectl", "--context", "jpiquot@local", "get", "namespace",
                                                          "kube-system", "-o", "jsonpath={.metadata.uid}"])
        for argv in plan["readOnlyVerification"]:
            self.assertEqual(argv[:3], ["kubectl", "--context", "jpiquot@local"])
            self.assertIn("get", argv)
            for banned in ("apply", "delete", "create", "patch", "replace", "edit"):
                self.assertNotIn(banned, argv)
        self.assertIn("validOnlyAfter", plan["stages"][1])
        self.assertNotIn("validOnlyAfter", plan["stages"][0])

    def test_exact_denial_argv(self):
        plan = render.build_apply_plan(complete_inputs(), render.EXPECTED_CONTEXT, render.EXPECTED_UID)
        base = ["kubectl", "--context", "jpiquot@local", "auth", "can-i"]
        for job in ("sa-job-1", "sa-job-2"):
            argvs = [d["argv"] for d in plan["jobIdentityDenialChecks"] if d["identity"] == job]
            expected = [base + ["create", "namespaces", "--as", job],
                        base + ["create", "storageclasses.storage.k8s.io", "--as", job],
                        base + ["create", "priorityclasses.scheduling.k8s.io", "--as", job],
                        base + ["get", "secrets", "--as", job, "-n", "hexalith-staging-data"]]
            for resource in ("networkpolicies", "resourcequotas", "limitranges"):
                for verb in ("create", "update", "patch", "delete"):
                    for ns in ("hexalith-staging", "hexalith-staging-data"):
                        expected.append(base + [verb, resource, "--as", job, "-n", ns])
            self.assertEqual(argvs, expected)
        cross = {}
        for d in plan["crossIdentityDenialChecks"]:
            cross.setdefault(d["identity"], []).append(d["argv"])
        self.assertIn(base + ["create", "resourcequotas", "--as", "sa-infra", "-n", "hexalith-staging"], cross["sa-infra"])
        self.assertIn(base + ["create", "networkpolicies", "--as", "sa-infra", "-n", "hexalith-staging-data"], cross["sa-infra"])
        self.assertIn(base + ["create", "namespaces", "--as", "sa-env"], cross["sa-env"])
        for d in plan["jobIdentityDenialChecks"] + plan["crossIdentityDenialChecks"]:
            self.assertEqual((d["expectedAnswer"], d["expectedExitCode"]), ("no", 1))

    def test_wrong_target_refused(self):
        for ctx, uid in (("other", render.EXPECTED_UID), (render.EXPECTED_CONTEXT, "0000"), ("", "")):
            with self.assertRaises(render.InputError):
                render.build_apply_plan(complete_inputs(), ctx, uid)
        code = render.main(["plan", "--inputs", str(HERE / "inputs.example.json"),
                            "--observed-context", "x", "--observed-cluster-uid", "y"])
        self.assertNotEqual(code, 0)


class PreflightTests(unittest.TestCase):
    def test_complete_candidate_renders_and_plan_stays_blocked(self):
        with tempfile.TemporaryDirectory() as tmp:
            src, out = Path(tmp) / "candidate.json", Path(tmp) / "out"
            src.write_text(json.dumps(complete_inputs()))
            self.assertEqual(preflight.main(["render", "--inputs", str(src), "--output", str(out)]), 0)
            self.assertEqual((out / "shared-infrastructure.yaml").read_text(), render.render(complete_inputs())["shared-infrastructure.yaml"])
            with contextlib.redirect_stdout(io.StringIO()) as stdout:
                self.assertEqual(preflight.main(["plan", "--inputs", str(src),
                                                  "--observed-context", render.EXPECTED_CONTEXT,
                                                  "--observed-cluster-uid", render.EXPECTED_UID]), 0)
            plan = json.loads(stdout.getvalue())
            self.assertFalse(plan["approvalGranted"])
            self.assertTrue(all(stage["executable"] is False for stage in plan["stages"]))

    def test_missing_inputs_refuse_before_renderer_and_write_nothing(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "out"
            with mock.patch.object(render, "render", side_effect=AssertionError("renderer called")), \
                    mock.patch.object(render, "build_apply_plan", side_effect=AssertionError("renderer called")), \
                    contextlib.redirect_stderr(io.StringIO()) as stderr, contextlib.redirect_stdout(io.StringIO()) as stdout:
                self.assertEqual(preflight.main(["render", "--inputs", str(HERE / "inputs.resolved.json"),
                                                 "--output", str(out)]), 2)
                self.assertEqual(preflight.main(["plan", "--inputs", str(HERE / "inputs.resolved.json"),
                                                 "--observed-context", render.EXPECTED_CONTEXT,
                                                 "--observed-cluster-uid", render.EXPECTED_UID]), 2)
            self.assertFalse(out.exists())
            self.assertEqual(stdout.getvalue(), "")
            self.assertIn("storage.provisioner", stderr.getvalue())
            self.assertIn("quotas.data.requestsStorage", stderr.getvalue())

    def test_invalid_quantities_and_relationships_report_fields(self):
        cases = [
            ("limitRange.defaultRequestCpu", "bogus"),
            ("limitRange.defaultRequestCpu", "0.1m"),
            ("limitRange.defaultRequestCpu", "1.5m"),
            ("limitRange.defaultRequestMemory", "1GiB"),
            ("limitRange.defaultRequestMemory", "1Gi"),
            ("limitRange.defaultLimitCpu", "-1"),
            ("limitRange.pvcMinStorage", "2Gi"),
            ("quotas.application.pods", "1.5"),
            ("quotas.application.requestsCpu", "1e1000000"),
            ("quotas.application.requestsMemory", "9223372036854775808"),
        ]
        for path, value in cases:
            data = complete_inputs()
            node = data
            parts = path.split(".")
            for part in parts[:-1]:
                node = node[part]
            node[parts[-1]] = value
            if path == "limitRange.pvcMinStorage":
                data["limitRange"]["pvcMaxStorage"] = "1Gi"
            with self.assertRaises(render.InputError, msg=path) as error:
                preflight.validate(data)
            self.assertTrue(any(p.startswith(path + ":") for p in error.exception.problems))

    def test_binary_ei_and_decimal_exponent_are_distinct(self):
        self.assertEqual(preflight.quantity("1Ei", "memory"), 2**60)
        self.assertEqual(preflight.quantity("1E", "memory"), 10**18)
        self.assertEqual(preflight.quantity("1e3", "memory"), 1000)

    def test_quota_must_fit_one_default_or_claim_in_each_namespace(self):
        for scope in ("application", "data"):
            for quota, value in (("requestsCpu", "1m"), ("requestsMemory", "1Mi"),
                                 ("limitsCpu", "1m"), ("limitsMemory", "1Mi"),
                                 ("requestsStorage", "1Mi")):
                data = complete_inputs()
                data["quotas"][scope][quota] = value
                path = f"quotas.{scope}.{quota}"
                with self.assertRaises(render.InputError, msg=path) as error:
                    preflight.validate(data)
                self.assertTrue(any(p.startswith(path + ":") for p in error.exception.problems))

    def test_invalid_candidate_emits_no_manifest_or_plan(self):
        with tempfile.TemporaryDirectory() as tmp:
            data = complete_inputs()
            data["limitRange"]["defaultRequestCpu"] = "1"
            data["limitRange"]["defaultLimitCpu"] = "500m"
            data["quotas"]["data"]["requestsStorage"] = "1Mi"
            src, out = Path(tmp) / "candidate.json", Path(tmp) / "out"
            src.write_text(json.dumps(data))
            with contextlib.redirect_stderr(io.StringIO()) as stderr, contextlib.redirect_stdout(io.StringIO()) as stdout:
                self.assertEqual(preflight.main(["render", "--inputs", str(src), "--output", str(out)]), 2)
                self.assertEqual(preflight.main(["plan", "--inputs", str(src),
                                                 "--observed-context", render.EXPECTED_CONTEXT,
                                                 "--observed-cluster-uid", render.EXPECTED_UID]), 2)
            self.assertFalse(out.exists())
            self.assertEqual(stdout.getvalue(), "")
            self.assertIn("limitRange.defaultRequestCpu", stderr.getvalue())
            self.assertIn("quotas.data.requestsStorage", stderr.getvalue())


class CniProbeTests(unittest.TestCase):
    def setUp(self):
        self.deadline = future_deadline()
        self.docs, self.client = cni_probe.manifests(DIGEST_IMAGE, "kube-system", {"k8s-app": "kube-dns"},
                                                     ATTEMPT_ID, self.deadline)

    def evaluate(self, body):
        return cni_probe.evaluate("PROBE_ATTEMPT=" + ATTEMPT_ID + "\n" + body, ATTEMPT_ID)

    def test_requires_pinned_image(self):
        for image in ("probe:latest", "", None):
            with self.assertRaises(cni_probe.ProbeInputError):
                cni_probe.manifests(image, "kube-system", {"a": "b"}, ATTEMPT_ID, self.deadline)

    def test_manifests_deny_by_default_and_allow_one_path(self):
        policies = {d["metadata"]["name"]: d for d in self.docs if d["kind"] == "NetworkPolicy"}
        self.assertEqual(policies["default-deny-all"]["spec"]["policyTypes"], ["Ingress", "Egress"])
        ingress = policies["allow-allowed-server-ingress"]["spec"]["podSelector"]["matchLabels"]
        self.assertEqual(ingress, {"role": "allowed-server"})
        self.assertFalse([d for d in policies.values() if "denied-server" in json.dumps(d["spec"])])
        self.assertEqual(self.docs[0]["metadata"]["labels"]["pod-security.kubernetes.io/enforce"], "restricted")

    def test_pods_are_restricted_compatible(self):
        for pod in [d for d in self.docs if d["kind"] == "Pod"] + [self.client]:
            sc = pod["spec"]["containers"][0]["securityContext"]
            self.assertIs(sc["allowPrivilegeEscalation"], False)
            self.assertIs(sc["runAsNonRoot"], True)
            self.assertEqual(sc["capabilities"]["drop"], ["ALL"])

    def test_evaluate(self):
        ok = self.evaluate("PROBE_allowed-server=0\nPROBE_denied-server=1\n")
        self.assertTrue(ok["passed"])
        self.assertFalse(ok["cleanupQualified"])
        bad = self.evaluate("PROBE_allowed-server=0\nPROBE_denied-server=0\n")
        self.assertFalse(bad["passed"])
        self.assertIn("does not enforce", bad["reason"])
        self.assertNotIn("deleteRequest", bad)
        self.assertFalse(self.evaluate("PROBE_allowed-server=1\nPROBE_denied-server=1\n")["passed"])
        self.assertFalse(self.evaluate("garbage")["passed"])

    def test_servers_have_readiness_and_writable_tmp(self):
        for pod in [d for d in self.docs if d["kind"] == "Pod"]:
            container = pod["spec"]["containers"][0]
            self.assertEqual(container["readinessProbe"]["tcpSocket"]["port"], 8080)
            self.assertEqual(container["volumeMounts"], [{"name": "tmp", "mountPath": "/tmp"}])
            self.assertIn("emptyDir", pod["spec"]["volumes"][0])
            self.assertIs(container["securityContext"]["readOnlyRootFilesystem"], True)

    def test_evaluate_strictness(self):
        for log in ("xPROBE_allowed-server=0\nPROBE_denied-server=1\n",
                    "PROBE_allowed-server=0\nPROBE_denied-server=1\nPROBE_denied-server=1\n",
                    "PROBE_allowed-server=0 trailing\nPROBE_denied-server=1\n",
                    "PROBE_allowed-server=0\n"):
            result = self.evaluate(log)
            self.assertFalse(result["passed"], log)
            self.assertIn("inconclusive", result["reason"])
        control = self.evaluate("PROBE_allowed-server=1\nPROBE_denied-server=1\n")
        self.assertIn("inconclusive", control["reason"])
        self.assertFalse(self.evaluate("PROBE_allowed-server=0\nPROBE_denied-server=1\nPROBE_foo=0\n")["passed"])
        self.assertFalse(cni_probe.evaluate("PROBE_ATTEMPT=other\nPROBE_allowed-server=0\nPROBE_denied-server=1\n", ATTEMPT_ID)["passed"])

    def test_real_client_script_markers_feed_evaluate(self):
        script = cni_probe._client_script(ATTEMPT_ID)
        self.assertIn("nc -z -w 3 $t 8080", script)
        self.assertIn("PROBE_ATTEMPT=" + ATTEMPT_ID, script)
        for t, code in (("allowed-server", 0), ("denied-server", 1)):
            self.assertIn(f"echo PROBE_$t=$r", script)
        simulated = "".join(f"PROBE_{t}={0 if t == 'allowed-server' else 1}\n" for t in ("allowed-server", "denied-server"))
        self.assertTrue(self.evaluate(simulated)["passed"])

    def test_collision_and_uid_guard(self):
        name = cni_probe.namespace_for(ATTEMPT_ID)
        self.assertNotEqual(name, cni_probe.namespace_for("another-attempt"))
        self.assertEqual(self.docs[0]["metadata"]["name"], name)
        self.assertEqual(self.docs[0]["metadata"]["labels"]["hexalith.com/probe-attempt"], ATTEMPT_ID)
        with self.assertRaises(cni_probe.ProbeInputError):
            cni_probe.creation_proposal(ATTEMPT_ID, self.deadline, {"checkedNamespace": name, "exists": True})
        with self.assertRaises(cni_probe.ProbeInputError):
            cni_probe.creation_proposal(ATTEMPT_ID, self.deadline, {})
        create = cni_probe.creation_proposal(ATTEMPT_ID, self.deadline,
                                             {"checkedNamespace": name, "exists": False})
        self.assertFalse(create["executable"])
        self.assertFalse(create["observationVerified"])
        self.assertFalse(create["creationActionQualified"])
        self.assertNotIn("createArgv", create)
        uid = "4f596937-037e-43ef-a734-889e05d91349"
        observed = copy.deepcopy(self.docs[0]); observed["metadata"]["uid"] = uid
        for bad in ({}, {"metadata": {"name": name, "uid": uid}},
                    {**observed, "metadata": {**observed["metadata"], "uid": "c6553959-36ec-4eb9-8d10-43e039636576"}},
                    {**observed, "metadata": {**observed["metadata"], "name": "other"}},
                    {**observed, "metadata": {**observed["metadata"], "annotations": {"hexalith.com/cleanup-deadline": future_deadline()}}}):
            with self.assertRaises(cni_probe.ProbeInputError):
                cni_probe.cleanup_proposal(ATTEMPT_ID, self.deadline, uid, bad)
        proposal = cni_probe.cleanup_proposal(ATTEMPT_ID, self.deadline, uid, observed)
        self.assertFalse(proposal["executable"])
        self.assertFalse(proposal["observationVerified"])
        self.assertFalse(proposal["cleanupQualified"])
        self.assertEqual(proposal["uidPrecondition"], uid)
        self.assertNotIn("deleteRequest", proposal)
        self.assertNotIn("cleanupArgv", proposal)

    def test_proposal_cli_refuses_unverified_observations(self):
        with tempfile.TemporaryDirectory() as tmp:
            collision = Path(tmp) / "collision.json"
            collision.write_text("{}")
            observed = Path(tmp) / "namespace.json"
            observed.write_text("{}")
            with contextlib.redirect_stdout(io.StringIO()) as stdout, contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(cni_probe.main(["creation-proposal", "--attempt-id", ATTEMPT_ID,
                    "--cleanup-deadline", self.deadline, "--collision-observation", str(collision)]), 2)
                self.assertEqual(cni_probe.main(["cleanup-proposal", "--attempt-id", ATTEMPT_ID,
                    "--cleanup-deadline", self.deadline, "--created-uid", "4f596937-037e-43ef-a734-889e05d91349",
                    "--observed-namespace", str(observed)]), 2)
            self.assertEqual(stdout.getvalue(), "")

    def test_attempt_and_deadline_required(self):
        for attempt, deadline in (("", self.deadline), ("Bad/ID", self.deadline), (ATTEMPT_ID, ""),
                                  (ATTEMPT_ID, "2020-01-01T00:00:00Z"), (ATTEMPT_ID, "2030-01-01")):
            with self.assertRaises(cni_probe.ProbeInputError):
                cni_probe.manifests(DIGEST_IMAGE, "kube-system", {"a": "b"}, attempt, deadline)

    def test_main_exit_codes(self):
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            (tmp / "ok.log").write_text(f"PROBE_ATTEMPT={ATTEMPT_ID}\nPROBE_allowed-server=0\nPROBE_denied-server=1\n")
            (tmp / "leak.log").write_text(f"PROBE_ATTEMPT={ATTEMPT_ID}\nPROBE_allowed-server=0\nPROBE_denied-server=0\n")
            with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(cni_probe.main(["evaluate", "--log-file", str(tmp / "ok.log"), "--attempt-id", ATTEMPT_ID]), 0)
                self.assertEqual(cni_probe.main(["evaluate", "--log-file", str(tmp / "leak.log"), "--attempt-id", ATTEMPT_ID]), 1)
                self.assertEqual(cni_probe.main(["evaluate", "--log-file", str(tmp / "missing.log"), "--attempt-id", ATTEMPT_ID]), 2)
                args = ["manifests", "--dns-namespace", "kube-system", "--dns-pod-label", "k8s-app=kube-dns",
                        "--attempt-id", ATTEMPT_ID, "--cleanup-deadline", self.deadline, "--output", str(tmp / "o")]
                self.assertEqual(cni_probe.main(args + ["--image", "probe:latest"]), 2)
                self.assertFalse((tmp / "o").exists())
                self.assertEqual(cni_probe.main(args + ["--image", DIGEST_IMAGE]), 0)
            self.assertTrue((tmp / "o" / "probe.yaml").exists())

    def test_deadline_expiry_on_second_check_writes_nothing(self):
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "probe"
            args = ["manifests", "--image", DIGEST_IMAGE, "--dns-namespace", "kube-system",
                    "--dns-pod-label", "k8s-app=kube-dns", "--attempt-id", ATTEMPT_ID,
                    "--cleanup-deadline", self.deadline, "--output", str(out)]
            with mock.patch.object(cni_probe, "deadline_for", side_effect=[None, cni_probe.ProbeInputError("deadline expired")]), \
                    contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(cni_probe.main(args), 2)
            self.assertFalse(out.exists())

    def test_argv_sequence_has_checks_but_no_cleanup_action(self):
        seq = cni_probe.argv_sequence(ATTEMPT_ID, self.deadline, "p.yaml", "c.yaml")
        self.assertEqual(seq["collisionCheck"], seq["postCreateUidCheck"])
        self.assertEqual(seq["postCreateUidCheck"], seq["cleanupIdentityCheck"])
        self.assertFalse(seq["cleanupQualified"])
        for argv in [v for v in seq.values() if isinstance(v, list)]:
            self.assertEqual(argv[:3], ["kubectl", "--context", "jpiquot@local"])
            self.assertNotIn("delete", argv)


class NoClusterCallTests(unittest.TestCase):
    def test_sources_import_no_process_or_network_modules(self):
        banned = {"subprocess", "socket", "urllib", "http", "requests", "ssl", "asyncio", "multiprocessing", "ctypes"}
        for name in ("render.py", "preflight.py", "cni_probe.py"):
            for node in ast.walk(ast.parse((HERE / name).read_text())):
                modules = [a.name for a in node.names] if isinstance(node, ast.Import) else \
                    [node.module or ""] if isinstance(node, ast.ImportFrom) else []
                for module in modules:
                    self.assertNotIn(module.split(".")[0], banned, f"{name} imports {module}")

    def test_main_paths_never_spawn_or_connect(self):
        def boom(*a, **k):
            raise AssertionError("process or network call attempted")
        with tempfile.TemporaryDirectory() as tmp, \
                mock.patch.object(subprocess, "Popen", boom), mock.patch.object(os, "system", boom), \
                mock.patch.object(socket, "socket", boom), mock.patch.object(socket, "create_connection", boom), \
                contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            tmp = Path(tmp)
            src = tmp / "in.json"
            src.write_text(json.dumps(complete_inputs()))
            self.assertEqual(render.main(["render", "--inputs", str(src), "--output", str(tmp / "r")]), 0)
            self.assertEqual(render.main(["plan", "--inputs", str(src), "--observed-context", render.EXPECTED_CONTEXT,
                                          "--observed-cluster-uid", render.EXPECTED_UID]), 0)
            self.assertEqual(render.main(["plan", "--inputs", str(src), "--observed-context", "x", "--observed-cluster-uid", "y"]), 2)
            self.assertEqual(preflight.main(["render", "--inputs", str(src), "--output", str(tmp / "guarded")]), 0)
            self.assertEqual(preflight.main(["plan", "--inputs", str(src), "--observed-context", render.EXPECTED_CONTEXT,
                                             "--observed-cluster-uid", render.EXPECTED_UID]), 0)
            self.assertEqual(cni_probe.main(["manifests", "--image", DIGEST_IMAGE, "--dns-namespace", "kube-system",
                                             "--dns-pod-label", "k8s-app=kube-dns", "--attempt-id", ATTEMPT_ID,
                                             "--cleanup-deadline", future_deadline(), "--output", str(tmp / "c")]), 0)
            (tmp / "l").write_text(f"PROBE_ATTEMPT={ATTEMPT_ID}\nPROBE_allowed-server=0\nPROBE_denied-server=1\n")
            self.assertEqual(cni_probe.main(["evaluate", "--log-file", str(tmp / "l"), "--attempt-id", ATTEMPT_ID]), 0)



class ModuleAgnosticTests(unittest.TestCase):
    """The foundation is consumed by modules and must never name one (docs/DEPENDENCY-DIRECTION.md)."""

    def test_foundation_names_no_consumer_module(self):
        for name in ("render.py", "preflight.py", "cni_probe.py", "apply-plan.json", "inputs.example.json"):
            text = (HERE / name).read_text().lower()
            self.assertNotIn("timesheets", text, name)


if __name__ == "__main__":
    unittest.main()
