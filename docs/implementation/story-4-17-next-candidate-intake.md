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

## Successor proposal — offline assessment continuation (2026-11-13)

**Offline scope and evidence criteria approved by the owner; exact implementation inputs and
execution remain conditional.** The published
reconciliation baselines were checked before editing: Platform local/remote main
`44756d1db8a38ac305cbb5247ae9cd52985b1d76` and its root-declared EventStore checkout
local/remote main `5f4db84502c7c54b316dd9429dccd12ae881e759`, both clean. The separate
EventStore sibling checkout has divergent history and concurrent tenant-boundary edits;
this continuation uses the baseline-matching checkout and leaves that sibling untouched.
These documentation identities do not replace the run identities above.

EventStore's proposed [remediation evidence contract](../../references/Hexalith.EventStore/docs/implementation/story-3-21-broker-remediation-evidence-contract.md)
owns the detailed source findings, minimum rotation observations and Given/When/Then acceptance
criteria. This is a local cross-repository proposal, with publication/adoption still outstanding.

### Workstreams and acceptance dependencies

These workstreams can advance independently. A supported-input diagnostic may address a bounded
rotation question while explicitly predicting unresolved pressure/RabbitMQ failures; delivery of
every remedy is required for qualification claims, not for proposing that diagnostic. Every run
still needs its own exact acceptance and the complete matrix. The numbered rows describe local
dependencies, not a requirement to finish all remediation before useful analysis can proceed.

| Order / owner | Concrete successor work | Proposed acceptance before moving on |
| --- | --- | --- |
| 1 — EventStore 3.21, Kafka pressure | Identify a supported Produce-pressure path yielding the unchanged exact Dapr quota rejection. Current byte-rate and request quotas throttle. A strict controller-mutation quota is administrative, not proof of the pressure publication. No such alternative was identified. | Given an exact supported tuple, when its source path is inspected, then the genuine Produce rejection and Dapr error mapping are identified. Without that mechanism, report unresolved feasibility and retain FAIL; no configuration-only retry is a remedy. |
| 2 — EventStore 3.21, pressure producer/verifier | Only after feasibility, specify fault application/removal receipts and independent verification. The current `validate_observations` also requires the literal `producer_byte_rate=1024` command; Platform's bounded schema has no replacement-fault field. | Given an accepted mechanism, when a future contract is reviewed, then producer and verifier bind that exact fault without changing `pressure_rejection`, the cancellation/readback bounds, retention, zero acknowledged loss or the full matrix. Implementation requires its own accepted scope; no code/profile changes occur here. |
| 3 — EventStore 3.21, Kafka rotation | Audit historical failures and candidate-02 using a joined publisher/process/generation → actual broker principal → installed and applied ACL → stale request → persisted readback record. Keep the new-generation positive control and unproved cold-generation question. | Given the original captures, when a cause is asserted, then the necessary joined observations support it. Missing principal/applied-state evidence yields an explicit unadjudicable historical cause. A future instrumented capture tests hypotheses; it cannot fill absent historical observations. The single verified PASS grants no closure. |
| 4 — EventStore/upstream, RabbitMQ | Obtain NACK propagation through retries/cancellation, revoked-credential/reconnect error propagation, and mandatory publishing with correlated returns completed before success. | Given the real Dapr publish path, when each existing fault is applied, then its unchanged attributable-error/effect predicate passes with bounded cancellation and persisted readback. A lower-level fix or native client test alone is insufficient. |
| 5 — EventStore, Builds, then Platform | Identify a supported official runtime embedding the required fixes, independently admit exact locks, and obtain Builds catalog/CLI/runner acceptance. Then prepare exact Platform source/configuration/worker identities, predictions and verifier calls. | Given accepted supported inputs and a separately accepted diagnostic question, when the execution proposal is reviewed, then all 24 vectors and separate capture/integrity/technical/cleanup/authority outcomes remain. A later exact execution decision is required. No attempt is dispatched by this proposal. |

