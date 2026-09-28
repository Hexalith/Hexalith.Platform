# Extract: recovery and access findings S1–S3 (PRD update 2026-09-28)

Source material for the PRD update. This file records accepted architecture positions and current PRD/addendum text. It does not edit the PRD, addendum or architecture.

- **Reviewer file:** `prds/prd-platform-2026-09-27/validate-2026-09-28/review-recovery-access.md`
- **PRD:** `prds/prd-platform-2026-09-27/prd.md`. **Addendum:** `.../addendum.md`
- **Spine:** `architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md`, 487 lines. **Arch log:** `.../.memlog.md`, 261 lines.
- Line numbers are 1-based and were read on 2026-09-28. The line numbers cited by the reviewer still match.

## Snapshot caveat (applies to all three findings)

- The spine frontmatter says `status: draft`, `updated: '2026-09-28'`. Update run 2 finalized the spine ("Spine status set final", arch log :241; "spine finalized", :242). Update run 3 (:243 onward) then edited the spine again. The last entry says "Dispatching confirmation review" (:261), and no `review-confirm.md` exists yet in `reviews/update-2026-09-28-r3/`.
- **Finalized in run 2, 2026-09-28:** C-09, C-11, C-25, C-50, D4, D5. These are the deputy model, the empty/degraded path, the module-revocation exception, the writer set and in-place recovery.
- **Draft in run 3, 2026-09-28:** VAL-04, VAL-08, G-4 and the :260 autofixes. These are the stop revision binding, admission records and the non-admission realm-removal exception. The user accepted each of them, but the spine that carries them has not yet passed its confirmation review.
- The earlier V-37 (update run 1, 2026-09-27; arch log :189–:190) is the base erasure/deletion/legal-hold exception that the PRD already carries.

---

## S1 — Restoration revocation exceptions (High)

### Accepted architecture position (capability level)

1. **Admission revocations always survive restoration.** A restore never re-admits a principal whose production admission was revoked, even when the revocation came after the recovery cut or just before the failure. Recovery never grants admission. When a person's production access must stay contained through a disaster restore, it is contained by an admission revocation.
2. **Two accepted lost-window exceptions** apply to disaster restoration, in addition to the existing V-37 exception for non-Memories erasures, deletions and legal holds:
   - (a) **Module-owned authorization revocations** acknowledged after the recovery cut.
   - (b) **Non-admission identity-provider access removals** that were not yet durably captured off the failed environment when it failed. Examples are a user disable, a role removal or a credential reset. The architecture bounds this window by the durable event-export frontier. Removals captured before that point are re-applied.
3. **Reporting and review.** The recovery records these exceptions in its report before service reopens. Reopening does not wait for the Administrator's review; Administrator or the deputy can reopen. Administrator must review the exceptions before clearing the promotion stop, which is what resumes promotions. Destructive-retention and external-effect work stays disabled until each owning module reconciles.
4. **What stays preserved:**
   - Memories erasure/tombstone continuity. No acknowledged Memories tombstone is lost.
   - Ordinary live authorization. The exceptions apply only to restoration from recovery points.
   - Application rollback, which never rewinds revocations or current security authority (PRD:284, NFR-1).
   - Rotation of restored credentials, proof that old credentials fail, and a verified denial of every principal whose latest admission record is a revocation.

### Verbatim architecture (spine)

