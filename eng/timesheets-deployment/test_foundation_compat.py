"""Consumer-side check: the Timesheets environment chart stays compatible with the
module-agnostic staging foundation (eng/staging-foundation). The dependency runs one way,
consumer to foundation; see eng/staging-foundation/DEPENDENCY-DIRECTION.md."""
import unittest
from pathlib import Path

CHART = Path(__file__).parent / "environment-chart/templates/prerequisites.yaml"


class FoundationCompatibilityTests(unittest.TestCase):
    def test_boundary_policy_stays_additive_to_default_deny(self):
        text = CHART.read_text()
        self.assertIn('.Release.Namespace "hexalith-staging"', text)
        self.assertIn("name: timesheets-boundary", text)
        self.assertIn("policyTypes: [Ingress, Egress]", text)
        self.assertIn("matchLabels:\n      app.kubernetes.io/name: timesheets", text)


if __name__ == "__main__":
    unittest.main()
