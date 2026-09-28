# Rubric review (round 2): Platform architecture spine

**Verdict: CONDITIONAL PASS.** Local/CI enrollment, the runner and the declaration work can build from this spine now. Hosted, recovery and McpCli-release stories should wait for RB-1 to RB-4. There are no critical findings: 4 high, 9 medium, 3 low.

## Scope

- **Subject:** `ARCHITECTURE-SPINE.md` at 22:35 on 2026-09-27, status final, 15 ADs, including today's uncommitted AD-11 amendment.
- **Driving inputs:**
  - the working trees of `prds/prd-platform-2026-09-27/prd.md` and `addendum.md`
  - `sprint-change-proposal-2026-09-27.md` (SCP)
  - the PRD update reviews in `prds/prd-platform-2026-09-27/update-2026-09-27/`. `review-recovery-access.md` A-1 there assigns two source amendments to this spine.
- **Brownfield inspected:**
  - `apphost.cs`, `global.json`, `DaprComponents/*.yaml`, `eng/verify-agents-host.sh`, `README.md`, `.github/`
  - the `src/` hosting and MCP/CLI projects of Parties, Tenants, Memories, EventStore, Folders, Projects and McpCli
- **Module spines checked:**
  - McpCli (`architecture-mcpcli-2026-09-22`)
  - Memories (`architecture-memories-2026-09-09`)
  - Projects (`architecture-projects-2026-07-15`)
  - Parties (`epic-8-domain-focus-2026-07-06`)
  - EventStore `architecture.md` (status draft)
  - Tenants `architecture.md`
- **Not re-raised:** PR-01, PR-37 and PR-38, which were declined in `reviews/update-2026-09-27/gate-summary.md`. The restated PRD thresholds in Release and Recovery Acceptance are treated as intentional. Deferrals RV-13 and RV-14 are accepted as recorded.

## Checklist

| # | Rubric item | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Fixes the real divergence points for the level below and misses none | **Partial** | Strong on the declaration schema, AD-4 source identity, the run lifecycle, the release/attempt model, the composed host and catalogs. Four divergence points are open:<br>- legacy MCP/CLI hosts in compositions (RB-1)<br>- deputy operational authority and notification recipients (RB-2)<br>- admission of synthetic production actors versus G1 (RB-3)<br>- who publishes the McpCli tool (RB-4)<br>Surface exposure class is also missing from the declaration (RB-5). |
| 2 | Every AD Rule is enforceable and prevents its stated divergence | **Partial** | Most Rules are validator-checkable; AD-4, AD-9, AD-10, AD-13 and AD-15 are good examples. After today's amendment, AD-11 no longer prevents "discovery advertising operations the selected environment cannot run" (RB-1). AD-6 admission-as-membership and G1 "group is empty" cannot both hold once smoke verification runs (RB-3). AD-14's asynchronous-step rule depends on an EventStore contract that EventStore does not bind (RB-9). |
| 3 | Nothing deferred could let two units diverge | **Partial** | Deferred rows mostly carry an owner and a gate. The following gates are too late or missing:<br>- the composed host is gated on Folders, but every environment needs it (RB-8)<br>- release/attempt controls say "Before G3", while the PRD says before the first production attempt, and the lock/record store is unnamed (RB-7)<br>- broker selection has no gate, and two PRD downstream rows are absent (RB-11)<br>- the telemetry sink is gated "before G2" with no isolation rule (RB-13)<br>The "Shared ServiceDefaults" row correctly holds its invariant ("new hosting helpers are refused until then"). |
| 4 | Named tech verified-current (light) | **Pass** | .NET 10.0.401, Aspire 13.5.4, CommunityToolkit Dapr beta.757→770 and EventStore.Aspire 3.106→3.109 match `apphost.cs` L2–6, `global.json` and the EventStore submodule tag (v3.109.0-18). Helm 4 `--rollback-on-failure` (formerly `--atomic`) is correct. The Kubernetes 1.34 EOL of 2026-10-27 is correct. Keycloak 26.7 is recorded in the memlog, and standard token exchange is on by default there. One internal inconsistency: Helm is listed as "4.2.0 or later" yet called a pin (RB-16). |
| 5 | Ratifies rather than contradicts brownfield | **Pass** | Stack pins equal the `apphost.cs` directives. The HS256 dev key (`apphost.cs` L178–181), the Works sibling path (L23–27) and local `defaultAction: allow` ACLs (`DaprComponents/accesscontrol.yaml` L20) are recorded as retirements or local-only. Domain-module `*.AppHost`, `*.Aspire` and `*.ServiceDefaults` projects (Tenants, Folders, Projects, Parties) are frozen under Migration and coexistence. The EXT-HOST-1 stance in `eng/verify-agents-host.sh` and the README matches the rule that domain modules have no hosting. One ambiguity: `apphost.cs` composes `eventstore-admin` (Admin.Server.Host) and `eventstore-operations`, and after the AD-11 amendment their hosted status is unclear (folded into RB-1). |
| 6 | Covers FR-1..FR-12, NFR-1..NFR-3 and architecturally significant consequences | **Partial** | FR-1–FR-7, FR-11, FR-12 (agent eligibility and the replacement policy) and NFR-1/NFR-3 are covered. Gaps:<br>- FR-8/FR-9 deputy recipients and authority (RB-2)<br>- PRD pre-G2 synthetic SM-4 admission (RB-3)<br>- FR-10 exposure class (RB-5)<br>- FR-8 post-window incidents (RB-6)<br>- NFR-2 coverage rule (RB-12)<br>- PRD downstream rows for critical-flow removal review and rerun evidence (RB-11)<br>RB-2, RB-3 and RB-12 were explicitly handed to this spine: PRD L346, addendum L185, L225 and L326, and `review-recovery-access.md` A-1. |
| 7 | No AD contradicts a module spine's binding decision without a recorded override | **Partial** | These overrides are recorded: Folders I-10/I-11, Projects AD-28/G-1/AD-30 (HA/RPO), Works AD-20, Memories digest ownership (ME AD-19), the Memories Redis exception rows (Source Precedence: "direct-provider access only per the AD-9 exceptions"), and EventStore's pending confirmations (AD-26 digest split, AD-33 activation, composed-subject issuer). These are unrecorded or mis-attributed:<br>- McpCli AD-17 runs its own publication pipeline (RB-4)<br>- EventStore AD-29 is credited with an "attested original actor" it does not define (RB-9)<br>- Projects AD-30 implies McpCli confirmation unlocks later; the McpCli test AppHost needs a new `Parties.Aspire`; McpCli per-setting resolution and `--actor`; Memories' own `deploy/kubernetes` and environment list (RB-10) |
| 8 | Every initiative-owned dimension is decided, deferred or open | **Partial** | Decided: deployment and environments, provider strategy (single on-prem node, accepted risk), operations, recovery, security, and cost ("No cost target is set"). Observability is only partial: "shared technical-module health and telemetry" plus a telemetry sink deferred to before G2, with no environment-isolation rule and no gate for staging (RB-13). |
| Hygiene | Binds/Prevents/Rule quality, rationale leakage, diagrams, altitude, usability | **Partial** | Binds/Prevents lines are crisp. Rules average about 180 words (AD-11: 247, AD-7: 216), and several bundle four to six sub-rules. Rationale fragments remain in Rules. AD-11 was amended without an `[AMENDED]` tag or memlog entry. The attempt lifecycle is prose that wants one state diagram (RB-14, RB-15). |