- **:295, Lost window:** "Non-Memories erasures, deletions, legal holds and module-owned authorization revocations acknowledged after the recovery cut, and realm access removals other than admission made after the durable event-export frontier, are accepted RPO exceptions. The recovery runner records them in the DR report before reopening, and Administrator reviews them before clearing the promotion stop."
- **:118, Admission records:** "Every grant or revocation of production human or synthetic admission, including the G1 SM-4 temporary grant, is first written as a signed Administrator record … These records, not a restored realm, are the recovery authority for admission; containment of a human's production access that must survive DR is made as an admission revocation."
- **:320, step 5 Credentials:** "Reconcile restored admission membership to the AD-6 admission records, removing every principal without a current grant record or with a later revocation. Re-apply admin and user revocations recorded after the cut, prove old in-environment credentials fail against the restored instances …"
- **:321, step 6 Verify:** "… denial at the gateway and through an asynchronous re-check of every principal whose latest admission record is a revocation, and that new credentials work and old ones fail at every surviving authority; record the lost-window exceptions."
- **:322, step 7 Reopen:** "… report the lost window. Administrator or the deputy reopens user ingress within the attempt …; the promotion stop stays set."
- **:119:** "recovery never grants admission or roles."
- **:294:** "No acknowledged tombstone may be lost and no erased key or tenant resurrected; unknown lineage fails closed."
- **:487, Accepted risks:** "… non-Memories erasures and module-owned revocations inside the lost RPO window, and realm access removals other than admission inside the event-export lag."
- **:478, G2:** "Keycloak database backup with admission reconciliation proven by a revocation lost before event export".
- **:289, G1/G2:** The G1 synthetic grant's "admission record carries an expiry no later than G2, and its revocation is followed by a denial check". G2 requires "the G1 grant's revocation record and denial check".

### User acceptance (quotes, dates)

- **C-25, arch log :220, run 2, 2026-09-28.** C-25 has no acceptance tag of its own. It sits under the Batch 3 header at :218: "Batch 3 (recovery deputy and DR), user accepted all recommendations." The entry reads: "C-25: module-owned authorization revocations acknowledged after the recovery cut are an accepted RPO exception listed in the DR report and reviewed by Administrator before reopening (consistent with V-37; per-module off-site revocation journal rejected)."
- **D5, :237, run 2, 2026-09-28.** It falls under :234, "Gate decisions (user accepted recommendations except D4)". It changes the review timing: "The recovery runner records lost-window exceptions in the DR report before reopening; Administrator reviews them before clearing the promotion stop (supersedes 'reviewed by Administrator before reopening')."
- **VAL-08, :250, run 3, 2026-09-28:** "Batch 3 (identity), user accepted recommendations. VAL-08 (amends AD-6): every production-admission grant or revocation … is first written as a signed Administrator record … recovery never adds admission (AD-6), so the deputy can complete and reopen without Administrator. Qualification: revoke, fail before event export, prove denial after DR. Implements PRD 'reapply post-cut revocations', 'denial of revoked principals' …" Line :252 confirms that the user "accepted every recommendation".
- **G-4, :258, run 3, 2026-09-28:** "Gate decision G-4 (user accepted; …): … Containment of a human's production access that must survive DR is made as an admission revocation record; other realm access removals (user disable, role removal, credential reset) after the durable event-export frontier join the accepted risks and the lost-window list." The :260 autofixes repeat it: "lost-window and accepted risks add non-admission realm removals inside export lag".

### Alternatives the user rejected

The PRD must not describe any of these as chosen:

- **C-25:** a per-module off-site revocation journal.
- **VAL-08:** "fail-closed admission after every DR (deputy cannot reopen; RTO depends on Administrator)" and "accepted loss with a shorter export bound (contradicts the PRD)". Admission-revocation loss is therefore not accepted.
- **G-4:** "record-first for every access-removing realm change (heavier)" and "drift checks only at release attempts and drills".

### Architecture-only mechanisms (keep out of the PRD)

- Signed, create-only admission records chained by predecessor digest, kept in the off-site attempt/lock/stop store.
- Keycloak group reconciliation and realm names.
- The durable event-export frontier and the export-lag bound.
- An EventStore admission projection that fails closed until rebuilt.
- The off-site monitor matching exported admission-group changes against signed records.
- DR step numbering, DR-scoped realm rights, and the "recovery runner" and "DR report" artifacts.

At PRD level, say "an admission revocation", "identity-provider access removals not yet captured off the failed environment", and "the recovery report".

