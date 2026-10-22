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

- [ ] `src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`, `R/ModuleCommandExecutionService.cs`, `R/ModuleTestSelection.cs` — accept suites/batches, validate compatibility and coordinate one fresh environment.
- [ ] `R/CompositionStartupRequest.cs`, `R/ModuleTestExecutionBudget.cs`, `R/CompositionEngine.cs`, `R/CompositionRunPlan.cs`, `R/ModuleTestRunRecord.cs`, new `R/ModuleTestExecutionTiming.cs` — separate clocks; persist activation without renewing active timeouts.
- [ ] New `R/CompositionResourceClaimStore.cs`, `R/CompositionPortAllocator.cs`, `R/CompositionRunStateStore.cs` — acquire exclusive runner claims atomically before launch; expose blocking ownership and release through existing ownership-limited teardown. Do not lock solely by manifest/composition.
- [ ] `R/CompositionReadiness.cs`, `R/CompositionResourceReadiness.cs`, new `R/CompositionStartupTaskResult.cs`, `src/hosts/Hexalith.Builds.Module.AppHost/RunReadiness.cs`, `RunEndpoints.cs` there — capture ownership/endpoints and task results; never infer observed endpoints from proposed ports.
- [ ] New `src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentDescriptorWriter.cs`, `R/NativeTestHandoff.cs`, `R/NativeTestExecutor.cs`, `R/CompositionSelectedTestGate.cs` — write/validate the descriptor and hand off its path; enforce actual execution deadlines and existing credential protections.
- [ ] Platform `src/Hexalith.Platform.Composition/PlatformRetentionProfile.cs`, `tests/Hexalith.Platform.Composition.Tests/PlatformRetentionProfileTests.cs` — adapt pre-start certificate validity to the startup ceiling plus execution budget; retain actual gate expiry checks.
- [ ] New `T/TestEnvironmentProvisioningTests.cs`, `T/CompositionResourceClaimStoreTests.cs`; existing `T/ModuleTestSelectionTests.cs`, `T/StartupCoordinationTests.cs`, `T/StartupReadinessTests.cs`, `T/NativeTestExecutorTests.cs` — cover every matrix row, descriptor reader acceptance, batch compatibility, delayed readiness preserving execution time, source drift and concurrent ownership.
- [ ] New Platform `tests/Hexalith.Platform.Composition.Tests/PlatformTestEnvironmentProvisioningTests.cs` — gated real-service proof: declared suite consumes descriptor, writes/reads run-isolated state, and cannot read its peer's data.
- [ ] Builds `README.md`, Platform `docs/contracts/test-environment-v1.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml` — document handoff, sync Story 2.4; retain `evidence/2-4/`.

**Acceptance Criteria:**

- Given declared suites, when the command runs, then readiness and required tasks pass before descriptor handoff and test execution.
- Given another run's conflicting claim, when starting without attach, then refusal names its owner without disturbing it.
- Given two fresh environments, when suites persist identical test keys, then each observes only its own synthetic data through its descriptor endpoints.
- Given a supported declared composition, when testing, then the v1 descriptor validates and `HXR003` never occurs; unsupported legacy ABI refusals remain.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

No intent gaps or irreversible changes. Dispatch spans runner, observations and handoff. Allocation probes are not reservations. Source qualification grants no package-mode CI or candidate qualification.

## Verification

Build Builds Module.Tests and Platform Composition.Tests individually in Debug with `-m:1`; invoke xUnit assemblies with focused `-class` filters, then affected regressions. Run schema parity and gated real-service proof. Record commands, full revisions, descriptor/timing/data and owned cleanup. Report blockers separately; model assertions cannot replace live evidence.
