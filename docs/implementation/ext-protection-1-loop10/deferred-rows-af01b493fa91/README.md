# EXT-PROTECTION-1 loop-10 deferred-row verification

Run `af01b493fa91`, 2026-10-31. This is the run authorized by the spec section **Authorized loop-10 deferred-row verification — 2026-10-31**. It is not a new review cycle.
- Status stays `in-progress` and the review counter stays 10.
- Both original baselines and the frozen intent are unchanged. The intent hash `96c592bd…` matches `../parent-audit.json`, and the Agents register hash `e29b7fc4…` is unchanged.
- `PrivateOwnerOperationDeadline`, the rejected rows and R7-BH-7 are untouched.
- No accepted target, profile, date or availability is proposed.
- No Live/OQ run, Agents consumer, cluster installation, production or native OpenBao operation, Git mutation or remote operation took place.

## 1. Verdicts

Each verdict comes from tracing the current source, not from the Review 11 text. A row is `real` when the harmful outcome it describes is reachable through current source. A row is `false` when a guard on the path blocks it. Every `false` verdict is backed by a passing test. Every `real` verdict has a regression test that failed on the unfixed source and passes after the fix. `verdicts.json` holds the per-test results before the fix, after it and in the final run.

| Row | Verdict | Why (file:line) | Backing test (before fix → after fix) |
|---|---|---|---|
| R11-BH-1 | false | The owner's `ReadBlockedReplacementAsync` returns the retained *operation* outcome: `ActivationBlockedByReplacementKeyCompromise`, with a receipt over the exact phase (`DeletionConsumptionActor.cs:314-317, 332`). It does not return the durable `ConsumptionBlocked` batch outcome. That stored outcome satisfies `UnissuedReplacementProof` (`GovernanceScopeGuardReducer.cs:443-463`). | Existing `RejectedUnissuedCandidateRetainsIssuedOrdinal…HealthyProgress`: the mirror commits with the stored outcome, and restore returns the identical outcome (Pass → Pass) |
| R11-BH-2 | **real** | Refusal-status half: false, because `Capture` accepts only `Blocked`/`Stale` (`DeletionConsumptionIdentity.cs:90`). Currentness half: real. The unissued branch accepted on repeated guard reads plus the journal, without the independent `VerifyCurrentBlockedReplacementAsync` that the issued branch requires (baseline `PublishedTrustDeletionConsumptionAuthority.cs:316-328` vs `:343`). The shipped Platform evidence journal performs the equivalent check (`ErasureRootAuthorityOperationHandler.cs:436-437`), so the gap was at the EventStore component contract. | New `UnissuedProofRequiresIndependentCurrentnessAndARefusalStatus` (**Fail → Pass**) |
| R11-BH-3 | false | The snapshot persisted before the failed re-validation is the exact, independently validated committed original. Every later read revalidates it before use (`InteractionOccurrenceRegistryActor.cs:103-114`). | New `RecoveredOriginalThatFailsPostPersistValidationStaysExactAndIsNeverServedUnvalidated` (Pass → Pass) |
| R11-BH-5 | false | The forwarder routes only to the single owner enrolled for the exact scope and returns that owner's result unchanged (`PublishedTrustDeletionConsumptionAuthority.cs:367-377`). Every consumer rejects a foreign `ScopeId` before it deserializes or persists anything (`RecoverableAnchoredState.cs:98`, `DeletionConsumptionActor.cs:360, 541`, `InteractionOccurrenceRegistryActor.cs:103`). | New `ForeignScopeOwnerOriginalsAreRefusedByEveryConsumerBeforePersistence`, including an exact-scope control (Pass → Pass) |
| R11-BH-6 | false | The journal client does not compare the response `TenantId`. The response is still bound to the exact transition by `Identity == ScopeId` and `RequestDigest` (`DaprErasureRootEvidenceJournal.cs:14`). The receiver derives the single tenant from the transition, requires `published-trust|kind|tenant|identity`, and requires the caller grant for that tenant (`ErasureRootAuthorityOperationHandler.cs:37, 467`). | Existing `ActualPublishedTrustProtocolScopeAuthenticatesItsExactTenantWithoutAcceptingALabelOverride` (Pass → Pass) |
| R11-BH-7 | false | Erased-original reads apply the owner, occurrence, key-reference and store checks (`ErasureRootOccurrenceBindings.cs:28-29`). The native skeleton read binds the caller's exact binding before it reads any writer proof (`ErasureRootNativeOccurrenceSource.cs:28`). | New `ErasedReadsRefuseEverySubstitutedSourceIdentity` (six substitutions, the native skeleton binding check, and a no-loss case that makes no call) (Pass → Pass) |
| R11-BH-8 | false | `UnwrapAsync` refuses a holder id that is live, completed or archived, and commits it through a head CAS (`ErasureRootCustody.cs:177-181`). At most one unwrap of a shared `erc-write` id can return a key, so the `TryAdd` loser branch (`ErasureRootOccurrenceKeyProvider.cs:64`) is unreachable and no loser commits a native holder. | New `ConcurrentUnwrapsOfOneSharedWriterHolderCommitExactlyOneNativeHolder` (8 concurrent unwraps, exactly 1 holder; retry after completion refused) (Pass → Pass) |
| R11-EC-1 | **real** | With `QualificationBindings` null, `VerifyLifecycle` and `VerifyReservedManifestAndCopyPolicy` dereferenced the bindings inside a non-short-circuit `&=` and threw `NullReferenceException` instead of denying (baseline `ErasureRootAuthorityOperationHandler.cs:250, 254`). | New `AbsentQualificationBindingsDenyWithoutThrowing` (2 cases) (**Fail (NullReferenceException) → Pass**) |
| R11-EC-3 | **real** | For a head mount that already existed, a configuration read without `data` (an empty object, null `data` or a 404) was treated as absent. The enrolled configuration was then written over the unverified object (baseline `bootstrap.py:215`). | New `PreexistingHeadConfigReadbackTests` (3 subtests) (**Fail: POST to `erc-heads/config` → Pass: step `head-config`, zero POSTs**) |
| R11-EC-6 | false | `Capture` rejects an unissued proof that carries a nonzero `CommittedIssuedGuardRevision` (`DeletionConsumptionIdentity.cs:65`). The authority therefore returns false before it consults the journal, and the reducer also requires zero (`GovernanceScopeGuardReducer.cs:447`). | New `UnissuedProofWithNonzeroIssuedRevisionIsRefusedBeforeTheJournal` (Pass → Pass) |
| R11-EC-7 | false | The synchronous production factory sets the `AsyncLocal` CA handoff. The first statement of the constructor, in the same flow, consumes and clears it, and concurrent flows each copy their own value. Validation callbacks capture the factory's local certificate (`OpenBaoErasureRootStore.cs:21, 53, 55, 61, 105-106`). | New `SequentialAndConcurrentStoresKeepOnlyTheirOwnPinnedCa` (sequential A, synthetic, B with disposal; 8 concurrent stores accept only their own CA) (Pass → Pass) |

