---
title: '2.4 Provision a run-owned environment for a test suite'
type: 'feature'
created: '2026-10-22'
status: 'in-review'
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
