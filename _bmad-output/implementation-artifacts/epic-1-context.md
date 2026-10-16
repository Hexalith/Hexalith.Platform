# Epic 1 Context: Develop a module against its minimum environment

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

A Parties developer runs and debugs their checkout from the Parties workspace, with Platform as a direct submodule, against exactly the servers the Parties declaration lists (EventStore, Tenants, Memories) as real services. EventStore, Memories and McpCli developers do the same from the Platform workspace. Breakpoints and source edits reach the running code, only direct references are initialized, and failures name the missing dependency. The mechanism is proven on the reference composition (Parties plus the three technical modules); SM-2 evidence across all four domain workspaces closes in Epic 3. Brownfield: there is no scaffold story.

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

- A module declares its own minimum server list (one list for dev and integration testing); Platform composes exactly that as real services. A domain module's minimum includes EventStore, Tenants and Memories plus declared extras. Technical modules (EventStore, Memories, McpCli) use owner-declared environments with no invented domain dependencies.
- Missing, duplicate or incompatible declarations fail validation. Declaration schema is the next major of the module manifest (v1 never accepted for enrollment); Platform owns semantics, Builds implements the validator.
- Dependency initialization follows only the active root's direct `references/` entries; nested submodules stay uninitialized and the tool fails naming any that are initialized.
- Active module and root-declared Hexalith dependencies run from source in Debug; everything else is a NuGet package or released image at the Builds catalog version, never as a fallback. A source and package copy of one identity fails the build. Missing required source fails naming dependency and path; no sibling, ancestor, nested or package fallback.
- Only the pinned `hexalith-module` tool (in `.config/dotnet-tools.json`) selects mode; file-existence switches are removed.
- Platform identity is the `references/Hexalith.Platform` submodule commit (HEAD in the Platform repo). Package mode refuses composition, catalog or tool-range mismatches; source mode warns.
- Startup: tool offers `run`, `down`, `test`, `debug`; waits on module-defined readiness (a running process is not readiness) and startup tasks; default 10-minute deadline, justified finite override allowed; a timeout names which resources are unready and why; actual duration and effective deadline are recorded.
- Stack pins: .NET SDK 10.0.401, Aspire 13.5.4 (Aspire CLI must equal AppHost SDK, checked by the tool), CommunityToolkit Aspire Dapr 13.5.1-beta.757, Hexalith.EventStore.Aspire 3.106.0 (catalog 3.109.0 needs toolkit beta.767+; align together), Dapr runtime 1.18.
- Epic 1 scope tagged per repository and owner-reviewed: Parties adoption and the EventStore, Tenants, Memories declarations. Tenants/Folders/Projects workspace adoption is module-owned; Platform supplies template, validator and guide.

## Technical Decisions

- One Platform-owned Aspire application model consumes declarations through ordinary C# helpers; no other AppHost composes multi-module environments. The Works preview stays the Development-only rollback composition until its parity gate.
- Active workspace root resolved once into a single mapping consumed by both build and launch.
- Brownfield facts: Builds hard-codes versions in `SupportedPlatformPins.cs` (catalog must become sole authority); `hexalith-module` 4.20.0-4.27.4 exist on NuGet but are not Platform-accepted; EventStore `RepositoryProjectPaths` probes sibling/ancestor paths; Parties `Directory.Build.props` uses file-existence switches; `DaprSelfHostedMtls.cs` fixes ports 50001/51005/51006 and a shared scheduler volume (concurrent runs unsafe; run-scoping is Epic 2); file-based AppHosts cannot use `DistributedApplicationTestingBuilder` via generated `Projects` types.
- Declaration fields cover identity, Dapr capabilities by logical role (with recovery class and config key), extension packages, resources, surfaces, topics with dead-letter, secrets, readiness, startup tasks (scopes: per-start verify, once-per-environment, recovery, operator-only), and startup override with justification.
- Dapr: modules address components by logical role and Platform binds names through the declared configuration key (no `const`/attribute literal component names). Declared-topic subscriptions are rendered declaratively by Platform. Canonical hosting set: `Hexalith.EventStore.Aspire`, `Hexalith.EventStore.ServiceDefaults`, `CommunityToolkit.Aspire.Hosting.Dapr`; refuse new hosting helpers until recorded. Local mTLS, deny-default per-receiver ACLs and scoped component files are validated/rendered before resources are added. Builds owns all versions including Dapr sentry/placement/scheduler images.
- Baseline composition runs exactly one `eventstore` app with zero extension packages.
- First Platform-accepted tool version: catalog is the one version authority, Builds.Module.AppHost launches the Platform model, package mode, identity checks, direct-only init and debug entry point implemented (blocking CI tier follows in Story 2.8).

## Cross-Story Dependencies

- Epic opens with the first shared artifacts (accepted `hexalith-module` version and next-major schema/validator); every consumer waits on them.
- 1.6 consumes declarations from 1.5, the source mapping from 1.3 and catalog pairs from 1.2; 1.7 binds names in the 1.6 composition; 1.8 builds on 1.3-1.7; 1.10 and 1.11 rely on 1.5/1.6; 1.12 and 1.13 need the accepted tool (1.9) and validator (1.1).
- Cross-repo work (Builds, EventStore, Tenants, Memories, Parties, Commons) requires the owning module's review.
- Epic 2 builds on this (run ownership, run-scoped resources); Epic 3 closes SM-2 across all four domain workspaces; Epic 4 needs Epic 1.
