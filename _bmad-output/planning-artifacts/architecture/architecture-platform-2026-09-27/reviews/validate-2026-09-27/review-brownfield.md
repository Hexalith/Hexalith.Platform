# Review — Brownfield code ratification
Verdict: PASS WITH FINDINGS — The spine names the main AD-1/AD-4 reconciliation in general terms and its Stack pins match the working tree. It says nothing about one direct AD-9 contradiction in an MVP module, Memories' direct Redis SDK usage. It also leaves several existing conventions unratified or under-described, so builders will see code and spine disagree.

Scope: Platform working tree on 2026-09-27, including the uncommitted `apphost.cs`/`global.json`/`README.md` edits, the untracked `DaprSelfHostedMtls.cs` and `DaprComponents/`, and the staged `.gitmodules`/`references/*`. Also covered: the MVP submodules under `references/` and `/home/administrator/projects/hexalith/mcpcli`. Spine line numbers refer to `ARCHITECTURE-SPINE.md`.

## Code conventions observed

| Convention (with file evidence) | Spine position | Class |
| --- | --- | --- |
| Platform owns a file-based Aspire root: `apphost.cs`, `aspire.config.json`. `eng/verify-agents-host.sh` fails if Agents hosting projects appear | AD-1 (L52), Design Paradigm (L25) | ratified |
| The Works lane resolves the undeclared sibling `../works` and the nested `works/references/Hexalith.EventStore` (`apphost.cs` L28-33). This happens even though `references/Hexalith.Works` and `references/Hexalith.EventStore` are now staged in `.gitmodules` | AD-4 (L70). Deferred row L233 mentions "sibling/nested" | contradicted+migration named (generic) |
| `#:package Hexalith.EventStore.Aspire@3.106.0` in the local AppHost, while `references/Hexalith.EventStore` is root-declared | Listed as a Stack seed (L176), with no AD-4 caveat | contradicted silently (BRN-4) |
| Debug mode defaults to NuGet in EventStore, Tenants, Parties and Memories. Examples: `Hexalith.Parties/Directory.Build.props` L22 (`Configuration=='Debug'` → `UseHexalithProjectReferences=false`), `Hexalith.Memories/Directory.Build.props` L39-45 | AD-4 requires "Debug/project assets". Deferred calls these "package-fallback helpers" | contradicted+migration named, but mischaracterized (BRN-3) |
| Debug mode defaults to project references in Folders, Projects, Agents and Works, with a CI/`GITHUB_ACTIONS` guard. Examples: `Hexalith.Folders/Directory.Build.props` L20-23, `Hexalith.Projects/Directory.Build.props` L36-38. FrontComposer's `deps.local.props` reads only root-declared `references/*` | AD-4 | ratified (not cited as the reference pattern) |
| Every module probes ancestor and sibling source locations (`..\Hexalith.X`, `..\references\Hexalith.X`, `..\..`). So does `Hexalith.EventStore.Aspire/RepositoryProjectPaths.cs` (candidates L65-88) | AD-4 "no ancestor/sibling/recursive" | contradicted+migration named ("ancestor" is not in the Deferred wording) |
| CI initializes only root-declared submodules and builds Release/NuGet (`Hexalith.Builds/.github/workflows/domain-ci.yml` L318-349, L653-670) | AD-4/AD-5 | ratified |
| The CI Aspire tier is advisory by default (`aspire-continue-on-error: true`, domain-ci.yml L109-112) and runs each module's own Aspire test project | AD-5 "reuse Builds workflows" | unaddressed (BRN-11) |
| Domain modules own AppHost, `*.Aspire` or `*.ServiceDefaults` projects: Tenants (AppHost, Aspire), Parties (AppHost), Folders (AppHost, Aspire [packable], ServiceDefaults), Projects (AppHost, Aspire, ServiceDefaults) | Design Paradigm L25. Migration convention L132 ("legacy module hosting") | contradicted+migration named (generic, not enumerated; BRN-5) |
| Technical-module AppHosts: EventStore (used by its integration tests via `CreateAsync<Projects.Hexalith_EventStore_AppHost>`) and Memories | "Technical modules may own reusable hosting capabilities" | ambiguous (BRN-5) |
| Three competing shared ServiceDefaults conventions: Tenants uses EventStore.ServiceDefaults, Parties uses Commons.ServiceDefaults, and Folders/Projects/Memories/Works each have their own. Aspire Dapr helpers are duplicated between `Hexalith.Commons.Aspire` and `Hexalith.EventStore.Aspire` | "Use shared technical-module health/telemetry facilities" (L131) | unaddressed (BRN-6) |
| Shared fixtures live in `EventStore.Testing.Integration` (`AspireTopologyFixtureBase<TAppHost>`, `DaprDomainServiceTestFixtureBase`) | Local tool convention (L129) | ratified as owner. The fixture contract contradicts the spine (BRN-7) |
| Local Dapr uses the `dapr init` Redis at `localhost:6379` as a state store and pub/sub not owned by the run (`DaprComponents/statestore.yaml`, `pubsub.yaml`; `HexalithEventStoreExtensions.cs` L157-159) | AD-10 "fresh run-owned … isolate data" | contradicted silently (BRN-7) |
| Fixed Dapr control-plane ports 50001/51005/51006 and the shared volume `hexalith-platform-dapr-scheduler` (`DaprSelfHostedMtls.cs` L11-16) | Deferred L233 | contradicted+migration named |
| Local Dapr runs with full mTLS: a self-hosted Sentry, a cert directory forced outside the repo, and `ExcludeFromManifest`. Each receiver has its own `accesscontrol.<app>.yaml`, and pub/sub publishing and subscription are scoped per app with `protectedTopics`. Dead-letter topics are `deadletter.<topic>` and `commanddeadletter.<topic>`. A separate `eventstore-operations` app handles poison-message capture and replay | Secrets (default-deny, TLS), "Dapr authenticated channels" (Deferred L238), "durable poison capture and replay" (L126) | partly ratified. The local mTLS/ACL convention is unaddressed (BRN-12) |
| Single `eventstore` app identity. Registrations are dev-only config overrides (`EventStore__DomainServices__Registrations__*`, `apphost.cs` L98-105), and `eventstore-admin` and `eventstore-operations` are separate identities | Shared host and catalogs (L125) | ratified |
| Tenants projections go through the SDK `IReadModelStore` (`Tenants/Projections/TenantProjectionHandler.cs`) | AD-9 | ratified |
| Projects and Parties persist domain data with raw `DaprClient` state calls | AD-9 "not raw Dapr state calls" | contradicted silently (BRN-2) |
| Memories calls Redis directly: StackExchange.Redis and NRedisStack are imported by 92 files (89 in Server) and used in `Memories.EventStore/RedisPreflightDedupStore.cs`. FalkorDB usage is spread across Server as well | AD-9 accepts only "Memories FalkorDB" | contradicted silently (BRN-1) |
| Modules own production deploy manifests: Memories Kustomize base/overlays and OpenBao values; Folders `deploy/dapr/production/*` patches the `hexalith-eventstore` and `hexalith-tenants` Deployments; Tenants and EventStore `deploy/dapr/*` | AD-2 (one Platform Helm package), Production profile authority (L128) | unaddressed (BRN-8) |
| Modules release through semantic-release: NuGet packages plus .NET SDK container images to the Zot registry (`domain-release.yml` L61-66, L391-471) | AD-2 "module publication authority remains mandatory" | ratified |
| `deploy/dapr/eventstore-routing-catalog.json`, `production-profile.yaml` and `openbao-secret-contract.yaml` | Platform-owned target paths (L125-128) | absent in every repo. No routing-catalog code exists in EventStore. Platform has no `deploy/` folder (BRN-13) |
| `.config/dotnet-tools.json` holds only unrelated tools (EventStore: admin CLI 3.82.0; Parties: aspirate 9.1.0). No Platform tool exists | Pinned Platform .NET tool (L129) | target. The gap is covered by Deferred L232 |
| The Agents EXT-HOST-1 contract (`README.md`, `docs/ext-host-1-agents-composition.md`) and the Works AD-20 R1-R11 gate (`README.md`, `apphost.cs` L42-45) | Not mentioned | unaddressed (BRN-9) |

