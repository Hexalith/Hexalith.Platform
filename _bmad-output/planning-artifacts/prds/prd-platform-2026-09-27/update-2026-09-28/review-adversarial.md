# Adversarial PRD review: 2026-09-28 update

Reviewed: 2026-09-28
Artifacts: `../prd.md` (draft, updated 2026-09-28), `../addendum.md`, `../.memlog.md` entries 89–101. Architecture spine consulted only to confirm intent.
Prior review: `../validate-2026-09-28/review-adversarial.md` (A1, A2).

**Verdict: Not ready to finalize as written.** There is one high finding: the interim rule leaves no clear way to ship a fix when a verified baseline stops working. There are 11 medium findings. Most are in the new attachment, CI-binding, promotion-stop and post-recovery rules. Each can be fixed with targeted wording and none reverses a user decision.

| Severity | Count |
| --- | --- |
| Critical | 0 |
| High | 1 |
| Medium | 11 |
| Low | 5 |

**Prior findings.** The update addresses the intent of A1: CI evidence is now bound to the candidate and baseline-only runs are labelled. It also addresses A2: an accepted attachment now holds the owner's environment. Both new rules open further edge cases, covered in ADV-6 to ADV-9.

Scope: I did not count missing implementation or qualification evidence as a defect. User-accepted policy choices, such as the revocation exceptions, no emergency release after disaster recovery (DR), and attachment option 1, are not challenged. Only their wording, bounds and interactions are reviewed. Line numbers refer to the current files.

---

## ADV-1: High. No clear way to ship a fix when a verified baseline stops working

**Location:** PRD FR-7 L188; FR-8 L207–L209, L213–L214; downstream table L352; glossary L384–L385. Addendum L198.

**Scenario that breaks:**

1. Release R passed verification and is the working baseline.
2. After the verification window, a latent defect breaks a critical flow. The probe fails beyond its bound or an incident is recorded, so the promotion stop is set (L207, L214).
3. Fix R+1 passes staging and is ready.
4. **Standard clear fails.** It needs "a verified current working release" (L208), and R is no longer working.
5. **Clearing anyway fails.** If Administrator names R's earlier verification and clears, FR-7's pre-update health check fails and sets the stop again (L188).
6. **Ordinary approved attempt fails.** It is still subject to the stop and the healthy-baseline check.
7. **Degraded path is closed.** It requires "no working release, or its last outcome was non-working" (L209, L385). R's last outcome was working, and a working release is defined by past verification (L382). Row L352 explicitly defers exactly these stops ("incidents, probe failures or failed pre-update health checks on a verified baseline"). Until that is decided, they "clear only through the standard Administrator record", and the standard record cannot be written.
8. **DR does not apply,** because there is no server or storage failure.

The only exit is a workaround: redeploy R as a "manual recovery" (L213) and hope it fails verification, which makes the last outcome non-working and opens the degraded path. That is the path L352 said stays closed. If the defect escapes the smoke checks, the redeploy verifies instead.

It is worse after an approved incompatible release (L210). The previous release cannot read the data, so fixing forward is the only option.

The revisit trigger, "before the first approved degraded-production attempt", comes too late. This can happen at the first post-window incident, from G1 onward.

**Suggested fix:** Set an interim rule in the PRD now. For example: "A recorded incident, probe failure or failed pre-update health check that shows the working baseline no longer passes readiness and smoke checks makes production degraded for FR-8 until a verified attempt succeeds." Alternatively, state that the standard clear accepts a verified approved attempt started under the stop. Move the L352 revisit condition to "before G1".

---

## ADV-2: Medium. A stop can be cleared while its cause persists, and a continuing condition then never re-sets it

**Location:** PRD FR-8 L207 ("A continuing condition is recorded once until it resolves"), L208; glossary L384. Addendum L212.

