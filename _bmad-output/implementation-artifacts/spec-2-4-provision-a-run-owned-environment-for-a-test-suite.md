---
title: '2.4 Provision a run-owned environment for a test suite'
type: 'feature'
created: '2026-10-22'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '543ea182bd83c813609c7df0491d1419b7f1f99e'
builds_baseline_commit: '66b9136a0b223925e51177a51eb48852e2f12787'
story_key: '2-4-provision-a-run-owned-environment-for-a-test-suite'
context:
  - '_bmad-output/implementation-artifacts/epic-2-context.md'
  - 'docs/contracts/test-environment-v1.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Make `hexalith-module test` provision fresh real services for a declared suite or compatible batch, enforce readiness, and hand tests a validated Story 2.2 descriptor.

## Boundaries & Constraints

Reuse the active mapping, authoritative declaration closure, pinned catalog, isolated workspaces and Story 1.8 readiness/tasks. Preserve source/Debug and package/Release boundaries. Keep independent runs concurrent; refuse actual exclusive-resource conflicts with the blocking run ID. Never attach implicitly or reach hosted application data.

Startup defaults to 600 seconds from the environment request, with finite justified declaration overrides. Start execution only after readiness; batches have a finite combined bound and each entry's configured limit. Persist timing before launch; preserve immutable selection/source hashes and legacy evidence bindings. Adapt certificates and gates consistently.

Support one or repeated `--profile` values from the same manifest. Share one environment only when mode, graph and profile-specific resource/runtime requirements agree; execute entries sequentially. Refuse incompatibility before startup.

Supply absolute `HEXALITH_TEST_ENVIRONMENT_PATH` to a private, credential-free v1 descriptor. Bind real run/attempt/owner/environment identities, composition content, verified catalog commit/instance, synthetic isolation, observed endpoints, readiness and startup-task results. Validate before handoff. Keep unavailable proof `not-yet-populated`; validity grants no candidate acceptance.

Exclude attachment admission, lifecycle-policy redesign, CI orchestration, domain fixture migration, publication and pins. Preserve independent edits and applications; verification may stop only its owned resources.

## I/O & Edge-Case Matrix

| State | Behavior |
| --- | --- |
| Ready suite/compatible batch | Fresh environment; descriptor before child launch. |
| Invalid selection/incompatible batch | Refuse before allocating services. |
| Managed exclusive claim held | Fail promptly and name owner; independent allocations coexist. |
| Readiness/task failure or startup timeout | No test child; preserve bounded diagnostics and startup timing. |
| Missing catalog identity, invalid observation or descriptor | Refuse handoff; never invent values or fall back to legacy ABI. |

</frozen-after-approval>

## Code Map

Builds paths below are relative to `references/Hexalith.Builds/`. `R/` means `src/libraries/Hexalith.Builds.Tooling/Runtime/`; `T/` means `test/Hexalith.Builds.Module.Tests/`.

- `R/CompositionEngine.cs` enforces Story 1.8; `R/CompositionDeclarationClosure.cs` selects dependencies.
- `R/ModuleCommandExecutionService.cs` currently charges setup to execution. Keep `ModuleTestSelection` source/evidence bindings; add actual execution timing separately.
- AppHost `RunReadiness.cs` reports only EventStore/UI URLs; Platform server annotations supply ownership.
- `src/libraries/Hexalith.Builds.Tooling/Workspace/CatalogVersionMaterializer.cs` verifies the authoritative catalog snapshot/commit.

## Tasks & Acceptance

**Execution:**

