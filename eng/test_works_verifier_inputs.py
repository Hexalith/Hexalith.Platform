"""Execute final verifier closure against real input inventories and independent drift controls."""

import tempfile
import unittest
from contextlib import contextmanager
from pathlib import Path
from unittest.mock import patch

import verify_works_host as gate
from verify_works_producer import BUILD_FILES, REALM_MANIFEST, runtime_source_snapshot


class FinalInputClosureTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory(prefix="works-final-inputs-")
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        self.works = self.root / "works"
        self.platform = self.root / "platform"
        self.sdk = self.root / "sdk"
        self.retained = self.root / "retained"
        self.revisions = {}
        self.ephemeral = (32768, 60999)
        self.reserved = ((20000, 20999),)

        for owner in (self.works, self.sdk):
            self.write(owner / "src/Domain/Domain.cs", "// source")
            self.write(owner / "src/Domain/Domain.csproj", "<Project />")
            for name in BUILD_FILES:
                self.write(owner / name, "<Project />")
        self.write(self.sdk / REALM_MANIFEST, '{"schema":"hexalith.module-manifest.v2"}')
        self.catalog = self.sdk / "references/Hexalith.Builds"
        self.write(self.catalog / "Props/Directory.Packages.props", "<Project />")
        self.write(self.catalog / "Props/Hexalith.Build.props", "<Project />")

        self.write(self.platform / "apphost.cs", "// host")
        self.write(self.platform / "src/Adapter/Adapter.cs", "// adapter")
        self.write(self.platform / "src/Adapter/Adapter.csproj", "<Project />")
        self.verifier = self.platform / "eng/verify_works_host.py"
        self.write(self.verifier, "# verifier")
        self.helper = self.platform / "eng/helper.py"
        self.write(self.helper, "# helper")

        self.fixture = self.works / "tests/Hexalith.Works.IntegrationTests/Fixture.cs"
        self.write(self.fixture, "// fixture")
        self.project = self.fixture.parent / "Hexalith.Works.IntegrationTests.csproj"
        self.write(
            self.project,
            '<Project><ItemGroup><Compile Include="$(HexalithPartiesRoot)/src/Authority.cs" /></ItemGroup></Project>',
        )
        self.resource = self.fixture.parent / "SchemaEvolution/Golden/Event.v1.json"
        self.write(self.resource, '{"event":"historical"}')
        self.testing = self.works / "tests/Hexalith.Works.Testing/Builder.cs"
        self.write(self.testing, "// test builder")
        self.write(self.testing.parent / "Hexalith.Works.Testing.csproj", "<Project />")
        self.runner = self.works / "xunit.runner.json"
        self.write(self.runner, '{"failSkips":true}')

        self.identity = self.works / "references/Hexalith.Platform/src/Hexalith.Platform.Identity/Identity.cs"
        self.write(self.identity, "// identity")
        self.write(self.identity.parent / "Hexalith.Platform.Identity.csproj", "<Project />")
        self.party = self.works / "references/Hexalith.Parties/src/Authority.cs"
        self.write(self.party, "// authority")
        self.composition = self.platform / "src/Hexalith.Platform.Composition/Composition.cs"
        self.write(self.composition, "// composition")
        self.write(
            self.composition.parent / "Hexalith.Platform.Composition.csproj",
            '<Project><ItemGroup><Compile Include="../../DaprSelfHostedMtls.cs" /></ItemGroup></Project>',
        )
        self.linked = self.platform / "DaprSelfHostedMtls.cs"
        self.write(self.linked, "// linked input")

        self.write(self.retained / "Program.cs", "// retained source")
        (self.retained / "references").mkdir()
        self.canonical_works = self.works / "config/admission.json"
        self.canonical_platform = self.platform / "config/admission.json"
        self.write(self.canonical_works, "{}")
        self.write(self.canonical_platform, "{}")
        self.consumer = self.root / "consumer"
        for suffix in ("", ".dll", ".deps.json", ".runtimeconfig.json"):
            self.write(self.consumer / ("Hexalith.Works.IntegrationTests" + suffix), "qualified consumer")
        self.output_resource = self.consumer / "SchemaEvolution/Golden/Event.v1.json"
        self.output_runner = self.consumer / "xunit.runner.json"
        self.output_testing = self.consumer / "Hexalith.Works.Testing.dll"
        self.write(self.output_resource, self.resource.read_text())
        self.write(self.output_runner, self.runner.read_text())
        self.write(self.output_testing, "qualified test helpers")
        self.binaries = {name: self.root / "binaries" / (name + ".dll") for name in ("runtime", "adapter")}
        self.runtime_roots = [self.root / "runtime" / name for name in ("works", "eventstore", "retained-host")]
        self.binaries["runtime"] = self.runtime_roots[0] / "runtime.dll"
        for directory in self.runtime_roots:
            self.write(directory / "resources/runtime.json", "runtime settings")
        self.binaries["consumer"] = self.consumer / "Hexalith.Works.IntegrationTests.dll"
        for name, path in self.binaries.items():
            self.write(path, name)

        self.start_patch("verify_works_host.__file__", str(self.verifier))
        self.start_patch("verify_works_host.subprocess.check_output", side_effect=self.git)
        self.ephemeral_probe = self.start_patch(
            "verify_works_host.host_ephemeral_port_range", return_value=self.ephemeral
        )
        self.reserved_probe = self.start_patch(
            "verify_works_host.host_reserved_port_ranges", return_value=self.reserved
        )
        self.summary = {
            "sourceProducers": {"runtimeSdk": runtime_source_snapshot(self.sdk)},
            "worksConsumerSource": gate.source_snapshot(self.works),
            "platformConsumerSources": gate.platform_consumer_sources(self.platform),
            "runtimeDirectories": [str(path) for path in self.runtime_roots],
            "runtimeArtifacts": gate.runtime_artifacts(self.runtime_roots),
            "consumer": {"root": str(self.consumer), "artifacts": gate.consumer_artifacts(self.consumer)},
            "platformAdapterAssemblies": {str(self.binaries["adapter"]): gate.sha256(self.binaries["adapter"])},
            "verifierSha256": gate.sha256(self.verifier),
            "verifierHelpers": {"helper.py": gate.sha256(self.helper)},
            "fixtureSources": gate.fixture_sources(self.works),
            "retainedHostSource": {
                "root": str(self.retained),
                "sourceFiles": {"Program.cs": gate.sha256(self.retained / "Program.cs")},
                "dependencyLinks": {},
            },
            "linkedFixtureSources": gate.linked_fixture_snapshot(self.works, self.platform),
            "canonicalSources": {
                name: gate.canonical_source_snapshot(owner)
                for name, owner in (("Works", self.works), ("Platform", self.platform))
            },
        }

    def start_patch(self, *args, **kwargs):
        patcher = patch(*args, **kwargs)
        result = patcher.start()
        self.addCleanup(patcher.stop)
        return result

    @staticmethod
    def write(path, content):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content)

    def git(self, command, **kwargs):
        """Supply only Git metadata; every source inventory and digest still reads the temporary filesystem."""
        self.assertEqual(command[:2], ["git", "-C"])
        owner = Path(command[2])
        arguments = command[3:]
        if arguments == ["rev-parse", "--show-toplevel"]:
            return str(owner) + "\n"
        if arguments == ["rev-parse", "HEAD"]:
            return self.revisions.get(str(owner), "a" * 40) + "\n"
        self.assertEqual(arguments, ["ls-files", "-z", "--cached", "--others", "--exclude-standard"])
        return (
            "\0".join(
                path.relative_to(owner).as_posix() for path in sorted(owner.rglob("*")) if path.is_file()
            ).encode()
            + b"\0"
        )

    def check(self, manifest=None):
        gate.require_unchanged_inputs(self.summary, self.works, self.platform, manifest, self.ephemeral, self.reserved)

    @contextmanager
    def changed(self, path, kind="changed"):
        original = path.read_bytes() if path.exists() else None
        if kind == "removed":
            path.unlink()
        else:
            self.write(path, "changed input")
        try:
            yield
        finally:
            if original is None:
                path.unlink()
            else:
                path.write_bytes(original)

    def test_unchanged_complete_input_graph_passes(self):
        self.check()

    def test_file_host_sdk_runtime_bytes_are_rechecked(self):
        host = self.root / 'host-output/apphost.dll'
        sdk = host.with_name('Hexalith.EventStore.Aspire.dll')
        self.write(host, 'host fixture')
        self.write(sdk, 'qualified SDK fixture')
        self.summary['host'] = {'assembly': str(host)}
        self.summary['platformHostSdk'] = {
            'Hexalith.EventStore.Aspire': {'consumerAssemblySha256': gate.sha256(sdk)}}
        self.check()
        with self.changed(sdk), self.assertRaisesRegex(ValueError, 'file-host SDK assembly changed'):
            self.check()

    def test_runtime_source_bytes_and_membership_are_rechecked(self):
        for kind in ("changed", "added", "removed"):
            path = self.sdk / ("src/Domain/Added.cs" if kind == "added" else "src/Domain/Domain.cs")
            with (
                self.subTest(kind=kind),
                self.changed(path, kind),
                self.assertRaisesRegex(ValueError, "SDK source changed during"),
            ):
                self.check()
        self.check()

    def test_runtime_builds_props_bytes_and_membership_are_rechecked(self):
        for kind in ("changed", "added", "removed"):
            path = self.catalog / ("Props/Added.props" if kind == "added" else "Props/Hexalith.Build.props")
            with (
                self.subTest(kind=kind),
                self.changed(path, kind),
                self.assertRaisesRegex(ValueError, "SDK Builds inputs changed during"),
            ):
                self.check()
        self.check()

    def test_checkout_sdk_revision_and_builds_inputs_are_rechecked(self):
        self.summary["sourceProducers"]["runtimeSdk"] = gate.source_snapshot(self.sdk, producer=True)
        self.check()
        self.revisions[str(self.sdk)] = "b" * 40
        with self.assertRaisesRegex(ValueError, "qualified SDK source graph changed"):
            self.check()
        self.revisions.pop(str(self.sdk))
        for path in (self.catalog / "Props/Hexalith.Build.props", self.sdk / "global.json", self.sdk / REALM_MANIFEST):
            with (
                self.subTest(path=path),
                self.changed(path),
                self.assertRaisesRegex(ValueError, "qualified SDK source graph changed"),
            ):
                self.check()

    def test_works_source_bytes_membership_and_revision_are_rechecked(self):
        for kind in ("changed", "added", "removed"):
            path = self.works / ("src/Domain/Added.cs" if kind == "added" else "src/Domain/Domain.cs")
            with (
                self.subTest(kind=kind),
                self.changed(path, kind),
                self.assertRaisesRegex(ValueError, "Works consumer source graph changed"),
            ):
                self.check()
        self.revisions[str(self.works)] = "b" * 40
        with self.assertRaisesRegex(ValueError, "Works consumer source graph changed"):
            self.check()

    def test_platform_source_bytes_and_complete_membership_are_rechecked(self):
        for suffix in ("cs", "csproj"):
            for kind in ("changed", "added", "removed"):
                path = self.platform / (
                    "src/Adapter/Added." + suffix if kind == "added" else "src/Adapter/Adapter." + suffix
                )
                with (
                    self.subTest(suffix=suffix, kind=kind),
                    self.changed(path, kind),
                    self.assertRaisesRegex(ValueError, "Platform consumer source membership or bytes changed"),
                ):
                    self.check()
        self.check()

    def test_every_artifact_family_is_rechecked(self):
        for name, category in (
            ("runtime", "runtime artifact"),
            ("consumer", "Works consumer artifact"),
            ("adapter", "Platform adapter assembly"),
        ):
            with (
                self.subTest(name=name),
                self.changed(self.binaries[name]),
                self.assertRaisesRegex(ValueError, category + ".*changed during"),
            ):
                self.check()

    def test_final_runtime_membership_is_rechecked_for_every_output(self):
        for directory in self.runtime_roots:
            for kind, filename in (("added", "resources/new.json"), ("removed", "resources/runtime.json")):
                with (self.subTest(directory=directory, kind=kind), self.changed(directory / filename, kind),
                      self.assertRaisesRegex(ValueError, "runtime artifact membership or bytes changed")):
                    self.check()
        self.check()

    def test_runtime_inventory_refuses_missing_outputs_and_symbolic_links(self):
        with self.assertRaisesRegex(ValueError, "runtime output directory"):
            gate.runtime_artifacts([self.root / "missing"])
        link = self.runtime_roots[0] / "linked"
        for target in (self.fixture, self.runtime_roots[1]):
            link.symlink_to(target)
            try:
                with self.assertRaisesRegex(ValueError, "symbolic link"):
                    self.check()
            finally:
                link.unlink()

    def test_copied_resources_runner_and_test_helper_binary_are_rechecked_without_source_changes(self):
        source_inventory = gate.fixture_sources(self.works)
        for path in (self.output_resource, self.output_runner, self.output_testing):
            with self.subTest(path=path.name), self.changed(path):
                self.assertEqual(source_inventory, gate.fixture_sources(self.works))
                with self.assertRaisesRegex(ValueError, "Works consumer artifact"):
                    self.check()
        self.check()

    def test_consumer_output_additions_and_removals_are_rechecked(self):
        for kind in ("added", "removed"):
            path = self.consumer / "Added.json" if kind == "added" else self.output_resource
            with (
                self.subTest(kind=kind),
                self.changed(path, kind),
                self.assertRaisesRegex(ValueError, "Works consumer artifact"),
            ):
                self.check()

    def test_verifier_and_helpers_are_rechecked(self):
        for path, category in ((self.verifier, "The verifier"), (self.helper, "A verifier helper")):
            with (
                self.subTest(path=path.name),
                self.changed(path),
                self.assertRaisesRegex(ValueError, category + " changed during"),
            ):
                self.check()

    def test_runner_resources_projects_and_test_helpers_are_rechecked(self):
        for path in (self.fixture, self.runner, self.resource, self.project, self.testing):
            with (
                self.subTest(path=path.name),
                self.changed(path),
                self.assertRaisesRegex(ValueError, "Qualified fixtures changed since the pre-command baseline"),
            ):
                self.check()

    def test_retained_topology_sources_are_rechecked(self):
        with (
            self.changed(self.retained / "Program.cs"),
            self.assertRaisesRegex(ValueError, "Historical topology source changed"),
        ):
            self.check()

    def test_linked_identity_parties_and_composition_inputs_are_rechecked(self):
        for path in (self.identity, self.party, self.linked):
            with (
                self.subTest(path=path.name),
                self.changed(path),
                self.assertRaisesRegex(
                    ValueError, "Explicit linked test-only Identity or Parties fixture sources changed"
                ),
            ):
                self.check()

    def test_linked_dependency_revision_is_rechecked(self):
        self.revisions[str(self.party.parents[1])] = "b" * 40
        with self.assertRaisesRegex(
            ValueError, "Explicit linked test-only Identity or Parties fixture sources changed"
        ):
            self.check()

    def test_canonical_source_bytes_and_membership_are_rechecked_for_both_owners(self):
        for name, path in (("Works", self.canonical_works), ("Platform", self.canonical_platform)):
            for kind in ("changed", "added", "removed"):
                selected = path.with_name("added.json") if kind == "added" else path
                with (
                    self.subTest(name=name, kind=kind),
                    self.changed(selected, kind),
                    self.assertRaisesRegex(ValueError, "Canonical " + name + " revision or ordinary"),
                ):
                    self.check()

    def test_canonical_revisions_are_rechecked_for_both_owners(self):
        for name, owner in (("Works", self.works), ("Platform", self.platform)):
            with self.subTest(name=name):
                self.revisions[str(owner)] = "b" * 40
                if name == "Works":
                    # Isolate the separate canonical revision binding from the consumer source binding above.
                    self.summary["worksConsumerSource"] = gate.source_snapshot(self.works)
                with self.assertRaisesRegex(ValueError, "Canonical " + name + " revision or ordinary"):
                    self.check()
                self.revisions.pop(str(owner))
                self.summary["worksConsumerSource"] = gate.source_snapshot(self.works)

    def test_snapshot_manifest_is_verified_and_its_final_observation_recorded(self):
        manifest = self.root / "snapshot.json"
        self.write(manifest, "{}")
        self.summary["platformSourceSnapshot"] = {"manifestSha256": gate.sha256(manifest)}

        def verify(path, platform):
            self.assertEqual((path, platform), (manifest, self.platform))
            return {"manifestSha256": gate.sha256(path), "originCommit": "a" * 40}

        with patch.object(gate.works_host_snapshot, "verify", side_effect=verify) as verifier:
            self.check(manifest)
            self.assertEqual(self.summary["platformSourceSnapshotFinal"], verify(manifest, self.platform))
            verifier.assert_called_once_with(manifest, self.platform)
            with (
                self.changed(manifest),
                self.assertRaisesRegex(ValueError, "Platform snapshot manifest changed during"),
            ):
                self.check(manifest)

    def test_snapshot_verification_failure_is_preserved(self):
        manifest = self.root / "snapshot.json"
        self.write(manifest, "{}")
        self.summary["platformSourceSnapshot"] = {"manifestSha256": gate.sha256(manifest)}
        with (
            patch.object(
                gate.works_host_snapshot, "verify", side_effect=ValueError("Snapshot current canonical inputs changed")
            ),
            self.assertRaisesRegex(ValueError, "Snapshot current canonical inputs changed"),
        ):
            self.check(manifest)

    def test_snapshot_final_check_refuses_a_changed_build_root_outside_the_export_links(self):
        manifest = self.root / "snapshot.json"
        self.write(manifest, "{}")
        binding = {"manifestSha256": gate.sha256(manifest)}
        self.summary["platformSourceSnapshot"] = binding
        self.write(self.platform / ".gitmodules", '[submodule "Builds"]\n path = references/Hexalith.Builds\n')
        builds = self.root / "alternate-builds"
        catalog = builds / "Props/Directory.Packages.props"
        self.write(catalog, "selected")
        with (patch.dict("os.environ", {"HexalithBuildsRoot": str(builds)}),
              patch.object(gate.works_host_snapshot, "verify", return_value=binding)):
            self.summary["canonicalSources"]["Platform"] = gate.canonical_source_snapshot(self.platform)
            self.check(manifest)
            with (self.changed(catalog), self.assertRaisesRegex(ValueError, "Platform build dependency selection")):
                self.check(manifest)

    def test_host_port_ranges_are_rechecked(self):
        self.ephemeral_probe.return_value = (32769, 60999)
        with self.assertRaisesRegex(ValueError, "ephemeral application-port range changed during"):
            self.check()
        self.ephemeral_probe.return_value = self.ephemeral
        self.reserved_probe.return_value = ((21000, 21999),)
        with self.assertRaisesRegex(ValueError, "reserved-port ranges changed during"):
            self.check()


if __name__ == "__main__":
    unittest.main()
