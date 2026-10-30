"""Verify the approved published Builds archive and retain its exact artifact closure."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import urllib.request
import zipfile

VERSION = "4.38.3"
SOURCE = "d1931b1e803144048d927fff16cb8119afde541e"
ARCHIVE_SHA256 = "6c074186a9bbfb66c558fe5d1090e1e35fdd58577c0396266074bd1cea8919fd"
TOOLING_SHA256 = "4e307577ae4fb02104914951a9905a1c9dd453447a80735443d24fbc83496823"
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
    manifest = json.loads((ROOT / ".config/dotnet-tools.json").read_bytes())
    if manifest["tools"]["hexalith.builds.module.cli"]["version"] != VERSION:
        raise ValueError("Approved archive must match the owner tool pin.")
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    state = source_state()
    if args.require_recorded:
        pin = subprocess.check_output(["git", "rev-parse", "HEAD:references/Hexalith.Builds"], cwd=ROOT, text=True).strip()
        if pin != SOURCE or state["dirty"]:
            raise ValueError("CI requires clean recorded Platform source with the exact approved Builds pin.")
    output.mkdir(parents=True)
    archive = output / ("hexalith.builds.module.cli." + VERSION + ".nupkg")
    if args.archive:
        archive.write_bytes(args.archive.read_bytes())
    else:
        url = "https://api.nuget.org/v3-flatcontainer/hexalith.builds.module.cli/" + VERSION + "/" + archive.name
        with urllib.request.urlopen(url, timeout=90) as response:
            archive.write_bytes(response.read())
    if hashlib.sha256(archive.read_bytes()).hexdigest() != ARCHIVE_SHA256:
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
    if hashes["tools/net10.0/any/Hexalith.Builds.Tooling.dll"] != TOOLING_SHA256:
        raise ValueError("Unexpected published Tooling identity.")
    host = tool / "g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll"
    version = subprocess.check_output(["dotnet", str(tool / "Hexalith.Builds.Module.Cli.dll"), "--version"], text=True).strip()
    if version != VERSION + "+" + SOURCE:
        raise ValueError("Published executable source/version does not match the approved tuple.")
    receipt = {"platformRevision": revision, "platformSourceState": state, "buildsRevision": SOURCE, "version": VERSION,
               "archiveSha256": ARCHIVE_SHA256, "files": hashes,
               "toolRoot": str(tool), "toolingAssembly": str(tool / "Hexalith.Builds.Tooling.dll"),
               "eventStoreHostAssembly": str(host), "qualification": "artifact identity only"}
    (output / "artifact-identity.json").write_text(json.dumps(receipt, indent=2) + "\n")
    print(str(tool))


if __name__ == "__main__":
    main()
