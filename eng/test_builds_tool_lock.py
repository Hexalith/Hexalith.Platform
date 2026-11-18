"""Lock generation and enforcement controls with synthetic bytes and mocked nuget.org reads; no network."""
import base64
import contextlib
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import builds_tool_lock
from builds_tool_lock import content_hash, load_lock, lock_tool, locked_entry, published_package_hash, require_locked
from builds_tuple import ROOT, load_binding

PACKAGE = b"synthetic signed package bytes"
REGISTRATION = "https://api.nuget.org/v3/registration5-gz-semver2/hexalith.builds.module.cli/6.0.5.json"
CATALOG = "https://api.nuget.org/v3/catalog0/data/synthetic/hexalith.builds.module.cli.6.0.5.json"
CONTENT = "https://api.nuget.org/v3-flatcontainer/hexalith.builds.module.cli/6.0.5/hexalith.builds.module.cli.6.0.5.nupkg"


def signed(_path):
    return subprocess.CompletedProcess([], 0, "Successfully verified package", "")


class ToolLockTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.entry = {"@id": CATALOG, "id": "Hexalith.Builds.Module.Cli", "version": "6.0.5",
                      "packageHash": content_hash(PACKAGE), "packageHashAlgorithm": "SHA512"}
        self.responses = {REGISTRATION: json.dumps({"catalogEntry": CATALOG}).encode(), CONTENT: PACKAGE}

    def get(self, url, bound):
        if url == CATALOG:
            return json.dumps(self.entry).encode()
        return self.responses[url]

    def test_content_hash_is_nugets_base64_sha512(self):
        self.assertEqual(content_hash(PACKAGE), base64.b64encode(hashlib.sha512(PACKAGE).digest()).decode())

    def test_committed_lock_names_the_pinned_tool_version(self):
        entry = locked_entry(load_lock(), load_binding()["version"])
        self.assertEqual(set(entry), {"id", "version", "contentHash"})
        self.assertEqual(entry["id"], "Hexalith.Builds.Module.Cli")
        self.assertEqual(len(base64.b64decode(entry["contentHash"], validate=True)), 64)
        self.assertEqual(json.loads((ROOT / "eng/builds-tool.lock.json").read_bytes())["schemaVersion"], 1)

    def test_signature_check_runs_exactly_dotnet_nuget_verify_all(self):
        path = self.root / "hexalith.builds.module.cli.6.0.5.nupkg"
        completed = subprocess.CompletedProcess([], 0, "verified", "")
        with patch.object(builds_tool_lock.subprocess, "run", return_value=completed) as run:
            self.assertIs(builds_tool_lock.verify_signature(path), completed)
        run.assert_called_once()
        self.assertEqual(run.call_args.args[0], ["dotnet", "nuget", "verify", str(path), "--all"])

    def test_lock_is_written_only_after_signature_and_nuget_agreement(self):
        self.assertEqual(lock_tool("6.0.5", get=self.get, verify=signed),
                         {"schemaVersion": 1, "packages": [{"id": "Hexalith.Builds.Module.Cli", "version": "6.0.5",
                                                            "contentHash": content_hash(PACKAGE)}]})
        archive = self.root / "local.nupkg"
        archive.write_bytes(PACKAGE)
        self.assertEqual(lock_tool("6.0.5", archive, get=self.get, verify=signed)["packages"][0]["contentHash"], content_hash(PACKAGE))
        with self.assertRaisesRegex(ValueError, "nuget verify --all refused"):
            lock_tool("6.0.5", get=self.get, verify=lambda path: subprocess.CompletedProcess([], 1, "", "NU3004"))
        self.entry["packageHash"] = content_hash(b"other bytes")
        with self.assertRaisesRegex(ValueError, "but nuget.org packageHash is"):
            lock_tool("6.0.5", get=self.get, verify=signed)

    def test_nuget_package_hash_requires_the_exact_id_version_and_sha512(self):
        self.assertEqual(published_package_hash("6.0.5", get=self.get), content_hash(PACKAGE))
        for key, value in (("id", "Other.Package"), ("version", "6.0.6"), ("packageHashAlgorithm", "SHA256"), ("packageHash", None)):
            with self.subTest(key=key):
                previous = self.entry[key]
                self.entry[key] = value
                with self.assertRaisesRegex(ValueError, "no SHA512 packageHash"):
                    published_package_hash("6.0.5", get=self.get)
                self.entry[key] = previous

    def test_enforcement_refuses_tampered_stale_and_missing_entries(self):
        document = {"schemaVersion": 1, "packages": [{"id": "Hexalith.Builds.Module.Cli", "version": "6.0.5", "contentHash": content_hash(PACKAGE)}]}
        entry = locked_entry(document, "6.0.5")
        self.assertEqual(require_locked(entry, PACKAGE), content_hash(PACKAGE))
        with self.assertRaisesRegex(ValueError, "Tool package Hexalith.Builds.Module.Cli 6.0.5 has contentHash .* differs"):
            require_locked(entry, PACKAGE + b"tampered")
        with self.assertRaisesRegex(ValueError, "names Hexalith.Builds.Module.Cli 6.0.5, but the Builds pin is 6.0.6"):
            locked_entry(document, "6.0.6")
        with self.assertRaisesRegex(ValueError, "no entry for Hexalith.Builds.Module.Cli"):
            locked_entry({"schemaVersion": 1, "packages": []}, "6.0.5")

    def test_malformed_lock_documents_refuse(self):
        valid = {"id": "Hexalith.Builds.Module.Cli", "version": "6.0.5", "contentHash": content_hash(PACKAGE)}
        path = self.root / "lock.json"
        for document in ({"schemaVersion": 2, "packages": [valid]}, {"schemaVersion": 1, "packages": [valid], "extra": 1},
                         {"schemaVersion": 1, "packages": {}}, {"schemaVersion": 1, "packages": [{**valid, "sha256": "a" * 64}]},
                         {"schemaVersion": 1, "packages": [valid, valid]}, {"schemaVersion": 1, "packages": [{**valid, "version": ""}]},
                         {"schemaVersion": 1, "packages": [{**valid, "contentHash": "not base64!"}]},
                         {"schemaVersion": 1, "packages": [{**valid, "contentHash": base64.b64encode(b"short").decode()}]}):
            path.write_text(json.dumps(document))
            with self.subTest(document=document), self.assertRaises(ValueError):
                load_lock(path)
        path.unlink()
        with self.assertRaisesRegex(ValueError, "missing or unreadable"):
            load_lock(path)

    def test_lock_command_writes_the_pinned_version_and_never_a_failed_lock(self):
        output = self.root / "builds-tool.lock.json"
        with contextlib.ExitStack() as stack:
            stack.enter_context(patch.object(sys, "argv", ["lock", "--output", str(output)]))
            stack.enter_context(patch.object(builds_tool_lock, "load_binding", return_value={"schemaVersion": 2, "version": "6.0.5", "releaseTag": "v6.0.5"}))
            stack.enter_context(patch.object(builds_tool_lock, "validate_owner_pins"))
            stack.enter_context(patch.object(builds_tool_lock, "_get", side_effect=self.get))
            stack.enter_context(patch.object(builds_tool_lock, "verify_signature", side_effect=signed))
            stack.enter_context(contextlib.redirect_stdout(io.StringIO()))
            # The default arguments bind at definition time; route them through the patched transport.
            stack.enter_context(patch.object(builds_tool_lock, "lock_tool", side_effect=lambda version, archive=None:
                                             lock_tool(version, archive, get=self.get, verify=signed)))
            builds_tool_lock.main()
            self.assertEqual(load_lock(output)["packages"][0]["version"], "6.0.5")
            output.unlink()
            self.entry["packageHash"] = content_hash(b"other bytes")
            with self.assertRaisesRegex(ValueError, "nuget.org packageHash"):
                builds_tool_lock.main()
        self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main()
