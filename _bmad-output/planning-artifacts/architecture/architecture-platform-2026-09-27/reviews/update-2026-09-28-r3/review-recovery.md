# Recovery safety and liveness review — update run 3 (2026-09-28)

**Verdict: conditional pass.** REC-1/VAL-09, REC-2/VAL-10, VAL-01 and VAL-06 are closed in structure. The new in-place data-restore and recovery-timer wording still has four high residuals: startup order, post-cut live state, branch selection, and the recovery-mode mutation clause. Fix these before recovery and staging-reset stories are finalized.

**Counts:** 4 high, 8 medium, 3 low (15 findings). Actions: 8 autofix, 7 discuss.

Scope: the 485-line draft `ARCHITECTURE-SPINE.md`, the update-3 diff and memlog decisions starting at "Update run 3 started". Settled and not reopened: D5 (in-place recovery without fence, Keycloak restore or cutover), D10 (fence-and-reissue owners), the single-node/no-HA envelope, four-hour RTO within coverage, the RPO exceptions, the Administrator-only stop-clear and admission roles, and every implementation gate in Owned work. No spine, memlog, spec, code or cluster was changed. Line references (`L`) point to the current spine.

## Closure of prior findings

| Prior | Status | Evidence | Residual |
| --- | --- | --- | --- |
| REC-1 / VAL-09 (in-place restore skips preparation) | **Closed in structure.** The row now splits baseline re-deploy from data restore. It adds quiesce (close admission, remove workloads, keep data), deploys the recovery-point release by digest in recovery mode, keeps the promotion stop set, excludes fence, Keycloak restore and cutover, and gives reopen authority to Administrator or the deputy. | L281, L313 | REC3-1, REC3-2, REC3-3, REC3-4, REC3-5, REC3-10, REC3-11 |
| REC-2 / VAL-10 (release-only timers) | **Closed in structure.** Timers are split into release-kind and recovery-kind, with a lifetime per recovery kind, phase deadlines recorded at phase start, inheritance on takeover, and the monitor using the recorded lifetime. | L283, L282 | REC3-8, REC3-13 |
| VAL-01 (hook endpoint unreachable in place) | **Closed.** Step 4 names the executor that owns the attempt, and the endpoint admits only that environment's recovery-hook principal at the current epoch. AD-7 grants that principal per job. | L318, L128 | REC3-15 (low) |
| VAL-06 (hook contract milestone) | **Closed.** The contract must precede the first staging deployment, and the first AD-12 drill proves it. | L445 | None |

## Findings

### REC3-1 — The recovery release starts, and must reach readiness, before its keys and data are restored

**High · discuss · confidence medium-high**

Evidence: L281 ("then deploys the recovery point's retained release … in recovery mode and runs DR steps 3–6"); L313 (the in-place restore "deploys the recovery point's release in recovery mode and reuses steps 3–6"); L283 ("recovery-release readiness at phase start plus 10 minutes"); L316–L318 (step 2 deploys, step 3 restores keys, step 4 restores data and then invokes hooks "inside the owning module's restored workload"); L223 (per-start verify tasks run at every start); L228 ("Required generations gate readiness").

Failure: in place, the recovery-point release starts against the failed candidate's data. That data is non-readable by that release by definition, because the named recovery exists for missing or breaking compatibility evidence. Its per-start verify tasks and data-dependent readiness can crash-loop or never pass. Two outcomes follow:
- The 10-minute recovery-release readiness deadline expires before step 3 runs.
- The hooks, which must run inside that workload, cannot be invoked.

The in-place restore therefore fails in exactly the case it exists for. Restoring databases underneath running workloads also leaves stale connections and in-memory state. In DR the same order makes readiness depend on key and secret generations that step 3 has not yet restored.

Correction (spine wording): in the DR preamble's recovery-mode definition, add: "In recovery mode the release's workloads start only after steps 3–4 restore keys and data; its readiness phase starts then, and step 4's hooks run inside the started workloads." For in-place, add to L281: "Quiesce, restore keys and data with no application workloads running, then start the recovery point's release."

### REC3-2 — Post-cut live state survives an in-place restore and replays into the recovered release

**High · discuss · confidence high (broker), medium (Scheduler)**

