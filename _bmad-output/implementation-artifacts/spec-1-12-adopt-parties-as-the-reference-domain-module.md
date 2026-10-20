---
title: '1.12 Adopt Parties as the reference domain module'
type: 'feature'
epic: 1
story: 12
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

**Problem:** Parties lacks a Platform declaration, tool pin and executable bindings; build selectors override the source mapping.

**Approach:** Enroll the existing Parties domain server with real EventStore, Tenants and Memories, then prove an active-source breakpoint and edit.

## Boundaries & Constraints

**Always:** Pin accepted tool 4.34.0. Preserve package CI, state formats, readiness semantics and deny-by-default ACLs. Use direct references. Preserve unrelated changes. Require module-owner review and upstream integration for completion.

**Never:** Initialize nested references in this workspace; retire or extend legacy hosting; enroll UI/MCP/sample hosts; enable optional rich search; add gateway extensions or change persistence behavior.

## I/O & Edge-Case Matrix

| Scenario | Input/state | Expected behavior | Failure |
|---|---|---|---|
| Own-workspace debug | Independent `Hexalith.Parties` clone | Parties plus six reference servers ready | Existing startup diagnostics |
| Mapping | Tool-selected roots, distracting sibling | Only mapped Debug projects | Missing/overridden source refuses |
| ACL source | Owner YAML and generated declaration | Exact existing EventStore POST grants | Missing, malformed, unsafe or stale input fails |
| Components/topics | Nondefault bound names | All consumers use bindings; declarative tenant subscription | Existing validation refuses invalid bindings |

</frozen-after-approval>

## Code Map

Aliases: `P = references/Hexalith.Parties`, `C = src/Hexalith.Platform.Composition`, `T = references/Hexalith.Tenants`.

- `P/Directory.Build.props`: root/mode/FromSource probes; Builds' `SourceMappingMaterializer` supplies these properties already.
- `C/PlatformServerBindings.cs`, `C/PlatformAppModel.cs`: missing Parties project, authentication and singular `party` gateway registration.
- `P/src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml`: authoritative 13-route ACL. Reuse strict parsing from `scripts/test-reference-module-declarations.py`.
- SDK already suppresses topic discovery with `Dapr:DeclarativeSubscriptions=true`. Tenant receiver is `/tenants/events`, topic `system.tenants.events`.

## Tasks & Acceptance

**Execution:**
- [ ] `P/module/hexalith.module-manifest.v2.json`, `P/.config/dotnet-tools.json` -- declare `parties/domain`, app/resource `parties`, dependencies EventStore/Tenants/Memories, `/ready`, configuration/secret/component keys and tenant subscription; pin the tool alongside aspirate.
- [ ] `P/scripts/render-platform-declaration.py`, `P/.github/workflows/ci.yml` -- derive callers/invocation rules from the owner ACL; support generation and strict `--check`; run the check in CI.
- [ ] `P/Directory.Build.props`, `P/Directory.Packages.props` -- remove dependency probing and mode/source selectors; retain only a deterministic direct Builds catalog default, respecting injected catalog authority, so ordinary package CI still works.
- [ ] `P/src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj`, `P/src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj`, `P/src/Hexalith.Parties/Hexalith.Parties.csproj` -- remove extra root probes and Memories reference metadata overriding the mapping.
- [ ] `P/src/Hexalith.Parties.Security/PartiesSecurityStoreOptions.cs`, sibling `PartyErasureRecordStore.cs`, `KeyOperationAuditService.cs`, `TenantKeyRotationService.cs` -- replace literal component names with `Parties:Security:StateStoreName`; retain default compatibility and record keys. Bind options in `P/src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`; pass configured names to health registration in `P/src/Hexalith.Parties/Program.cs`.
- [ ] `C/PlatformServerBindings.cs`, `C/PlatformAppModel.cs`, `C/PlatformLocalProfile.cs`, `C/Declarations/local-profile.json` -- add Parties executable, shared local authentication/app-channel binding, component inputs and `party` routing. `T/module/hexalith.module-manifest.v2.json` -- restore the existing local Parties-to-Tenants `GET /ready` permission without broadening other routes.
- [ ] `P/tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs`, `P/tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs` -- replace obsolete expectations. New `P/tests/Hexalith.Parties.Security.Tests/SecurityComponentBindingTests.cs`, `P/tests/Hexalith.Parties.Tests/HealthChecks/ConfiguredComponentHealthCheckTests.cs`, `P/scripts/test_platform_declaration.py`, `tests/Hexalith.Platform.Composition.Tests/PartiesAdoptionTests.cs` -- test matrix, configured writes/probes, ACL drift and seven-server composition.
- [ ] `P/README.md`, `P/docs/getting-started.md`, `P/docs/development-guide.md`, `P/docs/ci.md`, `README.md` -- document pinned-tool adoption and retained legacy-host scope.
- [ ] `P/references/Hexalith.Platform`, `P/references/Hexalith.Tenants`, `references/Hexalith.Parties`, `references/Hexalith.Tenants` -- integrate reviewed Tenants permission, Platform support P1, Parties adoption Q1 pinning P1, then Platform pinning Q1. Keep Q1 pinned to P1. No Builds release expected.
- [ ] `_bmad-output/implementation-artifacts/evidence/1-12/` -- record debugger/edit proof, clean rerun, Platform smoke, full revisions, tool/environment, mapping, diagnostics and cleanup. Initialize only the clone's direct references; Platform nested references remain uninitialized.

**Acceptance Criteria:**
- Given the independent Parties checkout, when the pinned tool runs `debug`, then exactly Parties and its declared reference services become ready, a breakpoint hits Parties source, and a disposable source edit changes its existing warning response header.
- Given completed proof, when the probe edit is restored and the environment stopped, then clean source reruns successfully and only run-owned resources are removed.
- Given source and package build checks, when evaluated, then source uses the selected direct projects and package CI retains package references without file-existence mode selection.
- Given reviewed upstream changes, when Platform verifies the pinned Parties declaration, then the same reference composition passes and the SM-2 record contains source revision, environment, result and diagnostics.

## Implementation Notes

- 2026-10-20: User approved this spec and continued implementation. Build step 3 requires sequential execution with no pushes or remote version-control operations. Prepare and verify local changes first; upstream integration remains an orchestrator follow-through after review. Preserve all concurrent changes outside this story, including other specs and recovery/Works work.

## Spec Change Log

## Review Triage Log

## Verification

Run ACL checks, owner test projects individually, and composition tests. In the clone run `dotnet tool restore`, then `dotnet tool run hexalith-module -- debug --manifest module/hexalith.module-manifest.v2.json`; attach to the service child PID. Break in `MvpComplianceWarningMiddleware.InvokeAsync` on `/ready`; edit `MvpComplianceWarning.Message` reversibly. Record any untagged Platform warning. Finish with exact-run `down` and nested-reference checks.
