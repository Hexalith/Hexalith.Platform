# Recovery and access review: Hexalith Platform PRD update, 2026-09-28

## Verdict

**Pass with required fixes. Counts: 0 critical, 1 high, 2 medium and 5 low.**

S1, S2 and S3 are resolved in both documents. None is overcorrected: the PRD does not promise more than the accepted architecture, and the approved path waives no common gate. The deputy's authority stays within its accepted limits. The one high finding is a gap left by S2. Suppose a verified baseline becomes unhealthy through an incident, a probe failure or a failed pre-update health check, rather than through a non-working attempt outcome. The PRD's interim rule then leaves no documented way to deploy a fixing release until the architecture decides the question routed to it. The medium findings cover the approved data-restore wording and acceptance measures that do not yet test the new stop and degraded-path rules.

**Scope and method.** This is a document review of the current `prd.md` (397 lines) and `addendum.md` (355 lines), both drafts dated 2026-09-28. They were checked against the earlier recovery/access review (`validate-2026-09-28/review-recovery-access.md`) and PRD log entries 89–101. The architecture sources are the current spine, a 487-line draft whose run-3 confirmation review is still pending, and its log entries 237–261. No source document was edited. No runtime or deployment state was checked, and missing implementation evidence is not treated as a finding (prd:14, prd:338–356).

**References** are 1-based line numbers. `prd` is the PRD, `add` the addendum, `spine` the architecture spine, `prd-log` the PRD `.memlog.md` and `arch-log` the architecture `.memlog.md`.

## Earlier findings: resolution check