## Findings

### RB-1: The AD-11 amendment dropped the rule that kept legacy MCP/CLI hosts out of Platform compositions

- **Severity:** high
- **Location:** AD-11 Rule (L117). Deferred › Owned work › "Legacy MCP/CLI retirement…" (L324).
- **Finding:** Before today's edit (per `git diff`), AD-11 read: "no module, FrontComposer or technical-module MCP host, including EventStore.Admin.Mcp, is deployed or routed in Platform compositions until an AD admits it under AD-14."
  - The replacement reads: "All proprietary Hexalith module and technical-module MCP hosts, plug-ins, and CLIs, including EventStore Admin, are obsolete migration sources; Platform admits no new alternate proprietary MCP/CLI surface. Retire each source only after its owner-approved operation inventory…"
  - "No new" exempts every existing host by implication. No sentence now governs whether an existing host may be enrolled, deployed or routed before retirement.
  - The approved SCP wording (§4 item 9) had that sentence: "Legacy hosts and CLIs are not enrolled into new Platform compositions; temporary compatibility use requires a named migration record and removal gate." It did not land.
  - Brownfield: every such host exists in the module repos: `Memories.Mcp/.Cli`, `Folders.Mcp/.Cli`, `Projects.Mcp/.Cli`, `Parties.Mcp`, `EventStore.Admin.Mcp/.Cli`.
  - The Memories spine still carries "Server plus gated MCP" in its `deploy/kubernetes` (ME L303) and "Production independently scales Server and gate-approved MCP" (ME L343).
  - "Including EventStore Admin" is ambiguous. It could mean only Admin.Cli/.Mcp, or also Admin.Server.Host/Admin.UI, which `apphost.cs` L30 and L101–114 compose today.
- **Failure scenario:**
  1. The Memories enrollment author lists `memories-mcp` in the declaration's enabled server list. It is existing, not "new".
  2. The Folders author omits `folders-mcp`.
  3. Platform renders Memories.Mcp, and Hosted interfaces maps its declared interface to an FQDN.
  4. It advertises operations outside the gateway metadata/digest check, which is exactly what AD-11's Prevents line forbids.
  5. Its Keycloak client is absent from the AD-14 client-to-surface map. The gateway either rejects it, or someone adds an ad-hoc surface class.
  6. Separately, one Platform story removes `eventstore-admin` as "EventStore Admin" while another keeps it.
