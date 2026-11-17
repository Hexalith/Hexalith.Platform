"""Verify the approved published Builds archive and retain its exact artifact closure."""
import argparse
from contextlib import ExitStack
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import urllib.request
import zipfile

from builds_tuple import load_binding, resolve_builds_root, validate_owner_pins
from bound_builds import inspection_environment, materialize, verify_bound_checkout
from builds_publication import _read_official, verify_handoff

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
    """Bind the clean recorded owner and the independently authenticated published producer."""
    if (not (ROOT / ".git").exists()
            or Path(git_output("rev-parse", "--show-toplevel").strip()).resolve() != ROOT.resolve()):
        raise ValueError("Published artifact preparation requires its own physical Platform checkout.")
    revision = git_output("rev-parse", "HEAD").strip()
    state = source_state()
    if state["dirty"]:
        raise ValueError("CI requires clean recorded Platform source for published artifact preparation.")
    if binding is not None and load_binding() != binding:
        raise ValueError("Recorded Platform publication binding changed before artifact preparation.")
    entry = git_output("ls-tree", "HEAD", "--", "references/Hexalith.Builds").strip().split()
    if len(entry) != 4 or entry[:2] != ["160000", "commit"] or entry[3] != "references/Hexalith.Builds":
        raise ValueError("Recorded Platform source must declare its actual Builds catalog gitlink.")
    return {"platformRevision": revision, "platformSourceState": state,
            "platformRecordedBuildsRevision": entry[2],
            "boundProducerContext": verify_bound_checkout(ROOT, resolve_builds_root(ROOT) if builds_root is None else builds_root)}


def require_unchanged_recorded_context(original, binding=None):
    """Refuse owner revisions, source bodies or producer inputs changed during archive verification."""
    builds_root = original["boundProducerContext"].get("buildsRoot")
    if recorded_preparation_context(binding, builds_root=builds_root) != original:
        raise ValueError("Recorded Platform source or the bound published producer changed during artifact preparation.")


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
    publication = binding["publication"]
    version, source = publication["version"], binding["producer"]["sourceRevision"]
    archive_sha256, tooling_sha256 = publication["archiveSha256"], publication["toolingSha256"]
    validate_owner_pins(binding, ROOT)
    # Own the exact producer checkout for the entire verification, including the final recheck.
    with ExitStack() as cleanup:
        builds_root = None
        if args.require_recorded:
            temporary = cleanup.enter_context(tempfile.TemporaryDirectory(prefix="platform-builds-"))
            builds_root = materialize(ROOT, Path(temporary) / "Hexalith.Builds")["buildsRoot"]
        recorded = recorded_preparation_context(binding, builds_root=builds_root) if args.require_recorded else None
        revision = recorded["platformRevision"] if recorded is not None else git_output("rev-parse", "HEAD").strip()
        state = recorded["platformSourceState"] if recorded is not None else source_state()
        handoff = verify_handoff(binding, read=lambda path: _read_official(path, metadata_token))
        output.mkdir(parents=True)
        archive = output / ("hexalith.builds.module.cli." + version + ".nupkg")
        if args.archive:
            archive.write_bytes(args.archive.read_bytes())
        else:
            url = "https://api.nuget.org/v3-flatcontainer/hexalith.builds.module.cli/" + version + "/" + archive.name
            with urllib.request.urlopen(url, timeout=90) as response:
                archive.write_bytes(response.read())
        if hashlib.sha256(archive.read_bytes()).hexdigest() != archive_sha256:
            raise ValueError("Published signed Builds archive SHA-256 differs from the approved observation.")
        verification = subprocess.run(["dotnet", "nuget", "verify", str(archive), "--all"], capture_output=True, text=True)
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
        framework = "net11.0" if version.startswith(("5.", "6.")) else "net10.0"
        tool_prefix = "tools/" + framework + "/any/"
        tool = extracted / tool_prefix
        if hashes[tool_prefix + "Hexalith.Builds.Tooling.dll"] != tooling_sha256:
            raise ValueError("Unexpected published Tooling identity.")
        host = tool / "g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll"
        executable_version = subprocess.check_output(["dotnet", str(tool / "Hexalith.Builds.Module.Cli.dll"), "--version"], text=True).strip()
        if executable_version != version + "+" + source:
            raise ValueError("Published executable source/version does not match the approved tuple.")
        receipt = {"platformRevision": revision, "platformSourceState": state, "buildsRevision": source, "version": version,
                   "archiveSha256": archive_sha256, "files": hashes,
                   "toolRoot": str(tool), "toolingAssembly": str(tool / "Hexalith.Builds.Tooling.dll"),
                   "eventStoreHostAssembly": str(host), "tupleBinding": binding, "publicationHandoff": handoff,
                   "qualification": "artifact identity only", "candidateAcceptance": False}
        if recorded is not None:
            require_unchanged_recorded_context(recorded, binding)
            receipt.update(platformRecordedBuildsRevision=recorded["platformRecordedBuildsRevision"],
                           boundProducerContext=recorded["boundProducerContext"])
        (output / "artifact-identity.json").write_text(json.dumps(receipt, indent=2) + "\n")
        print(str(tool))


if __name__ == "__main__":
    main()
