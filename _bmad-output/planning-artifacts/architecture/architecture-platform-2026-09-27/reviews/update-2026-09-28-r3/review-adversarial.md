# Adversarial review — update run 3 (2026-09-28)

**Verdict: FAIL.** The run-3 edits close VAL-01, VAL-03, VAL-06 and VAL-07 cleanly, and they close VAL-02 and VAL-04 at the rule level. The new recovery-kind and recovery-mode rules, though, leave three high-severity seams where two compliant units still build incompatibly. One of those seams partly re-opens VAL-08.

**Counts:** 0 critical, 3 high, 4 medium, 2 low. **Triage:** 2 autofix, 7 discuss.

**Scope and method.** I read the full spine (485 lines), the run-3 diff, the validation report being closed (VAL-01..VAL-10) and the memlog from "Update run 3 started", with D1–D10 checked for settled scope. I tested each edited clause against the unchanged rules it touches. For every edited clause, I tried to build two units, each obeying every AD and convention to the letter, that still fail together. I report a pair only if it survives a charitable reading. D1–D10, the AD adoptions, the accepted risks (single node, lost-window non-Memories erasures, in-place Kubernetes-minor downtime and so on) and implementation already assigned to a gated Owned-work or First-shared-versions row are not re-reported. I did not change the spine, memlog, spec, code or any cluster.

Line numbers refer to `ARCHITECTURE-SPINE.md` as reviewed (485 lines).

---

## ADV3-1 — A failed or interrupted recovery-kind attempt falls back to release-kind recovery

**Severity:** high. **Action:** discuss. **Confidence:** high.

**Evidence.**

- L281 (In-place recovery): "**Baseline re-deploy**, after a failed, unverified or interrupted recovery, is a Helm upgrade of the recorded working baseline." Data restore "first quiesces … then deploys the recovery point's retained release by digest in recovery mode and runs DR steps 3–6."
- L283 (Timing): "An interruption detected before the verification deadline plus the grace fails the attempt, which may still use its one recovery." For recovery-kind attempts the verification deadline is recorded only "once every hook whose module's reopening waits has completed". Also: "Takeover inherits every recorded value."
- L282 (Attempt ownership): the only takeovers are "an Administrator record, an In-place recovery or DR entry started by Administrator or the deputy, or a replacement job of the same workflow resuming the same attempt within the grace to perform its remaining single recovery".
- L286 (Automatic recovery): "One recovery: the AD-3 recovery render".

**Two units.**

- **Unit A** is the in-place data-restore runner on the production or staging executor. It quiesces, deploys the recovery release in recovery mode and is interrupted in step 4, with half the modules restored. It follows L283: the interruption comes before any verification deadline exists, so the attempt fails and "may still use its one recovery".
- **Unit B** is the deputy's documented in-place path (L281). The last outcome was "a failed … or interrupted recovery", so it runs **Baseline re-deploy**: a normal-mode Helm upgrade of the working baseline.

Both follow the text. B's render has no recovery mode, so Subscriptions and external-effect and destructive-retention workers return on half-restored data. The step 4 hooks and step 5 rotations never finish.

The "one recovery" wording has the same gap. A recovery-kind attempt has no defined one recovery. A replacement job "performing its remaining single recovery" could run the AD-3 render, which has no meaning here and may have no rollback set (L213). A second variant: Unit B's baseline re-deploy is release-kind, yet by takeover it "inherits every recorded value" of a recovery-kind attempt, including kind, lifetime and phase deadlines. The same happens when escalation to DR takes over an in-place restore and inherits the in-place Administrator-record lifetime instead of the outage-plus-four-hours rule.

**Consequence.** Recovery mode is lifted on inconsistent data by a compliant path. That breaks "never blindly repeat an unknown prior provider outcome" (L294) and can corrupt restored state. Independent executor, record-store and monitor implementations disagree on what a failed restore does next and which timers govern it.

**Safeguards considered.** The promotion stop stays set (L281, L287), which suspends promotion only. Recovery mode itself (L313) is not carried into the branch L281 selects. "Restarts never add a recovery" (L283) limits the number of recoveries but not their kind. The worker rule in L294 names no mechanism on the baseline re-deploy path.

**Minimal correction (Timing and interruption / In-place recovery).**

