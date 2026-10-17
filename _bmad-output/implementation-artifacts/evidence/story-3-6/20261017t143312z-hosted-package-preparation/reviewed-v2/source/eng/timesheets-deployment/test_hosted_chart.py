"""Render the actual maintained chart, then verify the hosted security boundaries."""

import copy
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest

import yaml


HERE = Path(__file__).resolve().parent
HELM = os.environ.get("HELM_BIN") or shutil.which("helm")
APPLICATION = {
    "image": {"digest": "sha256:" + "a" * 64, "pullSecretName": "fixture-registry-pull"},
    "sourceRevision": "b" * 40,
    "identity": {
        "authority": "https://human.fixture.invalid/realm",
        "issuer": "https://human.fixture.invalid/realm",
        "audience": "timesheets-fixture",
        "workloadAuthority": "https://authority.fixture.invalid/workload-authority",
        "workloadIssuer": "https://authority.fixture.invalid/workload-authority",
        "tokenEndpoint": "https://authority.fixture.invalid/workload-authority/token",
        "secretName": "fixture-client-reference",
    },
    "dapr": {"appTokenSecretName": "fixture-app-channel", "apiTokenSecretName": "fixture-sidecar-channel"},
    "ingress": {"enabled": True, "gateway": {"name": "fixture-gateway", "sectionName": "fixture-https"}},
}
ENVIRONMENT = {
    "trustDomain": "fixture-staging.invalid",
    "redis": {"host": "fixture-redis.hexalith-staging-data.svc.cluster.local:6380", "passwordSecretName": "fixture-redis-credential"},
    "network": {
        "gatewayNamespace": "fixture-ingress",
        "gatewayPodLabels": {"app.kubernetes.io/name": "fixture-gateway"},
        "egress": [{"to": [{"namespaceSelector": {"matchLabels": {"kubernetes.io/metadata.name": "hexalith-staging-data"}},
                               "podSelector": {"matchLabels": {"app.kubernetes.io/name": "fixture-redis"}}}],
                    "ports": [{"protocol": "TCP", "port": 6380}]}],
    },
}


