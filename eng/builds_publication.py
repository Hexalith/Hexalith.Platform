"""Authenticate the normal shared release handoff; this supplies no native qualification credit."""
import hashlib
import json
import urllib.request

API = "https://api.github.com/repos/Hexalith/Hexalith.Builds/"


def read_official(path):
    request = urllib.request.Request(API + path, headers={"User-Agent": "Hexalith-Platform-publication-verification/1.0",
                                                       "Accept": "application/vnd.github+json"})
    with urllib.request.urlopen(request, timeout=60) as response:
        raw = response.read(2_000_001)
    if len(raw) > 2_000_000:
        raise ValueError("Official release metadata exceeds its bound.")
    return json.loads(raw)


def verify_handoff(binding, read=read_official):
    publication = binding["publication"]
    if publication.get("kind") != "shared-release-handoff/v1":
        return None
    source, tag = binding["producer"]["sourceRevision"], publication["releaseTag"]
    release = read("releases/" + str(publication["releaseId"]))
    body = release.get("body")
    if release.get("id") != publication["releaseId"] or release.get("tag_name") != tag \
            or release.get("draft") is not False or release.get("prerelease") is not False \
            or not release.get("published_at") or not isinstance(body, str) \
            or hashlib.sha256(body.encode()).hexdigest() != publication["releaseBodySha256"]:
        raise ValueError("Official shared release identity or body differs from the bound publication.")
    markers = {"hexalith-release": source,
               "hexalith-release-run": str(publication["runId"]),
               "hexalith-release-artifact": str(publication["artifactId"]) + " sha256:" + publication["artifactSha256"],
               "hexalith-release-packages": "Hexalith.Builds.Evidence.Cli,Hexalith.Builds.Module.Cli",
               "hexalith-release-containers": ""}
    for name, expected in markers.items():
        observed = [line.partition(":")[2].strip() for line in body.splitlines() if line.startswith(name + ":")]
        if observed != [expected]:
            raise ValueError("Official shared release marker differs: " + name)
    reference = read("git/ref/tags/" + tag)["object"]
    for _ in range(4):
        if reference.get("type") == "commit":
            break
        if reference.get("type") != "tag":
            raise ValueError("Official release tag does not identify a commit.")
        reference = read("git/tags/" + reference["sha"])["object"]
    if reference.get("type") != "commit" or reference.get("sha") != source:
        raise ValueError("Official shared release tag differs from the bound producer source.")
    run = read("actions/runs/" + str(publication["runId"]))
    if run.get("id") != publication["runId"] or run.get("head_sha") != source \
            or run.get("status") != "completed" or run.get("conclusion") != "success" \
            or run.get("path") != ".github/workflows/builds-release.yml":
        raise ValueError("Original shared release run is not successful at the bound source.")
    artifact = read("actions/artifacts/" + str(publication["artifactId"]))
    owner = artifact.get("workflow_run", {})
    if artifact.get("id") != publication["artifactId"] or artifact.get("name") != "release" \
            or artifact.get("digest") != "sha256:" + publication["artifactSha256"] \
            or owner.get("id") != publication["runId"] or owner.get("head_sha") != source:
        raise ValueError("Original shared release artifact differs from the bound run and digest.")
    return {"releaseId": publication["releaseId"], "runId": publication["runId"],
            "artifactId": publication["artifactId"], "artifactSha256": publication["artifactSha256"],
            "releaseBodySha256": publication["releaseBodySha256"], "tagSource": source,
            "qualificationCredit": False, "legacyG4AcceptanceClaimed": False}
