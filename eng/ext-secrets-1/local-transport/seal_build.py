#!/usr/bin/env python3
"""Bind exact detached build inputs and artifacts before the local audit."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tarfile

from build import DAPR_ARCHIVE_SHA256, GO_ARCHIVE_SHA256, PIN, GO_VERSION, ROOT, isolated_go_environment


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def sdk_dependency_closure(assembly):
    assembly = Path(assembly).resolve()
    # Seal every app-local file, including managed/native assemblies, deps,
    # runtimeconfig, apphost and configuration/resources loaded from this folder.
    paths = [p for p in sorted(assembly.parent.rglob("*")) if p.is_file()]
    if any(not p.resolve().is_relative_to(assembly.parent) for p in paths):
        raise ValueError("SDK dependency escapes app-local closure")
    files = {str(p.resolve()): sha(p) for p in paths}
    if str(assembly) not in files or str(assembly.with_suffix(".deps.json")) not in files or str(assembly.with_suffix(".runtimeconfig.json")) not in files:
        raise ValueError("Executable, deps and runtimeconfig are required")
    return files


def fixture_closure(fixture, local=None):
    fixture = Path(fixture)
    local = Path(local) if local else Path(__file__).resolve().parent
    inventory = lambda root: {str(p.relative_to(root)) for p in root.rglob("*") if p.is_file() and "__pycache__" not in p.parts}
    if inventory(fixture) != inventory(local):
        raise ValueError("Exact reviewed fixture file inventory required")
    names = {str(p.relative_to(local)) for p in local.rglob("*.go")}
    if {str(p.relative_to(fixture)) for p in fixture.rglob("*.go")} != names:
        raise ValueError("Exact reviewed fixture Go source inventory required")
    for name in names | {"go.sum"}:
        if sha(fixture / name) != sha(local / name):
            raise ValueError("Built fixture source changed: " + name)
    expected_mod = (local / "go.mod").read_text().rstrip() + "\n\nreplace github.com/dapr/dapr => ../dapr-" + PIN + "\n"
    if (fixture / "go.mod").read_text() != expected_mod:
        raise ValueError("Only the pinned runtime module replacement is allowed")
    return {name: sha(fixture / name) for name in sorted(names | {"go.mod", "go.sum"})}


def seal(build, assembly):
    build, assembly = Path(build).resolve(), Path(assembly).resolve()
    archive_path = build / "source/dapr.tar.gz"
    toolzip = build / "tools/go-toolchain.zip"
    if sha(archive_path) != DAPR_ARCHIVE_SHA256 or sha(toolzip) != GO_ARCHIVE_SHA256:
        raise ValueError("Exact archive pins required")
    source = build / "source" / ("dapr-" + PIN)
    tree, changed = {}, []
    with tarfile.open(archive_path) as archive:
        for member in archive.getmembers():
            if not member.isfile():
                continue
            relative = str(Path(member.name).relative_to("dapr-" + PIN))
            original = hashlib.sha256(archive.extractfile(member).read()).hexdigest()
            actual = sha(source / relative)
            tree[relative] = actual
            if original != actual:
                changed.append(relative)
    if changed != ["pkg/api/grpc/server.go"]:
        raise ValueError("Runtime source delta must be exactly the retained header correction")
    actual_names = {str(p.relative_to(source)) for p in source.rglob("*") if p.is_file()}
    if actual_names != set(tree):
        raise ValueError("Unexpected/missing pinned runtime source file")
    fixture = build / "source/fixture"
    local = Path(__file__).resolve().parent
    fixture_sources = fixture_closure(fixture, local)
    go = build / "tools" / f"golang.org/toolchain@{GO_VERSION}/bin/go"
    environment = isolated_go_environment(build)
    for name in ("daprd-stable", "f1fixture"):
        info = subprocess.check_output([str(go), "version", "-m", str(build / "bin" / name)], env=environment).decode()
        (build / (name + "-build-info.log")).write_text(info)
    origins = {}
    for module, version, commit in (("github.com/dapr/components-contrib", "v1.18.0", "72e6a52340620aa41c4deecc40cbc6f0007b9afe"),
                                    ("google.golang.org/grpc", "v1.80.0", "397e45edaa68f8763773bbaaf539cf7894169cd2")):
        info_path = build / "go-modules/cache/download" / module / "@v" / (version + ".info")
        info = json.loads(info_path.read_text())
        if info["Origin"]["Hash"] != commit:
            raise ValueError("Pinned dependency origin mismatch")
        origins[module] = {"version": version, "commit": commit, "infoSha256": sha(info_path)}
    manifest = {"daprCommit": PIN, "runtimeFileCount": len(tree), "runtimeChangedFiles": changed,
                "archiveSha256": sha(archive_path), "goToolchainArchiveSha256": sha(toolzip),
                "patchedSourceSha256": tree["pkg/api/grpc/server.go"], "dependencyOrigins": origins,
                "fixtureSources": fixture_sources,
                "generatedSources": {name: digest for name, digest in tree.items() if name.startswith("pkg/proto/")},
                "retainedProtocols": {name: digest for name, digest in tree.items() if name in ("dapr/proto/components/v1/bindings.proto", "dapr/proto/components/v1/common.proto")},
                "binaries": {name: sha(build / "bin" / name) for name in ("daprd-stable", "f1fixture")},
                "sdkDriver": sdk_dependency_closure(assembly), "goBuildEnvironmentSha256": sha(build / "go-build-environment.json")}
    (build / "runtime-source-tree-sha256.json").write_text(json.dumps(tree, sort_keys=True, indent=2) + "\n")
    (build / "sealed-build.json").write_text(json.dumps(manifest, sort_keys=True, indent=2) + "\n")
    return manifest


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-root", required=True)
    parser.add_argument("--assembly", required=True)
    args = parser.parse_args()
    print(json.dumps(seal(args.build_root, args.assembly), sort_keys=True))
