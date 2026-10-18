# Independent rubric and source-input reconciliation

Reviewed: 2026-10-18. Reviewer scope: the architecture gate's good-spine rubric and separate reconciliation of both load-bearing source inputs against the decision design and architecture spine. No deliverable was edited by this reviewer.

Artifacts reviewed:

- `docs/implementation/ext-protection-1-custody-design-2026-10-18.md`
- `_bmad-output/planning-artifacts/architecture/architecture-ext-protection-1-custody-2026-10-18/ARCHITECTURE-SPINE.md`
- Input A: `docs/implementation/ext-secrets-1-dek-provider-decision-2026-10-18.md`
- Input B: `../eventstore/docs/implementation/ext-protection-1-owner-acceptance.md`
- Supporting sources: actual Platform lifecycle/wrap/authority contracts; EventStore manifest, key, binding and consumption-owner sources; parent Platform architecture AD-6 through AD-12 and its dependency/operating envelope.

## Verdict

**Pass as a decision-ready feasibility proposal after two compatibility clarifications; not production selection or G1/G3 closure.** No critical/high omission or silently claimed physical capability was found. The recommendation is concrete and technically falsifiable, and it names the native transaction failure rather than pretending the proposed protocol supplies that capability. Both clarification findings below can be fixed in the documents without changing the recommendation or authorizing infrastructure.

This review deliberately does not demand fabricated closure of the native transaction, hold-handoff, vendor-copy, plaintext-holder or installed-authority gates. They are explicit blockers and rejection criteria. A proposal that identifies why its candidate might fail is a valid decision artifact; it cannot be presented as the completed production primitive.

## Findings and disposition recommendations

### M1 — Distinguish queued owner reservations from the single physical epoch cycle

**Location:** design operating envelope, section 9: “A second batch queues without a new destructive reservation.” Related spine AD-20 and AD-22.

**Evidence:** EventStore `DeletionConsumptionActor.ReserveAndConsumeAsyncCoreAsync` commits `ConsumptionReserved` and its original receipt before invoking `IAtomicDeletionManifestProvider.ConsumeAsync`. Its overlap check prevents conflicting target membership, but permits a different disjoint target batch in the same tenant. Thus the provider may receive a second batch that already owns a durable irreversible EventStore reservation while the first epoch cycle is waiting for deletion.

**Risk:** a builder could try to avoid, replace or roll back that upstream reservation, or confuse one physical erasure cycle with one outstanding EventStore original. The provider cannot change the actor's documented reserve-before-consume boundary.

**Recommended disposition: autofix.** State that at most one **physical epoch cycle** runs per tenant; subsequent already-reserved owner originals can queue unchanged. Retain each original batch/request/reservation and process it when the tenant returns to a stable epoch, without allocating a substitute reservation. The existing result rules continue to apply: `NotStarted` means authoritative absence of a physical custody start under that exact original, while every admitted/pending physical start is `Unknown`. Queue admission/idempotency must not manufacture new owner authority. If queue rejection before EventStore reservation is required instead, that is a new upstream admission contract and must be an explicit G2 integration decision.

**F0 addition:** model two disjoint same-tenant batches whose EventStore reservations exist before either is offered to custody; lose queue and consume responses, complete the first cycle, and require the second to retain its original reservation/bytes through its later cycle.

### M2 — Make the existing KEK field's semantic binding explicit

**Location:** design section 3, `Registration.KekVersion` identifies the object wrapping-key generation; spine AD-17/AD-23.

**Evidence:** input A requires wrap/unwrap authentication of the complete identity and **per-tenant KEK version**. The proposed hierarchy intentionally has both immutable per-object `W(t)` and a changing per-tenant KMS epoch `E(g)`. The current document preserves original registration/AAD and moves W(t)'s KMS envelope, which is a useful separation, but its wording may be read as silently changing the meaning of the existing `KekVersion` field from a tenant KEK to a per-object wrapping key.

**Risk:** independent adapter builders could bind registration to incompatible layers or use unauthenticated epoch metadata. An old wrapper could be accepted with a substituted epoch identity if the native KMS ciphertext/key identity and current independent lineage are not checked together.

**Recommended disposition: autofix plus explicit pre-production contract gate.** Name the two version identities separately and state that both are authenticated: the existing immutable original object-wrap context, and the exact KMS epoch ARN/generation in the current encrypted-W envelope/current authority checkpoint. State whether the existing contract permits the intended `KekVersion` interpretation; if it changes established field semantics, require an accepted versioned mapping/contract amendment before provider implementation. Do not claim that preserving a string field alone establishes the required per-tenant KEK binding. Retaining the original root and registration remains mandatory.