## Findings

### BRN-1 — Memories' direct Redis SDK usage is neither an accepted AD-9 exception nor a named migration
- Severity: high
- Where: AD-9 (L100); Release and Recovery Acceptance, Memories erasure row (L149); Deferred "Memories recovery continuity and implementation gaps" (L240)
- Finding: AD-9 accepts only "Memories FalkorDB". In Memories code, however, Redis search/vector, dedup and coordination use StackExchange.Redis and NRedisStack directly, well beyond the adapter project. The memlog constraint (".. existing finite direct-Redis exception registry must be reconciled individually by capability; do not treat the FalkorDB clarification as blanket approval") never made it into the spine. The erasure row's "Shared Redis/FalkorDB projections" reads as if Redis projections are already sanctioned. The Deferred row's "adapter boundaries" does not say that Redis is currently unaccepted. Memories belongs to every domain module's minimum composition (L25), so this affects every enrollment.
- Evidence: `references/Hexalith.Memories/src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj` L56-58 (`NFalkorDB`, `NRedisStack`, `StackExchange.Redis`); `Hexalith.Memories.EventStore/Hexalith.Memories.EventStore.csproj` L34 (`StackExchange.Redis`); `Hexalith.Memories.EventStore/RedisPreflightDedupStore.cs` L11. 89 Server `.cs` files import `StackExchange.Redis` or `NRedisStack`, including Activities, Endpoints, Search, Tenants and Consistency. FalkorDB usage appears across Server/Activities (18 files), Graph and Workflows, not only in `Hexalith.Memories.Redis`.
- Failure scenario: A Memories builder reads "FalkorDB accepted" and "Redis/FalkorDB projections" and treats direct Redis as sanctioned. They then keep adding NRedisStack calls. Alternatively, a Platform validator enforces AD-9 literally and blocks Memories enrollment with no documented path forward.
- Recommendation: Add to AD-9 or to the Deferred Memories row: "Memories direct Redis (search/vector, dedup, coordination) is not an accepted exception. Before enrollment qualification, reconcile each direct-Redis registry entry by capability: either a named exception with owner and surface, or Dapr. Also confine FalkorDB SDK and native query usage to the adapter/composition boundary."
- Suggested disposition: autofix

