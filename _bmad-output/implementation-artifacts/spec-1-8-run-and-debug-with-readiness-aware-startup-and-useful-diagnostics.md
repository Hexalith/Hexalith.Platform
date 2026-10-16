---
title: '1.8 Run and debug with readiness-aware startup and useful diagnostics'
type: 'feature'
epic: 1
story: 8
created: '2026-10-16'
status: 'done'
baseline_commit: '5636b7f36e28e2731111b049f974b1fa951d1c36'
route: 'dispatch'
review_loop_iteration: 0
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

Paths under `references/Hexalith.Builds`.

- `src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`, `Tooling/Runtime/ModuleInvocationCommand.cs` -- register `debug` beside `run`; `down` keeps `--run-id`.
- `Tooling/Runtime/ModuleCommandExecutionService.cs` -- v1 path (~:573) and `ExecutePlatformAsync` (~:916-1008); engine construction, start, handoff and evidence writing (~:882).
- `Tooling/Runtime/CompositionEngine.cs` -- `StartAsync`, `WaitForReadinessAsync` (~:528), `ReadStartupFailureAsync` (~:450, one resource only), `TearDownAsync` (~:639, deletes workspace and diagnostics).
- `Tooling/Runtime/CompositionEngineOptions.cs:49` -- 5-minute `ReadinessTimeout`; `src/hosts/Hexalith.Builds.Module.AppHost/AppHostRunner.cs:88` -- separate hard-coded 5 minutes; `RunReadiness.cs` -- AppHost probing, writes readiness JSON; `RunTopology.cs:189` -- Debug `dotnet run`.
- `Tooling/Runtime/{CompositionReadiness,CompositionResourceReadiness,CompositionStartupFailure,CompositionRunPlan,CompositionRunState,CompositionDiagnosticMirror}.cs` -- carry deadline, per-resource error, duration; mirror records tokens only.
- `Tooling/Manifest/PlatformManifestValidator.cs` (:478, :555), `schemas/hexalith.module-manifest.v2.json` (`lifecycle.startup`, `startupOverride`) -- source of deadline and override; nothing reads them at runtime.
- Tests: `test/Hexalith.Builds.Module.Tests/{CompositionEngineTests,ModuleCommandApplicationTests,PlatformExecutionReviewTests,CompositionTestFiles}.cs`; the fake AppHost modes (exit, never, foreign, cause) are extended.

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

## Implementation Notes

## Spec Change Log

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

## Verification

**Commands:**
- `dotnet test references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests -nodeReuse:false` -- expected: all pass
- `dotnet build references/Hexalith.Builds/Hexalith.Builds.slnx -nodeReuse:false` -- expected: zero warnings as errors