Evidence:
- L281: "It restores no Keycloak, OpenBao or environment layer".
- L243: the broker is environment layer.
- L313: "no Subscriptions rendered until step 4 re-provisions the broker".
- L318: "Re-provision the broker and catch subscribers up from restored checkpoints".
- L227: "database backup alone does not recover broker backlog".
- L222: the live-authority-only recovery class.

Dapr 1.18 Scheduler, which is shared infrastructure, stores Jobs API jobs, actor reminders and workflow reminders in its embedded etcd (docs.dapr.io/concepts/dapr-services/scheduler, checked 2026-09-28).

Failure: in DR the broker and Scheduler are fresh. In place they are live and hold the failed candidate's post-cut state: unconsumed backlog, dead letters, consumer-group offsets, reminders, jobs and workflow timers. Rendering Subscriptions after step 4 delivers the candidate's incompatible messages to the recovery release. Reminders fire into it, bypassing the "only recovery-scope tasks" rule.

Rewinding EventStore also reuses sequence positions that post-cut messages already carried. Subscribers or dedup state that were not rewound can then drop new events as duplicates, or apply phantom ones. Live-authority-only stores keep post-cut writes as well. This is the backlog-before-hooks hazard that VAL-09 rejected, arriving by another route.

Correction: add to the In-place recovery row: "Before the recovery release starts, a data restore discards the environment's post-cut broker backlog, dead letters and consumer offsets and its Dapr jobs and reminders, and each module's purge hook clears its live-authority-only state beyond the cut. Step 4 then re-provisions the broker empty, and subscribers catch up only from the restored event store." Owners choose the mechanism (for example, topic re-creation, or per-app-ID Scheduler purge by the shared-infrastructure identity).

### REC3-3 — A failed or interrupted data restore is routed to baseline re-deploy

**High · autofix · confidence high**

Evidence: L281 ("**Baseline re-deploy**, after a failed, unverified or interrupted recovery, is a Helm upgrade of the recorded working baseline"). The data restore lists only two triggers: the named recovery and the staging reset.

Failure: a data restore is itself a recovery. If it fails verification or its executor dies mid-restore, the only in-place entry the row selects is baseline re-deploy. That is a normal-mode Helm upgrade with no quiesce, no recovery mode and no data restore, applied over partially restored or still-incompatible data with workers and Subscriptions enabled. In staging, the later candidate's attempt then waits on a reset that can never be re-run in place.

Correction: rewrite L281 as: "**Baseline re-deploy**, after a failed, unverified or interrupted automatic recovery or baseline re-deploy, … A failed, unverified or interrupted data restore re-enters as a data restore from the same recovery point; its quiesce makes re-entry independent of the partial state left behind." This preserves D5(a), which concerned the automatic recovery.

### REC3-4 — The recovery-mode mutation rule forbids the writes that step 4 and step 6 need

**High · autofix · confidence high**

