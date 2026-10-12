# Epic 1 Context: Develop a module against its minimum environment

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

The Parties developer runs and debugs their checkout from the Parties workspace, with Platform as a direct submodule, against exactly the servers Parties declares (EventStore, Tenants and Memories) as real services; EventStore, Memories and McpCli developers do the same from the Platform workspace. Edits and breakpoints reach the running code, only direct references are initialized, and failures say what is missing or unready and why. It ships what every later consumer waits on (the next declaration-schema major and the first Platform-accepted `hexalith-module`) and proves SM-2 on the reference composition; Epic 3 extends it to all domain workspaces.

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

- One owner-declared server list serves development and integration testing. Domain environments add EventStore, Tenants and Memories and exclude unrelated modules; EventStore, Memories and McpCli get no invented domain dependency.
- Validate before composing: missing, duplicate, incompatible or unknown fields report file, field path and reason, never a partial composition.
- Required servers need usable readiness checks; a running process is not ready. Declare only independently executable startup tasks, with scope and authority class; an empty list is valid. In-process hosted services are not tasks, and readiness alone never claims them complete.
- Startup deadline defaults to 10 minutes from the start request. Overrides need a finite value and justification; the complete environment uses the largest, reported as an override. Record duration, readiness results and effective deadline; a raised timeout is never an improvement.
- SM-2 evidence: breakpoint and edit in the active checkout, nested references uninitialized, recorded with revision, environment, result and diagnostics. A technical module's own AppHost serves only its own tests, never Platform evidence.
- Stories in other repositories need owner review and are done only when merged, released (tools as an accepted version), pinned in Platform and verified from the Platform workspace. Tenants, Folders and Projects adopt on their own.

## Technical Decisions

- **Ownership:** Platform owns declaration and composition semantics; Builds implements schema, validator and `hexalith-module`, and its catalog is the sole version authority.
- **Schema:** the next major after `hexalith.module-manifest.v1` (v1 never enrolls); current and previous majors are accepted. All Identity, Runtime, Surfaces, Integration and Lifecycle fields are defined now; later epics add rules, not majors. Platform assigns component names, namespaces, FQDNs and scopes; files such as the Parties ACL are declaration sources. Task scopes: per-start verify (idempotent), once-per-environment creation (never rerun on upgrade, rollback or restore), recovery (dependency order), operator-only.
- **Composition:** one Platform Aspire model consumes declarations via ordinary C# helpers; Builds.Module.AppHost launches it. Exactly one `eventstore` app runs, with no extension packages yet. Domain AppHosts are frozen. The Works preview runs only when `Platform:Works:Enabled` is set and remains the rollback composition.
- **Source mapping:** resolve the active root once into one mapping for build and launch. Initialize only direct `references/*`, never recursively or by remote update; an initialized nested submodule fails by path. Source mode builds root-declared Hexalith dependencies in Debug; others use catalog packages or images, never as fallback. Missing source or duplicate source/package identities fail by name. Only the pinned tool selects the mode, never file existence.
- **Platform identity:** the direct `references/Hexalith.Platform` commit (HEAD inside Platform) pins the Builds catalog and tool range. Package mode uses Release, NuGet and released images and refuses identity, catalog, tool-range or dirty/untagged mismatches; source mode warns. Aspire CLI must equal the AppHost SDK; EventStore.Aspire and the Dapr toolkit move together. Tool versions through 4.27.4 are not accepted.
- **Dapr:** Platform owns local mTLS, per-receiver access control and scoped components. Capabilities declare logical role, recovery class (authoritative-restore, rebuild-only, live-authority-only) and the configuration key binding the name; no literal names. Declared topics get Platform-rendered declarative subscriptions with dead-letter policy. Only Memories' FalkorDB graph, Redis search/vector indexes and SET NX dedup row may use provider SDKs; its other Redis use is transitional. New hosting helpers wait for the canonical ServiceDefaults and Aspire Dapr package set.
- **MCP:** enrolled hosts never map MCP endpoints; McpCli stdio is the only MVP MCP surface, built as a run-scoped, never-published tool for offline inspection here.

## UX & Interaction Patterns

No product UI. Operators use `hexalith-module` `run`, `debug`, `test` and `down` (stopping only what it started). Failures name the unready resource or missing dependency, reason, deadline and diagnostics location, not a log dump. Adoption checks report pass or fail with file and reason.

## Cross-Story Dependencies

- Schema, catalog, mapping and identity (1.1–1.4) precede declarations (1.5) and the model (1.6); 1.5 supplies the environments for 1.10 and 1.12.
- The accepted tool (1.9) needs 1.1–1.8 merged; 1.12 and 1.13 pin it, and Parties' name binding and subscriptions rely on 1.7.
- Epic 2 adds run-scoped resources (fixed Dapr ports block concurrent runs) and the blocking CI tier; Epic 3 adds extensions, connected McpCli and SM-2 across all workspaces, fed by kit checklists; Epic 11 retires the Works preview and frozen AppHosts.
