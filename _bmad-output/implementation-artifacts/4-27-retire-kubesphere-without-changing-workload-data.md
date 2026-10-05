---
title: Retire KubeSphere without changing workload data
type: story
epic: 4
story: 27
created: 2026-10-01
status: in-progress
route: dispatch
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/specs/spec-kubesphere-to-rancher/SPEC.md'
depends_on:
  - 4-26-qualify-rancher-and-the-management-migration
  - 4-0-prove-off-node-backups-and-isolated-restores
---

# Story 4.27: Retire KubeSphere without changing workload data

As Administrator, I want KubeSphere removed through a rehearsed native procedure while applications and recovery inputs are preserved, so that its licensing and compatibility no longer block the supported Kubernetes hop.

**Repo:** Administrator operations. **Covers:** AR-59, AR-63, FR-9, NFR-3. **Dependencies:** 4.26 retirement qualification and current validated 4.0 proofs; precedes 4.1 mutation. No dependency on Rancher availability.

Acceptance criteria:

1. Before mutation, obtain approval bound to the exact removal procedure, resource allowlist, workload interruption scope, incident/recovery owner, window and stop conditions. Independently verify fresh encrypted immutable off-node native workload proofs, external-etcd recovery and node/configuration recovery inputs under the approved attempt policy. Preserve sources and qualifying prior evidence.
2. Capture pre-change native API, authenticated workload health, controllers/replicas, namespace/resource ownership, admissions, storage and PVC UID/binding census. Verify independent private Administrator access again. Drift from the qualified deletion/dependency inventory stops execution.
3. Archive KubeSphere configuration and required metadata under restricted encrypted custody. Rehome any required extension/controller or resource ownership before removing its reconciler. Preserve public application/OIDC service contracts.
4. Apply only the rehearsed, UID-bound uninstall steps to KubeSphere-owned resources. Do not delete application/data namespaces, PVCs/PVs, shared storage/CNI/ingress/identity components or unrelated CRDs. Any finalizer intervention or changed deletion propagation requires the specific approved resource decision; no blanket forced deletion is permitted.
5. Verify the retired releases/controllers, obsolete API services/admission references and public console route are absent; classify any intentionally retained inert archival resources with owner and cleanup date. No unresolved KubeSphere-only runtime/admission dependency may remain on the 1.35 path.
6. Prove native API and private administration, DNS/CNI/storage/admission health, unchanged application/data namespace and PVC identities/bindings, and authenticated protected-workload smoke outcomes. Check old public console access from outside the private path. A failed/unobserved result stops further work and does not mark retirement done.
7. Record signed before/after inventories, deletion identities, preserved-state comparisons, versioned runbook digest and accountable outcome. Prepare a fresh post-retirement etcd point and configuration inventory for 4.1; old-point restoration must explicitly reconcile retired controllers and authority before reopening.
8. Handoff to 4.1 only with accepted retirement evidence. Keep all remaining upgrade blockers, freshness checks and approvals in force; no Kubernetes binary change, drain or upgrade is implied by retirement completion.

## Implementation handoff

Administrator approved the [course correction](../planning-artifacts/sprint-change-proposal-2026-10-01.md). This scoped input is backlog, not an executed qualification. Use the [migration spec](../specs/spec-kubesphere-to-rancher/SPEC.md) and [operations handoff](../../eng/cluster-management/README.md). Retain exact identities, sanitized attempt records and encrypted private inventories; require acceptance evidence and applicable exact procedure approvals before mutation or completion. No live inspection, deletion or installation is recorded by this file.

## Administrator decisions from Story 4.26 — 2026-10-03

- **Console route.** After the Story 4.2 closure, retirement removes the console Ingress, its Certificate and TLS Secret, and the manager Lease.
- **Namespaces.** Remove only the KubeSphere finalizer from the seven finalized namespaces, and keep the namespaces.

## 2026-10-04 owner decision and implementation handoff

