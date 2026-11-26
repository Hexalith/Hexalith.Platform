---
title: '3.6 slice 3: Select the complete environment in Builds'
type: 'feature'
created: '2026-11-26'
status: 'done'
route: 'dispatch'
baseline_commit: 'deccbc3bcf4ed687ecf4d556ebe356fe57148777'
story_key: '3-6-run-all-seven-mvp-modules-together-locally'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

The Builds runner can start individual modules and their minimum environments. A Platform-owned v2 manifest at `module/hexalith.module-manifest.v2.json` will select the complete environment through the existing `run --manifest` and `debug --manifest` commands. Its serverless `platform` declaration selects EventStore, Tenants, Parties, Folders, Projects, McpCli and Memories. The closure brings in Conversations through Projects. Builds prepares this entry point for Platform adoption and live acceptance in the next slice.

## Boundaries & Constraints

- Preserve individual-module and qualification commands. Accept the complete selector only from the verified Platform root. Startup coordination and the AppHost use the same closure. Check each declaration's owner identity against its mapped source or authenticated release snapshot. Refuse missing declarations, release mismatches and cycles before starting resources.
- Build McpCli in the private run workspace before startup. Source runs use Debug and mapped Contracts. Package runs use Release and catalog-pinned Contracts, with the McpCli project selected from the exact direct gitlink. The tool stays unpublished, uses stdio for MCP and starts no server. Clean it up with the run.
- Keep Platform runtime wiring, local realm changes and live seven-module acceptance in the Platform slice. Source Contracts enrollment needs the McpCli owner's mapping support. The Projects extension needs the owner's published release. Use fixtures for these dependencies while they are pending and require the released inputs for final adoption.

## I/O & Edge-Case Matrix

| Input | Expected |
| --- | --- |
| Platform selector with all declarations | Select all seven owners and Conversations once. |
| Missing Projects dependency or mismatched package snapshot | Refuse startup and name the owner. |
| Missing mapped McpCli checkout with another copy nearby | Refuse startup and name the required project. |
| Multiple justified startup overrides | Use the largest and report its owner and justification. |
| Required resource fails or times out | Name the unready resource and retain diagnostics. |

</frozen-after-approval>

## Code Map

Paths below are relative to the standalone Builds repository. Runtime types live in `src/libraries/Hexalith.Builds.Tooling/Runtime/`.

- `CompositionDeclarationClosure.cs` eagerly loads three reference owners and walks dependencies. `CompositionStartupRequest.cs` and `CompositionEngine.cs` already pass the active manifest to it. Platform's `PlatformDeclarationSet` uses the same entry point.
- `CompositionDeclarationOptions.cs` validates snapshot versions against the consumer catalog. `Tools/ci/release_declarations.py` verifies NuGet source commit, Git tag and manifest digest before recording `Props/release-declarations.json`.
- `PlatformManifestCommand.cs` builds McpCli for individual source runs. `../Workspace/WorkspaceRootResolver.cs` maps non-active references to packages in package mode. Reuse the guarded MSBuild inputs from `SourceMappingMaterializer` and the pinned catalog import pattern from `BuildModelAsync`.
- `CompositionEngine.cs` writes elapsed time to `readiness.json` and uses `HXI010` for success. The existing deadline resolver and timeout path already report overrides and failed resources.

## Tasks & Acceptance

- [x] `CompositionDeclarationClosure.cs`, `CompositionDeclarationOptions.cs`, `Tools/ci/release_declarations.py`, `Props/release-declarations.json` — select the complete graph. Extend release authentication with the participating owners' catalog package IDs. Preserve the reference snapshot sync until Platform adopts the complete selector. Verify Folders 2.1.0 and use the Projects extension release when published.
- [x] `CompositionStartupRequest.cs`, `CompositionEngine.cs`, `PlatformManifestCommand.cs`, `../Workspace/WorkspaceRootResolver.cs` — use the closure to select McpCli and the deadline. Pass the verified mapping into the tool build. Report the observed duration in `HXI010` and retain the existing teardown route.
- [x] `test/Hexalith.Builds.Module.Tests/DeclarationClosureTests.cs`, `McpCliRunBuildTests.cs`, `StartupCoordinationTests.cs`, `StartupReadinessTests.cs` — cover the matrix, both build modes and regressions in individual-module selection. Exercise the actual CLI entry point with fixture owners and verify `down` removes the tool.

**Acceptance Criteria**

- Given compatible mapped owner inputs, when the Platform manifest is passed to `run` or `debug`, then the AppHost receives the selected graph and the runner reports the built McpCli path, effective deadline and measured duration after declared readiness succeeds.
- Given the same Platform selector in package mode, when a fresh run starts, then declarations are authenticated against the pinned catalog and McpCli builds from the verified checkout with catalog-pinned dependencies.

## Implementation Notes

