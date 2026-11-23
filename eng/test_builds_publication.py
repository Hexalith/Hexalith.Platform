"""Release-by-tag authentication and official metadata transport controls; no network."""
import copy
import contextlib
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import urllib.error
import urllib.request
import zipfile

from builds_publication import read_official, tag_commit, verify_release
from builds_tool_lock import content_hash

SOURCE = "0123456789abcdef0123456789abcdef01234567"  # synthetic gitlink, not a Builds release
BINDING = {"schemaVersion": 2, "version": "6.0.5", "releaseTag": "v6.0.5"}


class ReleaseTagTests(unittest.TestCase):
    def setUp(self):
        self.body = (f"hexalith-release: {SOURCE}\nhexalith-release-run: 12\n"
                     f"hexalith-release-artifact: 13 sha256:{'a' * 64}\n"
                     "hexalith-release-packages: Hexalith.Builds.Evidence.Cli,Hexalith.Builds.Module.Cli\n"
                     "hexalith-release-containers: \n")
        self.responses = {
            "git/ref/tags/v6.0.5": {"object": {"type": "commit", "sha": SOURCE}},
            "releases/tags/v6.0.5": {"id": 11, "tag_name": "v6.0.5", "draft": False, "prerelease": False,
                                     "published_at": "2026-11-12T09:13:04Z", "body": self.body},
        }

    def verify(self, gitlink=SOURCE):
        return verify_release(BINDING, gitlink, read=lambda path: self.responses[path])

    def test_published_release_at_the_gitlink_yields_evidence_only(self):
        receipt = self.verify()
        self.assertEqual((receipt["releaseTag"], receipt["tagCommit"], receipt["releaseId"]), ("v6.0.5", SOURCE, 11))
        self.assertEqual(receipt["markers"]["hexalith-release"], SOURCE)
        self.assertEqual(receipt["markers"]["hexalith-release-run"], "12")
        self.assertEqual(receipt["releaseBodySha256"], hashlib.sha256(self.body.encode()).hexdigest())
        self.assertIs(receipt["evidenceOnly"], True)
        self.assertIs(receipt["qualificationCredit"], False)

    def test_tag_drift_from_the_gitlink_refuses(self):
        with self.assertRaisesRegex(ValueError, "names commit " + SOURCE + ", but the recorded Builds gitlink is " + "e" * 40):
            self.verify("e" * 40)
        self.responses["git/ref/tags/v6.0.5"]["object"]["sha"] = "b" * 40
        with self.assertRaisesRegex(ValueError, "names commit " + "b" * 40):
            self.verify()

    def test_unpublished_draft_prerelease_or_foreign_release_refuses(self):
        for key, value in (("draft", True), ("prerelease", True), ("published_at", None), ("tag_name", "v6.0.6"), ("body", None)):
            with self.subTest(key=key):
                previous = self.responses["releases/tags/v6.0.5"][key]
                self.responses["releases/tags/v6.0.5"][key] = value
                with self.assertRaisesRegex(ValueError, "is not a published release"):
                    self.verify()
                self.responses["releases/tags/v6.0.5"][key] = previous

    def test_missing_duplicate_or_wrong_release_marker_refuses(self):
        for body in (self.body.replace("hexalith-release: " + SOURCE + "\n", ""), self.body + "hexalith-release: " + SOURCE + "\n",
                     self.body.replace("hexalith-release: " + SOURCE, "hexalith-release: " + "b" * 40)):
            with self.subTest(body=body):
                self.responses["releases/tags/v6.0.5"]["body"] = body
                with self.assertRaisesRegex(ValueError, "marker differs from the recorded Builds gitlink"):
                    self.verify()

    def test_annotated_tag_is_peeled_and_unbounded_or_foreign_tag_chains_refuse(self):
        self.responses["git/ref/tags/v6.0.5"] = {"object": {"type": "tag", "sha": "c" * 40}}
        self.responses["git/tags/" + "c" * 40] = {"object": {"type": "commit", "sha": SOURCE}}
        self.assertEqual(self.verify()["tagCommit"], SOURCE)
        self.responses["git/tags/" + "c" * 40] = self.responses["git/ref/tags/v6.0.5"]
        with self.assertRaisesRegex(ValueError, "does not identify a commit"):
            tag_commit("v6.0.5", read=lambda path: self.responses[path])
        self.responses["git/ref/tags/v6.0.5"] = {"object": {"type": "tree", "sha": "d" * 40}}
        with self.assertRaisesRegex(ValueError, "does not identify a commit"):
            self.verify()


