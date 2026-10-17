import unittest

from observe_provider_metadata import classify


class ProviderObservationTests(unittest.TestCase):
    def test_native_empty_404_requires_list_capability(self):
        result = classify(404, {'errors': []}, {'ext-secrets-1-isolated/metadata': ['list']})
        self.assertEqual('Empty', result['status'])
        self.assertEqual([], result['metadataRootNames'])

    def test_denied_and_failed_reads_remain_unknown(self):
        for status, document, capabilities in [(403, {'errors': ['permission denied']}, {}),
                (500, {'errors': []}, {}), (404, {'errors': []}, {}),
                (404, {'errors': ['unknown mount']}, {'ext-secrets-1-isolated/metadata': ['list']})]:
            result = classify(status, document, capabilities)
            self.assertIsNone(result['metadataRootNames'])
            self.assertIsNone(result['opaqueNumericMappings'])

    def test_root_names_never_substitute_for_version_mappings(self):
        result = classify(200, {'data': {'keys': ['real-key']}}, {'ext-secrets-1-isolated/metadata': ['list']})
        self.assertEqual(['real-key'], result['metadataRootNames'])
        self.assertIsNone(result['opaqueNumericMappings'])


if __name__ == '__main__':
    unittest.main()
