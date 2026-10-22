"""Keep the published test-contract schemas identical to independent Builds mirrors."""

from pathlib import Path
import unittest


class TestEnvironmentContractMirrorsTests(unittest.TestCase):
    def test_canonical_contracts_match_builds_mirrors(self):
        root = Path(__file__).resolve().parents[2]
        for name in ("hexalith.test-environment.v1.json", "hexalith.test-result.v1.json"):
            with self.subTest(schema=name):
                self.assertEqual(
                    (root / "docs" / "contracts" / "schemas" / name).read_bytes(),
                    (root / "references" / "Hexalith.Builds" / "schemas" / name).read_bytes(),
                )


if __name__ == "__main__":
    unittest.main()
