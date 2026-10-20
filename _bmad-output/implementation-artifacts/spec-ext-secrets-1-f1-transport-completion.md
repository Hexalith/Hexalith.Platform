---
title: 'Resolve EXT-SECRETS-1 F1 transport and completion prerequisites'
type: 'feature'
created: '2026-10-20'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 3d51fed1f3d99acf36ef690f150136593298bd95
context:
  - docs/implementation/ext-secrets-1-erasure-authority-decision-2026-10-20.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Resolve the remaining F1 prerequisites for Agents Story 5.4: carry every owner-legal atomic 1,000-target manifest as one complete request, including the retained 24,692,931-byte SDK request, and establish how every key-bearing holder completes. Where an actual component, supported stack or separate authorization is missing, deliver an exact pinned configuration/change and termination protocol packet with explicit outstanding proof. Local conformance must never establish dependency availability or physical erasure.

## Boundaries & Constraints

Preserve Dapr-only access, owner identities/order/canonical bytes/signatures, existing public bounds, atomic consumption/shared physical cut, original recovery, paging, deadlines, analyzer fixes and all existing tests. Preserve the invoker's wrap/unwrap refusal until independent completion is proved. Preserve pre-existing unrelated workspace changes, as explicitly authorized on 2026-10-20.

Do not initialize nested submodules, deploy, inspect credentials, perform native destruction, run Live/F0/owner qualification, amend acceptance, add provider dependencies, split consumption or infer that one body-size setting covers the stack. Proposed artifacts and synthetic witnesses grant no authority.

## I/O & Edge-Case Matrix

