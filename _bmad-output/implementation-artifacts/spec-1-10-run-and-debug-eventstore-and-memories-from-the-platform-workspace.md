---
title: '1.10 Run and debug EventStore and Memories from the Platform workspace'
type: 'feature'
epic: 1
story: 10
created: '2026-10-18'
status: 'done'
baseline_commit: '79935099e9b4c6fdcffe29f84d42b8c92b50fc2b'
eventstore_baseline_commit: 'c0c08d450dff89faaeea2bb88dd570a589ce203d'
memories_baseline_commit: 'fb39a0c59cc3befd3176dec5e3c790f94d0e5bd1'
builds_baseline_commit: '4629ff2d3bd66376b29a553663c241d3a7b87853'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Nothing proves that `hexalith-module debug`, run from the Platform root, runs the `references/Hexalith.EventStore` and `references/Hexalith.Memories` checkouts in Debug, or that a breakpoint in them is hit. EventStore's published `RepositoryProjectPaths` still probes seven current, ancestor, sibling and root-level locations, and silently falls back to a path that does not exist. Memories' README presents its own AppHost as the way to run Memories.

**Approach:** Run `debug` from the Platform root on each owner manifest, fix whatever blocks it in the repository that owns the blocker, and prove a breakpoint hit in each checkout. Replace the helper's probing with resolution that matches the source mapping and fails naming the dependency and the path. Document that a technical module's own AppHost serves only that repository's tests.

## Boundaries & Constraints

**Always:** Run from the Platform root with the pinned tool: `dotnet tool run hexalith-module -- debug --manifest references/<Repo>/module/hexalith.module-manifest.v2.json`. Never pass the Platform copies in `Declarations/`: the tool already loads the owner manifest, so a copy is a duplicate (HXP003/HXP020). Keep the declared selections: EventStore runs {eventstore}; Memories runs {memories, eventstore} with no Tenants. Fix blockers in the repository that owns them. EventStore and Memories changes stay as local commits. Pushing triggers their release pipelines, so pushes and Platform gitlink bumps wait for the human's approval. Stage only this story's paths.

**Never:** Change Builds or the tool pin beyond the Builds fix decision below; this includes reporting the app process id instead of the launcher's (deferred 1.8 decision 2C). If any other blocker needs a Builds change, stop and ask. Refresh the Platform declaration copies or `provenance.json`, which only package mode reads. Edit `Directory.Build.props` file-existence probing; that is module adoption (AR-50). Change the Tenants, Timesheets, Conversations or Folders AppHosts. Count an own-repository AppHost run as evidence.

**Decisions (2026-10-18):**
- **Resolution rule.** `GetReferencedModuleProjectPath` mirrors the mapping. If `<root>/<Module>.slnx` exists, the dependency is the consuming repository itself and resolves under that root. Otherwise it resolves to `<root>/references/<Module>`. Any other case throws, naming the dependency and the expected path. Consumer AppHosts are not edited.
- **Memories copy.** Memories' internal `RepositoryProjectPaths` gets the same rule in this story.
- **Breakpoint proof.** Claude proves it headlessly with netcoredbg (MIT), downloaded to the scratchpad and never committed, attached to the app process under the reported launcher. If netcoredbg cannot attach to .NET 10, the human attaches VS Code and confirms.
- **Size.** Keep the full spec (about 2.4k tokens). The three ACs share one run setup.
- **Builds fix (human, 2026-10-18, superseding "never change Builds or the 4.33.0 pin").** The published 4.33.0 cannot start any v2 composition, which blocks AC1. 1.10 fixes the two packaging defects in Builds as one local `fix:` commit on top of Builds `main` (`4629ff2`):
  - the hosts' Tooling identity, already fixed by `6e8b71e`;
  - the packaged AppHost's baked `/home/runner` paths.
  After review, Claude pushes, the human approves the `build-release.yml` production gate, and Platform pins the released version (expected 4.33.1). AC1 is then proven with that pinned tool. Nothing else changes in Builds.
- **Additional Builds metadata repair (human, 2026-10-18, resumed run).** Approved refresh of the four changed package-audit families and any derived version-catalog metadata needed to unblock CI, keeping the selected dependency versions unchanged. The owner concurrently pushed this repair as `332b2c144defba546fe0a09626a4a72f2340e22f`; validate and use that result instead of generating another refresh.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|---|---|---|---|
| Debug EventStore | Owner manifest, `HEXALITH_DAPR_HOME` set | Ready; launcher process ids reported; a breakpoint in `references/Hexalith.EventStore` is hit | Unready resources named per 1.8 |
| Debug Memories | Owner manifest | Memories server, clock, telemetry, eventstore, redis-stack and falkordb ready; a breakpoint in `references/Hexalith.Memories` is hit | Same |
| Mapped dependency | Consumer root resolves the dependency where the mapping would | Helper returns that path | N/A |
| Unmapped layout | The dependency exists only as a sibling, ancestor or root-level copy | No path returned | Throws, naming the dependency and the expected path |

- **Formatting-only CI repairs (human, 2026-10-18, resumed run).** Approved the two PackagedSeedProbe conditional-expression formatting fixes and equivalent formatting-only CI fixes needed to continue the release. The owner already published the exact correction as `c6f56c011ce3a83cdf6100c322352daf125c6af9`; use that commit rather than creating a duplicate.

</frozen-after-approval>

## Code Map

Builds fix (`references/Hexalith.Builds`):
- `src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionEngine.cs:~1380-1425` builds the AppHost environment and runs `dotnet exec <AppHost.dll>`. `CompositionProcess.CreateStartInfo` (`Runtime/CompositionProcess.cs:55-90`) clears the inherited environment, so every override must be passed here explicitly.
- `Runtime/CompositionCommandOptions.cs:21-22` selects the packaged `g4-host/Hexalith.Builds.Module.AppHost.dll` unless `HEXALITH_G4_APPHOST` is set.
- The packaged host's `AssemblyMetadata` bakes CI paths: `dcpclipath`, `dcpextensionpaths`, `aspiredashboardpath`, `apphostprojectpath` and `apphostprojectbaseintermediateoutputpath` (`evidence/1-10/pinned-tool.txt`). Aspire 13.6.1 honours these overrides:
  - `DcpPublisher:CliPath` / `ASPIRE_DCP_PATH`
  - `DcpPublisher:DashboardPath` / `ASPIRE_DASHBOARD_PATH`
  - `AppHost:Directory`
  - `Aspire:Store:Path`
