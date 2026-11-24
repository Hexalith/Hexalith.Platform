"""Maintain the Platform module intake manifest. See docs/contracts/module-intake-v1.md."""

import argparse
from contextlib import contextmanager
import hashlib
from itertools import zip_longest
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import urlopen


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = Path("eng/module-intake.json")
SCHEMA = "hexalith.platform.module-intake.v1"
DECLARATION = "module/hexalith.module-manifest.v2.json"
CLASSES = ("none", "additive", "breaking")
ISSUER = "https://token.actions.githubusercontent.com"
PROVENANCE = "https://slsa.dev/provenance/v1"
TIMEOUT = 60
COMMIT = r"[0-9a-f]{40}"
SHA256 = r"[0-9a-f]{64}"
MODULE_ID = r"[a-z][a-z0-9]*(?:-[a-z0-9]+)*"
REPOSITORY = r"[A-Za-z0-9][A-Za-z0-9-]*/[A-Za-z0-9_][A-Za-z0-9_.-]*"
WORKFLOW = r"\.github/workflows/[A-Za-z0-9_][A-Za-z0-9_.-]*\.ya?ml"
REF = r"refs/(?:heads|tags)/[A-Za-z0-9_][A-Za-z0-9_/.-]*"
IMAGE = r"[a-z0-9]+(?:[.-][a-z0-9]+)+(?::[0-9]+)?(?:/[a-z0-9]+(?:(?:[._]|__|-+)[a-z0-9]+)*)+"
PACKAGE = r"[A-Za-z0-9_][A-Za-z0-9_.-]*"
VERSION = r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([a-z0-9-]+(?:\.[a-z0-9-]+)*))?"