| ID | Status | Evidence | Overcorrection check |
| --- | --- | --- | --- |
| S1: Restoration revocation exceptions | **Resolved** | prd:232 reconciles admission to recorded grants and revocations, reapplies captured revocations, denies revoked principals and never grants admission. prd:233 says admission revocations always survive, names two exceptions with separate windows (module-owned revocations after the cut; identity-provider removals not yet captured off-environment) and makes containment an admission revocation. They are recorded before reopening, and Administrator reviews them before clearing the stop. Also carried at prd:234, prd:304, prd:311, prd:317, prd:328; add:258, add:278, add:280, add:353. | Matches spine:118–119, :295, :320–:321, arch-log:250 (VAL-08), :258 (G-4) and :237 (D5 moves review to before stop clearance). The rejected alternatives (per-module journal, fail-closed admission, record-first for every realm change) are not reintroduced. NFR-1 rollback preservation (prd:292) and live authorization (prd:311) are unchanged. |
| S2: Administrator-approved release into degraded production | **Resolved, with a residual gap (F-1)** | prd:26, :48, :171 (the approved path waives no common gate), :188 (routes to FR-8), :208 (the only exception to the clear rule), :209 (one attempt; lifts only the observed stop and the healthy-baseline check; a later stop blocks; fresh-install rehearsal; on failure removes workloads, keeps data and sets a new stop; on success clears the observed stop; no post-DR emergency use), :212 (first deployment), :327 (SM-5), :384–:385; add:180, :198, :212. | Matches spine:280–:281, :288, :296 and arch-log:249 (VAL-04). It is not extended to other stops on a verified baseline: add:198 and prd:352 leave that question with the architecture, as the user decided (prd-log:93). |
| S3: Stale operations-writer and deputy summary | **Resolved** | add:230 (deputy notifications; recovery in place or on replacement capacity under the deputy's own MFA identity, with no ops-repository write or GitHub dependency; may declare but not clear a stop; G2 proof before coverage counts); add:330–:331 (two named writers, base permission read or none, accepted risk, Team trigger beyond the named writers, deputy needs no write); add:347 and add:353. | Matches spine:36, :130–:131, :458, :478, :481, :487. The obsolete "architecture does not yet implement" wording has been removed. |

## Findings

### F-1: High. A verified baseline that becomes unhealthy outside an attempt outcome has no documented route to a fixing release

**Location:** prd:188, prd:208, prd:209, prd:214, prd:352; add:198, add:212; spine:281, :282, :288, :309.

**Issue.** A stop can be cleared in two ways:

1. an Administrator record naming "a verified current working release" (prd:208); or
2. a successful approved empty or degraded attempt, which is available only when production has "no working release, or its last outcome was non-working" (prd:209).

Now suppose a production baseline was verified as working, but later fails readiness or smoke checks. Possible causes include a latent defect, expiring embedded configuration, or state the baseline cannot handle. Each is detected by a recorded incident (prd:214), an availability-probe failure, or a failed pre-update health check (prd:188).

- The last attempt outcome is still *working*, so the degraded path does not apply.
- prd:352 says that, pending an architecture decision, these stops "clear only through the standard Administrator record".
- That record needs a verified *current* working release, which does not exist.
- Even if Administrator clears the stop naming the old attempt, every release attempt must first confirm the baseline is "healthy now" (prd:188; spine:309 precondition 8). The attempt then fails before mutation and sets a new stop.
- Neither approved mode lifts that precondition outside the degraded path (spine:280).
- The spine offers a baseline re-deploy only after a failed or unverified recovery (spine:282). Re-deploying the same baseline would not fix a latent defect anyway.

The only apparent escape is to contrive a manual attempt that fails verification, which manufactures a non-working outcome. This is the most likely serious production incident after G2, and it has no product-level route back to a working release.

The revisit condition, "before the first approved degraded-production attempt" (prd:352), makes the problem worse. The need for such an attempt appears only when production is already stuck. The first deployment is an approved *empty* attempt, so it is ambiguous whether it even triggers the revisit. The architecture's r3 recovery review concluded that "no stop exists that the role holder cannot clear". That is true of clearing the stop, but not of deploying a fix, because the healthy-baseline precondition still blocks it.

**Suggested fix.** Keep the eligibility decision with the architecture, as the user decided, and make two requirement-level changes:

- Add an invariant to FR-8: from every promotion-stop state there is a documented, Administrator-authorized route to a verified working release, and a baseline that no longer passes its own checks never permanently blocks a verified fixing release.
- Move the prd:352 revisit condition to **before G2** (preferably before the first production deployment), and reword it so the architecture must supply that route. Examples: Administrator records production as non-working after an incident, or manual baseline re-deploy is allowed after any stop that establishes non-working production.

Do not select the mechanism in the PRD.

### F-2: Medium. The approved data-restore wording overstates the loss and bypasses the restoration protections

**Location:** prd:210, prd:296, prd:327; add:179; compare prd:232–234 and spine:282, :314–:321 (the in-place forms of steps 3–6).

**Issue.** After a failed approved incompatible release, prd:210 says "state written after the point is lost and reported with FR-9's lost-window exceptions". Read literally, this includes:

- admission revocations made during the attempt, which prd:233 says "always survive restoration";
- Memories tombstones, which prd:234 says the RPO never relaxes;
- reconciliation of destructive-retention and external-effect state.

The architecture preserves all three during the in-place data restore. The live realm is kept and the admission projection is rebuilt (spine:320). The live tenant-key store is kept and admission/purge hooks run (spine:318–:319). Recovery mode disables external-effect workers until reconciliation (spine:314). NFR-1 (prd:296) also moves this restore outside the rollback protections. The PRD therefore describes an explicitly approved destructive restore without stating which restoration guarantees still apply. SM-5 (prd:327) tests only ingress closure and the duration of that recovery. A builder or tester could reasonably accept an approved restore that resurrects an erased Memories tenant or skips the pending reconciliation.

**Suggested fix.** Change prd:210 to: "Other state written after the point is lost; FR-9's admission-revocation, Memories-erasure and reconciliation protections apply, and the lost-window exceptions are recorded before reopening and reviewed before the stop is cleared." Add to SM-5 that the approved-recovery rehearsal proves denial after an admission revoked during the attempt and non-resurrection of a Memories erasure acknowledged during the attempt.

### F-3: Medium. The acceptance measures do not test the new degraded-path limits, the stop triggers or the post-DR stop rules

**Location:** prd:327 (SM-5), prd:328 (SM-6), prd:332 (SM-C1); rules at prd:188, :207–:209, :233–:236.

**Issue.** SM-5 now covers observed-stop scoping, a later stop prevailing, and the deputy being unable to approve. Several new rules that prevent bypass or escalation still have no acceptance check:

- **Eligibility refusal.** A degraded-path approval is refused when production's last outcome was working (the path's main bypass risk), and it is not available in the post-DR reduced-recovery state (prd:209, :236).
- **Degraded failure handling.** Candidate workloads are removed, data is kept and a new stop is set. For the degraded path, the staging fresh-install rehearsal is required.
- **Stop triggers and non-clearing.** A failed pre-update health check, an unmatched production-admission change and an availability-probe failure beyond its bound each set the stop. Monitoring never clears it. A continuing condition is recorded once.
- **Review before clearing.** Administrator's review of the lost-window exceptions comes before any stop clearance. Among the lost-window exceptions, SM-6 records only the revocation exceptions, not the non-Memories erasures, deletions and legal holds that prd:234 also requires to be recorded.
- **Post-DR stop.** After DR, the stop stays set until staging is re-established.

SM-C1 counts gate bypasses, but not approvals outside eligibility or stop clearances without an Administrator record.

**Suggested fix.** Add these scenarios to SM-5. In SM-6, require the drill record to show the reduced-recovery state, the stop still set and every lost-window category recorded. Extend SM-C1 or add a counter-metric: zero stop clearances without an authenticated Administrator record (or a verified eligible approved attempt), and zero degraded approvals when production's last outcome was working. SM-6 drills are isolated, so live-state items may be proved through the drill's DR attempt record.