### R11-BH-9 observation point

An internal `ResponseBufferObserver` was added (`OpenBaoErasureRootStore.cs:148-153`; calls at `:347, 350, 378, 386`). It is null by default. It makes no wire-format, public API, option or DI change, and it never alters behavior. The new tests check the success and failure paths in the style of the R10-BH-17 tests: each buffer is nonzero while in use and zero afterwards.

The observation found a completion gap. On success, `ReadKeyAsync` returned a `JsonDocument` over a *retained copy* of the key-metadata response, and that copy was never zeroed (baseline `:379-397`). `KeyMetadataResponseBuffers…(epoch-success | verify-trim-success)` failed in phase A on `read-key:retained`. Key metadata is now projected inside `ReadKeyAsync`, so no copy outlives the read and every buffer is zeroed in `finally`. Callers see the same results and exceptions. All 6 R11-BH-9 cases pass after the change.

## 2. Fixes (each limited to its defect)

The complete delta is in `source-delta.patch` (pre-change bytes in `baseline/`, post-change bytes in `after/`, hashes in `source-delta.json`).

- **R11-BH-2:** the unissued branch of `PublishedTrustDeletionConsumptionAuthority.VerifyBlockedReplacementAsync` now also requires `_revocations.VerifyCurrentBlockedReplacementAsync(captured, state.Revision, Digest(state))`, the same evidence the issued branch requires. The setup of the existing unissued test moved into a shared helper that calls `RetainCurrentGuard()` so the fixture supplies that evidence; its assertions are unchanged.
- **R11-EC-1:** `current.QualificationBindings!.TryGetValue(...)` is replaced by `current.QualificationBindings is { } … && ….TryGetValue(...)` at `ErasureRootAuthorityOperationHandler.cs:250, 254`.
- **R11-EC-3:** `bootstrap.py` `ensure_config` writes only when this invocation created the mount (`if newly_created and (data is None or native_defaults)`). On an existing mount, a missing or unreadable configuration is refused at step `head-config` before any write.
- **R11-BH-9 completion:** see above.

## 3. Execution

All builds are serialized Debug project-reference builds: `-nodeReuse:false -m:1 -p:UseHexalithProjectReferences=true -p:GeneratePackageOnBuild=false --no-incremental`. They use `HexalithEventStoreRoot=/home/administrator/projects/hexalith/eventstore`, physical `HexalithCommonsRoot=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Commons` (also set as the process variable) and `HexalithTenantsBasePath=/home/administrator/projects/hexalith/eventstore/references/Hexalith.Tenants/src`.

