"""Verify the pinned Builds tool against its lock and release tag, and retain its exact artifact closure."""
import argparse
from contextlib import ExitStack
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zipfile

from builds_tuple import (GITLINK, TOOL_PACKAGE, load_binding, recorded_gitlink, resolve_builds_root, tool_framework,
                          validate_owner_pins)
from bound_builds import inspection_environment, materialize, verify_bound_checkout
from builds_publication import _read_official, verify_release
from builds_tool_lock import download, load_lock, locked_entry, require_locked, verify_signature

ROOT = Path(__file__).resolve().parents[1]


def git_output(*arguments, text=True):
    return subprocess.check_output(["git", *arguments], cwd=ROOT, env=inspection_environment(), text=text)


def source_state():
    status = git_output("status", "--porcelain", "--untracked-files=all")
    names = git_output("ls-files", "--cached", "--others", "--exclude-standard", "-z", text=False).decode().split("\0")
    entries = git_output("ls-files", "-v", "-z").split("\0")
    hidden = any(entry and (entry[0].islower() or entry.startswith("S ")) for entry in entries)
    hashes = {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest() for name in names if name and (ROOT / name).is_file()}
    digest = hashlib.sha256(json.dumps(hashes, sort_keys=True, separators=(",", ":")).encode()).hexdigest()
    return {"dirty": bool(status.strip()) or hidden, "status": status, "hiddenTrackedInputs": hidden,
            "inputHashes": hashes, "inputDigest": digest}


def recorded_preparation_context(binding=None, *, builds_root=None):
    """Bind the clean recorded owner and the producer checked out at its recorded Builds gitlink."""
    if (not (ROOT / ".git").exists()
            or Path(git_output("rev-parse", "--show-toplevel").strip()).resolve() != ROOT.resolve()):
        raise ValueError("Published artifact preparation requires its own physical Platform checkout.")
    revision = git_output("rev-parse", "HEAD").strip()
    state = source_state()
    if state["dirty"]:
        raise ValueError("CI requires clean recorded Platform source for published artifact preparation.")
    if binding is not None and load_binding() != binding:
        raise ValueError("Recorded Platform publication binding changed before artifact preparation.")
    gitlink = recorded_gitlink(ROOT, environment=inspection_environment())
    return {"platformRevision": revision, "platformSourceState": state,
            "platformRecordedBuildsRevision": gitlink,
            "boundProducerContext": verify_bound_checkout(ROOT, resolve_builds_root(ROOT) if builds_root is None else builds_root)}


def require_unchanged_recorded_context(original, binding=None):
    """Refuse owner revisions, source bodies or producer inputs changed during archive verification."""
    builds_root = original["boundProducerContext"].get("buildsRoot")
    if recorded_preparation_context(binding, builds_root=builds_root) != original:
        raise ValueError("Recorded Platform source or the bound published producer changed during artifact preparation.")


def verify_package_identity(extracted, version, source):
    """Read the release identity of the lock- and signature-verified NuGet archive."""
    nuspec = extracted / (TOOL_PACKAGE + ".nuspec")
    try:
        metadata = ET.parse(nuspec).getroot().findall("{*}metadata")
    except (OSError, ET.ParseError) as error:
        raise ValueError("The locked Builds archive has no readable NuGet manifest.") from error
    if len(metadata) != 1:
        raise ValueError("The locked Builds manifest must contain exactly one metadata element.")
    ids = metadata[0].findall("{*}id")
    versions = metadata[0].findall("{*}version")
    repositories = metadata[0].findall("{*}repository")
    if (len(ids) != 1 or ids[0].text != TOOL_PACKAGE
            or len(versions) != 1 or versions[0].text != version
            or len(repositories) != 1 or repositories[0].get("commit") != source):
        raise ValueError("The locked Builds manifest must identify " + TOOL_PACKAGE + " " + version + "+" + source + ".")


def main():
    metadata_token = os.environ.pop("GH_TOKEN", None)
    try:
        _prepare_artifacts(metadata_token)
    finally:
        if metadata_token is not None:
            os.environ["GH_TOKEN"] = metadata_token


