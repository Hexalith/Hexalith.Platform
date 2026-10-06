---
title: '4.27: Retire KubeSphere without changing workload data'
type: 'feature'
epic: 4
story: 27
created: '2026-10-05'
status: 'in-progress'
baseline_commit: '54920908f15a99b48e69861baf306365353fc6df'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/4-27-retire-kubesphere-without-changing-workload-data.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 4.26 qualified isolated retirement, but no production executor or approved live attempt exists. Console-closure and recovery gates remain open.

**Approach:** Build an explicit-context production executor using qualified guards, bind a fresh plan and exact-byte rehearsals to an Administrator-approved attempt, then retire and verify KubeSphere after preflight. Retain failed attempts; incomplete outcomes keep the story open.

## Boundaries & Constraints

**Always:** Preserve application/data namespaces, PVC/PV UIDs and bindings, shared CNI/storage/ingress/identity, public application/OIDC contracts and independent native access. Close the public console under 4.2 first. Validate fresh signed 4.0 workload proofs, off-node readback, a fresh production external-etcd point and fenced isolated restore under 4.1's contract, plus matching node/configuration recovery. Obtain a separate approval for the exact executor/procedure, plan, allowlist, outage, owner, window and stop/recovery conditions. The signed plan may explicitly bind existing native `coordination.k8s.io/v1` Lease identities and their complete desired content except `spec.renewTime`; allow only valid monotonic renewal on those exact identities while every other desired field remains unchanged. Preserve the qualified exact manager Lease checkpoint after controller removal. Keep raw inventories, logs, credentials and signed originals in restricted encrypted custody; publish only sanitized outcomes.

**Never:** Treat design approval, historic fixtures, hashes or an old backup gate as live authorization. Do not restore into the running cluster, delete a Namespace/PVC/PV or shared component, use wildcard or blanket finalizer edits, invoke licensed KubeSphere writes, install Rancher, or start the Kubernetes hop. Preserve `jpiquot` authority for 4.28.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Approved attempt | Fresh matching recovery, 4.2 closure, signed exact plan and current native census | Execute 19 reviewed phases with UID/resourceVersion/full-content guards and phase preservation checks | Stop and retain a sealed failure receipt on any unexpected result |
| Stale or incomplete gate | Expired proof, changed procedure/driver/module, drifted UID or desired content outside explicitly bound Lease renewal, missing owner or approval | Zero mutation requests | Record the rejected gate without promoting a proposal to an allowlist |
| Bound Lease renewal | Exact signed-plan native Lease identity, valid nonfuture and non-regressing `spec.renewTime`, all other desired content unchanged | Planning/execution handoff and preservation checks accept only that observed renewal; renewals grant no mutation authority | Missing binding, UID/holder/owner/spec/metadata drift, malformed/future/regressing time, and expired approval stop with zero further mutation requests |
| Partial retirement | An observed deletion, finalizer conflict, controller recreation or failed smoke | Preserve source/recovery evidence, stop remaining phases and require incident decision | Never auto-restore an old point or mark retirement accepted |
| Completion | Manager absent and protected workloads, bindings, access and health verified | Signed before/after outcome and fresh post-retirement 4.1 recovery inputs | Any failed or unobserved check leaves story open |

</frozen-after-approval>

## Code Map

- `eng/cluster-management/rehearse.py` — pure `dependency_plan`, `validate_allowlist`, content/transition/phase guards and `Fixture.retire_phase`; reuse guards, separate fixture transport from production transport. Keep source credentials out of kind.
- `eng/cluster-management/rehearse_catalog.py`, `rehearse_namespaces.py` — exact catalog, seven-namespace and fresh-node fixtures; rerun after executor/shared-code changes.
- `eng/cluster-management/qualify.py` — explicit native census and encrypted raw exports; refresh before planning. `evidence.py` owns immutable two-recipient attempts but currently publishes only to 4.26.
- `eng/kubernetes-upgrade/README.md`, `prepare.py` — 4.1 recovery record contracts; `prepare.py` inspects metadata only, so it cannot validate signatures or approve recovery.
- `eng/cluster-management/RETIRE-KUBESPHERE.md`, `README.md` — retirement procedure, retained objects and operator handoff; preserve hashed `eng/kubernetes-upgrade/MAINTENANCE.md`.
- `_bmad-output/implementation-artifacts/4-2-close-public-admin-exposure-and-anonymous-registry-reads.md` — 4.2 is in progress; closure requires independent external denial evidence.