- The matching `aspire.hosting.orchestration.linux-x64` and `aspire.dashboard.sdk.linux-x64` 13.6.1 packages are in the local NuGet cache after the Platform model build.
- Resolve these paths at the catalog Aspire version, and give the store and AppHost directory a run-private writable path. If a required package is missing, fail with a named prerequisite diagnostic, never a fallback. The source-built host used by `BuildsSourceStartupTests` must keep working.
- `Tools/build-g4-tool-packages.ps1` and `Tools/test-g4-packaged-platform-loader.ps1` (from `6e8b71e`) version the hosts and gate the packaged loader with `--validate-platform` only.
- Release: as in Story 1.9, `build-release.yml` is `workflow_dispatch`, needs green push CI at that SHA and the `production` environment approval, and semantic-release tags the SHA.

Builds (read only otherwise, `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling`):
- `Runtime/CompositionDeclarationClosure.cs:81-89,136-171` loads the owner manifest and the `--manifest` file.
- `Runtime/CompositionPrerequisiteProbe.cs:61-72,158-206` checks `HEXALITH_DAPR_HOME`: `tools/dapr` holds CLI 1.18.0, and `.dapr/bin/{daprd,placement,scheduler}` hold runtime 1.18.2.
- `Workspace/SourceMappingMaterializer.cs:122-161` writes `Hexalith<Id>Root`. A direct reference's root is `<root>/references/<Identity>`.

Platform:
- `src/Hexalith.Platform.Composition/PlatformServerBindings.cs:11-19,55-68` maps the launched projects to mapped roots. Reuse it as is.
- `PlatformAppModel.cs:179-209` launches with `dotnet run --configuration Debug`. The process id it reports is the launcher's.
- `tests/Hexalith.Platform.Composition.Tests/BuildsSourceStartupTests.cs:10-23` is the gated live engine test, with EventStore and Tenants cases.
- `README.md:26` states a stale Aspire CLI floor ("13.4.6 or later"). The tool requires exactly 13.6.1.

EventStore (`references/Hexalith.EventStore`):
- `src/Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs:54-100` holds the probing. `GetProjectPath` and `GetRepositoryRoot` (fixed five-level layout) stay.
- Callers of the probing:
  - `EventStorePlatformProjectMetadata.cs:19,36,53`, through `AddHexalithEventStore{PlatformProjects,GatewayProject}`, used by the Memories, Tenants, Timesheets and Conversations AppHosts.
  - Tenants' `TenantsServerProjectMetadata.cs:26`.
- `tests/Hexalith.EventStore.AppHost.Tests/Configuration/RepositoryProjectPathsTests.cs:21-46` holds the current-repository and fallback cases. Ledger entries DW-205 and DW-206 (`_bmad-output/implementation-artifacts/deferred-work.md:1370`) close with this change.

Memories (`references/Hexalith.Memories`):
- `README.md:37,65-72` runs Memories through its own AppHost.
- All three servers expose `/ready`.

Machine:
- `HEXALITH_DAPR_HOME` is unset, and the installed Dapr is unusable for this: runtime 1.18.7 instead of 1.18.2.
- Docker and Aspire CLI 13.6.1 are fine.
- No headless managed debugger is installed. vsdbg is licensed for VS Code only.

## Tasks & Acceptance

**Execution:**
- [x] *(Environment, not committed)* -- provision an isolated Dapr home at `~/.hexalith/dapr/1.18.2` in the probe's layout -- `debug` prerequisite.
- [x] `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/{RepositoryProjectPaths,EventStorePlatformProjectMetadata}.cs`, `RepositoryProjectPathsTests.cs`, EventStore ledger -- apply the resolution rule, throw naming the dependency and the path, fix the doc comments, and test the self, mapped and unmapped cases. Close DW-205 and DW-206. -- AC2.
- [x] `references/Hexalith.Memories/src/Hexalith.Memories.Aspire/{RepositoryProjectPaths,*ProjectMetadata}.cs` and their tests -- apply the same rule and update tests that relied on the silent fallback (for example `HexalithMemoriesAspireSecretStoreTests.cs:39`) -- consistency.
- [x] Platform root -- run `debug` for EventStore, then Memories, and fix blockers in the owning repository -- AC1. **Passed:** released and pinned `4.33.1` reached ready for both owner manifests, hit both checkout breakpoints, and completed exact-run cleanup (`evidence/1-10/pinned-{eventstore,memories}.txt`).
- [x] `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/*`, Builds tests and `Tools/*packaged*` gate -- pass the Aspire DCP, dashboard, AppHost-directory and store overrides when starting the AppHost. Cover them with tests, and make the packaged gate start the packed host past Aspire store creation. Then pack the tool locally (for example `4.33.1-local.1` into a scratch feed), install it into a scratch tool path, and run `debug` from the Platform root for both manifests with a netcoredbg breakpoint, as pre-release proof. Make one local commit, `fix(tool): launch the packaged AppHost with local Aspire paths`. **Never push.** -- unblocks AC1.
- [x] *(Orchestrator, after review, remote)* -- push Builds, EventStore and Memories; wait for green Builds CI; dispatch `build-release.yml` for human gate approval; wait for NuGet.org. Then, in Platform: pin `.config/dotnet-tools.json` to the release, move the Builds gitlink to its tag commit, stage the EventStore and Memories gitlinks, and update the README's Module tool record (accepted version, 4.33.0 unusable for run/debug, remove the known-issue note). Rerun AC1 with the pinned tool and netcoredbg, and update `evidence/1-10/`.
- [x] `_bmad-output/implementation-artifacts/evidence/1-10/` -- prove the breakpoints with netcoredbg and record the tool version, canonical revisions, process ids, breakpoint file:line/hit output and `down` result. **Passed:** EventStore `ServiceCollectionExtensions.cs:225` (`path = "/ready"`) and Memories `FalkorDbHealthCheck.cs:28` (`context.Registration.Name = "falkordb"`), with NuGet.org tool `4.33.1+c6f56c011ce3a83cdf6100c322352daf125c6af9`.
- [x] `tests/Hexalith.Platform.Composition.Tests/BuildsSourceStartupTests.cs` -- add a Memories case with servers `eventstore, memories, memories-access-telemetry-clock, memories-access-telemetry`.
- [x] Platform `README.md`, EventStore `README.md`/`CONTRIBUTING.md`, Memories `README.md` -- document the run and debug commands, the prerequisites, how to find the app process under the launcher, and the exact Aspire CLI version. State that an own-repository AppHost serves only that repository's tests and is never Platform evidence -- AC3.

