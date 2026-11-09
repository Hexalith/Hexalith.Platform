# EventStore qualification bindings — unpublished v2 design

This is Platform-owned, resource-free contract preparation for
[PLAT-ES416-QH](../../_bmad-output/implementation-artifacts/spec-plat-es416-qh-qualification-handoff.md).
The user selected this tranche after accepting the
[Q1–Q6 planning proposal](../../_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-09.md).
It supplies a concrete design and synthetic review examples. It publishes no
schema and implements no reader, writer, manifest extension or runtime control.
Builds and EventStore have not accepted or adopted this design. Existing tools
continue to support only the published [v1 contract](test-environment-v1.md).

The field names below are draft vocabulary for owner review. They are not an
accepted serialization API. The reviewed Q2 requirements govern this design;
changing its illustrative spelling does not relax those requirements.

## Versions and selection

The proposed pair is `hexalith.test-environment.v2` and
`hexalith.test-result.v2`; new credential kinds use an explicitly adopted private
`hexalith.test-endpoint-credentials.v2` channel. The policy remains
`hexalith.platform.candidate-and-attachment-policy.v1`.

A future adopted runner must select the pair from an accepted suite/plan binding
before allocation and verify explicit consumer support. `--profile` continues
to select a declared suite, not a free-form backend. No new manifest field,
CLI option, endpoint or control credential kind is introduced by this document.
Unsupported consumers/profiles or mismatched versions refuse; there is no
latest-version selection, automatic upgrade or silent downgrade. Ordinary v1
suites retain their descriptor/result pair and private v1 channel.

Keep `HEXALITH_TEST_ENVIRONMENT_PATH` as descriptor transport. The existing
private transport remains separate; its new credential kinds, exact file
contract and consumer selection must be agreed with Builds before adoption.

## Proposed public binding

Qualification executions add required `bindings.qualification`. All existing
run, environment, policy, deadline, artifact, candidate, decision, no-reuse,
suite, attachment, terminal, retention and cleanup responsibilities remain in
their existing bindings. References below resolve against those bindings;
repeated identities must agree and supply no independent authority.

| Group | Draft content and required checks |
| --- | --- |
| `scope` | Exact run/owner/attempt/environment, synthetic tenant/data isolation, profile content identity and suite/configuration/required-test-set identities. Compare every repeated value with its existing owner binding. |
| `profile` | `purpose` (`candidate` or `canonical`), definition identity, supported-release reference, Platform/catalog/composition and executing-tool identities; expected module artifacts, SDK/runtime/embedded component, component type **and API version**, backend image/configuration, security/placement configuration, actor-contract/capability version and required capabilities. Expected inputs do not establish actual loaded values. |
| `hosts` | Unique `hostId`, owned `resourceId`, shared logical actor app/type, writer/reader roles and exact HTTP/gRPC endpoint references. Writer A and B require independent processes, sidecars and caches observed at runtime. A reader may reuse a restarted writer host. Plan host IDs and different URLs alone prove no independence. |
| `operations` | EventStore-supplied lawful operation and owning-actor logical read route IDs, run-owned tenant/domain/aggregate partition, semantic barrier IDs and required-test-set identity. No SQL, provider addressing, private actor key or arbitrary command field. |
| `controls` | Allowlisted operation ID/version and owned endpoint reference; exact scope, permitted host/partition/phase, finite deadline, positive maximum uses and reset/cleanup obligation. No shell, caller-selected executable/target, hosted target, backend principal or standing fault service. |
| `observations` | Actual loaded SDK/runtime/embedded component/backend, negotiated capabilities/verifier readiness, host process/sidecar/cache identities, placement transitions, semantic barriers, and control request/apply/release/reset events with UTC times, affected resources, stable reasons and classified diagnostic references. |

Keep the v1 strict UTF-8/duplicate-key rules, closed objects, content identities,
UTC formats, finite integer/duration rules and public document size/depth limits
(1 MiB/64 levels). Future schemas must close every new nested object too.
The inherited total size bound is not permission for unbounded controls: their
accepted list, phase, uses and lifetime must be finite and declared before
execution. A control cannot outlive the run's applicable deadline or access.

`{"state":"not-yet-populated"}` remains an explicit unavailable observation,
not an empty successful observation. In fragments below, its use for support,
receiver and operation inputs makes the example deliberately ineligible for
execution. An adopted runner must refuse missing required launch prerequisites
before allocation. Later unavailable runtime observations block their required
lanes and grant no qualification credit. Configured pins, labels, startup
success and transport failure never establish storage enforcement.

## Descriptor/result agreement

The result snapshots the descriptor's exact qualification scope and declared
profile/hosts/operations/controls. These are immutable for that attempt.
Observation producers may subsequently append actual observations, matching the
existing result rules; they cannot replace previous events, rewrite identity,
change a control's original expiry or relabel the first outcome.

Future consumers must reject contradictory timestamps/state transitions,
foreign or duplicate host/control IDs, unresolved resources/endpoints,
inconsistent scope and unsupported control phases. An `applied` event requires
an observed application of that exact control; startup failure is not applied.
Reset failure remains visible and does not transform an assertion result.
Unverified discovery or leftovers never equal complete cleanup.

## Private credential boundary

The private v2 design preserves resource/module/endpoint-name/exact-URI
binding and adds exact run/owner/attempt/environment, tenant/data isolation,
profile/suite binding, control scope and expiry. Future credential kinds must
be finite and accepted by both owners, with protocol-specific header/metadata
names and no redirects or target overrides. This draft chooses no new kind.

