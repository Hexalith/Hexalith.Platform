---
title: 'Prove off-node backups and isolated restores'
type: 'story'
epic: 4
story: 0
created: '2026-09-28'
status: 'in-progress'
route: 'dispatch'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/evidence/epic-4/initial-cluster-inventory.md'
depends_on: []
blocks:
  - '4-1-upgrade-the-cluster-off-kubernetes-1-34-after-verified-backu'
---

# Story 4.0: Prove off-node backups and isolated restores

As Administrator,
I want restorable off-node backups for Keycloak PostgreSQL, shared OpenBao and Memories,
So that the Kubernetes upgrade has independently proven recovery points.

## Scope and current state

This story owns the backup and restore proof split from readiness finding C-15. It does not authorize a Kubernetes upgrade or replacement of any live PVC.

Discovery on 2026-09-28 found:

- `Cluster/keycloak-postgres` in namespace `keycloak` has two ready instances and no cluster-wide `Backup` or `ScheduledBackup` resources.
- `StatefulSet/hexalith-keys` in namespace `openbao` has three ready replicas. `CronJob/openbao-raft-snapshot` writes to PVC `openbao-snapshots`, whose retained local volume is pinned to `node1`; job success is not an off-node restore proof.
- `StatefulSet/redis-stack` and `StatefulSet/falkordb` in namespace `hexalith-memories` use PVCs `data-redis-stack-0` and `data-falkordb-0`. The cluster does not serve the `snapshot.storage.k8s.io` API.

The sanitized observations are in [initial-cluster-inventory.md](evidence/epic-4/initial-cluster-inventory.md). They are blockers, not completed acceptance.

## Dependencies and Administrator inputs

Before taking a backup, the Administrator records one approved evidence bundle location and, for each system, the accountable operator, recovery-point objective, retention, restore-test cadence, encrypted immutable off-node destination, restore target, cleanup owner and maximum proof age accepted by Story 4.1.

The following infrastructure decisions are required:

1. Keycloak: operator-supported CloudNativePG object-store backup destination and credentials, supplied without committing or printing secret values.
2. OpenBao: off-node immutable destination plus an isolated restore target whose unseal/recovery-key ceremony is controlled by the existing custodians.
3. Memories: either a qualified CSI snapshot API/class and restore `StorageClass`, or the deployment-owned, read-only maintenance-pod copy procedure allowed by the module runbook. The current cluster cannot execute the runbook's CSI path.
4. Signing: the Administrator-approved signer and verification trust root for the three proof records and their aggregate gate record.

## Ordered tasks

- [x] Record the sanitized initial inventory without Secret data.
- [ ] Create an access-controlled evidence directory `evidence/epic-4/4-0/<recovery-id>/` outside Git for full operational records; commit only sanitized summaries and cryptographic digests.
- [ ] Record the approved policy inputs above in `recovery-policy.json`, sign it, and bind all subsequent artifacts to its digest. For each source, record the source Kubernetes UID plus its system-native incarnation/index and the data cutoff; prove the cutoff satisfies the approved RPO at validation time.
- [ ] Maintain a signed `evidence-manifest.json` that covers every backup, restore, verification, validator and cleanup object with its immutable object identity, byte length and SHA-256; encryption-at-rest/key-custody evidence; object-lock/immutability mode and expiry; retention expiry; provider account/region/failure-domain identity; source UID/incarnation/index; data cutoff and measured RPO. Independently read back and checksum every listed object before signing the final manifest.
- [ ] Prove Keycloak PostgreSQL recovery:
  1. Capture the source `Cluster/keycloak-postgres` UID, PostgreSQL system identifier/timeline and WAL LSN, image digest, ready-instance count, schema/catalog digest, database/table/realm/client/user/role counts and approved non-secret canary digests without credential or row data.
  2. Create an operator-supported CloudNativePG physical backup to the approved off-node destination and wait for the operator's completed state. Record the backup resource UID, object-store identity, start/end timestamps, WAL boundary and manifest/checksum evidence.
  3. Restore into a new isolated namespace and a new CloudNativePG cluster; do not bind or replace `keycloak-postgres-1`, `keycloak-postgres-2` or any other live PVC. Prove a distinct ServiceAccount/RBAC boundary, default-deny network boundary with failed source/live-service probes, and new PV/PVC UIDs and provider volume handles with no attachment to source storage.
  4. Require the restored cluster ready; PostgreSQL system identity to be a valid recovery descendant at the recorded WAL cutoff; schema/catalog digest, inventory counts and canary digests to match; Keycloak migrations readable; and isolated login/OIDC plus representative realm/client/role checks to succeed.
  5. Write and sign `keycloak-restore-proof.json`; backup completion without steps 3–4 is a failure.
- [ ] Prove OpenBao recovery:
  1. Capture the source StatefulSet UID, raft cluster identifier, peer/member set, term, commit/applied index, snapshot index, enabled non-secret mount/path inventory digest and approved canary metadata digest; then take a fresh raft snapshot through the approved OpenBao operator workflow without exposing tokens, recovery keys or data values.
  2. Hash the snapshot, transfer it to the approved encrypted immutable off-node destination, and verify the destination object identity and checksum. The `openbao-snapshots` PVC is not an acceptable destination.
  3. Restore into the isolated target using custodian-controlled unseal/recovery material; never restore over `StatefulSet/hexalith-keys` or its live PVCs. Prove the target identity cannot access source namespace resources, source services or source storage and that all restored volumes have distinct UIDs/handles.
  4. Verify the restored raft is healthy, its recovered applied index reaches the signed snapshot index, and the peer shape, sanitized mount/path inventory digest and approved canary metadata digest match the source evidence; never include secret values in evidence.
  5. Write and sign `openbao-restore-proof.json`; a successful `CronJob/openbao-raft-snapshot` run alone is a failure.
