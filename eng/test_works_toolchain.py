"""The Works gate and CI consume one Builds-derived, Platform-qualified runtime selection."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import works_toolchain

QUALIFIED = hashlib.sha256(b"qualified runtime").hexdigest()


def catalog(runtime="1.18.7", cli="1.18.2", extra=""):
    return ("<Project><PropertyGroup><HexalithDaprRuntimeVersion>" + runtime + "</HexalithDaprRuntimeVersion>"
            "<HexalithDaprCliVersion>" + cli + "</HexalithDaprCliVersion>" + extra + "</PropertyGroup></Project>")


class WorksToolchainTests(unittest.TestCase):
    def fixture(self, pins=None, props=None):
        """A Platform root whose Builds catalog resolves through its own references directory."""
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name) / "platform"
        (root / "config").mkdir(parents=True)
        (root / "references/Hexalith.Builds/Props").mkdir(parents=True)
        (root / works_toolchain.PIN).write_text(json.dumps(
            {"qualifiedDaprRuntime": "1.18.7", "linuxAmd64DaprdSha256": QUALIFIED} if pins is None else pins))
        (root / "references/Hexalith.Builds/Props/Directory.Packages.props").write_text(catalog() if props is None else props)
        return root

    def test_ci_outputs_the_owner_pins(self):
        root = Path(__file__).resolve().parents[1]
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "output"
            subprocess.run([sys.executable, str(root / "eng/works_toolchain.py"), "--github-output", str(output)], check=True)
            pins = works_toolchain.load(root)
            self.assertEqual(output.read_text(), "dapr-cli=" + pins["daprCli"] + "\ndapr-runtime=" + pins["daprRuntime"] + "\n")

    def test_versions_are_derived_from_the_builds_catalog_that_composes_the_control_plane(self):
        root = self.fixture()
        pins = works_toolchain.load(root, environ={})
        self.assertEqual((pins["daprRuntime"], pins["daprCli"]), ("1.18.7", "1.18.2"))
        self.assertEqual(Path(pins["catalog"]), root / "references/Hexalith.Builds/Props/Directory.Packages.props")
        self.assertEqual(pins["linuxAmd64DaprdSha256"], QUALIFIED)
        # The same resolution order as Directory.Build.props: explicit root, then sibling, then references.
        sibling = root.parent / "Hexalith.Builds/Props"
        sibling.mkdir(parents=True)
        (sibling / "Directory.Packages.props").write_text(catalog(cli="1.18.3"))
        self.assertEqual(works_toolchain.load(root, environ={})["daprCli"], "1.18.3")
        explicit = root.parent / "explicit-builds/Props"
        explicit.mkdir(parents=True)
        (explicit / "Directory.Packages.props").write_text(catalog(cli="1.18.4"))
        self.assertEqual(works_toolchain.load(root, environ={"HexalithBuildsRoot": str(explicit.parent)})["daprCli"], "1.18.4")

    def test_selects_only_the_record_for_the_catalog_runtime(self):
        root = self.fixture(props=catalog(runtime="1.19.2", cli="1.19.1"))
        path = root / "config/works-proof-toolchains/1.19.2.json"
        path.parent.mkdir()
        path.write_text(json.dumps({"qualifiedDaprRuntime": "1.19.2", "linuxAmd64DaprdSha256": "a" * 64}))
        selected = works_toolchain.load(root, environ={})
        self.assertEqual("a" * 64, selected["linuxAmd64DaprdSha256"])
        self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), selected["pinSha256"])
        path.write_text(path.read_text().replace("1.19.2", "1.19.1"))
        with self.assertRaisesRegex(ValueError, "requalify"):
            works_toolchain.load(root, environ={})

    def test_a_catalog_runtime_without_a_requalified_hash_refuses(self):
        root = self.fixture(props=catalog(runtime="1.18.8"))
        with self.assertRaisesRegex(ValueError, "requalify"):
            works_toolchain.load(root, environ={})

    def test_invalid_pin_file_refuses(self):
        for name, pins in {
            "short-version": {"qualifiedDaprRuntime": "1.18", "linuxAmd64DaprdSha256": QUALIFIED},
            "prerelease-version": {"qualifiedDaprRuntime": "1.18.7-rc.1", "linuxAmd64DaprdSha256": QUALIFIED},
            "floating-version": {"qualifiedDaprRuntime": "latest", "linuxAmd64DaprdSha256": QUALIFIED},
            "numeric-version": {"qualifiedDaprRuntime": 1.18, "linuxAmd64DaprdSha256": QUALIFIED},
            "extra-key": {"qualifiedDaprRuntime": "1.18.7", "linuxAmd64DaprdSha256": QUALIFIED, "daprCli": "1.18.2"},
            "missing-hash": {"qualifiedDaprRuntime": "1.18.7"},
            "uppercase-hash": {"qualifiedDaprRuntime": "1.18.7", "linuxAmd64DaprdSha256": QUALIFIED.upper()},
            "short-hash": {"qualifiedDaprRuntime": "1.18.7", "linuxAmd64DaprdSha256": QUALIFIED[:63]},
            "not-an-object": ["1.18.7", QUALIFIED],
        }.items():
            with self.subTest(pins=name):
                with self.assertRaisesRegex(ValueError, "exact stable qualified runtime"):
                    works_toolchain.load(self.fixture(pins=pins), environ={})

    def test_invalid_builds_catalog_selection_refuses(self):
        for name, props in {
            "short-runtime": catalog(runtime="1.18"),
            "prerelease-runtime": catalog(runtime="1.18.7-rc.1"),
            "floating-runtime": catalog(runtime="latest"),
            "short-cli": catalog(cli="1.18"),
            "prerelease-cli": catalog(cli="1.18.2-rc.1"),
            "floating-cli": catalog(cli="latest"),
            "repeated-runtime": catalog(extra="<HexalithDaprRuntimeVersion>1.18.7</HexalithDaprRuntimeVersion>"),
            "conditional-cli": catalog().replace("<HexalithDaprCliVersion>", "<HexalithDaprCliVersion Condition=\"'$(X)' == ''\">"),
            "missing-cli": catalog().replace("<HexalithDaprCliVersion>1.18.2</HexalithDaprCliVersion>", ""),
        }.items():
            with self.subTest(catalog=name):
                with self.assertRaisesRegex(ValueError, "Builds catalog"):
                    works_toolchain.load(self.fixture(props=props), environ={})

    def test_runtime_bytes_and_cli_version_must_match_the_owner(self):
        root = self.fixture()
        runtime = root / "daprd"
        for cli, changed, accepted in (
            ("1.18.2", False, True),
            ("1.18.0", False, False),
            ("1.18.2", True, False),
        ):
            with self.subTest(cli=cli, changed=changed):
                runtime.write_bytes(b"changed runtime" if changed else b"qualified runtime")
                runtime.chmod(0o755)
                # Dapr CLI can report a different runtime from the explicit path.
                with patch.object(works_toolchain.subprocess, "check_output",
                                  return_value="CLI version: " + cli + "\nRuntime version: 1.18.3") as execute:
                    if accepted:
                        qualified = works_toolchain.qualify(root, runtime, environ={})
                        self.assertEqual(qualified["runtimeSha256"], QUALIFIED)
                        self.assertEqual(qualified["daprRuntime"], "1.18.7")
                    else:
                        with self.assertRaisesRegex(ValueError, "qualified Works proof"):
                            works_toolchain.qualify(root, runtime, environ={})
                    if changed:
                        execute.assert_not_called()
                    else:
                        execute.assert_called_once_with(["dapr", "--version"], text=True)

    def test_qualified_runtime_must_still_be_executable(self):
        root = self.fixture()
        runtime = root / "daprd"
        runtime.write_bytes(b"qualified runtime")
        runtime.chmod(0o600)
        with patch.object(works_toolchain.subprocess, "check_output") as execute:
            with self.assertRaises(PermissionError):
                works_toolchain.qualify(root, runtime, environ={})
        execute.assert_not_called()

    def test_wrong_hash_executable_is_refused_without_running_any_tool(self):
        root = self.fixture()
        marker = root / 'executed'
        runtime = root / 'daprd'
        runtime.write_text('#!/bin/sh\ntouch "' + str(marker) + '"\nprintf "1.18.7\\n"\n')
        runtime.chmod(0o755)
        with patch.object(works_toolchain.subprocess, 'check_output', wraps=subprocess.check_output) as execute:
            with self.assertRaisesRegex(ValueError, 'qualified Works proof runtime hash'):
                works_toolchain.qualify(root, runtime, environ={})
        execute.assert_not_called()
        self.assertFalse(marker.exists())


if __name__ == "__main__":
    unittest.main()
