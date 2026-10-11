---
title: '1.5 Declare EventStore, Memories and Tenants for the reference composition'
type: 'feature'
epic: 1
story: 5
created: '2026-10-11'
status: 'in-progress'
route: 'dispatch'
baseline_commit: '6f703dd0227d8fac023ad97d909c5aaa42115578'
review_loop_iteration: 2
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** EventStore, Memories and Tenants have no module-owned v2 declarations, so Platform cannot validate their real services and readiness for the reference composition.

**Approach:** Add one declaration per repository from its current services, Dapr roles and health endpoints. Validate the set together.

## Boundaries & Constraints

**Always:** Use hexalith.module-manifest.v2 and complete all five schema groups. EventStore has no domain dependency; Memories and Tenants name only their actual EventStore dependency, with no invented domain dependency. Required servers have usable /ready checks. Every declared Dapr role has a logical capability, recovery class and configuration key. Declare the three approved Memories provider SDK exceptions and its direct Redis coordination separately as transitional. Set lifecycle.tasks to [] in all three declarations: current startup work is in-process, not independently executable or proven complete by readiness. Keep module owners responsible for their declarations and review.

**Never:** Add cross-module wiring to the Tenants AppHost; compose or launch the Platform model (1.6); complete Dapr component-name rebinding (1.7); claim that /ready proves Tenants bootstrap or EventStore admin dependency health; expose a legacy Memories MCP server as an enrolled MCP surface; invent a Tenants synthetic-tenant creation task (3.5 prerequisite); modify the v1 manifest contract.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Joint enrollment | Three v2 files | Validation succeeds with unique identities | No partial declaration set |
| Required service | Each enabled required server | Declares its actual app/resource IDs and usable /ready check | Missing readiness fails with source and field |
| Memories exceptions | Four direct provider uses | Graph, search/vector and SET NX are named approved exceptions; Redis coordination is marked transitional | Any fifth exception or unmarked coordination fails review |
| Minimum environment | EventStore and Memories | No invented domain dependency | Reject accidental domain dependencies |

</frozen-after-approval>

## Code Map

- references/Hexalith.Builds/schemas/hexalith.module-manifest.v2.json and src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs: authoritative shape and validation; do not relax them. test/fixtures/module/platform/valid.json shows shape, not production identities.
- references/Hexalith.EventStore/src/Hexalith.EventStore.AppHost/Program.cs and src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs: gateway, optional admin/UI and Dapr roles. src/Hexalith.EventStore/HealthChecks/HealthCheckBuilderExtensions.cs: gateway /ready checks.
- references/Hexalith.Memories/src/Hexalith.Memories.AppHost/Program.cs and src/Hexalith.Memories.Server/Hosting/MemoriesServerServiceCollectionExtensions.cs: server, telemetry, providers and readiness. src/Hexalith.Memories.Server/Ingestion/WorkflowPayloadStoreOptions.cs has a state-store key; literal names remain for 1.7.
- references/Hexalith.Tenants/src/Hexalith.Tenants.Aspire/HexalithTenantsServerExtensions.cs and src/Hexalith.Tenants/Program.cs: service identity and /ready. src/Hexalith.Tenants.AppHost/Program.cs is frozen; TenantBootstrapHostedService is in-process.
- references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs and Services/EventStoreGatewayReadinessHealthCheck.cs: public API /ready checks the EventStore gateway through its Dapr sidecar; /alive stays independent. This check does not prove Tenants bootstrap completion.
- _bmad-output/planning-artifacts/epics.md: Story 1.5 acceptance at 1016; Story 1.7 binds component names; Story 3.5 and the owner-work table defer synthetic-tenant creation.

## Tasks & Acceptance