- [ ] Prove Memories recovery by following [the module backup/restore contract](../../references/Hexalith.Memories/docs/operations/backup-restore.md):
  1. Enumerate every tenant and the deployment-owned intake/in-flight workflow controls. Approve the quiescence/resume playbook and keep intake paused on any failed or uncertain capture.
  2. Produce validated logical exports and checksums for every tenant. Produce the paired Redis/FalkorDB physical recovery point only through qualified CSI snapshots or the runbook's approved quiesced read-only maintenance-copy path.
  3. Store logical and physical artifacts together in the approved immutable off-node destination and bind their object identities, source PVC UIDs and checksums in one recovery manifest.
  4. Restore into an isolated namespace with a distinct ServiceAccount/RBAC boundary, default-deny network policy and new PVC/PV UIDs/provider handles. Prove denied source namespace/API/live-service access and no source-volume attachment. For every tenant, require terminal restore counters to match the export and run `references/Hexalith.Memories/tools/verify-backup-recovery.py` against a consolidated tenant export.
  5. Explicitly resume source intake through the approved playbook, reconcile every queued/in-flight workflow captured at quiescence, and prove terminal processing with no missing or duplicate work before writing and signing `memories-restore-proof.json`.
  6. Preserve the verifier JSON, restore status bodies, checksums, resume/reconciliation result and smoke-test evidence.
- [ ] After evidence capture, remove every isolated restore target through its approved cleanup procedure. A second operator must sign `restore-target-cleanup.json` after proving namespace/workload/RBAC/network resources absent and checking PVCs, PVs, VolumeSnapshots/contents and provider volumes/snapshots for no unapproved residual storage.
- [ ] Assemble and sign `backup-gate.json` containing the final evidence-manifest digest, the three proof digests/signatures, recovery-point IDs, data cutoffs/RPO results, off-node object/storage-property validation, isolated-target validation, cleanup proof, verification timestamps, policy digest and expiry time.
- [ ] Have a second operator, using an identity independent of the backup operator, validate signatures, source-incarnation bindings, every manifest entry and storage property by provider/API evidence plus read-back checksums; sign `independent-validation.json` and mark Story 4.0 `done` only if all three proofs pass.

## Evidence contract

Each sanitized proof summary must contain `schemaVersion`, `system`, `sourceIdentity`, `sourceUid`, `sourceIncarnation`, `sourceIndex`, `dataCutoff`, `measuredRpo`, `recoveryPointId`, `capturedAt`, `offNodeObjectIdentity`, `artifactSha256`, `storageProperties`, `isolatedTargetIdentity`, `isolationChecks`, `restoredAt`, `verificationChecks`, `verificationResult`, `cleanupProofSha256`, `operator`, `validator`, `policySha256`, `evidenceManifestSha256`, `expiresAt` and `signatureIdentity`. The signed full record and detached signature remain in the approved access-controlled evidence store; the committed summary contains no credentials, tokens, keys, database rows, tenant payloads or Secret values.

Story 4.1 must validate `backup-gate.json` and the three signed proofs directly. A checklist, job status, screenshot or unsigned prose summary is not a substitute.

## Hard gates and stop conditions

- Stop before capture if the destination is on `node1`, mutable by the source workload, unencrypted, or cannot be independently read back and checksummed.
- Stop if encryption, immutability/object lock, retention, failure-domain separation, data-cutoff/RPO compliance, source-incarnation binding or independent validator evidence is absent or contradicted.
- Stop Keycloak proof on backup/WAL failure, incomplete object-store evidence, non-isolated storage, restore failure or source/restore inventory mismatch.
- Stop OpenBao proof if a snapshot remains only on `openbao-snapshots`, the custodian ceremony is unavailable, the isolated raft is unhealthy, or verification would expose secret data.
- Stop Memories proof if quiescence evidence is missing/stale/non-zero, if only one physical store is captured, if snapshots from different attempts are paired, or if the repository verifier fails. Keep intake paused until incident command makes an explicit safe decision.
- Never delete or replace a live PVC. Never layer a restore over a dirty or uncertain target.
- Do not close the proof while an isolated target or provider storage object remains outside the approved retention set, or while Memories intake/resume reconciliation is incomplete.
- Any absent, failed, stale or unverifiable proof keeps this story open and Story 4.1's upgrade gate closed.

## Acceptance criteria

**Given** the three live data systems
**When** Story 4.0 completes
**Then** each has an encrypted immutable recovery point off `node1`
**And** each has a signed proof from a successful isolated restore into new storage

**Given** the Memories proof
**When** it is reviewed
**Then** it follows the module-owned backup/restore contract
**And** `verify-backup-recovery.py` has passed for every consolidated tenant export

**Given** `backup-gate.json`
**When** Story 4.1 validates it
**Then** an independent validator's signature covers the final evidence manifest, every object checksum/storage property, source-incarnation and data-cutoff/RPO binding, isolation tests, successful Memories resume/reconciliation and signed residual-storage cleanup
**And** any missing or failed validation blocks the upgrade
