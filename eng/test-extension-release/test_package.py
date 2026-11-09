"""Resource-free negative controls for the finite fixture package identity."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
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
            archive.writestr("fixture.nuspec", f'<package><metadata><id>{package.PACKAGE}</id><version>1.9.0</version>'
                f'<repository type="git" url="{package.REPOSITORY}" commit="{source}" />'
                f'<dependencies><dependency id="{dependency}" version="[4.2.0, )" /></dependencies></metadata></package>')
            archive.writestr(package.LIBRARY, body or b"EventStoreExtensionAttribute platform-test")
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


if __name__ == "__main__":
    unittest.main()
