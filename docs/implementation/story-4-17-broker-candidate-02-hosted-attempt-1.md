# Story 4.17 — broker candidate-02, hosted attempt 1 (2026-11-13)

Status: **attempt consumed; packet integrity independently VERIFIED; technical qualification
FAIL (20/24).** Platform 4.17 and EventStore 3.21 remain **in-progress**. All five historical
guarantees remain **REJECTED** and open. Broker selection is **null** and production eligibility
is **FAIL**.

The machine-readable assessment governs this summary:
[`attempt-assessment.json`](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-02-hosted-attempt-1/attempt-assessment.json).
The [candidate-02 proposal](story-4-17-broker-candidate-02-proposal.md) defined the route and the
expected results. The [packet diagnosis](story-4-17-broker-candidate-01-packet-diagnosis.md)
explains the changes.

## Outcome

Run [43386723091](https://github.com/Hexalith/Hexalith.Platform/actions/runs/43386723091),
attempt 1, was dispatched once from the immutable tag. Its job started 4 seconds after the run was
created. It captured all 24 vectors and uploaded the artifact. This is the first independently
verified packet for Platform's configuration input. Every prediction in the proposal held.

| Phase | Hosted exit | Local exit (`495dd249` archive) | Result |
| --- | --- | --- | --- |
| bootstrap / sources / install | 0 / 0 / 0 | — | complete |
| capture | 1 | — | 4 scenarios not PASS; cleanup receipt complete |
| integrity (`--allow-failures`) | 0 | 0 | **PASS** |
| technical | 1 | 1 | **FAIL**: the four certain failures |
| authority (`--require-production-authority`) | 1 | 1 | the exact expected AD-26/3.19/Kubernetes/restore/4.16 rejection, empty stderr |

The hosted and local verifier stdout are byte-identical in every mode. The job conclusion is
`failure`, set by "Enforce bounded result". That step is designed to fail whenever technical
qualification fails, so bounded technical success is false. Because integrity is verified,
**cleanup is now credited**: no owned resources remain, unrelated containers are unchanged and
private material was removed.

## Identities

| Item | Value |
| --- | --- |
| Dispatch ref | `refs/tags/qualification/broker-candidate-02/attempt-1-4d66ff55` (created 2026-11-13T07:34:15Z) |
| Platform source | `4d66ff5584e4f62239551bb02311649334171a84` (observed on the worker) |
| EventStore gitlink and revision | `495dd24943031ec75e2822c6168ada3c8b9fdb82` (observed on the worker; contained in EventStore main `d457ecd5`) |
| Configuration SHA-256 | `b8601e3a6d4e8460d039573ca2f7e1728e52aa67e61fc0710d74cab5d072580b` (observed on the worker) |
| Job | `131756824340`, runner `GitHub Actions 1000081957`, `ubuntu-24.04`, image `20261111.127.1`, 07:34:35Z–07:40:48Z |
| Artifact | `platform-broker-candidate-02-43386723091-1`, 1,650,434 bytes, SHA-256 `a26b848840ca8061a00d8ea7598b52b6a91b5331822645c23bc527278e40949a` (matches the API digest), 544 entries, no inspection problems, expires 2026-12-13T07:40:43Z |

## Vectors (verified)

| Vector | Attempt 2 (unverified) | Candidate-02 (verified) | Note |
| --- | --- | --- | --- |
| rabbitmq/durable-quarantine | FAIL | **PASS** | Restored by the default RabbitMQ tuning, as diagnosed |
| rabbitmq/retention-expiry | FAIL | **PASS** | Restored by the same defaults |
| kafka/cancellation-backpressure | FAIL | **FAIL** | `acknowledgedLoss` is now 0 thanks to the retention fix. It still fails only because Kafka sends no attributable rejection: two 204s, six 500s |
| rabbitmq/cancellation-backpressure | FAIL | **FAIL** | `acknowledgedLoss` 3, no rejection (code-level NACK→204) |
| rabbitmq/missing-bindings | FAIL | **FAIL** | Unbound publish returns 204 (`mandatory=false`) |
| rabbitmq/secret-rotation | FAIL | **FAIL** | The stale publish timed out (599 after 5.01 s) instead of being refused |
| kafka/secret-rotation | PASS | **PASS** | The stale publisher got HTTP 500 "kafka server: The client is not authorized to access this topic" after 0.41 s, with no stale effect |

The other 17 vectors are verified PASS. In total: 20 PASS and 4 FAIL out of 24
(`vector-assessment.json`).

**Historical guarantees.** All five stay REJECTED and open. Four still fail here. Kafka rotation
now has one verified PASS on candidate-02. The 2026-11-10 runs failed it and the cause of the
change is undiagnosed, so the guarantee is not closed. Adjudicating it belongs to EventStore 3.21.

## Mutations and accounting

Each step was approved through the owner's "Approve as proposed" decision. Exactly one dispatch
ran, with no rerun, retry or cancellation.

1. **EventStore branch.** `fix/3-21-candidate-01-verifier-and-pressure-retention` was pushed at
   `495dd249`.
2. **EventStore main.** The merge commit `d457ecd51a17b8ecc0c678b02d19d7ee92c13141` (parents
   `f2062f7f`, `495dd249`) was pushed to main after the merged tree passed 160/32/11 tests. The
   EventStore ruleset allows only squash PR merges, which would drop `495dd249` from main, so this
   was a direct maintainer push. GitHub reported: "Bypassed rule violations for refs/heads/main:
   Changes must be made through a pull request; 7 of 7 required status checks are expected." No
   force-push followed. No release ran: EventStore's `release.yml` is `workflow_dispatch` only.
3. **Platform source branch.** `chore/broker-candidate-02-source` was pushed at `4d66ff55`. It
   started no workflow.
4. **Go/no-go.** All seven checks passed (`go-no-go.json`, raw responses retained):
   - provider status operational;
   - both rulesets equal to the recorded policy;
   - the tag absent;
   - the workflow idle with its two earlier runs;
   - the identities agree;
   - no workflow triggers on tags.
5. **Tag.** Created through the API (exit 0). It resolved to S and started no run.
6. **Dispatch.** Exactly one `gh workflow run … --ref qualification/broker-candidate-02/attempt-1-4d66ff55` (exit 0).

**Post-terminal checks** (`post-run-checks.json`): the tag still resolves to `4d66ff55`, rulesets
24657570 and 24657217 are unchanged, and the workflow lists exactly three runs.

## Concurrent activity (not this session's commands)

Another session's `/pushall` acted on Platform during the attempt:

- At 07:31:32Z it pushed `main` `b8620131 → d5928c8b`. That published this slice's three docs commits.
- At 07:39:24Z it merged the source branch into `main` as `8483559f` ("build: merge
  refs/remotes/origin/chore/broker-candidate-02-source into main via /pushall") and pushed it.
- At 07:39:44Z it deleted `chore/broker-candidate-02-source` while the run was in progress.

The run was unaffected because it ran from the immutable tag, as the attempt-1 lesson required.
Platform `main` now carries the candidate-02 gitlink and workflow. The Platform tuple CI preflight
failed on both pushes at the legacy same-source identity check. It was already failing on
`b8620131` and `0c97b496`.

## Commands

The scripts are retained in the evidence directory and each runs with `python3 -I`:

- `go_no_go.py`;
- `monitor_attempt.py` (GET polling, with the 30-minute no-job cancel rule, which did not trigger);
- `retrieve_and_verify_c02.py` (download, digest, inspection before extraction, three verifier modes);
- `assess_vectors_c02.py`;
- `post_run_checks_c02.py`.

The tag and dispatch argv, timestamps and exits are in `tag-create.*` and `single-dispatch.*`.
`file-manifest.json` commits every retained file. Check it with
`python3 -I ../candidate-01-startup-investigation/verify-manifest.py file-manifest.json`.

## Remaining uncertainty and next decisions

- **Kafka pressure.** The pass rule accepts only a rejection that a produce byte-rate quota never
  emits, so this vector cannot pass with the current fault. Changing the fault or the guarantee is
  for EventStore 3.21 to decide.
- **RabbitMQ rotation, pressure and missing binding.** These need component changes: the reconnect
  error must surface, NACKs must become errors, and `mandatory` must be set with return handling.
  They could also be resolved by owner exceptions. Upstream release status was not re-checked.
- **Kafka rotation.** It has one verified PASS, but its earlier failures are not explained. EventStore
  3.21 should decide whether this evidence is enough, or whether a repeat run or a cause analysis is
  needed.
- **Platform main.** It now differs from the legacy tuple source because of another session's
  `/pushall`. Restoring tuple CI needs the usual mirrored L/M pull-request route.
- **Not done here.** No broker selection, staging, Kubernetes rehearsal, AD-26 ratification or Story
  4.16 proof was attempted. The artifact expires on 2026-12-13.
