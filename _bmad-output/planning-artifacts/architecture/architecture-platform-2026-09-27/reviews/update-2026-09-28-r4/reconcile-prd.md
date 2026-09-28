# PRD reconciliation — update 2026-09-28, architecture run 4

Scope: only the finalized PRD's newly accepted CI candidate-artifact rules (A1), attachment rules (A2), expanded degraded-production definition, promotion-stop cause semantics, admission/deputy clarifications and expressly routed downstream bounds. This is input reconciliation, not a broad architecture review. No spine or memlog was edited.

Inputs:

- `prds/prd-platform-2026-09-27/prd.md`, final, updated 2026-09-28.
- Its `addendum.md` and `.memlog.md`, including the final update decisions and gate fixes.
- `architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md`, draft run-3 gate-fix version, 487 lines, and the latest architecture memlog entries.

Line references below refer to the versions read for this report. Paths are relative to `_bmad-output/planning-artifacts/` unless stated otherwise.

## Outcome

The PRD settled the product behavior. Adoption can amend existing AD-4, AD-5, AD-6 and AD-10 and their existing operational conventions; no new paradigm, stack, ownership model or AD number is required by this delta. The PRD explicitly leaves one release-policy fork for architecture: whether an approved release into degraded **non-empty** production also needs compatibility evidence or a named recovery. Several bounded implementation policies and post-DR operating choices are routed to named owners and future gates rather than silently decided.

The latest PRD memlog is decisive: the user selected A2 holding/retention behavior, widened degraded production after the reviewer gate, retained Administrator-only admission control with the deputy containment limitation, and accepted the remaining medium/low clarifications. Do not ask the user to choose these product rules again.

## Required amendments

### P1 — Bind CI candidate evidence to the candidate's actual artifacts

**Authority:** PRD FR-4 lines 123–124; downstream lines 403–404 and 413; candidate glossary line 436; addendum line 44; PRD memlog A1 decision plus final recheck requiring the candidate's own builds of every module artifact.

**Current gap:** AD-4 Package mode (line 100) says the module builds in Release, dependencies use released images, and the host assembles server/extension packages. AD-5 (line 108) makes the runner tier blocking but does not ensure it loads the just-built candidate. A catalog extension package or another build with the same version could still satisfy the text.

**Amend AD-4/AD-5 and the environment-descriptor/runner owned-work handoff:**

- CI evidence names the committed candidate revision and content identities of **every artifact of that module**, including extension packages.
- The running composition reports the identities actually loaded; they must match the candidate's own build outputs. Another revision/build, same-version replacement, or uncommitted local change cannot supply candidate acceptance evidence.
- A baseline-only run is labelled and cannot count as candidate acceptance.
- Preserve package mode: candidate artifacts are built in Release against the catalog's NuGet dependencies; dependency-service pins remain authoritative for modules other than the candidate. The rule must reach the composed host, so testing a domain candidate cannot leave its released extension package loaded there.
- Preserve the existing Platform-workspace rule for EventStore, Memories and McpCli; their independent repository fixture evidence remains insufficient.

This is an evidence/content identity invariant. It does not require choosing a temporary feed, a new artifact registry or a publication mechanism in the spine. Builds can implement materialization under the contract.

**Still routed, not decided:** whether candidate means PR head or merge result; the merge/release decision the evidence gates; reuse rules for unchanged modules. PRD line 404 assigns Platform with Builds before CI evidence acceptance.

### P2 — Give attachments a finite hold and explicit terminal semantics

**Authority:** PRD FR-4 lines 133–145; downstream lines 404 and 413; addendum lines 64–66, 116–117; PRD memlog user decisions for A2 and any-failed-local-run retention.

**Current gap:** AD-10 Outcome says the first terminal outcome alone decides retention, and Attach and cleanup permits compatible attachment without a hold, attached outcomes, post-finish refusal, artifact identities or CI job scope (lines 163–164).

**Amend AD-10 in place:**

