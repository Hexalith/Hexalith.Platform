# Review — Rubric walker
Verdict: PASS WITH FINDINGS — The spine covers the PRD's policy surface accurately and ratifies most inherited contracts, but it leaves nine cross-unit seams undecided (high). These need fixing before epics.

## Checklist
| # | Item | Result (pass/concern/fail) | Note |
| --- | --- | --- | --- |
| 1 | Fixes the real divergence points; misses none | concern | Missing: who builds the shared EventStore host that carries module in-process adapters (RUB-1), how identity/realm configuration is composed and which local issuer is used (RUB-6), CLI/MCP caller and surface identity (RUB-5), whether staging must run the production profile (RUB-7), how Platform itself is versioned and how module versions enter a release (RUB-9), and hosted host naming (RUB-14). |
| 2 | Every AD Rule is enforceable and prevents its divergence | concern | AD-7's "trusted" runner boundary has no enforcement mechanism (RUB-4). AD-11 says the gateway enforces surface eligibility, but the gateway has no authenticated surface signal (RUB-5). AD-6's "explicit environment admission" has no mechanism or enforcement point (RUB-6). AD-4 leaves undeclared dependencies undefined (RUB-12). |
| 3 | Nothing Deferred lets two units diverge | concern | "Token provisioning/renewal", "prove … schema/event compatibility", "Select exact provider/runtime pins" and "Define one shared version" each hide an undecided cross-team invariant (RUB-3, RUB-5, RUB-7, RUB-9). |
| 4 | Named tech verified-current (light) | pass | Stack matches `global.json`/`apphost.cs` and the memlog's NuGet checks. Minor: the Aspire CLI is unpinned although AD-10 depends on its lifecycle commands (RUB-20). |
| 5 | Ratifies rather than contradicts brownfield | concern | Acknowledged: the Works sibling/nested lane and fixed Dapr ports. Unclassified: FrontComposer.AppHost's integrated topology, the Agents EXT-HOST-1 host contract, Memories `deploy/kubernetes`, and Folders' as-built `eventstore` host image (RUB-1, RUB-10). |
| 6 | Covers every PRD capability, including quiet requirements | concern | FR-1..FR-12 and NFR-2/3 land, and the numeric policies are reproduced faithfully. NFR-1's explicit ask to define compatibility evidence is not met (RUB-3). The addendum's ask that architecture define the GitHub delivery mechanism is not met (RUB-15). The FR-7 intended-release binding is missing (RUB-19). |
| 7 | No AD weakens/contradicts an inherited module invariant without saying so | fail | AD-9 silently overrides Memories AD-8's Redis search/vector adapter and its Direct Redis Exception Registry (RUB-2). Projects AD-28 assigns the 99.9%/RPO-0 envelope to Platform; the spine "preserves" it but gives it no owner (RUB-8). The McpCli and Folders deltas are declared correctly. |
| 8 | Every altitude-owned dimension decided/deferred/open | concern | Silent: Platform self-versioning (RUB-9), hosted observability and outage detection, staging/production resource isolation on one node, the shared-infrastructure upgrade path, and cost (RUB-16). |
| H | Spine hygiene (terse, build substrate, valid diagrams, seed vs invariant) | concern (low) | Mostly terse and rule-shaped. The Mermaid is syntactically valid, but `\n` labels and a disconnected Memories island weaken it (RUB-20). The Source Precedence opening paragraph carries decision history that belongs in the memlog. |

## Findings

### RUB-1 — No owner or artifact identity for the shared EventStore host that carries module in-process registrations
- Severity: high
- Where: Consistency Conventions → "Shared host and catalogs" (spine line 125); AD-2
- Finding: The convention forbids any module wrapper under the `eventstore` identity. It requires module handler and idempotency-intent registrations to be composed "once into the shared EventStore host". It does not say:
  - which repository builds that host binary and image;
  - how module-owned adapter code gets into it;
  - what release identity the resulting composite image carries.

  EventStore's released image cannot contain Folders code, and EventStore AD-11 requires every additional image to get its own release identity and validation.
- Evidence:
  - Spine: "Compose module handler and idempotency-intent registrations once into the shared EventStore host. Reject conflicting app IDs/routes; do not run an additional module wrapper under the same `eventstore` identity."
  - Folders `architecture.md`:1651/1657: "`Hexalith.Folders.EventStore` (**as-built**) is the deployable EventStore host … (`ContainerRepository=eventstore`) … the home of the **A-9 idempotency intent adapters**".
  - EventStore `architecture.md` AD-11: "The current release mapping contains only `eventstore`; any additional image first receives an explicit release identity and the same validation contract."
  - EventStore AD-25: "Every compatible host loads the idempotency facet … Each entry binds the trusted adapter".
- Failure scenario:
  - Folders retires its wrapper as the spine requires and ships its adapters as a package, while EventStore keeps publishing a generic `eventstore` image. Platform deploys that image, Folders' catalog entries name adapters that aren't loaded, and readiness fails or Folders commands are refused.
  - Alternatively, Platform builds an ad-hoc composite image with no AD-11 identity. The AD-11/AD-26 authority validator then rejects it for production.