**Acceptance Criteria:**
- Given the Platform root with `HEXALITH_DAPR_HOME` set, when `debug` runs on the EventStore owner manifest, then it reaches ready from the Debug build of `references/Hexalith.EventStore`, a breakpoint there is hit, and `down` stops only that run. The same holds for Memories.
- Given the gated live tests, when they run, then the EventStore, Tenants and Memories cases pass.
- Given each README, when read, then it scopes the own-repository AppHost to that repository's own tests.

### Review Findings

Review pass 3, 2026-10-18. Four layers: blind hunter, edge-case hunter, verification gap and acceptance auditor. They reviewed one combined diff: Platform `7993509..cf20db9` (story paths), the story commits in EventStore `c0c08d4..d55c6f7` and Memories `fb39a0c..4f6b3b1`, and Builds `4629ff2..c6f56c0` without the generated audit, lock and fixture JSON.

- [x] [Review][Decision] AC2 and the verification evidence predate the final Platform state — `live-tests.txt` (14:37–14:42Z) records Platform, Builds, EventStore and Memories but not Tenants. Tenants was then at `57d564b4` (checkout reflog); the committed gitlink is now `c9feedb6` (`1fae05d`: 55 files, +4056 lines of unrelated Tenants work, SDK pin included), so the Tenants case never ran at the shipped state. `builds-resumed-verification.txt` is titled "at c6f56c0", but its live-test line cites the run at `332b2c1`. Memories later moved from `d0c5d8ca` to `4f6b3b1` (docs and nested gitlinks). The full Composition suite (12 baseline failures) last ran at the superseded state (`live-tests-historical.txt`). Options: rerun the gated class and the full Composition suite at `cf20db9`, or record the Tenants revision and reconcile the labels without rerunning. **Resolved (human, 2026-10-18): rerun at `cf20db9`** → patch.
- [x] [Review][Patch] EventStore README still finds the run id by newest state file (`ls -t … | head -1`), which contradicts the R2-B5 recipe in every other doc and can stop another run [references/Hexalith.EventStore/README.md:89]
- [x] [Review][Patch] EventStore and Memories run/debug docs omit first-run prerequisites the Platform README requires: the Builds and Tenants direct checkouts, `dotnet restore apphost.cs` (otherwise `HXR016`), `NUGET_PACKAGES`, and "stay attached, then `down`" [references/Hexalith.EventStore/CONTRIBUTING.md:123; references/Hexalith.Memories/README.md:80]
- [x] [Review][Patch] Platform README calls `4.33.0` "the first accepted version" and does not say it is now outside the range, after `cf20db9` raised the `hexalith-platform.json` floor to `[4.33.1, 5.0.0-0)` [README.md:33]
- [x] [Review][Patch] AC1 evidence cites Platform `5151bef`, whose `.config/dotnet-tools.json` pins `4.33.0`; the `4.33.1` pin was an uncommitted working-tree edit, committed later in `64cbd01` [_bmad-output/implementation-artifacts/evidence/1-10/README.md:16]
- [x] [Review][Patch] Evidence environment note merges two runs: the intermediate failure and retry (`live-tests-intermediate.txt`, 14:20Z/14:23Z, engine `303f86d`) and the final pass (`live-tests.txt`, 14:37Z, `332b2c1`), which had no failed first attempt [_bmad-output/implementation-artifacts/evidence/1-10/README.md:131]
- [x] [Review][Patch] Evidence claims an assembly identity for the EventStore hit, but `debugAssemblySha256` hashes `Hexalith.EventStore.dll`. `Extensions/**` is compiled into `Hexalith.EventStore.Gateway.dll`, where the frame is [_bmad-output/implementation-artifacts/evidence/1-10/pinned-eventstore.txt:35]
- [x] [Review][Patch] Evidence README does not link `live-tests-intermediate.txt`, `audit-validation.txt`, `catalog-validation.txt`, `seed-probe-build.txt`, the three `*-commitlint.txt` files or `pinned-tool.txt`, and does not mark `live-tests-intermediate.txt` superseded [_bmad-output/implementation-artifacts/evidence/1-10/README.md:137]
- [x] [Review][Defer] No automated check that the tool pin, `toolVersionRange` and the Builds gitlink tag agree [.config/dotnet-tools.json:6] — deferred: pre-existing gap, already recorded by the Story 1.9 second-pass entry (owner Builds or Story 2.8). The new `4.33.1` floor falls under the same check.
- [x] [Review][Defer] The run-id recipe has nothing to match after plain `run`, or when `debug` reports "launcher process id is unavailable" [README.md:144] — deferred: pre-existing Builds 1.8 behaviour, already recorded by the pass-1 B11b entry (print the run id in human output).

