---
title: '1.8 Run and debug with readiness-aware startup and useful diagnostics'
type: 'feature'
epic: 1
story: 8
created: '2026-10-16'
status: done
baseline_commit: '5636b7f36e28e2731111b049f974b1fa951d1c36'
workspace_baseline_commit: '53e2e2ec3e1ea67271867c4f4a1026d1198c42f6'
route: 'dispatch'
review_loop_iteration: 2
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `hexalith-module` has no `debug` command. Startup waits on a hard-coded 5-minute bound that ignores the manifest's `lifecycle.startup` deadline and override. A failure names one resource at most, keeps no diagnostics and records no startup duration.

**Approach:** Add `debug`, and make `run`/`debug` honour one effective deadline measured from the start request. Report every unready resource with its last result and the preserved-diagnostics location, and record duration, readiness results and deadline. `down` keeps stopping only run-tagged resources.

## Boundaries & Constraints

**Always:** Ready means every required resource reports module-defined readiness and every startup task succeeds; a live process alone is never ready. Default deadline 600s from the start request, before prerequisites and model build. Effective deadline for the complete environment is the largest declared finite override across modules, reported as an override with its justification and module. Failure output is a structured per-resource summary (name, last readiness result or error, effective deadline, diagnostics path), never a raw log dump. Diagnostics outlive teardown. `debug` runs the active module and root-declared Hexalith dependencies from source in Debug, reports each project's process id for attach, and records the same run state as `run` so `down` finds it. Preserve v1 manifests, existing exit codes and rule ids; extend additively.

**Never:** Change the schema's constant 600 default, add unbounded or non-finite overrides, treat a process, port or sidecar state alone as ready, stop untagged processes or containers, add a debugger-specific launch protocol, or touch hosted environments, concurrent-run isolation, or the Story 1.9 version pin.

**Decision (2026-10-16):** `debug` attach support means Debug-configuration source builds and reported process ids; no IDE-specific launcher.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Ready | All resources healthy, tasks succeed | Success; duration, per-resource results, effective deadline recorded | N/A |
| Default | No overrides | 600s deadline, not labelled override | N/A |
| Override | Modules declare 900s and 1200s | 1200s used, reported as override with justification | Invalid override refused by validator |
| Timeout | Two resources unready at deadline | Every unready resource listed with last result; deadline; diagnostics path | Existing timeout rule id; diagnostics preserved |
| Early failure | Startup task fails before deadline | Same report, failing task named | Same |
| Down | Run id or manifest hash match | Stops only tagged AppHost, processes, containers | Unknown run stays no-op |

</frozen-after-approval>

## Code Map

Paths under `references/Hexalith.Builds` unless prefixed with `{project-root}`.

- `src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`, `src/libraries/Hexalith.Builds.Tooling/Runtime/{ModuleInvocationCommand,ModuleCommandExecutionService}.cs` -- public run/debug/down routing, original lifecycle request time, bounded command setup/mapping, source Debug mode, handoff and evidence.
- `src/libraries/Hexalith.Builds.Tooling/Runtime/{CompositionStartupRequest,CompositionStartupDeadlineResolver,CompositionEffectiveDeadline,CompositionEngineOptions,CompositionRunPlan}.cs` -- one request clock through mapping, validated default/override selection, absolute deadline and run-plan metadata.
- `src/libraries/Hexalith.Builds.Tooling/Runtime/{CompositionDeclarationClosure,CompositionDeclarationOptions}.cs`, `{project-root}/src/Hexalith.Platform.Composition/{PlatformDeclarationSet,PlatformCompositionOptions,PlatformServerBindings}.cs` -- shared authoritative source/package dependency closure, unchanged package provenance checks, one immutable release-default set, catalog-version authority and task-owner roots.
- `src/libraries/Hexalith.Builds.Tooling/Runtime/{CompositionStartupTask,CompositionStartupTasks}.cs` -- owner-relative task execution, exact-once per-start tasks, inherited run ownership and API-compatible execution entry points.
- `src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs` -- prerequisite/model/preflight/start coordination, task completion, correlated post-task health refresh, final deadline check, retained failure state/diagnostics and bounded teardown.
- `src/hosts/Hexalith.Builds.Module.AppHost/{AppHostRunner,RunReadiness,RunReadinessHealth,RunResourceControl,RunTopology}.cs` -- AppHost absolute bound, module-defined health/invocation probes, fresh post-task readiness, refreshed launcher metadata, and Debug source launch.
- `src/libraries/Hexalith.Builds.Tooling/Runtime/{CompositionReadiness,CompositionResourceReadiness,CompositionReadinessProgress,CompositionStartupFailure,CompositionStartupReport,CompositionRunState,CompositionDiagnosticMirror}.cs` -- readiness/deadline/duration state, nonce correlation, structured per-resource failures and preserved metadata-only diagnostics.
- `src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs`, `schemas/hexalith.module-manifest.v2.json` -- unchanged constant 600 default and HXP021 validation for justified finite 0.001–3600-second overrides, consumed by the shared runtime resolver.
- `src/libraries/Hexalith.Builds.Tooling/RunEvidence/`, `schemas/hexalith.module-run-evidence.v1.json` -- existing additive run evidence support.
- `test/Hexalith.Builds.Module.Tests/{StartupCoordinationTests,DeclarationClosureTests,StartupReadinessTests,CompositionEngineTests,CompositionTestFiles}.cs`, `test/Hexalith.Builds.Module.Tests/Workspace/AppHostRunnerValidationTests.cs` -- request timing, dependency overrides/tasks, provenance, owned descendants, exact-once effects, fresh health/launcher metadata, final deadline and model-failure evidence. Existing `{project-root}/tests/Hexalith.Platform.Composition.Tests/PlatformAppModel{Tests,ReviewTests}.cs` cover model closure/provenance when the broader Platform dependency lane is buildable.

## Tasks & Acceptance

**Execution:**
- [x] `Tooling/Runtime/{ModuleInvocationCommand,ModuleCommandExecutionService}.cs`, `Module.Cli/ModuleCommandApplication.cs` -- add `debug` (source mode, Debug, process ids reported, same run state as `run`) -- FR-2 debugging
- [x] `Tooling/Runtime/{CompositionRunPlan,CompositionEngineOptions}.cs`, `CompositionEngine.cs`, `AppHostRunner.cs` -- resolve the effective deadline from declarations (default, largest override, justification, module), start the clock at the request, and use it for both bounds
- [x] `Tooling/Runtime/{CompositionResourceReadiness,CompositionReadiness,CompositionStartupFailure}.cs`, `RunReadiness.cs` -- report all unready resources with last result/error and failed startup tasks; keep status strings short and safe
- [x] `CompositionEngine.cs`, `CompositionRunState.cs`, `CompositionDiagnosticMirror.cs` -- preserve diagnostics outside the workspace on failure and report the path; record duration, readiness results and deadline on every run
- [x] `Tooling/RunEvidence/*`, `schemas/hexalith.module-run-evidence.v1.json` -- additive evidence fields if run evidence must carry the record
- [x] `test/Hexalith.Builds.Module.Tests/*` -- cover the matrix, including override aggregation, multi-resource timeout, early failure, `debug` registration and `down` scoping

**Acceptance Criteria:**
- Given a declared composition, when `debug` starts, then dependencies run from source in Debug and process ids are reported.
- Given any run, when startup completes, then the recorded duration, readiness results and effective deadline are readable from run state.
- Given `down`, when invoked, then only tagged resources of that run are stopped.


**Review re-derivation (iteration 1):**
- [x] `Tooling/Runtime/{ModuleCommandExecutionService,CompositionStartupRequest,CompositionEngineOptions,CompositionEngine}.cs` -- carry the original lifecycle request time through both v1/v2 run/debug paths; include mapping in the effective startup budget and recorded duration, preserving overrides and cancellation diagnostics.
- [x] `Tooling/Runtime/{CompositionEngine,CompositionDeclarationClosure,CompositionDeclarationOptions}.cs` and `{project-root}/src/Hexalith.Platform.Composition/{PlatformDeclarationSet,PlatformCompositionOptions,PlatformServerBindings}.cs` -- resolve the same authoritative source/package dependency closure used by composition before deadline/task selection. Preserve package provenance checks and resolve each task relative to its declaring repository.
- [x] `Tooling/Runtime/CompositionStartupTasks.cs`, `CompositionEngine.cs` -- tag each verification task and its descendants with the owning run identity using an internal/API-compatible execution path; prove `down` removes an owned descendant and leaves foreign processes alone.
- [x] `Module.AppHost/{RunReadiness,RunReadinessHealth,RunResourceControl,AppHostRunner}.cs` and `Tooling/Runtime/{CompositionEngine,CompositionReadiness}.cs` -- require a fresh module-defined health snapshot after successful tasks (an internal refresh handshake is acceptable); tasks execute once. Do not use a process/port/sidecar state as readiness.
- [x] `CompositionEngine.cs` -- check the absolute deadline immediately before recording ready; route model-build failure through preserved startup diagnostics/state with bounded cleanup.
- [x] `test/Hexalith.Builds.Module.Tests/{StartupReadinessTests,StartupCoordinationTests,DeclarationClosureTests,CompositionEngineTests,CompositionTestFiles}.cs`, `Workspace/AppHostRunnerValidationTests.cs` and `{project-root}/tests/Hexalith.Platform.Composition.Tests/PlatformAppModel{Tests,ReviewTests}.cs` -- verify dependency-only overrides and tasks, mapping time, post-task health regression, exact-once task side effects, owned descendants, final deadline, and model-failure evidence. Run the focused cases and required build/tests without weakening gates.