### F-4: Low. The glossary and addendum lag behind the FR-8 stop-trigger and clearance wording

**Location:** prd:384, add:212; compare prd:207–208.

**Issue.** prd:207 now says "every recovery entry (automatic, in-place or disaster recovery)" and includes probe failures. The glossary (prd:384) lists only "disaster recovery" and omits probe failures. add:212 still says "disaster recovery entry". The glossary also says Administrator clears the stop "after verified recovery", which is narrower than FR-8. A stop from a deputy declaration, an unmatched admission change or a transient health check may need no recovery at all.

**Suggested fix.** Align both with prd:207–208: "every recovery entry" and "naming a verified current working release".

### F-5: Low. The deputy's reopening of service is not limited to the current entry gate

**Location:** prd:34, prd:210, prd:232, prd:389; add:223; spine:322.

**Issue.** The deputy may "reopen service" after recovery. That includes the named recovery for an approved release, which may run before G2 (prd:54, add:177), or DR before G2. Nothing states that reopening restores only the user-ingress and admission state recorded before the incident. A literal reading would let the deputy open user ingress before G2, and opening G2 depends on evidence (prd:53) that the deputy cannot attest.

**Suggested fix.** In FR-8, FR-9 or the deputy description, add: "Reopening restores only the pre-incident ingress state; before G2, user ingress stays closed."

### F-6: Low. Admission grants recorded after the cut are silently not reinstated

**Location:** prd:232; add:278; spine:320; arch-log:250.

**Issue.** prd:232 says to "reconcile production admission to Administrator's recorded grants and revocations" and then that "recovery never grants production admission". The architecture only removes admission: a grant recorded after the cut is not re-added. The PRD sentence can be read as reinstating recorded grants, which would conflict with "never grants". The resulting access loss is not reported to Administrator either. This is a clarity and availability issue, not a security one.

**Suggested fix.** State that reconciliation only removes admission, and that grants recorded after the cut are listed in the recovery report for Administrator to re-apply.

### F-7: Low. It is unclear whether the deputy may also be a named operations writer

**Location:** prd:34, prd:389; add:230, add:331, add:353; spine:36, :487.

**Issue.** The deputy is described as working "without operations-repository write access". The second organization owner is a named writer who "can change what executors run", which is an accepted risk. Neither document says whether the deputy may be that person. If so, the deputy's limits (no release approval, no stop clearance) rest on executor-side verification of signed records plus the accepted two-writer risk, not on the absence of repository write.

**Suggested fix.** In the addendum, either state that the deputy is not a named writer, or state that when they are, the accepted two-writer risk covers them and executor-side signed-record checks enforce their limits.

### F-8: Low. The reduced-recovery state does not state its effect on the NFR-2 commitment

**Location:** prd:236, prd:309, prd:386; spine:284, :296.

**Issue.** After DR, the prepared replacement capacity has been consumed until new capacity is identified. A second major failure during that time cannot meet the four-hour RTO that NFR-2 still appears to promise for outages within coverage. Neither the PRD nor the glossary says what the reduced state reduces.

**Suggested fix.** Add to prd:236 or the glossary: "While reduced, the four-hour RTO is not committed for replacement-capacity recovery; incidents record the posture alongside coverage status."

## Checked and consistent

- **Approved paths and common gates.** Neither approved mode waives staging E2E, provenance, isolation, serialization or verification (prd:48, :171, :209; SM-C1 at prd:332). The degraded path adds the fresh-install rehearsal. The approved incompatible release names its recovery, acceptance checks and maximum duration before it starts. An overrun is reported, not aborted (prd:210), matching spine:284.
- **Authority.** Only Administrator approves releases, clears stops and administers admission (prd:34, :208, :263). The deputy may recover, verify, reopen and declare a stop, and performs the named recovery only after Administrator has approved it. Recovery never grants admission. Monitoring sets stops but never clears them. No path lets the deputy do something reserved to Administrator.
- **Stop set and clear semantics.** A durable stop, set once for a continuing condition, a later stop prevailing and recovery never needing a clear stop are consistent with spine:288. The first deployment's pre-existing probe-failure stop is lifted by its empty-production approval, so it causes no G1 deadlock.
- **Post-DR.** The reduced-recovery state is not degraded production, and there is no emergency release path (prd:209, :236; add:180; spine:296).
- **Gates.** G1 keeps the human admission group empty and gives synthetic access only to synthetic data. The SM-4 grant expires by G2, and G2 requires its revocation and a denial check. A policy change suspends G3 (prd:52–56; add:186–190; spine:289).
- **RPO/RTO, coverage and isolation.** These are unchanged and still adequate (prd:298–317). Deputy hours count toward coverage only after the G2 proof (add:230).
- **Downstream routing.** prd:352 and prd:356 route the degraded-eligibility question and any change from the run-3 confirmation review. F-1 concerns the interim rule and revisit timing in prd:352, not the routing itself.
