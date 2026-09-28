# Review — Operability & disaster recovery (round 2)

**Verdict: CONDITIONAL PASS.** The release-attempt state machine (sequences 1–2) can now be executed from start to finish, and every PRD number matches. All 19 round-1 findings are resolved or narrowed to smaller residuals. The conditions are 7 high findings, concentrated in three areas: whether the disaster-recovery (DR) sequence can actually be executed, how environment-layer and shared-infrastructure changes run on one node, and the PRD deputy role that the spine has not yet carried. Fix them before the recovery, production-deployment and environment-layer stories are finalized. None of them blocks the local, CI or staging-gate stories.

Scope read:
- Spine: all of it (working tree, 22:35).
- `.memlog.md`: grepped, plus the final entries L186–L208.
- PRD (working tree): FR-6–FR-10, NFR-1/NFR-2, release scope, glossary.
- Addendum: rollback, deputy, DR and hosted sections.
- Round-1 `reviews/validate-2026-09-27/review-operability.md`.
- EventStore `architecture.md`: AD-11/AD-22/AD-33 lifecycle and activation.
- Memories spine: AD-21 register continuity and the Deferred mirror row.
- Dapr mTLS documentation, for certificate validity.

**Numeric parity with the PRD is exact:**
- 10 min to readiness, then a 5 min verification window.
- 60 s continuous unavailability; sampling every 10 s or less.
- Second smoke failure at +30 s.
- Recovery: 10 min to readiness plus 5 min verification.
- Backup runs every 30 min; 7-day frequent and 30-day daily retention.
- Freshness checked every 15 min or less, with a warning before 1 h and a notification after 1 h.
- Availability probe every 5 min or less; hourly dead-man check.
- RPO at most 1 h; RTO at most 4 h.
- Drill before G2, then monthly, and after material changes.

## Round-1 findings: verification

| Round-1 | Status | Evidence in the spine / residual |
| --- | --- | --- |
| OPS-1 verdict totality, measurability | Resolved; residual | Verification row (L174): stable IDs, run at window start and then at a fixed cadence, a timeout counts as failure, a result from another release counts as missing, sampling every 10 s or less, a single restart does not fail the attempt, and the verdict is total. Residual: whether a retry inside the grace can rescue the attempt is ambiguous → OPS-9. |
| OPS-2 interruption | Resolved; residual | Interruption row (L171) lists the ambiguity conditions, and timers never restart. Residual: the grace, t0 and takeover bounds → OPS-9. |
| OPS-3 promotion stop | Resolved; residual | The stop is durable, set by every non-working outcome and every DR entry, and cleared only by an Administrator record. Pre-update health is defined, and a staging failure blocks only that candidate. Residual: incidents do not set the stop (PRD FR-8) → OPS-8. |
| OPS-4 record location, executor as single point of failure | Resolved; residual | Records live outside the cluster and the executor host; an attempt runs as one job; the off-site monitor checks for stale attempts; the production executor runs outside the cluster. Residual: the lock's location, and GitHub as the record store → OPS-9, OPS-1. |
| OPS-5 compatibility evidence | Resolved | Change classifications plus a staging rehearsal against the working baseline; two release modes. |
| OPS-6 rollback combination | Resolved | AD-15. The EventStore confirmation is deferred with an owner (L314). |
| OPS-7 Helm mechanics | Resolved | AD-3: `helm upgrade` of the retained package; `--rollback-on-failure` forbidden; "changed" defined; persistent objects kept outside the package. |
| OPS-8 lock scope | Resolved | Attempt ownership (L170). |
| OPS-9 recovery point | Resolved; residual | Definition, age and pruning (L178). Residual: tenant-key coverage and event-export lag → OPS-12. |
| OPS-10 tenant keys vs crypto-shredding | Resolved | AD-12 separate custody class; restore order (L180 steps 2–3). |
| OPS-11 detection/response | Resolved; residual | Off-site probe, declared response arrangements, worst case applied in the drill. Residual: what the drill can actually prove → OPS-13. |
| OPS-12 fence | **Partial** | The fence is now defined by credentials rather than network reachability, with per-instance prefixes and a proof step. But it targets authorities on the dead node and omits the shared authorities that survive → OPS-5. |
| OPS-13 monitor placement | Resolved | Off-site monitor, hourly dead-man check, GitHub recorded as an accepted risk. |
| OPS-14 artifact retention | Resolved | AD-2. |
| OPS-15 entry gates | Resolved; residual | G1/G2/G3 (L176). Residual: synthetic smoke identities versus the empty admission group → OPS-2. |
| OPS-16 Projects targets | Resolved | Source Precedence. |
| OPS-17 first deployment | Resolved | L175. |
| OPS-18 local lifecycle | Resolved | AD-10. |
| OPS-19 staging evidence | Resolved; residual | Serialized attempt and maximum evidence age. Residual: staging data lineage → OPS-10. |