**Additional acceptance checks:**
- Given a dependency-only override and task, when a module starts, then the selected composition uses that override and runs that task from its declaring repository.
- Given slow mapping, when run/debug starts, then its time counts toward the same effective deadline and startup duration.
- Given health degradation during a successful task, when task execution completes, then startup waits for fresh module readiness or fails at the original deadline.
- Given a task-created descendant, when down runs, then the owned descendant is stopped and foreign/untagged processes survive.

**Review re-derivation (iteration 2):**
- [x] `Tooling/Runtime/CompositionStartupRequest.cs` -- discover the available authoritative selected declaration closure before arming a shorter partial override; do not irreversibly cancel from an active-module override while a dependency's larger valid override is still being discovered. Keep the same original request clock, a finite discovery bound, caller cancellation, and default/override limits. Do not grant expensive source mapping a blanket maximum deadline instead of the validated effective deadline.
- [x] `{project-root}/src/Hexalith.Platform.Composition/PlatformAppModel.cs` and appropriate existing Aspire health registration helpers -- supply genuine application/protocol health checks for all required infrastructure resources used by the fresh evaluation, including Redis, vector Redis, and graph Redis. Preserve image pins, ports, ownership, existing retention changes, and the requirement that missing module health checks fail closed. A live process or open TCP port is insufficient.
- [x] `Tooling/Runtime/CompositionStartupTasks.cs` -- return the existing deadline-elapsed result for local task deadline cancellation, preserving explicit task failure and caller cancellation semantics.
- [x] Focused tests -- prove a short active override cannot cancel discovery of a larger dependency override; prove a composed infrastructure-plus-module model can complete a post-task refresh with real checks, and fails when the infrastructure check fails. Keep the missing-module-check and module-health-regression tests. Verify expired-task diagnostics distinguish timeout from a nonzero task exit. Run required build and module tests with the established xUnit fallback if necessary.

**Additional iteration 2 acceptance checks:**
- Given a short active override and a larger dependency override, when authoritative closure discovery takes longer than the active override, then startup uses the larger override measured from the original request without a poisoned cancellation token.
- Given healthy registered infrastructure and module checks after a successful task, when fresh readiness is requested, then it can finish; failing infrastructure checks or absent module checks still prevent readiness.

**Final review correction:**
- [x] `Tooling/Runtime/CompositionStartupTasks.cs` and covering tests -- drain large unterminated stdout/stderr with bounded buffers while preserving task outcomes, deadline cancellation, caller cancellation, ownership and cleanup.

## Implementation Notes

- Completed the ten follow-up review patches. The original request deadline now cancels prerequisite checks, descriptor children, source-mapping writes, model builds and preflight; resource and invocation probes use its remaining time. The existing three-argument model-build API remains available through an additive overload.
- Startup overrides must be usable by cancellation timers (at least 0.001 seconds) and at most 3600 seconds. Both schema bounds and semantic validation report the existing HXP021; the schema default remains 600 seconds.
- Progress is refreshed after the last resource, before invocation probing, and after invocation completes. Debug output, help and development guidance identify launcher process ids, and diagnostics name projects whose launcher id is missing. Task executable-resolution causes are retained and included in task-failure output.
- Added real readiness-writer and HXR025/HXR026/HXR027 reporting coverage, including final progress and process-id metadata. Test-only Aspire snapshots include its completed internal resource-ready event as well as module health reports; production readiness still uses Aspire's normal wait. An internal cutover delegate allows deterministic runner failure coverage.

### Review iteration 1 coordination changes (2026-10-16)

- `CompositionStartupRequest` carries the command request timestamp and bounds root checks, source mapping, command prerequisite/catalog checks, and descriptor children. Available authoritative owner metadata selects a validated override before expensive mapping; the engine reloads the complete closure and measures from the same original request. Caller cancellation remains distinct from deadline expiry.
- `CompositionDeclarationClosure` and `CompositionDeclarationOptions` now provide the single source/package owner loading, release provenance validation, catalog-version resolution, and dependency selection used by both lifecycle startup and `PlatformDeclarationSet`. Platform and the engine consume one immutable unchanged release-default set. Each task resolves and runs relative to its declaring repository and inherits the owning run tag, including descendants.
- Successful tasks execute once, then request a nonce-correlated AppHost refresh. `RunReadinessHealth` evaluates the registered module health checks afresh; missing checks remain unready. Actual annotated toolkit sidecars additionally require their associated module health and the fresh Dapr probe. Readiness metadata, including launcher ids, comes from the refreshed final snapshot. The engine checks the absolute deadline at the final ready transition. Model-build failures retain startup diagnostics and failed run state through bounded cleanup.

## Spec Change Log

### Review iteration 1 (2026-10-16)

The second review exposed gaps in the non-frozen Code Map/task plan: command-level request timing, authoritative dependency closure, verification-process ownership, and post-task readiness freshness. The tasks above now make these obligations explicit; the approved intent is unchanged. Also preserve model-build startup evidence and check the deadline at final readiness commit.

KEEP: retain all ten completed review fixes, the 600-second schema default and 0.001–3600-second override checks, caller cancellation semantics, structured multi-resource failures, preserved diagnostics, launcher-id terminology/missing-id messages, v1 compatibility, and all existing source/package provenance checks. Preserve all initial committed and concurrent workspace changes. The newly discovered omissions were already present in the resumed implementation, so there is no newly introduced implementation of those omissions to revert; re-derive only the affected startup coordination while retaining the working code. Do not revert baseline-wide or overwrite concurrent selected-test/observation edits.



### Review iteration 2 (2026-10-16)

R3-B5/R3-E1 exposed missing Platform infrastructure health registrations behind the new fail-closed fresh-health evaluation. R3-E2 exposed cancellation before the complete selected dependency deadline was known. The non-frozen task plan now explicitly covers both cases and their regression evidence; R3-B11's direct timeout-result correction is included in this re-derivation. The approved intent remains unchanged.

KEEP: preserve every passing iteration 1 behavior and all prior KEEP instructions, especially fresh module-defined health, refusal of missing module health checks, nonce correlation, exact-once tasks, owned descendants, source/package provenance, default mapping cancellation, original request duration, and all concurrent work. The missing infrastructure registrations predate this review's implementation, so removing the valid fresh-health guard would restore stale readiness and is forbidden. Re-derive only infrastructure registration and preliminary deadline ordering. The faulty partial-override application is removed before re-derivation; no broad code rollback is safe in this shared dirty workspace. Do not overwrite concurrent Works, retention, selected-test, or package-pin changes.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | medium | defer | `RunReadiness` still cancels each resource probe at 90s and each invocation probe at 45s. Those caps predate this story; the story replaced the two hard-coded five-minute bounds the spec names. |
| B2 | medium | patch | `SucceededAsync` catches every `OperationCanceledException` and returns false, so a cancelled start becomes HXR021 instead of HXC130. |
| B3 | medium | patch | `RunAsync` returns the current task name when remaining time is already zero, and the engine always formats that name as a task that failed. |
| B4 | low | reject | The preserved report names the task. The frozen intent forbids a raw log dump, and storing streams or an exit code adds fields the report does not require. |
| B5 | maybe-false | defer | `BeginOutputReadLine` sits outside the start `try`. The `exit 1` test still names the task, so the fast-exit path did not throw. A process that exits before the readers attach would settle whether this becomes HXR020. |
| B6 | low | reject | The schema allows an empty string argument and `Select` drops it. Declaring an empty argument is not an everyday task, and keeping it needs a new branch. Non-string arguments never reach `Select`; the schema requires strings. |
| B7 | low | patch | `failingTask` and the HXR021 message store only the task name. Two modules can both declare `verify`. |
| B8 | medium | patch | The validator accepts any positive finite `timeoutSeconds`. `1e308` is finite, `TimeSpan.FromSeconds` throws at `deadlineUtc = requestedAt + deadline.Timeout`, and the `runId is not null` catch does not run yet. |
| B9 | false | reject | A probe that fails returns that probe diagnostic before the deadline check. The failed probe is the failure; the deadline check runs after a successful probe. |
| B10 | medium | patch | Every timeout before a run id writes `diagnostics/before-run/summary.json`, so the next attempt overwrites it. |
| B11 | medium | patch | `ReportObservedFailureAsync` stores the full observation when nothing is unhealthy, and `ReadStartupFailuresAsync` labels every entry `Unready resource`, including `Healthy`. |
| B12 | false | reject | Healthy detail `module-ready` is a short result. Tokens-only mirrors and diagnostics that outlive `down` are what the spec requires. Success records duration and deadline on run state. |
| B13 | low | reject | `executable.pid` is the Aspire property name. A missing-pid warning adds a branch a normal project resource does not need. The missing test is V1. |
| B14 | false | reject | The engine sets `StartupDeadlineUtc` before it starts the AppHost. The null fallback is the schema default of 600 seconds and is not the launched plan. |
| B15 | false | reject | No shown task fails because it inherits the runner environment. The missing zero-exit test is V3. |
| B16 | false | reject | The diff does not change `ci.yml` or any Node action pin. |
| E1 | medium | patch | Same defect as B3: a deadline that elapsed before the task starts is reported as that task's failure. |
| E2 | medium | patch | Same defect as B2: caller cancellation is stored as HXR021. |
| E3 | medium | patch | Same defect as B11: a healthy observation is reported as unready. |
| E4 | medium | patch | Same defect as B8: an overflowing `timeoutSeconds` leaves `StartAsync` with no diagnostic. |
| E5 | medium | patch | `PreserveDiagnosticsAsync` is awaited before `TearDownAsync` with no surrounding try. An IO failure skips teardown. The general catch cannot finish teardown if preserve throws again. |
| E6 | false | reject | Non-string arguments fail schema validation before `Select`. |
| E7 | false | reject | A schema or run-id mismatch returns HXR022 and does not copy the unready list into the reported diagnostics. |
| E8 | low | reject | A truncated array is only a corrupt state file. Everyday documents are written by the serializer, and throwing adds a branch. |
| E9 | low | reject | A null array element is the same corrupt-file case. |
| E10 | false | reject | The property initializer and `Read` both produce a non-default empty array. `Write` is not handed `default` on this type's normal path. |
| E11 | medium | patch | After `RunAsync` returns, the ready branch does not check `appHost.HasExited`, so a host that died during the task is still marked ready. |
| E12 | medium | defer | Same pre-existing 90-second probe cap as B1. |
| E13 | medium | defer | Same cap as B1. The spec's two bounds were the engine timeout and the AppHost five-minute cancel, which now use the deadline. |
| E14 | false | reject | The validator writes `defaultTimeoutSeconds` as the constant 600. Only `override.timeoutSeconds` changes the clock, which is the declared rule. |
| E15 | medium | patch | HXR026 in `RunReadiness` and HXR027 in `AppHostRunner` still call the overload that passes a null unready list, so the report names one resource. |
| V1 | medium | patch | Startup tests assert documents written by the fake AppHost. `WaitAndWriteAsync`, `ReportStartupStallAsync`, and `executable.pid` have no test. Stopping progress or returning a null pid leaves those tests green. |
| V2 | medium | patch | `AppHostRunner` now cancels from `StartupDeadlineUtc`, but the runner tests return at `--validate-platform` before `CancelAfter`. Restoring the five-minute cancel leaves the unit tests green. |
| V3 | medium | patch | The only executed per-start task exits 1. The override test rewrites the task to `recovery`. A `SucceededAsync` that always returns false still passes both. |
| V4 | medium | patch | No test runs a task past the deadline. Removing `CancelAfter(remaining)` leaves the `exit 1` test passing while a sleep holds startup. |
| V5 | medium | patch | The redaction test attaches a log path and never a preserved directory. The timeout test checks `summary.json` and does not read `observed.txt`. Ignoring `preservedDirectory` stays green. |
| V6 | medium | patch | Public `debug` is tested only for a missing manifest, package mode, and an Aspire mismatch. `ReadyRunRecordsDeadlineAndDebugProcessIdsAsync` sets `DebugAttach` on an engine it builds itself. Either `ExecuteAsync` site can leave `DebugAttach` false and those tests still pass. |
| VO1 | medium | patch | Same defect as B11, filed under verification-gap other findings. |

