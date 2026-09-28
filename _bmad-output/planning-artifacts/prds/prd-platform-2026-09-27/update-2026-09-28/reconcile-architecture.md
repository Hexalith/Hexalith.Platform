# Reconcile: architecture against the updated PRD and addendum (2026-09-28)

Finalize-step check. Only this report was written; the PRD, addendum and architecture were not edited.

- **Updated documents:** `prds/prd-platform-2026-09-27/prd.md` (396 lines) and `.../addendum.md` (354 lines), both `status: draft`, `updated: 2026-09-28`.
- **Architecture:** `architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md` (487 lines, `status: draft`). Decision log `.memlog.md` L209–L261.
- **Mapping inputs:** `update-2026-09-28/extract-recovery-access.md` (S1–S3) and `update-2026-09-28/extract-ci-attachment-drift.md` (D-1..D-13, section 3.3).
- **User scope (PRD memlog, last 11 entries):** sync with the current architecture, including the draft run-3 decisions VAL-04, VAL-08 and G-4. Adopt S1 and S2. Put S3 and D-8..D-13 in the addendum, and D-1..D-7 in the PRD at capability level.
- **Snapshot caveat:** the run-3 confirmation review is still pending. `reviews/update-2026-09-28-r3/` has no `review-confirm.md`, and the arch log ends at L261 with "Dispatching confirmation review". Every item below that depends on VAL-04, VAL-08, G-4 or the L260 autofixes must be re-checked if that review changes them.

Line references are 1-based: `prd:N`, `add:N` and `spine:N`. Arch-log lines are `log:N`.

## Summary

| Status | Count | Items |
| --- | --- | --- |
| CARRIED | 14 | S1, S2, S3, D-1, D-2, D-3, D-4, D-5, D-6, D-8, D-9, D-10, D-12, D-13 |
| PARTIAL | 2 | D-7 (PRD), D-11 (addendum) |
| MISSING | 0 | — |
| MISSTATED | 0 | — |

The three high-risk points named in the brief are stated correctly:

- **Lost-window review timing.** The exceptions are recorded before service reopens, and Administrator reviews them before clearing the promotion stop, not before reopening (prd:233, add:279; spine:295, :322).
- **Scope of the empty/degraded approval.** It lifts only the observed stop and the healthy-baseline check, for one attempt, and a later stop still blocks (prd:209, add:180; spine:281, :288).
- **Post-DR posture.** The reduced-recovery posture is not degraded production, and no emergency release mode exists (prd:209, :236, :385; add:180; spine:296).

The remaining findings are a medium routing gap for A1/A2, one medium ambiguity leak on the degraded path, and several low-severity wording, timing and strength deviations.

---

## 1. Per-item reconciliation

### S1–S3

