# Review — Brownfield ratification (validate r2)

Verdict: **CONDITIONAL PASS** — Most of the prior brownfield gaps now have owned work, and the Stack pins match the code. Two existing conventions are still contradicted without a migration rule. First, Builds already ships a module runner, a composition AppHost, an `eventstore` composition root and a manifest schema. The spine assigns all four to a Platform tool that does not exist. Second, every MVP module hardcodes Dapr component names. Both must be resolved before module adoption epics start.

Scope: Platform repository at HEAD `32f63b5`. The root composition files have been committed since `b9410d3`. The spine is the working-tree version with the uncommitted AD-11 amendment. Submodules were checked at their current checkouts: EventStore `e29b44a2`, Tenants `49ca5b46`, Parties `c069af07`, Folders `fb55117`, Projects `6bc5155`, Memories `28977338`, McpCli `05e9a81`, Builds `0610f78`. Spine line numbers refer to `ARCHITECTURE-SPINE.md`. Read-only: no file other than this report was written.

## Prior findings verified (reviews/validate-2026-09-27/review-brownfield.md)

| Prior | Status now |
| --- | --- |
| BRN-1 Memories direct Redis | Resolved. AD-9 (L105) names the Redis search/vector and SET NX exceptions. The remaining coordination is transitional until G2, and the Deferred row "Memories conformance and continuity" (L320) owns it. The code still has direct Redis usage in more than 90 files under `Hexalith.Memories.Server`, which matches that transitional status. |
| BRN-2 Raw Dapr state in Parties/Projects | Resolved. AD-9 admits module-owned auxiliary records when they are declared. |
| BRN-3 Inverted Debug defaults | Partially resolved. AD-4 now says "the Platform tool alone sets the single mode property". L308 removes the file-existence selectors, which remain in `Tenants/Directory.Build.props` L18-22 and `Parties/Directory.Build.props` L4-11. Acceptable. |
| BRN-4 EventStore.Aspire pin and Works sibling paths | Resolved. The Stack row at L209 and the Deferred row at L308 cover both. |
| BRN-5 Hosting inventory | Resolved for domain modules (L309). `Builds.Module.AppHost` is still unclassified; see BF-1. |
| BRN-6 ServiceDefaults | Deferred with an owner and a gate (L310). |
| BRN-7 Local lifecycle | Resolved (L307). |
| BRN-8 Module deploy manifests | Resolved. AD-1 L57 makes them "declaration or conformance inputs … never a second writer". |
| BRN-9 Agents EXT-HOST-1 / Works AD-20 | Resolved (Migration row, L160). |
| BRN-10 Uncommitted seed | Now stale in the other direction; see BF-6. |
| BRN-11 Builds advisory Aspire tier | Partially resolved. AD-5 names a Builds-owned "blocking Platform-runner integration entry", but there is no Owned-work row or gate, and `domain-ci.yml` is unchanged. Folded into BF-1. |
| BRN-12 Local mTLS/ACL | Owned (L316). The dangling comment "See deploy/dapr/ for templates" (`DaprComponents/accesscontrol.yaml` L4) remains. It is trivial. |
| BRN-13 Catalog, secret contract and McpCli Platform reference | The catalog and secret-contract creation is owned (L315); the files still exist nowhere, which is expected. The McpCli fix went the wrong way; see BF-4. |

## Evidence table

