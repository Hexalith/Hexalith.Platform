---
title: '1.11 Run and debug McpCli from the Platform workspace'
type: 'feature'
epic: 1
story: 11
created: '2026-10-19'
status: 'done'
baseline_commit: 'f0e33167fcdb5f539da159c54f7505b72a65c9b2'
mcpcli_baseline_commit: '447085c42ec606f642240c4cae0415e7c2f97467'
builds_baseline_commit: 'b435f2e3b3892e03f69322d24de6003b0baf7a7d'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** McpCli has no module declaration, and the pinned tool (Builds 4.33.1) composes only servers. Nothing builds `references/Hexalith.McpCli` as a run-scoped, never-published tool, so `debug` from the Platform root cannot target McpCli or reach a breakpoint in it (AR-46).

**Approach:** Give McpCli a v2 declaration with its minimum environment and no interfaces. Make the tool build the active McpCli checkout in Debug into the run workspace, report the built tool, and remove it with `down`. Prove a breakpoint hit while that build runs an offline `modules` inspection under a debugger.

## Boundaries & Constraints

**Always:**
- Run from the Platform root with the pinned tool: `dotnet tool run hexalith-module -- debug --manifest references/Hexalith.McpCli/module/hexalith.module-manifest.v2.json`.
- The McpCli build resolves only through the source mapping (`HexalithMcpCliRoot`). It uses the run's pinned catalog as `BuildModelAsync` does, writes bin and obj only under the run workspace, never packs or publishes, and leaves the checkout's own `bin`/`obj` untouched.
- Fix blockers in the repository that owns them.
- McpCli and Builds changes stay as local commits until review is done.
- Stage only this story's paths.

**Never:**
- Initialize McpCli's nested `references/*` inside the Platform checkout; every Platform run would then fail on it. Prove the own-identity AC in a disposable clone instead.
- Edit McpCli's `Directory.Build.props` probing; that is module adoption (AR-50).
- Add Contracts enrollment, connected execution, profiles, or package-mode/candidate McpCli builds (Epic 3, AR-48).
- Declare an MCP or HTTP interface.
- Count a McpCli own-repository run as Platform evidence.

**Decisions (human, 2026-10-19):**
- **Builds change and release.**
  - Builds gets one `feat(tool)` commit on `origin/main` that builds the run-scoped McpCli.
  - After review, Claude pushes Builds and McpCli and dispatches `build-release.yml`. The human approves the `production` gate.
  - Platform then pins the release and moves the Builds gitlink to its tag.
  - The release carries the other Builds commits already on `origin/main`.
- **Minimum environment.** McpCli declares `dependencies: ["eventstore"]`, the gateway it targets. The engine's eventstore-gateway guard is unchanged.
- **Offline inspection.** `modules` returning `catalog_empty` (exit 2) is accepted. The proof is a breakpoint hit in `CatalogService.ListModules`. Contracts enrollment from source stays in Epic 3 (AR-48).
- **Size.** Keep the full spec (about 3k tokens).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Debug McpCli | McpCli manifest from the Platform root, `HEXALITH_DAPR_HOME` set | The minimum environment is ready. The output names the run-scoped Debug `Hexalith.McpCli.dll`. `modules` run from it under netcoredbg hits a breakpoint in `references/Hexalith.McpCli`. `down` deletes the build. | Unready resources are named per 1.8 |
| Missing checkout | `references/Hexalith.McpCli` uninitialized, or its project missing | No fallback | Fails before launch, naming McpCli and the expected project path |
| Sibling present | `../mcpcli` exists | Only the mapped `references/Hexalith.McpCli` is built | N/A |
| Other active module | EventStore or Memories manifest | No McpCli build | N/A |
| MCP host declared | An interface with protocol `mcp` | Rejected | `HXP002`/`HXP023` (existing) |

</frozen-after-approval>

## Code Map