### Current PRD/addendum text to replace

- **prd.md:226:** "Disaster recovery fences the failed environment and restores into quarantine. Before reopening, rotate restored credentials, reapply post-cut revocations, and verify compatible application/configuration versions, cross-module integrity, restored-release smoke tests and all NFR-3 access outcomes, including denial of revoked principals. Re-enable backups and monitoring before reopening service."
  - Change: "reapply post-cut revocations" should cover all admission revocations plus the captured identity-provider removals, subject to FR-9's accepted exceptions. "Denial of revoked principals" should become denial of every principal whose admission was revoked.
- **prd.md:227:** "… For other modules, deletions, erasures or legal holds acknowledged only inside the lost RPO window may be lost under the accepted MVP recovery envelope; report that window and complete module-owned reconciliation before resuming destructive retention or external effects."
  - Change: add module-owned authorization revocations and non-admission identity-provider access removals (with their own window). Add "recorded in the recovery report before reopening; Administrator reviews them before resuming promotions".
- **prd.md:296 (RPO row):** "… accepts up to one hour of committed-data loss subject to FR-9 erasure and security protections."
  - Change: "security protections" should point at FR-9, including its accepted revocation exceptions. As written it implies that no revocation is ever lost.
- **prd.md:303:** "… It does not waive module behavior, authorization, erasure protections or release gates; …"
  - Change: qualify "authorization" with "except the FR-9 accepted lost-window exceptions". Otherwise it contradicts C-25.
- **prd.md:309 (NFR-3):** "… Also prove permitted access for an explicitly authorized production user and denial after revocation. … Repeat the relevant checks after access changes and restoration."
  - Change: after restoration, prove denial for revoked admission, including an admission revoked just before the failure. Other revocations are subject to FR-9's exceptions.
- **addendum.md:273:** "… rotate restored credentials/signing keys and reapply post-cut revocations; verify integrity, release smokes and access; …"
  - Change: reconcile admission to Administrator admission decisions, re-apply captured removals, and record the lost-window exceptions.
- **addendum.md:275:** "For non-Memories erasures, deletions and legal holds acknowledged inside the lost window, the user accepted the ordinary RPO limitation. The recovery report states that window. … This exception does not relax Memories continuity or other module functional and authorization rules."
  - Change: extend the list. The last sentence ("does not relax … authorization rules") now conflicts with C-25 and G-4. Rephrase it to preserve live authorization, admission-revocation continuity and Memories continuity.

### Other lines that repeat the stale claim

- **prd.md:221:** "… including Keycloak, production access configuration and revocation evidence." This is consistent and can stay.
- **prd.md:284 (NFR-1):** "Application rollback must preserve … revocations and current security authority." This is still correct because it covers rollback, not restoration. Keep it, and mention it as preserved.
- **prd.md:56 (G1 synthetic grant):** "the test grant is revoked after the check." Optionally add the architecture's "expiry no later than G2 plus a denial check" at capability level.
- **prd.md:320 (SM-6):** It lists "security, erasure, data" checks. Optionally add "denial after restore of an admission revoked before the failure". This is the G2 proof at spine :478.
- **addendum.md:253 (drill checks):** "… and denial of revoked principals." Scope it to admission revocations and add recording of the lost-window exceptions.
- **addendum.md:348 (accepted risks):** "… and non-Memories erasures inside the lost RPO window." Add module-owned revocations and the non-admission access removals, matching spine :487.
- **addendum.md:248:** "Keycloak, its admin/user revocation event export" is consistent.

### Flags

