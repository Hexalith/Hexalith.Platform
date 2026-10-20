---
title: '1.12 Adopt Parties as the reference domain module'
type: 'feature'
epic: 1
story: 12
created: '2026-10-20'
status: 'done'
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
- [x] `P/module/hexalith.module-manifest.v2.json`, `P/.config/dotnet-tools.json` -- declare `parties/domain`, app/resource `parties`, dependencies EventStore/Tenants/Memories, `/ready`, configuration/secret/component keys and tenant subscription; pin the tool alongside aspirate.
- [x] `P/scripts/render-platform-declaration.py`, `P/.github/workflows/ci.yml` -- derive callers/invocation rules from the owner ACL; support generation and strict `--check`; run the check in CI.
- [x] `P/Directory.Build.props`, `P/Directory.Packages.props` -- remove dependency probing and mode/source selectors; retain only a deterministic direct Builds catalog default, respecting injected catalog authority, so ordinary package CI still works.
- [x] `P/src/Hexalith.Parties.Contracts/Hexalith.Parties.Contracts.csproj`, `P/src/Hexalith.Parties.Client/Hexalith.Parties.Client.csproj`, `P/src/Hexalith.Parties/Hexalith.Parties.csproj` -- remove extra root probes and Memories reference metadata overriding the mapping.
- [x] `P/src/Hexalith.Parties.Security/PartiesSecurityStoreOptions.cs`, sibling `PartyErasureRecordStore.cs`, `KeyOperationAuditService.cs`, `TenantKeyRotationService.cs` -- replace literal component names with `Parties:Security:StateStoreName`; retain default compatibility and record keys. Bind options in `P/src/Hexalith.Parties/Extensions/PartiesServiceCollectionExtensions.cs`; pass configured names to health registration in `P/src/Hexalith.Parties/Program.cs`.
- [x] `C/PlatformServerBindings.cs`, `C/PlatformAppModel.cs`, `C/PlatformLocalProfile.cs`, `C/Declarations/local-profile.json` -- add Parties executable, shared local authentication/app-channel binding, component inputs and `party` routing. `T/module/hexalith.module-manifest.v2.json` -- restore the existing local Parties-to-Tenants `GET /ready` permission without broadening other routes.
- [x] `P/tests/Hexalith.Parties.Ci.Tests/CommonsHttpRestoreRoutingTests.cs`, `P/tests/Hexalith.Parties.Tests/FitnessTests/AppHostTenantsTopologyTests.cs` -- replace obsolete expectations. New `P/tests/Hexalith.Parties.Security.Tests/SecurityComponentBindingTests.cs`, `P/tests/Hexalith.Parties.Tests/HealthChecks/ConfiguredComponentHealthCheckTests.cs`, `P/scripts/test_platform_declaration.py`, `tests/Hexalith.Platform.Composition.Tests/PartiesAdoptionTests.cs` -- test matrix, configured writes/probes, ACL drift and seven-server composition.
- [x] `P/README.md`, `P/docs/getting-started.md`, `P/docs/development-guide.md`, `P/docs/ci.md`, `README.md` -- document pinned-tool adoption and retained legacy-host scope.
- [x] `P/references/Hexalith.Platform`, `P/references/Hexalith.Tenants`, `references/Hexalith.Parties`, `references/Hexalith.Tenants` -- integrate reviewed Tenants permission, Platform support P1, Parties adoption Q1 pinning P1, then Platform pinning Q1. Keep Q1 pinned to P1. No Builds release expected.
- [x] `_bmad-output/implementation-artifacts/evidence/1-12/` -- record debugger/edit proof, clean rerun, Platform smoke, full revisions, tool/environment, mapping, diagnostics and cleanup. Initialize only the clone's direct references; Platform nested references remain uninitialized.