- [x] `src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`, `R/ModuleCommandExecutionService.cs`, `R/ModuleTestSelection.cs` — accept suites/batches, validate compatibility and coordinate one fresh environment.
- [x] `R/CompositionStartupRequest.cs`, `R/ModuleTestExecutionBudget.cs`, `R/CompositionEngine.cs`, `R/CompositionRunPlan.cs`, `R/ModuleTestRunRecord.cs`, new `R/ModuleTestExecutionTiming.cs` — separate clocks; persist activation without renewing active timeouts.
- [x] New `R/CompositionResourceClaimStore.cs`, `R/CompositionPortAllocator.cs`, `R/CompositionRunStateStore.cs` — acquire exclusive runner claims atomically before launch; expose blocking ownership and release through existing ownership-limited teardown. Do not lock solely by manifest/composition.
- [x] `R/CompositionReadiness.cs`, `R/CompositionResourceReadiness.cs`, new `R/CompositionStartupTaskResult.cs`, `src/hosts/Hexalith.Builds.Module.AppHost/RunReadiness.cs`, `RunEndpoints.cs` there — capture ownership/endpoints and task results; never infer observed endpoints from proposed ports.
- [x] New `src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentDescriptorWriter.cs`, `R/NativeTestHandoff.cs`, `R/NativeTestExecutor.cs`, `R/CompositionSelectedTestGate.cs` — write/validate the descriptor and hand off its path; enforce actual execution deadlines and existing credential protections.
- [x] Platform `src/Hexalith.Platform.Composition/PlatformRetentionProfile.cs`, `tests/Hexalith.Platform.Composition.Tests/PlatformRetentionProfileTests.cs` — adapt pre-start certificate validity to the startup ceiling plus execution budget; retain actual gate expiry checks.
- [x] New `T/TestEnvironmentProvisioningTests.cs`, `T/CompositionResourceClaimStoreTests.cs`; existing `T/ModuleTestSelectionTests.cs`, `T/StartupCoordinationTests.cs`, `T/StartupReadinessTests.cs`, `T/NativeTestExecutorTests.cs` — cover every matrix row, descriptor reader acceptance, batch compatibility, delayed readiness preserving execution time, source drift and concurrent ownership.
- [x] New Platform `tests/Hexalith.Platform.Composition.Tests/PlatformTestEnvironmentProvisioningTests.cs` — gated real-service proof: declared suite consumes descriptor, writes/reads run-isolated state, and cannot read its peer's data.
- [x] Builds `README.md`, Platform `docs/contracts/test-environment-v1.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml` — document handoff, sync Story 2.4; retain `evidence/2-4/`.

**Acceptance Criteria:**

- Given declared suites, when the command runs, then readiness and required tasks pass before descriptor handoff and test execution.
- Given another run's conflicting claim, when starting without attach, then refusal names its owner without disturbing it.
- Given two fresh environments, when suites persist identical test keys, then each observes only its own synthetic data through its descriptor endpoints.
- Given a supported declared composition, when testing, then the v1 descriptor validates and `HXR003` never occurs; unsupported legacy ABI refusals remain.

## Implementation Notes

- Declared suites and compatible repeated profiles now share one freshly owned environment and execute sequentially. Selection and legacy evidence bindings stay immutable; actual execution timing is persisted separately after readiness and required tasks.
- Runner-managed TCP claims are acquired atomically, report the conflicting owner, and release only after ownership-limited teardown. Observations carry allocated endpoints, module ownership, readiness timestamps and actual required task results.
- The descriptor writer rechecks selected source, authoritative closure and verified catalog material, validates with the current Story 2.2 reader, and hands an absolute private credential-free path to native children. Unknown candidate/tool/artifact/suite proof stays unpopulated.
- Platform certificate validity covers the startup ceiling plus finite execution budgets. Selected source project builds use run-owned output directories to prevent shared output races.
- The gated technical fixture uses its real minimum dependency closure (Platform + EventStore and owned infrastructure), two concurrent suite commands, actual Redis persistence, descriptor-derived endpoints, and explicit same-ID cleanup. Native environment inheritance remains unchanged.


## Spec Change Log

- 2026-10-22: Implemented all tasks and moved to review. Frozen intent is unchanged. Retained the broader reference-domain startup failure independently from the passing minimum-closure provisioning proof.


## Review Triage Log

All three independent layers completed before triage. Each finding was assessed individually; duplicate outcomes are grouped only after the verdicts below. Corrections use existing contracts, deadlines, metadata and refusal paths, with no new public API or changed frozen intent.

