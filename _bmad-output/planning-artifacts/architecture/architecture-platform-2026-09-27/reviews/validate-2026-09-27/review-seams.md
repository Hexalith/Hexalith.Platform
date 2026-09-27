# Review — Cross-module seams
Verdict: FAIL — the spine names owners for the artifact slots it lists (enrollment schema, routing catalog instance, secret contract, production profile), but the composed shared-EventStore-host artifact, how often the profile changes and who approves it, the identity-claim contract, the test-environment handoff, and the first shared version of most cross-team shapes are unowned or ordered nowhere, so teams working in parallel would build halves that do not fit.

Path keys used below (line numbers are from the files as read on 2026-09-27):
- **SP** = `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md`; **ML** = same folder `.memlog.md`; **PRD** = `_bmad-output/planning-artifacts/prds/prd-platform-2026-09-27/prd.md` (+ `addendum.md`)
- **ES** = `references/Hexalith.EventStore/_bmad-output/planning-artifacts/architecture.md`
- **MEM** = `references/Hexalith.Memories/_bmad-output/planning-artifacts/architecture/architecture-memories-2026-09-09/ARCHITECTURE-SPINE.md`
- **MCP** = `../mcpcli/_bmad-output/planning-artifacts/architecture/architecture-mcpcli-2026-09-22/ARCHITECTURE-SPINE.md`
- **TEN** = `references/Hexalith.Tenants/_bmad-output/planning-artifacts/architecture.md`
- **PAR** = `references/Hexalith.Parties/_bmad-output/planning-artifacts/architecture/epic-8-domain-focus-2026-07-06/ARCHITECTURE-SPINE.md`
- **PRJ** = `references/Hexalith.Projects/_bmad-output/planning-artifacts/architecture/architecture-projects-2026-07-15/ARCHITECTURE-SPINE.md`
- **FOL** = `references/Hexalith.Folders/_bmad-output/planning-artifacts/architecture.md`
- **BLD** = `references/Hexalith.Builds/.github/workflows/domain-ci.yml`

## Seam matrix

| Seam | Parties | Shape owner | Version/skew rule | Order | Fixed in spine? |
| --- | --- | --- | --- | --- | --- |
| Module enrollment declaration | Platform ↔ every module | Platform schema/validator; module instance (SP L124) | "one versioned" schema; no rule for skew between the module's pinned tool, the Platform submodule and the manifest's schema version | "Define one shared version before consumers implement" (SP L232) | Partial: fields incomplete, dual Platform version authority (SEAM-4, SEAM-8, SEAM-17) |
| Shared EventStore host (handlers + idempotency intent adapters) | Platform ↔ EventStore ↔ Folders (+ domain modules) | **Unstated** (no one is named to build the composed host project/image) | None; EventStore release identity is bound to its own `eventstore` subject | None | **No** (SEAM-1) |
| Routing/idempotency catalog | EventStore.Contracts ↔ Platform ↔ modules ↔ McpCli | EventStore.Contracts schema/codec; Platform instance (SP L125, ES L329) | Generation/root digest; no rule reconciling per-release regeneration with profile immutability | prepare/ready/commit (ES L331); no rollback after commit | Partial (SEAM-2, SEAM-14) |
| Production profile | EventStore ↔ Platform ↔ Folders/Memories/all consumers | Platform instance; AD-26 ratified by EventStore (SP L128, ES L287) | "Replacement requires AD-26 owner-ratified change" with no split between the profile template and per-release bindings | None | Partial (SEAM-2) |
| Publication authority ↔ Platform release record | EventStore ↔ Builds ↔ Platform | EventStore AD-11 records; Builds validators/codec; Platform release-record shape **unowned** | None | The lifecycle state required before staging and before production is unstated | Partial (SEAM-3) |
| Secrets | Platform ↔ EventStore AD-24 ↔ Memories | Platform contract (SP L127 = ES L258) | Generations/acknowledgements | publish/overlap/ack/revoke | Fixed for static secrets; open for per-tenant runtime credentials and the operator artifact (SEAM-12) |
| Critical-flow and smoke suites | modules ↔ Platform release workflow ↔ Builds | Declarations in enrollment; suite artifact and result shape **unowned** | "check-suite versions" are bound, but nothing says where a suite's identity comes from | Staging, then production | Partial (SEAM-16) |
| Test fixtures | EventStore.Testing(.Integration) ↔ Platform tool ↔ module Testing | Fixtures owned by EventStore; lifecycle owned by Platform; handoff contract **unowned** | None | None | Partial (SEAM-9) |
| CI | Builds ↔ Platform runner ↔ modules | Builds workflows (SP L76) | Each repo pins its own Builds SHA; CI Dapr default 1.18.2 | Isolated tests, then integration | Partial (SEAM-15) |
| Source/package mode switch | Platform AD-4/5 ↔ EventStore AD-11 ↔ Builds props ↔ modules | Split between EventStore `UseHexalithProjectReferences` and Parties' per-dependency switches | None | None | **No** (SEAM-15) |
| Recovery inventory and sets | modules ↔ Platform ↔ Administrator | Enrollment "recovery inventory"; Platform sequence (SP L118) | n/a | fence → identity → restore → verify → reopen | Mostly fixed; erasure gaps for Folders/Parties and genesis-task class (SEAM-11, SEAM-19) |
| Memories.Aspire digests ↔ Platform bindings | Memories ↔ Platform | Memories.Aspire (MEM L202, SP L25) | Helm package retains digests; rollback class undefined | Memories publishes, then Platform pins | Partial (SEAM-14) |
| McpCli static Contracts ↔ module Contracts ↔ gateway metadata | McpCli ↔ modules ↔ EventStore ↔ Platform | Schemas owned by modules; metadata endpoint co-owned by three parties with no single owner (SP L235); surface eligibility has no declaration site | Compatibility predicate unstated | Unstated | Partial (SEAM-6) |
| CLI/MCP → gateway → module authorization (actor + workload) | McpCli ↔ EventStore ↔ Projects ↔ FrontComposer | Delegation issuer **unowned** | None | None | **No** (SEAM-10) |
| Tenants UI/REST read gateway ↔ Platform ingress | Tenants ↔ Platform | Tenants AD-6/13/14; Platform ingress/DNS (SP L130) | n/a | Platform exposes separate query and command references | **Yes** |
| Keycloak realm/client/claims ↔ module audiences/permissions | Platform/Administrator ↔ EventStore AD-10/27 ↔ every module | **Unowned** (SP L238 lists three owners) | None | None | **No** (SEAM-5) |
| Dapr ACL tuples / pub-sub topics / subscriptions | modules (Parties I1 owner file) ↔ Platform profile ↔ EventStore AD-9 | The Parties authoritative file conflicts with Platform profile binding | None | EventStore AD-9 "one slice" | **No** (SEAM-4) |
| JWT host fingerprint inventory | EventStore ServiceDefaults ↔ module hosts ↔ Platform | EventStore (ES L154) | Versioned inventory | Implied: registration before non-Development readiness | Partial (SEAM-4) |
| AD-9 capability exceptions | Platform ↔ Memories | Platform AD-9 names FalkorDB only | None | No approver or decision point | Partial (SEAM-13) |
| Legacy module hosting retirement | modules ↔ EventStore AD-22 ↔ Platform | AD-22 authority; module parity | AD-22 exact-subject | Producer before consumer; parity + AD-22 (SP L132) | Direction fixed; authority during coexistence open (SEAM-18) |
| Platform capabilities assumed by Projects (durable task engine, service-level envelope) | Projects ↔ EventStore ↔ Platform | **Unowned** | n/a | n/a | **No** (SEAM-7) |
| Platform identity in a module workspace | Platform ↔ each module | Both the submodule (PRD) and the pinned tool (SP) | None | None | **No** (SEAM-8) |
| Rollback compatibility evidence (N-1 reads N) | modules ↔ Platform release | **Unowned** producer | "Previous code must read current schemas/events" (SP L64) | Before automatic promotion | Partial (SEAM-14) |

