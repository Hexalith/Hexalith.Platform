# Story 4.17 — offline broker remediation intake (2026-11-13)

Status: **offline analysis and owner handoff prepared; no successor candidate is ready.**
Platform 4.17 and EventStore 3.21 remain **in-progress**. Technical qualification remains
**FAIL, 20/24**; all five historical guarantees remain **REJECTED/open**. Broker selection
is **null** and production eligibility **FAIL**.

The owner selected the three recommended routes: preserve the Kafka pressure guarantee and
investigate a genuine rejecting fault; analyze Kafka rotation causality offline; and prepare
the RabbitMQ supported-delivery intake while retaining its requirements. These decisions
authorize this analysis, not a changed predicate, exception, private runtime or new run.

## Governing baseline

The [candidate-02 assessment](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-02-hosted-attempt-1/attempt-assessment.json)
governs the [hosted report](story-4-17-broker-candidate-02-hosted-attempt-1.md). Its
[vector assessment](../../_bmad-output/implementation-artifacts/evidence/4-17/candidate-02-hosted-attempt-1/vector-assessment.json)
records 20 verified PASS and four verified FAIL, with integrity verified and cleanup credited.

| Historical guarantee | Candidate-02 observation | Current disposition |
| --- | --- | --- |
| Kafka pressure | No attributable rejection; acknowledged loss 0 | REJECTED/open |
| Kafka rotation | Attributable authorization denial, new effect once, no stale effect within the observation window | One verified PASS; historical failure unadjudicated, REJECTED/open |
| RabbitMQ pressure | No attributable rejection; acknowledged loss 3 at the bound | REJECTED/open |
| RabbitMQ rotation | Stale publish timed out, synthetic status 599 | REJECTED/open |
| RabbitMQ missing binding | Physically absent binding; unbound publish returned 204 | REJECTED/open |

Run 43386723091 used Platform `4d66ff5584e4f62239551bb02311649334171a84`, EventStore
`495dd24943031ec75e2822c6168ada3c8b9fdb82` and original configuration SHA-256
`b8601e3a6d4e8460d039573ca2f7e1728e52aa67e61fc0710d74cab5d072580b`.
Current checkout identity is not substituted for those execution identities.

## Kafka pressure: the replacement fault remains an engineering dependency

The accepted [`pressure_rejection`](../../references/Hexalith.EventStore/tools/dapr_broker_qualification.py)
requires the Dapr publish error to identify Kafka's throttling-quota rejection on
`qualification.backpressure`. The producer currently applies `producer_byte_rate=1024`.
Candidate-02 removed the fixture-retention loss, but its two 204s and six 500s still supply
no accepted rejection. A timeout remains an uncertain publication, including when it later persists.

