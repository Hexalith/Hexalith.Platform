---
title: '1.13 Publish the domain-module enrollment kit'
type: 'feature'
epic: 1
story: 13
created: '2026-10-20'
status: 'in-review'
baseline_commit: '3d51fed1f3d99acf36ef690f150136593298bd95'
builds_baseline_commit: '5d03f1db9c38db34ecac7fe6048937478ffb82cc'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Module owners lack an enrollment kit; the accepted tool validates declarations without an adoption report.

**Approach:** Publish a guide and declaration, hosting-inventory and SM-2 templates. Extend Builds with opt-in `validate --adoption`, release it through the existing gated pipeline, then pin and verify it in Platform.

## Boundaries & Constraints

**Always:** Preserve concurrent changes and ordinary `validate --manifest` behavior. Adoption validation is read-only in the current module Git workspace, emitting human/JSON pass/fail rows with requirement, file and reason. Require direct Platform, Builds, McpCli and distinct EventStore/Tenants/Memories references plus declared server dependencies, excluding self. Check recorded gitlinks; reject initialized nested references without initialization.

Match the tool pin to the direct Platform checkout. Audit tracked module-owned files for known sibling/nested/package-fallback helpers, file-based dependency/mode selection, literal Dapr component names and programmatic subscriptions. Permit legitimate `Exists` guards and tool-mapped project/package pairs. Label scans as bounded static checks; require human review for unsupported constructs. Exclude only exact frozen hosting-project directories, never root build files.

**Never:** Adopt or retire another module here, fabricate SM-2 evidence, claim static checks prove runtime behavior, add another manifest major, upgrade runtime dependencies, or bypass release gates. Completion requires Builds owner review, upstream publication, the actual published tool pin/release-tag gitlink, and Platform verification.

## I/O & Edge-Case Matrix

| Input/state | Expected behavior |
| --- | --- |
| Complete fixture workspace, `validate --manifest module/hexalith.module-manifest.v2.json --adoption` | All static requirements reported PASS; explicitly state SM-2 remains separately unverified |
| Missing/malformed input, wrong pin, missing direct reference, initialized nested checkout, or forbidden source pattern | FAIL row naming file and reason; nonzero exit; report independent requirements too |
| Harmless `Exists`, generated mapping selectors, external sibling present | No false failure or sibling selection |
| Missing hosting inventory or arbitrary exclusion | FAIL; inventory cannot bypass root/dependency checks |
| Existing command without `--adoption` | Existing output and exits preserved |

</frozen-after-approval>

## Code Map

- Builds paths below are relative to `references/Hexalith.Builds/`.
- `src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`: `CreateValidateCommand` / `ExecuteValidationAsync`; no extension hook exists in accepted 4.34.0.
- `src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs`: reuse schema/Dapr validation.
- `src/libraries/Hexalith.Builds.Tooling/Workspace/`: reuse `ReadDirectReferencesAsync`, `ValidateRecordedGitlinksAsync`, `CheckNestedReferencesAsync`, `GitWorkspaceProcess`; avoid mutating `ResolveAsync`.
- `src/libraries/Hexalith.Builds.Tooling/Diagnostics/`: extend `ToolCommandResult` and `ToolDiagnosticFormatter` with optional adoption rows; omit absent JSON fields.
- `test/Hexalith.Builds.Module.Tests/`: reuse manifest/workspace Git fixtures.
- Platform `README.md`, `.config/dotnet-tools.json`: accepted 4.34.0; Story 1.11 evidence documents release/pin verification.

## Tasks & Acceptance