- **Suggested fix:**
  - Append to AD-11: "Legacy proprietary MCP/CLI hosts are not enrolled, deployed or routed in Platform compositions. A temporary compatibility use needs a named migration record with a removal gate, a surface-class client in the AD-14 map, and Administrator acceptance recorded in this spine."
  - State whether EventStore Admin.Server/Admin.UI (a UI surface) stays in the composition, separately from the obsolete Admin.Cli/.Mcp.
- **Disposition:** autofix. The SCP-approved wording exists. The Admin.Server/UI clarification needs a one-line user confirmation.

### RB-2: Deputy recovery authority and notification recipients are not implemented; three Rules contradict the selected PRD policy

- **Severity:** high
- **Location:**
  - Design Paradigm › Roles (L29)
  - AD-6 ("production realm changes are Administrator-only", L87)
  - AD-7 ("private operations repository writable only by Administrator"; recovery credentials "held in off-site custody", L93)
  - Diagnostics and notification ("GitHub issues assigned to Administrator are the single accepted notification path for every notification in this spine", L159)
  - Automatic recovery ("Report … to Administrator", L175)
  - Recovery point and freshness, and Detection and response (L178–179)
  - DR sequence step 4 (L180)
- **Finding:** The PRD selected a split deputy role (target users L34; FR-8 L203–204; FR-9 L224–225; glossary L377). Under it, the deputy:
  - receives deployment-failure, recovery and backup alerts
  - holds independent recovery and key access
  - executes recovery through their own identity
  - verifies and reopens service
  - cannot clear the promotion stop or administer production admission

  The addendum says the spine must change: "**Architecture follow-up is required:** its current Administrator-only operations access and notification rules do not yet implement deputy recovery authority" (L225). It adds: "The deputy access amendment must preserve this boundary or explicitly trigger that review" (L326). PRD L346 assigns this "before recovery/deployment stories are finalized".

  The spine gives the deputy only "backs Administrator for recovery and key custody" and unseal custody. It lists "recovery deputy" as a deferred item with no authority (L316).
- **Failure scenario:**
  - The notification builder implements L159 literally, and GitHub issues go only to Administrator. The FR-8/FR-9 deputy-delivery tests fail.
  - The recovery-workflow builder lets only the single ops-repo writer dispatch recovery jobs, so the deputy cannot start DR.
  - If the deputy starts DR anyway, step 4 ("re-apply admin and user revocations made after the cut"; realm-key rotation on compromise) is a production realm change that AD-6 reserves to Administrator. Deputy-led DR stalls mid-restore.
  - Alternatively, a builder grants the deputy ops-repo write access. That silently trips AD-7's "Adopt GitHub Team controls once a second person gains write access" without the review the addendum requires.
- **Suggested fix:**
  - **Roles:** add "The recovery deputy receives every deployment-failure, recovery, backup and monitor notification. Through their own identity, the deputy may run the recovery-executor workflows and the production recovery job, verify restoration and reopen service. The deputy cannot clear the promotion stop, approve releases or administer production admission."
  - **Diagnostics:** assign issues to Administrator and the deputy.
  - **AD-6:** add a recovery-scoped realm role limited to re-applying exported revocations and DR key rotation.
  - **AD-7:** name the deputy's trigger path (for example, a recovery-executor allowlist entry for the deputy's identity, or deputy as a second writer, which invokes the GitHub Team review).
- **Disposition:** discuss (trigger path and GitHub Team implication) plus autofix (recipients, Roles, AD-6 carve-out wording).

### RB-3: Synthetic production actors cannot pass verification while G1 requires an empty admission group; the PRD's pre-G2 SM-4 step is missing

- **Severity:** high
- **Location:**
  - AD-6 ("Production admission is membership in a named production group granted only by Administrator", L87)
  - Production entry gates G1 ("the production admission group is empty", L176)
  - Synthetic identities (L158)
  - Before production update ("its smoke suite passes now", L172)
  - Verification (L174)
- **Finding:**
  - Every production attempt, including the first G1 deployment, needs passing smoke checks. Necessary writes run as "per surface class, synthetic actor … clients".
  - Admission is membership in the production group, and G1 requires that group to be empty.
  - The spine never says whether synthetic actors are admitted, through which group, or whether they are exempt.
  - The PRD (L56) and addendum (L185) add a temporary step: "Administrator may temporarily admit a designated synthetic test identity to prove SM-4 positive access … the test grant is revoked after verification." The addendum says: "Architecture must carry this restricted qualification step alongside its initial empty-admission-group rule." `review-recovery-access.md` A-1 lists it as an owed spine amendment.
  - The spine contains neither the permanent smoke-actor admission nor the temporary SM-4 step.
