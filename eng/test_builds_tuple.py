"""Binding, lock and preparation controls use temporary files and mocked reads; never create Platform commits."""
import contextlib
import copy
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import zipfile

import prepare_builds_artifacts
import run_tuple_ci
from unittest.mock import patch

from builds_tool_lock import content_hash
from builds_tuple import (ROOT, load_binding, recorded_gitlink, resolve_builds_root, tool_version_range, validate_binding,
                          validate_owner_pins, verify_catalog)

SOURCE = "0123456789abcdef0123456789abcdef01234567"  # synthetic gitlink, not a Builds release
BLOB = "679a0c342f24aa40905eb84ad274f7e6647c995e"


def binding(version="6.0.5"):
    return {"schemaVersion": 2, "version": version, "releaseTag": "v" + version}


def lock(version, raw=b"synthetic package"):
    return {"id": "Hexalith.Builds.Module.Cli", "version": version, "contentHash": content_hash(raw)}


def owner_pins(root, version, tools=None, selected=None):
    (root / ".config").mkdir(parents=True, exist_ok=True)
    (root / ".config/dotnet-tools.json").write_text(json.dumps(
        {"tools": {"hexalith.builds.module.cli": {"version": tools or version}}}))
    (root / "hexalith-platform.json").write_text(json.dumps({"toolVersionRange": selected or tool_version_range(version)}))


def synthetic_archive(path, version, framework="net11.0", tooling=b"synthetic tooling"):
    prefix = "tools/" + framework + "/any/"
    with zipfile.ZipFile(path, "w") as package:
        package.writestr(prefix + "Hexalith.Builds.Tooling.dll", tooling)
        package.writestr(prefix + "Hexalith.Builds.Module.Cli.dll", b"synthetic CLI")
        package.writestr(prefix + "g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll", b"synthetic host")
    return prefix


class BindingTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.path = self.root / "binding.json"

    def load(self, value):
        self.path.write_text(json.dumps(value))
        return load_binding(self.path)

    def test_current_binding_is_version_and_tag_only_and_matches_the_pin_files(self):
        current = load_binding()
        self.assertEqual(set(current), {"schemaVersion", "version", "releaseTag"})
        self.assertEqual(current["releaseTag"], "v" + current["version"])
        validate_owner_pins(current, ROOT)
        tools = json.loads((ROOT / ".config/dotnet-tools.json").read_bytes())["tools"]["hexalith.builds.module.cli"]
        self.assertEqual(tools["version"], current["version"])
        self.assertIs(tools["rollForward"], False)

    def test_hashes_revisions_and_release_ids_cannot_enter_the_binding(self):
        for key, value in (("sourceRevision", SOURCE), ("archiveSha256", "a" * 64), ("releaseId", 1), ("runId", 2),
                           ("artifactId", 3), ("catalog", {"blob": BLOB}), ("candidateAcceptance", False)):
            with self.subTest(key=key), self.assertRaisesRegex(ValueError, "schema 2 with exactly"):
                self.load({**binding(), key: value})
        for value in ({"schemaVersion": 1, "version": "6.0.5", "releaseTag": "v6.0.5"}, {"version": "6.0.5", "releaseTag": "v6.0.5"},
                      {"schemaVersion": 2, "version": "6.0.5"}, [], "6.0.5"):
            with self.subTest(value=value), self.assertRaisesRegex(ValueError, "schema 2 with exactly"):
                self.load(value)

    def test_only_exact_released_versions_of_the_supported_generation_are_pins(self):
        for version in ("6.0.0", "6.0.5", "6.1.0", "6.999999999.999999999"):
            with self.subTest(version=version):
                self.assertEqual(self.load(binding(version))["version"], version)
        for version in ("6.0", "06.0.5", "6.01.0", "6.0.05", "6.0.5-beta", "6.0.5+build", "6.0.5.0", " 6.0.5", "6.1000000000.0", 6):
            with self.subTest(version=version), self.assertRaisesRegex(ValueError, "exact released"):
                self.load({"schemaVersion": 2, "version": version, "releaseTag": "v" + str(version)})
        for version in ("5.2.2", "7.0.0", "4.38.3"):
            with self.subTest(version=version), self.assertRaisesRegex(ValueError, "supported major generation"):
                self.load(binding(version))

    def test_release_tag_must_name_the_version(self):
        for tag in ("6.0.5", "v6.0.6", "V6.0.5", "v6.0.5 ", None):
            with self.subTest(tag=tag), self.assertRaisesRegex(ValueError, "release tag must be v6.0.5"):
                self.load({**binding(), "releaseTag": tag})

    def test_tools_pin_binding_version_and_range_floor_are_one_decision(self):
        owner_pins(self.root, "6.0.5")
        validate_owner_pins(binding(), self.root)
        self.assertEqual(tool_version_range("6.0.5"), "[6.0.5, 7.0.0-0)")
        for tools, selected, pinned in (("6.0.6", None, "6.0.5"), (None, "[6.0.6, 7.0.0-0)", "6.0.5"), (None, None, "6.0.6"),
                                        (None, "[6.0.5, 8.0.0-0)", "6.0.5"), (None, "[6.0.5, 7.0.0)", "6.0.5")):
            owner_pins(self.root, "6.0.5", tools=tools, selected=selected)
            with self.subTest(tools=tools, selected=selected, pinned=pinned), self.assertRaisesRegex(ValueError, "differ"):
                validate_owner_pins(binding(pinned), self.root)
        (self.root / "hexalith-platform.json").write_text("{}")
        with self.assertRaisesRegex(ValueError, "unreadable"):
            validate_owner_pins(binding(), self.root)

    def test_recorded_gitlink_is_read_from_the_platform_tree(self):
        self.assertEqual(recorded_gitlink(ROOT), subprocess.check_output(
            ["git", "rev-parse", "HEAD:references/Hexalith.Builds"], cwd=ROOT, text=True).strip())
        for output, valid in (("160000 commit " + SOURCE + "\treferences/Hexalith.Builds\n", True),
                              ("100644 blob " + BLOB + "\treferences/Hexalith.Builds\n", False), ("", False),
                              ("160000 commit d1931b1\treferences/Hexalith.Builds\n", False)):
            with self.subTest(output=output), patch("builds_tuple.subprocess.check_output", return_value=output):
                if valid:
                    self.assertEqual(recorded_gitlink(self.root), SOURCE)
                else:
                    with self.assertRaisesRegex(ValueError, "actual Builds catalog gitlink"):
                        recorded_gitlink(self.root)

    def test_workspace_producer_is_selected_before_owner_nested_checkout(self):
        owner = self.root / "workspace/references/Hexalith.Platform"
        sibling = owner.parent / "Hexalith.Builds"
        for root in (sibling, owner / "references/Hexalith.Builds"):
            catalog = root / "Props/Directory.Packages.props"
            catalog.parent.mkdir(parents=True)
            catalog.write_bytes(b"synthetic catalog selection control")
        self.assertEqual(resolve_builds_root(owner), sibling)

    def test_standalone_owner_is_selected_without_a_sibling_catalog(self):
        owner = self.root / "workspace/Hexalith.Platform"
        (owner.parent / "Hexalith.Builds").mkdir(parents=True)
        self.assertEqual(resolve_builds_root(owner), owner / "references/Hexalith.Builds")


class CatalogTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)

    def test_checkout_must_be_the_gitlink_commit(self):
        with patch("builds_tuple.subprocess.check_output", side_effect=[b"wrong\n"]), \
                self.assertRaisesRegex(ValueError, "checkout differs from the recorded Builds gitlink"):
            verify_catalog(SOURCE, self.root)

    def test_real_catalog_comes_from_the_gitlink_blob_with_git_crlf_normalization(self):
        # Read the existing dependency only; never create fixture commits or change its checkout/index.
        builds = resolve_builds_root(ROOT)
        gitlink = recorded_gitlink(ROOT)

        def git(*arguments):
            return subprocess.check_output(["git", *arguments], cwd=builds)
        committed = git("show", gitlink + ":Props/Directory.Packages.props")
        catalog = builds / "Props/Directory.Packages.props"
        self.assertNotIn(b"\r\n", committed)
        self.assertEqual(catalog.read_bytes().replace(b"\r\n", b"\n"), committed)
        path, body, identity = verify_catalog(gitlink, builds)
        self.assertEqual((path, body), (catalog, committed))
        self.assertEqual(identity, {"path": "Props/Directory.Packages.props", "sourceRevision": gitlink,
                                    "blob": git("rev-parse", gitlink + ":Props/Directory.Packages.props").decode().strip(),
                                    "sha256": hashlib.sha256(committed).hexdigest()})

    def test_dirty_catalog_and_git_errors_retain_public_diff_observation(self):
        canonical = b"<Project />\n"
        public_diff = "diff --git a/Props/Directory.Packages.props b/Props/Directory.Packages.props\n-<Project />\n+<Project Changed='true' />\n"
        command = ["git", "diff", "--exit-code", SOURCE, "--", "Props/Directory.Packages.props"]
        for code, stdout, stderr in ((1, public_diff, ""), (128, "", "fatal: public catalog comparison failed")):
            with self.subTest(exitCode=code), \
                    patch("builds_tuple.subprocess.check_output", side_effect=[SOURCE.encode(), BLOB.encode(), canonical]), \
                    patch("builds_tuple.subprocess.run", return_value=subprocess.CompletedProcess(command, code, stdout, stderr)):
                with self.assertRaisesRegex(ValueError, "catalog checkout comparison failed") as failure:
                    verify_catalog(SOURCE, self.root)
                observation = json.loads(str(failure.exception).split(": ", 1)[1])
                self.assertEqual(observation, {"argv": command, "cwd": str(self.root), "exitCode": code, "stdout": stdout, "stderr": stderr})


