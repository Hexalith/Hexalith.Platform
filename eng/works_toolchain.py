"""Platform-owned toolchain selected by the complete Works parity qualification."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess


def load(root):
    path = Path(root) / "config/works-proof-toolchain.json"
    pins = json.loads(path.read_text())
    if set(pins) != {"daprCli", "daprRuntime", "linuxAmd64DaprdSha256"} or any(
        not isinstance(pins[key], str) or not re.fullmatch(r"\d+\.\d+\.\d+", pins[key])
        for key in ("daprCli", "daprRuntime")
    ) or not re.fullmatch(r"[a-f0-9]{64}", pins.get("linuxAmd64DaprdSha256", "")):
        raise ValueError("Platform Works proof toolchain requires exact stable versions and a qualified Linux runtime hash")
    return pins


def qualify(root, runtime):
    """Refuse an unqualified runtime before any live fleet starts; retain actual tool identity."""
    pins = load(root)
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
        "pinSha256": hashlib.sha256((Path(root) / "config/works-proof-toolchain.json").read_bytes()).hexdigest()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--github-output", type=Path, required=True)
    args = parser.parse_args()
    pins = load(Path(__file__).resolve().parents[1])
    with args.github_output.open("a") as output:
        output.write("dapr-cli=" + pins["daprCli"] + "\ndapr-runtime=" + pins["daprRuntime"] + "\n")


if __name__ == "__main__":
    main()
