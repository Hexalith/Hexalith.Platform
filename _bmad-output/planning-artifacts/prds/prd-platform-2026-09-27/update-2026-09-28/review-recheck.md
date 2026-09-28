# Recheck of reviewer-gate fixes: PRD update 2026-09-28

- **Checked:** `prd.md` (443 lines) and `addendum.md` (355 lines), both drafts dated 2026-09-28, after the gate fixes. Neither document was edited.
- **Against:** `review-rubric.md`, `review-recovery-access.md`, `review-adversarial.md` and `review-consistency.md`. Their line numbers refer to `prd-before-gate-fixes.md` (cited here as `old:N`).
- **User decisions applied:** the last six `.memlog.md` entries:
  - degraded production includes a verified baseline shown broken by an incident, a probe failure beyond its bound or a failed pre-update health check; architecture adopts this before G2;
  - the deputy containment limitation is stated;
  - any failed local run, owner or attached, retains the environment;
  - other medium/low clarifications are applied, and items needing an architecture decision are routed.
- **Also consulted:** the architecture spine (`spine:N`, 487 lines) to test routing claims.
- **References:** current 1-based lines, written `prd:N` and `add:N`.

## Verdict

**The gate High is resolved; eleven findings are partly applied; there are two medium and nine low new problems.**

- **The gate High is resolved.** A verified baseline that stops passing its checks is now degraded (prd:229, prd:192, prd:240). Administrator's one-attempt approval then routes it to a fixing release. Adoption is routed before G2 (prd:395), and the "settled" sentence names the exceptions (prd:377).
- **Other findings.** Every other High/Medium finding is resolved, routed or partly applied. None is unresolved.
- **New problems.** The fixes introduce two medium problems, both at the edges of the new degraded definition:
  - a persisting condition that the baseline's own checks do not see (N-1);
  - over-broad "during or after disaster recovery" wording (N-2).

  There are also nine low problems.

## Counts

| Status | Count |
| --- | --- |
| RESOLVED | 42 |
| PARTIAL | 11 |
| UNRESOLVED | 0 |
| ROUTED | 4 |
| DECLINED-OK | 2 |
| **Total findings checked** | **59** |
| New problems | 2 Medium, 9 Low |

The count covers:

- **Rubric:** 1 High, 7 Medium and 4 Low findings, plus 6 actionable mechanical notes.
- **Recovery/access:** F-1 to F-8.
- **Adversarial:** ADV-1 to ADV-17.
- **Consistency:** CON-01 to CON-14, the update-summary link note and the spec follow-up group.

## ID integrity

- **Requirement IDs:** FR-1 to FR-12 are each defined exactly once (prd:72, 82, 92, 110, 147, 161, 184, 202, 246, 270, 284, 301).
- **NFRs:** NFR-1 to NFR-3 are each defined once (prd:321, 329, 344).
- **Success measures:** SM-1 to SM-6 are each defined once (prd:354–358, 365).
- **Counter-metrics:** SM-C1 to SM-C5 are each defined once (prd:369–373).
- **No duplicates:** the glossary term "SM-4 admission test identity" (prd:441) is a term, not a second SM-4 definition.
- **References:** every FR, NFR, SM, SM-C and G1–G3 reference resolves.
  - New cross-references resolve: "FR-10's shared-infrastructure rules" (prd:358), "FR-6's single-attempt rule and FR-10" (prd:239), "Shared-infrastructure changes follow FR-10" (prd:176), and "FR-9's admission-revocation, Memories-erasure and reconciliation protections" (prd:233 → prd:259, 261, 262).
  - Every addendum AD-n reference exists as a spine heading.
- **Links:** every local link and anchor resolves except `update-2026-09-28/update-summary.md` (prd:16). That file is expected to be created at finalize close.

## Rubric review