## Sequence walk

| Sequence | Step | Owner | Gap? |
| --- | --- | --- | --- |
| 1 Staging gate | Take the staging lock (epoch) | Staging executor, Administrator-only operations repository | — |
| | Deploy production's working baseline to staging | Staging executor | **OPS-10**: staging may hold writes from an unadopted breaking candidate, and no rule resets it. Pre-G1 there is no baseline, which implicitly routes to approved mode (acceptable). |
| | Upgrade to the candidate; run E2E, including McpCli flows | Staging executor; modules own the flows | — |
| | AD-15 rehearsal: baseline reads what the candidate wrote | Staging executor | A failure is breaking evidence (clear). The state staging is left in is undefined → OPS-10. |
| | Evidence binding and maximum age | Builds encoding, Platform | — |
| 2 Production attempt | Preconditions, including "healthy now" | Production executor | A failed precondition does not set the stop → **OPS-8**. At G1, the smoke synthetic actor is refused by the empty admission group → **OPS-2**. |
| | Lock and epoch | Record store outside cluster and executor host | The lock is not in the "outside" list, and takeover does not increment the epoch → OPS-9. |
| | Prepare catalog generations; ready-validate the rollback generation | Production executor, EventStore protocol | Whether this falls inside the 10-min deadline (t0) is undefined → OPS-9. |
| | Write the production-promoted record before traffic | Production executor (Platform issuer) | Never invalidated when the attempt fails → **OPS-14**. |
| | Helm upgrade; readiness within 10 min; commit at readiness | Production executor | — |
| | 5-min verification and its triggers | Production executor | Meaning of the grace → **OPS-9**. |
| | Working / failed → one recovery (10 + 5 min) → promotion stop | Production executor | — |
| | Notify | Executor → GitHub issue to Administrator | The deputy is not notified → **OPS-1**. |
| | Executor host dies mid-attempt | Off-site stale-attempt check; Administrator record | The replacement-job takeover "within the grace" is practically unreachable (OPS-9). The deputy cannot take over (OPS-1). The outcome is safe but manual: the lock stays held while an unverified release serves. |
| 3 Environment-layer / profile change | Both locks, staging first, one change owner | Production executor (cluster-scoped identity) plus staging executor | **OPS-3**: running across two executors and two jobs is undefined, and "staging first" qualifies nothing for components both environments share. |
| | Apply forward-only; production re-verifies the working release and renews its production-promoted record | Production executor | A failed re-verification is only "retain and report" (**OPS-3**). The qualified sets are immutable, so the baseline becomes undeployable (**OPS-4**). |
| | Kubernetes minor / CNI / ingress / node OS | Administrator | No version authority; an in-place upgrade takes down both environments (**OPS-3**). |
| 4 Approved / incompatible release | Administrator record replaces G3 and compatibility | Administrator | — |
| | Separately planned recovery | ? | Execution owner, lock/epoch handling, DR entry and a pre-attempt recovery point are all undefined → **OPS-11**. |
| 5 Disaster recovery | Detection (probe every 5 min or less → issue) | Off-site monitor → Administrator | Deputy not notified → OPS-1. |
| | DR entry (new epoch); start the recovery executor | Administrator (dispatch needs write access to the operations repository) | The deputy cannot start it, and it depends on GitHub → **OPS-1**. |
| | *(missing)* Reproduce the environment layer; restore Keycloak/OpenBao; deploy the release from the registry replica | — | **OPS-6** |
| | 1 Fence | Recovery executor | Proves against dead authorities and misses the surviving ones → **OPS-5**. |
| | 2 Restore the tenant-key store; re-apply tombstones | Recovery executor; Memories/EventStore mirror | Mirror owned before G2 (L320). The key coverage of the cut → OPS-12. |
| | 3 Restore into quarantine; run hooks in dependency order | Recovery executor; module hooks | Hooks are recovery-scoped startup tasks, so the release must already be deployed; that order is undefined → OPS-6. |
| | 4 Rotate credentials; replay revocations; repeat the fence proof | Recovery executor | Production-realm writes are Administrator-only (AD-6) → OPS-1. Restoring Keycloak's database conflicts with AD-6 → OPS-6. |
| | 5 Verify | Recovery executor | Whether reopening waits for rebuild-only replay → OPS-13. |
| | 6 Resume protection; reopen | Unstated | The spine names no reopen authority (the PRD grants it to the deputy) → OPS-1. What happens after DR → OPS-15. |
| | 4-hour RTO with one operator | Administrator/deputy | Unproven; feasible only if the environment layer is pre-provisioned on the prepared capacity. The drill proof is weak → OPS-13. |
| | Keycloak lost / registry on the failed node / GitHub down | — | OPS-6 / OPS-6 (pull by digest from the replica) / OPS-1 (only the notification risk is accepted) |
| 6 Backups and monitoring | Runs every 30 min; freshness every 15 min or less; warn below 1 h, notify above 1 h; pruning | Platform, Administrator, off-site monitor | — |
| | Definition of a complete set | Platform | Tenant-key coverage and Keycloak export lag → **OPS-12**. |
| | Memories tombstone continuity; broker backlog | Memories/EventStore (G2); broker qualification (L315) | Owned. Where they sit in the DR sequence → OPS-6. |
| 7 Drills | Before G2, monthly, after material changes | Administrator | What the drill can prove → **OPS-13**. |
| Other | Observability | Shared telemetry; sink and retention deferred to G2 (L319) | Failure domain → OPS-16. Disk headroom → OPS-7. |
| | Capacity/cost | Deferred to G2; no cost target | — |
| | Runbook ownership | Platform owns the sequence, Administrator the drill, modules the hooks | No deputy runbook → OPS-1. |
| | Certificate/DNS/credential expiry; OpenBao seal after restart | None | **OPS-7** |
| | Kubernetes upgrade; Dapr control plane and sidecar skew | Currency only before G1 | **OPS-3**, **OPS-4** |