Current upstream delivery was rechecked read-only on November 13. Dapr v1.19.1 resolves to
`cfb5ee090dda7cfc26145c7703605d831350aa2a`; its
[go.mod](https://github.com/dapr/dapr/blob/cfb5ee090dda7cfc26145c7703605d831350aa2a/go.mod)
embeds components v1.19.0, `1d331c389f11baaa1d664022b6b6c2f59811ce11`.
The latest runtime/component releases still predate the NACK backport. The exact main and
release-1.19 heads and remaining publish-path defects listed above were reconfirmed; the
EventStore contract links every inspected source. **No supported complete remedy is available
from those checked releases.** Source defects are demonstrated; proposed error propagation and
return handling are required delivery, not demonstrated end-to-end remedies. The particular
path causing the historical RabbitMQ 599 remains a hypothesis.

### Packet custody: decision and completion before expiry

The read-only [artifact listing for run 43386723091](https://api.github.com/repos/Hexalith/Hexalith.Platform/actions/runs/43386723091/artifacts)
still reports artifact `14018214306` unexpired, 1,650,434 bytes, digest
`a26b848840ca8061a00d8ea7598b52b6a91b5331822645c23bc527278e40949a`, with expiry
**2026-12-13T07:40:43Z**. The retained assessment records 544 entries and `bytesCommitted=false`.
A temporary verified download and retained assessment links do not establish durable custody.

**Required Platform owner decision:** name the accepted durable destination/failure domain,
retention period, access/deletion rules, transfer operator and independent verifier. Decide
preservation of the two historical Kafka failure packets as well as candidate-02; retain
candidate-01 only with its diagnostic-only label if it remains part of the comparison. Their
identities/digests are in the recovered-artifact table above. Read-only artifact APIs confirmed
the following separate deadlines; **the earliest preservation deadline is December 8**:

| Archive / evidence authority retained | Artifact | Recorded expiry (UTC) |
| --- | --- | --- |
| [Historical standalone failure](https://api.github.com/repos/Hexalith/Hexalith.EventStore/actions/runs/42532773702/artifacts) | `13638347236` | `2026-12-08T23:05:01Z` |
| [Historical fixture failure](https://api.github.com/repos/Hexalith/Hexalith.EventStore/actions/runs/42532776864/artifacts) | `13638293057` | `2026-12-08T23:08:16Z` |
| [Candidate-01, diagnostic-only](https://api.github.com/repos/Hexalith/Hexalith.Platform/actions/runs/43333995506/artifacts) | `13995674927` | `2026-12-13T00:11:32Z` |
| Candidate-02, independently verified packet with technical FAIL | `14018214306` | `2026-12-13T07:40:43Z` |

All four were reported unexpired with the retained digests. Candidate-02's deadline does not
extend the others. No destination or transfer is authorized here.

**Proposed custody acceptance:** Given that decision, when the separately authorized operator
preserves each selected archive, then retain its exact original sanitized ZIP without repacking,
API identity/run/source metadata, own archive digest, complete per-entry inventory, manifest/schema
identity and original evidence-authority label. Independently retrieve each archive from the chosen
destination, verify its own digest and every entry against its complete custody inventory, also
validate its packet manifest using its original schema, inspect extraction safety/private-material
exclusion, and record any original validation failure without upgrading evidence authority.
Candidate-02 specifically has 544 archive entries and 520 manifest artifacts; those counts do not
apply to other packets. Record a durable custody receipt per archive with destination, retention,
operator/verifier and completion time **before that archive's expiry**. Retain original commands,
loaded configuration, publisher/ACL diagnostics and persisted
readback needed for adjudication, plus original verifier/configuration identities. A reduced
summary is not the full packet. No custody receipt or transfer result is claimed by this assessment.

### Scope submitted for owner decision

1. **EventStore:** accept pressure feasibility investigation and the conditional producer/evidence
   contract scope, retaining FAIL if no supported mechanism meets the current predicate.
2. **EventStore / Platform:** accept the rotation minimum-evidence contract and decide whether
   historical causality is unadjudicable; approve exact supported observation/cold-generation
   scope before implementation. No historical principal or applied-state observation is invented.
3. **EventStore / upstream / Builds:** accept the three RabbitMQ delivery requirements and
   end-to-end criteria; identify an official supported tuple before any pin-adoption proposal.
4. **Platform custody owner:** choose custody terms, operator and verifier, then authorize and
   complete independently verified preservation before each recorded expiry, starting
   **2026-12-08T23:05:01Z** for the relied-on historical packets and no later than
   **2026-12-13T07:40:43Z** for candidate-02.

After those decisions, any exact execution proposal still requires separate acceptance. Both
parent statuses remain **in-progress**, all five historical guarantees **REJECTED/open**,
technical qualification **FAIL (20/24)**, selection **null** and production eligibility **FAIL**.
AD-26, profile inventory and staging/shared-host/restore prerequisites remain unchanged.

### Owner approval and active next steps — 2026-11-13

The owner's subsequent **“approve”** accepts this offline proposal: pressure-feasibility
investigation and conditional producer/evidence-contract scope, the minimum rotation evidence
criteria, the three RabbitMQ delivery requirements and the per-archive custody acceptance
criteria. This is the owner's acceptance of the requirements, not an upstream delivery receipt,
Builds catalog adoption, guarantee closure or approval of an unspecified execution.

Proceed with the independent offline workstreams under the existing predicates. Pressure
implementation remains conditional on an identified supported rejecting mechanism. Rotation
instrumentation must still establish a supported request/principal/applied-state join and exact
capture/admission changes. The approved treatment of missing historical observations is to
retain an unadjudicable cause and the open guarantee; later evidence cannot manufacture those
observations. No available remedy or qualified broker is inferred from approval.

An additional read-only recheck matched the recorded ZIP digests of both historical failure
packets and candidate-02. Searches of retained non-source JSON/log/text outputs found none of
the stock principal/request audit markers needed for that join; the EventStore contract records
the bounded search and its limits. No new qualification credit was produced.

**Still required for custody:** the actual destination/failure domain, retention/access/deletion
terms, transfer operator and independent verifier. Story 4.13 remains backlog and supplies no
accepted destination for these packets. Approval accepts preservation criteria but supplies none
of those missing values; the information request is pending. No transfer has started.

**Still required for execution:** an exact source/lock/configuration/worker/tool proposal and
its separate execution decision. No run, runtime-pin change, broker selection, commit or push
is authorized by this recorded offline acceptance. Both parent stories and all qualification
dispositions above remain unchanged.

## Applied recommendations — 2026-11-14

The owner's **“apply recommended”** direction is applied through the following concrete offline
deliverables. Existing qualification/runtime inputs and both main revisions remain unchanged.

| Recommendation | Completed action | Remaining dependency |
| --- | --- | --- |
| Preserve packets first, using an existing archive | [Preservation preparation](story-4-17-packet-custody-preparation-2026-11-14.md): all four untouched ZIPs staged locally with one complete inventory/manifest; independent verification passed for 2,215 entries and 2,059 packet artifacts. | No accepted off-site archive/allocation/access was identified in the inspected handoffs. Destination/access and real operator/verifier bindings are needed for transfer. Local staging uses hard links to the original cached files after disk-space recovery; it is not independent custody. |
| Bound Kafka pressure investigation | [Kafka work package](../../references/Hexalith.EventStore/docs/implementation/story-3-21-kafka-bounded-investigation.md): bounded source review complete, precise upstream question prepared, and stop condition applied. | No supported Produce-pressure rejection path was identified. Do not implement a replacement fault or tune the current quota without that path; unchanged predicate remains FAIL. |
| Prepare minimal Kafka rotation observations | The same package identifies supported dynamic broker logger controls, exact source capabilities, targeted full-log retention and prospective producer/verifier/admission changes. | Process/socket ownership, stale-ID/request correlation and audit/request association are still unresolved. Logging is a supported candidate input, not demonstrated causality or an implementation-ready remedy. |
| Prepare two RabbitMQ upstream packages | [Local issue-ready drafts](../../references/Hexalith.EventStore/docs/implementation/story-3-21-rabbitmq-upstream-work-packages.md): A combines pressure/rotation publish-error propagation; B covers optional mandatory publishing and correlated returns. Existing issue #4697's closed-but-unresolved mandatory half is referenced. | No issue posted, upstream acceptance or supported release delivery. EventStore/Builds alignment and Platform exact full-matrix execution remain later steps. |

The retained-packet hold is now concrete: keep the originals while the guarantees remain open,
then review deletion against applicable retention/backup/rollback requirements; no automatic
expiry is introduced. Prefer an already approved off-site archive over provisioning a new service.
The search result is limited to inspected handoffs, not a claim that no archive exists anywhere.

A future full-matrix diagnostic must answer a specific observation question with explicit expected
remaining failures, exact supported inputs and separate execution acceptance. No retry was run.
Both parent statuses remain **in-progress**, all five historical guarantees **REJECTED/open**,
technical qualification **FAIL (20/24)**, broker selection **null**, production eligibility **FAIL**.

## Packet preservation completed — 2026-11-14

The owner subsequently admitted all four exact sanitized ZIPs and the unchanged inventory
manifest to the existing Scaleway archive. The [custody receipt](story-4-17-custody/receipt.json)
accepts all five exact versions under `hexalith-recovery-points`, `fr-par`, prefix
`evidence/4-17/20261114t010116z-5574db84/`, completed at
**2026-11-14T01:14:47.503827Z**, before every respective GitHub expiry. The
[completed preservation record](story-4-17-packet-custody-preparation-2026-11-14.md#off-site-custody-completed--2026-11-14)
retains object versions, protection and the links to the inspected plan, current preflight,
conditional upload intents/results and separate validator-only download evidence.

The independent automated readback matched all five digests, **2,215 entry inventories**,
**2,059 packet artifact hashes**, original schemas, safety and bounded private-material
exclusions. All original identities and authority labels remain, including candidate-01's
diagnostic-only/original integrity rejection. This is separate agent execution on a shared
host; it supplies no independent human accountability or security boundary. Existing AES256,
thirty-day COMPLIANCE minimum and no-expiry `evidence/` lifecycle were verified. The
Administrator owns the no-deletion hold until guarantees close, followed by policy review.
Original cache/staging bytes and the manifest's historical local-only/null fields are unchanged.

**The destination decision and packet-preservation dependency above are now resolved.**
Kafka pressure feasibility, historical rotation adjudication/instrumentation, RabbitMQ delivery,
EventStore/Builds alignment, exact future execution acceptance, staging and Subscription
prerequisites remain open. No new qualification was dispatched. Platform 4.17 and EventStore
3.21 remain **in-progress**; five guarantees **REJECTED/open**, technical **FAIL (20/24)**,
selection **null**, production **FAIL**.

The [post-review record](story-4-17-custody/post-review-verification.md) documents local tooling
corrections, the historical transport-count limit, a passing manifest scan and current ACL
privacy established by stable-inventory inference. [Final local checks](story-4-17-custody/post-review-checks.json)
pass 49 controls while preserving original transfer/verification evidence. No transfer was
replayed and no qualification outcome changed.

## Rotation observation feasibility — 2026-11-14

The next approved offline investigation is recorded in the
[rotation feasibility decision](story-4-17-kafka-rotation-observation-feasibility.md).
The existing TCP forwarder emits no socket-pair association; Kafka's default Log4j pattern
omits thread identity while request/authorizer records use separate appenders. A handler
TRACE entry is a useful source lead, but levels alone do not establish an audit/request join.
The stock Produce request-log path still omits the stale HTTP message ID.

**No complete supported correlation method is demonstrated; instrumentation is not ready.**
The report maps conditional producer/forwarder/verifier/admission changes and refusal controls,
preserving the separate cold-generation question. No causal verifier, source/profile change,
upstream message or diagnostic run was implemented or authorized. Packet custody stays complete.
Both parents remain **in-progress**, five guarantees **REJECTED/open**, technical **FAIL (20/24)**,
selection **null**, production eligibility **FAIL**.

## Controlled publication association — 2026-11-14

The next bounded source-only task is complete in the
[controlled-association decision](story-4-17-kafka-controlled-association.md).
Dapr's outbound timeout can return without joining the component invocation; the pinned Kafka
component ignores that context. Sarama's synchronous result establishes completion for one
message, while its exposed drain path closes the producer. No nonclosing witness covering all
earlier Dapr invocations and their Produce attempts was established for the same live publisher.
A later HTTP success, a sentinel, readback or a calculated wait therefore cannot establish the
empty-other-work invariant. These are source conclusions, not a demonstrated historical cause.

**The source-path stop criterion applies: no causal instrumentation or logging-only retry is
ready from this construction.** Reopening needs a concrete supported completion/discrimination
witness, or a separately accepted alternative correlation design. Forwarding/principal,
serving-ACL/audit and cold-old-generation evidence remain separate dependencies; Kafka pressure
and RabbitMQ delivery remain open. No new implementation, candidate, run or upstream message
was produced. Packet custody remains complete; Platform 4.17/EventStore 3.21 remain
**in-progress**, five guarantees **REJECTED/open**, technical **FAIL (20/24)**, selection
**null**, production eligibility **FAIL**.