**Scenario that breaks:** An external DNS or ingress problem makes the off-site probe fail beyond its bound, which sets the stop. The baseline still passes its internal readiness and smoke checks. Administrator writes the clear record, naming a reason and a verified current release (L208). The probe episode continues, but it is the same continuing condition, so no new stop is recorded. Promotions resume while production is still unreachable from outside, and FR-7's internal health check does not catch it.

The same happens with a standing incident or unresolved admission drift (ADV-3). The clear rule never requires each recorded cause to be resolved or explicitly accepted. Implementers will differ: one re-sets the stop after a clear while the condition persists; another treats the clear as ending the episode.

**Suggested fix:** Require the clear record to state, for each cause, whether it was resolved or explicitly accepted. Also state that a monitor-raised condition still present after a clear is recorded as a new cause.

---

## ADV-3: Medium. The admission-drift trigger looks for unrecorded changes, not for a mismatch between records and actual membership

**Location:** PRD FR-8 L207 ("production-admission change without a matching Administrator record"); FR-11 L263; G2 L53; L56; glossary L384. Addendum L190.

**Scenario that breaks:** Administrator writes a signed revocation record to contain user U. Applying it in Keycloak fails or is never done. Keycloak's membership did not change, so there is no "change without a matching record": no stop, no alert. U keeps production access while the records say U is revoked.

The same gap applies when a temporary grant passes its expiry (L56 "expires no later than G2") and membership is never removed. The records and live membership diverge in the dangerous direction, and the trigger is silent. Detection also depends on the identity-provider event export, which has no bound at PRD level (ADV-12).

**Suggested fix:** Define the trigger as a mismatch between the production-admission group and Administrator's records, in both directions:

- members without a current grant, and
- principals whose latest record is a revocation or an expired grant.

Check this on a declared cadence. State that clearing a stop raised this way requires the membership to be reconciled first.

---

## ADV-4: Medium. "A stop recorded afterward still blocks" does not say what it blocks

**Location:** PRD FR-8 L209; FR-9 L231 (probe cadence); FR-7 L190; SM-5 L327; glossary L384.

**Scenario that breaks:** Production is non-empty but degraded and still partly serving users. Administrator approves one attempt against stop state N. The candidate's rollout takes several minutes, and the off-site probe (every ≤5 minutes, with a "declared bound" that has no owner or value) records a new unavailability episode, giving stop N+1.

Implementers can read "still blocks" three ways:

- **(a)** Abort the in-flight attempt, leaving production half-updated.
- **(b)** Let the attempt finish, but do not apply the clear. The PRD says a verified success "clears the observed stop and becomes the working baseline" as one step, so it is unclear whether the verified candidate becomes the baseline.
- **(c)** Block only new attempts.

The approved attempt's own rollout can trigger this, which defeats the degraded path in exactly the case it exists for. Separately, an automatic deployment that FR-7 accepts (under 60 continuous seconds of unavailability) can still trip the probe bound. That leaves a stop needing manual clearance after a successful promotion.

**Suggested fix:**

- State that a later stop prevents only the clear. The in-flight attempt runs to its outcome, and a verified success still becomes the working baseline.
- Name who declares the probe bound, and require it to be consistent with FR-7's rollout and verification thresholds.
- Optionally, count probe failures during a locked attempt's rollout toward that attempt's verification rather than as a new stop.

---

## ADV-5: Medium. Restoring data after a failed approved incompatible release does not clearly keep FR-9's erasure and revocation protections

**Location:** PRD FR-8 L210; NFR-1 L296; FR-9 L229, L233–L234; L48; FR-6 L171. Addendum L179.

**Scenario that breaks:** User ingress stays open during the incompatible attempt's rollout and verification, about 15 minutes or more. During that time a user erases a tenant's Memories data and the erasure is acknowledged. The attempt fails. The approved recovery "restores data to that recovery point", and L210 says flatly that "state written after the point is lost". FR-9's Memories guarantee ("every acknowledged erasure tombstone", L234) and "admission revocations always survive" (L233) appear only in the DR requirement. NFR-1 L296 calls this restore "not application rollback", and L210 only says losses are "reported with FR-9's lost-window exceptions".

