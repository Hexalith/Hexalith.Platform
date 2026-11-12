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
  <ItemGroup>
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="11.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Http" Version="11.0.0" />
    <PackageVersion Include="System.Text.Json" Version="11.0.0" />
  </ItemGroup>
</Project>
"""

REFERENCE_PINS = """#:package Microsoft.Extensions.Hosting@11.0.0
#:package Microsoft.Extensions.Http@11.0.0
#:package System.Text.Json@11.0.0
"""

APPHOST = """#!/usr/bin/env dotnet
#:sdk Aspire.AppHost.Sdk@{sdk}
#:package CommunityToolkit.Aspire.Hosting.Dapr@{dapr}
#:package Hexalith.EventStore.ServiceDefaults@3.120.0
{eventstore}
""" + REFERENCE_PINS

SPINE = """### AD-9 — Dapr runtime boundary [ADOPTED, AMENDED]
  - *Canonical hosting:* `Hexalith.EventStore.Aspire`, `Hexalith.EventStore.ServiceDefaults` and `CommunityToolkit.Aspire.Hosting.Dapr` form the canonical set.
### AD-10 — Next decision [ADOPTED]
"""


class VerifyPlatformPinsTests(unittest.TestCase):
    def test_file_host_uses_canonical_mapped_dependencies(self):
        for override, expected in [("", 0), (' Version="3.119.0"', 1), (' VersionOverride="3.119.0"', 1)]:
            with self.subTest(override=override), tempfile.TemporaryDirectory() as directory:
                root = pathlib.Path(directory)
                (root / "catalog.props").write_text(CATALOG)
                (root / "spine.md").write_text(SPINE)
                (root / "apphost.cs").write_text("#:sdk Aspire.AppHost.Sdk@13.6.1\n#:package CommunityToolkit.Aspire.Hosting.Dapr@9.9.9\n#:project src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj\n" + REFERENCE_PINS)
                project = root / "src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj"
                project.parent.mkdir(parents=True)
                project.write_text("<Project><ItemGroup>" + "".join(
                    f'<PackageReference Include="{package}"{override}/><ProjectReference Include="$(HexalithEventStoreRoot)/src/{package}/{package}.csproj"/>'
                    for package in ("Hexalith.EventStore.Aspire", "Hexalith.EventStore.ServiceDefaults")) + "</ItemGroup></Project>")
                result = subprocess.run([sys.executable, str(SCRIPT), "--root", str(root), "--catalog", str(root / "catalog.props"), "--spine", str(root / "spine.md")], capture_output=True, text=True)
                self.assertEqual(result.returncode, expected, result.stderr)

    def run_script(self, sdk="13.6.1", dapr="9.9.9", eventstore="#:package Hexalith.EventStore.Aspire@3.120.0", spine=SPINE, helper=None, layout="explicit", relative_root=False):
        with tempfile.TemporaryDirectory() as root:
            root = pathlib.Path(root)
            (root / "catalog.props").write_text(CATALOG)
            if layout != "explicit":
                catalog = root / "references/Hexalith.Builds/Props/Directory.Packages.props"
                catalog.parent.mkdir(parents=True)
                catalog.write_text(CATALOG)
                if layout == "sibling":
                    # Use a child Platform root so its sibling stays in temporary custody.
                    platform = root / "Hexalith.Platform"
                    platform.mkdir()
                    sibling = root / "Hexalith.Builds/Props/Directory.Packages.props"
                    sibling.parent.mkdir(parents=True)
                    sibling.write_text(CATALOG)
                    nested = platform / "references/Hexalith.Builds/Props/Directory.Packages.props"
                    nested.parent.mkdir(parents=True)
                    nested.write_text(CATALOG.replace("3.120.0", "0.0.0"))
                    root = platform
            (root / "apphost.cs").write_text(APPHOST.format(sdk=sdk, dapr=dapr, eventstore=eventstore))
            (root / "spine.md").write_text(spine)
            return subprocess.run(
                [sys.executable, str(SCRIPT), "--root", "." if relative_root else str(root), "--spine", str(root / "spine.md")]
                + (["--catalog", str(root / "catalog.props")] if layout == "explicit" else [])
                + (["--hosting-helper", helper] if helper else []),
                capture_output=True, text=True, cwd=root, timeout=30)

    def test_matching_pins_pass_and_conditional_dapr_override_is_ignored(self):
        self.assertEqual(self.run_script().returncode, 0)

    def test_default_catalog_uses_standalone_dependency(self):
        self.assertEqual(self.run_script(layout="nested").returncode, 0)

    def test_default_catalog_uses_sibling_before_nested_dependency(self):
        self.assertEqual(self.run_script(layout="sibling").returncode, 0)

    def test_relative_platform_root_resolves_sibling_catalog(self):
        self.assertEqual(self.run_script(layout="sibling", relative_root=True).returncode, 0)

    def test_sibling_catalog_still_refuses_stale_eventstore_pin(self):
        result = self.run_script(layout="sibling", eventstore="#:package Hexalith.EventStore.Aspire@3.119.0")
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("3.119.0", result.stderr)
        self.assertIn("3.120.0", result.stderr)

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

    def test_service_defaults_is_a_required_canonical_pin(self):
        with tempfile.TemporaryDirectory() as root:
            root = pathlib.Path(root)
            (root / "catalog.props").write_text(CATALOG)
            (root / "apphost.cs").write_text(APPHOST.format(sdk="13.6.1", dapr="9.9.9", eventstore="#:package Hexalith.EventStore.Aspire@3.120.0").replace("#:package Hexalith.EventStore.ServiceDefaults@3.120.0", ""))
            (root / "spine.md").write_text(SPINE)
            result = subprocess.run([sys.executable, str(SCRIPT), "--root", str(root), "--catalog", str(root / "catalog.props"), "--spine", str(root / "spine.md")], capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Hexalith.EventStore.ServiceDefaults", result.stderr)

    def test_package_mentions_outside_adopted_record_do_not_admit_helpers(self):
        for spine in [SPINE.replace("AD-9", "AD-8"), SPINE.replace("ADOPTED, AMENDED", "PROPOSED"), "### AD-9 — Boundary [ADOPTED]\nNo canonical hosting record.\n" + SPINE.replace("AD-9", "AD-10")]:
            with self.subTest(spine=spine):
                result = self.run_script(spine=spine, helper="Hexalith.EventStore.Aspire")
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("AD-9", result.stderr)

    def test_explicit_vendor_integrations_require_the_exact_catalog_version(self):
        for name in ("Aspire.Hosting.Docker", "Aspire.Hosting.Kubernetes", "Aspire.Hosting.Redis", "Aspire.Hosting.Keycloak"):
            for version, expected in [("13.6.5", 0), ("13.6.4", 1)]:
                with self.subTest(package=name, version=version), tempfile.TemporaryDirectory() as directory:
                    root = pathlib.Path(directory)
                    raw = CATALOG.replace("</Project>", '<ItemGroup><PackageVersion Include="' + name + '" Version="13.6.5" /></ItemGroup></Project>')
                    (root / "catalog.props").write_text(raw)
                    (root / "spine.md").write_text(SPINE)
                    (root / "apphost.cs").write_text(APPHOST.format(sdk="13.6.1", dapr="9.9.9", eventstore="#:package Hexalith.EventStore.Aspire@3.120.0")
                        + "#:package " + name + "@" + version + "\n")
                    result = subprocess.run([sys.executable, str(SCRIPT), "--root", str(root), "--catalog", str(root / "catalog.props"),
                        "--spine", str(root / "spine.md")], capture_output=True, text=True)
                    self.assertEqual(result.returncode, expected, result.stderr)

    def test_model_reference_floors_require_exact_pins_and_catalog_authority(self):
        for name in ("Microsoft.Extensions.Hosting", "Microsoft.Extensions.Http", "System.Text.Json"):
            for mutation in ("missing", "stale", "unknown", "missing-authority", "ambiguous-authority"):
                with self.subTest(package=name, mutation=mutation), tempfile.TemporaryDirectory() as directory:
                    root = pathlib.Path(directory)
                    catalog = CATALOG
                    host = APPHOST.format(sdk="13.6.1", dapr="9.9.9", eventstore="#:package Hexalith.EventStore.Aspire@3.120.0")
                    pin = "#:package " + name + "@11.0.0"
                    authority = '<PackageVersion Include="' + name + '" Version="11.0.0" />'
                    if mutation == "missing":
                        host = host.replace(pin, "")
                    elif mutation == "stale":
                        host = host.replace(pin, pin.replace("11.0.0", "10.0.15"))
                    elif mutation == "unknown":
                        host = host.replace(pin, pin.replace("11.0.0", "11.0.1"))
                    elif mutation == "missing-authority":
                        catalog = catalog.replace(authority, "")
                    else:
                        catalog = catalog.replace(authority, authority + authority)
                    (root / "catalog.props").write_text(catalog)
                    (root / "apphost.cs").write_text(host)
                    (root / "spine.md").write_text(SPINE)
                    result = subprocess.run([sys.executable, str(SCRIPT), "--root", str(root), "--catalog", str(root / "catalog.props"),
                        "--spine", str(root / "spine.md")], capture_output=True, text=True)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertIn(name, result.stderr)

    def test_vendor_directive_with_no_catalog_authority_refuses(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            (root / "catalog.props").write_text(CATALOG)
            (root / "spine.md").write_text(SPINE)
            (root / "apphost.cs").write_text(APPHOST.format(sdk="13.6.1", dapr="9.9.9", eventstore="#:package Hexalith.EventStore.Aspire@3.120.0")
                + "#:package Aspire.Hosting.Redis@13.6.5\n")
            result = subprocess.run([sys.executable, str(SCRIPT), "--root", str(root), "--catalog", str(root / "catalog.props"),
                "--spine", str(root / "spine.md")], capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("one exact catalog entry", result.stderr)

    def test_only_recorded_helpers_are_admitted(self):
        self.assertEqual(self.run_script(helper="Hexalith.EventStore.ServiceDefaults").returncode, 0)
        self.assertNotEqual(self.run_script(helper="Unrecorded.Hosting").returncode, 0)


if __name__ == "__main__":
    unittest.main()
