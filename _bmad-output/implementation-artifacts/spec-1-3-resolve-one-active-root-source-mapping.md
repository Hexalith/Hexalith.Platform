---
title: '1.3 Resolve one active-root source mapping'
type: 'feature'
epic: 1
story: 3
created: '2026-10-09'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Modules pick source or package Hexalith dependencies through `Exists()` probe chains (own `references/`, siblings, ancestors), so the edited checkout may not be what builds or runs, and source and package copies of one dependency can mix silently. `hexalith-module` has no mapping, mode or submodule handling.

**Approach:** In Builds, `hexalith-module run`/`test` resolve the active workspace root once into one immutable mapping (mode, active module, each root-declared `references/*` identity and its origin), initialize only direct submodules, and give that mapping to every build and launch the tool triggers. Build-side enforcement rejects duplicate source/package identities and unmapped source fallbacks.

## Boundaries & Constraints

**Always:** Implement in `references/Hexalith.Builds`. Resolve once per command, persist the mapping in the run workspace and plan, and reuse it for native tests and AppHost-launched projects. Only the tool's mode property sets the mode. Source mode builds the active module and source identities in Debug; package-origin identities resolve through the workspace's central package management at catalog versions, with no version supplied by the mapping. Mapping values take precedence over consumer `Directory.Build.*` probes. Submodule work is non-recursive, checks out the recorded gitlink, never uses `--remote`, and is unaffected by `submodule.recurse`. Check nested initialization before initializing anything. Resolution and initialization fail before descriptors, run state or resources, naming identity, path and reason in human and JSON output through a new `HXW` rule series and the existing phases, categories and exit codes. A workspace outside Git resolves to the active module with no dependencies.

**Never:** Package-mode identity, catalog, tag or tool-range checks (Story 1.4); `debug` or debugger attach (1.8); the Platform Aspire model or dependency-service images (1.6); editing other modules (1.12). Never change the tool's own packaged-host selection (`CompositionCommandOptions`, `PackagedHostProject`), tool ids, package layout, evidence schemas or exit codes. Never deinitialize automatically or use the network in tests.

**Decisions (2026-10-09):**
- **Mode.** `run` and `test` take `--mode source|package`, defaulting to `source`; CI passes `package`. In this story, package mode only changes the mapping: no source dependencies, and the active module builds in Release.
- **Active root.** The outermost Git superproject working tree that contains the manifest. The manifest must lie in that root or in one of its direct references.
- **Required references.** Every `references/*` entry in the root `.gitmodules` is a required source identity. Uninitialized ones are initialized.
- **Identity.** An identity is a module, named by its submodule leaf (e.g. `Hexalith.EventStore`). While that identity is source, any resolved package named `<identity>` or `<identity>.*`, direct or transitive, is a duplicate.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Source mode | Required direct references initialized | Mapping lists them as source at absolute checkouts; native tests and launched projects evaluate identical mapping values in Debug | N/A |
| Uninitialized direct reference | Required reference not initialized | Initialized at its recorded commit; its own submodules stay uninitialized | Init failure is a missing-source failure |
| Nested reference initialized | Any submodule below a direct reference initialized | Nothing is initialized | Fails naming the nested path, with a deinit hint |
| Missing source | Required path absent after init; sibling, ancestor or nested copies exist | No fallback is used | Fails naming identity and path |
| Duplicate identity | A source copy and a package copy of one identity in a build | Build stops | Fails naming identity, source path and package id/version |
| Unmapped project reference | ProjectReference outside the active module and source roots | Build stops | Fails naming the referenced path |
| Mode vs files | Same tree with optional files present or absent, in each mode | Mode always equals the tool property | N/A |
| No Git | Existing fixture workspaces | Mapping holds only the active module; current behavior is unchanged | N/A |

</frozen-after-approval>

## Code Map

Paths are relative to Builds. `T` = `src/libraries/Hexalith.Builds.Tooling`, `C` = `src/libraries/Hexalith.Builds.Module.Cli`, `A` = `src/hosts/Hexalith.Builds.Module.AppHost`, `M` = `test/Hexalith.Builds.Module.Tests`.

