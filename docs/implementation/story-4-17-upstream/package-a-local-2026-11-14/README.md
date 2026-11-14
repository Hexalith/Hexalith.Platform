# RabbitMQ Package A — reviewed local contribution

Prepared November 14, 2026 for [dapr/components-contrib #4764](https://github.com/dapr/components-contrib/issues/4764). [contribution.patch](contribution.patch) preserves publish failures and bounds cancellation across confirmation, retry, reconnect, lock and network waits. This is local source preparation, with no upstream publication or supported-runtime adoption.

Official base: [`440b15349565bb15f7a88636477864e5a9f8cc72`](https://github.com/dapr/components-contrib/commit/440b15349565bb15f7a88636477864e5a9f8cc72), obtained from the official repository on detached HEAD. [source-identity.json](source-identity.json) records the exact repository, commit, tree and observation time. Final patch SHA-256:

`ad71546ef66169c02b6b61f926b313c48d5f251e1a52e99c4129a44059dfa3b7`

The patch changes only `pubsub/rabbitmq/rabbitmq.go`, existing unit/integration test adaptations, and two focused public-Publish test files. It adds no metadata option, dependency change, mandatory publishing, correlated returns or Kafka change.

## Resulting behavior

- Real broker ACK/NACK notifications are distinguished from shutdown of pending confirmations. An actual NACK remains exactly `did not receive confirmation of publishing`, including cancellation/exhaustion after later uncertain attempts. A genuinely successful retry returns success. Preserving an earlier NACK does not establish rejection or zero effect for a later in-flight attempt.
- Native channel/connection `ACCESS_REFUSED` causes and reconnect errors retain their original text and error identity through public Publish. Permanent authorization and stopped-component failures terminate promptly. Cancellation or a generic socket failure is not invented broker-denial evidence.
- Lock acquisition and confirmation waits use the caller context. Dial, TLS/AMQP handshake, channel opening, confirm setup, exchange declaration and blocked writes can be interrupted by closing the exact active transport. Cancellation callbacks stop or join before ownership is released. Canceled lock waiters cannot abort the holder or start delayed work after release.
- Notification state is bounded per channel, with serialized access, delivery-tag matching and connection-generation isolation. Existing retry ceiling/delays and qualification time bounds remain. Interrupted in-flight publications retain uncertain effects requiring persisted readback.

## Verification and review

Authoritative commands, timestamps, exit codes and logs are in [test-results.json](test-results.json). Large JSON logs are losslessly compressed; receipts name their `.log.gz` files and retain uncompressed hashes. [log-storage.json](log-storage.json) records the mapping.

| Check | Result |
|---|---|
| Full RabbitMQ component suite | PASS: 61 top-level tests, 192 including subtests; no skips |
| Focused public Publish and Init tests under `-race -count=10` | PASS: 22 top-level / 68 including subtests per repetition, 680 pass events; no skips |
| Final corrected legacy reconnect fixture under `-race -count=10` | PASS |
| `go vet`, `gofmt`, changed-file whitespace | PASS |
| Integration-tag compilation with `-run '^$'` | PASS; no broker test executed |
| Reverse apply and forward apply to clean official source archive | PASS; applied files match the tested checkout |
| Original documentation/qualification input preservation | PASS: all 114 recorded inputs unchanged; original candidate-02 configuration hash still matches |

The [coverage map](coverage-map.json) binds every planned boundary scenario to executed passing tests.

The ten-repeat focused race pass used candidate `b49529292b89e8fc64222a4e955c9deb96b2c7039f42b788981e88814ecf19bb`. Only the legacy `TestMustReconnectOnAMQPError` fixture setup and its closed-state assertions changed afterward. Production, selected focused tests and their helpers are identical. That corrected case was separately repeated under race ten times, and the full component suite passed on the final patch. [final-subject.json](final-subject.json) records this distinction.

Three independent review layers found reproducible issues that the first green tests missed. Corrections cover asynchronous native denial, NACK followed by disconnect, conclusive-versus-transport error selection, failure exhaustion, shutdown classification, deliberate-abort cleanup and native default no-confirm behavior. Reviewers verified their corrections; all five preserved blind-review reproductions now pass. [review.md](review.md) records individual triage, follow-up results and the remaining SDK observation boundary. Original failing reproductions and intermediate logs remain, labeled separately from the final passing checks. A local artifact-verifier scratch-name collision was also corrected and its final check passed.

Go 1.26.10 was downloaded into disposable storage and checked against the official archive SHA-256; [toolchain-identity.json](toolchain-identity.json) records it. [artifact-verification.json](artifact-verification.json) binds the final patch and file hashes. The held checkout and toolchain/cache jobs are disposed at completion; historical helper scripts retain their original scratch paths as an execution record. Recreate the exact official revision under `run-with-temp` before applying the patch or rerunning the recorded component checks. No permanent toolchain/runtime installation is part of this work.

## Remaining integration gaps and delivery gate

The [concrete broker proposal](integration-proposal.md) remains unexecuted. Real RabbitMQ pressure policy/NACKs, revoked credentials, concurrent cancellation, shared-subscription recovery and unacknowledged redelivery, Dapr HTTP/ErrorInfo mapping, cold-generation control and persisted ID reconciliation still require evidence. Native protocol peers establish component behavior, not these outcomes.

The pinned AMQP SDK can replace an authentication-handshake refusal with generic `ErrCredentials`, which lacks the required `ACCESS_REFUSED` marker. It also sends close-ok before exposing NotifyClose; a blocked reply can leave the broker cause unavailable when cancellation returns. Preserve those limits and uncertainty. Neither generic error can be promoted into the unchanged accepted denial predicate.

The next delivery gate is upstream acceptance/merge or applicable backport followed by an **official supported Dapr release embedding the required component fixes**. Package B remains separately required for the missing-binding guarantee. A source patch, component merge, issue closure, partial package or native-client result is insufficient. EventStore 3.21 must independently admit exact runtime/component/source/binary/image/lock identities; Builds must align catalog/CLI/runners; Platform must separately accept an exact full-matrix execution proposal. Kafka stop rules remain independent and unresolved.

Both parent stories remain **in-progress**. Technical qualification stays **FAIL (20/24)**, all five historical guarantees **REJECTED/open**, broker selection **null**, production eligibility **FAIL**. All 24 vectors, predicates, time bounds, original evidence authority, packet custody and the December 13 Administrator review remain unchanged. No comments, PR, push, Dapr runtime adoption or Platform qualification run occurred.