- **Two different windows.** Module-owned revocations use the recovery cut. Non-admission identity-provider removals use the event-export frontier, a monitored export-lag bound, not the RPO. The PRD should not merge them into "the lost RPO window".
- **Review timing changed.** C-25 said "reviewed by Administrator before reopening". D5 supersedes that with "before clearing the promotion stop". Use the D5 wording.
- **Classification ambiguity.** A module-declared role held in Keycloak has an unclear class. Spine :115 says "Modules declare required clients, audiences, roles and claims". The r3 security review (SEC3-6) called such a removal "arguably not a 'module-owned' revocation", and G-4 now covers it as a realm removal. The PRD should not attempt a finer split.
- **In-place data restore.** An approved release's named recovery also records and reports lost-window exceptions (spine :282, :314, :321–:322, where the in-place forms keep the recording). It restores no Keycloak, so only the module-owned and data exceptions apply. This is optional context for FR-8.
- **Draft status.** The admission-record and realm-removal parts (VAL-08, G-4) are in the draft run-3 spine.

---

## S2 — Administrator-approved release into empty or degraded production (High)

### Accepted architecture position (capability level)

**Condition.** Production has no working baseline (empty) or its last outcome was non-working (degraded).

**What the Administrator approval does.** An authenticated Administrator record for one identified attempt names the reason and the promotion-stop state it observed. For that attempt only, it lifts two things:
- the promotion stop, as observed; and
- the "working baseline healthy now" precondition.

**What it never waives:**
- a stop recorded after the observed state;
- the staging gate, which rehearses a fresh install plus the candidate;
- provenance, lock/serialization, isolation, profile, qualification and enrollment checks;
- production verification and durable records.

**Outcomes.**
- A working, verified outcome acts as the stop clear, applied only if no newer stop was recorded. That release becomes the new working baseline.
- A non-working outcome sets a new stop. The rollback set is "remove workloads, keep data".

**Authority.** Only Administrator can approve. The deputy cannot approve releases or clear the stop. The deputy may declare a stop and may run recovery that keeps the stop set.

**Related paths.**
- First deployment is the "no working baseline" case.
- Manual in-place recovery by Administrator or the deputy needs no Administrator record and keeps the stop set. Any other manual change is an Administrator-approved attempt.
- After a DR restore, production is in a reduced-recovery posture that is **not** degraded production. The stop stays set until staging is re-established. There is no emergency release mode.

### Verbatim architecture (spine)

- **:280, Release modes:** "**Administrator-approved** replaces only those two triggers [G3 and complete compatibility evidence] with an Administrator record; it serves pre-G3 releases, SM-5 fault rehearsals before G2, empty or degraded production, and releases without demonstrated safe rollback. … The staging gate, lock, provenance, verification and records apply to both modes."
- **:281, Empty or degraded production:** "When production has no working baseline, or its last outcome was non-working, the Administrator record for an attempt names the reason and the observed stop revision and lifts the promotion stop and precondition 8 for that attempt only, never a set recorded after that revision; a working terminal outcome applies it as a clear by compare-and-set at that revision, and a non-working one writes a new set. The staging gate then rehearses a fresh install plus the candidate, and the rollback set is 'remove workloads, keep data'. Any other manual change is an Administrator-approved attempt under the lock and epoch; an In-place recovery needs no Administrator record. Either becomes the working baseline only after passing verification."
- **:288, Promotion stop:**
  - Set by: "every non-working terminal outcome, every recovery entry, a failed pre-update health check, a probe failure beyond a declared bound, a recorded incident, or an Administrator or deputy declaration".
  - Clear rule: "Only an Administrator record naming the reason, the observed revision and a verified current working attempt clears it, applied by compare-and-set only while that revision is unchanged, so a later set always prevails; Empty or degraded production is the only exception. Promotion stays suspended during any recovery."
  - Continuing conditions: "a continuing condition, such as one probe-failure episode or a standing incident, is recorded once until it resolves." This is the fix for the REC3-9 livelock on the degraded path.
- **:309, precondition 8:** "Production is healthy: its working baseline is ready and its smoke suite passes now." Precondition 1 (:302): "The lock and a new epoch are held, and the promotion stop is clear."
- **:296, After DR:** "Production then runs in a recorded reduced-recovery posture, which is not Empty or degraded production, … the promotion stop stays set until staging is re-established."
- **:36:** "only Administrator approves releases, clears the promotion stop and administers production admission."
- **:226:** "*promotion-stop* set records by any executor, the off-site monitor, Administrator or the deputy, and clear records only as Administrator records".
- **:289, G3:** "enable automatic promotion only after SM-5 rehearsals".

