---
title: 'Validate exact actor-history custody contracts'
type: 'feature'
created: '2026-10-08'
status: 'done'
baseline_commit: '2c4f788a6ffde2646de1686492dc817f5505c922'
route: 'dispatch'
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Actor-history custody has no exact policy gate. `IdentityHistoryPolicy.IsValid` accepts any positive retention and any policy id, and nothing preserves an original unit, lifecycle, copy, or receipt.

**Approach:** Add those contracts in `Hexalith.Platform.Custody` for `party-actor-retention-v1`, purpose `party-actor-history-v1`, trigger `binding-effective-at`, and 365 fixed days from the original binding-effective instant.

## Boundaries & Constraints

**Always:** Store instants only after `ToUniversalTime`. Expiry is that instant plus `TimeSpan.FromDays(365)` (31,536,000 seconds). Readable only while `now < ExpiresAt`. Admission receives `now` and rejects expiry; the unit constructor does not, so an expired record can round-trip. Domain must be `party` after `AggregateIdentity` lowercasing. Scope is tenant, domain, aggregate, environment, and instance. Evidence id and admission revision stay fixed; lifecycle revision, epoch, and fence stay separate. Generations grow only on the same unit and deadline. Copy class and outcome text stay opaque, and a copy deadline equals the unit deadline. States move only `Active` → `Expired` or `PendingDestruction`; `Expired` → `PendingDestruction`; `PendingDestruction` → `Destroyed`; `Destroyed` → `ReceiptFinal`. A forward move needs a higher current revision. Operation id starts at `PendingDestruction`; a receipt reference exists only at `ReceiptFinal`. Copy registration is `Registered`, `InFlight`, `Completed`, or `Invalidated`; only `Completed` has a completion reference. A receipt has inventory generation `>= 0`, at least one unique class outcome, and the original facts. Invalid input throws `ArgumentException`; overflow throws `ArgumentOutOfRangeException`; null objects throw `ArgumentNullException`.

**Never:** Register `IIdentityHistoryCustody` or add a provider, store, signer, adapter, or host wiring. Do not change `AddPlatformCustody`, cleanup, HMAC, export, signing, inventory, or EventStore contracts. Do not set the three capability flags. Leave Q1–Q7, F1, and F2 unresolved. Do not use a calendar year, local midnight, last access, erasure time, or a stored non-zero offset. Receipts have no payload, key, or actor content. A retry must not mint evidence, revision, or deadline.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Approved admission | Party scope, approved policy and purpose, offset-equivalent instant, future `now`, evidence id, revision > 0 | UTC instant and expiry 365 fixed days later; scope and evidence kept | N/A |
| Unapproved policy | Wrong or blank id, purpose, or trigger; any other retention | No instance | `ArgumentException` |
| Overflow | Instant plus 365 days outside `DateTimeOffset` | No deadline | `ArgumentOutOfRangeException` |
| Leap boundary | `2023-03-01T00:00:00Z` | Expiry `2024-02-29T00:00:00Z` | N/A |
| Exclusive deadline | One tick before, at, and after expiry | Readable only before | New admission at or after throws `ArgumentException` |
| Round-trip | Valid unit, lifecycle, copy, and receipt | JSON and same inputs compare equal, including zero offset and original facts | Tampered deadline or scope fails |
| Changed scope | Different tenant, aggregate, environment, instance, evidence, or instant | Unequal | N/A |
| Lifecycle | Reverse, skipped, or same-revision move; generation added on the same unit | Illegal move rejected; deadline unchanged | `ArgumentException` |
| Later copy | Deadline after the unit | No copy | `ArgumentException` |
| Receipt | Original facts, unique class outcomes, confirmation time | Round-trip keeps facts and expiry | Empty, blank, or duplicate outcomes, or a changed deadline, throw `ArgumentException` |
| Unavailable custody | `AddPlatformCustody` | History custody service is null | N/A |

</frozen-after-approval>

## Code Map

