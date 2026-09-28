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
- [ ] Record the approved policy inputs above in `recovery-policy.json`, sign it, and bind all subsequent artifacts to its digest.
- [ ] Prove Keycloak PostgreSQL recovery:
  1. Capture the source `Cluster/keycloak-postgres` UID, PostgreSQL image digest, ready-instance count, database inventory counts and recovery timestamp without credential or row data.
  2. Create an operator-supported CloudNativePG physical backup to the approved off-node destination and wait for the operator's completed state. Record the backup resource UID, object-store identity, start/end timestamps, WAL boundary and manifest/checksum evidence.
  3. Restore into a new isolated namespace and a new CloudNativePG cluster; do not bind or replace `keycloak-postgres-1`, `keycloak-postgres-2` or any other live PVC.
  4. Require the restored cluster ready, database and schema inventory equal to the captured source inventory, Keycloak migrations readable, and an isolated Keycloak smoke check to succeed.
  5. Write and sign `keycloak-restore-proof.json`; backup completion without steps 3–4 is a failure.
- [ ] Prove OpenBao recovery:
  1. Take a fresh raft snapshot through the approved OpenBao operator workflow without exposing tokens, recovery keys or data values.
  2. Hash the snapshot, transfer it to the approved encrypted immutable off-node destination, and verify the destination object identity and checksum. The `openbao-snapshots` PVC is not an acceptable destination.
  3. Restore into the isolated target using custodian-controlled unseal/recovery material; never restore over `StatefulSet/hexalith-keys` or its live PVCs.
  4. Verify the restored raft is healthy and that sanitized mount/path counts and approved canary metadata match the source inventory; never include secret values in evidence.
  5. Write and sign `openbao-restore-proof.json`; a successful `CronJob/openbao-raft-snapshot` run alone is a failure.
- [ ] Prove Memories recovery by following [the module backup/restore contract](../../references/Hexalith.Memories/docs/operations/backup-restore.md):
  1. Enumerate every tenant and the deployment-owned intake/in-flight workflow controls. Approve the quiescence/resume playbook and keep intake paused on any failed or uncertain capture.
  2. Produce validated logical exports and checksums for every tenant. Produce the paired Redis/FalkorDB physical recovery point only through qualified CSI snapshots or the runbook's approved quiesced read-only maintenance-copy path.
  3. Store logical and physical artifacts together in the approved immutable off-node destination and bind their object identities, source PVC UIDs and checksums in one recovery manifest.
  4. Restore into an isolated namespace with new PVCs. For every tenant, require terminal restore counters to match the export and run `references/Hexalith.Memories/tools/verify-backup-recovery.py` against a consolidated tenant export.
  5. Preserve the verifier JSON, restore status bodies, checksums and smoke-test evidence; write and sign `memories-restore-proof.json`.
- [ ] Assemble and sign `backup-gate.json` containing the three proof digests, signatures, recovery-point IDs, off-node object identities, isolated target identities, verification timestamps, policy digest and expiry time.
- [ ] Have a second operator validate the signatures and every referenced immutable object, then mark Story 4.0 `done` only if all three proofs pass.

## Evidence contract

Each sanitized proof summary must contain `schemaVersion`, `system`, `sourceIdentity`, `recoveryPointId`, `capturedAt`, `offNodeObjectIdentity`, `artifactSha256`, `isolatedTargetIdentity`, `restoredAt`, `verificationChecks`, `verificationResult`, `operator`, `policySha256`, `expiresAt` and `signatureIdentity`. The signed full record and detached signature remain in the approved access-controlled evidence store; the committed summary contains no credentials, tokens, keys, database rows, tenant payloads or Secret values.

Story 4.1 must validate `backup-gate.json` and the three signed proofs directly. A checklist, job status, screenshot or unsigned prose summary is not a substitute.

## Hard gates and stop conditions

- Stop before capture if the destination is on `node1`, mutable by the source workload, unencrypted, or cannot be independently read back and checksummed.
- Stop Keycloak proof on backup/WAL failure, incomplete object-store evidence, non-isolated storage, restore failure or source/restore inventory mismatch.
- Stop OpenBao proof if a snapshot remains only on `openbao-snapshots`, the custodian ceremony is unavailable, the isolated raft is unhealthy, or verification would expose secret data.
- Stop Memories proof if quiescence evidence is missing/stale/non-zero, if only one physical store is captured, if snapshots from different attempts are paired, or if the repository verifier fails. Keep intake paused until incident command makes an explicit safe decision.
- Never delete or replace a live PVC. Never layer a restore over a dirty or uncertain target.
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
**Then** all three proof signatures, immutable object identities, checksums and freshness bounds pass
**And** any missing or failed validation blocks the upgrade
