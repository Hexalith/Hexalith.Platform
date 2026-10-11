---
title: '1.5 Declare EventStore, Memories and Tenants for the reference composition'
type: 'feature'
epic: 1
story: 5
created: '2026-10-11'
status: 'in-progress'
route: 'dispatch'
baseline_commit: '6f703dd0227d8fac023ad97d909c5aaa42115578'
review_loop_iteration: 1
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
- [ ] `references/Hexalith.Memories/module/hexalith.module-manifest.v2.json` — declare the core server and required telemetry services, readiness, roles, four provider exception records and an empty task list; exclude legacy MCP hosting.
- [ ] `references/Hexalith.Tenants/module/hexalith.module-manifest.v2.json` — declare the required API, state/pubsub roles, /ready and an empty task list; keep the UI out of the minimum and do not claim bootstrap completion.
- [ ] `scripts/test-reference-module-declarations.py` — exercise the public v2 validator with the three real files and temporary duplicate-identity, absent-readiness and invalid-exception variants; assert atomic refusal, diagnostic source/field and empty task inventories.
- [ ] `references/Hexalith.Memories/module/hexalith.module-manifest.v2.json` — add a configuration input for the consumer-supplied LLM component YAML path, logical clock/marker signing secrets and tenant embedding credential namespace, and external-effect/destructive-retention worker controls; use current keys where available and mark Story 1.7 binding keys clearly in Design Notes.
- [ ] `references/Hexalith.Memories/src/Hexalith.Memories.AccessTelemetry.Clock/Program.cs` and `references/Hexalith.Memories/src/Hexalith.Memories.AccessTelemetry.Clock/ClockSigningReadinessHealthCheck.cs` — make `/ready` fail when the required clock signing material cannot be loaded; keep `/alive` independent of that dependency.
- [ ] `references/Hexalith.Memories/src/Hexalith.Memories.Server/NaturalLanguage/NaturalLanguageEmbeddingRetryHostedService.cs`, `references/Hexalith.Memories/src/Hexalith.Memories.Server/NaturalLanguage/NaturalLanguageDescriptionOptions.cs`, and `references/Hexalith.Memories/src/Hexalith.Memories.Server/Hosting/MemoriesServerServiceCollectionExtensions.cs` — add a `NaturalLanguage:RetryWorkerEnabled` disable path so the retry worker can be stopped during recovery; declare that key in the Memories worker inventory. Use the existing `AccessTelemetryLifecycle:Enabled` gate for telemetry delivery and retention workers.
- [ ] `references/Hexalith.Tenants/module/hexalith.module-manifest.v2.json` — describe the existing public global-administrator and user-tenants routes and the domain-service `/query`, `/project` and `/replay-state` operations without changing the AppHost.
- [ ] `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Program.cs`, `references/Hexalith.Tenants/src/Hexalith.Tenants.Api/Services/EventStoreGatewayReadinessHealthCheck.cs`, and `references/Hexalith.Tenants/tests/Hexalith.Tenants.IntegrationTests/TenantsApiReadinessTests.cs` — make the required public API's `/ready` depend on the EventStore gateway's `/ready` through Dapr, retain independent `/alive`, and verify both healthy and unavailable gateway responses. Do not treat this as proof of Tenants bootstrap completion.
- [ ] `scripts/test-reference-module-declarations.py`, `references/Hexalith.Memories/tests/Hexalith.Memories.AccessTelemetry.Tests/Clock/ClockSigningReadinessHealthCheckTests.cs`, and `references/Hexalith.Memories/tests/Hexalith.Memories.Server.Tests/NaturalLanguage/NaturalLanguageEmbeddingRetryHostedServiceTests.cs` — assert the added inventory, all required routes, a clock readiness failure when signing material is absent, and retry-worker disable behavior; retain the joint positive and negative validator cases.

**Acceptance Criteria:**
- Given the three declarations, when validated together, then all pass v2 validation with distinct identities.
- Given EventStore and Memories, when dependencies are inspected, then neither invents a domain dependency.
- Given a required server, when inspected, then its app/resource IDs and usable readiness match its owning service.
- Given these declarations, when their task inventories are inspected, then each is empty until its owner supplies an independently executable task with a scoped contract.
- Given Memories' exceptions, when inspected, then the three approved uses are distinct from transitional coordination.
- Given Tenants' AppHost, when this story lands, then it has no new cross-module wiring.

## Implementation Notes

Preserve the first pass's correct server identities, empty executable task lists, three approved Memories provider exceptions plus marked transitional Redis coordination, no enrolled MCP surface, and no Tenants AppHost edits. Preserve the joint validation and duplicate-identity, absent-readiness, unapproved-exception, fifth-exception and unmarked-coordination checks. Bind logical component names in Story 1.7.

Use `Memories:LlmComponentPath` as the extension's consumer-supplied LLM YAML path input, `Clock:SigningKeySecretName` and `AccessTelemetryLifecycle:MarkerKeyReference` for current logical secret names, `Embedding:CredentialNamespace` for tenant-scoped provider credentials, and `NaturalLanguage:RetryWorkerEnabled` for the new worker switch. The embedding namespace and logical Dapr component keys are declaration-stage inputs; Story 1.7 wires their names to running components. `AccessTelemetryLifecycle:Enabled` already gates telemetry delivery and retention.

## Spec Change Log

- 2026-10-11 review loop 1: Review found missing Memories LLM input, secret and worker inventories, Tenants routes, and a clock `/ready` check that can pass without signing material. Expanded Tasks & Acceptance to cover their owning files and verification. Avoid the known-bad state of schema-valid but incomplete declarations and a false-ready clock. KEEP the first pass's validated identities, empty task inventories, provider-exception policy, no MCP enrollment, untouched Tenants AppHost, and working positive/negative validator cases.

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
