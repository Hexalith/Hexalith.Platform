---
title: '1.4 Enforce the Platform identity and support package mode'
type: 'feature'
epic: 1
story: 4
created: '2026-10-10'
status: 'done'
baseline_commit: '78e47a4c8ca853adaf7aa476c11bce92ad7f42d0'
route: 'dispatch'
review_loop_iteration: 4
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `hexalith-module` builds a module without knowing which Platform it belongs to. It does not check that the Builds catalog and the tool match what that Platform pinned. Package mode (story 1.3) only switches to Release and NuGet origins, so a CI-style run can silently mix another Platform, catalog or tool.

**Approach:** In Builds, resolve one Platform identity per `run`/`test` with the story 1.3 mapping. The identity is the committed gitlink of the direct `references/Hexalith.Platform`, or HEAD inside the Platform repository. From the identity, read the pinned Builds catalog commit and the supported tool-version range. Evaluate the mismatch conditions: package mode refuses, naming each mismatch; source mode warns and continues. The mapping and plan carry the identity so the build and the composition both use it.

## Boundaries & Constraints

**Always:**
- Implement in `references/Hexalith.Builds`.
- Resolve the identity inside the existing single resolution, before descriptors, run state or resources. Read only local Git objects and refs: the tool never fetches. Never move an existing checkout.
- Fail closed on Git timeouts, truncated output or unreadable objects.
- Report every holding condition in one result, through new `HXW` rules in the existing phases, categories and exit codes, in both human and JSON output. Source-mode warnings appear whether the run later passes or fails.
- Add the identity to `mapping.json` and its content hash, and additively to `plan.json`.
- In package mode, initialize the direct Platform and Builds references at their recorded gitlinks, non-recursively, as story 1.3 does. Other package-origin references stay uninitialized.

**Never:**
- Pin or ratify a tool version, or edit `.config/dotnet-tools.json` (story 1.9).
- Build or launch the Platform model, or add dependency-service images (story 1.6).
- Change evidence schemas or the canonical evidence command (story 3.9).
- Add a debug entry point (story 1.8).
- Edit other modules (story 1.12).
- Change `CompositionCommandOptions`, `PackagedHostProject`, tool ids, package layout or exit codes.
- Use the network in tests.

**Decisions (2026-10-10):**
- **Pins.**
  - The catalog pin is Platform's own `references/Hexalith.Builds` gitlink in the identity's tree.
  - The tool range comes from a new Builds-owned record, `hexalith-platform.json`, at the Platform root: `schemaVersion` plus an optional `toolVersionRange` in NuGet range syntax.
  - A tracked record at HEAD, with no direct `references/Hexalith.Platform`, marks the Platform repository.
  - This story commits the record to Platform without a range. No tool is accepted yet: package mode refuses and source mode warns. Story 1.9 sets the range.
- **Missing reference.** A Git workspace that is not Platform and lacks a direct `references/Hexalith.Platform` fails in both modes, before anything is initialized. A no-Git workspace has no identity: package mode refuses, source mode warns.
- **Composition.** This story resolves, verifies and records the composition source (the Platform checkout, commit and tag, or HEAD inside Platform). Story 1.6 builds the Platform model from that source and adds service images.
- **Catalog versions.**
  - Package mode refuses any referenced package defined in the pinned catalog that resolves to a different version.
  - The catalog is evaluated in isolation from the pinned Builds commit, so consumer overrides cannot change it.
  - Source mode is unchanged.
- **Evidence.** Recording the mode and identity in run evidence stays deferred to story 3.9.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Matching module workspace | The Platform gitlink is committed, tagged `v<SemVer>` and clean. Builds equals the pin. The tool is in range | Identity, tag, pin and range are recorded in the mapping and plan. No identity diagnostics | N/A |
| Platform repository | The root is Platform | Identity and composition commit are HEAD. No tag or clean-root requirement | N/A |
| Composition mismatch | The staged or checked-out Platform commit differs from the committed gitlink | Package mode refuses. Source mode warns and runs | Names both commits |
| Catalog mismatch | The workspace's Builds gitlink differs from the pin, Builds is absent, or the Builds checkout is dirty | Package mode refuses. Source mode warns | Names the pin, the actual commit or state, and the path |
| Tool range | The running tool version is outside the range, or the range is unavailable | Package mode refuses. Source mode warns | Names the version, the range and the record location |
| Untagged or dirty Platform submodule | No `v<SemVer>` tag at the identity, or the Platform checkout has changes | Package mode refuses. Source mode warns | Names the commit or changed state. The tag hint says to fetch tags |
| Several conditions | More than one condition holds | All of them are reported together | Same refusal or warnings |
| Missing Platform reference | A Git workspace that is not Platform and has no direct `references/Hexalith.Platform` | Fails in both modes. Nothing is initialized | Names the root and the expected path |
| No Git | A standalone workspace | Package mode refuses. Source mode warns, and the story 1.3 behavior is otherwise unchanged | Names the missing identity |
| Catalog version | Package mode: a referenced, catalog-defined package resolves to another version | The build stops | Names the package, the resolved version and the catalog version |

</frozen-after-approval>

## Code Map

Paths are relative to Builds. `T` = `src/libraries/Hexalith.Builds.Tooling`, `C` = `src/libraries/Hexalith.Builds.Module.Cli`, `A` = `src/hosts/Hexalith.Builds.Module.AppHost`, `M` = `test/Hexalith.Builds.Module.Tests`.

- `C/ModuleCommandApplication.cs:157-192`: `--mode` on `run`/`test` maps to `WorkspaceMode`. Leave it unchanged.
- `T/Workspace/WorkspaceRootResolver.cs:22,37,68-112,186-198`: entry, active root, recorded gitlinks (186) and the init or package branch (187).
  - Capture the pre-initialization Platform and Builds state between 186 and 187.
  - Initialize Platform and Builds in package mode, then evaluate the identity. If initialization fails, collect the independently observable identity conditions before returning one result. Never move an existing checkout.
  - A regular tracked JSON blob marker identifies Platform only when no direct Platform reference is declared; symlinks and trees do not. Collect an absent Builds index gitlink as HXW009 while retaining the usual stage-zero validation for other direct references. Even a missing direct Platform declaration must retain an independently observable Builds-dirty finding in the same refusal. Do not discard independent conditions when a local identity object is unreadable or the committed Platform gitlink is absent. Choose deterministically among multiple valid release tags, and do not emit duplicate findings for one failed object read.