| Finding | Verdict | Route and evidence |
| --- | --- | --- |
| Blind 1: self-owned secondary port conflict | medium | Patch. Child allocation claims before `AllocateSecondaryPorts` excludes persisted unbound ports; no parent-process port history protects the child. Claim only the accepted non-overlapping allocation. |
| Blind 2: earlier entry native reports disappear | medium | Patch. Per-entry `native.trx` is written but omitted from durable metadata copying before workspace deletion. Retain every inspected report. |
| Blind 3: first entry stopping bound reused | medium | Patch. Compatibility allows distinct stop limits, but final processing always uses the initial `selected`; use the active entry's existing limit. |
| Blind 4: mutable/FIFO descriptor archiving | high | Patch. A child can replace its known descriptor path before uncancellable `File.Copy`; snapshot validated bounded bytes before launch and archive the snapshot. |
| Blind 5: non-600 unoverridden fallback | medium | Patch. `ReadinessTimeout` can reach the writer with `IsOverride=false`, where v1 requires 600. The approved policy requires a justified declaration override; refuse this unsupported handoff configuration before provisioning rather than fabricate justification. |
| Blind 6: interrupted claim publication | low | Patch. CreateNew exposes a partial owner document if the process ends during serialization, which owner-limited cleanup cannot decode. Atomic private temporary-file publication is a direct correction under the existing transaction. |
| Blind 7: incomplete batch legacy evidence | medium | Patch. The legacy artifact has one timing/report/count binding and cannot prove all entries; its validator also permits a list omitting the canonical selection. Full suite/candidate proof remains unavailable. Refuse legacy evidence export/validation for multi-entry batches before provisioning, and validate membership for represented selections; keep ordinary batch execution and diagnostic reports. |
| Blind 8: renewed post-processing timer | medium | Patch. The fresh five-second archival timer begins after report processing has used the stop budget, and exceptions replace the child outcome. Use the original remaining bound and preserve causal outcomes/stopping timestamps with evidence diagnostics. |
| Blind 9: unsupported CI refused after startup | medium | Patch. Missing actual CI metadata is discovered only by the writer after services start. Validate supported actual execution context before provisioning; do not invent metadata or add CI integrations. |
| Blind 10: cross-process claims unverified | medium | Patch. Existing contention uses tasks in one process, while the store's guarantee is across processes. Add a subprocess contention and interrupted-lock-holder check using existing production entry points. |
| Edge 1: self-owned secondary overlap | medium | Patch; same verified allocation ordering as Blind 1. |
| Edge 2: mixed stopping limits | medium | Patch; same active-entry bound defect as Blind 3. |
| Edge 3: per-entry report loss | medium | Patch; same omission as Blind 2. |
| Verification 1: activated gate deadline absent from tests | medium | Patch. Filed evidence verifies every direct gate test uses the legacy deadline; add delayed activation and controlled renewal/expiry assertions. |
| Verification 2: engine claim release absent from normal tests | medium | Patch. Filed evidence verifies engine cleanup tests seed no claims; assert successful release, peer preservation and retention after incomplete cleanup. |
| Parent live verification: shared provider restore collision | medium | Patch. Concurrent actual suite commands exposed shared composition-provider `obj/project.assets.json` despite separate final output directories. Added the existing `--artifacts-path` option under the run workspace; preserved assembly location, catalog, configuration and startup deadline. A real concurrent three-project regression passed both mode cases, followed by a passing fresh two-environment proof. |


## Design Notes

No intent gaps or irreversible changes. Dispatch spans runner, observations and handoff. Allocation probes are not reservations. Source qualification grants no package-mode CI or candidate qualification.

## Verification

Build Builds Module.Tests and Platform Composition.Tests individually in Debug with `-m:1`; invoke xUnit assemblies with focused `-class` filters, then affected regressions. Run schema parity and gated real-service proof. Record commands, full revisions, descriptor/timing/data and owned cleanup. Report blockers separately; model assertions cannot replace live evidence.


### Implementation verification, 2026-10-22

Evidence index: `evidence/2-4/README.md`; machine-readable results: `evidence/2-4/verification.json`.

- Builds Debug build: passed with zero warnings/errors. Serial affected regressions: **370/370 passed**. Final descriptor endpoint guard focused checks: **75/75 passed**. Production behavior did not change afterward.
- Platform Debug build: passed with zero warnings/errors; affected retention/build-isolation regressions: **29/29 passed**. Final fixture-only rebuild passed with one existing project-graph MSB3277 Redis assembly-version warning, retained in its log.
- Current Platform and Builds environment/result schemas are byte-identical, with hashes recorded in `schema-parity.json`.
- Gated real-service proof: **1/1 passed** in 101.923 seconds. Both fresh runs reached healthy readiness, passed the declared startup verifier, consumed validated descriptors, persisted identical keys, read their own values and could not see the peer's unique key. Actual execution received the full configured 120 seconds after approximately 96 seconds of startup in one run. Neither command produced `HXR003`.
- Final exact-owner audit covers all six run IDs from three attempts: no owned containers/processes, run workspaces, state files or JSON claims remain. The passing attempt's explicit cleanup receipts both succeeded on their first retry-safe call.
- Preserved failures: the six-server reference domain composition exited before healthy (`HXR025`); a fixture-only evidence transport mistake occurred after successful readiness/handoff; initial `HXR028` stop receipts were retained and same-ID retries succeeded. An eight-second startup-task fixture was made phase-specific with a sentinel and finite 15-second budget; the final serial regression run passed.

