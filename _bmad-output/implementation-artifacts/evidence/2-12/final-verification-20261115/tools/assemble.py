#!/usr/bin/env python3
"""Assemble the 2026-11-15 final verification evidence for story 2.12."""
import collections
import datetime
import hashlib
import json
import re
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path

FV = Path(__file__).resolve().parent.parent
OUT = Path("/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/evidence/2-12/final-verification-20261115")
ANSI = re.compile(r"\x1b\[[0-9;]*m")

records = [json.loads(line) for line in (FV / "checks.jsonl").read_text().splitlines()]
final = json.loads((FV / "collect-final.json").read_text())
package_assets = json.loads((FV / "collect-after-package.json").read_text())["restoredAssets"]
source_assets = json.loads((FV / "collect-source-observation.json").read_text())["restoredAssets"]

DISPOSITIONS = {
    "c-source-build": "FAILED (counted): canonical fresh Debug/source build. Builds Tooling GeneratePlatformVersionCatalog refuses the Platform SDK4.2 view selection propagated into Tooling; see sourceGateBlocker. Not caused by story-owned files.",
    "t-source-build": "FAILED (counted): canonical fresh Debug/source build. Restore NU1109 Microsoft.AspNetCore.OpenApi 11.0.0 (source EventStore.Gateway/Tenants) versus the Platform SDK4.2 view central 10.0.15; see sourceGateBlocker.",
    "t-source-enrollment-default": "NOT COUNTED: executed the stale 2026-11-13 assemblies left after both source builds failed (artifact mtimes/hashes recorded).",
    "t-source-live-consumer-and-cancellation": "NOT COUNTED: stale 2026-11-13 assembly predates 20a7d635 audience changes; the current launcher correctly refused it with exact-permission-inventory. Cleanup reported no leftovers.",
    "a-source-live-ordinary": "FAILED attempt 1 (retained): the pre-existing ordinary component path's `docker run` failed after 0.54 s; Docker events show its container created and destroyed within 0.1 s, consistent with the recorded R1 class of manually selected host-port refusal in this path (the harness withholds the daemon reason, so the cause is unverified). Attempt 2 on the identical assembly passed without code, budget or retry-policy changes.",
    "diag-c-source-build-normal": "DIAGNOSTIC: normal-verbosity repeat of the canonical Composition source build, retained as an excerpt to expose the catalog refusal text hidden by -v:q.",
}
for name in [r["name"] for r in records]:
    if name.startswith("supplementary-"):
        DISPOSITIONS[name] = ("SUPPLEMENTARY: explicit caller selection -p:HexalithEventStoreVersion=5.1.0 (the current authoritative SDK). "
                              "Shows the story sources build and pass in a coherent current source graph; does not close the canonical Debug/source gate.")

checks = []
for record in records:
    entry = dict(record)
    entry["disposition"] = DISPOSITIONS.get(record["name"], "PASS (counted)" if record["exitCode"] == 0 else "FAILED")
    checks.append(entry)
checks.append(dict(name="diag-c-source-build-normal", cwd="/home/administrator/projects/hexalith/platform",
    command=["dotnet", "build", "src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj", "-c", "Debug",
             "-p:UseHexalithProjectReferences=true", "-m:1", "-nodeReuse:false", "-v:n"], exitCode=1,
    outputSha256=hashlib.sha256((FV / "logs/diag-c-source-build-normal.log").read_bytes()).hexdigest(),
    disposition=DISPOSITIONS["diag-c-source-build-normal"]))


def classes(xml_path):
    counts = collections.defaultdict(collections.Counter)
    for test in ET.parse(xml_path).iter("test"):
        counts[test.get("type")][test.get("result")] += 1
    return {k: dict(v) for k, v in sorted(counts.items())}


STALE = {"t-source-enrollment-default"}
test_classes = {p.stem: classes(p) for p in sorted((FV / "xml").glob("*.xml")) if p.stem not in STALE}
test_classes["notCounted"] = {p.stem: classes(p) for p in sorted((FV / "xml").glob("*.xml")) if p.stem in STALE}

outputs = {}
for log in sorted((FV / "logs").glob("*.log")):
    text = log.read_text(errors="replace")
    if log.stem == "diag-c-source-build-normal":
        lines = text.splitlines()
        keep = [i for i, line in enumerate(lines) if "Platform version catalog" in line or "error MSB3073" in line or "Build FAILED" in line
                or re.search(r"^\s*\d+ (Warning|Error)\(s\)", line)]
        text = "\n".join(lines[i][:1200] for i in keep) + "\n[excerpt: catalog/error/summary lines only; full log hash in checks]\n"
    outputs[log.stem] = text