- Implement in the [isolated Builds worktree](../../../builds-story-3-6) at the recorded baseline. Keep the worktree changes local and use available release evidence. The sprint tracker represents the whole Story 3.6 and stays in progress during this slice.
- The Release build reported zero warnings and errors. All 102 focused cases passed, including CLI source run and debug, package run, asset inspection and `down`. The full project reported 3,334 cases with two failures and four skips. The two failures passed on isolated retry after restoring the scratch Git metadata. [Focused tests](../../../builds-story-3-6-matrix-tests.log) [Full test run](../../../builds-story-3-6-tests.log) [Retry](../../../builds-story-3-6-retry-tests.log)
- The release verifier authenticated Folders 2.1.0, Conversations 2.2.0 and McpCli 3.0.0. The Projects extension package endpoint returned HTTP 404, so package-mode verification used a fixture for it. The McpCli fixture verified a real mapped Contracts build. Live adoption still needs the owner's Contracts enrollment change, a released Projects extension and the Platform runtime wiring. [Release verification](../../../builds-story-3-6-release-verification.log) [Implementation diff](../../../builds-story-3-6-implementation.diff)
- Review fixes preserve matching individual package snapshots and pass the verified complete declaration set into the AppHost model call. A package run initializes the recorded McpCli gitlink and builds an extracted copy of that commit. The focused rerun passed 78 .NET cases and four Python cases, including cold initialization, ignored-file isolation, refusal of unpinned transitive Contracts, and comparison of the readiness diagnostic with the recorded duration. [Focused review tests](../../../builds-story-3-6-review-tests.log) [AppHost check](../../../builds-story-3-6-review-apphost-check.log)
- The final fresh-clone Release build passed with zero warnings or errors, and all four Python checks passed. The full .NET project ran 3,339 cases, with 3,334 passes, four skips and one failure in the Linux native-process discovery fixture. Both parameters of that fixture passed on isolated retry. [Full verification](../../../builds-story-3-6-final-verification.log) [Isolated retry](../../../builds-story-3-6-final-retry.log)
- The local Builds commit is `43206d060c33b3ee87050f57141162d84ae3d4bf`. The isolated worktree is clean. Story 3.6 remains in progress pending the published catalog pins, Platform adoption and live readiness. [Commit validation](../../../builds-story-3-6-commitlint.log)

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| Blind 1. Equal package declarations can conflict | Medium | Patch | The validator adds startup defaults. The comparison uses raw supplied JSON and a normalized snapshot. |
| Blind 2. Final validation reopens the graph | Medium | Patch | The second pass can read owner bytes whose hash differs from the bytes used to traverse dependencies. |
| Blind 3. The model reloads after AppHost verification | Medium | Patch | `ComposeRun` receives paths and calls the same loader after the guard. Keep the verified closure available to that call. |
| Blind 4. Ignored McpCli files can enter the package build | High | Patch | The clean-status check omits ignored files while MSBuild reads the live directory. Materialize the selected commit's files in the run workspace. |
| Blind 5. Cold source discovery retains the default during initialization | Medium | Defer | The existing metadata probe requires available declarations to authenticate overrides before initialization. A cold fetch can consume the fallback budget before those declarations become available. |
| Blind 6. Public debug build can look up the Release output | Medium | Patch | The existing overload uses Debug arguments regardless of mapping mode. The new output lookup used mapping mode unconditionally. |
| Blind 7. McpCli child expiry looks like a build failure | Medium | Patch | The invalid-operation catch runs before the engine checks the deadline. |
| Blind 8. Selector validation reuses a display path | Medium | Patch | The validator may redact `Source`. The supplied filesystem path can be kept for validation and graph loading. |
| Blind 9. Package CLI test substitutes the AppHost | Low | Reject | The fixture proves the CLI, tool build and cleanup. Real released Projects authority and the live Platform run are adoption requirements. The Compose caller is addressed by Gap 1. |
| Blind 10. Package success starts with McpCli checked out | Medium | Patch | Submodule creation initializes it, so the new cold initialization path needs coverage. |
| Blind 11. README lacks the complete command | Low | Patch | The current section can be read as excluding McpCli from all package runs. Document both entry points. |
| Edge 1. Windows path separators defeat deduplication | Medium | Patch | `Path.Combine` retains the embedded separator in the owner-relative path. Normalize both paths and use the filesystem comparison. |
| Edge 2. Cold package call omits `ActiveModule` | Medium | Patch | Full selection also recognizes the supplied selector, but only the active-module hint enables the named catalog view before loading. |
| Edge 3. Owner bytes can change between traversal and validation | Medium | Patch | Same second read as Blind 2. |
| Edge 4. Package mapping can look for the wrong tool output | Medium | Patch | Same configuration split as Blind 6. |
| Edge 5. Case-insensitive gitlink lookup can index at -1 | Medium | Patch | The recorded Git paths use the filesystem comparison, but `FindIndex` used case-sensitive identity matching. |
| Edge 6. Package copies can conflict with their own snapshots | Medium | Patch | Same normalization mismatch as Blind 1. |
| Edge 7. Verified AppHost graph can change before use | Medium | Patch | Same model reloading as Blind 3. |
| Edge 8. Ignored files can affect the Release tool | High | Patch | Same use of the live checkout as Blind 4. |
| Gap 1. Complete AppHost verification has only a direct guard test | Medium | Patch | The existing CLI fixture bypasses real composition. Exercise `RunTopology.Compose` with transitive drift and an absent model. |
| Gap 2. New release families lack apply-path coverage | Medium | Patch | Removing McpCli from the enrollment table kept the three existing Python checks green. |
| Gap 3. Cold package tool initialization lacks a success check | Medium | Patch | Same fixture setup as Blind 10. |
| Gap 4. The unpinned transitive Contracts refusal is unchecked | Medium | Patch | Existing negative cases stop before restore and ordinary catalog checks cover direct references. |
| Gap 5. The diagnostic's numeric duration is unchecked | Low | Patch | The tests assert the text prefix and persisted value separately. Compare the emitted and persisted durations. |

## Verification

- Use `run-with-temp` for disposable fixtures and builds. Build `test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj` and run that test project directly. Keep logs outside temporary directories and wait for child processes to exit.
- Validate released snapshots through the existing release declaration verifier. Verify the full tool and Platform pins once the owner prerequisites and Builds release are available.