This is local source/Debug qualification of provisioning. Broader domain startup, package execution, CI, attachment admission and candidate acceptance are not claimed. Independent changes and all pins are preserved.

### Parent acceptance audit, 2026-10-22

The complete baseline diff, including concurrent work, is retained at `/tmp/story-2-4-full-6hmyocln.patch`. Parent inspected the complete Story 2.4 implementation diff at `/tmp/story-2-4-code-m9jjj68q.patch`; concurrent Story 2.2 schema/reader changes and shared documentation hunks remain separate. Approved frozen intent fingerprint is unchanged. All implementation handoff source hashes match.

| Matrix row | Executed coverage | Result |
| --- | --- | --- |
| Ready suite/batch | `ReadyBatchHandsValidatedDescriptorsToSequentialChildrenAfterStartupAsync`; live declared-suite proof | Passed in 75 focused checks and live 1/1. |
| Invalid/incompatible batch | `IncompatibleBatchNeverStartsAsync` runtime/duplicate/missing cases | Passed in 75 focused checks. |
| Managed exclusive claim | `ConflictNamesOwnerAndRollsBackOnlyTheRejectedAcquisition`, `ConcurrentAcquisitionsHaveExactlyOneWinnerAsync`, `ChildConflictPreservesTheBlockingOwnerAsync` | Passed in 75 focused checks. |
| Readiness/task failure or timeout | `StartupReadinessTests` and `StartupCoordinationTests`; retained live startup refusal | Passed in serial 370; failures never yield a ready session or child. |
| Missing catalog/observations/descriptor | Ten `MissingProofNeverLaunchesAChildAsync` cases | Passed in 75 focused checks. |

Parent independently parsed the actual logs: 370/370, 75/75, 29/29 and live 1/1, each with zero errors/failures/skips/not-run. Inspected both positive descriptors, UTC execution intervals, startup tasks, data receipts and successful explicit cleanup receipts. All six exact-owner cleanup audits are empty; no `HXR003` appears in either positive command. Machine-readable parent checks are in `evidence/2-4/parent-verification-audit.json`.


### Review corrections and final parent verification, 2026-10-22

All review findings routed to patch are corrected. No findings were deferred, and frozen intent remains unchanged. Secondary allocation claims only accepted non-overlapping ports; claim documents publish atomically. Native execution retains validated pre-launch descriptor snapshots and every inspected report within the original processing bound. Mixed batches use the active entry's stopping/cleanup limit. Unsupported fallback/CI configuration and multi-entry legacy evidence export refuse before provisioning; imported evidence checks canonical selection membership. Activated gate and exact-owner cleanup regressions now run normally.

Parent verification exposed one further real concurrency defect: composition provider builds shared source restore intermediates. The one-argument `PlatformManifestCommand.BuildModelAsync` correction places artifacts under each existing run workspace. `PlatformModelBuildIsolationTests.ConcurrentProviderBuildsOwnAllGraphOutputsAsync` executes two concurrent three-project builds in each mode and verifies private graph outputs, unchanged source directories, catalog and configuration. Failed live evidence is preserved at `evidence/2-4/parent-final/live/`.

Final evidence is indexed in `evidence/2-4/parent-final/README.md` and `verification.json`:

- Both individual Debug builds passed with zero warnings/errors.
- Builds affected regressions: **426 passed**, zero failures/errors/skips/not-run, 163.359 seconds. All new review regression cases executed and passed.
- Platform affected regressions: **37 passed**, zero failures/errors; one separate opt-in occupied-listener proof was skipped. Current schema mirror parity: **1 passed**.
- Fresh real-service qualification: **1 passed**, zero failures/errors/skips, 82.865 seconds. Runs `8629727f7f9688e67b86b7a06e4dcf9d` and `927a57797db4d90d6bb817fc7d1cdc8b` each observed six healthy resources and a passed required startup verifier. Startup took 75.54 and 77.53 seconds; each then received the full 120-second execution interval. Both persisted/read their own value and could not observe the peer's key. Neither returned `HXR003`.
- Parent independently validated both retained descriptors and inspected durable entry reports. Final source fingerprints match all **51** reviewed files. Approved frozen fingerprint remains `1f9629e6eaa57fac455129b09e94a9667293a9c5cb167d6403d239d0fbe5d852`.
- Read-only exact-owner resource scanning covers all **ten** run IDs from every attempt: no owned containers/processes, run workspaces, state files or JSON claims remain. Each final positive cleanup succeeded on its first explicit call.

