"""Require actual execution of every discovered publisher control, including no skips."""
import importlib.util
import io
from pathlib import Path
from types import SimpleNamespace
import unittest

spec = importlib.util.spec_from_file_location("publisher_controls", Path(__file__).with_name("run-controls.py"))
controls = importlib.util.module_from_spec(spec)
spec.loader.exec_module(controls)


class ExecutionInventoryTests(unittest.TestCase):
    def suite(self, *, skip=False):
        class Control(unittest.TestCase):
            @unittest.skipIf(skip, "required control deliberately skipped by the refusal fixture")
            def test_required(self):
                pass
        return unittest.TestSuite([Control("test_required")])

    def test_successful_exact_inventory_passes(self):
        self.assertEqual(0, controls.run(self.suite(), {__name__: 1}, stream=io.StringIO()))

    def test_skipped_discovered_control_is_refused(self):
        self.assertEqual(1, controls.run(self.suite(skip=True), {__name__: 1}, stream=io.StringIO()))

    def test_missing_discovered_case_is_refused(self):
        self.assertEqual(1, controls.run(self.suite(), {__name__: 2}, stream=io.StringIO()))

    def test_incomplete_execution_is_refused_even_without_failure(self):
        class IncompleteRunner:
            def __init__(self, **kwargs):
                pass
            def run(self, suite):
                return SimpleNamespace(testsRun=0, skipped=[], wasSuccessful=lambda: True)
        self.assertEqual(1, controls.run(self.suite(), {__name__: 1}, stream=io.StringIO(), runner_factory=IncompleteRunner))