| Spine rule | Observed code (file:line) | Class |
| --- | --- | --- |
| AD-1: one Aspire model over ordinary C# helpers | `apphost.cs` L1-7 is a file-based AppHost. `aspire.config.json`. `eng/verify-agents-host.sh` L20-25 fails if module hosting projects appear | ratifies |
| AD-1: Dapr resources rendered from the profile and declarations | `DaprComponents/*.yaml` are hand-authored for the Works lane only (`pubsub.yaml` L29-43, `statestore.yaml` L26-31, per-app `accesscontrol.*.yaml`) | intended change (owned: L316 ratifies the local convention; the Works preview is labelled at L263) |
| AD-1: registrations come from declarations | `apphost.cs` L98-105 uses raw overrides of the form `EventStore__DomainServices__Registrations__*` | intended change (Works preview; Migration row L160) |
| AD-4: no sibling or nested fallback | `apphost.cs` L28-33 uses `../works` and `works/references/Hexalith.EventStore` | intended change (owned L308) |
| AD-4: missing source fails with a named path | `apphost.cs` L34-40 throws `FileNotFoundException` naming the path | ratifies |
| AD-4: root-declared Hexalith dependency builds from source | `apphost.cs` L7 has `#:package Hexalith.EventStore.Aspire@3.106.0`, while `references/Hexalith.EventStore` is declared | intended change (Stack L209, L308) |
| AD-4: the Platform submodule is the identity of a module workspace | Only `Hexalith.McpCli/.gitmodules` declares `references/Hexalith.Platform`. EventStore, Tenants, Parties, Folders, Projects and Memories do not | **silent contradiction** (BF-4) |
| AD-4: no file-existence mode selection | Existence-probe chains remain: `Tenants/Directory.Build.props` L18-22, `Parties/Directory.Build.props` L4-11, `EventStore/Directory.Build.props` L23-29, `Memories/Directory.Build.props` L24-31 | intended change (owned L308) |
| AD-6: "The root HS256 development key is retired from Platform compositions" | `apphost.cs` L189-201 applies the symmetric `DevOnlySigningKey-AtLeast32Chars!` to `eventstore` and `eventstore-admin` | **spine misdescribes** (BF-6) |
| AD-9 / Module declaration: Platform assigns Dapr component names | `apphost.cs` L140 has Platform supply `EventStoreOperations__PubSubName=pubsub` | ratifies (direction) |
| AD-9: "Module code never hardcodes Dapr component names" | `const` or fixed `"statestore"`, `"pubsub"` and `"secretstore"` values in Tenants, Parties, Folders, Projects, Memories and EventStore (list in BF-2) | **silent contradiction** (BF-2) |
| AD-9: named Memories exceptions; other coordination transitional | Direct Redis in more than 90 `Memories.Server` files (Activities 40, Endpoints 8, Search 6, Tenants 2, Consistency 3). FalkorDB appears across Activities, Graph and Workflows | intended change (owned L320) |
| AD-9: auxiliary records through module-owned Dapr stores | `Parties.Security/PartyErasureRecordStore.cs` L8, `TenantKeyRotationService.cs` L18, `Projects.Infrastructure/DaprProjectsStateStore.cs` | ratifies (declaration required) |
| AD-10: run-owned resources, no fixed ports | `DaprSelfHostedMtls.cs` L11-16 and L25-27 fix ports 50001/51005/51006 and the volume `hexalith-platform-dapr-scheduler`. `statestore.yaml`/`pubsub.yaml` use dapr-init `localhost:6379` | intended change (owned L307) |
| AD-10: "Only the Platform runner provisions … multi-module integration environments" | The Builds `hexalith-module` runner and `Builds.Module.AppHost` compose multi-module G-4 runs (`README.md` L204-273; `src/hosts/Hexalith.Builds.Module.AppHost/*.csproj` L6; `RunTopology.cs` L157) | **silent contradiction** (BF-1) |
| Local tool: the pinned Platform .NET tool in `.config/dotnet-tools.json` | Platform has no `.config/` directory, and no Platform tool exists anywhere. `README.md` L37-48 documents `aspire run`. Builds' `hexalith-module` claims to be "the only public runner" | **silent contradiction** (BF-1) |
| AD-13: a module never publishes an `eventstore` web project or image | `Folders.EventStore.csproj` L6 sets `ContainerRepository=eventstore`; the Folders release has `publish-containers: false` (L84) | intended change (owned, Composed host row L311) |
| AD-13: exactly one Platform-composed `eventstore` host | `Hexalith.Builds.Module.EventStoreHost/Program.cs` L13-15 is a "Builds-owned EventStore composition root". It is registered as app `eventstore` (`RunTopology.cs` L25, L195-196) and shipped inside the tool package (`Module.Cli.csproj` L23-27) | **silent contradiction** (BF-1) |
| Module declaration and First shared versions: "adopt or supersede Projects' `hexalith.module.v1`" | The implemented schema is Builds' `schemas/hexalith.module-manifest.v1.json` (plus `module-descriptor.v1` and `module-run-evidence.v1`). Its `platform` block pins `eventStoreVersion` const 3.109.0 and `daprRuntimeVersion` const 1.18.2 (L63-66) | **spine misdescribes** (BF-1) |
| Migration: domain modules own no AppHost, Aspire or ServiceDefaults | Tenants (AppHost, Aspire), Parties (AppHost), Folders (AppHost, Aspire, ServiceDefaults), Projects (AppHost, Aspire, ServiceDefaults) | intended change (owned L309) |
| Technical-module AppHosts stay valid for their own repository tests | EventStore, Memories and Commons hosts are used for their own tests; FrontComposer.AppHost is sample-only. `Builds.Module.AppHost` is not an own-repository test root | ratifies, except Builds (BF-1) |
| AD-5: disposable Linux hosted runners | Every module workflow uses `runs-on: ubuntu-latest` (one `windows-latest`). No self-hosted runners. Platform has no `.github/workflows` | ratifies |
| AD-5: a blocking Builds Platform-runner entry | `domain-ci.yml` L51-56, L109-112, L642-647: the Aspire tier runs the module's own AppHost test project and is advisory by default. There is no Platform-runner input | intended, but no owned row (BF-1) |
| AD-7: no deployment credential as a repository secret | Repository secrets hold publication credentials only: `NUGET_API_KEY`, `HEXALITH_ZOT_*`, `NUGET_SIGNING_*`. `PROJECTS_E2E_TEST_USER_PASSWORD` targets the local managed AppHost realm (`Projects/ci.yml` L201-209) | ratifies |
| Workflows: registry `registry.hexalith.com` | `domain-release.yml` L471 and L1025 use it as the default; EventStore `release.yml` L224 and Memories `release.yml` L122 do too | ratifies |
| Workflows and provenance: publication "verifies the intake-pinned module images"; each artifact class has provenance | `domain-release.yml` L996-1001 attests only `*.nupkg`. EventStore's release has a pinned Builds checkout but no image attestation. Memories' release uses Builds actions at `@main` with no attestation | **silent contradiction** (BF-5) |
| AD-2 seed: GitHub attestations and governed provenance | `Builds/Github/governed-provenance/*`, `domain-release.yml` L221-237 | ratifies (seed) |
| Stack: .NET 10.0.401, Aspire 13.5.4 | `global.json`; `apphost.cs` L2, L4, L5 | ratifies |
| Stack: Toolkit Dapr beta.757 "aligns to the Builds catalog (beta.770)" | Catalog `Props/Directory.Packages.props` L151 is beta.770. The Builds-approved `Tools/runtime-toolchain-baseline.json` L14 is **beta.757**, approved 2026-09-06 with Platform among the owner roles | **spine misdescribes Builds authority** (BF-6) |
| Stack: Kubernetes and Keycloak preview pins; EventStore 3.109.0 | Catalog L132-133 and L9 | ratifies |
| Stack: Dapr 1.18, skew to reconcile | `DaprSelfHostedMtls.cs` L43, L86, L107 use 1.18.3. `domain-ci.yml` L33 uses 1.18.2. The baseline L16 and the manifest schema L66 fix 1.18.2 | intended change (owned). The Builds pins are unnamed (BF-6) |
| Stack header: "Seed from the uncommitted working tree" | The seed was committed in `b9410d3` (2026-09-27 22:11) | **spine misdescribes** (BF-6) |
| Catalogs and Secrets: `deploy/dapr/eventstore-routing-catalog.json`, `openbao-secret-contract.yaml` | Absent in every repository. EventStore has no catalog code; its phase-4 spec (`readiness-gates.md` L38, L56) names the same paths | intended change (owned L315) |
| Module deploy assets as declaration inputs | Memories `deploy/kubernetes`, Folders `deploy/dapr/production`, Tenants and EventStore `deploy/dapr` | intended change (AD-1) |
| AD-11: McpCli stdio is the only MVP MCP surface; legacy surfaces are obsolete | See the inventory below | intended change. Scope gap in BF-3 |

