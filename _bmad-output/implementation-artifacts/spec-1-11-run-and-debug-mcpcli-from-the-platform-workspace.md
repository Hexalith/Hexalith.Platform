---
title: '1.11 Run and debug McpCli from the Platform workspace'
type: 'feature'
epic: 1
story: 11
created: '2026-10-19'
status: 'in-progress'
baseline_commit: 'f0e33167fcdb5f539da159c54f7505b72a65c9b2'
mcpcli_baseline_commit: '447085c42ec606f642240c4cae0415e7c2f97467'
builds_baseline_commit: 'b435f2e3b3892e03f69322d24de6003b0baf7a7d'
route: 'dispatch'
review_loop_iteration: 0
release_run_id: 39363818307
release_source_commit: '24322f56177b8fe86a78b2be7125f873c0a682bd'
release_status: 'waiting-production-approval'
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

### Review Findings

Review pass 2, 2026-10-20. Four layers (blind hunter BH, edge-case hunter EH, verification gap VG, acceptance auditor AA) reviewed Platform `f0e3316..39be561` (story paths), Builds `b435f2e..cf97810` and McpCli `447085c..943b75c`. During the review a concurrent `/pushall` pushed Builds `cf97810` and rebased McpCli `943b75c` onto `origin/main` as `742f5b6` (patch-identical, `git range-diff`), and Platform `c395e4a` moved the McpCli gitlink to `742f5b6`.

- [x] [Review][Patch] The McpCli build's pinned-catalog refusal is untested where McpCli calls it (VG1, medium gap): add `McpCliBuildRefusesMissingOrAlteredCatalogAuthorityAsync` mirroring `PlatformModelCatalogTests.cs:113` but calling `BuildMcpCliAsync`, asserting the refusal and no `tools` output [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/PlatformManifestCommand.cs:148] — fixed in Builds `24322f5`: 7 drift cases; 29/29 focused, 1100/1100 full
- [x] [Review][Patch] Both READMEs say `test` builds McpCli, but `test` refuses the McpCli manifest with `HXC002` before the engine starts, because it declares no `tests` (`ModuleCommandExecutionService.cs:1045-1048`) (AA5, low) [README.md:155] — fixed: both READMEs say `test` needs `--profile` naming a declared test and builds nothing for McpCli; the live check found `HXC001` without `--profile` and `HXC002` with one (`evidence/1-11/review-pass2-debug.txt`)
- [x] [Review][Patch] The evidence README's revisions are stale: it says Platform is uncommitted, Builds and McpCli are local and unpushed, McpCli is `943b75c`, and Platform HEAD commits Builds `4aaeed8` (BH12, AA7, VG, plus the residual of BH1/AA1) [_bmad-output/implementation-artifacts/evidence/1-11/README.md:3] — fixed: the status, revisions (pushed state, `742f5b6` as the patch-identical rebase of `943b75c`, Builds `24322f5`) and the `HXW009` note are current
- [x] [Review][Patch] The `HXW004` reason and hint are wrong for the project-missing and linked-outside cases: the hint only says to initialize the checkout, and the reason never mentions a link outside it (BH7, low) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/PlatformManifestCommand.cs:214] — fixed in Builds `24322f5`: the reason names the missing project and a project that resolves outside the checkout, the hint asks for a commit whose project lies inside it, and the test asserts both per case
- [x] [Review][Patch] The README's function-breakpoint recipe (`break Hexalith.McpCli.Core.Catalog.CatalogService.ListModules`, `--pending` then bound) is not in the evidence, which records only the `CatalogService.cs:12` line breakpoint (BH11, AA8, low) [README.md:209] — fixed: `evidence/1-11/review-pass2-debug.txt` ran the recipe verbatim against a live `debug` with the Builds CLI built from `fc5cfc0` (`--pending`, then bound, hit at `CatalogService.cs:11` from `CliRunner.cs:330`); the same run records the post-review `HXI010` text with the project path
- [x] [Review][Defer] Release and pin are still open: pinned `4.33.1` builds no McpCli, the Platform Builds gitlink is at untagged `cf97810`, and AC1 has no pinned-tool record and no record of the post-review `HXI010` text (BH2, AA2, AA3, medium) [.config/dotnet-tools.json:6] — deferred: tracked by the open orchestrator task "release and pin" above; runs after this review
- [x] [Review][Defer] The Platform model build also drops Platform's `CentralPackageTransitivePinningEnabled`, because `PinnedCatalogArguments` skips `Directory.Packages.props:2` (BH6, medium) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/PlatformManifestCommand.cs:93] — deferred: pre-existing in `BuildModelAsync`; not caused by this change
- [x] [Review][Defer] Real-process cancellation of `BuildMcpCliAsync` and `BuildModelAsync` is untested; only the build overrides are cancelled (VG2, gap) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/PlatformManifestCommand.cs:152] — deferred: one real-process cancellation test should cover both builders; the model-build gap is pre-existing

