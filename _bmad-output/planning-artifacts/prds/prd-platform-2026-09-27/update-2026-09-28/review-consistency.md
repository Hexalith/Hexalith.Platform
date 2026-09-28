# Cross-document consistency review — PRD update 2026-09-28

- **Reviewed:** `prds/prd-platform-2026-09-27/prd.md` (397 lines), `prds/prd-platform-2026-09-27/addendum.md` (355 lines), both `updated: 2026-09-28`. The reference was `architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md` (487 lines, frontmatter `status: draft`, run-3 confirmation pending). Downstream, `_bmad-output/specs/spec-platform/` (SPEC.md plus companions) was also checked.
- **Scope:** only this report was written. No other document was edited.
- **Line references:** 1-based, written `prd:N`, `add:N`, `spine:N`, `spec:N` (SPEC.md), `ac:N` (acceptance-criteria.md), `gl:N` (glossary.md), `sm:N` (success-measures.md), `seq:N` (sequencing.md).

## Verdict

**Consistent, with minor fixes.** Nothing blocks. The PRD and addendum agree on every rule the update introduced: release modes, empty/degraded approval scope, incompatible-release recovery, reduced-recovery state, revocation exceptions, deputy authority, the SM-4 grant, and the FR-4 attachment and CI-evidence rules. Two medium findings remain:

- **CON-01:** the promotion-stop trigger list in the PRD glossary and the addendum lags FR-8 and the spine.
- **CON-02:** the addendum puts EventStore G3 confirmations before the first production attempt.

The remaining findings are low-severity wording, completeness and terminology items.

| Severity | Count |
| --- | --- |
| High | 0 |
| Medium | 2 |
| Low | 12 |
| Not a defect: update-summary link, to be created at close | 1 |
| Expected spec follow-up, not a defect | 7 groups |

## Findings

### CON-01 — Medium — Promotion-stop triggers and clear rule stated three different ways

- **Locations:** prd:207 (authoritative list), prd:384 (glossary), add:212; spine:288
- **Issue:**
  - **FR-8 (prd:207) and the spine (spine:288).** The stop is set by *every recovery entry (automatic, in-place or disaster recovery)* and by an availability-probe failure beyond its bound. Promotion stays suspended during any recovery.
  - **Addendum (add:212).** It lists only "disaster recovery entry" and omits "promotion stays suspended during any recovery".
  - **PRD glossary (prd:384).** It also narrows recovery entry to "disaster recovery" and drops the probe-failure trigger. It says Administrator clears the stop "after verified recovery", whereas FR-8 requires a record naming the reason, the observed stop state and *a verified current working release*. A stop raised by an incident or an admission mismatch has no "recovery" to verify.
- **Fix:**
  - **add:212:** change "disaster recovery entry" to "every recovery entry (automatic, in-place or disaster recovery)", and add "promotion stays suspended during any recovery".
  - **prd:384:** replace the glossary entry with: "the durable block on further production promotion set by a non-working outcome, any recovery entry, a failed pre-update health check, a probe failure beyond its bound, a recorded incident, an unmatched admission change or a declared stop. Only an Administrator record naming a verified current working release clears it, except that a verified approved empty or degraded attempt clears only the stop Administrator observed."

### CON-02 — Medium — EventStore confirmations gate timing differs from the spine

- **Locations:** add:351; spine:474, spine:330; prd:351
- **Issue:**
  - **Addendum (add:351).** It requires EventStore "retention/activation/lifecycle confirmations" *before the first applicable production attempt, including an approved pre-G3 attempt*.
  - **Spine (spine:474).** The EventStore confirmations of AD-15 retention entries, catalog activation order, expected-generation commit, the recovery-point forward generation and production-promoted invalidation and renewal *additionally gate G3*. Spine:330 calls them "the G3 confirmations".
  - **Lifecycle part.** Only this part, AD-13 ratification and release-available, is needed earlier: through precondition 5 (spine:306) and First shared versions (spine:438).
  - **PRD (prd:351).** It follows the spine's boundary.
  - **Effect.** The addendum claims to summarize the accepted architecture, but it imposes an earlier, stricter gate.