## Inventory: existing proprietary MCP and CLI projects

Search method: `*.csproj` names containing Mcp or Cli, plus projects that reference `ModelContextProtocol*`, `System.CommandLine` or `PackAsTool`. Test and fixture projects are excluded. Tenants, Agents, Conversations, Works and Timesheets have none. Their `*.Client` projects and `Conversations.Admin.Web` are libraries or UIs.

| Path (under `references/`) | Module (class) | Kind | Transport or distribution |
| --- | --- | --- | --- |
| `Hexalith.EventStore/src/Hexalith.EventStore.Admin.Mcp` | EventStore (technical) | MCP host | stdio exe (`WithStdioServerTransport`), not packable |
| `Hexalith.EventStore/src/Hexalith.EventStore.Admin.Cli` | EventStore (technical) | CLI | dotnet tool `eventstore-admin`, packable. EventStore pins 3.82.0 in its own `.config/dotnet-tools.json` |
| `Hexalith.Memories/src/Hexalith.Memories.Mcp` | Memories (technical) | MCP host | HTTP (`MapMcp`, Web SDK), packable, container `memories-mcp`, composed in `Memories.AppHost/Program.cs` L492 |
| `Hexalith.Memories/src/Hexalith.Memories.Cli` | Memories (technical) | CLI | tool `memories`, packable |
| `Hexalith.FrontComposer/src/Hexalith.FrontComposer.Mcp` | FrontComposer (technical) | MCP plug-in host library | `ModelContextProtocol.AspNetCore`/`MapMcp`, packable, in the Builds catalog, hosted in `samples/Counter/Counter.Web` |
| `Hexalith.FrontComposer/src/Hexalith.FrontComposer.Cli` | FrontComposer (technical) | CLI | tool `frontcomposer` (inspection and migration of generated output) |
| `Hexalith.Parties/src/Hexalith.Parties.Mcp` | Parties (domain) | MCP host | HTTP web host, container `parties-mcp`, composed in `Parties.AppHost/Program.cs` L105 |
| `Hexalith.Parties/src/Hexalith.Parties.UI` | Parties (domain) | MCP plug-in consumer | references `FrontComposer.Mcp` (csproj L61-62); no `MapMcp` in `Program.cs` |
| `Hexalith.Folders/src/Hexalith.Folders.Mcp` | Folders (domain) | MCP host | stdio exe |
| `Hexalith.Folders/src/Hexalith.Folders.Cli` | Folders (domain) | CLI | tool `folders` (scaffold, System.CommandLine) |
| `Hexalith.Projects/src/Hexalith.Projects.Mcp` | Projects (domain) | MCP plug-in | library on `FrontComposer.Mcp` (`ProjectsMcpModule`), not hosted |
| `Hexalith.Projects/src/Hexalith.Projects.Cli` | Projects (domain) | CLI | exe scaffold, not packable |
| `Hexalith.ChatBot/src/Hexalith.ChatBot.Mcp` | ChatBot (domain, non-MVP) | MCP host | stdio exe, not packable |
| `Hexalith.ChatBot/src/Hexalith.ChatBot.Cli` | ChatBot (domain, non-MVP) | CLI | exe, not packable |
| `Hexalith.Builds/src/libraries/Hexalith.Builds.Module.Cli` | Builds (technical) | CLI (runner and qualification tooling) | tool `hexalith-module`, which ships the G-4 AppHost, EventStoreHost and UiHost |
| `Hexalith.Builds/src/libraries/Hexalith.Builds.Evidence.Cli` | Builds (technical) | CLI (evidence validator) | tool `hexalith-evidence` |
| `Hexalith.McpCli/src/Hexalith.McpCli` (+ `.Mcp`, `.Core`, `.Abstractions`, `.Analyzers`) | McpCli (tool) | the canonical target | tool `hexalith`; stdio MCP head |