**Acceptance Criteria:**
- Given the independent Parties checkout, when the pinned tool runs `debug`, then exactly Parties and its declared reference services become ready, a breakpoint hits Parties source, and a disposable source edit changes its existing warning response header.
- Given completed proof, when the probe edit is restored and the environment stopped, then clean source reruns successfully and only run-owned resources are removed.
- Given source and package build checks, when evaluated, then source uses the selected direct projects and package CI retains package references without file-existence mode selection.
- Given reviewed upstream changes, when Platform verifies the pinned Parties declaration, then the same reference composition passes and the SM-2 record contains source revision, environment, result and diagnostics.

## Implementation Notes

- 2026-10-20: User approved this spec and continued implementation. Build step 3 requires sequential execution with no pushes or remote version-control operations. Prepare and verify local changes first; upstream integration remains an orchestrator follow-through after review. Preserve all concurrent changes outside this story, including other specs and recovery/Works work.

- 2026-10-20 coordinator audit: local Parties integration is `ae00820347b87483cc90538012c27464375ad9a1` with Platform support P1 and Tenants `9a94131a26d0c5d80d5540706e267ee123d54b68`. Independent own-workspace breakpoint/edit/clean proof and live Platform proof `89a20036053fb4dfbcc7478b884a8d953898de5c` passed; all exact runs were removed. The final audit restores existing CLR constructor signatures and corrects onboarding; six configured-DI binding cases pass. Normal composition build passes; the broad 210-case run has 11 unchanged snapshot/fixture/content-root failures and seven opt-in skips, recorded separately. Focused adoption 3, receiver 5, seed 4, onboarding 1 and security 250 cases pass. Matrix coverage is recorded in `evidence/1-12/matrix-audit.json`. Owner review and upstream integration remain an explicit incomplete gate; step 3 permitted no remote VCS operations.

- 2026-10-20 final review: all three review layers completed and every finding is triaged below. Owner candidate `895e04c4a5cc46faab0ac2d780aeff6de4e9f777` includes every accepted patch; coordinator checks pass (250 security, 15 host/configuration, three adoption, five renderer, three cache cases, reference matrix and joint validation). Four pre-existing broad-test problems are appended to the deferred-work ledger. Local owner integration and final parent pin are prepared. Status stays `in-review` because the approved frozen constraint requires module-owner approval and upstream integration for completion; neither has been inferred from the earlier spec approval. The build workflow prohibits automatic push.

- 2026-10-20 upstream completion: the user explicitly approved the reviewed owner changes and authorized pushing Parties, then Platform. The four approved Parties commits were rebased onto upstream `32fa72e2cb223d7f8554d3a5439fa6e15212ec26` with a byte-identical patch, yielding published `35d891d202a87fd4f494f2cadd26c277df44b84a`. Platform adoption `e9a142dc5c56cdb3c187f5eec0d3b3a7178a0058` pins that reachable upstream commit while Parties retains P1 and Tenants. Both remote main heads were verified. Post-rebase builds and all seven integration checks pass, including 15 host/configuration, 14 upstream projection, five renderer and three adoption cases. Approval, revisions, ordering and patch equivalence are recorded in `evidence/1-12/upstream-integration.json`. The integration gate is satisfied; the spec is done, sprint tracking remains review per the build workflow, and the four pre-existing test problems remain deferred.

## Spec Change Log

## Review Triage Log

