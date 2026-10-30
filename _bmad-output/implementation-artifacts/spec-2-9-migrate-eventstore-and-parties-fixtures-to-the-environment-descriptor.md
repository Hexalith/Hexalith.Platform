---
title: '2.9 Migrate EventStore and Parties fixtures to the environment descriptor'
type: 'feature'
created: '2026-10-30'
status: 'in-progress'
baseline_commit: 'd5f16fb64f766eea4cf9dbc1a324ba3d46a1266a'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/docs/contracts/test-environment-v1.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Exported EventStore fixtures start infrastructure, while Parties integration fixtures start their own topology and declare no runner suites. Tests must consume environments owned by Platform.

**Approach:** Add a shared descriptor consumer to EventStore testing packages and a declared Parties suite using real EventStore, Tenants and Memories supplied by the runner.

## Boundaries & Constraints

**Always:**

- Read absolute `HEXALITH_TEST_ENVIRONMENT_PATH`; validate v1 schema, policy, semantic bindings, readiness, required startup success and synthetic isolation before exposing endpoints.
- Select endpoints by declared resource/module/name. Keep credentials in the separate runner channel; reject conflicting or missing identity. Disposal stops fixture requests and releases clients.
- Exported fixtures consume descriptors exclusively. Keep bootstrap private to non-packaged EventStore tests and document API migration.
- Preserve frozen Parties topology tests as a separate compatibility lane; isolated projects need no Platform or domain servers.
- Verify Parties create/query/persisted events and real Tenants/Memories through declared interfaces. Missing handoff fails without startup or successful skips.
- Decision (2026-10-30, user: split): attached execution, expiry enforcement and termination reporting remain open in follow-up `attached-test-execution-and-expiry-enforcement`. Preserve expiry semantics; fixture migration grants no attachment qualification.

**Never:** Start infrastructure from migrated fixtures; use hosted endpoints or suite doubles; mint credentials; infer unavailable bindings; change v1 semantics, optional feature policy, published pins or CI workflows.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Error handling |
| --- | --- | --- | --- |
| Ready owner | Valid descriptor/private credentials | Supplied endpoints and synthetic tenant | Bounded requests |
| Invalid handoff | Missing, oversized, malformed, unsupported or inconsistent | No clients or startup | Metadata-only refusal |
| Unready | Missing dependency/isolation or failed required task | Refuse suite execution | Fail, never skip |
| Disposed | Fixture ends | Its requests stop; runner retains lifecycle ownership | Report disposal failure |

</frozen-after-approval>

## Code Map

E = `references/Hexalith.EventStore`; P = `references/Hexalith.Parties`; T = `E/src/Hexalith.EventStore.Testing`; I = `E/src/Hexalith.EventStore.Testing.Integration`.

- New `T/TestEnvironmentDescriptorReader.cs` and `T/TestEnvironmentDescriptor.cs`: shared consumer/model. Embed authoritative schema through `T/Hexalith.EventStore.Testing.csproj` and use JsonSchema.Net. Match guards in `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentContractReader.cs`; Tooling is non-packable.
- `I/AspireTopologyFixtureBase.cs`, `I/DaprDomainServiceTestFixtureBase.cs`, `I/DaprFactAttribute.cs`: exported bootstrap and local-prerequisite gates. Replace with descriptor-only fixture APIs, removing startup dependencies in `I/Hexalith.EventStore.Testing.Integration.csproj`.
- `E/tests/Hexalith.EventStore.Testing*.Tests/`: consumer/refusal regression projects. Private bootstrap already exists in `E/tests/Hexalith.EventStore.IntegrationTests/`.
- `P/tests/Hexalith.Parties.IntegrationTests/Gateway/EventStoreGatewayE2ETests.cs`: reuse create/query/persisted-envelope assertions. Frozen infrastructure-control fixtures stay separate.
- New `P/tests/Hexalith.Parties.Platform.IntegrationTests/`: descriptor-only project, fixture and flow tests, without AppHost references.
- `P/scripts/render-platform-declaration.py` owns `P/module/hexalith.module-manifest.v2.json`; update both and `P/scripts/test_platform_declaration.py`.