| ID | Sev | Status | Evidence (current lines) |
| --- | --- | --- | --- |
| R-H1 No forward-fix path after an incident on a verified baseline | High | RESOLVED | (a) is moot: the user widened the definition instead of adding an interim no-fix rule. (b) Degraded is defined against the baseline and the last outcome (prd:229, glossary prd:427). (c) The revisit is now "FR-8 items before G2" (prd:395), as the user chose. (d) prd:377 reads "settled except where a row below routes a decision". Failed health checks and incidents now "make production degraded" (prd:192, prd:240). Residual edge cases are N-1 and N-2. |
| R-M1 Deputy cannot contain a production user; unstated | Medium | RESOLVED | prd:225: "While Administrator is unavailable, nobody can revoke production admission; the deputy contains incidents through the stop and documented recovery". The same limit is at add:230. The lost-window review must record "how each known loss is handled" and gates clearing, not reopening (prd:262). |
| R-M2 Probe-failure stop bound has no value or owner | Medium | ROUTED | Row prd:391: Platform with Administrator declares the bound consistently with FR-7, including probe failures during a locked attempt, before G1. |
| R-M3 Non-release production changes lack gate scope and failure path | Medium | RESOLVED | prd:239 lists what counts as an approved attempt and exempts rotations, admission records, environment-layer and shared-infrastructure changes. The urgent patch is in the PRD (prd:280). The shared-infrastructure failure path is prd:281. The actor is unclear (N-4). |
| R-L1 "If safe automatic rollback cannot be demonstrated" is ambiguous | Low | RESOLVED | prd:232 keys on compatibility evidence being "missing, stale, wrong-baseline, failing or breaking". The phrase is gone from FR-6 (prd:175). |
| R-L2 Attachment hold has no local bound | Low | RESOLVED | Finite hold limit, withdrawn at the limit (prd:139). The value is routed (prd:386). |
| R-M4 Reduced-recovery state is silent on the RTO | Medium | RESOLVED | prd:264, prd:340, glossary prd:428. |
| R-L3 Pending run-3 decisions not flagged where used | Low | PARTIAL | prd:377 now says rules in the adoption row govern until adopted, and row prd:396 still lists the run-3-contingent items. There is still no inline marker, and no single statement under Release scope, in FR-8, FR-9 or the G1 text (prd:56, 217–233, 259–260). |
| R-M5 FR-8 is an omnibus requirement | Medium | RESOLVED | The minimum fix was taken: bold subgroups Automatic recovery, Promotion stop, Administrator-approved attempts, and Baseline and incidents (prd:208, 215, 227, 236). The intro names the scope (prd:204). IDs are unchanged. FR-8 is now about 970 words (see N-11). |
| R-M6 Synthetic identity roles blurred | Medium | RESOLVED | "Standing synthetic check identities" (prd:52, 190) and "SM-4 admission test identity" in the human group (prd:56, 357). Glossary prd:440–441. |
| R-M7 PRD restates architecture mechanism | Medium | PARTIAL | The degraded bullet is now an invariant: "staging proves the candidate installs and works without relying on the baseline" (prd:230). Mechanism remains in two places: the incompatible-release step order (set stop, close ingress, remove, restore; prd:233) and the FR-9 sequence (fence, quarantine, rotate, reconcile; prd:258). Neither was moved to the addendum. |
| R-L4 "Earlier releases" in the Vision | Low | RESOLVED | prd:26: "releases before automatic promotion is qualified". |
| R-MN1 Broken update-summary link | Low | DECLINED-OK | prd:16. The file is still absent and is created at finalize close. Confirm at close. |
| R-MN2 Glossary/addendum promotion-stop drift | Low | RESOLVED | Glossary prd:426 and add:212 list every recovery entry, probe failure and admission mismatch, and "naming a verified current working release". |
| R-MN3 "Recovery owner" used for deputy actions | Low | RESOLVED | prd:212 reports to "Administrator and the deputy"; FR-9 says "Administrator or the deputy" (prd:244, 248). Pre-existing, not new: prd:253 uses "recovery owner" for per-dependency owners, which collides with glossary prd:430. |
| R-MN4 No glossary entry for production admission | Low | RESOLVED | Glossary prd:439. prd:291 and prd:294 still use "declaration" and "admission", now tied together by the glossary. |
| R-MN5 Recovery set / recovery point synonyms | Low | RESOLVED | Glossary prd:432: "also called a recovery set". |
| R-MN6 Addendum "SM-5 fault rehearsals before G2" versus the PRD | Low | PARTIAL | The PRD now names the rehearsals but drops the G2 limit: "Approval serves a release before G3, including SM-5 fault rehearsals" (prd:48). add:177 and spine:280 say "SM-5 fault rehearsals before G2". Read literally, the PRD allows approved production fault rehearsals after users are admitted. |

## Recovery and access review

