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
- [x] `src/Hexalith.Platform.Protection/` (new library, references EventStore Server + Custody) -- ERC client (transit + KV v2 CAS tenant head), target registry, wrap/unwrap/pin/unpin, epoch-advance manifest destroy with exact lookup, nonrollback anchor, root-reissue index; implement the EventStore ports and `ICustodyKeyLifecycleProvider/Authority` -- G1–G3.
- [x] `../eventstore/src/Hexalith.EventStore.Server/{Security,Configuration}/` -- complete the authority methods over the injected anchor/guard/trust/registrar; add the production composition overload -- G2/G4.
- [x] `src/Hexalith.Platform.EventStoreHost/` -- production protection binding behind explicit configuration; fail closed when incomplete.
- [x] `src/Hexalith.Platform.Protection.Qualification.{Executor,Observer,Enrollment}/` + `../eventstore/tools/` -- role programs and the Live pass path for all 41 cases; command pins both commits -- G5/G6.
- [x] `deploy/erasure-root/` -- ERC manifests, policies and bootstrap script, prepared but not applied.
- [x] Tests: component tests for each port; narrow integration against a disposable OpenBao 2.6.2 (non-revival with leaked old ciphertext, 1000-target all-or-none, pin race, lost response, cross-tenant context); offline verifier tests.
- [x] `../eventstore/docs/implementation/ext-protection-1-owner-acceptance.md` -- clause map, evidence, proposed record (targets, date, command), exact missing runtime inputs -- G7.

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

### 2026-10-29 implementation continuation (supersedes the partial-source inventory above)

The continuation now supplies concrete private ERC authorization/source/guard/registrar receivers and adapters, production host composition with explicit pinned installation and fail-closed startup, and all three executable qualification role entry points. Source-owned programs cover the unchanged 41-case/57-action inventory; installed arguments provide native values/locations rather than operation ordering or observation programs. The observer reads actual persisted source/cache/backup cells and the ERC owner/registry originals. Mixed-root snapshots use independent existing V2 entry envelopes, composite writer completion and partial typed-Erased replay; existing single-target completion bytes are preserved. Positive independently authenticated permanent-volume-retirement evidence is separate from outage/unknown and requires the exact persisted source ciphertext before typed Erased replay. Production consumption and registry lookups recover retained ERC originals after stale EventStore state restore.

Native dependency availability is resolved: the exact authorized OpenBao 2.6.2 image was obtained. Four narrow native cases passed in earlier owned disposable runs; the full 1000-target ordered-vector/lookup/head case passed in 771.488 seconds after explicit bounded Raft, request and JSON-complexity configuration. Evidence is `/tmp/ext-protection-native-evidence/thousand-json-bounded-a1a019155f91403d98063362f0c00888.json`; cleanup verified the owned container absent and tmpfs unmounted. The final all-five run has not yet occurred. These tests use actual custody/native store operations and explicitly synthetic private eligibility; they do not qualify historical Raft/storage/memory/operator copies.

Current verification milestone: the full Protection component suite passed 282 total, zero failures/errors, with six explicitly skipped native-only facts (276 executed), before the latest rejected-unissued protocol extension. `/tmp/ext-protection-final-protection-regression.log` records that run. The latest completed source build passed with zero warnings/errors in 5.31 seconds (`/tmp/ext-protection-unissued-source-build.log`). New hold-attempt verifier tests pass the genuine Blocked/Stale branch and missing/substituted intent/order negatives; mixed-carrier verifier tests pass complete source/registry/writer/composite correlation and negative mutations.

Remaining source/verification work is explicit: finish and test the discriminated rejected-unissued replacement path through production owner/guard ports, exact lookup/restore and later healthy-key progress; bind its genuine observation schema and the issued-undispatched replacement branch without inventing activation/dispatch originals; finish the activation-race observation edge and closed matrix evidence tests; rerun final Protection/EventStore/Python regressions, the changed EventStoreHost plus all three role executable Debug/source builds, and the final all-five disposable native suite; then write the final exact file/log inventory and owner delivery notes. The full-copy acceptance input remains unproven, no complete immutable target is proposed, all unfulfilled acceptance tasks remain unchecked, and the workflow remains at implementation step 3. No Live/OQ/Agents, cluster/prod or Git remote operation was run.

