"""Verify the approved published Builds archive and retain its exact artifact closure."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import urllib.request
import zipfile

from builds_tuple import load_binding, validate_owner_pins, validate_recorded_source, verify_catalog

ROOT = Path(__file__).resolve().parents[1]


def source_state():
    status = subprocess.check_output(["git", "status", "--porcelain", "--untracked-files=all"], cwd=ROOT, text=True)
    names = subprocess.check_output(["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=ROOT).decode().split("\0")
    hashes = {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest() for name in names if name and (ROOT / name).is_file()}
    digest = hashlib.sha256(json.dumps(hashes, sort_keys=True, separators=(",", ":")).encode()).hexdigest()
    return {"dirty": bool(status.strip()), "status": status, "inputHashes": hashes, "inputDigest": digest}


def main():
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
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    state = source_state()
    if args.require_recorded:
        pin = subprocess.check_output(["git", "rev-parse", "HEAD:references/Hexalith.Builds"], cwd=ROOT, text=True).strip()
        validate_recorded_source(binding, pin, state["dirty"])
        verify_catalog(binding, ROOT / "references/Hexalith.Builds")
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
    tool = extracted / "tools/net10.0/any"
    if hashes["tools/net10.0/any/Hexalith.Builds.Tooling.dll"] != tooling_sha256:
        raise ValueError("Unexpected published Tooling identity.")
    host = tool / "g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll"
    executable_version = subprocess.check_output(["dotnet", str(tool / "Hexalith.Builds.Module.Cli.dll"), "--version"], text=True).strip()
    if executable_version != version + "+" + source:
        raise ValueError("Published executable source/version does not match the approved tuple.")
    receipt = {"platformRevision": revision, "platformSourceState": state, "buildsRevision": source, "version": version,
               "archiveSha256": archive_sha256, "files": hashes,
               "toolRoot": str(tool), "toolingAssembly": str(tool / "Hexalith.Builds.Tooling.dll"),
               "eventStoreHostAssembly": str(host), "tupleBinding": binding, "qualification": "artifact identity only", "candidateAcceptance": False}
    (output / "artifact-identity.json").write_text(json.dumps(receipt, indent=2) + "\n")
    print(str(tool))


if __name__ == "__main__":
    main()
