"""Publication and source controls use temporary files and mocked reads; never create Git commits."""
import contextlib
import copy
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile

import prepare_builds_artifacts
import run_tuple_ci
from unittest.mock import patch

from builds_tuple import BINDING, ROOT, load_binding, validate_owner_pins, validate_recorded_source, verify_catalog


class BuildsTupleTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.path = self.root / "binding.json"
        # A fully bound historical release is a control, never a proposed future publication.
        self.binding = copy.deepcopy(load_binding(require_publication=False))
        self.binding["producer"]["sourceRevision"] = "d1931b1e803144048d927fff16cb8119afde541e"
        self.binding["producer"]["catalog"].update(
            blob="679a0c342f24aa40905eb84ad274f7e6647c995e",
            sha256="501d72650e3a42afc8fde0345a69208669e7fabdb98b6300f20afca07585d405")
        self.binding["publication"] = {
            "version": "4.38.3", "sourceRevision": self.binding["producer"]["sourceRevision"],
            "releaseTag": "v4.38.3", "catalogSha256": self.binding["producer"]["catalog"]["sha256"],
            "archiveSha256": "6c074186a9bbfb66c558fe5d1090e1e35fdd58577c0396266074bd1cea8919fd",
            "toolingSha256": "4e307577ae4fb02104914951a9905a1c9dd453447a80735443d24fbc83496823",
            "inventorySha256": "2421408b564af44ba75a83670aee147d21347d09ed5b823d091c24a6dbc29b5e",
            "inventoryUrl": "https://github.com/Hexalith/Hexalith.Builds/releases/download/v4.38.3/g4-tool-package-inventory.json"}

    def load(self):
        self.path.write_text(json.dumps(self.binding))
        return load_binding(self.path)

    def test_complete_historical_binding_is_readable(self):
        self.assertEqual(self.load(), self.binding)

    def test_each_missing_publication_field_refuses(self):
        for key in self.binding["publication"]:
            with self.subTest(key=key):
                original = self.binding["publication"][key]
                self.binding["publication"][key] = None
                with self.assertRaisesRegex(ValueError, "publication binding is unavailable"):
                    self.load()
                self.binding["publication"][key] = original

    def test_supported_nuget_version_boundaries(self):
        valid = ("4.0.0", "4.38.3", "4.38.3-0", "4.38.3-beta--x", "4.38.3-alpha.0", "4.2147483647.2147483647")
        invalid = ("5.0.0", "3.9.9", "4.0", "04.0.0", "4.01.0", "4.0.01", "4.38.3-01", "4.38.3-beta.01", "4.38.3-alpha..x", "4.38.3-beta_x", "4.38.3+build", "4.2147483648.0")
        for version in (*valid, *invalid):
            with self.subTest(version=version):
                self.binding["publication"].update(version=version, releaseTag="v" + version,
                    inventoryUrl="https://github.com/Hexalith/Hexalith.Builds/releases/download/v" + version + "/g4-tool-package-inventory.json")
                if version in valid:
                    self.assertEqual(self.load()["publication"]["version"], version)
                else:
                    with self.assertRaisesRegex(ValueError, "exact supported 4.x NuGet"):
                        self.load()

    def test_inventory_requires_digest_and_exact_official_release_asset(self):
        for key, value, message in (("inventorySha256", "unavailable", "SHA-256"),
                                   ("inventoryUrl", "https://example.com/g4-tool-package-inventory.json", "official release asset"),
                                   ("inventoryUrl", "https://github.com/Hexalith/Hexalith.Builds/releases/download/v4.35.2/g4-tool-package-inventory.json", "official release asset")):
            with self.subTest(key=key, value=value):
                original = self.binding["publication"][key]
                self.binding["publication"][key] = value
                with self.assertRaisesRegex(ValueError, message):
                    self.load()
                self.binding["publication"][key] = original

    def test_source_tag_and_catalog_mismatches_refuse(self):
        for key, value in (("sourceRevision", load_binding(require_publication=False)["producer"]["sourceRevision"]),
                           ("releaseTag", "v4.35.2"), ("catalogSha256", load_binding(require_publication=False)["producer"]["catalog"]["sha256"])):
            with self.subTest(key=key):
                original = self.binding["publication"][key]
                self.binding["publication"][key] = value
                with self.assertRaisesRegex(ValueError, "differs"):
                    self.load()
                self.binding["publication"][key] = original

    def test_short_source_and_malformed_hash_refuse(self):
        self.binding["producer"]["sourceRevision"] = "d1931b1"
        with self.assertRaisesRegex(ValueError, "full lowercase Git"):
            self.load()
        self.binding["producer"]["sourceRevision"] = self.binding["publication"]["sourceRevision"]
        self.binding["publication"]["archiveSha256"] = "unavailable"
        with self.assertRaisesRegex(ValueError, "SHA-256"):
            self.load()

    def test_candidate_acceptance_cannot_be_enabled(self):
        self.binding["candidateAcceptance"] = True
        with self.assertRaisesRegex(ValueError, "candidateAcceptance:false"):
            self.load()

    def test_owner_tool_and_floor_must_both_match(self):
        (self.root / ".config").mkdir()
        tools = self.root / ".config/dotnet-tools.json"
        floor = self.root / "hexalith-platform.json"
        tools.write_text(json.dumps({"tools": {"hexalith.builds.module.cli": {"version": "4.38.3"}}}))
        floor.write_text(json.dumps({"toolVersionRange": "[4.38.3, 5.0.0-0)"}))
        validate_owner_pins(self.binding, self.root)
        for path, content in ((tools, {"tools": {"hexalith.builds.module.cli": {"version": "4.35.2"}}}),
                              (floor, {"toolVersionRange": "[4.35.2, 5.0.0-0)"})):
            with self.subTest(path=path.name):
                original = path.read_bytes()
                path.write_text(json.dumps(content))
                with self.assertRaisesRegex(ValueError, "owner tool pin and range floor"):
                    validate_owner_pins(self.binding, self.root)
                path.write_bytes(original)

    def test_dirty_or_wrong_recorded_owner_refuses(self):
        source = self.binding["producer"]["sourceRevision"]
        validate_recorded_source(self.binding, source, False)
        for pin, dirty in ((source, True), (load_binding(require_publication=False)["producer"]["sourceRevision"], False)):
            with self.subTest(pin=pin, dirty=dirty), self.assertRaisesRegex(ValueError, "clean recorded Platform"):
                validate_recorded_source(self.binding, pin, dirty)

    def test_checkout_catalog_source_and_blob_are_checked(self):
        for outputs, message in (([b"wrong\n"], "checkout differs"),
                                 ([self.binding["producer"]["sourceRevision"].encode(), b"wrong\n"], "catalog blob differs"),
                                 ([self.binding["producer"]["sourceRevision"].encode(), self.binding["producer"]["catalog"]["blob"].encode(), b"changed"], "catalog SHA-256 differs")):
            with self.subTest(message=message), patch("builds_tuple.subprocess.check_output", side_effect=outputs), self.assertRaisesRegex(ValueError, message):
                verify_catalog(self.binding, self.root)

    def test_real_catalog_checkout_returns_path_with_git_crlf_normalization(self):
        # Read the existing dependency only; never create fixture commits or change its checkout/index.
        sibling = ROOT.parent / "Hexalith.Builds"
        builds = sibling if (sibling / "Props/Directory.Packages.props").is_file() else ROOT / "references/Hexalith.Builds"
        catalog = builds / "Props/Directory.Packages.props"
        def git(*arguments):
            return subprocess.check_output(["git", *arguments], cwd=builds)
        committed = git("show", "HEAD:Props/Directory.Packages.props")
        self.assertNotIn(b"\r\n", committed)
        self.assertIn(b"\r\n", catalog.read_bytes())
        self.assertEqual(catalog.read_bytes().replace(b"\r\n", b"\n"), committed)
        self.binding["producer"]["sourceRevision"] = git("rev-parse", "HEAD").decode().strip()
        self.binding["producer"]["catalog"].update(
            blob=git("rev-parse", "HEAD:Props/Directory.Packages.props").decode().strip(),
            sha256=hashlib.sha256(committed).hexdigest())
        self.assertEqual(verify_catalog(self.binding, builds), catalog)

    def test_dirty_catalog_and_git_errors_retain_public_diff_observation(self):
        canonical = b"<Project />\n"
        self.binding["producer"]["catalog"]["sha256"] = hashlib.sha256(canonical).hexdigest()
        outputs = [self.binding["producer"]["sourceRevision"].encode(), self.binding["producer"]["catalog"]["blob"].encode(), canonical]
        public_diff = "diff --git a/Props/Directory.Packages.props b/Props/Directory.Packages.props\n-<Project />\n+<Project Changed='true' />\n"
        command = ["git", "diff", "--exit-code", "HEAD", "--", "Props/Directory.Packages.props"]
        for code, stdout, stderr in ((1, public_diff, ""), (128, "", "fatal: public catalog comparison failed")):
            with self.subTest(exitCode=code), patch("builds_tuple.subprocess.check_output", side_effect=outputs), patch("builds_tuple.subprocess.run", return_value=subprocess.CompletedProcess(command, code, stdout, stderr)):
                with self.assertRaisesRegex(ValueError, "catalog checkout comparison failed") as failure:
                    verify_catalog(self.binding, self.root)
                observation = json.loads(str(failure.exception).split(": ", 1)[1])
                self.assertEqual(observation, {"argv": command, "cwd": str(self.root), "exitCode": code, "stdout": stdout, "stderr": stderr})

    def test_recorded_pin_and_catalog_refusals_precede_all_artifact_side_effects(self):
        source = self.binding["producer"]["sourceRevision"]
        other = load_binding(require_publication=False)["producer"]["sourceRevision"]
        for name, pin, dirty, catalog_error in (("pin", other, False, None), ("dirty", source, True, None), ("catalog", source, False, ValueError("catalog identity mismatch"))):
            with self.subTest(refusal=name), contextlib.ExitStack() as stack:
                output = self.root / ("recorded-" + name)
                stack.enter_context(patch.object(sys, "argv", ["prepare", "--output", str(output), "--require-recorded"]))
                stack.enter_context(patch.object(prepare_builds_artifacts, "load_binding", return_value=self.binding))
                stack.enter_context(patch.object(prepare_builds_artifacts, "validate_owner_pins"))
                stack.enter_context(patch.object(prepare_builds_artifacts, "source_state", return_value={"dirty": dirty}))
                reads = stack.enter_context(patch.object(prepare_builds_artifacts.subprocess, "check_output", side_effect=[source, pin]))
                catalog_check = stack.enter_context(patch.object(prepare_builds_artifacts, "verify_catalog", side_effect=catalog_error))
                signature = stack.enter_context(patch.object(prepare_builds_artifacts.subprocess, "run"))
                download = stack.enter_context(patch.object(prepare_builds_artifacts.urllib.request, "urlopen"))
                with self.assertRaisesRegex(ValueError, "catalog identity mismatch" if catalog_error else "clean recorded Platform"):
                    prepare_builds_artifacts.main()
                self.assertFalse(output.exists())
                self.assertEqual(reads.call_count, 2)  # Owner HEAD and pin only; no executable invocation.
                signature.assert_not_called()
                download.assert_not_called()
                if catalog_error:
                    catalog_check.assert_called_once()
                else:
                    catalog_check.assert_not_called()

    def test_archive_control_checks_executable_source_and_keeps_package_version(self):
        # Synthetic bytes exercise extraction/receipt mechanics; they are never publication evidence.
        archive = self.root / "control.nupkg"
        payload = b"synthetic unit-test tooling"
        with zipfile.ZipFile(archive, "w") as package:
            package.writestr("tools/net10.0/any/Hexalith.Builds.Tooling.dll", payload)
            package.writestr("tools/net10.0/any/Hexalith.Builds.Module.Cli.dll", b"synthetic CLI")
        self.binding["publication"]["archiveSha256"] = hashlib.sha256(archive.read_bytes()).hexdigest()
        self.binding["publication"]["toolingSha256"] = hashlib.sha256(payload).hexdigest()
        source = self.binding["producer"]["sourceRevision"]
        for index, executable in enumerate(("4.38.3+" + source, "4.38.3+wrong-source")):
            output = self.root / ("archive-control-" + str(index))
            with patch.object(sys, "argv", ["prepare", "--archive", str(archive), "--output", str(output)]), patch.object(prepare_builds_artifacts, "load_binding", return_value=self.binding), patch.object(prepare_builds_artifacts, "validate_owner_pins"), patch.object(prepare_builds_artifacts, "source_state", return_value={"dirty": False}), patch.object(prepare_builds_artifacts.subprocess, "check_output", side_effect=[source, executable]), patch.object(prepare_builds_artifacts.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, "verified control", "")), contextlib.redirect_stdout(io.StringIO()):
                if index:
                    with self.assertRaisesRegex(ValueError, "executable source/version"):
                        prepare_builds_artifacts.main()
                else:
                    prepare_builds_artifacts.main()
                    receipt = json.loads((output / "artifact-identity.json").read_bytes())
                    self.assertEqual(receipt["version"], "4.38.3")
                    self.assertEqual(receipt["buildsRevision"], source)
                    self.assertIs(receipt["candidateAcceptance"], False)

    def test_preparation_blocks_artifact_download_and_retains_terminal_preflight(self):
        self.binding["publication"]["version"] = None
        self.path.write_text(json.dumps(self.binding))
        incomplete = lambda: load_binding(self.path)
        output = self.root / "artifacts"
        with patch.object(sys, "argv", ["prepare", "--output", str(output), "--require-recorded"]), patch.object(prepare_builds_artifacts, "load_binding", incomplete), self.assertRaisesRegex(ValueError, "publication binding is unavailable"):
            prepare_builds_artifacts.main()
        self.assertFalse(output.exists())
        evidence = self.root / "preflight"
        with patch.object(sys, "argv", ["preflight", "--evidence", str(evidence), "--audit-only"]), patch.object(run_tuple_ci.signal, "signal"), patch.object(run_tuple_ci, "load_binding", incomplete), self.assertRaisesRegex(ValueError, "publication binding is unavailable"):
            run_tuple_ci.main()
        receipt = json.loads((evidence / "tuple-ci-result.json").read_bytes())
        self.assertEqual(receipt["status"], "failed")
        self.assertIs(receipt["candidateAcceptance"], False)
        self.assertIn("publication binding is unavailable", receipt["blocker"])


if __name__ == "__main__":
    unittest.main()
