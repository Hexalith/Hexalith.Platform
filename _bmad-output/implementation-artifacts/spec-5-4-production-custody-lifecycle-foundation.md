---
title: 'Platform custody-lifecycle-v1 foundation for Agents 5.4'
type: 'feature'
created: '2026-10-25'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: eecea3fa467d27226ef6173c19f853add36bb26c
context:
  - ../agents/_bmad-output/planning-artifacts/external-dependency-register.md
  - ../agents/_bmad-output/implementation-artifacts/story-5-4-dependency-reconciliation-2026-10-22.md
  - _bmad-output/planning-artifacts/architecture/architecture-ext-protection-1-custody-2026-10-18/ARCHITECTURE-SPINE.md
  - docs/implementation/ext-secrets-1-semantic-provider-2026-10-22/FINAL-VERIFICATION.md
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Continue the production custody-lifecycle-v1 foundation needed by Agents Story 5.4, reusing Platform custody work. Complete the adopted Dapr-only provider/component, actual key delivery, independent current/nonrollback authority, copy inventory, lifecycle operations and exact original receipt lookup. Return independently reviewed implementation, deployment artifacts, local validation and exact missing installation inputs.

Connect existing component internals through public authenticated adapters and deployment packaging. Distinguish source completion from missing installations, owning-service integrations and physical qualification.

## Boundaries & Constraints

**Always:** Applications use configured logical Dapr roles and provider-neutral contracts. Native SDKs, identifiers and credentials stay inside infrastructure. Require fresh authenticated current authority, independently enrolled genesis, retained immutable originals and allocation exclusions; local actor state never supplies authority. Preserve exact identity/AAD/KEK bytes, guard dispatch versus protection reservation ordering, all-or-none sorted manifests, survivor recovery, hold ordering, completion reserves and existing limits. Track actual key holders and copies before release; uncertain sends remain lookup-only. Key material is ephemeral, owned and cleared, never serialized into authorizer requests, originals, logs or evidence. Record credential references only.

Preserve the OpenBao ObservedRevival negative result, SHA-256 `6efa381bc70b3de57a24142e5e679f53781c7f89d5ab6740fb7664966f30888e`, with G1 unqualified. Wrappers, tombstones, missing keys, scheduled deletion and local doubles supply no irreversible-erasure proof. Retain historical evidence unchanged. Existing AWS infrastructure internals remain an uninstalled reference implementation, not an adopted mandatory backend.

**Never:** Provision/deploy, create/delete real keys, run destructive experiments, serve/worker against native installations, owner Live/Full commands, qualification/cohort or consuming Agents seams. Change dependency acceptance, integration dates, timing, Open export disposition or history policy. Edit sibling repositories, introduce application-native SDK access, self-issued approval evidence or production DI activation. Distinguish missing implementation from missing installation; references alone close neither.

## I/O & Edge-Case Matrix

| Given | When | Then |
| --- | --- | --- |
| Current enrolled authority and exact holder admission | Wrap/unwrap and complete a key use | Actual original key flows through Dapr; every copy remains tracked until independently verified completion. |
| Lost key response or duplicate delivery | Retry/lookup | Return value-free original status; never replay a release or regenerate a root. |
| Effective hold, stale proof, unavailable authority or substituted identity | Invoke lifecycle/release | Deny before effect; uncertainty remains Unknown. |
| Complete reserved multi-target manifest and survivor | Execute component lifecycle locally with doubles | One shared cut and complete ordered original, or no terminal destruction claim. |
| Retained terminal result | Recover pages after response loss | Verify exact bytes/signature/vector before exposing Consumed; corrupt/missing pages remain Unknown. |
| Application restore or missing genesis | Read current/admit transition | Independent challenge-bound authority governs; no bootstrap or restored rollback. |
| Missing actual installation/closure evidence | Render/preflight deployment | Name every missing input; produce no runnable fabricated installation or availability claim. |

</frozen-after-approval>

## Code Map

- `infrastructure/Hexalith.Platform.Custody.Dapr/{SemanticProvider,KeyExchange,RecoverableLifecycle,ClosureInventory,AwsAuthorityStore,CheckpointArchive,BindingService}.cs`: reuse native intents, whole-head CAS, B1–B5, copy closure and originals; repair gaps found during integration.
- `src/Hexalith.Platform.Secrets.Runtime/CustodyBinding{Contract,DaprInvoker,Originals,OwnerAdapter}.cs` and `CustodyOwnerAuthority.cs`: reuse exact wire validation, transport admission, signed currentness and paged assembly. Add public adapters without exposing internal native configuration.
- `CustodyBindingDaprCall.cs`: Build lacks authority; Dispatch discards outcomes. Replace production integration; retain fixture restrictions.
- `src/Hexalith.Platform.Protection/AtomicDeletionManifestBindingProvider.cs`: currently depends on the in-memory store/witness. Add an authenticated Dapr implementation of the existing manifest port.
- `CustodyKeyLifecycleBindingAuthority.cs`: replace snapshot authority in production.
- `docs/implementation/ext-secrets-1-semantic-provider-2026-10-22/`: historical handback, inputs and evidence; preserve bytes.

## Tasks & Acceptance

- [ ] `src/Hexalith.Platform.Secrets.Runtime/CustodyLifecycleClient.cs` and related adapters: expose usable wrap/unwrap/completion, pin/unpin and original lookup; connect an authenticated Dapr authorizer client with exact body binding and no local signing fallback.
- [ ] `src/Hexalith.Platform.Protection/DaprAtomicDeletionManifestProvider.cs`: implement existing manifest port using complete signed request, fresh authority and authenticated full-original assembly, preserving Unknown on loss.
- [ ] `infrastructure/Hexalith.Platform.Custody.Dapr/`: repair integration defects in authority, inventory, lifecycle and original recovery while preserving existing physical gates.
- [ ] `eng/ext-secrets-1/custody-deployment/`: add component container/build, Dapr registration, protected socket/workload/worker templates and offline input preflight. Require pinned artifacts, scoped credential references, independent authority placement and exact profiles; never apply.
- [ ] `infrastructure/Hexalith.Platform.Custody.Dapr.Tests/` and Runtime/Protection tests: exercise public adapter through actual SDK/service boundary; test matrix failures, multi-target lifecycle, loss, key cleanup and signed original recovery.
- [ ] `docs/implementation/custody-lifecycle-foundation-2026-10-25/`: retain local command results, review findings, source/artifact hashes and installation-input ledger with owner/action per gap; distinguish source, installation and physical-proof status.

Acceptance: Given the existing foundation, when local integration tests pass and independent review findings are resolved, then usable Dapr-only production adapters and deployment artifacts are reviewable with exact remaining blockers. Given absent physical installations/proof, when reporting completion, then no installed-provider, irreversible-erasure or dependency-availability claim is made.

## Implementation Notes

No irreversible operation is required. Give absent authorizer/control/observer implementations, EventStore integration and enrollments explicit dispositions; absent source is not configuration.

## Spec Change Log

## Review Triage Log

## Verification

Build/run component, targeted Runtime and Protection suites in fresh external artifact directories. Cover every matrix row, including signed refusals. Run offline deployment validation, scoped diff checks and negative-evidence checksum verification. Doubles establish code behavior only.