Preserve owned regular-file/private-permission checks, atomic creation after
successful owner preparation, bounded reading (private v1 is at most 64 KiB),
validation before client allocation/launch, exact-value native-report scanning,
and deletion with the owned workspace. Credential lifetime must fit its
control and run lifetime. Expiry/cancellation/withdrawal ends access even when
local failure resources remain retained. Missing, conflicting, foreign,
expired or over-scoped credentials refuse, without fallback.

Credential values never enter descriptor/result snapshots, TRX, logs or retained
diagnostics. Module tests get neither backend credentials nor erasure signing
keys. HMAC material stays in the separately qualified OpenBao/Dapr boundary;
value-free receiver references are not permission to distribute keys.

## Synthetic review fixtures

- [Public fragments](../../tests/contracts/fixtures/eventstore-qualification-v2/public-binding-fragments.json)
  contain a synthetic existing-binding context and separate descriptor/result
  qualification fragments. Their immutable portions match; only the observation
  timestamp advances. Required producers remain unavailable. No full descriptor,
  terminal result, runtime outcome, real identity or supported tuple is supplied.
- [Private metadata](../../tests/contracts/fixtures/eventstore-qualification-v2/private-credential-metadata.json)
  is a separate shape sketch tied to one control endpoint. It deliberately omits
  the credential value and an accepted credential kind. It is not a valid or
  usable private credential document and must never be copied to the runner's
  credential path.

All sample hashes, IDs, addresses and dates are synthetic. Endpoint references
resolve only within these examples. The fixtures are not a v2 schema, conformance
suite, consumer receipt, published sample contract or executable run plan.
Offline checks can establish syntax and reference consistency only.

| Review vector | Proposed refusal or blocked disposition |
| --- | --- |
| v2 descriptor paired with v1 result; v1-only consumer | Refuse selection; no downgrade or allocation. |
| Foreign run/attempt/environment/profile/suite or partition | Refuse the complete handoff/control, never repair its binding. |
| Duplicate host/control ID or unknown endpoint/resource | Refuse before module launch. |
| Shared writer process/sidecar/cache; unavailable placement | Block independence/failover proof; URLs alone cannot pass. |
| Missing required control, unsupported phase, zero uses or excessive lifetime | Refuse control admission; required lane remains non-passing. |
| Result changes immutable profile; contradictory control observations | Refuse evidence credit; retain first outcome and failed reset. |
| Missing loaded identity/capability/receiver or logical readback | Block the affected required lane, never pass or N/A. |
| Credential leakage, wrong URI/scope, missing value or expiry | Refuse private handoff/report retention; never infer a credential. |
| Failed/skipped/missing required TRX rows despite zero process exit | Refuse suite credit; inspect native results and reconcile exact counts. |

These are design-review vectors, not executed product refusals. Existing v1
mirror checks and this tranche's offline example checks grant no v2 support.

## Remaining delivery and acceptance

| Accepted work | Still required after this tranche |
| --- | --- |
| Q1 / U1 | Named executing maintainer; Builds accepting owner/work reference and EventStore consumer/assertion receipt; exact supported profile/tool/contracts, lawful routes/test set, prerequisites and concrete argv/target/window. Platform planning alone remains partial intake. |
| Q2 / U2 | Both owners accept the exact design; Platform publishes paired closed schemas, Builds ships byte-identical mirrors and adopted selection/readers/writers/private credentials/report checks, EventStore explicitly adopts. Exact tool acceptance and integration qualification stay independent. |
| Q3 / U2 | Content-bound tuple and actual loaded identities; independent actor hosts/sidecars/caches, placement, usable persistence and receiver readiness. |
| Q4 / U2–U4 | Bounded semantic after-read, placement/resume and post-commit acknowledgement controls, raw HTTP/gRPC/reachable aliases and fresh owning-actor readback; two separately authorized disposable falsifications with lawful controls. |
| Q5 / U2 | Pre-allocation/partial-start ownership census, bounded reset/access expiry, immutable first outcome, local retention, withdrawal-before-teardown, exact-owner CI cleanup/retry and peer preservation; qualified erasure receiver/custody before key delivery. |
| U3–U5 | Eligible supported released primitive, owner-approved run authority, mechanism proof and then complete canonical conformance with every prerequisite. |

The full [Q4 AC1–AC11 mapping](../../_bmad-output/planning-artifacts/sprint-change-proposal-2026-11-09.md#q4--supply-bounded-controls-and-execute-the-unchanged-logical-denominator)
remains required. Candidate mechanism proof uses its declared subset while
prerequisite-dependent AC8/AC9 rows stay pending in the full ledger. Missing,
unsupported, skipped or withdrawn lanes are never passed or N/A. Failed
falsification assertions remain expected failures for their exact adversaries.

The recorded SDK 1.19.0 / daprd 1.19.1 / embedded components 1.19.0 /
`state.postgresql` v1 finding remains negative; no new capability search or
alternative profile selection occurred. U1 remains partial, U2 pending and
U3–U5 blocked. G-APPEND/readiness remain FAIL/BLOCKED. The complete accepted
contract, including originally read revision, create-once identity, contiguous
ranges, coupled-state atomic rollback, Conflict/Unknown reconciliation and
bounded authorized resumable erasure, is unchanged. CAS adds no activation
fencing. This preparation supplies no live, candidate, tool or final qualification.
