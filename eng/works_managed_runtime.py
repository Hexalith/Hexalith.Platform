"""Bind the Works development proof to the measured shared .NET 11 runtime."""

import hashlib
from pathlib import Path
import re
import subprocess


VERSION = "11.0.0"
SCHEMA = "hexalith.works.managed-runtime.v1"
FRAMEWORKS = ("Microsoft.NETCore.App", "Microsoft.AspNetCore.App")
RUNTIME = re.compile(r"^(\S+) (\d+)\.(\d+)\.(\d+)(\S*) \[(.+)\]$")


def file_identity(path):
    path = Path(path).resolve(strict=True)
    return {"path": str(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}


def inventory(output=None):
    """Refuse missing, duplicate or higher runtimes before LatestMajor launches."""
    if output is None:
        output = subprocess.check_output(["dotnet", "--list-runtimes"], text=True, timeout=30)
    selected = {}
    observed = []
    for line in output.splitlines():
        match = RUNTIME.fullmatch(line.strip())
        if match is None:
            raise ValueError("Cannot read the installed managed runtime inventory")
        name, major, minor, patch, suffix, directory = match.groups()
        if name not in FRAMEWORKS:
            continue
        version = (int(major), int(minor), int(patch))
        observed.append({"name": name, "version": ".".join((major, minor, patch)) + suffix,
                         "directory": str(Path(directory).resolve(strict=True))})
        if version > (11, 0, 0):
            raise ValueError("Works development qualification requires the highest installed Core and ASP runtime to be " + VERSION)
        if version == (11, 0, 0) and not suffix:
            if name in selected:
                raise ValueError("Works development qualification requires one installation of each selected runtime")
            selected[name] = str((Path(directory) / VERSION).resolve(strict=True))
    if set(selected) != set(FRAMEWORKS):
        raise ValueError("Works development qualification requires both shared runtimes at " + VERSION)
    core = Path(selected[FRAMEWORKS[0]])
    asp = Path(selected[FRAMEWORKS[1]])
    return {"schema": SCHEMA, "version": VERSION, "frameworks": selected,
            "coreLib": file_identity(core / "System.Private.CoreLib.dll"),
            "json": file_identity(core / "System.Text.Json.dll"),
            "aspNetHosting": file_identity(asp / "Microsoft.AspNetCore.Hosting.dll"),
            "observedRuntimes": sorted(observed, key=lambda row: (row["name"], row["version"], row["directory"]))}


def require_process(record, expected):
    """Validate actual in-process or owned-PID observations against shared files."""
    if (not isinstance(record, dict) or type(record.get("pid")) is not int or record["pid"] <= 0
            or record.get("coreVersion") != VERSION):
        raise ValueError("A Works proof process did not record its actual managed runtime and PID")
    for name in ("coreLib", "json"):
        observed = record.get(name)
        if not isinstance(observed, dict) or set(observed) != {"path", "sha256"}:
            raise ValueError("A Works proof process has incomplete shared runtime file evidence")
        if observed != expected[name] or file_identity(observed["path"]) != observed:
            raise ValueError("A Works proof process did not load the measured trusted shared runtime " + name)
    return record


def require_start(record, expected):
    """Every actual start binds its host and exact runtime-module resource set."""
    value = record.get("managedRuntime")
    if not isinstance(value, dict) or set(value) != {"schema", "host", "resources"} or value["schema"] != SCHEMA:
        raise ValueError("A Works proof start is missing its actual managed runtime evidence")
    require_process(value["host"], expected)
    resources = value["resources"]
    modules = record.get("runtimeModules", [])
    names = [module.get("resource") for module in modules]
    if (not isinstance(resources, list) or not names or len(set(names)) != len(names)
            or any(not isinstance(item, dict) for item in resources)):
        raise ValueError("A Works proof start has an invalid managed resource inventory")
    actual_names = [item.get("resource") for item in resources]
    if len(actual_names) != len(set(actual_names)) or set(actual_names) != set(names):
        raise ValueError("A Works proof start did not bind every actual managed resource")
    pids = []
    for item in resources:
        require_process(item, expected)
        pids.append(item["pid"])
    if len(set(pids)) != len(pids) or value["host"]["pid"] in pids:
        raise ValueError("Actual Works services must record their own distinct process IDs")
    return value
