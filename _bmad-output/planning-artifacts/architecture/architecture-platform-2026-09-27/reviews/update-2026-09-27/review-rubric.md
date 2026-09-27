# Rubric Review — Platform Architecture Spine (update run 2026-09-27)

- **Subject:** `ARCHITECTURE-SPINE.md` (updated 2026-09-27 18:35, status draft)
- **Authority checked:** `.memlog.md` entries from "Update run started" onward (V-01..V-67 settlements, Themes 1–5, stack evidence)
- **Driving PRD:** `../../prds/prd-platform-2026-09-27/prd.md` (FR-1..FR-12, NFR-1..NFR-3, SM-1..SM-6), addendum hosted-architecture questions
- **Lens:** good-spine rubric walker (divergence coverage at initiative altitude, rule enforceability, deferral safety, tech currency, brownfield ratification, PRD coverage, altitude dimensions, internal consistency). Read-only; user-adopted decisions are not re-litigated.

## Verdict

**Conditional pass — no critical findings.** The update run carried every Theme 1–5 settlement into the spine: AD-13, AD-14 and AD-15, the amendments to AD-1, AD-3, AD-7, AD-9 and AD-12, the release and recovery policy rows, and the defers V-51 and V-63. FR-1..FR-12 and NFR-1..NFR-3 each map to at least one AD, and every AD appears in the capability map. Local links resolve. The spine can go to epics after the three high findings are fixed; each is a one- to two-sentence edit or a new Deferred row.

| Severity | Count |
| --- | --- |
| Critical | 0 |
| High | 3 |
| Medium | 15 |
| Low | 11 |
| **Total** | **29** |

Primary dispositions: 24 autofix, 5 discuss (R-03, R-08, R-09, R-10, R-14), 0 ignore. R-03, R-13 and R-14 also carry a secondary disposition, and R-07 carries a defer component.

### Rubric scorecard

| Criterion | Result | Notes |
| --- | --- | --- |
| Fixes the real divergence points for features/epics across 7 modules | Mostly | Gaps: composed host in local/source mode (R-01); module DR hook contract (R-06); declaration omits inputs that the DR and isolation rules depend on (R-05); technical vs domain classification (R-14) |
| Every AD Rule enforceable and prevents its divergence | Mostly | AD-4 tool/submodule comparison (R-17); AD-13 local mode (R-01); AD-8 patch currency (R-26); AD-7 wording vs AD-8 residual risk (R-18) |
| Nothing under Deferred lets two units diverge | Mostly | ServiceDefaults has three owners (R-15); the "broker replacement" row is misframed (R-04); the local mTLS rule sits inside a Deferred row (R-24) |
| Named tech verified-current | Yes, with pin nits | Re-verified 2026-09-27: Aspire.AppHost.Sdk/Aspire.Cli 13.5.4, Aspire.Hosting.Kubernetes 13.5.4-preview.1.26464.4, CommunityToolkit Dapr 13.5.1-beta.770, Hexalith.EventStore.Aspire 3.109.0, Dapr 1.18.4, Helm 4.3.0. Pin precision in R-20/R-21. GitHub plan features unverified (R-09) |
| Ratifies rather than contradicts brownfield | Mostly | The Builds Zot registry (`registry.hexalith.com`) is not ratified (R-13). Memories spine names Memories.Aspire the single digest owner, which conflicts with AD-3 (R-02). EventStore AD-26 hands the durable-broker choice to Platform Operations (R-04) |
| Covers FR-1..FR-12, NFR-1..NFR-3 | Yes | The narrowing of FR-12/SM-1 by AD-14 is not signposted (R-27) |
| Altitude dimensions decided/deferred/open | Gaps | Public exposure, DNS and ACME for a private-address installation (R-03); where environment-layer definitions live and who holds cluster-admin custody (R-07); probe hosting (R-10) |
| Internal consistency | Gaps | Memories digest authority stated three ways (R-02); test AppHost roots vs AD-10 (R-11); overloaded "release workflow" (R-12); unqualified AD refs (R-22); AD-2 binding list vs profile row (R-23) |

