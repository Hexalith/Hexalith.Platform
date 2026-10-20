# Epic 1 Context: Develop a module against its minimum environment

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Prove declaration-driven local development on Parties with real EventStore, Tenants and Memories. Parties uses its own workspace; EventStore, Memories and McpCli use Platform's workspace. Source edits and breakpoints reach the active checkout, initialization remains direct-only, and failures identify the dependency and cause. Complete evidence across domain workspaces follows in Epic 3.

## Stories

- Story 1.1: Validate module declarations against the Platform declaration schema
- Story 1.2: Make the Builds catalog the single version authority
- Story 1.3: Resolve one active-root source mapping
- Story 1.4: Enforce the Platform identity and support package mode
- Story 1.5: Declare EventStore, Memories and Tenants for the reference composition
- Story 1.6: Compose environments from declarations with one Platform Aspire model
- Story 1.7: Ratify local Dapr conventions and bind component names
- Story 1.8: Run and debug with readiness-aware startup and useful diagnostics
- Story 1.9: Ratify and pin the first Platform-accepted tool version
- Story 1.10: Run and debug EventStore and Memories from the Platform workspace
- Story 1.11: Run and debug McpCli from the Platform workspace
- Story 1.12: Adopt Parties as the reference domain module
- Story 1.13: Publish the domain-module enrollment kit

## Requirements & Constraints

- One module-owned server list serves development and testing using real services. Domain minimums include EventStore, Tenants and Memories; technical minimums need no invented domain dependencies.
- Resolve the active root once for build and launch. Initialize only direct `references/*`, without recursive or remote updates; fail on initialized nested submodules. Direct dependencies use source/Debug; others use catalog packages/images. Missing source or duplicate identities fail with no ancestor, sibling, nested-Platform or package fallback. Only the pinned tool selects mode.
- Platform identity is the direct Platform submodule commit outside Platform, or HEAD inside it; it pins catalog commit and tool range. Package mode rejects mismatches and dirty/untagged Platform submodules; source mode warns. Package mode uses Release/NuGet and released dependency images, with the composition from Platform's release tag outside Platform or HEAD inside it.
- Builds owns versions. Aspire CLI and AppHost SDK agree; EventStore.Aspire and the Dapr toolkit move together. Pin a released, accepted tool. The architecture's .NET `10.0.401` / Aspire `13.5.4` table is a planning snapshot; [accepted evidence](evidence/1-11/README.md) qualifies `10.0.402` / `13.6.1` and tool `4.34.0` at Builds tag commit `5d03f1db9c38db34ecac7fe6048937478ffb82cc`, supported range `[4.34.0, 5.0.0-0)`. Earlier pins are historical; reconcile future enrollment with Platform identity and acceptance evidence.
- Cross-repository completion requires owner review, upstream merge/release, Platform pin/declaration and workspace verification. Debug evidence binds revision, loaded identities, environment, result and diagnostics; model assertions alone cannot prove live behavior.

## Technical Decisions

- Platform owns one ordinary C# Aspire model and no domain behavior. Exactly one `eventstore` gateway starts without extensions here. Works preview remains opt-in. Technical AppHosts support own-repository tests, never Platform evidence.
- Platform owns declaration semantics; Builds implements schema/validation. Reject v1, unknown fields and missing/duplicate/incompatible identities with file, field and reason, without partial composition. Support current/previous Platform-capable majors. Define Identity, Runtime, Surfaces, Integration and Lifecycle together; later gates add rules without another major.
- Declare readiness, finite justified startup overrides and tasks with scope/authority. Scopes distinguish per-start verification, once-per-environment creation, recovery and operator-only work; creation never reruns on upgrade/rollback/restore. Dapr roles declare recovery class and configuration key. Platform assigns component names/scopes; module ACL files remain authoritative inputs.
- Use canonical EventStore.Aspire, EventStore.ServiceDefaults and CommunityToolkit Aspire Dapr, with Commons primitives. Require local mTLS, deny-by-default receiver ACLs and scoped components. Inject names through declared keys; render declarative subscriptions with explicit delivery routes and dead-letter policy, disabling programmatic discovery. Validate security before startup.
- Use Dapr/provider-neutral contracts except Memories' named graph, search/vector and preflight-dedup adapters; transitional Redis coordination does not block enrollment. McpCli is run-scoped, unpublished and qualified offline here. Stdio alone serves MVP MCP; enrolled MCP hosts and proprietary legacy surfaces are excluded.

## UX & Interaction Patterns

`run`/`debug` wait for module readiness and required tasks. The request starts a ten-minute deadline unless the largest justified finite override applies. Record duration, results and deadline. Failure names unready resources, last results and preserved diagnostics. `down` stops only tool-owned resources.

## Cross-Story Dependencies

Foundation and accepted release precede adoption. Parties proves the mechanism; the kit supports owner-led Tenants/Folders/Projects adoption. Validation reports each requirement's pass/fail, file and reason. Checklists preserve breakpoint, edit and direct-only initialization evidence for Epic 3's SM-2 record. Inventory domain AppHost/Aspire/ServiceDefaults projects as frozen, without new cross-module wiring; retire after producer adoption, owner-approved source/package/deployed parity and applicable EventStore consumer-removal authority. Epic 2 adds test lifecycle; Epic 3 adds extensions and connected McpCli.