## Findings

### SEAM-1 — The composed shared EventStore host has no builder, release identity or version rule
- Severity: critical
- Where: SP Consistency Conventions "Shared host and catalogs" (L125), "Module declaration authority" (L124); EventStore AD-11/AD-22/AD-25/AD-26.
- Finding: The spine requires module handler and idempotency-intent registrations to be composed "once into the shared EventStore host". Today those intent adapters are compile-time DI registrations in the host process, so the shared host must be built from module code. The spine does not say who builds that host project or image. It also leaves open which release identity and authority records cover it under EventStore AD-11/AD-26, and what version rule governs module adapters compiled against one `EventStore.DomainService` version running inside another host version. "Repository-relative" registrations also have no package-mode (CI/CD) form.
- Evidence:
  - SP L125: "Compose module handler and idempotency-intent registrations once into the shared EventStore host. Reject conflicting app IDs/routes; do not run an additional module wrapper under the same `eventstore` identity."
  - SP L124: "repository-relative host/contract/UI-descriptor registrations".
  - ES L168: "A validated index digest is the required artifact identity for deployment … The current release mapping contains only `eventstore`; any additional image first receives an explicit release identity and the same validation contract."
  - ES L279: each idempotency catalog entry "binds the trusted adapter".
  - ES L244: consumer removal requires that "the same immutable subject has passed … AD-11 `release-available`, and AD-26 `production-promoted`".
  - FOL L1651: "`Hexalith.Folders.EventStore` (**as-built**) is the deployable EventStore host for this module: an ASP.NET Core web project (`ContainerRepository=eventstore`) … the home of the **A-9 idempotency intent adapters**".
  - Code: `references/Hexalith.Folders/src/Hexalith.Folders.EventStore/Program.cs` calls `builder.Services.AddFoldersIdempotencyIntentAdapters();`, and `Hexalith.EventStore.Server/Commands/IdempotencyIntentAdapterServiceCollectionExtensions.cs` exposes `AddIdempotencyIntentAdapter<TAdapter>()`.
- Collision scenario: EventStore releases `eventstore` digest D with its authority records, and Folders keeps publishing its own `eventstore` image D_F. Platform can then only take one of three bad paths:
  1. Deploy D. AD-25 readiness fails because the Folders trusted adapters are missing.
  2. Deploy D_F. The spine forbids a module wrapper under the `eventstore` identity.
  3. Build its own image D′. No EventStore `release-available` or `production-promoted` record covers D′, so AD-22/AD-26 authority is void. Folders adapters built against DomainService 3.103 also run inside a 3.106 host with no compatibility rule.
