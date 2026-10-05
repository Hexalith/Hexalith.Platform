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

**Always:** Preserve application/data namespaces, PVC/PV UIDs and bindings, shared CNI/storage/ingress/identity, public application/OIDC contracts and independent native access. Close the public console under 4.2 first. Validate fresh signed 4.0 workload proofs, off-node readback, a fresh production external-etcd point and fenced isolated restore under 4.1's contract, plus matching node/configuration recovery. Obtain a separate approval for the exact executor/procedure, plan, allowlist, outage, owner, window and stop/recovery conditions. Keep raw inventories, logs, credentials and signed originals in restricted encrypted custody; publish only sanitized outcomes.

**Never:** Treat design approval, historic fixtures, hashes or an old backup gate as live authorization. Do not restore into the running cluster, delete a Namespace/PVC/PV or shared component, use wildcard or blanket finalizer edits, invoke licensed KubeSphere writes, install Rancher, or start the Kubernetes hop. Preserve `jpiquot` authority for 4.28.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Approved attempt | Fresh matching recovery, 4.2 closure, signed exact plan and current native census | Execute 19 reviewed phases with UID/resourceVersion/full-content guards and phase preservation checks | Stop and retain a sealed failure receipt on any unexpected result |
| Stale or incomplete gate | Expired proof, changed procedure/driver/module, drifted UID/content, missing owner or approval | Zero mutation requests | Record the rejected gate without promoting a proposal to an allowlist |
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
- [ ] `eng/cluster-management/RETIRE-KUBESPHERE.md`, `README.md` — document exact command inputs, plan/approval handoff, per-phase stop/recovery and post-retirement checks. Rerun catalog, seven-namespace and fresh-node fixtures with final executor/shared-module bytes; bind receipts to the fresh production plan.
- [ ] `_bmad-output/implementation-artifacts/4-27-retire-kubesphere-without-changing-workload-data.md`, `sprint-status.yaml` — record current gate and attempt evidence; mark done only after signed production preservation and retirement acceptance. Hand fresh post-retirement recovery inputs to 4.1.

**Acceptance Criteria:**
- Given an approved plan bound to current identities and exact rehearsed bytes, when the production executor runs, then every requested deletion is within the approved phase allowlist and each phase proves protected identities/bindings unchanged before the next request.
- Given completed phases, when retirement is assessed, then the old console and manager runtime/admission references are absent, public console access is denied externally, native administration and authenticated Keycloak/OpenBao/Memories/Forgejo outcomes pass, and the signed result names retained archive objects and incidents.
- Given any missing prerequisite or failed/unobserved outcome, when the handoff is evaluated, then 4.27 remains incomplete and 4.1's upgrade gate stays closed.

## Implementation Notes

