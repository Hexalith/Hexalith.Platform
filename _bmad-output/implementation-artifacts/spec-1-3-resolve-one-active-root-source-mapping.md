---
title: '1.3 Resolve one active-root source mapping'
type: 'feature'
epic: 1
story: 3
created: '2026-10-09'
status: 'in-review'
baseline_commit: '4a41508c354acdccd7e18d6f71ad6f7650295714'
route: 'dispatch'
review_loop_iteration: 6
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
- `T/Runtime/ModuleCommandExecutionService.cs:248-294`: runs the prerequisite gate (248), the Aspire probe (274), then descriptors (294). Resolve and initialize before descriptors. If checkout moves the active direct gitlink, the command must use the post-checkout manifest consistently; an already parsed pre-checkout object must not reach descriptors, profiles or evidence.
- `T/Runtime/CompositionEngine.cs:57-112, 572-614`: `StartAsync` re-checks; its first side effects are at 109-112. The AppHost environment is scrubbed at 572-614, so allow-list the mapping there.
- `T/Runtime/CompositionEngineOptions.cs`, `CompositionRunPlan.cs`, `CompositionRunPlanFactory.cs`: carry the mapping (additive `plan.json` field).
- `A/RunTopology.cs:157`: launches modules with `AddProject(module.ProjectPath)`; apply the mapping there and verify paths are mapped. Leave host projects at 194-207 alone.
- `T/Runtime/NativeTestExecutor.cs:47,99-104`: validates the native test project against the manifest repository and runs `dotnet test` with no configuration or properties. Also validate the primary test project against the source mapping before launching it.
- `T/Manifest/ManifestPathValidator.cs:23-42`: `FindRepositoryRoot` stops at the nearest `.git`. Reuse it only as the no-Git fallback.
- Reuse: `T/Filesystem/RepositoryPathResolver.cs` for symlink-safe containment, `T/Runtime/CompositionProcess.cs` for bounded processes, the `T/Manifest/PlatformManifestValidator.cs:76-88` diagnostic pattern, and `T/Hexalith.Builds.Tooling.csproj:17-42` for embedded resources (MSBuild files without a layout change).
- `M/CompositionTestFiles.cs`, `CompositionPrerequisiteProbeTests.cs`, `ModuleCommandApplicationTests.cs`: test patterns to follow. No Git test helper exists yet.
- Evidence: Parties `Directory.Build.props:4-35` holds the probe chains the mapping must override (`HexalithXRoot`, `UseHexalithProjectReferences`, `HexalithXFromSource`). No nested submodules are initialized in this workspace.
- Parties also probes `HexalithCommonsHttpFromSource` and `HexalithCommonsServiceDefaultsFromSource`; its project files use both independently of `HexalithCommonsFromSource`. EventStore `Directory.Build.props:23-30` sets `HexalithTenantsBasePath`, which its project files use for Tenants source references. The materializer must override these consumer inputs consistently with the selected identity.
- The generated MSBuild target must validate physical `ProjectReference` paths, including symlink traversal, with the filesystem's case rules. Do not treat a lexical `FullPath` prefix as containment. Host shim scope depends on the private `workspace/hosts` path, not a project-name match. Reject identities whose generated property suffixes collide; escape values for both XML and MSBuild condition syntax.
- Boundary tests must exercise the public CLI through resolution, the engine-written plan and mapping file, the native test process environment, and composed AppHost module project settings. Helper-only assertions do not prove these handoffs.
- `WorkspaceRootResolver` must physically validate the manifest path, including symlinks. `SourceMapping.ContainsProject` must classify the physical project path under `references/`, so an alias inside the active root cannot make a package-only reference look mapped. Nested preflight must inspect existing direct directories even without their own `.git` marker and distinguish actual or stale submodules from independent nested Git repositories.
- `Directory.Build.targets` may select `ProjectReference` items from probes before the late mapping import. Ensure the selected source/package items themselves follow the tool mode, including when a consumer target resets a probe. A late final-property check alone is insufficient. Git process setup must respect normal protocol restrictions in production and preserve the selected transport's authentication environment; file-backed test fixtures remain hermetic.
- Pair boundary tests across both modes: public `run` and `test`, engine-written source and package plans, and composed AppHost Debug/Release module resources.
- Git initialization must verify each checked-out HEAD equals the root's recorded gitlink even when local `submodule.*.update=none` suppresses checkout. Validate identities and every generated property name, including fixed mapping properties, before initializing direct references. Match direct manifest paths with the containing filesystem's case rules.
- Carry Git's normal transport configuration for HTTPS proxies and custom XDG config as well as SSH credentials without overriding protocol restrictions. A direct reference whose source project has a distinct `PackageId` must select the correct source by package identity, not the project filename. A package-origin identity must only convert project references physically in that package-origin root; keep mapped active-module projects even when filenames share that prefix. Preserve applicable dependency metadata during source/package item conversion or reject unsupported conversion with a diagnostic. Escape MSBuild item wildcard characters in generated paths.
- Validate unmapped project references before implicit restore can evaluate them. Validate descriptor and UI assembly paths against the source mapping before executing descriptor children. After checkout, recompute executable routing from the refreshed manifest, not the pre-checkout shape.
- Add a public `run` or `test` case that reaches an engine-written plan and verifies the command-resolved mapping and mode; test the spawned native process in both modes.
- Reconciliation must index only package candidates belonging to a mapped source identity, excluding archived evidence, tools, samples and unrelated duplicate filenames. Prove the candidate rule against the real root-declared EventStore tree without mutating it, then prove a normal successful restore/build selects project origin in source mode and central package origin in package mode.
- Evaluate effective `PackageId` with imported props, conditions and the selected configuration; preserve it through conversion. For an uninitialized package root, use the central catalog and declared module identity to resolve the package ID unambiguously without opening source, or fail with an actionable mapping diagnostic. Apply duplicate checks to package IDs within the source module's accepted identity prefix.
- Handle staged gitlink state consistently with Git's update target. Keep no-Git project containment compatible with local `references/` paths. Detect path casing from the containing filesystem, not from the OS alone. Preserve Git HTTPS certificate configuration in the scrubbed environment. Reject stale nested gitfile markers even when their target Git directory is gone.
- Preserve or explicitly reject every source/package item metadata field that changes dependency behavior, including project configuration/platform and package asset-selection metadata. Run project containment validation before explicit `ResolveProjectReferences` paths as well as restore. Test mapped MTP native arguments in both modes and use portable process doubles for Windows boundary checks.
- Enforce the selected configuration for retained ProjectReference items as well as converted items. Reject ProjectReference version metadata that would become a PackageReference version, and package metadata that would become active project configuration. Treat multiple absent project paths behind one package-origin identity as ambiguous even when the catalog has one candidate. Check catalog identity for each path rather than collapsing unrelated references.
- If `.gitmodules` is absent from the working tree, detect tracked declarations or gitlinks before treating the root as having no references. Physically validate a no-Git manifest symlink. Preserve Git client-certificate settings (`GIT_SSL_CERT` and `GIT_SSL_KEY`) alongside existing transport settings. Validate persisted mapping content against its hash before MSBuild tasks select dependencies.
- Preserve the prior `HXR003` behavior for a nonexecutable manifest when Aspire is unavailable, while still reloading after a recorded-gitlink checkout. Add a successful normal package-mode build that starts with a ProjectReference. Prove public test mode reaches the spawned native process with the command-resolved hash, and assert the spawned AppHost process environment and returned UI marker mapping guard.
- Restrict source candidates to production package projects even when tools, samples or evidence are nested under `src`; do not let an unrelated matching package ID select source. Treat a retained nested `.git` directory from an old direct-reference revision as an initialized nested submodule when ownership can be established, while allowing independent repositories.
- The root-declared Commons checkout keeps production packages under `src/libraries/<project>/<project>.csproj`; source candidate discovery must include this layout as well as `src/<project>/<project>.csproj`. Exclude utility and archived path segments at any depth.
- Do not change: `Tools/test-g4-tool-package-contracts.ps1:762-768` layout, `ToolProjectSpineTests` contracts.