| ID / layer | Verdict | Evidence and route |
|---|---|---|
| B1 / blind | medium | Reproduced under `python3 -O`: the renderer emits `unexpected` caller and `GET /process` because assertions disappear. Patch the existing validation checks to remain active under optimization, with an optimized-process regression case. |
| B2 / blind | low | The Playwright command still supplies retired selector properties, and no mapping is supplied; its Debug build therefore uses packages. Patch the development-guide claim to describe the actual package behavior. UI enrollment remains excluded. |
| B3 / blind | false | `PlatformAppModel.Compose(builder)` and public `ComposeRun` construct options with the fixed `ReferenceProjects` map; neither reads a configurable project-path override. The explicit `Projects` API supports owner-relative caller-supplied projects by contract. The cited cross-owner refusal tests mapping containment, so another explicit in-owner path is not an override reachable through the supported tool workflow. Reject. |
| B4 / blind | low | The helper intentionally reruns the pre-existing three-technical-owner ACL matrix; it does not provide Parties atomic-negative coverage. Parties itself has real closure validation, unsafe-owner-ACL negative tests and live proof. No malformed Parties declaration accepted by those gates was demonstrated. Reject the speculative expansion of the legacy inventory policy: it adds policy/test branches for an unlikely uncovered mutation. |
| B5 / blind | medium | The new owner CI job only compares generated bytes and exercises the Python renderer, so a jointly invalid template/declaration is not schema-validated by that job. Patch it to restore the pinned tool and validate Parties together with its directly initialized declared owners. |
| B6 / blind | low | The added subscription assertion accepts matching substrings even when component, scope or structure is wrong. Patch that assertion to parse the generated JSON resource and compare the exact Parties subscription fields. |
| B7 / blind | medium | Confirmed tests construct options rather than exercising `AddParties` configuration binding and startup validation. Patch with production-registration custom-component round trips and empty/whitespace rejection; groups with V1. |
| B8 / blind | false | `attach_ready.py` reports `BREAKPOINT PROOF PASSED` after observing a bound source breakpoint hit and HTTP 200; it does not report an edit assertion. The distinct edit proof records the actual `[platform-1-12-edit]` header in `edit-response.txt` and its restored value in `clean-response.txt`, with source hashes and probe patch. Thus the submitted proof demonstrates the edit without relying on that helper to assert it. Reject. |
| B9 / blind | low | The replay helper hardcodes this user's NuGet cache. A developer using another cache cannot run that helper. Patch cache discovery to respect `NUGET_PACKAGES` and the normal user cache, with an explicit missing-tool prerequisite. |
| B10 / blind | low | The evidence README labels older isolated-clone revisions as final before identifying the later compatibility candidate. Patch its opening to identify the current primary owner candidate and distinguish the older live-proof revisions. |
| V1 / verification-gap | medium | Filed evidence establishes that direct `Options.Create` registration bypasses production `.Bind`, so removing it leaves every new security test passing. Patch `AddParties` registration coverage with a nondefault store and round trips; groups with B7. |
| V2 / verification-gap | medium | Filed evidence establishes that helper-only custom-name checks and default-only host fixtures miss removal of configured arguments from `Program`. Patch the existing real-host factory/test to require custom state/pubsub names and assert `/ready` and both `/health` components. |

| C1 / coordinator verification | medium | Existing EventStore snapshot/catalog mismatch causes eight package-test failures; baseline snapshot and lookup code are unchanged. Defer to the snapshot owner, with exact comparison and logs. |
| C2 / coordinator verification | medium | Two existing isolated owner fixtures omit declared executable entries; unchanged test bodies demonstrate a fixture problem. Defer with missing-entry evidence. |
| C3 / coordinator verification | medium | One unchanged gateway seed case fails with a content-root error in the broad run while all four pass alone. Defer the verified parallel-fixture failure with its isolated rerun. |
| C4 / coordinator verification | medium | The unchanged historical receipt expects a git-describe string superseded by a tag on the same accepted commit. Defer the failing historical expectation without weakening it. |

All accepted findings are independent trivial corrections or test/gate additions: no new public surface or speculative runtime guards. B7/V1 share the missing production-binding test; the other accepted findings remain separate. No intent/spec loopback is needed.

## Verification

Run ACL checks, owner test projects individually, and composition tests. In the clone run `dotnet tool restore`, then `dotnet tool run hexalith-module -- debug --manifest module/hexalith.module-manifest.v2.json`; attach to the service child PID. Break in `MvpComplianceWarningMiddleware.InvokeAsync` on `/ready`; edit `MvpComplianceWarning.Message` reversibly. Record any untagged Platform warning. Finish with exact-run `down` and nested-reference checks.