> A recovery-kind attempt has no automatic recovery. On failure or interruption it keeps recovery mode and the promotion stop, and stops for intervention. A data restore, or a DR entry started by Administrator or the deputy, may take it over. The taker resumes from the recorded phase, keeps the recorded kind, lifetime and deadlines, and uses the same or an earlier named recovery point. Baseline re-deploy follows only a failed or interrupted release-kind recovery, never a data restore that has begun quiescing. Escalating an in-place restore to replacement-capacity DR opens a new DR attempt that records the original outage timestamp.

---

## ADV3-2 — Recovery mode's write limit contradicts step 4 catch-up and step 6 smokes, and has no exit

**Severity:** high. **Action:** discuss. **Confidence:** high.

**Evidence.**

- L313 (Recovery mode): "no Subscriptions rendered until step 4 re-provisions the broker; … only recovery-scope tasks mutate state until step 6 passes."
- L318 (step 4): hooks run "inside the owning module's restored workload" in order "admission and purge, backend-principal …, then rebuild-only replay. Re-provision the broker and catch subscribers up from restored checkpoints."
- L319 (step 5): "Reconcile restored admission membership to the AD-6 admission records …". This runs after step 4.
- L320 (step 6): "restored-release smokes". L234: "Smoke writes use only synthetic identities and data".
- L205 (AD-14 Chains): asynchronous steps "re-check that actor's current admission against EventStore's admission projection".
- L249 (Dapr activation): a change to any "Subscription … restarts every consuming sidecar … before readiness or qualification passes."
- L281: "the result becomes the working baseline only after it passes, and Administrator or the deputy reopens." L233: user-ingress admission is "changed only within an attempt".

**Two units, three collisions.**

1. **Catch-up writes before admission is reconciled.**
   - **Unit A** is a module's subscriber runtime. It follows step 4 and catches up from restored checkpoints, so process managers and asynchronous task steps resume and issue commands.
   - **Unit B** is the EventStore gateway or a module guard. It follows L313 and rejects every non-recovery-scope write until step 6.

   If B is enforced, A's handlers fail and events move into poison capture. If B is not enforced, commands run in step 4, *before* step 5 reconciles admission to the AD-6 records. A principal whose revocation was lost before event export, which is exactly VAL-08's case, is still admitted in the restored projection. Its restored asynchronous steps execute. **This partly re-opens VAL-08.**
2. **Smokes write before step 6 passes.** A module guard that follows L313 rejects the step-6 smoke writes, because they are ordinary synthetic commands and not recovery-scope tasks. Verification can then never pass.
3. **Subscription render restarts pods during hooks, and nothing exits recovery mode.** Rendering Subscriptions in step 4 is a Dapr resource change. Under L249 it restarts the consuming sidecars, which means pods, while in-workload hooks may still be running in them.

   Separately, no rule says who exits recovery mode or when. Unit A (runner) records the attempt working at step 6. Unit B (reopen by Administrator or the deputy) must change ingress "within an attempt" and must enable external-effect workers. That is either an unverified render after the verified one, or a new attempt that nothing requires. The verified state (workers off) is not the reopened state (workers on).

**Consequence.** Divergent recovery-mode enforcement will either deadlock verification or let actor-carrying work run for revoked principals before reconciliation. The riskiest transition, enabling external-effect workers, is never verified.

**Safeguards considered.**

- Step 6's "denial of revoked principals" detects residual admission only after the step-4 actions have already committed.
- External-effect workers are disabled (L313), but internal cross-module commands are not external effects.
- DR step 7 exists, but it follows verification and does not re-verify.

**Minimal correction (Recovery mode and DR steps 4–6).**

> Until step 6 passes, only recovery-scope tasks and the owning executor's step-6 smoke writes mutate state. Admission reconciliation to the AD-6 records runs before any actor-carrying step resumes. Broker re-provisioning, Subscription rendering and subscriber catch-up move out of step 4 into a recovery-mode exit render inside the owning attempt, after step 5. That render keeps user ingress closed, enables external-effect and destructive-retention workers only per their owning module's reconciliation result, and applies Dapr activation. The step-6 verification window opens only after that render. Reopening then changes only user-ingress admission.

---

## ADV3-3 — The in-place recovery release is deployed and gated before its data and generations exist

**Severity:** high. **Action:** discuss. **Confidence:** medium.

**Evidence.**