The memlog records only the MCP half of this list (V-48, memlog L171). The CLI half and the two Builds tools appear in neither the memlog nor the spine.

## Findings

### BF-1 — Builds already implements the runner, composition host, `eventstore` root and manifest schema that the spine assigns to Platform
- Severity: high
- Location: AD-10 (L111), AD-13 (L129), Module declaration (L147), Local tool and readiness (L156), First shared versions (L292, L297-298), Owned work (L307), AD-5 (L81)
- Finding: Builds has two repository-scoped tools. Its README (L206-207) calls them "the only public runner and readiness-validator contracts for the G-4 workflow":
  - `hexalith-module` validates a strict `hexalith.module-manifest.v1` and "owns supported runner lifecycle": `run`, `down` and `test`, with stable exit codes and `hexalith.module-run-evidence.v1`.
  - `hexalith-evidence` validates readiness-evidence matrices.

  The `hexalith-module` package carries a Builds-owned Aspire AppHost that "composes one G-4 run". That AppHost wires multi-module topologies, readiness and run ownership. The package also carries a Builds-owned `eventstore` composition root on `Hexalith.EventStore.Gateway` and a UI host. Projects' spine (L439-453, L489 G-4) targets exactly these tools and calls them "the platform module runner and evidence validator". Its `hexalith.module.v1` is realized as Builds' `hexalith.module-manifest.v1`.

  The Platform spine never names any of this. It gives the runner, the pinned tool, the declaration schema and the composed host to Platform, and it cites the schema as "Projects' `hexalith.module.v1`". Neither the Platform tool nor a Platform schema exists; Platform has no `.config/`. This is the "two lifecycle owners" condition that AD-10 exists to prevent, and a second EventStore composition root that AD-13 excludes. AD-5's Builds-owned "Platform-runner integration entry" has no Owned-work row. `domain-ci.yml` still runs module AppHosts in an advisory tier. The amended AD-11 would also classify both Builds tools as obsolete technical-module CLIs (BF-3).
