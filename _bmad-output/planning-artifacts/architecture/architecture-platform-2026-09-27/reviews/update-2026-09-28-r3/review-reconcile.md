# Input reconciliation review — update run 3 (2026-09-28)

**Verdict: PASS WITH CORRECTIONS.** All ten VAL items landed in the spine; one (VAL-09) is partial because one decided clause is missing. No PRD, addendum or sprint-change-proposal requirement was dropped or weakened. There are 15 defect findings: 1 medium and 14 low. None reverses a memlog decision. The medium item is a hole in the in-place data restore that the VAL-09 rationale already names: the live broker's post-cut backlog.

- Target: `ARCHITECTURE-SPINE.md` (485 lines, status draft), SHA-256 `eb026c85d6361fa01b2a06dcf28a69c26b603f2bc51f3443b3428089be28e374`.
- Diff: `reviews/update-2026-09-28-r3/spine.diff`.
- Decisions: `.memlog.md` lines 243–253.
- Findings being closed: `reviews/validate-2026-09-28/validation-report.md` and `findings.json`.
- Inputs: `prds/prd-platform-2026-09-27/prd.md` and `addendum.md`, `sprint-change-proposal-2026-09-27.md`.
- Spec companion: `_bmad-output/specs/spec-platform/*.md`.
- Nothing except this file was modified. Line numbers refer to the current spine unless another file is named.

## (a) VAL item landing against memlog decisions

| VAL | Memlog decision (line) | Status | Spine evidence | Notes |
| --- | --- | --- | --- | --- |
| VAL-01 | 244: the hook endpoint admits only the executor that owns the recovery attempt, through the environment's current-epoch recovery principal; hooks still run in the module workload | **Landed** | 318 (step 4 owning-executor mapping; endpoint admits only that environment's recovery-hook principal at the current epoch); 128 (AD-7 per-job recovery-hook principal for an owned recovery attempt); 445 (contract row names the principal) | The recovery executor's credential enumeration at 131 was not updated (RC3-4). Replacing network reachability with a principal check adds no NFR-3 negative case (RC3-5). |
| VAL-02 | 248: no older-epoch mutation, including one in flight, commits at any mutation authority. The new owner uses an authority-side fence, or revokes the previous job's credentials and drains. Otherwise it stops. Acceptance in First production attempt | **Landed** | 282 (invariant, both branches, stop otherwise); 472 ("takeover fencing or revoke-and-drain at every mutation authority") | Memlog lists authorities; the spine says "any mutation authority", which is equivalent. The owner-chosen mechanism and TokenRequest seed example are not carried; acceptable. Edge cases: RC3-10. |
| VAL-03 | 244: any changed effective content or binding of any listed Dapr resource restarts every consuming sidecar in the owning attempt before readiness or qualification; consumers count as changed; qualify policy-only and HTTPEndpoint-only updates | **Landed** | 249 (Dapr activation, all seven kinds, within the owning attempt); 90 (AD-3 changed workload); 142 (AD-8); 243 (env-layer row); 466 (qualification) | The Dapr activation paragraph now also carries an unrelated operations-repository sentence (RC3-13). Activation of Subscriptions rendered late in recovery mode is unstated (RC3-3). |
| VAL-04 | 249: monotonic stop revision; clear names the observed revision and working attempt and is applied by CAS; a later set dominates; the E/D override is bound to one attempt and its observed revision | **Landed** | 287 (revision, CAS clear, later set prevails); 280 (override names observed revision, "for that attempt only, never a set recorded after that revision") | The attempt ID is bound implicitly through "for that attempt only". Spine wording "verified current working attempt" refines the PRD's "working release"; it is stricter, not weaker. |
| VAL-05 | 251: staging-only predicate clause for marker-bearing creation and for tenants whose attested creation names the same synthetic client; the marker alone never admits; the production predicate and realm carry no clause; no run binding | **Landed** | 234 (Synthetic identities clause); 441 (predicate row cites the clause) | "realm" is dropped, and a "For that flow" condition was added that admission cannot evaluate (RC3-12). The row's Must-precede misses the new staging consumer (RC3-11). |
| VAL-06 | 244: the recovery hook contract must precede the first staging deployment; the first AD-12 drill is the production proof | **Landed** | 445 | Exact. |
| VAL-07 | 244: remove audience-only classification; every bearer endpoint validates audience and derives the surface from azp, or on chains from the attested originating surface; add a confidential-audience negative test | **Landed** | 202 (audience validated; surface from azp or attested chain surface; audience never establishes surface); 206 (negative test) | The scope sentence still says "externally reachable host", while the decision says "every bearer endpoint". The Chains bullet (205) covers internal hops, so coverage holds, but the wording is narrower (RC3-6). |
| VAL-08 | 250: every production admission grant or revocation, including SM-4, is first a signed Administrator record in the **off-site** record store, then applied in Keycloak. The records are the recovery authority. DR step 5 and a whole-server restore reconcile membership, removing only. The deputy can reopen. Qualify a revocation lost before event export | **Landed** | 118 (Admission records); 119 (Administration: reconcile); 319 (step 5 reconciliation); 251 (whole-server restore); 476 (G2 proof); 472 (admission records in First production attempt) | The spine places the records in "the attempt, lock and stop store", whose stated placement (225) is only "outside the target cluster and executor hosts". The decision's "off-site" durability is implicit, not stated (RC3-7). Adjacent residual: RC3-15. |
| VAL-09 | 245: two branches. Baseline re-deploy is a release-kind Helm upgrade. Data restore quiesces, then deploys the recovery point's release by digest **with environment-current values** in recovery mode. Recovery mode is defined once and shared with DR step 2. No OpenBao, Keycloak or env-layer restore, no fence or cutover. Administrator or deputy reopens and the stop stays set | **Partial** | 281 (both branches, quiesce, recovery release, steps 3–6, exclusions, reopen); 313 (recovery mode defined once); 316 (DR step 2 uses it); 278 (staging reset is a data restore); 247 (in-place keeps the applied union) | The "with environment-current values" clause did not land. The DR analog at 316 uses the recovery point's recorded configuration, which leaves the in-place value source ambiguous (RC3-2). The broker backlog and the env-layer boundary sit beyond the decision text but defeat its stated aim (RC3-1). "until step 6 passes" (313) goes beyond the memlog; it matches the VAL-09 recommendation and is accepted. |
| VAL-10 | 246: release-kind vs recovery-kind timers; recovery-kind persists kind, entry, outage and one lifetime (DR = outage + 4 h in coverage, else last drill; named recovery = duration in the Administrator record; staging reset = declared bound); phase deadlines; takeover inherits; monitor uses the recorded lifetime | **Landed** | 283 (full split); 279 (Administrator record states maximum duration); 469 (staging-reset maximum duration); 282 (monitor uses the recorded maximum lifetime); 38 (Terms) | Duration-type lifetimes have no anchor (RC3-9). The Binding classes attempt-bound list was not extended (RC3-8). |

