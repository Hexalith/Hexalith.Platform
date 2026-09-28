# Pragmatism and Bloat Review, update 2026-09-28: Platform Architecture Spine

- **Target:** `ARCHITECTURE-SPINE.md` (working tree, status `draft`, 467 lines, 11,961 words, 15 ADs). Compared with `HEAD` (328 lines, 8,439 words). Decisions: `.memlog.md` update run 2 (batches 1 to 6, C-01 to C-64).
- **Lens:** pragmatism and bloat for a terse build substrate read by small agents and humans. Tests: invariant only if independently built units could get it wrong; decisions, not rationale; one home per rule; seed and mechanism detail in Deferred or code; shape in diagrams. Also the opposite failure: text too terse for a small agent to read correctly.
- **Guardrails honoured:** every memlog decision survives (reworded, consolidated or moved only). PRD-restated thresholds stay verbatim (PR-01 declined), including the Local tool and readiness row, AD-10 lifecycle wording, the R&R timings and the Source Precedence envelope. User-adopted heavy items stay: tenant-key custody, rotation on restore, two executors plus a recovery executor, per-environment OpenBao, lock epoch and takeover, off-site monitor plus dead-man, PriorityClass and quotas, the Release tiers table and the attempt diagram.
- **Scope:** read-only. This file is the only output. Every "Old" string below was checked to occur exactly once in the spine.

## Verdict

**PASS WITH TRIMS.**

1. **The growth is earned.** Words rose 42% (+3,522). About 90% of that is decision content from the 24 settled clusters and 38 autofixes. The rest is structure the r2 review asked for: Terms, the Release tiers table, the attempt diagram, the numbered preconditions and DR sequence, sub-bullet labels and the Gate column. No AD carries unowned scope, and only three rationale fragments remain.
2. **The remaining bloat is restatement, not excess decisions.** Conventions, Source Precedence and Deferred re-say rules whose home is an AD or another row. Autofix trims recover about **310 words (2.6%)**. Discuss items move the total by about 10 words more, so the achievable net is about **−320 words, to roughly 11,640 (−2.7%)**. Going further would mean dropping decisions or PRD wording, which this review may not propose.
3. **Four places are too terse or stale for a small agent** and matter more than the trims:
   - AD-3 "forbids any other automatic rollback", which contradicts the Automatic recovery row (PR-201).
   - The Migration row implies legacy MCP hosts are in the composition; AD-11 excludes them (PR-202).
   - The declaration schema is still "adopt or supersede" although C-02 ratified it (PR-203).
   - A shared-infrastructure change never renews qualification or production-promoted records. After any cluster patch, every release would become undeployable and no rollback target would be valid (PR-204).

| Severity | Count | Disposition | Count |
| --- | --- | --- | --- |
| High | 0 | autofix | 27 |
| Medium | 4 | discuss | 5 |
| Low | 28 | defer | 0 |
| **Total** | **32** | ignore | 0 |

## 1. Word budget

The Δ column compares with `HEAD`. "Trim" is the autofix saving proposed here.

| Section | Lines | Words | Share | Δ | Trim | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| Frontmatter | 1–24 | 81 | 0.7% | +10 | 0 | Keep. |
| Design Paradigm (Roles, Terms, diagram) | 28–56 | 383 | 3.2% | +104 | −5 | Keep. Terms and Roles pay for themselves. Add one AppHost pointer (PR-206) and drop one AD-9 restatement (PR-216). |
| AD-1 to AD-15 | 58–210 | 3,854 | 32.2% | +851 | −74 | Proportionate. Average 257 words per AD, with about 120 words of sub-bullet labels that buy findability. See §2. |
| Consistency Conventions (14 rows) | 212–229 | 1,991 | 16.6% | +600 | −101 | **Main trim target.** Migration, Secrets, Workflows, Hosted interfaces and Synthetic identities restate AD rules. |
| Release tiers + Keycloak note | 231–240 | 280 | 2.3% | new | 0 | Keep. It resolved r2's five-home divergence. |
| R&R diagram + table (17 rows) | 242–282 | 1,960 | 16.4% | +434 | −25 | Protected PRD wording. Trim only non-PRD restatements in Backup coverage and Rollout. |
| Production preconditions | 284–296 | 162 | 1.4% | new | 0 | Keep. It is exemplary and resolved the eight-home scatter. |
| Disaster recovery sequence | 298–308 | 309 | 2.6% | moved | −11 | Keep the steps. The intro line restates AD-7. |
| Source Precedence | 310–323 | 494 | 4.1% | +140 | −41 | The EventStore row duplicates First shared versions. The Builds row duplicates Terms. |
| Stack | 325–341 | 184 | 1.5% | −31 | −18 | Good after C-62. Observed versions and one authority clause are duplicates. |
| Structural Seed | 343–391 | 296 | 2.5% | +37 | −12 | Keep the diagrams and the observation list, which is the only home for `nginx-public` and existing Memories resources. Drop one disclaimer. |
| Capability map | 393–408 | 270 | 2.3% | +62 | +9 | Keep. It is now a real entry point. Replace the "attempt rows" placeholder (PR-209). |
| Deferred intro + First shared versions | 410–433 | 377 | 3.2% | +142 | −6 | Keep. One stale cell (PR-203). |
| Owned work (25 rows) | 435–463 | 1,211 | 10.1% | +373 | −26 | Mostly legitimate work with gates. Three rows restate other homes. One row re-lists AD-6/7/8 (PR-232, discuss). |
| Accepted risks | 465–467 | 109 | 0.9% | +53 | 0 | Keep. It is the single home, and nothing restates it now. |
| **Total** | 467 | 11,961 | | +3,522 | **−310** | |