**F0/native-vector addition:** reject swapped W-version/tenant-epoch metadata and wrong key ARN/lineage while preserving successful survivor rewrap with unchanged original DEK/reference.

### L1 — Finalization mechanics remain pending

**Location:** spine frontmatter (`status: draft`) and planned gate completion.

**Recommended disposition: autofix at finalize.** After substantive reviewer fixes and deterministic lint, set `status: final`, retain all new ADs as `[PROPOSED]`, append the final event to the memlog, and keep production acceptance visibly unclaimed. Document lifecycle finality must not be confused with adoption of the proposed backend.

## Good-spine rubric walk

| Rubric dimension | Assessment |
| --- | --- |
| Real divergence points at feature altitude | Covered. Backend ownership, stable identity, current authority, physical pin order, common erasure prerequisite, irreversible receipt evidence, exact originals and caller authority each have an AD. |
| Enforceable Rules prevent stated divergences | Covered for the offline feasibility substrate. AD-16/20/21 explicitly block production on capabilities the chosen services do not supply. AD-19 does not invent a cross-owner atomic hold handoff. M1/M2 need wording precision at the interface seams. |
| Deferred choices cannot silently fork implementation | Acceptable at this altitude. SDK/runtime packaging, worker selection, checkpoint representation and actual quotes are deferred behind stable ownership and final-erasure invariants. Production blockers are labeled blockers rather than routine choices for individual builders. |
| Named technology/version is current and fit | Managed service API bindings are concrete and the design links primary AWS capability/limit/erasure/pricing sources. It correctly distinguishes an API date from a pinned server release or project access. This rubric walk does not replace the parent's live vendor-source verification. |
| Brownfield ratification | Covered. Existing Platform library/ports and EventStore owner remain boundaries; default refusal stays; no HMAC substitution, standalone per-target deletion loop or source-owner authority replacement is proposed. AD-9 exception is correctly escalated to the parent architecture. |
| Parent invariants remain binding | Covered. Parent AD-1, build modes, Administrator/deputy roles, executor/environment isolation, Dapr exceptions, run ownership, independent custody/recovery and forward-only rollback are inherited. Separate authority packaging is deferred without self-granting an exception. |
| Scope/capability coverage | Covered for a feasibility design, with unmet native atomicity, delay, hold handoff, internal-copy/holder assurance, complete providers and independent owner gates explicit. |
| Operational/environmental envelope | Strong coverage. Separate custody account, environments, ingress/egress, identities, survivor checkpoints, backup classes, logs, native erasure evidence, MFA, monitoring and recovery are addressed. Single-Region authority, no automatic current-head restore, one cycle/tenant, O(N) work, at-least-seven-day latency, RPO/RTO and retention decisions are explicit. |
| Shape and minimal seed | Appropriate. The spine carries invariants and two useful diagrams; evidence, cost and detailed failure protocol live in the companion design. No full schema/tree/runtime seed is invented. |
| Exact next work | Unambiguous. Only `model_epoch_custody.py` and `test_model_epoch_custody.py`; no network, credentials, real key bytes, provisioning or Live path. Failure cuts, controls, large vectors and explicit stop rules are named. |

## Input A reconciliation — Platform DEK decision