- Recommendation: Add to the convention: "Platform owns the composition host project for app ID `eventstore`, referencing EventStore host packages plus module-published adapter/registration packages. Its image is a Platform release subject with an explicit AD-11 release identity and evidence. Modules publish adapters as packages, never host images." If EventStore should own the host instead, say so and define the package-intake contract. Add EventStore owner ratification to Deferred.
- Suggested disposition: discuss

### RUB-2 — AD-9 silently overrides Memories AD-8's Redis adapter and Direct Redis Exception Registry
- Severity: high
- Where: AD-9 (line 100); dependency diagram (lines 38–39); Source Precedence Memories row
- Finding: AD-9 accepts only "Memories FalkorDB", and says "General state/coordination … retain Dapr boundaries". Memories AD-8 permits two things AD-9 doesn't mention:
  - a Redis search/vector data-plane adapter (`Adapters.Redis`);
  - a Direct Redis Exception Registry whose adopted row uses a raw Redis coordination primitive (`SET NX`) and lets later Memories ADs add rows.

  The spine neither accepts nor rejects these, and it doesn't say that it narrows Memories' authority. The memlog knew this ("must be reconciled individually by capability"), but the point didn't reach the spine.
- Evidence:
  - Spine AD-9: "Unsupported specialized capabilities may use named, module-owned bounded adapters; **Memories FalkorDB is accepted**. … General state/coordination, messaging, workflows and secrets retain Dapr boundaries. Additional exceptions identify the missing capability, owner and surface."
  - Memories spine AD-8: "Provider SDK references must be confined … to named internal `Adapters.Redis` or `Adapters.FalkorDb` namespaces … Only the finite Direct Redis Exception Registry below may bypass Dapr state".
  - Memories registry: "`IPreflightDedupStore.TryReserveAsync` / `ReleaseAsync` | `SET NX` with finite TTL … A later `AD-n` may add a row".
  - Memories Stack: "Redis Stack Server / NRedisStack / StackExchange.Redis".
  - Memlog L103: "Its existing finite direct-Redis exception registry must be reconciled individually by capability; do not treat the FalkorDB clarification as blanket approval".
- Failure scenario:
  - Platform implements AD-9's dependency and architecture guard with only NFalkorDB allowed. Memories' NRedisStack vector search and `SET NX` dedup fail the guard, so Memories cannot enroll. Every domain module's minimum environment (EventStore + Tenants + Memories) is then blocked.
  - Alternatively, Memories reads "Specialized bounded adapters" (plural, in the Source Precedence row) as approval and keeps adding registry rows. Teams then apply the guard inconsistently.
- Recommendation: Decide explicitly, with the user because they named only FalkorDB:
  - Is the Memories Redis search/vector adapter accepted on the same capability basis?
  - Does the `SET NX` preflight-dedup row stay as a named bounded exception with its fail-open posture, or migrate to Dapr state (with owner and deadline)?

  Record in AD-9 that new Memories registry rows require Platform acceptance.
- Suggested disposition: discuss

### RUB-3 — NFR-1 compatibility evidence is restated, not defined
- Severity: high
- Where: AD-3; Release and Recovery Acceptance table; module-declaration convention; Deferred "Release attempt state…"
- Finding: The PRD says "The architecture must define the compatibility evidence and rollback mechanism." The spine defines the mechanism (the retained Helm package). For evidence, it only states the rule "Previous code must read current schemas/events" and defers "prove … schema/event compatibility". There is:
  - no module-declared compatibility artifact;
  - no gate row in the acceptance table;
  - no agreed test shape.
- Evidence:
  - PRD NFR-1: "Compatibility must be verified before automatic promotion … The architecture must define the compatibility evidence and rollback mechanism."
  - Spine AD-3: "Previous code must read current schemas/events and use current security state."
  - Spine Deferred: "Before automated promotion, prove … schema/event compatibility".
  - The declaration list ("stable module/domain/app/resource identities … recovery inventory") has no compatibility item.
- Failure scenario: Parties changes an event format, while Projects documents compatibility only in prose. The promotion gate has nothing uniform to check, so it promotes. Production verification then fails and rolls back to N-1. N-1 cannot deserialize events written by the candidate during the window, so the rollback "succeeds" at Helm level while projections poison. That is exactly the NFR-1 violation the gate exists to prevent.
- Recommendation: Add a "Before production update" row: each included module declares rollback-compatibility evidence for the candidate against the recorded previous working release. The evidence has two parts:
  - a change classification for persisted schemas and event formats (none / additive / breaking);
  - a passing staging N-1 read check, where the previous release reads state and events written by the candidate.

  Breaking or missing evidence disables automatic promotion and requires the separately planned procedure. Add the item to the declaration list and reference EventStore AD-13 compatibility vectors.
- Suggested disposition: autofix

### RUB-4 — AD-7's trust boundary for the self-hosted deployment runner is not enforceable as written
- Severity: high
- Where: AD-7 (line 88)
- Finding: The Rule relies on "trusted release workflows" and "ordinary build/integration jobs stay on disposable hosted runners", but binds no mechanism to make that true. It doesn't say:
  - which repos, workflows or refs can target the runner;
  - that it is unavailable to `pull_request` or fork-triggered workflows;
  - whether credentials are released through protected GitHub Environments or reviewers;
  - how jobs are isolated from each other;
  - what RBAC scope the kube credentials have.

  Hexalith repositories are referenced by anonymous public GitHub URLs. GitHub's self-hosted-runner hardening guidance warns that forks of public repositories can run code on self-hosted runners. This runner holds production Kubernetes credentials and sits on the private LAN.
