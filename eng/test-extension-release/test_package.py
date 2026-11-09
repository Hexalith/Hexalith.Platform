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
    def archive(self, root, *, source=None, dependency="Hexalith.EventStore.DomainService", body=None):
        path = Path(root) / "fixture.nupkg"
        source = source or "a" * 40
        with zipfile.ZipFile(path, "w") as archive:
            archive.writestr("fixture.nuspec", f'<package><metadata><id>{package.PACKAGE}</id><version>1.9.0</version>'
                f'<repository type="git" url="{package.REPOSITORY}" commit="{source}" />'
                f'<dependencies><dependency id="{dependency}" version="[4.2.0, )" /></dependencies></metadata></package>')
            archive.writestr(package.LIBRARY, body or b"EventStoreExtensionAttribute platform-test")
        return path

    def test_accepts_only_the_declared_source_and_fixture(self):
        with tempfile.TemporaryDirectory() as root:
            result = package.inspect(self.archive(root), "1.9.0", "a" * 40)
            self.assertEqual(result["packageId"], package.PACKAGE)
            self.assertIn(package.LIBRARY, result["bodies"])

    def test_refuses_different_source(self):
        with tempfile.TemporaryDirectory() as root:
            with self.assertRaises(ValueError):
                package.inspect(self.archive(root, source="b" * 40), "1.9.0", "a" * 40)

    def test_refuses_foreign_dependency(self):
        with tempfile.TemporaryDirectory() as root:
            with self.assertRaises(ValueError):
                package.inspect(self.archive(root, dependency="Foreign.SDK"), "1.9.0", "a" * 40)

    def test_refuses_fixture_built_without_extension_api(self):
        with tempfile.TemporaryDirectory() as root:
            with self.assertRaises(ValueError):
                package.inspect(self.archive(root, body=b"fixture without identity"), "1.9.0", "a" * 40)


if __name__ == "__main__":
    unittest.main()