## 2. Per-AD fit (Binds/Prevents compared with Rule)

| AD | Words (HEAD) | Fit | Sub-bullets that belong elsewhere | Action |
| --- | --- | --- | --- | --- |
| AD-1 | 210 (201) | Yes | *Chart* re-lists tier contents (PR-223). Should receive HotReload from AD-8 (PR-230). | −2 |
| AD-2 | 187 (208) | Yes | None. The *Promotion* writer pointer is fine; its target row is trimmed instead (PR-211). | Keep |
| AD-3 | 181 (188) | Yes | *Outside rollback* first sentence re-lists the Release tiers table (PR-213). *Recovery render* re-defines the AD-15 rollback set and misstates "automatic rollback" (PR-201). | −16 |
| AD-4 | 201 (167) | Yes | None. | Keep |
| AD-5 | 80 (75) | Yes | None. | Keep |
| AD-6 | 333 (213) | **Partial** | *Authentication* binds MFA on GitHub, OpenBao, registry, DNS and the backup store, which is outside "identity realms" (PR-231). *Realms* repeats that the contract version is bound in the release record (PR-229). | −11 (−33 more if PR-231 is taken) |
| AD-7 | 357 (251) | Yes | None. Every clause traces to C-09, C-20, C-21 or C-51. The recovery executor could move to AD-12, but that saves nothing, so it is not raised. | Keep |
| AD-8 | 362 (230) | **Partial** | *Dapr* "disable HotReload" is rollout semantics, not reach (PR-230). *Namespaces* carries a "so" rationale clause that duplicates AD-7 (PR-217). *Negative tests* repeats the Hosted interfaces hostname rule (PR-218). PriorityClass and quotas stay by user decision. | −30 |
| AD-9 | 240 (189) | Yes | *Default* repeats the SDK mutation rule, which has two other homes (PR-215). | −7 |
| AD-10 | 225 (211) | Yes | None (protected wording). | Keep |
| AD-11 | 465 (290) | Yes | This is the heaviest AD, but Binds now names legacy retirement, and every clause traces to C-01, C-03, C-04, C-32 or C-33. Moving the retirement gate into Owned work would put a binding rule in Deferred, the anti-pattern r2 flagged in PR-107, so it is not raised. The trims happen where others restate it (PR-211, PR-224). | Keep |
| AD-12 | 190 (150) | Yes | *Model* restates who starts the recovery executor (PR-227). *Unseal* absorbs the custodian sentence from Secrets (PR-219). | +1 |
| AD-13 | 222 (223) | Yes | None. It is the lifecycle-issuer home; Binding classes now points to it (PR-229). | Keep |
| AD-14 | 372 (229) | Yes | None. *Chains* is dense, but it is all C-06. | Keep |
| AD-15 | 225 (178) | Yes | *Additive* restates the Module intake breaking definition (PR-214). | −9 |

---

## 3. Findings

### A. Too terse or stale: a small agent would misread these

#### PR-201: AD-3 forbids "any other automatic rollback", which contradicts Automatic recovery
- **Severity:** medium. **Disposition:** autofix.
- **Location:** AD-3 *Recovery render* (L88).
- **Finding:** The spine's own **Automatic recovery** row and diagram require one automatic recovery. Read literally, "any other automatic rollback are forbidden" tells a deployment-workflow agent either to disable that recovery or to treat `helm rollback` as a permitted non-"other" path. The same sentence also re-defines the AD-15 rollback set inline.
- **Old:** "Application recovery renders the working baseline's package with environment-current values and the prepared rollback generation through a Helm upgrade and commits that generation at readiness. Helm `--rollback-on-failure` and any other automatic rollback are forbidden."
- **New:** "Application recovery renders the prepared AD-15 rollback set through a Helm upgrade and commits its rollback generation at readiness. Helm `--rollback-on-failure`, `helm rollback` and every other automatic rollback path are forbidden."
- **Words:** −3.
- **Decision check:** C-39 (the render carries the prepared rollback generation, committed at readiness) is kept through the AD-15 definition. The ban on Helm-native rollback (V-series) is kept and made explicit.