| R2-B1 | medium | bad_spec | Command-level source mapping precedes `StartAsync`; only selected tests carry their request time into the engine. Non-test startup duration and deadline omit mapping. |
| R2-B2 | false | reject | carried B15: startup tasks still use the inherited runner environment. No failing task or unauthorized credential recipient was demonstrated; declared executables already run with the caller's permissions. |
| R2-B3 | medium | bad_spec | Startup task children receive no run tag. A successful executable can leave a descendant that the run-id scanner cannot attribute to this run. |
| R2-B4 | medium | defer | `CompositionProcess.Kill` waits up to 10 seconds independently; selected-test stopping budgets were introduced by concurrent work and are not caused by these startup review fixes. |
| R2-B5 | medium | bad_spec | The engine reads readiness before executing tasks and then checks only the AppHost process. A module can become unhealthy while a task runs and the old snapshot still yields success. |
| R2-B6 | medium | defer | The concurrently added selected-observation failure branch passes accumulated healthy resources to `ReportFailureAsync`, suppressing its actual observation cause in the resource summary. |
| R2-B7 | medium | defer | Selected-evidence completion validates report/count/cleanup fields but not a nonempty all-healthy resource list. This validation was added by concurrent selected-test work. |
| R2-B8 | medium | defer | Both selected-report readers call `File.Delete` from `finally` without catching IO/permission errors, which can replace the refusal and leave raw material. These readers are concurrent selected-test work. |
| R2-B9 | low | reject | The mirror stores fixed observation tokens, one per output line. Material growth needs a retention/cap policy already deferred; ordinary startup output causes negligible storage, and a cap adds state/branches beyond a direct correction. |
| R2-B10 | medium | defer | The concurrently added OCI verifier handles `.wh.daprd` but not root `.wh..wh..opq`, so a removed lower-layer executable can leave a stale accepted hash. |
| R2-E1 | medium | bad_spec | The engine validates only the supplied manifest, whereas `PlatformDeclarationSet.Load` expands authoritative owner declarations and selects dependencies. Dependency-only overrides/tasks are therefore omitted. |
| R2-E2 | medium | bad_spec | Same request-clock omission as R2-B1: lifecycle mapping uses the caller token and the engine starts a new timestamp afterward. |
| R2-E3 | low | patch | The final successful task and readiness read can finish after the loop's deadline check; add a final deadline check before ready state is recorded. |
| R2-E4 | medium | patch | The model-build `InvalidOperationException` branch deletes the prepared workspace and returns `Failed(..., null)`, bypassing startup evidence despite having a run identity. Use the existing preserved-failure/teardown path. |
| R2-VO1 | medium | defer | The current `ModuleTestSelectionTests` startup callback references `selectedRun` before declaration and `options` before assignment. The final solution build reproduced both compiler errors in concurrent test edits. |
| R2-VO2 | high | defer | `ReadObservationsAsync.Unsafe` compares exact handoff secrets only against string values, not property names. The inert reviewer repro accepted and returned a fixture credential used as an object key; this is concurrent observation work. |

| R3-B1 | false | reject | The live `qualify_runtime_source` now obtains the commit object, validates its commit SHA, then passes its distinct root tree SHA to `_tree`. Concurrent Works fixes superseded the frozen diff's commit/tree comparison. |
| R3-B2 | false | reject | The live host imports and invokes `qualify_runtime_source` for explicit roots and its verification command includes `test_verify_works_producer`; the reported unused-verifier path has been fixed concurrently. |
| R3-B3 | medium | defer | `runtime_source_snapshot` includes only `.cs`/`.csproj` plus three root build files. An inert temporary-tree reproduction adding `src/Directory.Build.props` left the qualification inventory unchanged. This is concurrent Works producer qualification, outside the startup change. |
| R3-B4 | medium | defer | The selected-test provisional budget is restricted from working-tree bytes before authoritative direct-reference mapping, and `Restrict` cannot extend it after refresh. This was introduced by concurrent selected-test execution work. |
| R3-B5 | medium | bad_spec | Fresh readiness correctly rejects missing health checks, but Platform's `redis`, `memories-vectors`, and `memories-graph` containers have no registered check. After a successful startup task the refresh therefore never completes for those otherwise healthy compositions. Register real infrastructure health checks while retaining the fresh module-health requirement. |
| R3-B6 | medium | defer | Selected terminal metadata copies use synchronous `File.ReadAllBytes` on child-visible paths without regular-file custody or a bound. A FIFO can hold terminal recording beyond the selected stopping budget. This path is concurrent selected-test work. |
| R3-B7 | medium | defer | The selected gate rejects links and oversized files but a FIFO passes both checks, then blocks `ReadAllBytes` while holding the synchronization lock; withdrawal/disposal cannot complete. This gate was introduced by concurrent selected-test work. |
| R3-B8 | high | defer | carried R2-VO2: exact handoff secrets are still matched only in string values, while property names use the generic detector. The same demonstrated credential-key retention defect remains in concurrent observation work; no duplicate deferral or patch. |
| R3-B9 | medium | defer | carried R2-B8: raw report/observation cleanup still calls `File.Delete` from `finally` outside protected handling. The same IO-failure escape remains in concurrent selected-test work; no duplicate deferral or patch. |
| R3-B10 | false | reject | carried R2-B2/B15: declared startup executables inherit the caller environment and permissions. The finding still demonstrates no unauthorized recipient or incorrect task outcome; no patch. |
| R3-B11 | medium | patch | The owned-task timeout cancels its local bound and returns `false`; `RunOwnedAsync` treats that as a named task failure rather than its existing deadline-elapsed result. Returning `null` for deadline cancellation preserves caller cancellation and corrects the causal diagnostic. |
| R3-E1 | medium | bad_spec | Same missing infrastructure health registrations as R3-B5: all runnable containers are included in fresh evaluation, and these Platform containers have zero health annotations. |
| R3-E2 | medium | bad_spec | `ConfigureAsync` arms the supplied manifest's short override before `ProbeMappingAsync` and full dependency loading. If it expires there, the linked token is irreversibly cancelled even when a dependency supplies the governing longer override. Resolve complete available owner metadata before applying a shorter partial override. |
| R3-E3 | low | reject | Dependency declarations can be externally edited between engine selection and AppHost loading, producing different snapshots. This requires concurrent edits to lifecycle declarations during a single startup, is unlikely in everyday execution, and a fix introduces closure snapshot/hash state and new guards rather than a direct correction. |
| R3-V1 | medium | defer | Pre-verified regression gap: real `RunReadiness` tests register no `ICompositionTestObservationProvider`; selected command tests bypass it with fixture readiness. Removing the module-identity observation check remains undetected. The observation provider boundary was introduced by concurrent selected-test work, so record the focused missing coverage separately. |