- Evidence: `references/Hexalith.Builds/README.md` L204-273; `schemas/hexalith.module-manifest.v1.json` L63-66; `src/hosts/Hexalith.Builds.Module.AppHost/Hexalith.Builds.Module.AppHost.csproj` L6; `RunTopology.cs` L25, L157, L195-196; `src/hosts/Hexalith.Builds.Module.EventStoreHost/Program.cs` L13-15; `src/libraries/Hexalith.Builds.Module.Cli/Hexalith.Builds.Module.Cli.csproj` L23-27 (commit `3c8a520` "ship the composition hosts inside the module tool"); `.github/workflows/domain-ci.yml` L51-56, L109-112, L642-647; `references/Hexalith.Projects/_bmad-output/planning-artifacts/architecture/architecture-projects-2026-07-15/ARCHITECTURE-SPINE.md` L439-453, L489. The memlog (L171) lists "Builds.Module" only as an AppHost, and the r1 rubric (update-2026-09-27/review-rubric.md L187) flagged it as "unclassified".
- Consequence: Once `hexalith-module` is published, Projects pins it under G-4 and other modules follow. From then on the ecosystem has two runners, two manifest schemas with conflicting version pins (the manifest schema fixes Dapr 1.18.2 and EventStore 3.109.0 as consts), and two `eventstore` roots. Platform-runner evidence and G-4 evidence will disagree about what a passing integration environment is, and the retirement of either path becomes a cross-repository migration.
- Suggested fix: Record an explicit decision in the spine, with an Owned-work row and the gate "before `hexalith-module` is first published or pinned". Choose one of:
  - (a) Ratify `hexalith-module` as the implementation of the Platform runner and tool. Platform keeps the declaration semantics, and Builds encodes and validates them, as it already does for the release record. Rename the Local tool convention accordingly, and state how the tool satisfies AD-4's submodule-identity check.
  - (b) Freeze `hexalith-module`, its hosts and its schema before publication, and name their supersession by the Platform tool with Builds as owner.

  Either way, make three wording changes. Change the First shared versions row to "adopt or supersede Builds `hexalith.module-manifest.v1` (the implementation of Projects' `hexalith.module.v1`)". Classify `Builds.Module.AppHost` and `EventStoreHost` under AD-10 and AD-13. Add the Builds Platform-runner CI entry, and the retirement of the advisory module-AppHost tier, to Owned work.