## Tasks & Acceptance

**Execution:**
- [x] `T/Workspace/*.cs` (new, one type per file): mode, mapping and entry records, root and `.gitmodules` resolver, submodule initializer and MSBuild materializer. Validate manifest and project paths physically, preflight real or stale nested submodules before update, verify the selected recorded gitlink with staged changes, reject property collisions before checkout, and preserve normal Git transport configuration. Keep no-Git behavior and filesystem-specific casing.
- [x] `T/Workspace/SourceMapping.props`, `SourceMapping.targets` (embedded): set mode and consumer-specific flags. Reconcile only real mapped package candidates by effective `PackageId`, including imports and conditions; resolve uninitialized package origins from the central catalog where unambiguous. Preserve or reject behavioral metadata. Validate project paths before restore and explicit project-reference resolution; reject duplicate identities and unmapped paths with `HXW` diagnostics.
- [x] `C/ModuleCommandApplication.cs`, `T/Runtime/ModuleCommandExecutionService.cs`, `CompositionEngine*.cs`, `CompositionRunPlan*.cs`, `NativeTestExecutor.cs`, `A/RunTopology.cs`: mode option; resolve once; use the post-checkout manifest for executable routing and profiles; validate module, descriptor/UI assembly, and native test projects before execution; carry the mapping to test and launch. Scope private host exclusion by path only.
- [x] `M/Workspace/*Tests.cs` plus a Git helper: cover every matrix row hermetically and verify the actual EventStore candidate set. Prove staged gitlink, no-Git local references, dynamic PackageId, package-only uninitialized roots, transport/casing/stale markers, metadata and normal successful restore origin in both modes. Verify public command, engine plan, AppHost and both VSTest/MTP native process handoffs across supported OSes.
- [x] `README.md`: document the mapping, mode, initialization rules and `HXW` diagnostics.
- [x] Review iteration 5 corrections: filter production source candidates; enforce retained and converted item metadata; reject ambiguous absent package paths; detect absent tracked `.gitmodules` and retained directory-form nested submodules; validate no-Git manifest symlinks and mapping hash; preserve client certificates and nonexecutable prerequisite behavior; and add the successful conversion and cross-boundary tests named above.

**Acceptance Criteria:**
- Given a `run` or `test`, when it starts, then the resolver executes once, and native tests and every launched module project use the same mapping content hash.
- Given a consumer whose `Exists()` probes point at sibling or ancestor checkouts, when evaluated under the mapping, then mapped roots and flags win.
- Given the existing suites and installed-tool probes, when run, then they pass unchanged.

## Implementation Notes