**Tally:** 9 landed, 1 partial (VAL-09), 0 missing, 0 contradicted.

No spine text contradicts a memlog decision. Two edits go beyond the decisions, and both are acceptable:

- "until step 6 passes" in Recovery mode (313), taken from the VAL-09 recommendation.
- "For that flow" in Synthetic identities (234). It needs rewording (RC3-12).

## (b) PRD, addendum and sprint change proposal

No quiet drop and no weakening were found. Each check:

- **Revocation reapplication** (prd.md:226; addendum 248, 273): strengthened. DR step 5 (319) now reconciles admission to signed records before it re-applies exported revocations. Denial of revoked principals is still checked in step 6 (320). The G2 row (476) proves a revocation lost before event export. Adjacent residual: realm revocations other than admission inside the export lag (RC3-15, pre-existing).
- **Deputy authority** (prd.md:34, 204, 377; addendum 218, 222): preserved.
  - The deputy starts in-place recovery and DR, and reopens after a data restore (281) and after DR (321).
  - Reconciliation only removes access, so no Administrator step blocks the deputy's reopening (119, 319).
  - Only Administrator clears the stop (287) or issues an Empty or degraded override (280).
  - Admission records are Administrator-signed (118). The deputy gains no admission authority.
- **Admission by Administrator only** (prd.md:255; addendum 323): strengthened. Every grant and revocation is a signed Administrator record before it reaches Keycloak (118). "Recovery never grants admission or roles" (119) is intact. The new staging-only predicate clause (234) touches neither production nor the production realm.
- **Staging gate** (prd.md:157–169): unchanged (277). A staging reset is now a full data restore (278, 281) with a declared maximum duration (469). It serializes as its own staging attempt and weakens no evidence rule.
- **RTO/RPO** (prd.md:296–301; addendum 237, 293): unchanged.
  - The DR maximum lifetime is outage time + 4 h within coverage (283), exactly the PRD RTO, and serves only stale-attempt notification.
  - Outside coverage there is no commitment, matching PRD 301.
  - "Timers never restart" keeps the outage clock unpaused.