An implementer can therefore build a plain point-in-time restore that brings erased data back. Two other points are loose:

- The PRD requires "a complete recovery point" before the attempt. It does not say the point must be usable in FR-9's sense (verified), or that it is cut after the attempt lock. The addendum (L179) says "cut after the lock".
- The PRD does not say which checks must pass before ingress reopens: FR-9's access, credential and NFR-3 checks, and keeping destructive-retention and external-effect workers disabled until reconciliation (L234).

**Suggested fix:** State that this in-place restore:

- keeps FR-9's Memories tombstone and key continuity and admission-revocation survival;
- keeps destructive-retention and external-effect workers disabled until module reconciliation; and
- re-verifies NFR-3 and SM-4 access outcomes before reopening ingress.

Also require a usable recovery point cut after the attempt lock.

---

## ADV-6: Medium. "Candidate" and "candidate acceptance" are not defined, and nothing enforces the refusal

**Location:** PRD FR-4 L121; SM-3 L325; FR-5 L150; downstream table L347, L355. The same word "candidate" means a production release in L209, L210 and L294.

**Scenario that breaks:**

- **Which revision?** GitHub CI runs a Parties pull request on the merge ref, a merge commit M1. The pull request later merges as M2 on a base that has moved. One implementer treats M1 evidence as evidence for "a different revision" and refuses it, so every merge needs a post-merge run. Another binds to the pull-request head H, which matches neither M1 nor M2.
- **Where is it refused?** No requirement names the decision that candidate evidence gates. It could be a merge check, admission of a module revision into a staging release, or only the SM-3 demonstration. So "refused as candidate evidence" may have no effect. It is also unclear whether the unchanged modules in a release (L46) need fresh candidate evidence or reuse what was accepted earlier.
- **Platform-workspace modules.** For EventStore, Memories and McpCli, "the module's Release artifact" is ambiguous.
- **Local evidence.** Local runs use the active checkout, which may have uncommitted changes (L119). Such evidence corresponds to no revision, and no rule stops it from being offered as acceptance evidence.

**Suggested fix:** Define "module candidate" as a commit SHA, and state whether that is the pull-request head or the merge result. Name the decision the evidence gates. State that unchanged modules reuse their accepted evidence, and that only CI evidence from a clean revision can satisfy candidate acceptance.

---

## ADV-7: Medium. Binding CI evidence to "the module's Release artifact" still lets an older build be exercised

**Location:** PRD FR-4 L121, L130; FR-2 L90; SM-3 L325. Addendum L34, L54, L326 (one Platform-composed EventStore host contains the module extension packages).

**Scenarios that break:**

- **More than one artifact per module.** Parties ships a server image, an EventStore extension package loaded into the composed EventStore host, and a Contracts package that McpCli uses. CI builds and names the candidate server, while the composed EventStore host pulls the published Parties extension from the Builds catalog. The evidence passes the new rule, but the changed extension behavior was never run. This is the A1 problem again, through a second artifact.
- **Version string rather than content.** The candidate package is packed with the same version string as an already published package. The package cache resolves the published copy. The evidence names "Parties 1.4.0 built from abc", which is correct by name and wrong by content.
- **Attachment.** Attachment checks cover composition, artifact mode, readiness and data isolation (L130), not artifact identity. Run B for candidate C2 attaches to A's environment, which runs C1. B's evidence is either wrongly attributed to C2 or refused only after the run.

**Suggested fix:**

- Identify every artifact the module adds to the composition by content digest, with its source revision recorded in provenance.
- Require the running environment to report the identities it actually loaded, as FR-7 does for production ("Results bind the intended served release").
- Add artifact identity to attachment compatibility for candidate runs.

---

## ADV-8: Medium. When the owner and an attached run finish differently, the rules do not say which outcome decides retention

**Location:** PRD FR-4 L134–L137; SM-3 L325; SM-C4 L335. Addendum L54, L104–L105.

**Scenario that breaks:**

