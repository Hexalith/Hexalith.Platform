# Platform architecture validation — 2026-09-28

**CHANGES REQUIRED** — Five independent lenses found 10 distinct items: 6 high, 3 medium and 1 low. Structural lint and technology verification pass; recovery, authorization and mutation-control rules need correction before the affected implementation work.

Target: [ARCHITECTURE-SPINE.md](../../ARCHITECTURE-SPINE.md), updated 2026-09-28. SHA-256: `79f0d65346e9b644de605791a0183e42182bacf7e25b79060ad9e00a2a356c06`.

Standalone review of the existing September 28 initiative-level contract, including its aligned spec and documented decisions. Existing workspace edits are input to this validation. The spine, source memlog, spec and implementation are unchanged.

## Checks

- Deterministic lint_spine.py: PASS, zero findings across 15 ADs.
- All 10 inline Markdown links resolve.
- The spine contains 15 stable AD IDs and four Mermaid diagrams.
- Five independent review lenses completed: rubric, technology/reality, adversarial compatibility, security and recovery.
- Dapr resource-restart and Keycloak audience/client behavior were cross-checked against current official documentation.
- SHA-256 comparison confirms the spine, source memlog and all captured spec files are unchanged from validation start.
- HTML verified in Chromium at desktop and mobile sizes: search, severity/action filters, reset and evidence expansion pass; no broken local links, horizontal overflow or JavaScript errors (report-check.json).

## Independent reviews

| Review | Verdict | Evidence |
| --- | --- | --- |
| Rubric and input coverage | CONDITIONAL PASS | [review-rubric.md](review-rubric.md) |
| Technology and repository reality | PASS — 0 findings | [review-reality.md](review-reality.md) |
| Adversarial compatibility | FAIL | [review-adversarial.md](review-adversarial.md) |
| Security and identity boundaries | NEEDS REVISION | [review-security.md](review-security.md) |
| Recovery safety and liveness | CONDITIONAL PASS | [review-recovery.md](review-recovery.md) |

## Triaged findings

### VAL-01 · HIGH · In-place recovery cannot reach the recovery hooks it must invoke

**Proposed action:** autofix. **Confidence:** high.

The production/staging recovery runner correctly performs in-place recovery on its own executor, while the module recovery endpoint correctly admits only the replacement-capacity recovery executor. Reused DR step 4 is denied on both in-place paths.

**Consequence:** The approved incompatible-release recovery and staging reset cannot restore and verify module-owned data; the deputy's documented in-place path also stops at this boundary.

**Recommended correction:** Define the caller as the executor owning the current recovery attempt: production executor for production in-place recovery, staging executor for staging reset, recovery executor for replacement-capacity DR. Authorize only that executor's environment-scoped, current-epoch recovery principal, and retain in-workload execution.

**Existing safeguards considered:** AD-7 already authorizes these in-place executors; the conflict is in the reused endpoint restriction. The correction preserves the accepted executor split and module-owned hook execution.

**Evidence:** [ARCHITECTURE-SPINE.md:277](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:309](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:314](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:129](../../ARCHITECTURE-SPINE.md)

**Reviewer findings:** RB-1, ADVR-1

### VAL-02 · HIGH · An epoch check before a mutation does not fence an earlier executor after takeover

**Proposed action:** discuss. **Confidence:** high.

Executor A reads and validates epoch e before a mutation, then its request is delayed. An allowed takeover gives B epoch e+1; B restores the baseline. A's already-authorized Kubernetes/catalog write then completes using still-valid per-job credentials. Both clients performed the required check before each mutation.

**Consequence:** A superseded executor can overwrite the recovered workload or catalog after the new owner has verified it; the signed attempt record and actual deployed state diverge.

**Recommended correction:** Add the semantic invariant that once takeover is accepted no prior-epoch mutation may commit, including in-flight writes. Require either fencing at each mutation authority or proven revocation/quiescence and draining of the earlier writer before the new epoch may mutate; record this prerequisite and fail closed if it cannot be proved.

**Existing safeguards considered:** The CAS-capable record store, signed records, per-job credentials and monotonic epochs protect ownership records but do not require stale writes to be rejected by Kubernetes, the catalog or other mutation targets.

**Evidence:** [ARCHITECTURE-SPINE.md:278](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:279](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:126](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:223](../../ARCHITECTURE-SPINE.md)

