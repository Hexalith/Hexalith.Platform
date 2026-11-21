"""All remaining proof executes on the candidate current collector."""
import copy
import json
from pathlib import Path
import unittest
from unittest.mock import patch

from run_tuple_ci import (collector_for, require_collector_execution,
                          require_collector_framework, require_collector_source, validate_profile)

ROOT = Path(__file__).resolve().parents[1]


class TupleCollectorTests(unittest.TestCase):
    def test_every_positive_inventory_executes_only_on_the_candidate(self):
        for filename, suite, expected in (
                ('tuple-ci-sdk42-current-suites.json', 'current42', 380),
                ('tuple-ci-sdk42-base-suites.json', 'base42', 67),
                ('tuple-ci-extension-suites.json', 'extensions', 18)):
            profile = json.loads((ROOT / 'eng' / filename).read_bytes())
            self.assertEqual(expected, validate_profile(profile))
            self.assertEqual({'current'}, {collector_for(group, suite) for group in profile['groups']})
            for group in profile['groups']:
                for wrong in (None, 'retained', 'fallback', ''):
                    with self.subTest(suite=suite, collector=wrong), self.assertRaises(ValueError):
                        collector_for({**group, 'collector': wrong}, suite)

    def test_runner_has_no_alternate_platform_checkout_or_fetch_path(self):
        for retired in ('legacy_tuple_source.py', 'pre_extension_tuple_source.py', 'package_reader_source.py',
                        'test_legacy_tuple_source.py', 'test_pre_extension_tuple_source.py', 'test_package_reader_source.py',
                        'legacy-tuple-source.json', 'pre-extension-tuple-source.json'):
            self.assertFalse((ROOT / 'eng' / retired).exists(), retired)
        self.assertFalse((ROOT / 'eng/tuple-ci-legacy-suites.json').exists())
        self.assertFalse((ROOT / 'eng/tuple-ci-suites.json').exists())
        source = (ROOT / 'eng/run_tuple_ci.py').read_text()
        for retired in ('reader-workspace', 'prepare-historical-reader', 'CURRENT_PLATFORM_SHA',
                        'retained_workspace', 'package_reader_source', '"fetch"', '"checkout"'):
            self.assertNotIn(retired, source)

    def test_framework_and_actual_runtime_must_both_match_the_collector(self):
        with self.assertRaises(ValueError):
            require_collector_framework({'project': {'frameworks': {'net10.0': {}}}}, 'net10.0')
        for framework, major in [('net11.0', '11')]:
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

    def test_a_moved_source_or_dirty_direct_owner_cannot_reuse_the_collector_receipt(self):
        source = 'a' * 40
        with patch('run_tuple_ci.subprocess.check_output', side_effect=[source + '\n', '']) as git:
            require_collector_source(ROOT, source)
            self.assertTrue(all(call.kwargs['cwd'] == ROOT for call in git.call_args_list))
        for revision, status in (('b' * 40, ''), (source, ' M Directory.Build.props\n'),
                                 (source, ' M references/Hexalith.Builds\n'), (source, '?? unexpected.cs\n')):
            with self.subTest(revision=revision, status=status), \
                    patch('run_tuple_ci.subprocess.check_output', side_effect=[revision + '\n', status]), \
                    self.assertRaisesRegex(ValueError, 'source identity or clean recorded owner'):
                require_collector_source(ROOT, source)


if __name__ == '__main__':
    unittest.main()