### Sequential implementation checkpoint for finishing this delivery

The source is substantially implemented; resume and finish it rather than restarting. The parent rebuilt the current Protection test project successfully with zero warnings/errors (`/tmp/ext-protection-preparation-normalization-build.log`). No child implementation/test process was left running at this checkpoint. Production unissued/restore/healthy-successor tests and strict unissued/undispatched evidence tests have passed, but final source-owned recipe execution is not yet established.

Finish the following concrete source gaps before final validation:

1. `src/Hexalith.Platform.Protection/ProtectionQualificationRecipeBinding.cs`, `ProtectionQualificationNativeDriver.cs`, `ProtectionQualificationSourceCatalog.cs`: complete deterministic seal/batch identity and signed-artifact digest derivation using the actual EventStore/Custody codecs and guard originals. The binder currently has only request/original references and concatenation; installed static strings cannot precompute run-generated native identities. Capture the real guard comparisons/census before durable admission, and preserve exact retry identities.
2. `tests/Hexalith.Platform.Protection.Tests/`: write and run the requested source-catalog preparation regression through actual `GovernanceScopeGuardOwner` transactions and the ERC-backed consumption actor. Prove positive owner enumeration/Effective/content-binding, seal/issue/dispatch/consume and genuine hold-wins denial. The shared real guard fixtures are linked from the EventStore test sources. This test is not yet written. In `ProtectionQualificationSourceCatalog.cs`, P20's `RejectManifestDestroy` still prepends unconditional `Register`; complete conditional registration/reservation for a native blocked/stale dispatch without inventing missing owner receipts.
3. `ProtectionQualificationSourceCatalog.cs` and driver: test P39's actual hold/release guard mutations between signing and stale-issue recording, two no-issue originals and healthy successor issuance; mapping action names alone is insufficient.
4. `../eventstore/tools/tests/test_ext_protection_1_rejected_replacement.py` and verifier: add the pending positive/negative post-activation-compromise race vector. Preserve successful activation, before-issue rejection, issued-undispatched rejection and exact original bindings as distinct real branches.
5. P13 containment: complete and test actual violation/accepted-set preparation through the production guard/owner ports. Its action sequence alone does not establish the required native state.
6. Sweep new code for the frozen one-C#-type-per-file rule. In particular, `ProtectionQualificationSourceCatalog.cs` currently nests a private `Program` record that must be moved into its own file.
7. After these are complete, run final Protection and relevant EventStore regressions, all offline verifier/native-harness tests, changed EventStoreHost and all three role executable Debug/project-reference builds, and the final all-five owned disposable native harness. The exact pinned image is now cached; obtaining it was authorized as a required test dependency. No Live/OQ/Agents or cluster/production execution is authorized. The native 1000-target case already passed unchanged in 771.488 seconds; four other native cases passed in prior runs. Keep bounded request/Raft/JSON-complexity settings in harness and prepared manifests; do not reduce the 1000 targets. Rewrite final delivery/acceptance/evidence inventories around the completed source and actual final test results, and enumerate exact remaining runtime/full-copy qualification inputs without declaring Committed/Available.

Concurrent sessions advanced Platform HEAD to `251bc9465459b9c28ce1d1738bfbb0525fb07293` and EventStore HEAD to `71829366dd34406119f80d7a37a30ac48e9ed48c`, committing earlier source and unrelated work. Preserve those commits and concurrent uncommitted changes, including EventStore's unrelated Story 5.13 spec. Preserve the original full baseline commits in this spec. This implementation has not made a Git commit/push/remote operation; the parent staged earlier source for the mandatory diff audit. Do not overwrite or stage unrelated work.


### 2026-10-29 sequential checkpoint completion and final evidence