### User acceptance (quotes, dates)

- **C-11, arch log :222, run 2, 2026-09-28.** It is listed as an autofix: "Autofixes accepted: … C-11 (empty-baseline and degraded-production path via Administrator-approved record replacing the healthy-production precondition, fresh-install rehearsal, remove-workloads-keep-data rollback set; manual changes are approved attempts under lock and epoch …)". The user accepted it through the run scope at :211 ("apply 38 autofixes … autofixes applied without a round-trip"). It was not an individually discussed choice.
- **C-50, :220, Batch 3 "user accepted all recommendations", run 2:** "after DR the promotion stop stays set until staging is re-established; no emergency release mode."
- **VAL-04, :249, run 3, 2026-09-28.** It sits under :248, "Batch 2 (concurrent authority races), user accepted recommendations.": "The empty-or-degraded override is bound to one attempt ID and the revision it observed and cannot lift sets recorded after it." Rejected alternative: "clear lists acknowledged set records". Line :252: "user accepted every recommendation"; the amendments land in "Promotion stop, Release modes, Empty or degraded production".
- **:260, run 3 gate autofixes:** "Update-3 gate autofixes accepted and applied with G-1..G-5: … failed approved release removes candidate workloads; working degraded override applies as a clear; … stop revision advances only for a new cause". This entry says "accepted" but does not name the user.
- **D5, :237:** "Precondition 1's clear-stop requirement applies to release attempts." This means recovery never needs the stop cleared.

### Architecture-only mechanisms (keep out of the PRD)

- Monotonic stop revision, compare-and-set, epochs and the numbered preconditions.
- The "remove-workloads" render mode and the attempt/lock/stop store.

At PRD level, say "the promotion-stop state Administrator observed", "a stop recorded after that approval still blocks", and "the candidate's workloads are removed while data is kept".

### Current PRD/addendum text to replace

- **prd.md:48:** "… **Administrator-approved**, with an authenticated approval record for a release before G3 or an incompatible release. …"
  - Change: add empty or degraded production (and SM-5 rehearsals before G2) as uses of approved mode.
- **prd.md:168:** "An authenticated Administrator approval permits a pre-G3 release or a separately planned incompatible release. It never substitutes for the staging gate, artifact provenance, environment isolation or production verification. …"
  - Change: add the degraded/empty case. Keep "never substitutes …".
- **prd.md:185:** "Before updating production, Platform identifies the working baseline, verifies that it is healthy now, … Missing preconditions stop the attempt before mutation. First-deployment handling is defined in FR-8."
  - Change: add the exception. An Administrator-approved attempt for empty or degraded production waives only the healthy-baseline check and the observed stop.
- **prd.md:204:** "Every non-working production outcome and disaster recovery entry sets a durable promotion stop … Only an authenticated Administrator record naming the reason and verified current working release clears it. The deputy may perform documented recovery, verify restoration and reopen service, but cannot clear the promotion stop or administer production-user access."
  - Change: keep the normal clear rule, and state that a newer stop always prevails. Add the single exception: a verified approved empty/degraded attempt acts as the clear. The deputy can do neither.
- **prd.md:208:** "… An incident establishing that production is no longer working sets or retains the promotion stop until Administrator records a verified baseline; it does not trigger an automatic search through older releases."
  - Change: link to the approved degraded path as a way to establish a verified baseline. See the ambiguity flag below.
- **addendum.md:177:** "… **Administrator-approved** uses an authenticated approval for pre-G3 releases and incompatible changes; …"
  - Change: add empty/degraded production.
