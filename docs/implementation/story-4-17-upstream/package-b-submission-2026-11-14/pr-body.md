## Problem and behavior

RabbitMQ can positively confirm a message that has no matching queue binding.
The component currently publishes with `mandatory=false` and has no return
handler, so a publisher confirm alone cannot tell callers that their message
was unroutable.

This adds optional `publishMandatory` metadata, default `false`. When enabled,
the component publishes with `mandatory=true` and resolves a correlated
`basic.return` before reporting success. A return followed by the publication's
own ACK fails that call with the reply code and text, for example
`broker returned the mandatory message as unroutable: 312 NO_ROUTE`.
The component wraps that error with `pubsub.NewTerminalError`
(FailedPrecondition) and does not retry it. The Dapr runtime may still retry
unless a resiliency `matching` rule excludes that code; no runtime status
mapping is claimed by these component tests.

`publishMandatory=true` requires `publisherConfirm=true`. `Init` rejects the
combination otherwise, before connecting, with an error naming both fields.
With mandatory publishing off, the existing publish flags and absence of a
return listener are unchanged. Publishing before any subscriber binds its
queue can now fail when the option is on.

## Dependency and review scope

This depends on the publish-error and cancellation work in #4767 (Package A),
which remains open. The proposed branch contains the unchanged Package A commit
`96c3259b7c12a476edd208848153d3cf72a8c4f0` followed by one Package B commit.
It targets `dapr/components-contrib:main`, currently
`440b15349565bb15f7a88636477864e5a9f8cc72`, so its GitHub diff includes both
packages. Narrowing that diff to B requires resolving A and checking/rebasing
this branch; a squash merge of A alone may leave its changes in the comparison.
The resulting exact base/diff will be reapproved before updating this branch.
The feature in this PR is Package B;
Package A's original commit and attribution are preserved.

The incremental Package B diff is the seven-file change after
`96c3259b7c12a476edd208848153d3cf72a8c4f0`: metadata declarations and tests,
the return-handling changes in `rabbitmq.go`, public publish tests, native
protocol-peer tests and a real-broker integration test, compiled but not
executed locally. Package B adds
1015 lines and removes 10. Please review #4767 first. This PR should be
rebased after that dependency is resolved; it should not independently merge
the overlapping Package A work.

## Correlation and failure handling

- `channelMutex` serializes publication through confirmation, so only one
  publication is outstanding per channel. Each channel adapter lazily registers
  one capacity-one `NotifyReturn` listener before its first mandatory publish.
