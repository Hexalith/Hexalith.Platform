# Epic 9 Context: Promote releases to production automatically (G3)

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Enable automatic production promotion (gate G3) for the reference composition, and only after it is proven safe. Until G3 every production release is Administrator-approved. This epic defines what "security-current" shared infrastructure means and enforces it with a currency check. It verifies the EventStore catalog and record behaviours that automatic rollback depends on. It then runs the full SM-5 rehearsal suite: every deployment-failure trigger, the one-shot recovery, every stop and approval rule, the release-mode and retained-data rules, and shared-infrastructure success and forward revert, which become possible once Epic 8 supplies complete recovery points. Finally, any later policy change suspends automation until the affected rehearsals repeat. When the epic is done, releases with valid compatibility evidence against the current baseline reach production on their own, and no validation is ever bypassed.

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

- **Automatic trigger:** G3 recorded, plus complete passing compatibility evidence against the *current* production working baseline, plus a passing currency check. Evidence that is missing, failing, stale, against the wrong baseline or breaking refuses the automatic path, and so does a failed currency check. Approval remains the fallback. It never bypasses staging E2E evidence, provenance, isolation, the lock or verification. Both modes share the same staging gate, lock, provenance, verification and records.
- **Compatibility evidence:** module change classifications (the maximum over the chain from the baseline) plus a staging candidate-to-baseline rehearsal. The rehearsal proves the baseline reads candidate-written state and events, including a newly introduced idempotent command. Rollback preserves business data, event history, credential rotations, revocations and security authority.
- **Currency check:** runs at every production attempt and every monthly drill. It compares each deployed shared component, including Rancher, required agents, the management-cluster distribution and the backup operator, with the supported, security-current profile inventory. It also checks the qualified management/workload version pairing, records exact deployed identities and the result, and names the out-of-date components when it blocks. Its effect on approved attempts is a 9.1 decision.
- **Rehearsed thresholds:** rollout must be ready within 10 min of start. Verification lasts 5 min. It fails on 60 continuous seconds of unavailability, on two consecutive smoke failures (the retry comes 30 s after the first failure) or on a result missing at the deadline. A single probe failure or restart does not fail it. Sampling runs at least every 10 s. Recovery is exactly one attempt: 10 min to readiness plus the same verification, never cycling to older releases. Interruptions never reset deadlines or add recoveries.
- **Promotion stop:** durable and per-cause, with a monotonic revision. The monitor may set it but never clear it. Only a signed Administrator record clears it, by compare-and-set at the observed revision and only after the lost-window and admission reviews. A later stop always prevails. An accepted cause is not re-recorded until it resolves and recurs, and any other persisting condition is re-recorded as a new cause.
- **Deputy limits:** the deputy may run documented recovery, verify, reopen to the recorded pre-incident ingress state (user ingress stays closed before G2) and declare a stop. The deputy may not approve releases, clear the stop or administer production admission. Rehearsals must cover every deputy prohibition and both recurrence rules. Story 9.5 currently lists only "the deputy cannot approve", a known open gap.
- **Degraded and incompatible attempts:** an approval clears only the stop it observed. Approval is refused when production is neither empty nor degraded. A failed attempt removes the candidate, keeps data and sets a new stop. A failed approved incompatible release closes ingress, then runs the named data-restore recovery. That recovery must preserve an admission revocation and a Memories erasure acknowledged during the attempt.
- **Policy-change suspension:** a change to verification or recovery thresholds or budgets, recovery procedures or rollback-set rules suspends automatic promotion. Platform and Administrator record which rehearsals the change affects, including whether module readiness or smoke declaration changes affect any. Promotion resumes only after all affected rehearsals pass again, with a record linking the change to the evidence.
- **Success counters, all zero:** automatic promotions without valid compatibility evidence and G3; promotions on failed, skipped, missing, stale or mismatched E2E evidence; degraded approvals outside empty or degraded production; stop clears without an Administrator record; recoveries reported successful without readiness, smoke and data checks. Recorded recovery time always includes operator and provisioning time.

## Technical Decisions

- **One production workflow, two modes:** approved mode replaces only the G3 and compatibility triggers with an Administrator record. Nothing mutates until every production precondition holds, including a clear stop, the mode trigger, fresh staging evidence, equal profile digests, healthy production and, for retained data, compatibility evidence or a named restore.
- **Automatic recovery:** a Helm upgrade that renders the prepared rollback set. That set is the baseline package with environment-current values plus the rollback generation, and it is recorded and ready-validated before rollout. The rollback generation commits at readiness. `helm rollback`, `--rollback-on-failure` and controller-driven rollback are forbidden. Forward-only inputs (catalog, keys, secrets, realm, routing, Dapr components) stay expand-only until the candidate is working, and contraction happens in a later attempt.
- **Production-promoted record:** written before the Helm upgrade and bound to the profile digest. It is invalidated on a non-working outcome and renewed on any environment-layer or shared change.
- **Shared infrastructure:** versions come from the shared facet of the profile inventory. Every change runs under both environment locks after a complete recovery point, and is first rehearsed on a production-profile copy on prepared capacity; the only exception is an urgent security patch applied in place with an Administrator record. A failed verification sets the stop, degrades production and is recovered by Administrator-owned forward revert.
- **Rehearsal harness:** SM-5 reuses the Epic 6 harness and adds none of its own. Faults are injected only inside Administrator-approved rehearsal attempts, at named injection points and with no standing credential. No injection changes a release-invariant value. Attempt records mark rehearsals, and GitHub issues carry a rehearsal label, so the deputy never receives a real alert for a planned fault.

## Cross-Story Dependencies

- 9.1 precedes 9.2. The 9.2 check feeds the currency-block rehearsal in 9.6 and the automatic path in 9.8.
- 9.3 is blocked on EventStore's G3 confirmations, an external prerequisite. G3 cannot be recorded while any confirmation is missing. Each one needs a recorded staging test.
- 9.4–9.6 build on Epic 6:
  - the fault-injection decision (6.1) and the harness (6.21);
  - the stop store and durable stop (6.9, 6.10), notifications (6.13) and preconditions (6.14);
  - rollout and verification (6.16), automatic recovery (6.17) and approved attempts (6.18);
  - the guarded shared-infrastructure procedure (6.22).
  The pre-G2 rehearsals (6.24) count as early SM-5 evidence.
- 9.6 also needs Epic 5's compatibility evidence (5.8) and named recovery (5.9). From Epic 8 it needs complete recovery points (8.7), prepared capacity (8.1), qualified in-place and named restore (8.16), Memories erasure continuity (8.13) and the deputy's recovery capability (8.14).
- Epic 8's reduced-recovery policy (8.2) decides whether automatic promotion may resume before the return to G2 conditions.
- 9.8 needs 9.1, 9.3 and 9.4–9.6 recorded, and 9.7 then governs suspension. Gates run strictly in the order G1, G2, G3.
- Epic 10's MVP acceptance closes after Epic 9. Epic 10 repeats SM-5 for all seven modules.
