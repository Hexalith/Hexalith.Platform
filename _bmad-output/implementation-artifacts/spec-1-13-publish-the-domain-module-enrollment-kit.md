---
title: '1.13 Publish the domain-module enrollment kit'
type: 'feature'
epic: 1
story: 13
created: '2026-10-20'
status: 'in-progress'
baseline_commit: '3d51fed1f3d99acf36ef690f150136593298bd95'
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
- [ ] `docs/domain-module-enrollment.md`, `README.md` — guide direct-only initialization, template customization, exact accepted-tool restore, mapping-only source selection, configured Dapr names/declarative subscriptions, required human source audit, and validation/run/debug/down recipes.
- [ ] `templates/domain-module-enrollment/hexalith.module-manifest.v2.json` — valid domain example: all five groups, minimum dependencies, readiness, logical Dapr roles/keys, declarative dead-letter subscription. Explain owner customization.
- [ ] `templates/domain-module-enrollment/hosting-inventory.json` — per-project path/kind/owner/consumers, frozen state and replacement/retirement plan. Require producer adoption, owner-reviewed source/package/deployed parity and applicable EventStore AD-22 exact-subject removal authority before retirement. Defaults remain pending.
- [ ] `templates/domain-module-enrollment/sm-2-checklist.md` — human source audit and pending demonstration: module/revision, Platform/tool identity, environment/run/mapping, command, breakpoint/hit, edit/runtime effect, direct-only/nested check, result, diagnostics, verifier/date. Feed Story 3.11.
- [ ] Builds `src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformAdoptionValidator.cs` and sibling result types — implement the matrix and fixed requirements: references/gitlinks, nested check, declaration, pin, dependency selectors, Dapr/subscriptions, hosting-record completeness. Keep one C# type per file.
- [ ] Builds CLI/Diagnostics files above — wire `--adoption`, all requirement rows and failing exit behavior; require manifests inside the active workspace.
- [ ] Builds `test/Hexalith.Builds.Module.Tests/PlatformAdoptionValidationTests.cs` and `PlatformManifestCommandTests.cs` — positive workspace and one failure per requirement, malformed/missing files, valid guards, frozen-directory confinement, read-only behavior, human/JSON rows and legacy compatibility.
- [ ] `tests/Hexalith.Platform.Composition.Tests/DomainEnrollmentKitTests.cs` — validate starter with real technical declarations, inventory defaults and candidate-tool commands in a disposable workspace.
- [ ] `.config/dotnet-tools.json`, `hexalith-platform.json`, Builds gitlink, `README.md`, `_bmad-output/implementation-artifacts/evidence/1-13/README.md` — after review/release, pin actual published version, raise the floor to it, retain upper bound `5.0.0-0`, and record canonical revisions, owner review, release and fresh acceptance evidence.

**Acceptance Criteria:**
- Given the kit, when followed, then all six Story 1.13 enrollment topics have commands and identified human verification.
- Given a workspace, when adoption validation runs, then every static requirement reports pass/fail, file and reason without modifying the workspace.
- Given completed adoption, when Platform verifies it, then the checklist records active-checkout breakpoint/edit and direct-only evidence suitable for Story 3.11.
- Given hosting projects, when inventoried, then each is frozen with an explicit gated retirement plan; inventory completeness grants no retirement authority.

## Implementation Notes

Planning assessment: no unresolved intent choices. Footprint: kit, Builds validator/CLI/results/tests, Platform tests/pins/evidence. NuGet publication is irreversible and requires the existing release approval gate; no publication during planning.

Implementation handoff: execute sequentially. Step 3 permits local work only: no pushes or remote operations. Prepare and verify the local release candidate; leave publication/pinning for the orchestrator after review. Preserve concurrent work and stage only story-owned paths. Read owning repository guidance before edits. Work in the owning repository for Git operations.

## Spec Change Log

## Review Triage Log

## Verification

- Build and execute focused Builds test classes directly with xUnit v3; run the owning test project checks required for release.
- Run `DomainEnrollmentKitTests` and both output modes of released pinned `validate --adoption` on disposable passing/failing workspaces; confirm nonzero failure exits and unchanged Git state.
- Verify published version, release tag/gitlink equality and supported range; retain exact commands/results in story evidence.