## Findings

### OPS-1 — The deputy recovery role, and a way to start recovery without GitHub, are missing from the architecture
- **Severity:** high
- **Location:**
  - Spine: Roles (L29), AD-6 (L87), AD-7 (L93), Attempt ownership (L170), Diagnostics and notification (L159), DR sequence steps 4 and 6 (L180), Accepted risks (L328).
  - PRD: L34, FR-8 L203–L204, FR-9 L225.
  - Addendum L225: "Architecture follow-up is required … current Administrator-only operations access and notification rules do not yet implement deputy recovery authority."
- **Finding:** The spine names the deputy only as a backup for "recovery and key custody". Everything the PRD lets the deputy do is still Administrator-only or impossible:
  - (a) Every notification goes to issues "assigned to Administrator"; the PRD requires delivery to Administrator **and** the deputy.
  - (b) The recovery executor runs only recovery workflows from an operations repository that only Administrator can write to. Dispatching a GitHub workflow requires write access, and granting it triggers the GitHub Team revisit.
  - (c) DR step 4 needs production-realm writes: revocation replay, client-secret rotation, and realm keys when the restore is compromise-driven. AD-6 makes production realm changes Administrator-only.
  - (d) Only an Administrator record can take over a stuck attempt.
  - (e) The spine names no reopen authority.
  - (f) Starting DR depends on GitHub: dispatch, the workflow source, and possibly the record store (the memlog offers GitHub deployment records as the example). The accepted risk covers only notification through GitHub.
- **Failure scenario:** Administrator is on a flight. The node's disk fails on Saturday at 10:00, inside declared coverage. The probe's issue is assigned to Administrator only. If the deputy notices anyway, they cannot dispatch the recovery workflow or replay revocations. The RTO is missed inside coverage. Every drill "passed", because Administrator ran them.
- **Suggested fix (rule wording):**
  - "Every notification is assigned to Administrator and the recovery deputy."
  - "Administrator or the deputy starts the recovery executor under their own MFA identity. It runs from a pinned, off-site copy of the recovery workflows and needs neither write access to the operations repository nor GitHub. Attempt records and the working baseline are readable without GitHub."
  - "During DR, and when taking over a stuck attempt solely to perform its documented recovery, the deputy holds DR-scoped production-realm rights. Those rights are limited to restoring existing authority: replaying revocations and rotating credentials and keys."
  - "Administrator or the deputy reopens after verification. Only Administrator clears the promotion stop or administers admission."
  - If the GitHub dependency is kept instead, record it as an accepted risk to the DR path.
- **Suggested disposition:** discuss. It touches the RV-1 single-writer decision and AD-6.

### OPS-2 — Synthetic smoke identities cannot pass production admission while G1 requires an empty admission group
- **Severity:** high
- **Location:**
  - Spine: Production entry gates G1 (L176), AD-6 (L87), Synthetic identities (L158), Before production update (L172), Verification (L174).
  - PRD: L52 and L56. Addendum L185: "Architecture must carry this restricted qualification step alongside its initial empty-admission-group rule."
- **Finding:** Several rules collide:
  - G1 requires the production admission group to be empty.
  - AD-6 makes admission a validated precondition for every production call.
  - Smoke suites use synthetic *actor* clients, and the pre-update health check needs their smoke results to pass.
  - The spine gives flagged synthetic actors no standing admission, and it does not carry the PRD's temporary SM-4 test grant.

  Between G1 and G2, every production attempt therefore fails verification. The alternatives are worse: add synthetic actors to the group (violating G1) or have the gateway skip admission for "synthetic"-flagged tokens (an admission bypass).
- **Failure scenario:**
  - Team A: the first G1 deployment's Tenants smoke check gets a 403, so it is a first-deployment failure. SM-5 rehearsals and every pre-G2 attempt become impossible.
  - Team B: the gateway exempts the synthetic flag from admission. A leaked synthetic client token then bypasses admission policy.