| ID | Home | Status | Where carried | Spine basis | Notes |
| --- | --- | --- | --- | --- | --- |
| S1 Restoration revocation exceptions | PRD | CARRIED | prd:232 (reconcile admission to recorded grants/revocations; re-apply captured revocations; denial of every revoked-admission principal; recovery never grants admission); prd:233 (admission revocations always survive; two exceptions with **separate** windows; containment via admission revocation; recorded before reopening; Administrator reviews before clearing stop); prd:210 (in-place restore reports lost window); prd:296, :304, :311 (NFR-1/NFR-2 qualifiers); prd:317 (NFR-3 denial after restore incl. revocation just before failure); prd:328 (SM-6); add:257, :277, :279, :352 | spine:118–119, :295, :320–:322, :478, :487 | The two windows are kept separate: module-owned revocations use the recovery cut, and identity-provider removals use "not yet captured outside the failed environment". They are not merged into the RPO window. The rejected alternatives (per-module journal, fail-closed admission after every DR, record-first for all realm changes) are not presented as chosen. Rollback preservation (prd:292) and live authorization (prd:311) are kept. Deviations in §1.2 items W-1 and W-8. |
| S2 Approved empty/degraded release | PRD | CARRIED | prd:26, :48 (mode uses); prd:171 (never substitutes for staging gate, provenance, isolation, verification); prd:188 (healthy-baseline exception routed to FR-8); prd:207–:209 (stop, clear rule, later stop prevails, empty/degraded path, not post-DR emergency); prd:212 (first deployment uses it); prd:213 (other manual change = approved attempt); prd:327 (SM-5 scenario incl. deputy cannot approve); prd:383–:384 (glossary); prd:352 (ambiguity routed to architecture); add:177, :180, :197, :211 | spine:36, :226, :280–:281, :288, :296, :302, :309 | Actor, scope and timing are correct: only Administrator approves, for one identified attempt naming the reason and the observed stop state. A verified success clears only the observed stop and becomes the baseline, and a failure removes the candidate's workloads, keeps data and sets a new stop. The deputy can do neither. See §2 item C2 for an ambiguity leak at add:197 and prd:188. |
| S3 Operations writers and deputy controls | Addendum | CARRIED | add:229 (deputy notifications, in-place/replacement-capacity recovery under own MFA identity, no ops-repo write or GitHub dependency, recovery-scoped IdP rights, may declare but not clear the stop, G2 proof, coverage counting after proof); add:329–:330 (two named writers, base permission read/none, accepted risk, signed records, trigger beyond named writers, deputy needs no write); add:346 (G2 deputy proof, "Amend architecture controls" dropped); add:352 (accepted risk); prd:34, prd:355 (architecture-alignment row removed; spec sync kept) | spine:36, :130–:131, :236, :292, :458, :478, :480–:481, :487 | The second owner's GitHub handle is correctly omitted. One timing deviation is noted in §1.2 item W-4. |

### D-1..D-7 (PRD, capability level)

| ID | Status | Where carried | Spine basis | Notes |
| --- | --- | --- | --- | --- |
| D-1 Approved incompatible release | CARRIED | prd:48, :171 (recovery, acceptance checks and **maximum duration** named before attempt); prd:210 (complete recovery point before start; non-working → stop, close ingress, remove candidate workloads keeping data, stop; Administrator or deputy runs data restore to that point; post-cut state lost and reported); prd:296 (explicitly not application rollback, resolving the NFR-1 tension); prd:327 (SM-5); add:179 | spine:280, :282, :284 (VAL-10), log:222 (C-38), :237 (D5), :246, :255 | No in-job automated restore is implied ("and stops"). Strength note in §1.2 item S-1. |
| D-2 Post-DR reduced-recovery posture | CARRIED | prd:236; prd:209 (not an emergency mode); prd:213 (DR becomes baseline only after verification); prd:385 (glossary) | spine:296, log:220 (C-50) | Uses the spine's renamed term ("reduced-recovery", not C-50's "degraded posture"), which avoids a clash with "degraded production". |
| D-3 SM-4 grant expiry and G2 proof; admission drift sets stop | CARRIED | prd:53 (G2 requires revocation with recorded denial check); prd:56 (expires no later than G2, revoked, denial check); prd:207 (admission change without matching Administrator record sets stop); prd:326 (SM-4); add:186, :189, :211 | spine:236, :289, log:258 (G-4) | The PRD does not say the SM-4 identity is limited to the synthetic tenant; add:189 does. See §1.2 item W-6. |
| D-4 G1 human admission group | CARRIED | prd:52 ("human production-admission group is empty"; synthetic identities through separate Administrator-granted access limited to synthetic data); add:185 | spine:117, :235, :289, log:216 (C-10) | — |
| D-5 Expanded stop triggers and clear semantics | CARRIED | prd:188 (failed health check sets stop); prd:207 (all triggers incl. probe bound, incident, unmatched admission change, Administrator or deputy declaration; continuing condition recorded once; monitor sets never clears; no release search); prd:208 (observed-state clear; later stop prevails); prd:383; add:211 | spine:288, log:222 (C-34), :249 (VAL-04) | See §1.2 item W-2 on "disaster recovery entry" versus "every recovery entry". |
| D-6 Tenant-lifecycle limits | CARRIED | prd:166 (staging E2E creates or deletes only run-scoped, synthetic-marked tenants within a module-declared tenant-lifecycle flow; excluded from real-tenant views); prd:186 (production smoke never creates or deletes tenants) | spine:235, log:238 (D8), :251 (VAL-05) | The local/CI exemption follows from the staging-only scope. The staging-only admission clause is correctly left out of the PRD as a mechanism. |
| D-7 Shared-infrastructure change control | **PARTIAL** | prd:170 (failed currency check blocks automatic path); prd:172 (coordinate both environments, after complete recovery point, re-run working-release smokes and NFR-3 checks, failed verification is non-working); prd:317 (NFR-3 repeat after shared changes); add:177; add:352 (K8s-minor two-environment outage risk) | spine:245, :487, log:224 (C-17) | **Missing in both documents:** (a) from G1, a pending shared change is first rehearsed on a production-profile copy on prepared capacity as part of the monthly drill; (b) an urgent security patch may be applied in place with a recovery point and an **Administrator record**. Item (b) is an Administrator-authority exception. Both are process/authority detail and fit the addendum ("Release modes and production entry" or "Remaining qualification"). This matches the user's D-7 summary ("currency and re-verification"), but the extract's D-7 scope was wider. |