Kafka 4.3.1 resolves to `26b251a451ce941d3d7a55e6487bcb7f16b5ad48`. Its
[Produce handler](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/core/src/main/scala/kafka/server/KafkaApis.scala#L485-L553)
applies bandwidth/request quotas through channel throttling and returns partition results with
a delay; quota excess does not replace those results with error 89. The
[strict controller-mutation quota](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/server/src/main/java/org/apache/kafka/server/quota/StrictControllerMutationQuota.java#L55-L64)
can throw that error, but it serves administrative mutations, as illustrated by
[topic creation](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/core/src/main/scala/kafka/server/ControllerApis.scala#L362-L387).
It is not evidence of rejection of a Dapr publication to the already-declared topic.

**Conclusion from these sources:** the present fault cannot meet the unchanged predicate.
No supported Produce quota mechanism yielding that predicate was identified. This is not a
universal claim about every possible fault or future Kafka implementation. Request-rate quotas,
fetch throttling, administrative rejection, message-size errors and ordinary timeouts are not
demonstrated replacements for the accepted producer-pressure proof.

**EventStore 3.21 intake:** identify and demonstrate a supported, command-bound producer-pressure
fault reaching the accepted Dapr rejection. Preserve the full matrix, real publishing path,
cancellation bound, exact acknowledged-ID reconciliation, retention and recovery. If feasibility
cannot be demonstrated, retain FAIL and bring any proposed guarantee/predicate change back to
the owner as a separate decision. No executable replacement fault is supplied by this analysis.

## Kafka rotation: source comparison narrows, but does not establish, causality

| Capture | Available result | Evidence authority |
| --- | --- | --- |
| November 9 runs 42532773702 / 42532776864 | Stale publish returned 204 and its ID persisted after the deny ACL | Historical result in the [dated dispositions](story-4-17-local-broker-dispositions-2026-11-10.md); recovered receipts/readback agree |
| Candidate-01 attempt 2, run 43333995506 | Producer reported PASS | Packet verifier rejected integrity; no guarantee credit |
| Candidate-02, run 43386723091 | Stale publish returned an attributable Kafka authorization error after 0.41 s; no stale effect observed | Independently verified PASS for this capture |

The rotation scenario and denial predicate are unchanged across historical EventStore
`0a6725d51bb17a9eae28a24a53f3636bab9d48d7`, candidate-01
`02a4d3b2c9fedd18ea2da4d754ab4b68f7e29213` and candidate-02
`495dd24943031ec75e2822c6168ada3c8b9fdb82`. All keep the old publisher alive while creating the
new credential generation, restart the startup-loaded consumer/new publisher, revoke the old
principal through topic ACLs, inspect ACL state and test the old endpoint.

Historical-to-candidate tuning changes affect consume retry (1 s to 200 ms) and initial group
rebalance delay (0 to 100 ms). Candidate-01-to-02 leaves the Kafka tuning and rotation sequence
unchanged. These facts do not establish that either tuning caused the denial to start working.
Recovered packets show the historical failures waited about 17.91 s and 17.67 s after the
`qualification.events` deny command completed, versus 12.50 s and 12.44 s for the later passes.
All four list the same logical ACL entries before the stale
publish. Thus a simple explanation that the passing runs merely allowed more propagation time
is contradicted by the observed timing. This does not exclude a propagation fault.

| Capture | Deny receipts (events first; stride 2) | ACL list / stale publish | Events deny → stale start | Last deny → stale start |
| --- | --- | --- | --- | --- |
| Nov9 standalone | 852–864 | 866 / 867 | 17.908967 s | 2.499353 s |
| Nov9 fixture | 844–856 | 858 / 859 | 17.671233 s | 2.560966 s |
| Candidate-01, diagnostic only | 1330–1342 | 1344 / 1345 | 12.499999 s | 1.761552 s |
| Candidate-02, verified | 884–896 | 898 / 899 | 12.438183 s | 1.722227 s |

Indexes are zero-based in each packet's `commands.json`. Intervals subtract the deny command's
`endedUtc` from the stale request's `startedUtc`; these are worker wall-clock observations,
not independently attested monotonic timing. The ACL list includes the old principal's DENY
alongside its existing allows and the rotated principal's allows. The stale request starts
about 0.00012–0.00013 s after the list command finishes in all four packets.

This sanitized receipt comparison is retained here so its arithmetic survives artifact expiry.
Times are UTC, copied from the above receipts; the run labels use the historical November 9
designation even where the receipt's UTC date is November 8.

| Capture | UTC date | Events deny ended | Last deny ended | ACL list ended | Stale started |
| --- | --- | --- | --- | --- | --- |
| Nov9 standalone | 2026-11-08 | 23:02:46.971212 | 23:03:02.380826 | 23:03:04.880057 | 23:03:04.880179 |
| Nov9 fixture | 2026-11-08 | 23:06:01.672103 | 23:06:16.782370 | 23:06:19.343206 | 23:06:19.343336 |
| Candidate-01, diagnostic only | 2026-11-13 | 00:09:33.572874 | 00:09:44.311321 | 00:09:46.072747 | 00:09:46.072873 |
| Candidate-02, verified | 2026-11-13 | 07:38:53.745269 | 07:39:04.461225 | 07:39:06.183323 | 07:39:06.183452 |

For a recovered packet, read the indexes from `observation.json`'s `denialObservation`,
subtract the referenced `commands.json` times, and count the stale ID's effect rows in
`application-readback.json`. All four have one new-generation effect; stale effects are
respectively 1, 1, 0 and 0. Normalize the ACL-list output by stripping each line containing
`principal=`, sorting those lines, and joining with LF without a trailing newline. The normalized
set hashes to `f989dd74ef7b01a5a934ec979136437aed34539eb59e9910bd991615d36406de` in every packet.
These retained values permit arithmetic/comparison checks after expiry; re-examining the raw
packet still requires the original archive or independent retained custody.

An administrative ACL list does not prove the authorizer state seen by the next Produce request,
and the harness does not bind that request to a measured authenticated broker principal.
ACL propagation/request timing and effective-principal identity remain hypotheses rather than
an established cause. The unchanged mTLS identity model supplies no basis for a SASL reauthentication remedy.

**EventStore 3.21 intake:** adjudicate the source/receipt comparison and identify the minimum
missing observations needed to distinguish propagation from identity. A future evidence plan
must bind the actual old publisher/process and credential generation, effective principal,
installed and applied ACL state, stale request and persisted readback. Retain the positive
new-generation control. Cold old-generation denial remains unproved. The owner has selected
analysis rather than another run or acceptance of the lone PASS; no guarantee is closed here.

### Recovered artifact identities

All four ZIPs were retrieved read-only into temporary storage, checked against their API or
retained SHA-256 commitments and inspected for unsafe entries before extraction. They were not
added to the repository. Candidate-01 remains diagnostic-only despite matching archive bytes.

| Capture | Artifact ID | ZIP SHA-256 |
| --- | --- | --- |
| Nov9 standalone | 13638347236 | `20a1719b753c7379207bbeb92cc73f70af99b7fa7e71734fd4a58ec7427cf223` |
| Nov9 fixture | 13638293057 | `20643dbd7d3702f3f504b0a4ce5fd3fe627ff32493d00624d37dca40e81b8a27` |
| Candidate-01 attempt 2 | 13995674927 | `906c0d943e7a0ee8393bb83b96c4c917477fe18820afe37fe6ba7274afe7aeb7` |
| Candidate-02 | 14018214306 | `a26b848840ca8061a00d8ea7598b52b6a91b5331822645c23bc527278e40949a` |

The November artifacts belong to `Hexalith.EventStore`; the candidate artifacts belong to
`Hexalith.Platform`. Retrieve by artifact ID under those repositories' Actions APIs and verify
the ZIP hash before inspecting the packet. Relevant packet-relative files are `commands.json`,
`kafka/secret-rotation/{observation,application-readback,broker-diagnostics}.json` and
`execution-tools/qualify-dapr-brokers.py`. Historical readback places the stale receipt/effect
at `/ledger/49` and `/ledger/59`; later readbacks contain no stale-ID row within their bounds.

Within class `Run`, select method `scenario` (not `reconcile`). Hash its original bytes from the
eight-space-indented `if scenario == "secret-rotation":` line through immediately before the
eight-space-indented `if scenario == "cancellation-backpressure":` line, retaining indentation
and line endings. This block has identical SHA-256
`4e021674fcc83ce239cc0974abd1bf4f085b1f736e58f717214a4029b3de6613` in all four archived tools.
Runtime/CLI/component
versions and Dapr-binary/Kafka-image digests also match. These checks narrow the comparison;
they do not authenticate a missing broker principal or confer new qualification credit.

## RabbitMQ: supported delivery required through the entire publish path

Checked read-only on November 13: latest releases remain
[Dapr 1.19.1](https://github.com/dapr/dapr/releases/tag/v1.19.1) (October 31) and
[components-contrib 1.19.0](https://github.com/dapr/components-contrib/releases/tag/v1.19.0)
(October 22). The [NACK backport #4748](https://github.com/dapr/components-contrib/pull/4748)
merged November 6, so neither release supplies it.

The inspected upstream `release-1.19` source is
[`46cf4f95b0c84ecb4014b6f79103a1b1ca9c5fbc`](https://github.com/dapr/components-contrib/blob/46cf4f95b0c84ecb4014b6f79103a1b1ca9c5fbc/pubsub/rabbitmq/rabbitmq.go#L282-L345);
`main` is
[`440b15349565bb15f7a88636477864e5a9f8cc72`](https://github.com/dapr/components-contrib/blob/440b15349565bb15f7a88636477864e5a9f8cc72/pubsub/rabbitmq/rabbitmq.go#L282-L345).
Both return a confirmation error from `publishSync`, but `Publish` still retries with two-second
waits, returns success on cancellation in those waits, and discards reconnect errors. Both still
publish with `mandatory=false`; no return handler was found in the inspected component file.
**Inference:** the backport is a necessary candidate input for the observed NACK defect, not a
demonstrated end-to-end remedy. In particular, failure propagation under the outbound deadline
must be proven rather than inferred from its lower-level fix.

| Owning work | Required supported delivery | Closure evidence |
| --- | --- | --- |
| EventStore 3.21 / upstream RabbitMQ component: pressure | Surface NACK through retry/cancellation/deadline handling to Dapr | Effective reject-publish policy, attributable error on the exact topic, zero acknowledged loss, bounded cancellation and persisted readback |
| EventStore 3.21 / upstream component: rotation | Surface revoked-credential denial rather than discarding reconnect failure or reporting success on cancellation | Attributable ACCESS_REFUSED, new-generation effect once, controlled restart and stale-effect observation; outstanding cold-generation control retained |
| EventStore 3.21 / upstream component: missing binding | Mandatory publishing with correlated return handling, not only negative confirmations | Bound positive control, measured binding absence before/after and rejected unbound publication |
| EventStore 3.21 then Builds | Released supported runtime embedding the accepted fixes, exact profile/tool identities | Independent lock admission and runtime/component inventory, followed by Platform-owned full-matrix execution under an exact approved proposal |

These are prepared dependency requests against existing owning work, not owner acceptance or
claims that upstream delivery exists. An alternate exchange would route the message rather than
prove rejection of the missing binding. No private build, exception or catalog-pin change is
part of this handoff.

## Diagnostic proposal and qualification readiness

1. EventStore 3.21 accepts the bounded question, assertions and evidence plan. A diagnostic
   proposal may investigate an unproved remedy or unresolved rotation behavior using supported
   inputs; it must label its hypotheses and expected failures. Presenting a remedy as available
   requires demonstrated fault feasibility or supported delivery.
2. Platform prepares exact source, lock, original configuration hash, worker/tool requirements,
   predicted results and independent verifier calls. Reuse the existing producer and manual
   workflow; preserve all 24 vectors and the immutable candidate-02 source.
3. Obtain the separate exact execution decision. A future diagnostic proposal may explicitly
   expect failures; those remain FAIL and cannot grant technical or production eligibility.

Missing fixes or unresolved rotation evidence block qualification claims; they do not forbid an
explicitly bounded diagnostic proposal addressing a useful question. Undeclared/unsupported
inputs or identity drift block execution admission. An impossible pressure-attribution path
cannot be advertised as a remedy or satisfy qualification. No further diagnostic proposal is
authorized by this offline decision. Broker selection additionally requires authenticated AD-26 decisions and
the Story 4.9 profile inventory. Staging acceptance still needs 4.14–4.16 and exact shared-host
rehearsal/restore evidence. Existing declaration-derived Subscription rendering must be consumed,
not duplicated. This offline slice supplies none of those later acceptance results.

## Offline verification

- Reconciled the two governing JSON assessments: 24 observed, 20 PASS, four FAIL, five historically
  rejected rows, verified integrity, credited cleanup, null selection and production FAIL.
- Matched the original candidate-02 JSON to its assessment SHA-256. No current source was
  substituted for an execution input, and no historical assessment was rewritten.
- Compared the rotation source across the three canonical EventStore revisions and inspected
  primary immutable Kafka/RabbitMQ sources. Residual causal uncertainty remains explicit.
- Checked local links and changed-file whitespace. No new broker run or qualification packet
  was produced; this report is an intake, not a verifier result.