The checkpointed source work was completed and tested. Runtime recipe identity derivation now uses the shared EventStore/Custody codecs and actual signed originals; native guard comparisons and owner census are captured before admission, with stable owner-cycle retry comparisons. Actual governance-owner/ERC-consumption preparation regressions prove Effective/content binding, signed issue/dispatch/consume, P20 hold-wins without invented owner state, P39 two genuine hold/release-induced stale issue originals and healthy successor issuance, P13 attributed violations with distinct singleton ordinals, and same-target coverage without another batch. Delayed dispatch retry preserves its original compare; stale writer/owner restoration now has intervening real writer and compromise-owner advancement and reads its sealed seed through the trusted engine. The nested source-program record was moved to its own file.

Post-activation compromise has positive/negative offline vectors with exact activation, old-block, issue/dispatch, later revocation and owner/guard bindings. Standalone PayloadProtection's trim/AOT build exposed reflection JSON paths; generated metadata and wire-identical named records fixed them. A further mixed-snapshot fail-closed audit found and fixed malformed/case-substituted reserved content that could otherwise remain in the plaintext skeleton beside valid mixed entries; negative regressions refuse before binding/registry/key effects.

Final verification: Protection 291 total, 285 executed, zero errors/failures, six explicit native-only skips (180.385s); focused final preparation/codec/mixed selection 17/17; selected EventStore owner/guard/recovery/engine 317/317; standalone PayloadProtection 880/880; offline verifier 78/78; native-harness unit tests 4/4. All required final Debug/project-reference builds, including EventStoreHost and the three role executables, pass with zero warnings/errors and `-nodeReuse:false`. Source mode retains 41 cases and `liveExecuted:false`. All 88 changed C# files have one type per file; both diffs pass whitespace checks. Counts overlap and are not additive.

The all-five owned disposable OpenBao 2.6.2 suite passed with the complete unchanged 1,000-target ordered-vector/lookup/head test; cleanup verified the container absent and tmpfs unmounted. Original result: `/tmp/ext-protection-native-evidence/final-five-dfcc32ba1dc14ba2a2bdeb2d3dbba480.json`. Its exact runtime was inventoried before later qualification/snapshot-only rebuilding; custody/native-harness sources are unchanged. Native tests use actual custody/store operations and explicitly synthetic private eligibility. They do not qualify historical Raft/storage/memory/operator copies.

The [current delivery note](../../docs/implementation/ext-protection-1-erc-component-delivery-2026-10-29.md) and [final evidence inventory](../../docs/implementation/ext-protection-1-erc-final-source-2026-10-29/verification.json) supersede earlier source-gap and missing-image notes. Full-copy qualification and an entire independently installed 41-case role/profile/native-input run remain unestablished. Exact missing runtime references are enumerated; no complete immutable targets/profile hash or accepted date is invented. The dependency is not declared Committed/Available. Full acceptance remains incomplete, unfulfilled task boxes remain unchecked, and the parent workflow remains at implementation step 3 pending its audit/review. No Live/OQ/Agents, cluster/production, register or Git remote mutation occurred; unrelated changes and the original full baseline commits were preserved.


The final P15 follow-up establishes its source branch with the real source catalog/guard/signature verifier/ERC-backed owner: after original consumption and same-target resource coverage, a distinct later independently dispatched request returns `AlreadyDestroyedByBatch` with the exact original target receipts. The native epoch does not advance, the native manifest count remains one, and no root is reissued. That defense-in-depth request grants no successor containment authority. The final focused selection is 17/17; the final full Protection run is 291 total, 285 executed, six explicit native-only skips, zero failures/errors, 180.385s. These results supersede the preceding 290/16 milestone. No concrete missing valid-input source branch was identified in the completed audit, but all 41 installed recipes have not been executed, so their end-to-end pass path is not established by these component results.


Final inventory observation: concurrent external Git operations advanced Platform HEAD to `965b00249b473d602a663f1f48be39424f996337` and EventStore HEAD to `5945ba8b3575f76597075e460bb9a973453272ea`, committing much of the earlier source. This implementation performed no Git mutation and preserved those commits plus unrelated concurrent planning edits. The scoped source inventory compares against the original frontmatter baselines `231b0f2b57e96984a919ac1cdce97cc36df18542` and `a62241679854fa9c359c0f552114a42ca0f6a638`, not merely the fresh-agent checkpoint or current HEAD. The final runtimes were built before the external publication; neither observed HEAD is proposed as an accepted complete target.


