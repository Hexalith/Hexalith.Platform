# Sprint change proposal for Kafka-only broker qualification

26 November 2026 · Owner approved Kafka-only qualification scope

## Issue and impact

Platform 4.17 and EventStore 3.21 previously required both brokers and all 24
vectors for the next hosted qualification. Candidate-02 passed 20, with RabbitMQ
backpressure, credential rotation and missing bindings and Kafka backpressure
failing. RabbitMQ depends on supported Dapr delivery of two upstream fixes. The
owner approved excluding RabbitMQ from the next candidate until it qualifies
separately. [Candidate-02 result](../../docs/implementation/story-4-17-broker-candidate-02-hosted-attempt-1.md) ·
[RabbitMQ deferred work DW-28](../implementation-artifacts/deferred-work.md#dw-28-qualify-the-rabbitmq-fixes-in-a-supported-dapr-release)

The change narrows the next evidence subject to Kafka. Platform 4.9 and 4.22
still consume an exact qualified inventory. EventStore 3.17, 3.19 and 5.7 still
consume the applicable qualified and owner-ratified inputs. The PRDs' delivery,
isolation and recovery requirements and the AD-26 ratification subject continue
to govern. There is no UX change and no epic resequencing.

## Approved changes

| Surface | Previous scope | Approved scope |
| --- | --- | --- |
| Platform 4.17 and EventStore 3.21 | Both brokers and all 24 vectors for the successor | A distinct Kafka-only successor must pass all 12 Kafka vectors on the exact supported profile. Retain original Kafka predicates, preservation, independent verification and cleanup controls. |
| RabbitMQ | Supported release delivery required before the successor attempt | Defer under DW-28. Exclude RabbitMQ from this candidate, its configuration, hosted attempt and eligibility claim. Re-entry requires separate qualification. |
| Future AD-26 evidence package | The retained, hash-bound preparation package describes the earlier 24-vector plan | Use a Kafka-only technical pass, the existing staging and recovery evidence and the authenticated Architecture and Platform deployment decisions on the exact subject. Retain the earlier package as a record of its original scope. |

Candidate-02 keeps its 24-vector denominator, 20/24 result and technical FAIL.
Its integrity and credited cleanup stand. Broker selection is null and production
eligibility is FAIL. Qualification scope approval leaves execution approval and
AD-26 ratification pending.

## Implementation handoff

EventStore 3.21 owns the supported Kafka backpressure rejection and the stale
publisher attribution witness. The current byte-rate fault cannot produce the
required Produce error 89. Rotation requires completion of earlier work on the
same publisher or a per-request discriminator, with authenticated principal and
effective deny ACL bound to the stale request. Both use the existing Kafka
predicates. [Kafka evidence requirements](../../docs/implementation/story-4-17-next-candidate-intake.md#kafka-only-successor--2026-11-26)

Once those inputs can be specified, EventStore owns a new, versioned Kafka-only
profile with an independently enforced `kafka` broker set and exact 12-vector
inventory. The installer, producer and verifier must agree on that profile and
its locked tools, source, configuration and component inputs. Prove that missing
Kafka vectors and extra brokers fail verification, and that historical profiles
and packets still use their original rules. Platform owns the immutable dispatch
inputs, allocation and evidence custody and seeks approval for one hosted
attempt after the exact profile is admitted. This handoff is moderate in scope
and removes upstream RabbitMQ timing from the Kafka work.

## Approval and application

The owner answered **yes** to Kafka-only qualification with RabbitMQ excluded
until separately qualified. The Platform and EventStore planning and intake
records apply that decision. Both implementation stories remain in progress.
The existing 1.19.2 comparison profile remains preparation evidence. A runnable
Kafka-only profile still depends on concrete Kafka evidence inputs.
