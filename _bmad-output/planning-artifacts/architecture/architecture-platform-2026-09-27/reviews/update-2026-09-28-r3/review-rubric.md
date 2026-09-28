# Update run 3 — good-spine rubric review

Reviewed 2026-09-28. Target: `../../ARCHITECTURE-SPINE.md` (485 lines, status draft, updated 2026-09-28, SHA-256 `eb026c85d6361fa01b2a06dcf28a69c26b603f2bc51f3443b3428089be28e374`). Inputs: `spine.diff`, `../validate-2026-09-28/validation-report.md`, memlog entries from line 243 ("Update run 3 started") onward, the Platform PRD and addendum. Read-only review; nothing but this file was written. D1–D10, earlier AD adoptions and amendments, overrides and accepted risks are not reopened. Owned-work implementation evidence that is already gated is not reported.

**Verdict: CONDITIONAL PASS.** The update closes the primary defect in each of the 10 VAL items. It removes audience-only surface classification, adds revision-conditioned stop clears, the takeover-fencing invariant, admission records, the staging lifecycle clause, Dapr activation and the release-kind/recovery-kind timing split. It introduces no new AD, and lint is clean. The main remaining problem is the in-place data restore. It reuses DR steps 3–5, which still name DR-only actors, stores and credentials, and it now says it "restores no … OpenBao or environment layer". The production and staging executors also have no path to read or decrypt recovery points. The new timing, takeover and admission-record text leaves smaller edge-case gaps.

**Counts:** 0 critical, 1 high, 7 medium, 7 low (15 findings). **Actions:** 13 autofix, 2 discuss (RB3-7, RB3-14).

## Findings

### RB3-1 — In-place data restore reuses DR steps whose actors, stores and credentials are DR-only

- **Severity:** high · **Action:** autofix · **Confidence:** high
- **Evidence:**
  - L281: data restore "deploys the recovery point's retained release by digest in recovery mode and runs DR steps 3–6. It restores no Keycloak, OpenBao or environment layer".
  - L317 (step 3): "Restore the tenant-key store and re-apply tombstone key destruction … read through its fence-and-reissue owner for this run". L243 lists the "tenant-key store" in the environment-layer tier.
  - L319 (step 5): synthetic material is "released to the recovery executor for that job only"; "Each surviving authority's owner issues the replacement instance's credentials there"; "Rotate realm signing keys unless the old issuer is proven unavailable".
  - L128 (AD-7 Credentials): the production and staging executors get only the deploy identity, the environment-layer identity, smoke clients and "the environment's recovery-hook principal". They get no recovery-point read and no decryption or custody material.
  - L131: only the recovery executor holds "read-only access to recovery points" and has "custodians release key, decryption and synthetic-client material per job". Its list also says "standing credentials only for …" and omits the recovery-hook principal that L128 grants to "each executor".
  - L289: copies are encrypted "with access and decryption material independent of the primary failure domain".
- **Consequence:** The VAL-01 fix covered the hook endpoint (step 4) but not the steps around it. That was also the prior rubric's RB-1 advice: "apply that wording consistently to steps 3–6 and custody handoff".
  - An approved incompatible release's named recovery and a staging reset still cannot restore or decrypt a recovery point on their owning executor.
  - The recovery runner team and the custody/credential team will diverge on four points:
    - whether step 3 rewinds the live tenant-key store, which L281 forbids and D5 implied;
    - whether the production executor receives backup and decryption authority;
    - which executor receives synthetic material;
    - whether every in-place restore rotates production realm signing keys.
  - The likely workaround is broad standing backup and decryption access on the production executor, which weakens AD-7 and AD-12.
- **Minimal correction (paste):**
  - In-place recovery row, replace "runs DR steps 3–6" with: "runs DR steps 4 and 6 and the in-place forms of steps 3 and 5: step 3 keeps the live tenant-key store, whose key destruction already persists, and only verifies that it covers every key generation the recovery point references; step 5 rotates only credentials restored with the data, rotates no realm signing key and issues no replacement-instance credentials."
  - AD-7 *Credentials*, after "the environment's recovery-hook principal", add: "and, for a data-restore attempt it owns, read access to that environment's recovery-point prefix plus the decryption material custodians release for that job, destroyed at job end".
  - AD-7 *Operator-started recovery*, after "the off-site registry replica", add: "; it obtains the replacement instance's recovery-hook principal per job".
  - DR step 5: replace "released to the recovery executor for that job only" with "released to the executor owning the attempt for that job only".