---

## Findings

### R-01 — AD-13 composed host has no local/source-mode rule (contradicts AD-4 and FR-2)

- **Severity:** high
- **Location:** AD-13 Rule; AD-4; Deferred › Owned work › "Composed host"
- **Issue:** AD-13 says every environment runs one `eventstore` host "built by Platform from the released EventStore server package plus enrolled modules' extension packages". AD-4 says root-declared Hexalith dependencies build from source in Debug. In the EventStore workspace, or in the Folders workspace where the active code is Folders' idempotency-intent adapters loaded into the eventstore host, FR-2 requires source changes and breakpoints to reach the running composed host. Neither AD says how the local or CI composed host is assembled from source projects or packages. The hosted-only phrase "Platform release workflow issues the composed image's identity" is left to cover local mode too. Implementers of the Platform runner and the EventStore extension API could build this in incompatible ways: generated project vs plugin load vs stock host.
- **Fix:** Append to AD-13: "In local and CI modes the composed host is assembled per the AD-4 mapping. Root-declared EventStore and extension projects build from source in Debug; others are packages at the Builds catalog version. Duplicate detection is identical in both modes. Only hosted environments require the Platform-issued composed image."
- **Disposition:** autofix

### R-02 — Memories data-service image digests have three conflicting authorities

- **Severity:** high
- **Location:** Design Paradigm ¶2 ("Memories.Aspire supplies qualification digests consumed through the profile inventory"); AD-3 ("exactly one version authority each: a profile facet, with module digest sets as qualification inputs"); Consistency Conventions › Production profile ("Per-release bindings — … module and Memories.Aspire image digests — live in the release record"); Deferred › Infrastructure currency ("through the Memories digest set")
- **Issue:** The Redis Stack/Redis 8 and FalkorDB images are environment-layer data services (AD-3). The Paradigm says their digests flow through the profile inventory. The Production profile convention says they are per-release bindings in the release record, which would make them roll back with the application and contradicts AD-3's forward-only rule. The Memories spine (§ ~line 202) names `Hexalith.Memories.Aspire` the "single owner of qualified container image digests" consumed by `deploy/kubernetes`. Memlog V-20 and V-36 are both user-adopted, but the spine carries them as incompatible sentences. Memories and the Platform environment layer could each pin a different image.
- **Fix:** Reconcile in the Production profile row: "Memories.Aspire publishes the qualified data-service digest set. The environment-layer profile facet is the only deploy authority for those images and must equal that set. The release record binds the digest-set reference as qualification evidence, never as a rollback-restored deployment input. A mismatch blocks the attempt." Align the Paradigm sentence to that wording.
- **Disposition:** autofix (wording reconciliation of V-20/V-36; discuss only if the user intended release-record authority)

### R-03 — External exposure, DNS authority and certificate issuance for a private-address installation are neither decided nor deferred

- **Severity:** high
- **Location:** Consistency Conventions › Hosted interfaces; Release and Recovery › Detection and response, Production entry gates; AD-7 ("verification endpoints"); PRD addendum line 264 lists "ingress, DNS, certificates" as architecture work
- **Issue:** The designated installation is `192.168.1.30`, a private RFC 1918 address. Several rules assume reachability that no rule provides:
  - The off-site probe (a GitHub-hosted runner) must reach production ingress.
  - Hosted McpCli users reach the "selected environment gateway".
  - Certificates must be issued for `hexalith.com` and `tache.ai`: ACME HTTP-01 needs public reachability, and DNS-01 needs DNS-provider credentials, which are themselves a secret with an environment dimension.
  - With ingress closed at G1, and for a failed first deployment, the production executor still needs "verification endpoints" that are not the public ingress.

  The spine fixes hostname ownership and issuer restriction but not the exposure path (NAT/port-forward, reverse proxy, tunnel), the DNS authority for each zone, the ACME challenge type, or the verification path. Probe, ingress, certificate and executor implementers would each assume a different topology.