- **addendum.md:193:** "… The recorded working baseline must be ready and pass its smoke suite now before an update begins. … A pre-existing unhealthy production environment stops the update for investigation."
  - Change: add "unless Administrator approves one attempt under the empty/degraded rule".
- **addendum.md:207:** "… Only an authenticated Administrator record naming the reason and a verified current working release clears the stop; deputy restoration does not resume promotions. …"
  - Change: same as prd.md:204.

### Other lines that repeat the stale claim

- **prd.md:374 (glossary, Promotion stop):** "the durable block on further production promotion after a non-working outcome or disaster recovery; only Administrator may clear it after verified recovery." Add the approved empty/degraded exception.
  - The glossary also names only two stop causes. Architecture adds a failed pre-update health check, a probe failure beyond a bound, a recorded incident, and an Administrator or deputy declaration (spine :288, C-34 at arch log :222). PRD:208 covers incidents. This is minor and optional.
- **prd.md:206:** "A first deployment has no previous working release; failure stops that deployment with user ingress closed …" This is consistent. Add that a first deployment, and any retry after its failure, runs as an Administrator-approved attempt under the empty-production rule. Without that, the PRD:204 clear rule deadlocks when there is no working release; the arch log records this "promotion-stop deadlock on the empty/degraded path" at :232.
- **addendum.md:208:** "Recovery from a failed first module enrollment removes its application workloads but preserves its data objects." This is consistent with "remove workloads, keep data".
- **prd.md:207:** "A manual recovery or disaster restore becomes the working baseline only after its required verification passes." This is consistent. Optionally add "or an approved empty/degraded attempt".
- **prd.md:319 (SM-5):** It covers "first deployment, failed/unverified recovery, durable promotion stop and Administrator-only resumption". Suggested additions:
  - an approved attempt on degraded production: its verified success clears the observed stop and sets the baseline, while a stop recorded after the approval still blocks;
  - a deputy cannot approve such an attempt.
- **prd.md:54 (G3) and prd.md:324 (SM-C1):** Both are consistent. Approved attempts precede G3 and do not bypass G2, and "Approval cannot waive common gates."
- **addendum.md:346:** "Before the first applicable production attempt, including an approved pre-G3 attempt; …" This is consistent.

### Flags

- **"Last outcome was non-working" is ambiguous for stops that are not attempt outcomes.** Examples are a post-window incident, a probe failure or a failed pre-update health check on a baseline that was verified working. Spine :288 lists these as stop causes, but :281 keys the override on the "last outcome". PRD:208 says an incident holds the stop "until Administrator records a verified baseline". It does not say whether the degraded override is the route for that. The PRD should either mirror the architecture's wording or raise this with the architecture owner. It should not decide the question itself.
- **Degraded production with existing data.** :281 prescribes a staging rehearsal of "a fresh install plus the candidate". Precondition 9 (:310) otherwise routes missing or wrong-baseline compatibility evidence to "a named planned recovery". The architecture does not say whether a degraded (non-empty) override also needs compatibility evidence or a named recovery point. This is an architecture-level question; the PRD should stay silent.
- **Not an emergency mode after DR.** Spine :296 and C-50 exclude the post-DR posture. The PRD must not present the approved degraded path as a way to release before staging is re-established after a DR.
- **Acceptance strength.** C-11 was accepted through the user's scope selection, not a discussed choice. The run-3 refinements (VAL-04, :260) are user-accepted but sit in the draft spine.

---

## S3 — Operations-writer set and deputy controls (Medium)

### Accepted architecture position (capability level)

**Writers.** The operations repository and the notification repository have exactly two named writers: Administrator and the second Hexalith organization owner. The organization base permission is read or none.

**Accepted risk.** Either named writer can change what the executors run.

**Enforcement.** The project stays on GitHub Free. Enforcement relies on executor allowlists and Administrator-signed records; repository write never authenticates a record. The GitHub Team control review is triggered only when someone beyond the named writers gains write or admin.