**Execution:**
- [x] `docs/domain-module-enrollment.md`, `README.md` — guide direct-only initialization, template customization, exact accepted-tool restore, mapping-only source selection, configured Dapr names/declarative subscriptions, required human source audit, and validation/run/debug/down recipes.
- [x] `templates/domain-module-enrollment/hexalith.module-manifest.v2.json` — valid domain example: all five groups, minimum dependencies, readiness, logical Dapr roles/keys, declarative dead-letter subscription. Explain owner customization.
- [x] `templates/domain-module-enrollment/hosting-inventory.json` — per-project path/kind/owner/consumers, frozen state and replacement/retirement plan. Require producer adoption, owner-reviewed source/package/deployed parity and applicable EventStore AD-22 exact-subject removal authority before retirement. Defaults remain pending.
- [x] `templates/domain-module-enrollment/sm-2-checklist.md` — human source audit and pending demonstration: module/revision, Platform/tool identity, environment/run/mapping, command, breakpoint/hit, edit/runtime effect, direct-only/nested check, result, diagnostics, verifier/date. Feed Story 3.11.
- [x] Builds `src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformAdoptionValidator.cs` and sibling result types — implement the matrix and fixed requirements: references/gitlinks, nested check, declaration, pin, dependency selectors, Dapr/subscriptions, hosting-record completeness. Keep one C# type per file.
- [x] Builds CLI/Diagnostics files above — wire `--adoption`, all requirement rows and failing exit behavior; require manifests inside the active workspace.
- [x] Builds `test/Hexalith.Builds.Module.Tests/PlatformAdoptionValidationTests.cs` and `PlatformManifestCommandTests.cs` — positive workspace and one failure per requirement, malformed/missing files, valid guards, frozen-directory confinement, read-only behavior, human/JSON rows and legacy compatibility.
- [x] `tests/Hexalith.Platform.Composition.Tests/DomainEnrollmentKitTests.cs` — validate starter with real technical declarations, inventory defaults and candidate-tool commands in a disposable workspace.
- [ ] `.config/dotnet-tools.json`, `hexalith-platform.json`, Builds gitlink, `README.md`, `_bmad-output/implementation-artifacts/evidence/1-13/README.md` — after review/release, pin actual published version, raise the floor to it, retain upper bound `5.0.0-0`, and record canonical revisions, owner review, release and fresh acceptance evidence.

**Acceptance Criteria:**
- Given the kit, when followed, then all six Story 1.13 enrollment topics have commands and identified human verification.
- Given a workspace, when adoption validation runs, then every static requirement reports pass/fail, file and reason without modifying the workspace.
- Given completed adoption, when Platform verifies it, then the checklist records active-checkout breakpoint/edit and direct-only evidence suitable for Story 3.11.
- Given hosting projects, when inventoried, then each is frozen with an explicit gated retirement plan; inventory completeness grants no retirement authority.

## Implementation Notes

Planning assessment: no unresolved intent choices. Footprint: kit, Builds validator/CLI/results/tests, Platform tests/pins/evidence. NuGet publication is irreversible and requires the existing release approval gate; no publication during planning.

Implementation baseline: Builds `5d03f1db9c38db34ecac7fe6048937478ffb82cc`. A concurrent `/pushall` commit captured in-progress local files at `1c560aa3ad0407717ef0e2ce5fb5e48f0ffd32a1`; review from the original Builds baseline through the final worktree, not HEAD-only. This does not establish owner approval, publication or Platform release acceptance.

Implementation handoff: execute sequentially. Step 3 permits local work only: no pushes or remote operations. Prepare and verify the local release candidate; leave publication/pinning for the orchestrator after review. Preserve concurrent work and stage only story-owned paths. Read owning repository guidance before edits. Work in the owning repository for Git operations.

## Spec Change Log

## Review Triage Log

### Pass 1 — 2026-10-20

Every finding was classified before grouping. All surviving entries are local corrections to demonstrated inputs, with no new public surface; route: patch.

| ID | Verdict | Evidence and route |
| --- | --- | --- |
| BH1 | medium | `tracked` removes every references/ path, although ordinary tracked helpers may live there. Git ls-files already represents real submodules as gitlinks. Patch the overbroad filter and test a tracked helper. |
| BH2 | medium | Dependency names become title-cased repository paths and membership uses ordinal case; actual ChatBot naming is rejected. Match existing direct repository identities without inventing casing. |
| BH3 | medium | Scan skips frozen directories before selecting audit type, so an inventoried host's mode assignment bypasses dependency checks. Preserve dependency inspection inside frozen hosts; limit the legacy exclusion to Dapr hosting patterns. |
| BH4 | medium | Inventory checks index membership but not physical project existence, then excludes the directory. Add existing-file confinement before exclusion. |
| BH5 | medium | The unconditional selector check covers only exact-case mode names and *Root, missing generated *FromSource and case-insensitive MSBuild names. Normalize property matching and cover assignments. |
| BH6 | medium | Imports are absent from selector kinds, allowing a dependency import guarded by Exists while unrelated optional imports correctly pass. Include dependency-selecting imports without rejecting optional imports. |
| BH7 | medium | The discovery regex contains bare Path followed by Hexalith/Root; ordinary Path.Combine calls fail. Require a real filesystem existence/discovery probe. |
| BH8 | medium | Raw C# text is scanned as executable code, so a removed invocation in a comment fails enrollment. Ignore comments/string-only invocation text with focused regressions. |
| BH9 | medium | Configuration scanning recognizes generic names, not the declaration's Sample:Dapr:EventsComponent key; the shipped example can retain a physical default. Check declared keys in supported configuration formats. |
| BH10 | low | The Topic expression omits global:: and normal qualified syntax. A directly corrected attribute matcher covers this demonstrated standard syntax without a new policy. |
| EH1 | medium | The references check excludes self only from the required set; it never rejects an existing self gitlink. Add the demonstrated self-reference refusal before source mapping. |
| EH2 | medium | Same missing physical-existence check as BH4; verified the inventory path is used to exclude scanning without opening the project. Patch with BH4. |
| EH3 | medium | Pin checks ignore commands, so an empty command array passes though documented dotnet tool run cannot resolve hexalith-module. Require the declared command and test malformed/missing arrays. |
| EH4 | medium | Same demonstrated comment false failure as BH8. Patch with BH8; the review's snippet is a suggested correction, not an existing guard. |
| VG1 | medium | Pre-verified regression gap: no test exercises the existing JSON/YAML/YML configuration-default branch. Add format-specific failures and legitimate inputs alongside BH9. |
| VG2 | medium | Pre-verified regression gap: matching dirty working-tree pins never diverge from committed Platform in current tests. Add a regression preserving committed HEAD while changing both local versions. |
| VG3 | medium | The compiled reproduction rejects Path.Combine(environment.ContentRootPath, ...); the bare-Path alternative directly explains it. Patch with BH7. |