- **Fix:**
  - **Split the add:351 boundary:** "Before the first applicable production attempt: AD-13 ratification and release-available lifecycle, provenance, one recovery attempt, concurrency/interruption controls and GitHub delivery. EventStore retention/activation confirmations and SM-5 qualification additionally gate G3."
  - **If the stricter timing is intended:** label it as a PRD-side requirement and route it to the architecture owner.

### CON-03 — Low — Addendum misstates the release tiers

- **Locations:** add:169; spine:243–246, spine:88
- **Issue:**
  - **Addendum (add:169).** It says "Data services, persistent state, shared infrastructure and credential rotation belong to a separately versioned, forward-only environment layer".
  - **Spine.** It has three distinct tiers beside the application package:
    - **Environment layer:** forward only.
    - **Shared infrastructure:** its own tier, under both locks, reverted forward or by a DR entry.
    - **Outside every release:** business data and credential rotation.
- **Fix:** "Data services and other environment-layer objects are applied forward-only in a separately versioned environment layer. Shared infrastructure is its own tier, changed under both environment locks. Business data and credential rotation sit outside every release. Application rollback touches none of them."

### CON-04 — Low — "An off-site executor runs recovery" overstates the recovery executor's role

- **Locations:** add:330; add:230; spine:127, spine:131, spine:282
- **Issue:** The off-site recovery executor runs only replacement-capacity recovery. In-place recovery runs on the production executor, and add:230 itself mentions in-place recovery.
- **Fix:** "An off-site recovery executor runs replacement-capacity recovery. In-place recovery runs on the production executor, started by Administrator or the deputy."

### CON-05 — Low — Addendum drill triggers omit recovery-mechanism changes

- **Locations:** add:258; prd:235, prd:328; spine:293
- **Issue:** The addendum repeats the drill only "after material changes to storage or backup behavior". The PRD's FR-9 and SM-6, and the spine, also repeat it after recovery-mechanism changes.
- **Fix:** add "or recovery mechanisms" at add:258.

### CON-06 — Low — Addendum G2 evidence row is incomplete

- **Locations:** add:187; prd:53; spine:289
- **Issue:** The PRD G2 row and the spine both require verified recovery access, prepared capacity and response coverage at G2. The addendum's "Entry gate / Evidence required" row omits them. They appear only in its qualification table (add:347, add:349).
- **Fix:** append to add:187: "recovery access, prepared capacity and response coverage verified".

### CON-07 — Low — Recovery-point precondition for shared-infrastructure changes reads as urgent-patch-only

- **Locations:** add:182; prd:172; spine:245
- **Issue:** The PRD and spine require a complete recovery point before *every* shared-infrastructure change. The addendum attaches "after a complete recovery point" only to the urgent-security-patch path. It also does not say that the change runs under both locks and re-runs working-release smokes and NFR-3 checks.
- **Fix:** "Every shared change runs under both locks after a complete recovery point and re-verifies each environment. From G1 it is first rehearsed on a production-profile copy in the monthly drill, except an urgent security patch applied in place with an Administrator record."

### CON-08 — Low — Addendum's "no change" retention case is narrower than AD-3's changed-workload definition

- **Locations:** add:210; spine:90; prd:204
- **Issue:** The addendum retains the current release when "no release-owned object changed and no catalog generation was committed". AD-3 also counts a workload as changed when a Dapr resource it consumes changed (Dapr activation). The PRD's capability-level wording, "neither workloads nor routing changed", is fine.
- **Fix:** add "and no consumed Dapr resource changed" at add:210.

### CON-09 — Low — Labelling of the PRD-only FR-4 requirements is incomplete in two places

- **Locations:** prd:347; prd:340, prd:355; add:34, add:54, add:343; prd:135; spine:163
- **Issue:**
  - **Where the labelling is correct.** The CI candidate-artifact identity and the attachment-holds-environment rules are labelled as not yet adopted at prd:340, prd:355, add:34 and add:54.
  - **prd:347.** It still says "Implement and qualify the *selected* … attachment lifetime, CI candidate-artifact identity", which reads as if the architecture had selected them.
  - **Addendum qualification table (add:343).** It has no adoption item, and its lifecycle row does not mention these rules.
  - **Listing extension.** "Retained environments show … attached runs" (prd:135) extends AD-10's listing (owner and age only, spine:163). The prd:355 adoption row covers it only implicitly.