## Tasks & Acceptance

**Execution:**

- [x] `T/TestEnvironmentDescriptorReader.cs`, `T/TestEnvironmentDescriptor.cs`, `T/Hexalith.EventStore.Testing.csproj` — add validated shared consumption and schema embedding.
- [x] The mapped I fixture/attribute/project files and `E/tests/Hexalith.EventStore.Testing*.Tests/*.cs` — replace exported startup; preserve private owner tests; test every matrix row.
- [ ] `P/tests/Hexalith.Parties.Platform.IntegrationTests/Hexalith.Parties.Platform.IntegrationTests.csproj`, `PlatformEnvironmentFixture.cs`, `PartyEnvironmentTests.cs` — implement real dependency observations, flow and persisted-state assertions.
- [ ] Parties renderer/manifest/declaration test files, `P/Hexalith.Parties.slnx`, `P/scripts/test.ps1` — enroll explicit source/package suites while preserving isolated tests.
- [x] `E/docs/reference/nuget-packages.md`, `P/README.md`, `_bmad-output/implementation-artifacts/evidence/2-9/README.md` — document migration, exact revisions, validation and limits.

**Acceptance Criteria:**

- Given either EventStore testing package in Platform, when its integration fixture initializes, then it consumes the descriptor and starts no infrastructure.
- Given Parties' declared suite, when executed through `hexalith-module test`, then real dependencies serve the flow and persisted state is verified.
- Given Parties isolated projects, when run directly without Platform/domain servers, then they pass with doubles confined to tests.
- Given EventStore own-repository tests, when using their private fixture, then bootstrap remains available without being exported.

## Implementation Notes

- Implemented the shared strict descriptor reader/model with the embedded authoritative schema,
  metadata-only refusal categories, exact resource/module/endpoint selection, matching private
  runner identity, bounded origin-confined HTTP clients and disposal cancellation/failure reporting.
- Exported Aspire/Dapr fixtures now consume descriptors exclusively. Removed their startup and
  local prerequisite-skip paths; retained optional performance opt-in and private owner bootstrap.
- Added the descriptor-only Parties Platform project, source/package declarations, renderer tests,
  inventory enrollment and migration documentation. The suite uses declared Tenants/Memories REST
  interfaces and EventStore create/query flows with independent persisted-envelope assertions.
- Current acceptance remains incomplete: the declared source run failed before fixture launch on
  EventStore route-catalog readiness, preventing its authorized writer-protocol cutover. Synthetic
  tenant records are also not provided by the current owner runner; Story 3.5 owns that producer.
  The possible selected-token `parties`/contract `party` mismatch is an unexecuted source inference.
- EventStore testing checks pass (189 + 51, no skips), the new Parties project builds, and declaration
  tests pass (7). Broader isolated/owner checks retain unresolved compatibility/package/fitness and
  current constructor drift; focused passing subsets do not qualify the full lane.
- Another actor committed shared-workspace changes during implementation. This implementation
  agent did not stage or commit. Exact revisions, task-owned paths, concurrent deltas, patches,
  commands, failures and exact cleanup are recorded in [evidence/2-9/README.md](evidence/2-9/README.md).
  Status and unverified acceptance remain in progress; attachment/package qualification is unchanged.
- Parent audit verified all four matrix rows with 41 verbose, passing tests and retained their named
  results. Corrected the Contracts.UI fallback to add an entry only when the selected catalog lacks
  it; published Builds 4.38.3 and current-source catalogs each resolve one 4.7.1 entry, and the Parties
  suite rebuild passes. The new correction remains a working-tree change after the externally created
  commits. Live and broad isolated acceptance remain unsatisfied, so review has not started.

## Spec Change Log

## Review Triage Log

## Verification

- Build affected projects individually in Debug with `-nodeReuse:false`; run xUnit v3 DLLs using `-class`.
- Run Parties isolated projects and `python3 scripts/test_platform_declaration.py` from P.
- Run declared source suites; retain descriptor/result and persistence observations. Inspect baseline resources before code changes.
- Check schema/packaging boundaries. Package acceptance requires authoritative publications; Debug grants none.