### BRN-2 — Domain modules persist domain data through raw Dapr state, and the spine does not say so
- Severity: medium
- Where: AD-9 (L100); Domain truth and delivery (L126)
- Finding: AD-9 says domain persistence goes through EventStore SDK contracts, "not raw Dapr state calls". Projects implements its own `DaprClient` projection store instead of the SDK `IReadModelStore` that Tenants uses. Parties stores erasure certificates, verification reports, tenant-key rotation progress, key-operation audits and search mappings in raw `statestore` keys. The spine also says "General state/coordination … retain Dapr boundaries", which leaves it unclear whether erasure, key-custody and audit records count as domain persistence or as general state. No migration is named.
- Evidence: `references/Hexalith.Projects/src/Hexalith.Projects.Infrastructure/DaprProjectsStateStore.cs` (`SaveStateAsync`/`TrySaveStateAsync`), used by `DaprProjectProjectionStore.cs` and `DaprProjectTenantAccessProjectionStore.cs`. `references/Hexalith.Parties/src/Hexalith.Parties.Security/PartyErasureRecordStore.cs` L12-39, `KeyOperationAuditService.cs` L22-52, `TenantKeyRotationService.cs` L239-255, `Hexalith.Parties/Search/PartyMemoryUnitMappingStore.cs` L152-278. The ecosystem baseline `references/Hexalith.AI.Tools/hexalith-llm-instructions.md` L124-134 requires the `IReadModelStore` seams.
- Failure scenario: Validators and reviewers apply AD-9 inconsistently. Recovery inventory (AD-12) misses raw-state domain records, such as erasure certificates, that sit outside EventStore streams.
- Recommendation: Either classify erasure, key-custody and audit records explicitly as permitted Dapr general state with module-owned recovery inventory, or add a Deferred row migrating Projects projections and Parties records to EventStore SDK seams. Name both modules.
- Suggested disposition: discuss