This accepts local source/Debug provisioning for the supported technical closure. The retained broader reference-domain startup failure remains outside this positive runtime qualification; package-mode live execution, CI orchestration and candidate acceptance are not claimed. Concurrent work and pins were preserved. Full and Story 2.4 review diffs were rewritten after corrections at their original temporary paths.


## Code review round 2, 2026-10-23

Scope: platform `543ea182..2048aff` (story code paths, evidence excluded) and Builds `66b9136a..db5abf9`; 51 files, +2285/−98. Review mode `full` against this spec, `epic-2-context.md` and `docs/contracts/test-environment-v1.md`. All four layers (Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor) completed; 71 raw findings were verified individually, then grouped. The reviewed runtime code is unchanged at current Builds/Platform HEAD, so every location below is current. `B/` means `references/Hexalith.Builds/`.

### Review Findings

**Decision needed** — both resolved by the owner on 2026-10-23 and now tracked as patches.

- [ ] [Review][Patch] Resource ownership falls back to the literal `platform` (decision resolved 2026-10-23: option (a), explicit attribution) — `RunReadiness.OwningModule` reflects on `PlatformServerAnnotation`; every resource without it (Redis, placement, scheduler, sidecars, module-declared resources) and any server whose annotation shape changes silently becomes `moduleId: "platform"`, so the writer's blank-owner refusal can never fire and the descriptor binds an owner no declaration supplied ("never invent values"). The `owner` refusal scenario changes `RunId`, not `ModuleId`. Options: (a) explicit attribution — PlatformAppModel annotates every composed resource with its owning module (servers → declaring module, runner infrastructure → the composition's actual platform declaration) and RunReadiness leaves `ModuleId` null otherwise so handoff refuses; (b) keep `platform` for infrastructure but refuse undecodable server annotations; (c) accept and document infrastructure attribution. Recommended (a). [B/src/hosts/Hexalith.Builds.Module.AppHost/RunReadiness.cs:458]
- [ ] [Review][Patch] Closed v1 run-evidence schema widened in place (decision resolved 2026-10-23: option (a), document the forward-incompatible additive change; pinned validators move with the tool) — `selectedTestRun` and resource items (`additionalProperties: false`) gained `selections`, `executionTiming`, `moduleId`, `observedAt`, `endpoints`; every new artifact emits them, so validators embedding the previous v1 schema reject artifacts from this tool. Old artifacts still validate. Options: (a) accept as a forward-incompatible additive change and document that pinned validators move with the tool; (b) publish `hexalith.module-run-evidence.v2` for the new fields; (c) omit the new fields from exported evidence and keep them in the durable run record only. Recommended (a). [B/schemas/hexalith.module-run-evidence.v1.json:258]

**Patch**