- **Suggested fix:** "Flagged synthetic actors are admitted through a separate, Administrator-granted synthetic admission that authorizes only the synthetic tenant. G1's empty group is the human production-user group. The PRD's SM-4 test identity is a temporary member of that group, admitted and revoked by Administrator with a record."
- **Suggested disposition:** autofix, because the policy is already decided in the PRD. Cross-check with the security lens.

### OPS-3 — Environment-layer and shared-infrastructure changes have no qualification venue and no failure path on one node
- **Severity:** high
- **Location:** Production profile (L154); Attempt ownership (L170); AD-3 forward-only (L69); AD-7 cluster-scoped identity held only by the production executor (L93); Automatic recovery "retain and report" (L175); Deferred Infrastructure currency (L318).
- **Finding:**
  - (a) "Staging first" qualifies nothing for components both environments share: the single Keycloak server, the Dapr control plane, the ingress controller, the certificate issuer, the CNI, the storage provisioner and the node itself. Applying such a change "to staging" applies it to production at the same moment.
  - (b) A shared change runs on two executors: the shared mutation needs the production executor's cluster-scoped identity, and staging verification runs on the staging executor. Both run under one change owner holding both locks. The rules for one job from lock to terminal outcome, and for the epoch, are written for a single executor.
  - (c) If production re-verification fails after a forward-only change, the rule yields "retain and report" with production broken. Nothing requires a recovery point before the change or a decision between reverting forward and entering DR.
  - (d) Kubernetes minor, node OS/kernel, CNI (which NFR-3 enforcement relies on), ingress controller, certificate issuer, OpenEBS provisioner and the CloudNativePG operator have no inventory pin. Their changes therefore bypass the profile digest, staging-first and re-verification.
  - (e) An in-place Kubernetes minor upgrade drains the only node, takes down both environments and cannot be downgraded. The currency rule forces one roughly every year. The outcome of the "currency checked at each production attempt" check is not stated.
- **Failure scenario:**
  - Kubernetes 1.34 is upgraded to 1.35, as required before G1. The CNI upgrade that comes with it changes how NetworkPolicy is enforced. Nothing is in the profile inventory, so neither re-verification nor the NFR-3 negative tests run again, and staging-to-production isolation is silently lost.
  - Separately, a Dapr control-plane patch breaks sidecar injection. Production re-verification fails, the rule says "retain and report", and production stays down until a forward fix is found by hand.
- **Suggested fix:**
  - "Every cluster-level component (Kubernetes minor, node OS, CNI, ingress, certificate issuer, storage provisioner, operators) is an environment-layer pin with one version authority."
  - "A shared-infrastructure change is rehearsed on a production-profile copy on the prepared capacity before it touches the shared node. It starts only after a complete recovery point cut after both locks are taken. Its production verification re-runs the working release's smoke checks and the NFR-3 negative tests."
  - "A failed verification is a non-working outcome. Its recovery is a forward revert where the component supports one, otherwise DR entry."
  - "A failed currency check blocks automatic promotion and opens an environment-layer change."
  - Record the two-environment outage of an in-place Kubernetes upgrade as an accepted risk, or use the prepared capacity to replace the cluster instead (blue/green).
- **Suggested disposition:** discuss.

### OPS-4 — Immutable qualified sets make every environment-layer security patch "breaking", and the sidecar patch is not pinned
- **Severity:** high
- **Location:**
  - AD-2 (L63): the release record binds the "qualified environment-layer digest sets".
  - Production profile (L154): "deployable, and a valid rollback target, only while current environment-layer versions fall within its qualified sets … otherwise the change is breaking"; "renewed production-promoted record"; "One sidecar patch per release across local, CI and staging".
  - Memlog V-13: "workloads pin the Dapr sidecar runtime image". This is absent from the spine.
- **Finding:** The qualified sets are release-invariant, and the publication workflow writes them once. A security patch published after a release falls outside every existing release's sets. The environment-layer attempt renews only the production-promoted record. Implementers can read this two ways:
  - **Strict reading:** after the first patch, the working baseline is neither deployable nor a valid rollback target. AD-15 then cannot be satisfied, automatic promotion stops, and the release named by a recovery point cannot be restored onto current pins either.
  - **Lenient reading:** the renewal silently extends the sets, and releases run on combinations nobody qualified.

  Separately, the sidecar patch is not bound in the release record or pinned per workload. With the injector's defaults, a control-plane patch changes the working release's sidecars at the next pod restart, without re-verification. Nothing checks the skew between the new control plane and retained rollback targets or recovery-point releases.
- **Failure scenario:** The OpenBao 2.6.3 security patch is applied. The working baseline R7 is qualified on 2.6.2.
  - Executor A refuses R7 as the rollback target, so R8 is forced into approved mode, and so is every candidate after each later patch.
  - Executor B promotes R8 automatically.

  Both claim compliance.