- L281: data restore "deploys the recovery point's retained release by digest in recovery mode and runs DR steps 3–6". The deploy comes before the restore.
- L283: "recovery-release readiness at phase start plus 10 minutes."
- L225: "**Environment-current** values are read from the target environment at deploy and rollback …: … key and catalog generations with root digest, secret-contract digest …".
- L228: "Required generations gate readiness." L223: "per-start verify (idempotent)" tasks run on every start.
- L226: "Recovery commits the rollback generation." L213: "Unless an Administrator-approved release names a separately planned recovery, record the rollback set". So the named-recovery release is exactly the one exempted from the rollback set.
- L224: breaking means "its predecessor cannot read the state, events and security state it writes". L91: "key generations and durable authority never rewind."

**Two units.**

- **Unit A** is the Platform data-restore runner. Per L225 it reads environment-current values at deploy, which gives the failed candidate's committed catalog and key generation (Gc). It deploys the recovery point's release (the baseline) against the live, candidate-era data. Per L226 it commits "the rollback generation" at readiness, or has none, because L213 exempted this release.
- **Unit B** is a module's startup and readiness declaration plus EventStore's catalog and idempotency recovery. Per-start verify tasks and generation-gated readiness evaluate candidate-era state that, by the definition of breaking, the baseline cannot read. Step 4 then restores the committed generation and idempotency state to the cut (G0).

The recovery release either never becomes ready, because hooks need a ready workload and readiness needs restored data, or it is bound to Gc or Gr while the restored authority says G0. In the second case the catalog either moves backward (L91) or silently drops the candidate's retention entries.

**Consequence.** The only recovery path for a breaking Administrator-approved release, and every staging reset, can fail its own readiness phase or verify against mismatched generations. DR has the same deploy-before-restore order, but there it starts from empty stores. In place, the stores hold state the recovery release cannot read.

**Safeguards considered.** The recovery-hook contract lists "recovery mode" (L445), but nothing says recovery-mode readiness excludes state-dependent verify tasks. The Catalogs activation order prepares a rollback generation, but AD-15 exempts named-recovery releases from recording one, and nothing says whether a data restore commits it or the restored generation.

**Minimal correction (Recovery mode, In-place recovery, AD-15).**

> In recovery mode, a workload's readiness is the readiness of its recovery-hook endpoint. Per-start verify tasks and generation-gated readiness run after step 4 against restored state. A data restore binds the recovery release to the key and catalog generations recorded with its recovery point. After step 4 it commits a forward catalog generation holding the recovery point's routes plus every idempotency and key entry of the pre-restore committed and candidate generations as retention entries. That generation is prepared at entry, including for a release with a named recovery. Key and catalog generations never move backward.

---

## ADV3-4 — Reusing DR steps 3 and 5 in place conflicts with "restores no Keycloak, OpenBao or environment layer" and leaves two admission authorities

**Severity:** medium. **Action:** discuss. **Confidence:** medium-high.

**Evidence.**

- L281: data restore "restores no Keycloak, OpenBao or environment layer" yet "runs DR steps 3–6".
- L243: the Environment-layer tier holds "Data services, broker, OpenBao, tenant-key store, volumes …".
- L317 (step 3): "Restore the tenant-key store and re-apply tombstone key destruction". L91: "key generations and durable authority never rewind."
- L319 (step 5): "Rotate realm signing keys unless the old issuer is proven unavailable"; "Rotate the restored synthetic clients' credentials under the run's DR-scoped realm rights, released to the recovery executor"; "Reconcile restored admission membership to the AD-6 admission records"; "Re-apply admin and user revocations recorded after the cut".
- L118: "These records, not a restored realm, are the recovery authority for admission." L441: EventStore's admission projection is "fed from the realm admin-event export".

**Two units.**

1. **Keys.**
   - **Runner A** follows L281 and L243 and skips step 3.
   - **Runner B** follows "runs DR steps 3–6" and restores the live tenant-key store to the cut. That moves key generations backward, which contradicts L91.

   The two implementations disagree about which authority holds key generations after an in-place restore.