- `C/ModuleCommandApplication.cs:132-185`: option factory for `run`/`down`/`test`; `validate` is at 75-102.
- `T/Runtime/ModuleCommandExecutionService.cs:248-294`: runs the prerequisite gate (248), the Aspire probe (274), then descriptors (294). Resolve and initialize after 274, before 294.
- `T/Runtime/CompositionEngine.cs:57-112, 572-614`: `StartAsync` re-checks; its first side effects are at 109-112. The AppHost environment is scrubbed at 572-614, so allow-list the mapping there.
- `T/Runtime/CompositionEngineOptions.cs`, `CompositionRunPlan.cs`, `CompositionRunPlanFactory.cs`: carry the mapping (additive `plan.json` field).
- `A/RunTopology.cs:157`: launches modules with `AddProject(module.ProjectPath)`; apply the mapping there and verify paths are mapped. Leave host projects at 194-207 alone.
- `T/Runtime/NativeTestExecutor.cs:99-104`: runs `dotnet test` with no configuration or properties.
- `T/Manifest/ManifestPathValidator.cs:23-42`: `FindRepositoryRoot` stops at the nearest `.git`. Reuse it only as the no-Git fallback.
- Reuse: `T/Filesystem/RepositoryPathResolver.cs` for symlink-safe containment, `T/Runtime/CompositionProcess.cs` for bounded processes, the `T/Manifest/PlatformManifestValidator.cs:76-88` diagnostic pattern, and `T/Hexalith.Builds.Tooling.csproj:17-42` for embedded resources (MSBuild files without a layout change).
- `M/CompositionTestFiles.cs`, `CompositionPrerequisiteProbeTests.cs`, `ModuleCommandApplicationTests.cs`: test patterns to follow. No Git test helper exists yet.
- Evidence: Parties `Directory.Build.props:4-35` holds the probe chains the mapping must override (`HexalithXRoot`, `UseHexalithProjectReferences`, `HexalithXFromSource`). No nested submodules are initialized in this workspace.
- Do not change: `Tools/test-g4-tool-package-contracts.ps1:762-768` layout, `ToolProjectSpineTests` contracts.

## Tasks & Acceptance

**Execution:**
- [ ] `T/Workspace/*.cs` (new, one type per file): mode, mapping and entry records, root and `.gitmodules` resolver, submodule initializer and MSBuild materializer. Isolates resolution from composition.
- [ ] `T/Workspace/SourceMapping.props`, `SourceMapping.targets` (embedded): set the mode, `UseHexalithProjectReferences`/`UseNuGetDeps` and per-identity `Hexalith<X>Root`/`Hexalith<X>FromSource`, and fail on duplicate identities and unmapped references. This is how one mapping reaches MSBuild.
- [ ] `C/ModuleCommandApplication.cs`, `T/Runtime/ModuleCommandExecutionService.cs`, `CompositionEngine*.cs`, `CompositionRunPlan*.cs`, `NativeTestExecutor.cs`, `A/RunTopology.cs`: mode option; resolve once; carry and apply to tests and launch.
- [ ] `M/Workspace/*Tests.cs` plus a Git helper: cover every matrix row hermetically, with `file://` repositories and a local folder feed.
- [ ] `README.md`: document the mapping, mode, initialization rules and `HXW` diagnostics.

**Acceptance Criteria:**
- Given a `run` or `test`, when it starts, then the resolver executes once, and native tests and every launched module project use the same mapping content hash.
- Given a consumer whose `Exists()` probes point at sibling or ancestor checkouts, when evaluated under the mapping, then mapped roots and flags win.
- Given the existing suites and installed-tool probes, when run, then they pass unchanged.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands** (from Builds):
- `dotnet build test/Hexalith.Builds.Module.Tests -c Debug -m:1` and the Evidence tests: zero warnings and errors. Then run both xUnit assemblies directly: all pass, none skipped.
- `pwsh Tools/test-g4-tool-package-contracts.ps1 -SkipSourceValidation -RetainPackageDirectory`: passes; grants no acceptance.