| Given | When | Then |
|---|---|---|
| Legal complete manifests, including maximum escaping | Measure real raw SDK serialization and conservative complete bounds | Preserve the entire owner request; account separately for API protobuf, headers, component metadata and backend retention/transaction limits. |
| Default or unqualified transport | An oversized request is attempted | Refuse before SDK send; configuration proposals do not activate a production path. |
| Bounded candidate profile | Large synthetic SDK calls run | Record actual message bytes and one invocation, with explicit absence of sidecar/backend/physical evidence. |
| Missing, replaced or delayed holder | Completion or termination is evaluated | Retain original identity and uncertainty; block key delivery and terminal closure. |
| Cancellation, restart, duplicate or lost acknowledgement | Recovery occurs | Preserve old incarnations, inventory watermarks and SendCommitted; status/lookup only. |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Secrets.Runtime/CustodyBindingContract.cs`: closed DTO validation, 64 MiB decode cap; manifest `Proof.HoldId` is unconstrained. Existing owner/public bounds stay unchanged.
- `CustodyBindingDaprInvoker.cs`: raw SDK single call, default 4 MiB refusal and wrap/unwrap denial. Component sizing excludes forwarded metadata.
- `CustodyBindingIntent.cs`: supplied boolean validation; no actual holder accounting.
- `tests/Hexalith.Platform.Secrets.Runtime.Tests/CustodyBinding*Tests.cs`: real SDK capture, pinned owner Capture fixture and deterministic F1 producer; extend these.
- `docs/implementation/ext-secrets-1-f1-2026-10-20/`: existing protocols, research pins, counterexamples and prerequisites.
- `eng/ext-secrets-1/verify_f1.py`: detached build/XML/hash audit and required behavioral coverage.

## Tasks & Acceptance

- [x] Runtime contract/invoker and focused tests — account for the private proof-shape gap, conservative full-request bounds and forwarded metadata; preserve default/key refusal and public contracts.
- [x] Existing SDK/feasibility tests — serialize the retained maximum request with explicit candidate channel settings; retain exact bytes/hashes and one-call/denial controls. Report no executed sidecar/backend path.
- [x] F1 `transport-profile.json` and `transport-profile.md` — pin source revisions, 128 MiB candidate request settings at SDK/API/component, bounded headers/metadata, sub-1-MiB full responses and backend retention/transaction requirements. Include exact patches/hashes if required; absent artifacts/approvals remain absent.
- [x] F1 `termination-protocol.md` and machine-readable packet — define durable lease/copy lineage, all SDK/sidecar/component/native/kernel/host/delayed holders, admission, sealing, watermarks, capacity and independently verified termination/sanitation. Missing installed verifier means no completion permission.
- [x] `eng/ext-secrets-1/` packet checker/tests and verifier coverage — reject missing holder classes, substituted incarnations, unsealed queues and synthetic availability claims without weakening existing audits.
- [x] Existing F1 README, buffer accounting and F2/F3 prerequisites — connect verified changes to the exact outstanding proof and next qualification packet.

Acceptance: Given absent semantic component/backend and completion proof, when local verification passes, then keep DependencyAvailable=false, LiveReady=false and key delivery closed, and return the pinned next packet with precise unproved items.

## Implementation Notes

- Implemented 17 F1-scoped files; all match the isolated snapshot based on `17179e59843ee8cfb35892a74a2c18d3136fc9cf`. Concurrent unrelated workspace work is preserved and excluded from the F1 review diff.
- Detached F1 verification: 238 Runtime tests, 101 F1 cases, 29 behavioral methods; zero warnings/errors. Full Python suite initially passed 181 tests; final reviewed suite passed 189 tests. Evidence and source archive: `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/f1-transport-completion-20261020/`.
- Exact SDK captures: retained manifest 24,692,931 API protobuf bytes; 32 MiB transition 44,740,962 API protobuf bytes; one call each. These are local handler witnesses only.
- Proposed 128 MiB profile and exact unapplied header-units patch, private proof bounds, termination protocol and negative packet audit delivered. No component/backend or independent termination verifier is installed; all release/availability/Live gates remain closed.

## Spec Change Log

- Review corrections preserved the approved intent and all C# behavior. Pinned all prerequisite declarations, exact patch and immutable synthetic original/parent/queue identities; added omission/substitution and dependency-revision negative controls. No public surface or completion authority was added.

## Review Triage Log

All 14 findings below are addressed by the four-file review patch. Root independently inspected the complete patch, verified disposable-source application against exact base/result hashes, and reran F1 plus all 189 Python tests using new detached source/artifacts. No findings deferred. Final evidence: `/home/administrator/hexalith-secrets-evidence/ext-secrets-1/platform-f1-transport-reviewed-eqo129lx/delivery-verification.json`. The 17 delivered source files exactly match that verified snapshot.

| Finding | Verdict / route | Evidence and correction required |
|---|---|---|
| B1 | medium / patch | Changing proposed retry instructions still passes packet conformance; require the pinned no-resend/original identity rules. |
| B2 | medium / patch | Removing completeResponseEnforcement still passes; require complete frame/content-type/unknown-field/compression rules. |
| B3 | medium / patch | Removing backendRequirements still passes; require complete-object capacity, atomic manifest transaction, retention and current-authority prerequisites. |
| B4 | medium / patch | DecodeCapBytes=1 in SDK summary still passes although limits and verifier disagree; validate all summary bounds. |
| B5 | medium / patch | Self-declared hash allows replacement patch bytes; pin approved path/hash and retain pinned base/result hashes. |
| B6 | medium / patch | Removing old sidecar from both inventories passes while replacement satisfies class count; bind admission to an immutable original fixture baseline. |
| B7 | low / patch | Coordinated arbitrary lineage labels pass; pin original lineage and validate the concrete witness relationships. Actual journal/descendant proof remains unimplemented. |
| B8 | medium / patch | Generic queue-class seals cannot distinguish old/replacement sidecars; bind concrete seals to exact queue/participant identities and original, with complete coverage. |
| B9 | medium / patch | Synthetic original omits tenant/authority/generations and accepts a malformed digest; include protocol bindings and validate digest shape. |
| B10 | low / patch | Omitted verifier/profile/measured-capacity keys are accepted as explicit null; require the absence declarations. |
| E1 | medium / patch | Independently reproduced replacement of headerPatch.path/hash with unrelated Markdown; same cause/correction as B5. |
| E2 | medium / patch | Independently reproduced removal of backend, response and retry requirements; same omissions as B1–B3. |
| E3 | medium / patch | Independently reproduced coordinated loss of old sidecar; same missing baseline binding as B6. |
| V1 | medium / patch | Wrong dependency revision rejection is untested at run(); add clean wrong-revision cases for both dependencies, proving rejection before build. |

## Design Notes

No stock sidecar patch is currently required for request size alone: API body limits and component receive limits differ from the sidecar component client's send/receive defaults. A proposed 128 MiB protobuf profile accommodates the existing 64 MiB data cap plus bounded metadata and 32 MiB transitions after Base64 encoding. It does not prove field-derived completeness for an unbounded private proof. Incoming gRPC headers become component metadata; the pinned header setting has a units discrepancy that must be measured and recorded.

Termination sequence: Admitted → SendCommitted → UseStopped → BoundarySealed → QuiescenceObserved → TerminationObserved → SanitizationVerified → Closed. Cancellation, pool return, socket closure and process death do not establish sanitation. Authority, originals and witnesses survive outside the boundary.

## Verification

Run `python3 eng/ext-secrets-1/verify_f1.py` with fresh detached artifacts/output and clean existing Agents sibling Builds `5d03f1db9c38db34ecac7fe6048937478ffb82cc` and EventStore `c9f47dd5ba72918445dfd24a674a5b2c310589bd`. Require zero warnings/errors, all existing/new Runtime tests, retained XML and exact source hashes. Run `python3 -m unittest discover -s eng/ext-secrets-1 -p 'test_*.py' -v` with a detached log. Complete edits before immutable snapshot verification; retain failed attempts. Independently review the diff and confirm unrelated changes are preserved.