#### PR-202: The Migration row implies legacy MCP/CLI hosts are in the composition
- **Severity:** medium. **Disposition:** autofix.
- **Location:** Migration and coexistence (L229), last sentence.
- **Finding:** AD-11 says legacy hosts "are not enrolled, deployed, routed … in any Platform composition" (C-03). The Migration row says Platform removes "their composition and route declarations" after retirement, which implies they are composed until then. An agent could enroll EventStore.Admin.Mcp "until retirement". The removal work already has an owner in the Owned work row "… with Platform for composition and route removal".
- **Old:** "Legacy MCP/CLI packages and routes retire under AD-11, after which Platform removes their composition and route declarations."
- **New:** "Legacy MCP/CLI sources retire under AD-11."
- **Words:** −11.
- **Decision check:** C-03 and C-33 stay in AD-11. Platform's removal work stays in the Owned work row.

#### PR-203: The declaration schema is still "adopt or supersede", and three schema names are chained
- **Severity:** medium. **Disposition:** discuss, to confirm how `hexalith.module-manifest.v1` relates to Projects' `hexalith.module.v1`.
- **Location:** First shared versions row 1 (L420), and Module declaration (L216).
- **Finding:** C-02 ratified `hexalith.module-manifest.v1` as the implementation, but the Deferred row still offers "adopt or supersede". The Module declaration row calls it "the implementation of Projects' `hexalith.module.v1`". A small agent cannot tell which identifier a module's declaration file carries.
- **Old (First shared versions):** "Declaration schema and validator: adopt or supersede Builds `hexalith.module-manifest.v1`; once published"
- **New:** "Declaration schema `hexalith.module-manifest.v1` and validator; once published"
- **Old (Module declaration):** "Builds implements them as `hexalith-module`'s `hexalith.module-manifest.v1`, the implementation of Projects' `hexalith.module.v1`."
- **New:** "Builds implements them as `hexalith.module-manifest.v1`, which supersedes Projects' `hexalith.module.v1`." Replace *supersedes* with the confirmed relation.
- **Words:** −6.
- **Decision check:** C-02 is unchanged. The Terms paragraph already names `hexalith-module`.

#### PR-204: A shared-infrastructure change never renews qualification or production-promoted records
- **Severity:** medium. **Disposition:** discuss. It extends C-16 to the C-17 tier, so it needs a one-line memlog decision.
- **Location:** Production profile (L223), Binding classes and records (L219), Release tiers shared row (L237), precondition 6 (L293).
- **Finding:** The profile digest covers "the inventory's environment-layer **and shared** pins", and precondition 6 checks "environment-layer **and shared** versions" against the "effective qualified sets". However:
  - Only "an environment-layer change" issues a qualification record and a renewed production-promoted record.
  - The shared-infrastructure change path re-runs smokes and negative tests but issues neither.
  - AD-2 and Binding classes call the sets "qualified environment-layer sets".

  An agent building the shared-infrastructure workflow will therefore leave every retained release outside its qualified sets and without a production-promoted record valid for the new digest. The next promotion and every rollback target then fail precondition 6, and nothing in the spine says how to recover.
- **Old (Production profile):** "An environment-layer change is its own attempt, staging first; the production attempt re-verifies the working release and issues a qualification record and a renewed production-promoted record at the new digest, and promotion stays stopped until both exist."
- **New:** "An environment-layer or shared-infrastructure change runs per Release tiers; its production attempt re-verifies the working release and issues a qualification record and a renewed production-promoted record at the new digest, and promotion stays stopped until both exist."
- **Old (Binding classes):** "*Qualification records* are Platform-issued and extend one release's qualified environment-layer sets for one environment."
- **New:** "*Qualification records* are Platform-issued and extend, for one environment, a release's qualified sets: the environment-layer and shared pin versions it is verified against."
- **Words:** +9. This supersedes PR-228, so it forgoes that finding's −6.
- **Decision check:** This extends C-16 and C-17 and removes nothing. If the user prefers shared changes to leave records untouched, the digest preimage must drop the shared pins instead. Either way, a memlog line is needed.

#### PR-205: The Memories source row ends with the fragment "and staging applies"
- **Severity:** low. **Disposition:** discuss, to confirm the meaning.
- **Location:** Source Precedence, Memories row (L317).
- **Finding:** The Memories spine's environment list ("local AppHost composition, CI, integration, and Production") has no staging. The fragment presumably means that the Platform staging gate still applies. As written, it is unreadable.
- **Old:** "Its `deploy/kubernetes` manifests and environment list are conformance inputs only (AD-1), and staging applies."
- **New:** "Its `deploy/kubernetes` manifests and environment list, which omits staging, are conformance inputs only (AD-1); the Platform staging gate applies to Memories."
- **Words:** +7.
- **Decision check:** C-29 (Memories deploy assets overridden) is kept.