### D-8..D-13 (addendum)

| ID | Status | Where carried | Spine basis | Notes |
| --- | --- | --- | --- | --- |
| D-8 G1 prerequisites | CARRIED | add:185 (IdP admin routes and public cluster console off public ingress, external negative probe); add:347 (ingress controller, CNI, registry, backup tooling, K8s patch; privileged CI runner off the cluster node, earlier if staging holds real data) | spine:475–:477, log:228 (C-23, C-27), :229 (C-54) | add:347 omits C-27's "not shared with any executor". AD-7 (spine:127) covers it generally, so this is only a nit. |
| D-9 Staging supersession and reset | CARRIED | add:181 | spine:279, log:221 (C-37) | Wording: "is no longer adopted" should read "is no longer eligible for adoption" (spine: "is not adopted … without an Administrator record reserving it"). A candidate that was never adopted cannot stop being adopted. |
| D-10 G3 suspension on policy change | CARRIED | prd:54; add:187 | spine:289 | Carried in the PRD as well, which is harmless. |
| D-11 Legacy MCP/CLI exclusion | **PARTIAL** | add:22 | spine:175–:176, log:212 (C-03) | (a) add:22 allows temporary compatibility use "outside hosted environments". Spine:175 says "only outside every Platform composition", which also excludes local and CI Platform compositions, so the addendum is **weaker** and inconsistent with its own preceding sentence. log:212 uses the addendum's wording, so the spine distillation is stricter than the logged decision. Align with the spine, or ask the architecture owner which is intended. (b) It omits "mapped in an enrolled host" and the validator rejecting an enrolled host that maps an MCP endpoint. That is mechanism detail and optional. |
| D-12 Interim unavailability of confirmation-required chains | CARRIED | add:26 | spine:176, AD-14 (spine:197–:207), log:215 (C-06) | See §2 item C4 on prd:272. |
| D-13 New accepted risks | CARRIED | add:352 (manual unseal, K8s-minor two-environment outage, plaintext connections, shared Keycloak/Dapr control plane/ingress) | spine:487, :230 | Overbroad wording; see §1.2 item W-5. |

### 1.2 Strength deviations (PRD or addendum stronger or weaker than the spine)