- **Failure scenario:**
  - The gateway enforces AD-6 admission for every actor. Authenticated smoke checks at the first G1 deployment are denied, verification fails, and the deployment "stops with ingress closed". It can never succeed.
  - Builder A adds synthetic actors to the production group, which violates G1.
  - Builder B exempts tokens carrying the synthetic flag from admission. That creates a permanent admission bypass keyed on a token claim.
  - Builder C makes production smoke read-only, which loses write-path verification.
- **Suggested fix:**
  - Synthetic identities: add "Synthetic smoke actors are admitted through a separate Administrator-granted synthetic-admission group scoped to the synthetic tenant. G1's 'empty' applies to the human production-admission group."
  - G1: add the PRD's temporary SM-4 identity step, with executor/probe-only ingress, revocation after the check, and isolated identity copies for drills.
- **Disposition:** discuss (admission model for synthetic actors) plus autofix (the SM-4 step, which is PRD text).

### RB-4: McpCli's own release pipeline contradicts AD-11's Platform-built, staging-validated McpCli candidate

- **Severity:** high
- **Location:** AD-11 ("Each Platform release builds a McpCli candidate from its intake Contracts; it runs the staging flows and is published only after staging validation", L117). Workflows and provenance (L155). Source Precedence McpCli row (L193). Owned work › Connected McpCli (L313).
- **Finding:**
  - The McpCli spine AD-17 binds its own publication path. `references/Hexalith.McpCli/.../architecture-mcpcli-2026-09-22/ARCHITECTURE-SPINE.md` L159–160 reads: "**Prevents:** a second release pipeline … **Rule:** semantic-release on a green `main` SHA uses tag `v<version>` … packs the two projects … `Hexalith.McpCli.Abstractions`, `Hexalith.McpCli`". Its L272 reads: "semantic-release packs and pushes two packages to nuget.org".
  - The Platform spine records only the token change for McpCli (L193). Its Connected McpCli row amends "availability, token, actor and package/source-mode contracts" but not publication ownership.
  - Nothing says which artifact users install, or how the staging-validated candidate relates to the nuget.org package ID and version.
- **Failure scenario:**
  - McpCli maintainers push `Hexalith.McpCli` 1.8.0 to nuget.org on a green `main`, built from McpCli's own Contracts pins.
  - The Platform publication workflow builds a candidate from the intake Contracts, a different closure, and "publishes" it after staging, either under the same package ID (a version collision or race) or somewhere else.
  - Users running `dotnet tool update` get the unvalidated nuget.org build, whose statically enrolled Contracts don't match the environment's catalog digests. Operations silently become non-executable in production.
  - Each team believes it owns release.
- **Suggested fix:** Record an explicit override, choosing one of two options:
  - **(a)** McpCli's pipeline publishes only prerelease or candidate versions. The Platform publication workflow is the sole publisher of the staging-validated stable tool, and the release record binds its package hash.
  - **(b)** McpCli publishes. Platform qualifies a named McpCli version by package hash in the release record and never rebuilds it.

  Add the chosen option to the Source Precedence McpCli row and to the Connected McpCli deferred row.
- **Disposition:** discuss.

### RB-5: The module declaration omits surface exposure class and required authorization (FR-10)

- **Severity:** medium
- **Location:** Consistency Conventions › Module declaration (L147). Hosted interfaces ("Platform maps declared logical interface names to FQDNs by one pattern", L157).
- **Finding:**
  - FR-10 (PRD L240): "Each module declares its external surfaces, required authorization and exposure classification; a disabled or private surface is not advertised as available."
  - The declaration claims to enumerate "every module input this spine references". It has only "logical interface names and route prefixes" and "inbound callers and operations", with no public/internal/disabled class and no per-surface authorization.
  - Hosted interfaces maps every declared interface to an FQDN.
- **Failure scenario:**
  - The chart generator gives `parties-adminportal` and the EventStore Admin UI public `tache.ai` FQDNs.
  - Tenants assumes its BFF is internal-only and declares it the same way.
  - Production exposes admin UIs publicly. Staging and production then drift through manual ingress patches.
- **Suggested fix:**
  - Add "per-interface exposure class (public ingress, internal-only, disabled) and required surface class/authorization" to the declaration list.
  - Hosted interfaces: "Only public-class interfaces receive ingress FQDNs. Disabled or internal interfaces are neither routed nor advertised."
- **Disposition:** autofix.

### RB-6: Incidents after the verification window do not set the promotion stop (FR-8)

- **Severity:** medium
- **Location:** Automatic recovery (L175). Detection and response (L179).
- **Finding:**
  - FR-8 (PRD L208): "An incident establishing that production is no longer working sets or retains the promotion stop until Administrator records a verified baseline."
  - The spine sets the stop only on "every non-working terminal outcome and every DR entry". The off-site probe only "notifies Administrator".
  - No rule says who may set the stop outside an attempt.