### RB3-2 — Admission records have no retention rule, so DR reconciliation can remove every long-standing grant

- **Severity:** medium · **Action:** autofix · **Confidence:** medium
- **Evidence:**
  - L118: admission records "are the recovery authority for admission".
  - L319: reconciliation removes "every principal without a current grant record".
  - L81 (AD-2 *Retention*): "records and evidence … retained for the backup retention period or their life as a rollback target, whichever is longer". That is 30 days (L289), and an admission grant is never a rollback target.
- **Consequence:** A record-store owner who applies AD-2 correctly can prune a grant record after 30 days. The next DR then removes every user admitted more than 30 days earlier. Only Administrator can re-grant, so a deputy-run DR reopens to a locked-out production. This undercuts the G2 "deputy's … rehearsed restore and reopen", and the reopened service would not count as restored for the RTO.
- **Minimal correction (paste), AD-6 *Admission records*:** "They are retained off the primary failure domain while their grant is current and until every recovery point cut before their revocation has expired; retention never prunes a current grant or its latest revocation."

### RB3-3 — Recovery mode and remove-workloads renders have no home in the retained package or the binding classes

- **Severity:** medium · **Action:** autofix · **Confidence:** medium
- **Evidence:**
  - L313: recovery mode requires "no Subscriptions rendered until step 4 re-provisions the broker" and workers disabled.
  - L281: quiesce "removes the current release's workloads".
  - L80 (AD-2): executors "never regenerate" the package.
  - L68 (AD-1 *Chart*): the shared helper's outputs list no mode switches.
  - L225: attempt-bound values are "environment-current values used, lock epoch, t0, deadlines, grace and outcome", and "readiness compares attempt-bound digests".
  - L445: the recovery hook contract lists "recovery mode" only as a module-facing field.
- **Consequence:** The publication workflow, which emits the chart, and the recovery runner, which must render it without Subscriptions or with workloads removed, are built separately. If the chart has no mode values, the executor can meet recovery mode only by regenerating or hand-editing, and AD-2 forbids both. Recovery-mode digests also match no recorded value, so the readiness digest comparison either fails or gets bypassed.
- **Minimal correction (paste), AD-1 *Chart*:** "The helper also emits recovery-mode and remove-workloads switches as chart values (Subscriptions off, declared external-effect and destructive-retention workers disabled, workloads removed), so executors reach both through the retained package."
- **Binding classes, attempt-bound list:** add "the render mode (normal, recovery or remove-workloads)".

### RB3-4 — Recovery mode forbids the subscriber catch-up that step 4 requires

- **Severity:** medium · **Action:** autofix · **Confidence:** high
- **Evidence:**
  - L313: "only recovery-scope tasks mutate state until step 6 passes".
  - L318 (step 4): "Re-provision the broker and catch subscribers up from restored checkpoints".
  - L223: recovery-scope tasks are one of four startup-task scopes; ordinary subscription handlers are not tasks.
- **Consequence:** One implementer blocks handlers until step 6. Catch-up then cannot run in step 4, and step 6's cross-module integrity and smoke checks run against stale projections. Another implementer lets handlers run, which violates recovery mode. Both follow the text.
- **Minimal correction (paste), Recovery mode sentence:** "…only recovery-scope tasks and step 4's subscriber catch-up from restored checkpoints mutate state until step 6 passes; no user- or schedule-triggered work runs before reopening."

### RB3-5 — Release-kind timing gives environment-layer and shared-infrastructure attempts a 10-minute rollout deadline

