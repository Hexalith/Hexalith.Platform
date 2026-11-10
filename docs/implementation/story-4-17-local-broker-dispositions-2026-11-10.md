# Story 4.17 local/CI durable-broker guarantee dispositions

This is Platform's record, made on 2026-11-10, of the five durable-broker guarantees that failed EventStore's local/CI qualification. EventStore's 2026-11-09 course correction (`sprint-change-proposal-2026-11-09.md` P2.F–H, P5.H) gave Platform concrete broker/component configuration and local/CI qualification execution. EventStore Story 3.21 supplies the requirements and test vectors and evaluates Platform's results.

The owner directed this local/CI slice of Story 4.17 on 2026-11-10. Platform `epics.md` Story 4.17 does not yet carry this handoff as an acceptance criterion. That planning note is tracked in Platform `deferred-work.md`.

The record is not a qualification packet. It selects no broker, ran nothing and converts no failure into a pass. It makes no staging, production-profile or Kubernetes claim.

## Candidate and results

| Item | Value |
| --- | --- |
| Dapr runtime / CLI | 1.19.1 / 1.19.1, with SDK 1.19.0 |
| Embedded components-contrib | v1.19.0, commit `1d331c389f11baaa1d664022b6b6c2f59811ce11` |
| Brokers | RabbitMQ `4.3.6-management`, Kafka `4.3.1`, both digest-pinned in EventStore `deploy/dapr/qualification/runtime-lock-1.19.1.json` |
| Hosted runs | [42532773702](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/42532773702) (standalone broker matrix) and [42532776864](https://github.com/Hexalith/Hexalith.EventStore/actions/runs/42532776864) (runtime workflow with the C# broker fixture), both on EventStore `0a6725d51bb17a9eae28a24a53f3636bab9d48d7` |
| Denominator | 24 vectors: 12 scenarios for each broker. 19 PASS, 5 FAIL (Kafka 10/12, RabbitMQ 9/12) in both runs. The 19 passes are EventStore's recorded results; this record does not re-evaluate them. |

## Why no configuration candidate was executed

For runtime 1.19.1, EventStore's `validate_profile` (`tools/dapr_broker_qualification.py:163-167` at the run commit) rejects any profile whose `inputs` differ from EventStore's checked-in `runtime-lock-1.19.1.json`. Those 75 inputs pin by SHA-256 the following, among others:

- the component files `deploy/dapr/qualification/{rabbitmq,kafka,subscriptions}.yaml`;
- the harness `tools/qualify-dapr-brokers.py`;
- the validator itself;
- the C# fixture;
- the workflows.

The harness holds the Kafka server properties (line 444) and every fault and readback bound. It also applies the RabbitMQ pressure policy (lines 406-408), whose definition `{"overflow": "reject-publish", "max-length": 4}` is at `tools/dapr_broker_qualification.py:21-24`; `rabbitmq.yaml` also sets `maxLen: "4"`.

Changing any component or broker setting therefore means editing EventStore, which is out of scope for this slice. A Platform-owned copy of the harness would be a new proof model, and the owner declined one on 2026-11-10. The dispositions below rest only on existing hosted observations, the pinned upstream source, the scenario pass rules and the public upstream status.

## Disposition vocabulary

- **REJECTED:** the guarantee fails on this candidate tuple, and this slice shows no configuration change that meets it. The guarantee stays a FAIL in the denominator. This is not a broker selection or rejection.
- **Code-level:** the failing path is fixed in the pinned component source, so no component metadata or broker setting reaches it.
- **Candidate inapplicable:** the proposed configuration change does not act on the failing mechanism.
- **Observed only:** no configuration candidate was evaluated. The disposition rests on the hosted observation, and the listed candidates are untested.

## Pass rules

These rules are summarized from `tools/dapr_broker_qualification.py` at the run commit: lines 52-71 (`attributable_denial`), 74 onward (`pressure_rejection`) and 228-233. The harness computes rotation denial at `tools/qualify-dapr-brokers.py:758`.

- **`secret-rotation`** requires all three of:
  - `oldGenerationDenied`: an HTTP 400–598 response whose Dapr `ErrorInfo` (`DAPR_PUBSUB_PUBLISH_MESSAGE`) carries the broker's own authorization error, with no stale effect. That error is RabbitMQ `Exception (403)…ACCESS_REFUSED` or Kafka `kafka server: The client is not authorized to access this topic`.
  - `newGenerationEffects == 1`.
  - `controlledRestart`.
- **`cancellation-backpressure`** requires:
  - `cancelled` (599 against a paused broker);
  - `boundedSeconds <= 10`;
  - `negativeConfirmRejected` (an attributable pressure rejection);
  - `acknowledgedLoss == 0`.
- **`missing-bindings`** requires `400 <= unboundPublishStatus < 599`, `allowedEffects == 1` and `bindingAbsent`.

## Dispositions

Each observation was reproduced in both hosted runs. EventStore records them in the "Approved hosted diagnostic reruns — 2026-11-09" section of `_bmad-output/implementation-artifacts/spec-3-21-latest-stable-dapr-runtime-refresh.md`.

| Broker / vector | Observation | Basis | Disposition |
| --- | --- | --- | --- |
| RabbitMQ `secret-rotation` | The new credential generation works. After `rabbitmqctl delete_user qualification` (harness line 737), the stale publisher's request ends with the harness's synthetic 599 timeout, with no effect within the 3 s bound (line 755). That is an unknown outcome, not an attributable denial. Cold old-generation restart remains unproved. | Observed only. Untested candidates: the component's `reconnectWaitSeconds`, or a Dapr resiliency outbound timeout, if either could surface `ACCESS_REFUSED` as an attributable error within the 3 s window. | **REJECTED** |
| RabbitMQ `cancellation-backpressure` | The queue's effective reject-publish/max-length-4 policy was measured. All eight publishes return 204, and three acknowledged IDs are absent at the readback bound. Native Dapr logs show three failed publisher confirmations. Canceled requests later persist once. EventStore treats this as failing the bounded guarantee, not as universal proof of permanent loss. | Code-level. The pinned `publishSync` logs a failed confirmation and returns `nil`, so the broker's NACK becomes HTTP 204 (`components-pubsub-rabbitmq-rabbitmq.go:290-299`, sha256 `0ca12e7d…`, equal to upstream tag v1.19.0). | **REJECTED** |
| RabbitMQ `missing-bindings` | Binding absence is physically measured before and after. The unbound publish returns 204 beside a passing bound control. | Code-level. `mandatory` is hardcoded `false` (`rabbitmq.go:282`) and is still `false` upstream on `main` and `release-1.19`. The proposed alternate exchange would route the message to its queue, and the confirmed publish would still return 204, so the rule still fails (candidate inapplicable). With `mandatory=true`, an unroutable message is reported by `basic.return`, not by a NACK, so a fix also needs return handling in the component. | **REJECTED** |
| Kafka `secret-rotation` | After the deny ACL (`--deny-principal User:CN=client --operation All` per topic, harness line 742), the old session returns 204 and its stale ID persists once. EventStore records principal/session and cold old-generation evidence as still outstanding. | Candidate inapplicable. `connections.max.reauth.ms` is SASL re-authentication (KIP-368), which "has no impact on non-SASL connections (e.g. … PLAINTEXT or SSL)". The vector authenticates with mTLS (`kafka.yaml:17`) over an `SSL` listener (harness line 444). Moving to SASL_SSL would change the vector's identity model: under KIP-684 such a listener can still require client certificates, but the authorization principal comes from SASL. The `StandardAuthorizer` also evaluates ACLs on every request, so re-authentication is not how a deny ACL takes effect. The persistence is undiagnosed; ACL propagation timing and the session's effective principal are the open questions. | **REJECTED** |
| Kafka `cancellation-backpressure` | Under the `producer_byte_rate=1024` client quota (harness line 779), two acknowledged IDs are absent at the 10 s readback bound (line 797) in each run. Several failed HTTP requests and the canceled request persist later. `acknowledgedLoss` counts only IDs absent at the bound, and later persistence of those two IDs is not established. Exact ID-to-offset causality is unproved. | Observed only. The fault and the bound are defined by the pinned harness. Untested candidates: the consumer's `initialOffset` and `consumeRetryInterval`, and the consumer-group rejoin time relative to the 10 s bound. | **REJECTED** |

All five guarantees remain failures in the 24-vector denominator. The selected broker stays `null`, and production eligibility stays FAIL.

## Upstream status, checked 2026-11-10

- components-contrib [#4698](https://github.com/dapr/components-contrib/commit/78808be217cc4aea768acbedd0161ab9db04a0b4) (`78808be217cc4aea768acbedd0161ab9db04a0b4`, 2026-10-30), "pubsub.rabbitmq: return error when broker NACKs a confirmed publish", is on `main`.
- Its backport [#4748](https://github.com/dapr/components-contrib/commit/ac89e0966ca5d9ff4dce4ae2ef0045c33e2660ee) (`ac89e0966ca5d9ff4dce4ae2ef0045c33e2660ee`, 2026-11-06) is on `release-1.19`.
- No components-contrib release contains either one yet. The latest is v1.19.0 (2026-10-22), and Dapr v1.19.1 (2026-10-31) embeds it.
- Using an unreleased build would be a private Dapr build, which this slice excludes.
- No upstream change to `mandatory` was found on `main` or `release-1.19`.

## Follow-up routes (not started)

| Route | Needed for | Owner |
| --- | --- | --- |
| A harness input that accepts Platform-owned component/broker configuration while keeping all 24 vectors; Platform then executes the untested candidates | Diagnosing RabbitMQ rotation, Kafka rotation and Kafka pressure | EventStore (Story 3.21 harness), then Platform |
| Requalification on a released Dapr runtime that embeds #4748 | Re-testing RabbitMQ pressure | EventStore runtime lock, then the Builds catalog/runner pins |
| An upstream component change for `mandatory` and return handling | RabbitMQ missing binding | Upstream Dapr |
| Broker selection or owner exceptions | Any broker choice | Owner, through AD-26 ratification |

EventStore's `deferred-work.md` entry "Deferred from: owner closure of spec-3-21-latest-stable-dapr-runtime-refresh.md (2026-11-09)" still lists Kafka `connections.max.reauth.ms` and a RabbitMQ alternate exchange as unverified mitigations. This record finds each inapplicable to its vector, for EventStore 3.21 to evaluate.

## Out of scope and not claimed

This slice does not cover staging acceptance criteria (they wait for Platform 4.14–4.16), broker selection, `production-profile.yaml`, Builds catalog/CLI/runner pins, EventStore edits, Redis pub/sub, private Dapr builds or provider exceptions. No Docker, broker, CI or cluster execution was performed.

## Sources

- EventStore at `0a6725d51bb17a9eae28a24a53f3636bab9d48d7`: `tools/dapr_broker_qualification.py`, `tools/qualify-dapr-brokers.py`, `deploy/dapr/qualification/{kafka,rabbitmq}.yaml`, `deploy/dapr/qualification/public-identities/1.19.1/components-pubsub-rabbitmq-rabbitmq.go`. Between that commit and EventStore `b2cab541ee3227e1e7a1600cb1d3da83f407dbf0`, the only cited-file changes are two release-metadata digests in the runtime lock (`aa16227bd`).
- EventStore observations: `_bmad-output/implementation-artifacts/spec-3-21-latest-stable-dapr-runtime-refresh.md`, sections "Approved hosted diagnostic reruns — 2026-11-09" and "Owner closure of the refresh slice — 2026-11-09".
- [KIP-368: Allow SASL Connections to Periodically Re-Authenticate](https://cwiki.apache.org/confluence/display/KAFKA/KIP-368:+Allow+SASL+Connections+to+Periodically+Re-Authenticate)
- [KIP-684: Support mutual TLS authentication on SASL_SSL listeners](https://cwiki.apache.org/confluence/spaces/KAFKA/pages/165224608/KIP-684+-+Support+mutual+TLS+authentication+on+SASL_SSL+listeners)
- [components-contrib releases](https://github.com/dapr/components-contrib/releases) and [Dapr releases](https://github.com/dapr/dapr/releases)