| R4-B1 | medium | patch | The initial readiness path accumulates earlier healthy entries, but the final `Observe`/retry block runs only for a refresh. A required resource can regress while later resources initialize and still be published as healthy. Apply that existing final snapshot block to initial startup too. |
| R4-B2 | medium | defer | The baseline `5636b7f3` already routes v2 readiness to `/v1.0/healthz/outbound` with separate direct module checks, without an authenticated app-channel invocation. A bad app-channel credential is therefore not tested by this unchanged protocol boundary; it predates the startup change. |
| R4-B3 | high | defer | carried R2-VO2/R3-B8: exact secret matching still excludes object property names in the observation reader. The same demonstrated credential retention remains in concurrent selected-test work; no new patch or duplicate deferral. |
| R4-B4 | medium | defer | carried R3-B3: nested build inputs remain outside the Works producer inventory. The same inert nested-build-file reproduction and concurrent-work attribution apply. |
| R4-B5 | medium | defer | carried R3-B4: selected-test execution budget is still tightened from provisional working-tree bytes before authoritative mapping, and cannot be extended. |
| R4-B6 | medium | defer | carried R3-B6: selected metadata still uses synchronous unbounded reads of child-visible paths, permitting a FIFO to block terminal recording. |
| R4-B7 | medium | defer | carried R3-B7: selected gate regular-file custody remains absent and the synchronous FIFO read holds its lock during renewal/closure. |
| R4-B8 | medium | defer | carried R2-B8/R3-B9: selected raw-file cleanup still performs unprotected deletion from `finally`, allowing IO failures to replace the intended refusal. |
| R4-B9 | medium | defer | carried R2-B10: the concurrently added OCI layer verifier still lacks opaque root-whiteout handling and can retain the removed lower-layer executable hash. |
| R4-B10 | medium | defer | carried R2-B6: concurrent selected-observation refusal still passes accumulated healthy resources as unready and suppresses its actual observation cause. |
| R4-E1 | medium | patch | Same initial final-snapshot omission as R4-B1. The existing `Observe`/unhealthy retry logic already demonstrates and handles this state for refresh requests; remove only the refresh-only condition. |
| R4-E2 | high | defer | carried R2-VO2/R3-B8: the same exact-secret-in-property-name defect remains in concurrent selected-observation inspection; no duplicate deferral. |
| R4-E3 | medium | patch | A nonzero platform preflight has an allocated run id, workspace and plan, but deletes the workspace and returns a null run identity. Replace that return with the existing preserved-failure/teardown path while retaining HXP020 and its causal message. |
| R4-V1 | medium | defer | Pre-verified regression gap: all current retention image-binding tests use the tagged `PostgresImage` string; none exercises the new digest-only `IsRequestedPostgres` branch. Removing that expression leaves verification green and a supported observation incomplete. This is concurrent retention work. |
| R4-VO1 | medium | defer | carried R3-B3: the verification reviewer independently reproduced ignored nested targets in Works producer qualification; the code and claim are unchanged. |


### Review follow-up triage (2026-10-17, R5)

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| R5-B1 | medium | defer | The hosted metadata-token installer calls create-orphan before writing its Issued journal; a lost response or parse failure can leave an unrecorded bounded token. This is concurrent hosted-secret provisioning, expressly outside the local startup intent. |
| R5-B2 | medium | defer | Installer.create performs Kubernetes creation before appending its non-atomic resource ledger. A lost response leaves no recorded identity for reconciliation. This independent hosted-installation path was not changed by Story 1.8. |
| R5-B3 | medium | defer | CurrentInventoryAuthority.Map omits the Active/Retained retirement invariants enforced by PlatformHmacKeySnapshot. InventoryReadiness uses the former and can report ready for metadata that key resolution rejects. This is concurrent hosted inventory work. |
| R5-B4 | medium | defer | carried R4-B2: v2 readiness already used sidecar outbound health plus direct module checks at baseline, without exercising the authenticated app-channel invocation. The startup fixes preserve that existing protocol boundary. |
| R5-B5 | medium | defer | The TenantRegistry host resolves KestrelServerOptions directly after startup, while the default service graph exposes options through the options services. An isolated WebApplication reproduction returned no direct registration, so the advertised 1 MiB limit is not applied. This hosted service is concurrent work outside this local startup change. |
| R5-B6 | medium | patch | Startup tasks use line-based redirected readers. An isolated reproduction retained 67,488,768 managed bytes while receiving a 32 MiB unterminated line. A noisy verifier can exhaust the runner before its finite deadline; replace both readers with bounded raw-stream drains, retaining existing lifecycle semantics. |
| R5-B7 | medium | defer | carried R3-B4/R4-B5: the selected-test budget is tightened from provisional working-tree declarations and Restrict cannot later extend it. This remains independent selected-test execution work. |
| R5-B8 | maybe-false | defer | Unverified medium: ReadAccessSecretsAsync counts six values but does not establish the unique service set. The reader runs before the native child, in a private workspace; settle reachable credential loss with a substituted access file before capture and an unsuppressed actual handoff credential. This is concurrent selected-report custody work. |
| R5-B9 | medium | defer | carried R2-B8/R3-B9/R4-B8: unprotected raw report/observation deletion in finally can replace the inspection result on IO or permission failure. No duplicate patch or deferral. |
| R5-B10 | medium | defer | Retention selection has no host OS guard while required observations inspect Linux /proc; unsupported hosts can compose before observation refuses. The verifier's linux/amd64 check identifies an OCI artifact, not the host. This existing independent retention path was preserved. |
| R5-E1 | low | reject | carried R3-E3: owner files can change between closure selection and AppHost loading, but this requires concurrent edits during one startup. The established verdict rejects adding closure snapshot/hash state and new guards for this uncommon case. |
| R5-E2 | medium | patch | Same demonstrated unterminated-output buffering root as R5-B6; both stdout and stderr need bounded drains. Grouped for one direct correction. |
| R5-E3 | medium | defer | Pre-run prerequisite refusal returns no run identity/state, exactly as the baseline and UnavailableAspireCreatesNoRunArtifactsAsync require. Allocated runs carry startup metadata; persisting rejected attempts would need a separate attempt-identity policy. The absence predates this story, and before-run timeouts already retain their attempt summary. |
| R5-V1 | medium | defer | carried R3-V1: incomplete-provider refusal is now covered, but complete selected observations still bypass the actual AppHost writer in tests. Removing TestObservation from the readiness write remains undetected. This unchanged selected-observation boundary belongs to concurrent selected-test work. |
| R5-V2 | medium | defer | Pre-verified gap: retention helper/model tests do not call PlatformRetentionProfile.ObserveAsync. A fallback bypassing BoundBackendImage in that provider can evade them. Exercise the actual Linux/Docker process/image observation path in the independent retention integration lane. |
| R5-V3 | medium | defer | carried R4-V1: all four backend-binding cases still use the tagged PostgresImage reference; the supported digest-only branch remains uncovered. No duplicate deferral. |

## Verification

**Commands:**
- `dotnet test references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests -nodeReuse:false` -- expected: all pass
- `dotnet build references/Hexalith.Builds/Hexalith.Builds.slnx -nodeReuse:false` -- expected: zero warnings as errors

### Review Findings

Code review 2026-10-16 (commits 643c640, 86b9f35 in references/Hexalith.Builds; full mode).

- [x] [Review][Patch] Per-resource (90s) and invocation (45s) probe caps override the effective deadline — replace both with the time remaining until the deadline (decision 1A) [RunReadiness.cs:22-24,50,184]
- [x] [Review][Patch] `debug` reports the `dotnet run` launcher pid, not the debuggee — label it "launcher process id" in the output and docs (decision 2B) [RunReadiness.cs:411-425, RunTopology.cs:189]
- [x] [Review][Patch] Override has no upper bound — reject overrides above 3600s with the existing HXP021 (decision 3A); this subsumes the overflow patch below [CompositionStartupDeadlineResolver.cs IsRepresentableTimeout]
- [x] [Review][Patch] Progress snapshot is written once per resource, so an engine-side timeout reports stale "last result"; also write before the invocation probe and after the last resource [RunReadiness.cs:45]
- [x] [Review][Patch] Platform preflight and model build use hard-coded 5-minute timeouts and the Aspire probe its own timeout; cap each at the remaining deadline and add a test [CompositionEngine.cs:286, PlatformManifestCommand.cs:86]
- [x] [Review][Patch] Overrides that pass `IsRepresentableTimeout` can still crash or collapse: `1e-300` gives a zero deadline; values above the `CancelAfter` limit (~24.8 days) or `DateTimeOffset` range throw [CompositionStartupDeadlineResolver.cs IsRepresentableTimeout]
- [x] [Review][Patch] `Select` discards `pathDiagnostics`; a missing or invalid executable fails as only "Startup task 'x' failed" with no cause [CompositionStartupTasks.cs Select]
- [x] [Review][Patch] `debug` succeeds with no or partial process ids and says nothing; add a diagnostic naming projects without a pid [CompositionEngine.cs WaitForReadinessAsync, RunReadiness.cs TryProcessId]
- [x] [Review][Patch] No test that a real HXR025/HXR026/HXR027 failure writes `UnreadyResources` (dropping `CurrentUnready(...)` stays green) [WorkspaceAppHostTests, AppHostRunnerValidationTests]
- [x] [Review][Patch] No test lets `WaitAndWriteAsync` finish against a healthy resource and asserts `readiness.json` `processId`, `detail` and `effectiveDeadline*` [AppHostRunnerValidationTests]
- [x] [Review][Defer] Startup task killed at the deadline returns false, not null, so a timeout may be worded as "task failed before the deadline" — deferred: maybe-false (medium if true); run a task past a short deadline and read the HXR021 message [CompositionStartupTasks.cs:160-165]
- [x] [Review][Defer] Task executable resolved against the root manifest's repository root for every module — deferred: maybe-false; declare a task in a dependency module with a relative path and see where it resolves [CompositionStartupTasks.cs Select]
- [x] [Review][Defer] `state/diagnostics/<runId>` and `before-run/<guid>` are created for every run and never pruned — deferred: needs a retention rule (not a code-only fix) [CompositionEngine.cs StartAppHost]
- [x] [Review][Defer] Debug attach to the child app process (launch `dotnet <dll>` directly in debug instead of `dotnet run`) — deferred: decision 2C, separate story
- [x] [Review][Defer] Debug-mode Package-to-Source coercion has no test — deferred: reachable only through programmatic `compositionOptions` [ModuleCommandExecutionService.cs:85,968]