- Evidence:
  - Spine AD-7: "Trusted release workflows use a dedicated self-hosted Linux runner with access to the designated Kubernetes API and verification endpoints. Separate staging/production credentials. Ordinary build/integration jobs stay on disposable hosted runners."
  - AD-7 Prevents: "exposing deployment authority to build jobs".
  - `.gitmodules` and README use `https://github.com/Hexalith/...`.
- Failure scenario: The runner is registered at org level for convenience. A PR workflow in any public Hexalith repo specifies `runs-on: self-hosted`, reads the production kubeconfig or reaches the private network, and deploys or exfiltrates. Or it simply reads a previous job's leftover credentials on a persistent runner.
- Recommendation: Tighten the Rule:
  - Register the runner to the Platform repo only, or to a runner group restricted to named release workflows on protected refs.
  - Never make it available to `pull_request` or fork-triggered workflows.
  - Release staging and production credentials only through GitHub Environments with protection rules.
  - Clean or ephemeralize per job.
  - Give kube credentials namespace-scoped RBAC rather than cluster-admin, with separate credentials for named infrastructure operations.
- Suggested disposition: autofix

### RUB-5 — CLI/MCP caller identity and surface identity are undecided, so AD-11's surface enforcement can't be enforced
- Severity: high
- Where: AD-11 (line 112); AD-6 (line 82); Deferred "Connected McpCli metadata…" ("token provisioning/renewal")
- Finding: Both McpCli heads (CLI and stdio MCP) call the same gateway with the same user bearer token. The spine gives the gateway no authenticated signal to tell CLI, MCP-agent and interactive-UI calls apart, and no workload or delegation credential for McpCli. The gateway therefore can't enforce "surface eligibility" or Projects' dual-principal rule. Only client-side catalog filtering can, and a user or agent bypasses it through the CLI head or plain HTTPS.
- Evidence:
  - Spine AD-11: "the gateway reauthorizes every call and enforces the module's permitted operation/surface contract … Module-specific confirmation and surface restrictions remain binding … Parties exposes no MCP erasure operation."
  - Spine AD-6: "Preserve required actor plus workload/delegation checks".
  - McpCli AD-10: "v1 `StaticBearerTokenHandler`".
  - McpCli AD-14: the profile stores a static `token`.
  - Projects AD-20: "Platform admission validates both credentials … Dual-principal adapters (Chatbot, Web, CLI) carry the actor's session and cannot originate or self-confirm."
  - Projects AD-29: "MCP cannot confirm end-user resolution or proposal choices".
- Failure scenario: An agent using stdio MCP doesn't see Parties' erasure operation. It shells `hexalith send …EraseParty` (CLI head, same token), and the gateway sees an ordinary authorized call. Or an agent sends a Projects confirmation that the gateway can't distinguish from a human interactive session. Conversely, Projects requires a workload credential McpCli never presents. Every Projects operation through McpCli then fails, and teams add ad-hoc exceptions.
- Recommendation: Add a binding contract:
  - Per-environment Keycloak clients per surface (e.g. CLI and MCP), using auth-code+PKCE or device flow rather than static tokens.
  - The authenticated client (`azp`) is the surface identity the gateway and modules evaluate.
  - MCP sessions never carry the interactive-session claim.
  - Agent-host delegation/workload semantics are agreed with Projects.
  - Consequential restrictions are enforced server-side by module policy against that authenticated surface.

  Leave token lifetime and refresh as seed.
- Suggested disposition: discuss

### RUB-6 — Identity configuration composition and local/CI identity are not fixed; "environment admission" is undefined
- Severity: high
- Where: AD-6; module-declaration convention (line 124); Deferred "Secrets, identity, network and transport setup"
- Finding: AD-6 governs only the hosted staging and production realms. Three things are not bound:
  - who declares and who composes module clients, audiences, roles and claim mappings into each environment's realm;
  - which issuer local and CI environments use (FR-1, FR-4, and the SM-1 CLI/MCP demos all need tokens);
  - what "explicit environment admission" is (realm membership, or a named claim) and which hosts check it.

  The brownfield already diverges. There are ten different `hexalith-realm.json` exports, with differing client sets across Parties, Projects, Tenants, EventStore, Folders, Memories and FrontComposer, plus an HS256 dev key in the root AppHost.
- Evidence:
  - `find references -name hexalith-realm.json` shows distinct files. Examples: Parties `['hexalith-eventstore','hexalith-parties','hexalith-parties-ui']`; Projects `['hexalith-eventstore','hexalith-projects-ui']`; Tenants `['hexalith-eventstore','hexalith-tenants-ui']`; Folders `['hexalith-eventstore','hexalith-folders']`; FrontComposer `['hexalith-eventstore','hexalith-frontcomposer-ui','hexalith-tenants-ui']`.
  - Root `apphost.cs`: `"DevOnlySigningKey-AtLeast32Chars!"` and `Issuer "hexalith-dev"`.
  - Projects AD-25: the platform tool "owns EventStore, Dapr, identity/secret injection".
  - Spine AD-6: "Each environment checks its designated issuer and API audiences, explicit environment admission, and module/tenant permissions."
  - Tenants architecture: "JWT bearer at the UI host (Keycloak/OIDC `Authority` in prod …)".
