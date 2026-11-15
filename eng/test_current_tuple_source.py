"""Real isolated Git controls for current sources, released packages and candidate preparation."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

import current_tuple_source as current
from current_tuple_source import PROVENANCE


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

    def test_decode_rejects_duplicate_fields_at_every_depth(self):
        for raw in (b'{"schema":1,"schema":1}', b'{"owner":{"commit":"a","commit":"a"}}',
                    b'{"owners":[{"version":"1.0.0","version":"1.0.0"}]}'):
            with self.subTest(raw=raw), self.assertRaisesRegex(ValueError, "Duplicate current tuple metadata field"):
                current.decode(raw)

    def test_committed_binding_and_provenance_reject_duplicate_fields(self):
        # Identical duplicate values would otherwise survive all semantic identity checks.
        for document, depth in ((current.BINDING, "root"), (current.BINDING, "owner"),
                                (PROVENANCE, "root"), (PROVENANCE, "owner")):
            binding = json.loads(json.dumps(self.binding))
            provenance = json.dumps(self.provenance)
            raw = json.dumps(binding) if document == current.BINDING else provenance
            if depth == "root":
                key, value = next(iter((binding if document == current.BINDING else self.provenance).items()))
                raw = raw[:-1] + "," + json.dumps(key) + ":" + json.dumps(value) + "}"
            else:
                key = "sourceRevision" if document == current.BINDING else "repository"
                value = binding["ownerSources"]["eventstore"][key] if document == current.BINDING else self.provenance["eventstore"][key]
                field = json.dumps(key) + ": " + json.dumps(value)
                raw = raw.replace(field, field + ", " + field, 1)
            if document == PROVENANCE:
                provenance = raw
                binding["packageProvenanceSha256"] = hashlib.sha256(raw.encode()).hexdigest()
            self.write(self.root, PROVENANCE, provenance.encode())
            self.write(self.root, current.BINDING, (raw if document == current.BINDING else json.dumps(binding)).encode())
            changed = self.commit(self.root)
            with self.subTest(document=document, depth=depth), self.assertRaisesRegex(ValueError, "Duplicate current tuple metadata field"):
                current.read_selection(self.root, changed)

    def test_current_checkout_and_actual_release_are_distinct_authenticated_identities(self):
        result = current.verify_checkout(self.root, self.source)
        self.assertFalse(result["qualificationCredit"])
        for name, item in self.provenance.items():
            self.assertNotEqual(item["commit"], self.binding["ownerSources"][name]["sourceRevision"])
        current.verify_package_release_identities(self.root, self.source)

    def test_depth_one_platform_checkout_authenticates_independent_owner_releases(self):
        prior = self.git(self.root, "rev-parse", self.source + "^")
        with tempfile.TemporaryDirectory(prefix="shallow-current-platform-") as directory:
            checkout = Path(directory) / "platform"
            self.git(self.root, "clone", "--depth=1", "--no-tags", self.root.as_uri(), str(checkout))
            self.assertEqual(self.git(checkout, "rev-parse", "--is-shallow-repository"), "true")
            self.assertEqual(self.git(checkout, "rev-list", "--count", "--all"), "1")
            self.assertEqual(self.git(checkout, "rev-list", "--all"), self.source)
            self.assertNotEqual(subprocess.run(["git", "cat-file", "-e", prior + "^{commit}"],
                                               cwd=checkout, capture_output=True).returncode, 0)
            for name, repository in current.OWNERS.items():
                owner = checkout / "references" / repository
                self.git(checkout, "clone", (self.root / "references" / repository).as_uri(), str(owner))
                self.git(owner, "checkout", "--detach", self.binding["ownerSources"][name]["sourceRevision"])
                self.assertFalse((owner / ".git/objects/info/alternates").exists())
            self.assertEqual(current.read_selection(checkout, self.source), (self.binding, self.provenance))
            self.assertEqual(current.create_binding(checkout, self.source), self.binding)
            self.assertFalse(current.verify_checkout(checkout, self.source)["qualificationCredit"])
            self.assertEqual(current.verify_package_release_identities(checkout, self.source)["packages"], self.provenance)
            owner = checkout / "references/Hexalith.Tenants"
            self.git(owner, "tag", "-f", "v1.0.0", self.binding["ownerSources"]["tenants"]["sourceRevision"])
            with self.assertRaisesRegex(ValueError, "actual release tag or declaration blob"):
                current.verify_checkout(checkout, self.source)
            self.git(owner, "tag", "-f", "v1.0.0", self.provenance["tenants"]["commit"])
            manifest = (owner / current.MANIFEST).read_bytes()
            self.write(owner, current.MANIFEST, manifest + b"tampered\n")
            with self.assertRaisesRegex(ValueError, "checkout or declaration differs"):
                current.verify_checkout(checkout, self.source)
            self.write(owner, current.MANIFEST, manifest)
            current.verify_checkout(checkout, self.source)
            self.assertEqual(self.git(checkout, "rev-list", "--count", "--all"), "1")
            self.assertNotEqual(subprocess.run(["git", "cat-file", "-e", prior + "^{commit}"],
                                               cwd=checkout, capture_output=True).returncode, 0)

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

    def test_declared_checkout_line_endings_preserve_canonical_binding_and_tampering_refuses(self):
        owner = self.root / "references/Hexalith.Memories"
        self.write(owner, ".gitattributes", b"* text=auto eol=crlf\n")
        source = self.commit(owner)
        self.gitlink("Hexalith.Memories", source)
        unbound = self.commit(self.root)
        self.binding = current.create_binding(self.root, unbound)
        self.write(self.root, current.BINDING, json.dumps(self.binding).encode())
        selected = self.commit(self.root)
        (owner / current.MANIFEST).unlink()
        self.git(owner, "checkout-index", "--force", "--", current.MANIFEST)
        actual = (owner / current.MANIFEST).read_bytes()
        canonical = subprocess.check_output(["git", "show", source + ":" + current.MANIFEST], cwd=owner)
        self.assertIn(b"\r\n", actual)
        self.assertNotEqual(actual, canonical)
        self.assertEqual(hashlib.sha256(canonical).hexdigest(), self.binding["ownerSources"]["memories"]["declarationSha256"])
        current.verify_checkout(self.root, selected)
        self.write(owner, current.MANIFEST, canonical)
        current.verify_checkout(self.root, selected)
        self.write(owner, current.MANIFEST, actual.replace(b'"owner"', b'"tampered-owner"'))
        with self.assertRaisesRegex(ValueError, "checkout or declaration differs"):
            current.verify_checkout(self.root, selected)

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

    def test_documentation_change_needs_no_companion_or_binding_refresh(self):
        self.write(self.root, "docs/guide.md", b"candidate-only instructions\n")
        changed = self.commit(self.root)
        before = self.git(self.root, "for-each-ref", "--format=%(refname) %(objectname)")
        self.assertEqual(current.create_binding(self.root, changed), self.binding)
        current.verify_checkout(self.root, changed)
        self.assertEqual(self.git(self.root, "for-each-ref", "--format=%(refname) %(objectname)"), before)

    def test_owner_gitlink_update_only_needs_its_current_binding(self):
        owner = self.root / "references/Hexalith.Parties"
        self.write(owner, "updated-source.txt", b"new source with the same released package\n")
        source = self.commit(owner)
        self.gitlink("Hexalith.Parties", source)
        changed = self.commit(self.root)
        before = self.git(self.root, "for-each-ref", "--format=%(refname) %(objectname)")
        binding = current.create_binding(self.root, changed)
        self.assertEqual(self.git(self.root, "for-each-ref", "--format=%(refname) %(objectname)"), before)
        self.write(self.root, current.BINDING, json.dumps(binding).encode())
        changed = self.commit(self.root)
        before = self.git(self.root, "for-each-ref", "--format=%(refname) %(objectname)")
        self.assertEqual(current.create_binding(self.root, changed), binding)
        current.verify_checkout(self.root, changed)
        self.assertEqual(self.git(self.root, "for-each-ref", "--format=%(refname) %(objectname)"), before)


if __name__ == "__main__":
    unittest.main()