- `T/Workspace/SubmoduleInitializer.cs:24-80,88-153`:
  - `InitializeDirectAsync` never moves an existing checkout and requires HEAD to equal the staged gitlink.
  - `ValidateRecordedGitlinksAsync` returns the staged SHAs.
  - `ValidateInitializedPackageRootsAsync` validates initialized package roots.
  - Reuse all three.
- `T/Workspace/GitWorkspaceProcess.cs:21`: bounded Git with an allow-listed environment. Treat `Started`, `TimedOut`, `OutputTruncated` and nonzero exit as failures.
- `T/Workspace/WorkspaceMappingException.cs`, `README.md:262-268`: HXW000–006 are taken; allocate from HXW007 on.
- `T/Workspace/SourceMapping.cs`: `ContentHash` hashes exactly `(Mode, Root, ActiveModule, Entries, HasGit)`. Add the identity to both the record and the hash.
- `T/Workspace/SourceMappingMaterializer.cs:17,49-57`: writes `mapping.json`.
- `T/Runtime/CompositionRunPlan.cs`, `CompositionRunPlanFactory.cs:79-128`: `plan.json`. The AppHost checks only `Schema` (`A/RunPlanSource.cs:29-31`), so an additive init property is safe.
- `T/Runtime/CompositionEngine.cs`: materialize and validate the pinned catalog before descriptor child execution. On a materialization failure, preserve its HXW diagnostic, leave no run state or resource, and remove the private run workspace before returning. If bounded deletion fails, include the private workspace path in the refusal.
- `T/Runtime/ModuleCommandExecutionService.cs:99,320,425,545,552-586,680-700`:
  - 99 and 320: resolve calls. 425: descriptors. 545: `StartAsync`.
  - 552-586: result diagnostics. 680-700: `WorkspaceMappingException` catch.
  - Warnings need a return channel from resolution and a merge into the final result.
- `T/Diagnostics/ToolDiagnostic.cs`, `ToolDiagnosticFormatter.cs`: there is no severity field. Non-fatal diagnostics use `ToolFailureCategory.None` (see HXI001/004). Keep the JSON shape.
- `Props/Directory.Packages.props`: the catalog. Its versions are condition-overridable properties.
  - Evaluate the pinned catalog in isolation for the actual consumer project name; the catalog changes `CommunityToolkit.Aspire.Hosting.Dapr` for `Hexalith.Folders.Aspire`. Do not assume one quoting or expression form for valid MSBuild project-name conditions. Preserve relative imports from the pinned Builds tree; no unpinned checkout file may change the result. Never use a single wrapper-name result for every consumer. A per-project snapshot or an on-demand isolated evaluation is acceptable if `catalog-versions.json` binds the pinned files, mapping hash and evaluated result. The real 305-item catalog exceeds `CompositionProcess`'s 65,536-character output cap; use a catalog-specific bounded capture or file output that handles it without relaxing unrelated process bounds, and test with a realistically large catalog.
  - Enforce in `T/Workspace/SourceMapping.targets` with the hashed-mapping pattern of `SourceMappingValidationTask`, including generated and Builds hosts that carry catalog-defined packages. Include `GlobalPackageReference` when it is a package input. Preserve catalog `HXW` diagnostics if materialization fails after a run ID is allocated; do not turn them into generic HXR020.
  - Compare effective NuGet versions semantically. Before restore, allow an equivalent exact range or indeterminate range to reach restore; after restore, match the active assets target using NuGet framework normalization and compare its actual resolved version to the consumer-specific catalog value. A catalog-defined direct reference absent from that target fails. A requested post-restore check without assets fails with HXW012.
  - Convert local catalog I/O failures into an HXW prerequisite diagnostic with the failing path and operation; do not let them become generic HXR020. Dynamic or glob imports outside the current pinned catalog may fail closed with HXW009 rather than reading unpinned files.
- `T/Workspace/PlatformIdentityResolver.cs:25`: choose the running CLI assembly's informational version in a real executable invocation, even if it differs from Tooling's. Verify that path with an executable-level test rather than comparing the selector to itself.
- Keep the existing EventStore 3.119.0 constant and fixture values in the module manifest schema; this story does not migrate the module manifest pin.
- `T/RunEvidence/ModuleRunEvidenceFactory.cs:166-176`: reads the tool version from `AssemblyInformationalVersion`. Reuse it, stripping build metadata, and add a test seam.
- `M/Workspace/WorkspaceGitFixture.cs:46,110,143`, `WorkspaceRootResolverTests.cs`, `WorkspaceCommandModeTests.cs:25-89,416-570`: the fixture and public CLI patterns. Extend the fixture with file-protocol Platform and Builds repositories that carry tags, the record and a Builds gitlink.
- Live state:
  - The Platform root has no tags, its Builds gitlink is `19bf05ec`, and it has no record yet (this story adds it).
  - Parties declares Platform `56eeda39` (uninitialized) and Builds `6a002df5`.
- Do not change: the `Tools/test-g4-tool-package-contracts.ps1` layout or the `ToolProjectSpineTests` contracts.

## Tasks & Acceptance