| ID | Sev | Status | Evidence (current lines) |
| --- | --- | --- | --- |
| F-1 No route to a fixing release when a verified baseline becomes unhealthy | High | RESOLVED | Same fix as R-H1 (prd:192, 229, 240, 395). The suggested invariant, "from every stop state there is a route", was not added. The residual states without a route are N-1 and N-2 (see the stop-route matrix). |
| F-2 Approved data-restore wording overstates the loss and bypasses protections | Medium | PARTIAL | The main fix is in: "FR-9's admission-revocation, Memories-erasure and reconciliation protections still apply, and NFR-3 access outcomes are re-verified before user ingress reopens" (prd:233). SM-5 proves an admission revoked and an erasure acknowledged during the attempt are preserved (prd:363). **Regression:** the earlier "reported with FR-9's lost-window exceptions" (old:208) was dropped. prd:233 no longer says that the lost post-point state, including module-owned revocations, is recorded before reopening. Only prd:224's generic "lost-window exceptions from recovery (FR-9)" covers review. |
| F-3 Acceptance measures do not test degraded limits, stop triggers or post-DR rules | Medium | PARTIAL | Added: each trigger sets a durable stop that monitoring never clears (prd:361); refusal when production is neither empty nor degraded (prd:362); SM-6 lists every lost-window category and grants to re-apply, and shows the reduced-recovery state with the stop set (prd:365); SM-C1 counts ineligible approvals and unauthorized clearances (prd:369). Still untested: failure handling of a degraded attempt (application removed, data kept, new stop; prd:231); a continuing condition recorded once and re-recorded after a clear (prd:223); Administrator's lost-window review and admission-match check preceding a clear (prd:224). |
| F-4 Glossary/addendum lag FR-8 triggers and clear rule | Low | RESOLVED | prd:426, add:212. |
| F-5 Deputy reopening not limited to the entry gate | Low | RESOLVED | prd:225: "Reopening restores only the ingress state recorded before the incident; before G2, user ingress stays closed." The addendum does not carry it (add:223, add:230), and the adoption row does not list it (N-5). |
| F-6 Post-cut admission grants silently not reinstated | Low | RESOLVED | prd:259: "Admission reconciliation only removes access … grants recorded after the recovery point's cut are listed for Administrator to re-apply". Also add:280 and SM-6 prd:365. |
| F-7 Whether the deputy may be a named operations writer | Low | RESOLVED | add:230, last sentence. |
| F-8 Reduced-recovery effect on NFR-2 | Low | RESOLVED | prd:264, 340, 428. |

## Adversarial review

