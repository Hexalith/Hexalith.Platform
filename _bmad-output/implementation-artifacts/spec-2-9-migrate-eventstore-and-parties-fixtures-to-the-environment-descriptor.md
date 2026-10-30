---
title: '2.9 Migrate EventStore and Parties fixtures to the environment descriptor'
type: 'feature'
created: '2026-10-30'
status: 'draft'
baseline_commit: 'd5f16fb64f766eea4cf9dbc1a324ba3d46a1266a'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/docs/contracts/test-environment-v1.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EventStore exports infrastructure-starting fixtures; Parties starts its own topology and declares no runner suites. These cannot establish Platform-owned integration evidence.

**Approach:** Provide a shared descriptor consumer in EventStore testing packages and a declared Parties suite using real EventStore, Tenants and Memories supplied by the runner.

## Boundaries & Constraints

**Always:**
- Read absolute `HEXALITH_TEST_ENVIRONMENT_PATH`; validate v1 schema, policy, semantic bindings, readiness, required startup success and synthetic isolation before exposing endpoints.
- Resolve endpoints by declared resource/module/name. Keep credentials in the runner's private channel; reject missing or conflicting identity. Disposal releases fixture clients only.
- Exported fixtures consume descriptors exclusively. Bootstrap stays in non-packaged EventStore tests. Document API migration.
- Preserve frozen Parties topology tests separately without Platform evidence; isolated tests need no servers.
- Verify Parties create/query/persisted events and real Tenants/Memories through declared interfaces. No suite doubles or successful missing-handoff skips.

**Never:** Start an AppHost, daprd or containers from migrated fixtures; discover hosted endpoints; mint replacement credentials; infer unavailable bindings; change v1 semantics, optional feature policy, published pins or CI workflows.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected behavior | Error handling |
| --- | --- | --- | --- |
| Ready owner | Valid descriptor and private credentials | Clients use supplied endpoints and synthetic tenant | Bounded execution |
| Invalid handoff | Missing, oversized, malformed, unsupported or inconsistent | No client or infrastructure startup | Metadata-only refusal |
| Unready | Missing dependency, unavailable isolation or failed required task | Suite cannot run | Fail, never skip |
| Disposed | Fixture ends | Its requests stop; environment remains runner-owned | Report disposal failure |

</frozen-after-approval>

## Open Questions

1. Story 2.7 assigns stopping attached work/access at expiry to 2.9; Builds currently rejects attached suite execution. **Split (recommended):** migrate fixtures first; move this open obligation into a named follow-up without attachment qualification claims. **Include:** add supervised attached execution and result/termination reporting here, expanding implementation and qualification. Expiry semantics stay fixed.

## Code Map

Paths use E = `references/Hexalith.EventStore`, P = `references/Hexalith.Parties`, B = `references/Hexalith.Builds`.

- `E/src/Hexalith.EventStore.Testing/`: add reader/model with embedded schema and JsonSchema.Net. Match `B/src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentContractReader.cs` guards; Tooling is non-packable.
- `E/src/Hexalith.EventStore.Testing.Integration/`: replace exported `AspireTopologyFixtureBase.cs` and `DaprDomainServiceTestFixtureBase.cs` bootstrap with descriptor clients; update `DaprFactAttribute.cs`, dependencies and package description.
- `E/tests/Hexalith.EventStore.Testing*.Tests/`: consumer/refusal tests. Private bootstrap stays in `E/tests/Hexalith.EventStore.IntegrationTests/`.
- `P/tests/Hexalith.Parties.IntegrationTests/Gateway/EventStoreGatewayE2ETests.cs`: reuse create/query/persisted-envelope assertions. Add separate `P/tests/Hexalith.Parties.Platform.IntegrationTests/` without AppHost references.
- `P/scripts/render-platform-declaration.py` owns `P/module/hexalith.module-manifest.v2.json`; update both and `P/scripts/test_platform_declaration.py`.
- `B/src/libraries/Hexalith.Builds.Tooling/Runtime/ModuleCommandExecutionService.cs` refuses attached suites. Its executor/process/gate own work and access; fixture cancellation cannot satisfy the attached-work obligation.

## Tasks & Acceptance

**Execution:**
- [ ] `E/src/Hexalith.EventStore.Testing/` — implement validated descriptor consumption and client bindings.
- [ ] `E/src/Hexalith.EventStore.Testing.Integration/` and affected test projects — remove exported startup and preserve private owner tests; cover every matrix row.
- [ ] `P/tests/Hexalith.Parties.Platform.IntegrationTests/` — implement real-service flow, persisted-state assertions and dependency observations.
- [ ] Parties renderer, manifest, declaration tests, `P/Hexalith.Parties.slnx` and `P/scripts/test.ps1` — enroll explicit source/package suites without changing isolated-lane behavior.
- [ ] `E/docs/reference/nuget-packages.md`, `P/README.md`, `_bmad-output/implementation-artifacts/evidence/2-9/` — document migration and retain validation, exact revisions and limitations.

**Acceptance Criteria:**
- Given either exported EventStore testing package in Platform, when its integration fixture initializes, then it consumes the descriptor and starts no infrastructure.
- Given Parties' declared runner suite, when executed by `hexalith-module test`, then real dependencies serve the flow and persisted state is verified.
- Given Parties isolated projects, when run directly without Platform/domain servers, then they pass with doubles confined to tests.
- Given EventStore own-repository tests, when their private fixture is used, then their existing bootstrap path remains available without being exported.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

- Build test projects individually in Debug with `-nodeReuse:false`; run xUnit v3 DLLs with `-class`.
- Run Parties' isolated project inventory and `python3 scripts/test_platform_declaration.py` from P.
- Run declared source suites through the runner; retain descriptor/result and independent persisted-state observations. Inspect baseline resources before code changes.
- Verify schema and packed API/dependency boundaries. Package acceptance requires authoritative published dependencies; Debug grants none.