**Rejected:** ReadObservedResourcesAsync read can leak the run — false, `IsTeardownFailure` catch wraps it (CompositionEngine.cs:1098-1103); healthy resources listed as unready — already fixed (B11); `diagnostics[0]` unguarded — false, all callers pass a non-empty list; smaller override shortens the default — spec says largest declared override, low; dropped empty task arguments — B6; stdout/exit code not preserved — B4; ModuleId unsanitized in `Describe`, diagnostics path fallback after failed preserve, mirror single `try`, final-read race, `down` manifest-hash test, rule catalog/changelog note, process-id inconsistency between modes, debug help/json schema, `per-start-verify` scope only, run evidence lacking duration, Manifest-to-Runtime namespace dependency — low, not met in everyday use and the fix adds branches or edits docs.


### Implementation verification (2026-10-16)

Commands ran from `references/Hexalith.Builds`. The frozen intent, `baseline_commit`, and `status` were preserved. The working tree and HEAD changed concurrently during implementation; unrelated selected-test/observation edits were retained.

- `dotnet build Hexalith.Builds.slnx -nodeReuse:false -m:1` initially passed with **zero warnings and zero errors**. Log: `/tmp/story18-solution-build.log`.
- `dotnet build test/Hexalith.Builds.Module.Tests -nodeReuse:false --no-restore -m:1` passed after the readiness-fixture corrections with **zero warnings and zero errors**. Log: `/tmp/story18-build7.log`.
- `dotnet test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -reporter verbose -result-xml /tmp/story18-module-tests.xml` ran **950 tests: 948 passed, two failed, zero skipped**. Both failures were the existing `ModuleRunnerReviewRegressionTests.ReadinessDeadlineObservationSharesStoppingBudgetAsync` wall-clock assertions (`observed`: 5.669s against 2s; `timeout`: 2.321s against 2s) during parallel execution. All **186** tests in `StartupReadinessTests` (23), `AppHostRunnerValidationTests` (13), `WorkspaceAppHostTests` (10), and `PlatformManifestValidationTests` (140) passed. Log: `/tmp/story18-module-direct-tests.log`; complete per-case results: `/tmp/story18-module-tests.xml`.
- `dotnet test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -class '*ModuleRunnerReviewRegressionTests' -reporter verbose -result-xml /tmp/story18-timing-rerun.xml` reran the entire affected class in isolation: **43 passed, zero failed/skipped**, including both timing cases, without changing their assertions. Log: `/tmp/story18-timing-rerun.log`; results: `/tmp/story18-timing-rerun.xml`.
- `dotnet test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -class '*AppHostRunnerValidationTests' -reporter verbose -result-xml /tmp/story18-apphost-tests.xml` passed **13/13**. Log: `/tmp/story18-apphost-tests.log`; results: `/tmp/story18-apphost-tests.xml`.
- The initial focused command, `dotnet test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -class '*StartupReadinessTests' -class '*AppHostRunnerValidationTests' -class '*WorkspaceAppHostTests' -class '*PlatformManifestValidationTests' -reporter verbose -result-xml /tmp/story18-focused-tests.xml`, exposed two incorrect task-path fixture assumptions and missing Aspire resource-ready events; it was interrupted while the healthy fixture waited. Those fixtures were corrected and passed in the later broad run. Initial log/results remain at `/tmp/story18-focused-tests.log` and `/tmp/story18-focused-tests.xml`.
- `dotnet test test/Hexalith.Builds.Module.Tests -nodeReuse:false --no-build --no-restore -m:1 --logger 'trx;LogFileName=story18-module-tests.trx' --results-directory /tmp/story18-test-results` reported **zero tests / exit 5** through the repository's Microsoft.Testing.Platform entry point. The built xUnit assembly was used according to the repository fallback guidance above. Log: `/tmp/story18-module-tests.log`.
- A final exact `dotnet test test/Hexalith.Builds.Module.Tests -nodeReuse:false` attempt was blocked by analyzer failures in concurrent selected-test/observation edits. Log: `/tmp/story18-module-project-command.log`.
- A final `dotnet build Hexalith.Builds.slnx -nodeReuse:false -m:1` attempt was also blocked: **seven analyzer errors, zero warnings**. One CA1068 error in the additive model-build overload was corrected afterward by keeping `CancellationToken` last and adjusting its caller. The other six reported errors were in concurrent `CompositionDocumentStore`, `SelectedNativeTestReport`, and `CompositionSelectedTestGate` changes. Log: `/tmp/story18-final-solution-build.log`. Final current-tree verification remains for the parent review after concurrent edits settle; the final help-text and compatibility-overload edits have not yet been compiled in a clean current-tree build.

Acceptance evidence includes default/override aggregation, successful run-state duration/readiness/deadline records, multiple unready resources and preserved diagnostics, successful/failed/expired startup tasks, public v1/v2 debug plus source Debug configuration, launcher metadata and missing-id diagnostics, and down ownership scoping. The pre-existing timing sensitivity and current-tree analyzer blocker remain explicit verification limits. Deferred review findings remain deferred.


### Review iteration 1 final verification (2026-10-16)

- `dotnet build Hexalith.Builds.slnx -nodeReuse:false -m:1` from `references/Hexalith.Builds` passed with **zero warnings and zero errors**. Current-code log: `/tmp/story18-revision-consistent-build.log`. The same normal solution build also passed after the suite with **zero warnings/errors**, restoring consistent generated outputs; log: `/tmp/story18-revision-post-test-build.log`. No test-only rebuild was run afterward.
- `dotnet test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -maxThreads 4 -reporter verbose -result-xml /tmp/story18-revision-consistent-tests.xml` passed **979/979 tests, zero failures/errors/skips**, in 141.760 seconds. No assertions or test selection were weakened. Log: `/tmp/story18-revision-consistent-tests.log`; complete results: `/tmp/story18-revision-consistent-tests.xml`.
- The passing full suite includes **84/84** focused cases: `StartupCoordinationTests` (10), `DeclarationClosureTests` (8), `StartupReadinessTests` (23), `CompositionEngineTests` (27), and `AppHostRunnerValidationTests` (16). These cover dependency-only deadlines/tasks and owner working directories, original request timing through mapping, inherited descendant ownership and down scoping, exact-once task effects, fresh module health after successful tasks, missing health checks, refreshed launcher ids, final deadline refusal, model-failure diagnostics/state, and unchanged package provenance checks.
- `dotnet build src/Hexalith.Platform.Composition -nodeReuse:false --no-restore -m:1 -p:BuildProjectReferences=false` passed with **zero warnings/errors** against the existing dependency outputs. Log: `/tmp/story18-revision-verified-model-build.log`. An earlier composition-project build with references also passed: `/tmp/story18-revision-model-build.log`.
- The broader Platform composition test project remains blocked by pre-existing Identity/EventStore API and package-version mismatches. Normal build: `/tmp/story18-revision-platform-build.log` (missing authentication/authorization types and JwtBearer assembly-version mismatch); no-project-reference fallback: `/tmp/story18-revision-platform-focused-build.log` (missing EventStore namespaces). No dependency pins or Identity/EventStore source were changed for this story. Shared closure/provenance behavior is independently covered by the passing Builds tests.
- The exact required `dotnet test test/Hexalith.Builds.Module.Tests -nodeReuse:false` command still reports **zero tests / exit 5** through Microsoft.Testing.Platform. Log: `/tmp/story18-revision-module-project-test.log`. The built xUnit assembly was run directly using the repository's documented fallback, with the complete passing results above.
- Verification limit/deferred item: existing `Workspace/PlatformIdentityTests.cs:201` builds the real CLI with `Version=9.9.9` and a temporary output directory but updates its default `obj/ref` assembly. A subsequent test-only `BuildProjectReferences=false` rebuild can therefore compile tests against 9.9.9 while retaining the normal runtime CLI DLL. The failed mismatched run is preserved at `/tmp/story18-revision-final-tests.xml` (199 missing-assembly failures and two consequent FIFO timeouts); a normal complete solution build restored consistency before the final passing run. The fixture was left unchanged and recorded in `deferred-work.md`; a normal post-suite build restores outputs for subsequent work.


### Review iteration 2 final verification (2026-10-16)

- `CompositionStartupRequest` discovers the complete available selected owner set under the original finite default bound before applying the effective override. The new controlled discovery test takes longer than the active 0.05-second override, then selects the dependency's 15-second override with the original timestamp and an uncancelled token. A separate public command regression proves a selected 0.5-second deadline still cancels expensive source mapping.
- `PlatformRedisHealthCheck` registers bounded Redis PING/PONG checks on the existing `redis`, `memories-vectors`, and `memories-graph` resources; container images, ports, authentication, labels, and retention settings are preserved. Fresh post-task readiness still refuses missing module checks and unhealthy module/infrastructure results. Local task-deadline cancellation now uses the existing elapsed result, distinct from a nonzero task exit and caller cancellation.
- `dotnet build Hexalith.Builds.slnx -nodeReuse:false -m:1` from `references/Hexalith.Builds` passed with **zero warnings/errors**. Log: `/tmp/story18-iteration2-solution-build.log`.
- `dotnet test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -maxThreads 4 -reporter verbose -result-xml /tmp/story18-iteration2-full-tests.xml` passed **984/984 tests, zero failures/errors/skips/not-run**, in 154.331 seconds. Log: `/tmp/story18-iteration2-full-tests.log`; complete results: `/tmp/story18-iteration2-full-tests.xml`. The direct assembly invocation uses the already verified repository fallback for the zero-test Microsoft.Testing.Platform project entry point.
- The full passing run includes **89** focused startup/closure/engine/AppHost cases: `StartupCoordinationTests` 12, `DeclarationClosureTests` 8, `StartupReadinessTests` 24, `CompositionEngineTests` 27, and `AppHostRunnerValidationTests` 18. Parent audit verified every frozen matrix row has a covering test that ran and passed, including ready/default/override/timeout/early-task-failure/down, plus the new discovery, protocol-refresh, and task-expiry cases.
- Agent focused verification passed **54/54** Builds cases (`/tmp/story18-iteration2-focused-tests2.xml`), and its focused test-project build passed with zero warnings/errors (`/tmp/story18-iteration2-focused-build3.log`).
- The Platform model project build passed with **zero warnings/errors** (`/tmp/story18-iteration2-platform-model-build.log`). The normal Platform test project retains the documented five missing EventStore namespace errors (`/tmp/story18-iteration2-platform-test-build.log`); no unrelated dependency changes were made.
- To exercise the actual new Platform registrations despite that existing test-project blocker, the tracked `PlatformAppModelReadinessTests.cs` ran in `/tmp/story18-iteration2-infrastructure-tests/Infrastructure.Tests.csproj`, referencing the real built Platform model and using a minimal source-mode fixture. **6/6 protocol cases passed**, covering each of the three actual composed infrastructure registrations with fragmented successful PONG and failing ERR responses. Log/results: `/tmp/story18-iteration2-infrastructure-tests.log` and `/tmp/story18-iteration2-infrastructure-tests.xml`. Parent inspected the harness, tracked assertions, actual registrations and results; it substitutes only the blocked surrounding fixture graph.