## Tasks & Acceptance

**Execution:**
- [x] `eng/cluster-management/rehearse.py` — extract transport-independent phase execution and preserve all exact deletion, transition, namespace-finalizer and full-content guards for fixture and production use.
- [x] `eng/cluster-management/retire.py` — add explicit native context/tool/credential inputs, fresh census and plan construction, independent preflight evidence validation, exact signed approval binding, default non-mutating mode and an explicitly armed production path; record every request/phase and stop at the first refusal.
- [x] `eng/cluster-management/evidence.py` — support immutable 4.27 private attempts and sanitized publication without changing 4.26 receipts; encrypt/read back raw before/after inventories, commands and diagnostics.
- [x] `eng/cluster-management/test_retire.py` and existing fixture tests — exercise stale gate/approval, drift, conflict, unexpected deletion, retained authority, protected namespace/storage and failed health paths; verify zero requests before a complete gate and fail-closed mid-phase behavior.
- [x] `eng/cluster-management/retire.py`, `test_retire.py` — resolve the approved Lease handoff limit through exact signed-plan identity/content bindings and monotonic-renewal guards; preserve strict comparison for unbound objects and the shared post-controller manager Lease transition, verify renewed execution baselines during assessment, and test both accepted renewal and every refusal boundary.
- [ ] `eng/cluster-management/RETIRE-KUBESPHERE.md`, `README.md` — document exact command inputs, plan/approval handoff, per-phase stop/recovery and post-retirement checks. Rerun catalog, seven-namespace and fresh-node fixtures with final executor/shared-module bytes; bind receipts to the fresh production plan.
- [ ] `_bmad-output/implementation-artifacts/4-27-retire-kubesphere-without-changing-workload-data.md`, `sprint-status.yaml` — record current gate and attempt evidence; mark done only after signed production preservation and retirement acceptance. Hand fresh post-retirement recovery inputs to 4.1.

**Acceptance Criteria:**
- Given an approved plan bound to current identities and exact rehearsed bytes, when the production executor runs, then every requested deletion is within the approved phase allowlist and each phase proves protected identities/bindings unchanged before the next request.
- Given completed phases, when retirement is assessed, then the old console and manager runtime/admission references are absent, public console access is denied externally, native administration and authenticated Keycloak/OpenBao/Memories/Forgejo outcomes pass, and the signed result names retained archive objects and incidents.
- Given any missing prerequisite or failed/unobserved outcome, when the handoff is evaluated, then 4.27 remains incomplete and 4.1's upgrade gate stays closed.

## Implementation Notes