| ID | Sev | Status | Evidence (current lines) |
| --- | --- | --- | --- |
| ADV-1 No clear way to ship a fix when a verified baseline stops working | High | RESOLVED | prd:229, 192, 240, 395. The user chose "before G2" over the suggested "before G1". prd:377 makes the PRD rule govern from G1 until adoption. |
| ADV-2 A stop can be cleared while its cause persists | Medium | RESOLVED | The clear record states per cause whether it is "resolved or accepted" (prd:224). A condition still present after a clear "is recorded as a new cause" (prd:223). Adoption is routed (prd:395). The combination creates N-1. |
| ADV-3 Admission-drift trigger looks only for unrecorded changes | Medium | PARTIAL | Mismatch "in either direction" (prd:221, glossary prd:426). A clear requires confirming membership matches the records (prd:224). Routed for adoption (prd:395). The declared check cadence was neither stated nor routed. Without it, a revocation that was recorded but never applied may be detected only at the next clear. |
| ADV-4 "A stop recorded afterward still blocks" is undefined | Medium | RESOLVED | A stop recorded after approval prevents the start; one during the attempt lets it finish without clearing (prd:230). Success becomes the baseline (prd:231). The probe bound, its consistency with FR-7 and probe failures during a locked attempt are routed (prd:391). |
| ADV-5 Incompatible-release data restore may drop FR-9 protections | Medium | RESOLVED | A usable recovery point is required once the attempt controls production (prd:232). Protections, reconciliation and NFR-3 re-verification apply (prd:233). SM-5 covers them (prd:363). The reporting regression is recorded under F-2. |
| ADV-6 "Candidate" undefined; refusal not enforced | Medium | ROUTED | Glossary prd:416 defines "Module candidate". Uncommitted local changes are refused (prd:121). Still routed to Platform with Builds before CI evidence is accepted (prd:386): PR head or merge result, the gated decision, and reuse of evidence for unchanged modules. |
| ADV-7 CI evidence can still exercise an older build | Medium | PARTIAL | Content identity, extension packages, loaded-identity reporting (prd:121) and artifact identity in attachment compatibility (prd:131) are in. **Remaining loophole:** the rule covers every candidate-built artifact "that the environment runs". A composed host that loads the *published* extension instead of the candidate's is not caught: that extension is not candidate-built, so it need not be identified, and nothing refuses it. add:34 omits the qualifier, so the two documents differ slightly. Fix: require the environment to run the candidate-built version of every artifact the module contributes, with the loaded identities matching. |
| ADV-8 Owner and attached outcomes differ | Medium | RESOLVED | Any failed local run retains the environment (prd:138). The withdrawn outcome is defined (prd:141, glossary prd:417). Wording tension is N-7. |
| ADV-9 Deferred cleanup unbounded; CI contradicts addendum | Medium | RESOLVED | Hold limit (prd:139). No new attachment after the owner finishes unless the environment is retained (prd:131). CI attachment is same-job only, and job cancel ends attached runs (prd:142, consistent with add:54). Pending-cleanup listing (prd:136). SM-C4 aligned (prd:372). |
| ADV-10 Reduced-recovery state has no consequences | Medium | RESOLVED | User access may continue, the RTO is not committed, and incidents record the state. The stop holds until Administrator re-establishes staging. No emergency path, "including for application security fixes" (prd:264). "During or after disaster recovery is neither" (prd:229) covers a failed DR. Routed (prd:392): automatic promotion before the return to G2, drill cadence, and re-establishing staging. The wording overshoots: see N-2. |
| ADV-11 Shared-infrastructure change control lacks a recovery path; currency check undefined | Medium | RESOLVED | Moved to FR-10 (prd:280–282). Forward revert or DR entry (prd:281). Urgent patch and rehearsal (prd:280). Currency check defined (prd:282, glossary prd:442). Inventory, cadence, lag and effect on approved attempts routed (prd:391). The actor is unclear: N-4. |
| ADV-12 Identity-provider capture lag unbounded and unmonitored | Medium | PARTIAL | "That capture lag has a declared, monitored bound; exceeding it notifies Administrator and the deputy" (prd:260). Bound routed before G1 (prd:391). The review gates promotion, not reopening (prd:262). Not carried: the spine's condition that a usable recovery point has "event-export lag within its bound" (spine:291) is absent from the FR-9 usable-point definition (prd:255). |
| ADV-13 Tenant-lifecycle flow declarer and cleanup | Low | ROUTED | Row prd:383: Tenants owner with module owners and Platform, before release-gate qualification and the first production verification. |
| ADV-14 G3 suspension trigger unclear | Low | PARTIAL | prd:54 lists FR-7/FR-8 thresholds and budgets, recovery procedures and rollback-set rules, and names Platform and Administrator as deciders. It still does not say whether module smoke or readiness declaration changes count, which was the scenario raised. |
| ADV-15 SM-4 grant could be proved through the synthetic group | Low | RESOLVED | prd:56 and prd:357 require the human production-admission path; add:190 matches. |
| ADV-16 Failed degraded attempt leaves nothing running | Low | RESOLVED | "Production then serves no application until a later attempt or recovery succeeds, a risk the approval accepts" (prd:231). The staging mechanism is left to architecture (prd:230). |
| ADV-17 Dense sentences and editorial issues | Low | PARTIAL | Split: stop triggers (prd:217–222), degraded bullets (prd:229–231), incompatible bullets (prd:232–233), FR-4 lifecycle (prd:136–141), shared infrastructure moved to FR-10, FR-9 revocations (prd:259–262) and the SM-5 scenario list (prd:358–364). SM-3 now covers baseline-only labelling (prd:356). Glossary adds candidate and currency check. Not done: the prd:396 revisit run-on is unchanged; "manual recovery" has no glossary entry; the update-summary link is pending (DECLINED-OK above). |

## Consistency review