- Normal post-suite `dotnet build Hexalith.Builds.slnx -nodeReuse:false -m:1` also passed with zero warnings/errors, restoring consistent generated CLI/test outputs. Log: `/tmp/story18-iteration2-post-test-build.log`. No test-only rebuild followed it.

### Final review patches and completion verification (2026-10-16)

- R4-B1/R4-E1: initial readiness now uses the existing final simultaneous resource snapshot/retry block, just like post-task refresh. The deterministic new regression holds a later module's invocation handshake, degrades the already checked resource, proves readiness remains absent, then verifies recovery and the updated launcher id.
- R4-E3: nonzero platform preflight now uses the existing preserved-failure/teardown path. Its regression retains HXP020 and the owner-input cause, and verifies the failed run identity, original duration, override metadata, preserved summary, and workspace cleanup.
- The final solution build passed with **zero warnings/errors** (`/tmp/story18-final-fixture-build2.log`), and the normal post-suite solution build also passed with **zero warnings/errors** (`/tmp/story18-final-verified-post-build.log`). Required command: `dotnet build Hexalith.Builds.slnx -nodeReuse:false -m:1` from `references/Hexalith.Builds`.
- The final direct xUnit fallback command `dotnet test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -maxThreads 4 -reporter verbose -result-xml /tmp/story18-final-verified-tests.xml` passed **999/999 tests**, with **zero failures/errors/skips/not-run**, in 152.842 seconds. Log: `/tmp/story18-final-verified-tests.log`; results: `/tmp/story18-final-verified-tests.xml`. This includes all matrix-covering tests and both final review regressions; the parent audited their per-case results.
- All **6/6** actual Platform infrastructure protocol cases remain passing in the tracked-source isolated harness (`/tmp/story18-iteration2-infrastructure-tests.xml`). The broader Platform test project retains its documented pre-existing EventStore/Identity dependency blocker; no dependency pins were changed.
- Final gate repair preserved concurrent selected-test behavior while correcting member order, empty-string syntax, exception documentation, and test analyzer requirements. No checks, scenarios, assertions, or analyzers were disabled.
- Two earlier final-suite attempts passed 998/999 and reproduced the existing divergent-CLI fixture's shared intermediate-output interference. Metadata inspection proved the test assembly referenced CLI 9.9.9 while its runtime file was 1.0.0. The fixture now uses a private intermediate directory and a copied Tooling reference for its existing no-dependencies build. Its two-case focused verification passed (`/tmp/story18-fixture-isolation-tests2.xml`), and the final full suite passed above. Metadata inspection after both focused and full runs confirmed the main test reference and runtime CLI remain 1.0.0. This resolves the previously deferred fixture-isolation item; earlier failure logs and ledger evidence are retained as history.
- All three independent review layers completed. Every finding has an individual verdict and route above; both final Story 1.8 patch roots are resolved. Unrelated selected-test, retention, Works qualification, and prior protocol findings were recorded in the deferred ledger without overwriting concurrent work. The approved block and both original baseline fields remain unchanged.

### Review Findings (code review 2026-10-17, chunk 1a: Builds `src/libraries`, commits 643c640 86b9f35 ed80c1c abdfca8 d961c44)

- [x] [Review][Patch] Unguarded diagnostics preservation on the pre-run timeout path: an IO/Unauthorized/Json error escapes instead of returning HXR021 [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:1447]
- [x] [Review][Patch] Redundant `_ = _ = entries.RemoveAll(...)` [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionStartupRequest.cs:114]
- [x] [Review][Patch] Pin `debug --evidence` command name (`ModuleRunEvidenceFactory` Debug arm has no test) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/RunEvidence/ModuleRunEvidenceFactory.cs:160]
- [x] [Review][Patch] Pin HXR022 for unsafe `UnreadyResources[].Name/Detail` and `FailingTask` in the startup-failure document [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:685]
- [x] [Review][Patch] Test that a diagnostics-preservation IO failure still tears down and returns the failure result [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:1203]
- [x] [Review][Defer] Debug rewrites only `mapping.Mode` for a package-origin mapping; programmatic callers may fail in Closure.Load [ModuleCommandExecutionService.cs:1282] — deferred: CLI rejects `--mode package` for debug; settle by running a programmatic debug start with a package mapping
- [x] [Review][Defer] Deadline is checked between steps only; probes use their own timeouts [CompositionEngine.cs] — deferred: unverified medium; settle by timing a stalled Aspire probe against a short deadline
- [x] [Review][Defer] Startup task stdout/stderr discarded and failure reason lost [CompositionStartupTasks.cs] — deferred: unverified medium; diagnosability gap, not an AC violation
- [x] [Review][Defer] Startup tasks of dependency modules resolve against the root manifest's repository root [CompositionStartupTasks.cs Select] — deferred: unverified; settle with a dependency-module task fixture in another repository
- [x] [Review][Defer] Preserved diagnostics (`diagnostics/<runId>`, `before-run/<guid>`) have no retention/cleanup [CompositionEngine.cs] — deferred: not required by this spec's ACs
- [x] [Review][Defer] Source-mode builds always use Debug configuration, which may affect plain `run` [CompositionLaunchConfiguration.cs] — deferred: unverified; compare with baseline 5636b7f

#### Rejected
- Override bounds not enforced / deadline overflow (3 layers): false. `CompositionStartupDeadlineResolver.cs:78` rejects `seconds > 3600`, the schema has `maximum: 3600`, and the validator raises HXP021. The layers reviewed intermediate commits, not the final state.
- "Process id" wording / missing launcher id not named: false. `CompositionEngine.cs:1069-1070` emit "launcher process id" and "launcher process id is unavailable". Remaining doc-comment wording is cosmetic (low, rejected).
- `CompositionStartupClock.Remaining(null)` returns 600 s: false. The schema default is documented and the deadline is always set on the engine path.
- `ImmutableArrayJsonConverter.Write` on a default array, null JSON arrays from the AppHost, narrow catch filters in the best-effort probe, path-spelling duplicates, orphaned task descendants, layering inversion, public signature changes, schema-version labels, unsanitised module id: low or speculative with no demonstrated reachable path, and the fix adds guards. Rejected.

### Review Findings (code review 2026-10-17, chunk 1b: Builds `src/hosts` + `test/`)

- [x] [Review][Patch] `refresh-readiness` deadline expiry throws OperationCanceledException out of `RunResourceControl.RunAsync`: no response is written and later control requests are never served; also add a handler test driving a real signed request (ok/nonce, `module-unready`, expiry) [references/Hexalith.Builds/src/hosts/Hexalith.Builds.Module.AppHost/RunResourceControl.cs:46]
- [x] [Review][Patch] No `RunTopology.Compose` test with `DebugAttach = true` (Source and Package) asserting `--configuration Debug` [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/Workspace/WorkspaceAppHostTests.cs:88]
- [x] [Review][Patch] No test for `debug` with a missing launcher process id (diagnostic), `run` recording no ids, or `executable.pid` as long/string [references/Hexalith.Builds/src/hosts/Hexalith.Builds.Module.AppHost/RunReadiness.cs:182]
- [x] [Review][Patch] `_ = selected.Timeout;` asserts nothing; assert the actual TimeSpan [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/StartupReadinessTests.cs:869]
- [x] [Review][Patch] Restore the final newline removed from `RunResourceControl.cs`, `CompositionTestFiles.cs`, `DeclarationClosureTests.cs`
- [x] [Review][Defer] Control loop is sequential: stop/start-eventstore requests wait while a refresh polls up to the deadline [RunResourceControl.cs] — deferred: unverified medium; settle by sending stop during an unhealthy refresh
- [x] [Review][Defer] Refresh waits until the deadline for a runnable resource with no health check that is not a module sidecar [RunReadinessHealth.cs] — deferred: unverified; settle with a health-check-less container in a composed model
- [x] [Review][Defer] Infrastructure refresh test uses a test-local Redis double, not Platform registrations [AppHostRunnerValidationTests.cs] — deferred: spec records the Platform test project as blocked

#### Rejected
- 0.001 s lower bound contradicted by `1e-300` test: false. `IsRepresentableTimeout` requires at least 1 ms, and `1e-300` and `0.0001` are in `UnrepresentableStartupOverrideFails`.
- HXR026 passes healthy resources as unready: duplicate of recorded R2-B6.
- `down` tested by run id only: previously rejected as low.
- Progress-write IO inconsistency, wall-clock thresholds, fake AppHost race and one-line string, reflection on Aspire internals, process-global env mutation, Windows skips, undocumented PlatformIdentityTests workaround, `abdfca8` early-return scope, null HealthStatus loop, sub-tick resolver test, lost end-to-end executable-cause test: low or speculative; fixes add guards or restructuring. Rejected.

