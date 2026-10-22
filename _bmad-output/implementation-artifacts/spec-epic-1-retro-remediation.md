---
title: 'Remediate Epic 1 active identity, enrollment handoff and verification fixtures'
type: 'bugfix'
created: '2026-10-22'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '3beff28eede537eb36e84cdb93900af2556202d7'
baseline_builds_commit: '781a4b648c2bd5fdd95884387bb2ababc59ffe31'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Epic 1 still maps standalone owners by checkout name, omits Platform executable registration from enrollment instructions, and has fixtures that fail before their intended assertions.

**Approach:** Complete retrospective actions A1/A2/A4 together, as requested. Reuse validated declaration identity, document the Platform-owned registration prerequisite, and restore meaningful verification with isolated fixtures.

## Boundaries & Constraints

**Always:** Preserve legacy and multi-module behavior, physical containment, direct-only initialization, recorded-gitlink ordering, duplicate guards and production provenance refusal. Preserve existing workspace changes, including the Builds catalog update and concurrent Memories work. Keep synthetic evidence distinct from real-owner/live acceptance.

**Never:** Change production snapshots, weaken validation, enroll another owner, initialize nested references, publish a tool, change pins, deploy, or change the retrospective verdict. A3/A5/A6 retain their recorded dispositions. No new schema or public registration API.

## I/O & Edge-Case Matrix

| State | Expected behavior |
| --- | --- |
| Valid single-owner v2 in canonical or renamed standalone root | Same declared repository identity, actual active path, in both modes; cover Git and supported non-Git resolution. |
| Legacy or multi-module declaration | Existing identity behavior; never select an arbitrary module. |
| Invalid v2 or identity collision | Fail with diagnostics; no invalid identity extraction, fallback or premature reference initialization. |
| Renamed source plus direct/transitive package copy | Existing duplicate refusal names canonical identity and origins; root named `Hexalith` does not capture unrelated packages. |
| Valid new owner absent from default project table | Static validity does not grant runtime registration; handoff remains pending. |
| Isolated package/owner negative fixtures | Valid prerequisites let each intended assertion execute; mismatched real provenance still fails closed. |

</frozen-after-approval>

## Code Map

Planning: no unresolved intent gaps or irreversible actions. The footprint is one resolver, enrollment documentation/templates, and fixture/regression tests. Builds paths are relative to `references/Hexalith.Builds/`; other paths are relative to Platform.

- `src/libraries/Hexalith.Builds.Tooling/Workspace/WorkspaceRootResolver.cs`: both directory-derived identity branches. Reuse `PlatformManifestValidator.Validate`, `PlatformManifestCommand.ActiveModule` and `CompositionDeclarationClosure.Repository`; preserve post-checkout manifest loading.
- `src/libraries/Hexalith.Builds.Tooling/Workspace/SourceMappingValidationTask.cs`: existing prefix guards should consume the corrected mapping unchanged.
- `src/Hexalith.Platform.Composition/PlatformServerBindings.cs`, `PlatformAppModel.cs`, `PlatformLocalProfile.cs`, `PlatformGatewayConfiguration.cs`: registration/configuration/routing authorities. The ordinary public runner offers no caller `Projects` override; the internal override is only a test seam.
- `tests/Hexalith.Platform.Composition.Tests/PlatformAppModelTests.cs`: shared package options mix old snapshots and current catalog; retain its separate real refusal case. Copied owner manifests require a repository-root marker and physical declared entry files.

## Tasks & Acceptance

**Execution:**

