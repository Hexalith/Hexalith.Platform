"""Real isolated source/clone controls for the explicitly selected historical reader workspace."""
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import legacy_tuple_source as legacy
import pre_extension_tuple_source as pre
from prepare_tuple_sources import FROZEN, prepare_tree
from package_reader_source import prepare_workspace, verify_source, direct_owners


class PackageReaderSourceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="reader-clone-control-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "source"
        self.owner = self.root / "references/Hexalith.Owner"
        for root, remote in [(self.root, "Hexalith.Platform"), (self.owner, "Hexalith.Owner")]:
            root.mkdir(parents=True, exist_ok=True)
            self.git(root, "init", "-q")
            self.git(root, "config", "user.name", "Reader source fixture")
            self.git(root, "config", "user.email", "fixture@invalid")
            self.git(root, "remote", "add", "origin", "https://github.com/Hexalith/" + remote + ".git")
        self.write(self.owner, "module.json", b'{}\n')
        self.write(self.owner, ".gitmodules", b'[submodule "references/Nested"]\n path = references/Nested\n url = https://example.invalid/Nested.git\n')
        self.git(self.owner, "update-index", "--add", "--cacheinfo", "160000," + "e" * 40 + ",references/Nested")
        old = self.commit(self.owner)
        self.write(self.owner, "next.txt", b"newer real owner source\n")
        self.owner_current = self.commit(self.owner)
        self.write(self.root, ".gitmodules", b'[submodule "references/Hexalith.Owner"]\n path = references/Hexalith.Owner\n url = https://github.com/Hexalith/Hexalith.Owner.git\n')
        self.write(self.root, "tuple.json", b"frozen authenticated private fixture tuple\n")
        self.write(self.root, "src/current.txt", b"original source\n")
        self.old_descriptor = dict(schema="hexalith.platform.legacy-tuple-source.v1", purpose="same-code-legacy-tuple-regression",
            sourceRevision=None, buildsSource=legacy.BUILDS, toolVersion="4.44.3", profileSha256=legacy.PROFILE_HASH,
            requiredCount=349, qualificationCredit=False)
        self.pre_descriptor = dict(schema="hexalith.platform.pre-extension-tuple-source.v1", purpose="same-code-sdk41-tuple-regression",
            sourceRevision=None, buildsSource=pre.BUILDS, eventStoreSource=pre.EVENTSTORE, toolVersion="4.45.0",
            profileSha256=pre.PROFILE_HASH, requiredCount=412, qualificationCredit=False)
        self.descriptors()
        self.git(self.root, "update-index", "--add", "--cacheinfo", "160000," + old + ",references/Hexalith.Owner")
        first = self.commit(self.root)
        self.old_descriptor['sourceRevision'] = self.pre_descriptor['sourceRevision'] = first
        self.descriptors()
        anchor = self.commit(self.root)
        # Synthetic constants are scoped solely to this real local Git behavior control.
        self.frozen = dict(anchor=anchor, hashes={"tuple.json": hashlib.sha256(b"frozen authenticated private fixture tuple\n").hexdigest()},
                           gitlinks={"references/Hexalith.Owner": old})
        self.write(self.root, "tuple.json", b"new current tuple\n")
        self.write(self.root, "src/current.txt", b"same new production and controls\n")
        self.git(self.root, "update-index", "--cacheinfo", "160000," + self.owner_current + ",references/Hexalith.Owner")
        current = self.commit(self.root)
        tree = prepare_tree(self.root, current, self.frozen)
        self.reader = self.git(self.root, "commit-tree", tree, "-p", current, "-m", "test(tuple): record private reader source")
        self.pre_descriptor['packageReaderSourceRevision'] = self.reader
        self.descriptors()
        self.current = self.commit(self.root)

    def git(self, root, *args):
        return subprocess.check_output(["git", *args], cwd=root, stderr=subprocess.PIPE).decode().strip()

    def write(self, root, path, raw):
        destination = root / path
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(raw)

    def descriptors(self):
        for path, document in [(legacy.DESCRIPTOR, self.old_descriptor), (pre.DESCRIPTOR, self.pre_descriptor)]:
            self.write(self.root, path, json.dumps(document).encode())

    def commit(self, root):
        self.git(root, "add", ".", ":(exclude)references")
        self.git(root, "commit", "-q", "-m", "test(tuple): record local reader inputs")
        return self.git(root, "rev-parse", "HEAD")

    def test_real_clones_select_exact_sources_without_changing_shared_or_nested_checkouts(self):
        destination = Path(self.temp.name) / "prepared"
        before = (self.git(self.root, "rev-parse", "HEAD"), self.git(self.root, "status", "--porcelain"),
                  self.git(self.owner, "rev-parse", "HEAD"), self.git(self.owner, "status", "--porcelain"))
        with patch.dict(FROZEN, {"package-reader": self.frozen}):
            receipt = prepare_workspace(self.root, self.current, destination)
        self.assertEqual(receipt['packageReaderSourceRevision'], self.reader)
        self.assertEqual(self.git(destination, "rev-parse", "HEAD"), self.reader)
        self.assertEqual(self.git(destination / "references/Hexalith.Owner", "rev-parse", "HEAD"), self.frozen['gitlinks']['references/Hexalith.Owner'])
        self.assertEqual(self.git(destination / "references/Hexalith.Owner", "rev-parse", "--show-superproject-working-tree"), str(destination))
        self.assertEqual(self.git(destination, "show", "HEAD:src/current.txt"), "same new production and controls")
        self.assertFalse((destination / "references/Hexalith.Owner/references/Nested/.git").exists())
        self.assertEqual(before, (self.git(self.root, "rev-parse", "HEAD"), self.git(self.root, "status", "--porcelain"),
                                 self.git(self.owner, "rev-parse", "HEAD"), self.git(self.owner, "status", "--porcelain")))
        self.assertFalse(receipt['runtimeAcceptance'])
        self.assertFalse(receipt['candidateAcceptance'])

    def test_stale_production_or_control_source_cannot_reuse_the_reader_descriptor(self):
        self.write(self.root, "src/current.txt", b"different production after reader preparation\n")
        changed = self.commit(self.root)
        with self.assertRaisesRegex(ValueError, "production, controls"):
            verify_source(self.root, changed, self.reader, frozen=self.frozen)

    def test_existing_destination_is_never_overwritten(self):
        destination = Path(self.temp.name) / "existing"
        destination.mkdir()
        (destination / "keep.txt").write_text("preserve")
        with patch.dict(FROZEN, {"package-reader": self.frozen}), self.assertRaisesRegex(ValueError, "new task-owned"):
            prepare_workspace(self.root, self.current, destination)
        self.assertEqual((destination / "keep.txt").read_text(), "preserve")

    def test_frozen_reader_metadata_and_owner_gitlinks_are_not_general_source_exemptions(self):
        self.git(self.root, "checkout", "-q", self.reader)
        self.write(self.root, "tuple.json", b"unapproved tuple\n")
        altered = self.commit(self.root)
        self.git(self.root, "checkout", "-q", self.current)
        self.pre_descriptor['packageReaderSourceRevision'] = altered
        self.descriptors()
        changed = self.commit(self.root)
        with self.assertRaisesRegex(ValueError, "exact published tuple"):
            verify_source(self.root, changed, altered, frozen=self.frozen)

    def test_escape_and_duplicate_direct_owner_paths_are_refused(self):
        self.write(self.root, ".gitmodules", b'[submodule "bad"]\n path = references/../escape\n')
        changed = self.commit(self.root)
        with self.assertRaisesRegex(ValueError, "root-declared"):
            direct_owners(self.root, changed)


if __name__ == '__main__':
    unittest.main()