- **Failure scenario:**
  1. After G3, the probe detects an outage at hour 3.
  2. An issue opens, but no stop is set.
  3. The outage clears transiently.
  4. The next automatic promotion passes the "healthy now" precondition and deploys on top of an unexplained incident.
  5. The monitor builder and the deployment-workflow builder disagree on whether the monitor may write the stop.
- **Suggested fix:** Add: "A detected production incident (probe failure beyond the verification thresholds, or an Administrator/deputy declaration) sets the durable promotion stop. The off-site monitor may set but never clear it."
- **Disposition:** autofix.

### RB-7: The release/attempt-control qualification gate is later than the PRD requires, and the lock/record store is unnamed

- **Severity:** medium
- **Location:** Owned work › "Release state, checks and notifications" ("Before G3: … interruption, epoch and concurrency behavior; one rollback; timers; rehearsal; provenance; stale-attempt detection; … actual GitHub issue delivery", L314). Attempt ownership (L170). First shared versions (L301, encoding only).
- **Finding:**
  - The addendum (L346): "Before the first applicable production attempt, including an approved pre-G3 attempt; … prove provenance, one recovery attempt, concurrency/interruption controls and actual GitHub delivery. SM-5 qualification additionally gates G3."
  - The spine's "Before G3" lets the Administrator-approved G1 deployments run first.
  - The lock and epoch, the attempt record and the durable promotion stop "live outside the target cluster and the executor host". No compare-and-set store is named or owned. Builds owns only the encoding.
- **Failure scenario:**
  - The epic plan puts lock, epoch and delivery qualification in the pre-G3 epic, so the first G1 attempt runs with unproven serialization.
  - Four hosts must share one lock and stop: the staging executor, the production executor, the recovery executor (DR "takes a new epoch") and the off-site monitor (stale-attempt check). Each builder picks a different store (GitHub issues or labels, OCI artifacts in the Builds registry, an object bucket). The "one lock" stops being one.
- **Suggested fix:**
  - Split the row: "Before the first production attempt: named CAS-capable attempt/lock/stop store reachable from all executors and the monitor; provenance; interruption and epoch; one recovery; GitHub delivery. Before G3: SM-5 rehearsals and EventStore confirmations."
  - Add a First shared versions row: "Attempt lock, record and promotion-stop store | Platform with Builds | all executors, monitor | first production attempt".
- **Disposition:** autofix (gate). Discuss or defer the store choice with a named owner.

### RB-8: The composed-host deliverables are gated on Folders, but every environment runs the Platform-composed host

- **Severity:** medium
- **Location:**
  - AD-13 ("hosted environments run the Platform-published composed image"; "EventStore ratifies this rule once, registering Platform as issuer for composed subjects", L129)
  - First shared versions (extension API consumers "Folders, Agents, modules with extensions", L293)
  - Owned work › Composed host ("… lifecycle-subject registration before Folders joins a Platform environment", L311)
- **Finding:**
  - AD-13 makes the Platform-composed `eventstore` host, with zero or more extensions, mandatory everywhere.
  - Any hosted deployment also needs Platform-issued lifecycle records. Those depend on EventStore registering Platform as composed-subject issuer.
  - EventStore's architecture still binds its own image lifecycle: "The current release mapping contains only `eventstore`" (EventStore `architecture.md` L171). It has no extension API or issuer registration yet.
  - The Aspire-to-Helm target (L312: Parties with EventStore, Tenants and Memories) needs a composed image before Folders is involved.
- **Failure scenario:**
  - The epic plan schedules the composed host and the issuer ratification with Folders enrollment.
  - The first staging deployment of the representative composition then either uses EventStore's stock image, violating AD-13 and lacking valid Platform-issued records, or blocks unexpectedly.
- **Suggested fix:** Re-gate as follows:
  - "Composed host build (with zero or more extensions), its lifecycle-subject registration and EventStore's issuer ratification: before the first staging deployment."
  - "Folders adapter packaging: before Folders joins."
- **Disposition:** autofix.

### RB-9: AD-14's asynchronous original-actor attestation is credited to an EventStore decision that does not define it

- **Severity:** medium
- **Location:**
  - AD-14 ("asynchronous task steps use the original actor attested by EventStore at admission", L135)
  - AD-11 (gateway check against "the EventStore-attested original actor", L117)
  - Source Precedence EventStore row ("AD-29 attested original actor", L191)
  - First shared versions (no row)
- **Finding:**
  - EventStore AD-29 (`references/Hexalith.EventStore/_bmad-output/planning-artifacts/architecture.md` L304–308) is "Admin Mutations Preserve Human And Service Attribution". It binds end-to-end operator identity or bounded delegation for Admin mutations only.
  - It does not bind an admission-time attestation of an original actor carried into asynchronous task steps.
  - The Platform spine treats the attestation as a retained EventStore contract and lists no deliverable, owner or gate for it. The closest item is L315, "confirm EventStore's actor-exposure assumption", which is a different thing.
