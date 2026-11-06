"""Authenticate actual local recorded-profile files without executing applications."""
import argparse
import datetime
import hashlib
import io
import json
import subprocess
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument("lane", choices=["current", "legacy"])
args = p.parse_args()
ev = Path(__file__).resolve().parent
base = Path("/home/administrator/.cache/hexalith/story-2-8/platform-scoped-gateway-4448-source-union-20261106")
cache = Path("/home/administrator/.cache/hexalith/story-2-8/parent-platform-e810-hosted-20261106")
current = args.lane == "current"
source = "e810be0356da55b6db1794262c722e358470a093" if current else "b65de93fc57b4e04ac28e02a5df59be388bdac73"
count, group_count = (271, 18) if current else (349, 16)
label = "gateway-current-271-after-official-tag-preparation" if current else "gateway-legacy-349-first"
result_path = base / "evidence" / label / "result.json"
result = json.loads(result_path.read_bytes())
sha = lambda body: hashlib.sha256(body).hexdigest()


def git(*arguments, data=None):
    return subprocess.check_output(["git", "--git-dir", str(cache / "source.git"), *arguments], input=data)


assert result["expectedSource"] == source and result["status"] == "passed"
assert result["declaredRequiredCount"] == count
for key in ["ciEnvironmentImpersonated", "qualificationCredit", "candidateAcceptance", "nativeAcceptance"]:
    assert result[key] is False
assert result["sourceUnchanged"] is True
assert result["sourceStateBefore"] == result["sourceStateAfter"]
assert result["sourceStateBefore"]["dirty"] is False
hashes = result["sourceStateBefore"]["inputHashes"]
blobs = {entry.split(b"\t", 1)[1].decode() for entry in git("ls-tree", "-r", "-z", source).split(b"\0")
         if entry and entry.split(b" ", 2)[1] == b"blob"}
assert set(hashes) == blobs
stream = io.BytesIO(git("cat-file", "--batch", data="".join(source + ":" + name + "\n" for name in hashes).encode()))
for name, digest in hashes.items():
    header = stream.readline().split()
    assert header[1] == b"blob" and sha(stream.read(int(header[2]))) == digest, name
    assert stream.read(1) == b"\n"
assert not stream.read()
profile = git("show", source + ":eng/tuple-ci-suites.json")
assert result["profile"] == json.loads(profile)
if not current:
    assert sha(profile) == "61689f1598dac43a9fa28dcf5319008059e0fea0f1018209b1a8e79e4f63d7b0"
assert len(result["groups"]) == group_count
cases, groups = [], []
for group, expected in zip(result["groups"], result["profile"]["groups"]):
    body = Path(group["xml"]).read_bytes()
    assert sha(body) == group["xmlSha256"]
    xml = ET.fromstring(body)
    assembly, required = xml.find("assembly"), expected["requiredCount"]
    assert int(assembly.attrib["passed"]) == int(assembly.attrib["total"]) == required
    assert all(int(assembly.attrib[key]) == 0 for key in ["failed", "errors", "skipped", "not-run"])
    tests = xml.findall(".//test")
    assert len(tests) == required and all(test.attrib["result"] == "Pass" for test in tests)
    assert group["selectors"] == expected["selectors"]
    names = [test.attrib["name"] for test in tests]
    cases.extend(names)
    groups.append({"group": group["number"], "passed": required, "xmlSha256": sha(body), "cases": names})
assert len(cases) == len(set(cases)) == count
assert set(cases) == {test["name"] for test in result["cases"]}
original_name = "parent-platform4948-current271-local-independent-audit-20261106.json" if current else "parent-platform-894-same-source-original349-hosted-audit-20261105.json"
original = json.loads((ev / original_name).read_bytes())
assert set(cases) == {name for group in original["groups"] for name in group["cases"]}
for command in result["commands"]:
    assert command["exitCode"] == 0
    for channel in ["stdout", "stderr"]:
        body = (result_path.parent / (command["name"] + "." + channel)).read_bytes()
        assert len(body) == command[channel]["bytes"] and sha(body) == command[channel]["sha256"]
observed = result["compositionTestOutputs"]
assert observed["unchanged"] is True and observed["before"] == observed["after"]
bodies = [observed["before"]["testAssembly"], *observed["before"]["dependencies"].values()]
for body in bodies:
    assert sha(Path(body["path"]).read_bytes()) == body["sha256"]
for asset in result["packageGraphAssets"]:
    body = Path(asset["path"]).read_bytes()
    assert sha(body) == asset["sha256"]
    graph = json.loads(body)
    project = Path(graph["project"]["restore"]["projectPath"])
    for name, library in graph["libraries"].items():
        if library["type"] == "project":
            assert name.startswith("Hexalith.Platform."), name
            target = (project.parent / library["msbuildProject"]).resolve()
            assert target.is_relative_to(base / "Hexalith.Platform" / "src"), target
binding_name = "parent-builds4448-actual-qualified-producer-and-tuple-binding-20261106.json" if current else "parent-builds-4443-qualified-tuple-binding-20261105.json"
binding = json.loads((ev / binding_name).read_bytes())["binding"]
identity = result["publicArtifactIdentity"]
assert result["tupleBinding"] == identity["tupleBinding"] == binding
assert identity["platformRevision"] == source
assert identity["platformSourceState"] == result["sourceStateBefore"]
version = binding["publication"]["version"]
archive = base / "evidence" / ("current4448-published" if current else "legacy4443-published") / ("hexalith.builds.module.cli." + version + ".nupkg")
assert sha(archive.read_bytes()) == binding["publication"]["archiveSha256"]
with zipfile.ZipFile(archive) as package:
    assert set(package.namelist()) == set(identity["files"])
    retained = 0
    for name, digest in identity["files"].items():
        body = package.read(name)
        assert sha(body) == digest
        if not name.endswith("/"):
            assert (archive.parent / "published" / name).read_bytes() == body
            retained += 1
    test_directory = Path(observed["before"]["testAssembly"]["path"]).parent
    genuine = []
    for name, member in [("Hexalith.Builds.Tooling.dll", "tools/net10.0/any/Hexalith.Builds.Tooling.dll"),
                         ("Hexalith.Builds.Module.EventStoreHost.dll", "tools/net10.0/any/g4-host/bin/EventStoreHost/Hexalith.Builds.Module.EventStoreHost.dll")]:
        body = (test_directory / name).read_bytes()
        assert body == package.read(member)
        genuine.append({"file": name, "sha256": sha(body)})
output = ev / ("parent-platform-scoped-gateway-" + args.lane + "-local-independent-audit-20261106.json")
assert not output.exists()
receipt = {"utc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "source": source,
           "actualPassed": count, "groups": groups, "originalCaseNamesRetainedExactly": True,
           "allTrackedBlobInputsMatchedGit": len(hashes), "allCommandOutputsVerified": len(result["commands"]),
           "beforeAfterOutputsRehashed": len(bodies), "signedArchiveMembersVerified": len(identity["files"]),
           "extractedPublicFilesVerified": retained, "genuineTestOutputBodies": genuine,
           "resultSha256": sha(result_path.read_bytes()), "nativeAcceptance": False, "candidateAcceptance": False}
output.write_text(json.dumps(receipt, indent=2) + "\n")
print(json.dumps({key: value for key, value in receipt.items() if key != "groups"}))