events = [json.loads(line) for line in (FV / "docker-events.jsonl").read_text().splitlines()]
owned = collections.defaultdict(dict)
for event in events:
    attributes = event["Actor"]["Attributes"]
    name = attributes.get("name", "")
    if not name.startswith(("hexalith-run-owned-", "hexalith-workload-component-")):
        continue
    item = owned[event["Actor"]["ID"]]
    item["name"] = name
    item["kind"] = ("run-owned authority" if name.startswith("hexalith-run-owned-workload-") else
                    "unrelated live control" if name.startswith("hexalith-run-owned-unrelated-") else "ordinary component authority")
    source = next((v for k, v in sorted(attributes.items()) if k.startswith("desktop.docker.io/binds/") and k.endswith("/Source") and v.startswith("/tmp/")), None)
    if source:
        item["privateDirectory"] = "/tmp/" + source.split("/")[2]
    item[event["Action"]] = datetime.datetime.fromtimestamp(event["timeNano"] / 1e9, datetime.timezone.utc).isoformat()
leftovers = dict(
    method="docker events (container create/destroy) for the whole verification window, plus labeled container and private-path snapshots before and after.",
    preSnapshot=(FV / "pre-snapshot.txt").read_text().splitlines(),
    postSnapshot=(FV / "post-snapshot.txt").read_text().splitlines(),
    observedContainers=len(owned),
    byKind=dict(collections.Counter(v["kind"] for v in owned.values())),
    createdWithoutDestroy=[v["name"] for v in owned.values() if "create" in v and "destroy" not in v],
    privateDirectoriesStillPresent=sorted(d for d in {v.get("privateDirectory") for v in owned.values()} if d and Path(d).exists()),
    remainingLabeledContainers=0 if len((FV / "post-snapshot.txt").read_text().splitlines()) == 1 else "see postSnapshot",
    note="The window overlapped another session's run-owned controls (story 12.2 in a separate Folders checkout); every observed authority, component and unrelated-control container was destroyed and no private directory remains, so no attribution split is needed for a zero result.")

source_gate = dict(
    status="blocked outside story-owned files",
    composition=dict(check="c-source-build", target="references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj(54,5) GeneratePlatformVersionCatalog",
        message="Platform version catalog validation failed: Catalog field Hexalith.EventStore.Aspire has inconsistent build selection '4.2.0'; authoritative catalog selects '5.1.0'. Change the catalog instead of overriding controlled selections."),
    compositionTests=dict(check="t-source-build", code="NU1109",
        message="Detected package downgrade: Microsoft.AspNetCore.OpenApi from 11.0.0 to centrally defined 10.0.15 (Hexalith.Platform.Composition.Tests -> Hexalith.Tenants -> Microsoft.AspNetCore.OpenApi >= 11.0.0; Hexalith.Platform.Identity -> Hexalith.EventStore.Gateway -> Microsoft.AspNetCore.OpenApi >= 11.0.0)."),
    cause=("Builds v6.0.3 (commit 58e1a5f4, 'fix(catalog): restore the qualified Platform package view', 2026-11-14) places "
           "Hexalith.Platform.Composition and Hexalith.Platform.Composition.Tests in the finite HexalithPlatformSdk42CatalogView "
           "(EventStore 4.2.0, Microsoft.AspNetCore.OpenApi 10.0.15, Memories 3.0.27, Tenants 7.0.7). In plain source mode Platform "
           "Directory.Build.targets (7a127bf3/c234e76b, 2026-11-09) propagates the computed HexalithEventStoreVersion=4.2.0 to every "
           "project reference, including Builds Tooling, whose catalog gate accepts only the authoritative 5.1.0; source Tenants 8 and "
           "EventStore 5.1 Gateway also require OpenApi 11. Package mode is coherent with that view and passes."),
    ownership="Builds catalog/tuple owner (pinned release tag v6.0.3, bound by Platform 266f643f) and Platform build propagation; neither is a story 2.12 file. Per the final-verification task, diagnostics are retained and no edit was made.",
    supplementary="With the explicit caller selection -p:HexalithEventStoreVersion=5.1.0 both Platform source builds pass with 0 warnings/0 errors, 37 default enrollment cases pass and both explicit lanes pass (supplementary-* checks).")

