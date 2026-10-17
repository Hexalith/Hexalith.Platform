"""Protect the read-only collector's boundary and credential suppression."""

import json
import base64
from contextlib import nullcontext
import os
import subprocess
import sys
import unittest
from unittest.mock import patch

import importlib.util
from pathlib import Path

spec = importlib.util.spec_from_file_location("timesheets_inspection", Path(__file__).with_name("observe_native.py"))
inspection = importlib.util.module_from_spec(spec)
spec.loader.exec_module(inspection)


class InspectionTests(unittest.TestCase):
    def test_workload_projection_excludes_environment_and_secret_values(self):
        payload = {"items": [{"kind": "Deployment", "metadata": {"name": "timesheets", "uid": "id", "annotations": {"secret": "do-not-export"}},
                              "spec": {"template": {"spec": {"containers": [{"image": "registry/timesheets@sha256:abc", "env": [{"name": "TOKEN", "value": "do-not-export"}]}]}}}}]}
        result = inspection.summarize("workloads", payload)
        self.assertNotIn("do-not-export", json.dumps(result))
        self.assertEqual(["registry/timesheets@sha256:abc"], result["records"][0]["images"])

    def test_component_projection_never_retains_metadata_values(self):
        payload = {"items": [{"metadata": {"name": "timesheets-readmodels"}, "spec": {"type": "state.redis", "metadata": [{"name": "redisPassword", "value": "hidden"}]}, "scopes": ["timesheets"]}]}
        result = inspection.summarize("components", payload)
        self.assertNotIn("hidden", json.dumps(result))
        self.assertEqual(["timesheets"], result["records"][0]["scopes"])

    def test_binding_projection_preserves_exact_user_and_role_only(self):
        result = inspection.summarize("bindings", {"items": [
            {"metadata": {"name": "admin"}, "subjects": [{"kind": "User", "name": "jpiquot"}, {"kind": "User", "name": "unrelated"}], "roleRef": {"name": "cluster-admin"}},
            {"metadata": {"name": "other"}, "subjects": [{"kind": "User", "name": "other"}]}]})
        self.assertEqual(2, result["count"])
        self.assertEqual(1, len(result["records"]))
        self.assertNotIn("unrelated", json.dumps(result))

    def test_queries_contain_no_domain_secret_or_mutation_request(self):
        for _, args in inspection.QUERIES:
            self.assertIn(args[0], ("get", "auth"))
            self.assertNotIn("secrets", args)
            self.assertNotIn("exec", args)
        self.assertNotIn("client-key", inspection.tls_observation.__code__.co_consts)

    def test_wrong_server_refuses_before_native_request(self):
        config = {"clusters": [{"cluster": {"server": "https://other"}}]}
        with patch.object(inspection, "run", return_value=subprocess.CompletedProcess([], 0, json.dumps(config).encode(), b"")) as run:
            with self.assertRaisesRegex(ValueError, "Unexpected target"):
                inspection.collect()
            self.assertEqual(1, run.call_count)

    def test_insecure_context_refuses_before_native_request(self):
        config = {"clusters": [{"cluster": {"server": inspection.SERVER, "insecure-skip-tls-verify": True}}]}
        with patch.object(inspection, "run", return_value=subprocess.CompletedProcess([], 0, json.dumps(config).encode(), b"")) as run:
            with self.assertRaisesRegex(ValueError, "disabled TLS"):
                inspection.collect()
            self.assertEqual(1, run.call_count)

    def test_changed_cluster_incarnation_stops_inspection(self):
        responses = [subprocess.CompletedProcess([], 0, json.dumps({"clusters": [{"cluster": {"server": inspection.SERVER}}]}).encode(), b""),
                     subprocess.CompletedProcess([], 0, json.dumps({"metadata": {"uid": "different"}}).encode(), b"")]
        with patch.object(inspection, "frozen_context", return_value=nullcontext((99, (b"ca", b"client")))), patch.object(inspection, "run", side_effect=responses) as run:
            with self.assertRaisesRegex(ValueError, "incarnation differs"):
                inspection.collect()
            self.assertEqual(2, run.call_count)

    def test_unavailable_cluster_identity_stops_before_other_native_queries(self):
        responses = [subprocess.CompletedProcess([], 0, json.dumps({"clusters": [{"cluster": {"server": inspection.SERVER}}]}).encode(), b""),
                     subprocess.CompletedProcess([], 1, b"", b"Forbidden")]
        with patch.object(inspection, "frozen_context", return_value=nullcontext((99, (b"ca", b"client")))), patch.object(inspection, "run", side_effect=responses) as run:
            with self.assertRaisesRegex(ValueError, "identity unavailable"):
                inspection.collect()
            self.assertEqual(2, run.call_count)

    def test_process_output_limit_applies_during_both_stream_reads(self):
        for stream in ("stdout", "stderr"):
            with self.subTest(stream=stream), patch.object(inspection, "MAX_BYTES", 1024):
                with self.assertRaisesRegex(ValueError, "exceeds the bound"):
                    inspection.run([sys.executable, "-c", f"import sys; sys.{stream}.write('x'*1000000)"])

    def test_deployment_uses_template_dapr_annotations(self):
        payload = {"items": [{"kind": "Deployment", "metadata": {"name": "timesheets"}, "spec": {"template": {
            "metadata": {"annotations": {"dapr.io/app-id": "timesheets", "secret": "hidden"}}, "spec": {"containers": []}}}}]}
        row = inspection.summarize("workloads", payload)["records"][0]
        self.assertEqual({"dapr.io/app-id": "timesheets"}, row["dapr"])
        self.assertNotIn("hidden", json.dumps(row))

    def test_installed_acl_and_route_wiring_retains_only_nonsecret_fields(self):
        configuration = {"items": [{"metadata": {"name": "timesheets"}, "spec": {"mtls": {"enabled": True}, "secret": "hidden",
            "accessControl": {"trustDomain": "staging", "defaultAction": "deny", "credential": "hidden", "policies": [{"appId": "eventstore",
            "namespace": "hexalith-staging", "defaultAction": "deny", "secret": "hidden", "operations": [{"name": "/project/rebuild/shared/v1", "httpVerb": ["POST"], "action": "allow", "token": "hidden"}]}]}}}]}
        row = inspection.summarize("daprConfigurations", configuration)["records"][0]
        self.assertEqual("staging", row["accessControl"]["trustDomain"])
        self.assertNotIn("hidden", json.dumps(row))
        route = {"items": [{"metadata": {"name": "timesheets"}, "spec": {"rules": [{"backendRefs": [{"name": "timesheets-public", "port": 8080}], "filters": [{"secret": "hidden"}]}]},
            "status": {"parents": [{"conditions": [{"type": "ResolvedRefs", "status": "True", "reason": "ResolvedRefs", "message": "hidden"}]}]}}]}
        row = inspection.summarize("routes", route)["records"][0]
        self.assertEqual([{"name": "timesheets-public", "port": 8080}], row["backends"])
        self.assertEqual("True", row["conditions"][0]["status"])
        self.assertNotIn("hidden", json.dumps(row))

    def _native_response(self, command, config_changed=False, **_kwargs):
        if "view" in command:
            server = "https://changed" if config_changed else inspection.SERVER
            payload = {"clusters": [{"cluster": {"server": server}}]}
        elif "kube-system" in command:
            payload = {"metadata": {"uid": inspection.CLUSTER_UID}}
        elif "whoami" in command:
            payload = {"status": {"userInfo": {"username": "jpiquot", "extra": {"authentication.kubernetes.io/credential-id": ["X509SHA256=aa"]}}}}
        elif "can-i" in command:
            return subprocess.CompletedProcess(command, 0, b"rules", b"")
        else:
            payload = {"items": []}
        return subprocess.CompletedProcess(command, 0, json.dumps(payload).encode(), b"")

    def test_every_network_request_pins_target_and_rechecks_identity(self):
        with patch.object(inspection, "frozen_context", return_value=nullcontext((99, (b"ca", b"client")))), \
                patch.object(inspection, "run", side_effect=self._native_response) as run, \
                patch.object(inspection, "public_certificates", return_value=(b"ca", b"client")), \
                patch.object(inspection, "tls_observation", return_value={"client": {"derSha256": "aa"}}):
            result = inspection.collect()
        requests = [call.args[0] for call in run.call_args_list if "view" not in call.args[0]]
        self.assertEqual(len(inspection.QUERIES) + 1, len(requests))
        for command in requests:
            self.assertIn("--server=" + inspection.SERVER, command)
            self.assertIn("--insecure-skip-tls-verify=false", command)
            self.assertIn("--tls-server-name=192.168.1.30", command)
        self.assertFalse(result["story36Accepted"])

    def test_changed_config_or_public_certificate_refuses_combined_evidence(self):
        count = 0
        def changed(command, **_kwargs):
            nonlocal count
            if "view" in command:
                count += 1
            return self._native_response(command, config_changed=count > 1)
        with patch.object(inspection, "frozen_context", return_value=nullcontext((99, (b"ca", b"client")))), patch.object(inspection, "run", side_effect=changed), patch.object(inspection, "public_certificates", return_value=(b"ca", b"client")):
            with self.assertRaisesRegex(ValueError, "changed during inspection"):
                inspection.collect()
        with patch.object(inspection, "frozen_context", return_value=nullcontext((99, (b"ca", b"client")))), patch.object(inspection, "run", side_effect=self._native_response), \
                patch.object(inspection, "public_certificates", return_value=(b"different", b"client")):
            with self.assertRaisesRegex(ValueError, "changed during inspection"):
                inspection.collect()

    def test_frozen_context_is_sealed_anonymous_and_uses_only_selected_identity(self):
        source = {"clusters": [{"cluster": {"server": inspection.SERVER, "certificate-authority-data": base64.b64encode(b"ca").decode()}}],
                  "users": [{"user": {"client-certificate-data": base64.b64encode(b"public").decode(), "client-key-data": base64.b64encode(b"private-fixture").decode()}}],
                  "contexts": [{"name": inspection.CONTEXT}]}
        response = subprocess.CompletedProcess([], 0, json.dumps(source).encode(), b"")
        with patch.object(inspection, "run", return_value=response):
            with inspection.frozen_context() as (descriptor, public_identity):
                self.assertEqual((b"ca", b"public"), public_identity)
                with self.assertRaises(PermissionError):
                    os.write(descriptor, b"changed")
                os.lseek(descriptor, 0, os.SEEK_SET)
                frozen = json.loads(os.read(descriptor, 10000))
                self.assertEqual(source["users"][0]["user"], frozen["users"][0]["user"])
                self.assertEqual("fixed", frozen["contexts"][0]["context"]["user"])
            with self.assertRaises(OSError):
                os.fstat(descriptor)

    def test_frozen_context_refuses_plugins_external_keys_and_proxy(self):
        for change in ("exec", "file", "proxy"):
            source = {"clusters": [{"cluster": {"server": inspection.SERVER, "certificate-authority-data": "Y2E="}}],
                      "users": [{"user": {"client-certificate-data": "cHVibGlj", "client-key-data": "cHJpdmF0ZQ=="}}], "contexts": [{}]}
            if change == "exec": source["users"][0]["user"]["exec"] = {"command": "forbidden"}
            if change == "file": source["users"][0]["user"] = {"client-key": "/external"}
            if change == "proxy": source["clusters"][0]["cluster"]["proxy-url"] = "https://external"
            response = subprocess.CompletedProcess([], 0, json.dumps(source).encode(), b"")
            with self.subTest(change=change), patch.object(inspection, "run", return_value=response):
                with self.assertRaisesRegex(ValueError, "cannot be safely frozen"), inspection.frozen_context():
                    self.fail("Unsafe credential path was admitted")

    def test_ingress_backend_and_route_parent_conditions_keep_identity(self):
        ingress = {"items": [{"metadata": {"name": "timesheets"}, "spec": {"rules": [{"http": {"paths": [
            {"backend": {"service": {"name": "timesheets-public", "port": {"number": 8080}}}}]}}]}}]}
        self.assertEqual([{"name": "timesheets-public", "port": {"number": 8080}}], inspection.summarize("ingress", ingress)["records"][0]["backends"])
        default = {"items": [{"metadata": {"name": "default"}, "spec": {"defaultBackend": {"service": {"name": "timesheets-public", "port": {"number": 8080}}}}}]}
        self.assertEqual([{"name": "timesheets-public", "port": {"number": 8080}}], inspection.summarize("ingress", default)["records"][0]["backends"])
        route = {"items": [{"metadata": {"name": "timesheets"}, "status": {"parents": [
            {"parentRef": {"name": "expected", "sectionName": "https"}, "conditions": [{"type": "Accepted", "status": "False"}]},
            {"parentRef": {"name": "other"}, "conditions": [{"type": "Accepted", "status": "True"}]}]}}]}
        conditions = inspection.summarize("routes", route)["records"][0]["conditions"]
        self.assertEqual("expected", conditions[0]["owner"]["parentRef"]["name"])
        self.assertEqual("False", conditions[0]["status"])
        self.assertEqual("other", conditions[1]["owner"]["parentRef"]["name"])


if __name__ == "__main__":
    unittest.main()
