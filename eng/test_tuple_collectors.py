"""Current proof cannot be substituted by a retained signed4.47/NET10 collector."""
import copy
import json
from pathlib import Path
import unittest

from legacy_tuple_source import git
from prepare_tuple_sources import FROZEN
from run_tuple_ci import (CURRENT_COLLECTOR_SELECTORS, collector_for, require_collector_execution,
                          require_collector_framework, require_retained_collector_binding, validate_profile)

ROOT = Path(__file__).resolve().parents[1]


class TupleCollectorTests(unittest.TestCase):
    def test_all_478_selectors_keep_their_counts_and_explicit_contexts(self):
        profile = json.loads((ROOT / 'eng/tuple-ci-sdk42-current-suites.json').read_bytes())
        self.assertEqual(478, validate_profile(profile))
        totals = {'current': 0, 'retained': 0}
        actual = set()
        for group in profile['groups']:
            role = collector_for(group, 'current42')
            totals[role] += group['requiredCount']
            if role == 'current': actual.update(group['selectors'])
        self.assertEqual({'current': 28, 'retained': 450}, totals)
        self.assertEqual(CURRENT_COLLECTOR_SELECTORS, actual)
        for filename, suite, role, expected in (
                ('tuple-ci-sdk42-base-suites.json', 'base42', 'retained', 97),
                ('tuple-ci-extension-suites.json', 'extensions', 'current', 9)):
            selected = json.loads((ROOT / 'eng' / filename).read_bytes())
            self.assertEqual(expected, validate_profile(selected))
            self.assertEqual({role}, {collector_for(group, suite) for group in selected['groups']})

    def test_current_production_and_artifact_proof_cannot_move_to_old_collector(self):
        for selector in CURRENT_COLLECTOR_SELECTORS:
            group = {'selectors': [selector], 'collector': 'current'}
            self.assertEqual('current', collector_for(group, 'current42'))
            for wrong in (None, 'retained', 'fallback', ''):
                with self.subTest(selector=selector, collector=wrong), self.assertRaises(ValueError):
                    collector_for({**group, 'collector': wrong}, 'current42')
        old = {'selectors': ['Hexalith.Platform.Composition.Tests.PlatformCurrentOwnerPackageTests'],
               'collector': 'retained'}
        self.assertEqual('retained', collector_for(old, 'current42'))
        for changed in ({**old, 'collector': 'current'},
                        {**old, 'selectors': [*old['selectors'], next(iter(CURRENT_COLLECTOR_SELECTORS))]}):
            with self.assertRaises(ValueError): collector_for(changed, 'current42')
        with self.assertRaises(ValueError): collector_for({**old, 'collector': 'current'}, 'base42')
        with self.assertRaises(ValueError): collector_for(old, 'extensions')

    def test_real_original_447_binding_is_required_without_a_new_metadata_exception(self):
        anchor = FROZEN['package-reader']['anchor']
        raw = git(ROOT, 'show', anchor + ':eng/builds-tuple.json')
        original = json.loads(raw)
        require_retained_collector_binding(original, raw)
        for field, value in (('version', '5.0.0'), ('version', '4.48.3'), ('sourceRevision', 'f' * 40)):
            changed = copy.deepcopy(original)
            changed['publication'][field] = value
            with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                require_retained_collector_binding(changed, raw)
        for changed in (raw + b'\n', json.dumps(original).encode()):
            with self.assertRaises(ValueError): require_retained_collector_binding(original, changed)

    def test_framework_and_actual_runtime_must_both_match_the_collector(self):
        for framework, major in (('net10.0', '10'), ('net11.0', '11')):
            require_collector_framework({'project': {'frameworks': {framework: {}}}}, framework)
            exact = {'target-framework': '.NETCoreApp,Version=v' + major + '.0',
                     'environment': '64-bit (x64) .NET ' + major + '.0.15 [collection-per-class]'}
            require_collector_execution(exact, framework)
            other = 'net11.0' if framework == 'net10.0' else 'net10.0'
            for targets in ({}, {other: {}}, {framework: {}, other: {}}):
                with self.subTest(framework=framework, targets=targets), self.assertRaises(ValueError):
                    require_collector_framework({'project': {'frameworks': targets}}, framework)
            for changed in ({**exact, 'target-framework': '.NETCoreApp,Version=v12.0'},
                            {**exact, 'environment': '64-bit .NET 12.0.0'},
                            {**exact, 'environment': ''}, {**exact, 'target-framework': ''}):
                with self.subTest(framework=framework, changed=changed), self.assertRaises(ValueError):
                    require_collector_execution(changed, framework)


if __name__ == '__main__':
    unittest.main()
