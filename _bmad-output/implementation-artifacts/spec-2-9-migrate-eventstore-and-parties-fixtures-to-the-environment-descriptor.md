---
title: '2.9 Migrate EventStore and Parties fixtures to the environment descriptor'
type: 'feature'
created: '2026-10-30'
status: 'done'
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
- [x] Parties renderer/manifest/declaration test files, `P/Hexalith.Parties.slnx`, `P/scripts/test.ps1` — enroll explicit source/package suites while preserving isolated tests.
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
- The final post-review source run `60799ff601e46cf61d4b3ae7c2316d3f` passed the actual declared
  `platform-source` suite through `hexalith-module test`: five MTP cases executed/passed (one real flow and four assertion controls), zero
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
  conflicting or missing credentials. Both request and content header collections are checked
  for Dapr routing/API-token overrides before secret injection; two actual no-send cases were red
  before the repair and pass afterward. The Memory workload client uses its own sidecar; its receiver
  adds only the demonstrated EventStore canonical metadata POST grant, preserving deny-default,
  workload/channel/catalog policies and all existing pins/optional features.
- Native TRX captured output is intentionally removed by the existing report redactor. Persistence
  observations use the existing inspected `functional-observations.json` channel instead: explicit
  report/descriptor-owned path, bounded private CreateNew write after assertions, exact run/status,
  qualificationCredit=false and credential inspection before retention. No payloads or credentials
  are retained. All earlier failures and the intermediate passes remain recorded separately.
- Final post-review SDK checks pass 248/248 and exported fixture checks 58/58 without skips. Named
  reader/credential/fixture results cover 45/48/14 cases (107), including every frozen matrix row,
  exact tuple selection, malformed private channels, request/content-header and prefix no-send
  refusals, and streamed/disposal cleanup. Runner checks pass 144/144, including actual selected-child secret retention and invalid-scope refusals. The current
  complete composition partial class passes 179 total (175 passed, four existing optional native
  skips), and the exact current identifier/deadline guard fixtures pass 5/5. No required native or
  descriptor-matrix case skips. Refreshed main/projection/private checks close the later SDK drift.
- The isolated/private-owner continuation checkpoint passed all earlier declared projects, main
  Parties 2,811 total (2,807 passed; four existing optional skips), private cutover/restore 16 and
  declaration tests 7. Its source/package/Release and repository-path repairs preserve strict
  committed-gitlink/actual-HEAD/clean immutable-consumption gates: diagnostic hashes and source
  overrides grant no immutable receipt or historical approval. The final current inventory runs all
  fifteen isolated projects: 6,183 total, 6,178 passed, five existing optional skips, zero failures,
  errors or not-run. These are the four main-suite optional native checks and the existing
  published-SDK-only large-history Projections case; no required native or matrix case skips.
  Current private-owner classes pass 24/24 (nine resilience, fifteen cutover-policy cases), and
  declaration checks pass 7/7. Earlier failures, including ordinary artifact-path assumptions,
  stale selected-source observations and local/public same-version package selection, remain
  retained in the final isolated evidence. Exact local-ID mapping and private caches now keep
  package fixtures on their actually packed owner prerequisites, preserving all dependency,
  consumer, Release and audit assertions. Clean local Release and independent source API exports
  match the approved sixteen-line additive Story 3.3 snapshot; no published-package credit follows.
- Shared revisions advanced through other actors' commits. The original spec baseline remains
  `d5f16fb64f766eea4cf9dbc1a324ba3d46a1266a`; the final integrated qualification snapshot is
  `b4118eda9740781ca8e1f4f745d4cff7f046ecfa`, distinct from earlier merge input `2030542…` and
  initial isolated snapshot `c31e4f7…`. Forty task source inputs, all selected owners, exact binary
  hashes and owned merge hunks are recorded. A final audit covered 11,141 current production/config
  inputs across all root-declared owners and imported 32 stable foreign changes into the isolated
  source checkout. The final cohort records 104,669 exact source-file hashes after all twenty review-patched inputs,
  the earlier SDK header guards and 22 test/docs input repairs; all hashes match after execution. Obsolete snapshot-driven fixture changes were
  withdrawn, preserving the current foreign tests and production guards. Independent registry/package/container changes and
  foreign staged work are preserved; local completion commits record the verified changes without push or publication.