- **Failure scenario:** Projects, whose tasks run asynchronous steps, needs the attested actor for its task steps. EventStore has no such contract, so Projects invents its own actor stamp in task state. The gateway (AD-11) then either cannot verify it or trusts a module-set value, reintroducing the caller-asserted actor that AD-14 prevents.
- **Suggested fix:**
  - Add a First shared versions row: "Admission-time original-actor attestation for asynchronous task steps | EventStore | Projects, gateway, McpCli | first asynchronous cross-module step / FR-12 acceptance".
  - Correct the Source Precedence attribution. Say "to be added by EventStore", or cite the right AD.
- **Disposition:** autofix (row and attribution). EventStore owns the design.

### RB-10: Module-spine contradictions left unrecorded (Projects, McpCli, Memories)

- **Severity:** medium
- **Location:** Source Precedence and Module Integration (L187–197). AD-10 (L111). AD-14 (L135). Connected McpCli row (L313).
- **Finding:** These module decisions contradict Platform ADs, and the Platform spine does not record them as overridden. Each needs one line in its Source Precedence row.
  - **Projects AD-30** (Projects spine L305): "consequential MCP confirmation by a human actor, MCP Selection Evidence, and proposed-Project confirmation on MCP remain blocked until Story 8.11 deployment/rollback evidence passes…"
    - This implies they unlock later. Platform AD-14 makes confirmation-required operations non-executable through McpCli in the MVP, and Projects' own AD-29 (L299) agrees with Platform.
    - Platform limits Projects AD-30 only on HA/RPO-0 (L187).
  - **McpCli test AppHost** (McpCli spine L190, L251: "`Hexalith.McpCli.AppHost/` # Aspire topology: EventStore platform + Tenants + Parties"; L223: "Parties.Aspire release pending").
    - Platform AD-10 forbids McpCli tests from starting their own AppHost. McpCli is a tool, not a technical module.
    - The demanded `Parties.Aspire` package contradicts the Platform rules that domain modules own no Aspire infrastructure and that domain-module hosting is frozen.
  - **McpCli AD-13 settings resolution** (McpCli spine L136): URL and token resolve independently through flag, environment, profile and default. `--actor` is accepted on every verb.
    - This contradicts AD-11's "profile binds one environment's gateway, issuer and audience" and its hosted `--actor` refusal.
    - L313 covers it only generically ("availability, token, actor").
  - **Memories** (Memories spine L248, L303, L343):
    - its environment list is "local AppHost composition, CI, integration, and Production", with no staging
    - `deploy/kubernetes` holds "Server plus gated MCP"
    - production "independently scales Server and gate-approved MCP"

    AD-1 overrides these generically ("module deploy assets are declaration or conformance inputs"), but the Memories row doesn't say so.
- **Failure scenario:**
  - A Projects story unlocks MCP Selection Evidence after Story 8.11, citing its own AD-30.
  - The Parties team publishes a new `Parties.Aspire` package because McpCli tests depend on it.
  - The Memories team keeps maintaining its own production manifests as a second writer.
- **Suggested fix:** Extend the Source Precedence rows:
  - **Projects:** "AD-30's MCP confirmation/Selection Evidence unlock is overridden by AD-14; confirmation stays UI-only."
  - **McpCli:** "Its test AppHost migrates to the AD-10 descriptor. No new `Parties.Aspire` is published. Settings resolve as one environment-bound profile; hosted `--actor` is refused."
  - **Memories:** "Its `deploy/kubernetes` and environment list are conformance inputs only (AD-1); staging applies."
- **Disposition:** autofix.

### RB-11: Deferred rows lack gates or omit assigned PRD work: broker selection, critical-flow removal review, rerun evidence

- **Severity:** medium
- **Location:** Owned work › "Shared runtime and profile evidence" (L315, no boundary). Staging gate ("evidence has a maximum age", L168).
- **Finding:**
  - **(a) Broker selection has no gate.** "Shared runtime and profile evidence" is the only owned-work row with no gate, yet it contains "select and qualify the approved durable production broker". Staging runs the same template, and the recovery inventory depends on the broker ("database backup alone does not recover broker backlog").
  - **(b) Two PRD downstream rows (PRD L337–338) have no spine counterpart:**
    - "Policy for reviewing removals or remapping of required critical-flow checks … matching digests alone do not establish that reduced coverage is acceptable"
    - "Staging execution triggers and rules for accepting rerun evidence without hiding failed or incomplete attempts"
  - The evidence maximum age has no owner or gate.
- **Failure scenario:**
  - A module drops two critical flows. The new suite is non-empty and mapped, so the gate accepts it, while other module owners expect Platform review.
  - The staging executor accepts the latest passing rerun and discards the earlier failure, in conflict with SM-C1.