class PreflightTests(unittest.TestCase):
    """Drive run_tuple_ci's audit path with synthetic owners; real Builds/Git reads are mocked per call."""

    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.catalog_body = b"<Project><PropertyGroup><HexalithEventStoreVersion>4.2.0</HexalithEventStoreVersion></PropertyGroup></Project>\n"

    def owner(self, name, suite_file, count):
        owner = self.root / name / "references/Hexalith.Platform"
        sibling = owner.parent / "Hexalith.Builds"
        (sibling / "Props").mkdir(parents=True)
        (sibling / "Props/Directory.Packages.props").write_bytes(self.catalog_body)
        (owner / "eng").mkdir(parents=True)
        (owner / ("eng/" + suite_file)).write_text(json.dumps({"groups": [{
            "requiredCount": count, "filter": "-class", "selectors": ["Synthetic.Control"], "collector": "current"}]}))
        (owner / "Hexalith.Platform.slnx").write_text('<Solution><Project Path="Owned.csproj" /></Solution>')
        (owner / "Owned.csproj").write_text("<Project />")
        owner_pins(owner, "6.0.5")
        return owner, sibling

    def preflight(self, stack, owner, arguments, git_reads, commands, locked):
        stack.enter_context(patch.object(sys, "argv", ["preflight", *arguments]))
        stack.enter_context(patch.object(run_tuple_ci, "ROOT", owner))
        stack.enter_context(patch.object(run_tuple_ci, "load_binding", return_value=binding()))
        stack.enter_context(patch.object(run_tuple_ci, "load_lock", return_value={"schemaVersion": 1, "packages": [locked]}))
        stack.enter_context(patch.object(run_tuple_ci, "source_state", return_value={"dirty": False}))
        stack.enter_context(patch.object(run_tuple_ci, "sdk42_collecting_cohort", return_value=None))
        stack.enter_context(patch.object(run_tuple_ci.signal, "signal"))
        stack.enter_context(patch.object(run_tuple_ci.platform, "platform", return_value="synthetic platform"))
        reads = stack.enter_context(patch.object(run_tuple_ci.subprocess, "check_output", side_effect=git_reads))
        stack.enter_context(patch.object(run_tuple_ci.subprocess, "run", side_effect=commands))
        return reads

    def test_preflight_binds_the_gitlink_catalog_and_selected_sibling_msbuild_root(self):
        owner, sibling = self.owner("workspace", "tuple-ci-sdk42-current-suites.json", 471)
        for name, checkout in (("matching", SOURCE), ("stale", "4" * 40)):
            with self.subTest(source=name), contextlib.ExitStack() as stack:
                evidence = self.root / ("preflight-" + name)
                commands = []

                def command(argv, **kwargs):
                    commands.append(argv)
                    if argv[:2] == ["git", "diff"]:
                        return subprocess.CompletedProcess(argv, 0, "", "")
                    if argv[0] == "git":
                        output = "3" * 40 if argv[-1] == "HEAD" else SOURCE
                    elif len(argv) > 1 and argv[1].endswith("prepare_builds_artifacts.py"):
                        output = str(self.root / "synthetic-verified-tool")
                    elif argv[:2] == ["dotnet", "msbuild"]:
                        output = json.dumps({"Items": {"ProjectReference": [], "Reference": []}})
                    else:
                        self.assertEqual(argv, ["dotnet", "--info"])
                        output = "synthetic dotnet info"
                    kwargs["stdout"].write(output)
                    return subprocess.CompletedProcess(argv, 0)

                # An ambient root is never the verified producer; the selected sibling is bound instead.
                stack.enter_context(patch.dict(run_tuple_ci.os.environ, {"HexalithBuildsRoot": str(self.root / "unverified-root")}))
                reads = self.preflight(stack, owner, ["--evidence", str(evidence), "--audit-only", "--suite", "current42", "--eventstore-sdk", "4.2.0"],
                                       [checkout.encode(), BLOB.encode(), self.catalog_body], command, lock("6.0.5"))
                if name == "stale":
                    with self.assertRaisesRegex(ValueError, "Builds checkout differs"):
                        run_tuple_ci.main()
                else:
                    run_tuple_ci.main()
                result = json.loads((evidence / "tuple-ci-result.json").read_bytes())
                self.assertIs(result["candidateAcceptance"], False)
                self.assertEqual(result["tupleBinding"], binding())
                self.assertEqual(result["toolLock"], lock("6.0.5"))
                self.assertTrue(all(call.kwargs["cwd"] == sibling for call in reads.call_args_list))
                if name == "stale":
                    self.assertEqual(result["status"], "failed")
                    self.assertEqual([item["name"] for item in result["commands"]], ["revision", "dotnet-info", "builds-pin"])
                else:
                    self.assertEqual(result["status"], "graph-verified")
                    self.assertEqual(result["buildsCatalog"], {"path": "Props/Directory.Packages.props", "sourceRevision": SOURCE,
                                                               "blob": BLOB, "sha256": hashlib.sha256(self.catalog_body).hexdigest()})
                    self.assertEqual((evidence / "authenticated-catalog.props").read_bytes(), self.catalog_body)
                    graphs = [argv for argv in commands if argv[:2] == ["dotnet", "msbuild"]]
                    self.assertEqual(len(graphs), 1)
                    self.assertIn("-p:HexalithBuildsRoot=" + str(sibling), graphs[0])

    def test_stale_lock_refuses_before_the_gitlink_or_catalog_is_read(self):
        owner, _ = self.owner("stale-lock", "tuple-ci-sdk42-current-suites.json", 471)
        with contextlib.ExitStack() as stack:
            evidence = self.root / "stale-lock-evidence"

            def command(argv, **kwargs):
                kwargs["stdout"].write("3" * 40 if argv[0] == "git" else "synthetic dotnet info")
                return subprocess.CompletedProcess(argv, 0)

            reads = self.preflight(stack, owner, ["--evidence", str(evidence), "--audit-only", "--suite", "current42", "--eventstore-sdk", "4.2.0"],
                                   [], command, lock("6.0.4"))
            with self.assertRaisesRegex(ValueError, "names Hexalith.Builds.Module.Cli 6.0.4, but the Builds pin is 6.0.5"):
                run_tuple_ci.main()
            reads.assert_not_called()
        result = json.loads((evidence / "tuple-ci-result.json").read_bytes())
        self.assertEqual(result["status"], "failed")
        self.assertEqual([item["name"] for item in result["commands"]], ["revision", "dotnet-info"])

    def test_artifact_reuse_reaches_graph_only_with_the_locked_unchanged_net11_closure(self):
        owner, sibling = self.owner("reuse", "tuple-ci-extension-suites.json", 9)
        host = "tools/net11.0/any/g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll"
        for fault in (None, "net10-root", "different-binding", "different-lock", "tampered-archive", "changed-extracted-host"):
            with self.subTest(fault=fault), contextlib.ExitStack() as stack:
                artifacts = self.root / ("reuse-artifacts-" + str(fault))
                artifacts.mkdir()
                archive = artifacts / "hexalith.builds.module.cli.6.0.5.nupkg"
                synthetic_archive(archive, "6.0.5")
                with zipfile.ZipFile(archive) as package:
                    package.extractall(artifacts / "published")
                locked = lock("6.0.5", archive.read_bytes())
                tool = artifacts / "published/tools/net11.0/any"
                identity = {"toolRoot": str(tool), "version": "6.0.5", "buildsRevision": SOURCE, "tupleBinding": binding(),
                            "toolLock": copy.deepcopy(locked), "qualification": "artifact identity only", "candidateAcceptance": False}
                if fault == "net10-root":
                    identity["toolRoot"] = str(artifacts / "published/tools/net10.0/any")
                elif fault == "different-binding":
                    identity["tupleBinding"] = binding("6.0.6")
                elif fault == "different-lock":
                    identity["toolLock"]["contentHash"] = content_hash(b"other")
                elif fault == "tampered-archive":
                    archive.write_bytes(archive.read_bytes() + b"tampered")
                elif fault == "changed-extracted-host":
                    (artifacts / "published" / host).write_bytes(b"changed extracted host")
                (artifacts / "artifact-identity.json").write_text(json.dumps(identity))
                evidence = self.root / ("reuse-evidence-" + str(fault))
                commands = []

                def git_read(argv, **kwargs):
                    self.assertEqual(kwargs["cwd"], sibling)
                    return {"HEAD": SOURCE.encode(), SOURCE + ":Props/Directory.Packages.props": BLOB.encode()}.get(argv[-1], self.catalog_body)

                def command(argv, **kwargs):
                    commands.append(argv)
                    if argv[:2] == ["git", "diff"]:
                        return subprocess.CompletedProcess(argv, 0, "", "")
                    if argv[0] == "git":
                        output = "3" * 40 if argv[-1] == "HEAD" else SOURCE
                    elif argv[:2] == ["dotnet", "--info"]:
                        output = "synthetic dotnet info"
                    else:
                        self.assertEqual(argv[:2], ["dotnet", "msbuild"])
                        self.assertIn("-p:HexalithBuildsPublishedToolRoot=" + str(tool), argv)
                        output = json.dumps({"Items": {"ProjectReference": [], "Reference": [
                            {"Identity": "Hexalith.Builds.Tooling", "HintPath": str(tool / "Hexalith.Builds.Tooling.dll")}]}})
                    kwargs["stdout"].write(output)
                    return subprocess.CompletedProcess(argv, 0)

                self.preflight(stack, owner, ["--evidence", str(evidence), "--audit-only", "--artifacts", str(artifacts),
                                              "--suite", "extensions", "--eventstore-sdk", "4.2.0"], git_read, command, locked)
                if fault:
                    message = {"changed-extracted-host": "Extracted artifact changed",
                               "tampered-archive": "Tool package Hexalith.Builds.Module.Cli 6.0.5 has contentHash"}.get(fault, "another tool root or tuple")
                    with self.assertRaisesRegex(ValueError, message):
                        run_tuple_ci.main()
                else:
                    run_tuple_ci.main()
                result = json.loads((evidence / "tuple-ci-result.json").read_bytes())
                graphs = [argv for argv in commands if argv[:2] == ["dotnet", "msbuild"]]
                self.assertEqual(result["status"], "failed" if fault else "graph-verified")
                self.assertIs(result["candidateAcceptance"], False)
                self.assertEqual(result["tests"], [])
                self.assertNotIn("runtimeBuildContext", result)
                if fault:
                    self.assertEqual(graphs, [])
                else:
                    self.assertEqual(result["toolRoot"], str(tool))
                    self.assertEqual(len(graphs), 1)
                    self.assertEqual(result["projectGraphPaths"], ["Owned.csproj"])

    def test_incomplete_binding_blocks_artifact_download_and_retains_terminal_preflight(self):
        path = self.root / "binding.json"
        path.write_text(json.dumps({"schemaVersion": 2, "version": None, "releaseTag": None}))
        incomplete = lambda: load_binding(path)
        output = self.root / "artifacts"
        with patch.object(sys, "argv", ["prepare", "--output", str(output), "--require-recorded"]), \
                patch.object(prepare_builds_artifacts, "load_binding", incomplete), self.assertRaisesRegex(ValueError, "exact released"):
            prepare_builds_artifacts.main()
        self.assertFalse(output.exists())
        evidence = self.root / "preflight"
        with patch.object(sys, "argv", ["preflight", "--evidence", str(evidence), "--audit-only", "--suite", "current42", "--eventstore-sdk", "4.2.0"]), \
                patch.object(run_tuple_ci.signal, "signal"), patch.object(run_tuple_ci, "load_binding", incomplete), \
                self.assertRaisesRegex(ValueError, "exact released"):
            run_tuple_ci.main()
        receipt = json.loads((evidence / "tuple-ci-result.json").read_bytes())
        self.assertEqual(receipt["status"], "failed")
        self.assertIs(receipt["candidateAcceptance"], False)
        self.assertIn("exact released", receipt["blocker"])