- **Fix:**
  - **prd:347:** "Implement and qualify the selected root-source mapping, declaration/export contracts, Platform runner ownership and disposable CI lifecycle, and, once adopted (row below), the FR-4 attachment lifetime and CI candidate-artifact identity".
  - **add:343:** add "after AD-4/AD-5/AD-10 adopt the FR-4 additions".
  - **prd:355:** name "attached-run listing" in the row.

### CON-10 — Low — SM-4 temporary grant: group and naming are unclear

- **Locations:** prd:52, prd:56, prd:326; add:186, add:190; spine:289, spine:235
- **Issue:**
  - **Spine.** The spine adds the designated synthetic identity to the *human* production-admission group. The G1 condition "human group is empty" is therefore deliberately broken between G1 and G2, and the grant is distinct from the standing synthetic-admission group.
  - **PRD and addendum.** They say only that Administrator "admits" a synthetic test identity. The G1 row adds that synthetic identities use "separate Administrator-granted access", which a reader may apply to the SM-4 grant too.
  - **SM-4 naming.** SM-4 calls the same identity "a controlled explicitly admitted test user".
- **Fix:**
  - **prd:56 and add:190:** state "the temporary grant is membership in the human production-admission group, distinct from the standing synthetic-admission access".
  - **prd:326:** use "designated synthetic test identity" instead of "test user".

### CON-11 — Low — Terminology drift across the two documents

- **Locations:** prd:209, prd:385, add:198 against add:180, spine:281; prd:42, prd:255, prd:260, prd:264 against prd:52, prd:232–233, prd:263, add:328; spine:296
- **Issue:**
  - **(a) Empty or degraded production.** It is defined as "no working *release*" at prd:209, prd:385 and add:198, but as "no working *baseline*" at add:180 and in the spine.
  - **(b) Production access.** The PRD uses "production-user declaration" in some places and "production admission" in others. The glossary defines only "Production user".
  - **(c) Admission group name.** add:328 says "named production admission group" where the rest of the documents say "human production-admission group".
  - **(d) Reduced-recovery term.** The PRD and addendum say "reduced-recovery state"; the spine says "posture". The two are consistent between the PRD and the addendum and acceptable; no change is needed.
- **Fix:**
  - **(a):** use "no working baseline" in all three places, or state once that the two are equivalent.
  - **(b):** add a glossary entry: "Production admission: Administrator's explicit declaration that makes a user a production user".
  - **(c):** write "human production-admission group" at add:328.

### CON-12 — Low — Superseded historical McpCli and AD-4 statements are not marked

- **Locations:** add:20, add:22, add:34; spine:100–102, spine:176; `.gitmodules:28`; seq:215–217
- **Issue:**
  - **add:20.** It still gives the sibling `../mcpcli` as McpCli's canonical location and says it is absent from Platform declarations. Platform now declares `references/Hexalith.McpCli`, and AD-4 forbids sibling resolution. add:10 labels repository observations as historical, but this one is not marked superseded.
  - **add:22.** It says EventStore Admin and non-gateway capabilities need "a generic McpCli extension decision". The spine requires a Platform AD admitting them under AD-14.
  - **add:34.** It omits package mode's requirement to build at a Platform release tag; an untagged or dirty submodule is source-mode only.
  - **Spec.** seq:215–217 already lists these lines as stale.
- **Fix:**
  - **add:20:** add "Superseded: Platform now declares `references/Hexalith.McpCli`; sibling paths are never a resolution path (AD-4)".
  - **add:22:** reword to "a Platform AD admitting them under AD-14 (AD-11 New surfaces)".
  - **add:34:** add "at a Platform release tag (package mode)".

### CON-13 — Low — Architecture gate citation and status label

