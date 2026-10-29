---
title: 'EXT-PROTECTION-1 production engine, erasure-root custody and owner verifier'
type: 'feature'
created: '2026-10-19'
status: 'in-progress'
baseline_commit: '231b0f2b57e96984a919ac1cdce97cc36df18542'
eventstore_baseline_commit: 'a62241679854fa9c359c0f552114a42ca0f6a638'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/../agents/_bmad-output/planning-artifacts/external-dependency-register.md'
  - '{project-root}/../eventstore/docs/implementation/ext-protection-1-owner-acceptance.md'
  - '{project-root}/docs/implementation/ext-secrets-1-dek-provider-decision-2026-10-18.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EXT-PROTECTION-1 is `Uncommitted`, which blocks Agents Story 5.4 owner qualification (OQ-5.4-1 phase 0). EventStore has the hooks, engine, codecs and consumption/registry actors, but nothing implements key custody, bindings, the atomic manifest, nonrollback authority or a Live verifier pass path (gaps G1–G7). Ordinary OpenBao Raft restore revives deleted keys (`ObservedRevival`).

**Approach:** Build a Platform-owned erasure-root custody ("ERC") using per-tenant epoch keys. All surviving keys move to the next epoch, then the old epoch is destroyed in one native operation. Bind that custody and complete the authorities behind EventStore's existing ports in `Hexalith.Platform.EventStoreHost`. Complete the Live verifier with Platform role programs, then return a reviewed acceptance packet: proposed immutable targets, integration date, executable command and exact missing runtime inputs.

**Decisions (owner, 2026-10-19, "Adopt all"):** Use custody class **A1**: a dedicated in-cluster OpenBao 2.6.2 ERC in its own namespace. It is never snapshotted and is excluded from the daily snapshot job, Story 4.0 off-node copies and Velero; no copy of it exists. Erasure is final at the trim. Losing the ERC volume makes every protected payload `Erased`, and that consequence is accepted. The same ERC holds the tenant head: holds, erasure ordering, nonrollback anchors and the index of erased identities. The design keeps a durability-policy seam (`None` now) so A2 can be added later. The full spec is kept as one delivery.

## Boundaries & Constraints

**Always:** The register's EXT-PROTECTION-1 contract including v21/v23 is authoritative; this spec cannot narrow it. Preserve tenant isolation, hold pins, all-or-none manifest destruction, exact retry/lookup, the compromise/re-attestation protocol, restore safety and every existing fail-closed path. Reuse EventStore ports/actors/codecs and Platform custody contracts (`CustodyKeyRegistration`, `CustodyKeyWrapCore` AAD, lifecycle statuses, signing/trust/registrar adapters). Absent custody or authority → typed refusal, zero effects. One C# type per file. Debug project-reference builds with `-nodeReuse:false`; test DLLs run directly. Preserve unrelated changes made by concurrent sessions.

**Never:** Edit the Agents register or Agents code; run the Live owner command, OQ-5.4-1 or any Agents consumer; install or provision anything in the cluster; touch production OpenBao data; declare any record Committed/Available; propose a target that lacks the full implementation and verifier; use no-op/self-reporting engines, synthetic keys or HMAC custody as production substitutes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Seal/replay | Interaction event or directory outbox with explicit alias | Only `ProtectedContent` sealed under the target root; trusted replay equals input; broker/read models stay sealed | Missing custody → `PayloadProtectionUnavailable` |
| Manifest erase | Exact sorted 1..1000 same-tenant targets, valid reservation | Survivors moved to the successor epoch; one native trim; ordered receipt vector; typed `Erased` replay | Pin/unknown hold → zero effects; changed digest → conflict |
| Loss/restore | Lost response, crash, restored EventStore state, leaked old ciphertext | Exact original lookup; old ciphertext cannot decrypt; no root reissue | ERC unreachable → Unknown, never NotStarted |
| v23 race | Block vs reserve, compromise vs activation | Exactly one winner at the protection owner | Pending/unknown block → no effect |

</frozen-after-approval>

## Code Map

