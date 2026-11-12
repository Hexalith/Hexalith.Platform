"""Real isolated Git controls for current sources, released packages and companion preparation."""
from __future__ import annotations

import copy
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

import current_tuple_source as current
from legacy_tuple_source import PROVENANCE
from prepare_tuple_sources import FROZEN, prepare_tree


class CurrentTupleSourceTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="current-tuple-source-control-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.initialize(self.root)
        self.provenance = {}
        for name, repository in current.OWNERS.items():
            owner = self.root / "references" / repository
            self.initialize(owner)
            manifest = (json.dumps({"owner": repository, "released": True}) + "\n").encode()
            self.write(owner, current.MANIFEST, manifest)
            release = self.commit(owner)
            self.git(owner, "tag", "v1.0.0", release)
            # The synchronized current source deliberately advances beyond the released package.
            self.write(owner, current.MANIFEST, (json.dumps({"owner": repository, "released": False}) + "\n").encode())
            self.write(owner, "current-source.txt", b"newer owner source\n")
            source = self.commit(owner)
            self.gitlink(repository, source)
            if name in current.PACKAGES:
                self.provenance[name] = {"repository": repository, "version": "1.0.0", "commit": release,
                                         "source": current.MANIFEST, "sha256": hashlib.sha256(manifest).hexdigest()}
                self.write(self.root, "src/Hexalith.Platform.Composition/Declarations/" + name + ".json", manifest)
        self.write(self.root, PROVENANCE, json.dumps(self.provenance).encode())
        self.write(self.root, "src/production.txt", b"same current production\n")
        unbound = self.commit(self.root)
        self.binding = current.create_binding(self.root, unbound)
        self.write(self.root, current.BINDING, json.dumps(self.binding).encode())
        self.source = self.commit(self.root)

    def initialize(self, root):
        root.mkdir(parents=True, exist_ok=True)
        self.git(root, "init", "-q")
        self.git(root, "config", "user.name", "Current tuple source control")
        self.git(root, "config", "user.email", "fixture@invalid")

    def git(self, root, *args):
        return subprocess.run(["git", *args], cwd=root, check=True, capture_output=True).stdout.decode().strip()

    def write(self, root, path, raw):
        target = root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(raw)

    def commit(self, root):
        self.git(root, "add", ".", ":(exclude)references") if root == self.root else self.git(root, "add", ".")
        self.git(root, "commit", "-q", "-m", "test(tuple): record private source control")
        return self.git(root, "rev-parse", "HEAD")

    def gitlink(self, repository, source):
        self.git(self.root, "update-index", "--add", "--cacheinfo", "160000," + source + ",references/" + repository)

    def test_current_checkout_and_actual_release_are_distinct_authenticated_identities(self):
        result = current.verify_checkout(self.root, self.source)
        self.assertFalse(result["qualificationCredit"])
        for name, item in self.provenance.items():
            self.assertNotEqual(item["commit"], self.binding["ownerSources"][name]["sourceRevision"])
        current.verify_package_release_identities(self.root, self.source)

    def test_current_gitlink_change_requires_a_matching_committed_binding(self):
        self.gitlink("Hexalith.Parties", "e" * 40)
        changed = self.commit(self.root)
        with self.assertRaisesRegex(ValueError, "gitlink differs"):
            current.read_selection(self.root, changed)

    def test_worktree_mutation_cannot_supply_current_owner_declaration_authority(self):
        owner = self.root / "references/Hexalith.Parties"
        self.write(owner, current.MANIFEST, b"uncommitted replacement declaration\n")
        self.assertEqual(current.create_binding(self.root, self.source), self.binding)
        with self.assertRaisesRegex(ValueError, "checkout or declaration differs"):
            current.verify_checkout(self.root, self.source)

    def test_wrong_initialized_owner_head_refuses_with_unchanged_manifest(self):
        owner = self.root / "references/Hexalith.Parties"
        manifest = (owner / current.MANIFEST).read_bytes()
        self.write(owner, "later-source.txt", b"unselected later source\n")
        self.commit(owner)
        self.assertEqual((owner / current.MANIFEST).read_bytes(), manifest)
        with self.assertRaisesRegex(ValueError, "checkout or declaration differs"):
            current.verify_checkout(self.root, self.source)

    def test_crossed_release_tag_and_source_refuse_even_with_fresh_binding(self):
        item = self.provenance["tenants"]
        item["commit"] = self.binding["ownerSources"]["tenants"]["sourceRevision"]
        self.write(self.root, PROVENANCE, json.dumps(self.provenance).encode())
        changed = self.commit(self.root)
        self.write(self.root, current.BINDING, json.dumps(current.create_binding(self.root, changed)).encode())
        changed = self.commit(self.root)
        with self.assertRaisesRegex(ValueError, "actual release tag"):
            current.verify_package_release_identities(self.root, changed)

    def test_missing_package_tag_cannot_be_replaced_by_the_committed_binding(self):
        owner = self.root / "references/Hexalith.Memories"
        self.git(owner, "tag", "-d", "v1.0.0")
        with self.assertRaisesRegex(ValueError, "release tag or declaration is unavailable"):
            current.verify_package_release_identities(self.root, self.source)

    def test_package_declaration_snapshot_and_source_blob_must_match(self):
        self.write(self.root, "src/Hexalith.Platform.Composition/Declarations/eventstore.json", b"foreign snapshot\n")
        changed = self.commit(self.root)
        with self.assertRaisesRegex(ValueError, "snapshot differs"):
            current.read_selection(self.root, changed)

    def test_self_consistent_snapshot_must_match_actual_release_blob(self):
        forged = b'{"owner":"Hexalith.Tenants","unpublished":true}\n'
        self.write(self.root, "src/Hexalith.Platform.Composition/Declarations/tenants.json", forged)
        self.provenance["tenants"]["sha256"] = hashlib.sha256(forged).hexdigest()
        self.write(self.root, PROVENANCE, json.dumps(self.provenance).encode())
        unbound = self.commit(self.root)
        self.write(self.root, current.BINDING, json.dumps(current.create_binding(self.root, unbound)).encode())
        changed = self.commit(self.root)
        current.read_selection(self.root, changed)
        with self.assertRaisesRegex(ValueError, "actual release tag or declaration blob"):
            current.verify_package_release_identities(self.root, changed)

    def test_tree_targeting_tag_cannot_supply_a_release_commit(self):
        owner = self.root / "references/Hexalith.Tenants"
        tree = self.git(owner, "rev-parse", "v1.0.0^{tree}")
        self.assertEqual(self.git(owner, "cat-file", "-t", tree), "tree")
        self.git(owner, "tag", "-f", "v1.0.0", tree)
        self.provenance["tenants"]["commit"] = tree
        self.write(self.root, PROVENANCE, json.dumps(self.provenance).encode())
        unbound = self.commit(self.root)
        self.write(self.root, current.BINDING, json.dumps(current.create_binding(self.root, unbound)).encode())
        changed = self.commit(self.root)
        with self.assertRaisesRegex(ValueError, "release tag or declaration is unavailable"):
            current.verify_package_release_identities(self.root, changed)

    def test_foreign_package_repository_and_extra_provenance_fields_refuse(self):
        self.provenance["eventstore"]["repository"] = "Foreign.Owner"
        self.write(self.root, PROVENANCE, json.dumps(self.provenance).encode())
        changed = self.commit(self.root)
        self.write(self.root, current.BINDING, json.dumps(current.create_binding(self.root, changed)).encode())
        changed = self.commit(self.root)
        with self.assertRaisesRegex(ValueError, "Unsupported current package release provenance"):
            current.read_selection(self.root, changed)

    def frozen_anchor(self, name):
        frozen = copy.deepcopy(FROZEN[name])
        production_root = Path(__file__).resolve().parents[1]
        for path in frozen["hashes"]:
            raw = subprocess.check_output(["git", "show", frozen["anchor"] + ":" + path], cwd=production_root)
            self.write(self.root, path, raw)
        for path, source in frozen["gitlinks"].items():
            (self.root / path).mkdir(parents=True, exist_ok=True)
            self.git(self.root, "update-index", "--add", "--cacheinfo", "160000," + source + "," + path)
        frozen["anchor"] = self.commit(self.root)
        self.git(self.root, "checkout", "-q", self.source)
        return frozen

    def test_companion_preparation_changes_only_frozen_metadata_and_preserves_index(self):
        for name in FROZEN:
            with self.subTest(cohort=name):
                frozen = self.frozen_anchor(name)
                before = (self.git(self.root, "write-tree"), self.git(self.root, "rev-parse", "HEAD"), self.git(self.root, "status", "--porcelain"))
                tree = prepare_tree(self.root, self.source, frozen)
                after = (self.git(self.root, "write-tree"), self.git(self.root, "rev-parse", "HEAD"), self.git(self.root, "status", "--porcelain"))
                self.assertEqual(before, after)
                self.assertEqual(self.git(self.root, "show", tree + ":src/production.txt"), "same current production")
                self.assertEqual(self.git(self.root, "show", tree + ":" + current.BINDING), self.git(self.root, "show", self.source + ":" + current.BINDING))
                for path, expected in frozen["hashes"].items():
                    raw = subprocess.check_output(["git", "show", tree + ":" + path], cwd=self.root)
                    self.assertEqual(hashlib.sha256(raw).hexdigest(), expected)

    def test_refreshed_companions_keep_exact_production_equality_and_frozen_owner_tuples(self):
        import legacy_tuple_source as legacy
        import pre_extension_tuple_source as pre_extension
        production_root = Path(__file__).resolve().parents[1]
        frozen = {name: self.frozen_anchor(name) for name in FROZEN}
        for path in ["eng/builds-tuple.json", ".config/dotnet-tools.json", "hexalith-platform.json", "eng/tuple-ci-suites.json"]:
            raw = subprocess.check_output(["git", "show", frozen["pre-extension"]["anchor"] + ":" + path], cwd=self.root)
            self.write(self.root, path, raw)
        binding = json.loads((self.root / "eng/builds-tuple.json").read_bytes())
        binding["producer"]["sourceRevision"] = "e" * 40
        binding["publication"].update(version="4.999.0", sourceRevision="e" * 40, releaseTag="v4.999.0",
            inventoryUrl="https://github.com/Hexalith/Hexalith.Builds/releases/download/v4.999.0/g4-tool-package-inventory.json")
        self.write(self.root, "eng/builds-tuple.json", json.dumps(binding).encode())
        tools = json.loads((self.root / ".config/dotnet-tools.json").read_bytes())
        tools["tools"]["hexalith.builds.module.cli"]["version"] = "4.999.0"
        self.write(self.root, ".config/dotnet-tools.json", json.dumps(tools).encode())
        self.write(self.root, "hexalith-platform.json", b'{"schemaVersion":1,"toolVersionRange":"[4.999.0, 5.0.0-0)"}')
        (self.root / "references/Hexalith.Builds").mkdir(exist_ok=True)
        self.gitlink("Hexalith.Builds", "e" * 40)
        descriptors = {}
        for name, descriptor in [("legacy", legacy.DESCRIPTOR), ("pre-extension", pre_extension.DESCRIPTOR)]:
            descriptors[name] = json.loads((production_root / descriptor).read_bytes())
            descriptors[name]["sourceRevision"] = frozen[name]["anchor"]
            if name == "pre-extension":
                # Production selectors are unavailable in this private fixture history.
                descriptors[name]["packageReaderSourceRevision"] = frozen["package-reader"]["anchor"]
            self.write(self.root, descriptor, json.dumps(descriptors[name]).encode())
        selected = self.commit(self.root)
        companions = {}
        for name, configuration in frozen.items():
            tree = prepare_tree(self.root, selected, configuration)
            companions[name] = self.git(self.root, "commit-tree", tree, "-p", selected,
                                        "-m", "test(tuple): record private companion control")
        for name, descriptor in [("legacy", legacy.DESCRIPTOR), ("pre-extension", pre_extension.DESCRIPTOR)]:
            descriptors[name]["sourceRevision"] = companions[name]
            if name == "pre-extension":
                descriptors[name]["packageReaderSourceRevision"] = companions["package-reader"]
            self.write(self.root, descriptor, json.dumps(descriptors[name]).encode())
        selected = self.commit(self.root)
        for name, verifier in [("legacy", legacy.verify), ("pre-extension", pre_extension.verify)]:
            self.git(self.root, "checkout", "-q", companions[name])
            result = verifier(self.root, selected, companions[name])
            self.assertEqual(result["sourceEquivalence"], "verified")
            self.assertFalse(result["qualificationCredit"])
            self.git(self.root, "checkout", "-q", selected)
            self.write(self.root, "src/production.txt", b"changed after companion generation\n")
            changed = self.commit(self.root)
            self.git(self.root, "checkout", "-q", companions[name])
            with self.assertRaisesRegex(ValueError, "Production"):
                verifier(self.root, changed, companions[name])
            self.git(self.root, "checkout", "-q", selected)

    def test_corrupted_frozen_metadata_cannot_create_a_companion_tree(self):
        frozen = self.frozen_anchor("legacy")
        frozen["hashes"]["eng/builds-tuple.json"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "frozen body"):
            prepare_tree(self.root, self.source, frozen)


if __name__ == "__main__":
    unittest.main()