def unique_members(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON member {key!r}")
        result[key] = value
    return result


def reject_constant(value):
    raise ValueError(f"invalid JSON constant {value!r}")


def read_json(raw, label):
    try:
        return json.loads(raw, object_pairs_hook=unique_members, parse_constant=reject_constant)
    except ValueError as error:
        raise ValueError(f"{label}: {error}") from error


def require_pattern(value, pattern, label):
    if not isinstance(value, str) or re.fullmatch(pattern, value) is None:
        raise ValueError(f"{label}: invalid value {value!r}")
    return value


def require_members(value, members, label):
    if not isinstance(value, dict) or set(value) != set(members.split()):
        raise ValueError(f"{label}: expected exactly {members}")


def require_list(value, label):
    if not isinstance(value, list) or not value:
        raise ValueError(f"{label}: expected a nonempty list")


def version_key(version, label):
    require_pattern(version, VERSION, label)
    major, minor, patch, prerelease = re.fullmatch(VERSION, version).groups()
    identifiers = []
    for part in prerelease.split(".") if prerelease else ():
        if part.isdecimal():
            if len(part) > 1 and part.startswith("0"):
                raise ValueError(f"{label}: prerelease number has leading zeroes")
            identifiers.append((0, int(part)))
        else:
            identifiers.append((1, part))
    return int(major), int(minor), int(patch), prerelease is None, identifiers


def validate_attestation(identity, label):
    if identity is None:
        return
    require_members(identity, "workflow ref buildsWorkflow buildsSha", label)
    require_pattern(identity["workflow"], WORKFLOW, f"{label}.workflow")
    require_pattern(identity["ref"], REF, f"{label}.ref")
    require_pattern(identity["buildsWorkflow"], f"{REPOSITORY}/{WORKFLOW}", f"{label}.buildsWorkflow")
    require_pattern(identity["buildsSha"], COMMIT, f"{label}.buildsSha")


def validate_artifacts(entries, name_field, name_pattern, digest_field, digest_pattern, label):
    require_list(entries, label)
    names = set()
    for entry in entries:
        require_members(entry, f"{name_field} {digest_field}", label)
        name = require_pattern(entry[name_field], name_pattern, f"{label}.{name_field}")
        if name.lower() in names:
            raise ValueError(f"{label}: duplicate {name}")
        names.add(name.lower())
        require_pattern(entry[digest_field], digest_pattern, f"{label}/{name}.{digest_field}")


def validate_release(release, label):
    require_members(release, "version commit packages images attestation evidence classification", label)
    require_pattern(release["commit"], COMMIT, f"{label}.commit")
    validate_artifacts(release["packages"], "id", PACKAGE, "sha256", SHA256, f"{label}.packages")
    validate_artifacts(release["images"], "name", IMAGE, "digest", f"sha256:{SHA256}", f"{label}.images")
    validate_attestation(release["attestation"], f"{label}.attestation")
    require_list(release["evidence"], f"{label}.evidence")
    for url in release["evidence"]:
        require_pattern(url, r"https://[^/\s]+/[^\s]+", f"{label}.evidence")
    if len(set(release["evidence"])) != len(release["evidence"]):
        raise ValueError(f"{label}.evidence: duplicate URL")
    declaration = release["classification"]
    require_members(declaration, "changeClass declarationSha256", f"{label}.classification")
    if declaration["changeClass"] not in CLASSES:
        raise ValueError(f"{label}.classification.changeClass: expected one of {', '.join(CLASSES)}")
    require_pattern(declaration["declarationSha256"], SHA256, f"{label}.classification.declarationSha256")


def compute_classification(modules, baseline, versions):
    require_pattern(baseline, r"[A-Za-z0-9][A-Za-z0-9._:/-]*", "classification.workingBaseline")
    if not isinstance(versions, dict) or (baseline == "none") != (not versions):
        raise ValueError("classification.baselineVersions: a named working baseline requires module versions")
    for module in versions:
        if module not in modules:
            raise ValueError(f"classification.baselineVersions: unknown baseline module {module}")
    chain = []
    for module, entry in sorted(modules.items()):
        releases = entry["releases"]
        if module in versions:
            recorded = [release["version"] for release in releases]
            if versions[module] not in recorded:
                raise ValueError(f"classification.baselineVersions: {module} {versions[module]!r} is absent")
            releases = releases[recorded.index(versions[module]) + 1:]
        chain.extend(
            {"module": module, "version": release["version"], "changeClass": release["classification"]["changeClass"]}
            for release in releases
        )
    result = {"workingBaseline": baseline}
    if versions:
        result["baselineVersions"] = dict(sorted(versions.items()))
    result["chain"] = chain
    result["effective"] = max((entry["changeClass"] for entry in chain), key=CLASSES.index, default="none")
    return result


def validate(document):
    require_members(document, "schema modules classification", "manifest")
    if document["schema"] != SCHEMA:
        raise ValueError(f"manifest.schema: expected {SCHEMA}")
    modules = document["modules"]
    if not isinstance(modules, dict) or not modules:
        raise ValueError("manifest.modules: expected a nonempty object")
    for name, entry in modules.items():
        require_pattern(name, MODULE_ID, "module key")
        require_members(entry, "repository releases", name)
        require_pattern(entry["repository"], REPOSITORY, f"{name}.repository")
        require_list(entry["releases"], f"{name}.releases")
        previous = None
        for release in entry["releases"]:
            version = release.get("version") if isinstance(release, dict) else None
            label = f"{name} {version}"
            order = version_key(version, f"{label}.version")
            if previous is not None and order <= previous:
                raise ValueError(f"{label}: versions must be in strictly increasing order")
            validate_release(release, label)
            previous = order

    stored = document["classification"]
    if not isinstance(stored, dict):
        raise ValueError("classification: expected an object")
    expected = compute_classification(modules, stored.get("workingBaseline"), stored.get("baselineVersions", {}))
    require_members(stored, " ".join(expected), "classification")
    if not isinstance(stored["chain"], list):
        raise ValueError("classification.chain: expected a list")
    missing = object()
    for actual_entry, expected_entry in zip_longest(stored["chain"], expected["chain"], fillvalue=missing):
        if actual_entry != expected_entry:
            wanted = "no entry" if expected_entry is missing else repr(expected_entry)
            found = "no entry" if actual_entry is missing else repr(actual_entry)
            raise ValueError(f"classification.chain drift: expected {wanted}, found {found}")
    if stored["effective"] != expected["effective"]:
        raise ValueError(f"classification.effective: expected {expected['effective']!r}, found {stored['effective']!r}")


def git(*args):
    return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, timeout=TIMEOUT)


def check_append_only(document, against):
    revision = git("rev-parse", "--verify", "--end-of-options", f"{against}^{{commit}}")
    if revision.returncode:
        raise ValueError(f"cannot resolve module intake history at {against!r}")
    ref = revision.stdout.decode().strip()
    files = git("ls-tree", "--name-only", ref, "--", MANIFEST.as_posix())
    if files.returncode:
        raise ValueError(f"cannot read module intake tree at {ref}")
    if not files.stdout:
        return
    previous = git("show", f"{ref}:{MANIFEST.as_posix()}")
    if previous.returncode:
        raise ValueError(f"cannot read module intake history at {ref}")
    old = read_json(previous.stdout, f"{ref}:{MANIFEST}")
    validate(old)
    old_baseline = old["classification"]
    current_baseline = document["classification"]
    baseline = old_baseline["workingBaseline"]
    if baseline != "none" and baseline == current_baseline["workingBaseline"]:
        if old_baseline["baselineVersions"] != current_baseline["baselineVersions"]:
            raise ValueError(f"working baseline {baseline!r} already names different module versions")
    for name, module in old["modules"].items():
        current = document["modules"].get(name)
        if current is None or current["repository"] != module["repository"]:
            raise ValueError(f"{name}.repository: existing module and repository are append-only")
        for index, release in enumerate(module["releases"]):
            if index >= len(current["releases"]) or current["releases"][index] != release:
                raise ValueError(f"{name} {release['version']}: existing release entry is append-only")


