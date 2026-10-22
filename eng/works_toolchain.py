"""Platform-owned toolchain selected by the complete Works parity qualification.

The Builds catalog (``HexalithDaprRuntimeVersion`` / ``HexalithDaprCliVersion``) is the single version authority
for the Works fleet: Platform composes the Dapr control plane from it, and this helper derives the sidecar
runtime and CLI from the same catalog. ``config/works-proof-toolchain.json`` only binds the exact qualified Linux
daprd bytes to the catalog runtime they were qualified for; a catalog change refuses until that hash is requalified.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

PIN = "config/works-proof-toolchain.json"
STABLE = re.compile(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)")
DIGEST = re.compile(r"[a-f0-9]{64}")


def builds_catalog(root, environ=None):
    """Resolve the Builds catalog exactly as Platform's Directory.Build.props resolves HexalithBuildsRoot."""
    root = Path(root)
    environ = os.environ if environ is None else environ
    configured = environ.get("HexalithBuildsRoot", "").strip()
    if configured:
        builds = Path(configured) if Path(configured).is_absolute() else root / configured
    elif (root.parent / "Hexalith.Builds/Props/Directory.Packages.props").is_file():
        builds = root.parent / "Hexalith.Builds"
    else:
        builds = root / "references/Hexalith.Builds"
    return builds / "Props/Directory.Packages.props"


def catalog_version(catalog, name):
    """Read one exact stable Builds catalog pin; conditional or repeated selections are refused."""
    values = [item for item in ET.parse(catalog).getroot().iter(name)]
    if len(values) != 1 or values[0].get("Condition") is not None:
        raise ValueError("The Builds catalog must select exactly one unconditional " + name + ": " + str(catalog))
    value = values[0].text or ""
    if not STABLE.fullmatch(value):
        raise ValueError("The Builds catalog " + name + " must be an exact stable version: " + repr(value))
    return value


def load(root, environ=None):
    """Derive the Works proof toolchain from the Builds catalog and bind its qualified Linux runtime hash."""
    path = Path(root) / PIN
    pins = json.loads(path.read_text())
    if not isinstance(pins, dict) or set(pins) != {"qualifiedDaprRuntime", "linuxAmd64DaprdSha256"} or not isinstance(
        pins["qualifiedDaprRuntime"], str
    ) or not STABLE.fullmatch(pins["qualifiedDaprRuntime"]) or not isinstance(
        pins["linuxAmd64DaprdSha256"], str
    ) or not DIGEST.fullmatch(pins["linuxAmd64DaprdSha256"]):
        raise ValueError("Platform Works proof toolchain requires an exact stable qualified runtime and a lowercase Linux runtime hash")
    catalog = builds_catalog(root, environ)
    runtime = catalog_version(catalog, "HexalithDaprRuntimeVersion")
    cli = catalog_version(catalog, "HexalithDaprCliVersion")
    if runtime != pins["qualifiedDaprRuntime"]:
        raise ValueError("The Builds catalog selects Dapr runtime " + runtime + " but Platform qualified the Works proof runtime hash for "
                         + pins["qualifiedDaprRuntime"] + "; requalify the Linux runtime before using the new catalog pin")
    return {"daprCli": cli, "daprRuntime": runtime, "linuxAmd64DaprdSha256": pins["linuxAmd64DaprdSha256"],
            "catalog": str(catalog), "catalogSha256": hashlib.sha256(catalog.read_bytes()).hexdigest()}


def qualify(root, runtime, environ=None):
    """Refuse an unqualified runtime before any live fleet starts; retain actual tool identity."""
    pins = load(root, environ)
    runtime = Path(runtime).resolve(strict=True)
    runtime_version = subprocess.check_output([str(runtime), "--version"], text=True).strip()
    cli = subprocess.check_output(["dapr", "--version"], text=True)
    if runtime_version != pins["daprRuntime"] or not re.search(
        r"(?m)^CLI version:\s*" + re.escape(pins["daprCli"]) + r"\s*$", cli
    ):
        raise ValueError("Installed Dapr differs from Platform's qualified Works proof toolchain")
    digest = hashlib.sha256(runtime.read_bytes()).hexdigest()
    if digest != pins["linuxAmd64DaprdSha256"]:
        raise ValueError("Installed daprd differs from Platform's qualified Works proof runtime hash")
    return pins | {"runtime": str(runtime), "runtimeSha256": digest,
        "pinSha256": hashlib.sha256((Path(root) / PIN).read_bytes()).hexdigest()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--github-output", type=Path, required=True)
    args = parser.parse_args()
    pins = load(Path(__file__).resolve().parents[1])
    with args.github_output.open("a") as output:
        output.write("dapr-cli=" + pins["daprCli"] + "\ndapr-runtime=" + pins["daprRuntime"] + "\n")


if __name__ == "__main__":
    main()
