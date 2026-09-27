# Validation reconciliation — PRD update, 2026-09-27

## Verdict and basis

The updated PRD addresses the substantive high-impact validation themes: release scope, compatibility gates, approved releases, identity/automation isolation, independent recovery monitoring, McpCli eligibility, and actionable deputy/response coverage. The remaining items below are focused wording or implementation-policy handoffs; they do not require changing the seven-module scope, accepted thresholds or requirement IDs. No implementation or production qualification was performed.

Reviewed the current `prd.md` (draft, 368 lines), `addendum.md` (344 lines), `.memlog.md`, and the original `validation-report.md`, `review-rubric.md`, `review-adversarial-general.md`, and `review-consistency.md`. The addendum author confirmed editing had finished before reconciliation. Line references identify this reviewed snapshot; headings and requirement IDs are stable anchors if subsequent polishing moves lines.

Later user decisions govern stale architecture wording: deputy receives alerts and may restore, verify and reopen; Administrator alone resumes promotions and administers admission. The four-hour RTO applies to outages beginning within declared coverage, without pausing/resetting the outage clock; backup/RPO/monitoring remain continuous (`.memlog.md:76–77`, `prd.md:32,188–189,282–286`).

Disposition meanings: **applied** is covered in the updated documents; **already satisfied** was already required; **unsupported proposal** is a suggested mechanism/commitment not established by accepted decisions; **downstream evidence** remains owned implementation or qualification work. Unsupported proposals are not silently converted into product obligations.

## Remaining focused gaps

1. **Source target and technical-module composition — wording.** FR-2 still says EventStore, Memories and McpCli “are declared directly” (`prd.md:78`), while historical evidence says McpCli is absent (`addendum.md:20`). State that Platform **must declare** the technical modules directly before accepting their source-debug evidence. Replace “selected scenario” with “module configuration.” Explicitly say technical-module Platform integration uses its declared minimum composition in the Platform workspace; SM-3 should require evidence for each supported module, not only the Parties example (`prd.md:109,302`). Covers CON-3/4 and the remaining part of ADV-35; no new module workspace is needed.
2. **Declaration weakening — owned policy still needed.** Suite/release digests and non-empty checks now bind evidence (`prd.md:147–156`), but cannot by themselves detect a release that removes the failing critical flow. ADV-4's blanket Administrator approval for every removal/remap is not an accepted architecture rule. Assign review of critical-flow/smoke declaration removals and remappings to module owners with Platform/Builds, with an explicit policy before the first production gate; do not describe digest binding as having solved the semantic coverage problem. This can remain a downstream policy decision with that owner and gate.
3. **Staging trigger and rerun acceptance — named handoff needed.** Serialization, evidence freshness and wrong-release rejection are applied (`prd.md:151,154`). The trigger that publishes/deploys a staging candidate and whether a fresh rerun supersedes failed evidence remain unstated. Record them as Platform/Builds-owned gate policy before first staged production evidence, keeping failed history and current-baseline checks. No universal rerun ban or numeric retry quota is supported. Covers rubric Scope honesty and the residual of ADV-24.
4. **Known-bad baseline — clarify existing stop rule.** The updated text requires a healthy current baseline and durable stop, but post-window incidents still have only an “operational incident” sentence (`prd.md:170,189,193`). Clarify that a baseline known or suspected to be bad cannot qualify a subsequent automatic promotion merely because its original smoke suite passes, and that Administrator must record a verified resolution/current baseline before resumption. Do not implement ADV-6's proposed automatic search through older releases; the accepted one-attempt/no-cycling policy remains.
5. **Artifact/evidence retention — carry accepted bound into handoff.** Durable retrievability is now required (`prd.md:192`), and the addendum describes retained artifacts (`addendum.md:169–171`), but neither states AD-2's existing retention bound: backup retention or life as a rollback target, whichever is longer (`architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md:63`). Add that accepted bound to the addendum or explicitly cite it in the retention qualification work. No new arbitrary logging period is needed.

## Adversarial findings: full disposition

Source finding references below are to `review-adversarial-general.md` in the parent directory.