class ArtifactIdentityTests(unittest.TestCase):
    """AR-12 records the lock-verified archive member and the built output, then refuses any difference."""

    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        archive = root / "hexalith.builds.module.cli.6.0.5.nupkg"
        prefix = synthetic_archive(archive, "6.0.5")
        with zipfile.ZipFile(archive) as package:
            package.extractall(root / "published")
            files = {name: hashlib.sha256(package.read(name)).hexdigest() for name in package.namelist()}
        self.lock = lock("6.0.5", archive.read_bytes())
        self.receipt = {"toolLock": self.lock, "buildsRevision": SOURCE, "toolPrefix": prefix, "files": files}
        self.tool = root / "published" / prefix
        output = root / "bin/Release/net11.0"
        output.mkdir(parents=True)
        for filename, member in run_tuple_ci.BUILDS_ARTIFACT_MEMBERS:
            (output / filename).write_bytes((self.tool / member).read_bytes())
        self.output = output
        self.collectors = {"current": {"toolRoot": str(self.tool), "assembly": str(output / "Hexalith.Platform.Composition.Tests.dll")}}

    def test_expected_and_loaded_identities_are_recorded_for_both_builds_outputs(self):
        identities = run_tuple_ci.builds_artifact_identities(self.receipt, self.lock, SOURCE, self.collectors)
        for filename, member in run_tuple_ci.BUILDS_ARTIFACT_MEMBERS:
            record = identities["current"][filename]
            self.assertEqual(record["archiveMember"], self.receipt["toolPrefix"] + member)
            self.assertEqual(record["expectedSha256"], self.receipt["files"][record["archiveMember"]])
            self.assertEqual(record["loadedSha256"], hashlib.sha256((self.output / filename).read_bytes()).hexdigest())
            self.assertEqual(record["expectedSha256"], record["loadedSha256"])
            self.assertEqual(record["loadedPath"], str(self.output / filename))

    def test_changed_output_receipt_lock_or_builds_revision_refuses(self):
        for field, value in (("toolLock", lock("6.0.5", b"other bytes")), ("buildsRevision", "e" * 40)):
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, "differs from the locked tool and recorded Builds gitlink"):
                run_tuple_ci.builds_artifact_identities({**self.receipt, field: value}, self.lock, SOURCE, self.collectors)
        with self.assertRaisesRegex(ValueError, "differs from the locked tool and recorded Builds gitlink"):
            run_tuple_ci.builds_artifact_identities(self.receipt, self.lock, "e" * 40, self.collectors)
        changed = self.output / "Hexalith.Builds.Tooling.dll"
        changed.write_bytes(b"changed build output")
        with self.assertRaisesRegex(ValueError, "authenticated published artifact: Hexalith.Builds.Tooling.dll"):
            run_tuple_ci.builds_artifact_identities(self.receipt, self.lock, SOURCE, self.collectors)
        # Even when the extracted tool was changed too, the lock-verified archive member still refuses, after recording both.
        (self.tool / "Hexalith.Builds.Tooling.dll").write_bytes(b"changed build output")
        identities = {}
        with self.assertRaisesRegex(ValueError, "lock-verified archive member: Hexalith.Builds.Tooling.dll"):
            run_tuple_ci.builds_artifact_identities(self.receipt, self.lock, SOURCE, self.collectors, identities)
        record = identities["current"]["Hexalith.Builds.Tooling.dll"]
        self.assertEqual(record["loadedSha256"], hashlib.sha256(b"changed build output").hexdigest())
        self.assertNotEqual(record["expectedSha256"], record["loadedSha256"])