**Execution:**
- [x] `T/Workspace/PlatformIdentity*.cs` (new, one type per file): the identity record, condition findings, record reader and resolver. Read the record with `git show <identity>:<path>`, the pin with `ls-tree -z <identity>`, tags with `tag --points-at`, and dirtiness with `status --porcelain -z --ignore-submodules=all --untracked-files=all`. Require strict SemVer tags and a regular blob marker; preserve all independently observable findings, avoid duplicates for one unreadable record, choose a stable tag, and accept every schema-valid numeric schemaVersion 1 representation.
- [x] `T/Workspace/WorkspaceRootResolver.cs`, `SubmoduleInitializer.cs`: wire in identity resolution and package-mode Platform/Builds initialization. Require the marker and absence of a direct Platform reference for a Platform root; collect a missing Builds gitlink as HXW009 with other findings, including if initialization fails or the direct Platform reference is missing. Package mode throws one Prerequisite `WorkspaceMappingException` that lists every finding; source mode returns warnings except for the frozen missing-reference failure.
- [x] `T/Workspace/SourceMapping*.cs`, `T/Runtime/CompositionRunPlan*.cs`: carry the identity in the mapping, its hash and the plan.
- [x] `T/Runtime/ModuleCommandExecutionService.cs`: surface source-mode warnings in human and JSON output on both pass and fail.
- [x] `T/Workspace/CatalogVersion*.cs` (new), `SourceMapping.props`, `SourceMapping.targets`: in package mode, evaluate the pinned catalog for each consumer project name without consumer property or environment overrides into a hashed `catalog-versions.json` beside `mapping.json`. Support valid MSBuild condition forms and static relative imports; fail closed on unsupported dynamic or wildcard paths. A generated guard on every package-mode project rejects any referenced catalog-defined package whose resolved version differs (HXW012), including `VersionOverride` and `GlobalPackageReference`; malformed snapshots, absent required assets and missing active-target references fail with HXW012. Avoid the real-catalog output limit, normalize target framework keys, and preserve HXW failures through composition before descriptors run. Convert catalog I/O faults to HXW, clean the private workspace on refusal, and name it if deletion fails.
- [x] `schemas/hexalith.platform-identity.v1.json` (new): record schema. The reader rejects unknown fields and an invalid range.
- [x] `{project-root}/hexalith-platform.json` (new, Platform repository): `schemaVersion` only, with no range. It marks Platform; story 1.9 adds the range.
- [x] `M/Workspace/PlatformIdentity*Tests.cs`: cover every matrix row in both modes through the resolver and the public CLI, including mapping, hash and plan handoff. Add real package-mode MSBuild consumer tests that reject both mismatched `VersionOverride` and a range whose restored package differs, and pass when the local-feed version matches the pin, including a versionless centrally managed reference. Build a conditional Folders.Aspire consumer through generated imports. Cover conditional syntax, large catalog output, relative imports, global references, normalized target keys, missing target packages, multiple tags, and public composition/catalog/tool-range mismatches in human and JSON output. Force a catalog materialization failure and assert HXW009 before descriptors with no orphaned workspace. Add a real CLI process test where CLI and Tooling informational versions differ and assert the CLI version controls HXW010. Keep module manifest pins unchanged.
- [x] `README.md`: document the identity, the record, the conditions and the new HXW rules.

**Acceptance Criteria:**
- Given `run` or `test`, when it starts, then the identity resolves once, and `mapping.json`, its hash and `plan.json` carry the same identity.
- Given a package-mode refusal, when it is reported, then no descriptor, run state or resource exists.
- Given the existing suites, when run, then the Module suite passes. The Evidence suite's accepted 40-case catalog/corpus blocker is unchanged.

### Review Findings

Code review 2026-10-11 of Builds `bd59a07..cdadaba` (commit `cdadaba`) plus Platform `hexalith-platform.json` (`78e47a4..63230cd`). Paths are relative to `references/Hexalith.Builds`, and line numbers are at `cdadaba`. Layers: Blind Hunter, Edge Case Hunter, Verification Gap and Acceptance Auditor; none failed. Totals: 44 raw findings, 1 decision-needed, 12 patch, 1 defer and 19 rejected. Decision resolved 2026-10-11 (recommendation adopted): D1→A, keep the warning; recorded in the Spec Change Log and closed with no code change. All 12 patches were applied 2026-10-11 in the Builds working tree (uncommitted). The command now runs a catalog preflight in a private scratch workspace before its descriptor load, and new tests cover each finding. Before restoring the fixes, the new tests for the code changes were confirmed to fail on the unfixed source. Verification: `dotnet build test/Hexalith.Builds.Module.Tests -c Debug -m:1` had 0 warnings and 0 errors. The Module assembly passed 776 tests with 0 failed and 0 skipped. `pwsh Tools/test-g4-tool-package-contracts.ps1 -Version 0.0.0-ci.1 -SkipSourceValidation -RetainPackageDirectory` passed, and `git diff --check` is clean in Builds and Platform.