- **Severity:** medium · **Action:** autofix · **Confidence:** medium
- **Evidence:**
  - L283: "Release-kind attempts — every lock-covered change except a data restore or DR — record t0 … and the rollout deadline t0 + 10 minutes; their maximum lifetime is derived from these". The memlog (L246) explicitly includes environment-layer and shared-infrastructure changes.
  - L282: the lock covers "environment-layer, profile, realm-contract or operator-artifact change"; shared-infrastructure changes hold both locks.
  - L243–L244: data-service, OpenBao and broker upgrades, Kubernetes minor upgrades, Keycloak server upgrades and the Dapr control plane; rollback is "Forward only; restore is DR".
  - L485 (accepted risk): "in-place Kubernetes-minor upgrades that take both environments down".
- **Consequence:** A legitimate Kubernetes-minor or data-service upgrade exceeds t0 + 10 minutes. The attempt is then failed and has no recovery path, and the off-site monitor raises a stale-attempt alarm from the derived lifetime. Alternatively, each workflow invents its own deadline, and the executor and monitor disagree. That is the same divergence VAL-10 fixed for recovery attempts.
- **Minimal correction (paste), Timing and interruption, after the release-kind sentence:** "Environment-layer and shared-infrastructure attempts instead record at entry the maximum lifetime their named workflow declares; their re-verification of each working release records a readiness deadline of phase start plus 10 minutes and the verification window when it starts."

### RB3-6 — "Takeover inherits every recorded value" is ambiguous when a new attempt supersedes an old one, and nothing closes the superseded record

- **Severity:** medium · **Action:** autofix · **Confidence:** medium
- **Evidence:**
  - L283: "Takeover inherits every recorded value. Timers never restart". This sentence is new in run 3.
  - L282: takeover may come from "an Administrator record, an In-place recovery or DR entry … or a replacement job of the same workflow resuming the same attempt"; "The off-site monitor notifies when a record stays non-terminal past its recorded maximum lifetime".
  - L225: "attempt records, one per attempt and immutable once terminal", each by its own writer.
  - L321: DR has its own "DR attempt record".
- **Consequence:** One reading has a DR or in-place entry that takes over a stuck release inherit the release's t0, 10-minute deadline and derived lifetime, so the monitor or the timing rule fails a valid DR within minutes. The other reading starts a new attempt, but no writer is allowed to terminate the superseded record. It then stays non-terminal and the stale-attempt alarm fires forever.
- **Minimal correction (paste), Timing and interruption:** replace "Takeover inherits every recorded value." with "A replacement job resuming the same attempt inherits every recorded value; any other takeover records the superseded attempt's terminal outcome as non-working (superseded), written by the taking-over attempt's writer, and records its own values by kind."
- **Binding classes, attempt-record writers:** add "and a superseded attempt's terminal record by the writer of the attempt that took it over".

### RB3-7 — Data restore names no catalog generation for the recovery release

- **Severity:** medium · **Action:** discuss · **Confidence:** medium
- **Evidence:**
  - L226: "Activation in every environment … Recovery commits the rollback generation".
  - L213 (AD-15): the rollback set is recorded "Unless an Administrator-approved release names a separately planned recovery". Approved named-recovery releases, the main data-restore case, therefore have no prepared rollback generation.
  - L225: "key and catalog generations with root digest" are environment-current, "read from the target environment".
  - L281: data restore "restores no … environment layer".
- **Consequence:** After an approved breaking candidate commits its generation, the in-place recovery release is deployed with environment-current values. That means it runs on the candidate's generation, whose codec or descriptors the baseline host may not tolerate. The alternative is that the catalog store is rewound with the data, which contradicts the forward-only catalog rule. The recovery runner and EventStore's catalog protocol owner will pick different behaviors.
- **Minimal correction (paste, option A), In-place recovery row:** "The recovery release runs on a forward generation prepared from the recovery point's committed generation under the EventStore catalog protocol, keeping later idempotency and key entries as non-executable retention entries; the attempt record names it."
- **Option B:** the catalog store is restored with the data and its restored generation is recorded. Record the choice in AD-15 or Catalogs.

### RB3-8 — The AD-3 "exactly one tier" rule leaves NetworkPolicies, which recovery-mode quarantine depends on, without a tier

