"""Owner package lock generation and enforcement controls with synthetic bytes, mocked restore and mocked nuget.org; no network."""
import base64
import contextlib
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import xml.etree.ElementTree as ET

sys.path.insert(0, str(Path(__file__).resolve().parent))

import owner_packages_lock
from builds_tool_lock import content_hash
from owner_packages_lock import (SURFACES, archive_path, generate, load_lock, lock_packages, locked_entry, owner_closure, project_owner_references,
                                 require_current, require_locked, restore_surfaces, run_built_owner_references, surfaces)
from builds_tuple import ROOT

PACKAGES = {"Hexalith.Tenants.Contracts": ("8.0.1", b"synthetic tenants contracts"),
            "Hexalith.EventStore.Contracts": ("5.2.0", b"synthetic eventstore contracts")}
REGISTRATION = "https://api.nuget.org/v3/registration5-gz-semver2/"


def signed(_path):
    return subprocess.CompletedProcess([], 0, "Successfully verified package", "")


def assets(libraries):
    return {"libraries": {key: {"type": kind, "path": key.lower()} for key, kind in libraries.items()}}


class OwnerPackageLockTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.packages = self.root / "packages"
        self.catalog = {}
        for package_id, (version, raw) in PACKAGES.items():
            path = archive_path(self.packages, package_id, version)
            path.parent.mkdir(parents=True)
            path.write_bytes(raw)
            self.catalog[package_id] = {"id": package_id, "version": version, "packageHash": content_hash(raw), "packageHashAlgorithm": "SHA512"}
        self.closure = {package_id: {"version": version, "path": package_id.lower() + "/" + version} for package_id, (version, _) in PACKAGES.items()}

    def get(self, url, bound):
        for package_id, entry in self.catalog.items():
            if url == REGISTRATION + package_id.lower() + "/" + entry["version"] + ".json":
                return json.dumps({"catalogEntry": entry}).encode()
        raise AssertionError("Unexpected URL " + url)

    def test_committed_lock_covers_every_surface_reference_with_one_exact_hash(self):
        lock = load_lock()
        self.assertEqual(json.loads(owner_packages_lock.LOCK.read_bytes())["schemaVersion"], 1)
        locked = {entry["id"]: entry for entry in lock["packages"]}
        selected = surfaces()
        self.assertEqual(set(selected), set(SURFACES) | {"Hexalith.Parties"})
        for surface, references in selected.items():
            for reference in references:
                with self.subTest(surface=surface, reference=reference):
                    self.assertIn(reference, locked)
        for entry in lock["packages"]:
            self.assertEqual(set(entry), {"id", "version", "contentHash"})
            self.assertEqual(len(base64.b64decode(entry["contentHash"], validate=True)), 64)
        self.assertEqual([entry["id"] for entry in lock["packages"]], sorted(locked))

    def test_surfaces_restate_the_owner_references_of_their_project_files(self):
        """A new owner PackageReference in a surface project must also reach the generator, or the lock stays incomplete."""
        source = ROOT / "src"
        for name in ("Hexalith.Platform.Composition", "Hexalith.Platform.EventStoreHost"):
            with self.subTest(surface=name):
                self.assertEqual(project_owner_references(source / name / (name + ".csproj"), source), SURFACES[name])
        reader = project_owner_references(source / "Hexalith.Platform.CatalogBuilder/Hexalith.Platform.CatalogBuilder.csproj", source)
        self.assertTrue(reader)
        self.assertLessEqual(set(reader), set(SURFACES["Hexalith.Platform.CatalogBuilder"]))

    def test_lock_is_written_only_after_signature_and_nuget_agreement(self):
        lock = lock_packages(self.closure, self.packages, get=self.get, verify=signed)
        self.assertEqual(lock, {"schemaVersion": 1, "packages": [
            {"id": package_id, "version": version, "contentHash": content_hash(raw)} for package_id, (version, raw) in sorted(PACKAGES.items())]})
        with self.assertRaisesRegex(ValueError, "nuget verify --all refused Hexalith.EventStore.Contracts 5.2.0"):
            lock_packages(self.closure, self.packages, get=self.get, verify=lambda path: subprocess.CompletedProcess([], 1, "", "NU3004"))
        self.catalog["Hexalith.EventStore.Contracts"]["packageHash"] = content_hash(b"other bytes")
        with self.assertRaisesRegex(ValueError, "Hexalith.EventStore.Contracts 5.2.0 bytes have contentHash .* but nuget.org packageHash is"):
            lock_packages(self.closure, self.packages, get=self.get, verify=signed)

    def test_signature_check_reads_a_private_copy_of_the_hashed_bytes(self):
        seen = []

        def verify(path):
            seen.append((Path(path).name, Path(path).read_bytes(), Path(path).is_relative_to(self.packages)))
            return signed(path)

        lock_packages(self.closure, self.packages, get=self.get, verify=verify)
        self.assertEqual(seen, [(archive_path(self.packages, package_id, version).name, raw, False)
                                for package_id, (version, raw) in sorted(PACKAGES.items())])

    def test_closure_collects_every_owner_package_and_refuses_ambiguity(self):
        documents = {"Hexalith.Platform.Composition": assets({"Hexalith.Tenants.Contracts/8.0.1": "package", "Aspire.Hosting/13.0.0": "package"}),
                     "Hexalith.Platform.CatalogBuilder": assets({"Hexalith.Tenants.Contracts/8.0.1": "package",
                                                                 "Hexalith.EventStore.Contracts/5.2.0": "package"})}
        self.assertEqual(list(owner_closure(documents)), ["Hexalith.EventStore.Contracts", "Hexalith.Tenants.Contracts"])
        for fault, changed in (
                ("project", {"Hexalith.Platform.Composition": assets({"Hexalith.Tenants.Contracts/8.0.1": "project"})}),
                ("versions", {**documents, "Hexalith.Platform.EventStoreHost": assets({"Hexalith.Tenants.Contracts/8.0.2": "package"})}),
                ("empty", {"Hexalith.Platform.Composition": assets({"Aspire.Hosting/13.0.0": "package"})})):
            with self.subTest(fault=fault), self.assertRaisesRegex(ValueError, "not a package|one version per owner|no owner packages"):
                owner_closure(changed)

    def test_enforcement_refuses_tampered_stale_and_missing_entries(self):
        lock = {"schemaVersion": 1, "packages": [{"id": "Hexalith.Tenants.Contracts", "version": "8.0.1", "contentHash": content_hash(b"bytes")}]}
        entry = locked_entry(lock, "Hexalith.Tenants.Contracts", "8.0.1")
        self.assertEqual(require_locked(entry, b"bytes"), content_hash(b"bytes"))
        with self.assertRaisesRegex(ValueError, "Owner package Hexalith.Tenants.Contracts 8.0.1 has contentHash .* differs"):
            require_locked(entry, b"bytes" + b"tampered")
        with self.assertRaisesRegex(ValueError, "names Hexalith.Tenants.Contracts 8.0.1, but 8.0.2 is selected"):
            locked_entry(lock, "Hexalith.Tenants.Contracts", "8.0.2")
        with self.assertRaisesRegex(ValueError, "no entry for Hexalith.EventStore.Contracts 5.2.0"):
            locked_entry(lock, "Hexalith.EventStore.Contracts", "5.2.0")

    def test_malformed_lock_documents_refuse(self):
        valid = {"id": "Hexalith.Tenants.Contracts", "version": "8.0.1", "contentHash": content_hash(b"bytes")}
        path = self.root / "lock.json"
        for document in ({"schemaVersion": 2, "packages": [valid]}, {"schemaVersion": 1, "packages": [valid], "extra": 1},
                         {"schemaVersion": 1, "packages": {}}, {"schemaVersion": 1, "packages": []},
                         {"schemaVersion": 1, "packages": [{**valid, "sha256": "a" * 64}]},
                         {"schemaVersion": 1, "packages": [valid, valid]}, {"schemaVersion": 1, "packages": [{**valid, "version": ""}]},
                         {"schemaVersion": 1, "packages": [{**valid, "id": "Newtonsoft.Json"}]},
                         {"schemaVersion": 1, "packages": [{**valid, "contentHash": "not base64!"}]},
                         {"schemaVersion": 1, "packages": [{**valid, "contentHash": base64.b64encode(b"short").decode()}]}):
            path.write_text(json.dumps(document))
            with self.subTest(document=document), self.assertRaises(ValueError):
                load_lock(path)
        path.unlink()
        with self.assertRaisesRegex(ValueError, "missing or unreadable"):
            load_lock(path)

    def test_run_built_owner_references_follow_the_pinned_project_closure(self):
        checkout = self.root / "references/Hexalith.Parties"
        projects = {
            "src/Hexalith.Parties/Hexalith.Parties.csproj": '<Project><ItemGroup><ProjectReference Include="..\\Hexalith.Parties.Contracts\\Hexalith.Parties.Contracts.csproj" />'
                '<ProjectReference Include="$(HexalithTenantsRoot)/src/Hexalith.Tenants.Client/Hexalith.Tenants.Client.csproj" Condition="x" />'
                '<PackageReference Include="Hexalith.Tenants.Client" Condition="\'$(HexalithTenantsFromSource)\' != \'true\'" />'
                '<PackageReference Include="Hexalith.Commons" /></ItemGroup></Project>',
            "src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj": '<Project><ItemGroup><PackageReference Include="Hexalith.McpCli.Abstractions" />'
                '<PackageReference Include="Hexalith.Parties.Testing" /><ProjectReference Include="../Hexalith.Parties/Hexalith.Parties.csproj" /></ItemGroup></Project>',
        }
        for relative, body in projects.items():
            (checkout / relative).parent.mkdir(parents=True, exist_ok=True)
            (checkout / relative).write_text(body)
        recorded = "a" * 40

        def uninitialized(argv, cwd, text):
            return "160000 commit " + recorded + "\treferences/Hexalith.Parties\n"

        with patch.object(owner_packages_lock.subprocess, "check_output", side_effect=uninitialized), \
                self.assertRaisesRegex(ValueError, "initialize the pinned submodule"):
            run_built_owner_references(self.root)
        (checkout / ".git").write_text("gitdir: synthetic\n")

        def git(argv, cwd, text):
            return ("160000 commit " + recorded + "\treferences/Hexalith.Parties\n") if argv[1] == "ls-tree" else recorded + "\n"

        with patch.object(owner_packages_lock.subprocess, "check_output", side_effect=git):
            self.assertEqual(run_built_owner_references(self.root), ("Hexalith.McpCli.Abstractions", "Hexalith.Tenants.Client"))
        recorded_checkout = recorded
        recorded = "b" * 40
        with patch.object(owner_packages_lock.subprocess, "check_output",
                          side_effect=lambda argv, cwd, text: ("160000 commit " + recorded + "\tx\n") if argv[1] == "ls-tree" else recorded_checkout + "\n"), \
                self.assertRaisesRegex(ValueError, "checkout differs from its recorded gitlink"):
            run_built_owner_references(self.root)

    def test_restore_uses_the_catalog_body_each_surface_name_and_only_nuget_org(self):
        calls = []

        def run(argv, cwd, env, capture_output, text, timeout):
            calls.append((argv, env))
            project = Path(argv[2])
            (project.parent / "obj").mkdir()
            (project.parent / "obj/project.assets.json").write_text(json.dumps(assets({"Hexalith.Tenants.Contracts/8.0.1": "package"})))
            return subprocess.CompletedProcess(argv, 0, "", "")

        work = self.root / "work"
        work.mkdir()
        documents, packages = restore_surfaces(b"<Project>catalog</Project>", work, run=run)
        self.assertEqual(set(documents), set(SURFACES))
        self.assertEqual((work / "Directory.Packages.props").read_bytes(), b"<Project>catalog</Project>")
        props = {element.tag: element.text for element in ET.parse(work / "Directory.Build.props").getroot().iter()}
        self.assertEqual((props["UseHexalithProjectReferences"], props["CentralPackageTransitivePinningEnabled"]), ("false", "true"))
        sources = ET.parse(work / "nuget.config").getroot().find("packageSources")
        self.assertEqual([(element.tag, element.get("value")) for element in sources], [("clear", None), ("add", "https://api.nuget.org/v3/index.json")])
        for (argv, env), (name, references) in zip(calls, SURFACES.items()):
            self.assertEqual(Path(argv[2]).name, name + ".csproj")
            self.assertEqual(argv[3:7], ["--configfile", str(work / "nuget.config"), "--packages", str(packages)])
            self.assertEqual(env["NUGET_PACKAGES"], str(packages))
            included = [item.get("Include") for item in ET.parse(argv[2]).getroot().iter("PackageReference")]
            self.assertEqual(included, list(references))
        failed = self.root / "failed"
        failed.mkdir()
        with self.assertRaisesRegex(ValueError, "owner references against the pinned catalog failed"):
            restore_surfaces(b"<Project />", failed, run=lambda argv, **_: subprocess.CompletedProcess(argv, 1, "", "NU1102"))

    def test_check_refuses_a_lock_that_differs_from_the_regenerated_one(self):
        expected = lock_packages(self.closure, self.packages, get=self.get, verify=signed)
        require_current(json.loads(json.dumps(expected)), expected)
        stale = json.loads(json.dumps(expected))
        stale["packages"][0]["version"] = "8.0.0"
        with self.assertRaisesRegex(ValueError, "differs from the pinned catalog and nuget.org for: Hexalith.EventStore.Contracts"):
            require_current(stale, expected)
        missing = {"schemaVersion": 1, "packages": expected["packages"][1:]}
        with self.assertRaisesRegex(ValueError, "for: Hexalith.EventStore.Contracts"):
            require_current(missing, expected)
        reordered = {"schemaVersion": 1, "packages": list(reversed(expected["packages"]))}
        with self.assertRaisesRegex(ValueError, "regenerated entries in another order"):
            require_current(reordered, expected)

    def test_command_writes_the_regenerated_lock_and_check_never_writes(self):
        body = b"<Project />"
        identity = {"sha256": hashlib.sha256(body).hexdigest()}

        def restore(catalog_body, work, selected):
            self.assertEqual(catalog_body, body)
            self.assertIn("Hexalith.Parties", selected)
            documents = {"Hexalith.Platform.CatalogBuilder": assets({package_id + "/" + version: "package"
                                                                     for package_id, (version, _) in PACKAGES.items()})}
            return documents, self.packages

        output = self.root / "owner-packages.lock.json"
        with contextlib.ExitStack() as stack:
            stack.enter_context(patch.object(owner_packages_lock, "pinned_catalog", return_value=(body, identity)))
            stack.enter_context(patch.object(owner_packages_lock, "run_built_owner_references", return_value=("Hexalith.Tenants.Client",)))
            stack.enter_context(patch.object(owner_packages_lock, "generate", side_effect=lambda: generate(
                restore=restore, get=self.get, verify=signed)))
            stack.enter_context(contextlib.redirect_stdout(io.StringIO()))
            stack.enter_context(patch.object(sys, "argv", ["lock", "--output", str(output)]))
            owner_packages_lock.main()
            written = load_lock(output)
            self.assertEqual([entry["id"] for entry in written["packages"]], sorted(PACKAGES))
            stack.enter_context(patch.object(sys, "argv", ["lock", "--check", "--output", str(output)]))
            owner_packages_lock.main()
            output.write_text(json.dumps({**written, "packages": written["packages"][:1]}))
            before = output.read_bytes()
            with self.assertRaisesRegex(ValueError, "differs from the pinned catalog"):
                owner_packages_lock.main()
            self.assertEqual(output.read_bytes(), before)


if __name__ == "__main__":
    unittest.main()
