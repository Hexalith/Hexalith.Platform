"""Resource-free negative controls for the finite fixture package identity."""
import base64
import hashlib
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location("fixture_package", Path(__file__).with_name("verify-package.py"))
package = importlib.util.module_from_spec(spec)
spec.loader.exec_module(package)


class PackageIdentityTests(unittest.TestCase):
    approved = {package.DECLARATION: (package.ROOT / package.FIXTURE / "hexalith.module-manifest.v2.json").read_bytes(),
                package.PRODUCER: (package.ROOT / package.FIXTURE / "fixture-producer.json").read_bytes(),
                **{member: (package.ROOT / source).read_bytes() for source, member in package.NATIVE_SOURCE_FILES.items()}}

    def inspect(self, path, version, source):
        return package.inspect(path, version, source, approved=self.approved)

    def archive(self, root, *, source=None, dependency="Hexalith.EventStore.DomainService", body=None, declaration=None, producer=None, test_entry=None, omit=None):
        path = Path(root) / "fixture.nupkg"
        source = source or "a" * 40
        with zipfile.ZipFile(path, "w") as archive:
            archive.writestr(package.SPECIFICATION, f'<package><metadata><id>{package.PACKAGE}</id><version>1.9.0</version>'
                f'<repository type="git" url="{package.REPOSITORY}" commit="{source}" />'
                f'<dependencies><dependency id="{dependency}" version="[4.2.0, )" /></dependencies></metadata></package>')
            archive.writestr(package.LIBRARY, body or b"EventStoreExtensionAttribute platform-test")
            archive.writestr(package.LIBRARY.removesuffix(".dll") + ".xml", "<doc/>")
            for member in package.NUGET_METADATA - {package.SPECIFICATION}:
                archive.writestr(member, "<metadata/>")
            if omit != package.DECLARATION:
                archive.writestr(package.DECLARATION, declaration or self.approved[package.DECLARATION])
            if omit != package.PRODUCER:
                archive.writestr(package.PRODUCER, producer or self.approved[package.PRODUCER])
            for member in package.NATIVE_SOURCE_FILES.values():
                if omit != member:
                    archive.writestr(member, test_entry or self.approved[member])
        return path

    def test_accepts_only_the_declared_source_and_fixture(self):
        with tempfile.TemporaryDirectory() as root:
            result = self.inspect(self.archive(root), "1.9.0", "a" * 40)
            self.assertEqual(result["packageId"], package.PACKAGE)
            self.assertIn(package.LIBRARY, result["bodies"])

    def test_refuses_extra_payload_and_excludes_only_the_standard_signature_from_body_comparison(self):
        with tempfile.TemporaryDirectory() as root:
            path = self.archive(root)
            with zipfile.ZipFile(path, "a") as archive:
                archive.writestr(".signature.p7s", b"signature belongs to the independent signature verifier")
            self.assertNotIn(".signature.p7s", self.inspect(path, "1.9.0", "a" * 40)["bodies"])
        for member in ["build/Unexpected.targets", "lib/net11.0/Unexpected.dll", "package/services/metadata/core-properties/unexpected.psmdcp"]:
            with self.subTest(member=member), tempfile.TemporaryDirectory() as root:
                path = self.archive(root)
                with zipfile.ZipFile(path, "a") as archive:
                    archive.writestr(member, b"unexpected")
                with self.assertRaisesRegex(ValueError, "Unexpected or missing fixture archive members"):
                    self.inspect(path, "1.9.0", "a" * 40)

    def test_refuses_missing_declaration_or_producer(self):
        for missing in [package.DECLARATION, package.PRODUCER, *package.NATIVE_SOURCE_FILES.values()]:
            with self.subTest(missing=missing), tempfile.TemporaryDirectory() as root, self.assertRaises(ValueError):
                self.inspect(self.archive(root, omit=missing), "1.9.0", "a" * 40)

    def test_refuses_altered_declaration_or_foreign_producer(self):
        for member in ["declaration", "producer", "test_entry"]:
            with self.subTest(member=member), tempfile.TemporaryDirectory() as root, self.assertRaises(ValueError):
                self.inspect(self.archive(root, **{member: b'{}'}), "1.9.0", "a" * 40)

    def test_refuses_unapproved_source_owned_identity_even_if_body_is_consistent(self):
        approved = dict(self.approved)
        approved[package.PRODUCER] = approved[package.PRODUCER].replace(b'Hexalith/Hexalith.Platform', b'Foreign/Other')
        with tempfile.TemporaryDirectory() as root:
            path = self.archive(root, producer=approved[package.PRODUCER])
            with self.assertRaisesRegex(ValueError, "unsupported"):
                package.inspect(path, "1.9.0", "a" * 40, approved=approved)

    def test_refuses_consistently_rebound_foreign_owner_metadata(self):
        approved = dict(self.approved)
        approved[package.PRODUCER] = approved[package.PRODUCER].replace(b'"Hexalith.Platform"', b'"Hexalith.Platformtestextension"')
        with tempfile.TemporaryDirectory() as root:
            path = self.archive(root, producer=approved[package.PRODUCER])
            with self.assertRaisesRegex(ValueError, "unsupported"):
                package.inspect(path, "1.9.0", "a" * 40, approved=approved)

    def test_refuses_consistently_rebound_test_profile(self):
        approved = dict(self.approved)
        approved[package.DECLARATION] = approved[package.DECLARATION].replace(b'eventstore-extension-package', b'foreign-profile')
        with tempfile.TemporaryDirectory() as root:
            path = self.archive(root, declaration=approved[package.DECLARATION])
            with self.assertRaisesRegex(ValueError, "unsupported"):
                package.inspect(path, "1.9.0", "a" * 40, approved=approved)

    def test_refuses_different_source(self):
        with tempfile.TemporaryDirectory() as root:
            with self.assertRaises(ValueError):
                self.inspect(self.archive(root, source="b" * 40), "1.9.0", "a" * 40)

    def test_refuses_foreign_dependency(self):
        with tempfile.TemporaryDirectory() as root:
            with self.assertRaises(ValueError):
                self.inspect(self.archive(root, dependency="Foreign.SDK"), "1.9.0", "a" * 40)

    def test_refuses_fixture_built_without_extension_api(self):
        with tempfile.TemporaryDirectory() as root:
            with self.assertRaises(ValueError):
                self.inspect(self.archive(root, body=b"fixture without identity"), "1.9.0", "a" * 40)