| ID | Sev | Status | Evidence (current lines) |
| --- | --- | --- | --- |
| CON-01 Stop triggers and clear rule stated three ways | Medium | RESOLVED | prd:217–224, glossary prd:426, add:212. |
| CON-02 EventStore confirmations gate timing | Medium | RESOLVED | add:351 splits the boundary; the confirmations "additionally gate G3". |
| CON-03 Release tiers misstated | Low | RESOLVED | add:169. |
| CON-04 Off-site executor role overstated | Low | RESOLVED | add:330. |
| CON-05 Drill triggers omit recovery mechanisms | Low | RESOLVED | add:258. |
| CON-06 Addendum G2 row incomplete | Low | RESOLVED | add:187. |
| CON-07 Shared-infrastructure recovery point reads as urgent-patch-only | Low | RESOLVED | add:182. |
| CON-08 "No change" retention case narrower than AD-3 | Low | RESOLVED | add:210: "no consumed Dapr resource changed". |
| CON-09 Labelling of PRD-only FR-4 requirements | Low | PARTIAL | prd:385 ("once adopted") and prd:395 ("attached-run listing") are fixed. add:343 still has no "after AD-4/AD-5/AD-10 adopt the FR-4 additions" qualifier. |
| CON-10 SM-4 grant group and naming | Low | RESOLVED | prd:56, 357; add:186, 190. |
| CON-11 Terminology drift | Low | RESOLVED | (a) "no working baseline" at prd:229, 427 and add:180; (b) glossary prd:439; (c) add:328. |
| CON-12 Superseded McpCli/AD-4 statements unmarked | Low | RESOLVED | add:20, add:22, add:34. |
| CON-13 Architecture gate citation and status | Low | RESOLVED | add:10 cites both gate summaries and the `draft` status; prd:377 names both reviews. |
| CON-14 Addendum carries only half the degraded question | Low | RESOLVED | add:180 carries both the definition widening and the compatibility-evidence question. |
| CON-link Update-summary link | n/a | DECLINED-OK | Created at close (prd:16). |
| CON-spec Spec follow-up (7 groups) | n/a | ROUTED | Row prd:396, spec owners with Administrator, before recovery and deployment stories are finalized. |

## Promotion-stop route matrix

This table checks, for each state that sets or holds the stop, whether a documented route leads to a verified working release.

| Stop state | Route | Result |
| --- | --- | --- |
| Failed compatible deployment, automatic recovery verified | Administrator clear naming the restored release (prd:224) | OK |
| Failed or unverified automatic recovery | Last outcome non-working, so degraded, then an approved attempt (prd:229–231); or manual recovery verified as baseline (prd:238) | OK |
| Failed pre-update health check | Degraded (prd:192, 229), then an approved attempt | OK (was the gate High) |
| Incident or probe failure where the baseline fails its own checks | Degraded (prd:229, 240), then an approved attempt | OK (was the gate High) |
| Incident or probe failure where the baseline still passes its readiness and smoke checks | Not degraded under prd:229. A clear re-records the persisting cause (prd:223). | **No route: N-1** |
| Admission mismatch | Administrator reconciles membership, then clears (prd:224) | OK |
| Administrator or deputy declaration | Administrator clear | OK |
| Failed approved degraded attempt | Last outcome non-working, so degraded, then another approved attempt; nothing serves meanwhile (prd:231) | OK |
| Failed approved incompatible release | Approved recovery; verified becomes baseline (prd:233, 238); failed means degraded | OK |
| Failed first deployment | Still empty, so an approved attempt (prd:234) | OK |
| Failed shared-infrastructure verification | Forward revert or DR entry, then clear (prd:281) | OK; actor unclear (N-4) |
| DR in progress, failed or unverified | DR retry only: "during … disaster recovery is neither" (prd:229) | **No route if the restored release itself cannot verify: N-2** |
| Reduced-recovery state | Stop held until staging is re-established, then a standard clear. An incident on the restored release waits for the state to end (prd:264). | User-accepted |
| Production after the reduced-recovery state ends | "After disaster recovery is neither" (prd:229, 427) literally excludes the degraded path for good | **Wording gap: N-2** |

## New problems introduced by the fixes

### N-1 — Medium — Persisting conditions the baseline's checks miss have no route, and "accepted" cannot hold

**Location:** prd:220, prd:223, prd:224, prd:229, prd:240, glossary prd:427, add:212.

**Problem.** Degraded is defined in two ways:

- **prd:229 and glossary prd:427.** The incident or probe failure must establish that "the working baseline no longer passes its readiness and smoke checks".
- **prd:220 and prd:240.** An incident "establishing that production is no longer working" makes production degraded.

Now take a release-owned fault that only the off-site probe or users see, for example broken public routing, while the internal readiness and smoke checks still pass. Under prd:229 production is not degraded, so no approved attempt is available.

