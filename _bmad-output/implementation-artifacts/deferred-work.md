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
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/4-0-prove-off-node-backups-and-isolated-restores.md`
  summary: Make Story 4.1's mutation gate also verify the Administrator-signed, passing Story 4.0 `validation.json`, not only `backup-gate.json` and the three proofs.
  evidence: Story 4.0 signs `backup-gate.json` before `validation.json` exists, and Story 4.1's gate item 2 plus the 4.0 evidence contract check only the gate and proofs, so a failed validation blocks the upgrade only through the sprint-status `done` flag (review finding B3, 2026-10-01).
  status: resolved
  resolution: Story 4.1 now requires signed passing fresh final validation with exact gate/manifest/policy/recovery-ID bindings independently of sprint status. Its preparation tool always closes mutation authorization, and operational attempt 20261001t075120z-backup-verified directly verifies those bindings/signatures and retains signed gate-validation.json; see evidence/epic-4/4-1/20261001t075120z-backup-verified/summary.md. Remaining hop gates are still closed.
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/4-0-prove-off-node-backups-and-isolated-restores.md`
  summary: Give the CloudNativePG barman-cloud plugin a credential scoped to the `keycloak/` prefix instead of the general recovery writer key.
  evidence: Secret `keycloak/recovery-writer-s3` holds the writer key inside the source workload's namespace, so that workload can add new latest versions under `openbao/`, `memories/` and `evidence/`; object lock keeps existing versions immutable and `validate.py` would fail on a forged latest record, but it is an avoidable tamper and denial path (review finding B15b, 2026-10-01).
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/4-0-prove-off-node-backups-and-isolated-restores.md`
  summary: Complete the renumbering note in `implementation-readiness.md` and pin the superseded 2026-09-28 report by commit.
  evidence: The note says two renumberings apply, but the same sprint-status diff also shifts 5.9→5.10, 5.10→5.11 and 8.16→8.19 and retitles 6.19, 6.20 and 6.22; the "carried forward unchanged" lists point to an overwritten report with no commit hash (review finding B19, 2026-10-01).
- source_spec: `/home/administrator/projects/hexalith/platform/_bmad-output/implementation-artifacts/4-0-prove-off-node-backups-and-isolated-restores.md`
  summary: Remove two rehearsal assumptions from the retained final-run tooling before the next backup proof run.
  evidence: Fresh run 20261001t061620z required a source-pre-derived memories/census.json input because mem.py manifest reads it while final-run-order.sh does not create it. proofs.py also emitted an OpenBao isolatedTargetIdentity.abortedAttempt note despite no aborted target in that fresh run. The input was supplied from the real census without changing retained tool checksums; a signed final-run-observations.json supplement clarifies the descriptive note and binds the original signed records. See evidence/epic-4/4-0/20261001t061620z/summary.md.