1. Attachment checks composition, artifact mode **and artifact identities**, readiness and data isolation. New attachment after the owner finishes is refused unless the environment is retained after failure; hosted attachment remains refused.
2. Accepted attachments hold the environment. Record the owner's first terminal outcome immediately; defer its automatic cleanup until all attached runs end, subject to a finite, displayed hold limit. Cancelling an attachment never stops the owner's environment.
3. At the hold limit, end and report the attachment as **withdrawn**. Withdrawal is neither pass nor failure and supplies no valid integration evidence.
4. A failure of any local run served by the environment, owner or attached, retains the environment under the failed-local policy and lists the failed run. This is the explicit exception to owner-first-outcome cleanup. Later cancellation cannot erase already-retained failure state.
5. Explicit local owner cancellation or developer environment stop ends/reports attached runs first, then cleans owner-created resources. An explicit stop is the way to remove a retained environment; an attachment's own cancellation is not.
6. List retained and pending-cleanup environments with owner, age and attached runs.
7. CI attachments are confined to the same job, end with that job, and job cancellation ends them before cleanup; CI still cleans after every outcome and partial startup.

Update the existing runner-lifecycle owned-work row (line 457) and descriptor contract row, and preserve idempotent cleanup/leftover reporting. A compact owner/attachment outcome table is a possible presentation choice, not a new lifecycle framework.

**Still routed:** the numeric finite hold limit (Platform with Builds, before CI evidence acceptance). The existing ten-minute startup deadline is not that limit.

### P3 — Use the accepted broader definition of degraded production

**Authority:** PRD FR-7 line 193; FR-8 lines 230–243; downstream line 414; glossary line 427; addendum lines 196–200 and 218; memlog explicit user decision after the PRD reviewer gate High.

**Current gap:** Empty or degraded production (spine line 281) admits only no working baseline or last outcome non-working. A previously verified release that later becomes non-working cannot use the documented fixed-release route even though the Promotion stop row already names incidents/probe/health failures.

**Amend the existing Empty or degraded production convention and dependent acceptance wording:**

- Empty means no working baseline. Degraded also includes a recorded incident, probe failure beyond its declared bound, or failed pre-update health check establishing that production is no longer working.
- Such a failed pre-update health check sets the promotion stop as well as refusing the ordinary attempt.
- Preserve one identified Administrator-approved attempt, observed stop revision, common staging/provenance/lock/verification gates, fresh-install candidate proof, and removal of candidate workloads while retaining data on failure.
- A later stop before start blocks the attempt; one set during the attempt allows it to finish but prevents success from clearing that later stop. Do not weaken the existing CAS rule.
- No release runs during any recovery. The post-DR reduced-recovery state is expressly excluded; widening degradation is not an emergency release path for that state.
- Scope Automatic recovery and its diagram to compatible deployment over a working baseline; the approved empty/degraded failure path removes candidate workloads and does not automatically redeploy a previous release.

**Genuine fork:** see Q1 below. The fresh-install staging proof alone does not answer compatibility with persistent data in non-empty degraded production.

### P4 — Make promotion-stop causes and clearance complete

**Authority:** PRD FR-8 lines 218–225 and 232; downstream line 414; addendum lines 230–232; PRD memlog accepted gate clarifications and recheck.

**Current gap:** the Promotion stop row (spine line 288) supplies monotonic revisions and continuing-condition deduplication, but no per-cause disposition or behavior for an unresolved condition after clearance. Diagnostics and notification (line 236) observes unmatched exported admission changes, which covers only one direction of admission drift.

**Amend Promotion stop and its record/monitor contracts:**

- Add admission/record mismatch **in either direction** to the stop triggers, checked on a declared cadence. Detect both an unmatched membership change and a signed record not reflected in enforced admission; watching only incoming export events is insufficient to express this product requirement.
- The Administrator clearing record marks every recorded cause resolved or accepted, while preserving the reason, observed revision and verified working attempt identity.
- A continuing condition is recorded once until it resolves. A cause accepted by the clearing record is not re-recorded until it resolves and recurs. Any other condition still present after clearance is recorded as a new cause.
- Before clearing, Administrator reviews recorded lost-window exceptions and confirms that admission matches the signed records. The same reviews happen **before approving** the exceptional empty/degraded attempt; its success is another clearance route, not a review bypass.
- Preserve monotonic revision/CAS behavior, monitoring set-only authority, and deputy inability to clear.

The cause disposition can extend the existing signed Administrator record and stop store. The PRD does not demand another service or a ticket workflow. Cause IDs and encoding are implementation work under the existing shared record contract.

### P5 — State admission restoration and deputy limits at their existing homes

**Authority:** PRD FR-8 line 226; FR-9 lines 272–275; downstream line 414; addendum lines 242–253 and 317.