- **Owner passes, attached run fails.** Local owner A passes, and attached run B fails. Retention follows "the run's first terminal outcome" (L135), which for the environment's owner is A's success, so cleanup runs once B ends. B's developer loses the failed environment that the failed-local rule (L134) promises for debugging.
- **Attached run ended by the owner.** When an explicit stop or owner cancellation "ends and reports" an attached run (L136–L137), its outcome is not defined. It could be recorded as a false test failure, as cancelled, or with its partial passing results counted as a pass. The update's own option analysis proposed "environment withdrawn by owner, neither pass nor failure, not valid evidence", but that wording was not carried into the PRD.

**Suggested fix:** State one of two rules:

- a failure by any run served by the environment retains it under the failed-local rule, or
- only the owner's outcome counts, and a run that needs failure retention must use its own environment.

Define the outcome of an attached run ended by the owner as "withdrawn": neither pass nor fail, and not valid integration evidence.

---

## ADV-9: Medium. Deferred cleanup has no time limit, and the CI rules contradict the addendum

**Location:** PRD FR-4 L130, L135–L138; SM-C4 L335. Addendum L54, L104–L105.

**Scenarios that break:**

- **A hung attached run holds the environment.** Owner A passes locally, and attached run B hangs. Cleanup waits indefinitely. The environment is not "retained after failure", so it may not appear in the retained list (L135). Its ports stay held, and later runs fail with "the conflicting run identified" (L130).
- **New attachments keep extending the hold.** Nothing forbids attaching to an environment whose owner has already finished. A stream of attachments can postpone cleanup indefinitely. Attachment to a failed environment kept for debugging is also allowed.
- **CI cleanup deferral has no limit.** "defers but never skips" (L138) conflicts with disposable runners:
  - If the owner job ends, the runner is destroyed while B is running, which is what SM-C4 L335 forbids.
  - If the owner job instead blocks, it runs until the GitHub job timeout kills the runner, with no reported cleanup.
- **CI owner cancellation contradicts the addendum.** The PRD says local cancellation ends attached runs (L137) and CI cancellation defers cleanup (L138). The addendum says owner cancellation ends and reports attached runs for local and CI alike (L54, L105).
- **No stop mechanism in CI.** CI has no "explicit developer stop" to end a hung hold.

**Suggested fix:**

- Limit how long an attached run can hold the environment, for example to its own test timeout or, in CI, to the owner job's lifetime. When the limit expires, end the attached run and report it as withdrawn.
- Refuse new attachments once the owner has a terminal outcome, except, if intended, to failed-local environments kept for debugging.
- Either limit CI attachment to runs on the same runner and job, or adopt the addendum's rule that owner cancellation ends attached runs first.
- List pending-cleanup environments with their attached runs.

---

## ADV-10: Medium. The reduced-recovery state has entry and exit conditions but no defined consequences

**Location:** PRD FR-9 L236; glossary L386; FR-8 L209; NFR-2 L305–L309; SM-6 L328. Addendum L180, L182.

**Scenario that breaks:** DR restores production onto the prepared capacity, and the deputy reopens user ingress. Staging was on the lost shared node. The PRD leaves six questions open:

1. Users stay admitted while G2 conditions (prepared capacity, a passed drill) no longer hold. The PRD does not say whether that is allowed.
2. A second failure cannot meet the four-hour RTO because there is no prepared capacity. The PRD does not say whether the RTO commitment is suspended and reported as such.
3. The stop holds "until staging is re-established", but staging re-establishment has no owner, target or downstream row. Every release, including a security fix to the restored release, therefore waits indefinitely. The urgent-patch exception (addendum L182) covers only shared infrastructure.
4. Once staging returns and the stop clears, it is unclear whether automatic promotion (G3) may resume while still in reduced recovery.
5. The monthly drill (SM-6) cannot run on prepared capacity during this state.
6. "Not an emergency release mode after disaster recovery" (L209) may or may not also rule out the degraded path after a failed or unverified DR.

**Suggested fix:**