- **Severity:** medium · **Action:** autofix · **Confidence:** medium
- **Evidence:**
  - L88 (AD-3): "Every deployed object belongs to exactly one tier in Release tiers".
  - L242–L245: the tier contents list no NetworkPolicies, namespaces, quotas, PriorityClasses or identity RBAC.
  - L141: "default-deny ingress and egress NetworkPolicy".
  - L313: recovery-mode "quarantine admits only the executor owning the attempt and the recovery workloads", which now applies in place on live namespaces (L281).
  - L138: the environment-layer identity writes the data namespace, and the application deploy identity "holds none of these".
- **Consequence:** For NetworkPolicies, quarantine policy and namespace-level controls, the chart helper, the environment-layer workflow and the recovery runner can each assume a different writer and version authority. In-place quarantine could then be applied by the wrong identity or not at all. It could also be reverted by an application rollback, since tier membership decides which objects AD-3 rollback touches.
- **Minimal correction (paste), Release tiers:**
  - Application package contents: add "per-workload NetworkPolicies derived from declared inbound callers and egress".
  - Environment layer contents: add "namespaces, quotas and limits, default-deny and quarantine NetworkPolicies, and environment identity RBAC".
  - Shared infrastructure contents: add "PriorityClasses and StorageClasses".

### RB3-9 — DR verification does not require the admission projection to reflect the step-5 reconciliation before asynchronous tasks resume

- **Severity:** low · **Action:** autofix · **Confidence:** medium
- **Evidence:**
  - L319: step 5 reconciles Keycloak membership.
  - L205 (AD-14 *Chains*): asynchronous steps "re-check that actor's current admission against EventStore's admission projection".
  - L441: the projection is "fed from the realm admin-event export with a declared maximum staleness".
  - L320 (step 6): checks "denial of revoked principals" but not the projection.
  - VAL-08's consequence named "restored asynchronous tasks can run for the resurrected actor".
- **Consequence:** A token-level gateway test can pass while the restored projection still reads a reconciled-away principal as admitted. Asynchronous tasks resumed after step 6 then run for that principal. This depends on how staleness is measured.
- **Minimal correction (paste), step 6:** after "denial of revoked principals", add ", including that EventStore's admission projection reads every principal removed in step 5 as revoked before any asynchronous task resumes".

### RB3-10 — The admission-predicate milestone names a consumer later than the gateway's staging lifecycle clause

- **Severity:** low · **Action:** autofix · **Confidence:** medium
- **Evidence:**
  - L441: the row now includes "the staging-only lifecycle clause", with Must precede "First asynchronous cross-module step".
  - L234: the gateway applies the clause to staging tenant-lifecycle E2E.
  - L277: every enrolled module's critical flows must pass in staging.
  - L288: production admission is exercised from G1 (SM-4).
- **Consequence:** This is the same class as VAL-06. Read literally, the row lets the first staging promotion with a tenant-lifecycle flow, and G1 admission, precede the predicate, so the staging gate is blocked or the Tenants E2E is dropped.
- **Minimal correction (paste), Must precede cell:** "First hosted enrollment for the gateway predicate, including its staging lifecycle clause; first asynchronous cross-module step for the projection."

### RB3-11 — The NFR-3 negative-test list omits the new recovery-hook principal and internal recovery endpoint

- **Severity:** low · **Action:** autofix · **Confidence:** high
- **Evidence:**
  - L128: each executor now obtains "the environment's recovery-hook principal". Staging holds one for resets.
  - L318: the endpoint "admits only that environment's recovery-hook principal at the current epoch".
  - L143: the automation targets end at "backup prefixes and the internal verification endpoint".
- **Consequence:** Nothing requires proof that the staging recovery-hook principal is denied at production recovery endpoints. That is the new cross-environment path this update introduced.
- **Minimal correction (paste), AD-8 *Negative tests*:** replace "and the internal verification endpoint" with "the internal verification endpoint and the internal recovery endpoint, including the staging recovery-hook principal and a prior-epoch principal".

### RB3-12 — Owned-work gates for data restore and takeover fencing are later than their first staging consumer

