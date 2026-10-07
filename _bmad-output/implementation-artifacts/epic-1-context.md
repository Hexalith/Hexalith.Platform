# Epic 1 Context: Develop a module against its minimum environment

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Enable source development for Parties from its own workspace, using Platform as a direct submodule and exactly its declared real-service minimum environment, and for EventStore, Memories and McpCli from the Platform workspace. Prove that edits and breakpoints reach the active checkout, initialization stays within direct references, and failures identify their cause. All-domain-workspace verification continues later.

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

- Module owners declare one server list for development and integration testing. Domain environments include EventStore, Tenants and Memories plus declared dependencies, excluding unrelated modules. Technical/tool environments introduce no invented domain dependencies. Isolated test doubles remain module-owned.
- Validate declarations before composition. Missing, duplicate, incompatible or unknown fields produce actionable file, field-path and reason diagnostics; errors never yield a partial composition. Required resources need usable readiness evidence, not merely a running process.
- The default startup budget is ten minutes from environment request. Overrides require a finite value and justification; complete environments use the largest declared override. Preserve startup duration, readiness results, effective budget and failure diagnostics. Timeout reports identify unready resources, and definite failure may stop early.
- Demonstrate a breakpoint and source edit in the active checkout. Stop only the owning tool's resources.
- Module declaration changes require owner review. Tenants, Folders and Projects adopt in their own repositories; Platform supplies the enrollment kit.

## Technical Decisions

Platform owns composition and declaration semantics; Builds implements schema, validation and `hexalith-module`. Modules retain behavior, UIs, thin SDK hosts, authorization and readiness. One Platform Aspire model consumes declarations through ordinary C# helpers; Builds' AppHost launches it. Domain AppHosts remain frozen pending qualified retirement. New hosting helpers require ratified ServiceDefaults and Aspire/Dapr packages. Preserve the optional Works preview.

The next major after `hexalith.module-manifest.v1` starts Platform declarations; v1 cannot enroll. Support current and previous Platform majors. Define the complete structure now; later stages add validation rules. The groups cover:

- Identity: module, app and resource identities and enabled servers.
- Runtime: logical Dapr capabilities, recovery classes and configuration keys for bound component names; extension packages and inputs; requests, limits, replicas defaulting to one and volumes.
- Surfaces: logical interfaces, route prefixes, exposure and surface classes, authorization, inbound callers and operations.
- Integration: topics and dead-letter policies, logical secrets and dynamic namespaces, identity, egress, provider tenancy and disable controls for external-effect or destructive-retention workers.
- Lifecycle: readiness, scoped startup tasks and authority classes, recovery/fence hooks, replay/reopening behavior, justified startup overrides, critical flows and smoke surfaces, change classification and recovery inventory.

Platform assigns component names, namespaces, FQDNs and scopes and renders declarative subscriptions. Dapr capabilities bind names through configuration and declare authoritative-restore, rebuild-only or live-authority-only recovery. Provider exceptions are Memories' FalkorDB graph, Redis search/vector indexes and SET NX preflight-dedup row; transitional Redis coordination permits local enrollment. Enrolled hosts cannot map MCP endpoints; MVP MCP uses McpCli stdio.

Startup scopes are per-start verify, once-per-environment creation, recovery and operator-only. Verification is idempotent; creation runs only at initial creation/enrollment; recovery follows dependency order. Hosted authority/population creation is operator-only.

Resolve one active-root build/launch mapping. Initialize only direct `references/*` submodules without recursion or remote updates; initialized nested submodules fail. Source mode uses active/root-declared source in Debug and catalog packages/images elsewhere. Missing source and duplicate source/package identities fail without fallback. Only the pinned tool selects mode.

Package mode uses Release/NuGet artifacts and catalog images. Platform identity is the direct Platform submodule commit, or HEAD inside Platform; it pins the Builds catalog and tool range. Package mode refuses identity/catalog/tool-range or dirty/untagged-submodule mismatches; source mode warns. Builds owns versions; Aspire CLI equals AppHost SDK and EventStore.Aspire/Dapr toolkit pins move together.

## UX & Interaction Patterns

Expose stable run, debug, test and down commands. Failures name dependencies/resources, paths, reasons, applicable deadlines and diagnostics locations. Adoption checks report pass/fail with files. McpCli is a run-scoped local tool.

## Cross-Story Dependencies

Schema, catalog and source/identity mechanisms enable declarations and composition. Dapr conventions and readiness precede the accepted tool and reference adoption. Owner-reviewed EventStore, Memories and Tenants declarations supply Parties' dependencies. Epic 2 adds integration lifecycle; Epic 3 adds extensions, connected McpCli and all-domain-workspace verification.
