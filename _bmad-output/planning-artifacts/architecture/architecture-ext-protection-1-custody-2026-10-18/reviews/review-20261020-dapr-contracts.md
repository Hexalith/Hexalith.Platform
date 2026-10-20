# Dapr boundary — contracts and good-spine review, 2026-10-20

**Verdict: changes requested in the closed operation contract and epoch-preparation wording.** The Dapr adoption itself preserves the owner's later constraint, source custody policy and the distinction between architecture approval and missing physical proof. No native AD-9 exception or mandatory cloud infrastructure is silently adopted.

Reviewed CURRENT Platform decision/spine, `SPEC.md`, `custody-binding-contract.md`, `implementation-plan.md` and `OWNER-APPROVAL.md`, against actual custody interfaces, mandatory inherited anchored transitions and the previously reconciled Agents/EventStore contract. No code, deployment, register or historical proposal was edited; no physical/qualification operation was run.

## High — closed binding allowlist omits mandatory anchored mutations (autofix)

The decision's complete semantic allowlist contains `admit-transition` and `lookup-transition` but no operation for `RecordTransitionAsync` or `RecoverTransitionAsync`. Actual `ICustodyKeyLifecycleAuthority` inherits `IAnchoredStateTransitionAuthority`; the current actor/deadline wrapper calls its admission, record, recovery and verification methods. The inherited contract expressly says admission does not advance the independent anchor, while recovery is a separately authorized mutation and verification is a read. Lookup must remain read-only under the adopted decision too.

Without explicit record/recovery operations, independent adapters could reinterpret admission or lookup as mutation, omit mandatory inherited proofs, or acquire a native bypass to advance anchors. Each conflicts with adopted AD-18/AD-23/AD-25 and the source's default-deny behavior.

Add closed `record-transition` and `recover-transition` operations and an exact authenticated verification mapping (`verify-transition` or read-only exact `lookup-transition`). Bind each phase to the same complete owner/purpose/original operation, predecessor/target revision/digest and transition bytes, current phase-specific permission and retained original result. Admission must stay non-advancing; record/recovery must conditionally mutate the independent anchor; verification/lookup must never do so. Add those mappings to the binding contract and I2/I5 acceptance, rather than relying on generic “anchored adapters” wording.

## Medium — expressly exclude E(g) from pre-arm retirement (autofix)

AD-17 says before B3 to permanently delete every “older/abandoned/tainted generation receiving X except that exact adopted successor.” E(g) is older than E(g+1), yet B4 is intended to be its physical cut after arm. The decision's similarly compressed predecessor rule can be read with the same ambiguity. An independent component builder must not delete the current target-bearing epoch during B2 preparation.

State the two live roles explicitly: E(g) remains the exact old epoch reserved/armed for B4; the verified adopted E(g+1) is the survivor path; every **other** decrypting predecessor/abandoned/tainted epoch must be permanently deleted before B3. Native effect/receipt evidence still follows B4/B5. Carry this distinction into the checkpoint/physical-intent contract and the offline model's pre-arm no-target-cut assertion.

## Contract/spec reconciliation