- [ ] [Review][Patch] Independent runs collide on port claims and fail with `HXR031` — `CompositionPortAllocator.Allocate` probes random free ports without consulting the claim store, and both claim sites fail the start without reallocating. A port claimed by another run but not yet (or never) bound looks free; with ~50–60 claimed ports per run in a ~20,000-port range, two concurrent or one retained peer collide on the order of 10–15% of starts, contradicting "independent allocations coexist". Exclude claimed ports and reallocate fresh allocations on conflict; keep `HXR031` for claims that cannot be reallocated. The live proof could not see this because both commands shared one in-process allocator. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:331; src/Hexalith.Platform.Composition/PlatformAppModel.cs:376]
- [ ] [Review][Patch] Existing selected-suite report-safety regressions silently stop before the child runs — `ModuleRunnerReviewRegressionTests.Session()` lacks provisioning observations, so `ExecuteSelectedAsync` refuses with HXT009 before launch; `EveryExitInspectsEncodedReportsAsync` `utf16-failed`/`utf16-safe` fail and the other cases pass without a child (a 60 s `token-hang` passes in under a second). The class was outside the recorded 426-test list. Route `Session()` through `TestEnvironmentProvisioningFixture` and assert a child-ran sentinel. [B/test/Hexalith.Builds.Module.Tests/ModuleRunnerReviewRegressionTests.cs:562]
- [ ] [Review][Patch] Batch combined budget omits inter-entry overhead — `Activate(Σ execution limits)` starts before entry 1 while stop, report processing (up to min(stop, 30) s), archival and descriptor writing between entries consume it; a later entry is cancelled (HXT007) before its own advertised `deadlines.execution.expiresAt`, `HEXALITH_G4_EXECUTION_DEADLINE_UTC`, token and gate expiry. Bound the batch by Σ(execution + stopping/processing allowances) so every entry receives its configured limit and advertised deadlines match enforcement. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1121]
- [ ] [Review][Patch] Startup expiry during source mapping in the test path loses startup timing and uses `HXR025` — the inner `catch (OperationCanceledException)` maps it to a refusal with the AppHost "exited before healthy" code, bypassing `ReportSetupTimeoutAsync`, which run/debug use to emit `HXR021` and preserve the startup summary (matrix row 4). [B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1147]
- [ ] [Review][Patch] Terminal recording rereads `execution-timing.json` from the child-reachable run workspace — contradicts the stated "never reread child-writable entry files" invariant; when archival is skipped (exhausted processing budget) a child-left FIFO blocks `File.OpenRead` indefinitely, or forged timing is recorded. Use the runner's in-memory timing (session/native result) and fall back to the prior record. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:562]
- [ ] [Review][Patch] A failed batch does not identify the failing entry or the entries never executed — the loop breaks on the first failure and the result carries only that entry's generic diagnostics; archives are keyed by hashed names. Add a metadata-only diagnostic naming executed, failed and not-executed entries, with a mid-batch failure regression. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1136]
- [ ] [Review][Patch] Setup-phase `HXT007` handling is stale after moving setup to the startup clock — the "during setup" catch can no longer fire (the execution budget is unbounded until activation), and `SetupUsesInitialFiniteLifetimeAsync` still expects HXT007 within 3 s: `early-mapping` now waits the full 600 s and fails with HXR021. Delete the dead catch and rewrite the theory with a short `ReadinessTimeout` asserting the new startup classification. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:773; B/test/Hexalith.Builds.Module.Tests/ModuleRunnerReviewRegressionTests.cs:177]
- [ ] [Review][Patch] Engine `HXR031` translation and primary-port claiming are untested — no test reads an AppHost HXR031 document through `ReadStartupFailuresAsync` or proves plan ports are claimed after ready and released after `down`; removing either passes the suite (AC2). Also closes the matching Story 2.3 deferred ledger item. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:331]
- [ ] [Review][Patch] Startup task results never reach a writer test — the fixture always uses `StartupTasks = []`; dropping `StartupTaskResults = taskResults` would refuse every real suite with a per-start task while all tests pass. Assert the recorded `sample/verify` result in `StartupTaskReadyRequiresItsArgumentsAsync`. [B/test/Hexalith.Builds.Module.Tests/StartupReadinessTests.cs:288]
- [ ] [Review][Patch] AppHost readiness endpoints, module ID and observation times are produced but never asserted — `HealthyReadinessWritesMetadataAndFinalProgressAsync` exercises the code with an allocated endpoint but checks none of the new fields; an annotation shaped like the real `PlatformServerBinding` (including a `-dapr-cli` sibling) is never resolved. [B/test/Hexalith.Builds.Module.Tests/Workspace/AppHostRunnerValidationTests.cs:285]
- [ ] [Review][Patch] `ComposeRun` secondary-port claiming call site is untested — only the helper is called directly; reverting `PlatformAppModel.cs:138` passes every test. Also closes the matching Story 2.3 deferred ledger item. [tests/Hexalith.Platform.Composition.Tests/PlatformRunIsolationTests.cs:173]
- [ ] [Review][Patch] Evidence validator execution-timing coherence is never exercised against tampering — deleting the `IsExecutionTimingCoherent` clause accepts an extended deadline; add deadline/effective-seconds/configuration-identity/start-before-request/credential-expiry mutations. [B/src/libraries/Hexalith.Builds.Tooling/RunEvidence/ModuleRunEvidenceArtifactValidator.cs:486]
- [ ] [Review][Patch] Activated deadline environment values are indistinguishable from legacy values in tests — reverting to `selected.ExecutionDeadlineUtc`/`CredentialExpiryUtc` would tell children a deadline earlier by the whole startup time; assert env deadline == descriptor `expiresAt` and token `exp` in the delayed-start batch test. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestHandoff.cs:96]
- [ ] [Review][Patch] Batch refusal branches are untested beyond runtime/duplicate/missing — mode mismatch, retention/non-retention mixing and combined-limit overflow can be removed without failures, losing the refuse-before-startup promise. [B/test/Hexalith.Builds.Module.Tests/TestEnvironmentProvisioningTests.cs:103]
- [ ] [Review][Patch] The combined batch execution bound is never exercised — no `ModuleTestExecutionBudget` test proves no cancellation before `Activate`, cancellation after N s, and no renewal by later `Activate`/`Restrict`. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleTestExecutionBudget.cs:11]
- [ ] [Review][Patch] The unsafe-endpoint refusal is never reached by tests — the `endpoint` scenario replaces the only EventStore endpoint, so it fails the EventStore match before `SafeEndpoint`; keep the EventStore endpoint and add hosted-host/userinfo/query endpoints so loopback, credential and query/fragment checks are what refuse. [B/test/Hexalith.Builds.Module.Tests/TestEnvironmentProvisioningTests.cs:160]
- [ ] [Review][Patch] Explicit `down` wipes batch membership from the durable record — `Selections = _options.SelectedTests` overwrites prior entries with `[]` when a cleanup engine records the stopping outcome; keep `record.Selections` when the options carry none. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:562]
- [ ] [Review][Patch] Stale `functional-observations.json` leaks between batch entries — only the report is deleted from the shared `native-tests` directory, so `File.Exists(observationPath)` makes a later entry consume the earlier entry's observations; delete it with the report. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestExecutor.cs:333]
- [ ] [Review][Patch] Descriptor-writer refusal reasons collapse into one generic `HXT009` — the writer's constant messages (source drift, missing catalog, unsafe endpoint, tasks not passed) are discarded; carry the runner-authored message into the diagnostic. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestExecutor.cs:323]
- [ ] [Review][Patch] Batch selection refusals share one message — missing, duplicate, incompatible and overflow selections, and the unrelated supplied-mapping mode mismatch, all report "Select compatible declared suites…"; use distinct constant messages. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:1063]
- [ ] [Review][Patch] Startup-task results are written with the deadline-bound token — when a task reaches the startup deadline the write throws, skipping the post-loop diagnostic that names the expired task (test commands now pass the startup token); write with a non-deadline bound. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:1025]
- [ ] [Review][Patch] `credentialExpiryUtc` parsing can escape evidence validation — `GetDateTimeOffset()` throws `FormatException` for a non-date string (format is not asserted by the schema evaluation) while `TryValidate` only catches `JsonException`; use `TryGetDateTimeOffset`. [B/src/libraries/Hexalith.Builds.Tooling/RunEvidence/ModuleRunEvidenceArtifactValidator.cs:492]
- [ ] [Review][Patch] Cross-process claim test builds its child without the repository's isolated-build flags — add `-nodeReuse:false` and `-p:UseSharedCompilation=false` as `PlatformManifestCommand` does, avoiding lingering nodes and load-induced 60 s timeouts. [B/test/Hexalith.Builds.Module.Tests/CompositionResourceClaimStoreTests.cs:106]
- [ ] [Review][Patch] Run-private project build test can pass vacuously — assert the composed builder contains at least one `ProjectResource` before checking `--artifacts-path`. [tests/Hexalith.Platform.Composition.Tests/PlatformRetentionBuildIsolationTests.cs:57]
- [ ] [Review][Patch] Live proof never deletes its temporary workspace — `platform-suite-provisioning-*` (state, claims, suite-proof) remains after every run; delete it after the cleanup audit. [tests/Hexalith.Platform.Composition.Tests/PlatformTestEnvironmentProvisioningTests.cs:24]
- [ ] [Review][Patch] Live proof validates suite re-serialized receipts instead of the runner's archived descriptor snapshots, and `persisted` is a hard-coded literal — validate `state/diagnostics/<run>/test-environment.json` and derive `persisted` from the `SAVE` reply. [tests/Hexalith.Platform.Composition.Tests/PlatformTestEnvironmentProvisioningTests.cs:65; eng/test-environment-provisioning/suite.py:82]
- [ ] [Review][Patch] `PreStartValidityCeiling` batch-sum branch is untested — only the single-entry branch is asserted. [src/Hexalith.Platform.Composition/PlatformRetentionProfile.cs:242]
- [ ] [Review][Patch] Run-private `--artifacts-path` for vstest/mtp selected children is untested — every selected-suite test uses `python3`; assert the argument via the `NativeDotnetCommand` stand-in. [B/src/libraries/Hexalith.Builds.Tooling/Runtime/NativeTestExecutor.cs:372]
- [ ] [Review][Patch] Contract handoff section misdescribes the proof and carries a story-named heading — it says the gated test "runs two declared suites" (it runs one declared suite in two concurrent fresh environments), titles a versioned-contract section "Story 2.4 runner handoff", and leaves a double blank line before it. [docs/contracts/test-environment-v1.md:248]
- [ ] [Review][Patch] Builds README handoff section breaks its wrapping and omits that top-level `test-environment.json`/`execution-timing.json` archives are overwritten per batch entry (per-entry archives are authoritative). [B/README.md:382]
- [x] [Review][Patch] Sprint tracking was not synced — `sprint-status.yaml` shows `review` while this spec said `done`; synced by this review's status update. [_bmad-output/implementation-artifacts/sprint-status.yaml:60]