The standard clear also fails. Administrator may mark the cause "accepted" (prd:224). But because the condition persists, prd:223 records it again as a new cause straight after the clear. Promotion therefore stays blocked with no route to a fixing release. The same loop blocks promotion indefinitely for an external-only probe failure that Administrator judges harmless. "Accepted" is meaningful only for one-shot causes.

**Fix.** Choose one of the two definitions: either Administrator's incident record may establish that production is degraded, or align prd:240 with prd:229. Also let an accepted continuing condition stay accepted until it changes, rather than being re-recorded. The adversarial fix scoped re-recording to monitor-raised conditions; the PRD dropped that qualifier. Add both to row prd:395.

### N-2 — Medium — "During or after disaster recovery is neither" goes beyond the user decision and the spine

**Location:** prd:229, glossary prd:427; compare prd:192, prd:240, prd:264, add:180, add:198, spine:296.

**Problem.** The user excluded only the post-DR reduced-recovery state. The spine's reduced-recovery posture "is not Empty or degraded production" (spine:296), and add:180 says the same. The PRD wording goes further in two ways:

- **"After".** Read literally, it disables the degraded path permanently after any disaster recovery, even once the reduced-recovery state has ended.
- **"During".** It excludes a failed or unverified DR from the degraded path. Retrying DR is then the only route, so a restored release that itself cannot verify has no fixing-release route. Neither the user nor the spine decided this.

**Conflicts with other text.** prd:192 and prd:240 say a failed health check or incident "makes production degraded" without that exception. add:198 says an unhealthy baseline "can proceed only through one Administrator-approved attempt".

**Fix.** Write "Production in the reduced-recovery state (FR-9) is neither empty nor degraded". Route the failed-DR case to row prd:392 or prd:395 if it needs a decision.

### N-3 — Low — The approved-attempt clearing route skips the pre-clear checks

**Location:** prd:224, prd:231, prd:262.

**Problem.** Before an Administrator clear, prd:224 requires three things: the lost-window review, confirmation that production admission matches the records, and a per-cause resolved-or-accepted statement. The "only other route", a verified approved attempt (prd:231), clears the observed stop without any of them.

For example, after a failed incompatible restore followed by a failed recovery, a degraded attempt could resume promotions before Administrator reviews the lost state. This contradicts prd:262, under which the review "gates clearing the promotion stop".

**Fix.** Require the approval record, or the success, to carry the same review, or state that the success clears only causes that need no review.

### N-4 — Low — Undefined actor for shared-infrastructure recovery

**Location:** prd:281; compare prd:244, prd:248, spine:245.

**Problem.** "The named change owner recovers by a forward revert or a disaster recovery entry." The "named change owner" is not defined in the PRD, and the spine names no actor for recovery. A DR entry is Administrator or deputy authority under FR-9. It is also unclear whether a failed shared-infrastructure outcome counts as production's "last outcome was non-working". If it does, the application approval path opens for an infrastructure fault.

**Fix.** Name Administrator, or the deputy for a DR entry. State whether shared-infrastructure outcomes make production degraded.

### N-5 — Low — The adoption row does not list every PRD-only rule, and its timing is looser than the stop-clearance row

**Location:** prd:377, prd:390, prd:395; rules at prd:121, 131, 142, 225, 259.

**Problem: missing rules.** prd:377 says the rules awaiting adoption are those "marked in the adoption row", so an unlisted rule reads as already carried by the architecture. Several new rules have no spine counterpart but are missing from row 395:

- attachment refused after the owner has finished;
- artifact identity as an attachment-compatibility check;
- CI attachment limited to the same job (prd:131, prd:142). add:54 already labels these as awaiting AD-10;
- the environment reporting the identities it actually loaded, and refusal of uncommitted evidence (prd:121);
- the deputy reopening limit (prd:225; spine:322 has no such limit, and the addendum does not carry it at add:223 or add:230);
- the listing of post-cut grants for Administrator (prd:259).

**Problem: timing.** Row prd:390 qualifies "promotion-stop clearance" before the first applicable production attempt, which is at G1. Row prd:395 lets the PRD's per-cause, two-way-mismatch and re-recording clearance rules wait until G2. Clearance used between G1 and G2 may therefore lack rules the PRD says govern.

### N-6 — Low — Addendum attachment table applies failure retention to CI

**Location:** add:105; contradicts prd:138, prd:142, add:106 and the user decision that "CI still always cleans up".

