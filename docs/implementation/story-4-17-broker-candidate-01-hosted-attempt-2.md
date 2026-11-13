# Story 4.17 — broker candidate-01, hosted attempt 2 (2026-11-13)

Status: **attempt consumed; packet retained; integrity NOT verified.** Platform 4.17 and
EventStore 3.21 remain **in-progress**. All five historical guarantees remain
**REJECTED**. Broker selection is **null** and production eligibility is **FAIL**.

The machine-readable assessment governs this summary:
[`attempt-assessment.json`](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-01-hosted-attempt-2/attempt-assessment.json).

## Outcome

Run [43333995506](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43333995506),
attempt 1, started on a GitHub-hosted runner 5 seconds after creation. It captured
all 24 vectors and uploaded its artifact. The pinned EventStore verifier then
rejected the packet in all three modes, before any vector was assessed:

```text
FAIL: platformConfig.execution: sidecar executable lacks the measured Dapr preflight binding
```

That rejection is a **demonstrated verifier defect** in EventStore
`02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213`, not a capture fault (see
[Defect](#demonstrated-verifier-defect)). As a result, no vector, cleanup or authority
result can be credited from this attempt.

## Authorization and accounting

The owner chose the "Immutable tag (Recommended)" option on 2026-11-13. It authorized
creating the tag, one dispatch, the 30-minute no-job rule, the artifact download and
the three local verifiers. Exactly one run was dispatched. There was no rerun, retry or
cancellation. The queue timeout was not needed.

The proposal's go/no-go checks all passed before the tag was created (see
`pre-dispatch-checks.json`, transcribed from session output):

- githubstatus reported all systems operational and no open incident;
- both rulesets matched the recorded policy;
- the tag did not exist yet;
- the workflow was active, with one run and nothing queued;
- the identities matched;
- no workflow triggers on tags;
- creating the tag started no run.

## Identities

| Item | Value |
| --- | --- |
| Dispatch ref | `refs/tags/qualification/broker-candidate-01/attempt-2-94001fad` (created 2026-11-13T00:03:19Z, HTTP 201) |
| Platform source | `94001fad925fdf7fa03a90b507b8f8e19074f9e3` (observed on the worker) |
| Workflow blob | `9130ed4243e3961326e984e8fd22e49b70695aa4` |
| EventStore gitlink | `160000 commit 02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213` (observed on the worker) |
| Candidate SHA-256 | `472bae6b4fc77448c96aa066c41469e689d50d0b98dcbea103333d06524208dc` (observed on the worker) |
| Profile | `deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json` |
| Job | `131585434513`, runner `GitHub Actions 1000081917`, `ubuntu-24.04`, image `20261111.127.1` |
| Artifact | `platform-broker-candidate-01-43333995506-1`, 2,666,335 bytes, SHA-256 `906c0d943e7a0ee8393bb83b96c4c917477fe18820afe37fe6ba7274afe7aeb7` (matches the API digest), expires 2026-12-13T00:11:32Z |

## Results

Raw exits recorded by the workflow are below. Steps 6–9 are `continue-on-error`, so
GitHub shows them as successful steps; the final "Enforce bounded result" step fails
the job by design.

| Phase | Exit | Recorded phase |
| --- | --- | --- |
| bootstrap | 0 | complete |
| sources | 0 | complete |
| install | 0 | complete |
| capture | 1 | producer-execution |
| integrity | 1 | verifier-execution |
| technical | 1 | verifier-execution |
| authority | 1 | verifier-execution |

The producer returns 1 whenever any scenario is not PASS or cleanup is incomplete
(`qualify-dapr-brokers.py:1063`), and its packet records six non-PASS scenarios. Its
stdout is a private transcript and was not uploaded, so its text is not claimed here.

**Archive handling.** The archive was downloaded into a fresh scratch directory, with
its digest checked against the API. All 575 entries were inspected before extraction:
there were no unsafe paths, symlinks, duplicates or private transcripts. Nothing from
it was executed. Only the workflow's four sanitized summaries are committed
(`artifact-*.json`). The packet bytes are not committed; `artifact-extracted-digests.json`
records the SHA-256 of every extracted file.

**Local verifiers.** Each mode ran with the original configuration from an isolated
`git archive` of EventStore 02a4d3b2. Each exited 1, with stdout byte-identical to the
hosted run's (SHA-256 `149cacf3…5b9520`). The expected authority rejection was
therefore **not** observed. The verifier never reached it.

### Demonstrated verifier defect

`tools/dapr_platform_broker_configuration.py:298-307` decides whether a command
receipt is a daprd sidecar launch. Any receipt whose arguments include `--app-id`,
`--resources-path` or `--config` counts as one, and its `argv[0]` must then be the
measured daprd. Receipt 1079 is the producer's own Kafka topic creation
(`qualify-dapr-brokers.py:442`):

```text
docker exec <kafka> /opt/kafka/bin/kafka-topics.sh … --create --topic qualification.events … --config retention.ms=60000 --config segment.ms=1000
```

It contains `--config` and its `argv[0]` is `docker`, so the check fails. The check
arrived in 02a4d3b2 ("build: support bounded platform broker configuration"). The Kafka
`--config` call predates it (54261aecc). The file is unchanged at EventStore main
`bb9e2b51`.

**Non-authoritative diagnostic** (`local-diagnostic-verification.json`,
`diagnostic_patched_verifier.py`). In a scratch copy, the check was changed to skip
`docker exec` receipts, and the trusted-root binding was pointed at the unpatched
archive. With those two changes:

- integrity passes (exit 0);
- technical fails on the six scenarios below;
- authority exits 1 with the exact pinned AD-26/3.19/Kubernetes/restore/4.16 rejection message.

So the false positive is the only integrity blocker the verifier reports. This
diagnostic is not qualification evidence and credits nothing.

### Vectors (producer-reported, unverified)

24 of 24 vectors were observed: 18 PASS and 6 not PASS. None is verified or credited
(`vector-assessment.json`).

| Vector | Producer | Recorded fields (not a verified cause) |
| --- | --- | --- |
| rabbitmq/cancellation-backpressure | FAIL | acknowledgedLoss 3, negativeConfirmRejected false (historically rejected) |
| rabbitmq/durable-quarantine | FAIL | blockedBy "bounded readiness/readback condition not met" |
| rabbitmq/missing-bindings | FAIL | bindingAbsent true, unboundPublishStatus 204 (historically rejected) |
| rabbitmq/retention-expiry | FAIL | blockedBy "bounded readiness/readback condition not met" |
| rabbitmq/secret-rotation | FAIL | oldGenerationDenied false, stalePublishStatus 500 (historically rejected) |
| kafka/cancellation-backpressure | FAIL | acknowledgedLoss 2, negativeConfirmRejected false (historically rejected) |

The other 18 vectors report PASS. That includes `kafka/secret-rotation`, which is
historically rejected. An unverified PASS does not reopen or change that rejection.

**Cleanup.** The receipt is complete: no owned resources remain, and all six child
containers are absent. It is **not credited**, because cleanup credit requires verified
integrity.

**After the terminal status** (`post-run-checks.json`):

- the tag still resolves to `94001fad…`;
- rulesets 24657570 and 24657217 are unchanged;
- the workflow lists exactly two runs.

The activity API returns no events for the tag ref.

## What this settles about attempt 1

The same source, workflow bytes, `ubuntu-24.04` label and `workflow_dispatch` event
were allocated in 5 seconds on a healthy provider. Attempt 1's `startup_failure`
(run 43097161643) was therefore not caused by the workflow file, the label or the
event type. Two causes remain: the 2026-11-11 provider incident and the deletion of
the dispatch ref while queued. This attempt does not separate them.

## Orphaned runs from 2026-11-11

As the owner approved, all 43 runs still queued with zero jobs were sent `cancel`, then
`force-cancel`. GitHub refused both with HTTP 409 for every run ("Cannot cancel a
workflow run that has not been queued yet."). All 43 remain queued
(`orphaned-run-cancellation.json`). `/pushall` is unchanged.

The owner then approved deleting the 43 records. After re-checking that each was still
queued with zero jobs, one `DELETE` per run returned HTTP 403 ("Could not delete the
workflow run"). All 43 remain (`orphaned-run-deletion.json`). GitHub's documentation
says "You can delete a workflow run that has been completed, or is more than two weeks
old". These runs cannot complete, so they become deletable after
2026-11-25T20:52:47Z, two weeks after the newest was created. `delete_orphaned_runs.py`
can be rerun then.

## Commands

All are recorded with argv and exits in the evidence directory:

- `cancel_orphaned_runs.py` (cancellation) and `delete_orphaned_runs.py` (deletion);
- `tag-create.*` and `single-dispatch.*` (the attempt's only two mutations; the
  cancellation POSTs touched only the 43 orphaned runs);
- `monitor_attempt.py` (GET polling);
- `retrieve_and_verify.py` (download, inspection, three pinned verifier modes);
- `diagnostic_patched_verifier.py` (scratch-only diagnostic);
- `post_run_checks.py` (GET).

`file-manifest.json` commits every retained file. Check it with
`python3 -I ../candidate-01-startup-investigation/verify-manifest.py file-manifest.json`.

## Remaining uncertainty

- The six producer failures are unverified. Their causes, and whether the newly
  failing `rabbitmq/durable-quarantine` and `rabbitmq/retention-expiry` are broker
  behavior or harness timing, are not established.
- The correct shape of the verifier fix is not settled. One option is to exclude
  `docker exec` receipts. Another is to match the sidecar only by its exact executable
  path and flag structure. Either needs EventStore regression tests, including a
  real-shaped Kafka topic-creation receipt.
- The captured packet cannot be re-verified by a fixed verifier at a new EventStore
  revision: the source-binding checks tie the packet to 02a4d3b2's own tool bytes. A
  fix therefore needs a new hosted attempt.
- Attempt 1's residual cause (provider incident vs ref deletion) is unchanged.

## Next owner decision

Fixing the verifier means a new EventStore revision, a Platform gitlink and workflow
pin update, and a new candidate. Recommended: **do not spend a hosted attempt only to
get an authoritative FAIL.** Fix the verifier false positive (with a regression test) in
the same EventStore change set that addresses the broker failures. Then request one
attempt for that new candidate. The alternative is to fix the verifier alone and run
one attempt now, to get a verified baseline for the six failures. That costs an extra
review and repin cycle for a result that cannot select a broker.

The artifact expires on 2026-12-13. Any re-examination of these bytes must happen
before then.