### BRN-3 — AD-4's Debug/project rule inverts the existing default in four MVP modules; "package-fallback helpers" understates this
- Severity: medium
- Where: AD-4 (L70); Deferred "Source/package adoption" (L233)
- Finding: EventStore, Tenants, Parties and Memories deliberately default to NuGet even in Debug. Their own documented rationale: CI restores without a configuration and then builds Release. So this is a default mode, not a fallback. Folders, Projects, Agents and Works already follow AD-4, with a CI guard. FrontComposer's `deps.local.props` reads root-declared `references/*` only, which is AD-4's intended shape. The spine names neither the existing mode switch (`UseHexalithProjectReferences`/`UseNuGetDeps`/`Hexalith<X>FromSource`/`Hexalith<X>Root`) nor the ancestor (`..\..`) probes. Some per-dependency `FromSource` flags are inferred from file existence regardless of mode (Parties L27/L30), which silently mixes source and package builds.
- Evidence: `references/Hexalith.Parties/Directory.Build.props` L4-34 (L22: Debug → false); `Hexalith.Memories/Directory.Build.props` L36-45; `Hexalith.Tenants/Directory.Build.props` L48-58; `Hexalith.EventStore/Directory.Build.props` L44-53; `Hexalith.Folders/Directory.Build.props` L15-26; `Hexalith.Projects/Directory.Build.props` L36-43; `Hexalith.FrontComposer/deps.local.props`.
- Failure scenario: A builder flips the Debug default to satisfy AD-4 without the CI guard. Plain `dotnet restore`/`build -c Release` in CI then resolves project graphs, which reintroduces the documented restore mismatch. Alternatively, builders treat "fallback" as a narrow edge case and leave the defaults alone.
- Recommendation: Reword the Deferred row. It should name the inverted Debug defaults in these four modules and require CI to set the mode explicitly (as Folders, Projects and Agents do). It should add ancestor probes and existence-inferred per-dependency switches, and either ratify `UseHexalithProjectReferences` as the single mode selector or name its replacement. Cite FrontComposer `deps.local.props` as the reference pattern.
- Suggested disposition: autofix

### BRN-4 — The Stack table ratifies a Hexalith package pin in the local AppHost against AD-4, and the Works lane ignores the newly declared references
- Severity: medium
- Where: Stack (L176); Structural Seed (L207); Deferred (L233)
- Finding: The local Platform AppHost pulls `Hexalith.EventStore.Aspire` from NuGet 3.106.0 even though `references/Hexalith.EventStore` is root-declared. Meanwhile the EventStore server, admin and operations projects launch from a nested sibling checkout at an arbitrary commit, so the helper and the runtime can drift apart. The EventStore helper's own contract expects the consuming AppHost to force a Debug build through a project reference (`EventStorePlatformProjectMetadata.cs`). The staged `.gitmodules` now declares `references/Hexalith.Works` and `references/Hexalith.EventStore`, but `apphost.cs` still hard-codes `../works` and `works/references/Hexalith.EventStore`. This is the hard-coded, only resolution path, not a "helper" fallback.
- Evidence: `apphost.cs` L7 (`#:package Hexalith.EventStore.Aspire@3.106.0`), L28-33; `.gitmodules` (Works, EventStore staged); `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/EventStorePlatformProjectMetadata.cs` class summary.
- Failure scenario: The Stack table is read as the approved local seed and gets copied. A helper API change in EventStore source is never exercised locally, or it breaks against the 3.106.0 helper.
- Recommendation: Annotate the Stack row: "CI/Release pin. The AD-4 local mode resolves EventStore.Aspire from `references/Hexalith.EventStore` (for example `#:project`)." In Deferred, name the Works lane's switch from `../works` and nested EventStore to the root-declared references.
- Suggested disposition: autofix