- Suggested disposition: discuss

### BF-2 — Every MVP module hardcodes Dapr component names, and the spine forbids this without a migration rule
- Severity: high
- Location: AD-9 (L105, "Module code never hardcodes Dapr component names"), Module declaration (L147, "Platform assigns component names"), AD-8 (L99), Owned work (L303-324: no row)
- Finding: The ecosystem convention is fixed logical names: `statestore`, `pubsub` and `secretstore`. EventStore documents `statestore` as "the component every Hexalith domain already binds". Domain modules hardcode these names as private `const` values. Some SDK subscriptions bind them through attribute constants: `[Topic(ProjectionChangeNotifierOptions.DefaultPubSubName, …)]` and `[EnvironmentTopic(PubSubName, …)]`. EventStore's own AD-24 contract fixes a singleton `openbao` component. No Owned-work row, gate or owner covers replacing these with Platform-assigned names. This conflicts with AD-8 in hosted environments. Kubernetes Dapr Component names are unique per namespace. Each environment has one application namespace, and each module on a shared instance needs its own least-privilege principal, which means its own Component. Two modules that both read `"statestore"` cannot both get their own component.
- Evidence:
  - Tenants: `src/Hexalith.Tenants/Projections/TenantProjectionHandler.cs` L26; `GlobalAdministratorProjectionHandler.cs` L20; `Queries/Handlers/TenantQueryHandlerBase.cs` L29.
  - Parties: `src/Hexalith.Parties.Security/PartyErasureRecordStore.cs` L8; `TenantKeyRotationService.cs` L18; `KeyOperationAuditService.cs` L9.
  - Folders: `src/Hexalith.Folders/Projections/TenantAccess/FoldersTenantEventSubscription.cs` L9; `Projections/SemanticIndexing/EventStoreSemanticIndexingBridgeStore.cs` L20.
  - Projects: `src/Hexalith.Projects/Projections/TenantAccess/ProjectsTenantEventSubscription.cs` L18; `Hexalith.Projects.Workers/ProjectsWorkersModule.cs` L50.
  - Memories: `src/Hexalith.Memories.EventStore/EventIngestionController.cs` L39, L59; `Hexalith.Memories.Server/Ingestion/EmbeddingSecretStore.cs` L17; `DirectoryIngestionService.cs` L26.
  - EventStore: `src/Hexalith.EventStore.Server/Configuration/ProjectionChangeNotifierOptions.cs` L12; `Hexalith.EventStore/Controllers/ProjectionNotificationController.cs` L36; `Hexalith.EventStore.DomainService/EventStoreDataProtectionOptions.cs` L27-30; `_bmad-output/specs/spec-eventstore-phase-4-readiness-recovery/readiness-gates.md` L56 (singleton `openbao`).
- Consequence: The Aspire-to-Helm qualification (L312) or the first hosted enrollment finds that Platform-assigned names break module startup and subscriptions. Platform then either patches every module under deadline or keeps the shared names, which gives up AD-8's per-module principals on shared instances.
- Suggested fix:
  - Add an Owned-work row "Dapr component-name injection". Owners: module owners, with EventStore for the SDK defaults and attribute-bound subscriptions. Gate: before the Aspire-to-Helm qualification and hosted enrollment.
  - State whether logical names may remain as overridable option defaults, provided Platform always binds them through configuration and no `const` or attribute literal is used.
  - Reconcile EventStore AD-24's singleton `openbao` with Platform-assigned names.
- Suggested disposition: autofix (row); discuss (whether logical defaults are allowed)