def fetch(url):
    with urlopen(url, timeout=TIMEOUT) as response:
        return response.read()


def package_digest(package, version):
    name = package.lower()
    return hashlib.sha256(fetch(f"https://api.nuget.org/v3-flatcontainer/{name}/{version}/{name}.{version}.nupkg")).hexdigest()


def image_digest(image, tag):
    result = subprocess.run(
        ["docker", "buildx", "imagetools", "inspect", f"{image}:{tag}", "--format", "{{json .Manifest}}"],
        check=True, capture_output=True, timeout=TIMEOUT,
    )
    manifest = read_json(result.stdout, f"{image}:{tag}")
    if not isinstance(manifest, dict) or manifest.get("mediaType") not in (
        "application/vnd.oci.image.index.v1+json", "application/vnd.docker.distribution.manifest.list.v2+json",
    ):
        raise ValueError(f"{image}:{tag}: expected an image index or manifest list")
    return require_pattern(manifest.get("digest"), f"sha256:{SHA256}", f"{image}:{tag} digest")


def declaration_classification(raw, module):
    declaration = read_json(raw, f"{module} owner declaration")
    try:
        if declaration["schema"] != "hexalith.module-manifest.v2" or not isinstance(declaration["modules"], list):
            raise ValueError
        entries = [entry for entry in declaration["modules"] if entry["identity"]["moduleId"] == module]
        if len(entries) != 1:
            raise ValueError
        change = entries[0]["lifecycle"]["classification"]["changeClass"]
        if change not in CLASSES:
            raise ValueError
    except (KeyError, TypeError, ValueError) as error:
        raise ValueError(f"{module} owner declaration: expected one v2 module with a valid lifecycle.classification.changeClass") from error
    return {"changeClass": change, "declarationSha256": hashlib.sha256(raw).hexdigest()}


def load(path):
    document = read_json(path.read_bytes(), path)
    validate(document)
    return document


@contextmanager
def current_manifest(path, initial):
    import fcntl

    while True:
        try:
            source = path.open("rb")
        except FileNotFoundError:
            if initial is None:
                raise
            yield initial, None
            return
        with source:
            fcntl.flock(source, fcntl.LOCK_EX)
            identity = os.fstat(source.fileno())
            # A writer ahead of us may have replaced the path while we waited.
            if not os.path.samestat(identity, path.stat()):
                continue
            document = read_json(source.read(), path)
            validate(document)
            yield document, identity
            return


@contextmanager
def edit(path, initial=None):
    path = path.resolve()
    with current_manifest(path, initial) as (document, identity):
        yield document
        validate(document)
        with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=path.parent, delete=False) as output:
            pending = Path(output.name)
            try:
                output.write(json.dumps(document, indent=2) + "\n")
                output.flush()
                os.fchmod(output.fileno(), identity.st_mode & 0o777 if identity else 0o644)
                os.fsync(output.fileno())
                if identity is None:
                    os.link(pending, path)  # Fail if another process created the manifest first.
                else:
                    os.replace(pending, path)
            finally:
                pending.unlink(missing_ok=True)