### BRN-5 — The AD-1 hosting prohibition is not enumerated, and technical-module AppHosts and domain `*.Aspire` helpers are ambiguous
- Severity: medium
- Where: Design Paradigm (L25); AD-1 (L52); Migration (L132); Deferred
- Finding: The spine's migration convention covers retiring "legacy module hosting" in general, but no Deferred row owns the inventory. Existing projects:
  - MVP domain modules: Tenants (AppHost, Aspire), Parties (AppHost), Folders (AppHost, packable Aspire, ServiceDefaults), Projects (AppHost, Aspire, ServiceDefaults).
  - MVP technical modules: EventStore (AppHost, Aspire, ServiceDefaults), Memories (AppHost, packable Aspire, ServiceDefaults).
  - Declared non-MVP modules: Works (AppHost, ServiceDefaults), Timesheets (AppHost, ServiceDefaults), ChatBot, Conversations and FrontComposer (AppHost).
  - Agents and McpCli are clean.

  The spine does not say whether technical-module AppHosts remain valid roots for AD-5's "isolated tests without Platform". The EventStore integration tests depend on `Projects.Hexalith_EventStore_AppHost`. Some domain `*.Aspire` helpers also encode functional wiring that module declarations must absorb, for example `FoldersAspireModule.WithFoldersDomainEventTopicOverride` and `WithFoldersMemoriesSourceRouting`.
- Evidence: `references/Hexalith.*/src/*.{AppHost,Aspire,ServiceDefaults}`; `Hexalith.Folders.Aspire/FoldersAspireModule.cs` L82, L109, L140; `Hexalith.EventStore/tests/Hexalith.EventStore.IntegrationTests/Fixtures/*.cs` (`CreateAsync<Projects.Hexalith_EventStore_AppHost>`); `references/Hexalith.AI.Tools/hexalith-llm-instructions.md` L131-134.
- Failure scenario: Module teams delete their AppHosts or `*.Aspire` packages too early and lose functional overrides. Or they keep adding to them because nothing tracks the migration. EventStore's isolated test tier is broken if its AppHost is treated as legacy.
- Recommendation: Add a Deferred row "Module hosting inventory and retirement" that lists the projects above. State that technical-module AppHosts are permitted as isolated-test roots, or name their replacement. Require the enrollment declaration to express functional topic and route overrides before any `*.Aspire` helper is retired.
- Suggested disposition: autofix (row); discuss (technical AppHost status)

### BRN-6 — "Shared health/telemetry facilities" is unnamed while the code has three competing conventions
- Severity: medium
- Where: Diagnostics and notification (L131)
- Finding: Tenants uses `Hexalith.EventStore.ServiceDefaults` (`AddServiceDefaults`). Parties uses `Hexalith.Commons.ServiceDefaults` (`AddHexalithServiceDefaults`). Folders, Projects, Memories and Works each have their own ServiceDefaults. `Hexalith.Commons.Aspire` duplicates EventStore.Aspire's `AspireDaprDomainModule*`, `AspireDaprLocalServiceEndpoints` and `AspireDaprSharedComponents`. Parties re-implements `DaprStateStoreHealthCheck`. Separately, the Works boundary record (AD-20) assigns ServiceDefaults, health/telemetry and projection/query plumbing to Platform itself, which contradicts the spine's technical-module ownership.
- Evidence: `grep` of module csproj references (Tenants → `Hexalith.EventStore.ServiceDefaults`; Parties → `Hexalith.Commons.ServiceDefaults`); `references/Hexalith.Commons/src/libraries/Hexalith.Commons.Aspire/*`; `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/AspireDapr*.cs`; `references/Hexalith.Parties/src/Hexalith.Parties/HealthChecks/DaprStateStoreHealthCheck.cs`; `references/Hexalith.Works/docs/boundary-decision-record.md` L99-105.
- Failure scenario: Each migrating module picks a different "shared" facility. Readiness and telemetry semantics diverge, which undermines AD-10 readiness and evidence binding.
- Recommendation: Name the canonical owner package for ServiceDefaults/health/telemetry and for the Aspire Dapr helpers, or record the choice as a Deferred decision with an owner. State that the Platform spine's ownership split supersedes Works AD-20's list.
- Suggested disposition: discuss