| Concern | Result |
| --- | --- |
| Owner approval | The frozen original approval basis is historical; the later explicit Dapr instruction qualifies adoption. OA-1 shared-cut approval is stated, while physical erasure/copy/hold/original evidence remains mandatory. No false native cross-service transaction is asserted. |
| Parent AD-9 | Existing provider-neutral/Dapr direction remains intact. The proposed semantic component adds a missing full lifecycle capability; it is not a thin wrapper around a direct-provider exception. No parent exception is edited/adopted. |
| Provider dependency | Applications/DI/contracts contain logical roles and opaque identities only. Native packages/resource IDs/credentials belong to replaceable infrastructure component implementation/configuration. AWS remains optional research; no cloud account/Region is a mandatory application dependency. |
| Key bytes/identity | Original tenant KEK, `CustodyKeyWrapCore`, C0 AAD/KekVersion and opaque original semantics survive. Explicit outer AAD order and inner enum/base64/Unicode rules reduce byte drift. F1 exact vectors remain a required pre-integration gate. |
| Authority and restore | Source-default-deny authority and admitted-original anchored transitions remain binding, subject to the missing operation mapping above. Opaque initial enrollment is pinned independently; first-release authority replacement/incarnation rollover stays unsupported/fail-closed. No restored clone or root reissue path is granted. |
| Holds/v21/v23 | Guard registration/dispatch, EventStore reserve/block/activation and custody arm remain distinct. Earlier effective/unknown pins block; later post-dispatch hold cannot obtain a conflicting successful physical pin and remains restrictive elsewhere. Current sole attestation/replacement-key revision semantics remain. |
| Original outcomes | Whole sorted target identity, original reservation/digest, exact ordered vectors/signatures, authenticated coverage/residual protocol, Unknown versus NotStarted and read-only lookup remain. No edited signed membership or per-target loop is introduced. |
| Copy and native intents | Dapr SDK/sidecar/UDS/gRPC/TLS buffers and late/duplicate transport calls explicitly join copy/holder closure. Retry suppression is not called exactly-once; admitted-original deduplication and no send-unknown replay remain required. Native-only proof does not replace end-to-end Dapr proof. |
| Operating envelope | Existing actor limits/completion headroom, whole-tenant preparation freeze, one cycle/tenant, O(N) sweep, independent failure/recovery classes and finite full-reference retention are preserved. Actual component/provider artifacts, placement and numerical operational settings remain missing inputs, not invented installation strings. |
| Qualification | F0/I1 offline work is the next scope. I2 must stop on unsupported transport/component behavior; I3 still owes a concrete provider/artifact. F2/F3 require separate exact synthetic authorization. No Live/Available/register/hash/plan change is claimed. |

## Good-spine checklist

The adopted paradigm and dependency boundary are explicit. AD IDs remain stable, inherited parent decisions are read-only and the new AD-25 fixes the Dapr constraint at the correct shared boundary. Deployment independence and recovery classes are still visible rather than hidden by a UDS process. The spec preserves capability coverage and directs implementation toward offline feasibility; it does not pretend an implemented component or physical backend exists.

The two findings above are real cross-unit divergence points: independent units could disagree about anchor mutation and when E(g) may be deleted. All other previously reviewed physical/native/transport assumptions remain plainly unproved and gated. Approval records neither authorize infrastructure deployment nor waive copy/holder/restore proof.

## Focused closure after revision — 2026-10-20

Re-read the CURRENT decision's complete operation allowlist, anchor/current-read/key-delivery/transport paragraphs, revised AD-17/AD-25 and expanded private binding contract.

- The high anchored-transition finding is closed. `admit-transition`, `record-transition`, `recover-transition` and `verify-transition` now map explicitly to the four inherited methods. Admission retains permission without advancing; record advances conditionally; recovery is a separately current-authorized same-admitted-original mutation with no physical replay; verification and lookup are fresh authenticated reads. Full owner/purpose/predecessor/target identity and original bytes remain bound. The common envelope also distinguishes key, manifest and anchored scopes without inventing a key identity for anchor operations.
- The medium pre-arm epoch finding is closed. Current source E(g) expressly remains live until B4, adopted successor remains for survivors with no selected X path, and only every **other** decrypting generation must already be permanently deleted before B3. This is consistent between the decision and AD-17.

The additional transport clarifications preserve the existing contract: value-free original receipts remain byte-identical and recoverable, while raw key delivery is ephemeral and individually leased rather than persisted/replayed for idempotency. Fresh challenge-bound current reads are separated from historical outcome replay. UDS access and Init pinning do not claim original-caller authentication or authority rollover. Bounded immutable receipt-byte pages are assembled/hash/identity/vector/signature checked before public Consumed; they do not split manifest admission or physical effects. Input remains one complete semantic invocation. Actual maximum-size input, 4 MiB pluggable response limits, deadline/assembly behavior, message duplicates and key-buffer closure remain explicit F1 stop gates; no unverified override or native bypass is promised.

**Final verdict: pass for the Dapr-constrained adopted architecture and bounded offline/spec handoff; no remaining actionable contract documentation finding from this review.** A qualifying component/provider, native/end-to-end transport/holder/copy proofs and actual installation/owner qualification remain absent. This closure establishes no physical guarantee, deployment authorization, register change or Available status.
