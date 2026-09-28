# Pragmatism and Bloat Review, update run 3 (2026-09-28): Platform Architecture Spine

- **Target:** `ARCHITECTURE-SPINE.md` (working tree, status `draft`, 485 lines, 15,005 words). Compared with the pre-update snapshot behind `spine.diff` (14,340 words), so the delta is +665.
- **Decisions:** `.memlog.md` from "Update run 3 started" (L243 to L253): the VAL-01, VAL-03, VAL-06 and VAL-07 autofixes, and batches 1 to 3 (VAL-09, VAL-10, VAL-02, VAL-04, VAL-08, VAL-05).
- **Lens:** does each new or changed sentence prevent a divergence between independently built units, or is it rationale, duplication, implementation detail or over-specification? Is any new rule given two or more homes? Is anything harder to find? Is any mechanism heavier than the decision the user accepted?
- **Guardrails:** no trim below removes an accepted decision's binding content. The trims only drop restatements, rationale and re-enumerations whose binding home is named. PRD-protected wording (R&R thresholds, the four-hour RTO) is untouched. Prior declines (PR-230, PR-231, PR-232) are not re-raised.
- **Method:** each "Old" string below was checked to occur exactly once in the spine. The autofix set was applied to a scratch copy in sequence and re-counted with `wc -w`.

## Verdict

**PASS WITH TRIMS. The +665 words are earned.**

1. **About 90% of the growth is binding content from the ten accepted findings.** VAL-10 timing accounts for +118, VAL-02 takeover fencing for +66, VAL-08 admission records for +90 across AD-6, DR step 5 and G2, VAL-09 recovery mode and in-place branches for about +100, and VAL-05 for +59. VAL-03, VAL-04, VAL-07 and VAL-01 account for the rest. The Terms pointers (+14) pay for themselves. Several edits removed duplication: DR step 2 now points at recovery mode (−14), and the environment-layer change path points at Dapr activation (−12).
2. **No mechanism is heavier than what the user accepted.** Each spine clause matches its memlog decision or is lighter:
   - VAL-10 compresses an eleven-item release-kind list into "every lock-covered change except a data restore or DR".
   - VAL-02 keeps the Secret-bound TokenRequest seed out of the spine.
   - VAL-04 leaves the encoding to Builds.
   - VAL-05 adds no run binding.
   - None of the rejected alternatives (epoch ValidatingAdmissionPolicy, list-based clears, a flat 4 h lifetime, per-run tenant claims) came back in.
3. **Two medium findings matter more than the trims:**
   - **PR3-1:** the operations-repository definitions rule now sits inside the **Dapr activation** paragraph, so an agent looking for where environment definitions live will not find it.
   - **PR3-2:** the in-place quiesce step says "closes user **admission**". An agent can read that as emptying the AD-6 production-admission group. Recovery can never re-add members to that group, so the deputy could not reopen.
4. **About 70 words of the growth are restatement or rationale.** They sit in the DR intro, DR step 4, AD-8, First shared versions, one Owned work row and the In-place row. The autofix trims recover **77 words (−0.5%, to 14,928)**, about 12% of this update's growth. Going further would mean cutting VAL decision text.

| Severity | Count | Action | Count |
| --- | --- | --- | --- |
| High | 0 | autofix | 11 |
| Medium | 2 | discuss | 1 |
| Low | 14 | defer | 1 |
| **Total** | **16** | ignore | 3 |

## 1. Where the +665 went