### Review Findings (code review 2026-10-17, group 2: platform commits 53e2e2e 0bff314)

- [x] [Review][Patch] `PlatformPostgresqlHealthCheck` catches OperationCanceledException unconditionally, so caller cancellation reports Unhealthy; filter like `PlatformRedisHealthCheck` (`when (!cancellationToken.IsCancellationRequested)`) [src/Hexalith.Platform.Composition/PlatformPostgresqlHealthCheck.cs:35]
- [x] [Review][Patch] `PlatformPostgresqlHealthCheck` has no test: add a loopback fake-server test (non-'S' reply, untrusted certificate, valid chain, bare open port) [tests/Hexalith.Platform.Composition.Tests/PlatformRetentionProfileTests.cs]
- [x] [Review][Patch] No assertion on the retention image names, tags and SHA256 pins (`ghcr.io/dapr/*`, `redis-stack-server 7.4.0-v8`, `falkordb v4.12.0`) or that the default run stays on `daprio/*` and `latest` [src/Hexalith.Platform.Composition/PlatformAppModel.cs]
- [x] [Review][Patch] No assertion that `Resolve(..., profile).Type("access-telemetry-store")` is `state.postgresql` and that `Render` emits the profile component [src/Hexalith.Platform.Composition/PlatformDaprBindings.cs]
- [x] [Review][Defer] `PlatformRetentionProfile.ObserveAsync` (/proc, docker inspect, psql evidence) is untested [PlatformRetentionProfile.cs] — deferred: Linux/Docker bound; extract pure parts first
- [x] [Review][Defer] `run_verification` tamper checks (runtimeArtifacts, worksConsumerSource, CalledProcessError) are untested [eng/verify_works_host.py] — deferred: orchestration harness; helpers carry the logic
- [x] [Review][Defer] Platform has no tests for the `CompositionDeclarationClosure` delegation (supplied-path validation, snapshot conflicts, `TaskRepositoryRoot`) [PlatformDeclarationSet.cs, PlatformServerBindings.cs] — deferred: unverified medium; spec fallback is the Builds `DeclarationClosureTests`; settle by comparing old and new validation
- [x] [Review][Defer] Only the `memories` resource waits for `telemetryPostgres`; other `telemetry-state` consumers do not [PlatformAppModel.cs:274] — deferred: unverified; settle by listing telemetry-state consumers in the retention run

#### Rejected
- `Dapr:Mtls:CertificateDirectory` unused: false. `DaprSelfHostedMtls.cs:8` and `ResolveCertificateDirectory` read it.
- `user.Split(':')[1]` and 0600 files with a null user: false. A null user only occurs off Linux, where `SetUnixFileMode` is skipped on Windows; on Linux the user is `uid:gid`.
- `pid` lambda shadowing (CS0136): false. The committed code builds and its tests ran.
- Retention and Dapr 1.18.1 pins outside Story 1.8: the spec records them as concurrent retention work, and the fix edits the spec.
- Redis `PING` fails closed with auth, `verify_works_host.py` KeyError and snapshot scope, two Postgres passwords, SQL interpolation, tmpfs sizing, hard-coded digests, `/proc` races, commit-message wording, and similar: low or speculative; fixes add guards or restructuring. Rejected.

### Review Findings (code review 2026-10-17, full diff of Builds commits 643c640 86b9f35 ed80c1c abdfca8 d961c44, four layers)

Findings already recorded in chunks 1a/1b above (refresh-readiness cancellation, unguarded pre-run diagnostics preservation, `_ = _ =`, missing final newlines, `debug --evidence` pin, Debug-configuration test, package-mapping debug) were re-raised by this pass and are not repeated.

- [x] [Review][Patch] `CompositionDeclarationClosure.Load` (and `CompositionDeclarationOptions` catalog/props reads) leak `JsonException`, `KeyNotFoundException`, `InvalidOperationException` and `XmlException` on malformed provenance, snapshot, catalog or props files; the callers (`CompositionStartupRequest.ConfigureAsync`, line 75) catch only `InvalidDataException`/IO, so startup crashes instead of returning a diagnostic. Wrap parse/lookups into `InvalidDataException` [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionDeclarationClosure.cs:58]
- [x] [Review][Patch] HXR026 failure hard-codes app id `"memories"` instead of the selected test's module [references/Hexalith.Builds/src/hosts/Hexalith.Builds.Module.AppHost/RunReadiness.cs:360]
- [x] [Review][Patch] New `tests[]` validator rules (HXP003 duplicate name, HXP020 entry suffix per executor, HXP020 python3 non-source mode) have no rule-id/field assertion; `InvalidSelectionNeverStartsAsync` passes without them [references/Hexalith.Builds/test/Hexalith.Builds.Tests/PlatformManifestValidationTests.cs]
- [x] [Review][Patch] No test for `StopSignal` swallowing a stdin `IOException` when `RetainOnFailure` (and cancelling when not) [references/Hexalith.Builds/src/hosts/Hexalith.Builds.Module.AppHost/StopSignal.cs:109]
- [x] [Review][Defer] Timeout report may carry the generic "did not become healthy" message with no resource names when no failure/progress document exists, and unsafe resource names are dropped from the list [CompositionEngine.cs ~1116, ReadUnreadyProgressAsync] — deferred: unverified medium; settle by expiring the deadline before the AppHost writes progress
- [x] [Review][Defer] `startup-failure.json` read in the engine does not re-validate run id/schema/resource text the way `ReadStartupFailuresAsync` does [CompositionEngine.cs ~2478] — deferred: unverified; settle by planting a foreign-run document
- [x] [Review][Defer] Later `StartAsync` failure paths still use `CleanupPreparedWorkspaceAsync` without preserving diagnostics [CompositionEngine.cs ~1403] — deferred: unverified medium; settle by failing after workspace preparation
- [x] [Review][Defer] Retained-log writer: append-mode stream never disposed, no size cap, opened before deadline check; failure to open throws [AppHostRunner.cs] — deferred: retention work is concurrent, outside Story 1.8
- [x] [Review][Defer] Cancelled startup handler uses `descriptorChildEntryAssemblyPath!` which may be null [ModuleCommandExecutionService.cs] — deferred: unverified; settle by cancelling with no composition options

#### Rejected
- HXR025 replaced by HXR021 / lost per-step bounds: false for the id (HXR025 remains at RunReadiness.cs:327/332 for stalls; HXR021 is the startup-bound rule); removing the 90 s/45 s caps is the spec's single effective deadline.
- `Remaining(null)` returns 600 s: duplicate of chunk 1a rejection.
- Changes outside Story 1.8 (retention, selected-test, `tests[]`): informational; the spec records them as concurrent work and the fix edits the spec.
- Startup-task failure cause not surfaced, hash-case comparison, duplicated blocks, `HXP021` precedence parentheses, 1 ms lower bound (spec-approved), negative `StopTimeoutSeconds`, default `ImmutableArray`, path-spelling duplicates, `SingleOrDefault`, primary-resource naming, `retained` label, `executable.pid` type, `--mode package` help text: low or speculative; fixes add guards or complexity. Rejected.


### Review follow-up implementation and verification (2026-10-17)