- **Severity:** low · **Action:** autofix · **Confidence:** medium
- **Evidence:**
  - L472: "in-place recovery entry point with quiesce and recovery mode" and "takeover fencing or revoke-and-drain" are gated at First production attempt.
  - L278: a staging reset runs "an in-place data restore as its own staging attempt".
  - L445: the hook contract is due at first staging deployment.
  - L469: only the "staging-reset maximum duration" is gated at first staging evidence.
- **Consequence:** This is the VAL-06 class again. Staging resets and staging takeovers can occur before production, while the quiesce and recovery-mode path and staging fencing are nominally due later.
- **Minimal correction (paste), Owned work:** add the row "First staging deployment | Staging data restore and takeover fencing | Platform, Builds | Staging-reset entry with quiesce and recovery mode, owning-executor hook invocation, and takeover fencing or revoke-and-drain at staging mutation authorities."

### RB3-13 — The recovery-kind DR lifetime outside coverage has no anchor and no pre-drill value

- **Severity:** low · **Action:** autofix · **Confidence:** medium
- **Evidence:**
  - L283: "for DR, the outage time plus four hours when the outage began within coverage, otherwise the last drill's measured duration". The first form is an absolute deadline; the second is a bare duration.
  - L288 and L292: production exists from G1, and the first drill comes only before G2.
- **Consequence:** For a DR outside coverage, the executor and monitor can anchor the duration differently: at entry, at the outage, or not at all. A DR between G1 and the first drill has no defined lifetime.
- **Minimal correction (paste):** "…otherwise entry time plus the last drill's measured duration, or plus four hours before the first drill".

### RB3-14 — Nothing checks drift between admission records and Keycloak membership outside DR

- **Severity:** low · **Action:** discuss · **Confidence:** medium
- **Evidence:**
  - L118: records are "first written … and then applied in Keycloak".
  - L119: reconciliation happens only in recovery.
  - L441: EventStore's admission projection already mirrors membership.
- **Consequence:**
  - A revocation record that is never applied leaves live access indefinitely.
  - A grant applied without a record is silently removed at the next DR.
  - Neither is detected until recovery.
- **Minimal correction (paste), AD-6 *Admission records*:** "Each production release attempt and each drill compare the admission projection with the admission records; any difference stops without mutation and notifies." The mechanism choice is why this is marked discuss.

### RB3-15 — The capability map lags the edited recovery and admission ownership

- **Severity:** low · **Action:** autofix · **Confidence:** high
- **Evidence:**
  - L417 (FR-9): governing decisions are "AD-2/AD-3/AD-6/AD-8/AD-12". AD-7 (recovery executor, recovery-hook principal) and In-place recovery are missing, although FR-9's in-place and DR paths now depend on both.
  - L419 (FR-11): the "Disaster recovery sequence" is not named, although step 5 now enforces FR-11's revocation authority.
- **Consequence:** This is a traceability gap only. Story writers who work from the map miss the executor credential and reconciliation obligations.
- **Minimal correction (paste):**
  - FR-9 row: "AD-2/AD-3/AD-6/AD-7/AD-8/AD-12; Disaster recovery sequence, In-place recovery, Backup coverage and cadence, Recovery point and freshness, Data protection".
  - FR-11 row: add "Disaster recovery sequence".

## AD-by-AD enforceability sweep