- Recommendation: Add a rule that names:
  - the owner of the composed host (recommended: EventStore owns a host-composition contract and a versioned in-process extension API);
  - how modules publish adapters and handlers (a library package plus the extension-contract version they target);
  - the release identity of the composed image (either a new AD-11 subject class ratified by EventStore, or out-of-process adapters so the image stays EventStore's own);
  - the build order: extension contract v1 → module adapter packages → composed host → catalog generation → profile/release binding.
- Suggested disposition: discuss

### SEAM-2 — The production profile's scope conflicts with how often the catalog and module digests change
- Severity: high
- Where: SP "Production profile authority" (L128), Stack note (L178); EventStore AD-26/AD-33; Memories AD-19.
- Finding: The profile binds the catalog/secret digests and the "exact release authority", and replacing it requires an EventStore AD-26 owner-ratified change. The routing catalog is regenerated whenever any module adds a route, so every module release changes the profile digest. That either makes the EventStore owner an approval gate for every module release, or leaves the profile describing a catalog that is no longer deployed. The spine also does not say whether Memories.Aspire data-service digests and Keycloak pins belong in the profile or in a separate "environment inventory" (L178 says "environment/profile inventory").
- Evidence:
  - SP L128: "Bind tested runtime/components, scopes/ACLs, resiliency, secret/catalog identities, recovery and exact release authority … replacing an authorizing profile requires the EventStore AD-26 owner-ratified change, a new canonical digest and renewed exact-subject authority."
  - ES L287: "Its digest must bind … app IDs, scopes and ACLs, resiliency, OpenBao contract digest, route/idempotency catalog digests … adding, replacing, or retiring an authorizing profile requires approved architecture change and a new canonical digest."
  - MEM L202: "`Hexalith.Memories.Aspire` is the single owner of qualified container image digests, and **every consumer of a container default resolves images from that one digest set**".
- Collision scenario: Projects adds a command, and Platform regenerates the catalog (new root digest). The Platform release workflow now needs either a new EventStore architecture approval, or it ships with the prior profile digest, making `production-promoted` records bound to a profile that does not describe production. A Memories FalkorDB digest bump hits the same fork.
- Recommendation: Split the ratified profile template (runtime, component types/versions, sidecar mode, ACL policy shape; changed only through AD-26) from the per-release bindings (catalog generation digest, secret-contract digest, app-ID set, module and Memories.Aspire image digests), which the Platform release record carries under a Platform-owned rule. Name the file that holds the Keycloak and data-service pins. Get EventStore to ratify the split.
- Suggested disposition: discuss

### SEAM-3 — EventStore's publication lifecycle and the Platform release record have no agreed handshake
- Severity: high
- Where: SP AD-2 (L58), Deferred "Release attempt state" (L236); EventStore AD-11/AD-26; Projects AD-30; Builds release workflows.
- Finding: Platform is EventStore's "deployment owner". The spine never says that Platform's release workflow issues EventStore's `production-promoted` deployment-owner records. It also does not say which EventStore lifecycle state (`evidence-validated` candidate or `release-available`) is required before staging and before production. The Platform release record has no named shape owner, while Builds already owns `ReleaseEvidenceCodec`, the `hexalith.dependency-release-handoff.v1` handoff and Projects' `hexalith.readiness-evidence.v1` validator.
- Evidence:
  - SP L58: "The release record binds exact module/artifact identities, configuration, applicable routing/profile identities and check-suite versions to their evidence. Module publication/profile authority remains mandatory".
  - ES L170: "`release-available` requires a separate authenticated release-owner record, and each `production-promoted` profile requires a separate authenticated deployment-owner record under AD-26."
  - ES L287: "A separately authorized immutable candidate may be published under AD-11 solely to produce evidence".
  - ES L168: "The SHA-pinned shared Builds publisher/validator owns the shape … `ReleaseEvidenceCodec`, and bounded smoke contract."
  - PRJ L302: "schema `hexalith.readiness-evidence.v1`. `Hexalith.Builds` owns the `hexalith-evidence validate` capability."
- Collision scenario: The EventStore release owner waits for staging evidence before issuing `release-available`, while Platform's staging deployment waits for `release-available` because a tag "is not authority". The result is a deadlock, or Platform promotes and never writes `production-promoted`. AD-22 legacy retirement then stays blocked for every module, and Platform invents a third evidence schema next to Builds'.
- Recommendation: Add a per-environment table of the required module lifecycle state (staging accepts EventStore `evidence-validated` candidates; production requires `release-available`). State that Platform's successful FR-7 verification emits the AD-11/AD-26 `production-promoted` record bound to the profile digest. Make Builds validators the encoding and validation authority for the Platform release record.
- Suggested disposition: discuss (the "Platform issues production-promoted" clause could be an autofix)

### SEAM-4 — The enrollment declaration lacks inputs that Platform-owned artifacts need, so Platform would hand-maintain per-module data
- Severity: high
- Where: SP "Module declaration authority" (L124), "Secrets" (L127), "Production profile authority" (L128), AD-11 "module-declared surface eligibility" (L112), AD-6 fingerprint conformance (L82).
- Finding: Platform composes the secret contract, component scopes, ACLs, topics/subscriptions and surface eligibility, and it relies on EventStore's host fingerprint inventory. The declaration schema carries none of the per-module facts these need: inbound caller tuples, pub/sub topics, dead-letter policy, required logical secrets, externally reachable hosts, or per-operation surface eligibility. Parties already asserts sole ownership of its ACL tuple set, and EventStore requires ACLs, topics and scopes to change in one slice.
- Evidence:
  - SP L124 lists the fields: identities, server list, capabilities, registrations, fixture profile, readiness/startup tasks, critical flows/checks, recovery inventory. It has no ACL, topic, secret or eligibility entries.
  - PAR L130–141: "The ACL has exactly one authoritative owner file at any time (today: `src/Hexalith.Parties.AppHost/DaprComponents/accesscontrol.parties.yaml`) … an external copy that adds an app ID, verb, or route is an I1 route-list change and needs the same recorded approval."
  - ES L146: "App IDs, service methods, route-catalog fingerprints, sidecar options, component scopes, ACLs … topics … change in one slice across AppHost and deployment assets."
  - ES L258: the secret contract "inventories logical name and map keys, consumer app and dependent component/host".
  - ES L154: "ServiceDefaults owns the versioned host/config fingerprint inventory".
  - Code: root `apphost.cs` hand-codes `DaprComponents/accesscontrol.works.yaml` and `EventStore__Publisher__TopicOverrides__work`.
- Collision scenario: Platform writes the staging ACL admitting `eventstore-operations` replay into Parties. Parties' fitness gate asserts only its local file, so Parties I1 is silently violated. Folders publishes to the Memories ingestion topic, which no declaration names, so Platform's default-deny scopes drop it and indexing never happens with no error.
- Recommendation: Extend enrollment schema v1 with inbound caller tuples, publish/subscribe topics with poison/dead-letter policy, required logical secrets (name, lifecycle, rotation unit), externally reachable hosts (cross-checked with the EventStore fingerprint inventory), and per-operation surface eligibility. State that Platform artifacts are derived from declarations, never hand-maintained per module, and that module-authoritative files such as Parties I1 are the declaration source.
- Suggested disposition: autofix (spine text); discuss the Parties I1 owner path

### SEAM-5 — The Keycloak realm and claim contract has no shape owner; six module dev realms already disagree
- Severity: high
- Where: SP AD-6 (L82), Deferred "Secrets, identity, network" (L238).
- Finding: AD-6 depends on "API audiences … and module/tenant permissions". Nobody owns the client, audience, claim-name, mapper and role contract that each realm (local, staging, production) must implement, or the declaration through which modules state what they need. The six module dev realms share the realm name `hexalith` but diverge on claims.
- Evidence:
  - SP L82: "Each environment checks its designated issuer and API audiences, explicit environment admission, and module/tenant permissions."
  - SP L238 owner: "Platform/Administrator/module owners".
  - ES L293: tenants come from "`eventstore:tenant` grants".
  - TEN L478–480: "maps the actor's claims (`sub`, `eventstore:tenant=system`, `global_admin`/`role` shapes)".
  - `references/Hexalith.*/src/Hexalith.*.AppHost/KeycloakRealms/hexalith-realm.json` mapper sets:
    - EventStore: `eventstore:tenant`, `eventstore:domain`, `eventstore:permission`, `global_admin`.
    - Memories: `tenants` only.
    - Parties: adds `party_id`, `roles` and audience `hexalith-parties`.
    - Tenants and Projects: add `eventstore:current-tenant`.
    - Folders: lacks `eventstore:domain`.
  - ML L53: dev exports "must not be copied".
- Collision scenario: Platform builds the staging realm from EventStore's mapper set. Memories expects `tenants` and fails closed on every tenant call, and Parties rejects tokens lacking `party_id`/`hexalith-parties`. The local complete environment must import one realm and cannot satisfy all six.
- Recommendation: Name one owner of a versioned realm contract (recommended: EventStore AD-10/AD-27 owns canonical claim names; Platform owns the per-environment realm instances). Modules declare the audiences, claims and roles they require in enrollment. Order: claim contract v1 before any module's hosted-auth or McpCli token work.
- Suggested disposition: discuss

### SEAM-6 — McpCli connected discovery: metadata API, compatibility predicate, surface eligibility and Contracts release order are unowned
- Severity: high
- Where: SP AD-11 (L112), Deferred "Connected McpCli metadata" (L235).
- Finding: Discovery has four open points:
  - **Metadata endpoint:** it must live in the EventStore gateway, yet it is assigned to three owners at once.
  - **Compatibility predicate:** "Compatible" is undefined; the catalog joins on "contract version", with no rule for when a bundled version matches a deployed one.
  - **Surface eligibility:** "module-declared surface eligibility" conflicts with McpCli's equal-heads rule (AD-5) and its fixed tool set (AD-12).
  - **Release order:** McpCli locks its dependency closure to one EventStore client/Contracts version, so the order EventStore.Contracts → module Contracts → McpCli → Platform must hold but is stated nowhere.
- Evidence:
  - SP L112: "intersects bundled compatible operations with the selected gateway's committed EventStore catalog, module-declared surface eligibility and applicable authorization. Unknown/incompatible mappings are not executable".
  - SP L235 owner "McpCli/EventStore Contracts/Platform".
  - ES L329: map "to exactly one app ID, method, and contract version".
  - MCP L86 (AD-5): "One call model, one document set, equal across heads".
  - MCP L146 (AD-15): "The allowed transitive baseline is the exact locked closure of the pinned EventStore client, `Hexalith.EventStore.Contracts`, and Stack packages … Projects and Folders enter only when they satisfy the rule."
  - MCP L80 (AD-4): manifest from flagged `PackageReference`s only.
- Collision scenario: Parties ships Contracts 2.1 with an additive field, and the production catalog records 2.1 while the installed McpCli bundles 2.0. Under the fail-closed rule, all Parties operations disappear from production McpCli until a new McpCli release. Separately, Parties bumps EventStore.Contracts, which breaks McpCli's locked closure, and McpCli cannot build.
- Recommendation: Make EventStore the owner of a versioned gateway metadata endpoint derived from the committed catalog (compliant with AD-10/AD-16). Define the compatibility predicate in the catalog entry (exact version or declared range). Choose one declaration site for surface eligibility (decoration attribute or enrollment) and amend McpCli AD-5/AD-12. Fix the release order and the skew behaviour (an older McpCli shows affected operations as non-executable, not missing).
- Suggested disposition: discuss

### SEAM-7 — Platform capabilities that Projects depends on have no owner in the Platform or EventStore spines
- Severity: high
- Where: SP Source Precedence Projects row (L163), Deferred "Module service-level and functional qualification" (L241); Projects AD-9, AD-28, gate G-1.
- Finding: Projects assigns the durable task engine, Confirmation Artifacts and service-level enforcement (99.9%, 15-minute service RTO, committed-event RPO 0) to "EventStore/platform" or "the platform". The EventStore spine has no durable-task AD. The Platform spine hands qualification back to "module owners with Platform", adopts no HA on its one-node cluster, and records no override for Projects as it did for Folders.
- Evidence:
  - PRJ L161 (AD-9): "EventStore/platform owns generic task IDs, admission, records, leases, checkpoints, receipts, retry scheduling …".
  - PRJ L483 (G-1): "EventStore/platform Durable Task engine and opaque Confirmation Artifact record … absent from current published/clean EventStore 3.70.1 API evidence".
  - PRJ L290 (AD-28): "The platform configuration and evidence must enforce 99.9% monthly availability, 15-minute service RTO … and committed-event RPO 0".
  - SP L163: "Preserve the 99.9% target … and primary-domain committed-event RPO 0 qualification; Platform disaster targets do not prove them."
  - SP L23: "Platform does not implement … a second orchestration framework."
  - ES: no durable-task or Confirmation Artifact AD (grep: none).
- Collision scenario: The Projects team waits on "platform" for G-1 and HA enforcement, Platform waits on Projects to qualify, and EventStore has no story for either. Projects cannot supply critical flows for consequential work or production evidence, so FR-10 (MVP includes Projects in production) is silently unattainable.
- Recommendation: Record an owner for G-1 (EventStore, or explicitly outside the MVP) and an explicit decision on Projects AD-28: a user override like Folders', or Projects production enrollment blocked with a named owner for the HA/RPO-0 capability.
- Suggested disposition: discuss

### SEAM-8 — A module workspace has two authorities for the Platform version (submodule and pinned tool)
- Severity: medium
- Where: SP "Local tool and readiness" (L129), AD-4 (L70); PRD FR-2.
- Finding: The PRD makes Platform a direct Git submodule of each module workspace, while the spine makes it a pinned .NET tool in `.config/dotnet-tools.json`. Nothing says which one defines the composition code and the enrollment schema version, or what happens when they differ.
- Evidence:
  - PRD L58: "A module workspace includes Platform as a direct Git submodule".
  - Addendum L38: "Parties repository directly references EventStore, Tenants, Memories, and Platform as submodules".
  - SP L129: "Provide a pinned consumable Platform .NET tool registered in the module's `.config/dotnet-tools.json`".
  - SP L70: "No … nested-Platform … fallback".
  - PRJ L272: "A pinned independently consumable platform .NET tool … without edits to its repository."
- Collision scenario: Parties pins tool 1.3 while `references/Platform` points at a 1.5 commit. The tool validates against schema v1 while the submodule's composition expects v2 fields. Which code composes the environment, and which schema validates the manifest?
- Recommendation: Declare the tool package version as the single Platform identity for module workspaces. The submodule is either absent or used only in an explicit Platform-source debug mode. Manifests carry a schema version, and the tool rejects unknown major versions.
- Suggested disposition: discuss (touches PRD FR-2 wording)

### SEAM-9 — Test-environment lifecycle is owned twice: EventStore fixtures vs the Platform runner
- Severity: medium
- Where: SP AD-10 (L106), "Local tool and readiness" (L129).
- Finding: The spine keeps shared fixtures in EventStore.Testing(.Integration) but gives the lifecycle policy to the Platform runner, and defines no handoff contract for endpoints and composition identity. The existing EventStore fixture starts and disposes its own topology, uses a 6-minute timeout, cleans up on failure, and needs a project-typed AppHost. Platform's AppHost is file-based.
- Evidence:
  - SP L106: "retain surviving local test/startup failure … supply endpoints and composition identity to ordinary module tests".
  - SP L129: "Shared reusable fixtures remain owned by `EventStore.Testing(.Integration)` … Default startup deadline is 10 minutes".
  - Code: `references/Hexalith.EventStore/src/Hexalith.EventStore.Testing.Integration/AspireTopologyFixtureBase.cs` has `AspireTopologyFixtureBase<TAppHost>` built with `DistributedApplicationTestingBuilder` (L131), `StartupTimeout => TimeSpan.FromMinutes(6)` (L74), and on timeout `await DisposeAsync()` then throw (L159–162).
  - ML L38: "File-based AppHosts cannot be passed to DistributedApplicationTestingBuilder".
- Collision scenario: Parties tests derive from the EventStore fixture and start their own topology, bypassing AD-10 (wrong deadline, no retention, no ownership record). Or the Platform runner also starts one, giving two environments per suite with conflicting cleanup.
- Recommendation: State that the Platform runner is the only lifecycle owner. EventStore.Testing.Integration exposes attach-mode fixtures that read a versioned, Platform-owned environment descriptor. The self-hosting fixture base becomes EventStore-internal or legacy.
- Suggested disposition: discuss

### SEAM-10 — The actor+workload delegation chain for McpCli has no issuer, and parallel MCP surfaces have no status
- Severity: medium
- Where: SP AD-6 (L82), AD-11 (L112).
- Finding: The spine keeps "actor plus workload/delegation checks", but McpCli sends only a static user bearer token. Nobody is named to mint the workload/delegation context that Projects requires on gateway-mediated calls. Projects and Parties also route MCP through a FrontComposer MCP host, and Memories runs its own production MCP. The spine neither admits these as Platform interfaces nor retires them.
- Evidence:
  - SP L82: "Preserve required actor plus workload/delegation checks; a valid bearer token alone does not satisfy every operation's authorization."
  - PRJ L242 (AD-20): "An immutable context carries … authenticated caller/workload service, delegation identifier/scopes/audience".
  - PRJ L296 (AD-29): "FrontComposer/platform composes MCP".
  - PAR L90: "MCP plumbing → FrontComposer MCP host on Commons.Http (G11)".
  - MCP L116 (AD-10): "v1 `StaticBearerTokenHandler`".
  - MEM L339: "Production independently scales Server and gate-approved MCP."
- Collision scenario: Every McpCli call to Projects fails closed (FR-12 unmet for Projects), or someone adds an allow-all composition that Projects forbids. Meanwhile the FrontComposer MCP host exposes Projects and Parties operations with separate eligibility rules.
- Recommendation: Name the delegation issuer for gateway-mediated calls (the EventStore gateway) and its shape. State whether FrontComposer and module MCP hosts are permitted MVP interfaces (consuming the same eligibility declaration) or are retired through McpCli AD-21 inventories.
- Suggested disposition: discuss

### SEAM-11 — The Folders override leaves an unplanned EventStore dependency and deletion-resurrection risk after restore
- Severity: medium
- Where: SP Source Precedence (L154, L164), "Memories erasure continuity" (L149), Deferred "Folders source alignment" (L242).
- Finding: The override supersedes Folders' RPO, retention and topology envelope. Folders' own plan, however, keeps the stories that produce its recovery inventory and drill evidence (13.5, 13.7) chained to EXT-ES-RECOVERY. That item asks EventStore for PostgreSQL v2 compatibility, a recovery-safety export and restored-backup admission, none of which EventStore plans. The spine calls fixing this "documentation consistency". Separately, non-resurrection after restore is guaranteed only for Memories tombstones. Folders deletions/legal holds and Parties erasures inside the one-hour RPO window have no rule.
- Evidence:
  - SP L154: "supersede Folders' five-minute RPO, 35-day retention and mandatory replicated/no-singleton production topology … Ordinary Platform evidence and module behavior, authorization and data-safety rules remain required."
  - SP L242: "This is documentation consistency work, not an additional Folders enrollment block."
  - FOL L226: "`EXT-ES-RECOVERY` publishes the PostgreSQL v2 actor-state compatibility, physical backup, recovery-safety export, and restored-backup admission capability".
  - FOL L242: "Story 13.7 follows … `EXT-ES-RECOVERY`, and produces the supported-profile, backup, restore, and drill evidence."
  - FOL L833: "EventStore owns a signed metadata-only recovery-safety export … prevents deletion resurrection".
  - ES L362: "v2 is incompatible and has no v1 migration path; AD-26 retains v1".
- Collision scenario: Platform expects Folders' AD-12 recovery boundaries and integrity checks, but Folders' execution rank blocks them on an EventStore deliverable that will never arrive. A drill restores a point 50 minutes old, and a Parties erasure or Folders deletion from the last hour reappears.
- Recommendation: Make the Folders re-plan an owned prerequisite of Folders' enrollment evidence, not optional documentation. Decide whether the recovery-safety export is a retained data-safety rule (name EventStore as owner) or is dropped. Add a general rule that acknowledged erasures, deletions and legal holds are not resurrected by restore, or record an owner-accepted RPO exception for Parties and Folders (the facets of EventStore AD-30).
- Suggested disposition: discuss

### SEAM-12 — The static secret contract conflicts with Memories' runtime per-tenant credentials and operator artifact
- Severity: medium
- Where: SP "Secrets" (L127).
- Finding: Platform "owns … scopes and acknowledged rotations" through a static, value-free inventory. Memories provisions and rotates per-tenant backend credentials at runtime through its lifecycle workflow, and keeps an operator artifact whose single writer is "the operator". The spine does not say which role that is.
- Evidence:
  - SP L127: "Platform owns the value-free `deploy/dapr/openbao-secret-contract.yaml`, scopes and acknowledged rotations … Required key/secret generations gate readiness."
  - MEM L170 (AD-15): "rotation of a tenant data-plane credential is an AD-6 lifecycle operation … Production qualification is blocked until per-tenant backend principals replace it".
  - MEM L110 (AD-5): "One deployment-time artifact, the operator artifact … with exactly one writer, the operator, held in AD-15's operator secret scope".
- Collision scenario: A Platform rotation run rotates the Memories Redis credential that the Memories lifecycle also rotates. Both write, and generations are never acknowledged, so readiness is blocked. Or tenant principals are absent from the contract and default-deny blocks them. With no named operator, the operator artifact is never produced and Memories cannot qualify.
- Recommendation: Let the contract declare a module-owned dynamic secret namespace (policy path template, rotation owned by the module) alongside static entries. Assign the Memories "operator" role (Administrator or Platform deployment owner) and name the per-environment step that produces the operator artifact.
- Suggested disposition: discuss

### SEAM-13 — The AD-9 exception list names FalkorDB only; Memories' Redis adapters have no approver
- Severity: medium
- Where: SP AD-9 (L100).
- Finding: Memories treats its direct Redis search/vector adapter and a Redis `SET NX` coordination exception as approved. Platform AD-9 accepts only FalkorDB, explicitly reserves coordination state to Dapr, and names no approver or decision point for further exceptions. Memories is in every domain module's minimum composition, so this stalls every enrollment.
- Evidence:
  - SP L100: "**Memories FalkorDB is accepted** … General state/coordination, messaging, workflows and secrets retain Dapr boundaries. Additional exceptions identify the missing capability, owner and surface."
  - MEM L128 (AD-8): "confined to … `Adapters.Redis` or `Adapters.FalkorDb` … Only the finite Direct Redis Exception Registry below may bypass Dapr state".
  - MEM L253–255: "Direct Redis search/vector/index operations are ordinary provider data-plane adapter work. The only coordination-state exception to Dapr is: `IPreflightDedupStore.TryReserveAsync` … `SET NX`".
  - ML L103: the registry "must be reconciled individually by capability".
- Collision scenario: Platform's AD-9 dependency guard flags NRedisStack/StackExchange.Redis in Memories. Memories cites its own AD-8, and no party has authority to close the gap.
- Recommendation: List the Memories Redis search/vector adapter in AD-9 now and decide on the `SET NX` dedup exception, or name the approver (the Platform architecture owner) and require the decision before the enrollment validator enforces AD-9.
- Suggested disposition: autofix (list) or discuss

### SEAM-14 — Rollback crosses owner protocols with no handshake (N-1 compatibility, post-commit catalog, data-service images)
- Severity: medium
- Where: SP AD-3 (L64), Deferred "Release attempt state" (L236).
- Finding: Three rollback handshakes are missing:
  - **N-1 compatibility evidence:** "Previous code must read current schemas/events", but nobody is named to produce that evidence or its shape.
  - **Catalog after commit:** EventStore defines catalog rollback only during activation. Returning to the prior generation after commit would rewind the digest-key generations bound in the idempotency facet, which AD-3 forbids.
  - **Memories data-service images:** images arriving through Memories.Aspire are not classified as application artifacts (rolled back) or infrastructure (kept).
- Evidence:
  - SP L64: "Previous code must read current schemas/events … Catalog restoration follows its owner's activation/continuity protocol; never rewind key generations or durable authority."
  - ES L331: "failure rolls back to the prior complete generation" (during activation only).
  - ES L279: facet binds "active and reader digest-key generations".
  - ES L140: "`SkippedUnknownEventType` … require an explicit cataloged policy".
  - MEM L202: Memories.Aspire owns the Redis/FalkorDB digests.
  - SP L64: data and infrastructure sit outside rollback.
- Collision scenario:
  - Release N adds an event type and smoke writes happen during the verification window. Rollback to N-1 hits unknown event types with no proven tolerance.
  - Or rollback restores catalog G-1 and resurrects a retired key generation.
  - Or rollback reverts a FalkorDB image over newer on-disk data.
- Recommendation: Add a module-declared rollback-compatibility check (an enrollment field) consumed by the promotion gate. Ask EventStore to define post-commit rollback as a new forward generation that keeps key generations. Classify module-supplied data-service images as infrastructure outside Helm application rollback, or require forward-compatible data formats.
- Suggested disposition: discuss

### SEAM-15 — CI and source/package mode: Builds defaults and EventStore's package-default switch contradict AD-4/AD-5, and no owner is named
- Severity: medium
- Where: SP AD-4 (L70), AD-5 (L76), Deferred "Source/package adoption" (L233).
- Finding: The Builds Aspire tier is non-blocking by default, has a 10-minute job timeout that includes restore, build and Dapr init, pins Dapr runtime 1.18.2 (the cluster runs 1.18.1), and runs a module-owned AppHost test instead of the Platform runner. EventStore's source/package switch defaults to packages even in Debug. The spine assigns neither owner nor skew rule.
- Evidence:
  - SP L76: "Reuse Builds workflows … then real-service Aspire integration using Release/NuGet artifacts."
  - BLD L109–113: `aspire-continue-on-error` "Whether the Aspire tier is non-blocking." `default: true`.
  - BLD L118–121: `aspire-timeout-minutes` default 10.
  - BLD L29: `dapr-runtime-version` default `'1.18.2'`.
  - ES L160/166: "Package mode is default … Unset or explicit `UseHexalithProjectReferences=false` is package intent in every configuration, including Debug."
  - ML L35: Parties per-dependency switches "inferred from file existence".
  - ML L65: Dapr 1.18.1 installed.
  - PAR front matter: "HEAD Builds gitlink … past the last I16-authorized pin … I16 stop in effect".
- Collision scenario: Module CI goes green with failing integration tests (contradicting FR-5). The Platform tool runs Parties in Debug, but its props resolve EventStore from NuGet, a silent package substitution. Moving Builds for Platform CI triggers the Parties I16 stop.
- Recommendation: Assign Builds ownership of a blocking Platform-integration CI entry that uses the Platform runner, AD-10 deadlines and the Dapr pin from the profile inventory. Name one source/package property owner (a Builds prop set only by the Platform tool) and retire per-dependency switches. State the skew rule between a module's Builds pin and Platform composition pins.
- Suggested disposition: discuss (owner names can be an autofix)

### SEAM-16 — Critical-flow and smoke suites have no artifact identity or execution contract
- Severity: medium
- Where: SP Release and Recovery Acceptance "Staging gate" (L140), AD-2 (L58).
- Finding: The release record binds "check-suite versions", but the spine does not say:
  - how module E2E and smoke suites are published as immutable artifacts;
  - how the private deployment runner invokes them with environment endpoints and credentials;
  - what result format the gate reads.
- Evidence:
  - SP L140: "complete flow-to-test mappings … pass for the exact release".
  - SP L58: "check-suite versions".
  - PRD L133: flow lists "must be supplied when their release checks are integrated with Platform".
  - ES L168: Builds owns the "bounded smoke contract".
  - PRJ L302: Builds-owned `hexalith.readiness-evidence.v1`.
- Collision scenario: Parties keeps its E2E tests as a source project run from `main`, while Platform records the Parties package version as the suite version. The evidence is from the wrong revision and the gate cannot detect it.
- Recommendation: Make the check suite part of each module's release (a digest-identified package or image), with a Platform-owned invocation and result contract (or adopt Builds' bounded smoke contract), declared in the enrollment instance.
- Suggested disposition: discuss