Builds (`references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling`):
- `Runtime/PlatformManifestCommand.cs:66-118` `BuildModelAsync` is the template to follow:
  - The pinned catalog is injected through `CustomBeforeDirectoryBuildProps`. It sets `HexalithVersionsLoaded`, which also skips McpCli's sibling probing.
  - `CompositionProcess.RunAsync` runs within the startup deadline.
  - `BoundedOutput` formats failures.
  - Use `--artifacts-path <workspace>/tools/mcpcli`, not `--output`, so obj also stays in the run.
- `Runtime/CompositionEngine.cs:~171` builds the model during preparation. Add the McpCli build there when the active module is `mcpcli` in source mode.
- `CompositionEngine.cs:1059-1072` emits the ready `HXI010` diagnostics. Add one there naming the tool path. `down` already deletes the run workspace.
- `Runtime/CompositionDeclarationClosure.cs:229` treats `mcpcli` as non-domain.
- `src/hosts/Hexalith.Builds.Module.AppHost/RunTopology.cs:307-311` requires an eventstore gateway. Leave it unchanged; McpCli declares `eventstore`.
- `Manifest/PlatformManifestValidator.cs:552-555` raises `HXP023`.
- Schema `schemas/hexalith.module-manifest.v2.json`:
  - `identity.servers` may be empty.
  - Each module needs `identity`, `runtime`, `surfaces`, `integration` and `lifecycle`, with all of their listed arrays.
- `Workspace/SourceMappingMaterializer.cs:139-141` maps `HexalithMcpCliRoot` from the `.gitmodules` direct references only.
- Tests: `DeclarationClosureTests.cs:27-31`, `StartupCoordinationTests.cs` (mcpcli fixtures), `PlatformManifestValidationTests.cs:381-389`.
- Git state:
  - The Platform gitlink is `4aaeed8` (v4.33.1-4), and `origin/main` (`b435f2e`) is 33 commits ahead, including `32bd31a`, which updates packages. Commit on top of `origin/main`.
  - Release works as in 1.9/1.10: `build-release.yml` runs by `workflow_dispatch`, needs green push CI and `production` approval.

McpCli (`references/Hexalith.McpCli`, `main` at `447085c`, clean):
- `src/Hexalith.McpCli/Hexalith.McpCli.csproj` is net10.0, `PackAsTool`. The offline `modules|operations|describe` verbs need no URL.
- Breakpoints: `src/Hexalith.McpCli.Core/Catalog/CatalogService.cs:12` and `src/Hexalith.McpCli/Cli/CliRunner.cs:330`.
- Checked 2026-10-19: a scratch `--artifacts-path` Debug build had 0 warnings and took 3.6 s, and the checkout stayed clean. `modules` returns `catalog_empty`, exit 2.
- MCP is stdio only: `src/Hexalith.McpCli.Mcp/McpCliMcpServiceCollectionExtensions.cs:122` registers only the stdio transport, and `CliRunner.cs:701-705` refuses `http`.
- McpCli has no `module/`, no AppHost and no tool manifest.
- All of its `references/*` are uninitialized, and its own `references/Hexalith.Platform` is at `2ac8e73`.
- Pushing `main` runs CI only; `release.yml` is manual.
- To shape the declaration, mirror `references/Hexalith.EventStore/module/hexalith.module-manifest.v2.json`.

Platform:
- `src/Hexalith.Platform.Composition/PlatformServerBindings.cs:30-34` maps `Repository("mcpcli")` to `Hexalith.Mcpcli`, which is the wrong case on Linux.
- `tests/Hexalith.Platform.Composition.Tests/BuildsSourceStartupTests.cs:10-23` holds the gated live cases.
- `README.md:60-178` is the "Run and debug a technical module" section.
- `.config/dotnet-tools.json` pins `4.33.1`, and `hexalith-platform.json` sets the range `[4.33.1, 5.0.0-0)`.
- The netcoredbg 3.2.0 recipe is in `evidence/1-10/diagnostic-eventstore.txt:5-28`. It is downloaded to the scratchpad and never committed.

