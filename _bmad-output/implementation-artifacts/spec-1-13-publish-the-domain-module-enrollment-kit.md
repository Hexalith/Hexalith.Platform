---
title: '1.13 Publish the domain-module enrollment kit'
type: 'feature'
epic: 1
story: 13
created: '2026-10-20'
status: 'in-progress'
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
- [x] `.config/dotnet-tools.json`, `hexalith-platform.json`, Builds gitlink, `README.md`, `_bmad-output/implementation-artifacts/evidence/1-13/README.md` — after review/release, pin actual published version, raise the floor to it, retain upper bound `5.0.0-0`, and record canonical revisions, owner review, release and fresh acceptance evidence.

**Acceptance Criteria:**
- Given the kit, when followed, then all six Story 1.13 enrollment topics have commands and identified human verification.
- Given a workspace, when adoption validation runs, then every static requirement reports pass/fail, file and reason without modifying the workspace.
- Given completed adoption, when Platform verifies it, then the checklist records active-checkout breakpoint/edit and direct-only evidence suitable for Story 3.11.
- Given hosting projects, when inventoried, then each is frozen with an explicit gated retirement plan; inventory completeness grants no retirement authority.

### Review Findings

Code review 2026-10-20 of released `v4.35.0` (Builds `5d03f1d..fff4a21`, story paths) plus Platform story paths `3d51fed..HEAD`. Layers: Blind Hunter, Edge Case Hunter, Verification Gap, Acceptance Auditor; none failed. Headline: released `validate --adoption` run read-only on the real `references/Hexalith.Parties` checkout fails `workspace` ("Cannot enumerate tracked module-owned files"), so the kit cannot pass on any target module today.

