"""Record exact final-review verification commands without shell interpolation."""
import json
import pathlib
import subprocess
import sys
import time

evidence = pathlib.Path(__file__).resolve().parent
root = evidence.parents[4]
label, repository, *argv = sys.argv[1:]
assert repository in {"platform", "builds"}
cwd = root if repository == "platform" else root / "references/Hexalith.Builds"
started = time.monotonic()
log_path = evidence / (label + ".log")
with log_path.open("w") as log:
    process = subprocess.run(argv, cwd=cwd, stdout=log, stderr=subprocess.STDOUT)
record = {"cwd": str(cwd), "argv": argv, "exitCode": process.returncode, "durationSeconds": round(time.monotonic() - started, 3), "log": log_path.name}
receipt = evidence / (label + ".command.json")
receipt.write_text(json.dumps(record, indent=2) + "\n")
print(json.dumps(record))
print("\n".join(log_path.read_text(errors="replace").splitlines()[-45:]))
sys.exit(process.returncode)