- `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Security/IdentityHistoryPolicy.cs` — reuse `IsValid` and `DeriveExpiry`; do not edit or mint `IdentityHistoryCustodyEvidence`.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Identity/AggregateIdentity.cs` — reuse lowercase validation; do not edit.
- `src/Hexalith.Platform.Custody/IdentityHistoryCleanup.cs` lines 24–32 and `PlatformCustodyServiceCollectionExtensions.cs` — keep the existing gate and leave history unregistered.
- `src/Hexalith.Platform.Custody/ExportKeyDeliveryIdentity.cs` — `ArgumentException` style. Normalize offsets here. `IdentityHistoryCleanupTests` line 105 expects no history service.
- Add `IdentityHistoryCustodyOptions.cs`, `IdentityHistoryCustodyUnit.cs`, `IdentityHistoryLifecycleRecord.cs`, `IdentityHistoryCopyRecord.cs`, `IdentityHistoryDestructionReceipt.cs`, and `tests/Hexalith.Platform.Custody.Tests/IdentityHistoryCustodyPolicyTests.cs`.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Platform.Custody/IdentityHistoryCustodyOptions.cs` -- Add the approved policy, UTC deadline, exclusive read, and admission rejection.
- [x] `src/Hexalith.Platform.Custody/IdentityHistoryCustodyUnit.cs` -- Add the party unit, evidence, admission revision, and derived expiry.
- [x] `src/Hexalith.Platform.Custody/IdentityHistoryLifecycleRecord.cs` -- Add forward-only state, separate authority, generations, and receipt linkage.
- [x] `src/Hexalith.Platform.Custody/IdentityHistoryCopyRecord.cs` -- Bind generation, class, location, owner, registration, and the original deadline.
- [x] `src/Hexalith.Platform.Custody/IdentityHistoryDestructionReceipt.cs` -- Add the content-free original receipt.
- [x] `tests/Hexalith.Platform.Custody.Tests/IdentityHistoryCustodyPolicyTests.cs` -- Cover the matrix, including absent history registration.

**Acceptance Criteria:**
- Given the contracts, when built and serialized, then scope, evidence, admission revision, and UTC expiry survive retry and JSON round-trip.
- Given `AddPlatformCustody`, when the provider is built, then `IIdentityHistoryCustody` is absent and no capability flag is set.
- Given Q1–Q7, F1, and F2, when this task ends, then they remain unresolved and no backend, host, or successor code was added.

## Implementation Notes

## Spec Change Log

