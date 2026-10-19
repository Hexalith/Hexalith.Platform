# Epic 1 Context: Develop a module against its minimum environment

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

A Parties developer runs and debugs their checkout from the Parties workspace, with Platform as a direct submodule, against exactly the servers the Parties declaration lists (EventStore, Tenants, Memories) as real services. EventStore, Memories and McpCli developers do the same from the Platform workspace. Breakpoints and source edits reach the running code, only direct references are initialized, and failures name the missing dependency. One declaration-driven Platform composition replaces per-module AppHosts and is the foundation for every later epic. The mechanism is proven on the reference composition (Parties plus the three technical modules); SM-2 evidence across all four domain workspaces closes in Epic 3. This is brownfield work, so there is no scaffold story.

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

- **Minimum environment.** A module declares one server list for both development and testing, and Platform composes exactly that list as real services, with no unrelated modules. A domain module's minimum is EventStore, Tenants and Memories plus its declared servers. Technical modules (EventStore, Memories, McpCli) declare their own minimums and add no invented domain dependency.
- **Declaration schema.** It is the next major of `hexalith.module-manifest`; v1 is never accepted for enrollment. Platform owns the semantics, and Builds implements the schema and validator.
  - The full field structure (Identity, Runtime, Surfaces, Integration, Lifecycle) is defined now. Later epics add rules, never a new major.
  - The current and previous majors are accepted. Each failure names the file, field and reason, and no partial composition is produced.
  - A required server must declare readiness, and a startup override must be finite and justified.
  - Each startup task declares a scope (per-start verify, once-per-environment creation, recovery or operator-only) and an authority class.
  - Each Dapr capability declares a logical role, a recovery class and a configuration key. Literal component names are rejected.
  - An enrolled host that maps an MCP endpoint is rejected.
- **Source mapping.** The active root is resolved once into one mapping that both build and launch use.
  - Only the root's direct `references/*` are initialized, never recursively. Any initialized nested submodule fails the run by name.
  - Root-declared Hexalith dependencies build from source in Debug. Everything else comes as a package or released image at the catalog version, and that copy is never a fallback for source.
  - A source copy and a package copy of the same identity fail the build. Missing source fails, naming the dependency and path, with no fallback.
  - Only the pinned tool's mode property selects the mode; file existence never does.
- **Platform identity.** In any workspace other than Platform, the commit of the direct `references/Hexalith.Platform` submodule is the identity; in the Platform repository it is HEAD. The identity pins the Builds catalog commit and the supported tool range. Package mode refuses a mismatch or an untagged or dirty submodule; source mode only warns.
- **Local tool.** The tool provides `run`, `down`, `test` and `debug`, and `down` stops only what the tool started.
  - Readiness means module-defined readiness plus successful startup tasks. A running process is never readiness.
  - The default deadline is 10 minutes. The environment uses the largest justified override, reported as an override.
  - A timeout names each unready resource, its last result, the effective deadline and where the diagnostics are kept. It never dumps raw logs.
  - Each run records its actual duration, readiness results and effective deadline.
- **Evidence.** A technical module's own-repository AppHost serves only its own tests. Platform evidence comes only from the Platform workspace.
- **Stack pins (no upgrades).**
  - .NET SDK 10.0.401.
  - Aspire 13.5.4. The CLI must equal the AppHost SDK, and the tool checks this.
  - CommunityToolkit Aspire Dapr 13.5.1-beta.757.
  - Hexalith.EventStore.Aspire 3.106.0. Catalog 3.109.0 needs toolkit beta.767 or later, so the two move together.
  - Dapr 1.18.
- **Cross-repository done.** A story tagged with another repository is done only when its change is:
  - merged upstream;
  - released (for tools, as a Platform-accepted version);
  - pinned in Platform;
  - verified from the Platform workspace;
  - reviewed by that module's owner.

  Tenants, Folders and Projects adopt Platform in their own workspaces. Platform supplies the template, validator and guide.

## Technical Decisions

- **One composition model.** One Platform-owned Aspire model consumes declarations through ordinary C# helpers, and no other AppHost composes multi-module environments.
  - Builds.Module.AppHost launches that model.
  - Builds.Module.EventStoreHost becomes the composed-host seed or is removed.
  - UiHost stays a run-scoped host.
  - Domain-module AppHosts are frozen.
- **Works preview.** It stays the rollback composition and is composed only when `Platform:Works:Enabled` is set.
- **EventStore host.** Exactly one `eventstore` app runs, and it is the gateway. Platform composes it from the EventStore server through the source mapping, with zero extension packages; extension packages arrive in Epic 3.
- **AppHost form.** Record the chosen form. A file-based `apphost.cs` cannot use `DistributedApplicationTestingBuilder` through generated `Projects` types.
- **Dapr.** Platform assigns component names and injects them through the declared configuration keys; there are no `const` or attribute literals. It renders declared topics as declarative Subscription objects with dead-letter policy, and compositions contain no programmatic subscriptions.
- **Local security.** Before any resource is added, Platform validates and renders deny-default per-receiver ACLs, explicit grants (caller, path, verbs) and scoped components.
  - mTLS covers the sidecars and Sentry, placement and scheduler. The control plane uses the `localhost` trust domain in the `default` namespace, and ACLs match the SPIFFE trust domain `public`.
  - Fixture assertions do not prove live enforcement.
- **Canonical hosting set.** It is `Hexalith.EventStore.Aspire`, `Hexalith.EventStore.ServiceDefaults` and `CommunityToolkit.Aspire.Hosting.Dapr`. New hosting helpers are refused until this set is recorded.
- **Version authority.** The Builds catalog is the only version authority, including Dapr control-plane images. Remove the literals from `SupportedPlatformPins.cs` and `CompositionToolchainPins.cs`. Tool versions up to 4.27.4 are not accepted, and the accepted version is pinned in `.config/dotnet-tools.json`.
- **Brownfield.**
  - Remove EventStore `RepositoryProjectPaths` probing and the Parties file-existence switches.
  - The Parties ACL file becomes a declaration source.
  - `DaprSelfHostedMtls.cs` fixes its ports and scheduler volume, so concurrent runs stay unsafe until Epic 2.

## Cross-Story Dependencies

- **First shared artifacts.** Every consumer waits on the schema and validator (1.1) and the accepted tool (1.9).
- **Within the epic.**
  - 1.6 consumes 1.5, 1.3 and 1.2.
  - 1.7 binds names in the 1.6 composition.
  - 1.8 builds on 1.3–1.7.
  - 1.9 needs 1.1–1.8 merged.
  - 1.10 uses the declarations from 1.5.
  - 1.12 and 1.13 need 1.9 and 1.1.
- **Later epics.**
  - The blocking CI tier is Story 2.8.
  - Run ownership is Epic 2.
  - Extension packages, connected McpCli and the SM-2 evidence fed by the 1.13 checklist are Epic 3.
  - Epic 4 needs Epic 1.