| Finding(s) and original reference | Disposition | Current evidence and practical limit |
| --- | --- | --- |
| ADV-1, release unit, :11 | Applied | `prd.md:42,138,146,354`: complete enrolled composition includes unchanged modules/dependencies; all seven remain final MVP scope. Git references do not define gate scope. |
| ADV-2, compatibility, :18 | Applied; original overstatement corrected | NFR-1 already required compatibility. It is now fail-closed in FR-6 and measured by SM-5/SM-C1: `prd.md:152,271,304,309`. Module classifications and Platform rehearsal name ownership. |
| ADV-3, automation isolation, :25 | Applied | `prd.md:112,228,240,292–294,303,310`: CI has no hosted credentials; workload, automation, recovery and identity-management boundaries and direct negative checks are explicit. |
| ADV-4, gate weakening, :32 | Downstream evidence; proposed approval mechanism unsupported | Digests and exact evidence are applied (`prd.md:151,156`); semantic removal/remapping policy is residual gap 2. Nothing claims the original recommendation was already accepted. |
| ADV-5, approved/manual/resume, :39 | Applied | `prd.md:44,50,153,182,189,192`: approved mode retains common gates, incompatible changes name recovery, Administrator alone clears stop. It is not a general permission to bypass evidence. |
| ADV-6, revoked baseline, :46 | Applied in part; wording gap | Current health, durable stop and verified replacement are in `prd.md:170,189,192`; residual gap 4 clarifies latent incidents. Automatically skipping through old releases is unsupported and contradicts no cycling. |
| ADV-7, RTO coverage, :53 | Applied by new user decision | `prd.md:282–286,305`: target applicability uses outage start; coverage ending cannot stop the clock; full durations recorded; continuous RPO remains. Annual surprise drills and a clock beginning at acknowledgement are unsupported proposals. |
| ADV-8, deputy, :60 | Applied by new user decision; downstream qualification | `prd.md:32,188–189,209–210,327,329`; `addendum.md:336`: independent access, alerts and restoration authority must be demonstrated; Administrator retains admission/promotion authority. No new deputy exercise frequency was invented. |
| ADV-9, freshness/monitor silence, :67 | Applied | `prd.md:208–210,305`: complete validated points, warning before one hour, 15-minute freshness, five-minute availability, hourly independent silence check. Exact pre-hour warning threshold is implementation policy, not a new 45-minute promise. |
| ADV-10, off-site copies, :74 | Applied | `prd.md:209,286`: copies always off-site; whole-site recovery still depends on independently located exercised capacity. Backup placement does not prove site recovery. |
| ADV-11, technical/tool obligations, :81 | Applied | `prd.md:70,138,146,225,263`: essential service/tool flows count, no silent exemption, McpCli is a caller-hosted client with connected demonstration. |
| ADV-12, empty catalog, :88 | Applied; quota proposal unsupported | `prd.md:261,303`: positive named operations plus refusal checks; empty catalog cannot pass. No arbitrary requirement for one query and one command per module. |
| ADV-13, production entry, :95 | Applied; proposed gate conflation rejected | `prd.md:46–50,303–305`: G1 closed, G2 recovery/access and controlled test admission, G3 SM-5. Requiring SM-5 before G2 would erase the accepted sequencing. |
| ADV-14, restored-copy isolation, :102 | Applied | `prd.md:211,213,292–294,310`: quarantine and restricted restored copies, no live/shared-authority mutation in drills, negative checks. |
| ADV-15, shared capacity, :109 | Applied; downstream evidence | `prd.md:38,112,228`; `addendum.md:316,319,342`: separate state/credentials, resource limits, disposable CI and residual shared-node risk. No unaccepted node/site availability guarantee. |
| ADV-16, configuration/infrastructure bypass, :116 | Applied | `prd.md:154,269`; `addendum.md:169,191`: all workload-affecting changes serialized, infrastructure forward-only, current authority retained. Not every environment-layer change is misclassified as an application rollback. |
| ADV-17, server enforcement, :123 | Applied; implementation evidence | `prd.md:260–261`; `addendum.md:26`: authenticated surface/actor, gateway reauthorization and direct public-token negative checks. Audit implementation must preserve authenticated identity rather than accept asserted actor labels. |
| ADV-18, recovery set age, :130 | Applied; proposed formula superseded | `prd.md:208,281,361`: complete verified set at one declared cut; age is failure time minus cut. Review's “oldest member” formula is not the accepted recovery model. |
| ADV-19, Platform inventory, :137 | Applied; downstream evidence | `prd.md:205–206,327`: Platform retained releases/records, shared identity/secrets and module-owned integrity hooks included. Inventory completeness is verified during qualification; no unaccepted cluster-scanning tool required. |
| ADV-20, versions/composition, :144 | Applied; downstream evidence | `prd.md:84,151,322`; `addendum.md:34,42,169–171`: common source identity/catalog, schema and retained release binding. Actual source-to-package parity remains qualification. |
| ADV-21, missing roots/skew, :151 | Applied | `prd.md:84`; `addendum.md:34`: declared source must exist, other dependencies use catalog packages, no fallback, duplicate source/package fails, CI tool/submodule commit match. |
| ADV-22, GitHub/private evidence, :158 | Applied; accepted risk | `prd.md:188,210,284`; `addendum.md:206,319,342`: controlled evidence, owner/deputy delivery and declared acknowledgement bound. GitHub remains sole channel; no invented alternate pager or automatic escalation schedule. |
| ADV-23, verification mechanics, :165 | Applied | `prd.md:171–176`: sampled availability, cadence, release identity, check timeout, deadline, bounded continuation and no reset. Numeric cadence/grace qualification belongs to owner (`prd.md:326`). |
| ADV-24, staging concurrency/reruns, :172 | Applied in part; downstream evidence | `prd.md:151,154`: serialization, age and identity addressed. Trigger/rerun acceptance is residual gap 3; no new global rerun ban or version-number ordering rule. |
| ADV-25, auditable decisions, :179 | Applied; retention handoff gap | `prd.md:188–192,304,309–311`: decisions, approvals, outcomes and baseline survive failed environment; residual gap 5 carries accepted artifact retention. Counter-metrics remain defined demonstrations/audits, not a claim of universal incident observation. |
| ADV-26, runner loss/orphans, :186 | Applied with selected mechanism; downstream evidence | `prd.md:112,121–123`; `addendum.md:52,54`: disposable GitHub-hosted runners, run ownership, idempotent cleanup and leftovers. Persistent cluster TTL/sweeper is an unsupported mechanism for this selected hosting; runner-loss cleanup evidence remains with lifecycle qualification (`prd.md:322`). |
| ADV-27, local identity, :193 | Downstream evidence; architecture already decides contract | Architecture AD-6 (`ARCHITECTURE-SPINE.md:87`) generates local/CI/staging/production identity from a common contract; `prd.md:257,292,322–324` and `addendum.md:318,335` require separation and qualification. A separate product-selected local identity provider is unnecessary. No production credentials enter CI. |
| ADV-28, synthetic smoke writes, :200 | Already required; clarified; downstream evidence | `prd.md:168,292`: dedicated synthetic identities/data, no real-user change/external effects, purpose-limited permissions. Specific tenant/partition choice and growth policy belong to module smoke qualification; no arbitrary product retention invented. |
| ADV-29, erasure, :207 | Applied to accepted scope | `prd.md:212,288`; `addendum.md:295–301,324`: Memories continuity, lost-window exception, reconciliation and module obligations retained. No unprovided legal classification or universal off-site erasure ledger inferred. |
| ADV-30, admission/admin, :214 | Applied; downstream evidence | `prd.md:240,294`; `addendum.md:317`: Administrator-only auditable admission, no implicit/staging grants, revocation denial. Additional periodic review/revocation SLA was not previously accepted. |
| ADV-31, Works/Agents, :221 | Already sourced; applied migration context | `addendum.md:14,315`: existing Works parity rollback path and Agents story remain historical ownership context; domain hosting freezes until parity retirement; seven-module acceptance remains in `prd.md:36,42`. No deletion or broadened MVP inferred. |
| ADV-32, interfaces/exposure, :228 | Applied; downstream evidence | `prd.md:225,321,325,346`; `addendum.md:337`: declarations include exposure and authorization, unavailable/private surfaces not advertised; ingress/DNS details qualify before G1. No presumed Internet exposure from DNS names/private address. |
| ADV-33, rotation/configuration, :235 | Applied | `prd.md:186,211,269–273`; `addendum.md:171,299`: current authority survives rollback; restored secrets rotate and post-cut revocations replay before reopening. |
| ADV-34, mandatory baseline, :242 | Already required; validator elaboration | `prd.md:93`; `addendum.md:42`: baseline remains mandatory and incompatible/missing declarations fail validation. Explicit “no silent injection” wording would be a useful minor clarification, not a new product choice. |
| ADV-35, coverage/adoption, :249 | Downstream evidence; wording gap | Seven-module scope is explicit (`prd.md:42`), but technical per-module SM-3 evidence remains implicit (residual gap 1). Scheduled complete-environment CI frequency is an unsupported additional commitment. |
| ADV-36, startup override, :256 | Already required; downstream evidence | `prd.md:114,313`: finite justified override and actual time reporting. Exact build/pull boundary and recorded justification format belong to runner qualification (`prd.md:322`). An arbitrary maximum timeout is unsupported. |
| ADV-37, retained-run next start, :263 | Applied | `prd.md:118–123`: fresh owned environment or named collision failure, compatible explicit attach, first terminal outcome and leftover reporting. |
| ADV-38, deployment disruption, :270 | Unsupported new availability promise; existing limits preserved | `prd.md:171–178,187,331`: numeric attempt limits are accepted, no zero-downtime/uptime SLO. Summed timeouts are not a guaranteed outage ceiling, especially with bounded grace or operator intervention. |