| # | Direction | Location | Architecture | Severity | Suggested fix |
| --- | --- | --- | --- | --- | --- |
| W-1 | **Weaker** | prd:233–:234 | spine:295 applies "recorded in the DR report before reopening" and "Administrator reviews them before clearing the promotion stop" to **all** lost-window exceptions, including non-Memories erasures, deletions and legal holds. | Low–medium | prd:233 limits Administrator review to "both" revocation exceptions. prd:234 only says to "report that window in the recovery report", with no before-reopening timing and no stop-clearance review. Extend prd:234 to match. add:279 already says "each window before reopening … reviews the exceptions". |
| W-2 | Weaker (wording) | prd:207 | spine:288: "every recovery entry" sets the stop, and "Promotion stays suspended during any recovery". | Low | prd:207 says "disaster recovery entry". In practice this is covered, because in-place recovery follows a non-working outcome and spine:282 keeps the stop set, but the PRD never says that manual or in-place recovery keeps the stop set. Use "every recovery entry". This wording predates this update. |
| S-1 | Stronger | prd:210 ("restores data … within the approved maximum duration"); prd:327 | spine:284: the approved recovery's duration is a recorded maximum lifetime that "notifies and never aborts". | Low | The PRD reads as a hard completion bound. Optionally add "an overrun is reported". SM-5's "within the approved duration" is fine as a rehearsal target. |
| W-4 | Weaker (timing) | add:347 (base permission read/none "Before G1") | spine:458: set at **First publication**, which comes before the first staging deployment and G1. | Low | Move the base-permission item to a "Before first publication" boundary, or cite spine:458. Live state is still `write` (log:233). add:330 states it as the decision, not the current state, which is correct. |
| W-5 | Weaker (overbroad risk) | add:352 "plaintext data-service connections inside the data namespace" | spine:487 accepts only "plaintext Redis and FalkorDB connections". spine:230 requires TLS where the provider supports it natively, including PostgreSQL. | Low | Say "plaintext Redis and FalkorDB connections". |
| W-6 | Weaker (PRD only) | prd:56 | spine:289: the SM-4 identity "holds production permissions limited to the synthetic tenant". | Low | Carried at add:189. Optionally add "limited to the synthetic tenant" at prd:56. |
| W-7 | Weaker (pre-existing) | prd:235, prd:328 (drill triggers) | spine:293 also repeats the drill after "recovery-mechanism changes". | Low | prd:354 covers Memories "whenever recovery mechanisms change", but FR-9 and SM-6 do not. Optional. |
| W-8 | Ambiguous | add:257 "including a revocation made before event export" | spine:478: "a revocation lost before event export", meaning made but not yet exported at the failure. | Low | Say "a revocation not yet exported when the failure occurred". prd:317 and prd:328 already say "just before the failure". |

---

## 2. Other PRD/addendum statements against the current spine (PRD-level matters)