### SEAM-17 — Only the enrollment schema has a "first shared version" order; the other shared v1 shapes are unordered
- Severity: medium
- Where: SP Deferred "Enrollment schema/validator" (L232), "Module declaration authority" (L124).
- Finding: The spine orders only the enrollment schema before consumers. The following shapes also span teams and are consumed by other teams' epics, yet have no v1 owner or ordering:
  - the shared-host extension contract (SEAM-1);
  - the routing catalog schema (the file is still absent);
  - the gateway metadata API (SEAM-6);
  - the realm/claim contract (SEAM-5);
  - the environment descriptor for fixtures (SEAM-9);
  - the check-suite contract (SEAM-16);
  - the profile/release-record split (SEAM-2, SEAM-3).

  Projects has already fixed a schema name and path that the spine calls "implementation seed".
- Evidence:
  - SP L232: "Define one shared version before consumers implement against it."
  - SP L124: "Exact encoding is implementation seed."
  - PRJ L438–447: `module/hexalith-projects.module.json  # hexalith.module.v1` and `dotnet tool run hexalith-module run|down|test`.
  - ES L459: catalog owners "create `deploy/dapr/eventstore-routing-catalog.json`" (absent).
  - ES L287: "The file is absent".
- Collision scenario: Projects implements `hexalith.module.v1`, Parties implements the PRD addendum "configuration file listing the servers", and Platform then publishes a different v1, forcing two rewrites. McpCli builds discovery against a gateway endpoint EventStore never ships.
- Recommendation: Add a "first shared versions" table (artifact, owner, consumers, must-exist-before which epic), and adopt or explicitly supersede Projects' `hexalith.module.v1`. Until those rows exist, only Platform-internal and EventStore-schema epics are safe to run in parallel. Module adoption epics should wait.
- Suggested disposition: autofix

