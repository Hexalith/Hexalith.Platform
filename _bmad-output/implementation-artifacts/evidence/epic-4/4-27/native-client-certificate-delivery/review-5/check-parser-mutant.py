#!/usr/bin/env python3
"""Prove real transcript controls reject a hardcoded OK/zero-skip parser.

Only this imported checker's in-memory function is mutated; all working sources,
receipts, historical packets and logs remain unchanged. The receipt self-test
uses isolated temporary copies and real synthetic unittest adverse transcripts.
"""
import importlib.util
import json
from pathlib import Path
import re
import sys

path = Path(__file__).absolute().with_name('verify-receipt.py')
spec = importlib.util.spec_from_file_location('review5_receipt_parser_mutant', path)
checker = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = checker
spec.loader.exec_module(checker)
receipt = checker.verify(checker.ROOT, checker.PACKET)


def mutant(raw):
    count = re.search(rb'^Ran ([0-9]+) tests? in [0-9.]+s$', raw, re.MULTILINE)
    assert count is not None
    return {'tests': int(count[1]), 'skips': 0, 'outcome': 'OK'}


checker.unittest_summary = mutant
checker.verify(checker.ROOT, checker.PACKET)  # The unchanged positive run passes.
try:
    checker.self_test(receipt)
except AssertionError as exc:
    assert str(exc) == 'hash-consistent non-passing transcript escaped:FAILED (failures=1)', str(exc)
    print(json.dumps({'hardcodedOKZeroSkipParserRejectedBySelfTest': True,
        'boundTestCountPreserved': True, 'rejection': str(exc), 'workingFilesMutated': False}, sort_keys=True))
else:
    raise AssertionError('hardcoded parser survived actual adverse transcript control')
