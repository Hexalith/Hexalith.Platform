---
title: 'Story 4.17: RabbitMQ Package A local upstream contribution'
type: 'bugfix'
created: '2026-11-14'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '66c8b2bed82213ee4038da525eab5e81e78bbc13'
upstream_commit: '440b15349565bb15f7a88636477864e5a9f8cc72'
context: []
---

<frozen-after-approval reason="user explicitly authorized this exact local contribution and work alongside existing edits">

## Intent

Prepare a reviewed local contribution for dapr/components-contrib issue #4764. Preserve RabbitMQ publish errors through the public Publish path and bound cancellation across confirmation, retries, reconnect, locking and network waits, with focused public-boundary tests including concurrent callers. Save a reviewable patch, exact official source identity and useful test evidence outside the disposable checkout.

## Boundaries & Constraints

**Always:** Use official main commit `440b15349565bb15f7a88636477864e5a9f8cc72`. Work in the checkout supplied by the held `run-with-temp` job. Preserve intentional uncommitted documentation and both in-progress parents. Preserve all qualification predicates, existing time bounds, all 24 vectors and uncertain in-flight outcomes. Prepare broker integration work as an unexecuted concrete proposal, distinguishing component tests from Dapr qualification.

**Never:** Package B mandatory/correlated returns, Kafka changes, external comments, PR creation, push, runtime adoption or Platform qualification. Do not turn a timeout into a broker denial or claim bounded absence means permanent absence.

## I/O & Edge-Case Matrix

| Given | When | Then |
|---|---|---|
| ACK or successful retry | Public Publish completes | Success reflects the real outcome |
| NACK and final failure, including retry cancellation | Public Publish returns | Exact `did not receive confirmation of publishing` error survives |
| Broker 403 ACCESS_REFUSED | Publish or reconnect fails | Original error is retained; permanent authorization stops futile retries |
| Cancellation before work or during any wait | Publish terminates | Non-success within budget; no subsequent retry or delayed publication |
| Stalled concurrent holder | Waiting callers expire | Each terminates without killing the holder's unrelated connection |
| Publication already sent | Confirmation is interrupted | Non-success leaves effect uncertain; no inferred rejection or zero effect |

</frozen-after-approval>

## Code Map

- Checkout: `/tmp/hexalith-job.ZK82D9vZ/components-contrib`, held by workspace session until final cleanup. All upstream code edits belong here.
- `pubsub/rabbitmq/rabbitmq.go`: Publish, publishSync, reconnect, dial, confirmationError, reset. Existing retry ceiling 3 and retry delay 2 seconds remain. Reuse the connection-generation guard and existing terminal/retriable errors.
- `pubsub/rabbitmq/rabbitmq_test.go`: in-memory broker and dial seam; existing reconnect/helper tests. `rabbitmq_integration_test.go` has two RLock snapshots requiring mechanical adaptation if lock API changes; do not run broker integration.
- Pinned `github.com/dapr/kit/concurrency/lock.Context` supplies cancellation-aware locking; prefer it over polling or detached worker wrappers. Initialize constructors and test literals consistently.
- Pinned AMQP v1.14.0 `DeferredConfirmation.WaitContext` exists. Its publish context does not interrupt in-flight network I/O; declaration, channel-open, Confirm and graceful Close have no context. Retain raw net.Conn from Config.Dial; cancel by closing that exact transport, not by graceful channel Close or a shared-generation lookup. DialContext plus a cancellation hook must cover TLS/AMQP handshake and channel setup.
- Artifacts: `docs/implementation/story-4-17-upstream/package-a-local-2026-11-14/` under Platform. `checks.py` holds verified Go1.26.10 plus scratch caches in a separate run-with-temp job. Parent runs serialized checks over stdin. Its `checks-environment.json` records toolchain path if an implementation-side check is needed; use run-with-temp for any additional jobs.
- Governing Platform intake and EventStore evidence contract were read; qualification tooling/profiles/configurations remain untouched. `preservation-before.json` binds preexisting documentation and qualification sources.