**Execution:**
- [ ] `references/Hexalith.EventStore/module/hexalith.module-manifest.v2.json` — declare the gateway, its state/pubsub roles, surfaces, readiness and an empty task list; exclude optional admin/UI from the minimum.
- [ ] `references/Hexalith.Memories/module/hexalith.module-manifest.v2.json` — declare the core server and required telemetry services, readiness, roles, four provider exception records and an empty task list; exclude legacy MCP hosting. Inventory Redis and FalkorDB provider inputs as well as the consumer-supplied LLM YAML path.
- [ ] `references/Hexalith.Tenants/module/hexalith.module-manifest.v2.json` — declare the required API, state/pubsub roles, /ready and an empty task list; keep the UI out of the minimum and do not claim bootstrap completion.
- [ ] `scripts/test-reference-module-declarations.py` — exercise the public v2 validator with the three real files and temporary duplicate-identity, absent-readiness, unapproved and fifth exception, and unmarked coordination variants; assert atomic refusal, diagnostic source/field and empty task inventories.
- [ ] `references/Hexalith.Memories/module/hexalith.module-manifest.v2.json` — inventory both required telemetry component YAML paths (`stateStoreComponentPath`, `configurationStoreComponentPath`), `Clock:SecretStoreName`, clock source configuration (`Clock:Sources` and `Clock:AllowDevelopmentSources`), logical signing secret names, tenant embedding credential namespace, and all external-effect/destructive-retention workers including `AccessTelemetryHeartbeatWorker`. Use configuration sources for secret-name inputs; mark Story 1.7 binding keys in Design Notes.
- [ ] `references/Hexalith.Memories/src/Hexalith.Memories.AccessTelemetry.Clock/Program.cs` and a readiness check — make `/ready` fail if signing material is unavailable or the configured clock sources cannot support attestation; keep `/alive` independent. Test both healthy and failing `/ready` responses.
- [ ] `references/Hexalith.Memories/src/Hexalith.Memories.Server/NaturalLanguage/NaturalLanguageEmbeddingRetryHostedService.cs`, its options and registration, and focused tests — add a `NaturalLanguage:RetryWorkerEnabled` recovery switch; when disabled during an active tick, stop draining and scheduling remaining retry records without losing them. Declare the switch in the worker inventory.
- [ ] `references/Hexalith.Memories/src/Hexalith.Memories.AccessTelemetry/Lifecycle/AccessTelemetryLifecycleActor.cs` and focused tests — make `AccessTelemetryLifecycle:Enabled=false` suppress retention purge even for an already registered actor receiving reminders; preserve records and resume normally when reenabled. Keep this control in the retention worker inventory.
- [ ] `references/Hexalith.Tenants/module/hexalith.module-manifest.v2.json` — describe the existing public global-administrator and user-tenants routes and domain-service `/query`, `/project` and `/replay-state` operations. Inventory the required `daprConfigPath` and API authentication input (`Oidc:Authority` or `Jwt:SigningKey`) without changing the AppHost.
- [ ] `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs`, `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/EventStoreGatewayReadinessHealthCheck.cs`, and `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiReadinessTests.cs` — make the required public API's `/ready` depend on EventStore gateway `/ready` through Dapr, retain independent `/alive`, and verify healthy and unavailable responses plus the configured Dapr endpoint URI authority. Do not treat this as proof of Tenants bootstrap completion.
- [ ] `scripts/test-reference-module-declarations.py` and focused Memories/Tenants tests — assert complete inventory and routes, clock healthy and missing-source/missing-key readiness, live retry disable, telemetry retention disable, and API gateway readiness destination; run every frozen matrix case.

**Acceptance Criteria:**
- Given the three declarations, when validated together, then all pass v2 validation with distinct identities.
- Given EventStore and Memories, when dependencies are inspected, then neither invents a domain dependency.
- Given a required server, when inspected, then its app/resource IDs and usable readiness match its owning service.
- Given these declarations, when their task inventories are inspected, then each is empty until its owner supplies an independently executable task with a scoped contract.
- Given Memories' exceptions, when inspected, then the three approved uses are distinct from transitional coordination.
- Given Tenants' AppHost, when this story lands, then it has no new cross-module wiring.

## Implementation Notes

Preserve the first pass's correct server identities, empty executable task lists, three approved Memories provider exceptions plus marked transitional Redis coordination, no enrolled MCP surface, and no Tenants AppHost edits. Preserve the joint validation and duplicate-identity, absent-readiness, unapproved-exception, fifth-exception and unmarked-coordination checks. Bind logical component names in Story 1.7.

Use `Memories:LlmComponentPath` as the extension's consumer-supplied LLM YAML path input, and name the telemetry extension's required state and configuration component paths. Use `Clock:SecretStoreName`, `Clock:SigningKeySecretName`, `Clock:Sources` and `Clock:AllowDevelopmentSources` for clock provisioning. Use `AccessTelemetryLifecycle:MarkerKeyReference` for the current logical marker secret name, `Embedding:CredentialNamespace` for tenant-scoped provider credentials, and `NaturalLanguage:RetryWorkerEnabled` for retry recovery. The embedding namespace and logical Dapr component keys are declaration-stage inputs; Story 1.7 wires their names to running components. The existing `AccessTelemetryLifecycle:Enabled` gate only prevents hosted-service registration; the actor must also check it before purging when an existing reminder fires.

## Spec Change Log

