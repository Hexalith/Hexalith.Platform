# Authenticated EventStore owner handback

`CustodyOwnerAuthority` is a public, compiled Dapr-only implementation of `ICustodyOwnerStateAuthority` and EventStore's existing `IAnchoredStateTransitionAuthority`. It obtains each distinct admit/record/recover/verify/read permission through `ICustodyOwnerCallSource`, sends the exact private-operation/control statements, and verifies P-256 signed challenge-bound owner evidence. Missing source/installed trust returns false; construction supplies no authority or DI activation. The source is `src/Hexalith.Platform.Secrets.Runtime/CustodyOwnerAuthority.cs`. `CustodyBindingOwnerAdapter.cs` supplies the internal exact-phase bridge. Application code takes no AWS dependency or native IDs.

The external authorizer must authenticate the original private method and independently current issuer/audience/role/revocation; verify the source owners' full guard/protection originals; enforce exact logical operation permission; and issue the complete encoded request/short-lived statements. It must be independently installed and hash-bound. `ICustodyOwnerCallSource` is an installation port, not an implemented enrollment/credential issuer or permission to sign from restored app state. No such installation is present.

## Installation and genesis

For canonical tenant `t`, existing actor scope is exactly:

```
t:protection:deletion-consumption-v23|deletion-consumption-v23
```

`CustodyProtectionOwnerIdentity.Scope(t)` uses the actual `AggregateIdentity` type. Its `GenesisBytes(t)` matches the current EventStore `DeletionConsumptionLedger(t,0,0,[],[],[])` default serializer property order and bytes:

```json
{"TenantId":"t","Revision":0,"KeyBlockSetRevision":0,"Batches":[],"Revocations":[],"Operations":[]}
```

The three exact EventStore record sources are retained under the component test's `OwnerConformance/` directory and compared at runtime against this projection. Their hashes and inspected sibling revision are in `source-inventory.json`. These are conformance copies, not an EventStore edit or installed fixture.

The independent enrollment role must install `AuthorityEnrollmentV1` and a tenant checkpoint with this exact scope/revision/digest and no admitted transition, pinning native table ID/ARN, archive owner/version, independent public trust and artifact profile. It must authenticate explicit first installation against the live owner inventory and fence any old owner writer. It must never enroll merely because `TryGetStateAsync` is absent. The executable component has no create/enroll/restore/bootstrap path. An absent owner scope, missing table, changed table ID, archive version/profile/trust mismatch or lost authority refuses. First-release replacement/rollover is unsupported.

## Concrete EventStore integration changes to make in its owning checkout

This checkout intentionally does not edit the independent EventStore repository. Its existing `PublishedTrustDeletionConsumptionAuthority.ValidateStateAsync` still rejects revision zero and compares the independent owner revision to the governance-guard revision. That source remains an integration blocker.

1. Inject a tenant-scoped `ICustodyOwnerStateAuthority` (with the exact actor scope and independently enrolled public references) alongside the existing guard, capability trust and revocation authority. Keep existing dispatch, registrar, block/replacement/activation verification.
2. `ValidateStateAsync(tenant, revision, digest)` must allow nonnegative owner revisions and invoke `owner.ValidateStateAsync(revision,digest)` for that tenant. Remove the comparison to guard revision and the synthetic `Proof("state",...)` transition. Revision zero succeeds only when the independently surviving enrolled scope still selects the exact genesis digest. A restored local revision-zero or older ledger fails after any owner advance.
3. `AuthorizeOperationAsync(tenant,identity,operation)` must invoke `owner.AuthorizeOperationAsync(identity,operation)` using the existing private operation identity. The adapter derives an exact domain-separated operation identity under `custody-owner-operation-v1`; its independently issued private authorization binds the whole request. Do not translate a read-only guard snapshot or app identity into this permission.
4. Forward the actor's **actual** `AnchoredStateTransition` to Admit/Record/Recover/Verify. Its `ScopeId`, owner predecessor/target revisions/digests and target bytes stay unchanged. Do not synthesize revision-proof transitions. `RecordRevisionAsync` must verify the actual retained owner target through the owner current-state port; it is not permission to mint a new transition without admitted target bytes.
5. Install independently current exact recovery permission. Admit retains the complete original without advancing the owner; record compares predecessor and advances; recover uses the exact same retained admission without physical replay; verify/lookup never mutate. The tenant custody head revision in the envelope is separate from both owner and guard revisions. Later owner transitions retain previous immutable admissions and require the current owner predecessor.

The public client is usable by that integration after its private authorizer and public trust are installed. Local tests execute its actual pinned Dapr SDK framing against the real semantic provider, including exact EventStore genesis, consecutive owner transitions, stale restored genesis denial and signature substitution. This is source interoperability; no EventStore production adapter, owner activation, installed issuer, real guard reservation issuer, or physical qualification is claimed.

## Hold/dispatch handoff

A successful component `pin` requires a current signed `HoldReservation` with exact HoldId, singleton target digest, registered guard revision/receipt, reservation identity and state `RegisteredDispatchBlocked`. The guard/control owner must durably exclude dispatch until the exact custody pin outcome is reconciled; no timeout, local restore or missing acknowledgement may drop that reservation. A later post-dispatch hold cannot obtain this state. The custody head CAS serializes the actual pin with complete-manifest reservation; exact authorized release removes only its own pin. The signed control claim must be backed by this installed owner protocol. An ordinary fresh guard read, caller boolean or signature without the required durable reservation semantics cannot supply it. That cross-owner issuer/integration is still absent.