Evidence: L313 ("only recovery-scope tasks mutate state until step 6 passes"); L320 (step 6 runs "restored-release smokes"); L281 and L286 (in-place verification is "the same as Automatic recovery", meaning the release's smoke checks at cadence); L234 ("Smoke writes use only synthetic identities and data"); L318 (subscribers catch up in step 4).

Failure: suppose recovery mode is enforced as written, for example by the gateway rejecting non-recovery commands. Then synthetic smoke writes fail, and subscriber handlers cannot update projections during catch-up. Step 6 cannot pass, so every DR and every in-place data restore fails verification. Alternatively, implementers carve out undocumented exceptions.

Correction: in L313, change the clause to "only recovery-scope tasks, subscriber catch-up of restored events and the attempt's synthetic smoke checks mutate state until step 6 passes."

### REC3-5 — Leaving recovery mode and reopening in place have no defined transition

**Medium · discuss · confidence high**

Evidence:
- L313: recovery mode disables workers and Subscriptions.
- L294: workers "stay disabled until each owning module has restored and reconciled", with no mechanism for turning them back on.
- L283: verification opens once waiting hooks complete.
- L281: "the result becomes the working baseline only after it passes, and Administrator or the deputy reopens".
- L233: user-ingress admission is Gateway state "changed only within an attempt".
- L36: the deputy's reopen authority; named writers are Administrator and the second owner.
- L321: DR's reopen is an explicit step inside its attempt.

Failure: nothing states five things:
1. When Subscriptions render. A Dapr activation that restarts every consumer inside the five-minute window can fail verification.
2. When workers re-enable.
3. Which render the attempt records as the working baseline. If it records the recovery-mode render, later recoveries reproduce it; if it records the normal render, that render was never verified.
4. Whether in-place reopen belongs to the recovery attempt.
5. What a staging reset does about reopening. The row requires a human reopen for it, which staging automation cannot perform.

If reopen is a separate attempt, a deputy who is not a named writer has no entry point to open ingress. That breaks deputy-alone recovery.

Correction: add to L281: "A data restore leaves recovery mode within its attempt. After step 4 it renders Subscriptions with Dapr activation before the verification window opens, and re-enables each module's workers once its hook reports external-operation state reconciled. It records the normal-mode render as its result. After verification it reopens as its final step, by Administrator or the deputy through the recovery entry point; a staging reset reopens itself. The stop stays set."

### REC3-6 — In place, steps 5–6 demand fence-dependent reissue and proof

**Medium · autofix · confidence high**

Evidence: L281 ("runs DR steps 3–6 … has no fence or cutover"); L319 ("Rotate realm signing keys unless the old issuer is proven unavailable … Each surviving authority's owner issues the replacement instance's credentials there"); L320 ("new credentials work and old ones fail at every surviving authority"); L315 (the fence is what revokes the old credentials).

Failure: in place there is no replacement instance and no fenced old instance. The literal step 6 proof requires the live instance's credentials to fail at the following authorities:
- backup store
- record store
- registry
- tombstone mirror
- event-export sink

Satisfying that means self-fencing, which breaks backups, record writes and the executor itself. Otherwise the proof cannot pass. The realm-key clause also rotates production realm signing keys on every named recovery, because the live issuer is "available". That forces a global re-login for no gain.

Correction: add to L281: "In place, step 5 rotates only restored in-environment credentials and keys, reconciles admission and re-applies revocations; surviving-authority reissue, realm signing-key rotation and step 6's old-credentials-fail proof apply only after a fence."

### REC3-7 — "No environment-layer restore" contradicts steps 3–4, and the recovery-point release is not checked for validity

**Medium · autofix · confidence high (wording), medium (staging case)**

Evidence: L243 (the environment layer includes "Data services, broker, OpenBao, tenant-key store, volumes"); L281 ("It restores no Keycloak, OpenBao or environment layer" versus "runs DR steps 3–6", which restore the tenant-key store and data services); L316 (DR step 2 requires the digest "within the recovered release's effective sets"); L230 (validity only within effective qualified sets); L215 (expand-only relative to the working baseline); L277 (staging contracts relative to production's working baseline); L278 (the reset point is "cut at the start of the unadopted candidate's attempt").

Failure 1 (wording): under the Terms, the row forbids the tenant-key and data restores it runs. Native data-service restores, such as a CloudNativePG recovery cluster, are environment-layer objects.

Failure 2 (validity): for a production named recovery, the point's release is the working baseline, and AD-15 keeps every forward-only input expand-only relative to it. Skipping Keycloak, OpenBao and the environment layer is therefore safe there. A staging reset point can instead bind a release older than production's current working baseline. Staging may already have contracted Components, keys, secrets or realm entries relative to that newer baseline, and the regenerated staging realm follows the current contract. Nothing in the in-place path checks this, unlike DR step 2.

Correction: rewrite in L281: "It re-applies no environment-layer definition and restores no Keycloak or OpenBao; only the tenant-key store and module data restore, through steps 3–4. It proceeds only while the recovery point's release is valid for the current environment (effective qualified sets and, in production, a valid production-promoted record), otherwise it stops for intervention."

### REC3-8 — Takeover inheritance, re-entry lifetimes and interruption semantics are release-shaped for recovery-kind attempts

**Medium · autofix · confidence high**

Evidence: L282 (an Administrator record, an in-place entry or a DR entry "may take it over"); L283 ("Takeover inherits every recorded value"; the DR lifetime is "the outage time plus four hours"; "An interruption detected before the verification deadline plus the grace fails the attempt, which may still use its one recovery"; "Restarts never add a recovery").

Failure: three cases.
1. An in-place or DR entry that takes over a stuck release attempt inherits that attempt's t0, rollout deadline, expired lifetime and "one recovery used". Read strictly, the deputy's recovery is born expired, and "never add a recovery" can bar it.
2. A DR re-entry after outage + 4 h records a lifetime already in the past. If that lifetime is enforced, no DR can run after the four-hour mark. If it is not, the monitor alarms from entry.
3. For a recovery-kind attempt interrupted mid-restore, "its one recovery" and "verification deadline" are undefined.

Correction: add to L283: "Only a replacement job resuming the same attempt inherits its recorded values. An Administrator-record, in-place or DR takeover records the prior attempt superseded and starts its own attempt with its own kind's values. A recovery-kind maximum lifetime triggers notification, never abort. A re-entry after it has passed records its entry time plus the declared bound, keeping the original outage time for RTO reporting. An interrupted recovery-kind attempt fails without an automatic recovery, and Administrator or the deputy re-enters."

### REC3-9 — Continuing stop sets can starve the revision-conditioned clear and the degraded override

**Medium · discuss · confidence medium**

Evidence: L287 ("a monotonic revision that every set record advances"; set by "a probe failure beyond a declared bound … The off-site monitor may set it"; the clear is applied "by compare-and-set only while that revision is unchanged"); L280 (the override names "the observed stop revision … never a set recorded after that revision"); L301 (precondition 1); L291 (probe every five minutes or less). L281 admits probe sources during quiesce, but L313's quarantine admits only the executor and recovery workloads.

Failure: nothing makes set records edge-triggered. In degraded production the probe keeps failing, which is the reason for the attempt, so a new set record can land between Administrator signing the override and the executor's precondition check. The attempt then stops without mutation, Administrator re-signs, and the race repeats every probe cycle. The same churn can defeat a stop clear while any continuing condition persists.

This is not a stop that nobody can clear, because Administrator can clear it once conditions stabilize. It is a livelock on the only path that repairs degraded production. Recovery-mode quarantine excluding the probe adds continuing sets during every recovery.

Correction: add to the Promotion stop row: "A set record advances the revision only for a new cause; a continuing condition, such as one probe-failure episode or a standing incident, is recorded once until it resolves." Align L313 quarantine with L281 so that probe sources stay admitted.

### REC3-10 — Which catalog generation an in-place recovery activates is unspecified

**Medium · discuss · confidence medium**

Evidence: L213 (no rollback set is recorded when "an Administrator-approved release names a separately planned recovery"); L226 ("Recovery commits the rollback generation"); L225 (environment-current values include "key and catalog generations"); L89 (application recovery renders the prepared rollback set); L281 (baseline re-deploy is "a Helm upgrade of the recorded working baseline").

Failure:
- Data restore: after a failed approved incompatible release, the committed generation is the candidate's, and no rollback generation was prepared. Deploying the recovery-point release "with environment-current values" gives the baseline host a generation whose adapters it lacks, so readiness fails or it routes candidate operations. Whether the restored data rewinds the catalog to the cut is also unstated.
- Baseline re-deploy: "the recorded working baseline" can be read as the baseline attempt record's old values, including its catalog generation, which is a rewind. The correct reading is the prepared rollback set.

Correction: rewrite in L281: "Baseline re-deploy renders the attempt's prepared AD-15 rollback set. A data restore activates, for the recovery point's release, the catalog generation recorded with its recovery point, re-committed as a forward generation at recovery-release readiness." EventStore should confirm the mechanism under its catalog protocol.

### REC3-11 — A failed approved release keeps its workloads running until the named recovery starts

**Medium · discuss · confidence high**

Evidence: L279 ("on a non-working outcome the executor sets the promotion stop, closes user ingress and stops; the named recovery then runs as an in-place recovery"); L281 (quiesce happens only when that later attempt starts, and a human starts it); L291 (outside coverage, response is unbounded); L294.

Failure: between the failure and the human start, which can be hours outside coverage, the candidate's consumers, scheduled and external-effect workers keep writing breaking data and performing provider calls. The restore will rewind all of it. This enlarges the lost-window external effects, including provider outcomes that must not be repeated blindly. It is the concrete failure REC-1 described, now narrowed to this gap. The fix changes the executor's terminal action; C-38/D5 intent is kept.

Correction: rewrite in L279: "… the executor sets the promotion stop, closes user ingress, removes the candidate's workloads keeping data objects, and stops …". Ingress is already closed, so users lose nothing further.

### REC3-12 — Recovery hook tasks already running at takeover are not epoch-fenced

**Medium · autofix · confidence medium**

Evidence: L318 (the endpoint admits only "the recovery-hook principal at the current epoch"; hooks run "inside the owning module's restored workload under its own least-privilege principals"); L282 (the new owner must fence at each authority, or revoke the previous job's credentials and wait "the declared maximum in-flight request duration"; "otherwise it stops for intervention"); L445.

Failure: the endpoint fences new calls only. A purge, re-provisioning or rebuild-only replay task started under epoch e keeps mutating under module principals after a takeover to e+1, and revoking the executor's credentials does not stop it. A DR re-entry, which has no quiesce, then runs the hooks concurrently. The takeover invariant cannot be met for these tasks, so the only remaining rule is stopping for intervention, and that intervention is the takeover itself.

Correction: add to the Recovery hook contract row (L445): "each recovery-scope task carries its invoking epoch and commits only while that epoch is current." Alternatively, add to L282: "a recovery takeover removes recovery workloads before re-invoking hooks."

### REC3-13 — A release-kind attempt holding the lock before its first mutation has no stale-attempt bound

**Low · autofix · confidence high**

Evidence: L283 (release-kind records t0 "at their first mutation", and the maximum lifetime is "derived from these"); L282 (the monitor uses the recorded maximum lifetime); L301.

Failure: a job that dies after taking the lock and epoch but before t0 holds the lock without a recorded lifetime. The monitor never reports it, and staging or production promotion blocks silently. The memlog rejected exactly this pattern for recovery attempts.

Correction: add to L283: "Every attempt records at lock acquisition a pre-mutation bound, which serves as its maximum lifetime until t0."

### REC3-14 — Pruning can remove the point a named recovery or staging reset needs

**Low · autofix · confidence medium**

Evidence: L290 ("Pruning skips points referenced by an open recovery"); L279 (the named point is recorded before the attempt); L278 (the reset point is the one cut at the start of the unadopted candidate's attempt); L289 (seven-day frequent retention).

Failure: the named point and the unadopted-candidate reset point are referenced before any recovery opens. If either is pruned, the named recovery has no point. The staging reset also becomes impossible, and every later candidate is blocked.

Correction: in L290, change the rule to: "Pruning skips points referenced by an open recovery, a pending named recovery or an unadopted staging candidate's reset."

### REC3-15 — The recovery executor's credential list omits the recovery-hook principal

**Low · autofix · confidence medium**

Evidence: L131 ("standing credentials only for … custodians release key, decryption and synthetic-client material per job"); L128 (every executor gets the recovery-hook principal for a recovery attempt it owns); L318.

Failure: the "only" list can be read as excluding the principal that step 4 requires in DR.

Correction: in L131, add "and the replacement environment's recovery-hook principal" to the per-job release list.

## Checked without a finding

- **Expand-only safety in production.** For an approved release's named recovery, the recovery point's release is the working baseline. AD-15 (L215) and the pre-attempt environment-layer union (L247) keep the realm, secrets, Components and admission groups compatible with it until the candidate is recorded working. Skipping the Keycloak, OpenBao and environment-layer restore is therefore safe there. The staging case is REC3-7.
- **DR step 5 admission reconciliation with the deputy acting alone.** The admission records are signed and stored off-cluster (L118). Reconciliation only removes principals (L319), so the deputy needs no Administrator action to reconcile, verify and reopen, and the event-export lag no longer resurrects revoked admission. VAL-08 is closed. In place, Keycloak is live and correct, and step 6 still proves "denial of revoked principals" for any restored admission projection.
- **Revision-conditioned clear.** A later set always prevails, and the degraded override is bound to one attempt and one revision. Recovery never needs the stop clear (the D5 precondition-1 scope), and Administrator-only clearing is a settled role. No stop exists that the role holder cannot clear; the residual is REC3-9's livelock.
- **Takeover mutation fencing** (L282), **release-kind timers** (unchanged in substance), the **four-hour DR lifetime within coverage**, the **staging-reset bound** (gated in Owned work, L469) and **recovery-mode ingress and worker disable on entry** match the memlog decisions.
- **Order from quiesce to recovery release.** Removing workloads before deploying the recovery release, and holding Subscriptions until the broker is re-provisioned, prevent consumers from draining before hooks run, as intended. The remaining ordering defects are REC3-1 and REC3-2.