- [x] Builds `src/libraries/Hexalith.Builds.Tooling/Workspace/WorkspaceRootResolver.cs` — derive standalone identity from validated v2 declarations before entries/collision checks.
- [x] Builds `test/Hexalith.Builds.Module.Tests/Workspace/WorkspaceRootResolverTests.cs` and `WorkspaceMsBuildTests.cs` in that directory — cover matrix cases using resolver-produced mappings; retain checkout/symlink regressions. Update Builds `README.md` temporary directory-name guidance.
- [x] `tests/Hexalith.Platform.Composition.Tests/PartiesAdoptionTests.cs` — prove canonical and renamed resolved roots bind the actual Parties project.
- [x] `tests/Hexalith.Platform.Composition.Tests/PlatformPackageFixture.cs` and `PlatformOwnerFixture.cs` — add disposable, test-only helpers with consistent catalog, declaration bytes, provenance commits/hashes and physical declared entries. Qualify synthetic fixtures only.
- [x] `tests/Hexalith.Platform.Composition.Tests/PlatformAppModelTests.cs`, `PlatformAppModelReviewTests.cs` and `PlatformDaprBindingsTests.cs` — use those fixtures so package and ownership cases reach their assertions; preserve independent production-refusal coverage.
- [x] `scripts/test-reference-module-declarations.py` — materialize declared entries and validate the unmodified temporary set before the complete negative matrix; preserve public-diagnostic assertions.
- [x] `docs/domain-module-enrollment.md`, `templates/domain-module-enrollment/platform-handoff.md` and `sm-2-checklist.md` — require the accountable Platform/module owners' handoff before run/debug: reviewed direct Platform revision, server project/image bindings, scoped configuration, authentication/token adapters, gateway routing tuple, Dapr policy evidence and acceptance references. Keep real-owner evidence pending.
- [x] `tests/Hexalith.Platform.Composition.Tests/DomainEnrollmentBindingTests.cs` — demonstrate an unregistered synthetic owner refusing, then explicit project/configuration binding succeeding; verify routing preservation/filtering. Label this composition-only evidence, with ordinary CLI qualification still required.

**Acceptance Criteria:**

- Given the resolver's actual output, when Platform binds canonical and renamed Parties workspaces, then both select the owner project at their respective active paths.
- Given a module outside `ReferenceProjects`, when a maintainer follows the guide, then a pending, accountable registration record precedes ordinary CLI commands and names the Platform revision that must contain the implementation.
- Given repaired fixtures, when focused regressions, the full composition suite and the declaration matrix execute, then every intended assertion is reached; record counts, opt-in skips and any remaining blockers with exact failures and owners. The historical Dapr timeout remains unresolved without new live evidence.

## Implementation Notes