- Completed all 18 previously unchecked review patches. Pre-run timeout diagnostics preservation now retains HXR021 if IO, permission, or JSON recording fails, and reports the existing HXT008 recording diagnostic. The started-run IO regression proves teardown still removes owned resources and retains failed state. Unsafe resource names, resource details, and failing task names are pinned to HXR022. Removed the redundant discard, asserted the effective TimeSpan, and restored the three requested final newlines.
- Malformed provenance, released snapshots, catalog snapshots, and catalog XML now become InvalidDataException refusals at the shared declaration/catalog boundaries. Nine regression cases cover invalid JSON, missing owner keys, invalid shapes, empty released module sets, invalid XML, null catalog snapshots, and incomplete snapshot fields. Existing provenance and dependency selection checks remain in place.
- Refresh deadline cancellation now produces a `module-unready` response without ending the control handler; explicit caller cancellation still propagates. Three real run-scoped, nonce-correlated control-request cases exercise success/correlated readiness, observation refusal, expiry, and a subsequent request. The existing private request record has no signature field; the review item's “signed request” wording does not require a new authentication protocol. HXR026 observation failures now name the selected module rather than a hard-coded Memories app id.
- Extended the real RunTopology tests for DebugAttach in both Source and Package mappings and retained explicit `--configuration Debug` assertions. Package mappings retain their own Release environment; the explicit Debug launch argument selects the build configuration. Added a debug evidence command-name assertion, normal run/no-id state assertions, a long-valued executable.pid case alongside the existing int/string cases, and stdin IO retention/cancellation cases. The existing missing/partial debug launcher-id diagnostics tests ran and passed. New tests[] cases assert exact HXP003/HXP020 identities and declaration fields for duplicate names, executor entry suffixes, and Python package mode.
- PostgreSQL readiness now propagates caller cancellation. The trusted-chain test also exposed that CreateFromPemFile requires private-key material, while the run CA file contains only a certificate; readiness now loads that trust anchor using certificate-only CreateFromPem. The five loopback cases verify a non-S reply, an untrusted certificate, a valid run chain/hostname, a bare open port reaching the local bound, and explicit caller cancellation. The retention model fixture now sets only its builder's local Development environment so it satisfies the existing profile guard.
- Retention model assertions pin effective image names and SHA256 digests, and require null floating tags after Aspire's WithImageSHA256 selects the immutable image. Exposed Dapr/PostgreSQL declared version references are pinned separately; default control-plane images retain daprio/* and default vector/graph images retain latest. Binding assertions prove the selected access-telemetry-store type and rendered scoped component are state.postgresql, while the default remains state.redis. No image pins, dependency versions, or runtime guards changed.
- `dotnet build Hexalith.Builds.slnx -nodeReuse:false -m:1` passed with zero warnings/errors (`/tmp/story18-review-build4.log`). The normal project test entry point again reported zero tests/exit 5 (`/tmp/story18-review-project-tests.log`); the established direct xUnit fallback `dotnet test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -maxThreads 4 -reporter verbose -result-xml /tmp/story18-review-final-tests.xml` passed **1052/1052**, with zero failures/errors/skips/not-run in **200.207 seconds** (`/tmp/story18-review-final-tests.log`). Relevant class counts are preserved in `/tmp/story18-review-verification-matrix.txt`.
- The first full run had two failures (`/tmp/story18-review-full-tests.log`): an existing 1.5-second stopping-budget assertion missed by 41 ms while another build ran, and the new Package/Debug test initially confused mapping environment configuration with the explicit launch configuration. Verification was made sequential and only the latter fixture expectation was corrected; the clean full rerun passed both, without changing the stopping threshold or dropping the Debug argument assertion.
- The normal Platform test-project build initially passed with zero warnings/errors (`/tmp/story18-review-platform-normal-build2.log`). A later normal rebuild currently fails with three CS0234/CS0246 errors in unchanged Works.Runtime code for Hexalith.EventStore.DomainService/EventStoreDomainDiagnostics (`/tmp/story18-review-platform-normal-build3.log`). A no-dependencies shortcut also selected unusable current EventStore reference outputs (`/tmp/story18-review-platform-focused-model-build.log`). These broad dependency blockers remain separate from the focused passing evidence; no Works/EventStore dependency code or pins were changed.
- The fallback at `/tmp/story18-review-platform-source/` compiles **every actual Platform model source file plus DaprSelfHostedMtls.cs**, using the dependency DLLs retained by the earlier green normal test-project build. It then compiles the tracked PlatformRetentionProfileTests.cs and PlatformAppModelReadinessTests.cs with the existing minimal Root/Options/Builder fixture. Both builds enforce TreatWarningsAsErrors and pass with zero warnings/errors (`/tmp/story18-review-platform-source-build2.log`). The **22/22 tracked cases** pass, with zero failures/errors/skips/not-run in **17.360 seconds** (`/tmp/story18-review-platform-source-final-tests.log`, `/tmp/story18-review-platform-source-final-tests.xml`), including all six actual composed Redis/vector/graph protocol cases. The model and health-check source are used directly; the fixture substitutes only the blocked surrounding dependency graph.
- Aspire baseline startup was attempted through `aspire start --no-build --non-interactive` against the private runner AppHost and refused because no absolute runner-owned run plan was supplied (`/tmp/story18-review-aspire-baseline.log`). The real in-process AppHost/handler regressions and protocol tests supply the focused lifecycle evidence. The approved block, both original baseline fields, deferred findings, version pins, and concurrent work are preserved.

- Final post-verification normal Builds solution build also passed with zero warnings/errors (`/tmp/story18-review-post-verification-build.log`, 43.27 seconds), leaving consistent ordinary outputs after the test/fallback runs.

### Parent final review verification (2026-10-17)

- All three independent layers completed against the captured baseline diff. All 16 findings have individual R5 verdicts in Review Triage Log. R5-B6/R5-E2 shared one patch root; prior findings retained their established routes and independent hosted/retention/selected-test issues were deferred. The final patched diff is `/tmp/story18-audit-nv4d3f64/changes.diff`; opaque binary evidence is represented by ordinary Git binary-file summaries. The spec is excluded from reviewer content so only the edge-case layer receives its separate claims file.
- Startup stdout/stderr now drain through cancellable 8 KiB buffers to Stream.Null, awaited together with process exit. The isolated regression emits 32 MiB without newlines on each stream and verifies both zero/nonzero exits. It failed against the old readers at 404,281,112 allocated bytes and passes after the fix below a 16 MiB budget. All 12 focused lifecycle cases passed (`/tmp/story18-output-focused-tests.xml`).
- Parent inspected the source/test patch, the actual-source Platform fallback projects/fixture, and per-case result files. `dotnet build Hexalith.Builds.slnx -nodeReuse:false -m:1` passed with zero warnings/errors in 31.86 seconds (`/tmp/story18-parent-final-build.log`).
- Exact project command `dotnet test test/Hexalith.Builds.Module.Tests -nodeReuse:false` again produced zero tests/exit 5 (`/tmp/story18-parent-project-tests.log`). The established direct xUnit fallback `dotnet test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -maxThreads 4 -reporter verbose -result-xml /tmp/story18-parent-final-tests.xml` passed **1053/1053**, zero failures/errors/skips/not-run in 152.794 seconds (`/tmp/story18-parent-final-tests.log`). The final output regression ran and passed in that full suite.
- Parent audited every frozen matrix row and the added request-clock/dependency/freshness/ownership checks against actual passing final results. All original 18 review patch tasks and the final bounded-output task are complete. The 22/22 actual-source Platform protocol/TLS/retention cases remain passing (`/tmp/story18-review-platform-source-final-tests.xml`); the current broad Platform dependency blocker remains exactly as recorded above.
- CRLF-aware Git whitespace checks pass in Platform and Builds. Frozen approved intent and both original baseline identifiers remain unchanged. No public API, schema default, dependency pin, or unrelated runtime behavior changed in the final patch.

- Local Builds completion revision: `b7a9c9ec9f57e098b408d3b940929ef4b160985b`. Its exact Conventional Commit message passed pinned commitlint 21.2.3 (`/tmp/story18-builds-commitlint.log`); the Platform completion message passed its pinned commitlint 21.2.1 (`/tmp/story18-platform-commitlint.log`).

### Review Findings (code review 2026-10-17, re-run chunk 1: Builds `Runtime/Composition*` at b7a9c9e vs baseline 5636b7f, four layers)

- [x] [Review][Patch] Expired startup task is not named (fixed: the diagnostic names the task; `failingTask` stays null to keep timeout distinct from a nonzero exit): `deadlineElapsed` breaks to the generic HXR021 timeout with `failingTask` null, so a task running at the deadline yields no resource and no task (violates the Timeout matrix row "same report", iteration 2 timeout-vs-exit distinction) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:972]
- [x] [Review][Patch] Failed-run state no longer persists HXR023 when teardown leaves tagged resources: `ruleId = located[0].RuleId` replaced `clean ? rule : "HXR023"`; breaks "preserve rule ids" and the retried-`down` signal (restore `clean || retained ? ruleId : "HXR023"`) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:1224]
- [x] [Review][Patch] Add a CompositionEngineTests case where teardown leaves a tagged resource after a startup failure: assert HXR023 in returned diagnostics (non-retained yes, retained no) and the persisted state rule id; no test references HXR023 today [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:1243]
- [x] [Review][Patch] Timeout record persists only unready resources while early task failure persists all `readiness.Resources`; "record readiness results" should hold the full last result set in both [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:1110]
- [x] [Review][Patch] `ReadUnreadyProgressAsync` silently drops unready entries failing `IsSafeFailureText`; keep them with a placeholder name/detail so "every unready resource" holds [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:795]
- [x] [Review][Defer] Selected-test machinery (`RecordTestOutcomeAsync` rewrites native report/hashes even when `prior` exists, fail-fast `outcome.lock` blocks state deletion in `down`, `KillOwnedWithoutWaiting` catch set, `WriteStateAsync` does not carry `TestRun` forward, `RetainOnFailure` handle not disposed) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:480] — deferred: belongs to concurrent selected-test work, not 1.8 ACs; settle by a selected-session test with a second terminal record
- [x] [Review][Defer] `DownWithinBudgetAsync` (graceful stop, kill without wait, `recordWithdrawal: false`) has no test; every caller test stubs `PlatformDownOverride` [ModuleCommandExecutionService.cs:1167] — deferred: selected-test scope
- [x] [Review][Defer] Startup-request edge cases: `ConfigureAsync` git `Win32Exception`/malformed declaration JSON, empty `GetFileName` identity in `ProbeMappingAsync`, silently dropped non-string task arguments, absent `executable` property [CompositionStartupRequest.cs, CompositionStartupTasks.cs] — deferred: unverified medium; settle with fixtures for git-absent and malformed declaration
- [x] [Review][Defer] `DownAsync(runId)` lets a corrupt state file throw before the tagged-resource sweep [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:434] — deferred: unverified; settle with a corrupt-state `down` test

#### Rejected
- Closure discovery / setup outside `startupBound`: low. The command-level `CompositionStartupRequest` already bounds discovery (previously triaged); fix adds a second clock.
- Wall-clock `DateTimeOffset` deadline vs monotonic; poll delay overshoot; non-positive preflight timeout: low, deadline is re-checked between steps and `Math.Max(1, …)` clamps the probe.
- Deadline overflow / unbounded override: false. Resolver rejects `seconds > 3600` (R-chunk 1a).
- `InvalidDataException` escaping after `runId` is set: false. The `when (runId is null)` filter falls through to the `runId is not null` catch, which tears down.
- Diagnostics path leak, retained run unannounced, `DiagnosticsPath` absent on ready runs, duplicated withdrawal diagnostic, mirror append growth, test seams in options: low or cosmetic.

Patches applied 2026-10-17 in Builds (`CompositionEngine.cs`, `CompositionStartupTasks.cs`): HXR023 persisted for unclean teardown, full observed readiness recorded on timeout with unsafe entries sanitized rather than dropped, expired task named. Added `UncleanTeardownAfterReadinessFailureReportsAndPersistsHxr023Async`; extended `StartupTaskIsStoppedAtTheDeadlineAsync`. Full `Hexalith.Builds.Module.Tests` run: 1054 passed, 0 failed. Chunk 1 only; AppHost/CLI/tests and platform-repo chunks remain unreviewed in this re-run.