class OfficialMetadataTransportTests(unittest.TestCase):
    def test_job_token_authenticates_only_the_fixed_official_metadata_request(self):
        token = "fake-publication-job-token"
        metadata = {"id": 11, "body": "public release metadata"}
        with patch.dict(os.environ, {"GH_TOKEN": token}), patch("builds_publication.urllib.request.urlopen") as transport:
            response = transport.return_value.__enter__.return_value
            response.read.return_value = json.dumps(metadata).encode()
            result = read_official("releases/11")
        transport.assert_called_once()
        request = transport.call_args.args[0]
        self.assertEqual(request.full_url, "https://api.github.com/repos/Hexalith/Hexalith.Builds/releases/11")
        self.assertEqual(request.get_header("Authorization"), "Bearer " + token)
        self.assertEqual(request.get_header("Accept"), "application/vnd.github+json")
        self.assertEqual(request.get_header("User-agent"), "Hexalith-Platform-publication-verification/1.0")
        self.assertEqual(transport.call_args.kwargs, {"timeout": 60})
        response.read.assert_called_once_with(2_000_001)
        self.assertEqual(result, metadata)
        self.assertNotIn(token, json.dumps(result))

    def test_missing_or_empty_job_token_keeps_anonymous_requests(self):
        for environment in ({}, {"GH_TOKEN": ""}):
            with self.subTest(environment=environment), patch.dict(os.environ, environment, clear=True), \
                    patch("builds_publication.urllib.request.urlopen") as transport:
                transport.return_value.__enter__.return_value.read.return_value = b'{"id":11}'
                self.assertEqual(read_official("releases/11"), {"id": 11})
                transport.assert_called_once()
                request = transport.call_args.args[0]
                self.assertEqual(request.full_url, "https://api.github.com/repos/Hexalith/Hexalith.Builds/releases/11")
                self.assertIsNone(request.get_header("Authorization"))

    def test_metadata_length_bound_still_accepts_the_limit_and_refuses_excess(self):
        for length in (2_000_000, 2_000_001):
            with self.subTest(length=length), patch.dict(os.environ, {"GH_TOKEN": "fake-bounded-job-token"}), \
                    patch("builds_publication.urllib.request.urlopen") as transport:
                response = transport.return_value.__enter__.return_value
                response.read.return_value = b'"' + b'x' * (length - 2) + b'"'
                if length == 2_000_000:
                    self.assertEqual(read_official("releases/11"), "x" * (length - 2))
                else:
                    with self.assertRaisesRegex(ValueError, "metadata exceeds its bound"):
                        read_official("releases/11")
                response.read.assert_called_once_with(2_000_001)
                transport.assert_called_once()

    def test_http_403_propagates_without_anonymous_retry(self):
        error = urllib.error.HTTPError("https://api.github.com/repos/Hexalith/Hexalith.Builds/releases/11",
                                       403, "rate limit exceeded", {}, None)
        self.addCleanup(error.close)
        with patch.dict(os.environ, {"GH_TOKEN": "fake-refused-job-token"}), \
                patch("builds_publication.urllib.request.urlopen", side_effect=error) as transport:
            with self.assertRaises(urllib.error.HTTPError) as refused:
                read_official("releases/11")
        self.assertIs(refused.exception, error)
        transport.assert_called_once()
        self.assertEqual(transport.call_args.args[0].get_header("Authorization"), "Bearer fake-refused-job-token")

    def test_redirects_do_not_forward_the_job_token_to_another_origin(self):
        with patch.dict(os.environ, {"GH_TOKEN": "fake-redirect-job-token"}), \
                patch("builds_publication.urllib.request.urlopen") as transport:
            transport.return_value.__enter__.return_value.read.return_value = b'{"id":11}'
            read_official("releases/11")
        request = transport.call_args.args[0]
        self.assertEqual(request.get_header("Authorization"), "Bearer fake-redirect-job-token")
        for destination in ("https://api.nuget.org/v3/index.json", "https://example.com/metadata"):
            with self.subTest(destination=destination):
                redirected = urllib.request.HTTPRedirectHandler().redirect_request(
                    request, None, 302, "Found", {}, destination)
                self.assertEqual(redirected.full_url, destination)
                self.assertIsNone(redirected.get_header("Authorization"))