- **Fix:**
  - Add a Hosted interfaces clause: "Public exposure of each environment's ingress uses one declared path. DNS records for each zone have one named owner. Certificates use one declared ACME challenge whose credentials are per-environment secrets. Executors verify through an internal endpoint that applies the same authentication as public ingress."
  - Add a Deferred › Owned work row, "Exposure, DNS and ACME path | Administrator with Platform | Before G1".
  - State that the off-site probe targets the public production endpoint.
- **Disposition:** discuss (choose the exposure path) + autofix (Deferred row)

### R-04 — Durable production broker selection is misframed as a measurement-gated "replacement"

- **Severity:** medium
- **Location:** Stack footnote ("No broker brand is adopted"); Deferred › Owned work › "Broker replacement, warm standby, regions or scale"; "Shared runtime and profile evidence" row
- **Issue:** EventStore AD-26 requires "an approved durable broker" and marks Redis pub/sub as Development/test only. EventStore's deferred table assigns "Durable production broker and delivery retention" to Platform Operations before an AD-26 profile can pass. The memlog records broker selection as production-profile qualification seed. The spine has no current production broker to "replace", yet its only broker row defers the work until a measured requirement exists. An owner reading the Deferred table could postpone the broker indefinitely while EventStore's profile proof waits on it.
- **Fix:** Add to the "Shared runtime and profile evidence" row: "select and qualify the approved durable production broker (component type, delivery retention, dead-letter and backlog recovery; Redis pub/sub excluded per EventStore AD-26) before production profile proof". Rename the later row "Broker change, warm standby, regions or scale".
- **Disposition:** autofix

### R-05 — Module declaration omits inputs that the DR, isolation and quota rules depend on

- **Severity:** medium
- **Location:** Consistency Conventions › Module declaration ("It enumerates every module input this spine references"); AD-8; DR sequence step 1; Lost-window erasures; External effects
- **Issue:** The declaration claims to enumerate every module input the spine references, but it misses several that other rules consume:
  - DR step 1 and the Lost-window row require Platform to "disable external-effect and destructive retention workers", but modules never declare which workers those are or how to disable them.
  - AD-8 requires per-environment "external-provider tenancy", but external providers are not declared.
  - AD-8 staging quotas and limits, the Tenants InteractiveServer replica rule and volume needs require per-workload resource requests and limits, replica count and persistent-volume needs, but nothing says whether modules declare these or Platform assigns them.

  Seven modules would express these inputs ad hoc.
- **Fix:** Extend the enumeration with:
  - external-effect and destructive-retention workers, with their disable control;
  - external providers needing per-environment tenancy;
  - workload resource requests and limits, replicas (default 1) and persistent-volume needs.
- **Disposition:** autofix

### R-06 — No shared contract for module recovery hooks

- **Severity:** medium
- **Location:** Deferred › First shared versions; AD-12 ("modules own state classes, recovery boundaries, reconciliation and integrity checks"); DR sequence steps 3 and 5
- **Issue:** The DR sequence runs "module admission and purge, replay rebuild-only state" and cross-module integrity checks, and the G2 drill runs them for every enrolled module. The invocation and result contract for these hooks is not a first-shared-version artifact, so each module will implement DR hooks with different entry points, idempotency and result shapes. A uniform, timed four-hour procedure is then impossible.
- **Fix:** Add a first-shared-versions row: "Recovery hook contract (quarantine admission, purge, rebuild/replay, integrity check, external-effect reconciliation, result/evidence shape) | Platform | All modules | First AD-12 drill / G2".
- **Disposition:** autofix

### R-07 — Environment-layer definition, custody and sequencing are unowned; AD-7 → AD-12 reference dangles