- Failure scenario:
  - The FR-1 complete environment must pick one realm or issuer. Parties fixtures expect `hexalith-parties-ui`, Projects expects `hexalith-projects-ui`, and the Works lane uses HS256, so the complete environment breaks the others' tests or runs several issuers.
  - In hosted environments, someone adds a module audience by hand in staging, and it is missing in production. The promoted release then fails verification and rolls back.
  - EventStore treats admission as issuer and realm membership, while Tenants' UI checks a role. Denial becomes inconsistent across interfaces, violating the FR-11 "any supported interface" requirement.
- Recommendation:
  - Add required clients, audiences and roles/claim mappings (environment-neutral) to the module declaration.
  - Platform composes one realm definition per environment (local, CI, staging, production) from those declarations.
  - Fix the local/CI issuer once: for example, a Keycloak container with the composed realm, or EventStore AD-10 symmetric mode restricted to CI.
  - Define admission as a single mechanism, e.g. production-realm membership granted only by Administrator and enforced by every AD-10 host's issuer and audience check.
  - State that realm configuration is Platform-managed configuration, applied before promotion and outside application rollback (AD-3).
- Suggested disposition: discuss

### RUB-7 — Staging isn't required to run the production runtime profile, which weakens the E2E gate
- Severity: high
- Where: AD-2; Consistency Conventions → "Production profile authority" (line 128); Staging gate row
- Finding: Only `deploy/dapr/production-profile.yaml` binds runtime, components, resiliency and ACLs. AD-2 allows "separately versioned environment configuration" without limiting what it may change. Staging could therefore run different state-store or broker types and still produce "exact-release" E2E evidence. That brings AD-2's "testing one release and deploying another" back at the runtime layer.
- Evidence:
  - Spine AD-2: "Promote the same package/images with separately versioned environment configuration and current secret references."
  - Spine: "Platform owns the canonical `deploy/dapr/production-profile.yaml` … Bind tested runtime/components, scopes/ACLs, resiliency…".
  - EventStore AD-26: "Redis is Development/test only".
- Failure scenario: On the single shared node, staging runs Redis state and pub/sub to save resources, while production runs PostgreSQL actor state and a durable broker. E2E passes in staging. Production then shows different redelivery and actor-state behavior, triggering rollbacks or leaving delivery defects undetected.
- Recommendation: Add: "Staging runs the canonical production profile. Environment configuration may change only instance endpoints, credentials, hostnames, realm/issuer and replica scale. Any other difference is a separate profile, and staging evidence under it does not authorize production promotion."
- Suggested disposition: autofix

### RUB-8 — The Projects availability and RPO-0 envelope assigned to Platform has no owner
- Severity: high
- Where: Source Precedence → Projects row (line 163); Deferred "Module service-level and functional qualification"
- Finding: Projects AD-28 says the platform configuration and evidence must enforce 99.9% monthly availability and committed-event RPO 0. The spine "preserves" those targets but supplies no envelope capable of meeting them: one node, local storage, backup-restore DR, and a PRD that sets no uptime target. It then hands ownership back to "module owners with Platform". Folders' equivalent conflict was settled by a user override; this one is neither settled nor raised as an open question.
- Evidence:
  - Projects spine AD-28: "The platform AppHost owns environment topology … and primary-region recovery policy … The platform configuration and evidence must enforce 99.9% monthly availability, 15-minute service RTO, accepted-task recovery … within five minutes, and committed-event RPO 0".
  - Spine: "Preserve the 99.9% target, five-minute task recovery/NeedsAttention and primary-domain committed-event RPO 0 qualification; Platform disaster targets do not prove them."
  - PRD: "No numeric uptime percentage … is established".
  - Structural Seed: "one Kubernetes node with local storage".
- Failure scenario: Projects' fail-closed AD-30 release gate needs this evidence, while Platform epics build a single-node backup/restore envelope. Projects can never produce the evidence, so its production enrollment is blocked indefinitely, or the target is quietly waived.
- Recommendation: Raise an explicit open question for the user. Either apply a Folders-style override to Projects AD-28's infrastructure clauses, or add replication/HA capability for the stated durability domain to the Platform envelope. Record the answer in Source Precedence.
- Suggested disposition: discuss

### RUB-9 — Platform's own versioning and adoption contract is silent
- Severity: high
- Where: Consistency Conventions → "Module declaration authority" and "Local tool and readiness"; AD-2; Deferred "Enrollment schema/validator…"
- Finding: Three things are undecided:
  - Each module workspace pins Platform twice: as a direct Git submodule (FR-2) and as a pinned .NET tool in `.config/dotnet-tools.json`. No rule says which is authoritative or that the two must match.
  - There is no evolution or compatibility policy for the enrollment schema. The Platform-root complete environment composes seven module declarations that may sit at different schema versions.
  - There is no intake rule for how module releases enter the single Platform Helm package: who selects versions, which module evidence is a prerequisite (e.g. EventStore `release-available`), and how a single-module hotfix ships.