| Hunk (new lines) | Δ words | Source | Judgement |
| --- | --- | --- | --- |
| Frontmatter source, status (L8, L24) | +2 | traceability | Keep. |
| Terms (L38) | +14 | findability for VAL-09 and VAL-10 terms | Keep. It points readers to release-kind, recovery-kind and recovery mode. |
| AD-3 *Changed workload* (L90) | +8 | VAL-03 | Keep. One clause and a pointer. This is the right pattern. |
| AD-6 *Admission records*, *Administration* (L118–119) | +58 | VAL-08 | Keep. It sets the authority and the recovery exception. |
| AD-7 *Credentials* (L128) | +10 | VAL-01 | Keep. |
| AD-8 *Dapr* (L142) | +11 | pointer (2) plus restatement (9) | Trim the restatement (PR3-5). |
| AD-14 *Surface*, *Negative tests* (L202, L206) | +29 | VAL-07 | Keep. The chained-request clause stops *Surface* from contradicting *Chains*. |
| Synthetic identities (L234) | +52 | VAL-05 | Keep (PR3-14 considered). |
| Release tiers table, paragraphs (L243–251) | +42 net | VAL-03 (+45), VAL-09 in-place union, Keycloak +2; a 48-word sentence moved | Keep the content. Fix the placement (PR3-1) and drop the lead-in (PR3-6). |
| R&R rows (L278–283, L287) | +276 | VAL-09 (+50), VAL-10 (+118), VAL-02 (+66), VAL-04 (+42) | Keep. Trim 12 words (PR3-2, PR3-9) and reorder one sentence (PR3-10). |
| DR intro, steps 2, 4, 5 (L313–319) | +90 | VAL-09 recovery mode, VAL-01, VAL-08; step 2 −14 dedupe | Keep the recovery-mode definition. Trim 31 words of restatement (PR3-3, PR3-4). |
| First shared versions (L441, L445) | +27 | VAL-05 pointer, VAL-01/06/09 | Trim the rationale clause (PR3-7). |
| Owned work (L466, L469, L472, L476) | +46 | VAL-03 qualification, VAL-10 staging bound, VAL-02 acceptance, VAL-08 G2 proof | Keep. Trim 11 words of row re-listing in L472 (PR3-8). |
| **Total** | **+665** | | **Autofix −77** |

## 2. Duplication map for the rules the brief named

| Rule | Definition home | Other mentions | Verdict |
| --- | --- | --- | --- |
| Recovery mode | DR intro L313 | Terms L38, In-place L281, step 2 L316, First shared versions L445, Owned work L472 | One definition. The other mentions are pointers. But L313 also restates the in-place data-restore procedure from L281 (PR3-3). |
| Admission records | AD-6 L118 | AD-6 *Administration* L119 (recovery exception), DR step 5 L319 (reconciliation semantics, single home), Keycloak note L251, G1 L288, Owned work L472, G2 L476 | Good split: authority in AD-6, procedure in step 5. G1's "the grant is recorded" is now a weaker duplicate (PR3-11). L472's "including admission records" repeats "signed records" (PR3-8). |
| Dapr activation | Release tiers L249 | AD-3 L90, AD-8 L142, tier table L243, Owned work L466 | One definition. The pointers are correct except AD-8, which restates the consequence (PR3-5). The paragraph also absorbed an unrelated rule (PR3-1). |
| Stop revision | Promotion stop L287 | Empty or degraded L280 | Two homes, and both are needed: the rule and its only exception. Not raised. |
| Timer kinds | Timing L283 | Terms L38, Owned work L472 ("by attempt kind") | One definition. Drop the Owned work qualifier (PR3-8). |
| Recovery-hook principal | DR step 4 L318 (endpoint), AD-7 L128 (holder), First shared versions L445 (contract) | none | Three homes, three different facts. Not raised. |
| Which executor owns which recovery | In-place L281, DR intro L313, AD-7 L127/L131, Binding classes L225 | DR step 4 L318 re-lists all three; In-place L281 repeats the recovery-executor sentence | Two re-listings (PR3-4, PR3-9). |

## 3. Findings

### A. Findability and misread risk