- [ ] [Review][Patch] Scan JSON/YAML only as .NET configuration files (decision 1a, 2026-10-20) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformAdoptionSourceAudit.cs:49] — Oversized evidence JSON (Parties/Tenants 10, Projects 17) and Folders' 1.1 MB OpenAPI YAML fail both audits with no passing route. Route JSON/YAML only to the Dapr configuration-default check and only for `appsettings*.json|yaml|yml`; drop them from `dependency-selectors`; apply the 1 MiB bound after filtering. Regressions: oversized evidence JSON passes; literal default in `appsettings.Development.json` fails. Unusual config files fall to the required human review. [blind-hunter+verification-gap+edge-case-hunter; high]
- [ ] [Review][Patch] Permit only the exact `HexalithBuildsRoot` bootstrap default (decision 2a, 2026-10-20) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformAdoptionSourceAudit.cs:121] — The tool injects its mapping via `CustomBeforeDirectoryBuildProps`, so a `Condition="'$(HexalithBuildsRoot)' == ''"` default to `$(MSBuildThisFileDirectory)references/Hexalith.Builds` (either separator, whitespace-insensitive) is inert under the tool and only bootstraps non-tool builds. Skip exactly that element; other roots, `*FromSource`, mode properties, `Exists` conditions and siblings still fail. Add pass/fail regressions and one guide §4 sentence. Unblocks Parties `Directory.Build.props`/`Directory.Packages.props`. [acceptance-auditor; medium]
- [ ] [Review][Patch] Reject pending owner and consumers in hosting records (decision 3a′, 2026-10-20) [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformHostingInventory.cs:39] — Fail `owner` and `consumers` values starting with "pending" (case-insensitive); accept `["none"]` as explicit no-consumers and document it in the guide/template; plans and gates may stay pending. Customize owner/consumers in the Platform kit-test fixture; add one failing regression. Template stays pending, so an unedited copy fails until the owner fills it in. [blind-hunter+acceptance-auditor; medium]
- [ ] [Review][Patch] Tracked-file enumeration is capped at 65,536 characters, so real modules fail `workspace` and both source audits never run [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformAdoptionValidator.cs:48] — `ls-files -z` is 236 kB (Timesheets) to 4.2 MB (EventStore); reproduced on Parties with released 4.35.0, Git state unchanged. Read with an explicit large bound and add a regression with more than 65,536 characters of tracked paths. [edge-case-hunter+verification-gap+acceptance-auditor; high]
- [ ] [Review][Patch] Intra-repository `ProjectReference`s are reported as sibling fallback [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformAdoptionSourceAudit.cs:115] — `..\Hexalith.Parties.Contracts\…csproj` matches the textual `(?:\.\.[/\\])+Hexalith\.` test; Parties, Tenants, Folders and Projects all use this layout. Resolve the include against the build file's directory and fail only when it leaves the module root or enters a nested `references/*/references`. [acceptance-auditor; high]
- [ ] [Review][Patch] C#/script `dependency-selectors` patterns flag ordinary code [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformAdoptionSourceAudit.cs:64] — Raw, unmasked content: any `File.Exists`/`Directory.Exists` line containing `root` (any case) fails, e.g. Tenants `File.Exists(Path.Combine(repoRoot, "Hexalith.Tenants.sln"))`; helper names `FindRepositoryRoot`, `GetSourcePath`, `packageRoot` fail; comments are not masked. Require the probe to target `references/Hexalith.*`, `../Hexalith.*` or tool mode properties, mask comments, and add regressions for these real forms. [blind-hunter+edge-case-hunter+acceptance-auditor; high]
- [ ] [Review][Patch] Commented JSON fails the Dapr audit as malformed [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformAdoptionSourceAudit.cs:214] — default `JsonDocument.Parse` rejects comments/trailing commas; Tenants' tracked `.vscode/launch.json` fails `dapr-subscriptions`. Use `CommentHandling = Skip`, `AllowTrailingCommas = true`; add a JSONC regression. Templated (Helm) YAML parse failure is unverified for current modules. [blind-hunter+verification-gap+edge-case-hunter; medium]
- [ ] [Review][Patch] Platform kit test never exercises the pinned released tool [tests/Hexalith.Platform.Composition.Tests/DomainEnrollmentKitTests.cs:87] — It copies one pin file everywhere (pin compared with itself), runs a hand-built `bin/Release` DLL (absent on a clean checkout, stale DLL silently passes) via an undocumented `HEXALITH_ENROLLMENT_CANDIDATE`; a pin of 4.34.0 or a floor mismatch would still pass. Add a fact asserting the exact pin is inside `toolVersionRange`, equals its floor and disables roll-forward; run or document the released tool for the CLI case and rename "Candidate". [verification-gap+blind-hunter+edge-case-hunter; medium]
- [ ] [Review][Patch] Guide copy commands overwrite existing module files [docs/domain-module-enrollment.md:72] — `cp …/.config/dotnet-tools.json .config/dotnet-tools.json` deletes other pinned tools (Parties pins `aspirate` 9.1.0); step 2 `cp` (line 46) overwrites an existing `module/hexalith.module-manifest.v2.json`. Instruct merging only the `hexalith.builds.module.cli` entry and copying templates only when absent. [blind-hunter; medium]
- [ ] [Review][Patch] Frozen-inventory exemption rules are untested [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/PlatformAdoptionValidationTests.cs:147] — No case covers non-`frozen` state, a host directly in `src/`, or another `.csproj` under the frozen directory; deleting any rule widens the `dapr-subscriptions` exemption silently. Add cases asserting failing `hosting-inventory` and `dapr-subscriptions` rows. [verification-gap; medium]
- [ ] [Review][Patch] Declared-but-uninitialized required reference is untested [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/PlatformAdoptionValidationTests.cs:69] — The `IsInitialized` guard in `gitlinks` is the only protection; add an `uninitialized-reference` mutation (deinit Tenants, expect `gitlinks`). [verification-gap; medium]
- [ ] [Review][Patch] No acceptance run against a real module workspace [_bmad-output/implementation-artifacts/evidence/1-13/README.md] — Every candidate/packaged/released run and kit test uses tiny disposable fixtures, which is why the four defects above escaped. After fixes, record a read-only run on a real module checkout (e.g. a Parties clone) in evidence. [blind-hunter; medium]
- [ ] [Review][Patch] README Parties section contradicts the raised floor and is stale [README.md:67] — It says Parties uses tool 4.34.0 right after declaring 4.34.0 not Platform-accepted, without saying this is valid only against Parties' Platform gitlink `17179e5` (range `[4.34.0, 5.0.0-0)`), and still says owner review/upstream integration are required although Story 1.12 is complete. [blind-hunter+edge-case-hunter+acceptance-auditor; low]
- [ ] [Review][Patch] Adoption rows expose absolute paths and drop remediation hints [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformAdoptionValidator.cs:174] — `WorkspaceMappingException` diagnostics put `Path.Combine(root, reference)` in `Field`, used as the row `File`; the nested-check `Hint` (exact `git -C … submodule deinit` command) is discarded. Make the file workspace-relative and append the hint. [blind-hunter+edge-case-hunter+acceptance-auditor; low]
- [ ] [Review][Patch] Hosting project in a top-level directory can never pass [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformHostingInventory.cs:53] — `directory.Contains('/')` rejects `Hexalith.X.AppHost/Hexalith.X.AppHost.csproj`, while omission fails completeness. Drop that clause; keep the `src`/`test`/`tests` refusal. [blind-hunter+edge-case-hunter+acceptance-auditor; low]
- [ ] [Review][Patch] Dapr subscription/literal-name patterns miss common forms [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformAdoptionSourceAudit.cs:75] — Missed: `[HttpPost("orders"), Topic(...)]` attribute lists, controller `[HttpGet("dapr/subscribe")]`, and `TrySaveStateAsync`/`QueryStateAsync`/`PublishBulkEventAsync`/`InvokeBindingAsync` with literal names. Extend the expressions with regressions. [blind-hunter; low]
- [ ] [Review][Patch] Guide step 1 conflicts with the `gitlinks` check and omits the naming rule [docs/domain-module-enrollment.md:18] — "Initialize only these root paths" lists six fixed paths, but every additional declared dependency's reference must also be initialized; the `Hexalith.<id>` (case-insensitive) folder rule is not stated. [blind-hunter; low]
- [ ] [Review][Patch] Guide has no command for adding direct references (AC1 topic 1) [docs/domain-module-enrollment.md:11] — Only initialization is shown; add the `git submodule add <url> references/Hexalith.<Id>` recipe. [acceptance-auditor; low]
- [ ] [Review][Patch] Kit test parses stdout+stderr as JSON [tests/Hexalith.Platform.Composition.Tests/DomainEnrollmentKitTests.cs:134] — any stderr line breaks the JSON case; return and parse stdout separately. [blind-hunter+edge-case-hunter; low]
- [ ] [Review][Patch] Running outside Git leaves a relative root and raw `basePath` exceptions in rows [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformAdoptionValidator.cs:37] — `root` stays `"."`, so `Path.GetFullPath(path, ".")` throws `ArgumentException` in declaration/tool-pin/inventory rows. Initialize `root` with `Path.GetFullPath`. [edge-case-hunter; low]
- [ ] [Review][Patch] Command test ignores `TimedOut` under its 30-second bound [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/PlatformManifestCommandTests.cs:801] — a slow start surfaces as a misleading exit-code mismatch; assert `TimedOut` is false first. [edge-case-hunter; low]
- [ ] [Review][Patch] README lost the Platform-root restore and `--version` check [README.md:30] — the old Module tool recipe now appears only as a Parties-checkout debug command; re-add `dotnet tool restore` / `dotnet tool run hexalith-module --version` under Module tool. [edge-case-hunter; low]
- [ ] [Review][Patch] JSON report fields and `adoptionScope` are never asserted [references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/PlatformManifestCommandTests.cs:795] — tests check only row count 9 and text already present in diagnostics; assert row `requirement/passed/file/reason`, `adoptionScope == Scope` and the human trailer. [verification-gap; low]