- **Deployment and verification thresholds** (FR-7/FR-8): unchanged for release-kind attempts, including baseline re-deploy (283). The recovery-release readiness deadline reuses the 10-minute budget.
- **Promotion-stop clearing** (prd.md:204): stricter. A clear now needs the observed revision, applied by CAS (287).
- **Surface enforcement** (prd.md:275): strengthened (202, 206).
- **Sprint change proposal** (McpCli replaces proprietary MCP/CLI): no conflict. McpCli remains the `agent` class (203), confirmation stays UI-only, and no new surface or client is introduced.

## (c) Spec-companion passages that now diverge (informational, for the bmad-spec refresh)

"Stale" means the text now disagrees with the spine or is out of date. "Incomplete" means the text is still true but lacks a new spine rule that the spec should carry.

| # | File:line | Kind | What changed in the spine |
| --- | --- | --- | --- |
| 1 | SPEC.md:24 | Stale | Cites the spine as "final, updated 2026-09-28". The spine is now `status: draft` under update run 3; refresh after finalization. |
| 2 | SPEC.md:83 | Incomplete | Identity: add AD-6 Admission records. Every production grant or revocation is a signed Administrator record written before Keycloak, and those records are the recovery authority. |
| 3 | SPEC.md:85 | Stale | Release modes: the record now names the planned recovery "with its acceptance checks and maximum duration" (RRA Release modes). |
| 4 | SPEC.md:87 | Incomplete | Promotion stop: a clear names the observed revision and a verified working attempt, and is applied by CAS; a later set prevails. |
| 5 | SPEC.md:88 | Stale | In-place recovery now has two branches: baseline re-deploy, and data restore (quiesce, recovery release in recovery mode, DR steps 3–6, no Keycloak/OpenBao/env-layer restore). A staging reset runs on the staging executor. Administrator or the deputy reopens. |
| 6 | acceptance-criteria.md:92 | Stale | A staging reset is an in-place data restore (quiesce, recovery mode) with a declared staging bound. |
| 7 | acceptance-criteria.md:98 | Stale | Approval names the recovery procedure, its acceptance checks and its maximum duration. |
| 8 | acceptance-criteria.md:99 | Incomplete | Attempt ownership adds the takeover fencing invariant: no older-epoch mutation commits; an authority-side check or revoke-and-drain is required, otherwise stop. |
| 9 | acceptance-criteria.md:131 | Incomplete | Timing: release-kind vs recovery-kind attempts, recovery-kind maximum lifetimes, phase deadlines, takeover inherits every recorded value. |
| 10 | acceptance-criteria.md:144 | Stale | An approved incompatible release's named recovery runs as an in-place data restore (quiesce, recovery mode). |
| 11 | acceptance-criteria.md:149 | Stale | Stop clear is revision-conditioned (CAS) and names a verified current working attempt. The spine now says "attempt", while the PRD-derived text says "release". |
| 12 | acceptance-criteria.md:151 | Incomplete | Name this branch "baseline re-deploy" and add the data-restore branch. |
| 13 | acceptance-criteria.md:189 | Incomplete | DR: recovery mode (spine DR sequence). Step 4 hooks are invoked by the owning executor through the environment's recovery-hook principal. |
| 14 | acceptance-criteria.md:190 | Incomplete | Before reopening: reconcile restored admission membership to the AD-6 admission records. |
| 15 | acceptance-criteria.md:224 | Incomplete | Administrator grants and revokes admission through a signed admission record written before the Keycloak change. |
| 16 | acceptance-criteria.md:226 | Stale | Synthetic admission: add the staging-only tenant-lifecycle clause (marker-bearing creation; tenants whose attested creation names the same synthetic client). The production predicate has no such clause. |
| 17 | acceptance-criteria.md:237 | Incomplete | AD-14 Surface: audience is validated but never establishes the surface; the surface comes from azp or the attested originating surface. |
| 18 | acceptance-criteria.md:238 | Stale | Negative tests now include a token with a confidential client's correct target audience from an `agent` or `service` origin. |
| 19 | sequencing.md:25 | Stale | First production attempt row now covers: timing by attempt kind, takeover fencing or revoke-and-drain, revision-conditioned stop clears, in-place entry with quiesce and recovery mode, and admission records. |
| 20 | sequencing.md:37 | Incomplete | The SM-4 grant and its revocation are each written first as signed Administrator admission records. |
| 21 | sequencing.md:52 | Stale | G2 row: "Keycloak database backup with admission reconciliation proven by a revocation lost before event export". |
| 22 | sequencing.md:105 | Stale | Shared runtime and profile evidence adds Dapr activation qualification (one policy-only and one HTTPEndpoint-only update). |
| 23 | sequencing.md:108 | Stale | Staging evidence policy adds the staging-reset maximum duration. |
| 24 | sequencing.md:132 | Stale | Admission predicate row adds the staging-only lifecycle clause. |
| 25 | sequencing.md:136 | Stale | Recovery hook contract row adds recovery mode and the per-environment recovery-hook principal. Must precede is now "First staging deployment…; the first AD-12 drill proves it". The Blocks column should add CAP-6 (staging reset). |
| 26 | sequencing.md:163 | Stale | A staging reset is an in-place **data restore** (quiesce, recovery mode), not a generic in-place recovery. |
| 27 | sequencing.md:210 | Stale | "re-synced to the final spine … 2026-09-28" predates update run 3. |
| 28 | sequencing.md:215 | Stale | The gate link will move to the update-2026-09-28-r3 gate once finalized. |
| 29 | glossary.md:45 | Incomplete (optional) | Promotion stop: revision-conditioned clear. |
| 30 | glossary.md:79 | Incomplete | Add release-kind and recovery-kind attempts (RRA Timing and interruption) next to attempt, lock, epoch. |
| 31 | glossary.md:87 | Incomplete | In-place recovery: baseline re-deploy and data restore. |
| 32 | glossary.md:90 | Incomplete | Add recovery mode (spine DR sequence) and the recovery-hook principal (AD-7). |
| 33 | glossary.md:96 | Incomplete | Add admission records (AD-6). |
| 34 | glossary.md (new, near :76) | Incomplete (optional) | New term: Dapr activation (spine Release tiers). |
| 35 | success-measures.md:17–20 | Incomplete (optional) | Spine carry-through could note three items: SM-5 covers takeover fencing, revision-conditioned clears and data-restore quiesce; SM-6 proves admission reconciliation; SM-4 includes the audience-spoof negative test. |