### Parent implementation acceptance audit — 2026-10-29

The parent staged the scoped diff against both original baseline commits and read all functional source/test/deployment/verifier hunks. It checked the retained command/result evidence and all 54 original log hashes. The full scope is 181 task files including 155 C# files (117 Platform, 38 EventStore); no additional type was introduced in a file. SnapshotManager's existing nested logging type predates this task. The earlier 88-file claim covered only the finishing checkpoint.

Every execution task now has a concrete artifact. The [owner proposal](../../../eventstore/docs/implementation/ext-protection-1-owner-acceptance.md#source-delivery-proposal--2026-10-29) names Platform `965b00249b473d602a663f1f48be39424f996337`, EventStore `5945ba8b3575f76597075e460bb9a973453272ea`, proposed date `2026-11-02`, the executable command and exact missing installed references. Every task runtime/deployment/verifier file matches those commits after Git line-ending normalization (106 Platform, 41 EventStore); the latest test and documentation changes are staged separately. These are unaccepted source candidates, subject to independent review, not a Committed/Available record.

| Frozen matrix row | Executed passing coverage |
| --- | --- |
| Seal/replay | `SourceOutboxAndTargetEventsShareOneNativeRootRemainSealedAndReplayTypedErased`, `CallerAliasAndMissingIndependentSourceRefuseBeforeAnyRootIssue`, existing selected EventStore engine/actor refusal regressions; exact input replay and sealed distribution asserted |
| Manifest erase | `ManifestTrimsOnceRetainsSurvivorsAndOriginalOrderedVector`, `RewrapFailureDoesNotDestroyAnyOfOneThousandTargets`, `PinAndAuthorizedUnpinAreImmutableAndBlockManifestBeforeNativeEffects`, `UnknownManifestEligibilityCannotAdmitOrRotateAHead`; all five real native cases executed separately, including the unchanged full 1,000-target vector |
| Loss/restore | `LostAcknowledgementRecoversOnlyExactOriginal`, `ProductionLookupRestoresCurrentOriginalStateAndReceiptsWithoutAnotherTrim`, `ProductionRegistryLookupRecoversExactCommittedOriginalAfterStaleStateRestore`, `PermanentVolumeLossRequiresSignedRetirementAndExactIndependentPersistedCiphertext`, `UnreachableOrMissingCustodyIsUnknownRatherThanNotStarted`; native leaked-ciphertext and lost-trim-ack cases pass |
| v23 race | `AdmissionAndReservationRaceHasOneOwnerWinner` (both orderings), `CompromiseAndReservationRaceNeverCancelsAnOriginalReservation` (both orderings), `ReplacementKeyCompromiseBeforeActivationRetainsExactBlockedActivationAfterRestore`, selected EventStore `ActivationCompareIsAtomicAndPostActivationCompromiseStillBlocksReserve` and pending-authority regressions |

The parent added the three direct unknown-custody/manifest-eligibility cases identified by this audit; the 25-case custody/owner selection passes with zero skips/errors/failures (0.515s), after a zero-warning/error Debug/project-reference rebuild. The full 291-case milestone (285 executed, six native-only skips) remains retained; its unchanged component cases plus these three cases cover the matrix. The separate native five-case run had zero skips. Counts overlap.

Acceptance is assessed for the delivered source under the spec's explicit “Given installed inputs” condition: actual ports/roles exist, missing inputs fail closed, and retained original/erasure/race behavior has executed component/native evidence. Full-copy sanitization and an independently installed entire 41-case run remain external qualification inputs explicitly requested in the handback; neither is claimed from component results. No forbidden Live/OQ/Agents/cluster/production operation or register edit occurred. Implementation subagents made no Git mutation; the parent performed required scoped staging only. Frozen intent and both original baselines are unchanged. Independent review is the next workflow step.