#### PR3-1: The operations-repository definitions rule is filed under "Dapr activation"
- **Severity:** medium. **Action:** autofix.
- **Evidence:** L249. The **Dapr activation.** paragraph opens with the VAL-03 restart rule, then continues with "Environment-layer and shared-infrastructure definitions … live in the operations repository; each attempt records their digest, and the pinned off-site recovery copy carries them …". Before this update, that sentence closed the environment-layer paragraph (now L247). It was restored as a regression fix in run 2 (RR ops-repo definition home). The diff moved it by accident when it shortened L247.
- **Why it matters:** DR step 2 ("from the off-site definitions") and AD-7's pinned off-site copy depend on this rule. An agent scanning bold labels for where definitions live finds only a sidecar-restart rule.
- **Old:** "in-place recovery leaves the applied union in place.\n\n**Dapr activation.** With HotReload off, a change … before readiness or qualification passes. Environment-layer and shared-infrastructure definitions — … — live in the operations repository; each attempt records their digest, and the pinned off-site recovery copy carries them at every retained recovery point's configuration identity."
- **New:** "in-place recovery leaves the applied union in place. Environment-layer and shared-infrastructure definitions — … — live in the operations repository; each attempt records their digest, and the pinned off-site recovery copy carries them at every retained recovery point's configuration identity.\n\n**Dapr activation.** With HotReload off, a change … before readiness or qualification passes."
- **Words:** 0 (move only).
- **Decision check:** None changed.

#### PR3-2: The quiesce step says "user admission", which collides with AD-6 production admission
- **Severity:** medium. **Action:** autofix.
- **Evidence:** L281: "It first quiesces — closes user admission to executor and probe sources and removes …".
  - The state being changed is Hosted interfaces' **user-ingress admission** on the Gateway (L233: "open, or executor and probe sources only").
  - Release modes (L279) and recovery mode (L313) call the same action "closes user ingress" and "user ingress closed".
  - In this spine, "admission" otherwise means AD-6 production admission, the Keycloak group (L117–118).
- **Why it matters:** An executor author could implement quiesce by emptying the human production-admission group. AD-6 says "recovery never grants admission", and only Administrator can grant it. So a deputy-run recovery could never reopen, which reverses the D5/VAL-09 intent. The replacement is also shorter.
- **Old:** "It first quiesces — closes user admission to executor and probe sources and removes the current release's workloads, keeping data objects —"
- **New:** "It first quiesces — closes user ingress and removes the current release's workloads, keeping data objects —"
- **Words:** −5.
- **Decision check:** VAL-09 "close user admission (executor and probe sources only; staging included)" is kept. "Closed" is defined in L233 as executor and probe sources only, and the row already covers staging.

#### PR3-10: The takeover-fencing invariant comes before the sentence saying who may take over
- **Severity:** low. **Action:** autofix.
- **Evidence:** L282. The new sentence "Once a takeover is accepted, no older-epoch mutation …" sits between the epoch sentence and "A production attempt runs from lock to terminal outcome in one job. Only an Administrator record, … may take it over, incrementing the epoch." The reader meets the consequence of a takeover before the definition of one.
- **Old:** "The lock carries a monotonic epoch checked before every mutation. Once a takeover is accepted, … otherwise it stops for intervention without mutation. A production attempt runs from lock to terminal outcome in one job. Only an Administrator record, … may take it over, incrementing the epoch."
- **New:** "The lock carries a monotonic epoch checked before every mutation. A production attempt runs from lock to terminal outcome in one job. Only an Administrator record, … may take it over, incrementing the epoch. Once a takeover is accepted, … otherwise it stops for intervention without mutation."
- **Words:** 0 (reorder).
- **Decision check:** VAL-02 is unchanged.

### B. Restatement and rationale introduced or exposed by this update

