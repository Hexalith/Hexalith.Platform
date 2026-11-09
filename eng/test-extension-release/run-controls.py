#!/usr/bin/env python3
"""Run the publisher's exact resource-free control inventory before qualification."""
from pathlib import Path
import sys
import unittest


def cases(suite):
    for item in suite:
        if isinstance(item, unittest.TestSuite):
            yield from cases(item)
        else:
            yield item


def run(suite, expected, *, stream=sys.stderr, runner_factory=unittest.TextTestRunner):
    inventory = {}
    for case in cases(suite):
        module = case.__class__.__module__
        inventory[module] = inventory.get(module, 0) + 1
    if inventory != expected:
        print(f"Publisher control inventory differs: expected {expected}, actual {inventory}", file=stream)
        return 1
    result = runner_factory(verbosity=2, stream=stream).run(suite)
    if result.testsRun != sum(expected.values()) or result.skipped or not result.wasSuccessful():
        print("Publisher controls require the exact executed inventory with no skips or failures.", file=stream)
        return 1
    return 0


def main():
    suite = unittest.defaultTestLoader.discover(str(Path(__file__).resolve().parent), pattern="test_*.py")
    expected = {"test_host_entry": 17, "test_package": 9, "test_controls": 4}
    print("Publisher controls: 30 (17 readiness, 9 archive identity, 4 execution-inventory controls).", flush=True)
    return run(suite, expected)


if __name__ == "__main__":
    raise SystemExit(main())