def add(args):
    require_pattern(args.module, MODULE_ID, "module")
    require_pattern(args.repository, REPOSITORY, "repository")
    require_pattern(args.commit, COMMIT, "commit")
    order = version_key(args.version, "version")
    require_pattern(args.image_tag, r"[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}", "image tag")
    attestation = read_json(args.attestation.read_bytes(), args.attestation) if args.attestation else None
    validate_attestation(attestation, "attestation")
    for package in args.package:
        require_pattern(package, PACKAGE, "package ID")
    for image in args.image:
        require_pattern(image, IMAGE, "image name")
    raw = fetch(f"https://raw.githubusercontent.com/{args.repository}/{args.commit}/{DECLARATION}")
    classification = declaration_classification(raw, args.module)
    release = {
        "version": args.version,
        "commit": args.commit,
        "packages": [{"id": package, "sha256": package_digest(package, args.version)} for package in sorted(args.package, key=str.lower)],
        "images": [{"name": image, "digest": image_digest(image, args.image_tag)} for image in sorted(args.image)],
        "attestation": attestation,
        "evidence": args.evidence,
        "classification": classification,
    }
    initial = {"schema": SCHEMA, "modules": {}, "classification": {"workingBaseline": "none"}}
    with edit(args.manifest, initial) as document:
        module = document["modules"].setdefault(args.module, {"repository": args.repository, "releases": []})
        if module["repository"] != args.repository:
            raise ValueError(f"{args.module}: existing repository is {module['repository']}")
        releases = module["releases"]
        if releases and order <= version_key(releases[-1]["version"], args.module):
            raise ValueError(f"{args.module} {args.version}: must append a version after {releases[-1]['version']}")
        releases.append(release)
        stored = document["classification"]
        document["classification"] = compute_classification(
            document["modules"], stored["workingBaseline"], stored.get("baselineVersions", {}),
        )


def classify(args):
    with edit(args.manifest) as document:
        stored = document["classification"]
        versions = {}
        for assignment in args.baseline_release:
            module, separator, version = assignment.partition("=")
            if not separator or not version or module in versions:
                raise ValueError(f"--baseline-release {assignment!r}: expected one MODULE=VERSION per module")
            versions[module] = version
        if args.baseline == stored["workingBaseline"]:
            recorded = stored.get("baselineVersions", {})
            if versions and versions != recorded:
                raise ValueError(f"working baseline {args.baseline!r} already names different module versions")
            versions = recorded
        result = compute_classification(document["modules"], args.baseline, versions)
        document["classification"] = result
    return result


def expected_certificate(repository, release):
    identity = release["attestation"]
    return {
        "issuer": ISSUER,
        "runnerEnvironment": "github-hosted",
        "sourceRepositoryURI": f"https://github.com/{repository}",
        "sourceRepositoryDigest": release["commit"],
        "buildConfigURI": f"https://github.com/{repository}/{identity['workflow']}@{identity['ref']}",
        "sourceRepositoryRef": identity["ref"],
        "buildSignerURI": f"https://github.com/{identity['buildsWorkflow']}@{identity['buildsSha']}",
        "buildSignerDigest": identity["buildsSha"],
    }


def missing_bundle(stderr, repository, digest):
    if f"No attestations found for subject {digest}".encode() in stderr:
        return True
    # A credential can receive a 404 for a bundle that is publicly readable.
    # Check the repository and repeat the exact digest lookup without credentials.
    endpoint = f"https://api.github.com/repos/{repository}"
    attestations = f"{endpoint}/attestations/{digest}"
    if f"HTTP 404: Not Found ({attestations}?".encode() not in stderr:
        return False
    try:
        metadata = read_json(fetch(endpoint), "GitHub repository")
        if not isinstance(metadata, dict) or metadata.get("private") is not False:
            return False
    except (OSError, ValueError):
        return False
    try:
        result = read_json(fetch(f"{attestations}?{urlencode({'predicate_type': PROVENANCE})}"), "GitHub attestations")
        return isinstance(result, dict) and result.get("attestations") == []
    except HTTPError as error:
        return error.code == 404
    except (OSError, ValueError):
        return False