**Deferred**

- [x] [Review][Defer] `RunEndpoints.Observe` throws for a healthy resource with an unallocated endpoint on every run path, including failure and progress reporting [B/src/hosts/Hexalith.Builds.Module.AppHost/RunEndpoints.cs:63] — deferred: maybe-false, medium if true. Settle by confirming whether Aspire 13.6 can report a resource `Healthy` before all its endpoints are allocated (for example proxyless or sidecar endpoints); if it can, return only allocated endpoints from `Observe` and refuse unallocated ones only in the descriptor writer.

**Rejected**

- `false` — Orphaned claims after a crash before the first state write: `down --run-id <named owner>` scans, deletes the workspace and releases claims even when no state file exists (`CompositionEngine.DownAsync` → `ReleaseOwned`).
- `false` — Same-owner re-acquisition self-conflict: no production path re-acquires claims for an existing run; `--validate-platform` composes with claims disabled.
- `low` — Readiness is not re-observed between batch entries: each descriptor reports the truthful startup `observedAt`; an earlier entry breaking services is uncommon and re-observation needs a new AppHost query mechanism.
- `false` — Timing persisted before descriptor validation: activation actually occurred, `actualDurationSeconds` stays null and HXT009 records that no child launched.
- `false` — No live batch proof and trivially distinct environment/catalog IDs: the required gated proof covers declared-suite isolation only, and distinct runner-issued identities are the asserted property.
- `low` — `suite.py` rendezvous through the runner layout with an 80 s wait: opt-in gated proof that fails loudly; a cleaner rendezvous needs a new handoff channel.
- `low` — Live-proof evidence-directory reuse, prebuilt AppHost assumption, missing `HEXALITH_DAPR_HOME`, and cleanup assertions masking the primary failure: opt-in test, loud failures, fixes add guards.
- `false` — CI environment-variable mutation races: `PlatformManifestTestGrouping` uses `DisableParallelization = true`, so the collection runs alone.
- `false` — Widened certificate/capability lifetime and no aggregate cap: the spec directs pre-start validity to the startup ceiling plus execution budget with gate expiry retained; `SelectBatch` bounds the combined limit.
- `false` — Trivial startup verifier: the fixture task exists to exercise the required-task gate, and its pass is truthfully observed.
- `low` — Plan `EnvironmentId`/`AttemptId` initializers regenerate for pre-change `plan.json`: only legacy plan files read by a new tool; `down` does not use them.
- `false` — Fabricated `attemptSequence`/`attemptId`/catalog `instanceId`: the runner is the issuer of run-scoped identities, there are no retries, and the catalog commit comes from the verified snapshot.
- `false` — Unpinned samples link: the hunk is Story 2.2's concurrent contract edit, not Story 2.4 code.
- `false` — Persisted selection rewritten during batches: each `ModuleTestSelection` stays immutable and the full ordered list is retained; only the active-entry pointer changes.
- `false` — Story 2.2 hunks inside the diff: concurrent committed Story 2.2 work in the shared contract file.
- `low` — `ReleaseOwned` throwing inside teardown: requires >2 s transaction-lock contention or delete failure; fix adds a guard.
- `low` — Corrupt claim files blocking as owner `unknown` and the resulting HXR022 text: claims publish atomically; recovery needs new reclamation handling.
- `low` — Startup overrides with more than seven fractional digits pass pre-provisioning: valid but unrealistic manifest values; fix adds a guard.
- `low` — Failed mapping probe applying the 600 s fallback despite a larger dependency override: rare first-run condition; fix restructures startup-token ownership.
- `low` — Activation more than 1 s after the startup deadline outrunning the certificate ceiling: readiness-to-activation is milliseconds; fix adds a guard.
- `low` — Cross-process claim test's 2 s interruption window: kill-and-exit is far below the window; fix adds a test-only parameter.

Outcome: both decisions adopted as recommended; 32 patch items left as action items (the sprint-status sync item is complete). Story status returns to `in-progress`.
