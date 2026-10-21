"""The Works gate and CI consume one Platform-owned runtime selection."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import works_toolchain


class WorksToolchainTests(unittest.TestCase):
    def test_ci_outputs_the_owner_pins(self):
        root = Path(__file__).resolve().parents[1]
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "output"
            subprocess.run([sys.executable, str(root / "eng/works_toolchain.py"), "--github-output", str(output)], check=True)
            pins = works_toolchain.load(root)
            self.assertEqual(output.read_text(), "dapr-cli=" + pins["daprCli"] + "\ndapr-runtime=" + pins["daprRuntime"] + "\n")

    def test_actual_runtime_version_and_bytes_must_match_the_owner(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "config").mkdir()
            runtime = root / "daprd"
            runtime.write_bytes(b"qualified runtime")
            pins = {"daprCli": "1.18.2", "daprRuntime": "1.18.7",
                    "linuxAmd64DaprdSha256": hashlib.sha256(runtime.read_bytes()).hexdigest()}
            (root / "config/works-proof-toolchain.json").write_text(json.dumps(pins))
            for version, cli, changed, accepted in (
                ("1.18.7", "1.18.2", False, True),
                ("1.18.3", "1.18.2", False, False),
                ("1.18.7", "1.18.0", False, False),
                ("1.18.7", "1.18.2", True, False),
            ):
                with self.subTest(version=version, cli=cli, changed=changed):
                    runtime.write_bytes(b"changed runtime" if changed else b"qualified runtime")
                    with patch.object(works_toolchain.subprocess, "check_output", side_effect=[version, "CLI version: " + cli + "\nRuntime version: " + version]):
                        if accepted:
                            self.assertEqual(works_toolchain.qualify(root, runtime)["runtimeSha256"], pins["linuxAmd64DaprdSha256"])
                        else:
                            with self.assertRaisesRegex(ValueError, "qualified Works proof"):
                                works_toolchain.qualify(root, runtime)