2. **Realm.** In place, "the old issuer" is the live production realm and is always available. A literal step-5 runner rotates live production (or staging) realm signing keys on every data restore, invalidating every session. A runner following "restores no Keycloak" skips it. The "released to the recovery executor" material does not exist on the production or staging executor.
3. **Admission, with two owners of one fact.**
   - **Unit A** is the EventStore admission projection. It is part of restored EventStore state or rebuilt from its restored export checkpoint, so in place it returns to the cut.
   - **Unit B** is Platform's in-place path. It restores no Keycloak, so step 5 has no "restored membership" to reconcile, and re-applying post-cut revocations in Keycloak does nothing because they are already there. No new admin event reaches the projection.

   The AD-6 records are declared the recovery authority for Keycloak membership. The projection the gateway and asynchronous re-checks actually read has no rule tying it to those records. It can re-admit a principal revoked after the cut.

**Consequence.** Implementations diverge on key-generation authority. Production realm keys may be rotated without need. After an in-place restore, the admission projection can disagree with Keycloak and with the admission records. Step 6's "denial of revoked principals" then either fails with no defined remedy or, with a sampled check, passes over residual admission.

**Safeguards considered.** VAL-08's record-first rule protects Keycloak membership in DR. The projection's declared staleness bound fails closed only on *stale*, not on *rewound-but-fresh* state.

**Minimal correction (In-place recovery; AD-6 Admission records).**

> In place, step 3 does not restore the tenant-key store. It verifies that the live store holds every key generation the recovery point references, and re-applies tombstone key destruction. Step 5 rotates only restored data-service and application credentials, and rotates realm signing keys only for a compromise-driven restore. The AD-6 admission records are the recovery authority for both Keycloak membership and EventStore's admission projection. Every recovery rebuilds the projection from the records and the export up to the present, not from a restored checkpoint, before any actor-carrying step resumes.

---

## ADV3-5 — The staging lifecycle clause keys on a client that chained and asynchronous steps do not carry

**Severity:** medium. **Action:** discuss. **Confidence:** medium-high.

**Evidence.**

- L234: "the admission predicate also admits the synthetic group for a tenant-creation command carrying the marker and for later operations and cleanup on a tenant whose EventStore-attested creation names **the same synthetic client**". Also "Confirmation-required flows are tested only through their UI surface" and "Per surface class, synthetic actor and workload clients".
- L204: "the workload is the authenticated client or, in server-to-server calls, the calling module's client or sidecar-attested app ID."
- L205 and L440: EventStore attests the *original actor* and *originating surface*, not the originating client. Asynchronous steps "re-check that actor's current admission against EventStore's admission projection."

**Two units.**

- **Unit A** is EventStore's staging predicate. It follows L234 and admits later operations on run-scoped tenant T only when the requesting client equals the synthetic client named at creation.
- **Unit B** is the module-declared tenant-lifecycle critical flow. Tenants creates T through the synthetic agent client. Downstream modules (Memories per-tenant principals, Parties) act on T through token-exchanged or asynchronous steps whose workload is *their own* service client (L204). Cleanup, if confirmation-required, runs through the synthetic *UI* client (L234).

Each unit obeys its rule. Every hop after creation that is not made by the creating client is denied, or is unknown and so fails closed.

**Consequence.** The accepted lifecycle flow still cannot pass end to end across modules, and run-scoped tenants leak when cleanup uses another surface. This is the same staging-gate blockage VAL-05 was meant to remove.

**Safeguards considered.** Serialized staging attempts (memlog, VAL-05) prevent cross-run confusion, but not cross-client denial. The marker-alone ban stays intact under the correction.

**Minimal correction (Synthetic identities; First shared versions admission-predicate row).**

> …and for later operations and cleanup on a tenant whose EventStore-attested creation names the same synthetic **actor**, directly or as the attested original actor of a chained or asynchronous step.

The marker-alone and production-exclusion clauses stay as written.

---

## ADV3-6 — The release-kind 10-minute rollout deadline governs infrastructure attempts that cannot meet it

**Severity:** medium. **Action:** discuss. **Confidence:** high.

**Evidence.**

- L283: "**Release-kind** attempts — every lock-covered change except a data restore or DR — record t0 at their first mutation … and the rollout deadline t0 + 10 minutes; their maximum lifetime is derived from these". The memlog explicitly assigns environment-layer and shared-infrastructure changes to release-kind.
- L284: "All required workloads reach the intended release and readiness by the rollout deadline."
- L244: a shared-infrastructure change "re-runs each environment's working-release smokes on that environment's executor"; its rollback is "Forward revert or DR entry". L243: environment-layer changes are "Forward only; restore is DR".
- L185: custodians "unseal each environment's OpenBao manually". L474: G1 requires Kubernetes 1.34 to a supported minor and OpenBao to a patched release. L485 accepts in-place Kubernetes-minor upgrades that take both environments down.
- L282: "A production attempt runs from lock to terminal outcome in one job."