Grouping: BH1; BH2; BH3; BH4/EH2; BH5; BH6; BH7/VG3; BH8/EH4; BH9/VG1; BH10; EH1; EH3; VG2. All route to patch. Publication and broad-gate failures remain unresolved acceptance work, not fabricated completion evidence.

All 17 findings are fixed and their targeted regressions passed (65 cases plus 3 final generic-configuration format cases). No finding was deferred. The earlier broad failures were superseded by a restored normal Platform build and stable full Builds verification; publication remains pending owner review.

## Verification

- Build and execute focused Builds test classes directly with xUnit v3; run the owning test project checks required for release.
- Run `DomainEnrollmentKitTests` and both output modes of released pinned `validate --adoption` on disposable passing/failing workspaces; confirm nonzero failure exits and unchanged Git state.
- Verify published version, release tag/gitlink equality and supported range; retain exact commands/results in story evidence.

Local implementation verification: Builds focused Release build (zero warnings/errors), 97 focused adoption/manifest-command tests passed; Platform disposable focused lane ran the actual `DomainEnrollmentKitTests` against the same validator (4 passed); four direct candidate CLI human/JSON pass/fail commands preserved every workspace file hash and Git state. Normal Platform Release build and full Builds test suite remain blocked/failing amid existing/concurrent non-story dependency and legacy-fixture changes. See `evidence/1-13/README.md` and retained raw outputs. Owner review, actual upstream release, release-tag gitlink/pin/floor update, released-tool acceptance and human SM-2 remain pending.

Coordinator matrix audit: read the complete story-scoped combined Platform/Builds diff at `/tmp/story-1-13-review-zwttdunt.diff`. CompleteWorkspacePassesAndRemainsUnchangedAsync covers happy/read-only/sibling/legitimate guards; EveryRequirementFailureIsReportedAsync covers missing/malformed inputs, pins, direct references, gitlinks and forbidden patterns; InitializedNestedReferenceFailsReadOnlyAsync covers nested initialization; FrozenInventoryIsConfinedToExactHostingDirectoryAsync and arbitrary-exclusion cover inventory confinement; ReportsRenderRowsAndLeaveLegacyJsonUnchangedAsync plus PlatformManifestCommandTests cover legacy compatibility and output modes. All ran in the 97-pass output. Platform tests and four direct commands cover shipped templates and candidate behavior. Local implementation tasks are verified; the explicitly post-review publication/acceptance task remains open.

Final coordinator verification: stable full Builds suite ran 1,185 cases, 1,182 passed, zero failed, three enclosing-Platform-dependent skips. Three final generic configuration regressions also passed independently. Freshly restored normal Platform composition test project builds with zero warnings/errors and passes all four DomainEnrollmentKitTests. Four commands through a locally packaged tool and actual dotnet tool run pass in human/JSON success/failure modes, preserving all file hashes and Git state. All review findings are fixed, with none deferred. Builds local commits: `b6642fecff8617e1765d835e71ebde316e3f71a5` and `fff4a212180d8cd1c5c0759655a9bc4fb6963c45`. Complete product diff, source hashes and raw outputs are retained in evidence/1-13. Spec remains in-review because the approved completion constraint requires Builds owner review, upstream publication and released-pin acceptance. Never mark those complete from local candidate evidence.