- **Suggested fix:** Add gates and rows:
  - broker: "before first staging deployment"
  - "Critical-flow removal/remap review policy | Module owners with Platform | before release-gate qualification"
  - "Staging trigger, rerun acceptance and evidence maximum age | Platform with Builds | before the first staging evidence is accepted"
- **Disposition:** autofix.

### RB-12: The NFR-2 response-coverage rule is loose ("such as")

- **Severity:** medium
- **Location:** Detection and response ("declares response arrangements, such as staffed hours and a maximum acknowledgement time; the RTO is assessed within them", L179). DR evidence (L181).
- **Finding:**
  - NFR-2 (PRD L297–301) fixes the following:
    - coverage with a time zone
    - primary/deputy responsibility and a maximum acknowledgement delay
    - "an incident beginning within coverage remains covered when the scheduled coverage ends"
    - "the clock never pauses or restarts when coverage begins"
    - every incident records full duration and coverage status
  - `review-recovery-access.md` A-1 asks the spine to "carry NFR-2's precise start-of-outage coverage rule so implementation uses one definition". The spine's "such as" makes each element optional.
- **Failure scenario:** The drill-report builder counts an incident that started 10 minutes before coverage ended as covered. The incident-report builder pauses the clock outside coverage. The two produce incompatible RTO evidence.
- **Suggested fix:** Replace the clause with the compressed NFR-2 definition: time zone, primary/deputy, acknowledgement bound, applicability decided at outage start, clock never paused, coverage status recorded for every incident.
- **Disposition:** autofix.

### RB-13: Observability has no environment-isolation rule, and its gate is later than staging needs

- **Severity:** medium
- **Location:** Diagnostics and notification ("Use shared technical-module health and telemetry", L159). Owned work › Recovery capacity and coverage ("telemetry sink and minimum retention", before G2, L319).
- **Finding:**
  - The sink, the OTLP export path and the per-environment separation are undecided.
  - The only gate is "before G2", tucked in the recovery row. Staging E2E diagnostics ("preserve classified, redacted diagnostics after cleanup") need a sink at the first staging deployment.
  - Default-deny egress (AD-8) means Platform, not a module egress declaration, must render the export destination.
- **Failure scenario:**
  - Staging and production export to one shared in-cluster collector and dashboard, and staging-authorized people read production traces carrying tenant data (NFR-3).
  - Or one module declares the collector as its own egress while others do not, and production exporters are silently blocked.
- **Suggested fix:**
  - Diagnostics: "Telemetry sinks are per environment, or partitioned with production data readable only by production principals. Platform renders exporter configuration and egress; modules never declare the sink."
  - Gate the sink before the first staging deployment. Keep retention and off-site durability before G2.
- **Disposition:** autofix. Discuss only if a shared sink is intended.

### RB-14: AD-11 amendment provenance is missing

- **Severity:** low
- **Location:** AD-11 heading (L113). Front matter `sources` (L12–17). `.memlog.md` (last write 19:34; spine 22:35).
- **Finding:**
  - AD-11 was substantively amended today for the SCP, but its heading still reads `[ADOPTED]`. AD-1/3/7/9/12 use `[ADOPTED, AMENDED]`.
  - The memlog has no entry: `grep -i "obsolete|migration source|proprietary"` finds nothing.
  - The SCP is not listed in `sources`.
- **Failure scenario:** A later updater or validator cannot tell the deploy/route prohibition was removed deliberately. That is how RB-1 arose.
- **Suggested fix:** Tag `[ADOPTED, AMENDED]`, add a memlog decision entry citing the SCP, and add the SCP to `sources`.
- **Disposition:** autofix.

### RB-15: Rule hygiene: rationale in Rules, oversized Rules, and a state machine written as prose

- **Severity:** low
- **Location:** AD-14 ("Because `azp` authenticates only confidential clients, …", L135). AD-3 ("`--rollback-on-failure` (formerly `--atomic`)", L69). AD-7 ("co-scheduling on the shared node is the AD-8 residual risk", L93). Release and Recovery Acceptance rows Attempt ownership through Automatic recovery (L170–175).
- **Finding:**
  - Rules average about 180 words. AD-11 is 247 and AD-7 is 216, and several bundle four to six sub-rules.
  - Justification and history belong in the memlog.
  - The attempt lifecycle is a state machine spread across six prose rows: lock → preconditions → rollout → verification → working/failed → one recovery → terminal/stop, plus interruption, takeover and DR entry.
- **Failure scenario:** A small implementation agent reads AD-14's "because" clause as a condition, or misses the takeover transition buried in Attempt ownership.
- **Suggested fix:**
  - Move the three rationale fragments to the memlog.
  - Add one `stateDiagram-v2` for the attempt lifecycle and trim the rows to guards and thresholds.
  - Optionally bullet the sub-rules of AD-1, AD-7 and AD-11.
