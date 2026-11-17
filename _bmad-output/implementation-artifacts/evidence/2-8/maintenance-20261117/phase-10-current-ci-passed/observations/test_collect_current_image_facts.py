"""Resource-free registry authentication and digest-boundary controls."""
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
import urllib.error
import urllib.parse
from unittest.mock import patch

from collect_current_image_facts import Registry, collect, decode, digest, main, token_locator


class ImageFactsTests(unittest.TestCase):
    def fixture(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        config = json.dumps({"os": "linux", "architecture": "amd64", "config": {
            "Env": ["SECRET=synthetic-never-retain"], "Labels": {"private": "synthetic-never-retain",
                "org.opencontainers.image.revision": "a" * 40}}}).encode()
        manifest = json.dumps({"schemaVersion": 2, "config": {"digest": digest(config)}, "layers": []}).encode()
        index = json.dumps({"schemaVersion": 2, "manifests": [{"digest": digest(manifest),
            "platform": {"os": "linux", "architecture": "amd64"}}]}).encode()
        values = {"7.0.7": index, digest(manifest): manifest, digest(config): config}
        class Client:
            def get(self, repository, kind, reference, expected=None):
                raw = values[reference]
                if expected is not None and digest(raw) != expected:
                    raise ValueError("changed fixture body")
                return raw, digest(raw)
        return Path(temporary.name), values, Client()

    def test_complete_index_binds_real_child_and_config_without_retaining_environment(self):
        output, values, client = self.fixture()
        record = collect(client, "tenants", "7.0.7", output)
        self.assertEqual(record["tagDigest"], digest(values["7.0.7"]))
        self.assertFalse(record["qualificationCredit"])
        for path in output.iterdir():
            self.assertNotIn("synthetic-never-retain", path.read_text())

    def test_wrong_or_duplicate_platform_cannot_supply_a_linux_amd64_identity(self):
        for fault in ("duplicate", "wrong-os", "wrong-arch"):
            output, values, client = self.fixture()
            index = json.loads(values["7.0.7"])
            if fault == "duplicate": index["manifests"].append(index["manifests"][0])
            else: index["manifests"][0]["platform"]["os" if fault == "wrong-os" else "architecture"] = "unqualified"
            values["7.0.7"] = json.dumps(index).encode()
            with self.subTest(fault=fault), self.assertRaisesRegex(ValueError, "exactly one"):
                collect(client, "tenants", "7.0.7", output)

    def test_changed_child_body_refuses_before_configuration_export(self):
        output, values, client = self.fixture()
        child = json.loads(values["7.0.7"])["manifests"][0]["digest"]
        values[child] += b" "
        with self.assertRaisesRegex(ValueError, "changed fixture body"):
            collect(client, "tenants", "7.0.7", output)
        self.assertFalse((output / "tenants-config-observation.json").exists())

    def test_token_request_uses_fixed_pull_scope_and_exact_registry_origin(self):
        challenge = 'Bearer realm="https://registry.hexalith.com/token",service="registry",scope="repository:foreign:pull,push"'
        url = urllib.parse.urlsplit(token_locator(challenge, "tenants"))
        self.assertEqual(urllib.parse.parse_qs(url.query)["scope"], ["repository:tenants:pull"])
        for realm in ("https://foreign.example/token", "http://registry.hexalith.com/token",
                      "https://registry.hexalith.com@foreign.example/token", "https://registry.hexalith.com/token?scope=push"):
            with self.subTest(realm=realm), self.assertRaisesRegex(ValueError, "trusted registry origin"):
                token_locator('Bearer realm="' + realm + '",service="registry"', "tenants")

    def test_duplicate_json_fields_are_refused(self):
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            decode(b'{"schemaVersion":2,"schemaVersion":1}')

    def test_basic_reader_stays_on_the_original_read_request_and_does_not_claim_token_scope(self):
        client = Registry("synthetic-reader", "synthetic-password")
        raw = b'{"schemaVersion":2}'
        challenge = urllib.error.HTTPError("https://registry.hexalith.com/v2/tenants/manifests/7.0.7", 401,
            "Unauthorized", {"WWW-Authenticate": 'Basic realm="registry"'}, None)
        with patch.object(client, "_read", side_effect=[challenge, (raw, {"Docker-Content-Digest": digest(raw)})]) as read:
            self.assertEqual((raw, digest(raw)), client.get("tenants", "manifests", "7.0.7"))
        self.assertEqual(read.call_args_list[0].args[0], read.call_args_list[1].args[0])
        self.assertTrue(read.call_args_list[1].args[1].startswith("Basic "))
        self.assertEqual(set(), client.token_scopes)

    def test_local_invocation_refuses_before_credentials_or_registry_client(self):
        with tempfile.TemporaryDirectory() as temporary, patch.dict(os.environ, {}, clear=True), \
                patch.object(sys, "argv", ["collect", "--output", temporary + "/facts"]), \
                patch("collect_current_image_facts.Registry") as registry:
            with self.assertRaisesRegex(ValueError, "disposable hosted Parties job"):
                main()
            registry.assert_not_called()
            self.assertFalse(Path(temporary, "facts").exists())


if __name__ == "__main__":
    unittest.main()