| AD | Enforceable and prevents its divergence? | Note |
| --- | --- | --- |
| AD-1 | Yes, with a gap | One model, one chart helper, no Secret, fallback trigger. The helper lacks recovery-mode and remove-workloads switches (RB3-3). |
| AD-2 | Yes, with a gap | Digest-bound promotion and no regeneration hold. The generic record retention now collides with admission records (RB3-2). |
| AD-3 | Mostly | The Dapr-consumer extension of "changed workload" is correct and consistent with L249. The "exactly one tier" rule leaves NetworkPolicies and namespace controls without a tier (RB3-8). |
| AD-4 | Yes | Unchanged; mode, identity and mapping checks are testable. |
| AD-5 | Yes | Unchanged. |
| AD-6 | Yes, with gaps | Admission records close VAL-08's lost-revocation path and match PRD FR-11 and FR-9. Retention (RB3-2) and drift (RB3-14) are open. |
| AD-7 | Partial | The owning executor's hook principal closes VAL-01's endpoint gap. Recovery-point read, decryption and custody release are still recovery-executor-only, and the recovery executor's "only" list omits the hook principal (RB3-1). |
| AD-8 | Yes | The Dapr activation reference is consistent. The negative-test list misses the new principal (RB3-11). |
| AD-9 | Yes | Unchanged. |
| AD-10 | Yes | Unchanged. |
| AD-11 | Yes | Unchanged; availability is tied to the served eligibility and digest. |
| AD-12 | Yes | "Run by the AD-7 recovery executor" remains true for replacement-capacity DR. In-place reuse is handled in the In-place recovery row (RB3-1). |
| AD-13 | Yes | Unchanged. |
| AD-14 | Yes | Audience-only classification is removed. Surface comes from `azp` plus the attested originating surface, and the new audience-spoof negative test is present. No contradiction with Chains (L205) or Client types (L203) was found. |
| AD-15 | Yes, with a gap | Expand-only and rollback-set rules are unchanged. The approved named-recovery exemption leaves the data-restore catalog generation undefined (RB3-7). |

## Closure of the validation findings

| VAL | Status | Residual |
| --- | --- | --- |
| VAL-01 | Endpoint closed (L318, L128) | The custody, backup-read, step 3 and step 5 reuse is open (RB3-1). |
| VAL-02 | Closed (L282, L472) | Superseded-record handling (RB3-6). |
| VAL-03 | Closed (L249, L90, L142, L243, L466) | — |
| VAL-04 | Closed (L287, L280) | — |
| VAL-05 | Closed (L234, L441) | Milestone (RB3-10). |
| VAL-06 | Closed (L445) | — |
| VAL-07 | Closed (L202, L206) | — |
| VAL-08 | Closed (L118–L119, L319, L476) | Retention (RB3-2), projection (RB3-9), drift (RB3-14). |
| VAL-09 | Closed (L281, L313) | Step reuse (RB3-1), render mechanism (RB3-3), catch-up (RB3-4), catalog (RB3-7), quarantine tier (RB3-8), gate (RB3-12). |
| VAL-10 | Closed for recovery-kind (L283) | Release-kind scope (RB3-5), takeover inheritance (RB3-6), anchor (RB3-13). |

## Other rubric checks

- **Divergence points:** The edits fix the real cross-unit seams: the hook endpoint, the stop CAS, surface derivation, the admission authority, Dapr activation and attempt timing. The seams still missed are listed in RB3-1, RB3-3, RB3-7 and RB3-8.
- **Deferred items:** No First-shared-versions or Owned-work row lets two units diverge on semantics. Record encodings stay Builds-owned, and the recovery-mode contract is Platform-owned and due at first staging deployment. Two gates are later than their first consumer (RB3-10, RB3-12).
- **Named technology:** The Stack table is unchanged by this update. The new Dapr activation list matches the memlog's verified Dapr 1.18 hot-reload resource set (memlog L223: Components, Subscriptions, MCPServers, Configurations, HTTPEndpoints, Resiliencies, WorkflowAccessPolicies). Kubernetes TokenRequest Secret binding appears only as a memlog seed and is not bound. No technology finding.
- **Brownfield:** The update makes no new brownfield claim and does not contradict the observed-infrastructure paragraph (L405) or Owned work.
- **PRD coverage:** FR-1..FR-12 and NFR-1..NFR-3 all remain mapped (L411–L423). The update strengthens:
  - FR-8's stop clear (revision-bound, still Administrator-only);
  - FR-11's "authenticated, auditable action" (admission records);
  - FR-9's "reapply post-cut revocations … denial of revoked principals" (step 5 reconciliation plus the G2 proof);
  - FR-12's server-side surface check (AD-14).

  No PRD threshold is weakened. Traceability lag is noted in RB3-15.
- **Altitude ownership:** Every edited dimension is decided or assigned to an owner. No open question was introduced. Frontmatter status `draft` is correct during the update run.