- **Disposition:** autofix (rationale). Defer or ignore the diagram and reformatting at the user's discretion.

### RB-16: Helm is a "pin" given as a floor

- **Severity:** low
- **Location:** Stack ("Helm | 4.2.0 or later (4.3.0 current) | Toolchain pin in the profile inventory", L212). Production profile ("the profile inventory pins … the deploy toolchain", L154).
- **Finding:** "or later" is a floor, not a pin.
- **Failure scenario:** The staging and production executors run different Helm minors. Server-side-apply ownership or rendering then differs between the rehearsed and the promoted upgrade.
- **Suggested fix:** "Floor 4.2.0; exact version pinned in the profile inventory and equal on every executor."
- **Disposition:** autofix.

## Section 7 detail: module spine cross-check

| Module decision | Platform AD | Status |
| --- | --- | --- |
| McpCli AD-17: own semantic-release to nuget.org (L159–160, L272) | AD-11 McpCli candidate | **Unrecorded (RB-4)** |
| McpCli AD-13: per-setting resolution, `--actor` everywhere (L136) | AD-11, AD-14 | Partially recorded (L313, generic) (RB-10) |
| McpCli AD-3: local-only availability, no metadata or digest (L76) | AD-11 connected availability | Partially recorded (L313 "availability") |
| McpCli AD-10: future HTTP transport "replaces Actor from the forwarded user header" (L118) | AD-14 (no caller-set headers or raw forwarding) | Outside MVP (AD-11 stdio only). Should be named in the "Legacy/generic transport decision" deferral; low risk until HTTP is admitted. |
| McpCli test AppHost plus a new `Parties.Aspire` (L190, L223, L251) | AD-10; domain modules own no Aspire | **Unrecorded (RB-10)** |
| McpCli three-path Builds import with sibling fallback (L205, L229) | AD-4 | Recorded generically (Owned work › Source/package adoption, L308) |
| Memories environment list without staging; own `deploy/kubernetes` with MCP; independent production scaling (L248, L303, L343) | AD-1, AD-8, AD-11 | Generic AD-1 override only (RB-10; RB-1 for MCP) |
| Memories: later `AD-n` may add Redis rows (L260) | AD-9 acceptance in the Platform spine | Recorded ("direct-provider access only per the AD-9 exceptions", L192) |
| Memories: tombstone mirror deferred until "a finite recovery objective … justifies" it (L447) | G2 / Memories continuity | Consistent by its own trigger: the Platform envelope is finite. Suggest noting this in the Memories row. |
| Memories AD-19: `Memories.Aspire` owns data-service digests | AD-3 / production profile | Recorded (qualification inputs) |
| EventStore AD-11: own `eventstore` release mapping (L171) | AD-13 | Recorded as pending ratification. Gate is too late (RB-8). |
| EventStore AD-26: profile digest binds app IDs, OpenBao and catalog digests (L290) | Production profile digest definition | Recorded as pending ("Profile template and per-release binding split", L300) |
| EventStore AD-33: failed activation "rolls back to the prior complete generation" (L334) | AD-15 forward rollback generation | Recorded as pending (L314 EventStore confirmation) |
| EventStore AD-29: Admin-mutation attribution only (L304–308) | AD-14 async attested actor | **Mis-attributed (RB-9)** |
| Projects AD-30: MCP confirmation and Selection Evidence unlock after Story 8.11 (L305) | AD-14 | **Unrecorded (RB-10)** |
| Projects G-6 toolchain tuple .NET 10.0.400 / Aspire 13.5.3 (L491) | Stack 10.0.401 / 13.5.4 | Qualification evidence, not a binding tuple. Low; align during Source/package adoption. |
| Projects AD-28 99.9% availability, RPO 0; G-1 durable engine | Recovery envelope | Recorded override |
| Parties: MCP to McpCli, ACL file as a declaration source, no MCP erasure, local topology "FrontComposer.AppHost or an approved platform AppHost owner" | AD-1, AD-9, AD-11 | Consistent or recorded |
| Tenants: HS256 optional in development only (frozen transitional AppHost) | AD-6 HS256 retirement | Consistent |

Consistent across modules: Dapr 1.18 minor, with patch skew acknowledged; no module binds a shared realm; no competing operational notification channel; EventStore's Redis pub/sub is development/test only; and the catalog and secret-contract paths are Platform-owned (EventStore L261, L330).

## Counts

| Severity | Count | IDs |
| --- | --- | --- |
| Critical | 0 | none |
| High | 4 | RB-1, RB-2, RB-3, RB-4 |
| Medium | 9 | RB-5 … RB-13 |
| Low | 3 | RB-14, RB-15, RB-16 |

Recommended order: RB-1 and RB-14 together (one AD-11 edit), then RB-3 and RB-2 (the PRD-assigned alignment), then RB-4 (a user decision), then the medium autofixes as one batch.