#### PR3-3: The DR intro restates the in-place data-restore procedure
- **Severity:** low. **Action:** autofix.
- **Evidence:** L313 says "an in-place data restore quiesces, deploys the recovery point's release in recovery mode and reuses steps 3–6 (In-place recovery)". L281 is the home and says the same thing in full ("It first quiesces … then deploys the recovery point's retained release by digest in recovery mode and runs DR steps 3–6"). The DR intro only needs to say which steps the in-place path reuses. It already has the pointer.
- **Old:** "an in-place data restore quiesces, deploys the recovery point's release in recovery mode and reuses steps 3–6 (In-place recovery)."
- **New:** "an in-place data restore reuses steps 3–6 (In-place recovery)."
- **Words:** −10.
- **Decision check:** VAL-09 quiesce and recovery-release deployment stay in L281. The recovery-mode definition stays at L313.

#### PR3-4: DR step 4 re-enumerates which executor owns each recovery kind
- **Severity:** low. **Action:** autofix.
- **Evidence:** L318 says "The executor owning the attempt — the recovery executor for DR, the production executor for production in-place recovery, the staging executor for a staging reset — invokes …". That mapping is already bound in four places:
  - In-place recovery L281 (production executor; "a staging reset runs it on the staging executor").
  - The DR intro L313 (recovery executor).
  - AD-7 L127 and L131.
  - The attempt-record writers in Binding classes L225.

  VAL-01's binding content is "only the executor owning the current recovery attempt … through that environment's scoped current-epoch recovery principal". The shortened sentence keeps it, together with the principal clause later in the same step.
- **Old:** "The executor owning the attempt — the recovery executor for DR, the production executor for production in-place recovery, the staging executor for a staging reset — invokes"
- **New:** "The executor owning the attempt invokes"
- **Words:** −21.
- **Decision check:** VAL-01 is kept ("owning the attempt"; "admits only that environment's recovery-hook principal at the current epoch"). If the recovery lens wants the list kept at the fault site, fall back to the pointer "The executor owning the attempt (Attempt ownership) invokes" (−19).

#### PR3-5: AD-8 restates the Dapr activation consequence
- **Severity:** low. **Action:** autofix.
- **Evidence:** L142: "and disable HotReload, so every Dapr resource change activates through sidecar restarts (Dapr activation);". The "so …" clause is the consequence, and its home is L249. AD-8 is the reach and isolation decision. The pointer is enough to keep the rule findable from AD-8.
- **Old:** "and disable HotReload, so every Dapr resource change activates through sidecar restarts (Dapr activation);"
- **New:** "and disable HotReload (Dapr activation);"
- **Words:** −9.
- **Decision check:** VAL-03 lives in full at L249 and L90. The memlog's "amendments land in AD-8" is met by the pointer.

#### PR3-6: The Dapr activation lead-in is rationale
- **Severity:** low. **Action:** autofix.
- **Evidence:** L249 opens with "With HotReload off," which is the reason for the rule. AD-8 binds HotReload off for every hosted Configuration, so the condition is always true.
- **Old:** "**Dapr activation.** With HotReload off, a change to the effective content"
- **New:** "**Dapr activation.** A change to the effective content"
- **Words:** −3.
- **Decision check:** VAL-03's rule, resource list, identity, attempt scope and "before readiness or qualification passes" are unchanged.

#### PR3-7: The recovery hook contract's Must-precede cell carries a rationale clause
- **Severity:** low. **Action:** autofix.
- **Evidence:** L445 says "First staging deployment, whose recovery points and resets consume it; the first AD-12 drill proves it". The "whose … consume it" clause explains the gate and belongs in the memlog, where VAL-06 records it ("earliest consumer: first staging recovery point and reset").
- **Old:** "First staging deployment, whose recovery points and resets consume it; the first AD-12 drill proves it"
- **New:** "First staging deployment; the first AD-12 drill proves it"
- **Words:** −7.
- **Decision check:** VAL-06 is kept: the gate is the first staging deployment and the first drill is still the proof boundary.

