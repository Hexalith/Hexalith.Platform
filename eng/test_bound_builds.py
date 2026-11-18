"""Exercise exact producer acquisition with real repositories and shallow boundaries."""

import hashlib
import json
import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from bound_builds import materialize, verify_bound_checkout
import prepare_builds_artifacts


def git(root, *arguments):
    environment = {key: value for key, value in os.environ.items() if not key.upper().startswith("GIT_")}
    return subprocess.check_output(["git", "-C", str(root), *arguments], env=environment, text=True).strip()


def commit(root):
    git(root, "add", ".")
    git(
        root,
        "-c",
        "user.name=Fixture",
        "-c",
        "user.email=fixture@example.invalid",
        "-c",
        "commit.gpgsign=false",
        "-c",
        "core.hooksPath=/dev/null",
        "commit",
        "--quiet",
        "-m",
        "build: record producer fixture",
    )
    return git(root, "rev-parse", "HEAD")


class BoundBuildsTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.platform = self.root / "Hexalith.Platform"
        (self.platform / "eng").mkdir(parents=True)
        self.builds = self.root / "Hexalith.Builds"
        (self.builds / "Props").mkdir(parents=True)
        git(self.builds, "init", "--quiet")
        self.catalog = self.builds / "Props/Directory.Packages.props"
        (self.builds / ".gitattributes").write_text("Props/Directory.Packages.props text eol=crlf\n")
        self.catalog.write_text("<Project />\n")
        (self.builds / "Hexalith.Build.props").write_text("<Project />\n")
        self.revision = commit(self.builds)
        self.catalog_sha256 = hashlib.sha256(self.catalog.read_bytes()).hexdigest()
        self.binding = {"schemaVersion": 2, "version": "6.0.5", "releaseTag": "v6.0.5"}
        self.binding_path = self.platform / "eng/builds-tuple.json"
        self.binding_path.write_text(json.dumps(self.binding))
        # The recorded Platform gitlink is the producer authority; the source checkout has moved on.
        git(self.platform, "init", "--quiet")
        (self.platform / "references/Hexalith.Builds").mkdir(parents=True)
        self.record_gitlink(self.revision)
        self.catalog.write_text("<Project><!-- later catalog --></Project>\n")
        self.current = commit(self.builds)

    def record_gitlink(self, revision):
        git(self.platform, "update-index", "--add", "--cacheinfo", "160000", revision, "references/Hexalith.Builds")
        return commit(self.platform)

    def test_newer_dirty_workspace_is_preserved_and_private_catalog_drift_refuses(self):
        self.catalog.write_text(self.catalog.read_text() + "<!-- local work -->\n")
        before = self.catalog.read_bytes()
        context = materialize(self.platform, self.root / "private")
        selected = Path(context["buildsRoot"])
        self.assertEqual(self.revision, git(selected, "rev-parse", "HEAD"))
        self.assertEqual(self.catalog_sha256, hashlib.sha256(Path(context["catalog"]).read_bytes()).hexdigest())
        self.assertEqual(context["catalogSha256"], self.catalog_sha256)
        self.assertEqual(context["sourceRevision"], self.revision)
        self.assertEqual(self.current, git(self.builds, "rev-parse", "HEAD"))
        self.assertEqual(before, self.catalog.read_bytes())
        self.assertFalse((self.platform / "references/Hexalith.Builds/.git").exists())
        Path(context["catalog"]).write_text("changed private catalog")
        with self.assertRaisesRegex(ValueError, "checkout comparison failed"):
            verify_bound_checkout(self.platform, selected)
        with self.assertRaisesRegex(ValueError, "recorded Builds gitlink"):
            verify_bound_checkout(self.platform, self.builds)

    def test_nested_paths_and_modified_supporting_inputs_cannot_supply_the_bound_context(self):
        context = materialize(self.platform, self.root / "private")
        selected = Path(context["buildsRoot"])
        nested = selected / "nested"
        nested.mkdir()
        with self.assertRaisesRegex(ValueError, "own initialized physical checkout"):
            verify_bound_checkout(self.platform, nested)
        (selected / "foreign.props").write_text("<Project />")
        with self.assertRaisesRegex(ValueError, "modified or untracked"):
            verify_bound_checkout(self.platform, selected)

    def test_shallow_source_fetches_only_into_the_private_checkout(self):
        upstream = self.root / "upstream"
        self.builds.rename(upstream)
        subprocess.run(["git", "clone", "--quiet", "--depth=1", upstream.as_uri(), str(self.builds)], check=True)
        context = materialize(self.platform, self.root / "private")
        self.assertEqual(self.revision, git(Path(context["buildsRoot"]), "rev-parse", "HEAD"))
        self.assertEqual("true", git(self.builds, "rev-parse", "--is-shallow-repository"))
        self.assertEqual(self.current, git(self.builds, "rev-parse", "HEAD"))
        absent = subprocess.run(
            ["git", "-C", str(self.builds), "cat-file", "-e", self.revision + "^{commit}"], capture_output=True
        )
        self.assertNotEqual(0, absent.returncode)

    def test_hidden_index_flags_cannot_bless_modified_supporting_build_inputs(self):
        for flag in ("--assume-unchanged", "--skip-worktree"):
            context = materialize(self.platform, self.root / flag.removeprefix("--"))
            selected = Path(context["buildsRoot"])
            git(selected, "update-index", flag, "Hexalith.Build.props")
            (selected / "Hexalith.Build.props").write_text("modified supporting build props")
            self.assertEqual("", git(selected, "status", "--porcelain"))
            with self.subTest(flag=flag), self.assertRaisesRegex(ValueError, "assume-unchanged or skip-worktree"):
                verify_bound_checkout(self.platform, selected)

    def test_unknown_gitlink_or_invalid_binding_refuses_without_changing_the_source(self):
        self.record_gitlink("e" * 40)
        with self.assertRaises(subprocess.CalledProcessError):
            materialize(self.platform, self.root / "unknown")
        self.record_gitlink(self.revision)
        self.binding_path.write_text(json.dumps({**self.binding, "sourceRevision": self.revision}))
        with self.assertRaisesRegex(ValueError, "schema 2"):
            materialize(self.platform, self.root / "invalid")
        self.assertEqual(self.current, git(self.builds, "rev-parse", "HEAD"))

    def test_ambient_git_redirection_cannot_replace_the_physical_source(self):
        with patch.dict(
            os.environ,
            {
                "GIT_DIR": str(self.builds / ".git"),
                "GIT_WORK_TREE": str(self.builds),
                "GIT_INDEX_FILE": str(self.root / "foreign-index"),
            },
        ):
            context = materialize(self.platform, self.root / "private")
            self.assertEqual(context, verify_bound_checkout(self.platform, Path(context["buildsRoot"])))
        self.assertFalse((self.root / "foreign-index").exists())

    def recorded_owner(self):
        """Create a clean owner clone whose recorded gitlink selects the producer."""
        owner_revision = git(self.platform, "rev-parse", "HEAD")
        owner = self.root / "private/Hexalith.Platform"
        subprocess.run(["git", "-c", "submodule.recurse=false", "clone", "--quiet", "--shared",
                        str(self.platform), str(owner)], check=True)
        producer = self.root / "private/Hexalith.Builds"
        materialize(owner, producer, self.builds)
        return owner, producer, owner_revision

    def test_recorded_artifacts_bind_the_owner_gitlink_as_the_producer(self):
        owner, producer, revision = self.recorded_owner()
        with patch.object(prepare_builds_artifacts, "ROOT", owner):
            context = prepare_builds_artifacts.recorded_preparation_context()
            prepare_builds_artifacts.require_unchanged_recorded_context(context)
        self.assertEqual(context["platformRevision"], revision)
        self.assertEqual(context["platformRecordedBuildsRevision"], self.revision)
        self.assertEqual(context["boundProducerContext"]["sourceRevision"], self.revision)
        self.assertEqual(context["boundProducerContext"]["buildsRoot"], str(producer))
        self.assertFalse((owner / "references/Hexalith.Builds/.git").exists())
        self.assertEqual(git(self.builds, "rev-parse", "HEAD"), self.current)

    def test_normal_recorded_preparation_materializes_raw_bytes_and_rechecks_that_exact_producer(self):
        owner, producer, _ = self.recorded_owner()
        # Restore the normal CRLF checkout, which cannot itself supply exact catalog bytes.
        (producer / ".git/info/attributes").unlink()
        (producer / "Props/Directory.Packages.props").unlink()
        git(producer, "checkout", "--", "Props/Directory.Packages.props")
        before = (producer / "Props/Directory.Packages.props").read_bytes()
        self.assertIn(b"\r\n", before)
        with patch.object(prepare_builds_artifacts, "ROOT", owner):
            with self.assertRaisesRegex(ValueError, "working bytes differ"):
                prepare_builds_artifacts.recorded_preparation_context()
            with tempfile.TemporaryDirectory() as temporary:
                exact = materialize(owner, Path(temporary) / "producer", producer)
                context = prepare_builds_artifacts.recorded_preparation_context(builds_root=exact["buildsRoot"])
                prepare_builds_artifacts.require_unchanged_recorded_context(context)
                # The end guard must inspect the same private source, not resolve the CRLF sibling again.
                (Path(exact["buildsRoot"]) / "Hexalith.Build.props").write_text("changed producer")
                with self.assertRaisesRegex(ValueError, "modified or untracked"):
                    prepare_builds_artifacts.require_unchanged_recorded_context(context)
            self.assertFalse(Path(exact["buildsRoot"]).exists())
        self.assertEqual(before, (producer / "Props/Directory.Packages.props").read_bytes())
        self.assertEqual("", git(producer, "status", "--porcelain"))

    def test_recorded_artifacts_refuse_wrong_dirty_or_hidden_bound_inputs(self):
        for fault in ("wrong-revision", "dirty-catalog", "assume-unchanged", "skip-worktree"):
            with self.subTest(fault=fault):
                # Each subcase has its own materialized physical producer.
                owner, producer, _ = self.recorded_owner()
                if fault == "wrong-revision":
                    git(producer, "checkout", "--quiet", "--detach", self.current)
                elif fault == "dirty-catalog":
                    (producer / "Props/Directory.Packages.props").write_text("changed catalog")
                else:
                    git(producer, "update-index", "--" + fault, "Hexalith.Build.props")
                    (producer / "Hexalith.Build.props").write_text("hidden supporting input")
                    self.assertEqual("", git(producer, "status", "--porcelain"))
                with patch.object(prepare_builds_artifacts, "ROOT", owner), self.assertRaises(ValueError):
                    prepare_builds_artifacts.recorded_preparation_context()
                # Remove only this disposable fixture so the next subcase starts from its real Git bodies.
                shutil.rmtree(owner.parent)

    def test_recorded_artifacts_refuse_dirty_owner_and_hidden_owner_inputs(self):
        owner, _, _ = self.recorded_owner()
        binding = owner / "eng/builds-tuple.json"
        before = binding.read_bytes()
        for hidden in (False, True):
            if hidden:
                git(owner, "update-index", "--assume-unchanged", "eng/builds-tuple.json")
            binding.write_bytes(before + b" ")
            with self.subTest(hidden=hidden), patch.object(prepare_builds_artifacts, "ROOT", owner), self.assertRaisesRegex(ValueError, "clean recorded Platform"):
                prepare_builds_artifacts.recorded_preparation_context()
            binding.write_bytes(before)

    def test_recorded_artifacts_recheck_clean_owner_head_changes(self):
        owner, _, _ = self.recorded_owner()
        with patch.object(prepare_builds_artifacts, "ROOT", owner):
            context = prepare_builds_artifacts.recorded_preparation_context()
            (owner / "future.txt").write_text("later clean owner revision")
            advanced = commit(owner)
            self.assertEqual("", git(owner, "status", "--porcelain"))
            with self.assertRaisesRegex(ValueError, "changed during artifact preparation"):
                prepare_builds_artifacts.require_unchanged_recorded_context(context)
        self.assertNotEqual(context["platformRevision"], advanced)

    def test_recorded_artifacts_require_a_real_recorded_catalog_gitlink(self):
        owner, _, _ = self.recorded_owner()
        git(owner, "update-index", "--force-remove", "references/Hexalith.Builds")
        (owner / "references/Hexalith.Builds").rmdir()
        (owner / "references/Hexalith.Builds").write_text("ordinary file cannot be a recorded catalog")
        commit(owner)
        with patch.object(prepare_builds_artifacts, "ROOT", owner), self.assertRaisesRegex(ValueError, "actual Builds catalog gitlink"):
            prepare_builds_artifacts.recorded_preparation_context()


if __name__ == "__main__":
    unittest.main()