class MetadataTokenEnvironmentTests(unittest.TestCase):
    def test_ordinary_child_environments_remove_tokens_and_preserve_values_and_overrides(self):
        from run_tuple_ci import _subprocess_environment
        token = "fake-private-metadata-token"
        script = "import json,os;print(json.dumps({key:os.environ.get(key) for key in ['GH_TOKEN','CONTROL','DOTNET_ROLL_FORWARD']}))"
        ambient = {"GH_TOKEN": "fake-ambient-token", "CONTROL": "ambient", "DOTNET_ROLL_FORWARD": "Minor"}
        override = {"GH_TOKEN": "fake-override-token", "CONTROL": "override", "DOTNET_ROLL_FORWARD": "LatestPatch"}
        with patch.dict(os.environ, ambient, clear=True):
            for environment in (None, override):
                original = dict(ambient if environment is None else environment)
                for name in ("revision", "restore", "build", "test-0", "prepare-historical-reader",
                             "published-artifacts", "retained-published-artifacts"):
                    with self.subTest(name=name, override=environment is not None):
                        child = _subprocess_environment(name, environment, metadata_token=token)
                        observed = json.loads(subprocess.check_output([sys.executable, "-c", script], env=child, text=True))
                        expected = token if name == "published-artifacts" else None
                        self.assertEqual(observed, {"GH_TOKEN": expected, "CONTROL": original["CONTROL"],
                                                    "DOTNET_ROLL_FORWARD": original["DOTNET_ROLL_FORWARD"]})
                        self.assertEqual(dict(ambient if environment is None else environment), original)
            child = _subprocess_environment("published-artifacts")
            self.assertNotIn("GH_TOKEN", child)
            self.assertEqual(dict(os.environ), ambient)

    def test_runner_confines_inheritance_to_fresh_verification_and_restores_environment_on_failure(self):
        import run_tuple_ci
        token = "fake-runner-metadata-token"
        binding, source = dict(BINDING), SOURCE
        locked = {"id": "Hexalith.Builds.Module.Cli", "version": "6.0.5", "contentHash": content_hash(b"synthetic")}
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "eng").mkdir()
            (root / "eng/tuple-ci-sdk42-current-suites.json").write_text(json.dumps({"groups": [
                {"requiredCount": run_tuple_ci.REQUIRED_TOTALS["current42"], "filter": "-class",
                 "selectors": ["Synthetic.Control"], "collector": "current"}]}))
            (root / "Hexalith.Platform.slnx").write_text("<Solution />")
            catalog = root / "catalog.props"
            catalog.write_text("<Project><PropertyGroup><HexalithEventStoreVersion>4.2.0</HexalithEventStoreVersion></PropertyGroup></Project>")
            for fail in (False, True):
                evidence = root / ("failed" if fail else "passed")
                commands = []

                def source_state():
                    self.assertNotIn("GH_TOKEN", os.environ)
                    return {"dirty": False}

                def catalog_check(*_args):
                    self.assertNotIn("GH_TOKEN", os.environ)
                    return catalog, catalog.read_bytes(), {"sha256": hashlib.sha256(catalog.read_bytes()).hexdigest()}

                def command(argv, **kwargs):
                    self.assertNotIn("GH_TOKEN", os.environ)
                    metadata = len(argv) > 1 and argv[1].endswith("prepare_builds_artifacts.py")
                    self.assertEqual(kwargs["env"].get("GH_TOKEN"), token if metadata else None)
                    self.assertEqual(kwargs["env"]["CONTROL"], "preserved")
                    commands.append(argv)
                    output = str(root / "tool") if metadata else source if argv[0] == "git" else "synthetic dotnet info"
                    kwargs["stdout"].write(output)
                    return subprocess.CompletedProcess(argv, 1 if metadata and fail else 0)

                with self.subTest(failure=fail), contextlib.ExitStack() as stack:
                    stack.enter_context(patch.dict(os.environ, {"GH_TOKEN": token, "CONTROL": "preserved"}))
                    stack.enter_context(patch.object(sys, "argv", ["preflight", "--evidence", str(evidence), "--audit-only", "--suite", "current42"]))
                    stack.enter_context(patch.object(run_tuple_ci, "ROOT", root))
                    stack.enter_context(patch.object(run_tuple_ci, "load_binding", return_value=binding))
                    stack.enter_context(patch.object(run_tuple_ci, "load_lock", return_value={"schemaVersion": 1, "packages": [locked]}))
                    stack.enter_context(patch.object(run_tuple_ci, "source_state", side_effect=source_state))
                    stack.enter_context(patch.object(run_tuple_ci, "validate_owner_pins"))
                    stack.enter_context(patch.object(run_tuple_ci, "resolve_builds_root", return_value=root))
                    stack.enter_context(patch.object(run_tuple_ci, "verify_catalog", side_effect=catalog_check))
                    stack.enter_context(patch.object(run_tuple_ci, "verify_unified_catalog", return_value={"properties": {"HexalithEventStoreVersion": "5.4.0"}}))
                    stack.enter_context(patch.object(run_tuple_ci.signal, "signal"))
                    stack.enter_context(patch.object(run_tuple_ci.platform, "platform", return_value="synthetic platform"))
                    stack.enter_context(patch.object(run_tuple_ci.subprocess, "run", side_effect=command))
                    if fail:
                        with self.assertRaisesRegex(RuntimeError, "published-artifacts failed"):
                            run_tuple_ci.main()
                    else:
                        run_tuple_ci.main()
                    self.assertEqual(os.environ["GH_TOKEN"], token)
                    self.assertEqual(os.environ["CONTROL"], "preserved")
                verification = [argv for argv in commands if len(argv) > 1 and argv[1].endswith("prepare_builds_artifacts.py")]
                self.assertEqual(len(verification), 1)
                self.assertIn("--require-recorded", verification[0])
                receipt = (evidence / "tuple-ci-result.json").read_text()
                self.assertNotIn(token, receipt)
                self.assertEqual(json.loads(receipt)["status"], "failed" if fail else "graph-verified")

    def test_artifact_verifier_keeps_metadata_token_out_of_git_signature_and_tool_children(self):
        import prepare_builds_artifacts
        token = "fake-artifact-metadata-token"
        with tempfile.TemporaryDirectory() as directory, contextlib.ExitStack() as stack:
            root = Path(directory)
            archive = root / "synthetic.nupkg"
            output = root / "artifacts"
            with zipfile.ZipFile(archive, "w") as package:
                package.writestr("tools/net11.0/any/Hexalith.Builds.Tooling.dll", b"synthetic tooling control")
                package.writestr("tools/net11.0/any/Hexalith.Builds.Module.Cli.dll", b"synthetic CLI control")
            locked = {"id": "Hexalith.Builds.Module.Cli", "version": "6.0.5", "contentHash": content_hash(archive.read_bytes())}

            def no_token():
                self.assertNotIn("GH_TOKEN", os.environ)
                self.assertEqual(os.environ["CONTROL"], "preserved")

            def git_or_tool(argv, **_kwargs):
                no_token()
                return SOURCE if argv[0] == "git" else "6.0.5+" + SOURCE

            def signature(argv, **_kwargs):
                no_token()
                self.assertEqual(argv[:3], ["dotnet", "nuget", "verify"])
                return subprocess.CompletedProcess(argv, 0, "synthetic verified signature", "")

            def metadata(request, **_kwargs):
                no_token()
                self.assertEqual(request.full_url, "https://api.github.com/repos/Hexalith/Hexalith.Builds/releases/tags/v6.0.5")
                self.assertEqual(request.get_header("Authorization"), "Bearer " + token)
                return io.BytesIO(b'{"id":11}')

            def release(_binding, gitlink, read):
                no_token()
                self.assertEqual(gitlink, SOURCE)
                self.assertEqual(read("releases/tags/v6.0.5"), {"id": 11})
                return {"releaseId": 11}

            stack.enter_context(patch.dict(os.environ, {"GH_TOKEN": token, "CONTROL": "preserved"}))
            stack.enter_context(patch.object(sys, "argv", ["prepare", "--archive", str(archive), "--output", str(output)]))
            stack.enter_context(patch.object(prepare_builds_artifacts, "load_binding", return_value=dict(BINDING)))
            stack.enter_context(patch.object(prepare_builds_artifacts, "validate_owner_pins"))
            stack.enter_context(patch.object(prepare_builds_artifacts, "load_lock", return_value={"schemaVersion": 1, "packages": [locked]}))
            stack.enter_context(patch.object(prepare_builds_artifacts, "recorded_gitlink", side_effect=lambda *a, **k: (no_token(), SOURCE)[1]))
            stack.enter_context(patch.object(prepare_builds_artifacts, "source_state", side_effect=lambda: (no_token(), {"dirty": False})[1]))
            stack.enter_context(patch.object(prepare_builds_artifacts, "verify_release", side_effect=release))
            stack.enter_context(patch.object(prepare_builds_artifacts.subprocess, "check_output", side_effect=git_or_tool))
            stack.enter_context(patch.object(prepare_builds_artifacts.subprocess, "run", side_effect=signature))
            transport = stack.enter_context(patch("builds_publication.urllib.request.urlopen", side_effect=metadata))
            stack.enter_context(contextlib.redirect_stdout(io.StringIO()))
            prepare_builds_artifacts.main()
            transport.assert_called_once()
            self.assertEqual(os.environ["GH_TOKEN"], token)
            self.assertNotIn(token, (output / "artifact-identity.json").read_text())

if __name__ == "__main__":
    unittest.main()
