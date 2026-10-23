---
title: 'Implement the semantic Dapr custody provider and surviving authority'
type: 'feature'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 543ea182bd83c813609c7df0491d1419b7f1f99e
context:
  - ../eventstore/docs/implementation/ext-protection-1-owner-acceptance.md
  - ../agents/_bmad-output/planning-artifacts/external-dependency-register.md
  - docs/implementation/ext-secrets-1-erasure-authority-decision-2026-10-20.md
  - docs/implementation/ext-secrets-1-erasure-authority-2026-10-20/OWNER-APPROVAL.md
  - _bmad-output/specs/spec-ext-secrets-1-interaction-custody/custody-binding-contract.md
  - _bmad-output/planning-artifacts/architecture/architecture-ext-protection-1-custody-2026-10-18/ARCHITECTURE-SPINE.md
  - docs/implementation/ext-secrets-1-f1-2026-10-20/f2-f3-prerequisites.md
---

<frozen-after-approval reason="user explicitly authorizes implementation and reversible preparation; physical provisioning/experiments need a concrete approval packet">

## Intent

Agents Story 5.4 lacks the I3 semantic `custody-lifecycle-v1` infrastructure component/provider and independently surviving current authority. Implement the first unfinished prerequisite after completed F0/F1 source/transport evidence, with reviewable executable source and preparation for separately authorized physical qualification. Implement a replaceable component-internal AWS reference provider; choosing this source implementation authorizes no AWS deployment.

## Boundaries & Constraints

All native dependencies stay inside the new infrastructure component. Preserve existing canonical identity/AAD/KEK bytes, full 1–1,000 target manifests, original receipts/signatures, allocation exclusions, hold/dispatch/protection-reservation ordering, whole-tenant preparation and one shared epoch cut. Preserve 1,000 registrations/10,000 lifecycle requests, 32 MiB pending limits and measured completion reserves; no automatic eviction. Original results and trust survive erased epochs and application restores. First-release authority replacement is unsupported. Missing authority, unverified inventory, uncertain native send or lost result fails closed.

Never promote disposable models, HMAC custody, restored tombstones, successful SDK calls or missing keys into physical evidence. No provisioning, real key creation/deletion, qualification/cohort, Agents consumers, acceptance amendment or Available declaration. Preserve unrelated dirty work and every journal. Keep accepted timing/date/host/export disposition and OQ-5.4-1 unchanged. Local verification proves source/component behavior only; physical gates remain explicit.

## I/O & Edge-Case Matrix