- `PhaseExecutor` shares the existing reviewed guards between kind and the explicit native production transport. Production dispatch independently checks action kind, approved phase root, UID/resourceVersion and exact named-finalizer body; it stops at the first refusal. Protected and retained state, source/tool/credential/trust/code/evidence identities and gate expiry are checked before subsequent requests.
- Immutable planning custody names a different future execution attempt. Original signatures and exact plan/receipt/procedure/allowlist/outage/owner/window bindings precede arming. A separate read-only assessment requires new signed postflight health/external-denial/recovery inputs and exact Administrator acceptance; successful deletion alone keeps the story open and the 4.1 gate closed.
- Continuation verification: all **251** cluster-management tests pass with no skips, including 73 retirement tests for signed Lease bindings, nanosecond/nonfuture/monotonic renewal, unchanged other content, exact checkpoint GETs, post-DELETE termination state, renewed assessment baseline and expiry after blocking configuration validation, plus the fresh-node CRI stop-failure boundary. Verbose audit: `/tmp/story-4-27-cri-timeout-full-suite.log`. Renewed namespace qualification `20261005t142929z-s427-namespaces` completed with seven exact UID/resourceVersion-bound PUTs, zero Namespace DELETEs, unchanged protected identities/storage and canary, verified encrypted readbacks and successful owned-resource cleanup. Catalog/fresh-node attempt [20261005t142929z-s427-catalog](evidence/epic-4/4-27/20261005t142929z-s427-catalog/qualification.json) passed the 118-object catalog check and completed 18 phases, then stopped on a command/custody error during a final state poll; its original cleanup also timed out and remains `failed-cleanup`. Exact owned-resource removals are retained in separate [conservative cleanup recovery](evidence/epic-4/4-27/20261005t155006z-s427-fixture-cleanup/cleanup-recovery.json); the later [read-only shared-predicate verification](evidence/epic-4/4-27/20261005t155136z-s427-cleanup-verification/cleanup-recovery.json) proved its node/network/recorded volume/image aliases absent, with zero removals and the original failure manifest unchanged. Renewed [20261005t155319z-s427-catalog](evidence/epic-4/4-27/20261005t155319z-s427-catalog/qualification.json) passed all 19 retirement phases with 471 exact removals and 46 retained CRDs, then stopped on the third fresh-node sandbox stop with CRI `DeadlineExceeded`; source, fresh-node and aggregate cleanup passed. The separate [retained-image timeout probe](evidence/epic-4/4-27/20261005t165533z-s427-cri-timeout-probe/observation.json) confirmed a two-second default and no configuration override, with only offline help/configuration reads and exact owned cleanup. The fresh-sandbox stop now has an explicit 30-second CRI bound while retaining the 120-second subprocess bound and first-error refusal; focused coverage proves no later stop or node-input/snapshot replacement follows a failure. Final-byte [namespace attempt 20261005t170204z-s427-namespaces](evidence/epic-4/4-27/20261005t170204z-s427-namespaces/qualification.json) passed with seven UID/resourceVersion-bound PUTs, zero Namespace DELETEs, protected identity/storage/canary preservation, 85 verified encrypted readbacks and successful cleanup. Final-byte catalog/fresh-node attempt `20261005t170204z-s427-catalog` is running sequentially; its retirement, restore and final cleanup remain pending. Earlier full-fixture current-match flags are false because the shared module changed; all private attempts stay immutable. Earlier completed fixtures retain their original, superseded code/procedure identities and measured outcomes in compact 4.27 projections; current-match flags are false and authoritative private attempts remain untouched.
- The renewed unmocked synthetic native census `20261005t144246z-s427-native-census` observed 295 resources with zero mutations, 89 verified encrypted readbacks and temporary synthetic-input cleanup. The actual renewed Lease pair below passed the real binding/content validator. Final `validate_rehearsals`, historical source scope and 251-test matrix audit remain pending until the catalog manifest exists; earlier audits retain only their recorded prior-byte meaning. No fresh production plan, approval, mutation or acceptance is claimed.
- Remaining operational work: fresh native production plan bound to final-byte receipts, accepted 4.2 closure, fresh signed production 4.0/4.1 recovery/readback and matching node/configuration, separate exact execution approval, actual production retirement/preservation and signed postflight acceptance/4.1 recovery handoff. No production attempt was armed or run; story and sprint remain `in-progress`.

- **Measured operability gate:** the historical census includes 13 Leases. The retained [synthetic native observation](evidence/epic-4/4-27/20261005t105545z-s427-lease-handoff/qualification.json) requested a 12-second pause and measured a 15.205-second response interval: the Node-Lease UID and every other desired-content field stayed unchanged while `spec.renewTime` and its full desired-content digest changed. Both actual command/results remain in four encrypted exports with verified two-recipient readback. This immutable observation records the pre-continuation strict-handoff limit. The approved continuation now permits only explicit signed-plan native Lease renewal with every other desired field fixed, validated monotonic/nonfuture time and the unchanged qualified manager checkpoint. The renewed [actual synthetic Lease pair](evidence/epic-4/4-27/20261005t145105z-s427-lease-handoff/qualification.json) requested 12 seconds and measured a 15.151-second response interval. The real new binding/content validator accepted monotonic renewal on the same UID with every other desired field unchanged, no mutation authority, zero requests and no production plan; five encrypted/read-back exports retain both commands/results and the binding model. Its preceding generic-list-wrapper harness refusal remains a separate immutable failed outcome. This closes the measured local handoff limit under the approved binding contract; all production prerequisites and exact-attempt approval remain required.

- Local prerequisite metadata inspection found the retained 4.0 gate `20261001t061620z` expired at `2026-10-02T06:16:52Z`, and 4.1 records remain historical `20261001t075120z-backup-verified`. No current console-closure record, retirement policy or exact execution approval was found in the inspected retained roots. This read-only local discovery checked metadata/signature-file existence, performed no cryptographic signature validation and made zero remote operations; these historical records supply no current execution gate.

## Spec Change Log

- 2026-10-05: User replied "I approvce" after the documented Lease-renewal operability gate and pending 4.27 work. Continue implementation with the narrow signed-plan Lease-renewal exception above. Fresh production prerequisites and exact-attempt approval remain required. Existing qualification receipts retain their original byte identities; renew final-byte fixtures after this change.

## Review Triage Log

## Design Notes