### BRN-7 — Local lifecycle and fixture conventions contradict AD-10 beyond "fixed Dapr ports/shared volumes"
- Severity: medium
- Where: AD-10 (L106); Local tool and readiness (L129); Deferred (L233)
- Finding: The Deferred row lists only fixed Dapr ports and shared volumes. Four other existing conventions also break run ownership:
  - Every local composition and fixture uses the externally provisioned `dapr init` Redis at `localhost:6379` as a shared state store and pub/sub, not owned by the run.
  - A cross-process lock file (`hexalith-dapr-test-fixture.lock`) serializes all Dapr fixtures.
  - The optional persistent Keycloak container uses fixed host ports 8180/8543.
  - Sentry credentials go to a shared per-user directory `~/.dapr/certs/hexalith-platform`.

  The spine names `EventStore.Testing(.Integration)` as owner of shared fixtures. Its only topology fixture, however, is typed to a csproj AppHost marker (`AspireTopologyFixtureBase<TAppHost>`), which Platform's file-based `apphost.cs` cannot supply according to the memlog. That fixture also explicitly checks "process liveness, not full readiness", while the spine says "process-running is insufficient".
- Evidence: `DaprComponents/statestore.yaml`, `DaprComponents/pubsub.yaml` (`redisHost: localhost:6379`); `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` L157-159; `Hexalith.EventStore.Testing.Integration/AspireTopologyFixtureBase.cs` L23, L34, L41-42, L80, L149; `Hexalith.EventStore.Aspire/HexalithEventStoreSecurityExtensions.cs` L116-128, `KeycloakFastStartPorts.cs` L19-22; `DaprSelfHostedMtls.cs` L268-275.
- Failure scenario: Builders parameterize the ports and volumes and declare AD-10 met, but two runs still share Redis state and topics. Module fixtures extend the liveness-only base and report ready before startup tasks have finished.
- Recommendation: Extend the Deferred wording to cover dapr-init Redis, the fixture lock, persistent or fixed-port Keycloak, and the shared cert directory. Add that `EventStore.Testing.Integration` must provide a readiness-based fixture that is not tied to an AppHost type, for the Platform runner.
- Suggested disposition: autofix

### BRN-8 — Module-owned production deploy manifests are neither ratified as declarations nor scheduled for migration
- Severity: medium
- Where: AD-1/AD-2 (L52, L58); Production profile authority (L128); Migration (L132)
- Finding: Memories ships a full Kustomize deployment (base plus production and qualification overlays, namespace `hexalith-memories`, image tags `0.0.0`) and OpenBao Helm values. Folders ships `deploy/dapr/production/*`, which patches other modules' Deployments (`hexalith-eventstore`, `hexalith-tenants`) with Folders-named Dapr configs in `hexalith-production`, plus `containers/production/service-images.yaml` and `nuget/release-packages.yaml`. Tenants and EventStore ship `deploy/dapr/*`. The spine makes Platform the owner of one Helm package, the per-app bindings and ACLs, and the canonical profile, but says nothing about these artifacts. Because a Dapr app takes a single configuration, per-module configs for the shared `eventstore`/`tenants` app IDs will collide.
- Evidence: `references/Hexalith.Memories/deploy/kubernetes/overlays/production/kustomization.yaml`; `references/Hexalith.Folders/deploy/dapr/production/sidecar-config-bindings.yaml` L7, L14, L31, L38; `references/Hexalith.Tenants/deploy/dapr/*`; `references/Hexalith.EventStore/deploy/dapr/*`.
- Failure scenario: Operators apply a module Kustomize overlay alongside the Platform Helm release. The result is two sources of truth for the same workloads and Dapr configs, and AD-2's retained-artifact rollback no longer covers what is running.
- Recommendation: State that module `deploy/` artifacts are conformance inputs or declarations consumed by the Platform package and profile, not independently applied deployments. Add them to the Migration and Deferred rows, including consolidating the per-app Dapr Configuration for shared app IDs.
- Suggested disposition: autofix

