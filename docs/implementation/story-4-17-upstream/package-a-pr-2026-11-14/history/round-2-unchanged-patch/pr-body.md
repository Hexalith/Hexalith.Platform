# Description

When a RabbitMQ publish failed and the caller's context was canceled during the retry or reconnect wait, `Publish` returned `nil`, so a failed publish was reported as a success. The result of `reconnect` was discarded. Waits for publisher confirmations, the channel lock and connection setup did not observe the caller's context.

This PR changes the RabbitMQ pub/sub component as follows:

- **Failures are returned instead of `nil`.** Cancellation during a retry or reconnect wait now returns a retriable error, as exhausted retries already did. A broker NACK (`did not receive confirmation of publishing`) or a server-sent AMQP exception seen on an attempt is returned in preference to a cancellation or transport error. A retry that is actually confirmed still returns `nil`.
- **Reconnect errors are kept.** `ACCESS_REFUSED` (403) and stopped/closed-component errors are returned as terminal errors without further retries.
- **NACK versus shutdown.** Confirmations are read from the channel's `NotifyPublish` listener and matched by delivery tag. A real NACK is therefore distinguished from pending confirmations that `amqp091-go` completes with `ack=false` when the channel or connection shuts down. The native channel/connection close reason, for example a 403 `ACCESS_REFUSED` after publication, is retained and returned instead of a generic closed-channel error.
- **Cancellation is bounded.** The channel lock is now a `dapr/kit` `lock.Context`, acquired with the caller's context when publishing and reconnecting, and confirmations are awaited with that context. `Init` and `reconnect` pass the context to dial. Dial, the TLS and AMQP handshakes, channel open, confirm setup, exchange declaration and blocked socket writes are interrupted by closing the transport of that connection generation. The URI/default handshake timeout is kept. The cancellation callback is stopped or joined before the lock is released, so a canceled waiter cannot abort the lock holder or a newer connection. A new connection is installed only after its setup completes.

If cancellation interrupts a publication that was already written, its outcome is uncertain: the returned error does not mean the message was not delivered. The retry count, retry/reconnect delays and metadata are unchanged. There is no new metadata option, dependency or `go.mod` change.

### Tests

- `rabbitmq_publish_test.go` drives the public `Publish` method with a controllable channel fake. It covers confirmation outcomes, cancellation before start and during exchange declaration, confirmation, blocked writes and reconnect. It also covers NACKs preserved across retry cancellation and later transport failures, `ACCESS_REFUSED` termination, reconnect-failure exhaustion, concurrent waiters that must not abort the lock holder, error isolation between concurrent callers, stale connection generations and a publish queued behind `Close`.
- `rabbitmq_publish_transport_test.go` runs the real `amqp091-go` client against an in-process AMQP 0-9-1 peer over `net.Pipe`. It covers transport cancellation at dial, TLS, AMQP and authentication handshakes, channel open, confirm setup, exchange declaration, write and confirmation. It also covers native ack/nack, a NACK followed by disconnect, asynchronous `channel.close`/`connection.close` 403 after publication, publishing without confirms, close and recovery, and `Init` cancellation with the `connection_timeout` URI handshake timeout.
- `rabbitmq_test.go` adds `Init` cancellation before and during dial. Existing unit and integration tests are adapted to the context-aware lock and dial signature. The `TestMustReconnectOnAMQPError` fixture now starts with an explicitly open channel and also asserts the closed-channel case.

Run locally with Go 1.26.10:

- `go test -count=1 ./pubsub/rabbitmq`: 61 top-level tests, no skips
- `go test -race -count=10 -run '^(TestPublishPublic|TestInit)' ./pubsub/rabbitmq`
- `go test -race -count=10 -run '^TestMustReconnectOnAMQPError$' ./pubsub/rabbitmq`
- `go vet ./pubsub/rabbitmq`, `gofmt -l`, `go mod tidy -diff`
- `golangci-lint run --new-from-rev=<base> ./pubsub/rabbitmq/...` with v2.10.1: no new issues
- `go test -tags=integration_test -run '^$' ./pubsub/rabbitmq`: compile only

The integration and certification tests were not run against a RabbitMQ broker.

### Known limits of `amqp091-go`

- If the transport fails during the authentication step, `amqp091-go` returns its generic `ErrCredentials` (`Exception (403) Reason: "username or password not allowed"`) whatever the actual cause, without the broker's `ACCESS_REFUSED` reply text. It is passed through as is rather than rewritten into an `ACCESS_REFUSED` reply; because its code is 403, it ends retries. When the caller's context is canceled, the cancellation error is returned instead, since the abort itself can produce `ErrCredentials`.
- `amqp091-go` writes `close-ok` before it delivers the close reason to `NotifyClose` listeners. If that write blocks, a canceled `Publish` can return the cancellation error before the broker's close reason becomes observable.

### Not included

- Integration or certification tests against a real RabbitMQ broker, for example NACKs under `reject-publish` overflow, revoked credentials, and subscription recovery and redelivery after a transport abort.
- Mapping of these errors through the Dapr runtime publish API (HTTP/gRPC status and `ErrorInfo`).
- Mandatory publishing and correlated unroutable returns (#4765).

### Relation to #4766

#4766 changes the two retry waits so that cancellation no longer returns `nil`, which is one part of this PR. Thanks for that fix. Both PRs change the same lines in `Publish`, so they conflict. This PR selects the error differently: a NACK or server exception from an attempt is returned in preference to the cancellation error. It also covers the other paths listed in #4764. I'm happy to rebase onto #4766 if it merges first, or to split this PR into smaller ones (for example retry/reconnect error handling, confirmation handling and transport cancellation) if that is easier to review.

## Issue reference

Partially addresses #4764. The items under "Not included" remain open there.

## Checklist

* [x] Code compiles correctly
* [x] Created/updated tests
* [ ] Extended the documentation
    * [ ] Created the dapr/docs PR: N/A, no metadata, configuration or documented behavior change

🤖 Generated with [Claude Code](https://claude.com/claude-code)