- **Severity:** medium
- **Location:** AD-3; AD-7 ("cluster-scoped prerequisites stay outside the application package"; "AD-12 requires independently usable recovery execution"); AD-6 ("applied forward-only before the attempt that needs it"); Release and Recovery › Attempt ownership
- **Issue:** Four related gaps:
  - AD-3 creates a forward-only environment layer (data services, brokers, PVs, CRDs, Dapr control plane, Components with app-ID scopes) but not where its definitions live, which identity applies them, or how they are reproduced onto replacement capacity within the four-hour RTO. Deploy identities are namespace-scoped, staging has no Dapr control-plane write, and no cluster-admin custody is named.
  - Only the realm contract gets a "before the attempt that needs it" rule. A release that enrolls a new app ID also needs Component scopes, OpenBao policies and secret namespaces to exist first, and nothing orders them.
  - AD-7 says "AD-12 requires independently usable recovery execution", but AD-12 does not say this.
- **Fix:**
  - AD-3: add "environment-layer definitions are versioned in the private operations repository, applied under the attempt lock by a named cluster-scoped identity held outside both executors' routine jobs, and reproducible onto replacement capacity".
  - Generalize AD-6's clause: "every environment-layer input a release needs is applied forward-only and verified before the attempt".
  - AD-12: add "recovery execution (runner or operator workstation) independent of the primary site".
- **Disposition:** autofix (sequencing and AD-12 text) + defer (IaC mechanism to "Recovery capacity and coverage")

### R-08 — Environment-layer objects share the application namespace with a namespace-scoped deploy identity

- **Severity:** medium
- **Location:** AD-7 ("Each deploy identity is namespace-scoped"); AD-8 ("one application namespace … own data-service instances, OpenBao instance"); Structural Seed ("Staging namespace: apps, data services, OpenBao"); AD-3
- **Issue:** The data services, OpenBao, PVCs and bootstrap Secrets appear to live in the same namespace the application deploy identity controls. A namespace-scoped role can then delete or patch environment-layer objects, `exec` into data or OpenBao pods, or read bootstrap Secrets. AD-3 places these outside application release, and this RBAC scope undoes that. Pod Security `restricted` is also stated for the whole namespace without qualifying it for OpenBao (mlock/IPC_LOCK) or the data-service images.
- **Fix:** Choose one of:
  - A per-environment data/OpenBao namespace, outside the application deploy identity's scope.
  - Deploy-identity RBAC limited to release-owned kinds and names, with no `pods/exec` on data pods and no bootstrap-Secret read.

  Add "PSS-restricted compatibility of data services and OpenBao" to the "Secrets, identity, network and transport" qualification row.
- **Disposition:** discuss

### R-09 — GitHub plan prerequisites for the private operations repository are unverified

- **Severity:** medium
- **Location:** AD-7 (runner groups restricted to "named release workflows", "per-job credentials from protected environments", private operations repository); AD-2 (provenance verification)
- **Issue:** GitHub documentation as of 2026-09-27:
  - Environment secrets and deployment-branch policies in private repositories require Pro, Team or Enterprise.
  - Required reviewers and other protection rules in private repositories are not offered below Enterprise.
  - Team organizations can create runner groups scoped by repository. The docs page checked does not document restricting a group to selected workflows, which appears to be an Enterprise feature.
  - Artifact attestations in private repositories require Enterprise Cloud (recalled from the attestation docs; not re-fetched in this review).

  AD-7's controls may therefore not exist on the organization's plan. Without a stated fallback, each implementer will substitute a different one.
- **Fix:** Add a Deferred prerequisite row, "GitHub plan capability check | Administrator | Before executor enrollment". Restate AD-7's control so it is achievable on Team: "runner groups are scoped only to the private operations repository, which contains only release workflows; environments restrict deployment branches". Use required reviewers only if the plan provides them. Build provenance subjects in public repositories, or choose a signing mechanism the plan supports.
- **Disposition:** discuss

### R-10 — Off-site probe host and monitor cadence/heartbeat are undecided