| Binding source requirement | Where it lands | Reconciliation |
| --- | --- | --- |
| Preserve `ObservedRevival`; stop ordinary Raft as erasure class | Design 1–2; AD-16 | Complete. Historical observed evidence is not turned into qualification. HMAC retains only its existing scope. |
| Concrete backend/version/store class/independent authority | Design 2–4, 9–10; AD-16/18 | Complete as proposed binding: standard AWS-generated single-Region KMS, DDB conditional current authority, immutable S3 originals, independent account/control service. Resource instances are deliberately not invented. |
| One original full-target root, complete AAD, original retry and no rotation recreation | Design 3–4/8; AD-17/18/23 | Covered; M2 clarifies the per-tenant epoch versus immutable wrap-version binding. |
| Pin/unpin exact identities; active/unknown pin denies with zero effects | Design 5; AD-19/20 | Covered as invariant; real guard-to-custody ordering is explicitly unresolved. No successful effective pin may be ignored. |
| All-or-none 1,000-target native transaction and immutable ordered vector | Design 5–6/10; AD-20/22 | Full native requirement explicitly **unmet**. Candidate's common physical cut and forward recovery are not mislabeled native transactional destruction. Owners may reject it. |
| Failure injection before/at/after commit; no per-key bypass | Design 5/8/11; AD-20/23 | Covered in protocol and bounded model/experiment plan. No executed proof claimed. |
| Exact retry/lookup after loss/failover; outage never absence | Design 4–6; AD-18/22 | Covered. Permanent originals outlive ten-minute service token. M1 clarifies concurrent queued EventStore originals. |
| Current caller authority and independent EventStore authorities | Design 4–5/8; AD-18/23 | Covered. No custody-minted dispatch/compromise/activation authority. |
| Every old copy: replicas, backup, log, historical KEK, cache, clone/operator path | Design 3/7/10; AD-17/18/21 | Complete inventory dimensions. Internal vendor copies and process-memory closure are named proof/assurance gates rather than waved away. |
| Old configuration/owner rollback cannot restore authority or root | Design 4–5/7; AD-18 | Covered under independent-authority survival assumption, incarnation/profile binding and permanent enrollment indexes. Total independent authority loss explicitly fails closed. |
| Public anchors/outcomes through referenced-batch lifetime | Design 6/9–10; AD-21/22 | Covered with accepted retention/trust policy still a pre-production gate. |
| Actual source/provider bindings before later real qualification | Design 8/10–11; AD-23 | Covered. Missing six-operation and batch ports remain missing; future work staged after erasure and contract gates. |
| No Live/OQ/consumer/register/availability claims | Throughout, especially 1/10–11 | Preserved. No source input is overwritten by this design. |

## Input B reconciliation — EventStore owner acceptance

| Owner requirement or hidden seam | Where it lands | Reconciliation |
| --- | --- | --- |
| G1/G3 source gap is an actual absent provider, not a configuration exercise | Design 1/8/10; AD-16/23 | Complete. No production implementation or installation is claimed. |
| Explicit source/target/outbox/snapshot/cache identity | Design 3/7/8; AD-17/23 | Complete. Same DEK/reference survives epoch evacuation; G4 mixed-root policy remains separate. |
| `IAtomicDeletionManifestProvider` exact source interface/result | Design 5–6/8; AD-20/22/23 | Correct Server.Security location and method/result states. No signature-only reservation substitution. |
| Actual owner reserves before provider starts | Design 5/9; AD-20/22 | Needs M1 wording fix; otherwise original-reservation recovery contract preserved. |
| Once-per-reference exclusive encryption lease and no retry reencryption | Design 8; AD-23 | Explicitly covered, including Unknown denial. |
| Exact independent binding/source-writer completion lease and release-time checks | Design 5/8; AD-20/23 | Covered. Fence/drain recognizes already released buffers and late operations. |
| Irreversible tombstone distinct from missing key/outage | Design 8; AD-21/23 | Covered. Native final-deletion evidence plus all-copy/barrier verification is required. |
| Complete canonical sorting/digest and size bounds | Design 6/11; AD-22/conventions | Covered. Native target digest and separate RFC 8785 capability bytes are distinguished; max legal byte vectors included. |
| Unique ordered target receipt IDs and original-batch coverage | Design 5–6; AD-22 | Covered. No fresh destructive identity for covered targets; mixed cohorts require owner-authorized residual handling. |
| Pre-dispatch hold versus later hold ordering | Design 5; AD-19 | Precisely surfaced as G2 handoff gate; no Product disposition or invented atomic cross-owner transaction. |
| Single owner for reserve/block/revocation/activation; consumed original recovery | Design 8; AD-23 | Preserved. Existing G2 journal/current authority remains mandatory; custody does not merge ownership. |
| Inherited admitted-original conditional transition journal | Design 4/8; AD-18/23 | Covered. Deprecated `RecordRevisionAsync` alone explicitly insufficient. |
| Thirty-second observation deadline and late-I/O quiescence | Design 5; AD-20/23 | Covered. Multi-day durable work returns Unknown and does not create a replacement operation. |
| G2/G4/G5/G6 and complete canonical G7 target remain open | Design 1/8/10–11; Deferred/Open Gates | Preserved. No test artifact/model/signature is promoted to physical proof or Live acceptance. |

## Handoff recommendation

Apply M1/M2 and finalize mechanics. The exact next implementation remains the two-file offline epoch model, with the additional queued-reservation and version-binding adversarial cases above. The document should be offered as a concrete recommendation that can be accepted or rejected on its delay, native-atomicity, independent-authority and operating-envelope constraints. Production provider construction and all infrastructure/Live actions remain outside this handoff.