### BRN-9 — The existing consumer host contracts in the Platform root (Agents EXT-HOST-1, Works AD-20) are not scoped
- Severity: medium
- Where: Design Paradigm MVP list (L25); Source Precedence (L152-165); Structural Seed (L207)
- Finding: `README.md` and `docs/ext-host-1-agents-composition.md` present Platform as the EXT-HOST-1 delivery target for Agents. That contract covers Conversations, provider and safety adapters, Dapr Workflow and evidence ingress, and `eng/verify-agents-host.sh` gates it with a local Release build. `apphost.cs` and `README.md` tie the Works lane to Works AD-20's R1-R11 parity gate and require an "AD-20 exception" outside Development. The spine mentions neither Agents nor AD-20, and its MVP excludes both modules. `.gitmodules` also declares non-MVP references: Agents, ChatBot, Conversations, Timesheets and Works.
- Evidence: `README.md` L5, L19, L46-55; `docs/ext-host-1-agents-composition.md`; `eng/verify-agents-host.sh`; `apphost.cs` L24, L42-45; `references/Hexalith.Works/docs/boundary-decision-record.md` L9-14, L99-105.
- Failure scenario: A builder editing `apphost.cs` cannot tell whether the Agents and Works lanes must keep working, or which ownership split wins: the spine's (technical modules own ServiceDefaults and projection plumbing) or Works AD-20's (Platform owns them).
- Recommendation: Add a Source Precedence row or scope note. It should say that non-MVP consumers (Agents EXT-HOST-1, Works AD-20 lane) remain supported previews under the Migration convention, and that where AD-20's ownership list conflicts, the Platform spine's split governs.
- Suggested disposition: discuss

### BRN-10 — The Stack and Structural Seed evidence depends on uncommitted and untracked files
- Severity: low
- Where: Stack (L168-178); Structural Seed (L207)
- Finding: Every Stack pin (.NET 10.0.401, Aspire 13.5.4, Toolkit Dapr 13.5.1-beta.757, EventStore.Aspire 3.106.0) matches the working tree. However, HEAD still has .NET 10.0.302, Aspire.AppHost.Sdk 13.4.6 and an empty host. The Works preview relies on the untracked `DaprSelfHostedMtls.cs` and `DaprComponents/`, and the submodules are only staged. `README.md` still says Aspire CLI 13.4.6. The Stack also omits a root-file pin, Dapr control-plane images 1.18.3 (`DaprSelfHostedMtls.cs` L43, L86, L107), while the Builds CI runtime defaults to 1.18.2 (`domain-ci.yml` L29-33) and the cluster runs 1.18.1 (memlog). The claim "opt-in Works development preview, not the complete MVP" is accurate for the working tree. With the flag off (the default), the composition is empty.
- Evidence: `git diff` of `apphost.cs`/`global.json`; `git status` (untracked `DaprSelfHostedMtls.cs`, `DaprComponents/`).
- Failure scenario: The working tree is reset or split into different commits, and the spine's "observed seed" then describes code that does not exist.
- Recommendation: Label the Stack table "working tree 2026-09-27, uncommitted", or commit before finalizing. Add the local Dapr 1.18.3 observation next to the profile-inventory note.
- Suggested disposition: autofix

### BRN-11 — The Builds CI Aspire tier is advisory and module-AppHost based, and AD-5 does not say what changes
- Severity: low
- Where: AD-5 (L76)
- Finding: The reusable workflow makes the Aspire tier non-blocking by default and runs each module's own Aspire test project. AD-5 says "reuse Builds workflows … then real-service Aspire integration" but does not say whether this tier must gate, or that it must invoke the Platform runner instead of module AppHosts.
- Evidence: `references/Hexalith.Builds/.github/workflows/domain-ci.yml` L51-56, L109-112, L642-702.
- Failure scenario: Integration failures in CI stay advisory while the spine is read as requiring them.
- Recommendation: State the gating expectation and add "Builds Aspire tier moves to the Platform runner" to the Deferred row for the enrollment schema and runner.
- Suggested disposition: discuss