**Two units.**

- **Unit A** is the shared-infrastructure workflow for the G1 Kubernetes minor and node-OS upgrade, or the environment-layer workflow for the OpenBao patch, which needs manual custodian unseal. Both are legitimate release-kind attempts whose first mutation starts t0.
- **Unit B** is the timing, monitor and verification implementation. It enforces t0 + 10 minutes and the derived lifetime.

Every such change misses the rollout deadline by construction and fails. Its only "recovery" is a forward revert, impossible for a Kubernetes minor, or DR. Separately, L244 spans two executors, while L282 requires one job.

**Consequence.** Mandatory G1 currency work and routine data-service or OpenBao upgrades are recorded as failed attempts. They set the promotion stop and push toward DR entry, or operators bypass the timer rule. The accepted downtime risk covers the outage, not the deadline semantics.

**Safeguards considered.** The accepted risk on in-place Kubernetes-minor downtime and the Administrator-record exception for urgent patches. Neither changes the deadline.

**Minimal correction (Timing and interruption).**

> The t0 + 10-minute rollout deadline applies to application-package rollouts. An environment-layer or shared-infrastructure attempt records, at entry, a declared per-workflow maximum duration for its infrastructure phase, including any custodian unseal. Its 10-minute readiness deadline starts when workload re-verification begins, and its maximum lifetime derives from both. A shared-infrastructure change records one attempt per environment, each on that environment's executor, under the held locks.

---

## ADV3-7 — The recovery-hook contract has no version window across retained releases

**Severity:** medium. **Action:** discuss. **Confidence:** medium.

**Evidence.**

- L445: the recovery hook contract now carries "recovery mode, internal recovery endpoint and its per-environment recovery-hook principal, result shape". It is due at "First staging deployment", so it will change before the first AD-12 drill proves it.
- L79: the release record binds many contract versions (for example the realm-contract version) but not the recovery-hook contract version.
- L131: DR runs "from a pinned off-site copy of the recovery workflows". In place, the runner is the production executor's current entry point. L249: only the environment *definitions* are carried "at every retained recovery point's configuration identity".
- L289: points are retained for 30 days, and a production working baseline can be months old. Compare L193, where the extension API supports "current and previous" majors, and L222, where declarations accept "the current and previous" major.

**Two units.**

- **Unit A** is the recovery runner at hook-contract version N+1: the current in-place entry point or the pinned off-site DR copy.
- **Unit B** is the recovery point's retained release, whose module workloads implement hooks, endpoint, principal validation and result shape at version N.

Each is correct for its own version. They meet only during a real restore.

**Consequence.** A restore of an older but valid recovery point fails at step 4, because the endpoint rejects the principal or the result shape is misread. This is likely because the contract gained fields in this very update and will keep changing until the first drill.

**Safeguards considered.** Recovery-point integrity and the compatible-release identity (L290) cover data and release, not the hook contract. The First shared versions rule says consumers wait for a contract, but it does not cover old producers.

**Minimal correction (Recovery hook contract row; AD-2 Record).**

> The release record binds the recovery-hook contract version its modules implement. The in-place entry point and the pinned off-site recovery workflows support every contract version bound by a rollback target or a retained recovery point's release. A contract major retires only after no such release remains.

---

## ADV3-8 — Staging reset now leaves the reset candidate's environment-layer objects (re-opened)

**Severity:** low. **Action:** autofix. **Confidence:** medium.

**Evidence.**

- L247 now reads "in-place recovery leaves the applied union in place". Before run 3 it said "DR and in-place recovery render them from the recovered release".
- L278: a staging reset is an in-place data restore that runs before the next candidate's attempt.
- L306 and L230: staging and production are compared by profile digest, which covers template and pins, not rendered environment-layer objects.
- L152: "A logical name may remain an option default for isolated module use".

**Two units.**