- **Severity:** medium
- **Location:** Release and Recovery › Detection and response ("intervals of five minutes or less"); Recovery point and freshness ("GitHub-hosted monitor, scheduled off the hour"); Diagnostics and notification
- **Issue:** The probe's host is not named. GitHub `schedule` has a five-minute minimum and runs are best-effort: they can be delayed or dropped under load. A five-minute probe in a private repository also uses about 8.6k billed runner-minutes a month, against a Team allowance of 3k. The freshness monitor has no stated cadence, so an hourly run can detect a one-hour RPO breach up to about two hours late. Nothing alerts when the monitor itself fails to run; "notifies on job failure" covers only runs that start. NFR-2 detection and FR-9 alerting depend on both monitors.
- **Fix:** Name the probe host: an external uptime service or off-site VM, notifying through GitHub (for example `repository_dispatch` to an issue). Set the freshness-monitor cadence to 15 minutes or less. Add a dead-man heartbeat so that a missing monitor or probe run notifies Administrator. Record GitHub schedule jitter as a residual risk if GitHub stays the host.
- **Disposition:** discuss

### R-11 — "Technical-module AppHosts stay valid isolated-test roots" contradicts AD-10

- **Severity:** medium
- **Location:** Consistency Conventions › Migration and coexistence; AD-10 ("EventStore.Testing(.Integration), module and McpCli tests … never start their own AppHost")
- **Issue:** Memlog V-40 keeps the EventStore integration-test host valid, while V-04 (reflected in AD-10) forbids EventStore.Testing(.Integration) from starting an AppHost. As written, the EventStore team cannot tell whether its repository-internal integration tests may keep starting EventStore.AppHost, or whether that host must move behind the Platform descriptor.
- **Fix:** Add to the Migration row: "A technical module's own repository tests may start its AppHost as FR-5 isolated or pre-Platform tests. Such runs never count as Platform integration evidence. Shared fixture packages consumed by other modules (EventStore.Testing.Integration) use only the Platform descriptor."
- **Disposition:** autofix

### R-12 — "Release workflow" is overloaded; publication location and provenance subject unclear

- **Severity:** medium
- **Location:** AD-2 ("verifies every digest's provenance against the protected Builds workflow and ref"); AD-7 ("named release workflows"; "Hosted release … workflows … live in a private operations repository"); AD-13 ("The Platform release workflow issues the composed image's identity")
- **Issue:** One term covers two different things: (a) publication, which builds the composed host and application package, and (b) deployment, which runs on the executors from the private operations repository. It is unclear which repository builds and publishes the composed image and chart, and which workflow identity the executors verify. Placing publication in the private repository also triggers the plan limits in R-09.
- **Fix:** Define two terms:
  - "Platform publication workflow": the Platform repository, the Builds reusable workflow, disposable hosted runners. It produces the package, composed image and release record.
  - "Deployment workflows": the private operations repository, on the executors.

  AD-2 then verifies provenance against the publication workflow and ref.
- **Disposition:** autofix

### R-13 — The existing OCI registry is not ratified

- **Severity:** medium
- **Location:** AD-2 (retained OCI package and image digests); Hosted interfaces (hostname ownership); brownfield `references/Hexalith.Builds/.github/workflows/domain-release.yml:471` (`HEXALITH_ZOT_REGISTRY … 'registry.hexalith.com'`)
- **Issue:** Builds already publishes module containers to a Zot registry at `registry.hexalith.com`. The spine never names the registry that holds the retained application package and images, its failure domain (if it runs on the primary node, restores depend on the off-site copy), cluster pull credentials, or who owns that hostname. `hexalith.com` is the staging zone under the hostname-ownership rule, yet this shared-infrastructure host serves production artifacts.
- **Fix:** Add to AD-2 or the Stack: "Retained packages and images live in the Builds Zot registry (`registry.hexalith.com`) with an off-site replica. Pull credentials are per-environment secrets." Carve shared-infrastructure hostnames (registry, Keycloak issuer hosts) out of staging's `hexalith.com` ownership, with a named owner.
- **Disposition:** autofix (ratify brownfield); discuss if the registry runs on the primary node