- [x] [Review][Decision] Source mode now only warns on a moved Builds checkout (medium; decision D1→A: keep the warning, recorded in the Spec Change Log) — In story 1.3, `InitializeDirectAsync` failed HXW004 when an initialized direct checkout's HEAD differed from its staged gitlink. Source mode now leaves Platform and Builds out of that call. A Builds checkout moved without staging therefore reaches only the HXW009 `checked out` condition and is reported as a warning. For Platform, the frozen composition row covers a checked-out commit that differs, so the warning is specified. For Builds, the frozen catalog row lists only three cases: a gitlink that differs from the pin, an absent Builds, or a dirty checkout. The README documents the new warning, but the spec never authorized relaxing story 1.3's failure. Options: (A) keep the warning, which fits "source mode warns and continues" for catalog conditions, and record that in the spec; (B) restore HXW004 in source mode for a Builds checkout whose HEAD differs from its staged gitlink. [src/libraries/Hexalith.Builds.Tooling/Workspace/WorkspaceRootResolver.cs:244]
- [x] [Review][Patch] Public package `run`/`test` runs descriptor child code before the pinned-catalog preflight (medium) — `ModuleCommandExecutionService` calls `ExecutableDescriptorLoader.LoadAsync`, which launches descriptor child processes, before `CompositionEngine.StartAsync` materializes the catalog. The engine's "must not run descriptor child code" guard protects only its own second load. So a catalog refusal is reported after descriptor code has already run, which breaks AC2 and the Code Map ordering. `PublicCatalogFailureRetainsHxw009BeforeDescriptorsAsync` passes because it gives the service a working child and gives only the engine a missing one. Fix: run the catalog preflight before the service-level descriptor load. Then pass the missing child path to both, or record child launches and assert there are none. [src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs:431]
- [x] [Review][Patch] No test covers a Platform or Builds checkout moved without staging (medium) — Package mode no longer runs HXW004 validation on these references. Refusal now depends only on HXW008 checkout-vs-staged (`:105-109`) and on the HXW009 `buildsHead != buildsStaged` clause (`:179`). Deleting either clause leaves the suite green. Add Platform and Builds cases that check out another commit without staging, asserting a source-mode warning and a package-mode refusal. Expectations depend on the decision above. [src/libraries/Hexalith.Builds.Tooling/Workspace/PlatformIdentityResolver.cs:105]
- [x] [Review][Patch] No test loads a source mapping with identity findings through `LoadVerified` (medium) — The live record has no tool range, so every real source-mode workspace carries HXW010 in `mapping.json` and in its hash. `SourceMappingValidationTask` and `SourceMappingReconciliationTask` reload that mapping in every consumer build. No test round-trips a mapping that has findings, so a serialization change would fail every source-mode build while tests stay green. [src/libraries/Hexalith.Builds.Tooling/Workspace/SourceMapping.cs:41]
- [x] [Review][Patch] No test exercises the catalog snapshot integrity checks (medium) — The checks cover pinned-file hashes, the snapshot `Hash`, `MappingHash` and `CatalogCommit`. The only negative snapshot test hits the separate null-`Versions` branch. No test produces "differs from the command-resolved identity", so a stale or modified snapshot would be accepted if the check regressed. [src/libraries/Hexalith.Builds.Tooling/Workspace/CatalogVersionValidationTask.cs:86]
- [x] [Review][Patch] A source-mode missing-reference failure renders warning findings as failures (low) — Both throws call `Diagnostics(..., warning: false)` regardless of mode. Builds dirty (HXW009), range unavailable (HXW010) and dirty Platform (HXW011) therefore get category `PrerequisiteUnavailable` next to the real HXW007. The task says "source mode returns warnings except for the frozen missing-reference failure." Every not-yet-enrolled module hits this. Keep only the missing-reference HXW007 fatal in source mode. Same issue at `:232`. [src/libraries/Hexalith.Builds.Tooling/Workspace/WorkspaceRootResolver.cs:207]
- [x] [Review][Patch] A valid tag whose build metadata contains a hyphen is rejected (low) — `IsReleaseTag` takes the first `-` anywhere in the tag. For `v1.0.0+build-01` it reads `01` as a prerelease identifier and rejects it, producing a spurious HXW011. Look for `-` only before `+`. [src/libraries/Hexalith.Builds.Tooling/Workspace/PlatformIdentityResolver.cs:334]
- [x] [Review][Patch] The isolated catalog evaluation still reads `Directory.Build.rsp` (low) — `dotnet msbuild <wrapper> -getItem:PackageVersion` runs without `-noAutoResponse`. A `Directory.Build.rsp` in an ancestor of the run workspace can therefore pass `-p:` overrides into the "isolated" pins. Add `-noAutoResponse`. [src/libraries/Hexalith.Builds.Tooling/Workspace/CatalogVersionMaterializer.cs:269]
- [x] [Review][Patch] An unmatched target framework throws instead of reporting "no active target" (low) — `FirstOrDefault` on `JsonProperty` returns `default`, not `null`. So `selectedFramework.Name` throws `InvalidOperationException`, and the generic "Pinned catalog validation failed: Operation is not valid…" HXW012 replaces the specific message. Use `.Where(...).Select(p => p.Name).FirstOrDefault()`. [src/libraries/Hexalith.Builds.Tooling/Workspace/CatalogVersionValidationTask.cs:140]
- [x] [Review][Patch] README rule descriptions are narrower than how the code uses them (low) — The code also emits HXW009 for catalog evaluation, XML, import, I/O and private-workspace cleanup failures. It also emits HXW012 for malformed or modified snapshots, missing assets and missing active targets. The README describes only pin/checkout and version mismatches. Document the actual coverage. [README.md:297]
- [x] [Review][Patch] README omits the Platform-repository exception in package mode (low) — Inside Platform, the identity is HEAD, and the matrix drops the tag and clean-root requirements (`PlatformRepositoryUsesHeadAsync` passes with a dirty tree). The README states the requirements without that exception. [README.md:276]
- [x] [Review][Patch] No test rejects a `schemaVersion` other than 1 (low) — `NumericSchemaVersionOneIsAcceptedAsync` covers only accepted forms. Dropping the exponent check would accept `10`, `0.1` or `1e3` without a failing test. Add a rejecting theory (`2`, `10`, `0.1`, `1e1`, `-1`, `"1"`). [src/libraries/Hexalith.Builds.Tooling/Workspace/PlatformIdentityResolver.cs:282]
- [x] [Review][Patch] No test covers identity warnings on source-mode resolver failures (low) — The failed Platform/Builds initialization at `:235` and the warning append on a later HXW001/HXW004 at `:277` are never reached in source mode by any test. Deleting either leaves the suite green, although the frozen rule says warnings appear whether the run passes or fails. [src/libraries/Hexalith.Builds.Tooling/Workspace/WorkspaceRootResolver.cs:277]
- [x] [Review][Defer] Module manifests require EventStore 3.119.0 while the catalog and G-4 acceptance require 3.120.0 [src/libraries/Hexalith.Builds.Tooling/Manifest/ModuleManifestLoader.cs:33] — deferred: the pre-existing catalog bump `9e6b6ca` created this. This story keeps 3.119.0 on purpose (Code Map, R2-BH9). Package mode will enforce 3.120.0 packages for manifests that must declare 3.119.0, and `G4P0AcceptanceValidator` still checks `SupportedPlatformPins`. A manifest pin migration story must realign them.