**Rejected (pass 3):**
- Blind: inotify exhaustion is undocumented — low: specific to this host's other sessions; a troubleshooting section is more than a correction.
- Blind: the helper docstring overclaims a match with the build-time `ProjectReference` — false: when the helper returns, its path is the props' first candidate (`references/` or self); in the other layouts it throws, so the paths never diverge.
- Blind: the Memories live case does not assert the Redis and FalkorDB containers — low, carried B9: Memories `/ready` includes those health checks.
- Blind: `332b2c1` collapses multi-line conditionals onto long lines — low: an owner-authored, approved repair; reformatting 20 Builds files is outside this story.
- Blind: nine Builds files lost their final newline — false: Builds `.editorconfig:16` sets `insert_final_newline = false` for `*.cs`.
- Blind: evidence cites `/tmp` logs and does not commit the harness — low: process-only; the cited scratch attempts are explicitly not counted.
- Blind: nothing ties the restored package hash to the release inventory — low: NuGet versions are immutable, and the informational version `+c6f56c0` names the commit.
- Blind: `release-progress.json` has a stale `lastCheckedUtc` and API boilerplate — low: raw capture; editing it would alter the record.
- Blind: ANSI escapes and duplicated lines in `qualification-blocker.txt` — low: raw capture, cosmetic.
- Blind: the docs cover only Linux — low: the team works on Linux/WSL2, and macOS/Windows recipes would need new research.
- Blind: tests are missing for the invalid-version and RID branches and the exception hint — low: no defect shown; adding tests is more than a correction.
- Blind: unit-test evidence names no revisions and leaves failures untracked — low: the failure sets are identical to baseline and come from nested submodules that are uninitialized by design.
- Auditor: three Builds `fix(tool)` commits instead of one — low: process-only; the two later commits are the B7c and B8-r review patches.
- Auditor: the release gate was approved under a delegated instruction — false: the approval records the owner's explicit instruction, which is sufficient for a sole owner.
- Auditor: story commits are mixed with unrelated work — low: process-only; these are the owner's own sync commits.
- Auditor: sprint status `review` against spec `done` — false: this review's step 6 syncs both (carried B4a).
- Edge: a recycled launcher pid could match a stale state — low: needs a pid and manifest-hash collision; a guard adds complexity.
- Edge: unset `HOME` gives a relative packages root — low, carried E8: the dotnet CLI itself needs `HOME`.
- Edge: a whitespace `AspirePackagesRoot` throws before the run exists — false, carried E6: only tests set it.
- Edge: the gate resolves `pwsh` from `PATH` — low: CI and normal installs have it on `PATH`; `[Environment]::ProcessPath` would be wrong for a dotnet-tool `pwsh`.
- Edge: the public helper now throws on a patch release — low, carried B5a.
- Edge: "mirrors the mapping" but nested checkouts fail — false, carried E13: intended by the frozen rule and AD-4.
- Edge: the gate never launches DCP — low, carried R2-B9: real compositions supply DCP execution evidence.

## Implementation Notes

- **Review pass 3 re-verification at exact `cf20db9`.** A disposable Platform worktree and local shared clones of all 17 root gitlinks isolated the requested run from concurrent edits in the active workspace. Clean Debug builds passed with zero warnings/errors. The gated class passed EventStore and Tenants (`c9feedb6`) but failed Memories on Sentry Running/Unhealthy (3 total, 1 failed, 451.333 s). Its unchanged-source focused Memories retry passed 1/1 in 101.792 s. All three distinct cases have passing evidence across two invocations; there was no single all-green class run. The first failure is preserved. All checkout revisions and the relevant binary hashes matched before/after each lane; all trees stayed clean. The four protected Dapr containers remained, and the temporary inotify cap was restored. See `evidence/1-10/{live-tests-final,live-tests-final-retry}.txt` and `final-verification.json`.

- **Review pass 3 full-suite comparison.** At the same committed tree, the full Composition suite ran 201 tests: 12 failed, 6 skipped (70.200 s). Eleven failures match the historical set; the formerly failing configured Gateway seed subscription now passes. The added failure is the existing explicit-declaration absolute-path substring assertion when the test output is under `/tmp`. The validator's relative-path formatting and the fixture are unchanged from baseline; the fixture creates no tool floor/pin files. The same byte-identical test output passes that focused assertion under `/home` (1/1, 0.317 s). No assertion or diagnostic behavior was changed. The complete failure-set comparison, unsuccessful shell-CWD experiment and successful relocation are linked from `evidence/1-10/README.md`.

- **Review pass 3 documentation and provenance patches.** EventStore local commit `6f16f79f306fc22b26e3f164b001bd86a92039d4` fixes launcher/manifest run selection and first-run prerequisites; Memories local commit `842bbe33c2473f87684a6283c3cc736bb8fad7d2` completes the same prerequisites and debugger lifetime. Both passed the owning repository's pinned commitlint before and after commit; neither was pushed, and the Platform gitlinks remain unstaged pending human approval. Platform now states the exact accepted range and excludes 4.33.0. Evidence discloses that the AC1 pin was a working-tree edit until `64cbd01`, distinguishes the intermediate and stable runs, identifies the EventStore executable hash accurately (no historical Gateway assembly hash was captured), and links all supporting records.

- **Released and verified.** Builds CI `39138234609` and approved Release `39139908836` both passed at `c6f56c011ce3a83cdf6100c322352daf125c6af9`; release tag `v4.33.1` resolves to that canonical commit. NuGet.org tool restore succeeded, Platform pins `4.33.1`, and the Builds gitlink matches the release tag. The production approval by `jpiquot`, qualified package publication and remote verification are recorded in `evidence/1-10/release-progress.json`; the duplicate dispatch from this resumed run was cancelled.

- **Final AC1 proof.** From Platform HEAD `5151bef5b68d6bd1f33d94f0a3a1b11d32dd48a6` with the uncommitted `4.33.1` tool-pin edit later committed in `64cbd01`, using Builds `c6f56c011ce3a83cdf6100c322352daf125c6af9`, EventStore `d55c6f7c11e9bce6f17d490a84c83f1561979d63` and Memories `4f6b3b1265d15d208761e6419e5c825d3f39fea3`, the exact pinned-tool commands reached ready and netcoredbg hit both checkout breakpoints. EventStore launcher/app `43628`/`52553`, run `ce774342c896def4fd912cfa63ed0605`; Memories target launcher/app `92843`/`2651`, run `bc73e2fbec937376a8efbb3a3dd32026`. Both `down --run-id` commands succeeded, removed their workspace and recorded project/app processes and left no run-labelled containers. All four baseline Dapr containers remained running. The temporary inotify limit was restored; raw transcripts and UTC metadata are in `evidence/1-10/pinned-{eventstore,memories}.txt` and `pinned-proof.json`.