- [Runtime evidence](evidence/2-9/runtime-prerequisites/) preserves source selection/repair records,
  named commands/results, all failed attempts, actual owner receipts and exact cleanup. The final
  scan checked thirteen actual configuration/native-bearer values against fifty retained runtime files,
  with zero exact matches or supplemental JWT/private-key patterns. In-process preparation
  operator/workload values are outside that external exact scan coverage; the runner separately
  inspects actual handed-off native/Dapr values. Source-only acceptance grants no attached execution,
  expiry-termination, installed/published package, CI, immutable consumption or broader tool credit.
- User continuation (2026-10-30): "finish the missing work before proceeding" explicitly authorized
  the necessary runner-owned local catalog, tenant and credential/domain prerequisites. Fixtures
  still neither provision environments nor mint credentials. The attached-execution follow-up,
  authoritative publication requirement and prohibition on hosted changes remain unchanged.

- Final post-review runtime evidence retains inspected TRX 5/5 passed: one real flow in 4.6449999
  seconds and four assertion controls. Functional artifact SHA256 is
  `b1c10726df96d70e4d3a78e9dd4263c36fbfa40499776b3fa7bfe852df16e225`.
  Actual tenant `g4t-60799ff601e4`, aggregate `01M678CPHY1542A2PMY330A24R` and command
  `01M678CPJ01WV70WSRTPZ74ATN` have two persisted envelopes at sequences 1/2 and metadata sequence 2.
  Automatic cleanup completed with 22 owned logical resources, zero leftovers/failures; independent
  run-label/PID/workspace/private-credential checks are empty. The observer checked 13 actual values
  across 50 runtime files with zero exact matches or JWT/private-key patterns. Its private cache was
  deleted after observer exit. All 104,669 launch source hashes remain unchanged after execution.
  Earlier `29b5e1…` and `230137…` passes remain historical checkpoints; `d04e42…` was interrupted for
  the header repair and grants no final acceptance or unavailable exact-scan credit.