- Preserve the proven single resolver handoff, recorded-gitlink direct initialization, persisted mapping/hash, package-asset validation task, physical project-reference check, consumer-specific flags, collision and condition safety, path-scoped host exclusion, fail-closed Git probe and hermetic fixtures. The second review diff at `/tmp/story-1-3-rederive-FEKI9O.patch` is reference material only; this spec controls the next re-derived implementation.
- Live Aspire composition was not exercised in the first verification run. The revised tests must prove the handoffs at process and resource boundaries without requiring external services.
- KEEP the third pass's 529/529 Module and 107/107 Evidence baseline, mapping persistence/hash, physical path checks, nested ownership scan, package asset validation, consumer-specific flags, post-checkout reload, initial item reconciliation, Git protocol restraint and SSH environment handling, and both-mode engine/AppHost tests. The third review diff at `/tmp/story-1-3-final-Qah1Uv.patch` is reference material only.
- KEEP the fourth pass's 544/544 Module and 107/107 Evidence baseline, public command-to-engine plan capture, both-mode native VSTest handoff, descriptor guard, pre-restore path test, exact checkout verification, physical-origin reconciliation, metadata preservation/rejection, and wildcard/XDG tests. The fourth review diff at `/tmp/story-1-3-fourth-5e1v77.patch` is reference material only.
- KEEP the fifth pass's 559/559 Module and 107/107 Evidence baseline, normal source and package origin builds, effective PackageId evaluation, catalog fallback, index gitlink verification, no-Git local references, filesystem-aware casing, stale gitfile handling, both native test platforms, and portable process doubles. The fifth review diff at `/tmp/story-1-3-review-tPjP2x.patch` is reference material only.

## Spec Change Log

- 2026-10-09, review iteration 1: B1–B4, B6–B9, B11, E2–E5 and V1–V4 showed that generic per-identity properties and helper tests were insufficient. Expanded the Code Map and tasks for Parties Commons variants, EventStore Tenants base path, nested checkout preflight, physical and case-aware paths, generated-name and condition safety, path-only host scoping, and command/engine/native/AppHost boundary tests. This avoids the reviewed state in which package mode could still select source, symlinks could evade `HXW006`, valid paths could break MSBuild, and handoff regressions could pass tests. KEEP the working single-resolution pipeline, direct gitlink behavior, mapping persistence/hash, late import, versioned duplicate diagnostic, fail-closed Git probe and hermetic tests.
- 2026-10-09, review iteration 2: B1–B5, B7, B9–B10, E1–E2 and V1–V3 showed missed physical manifest and primary-project checks, a stale loaded manifest after gitlink checkout, consumer target item decisions before late imports, nested-repository ownership errors, production Git transport issues, and one-mode-only boundary tests. Expanded the Code Map and tasks to make these explicit. This avoids the known-bad state where a command builds unmapped or stale source, rejects a valid independent fixture, or misses a mode regression. KEEP the package-asset validation task, physical symlink-safe `ProjectReference` check, consumer-specific flags, condition/collision safety, host-path scope, mapping persistence and passing 517 Module/107 Evidence baseline.
- 2026-10-09, review iteration 3: B1–B12, E1–E4 and V1 found that recorded HEAD and property validation can be bypassed by Git configuration or validation order, item reconciliation can select the wrong identity or lose metadata, descriptor code can execute before mapping validation, and the public command-to-engine/native process handoffs lack full both-mode coverage. Expanded the Code Map and tasks for exact gitlink verification, complete collision and path handling, normal Git transport configuration, physical and package-ID-aware item selection, early validation, refreshed routing and boundary tests. This avoids stale checkout builds, unmapped descriptor execution, wrong source/package selection and missed regressions. KEEP the passing 529 Module/107 Evidence baseline and the working features listed in Implementation Notes.
- 2026-10-09, review iteration 4: B1–B12, E1–E5 and V1–V2 exposed a real Platform source-mode blocker from archived duplicate csproj names, incomplete evaluated PackageId and metadata handling, staged-gitlink and no-Git regressions, transport/casing/stale-marker edges, and missing successful-restore/MTP/Windows checks. Expanded the Code Map and tasks to identify production package candidates, evaluate actual package IDs, resolve absent package roots through the central catalog, check Git's selected index gitlink, keep no-Git behavior, and verify normal builds in both modes. This avoids an HXW002 failure against EventStore evidence, wrong dependency origins, and false or missed mapping diagnostics. KEEP the 544 Module/107 Evidence baseline and fourth-pass features listed in Implementation Notes.
- 2026-10-09, review iteration 5: B2–B11, E1, E3–E5 and V1–V2 exposed residual nonproduction candidates, missing metadata enforcement, retained directory-form nested submodules, incomplete roots when `.gitmodules` vanishes, transport and persisted-map drift, no-Git symlink handling, ambiguous catalog fallback, changed prerequisite ordering, and untested launch/marker handoffs. Expanded the Code Map and tasks for these cases and a successful converted normal build. This avoids wrong package versions/configurations, silent reference loss, certificate failures, and an unverified mapping boundary. KEEP the 559 Module/107 Evidence baseline and fifth-pass features listed in Implementation Notes.
- 2026-10-09, sixth-pass diff audit: real Commons package projects live under `src/libraries`, while the first revised production filter accepted only one project directory below `src`. Expanded the Code Map and corrected the implementation and read-only checkout test before review, avoiding a false duplicate failure for Commons source mode. KEEP the sixth pass's other verified behavior and full gate results.

## Review Triage Log

