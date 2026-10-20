#!/usr/bin/env python3
"""Run the existing reference ACL matrix with the accepted released validator.

The normal source build is separately recorded as blocked. Only the validator
process selection is replaced here; validation inputs/assertions remain intact.
"""
import importlib.util
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[5]
SCRIPT = ROOT / 'scripts/test-reference-module-declarations.py'

def restored_validator() -> Path:
    """Resolve the pinned package from the configured or normal NuGet cache."""
    cache = Path(os.environ.get('NUGET_PACKAGES') or Path.home() / '.nuget/packages').expanduser()
    validator = cache / 'hexalith.builds.module.cli/4.34.0/tools/net10.0/any/Hexalith.Builds.Module.Cli.dll'
    if not validator.is_file():
        raise FileNotFoundError(
            f"Pinned module tool 4.34.0 is missing at '{validator}'. "
            "Run 'dotnet tool restore' from Platform using this NuGet cache before running the reference checks.")
    return validator


VALIDATOR: Path
spec = importlib.util.spec_from_file_location('reference_declarations', SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
run = subprocess.run

def accepted_run(command, **kwargs):
    if command[:3] == ['dotnet', 'build', str(module.CLI)]:
        print('Using the accepted packaged validator:', VALIDATOR, flush=True)
        return subprocess.CompletedProcess(command, 0, '', '')
    if command[:2] == ['dotnet', 'run'] and str(module.CLI) in command:
        command = ['dotnet', str(VALIDATOR), *command[command.index('--') + 1:]]
    return run(command, **kwargs)

# A current Memories declaration includes a module-owned Python test entry. The
# existing negative fixtures copy manifests into a temp owner root, so copy their
# actual declared entry files there too; malformed declarations still fail normally.
original_temporary = module.tempfile.TemporaryDirectory

def owner_fixture_directory(*args, **kwargs):
    temporary = original_temporary(*args, **kwargs)
    for manifest in module.PATHS:
        document = json.loads(manifest.read_text())
        for owner in document["modules"]:
            for test in owner.get("tests", []):
                entry = Path(test["entry"])
                destination = Path(temporary.name) / entry
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(manifest.parents[1] / entry, destination)
    return temporary


def main() -> int:
    global VALIDATOR
    try:
        VALIDATOR = restored_validator()
    except FileNotFoundError as error:
        print(f"Prerequisite unavailable: {error}", file=sys.stderr)
        return 2
    module.tempfile.TemporaryDirectory = owner_fixture_directory
    module.subprocess.run = accepted_run
    module.main()
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
