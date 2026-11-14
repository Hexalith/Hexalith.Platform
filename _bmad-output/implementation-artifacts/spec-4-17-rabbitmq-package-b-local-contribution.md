---
title: 'Story 4.17: RabbitMQ Package B local upstream contribution'
type: 'feature'
created: '2026-11-14'
status: 'in-progress'
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
- [ ] `metadata.go`, `metadata.yaml`, `metadata_test.go` -- add the option and its cross-field rule -- opt-in API.
- [ ] `rabbitmq.go` -- listener, drain, post-ACK return check, sentinel, classification and forced reconnect -- exact correlation within existing machinery.
- [ ] `rabbitmq_publish_test.go`, `rabbitmq_publish_transport_test.go`, `rabbitmq_integration_test.go` -- cover every matrix row at public `Publish`, including the native return-then-ACK peer, a concurrent routed caller beside an unroutable one, duplicate MessageIds, cancellation and channel replacement -- deterministic, race-clean proof.
- [ ] Platform `package-b-local-2026-11-14/` -- `contribution.patch` relative to `96c3259b`, source/toolchain identities, receipts, `coverage-map.json`, `integration-proposal.md` (the work-package recipe, unexecuted) and `README.md` -- reviewable record.
- [ ] Records (parent) -- a dated section in `docs/implementation/story-4-17-next-candidate-intake.md`; a dated 4.17 comment in `sprint-status.yaml`; a dated section in the EventStore `story-3-21-broker-remediation-evidence-contract.md` as a local commit only.

**Acceptance Criteria:**
- Given `96c3259b` plus `contribution.patch`, when the Package A check set runs (14 receipts, with `--new-from-rev` and `diff --stat` set to `96c3259b`) plus `make check-component-metadata`, then all pass with no skips. Unit and focused-race counts are at least Package A's 62/196 and 230/720, and there are 0 new lint issues.
- Given the completed slice, when the records are read, then the gates remain: an official supported Dapr release, EventStore admission, Builds alignment and a separate Platform full-matrix proposal. No remote write occurred.

## Design Notes

Correlation relies on Package A's invariant. `channelMutex` keeps at most one publication outstanding per channel. A stale buffered return is drained at publish start. A per-generation listener isolates replaced channels. Forcing a reconnect after any unresolved attempt closes the one remaining gap: a live channel whose earlier message may still produce a return. Option name `publishMandatory` follows #4765; per-request overrides and `immediate` are out of scope.

## Verification

**Commands:**
- Held-job checks from the adapted `required-checks.jsonl` -- expected: all exit 0, the floors above, an empty `gofmt -s -l`, no tidy diff.
- `git diff --stat <base> HEAD` -- expected: only `pubsub/rabbitmq` files.