Review iteration 1 (2026-10-09). Each row records one reviewer finding before grouping.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | high | bad_spec | Parties independently sets `HexalithCommonsHttpFromSource` and `HexalithCommonsServiceDefaultsFromSource` in `Directory.Build.props`; package mode clears the root but leaves these flags true, so its project references can select source. |
| B2 | high | bad_spec | EventStore project references use `HexalithTenantsBasePath`, whose probe chain is not overridden by the generated mapping; a separate earlier matching checkout can win. |
| B3 | medium | bad_spec | `ResolveAsync` skips nested checkout inspection in package mode, while the matrix requires a failure for any initialized nested reference. |
| B4 | medium | bad_spec | Nested inspection reads only the current direct checkout's `.gitmodules`; an initialized nested checkout retained from a previous gitlink can remain after nonrecursive update. |
| B5 | false | reject | MSBuild condition string equality is case insensitive; the `StartsWith` checks also use `OrdinalIgnoreCase`, so package ID casing does not bypass duplicate detection. |
| B6 | high | bad_spec | `ProjectReference` validation compares lexical `FullPath` prefixes; an in-root symlink to an external project passes the prefix check and builds unmapped source. |
| B7 | medium | bad_spec | `OrdinalIgnoreCase` accepts a differently cased path prefix on case sensitive filesystems, where it can name a separate checkout. |
| B8 | medium | bad_spec | Removing dots from identities maps `Hexalith.Foo.Bar` and `Hexalith.FooBar` to the same generated property names, allowing one root and flag to overwrite the other. |
| B9 | medium | bad_spec | XML escaping leaves an apostrophe in the parsed MSBuild condition literal; a checkout path such as `O'Brien` breaks the generated condition. |
| B10 | false | reject | The cited scripts are packaged-tool contract and evidence probes that stop at prerequisite `HXR003`; they do not run a mapped CI build, and existing evidence command strings are outside this story's allowed changes. |
| B11 | medium | bad_spec | No test observes a composed AppHost module project evaluating the mapping hash; factory and environment helper assertions cannot detect a broken `RunTopology` handoff. |
| E1 | false | reject | The frozen no-Git matrix row explicitly preserves existing fixture behavior; source enforcement for Git workspaces is conditional on `HasGit`, as intended for that fallback. |
| E2 | high | bad_spec | The same lexical `ProjectReference` containment check as B6 accepts a symlink pointing outside mapped roots. |
| E3 | medium | bad_spec | The same unescaped MSBuild condition literal as B9 fails for apostrophes in workspace paths. |
| E4 | medium | bad_spec | The same generated-property collision as B8 lets two distinct identities share one root and source flag. |
| E5 | medium | bad_spec | The materializer excludes projects by reserved host shim name as well as host path; a legitimate mapped module with that project name receives no mapping. |
| E6 | low | reject | The public CLI admits only two enum values; an undefined value requires an unsupported direct API call, and adding a guard would not address an everyday command failure. |
| V1 | medium | bad_spec | Command tests stop at missing manifest `HXM005`, so they never prove that `--mode package` reaches package-origin resolution. |
| V2 | medium | bad_spec | Plan tests call the factory directly; none checks that `CompositionEngine.StartAsync` writes the resolved mapping into `plan.json`. |
| V3 | medium | bad_spec | Native test tests inspect helper output, but no spawned test process proves that `NativeTestExecutor` supplies the mapping environment. |
| V4 | medium | bad_spec | No composition test inspects a module project's mapping imports, hash and configuration after `RunTopology.Compose`. |

Grouped bad-spec roots: consumer-specific mapping properties (B1, B2); nested preflight (B3, B4); physical and case-aware containment (B6, B7, E2); generated property/condition safety (B8, B9, E3, E4); host-scoping (E5); and boundary verification (B11, V1–V4). These require a re-derived implementation. KEEP: the single resolver handoff, recorded-gitlink direct initialization, persisted mapping and hash, late MSBuild import, versioned `HXW005` including assetless packages, fail-closed Git probe, hermetic fixture design, and passing 504/504 Module plus 107/107 Evidence baseline should survive re-derivation.

Review iteration 2 (2026-10-09). IDs are local to this iteration; each row again records an independent finding before grouping.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | high | bad_spec | Root membership checks the manifest's lexical path; a symlink under the root can load a manifest physically outside it and still pass `HXW001`. |
| B2 | high | bad_spec | `ModuleCommandExecutionService` loads the manifest before `ResolveAsync`; for an advanced direct checkout, recorded-gitlink update changes the file while descriptor and profile handling retain the old manifest object. |
| B3 | high | bad_spec | `NativeTestExecutor` validates the test project against the manifest repository but not the source mapping; a package-origin `references/*` project can be the primary test project without a `ProjectReference` for `HXW006` to inspect. |
| B4 | high | bad_spec | `ContainsProject` classifies `references/` lexically before resolving symlinks, so an in-root alias to a package-only reference can pass the active-root source entry. |
| B5 | high | bad_spec | The late import restores final properties but cannot undo `ProjectReference` items selected inside `Directory.Build.targets` before that import; package mode can select source then fail instead of selecting packages. |
| B6 | false | reject | The non-executable manifest path returns legacy prerequisite/profile results and triggers no build or launch; the mapping requirement applies to the executable paths that actually build or launch projects. |
| B7 | medium | bad_spec | The recursive nested scan treats any child `.git` marker as a submodule; an independent fixture repository inside a direct reference receives `HXW003` and an inapplicable deinit hint. |
| B8 | false | reject | A symlink to an undeclared checkout is not a Git submodule nested below the direct reference; skipping its traversal does not bypass the specified nested-submodule preflight. |
| B9 | medium | bad_spec | Production Git inspections and updates force `protocol.file.allow=always`, overriding Git's local-protocol restriction for repository supplied `.gitmodules`; local fixtures need this only in tests. |
| B10 | medium | bad_spec | The bounded Git process inherits no SSH agent or askpass variables, so a direct SSH reference relying on those credentials can fail `HXW004` while ordinary Git succeeds. |
| E1 | high | bad_spec | The same symlink alias to a package-only `references/*` path as B4 passes `ContainsProject` through the active root. |
| E2 | medium | bad_spec | Nested preflight is skipped when the direct reference directory exists without its own `.git` marker, even if an initialized nested checkout remains below it; source initialization can start first. |
| E3 | low | reject | A per-directory case-sensitive Windows tree is possible, but uncommon; the current OS-aware comparison covers ordinary Windows/Linux use, and filesystem-specific probing adds substantial complexity. |
| E4 | maybe-false | defer | A renamed outer checkout changes the directory-derived active identity, but the accepted contract does not identify a canonical name for a standalone root module; deciding whether this is a duplicate requires an identity source agreed with the owner. |
| V1 | medium | bad_spec | The valid-manifest CLI test exercises only `run`; `test --mode package` can still regress to source resolution undetected. |
| V2 | medium | bad_spec | The engine-written mapping and plan test exercises only package mode; source-only materialization errors would pass its helper tests. |
| V3 | medium | bad_spec | The composed AppHost resource test exercises only source mode; a package-only configuration regression would pass. |