- **Suggested fix:**
  - "An environment-layer attempt that re-verifies a release under new versions issues a Platform qualification record extending that release's qualified sets. The release record stays immutable; a release's effective sets are its record plus those extensions."
  - "Each release record binds the Dapr sidecar patch, which workloads pin. An environment-layer change is breaking for any retained rollback target or recovery-point release it would put outside the control plane's supported skew."
- **Suggested disposition:** autofix. Fold it into the EventStore "renewed production-promoted records" confirmation already deferred at L314.

### OPS-5 — The DR fence proves against authorities on the dead node and misses the shared ones that survive
- **Severity:** high
- **Location:** DR sequence steps 1 and 4 (L180); Lost window and external effects (L183); Memories erasure continuity (L182); AD-6 event export (L87).
- **Finding:** Step 1 revokes the old environment's "database, broker, OpenBao, backup-write and deployment credentials" and requires proof that old credentials fail. With the node dead, its databases, broker and OpenBao are unreachable: revoking there is impossible and the proof is vacuous. Step 4's repeat against the restored instances is the real proof. The fence never lists what a returning node can still write to:
  - the off-site tombstone mirror;
  - external providers, since each environment has its own provider tenancy;
  - the off-cluster Keycloak event-export sink;
  - the attempt-record store;
  - the registry.

  "Disable external-effect workers" can only apply to the new environment; on a returning node those workers restart with valid provider credentials. A returning node can also serve tache.ai to clients that still hold the old DNS entry.
- **Failure scenario:** Power returns and the old node boots 3 hours into DR.
  - Its Folders workers resume provider mutations using production provider credentials that are still valid. The restore later has to reconcile those effects as "unknown".
  - Its Memories instance appends to the off-site mirror, so two lineages write to it. After reopening, Memories finds an unknown lineage and fails closed.
- **Suggested fix:** "The fence revokes the old environment instance's credentials at every authority that survives the failure: the off-site backup store, tombstone mirror, external providers, Keycloak event-export sink, registry and record store. The old node stays isolated from the network and DNS until it is re-imaged. The fence proof runs against those surviving authorities before step 2. Old in-environment credentials are invalidated by step 4's rotation and proven against the restored instances."
- **Suggested disposition:** autofix.

### OPS-6 — The DR sequence omits the steps that make step 3 executable, and Keycloak restore authority is unresolved
- **Severity:** high
- **Location:** DR sequence (L180); AD-12 (L123); Startup task lifecycle, recovery scope (L148); AD-6, "no automation, executor or fixture holds master or cross-realm admin" (L87); Workflows and provenance, registry off-site replica (L155); Domain truth and delivery, broker backlog (L152); First shared versions, recovery hook contract (L299).
- **Finding:**
  - No step reproduces the environment layer on the prepared capacity: cluster, CNI, Dapr control plane and trust root, OpenBao unseal, data services, broker, ingress and issuer. No step deploys the recovery point's compatible retained release. Yet recovery hooks are declared as *recovery-scoped startup tasks*, which run inside deployed workloads.
  - Keycloak, the environment's OpenBao and the registry replica have no position in the sequence. Step 4's revocation replay and step 5's access checks need Keycloak, and image pulls need the replica when registry.hexalith.com is on the failed node.
  - Keycloak is one server holding the master, staging and production realms. Restoring its database is a master-level, cross-realm operation that AD-6 bars the recovery executor from holding. It also brings the staging realm into production recovery capacity. The observed Keycloak database currently has no backup (memlog L129).
  - No step re-provisions the broker or catches subscribers up from their restored checkpoints by EventStore replay, even though L152 says a database backup does not recover broker backlog.
  - The DNS and certificate cutover for tache.ai and the issuer hostnames is on the RTO path but not placed.
- **Failure scenario:** Parties writes its hook assuming Dapr state and OpenBao are up. Memories assumes its hook runs before any workload starts. The executor deploys after step 3, so the Parties hook fails. The order gets settled ad hoc during the first drill, on RTO time.
- **Suggested fix:**
  - Add "Step 1b: reproduce the environment layer from the profile inventory on the prepared capacity. Restore Keycloak and the environment OpenBao into quarantine. Deploy the recovery point's release from the off-site registry replica, by digest, with ingress closed, promotion stopped, and external-effect and destructive workers disabled. Recovery hooks may assume exactly this state."
  - "Broker re-provisioning and subscriber catch-up from restored checkpoints precede step 5. The DNS cutover belongs to step 6."
  - Name the Keycloak database restore as a DR-scoped operator step for Administrator or the deputy, with custody, as an explicit AD-6 exception. Alternatively, restore only the production realm.
- **Suggested disposition:** autofix for the ordering; discuss for Keycloak restore authority.