#### PR3-8: The First production attempt acceptance row re-lists new row qualifiers
- **Severity:** low. **Action:** autofix.
- **Evidence:** L472 appends "by attempt kind", "with quiesce and recovery mode" and ", including admission records" to items whose rows (Timing L283, In-place L281) or record class ("signed records", which covers Administrator records including AD-6 L118 admission records) already carry them. The acceptance boundary is "the row as written". Qualifiers repeated here drift when the row changes. The two acceptance items that are genuinely new stay: takeover fencing (explicitly added by VAL-02) and revision-conditioned clears.
- **Old:** "interruption, epoch and timing by attempt kind; takeover fencing or revoke-and-drain at every mutation authority; revision-conditioned stop clears; one recovery; in-place recovery entry point with quiesce and recovery mode; signed records, including admission records;"
- **New:** "interruption, epoch and timing; takeover fencing or revoke-and-drain at every mutation authority; revision-conditioned stop clears; one recovery; in-place recovery entry point; signed records;"
- **Words:** −11.
- **Decision check:** The VAL-02 acceptance item is kept. VAL-04, VAL-08, VAL-09 and VAL-10 stay bound in their rows, which "timing", "in-place recovery entry point" and "signed records" name.

#### PR3-9: The In-place row keeps a fourth home for "DR runs on the recovery executor"
- **Severity:** low. **Action:** autofix.
- **Evidence:** L281 ends "Replacement-capacity DR stays with the recovery executor." The same fact is in AD-7 *Executors* (L127: "runs only replacement-capacity recovery"), AD-12 *Model* (L183: "run by the AD-7 recovery executor") and the DR intro (L313). This update rewrote the row, so the orphaned boundary sentence can go now.
- **Old:** "and Administrator or the deputy reopens. Replacement-capacity DR stays with the recovery executor. |"
- **New:** "and Administrator or the deputy reopens. |"
- **Words:** −7.
- **Decision check:** D5 ("Replacement-capacity DR stays with the recovery executor") stays bound in AD-7 and the DR intro.

#### PR3-11: G1's "the grant is recorded" is now a weaker duplicate of AD-6 admission records
- **Severity:** low. **Action:** autofix.
- **Evidence:** L288 says "the grant is recorded and its revocation followed by a denial check". AD-6 L118 now binds the stronger rule: the G1 SM-4 temporary grant, and its revocation, is first written as a signed Administrator admission record, then applied in Keycloak. Two wordings of the same obligation invite an implementation that only logs the grant.
- **Old:** "the grant is recorded and its revocation followed by a denial check;"
- **New:** "its revocation is followed by a denial check;"
- **Words:** −4.
- **Decision check:** VAL-08 explicitly names the G1 SM-4 grant in AD-6 L118. G1 keeps the denial check.

### C. Discuss

#### PR3-12: DR step 1 still disables workers that recovery mode now keeps disabled
- **Severity:** low. **Action:** discuss.
- **Evidence:** L315 (step 1, Fence) ends "Disable external-effect and destructive retention workers." Before this update, step 2 repeated this ("with user ingress closed and workers disabled"). Now recovery mode (L313) covers it for the restored release, and Lost window (L294) owns the re-enable condition. At step 1 nothing is deployed on the prepared capacity yet, so the sentence is either vestigial or about the old instance, which the text does not say.
- **Options:**
  - (a) Drop the sentence (−6) if it meant the restored release.
  - (b) Scope it: "Disable the old instance's external-effect and destructive-retention workers where still reachable." (+4) if it is a fence action.
- **Recommendation:** (a). The old node is already "isolated from network and DNS", so its workers cannot reach providers.
- **Decision check:** VAL-09's recovery mode already carries the rule.

### D. Defer

#### PR3-13: Three R&R cells are now long enough to hurt scanning
- **Severity:** low. **Action:** defer. **Trigger:** the next amendment to any of these three rows.
- **Evidence:** Timing and interruption (L283) is 233 words, Attempt ownership (L282) 197 and In-place recovery (L281) 172. Timing now holds three subjects: grace, release-kind and recovery-kind.
- **Proposal:** when Timing next changes, split it into "Timing: release-kind" and "Timing: recovery-kind" rows, keeping the shared grace and interruption sentences in the first. Terms L38 would then point at both. There is no word saving, and doing it now would churn a draft under review.

