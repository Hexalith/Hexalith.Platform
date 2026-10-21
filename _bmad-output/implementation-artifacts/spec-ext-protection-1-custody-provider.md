---
title: 'EXT-PROTECTION-1 custody provider'
type: 'feature'
created: '2026-10-21'
status: 'done'
route: 'dispatch'
review_loop_iteration: 1
baseline_commit: 'f97065cc31c2bdc1afd11c515298cc8f1a51380b'
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
- [x] `src/Hexalith.Platform.Custody.Component/` — private store: envelopes, copy inventory, outside witness, shared-cut destroy, pin fence, exact lookup. No Dapr, OpenBao, or cloud SDK. One live epoch per tenant. A cut rewraps only that tenant's live non-target envelopes, using the full key identity. A destroyed or zeroed envelope is not a survivor and is not unwrapped. Admit the original on the draft witness and publish it only with the successful cut. A failed or blocked cut leaves no admission, and a later different digest for that operation conflicts. After one cut, a second manifest for a remaining live key still destroys that key or none, and another tenant's key still decrypts. `FindEnrolled` returns null for zero or multiple matches and does not throw. Destroyed targets drop and zero their plaintext DEK bytes. The replaced volume and any abandoned cut epoch are zeroed. `Dispose` refuses later effects. `TryDecrypt` clears the destination when it fails.
- [x] `src/Hexalith.Platform.Secrets.Runtime/CustodyKeyLifecycleBindingProvider.cs` — provider. Pin/Unpin call `pin`/`unpin`; root Destroy calls `destroy-manifest`. Lookup sends no effect. `Conflict` and `BlockedByPin` require the same witness confirmation as a successful effect. A lookup of an operation the witness has never stored is `Unavailable`; an unavailable witness stays `Unknown`.
- [x] `src/Hexalith.Platform.Secrets.Runtime/CustodyKeyLifecycleBindingAuthority.cs` — authority. Null witness denies. Keep denying actor admission, registration, effect, state, and transition checks while the nine runtime references are null. `VerifyOutcomeAsync` reads the witness and rejects a self-report.
- [x] `src/Hexalith.Platform.Protection/AtomicDeletionManifestBindingProvider.cs` — manifest `ConsumeAsync`/`LookupAsync`. EventStore.Server reference only here. `InternalsVisibleTo` from Secrets.Runtime.
- [x] `Hexalith.Platform.slnx` — add the new projects and test projects.
- [x] `tests/Hexalith.Platform.Custody.Component.Tests/` plus secrets-runtime and protection tests — matrix, including backup non-revival, a second manifest after a successful cut, a second tenant that still decrypts, lifecycle `ExecuteAsync` conflict and pinned destroy, and a missing-operation lookup.
- [x] Commit only those Platform sources and tests after `npx commitlint --edit` accepts the subject. Do not revert or restage unrelated Story 1.13 or composition edits.

**Acceptance Criteria:**
- Given the disposable component, when its pre-destruction volume is restored, then the destroyed key cannot decrypt and an unrelated key can.
- Given the nine named runtime fields, when this is committed, then each is still null in the installed secrets runtime reference.
- Given this delivery, then the Agents register is unchanged, EXT-PROTECTION-1 is not `Committed` or `Available`, and Live did not run.
- Given `raft-probe-attempt-6.result.json`, then its SHA-256 is still the `ObservedRevival` hash in Intent.

## Implementation Notes

## Spec Change Log

- Trigger: review loop 1. A successful cut kept destroyed envelopes as survivors and used one epoch for every tenant, so the next manifest returned `Unknown` and another tenant's key lost its epoch. Blocked and failed cuts also published the admission early.
- Amended: the component task now requires per-tenant epochs, full-identity live survivors, draft-only admission, a second manifest, and zeroing of replaced key bytes. The provider task requires witness confirmation for conflict and pin-block, and distinguishes a missing lookup from an outage. The authority task keeps actor admission denied while the nine references are null. Tests must cover the second cut, the second tenant, lifecycle conflict, and pinned destroy.
- Known-bad state avoided: a later destroy that cannot finish, collateral loss of another tenant, and a stuck reservation after a refused cut.
- KEEP: witness outside the restored volume; same-tenant unrelated key still decrypts after restore; pin blocks destroy and another hold's unpin does not release it; lookup sends no effect; duplicate consume returns the same vector; changed manifest digest conflicts; a lost effect stays `Unknown`; null witness and absent binding write nothing; a transport claim without a witness fact is not a receipt; a missing Dapr role refuses before the client; a signed manifest is not sorted; the nine runtime fields stay null; the Raft `ObservedRevival` hash and HMAC files stay byte-for-byte; host startup, EventStore, and the Agents register stay unchanged; operations remain `pin`, `unpin`, `destroy-manifest`, and `lookup-original`; one type per file; EventStore.Server is referenced only by the protection project.

## Review Triage Log