## Consistency findings: complete mapping

Original locations are in `review-consistency.md`.

| IDs / source reference | Disposition | Evidence |
| --- | --- | --- |
| CON-1/2, :9/:17 | Applied | Eligibility in intro/glossary/SM-4/handoff, concrete positive/refusal acceptance: `prd.md:245,261,303,321,347–348`. |
| CON-3/4, :25/:33 | Wording and downstream evidence | Source target/technical minimum composition are residual gap 1. `addendum.md:20` is correctly historical but FR-2 still needs target phrasing. |
| CON-5, :41 | Applied | Domain-only module workspace glossary: `prd.md:340`. |
| CON-6, :49 | Applied by later user decision | `prd.md:32,188–189,327,329`; future spine synchronization explicitly owned. |
| CON-7, :70 | Applied | Both release modes and G3 in `prd.md:44,50,152–153`; incompatible recovery exception preserved. |
| CON-8, :78 | Applied by later user decision | `prd.md:282–286`; no paused clock or implied continuous staffing. |
| CON-9, :86 | Applied | `prd.md:84`; `addendum.md:34`: root source/catalog-package distinction. |
| CON-10, :94 | Applied | `prd.md:38`; `addendum.md:316`: shared capacity does not mean shared application data-service instances. |
| CON-11, :102 | Applied | `prd.md:212,288`: explicit accepted lost-window exception and Folders/Projects envelope. |
| CON-12, :112 | Applied | `prd.md:119,315–329`; `addendum.md:311–340`: decisions distinguished from qualification work, no stale CI/realm/cleanup choice. |
| CON-13, :120 | Applied in PRD; spec synchronization downstream | `prd.md:303,329`: controlled test admission before general opening. SM-5 still gates G3, not G2. |
| CON-14, :128 | Downstream artifact maintenance | PRD correctly distinguishes domain/technical workspaces; stale spec brownfield is assigned via `prd.md:329`; do not copy its error into PRD. |
| CON-15, :136 | Applied | `prd.md:70,225,263`: client connection/eligible invocation establishes McpCli availability. |