- State what reduced recovery allows and restricts: user access continues; the RTO commitment is suspended and reported; automatic promotion stays suspended until the return to G2 conditions; the drill cadence is deferred.
- Add a downstream row that owns re-establishing staging.
- State how a security fix to the restored release is handled, even if the answer is that none exists.
- Clarify whether the L209 exclusion covers a failed DR.

---

## ADV-11: Medium. Shared-infrastructure change control has no recovery path and relies on an undefined currency check

**Location:** PRD FR-6 L170, L172; NFR-1 L292; NFR-3 L317; FR-8 L200, L209. Addendum L177, L182.

**Scenario that breaks:** An ingress, CNI or Dapr control-plane upgrade runs under both locks. Production's re-run smoke checks fail, which is "a non-working outcome" (L172) and sets the stop. The PRD offers no recovery:

- FR-8's automatic recovery covers only "a failed compatible production deployment" (L200).
- NFR-1 says rollback never rewinds shared infrastructure (L292).
- The degraded path's failure rule refers to "the candidate's workloads" (L209).

Staging runs on the same shared change, so staging evidence for a fix may also be impossible to produce. One implementer will use an approved forward revert, another will enter DR.

Other gaps:

- The "shared-infrastructure currency check", which blocks automatic promotion (L170), is not defined. It is unclear what is compared, how often, against which inventory, and whether a patch-level lag fails it. If patch lag fails it, each upstream CVE blocks automatic promotion until the change passes the monthly rehearsal. That rehearsal-before-apply rule and the urgent-patch exception appear only in the addendum (L182).
- "Each environment's working-release smoke checks" (L172) is undefined for staging, which has candidates, not a working release.
- Shared-infrastructure control sits inside the staging E2E requirement (FR-6), which makes it easy to miss.

**Suggested fix:**

- Add a separate requirement, or an FR-7/NFR-3 bullet, stating that a failed shared-infrastructure verification recovers by forward revert or DR, under the stop, owned by Administrator.
- Define the currency check: the inventory, the cadence, the allowed patch lag, and whether it also blocks the approved mode.
- Bring the rehearsal and urgent-patch rules into the PRD, or cite them from it.
- Say what staging re-verification runs.

---

## ADV-12: Medium. The window for losing identity-provider access removals has no limit and is not monitored

**Location:** PRD FR-9 L229, L231, L233; NFR-2 L304; SM-6 L328. Addendum L280.

**Scenario that breaks:** The exception is "identity-provider access removals other than admission that had not yet been captured outside the failed environment" (L233). Nothing limits how far that capture may lag:

- NFR-2's one-hour RPO explicitly carves out "its listed revocation exceptions" (L304).
- FR-9's monitoring covers recovery-point age and availability, not export lag (L231).
- A "usable recovery point" requires only unspecified "required security … context" (L229).

If the export stalls for a day, a day of user disables and credential resets is silently lost, the accepted exception covers it, and no alert fires. The architecture bounds and monitors this lag, but the PRD lost that bound in translation.

Separately, "Administrator reviews them before clearing the promotion stop" attaches the review to release control. It does not protect access, and it has no required outcome, while the deputy may reopen service before any review. That matches the user's decision, but readers may assume it protects access.

**Suggested fix:**

- Add a declared, monitored limit on export lag. Require it for a usable recovery point, and notify Administrator and the deputy when it is exceeded.
- State plainly that the review gates only resuming promotions, not reopening service.

---

## ADV-13: Low. The tenant-lifecycle limit does not say who may declare such a flow or who removes leftover tenants

**Location:** PRD FR-6 L166; FR-7 L186; FR-4 L129.

**Scenario:**

- Any module can add a "tenant-lifecycle critical flow". Review is required only for removing or remapping flows (L345), so Parties can give its staging E2E suite tenant create and delete authority without review by the Tenants owner.
- Synthetic tenants left by failed or cancelled staging E2E runs have no cleanup obligation.
- Changes to non-synthetic staging tenants other than create and delete, such as suspend, key rotation or membership, are unconstrained.
- Production smoke tests "never create … tenants". The PRD does not say who provisions the production synthetic tenant before the first deployment's verification or after DR.