### R-14 — Module classification (technical vs domain) is never stated

- **Severity:** medium
- **Location:** Design Paradigm ¶2; Migration and coexistence; Deferred › Hosting inventory and retirement; AD-11 ("module and FrontComposer MCP hosts")
- **Issue:** Whether a module's AppHost, Aspire and ServiceDefaults projects are frozen, valid or sample-only depends on its classification, and the spine never gives one:
  - Memories is treated as both. Memories.Aspire supplies qualification digests, a technical-style capability, yet the Memories AppHost/Aspire/ServiceDefaults are listed as frozen like a domain module's.
  - Tenants is a domain module per the root LLM instructions, but it is also everyone's minimum dependency.
  - Builds.Module's AppHost and EventStore.Admin.Mcp (memlog V-48) are unclassified.
- **Fix:** Add a one-line classification to the Migration row:
  - Technical: EventStore, Commons, Builds, FrontComposer, PolymorphicSerializations, and Memories if the user confirms.
  - Domain: Tenants, Parties, Folders, Projects, and the non-MVP domain modules.
  - McpCli: a tool.

  State that the AD-11 MCP ban includes technical-module MCP hosts such as EventStore.Admin.Mcp.
- **Disposition:** discuss (Memories classification); autofix the rest

### R-15 — Shared ServiceDefaults / Aspire Dapr helpers deferral has three owners and no decider

- **Severity:** medium
- **Location:** Deferred › Owned work › "Shared ServiceDefaults and Aspire Dapr helpers | EventStore, Commons, Platform"; Diagnostics convention ("Use shared technical-module health and telemetry")
- **Issue:** Three co-owners and no decider (defer V-51). Until one package set is named, modules enrolling in parallel can wire different ServiceDefaults, health and telemetry stacks, which is the divergence this deferral should prevent.
- **Fix:** "Platform decides, consulting EventStore and Commons. The canonical package set is recorded in this spine before the first module enrolls. Until then, enrollment of new hosting helpers is refused."
- **Disposition:** autofix

### R-16 — AD-9 omits the Memories transition that the memlog adopted

- **Severity:** medium
- **Location:** AD-9 Rule ("all other coordination state … use Dapr"); Source Precedence › Memories; Deferred › Memories conformance
- **Issue:** Memlog Theme 2 (V-07) says the migration of remaining direct Redis coordination "gates production admission, not local/CI enrollment". AD-9 reads as unconditional and binds the "dependency guards". A validator built from AD-9 alone would reject Memories, a mandatory dependency of every domain module, from local and CI enrollment today.
- **Fix:** Add to AD-9: "Memories' remaining direct Redis coordination is a transitional exception until G2; it does not block local or CI enrollment."
- **Disposition:** autofix

### R-17 — AD-4 tool/submodule identity comparison is undefined

- **Severity:** medium
- **Location:** AD-4 ("the pinned CI tool refuses to run when its version differs"); Local tool and readiness
- **Issue:** The submodule identity is a commit SHA and the tool identity is a NuGet version, and nothing says how to compare them. One implementer might compare release tags and another embedded SHAs, and a submodule on an untagged commit has no defined outcome.
- **Fix:** "The Platform tool embeds its source commit, for example in its informational version, and refuses to run unless that commit equals the Platform submodule HEAD. An untagged or dirty submodule is allowed only in local source mode."
- **Disposition:** autofix

### R-18 — AD-7 isolation wording conflicts with AD-8's accepted single-node residual risk

- **Severity:** low
- **Location:** AD-7 ("Staging, test or PR code never runs where production credentials are or were present"); AD-8 ("The single-node shared kernel is an accepted residual risk")
- **Issue:** Read literally, AD-7 is violated by the observed topology: staging pods run on the same node as production pods that hold production credentials.
- **Fix:** Scope it: "…never runs on an executor or host process that holds or held production deployment credentials; co-scheduling of staging and production pods on the shared node is the AD-8 residual risk."
- **Disposition:** autofix