def verify_image(repository, release, image):
    ref = f"{image['name']}@{image['digest']}"
    if release["attestation"] is None:
        raise ValueError(f"attestation-missing: {ref} has no recorded attestation identity")
    expected = expected_certificate(repository, release)
    # Scope the GitHub lookup to the source repo and verify signatures. Check the complete identity
    # in Python so a verified bundle with different certificate fields can be diagnosed precisely.
    command = [
        "gh", "attestation", "verify", f"oci://{ref}", "--hostname", "github.com", "--repo", repository,
        "--cert-oidc-issuer", ISSUER, "--cert-identity-regex", r"^https://github[.]com/",
        "--predicate-type", PROVENANCE, "--format", "json",
    ]
    try:
        response = subprocess.run(command, capture_output=True, timeout=TIMEOUT)
    except (OSError, subprocess.TimeoutExpired) as error:
        raise ValueError(f"attestation-unverified: {ref}: {error}") from error
    if response.returncode:
        rejected_field = re.search(
            rb"(?m)^Error: expected (SourceRepository(?:Owner)?URI|Issuer) to be [^,\r\n]+, got [^\r\n]*\r?$",
            response.stderr,
        )
        if rejected_field:
            field = "issuer" if rejected_field[1] == b"Issuer" else "sourceRepositoryURI"
            raise ValueError(f"attestation-mismatch: {ref}: {field}")
        if missing_bundle(response.stderr, repository, image["digest"]):
            raise ValueError(f"attestation-missing: {ref} has no GitHub attestation bundle")
        raise ValueError(f"attestation-unverified: {ref}: gh verify exited {response.returncode}")
    verified = read_json(response.stdout, f"attestation-unverified: {ref}: gh verify JSON")
    if not isinstance(verified, list):
        raise ValueError(f"attestation-unverified: {ref}: expected a JSON list of verification results")
    if not verified:
        raise ValueError(f"attestation-unverified: {ref}: gh verify returned no verification results")
    mismatches = set()
    matched = False
    for entry in verified:
        try:
            result = entry["verificationResult"]
            certificate = result["signature"]["certificate"]
            subjects = result["statement"]["subject"]
            if not isinstance(certificate, dict) or not isinstance(subjects, list) or not subjects:
                raise TypeError
            digests = [subject["digest"] for subject in subjects]
            if not all(isinstance(digest, dict) and all(isinstance(value, str) for value in digest.values()) for digest in digests):
                raise TypeError
            differences = {field for field, value in expected.items() if certificate.get(field) != value}
            if not any(digest.get("sha256") == image["digest"].removeprefix("sha256:") for digest in digests):
                differences.add("subjectDigest")
        except (KeyError, TypeError) as error:
            raise ValueError(f"attestation-unverified: {ref}: missing certificate or statement in gh verify JSON") from error
        matched |= not differences
        mismatches.update(differences)
    if matched:
        return {"image": image["name"], "subjectDigest": image["digest"], "certificate": expected}
    raise ValueError(f"attestation-mismatch: {ref}: {'; '.join(sorted(mismatches))}")


def admit(document, module_name, version, image_name):
    module = document["modules"].get(module_name)
    if module is None:
        raise ValueError(f"unknown module {module_name!r}")
    release = next((item for item in module["releases"] if item["version"] == version), None)
    if release is None:
        raise ValueError(f"{module_name}: unknown release {version!r}")
    image = next((item for item in release["images"] if item["name"] == image_name), None)
    if image is None:
        raise ValueError(f"{module_name} {version}: unknown image {image_name!r}")
    return verify_image(module["repository"], release, image)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=ROOT / MANIFEST)
    parser.add_argument("--check", action="store_true", help="Check schema, classification, and recorded release history")
    parser.add_argument("--against", help="Compare release history with this Platform Git revision (default HEAD)")
    subparsers = parser.add_subparsers(dest="command")
    subparsers.add_parser("validate", help="Validate the manifest and recompute its classification")
    add_parser = subparsers.add_parser("add", help="Append an owner release using published package bytes and image indexes")
    add_parser.add_argument("module")
    add_parser.add_argument("version")
    add_parser.add_argument("--repository", required=True)
    add_parser.add_argument("--commit", required=True)
    add_parser.add_argument("--package", action="append", required=True)
    add_parser.add_argument("--image", action="append", required=True)
    add_parser.add_argument("--image-tag", help="Registry tag for all images (default VERSION)")
    add_parser.add_argument("--evidence", action="append", required=True)
    add_parser.add_argument("--attestation", type=Path, help="JSON identity object (default null)")
    classify_parser = subparsers.add_parser("classify", help="Record and print classification for a working baseline")
    classify_parser.add_argument("--baseline", required=True)
    classify_parser.add_argument("--baseline-release", action="append", default=[], metavar="MODULE=VERSION")
    admit_parser = subparsers.add_parser("admit", help="Verify an image against its recorded digest and attestation identity")
    admit_parser.add_argument("module")
    admit_parser.add_argument("version")
    admit_parser.add_argument("image", help="Exact untagged image repository name from the manifest")
    args = parser.parse_args(argv)
    if args.check and args.command or args.against and not args.check or not (args.check or args.command):
        parser.error("choose --check or a subcommand; --against requires --check")
    if args.command == "add":
        args.image_tag = args.image_tag or args.version
        add(args)
        return
    if args.command == "classify":
        print(json.dumps(classify(args), indent=2))
        return
    document = load(args.manifest)
    if args.check and (args.against or Path(os.path.abspath(args.manifest)) == ROOT / MANIFEST):
        check_append_only(document, args.against or "HEAD")
    if args.command == "admit":
        print(json.dumps(admit(document, args.module, args.version, args.image), indent=2))


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, subprocess.SubprocessError) as failure:
        sys.exit(str(failure))
