"""Offline enrollment policy tests with real, disposable Git repositories."""

import contextlib
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import prepare


PROJECT = Path(__file__).resolve().parents[2]
TIMESHEETS = PROJECT / prepare.PIN


def git(root, *args):
    subprocess.run(["git", "-c", "core.commitGraph=false", "-C", str(root), *args],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True)


def init(root):
    git(root, "init", "--quiet")
    git(root, "config", "user.name", "Offline fixture")
    git(root, "config", "user.email", "fixture@example.invalid")


class PrepareTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.platform = self.base / "platform"
        self.source = self.base / "timesheets"
        self.output = self.base / "plan.json"
        self.platform.mkdir()
        self.source.mkdir()
        for relative in prepare.SOURCE_FILES:
            destination = self.source / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(TIMESHEETS / relative, destination)
        init(self.source)
        git(self.source, "add", ".")
        git(self.source, "commit", "--quiet", "-m", "fixture")
        for relative in prepare.PLATFORM_FILES:
            destination = self.platform / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(PROJECT / relative, destination)
        init(self.platform)
        git(self.platform, "add", ".")
        self.direct = self.platform / prepare.PIN
        shutil.copytree(self.source, self.direct)
        git(self.platform, "update-index", "--add", "--cacheinfo",
            f"160000,{prepare.revision(self.source)},{prepare.PIN}")
        git(self.platform, "commit", "--quiet", "-m", "fixture")

    def run_prepare(self):
        return prepare.prepare(self.platform, self.source, self.output)

    def refused(self):
        with self.assertRaises(prepare.PreparationRefused):
            self.run_prepare()
        self.assertFalse(self.output.exists())

    def test_valid_preparation_is_reproducible_and_blocked(self):
        first = self.run_prepare()
        second = self.base / "second.json"
        prepare.prepare(self.platform, self.source, second)
        self.assertEqual(self.output.read_bytes(), second.read_bytes())
        self.assertEqual(first["candidate"]["revision"], prepare.revision(self.source))
        self.assertFalse(first["deploymentAuthorized"])
        self.assertFalse(first["activationAuthorized"])
        self.assertEqual(first["acceptance"], "FAIL")
        self.assertFalse(first["liveEvidenceCollected"])
        self.assertIsNone(first["retainedRelease"]["imageDigest"])
        self.assertEqual(first["proposedTarget"]["applicationNamespace"], "hexalith-staging")
        self.assertEqual(first["proposedTarget"]["dataNamespace"], "hexalith-staging-data")
        self.assertEqual(first["listeners"]["internal"]["port"], 8081)
        self.assertFalse(first["listeners"]["internal"]["routePublished"])
        self.assertIsNone(first["componentBindings"][0]["configurationKey"])
        self.assertTrue(first["expectedClusterEvidence"]["freshNativeBindingRequired"])
        self.assertEqual(first["expectedClusterEvidence"]["sourceSha256"], prepare.OWNER_REPORT_SHA256)
        self.assertEqual(first["generator"]["sha256"], prepare.EXECUTING_SCRIPT_SHA256)
        self.assertTrue(first["directSubmodule"]["observedCheckoutClean"])
        self.assertEqual(len([p for p in first["platform"]["inputsSha256"] if p.endswith("/module/hexalith.module-manifest.v2.json")]), 3)
        self.assertEqual(len([p for p in first["platform"]["inputsSha256"] if "/Declarations/" in p]), 3)
        self.assertIn("/api/timesheets/magic-links/confirm/submit", {r["path"] for r in first["publicRouteAllowlist"]})
        self.assertIn("/api/timesheets/magic-links/adjust/submit", {r["path"] for r in first["publicRouteAllowlist"]})
        self.assertTrue(all(p["status"] == "missing" for p in first["prerequisites"]))
        hash_value = first.pop("preparationSha256")
        self.assertEqual(hash_value, prepare.sha256(prepare.canonical(first)))

    def test_missing_source_refuses_without_output(self):
        shutil.rmtree(self.source)
        self.refused()

    def test_missing_committed_required_file_refuses(self):
        git(self.source, "rm", prepare.SOURCE_FILES[0])
        git(self.source, "commit", "--quiet", "-m", "remove input")
        self.refused()

    def test_dirty_tracked_source_refuses(self):
        with (self.source / prepare.SOURCE_FILES[0]).open("a") as stream:
            stream.write("\n<!-- modified -->\n")
        self.refused()

    def test_untracked_source_refuses(self):
        (self.source / "uncommitted.txt").write_text("uncommitted input")
        self.refused()

    def test_wrong_repository_root_refuses(self):
        self.source = self.source / "src"
        self.refused()

    def test_missing_platform_input_refuses(self):
        (self.platform / prepare.SPINE).unlink()
        self.refused()

    def test_original_root_symlink_aliases_refuse(self):
        for field in ("source", "platform"):
            with self.subTest(field=field):
                original = getattr(self, field)
                alias = self.base / (field + "-alias")
                alias.symlink_to(original, target_is_directory=True)
                setattr(self, field, alias)
                self.refused()
                setattr(self, field, original)

    def test_linked_worktree_metadata_is_protected(self):
        for field in ("source", "platform"):
            with self.subTest(field=field):
                original = getattr(self, field)
                linked = self.base / (field + "-linked")
                git(original, "worktree", "add", "--detach", str(linked), "HEAD")
                setattr(self, field, linked)
                actual, common = prepare.git_metadata(linked)
                self.assertFalse(common.is_relative_to(linked))
                for directory in (actual, common):
                    self.output = directory / "refused-plan.json"
                    self.refused()
                setattr(self, field, original)

    def test_alternate_platform_generator_must_match_executing_bytes(self):
        (self.platform / prepare.GENERATOR).write_text("# different executing implementation\n")
        self.refused()

    def test_changed_committed_dated_owner_report_refuses(self):
        with (self.source / prepare.OWNER_REPORT).open("a") as stream:
            stream.write("\nDifferent observation report.\n")
        git(self.source, "add", prepare.OWNER_REPORT)
        git(self.source, "commit", "--quiet", "-m", "report evolves")
        self.refused()

    def test_final_platform_input_recheck_refuses_changes(self):
        original = prepare.source_identity
        first = True

        def changed_platform(root):
            nonlocal first
            identity = original(root)
            if first:
                first = False
                with (self.platform / prepare.STATUS).open("a") as stream:
                    stream.write("\n# concurrent Platform input change\n")
            return identity

        with patch.object(prepare, "source_identity", side_effect=changed_platform):
            self.refused()

    def test_source_changes_during_preparation_refuse_without_output(self):
        original = prepare.source_identity
        first = True

        def changing_source(root):
            nonlocal first
            identity = original(root)
            if first:
                first = False
                with (self.source / prepare.SOURCE_FILES[0]).open("a") as stream:
                    stream.write("\n<!-- concurrent source edit -->\n")
            return identity

        with patch.object(prepare, "source_identity", side_effect=changing_source):
            self.refused()

    def test_output_cannot_dirty_candidate_or_nested_references(self):
        for root in (self.source, self.direct):
            with self.subTest(root=root.name):
                self.output = root / "new-plan.json"
                self.refused()
        prepare.clean_source(self.source)
        prepare.clean_source(self.direct)

    def test_existing_destination_is_preserved(self):
        self.output.write_bytes(b"existing evidence\n")
        with self.assertRaises(prepare.PreparationRefused):
            self.run_prepare()
        self.assertEqual(self.output.read_bytes(), b"existing evidence\n")

    def test_symlink_destination_is_preserved(self):
        target = self.base / "retained.json"
        target.write_bytes(b"retain")
        self.output.symlink_to(target)
        with self.assertRaises(prepare.PreparationRefused):
            self.run_prepare()
        self.assertEqual(target.read_bytes(), b"retain")

    def test_exclusive_creation_refuses_a_racing_writer(self):
        original = prepare.build_plan

        def race(*args):
            plan = original(*args)
            self.output.write_bytes(b"other writer")
            return plan

        with patch.object(prepare, "build_plan", side_effect=race):
            with self.assertRaises(prepare.PreparationRefused):
                self.run_prepare()
        self.assertEqual(self.output.read_bytes(), b"other writer")
        self.assertFalse(list(self.base.glob(".timesheets-plan-*.tmp")))

    def test_output_parent_replacement_cannot_redirect_into_protected_trees(self):
        original = prepare.build_plan
        for target in (self.source, self.direct, self.platform / ".git"):
            with self.subTest(target=target.name):
                parent = self.base / ("output-" + str(len(list(self.base.glob("output-*")))))
                parent.mkdir()
                saved = parent.with_name(parent.name + "-retained")
                self.output = parent / "new-plan.json"

                def replace_parent(*args):
                    plan = original(*args)
                    parent.rename(saved)
                    parent.symlink_to(target, target_is_directory=True)
                    return plan

                with patch.object(prepare, "build_plan", side_effect=replace_parent):
                    self.refused()
                self.assertFalse((target / "new-plan.json").exists())
                self.assertEqual(list(saved.iterdir()), [])

    def test_write_or_close_failure_leaves_no_plan_or_owned_temp(self):
        original = os.fdopen

        class FailingStream:
            def __init__(self, stream, failure):
                self.stream, self.failure = stream, failure

            def __enter__(self):
                return self

            def __exit__(self, *args):
                result = self.stream.__exit__(*args)
                if self.failure == "close":
                    raise OSError("injected close failure")
                return result

            def write(self, data):
                if self.failure == "write":
                    self.stream.write(data[:8])
                    raise OSError("injected write failure")
                return self.stream.write(data)

            def __getattr__(self, name):
                return getattr(self.stream, name)

        for failure in ("write", "close"):
            with self.subTest(failure=failure):
                with patch.object(os, "fdopen", side_effect=lambda *a, **k: FailingStream(original(*a, **k), failure)):
                    self.refused()
                self.assertFalse(list(self.base.glob(".timesheets-plan-*.tmp")))

    def test_source_drift_is_an_explicit_enrollment_blocker(self):
        (self.source / "new-source.txt").write_text("committed evolution")
        git(self.source, "add", ".")
        git(self.source, "commit", "--quiet", "-m", "candidate evolves")
        pin_before = prepare.gitlink(self.platform, index=True)
        plan = self.run_prepare()
        self.assertIn("source-enrollment-drift", {p["id"] for p in plan["prerequisites"]})
        self.assertEqual(plan["directSubmodule"]["indexRevision"], pin_before)
        self.assertEqual(prepare.gitlink(self.platform, index=True), pin_before)
        self.assertNotEqual(plan["candidate"]["revision"], pin_before)

    def test_direct_checkout_drift_is_distinct_from_declared_pin(self):
        (self.direct / "checkout-only.txt").write_text("external checkout")
        git(self.direct, "add", ".")
        git(self.direct, "commit", "--quiet", "-m", "checkout evolves")
        plan = self.run_prepare()
        direct = plan["directSubmodule"]
        self.assertEqual(direct["declaredRevision"], direct["indexRevision"])
        self.assertNotEqual(direct["observedCheckoutRevision"], direct["indexRevision"])
        self.assertIn("source-enrollment-drift", {p["id"] for p in plan["prerequisites"]})

    def test_dirty_direct_checkout_is_preserved_and_blocked_at_same_head(self):
        path = self.direct / prepare.SOURCE_FILES[0]
        expected = path.read_bytes() + b"\n<!-- direct local edit -->\n"
        path.write_bytes(expected)
        plan = self.run_prepare()
        self.assertEqual(plan["directSubmodule"]["observedCheckoutRevision"], plan["directSubmodule"]["indexRevision"])
        self.assertFalse(plan["directSubmodule"]["observedCheckoutClean"])
        self.assertIn("dirty-direct-checkout", {p["id"] for p in plan["prerequisites"]})
        self.assertEqual(path.read_bytes(), expected)

    def test_direct_checkout_cleanliness_drift_during_preparation_refuses(self):
        original = prepare.checkout_snapshot
        first = True

        def changed_direct(root):
            nonlocal first
            snapshot = original(root)
            if first:
                first = False
                with (self.direct / prepare.SOURCE_FILES[0]).open("a") as stream:
                    stream.write("\n<!-- concurrent direct edit -->\n")
            return snapshot

        with patch.object(prepare, "checkout_snapshot", side_effect=changed_direct):
            self.refused()

    def test_unknown_dirty_direct_files_are_blocked_without_reading_contents(self):
        private = self.direct / "unknown-private-data.txt"
        private.write_text("Disposable private-data fixture")
        original = prepare.read_file

        def allowlisted_only(root, relative):
            self.assertNotEqual(Path(root) / relative, private)
            return original(root, relative)

        with patch.object(prepare, "read_file", side_effect=allowlisted_only):
            plan = self.run_prepare()
        self.assertFalse(plan["directSubmodule"]["observedCheckoutClean"])
        self.assertIn("dirty-direct-checkout", {p["id"] for p in plan["prerequisites"]})

    def test_known_dirty_direct_bytes_changing_at_same_status_refuse(self):
        path = self.direct / prepare.SOURCE_FILES[0]
        with path.open("a") as stream:
            stream.write("\n<!-- initial direct edit -->\n")
        original = prepare.checkout_snapshot
        first = True

        def changed_known_source(root):
            nonlocal first
            snapshot = original(root)
            if first:
                first = False
                with path.open("a") as stream:
                    stream.write("\n<!-- different dirty bytes -->\n")
            return snapshot

        with patch.object(prepare, "checkout_snapshot", side_effect=changed_known_source):
            self.refused()

    def test_declaration_surface_and_dependency_drift_refuses(self):
        path = self.platform / prepare.DECLARATION
        original = path.read_bytes()
        for change in ("public-internal", "remove-dependency", "second-server"):
            with self.subTest(change=change):
                document = json.loads(original)
                module = document["modules"][0]
                if change == "public-internal":
                    module["surfaces"]["interfaces"][-1]["exposure"] = "public"
                elif change == "remove-dependency":
                    module["identity"]["dependencies"].remove("memories")
                else:
                    module["identity"]["servers"].append(module["identity"]["servers"][0])
                path.write_bytes(prepare.canonical(document))
                self.refused()

    def test_surface_authorization_operations_and_callers_cannot_be_weakened(self):
        path = self.platform / prepare.DECLARATION
        original = path.read_bytes()
        for change in ("anonymous-public-management", "anonymous-internal", "anonymous-operation", "empty-internal-callers"):
            with self.subTest(change=change):
                document = json.loads(original)
                surfaces = document["modules"][0]["surfaces"]["interfaces"]
                internal = next(s for s in surfaces if s["name"] == "process")
                if change == "anonymous-public-management":
                    next(s for s in surfaces if s["name"] == "capability-management")["authorization"]["policy"] = "anonymous"
                elif change == "anonymous-internal":
                    internal["authorization"]["policy"] = "anonymous"
                elif change == "anonymous-operation":
                    internal["operations"][0]["authorizationPolicy"] = "anonymous"
                else:
                    internal["callers"] = []
                path.write_bytes(prepare.canonical(document))
                self.refused()

    def test_huge_integer_and_deep_json_have_bounded_cli_refusals(self):
        path = self.platform / prepare.DECLARATION
        for data in (b'{"unused":' + b"9" * 5000 + b"}", b"[" * 2000 + b"0" + b"]" * 2000):
            with self.subTest(size=len(data)):
                path.write_bytes(data)
                output = io.StringIO()
                with contextlib.redirect_stdout(output):
                    code = prepare.main(["--platform-root", str(self.platform), "--source-root", str(self.source),
                                         "--output", str(self.output)])
                self.assertEqual(code, 1)
                self.assertEqual(output.getvalue(), "Preparation refused: invalid, changed or unavailable input/output. No deployment was performed.\n")
                self.assertFalse(self.output.exists())

    def test_locally_authored_receipts_cannot_open_gates(self):
        # There is no input path for authority records. A local adjacent claim is inert.
        (self.base / "receipt.json").write_text('{"accepted":true,"deploymentAuthorized":true}')
        plan = self.run_prepare()
        self.assertFalse(plan["deploymentAuthorized"])
        self.assertEqual(plan["acceptance"], "FAIL")
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as error:
            prepare.main(["--source-root", str(self.source), "--output", str(self.base / "override.json"),
                          "--receipt", str(self.base / "receipt.json"), "--grant", "operator", "--deploy"])
        self.assertEqual(error.exception.code, 2)
        self.assertFalse((self.base / "override.json").exists())

    def test_git_failures_emit_bounded_diagnostics(self):
        shutil.rmtree(self.source / ".git")
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            code = prepare.main(["--platform-root", str(self.platform), "--source-root", str(self.source),
                                 "--output", str(self.output)])
        self.assertEqual(code, 1)
        self.assertEqual(output.getvalue(), "Preparation refused: invalid, changed or unavailable input/output. No deployment was performed.\n")
        self.assertFalse(self.output.exists())

    def test_git_read_is_bounded_and_ignores_inherited_overrides(self):
        original = subprocess.run
        commands = []

        def observe(command, **kwargs):
            commands.append(command)
            self.assertEqual(kwargs["timeout"], 15)
            self.assertNotIn("GIT_DIR", kwargs["env"])
            self.assertEqual(kwargs["env"]["GIT_OPTIONAL_LOCKS"], "0")
            return original(command, **kwargs)

        with patch.dict(os.environ, {"GIT_DIR": "/untrusted"}), patch.object(subprocess, "run", side_effect=observe):
            self.run_prepare()
        allowed = {"rev-parse", "status", "show", "ls-tree", "ls-files"}
        self.assertTrue(commands)
        self.assertTrue(all(command[9] in allowed for command in commands))


if __name__ == "__main__":
    unittest.main()