#### PR-206: The Paradigm says domain modules "own no AppHost" with no transition pointer
- **Severity:** low. **Disposition:** autofix. This is residue from r2's PR-107.
- **Location:** Design Paradigm (L32).
- **Finding:** An agent that reads only the Paradigm may delete a domain AppHost now. The Migration row freezes existing AppHosts until parity evidence and EventStore AD-22 authority exist.
- **Old:** "they own no AppHost, Aspire or ServiceDefaults infrastructure."
- **New:** "they own no AppHost, Aspire or ServiceDefaults infrastructure; existing ones follow Migration and coexistence."
- **Words:** +6.
- **Decision check:** None is affected. This is a pointer only.

#### PR-207: Production profile calls the template "EventStore-ratified … still unratified"
- **Severity:** low. **Disposition:** autofix.
- **Location:** Production profile (L223).
- **Finding:** The sentence contradicts itself, and ratification status is Deferred content: the First shared versions row "Profile template and per-release binding split" and the Owned work row "Ratify the profile".
- **Old:** "changes only through EventStore AD-26, still unratified."
- **New:** "changes only through EventStore AD-26."
- **Words:** −2.
- **Decision check:** Status stays in the Deferred rows.

#### PR-208: "Hosted sidecar patch … in CI"
- **Severity:** low. **Disposition:** autofix.
- **Location:** Production profile (L223).
- **Finding:** CI is not hosted and has no shared control plane. An agent may try to compare CI sidecars with the cluster control plane.
- **Old:** "The hosted sidecar patch equals the control-plane patch and the release's pinned patch in CI, staging and production; local runs report their patch and warn on mismatch."
- **New:** "In CI, staging and production the sidecar patch equals the release's pinned patch, and in hosted environments also the control-plane patch; local runs report their patch and warn on mismatch."
- **Words:** +3.
- **Decision check:** C-43 is kept exactly.

#### PR-209: The Capability map says "attempt rows"
- **Severity:** low. **Disposition:** autofix.
- **Location:** Capability map, FR-7/FR-8 (L401).
- **Old:** "Binding classes and records, Production preconditions, attempt rows"
- **New:** "Binding classes and records, Production preconditions, Attempt ownership, Timing and interruption, Rollout, Verification, Automatic recovery, Promotion stop"
- **Words:** +9.
- **Decision check:** None is affected. This is a findability fix.

### B. Duplication, rationale and seed detail

#### PR-210: The EventStore source row re-lists the First shared versions rows
- **Severity:** low. **Disposition:** autofix.
- **Location:** Source Precedence, EventStore row (L316).
- **Finding:** All five "requested" items are already rows in First shared versions (the extension API, AD-13 ratification, the attestation and the catalog schema) or in the Owned work G3 confirmations (production-promoted invalidation and renewal).
- **Old:** "Requested from EventStore: the versioned extension API; AD-13 ratification with composed-subject issuer registration; admission-time original-actor and originating-surface attestation extending EventStore AD-29; the surface-eligibility vocabulary in the catalog schema; production-promoted invalidation and renewal."
- **New:** "Requested from EventStore: its First shared versions rows and the G3 confirmations in Owned work."
- **Words:** −17.
- **Decision check:** C-28 and C-35 stay in the Deferred rows.

#### PR-211: Workflows and provenance restates AD-7, AD-11 and the record writers
- **Severity:** low. **Disposition:** autofix.
- **Location:** Workflows and provenance (L224).
- **Old 1:** "writes the release record and publishes the stable McpCli after staging validation."
- **New 1:** "writes the release record and publishes the stable McpCli (AD-11)."
- **Old 2:** "**Deployment workflows** run from the private operations repository on the executors and only verify and deploy."
- **New 2:** "**Deployment workflows** (AD-7) only verify and deploy."
- **Old 3:** "Deploy accepts artifacts only with matching provenance, the release record only from the publication workflow, and attempt records and evidence only from the environment's executor identity, or the recovery executor for DR."
- **New 3:** "Deploy accepts artifacts only with matching provenance, records only from their writers in Binding classes and records, and evidence only from the attempt-record writer."
- **Words:** −19.
- **Decision check:** C-04, C-21 and C-47 are kept. AD-7 *Triggers* owns the operations-repository location. Binding classes owns the record writers.

#### PR-212: The Migration row restates AD-1 and AD-10
- **Severity:** low. **Disposition:** autofix.
- **Location:** Migration and coexistence (L229).
- **Old 1:** "The Platform composition, launched by the Platform tool, is the only authority for integrated multi-module environments; Builds.Module.AppHost launches it rather than composing its own,"
- **New 1:** "Builds.Module.AppHost launches the AD-1 model rather than composing its own,"
- **Old 2:** "Technical-module AppHosts stay valid for their own repository tests; FrontComposer.AppHost is sample-only."
- **New 2:** "FrontComposer.AppHost is sample-only."
- **Words:** −23.
- **Decision check:** AD-1 *Model* ("no other AppHost composes multi-module environments") and AD-10 *Owner* (technical-module own-repository tests) keep both rules. C-02 is kept.