- **Unit A** is the staging reset. It keeps the union of production's baseline and the unadopted candidate C1, including C1-only Components, HTTPEndpoints and MCPServers.
- **Unit B** is the next candidate C2's staging attempt. It applies union(baseline, C2) and computes evidence. A C2 module whose declaration dropped a role but whose code falls back to the default logical name resolves C1's leftover Component in staging.

Production will receive only union(baseline, C2).

**Consequence.** Staging evidence passes against environment-layer objects production will never have, which masks a missing declaration. Before run 3, the re-render from the recovered release removed them.

**Safeguards considered.** AD-9's no-literal-name rule narrows but does not remove the default-name fallback. AD-15 expand-only rules are measured against production's working baseline, so removing C1-only objects in staging is permitted.

**Minimal correction (Staging reset).**

> Before the later candidate's staging attempt, a staging environment-layer attempt renders from the union of production's working baseline and the later candidate, removing objects only the reset candidate declared. Staging evidence records the rendered environment-layer digest.

---

## ADV3-9 — What happens to the stop after a working empty-or-degraded override is ambiguous under the revision model

**Severity:** low. **Action:** autofix. **Confidence:** medium.

**Evidence.**

- L280: the override "lifts the promotion stop and precondition 8 for that attempt only, never a set recorded after that revision; its terminal outcome re-sets the stop unless working."
- L287: "Only an Administrator record … clears it, applied by compare-and-set …; Empty or degraded production is the only exception."
- L225: "clear records only as Administrator records".

**Two units.**

- **Unit A** is the Builds store encoding. It treats the override as an attempt-scoped lift, so after a working outcome the stop is still set at the observed revision, and no executor may write a clear.
- **Unit B** is the release workflow. It reads "re-sets the stop unless working" as meaning a working outcome leaves the stop clear, and proceeds to the next release.

**Consequence.** The next release is either blocked by Unit A until an unexpected Administrator clear, or Unit B treats it as permitted while the store says it is stopped. This is a liveness and diagnosability mismatch, not a safety break: later sets still prevail.

**Safeguards considered.** The revision CAS already guarantees that a later set wins under either reading.

**Minimal correction (Empty or degraded production).**

> A working terminal outcome applies the override as a clear, by compare-and-set at the observed revision; any later set prevails. A non-working outcome writes a new set.

---

## Re-opened-hole check

- **VAL-08:** partly re-opened by ADV3-2, because step-4 catch-up runs before step-5 reconciliation, and by ADV3-4, because in place the admission projection is not tied to the records. The DR membership reconciliation itself is sound.
- **Environment-layer union change:** re-opens staging drift (ADV3-8).
- **VAL-01 (hook caller), VAL-03 (Dapr activation), VAL-04 (revision CAS), VAL-06 (milestone) and VAL-07 (surface from `azp` plus attested origin):** no earlier hole re-opened. Removing the audience-only alternative weakens no host that needs it, since the attested originating surface covers chains.
- **VAL-02 (takeover fencing):** closed at the rule level. The DR case, with standing prepared-capacity credentials, is satisfiable by minting per-job tokens from them (AD-7 already rotates them after every DR), so it is not reported.

## Considered and not reported

- **Takeover fencing with standing or personal credentials** (recovery executor; Administrator- or deputy-run Keycloak and OpenBao steps). Per-job tokens minted from standing credentials satisfy the invariant, and human steps are the intervention.
- **How the hook endpoint checks "the current epoch" when module workloads cannot reach the store.** The carrier belongs to the recovery-hook contract row (L445), and revoke-and-drain covers stale principals.
- **Verification window opening before step-5 rotation.** The DR sequence orders step 6 after step 5, and the charitable reading holds.
- **An AD-14 attestation carried in a header being a "caller-set header".** An EventStore-attested value is not caller-set.
- **Level-triggered monitor sets outrunning the revision-bound override.** A charitable monitor sets only when the stop is not already set.
- **Run-scoped tenants excluded by identifier rather than marker.** Pre-existing since D8, staging-only.
- **DR maximum lifetime already expired at entry.** It only triggers stale-attempt notifications.
- **Helm pending-upgrade state after revoke-and-drain versus the `helm rollback` ban.** This is a mechanism detail inside the gated takeover-fencing row.
- **"Administrator or the deputy reopens" after an automated staging reset.** A staging liveness nit: E2E uses executor sources.