- **Retained objects (Administrator decision G108).** The license Secrets, `Cluster/host` and app Category are inert archive. The five `jpiquot` objects in the [retained-object table](../../eng/cluster-management/RETIRE-KUBESPHERE.md#approved-retirement-decisions-2026-10-04) form active authority, including a cluster-admin binding. The Administrator is custodian and approved a 2026-11-03 review date. The G108 decision leaves 4.27's scope unchanged; 4.28 removes the `jpiquot` path and its dangling ServiceAccount subject once the named credential is live.
- **Native administration first (review handoff G110).** Before any deletion, revalidate the recovered existing native client and its unowned cluster-admin authority as the independent path. The named one-year Administrator certificate with its own unowned binding is issued and tested in 4.28; its issuance does not gate 4.27. Sealed native break-glass remains independent of both managers, with custody specifics retained privately.
- **Recovery route (review clarification G111).** Before mutation, capture a fresh production external-etcd recovery point and prove a fenced isolated restore under 4.1's `external-etcd-recovery-point.json` / `external-etcd-isolated-restore.json` contract and 4.0's proof/custody policy, with matching node/configuration recovery. Never restore into the running cluster.
- **Exact execution bytes (review-derived prerequisite G109).** Name the executor issuing production requests. Rerun the full catalog retirement, seven-namespace interventions and fresh-node snapshot/restore fixtures with the exact procedure, driver and imported-module bytes 4.27 will execute, then regenerate the fresh source-bound production plan bound to that procedure digest and those receipts. The kind-bound qualification `Fixture` is not a production executor; a later procedure change requires renewed rehearsal and plan binding before approval.

The exact procedure and allowlist come from the Story 4.26 handoff. Each production change still needs its own approval.

## 2026-10-05 implementation and current execution gate

- [Production executor](../../eng/cluster-management/retire.py) now makes a fresh explicit-native proposal, verifies original SSH signatures and current 4.0/4.1/4.2 prerequisite bindings, and issues requests only after an exact plan/allowlist/procedure/tool/receipt/outage/owner/window approval and matching explicit arm. Separate immutable planning and execution IDs prevent custody reuse. No production attempt was armed or executed by this implementation.
- [Shared phase guards](../../eng/cluster-management/rehearse.py) are transport independent. The production client preserves the exact deletion, named finalizer and content-transition guards, retains every request and stops at the first refusal. Each subsequent mutation checks protected identities/storage, retained full content and authority, current source identity, bound tool/credential/trust/code bytes, unchanged signed evidence and remaining freshness/window. Partial outcomes require a new incident decision; no automatic restoration occurs.
- [Evidence custody](../../eng/cluster-management/evidence.py) supports immutable private 4.27 attempts and signed sanitized receipt publication while preserving existing 4.26 receipts. Native inventories, command bodies/results, signed original inputs and diagnostics are encrypted for two recipients and read back before promotion to a receipt. This local readback does not claim independent off-node recovery custody.
- [Operator handoff](../../eng/cluster-management/RETIRE-KUBESPHERE.md#production-executor-and-exact-attempt-handoff-2026-10-05) defines exact CLI inputs, private signed record contracts, plan/approval transfer, per-phase stops and postflight assessment. Successful phases remain `retired-awaiting-acceptance`; failed/unobserved health, external denial or fresh recovery inputs leave the story incomplete. A separate read-only assessment requires fresh signed authenticated Keycloak/OpenBao/Memories/Forgejo outcomes, private native access, external console denial, new post-retirement etcd/node configuration inputs and exact Administrator acceptance.
- Verification: all **224** cluster-management tests pass, including 47 new executor tests with real SSH signatures and native transport refusal cases. Verbose local audit: `/tmp/story-4-27-full-suite.log`. The final-byte [catalog and fresh-node rehearsal](evidence/epic-4/4-27/20261005t104023z-s427-catalog/qualification.json) completed all nineteen phases and 471 exact removals with 46 CRDs retained, then restored all 798 baseline identities with zero baseline/CRD UID mismatches, canary readback, hash checking, five ready manager deployments, 845 verified encrypted readbacks and cleanup. The final-byte [seven-namespace rehearsal](evidence/epic-4/4-27/20261005t104023z-s427-namespaces/qualification.json) passed seven guarded PUTs, zero Namespace DELETEs, preservation, 85 verified encrypted readbacks and cleanup. Both remain `passed-limited` synthetic outcomes; production acceptance remains unobserved.
- The current-executor [unmocked native census smoke](evidence/epic-4/4-27/20261005t104956z-s427-native-census/qualification.json) passed inside the isolated node with synthetic credentials: 295 observed resources, zero mutations, 89 verified encrypted readbacks and temporary-input cleanup. The [local binding audit](evidence/epic-4/4-27/20261005t104023z-s427-verification/verification.json) ran the real `validate_rehearsals` against both final authoritative private manifests and current code/procedure digests. Pure `dependency_plan` / `actions_for` / `validate_production_scope` checks on the actual historical fixture census passed: 2,596 resources, nineteen phases, 482 deletion identities, seven namespace interventions, five retained authority objects and none in scope. These checks created no fresh production plan, approval, mutation or acceptance. Superseded completed fixtures and earlier smoke/setup refusals remain immutable under their original identities in compact 4.27 projections; authoritative attempts stay private, and existing 4.26 evidence stays untouched.

- **Measured operability gate:** the historical census includes 13 Leases. The retained [synthetic native observation](evidence/epic-4/4-27/20261005t105545z-s427-lease-handoff/qualification.json) requested a 12-second pause and measured a 15.205-second response interval: the Node-Lease UID and every other desired-content field stayed unchanged while `spec.renewTime` and its full desired-content digest changed. Both actual command/results remain in four encrypted exports with verified two-recipient readback. The strict plan-to-execution comparison refuses normal renewal with zero mutations. Production activation remains gated pending an explicit intent decision; no live-Lease exception was added and the qualified post-controller manager Lease checkpoint remains unchanged.

**Current live gate: closed.** No fresh production plan bound to these final-byte receipts, accepted independent 4.2 console closure, fresh attempt-specific production recovery/readback, separate exact execution approval, production preservation/retirement acceptance or post-retirement 4.1 recovery inputs are supplied by this code change. Keep 4.27 `in-progress` and 4.1's native-hop gate closed. Preserve `jpiquot` authority for its separately approved removal in 4.28 and preserve hashed `MAINTENANCE.md`.
