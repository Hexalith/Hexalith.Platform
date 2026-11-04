"""Read-only readiness audit for a future real run. Never dispatches or cancels."""
import datetime
import hashlib
import json
import re
import stat
import zipfile
from pathlib import PurePosixPath

UTC = datetime.timezone.utc
WORKFLOW = ".github/workflows/ci-lifecycle-qualification.yml"
REPOSITORY = "Hexalith/Hexalith.Parties"
IDENTITY = re.compile(r"[0-9a-f]{32}")


def instant(value):
    observed = datetime.datetime.fromisoformat(value.replace("Z", "+00:00"))
    assert observed.tzinfo is not None
    return observed


def audit(archive, run, jobs, artifact, expected_source, phase, expected_tool_manifest_hash):
    """Return evidence only after fresh actual source/job/phase/hold checks pass."""
    now = datetime.datetime.now(UTC)
    assert phase in ("startup", "execution")
    assert re.fullmatch(r"[0-9a-f]{40}", expected_source)
    assert run["head_sha"] == expected_source and run["event"] == "workflow_dispatch"
    assert run["path"] == WORKFLOW and run["status"] == "in_progress"
    assert run["conclusion"] is None and int(run["run_attempt"]) == 1
    assert run["repository"]["full_name"] == REPOSITORY
    assert artifact["name"] == "lifecycle-cancellation-ready" and not artifact["expired"]
    assert artifact["workflow_run"]["id"] == run["id"]
    assert artifact["workflow_run"]["head_sha"] == expected_source
    raw = archive.read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    assert artifact["digest"] == "sha256:" + digest
    qualifying = [job for job in jobs["jobs"] if job["name"] == "qualification"]
    assert len(qualifying) == 1
    job = qualifying[0]
    assert job["status"] == "in_progress" and job["conclusion"] is None
    waiting = [step for step in job["steps"] if step["name"] == "Await parent cancellation of this exact ready run"]
    assert len(waiting) == 1 and waiting[0]["status"] == "in_progress"
    assert not any(step["conclusion"] in ("failure", "cancelled") for step in job["steps"])
    with zipfile.ZipFile(archive) as content:
        names = content.namelist()
        assert len(names) == len(set(names))
        for member in content.infolist():
            path = PurePosixPath(member.filename)
            assert not path.is_absolute() and ".." not in path.parts
            assert "\\" not in member.filename and not stat.S_ISLNK(member.external_attr >> 16)

        def read(basename):
            found = [name for name in names if PurePosixPath(name).name == basename]
            assert len(found) == 1
            body = content.read(found[0])
            assert len(body) <= 4 * 1024 * 1024
            return json.loads(body)

        journal, inputs = read("observations.json"), read("inputs.json")
        assert journal["schema"] == "hexalith.ci-lifecycle-qualification.v1"
        assert journal["classification"] == "baseline-only"
        assert journal["source"] == inputs["source"] == expected_source
        assert inputs["candidateAcceptance"] is False
        assert inputs["toolManifestSha256"] == expected_tool_manifest_hash
        assert journal["mappingHash"] == inputs["mappingHash"]
        assert re.fullmatch(r"[0-9a-f]{64}", journal["mappingHash"])
        context = journal["executionContext"]
        assert inputs["executionContext"] == context
        assert context == {"kind": "ci", "repository": REPOSITORY,
                           "workflowRunId": str(run["id"]), "workflowAttempt": 1,
                           "jobId": "qualification", "runnerKind": "github-hosted",
                           "operatingSystem": "Linux", "disposable": True}
        owners = [owner for owner in journal["owners"] if owner["label"] == "cancel"]
        assert len(owners) == len(journal["owners"]) == 1
        owner = owners[0]
        assert IDENTITY.fullmatch(owner["runId"])
        assert owner["cancellationPhase"] == phase
        assert isinstance(owner["pid"], int) and owner["pid"] > 1
        assert owner["processGroup"] == owner["pid"] and owner["birth"].isdigit()
        assert not owner.get("cancellationSignals") and not owner.get("cancellationSnapshot")
        ready = instant(owner["cancellationReadyAtUtc"])
        assert 0 <= (now - ready).total_seconds() <= 20
        events = [item for item in journal["observations"] if item["kind"] == "actual-cancellation-ready"]
        assert len(events) == 1
        event = events[0]
        assert event["phase"] == phase and event["ownerRunId"] == owner["runId"]
        assert instant(event["readyAtUtc"]) == ready
        if phase == "startup":
            paused = owner["pausedHost"]
            assert isinstance(paused["pid"], int) and paused["pid"] > 1 and paused["birth"].isdigit()
            starts = [item for item in journal["observations"] if item["kind"] == "startup-cancellation-ready"]
            assert len(starts) == 1
            start = starts[0]
            assert start["ownerRunId"] == owner["runId"] and start["appHostProcessId"] == paused["pid"]
            assert start["actualOwnedContainers"] and instant(start["startupDeadlineUtc"]) > now
            assert not owner.get("admission") and not owner.get("executionCheckpoint")
        else:
            checkpoint = owner["executionCheckpoint"]
            assert checkpoint["runId"] == owner["runId"] and checkpoint["realPersistenceAssertionsPassed"] is True
            assert isinstance(checkpoint["processId"], int) and checkpoint["processId"] > 1
            assert checkpoint["birth"].isdigit()
            retained = read("cancel-execution-ready.json")
            assert retained["runId"] == checkpoint["runId"] and retained["processId"] == checkpoint["processId"]
            assert retained["realPersistenceAssertionsPassed"] is True
            admission = owner["admission"]
            assert event["admission"] == admission and admission["ownerRunId"] == owner["runId"]
            assert all(IDENTITY.fullmatch(admission[key]) for key in ("ownerRunId", "attachedRunId", "originalAttachmentRequestId", "environmentId"))
            assert admission["requestedHoldSeconds"] == admission["effectiveHoldSeconds"] == 240
            accepted, expires = instant(admission["acceptedAtUtc"]), instant(admission["expiresAtUtc"])
            assert accepted <= ready <= now < expires
            assert expires - accepted == datetime.timedelta(seconds=240)
            assert admission.get("endedAtUtc") is None and admission.get("outcome") is None
    return {"auditedAtUtc": now.isoformat(), "source": expected_source, "runId": run["id"],
            "attempt": 1, "jobId": job["id"], "artifactId": artifact["id"],
            "archiveSha256": digest, "phase": phase, "ownerRunId": owner["runId"],
            "mappingHash": journal["mappingHash"], "readinessAgeSeconds": (now - ready).total_seconds(),
            "scope": "Fresh immutable readiness plus active controller wait; actual signal-time phase/PID/hold and cleanup still require the final runner observations.",
            "cancelRequested": False, "nativeAcceptance": False, "candidateAcceptance": False}