## Tasks & Acceptance

**Execution:**
- [x] `references/Hexalith.McpCli/module/hexalith.module-manifest.v2.json` -- declare `mcpcli`:
  - no servers;
  - `dependencies: ["eventstore"]`;
  - no interfaces;
  - empty runtime, integration and lifecycle arrays;
  - an additive classification with a reason.
- [x] `references/Hexalith.McpCli/README.md` -- state that run and debug happen from the Platform workspace, and that the own `references/Hexalith.Platform` serves only own-repository tests.
- [x] Builds `Runtime/*` and tests -- add a McpCli build helper beside `BuildModelAsync`, wire it into the engine, add the `HXI010` tool-path diagnostic, and add a failure diagnostic that names McpCli and the path. Unit-test the I/O matrix:
  - the build arguments: Debug, an artifacts path under the workspace, the pinned catalog;
  - a missing checkout;
  - a sibling that is ignored;
  - no build when another module is active;
  - a McpCli-shaped manifest that validates.

  Make one local commit on `origin/main`, `feat(tool): build a run-scoped McpCli for McpCli runs`.
- [x] Pre-release proof, as in 1.10:
  1. Pack locally to a scratch feed.
  2. Install into a scratch tool path.
  3. Run `debug` from the Platform root with a netcoredbg breakpoint.
- [x] `PlatformServerBindings.cs` and `BuildsSourceStartupTests.cs` -- map `mcpcli` to `Hexalith.McpCli`, and add a gated live `mcpcli` case that expects `["eventstore"]`.
- [x] `README.md` -- add the McpCli row, plus a recipe that launches the run-scoped build under the debugger. A CLI is short-lived, so it is launched, not attached.
- [ ] *(Orchestrator, after review)* -- release and pin:
  1. Push McpCli and Builds, wait for green CI, and dispatch `build-release.yml` for the gate approval.
  2. In Platform, pin the release, move the Builds gitlink to its tag, stage the McpCli gitlink, and update the README tool record.
  3. Rerun AC1 with the pinned tool.
- [x] Disposable McpCli clone in the scratchpad -- run the pinned tool as far as identity resolution; no environment start is needed. Record the Platform identity from the run's source-mapping record (`Workspace/PlatformIdentityResolver.cs`). It must equal the clone's `references/Hexalith.Platform` commit, and it is recorded as non-Platform evidence.
- [x] `_bmad-output/implementation-artifacts/evidence/1-11/` -- record (pre-release run; the orchestrator adds the pinned-tool rerun):
  - the tool version and the canonical revisions;
  - the run id and the DLL path;
  - the PDB source paths under `references/Hexalith.McpCli`;
  - the breakpoint file:line and its hit output;
  - the `down` result showing the build removed.

**Acceptance Criteria:**
- Given the Platform root with `HEXALITH_DAPR_HOME` set, when the pinned tool runs `debug` on the McpCli manifest, then:
  - the environment reaches ready;
  - the output names a run-scoped Debug build of `references/Hexalith.McpCli`;
  - a breakpoint is hit while that build runs `modules` offline;
  - `down` removes the build.
- Given the McpCli declaration, when validated, then it passes; when an `mcp` interface is added, validation fails.
- Given a sibling `../mcpcli`, when the tool resolves and builds, then only `references/Hexalith.McpCli` is used.
- Given a McpCli clone with its own `references/Hexalith.Platform`, when the tool runs there, then it reports that submodule commit as the Platform identity, and the evidence marks the run as non-Platform.
- Given each README, when read, then McpCli's own-repository runs are scoped to its own tests.

## Implementation Notes

