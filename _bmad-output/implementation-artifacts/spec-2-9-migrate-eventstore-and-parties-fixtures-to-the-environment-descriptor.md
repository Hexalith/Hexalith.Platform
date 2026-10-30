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
  - '{project-root}/references/Hexalith.AI.Tools/hexalith-state-instructions.md'
  - '{project-root}/_bmad-output/implementation-artifacts/spec-3-3-generate-the-run-scoped-routing-catalog.md'
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
- [x] `P/tests/Hexalith.Parties.Platform.IntegrationTests/Hexalith.Parties.Platform.IntegrationTests.csproj`, `PlatformEnvironmentFixture.cs`, `PartyEnvironmentTests.cs` — implement real dependency observations, flow and persisted-state assertions.
- [ ] Parties renderer/manifest/declaration test files, `P/Hexalith.Parties.slnx`, `P/scripts/test.ps1` — enroll explicit source/package suites while preserving isolated tests.
- [x] `E/docs/reference/nuget-packages.md`, `P/README.md`, `_bmad-output/implementation-artifacts/evidence/2-9/README.md` — document migration, exact revisions, validation and limits.

**Acceptance Criteria:**

- Given either EventStore testing package in Platform, when its integration fixture initializes, then it consumes the descriptor and starts no infrastructure.
- Given Parties' declared suite, when executed through `hexalith-module test`, then real dependencies serve the flow and persisted state is verified.
- Given Parties isolated projects, when run directly without Platform/domain servers, then they pass with doubles confined to tests.
- Given EventStore own-repository tests, when using their private fixture, then bootstrap remains available without being exported.

## Implementation Notes

- Implemented strict shared descriptor consumption with the embedded authoritative v1 schema,
  metadata-only refusals, exact resource/module/endpoint selection, matching private identities,
  bounded origin-confined clients and fixture-owned cancellation/disposal. Multiple endpoints on
  one resource and streaming bodies remain covered. Exported fixtures start no infrastructure;
  private EventStore bootstrap and the separate frozen Parties topology lane remain available.
- The final integrated source run `23013726fd912b1143fae3f1302661c3` passed the actual declared
  `platform-source` suite through `hexalith-module test`: one MTP case executed/passed, zero
  failures/errors/skips/not-executed. The runner retained the ready owner descriptor, result,
  inspected report, timings/startup observations and real persisted-state observations. These record
  two stored events at sequences 1/2, metadata sequence 2, exact synthetic identity and command,
  PartyCreated/PartyDisplayNameDerived envelope metadata/timestamps and a Current projection.
  Real Tenants and Memories served the run tenant. Exact automatic cleanup left no resources,
  workspaces or claims and reported no uncertainty or failures.
- Necessary run-owned preparation now uses the completed Story 3.3 owner producer and existing
  owner contracts: real service-bound BootstrapGlobalAdmin, separate operator tenant/membership
  commands, non-admin native scope derived from actual public catalog operations and real Memories
  provisioning to Active. Hosted bootstrap remains disabled. Native subject has TenantReader
  membership and consumes the actual `party` and Tenants query domains without administrator
  authority. No general Story 3.3/3.5 or production qualification is inferred.
- Memories' declared public API is consumed through its actually allocated, identity-probed Dapr
  invocation URI and public `/api/v1/` prefix. A separate strict private file binds the existing
  DAPR_API_TOKEN to run/isolation/resource/module/endpoint/exact URI. It is published only after real
  preparation, excluded from all evidence and deleted with the workspace; APP_API_TOKEN remains
  between sidecar and app. Clients refuse prefix escapes, Dapr target/Basic-auth overrides and
  conflicting or missing credentials. The Memory workload client uses its own sidecar; its receiver
  adds only the demonstrated EventStore canonical metadata POST grant, preserving deny-default,
  workload/channel/catalog policies and all existing pins/optional features.
- Native TRX captured output is intentionally removed by the existing report redactor. Persistence
  observations use the existing inspected `functional-observations.json` channel instead: explicit
  report/descriptor-owned path, bounded private CreateNew write after assertions, exact run/status,
  qualificationCredit=false and credential inspection before retention. No payloads or credentials
  are retained. All earlier failures and the intermediate passes remain recorded separately.
- Final integrated affected checks pass: EventStore Testing 232, Testing.Integration 54, runner 77
  and actual composition regressions 2; no failures/skips/not-run. Named reader/credential/fixture
  results cover 35/42/10 cases, including every frozen matrix row, exact tuple selection, malformed
  private channels, no-send path/routing refusals and streamed/disposal cleanup.
- The isolated/private-owner continuation checkpoint passed all earlier declared projects, main
  Parties 2,811 total (2,807 passed; four existing optional skips), private cutover/restore 16 and
  declaration tests 7. Its source/package/Release and repository-path repairs preserve strict
  committed-gitlink/actual-HEAD/clean immutable-consumption gates: diagnostic hashes and source
  overrides grant no immutable receipt or historical approval. A final current isolated/private
  rerun is recorded separately under the runtime evidence.
- Shared revisions advanced through other actors' commits. The original spec baseline remains
  `d5f16fb64f766eea4cf9dbc1a324ba3d46a1266a`; the final integrated qualification snapshot is
  `b4118eda9740781ca8e1f4f745d4cff7f046ecfa`, distinct from earlier merge input `2030542…` and
  initial isolated snapshot `c31e4f7…`. Forty task source inputs, all selected owners, exact binary
  hashes and owned merge hunks are recorded. Independent registry/package/container changes and
  foreign staged work are preserved; this agent made no commit, push or publication.
- [Runtime evidence](evidence/2-9/runtime-prerequisites/) preserves source selection/repair records,
  named commands/results, all failed attempts, actual owner receipts and exact cleanup. The final
  scan checked thirteen actual configuration/native-bearer values against forty-nine retained files,
  with zero exact matches or supplemental JWT/private-key patterns. In-process preparation
  operator/workload values are outside that external exact scan coverage; the runner separately
  inspects actual handed-off native/Dapr values. Source-only acceptance grants no attached execution,
  expiry-termination, installed/published package, CI, immutable consumption or broader tool credit.
- User continuation (2026-10-30): "finish the missing work before proceeding" explicitly authorized
  the necessary runner-owned local catalog, tenant and credential/domain prerequisites. Fixtures
  still neither provision environments nor mint credentials. The attached-execution follow-up,
  authoritative publication requirement and prohibition on hosted changes remain unchanged.

## Spec Change Log

## Review Triage Log

## Verification

- Build affected projects individually in Debug with `-nodeReuse:false`; run xUnit v3 DLLs using `-class`.
- Run Parties isolated projects and `python3 scripts/test_platform_declaration.py` from P.
- Run declared source suites; retain descriptor/result and persistence observations. Inspect baseline resources before code changes.
- Check schema/packaging boundaries. Package acceptance requires authoritative publications; Debug grants none.