| # | Location | Issue | Severity | Suggested handling |
| --- | --- | --- | --- | --- |
| C1 | prd:340, prd:347; add:34, add:54 | **A1 and A2 are PRD decisions the architecture does not yet carry, but they are presented as accepted-architecture context.** prd:340 says "The accepted architecture defines the mechanisms summarized in the addendum". prd:347 says "Implement and qualify the **selected** … attachment lifetime, CI candidate-artifact identity", owned by Platform with Builds, with no architecture owner. add:34 puts the CI candidate-evidence rule inside the AD-4 summary, and add:54 puts the attachment-holds-environment rule inside the AD-10 summary. The spine has no such rules: spine:163 says "A run's first terminal outcome alone decides retention or cleanup … CI always cleans" with no deferral for attached runs, and AD-4/AD-5 (spine:100, :108) have no candidate-artifact binding or baseline-only labelling (see extract §1.3 and §2.4). This is not a contradiction, because the PRD may add requirements, but nothing routes these requirements to the architecture. | Medium | Add a downstream row, or extend prd:352 or prd:355, asking the Platform architecture owner to adopt A1 (AD-4/AD-5) and A2 (AD-10 outcome/attach) before the runner-lifecycle work at spine:457. In add:34 and add:54, mark these sentences as PRD requirements pending architecture adoption. |
| C2 | add:197; prd:188 | **The degraded-path ambiguity leaks.** add:197 says a pre-existing unhealthy production "stops the update … and sets the promotion stop, unless Administrator approves one attempt under the empty or degraded production rule". A failed pre-update health check on a verified baseline therefore reads as eligible for the degraded path. prd:188 puts "a failed health check also sets the promotion stop" directly before "empty or degraded production follow the Administrator-approved path". prd:352 reserves exactly this question (stops on a verified baseline) for the architecture, with an interim of "standard Administrator record only", but it lists only "incidents or probe failures" and omits failed pre-update health checks. The literal reading of spine:281 ("last outcome was non-working") excludes these cases. The user decision (PRD memlog) keeps the question with the architecture. | Medium–low | Qualify add:197 with "where production has no working release or its last outcome was non-working", and add "failed pre-update health checks" to prd:352's question. Note for the architecture owner: under the interim rule, a non-working production caused by a stop that is not an outcome may have no clear route. spine:282 covers baseline re-deploy only after a failed or unverified recovery. That gap belongs to the architecture, not the PRD. |
| C3 | prd:340 | "its passing handoff review establishes document readiness". The current spine is `status: draft` and its run-3 confirmation review is pending. add:10 and prd:355 say so correctly. | Low | Reword to "the architecture's 2026-09-27 handoff review passed; the 2026-09-28 run-3 revision awaits its confirmation review". |
| C4 | prd:272 | FR-12 says confirmation-required operations "remain available only through that UI". Until EventStore originating-surface attestation lands, spine AD-14 keeps confirmation-required cross-module chains (for example Projects project-folder replace) unavailable. The user placed D-12 in the addendum, where add:26 carries it. | Low | Acceptable as is. Optionally add "where the owning module and architecture make them available". |
| C5 | prd:355 | The revisit condition covers re-checking "admission-record, access-removal or approved-attempt decisions" if the confirmation review changes them. It does not cover the other run-3 items the PRD now carries: the G1 SM-4 grant expiry, the admission-drift stop trigger and the in-place data restore for approved releases. | Low | Broaden to "any run-3 decision carried in this PRD". |

Checked and consistent: roles and authority (spine:36 vs prd:34, :208, :263, :388); thresholds (FR-4 10 min; FR-7 10 min / 5 min / 60 s / 2×30 s / ≤10 s; FR-8 10 min; FR-9 30 min, 7/30 days, 15 min, 1 h, 5 min, hourly dead-man; NFR-2 1 h / 4 h with coverage) against spine:233, :284–:287, :290–:293; G1–G3 (prd:52–:56 vs spine:289, :475–:478); MVP scope and module set; NFR-1 wording against the approved data restore (prd:296); NFR-2 waiver wording against spine:326 (prd:311 keeps "live authorization" and names FR-9's only accepted losses); CI credentials (prd:122, add:52 vs spine:108).

---

## 3. Architecture mechanism leaked into the PRD

**No material leakage.** The PRD contains no stop revision, compare-and-set, epoch, lock, numbered precondition, Keycloak group or realm name, event-export frontier or lag bound, signed or chained record encoding, DR step numbering, "recovery runner" or "DR report", render modes, executor names or GitHub handles. It uses "the stop state Administrator observed", "a stop recorded after that observation", "not yet captured outside the failed environment", "Administrator's recorded grants and revocations" and "the recovery report", as the extracts recommended.

Borderline items, all acceptable at capability level:

- **prd:34, "without operations-repository write access".** This is an authority boundary rather than a mechanism. It could move to add:229, where it is already stated.
- **prd:207, "A continuing condition is recorded once until it resolves".** This is the behavioral rule that prevents a standing condition from re-blocking after a clear.
- **prd:209, "Staging rehearses a fresh install plus the candidate".** A testable staging requirement.
- **prd:210, "keeping data objects".** Slightly technical, but consistent with prd:204.

The mechanism-level material the extracts told the addendum to hold is in the addendum: signed admission records (add:277, :279), the event-export frontier (add:279), the staging recovery-point cut (add:181) and the CI tool-identity checks per D2 (add:34).