#### PR-213: AD-3 re-lists the Release tiers table
- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-3 *Outside rollback* (L87).
- **Old:** "Persistent data, Keycloak, shared infrastructure, backups and credential rotation stay outside application rollback. Persistent objects survive package removal."
- **New:** "Persistent objects survive package removal."
- **Words:** −13.
- **Decision check:** *Tiers* already says "application rollback touches only the application-package tier". The table places Keycloak and shared infrastructure in the shared tier, and business data, backups and credential rotation outside every release.

#### PR-214: AD-15 *Additive* restates the Module intake breaking definition
- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-15 (L210). Rename the label *Rehearsal*.
- **Old:** "A new adapter or descriptor change is additive only when the baseline tolerates it (breaking per Module intake). Staging rehearses the rollback set (Staging gate)."
- **New:** "Staging rehearses the rollback set (Staging gate); adapter and descriptor changes are classified per Module intake."
- **Words:** −9.
- **Decision check:** C-40, the single breaking definition, is strengthened.

#### PR-215: The EventStore SDK mutation rule has three homes
- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-9 *Default* (L146). The other homes are Paradigm L30 and Domain truth and delivery (L221).
- **Old:** "Use Dapr through established shared SDKs or provider-neutral contracts wherever it supplies the capability. Event-sourced aggregate mutation uses EventStore SDK contracts. Module-owned"
- **New:** "Use Dapr through established shared SDKs or provider-neutral contracts wherever it supplies the capability. Module-owned"
- **Words:** −7.
- **Decision check:** The Paradigm and Domain truth rows keep the rule.

#### PR-216: The Paradigm diagram note restates AD-9's adapter boundary
- **Severity:** low. **Disposition:** autofix.
- **Location:** Paradigm note (L56).
- **Old:** "Arrows show calls or dependency direction. Provider SDKs stay inside Dapr components or the named AD-9 adapters. Module-owned"
- **New:** "Arrows show calls or dependency direction. Module-owned"
- **Words:** −11.
- **Decision check:** AD-9 *Exceptions* is kept: "Provider SDKs … stay inside each named adapter boundary; all other … use Dapr".

#### PR-217: AD-8 carries a "so" rationale clause that duplicates AD-7
- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-8 *Namespaces* (L135).
- **Old:** "The application deploy identity holds none of these; it is secret-equivalent for its namespace, so it is per-job and never co-resident with module code."
- **New:** "The application deploy identity holds none of these."
- **Words:** −16.
- **Decision check:** C-20 is kept in AD-7. *Credentials* makes the application deploy identity per-job. *Module-code sandbox* denies module code any access to the job's credential material. The memlog already holds "secret-equivalent" as rationale.

#### PR-218: The staging-hostname ban still has two homes with different mechanisms
- **Severity:** low. **Disposition:** autofix. This is r2 PR-116 residue.
- **Location:** AD-8 *Negative tests* (L139) and Hosted interfaces (L226).
- **Old (AD-8):** " A staging release declaring a production or shared hostname is rejected."
- **New:** delete. The negative-test list keeps "hostnames".
- **Old (Hosted interfaces):** "Shared-infrastructure and production-trust names are reserved in the FQDN pattern and rejected at admission for staging,"
- **New:** "Shared-infrastructure and production-trust names are reserved in the FQDN pattern and rejected for staging by the release validator and at admission,"
- **Words:** −6.
- **Decision check:** C-18 and both mechanisms are kept in one home.

#### PR-219: The Secrets row restates Production profile and AD-12 and carries seed detail
- **Severity:** low. **Disposition:** autofix.
- **Location:** Secrets (L222), AD-12 *Unseal* (L180), Owned work "Secrets, identity, network and transport" (L450).
- **Old 1:** "Per-app tokens are mounted per pod (seed: `vaultTokenMountPath` with `dapr.io/volume-mounts`); they and any Kubernetes Secrets are documented bootstrap exceptions, each with a named renewal owner."
- **New 1:** "Per-app tokens are mounted per pod; they and any Kubernetes Secrets are documented bootstrap exceptions." Production profile already gives every time-bound credential a named renewal owner.
- **Old 2:** " Administrator and the recovery deputy are the unseal and recovery-key custodians."
- **New 2:** delete. The sentence moves into AD-12, as below.
- **Old 3 (AD-12):** "*Unseal:* Each environment's OpenBao unseals manually by Administrator or the deputy within declared coverage."
- **New 3:** "*Unseal:* Administrator and the deputy are the unseal and recovery-key custodians and unseal each environment's OpenBao manually within declared coverage."
- **Old 4 (Owned work):** "classification of the existing OpenBao,"
- **New 4:** "per-app token mounts (seed: `vaultTokenMountPath` with `dapr.io/volume-mounts`); classification of the existing OpenBao,"
- **Words:** −8 net.
- **Decision check:** C-14 (seed), C-19 (custody and unseal) and the renewal-owner autofix are all kept.