**Current coverage:** AD-6 already makes admission records authoritative, restricts admission administration to Administrator, forbids recovery grants, and permits deputy recovery. The recovery sequence removes absent/revoked grants and lost-window review already gates stop clearance rather than reopening. Preserve these settled decisions.

**Required explicit additions:**

- Recovery sequence step 5 records grants made after the recovery cut for Administrator to re-apply; it never creates admission from them. This is the missing operational outcome of the existing no-grant rule.
- State the accepted limitation: while Administrator is unavailable, nobody revokes production admission; the deputy can declare a stop and execute documented recovery but gains no admission-administration right. Put the authority rule once in Roles/AD-6 and the consequence in the accepted risks or recovery convention as appropriate.
- Reopening restores only the ingress state recorded before the incident; before G2, user ingress remains closed. Record the prior ingress state with the existing attempt/recovery context so the recovery sequence cannot silently open G2.
- Lost-window records still precede reopening; Administrator's disposition of those losses gates stop clearance (or approval of the exceptional attempt), not deputy reopening.

Do not redesign recovery to grant the deputy access administration, replay all post-cut grants, or require Administrator presence to reopen. Those would contradict the accepted product decisions.

## Named downstream work to carry, without inventing values

The spine should include or extend a small number of Owned work rows matching PRD lines 400–415. These assignments are part of the input; absent evidence remains a gate failure.

| Item | Owner and deadline in PRD | Required treatment |
| --- | --- | --- |
| Candidate revision (head/merge result), gated decision, unchanged-module evidence reuse, attachment hold limit | Platform with Builds; before CI integration evidence acceptance | Carry as one CI policy handoff, and include A1/A2 proof in the runner implementation row. |
| Availability-probe stop bound, including failures during a locked attempt | Platform with Administrator; before G1 | Preserve FR-7 thresholds; define the external-probe relationship before G1, not at the first post-G1 incident. |
| Identity-provider capture-lag bound; admission-mismatch check cadence | Platform with Administrator; before G1 | Existing realm/event-export and monitor work is close, but tighten its deadline to G1 and name both bounds. |
| Shared-infrastructure currency inventory, cadence, allowed lag, effect on approved attempts | Platform with Administrator; before G3 | The spine already checks support/security currency at attempts and drills. Keep that binding rule; route the remaining policy details and effect on approved attempts, rather than implying the automatic-block rule answers them all. |
| Reduced-recovery operating rules | Administrator with Platform architecture; before G2 | Decide whether automatic promotion may resume before restored G2 conditions, drill cadence while prepared capacity is consumed, and the staging re-establishment procedure. Preserve no emergency release path. |
| Degraded non-empty compatibility/named recovery | Platform architecture owner with Administrator; before first applicable production attempt | Q1 below; explicit design fork, not merely evidence collection. |
| Tenants-owner review of lifecycle declarations, synthetic staging leftovers, production synthetic tenant provisioning | Tenants owner with module owners and Platform; before release-gate qualification and first production verification | Add to existing staging/module-intake owned work; no new synthetic-admission design needed. |

The accepted post-DR consequence is explicit in PRD FR-9 line 280: no four-hour RTO commitment for a further failure needing replacement capacity while in the reduced-recovery state, and incidents report that state. Add it to After DR; the remaining operating choices above can stay deferred to their G2 gate.

## Questions that merit architecture choice

### Q1 — Compatibility or named recovery for non-empty degraded production

PRD line 414 and addendum line 199 explicitly leave this unresolved. The current spine combines a fresh-install/remove-workloads rule (Empty or degraded production) with a general named-recovery requirement whenever compatibility evidence is missing (Release modes and precondition 9). Simply widening the degraded predicate leaves their interaction ambiguous.

The realistic choice is whether non-empty degraded production retains the existing requirement for either usable compatibility evidence or a named recovery, or whether the exceptional attempt also waives that requirement. The latter introduces a materially broader data/recovery risk; it was not authorized by the PRD's expansion of the definition. The conservative continuation of existing decisions is to retain precondition 9, make the interaction explicit, and use a named recovery where current compatibility cannot be established. Confirmation is appropriate because the source expressly routes this decision to architecture.

### Other choices are owned deferrals

Do not turn the numeric hold/probe/capture-lag settings, candidate revision/gate/reuse policy or reduced-recovery operating rules into invented defaults. Keep named owners and the stated G1/G2/G3/evidence gates unless the parent run deliberately chooses to resolve one now. They do not require re-opening A1, A2, admission authority or the degraded definition.

## Source maintenance after adoption