- Evidence:
  - PRD FR-2 section: "A module workspace includes Platform as a direct Git submodule".
  - Spine: "Provide a pinned consumable Platform .NET tool registered in the module's `.config/dotnet-tools.json`".
  - Spine: "Platform owns one versioned, non-secret enrollment schema/validator".
  - Deferred: "Define one shared version before consumers implement against it" (covers only the first version).
  - Spine AD-2: "Publish and retain one immutable versioned Helm package".
- Failure scenario:
  - Parties pins tool 1.3 while its Platform submodule sits at 1.5. The launched composition code differs from the Platform source being debugged, which is AD-4's own failure mode, for Platform itself.
  - Projects moves to schema v2 while Parties stays on v1, and the complete-environment validator rejects one of them.
  - Module teams publish images expecting deployment, but nobody owns Platform release intake, so production drifts.
- Recommendation: Add a "Platform versioning" convention:
  - One Platform version covers tool, schema and composition.
  - The tool refuses to run when it doesn't match the root-declared Platform submodule, or the spine drops one of the two pins.
  - The schema is additive within a major version, and the validator accepts N and N-1 majors.
  - Module intake is a versioned release manifest in the Platform repo that pins module package and image digests. It is updated by a PR that requires the module's release evidence.
  - A single-module fix ships as a new Platform release.
- Suggested disposition: discuss

### RUB-10 — Existing composition roots and module deployment manifests aren't classified
- Severity: medium
- Where: AD-1; Structural Seed note (line 207); Consistency Conventions → "Migration"
- Finding: The spine mentions only the Works preview. Three other composition surfaces are left unclassified:
  - FrontComposer.AppHost already composes EventStore, Tenants, Parties and UIs, with its own realm. Parties I1a names it as a candidate owner of the integrated topology.
  - This repo's README and AppHost declare it the Agents EXT-HOST-1 host, a non-MVP consumer of the same `apphost.cs`.
  - Memories ships its own `deploy/kubernetes` manifests.

  AD-1's "one Aspire application model" and AD-2's single Helm package don't say whether these are legacy surfaces retired under the Migration convention or sanctioned parallel paths. Parties I1a also requires the platform AppHost owner to be explicitly approved.
- Evidence:
  - `references/Hexalith.FrontComposer/src/Hexalith.FrontComposer.AppHost/Program.cs` calls `AddProject<HexalithEventStore>("eventstore")`, `AddProject<HexalithTenants>("tenants")`, `AddProject<HexalithParties>("parties")` and `AddProject<HexalithTenantsUI>("tenants-ui")`.
  - Parties spine I1a: "The target integrated local topology is owned by FrontComposer.AppHost or an explicitly approved platform AppHost owner".
  - README: "This repository is the **EXT-HOST-1** delivery target for Hexalith Agents".
  - `apphost.cs`: "The Platform AppHost is also the clean-checkout Agents composition root."
  - Memories seed: "`deploy/kubernetes/` # Server plus gated MCP, telemetry, PostgreSQL, Redis, FalkorDB, Dapr".
- Failure scenario:
  - Parties adds new wiring to FrontComposer.AppHost (permitted by its own I1a) while Platform builds the enrollment path, producing two integrated graphs.
  - Memories keeps updating `deploy/kubernetes`, which duplicates the Platform Helm package, so two owners maintain the hosted Memories topology.
  - Agents Story 5.6 hand-wires Agents into `apphost.cs` and bypasses the declaration schema.
- Recommendation: Add to Migration or Structural Seed:
  - FrontComposer.AppHost, module AppHosts and module Kubernetes manifests are legacy rollback surfaces retired under the Migration convention.
  - Platform is the approved platform AppHost owner; add "record Parties I1a approval" to Deferred.
  - Non-MVP consumers (Agents EXT-HOST-1, the Works lane) enroll through the same declaration schema when they leave preview.
  - Technical modules supply images, digests and helper packages, not deployment manifests.
- Suggested disposition: autofix

### RUB-11 — Whether module- or FrontComposer-hosted MCP servers may coexist with AD-11 is undecided
- Severity: medium
- Where: AD-11; line 44 ("Module-owned presentation transports remain valid"); Deferred "Hosted HTTP MCP…"
- Finding: The spine says a hosted MCP server is "not required", which is not the same as "not permitted". Memories plans a gated production MCP host. Projects expects "FrontComposer/platform" to compose MCP. Parties moves its MCP plumbing to the FrontComposer MCP host. The spine doesn't say whether those hosts are deployed and routed in Platform environments, or how they relate to McpCli's eligibility rules.
- Evidence:
  - Spine AD-11: "no MVP hosted HTTP MCP server, remote plugin loader or second registry is required".
  - Memories: "Production independently scales Server and gate-approved MCP".
  - Memories ledger: "Kubernetes base runs the MCP deployment at `replicas: 2`".
  - Projects AD-29: "FrontComposer/platform composes MCP over the same versioned contracts".
  - Parties spine: "MCP plumbing → FrontComposer MCP host on Commons.Http (G11)".
  - `src/Hexalith.FrontComposer.Mcp` exists.
