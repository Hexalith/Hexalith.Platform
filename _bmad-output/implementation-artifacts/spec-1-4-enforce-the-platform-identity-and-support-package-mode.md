---
title: '1.4 Enforce the Platform identity and support package mode'
type: 'feature'
epic: 1
story: 4
created: '2026-10-10'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
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
  - Initialize Platform and Builds in package mode, then evaluate the identity.
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
- `T/Runtime/ModuleCommandExecutionService.cs:99,320,425,545,552-586,680-700`:
  - 99 and 320: resolve calls. 425: descriptors. 545: `StartAsync`.
  - 552-586: result diagnostics. 680-700: `WorkspaceMappingException` catch.
  - Warnings need a return channel from resolution and a merge into the final result.
- `T/Diagnostics/ToolDiagnostic.cs`, `ToolDiagnosticFormatter.cs`: there is no severity field. Non-fatal diagnostics use `ToolFailureCategory.None` (see HXI001/004). Keep the JSON shape.
- `Props/Directory.Packages.props`: the catalog. Its versions are condition-overridable properties.
  - Evaluate the pinned file in a wrapper project, for example `dotnet msbuild -getItem:PackageVersion` through `CompositionProcess`, reading the pinned tree rather than the working copy.
  - Enforce in `T/Workspace/SourceMapping.targets` with the hashed-mapping pattern of `SourceMappingValidationTask`.
- `T/RunEvidence/ModuleRunEvidenceFactory.cs:166-176`: reads the tool version from `AssemblyInformationalVersion`. Reuse it, stripping build metadata, and add a test seam.
- `M/Workspace/WorkspaceGitFixture.cs:46,110,143`, `WorkspaceRootResolverTests.cs`, `WorkspaceCommandModeTests.cs:25-89,416-570`: the fixture and public CLI patterns. Extend the fixture with file-protocol Platform and Builds repositories that carry tags, the record and a Builds gitlink.
- Live state:
  - The Platform root has no tags, its Builds gitlink is `19bf05ec`, and it has no record yet (this story adds it).
  - Parties declares Platform `56eeda39` (uninitialized) and Builds `6a002df5`.
- Do not change: the `Tools/test-g4-tool-package-contracts.ps1` layout or the `ToolProjectSpineTests` contracts.

## Tasks & Acceptance

**Execution:**
- [ ] `T/Workspace/PlatformIdentity*.cs` (new, one type per file): the identity record, condition findings, record reader and resolver. Read the record with `git show <identity>:<path>`, the pin with `ls-tree -z <identity>`, tags with `tag --points-at`, and dirtiness with `status --porcelain -z --ignore-submodules=all`.
- [ ] `T/Workspace/WorkspaceRootResolver.cs`, `SubmoduleInitializer.cs`: wire in identity resolution and package-mode Platform/Builds initialization. Package mode throws one Prerequisite `WorkspaceMappingException` that lists every finding; source mode returns warnings.
- [ ] `T/Workspace/SourceMapping*.cs`, `T/Runtime/CompositionRunPlan*.cs`: carry the identity in the mapping, its hash and the plan.
- [ ] `T/Runtime/ModuleCommandExecutionService.cs`: surface source-mode warnings in human and JSON output on both pass and fail.
- [ ] `T/Workspace/CatalogVersion*.cs` (new), `SourceMapping.props`, `SourceMapping.targets`: in package mode, evaluate the pinned catalog into a hashed `catalog-versions.json` beside `mapping.json`. A generated guard rejects any referenced catalog-defined package whose effective version differs (HXW012), including `VersionOverride`.
- [ ] `schemas/hexalith.platform-identity.v1.json` (new): record schema. The reader rejects unknown fields and an invalid range.
- [ ] `{project-root}/hexalith-platform.json` (new, Platform repository): `schemaVersion` only, with no range. It marks Platform; story 1.9 adds the range.
- [ ] `M/Workspace/PlatformIdentity*Tests.cs`: cover every matrix row in both modes through the resolver and the public CLI, including mapping, hash and plan handoff.
- [ ] `README.md`: document the identity, the record, the conditions and the new HXW rules.

**Acceptance Criteria:**
- Given `run` or `test`, when it starts, then the identity resolves once, and `mapping.json`, its hash and `plan.json` carry the same identity.
- Given a package-mode refusal, when it is reported, then no descriptor, run state or resource exists.
- Given the existing suites, when run, then the Module suite passes. The Evidence suite's accepted 40-case catalog/corpus blocker is unchanged.

## Implementation Notes

## Spec Change Log

## Review Triage Log

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