class PreparationTests(unittest.TestCase):
    """Synthetic package bytes exercise preparation mechanics; they are never publication evidence."""

    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.archive = self.root / "synthetic.nupkg"
        self.prefix = synthetic_archive(self.archive, "6.0.5")
        self.release = {"releaseTag": "v6.0.5", "tagCommit": SOURCE, "evidenceOnly": True, "qualificationCredit": False}

    def prepare(self, stack, output, *, locked=None, executable=None, recorded=False, release=None, signature=None):
        arguments = ["prepare", "--archive", str(self.archive), "--output", str(output)] + (["--require-recorded"] if recorded else [])
        stack.enter_context(patch.object(sys, "argv", arguments))
        stack.enter_context(patch.object(prepare_builds_artifacts, "load_binding", return_value=binding()))
        stack.enter_context(patch.object(prepare_builds_artifacts, "validate_owner_pins"))
        stack.enter_context(patch.object(prepare_builds_artifacts, "load_lock", return_value={"schemaVersion": 1, "packages": [
            locked or lock("6.0.5", self.archive.read_bytes())]}))
        stack.enter_context(patch.object(prepare_builds_artifacts, "source_state", return_value={"dirty": False}))
        stack.enter_context(patch.object(prepare_builds_artifacts, "recorded_gitlink", return_value=SOURCE))
        stack.enter_context(patch.object(prepare_builds_artifacts, "git_output", return_value="a" * 40))
        handoff = stack.enter_context(patch.object(prepare_builds_artifacts, "verify_release",
            side_effect=release, return_value=self.release))
        verify = stack.enter_context(patch.object(prepare_builds_artifacts, "verify_signature",
            side_effect=signature, return_value=subprocess.CompletedProcess([], 0, "verified control", "")))
        version = stack.enter_context(patch.object(prepare_builds_artifacts.subprocess, "check_output",
            return_value=executable or "6.0.5+" + SOURCE))
        download = stack.enter_context(patch.object(prepare_builds_artifacts, "download"))
        stack.enter_context(contextlib.redirect_stdout(io.StringIO()))
        return handoff, verify, version, download

    def test_locked_archive_receipt_records_per_file_expected_identities(self):
        output = self.root / "prepared"
        with contextlib.ExitStack() as stack:
            handoff, verify, version, download = self.prepare(stack, output)
            prepare_builds_artifacts.main()
        receipt = json.loads((output / "artifact-identity.json").read_bytes())
        tool = output / "published" / self.prefix
        with zipfile.ZipFile(self.archive) as package:
            expected = {name: hashlib.sha256(package.read(name)).hexdigest() for name in package.namelist()}
        self.assertEqual(receipt["files"], expected)
        self.assertEqual(receipt["toolLock"], lock("6.0.5", self.archive.read_bytes()))
        self.assertEqual((receipt["version"], receipt["buildsRevision"], receipt["toolPrefix"]), ("6.0.5", SOURCE, self.prefix))
        self.assertEqual(receipt["toolRoot"], str(tool))
        self.assertEqual(receipt["tupleBinding"], binding())
        self.assertEqual(receipt["release"], self.release)
        self.assertEqual(receipt["archiveSha256"], hashlib.sha256(self.archive.read_bytes()).hexdigest())
        self.assertIs(receipt["candidateAcceptance"], False)
        self.assertEqual(Path(receipt["toolingAssembly"]).read_bytes(), b"synthetic tooling")
        self.assertEqual(handoff.call_args.args[:2], (binding(), SOURCE))
        self.assertEqual(verify.call_args.args[0], output / "hexalith.builds.module.cli.6.0.5.nupkg")
        self.assertEqual(version.call_args.args[0], ["dotnet", str(tool / "Hexalith.Builds.Module.Cli.dll"), "--version"])
        download.assert_not_called()

    def test_tampered_package_fails_naming_the_package_before_signature_or_extraction(self):
        output = self.root / "tampered"
        with contextlib.ExitStack() as stack:
            _, verify, version, _ = self.prepare(stack, output, locked=lock("6.0.5", b"other bytes"))
            with self.assertRaisesRegex(ValueError, "Tool package Hexalith.Builds.Module.Cli 6.0.5 has contentHash .* differs from eng/builds-tool.lock.json"):
                prepare_builds_artifacts.main()
            verify.assert_not_called()
            version.assert_not_called()
        self.assertFalse((output / "published").exists())
        self.assertFalse((output / "artifact-identity.json").exists())

    def test_stale_or_missing_lock_refuses_before_any_side_effect(self):
        for locked, message in ((lock("6.0.4"), "names Hexalith.Builds.Module.Cli 6.0.4, but the Builds pin is 6.0.5"),
                                ({**lock("6.0.5"), "id": "Hexalith.Builds.Evidence.Cli"}, "no entry for Hexalith.Builds.Module.Cli")):
            output = self.root / ("lock-" + locked["version"] + locked["id"])
            with self.subTest(message=message), contextlib.ExitStack() as stack:
                handoff, verify, _, download = self.prepare(stack, output, locked=locked)
                with self.assertRaisesRegex(ValueError, message):
                    prepare_builds_artifacts.main()
                handoff.assert_not_called()
                verify.assert_not_called()
                download.assert_not_called()
            self.assertFalse(output.exists())

    def test_tag_drift_refuses_before_download(self):
        output = self.root / "tag-drift"
        with contextlib.ExitStack() as stack:
            _, verify, _, download = self.prepare(stack, output, release=ValueError("Builds release tag v6.0.5 names commit " + "e" * 40))
            with self.assertRaisesRegex(ValueError, "names commit"):
                prepare_builds_artifacts.main()
            verify.assert_not_called()
            download.assert_not_called()
        self.assertFalse(output.exists())

    def test_executable_must_report_the_pinned_version_and_gitlink(self):
        for executable in ("6.0.5+" + "e" * 40, "6.0.6+" + SOURCE, "6.0.5"):
            output = self.root / ("executable-" + executable)
            with self.subTest(executable=executable), contextlib.ExitStack() as stack:
                self.prepare(stack, output, executable=executable)
                with self.assertRaisesRegex(ValueError, "Published executable reports"):
                    prepare_builds_artifacts.main()
            self.assertFalse((output / "artifact-identity.json").exists())

    def test_failed_signature_retains_its_output_and_writes_no_receipt(self):
        output = self.root / "unsigned"
        with contextlib.ExitStack() as stack:
            self.prepare(stack, output, signature=lambda path: subprocess.CompletedProcess([], 1, "", "NU3004: unsigned"))
            with self.assertRaisesRegex(ValueError, "signature verification failed"):
                prepare_builds_artifacts.main()
        self.assertEqual((output / "signature.stderr").read_text(), "NU3004: unsigned")
        self.assertFalse((output / "artifact-identity.json").exists())

    def test_recorded_refusal_precedes_all_artifact_side_effects(self):
        for message in ("clean recorded Platform", "actual Builds catalog gitlink", "Builds checkout differs"):
            output = self.root / ("recorded-" + message.replace(" ", "-"))
            with self.subTest(refusal=message), contextlib.ExitStack() as stack:
                stack.enter_context(patch.object(prepare_builds_artifacts, "materialize", return_value={"buildsRoot": str(self.root / "producer")}))
                stack.enter_context(patch.object(prepare_builds_artifacts, "recorded_preparation_context", side_effect=ValueError(message)))
                handoff, verify, _, download = self.prepare(stack, output, recorded=True)
                with self.assertRaisesRegex(ValueError, message):
                    prepare_builds_artifacts.main()
                handoff.assert_not_called()
                verify.assert_not_called()
                download.assert_not_called()
            self.assertFalse(output.exists())

    def test_recorded_owner_change_during_archive_verification_refuses_receipt_and_export(self):
        current = {"platformRevision": "a" * 40, "platformSourceState": {"dirty": False},
                   "platformRecordedBuildsRevision": SOURCE, "boundProducerContext": {"sourceRevision": SOURCE}}
        observations = []

        def context(_binding, *, builds_root=None):
            observations.append(copy.deepcopy(current))
            return copy.deepcopy(current)

        def signature(path):
            # A different committed owner HEAD stays clean: status alone cannot detect it.
            current["platformRevision"] = "c" * 40
            return subprocess.CompletedProcess([], 0, "verified synthetic control", "")

        output = self.root / "recorded-archive-control"
        exported = io.StringIO()
        with contextlib.ExitStack() as stack:
            stack.enter_context(patch.object(prepare_builds_artifacts, "materialize", return_value={"buildsRoot": str(self.root / "producer")}))
            stack.enter_context(patch.object(prepare_builds_artifacts, "recorded_preparation_context", side_effect=context))
            handoff, _, version, _ = self.prepare(stack, output, recorded=True, signature=signature)
            stack.enter_context(contextlib.redirect_stdout(exported))
            with self.assertRaisesRegex(ValueError, "changed during artifact preparation"):
                prepare_builds_artifacts.main()
        self.assertEqual([entry["platformRevision"] for entry in observations], ["a" * 40, "c" * 40])
        self.assertEqual(handoff.call_args.args[1], SOURCE)
        version.assert_called_once()
        self.assertTrue((output / "signature.stdout").is_file())
        self.assertFalse((output / "artifact-identity.json").exists())
        self.assertEqual(exported.getvalue(), "")


if __name__ == "__main__":
    unittest.main()