### BF-3 — The amended AD-11 sweep drops the approved scope limit and records no inventory
- Severity: medium
- Location: AD-11 (L117), Owned work "Legacy MCP/CLI retirement" (L324), Migration and coexistence (L160), frontmatter `sources`
- Finding: The approved sprint change proposal limits the change to "module presentation and operator tools" (`sprint-change-proposal-2026-09-27.md` L14). AD-11 instead says "All proprietary Hexalith module and technical-module MCP hosts, plug-ins, and CLIs … are obsolete". Builds is a listed technical module, so the literal text makes Builds' `hexalith-module` and `hexalith-evidence` obsolete. The spine itself relies on Builds for the validators, the check-suite contract and the release-record encoding. The same text arguably also covers the pinned Platform tool. Four other gaps:
  - The spine does not say whether existing legacy surfaces are frozen (no new operations) or may keep evolving until retirement. It says only that no *new* surface is admitted. Domain AppHosts, by contrast, are explicitly frozen.
  - The spine no longer states the r1 decision (memlog V-48) that Memories.Mcp and Parties.Mcp are not composed into Platform environments. Both are hosted HTTP MCP servers with containers, wired into frozen module AppHosts.
  - The amendment has no memlog entry; the memlog's last event, "spine finalized", predates it.
  - The sprint change proposal is not in `sources`.
- Evidence: the inventory above; `sprint-change-proposal-2026-09-27.md` L14, L80-90; `.memlog.md` L171, L182; `git diff` of the spine (the AD-11 and L324 changes are uncommitted).
- Consequence: A literal reading retires the qualification tooling the release process depends on, or builders treat the rule as unenforceable. Legacy surfaces keep gaining operations that McpCli must later match.
- Suggested fix:
  - Import the proposal's scope limit: operation-access surfaces are in scope; build, qualification and developer tooling (the Platform tool, `hexalith-module`, `hexalith-evidence`) are out.
  - Add: "existing surfaces are frozen and are never composed into Platform environments".
  - Record the amendment and this inventory in the memlog, and add the proposal to `sources`.
- Suggested disposition: autofix

### BF-4 — AD-4's Platform-submodule identity has no brownfield path, and the owned work removes the only existing instance
- Severity: medium
- Location: AD-4 (L75), AD-10 (L111), AD-11 (L117, "Locally the Platform tool builds and launches McpCli"), Owned work "Source/package adoption" (L308)
- Finding: AD-4 makes the Platform submodule commit the single Platform identity of a module workspace. The pinned CI tool refuses to run unless the submodule HEAD matches. No MVP domain or technical module declares `references/Hexalith.Platform`; only McpCli does. Yet L308 schedules "removal of McpCli's unused nested Platform reference". McpCli's tests must consume the Platform runner's descriptor (AD-10), so McpCli CI needs exactly that submodule. Within McpCli's own workspace the reference is direct, not nested. The Platform↔McpCli cycle is harmless because nested submodules stay uninitialized (PRD L89). "Add missing direct declarations" does not say that the Tenants, Parties, Folders and Projects workspaces must add Platform.
- Evidence: `references/Hexalith.McpCli/.gitmodules` (entry `references/Hexalith.Platform`); `.gitmodules` of EventStore, Tenants, Parties, Folders, Projects and Memories (no Platform entry); PRD `prd.md` L80, L89; `addendum.md` L34.
- Consequence: Following L308 breaks AD-4's CI identity check for McpCli. Domain modules have no named step that gives them the identity AD-4 requires.
- Suggested fix: Replace the L308 clause with "add a direct `references/Hexalith.Platform` declaration to every workspace that runs the Platform tool: domain modules, McpCli, and technical modules when they produce Platform integration evidence; keep McpCli's".
- Suggested disposition: discuss (autofix once the intent is confirmed)