**Suggested fix:**

- Require Tenants-owner review when a flow is declared tenant-lifecycle.
- Require cleanup or reporting of leftover run-scoped synthetic tenants.
- State that the production synthetic tenant is provisioned by an operator-authorized step before verification.

---

## ADV-14: Low. The G3 suspension trigger does not say what counts as a policy change

**Location:** PRD G3 L54. Addendum L188.

**Scenario:** A module weakens its smoke suite. One implementer treats that as a "verification policy" change and suspends automatic promotion. Another treats only FR-7 thresholds as policy, so the weakened suite goes live without re-rehearsal. "Affected" rehearsals has no named decider.

**Suggested fix:** List what triggers suspension. For example: FR-7 and FR-8 thresholds and budgets, recovery procedures, rollback-set rules, and whether module readiness or smoke declaration changes count. Name who decides which rehearsals are affected.

---

## ADV-15: Low. The SM-4 test grant might be proved through the synthetic group instead of the human admission path

**Location:** PRD G1 L52, L56; SM-4 L326; FR-11 L263.

**Scenario:** L52 gives synthetic identities standing separate access. L56 lets Administrator "admit a designated synthetic production test identity" without saying the grant goes through the human production-admission path that FR-11 governs, and SM-4 calls it a "test user". An implementer can produce SM-4 positive evidence through the standing synthetic group. That never exercises explicit production-user admission.

**Suggested fix:** State that the temporary grant is made through the human production-admission path, by an Administrator admission record, to prove FR-11's positive case.

---

## ADV-16: Low. A failed approved attempt on non-empty degraded production leaves nothing running

**Location:** PRD FR-8 L209, L178. Addendum L180.

**Scenario:** Degraded production is partly serving users. The approved candidate fails, and "failure removes the candidate's workloads while keeping data". Production now serves nothing, and the partly working previous release is not restored. The user-ingress state after this failure is stated for first deployment (L212) and incompatible releases (L210), but not for degraded production.

The staging rule "rehearses a fresh install plus the candidate" does not say whether staging's data and users are wiped. A fresh-install rehearsal also does not reflect production's existing data. Row L352 partly defers this.

**Suggested fix:** State the ingress state and expected service level after a failed degraded attempt, so the approval covers that risk. Say how staging carries out a fresh-install rehearsal without destroying staging state.

---

## ADV-17: Low. Several sentences are too dense to act on, and two editorial issues remain

**Location and suggested splits:**

| Line | Problem | Suggested fix |
| --- | --- | --- |
| FR-8 L207 | Eight stop triggers in one sentence | Make a bulleted trigger list |
| FR-8 L209 | Seven clauses | Split into eligibility, approval content, what is lifted, what still applies, rehearsal, failure, success |
| FR-8 L210 | Dense | Separate the attempt from the recovery |
| FR-4 L136 | Five lifecycle rules | Separate holding, cancellation, explicit stop and failure retention |
| FR-6 L172 | Mixes attempt serialization, shared-infrastructure control and evidence protection | Move shared-infrastructure control out of FR-6 (see ADV-11) |
| FR-9 L233 | Guarantee, exceptions, operator guidance and reporting in one bullet | Split |
| SM-5 L327 | Seven sentences, about twelve scenarios | Make a scenario list |
| Downstream table L356 | Revisit condition is one run-on sentence of about 40 words | Shorten |

**Editorial issues:**

- The link to `update-2026-09-28/update-summary.md` (L16) does not resolve yet. Create the summary at finalize or remove the link.
- SM-3 (L325) does not demonstrate baseline-only labelling. Add "a baseline-only run is labelled and cannot satisfy candidate acceptance".
- These terms carry gating effect but are not in the glossary: "candidate" (ADV-6), "shared-infrastructure currency check" (ADV-11) and "manual recovery" (ADV-1).