## Rubric-specific reconciliation

Every rubric issue is either covered above or explicitly dispositioned here:

- **Decision readiness** (`review-rubric.md:17–27`): RTO/deputy now decided and measurable. Failed rollback response is not silently converted into NFR-2 backup-restoration coverage; production incident availability remains outside an invented SLA. Known-bad baseline stop wording is residual gap 4.
- **Substance** (`:35–45`): deputy now has authority, delivery, custody and pre-G2 proof; new authority requires downstream architecture synchronization before implementation (`prd.md:329`).
- **Strategic coherence** (`:61–64`): brief-sourced problem restored (`prd.md:20`), all-seven final adoption retained (`:42`), legacy hosting retirement context recorded (`addendum.md:315`). A new numeric adoption KPI is unsupported.
- **Done-ness** (`:75–92`): interfaces, compatibility gate, recovery set and module integrity ownership addressed. “Selected scenario” remains a minor phrase to fix in residual gap 1; minimum diagnostic contents and runtime evidence retention remain qualification details, with accepted artifact retention in residual gap 5.
- **Scope honesty** (`:98–106`): staging sequencing and legacy migration are captured, but trigger/rerun policy needs explicit handoff (gap 3). Works/Agents are not added to MVP by historical notes.
- **Downstream usability** (`:120–146`): release and eligibility definitions/owners are applied. Domain/technical workspace target is gap 1. Add a domain-member list and avoid implying Tenants starts a duplicate Tenants instance if polishing glossary; requirement does not demand duplicate services.
- **Mechanical/status** (`:151–159`): draft status correctly reflects an in-progress update; stable IDs and selected thresholds retained. Final link/ID/status checks belong after review fixes. No claim is made here that runtime or production gates passed.

## Scope of this reconciliation

Only this report was written. PRD, addendum, memlog, architecture, spec, source, runtime, deployment and Git state were not modified by this reviewer. Later PRD fixes should be checked against the five focused gaps, and their dispositions appended below or recorded in the update review resolution artifact; the original validation reports should remain historical evidence.
