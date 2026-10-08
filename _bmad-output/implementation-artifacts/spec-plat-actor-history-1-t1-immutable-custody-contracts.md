---
title: 'Validate exact actor-history custody contracts'
type: 'feature'
created: '2026-10-08'
status: 'in-progress'
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
- [ ] `src/Hexalith.Platform.Custody/IdentityHistoryCustodyOptions.cs` -- Add the approved policy, UTC deadline, exclusive read, and admission rejection.
- [ ] `src/Hexalith.Platform.Custody/IdentityHistoryCustodyUnit.cs` -- Add the party unit, evidence, admission revision, and derived expiry.
- [ ] `src/Hexalith.Platform.Custody/IdentityHistoryLifecycleRecord.cs` -- Add forward-only state, separate authority, generations, and receipt linkage.
- [ ] `src/Hexalith.Platform.Custody/IdentityHistoryCopyRecord.cs` -- Bind generation, class, location, owner, registration, and the original deadline.
- [ ] `src/Hexalith.Platform.Custody/IdentityHistoryDestructionReceipt.cs` -- Add the content-free original receipt.
- [ ] `tests/Hexalith.Platform.Custody.Tests/IdentityHistoryCustodyPolicyTests.cs` -- Cover the matrix, including absent history registration.

**Acceptance Criteria:**
- Given the contracts, when built and serialized, then scope, evidence, admission revision, and UTC expiry survive retry and JSON round-trip.
- Given `AddPlatformCustody`, when the provider is built, then `IIdentityHistoryCustody` is absent and no capability flag is set.
- Given Q1–Q7, F1, and F2, when this task ends, then they remain unresolved and no backend, host, or successor code was added.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet test tests/Hexalith.Platform.Custody.Tests/Hexalith.Platform.Custody.Tests.csproj --filter IdentityHistoryCustodyPolicyTests -p:UseHexalithProjectReferences=true -p:HexalithEventStoreRoot=references/Hexalith.EventStore` -- expected: pass, none skipped.
