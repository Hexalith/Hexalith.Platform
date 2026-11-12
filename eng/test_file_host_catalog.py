"""Real Git controls for current file AppHost authority during historical model replay."""
import copy
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

from file_host_catalog import prepare


class FileHostCatalogTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="file-host-catalog-control-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "platform"
        self.builds = Path(self.temp.name) / "builds"
        for root in [self.root, self.builds]:
            root.mkdir()
            self.git(root, "init", "-q")
            self.git(root, "config", "user.name", "File host control")
            self.git(root, "config", "user.email", "fixture@invalid")
        self.path = "Props/Directory.Packages.props"
        self.write(self.builds, self.path, b"<Project><OldCatalog /></Project>\n")
        self.old = self.commit(self.builds)
        self.raw = b"<Project><CurrentCatalog /></Project>\n"
        self.write(self.builds, self.path, self.raw)
        self.source = self.commit(self.builds)
        self.binding = {"schemaVersion": 1, "candidateAcceptance": False,
            "producer": {"repository": "https://github.com/Hexalith/Hexalith.Builds.git", "sourceRevision": self.source,
                "catalog": {"path": self.path, "blob": self.git(self.builds, "rev-parse", "HEAD:" + self.path),
                            "sha256": hashlib.sha256(self.raw).hexdigest()}},
            "publication": {"version": "4.99.0", "sourceRevision": self.source, "releaseTag": "v4.99.0",
                "catalogSha256": hashlib.sha256(self.raw).hexdigest(), "archiveSha256": "a" * 64,
                "toolingSha256": "b" * 64, "inventorySha256": "c" * 64,
                "inventoryUrl": "https://github.com/Hexalith/Hexalith.Builds/releases/download/v4.99.0/g4-tool-package-inventory.json"}}
        self.write(self.root, "apphost.cs", b"// Exact same current file AppHost source\n")
        self.write(self.root, "eng/builds-tuple.json", json.dumps(self.binding).encode())
        self.gitlink(self.source)
        self.current = self.commit(self.root)
        self.gitlink(self.old)
        self.historical = self.commit(self.root)
        self.git(self.builds, "checkout", "-q", self.old)

    def git(self, root, *args):
        return subprocess.check_output(["git", *args], cwd=root, stderr=subprocess.PIPE).decode().strip()

    def write(self, root, relative, raw):
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)

    def commit(self, root):
        if root == self.root:
            self.git(root, "add", ".", ":(exclude)references")
        else:
            self.git(root, "add", ".")
        self.git(root, "commit", "-q", "-m", "test(tuple): record private file host control")
        return self.git(root, "rev-parse", "HEAD")

    def gitlink(self, source):
        self.git(self.root, "update-index", "--add", "--cacheinfo", "160000," + source + ",references/Hexalith.Builds")

    def output(self):
        return Path(self.temp.name) / "prepared"

    def test_exact_current_catalog_is_exported_without_changing_historical_checkout(self):
        before = (self.git(self.root, "rev-parse", "HEAD"), self.git(self.builds, "rev-parse", "HEAD"))
        catalog, receipt = prepare(self.root, self.builds, self.current, self.output())
        self.assertEqual(catalog.read_bytes(), self.raw)
        self.assertEqual(receipt["buildsSourceRevision"], self.source)
        self.assertFalse(receipt["runtimeAcceptance"])
        self.assertFalse(receipt["candidateAcceptance"])
        self.assertEqual(before, (self.git(self.root, "rev-parse", "HEAD"), self.git(self.builds, "rev-parse", "HEAD")))

    def test_changed_apphost_cannot_use_another_current_sources_catalog(self):
        self.write(self.root, "apphost.cs", b"// Different source\n")
        self.commit(self.root)
        with self.assertRaisesRegex(ValueError, "AppHost source"):
            prepare(self.root, self.builds, self.current, self.output())

    def test_self_consistent_but_false_catalog_digest_refuses(self):
        self.git(self.root, "checkout", "-q", self.current)
        binding = copy.deepcopy(self.binding)
        binding["producer"]["catalog"]["sha256"] = binding["publication"]["catalogSha256"] = "0" * 64
        self.write(self.root, "eng/builds-tuple.json", json.dumps(binding).encode())
        changed = self.commit(self.root)
        with self.assertRaisesRegex(ValueError, "authenticated Git blob"):
            prepare(self.root, self.builds, changed, self.output())

    def test_producer_gitlink_must_equal_the_current_binding(self):
        with self.assertRaisesRegex(ValueError, "producer gitlink"):
            prepare(self.root, self.builds, self.historical, self.output())

    def test_annotated_tag_object_cannot_alias_a_current_source_commit(self):
        self.git(self.root, "tag", "-a", "alias", self.current, "-m", "Private tag control")
        alias = self.git(self.root, "rev-parse", "refs/tags/alias")
        with self.assertRaisesRegex(ValueError, "canonical current source commit"):
            prepare(self.root, self.builds, alias, self.output())

    def test_existing_evidence_directory_is_not_overwritten(self):
        self.output().mkdir()
        marker = self.output() / "keep.txt"
        marker.write_text("preserve")
        with self.assertRaises(FileExistsError):
            prepare(self.root, self.builds, self.current, self.output())
        self.assertEqual(marker.read_text(), "preserve")


if __name__ == "__main__":
    unittest.main()
