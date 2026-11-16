"""Exercise exact producer acquisition with real repositories and shallow boundaries."""

import hashlib
import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from bound_builds import materialize, verify_bound_checkout


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
        self.binding = {
            "schemaVersion": 1,
            "candidateAcceptance": False,
            "producer": {
                "repository": "https://github.com/Hexalith/Hexalith.Builds.git",
                "sourceRevision": self.revision,
                "catalog": {
                    "path": "Props/Directory.Packages.props",
                    "blob": git(self.builds, "rev-parse", "HEAD:Props/Directory.Packages.props"),
                    "sha256": hashlib.sha256(self.catalog.read_bytes()).hexdigest(),
                },
            },
        }
        self.binding_path = self.platform / "eng/builds-tuple.json"
        self.binding_path.write_text(json.dumps(self.binding))
        self.catalog.write_text("<Project><!-- later catalog --></Project>\n")
        self.current = commit(self.builds)

    def test_newer_dirty_workspace_is_preserved_and_private_catalog_drift_refuses(self):
        self.catalog.write_text(self.catalog.read_text() + "<!-- local work -->\n")
        before = self.catalog.read_bytes()
        context = materialize(self.platform, self.root / "private")
        selected = Path(context["buildsRoot"])
        self.assertEqual(self.revision, git(selected, "rev-parse", "HEAD"))
        self.assertEqual(
            self.binding["producer"]["catalog"]["sha256"],
            hashlib.sha256(Path(context["catalog"]).read_bytes()).hexdigest(),
        )
        self.assertEqual(self.current, git(self.builds, "rev-parse", "HEAD"))
        self.assertEqual(before, self.catalog.read_bytes())
        self.assertFalse((self.platform / "references/Hexalith.Builds/.git").exists())
        Path(context["catalog"]).write_text("changed private catalog")
        with self.assertRaisesRegex(ValueError, "checkout comparison failed"):
            verify_bound_checkout(self.platform, selected)
        with self.assertRaisesRegex(ValueError, "bound producer revision"):
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

    def test_wrong_authenticated_catalog_refuses_without_changing_the_source(self):
        for key, value in (("blob", "e" * 40), ("sha256", "e" * 64)):
            original = self.binding["producer"]["catalog"][key]
            self.binding["producer"]["catalog"][key] = value
            self.binding_path.write_text(json.dumps(self.binding))
            with self.subTest(key=key), self.assertRaisesRegex(ValueError, "catalog (blob|SHA-256) differs"):
                materialize(self.platform, self.root / key)
            self.binding["producer"]["catalog"][key] = original
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


if __name__ == "__main__":
    unittest.main()
