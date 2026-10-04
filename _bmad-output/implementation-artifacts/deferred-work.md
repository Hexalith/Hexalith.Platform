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

- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Restore the explicit requirement for accepted Story 4.2 exposure closure and Story 4.3 runner relocation before staging in the generated Epic 4 agent context.
  evidence: Review E13 verified that refreshing `epic-4-context.md` removed the previous explicit staging gate and the replacement dependency sequence omits it; upstream approved requirements retain both controls. Deferred because the workflow routes agent-context edits to deferred work.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Restore the Story 4.1 post-upgrade evidence, Story 4.3 runner relocation/namespace removal/no-cluster-access, registry immutability/per-environment credential, staging profile-template and Traefik/Gateway requirements dropped from the regenerated `epic-4-context.md` (review loop 2, G45).
  evidence: The baseline diff removes those bullets from `epic-4-context.md` while `epics.md` still requires them; the fix edits agent context, which the review workflow defers.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Prove that the rehearsal fixture's internal Docker network cannot reach the Docker bridge/host or other source addresses (node InternalIP, etcd advertise address) besides the probed endpoint host (review loop 2, G46).
  evidence: Unverified, medium if true. `rehearse.py` probes only the source endpoint host and 1.1.1.1:443. Settle by probing the bridge gateway and every source address from inside a fresh fixture and recording the address and failure mode.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Track the Administrator's host-access remediation actions recorded in private custody (review loop 3, G51).
  evidence: Review B41 found the actions listed only in prose. Their specifics are held in private custody: `passage-07.age` of attempt `20261003t103215z-custody-redaction` (ciphertext SHA-256 `5c5732fe3e8b45fe01d9bcf19eaee382905acae8800bdbb9187335536eba7cab`; criterion 5 remains unmet). Deferred because the underlying conditions predate this story and remediation is an Administrator decision.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Record a critical path and fallback for leaving Kubernetes 1.34 before its 2026-10-27 end of life (review loop 3, G53).
  evidence: All eight 4.26 criteria are open and 4.27 has not started, and nothing records a critical path or fallback. Deferred because the schedule belongs to Story 4.1, which predates this story.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Restore off-cluster record storage, the distinct staging promotion-stop state, separate-writer off-site replication and signed release records dropped from the regenerated `epic-4-context.md` (review loop 3, G55).
  evidence: Review B46 found these dropped beyond G45's list while the upstream planning artifacts still require them. Deferred because the fix edits agent context, which the review workflow defers.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Check whether any production API group serves a resource only in a non-preferred version, which `collect()` would miss (review loop 3, G71).
  evidence: Unverified, medium if true. `collect()` lists each group's preferred version and adds non-preferred versions only for CRDs. Settle by comparing per-version discovery on production.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Configure the EventStore host's advertised request body limit through Kestrel options during host construction.
  evidence: G87; Program.cs resolves raw KestrelServerOptions with GetService in its ApplicationStarted callback, with no raw service registration, so the nullable assignment skips the one-megabyte limit. This file belongs to separate earlier platform commits.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Reject out-of-range or fractional actorRevision values in the real platform identity admission provider.
  evidence: G88; PlatformIdentityGatewayAdmission.AdmitAsync uses JsonElement.GetInt64 after resolving an active actor, but FormatException is absent from its rejection catch filter, allowing malformed numeric revisions to escape as server errors. The identity provider is separate earlier platform work.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Add executing admission tests for the real registered platform identity gateway provider.
  evidence: G89; verification-layer symbol/import searches found no test invoking PlatformIdentityGatewayAdmission.AdmitAsync. Enrollment/login and mocked controller denial tests cannot detect source allowlist, actor revision or operator provenance regressions in that separate identity workstream.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Assign an owner for artifact signature and image SBOM/scanning verification of the Rancher/K3s management deployment.
  evidence: G103, from the 2026-10-04 code review of the fix commit `0e602ad..dc9a6ab`. `eng/cluster-management/RANCHER.md:35` defers "artifact signatures and comprehensive image SBOM/scanning" to "later deployment hardening", but neither the 4.28 story nor this ledger owns them. The spec follow-through still requires signature evidence. Medium: a 4.28 installation could proceed without authenticity checks.

- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Restore a buildable default package-reference dependency lane for the pre-existing Platform identity project (G106).
  evidence: Blind B86 and an independent root `dotnet build Hexalith.Platform.slnx --no-restore` both fail with 26 missing identity/security type errors. Directory.Build.props defaults UseHexalithProjectReferences=false, while the published Gateway 3.110.0 dependency lacks the consumed APIs. The project and dependency setup predate this resumed qualification run; fix in the separate identity workstream.
- source_spec: `_bmad-output/implementation-artifacts/spec-4-26-qualify-rancher-and-the-management-migration.md`
  summary: Make the pre-existing global-login alias test assert real registry lookup aliases (G107).
  evidence: Verification-gap V38 found SameVerifiedLoginAcrossTenantClaims_UsesOneGlobalActorAndNeverEnrolls returns one registry entry for Arg.Any<string>() and compares those predetermined actor IDs. Tenant-dependent alias derivation therefore evades its assertions; use an alias-bound registry response and verify different-login denial in the identity workstream. This test predates the resumed qualification run.