#### PR-220: Backup coverage restates AD-12 and AD-6
- **Severity:** low. **Disposition:** autofix. Neither sentence is PRD wording; the PRD says only that tenant-key custody is "separated from ordinary data backups".
- **Location:** Backup coverage and cadence (L276).
- **Old 1:** " The environment OpenBao backup unit holds no tenant-key material."
- **New 1:** delete. AD-12 *Key custody* already says the store is "excluded from ordinary OpenBao snapshots … never held with ciphertext backups".
- **Old 2:** " The Keycloak event export ships within a declared bound."
- **New 2:** delete. AD-6 says "exported off-cluster within a declared bound", and Recovery point says "event-export lag within its bound".
- **Words:** −18.
- **Decision check:** C-52 and the AD-12 key-custody rule are kept.

#### PR-221: Hosted interfaces restates AD-13's gateway
- **Severity:** low. **Disposition:** autofix. This is r2 PR-118 residue.
- **Location:** Hosted interfaces (L226).
- **Old:** "The gateway is the composed `eventstore` host's authenticated command, query and metadata endpoint; ingress terminates TLS and routes but makes no authorization decision, and no external caller bypasses it to reach pods."
- **New:** "Ingress terminates TLS and routes to the AD-13 gateway but makes no authorization decision, and no external caller bypasses the gateway to reach pods."
- **Words:** −8.
- **Decision check:** AD-13 *One host* ("exactly one `eventstore` app, which is the gateway") is kept.

#### PR-222: Synthetic identities restates the AD-7 sandbox token rule
- **Severity:** low. **Disposition:** autofix.
- **Location:** Synthetic identities (L227).
- **Old:** "rotated through an Administrator attempt; each job mints short-lived tokens for its sandbox."
- **New:** "rotated through an Administrator attempt."
- **Words:** −8.
- **Decision check:** AD-7 *Module-code sandbox* keeps it: "receives only short-lived synthetic-client tokens".

#### PR-223: AD-1 *Chart* re-lists the application-package tier contents
- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-1 (L66).
- **Old:** "to the generated application chart, which carries workloads, services and Gateway API routes."
- **New:** "to the generated application chart (Release tiers)."
- **Words:** −6.
- **Decision check:** The Release tiers Application package row keeps the contents, including Gateway API routes (C-23).

#### PR-224: Owned work rows restate First shared versions and AD-11
- **Severity:** low. **Disposition:** autofix.
- **Location:** Owned work: First production attempt (L454), Composed host (L447), Trigger for additional MCP transports (L462).
- **Old 1:** "The attempt, lock and stop store reachable from all executors and the monitor; provenance;"
- **New 1:** "Provenance;". The First shared versions row "Attempt lock, record and promotion-stop store | … | All executors, monitor | First production attempt" owns it.
- **Old 2:** "Build the composed host with zero or more extensions, register its lifecycle subject and obtain EventStore's ratification;"
- **New 2:** "Build the composed host with zero or more extensions;". The First shared versions row "AD-13 ratification and composed-subject issuer registration" owns it.
- **Old 3:** "Outside the MVP; each needs an AD admitting it under AD-14, and forwarded headers never establish the actor."
- **New 3:** "Outside the MVP (AD-11 New surfaces)."
- **Words:** −33.
- **Decision check:** C-05, C-32 and C-29 (the forwarded-header override stays in AD-11 *Profiles and actor* and the McpCli source row) are kept.

#### PR-225: Source Precedence restates Maintenance, Release tiers and Terms
- **Severity:** low. **Disposition:** autofix.
- **Location:** Source Precedence paragraph (L312), Memories row (L317), Builds row (L319).
- **Old 1:** "Neither blocks enrollment, and Projects AD-30 release gates may not demand Platform HA or RPO-0 evidence; aligning their source documents is maintenance."
- **New 1:** "Neither blocks enrollment, and Projects AD-30 release gates may not demand Platform HA or RPO-0 evidence." The Maintenance row owns the alignment work. The envelope numbers are untouched.
- **Old 2:** "direct-provider access only per the AD-9 exceptions; Memories.Aspire digest sets as qualification inputs."
- **New 2:** "direct-provider access only per the AD-9 exceptions." The Release tiers environment-layer row owns this. This is r2 PR-123 residue.
- **Old 3:** "`hexalith-module` is the Platform tool and runner and `hexalith-evidence` the readiness validator; `hexalith.module-manifest.v1` implements the declaration schema; Builds owns"
- **New 3:** "`hexalith-evidence` is the readiness validator; Builds owns". Terms and Module declaration own the rest.
- **Words:** −24.
- **Decision check:** C-02 is kept in Terms and Module declaration. C-16 is kept in Release tiers.