requirements = [
    ("fresh Debug/source build: Composition (C)", "FAILED - blocked outside story (sourceGateBlocker); supplementary explicit-SDK build passes"),
    ("fresh Debug/source build: Composition.Tests (T)", "FAILED - blocked outside story (sourceGateBlocker); supplementary explicit-SDK build passes"),
    ("fresh Debug/source build: Folders AppHost.Tests (A)", "PASS 0 warnings/0 errors"),
    ("fresh Debug/package builds: C, T, A", "PASS 0 warnings/0 errors each"),
    ("T enrollment class default, package", "PASS 37 total, 0 failed, 1 explicit not run"),
    ("T explicit consumer/fresh-host and cancellation lanes, package", "PASS 1+1 executed; provisioned=true; adoptionCancelled=true; 0 leftovers"),
    ("T enrollment class default + explicit lanes, source", "NOT EXECUTED on a canonical source build (build blocked); supplementary explicit-SDK source: PASS 37 + 1 + 1"),
    ("A enrollment classes default, source and package", "PASS 14 total, 0 failed, 6 explicit not run, each mode"),
    ("A ordinary live consumer lane, source and package", "PASS 5/5 each mode (source attempt 1 failed at docker run; retained)"),
    ("A run-owned live consumer lane, source and package", "PASS 1/1 each mode; provisioned=true; 0 leftovers"),
    ("Folders UI regressions (SafeCopyIdTests, CorrelationCopyButtonTests, UserContextAccessorRegistrationTests), source and package", "PASS 13/13 each mode (2+6+5)"),
    ("Folders Contracts deployment/OpenApi conformance classes changed for net11 paths (+McpCli PackedContractsClosureTests), source and package", "PASS 303/303 each mode"),
    ("K mvn -B -ntp package", "PASS 23 provider tests; JAR sha256 2de2c806..."),
    ("K python3 -B -m unittest test_run_owned", "PASS 44/44"),
    ("K verify_component.py --run-owned", "PASS 19 fresh RSA probes, 5 real-container terminal controls, UID 1001 caller"),
    ("independent zero-leftover observation", "PASS 0 labeled containers, 0 private run directories"),
]

assemblies = {}
for record in records:
    for path, value in record.get("artifactsAfter", {}).items():
        assemblies.setdefault(path, {})[record["name"]] = value

verification = dict(
    schema="hexalith.run-owned-enrollment-final-verification/v1",
    qualification="run-owned enrollment",
    recordedAt=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    task="spec-2-12 'Final verification of the committed current bytes (2026-11-15 resume)'",
    environment=dict(dotnetSdk="11.0.100", pwsh="7.6.2 via ~/.dotnet/tools on PATH", docker="29.8.2 (Docker Desktop WSL2)",
        keycloakImage="quay.io/keycloak/keycloak:26.6.4 sha256:0aae0de7fca85525f727d3354df17896092de8bb26ae4c12d89c77e5df8cbce4",
        buildFlags="-c Debug -m:1 -nodeReuse:false -v:q", testRunner="dotnet exec <net11.0 DLL> (xUnit v3 in-process runner); zero-test runs never counted"),
    repositories=final["repositories"],
    sourceSha256=final["sources"],
    providerJarSha256=final["sources"]["Platform"]["infrastructure/keycloak-workload-binding/target/keycloak-workload-binding-1.0.0-SNAPSHOT.jar"],
    requirements=[dict(requirement=r, result=s) for r, s in requirements],
    sourceGateBlocker=source_gate,
    testClasses=test_classes,
    finalAssemblies=assemblies,
    restoredAssets=dict(package=package_assets, sourceObservation=dict(
        note="Folders entries are the canonical source graph used by a-/ui-/contracts-source checks (re-restored afterwards for this observation, then restored back to package mode). Platform entries are the supplementary explicit-SDK 5.1.0 source graph.",
        assets=source_assets)),
    leftovers=leftovers,
    checks=checks,
    limitations=[
        "The canonical Platform Composition/Composition.Tests Debug/source builds fail (sourceGateBlocker); the source-mode T enrollment runs therefore exist only as supplementary explicit-SDK evidence.",
        "Platform package mode resolves the finite SDK4.2 view (EventStore 4.2.0, Memories 3.0.27, Tenants 7.0.7) selected by the Builds 6.0.3 catalog owner, not the latest EventStore 5.1.0; Folders package mode resolves EventStore 5.1.0, Memories 4.0.0, Tenants 8.0.0.",
        "Strict Folders acceptance, Folders 12.2 completion, Platform lifecycle qualification and new SDK cohort qualification are not claimed.",
        "Story-owned sources, revisions and gitlinks were identical before and after the run; no tracked file was edited by this verification except the spec record.",
    ])

if OUT.exists():
    shutil.rmtree(OUT)
(OUT / "xunit").mkdir(parents=True)
(OUT / "tools").mkdir()
(OUT / "verification.json").write_text(json.dumps(verification, indent=1) + "\n")
(OUT / "outputs.json").write_text(json.dumps(outputs, indent=1) + "\n")
for xml in sorted((FV / "xml").glob("*.xml")):
    if not xml.name.startswith("contracts-") and xml.stem not in STALE:
        shutil.copyfile(xml, OUT / "xunit" / xml.name)
for tool in ("check.py", "phase.sh", "collect.py", "scan.py", "assemble.py"):
    shutil.copyfile(FV / "tools" / tool, OUT / "tools" / tool)
print(OUT)