class PackageSdkTests(unittest.TestCase):
    version = "5.4.0"
    source = "a" * 40
    member = "lib/net11.0/" + package.SDK_PACKAGE + ".dll"

    def prepare_restore(self, root, *, archive_source=None):
        path = package.archive_path(root, package.SDK_PACKAGE, self.version)
        path.parent.mkdir(parents=True)
        with zipfile.ZipFile(path, "w") as sdk:
            sdk.writestr(package.SDK_PACKAGE + ".nuspec",
                         f'<package><metadata><repository url="{package.SDK_REPOSITORY}" '
                         f'commit="{archive_source or self.source}" /></metadata></package>')
            sdk.writestr(self.member, b"SDK body")
        extracted = path.parent / self.member
        extracted.parent.mkdir(parents=True)
        extracted.write_bytes(b"SDK body")
        name = f"{package.SDK_PACKAGE}/{self.version}"
        assets = {
            "libraries": {name: {"type": "package", "path": f"{package.SDK_PACKAGE.lower()}/{self.version}"}},
            "packageFolders": {str(root): {}},
            "targets": {"net11.0": {name: {"compile": {self.member: {}}, "runtime": {self.member: {}}}}},
        }
        dependency = [{"id": package.SDK_PACKAGE, "version": self.version}]
        release = {"version": self.version, "commit": self.source}
        content_hash = base64.b64encode(hashlib.sha512(path.read_bytes()).digest()).decode()
        lock = {"packages": [{"id": package.SDK_PACKAGE, "version": self.version, "contentHash": content_hash}]}
        return path, extracted, assets, dependency, release, lock

    def test_accepts_current_release_and_authenticates_actual_archive(self):
        with tempfile.TemporaryDirectory() as root, patch.object(package.subprocess, "run") as verify_signature:
            path, _, *inputs = self.prepare_restore(root)

            def verify_copy(arguments, **options):
                self.assertEqual(arguments[:3], ["dotnet", "nuget", "verify"])
                self.assertEqual(arguments[4:], ["--all"])
                self.assertEqual(options, {"capture_output": True, "text": True, "timeout": 60, "check": True})
                copy = Path(arguments[3])
                self.assertNotEqual(copy, path)
                self.assertEqual(copy.read_bytes(), path.read_bytes())
                return subprocess.CompletedProcess([], 0, "signature verified")

            verify_signature.side_effect = verify_copy
            signed = package.verify_sdk(*inputs)
            identity = signed[f"{package.SDK_PACKAGE}/{self.version}"]
            self.assertEqual(identity["source"], self.source)
            self.assertEqual(identity["signatureVerification"], "signature verified")
            verify_signature.assert_called_once()

    def test_refuses_wrong_dependency_and_restored_version(self):
        with tempfile.TemporaryDirectory() as root:
            _, _, assets, dependency, release, lock = self.prepare_restore(root)
            with self.assertRaisesRegex(ValueError, "dependency must name"):
                package.verify_sdk(assets, [{"id": package.SDK_PACKAGE, "version": "4.2.0"}], release, lock)
            restored = assets["libraries"].pop(f"{package.SDK_PACKAGE}/{self.version}")
            assets["libraries"][f"{package.SDK_PACKAGE}/4.2.0"] = restored
            with self.assertRaisesRegex(ValueError, "restored.+4.2.0.+qualified EventStore release"):
                package.verify_sdk(assets, dependency, release, lock)

    def test_refuses_modified_archive_before_signature_verification(self):
        with tempfile.TemporaryDirectory() as root, patch.object(package.subprocess, "run") as verify_signature:
            path, _, *inputs = self.prepare_restore(root)
            path.write_bytes(path.read_bytes() + b"unlocked bytes")
            with self.assertRaisesRegex(ValueError, "owner-packages.lock.json"):
                package.verify_sdk(*inputs)
            verify_signature.assert_not_called()

    def test_refuses_locked_archive_from_wrong_source_or_with_wrong_compile_bytes(self):
        for mismatch in ("source", "restored SDK body"):
            with self.subTest(mismatch=mismatch), tempfile.TemporaryDirectory() as root, \
                    patch.object(package.subprocess, "run") as verify_signature:
                foreign = mismatch == "source"
                _, extracted, *inputs = self.prepare_restore(root, archive_source="b" * 40 if foreign else None)
                if not foreign:
                    extracted.write_bytes(b"altered SDK body")
                verify_signature.return_value = subprocess.CompletedProcess([], 0, "signature verified")
                with self.assertRaisesRegex(ValueError, mismatch):
                    package.verify_sdk(*inputs)

    def test_refuses_missing_domainservice_compile_asset(self):
        with tempfile.TemporaryDirectory() as root:
            _, _, assets, dependency, release, lock = self.prepare_restore(root)
            selected = assets["targets"]["net11.0"][f"{package.SDK_PACKAGE}/{self.version}"]
            for compile_target in ({}, {"lib/net11.0/_._": {}}):
                with self.subTest(compile_target=compile_target):
                    selected["compile"] = compile_target
                    with self.assertRaisesRegex(ValueError, "no selected compile assembly"):
                        package.verify_sdk(assets, dependency, release, lock)

    def test_refuses_invalid_nuget_signature(self):
        with tempfile.TemporaryDirectory() as root, patch.object(package.subprocess, "run") as verify_signature:
            _, _, *inputs = self.prepare_restore(root)
            verify_signature.side_effect = subprocess.CalledProcessError(1, "dotnet nuget verify")
            with self.assertRaises(subprocess.CalledProcessError):
                package.verify_sdk(*inputs)


if __name__ == "__main__":
    unittest.main()