#### PR-226: Stack and Structural Seed repeat the Paradigm disclaimer and memlog observations
- **Severity:** low. **Disposition:** autofix.
- **Location:** Stack header (L327), Stack note (L341), Structural Seed prose (L391).
- **Old 1:** "Seed from commit b9410d3 and the Builds catalog at 0610f78 on 2026-09-27; the Builds catalog is the package version authority, and per-row evidence is in the memlog. These are existing pins, not upgrade requests or a proven production combination."
- **New 1:** "Seed from commit b9410d3 and the Builds catalog at 0610f78 on 2026-09-27; per-row evidence is in the memlog. These are existing pins, not upgrade requests." AD-4 and the Builds row own catalog authority. The Paradigm disclaimer covers "not proven".
- **Old 2:** "Keycloak (observed 26.7.4), OpenBao, data-service, broker, Traefik (observed 3.7.13), Calico,"
- **New 2:** "Keycloak, OpenBao, data-service, broker, Traefik, Calico,". The memlog `(version)` entries hold the observations.
- **Old 3:** " Executors, monitors, replacement capacity and complete backup coverage are not yet demonstrated."
- **New 3:** delete. The Paradigm says status "does not establish deployed capability", and the G1 and G2 Owned work rows own these items.
- **Words:** −30.
- **Decision check:** C-62 (the Stack header and catalog authority) is kept in the header and the Builds row.

#### PR-227: Who starts the recovery executor is stated in three places
- **Severity:** low. **Disposition:** autofix.
- **Location:** DR sequence intro (L300), AD-12 *Model* (L178). The home is AD-7 *Recovery executor*.
- **Old 1:** "Run by the recovery executor, started by Administrator or the deputy." (the whole intro line)
- **New 1:** delete. Step 2 and step 7 keep their Administrator-or-deputy actions.
- **Old 2:** "run by the AD-7 recovery executor under Administrator or the deputy;"
- **New 2:** "run by the AD-7 recovery executor;"
- **Words:** −16.
- **Decision check:** C-09 (the trigger path) is kept verbatim in AD-7.

#### PR-228: Production profile restates the Release tiers change path
- **Severity:** low. **Disposition:** autofix. Superseded if PR-204 is accepted.
- **Location:** Production profile (L223).
- **Old:** "An environment-layer change is its own attempt, staging first; the production attempt"
- **New:** "A production environment-layer attempt (Release tiers)"
- **Words:** −6.
- **Decision check:** C-16 is kept, and the Release tiers row keeps "its own attempt, staging first".

#### PR-229: Micro-duplicates
- **Severity:** low. **Disposition:** autofix. Each edit is under 10 words.
- **Edit a (Binding classes, L219):**
  - Old: "*Lifecycle records* follow EventStore AD-11: EventStore-issued for the server package, Platform-issued for the composed image;"
  - New: "*Lifecycle records* follow EventStore AD-11 with the AD-13 issuers;"
  - Words: −6.
- **Edit b (Catalogs, L220):**
  - Old: "Activation follows the production attempt diagram; recovery commits the rollback generation."
  - New: "Activation follows the production attempt diagram."
  - Words: −5. AD-3 and the diagram own the recovery commit.
- **Edit c (Rollout, L271):**
  - Old: "Write the Platform-issued production-promoted record, bound to the active profile digest, before the Helm upgrade;"
  - New: "Write the production-promoted record before the Helm upgrade;"
  - Words: −7. AD-13 owns the issuer. Binding classes and Production profile own the digest binding.
- **Edit d (AD-6 *Realms*, L113):**
  - Old: "The contract version is bound in the release record and applied forward-only by Administrator before the attempt that needs it."
  - New: "Administrator applies each contract version forward-only before the attempt that needs it."
  - Words: −8. The AD-2 *Record* list owns the binding.
- **Edit e (First shared versions, L426):**
  - Old: "Realm contract with client-to-surface map, token-exchange permissions and preconditions (requester in the subject token audience, per-client standard-exchange switch, refresh-token setting) and synthetic tenant identifier"
  - New: "Realm contract instance (AD-6), with token-exchange preconditions: requester in the subject token audience, per-client standard-exchange switch, refresh-token setting"
  - Words: −6.
- **Edit f (AD-6 *Administration*, L116):**
  - Old: "within a declared bound for DR replay."
  - New: "within a declared bound."
  - Words: −3. This is rationale; the memlog holds it.
- **Words:** −35 in total.
- **Decision check:** C-35 and C-58 are kept, and the V-series realm decisions are unchanged. Every fact keeps one home.

### C. Fit moves

#### PR-230: HotReload is rollout semantics, not isolation
- **Severity:** low. **Disposition:** autofix.
- **Location:** AD-8 *Dapr* (L138) to AD-1 *Dapr resources* (L67).
- **Old (AD-8):** "allow callers by trust domain, namespace and app ID, and disable HotReload;"
- **New:** "allow callers by trust domain, namespace and app ID;"
- **Old (AD-1):** "*Dapr resources:* Rendered from the canonical profile and declarations and placed per Release tiers."
- **New:** "*Dapr resources:* Rendered from the canonical profile and declarations and placed per Release tiers; hosted Configurations disable HotReload."
- **Words:** +1.
- **Decision check:** C-43 is kept. A chart-generator agent reads AD-1, not AD-8.