Total: 35 passages (18 stale, 17 incomplete, of which 3 optional). `brownfield.md` is unaffected.

## Defect findings

### RC3-1 · MEDIUM · discuss — In-place data restore: environment-layer boundary and live-broker backlog undefined

- **Evidence:**
  - 281: the data restore "restores no Keycloak, OpenBao or environment layer".
  - 243: the environment layer contains data services, the broker and the tenant-key store.
  - 317: step 3 restores the tenant-key store.
  - 318: step 4 restores data, then "Re-provision the broker and catch subscribers up from restored checkpoints".
  - 313: recovery mode renders "no Subscriptions … until step 4 re-provisions the broker".
  - 466: the production broker is qualified "with delivery retention".
  - Memlog 245: the aim is to prevent "live candidate writers/consumers mutating restored state"; the rejected alternative was "consumers drain backlog before hooks".
- **Problem:**
  - In DR, re-provisioning means a fresh broker. In place, the broker is the live one. It still holds the failed candidate's post-cut messages, possibly in formats the recovery release cannot read and naming events the restore rewound.
  - Catching up from restored checkpoints delivers those messages into restored projections.
  - "restores no … environment layer" also reads as forbidding the tenant-key-store and data-service content restores that steps 3–4 perform.
- **Correction (In-place recovery row, 281):** replace "It restores no Keycloak, OpenBao or environment layer, and has no fence or cutover." with: "It restores no Keycloak or OpenBao and re-applies no environment-layer definitions, so the applied union stays; steps 3–4 restore only tenant-key store and data-service contents. Before any Subscription is rendered, step 4 re-provisions the live broker through the environment-layer identity, discarding every message and consumer position after the cut. It has no fence or cutover." The mechanism (purge vs recreate topics) is the discussion point.

### RC3-2 · LOW · autofix — Recovery-release value source not carried (VAL-09 partial)

- **Evidence:**
  - Memlog 245 decides the release is deployed "by digest with environment-current values in recovery mode".
  - 281 omits the value source.
  - The DR analog (316) takes configuration from the recovery point's recorded digest, which invites the wrong reading in place.