- [RabbitMQ sends a return before the positive confirm of an unroutable
  mandatory message](https://www.rabbitmq.com/docs/confirms). The pinned
  amqp091-go v1.14.0 client [assembles the complete content before dispatch](https://github.com/rabbitmq/amqp091-go/blob/387d77a50ea8b8c38705bb18cc80f5d6599a8477/channel.go#L613-L645)
  and [delivers a return synchronously to its listener](https://github.com/rabbitmq/amqp091-go/blob/387d77a50ea8b8c38705bb18cc80f5d6599a8477/channel.go#L511-L515)
  before processing the following frame. Its [notification timeout](https://github.com/rabbitmq/amqp091-go/blob/387d77a50ea8b8c38705bb18cc80f5d6599a8477/channel.go#L28-L56)
  can drop a return if the listener fills; the single-outstanding-publication
  rule and drain keep this capacity-one listener available. After receiving
  its own ACK, the attempt checks the
  buffered return without blocking. Its exchange and routing key must match.
- A mismatched return fails the attempt as non-conclusive and forces a fresh
  channel before a retry. An attempt whose mandatory publication may have
  reached the broker without an observed ACK or NACK marks that generation
  unresolved; no later caller publishes on it. Old listeners remain attached
  to their old generation. The existing pre-publish drain removes residual
  notifications from resolved attempts.
- Correlation does not read application message IDs, allocate a map or spawn
  background goroutines. Concurrent callers and duplicate IDs are covered.
  A NACK retains Package A's existing error text even if a return accompanies it.
- A return observed with its ACK as the caller's context ends remains the
  unroutable error. Cancellation before ACK remains uncertain and uses Package
  A's transport abort; it is not inferred to be a broker rejection.

The existing default, retry limits and delays, error retention, authorization
handling, warnings, retry-exhaustion log and abort ordering are preserved.

## Consequences

The component shares a connection with subscriptions. Forcing a reconnect
resets that connection, so subscriptions reconnect and unacknowledged
deliveries can be redelivered, as with Package A's cancellation abort. Newly
covered cases include a mismatched return or a confirmation/write failure
that leaves the channel live. Retrying a mismatched return can duplicate a
message already ACKed by the broker. These component checks establish no
end-to-end delivery guarantee. A later
caller encountering a marked generation consumes one attempt and waits
`reconnectWaitSeconds` before reconnecting.

## Validation

The reviewed source tree is `50f8ef2d3d3a240595d6088917f9d51afe04ec3c`.
Local checks ran against a disposable test commit with that exact tree,
parent `96c3259b7c12a476edd208848153d3cf72a8c4f0`, Go 1.26.10 and
golangci-lint v2.10.1. Its commit message identified it as a local test vehicle;
the proposed public commit changes the message, not the tested source.

The public, immutable [check summary and receipts](https://github.com/Hexalith/Hexalith.Platform/tree/50fb0462924a765e38d830e42af242ccfaf5982f/docs/implementation/story-4-17-upstream/package-b-local-2026-11-14),
[exact command list](https://github.com/Hexalith/Hexalith.Platform/blob/50fb0462924a765e38d830e42af242ccfaf5982f/docs/implementation/story-4-17-upstream/package-b-local-2026-11-14/required-checks.jsonl)
and [negative-control harness](https://github.com/Hexalith/Hexalith.Platform/blob/50fb0462924a765e38d830e42af242ccfaf5982f/docs/implementation/story-4-17-upstream/package-b-local-2026-11-14/negative-controls.py)
retain the detailed results and reproduction procedure outside the upstream
source diff. These are local component results, not upstream CI results.

- Full RabbitMQ unit suite: 73 top-level / 232 test and subtest pass events,
  with no skips or failures.
- Public Publish and Init focused tests, `-race -count=10`: 330 top-level /
  1000 pass events; the legacy reconnect fixture also passed 10/10.
- `gofmt -s -l`, `git diff --check`, `go vet ./pubsub/rabbitmq` and both
  `golangci-lint run --new-from-rev=96c3259b7c12a476edd208848153d3cf72a8c4f0
  ./pubsub/rabbitmq/...` runs passed with no new lint issues.
- Root and certification module tidy checks, including compatibility mode,
  passed without diffs; `make check-component-metadata` passed.
- Eighteen negative controls each detected removal of a mandatory-return
  behavior, including exchange-only and routing-key correlation checks.
- `go test -tags=integration_test -run '^$' ./pubsub/rabbitmq` compiled
  integration tests only. No real broker, integration, conformance,
  certification or Dapr runtime suite was executed.

The tests include a native in-process AMQP peer sending return then ACK,
same-exchange/key concurrent callers with duplicate message IDs, stale and
replacement generations, mismatched exchange and routing key, a closed return
listener, cancellation, mandatory/confirm validation and unchanged opt-out.

## Remaining work

- Execute integration/certification coverage against RabbitMQ, including
  return-before-ACK wire evidence, subscription recovery and persisted effects.
- Verify Dapr runtime HTTP/gRPC mapping, resiliency retries and end-to-end
  unroutable publication behavior. Component error classification alone does
  not establish that result.
- Add the `publishMandatory` reference documentation in `dapr/docs`; the
  component metadata is included here, but no docs PR is part of this change.

Partially addresses #4765, following #4697. No issue closure or supported
Dapr release delivery is claimed.

## Checklist

- [x] Code compiles correctly
- [x] Created/updated tests
- [x] Updated component metadata
- [ ] Extended the user documentation in `dapr/docs`
- [ ] Executed real-broker integration/certification and Dapr runtime tests

RELEASE NOTE: **ADD** Optional mandatory RabbitMQ publishing with correlated
unroutable-return failures while retaining the existing default.