**Problem.** The row "Test explicitly attaches to a compatible local or CI environment" says "if it fails, the environment is retained".

**Fix.** Limit that clause to local environments.

### N-7 — Low — First-terminal-outcome wording conflicts with attached-failure retention, and the explicit stop was narrowed

**Location:** prd:136, prd:138, prd:140; add:54, add:105.

**Problem.** Two wording issues:

- **Outcome rule.** "The run's first terminal outcome determines cleanup or retention" (prd:136, and the same at add:54) conflicts with a later attached failure retaining an environment whose owner already succeeded (prd:138).
- **Explicit stop.** prd:140 now allows an explicit developer stop only for a "retained environment". add:54 and add:105 apply it generally. A pending-cleanup environment with a hung attached run is therefore bounded only by the hold limit.

**Fix.** Write "any served local run's failure retains the environment; otherwise the owner's outcome decides". Allow an explicit stop of pending-cleanup environments.

### N-8 — Low — Addendum sentence can be read as letting the deputy restore post-cut grants

**Location:** add:223; compare prd:259, add:280, add:328.

**Problem.** "Restoring already authorized access is part of recovery" can be read as the deputy re-adding grants recorded after the cut. prd:259 and add:280 reserve those grants for Administrator to re-apply.

**Fix.** Write "access present at the recovery cut, less reconciled removals".

### N-9 — Low — SM-5 claims to cover FR-10 shared-infrastructure rules, but no scenario does

**Location:** prd:358–364.

**Problem.** SM-5 now "validate[s] … FR-10's shared-infrastructure rules". None of the listed scenarios exercises any of the following:

- a failed shared-infrastructure verification and its forward revert or DR entry;
- the urgent in-place patch path;
- the currency-check block.

**Fix.** Add a scenario, or drop the claim.

### N-10 — Low — The widened degraded definition makes the automatic-recovery trigger ambiguous

**Location:** prd:204, prd:231, prd:395.

**Problem.** Degraded now includes a verified baseline that later breaks. A failed approved degraded attempt with valid compatibility evidence therefore literally matches FR-8's automatic-recovery trigger: "a failed compatible production deployment over a recorded working baseline". That conflicts with prd:231, which removes the application, keeps data and serves nothing. Row prd:395's open question on compatibility evidence and named recovery is related but does not decide which rule applies.

**Fix.** State that prd:231 governs approved degraded attempts, or add this to the row prd:395 decision.

### N-11 — Low — Density

**Location and problems:**

- **Row prd:395 (89 words).** It mixes FR-4 adoption, FR-8 adoption and a separate open decision in one cell. Split it into three rows.
- **add:180 (179 words) and add:212 (172 words).** Each is a single bullet that combines the selected rule, PRD-only additions awaiting adoption and exceptions.
- **Glossary prd:427.** It nests "or whose … or whose".
- **Growth.** FR-8 grew from about 710 to about 970 words, and the PRD from about 8,500 to about 9,600 words (+12.6%).

The FR-8 subgroups keep the text actionable, but the downstream row and the two addendum bullets are now hard to act on.

## Other checks

- **Authority.** No path lets the deputy approve releases, clear the stop or administer admission:
  - prd:34, prd:225 and prd:294, and glossary prd:431, all say this, and so do add:223, add:230 and add:328.
  - The deputy runs an approved recovery only after Administrator approval (prd:233).
  - Recovery never grants admission (prd:259).
  - The only authority wording that is loose is N-4 and N-8.
- **PRD versus addendum.** The addendum correctly labels, as awaiting adoption: the degraded widening (add:180), the admission mismatch in both directions, per-cause clearing and re-recording (add:212), and the FR-4 CI and attachment rules (add:34, add:54). The remaining divergences are:
  - N-2: add:180 excludes only the reduced-recovery state;
  - N-6: add:105 applies failure retention to CI;
  - N-7: add:54 and add:105 differ on the explicit stop;
  - ADV-7: add:34 has no "that the environment runs" qualifier;
  - R-MN6: add:177 limits fault rehearsals to before G2;
  - CON-09: add:343.
- **Rules stronger than the routing admits.** The degraded widening, per-cause clearing and admission mismatch in both directions are PRD rules. prd:377 and row prd:395 explicitly say they govern until adopted, so this is not overclaiming. The only unflagged cases are the unlisted rules in N-5.