- Failure scenario: The same operations end up on three MCP surfaces (McpCli, Memories.Mcp, FrontComposer.Mcp), each enforcing eligibility and confirmation differently. Platform ingress either rejects them as undeclared "alternate transport" or exposes them without surface policy.
- Recommendation: Choose one:
  - In MVP hosted environments, McpCli stdio is the only MCP surface, and module or FrontComposer MCP hosts are not deployed or routed until an AD admits them under the RUB-5 surface-identity contract.
  - Or admit them explicitly as declared module presentation transports.
- Suggested disposition: discuss

### RUB-12 — AD-4 doesn't say how undeclared dependencies resolve, or how it maps onto EventStore AD-11's source switch
- Severity: medium
- Where: AD-4 (line 70)
- Finding: AD-4 combines "only its directly declared dependencies", "missing required source fails" and "No … package fallback". That leaves two things open:
  - whether a Hexalith library the root does not declare but a source dependency needs (e.g. FrontComposer or Commons under Tenants) must be declared, or resolves as a pinned package;
  - whether local mode sets EventStore AD-11's `UseHexalithProjectReferences=true` or uses a Platform-specific mapping.
- Evidence:
  - Spine AD-4: "Resolve its active module and only its directly declared dependencies, including libraries, into one mapping … missing required source fails … No ancestor, sibling, recursive, nested-Platform or package fallback."
  - EventStore AD-11: "Package mode is default; source mode requires explicit `UseHexalithProjectReferences=true` and a root-declared available source … Unset … is package intent in every configuration, including Debug."
  - Memlog L35: Parties' `Directory.Build.props` infers per-dependency source switches from file existence.
- Failure scenario: A Parties workspace builds Tenants from source, and Tenants references FrontComposer, which Parties hasn't declared. One team treats that as a hard failure and adds many submodules. Another lets MSBuild fall back to a package, which violates "no package fallback". Workspaces diverge, and the same library ends up as both a source copy and a package copy.
- Recommendation: Add:
  - "Root-declared Hexalith dependencies are source (Debug project references). Every other dependency resolves as a package at the root's Builds catalog version."
  - "Source and package copies of the same Hexalith package identity in one build fail."
  - "Local mode sets EventStore AD-11's source switch from the single mapping. File-existence inference is removed."
- Suggested disposition: autofix

### RUB-13 — Release records and recovery points aren't bound together or made to survive site loss
- Severity: medium
- Where: AD-2; AD-12; Release and Recovery Acceptance → "Deployment ownership" and "Backup coverage and cadence"
- Finding: Nothing requires each recovery point to record the working release and configuration identity. Nothing requires release records, the previous-working-release pointer, or retained packages and images to live outside the primary failure domain, or to be retained at least as long as the 30-day recovery window. Helm's own release history lives in in-cluster Secrets.
- Evidence:
  - PRD glossary: "Recovery point: a usable copy of authoritative application data with the compatible application version and configuration".
  - Addendum: "Record the application release and configuration compatible with each recovery point."
  - Spine: attempt state is recorded "durably beyond the runner process" (not "beyond the site").
  - Spine AD-12: "restore compatible retained applications/configuration".
- Failure scenario: After site loss, the operator restores a 20-day-old recovery point. The release history was in cluster Helm Secrets or on the runner's disk, so the matching release is guesswork. Registry cleanup has already removed images older than 14 days, so the compatible release can't be deployed at all.
- Recommendation: Extend the backup row:
  - Each recovery point records its working release and configuration identity.
  - Release records and retained packages and images are stored off the primary failure domain and kept at least as long as the oldest retained recovery point.
  - In-cluster Helm history is never the release record.
- Suggested disposition: autofix

### RUB-14 — The hosted-interface naming contract between module declarations and environments is undefined
- Severity: medium
- Where: Consistency Conventions → "Hosted interfaces" (line 130)
- Finding: "Modules declare supported routes and interface hosts" doesn't say whether modules declare environment-neutral logical names or FQDNs. It also doesn't fix how Platform maps them (subdomain or path). That mapping drives Keycloak redirect URIs and web origins, cookie domains, CORS, McpCli profile gateway URLs, and E2E/smoke endpoints across `hexalith.com` and `tache.ai`.
- Evidence: Spine: "Staging uses `hexalith.com`; production uses `tache.ai` … modules declare supported routes and interface hosts."
- Failure scenario: Tenants declares `tenants.hexalith.com` while Parties declares the path `/parties`. The promoted release's Tenants redirect URIs are wrong on `tache.ai`, and production verification fails.
- Recommendation: Add: "Modules declare logical interface names and route prefixes only. Platform maps them to environment FQDNs using one seed pattern and derives redirect URIs and origins. No module artifact or configuration contains an environment FQDN."
- Suggested disposition: autofix

