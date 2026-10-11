---
title: '1.5 Declare EventStore, Memories and Tenants for the reference composition'
type: 'feature'
epic: 1
story: 5
created: '2026-10-11'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
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
- _bmad-output/planning-artifacts/epics.md: Story 1.5 acceptance at 1016; Story 1.7 binds component names; Story 3.5 and the owner-work table defer synthetic-tenant creation.

## Tasks & Acceptance

**Execution:**
- [ ] `references/Hexalith.EventStore/module/hexalith.module-manifest.v2.json` — declare the gateway, its state/pubsub roles, surfaces, readiness and an empty task list; exclude optional admin/UI from the minimum.
- [ ] `references/Hexalith.Memories/module/hexalith.module-manifest.v2.json` — declare the core server and required telemetry services, readiness, roles, four provider exception records and an empty task list; exclude legacy MCP hosting.
- [ ] `references/Hexalith.Tenants/module/hexalith.module-manifest.v2.json` — declare the required API, state/pubsub roles, /ready and an empty task list; keep the UI out of the minimum and do not claim bootstrap completion.
- [ ] `scripts/test-reference-module-declarations.py` — exercise the public v2 validator with the three real files and temporary duplicate-identity, absent-readiness and invalid-exception variants; assert atomic refusal, diagnostic source/field and empty task inventories.

**Acceptance Criteria:**
- Given the three declarations, when validated together, then all pass v2 validation with distinct identities.
- Given EventStore and Memories, when dependencies are inspected, then neither invents a domain dependency.
- Given a required server, when inspected, then its app/resource IDs and usable readiness match its owning service.
- Given these declarations, when their task inventories are inspected, then each is empty until its owner supplies an independently executable task with a scoped contract.
- Given Memories' exceptions, when inspected, then the three approved uses are distinct from transitional coordination.
- Given Tenants' AppHost, when this story lands, then it has no new cross-module wiring.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

The v2 validator checks that task executable paths exist. Current startup work is largely hosted services, and the Tenants synthetic-tenant task is assigned to Epic 3. The 2026-10-11 decision is to declare empty task lists now, with no redundant /ready wrappers; later owning stories add runnable tasks. Story 1.7 binds logical component keys that current code still hardcodes.

## Verification

**Commands:**
- `python3 scripts/test-reference-module-declarations.py` — joint positive and negative validator cases pass.
- `git -C references/Hexalith.Tenants diff -- src/Hexalith.Tenants.AppHost/Program.cs` — empty.
- `git diff --check` in each changed repository — clean.