- 2026-10-11 review loop 1: Review found missing Memories LLM input, secret and worker inventories, Tenants routes, and a clock `/ready` check that can pass without signing material. Expanded Tasks & Acceptance to cover their owning files and verification. Avoid the known-bad state of schema-valid but incomplete declarations and a false-ready clock. KEEP the first pass's validated identities, empty task inventories, provider-exception policy, no MCP enrollment, untouched Tenants AppHost, and working positive/negative validator cases.
- 2026-10-11 review loop 2: Review found missing telemetry YAML paths, clock store/source configuration, heartbeat inventory, Tenants Dapr/auth inputs, and runtime gaps in clock readiness, live retry disable and retention disable. Expanded tasks to require those inputs, guards and healthy/failure-path tests. Avoid the known-bad state where a declaration appears complete but the service cannot start or an already registered actor purges while disabled. KEEP the second pass's validated manifests, distinct identities, empty tasks, corrected configuration sources for secret names, Redis/FalkorDB provider inventory, Tenants routes, joint negative validator, clock signing and Tenants gateway checks, and retry switch foundation; preserve the Tenants AppHost boundary.

## Review Triage Log

| Finding | Verdict and evidence | Route |
| --- | --- | --- |
| B1 submodule gitlinks | false — the owning files are staged in their submodules; no root commit or delivery checkout has been made. Committing a root gitlink alone would be a later packaging error. | reject |
| B2 Memories workflow role | false — `AddDaprWorkflow` registers an in-process workflow runtime, with no named component or configuration key to bind as a Dapr role. | reject |
| B3 Memories LLM input | medium — `AddHexalithMemoriesSearchIndexServer` requires an LLM component YAML path, while the declaration's extension has no input for it; a declaration consumer cannot supply that required path. | bad_spec |
| B4 Memories secrets | medium — embedding credentials are selected by tenant key and the clock reads `Clock:SigningKeySecretName`; the empty secret and dynamic namespace lists hide these provisioning inputs. | bad_spec |
| B5 Memories workers | high — the embedding retry service and telemetry lifecycle can create effects or purge records, while `workers: []` supplies no recovery disable control. | bad_spec |
| B6 Tenants public routes | medium — contract routes include `/api/global-administrators` and `/api/users/{userId}/tenants`; the declaration exposes only `/api/tenants`. | bad_spec |
| B7 Tenants domain routes | medium — `UseEventStoreDomainService` maps `/query` and `/replay-state`, and Program maps `/project`; the declaration names only `/process`. | bad_spec |
| B8 EventStore projection topic | false — the running gateway retains `[Topic]` and `MapSubscribeHandler`; the v2 topic name schema cannot encode the wildcard subscription, and no declarative renderer is changed here. | reject |
| B9 duplicate app/resource tests | false — Builds' `PlatformManifestValidationTests` already checks duplicate app and resource IDs, while this story's script checks joint module identity and positive uniqueness. | reject |
| E1 clock readiness | medium — the clock disables Redis readiness and registers no ready-tagged check; `/ready` can return healthy without signing material. | bad_spec |
| E2 local telemetry ACL | medium — self-hosted Dapr configs explicitly omit invocation ACLs without Sentry/mTLS, so local callers outside the declared list may invoke telemetry routes; this predates the declarations. | defer |
| E3 validator timeout | false — no reachable hang is shown; `subprocess.run` waits for the finite local validator command and a timeout would add an arbitrary failure mode. | reject |
| V1 root test registration | low — ordinary root solution tests do not invoke the new script, but the story's explicit verification command does and passed. A nested Python/dotnet test harness would add complexity beyond this review gate. | reject |
| V2 submodule gitlinks | false — same delivery-stage claim as B1; no root commit has been attempted and the submodule worktrees contain the staged manifests. | reject |
| B2-1 tenants topic spelling | false — schema topic names are logical identifiers and disallow dots; `tenants-events` is the declared logical name, while existing AppHost wiring still publishes `tenants.events` until binding conventions are ratified in 1.7. | reject |
| B2-2 Dapr Conversation role | false — carried B2: the schema has no conversation capability, and the running client uses a provider component rather than a name-bindable declared role. | reject |
| B2-3 Memories LLM name | maybe-false — Aspire names the resource `memories-llm`, but the supplied YAML may define the runtime Dapr name; inspect the rendered component in Story 1.6 to settle whether `NaturalLanguage:DaprComponentName=llm` diverges. Potential medium startup failure. | defer |
| B2-4 telemetry YAML paths | medium — `AddHexalithMemoriesAccessTelemetry` requires state and configuration component paths; the extension input inventory cannot supply either. | bad_spec |
| B2-5 clock secret store | medium — the clock reads `Clock:SecretStoreName`; the manifest names the key but omits the store-selection input needed when the component is renamed. | bad_spec |
| B2-6 clock sources | medium — without three configured sources or the development-source switch, `ClockAttestationService` refuses to construct, while the declaration has neither input. | bad_spec |
| B2-7 clock source readiness | medium — the new check resolves only the key; with no UTC sources `/ready` can be healthy while attestation fails. | bad_spec |
| B2-8 telemetry heartbeat | medium — `AccessTelemetryHeartbeatWorker` sends lease heartbeats when telemetry is enabled, but the worker inventory omits it. | bad_spec |
| B2-9 retry mid-tick disable | high — `TickAsync` checks the switch only at entry; a live disable during a backlog can still dequeue and schedule more workflows. | bad_spec |
| B2-10 Tenants Dapr ACL path | medium — `AddHexalithTenantsServer` requires `daprConfigPath`; the declaration has no input for that path. | bad_spec |
| B2-11 Tenants authentication input | medium — Tenants API throws without OIDC authority or signing key, while its extension input list gives a declaration consumer neither. | bad_spec |
| B2-12 Tenants API helper | false — Story 1.6 may add the declared API project through its ordinary mapped-project helper; an owner-specific `Hexalith.Tenants.Aspire` API method is not required by this declaration. | reject |
| V2-1 clock healthy test | medium — existing tests exercise missing key `/ready` and valid-key attestation separately; a check that always reports unhealthy would pass both. | patch |
| V2-2 live retry disable test | medium — current tests start disabled or call `TickAsync` with disabled options; they cannot catch retries continuing after an enabled tick starts. | bad_spec |
| V2-3 readiness destination test | low — the fake handler checks Dapr headers and path but not URI authority, so a wrong base address could pass; a direct URI assertion fixes this. | patch |
| E2-1 stalled clock lookup | maybe-false — the synchronous Dapr secret resolution is not cancellable by the health-check token; a runtime stall test or SDK timeout evidence is needed to determine whether it can hang. Potential medium readiness delay. | defer |
| E2-2 retry mid-tick disable | high — carried B2-9: the current tick keeps draining after a live disable. | bad_spec |
| E2-3 live re-enable | false — the declared recovery control is applied by rendering/restarting the service; the spec does not promise enabling an unregistered worker without a restart. | reject |
| E2-4 retention disable | high — the telemetry actor remains registered when `AccessTelemetryLifecycle:Enabled=false`, and its reminder calls `PurgeAsync` without an enabled guard. The declared retention disable control is unsafe. | bad_spec |