Rejected:
- `low` tool-pin does not compare the executing binary or `toolVersionRange` — spec requires matching the direct Platform checkout, which is implemented; the guide mandates `dotnet tool run`; adding a binary-version guard is new logic.
- `low` kit test does not re-check Git after its failing run — failing-path read-only behaviour is covered by Builds `InitializedNestedReferenceFailsReadOnlyAsync`.
- `low` report contract undocumented, exit code shared with usage errors, failures printed twice, literal names under `dapr-subscriptions` — exit 1 matches the existing `validate` manifest-failure convention; the rest is cosmetic and changes public output.
- `low` full `v4.34.0..v4.35.0` delta and legacy v1 pin constants unreviewed — runtime catalog and run/debug topology are unchanged; no Parties/Tenants/Folders/Projects ships a v1 manifest; process-only.
- `low` skipped CI context-dependent tests lack an in-place compensating run — process-only; the local stable suite ran with the same three context skips.
- `low` YAML alias fan-out is unbounded — input is the owner's own tracked files; the fix adds guards for a self-inflicted case.
- `false` evidence README contradicts its completed status — the cited `fbd5abd`/BLOCKED lines sit in sections titled "(historical)", and `MinVerVersionOverride=4.34.0` is documented as local-only (README line 34).
- `false` Diagnostics namespace depends on Manifest — same assembly; no caller or rule named that breaks.
- `low` module-owned Dapr `kind: Component`/`Subscription` YAML not recognized — current module component YAML sits in frozen AppHost directories; needs a new detection branch.
- `low` hosting kind detected by file name only (`AppHost.csproj`, `IsAspireHost`) — all target modules use `*.AppHost/.Aspire/.ServiceDefaults.csproj`; SDK detection is new logic.
- `low` C# masker mis-tracks `@$"…\"`, `@""""` and apostrophes in `#region` — rare constructs, false negatives backstopped by mandatory human review; the fix adds branches.
- `low` `gitlinks` stops at the first bad reference — each reference is not an independent requirement; the fix adds collection logic.
- `false` hyphenated dependency ids never match a reference — matching equals the run tool's `CompositionDeclarationClosure.Repository` mapping (`"Hexalith." + id`, compared case-insensitively).
- `low` `references/Hexalith.Platform` without `.git` lets `tool-pin` pass — `gitlinks` still fails for an uninitialized Platform, so the report fails; the fix adds a guard.
- `low` sibling paths in custom-named properties escape the build audit — target modules use `Hexalith*Root` names, which are caught; a fix needs the path-resolution redesign above.
- `low` NuGet.config/.slnx/.fsproj/.cmd not scanned — no target module has a relative NuGet feed; the guide declares the bounded scope with human review.
- `low` kit test `WaitForExitAsync` is unbounded — the xUnit cancellation token applies; adding a bounded kill path is new logic for an unlikely hang.
- `low` evidence records the owner-account branch-rule bypass — owner-authorized, and CI, qualification and production gates all ran; process-only.

