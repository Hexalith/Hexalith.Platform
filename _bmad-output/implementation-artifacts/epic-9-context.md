# Epic 9 Context: Promote releases to production automatically (G3)

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Enable automatic production promotion for the reference composition only after gate G3 proves its safety. Define and enforce shared-infrastructure currency, verify the EventStore behaviours required for recovery, and rehearse deployment failure, recovery, interruption, approval, retained-data and infrastructure paths. Administrator-approved releases remain available before G3 under the same staging, provenance, isolation and verification controls. Later policy changes suspend automation until affected rehearsals pass again.

## Stories

- Story 9.1: Decide the shared-infrastructure currency policy
- Story 9.2: Run the shared-infrastructure currency check
- Story 9.3: Verify EventStore's G3 confirmations
- Story 9.4: Rehearse deployment verification and one-shot recovery (SM-5, part 1)
- Story 9.5: Rehearse the promotion-stop and approval rules (SM-5, part 2)
- Story 9.6: Rehearse release-mode, retained-data and infrastructure rules (SM-5, part 3)
- Story 9.7: Suspend automatic promotion when policy changes
- Story 9.8: Reach G3 and enable automatic promotion

## Requirements & Constraints

- Automatic promotion requires recorded G3, complete passing compatibility evidence against the current production working baseline, and a passing infrastructure currency check. Missing, failed, stale, wrong-baseline or breaking evidence refuses automation. Approval never bypasses staging E2E, provenance, isolation, ownership or verification.
- Both release modes require passing exact-release staging E2E for every enrolled module's declared critical flows. Skipped, incomplete or absent results fail the gate. Compatibility combines module change classifications with a staging candidate-to-baseline rehearsal proving the baseline reads candidate-written state and events, including a new idempotent command where applicable.
- Currency compares deployed shared components with supported, security-current profile pins at every production attempt and monthly drill. Include Rancher, required agents, management-cluster distribution and backup operator; check the qualified management/workload pairing and record exact deployed identities, result and failing components. Story 9.1 decides inventory boundaries, allowed lag, cadence and the effect on approved attempts.
- Rollout readiness is due within ten minutes. Verification lasts five minutes, samples at least every ten seconds and fails for sixty continuous seconds of unavailability, the same smoke check failing twice consecutively thirty seconds apart, or no completed passing result at the deadline. One probe failure or restart alone is insufficient. Results bind the intended release.
- Compatible deployment recovery gets one attempt with ten minutes to readiness and the same verification. Preserve business data, event history, credential rotations, revocations and current security authority. Never cycle through older releases. Interruptions neither restart deadlines nor replenish recovery allowance; uncertain ownership or an observation gap stops changes or invalidates verification.
- Promotion stops are durable and per-cause with monotonic revisions. Monitoring sets but never clears them. Only Administrator clears by signed compare-and-set at the observed revision after lost-window and admission reviews. Later stops prevail. Accepted causes recur only after resolution and recurrence; any other continuing condition is re-recorded after clearance.
- Empty/degraded approval is refused for healthy nonempty production. Failed approved attempts remove candidate workloads while retaining data and set a new stop. Failed approved incompatible releases close user ingress and use their named recovery, preserving admission revocation and acknowledged Memories erasure during the attempt. The deputy may recover and declare stops, but cannot approve releases, clear stops or administer admission.
- Policy changes affecting verification/recovery thresholds, budgets, procedures or rollback-set rules suspend automation. Platform and Administrator identify affected rehearsals, including whether readiness/smoke declaration changes affect them; resumption links the change to passing repeat evidence.
- Acceptance requires zero automatic promotions without G3/valid compatibility, zero staging-gate bypasses, zero invalid degraded approvals or unauthorized stop clears, and zero reported successful recoveries without readiness, smoke and data verification.

## Technical Decisions

- One production workflow supports automatic and Administrator-approved modes. Approval substitutes only for the G3/compatibility triggers; all other preconditions still hold before mutation. Attempts use environment locks and monotonic epochs; shared changes hold both locks.
- Recovery uses Helm upgrade to render the prepared rollback set: previous application, environment-current values and the rollback generation, validated before rollout. Commit the rollback generation at readiness. Helm rollback and controller-driven rollback are forbidden. Forward-only catalog, routing, key, secret, realm and Dapr inputs remain expand-only until the candidate works; contraction is a later attempt.
- Production-promoted records bind the active profile digest, precede upgrade, are invalidated by non-working outcomes, and are renewed after environment-layer/shared changes. Qualification must cover the current effective profile sets.
- Shared pins belong to the profile inventory, separate from application artifacts and deploy-tool identities. Shared changes require a complete recovery point and rehearsal on prepared production-profile capacity, except an urgent in-place security patch with an Administrator record. Failed shared verification degrades production and sets its stop; Administrator owns forward revert. A failed currency check blocks automation.
- Rancher uses a dedicated private management cluster and retains independently qualified native workload-cluster access. Its backup does not replace workload/etcd recovery. Management/workload pairing, exact images/chart and current authority are qualified separately; manager unavailability does not remove native recovery capability.
- SM-5 extends the Epic 6 harness. Fault injection occurs only in Administrator-approved rehearsal attempts at named points without standing credentials or release-invariant changes. Records and GitHub issues identify rehearsals to avoid real incident alerts.

## Cross-Story Dependencies

- 9.1 precedes 9.2; 4.9 owns the ratified hosted profile inventory, including shared pins and identities. These are required inputs, not established evidence of implemented policy or inventory. Currency enforcement feeds 9.6 and the automatic gate in 9.8.
- 9.3 requires EventStore confirmations of retention, catalog activation order, expected-generation commit, recovery-point forward generation and production-promoted record invalidation/renewal, each proven in staging. Missing confirmation blocks G3.
- 9.4–9.6 reuse Epic 6 fault decisions/harness, durable stop, notification, precondition, verification, recovery, approval and guarded shared-infrastructure procedures. Pre-G2 rehearsal evidence can contribute early SM-5 proof.
- 9.6 also depends on Epic 5 compatibility/named recovery and Epic 8 complete recovery points, prepared capacity, qualified restore, Memories continuity and deputy access. Reduced-recovery policy decides whether automation may resume before return to G2 conditions.
- 9.8 records G3 only after the policy, EventStore confirmations and all SM-5 paths pass; 9.7 governs later suspension. Gates remain ordered G1, G2, G3. Epic 10 repeats acceptance for the complete seven-module composition.
