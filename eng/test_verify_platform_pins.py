import pathlib
import subprocess
import sys
import tempfile
import unittest

SCRIPT = pathlib.Path(__file__).with_name("verify-platform-pins.py")

CATALOG = """<Project>
  <PropertyGroup>
    <HexalithAspireAppHostSdkVersion>13.6.1</HexalithAspireAppHostSdkVersion>
    <HexalithEventStoreVersion>3.120.0</HexalithEventStoreVersion>
    <HexalithAspireHostingDaprVersion>9.9.9</HexalithAspireHostingDaprVersion>
    <HexalithAspireHostingDaprVersion Condition="'$(MSBuildProjectName)' == 'Other'">1.0.0</HexalithAspireHostingDaprVersion>
  </PropertyGroup>
</Project>
"""

APPHOST = """#!/usr/bin/env dotnet
#:sdk Aspire.AppHost.Sdk@{sdk}
#:package CommunityToolkit.Aspire.Hosting.Dapr@{dapr}
{eventstore}
"""


class VerifyPlatformPinsTests(unittest.TestCase):
    def run_script(self, sdk="13.6.1", dapr="9.9.9", eventstore="#:package Hexalith.EventStore.Aspire@3.120.0"):
        with tempfile.TemporaryDirectory() as root:
            root = pathlib.Path(root)
            (root / "catalog.props").write_text(CATALOG)
            (root / "apphost.cs").write_text(APPHOST.format(sdk=sdk, dapr=dapr, eventstore=eventstore))
            return subprocess.run(
                [sys.executable, str(SCRIPT), "--root", str(root), "--catalog", str(root / "catalog.props")],
                capture_output=True, text=True)

    def test_matching_pins_pass_and_conditional_dapr_override_is_ignored(self):
        self.assertEqual(self.run_script().returncode, 0)

    def test_sdk_drift_names_both_versions(self):
        result = self.run_script(sdk="13.6.0")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("13.6.0", result.stderr)
        self.assertIn("13.6.1", result.stderr)

    def test_dapr_drift_is_refused(self):
        result = self.run_script(dapr="1.0.0")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("CommunityToolkit.Aspire.Hosting.Dapr", result.stderr)

    def test_missing_pin_is_refused(self):
        result = self.run_script(eventstore="")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("<missing>", result.stderr)


if __name__ == "__main__":
    unittest.main()
