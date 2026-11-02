"""Offline drift controls for the retained CI reader preparation artifacts."""

import importlib.util
import json
import tempfile
import unittest
from contextlib import redirect_stdout
from io import StringIO
from pathlib import Path
from unittest.mock import patch


SCRIPT = Path(__file__).resolve().parents[1] / "prepare.py"
SPEC = importlib.util.spec_from_file_location("ci_reader_prepare", SCRIPT)
assert SPEC and SPEC.loader
PREPARE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PREPARE)
ARTIFACTS = (
    "configuration.current.json",
    "configuration.ci-reader.json",
    "configmap.patch.json",
    "reload.patch.json",
    "plan.json",
)


class OfflinePreparationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        destination = Path(self.temporary.name)
        for name in ARTIFACTS:
            (destination / name).write_bytes((PREPARE.OUTPUT / name).read_bytes())
        self.output_patch = patch.object(PREPARE, "OUTPUT", destination)
        self.output_patch.start()
        self.addCleanup(self.output_patch.stop)

    def test_offline_check_accepts_exact_artifacts_without_kubernetes(self):
        with patch.object(PREPARE, "read_kubernetes", side_effect=AssertionError("Kubernetes read")):
            with redirect_stdout(StringIO()):
                PREPARE.offline_check()

    def test_offline_check_refuses_changed_middle_volume_binding(self):
        path = PREPARE.OUTPUT / "reload.patch.json"
        operations = json.loads(path.read_text())
        operations[5]["value"][0]["mountPath"] = "/unexpected"
        path.write_text(json.dumps(operations, indent=2) + "\n")

        with patch.object(PREPARE, "read_kubernetes", side_effect=AssertionError("Kubernetes read")):
            with self.assertRaisesRegex(ValueError, "Retained reload patch drift"):
                PREPARE.offline_check()

    def test_offline_check_refuses_changed_known_read_probe(self):
        path = PREPARE.OUTPUT / "plan.json"
        plan = json.loads(path.read_text())
        plan["knownReadProbe"]["repository"] = "unexpected"
        path.write_text(json.dumps(plan, indent=2) + "\n")

        with patch.object(PREPARE, "read_kubernetes", side_effect=AssertionError("Kubernetes read")):
            with self.assertRaisesRegex(ValueError, "Retained plan drift"):
                PREPARE.offline_check()


if __name__ == "__main__":
    unittest.main()