- **Correction (281):** "…then deploys the recovery point's retained release by digest with environment-current values in recovery mode and runs DR steps 3–6."
- **Note for the recovery lens (pre-existing since D5, not a regression):** the environment-current catalog generation after an incompatible candidate is the candidate's committed one. When no rollback set exists, AD-15 does not require the recovery release to tolerate it, so the named recovery should name the generation it commits.

### RC3-3 · LOW · discuss — Recovery-mode readiness and step-4 Subscription activation undefined

- **Evidence:**
  - 283: "recovery-release readiness at phase start plus 10 minutes". That phase precedes the step 4 data restore: in DR against empty stores, in place against candidate-written data.
  - 313: Subscriptions are withheld until step 4.
  - 249: Dapr activation must restart consumers "before readiness or qualification passes".
  - 318: step 4 does not say it renders Subscriptions or restarts their consumers.
- **Correction (append to Recovery mode, 313):** "Recovery-mode readiness means every recovery workload runs and serves its recovery hook endpoint, not data-dependent readiness; step 4 renders the Subscriptions with Dapr activation of their consumers before the step 6 verification window opens."

### RC3-4 · LOW · autofix — Recovery executor's credential list omits the recovery-hook principal

- **Evidence:**
  - 128: each executor obtains "for a recovery attempt it owns, the environment's recovery-hook principal".
  - 131: the recovery executor "holds standing credentials only for …", and custodians release only "key, decryption and synthetic-client material".
  - 318: DR step 4 requires the principal.
- **Correction (131):** "custodians release key, decryption, synthetic-client and recovery-hook-principal material per job, destroyed at job end".

### RC3-5 · LOW · autofix — NFR-3 negative matrix lacks the recovery hook endpoint

- **Evidence:**
  - The diff replaces "reachable only from the recovery executor" with principal admission (318).
  - The staging executor now obtains a staging recovery-hook principal (128).
  - AD-8 automation targets (143) list only "the internal verification endpoint".
  - PRD NFR-3 (prd.md:307, 309) requires denial tests for automation credentials.
- **Correction (143):** "…records, evidence, backup prefixes, the internal verification endpoint and the recovery hook endpoints, including a staging recovery-hook principal against production's; …"

### RC3-6 · LOW · autofix — AD-14 Surface scope narrower than decided

- **Evidence:** memlog 244 says "every bearer endpoint validates audience and derives the surface…". 202 says "Every externally reachable host that accepts bearer tokens…". Chains (205) keeps internal hops covered, but the prohibition "Neither the target audience … establish the surface" binds only external hosts.
- **Correction (202):** "Every host that accepts bearer tokens, including internal targets of chained steps, validates its own audience and derives the surface…"

### RC3-7 · LOW · autofix — Admission-record durability not stated

- **Evidence:**
  - Memlog 250 puts the records in "the off-site record store".
  - 118 names the attempt, lock and stop store, which 225 places only "outside the target cluster and executor hosts".
  - The VAL-08 scenario is loss of the whole primary site immediately after a revocation.
- **Correction (118):** "…is first written as a signed Administrator record in the attempt, lock and stop store, durable outside the primary failure domain, and then applied in Keycloak."

### RC3-8 · LOW · autofix — Binding classes attempt-bound list not updated

- **Evidence:**
  - 225 lists "the environment-current values used, lock epoch, t0, deadlines, grace and outcome".
  - 283 adds kind, entry time, outage timestamp, maximum lifetime and phase deadlines.
  - 280 and 287 add the observed stop revision.
- **Correction (225):** "**Attempt-bound** values — the environment-current values used, attempt kind, lock epoch, t0 or entry time, any outage timestamp, maximum lifetime, deadlines, grace, any observed stop revision and outcome — …"

### RC3-9 · LOW · autofix — Recovery-kind lifetimes mix a deadline with unanchored durations

- **Evidence:** 283 defines one deadline, "outage time plus four hours", beside three durations: "the last drill's measured duration", "the duration its Administrator record states" and "a declared staging bound". The durations have no stated anchor, so the executor and the monitor can compute different expiry times.
- **Correction (283):** "…and one maximum lifetime, recorded as a deadline anchored at the outage timestamp where one applies, otherwise at entry: for DR, four hours when the outage began within coverage, otherwise the last drill's measured duration; for an approved release's named recovery, the duration its Administrator record states; for a staging reset, the declared staging bound."

### RC3-10 · LOW · autofix — Takeover fencing edge cases