- **Historical CI qualification blocker (resolved).** CI `39135616837` at `332b2c144defba546fe0a09626a4a72f2340e22f` passed the audit/catalog checks and full Builds module tests, then failed with SA1143 at two conditional expressions in `Tools/PackagedSeedProbe/PackagedSeedProbe.cs`. The approved formatting-only owner correction is `c6f56c011ce3a83cdf6100c322352daf125c6af9`; subsequent CI and release passed. This is the only Builds difference from the stable AC2 run. Local source suites and package/loader/seed checks also passed (`evidence/1-10/builds-resumed-verification.txt`).

- **Historical stable AC2 re-verification (superseded by the review-pass-3 rerun).** Builds `332b2c144defba546fe0a09626a4a72f2340e22f`, EventStore `d55c6f7c11e9bce6f17d490a84c83f1561979d63`, Memories `d0c5d8ca96b06e02acdda88f255b33288642a7f7`: the source live class passed 3/3 in 286.050 s. All four repository revisions and the three relevant binary hashes matched before/after. `evidence/1-10/live-tests.txt` records the raw output, UTC interval, and restoration of the temporary inotify cap.

- **Resumed run (2026-10-18).** Review pass 2 corrected first-run owner/Aspire-package prerequisites and the run-id lookup in Platform/EventStore/Memories documentation. At Builds `332b2c144defba546fe0a09626a4a72f2340e22f`, the audit validator passes for 307 packages/148 families and the Platform catalog validator passes for EventStore 3.125.1 and toolkit 13.6.1-beta.935. The former CI blocker at `82565f485efbe961e553d7eac5393b4373858c7d` is superseded by that owner repair; exact-SHA CI/release and final pinned breakpoint proof subsequently passed as recorded above.

