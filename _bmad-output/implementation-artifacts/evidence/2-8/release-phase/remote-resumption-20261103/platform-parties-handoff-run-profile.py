import json, os, subprocess, time, xml.etree.ElementTree as ET
from pathlib import Path
root = Path("/home/administrator/.cache/hexalith/story-2-8/platform-parties-handoff-20261104/Hexalith.Platform")
ev = Path(__file__).parent
assembly = root / "tests/Hexalith.Platform.Composition.Tests/bin/Release/net10.0/Hexalith.Platform.Composition.Tests.dll"
profile = json.loads((root / "eng/tuple-ci-suites.json").read_text())
env = dict(os.environ, NUGET_PACKAGES="/home/administrator/.cache/hexalith/story-2-8/tenants7-official-nuget-20261103", NUGET_CERT_REVOCATION_MODE="offline")
rows = []
for index, group in enumerate(profile["groups"], 1):
    stem = f"platform-parties-handoff-required-{index:02d}"
    xml = ev / (stem + ".xml")
    command = ["dotnet", str(assembly)]
    for selector in group["selectors"]: command += [group["filter"], selector]
    command += ["-result-xml", str(xml)]
    start = time.time()
    with (ev / (stem + ".log")).open("w") as output:
        result = subprocess.run(command, cwd=root, env=env, stdout=output, stderr=subprocess.STDOUT)
    totals = {key: sum(int(a.get(key, "0")) for a in ET.parse(xml).getroot().findall("assembly")) for key in ("total", "passed", "failed", "skipped", "not-run", "errors")}
    row = {"group": index, "command": command, "exitCode": result.returncode, "requiredCount": group["requiredCount"], "totals": totals, "seconds": round(time.time()-start,3)}
    rows.append(row)
    (ev / "platform-parties-handoff-required-summary.json").write_text(json.dumps({"source": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(), "groups":rows}, indent=2)+"\n")
    print(json.dumps(row), flush=True)
    if result.returncode or totals["total"] != group["requiredCount"] or totals["passed"] != group["requiredCount"] or any(totals[k] for k in ("failed", "skipped", "not-run", "errors")):
        raise SystemExit(1)