- **Review fixes (amended into both commits).** Builds is now `cf978100f7668de135eb8a2adcd535e21b26471b` (was `d7c828a`) and McpCli `943b75ce85cbedc83b73f3b90277dcd3d4fae83b` (was `13568c0`). The Platform McpCli gitlink is restaged at `943b75c`. The changes:
  - **Builds:**
    - The McpCli build passes `-p:CentralPackageTransitivePinningEnabled=true`. A real Debug build has 0 warnings and restores with transitive pinning (`evidence/1-11/transitive-pinning-build.txt`).
    - `HXR020` keeps the full, already-bounded exception message after "McpCli run-scoped build failed: ".
    - `HXI010` (field `mcpcli`) names both the DLL and the mapped project path.
    - `CompositionDeclarationClosure.Repository("mcpcli")` returns `Hexalith.McpCli`.
  - **Builds tests:** a `linked-outside` resolution case (a symlinked project outside the checkout gives `HXW004`), a real failed-build case, and an McpCli deadline case (`HXR021`, bounded, no model build, workspace removed). The ready case asserts the project path. `DeclarationClosureTests` asserts `Repository("mcpcli")`. Focused runs: 22/22 and 4/4.
  - **Platform:** the default-run `McpCliDeclarationTests` validates the real McpCli manifest and checks that `Prepare` with `mcpcli` yields only `eventstore`; it passes. The README covers `run`/`debug`/`test`, `HXW004`/`HXR020`, no package-mode build (Epic 3), the EventStore gateway for connected execution (Epic 3) with offline verbs only, and a function breakpoint with the `--pending` note (verified with netcoredbg).
  - **McpCli:** the README links the Platform recipe.
  - **Evidence:** the PDB wording names the two PDBs inspected, `live-tests.txt` is annotated, and `validation.txt` is reproducible.
  - **Not rerun:** the pre-release proof, the live tests and the clone run ran at `d7c828a`/`13568c0` and were not repeated; the coordinator runs full verification.
- **Post-review verification (orchestrator, 2026-10-19).** The Platform Builds gitlink is staged at `cf97810`, because source mode requires the staged gitlink to equal the checkout.
  - Full Builds suite: 1093/1093.
  - Full Platform Composition suite: 203 run, 11 failed, 7 skipped. All 11 failures belong to the known 1.10 set, so none is new, and `McpCliDeclarationTests` passes.
  - The gated live McpCli case passes 1/1 (56 s) with the patched engine. The containers are back to baseline and the checkout is clean.
  - Record: `evidence/1-11/post-review-verification.txt`.
  - The pinned-tool AC1 rerun still follows the release.