**Deputy rights:**
- receives every deployment-failure, recovery, backup and monitor notification;
- holds independent recovery and key access;
- under their own MFA identity, runs documented in-place recovery or replacement-capacity recovery, with no operations-repository write and no GitHub dependency;
- verifies and reopens whether or not Administrator is available;
- cannot approve releases, clear the stop or administer admission;
- has identity rights scoped to recovery (restore, revocation replay, key rotation).

**Still open:**
- G2 qualification of the deputy's identity, minimum permissions, key custody, alert delivery, and rehearsed restore and reopen. Deputy hours count toward response coverage only after that proof.
- Re-syncing the spec and addendum with the architecture.
- Actually configuring the repositories.

### Verbatim architecture (spine)

- **:36:** "The named **recovery deputy** receives every deployment-failure, recovery, backup and monitor notification, holds independent recovery and key access, and may execute documented recovery, verify restoration and reopen service, whether or not Administrator is available. **Named writers** of the operations repository are Administrator and the second Hexalith organization owner."
- **:130:** "Deployment workflows live in a private operations repository whose only writers are the named writers; the organization base repository permission is read or none. … Executors … act on an Administrator record only after verifying its signature."
- **:131:** "The production executor also accepts an in-place recovery started locally by Administrator or the deputy under their own MFA identity through an allowlisted recovery entry point. … It accepts replacement-capacity recovery started by Administrator or the deputy under their own MFA identity from a pinned off-site copy of the recovery workflows and environment definitions, needing neither operations-repository write nor GitHub."
- **:236:** "GitHub issues in a private notification repository with the operations repository's writer set, raised through an issues-only credential, assigned to Administrator and mentioning the recovery deputy, are the single accepted notification path."
- **:292:** "deputy hours count toward coverage only after the G2 deputy proof."
- **:478 (G2 owned work):** "the deputy's identity, minimum permissions, key custody, alert delivery and rehearsed restore and reopen".
- **:480:** "re-sync `_bmad-output/specs/spec-platform/SPEC.md` and the PRD addendum (tool identity, notification recipients, deputy follow-up) with this spine".
- **:481:** "Adopt environments, protected branches and runner groups once anyone beyond the named writers gains write or admin on the operations repository."
- **:487 (accepted risk):** "two named writers of the operations repository, each able to change what executors run".
- **:458 (owned work):** "set the organization base repository permission to read or none; create the operations and notification repositories with only the named writers."

### User acceptance (quotes, dates)

- **C-09, arch log :218, run 2, 2026-09-28.** It carries the final PRD deputy model: "Batch 3 (recovery deputy and DR), user accepted all recommendations. … the off-site recovery executor accepts recovery runs started by Administrator or the deputy under their own MFA identity …, needing neither operations-repository write nor GitHub; the deputy's realm rights are DR-scoped … Before G2: prove the deputy's identity, minimum permissions, key custody, alert delivery and rehearsed restore and reopen."
- **D4, :236, run 2, 2026-09-28.** This was the user's own choice, not the recommendation. Line :234 says "user accepted recommendations except D4". The entry reads: "D4 (user chose option 3 with owners-only writers): the second Hexalith org owner (tinouit) is accepted as a named writer and administrator of the operations and notification repositories alongside Administrator; … Accepted risk: every named writer of the operations repository can change what executors run. GitHub Free is kept; … the GitHub Team trigger becomes 'anyone beyond the named writers gains write or admin'. Supersedes 'writable only by Administrator' in AD-7 and memlog RV-1's single-writer framing."
- **D5, :237:** "Administrator or the deputy may start an in-place recovery on the production executor under their own MFA identity … (no operations-repository write) … The deputy may act whether or not Administrator is available; deputy hours count toward response coverage only after the G2 deputy proof."
- **PRD log :76 (PRD-side origin):** "user selected deputy authority to receive alerts, execute documented recovery, verify and reopen service; Administrator alone resumes promotions. This requires downstream alignment of architecture operational-access and notification rules …". The architecture completed that alignment in run 2.