### BF-5 — The spine requires verifiable module-image provenance that no module release produces today
- Severity: medium
- Location: Workflows and provenance (L155, "verifies the intake-pinned module images"; "Provenance for each artifact class"), Module intake (L149), AD-2 (L63)
- Finding: No module release attests its container images:
  - The Builds `domain-release` workflow (Tenants, Parties and Folders call it at a pinned SHA) attests only `*.nupkg`.
  - EventStore uses its own release workflow with a pinned Builds checkout and no image attestation.
  - Memories, whose images are in every composition, uses its own release with Builds actions at `@main` and no attestation.

  All three push to `registry.hexalith.com` with repository-secret Zot credentials. The spine does not define what "verifies" means for module images, and no Owned-work row requires module release paths to converge. The row at L314 names "provenance" for Platform, Builds and Administrator only.
- Evidence: `references/Hexalith.Builds/.github/workflows/domain-release.yml` L996-1001; `Hexalith.EventStore/.github/workflows/release.yml` L114-127, L158-161, L224-226; `Hexalith.Memories/.github/workflows/release.yml` L42-45, L120-124; `Hexalith.Tenants/.github/workflows/release.yml` L281; `Hexalith.Parties/.github/workflows/release.yml` L298; `Hexalith.Folders/.github/workflows/release.yml` L77.
- Consequence: Either the publication workflow cannot verify any Memories or EventStore image and every release blocks, or verification quietly degrades to a digest pin. AD-2's provenance chain then has a gap at module intake.
- Suggested fix: Define the module-image verification predicate: an attestation naming the module repository, workflow, protected ref and pinned Builds workflow ref. Add an Owned-work row: Builds `domain-release` attests image digests; EventStore and Memories adopt it or an equivalent attested path; gate "before the first staging promotion".
- Suggested disposition: autofix (row); discuss (predicate)

### BF-6 — Several statements about the Platform root and Stack no longer match the code
- Severity: low
- Location: AD-6 (L87), Stack header (L201) and rows L208 and L213
- Finding: Three statements no longer match the code:
  1. AD-6 states that the HS256 development key "is retired from Platform compositions". The only existing composition still applies it.
  2. The Stack says its seed comes "from the uncommitted working tree", but it was committed in `b9410d3`.
  3. The Toolkit Dapr row treats Platform's beta.757 as drift from "the Builds catalog (beta.770)". Builds' own approved `runtime-toolchain-baseline.json` (2026-09-06, owner roles Builds, Platform and FrontComposer/Web) pins beta.757 and Dapr runtime 1.18.2, and the manifest schema hard-codes 1.18.2. Builds therefore holds two version authorities that disagree, and the spine names only one.
- Evidence: `apphost.cs` L189-201; `git log -- apphost.cs` (`b9410d3`); `references/Hexalith.Builds/Tools/runtime-toolchain-baseline.json` L4, L14, L16; `Props/Directory.Packages.props` L151; `schemas/hexalith.module-manifest.v1.json` L66.
- Consequence: These are small, but they mislead: a builder believes HS256 is already gone, or aligns to the wrong Builds pin.
- Suggested fix:
  1. Reword AD-6 to future tense ("is removed from the Works preview when the local realm lands") and add that step to the L308 row.
  2. Change the Stack header to "committed seed, 2026-09-27".
  3. Name the catalog as the package authority and require the baseline and the manifest-schema consts to be refreshed in the same alignment work.
- Suggested disposition: autofix

## Checked and clean
- Platform CI is absent, so it cannot contradict AD-5 or AD-7. Every module workflow runs on GitHub-hosted runners, and none holds deployment or hosted-environment credentials.
- The AD-9 provider-SDK boundary holds. `StackExchange.Redis`, `NRedisStack` and `NFalkorDB` appear only in Memories runtime projects; other provider references appear only in AppHost hosting projects.
- Module deploy assets and the Folders `eventstore` image are covered by AD-1 and the Composed host row. Folders currently does not publish containers.
- Module release registry defaults match `registry.hexalith.com`, and Builds' governed provenance exists as the AD-2 seed.
- The Stack pins for .NET, Aspire, Kubernetes, Keycloak and EventStore match `global.json`, `apphost.cs` and the Builds catalog.