- Three independent review layers yielded thirteen separately triaged findings. All ten patches are
  fixed and parent-verified; three issues are deferred with evidence: legacy Tenants/McpCli consumer
  migration outside this EventStore/Parties intent, pre-existing Windows workspace ACL policy, and
  independent private-host Swagger ordering. This qualifies Linux source execution only. Ordinary
  Parties solution builds exclude the explicitly declared native project until the migrated package
  API is authoritatively published; pins and CI workflows remain unchanged. Full verification and
  final native acceptance are in [post-review evidence](evidence/2-9/README.md).

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| B1 | high | patch | The newly enrolled native project compiles against missing types in published Testing.Integration3.128.4 when ordinary Release solutions build. The cached DLL contains AspireTopologyFixtureBase`1 and no TestEnvironmentEndpoint; both CI/release select Standalone.slnx. Give the declared native project an explicit build boundary until its package API is published, without changing pins/workflows. Fixed: both solutions retain explicit native project selection but exclude it from ordinary solution builds. Six solution controls and the full 474-case CI project pass; pins and workflows are unchanged. |
| B2 | medium | defer — outside the stated EventStore/Parties author intent | Tenants source fixtures still override removed bootstrap members; McpCli retains the generic published-package consumer. The approved breaking API migration and epics.md:1520–1541 target EventStore/Parties, while these legacy consumers need their own descriptor port before an SDK publication/cohort qualification. No successful Tenants/McpCli fixture qualification is claimed. |
| B3 | medium | defer — pre-existing workspace privacy policy | CompositionWorkspace.CreatePrivateDirectory already creates Windows directories with inherited ACLs and documents privacy only where supported; it is unchanged by this story. The new private channel relies on that existing owner workspace policy. A broadly readable Windows override can expose runner secrets, so owner ACL enforcement/validation remains required before Windows qualification; this run qualifies Linux source only. |
| B4 | medium | patch | Windows fully-qualified UNC paths pass the reader path check and reach CreateFileW before cancellation. Reject network/device-network forms before any descriptor or sibling credential filesystem access; retain normal absolute local paths and metadata-only refusals. Fixed: shared local-path validation rejects UNC, device-network and slash-normalized forms before opening either file. Full SDK248 and named reader45/credential48 cases pass. |
| B5 | medium | patch | PlatformEnvironmentFixture.Resources omits redis/parties/tcp, although persistence later selects it. A missing Redis binding is discovered after the create command. Include required non-HTTP binding validation during initialization without creating an HTTP client for TCP or exposing any client first. Fixed: the fixture prevalidates redis/parties/tcp among all required tuples before exposing clients and allocates HTTP clients only for HTTP transports. All14 fixture cases and full58 pass. |
| B6 | medium | patch | The selected executor currently requires functional observations only for Memories retention or an existing file. An otherwise passing Parties report with an absent observation file therefore skips persistence-artifact inspection. Require it for the two declared Parties profiles and cover the missing-file outcome. Fixed: both declared Parties profiles require inspected functional observations even when the file is absent. The executing selected-child missing-file controls pass within runner144. |
| B7 | medium | patch | The real Memories GET checks only id, so an inactive registry response satisfies the assertion after preparation once saw Active. Assert the current response is Active and verify an inactive response fails, without replacing the real dependency. Fixed: current Memories responses must contain the expected id and active status. Four assertion controls pass; the final real dependency run is retained separately. |
| B8 | low | patch | Current canonical, Builds and embedded v1 schemas are byte-identical, independently SHA-verified, but SDK tests lack a permanent immutable-byte guard. A direct embedded-resource hash assertion prevents a later SDK packaging edit from silently changing v1, without an external checkout dependency. Fixed: an embedded-resource SHA256 assertion pins the authoritative v1 bytes without an external checkout. Full SDK248 passes. |
| B9 | medium | patch | Ordinary clients reject foreign URI origins but allow request.Headers.Host. A different virtual host on the same listener can receive the bearer. Reject explicit Host overrides before sending, including ordinary authenticated clients; preserve ordinary valid requests. Fixed: ordinary and invocation clients reject explicit Host overrides before any send. Credential no-send cases pass in the full SDK suite. |
| B10 | medium | patch | GetEndpoint resolves the exact tuple, but HTTP client creation checks only URI scheme and ignores the supplied protocol field. A protocol=tcp binding with an HTTP URI is accepted as HTTP. Validate the declared HTTP transport too, preserving generic non-HTTP endpoint selection. Fixed: HTTP client creation checks both the declared protocol and URI transport while generic TCP selection remains available. Reader and fixture transport controls pass. |
| V1 | medium | patch | Pre-verified regression gap: NativeEndpointCredentialTests supplies secrets directly to the report reader; existing executing dapr-secret case exercises retention/access.json. Neither detects removal of the actual executor AddRange(endpointSecrets). Add an executing selected-child case using the new private endpoint channel, with HXT008/no-retained-report assertions. Fixed: an actual selected child consumes the strict private endpoint channel, writes its bare opaque secret inside valid TRX, and reaches a positive execution marker. HXT008 refuses retention with no report bytes/hash; runner144 passes. |
| V2 | medium | patch | Pre-verified regression gap: selected-executor tests leave NativeTestScope null and the real suite supplies only valid scope. Foreign run/module, wildcard or missing synthetic-tenant scope can stop refusing without a negative test detecting it. Exercise those prepared scopes and assert HXT009 with no child launch. Fixed: foreign run/module, domain/tenant wildcards and absent synthetic-tenant scopes each produce HXT009 before child launch; a valid prepared scope demonstrably launches. Runner144 passes. |
| V3 | medium | defer — independent pre-existing host change | The private Platform host installs its 404-for-unmatched-endpoints boundary ahead of Swagger middleware, so Swagger cannot run. This is real, but belongs to preserved independent host/security changes (host commit6346018797bb0b2ecff470cd7aa701923510297a), not this fixture migration; its native flow uses the Builds-owned seed. Reorder middleware and verify human Swagger access in that owner work. |

## Verification

- Build affected projects individually in Debug with `-nodeReuse:false`; run xUnit v3 DLLs using `-class`.
- Run Parties isolated projects and `python3 scripts/test_platform_declaration.py` from P.
- Run declared source suites; retain descriptor/result and persistence observations. Inspect baseline resources before code changes.
- Check schema/packaging boundaries. Package acceptance requires authoritative publications; Debug grants none.