### E. Ignore (considered and kept)

#### PR3-14: Synthetic identities, "and the production predicate has no such clause"
- **Severity:** low. **Action:** ignore.
- **Evidence:** L234. This looks redundant with "in staging only". But the memlog VAL-05 line binds "the production predicate and realm carry no such clause", which means the clause is absent from the production configuration, not disabled at runtime. That is security-bearing, so keep it (8 words).

#### PR3-15: AD-14 *Surface*, "validates its own audience"
- **Severity:** low. **Action:** ignore.
- **Evidence:** L202 overlaps AD-6 *Validation* (L116, "validates issuer, audiences …"). VAL-07 wording pairs audience validation with "Neither the target audience … establish the surface". The five words stop an implementer treating audience as validated-and-classifying. Keep.

#### PR3-16: Promotion stop, "applied by compare-and-set … so a later set always prevails"
- **Severity:** low. **Action:** ignore.
- **Evidence:** L287. Compare-and-set is mechanism and "a later set always prevails" is the invariant. VAL-04 binds both ("applied by CAS only while the revision is unchanged; any later set dominates"). The encoding is correctly left to Builds. Keep.

## 4. Achievable net

| Bucket | Words |
| --- | --- |
| Autofix: PR3-1 (0), PR3-2 (−5), PR3-3 (−10), PR3-4 (−21), PR3-5 (−9), PR3-6 (−3), PR3-7 (−7), PR3-8 (−11), PR3-9 (−7), PR3-10 (0), PR3-11 (−4) | **−77** (verified by applying to a scratch copy: 15,005 to 14,928) |
| Discuss: PR3-12 option (a) | −6 more |
| **Achievable net** | **about −83, to roughly 14,922 words; the update stays at about +580** |

Cutting further would remove VAL decision text: the timing lifetimes, the takeover invariant, the recovery-mode list, the admission-record authority or the staging lifecycle clause. This review may not propose that.

## Considered and not raised

- **Terms additions (+14).** These pointers carry the new vocabulary and are the cheapest findability gain in the update.
- **The recovery-mode list (L313).** Each clause is a render or admission flag that the renderer and the recovery executor must implement the same way. "No Subscriptions rendered until step 4" is the VAL-09 fix for consumers draining the backlog before hooks run.
- **Timing lifetimes per recovery kind (L283).** These are decision values tied to the PRD RTO and to the Administrator record. They are not seed.
- **Attempt ownership fencing sentence length (L282).** It carries exactly the VAL-02 invariant, the two per-authority options and the stop. The seed mechanism (Secret-bound TokenRequest) is correctly kept out.
- **Release tiers "in-place recovery leaves the applied union in place" (L247) and In-place "restores no … environment layer" (L281).** They overlap, but the tier sentence also covers the baseline re-deploy branch, so both stay.
- **Release modes "and maximum duration" (L279) and Timing "the duration its Administrator record states" (L283).** One sets the record's content and the other its use. Both are needed.
- **The DR step 5 reconciliation sentence together with "Re-apply admin and user revocations recorded after the cut".** They cover different sources (admission records compared with the event export). Keep both.
- **Correctness observations for other lenses, outside the bloat scope:**
  - AD-7 *Credentials* scopes per-job credentials to "its own environment", which reads awkwardly for the recovery executor's recovery-hook principal.
  - In-place "Verification is the same as Automatic recovery" sits beside DR step 6's verify, so the two verification sets should be stated as cumulative.

## Suggested order

1. **Apply the misread fixes first:** PR3-2, then PR3-1 and PR3-10.
2. **Apply the restatement trims:** PR3-3, PR3-4, PR3-5, PR3-6, PR3-7, PR3-8, PR3-9 and PR3-11.
3. **Settle PR3-12** in one line with the recovery lens.
4. **Hold PR3-13** until the next edit to Timing.
