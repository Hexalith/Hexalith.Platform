#!/usr/bin/env python3
"""Build detached pinned synthetic F1 inputs. Never initialize submodules."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tarfile
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
PIN = "4cef924a0b1360c69d810deec8cd59bf853f6721"
GO_VERSION = "v0.0.1-go1.26.4.linux-amd64"
DAPR_ARCHIVE_SHA256 = "321eabbc9124a516ec00fd8350db3e5950ceb2da66c35a223b55a32fddb7ab4e"
GO_ARCHIVE_SHA256 = "0221cfe82f9c88c717677cb5c1f59f598d177cd7eac18c11f90ae29432d356f9"
DOCS = ROOT / "docs/implementation/ext-secrets-1-f1-2026-10-20"


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write(path, value):
    Path(path).write_text(json.dumps(value, indent=2, sort_keys=True) + "\n")


def download(url, path):
    subprocess.run(["curl", "--fail", "--location", "--connect-timeout", "15", "--max-time", "240", url, "--output", str(path)], check=True)


def build(output, builds, eventstore):
    output = Path(output).resolve()
    if output.exists() or output.is_relative_to(ROOT):
        raise ValueError("A fresh detached build directory is required")
    output.mkdir(parents=True, mode=0o700)
    source, tools = output / "source", output / "tools"
    source.mkdir(); tools.mkdir(); (output / "bin").mkdir()
    toolzip = tools / "go-toolchain.zip"
    download(f"https://proxy.golang.org/golang.org/toolchain/@v/{GO_VERSION}.zip", toolzip)
    if sha(toolzip) != GO_ARCHIVE_SHA256:
        raise ValueError("Pinned Go toolchain archive digest mismatch")
    with zipfile.ZipFile(toolzip) as archive:
        archive.extractall(tools)
        for info in archive.infolist():
            if info.external_attr >> 16:
                (tools / info.filename).chmod(info.external_attr >> 16)
    go = tools / f"golang.org/toolchain@{GO_VERSION}/bin/go"
    archive_path = source / "dapr.tar.gz"
    download(f"https://codeload.github.com/dapr/dapr/tar.gz/{PIN}", archive_path)
    if sha(archive_path) != DAPR_ARCHIVE_SHA256:
        raise ValueError("Pinned Dapr source archive digest mismatch")
    with tarfile.open(archive_path) as archive:
        archive.extractall(source, filter="data")
    dapr = source / f"dapr-{PIN}"
    for line in (DOCS / "protocol/SHA256SUMS").read_text().splitlines():
        digest, name = line.split(maxsplit=1)
        retained = DOCS / "protocol" / name.lstrip("*")
        imported = dapr / name.lstrip("*")
        if sha(retained) != digest or sha(imported) != digest:
            raise ValueError("Retained and imported protocol source mismatch: " + name)
    profile = json.loads((DOCS / "transport-profile.json").read_text())
    patch = DOCS / profile["headerPatch"]["path"]
    server = dapr / "pkg/api/grpc/server.go"
    if sha(patch) != profile["headerPatch"]["sha256"] or sha(server) != profile["headerPatch"]["baseSourceSha256"]:
        raise ValueError("Pinned patch/base identity mismatch")
    subprocess.run(["git", "apply", "--check", str(patch)], cwd=dapr, check=True)
    subprocess.run(["git", "apply", str(patch)], cwd=dapr, check=True)
    if sha(server) != profile["headerPatch"]["resultSourceSha256"]:
        raise ValueError("Pinned patch result mismatch")
    fixture = source / "fixture"
    shutil.copytree(HERE, fixture, ignore=shutil.ignore_patterns("__pycache__"))
    environment = dict(os.environ, GOCACHE=str(output / "go-cache"), GOMODCACHE=str(output / "go-modules"),
                       GOTOOLCHAIN="local", CGO_ENABLED="0", GOMAXPROCS="6",
                       GOSUMDB="sum.golang.org https://proxy.golang.org/sumdb/sum.golang.org")
    commands = []

    def execute(command, cwd, log):
        commands.append({"argv": list(map(str, command)), "cwd": str(cwd), "log": log})
        write(output / "build-commands.json", commands)
        with (output / log).open("wb") as stream:
            subprocess.run(list(map(str, command)), cwd=cwd, env=environment, stdout=stream, stderr=subprocess.STDOUT, check=True, timeout=1800)

    execute([go, "build", "-tags", "stablecomponents", "-p", "4", "-trimpath", "-o", output / "bin/daprd-stable", "./cmd/daprd"], dapr, "daprd-stable-build.log")
    execute([go, "mod", "edit", f"-replace=github.com/dapr/dapr=../dapr-{PIN}"], fixture, "fixture-config.log")
    execute([go, "build", "-mod=readonly", "-p", "2", "-trimpath", "-o", output / "bin/f1fixture", "."], fixture, "fixture-build.log")
    execute(["dotnet", "build", ROOT / "tests/Hexalith.Platform.Secrets.Runtime.Tests/Hexalith.Platform.Secrets.Runtime.Tests.csproj", "-c", "Debug",
             "--artifacts-path", output / "dotnet-artifacts", "-m:1", "-p:NuGetAudit=false", "-p:MinVerVersionOverride=1.0.0",
             "-p:UseHexalithProjectReferences=true", f"-p:HexalithBuildsRoot={builds}", f"-p:HexalithEventStoreRoot={eventstore}"], ROOT, "dotnet-build.log")
    if "0 Warning(s)" not in (output / "dotnet-build.log").read_text():
        raise ValueError("Warning-free .NET build required")
    for name in ("daprd-stable", "f1fixture"):
        execute([go, "version", "-m", output / "bin" / name], source, name + "-build-info.log")
    write(output / "build-manifest.json", {"daprCommit": PIN, "archiveSha256": sha(archive_path), "goArchiveSha256": sha(toolzip),
          "patchSha256": sha(patch), "patchedSourceSha256": sha(server), "goVersion": "go1.26.4",
          "binaries": {name: sha(output / "bin" / name) for name in ("daprd-stable", "f1fixture")},
          "fixtureSources": {str(p.relative_to(fixture)): sha(p) for p in fixture.rglob("*") if p.is_file()}})


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True)
    parser.add_argument("--builds-root", required=True)
    parser.add_argument("--eventstore-root", required=True)
    args = parser.parse_args()
    build(args.output, args.builds_root, args.eventstore_root)
