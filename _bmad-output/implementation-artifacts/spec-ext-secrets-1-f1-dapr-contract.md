---
title: EXT-SECRETS-1 I2/F1 Dapr custody contract feasibility
type: feature
created: 2026-10-20
status: done
route: dispatch
review_loop_iteration: 0
baseline_commit: 69689ab00b2056c6f311af9cb4120fb38c8c766d
context:
  - /tmp/platform-ext-secrets-1-f1-20261020/_bmad-output/specs/spec-ext-secrets-1-interaction-custody/custody-binding-contract.md
  - /tmp/platform-ext-secrets-1-f1-20261020/docs/implementation/ext-secrets-1-erasure-authority-decision-2026-10-20.md
---

<frozen-after-approval reason="explicit user continuation of approved I2/F1; scope authorized in invocation">

## Intent

Complete the provider-neutral Dapr custody component contract and bounded synthetic F1 checks from candidate 69689ab00b2056c6f311af9cb4120fb38c8c766d. Establish exactly what source/transport can support and retain reproducible counterexamples where the complete physical contract cannot qualify.

## Boundaries & Constraints

Always preserve existing identities, canonical bytes, signed capabilities, ports, policies and actor limits. Use pinned Dapr 1.18.1 and Dapr.Client 1.18.10. Keep full 1–1,000 target admission atomic. Distinguish local contract conformance, source-inspected transport facts and unproved physical guarantees. Default missing/current authorization or ambiguous results to denial/Unknown. This work is an unbound source contract, not a production provider adapter (I5).

Never rerun F0 or owner qualification, deploy, perform native destruction, inspect credentials/real keys, amend Agents register, invent authority, add provider SDKs or bypass Dapr. Keep Live blocked. No concrete component is installed/qualified; a synthetic harness is never physical evidence.

## I/O & Edge-Case Matrix