- high — `CustodyComponentStore.Consume` treats zeroed envelopes as survivors, so the next manifest cannot unwrap them and stays `Unknown` after `TryAdmit`. Verified: survivors are chosen by object id from every remaining envelope, and admission is stored on the live witness before `TryCut`.
- high — one `LiveGeneration` wraps every tenant, and a cut destroys that epoch after rewrapping only the target tenant. Verified: `TryCut` calls `DestroyEpoch` on the single live generation.
- medium — `FindEnrolled` uses `SingleOrDefault` and throws when two versions share tenant, object, and alias. Verified at `CustodyComponentStore.FindEnrolled`.
- medium — `TryEnroll` keeps plaintext DEK bytes in `_deks` after destroy, and a second identity with the same `DekKey` overwrites them without zeroing. Verified: `Consume` never removes `_deks`.
- medium — after `RestoreVolume`, `TryUnwrap` opens the restored envelope under the current epoch and ignores the witness successor that `TryDecrypt` uses. Verified in `TryUnwrap` versus `TryDecrypt`.
- medium — `TryAdmit` runs before the gate. A blocked cut stores no manifest digest, so a later different request digest overwrites the blocked fact instead of conflicting. Verified: the blocked path calls `Remember` and does not compare the previous digest.
- false — lifecycle `Destroy` returning `Unavailable` until an original is admitted is the specified missing-original refusal. `ConsumeAsync` is the path that admits during the cut. The test `TryAdmit` is the fixture for an original that already exists.
- false — denying actor admission, registration, effect, state, and transition checks while the nine references are null matches the constraint that those authorities are not installed. `VerifyOutcomeAsync` is the witness check the task names.
- low — rejected. `CustodyBindingDaprCall` drops the transport outcome, but host startup is unwired, so no caller meets it, and connecting that call to the store is more than a direct correction.
- low — rejected. The actor dropping provider `BlockedByPin` is the existing `RetainAsync` contract, which this delivery must not change, and the host does not register the actor.
- false — the review diff also contains Story 1.13 and dirty submodules. Commit `c0d1e3d` does not. Those edits are not this provider.
- low — rejected. A negative or short snapshot can be parsed loosely. Capture output is what restore uses, and rejecting every corrupt length adds a new parser.
- low — rejected. An undefined purpose integer can be restored. Later manifest validation refuses a non-root, and rejecting it adds parser branches.
- medium — `TryCut` can return false after `AddEpoch` without zeroing `nextEpoch`. Verified: the early `return false` is after `AddEpoch(nextEpoch)`.
- medium — replacing `_volume` drops the previous object without zeroing its epoch. Verified at the assignment after `TryCut`.
- low — `TryUnwrap` returns a non-32-byte `Open` result without zeroing it. `Open` returns `ciphertext.Length` bytes.
- low — `TryDecrypt` leaves the caller buffer unchanged on failure.
- medium — `Pin`, `Consume`, and `RestoreVolume` do not check `_disposed`, so a disposed store still changes the witness.
- false — an empty `HoldId` is not null, and the blocked path returns `BlockedByPin` for a non-null hold. It does not turn every destroy into `Unknown`.
- low — rejected. An undefined action falls through to `unpin`. Callers use the closed enum, and `Enum.IsDefined` is an added guard.
- medium — `Interpret` confirms only `Pinned`, `Unpinned`, and `Destroyed`. A binding can return `Conflict` or `BlockedByPin` without a witness fact.
- medium — verification gap, pre-verified. Lifecycle `ExecuteAsync` tests never assert duplicate pin, missing unpin, or pinned destroy, so a wrong `Map` arm would still pass.
- medium — verification gap, pre-verified. `LookupAsync` for an operation the witness has never stored is untested, so mapping `NotStarted` to `Unknown` would not fail.
- high — `Consume` remembers `BlockedByPin` under the batch id, and the manifest port reuses that id. After the hold is released, the same consume returns the old block and does not cut. Verified: `FindFact` returns before the gate, and `CustodyComponentBindingCall` uses `manifest.BatchId` when lifecycle is null.
- high — `Confirms` returns true for every `Conflict` whose digest differs from the stored fact, including a self-report against a pin or destroy. Verified at `CustodyOutsideWitness.Confirms`.
- medium — `FindEnrolled` still counts a zeroed envelope, so a replacement version makes the manifest port return `Unknown`. Verified: the match does not skip `IsZeroed` or destroyed identities.
- medium — two live envelopes with the same tenant and object id make `CustodySharedCutModel.Create` throw, the gate returns refused, and `Consume` stores that as a permanent `Unknown`. `AllocationKey` is tenant plus object id.
- medium — `Dispose` zeros the KEK and plaintext DEK dictionary and leaves volume and witness epoch bytes readable through `CaptureVolume`.
- low — carried. `CustodyBindingDaprCall` still drops the transport outcome. Host startup is unwired, and the previous rejection stands.
- false — carried. Story 1.13 enrollment text and dirty submodules are in the baseline diff and not in the custody commit.
- low — rejected. A short snapshot can pass `ReadBytes` and later throw `ArgumentException`. Capture does not write that snapshot, and the fix is a new parser.
- medium — verification gap, pre-verified. Forged `Conflict` and `BlockedByPin` without a witness fact are untested.
- medium — verification gap, pre-verified. A `NotStarted` witness plus a binding that claims `Destroyed` is untested on the manifest port.
- medium — verification gap, pre-verified. `ConsumeAsync` for an unresolved `FindEnrolled` target is untested.
- medium — verification gap, pre-verified. Destroy of a key absent from the admitted manifest is untested.
- medium — verification gap, pre-verified. A null witness on the manifest port is untested.
- medium — verification gap, pre-verified. `CustodyBindingDaprCall.DispatchAsync` is never called by the unconfigured-role test.

## Verification

**Commands:**
- `dotnet build tests/Hexalith.Platform.Custody.Component.Tests/Hexalith.Platform.Custody.Component.Tests.csproj --configuration Debug -p:UseHexalithProjectReferences=true -nodeReuse:false -m:1` — expected: exit 0.
- Direct `dotnet` runs of those three test assemblies — expected: matrix passes, including non-revival. `sha256sum` of the Raft result matches the Intent hash. `git diff --check` and commitlint accept.