Grouped bad-spec roots: manifest physical identity and reload order (B1, B2); primary project mapping containment (B3, B4, E1); early consumer item selection (B5); nested Git ownership/preflight (B7, E2); production Git transport behavior (B9, B10); and mode-paired boundary verification (V1–V3). E4 is unverified and lower-priority while re-derivation proceeds. KEEP: the re-derived package-asset validation task, physical symlink-safe `ProjectReference` check, consumer-specific flags, collision/condition safety, path-scoped host exclusion, and 517 Module/107 Evidence passing baseline.

Review iteration 3 (2026-10-09). IDs are local to this iteration; each finding is recorded before grouping.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | high | bad_spec | `git submodule update` can honor local `submodule.*.update=none`; the initializer checks a marker but never compares checkout HEAD with the root gitlink, so a stale source can be mapped. |
| B2 | medium | bad_spec | `WorkspaceRootResolver` initializes direct sources before generated-name collision detection; an invalid workspace can mutate checkouts before `HXW002`. |
| B3 | medium | patch | Direct-reference matching uses ordinal casing for the physically valid manifest path, so a valid differently cased spelling on a case-insensitive filesystem can miss the declaration. |
| B4 | medium | bad_spec | The scrubbed Git environment omits HTTP(S) proxy variables, so a normal proxied HTTPS submodule fetch can fail as `HXW004`. |
| B5 | medium | bad_spec | Generated `ProjectReference Include` paths escape MSBuild metacharacters except `*` and `?`; valid Unix directory names with these characters can be interpreted as item globs. |
| B6 | high | bad_spec | Package-origin conversion matches a `ProjectReference` filename alone; a mapped project in the active root with the same prefix can be replaced with a package. |
| B7 | high | bad_spec | Source-origin conversion assumes `PackageId` equals the csproj filename; a valid distinct package ID leaves the package reference selected and triggers a false duplicate or wrong origin. |
| B8 | medium | bad_spec | The conversion removes a package item and creates a project item without applicable metadata such as `Aliases` or `PrivateAssets`, which can alter compilation or dependency flow. |
| B9 | high | bad_spec | `HexalithValidateSourceMapping` depends on `ResolvePackageAssets` and runs after implicit restore, so an unmapped project can be evaluated or fail before `HXW006` is emitted. |
| B10 | high | bad_spec | `ExecutableDescriptorLoader` executes assemblies under the manifest repository before the mapping is applied to descriptor/UI assembly paths; a package-only reference can supply executable descriptor code. |
| B11 | medium | bad_spec | Public command tests stop before composition, while engine tests inject a mapping directly; removing the command-to-engine assignment would leave them green. |
| B12 | medium | patch | The spawned native-process environment assertion runs only in source mode; package-mode process configuration can regress while helper assertions pass. |
| E1 | medium | bad_spec | `isExecutable` is calculated before recorded-gitlink checkout and not recalculated from the reloaded manifest, so changed executable shape can steer the command through stale routing. |
| E2 | high | bad_spec | Collision detection compares generated names only between entries; identity `Hexalith.SourceMapping` can overwrite the fixed `HexalithSourceMappingRoot` property. |
| E3 | high | bad_spec | The source project identity assumption is the B7 case: a distinct `PackageId` is not used for item reconciliation. |
| E4 | medium | bad_spec | The scrubbed Git environment omits `XDG_CONFIG_HOME`, so Git can lose custom transport or credential configuration that ordinary Git uses. |
| V1 | medium | bad_spec | The public CLI has no test that reaches an engine-written plan with its resolved mapping; the command test stops at `HXD002` and the engine test supplies its own mapping. |

Grouped bad-spec roots: recorded checkout and early validation (B1, B2, E2); Git environment fidelity (B4, E4); item identity/origin/metadata and path escaping (B5–B8, E3); validation and descriptor execution order (B9, B10); refreshed routing (E1); and public command handoff verification (B11, V1). B3 and B12 would be focused patches, but the bad-spec loopback makes those fixes moot in this pass; their requirements are retained above. KEEP: persisted mapping/hash, physical path checks, nested ownership scan, package asset validation, consumer-specific flags and initial item reconciliation, Git protocol restraint and SSH handling, both-mode engine/AppHost tests, and the passing 529/107 baseline.