### OPS-7 — Outage triggers for the whole installation have no owner and no early warning
- **Severity:** high
- **Location:** Diagnostics and notification (L159): the monitor covers only the probe, freshness and stale attempts. Secrets (L153): renewal owners exist only for bootstrap tokens, and the unseal custodians are Administrator and the deputy. Hosted interfaces, ACME (L157). AD-7: pre-provisioned recovery credentials (L93). Deferred Secrets row: the 2027-07-19 expiry (L316).
- **Finding:**
  - **Expiry.** Time-bound trust material has no inventory, owner or expiry check:
    - Dapr's Sentry-generated root and issuer certificates. They are valid for one year by default, and when they expire, mTLS fails for every sidecar in both environments ([Dapr mTLS docs](https://docs.dapr.io/operations/security/mtls/)).
    - ACME certificates and DNS-challenge credentials.
    - Kubernetes PKI and Keycloak realm keys.
    - Executor/runner registrations and synthetic credentials.
    - Off-site storage and backup keys.
    - The recovery executor's pre-provisioned credentials.
    - The hexalith.com and tache.ai domain registrations.

    The probe detects any of these only after the outage. An expired recovery-executor credential surfaces at the next monthly drill or in a real DR.
  - **OpenBao seal.** The unseal custodians are people. After any node restart (power loss, kernel patch, Kubernetes upgrade), both environments' OpenBao are sealed, and every restarting workload fails its secret reads until a custodian unseals. Outside response coverage, production stays down. The spine neither chooses auto-unseal with an off-node key nor records the manual path as accepted, and the monitor does not check seal state.
  - **Disk.** On single-node local storage (OpenEBS hostpath, memlog L64), running out of volume space stops the data services of both environments. One example is a WAL-archive backlog when off-site upload fails. Nothing warns of low headroom.
- **Failure scenario:** The generated Dapr root certificate reaches its one-year expiry. Every sidecar in staging and production loses mTLS. The probe raises an issue within 5 minutes, and an operator then has to discover the renew-and-restart procedure under outage pressure.
- **Suggested fix:**
  - "Every time-bound certificate, credential, token and domain registration in the recovery inventory and the profile has a named renewal owner. The off-site monitor warns a declared lead time before each expiry and at every drill."
  - "Each environment declares its OpenBao seal policy: auto-unseal with off-node key custody, or manual unseal counted within response coverage. The monitor reports seal state and node volume headroom."
- **Suggested disposition:** autofix for the inventory and monitor; discuss for the unseal policy.

### OPS-8 — Incidents detected by the probe do not set the promotion stop
- **Severity:** medium
- **Location:** Automatic recovery (L175); Detection and response (L179); Before production update (L172). PRD FR-8 L208: "An incident establishing that production is no longer working sets or retains the promotion stop until Administrator records a verified baseline."
- **Finding:** Only attempt outcomes and DR entry set the stop. A probe-detected outage, or a failed pre-update health check, only notifies. Once production recovers, whether by itself or by an unrecorded manual action, the next automatic promotion runs. No Administrator-verified baseline is ever recorded.
- **Failure scenario:** Working release R7 has a latent defect. The probe fails intermittently overnight and someone restarts pods. At 09:00 production passes the health check, R8 is promoted automatically, and R7 remains the rollback target.
- **Suggested fix:** "A failed pre-update health check, a probe failure lasting beyond a declared bound, or a recorded incident sets the durable promotion stop. The off-site monitor may set the stop but never clear it."
- **Suggested disposition:** autofix.

### OPS-9 — The grace, timing anchors and takeover are under-specified
- **Severity:** medium
- **Location:** Attempt ownership (L170); Interruption (L171); Rollout (L173); Verification (L174).
- **Finding:**
  - "Grace" appears three times: for an in-flight retry, for interruption detection, and for replacement-job takeover. The spine does not say whether it is one value, where it is recorded, or what its maximum is.
  - "otherwise failed after one bounded grace for an in-flight retry" leaves open whether a retry that passes inside the grace yields "working". The PRD reading (L189) is that a grace "may finish an in-flight retry".
  - GitHub never re-runs a job by itself, and the monitor only notifies. A takeover "within the grace" is therefore practically unreachable, so an executor dying during verification leaves the lock held and an unverified release serving. That outcome is safe, but the spine should state it, together with a maximum lifetime derived from the timers.
  - The lock is not in the list of records kept "outside the cluster and executor", and a takeover does not say it increments the epoch.
  - "Deployment start" does not say whether catalog preparation and rollback ready-validation fall inside the 10-minute deadline. If they don't, the phase before the Helm upgrade is unbounded while the lock is held.
- **Failure scenario:** Check X fails at 4:45. Its retry at 5:15 passes. Executor A declares the release working; executor B declares it failed and rolls back.
- **Suggested fix:** "Each attempt records t0 at its first mutation (catalog preparation), the rollout deadline t0 + 10 min, and one attempt grace no longer than the check timeout plus 30 s, used for in-flight retries, interruption detection and takeover. A retry that completes within the grace is the check's latest result. The lock and its epoch live with the attempt record, and a takeover increments the epoch. The maximum lifetime is derived from these values and recorded."
- **Suggested disposition:** autofix.