def _prepare_artifacts(metadata_token):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--require-recorded", action="store_true")
    args = parser.parse_args()
    output = args.output.resolve()
    if output.exists():
        raise ValueError("Choose a new artifact directory; existing evidence is never replaced.")
    binding = load_binding()
    version = binding["version"]
    validate_owner_pins(binding, ROOT)
    # A stale or missing lock refuses before any download or side effect.
    lock = locked_entry(load_lock(), version)
    # Own the exact producer checkout for the entire verification, including the final recheck.
    with ExitStack() as cleanup:
        builds_root = None
        if args.require_recorded:
            temporary = cleanup.enter_context(tempfile.TemporaryDirectory(prefix="platform-builds-"))
            builds_root = materialize(ROOT, Path(temporary) / "Hexalith.Builds")["buildsRoot"]
        recorded = recorded_preparation_context(binding, builds_root=builds_root) if args.require_recorded else None
        revision = recorded["platformRevision"] if recorded is not None else git_output("rev-parse", "HEAD").strip()
        state = recorded["platformSourceState"] if recorded is not None else source_state()
        source = recorded["platformRecordedBuildsRevision"] if recorded is not None else recorded_gitlink(ROOT, environment=inspection_environment())
        release = verify_release(binding, source, read=lambda path: _read_official(path, metadata_token))
        output.mkdir(parents=True)
        archive = output / (TOOL_PACKAGE.lower() + "." + version + ".nupkg")
        archive.write_bytes(args.archive.read_bytes() if args.archive else download(version))
        # The lock names the exact package bytes; the signature is checked separately.
        require_locked(lock, archive.read_bytes())
        verification = verify_signature(archive)
        (output / "signature.stdout").write_text(verification.stdout)
        (output / "signature.stderr").write_text(verification.stderr)
        if verification.returncode:
            raise ValueError("NuGet repository signature verification failed; retained signature output explains why.")
        hashes = {}
        extracted = output / "published"
        with zipfile.ZipFile(archive) as package:
            for entry in package.infolist():
                if entry.is_dir():
                    continue
                destination = (extracted / entry.filename).resolve()
                if not destination.is_relative_to(extracted) or entry.filename in hashes:
                    raise ValueError("Unsafe or duplicate archive member.")
                payload = package.read(entry)
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(payload)
                hashes[entry.filename] = hashlib.sha256(payload).hexdigest()
        tool_prefix = "tools/" + tool_framework(version) + "/any/"
        tool = extracted / tool_prefix
        if tool_prefix + "Hexalith.Builds.Tooling.dll" not in hashes:
            raise ValueError("The locked Builds archive has no " + tool_prefix + "Hexalith.Builds.Tooling.dll.")
        host = tool / "g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll"
        if tool_prefix + "Hexalith.Builds.Module.Cli.dll" not in hashes:
            raise ValueError("The locked Builds archive has no " + tool_prefix + "Hexalith.Builds.Module.Cli.dll.")
        verify_package_identity(extracted, version, source)
        # AR-12 expected identities: per-file SHA-256 of the lock-verified archive.
        receipt = {"platformRevision": revision, "platformSourceState": state, "buildsRevision": source, "version": version,
                   "buildsGitlink": GITLINK, "toolLock": lock, "archiveSha256": hashlib.sha256(archive.read_bytes()).hexdigest(),
                   "files": hashes, "toolPrefix": tool_prefix,
                   "toolRoot": str(tool), "toolingAssembly": str(tool / "Hexalith.Builds.Tooling.dll"),
                   "eventStoreHostAssembly": str(host), "tupleBinding": binding, "release": release,
                   "qualification": "artifact identity only", "candidateAcceptance": False}
        if recorded is not None:
            require_unchanged_recorded_context(recorded, binding)
            receipt.update(platformRecordedBuildsRevision=recorded["platformRecordedBuildsRevision"],
                           boundProducerContext=recorded["boundProducerContext"])
        (output / "artifact-identity.json").write_text(json.dumps(receipt, indent=2) + "\n")
        print(str(tool))


if __name__ == "__main__":
    main()
