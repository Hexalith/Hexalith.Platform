"""Finite shared release format and original public handoff refusal controls."""
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

from builds_publication import read_official, verify_handoff
from builds_tuple import load_binding, validate_binding


class SharedPublicationTests(unittest.TestCase):
    def setUp(self):
        self.binding = copy.deepcopy(load_binding(require_publication=False))
        publication = self.binding["publication"]
        source = self.binding["producer"]["sourceRevision"]
        self.body = (f"hexalith-release: {source}\nhexalith-release-run: 12\n"
                     f"hexalith-release-artifact: 13 sha256:{'a' * 64}\n"
                     "hexalith-release-packages: Hexalith.Builds.Evidence.Cli,Hexalith.Builds.Module.Cli\n"
                     "hexalith-release-containers: \n")
        self.binding["publication"] = {key: publication[key] for key in
            ("version", "sourceRevision", "releaseTag", "catalogSha256", "archiveSha256", "toolingSha256")}
        self.binding["publication"].update(kind="shared-release-handoff/v1", releaseId=11, runId=12, artifactId=13,
            artifactSha256="a" * 64, releaseBodySha256=hashlib.sha256(self.body.encode()).hexdigest())
        manifest = {"sha": source, "version": publication["version"], "packages": [
            {"id": identity, "path": "packages/" + identity + "." + publication["version"] + ".nupkg"}
            for identity in ("Hexalith.Builds.Evidence.Cli", "Hexalith.Builds.Module.Cli")], "containers": []}
        self.manifest(manifest)
        self.responses = {
            "releases/11": {"id": 11, "tag_name": publication["releaseTag"], "draft": False,
                            "prerelease": False, "published_at": "2026-11-12T09:13:04Z", "body": self.body},
            "git/ref/tags/" + publication["releaseTag"]: {"object": {"type": "commit", "sha": source}},
            "actions/runs/12": {"id": 12, "head_sha": source, "status": "completed", "conclusion": "success",
                                "path": ".github/workflows/builds-release.yml"},
        }

    def manifest(self, value):
        raw = value if isinstance(value, str) else json.dumps(value)
        self.binding["publication"].update(manifestJson=raw, manifestSha256=hashlib.sha256(raw.encode()).hexdigest())

    def verify(self):
        validate_binding(self.binding)
        return verify_handoff(self.binding, read=lambda path: self.responses[path])

    def test_shared_publication_is_identity_evidence_without_legacy_native_credit(self):
        receipt = self.verify()
        self.assertFalse(receipt["qualificationCredit"])
        self.assertFalse(receipt["legacyG4AcceptanceClaimed"])
        self.assertNotIn("inventoryUrl", self.binding["publication"])

    def test_net11_shared_publication_requires_its_own_closed_manifest_and_owner_major_range(self):
        from builds_tuple import validate_owner_pins
        for version, upper in (("5.0.0", "6.0.0-0"), ("6.0.0", "7.0.0-0")):
            with self.subTest(version=version):
                publication = self.binding["publication"]
                old_tag = publication["releaseTag"]
                publication.update(version=version, releaseTag="v" + version)
                manifest = json.loads(publication["manifestJson"])
                manifest["version"] = version
                for item in manifest["packages"]:
                    item["path"] = "packages/" + item["id"] + "." + version + ".nupkg"
                self.manifest(manifest)
                self.responses["releases/11"]["tag_name"] = publication["releaseTag"]
                self.responses["git/ref/tags/" + publication["releaseTag"]] = self.responses.pop("git/ref/tags/" + old_tag)
                self.assertFalse(self.verify()["qualificationCredit"])
                with tempfile.TemporaryDirectory() as directory:
                    root = Path(directory)
                    (root / ".config").mkdir()
                    (root / ".config/dotnet-tools.json").write_text(json.dumps({
                        "tools": {"hexalith.builds.module.cli": {"version": version}}}))
                    floor = root / "hexalith-platform.json"
                    floor.write_text(json.dumps({"toolVersionRange": "[" + version + ", " + upper + ")"}))
                    validate_owner_pins(self.binding, root)
                    floor.write_text(json.dumps({"toolVersionRange": "[" + version + ", " + version + "-0)"}))
                    with self.assertRaisesRegex(ValueError, "owner tool pin and range floor"):
                        validate_owner_pins(self.binding, root)

    def test_future_generation_refuses_even_with_a_complete_shared_manifest(self):
        publication = self.binding["publication"]
        publication.update(version="7.0.0", releaseTag="v7.0.0")
        manifest = json.loads(publication["manifestJson"])
        manifest["version"] = "7.0.0"
        for item in manifest["packages"]:
            item["path"] = "packages/" + item["id"] + ".7.0.0.nupkg"
        self.manifest(manifest)
        with self.assertRaisesRegex(ValueError, "exact supported"):
            validate_binding(self.binding)

    def test_unknown_missing_mixed_or_non_numeric_publication_fields_refuse(self):
        for key, value in (("kind", "other/v1"), ("inventoryUrl", "invented"), ("releaseId", True),
                           ("runId", "12"), ("artifactId", -1), ("artifactSha256", "x")):
            with self.subTest(key=key):
                previous = copy.deepcopy(self.binding)
                self.binding["publication"][key] = value
                with self.assertRaises(ValueError):
                    self.verify()
                self.binding = previous
        for key in tuple(self.binding["publication"]):
            previous = self.binding["publication"].pop(key)
            with self.subTest(missing=key), self.assertRaises(ValueError):
                self.verify()
            self.binding["publication"][key] = previous

    def test_changed_release_tag_body_run_and_artifact_refuse(self):
        for path, key, value in (("releases/11", "id", 99), ("releases/11", "draft", True),
                ("releases/11", "prerelease", True), ("releases/11", "body", self.body + "changed"),
                ("actions/runs/12", "head_sha", "b" * 40), ("actions/runs/12", "conclusion", "failure"),
                ("actions/runs/12", "path", ".github/workflows/other.yml")):
            with self.subTest(path=path, key=key):
                previous = self.responses[path][key]
                self.responses[path][key] = value
                with self.assertRaises(ValueError):
                    self.verify()
                self.responses[path][key] = previous
        path = "git/ref/tags/" + self.binding["publication"]["releaseTag"]
        self.responses[path]["object"]["sha"] = "b" * 40
        with self.assertRaisesRegex(ValueError, "tag differs"):
            self.verify()

    def test_manifest_source_packages_paths_duplicates_and_trailers_refuse(self):
        original = json.loads(self.binding["publication"]["manifestJson"])
        for key, value in (("sha", "b" * 40), ("version", "4.0.0"), ("packages", []), ("containers", ["other"])):
            with self.subTest(key=key):
                manifest = copy.deepcopy(original)
                manifest[key] = value
                self.manifest(manifest)
                with self.assertRaisesRegex(ValueError, "manifest source/version/package closure"):
                    self.verify()
        for raw in ('{"sha":"x","sha":"x"}', json.dumps(original) + '{}'):
            self.manifest(raw)
            with self.assertRaisesRegex(ValueError, "manifest is invalid"):
                self.verify()
        manifest = copy.deepcopy(original)
        manifest["packages"][0]["path"] = "../outside.nupkg"
        self.manifest(manifest)
        with self.assertRaisesRegex(ValueError, "package closure"):
            self.verify()

    def test_rehashed_missing_duplicate_or_wrong_markers_still_refuse(self):
        for body in (self.body.replace("hexalith-release-run: 12", "hexalith-release-run: 99"),
                     self.body + "hexalith-release-run: 12\n", self.body.replace("hexalith-release-containers: \n", "")):
            with self.subTest(body=body):
                self.responses["releases/11"]["body"] = body
                self.binding["publication"]["releaseBodySha256"] = hashlib.sha256(body.encode()).hexdigest()
                with self.assertRaisesRegex(ValueError, "marker differs"):
                    self.verify()

    def test_annotated_tag_is_peeled_and_unbounded_tag_chain_refuses(self):
        path = "git/ref/tags/" + self.binding["publication"]["releaseTag"]
        self.responses[path] = {"object": {"type": "tag", "sha": "c" * 40}}
        self.responses["git/tags/" + "c" * 40] = {"object": {"type": "commit", "sha": self.binding["producer"]["sourceRevision"]}}
        self.verify()
        self.responses["git/tags/" + "c" * 40] = self.responses[path]
        with self.assertRaises(ValueError):
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
                        expected = token if name in ("published-artifacts", "retained-published-artifacts") else None
                        self.assertEqual(observed, {"GH_TOKEN": expected, "CONTROL": original["CONTROL"],
                                                    "DOTNET_ROLL_FORWARD": original["DOTNET_ROLL_FORWARD"]})
                        self.assertEqual(dict(ambient if environment is None else environment), original)
            child = _subprocess_environment("published-artifacts")
            self.assertNotIn("GH_TOKEN", child)
            self.assertEqual(dict(os.environ), ambient)

    def test_runner_confines_inheritance_to_fresh_verification_and_restores_environment_on_failure(self):
        import run_tuple_ci
        token = "fake-runner-metadata-token"
        binding = load_binding(require_publication=False)
        source = binding["producer"]["sourceRevision"]
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "eng").mkdir()
            (root / "eng/tuple-ci-suites.json").write_text(json.dumps({"groups": [
                {"requiredCount": 1, "filter": "-class", "selectors": ["Synthetic.Control"]}]}))
            (root / "Hexalith.Platform.slnx").write_text("<Solution />")
            catalog = root / "catalog.props"
            catalog.write_text("<Project><PropertyGroup><HexalithEventStoreVersion>4.1.0</HexalithEventStoreVersion></PropertyGroup></Project>")
            for fail in (False, True):
                evidence = root / ("failed" if fail else "passed")
                commands = []

                def source_state():
                    self.assertNotIn("GH_TOKEN", os.environ)
                    return {"dirty": False}

                def catalog_check(*_args):
                    self.assertNotIn("GH_TOKEN", os.environ)
                    return catalog

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
                    stack.enter_context(patch.object(sys, "argv", ["preflight", "--evidence", str(evidence), "--audit-only"]))
                    stack.enter_context(patch.object(run_tuple_ci, "ROOT", root))
                    stack.enter_context(patch.object(run_tuple_ci, "load_binding", return_value=binding))
                    stack.enter_context(patch.object(run_tuple_ci, "source_state", side_effect=source_state))
                    stack.enter_context(patch.object(run_tuple_ci, "validate_owner_pins"))
                    stack.enter_context(patch.object(run_tuple_ci, "resolve_builds_root", return_value=root))
                    stack.enter_context(patch.object(run_tuple_ci, "verify_catalog", side_effect=catalog_check))
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
        binding = copy.deepcopy(load_binding(require_publication=False))
        version = binding["publication"]["version"]
        source = binding["producer"]["sourceRevision"]
        framework = "net11.0" if version.startswith(("5.", "6.")) else "net10.0"
        with tempfile.TemporaryDirectory() as directory, contextlib.ExitStack() as stack:
            root = Path(directory)
            archive = root / "synthetic.nupkg"
            output = root / "artifacts"
            payload = b"synthetic tooling control"
            with zipfile.ZipFile(archive, "w") as package:
                package.writestr("tools/" + framework + "/any/Hexalith.Builds.Tooling.dll", payload)
                package.writestr("tools/" + framework + "/any/Hexalith.Builds.Module.Cli.dll", b"synthetic CLI control")
            binding["publication"].update(archiveSha256=hashlib.sha256(archive.read_bytes()).hexdigest(),
                                           toolingSha256=hashlib.sha256(payload).hexdigest())

            def no_token():
                self.assertNotIn("GH_TOKEN", os.environ)
                self.assertEqual(os.environ["CONTROL"], "preserved")

            def git_or_tool(argv, **_kwargs):
                no_token()
                return source if argv[0] == "git" else version + "+" + source

            def signature(argv, **_kwargs):
                no_token()
                self.assertEqual(argv[:3], ["dotnet", "nuget", "verify"])
                return subprocess.CompletedProcess(argv, 0, "synthetic verified signature", "")

            def metadata(request, **_kwargs):
                no_token()
                self.assertEqual(request.full_url, "https://api.github.com/repos/Hexalith/Hexalith.Builds/releases/11")
                self.assertEqual(request.get_header("Authorization"), "Bearer " + token)
                return io.BytesIO(b'{"id":11}')

            def handoff(_binding, read):
                no_token()
                self.assertEqual(read("releases/11"), {"id": 11})
                return {"releaseId": 11}

            stack.enter_context(patch.dict(os.environ, {"GH_TOKEN": token, "CONTROL": "preserved"}))
            stack.enter_context(patch.object(sys, "argv", ["prepare", "--archive", str(archive), "--output", str(output)]))
            stack.enter_context(patch.object(prepare_builds_artifacts, "load_binding", return_value=binding))
            stack.enter_context(patch.object(prepare_builds_artifacts, "validate_owner_pins"))
            stack.enter_context(patch.object(prepare_builds_artifacts, "source_state", side_effect=lambda: (no_token(), {"dirty": False})[1]))
            stack.enter_context(patch.object(prepare_builds_artifacts, "verify_handoff", side_effect=handoff))
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