| Given | When | Then |
|---|---|---|
| Enrolled authority | Read or compare current head | Fresh strongly consistent identity-bound read; restored clone/table/profile rejected; no self-enrollment. |
| Concurrent holds/reservation | Reserve complete manifest | One revision comparison; earlier/unknown pin blocks; late post-dispatch pin never succeeds. |
| Native send admission | Restart, duplicate or lost acknowledgement | Permanent original/intents survive; SendCommitted is lookup-only, never automatic resend. |
| Complete inventory and survivors | Advance B1–B5 | Drain and retire alternate/tainted generations, verify survivor copies, adopt successor, schedule sole source cut, publish only independent final evidence. |
| Old app/checkpoint/key/receipt copies | Restore/reissue | Same surviving authority and exclusions govern; no revive or replacement original. |
| Complete retained original | Page/recover | Exact bytes/signature and complete ordered vector; no partial Consumed. |
| Missing installed trust/closure/qualification | Invoke key release/destruction | Closed result with no effect; neither synthetic success nor unqualified installed claims. |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Secrets.Runtime/CustodyBinding*.cs`: reuse closed wire validation, original owner digests, C0/C1 codecs, original paging and intent semantics. Wrap currently only contains a completed registration and invoker refuses key delivery. Complete the input/result exchange without persisting raw key responses or activating production DI.
- `src/Hexalith.Platform.Custody.Component/`: in-memory fixture retains plaintext/epoch bytes; do not reuse as a physical provider. `CustodyNonrollbackJournal` is in-process, not installed authority.
- `eng/ext-secrets-1/local-transport/` and F1 protocol directory: retained pinned Dapr 1.18.1 protocol/reflection/UDS and 128 MiB candidate transport. Preserve original evidence; do not rerun F0/F1 or represent patched transport as accepted production.
- New `infrastructure/Hexalith.Platform.Custody.Dapr/`: standalone executable component; application projects must not reference it or its native dependencies. Reuse existing internal contracts through narrowly scoped friend access if appropriate. Generated pinned protobuf, gRPC UDS/reflection, exact same-profile Init and complete frame bounds.
- New component-internal backend: pre-enrolled DynamoDB table identity and conditional current head, immutable S3 object versions/checkpoints/originals, nonexportable single-region AWS_KMS symmetric epochs. Provider-native resource IDs remain private. Immutable objects precede a single conditional head commit; manifests do not fit into table items or per-target transactions. Pin actual table ID/ARN and bucket owner/version; no create/restore/bootstrap on ordinary startup. Runtime roles cannot restore/recreate authority, delete originals, cancel deletion or create substitute epochs.
- `docs/implementation/ext-secrets-1-semantic-provider-2026-10-22/`: new evidence, exact approval preparation and delivery packet. Existing reports remain historical.

## Tasks & Acceptance

- [x] New infrastructure project and scoped Runtime contract extension — implement executable semantic Dapr host, closed authentication and key-exchange framing, durable current authority/originals/native intents, real backend primitives and B0–B5 lifecycle. Exact current operation and independently signed control/observer evidence are mandatory; a caller boolean is never proof.
- [x] New component verification — build and exercise persistence/CAS/loss/restore/substitution/hold/original tests; test doubles are explicitly source evidence only. No synthetic test can satisfy physical qualification.
- [x] New approval packet — prepare exact proposed resources, operations, cost ceiling, independent observer, retention/journals and cleanup with missing account/placement/trust identities explicit. Enforce original five-epoch/two-KEK/one-authority/two-archive/two-control cap for this reference profile and one schedule/two tenants. No operation is executed.
- [x] New delivery report — actual source/artifact/configuration/plan/trust hashes, executable commands, precise missing installations and conditional proposed dates; distinguish available bytes from null installed facts.
- [x] Independently review changes and repair material findings; retain verification failures and all original journals.

Acceptance: Given the implementation, when locally verified, then actual source/component capabilities and remaining installation/physical blockers are independently reviewable, with no unsupported physical claim or consumer activation. Given missing external approval, when preparation finishes, then only a concrete bounded resource/experiment packet is submitted for approval.

## Implementation Notes

The existing dirty work is explicitly preserved under the user's instruction. No intent gap blocks reversible source implementation; actual account/location/identities and expenditure require the final concrete provisioning approval. No source change is permission to run its native methods. If a primitive cannot safely implement an invariant, keep that capability closed and report the exact unresolved implementation gap rather than substituting a fixture.

Resume instruction (2026-10-23): finish executable key delivery, hold pin/unpin, atomic manifest destruction, exact-original recovery, independently surviving current authority, and transport/buffer closure. Supply EventStore with the authenticated installation/genesis/current-owner protocol and its concrete adapter handback. Protection-owner and governance-guard revisions are independent; absent or restored local state cannot authorize genesis. Read the complete relevant EXT-SECRETS-1, EXT-PROTECTION-1, EXT-HOST-1 v21/v23 and OQ-5.4-1 register contracts. Preserve every existing artifact and journal. Return reviewed implementation, meaningful component tests, exact remaining installation gaps, and a concrete bounded F2/F3 approval packet under OWNER-APPROVAL.md. Do not provision, run physical experiments or the cohort, execute consuming Agents seams, change acceptance/registers, or advance availability. The independent EventStore checkout is context; deliver the provider-neutral adapter/protocol handback here unless repository-local instructions explicitly authorize a scoped change there.

## Spec Change Log

## Review Triage Log

All three independent layers reviewed the custody-scoped since-baseline diff at `/tmp/semantic-custody-review-32307sa8/custody-scoped.diff` (SHA-256 `1b8ad409884a2aaca7caf8e2ef38a4851df82db5337d97266a8208e4a76d2ddb`). The complete since-baseline artifact is retained alongside it; unrelated concurrent changes are excluded from custody review. Every filed finding is retained separately below before grouping. Each confirmed correction concerns an existing invariant and adds no public application surface; route is patch. No frozen intent or pre-existing work is reverted.

| ID / layer | Verdict / route | Evidence |
| --- | --- | --- |
| B1 blind | high / patch | Program checks socket existence before worker dispatch; a serving socket therefore rejects every worker action. Restrict existing socket checks to serve. |
| B2 blind | high / patch | Profile accepts two opaque epochs for one native ARN/ID, so source deletion can also delete the successor. Require distinct native keys. |
| B3 blind | high / patch | Profile permits a KEK protector to overlap an epoch; deleting that epoch can strand survivor KEKs. Require disjoint native identities. |
| B4 blind | medium / patch | Empty/incomplete per-tenant epoch and KEK mappings pass profile validation, despite the required two-tenant lanes. Validate each lane before startup. |
| B5 blind | high / patch | Worker native intents use the permission signer's key ID as ProducerIncarnation. That does not identify the worker whose buffers need closure. Bind the authenticated executor incarnation and require it in inventory. |
| B6 blind | medium / patch | Unrestricted lease/attempt strings joined with a colon collide; duplicate delivery also lacks exact KeyUse comparison. Use the existing canonical tuple primitive and reject changed retained uses. |
| B7 blind | medium / patch | Archive encoding reuses payloads only within one commit and rewrites every registration on later commits, causing unnecessary retained growth. Reuse immutable references from the exact preceding archive. |
| B8 blind | high / patch | Commit extracts/writes payloads before capacity checks; an exhausted head can accumulate unselected objects. Preflight collection and bounded index capacity before any archive write. |
| B9 blind | medium / patch | Parsed heads are reserialized for a string equality condition, so valid externally formatted enrollment heads cannot advance. Preserve the exact read attribute for CAS. |
| B10 blind | medium / patch | Decode loses the decoded RootKey reference when validation returns false. Clear that owned allocation on rejection. |
| E1 edge | high / patch | Independently confirms B1: an online component's socket blocks worker execution. |
| E2 edge | high / patch | Independently confirms B2/B3: epoch/protector native aliasing can delete surviving material. |
| E3 edge | medium / patch | Independently confirms B6 with lease/attempt pairs (a:b,c) and (a,b:c). |
| E4 edge | medium / patch | A successor admission replaces Anchor.Admitted, making an earlier retained recorded transition unverifiable. Exact historical lookup must read its retained original without moving current authority. |
| V1 verification | medium / patch | Filed regression evidence: all crypto/lifecycle fixtures bypass real KMS adapters, so incompatible encryption contexts or substituted native responses would escape tests. Add native API-double coverage of those actual adapters. |
| V2 verification | medium / patch | Filed regression evidence: SDK fixture bypasses BindingService.Init/Invoke, so missing initialization would escape interoperability tests. Exercise the registered service boundary and refusal cases. |
| V3 verification | medium / patch | Filed regression evidence: existing late-pin test is also blocked by Frozen and cannot detect removal of the pre-custody dispatch check. Exercise signed invalid reservations before a custody cycle. |

Grouping for implementation: B1/E1; B2/B3/E2; B4; B5; B6/E3; B7; B8; B9; B10; E4; V1; V2; V3. No finding is deferred or silently discarded.

Post-repair verification: blind hunter confirmed all ten original findings resolved; edge-case hunter confirmed all four resolved; verification reviewer confirmed all three gaps resolved and the additional archive/native-store tests meaningful. These follow-ups were static inspections. The parent executed the final tests below. No concrete new defect was filed and nothing was deferred. The final lifecycle fixture additionally exercises two destruction targets, a surviving third root, ordered complete receipts and no partial reservation when the second target is pinned.

## Verification

Build/test the isolated infrastructure component with pinned dependencies and fresh external outputs. Retain exact commands and hashes. Run targeted existing Runtime wire/codec tests when changed. Review the scoped diff and audit every matrix row. No owner Live/Full command runs.

Final executed verification: both fresh builds passed with zero warnings/errors; 18/18 executable component scenarios and 103/103 targeted Runtime tests passed, with no failure/skip/unrun. Commands, retained logs, source/artifact/configuration/plan hashes and the per-row matrix audit are in the [final verification report](../../docs/implementation/ext-secrets-1-semantic-provider-2026-10-22/FINAL-VERIFICATION.md). Production source did not drift during verification; the intentional final two-target fixture extension was rebuilt and rerun successfully.

Completion is the bounded source/component delivery and approval preparation described by this spec. EventStore source integration, private issuers, installations and physical qualification remain explicitly blocked; no accepted target, provisioning/experiment permission, dependency availability or cohort execution is created by this status.