## Implementation Notes

Owner approved reviewed Builds candidate `fff4a212180d8cd1c5c0759655a9bc4fb6963c45` and the gated release on 2026-10-20 at 12:57:36 UTC (reply: “Approve candidate and release”). Recorded in evidence/1-13/owner-approval.json. Upstream source push is authorized; exact-source CI and the existing release pipeline remain required before pinning.

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

All 17 findings are fixed and their targeted regressions passed (65 cases plus 3 final generic-configuration format cases). No finding was deferred. The earlier broad failures were superseded by a restored normal Platform build and stable full Builds verification; owner approval, gated publication and fresh released-pin acceptance are complete (see final verification below).

## Verification

- Build and execute focused Builds test classes directly with xUnit v3; run the owning test project checks required for release.
- Run `DomainEnrollmentKitTests` and both output modes of released pinned `validate --adoption` on disposable passing/failing workspaces; confirm nonzero failure exits and unchanged Git state.
- Verify published version, release tag/gitlink equality and supported range; retain exact commands/results in story evidence.

Historical initial implementation verification (superseded by the final checks below): Builds focused Release build (zero warnings/errors), 97 focused adoption/manifest-command tests passed; Platform disposable focused lane ran the actual `DomainEnrollmentKitTests` against the same validator (4 passed); four direct candidate CLI human/JSON pass/fail commands preserved every workspace file hash and Git state. Normal Platform Release build and full Builds test suite remain blocked/failing amid existing/concurrent non-story dependency and legacy-fixture changes. See `evidence/1-13/README.md` and retained raw outputs. At that initial checkpoint, release acceptance was pending. The final checks below supersede that state; human SM-2 remains a later module-owner responsibility.