Review iteration 4 (2026-10-09). IDs are local to this iteration; each finding was assessed before grouping.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | high | bad_spec | Reconciliation indexes all csproj files under source references and errors on repeated inferred IDs; the tracked EventStore evidence tree contains many repeated `Probe.csproj`, `Host.csproj` and `Consumer.csproj` files, so real source mode can fail `HXW002` before a build. |
| B2 | medium | bad_spec | `ReadPackageId` reads only literal XML and falls back to the filename; imported or computed package IDs can miss source selection. |
| B3 | medium | bad_spec | The XML reader ignores a parent `PropertyGroup` condition, so it may choose a PackageId for the wrong configuration or framework. |
| B4 | medium | bad_spec | Project-only metadata such as `SetConfiguration` and `SetPlatform` is copied into a PackageReference without equivalent behavior; the current unsupported list omits it. |
| B5 | medium | bad_spec | Package asset-selection metadata such as `ExcludeAssets` can be copied into a ProjectReference without preserving dependency behavior; the current unsupported list omits it. |
| B6 | medium | bad_spec | `ContainsProject` excludes `Root/references` even when `HasGit=false`; this changes legacy no-Git fixtures whose active module contains local reference projects. |
| B7 | high | bad_spec | The initializer compares checkout HEAD with the root HEAD gitlink while `git submodule update` uses the staged index gitlink; a valid staged update is falsely reported stale. |
| B8 | medium | bad_spec | OS-based ordinal casing misses valid direct-reference path spellings on a case-insensitive macOS volume, despite the spec requiring containing-filesystem rules. |
| B9 | medium | bad_spec | The scrubbed Git process drops `GIT_SSL_CAINFO`; HTTPS submodules using a custom CA can fail `HXW004` although ordinary Git succeeds. |
| B10 | medium | bad_spec | The early validation target names restore entry points but not direct `ResolveProjectReferences`; that explicit target can evaluate an unmapped project before `HXW006`. |
| B11 | medium | bad_spec | A stale nested `.git` file whose Git directory was deleted is no longer recognized by the physical `modules` containment check, allowing initialization to proceed. |
| B12 | medium | bad_spec | Public command, engine-plan and native-process boundary tests skip Windows because their process doubles use POSIX shell; those mode handoffs remain unverified on a supported OS. |
| E1 | high | bad_spec | The same unfiltered duplicate-project scan as B1 fails on unrelated source checkout projects. |
| E2 | medium | bad_spec | An uninitialized package-origin root prevents reading a project file; filename fallback can reject a valid centrally managed package whose ID differs from the filename. |
| E3 | high | bad_spec | `requiresMapping` is based on the pre-checkout manifest; when that manifest is nonexecutable but the recorded version is executable, checkout never occurs and stale routing wins. |
| E4 | medium | bad_spec | Imported, conditional and expression-defined `PackageId` values are not evaluated; this is the B2/B3 root cause. |
| E5 | high | bad_spec | Reserved Commons property checks use case-sensitive pattern matching, so an identity with different casing can set a Commons source flag without the Commons identity being mapped. |
| E6 | false | reject | The frozen contract names direct `references/*` paths literally; a `References/*` declaration is outside that convention, even on a case-insensitive filesystem. |
| V1 | medium | patch | Mapped spawned native-process tests exercise VSTest only; MTP configuration arguments can regress in package mode while existing checks pass. |
| V2 | medium | bad_spec | Reconciliation tests call its target directly and rejection tests build, but no successful ordinary build/restore checks whether source mode uses the project and package mode uses the catalog package. |
| V3 | false | reject | The frozen duplicate rule applies to package IDs equal to `<module identity>` or prefixed by it; `Contoso.Contracts` is outside `Hexalith.Dep`'s specified duplicate domain. |

Grouped bad-spec roots: candidate and effective package identity selection (B1–B3, E1–E2, E4); conversion metadata (B4–B5); fallback and Git state handling (B6–B9, B11, E3); early target coverage (B10); property casing (E5); and normal-build and Windows verification (B12, V2). V1 is a focused test patch but is retained in the revised spec because the bad-spec loopback supersedes patch work. KEEP: the fourth-pass public plan capture, both-mode VSTest/native and AppHost handoffs, descriptor guard, early restore check, exact checkout check, physical-origin conversion, metadata preservation, and passing 544/107 baseline.