@unittest.skipUnless(HELM, "Helm 4.2.0+ required; set HELM_BIN to the qualified local tool")
class HostedChartTests(unittest.TestCase):
    """Fixture values never represent installed credentials, images, realms, or policy."""

    def render(self, values=None, *, environment=False, namespace="hexalith-staging", succeeds=True):
        chart = HERE / ("environment-chart" if environment else "chart")
        with tempfile.TemporaryDirectory(prefix="timesheets-chart-") as temporary:
            values_path = Path(temporary) / "values.json"
            values_path.write_text(json.dumps(values or {}), encoding="utf-8")
            result = subprocess.run([HELM, "template", "hexalith-platform", str(chart), "--namespace", namespace,
                                     "--values", str(values_path)], capture_output=True, text=True, timeout=30, check=False)
        if succeeds:
            self.assertEqual(result.returncode, 0, result.stderr)
            return [document for document in yaml.safe_load_all(result.stdout) if document]
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual(result.stdout, "")
        return result.stderr

    def test_unbound_candidate_never_renders_a_deployment(self):
        self.assertIn("retained image digest is required", self.render(succeeds=False))

    def test_digest_pinned_image_source_revision_and_split_ports(self):
        documents = self.render(APPLICATION)
        deployment = next(item for item in documents if item["kind"] == "Deployment")
        pod = deployment["spec"]["template"]["spec"]
        self.assertEqual(deployment["metadata"]["annotations"]["hexalith.com/source-revision"], "b" * 40)
        self.assertEqual(pod["containers"][0]["image"], "registry.hexalith.com/timesheets@sha256:" + "a" * 64)
        self.assertEqual([(port["name"], port["containerPort"]) for port in pod["containers"][0]["ports"]], [("public", 8080), ("internal", 8081)])
        self.assertTrue(pod["securityContext"]["runAsNonRoot"])
        self.assertEqual(pod["securityContext"]["seccompProfile"]["type"], "RuntimeDefault")
        self.assertEqual(pod["containers"][0]["securityContext"]["capabilities"]["drop"], ["ALL"])
        self.assertEqual(pod["containers"][0]["readinessProbe"]["httpGet"], {"path": "/ready", "port": "internal"})

    def test_public_route_matches_owner_allowlist_and_never_internal_surface(self):
        route = next(item for item in self.render(APPLICATION) if item["kind"] == "HTTPRoute")
        self.assertEqual(route["spec"]["parentRefs"][0]["namespace"], "hexalith-staging-data")
        self.assertEqual(route["spec"]["hostnames"], ["timesheets.hexalith.com"])
        matches = route["spec"]["rules"][0]["matches"]
        actual = {(item["path"]["type"], item["path"]["value"]) for item in matches}
        expected = {("Exact", path) for path in ["/api/timesheets/magic-links/confirm", "/api/timesheets/magic-links/confirm/submit",
                    "/api/timesheets/magic-links/adjust", "/api/timesheets/magic-links/adjust/submit", "/metadata/timesheets"]}
        expected.add(("PathPrefix", "/api/timesheets/magic-links/confirmation-capabilities"))
        self.assertEqual(actual, expected)
        self.assertEqual(route["spec"]["rules"][0]["backendRefs"], [{"name": "timesheets-public", "port": 8080}])

    def test_registry_pull_secret_is_required_and_only_referenced_by_the_pod(self):
        missing = copy.deepcopy(APPLICATION)
        del missing["image"]["pullSecretName"]
        self.assertIn("existing image.pullSecretName is required", self.render(missing, succeeds=False))
        documents = self.render(APPLICATION)
        pod = next(item for item in documents if item["kind"] == "Deployment")["spec"]["template"]["spec"]
        self.assertEqual(pod["imagePullSecrets"], [{"name": "fixture-registry-pull"}])
        self.assertFalse({"Secret", "Role", "RoleBinding"} & {item["kind"] for item in documents})
        self.assertNotIn("fixture-registry-pull", json.dumps(pod["containers"]))

    def test_dapr_and_application_use_the_same_existing_token_secrets(self):
        documents = self.render(APPLICATION)
        pod = next(item for item in documents if item["kind"] == "Deployment")["spec"]["template"]
        annotations = pod["metadata"]["annotations"]
        environment = {item["name"]: item["valueFrom"]["secretKeyRef"] for item in pod["spec"]["containers"][0]["env"]}
        self.assertEqual(annotations["dapr.io/app-port"], "8081")
        self.assertEqual(environment["APP_API_TOKEN"], {"name": annotations["dapr.io/app-token-secret"], "key": "token"})
        self.assertEqual(environment["DAPR_API_TOKEN"], {"name": annotations["dapr.io/api-token-secret"], "key": "token"})
        self.assertEqual(annotations["dapr.io/sidecar-listen-addresses"], "127.0.0.1")

    def test_application_package_does_not_own_environment_or_data(self):
        self.assertEqual({item["kind"] for item in self.render(APPLICATION)}, {"ConfigMap", "Service", "Deployment", "HTTPRoute"})
        config = next(item["data"] for item in self.render(APPLICATION) if item["kind"] == "ConfigMap")
        self.assertEqual(config["Timesheets__LiveDaprFixture__Enabled"], "false")
        self.assertEqual(config["Timesheets__InternalSurface__AllowOnAnyPort"], "false")
        self.assertEqual(config["Authentication__WorkloadIssuer__EnableResourceBindingRequests"], "true")
        self.assertEqual(config["Authentication__JwtBearer__Authority"], "https://human.fixture.invalid/realm")
        self.assertEqual(config["Authentication__WorkloadJwtBearer__Authority"], "https://authority.fixture.invalid/workload-authority")
        self.assertFalse(any("SigningKey" in key or "Migration" in key or "ClientSecret" in key for key in config))

    def test_recovery_and_remove_workloads_keep_ingress_closed(self):
        for mode in ("recovery", "remove-workloads"):
            with self.subTest(mode=mode):
                values = dict(APPLICATION, mode=mode)
                documents = self.render(values)
                self.assertNotIn("HTTPRoute", {item["kind"] for item in documents})
                deployments = [item for item in documents if item["kind"] == "Deployment"]
                if mode == "recovery":
                    self.assertEqual(deployments[0]["spec"]["replicas"], 0)
                else:
                    self.assertFalse(deployments)

    def test_ingress_is_closed_unless_explicitly_enabled(self):
        values = copy.deepcopy(APPLICATION)
        values["ingress"]["enabled"] = False
        self.assertNotIn("HTTPRoute", {item["kind"] for item in self.render(values)})

    def test_invalid_namespace_digest_source_https_or_algorithms_refuse(self):
        for section, key, value in (("image", "digest", "latest"), ("identity", "authority", "http://authority.fixture.invalid"),
                                    ("identity", "tokenEndpoint", "https://user:password@authority.fixture.invalid/token"),
                                    ("identity", "workloadAuthority", "http://authority.fixture.invalid"),
                                    ("identity", "allowedAlgorithms", ["HS256"])):
            with self.subTest(key=key):
                values = copy.deepcopy(APPLICATION)
                values[section][key] = value
                self.render(values, succeeds=False)
        self.render(dict(APPLICATION, sourceRevision="main"), succeeds=False)
        self.render(APPLICATION, namespace="default", succeeds=False)
        values = copy.deepcopy(APPLICATION)
        values["dapr"]["configuration"] = "uninstalled-configuration"
        self.render(values, succeeds=False)

    def test_environment_scope_enables_durable_shared_rebuild_only_for_owner(self):
        documents = self.render(ENVIRONMENT, environment=True)
        self.assertNotIn("Secret", {item["kind"] for item in documents})
        component = next(item for item in documents if item["kind"] == "Component")
        self.assertEqual(component["metadata"]["name"], "timesheets-readmodels")
        self.assertEqual(component["scopes"], ["timesheets"])
        metadata = {item["name"]: item for item in component["spec"]["metadata"]}
        self.assertEqual(metadata["enableTLS"]["value"], "true")
        self.assertEqual(metadata["actorStateStore"]["value"], "false")
        self.assertEqual(metadata["redisPassword"]["secretKeyRef"]["name"], "fixture-redis-credential")
        configuration = next(item["spec"] for item in documents if item["kind"] == "Configuration")
        self.assertEqual(configuration["features"], [{"name": "HotReload", "enabled": False}])
        access = configuration["accessControl"]
        self.assertEqual(access["defaultAction"], "deny")
        self.assertEqual(len(access["policies"]), 1)
        caller = access["policies"][0]
        self.assertEqual((caller["appId"], caller["namespace"], caller["trustDomain"]), ("eventstore", "hexalith-staging", "fixture-staging.invalid"))
        self.assertIn({"name": "/project/rebuild/shared/v1", "httpVerb": ["POST"], "action": "allow"}, caller["operations"])
        self.assertFalse(any("*" in operation["name"] for operation in caller["operations"]))
        role = next(item for item in documents if item["kind"] == "Role")
        self.assertEqual(role["rules"], [{"apiGroups": [""], "resources": ["secrets"], "resourceNames": ["fixture-redis-credential"], "verbs": ["get"]}])

    def test_network_policy_restricts_public_and_private_listeners_independently(self):
        policy = next(item["spec"] for item in self.render(ENVIRONMENT, environment=True) if item["kind"] == "NetworkPolicy")
        self.assertEqual(set(policy["policyTypes"]), {"Ingress", "Egress"})
        public, private = policy["ingress"]
        self.assertEqual(public["ports"], [{"protocol": "TCP", "port": 8080}])
        self.assertEqual(public["from"][0]["namespaceSelector"]["matchLabels"]["kubernetes.io/metadata.name"], "fixture-ingress")
        self.assertEqual(private["from"], [{"podSelector": {"matchLabels": {"app.kubernetes.io/name": "eventstore"}}}])
        self.assertEqual({entry["port"] for entry in private["ports"]}, {8081, 50002})
        self.assertEqual(policy["egress"], ENVIRONMENT["network"]["egress"])

    def test_missing_or_default_open_environment_bindings_refuse(self):
        self.render(environment=True, succeeds=False)
        values = copy.deepcopy(ENVIRONMENT)
        values["trustDomain"] = "public"
        self.render(values, environment=True, succeeds=False)
        values = copy.deepcopy(ENVIRONMENT)
        values["network"]["egress"] = []
        self.render(values, environment=True, succeeds=False)
        values["network"]["egress"] = [{}]
        self.render(values, environment=True, succeeds=False)

    def test_caller_supplied_secret_literals_are_not_supported_values(self):
        values = copy.deepcopy(APPLICATION)
        values["identity"]["clientSecret"] = "must-not-be-rendered"
        self.render(values, succeeds=False)

    def test_private_acl_stays_in_parity_with_installed_sdk_routes(self):
        sdk = HERE.parents[2] / "Hexalith.EventStore/src/Hexalith.EventStore.DomainService/EventStoreDomainServiceExtensions.cs"
        source = sdk.read_text(encoding="utf-8")
        expected = set(re.findall(r'"(/(?:project|process|query|replay-state|admin)[^" ]*)"', source))
        self.assertIn("/project/rebuild/shared/v1", expected)
        configuration = next(item["spec"] for item in self.render(ENVIRONMENT, environment=True) if item["kind"] == "Configuration")
        actual = {item["name"] for item in configuration["accessControl"]["policies"][0]["operations"]}
        self.assertEqual(actual, expected)

    def test_retained_package_renders_the_same_objects_as_its_source(self):
        for chart, values in (("chart", APPLICATION), ("environment-chart", ENVIRONMENT)):
            with self.subTest(chart=chart), tempfile.TemporaryDirectory(prefix="timesheets-package-") as temporary:
                root = Path(temporary)
                values_path = root / "fixture-values.json"
                values_path.write_text(json.dumps(values), encoding="utf-8")
                package = subprocess.run([HELM, "package", str(HERE / chart), "--destination", str(root)],
                                         capture_output=True, text=True, timeout=30, check=False)
                self.assertEqual(package.returncode, 0, package.stderr)
                archives = list(root.glob("*.tgz"))
                self.assertEqual(len(archives), 1)
                rendered = subprocess.run([HELM, "template", "hexalith-platform", str(archives[0]), "--namespace", "hexalith-staging",
                                          "--values", str(values_path)], capture_output=True, text=True, timeout=30, check=False)
                self.assertEqual(rendered.returncode, 0, rendered.stderr)
                archived_objects = [document for document in yaml.safe_load_all(rendered.stdout) if document]
                self.assertEqual(archived_objects, self.render(values, environment=chart == "environment-chart"))


if __name__ == "__main__":
    unittest.main()