- Implemented and audited all eight tasks against the unified Platform diff (including new files) and Builds working diff. Preserved the concurrent owner/catalog/gitlink changes.
- Matrix audit: resolver class 66/66 and MSBuild class 95/95 passed without skips; new Parties/enrollment cases and repaired package/owner cases passed in the full composition run. Declaration matrix completed all 42 PASS rows.
- Full composition result: 214 passed, five classified integration/release failures, seven opt-in live skips. Exact commands, covering test names, accountable owners and original/final source tuples are retained in [verification evidence](evidence/epic-1-retro-remediation/README.md). This satisfies the approved remaining-blocker disposition; no live or published-release acceptance is claimed.
- Final post-review verification supersedes the initial counts above: 183 resolver/MSBuild tests and all 42 declaration-matrix checks passed. The full composition assembly had 218 passes, four classified failures and seven live skips (zero errors). All 29 named cases in the ten-method matrix audit passed. Review patches preserve Platform-owned deployment identity and independently exercise package provenance guards; two diagnostic-path assertions now match the existing public relative paths without weakening refusal checks.
- Shared-workspace compilation was blocked by concurrent unfinished source changes. The final evidence uses fixed Platform `e39d59b9e7f93c5880eff423e24b8a0f274560ed` and recorded Builds `2210aaaf54c779b8763dbf1e8767bdc1b422bc6d`, plus the retained two-assertion patch. Exact commits, commands, source/assembly hashes, XML and four remaining failure names/owners are in the evidence. Three release-pin mismatches remain with Platform/Builds release coordination; the duplicate Works EventStore token remains with Platform Works/EventStore integration. No release/live qualification or change to the original baseline is claimed.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Evidence and disposition |
| --- | --- | --- |
| Blind 1: Platform-owned deployment declaration becomes workspace identity | medium | The retained Timesheets candidate validates successfully, is outside references, and its derived Hexalith.Timesheets identity collides with Platform's direct Timesheets reference at the pre-initialization duplicate guard. Patched by retaining the already-recognized Platform repository boundary; both source/package regression cases passed. |
| Blind 2: malformed/oversized/unreadable classification falls back | false | Resolver accepts a validated manifest by contract; ordinary root callers classify v2 or run ModuleManifestLoader before root resolution. Malformed/unsupported input exits at ModuleCommandExecutionService's manifest guard; direct-reference parsing remains deliberately after recorded checkout. Recognizable invalid v2 is rejected and tested before identity extraction. |
| Blind 3: no successful unrelated-package build | false | The transitive regression requires project.assets.json after the unrelated wrapper passed the direct guard; the old broad Hexalith identity would fail before restore. Resolver identity assertions plus existing successful package/MSBuild tests cover the unchanged downstream selection. No uncovered failing caller was established. |
| Blind 4: normalized/reserved collision variants | false | All generated-name guards still run over the same entries immediately after the new canonical identity is inserted. Existing reserved/property-collision tests exercise these shared guards, and the new declared-identity collision verifies pre-initialization refusal. No bypass or changed guard was demonstrated. |
| Blind 5: Parties binding test is non-Git | false | Git resolver theories independently prove canonical identities and actual paths in both modes; PlatformServerBindings consumes those entries without a HasGit-specific branch. The non-Git binding test checks the actual Parties declaration/project at that consumer boundary. |
| Blind 6: production refusal uses source-origin entries in package mode | low | The release guard is selected by mode before source-entry use, so the observed refusal is real, but its mapping is less representative than the resolver's. Patched the test entries to package origins with null paths; the independent production-refusal case passed. |
| Blind 7: independent release hash/commit/catalog mutations absent | low | Synthetic positive metadata is self-consistent; byte/repository mutations exercise the earlier integrity guard, and production refusal exercises version mismatch. Patched with independent trusted hash/commit/catalog mutations; all three cases passed. |
| Blind 8: routing helper observation is separate from composition | low | The test honestly labels composition-only evidence and directly observes the real gateway helper's preservation/filtering; it does not claim ordinary CLI/live qualification. Existing composition routing tests cover model integration. Reject broader fixture setup for a negligible extra proof path. |
| Blind 9: failed fixture construction can leak temporary roots | low | Copy/parse/validation failure before IDisposable is returned can leave a small temporary fixture. Successful paths dispose normally; cleanup of exceptional test setup would add failure-path guards for an uncommon, negligible developer-only issue. Reject. |
| Blind 10: no per-command working-source hash | low | Evidence names original/final tuples and explicitly disclaims exact published/live qualification across concurrent integration. Per-command source fingerprint orchestration is the deferred A5 process work, not a hidden acceptance claim here. Reject expanding static-unit evidence machinery. |
| Verification other 1: source gitlink differs from published tool | medium | Recorded Builds 2d653b611c8efcffb4d27bd8715d5dbf24eaebfd differs from pinned 4.35.2 metadata/tag 4248b8c70cb35ba79fdd7e4dd63049d4c73211a0, as three enabled tests demonstrate. It was introduced by concurrent user integration, not this implementation. Defer to Platform/Builds release coordination; preserve pins and integrated revisions. |

## Verification

- `dotnet build references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1` — build resolver/MSBuild regressions.
- Invoke that project's `bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll` with `dotnet` and xUnit `-class '*WorkspaceRootResolverTests'` / `-class '*WorkspaceMsBuildTests'`.
- `dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -m:1`, then `dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll` — focused affected classes first, then the full suite.
- `python3 scripts/test-reference-module-declarations.py` — complete through its final success row.

Retain exact commands/results under `_bmad-output/implementation-artifacts/evidence/epic-1-retro-remediation/`; synthetic results confer no published-release or live acceptance.