### RUB-15 — GitHub delivery and off-site freshness monitoring are left undefined, though the PRD assigns them to architecture
- Severity: medium
- Where: Consistency Conventions → "Diagnostics and notification"; "Recovery freshness" row; Deferred "Release attempt state, checks and notifications"
- Finding: The spine restates the requirements, but chooses neither a delivery mechanism nor where the freshness and failure monitor runs. The only Platform executor, the AD-7 runner, sits on the primary site's private network.
- Evidence:
  - Addendum: "The delivery mechanism and Administrator's GitHub account mapping remain implementation configuration. Architecture must define them".
  - PRD FR-9: "Failure reporting remains usable when the primary environment is down"; the addendum adds "architecture chooses how."
  - Spine: "site-loss failure reporting must survive the primary site".
- Failure scenario: The freshness check is implemented as an in-cluster CronJob or a job on the self-hosted runner. Site loss silences both backups and alerts, so the RPO breach goes unreported.
- Recommendation: Decide:
  - A GitHub-hosted scheduled workflow reads the newest usable recovery-point metadata from the off-site store and opens or updates a GitHub issue assigned to Administrator.
  - Deployment and recovery outcomes post to the same channel.
  - The on-site runner is never the only reporter.
- Suggested disposition: autofix

### RUB-16 — Operational envelope gaps for the shared single-node installation
- Severity: medium
- Where: Consistency Conventions → "Diagnostics and notification"; AD-3; AD-8; Structural Seed
- Finding: Four operational gaps:
  - No hosted telemetry sink or retention, and no production outage detection or alerting owner, although the RTO clock includes detection.
  - No resource isolation between staging and production on the shared node (quotas, priority classes, limits).
  - No change path for shared infrastructure (the cluster-wide Dapr control plane, Keycloak, ingress, CNPG/OpenBao operators). These upgrades hit both environments at once and bypass the staging gate, and AD-3 excludes them from application rollback.
  - Cost and capacity are silent; the spine doesn't even say "no target".
- Evidence:
  - PRD NFR-2: RTO covers "Service outage to verified restoration, including detection".
  - Spine: "Later incidents are outside this automatic policy".
  - Spine: "Recorded cluster inspection found one Kubernetes node".
  - Spine AD-3: "Keep … shared infrastructure … outside application rollback ownership."
- Failure scenario:
  - A Dapr control-plane upgrade made for a staging need changes production sidecar injection with no gate.
  - A staging load test evicts production pods, and "existing unhealthy production" then blocks promotion.
  - A night-time outage goes unnoticed and consumes the four-hour RTO.
- Recommendation: Add conventions:
  - Production gets a PriorityClass and staging gets ResourceQuotas and limits.
  - Shared-infrastructure changes follow a Platform-owned change procedure, separate from application releases. It is staged where possible and requires a production-profile digest change when bound.
  - Seed a hosted OTel sink per environment and an off-site availability probe that notifies Administrator.
  - State "no cost target; capacity budget is seed".
- Suggested disposition: discuss

### RUB-17 — Ownership of the test-fixture lifecycle is split ambiguously
- Severity: medium
- Where: Consistency Conventions → "Local tool and readiness" (line 129); AD-10
- Finding: The convention names both "thin manifest-aware fixtures" (Platform) and "shared reusable fixtures … owned by `EventStore.Testing(.Integration)`" without saying which one starts, retains and disposes the run-owned environment. The standard Aspire testing fixture disposes its AppHost on teardown, which defeats AD-10's retention of failed local runs.
- Evidence:
  - Spine: "thin manifest-aware fixtures. Shared reusable fixtures remain owned by `EventStore.Testing(.Integration)`".
  - Addendum: "normal test-fixture disposal must not silently defeat the agreed retention of failed local environments."
- Failure scenario: Projects uses Platform fixtures, which retain failed runs. Parties uses an EventStore.Testing fixture, which disposes on failure, so its failed runs lose their environment and the two modules keep different cleanup-ownership records.
- Recommendation: Add: "Only the Platform tool starts, retains and cleans environments. EventStore.Testing fixtures consume the endpoints and composition identity it supplies and never own environment disposal."
- Suggested disposition: autofix

### RUB-18 — The module-declaration contents are scattered and incomplete
- Severity: low
- Where: Consistency Conventions → "Module declaration authority" (line 124)
- Finding: The declaration list is the main cross-team contract, but it omits several items that other rows rely on:
  - production smoke suites (used in "Before production update");
  - the startup-timeout override;
  - interface names and routes;
  - surface eligibility (AD-11);
  - secret and identity needs;
  - rollback-compatibility evidence (RUB-3).
- Evidence: Spine line 124 list versus lines 129, 130 and 142, and AD-11.
- Failure scenario: The first schema version ships without smoke or eligibility fields. Modules then encode them ad hoc in different places.
- Recommendation: Make line 124 one enumerated list that covers every module-supplied input referenced anywhere in the spine.
- Suggested disposition: autofix

### RUB-19 — Quiet PRD details that were dropped
- Severity: low
- Where: Release and Recovery Acceptance → "Verification"; AD-12
- Finding: Three PRD details are missing:
  - FR-7 "Verification targets the intended release", and the addendum's "check runner must identify the intended release rather than accidentally verifying only an old replica". The Verification row doesn't say this.
  - The SM-4 re-verification "after access-configuration changes" isn't bound.
  - Nothing suspends automatic promotion while a DR restore is fencing writers.