Rejected:
- BH2, release tag chosen by ordinal rather than SemVer precedence (low): the spec asks only for a deterministic choice (R3-BH5). Nothing reads `ReleaseTag` yet, and several release versions on one commit is unrealistic.
- BH4 / EH1, unborn HEAD fails source mode (low): it needs a module repository with no commits. A clean fix must restructure three HEAD probes to avoid duplicate HXW007s.
- BH5 / EH13 / AA6, early run ID leaves a state record on cancellation or a non-HXW exception before descriptors (low): the record is inert; only `down` reads it, by ID. Before this change, I/O failures in the materializer already took this path. Fixing it needs new pre-run catch branches.
- BH9, the schema forbids `$schema` (false): the spec requires the reader to reject unknown fields. The schema-versus-reader parity gap is covered by the `schemaVersion` patch above.
- BH11, parallel on-demand evaluation and lock timeout (low): the lock covers only a read-compare-write, so the 30-second timeout is unreachable in practice. Each new consumer name needs its own evaluation regardless.
- BH12, finding messages in the mapping hash (false): the hash is compared only inside one run, between command-resolved and reloaded, using the same tool. Nothing compares it across versions.
- BH14 / EH4, catalog conditions on properties other than project name (low): the pinned catalog has none. Detecting or evaluating them needs new parsing.
- EH3, `git show` cap on a large catalog (false): the pinned catalog is 29,224 bytes, below the 65,536 cap, and the frozen rule requires failing closed on truncation (R3-BH7).
- EH6, a range-valued catalog `Version` (low): the pinned catalog uses bare versions, and this was already rejected in R5-BH2. It fails closed.
- EH7, an optional import created in `pinned-tree` after materialization (low): it needs writes inside the private run workspace, and the pinned catalog has no imports.
- EH8, `dotnet` not on PATH (low): the AppHost's consumer builds also launch `dotnet` from PATH, so such a run fails regardless. Reusing the engine's host resolver adds plumbing.
- EH9, a differently cased `References/Hexalith.Platform` (low): the frozen identity is the literal `references/Hexalith.Platform` path, and the fix adds comparers across several call sites.
- EH10, an extra HXW009 "checked out absent" on a missing-reference refusal (low): it is accurate, appears only in an already failing result, and suppressing it needs a new parameter.
- EH11, warnings ordered before the causal diagnostic (false): the outcome carries the causal rule, category and exit code independently, and no consumer reads `Diagnostics[0]` from the merged list. The ordering is chronological.
- EH12, an evidence-write failure on a passing run drops identity warnings (low): it needs an evidence write failure. `MergeEvidenceFailure` already replaced all diagnostics on that path before this story, so a fix must filter which ones to keep.
- EH15, a declared Platform reference with no stage-zero gitlink fails before identity resolution (false): the Code Map keeps "the usual stage-zero validation for other direct references" and exempts only Builds.
- AA2, not every matrix row is tested through the public CLI in both modes (low): package composition, catalog and tool mismatches, multi-condition refusal and the source warning channel are covered publicly, and the resolver covers each row. The remaining rows differ only in rule text on the same rendering path.
- AA3, HXW012 not shown in CLI output (low, maybe-false): the matrix requires only that the build stop and name the package, which the MSBuild error does. Surfacing resource build logs belongs to story 1.8.
- VG6, engine cleanup on its own descriptor/HXW006 refusals is untested (low): in the public command, the service-level checks shadow these engine paths, and the descriptor-ordering patch above will reshape them anyway.

## Implementation Notes

## Spec Change Log

- 2026-10-10 review loop 1: The first implementation evaluated the pinned catalog once under `pinned-catalog.proj`, so the Folders.Aspire conditional version was replaced by the wrapper default; raw version comparison also rejected equivalent exact ranges. Review also found marker-only Platform detection, missing Builds stage-zero findings, excluded host guards and no build-level HXW012 test. The Code Map and tasks now require consumer-specific isolated evaluation, semantic version checks, complete identity detection and build-level verification. Known-bad state avoided: a green task unit test alongside a package build that accepts the wrong pin or rejects a valid package. KEEP: the local-object identity model, no-fetch Git checks, mapping and plan hash handoff, source warning channel, HXW diagnostic aggregation, file-protocol fixtures, the Platform record without a range, and the previously passing 708-case Module suite and package contract gate. The reviewed first-pass patch is preserved at `/tmp/story-1-4-diff.kkc5Uc` as an implementation reference; the spec remains authoritative.
- 2026-10-10 review loop 2: The second implementation handled conditional catalogs and passed 716 Module tests, but the real catalog's evaluated output exceeds the generic process cap. Review also found cross-framework asset comparisons, omitted global references, malformed marker acceptance, lost diagnostics on catalog errors or missing identity objects, a case-sensitive condition scan, insufficient post-restore integration proof, and an unrelated module-manifest pin migration. The Code Map and tasks now require bounded real-size catalog evaluation, active-framework resolution, complete diagnostics, regular-blob marker validation and local-feed integration tests while preserving the existing manifest pin. Known-bad state avoided: every compatible package run failing before composition, a wrong package escaping or being falsely rejected, and 3.119.0 manifests becoming invalid. KEEP: the second pass's conditional catalog snapshot, semantic NuGet comparisons, generated/Builds host guard coverage, source warning channel, complete mapping/plan identity handoff, strict SemVer and dirty checks, real pre-restore build test, 716 passing Module tests, and package contract gate. The reviewed second-pass patch is preserved at `/tmp/story-1-4-review2.jwcnAg` as an implementation reference; the amended spec remains authoritative.
- 2026-10-10 review loop 3: The third implementation passed 726 Module tests and the package contract gate, but review found a catalog materialization refusal after descriptor execution, weak coverage of successful/conditional generated builds, incomplete or normalized NuGet assets handling, and lost findings on initialization failure. It also identified unstable tag choice, orphaned workspace cleanup, valid MSBuild condition and relative-import handling. The Code Map and tasks now require catalog preparation before descriptors, cleanup on refusal, a stable release tag, complete active-target validation, robust isolated catalog evaluation and public/build integration tests. Known-bad state avoided: descriptor code executing for a doomed package run, false acceptance or rejection after restore, and unobservable or orphaned failures. KEEP: the third pass's real-size catalog capture, active-framework assets selection, global references, local-feed rejection test, root blob marker, failure aggregation, preserved HXW diagnostics, 3.119.0 manifest compatibility, 726 passing Module tests and package contract gate. The reviewed third-pass patch is preserved at `/tmp/story-1-4-review3.diff` as an implementation reference; the amended spec remains authoritative.
- 2026-10-10 review loop 4: The fourth implementation passed 738 Module tests and package contracts, including pinned relative imports, compound and on-demand project conditions, local-feed success/rejection, normalized assets and public catalog-preflight cleanup. Review found unclassified local catalog I/O, missing independent Builds dirtiness on an absent Platform declaration, weak central-package and executable CLI-version coverage, and incomplete public mismatch assertions. The Code Map and tasks now require HXW I/O diagnostics, complete missing-reference aggregation, versionless central-package and divergent CLI process tests, and explicit public mismatch rendering. Known-bad state avoided: HXR020 masking a catalog read failure, silent omission of a second mismatch, and a wrong assembly version deciding package eligibility without executable proof. KEEP: the fourth pass's pinned-tree hash binding, bounded on-demand evaluation, pre-descriptor catalog preflight, cleanup, stable tag choice, 3.119.0 manifest compatibility, all 738 passing Module tests and package contract gate. The reviewed fourth-pass patch is preserved at `/tmp/story-1-4-review4.diff` as an implementation reference; the amended spec remains authoritative.

- 2026-10-11 code review: decision D1→A. A Builds checkout moved without staging is a catalog condition: source mode reports HXW009 as a warning and continues, and package mode refuses. Story 1.3's HXW004 no longer applies to the Platform and Builds identity references in source mode, matching the frozen composition row for Platform. KEEP: package-mode refusal for both references, and the README statement of this behavior.