Rejected:
- BH1/AA1 (Platform's gitlink names an McpCli commit on no remote) — false now: `c395e4a` (pushed as part of `13d06bc`) moved the gitlink to `742f5b6`, which is on McpCli `origin/main`; the stale hashes in the evidence are a patch above.
- BH3/EH5 (package mode builds no McpCli and says nothing) — low: decided in pass 1 (BH5/EH3), and the README says so; package-mode McpCli is Epic 3; a diagnostic adds a branch.
- BH4/EH1/VG (a process `TimedOut` is reported as `HXR020`, not `HXR021`) — low: both timers fire at the same deadline, for a build of about 4 s against 600 s; this mirrors `BuildModelAsync`; pass-1 rejection (VG5/BH4/EH1) stands; the fix adds branches.
- EH2 (`dotnet` fails to start, giving an empty `HXR020` reason) — low: the tool itself runs under `dotnet`; this mirrors `BuildModelAsync`; the fix adds a branch.
- BH5 (a RuntimeIdentifier or multi-targeting moves the DLL) — low: pass-1 BH3 rejection stands; McpCli single-targets `net10.0`.
- BH8 ("direct reference only" assumed; a suffix collision is misreported) — false: entries come only from the active root and direct `.gitmodules` references (`WorkspaceRootResolver.cs:132-149`), and collisions are refused as `HXW002` (`:170-173`).
- BH9 (the `mcpcli` repository map is maintained in three places) — low: the duplication predates this change; both maps now agree, and each has a test.
- BH10 (the live Platform test leaves out the main claims) — false: `McpCliRunBuildTests` checks the project path in `HXI010` (`:276`), that `down` deletes the DLL and the workspace (`:282-284`), and that the checkout is unchanged (`:176`).
- BH13 (the remaining AC1 work has no tracked follow-up) — false: the orchestrator task is open `[ ]`, and `sprint-status.yaml` is `review`.
- BH14/EH7 (`McpCliDeclarationTests` is ungated and can throw `NullReferenceException`) — false: it fails loudly on an unreachable layout, using the same idiom as `OwnerOptionBindingTests.cs:15-17` and `PlatformAppModelTests.cs:28-30`, which also read `references/*` ungated.
- BH15/EH4 (the `HXR020` message is not re-bounded, and a catalog failure is blamed on McpCli) — low: the build message is already bounded; pass 1 removed the re-bounding (BH14/EH2) and rejected the labelling (BH14b).
- BH16 (evidence formatting: local times, absolute paths, an unproven "unchanged since") — low: the raw captured output is kept verbatim, and `git diff d7c828a cf97810 -- Manifest schemas` is empty, so the claim holds.
- BH17 (the deadline test depends on wall-clock time) — low: it uses the same 2 s/8 s bounds as `StartupReadinessTests.cs:712-725`; no flake has been shown.
- EH3/EH8 (a referenced project symlinked outside the checkout, and the "never a sibling" claim) — low: unlikely; checking every referenced project adds complexity.
- EH6 (a `;` in the workspace path splits the `-p:` values) — low: unlikely, and shared with the model build; escaping adds complexity.
- AA4 (the spec says `done` before the cross-repository done criteria are met) — the fix edits the spec under review; `sprint-status.yaml` stays authoritative.
- AA6 (other exceptions reach the generic `apphost` handler) — false: the reachable exceptions are `WorkspaceMappingException`, `InvalidOperationException` (the catalog check is wrapped) and `OperationCanceledException`; a process that fails to start returns a result, not an exception.
- AA9 (the story commit includes an `epic-1-context.md` rewrite) — false: it is this spec's own `context` document, recompiled for the story.

## Implementation Notes


- **Resumed release handoff (2026-10-20).** At Platform 23a35074b10f999946ed5fdd621e4bb60af73de8, fresh focused verification passed (Builds 29/29; Platform actual McpCli declaration/casing 1/1), and review pass 3 found no new McpCli-run product patch. Builds 24322f56177b8fe86a78b2be7125f873c0a682bd is current remote main and push CI 39360743339 passed. McpCli's reviewed checkout 742f5b6ead66937f62025b73d77452e0f8126658 has successful CI; remote main has since advanced to documentation-only 0779fd98df7bffcfde79a8ce121076390c30284c, so this run did not move or push that checkout. The exact-source/CI release guard passed, and the orchestrator dispatched build-release.yml as run 39363818307. Its source-check job passed; production approval is pending with the human, as the frozen decisions require. No approval API was invoked. See [release-handoff.json](evidence/1-11/release-handoff.json). The tool remains pinned to 4.33.1 until publication, so the release/pin task and pinned-tool AC1 remain incomplete. Earlier local/unpushed notes below describe historical execution states.

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


### Review pass 3 — resumed build, 2026-10-20

Three independent reviewers inspected the combined story diff, including Builds and McpCli changes from the recorded baselines. Fresh checks passed: Builds McpCli matrix 29/29 and Platform real declaration/casing 1/1. No new McpCli-run product patch is required. The release/pin task remains open and is being executed by the orchestrator; the spec is not complete.

| ID | Finding | Verdict | Evidence and route |
| --- | --- | --- | --- |
| P3-BH1 | Pinned 4.33.1 cannot build McpCli | medium | Carried from pass-2 BH2/AA2/AA3: the manifest still selects 4.33.1. Retain the open release/pin task and existing ledger entry; no duplicate deferral. |
| P3-BH2 | Raw MSBuild Include expressions can reject approved test references | medium | Verified DependencyPolicyTests.EvaluateReferences concatenates raw project XML with evaluated Debug/Release items; DependencyPolicyGate.CheckTestProjects then rejects the unresolved expression. The change belongs to pre-existing McpCli story 4.7 commit f2242b76e117c93f3bcf77320ca5d133a61e9308, preceding this story's declaration commit. Defer to its owner. |
| P3-BH3 | Inactive forbidden references in imported props evade the literal scan | medium | Verified the fallback reads only XDocument.Load(project), while the MSBuild query omits items whose conditions are false. Imported inactive items are absent from both sets. Pre-existing McpCli story 4.7 behavior; defer with its own regression case. |
| P3-BH4 | Real-process cancellation is untested | medium | Carried from pass-2 VG2: real builder tests finish normally; the deadline test substitutes the builder. Keep the existing shared cancellation/deadline ledger entry without duplicating it. |
| P3-BH5 | Build process timeout can report HXR020 rather than HXR021 | low | Carried from pass-2 BH4/EH1/VG and pass-1 VG5/BH4/EH1: simultaneous timers can race, matching the existing model builder. Reject under the recorded unlikely-case/added-branch decision. |
| P3-BH6 | Referenced source links can reach a sibling | low | Carried from pass-2 EH3/EH8: executable-project containment is checked; checking the entire project/source graph adds complexity for an unlikely case. Reject under the recorded decision. |
| P3-BH7 | Model build omits transitive pinning | medium | Carried from pass-2 BH6: BuildModelAsync retains its pre-existing catalog arguments. Keep the existing ledger entry; no duplicate deferral. |
| P3-BH8 | Cached epic context mandates obsolete SDK/Aspire pins | low | Verified context lists 10.0.401/13.5.4 while global.json and accepted-tool evidence use 10.0.402/13.6.1. A later planning/implementation agent could copy obsolete versions. Defer because the fix edits agent context, per the review workflow. |
| P3-BH9 | Cached Works decision omits the Development restriction | low | Verified WorksPreviewModel still rejects non-Development environments, but the compiled context names only the feature switch. A later agent could remove the guard on that basis. Defer because the fix edits agent context. |
| P3-BH10 | Spec state and historical execution records disagree | low | The release task is still incomplete and sprint status remains in-progress; old implementation notes describe earlier runs. Reject the requested spec-only fix under the review rule. Current revalidation evidence records canonical revisions and the remaining handoff. |
| P3-EH1 | Approved property-based Include is rejected as raw text | medium | Same verified behavior as P3-BH2; the review's EvaluateInclude guard snippet is absent from the actual source. Group with P3-BH2's pre-existing policy issue and defer once. |
| P3-EH2 | Process timer can win the startup cancellation race | low | Carried from pass-2 EH1/BH4 and pass-1 EH1/VG5. Keep the recorded rejection; no new implementation change. |
| P3-EH3 | Pinned command starts servers without building McpCli | medium | Carried from pass-2 BH2/AA2/AA3. Existing release/pin task remains open; no duplicate deferral. |
| P3-VG1 | Real build token/deadline forwarding is not exercised | medium | Pre-verified gap; carried from pass-2 VG2, already tracked in the shared real-process cancellation/deadline entry. Defer there without a duplicate. |
| P3-VG2 | Released pinned-tool acceptance evidence is absent | medium | Carried from pass-2 release/pin finding. The orchestrator must release, pin and rerun AC1 before completing this spec. |


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
