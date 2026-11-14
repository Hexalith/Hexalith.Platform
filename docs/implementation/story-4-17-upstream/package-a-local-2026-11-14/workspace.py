import json, os, pathlib, subprocess, sys
from datetime import datetime, timezone
out = pathlib.Path(__file__).resolve().parent
source = pathlib.Path(os.environ["TMPDIR"]) / "components-contrib"
subprocess.run(["git", "clone", "--depth=1", "--branch=main", "https://github.com/dapr/components-contrib.git", str(source)], check=True)
subprocess.run(["git", "checkout", "--detach"], cwd=source, check=True)
def git(*args):
    return subprocess.check_output(["git", *args], cwd=source, text=True).strip()
identity = {"repository": git("remote", "get-url", "origin"), "requestedRef": "refs/heads/main", "commit": git("rev-parse", "HEAD"), "tree": git("rev-parse", "HEAD^{tree}"), "observedUtc": datetime.now(timezone.utc).isoformat(), "commitMetadata": git("show", "-s", "--format=%H%n%T%n%cI%n%s", "HEAD"), "initialStatus": git("status", "--porcelain"), "sourceUrl": "https://github.com/dapr/components-contrib/commit/" + git("rev-parse", "HEAD")}
(out / "source-identity.json").write_text(json.dumps(identity, indent=2) + "\n")
(out / "workspace-path.txt").write_text(str(source) + "\n")
print("WORKSPACE_READY " + str(source), flush=True)
for line in sys.stdin:
    if line.strip() == "cleanup":
        break
print("WORKSPACE_RELEASED", flush=True)