### OPS-10 — After an unadopted candidate, staging data can block every later gate
- **Severity:** medium
- **Location:** Staging gate (L168); AD-15 (L141); Startup task lifecycle (L148).
- **Finding:** Each staging attempt first deploys production's baseline onto staging data, and that data holds the writes of every earlier candidate, including breaking ones never promoted. AD-15's no-retirement rule is keyed to "recorded working", a state staging never reaches. After an abandoned breaking candidate, the baseline cannot read staging's state, so every later candidate fails the gate's first step. No rule returns staging to a state the baseline can read, and creation tasks run only when a new environment is created.
- **Failure scenario:** R9 writes an event that R7 cannot read. The rehearsal fails, the evidence says breaking, and R9 is abandoned. R10's attempt deploys R7, whose projection poisons on R9's events: a staging failure, so R10 is blocked, and then R11.
- **Suggested fix:** "After a candidate that production does not adopt, staging is returned to a state its production working baseline can read, as its own staging attempt before the next gate. It is either restored from a staging recovery point cut before that candidate, or recreated with its creation tasks and synthetic data. Staging catalog and key retirements follow AD-15 relative to production's working baseline."
- **Suggested disposition:** discuss.

### OPS-11 — The planned recovery for an approved incompatible release has no execution rules
- **Severity:** medium
- **Location:** Release modes (L169); AD-15, "Unless an Administrator-approved release names a separately planned recovery" (L141); Attempt ownership (L170).
- **Finding:** When an approved incompatible release fails verification, the spine does not say whether the executor:
  - runs the planned recovery in the same job and under the same lock;
  - stops, and if so with user ingress open or closed;
  - treats a planned recovery that restores data as a DR entry (new epoch, fence, stop).

  No rule requires a recovery point cut immediately before the attempt. A restore-based plan can therefore lose one backup interval plus every write made during the attempt.
- **Failure scenario:** R12 (breaking schema) is approved at 18:00 with the plan "restore the pre-release backup". Verification fails at 18:12. The newest complete recovery point was cut at 17:31. The executor stops and Administrator is unreachable. Users keep writing under R12 until morning, and the plan then loses everything from 17:31 onward.
- **Suggested fix:** "An incompatible approved attempt starts only after a complete recovery point is cut after its lock is taken. On a non-working outcome, the executor sets the stop, closes user ingress and stops. The named recovery runs as a DR entry under a new epoch, by Administrator or the deputy, with the same verification."
- **Suggested disposition:** discuss.

### OPS-12 — The complete recovery-point definition omits tenant-key coverage and event-export lag
- **Severity:** medium
- **Location:** Recovery point and freshness (L178); AD-12 tenant-key custody class (L123); Backup coverage (L177); DR step 4 (L180).
- **Finding:** Two gaps:
  - **Tenant keys.** "Complete" does not require a tenant-key backup that covers every key generation referenced by data at or before the cut. Tenant-key backups are a separate class with no stated cadence, so the monitor can report a complete point whose data cannot be decrypted for tenants created or keys rotated since the last key backup.
  - **Keycloak event export.** Its "freshness" has no bound. Revocations made after the last shipment are lost in step 4, and a revoked principal regains access.
- **Failure scenario:** Tenant T is created at 10:05. The key backup runs daily at 00:30. The 10:30 data cut is reported "complete". The node is lost at 10:50, and T's data is restored as ciphertext without its key.
- **Suggested fix:** "A recovery point is complete only when a tenant-key backup covers every key generation referenced at or before its cut; the monitor verifies this without holding key material. The Keycloak event export ships within a declared bound, and its lag is monitored like recovery-point age."
- **Suggested disposition:** autofix.

### OPS-13 — The drill cannot prove the RTO as written
- **Severity:** medium
- **Location:** DR evidence (L181); Detection and response (L179); DR step 5 (L180); Memories erasure continuity (L182).
- **Finding:**
  - "Measures real detection" contradicts "never mutate live production": an isolated drill cannot observe a real outage.
  - Substitutions are listed but untimed: fence against copies, Keycloak generated from the contract, no DNS or certificate cutover, a copy of the mirror. A faster substitute proves nothing about how long the real step takes.
  - Rebuild-only replay is on the critical path. Memories syntactic, vector and graph projections are keyed by embedding epoch and may be re-embedded. The spine does not say whether reopening waits for the full rebuild or for a module-declared degraded state.
  - Monthly drills occupy the prepared capacity, and nothing says a real disaster preempts a drill.