- **Commits (local, not pushed).** McpCli `13568c0bc56fc42909c620847915b993483c594d` (now `943b75c`) `feat(module): declare McpCli for Platform runs` on `447085c` (declaration and README). Builds `d7c828a197d3bfb4762301a4d4c341a8c8701bfb` (now `cf97810`) `feat(tool): build a run-scoped McpCli for McpCli runs` on `origin/main` `b435f2e`; the Builds checkout's local `main` was fast-forwarded to `origin/main` first. Both messages pass the owning repository's pinned commitlint (`evidence/1-11/*-commitlint.txt`). Platform changes are uncommitted; only the McpCli gitlink is staged (at `13568c0`), because source mode refuses a direct reference whose HEAD differs from its staged gitlink.
- **Builds design.** `PlatformManifestCommand` gains `BuildsMcpCli` (active module `mcpcli` and source mode), `McpCliProjectPath`, `McpCliToolPath`, `BuildMcpCliAsync`, and the internal `McpCliBuildArguments`/`ResolveMcpCliProject`. The pinned-catalog check and the four catalog arguments are now shared with `BuildModelAsync`, whose messages are unchanged. McpCli resolves only through the mapping entry whose property suffix is `McpCli` (the `HexalithMcpCliRoot` entry); a missing entry, an uninitialized checkout or a missing project raises `WorkspaceMappingException` with `HXW004` (field = the expected project path, message names `Hexalith.McpCli`), which the engine's existing handler turns into a pre-launch `unavailable` refusal. A failed build raises `InvalidOperationException`, reported as `HXR020` "McpCli run-scoped build failed: ..." with the project as field and teardown. The build runs `dotnet build <project> --configuration Debug --artifacts-path <workspace>/tools/mcpcli -p:GeneratePackageOnBuild=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0 -nodeReuse:false -p:UseSharedCompilation=false` plus the pinned-catalog arguments, from the mapped McpCli root. The engine builds McpCli right after the mapping is materialized and before the Platform model, so a missing checkout fails before the model build. `CompositionRunPlan.McpCliToolPath` (additive, `plan.json` `mcpCliToolPath`) carries the path to the ready diagnostic: `HXI010` field `mcpcli`, "Run-scoped McpCli Debug build '<path>'; ...". It is reported for `run` and `debug`. `down` deletes it with the workspace. Test seam: internal `CompositionEngineOptions.McpCliBuildOverride`; the two existing `StartupCoordinationTests` that start an `mcpcli`-active engine set it. The Builds README documents the behavior.
- **Tests.** 19 new `McpCliRunBuildTests` cases: build arguments; `BuildsMcpCli` matrix (other modules, package mode); unmapped/uninitialized/project-less checkout with a sibling present; mapped checkout selected over a sibling; a real `dotnet build` of a fake McpCli through `WorkspaceGitFixture` (Debug, pinned catalog observed, artifacts under the run workspace, checkout and guarded sibling unchanged, no `.nupkg` despite `GeneratePackageOnBuild=true` in the project); McpCli-shaped declaration valid and `HXP023` with an `mcp` interface; engine ready path (`HXI010`, plan, `down` deletes the tool); EventStore and Memories owner manifests never build McpCli; missing checkout refuses before the model build; build failure `HXR020`. Full suite 1090/1090; baseline at `b435f2e` 1071/1071 (`evidence/1-11/unit-tests.txt`).
- **Pre-release proof (passed).** `4.34.0-local.1` packed from a clean scratch clone of `d7c828a`, loader gate passed, installed into a scratch tool path. From the Platform root, `debug` reached ready (run `493239030fb3522402ccfed7d355fca0`), `HXI010` named the run-scoped DLL, netcoredbg 3.2.0 launched it with `modules` and hit `CatalogService.cs:12`, the checkout stayed clean (`status --short --ignored` empty), and `down --run-id` removed the workspace and the DLL (`evidence/1-11/prerelease-proof.txt`). The gated live McpCli and EventStore cases passed 2/2 with the source-built engine (`evidence/1-11/live-tests.txt`).
- **Disposable clone (non-Platform evidence).** A scratch clone of McpCli `13568c0` with its 17 direct references pointed at local checkouts ran the pinned `4.33.1` until the run recorded `source-mapping/mapping.json`, then was interrupted (`HXC130`, no environment started). The recorded identity is `2ac8e73c09fe631c5620723b54784d2e28e8941f`, equal to the clone's `references/Hexalith.Platform` gitlink, with `IsPlatformRepository: false` (`evidence/1-11/clone-identity.txt`).
- **Platform.** `PlatformServerBindings.Repository("mcpcli")` returns `Hexalith.McpCli`. `BuildsSourceStartupTests` adds `TechnicalMcpCliStartsThroughTheRealBuildsEngineAsync` (servers `["eventstore"]`, plus the tool path in the plan, on disk and in `HXI010`); other cases assert no tool path. README: McpCli row, prerequisites, commands, build behavior, "Launch McpCli under a debugger", and the own-repository scope.
- **Not done here (orchestrator, after review).** Push McpCli and Builds, green CI, dispatch `build-release.yml` (production approval), pin the release in `.config/dotnet-tools.json`, move the Builds gitlink to its tag, update the README tool record (which currently says `4.33.1` does not build McpCli), and rerun AC1 with the pinned tool.
- **Workspace observations.** A concurrent session (Works story 4.16 verifier) modified `apphost.cs`, `docs/works-host-parity.md` and `eng/*verify_works_host.py`, and repeatedly held the fixed Dapr control-plane ports; live runs here waited for 45 s free windows. The Platform index's Builds gitlink was staged by another process, first at `b435f2e` and then at `d7c828a` (this story's unpushed commit); this story did not stage it and left it as found. Review pass 1 (BH9) later added the `mcpcli` arm to Builds `Repository()`.