Review iteration 5 (2026-10-09). IDs are local to this iteration; each reviewer finding was assessed before grouping.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | false | reject | Skipping the active root as a conversion candidate is intentional: the frozen duplicate rule says a source identity plus any matching package is an error, so `HXW005` is correct. |
| B2 | medium | bad_spec | The scan excludes archived paths outside `src`, but a tool, sample or evidence project under `src` with a matching PackageId remains a candidate and can select the wrong source or report `HXW002`. |
| B3 | medium | bad_spec | A ProjectReference's `Version` or `VersionOverride` metadata survives conversion to PackageReference and can bypass or conflict with the central catalog. |
| B4 | medium | bad_spec | A PackageReference's otherwise inert `SetConfiguration` or `SetPlatform` metadata survives conversion to ProjectReference and can change the source build. |
| B5 | medium | bad_spec | Retained ProjectReference items bypass conversion checks and can retain configuration metadata that overrides Debug or Release. |
| B6 | medium | bad_spec | An old-form nested submodule with a retained `.git` directory is recognized as Git but lacks the gitfile fallback when its former gitlink is absent, so preflight can treat it as independent. Ownership evidence must distinguish it from a true independent repository. |
| B7 | high | bad_spec | A missing working-tree `.gitmodules` returns zero references even when Git tracks direct gitlinks, silently omitting required identities. |
| B8 | medium | bad_spec | Git's scrubbed environment carries CA settings but omits `GIT_SSL_CERT` and `GIT_SSL_KEY`, so mutual TLS submodule initialization can fail. |
| B9 | medium | bad_spec | MSBuild tasks reread mutable `mapping.json` without comparing its content to the generated hash, allowing dependency selection to drift from the plan. |
| B10 | medium | bad_spec | A normal package-mode build beginning with a ProjectReference is untested; only the isolated reconciliation target exercises project-to-package conversion. |
| B11 | medium | bad_spec | Public `test --mode` stops at plan capture in tests, while native process tests inject a mapping; the command-to-native-process hash handoff has no covering test. |
| E1 | medium | bad_spec | The no-Git resolver returns before physical manifest containment, so a manifest symlink pointing outside can be accepted as the active module's manifest. |
| E2 | false | reject | Carried from iteration 4 E6: the frozen contract declares literal direct `references/*` paths; a differently cased `References/*` declaration is outside that convention. |
| E3 | high | bad_spec | When source is absent, separate ProjectReference paths can each select the sole identity-prefixed catalog package, collapsing distinct references without unambiguous path identity. |
| E4 | medium | bad_spec | The same omitted `GIT_SSL_CERT` and `GIT_SSL_KEY` environment as B8 can break client-certificate transport. |
| E5 | medium | bad_spec | Executable routing defers the nonexecutable `HXR003` gate until after Aspire probing; a missing Aspire executable can now produce `HXR015` instead of the earlier prerequisite result. |
| V1 | medium | bad_spec | Preverified gap: the fake AppHost captures files but not its spawned environment, so removing `StartAppHost` mapping variables would leave existing assertions green. |
| V2 | medium | bad_spec | Preverified gap: no mapped descriptor test returns a UI marker assembly under a package origin; removing that guard would leave tests green. |

Grouped bad-spec roots: source candidate and absent package identity selection (B2, E3); conversion and retained-item metadata (B3–B5); Git tree, ownership and transport (B6–B8, E4); mapping immutability and no-Git containment (B9, E1); prerequisite ordering (E5); and end-to-end verification gaps (B10–B11, V1–V2). B1 and E2 are rejected. KEEP the 559 Module/107 Evidence baseline and all fifth-pass features listed in Implementation Notes.

Review iteration 6 (2026-10-09). Each finding was assessed before grouping; the five-loop cap stops another re-derivation.

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | medium | patch | ProjectReference `IncludeAssets` or `ExcludeAssets` metadata is copied into a PackageReference without a conversion check, where it can change assets. Add these fields to the existing unsupported list. |
| B2 | medium | patch | PackageReference `ReferenceOutputAssembly` or `Private` metadata is copied into a ProjectReference, where it can change compilation or copy behavior. Add these fields to the existing unsupported list. |
| B3 | false | reject | The frozen scope excludes package-mode catalog checks for Story 1.4; central package management already reports a missing PackageVersion during restore. A new HXW catalog diagnostic is not required for an existing project path. |
| B4 | false | reject | The accepted catalog fallback deliberately maps one absent path to the sole identity-prefixed package when PackageId differs from the filename; no second candidate or expected distinct package was shown. |
| B5 | false | reject | A candidate must be packable, have the exact requested effective PackageId, and match the source identity prefix; duplicate candidates fail `HXW002`. Folder location alone does not show a wrong package selection. |
| B6 | medium | bad_spec | Reconciliation evaluates every accepted production csproj before comparing its PackageId with requested packages; one malformed unrelated project can fail an otherwise valid consumer build. Avoiding that without losing distinct imported PackageIds needs a specified selection strategy. |
| B7 | medium | patch | `ValidateExistingFile` can return null with native path diagnostics, but the command ignores that list and may start composition before the executor reports the invalid primary test project. Return the existing diagnostic before launch. |
| B8 | medium | patch | `ReadDirectReferencesAsync` checks `File.Exists(.gitmodules)` but does not physically contain the file; an external symlink can supply root declarations. Apply the existing physical path resolver before reading it. |
| B9 | medium | bad_spec | Package mode accepts a `.gitmodules` direct declaration without verifying its stage-zero gitlink, allowing a stale or untracked submodule declaration to become a package identity. |
| B10 | medium | bad_spec | Git emits `submodule.name with spaces.path references/Hexalith.Dep` for a legal subsection name; splitting at the first space misparses the key and silently omits that direct reference. This was reproduced with `git config --get-regexp`. |
| B11 | high | patch | On Windows, `references/Foo\\Bar` passes the two-part slash check and is treated as a deeper path. Reject a backslash anywhere in a declared direct reference path. |
| V1 | medium | patch | Preverified gap: explicit-mode plan tests do not prove the public default selects source and Debug; add an omitted-mode plan case. |
| V2 | medium | patch | Preverified gap: resolver and injected-engine no-Git tests do not prove the public command hands its resolved no-Git mapping to the plan; add a public no-Git capture case. |
| V3 | medium | patch | Preverified gap: the public native-process test covers VSTest only, while direct MTP tests bypass command routing; run that public case with `full-mtp` in both modes. |
| E1 | medium | bad_spec | The same package-mode declaration without a stage-zero gitlink as B9 can enter the mapping. |
| E2 | high | patch | The same mixed-separator Windows path as B11 can pass the direct-path check and receive the wrong identity. |
| E3 | medium | patch | `GIT_SSH_VARIANT` is absent from Git's transport allowlist, so a configured custom SSH helper may receive incompatible arguments during direct checkout. |