- **Failure scenario:** The drill reports a 2 h 40 min RTO. The real event adds DNS propagation, ACME issuance, a Keycloak database restore of both realms, and a 5-hour Memories re-projection. The actual time to verified service is about 9 hours.
- **Suggested fix:**
  - "Detection time is the probe interval plus the measured probe-to-issue latency against a drill target."
  - "Each substitution records a measured or bounded duration for the real step, which is added to the RTO; an unbounded substitution fails the RTO proof."
  - "Each module declares whether reopening waits for its rebuild-only replay or admits a declared degraded state, and the drill times it at representative size."
  - "A real DR entry aborts any running drill and reclaims the prepared capacity."
- **Suggested disposition:** autofix.

### OPS-14 — The production-promoted record is written before verification and never invalidated
- **Severity:** medium
- **Location:** Rollout (L173); AD-13 (L129); Migration and coexistence, EventStore AD-22 (L160). EventStore `architecture.md` AD-22: consumer removal requires "AD-26 `production-promoted` for the canonical profile digest". AD-11 defines expiry, revocation and invalidation.
- **Finding:** The record is written before readiness and verification. No rule invalidates it when the attempt ends non-working, so a failed or rolled-back subject keeps a state that EventStore consumers treat as a precondition for removing infrastructure.
- **Failure scenario:** Composed host C9 fails verification and is rolled back. A module's legacy-retirement packet then cites C9's still-valid production-promoted record.
- **Suggested fix:** "A non-working terminal outcome invalidates the attempt's production-promoted record under EventStore's invalidation rule. Only a working outcome leaves it valid."
- **Suggested disposition:** discuss. Add it to the EventStore confirmation row (L314).

### OPS-15 — The state after DR is undefined
- **Severity:** medium
- **Location:** DR step 6 (L180); Production entry gates (L176); Staging gate (L168); Recovery capacity and coverage (L319).
- **Finding:** On a single node, a DR loses staging as well, and the prepared capacity becomes production. After reopening:
  - There is no staging, and every promotion needs the staging gate, including an emergency fix in Administrator-approved mode.
  - No prepared capacity remains for the next disaster, which removes a G2 precondition.
  - The monitor may be hosted at the capacity that was just consumed.

  The spine does not define this degraded posture or how production returns to the G2 conditions.
- **Failure scenario:** Two days after DR, a security fix is ready, but nothing can promote it because staging no longer exists. A second failure then has no prepared target.
- **Suggested fix:** "After DR, production runs in a recorded degraded posture. The promotion stop stays set until staging is re-established. New prepared capacity is identified, and a drill repeats as a material change. Administrator records the return to G2 conditions."
- **Suggested disposition:** discuss.

### OPS-16 — The telemetry sink's failure domain is unstated
- **Severity:** low
- **Location:** Diagnostics and notification (L159); Deferred Recovery capacity and coverage, "telemetry sink and minimum retention" (L319). PRD FR-8: diagnostics must stay retrievable independently of the failed environment.
- **Finding:** The sink and its retention are deferred to G2 with an owner, which is acceptable. But nothing requires production diagnostics to outlive the node, and AD-2 covers "evidence" only. An in-cluster sink loses the logs and traces that would explain a node failure or a failed attempt.
- **Suggested fix:** "Attempt-window diagnostics and the production telemetry needed for incident analysis are shipped outside the primary failure domain, with the declared minimum retention."
- **Suggested disposition:** defer, by adding it to the existing Deferred row.

## Checked and clean

- **Attempt serialization.** One lock covers every workload-affecting change. It carries an epoch, and DR entry takes a new one. The durable promotion stop is cleared only by an Administrator record naming a verified working release. Manual and DR changes become the baseline only after verification.
- **Recovery mechanics.** Recovery re-renders the retained package with environment-current values, never automatic Helm rollback. There is exactly one recovery, and releases are never cycled. A failed first deployment keeps ingress closed. Rolling back a first module enrollment removes workloads but never data objects.
- **Staging gate.** It fails closed on missing, skipped, stale or wrong-release evidence. A failed rehearsal is breaking evidence, not a staging failure. Approved mode keeps the staging gate, lock, provenance and verification.
- **Backups.** Classes are separated. OpenBao is per environment, and its backup excludes tenant keys. Copies are immutable and off-site with per-instance prefixes. Rotated key generations are retained as long as the data backups. Pruning skips points referenced by an open recovery.
- **Recovery point definition and freshness monitor.** They match PRD FR-9, apart from the OPS-12 additions.
- **Memories continuity.** Memories tombstone continuity is a G2 prerequisite with owners. Unknown lineage fails closed. The non-Memories erasure exception in the lost window is stated and reported.
- **Monitoring and retention.** An off-site monitor runs with an hourly dead-man check, and GitHub as the single notification path is recorded as an accepted risk. Artifact and record retention covers the backup window (AD-2).
- **Gates and precedence.** Gates G1/G2/G3 and the Source Precedence override of the Folders and Projects availability clauses are consistent with the PRD.
