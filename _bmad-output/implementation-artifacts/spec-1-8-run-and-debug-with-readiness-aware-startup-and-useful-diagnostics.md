---
title: '1.8 Run and debug with readiness-aware startup and useful diagnostics'
type: 'feature'
epic: 1
story: 8
created: '2026-10-16'
status: 'in-review'
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

## Verification

**Commands:**
- `dotnet test references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests -nodeReuse:false` -- expected: all pass
- `dotnet build references/Hexalith.Builds/Hexalith.Builds.slnx -nodeReuse:false` -- expected: zero warnings as errors