## Review Triage Log

| Finding | Verdict | Route and evidence |
|---|---|---|
| BH1 marker bypass | medium | bad_spec: `PlatformIdentityResolver` treats any HEAD marker as Platform even when a direct Platform reference exists; the frozen identity rule requires both conditions. |
| BH2 Aspire before identity | false | Existing prerequisite probing can stop before mapping; the frozen rule requires identity inside resolution before descriptors, run state and resources, and warnings after resolution, not before an unavailable Aspire prerequisite. |
| BH3 nonexecutable root | false | A nonexecutable root manifest cannot enter composition and exits with its established HXR003 validation result; no Platform identity is used for a build or launch. |
| BH4 inherited catalog override | false | `CompositionProcess.CreateStartInfo` clears the process environment and passes only an allowlist; catalog version override variables are not inherited by the isolated evaluation. |
| BH5 conditional catalog version | medium | bad_spec: the pinned catalog sets `CommunityToolkit.Aspire.Hosting.Dapr` to 13.0.0 for `Hexalith.Folders.Aspire`, while the wrapper project evaluates the preview default once for every consumer. |
| BH6 equivalent version text | medium | bad_spec: the task compares raw strings before restore, so `[1.0.0]` can fail against a 1.0.0 pin despite resolving to the same version. |
| BH7 untracked suppression | medium | patch: Git's `status.showUntrackedFiles=no` can suppress untracked changes from the current porcelain command; force untracked reporting in the dirty check. |
| BH8 invalid prerelease tag | medium | patch: the release-tag regex accepts numeric prerelease `01`, which SemVer forbids; enforce strict SemVer. |
| BH9 excluded hosts | medium | bad_spec: generated hosts and two Builds hosts have catalog-defined package references, yet the new guard inherits the source-mapping condition that excludes them. |
| BH10 transitive package | false | The frozen catalog rule concerns referenced packages; the guard checks effective direct `PackageReference` items, while a transitive library without a project reference is outside that declared set. |
| BH11 absent staged Builds gitlink | medium | bad_spec: `ValidateRecordedGitlinksAsync` throws before identity findings are collected when Builds is declared but absent from the index; the matrix calls for HXW009 alongside other findings. |
| EH1 conditional catalog version | medium | bad_spec: same defect as BH5; `MSBuildProjectName` changes the actual pinned catalog version for Folders.Aspire. |
| EH2 equivalent exact range | medium | bad_spec: same defect as BH6; a syntactically different exact NuGet range can resolve to the pin. |
| EH3 invalid prerelease tag | medium | patch: same defect as BH8; `v1.0.0-01` passes the regex. |
| EH4 missing snapshot versions | low | patch: a null `Versions` deserializes, then `ToDictionary` throws outside the task's caught exception set, replacing HXW012 with a generic MSBuild task error. |
| EH5 root validation warning | false | A root manifest failing initial validation stops before identity resolution; the frozen warning rule concerns a run that fails later, after resolution. |
| VG1 missing build-level guard test | medium | bad_spec: existing tests invoke the task directly and inspect target registration, but none builds a package-mode consumer through generated imports to prove HXW012 fires. |
| R2-BH1 real catalog exceeds process output limit | high | bad_spec: the pinned catalog's `-getItem:PackageVersion` output exceeds `CompositionProcess`'s 65,536-character cap, so `CatalogVersionMaterializer.EvaluateAsync` rejects a compatible package run. |
| R2-BH2 catalog failure becomes HXR020 | medium | bad_spec: `CompositionEngine.StartAsync` materializes after allocating `runId`; its broad catch maps `WorkspaceMappingException(HXW009)` to HXR020, losing the specific catalog failure. |
| R2-BH3 non-blob Platform marker | medium | bad_spec: `ls-tree` output length accepts a symlink or tree named `hexalith-platform.json` as a Platform root; a Git module without a direct Platform reference then avoids the mandatory missing-reference check. |
| R2-BH4 missing identity object loses findings | medium | bad_spec: after recording HXW008, `ls-tree` on an unavailable identity commit throws and the earlier mismatch is discarded instead of joining the refusal. |
| R2-BH5 absent assets skip | low | patch: `CheckAssets=true` with a missing file skips the resolved-version comparison. Although an ordinary `ResolvePackageAssets` build fails before this point, a direct task call reports success without evidence; fail with HXW012. |
| R2-BH6 case-sensitive project condition | medium | bad_spec: MSBuild accepts `$(msbuildprojectname)`, but `ConditionalProjectNames` filters only the canonical casing and can assign the wrong default pin. |
| R2-BH7 global package references | medium | bad_spec: `@(GlobalPackageReference)` is a referenced package source, but the generated guard passes only `@(PackageReference)` and can miss a catalog-defined global package. |
| R2-BH8 schema range syntax | low | reject: JSON Schema's nonempty-string constraint permits malformed NuGet ranges, but the runtime reader rejects them with HXW007; mirroring NuGet's full parser in a schema pattern adds complexity without an everyday bypass. |
| R2-BH9 EventStore manifest pin | medium | bad_spec: changing the public manifest schema constant from 3.119.0 to 3.120.0 rejects previously valid manifests; story 1.4 contains no pin migration requirement. |
| R2-BH10 tiny catalog fixture | medium | bad_spec: fixture-only materialization cannot detect the real pinned catalog output-limit failure demonstrated in R2-BH1; add a bounded-size realistic catalog test. |
| R2-BH11 missing plan test | false | `WorkspaceCommandModeTests.cs:81-82` already checks that serialized `plan.json` identity commit equals the serialized mapping identity commit. |
| R2-EH1 absent assets skip | low | patch: same direct-task behavior as R2-BH5; the ordinary build path fails in `ResolvePackageAssets`, but the task itself silently succeeds if assets are absent. |
| R2-EH2 cross-framework assets | medium | bad_spec: scanning the global `libraries` object can reject a valid target framework when another framework resolves a different version; use the active target's libraries. |
| R2-EH3 early missing Platform gitlink return | medium | bad_spec: `ResolveAsync` returns immediately when HEAD lacks a declared Platform gitlink, before observing an independent dirty or mismatched Builds state. |
| R2-EH4 case-sensitive project condition | medium | bad_spec: same MSBuild property-casing mismatch as R2-BH6; the lowercase condition bypasses project-specific evaluation. |
| R2-EH5 restore-only range check | false | The frozen behavior governs tool `run` and `test` builds, which execute `CoreCompile`; a separate `dotnet restore` command is not a supported invocation of this story's package-mode guard. |
| R2-VG1 no post-restore build verification | medium | bad_spec: task-level fabricated assets and a build rejected before restore do not prove that the generated post-restore target rejects an indeterminate range resolving away from the pin; a local-feed build test is needed. |
| R2-VG2 real catalog output cap | high | bad_spec: independently measured the actual pinned catalog output above the 65,536-character capture bound; this duplicates R2-BH1's production failure. |
| R3-BH1 initialization masks findings | medium | bad_spec: `InitializeDirectAsync` can throw on a mismatched checkout before the identity resolver observes the independently available tool range, dirtiness and Builds pin; the one-result requirement is lost. |
| R3-BH2 absent active-target package | medium | bad_spec: the post-restore task compares only packages present in the assets target and does not fail when a catalog-defined direct reference is absent from that target, so incomplete assets can pass. |
| R3-BH3 unnormalized target framework | medium | bad_spec: the task constructs the target key from raw `TargetFramework`, while NuGet assets can use normalized keys such as `.NETFramework,Version=v4.8`; valid restores can receive HXW012. |
| R3-BH4 failed catalog leaves workspace | low | patch: the special `WorkspaceMappingException` catch returns after creating the run directory but without a run ID in the result, leaving an orphan that cannot be removed through the command. |
| R3-BH5 ambiguous release tags | medium | patch: `git tag --points-at` can be ordered by Git configuration; selecting its first valid tag changes the mapping hash across machines when a commit has several tags. Sort before choosing. |
| R3-BH6 restricted MSBuild condition syntax | medium | bad_spec: the condition-name extractor rejects valid double-quoted or compound project-name expressions although MSBuild can evaluate them; package mode refuses an otherwise valid pinned catalog. |
| R3-BH7 large Git catalog read | false | The current pinned file is below the Git output bound, and the frozen safety rule explicitly requires failure on truncated Git output; catalog growth beyond that bound must fail closed. |
| R3-BH8 relative catalog imports | medium | bad_spec: copying only the pinned props file to the run directory changes its relative import base, so a valid pinned catalog importing sibling files cannot be evaluated correctly. |
| R3-BH9 no successful generated package build | medium | bad_spec: build-level tests assert HXW012 refusals, but none proves the post-restore generated target accepts a locally restored matching package; false refusals could go undetected. |
| R3-BH10 no public multi-finding refusal test | medium | bad_spec: resolver tests collect several findings and source-mode public tests render warnings, but no public package command test asserts several refusal diagnostics in both human and JSON formats. |
| R3-BH11 HXW-preservation path untested | medium | bad_spec: no test forces materialization failure after run ID allocation and checks that the command retains HXW009; the new special catch can regress unseen. |
| R3-EH1 absent active-target package | medium | bad_spec: same post-restore omission as R3-BH2; the task succeeds if the expected direct package is absent from selected assets. |
| R3-EH2 failed catalog leaves workspace | low | patch: same orphaned run directory as R3-BH4; the special catch has no cleanup. |
| R3-EH3 descriptor before catalog refusal | medium | bad_spec: `ExecutableDescriptorLoader.LoadAsync` executes before `SourceMappingMaterializer.WriteAsync`; an invalid pinned catalog can cause package refusal only after descriptors ran, contrary to the acceptance boundary. |
| R3-VG1 conditional consumer name unverified through build | medium | bad_spec: the direct task test sets `ProjectName` manually; build tests use the default catalog and cannot detect a missing generated `ProjectName` attribute for Folders.Aspire. |
| R3-VG2 restore-only range check | false | carried from R2-EH5: the frozen behavior governs `run` and `test` builds, not a standalone `dotnet restore` command. |
| R3-VG3 failed catalog leaves workspace | low | patch: same orphaned directory as R3-BH4; a failed materialization returns without run state or a cleanup handle. |
| R4-BH1 invented default lacks package version | low | reject: the current pinned catalog defines default package versions; a future catalog with only consumer-specific definitions fails closed rather than silently accepting a wrong version. Supporting an absent default would add parsing branches beyond this story's pinned catalog. |
| R4-BH2 property import path | low | reject: dynamic import paths are absent from the pinned catalog and already refuse with HXW009; resolving arbitrary MSBuild expressions in the pinned tree would add a new evaluator rather than a direct correction. |
| R4-BH3 wildcard import path | low | reject: the current pinned catalog has no wildcard imports; the parser fails closed with HXW009 for a future glob instead of using an unpinned file. |
| R4-BH4 unused invalid conditional import | low | reject: a future pinned catalog can contain an invalid unused branch, but its referenced file is still part of that catalog; refusing an unusable pinned input is a safe failure and skipping it needs conditional evaluation logic. |
| R4-BH5 normalized target without alias | maybe-false | defer: the code uses `project.frameworks[*].targetAlias` for normalization; a real supported SDK assets file with a normalized key but no alias is needed to show a false refusal. If demonstrated, the effect would be medium. |
| R4-BH6 catalog I/O mapped to HXR020 | medium | bad_spec: `File.WriteAllTextAsync` and other local I/O in `CatalogVersionMaterializer.WriteAsync` can throw outside `WorkspaceMappingException`; the engine then emits HXR020 instead of a prerequisite HXW diagnostic naming the catalog operation. |
| R4-BH7 cleanup failure hidden | low | patch: `TryDeleteAsync` can return false after ten retries, yet catalog refusal returns no run ID or workspace path; include the path in the refusal if cleanup fails. |
| R4-BH8 on-demand evaluation after descriptors | false | Frozen ordering puts identity resolution before descriptors, while catalog version checks occur during a build; a consumer discovered by descriptor execution cannot be evaluated before that execution. Preflight already handles the pinned catalog's known project conditions. |
| R4-BH9 duplicate unreadable-record diagnostic | low | patch: `TryGitAsync` records HXW007 and the following null-record branch records another HXW007 for the same unreadable object; remove the duplicate while retaining independent findings. |
| R4-BH10 schema accepts numeric 1.0 | low | patch: JSON Schema `const: 1` accepts numerically equal 1.0, while `TryGetInt32` rejects its token; align runtime and schema with a direct numeric check. |
| R4-BH11 versionless central build test | medium | bad_spec: the generated guard reads `@(PackageVersion)` but the build tests do not exercise a versionless `PackageReference` resolved centrally; this common consumer path needs a successful local-feed build. |
| R4-BH12 public mismatch matrix tests | medium | bad_spec: current public tests cover source warnings and one multi-condition package refusal, but composition, catalog and tool-range mismatches are not individually asserted at the human and JSON command boundary. |
| R4-EH1 backslash import | low | reject: a future pinned catalog using backslash separators on this Unix runner fails closed with HXW009; the actual pinned catalog has no relative imports, and cross-platform import normalization needs more than a direct correction. |
| R4-EH2 wildcard import | low | reject: same unsupported future wildcard form as R4-BH3, with safe HXW009 refusal and no current pinned use. |
| R4-EH3 missing Platform plus dirty Builds | medium | bad_spec: `WorkspaceRootResolver` throws on a missing direct Platform reference before identity resolution can report an independently dirty Builds checkout as HXW009. |
| R4-EH4 source missing Platform should warn | false | The frozen missing-reference decision explicitly requires failure in both modes; `WorkspaceRootResolver`'s HXW007 is correct. |
| R4-EH5 cleanup failure hidden | low | patch: same failed-deletion result as R4-BH7; the refusal should name any remaining private workspace. |
| R4-VG1 real CLI version selection untested | medium | bad_spec: in-process tests compare the selector to itself and cannot prove the executable chooses its own informational version when CLI and Tooling versions differ; add a process-level divergent-version check. |
| R5-BH1 empty evaluated catalog version | low | patch: `EvaluateAsync` adds an empty Version string to its dictionary, while preflight does not validate it; the task rejects it only during a later build. Reject the empty value while parsing the isolated evaluation. |
| R5-BH2 exact range in catalog pin | low | reject: the current pinned catalog uses bare versions; supporting future range-valued `PackageVersion` pins would add a second semantic pin representation, while the present code fails closed rather than mixing versions. |
| R5-BH3 on-demand HXW009 becomes HXW012 | low | patch: `EvaluateForProjectAsync` throws an HXW009 mapping failure, but the build task's generic catch reports HXW012 at the snapshot. Preserve the original rule and failing path for this specific exception. |
| R5-BH4 temporary evaluation cleanup failure | low | patch: `EvaluateForProjectAsync` ignores `TryDeleteAsync` returning false, leaving a private `catalog-evaluations` directory without a reported path. Report that path on the failure result. |
| R5-BH5 descriptor and mapping cleanup failure | low | patch: `CompositionEngine` also ignores failed private-workspace deletion on descriptor or unmapped-module refusal; append a diagnostic naming the remaining path. |
| R5-BH6 duplicate catalog Git-read HXW007 | low | patch: `TryGitAsync` records a failed `git show`, then the null-catalog branch records HXW007 again for the same object; retain the first diagnostic only. |
| R5-BH7 blank ProjectName defaults | false | Generated MSBuild targets pass `$(MSBuildProjectName)` from a named project file; an empty name is not reached by a package-mode build, and a direct task invocation is outside the command path. |
| R5-BH8 case-insensitive project cache | low | reject: the pinned catalog has no case-sensitive property-function names that differ only by case; supporting a future pair of such project names would require a cache/hash redesign and current behavior fails closed or uses the present pin. |
| R5-BH9 public catalog I/O test | low | reject: direct materializer I/O and public malformed-catalog preflight tests exercise the respective conversion and command boundary; a second public fault-injection case is unlikely to add a distinct everyday failure and requires test-only I/O seams. |
| R5-BH10 negative central-version build | low | reject: the task uses the same `DefinitelyDifferent` comparison after selecting `PackageVersion` metadata; existing mismatched explicit-version builds and successful central-version builds cover the two mechanisms without a duplicate local-feed failure test. |
| R5-EH1 marker changes between probes | low | patch: if an external HEAD move removes the marker between the early check and identity resolution, source mode can return a null identity with warnings; fail with HXW007 when neither a direct Platform reference nor a resolved Platform root remains. |
| R5-EH2 invalid record yields HXW010 | false | HXW007 names the invalid record while HXW010 separately names the unavailable tool range; both are holding conditions required by the frozen matrix, not duplicate object-read findings. |
| R5-EH3 duplicate catalog Git-read HXW007 | low | patch: same duplicate read as R5-BH6; the second null-catalog finding repeats the `TryGitAsync` diagnostic. |
| R5-EH4 descriptor and mapping cleanup failure | low | patch: same unreported private workspace as R5-BH5; include its path if bounded deletion fails. |

