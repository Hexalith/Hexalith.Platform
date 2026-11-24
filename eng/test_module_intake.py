"""Exercise module intake without contacting GitHub, NuGet or a registry."""

import copy
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
from urllib.error import HTTPError

import module_intake as intake


REPOSITORY = "Example/Module"
IMAGE = "registry.example.com/eventstore"
DIGEST = "sha256:" + "a" * 64
COMMIT = "b" * 40
SIGNER = "c" * 40
WORKFLOW = {
    "workflow": ".github/workflows/release.yml",
    "ref": "refs/heads/main",
    "buildsWorkflow": "Example/Builds/.github/workflows/domain-release.yml",
    "buildsSha": SIGNER,
}
CERTIFICATE = {
    "issuer": "https://token.actions.githubusercontent.com",
    "runnerEnvironment": "github-hosted",
    "sourceRepositoryURI": "https://github.com/Example/Module",
    "sourceRepositoryDigest": COMMIT,
    "sourceRepositoryRef": "refs/heads/main",
    "buildConfigURI": "https://github.com/Example/Module/.github/workflows/release.yml@refs/heads/main",
    "buildSignerURI": "https://github.com/Example/Builds/.github/workflows/domain-release.yml@" + SIGNER,
    "buildSignerDigest": SIGNER,
}


def release(version, change="additive"):
    return {
        "version": version,
        "commit": COMMIT,
        "packages": [{"id": "Example.EventStore", "sha256": "d" * 64}],
        "images": [{"name": IMAGE, "digest": DIGEST}],
        "attestation": copy.deepcopy(WORKFLOW),
        "evidence": [f"https://github.com/{REPOSITORY}/releases/tag/v{version}"],
        "classification": {"changeClass": change, "declarationSha256": "e" * 64},
    }


def verification_result(certificate=None, digest=DIGEST):
    return {"verificationResult": {
        "signature": {"certificate": CERTIFICATE.copy() if certificate is None else certificate},
        "statement": {"subject": [{"name": IMAGE, "digest": {"sha256": digest.removeprefix("sha256:")}}]},
    }}


def manifest(*releases):
    modules = {"eventstore": {"repository": REPOSITORY, "releases": list(releases)}}
    return {
        "schema": intake.SCHEMA,
        "modules": modules,
        "classification": intake.compute_classification(modules, "none", {}),
    }


class ModuleIntakeTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.directory = Path(temporary.name)
        self.manifest = self.directory / "module-intake.json"
        self.gh = self.directory / "gh"
        self.gh.write_text(
            f"#!{sys.executable}\n"
            "import os, sys\n"
            "from pathlib import Path\n"
            "import json\n"
            "Path(os.environ['GH_CALL']).write_text(json.dumps(sys.argv[1:]))\n"
            "sys.stdout.write(os.environ['GH_RESULT'])\n"
            "sys.stderr.write(os.environ['GH_ERROR'])\n"
            "sys.exit(int(os.environ['GH_EXIT']))\n"
        )
        self.gh.chmod(0o755)
        self.write(manifest(release("5.4.0")))
        self.env = {
            **os.environ,
            "PATH": str(self.directory),
            "GH_CALL": str(self.directory / "gh-call.json"),
            "GH_RESULT": json.dumps([verification_result()]),
            "GH_ERROR": "",
            "GH_EXIT": "0",
        }

    def write(self, document):
        self.manifest.write_text(json.dumps(document))

    def run_cli(self, *args):
        return subprocess.run(
            [sys.executable, "-B", intake.__file__, "--manifest", str(self.manifest), *args],
            check=False, capture_output=True, text=True, env=self.env,
        )

    def admit(self):
        return self.run_cli("admit", "eventstore", "5.4.0", IMAGE)

    def assert_refused(self, result, message):
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(message, result.stderr)
        self.assertEqual(result.stdout, "")

    def test_admits_pinned_digest_and_exact_certificate(self):
        result = self.admit()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(json.loads(result.stdout), {"image": IMAGE, "subjectDigest": DIGEST, "certificate": CERTIFICATE})
        args = json.loads(Path(self.env["GH_CALL"]).read_text())
        self.assertEqual(args[:3], ["attestation", "verify", f"oci://{IMAGE}@{DIGEST}"])
        self.assertIn(REPOSITORY, args)
        self.assertEqual(args[-2:], ["--format", "json"])

    def test_admits_the_second_image_of_the_selected_second_release(self):
        second = release("5.5.0")
        second_image = IMAGE + "-api"
        second_digest = "sha256:" + "f" * 64
        second["commit"] = "f" * 40
        second["images"].append({"name": second_image, "digest": second_digest})
        self.write(manifest(release("5.4.0"), second))
        certificate = {**CERTIFICATE, "sourceRepositoryDigest": second["commit"]}
        verified = verification_result(certificate, second_digest)
        verified["verificationResult"]["statement"]["subject"][0]["name"] = second_image
        self.env["GH_RESULT"] = json.dumps([verified])
        result = self.run_cli("admit", "eventstore", "5.5.0", second_image)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(json.loads(result.stdout), {"image": second_image, "subjectDigest": second_digest, "certificate": certificate})
        args = json.loads(Path(self.env["GH_CALL"]).read_text())
        self.assertEqual(args[2], f"oci://{second_image}@{second_digest}")

    def test_refuses_null_identity_without_invoking_gh(self):
        unattended = release("5.4.0")
        unattended["attestation"] = None
        self.write(manifest(unattended))
        self.assert_refused(self.admit(), "attestation-missing")
        self.assertFalse(Path(self.env["GH_CALL"]).exists())

    def test_refuses_absent_bundle(self):
        self.env.update(GH_EXIT="1", GH_RESULT="", GH_ERROR=f"No attestations found for subject {DIGEST}")
        self.assert_refused(self.admit(), "attestation-missing")

    def test_confirms_public_bundle_absence_after_an_authenticated_404(self):
        endpoint = f"https://api.github.com/repos/{REPOSITORY}"
        digest_lookup = f"{endpoint}/attestations/{DIGEST}"
        self.env.update(GH_EXIT="1", GH_RESULT="", GH_ERROR=f"\nError: HTTP 404: Not Found ({digest_lookup}?per_page=30)")
        document = intake.load(self.manifest)
        public = b'{"private":false}'
        cases = [
            ([public, HTTPError(digest_lookup, 404, "Not Found", None, None)], "attestation-missing"),
            ([public, b'{"attestations":[]}'], "attestation-missing"),
            ([public, b'{"attestations":[{"repository_id":1}]}'], "attestation-unverified"),
            ([public, HTTPError(digest_lookup, 403, "Forbidden", None, None)], "attestation-unverified"),
            ([public, b'{}'], "attestation-unverified"),
            ([public, TimeoutError()], "attestation-unverified"),
            ([b'{"private":true}'], "attestation-unverified"),
            ([HTTPError(endpoint, 404, "Not Found", None, None)], "attestation-unverified"),
        ]
        for responses, code in cases:
            with self.subTest(responses=responses), patch.dict(os.environ, self.env), patch.object(intake, "fetch", side_effect=responses) as fetch:
                with self.assertRaisesRegex(ValueError, f"^{code}:"):
                    intake.admit(document, "eventstore", "5.4.0", IMAGE)
                queries = [endpoint, f"{digest_lookup}?predicate_type=https%3A%2F%2Fslsa.dev%2Fprovenance%2Fv1"]
                self.assertEqual([call.args[0] for call in fetch.call_args_list], queries[:len(responses)])

    def test_other_404s_are_unverified(self):
        self.env.update(GH_EXIT="1", GH_RESULT="", GH_ERROR="Error: HTTP 404: Not Found (https://registry.example.com/v2/)")
        self.assert_refused(self.admit(), "attestation-unverified")

    def test_maps_gh_certificate_policy_failures_without_treating_signature_failures_as_identity(self):
        self.env.update(GH_EXIT="1", GH_RESULT="")
        for rule, field in [
            ("expected SourceRepositoryURI to be https://github.com/Example/Module, got https://github.com/Example/Other", "sourceRepositoryURI"),
            ("expected SourceRepositoryOwnerURI to be https://github.com/Example, got https://github.com/Other", "sourceRepositoryURI"),
            ("expected Issuer to be https://token.actions.githubusercontent.com, got https://token.actions.githubusercontent.com/enterprise -- if you have a custom OIDC issuer policy for your enterprise, use the --cert-oidc-issuer flag with your expected issuer", "issuer"),
        ]:
            with self.subTest(rule=rule):
                self.env["GH_ERROR"] = f"\nError: {rule}\n"
                self.assert_refused(self.admit(), f"attestation-mismatch: {IMAGE}@{DIGEST}: {field}")
        for reason in ['verifying with issuer "sigstore.dev": signature verification failed', 'provided artifact digests do not match digests in statement']:
            with self.subTest(reason=reason):
                self.env["GH_ERROR"] = f"\nError: {reason}\n"
                self.assert_refused(self.admit(), "attestation-unverified")

    def test_names_each_wrong_certificate_field(self):
        differences = {
            "issuer": "https://example.com",
            "runnerEnvironment": "self-hosted",
            "sourceRepositoryURI": "https://github.com/Example/Other",
            "sourceRepositoryDigest": SIGNER,
            "sourceRepositoryRef": "refs/heads/Main",
            "buildConfigURI": "https://github.com/Example/Module/.github/workflows/other.yml@refs/heads/main",
            "buildSignerURI": "https://github.com/Example/Builds/.github/workflows/domain-release.yml@refs/heads/main",
            "buildSignerDigest": COMMIT,
        }
        for field, value in differences.items():
            with self.subTest(field=field):
                certificate = {**CERTIFICATE, field: value}
                self.env["GH_RESULT"] = json.dumps([verification_result(certificate)])
                self.assert_refused(self.admit(), f"attestation-mismatch: {IMAGE}@{DIGEST}: {field}")

    def test_refuses_a_subject_digest_mismatch(self):
        self.env["GH_RESULT"] = json.dumps([verification_result(digest="sha256:" + "f" * 64)])
        self.assert_refused(self.admit(), "subjectDigest")

    def test_accepts_a_complete_match_among_verified_attestations(self):
        wrong_runner = verification_result({**CERTIFICATE, "runnerEnvironment": "self-hosted"})
        self.env["GH_RESULT"] = json.dumps([wrong_runner, verification_result()])
        self.assertEqual(self.admit().returncode, 0)

    def test_refuses_malformed_records_or_subjects_after_a_match(self):
        invalid_subject = verification_result()
        invalid_subject["verificationResult"]["statement"]["subject"].append({"digest": []})
        for results in ([verification_result(), {}], [verification_result(), invalid_subject], [invalid_subject]):
            with self.subTest(results=results):
                self.env["GH_RESULT"] = json.dumps(results)
                self.assert_refused(self.admit(), "attestation-unverified")

    def test_refuses_gh_failure_or_invalid_json(self):
        invalid_constant = json.dumps([{**verification_result(), "untrusted": float("nan")}])
        for code, output in [("1", ""), ("0", "Verified!"), ("0", "{}"), ("0", "[]"), ("0", "[{}]"), ("0", invalid_constant)]:
            with self.subTest(code=code, output=output):
                self.env.update(GH_EXIT=code, GH_RESULT=output)
                self.assert_refused(self.admit(), "attestation-unverified")

    def test_refuses_missing_gh(self):
        self.gh.unlink()
        self.assert_refused(self.admit(), "attestation-unverified")

    def test_refuses_gh_timeout(self):
        self.gh.write_text(f"#!{sys.executable}\nimport time\ntime.sleep(30)\n")
        with patch.dict(os.environ, self.env), patch.object(intake, "TIMEOUT", 0.05):
            with self.assertRaisesRegex(ValueError, "attestation-unverified.*timed out"):
                intake.admit(intake.load(self.manifest), "eventstore", "5.4.0", IMAGE)

    def test_classifies_all_releases_after_a_named_baseline(self):
        document = manifest(release("5.4.0", "breaking"), release("5.5.0"), release("5.6.0", "breaking"), release("5.7.0", "none"))
        self.write(document)
        result = self.run_cli("classify", "--baseline", "platform-working-42", "--baseline-release", "eventstore=5.4.0")
        self.assertEqual(result.returncode, 0, result.stderr)
        classification = json.loads(result.stdout)
        self.assertEqual(classification, {
            "workingBaseline": "platform-working-42",
            "baselineVersions": {"eventstore": "5.4.0"},
            "chain": [{"module": "eventstore", "version": version, "changeClass": change}
                      for version, change in [("5.5.0", "additive"), ("5.6.0", "breaking"), ("5.7.0", "none")]],
            "effective": "breaking",
        })
        self.assertEqual(json.loads(self.manifest.read_bytes())["classification"], classification)
        self.assertEqual(self.run_cli("--check").returncode, 0)
        result = self.run_cli("classify", "--baseline", "none")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(json.loads(result.stdout)["chain"][0]["version"], "5.4.0")

    def test_candidate_at_baseline_has_no_changes(self):
        document = manifest(release("5.4.0", "breaking"))
        result = intake.compute_classification(document["modules"], "platform-working-42", {"eventstore": "5.4.0"})
        self.assertEqual(result["effective"], "none")
        self.assertEqual(result["chain"], [])

    def test_new_module_contributes_its_first_release_to_a_named_baseline(self):
        document = manifest(release("5.4.0", "none"))
        document["modules"]["new-module"] = {"repository": "Example/NewModule", "releases": [release("1.0.0", "breaking")]}
        document["classification"] = intake.compute_classification(document["modules"], "none", {})
        self.write(document)
        result = self.run_cli("classify", "--baseline", "platform-working-42", "--baseline-release", "eventstore=5.4.0")
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(json.loads(result.stdout)["chain"], [{"module": "new-module", "version": "1.0.0", "changeClass": "breaking"}])
        self.assertEqual(json.loads(result.stdout)["effective"], "breaking")

    def test_check_refuses_invalid_schema(self):
        document = manifest(release("5.4.0"))
        document["schema"] = "v2"
        self.write(document)
        self.assert_refused(self.run_cli("--check"), "manifest.schema")
        self.write(manifest(release("5.4.0")))
        self.manifest.write_text(self.manifest.read_text().replace('"version": "5.4.0"', '"version": "5.4.0", "version": "5.5.0"', 1))
        self.assert_refused(self.run_cli("--check"), "duplicate JSON member 'version'")

    def test_check_refuses_out_of_order_or_duplicate_versions(self):
        for version in ("5.4.0", "5.5.0"):
            with self.subTest(version=version):
                self.write(manifest(release("5.5.0"), release(version)))
                self.assert_refused(self.run_cli("--check"), f"eventstore {version}: versions")

    def test_check_refuses_chain_drift_and_wrong_effective_class(self):
        document = manifest(release("5.4.0"), release("5.5.0"), release("5.6.0", "breaking"))
        document["classification"]["chain"].pop(1)
        self.write(document)
        self.assert_refused(self.run_cli("--check"), "'version': '5.5.0'")
        document["classification"] = intake.compute_classification(document["modules"], "none", {})
        document["classification"]["chain"].append(None)
        self.write(document)
        self.assert_refused(self.run_cli("--check"), "classification.chain drift")
        document["classification"] = intake.compute_classification(document["modules"], "none", {})
        document["classification"]["effective"] = "additive"
        self.write(document)
        self.assert_refused(self.run_cli("--check"), "classification.effective")

    def history(self, original):
        path = self.directory / "repository" / intake.MANIFEST
        path.parent.mkdir(parents=True)
        shutil.copyfile(intake.__file__, path.parent / "module_intake.py")
        path.write_text(json.dumps(original))
        self.git(path, "init", "-q")
        self.commit_manifest(path)
        return path

    def git(self, path, *args):
        return subprocess.run(
            ["git", "-c", "user.name=Intake test", "-c", "user.email=intake@example.com", *args],
            cwd=path.parents[1], check=True, capture_output=True, text=True,
        ).stdout.strip()

    def commit_manifest(self, path):
        self.git(path, "add", str(path))
        self.git(path, "commit", "-qm", "Record module intake")

    def check_git_manifest(self, path, *args):
        return subprocess.run(
            [sys.executable, "-B", str(path.parent / "module_intake.py"), *args],
            cwd=path.parents[1], capture_output=True, text=True,
        )

    def test_check_against_an_earlier_commit_refuses_a_reclassified_release(self):
        path = self.history(manifest(release("5.4.0")))
        base = self.git(path, "rev-parse", "HEAD")
        path.write_text(json.dumps(manifest(release("5.4.0"), release("5.5.0"))))
        result = self.check_git_manifest(path, "--check", "--against", base)
        self.assertEqual(result.returncode, 0, result.stderr)

        path.write_text(json.dumps(manifest(release("5.4.0", "none"), release("5.5.0"))))
        self.commit_manifest(path)
        self.assertEqual(self.check_git_manifest(path, "--check").returncode, 0)
        self.assert_refused(self.check_git_manifest(path, "--check", "--against", base), "eventstore 5.4.0: existing release entry is append-only")

    def test_check_refuses_changes_to_the_same_working_baseline_binding(self):
        document = manifest(release("5.4.0"), release("5.5.0"))
        baseline = "platform-working-42"
        document["classification"] = intake.compute_classification(document["modules"], baseline, {"eventstore": "5.4.0"})
        path = self.history(document)
        document["classification"] = intake.compute_classification(document["modules"], baseline, {"eventstore": "5.5.0"})
        path.write_text(json.dumps(document))
        self.assert_refused(self.check_git_manifest(path, "--check"), f"working baseline {baseline!r} already names different module versions")

    def test_check_still_compares_canonical_manifest_when_it_is_a_symlink(self):
        path = self.history(manifest(release("5.4.0")))
        self.write(manifest(release("5.4.0", "none")))
        path.unlink()
        path.symlink_to(self.manifest)
        refusal = "eventstore 5.4.0: existing release entry is append-only"
        self.assert_refused(self.check_git_manifest(path, "--check"), refusal)
        self.assert_refused(self.check_git_manifest(path, "--manifest", "eng/module-intake.json", "--check"), refusal)

    def test_add_resolves_artifacts_and_records_owner_declaration(self):
        raw = json.dumps({
            "schema": "hexalith.module-manifest.v2",
            "modules": [{"identity": {"moduleId": "eventstore"},
                         "lifecycle": {"classification": {"changeClass": "breaking"}}}],
        }).encode()
        package = b"downloaded package archive"
        command = ["--manifest", str(self.manifest), "add", "eventstore", "5.5.0", "--commit", COMMIT,
                   "--repository", REPOSITORY, "--package", "Example.EventStore", "--image", IMAGE, "--image-tag", "v5.5.0",
                   "--evidence", "https://example.com/release/5.5.0"]
        index = subprocess.CompletedProcess([], 0, stdout=json.dumps({
            "digest": DIGEST, "mediaType": "application/vnd.oci.image.index.v1+json",
        }).encode())
        with patch.object(intake, "fetch", side_effect=[raw, package]) as fetch, patch.object(intake.subprocess, "run", return_value=index) as inspect:
            intake.main(command)
        self.assertEqual(fetch.call_args_list[0].args, (f"https://raw.githubusercontent.com/{REPOSITORY}/{COMMIT}/{intake.DECLARATION}",))
        self.assertEqual(fetch.call_args_list[1].args, ("https://api.nuget.org/v3-flatcontainer/example.eventstore/5.5.0/example.eventstore.5.5.0.nupkg",))
        self.assertIn(f"{IMAGE}:v5.5.0", inspect.call_args.args[0])
        document = intake.load(self.manifest)
        first, entry = document["modules"]["eventstore"]["releases"]
        self.assertEqual(first, release("5.4.0"))
        self.assertEqual(entry["classification"], {"changeClass": "breaking", "declarationSha256": hashlib.sha256(raw).hexdigest()})
        self.assertEqual(entry["packages"], [{"id": "Example.EventStore", "sha256": hashlib.sha256(package).hexdigest()}])
        self.assertEqual(entry["images"], [{"name": IMAGE, "digest": DIGEST}])
        self.assertEqual(document["classification"]["effective"], "breaking")
        before = self.manifest.read_bytes()
        with patch.object(intake, "fetch", side_effect=[raw, package]), patch.object(intake.subprocess, "run", return_value=index):
            with self.assertRaisesRegex(ValueError, "must append a version after 5.5.0"):
                intake.main(command)
        self.assertEqual(self.manifest.read_bytes(), before)

    def test_add_refuses_a_single_platform_manifest(self):
        manifest_result = subprocess.CompletedProcess([], 0, stdout=json.dumps({
            "digest": DIGEST, "mediaType": "application/vnd.oci.image.manifest.v1+json",
        }).encode())
        with patch.object(intake.subprocess, "run", return_value=manifest_result):
            with self.assertRaisesRegex(ValueError, "expected an image index or manifest list"):
                intake.image_digest(IMAGE, "5.5.0")


if __name__ == "__main__":
    unittest.main(verbosity=2)
