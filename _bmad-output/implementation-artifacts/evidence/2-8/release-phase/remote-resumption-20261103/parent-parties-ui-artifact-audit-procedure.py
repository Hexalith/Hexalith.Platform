"""Read actual completed Parties UI evidence. Never mutates a workflow or source."""
import base64
import datetime
import hashlib
import io
import json
import re
import stat
import subprocess
import sys
import zipfile
from pathlib import Path, PurePosixPath

def sha(body):
    return hashlib.sha256(body).hexdigest()

def safe_zip(content, limit):
    entries = content.infolist()
    assert len(entries) == len({item.filename for item in entries})
    assert sum(item.file_size for item in entries) <= limit
    for item in entries:
        name = PurePosixPath(item.filename)
        assert not name.is_absolute() and ".." not in name.parts and "\\" not in item.filename
        assert not stat.S_ISLNK(item.external_attr >> 16)
    return entries

def main():
    receipt_path, cache_path, official_path, output_path = map(Path, sys.argv[1:])
    receipt = json.loads(receipt_path.read_text())
    run, jobs = receipt["run"], receipt["jobs"]["jobs"]
    assert run["repository"]["full_name"] == "Hexalith/Hexalith.Parties"
    assert int(run["run_attempt"]) == 1
    tested = receipt["testedCommit"]
    if run["event"] == "pull_request":
        assert run["head_sha"] in [parent["sha"] for parent in tested["parents"]]
    else:
        assert run["event"] == "push" and tested["sha"] == run["head_sha"]
    selected = [job for job in jobs if job["name"] == "UI accessibility"]
    assert len(selected) == 1 and selected[0]["conclusion"] == "success"
    artifacts = [a for a in receipt["artifacts"]["artifacts"] if a["name"] == "ui-a11y-results"]
    assert len(artifacts) == 1
    artifact = artifacts[0]
    assert not artifact["expired"] and artifact["workflow_run"]["id"] == run["id"]
    assert artifact["workflow_run"]["head_sha"] == run["head_sha"]
    cache_path.mkdir(parents=True, exist_ok=True)
    archive = cache_path / "ui-a11y-results.zip"
    if not archive.exists():
        with archive.open("xb") as stream:
            subprocess.run(["gh", "api", f"repos/Hexalith/Hexalith.Parties/actions/artifacts/{artifact['id']}/zip"],
                           stdout=stream, check=True)
    raw = archive.read_bytes()
    assert len(raw) == artifact["size_in_bytes"] and "sha256:" + sha(raw) == artifact["digest"]
    official = json.loads(official_path.read_text())
    shell = next(package for package in official["packages"] if package["package"] == "Hexalith.FrontComposer.Shell")
    assert shell["version"] == "4.7.6" and shell["repositorySignatureExitCode"] == 0
    members, observations, errors = [], [], []
    with zipfile.ZipFile(archive) as content:
        for item in safe_zip(content, 256 * 1024 * 1024):
            if not item.is_dir():
                body = content.read(item)
                members.append({"path": item.filename, "bytes": len(body), "sha256": sha(body)})
        html = content.read("playwright-report/index.html").decode()
        encoded = re.search(r"data:application/zip;base64,([A-Za-z0-9+/=]+)", html)
        assert encoded
        with zipfile.ZipFile(io.BytesIO(base64.b64decode(encoded.group(1), validate=True))) as embedded:
            safe_zip(embedded, 64 * 1024 * 1024)
            report = json.loads(embedded.read("report.json"))
            cases = [case for file in report["files"] for case in file["tests"]]
            assert len(cases) == 68 and len({case["testId"] for case in cases}) == 68
            assert all(case["ok"] and case["outcome"] in ("expected", "flaky") for case in cases)
            assert report["stats"]["unexpected"] == report["stats"]["skipped"] == 0

            def walk(value):
                if isinstance(value, dict):
                    if value.get("name") == "frontcomposer-assets.json" and isinstance(value.get("body"), str):
                        actual = json.loads(value["body"])
                        assert actual["mode"] == "package" and actual["version"] == "4.7.6" and len(actual["assets"]) == 4
                        for asset in actual["assets"]:
                            name = asset["servedPath"].split("Hexalith.FrontComposer.Shell/", 1)[1]
                            assert asset["sha256"] == shell["payloadMembers"]["staticwebassets/" + name]
                        observations.append({"bodySha256": sha(value["body"].encode()), "body": actual})
                    if isinstance(value.get("errors"), list):
                        errors.extend(error for error in value["errors"] if isinstance(error, dict) and error.get("message"))
                    for child in value.values():
                        walk(child)
                elif isinstance(value, list):
                    for child in value:
                        walk(child)

            for name in embedded.namelist():
                if name != "report.json":
                    walk(json.loads(embedded.read(name)))
    assert len(observations) >= 6
    retried = [case for case in cases if len(case["results"]) > 1]
    audit = {
        "at": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "source": run["head_sha"], "testedSource": tested["sha"], "testedTree": tested["tree"]["sha"],
        "run": run["id"], "attempt": 1, "job": selected[0]["id"], "artifact": artifact,
        "cache": str(archive), "members": members, "stats": report["stats"], "actualCases": len(cases),
        "singleAttemptCases": len(cases) - len(retried), "retriedCases": retried,
        "actualServedAssetObservations": observations, "firstErrors": errors,
        "scope": "Actual hosted UI gate only. Original configured retries are reported, never relabelled as clean passes. Native and whole-story gates remain separate.",
        "candidateAcceptance": False, "storyComplete": False,
    }
    assert not output_path.exists()
    output_path.write_text(json.dumps(audit, indent=2) + "\n")
    print(json.dumps({"source": audit["source"], "run": run["id"], "stats": audit["stats"],
                      "singleAttemptCases": audit["singleAttemptCases"],
                      "retriedCases": len(retried), "servedAssetAttachments": len(observations)}, indent=2))

if __name__ == "__main__":
    main()