## Spec Change Log

## Review Triage Log

Review pass 1, 2026-10-19. Three layers reviewed one combined diff:
- Blind hunter (BH) and edge-case hunter (EH).
- Verification gap (VG); its gap findings arrive pre-verified.

The diff covered:
- Platform story paths at `f0e3316` plus the working tree;
- Builds `b435f2e..d7c828a`;
- McpCli `447085c..13568c0`.

| ID | Finding | Verdict | Evidence | Route |
|---|---|---|---|---|
| BH1 | The run-scoped build drops McpCli's `CentralPackageTransitivePinningEnabled` | medium | `-p:ImportDirectoryPackagesProps=false` skips McpCli `Directory.Packages.props:4`, and the Builds catalog sets only `ManagePackageVersionsCentrally`. The GHSA-334f pin (`Props/Directory.Packages.props:224`) applies only with transitive pinning on, so the debugged build can resolve different transitive versions than McpCli's own build. | patch |
| VG1 | No test bounds the McpCli build stage by the startup deadline | medium (gap) | Every `McpCliBuildOverride` returns or throws at once. The model stage has `PlatformPreparationUsesRemainingDeadlineAsync`; McpCli has none. | patch |
| VG2 | The real `BuildMcpCliAsync` failure branch is never run | medium (gap) | The only real-build test is the success path, and the `HXR020` test replaces the builder. Making the check always return the tool would pass every test. | patch |
| VG3 | No test checks that the tool project stays inside the mapped checkout | low (gap) | No test covers a link inside the checkout. Replacing `TryResolveExistingFile` with `File.Exists` would pass, which weakens AC3. | patch |
| VG4 | The real McpCli declaration is checked only by a skipped live test and an inline copy | medium (gap) | No non-skipped test loads `references/Hexalith.McpCli/module/hexalith.module-manifest.v2.json`. A drift in its servers or dependencies would go unnoticed. | patch |
| BH9 | The Platform `Repository("mcpcli")` fix has no default-run test, and Builds still maps it to `Hexalith.Mcpcli` | low | Builds `CompositionDeclarationClosure.cs:57-63` has no `mcpcli` arm, and `CompositionDeclarationOptions.cs:73,96` derives names from it. Every current use is case-insensitive. | patch |
| BH14/EH2 | `HXR020` re-bounds an already bounded message and drops the project prefix | low | `CompositionEngine.cs:180` takes the last 512 characters of a message that is already about 1 KB, so the "McpCli '<project>' could not build" prefix is lost. The field still names the project. The fix is a direct correction. | patch |
| EH5 | `HXI010` names only the DLL, not the checkout it was built from | low | AC1 says the output names a run-scoped build *of* `references/Hexalith.McpCli`. The message (`CompositionEngine.cs:1099`) omits the project path. | patch |
| BH5/EH3 | Package mode silently builds no McpCli | low | `BuildsMcpCli` returns false for package mode, but only the Builds README says so. Fix with one Platform README sentence (no new diagnostic; package mode is Epic 3). | patch |
| BH13 | The READMEs disagree on which commands build McpCli and omit the rule IDs | low | Builds says `run`/`debug`/`test`, Platform says `run`/`debug`, and Platform does not name `HXW004`/`HXR020`. | patch |
| BH11 | The debugger recipe uses a line breakpoint and does not explain the `--pending` warning | low | `prerelease-proof.txt:434` shows the expected `--pending` warning. A function breakpoint survives source edits. | patch |
| BH12 | The docs never say what the started gateway is for in an offline debug session | low | Neither README says that connected execution against the gateway is Epic 3. | patch |
| BH10a | The McpCli README does not link the Platform recipe it refers to | low | "the Platform README has the recipe" has no link. | patch |
| BH10b | The READMEs name no tool version that builds McpCli | low | The post-review release task updates the README tool record. The orchestrator names the pinned release in both READMEs then. | patch (orchestrator, at pin) |
| BH10c | The absolute GitHub manifest link 404s before push | false | It follows the McpCli README's existing absolute-URL convention (the integration spec link) and resolves once pushed. | reject |
| BH6 | The evidence calls the PDB lists complete but covers 2 of the 4 first-party assemblies | low | `prerelease-proof.txt` lists only `Hexalith.McpCli` and `.Core`; `.Mcp` and `.Abstractions` are absent. | patch |
| BH7 | The live-test evidence lacks a before-containers line, has dangling markers, and leaves unexplained mTLS containers | low | At triage, `docker ps -a` shows only the four baseline containers. The record does not say so. | patch |
| BH8 | `validation.txt` cannot be reproduced as recorded | low | The pre-release commands are missing, the negative manifest delta is not recorded, and the masked paths are malformed. | patch |
| BH2 | The Platform test needs Builds `McpCliToolPath` before the gitlink moves | low | The orchestrator lands the Builds gitlink and the Platform paths in one commit at the release tag. The fix would edit the spec or process only. | reject |
| BH3 | The tool path assumes a single TFM with no RID | low | McpCli single-targets `net10.0` (`Directory.Build.props:12`). Querying `TargetPath` adds complexity for an unlikely case. | reject |
| VG5/BH4/EH1 | A process timeout is reported as `HXR020`, not `HXR021` | low | Both timers fire at the same deadline, for a build of about 4 s against 600 s. This matches `BuildModelAsync`'s existing handling (`CompositionEngine.cs:193-197`), and the fix adds branches. | reject |
| BH14b | A pinned-catalog failure is labelled a build failure | low | The catalog is materialized immediately before (`CompositionEngine.cs:168`), which matches the model path. The fix adds a branch. | reject |
| BH15 | Test assertions are thinner than the behaviour | false | No product defect is shown. The arguments are present in `McpCliBuildArguments`, and the plan value is asserted through the deserialized `session.Plan.McpCliToolPath`. | reject |
| EH4 | `down` while a user-launched McpCli holds the DLL | low | Linux deletes open files. Windows is not a supported dev host here (1.10 pass 3). `down` stops only run-owned resources by contract. Detection adds complexity. | reject |

## Design Notes

AR-46 makes the tool own the build ("the Platform tool builds a run-scoped, never-published McpCli"). A Platform-model resource that builds and exits would break readiness, and `dotnet run` writes into the checkout. `run` in source mode builds it too; package mode is Epic 3. Breakpoint recipe:

```bash
netcoredbg --interpreter=cli -- dotnet <run>/tools/mcpcli/bin/Hexalith.McpCli/debug/Hexalith.McpCli.dll modules
# break /abs/references/Hexalith.McpCli/src/Hexalith.McpCli.Core/Catalog/CatalogService.cs:12, then run
```

## Verification

**Commands:**
- `dotnet exec` on the Builds `Hexalith.Builds.Module.Tests` DLL, run with `-nodeReuse:false` builds -- expected: the new tests pass, and failures match the baseline.
- `dotnet tool run hexalith-module -- debug --manifest references/Hexalith.McpCli/module/hexalith.module-manifest.v2.json` -- expected: ready, with `HXI010` naming the McpCli DLL.
- `git -C references/Hexalith.McpCli status --short --ignored` -- expected: no `bin`/`obj` after the run.