In each phase, every project output was fresh, with 0 warnings and 0 errors (`*/build-freshness.json`). The complete outputs were copied to a fresh external runtime (`<scratchpad>/ext-protection-loop10-deferred-rows-af01b493fa91/<phase>/runtime/`) and inventoried before execution: Platform 257 files, EventStore 692 files. Both were verified unchanged afterwards. The DLLs ran directly with `-class` selections. Each command in `*/commands.json` records its argv, cwd, recorded environment, UTC interval, exit code and log hash.

| Phase | Source | Bootstrap and prepared shell | Platform DLL | EventStore DLL |
|---|---|---|---|---|
| `phase-a-attempt1-compile-error` | tests only | 12 OK, 3 subtests fail (EC-3) | build error: xUnit1051 in a new test (`default` CancellationToken); retained unchanged, never relabelled | not built |
| `phase-a` (before fixes) | tests and observation point | 12 OK, **3 subtests fail (EC-3)** | 68 total, **4 failed** (EC-1 ×2, BH-9 retained ×2) | 153 total, **1 failed** (BH-2) |
| `phase-b` (after fixes) | fixed | 13 OK | 68/68 pass | 153/153 pass |
| `final` | fixed | 13 OK | **96/96 pass** | **370/370 pass** |

The final class sets are the 6 Platform and 5 EventStore classes from `../current-graph-314ddb04c56c/commands.json`, plus classes for the changed files:
- `ErasureRootLoop3ReceivingTests` (the changed handler; it also backs R11-BH-6);
- `DeletionConsumptionActorTests` and `DeletionConsumptionOriginalTests` (consumers of the changed authority).

The final run had zero failures, errors and skips. Raw notices are the existing redirect-test `ResourceWarning` and xUnit's deprecation notices for `-parallel` and `-xml`.

Bootstrap native integration and the five-case native suite were **not** run. Their passes belong to `source-gaps-7b5a4e938648` and do **not** cover the source changed here: `OpenBaoErasureRootStore.cs` (the trim and key-metadata read path), `ErasureRootAuthorityOperationHandler.cs`, `bootstrap.py` and `PublishedTrustDeletionConsumptionAuthority.cs`.

## 4. Concurrency and preservation

A concurrent session committed Platform `68b785b6` ("fix: story 2.8"), which included the earlier untracked `current-graph-314ddb04c56c` bundle. None of this run's task files were committed by it. EventStore HEAD is unchanged at `61e6fa85`. Both checkouts also hold unrelated concurrent work, listed in `git-after.json`. Task sources were byte-stable throughout each phase (`*/post-run-integrity.json`). All 346 pre-existing files under `ext-protection-1-loop10/` are byte-identical, and the tracked evidence matches HEAD (`evidence-preservation.json`).

## 5. Remaining source gaps and prerequisites

- **R7-BH-7 stays deferred:** generic `subprocess.run(capture_output=True)` with no stderr bound.
- **Native evidence:** the native results no longer cover the changed trim and key-metadata read path or the bootstrap configuration step. A fresh owned OpenBao 2.6.2 bootstrap integration and five-case run would be needed to restore native evidence for this source.
- **R11-BH-2 in production:** the unissued path now also calls the registrar's `VerifyCurrentBlockedReplacement`, which already handles unissued requests (`ErasureRootAuthorityOperationHandler.cs:387-399`). No Platform test drives the real authority's unissued branch end to end, because `ErasureRootUnissuedReplacementTests` substitutes the authority.
- **Possible gap near R11-BH-1 (not opened):** the unissued proof binds the exact guard revision by design, so any guard transition between the owner's reconcile and the guard mirror makes that mirror permanently unavailable. This was outside R11-BH-1's claim and was not verified.
- **Installed-qualification prerequisites** are unchanged from `../source-gaps-7b5a4e938648/README.md` and `../current-graph-314ddb04c56c/README.md`:
  - the accepted v3 profile and role-installation closures;
  - the dedicated CA bytes, consistent across the Secret, the installation and bootstrap;
  - the canonical tenant/key and genesis maps, with private grants and trust;
  - the qualified `erasure-root-no-backup` storage and all-copy, restore and retirement authorities;
  - the loop-7 signing-readiness backend;
  - the source, control and fault cells and the observer permission;
  - the Dapr 8 MiB allowances for legacy chunks;
  - cgroup v2 with kubelet `NoSwap`.
- **Not established:** the installed 41-case and physical-copy qualifications. Production destruction remains independently gated.

## Files

The scripts are `phase.py` and `package.py`. The phase directories are `phase-a-attempt1-compile-error/`, `phase-a/`, `phase-b/` and `final/`. They hold commands, raw `*.log.txt`, xUnit XML, runtime manifests, freshness, integrity and task-source hashes. The remaining evidence is `verdicts.json`, `verification.json`, `baseline/`, `after/`, `source-delta.patch`, `source-delta.json`, `git-after.json`, `evidence-preservation.json` and `artifact-inventory.json`.