| Scenario | Given / When | Then |
|---|---|---|
| Exact bytes | Existing complete identities, Unicode/optional values and signed capability | Preserve original byte/digest/order rules; reject substitutions/unknown fields. |
| Full manifest | Legal 1/1,000 vectors including maximum escaping/Unicode | Measure whole encoded request at each known limit; retain exact oversized counterexample without tightening fields or splitting. |
| Original pages | Current authorized header/pages, <=256 KiB data and <1 MiB frame | Assemble contiguous complete original under bounded total/deadline; validate header identity, count, hashes, vector/signature before terminal exposure. |
| Invalid pages | Wrong authority/identity, missing/holey/overlapping/truncated/oversized/tampered pages | Fail closed without partial Consumed; never cache/page key payloads. |
| Phases | admit/record/recover/verify/lookup | Bind exact scope/predecessor/target/bytes and distinct private methods; recovery separately authorized; reads do not mutate. |
| Faults | Duplicate, lost result, cancellation, restart/re-registration | Preserve original IDs/intents; no automatic effect retry; lookup only; changed enrollment rejected; same enrollment retains uncertainty. |
| Key release | SendCommitted or uncertain delivery | Status-only duplicate/lookup; no cached raw key; incomplete holders/buffers block closure. |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Custody/PlatformCanonicalBytes.cs`: reuse framing, no alternative identity sort.
- `CustodyKeyObjectIdentity`, `CustodyKeyRegistration`, `CustodyKeyLifecycleRequest`, `ICustodyKeyLifecycleProvider/Authority`: existing ports remain unchanged and unbound.
- EventStore contracts `DeletionBatchCapabilityIdentity.TargetManifestDigest` preserves supplied order; strings allow 2048 UTF-16 chars, not a 2048-byte cap. `CanonicalPayload` is the existing signed JCS identity.
- EventStore `AnchoredStateTransition` and `IAnchoredStateTransitionAuthority`: four phases, max pending 32 MiB.
- `SecretsDapr.cs`: existing service transport is not custody output-binding transport; do not change its public behaviors.
- `tests/Hexalith.Platform.Secrets.Runtime.Tests`: existing test project; Builds `/tmp/ext-secrets-1-builds-20261019`, EventStore `/tmp/ext-secrets-1-eventstore-20261019` are clean pinned build dependencies.
- `eng/ext-secrets-1/prepare_acceptance.py`: existing detached preparation only, no Live execution.

## Tasks & Acceptance

- [x] `src/Hexalith.Platform.Secrets.Runtime/CustodyBinding*.cs` — implement closed provider-neutral value-free request/result framing, operations, distinct phase mapping and current exact authorization seams using existing DTOs/bytes. Include explicit enrollment, NativeIntent and ephemeral delivery contract validation without pretending to implement durable physical authority. No DI production binding.
- [x] Same source — bounded authenticated original-result assembly (header/reference/request/reservation/manifest, exact length/count/hash), preserve complete original vector/signature; malformed/unavailable stays Unknown. No key paging.
- [x] Same source — one-attempt Dapr raw-byte binding invoker using existing SDK; enforce supported default limit/refusal and caller budget; no retry loop or inferred physical success. If unsupported, retain a fail-closed contract instead of bypass.
- [x] `tests/Hexalith.Platform.Secrets.Runtime.Tests/CustodyBinding*Tests.cs` — meaningful synthetic positives/negative controls for every matrix row, exact existing byte vectors, real SDK serialization/size boundary where feasible. Emit deterministic F1 JSON evidence to an explicitly supplied output path; include oversize construction/length/hash, finite actor headroom and limits. Do not rerun shared-cut F0 tests.
- [x] `docs/implementation/ext-secrets-1-f1-2026-10-20/README.md` — capability/gap matrix, source/transport/physical distinctions, concrete buffer accounting strategy, exact F2/F3 prerequisites and replay commands. Root coordinator writes final research/pins/packet after implementation.

Acceptance: Given no qualified component, when synthetic conformance passes, then report bounded source evidence and explicit physical/Live blockers. Given worst legal request exceeds pinned defaults, when measured, then retain reproducible counterexample and require separately approved supported transport/component revision. Given unknown native/key I/O, when cancellation/restart occurs, then never infer closure or create a new original.

## Implementation Notes

Final source unit: 95 focused F1 cases and eight audit regressions, with all reviewer findings closed. Both wrap and unwrap explicitly refuse; exact lifecycle lookup and nonterminal unverified manifest status framing preserve existing semantics. Owner-accepted synthetic input witnesses use valid tenant/batch/UTF-8 bounds; prior wider examples are structural only. Final full Runtime verification is recorded in the detached bundle.

Review requires preservation of the consuming owner constraints as well as public canonical DTO bounds: AggregateIdentity tenant/domain <=64 ASCII and batch aggregate ID <=256; DeletionConsumptionIdentity.Capture text <=2048 UTF-8 bytes and dispatch >= committed issue. Structural schema sizing must not be labeled a legal admitted owner request. Existing DTO digest/canonical byte rules, all-copy policy and no-Available boundary remain unchanged.

Implemented the unbound contract, deny-default exact authorization seam, byte codecs, strict original paging and single-attempt raw SDK invoker; raw unwrap is explicitly refused. Initial verification: 56 F1 and 188 total Runtime tests passed with zero warnings/errors. Complete request default-limit, transport-holder closure, durable component, current authority and operating-capacity feasibility remain blocked. Root added fresh-evidence audit/replay tooling, pinned public research/protocols, capability matrix and separate F2/F3 prerequisites.

No intent gaps or irreversible work. User explicitly authorized this continuation in an isolated worktree; no additional scope approval needed. Footprint is new contract/source tests and evidence documents only. Implementation may choose internal structure; preserve sanctioned semantics if a concrete unsupported primitive forces refusal.

## Spec Change Log

## Review Triage Log

| Finding | Verdict / route | Evidence and correction |
|---|---|---|
| V1 | medium / patch | Pin/unpin branches have no request tests; ledger sizing does not execute validation. Add valid and independently malformed lifecycle cases. |
| V2 | medium / patch | Transition negative controls use stale owner digests and fail before scope/target-hash checks. Rebind those requests before asserting rejection. |
| V3 | medium / patch | Per-page hash faults do not exercise the whole-original SHA check. Add valid pages with a mismatching complete hash. |
| E1 | medium / patch | Detached probe proves an isolated JWS surrogate becomes U+FFFD after Encode/Decode. Require strict UTF-8 without changing original bytes. |
| B1 | high / patch | Detached probe returns Consumed for pre-cancelled completed-task assembly. Require explicit budget checks, including before terminal exposure, and isolate cancellation tests. |
| B2 | medium / patch | Exact lifecycle lookup rejects the original payload; dropping it changes the owner digest. Retain original lifecycle identity under read-only lookup. |
| B3 | medium / patch | Manifest response union cannot carry existing NotStarted. Add value-free status/proof framing that remains unverified until an independent verifier accepts it. |
| B4 | medium / patch | Wrap exchange has no input/registration-return contract. Explicitly refuse this incomplete path and retain its exact missing exchange as a qualification gap. |
| B5 | medium / patch | Probe accepts mismatched wrap envelope operation/revision. Bind duplicate original representations before any authorization. |
| B6 | medium / patch | Registration/lifecycle text accepts 6144 UTF-8 bytes where existing custody owner allows 2048. Reuse owner-specific byte limits; preserve separate capability rules. |
| B7 | medium / patch | Probe accepts dispatch preceding committed issue. Preserve the existing owner static ordering relationship. |
| B8 | medium / patch | Lifecycle response framing permits incompatible action/status/receipt combinations before ReceivedUnverified. Validate static result shape without granting physical authority. |
| B9 | medium / patch | Evidence audit accepts producer-only XML. Require the behavioral class/method coverage manifest and retain audit negative controls. |
| B10 | medium / patch | Maximal fixture tenant/batch bypasses AggregateIdentity restrictions. Retain structural vectors as structural; add counterexamples verified by existing owner Capture and correct all legal-size claims. |

| E2 | medium / patch, closed | Replaced unconditional external compilation with byte-identical pinned local fixture; verifier compares local/external/approved hashes. Independent package-only evaluation with a missing source root confirms local Compile/EmbeddedResource resolution. |

All 15 source/test findings are closed by independent verification. The final fresh Runtime/audit verification and exact content hashes are retained in the detached F1 evidence bundle; no physical, current-authority, complete-frame or installed-component gap is closed by this source status. I2/F1 ends with a feasibility stop, and Live remains blocked.

## Verification

Build and run the Runtime test project with pinned external Builds/EventStore paths. Retain all failures and passing XML/logs under detached F1 evidence, no Local verifier (which reruns completed F0), no Live verifier. Independently review staged diff and exact result counts. Commit final source locally, then prepare detached immutable replacement packet from clean HEAD with absent anchors retained as null.