### BRN-12 — The local Dapr security convention is in code but not ratified
- Severity: low
- Where: Secrets (L127); Deferred "Secrets, identity, network and transport" (L238)
- Finding: The Platform AppHost applies mTLS to every sidecar through the self-hosted Sentry (`DaprSelfHostedMtls.ConfigureSidecar`). Components are scoped per app, with explicit publishing and subscription scopes and `protectedTopics`. Receivers get per-app `accesscontrol.<app>.yaml` files. Local configs for `eventstore` and `eventstore-admin` use top-level `defaultAction: allow`, while the configs for works and operations deny by default. JWT validation uses a symmetric dev key instead of the Keycloak helper that EventStore.Aspire offers. The spine is silent on whether new modules composed locally must follow this pattern. `DaprComponents/accesscontrol.yaml` L4 also points to "deploy/dapr/ for templates", which does not exist in Platform.
- Evidence: `apphost.cs` L167-187, L189-200; `DaprComponents/*.yaml`.
- Failure scenario: A new module is composed without an ACL file or scoped components, and the only missing piece is a local-parity convention.
- Recommendation: Add one line to the conventions: "Local compositions run Dapr with mTLS, a per-receiver ACL and app-scoped components. Local allow-defaults are dev-only and never profile inputs." Fix the dangling comment during migration.
- Suggested disposition: defer

### BRN-13 — Platform-owned `deploy/dapr/*` targets and McpCli's nested Platform are unaddressed
- Severity: low
- Where: Shared host and catalogs, Secrets, Production profile authority (L125-128); AD-4 "no nested-Platform"
- Finding: `deploy/dapr/eventstore-routing-catalog.json`, `production-profile.yaml` and `openbao-secret-contract.yaml` exist in no repo, Platform has no `deploy/` folder, and EventStore contains no routing-catalog codec code. Only the profile has an explicit Deferred row; creating the catalog and the secret contract is left implicit. McpCli is the only module that declares `references/Hexalith.Platform`, which is populated but unused by its build. AD-4 forbids nested-Platform fallback, and the spine expects modules to consume Platform as a pinned tool, but it does not say whether this declaration should be removed.
- Evidence: `find` across `platform/references` and `mcpcli` (no matches); `/home/administrator/projects/hexalith/mcpcli/.gitmodules`.
- Failure scenario: Builders assume the catalog and secret contract already exist, or McpCli's nested Platform becomes an implicit source for the Platform tool.
- Recommendation: Add "create routing catalog and secret contract instances" to Deferred. Add McpCli's `references/Hexalith.Platform` to the source-adoption row.
- Suggested disposition: autofix

## Checked and clean
- Stack pins match the working-tree `global.json` and the `apphost.cs` `#:sdk`/`#:package` lines exactly.
- "Current root composition is an opt-in Works development preview, not the complete MVP": accurate. The lane is gated by `Platform:Works:Enabled` and is Development-only, with no MVP modules composed.
- The Deferred statement "Existing sibling/nested/package-fallback helpers and fixed Dapr ports/shared volumes require reconciliation" is accurate as far as it goes. The sibling and nested probes, fixed ports 50001/51005/51006 and the scheduler volume are real. The omissions are covered in BRN-3, BRN-4 and BRN-7.
- AD-4 "Initialize only root-declared references" matches the Builds `initialize-build` step. CI Release/NuGet matches `domain-ci.yml`.
- Shared EventStore host with a single `eventstore` identity: `AddHexalithEventStore` plus `AddEventStoreDomainModule`; registrations are Development-only config overrides, consistent with EventStore's architecture (runtime overrides are dev-only).
- Generic fixtures are in `EventStore.Testing(.Integration)`. No module Testing package contains a generic Aspire/Dapr fixture.
- Tenants keeps its UI host plus a separate API host (`Hexalith.Tenants.Api`), consistent with the Hosted interfaces row.
- Agents and McpCli ship no AppHost, Aspire or ServiceDefaults projects. `eng/verify-agents-host.sh` enforces this for Agents.
- The local mTLS Sentry, placement and scheduler are `ExcludeFromManifest` and Development-only, consistent with AD-1: a local topology is not deployability evidence.
- No direct provider SDKs (Redis, Npgsql, EF Core, broker clients) appear in the Tenants, Parties, Folders or Projects runtime projects. Such references exist only in test projects and in `Hexalith.Builds.Tooling`, which is build and qualification tooling outside AD-9's runtime scope.
- Module release pipelines (semantic-release NuGet plus SDK container images to the Zot registry) are consistent with AD-2's "module publication authority remains mandatory".
- The minimum composition (EventStore, Tenants, Memories) is mutually root-declared in each of those modules' `.gitmodules`, and Parties declares all three, consistent with the AD-1 qualification set.