## Tasks & Acceptance

- [x] `pubsub/rabbitmq/rabbitmq.go` and any narrowly needed internal adapter: implement context-aware acquisition, confirmation and exact-generation transport abort; check context before each broker operation/retry and before installing new state. Stop/join cancellation callbacks before releasing ownership; no detached publishers or background retry jobs.
- [x] Preserve conclusive publish/reconnect failure text on cancellation/exhaustion, successful retry results and native authorization errors. Return cancellation if no conclusive broker failure exists. In-flight network interruption remains uncertain. A canceled handshake can be mapped by AMQP to generic ErrCredentials: prioritize actual cancellation, never relabel it ACCESS_REFUSED.
- [x] `pubsub/rabbitmq/rabbitmq_test.go` and focused new tests: exercise every matrix row at public Publish, including active/passive declaration, confirmation ACK/NACK, write, dial/handshake, confirm setup, retry/reconnect delay, lock contention, canceled-before-start, stale generation and concurrent error isolation. Use synchronization gates and join workers; no broker execution or unsafe private-field tricks. Adapt native channel confirmation through a small internal interface if needed for deterministic tests.
- [x] Platform artifact directory: retain final unified patch including new files, official source/toolchain identities, commands/exit codes/logs, review findings, test coverage mapping and unexecuted integration proposal. Verify patch applicability and original-byte preservation, then finish both held jobs and confirm cleanup.

Acceptance: Given the pinned source plus patch, when focused and full component unit tests and focused race tests run, then they pass with no skips in the new coverage. Given unexecuted broker/Dapr work, when reporting results, then list it as a gap with its concrete later experiment and supported-release gate. Given existing Platform/EventStore qualification state, when preparation ends, then both parents remain in-progress and 20/24 FAIL, five REJECTED/open guarantees, null selection and production FAIL remain.

## Implementation Notes

The detailed user request settles scope and authorization; no additional approval or irreversible action is needed. Existing documentation bytes remain intact. The source checkout started clean on detached HEAD. Parent owns final test evidence and artifact assembly; implementation agent may report tests ready for parent execution.

## Spec Change Log

## Review Triage Log

Round 1 reviewed snapshot: SHA-256 `b207d5780597e5ee806410adfd1786480d215ff6bd74f3e9cca00a4c2356ea27` (canonical hash in retained review-round-1-subject.json). All three independent reports were collected before triage. Findings are graded individually before grouping.

| ID | Verdict | Evidence and route |
|---|---|---|
| BH1 | high | Native reset reproduction failed on 99/100 healthy connections: Abort causes io.ErrClosedPipe from graceful Close, which reset propagates. Patch cleanup error handling and add native recovery/Close regression. |
| BH2 | high | Preserved live-context deadline reproduction returns nil after three failed Publish operations. remember discards context errors even while caller context is live; result receives nil. Patch error retention and exhaustion regression. |
| BH3 | high | Preserved NACK → io.ErrClosedPipe → canceled confirmation repro loses NACK. A generic transport error is incorrectly considered conclusive. Patch definitive-error classification and test. |
| BH4 | medium | Preserved queued shutdown reproduction returns Unavailable after publishSync supplied a terminal error. Publish retries and reconnect overwrites the error. Patch immediate terminal/stopped handling. |
| BH5 | high | Three native AMQP peer reproductions lose server channel.close(403). NotifyClose is not retained; deferred shutdown becomes generic ErrClosed. Patch native outcome preservation and channel/connection-denial tests. |
| BH6 | high | Source maps a genuine false confirmation to ErrClosed whenever channel.IsClosed is later true. A NACK followed immediately by disconnect is a reachable negative-confirmation/close race. Patch with definitive native confirmation evidence; test NACK then close. |
| BH7 | medium | Native tests always enable confirms; default no-confirm success and in-flight write cancellation only use a mock. Add native no-confirm coverage and post-completion cancellation isolation. |
| BH8 | maybe-false | Actual shared-subscription recovery/unacknowledged redelivery remains empirically unknown. It requires the proposed real-broker experiment. Execution is explicitly excluded by the user's intent; record concrete recovery/readback checks in the integration proposal, without claiming them passed. |
| EH1 | high | Independent native 403 repro agrees with BH5; same native outcome-loss root cause. Patch with BH5, retaining original broker text and terminal classification. |
| EH2 | high | On uncanceled exhaustion result chooses failure rather than earlier conclusive NACK; source confirms a later generic failure can replace it. Patch with BH3 and add exhaustion case. |
| VG1 | medium | Preverified coverage gap: all native cancellation tests bypass Init. Add synchronized Init cancellation and cleanup regression. |
| VG2 | medium | Preverified coverage gap: no test contains connection_timeout; deleting SetDeadline would leave caller-cancellation tests green. Add native stalled handshake with URI timeout and live caller. |
| VG3 | high | Independent native denial repro confirms BH5 and missing terminal 403 path. Patch with BH5, preserving its red reproduction. |
| OWN1 | false | Declaration caching records a successfully completed broker declaration. The immediate post-operation context check prevents delayed publication, and aborted-generation reconnect clears the cache. No incorrect broker state or late publication was demonstrated; do not add another declaration helper. |