## Review Triage Log

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH01 | low | defer | The Builds R5 note still says patches are uncommitted on `50b0257` while this dirty tree moves that gitlink to `0097b5a`, and other submodule pointers move with it. That mismatch is in other in-progress work, not in the actor-history contracts. |
| BH02 | false | reject | `IdentityHistoryCustodyOptions`, the unit, lifecycle, copy, and receipt do not change signing, HMAC, `AddPlatformCustody`, or a history store. `sprint-status.yaml` has no key because this file is not an epic story. Empty review sections are filled by this step. |
| BH03 | medium | defer | `RetainAsync` calls `ResolveTrustAsync` with `requireCurrent: false` for a fresh `SignAsync` as well as lookup recovery (`DeletionCapabilitySigningActor.cs` around the retain call). A non-current, non-revoked verifier can still store that in-flight signature. This signing change was already dirty before this story. |
| BH04 | medium | defer | After a `Signed` outcome is saved, `ResolveAsync` returns it without reading trust or `OriginalIssuanceReceiptId` again. The receipt id is checked in `RetainAsync` and is not a field of `DeletionCapabilitySigningOutcome`. Pre-existing signing work. |
| BH05 | medium | defer | `IsRevoked` defaults to false, and retain does not require current authority, so a publisher that only clears `IsCurrentNonRevoked` still passes retain. Pre-existing signing work. |
| BH06 | medium | defer | `PrivateOwnerOperationAuthenticator` computes its tag with `PlatformHmacPurpose.TrustedEnvelope`. That type is separate uncommitted authenticator work; the history contracts do not call it. |
| BH07 | medium | defer | `TryWriteAsync` advances `RecordRevisionAsync` before `TrySaveStateAsync` and ignores a false or thrown save. A lost CAS can leave the anchor ahead of durable state. Spool work, not these contracts. |
| BH08 | medium | defer | `IsReadyAsync` uses `Records.All(...)`, which is true for an empty list, and `ReadAsync` turns a missing component value into revision 0 with no records when the authority attests that digest. Spool work. |
| BH09 | medium | defer | `ObserveAsync` returns null once `Records.Count` reaches 10000, and nothing removes stored records, so a fully receipted spool can stay ready while new observations are dropped. Spool work. |
| BH10 | medium | defer | `DrainAsync` throws `ArgumentOutOfRangeException` for a bad `maximumCount` inside `catch (Exception)` and returns 0. The same catch turns malformed spool state into a normal null or zero. Spool work. |
| BH11 | medium | patch | `IdentityHistoryCustodyUnit` rejects `admissionRevision <= 0`, and no test calls that path. The non-party domain clause is already locked by the JSON domain tamper. Cross-object matching, string length limits, and the spool test command are not requirements of these separate contracts. |
| BH12 | medium | defer | `StateDigest` and `IntentDigest` hash default `JsonSerializer` bytes with no domain prefix, so a serializer change moves restore anchors. Spool work. |
| BH13 | low | reject | Lifecycle and receipt use `Equals` for value comparison, and the round-trip tests use `ShouldBe`. No caller in this change uses `==`. Adding operators would publish API the contract does not name. |
| BH14 | medium | defer | Private-owner grant and credential checks require `Offset == TimeSpan.Zero` and reject an offset-equivalent UTC instant. That authenticator is separate from the history types, which normalize with `ToUniversalTime`. |
| EH01 | medium | defer | Same empty-spool readiness hole as BH08: a null component read becomes revision 0, and readiness can succeed when that empty digest is attested. |
| EH02 | medium | defer | `record.ObservedAt + RecoveryHorizon` can throw `ArgumentOutOfRangeException` near `DateTimeOffset.MaxValue`. `DrainAsync` catches it and returns, so later records in that call are not acknowledged. Spool work. |
| EH03 | medium | defer | `Capture` accepts an `ObservedAt` later than now. The horizon check `now < ObservedAt + horizon` then stays true, so automatic append remains open. Spool work. |
| EH04 | medium | defer | `SourceStream` builds an `AggregateIdentity` from the routing tenant. `Text` does not apply that identity's regex, so an illegal tenant throws out of `Exact` and the drain catch stops the batch. Spool work. |
| EH05 | medium | defer | `Text` calls `UTF8Encoding(false, true).GetByteCount` and does not catch `EncoderFallbackException`. `IntentDigest` therefore throws on an unpaired surrogate. Spool work. |
| EH06 | medium | defer | Same fresh-retain path as BH03: `requireCurrent` is false while `recoveringOriginal` is false, so a rotated non-revoked verifier can still save the in-flight signature. |
| EH07 | false | reject | The history contracts do not construct or register `ReplicatedSecurityObservationSpool`. `AddPlatformCustody` is unchanged, and the assembly test still requires no `IIdentityHistoryCustody` implementation. |
| VG01 | medium | defer | Pre-verified. No fixture sets `IsRevoked` true while `IsCurrentNonRevoked` stays true, so deleting the new revocation clause leaves the signing tests green and fresh issuance can proceed. Pre-existing signing work. |
| VG02 | medium | defer | Pre-verified. No test rotates trust to non-current and non-revoked inside a successful `SignAsync`. The suite does not pin whether that in-flight signature is stored. Pre-existing signing work. |
| VG03 | medium | defer | Pre-verified. Authenticator tests never pass a null or expired signing profile, so removing both `IsValid` checks still leaves those tests green. Separate authenticator work. |
| VG04 | medium | defer | Pre-verified. The withdrawal test's second grant read returns null, so deleting `currentGrant != grant` stays green while a replaced still-valid grant can be issued. Separate authenticator work. |
| VG05 | medium | defer | Pre-verified. Spool tests keep `ValidUntil` two days ahead, so removing the expiry comparison stays green and an expired target can still observe. Spool work. |
| VG06 | medium | defer | Pre-verified. No test makes `RecordRevisionAsync` return false for the revision about to be written, so dropping that refusal still lets `ObserveAsync` store state. Spool work. |

## Verification

**Commands:**
- `dotnet test tests/Hexalith.Platform.Custody.Tests/Hexalith.Platform.Custody.Tests.csproj --filter IdentityHistoryCustodyPolicyTests -p:UseHexalithProjectReferences=true -p:HexalithEventStoreRoot=references/Hexalith.EventStore` -- expected: pass, none skipped.