- `PhaseExecutor` shares the existing reviewed guards between kind and the explicit native production transport. Production dispatch independently checks action kind, approved phase root, UID/resourceVersion and exact named-finalizer body; it stops at the first refusal. Protected and retained state, source/tool/credential/trust/code/evidence identities and gate expiry are checked before subsequent requests.
- Immutable planning custody names a different future execution attempt. Original signatures and exact plan/receipt/procedure/allowlist/outage/owner/window bindings precede arming. A separate read-only assessment requires new signed postflight health/external-denial/recovery inputs and exact Administrator acceptance; successful deletion alone keeps the story open and the 4.1 gate closed.
- Verification: all **224** cluster-management tests pass, including 47 new executor tests with real SSH signatures and native transport refusal cases. Verbose local audit: `/tmp/story-4-27-full-suite.log`. The final-byte [catalog and fresh-node rehearsal](evidence/epic-4/4-27/20261005t104023z-s427-catalog/qualification.json) completed all nineteen phases and 471 exact removals with 46 CRDs retained, then restored all 798 baseline identities with zero baseline/CRD UID mismatches, canary readback, hash checking, five ready manager deployments, 845 verified encrypted readbacks and cleanup. The final-byte [seven-namespace rehearsal](evidence/epic-4/4-27/20261005t104023z-s427-namespaces/qualification.json) passed seven guarded PUTs, zero Namespace DELETEs, preservation, 85 verified encrypted readbacks and cleanup. Both remain `passed-limited` synthetic outcomes; production acceptance remains unobserved.
- The current-executor [unmocked native census smoke](evidence/epic-4/4-27/20261005t104956z-s427-native-census/qualification.json) passed inside the isolated node with synthetic credentials: 295 observed resources, zero mutations, 89 verified encrypted readbacks and temporary-input cleanup. The [local binding audit](evidence/epic-4/4-27/20261005t104023z-s427-verification/verification.json) ran the real `validate_rehearsals` against both final authoritative private manifests and current code/procedure digests. Pure `dependency_plan` / `actions_for` / `validate_production_scope` checks on the actual historical fixture census passed: 2,596 resources, nineteen phases, 482 deletion identities, seven namespace interventions, five retained authority objects and none in scope. These checks created no fresh production plan, approval, mutation or acceptance. Superseded completed fixtures and earlier smoke/setup refusals remain immutable under their original identities in compact 4.27 projections; authoritative attempts stay private, and existing 4.26 evidence stays untouched.
- Remaining operational work: fresh native production plan bound to final-byte receipts, accepted 4.2 closure, fresh signed production 4.0/4.1 recovery/readback and matching node/configuration, separate exact execution approval, actual production retirement/preservation and signed postflight acceptance/4.1 recovery handoff. No production attempt was armed or run; story and sprint remain `in-progress`.

- **Measured operability gate:** the historical census includes 13 Leases. The retained [synthetic native observation](evidence/epic-4/4-27/20261005t105545z-s427-lease-handoff/qualification.json) requested a 12-second pause and measured a 15.205-second response interval: the Node-Lease UID and every other desired-content field stayed unchanged while `spec.renewTime` and its full desired-content digest changed. Both actual command/results remain in four encrypted exports with verified two-recipient readback. The strict plan-to-execution comparison refuses normal renewal with zero mutations. Production activation remains gated pending an explicit intent decision; no live-Lease exception was added and the qualified post-controller manager Lease checkpoint remains unchanged.

## Spec Change Log

## Review Triage Log

## Design Notes

The production executor must call the same reviewed phase guards as the synthetic fixtures through a separately injected native client. Its only mutation mode requires a verified, attempt-specific approval after the final plan and exact-byte fixture receipts exist. A stopped partial deletion is an incident requiring a new decision, never an automatic rollback.

## Verification

**Commands:**
- `python3 -m unittest discover -s eng/cluster-management -p 'test_*.py' -v` — 224 tests pass; full output retained at `/tmp/story-4-27-full-suite.log`.
- `git diff --check HEAD` — no whitespace errors.

- Actual private final catalog/namespace manifests accepted by `validate_rehearsals` under frozen code and procedure digests; historical-source phase/action/scope validation also passes. Exact manifest/code/source/test-log digests and measured outcomes are retained in the [local binding audit](evidence/epic-4/4-27/20261005t104023z-s427-verification/verification.json). No fresh production gate is claimed.

**Matrix audit:** all covering tests ran and passed (`... ok`) in the 224-test verbose output; none was skipped. This verifies software behavior; live operational acceptance remains unobserved.

| Matrix row | Passing covering tests |
| --- | --- |
| Approved attempt | `test_shared_phase_execution_and_real_plan_to_execute_handoff` |
| Stale or incomplete prerequisite | `test_stale_approval_is_zero_mutations`, `test_missing_recovery_or_console_gate_is_zero_mutations`, `test_caller_verified_flag_cannot_replace_actual_signature` |
| Partial retirement | `test_conflict_stops_first_request_without_retry`, `test_unexpected_protected_deletion_stops_before_next_request`, `test_recreated_controller_stops_before_next_request` |
| Completion | `test_signed_postflight_and_exact_administrator_acceptance`, `test_failed_post_retirement_health_leaves_acceptance_and_hop_closed` |

**Manual checks (if no CLI):**
- Inspect exact signed approvals, independent external denial, fresh recovery/readback and authenticated production outcomes before recording completion.
