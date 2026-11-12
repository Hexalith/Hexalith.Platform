"""Exercise runtime inventory, real file drift and per-process evidence refusals."""

import copy
from pathlib import Path
import tempfile
import unittest

import works_managed_runtime as runtime


class ManagedRuntimeTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="works-managed-runtime-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.lines = []
        for name in runtime.FRAMEWORKS:
            root = self.root / "shared" / name
            directory = root / runtime.VERSION
            directory.mkdir(parents=True)
            for file in ("System.Private.CoreLib.dll", "System.Text.Json.dll", "Microsoft.AspNetCore.Hosting.dll"):
                (directory / file).write_bytes((name + file).encode())
            self.lines.append(f"{name} {runtime.VERSION} [{root}]")
        self.output = "\n".join(self.lines)
        self.expected = runtime.inventory(self.output)
        self.process = {"pid": 100, "coreVersion": runtime.VERSION,
                        "coreLib": self.expected["coreLib"], "json": self.expected["json"]}
        self.record = {"runtimeModules": [{"resource": name} for name in ("works", "eventstore")],
                       "managedRuntime": {"schema": runtime.SCHEMA, "host": self.process,
                           "resources": [dict(self.process, resource=name, pid=pid)
                                         for name, pid in (("works", 101), ("eventstore", 102))]}}

    def test_exact_shared_inventory_and_real_service_processes_pass(self):
        self.assertEqual(runtime.require_start(self.record, self.expected), self.record["managedRuntime"])
        # A recorded test runner may itself be the in-process AppHost. It need not invent a second host PID.
        self.assertEqual(self.record["managedRuntime"]["host"]["pid"], 100)

    def test_missing_duplicate_future_and_malformed_inventory_refuse(self):
        for value in (self.lines[0], self.output + "\n" + self.lines[0], self.output + "\n" + self.lines[0].replace("11.0.0", "12.0.0"),
                      self.output + "\n" + self.lines[0].replace("11.0.0", "11.0.1"), self.output + "\nunreadable"):
            with self.subTest(value=value), self.assertRaises(ValueError):
                runtime.inventory(value)

    def test_process_records_refuse_missing_wrong_runtime_and_untrusted_copy(self):
        for fault in ("missing", "schema", "resource", "duplicate-resource", "duplicate-pid", "host-pid", "core", "json", "pid", "copy"):
            record = copy.deepcopy(self.record)
            value = record["managedRuntime"]
            if fault == "missing":
                del record["managedRuntime"]
            elif fault == "schema":
                value["schema"] = "unknown"
            elif fault == "resource":
                value["resources"].pop()
            elif fault == "duplicate-resource":
                value["resources"].append(value["resources"][0])
            elif fault == "duplicate-pid":
                value["resources"][1]["pid"] = value["resources"][0]["pid"]
            elif fault == "host-pid":
                value["resources"][0]["pid"] = value["host"]["pid"]
            elif fault == "core":
                value["resources"][0]["coreVersion"] = "10.0.15"
            elif fault == "json":
                value["resources"][0]["json"]["sha256"] = "0" * 64
            elif fault == "pid":
                value["resources"][0]["pid"] = True
            else:
                copied = self.root / "copied-json.dll"
                copied.write_bytes(Path(self.expected["json"]["path"]).read_bytes())
                value["resources"][0]["json"] = runtime.file_identity(copied)
            with self.subTest(fault=fault), self.assertRaises(ValueError):
                runtime.require_start(record, self.expected)

    def test_changed_runtime_body_cannot_reuse_an_old_receipt(self):
        Path(self.expected["json"]["path"]).write_bytes(b"changed shared runtime")
        with self.assertRaises(ValueError):
            runtime.require_start(self.record, self.expected)


if __name__ == "__main__":
    unittest.main()
