---
title: 'Story 4.17: RabbitMQ Package B local upstream contribution'
type: 'feature'
created: '2026-11-14'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'f5f30e150f1c5ec72acac97054d69cd1e42f5da5'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** RabbitMQ publishes with `mandatory=false` and has no return handler. A publisher confirm therefore acknowledges an unroutable message, and Dapr returned 204 for the physically unbound `qualification.missing-binding` topic (dapr/components-contrib #4765, following #4697). Package A (PR #4767) is OPEN; all six upstream workflows await maintainer approval (`action_required`), with no reviews or comments as of 2026-11-14T11:54Z, so nothing on it is actionable.

**Approach:** Prepare a reviewed, local-only Package B contribution: opt-in mandatory publishing whose `basic.return` is bound to the exact attempt and channel generation, resolved before success, and carried through public `Publish` as a conclusive error. Save the patch, source identities, check receipts and an unexecuted broker/Dapr proposal under Platform, as Package A's local slice did.

**Owner decisions (2026-11-14, "Adopt all"):**
- Base: Package A head `96c3259b7c12a476edd208848153d3cf72a8c4f0` (sole parent `440b15349565bb15f7a88636477864e5a9f8cc72`). The patch stacks on #4767; submission waits for A or ships as a stacked PR, in a later separately approved slice.
- Unroutable return: terminal `pubsub.NewTerminalError` (FailedPrecondition), no retry, so the existing loop stops at once.
- `publishMandatory=true` with `publisherConfirm=false`: `Init` fails with a clear metadata error naming both fields.
- The full spec is kept above the token guideline.

## Boundaries & Constraints

**Always:** Work in disposable `run-with-temp` checkouts. Keep the default (`mandatory=false`, no return listener) byte-for-byte compatible when the option is off. Correlate returns without application `MessageId`, so duplicate IDs are harmless. Keep correlation bounded (no maps, no unbounded buffers, no background goroutines). Preserve Package A's retry count and delay, error retention, NACK text, authorization handling, abort ordering and restored logs. Keep both parents in-progress, technical FAIL (20/24), five REJECTED/open guarantees, selection null, production FAIL, Kafka stop rules, packet custody and the December 13 review.

**Never:** Push, fork, open or edit a PR, or comment on/label any issue or PR (#4764–#4767). Push Platform or EventStore. Run a broker, Dapr runtime, conformance/certification suite or qualification. Add an alternate exchange, routing service or app-side provider access. Claim Dapr HTTP mapping, upstream acceptance or a delivery gate. Change Package A behavior beyond what mandatory mode requires.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected | Handling |
|---|---|---|---|
| Opt-out | `publishMandatory` absent/false | Existing publish args, no listener, unchanged tests | — |
| Incompatible config | mandatory true, confirms off | `Init` error naming both fields | No component created |
| Routed | mandatory, ACK, no return | Success | — |
| Unroutable | return (312 NO_ROUTE) then ACK | That call fails at once with the terminal unroutable error, carrying the reply code and text; never replaced by retry exhaustion, cancellation or timeout | No retry |
| Stale return | earlier attempt unresolved on a live channel | No later publication on that generation; never attributed to a later call | Forced reconnect |
| Replacement channel | reconnect between attempts | Fresh per-generation listener; old returns unreachable | — |
| Canceled before ACK | return possibly pending | Non-success, uncertain outcome; transport abort as in Package A | No inferred rejection |
| Mismatched return | exchange/routing key differ from attempt | Fail closed, non-conclusive; channel reset | No success |

</frozen-after-approval>

## Code Map

Upstream paths are under `pubsub/rabbitmq/` at `96c3259b7c12a476edd208848153d3cf72a8c4f0` (pinned amqp091-go v1.14.0).

- `rabbitmq.go:135-166` `rabbitMQAMQPChannel` -- add a per-generation `returns <-chan amqp.Return`. Drain it with the existing confirmation-drain loop before each publish.
- `rabbitmq.go:409-414` `dialWithConfig` -- register `NotifyReturn(make(chan amqp.Return, 1))` only in mandatory mode, beside `NotifyPublish`.
- `rabbitmq.go:240-277` `rabbitMQAMQPConfirmation.outcome`/`WaitContext` -- after its own ACK, receive from `returns` without blocking; a return makes the outcome a new sentinel (e.g. `errPublishUnroutable`) carrying ReplyCode/ReplyText. Rationale: amqp091 assembles and delivers a return before it handles the next frame, so a return always precedes its ACK in the listener (`channel.go:558-645`; `confirms.go:86-89`). A full listener drops the return after 5 s and stalls the reader (`channel.go:28,36-56`), so the drain-plus-single-outstanding rule must keep it empty.
- `rabbitmq.go:505-592` `publishSync` (holds `channelMutex` through confirmation; line 569 passes `false, false`) -- pass `r.metadata.PublishMandatory`. Never `immediate`; RabbitMQ answers 540 and closes the connection.
- `rabbitmq.go:629-706` `Publish`, `remember`/`result`, `mustReconnect` 1340-1360 -- wrap the sentinel with `pubsub.NewTerminalError`, so the existing FailedPrecondition check at 698 stops the loop; `remember` still writes the attempt warning. In mandatory mode, force a reconnect when an attempt ended without an observed confirmation.
- `metadata.go:54,85,134,149,200-202` -- add the `PublishMandatory` field and the `publishMandatory` key, default false, with cross-field validation. `metadata.yaml:92-98` holds the entry format. `make check-component-metadata` (Makefile 214-226) requires the yaml entry.
- Tests: `metadata_test.go:57-92,400-418`. In `rabbitmq_publish_test.go`, extend `publishTestBroker` (43-98; it currently discards mandatory/immediate) and reuse `newPublicPublisher`, `startPublicPublish`, `awaitPublish*` and `TestPublishPublicFailureLogging`. In `rabbitmq_publish_transport_test.go`, `publishProtocolPeer.send` writes method frames only; add a basic.return (60.50) method, content-header and body writer before the ACK (255-263); `method(60,40)` (211) exposes the mandatory bit, and `TestPublishPublicNativeConfirmationOutcomes` (354-410) is the template. `rabbitmq_integration_test.go` (tag `integration_test`) gets a compile-only unroutable test.
- Platform tooling to copy and adapt: `docs/implementation/story-4-17-upstream/package-a-pr-2026-11-14/{workspace.py,checks.py,summarize-checks.py,required-checks.jsonl}`. Output goes to `docs/implementation/story-4-17-upstream/package-b-local-2026-11-14/`.

## Tasks & Acceptance

**Execution:**
- [x] `metadata.go`, `metadata.yaml`, `metadata_test.go` -- add the option and its cross-field rule -- opt-in API.
- [x] `rabbitmq.go` -- listener, drain, post-ACK return check, sentinel, classification and forced reconnect -- exact correlation within existing machinery.
- [x] `rabbitmq_publish_test.go`, `rabbitmq_publish_transport_test.go`, `rabbitmq_integration_test.go` -- cover every matrix row at public `Publish`, including the native return-then-ACK peer, a concurrent routed caller beside an unroutable one, duplicate MessageIds, cancellation and channel replacement -- deterministic, race-clean proof.
- [x] Platform `package-b-local-2026-11-14/` -- `contribution.patch` relative to `96c3259b`, source/toolchain identities, receipts, `coverage-map.json`, `integration-proposal.md` (the work-package recipe, unexecuted) and `README.md` -- reviewable record.
- [x] Records (parent) -- a dated section in `docs/implementation/story-4-17-next-candidate-intake.md`; a dated 4.17 comment in `sprint-status.yaml`; a dated section in the EventStore `story-3-21-broker-remediation-evidence-contract.md` as a local commit only.

**Acceptance Criteria:**
- Given `96c3259b` plus `contribution.patch`, when the Package A check set runs (14 receipts, with `--new-from-rev` and `diff --stat` set to `96c3259b`) plus `make check-component-metadata`, then all pass with no skips. Unit and focused-race counts are at least Package A's 62/196 and 230/720, and there are 0 new lint issues.
- Given the completed slice, when the records are read, then the gates remain: an official supported Dapr release, EventStore admission, Builds alignment and a separate Platform full-matrix proposal. No remote write occurred.

## Implementation Notes

- Contribution: [contribution.patch](../../docs/implementation/story-4-17-upstream/package-b-local-2026-11-14/contribution.patch), SHA-256 `422dda73031e0bcbcf7a5e39329581f00a70486c2f7d23e9314041c4aaf3a29f`, seven `pubsub/rabbitmq` files relative to `96c3259b`. Its tree is `a7bc605f9388bbb0308f7a08d2cff9b2fa6123b3`. All 15 receipts PASS: unit 73/230, focused race 330/980, legacy 10/10, 0 new lint issues, component metadata. 15/15 negative controls are detected.
- Two departures from the Code Map:
  - The return listener is registered lazily by `rabbitMQAMQPChannel` before its first mandatory publish, not in `dialWithConfig`. This keeps existing dial call sites and tests unchanged.
  - The forced reconnect returns a nil channel through the new `unresolvedChannel()`, so `mustReconnect` itself is unchanged. A later caller that meets a marked generation spends one attempt and `ReconnectWait` before reconnecting.
- Parent matrix audit: every mapped test has pass events in `focused-race.log.gz`/`full-unit.log.gz`, with no skip or fail events, across all nine coverage rows.
- Concurrent sessions:
  - During this slice another session committed and pushed the draft spec with submodule gitlinks (`70a016a98c97c19656a4c279a93b88e255d41b07`). It also rewrote the baseline `f5f30e150f1c5ec72acac97054d69cd1e42f5da5` as content-equivalent `e58f4fc67c428494c6903f3d87c537c06aeb4ab5` on `origin/main`.
  - The EventStore record is local commit `d869fb858c0c639824595dc61db382ec8b5ed79c`, which is not pushed.

## Spec Change Log

## Review Triage Log

Round 1 (diff: Platform records, package directory including ignored logs, `contribution.patch` and the EventStore commit `d869fb858c0c639824595dc61db382ec8b5ed79c`; Blind Hunter BH, Edge Case Hunter ECH, Verification Gap VG). All three layers reported before triage. Each finding was graded before grouping.

| ID | Verdict | Evidence and route |
|---|---|---|
| VG1/BH2 | medium | Pre-verified. Removing `returned.Exchange != c.exchange` passes the full suite. The only native mismatch changes just the routing key, and the negative control removes the whole condition. Most publishes have key `""`, so the exchange is the real discriminator → **patch**: add a native mismatched-exchange stage and an exchange-only negative control. |
| VG2 | medium | Pre-verified. Dropping `key: key` from the confirmation literal passes the full suite, because no mandatory test publishes with `routingKey` → **patch**: add a native unroutable-with-routing-key stage and a negative control. |
| BH1/ECH3/VG-o1 | low | `stale := nativeReturns(t, r)` runs before the lazy registration, so it is always nil and `NotEqual(stale, fresh)` is vacuous. Reuse would still be caught indirectly → **patch**: capture after the first publish starts, require it non-nil, then compare. |
| BH3 | low | The concurrent test uses different exchanges, so a misattribution would be caught by the mismatch guard. Ordering alone, which protects same-exchange and same-key callers, has no concurrent test → **patch**: share the exchange and key and let the peer return by body. |
| ECH4/ECH10a | low | The 20 ms `WithTimeout` starts at subtest start under `t.Parallel()`. Under load it can expire before the publish, and Publish then returns DeadlineExceeded, not the unroutable error → **patch**: a deadline long enough that only the confirmation wait reaches it. |
| BH5 | low | `withCode` returns an error with a status unchanged, so the second `NewTerminalError` is a no-op. The extra assignment still enlarges the upstream diff → **patch**: fold `errors.Is(err, errPublishUnroutable)` into the existing terminal condition. |
| BH7 | low | The `metadata.yaml` wording "non-retriable" overstates it: the upstream `NewRetriableError` comment says the runtime retries every error without a resiliency `matching`. The text also omits that publishing before any subscriber binds a queue now fails → **patch**: reword. |
| BH4 | low | A mismatched return fails that attempt as non-conclusive, then retries on a fresh channel, which matches the frozen "non-conclusive" row (the unroutable row alone is terminal). The broker had ACKed the first attempt, so a retry can duplicate it; that is at-least-once, as in Package A, but undisclosed → **patch**: one README sentence. |
| BH8 | low | A new metadata field normally needs a dapr/docs reference update, and none is recorded → **patch**: add it to the README's not-included list. |
| BH10a | low | `publishMandatory_value=true/TRUE` are compatible cases listed under "Incompatible config" → **patch**: move them to the opt-in row. |
| BH11 | low | The scripts write into their own directory, so a rerun overwrites the retained receipts → **patch**: README note to copy them first. |
| BH12/BH14a | low | `remoteWrites: []` and `remoteReads` are declared literals. `make check-component-metadata` also resolves `github.com/dapr/components-contrib@main`, which is unlisted → **patch**: label them as declared and add that read. |
| BH18 | low | The README cites the SDK ordering but not RabbitMQ's return-before-ACK guarantee, and the proposal does not record wire order → **patch**: cite rabbitmq.com/docs/confirms and add a proposal step. |
| ECH11 | low | The records say Platform was not pushed, but another session's `70a016a98c97c19656a4c279a93b88e255d41b07` published the draft spec → **patch**: one sentence in the README and intake. |
| BH15 | low | The EventStore record omits the forced-reconnect consequence and will need the review outcome → **patch**: update it with the final records. |
| BH16/ECH6 | medium | The EventStore checkout `d869fb858c0c` is ahead of every remote, and a concurrent `/pushall` already published `70a016a9` with unpushed gitlink `3c3a8c70f`. The hazard predates this slice, and the fix is shared submodule state outside the diff → **defer**, surfaced at presentation. |
| BH6 | low | A return buffered alongside a NACK is discarded silently by the next drain. RabbitMQ does not return a routed, NACKed message, and logging needs logger plumbing in the adapter → rejected. |
| BH9 | low | A later caller pays one attempt plus `ReconnectWait` after a mark. The common cause, a cancellation, already aborted the transport under Package A, so that cost exists. Inline reconnect adds branches → rejected (already disclosed). |
| ECH1 | low | A caller can see repeated `errPublishUnresolved` only if other callers' cancellations keep marking each new generation inside its window. It is bounded by 3 attempts and returns a retriable error → rejected (the fix adds guards). |
| BH10b | low | `TestCreateMetadata` and `TestMandatoryReturnListenerClosed` run only in the non-race suite. They have no concurrency → rejected. |
| BH13 | false | `analyzerRan` is a weak heuristic, but exit 0 plus the negative control (yaml entry removed → exit 2) prove the analyzer checks the entry. |
| BH14b | low | git, make and python3 versions are unrecorded. They do not affect Go test outcomes → rejected. |
| BH17 | low | `baseline_commit` was rewritten by another session's rebase; the fix edits this spec, and the Implementation Notes record the equivalent → rejected. |
| ECH2 | false | A nil confirmation in mandatory mode is unreachable: `createMetadata` rejects mandatory without confirms before any connection, metadata is immutable afterward, and `reconnect` calls `Confirm` whenever `PublisherConfirm` is set. |
| ECH5 | low | A fixed three-generation slice would panic if `publishMaxRetries` grew. The failure is loud and hypothetical → rejected. |
| ECH7 | low | `urlopen` has no timeout, so a hang would be visible (Package A ECH11 precedent) → rejected. |
| ECH8 | low | A crashed `checks.py` makes `run-checks.py` raise loudly; `run-with-temp` cleanup still applies → rejected. |
| ECH9 | low | GO* variables are not scrubbed. The host has none (Package A ECH12 precedent) → rejected. |
| ECH10b | low | Other windows wait inside the 2 s retry delay and fail only if the test stalls about 2 s (disclosed) → rejected. |

## Design Notes

Correlation relies on Package A's invariant. `channelMutex` keeps at most one publication outstanding per channel. A stale buffered return is drained at publish start. A per-generation listener isolates replaced channels. Forcing a reconnect after any unresolved attempt closes the one remaining gap: a live channel whose earlier message may still produce a return. Option name `publishMandatory` follows #4765; per-request overrides and `immediate` are out of scope.

## Verification

**Commands:**
- Held-job checks from the adapted `required-checks.jsonl` -- expected: all exit 0, the floors above, an empty `gofmt -s -l`, no tidy diff.
- `git diff --stat <base> HEAD` -- expected: only `pubsub/rabbitmq` files.
