- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-4-0-4-3-start-urgent-kubernetes-upgrade-track.md`
  summary: Restore the completed `stepsCompleted` markers in the Epic plan.
  evidence: Both review lenses found that concurrent `/pushall` commit `63af0c72` changed the completed marker list to `[]`, which can make planning automation treat the artifact as unvalidated.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-4-0-4-3-start-urgent-kubernetes-upgrade-track.md`
  summary: Reconcile the accepted Kubernetes 1.34 staging risk with the requirement to upgrade before staging exists.
  evidence: Pre-existing `epics.md` AR-64 permits staging on 1.34 after 2026-10-27 while Story 4.1 requires a supported minor before staging; one sequence must become authoritative.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-4-0-4-3-start-urgent-kubernetes-upgrade-track.md`
  summary: Make recovery-job credential cleanup fail safe on abnormal executor termination.
  evidence: Concurrent AR-28 text promises destruction or revocation at job end but does not define a short lease or out-of-band finalizer for a crashed or killed job.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-4-0-4-3-start-urgent-kubernetes-upgrade-track.md`
  summary: Assign every unlisted Kubernetes object kind to exactly one release tier.
  evidence: Concurrent AR-32 text requires a unique tier but does not classify RBAC, ServiceAccounts, CRDs, PDBs or admission objects, allowing ownership and rollback behavior to diverge.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/spec-4-0-4-3-start-urgent-kubernetes-upgrade-track.md`
  summary: Add router-level coverage for the canonical Tenants workspace URLs.
  evidence: Existing tests verify aliases or render `TenantsWorkspace` directly; neither `/tenants/tenants` nor `/tenants/workspace-users` is routed through the production assembly, so the generic landing-page catch-all can win unnoticed.
