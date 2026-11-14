## Describe the feature

Please support opt-in mandatory RabbitMQ publishing with correctly correlated unroutable returns.
This follows the unresolved mandatory/return part of
[#4697](https://github.com/dapr/components-contrib/issues/4697), which is closed/completed.
The NACK backport [#4748](https://github.com/dapr/components-contrib/pull/4748) addresses a different
failure mechanism. A publisher confirm alone can acknowledge an unroutable message.

The [inspected release-1.19 publish path](https://github.com/dapr/components-contrib/blob/46cf4f95b0c84ecb4014b6f79103a1b1ca9c5fbc/pubsub/rabbitmq/rabbitmq.go#L282-L345)
still passes `mandatory=false` and has no return handler. The
[inspected main revision](https://github.com/dapr/components-contrib/blob/440b15349565bb15f7a88636477864e5a9f8cc72/pubsub/rabbitmq/rabbitmq.go)
has the same relevant behavior. Read-only checks on November 14, 2026 found the latest Dapr
and component releases still v1.19.1 and v1.19.0, respectively.

Our retained qualification measured physical absence of the missing-binding topic's binding
both before and after its publication. Dapr returned 204, beside a passing routed control.
That is a historical observation, not a new reproduction during issue preparation. An opt-in
failure mode lets callers distinguish unroutable publication from accepted delivery.

## Proposed behavior

Keep the existing default. An opt-in metadata setting could be named `publishMandatory`;
the actual API name belongs to upstream. Require publisher confirms for that mode or reject
an incompatible configuration. Bind each `basic.return` to its exact attempt and channel
generation, and resolve return/confirmation ordering before reporting success.

RabbitMQ documents [returns preceding positive confirms](https://www.rabbitmq.com/docs/confirms).
A later ACK must not overwrite the returned failure. Carry the conclusive unroutable error through
the public `Publish`/Dapr path within the current HTTP budget. Use final classification or bounded
retry handling that preserves the error; retry exhaustion into a generic timeout is insufficient.

Keep correlation bounded within the existing publisher machinery, including concurrent callers,
retries, duplicate application IDs, cancellation and replacement channels. No alternate exchange,
routing service or application-side provider access is proposed: routing an otherwise unbound
message would not demonstrate rejection.

## Reproduction and verification

The [published work package](https://github.com/Hexalith/Hexalith.EventStore/blob/51dd68b86a690ffe9e11c565dccec49b2d401a94/docs/implementation/story-3-21-rabbitmq-upstream-work-packages.md#concrete-reproduction-inputs-and-request-steps)
contains immutable component/Subscription inputs and complete unexecuted fixture recipes.
Use a fresh owned TLS fixture with local credentials and distinct bound-control/unbound IDs.

1. Preserve a bound `qualification.events` positive control and physically remove/inspect the
   binding for `qualification.missing-binding`.
2. Once the proposed mode exists in a supported component, enable it with publisher confirms and publish a unique unbound
   ID through Dapr. Retain `basic.return` / `312 NO_ROUTE`, subsequent confirmation and the Dapr
   response, then inspect bindings again and retain ID-based readback.
3. Verify concurrent/retried calls, reused application IDs, cancellation and channel replacement;
   an old return must not fail a later unrelated call. Verify opt-out compatibility too.

Given the unbound mandatory publication, when return precedes ACK, then the exact call must fail
within its existing budget. Given the routed control, when published, then success and exactly
one persisted effect must remain. Our qualification requires HTTP 400–598, physical binding
absence before/after and `allowedEffects == 1` for this vector; an error type name alone does
not establish that Dapr response. Transport uncertainty must stay explicit.

These recipes have not been executed during issue preparation. Extend existing metadata,
component and integration/certification tests; native AMQP success or a source merge alone
would not supply supported-runtime Dapr-path qualification.

## Release Note

RELEASE NOTE: **ADD** Optional mandatory RabbitMQ publishing with correlated unroutable-return failures while retaining the existing default.
