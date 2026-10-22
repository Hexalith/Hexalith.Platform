---
title: 'Remediate Epic 1 active identity, enrollment handoff and verification fixtures'
type: 'bugfix'
created: '2026-10-22'
status: 'in-review'
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

## Spec Change Log

## Review Triage Log

## Verification

- `dotnet build references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1` — build resolver/MSBuild regressions.
- Invoke that project's `bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll` with `dotnet` and xUnit `-class '*WorkspaceRootResolverTests'` / `-class '*WorkspaceMsBuildTests'`.
- `dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -m:1`, then `dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll` — focused affected classes first, then the full suite.
- `python3 scripts/test-reference-module-declarations.py` — complete through its final success row.

Retain exact commands/results under `_bmad-output/implementation-artifacts/evidence/epic-1-retro-remediation/`; synthetic results confer no published-release or live acceptance.