**Reviewer findings:** ADVR-2

### VAL-03 · HIGH · Dapr resources can change without activating the change in existing sidecars

**Proposed action:** autofix. **Confidence:** high.

HotReload is disabled but consumer restart is specified only for changed Components, leaving other Dapr resource updates without a required activation mechanism.

**Consequence:** Sidecars can retain old authorization, subscriptions or HTTP endpoint configuration while release records and rendered objects describe the new state.

**Recommended correction:** Require changed effective Dapr resource content or bindings to restart every affected consuming sidecar within the owning application/environment attempt before readiness or qualification passes; qualify policy-only and HTTPEndpoint-only updates.

**Existing safeguards considered:** The environment-layer row already requires restarts for changed Components, and readiness compares attempt-bound digests. It does not bind the other Dapr resource changes to a consuming-sidecar restart.

**Evidence:** [ARCHITECTURE-SPINE.md:140](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:240](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:241](../../ARCHITECTURE-SPINE.md)

**Reviewer findings:** RB-3

**Primary sources:** [Dapr v1.18: Updating resources (verified September 28)](https://docs.dapr.io/operations/components/component-updates/)

### VAL-07 · HIGH · Confidential target audience is not proof of the calling surface

**Proposed action:** autofix. **Confidence:** high.

AD-14 explicitly allows an externally reachable bearer host to accept its confidential-client audience instead of deriving the surface from authenticated azp. Audience identifies the target, not the originating client. A service token legitimately targeted to a confidential host for an agent-eligible operation can therefore be interpreted as UI authority by an independently implemented host.

**Consequence:** The realm and host teams can satisfy different clauses while allowing an agent/service origin to reach UI-only or confirmation-required operations. Correct target audience alone cannot enforce the calling-surface boundary.

**Recommended correction:** Require audience validation plus authenticated client-to-surface validation on every bearer endpoint, and require the attested effective originating surface on chained requests. Remove audience-only classification as an alternative. Add a negative case with the correct confidential target audience and a disallowed agent/service origin.

**Existing safeguards considered:** AD-14 already requires client classes, effective originating surface on chains, constrained exchange and per-client negative tests. The audience-only alternative weakens how a bearer host establishes that surface; its removal implements the existing policy.

**Evidence:** [ARCHITECTURE-SPINE.md:199](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:200](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:201](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:203](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:204](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:438](../../ARCHITECTURE-SPINE.md)

**Reviewer findings:** SEC-1

**Primary sources:** [Keycloak: Configuring and using token exchange](https://www.keycloak.org/securing-apps/token-exchange)

### VAL-08 · HIGH · Recovery can resurrect admission revoked inside the event-export lag

**Proposed action:** discuss. **Confidence:** high.

Identity-event export permits a bounded lag, yet DR restores admission from an older database plus surviving exported events and must deny revoked principals. A production-admission removal acknowledged immediately before failure can be absent from both restore inputs. The accepted RPO exception names module-owned revocations, not Keycloak admission revocations.

**Consequence:** Restoring the realm and rebuilding the admission projection can re-admit a revoked actor with no evidence of the missing revocation. Signing-key and credential rotation does not repair that admission state; restored asynchronous tasks can run for the resurrected actor without a fresh login.

**Recommended correction:** Choose a recovery-safe admission authority: independently durable revocation records before acknowledgement, or fail-closed restored human admission until Administrator reconciliation and explicit reauthorization when the export frontier may be incomplete. Any intended loss window for identity revocations requires an explicit risk decision. Qualify failure immediately after revocation and before event export.

**Existing safeguards considered:** Event-export lag is bounded and monitored; DR rotates keys and replays exported revocations. None recovers an acknowledged identity revocation that had not reached surviving storage. The accepted lost-window exception explicitly covers module-owned revocations.

**Evidence:** [ARCHITECTURE-SPINE.md:117](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:203](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:247](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:286](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:290](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:312](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:315](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:316](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:437](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:481](../../ARCHITECTURE-SPINE.md)

**Reviewer findings:** SEC-2

### VAL-09 · HIGH · In-place restore skips compatible-code preparation and quiescence

**Proposed action:** discuss. **Confidence:** high.

The in-place data-restore branch reuses DR steps 3–6, omitting not only D5's excluded fencing, Keycloak restore and cutover but also the worker disable, quarantine admission and recovery-point release deployment supplied by DR steps 1–2. Redeployment in the in-place row applies to its separate interrupted-recovery branch. A deployment lock does not stop live application writers.

**Consequence:** After an incompatible candidate fails, keys and old data can be restored while candidate consumers and workers are active, and recovery hooks can execute in incompatible candidate workloads. Staging reset has no inherited production ingress closure. This can corrupt restored state or cause external effects before reconciliation.

**Recommended correction:** Define an in-place restore preparation phase: close user admission, quiesce ordinary writers/consumers and destructive/external-effect workers, enter quarantine, and deploy the named recovery point's compatible retained release and configuration in recovery mode before key/data restoration. Permit recovery-task writes only until checks pass. Preserve D5's no old-instance fencing, no Keycloak database restore and no DNS cutover; use existing reopening authority and retain the promotion stop.

**Existing safeguards considered:** The failed approved-release path already closes production user ingress, and D5 intentionally excludes old-instance fencing, Keycloak restore and cutover. That does not quiesce background writers or prepare compatible recovery workloads; staging reset also needs an explicit admission boundary.

**Evidence:** [ARCHITECTURE-SPINE.md:274](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:275](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:277](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:278](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:311](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:312](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:314](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:245](../../ARCHITECTURE-SPINE.md)

**Reviewer findings:** REC-1

### VAL-04 · MEDIUM · A delayed promotion-stop clear can erase a newer incident

**Proposed action:** discuss. **Confidence:** high.

Administrator signs a clear for stop S1 naming the verified working baseline. Before that clear is applied, the off-site monitor records a new incident S2 without changing the working baseline. A store consumer then applies the still-valid signed clear, because no rule binds it to the observed stop revision or rejects it after S2.

**Consequence:** Promotion may resume while a newer recorded incident remains unresolved. The required health precheck does not cover every incident that legitimately sets the stop.

**Recommended correction:** Bind a clear record to the exact observed stop revision/set records and verified working-attempt identity, and apply it conditionally; any unacknowledged newer set keeps the stop active. Bind an empty/degraded override to one attempt and prevent it from consuming later stop records.

**Existing safeguards considered:** Only Administrator can clear the stop, and releases still have a health precheck. Neither requires a clear to acknowledge a later stop for an incident beyond that health check.

**Evidence:** [ARCHITECTURE-SPINE.md:223](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:278](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:283](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:276](../../ARCHITECTURE-SPINE.md)

**Reviewer findings:** ADVR-3

### VAL-05 · MEDIUM · Staging tenant-lifecycle tests have no admission authority for their permitted tenant IDs

**Proposed action:** discuss. **Confidence:** high.

A staging E2E unit obeys the explicit exception by creating a run-scoped tenant T distinct from the fixed environment synthetic tenant S. The gateway independently applies the declared synthetic-admission predicate, which admits its synthetic client only for S, while the sandbox has no other tokens. Operations addressing T are denied.

**Consequence:** The accepted staging tenant-lifecycle critical flow cannot pass without an undocumented admission exception or broader credentials, so its exact-release staging gate remains blocked.

**Recommended correction:** Specify the staging-only authorization bridge for the accepted lifecycle exception: a short-lived run-bound synthetic actor scope covering only the attested test tenant IDs and lifecycle operations, with no production or real-tenant grant. Make creation, subsequent calls and cleanup consume the same run scope and preserve exclusion markers.

**Existing safeguards considered:** The exception is restricted to staging and marked test tenants. The fixed synthetic-tenant admission rule and executor sandbox otherwise remain appropriately restrictive.

**Evidence:** [ARCHITECTURE-SPINE.md:127](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:232](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:437](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:273](../../ARCHITECTURE-SPINE.md)

**Reviewer findings:** ADVR-4

### VAL-10 · MEDIUM · Generic attempt timers lack recovery-specific phase and lifetime semantics

**Proposed action:** discuss. **Confidence:** high.

Attempt ownership covers DR and in-place data restore, while 'each attempt' timing derives a maximum lifetime from catalog preparation, ten-minute rollout and release verification. Recovery begins with other mutations and can spend substantially longer restoring data within its separately specified four-hour RTO. The readiness-clock start and persisted maximum lifetime for these attempts are unspecified.

**Consequence:** Independent implementations can incorrectly expire a valid restore using a release-sized lifetime, or leave restore attempts without a useful stale-attempt deadline. This is a scope ambiguity in the generic timer rule, not a challenge to the explicit four-hour incident RTO.

**Recommended correction:** Scope release deadlines to release attempts. Persist recovery phase deadlines and a declared overall lifetime; for a covered outage, fit recovery into the remaining four-hour incident budget. Define when restored-workload readiness and verification begin, preserve the outage timestamp and deadlines across takeover, and have stale-attempt monitoring use the recorded attempt-kind deadline.

**Existing safeguards considered:** The four-hour outage-to-service RTO and unchanged outage timestamp are explicit and remain valid. The ambiguity is which phase/lifetime fields a recovery attempt records for executor and monitor agreement.

**Evidence:** [ARCHITECTURE-SPINE.md:277](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:278](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:279](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:282](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:287](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:288](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:311](../../ARCHITECTURE-SPINE.md)

**Reviewer findings:** REC-2

### VAL-06 · LOW · The recovery-contract milestone names a later consumer

**Proposed action:** autofix. **Confidence:** high.

The recovery-hook row names the first DR drill as its milestone even though staging recovery points and resets consume it earlier. The general consumer-waits rule already requires the contract before use.

**Consequence:** The milestone can mislead sequencing plans, but the existing rule prevents consuming an unavailable contract. This is planning clarity, not an unsafe architectural deferral.

**Recommended correction:** Align the Must precede cell with the earliest staging recovery-point or reset consumer, while preserving the general rule that consumers wait for shared contracts.

**Existing safeguards considered:** The First shared versions preamble says consumers wait, but the explicit first-drill milestone is later than the mandatory staging recovery-point/reset consumers. Move the named milestone to the earliest consumer.

**Evidence:** [ARCHITECTURE-SPINE.md:441](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:273](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:274](../../ARCHITECTURE-SPINE.md); [ARCHITECTURE-SPINE.md:277](../../ARCHITECTURE-SPINE.md)

**Reviewer findings:** RB-2

## Disposition of all reviewer findings

| Reviewer ID | Disposition | Reason |
| --- | --- | --- |
| RB-1 | VAL-01 | Merged independent reports of the same endpoint contradiction. |
| ADVR-1 | VAL-01 | Merged independent reports of the same endpoint contradiction. |
| ADVR-2 | VAL-02 | Retained; concrete residual in the current revision. |
| RB-3 | VAL-03 | Retained; concrete residual in the current revision. |
| ADVR-3 | VAL-04 | Retained; concrete residual in the current revision. |
| ADVR-4 | VAL-05 | Retained; concrete residual in the current revision. |
| RB-2 | VAL-06 — downgraded to low | First shared versions already requires consumers to wait. The reviewer agreed this is milestone clarity, not an unsafe deferral. |
| SEC-1 | VAL-07 | Retained; concrete residual in the current revision. |
| SEC-2 | VAL-08 | Retained; concrete residual in the current revision. |
| REC-1 | VAL-09 | Retained; concrete residual in the current revision. |
| REC-2 | VAL-10 | Retained; concrete residual in the current revision. |

## What already holds

- All 12 functional requirements and three nonfunctional requirements have architecture mappings.
- Release tiers, explicit authority, one-recovery policy, production entry gates and module-owned behavior are specified.
- Technology seed corrections are grounded in repository and primary-source evidence.
- Incomplete implementation generally has a named owner and gate; accepted risks are recorded and were not reopened.

## Limits and next step

This is an architecture consistency review, not proof that the implementation or deployment meets the contract. No application, cluster, repository settings or external service was changed.
The technology reviewer checked current repository and official-source evidence. This run did not re-audit live infrastructure, validate real credentials, execute recovery drills or run application tests.
Historical findings were not copied forward as defects. The current spine, accepted decision history and aligned spec govern this report.
Findings are architectural inferences and counterexamples, not reproduced runtime exploits. Primary-source citations substantiate the technology behavior; the report identifies the resulting contract gaps.

Roll the accepted findings into bmad-architecture Update, preserving AD-1 through AD-15; resolve the discuss items, apply the clear wording corrections, then refresh the affected spec companions with bmad-spec.