Coordinator matrix audit: read the complete story-scoped combined Platform/Builds diff at `/tmp/story-1-13-review-zwttdunt.diff`. CompleteWorkspacePassesAndRemainsUnchangedAsync covers happy/read-only/sibling/legitimate guards; EveryRequirementFailureIsReportedAsync covers missing/malformed inputs, pins, direct references, gitlinks and forbidden patterns; InitializedNestedReferenceFailsReadOnlyAsync covers nested initialization; FrozenInventoryIsConfinedToExactHostingDirectoryAsync and arbitrary-exclusion cover inventory confinement; ReportsRenderRowsAndLeaveLegacyJsonUnchangedAsync plus PlatformManifestCommandTests cover legacy compatibility and output modes. All ran in the 97-pass output. Platform tests and four direct commands cover shipped templates and candidate behavior. Local implementation tasks are verified; the explicitly post-review publication/acceptance task remains open.

Final coordinator verification: stable full Builds suite ran 1,185 cases, 1,182 passed, zero failed, three enclosing-Platform-dependent skips. Three final generic configuration regressions also passed independently. Freshly restored normal Platform composition test project builds with zero warnings/errors and passes all four DomainEnrollmentKitTests. Four commands through a locally packaged tool and actual dotnet tool run pass in human/JSON success/failure modes, preserving all file hashes and Git state. All review findings are fixed, with none deferred. Builds local commits: `b6642fecff8617e1765d835e71ebde316e3f71a5` and `fff4a212180d8cd1c5c0759655a9bc4fb6963c45`. Complete product diff, source hashes and raw outputs are retained in evidence/1-13. The spec remained in-review at that local checkpoint; public release and fresh acceptance were completed afterward as recorded below.


Released acceptance — 2026-10-20: owner-approved source `fff4a212180d8cd1c5c0759655a9bc4fb6963c45` passed exact-source CI `39426403061` (Module 1,188 total, 1,185 passed, 0 failed, 3 context skips) and official release `39431435493`. Published `v4.35.0` points to that exact commit. Platform pins published tool `4.35.0` with roll-forward disabled, range `[4.35.0, 5.0.0-0)` and the matching Builds gitlink. Fresh NuGet-only restoration into an empty CLI home/package cache passed all four human/JSON success/failure commands with unchanged Git state and every file hash. All four Platform kit tests passed against the official released CLI. The fresh signed-package hash matches the publisher-verified remote package and its source-bound qualified inventory. Runtime catalog and AppHost topology are unchanged from v4.34.0. No review finding was deferred. All story execution tasks are complete; module-owner live SM-2 and retirement gates remain explicitly pending in their templates for later adoption. Canonical records, approval, workflow/release links and reproducible outputs are in `evidence/1-13/README.md`.