- `../eventstore/src/Hexalith.EventStore.PayloadProtection/IInteractionOccurrenceKeyProvider.cs`, `IInteractionProtectionBindingProvider.cs` — ports to implement (compiled into Server). Engine `InteractionPayloadProtectionService` exists.
- `../eventstore/src/Hexalith.EventStore.Server/Security/IAtomicDeletionManifestProvider.cs`, `IDeletionConsumptionAuthority.cs`, `IInteractionOccurrenceAuthority.cs`, `PublishedTrustDeletionConsumptionAuthority.cs` (six methods deny by design), `DeletionConsumptionActor.cs`, `InteractionOccurrenceRegistryActor.cs`, `Dapr*` proxies — complete the authorities and keep the actor protocols.
- `../eventstore/src/Hexalith.EventStore.Contracts/Security/IAnchoredStateTransitionAuthority.cs` — nonrollback anchor (Admit/Recover/Record/Verify) to back with the ERC head.
- `../eventstore/src/Hexalith.EventStore.Server/Configuration/InteractionPayloadProtectionServiceCollectionExtensions.cs` — opt-in currently leaves engine dependencies unresolvable; add a production overload that accepts host providers.
- `../eventstore/tools/ext_protection_1.py`, `tools/ext-protection-1/plan.json` (41 cases), `tools/tests/test_ext_protection_1*.py` — the Live harness (profile v2, roles, `accepted_record()`); add the Platform pin and the role-program contract.
- `src/Hexalith.Platform.Custody/` — `ICustodyKeyLifecycleProvider/Authority`, `CustodyKeyLifecycleActor`, internal `CustodyKeyWrapCore`, `DeletionBatchExecutionCoordinator`, signing/trust/registrar adapters, `Fr34*` gate. No production provider exists.
- `src/Hexalith.Platform.EventStoreHost/Program.cs` — the deployed `eventstore` container; composition point.
- `src/Hexalith.Platform.Secrets.Qualification.{Executor,Observer,Enrollment}` — role-program pattern to reuse.
- `eng/ext-secrets-1/probe_raft_non_revival.py` — disposable-container pattern for narrow OpenBao integration tests.
- Verified 2026-10-19 on OpenBao 2.6.2 (digest `11fd73a2…`): derived aes256-gcm96 transit keys with `context` support batch rewrap without plaintext export. Wrong context fails authentication. After rewrap, `min_*_version` + `trim` removes old versions and refuses older ciphertext, and the trim cannot be lowered back. Rewrap with `associated_data` fails, so use `context`.

## Tasks & Acceptance

**Execution:**
- [ ] `src/Hexalith.Platform.Protection/` (new library, references EventStore Server + Custody) -- ERC client (transit + KV v2 CAS tenant head), target registry, wrap/unwrap/pin/unpin, epoch-advance manifest destroy with exact lookup, nonrollback anchor, root-reissue index; implement the EventStore ports and `ICustodyKeyLifecycleProvider/Authority` -- G1–G3.
- [ ] `../eventstore/src/Hexalith.EventStore.Server/{Security,Configuration}/` -- complete the authority methods over the injected anchor/guard/trust/registrar; add the production composition overload -- G2/G4.
- [ ] `src/Hexalith.Platform.EventStoreHost/` -- production protection binding behind explicit configuration; fail closed when incomplete.
- [ ] `src/Hexalith.Platform.Protection.Qualification.{Executor,Observer,Enrollment}/` + `../eventstore/tools/` -- role programs and the Live pass path for all 41 cases; command pins both commits -- G5/G6.
- [x] `deploy/erasure-root/` -- ERC manifests, policies and bootstrap script, prepared but not applied.
- [ ] Tests: component tests for each port; narrow integration against a disposable OpenBao 2.6.2 (non-revival with leaked old ciphertext, 1000-target all-or-none, pin race, lost response, cross-tenant context); offline verifier tests.
- [ ] `../eventstore/docs/implementation/ext-protection-1-owner-acceptance.md` -- clause map, evidence, proposed record (targets, date, command), exact missing runtime inputs -- G7.

**Acceptance Criteria:**
- Given installed inputs, when the proposed command runs from the Agents root, then every case has a real pass path; given any absent input, it refuses with `DependencyNotAvailable` and zero effects.
- Given a destroyed manifest, when any retained old ciphertext or restored EventStore state is replayed, then content is `Erased` and no root is reissued.
- Given the delivery, then Agents files, the register and cluster state are unchanged and no Live/cohort run occurred.

## Implementation Notes

### 2026-10-29 component implementation and acceptance audit

Implemented native transit/KV-CAS transport, tenant-head custody, random-root wrapping, holders, pins, original-manifest recovery, epoch trim, erasure index, lifecycle/owner transition adapters, explicit EventStore provider composition and dual-target verifier pins. Prepared deployment files use Velero exclusion labels and require the independently qualified `erasure-root-no-backup` storage class; no cluster resources were applied.

The remaining unchecked tasks are not complete. Concrete private authorization, occurrence/writer binding, production host composition and the three real qualification role programs are still source work. The owner command therefore lacks the required real 41-case pass path. The full-copy guarantee in the referenced custody decision's acceptance criterion 6 is unproven for historical Raft/storage/memory/operator copies; the implemented live-native trim does not establish it. Required native OpenBao 2.6.2 integration could not run because the exact image/binary is absent locally. No complete target is proposed.

Verification: 29 final focused .NET tests passed; 128 broader component tests passed before the final negative-input guard; 67 offline verifier tests passed. Debug/project-reference builds reported zero warnings/errors. Counts are not additive. Source mode retained 41 cases and `liveExecuted:false`. Native, Live, cohort and Agents execution did not occur. This implementation did not edit Agents/register files; unrelated concurrent changes in the Agents checkout were preserved.

Matrix audit: manifest, loss/recovery and isolation have passing component coverage, but native evidence is missing. End-to-end seal/replay and complete v23 ERC-backed block/activation races lack the new production composition and real qualification roles. Acceptance criteria 1 and 2 are not established. The workflow remains at step 3; it has not advanced to independent review or completion.

See the [delivery note](../../docs/implementation/ext-protection-1-erc-component-delivery-2026-10-29.md) and [verification record](../../docs/implementation/ext-protection-1-erc-components-2026-10-29/verification.json) for evidence and remaining inputs. The frozen approved intent above is unchanged.