## Design Notes

**Definitions:**
- *Identity:* in a module workspace, the `references/Hexalith.Platform` gitlink in the superproject's HEAD tree. Inside Platform, it is HEAD.
- *Composition commit:* the Platform staged gitlink, which story 1.3 requires the checkout HEAD to equal. A locally staged, uncommitted Platform bump is therefore a composition mismatch. That makes it a source-mode warning rather than story 1.3's HXW004 failure.
- *Pin:* the Builds gitlink in the identity's tree. It is compared with the workspace's staged Builds gitlink and a clean Builds checkout.
- *Release tag:* a local `v` + SemVer 2.0 tag, annotated or lightweight, that points at the identity.
- *Dirty:* any `status --porcelain` entry other than a nested submodule.
- *Tool version:* the CLI's informational version without `+metadata`. The range uses NuGet `VersionRange`.

**Rules:**

| Rule | Condition |
|------|-----------|
| HXW007 | Identity unresolvable: missing reference, no Git, or unreadable record or pin |
| HXW008 | Composition mismatch |
| HXW009 | Catalog-pin mismatch |
| HXW010 | Tool outside the range, or range unavailable |
| HXW011 | Platform submodule untagged or dirty |
| HXW012 | Package version differs from the catalog (package mode) |

Example source-mode warning: `HXW009 Hexalith.Builds: references/Hexalith.Builds is 6a002df5…; Platform 56eeda39… pins 19bf05ec…`

## Verification

**Commands** (run from Builds):
- `dotnet build test/Hexalith.Builds.Module.Tests -c Debug -m:1`: zero warnings and zero errors. Then run the Module xUnit assembly directly: all tests pass, none skipped.
- `pwsh Tools/test-g4-tool-package-contracts.ps1 -Version 0.0.0-ci.1 -SkipSourceValidation -RetainPackageDirectory`: passes.
- `git diff --check` in Builds and in the Platform root: clean.
