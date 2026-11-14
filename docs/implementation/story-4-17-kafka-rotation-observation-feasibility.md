# Story 4.17 — Kafka rotation observation feasibility

2026-11-14, Europe/Paris. Continues the owner-approved
[offline intake](story-4-17-next-candidate-intake.md).

**Decision: dynamic logging alone is insufficient. A complete supported correlation method
remains unproved; causal-verifier implementation is not ready.** This is a bounded source
assessment, not proof that every stock Kafka observation design is impossible.

Platform 4.17/EventStore 3.21 remain **in-progress**, five guarantees **REJECTED/open**,
technical **FAIL (20/24)**, selection **null**, production **FAIL**. Packet custody is
[complete](story-4-17-custody/receipt.json). No runtime, source/profile input or historical
assessment changed; no diagnostic ran.

## Inspection identities

Platform baseline: `8bd5e7330003c04895b0b86b0f5bb04fb4cf8957`.
EventStore: `51dd68b86a690ffe9e11c565dccec49b2d401a94`.
Kafka: `26b251a451ce941d3d7a55e6487bcb7f16b5ad48` (4.3.1).
These are assessment identities, never replacements for candidate-02 execution identities.
The EventStore [bounded investigation](../../references/Hexalith.EventStore/docs/implementation/story-3-21-kafka-bounded-investigation.md)
and [evidence contract](../../references/Hexalith.EventStore/docs/implementation/story-3-21-broker-remediation-evidence-contract.md)
remain governing. Read-only release APIs on November 14 still identify
[Dapr 1.19.1](https://github.com/dapr/dapr/releases/tag/v1.19.1) (October 31) and
[components 1.19.0](https://github.com/dapr/components-contrib/releases/tag/v1.19.0)
(October 22) as latest stable releases. This supplies no new RabbitMQ remedy or pin adoption.

## Publisher to authenticated connection: include the forwarder

[`Run.start_broker`](https://github.com/Hexalith/Hexalith.EventStore/blob/51dd68b86a690ffe9e11c565dccec49b2d401a94/tools/qualify-dapr-brokers.py#L410-L450)
routes Dapr through a loopback TCP forwarder and Docker's published endpoint before Kafka's
SSL listener. [`Forwarder.handle`](https://github.com/Hexalith/Hexalith.EventStore/blob/51dd68b86a690ffe9e11c565dccec49b2d401a94/tools/dapr_qualification_tcp_proxy.py#L16-L33)
owns paired sockets but emits no association receipt; it forwards encrypted bytes without
TLS termination or Kafka parsing. A publisher tuple is therefore not directly the broker's
tuple. Container port inspection alone does not measure per-connection translation.

A prospective change could retain metadata in this existing forwarder. It still needs
process-start/socket ownership, measured Docker forwarding on the exact worker, and the
broker-observed connection/principal match. No such capture is delivered or admitted.
PID/app ID, configured ports and expected certificate subjects alone are insufficient.
Retain public generation fingerprints; exclude private keys and secret values.
Loaded YAML hashes are insufficient too: [`Run.components`](https://github.com/Hexalith/Hexalith.EventStore/blob/51dd68b86a690ffe9e11c565dccec49b2d401a94/tools/qualify-dapr-brokers.py#L505-L548) copies OpenBao secret references,
while rotation changes the referenced secret without changing those references. Identical
resource bytes cannot show which resolved certificate a process loaded. Resolved-generation
evidence and the actual connection's authenticated-credential witness remain missing.

## HTTP ID to Produce: the decisive missing discriminator

[Completed request conversion](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/server/src/main/java/org/apache/kafka/network/RequestConvertToJson.java#L754-L799)
exposes header, connection, client/correlation IDs, principal, protocol and response. However,
[Produce conversion](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/server/src/main/java/org/apache/kafka/network/RequestConvertToJson.java#L528-L529)
disables record serialization. The [converter generator](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/generator/src/main/java/org/apache/kafka/message/JsonConverterGenerator.java#L384-L400)
retains record size instead of contents. This log path supplies no stale CloudEvent ID.

The producer records the HTTP ID/input and endpoint, but no mapping to Kafka correlation IDs.
Both publisher generations load `clientID=qualification`; it is not an operation or instance
witness. Length and timestamp proximity cannot distinguish retries, batching or other in-flight
publication. Readback identifies persisted effects, but a denied request may leave no record.

There is a concrete obstacle to assuming an empty request queue:
[`Run.run_broker`](https://github.com/Hexalith/Hexalith.EventStore/blob/51dd68b86a690ffe9e11c565dccec49b2d401a94/tools/qualify-dapr-brokers.py#L654-L680)
reuses a publisher across scenarios. The released
[component publish path](https://github.com/dapr/components-contrib/blob/1d331c389f11baaa1d664022b6b6c2f59811ce11/common/component/kafka/producer.go#L243-L325)
does not use its context parameter and logs topic plus eventual partition/offset, without
the required HTTP-ID/correlation bridge. **Inference:** an earlier HTTP timeout alone cannot
prove that publisher work has drained. A sequential harness call is not sufficient isolation.

A future design must supply a supported discriminator for the HTTP operation's actual Produce
request set, including retries, or prove a controlled association by construction. Neither is
demonstrated here. Changing topics/client IDs or treating temporal isolation as proved would
require a concrete reviewed design; this assessment makes none of those changes.

## Applied ACL/audit to request: a narrower source lead

[StandardAuthorizerData](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/metadata/src/main/java/org/apache/kafka/metadata/authorizer/StandardAuthorizerData.java#L184-L305)
traces ACL additions after local-cache assignment and audits principal, host, operation,
resource, request type, result and matching rule. Its audit line has no connection/correlation
ID; the application trace has no per-request metadata-offset witness.
There is a further distinction: [`StandardAuthorizer.loadSnapshot`](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/metadata/src/main/java/org/apache/kafka/metadata/authorizer/StandardAuthorizer.java#L115-L121)
calls `newData.addAcl` before publishing that cache as serving state. The same addition trace
can therefore describe snapshot construction. Reject such a trace as standalone proof that
the serving authorizer had applied the deny; distinguish cache publication and the actual
request's state.

[`KafkaApis.handle`](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/core/src/main/scala/kafka/server/KafkaApis.scala#L152-L171)
adds a useful TRACE entry with request description, connection, protocol and principal before
Produce dispatch; [authorization](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/core/src/main/scala/kafka/server/KafkaApis.scala#L430-L443)
precedes append handling. A handler-scoped audit association is a source lead to investigate.
It requires the additional logger `kafka.server.KafkaApis=TRACE`; the prior three-logger
proposal does not enable this entry. In the pinned default [handler routing](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/config/log4j2.yaml#L90-L122), its explicit
RequestAppender stanza is commented out: it inherits Root and writes to `server.log`/stdout.
Capture that output too, or explicitly admit a different routing. Request/authorizer files
alone would miss this witness. These are upstream defaults, not measured effective settings
of the qualification image; exact-image paths/layout/routing still need verification.

**Logger levels alone cannot deliver it.** The [default Log4j configuration](https://github.com/apache/kafka/blob/26b251a451ce941d3d7a55e6487bcb7f16b5ad48/config/log4j2.yaml#L15-L85)
omits thread identity and uses separate request/authorizer appenders. TRACE changes neither
the format nor cross-file ordering. A thread-bearing ordered-appender proposal needs exact
configuration admission, handler/callback sequencing analysis and ambiguity controls. Even
success here would not supply the missing HTTP discriminator.

## Conditional change and refusal map

These are dependencies for a future proposal, **not admitted implementation tasks**.

| Existing EventStore owner/file | Required evidence after feasibility | Refuse causal credit when |
| --- | --- | --- |
| `qualify-dapr-brokers.py`: process/sidecar/components; existing TCP proxy | Start identity, executable, endpoint, loaded resources plus resolved public generation, full socket path and authenticated credential | Wrong/reused PID, foreign endpoint/container/principal, unproved/wrong generation, YAML-only credential attribution or unmeasured translation |
| Rotation scenario and `Run.kafka` | Prior/effective/restored logger state, installed/applied ACL and actual request | Wrong target, pre-application or snapshot-construction ACL evidence, failed restoration, reused/ambiguous connection or request IDs |
| `Run.finish` and finalization | Complete targeted logs and rotated segments before deletion; paths/order/hashes/coverage/clock basis and private-material checks | Missing/truncated logs, gaps or unproved ordering; existing `docker logs --tail 150` is insufficient |
| `dapr_broker_qualification.py` and existing `tools/tests` | Independently validate every admitted receipt/join | False HTTP attribution, retries/concurrency ambiguity or contradictory claims; absent evidence stays unadjudicable |
| Profile/source/configuration admission | Exact separately admitted diagnostic subject and compatibility controls | Schema-1 tuning has no logger/process fields; existing profiles/input inventories and packet schemas are exact |

Do not refresh historical locks or reinterpret old packets to admit changed tools. Preserve
both brokers/all 24 vectors and separate capture/integrity/technical/cleanup/authority outcomes.
The live-session rule keeps attributable denial, controlled restart, new effect exactly 1,
zero stale effects within three seconds and the five-second stale HTTP timeout. The separate
**cold-old-generation** question requires its own exact process/generation and specified
broker-refusal or startup-failure control; it cannot replace live-session denial.

## Outcome

No complete supported join was identified. Resolve the HTTP/Produce discriminator, forwarding/
principal mapping and applied-ACL/audit association before a causal-verifier implementation.
A diagnostic may investigate unresolved observations and predict FAILs, but needs exact
accepted inputs, preservation/cleanup and its own execution decision. Historical causality
remains unadjudicable; future observations cannot fill absent historical evidence.

**Recommended next offline task:** assess one controlled HTTP/Produce association design by
construction. Inspect the pinned component/Sarama send, retry and cancellation paths alongside
the harness's publisher lifetime; write an invariant accounting for every possible in-flight
publication on the stale connection, including work surviving earlier HTTP timeouts. Keep the
same live old publisher and existing predicate. Stop after that source-path assessment: if
no supported invariant can exclude an unrelated Produce, or the design requires replacing the
live-session test with a fresh/cold publisher, retain the blocker. Do not implement it or run
another logging-only diagnostic. This is a proposed next task, not new implementation or
execution approval; any supported candidate still needs the other two joins and exact admission.

Prepared upstream Kafka/RabbitMQ questions remain unsent; explicit messaging authorization is
needed to send them. No new run, input adoption, broker selection or guarantee closure follows.