### Architecture-only details (keep out of the PRD)

- The second owner's GitHub handle ("tinouit").
- Issues-only credential, off-site recovery workflow copy, allowlisted recovery entry point, executor names.
- Publication-repository ruleset editors.

At PRD level, "the second Hexalith organization owner" is sufficient.

### Current PRD/addendum text to replace

- **addendum.md:225:** "Implement Option 1 with ordinary named identities, narrowly scoped permissions and a rehearsed runbook, without a custom workflow system. **Architecture follow-up is required:** its current Administrator-only operations access and notification rules do not yet implement deputy recovery authority. Update those controls and prove independent deputy access before relying on deputy response coverage. This does not automatically grant operations-repository write access; if a second writer is introduced, the accepted GitHub Team control review applies."
  - Change: the architecture now implements the deputy's notifications, recovery and reopening without repository write. What remains is to implement and prove the deputy's identity, minimum permissions, custody, alert delivery and rehearsed restore/reopen before counting deputy coverage. The second writer is already accepted, and the Team review applies only beyond the two named writers.
- **addendum.md:326:** "| GitHub plan and control boundary | Remain on GitHub Free with a single-writer operations repository and enforcement at the executor/target through allowlists and OIDC or executor-held credentials. Revisit GitHub Team controls when a second writer is introduced. The deputy access amendment must preserve this boundary or explicitly trigger that review. |"
  - Change: two named writers (Administrator and the second organization owner) with the accepted risk. Enforcement uses executor allowlists and signed Administrator records. The review trigger is anyone beyond the named writers. The deputy needs no repository write.
- **addendum.md:342:** "| Deputy operational access, alerts and rehearsed recovery | Administrator with Platform | Amend architecture controls and prove the deputy's own identity, minimum permissions, key custody, notification delivery and restoration/reopening authority before counting deputy coverage. Administrator alone resumes promotions. |"
  - Change: drop "Amend architecture controls". Keep the proof items, gated at G2.
- **prd.md:346:** "| Align architecture operational access and notification recipients with the selected deputy role; synchronize spec/acceptance wording for both release modes, G1–G3 and RTO coverage | Platform architecture/spec owners with Administrator | Before recovery/deployment stories are finalized; this PRD's later explicit decisions govern any stale downstream wording |"
  - Change: mark the architecture alignment as done in the spine. Keep the spec sync, and the addendum sync per spine :480. Deputy qualification stays at G2 (PRD:344).

### Other lines to check

- **addendum.md:325:** "An Administrator-controlled private operations repository supplies allowlisted deployment workflows …" This conflicts with the two named writers. Say "named-writer" or "privately controlled".
- **addendum.md:348 (accepted risks):** "… GitHub Free controls, …" Add "two named writers, each able to change what executors run", matching spine :487.
- **addendum.md:214:** "GitHub issues assigned to Administrator are the accepted channel … The selected deputy policy below adds delivery to the named deputy …" This is consistent. Optionally note "private notification repository with the same writer set".
- **prd.md:34:** "… reopen service when Administrator is unavailable." Architecture (spine :36, D5) says "whether or not Administrator is available". Optional wording alignment; the reviewer did not treat it as a defect.
- **addendum.md:185:** "Architecture must carry this restricted qualification step …" This is now carried at spine :289. It is a stale follow-up of the same kind, outside S3 proper.

### Flags

- **Live state does not match yet.** Arch log :233 (2026-09-28, read-only gh api) observed "GitHub org Hexalith has two owners (jpiquot, tinouit) and default_repository_permission write". Setting the base permission to read or none is still owned work (spine :458). The PRD should not state it as done.
- **Deputy identity is unspecified.** The architecture does not say whether the deputy is the second owner. The deputy's rights are defined independently of repository write.
- **Acceptance.** Everything here is from finalized run 2 and is user-accepted. D4 is explicitly the user's own choice against the recommendation.