Surviving changes are direct corrections within the existing Publish/cancellation requirements and internal adapters, plus focused missing coverage. No public API or scope decision changes. Shared-broker execution is excluded by the user and remains a concrete proposal. No source finding requires a new owner decision.


## Verification

Use the held checks.py runner (all build/cache outputs under its TMPDIR): `go test -count=1 -timeout=120s ./pubsub/rabbitmq`; focused `go test -race -count=10` for new Publish tests; `go vet ./pubsub/rabbitmq`; `gofmt -l` on changed Go files; upstream `git diff --check`. Parent verifies final patch reverse/forward applicability and preserved-file hashes offline, without qualification execution.

Implementation checkpoint: public-boundary seam and native net.Pipe protocol-peer tests pass in focused-unit-2. Full suite, repeated race, vet and integration-tag compile-only checks are queued serially. The native peer is a unit fixture, with no RabbitMQ broker or Dapr runtime execution. Patch snapshot is in the held checkout’s `.git/review-package-a.diff`.


Completion — November 14, 2026: final local artifact slice is done; both parent sprint statuses remain in-progress by explicit user instruction. Three independent reviewers verified source/test corrections. The final patch is `ad71546ef66169c02b6b61f926b313c48d5f251e1a52e99c4129a44059dfa3b7`, official source `440b15349565bb15f7a88636477864e5a9f8cc72`. Full component suite passes (61 top-level / 192 with subtests), 68 focused Publish/Init cases pass ten race repetitions, and the corrected legacy fixture passes ten race repetitions. Vet, formatting, integration-tag compilation without execution, forward/reverse apply and preservation checks pass. The last patch correction only initializes the existing reconnect test as open and adds closed-state assertions; production and focused-test source match the reviewed/race-tested candidate. All historical failure logs remain labeled in the artifact report.

Review follow-up: all five preserved blind reproductions pass. Native close notifications preserve observable server causes; real NACKs survive subsequent disconnects. If the SDK has not exposed a cause before cancellation, that outcome stays uncertain; the blocked close-ok notification limit and generic authentication-marker mapping are retained in the unexecuted broker proposal. No source defect remained in targeted follow-up. Shared-subscription recovery, actual RabbitMQ faults, Dapr mapping and persisted readback remain unexecuted integration gaps. Supported delivery, EventStore admission, Builds alignment and separately accepted Platform all-24-vector execution remain gates; this local slice grants no qualification.

Both held run-with-temp jobs exited zero; checkout, toolchain and caches were removed after all workers finished. All 114 recorded preexisting documentation/qualification inputs match, including intentional uncommitted edits. No upstream comment, PR, push, runtime adoption or Platform qualification occurred. The new artifact directory and this spec are the only preparation outputs selected for the workflow's local commit; other working changes and gitlinks are preserved.