- **Locations:** add:10; prd:340; spine:8; `architecture/.../reviews/update-2026-09-28/gate-summary.md`; seq:214
- **Issue:**
  - **Gate citation.** Both documents cite only the 2026-09-27 update gate. The 2026-09-28 run-2 gate (`reviews/update-2026-09-28/gate-summary.md`) also passed and is the latest completed gate.
  - **Status label.** The spine frontmatter is now `status: draft` pending the run-3 confirmation, while both documents call it the "accepted architecture". The hedge at prd:340 and add:10 prevents a factual error.
  - **Spec.** It flags the add:10 link as stale.
- **Fix:** cite both gate summaries, and add once "(spine status `draft` pending run-3 confirmation; its adopted decisions are user-accepted)".

### CON-14 — Low — The addendum carries only half of the open degraded-path question

- **Locations:** prd:352; add:198; spine:281
- **Issue:** The PRD's open question has two parts:
  - whether stops on a verified baseline (incident, probe failure or failed pre-update health check) qualify for the degraded path;
  - whether a degraded *non-empty* approval also needs compatibility evidence or a named recovery.

  add:198 carries only the first part. The spine fixes the degraded rollback set as "remove workloads, keep data" but is silent on compatibility evidence.
- **Fix:** add to add:198: "and whether a degraded non-empty approval also needs compatibility evidence or a named recovery (PRD downstream table)".

### Not a defect — update summary link

- **Location:** prd:16.
- **Detail:** `update-2026-09-28/update-summary.md` does not exist yet. It is created at close.

## Check results

### 1. PRD and addendum terms and rules

- **Consistent:**
  - working baseline and working release (prd:382–383; add:171, add:212);
  - empty or degraded production scope: one attempt, lifts only the observed stop and the healthy-baseline check, a later stop prevails, failure removes workloads and keeps data, success becomes the baseline (prd:209; add:180);
  - reduced-recovery state is not degraded production and has no emergency path (prd:209, prd:236; add:180);
  - recovery point (prd:229, prd:390; add:257);
  - enrolled and enabled modules (prd:46, prd:371–372; add:22, add:24);
  - production user (FR-11);
  - recovery deputy: alerts, independent access, restore, verify and reopen, declare but never clear a stop, no release approval, no admission administration (prd:34, prd:208, prd:389; add:223, add:230, add:347).
- **Also consistent:**
  - the approved incompatible release: recovery point first, stop, ingress closed, workloads removed, data restored to the point (prd:210; add:179);
  - the revocation exceptions (prd:233; add:280);
  - the attachment lifecycle (prd:136–138; add:54, add:104–105).
- **Exceptions:** CON-01, CON-10, CON-11 and CON-14.

### 2. ID references

- **Resolution.** All IDs resolve: FR-1..FR-12, NFR-1..NFR-3, SM-1..SM-6, SM-C1..SM-C5, G1..G3, and AD-1, 2, 4, 5, 10, 11, 12, 14 and 15.
- **Semantic check.** Every cross-reference was checked for correct use, for example:
  - FR-8 for the empty/degraded path (prd:48, prd:171);
  - FR-6 for the named recovery (prd:200, prd:294);
  - FR-7's healthy-baseline check (prd:209);
  - FR-9's lost-window exceptions (prd:210);
  - the "Validates" and "Counterbalances" targets of each SM;
  - AD numbers against spine headings.
- **Result.** No reference is misapplied.

### 3. Addendum claims about the architecture

These addendum statements match the spine:

- AD-11 and AD-14: McpCli, surfaces, legacy exclusion inside every Platform composition;
- AD-4 identity checks;
- AD-5 and AD-10 run ownership;
- AD-1 and AD-2 retained package and retention;
- release modes, incompatible release, empty/degraded path, staging supersession;
- G1, G2 and G3, and the SM-4 grant;
- the failure thresholds;
- the lock and records;
- AD-15;
- the promotion-stop clear rule;
- deputy controls;
- AD-12 order and exceptions;
- the hosted-decision table;
- accepted risks.

