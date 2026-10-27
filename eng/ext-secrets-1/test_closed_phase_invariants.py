"""Fixture/source semantics only; no installed host or native physical proof is supplied."""
import json
from pathlib import Path
import unittest

from closed_phase_invariants import REQUIRED, measure, missing_inputs, verify
from joint_live_phase_runner import Blocked


class ClosedPhaseInvariantTests(unittest.TestCase):
    def test_every_missing_collector_identifies_its_actual_public_api_limit_and_available_source(self):
        inventory = json.loads(Path(__file__).with_name('native-phase-invariant-inventory.json').read_text())
        root = Path(__file__).resolve().parents[2]
        self.assertEqual(REQUIRED, {row['caseId']: {item['name'] for item in row['invariants']} for row in inventory['cases']})
        for case in inventory['cases']:
            for item in case['invariants']:
                for source in item['availableMeasurementSources']:
                    self.assertTrue((root / source).is_file(), source)
                if item['missingInput']:
                    self.assertEqual('ExternalProtocolDependentSource', item['sourceDependencyClassification'])
                    self.assertTrue(item['absentPublicProtocol'])
                    self.assertFalse(item['missingInput'].startswith('Source.'))
            self.assertTrue(missing_inputs(case['caseId']))
            with self.assertRaises(Blocked):
                verify(case['caseId'], {'records': [{'sourceRevision': 1}]})

    def test_closed_canary_measurement_uses_actual_identity_and_original_comparison_fields(self):
        target = {'EngineId': 'actual-fixture-engine', 'EngineVersion': 'a' * 40}
        original = {'Reference': {'Target': target}, 'SourceRevision': 1,
                    'EngineName': target['EngineId'], 'EngineRevision': target['EngineVersion'],
                    'OriginalPlaintextByteCount': 31, 'UnsealEqual': True}
        records = [{'payload': {'guard': {}, 'canaryOriginals': [original]}}]
        self.assertTrue(measure('ActualProductionEngine', records))
        self.assertTrue(measure('UnsealEquality', records))
        original['UnsealEqual'] = False
        self.assertFalse(measure('UnsealEquality', records))
        original['EngineRevision'] = 'b' * 40
        self.assertFalse(measure('ActualProductionEngine', records))
        self.assertFalse(any('OriginalCanaryComparison' in gap or 'EngineIdentity' in gap for gap in missing_inputs('host:H3')))


if __name__ == '__main__':
    unittest.main()
