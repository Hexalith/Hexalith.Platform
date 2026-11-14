## Expected Behavior

RabbitMQ publish failures should reach the public `Publish` caller and Dapr API within the
caller's existing deadline. Broker NACK and revoked-credential errors must remain attributable
when the final outcome is failure; cancellation must not be returned as success. A successful
retry should retain its actual outcome. An uncertain in-flight effect requires separate readback.

This follows the NACK part of [#4697](https://github.com/dapr/components-contrib/issues/4697)
and [the 1.19 backport #4748](https://github.com/dapr/components-contrib/pull/4748). It concerns
the surrounding publish/reconnect/cancellation path, rather than re-reporting the fixed helper.

## Actual Behavior

Read-only checks on November 14, 2026 found Dapr v1.19.1 still declaring components-contrib
v1.19.0 in its [immutable go.mod](https://github.com/dapr/dapr/blob/cfb5ee090dda7cfc26145c7703605d831350aa2a/go.mod).
The backport is present in release-1.19 source but is not in that component release.

In the [inspected release-1.19 source](https://github.com/dapr/components-contrib/blob/46cf4f95b0c84ecb4014b6f79103a1b1ca9c5fbc/pubsub/rabbitmq/rabbitmq.go#L224-L345):

- `confirmationError` uses a context-free `Wait()`.
- Both retry waits return `nil` when `ctx.Done()` fires.
- The reconnect return value is discarded.
- `publishSync` acquires `channelMutex` and declares the exchange before publishing. Lock,
  exchange and connection waits need inclusion in the deadline analysis too.

The inspected [main revision](https://github.com/dapr/components-contrib/blob/440b15349565bb15f7a88636477864e5a9f8cc72/pubsub/rabbitmq/rabbitmq.go)
has the same relevant paths. A lower-level NACK fix does not establish their end-to-end outcome.

Our retained Dapr-path qualification observed no accepted pressure rejection and three
acknowledged IDs absent at its bounded readback. The stale-credential publication timed out;
its producer recorded synthetic status 599, rather than a native broker error. The new-generation
control worked. These are historical observations, not a fresh reproduction or proof that one
specific source path caused the timeout or that the missing effects were permanently lost.

## Steps to Reproduce the Problem

The [published work package and complete, unexecuted fixture recipes](https://github.com/Hexalith/Hexalith.EventStore/blob/51dd68b86a690ffe9e11c565dccec49b2d401a94/docs/implementation/story-3-21-rabbitmq-upstream-work-packages.md#concrete-reproduction-inputs-and-request-steps)
provide the immutable component/Subscription inputs and request commands. Use fresh fixture-local
TLS credentials; no private qualification packet or credential material is required.

1. Enable publisher confirms, durable delivery and the existing quorum queue. Stop its consumer,
   apply/inspect `overflow=reject-publish,max-length=4` on the exact backpressure queue, and publish
   eight distinct IDs through Dapr. Retain native NACKs, Dapr responses and elapsed time; restart
   the consumer and reconcile every ID in the existing ten-second readback window.
2. Exercise cancellation separately during confirmation, retry, reconnect, mutex acquisition,
   exchange declaration and connection establishment. Check at the public `Publish` boundary;
   timer-only tests or a mock returning no confirmation cannot cover those paths.
3. Keep the old publisher alive, create the new credential generation and restart the consumer/new
   publisher. Retain the new-generation positive control, revoke the old credential, and publish
   through the identified old endpoint. Capture reconnect errors, Dapr response and persisted effects.

These recipes have not been executed as part of preparing this issue. The pinned released
runtime and inspected backport source are distinct inputs; source inspection is not attestation
that the historical runtime embedded the backport.

## Requested behavior and verification

- Given a NACK and final publish failure, when it reaches Dapr, then the attributable
  `did not receive confirmation of publishing` error survives; a real successful retry remains success.
- Given cancellation at any listed wait, when the budget expires, then termination is bounded,
  returns non-success and starts no delayed publication/retry after observing cancellation.
- Given revoked-credential reconnect returning `Exception (403)` / `ACCESS_REFUSED`, when the
  operation fails, then that error is preserved instead of being discarded or replaced by success.
  A generic timeout is not a fabricated broker denial.
- Given our existing qualification faults, when independently evaluated through Dapr, then
  the separate paused-broker cancellation request returns synthetic 599 within ten seconds, acknowledged loss
  is zero within ten-second recovery/readback, the new-generation effect occurs exactly once,
  and no stale effect appears within the three-second window. Preserve existing request timeouts.
  A separate cold-old-generation control cannot replace the live old-session observation.

Please extend the existing component and integration/certification tests at `Publish`, including
stalled concurrent callers. No new retry service or private Dapr runtime is proposed. Supported
release delivery and independent Dapr-path qualification would still be needed after a source fix.

## Release Note

RELEASE NOTE: **FIX** Preserve RabbitMQ publish failures through cancellation and reconnect, and bound publish waits by the caller's deadline.