### R-19 — "Profile" vocabulary and the scope of the profile digest are undefined

- **Severity:** medium
- **Location:** AD-1 ("canonical profile"); AD-3 ("profile facet"); Production profile convention ("template", "profile inventory", "equal profile digests"); Rollout ("active profile digest")
- **Issue:** Five profile terms are used without definitions. The promotion gate "requires equal profile digests", but the digest's scope is unstated: template only, or template plus environment-layer inventory. EventStore AD-26 still defines one `deploy/dapr/production-profile.yaml` whose single digest also binds app IDs and catalog digests. The template/binding split is deferred to EventStore ratification, but Platform's own terms should be exact now.
- **Fix:** One sentence in the Production profile row: "The profile digest covers the template plus environment-layer inventory, excluding declared per-environment bindings. Per-release bindings are covered by the release record, not the profile digest."
- **Disposition:** autofix

### R-20 — Helm pin and forbidden-rollback flag are imprecise

- **Severity:** low
- **Location:** Stack › Helm "4.x"; AD-3 ("Helm automatic or atomic rollback is forbidden"); AD-2
- **Issue:** The memlog records that `aspire deploy` needs Helm 4.2.0 or later (latest 4.3.0), but "4.x" admits 4.0 and 4.1. Helm 4 has no `--atomic`; the flag is `--rollback-on-failure` (verified on helm.sh). The spine also does not say that executors never regenerate the chart with `aspire publish` or `aspire deploy`.
- **Fix:**
  - Stack: "Helm ≥ 4.2.0 (4.x)".
  - AD-3: "`--rollback-on-failure` (formerly `--atomic`) is forbidden".
  - AD-2: "executors run `helm upgrade` of the retained package by digest, never `aspire publish` or `aspire deploy`".
- **Disposition:** autofix

### R-21 — Stack pins lack an alignment rule

- **Severity:** low
- **Location:** Stack › CommunityToolkit Aspire Dapr hosting; Aspire CLI
- **Issue:**
  - The CommunityToolkit Dapr pin is beta.757 while the Builds catalog and the latest release are beta.770. Unlike the EventStore.Aspire row, the row does not say that package mode aligns to the catalog.
  - The Aspire CLI minimum of 13.4.6 is below the AppHost SDK 13.5.4 (installed CLI 13.5.3).
- **Fix:** Add "Package-mode pin aligns to the Builds catalog (beta.770)". Raise the CLI floor to "≥ 13.5.4, matching Aspire.AppHost.Sdk".
- **Disposition:** autofix

### R-22 — Unqualified external AD references

- **Severity:** low
- **Location:** Deferred › Hosting inventory and retirement ("until parity and AD-22 authority"); AD-6 ("the distinct AD-28 app-channel contract")
- **Issue:** The spine's own ADs stop at AD-15, and "AD-28" also appears as Projects AD-28 in Source Precedence. Bare external references are ambiguous.
- **Fix:** "EventStore AD-22 authority"; "the distinct EventStore AD-28 app-channel contract".
- **Disposition:** autofix

### R-23 — AD-2's release-record binding list diverges from the Production profile row

- **Severity:** low
- **Location:** AD-2 Rule; Consistency Conventions › Production profile
- **Issue:** AD-2's list omits the secret-contract digest, the app-ID set and the Memories digest-set reference, all of which the Production profile row puts in the record.
- **Fix:** Have AD-2 say "…plus every per-release binding named in the Production profile convention".
- **Disposition:** autofix

### R-24 — Local mTLS/ACL rule stated inside a Deferred row

- **Severity:** low
- **Location:** Deferred › "Secrets, identity, network and transport" ("Local compositions use mTLS, per-receiver ACLs and scoped components; local allow-defaults are never profile inputs")
- **Issue:** This is defer V-63, which is still unratified, but it reads as a binding rule placed among deferred items. It also leaves open whether local compositions must already comply.
- **Fix:** Either move it to Conventions as decided, or rephrase it: "Ratify the existing local mTLS, per-receiver ACL and scoped-component convention before the first enrollment; local allow-defaults are never profile inputs."
- **Disposition:** autofix