- Evidence: PRD FR-7; addendum "Failure triggers" item 3; PRD SM-4; spine AD-12 "Fence old writers".
- Failure scenario: A smoke check hits a surviving old replica and passes, so a broken release is marked "working". Separately, a scheduled promotion runs mid-restore.
- Recommendation: Add these to the Verification row: smoke results must attest the serving release identity; an access-configuration change triggers the NFR-3 isolation suite; automatic promotion is suspended while any recovery is in progress.
- Suggested disposition: autofix

### RUB-20 — Diagram and hygiene polish
- Severity: low
- Where: Both Mermaid blocks; Stack; Source Precedence opening paragraph
- Finding:
  - Structural Seed labels use `\n` inside quoted Mermaid labels. Many renderers show that literally; `<br/>` is the portable form.
  - In the paradigm diagram, `Memories → Adapter → Falkor` is disconnected from Host/Composition, and `Host` and `Technical` duplicate the same concept.
  - The Stack omits the Aspire CLI, although AD-10's lifecycle wraps `aspire start`/`aspire stop` (memlog L108). The README still says "Aspire CLI `13.4.6` or later" against AppHost SDK 13.5.4.
  - The Source Precedence opening paragraph narrates decision history that belongs in the memlog.
- Evidence: Spine lines 38–39, 188–204 and 166–178; README "Requirements".
- Failure scenario: A developer on Aspire CLI 13.4.x runs the AD-10 lifecycle against a 13.5.4 AppHost and gets untested `start`/`stop` targeting semantics.
- Recommendation: Use `<br/>` in labels. Connect Memories under Host, or merge `Host` and `Technical`. Add an Aspire CLI pin to the Stack or the tool manifest. Trim the Source Precedence prose to the binding rule.
- Suggested disposition: autofix

## Checked and clean
- **FR-1..FR-12 and NFR-2/NFR-3 each land.** Each is covered by an AD or acceptance row, and the capability map and frontmatter `binds` list all 15 IDs.
- **PRD numeric policy is reproduced exactly.** This covers:
  - the 10-minute test-startup default with justified finite override and recorded actual duration;
  - the 10-minute rollout, 5-minute window, 60 s unavailability, and two consecutive smoke failures 30 s apart;
  - one recovery attempt with a 10-minute restore, first-deployment stop, and promotion halt;
  - 30-minute backups, 7/30-day retention, the 1-hour freshness alert, pre-production/monthly/after-change drills, RPO ≤ 1 h and RTO ≤ 4 h.
- **PRD non-goals are honored.** There is no mocking subsystem, no traffic-metric rollback, no data or event rewind (AD-3), and no standby (AD-12, Deferred).
- **Counter-metrics SM-C1..C5 are reflected.** Wrong-release, missing and skipped results block promotion; cleanup leftovers are reported; false recovery is excluded; startup duration is recorded.
- **Local lifecycle choices match the addendum's selected options.** AD-10 implements retain-on-local-failure, clean-on-cancel (own resources only), always-clean CI, explicit attach without teardown, and idempotent attributable cleanup.
- **The Folders override matches memlog L153–154.** It is bounded correctly: behavior, authorization and data-safety gates are still required, and no Folders-only profile or v2 gate is imposed.
- **EventStore contracts are ratified consistently.** This covers:
  - AD-24 (Platform owns the value-free OpenBao secret contract);
  - AD-33 (the Platform-owned routing-catalog instance with prepare/ready/commit);
  - AD-26 (still marked unratified, with no qualification implied);
  - AD-22 (exact-subject consumer-removal authority accompanies parity);
  - AD-10 vs AD-28 (kept distinct);
  - AD-11 (a mutable tag is never authority).
- **The McpCli deltas are declared.** AD-11 adds connected discovery, and Deferred explicitly requires amending McpCli's static-availability and package/source-mode contracts, which satisfies item 7 for McpCli.
- **Tenants constraints are preserved.** The Tenants UI host, the BFF-to-REST read transport, and the one-replica InteractiveServer limit are all kept.
- **Memories erasure and recovery constraints are preserved.** Memories.Aspire remains the digest owner (consistent with Memories AD-19). The Memories AD-2/AD-16/AD-21 rules hold: no restore of Redis/FalkorDB projections, a copied register isn't authority, unknown lineage fails closed, and the tombstone mirror blocks recoverable production.
- **Known brownfield conflicts are declared as reconciliation work rather than ratified.** This covers the Works sibling/nested paths, fixed Dapr ports 50001/51005/51006 and the shared scheduler volume (`DaprSelfHostedMtls.cs`), and the missing McpCli root reference.
- **The Stack matches the root files.** `global.json` has 10.0.401 with latestPatch; `apphost.cs` has Aspire.AppHost.Sdk 13.5.4, Docker/Redis 13.5.4, CommunityToolkit Dapr 13.5.1-beta.757, and Hexalith.EventStore.Aspire 3.106.0.
- **Both Mermaid blocks are structurally valid.** Both use `flowchart`, and the subgraph syntax, node IDs and edges are well-formed.
- **AD-8's separate data-service instances per environment are at least as strict as the PRD's isolation requirement.** The single-node, no-HA caveat is stated honestly.
