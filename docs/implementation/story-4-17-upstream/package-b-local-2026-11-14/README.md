# RabbitMQ Package B — local contribution, prepared and not submitted

Prepared November 14, 2026 for [dapr/components-contrib #4765](https://github.com/dapr/components-contrib/issues/4765), the mandatory/return follow-up to #4697. [contribution.patch](contribution.patch) adds opt-in mandatory publishing whose `basic.return` is bound to the exact attempt and channel generation, resolved before success and carried through public `Publish` as a conclusive error. It stacks on Package A ([PR #4767](https://github.com/dapr/components-contrib/pull/4767)). This is local source preparation only: nothing was pushed, forked, opened or commented on.

Package A was OPEN with all six upstream workflows awaiting maintainer approval (`action_required`) and no reviews or comments as of 2026-11-14T11:54Z, so nothing on it was actionable. The owner's "Adopt all" decisions fix the base at Package A's head, make an unroutable return terminal (`pubsub.NewTerminalError`, FailedPrecondition, no retry) and make `Init` reject `publishMandatory=true` without `publisherConfirm`. Submission waits for Package A, or ships as a stacked PR, in a later separately approved slice.

## Subject

| Item | Value |
|---|---|
| Base | Package A head `96c3259b7c12a476edd208848153d3cf72a8c4f0` (tree `055ab8a8a908eacb09828847232c94ed1c74b07f`, sole parent official `440b15349565bb15f7a88636477864e5a9f8cc72`) |
| Base source | Fetched from the retained [Package A bundle](../package-a-pr-2026-11-14/package-a-pr.bundle) (SHA-256 `2b76da6c…9c51`, matching its submission record). A read-only `ls-remote` at 13:16:13Z found `refs/pull/4767/head` at this commit and official main still at `440b1534`; see [source-identity.json](source-identity.json). |
| Patch | [contribution.patch](contribution.patch), relative to the base, SHA-256 `7d55faea09b30fe31228566601e72e04b2158bc64a65d9d45fb086c16a31e392`, 50637 bytes (review fixes; supersedes `422dda73031e0bcbcf7a5e39329581f00a70486c2f7d23e9314041c4aaf3a29f`) |
| Changed files | `pubsub/rabbitmq/{metadata.go,metadata.yaml,metadata_test.go,rabbitmq.go,rabbitmq_integration_test.go,rabbitmq_publish_test.go,rabbitmq_publish_transport_test.go}`: 1015 insertions, 10 deletions |
| Local test subject | Recreated by every run as a disposable signed-off commit whose tree equals base plus patch. The final run's subject is commit `25bc4c802593389726c090ab282451b65b2e295a`, tree `50f8ef2d3d3a240595d6088917f9d51afe04ec3c` (sole parent the base). The superseded patch's subject was commit `abaa70271227406cf82920a8a313133cc25885a7`, tree `a7bc605f9388bbb0308f7a08d2cff9b2fa6123b3`. It sits on local branch `local/rabbitmq-mandatory-returns-4765`; see [commit-identity.json](commit-identity.json). Its [message](commit-message-input.txt) is a test-vehicle text, not approved PR text. |

## Resulting behavior

- **Opt-in API.** New metadata `publishMandatory` (bool, default false), documented in `metadata.yaml`. `publishMandatory=true` with `publisherConfirm` false or absent fails `Init` before any dial: `rabbitmq pub/sub error: publishMandatory requires publisherConfirm to be true`.
- **Opt-out is unchanged.** With the option off, publications keep `mandatory=false, immediate=false` on the wire and no return listener is registered. Existing tests pass without behavioral edits. In existing test code the patch only adds a default assertion and two `publishMandatory` subtest loops to `TestCreateMetadata`, plus two counters in Package A's fake broker, which now records the mandatory and immediate bits it previously discarded.
- **Mandatory mode.** Publications use `mandatory=true`, never `immediate` (RabbitMQ answers that with a connection close). Each channel adapter registers one `NotifyReturn` listener of capacity one before its first mandatory publication.
- **Unroutable.** A return followed by the attempt's own ACK fails that call at once: `broker returned the mandatory message as unroutable: 312 NO_ROUTE`. The existing terminal check after `publishSync`, extended with the unroutable sentinel, returns it wrapped by `pubsub.NewTerminalError` (FailedPrecondition) and stops the retry loop; the attempt warning is still logged. The component does not retry it; the Dapr runtime still may, unless a resiliency `matching` rule excludes the code. A return observed with an ACK that arrives as the caller's context ends is still reported; cancellation, timeout or retry exhaustion never replaces it.
- **Mismatched return.** A return naming another exchange or routing key fails the attempt with a retriable, non-conclusive error. The generation is not reused; the existing retry reconnects. If all attempts mismatch, `Publish` returns that error with code Unavailable, never success or an unroutable claim.
- **Unresolved attempt.** When a mandatory publication may have reached the channel without an observed ACK or NACK (cancellation, write or confirmation failure, mismatch), the generation is marked under `channelMutex`. No later publication uses it; the attempt or the next caller reconnects to a fresh channel and listener. Cancellation before ACK stays uncertain: Package A's transport abort runs unchanged, and no rejection is inferred.
- **Unchanged Package A machinery.** Retry count and delays, error retention, NACK text, authorization handling, abort ordering and restored logs are unchanged. A NACK stays `did not receive confirmation of publishing` even if a return accompanied it.

## Correlation design and deviations from the code map

Correlation uses Package A's invariant: `channelMutex` keeps at most one publication outstanding per channel, held through confirmation. amqp091-go v1.14.0 dispatches a fully assembled `basic.return` to its listener before handling the next frame (`channel.go` `recvContent`/`dispatch`). RabbitMQ sends the return before the ACK of the same publication ([publisher confirms](https://www.rabbitmq.com/docs/confirms)). So once an attempt receives its own ACK, a buffered return belongs to that attempt; `outcome` reads it without blocking. The existing pre-publish drain loop also empties the return listener, which only ever holds a return from a resolved attempt (a NACK with a return). A generation with an unresolved mandatory attempt is never published on again, which closes the remaining gap. These rules also keep the capacity-one listener from filling. A full listener would stall the SDK reader for five seconds and then drop the return (`channel.go` `notifyAll`). Correlation holds no maps, unbounded buffers, background goroutines or application `MessageId` reads; duplicate IDs are harmless.

Two implementation choices differ from the spec's code map without changing its intent:

- The listener is registered by the channel adapter before that channel's first mandatory publication, not inside `dialWithConfig`. The dial seam (`connectionDial` and `dialWithConfig`) has no mode parameter. Adding one would change 14 `connectionDial` replacements and 6 `dialWithConfig` calls in existing tests, while the opt-out row requires unchanged tests. The listener remains per channel generation and exists only in mandatory mode.
- The forced reconnect reuses `mustReconnect`'s existing nil-channel rule. `publishSync` returns a nil channel for a marked generation, so `mustReconnect` is unchanged. `reset()` clears the mark together with the generation.

## Disclosed consequences and limits

- A forced reconnect calls Package A's `reset()`, which aborts the shared connection. Subscriptions on it reconnect, and unacknowledged deliveries are redelivered. On cancellation Package A already aborts the transport. The new cases are a mismatched return or a confirmation/write failure that leaves the channel live. This is an implementation consequence, not real-broker evidence.
- A mismatched return fails that attempt as non-conclusive, so the retry can duplicate a message the broker had already ACKed; delivery stays at-least-once, as with Package A's retries.
- A later caller that meets a marked generation spends one of its three attempts on the refusal, then reconnects after `reconnectWaitSeconds`, as with an uninitialized channel today.
- A mismatched or stale return on a live channel cannot be produced by RabbitMQ on demand. Protocol-peer and fake-broker tests cover those cases; the integration proposal only measures their observable consequences.
- Dapr HTTP status mapping of the FailedPrecondition error, runtime resiliency retries and persisted effects are **not claimed**. The [integration proposal](integration-proposal.md) holds the unexecuted broker/Dapr recipe.

## Checks

[test-results.json](test-results.json) is **PASS**. All 15 required receipts and the supplementary negative controls bind the local test commit with a clean tree before and after. [required-checks.jsonl](required-checks.jsonl) is Package A's 14-check set, with `--new-from-rev`, `diff --stat` and `diff --check` set to `96c3259b`. Its gofmt list also covers the two metadata Go files, and it adds `make check-component-metadata`. [toolchain-identity.json](toolchain-identity.json) records Go 1.26.10, verified against the go.dev archive SHA-256, and golangci-lint v2.10.1, verified against its release checksums.

| Check | Result |
|---|---|
| `git diff --stat 96c3259b HEAD` | only the seven `pubsub/rabbitmq` files; 1015 insertions, 10 deletions |
| `gofmt -s -l` on all seven Go files changed by Packages A and B; `git diff --check`; `go vet ./pubsub/rabbitmq` | PASS, empty output |
| `golangci-lint run --new-from-rev=96c3259b ./pubsub/rabbitmq/...`, default and unlimited counts | PASS, `0 issues.` |
| Full unit suite | PASS: 73 top-level / 232 test and subtest pass events, no skips or failures (floor 62/196) |
| Focused public Publish and Init, `-race -count=10` | PASS: 330 top-level / 1000 pass events, 100 unique names, no skips or failures (floor 230/720 and 72 names). The test binary took 86.4 s of its 180 s budget (Package A: 81.7 s). |
| Legacy reconnect fixture, `-race -count=10` | PASS: 10/10 |
| Integration tag with `-run '^$'` | PASS; compile only, so `TestMandatoryUnroutablePublish` was not executed |
| Root `go mod tidy -diff` and `-compat=1.20 -diff`; certification module tidy and compatibility tidy | PASS, no diff |
| `make check-component-metadata` | PASS, exit 0; the negative control that removes the `publishMandatory` entry makes it fail |

[summarize-checks.py](summarize-checks.py) re-hashes every log and requires exact argv, exit 0, a clean committed subject and Package A's final count floors. It also requires the current patch bytes and every [coverage-map.json](coverage-map.json) test name passing in its named log. It returns FAIL and a nonzero exit otherwise. [workspace-lifecycle.json](workspace-lifecycle.json) records both held jobs finishing with exit 0 and their directories removed; [run-checks.out](run-checks.out) is the raw run output.

## Negative controls

[negative-controls.py](negative-controls.py) ran in the same held checkout after the required checks. Each of 18 controls weakened one behavior, ran the mandatory-focused tests (or the metadata check) and restored the exact bytes. The controls cover:

- the post-ACK return check, the pre-publish return drain and listener registration;
- marking, refusing and clearing an unresolved generation, and keeping opt-out unmarked;
- terminal classification, the whole mismatch check, its exchange half and its routing-key half, recording the attempt's routing key, and the mandatory bit;
- treating a NACK or a return as resolved, and success on a closed listener after the ACK;
- the `publisherConfirm` rule and the `metadata.yaml` entry.

The final run detected all 18 controls (the superseded run detected its 15). Detection means tests failed, or for metadata the analyzer named `publishMandatory`. No control caused a build failure or panic, and the checkout was byte-identical and clean afterwards ([negative-controls.receipt.json](negative-controls.receipt.json)). The controls show test sensitivity; they are not broker evidence.

## Reproduce

All disposable sources, tools, caches and build outputs live beneath `run-with-temp`'s private TMPDIR; keep these evidence files outside it. The scripts write receipts next to themselves, so copy them (with `contribution.patch`, `commit-message-input.txt`, `required-checks.jsonl` and `coverage-map.json`) to a new directory beside `package-a-pr-2026-11-14/` before reproducing. From that directory run `python3 -B run-checks.py`. It starts [workspace.py](workspace.py) and [checks.py](checks.py) as two held `run-with-temp` jobs and feeds them every required check plus [negative-controls.py](negative-controls.py). It then releases both jobs, records [workspace-lifecycle.json](workspace-lifecycle.json) and runs `summarize-checks.py`. Remote access is read-only: `ls-remote`, a fetch of the official base by SHA, toolchain downloads and the Go module proxy. The `remoteReads` and `remoteWrites` lists in `workspace-lifecycle.json` are the procedure's declared lists, not a network trace; `make check-component-metadata` also resolves `github.com/dapr/components-contrib@main` through the module proxy. A rerun creates a new local commit SHA with the same tree. Large logs are stored as deterministic gzip, with uncompressed and stored hashes in their receipts. The root `.gitignore` excludes `.log` files, so record this directory with `git add -f` for the useful logs.

## Not done, and unchanged dispositions

Not done: any push, fork, pull request, issue or PR comment or label (#4764–#4767); any Platform or EventStore push; any broker, Dapr runtime, conformance/certification suite or qualification run. No alternate exchange, routing service or application-side provider access was added. Neither upstream acceptance, Dapr HTTP mapping nor a delivery gate is claimed. Not included: a dapr/docs RabbitMQ component reference update for `publishMandatory`. Another session's Platform commit `70a016a98c97c19656a4c279a93b88e255d41b07` published the draft spec; this slice itself pushed nothing. Review round 1 ran three independent layers. It applied 15 test, wording and record fixes, including the exchange-only and routing-key mismatch controls; the only production edit folds the unroutable check into the existing terminal condition. It deferred one shared-submodule hazard and rejected the rest with evidence; see the spec's Review Triage Log.

Both parent stories remain **in-progress**. Technical qualification stays **FAIL (20/24)**, all five historical guarantees **REJECTED/open**, broker selection **null** and production eligibility **FAIL**. All 24 vectors, predicates, time bounds, original evidence authority, Kafka stop rules, packet custody and the December 13 Administrator review remain unchanged. The delivery gate is still an **official supported Dapr release embedding the required Package A and Package B fixes**. EventStore exact runtime/component/source/binary/image/lock admission, Builds alignment and a separately accepted Platform full-matrix proposal follow.