Grouped specification defects: eager evaluation of unrelated source projects (B6), unverified package-mode gitlinks (B9, E1), and space-containing Git subsection parsing (B10). Patch findings are recorded but not applied because the specification defects trigger a loopback and `review_loop_iteration` has exceeded five. The staged sixth-pass Builds implementation and its passing gates remain available for human review.

## Verification

**Commands** (from Builds):
- `dotnet build test/Hexalith.Builds.Module.Tests -c Debug -m:1` and the Evidence tests: zero warnings and errors. Then run both xUnit assemblies directly: all pass, none skipped.
- `pwsh Tools/test-g4-tool-package-contracts.ps1 -Version 0.0.0-ci.1 -SkipSourceValidation -RetainPackageDirectory`: passes; grants no acceptance. The script requires `-Version`.

**Pre-review results (2026-10-09):** Module 504/504; Evidence 107/107; focused workspace 25/25; Module, Evidence and AppHost Debug builds each had zero warnings and errors; packed G-4 contract qualification passed; `git diff --check` passed. Re-run after re-derivation.

**Re-derived results (2026-10-09):** Module 517/517 and Evidence 107/107, none skipped; Module and Evidence Debug builds had zero warnings and errors; packed G-4 contract qualification passed in Release with zero warnings and errors; `git diff --check` passed. The new tests exercise the public CLI, an engine-written plan, a spawned native test process and composed AppHost project settings. No live Aspire/container topology was started.

**Status after review iteration 2:** The 517/107 results describe the reverted second pass. Re-run all gates after the next re-derivation.

**Third implementation pass (2026-10-09):** Module Debug build had zero warnings/errors; all 529 Module tests and 107 Evidence tests passed, with none skipped on Linux. The Release packed-tool contract passed with zero build warnings/errors, and `git diff --check` passed. The Module suite ran the matrix coverage: `WorkspaceRootResolverTests` checks direct and nested initialization, missing source, active root, and no-Git fallback; `WorkspaceMsBuildTests` checks source/package selection with optional files, duplicate packages, and unmapped projects; `WorkspaceCommandModeTests`, `WorkspaceRunPlanTests`, and `WorkspaceAppHostTests` check public run/test mode handling and mapping handoffs in both modes. Live Aspire services were not started; composed resource and spawned native process boundaries were tested.

**Fourth implementation pass (2026-10-09):** Module Debug and Evidence Debug builds had zero warnings/errors; all 544 Module and 107 Evidence tests passed with none skipped on Linux. The Release packed-tool contract passed and `git diff --check` was clean. All eight matrix rows are exercised by the passing Module suite: `WorkspaceRootResolverTests` covers source checkout, nested and missing sources, and no-Git fallback; `WorkspaceMsBuildTests` covers duplicates, unmapped references, and both-mode consumer selection; `WorkspaceCommandModeTests` reaches an engine-written plan from public run/test in both modes; `WorkspaceRunPlanTests` checks both-mode native process handoff; `WorkspaceAppHostTests` checks both-mode composed resources. Live Aspire services were not started. Reconciliation reads a literal unconditional `PackageId` or the project filename; a PackageId computed by an expression or imported props is not evaluated and may fail to select source.

**Fifth implementation pass (2026-10-09):** Module Debug build and test: 559/559 passed; Evidence Debug build and test: 107/107 passed, none skipped. AppHost Debug build and the Release packaged-tool G-4 contract passed with zero warnings and errors. `git diff --check` passed in Builds. The eight matrix rows are covered by passing `WorkspaceRootResolverTests` (direct, nested, missing and no-Git), `WorkspaceMsBuildTests` (duplicates, unmapped projects, modes and optional files), `WorkspaceCommandModeTests` (public run/test resolution and plan), `WorkspaceRunPlanTests` (both native platforms and modes), and `WorkspaceAppHostTests` (composed module projects). The EventStore production candidate test ran against the root-declared checkout. Portable process doubles permit Windows execution, but this run was on Linux; live Aspire services were not started.

**Status after review iteration 5:** The fifth-pass implementation was reverted for re-derivation. Its verification results above are historical; all gates must run against the next implementation.

**Sixth implementation pass (2026-10-09):** Module Debug build and tests: 578/578 passed; Evidence Debug build and tests: 107/107 passed, none skipped. AppHost Debug build and the Release packaged-tool G-4 contract passed with zero warnings and errors. `git diff --cached --check` passed in Builds. All eight matrix rows are covered by the passing `WorkspaceRootResolverTests` (direct, nested, missing and no-Git), `WorkspaceMsBuildTests` (duplicate identity, unmapped project, both modes and optional files), `WorkspaceCommandModeTests` (public run/test mapping and native handoff), `WorkspaceRunPlanTests` (both native platforms and modes), and `WorkspaceAppHostTests` (both composed modes). Read-only candidate tests ran against the root-declared EventStore and Commons checkouts. Live Aspire services and Windows execution were not run; local process and resource doubles were used on Linux.