### R-25 — Startup-override precedence and "first recorded outcome" are ambiguous

- **Severity:** low
- **Location:** Local tool and readiness ("a declared finite module override wins"); AD-10 ("The first recorded outcome fixes precedence")
- **Issue:** When several enrolled modules declare different overrides in the complete environment, it is not said which one wins. "First recorded outcome fixes precedence" does not tell a reader how failure, cancellation and success interact for retention.
- **Fix:** "The active module's override applies; in the complete environment, the largest declared finite override applies. A run's first terminal outcome (failure, cancellation or success) alone decides retention or cleanup."
- **Disposition:** autofix

### R-26 — "Current on security patches" has no checkpoint

- **Severity:** low
- **Location:** AD-8 ("Reused infrastructure is suitable only while within upstream support and current on security patches")
- **Issue:** Without a bound or checkpoint, two operators can disagree about compliance, and nothing gates on it.
- **Fix:** Add "checked at each production attempt and each monthly drill against the environment-layer inventory; a critical advisory unpatched beyond a declared bound sets the promotion stop."
- **Disposition:** autofix

### R-27 — AD-14's narrowing of FR-12/SM-1 is not signposted

- **Severity:** low
- **Location:** AD-14; Capability map › FR-12
- **Issue:** Because the CLI and MCP heads are one surface, a human using the CLI also cannot run confirmation-required or UI-only operations. This is user-adopted (V-10), but FR-12 and SM-1 say "commands and queries published by enabled modules". SM-1 acceptance could be disputed.
- **Fix:** Add to the FR-12 map row: "acceptance covers agent-eligible operations only (AD-14)".
- **Disposition:** autofix

### R-28 — "Platform architecture owner" is not a named role

- **Severity:** low
- **Location:** AD-9 ("needs Platform architecture-owner acceptance recorded in this spine")
- **Issue:** Administrator is the only named human role, so the approver of new AD-9 exceptions is unidentified.
- **Fix:** Name the role (for example, "Administrator as Platform architecture owner") or add it to the roles used by the spine.
- **Disposition:** autofix

### R-29 — "Gateway" is used as a component but never defined

- **Severity:** low
- **Location:** Design Paradigm diagram ("Selected environment gateway"); AD-11; AD-14 ("gateways derive the surface from … azp")
- **Issue:** It is unclear whether authorization happens at the ingress controller or at the EventStore host. If implementers put `azp`-based surface checks in ingress annotations, the checks diverge from EventStore's catalog/metadata authority.
- **Fix:** "The gateway is the composed EventStore host's authenticated command/query/metadata endpoint exposed through environment ingress. Ingress terminates TLS and routes but makes no authorization decision."
- **Disposition:** autofix

---

## Verified as consistent (no finding)

- All seven Source Precedence links and the PRD and memlog links resolve. The frontmatter source `reviews/validate-2026-09-27/findings.json` exists.
- AD IDs are stable (AD-1..AD-12 kept, AD-13..AD-15 new). Every AD appears in the Capability map. The "Release and Recovery Acceptance" heading matches the AD-12 reference. G1/G2/G3 and SM-4/SM-5 references resolve.
- The external references EventStore AD-10, AD-22, AD-24, AD-25, AD-26 [ASSUMPTION, "still unratified" is accurate] and AD-33 match their EventStore topics.
- Settled decisions are carried faithfully:
  - Folders/Projects override; AD-1 Option 1 with fallback trigger.
  - Two isolated executors (V-02); AD-15 expand/contract; per-environment OpenBao.
  - Key-custody backup class (V-19); lost-window exception (V-37); stdio-only MCP (V-48); intake manifest and N/N-1 schema (V-08).
- Stack rows match the working tree (`global.json`, `apphost.cs` directives) and current NuGet/GitHub releases as of 2026-09-27.