- **Historical AC1 blocker (Builds; repair/release now authorized).** `dotnet tool run hexalith-module -- debug` with the pinned `4.33.0` fails with `HXP020` ("Could not load file or assembly 'Hexalith.Builds.Tooling, Version=4.33.0.0'") for both owner manifests. Two defects in the package: (1) `g4-host/Hexalith.Builds.Tooling.dll` is `1.0.0.0` while the CLI builds the Platform model against its `4.33.0.0` copy; Builds `6e8b71e` fixes this but is an unreleased `build:` commit. (2) The packaged AppHost bakes the CI runner's `/home/runner/...` AppHost, DCP and dashboard paths; past cause 1, Aspire stops with `UnauthorizedAccessException` on `/home/runner`. Both need the Builds release and pin move authorized by the later decisions above. Evidence: `evidence/1-10/pinned-tool.txt`.
- **Diagnostic only.** With the Builds CLI built from `references/Hexalith.Builds`, `debug` reached ready for both manifests, netcoredbg hit `ServiceCollectionExtensions.cs:225` (EventStore) and `FalkorDbHealthCheck.cs:28` (Memories), and `down --run-id` removed only the run. netcoredbg 3.2.0 `detach` leaves a .NET 10.0.12 debuggee suspended; the docs say to stay attached and finish with `down`.
- **Resolution rule.** Both helpers route through `ResolveReferencedModuleProjectPath(root, …)` (internal; Memories exposes internals to `Hexalith.Memories.Server.Tests`) and throw `InvalidOperationException` naming the dependency and the expected path. Inside the Memories repository the helper now resolves its own projects, which the secret-store test comment described as impossible.
- **Historical gitlinks before the existing commits were synchronized.** EventStore (`a3b0c587`, `2af29911`) and Memories (`c16ea1a1`, `5d77b0ec`) commits are local and unpushed; the Platform gitlinks are not staged. Until `git add references/Hexalith.EventStore references/Hexalith.Memories`, source-mode runs and the Composition tests refuse the checkouts (HEAD differs from the staged gitlink).
- **Builds fix (local commit `5faaf88` on `4629ff2`, not pushed).** New `CompositionAspireHostPaths` resolves `aspire.hosting.orchestration.<rid>`/`aspire.dashboard.sdk.<rid>` at the catalog Aspire version from `NUGET_PACKAGES` or `~/.nuget/packages`; a missing package is prerequisite `HXR016` naming the expected path, checked right after `HXR014`. The engine passes `DcpPublisher__CliPath`/`DcpPublisher__DashboardPath` plus run-private `<workspace>/apphost` and `<workspace>/aspire-store` to the preflight and the AppHost. Aspire 13.6.1 adds `AppHost:Directory` and `Aspire:Store:Path` as in-memory configuration after the environment source, so environment variables cannot override them (the Code Map's premise); `AppHostRunner.CreateBuilder` applies them through `DistributedApplicationOptions.ProjectDirectory` and `builder.Configuration["Aspire:Store:Path"]`. This touches `src/hosts/Hexalith.Builds.Module.AppHost/AppHostRunner.cs`, outside the task's listed paths but inside the fix decision. The packaged gate now starts the packed host without `--validate-platform`; the loader fixture records the resolved store, AppHost directory, DCP and dashboard at `BeforeStartEvent` and exits before resources start. README documents `HXR016`. Tests: 15 new (resolver, engine `HXR016` and environment capture, AppHost builder); full `Hexalith.Builds.Module.Tests` 1069 run, 1 timing failure (`ReadinessDeadlineObservationSharesStoppingBudgetAsync`, 1.72 s vs 1.5 s bound under load) that passed 3/3 alone. Pre-release proof: `evidence/1-10/prerelease-tool.txt`. Gated live tests with the changed Tooling: 3/3.
- **Historical Builds gitlink before the existing commits were synchronized.** Builds HEAD was `5faaf88` while Platform stages `4629ff2`, so source-mode runs from the Platform root refuse the Builds checkout until the orchestrator moves the gitlink to the release tag commit.
- **Historical environment.** The intermediate run (`live-tests-intermediate.txt`, engine `303f86d`) failed with Sentry Exited at 14:20Z and passed its 14:23Z retry after temporary inotify headroom. The later stable run (`live-tests.txt`, engine `332b2c1`) passed its single recorded 14:37Z attempt; it had no failed first attempt. Both runs restored the original watch limit.

## Spec Change Log

## Review Triage Log

Review pass 1, 2026-10-18. Three layers: blind hunter (B), edge-case hunter (E) and verification gap (V). They reviewed one combined diff of the Platform story paths, EventStore `c0c08d45..2af29911`, Memories `fb39a0c5..5d77b0ec` and Builds `4629ff2..5faaf88`.

| # | Finding | Verdict | Evidence | Route |
|---|---|---|---|---|
| B1 | AC2 live-test evidence predates the Builds fix `5faaf88` | medium | `live-tests.txt` records an 11:34Z run against `6e8b71e`. The fix changes every run's AppHost start. | patch (orchestrator reruns after release) |
| B2 | Live-test timeline is inconsistent | medium | The quoted Sentry failure (11:45:28Z) is after the "third, passing" run (11:34Z). The diagnostic EventStore run (11:33Z) overlaps that run despite the fixed Dapr ports. | patch, with B1 |
| B3 | Evidence README version table is stale | low | It says "Builds `6e8b71e`, not changed" and Platform `7993509`. The pre-release proof ran at Platform `2cc03cd` with Builds `4629ff2`+fix. | patch, with B1 |
| B4a | Sprint status `in-progress` while spec is `in-review` | false | Step 5 syncs sprint status. | reject |
| B4b | DW-205/DW-206 resolutions cite the spec without a repository | low | Inside EventStore the bare `spec-1-10-…` name points nowhere. Direct text fix. | patch |
| B5a | The public helper now throws on patch release with no breaking marker | low | AD-4 forbids sibling and ancestor layouts. Standard layouts (self `.slnx`, `references/`) are unaffected, and the human accepted that consumers are not edited. A major bump would cascade through the catalog. | reject |
| B5b | Tenants `TenantsServerProjectMetadata` remarks still describe the removed probing | low | `references/Hexalith.Tenants/src/Hexalith.Tenants.Aspire/TenantsServerProjectMetadata.cs:11,21`. Another repository, wrong once Tenants takes the new package. | defer |
| B6 | Segment validation and root containment still missing (DW-207) | low | The issue predates this story and is tracked as EventStore DW-207. Memories' copy has the same gap. | defer |
| B7a | HXR016 requires the dashboard package though `DisableDashboard = true` | low | Both packages come from the same `Aspire.AppHost.Sdk` restore, so one is never present without the other. Dropping the override would leave a baked path. | reject |
| B7b | NuGet.config `globalPackagesFolder` is ignored | low | Rare. The fix needs NuGet.Configuration, and the HXR016 hint names the `NUGET_PACKAGES` workaround. | reject |
| B7c | `OSArchitecture` mismatches the restored RID under emulation | low | The SDK restores orchestration for `NETCoreSdkRuntimeIdentifier`, which is the process architecture. The fix is one word (`ProcessArchitecture`). | patch |
| B8 | Preflight overrides untested; gate validates with an empty environment; pwsh assembly load conflict | false | Validation returns before `Build()`, so preflight never uses the overrides. Under `-NoProfile`, pwsh has loaded no NuGet.Versioning, and the gate passed locally. | reject |
| B9 | Blank-directory test and Memories live case are weak | low | The blank test catches the realistic regression (blank passed through). Memories `/ready` includes its Redis and FalkorDB checks. | reject |
| B10 | The secret-store test keeps text counts instead of a positive model build | false | No bad outcome: the counts still guard the wiring. A positive test would be an enhancement. | reject |
| B11 | `down --run-id` is documented, but nothing says how to find the id | medium | `diagnostic-eventstore.txt:52`: after an earlier failed run, `down --manifest` gives HXR024, and `debug` human output does not print the run id. Run state is `~/.local/share/hexalith-builds/g4-runs/<runId>.json` (`CompositionRunStateStore.cs:27-28`). | patch (docs) |
| B11b | The tool hides the run id, and stale failed runs block `down --manifest` | medium | This is existing Builds 1.8 behaviour, not this diff. | defer |
| B12 | Known-issue note gives no workaround | false | The orchestrator task removes the note when the pin moves to the fixed release. | reject |
| B13 | Docs recommend VS Code but only netcoredbg was tested | low | vsdbg attach and detach is the standard path, and the caveat is netcoredbg-specific. | reject |
| B14 | Dapr snippet is Linux-amd64-only and leaves the shell in a temp directory | low | Direct fix: label it Linux x64 and run it in a subshell. Merged with E1. | patch |
| B15 | Pinned versions are copied across three repositories | low | Docs drift on catalog moves. A pointer would still be a copy. | reject |
| B16a | Memories CONTRIBUTING AppHost row is unscoped | false | `CONTRIBUTING.md:526` is the packaging table and does not present the AppHost as the way to run Memories. | reject |
| B16b | Memories `docs/dev/quickstart.md` runs the own AppHost without scoping | low | `quickstart.md:50,117,191`. Direct one-sentence fix. | patch |
| B16c | Memories README "`references/Hexalith.EventStore`" is ambiguous | low | Memories has its own nested submodule of that name. Direct wording fix. | patch |
| B17 | Intermediate-output path undocumented; HXR016 hint names Platform `apphost.cs`; fixture "forbidden" marker | low | The store override covers the intermediate-output use, and the hint is an example. The marker is fixture-internal. | reject |
| B18 | Evidence redaction and version strings are inconsistent | low | Cosmetic, no secret exposed. | reject |
| E1 | Dapr snippet extracts tarballs even when checksums fail | medium | No `set -e`: `sha256sum -c` failure does not stop `tar`. | patch |
| E2 | Non-linux-amd64 hosts | low | Same as B14. | patch, with B14 |
| E3 | `down` needs `--run-id` after an earlier failed run | medium | Same as B11. | patch, with B11 |
| E4 | Rooted, `..` or null segments escape the root | low | Same as B6, existing issue. | defer, with B6 |
| E5 | A repository with `.sln` but no `<Module>.slnx` is treated as a consumer | false | The frozen rule names `.slnx`, and every Hexalith repository root has one (checked 2026-10-18). | reject |
| E6 | Whitespace `AspirePackagesRoot` throws before the run exists | false | Only tests set it, so the CLI never reaches this. | reject |
| E7 | NuGet.config packages folder ignored | low | Same as B7b. | reject |
| E8 | `HOME` unset probes `<cwd>/.nuget/packages` | low | The dotnet CLI itself needs `HOME`, so this is rare. | reject |
| E9 | Dashboard package required | low | Same as B7a. | reject |
| E10 | Engine tests now depend on the host NuGet cache | low | The test project references `Hexalith.Builds.Module.AppHost`, and its `Aspire.AppHost.Sdk` 13.6.1 restore caches both packages wherever the tests build. | reject |
| E11 | Null RID becomes an empty string in the pwsh gate | low | Unsupported platforms only; CI is linux-x64. | reject |
| E12 | The loader receipt drops a failed child's exit and timeout facts | low | `Invoke-PackagedHostChild` throws before returning, so `validation` and `runtime` stay null. Direct fix. | patch |
| E13 | Helper claims to match the mapping, but nested checkouts in Platform fail | false | Intended by the frozen rule and AD-4: nested submodules stay uninitialized, own AppHosts serve own tests, and the failure names the path. | reject |
| V1 | Existing fake-AppHost engine tests read the real NuGet cache | low | Same as E10. | reject |
| B8-r | Correction to B8 (the in-process assembly load part) | high | Builds push CI [39129734206](https://github.com/Hexalith/Hexalith.Builds/actions/runs/39129734206) at `13a3316` failed in "Qualify G-4 tool source and Release packages". The loader gate passed, but it had loaded the payload's `NuGet.Versioning` and Tooling into the shared pwsh process. `write-platform-version-catalog.ps1:58` then ran `Add-Type` on the SDK's `NuGet.Versioning` and failed with "Could not load file or assembly 'NuGet.Versioning, Version=7.9.0.0'". CI was green at `4629ff2`. The earlier `false` verdict only reasoned about a fresh `-NoProfile` process. | patch |

Review pass 2 (resumed build), 2026-10-18. Three independent reviewers read the complete baseline diff; unrelated intervening work was excluded from triage. Existing verdicts and routes are carried where the location and claim are unchanged.

| # | Finding | Verdict | Evidence | Route |
|---|---|---|---|---|
| R2-B1 | EventStore prerequisites omit the other declaration-owner checkouts | medium | `CompositionDeclarationClosure.LoadDeclarations` reads EventStore, Tenants and Memories owner manifests before selection. README now lists all owner checkouts and distinguishes them from the launched selection. | patch |
| R2-B2 | First-run instructions omit the local Aspire SDK package restore | medium | `CompositionEngine` checks HXR016 before the model build; tool restore does not populate Aspire SDK packages. README now gives `dotnet restore apphost.cs`, verified with SDK 10.0.402, and explains `NUGET_PACKAGES`. | patch |
| R2-B3 | NuGet.config globalPackagesFolder is ignored | low | carried B7b: resolver intentionally reads `NUGET_PACKAGES` or the user default; HXR016 names the workaround. Effective NuGet configuration discovery adds complexity for an uncommon setup. | reject |
| R2-B4 | Unchecked helper segments can escape the mapped root | low | carried B6/E4: EventStore DW-207 and the prior Platform deferral cover both copies. | defer (already recorded) |
| R2-B5 | Globally newest run state can belong to another invocation | medium | Explicit `--run-id` selects by id without checking the manifest hash. Platform README, EventStore CONTRIBUTING and Memories README now require the reported launcher id and matching manifest SHA-256. | patch |
| R2-B6 | AC2 evidence predates the Builds startup fix | medium | carried B1: refreshed source live tests passed 3/3 at the canonical revisions recorded in `evidence/1-10/live-tests.txt`, with identical before/after binary hashes. | patch |
| R2-B7 | The historical live-test timestamps conflict | medium | carried B2: the old result cannot establish ordering. Preserve it as historical and supersede it with directly captured UTC start/end times. | patch |
| R2-B8 | Evidence version table describes an earlier implementation | low | carried B3: replace the current environment record and retain the source/prerelease proofs as historical evidence with their original identities. | patch |
| R2-B9 | The package gate does not execute DCP | low | The gate deliberately proves packaged contract loading, Aspire store creation and path overrides. Running the real compositions supplies DCP execution evidence. Empty or non-executable restored binaries require a damaged cache; another execution guard adds complexity outside ordinary use. | reject |
| R2-B10 | Resolver pwsh child has no timeout | maybe-false | The invocation is synchronous and has no local timeout, but no stalled assembly/package lookup was reproduced. An injected stalled resolver and a gate-duration assertion would establish whether this is an operational medium-severity failure. | defer (medium, unverified) |
| R2-B11 | Secret-store tests should become model-level tests | false | carried B10: the source assertions still protect the wiring; no incorrect model behavior was demonstrated. Additional model coverage is an enhancement. | reject |
| R2-E1 | The newest-state recipe can select a different run | medium | Same demonstrated root cause as R2-B5; all three recipes now check launcher identity and manifest hash. | patch (with R2-B5) |
| R2-E2 | Resolver child can outlive the AppHost child timeout | maybe-false | Same unverified stall condition as R2-B10; record one follow-up with a reproducible delayed-child check. | defer (with R2-B10) |
| R2-E3 | EventStore helper accepts traversal segments | low | carried B6/E4: pre-existing DW-207; no second deferral. | defer (already recorded) |
| R2-E4 | Memories helper accepts traversal segments | low | carried B6/E4: the prior Platform deferral explicitly covers the Memories copy. | defer (already recorded) |
| R2-V1 | Native arm64 pwsh can select a different RID from x64 dotnet | low | The gate resolves inside pwsh but starts the AppHost with dotnet. The uncommon mixed-architecture setup can fail; current Linux x64 CI and normal matching installations are unaffected. Resolving through another host adds public plumbing or process complexity. | reject |


Review pass 4 (resumed build), 2026-10-18. Blind hunter, edge-case hunter and verification-gap reviewer examined the combined story diff and expanded owner revisions. Every finding is recorded below before grouping. EventStore security changes belong to intervening owner commits `39b366bca3dbe508b4cf3c5afa69db1ddadb6dd2` and `993be1bcc03fea06b9cd451ad04d57783a119e83`, not the 1.10 path-resolution commits; their follow-ups are deferred, rather than changing that independent work here.

| # | Finding | Verdict | Evidence | Route |
|---|---|---|---|---|
| R4-B1 | Linux-musl selects the glibc Aspire packages | low | The SDK's supported RID list includes linux-musl-x64, while the resolver chooses linux-x64. Alpine is outside the Linux/WSL2 development environment qualified here; libc detection and extra selection branches add complexity for an uncommon setup. | reject |
| R4-B2 | Both project-path helpers accept traversal/rooted segments | low | carried B6/E4 and R2-B4: both copies and EventStore DW-207 already cover this pre-existing input-validation gap; the code is unchanged. | defer (already recorded) |
| R4-B3 | A newer revoked trust entry can coexist with an older accepted entry | maybe-false | SignatureMatches filters revoked entries and requires one remaining match. Whether revocation supersedes an older profile or applies only to that entry requires the owner's trust-profile selection contract and a mixed-revision test. This is independent EventStore owner work; potential impact is medium, unverified. | defer |
| R4-B4 | The authority retains caller-owned public-key byte arrays | medium | The constructor copies only the collection; SignatureMatches later imports each retained SubjectPublicKeyInfo array. A caller can mutate the accepted key bytes after construction without changing the trust metadata. Independent EventStore owner work. | defer |
| R4-B5 | Issue/dispatch receipts lack the hold receipt's format checks | low | Committed checks capability, signing identity, detached-JWS digest, revision and receipt identity; the reducer emits a hashed intent digest and validates bounded operation IDs. Malformed injected metadata lacks a demonstrated authorization bypass; extra corruption guards add complexity beyond a correction. | reject |
| R4-B6 | Actor-name collisions silently retain a different CLR actor | medium | RegisterActor returns on name alone without checking the implementation. An explicit protection opt-in can therefore retain an incompatible actor and fail its protection calls. Independent EventStore owner work. | defer |
| R4-B7 | A writer that never completes holds an actor turn indefinitely | medium | AwaitAsync intentionally awaits the original writer after cancellation to prevent overlapping mutations; the lifecycle tracker preserves that exclusion. No bounded host-recovery path exists if the underlying write never settles. Independent EventStore owner work; any fix must preserve mutation exclusion. | defer |
| R4-B8 | The packaged resolver child is not bounded by the child timeout | maybe-false | carried R2-B10/R2-E2: the synchronous resolver call still has no timeout, but no stalled resolution was reproduced. The existing medium/unverified follow-up specifies the delayed-child check needed to establish harm. | defer (already recorded) |
| R4-B9 | The Memories live test does not directly assert storage containers | low | carried B9 and pass-3 rejection: Memories /ready includes Redis/FalkorDB health, and the released-tool proof also records both containers. A second direct container assertion is an enhancement. | reject |
| R4-B10 | The Sentry failure lacks its health-probe error and no single all-green class run exists | maybe-false | The failed attempt is preserved and the unchanged-source Memories retry passes; all three cases have passing evidence, which satisfies the AC without requiring one invocation. A repeatability defect remains unproven. Capture Docker health-probe results on a future failure to distinguish an environmental transient from a runtime defect (medium if reproduced). | defer |
| R4-E1 | EventStore helper can escape the mapped checkout | low | carried B6/E4 and R2-E3: this is the same already-recorded segment/containment gap at the unchanged helper. | defer (already recorded) |
| R4-E2 | Memories helper can escape the mapped checkout | low | carried B6/E4 and R2-E4: the existing Platform deferral explicitly includes this copy. | defer (already recorded) |
| R4-E3 | Public-key bytes remain mutable after authority construction | medium | The retained arrays and verification consumer confirm the same defect as R4-B4; one shared owner follow-up records both findings. | defer (with R4-B4) |
| R4-E4 | A conflicting actor registration wins by name | medium | The name-only early return confirms the same defect as R4-B6; one shared owner follow-up records both findings. | defer (with R4-B6) |
| R4-V1 | The signature-negative test can fail at guard equality even if VerifyData accepts every signature | medium | Pre-verified gap: the altered-JWS case leaves the guard's original JWS/digest in place, and the trailing-SPKI test rejects before VerifyData. Add a valid matching request/guard with an unrelated well-formed P-256 anchor to isolate signature rejection. Independent EventStore owner work. | defer |

No patch, bad_spec or intent_gap entries remain. The six new owner/diagnostic follow-ups are appended to deferred-work.md; existing deferrals are carried without duplication. Matrix audit: both released-tool breakpoint/teardown transcripts cover the debug rows; the executed EventStore 18/18 and Memories 9/9 path tests cover self/mapped/unmapped layouts (unit-tests.txt). All three distinct live cases passed at exact cf20db9 across the initial class and focused retry. The full suite's 12 failures and changed membership remain explicit.

## Verification

**Commands** (build with `-nodeReuse:false`; run each test DLL with `dotnet exec`, because `dotnet test` on the `.slnx` runs zero tests):
- EventStore `Hexalith.EventStore.AppHost.Tests.dll` -- expected: the `RepositoryProjectPaths` tests pass, with no new failures.
- Memories `Hexalith.Memories.Server.Tests.dll` -- expected: no new failures.
- `HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE=1 HEXALITH_DAPR_HOME=~/.hexalith/dapr/1.18.2 dotnet exec tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -class '*BuildsSourceStartupTests'` -- expected: all three distinct live cases pass. The review-pass-3 run passed two cases initially and Memories on its focused retry. The full-suite failure count remains 12, with the changed membership and layout-sensitive assertion recorded in `evidence/1-10/failure-membership-final.txt`.

**Completed verification:** the focused helper/secret-store checks passed; all three distinct live cases have passing results at exact `cf20db9` across the initial class and focused Memories retry (the first class had one failure); released-tool breakpoint and exact-run teardown proof passed for both owner manifests; and Builds CI/release qualification passed. The rerun full suite and its exact failure-set comparison are recorded in the evidence. Three independent review layers were triaged; the unverified resolver-child timeout remains deferred (R2-B10/R2-E2). Existing baseline failures and earlier superseded proofs remain identified in the evidence.