#### PR-231: AD-6 *Authentication* binds authorities outside identity realms
- **Severity:** low. **Disposition:** discuss, because it is a move only.
- **Location:** AD-6 (L117) to Roles (L34).
- **Finding:** MFA on GitHub, OpenBao, the registry, DNS, the backup store and custody is a property of the Administrator and deputy roles, not of the realms. An agent working on the registry or backup store will not look in AD-6.
- **Old:** "Every Administrator and deputy authority — GitHub, Keycloak administration, OpenBao, registry, DNS, backup store and custody — requires phishing-resistant MFA; sealed break-glass credentials alert on use."
- **New:** Move the sentence verbatim to the end of **Roles**. AD-6 *Authentication* keeps "Public clients never receive `offline_access`."
- **Words:** 0.
- **Decision check:** C-13 is kept verbatim.

#### PR-232: The Owned work "Secrets, identity, network and transport" row re-lists AD-6, AD-7 and AD-8
- **Severity:** low. **Disposition:** discuss. The row's gate-checklist value may be wanted by sprint planning.
- **Location:** Owned work, First staging deployment (L450).
- **Old:** "Keycloak inventory; realm contract with event export channel, bound and retention; token-exchange clients; per-environment OpenBao and tenant-key store; classification of the existing OpenBao, splitting its shared `openbao-runtime-bootstrap` token per app and renewing before its 2027-07-19 expiry; executor credentials or OIDC; module-code sandbox; CNI enforcement; Pod Security compatibility of data services and OpenBao; Gateway API CRDs and the Traefik Gateway provider; hostname admission and staging HTTP-01; Dapr trust domains, HotReload off and workflow policies; all negative isolation cases."
- **New:** "Implement the AD-6, AD-7 and AD-8 rules and Hosted interfaces for staging, plus: Keycloak inventory; classification of the existing OpenBao, splitting its shared `openbao-runtime-bootstrap` token per app and renewing before its 2027-07-19 expiry; proof of CNI enforcement; Pod Security compatibility of data services and OpenBao; Gateway API CRDs and the Traefik Gateway provider." If PR-219 lands first, keep its seed clause.
- **Words:** −24.
- **Decision check:** Every dropped item is a rule already stated in AD-6, AD-7, AD-8 or Hosted interfaces. Only the brownfield-specific work stays listed.

---

## 4. Achievable net

| Bucket | Words |
| --- | --- |
| Autofix trims (PR-201, 202, 206–230; PR-228 counted) | **−310** |
| Discuss: PR-203 (−6), PR-204 (+9, forgoes PR-228's −6), PR-205 (+7), PR-231 (0), PR-232 (−24) | about −8 |
| **Achievable net** | **about −318, to roughly 11,640 words (−2.7%)** |

Going below about 11,600 words would require one of three things, all outside this review's mandate: dropping decisions (the AD-11 legacy list, the AD-14 chains, the AD-8 negative-test matrix), paraphrasing protected PRD wording (R&R, Local tool, AD-10), or moving the Module declaration field list before the schema is published (the r2 PR-124 defer trigger still stands).

## Considered and not raised

- **R&R rows, the Source Precedence envelope, Local tool and readiness, and AD-10.** These are protected PRD wording. Automatic recovery's "to Administrator and the deputy" is PRD L203 wording and stays.
- **Sub-bullet labels (about 120 words).** They were requested by r2 (C-56). They are the main findability gain.
- **The Structural Seed observation list.** It is the only home for "Traefik serving the `nginx-public` class" and "existing Memories resources". A side note for the reconcile lens: C-23's "Platform never renders routes on the compatibility class; existing shared Ingresses stay on it until migrated as shared-infrastructure changes" is only implicit, through "Gateway API routes".
- **AD-11 retirement gate into Owned work.** This would put a binding rule in Deferred, which r2 PR-107 flagged as an anti-pattern.
- **AD-7 recovery executor into AD-12.** This changes nothing and saves nothing.
- **AD-6 Keycloak DR exception compressed to a DR-sequence pointer.** It is authority-bearing and must stay findable from AD-6.
- **Frontmatter review-folder sources.** They are traceability, not cost.
- **The Owned work "Staging evidence policy" row.** It overlaps the Staging gate but owns the undecided values (maximum age, triggers).

## Suggested order

1. **Discuss first:** PR-204, which needs a memlog line and decides whether PR-228 applies, then PR-203, PR-205, PR-231 and PR-232.
2. **Autofix the misread risks:** PR-201, PR-202, PR-206, PR-207, PR-208 and PR-209.
3. **Autofix the trims:** PR-210 to PR-227 and PR-229.
4. **Fit moves last:** PR-230, then PR-231 if accepted.