## Design Notes

The v2 validator checks that task executable paths exist. Current startup work is largely hosted services, and the Tenants synthetic-tenant task is assigned to Epic 3. The 2026-10-11 decision is to declare empty task lists now, with no redundant /ready wrappers; later owning stories add runnable tasks. Story 1.7 binds logical component keys that current code still hardcodes.

Story 1.7 binding keys are `Dapr:StateStoreComponent`, `Dapr:PubSubComponent`, `Dapr:SecretStoreComponent`, `AccessTelemetryLifecycle:StateStoreComponent`, `AccessTelemetryLifecycle:SecretStoreComponent`, `AccessTelemetryLifecycle:ConfigurationComponent`, and `Embedding:CredentialNamespace`. They are declaration-stage inputs in Story 1.5; current services still use literal Dapr component names and tenant credential conventions. `Memories:LlmComponentPath` is the consumer-supplied YAML path input for the Memories Aspire extension.

## Verification

**Commands:**
- `python3 scripts/test-reference-module-declarations.py` — joint positive and negative validator cases pass.
- `dotnet test --project tests/Hexalith.Memories.AccessTelemetry.Tests/Hexalith.Memories.AccessTelemetry.Tests.csproj --no-build --filter-class '*ClockSigningReadinessHealthCheckTests'` in Memories — passes; missing signing material returns 503 on `/ready` and 200 on `/alive`.
- `dotnet test --project tests/Hexalith.Memories.Server.Tests/Hexalith.Memories.Server.Tests.csproj --filter-class '*NaturalLanguageEmbeddingRetryHostedServiceTests'` in Memories — passes; disabled retry worker does not drain backlog.
- `dotnet test --project tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj --no-build --filter-class '*TenantsApiReadinessTests'` in Tenants — passes; API `/ready` follows EventStore gateway readiness through Dapr and `/alive` remains independent.
- `dotnet test --project tests/Hexalith.Tenants.IntegrationTests/Hexalith.Tenants.IntegrationTests.csproj --no-build --filter-class '*TenantsApiStructuralTests'` in Tenants — passes; the platform handler retains Dapr routing-header ownership.
- `git -C references/Hexalith.Tenants diff -- src/Hexalith.Tenants.AppHost/Program.cs` — empty.
- `git diff --check` in each changed repository — clean.