- **Evidence:**
  - 282: the new owner must use an authority-side check or revoke-and-drain "before its first mutation at each authority", "otherwise it stops". No clause covers authorities where the previous owner held no credential (replacement capacity at DR entry) or that DR step 1 already fenced. A literal reading stops DR takeovers.
  - "the declared maximum in-flight request duration" has no declaring owner and is not an attempt-bound value.
  - Its wait interacts with the inherited deadlines, and the grace (283) is "used for … takeover".
- **Correction (282):** "An authority at which the previous owner held no credential, or whose credentials DR step 1 fenced, needs no wait. Each authority's maximum in-flight request duration is declared by its owner and recorded in the attempt; the wait counts against the inherited deadlines."

### RC3-11 · LOW · autofix — New staging consumers sequenced late (VAL-06 analog)

- **Evidence:**
  - 441: the admission-predicate row "Must precede: First asynchronous cross-module step". Its new staging-only lifecycle clause (234) is consumed by the staging gate's tenant-lifecycle flows (277).
  - 472: "in-place recovery entry point with quiesce and recovery mode" is owned only at First production attempt. Staging resets are data restores on the staging executor (278, 281), and 445 now dates the hook contract to the first staging deployment.
- **Correction:**
  - 441 Must precede: "Hosted enrollment and the first staging gate running a tenant-lifecycle critical flow; first asynchronous cross-module step".
  - 469 acceptance: append "the staging reset as an in-place data restore on the staging executor (quiesce, recovery mode and hook invocation) before the first reset".

### RC3-12 · LOW · autofix — Synthetic identities clause wording

- **Evidence:**
  - 234 opens with "For that flow, and in staging only, the admission predicate also admits…". Admission cannot evaluate flow membership.
  - Memlog 251 has no flow condition and says "the production predicate and realm carry no such clause". The spine drops "realm".
- **Correction (234):** "In staging only, to serve those flows, the admission predicate also admits…; the marker alone never admits, and neither the production predicate nor the production realm carries such a clause."

### RC3-13 · LOW · autofix — Dapr activation paragraph mixes topics

- **Evidence:** 249 (diff lines 98–102). The sentence "Environment-layer and shared-infrastructure definitions … live in the operations repository; each attempt records their digest…" moved from the 247 paragraph into the bold "Dapr activation." rule paragraph.
- **Correction:** move that sentence back to the end of the paragraph at 247. Paragraph 249 then holds only the Dapr activation rule.

### RC3-14 · LOW · discuss — Step 5 Keycloak actions unscoped for in-place data restore

- **Evidence:**
  - 281: the data restore "restores no Keycloak" yet runs steps 3–6.
  - 319, step 5, contains three Keycloak actions: "Rotate the restored synthetic clients' credentials under the run's DR-scoped realm rights, released to the recovery executor", "Rotate realm signing keys unless the old issuer is proven unavailable", and "Reconcile restored admission membership…".
  - 119: the production executor holds no realm rights. The recovery exception covers runs that restore the Keycloak database.
  - A literal in-place run either stalls or pressures granting realm rights to the production or staging executor.
- **Correction (319, or 281):** "In an in-place data restore, step 5 rotates only credentials restored with the data; its Keycloak actions apply only to runs that restored the Keycloak database."

### RC3-15 · LOW · discuss — Residual next to VAL-08: realm revocations other than admission inside the export lag

- **Evidence:**
  - prd.md:226 requires DR to "reapply post-cut revocations" and prove "denial of revoked principals".
  - 118 records only admission grants and revocations.
  - 119 and 319 re-apply other admin revocations only from the event export, whose lag is bounded (290).
  - The lost-window exception (294) and the accepted risks (485) name only module-owned revocations.
  - So a Keycloak realm-role removal acknowledged inside the export lag is neither recoverable nor an accepted risk. This is pre-existing, not introduced by this update.
- **Correction:** either extend Admission records (118) to "every production admission or realm-role grant or revocation", or add "Keycloak realm-role revocations inside the event-export bound" to Accepted risks with Administrator acceptance.

## Counts

| Severity | Count | IDs |
| --- | --- | --- |
| High | 0 | — |
| Medium | 1 | RC3-1 |
| Low | 14 | RC3-2 … RC3-15 |

| Action | Count |
| --- | --- |
| Autofix | 11 (RC3-2, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13) |
| Discuss | 4 (RC3-1, 3, 14, 15) |