The PRD and addendum currently label A1/A2 and other new rules as awaiting adoption, and describe architecture run 3 as awaiting confirmation. Once this update actually passes its reviewer gate, those status statements need a narrow refresh; the requirements and their stable FR/NFR/SM IDs do not need rewriting. The existing source-alignment row should stop claiming that the addendum's tool identity, notification recipients and deputy follow-up are unsynchronized: the finalized PRD update already corrected those. SPEC synchronization remains separate downstream work.

No external sources were consulted: this reconciliation adopts already accepted internal requirements and binds no new technology or version.

## Final reconciliation disposition — 2026-09-28

**PASS for the scoped PRD reconciliation.** Re-read the updated spine after adoption and the user's Q1 decision. P1–P5 are preserved, Q1 is resolved, and no additional PRD-preservation correction is required. This disposition supersedes the open adoption gaps and Q1 discussion above; the original findings remain as the review history.

| Item | Disposition and current spine evidence |
| --- | --- |
| P1 — CI candidate artifact binding | **Resolved.** AD-4 Candidate mapping (line 102) routes the candidate's own committed Release artifacts through the composed host, McpCli and tests; AD-5 Evidence (line 112) binds expected and actually loaded content identities, refuses substituted/uncommitted inputs and labels baseline-only evidence. Shared descriptor and runner qualification rows carry these rules (lines 451, 464–465). Existing package boundaries, other-module catalog pins and technical-module Platform-workspace rules remain. |
| P2 — Attachment lifecycle | **Resolved.** AD-10 Outcome, Attachments and Stop and cleanup (lines 167–169) cover immediate outcomes, finite displayed holds, withdrawn outcomes, any-local-failure retention, artifact identity checks, owner-finish refusal, listing, same-job CI scope, attachment cancellation, explicit stop and owner/job cleanup ordering. The shared policy/qualification rows prevent consumers selecting independent hold/evidence rules. |
| P3 — Expanded degraded definition | **Resolved.** Empty or degraded production (line 288) and precondition 8 (line 316) include incident, bounded probe failure and failed current health check. Later-stop before/during behavior, single approval, fresh-install proof, candidate removal, no automatic old-baseline recovery, no release during recovery and exclusion of post-DR reduced recovery are explicit. The diagram is scoped to ordinary compatible deployment (line 263), and Automatic recovery explicitly excludes approved empty/degraded attempts (line 294). |
| P4 — Stop causes and clearance | **Resolved.** Diagnostics and notification (line 241) compares live admission and records in both directions on the declared cadence. Promotion stop (line 295) preserves monotonic revisions/CAS, each-cause dispositions, accepted-cause recurrence, re-recording of other persisting conditions, lost-window handling and admission checks before normal clearance or exceptional approval. Monitor/deputy clearance authority was not enlarged. |
| P5 — Admission and deputy limits | **Resolved.** Roles (line 37) states Administrator absence/containment and prior-ingress limits; attempt records carry pre-incident ingress state (line 231). Recovery step 5 lists post-cut grants for Administrator without replaying them (line 327), and step 7 restores only prior ingress with pre-G2 closure (line 329). Recovery exception reporting still precedes reopening and Administrator review gates promotion resumption (line 302). |
| Q1 — Retained-data degraded recovery | **Resolved by user decision.** Release modes, Empty or degraded production and precondition 9 (lines 287–288, 317) preserve valid compatibility evidence **or** a named data-restore recovery with checks, maximum duration and usable post-lock recovery point. A fresh installation with no retained application data is explicitly distinguished. The first-applicable-attempt qualification row proves this rule and the accepted post-cut admission/erasure protections (line 483). |
| Downstream bounds and operating policy | **Preserved as owned and gated work.** Candidate selection/gated decision/evidence reuse/finite hold are shared before acceptance and attachment implementation (line 465); probe/export/admission bounds before G1 (line 484); reduced-recovery rules before G2 (line 489); remaining currency policy before G3, retaining existing attempt/drill cadence (line 490). No numeric values were invented. Tenants lifecycle review/cleanup/provisioning is included (line 479), and After DR preserves the reduced-state RTO limitation and no emergency release path (line 303). |

Expected PRD/addendum adoption and confirmation-status refreshes are excluded from findings as instructed; source metadata will be refreshed only after the architecture gate passes. No source, spine or memlog edits were made during this recheck. This is a document-preservation result, not implementation or production qualification evidence.