### SEAM-18 — No authoritative composition is named while legacy module AppHosts and Platform coexist
- Severity: low
- Where: SP "Migration" (L132).
- Finding: AD-22 requires `production-promoted`, so legacy module AppHosts coexist with Platform throughout the MVP. The spine does not say which composition is authoritative for topology changes and module CI in that period (EventStore AD-9 requires one slice), and "applicable" AD-22 authority is undefined for hosting that is not EventStore infrastructure.
- Evidence:
  - SP L132: "applicable EventStore AD-22 exact-subject consumer-removal authority is satisfied".
  - ES L244: requires "AD-26 `production-promoted`".
  - TEN L152: "`src/Hexalith.Tenants.AppHost` is transitional legacy … must not gain shared hosting plumbing".
  - PAR L153 (I1a).
- Collision scenario: Tenants needs a new topic for its CI, so it adds it to its legacy AppHost (breaking its own AD-13) or to Platform only, which its CI does not run.
- Recommendation: State that during coexistence the Platform composition is authoritative and legacy AppHosts are frozen rollback surfaces. Define the scope of "applicable".
- Suggested disposition: autofix

### SEAM-19 — Startup tasks lack a lifecycle class, so Memories' genesis step could re-run on restore
- Severity: low
- Where: SP "Module declaration authority" (L124), AD-10 (L106), AD-12 (L118).
- Finding: Declarations list "startup tasks" without distinguishing per-environment-start tasks from once-per-population bootstrap. Memories' genesis marker must never be re-created on a hosted redeploy or a disaster-recovery restore.
- Evidence:
  - SP L124: "readiness/startup tasks".
  - MEM L214 (AD-21): "A named platform-bootstrap step, run before the first tenant may be provisioned, exclusively creates the register's genesis marker … Starting a new population is not recovery".