Approved Lease continuation: build explicit private plan bindings for existing native Lease UIDs, API identity/name/namespace, the original valid renewal time and a complete desired-content digest excluding only `spec.renewTime` (with the same existing status/server-bookkeeping normalization). Execution validates nonfuture, monotonic time and every other desired field against those bindings. Recheck retained Lease content during preservation rather than skipping all Lease desired fields. Seed the execution's private reviewed baseline only from the freshly validated objects so the unchanged shared manager Lease checkpoint still checks its exact post-controller transition. Assessment must verify the encrypted execution-before baseline against the same signed-plan bindings. No wildcard, blanket content exclusion, new Lease mutation, or expiry bypass is permitted. Old plans without renewal bindings retain their strict behavior. Document the explicit bindings in the operator handoff and rerun full catalog/fresh-node and seven-namespace qualification sequentially with settled final bytes; retain superseded attempts truthfully.

Use the established absolute retained-tool input contract consistently: resolve or refuse relative tool paths before hashing and execution so the recorded executable and the invoked executable are identical. Add a focused refusal/binding test for a relative basename that could otherwise resolve through PATH. Settle all changes and meaningful checks before renewing the long fixtures.

Renew the read-only synthetic native Lease observation with the settled executor. Validate its two actual samples through the real signed-plan Lease binding/content validator and retain a sanitized outcome proving monotonic renewal accepted, all other desired fields unchanged, and zero mutation requests or new mutation authority. Keep source credentials out of the fixture and preserve earlier immutable observations.

The renewed isolated restore stopped on the third fresh-sandbox stop with CRI `DeadlineExceeded`; the retained-image probe measured a two-second `crictl` default and no configuration timeout override. Set an explicit `--timeout 30s` only for stopping each exact listed sandbox in the fresh rollback node. Preserve the existing 120-second subprocess bound, validated node/sandbox identities, first nonzero-exit refusal and exact owned-resource cleanup. Verify that a stop failure permits no later sandbox stop or node-input/snapshot replacement. Settle the module and tests, then renew seven-namespace and full catalog/fresh-node qualification sequentially because the shared module bytes changed. Preserve the failed retirement/restore attempt and its successful cleanup; change no production phase guard or frozen intent.

The production executor must call the same reviewed phase guards as the synthetic fixtures through a separately injected native client. Its only mutation mode requires a verified, attempt-specific approval after the final plan and exact-byte fixture receipts exist. A stopped partial deletion is an incident requiring a new decision, never an automatic rollback.

## Verification

**Commands:**
- `python3 -m unittest discover -s eng/cluster-management -p 'test_*.py' -v` — 251 tests pass; full output retained at `/tmp/story-4-27-cri-timeout-full-suite.log`.
- `git diff --check HEAD` — no whitespace errors.

- Current-byte private fixture manifest validation and renewed binding audit remain pending until the sequential final fixtures finish. The earlier [local audit](evidence/epic-4/4-27/20261005t104023z-s427-verification/verification.json) retains its prior byte identities and measured outcomes; it supplies no current or fresh production gate.

**Matrix audit:** all covering tests ran and passed (`... ok`) in the 251-test verbose output; none was skipped. This verifies software behavior; live operational acceptance remains unobserved.

| Matrix row | Passing covering tests |
| --- | --- |
| Approved attempt | `test_shared_phase_execution_and_real_plan_to_execute_handoff` |
| Stale or incomplete prerequisite | `test_stale_approval_is_zero_mutations`, `test_missing_recovery_or_console_gate_is_zero_mutations`, `test_caller_verified_flag_cannot_replace_actual_signature` |
| Bound Lease renewal | `test_bound_node_lease_renewal_accepts_signed_handoff_without_lease_mutation`, `test_manager_checkpoint_object_get_refuses_nanosecond_regression_below_observed_floor`, `test_bound_lease_holder_drift_after_accepted_delete_stops_further_requests`, `test_assessment_accepts_validated_renewed_execution_baseline_and_later_renewal` |
| Partial retirement | `test_conflict_stops_first_request_without_retry`, `test_unexpected_protected_deletion_stops_before_next_request`, `test_recreated_controller_stops_before_next_request` |
| Completion | `test_signed_postflight_and_exact_administrator_acceptance`, `test_failed_post_retirement_health_leaves_acceptance_and_hop_closed` |

**Manual checks (if no CLI):**
- Inspect exact signed approvals, independent external denial, fresh recovery/readback and authenticated production outcomes before recording completion.