The PRD-only FR-4 requirements are labelled as awaiting adoption at prd:340, prd:355, add:34 and add:54. The degraded-path open question is labelled at prd:352 and add:198.

- **Exceptions:** CON-02 to CON-08, CON-09, CON-12 and CON-13.
- **Observation only, not a PRD defect.** The spine's own Promotion stop row (spine:288) omits the admission-mismatch trigger, which appears only in Diagnostics and notification (spine:236). The PRD's claim is still backed by the spine.

### 4. Local links and anchors

- **Result.** 17 local links were checked (10 in prd.md, 7 in addendum.md), plus 3 anchors: `#module-owned-configuration-example`, `#mcpcli-context` and `#hosted-architecture-questions`.
- **Missing.** Only `update-2026-09-28/update-summary.md`, which is expected (see above).

### 5. Stale pre-update text

None of the four named patterns remains in prd.md or addendum.md:

- **Operations repository writers:** now "only writers are the named writers" and "two named writers" (add:330–331, add:353).
- **Revocation re-application:** it now carries admission reconciliation and the two exceptions (prd:232–233; add:278, add:280).
- **Healthy baseline:** it now has the empty/degraded exception (prd:188, prd:209, prd:385; add:180, add:198).
- **"Admission group is empty":** both occurrences say "human" (prd:52; add:186).

Older superseded text outside these patterns is reported in CON-12 and CON-13.

## Spec follow-up (expected, not defects)

`_bmad-output/specs/spec-platform/SPEC.md` and its companions exist. They predate this PRD update and call the spine "final" (spec:23), while the spine is now `draft`. prd:356 already assigns spec synchronization to the spec owners. The conflicts with the updated PRD are:

1. **Release modes.**
   - ac:97 lets approval permit only "a pre-G3 release or a separately planned incompatible release". It omits the empty/degraded attempt and the maximum duration.
   - ac:96–98 omits the shared-infrastructure currency block.
   - spec:85 omits the maximum duration.
   - ac:156 omits the approved empty/degraded attempt from the paths that become the working baseline.
   - sm:14 (SM-5) omits the degraded-attempt scenarios (clears only the observed stop, a later stop still blocks, the deputy cannot approve) and the closing of user ingress on a failed incompatible release.
2. **Promotion stop.**
   - ac:149 and gl:45 list only "non-working outcome and disaster recovery entry" as triggers.
   - The clear record there does not name the observed stop state, and there is no later-stop-prevails rule and no empty/degraded exception.
   - spec:52 (CAP-8) and spec:87 say "only an Administrator record clears" with no exception.
3. **Revocation during restore.**
   - ac:190 still says "re-apply post-cut revocations". It has no admission reconciliation to Administrator records and no guarantee that admission revocations survive.
   - ac:193 lists module-owned revocations but not the identity-provider non-admission removal exception.
   - sm:15 (SM-6) lacks denial of an admission revoked just before failure, recording of the exceptions and the recovery-mechanism trigger.
   - spec:91 (NFR-2) lacks the revocation protections.
4. **Deputy authority.**
   - gl:48 and spec:70 omit that the deputy may declare a stop and cannot approve releases.
   - ac:149 omits the deputy's stop declaration and the prohibition on approving releases.
5. **Attachment.** spec:78–79 and ac:53–62 lack the attachment-holds-environment rule:
   - the owner's first outcome is recorded;
   - automatic cleanup is deferred;
   - an explicit stop or cancellation ends attached runs first;
   - CI cleanup is deferred, never skipped;
   - retained environments list their attached runs.
6. **CI evidence.** ac:39–41 and sm:12 (SM-3) lack:
   - the candidate's Release artifact and source-revision identity;
   - refusal of evidence for another artifact or revision;
   - labelling of baseline-only runs.
7. **Other drift.**
   - spec:97 and seq:37 omit the SM-4 admission record's expiry no later than G2. sm:13 (SM-4) also omits the expiry.
   - spec:98 (G2) omits the G1 grant's revocation record and denial check.
   - seq:211–222 lists addendum lines as stale. Several of them are now fixed: notification recipients, the architecture follow-up, the single-writer repository and the deputy amendment.
