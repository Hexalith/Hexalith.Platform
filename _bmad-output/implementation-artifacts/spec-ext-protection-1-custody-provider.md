---
title: 'EXT-PROTECTION-1 custody provider'
type: 'feature'
created: '2026-10-21'
status: 'draft'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/../agents/_bmad-output/planning-artifacts/external-dependency-register.md'
  - '{project-root}/../eventstore/docs/implementation/ext-protection-1-owner-acceptance.md'
  - '{project-root}/docs/implementation/ext-secrets-1-erasure-authority-decision-2026-10-20.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EXT-PROTECTION-1 stays `Uncommitted`. No production class implements `ICustodyKeyLifecycleProvider`, `ICustodyKeyLifecycleAuthority`, or `IAtomicDeletionManifestProvider`, and EventStore cannot invent them. OpenBao HMAC and ordinary Raft restore are not a DEK or manifest provider. The retained `ObservedRevival` file hashes to `6efa381bc70b3de57a24142e5e679f53781c7f89d5ab6740fb7664966f30888e`.

**Approach:** Add those production ports on `bindings.hexalithcustody` / `custody-lifecycle-v1`. Pin, Unpin, and Destroy use `ExecuteAsync`/`LookupAsync`; `DestroyDekManifest` uses `ConsumeAsync`/`LookupAsync`. The witness stays outside the restored volume. Prove all-copy destruction and non-revival, then commit the Platform provider.

## Boundaries & Constraints

**Always:** Keep v21 and v23, role `interaction-custody`, and the fourteen operations. Root Destroy uses the original manifest. Missing binding, authority, or witness returns unavailable or unknown and writes nothing. One C# type per file. A disposable proof is not an installed public reference.

**Never:** Edit the Agents register or mark EXT-PROTECTION-1 `Committed` or `Available`. Run Live or OQ-5.4-1. Use HMAC, Raft, a Dapr secret/state/crypto shim, or self-report as destruction. Rewrite the Raft result, installed `runtime-reference.json`, or `provider-key-mappings.json`. Add a cloud SDK or application database. Edit EventStore or host startup. Install guard, role, or trust-anchor programs. Fill these null fields: `runtime.allCopyLifecycleAuthorityReference`, `runtime.allCopyInventoryReference`, `runtime.nonrollbackJournalReference`, `runtime.restoreTarget`, `runtime.protectionOwnerReference`, `runtime.compromiseGuardMirrorReference`, `runtime.currentRevocationReference`, `runtime.publicTrustReference`, `runtime.publicAnchorRetentionReference`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Pin / Unpin | Exact hold, live epoch | Execute pins or unpins; same-digest lookup matches | Duplicate or missing hold conflicts; no witness is unavailable |
| Pinned destroy | Active pin | No effect; BlockedByPin | Another unpin does not release it |
| Manifest consume | Sorted same-tenant targets, one reservation | All targeted copies destroyed or none; retry equals lookup | Changed digest conflicts; loss stays Unknown and lookup starts nothing |
| Backup restore | Restore the pre-destruction volume | Destroyed key cannot decrypt; unrelated key can | Outside witness rejects re-promotion; outage is Unknown, not NotStarted |
| Absent binding | No component | Typed refusal, zero writes | Transport success is not a receipt |

</frozen-after-approval>

## Code Map

- `src/Hexalith.Platform.Custody/ICustodyKeyLifecycleProvider.cs` — keep `ExecuteAsync`/`LookupAsync`. Actions live in `CustodyKeyLifecycleAction`.
- `src/Hexalith.Platform.Custody/ICustodyKeyLifecycleAuthority.cs` — `VerifyOutcomeAsync` rejects self-report. Do not change.
- `src/Hexalith.Platform.Custody/CustodyKeyLifecycleActor.cs` — null ports fail closed; pinned Destroy is BlockedByPin. Do not change.
- `src/Hexalith.Platform.Secrets.Runtime/CustodyBindingContract.cs` — reuse `pin`, `unpin`, `destroy-manifest`, `lookup-original`. Do not sort a signed manifest.
- `src/Hexalith.Platform.Secrets.Runtime/CustodyBindingDaprInvoker.cs` — reuse. `ReceivedUnverified` is not destruction. `wrap`/`unwrap` stay refused.
- `src/Hexalith.Platform.Custody/CustodySharedCutModel.cs` — reuse B0–B5 in the component. Not physical proof. Keep C0 inside the epoch envelope via `CustodyKeyWrapCore`.
- `../eventstore/src/Hexalith.EventStore.Server/Security/IAtomicDeletionManifestProvider.cs` — implement in Platform only. Leave `custody-component.proposed.json` `installed: false`. Raft result and HMAC files stay byte-for-byte.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.Platform.Custody.Component/` — private store: envelopes, copy inventory, outside witness, shared-cut destroy, pin fence, exact lookup. No Dapr, OpenBao, or cloud SDK.
- [ ] `src/Hexalith.Platform.Secrets.Runtime/CustodyKeyLifecycleBindingProvider.cs` — provider. Pin/Unpin call `pin`/`unpin`; root Destroy calls `destroy-manifest`. Lookup sends no effect.
- [ ] `src/Hexalith.Platform.Secrets.Runtime/CustodyKeyLifecycleBindingAuthority.cs` — authority. Null witness denies.
- [ ] `src/Hexalith.Platform.Protection/AtomicDeletionManifestBindingProvider.cs` — manifest `ConsumeAsync`/`LookupAsync`. EventStore.Server reference only here. `InternalsVisibleTo` from Secrets.Runtime.
- [ ] `Hexalith.Platform.slnx` — add the new projects and test projects.
- [ ] `tests/Hexalith.Platform.Custody.Component.Tests/` plus secrets-runtime and protection tests — matrix, including backup non-revival.
- [ ] Commit only those Platform sources and tests after `npx commitlint --edit` accepts the subject.

**Acceptance Criteria:**
- Given the disposable component, when its pre-destruction volume is restored, then the destroyed key cannot decrypt and an unrelated key can.
- Given the nine named runtime fields, when this is committed, then each is still null in the installed secrets runtime reference.
- Given this delivery, then the Agents register is unchanged, EXT-PROTECTION-1 is not `Committed` or `Available`, and Live did not run.
- Given `raft-probe-attempt-6.result.json`, then its SHA-256 is still the `ObservedRevival` hash in Intent.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Platform.Custody.Component.Tests/Hexalith.Platform.Custody.Component.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -nodeReuse:false -m:1` — expected: exit 0.
- Direct `dotnet` runs of those three test assemblies — expected: matrix passes, including non-revival. `sha256sum` of the Raft result matches the Intent hash. `git diff --check` and commitlint accept.