- Collision scenario: A DR restore runs the declared startup tasks, and the genesis task finds the register unreadable and creates a new population, defeating AD-21 erasure continuity.
- Recommendation: Add a startup-task class to enrollment schema v1: per run-owned environment, once per environment lifetime, or never on restore.
- Suggested disposition: autofix

## Checked and clean
- **Tenants read transport and ingress:** SP L44/L130 preserve the BFF-to-Tenants REST read contract, keep it separate from the McpCli gateway route, and hold InteractiveServer to one replica. This matches TEN AD-6 (L110), AD-13 (L152) and AD-14 (L158).
- **Routing catalog ownership split:** SP L125 matches ES AD-33 L329 word for word (EventStore.Contracts owns schema/codec, Platform owns the instance, prepare/ready/commit, no hand-maintained McpCli catalog).
- **Secret contract composer:** SP L127 matches ES AD-24 L258 (Platform deployment owner is sole composer; Kubernetes Secrets bootstrap-only; generations gate readiness).
- **Profile file ownership and ratification:** SP L128 matches ES AD-26 L287 (Platform publishes `deploy/dapr/production-profile.yaml`; AD-26 is still an assumption; Dapr API portability is not compatibility). The only problem is the change-cadence gap in SEAM-2.
- **JWT vs app-channel authentication:** SP L82 keeps EventStore AD-10 and AD-28 distinct, as ES L154/L299 require.
- **Delivery semantics:** SP L126 matches ES AD-8 L138–140 (at-least-once, unordered, MessageId dedup, durable poison capture, no second outbox).
- **Memories digest ownership direction:** SP L25 matches MEM AD-19 L202 (Memories.Aspire is the single digest owner and Platform consumes it). The rollback class is open (SEAM-14).
- **Memories erasure and recovery:** SP L149 matches MEM AD-2 L90 and AD-21 L214 (projections have no backup restore; replay only; unknown lineage fails closed; a copied register is not authority).
- **Parties MCP erasure:** SP L112 "Parties exposes no MCP erasure operation" matches PAR I7.
- **McpCli placement:** stdio/CLI on the user or agent host calling the HTTPS gateway, with no v1 hosted HTTP MCP and no remote plugin loader. SP L112 matches MCP AD-10 and the `unsupported_transport` behaviour (MCP L316).
- **No domain-owned AppHost:** SP L25 matches TEN AD-13, PAR I1a target state, and PRJ AD-24/AD-25.
- **Projects manifest fields:** the SP L124 declaration fields cover the PRJ AD-25 L272 manifest contents (host, IDs, UI descriptor, fixture profile, repository-relative paths, thin fixtures).
- **Single `eventstore` app ID:** consistent with FOL I-4 stable app IDs. Who builds that host remains open (SEAM-1).
- **Direction of legacy retirement:** "producer before consumer; parity + AD-22" matches PAR AD-1/I3/I1a and ES AD-22.
- **Staging/production policy numbers:** the gate, rollout and verification numbers are faithful to PRD FR-6 to FR-8.
