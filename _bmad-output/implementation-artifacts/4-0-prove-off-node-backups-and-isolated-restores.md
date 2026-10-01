---
title: 'Prove off-node backups and isolated restores'
type: 'story'
epic: 4
story: 0
created: '2026-09-28'
status: 'in-progress'
route: 'dispatch'
baseline_commit: '6ac920bd26a38b7b50818b62f6562199c6014005'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/evidence/epic-4/initial-cluster-inventory.md'
  - '{project-root}/_bmad-output/implementation-artifacts/evidence/epic-4/scaleway-backup-audit.md'
  - '{project-root}/_bmad-output/implementation-artifacts/evidence/epic-4/recovery-destination-setup.md'
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

The sanitized observations are in [initial-cluster-inventory.md](evidence/epic-4/initial-cluster-inventory.md). The read-only follow-up of the existing provider is in [scaleway-backup-audit.md](evidence/epic-4/scaleway-backup-audit.md). They are blockers, not completed acceptance.

## Dependencies and Administrator inputs

Before taking a backup, the Administrator records one approved evidence bundle location and, for each system, the accountable operator, recovery-point objective, retention, restore-test cadence, encrypted immutable off-node destination, restore target, cleanup owner and maximum proof age accepted by Story 4.1.

The Administrator approved these policy defaults on 2026-09-28: recovery-point objective `30m`, retention `30d`, restore-test cadence `monthly`, and maximum proof age `24h`. The Administrator also identified Scaleway as the existing backup provider. Exact Scaleway object identities, immutability, encryption, retention, failure-domain placement, credentials references and read-back checksums remain to be verified; the provider statement alone is not recovery evidence. Identity, custody, cleanup-owner and system-access inputs remain subject to the hard gates below and must not be inferred from these approvals.

On 2026-09-29 the Administrator chose the destination: a new dedicated Scaleway Object Storage bucket in `fr-par`, created with versioning and object lock enabled and a default `COMPLIANCE` retention of 30 days. It holds only Story 4.0 recovery points and evidence. `hexalith-velero-backups` stays unchanged and is not a Story 4.0 destination; a re-check that day still found no versioning, object lock, lifecycle or bucket policy on it. The new bucket needs a backup write credential that cannot delete objects or bypass retention and a separate read-only validator credential; the Velero credential must have no access to it. Velero was moved the same day from an organization-admin personal key to scoped application `hexalith-velero`, which cannot reach this bucket or IAM (see [recovery-destination-setup.md](evidence/epic-4/recovery-destination-setup.md#velero-key-replacement)). The destination was set up and read back on 2026-09-29: bucket `hexalith-recovery-points` in dedicated Scaleway project `hexalith-recovery`, with writer application `hexalith-recovery-writer` for backups and validator application `hexalith-recovery-validator` for read-only validation. Identities, settings and probe results are in [recovery-destination-setup.md](evidence/epic-4/recovery-destination-setup.md). Backups write only under the lifecycle-managed prefixes `keycloak/`, `openbao/` and `memories/`; sanitized evidence copies go under `evidence/`.

The Administrator also decided on 2026-09-29:

- The bucket and credentials were set up on the Administrator's behalf with their organization-owner key. Application keys are in `~/.config/hexalith-recovery/writer.env` and `validator.env` on the Administrator's workstation and are referenced by path only; key values are never printed, logged or committed. The validator key stays in the Administrator's custody as the read-only credential used for validation.
- Keycloak uses the CloudNativePG barman-cloud plugin with the new bucket; the live instance image is not changed.
- Proof records are signed with SSH signatures (`ssh-keygen -Y sign -n hexalith-recovery`), replacing the earlier GPG choice because no GPG keys exist. The Administrator signs with `~/.ssh/id_ed25519_git_signing` (`jpiquot@itaneo.com`, `SHA256:8XlNQvE3ucPf/e509wU4qtNgiyWA+TKmLei7F7+TCvk`, passphrase-protected). An `allowed_signers` file holding that public key is the verification trust root. Signing is always done by the Administrator entering their own passphrase; the implementer prepares the records and never signs on anyone's behalf.
- On 2026-10-01 the Administrator decided to sign every record alone, including cleanup and validation, so Story 4.0 has no second-person validator. This still meets `epics.md`, which requires signed proofs naming the accountable operator. As a safeguard, validation re-reads every object and storage property with the read-only validator credential, not the writer that made the backups. It does not rely on values the capture scripts recorded. `pduong@itaneo.com` keeps read-only bucket access (see [recovery-destination-setup.md](evidence/epic-4/recovery-destination-setup.md#independent-validator-identity)) for an optional extra check; the gate does not require it.
- OpenBao has no separate custodians. It auto-unseals with `seal "static"` from Secret `openbao/openbao-seal`; its `shamir` 2-of-3 recovery shares are in Secret `openbao/openbao-operator-credentials` and are not escrowed. The module's OpenBao runbook names the Administrator as owner. On 2026-09-29 the Administrator approved a temporary copy of the static seal key into the isolated restore namespace only; it must be deleted with that namespace and proven absent in `restore-target-cleanup.json`. This approval is the custodian ceremony for Step 3 of the OpenBao proof.
- Evidence bundle location, set on 2026-09-29 under the Administrator's delegation: full operational records live in `~/hexalith-recovery-evidence/4-0/<recovery-id>/` on the Administrator's workstation (directory mode `0700`, outside Git), and every signed record plus its detached signature is uploaded by the writer to `s3://hexalith-recovery-points/evidence/4-0/<recovery-id>/`, where object lock keeps it immutable. Git holds only sanitized summaries and digests under `_bmad-output/implementation-artifacts/evidence/epic-4/`.
- Memories intake may be paused whenever the proof needs it. No deployment-owned quiescence/resume playbook exists yet, so the implementer writes it first and the Administrator approves it before use. On 2026-09-29 Redis held 3 keys (about 2 MB); `data-redis-stack-0` and `data-falkordb-0` are `openebs-hostpath-retain` volumes of 20Gi and 10Gi. Intake stays paused on any failed or uncertain capture.

After reviewing recovery run `20260929t124806z`, the Administrator decided on 2026-09-29:

- That run is a rehearsal. Its Keycloak and OpenBao proofs expire before the remaining steps can finish, so the Administrator does not sign them. When every open item is ready, a final fresh capture, restore and cleanup of all three systems runs shortly before the Story 4.1 upgrade, using the kept scripts. Only that run is signed, assembled into `backup-gate.json` and validated.
- The additions from that run are approved and kept: `ScheduledBackup/keycloak/keycloak-postgres-daily` at 02:00 UTC, and CloudNativePG `WATCH_NAMESPACE=keycloak,cnpg-system`.
- The [Memories quiescence/resume playbook](evidence/epic-4/memories-quiescence-resume-playbook.md) is approved with these answers:
  - **D1:** Memories has zero tenants. The logical proof records that from the census (no `tenant-registry-index`), so there is no tenant export to run `verify-backup-recovery.py` against. The proof must say so explicitly rather than claim a verifier pass.
  - **D2:** not needed, because no tenant API export runs.
  - **D3:** the isolated restore covers only the paired physical copies. It restores `redis-stack` and `falkordb` under those names into a new namespace with default-deny networking and no egress exception, and needs no embedding-provider credential or `memories` app. It passes when the restored key, graph and `memories-events` stream counts and consumer-group state match the pre-copy census.
  - **D4:** the full Memories outage while both StatefulSets are at 0 for the copy is approved.
  - **D5:** the maximum quiescence-evidence age is 900 s.
  - **Resume:** the implementer may resume Memories as soon as the restore proof passes and the drain re-check is clean. Any failed or uncertain step keeps intake paused and goes back to the Administrator.
- Run the Memories rehearsal now, so the final run repeats a proven procedure.

The following infrastructure decisions are required:

1. Keycloak: operator-supported CloudNativePG object-store backup destination and credentials, supplied without committing or printing secret values. On 2026-09-29 the cluster ran CloudNativePG `1.30.0` with instance image `docker.io/library/postgres:15.15`, which has no `barman-cloud` binaries, so the in-tree `spec.backup.barmanObjectStore` method cannot run without an image change. The barman-cloud plugin needs no image change; cert-manager `v1.21.2`, which the plugin requires, is installed. Enabling the plugin on `Cluster/keycloak-postgres` triggers a rolling update of both instances ([plugin migration docs](https://cloudnative-pg.io/plugin-barman-cloud/docs/migration/)), so Keycloak briefly loses its database when the primary restarts. On 2026-09-29 the Administrator approved enabling it whenever the proof needs it.
2. OpenBao: off-node immutable destination plus an isolated restore target unsealed with the approved temporary copy of the static seal key (see above).
3. Memories: either a qualified CSI snapshot API/class and restore `StorageClass`, or the deployment-owned, read-only maintenance-pod copy procedure allowed by the module runbook. The current cluster cannot execute the runbook's CSI path.
4. Signing: SSH signatures with the trust root described above for the three proof records and their aggregate gate record.

## Ordered tasks

- [x] Record the sanitized initial inventory without Secret data.
- [x] Create an access-controlled evidence directory `evidence/epic-4/4-0/<recovery-id>/` outside Git for full operational records; commit only sanitized summaries and cryptographic digests.
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
- [ ] After evidence capture, remove every isolated restore target through its approved cleanup procedure. The Administrator signs `restore-target-cleanup.json` after proving namespace/workload/RBAC/network resources absent and checking PVCs, PVs, VolumeSnapshots/contents and provider volumes/snapshots for no unapproved residual storage.
- [ ] Assemble and sign `backup-gate.json` containing the final evidence-manifest digest, the three proof digests/signatures, recovery-point IDs, data cutoffs/RPO results, off-node object/storage-property validation, isolated-target validation, cleanup proof, verification timestamps, policy digest and expiry time.
- [ ] Validate with the read-only validator credential rather than the writer, without relying on values the capture scripts recorded. Check the signatures, source-incarnation bindings, and every manifest entry and storage property against provider/API evidence plus read-back checksums. The Administrator signs `validation.json`, and Story 4.0 is marked `done` only if all three proofs pass.

### Final-run preparation (Administrator decision, 2026-10-01)

On 2026-10-01 the Administrator decided to prepare the final run now and to run it only when the Story 4.1 window is scheduled. This preparation takes no captures, causes no outage, mutates nothing in the cluster or bucket, and signs nothing. The rehearsal bundle `~/hexalith-recovery-evidence/4-0/20260929t124806z/` stays byte-for-byte unchanged.

- [x] Stage the final-run tool set in `~/hexalith-recovery-evidence/4-0/final-run-tools/` (mode `0700`, files `0600`, scripts `0700`, outside Git), copied from the rehearsal `tools/`. Leave out `RID`, so a tool run from the staging directory fails instead of writing into any bundle. `final-run-order.sh new` run from there must create the fresh bundle, copy every tool into it and seed `signatures/allowed_signers` with only the Administrator key above.
- [x] Apply the single-signer decision. In `policy.py`, the accountable operator, cleanup owner, cleanup signer and validator are all the Administrator. Mark the 2026-09-29 `pduong@itaneo.com` decision superseded by the 2026-10-01 decision and state that validator-credential custody stays with the Administrator. In `proofs.py`, set `validator` to the Administrator. In the final run, fail instead of falling back when `cleanup/restore-target-cleanup.json` is absent. In `sign.sh`, `final-run-order.sh` and every read-back identity string, remove the second-signer flow and `independent-validation.json`.
- [x] Add a `cleanup` output step that writes unsigned `cleanup/restore-target-cleanup.json` from the `cleanup_check.py` result. It records the absence checks for every namespace, workload, RBAC, network, PVC, PV, VolumeSnapshot and provider object, plus the deletion and absence of the temporary OpenBao seal-key copy.
- [x] Add `gate.py`. It writes `gate/backup-gate.json` with every field listed in the `backup-gate.json` task above. It refuses to write when any input record lacks a valid signature against `signatures/allowed_signers`, its `verificationResult` is not `pass`, or it is older than the policy's maximum proof age.
- [x] Add `validate.py`. It loads only `~/.config/hexalith-recovery/validator.env` and never the writer credential, and it writes unsigned `validation/validation.json`. It re-reads every manifest object and every uploaded signed record, recomputes SHA-256 and byte length, and checks the bucket and object versioning, `COMPLIANCE` lock and retain-until date against policy, plus encryption. It verifies every signature. It re-reads source UIDs and system incarnations read-only from the live API and compares them with the proofs. It also re-checks the RPO and proof age at validation time. Any failure or skipped check makes the result `fail`. If a check would need another credential or a mutating call, record it in the output as a manual final-run step; do not substitute a weaker check.
- [x] Wire `final-run-order.sh` steps `cleanup`, `records`, `gate` and `validate` into the signing order: Administrator signs cleanup → proofs and manifest → gate → `validation.json`, uploading each with `upload_signed.py`.
- [x] Commit a sanitized `evidence/epic-4/4-0-final-run-tools.md` listing each staged file's SHA-256 and the changes made, and update the open items below.

## Evidence contract

Each sanitized proof summary must contain `schemaVersion`, `system`, `sourceIdentity`, `sourceUid`, `sourceIncarnation`, `sourceIndex`, `dataCutoff`, `measuredRpo`, `recoveryPointId`, `capturedAt`, `offNodeObjectIdentity`, `artifactSha256`, `storageProperties`, `isolatedTargetIdentity`, `isolationChecks`, `restoredAt`, `verificationChecks`, `verificationResult`, `cleanupProofSha256`, `operator`, `validator`, `policySha256`, `evidenceManifestSha256`, `expiresAt` and `signatureIdentity`. The signed full record and detached signature remain in the approved access-controlled evidence store; the committed summary contains no credentials, tokens, keys, database rows, tenant payloads or Secret values.

Story 4.1 must validate `backup-gate.json` and the three signed proofs directly. A checklist, job status, screenshot or unsigned prose summary is not a substitute.

## Hard gates and stop conditions

- Stop before capture if the destination is on `node1`, mutable by the source workload, unencrypted, or cannot be independently read back and checksummed.
- Stop if encryption, immutability/object lock, retention, failure-domain separation, data-cutoff/RPO compliance, source-incarnation binding or validation evidence is absent or contradicted.
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
**Then** the Administrator's signature on `validation.json`, made after a read-only re-check with the validator credential, covers the final evidence manifest, every object checksum/storage property, source-incarnation and data-cutoff/RPO binding, isolation tests, successful Memories resume/reconciliation and signed residual-storage cleanup
**And** any missing or failed validation blocks the upgrade

**Given** the staged final-run tools
**When** `grep -rn 'pduong\|independent-validation'` runs over them
**Then** the only matches are the record that the 2026-09-29 decision is superseded and the optional read-only access note
**And** `python3 -m py_compile` passes for every Python file and `bash -n` passes for every shell script

**Given** the unsigned rehearsal bundle
**When** `gate.py` and `validate.py` run against it in a dry-run mode that writes output only under the session scratch directory
**Then** `gate.py` refuses because the proofs are unsigned
**And** `validate.py` re-reads every manifest object with the validator credential, reports matching checksums and storage properties, and returns `fail` only for missing signatures, expired proof age or RPO staleness
**And** the SHA-256 of every file in the rehearsal bundle is identical before and after

## Implementation status (2026-09-29, rehearsal `20260929t124806z`)

The sanitized summary is [4-0-recovery-run-20260929t124806z.md](evidence/epic-4/4-0-recovery-run-20260929t124806z.md). The full records are in `~/hexalith-recovery-evidence/4-0/20260929t124806z/`.

The rehearsal covered all three systems. Each got an off-node, COMPLIANCE-locked, AES256 recovery point in `fr-par`, read back by the validator key. Each was restored into a new isolated namespace on new volumes and verified, and every target was then removed; the cleanup check covers all four namespaces.

- **Keycloak and OpenBao:** all checks passed.
- **Memories:**
  - It followed the approved playbook. Intake was paused from 14:08:40 to 14:15:03, and both stores were stopped from 14:10:00 to 14:11:21.
  - Paired copies were taken and restored (D3). They match the pre-copy census on every durable field.
  - The drain re-check was clean, intake resumed, and `/ready` reports `Healthy`.
  - There are zero tenants, so no verifier pass is claimed (D1).
- **Finding:** the Kubernetes API Service VIP is reachable at TCP level from default-deny namespaces (kube-proxy IPVS), while every resource request is refused with 403. It is recorded for the final run.

Nothing from this run is signed, per the Administrator's decision. The kept procedure for the final run is `tools/final-run-order.sh` in the bundle.

## Final-run preparation status (2026-10-01)

The final-run tools are staged in `~/hexalith-recovery-evidence/4-0/final-run-tools/`, with no `RID` and with a `SHA256SUMS` that `new` enforces. They were revised after the review below. The sanitized file list, digests, changes, self-test and dry-run results are in [4-0-final-run-tools.md](evidence/epic-4/4-0-final-run-tools.md).

- **Single-signer changes:** `policy.py`, `proofs.py`, `sign.sh`, `final-run-order.sh` and the read-back identity strings now name the Administrator as the only signer and as validator.
- **New tools:** `cleanup_record.py`, `gate.py`, `validate.py`, `s40verify.py` and `selftest.sh`.
- **Signing order:** `final-run-order.sh` enforces it: the policy before any capture, then cleanup, then the manifest and proofs, then the gate, then `validation.json`. Each record is uploaded through `sign.sh`.
- **Re-runs:** signed records cannot be overwritten by a re-run.
- **Pre-capture gate:** `precheck.py` stops the run when its gate fails, including a bucket-policy digest that differs from the pinned revision.
- **Self-test:** `selftest.sh` passes all 32 assertions. It runs the sign, upload, records, gate and validate chain against a local moto S3 server, with a throwaway key and a stubbed kubectl, including a tampered-upload failure.
- **Dry runs on the unsigned rehearsal bundle:**
  - `gate.py` refused.
  - `validate.py` re-read all 39 manifest objects with the validator key, and checksums and storage properties matched. It failed on:
    - missing signatures (14);
    - the superseded validator named in the rehearsal proofs (`identity`, 3);
    - proof age (3) and RPO staleness (3);
    - the rehearsal policy's pre-2026-09-30 bucket-policy digest (`storage`, 1).

    Checks blocked by the absent signed records were skipped under their own categories. The `identity` and `storage` failures come from review fixes G9 and G3. They go beyond the dry-run acceptance criterion, which was written before the review, and both are true of the rehearsal records.
  - All 76 rehearsal files kept their SHA-256.
- **Not done in preparation:** nothing was captured, mutated or signed.

**Open, so this story stays `in-progress`:**

- The final signed run shortly before the Story 4.1 upgrade. Start it no earlier than about 20 h before the planned kubeadm hop, with `./final-run-order.sh new` from the staging directory, and follow the order in [4-0-final-run-tools.md](evidence/epic-4/4-0-final-run-tools.md#for-the-final-run). Before signing `validation.json`, perform the two manual steps it lists:
  - **M1:** the Memories `run_id`.
  - **M2:** every IAM policy of the writer, plus its bucket-policy statement.
- The signed `restore-target-cleanup.json`, `backup-gate.json` and `validation.json`.

## Review Triage Log

Review of the final-run preparation on 2026-10-01 used three layers: Blind Hunter (B), Edge Case Hunter (E) and Verification Gap (V, pre-verified). Groups (G) share one root cause.

| ID | Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- | --- |
| B1 | Frontmatter `in-review` disagrees with body/sprint-status `in-progress` | reject | — | `in-review` is the workflow's transient review state; the final status is set when the build closes, and the fix would edit this spec. |
| B2a | `gate.py` hard-codes `pass` without enforcing its computed RPO/off-node/isolation flags | high | patch G1 | `gate.py` computes `withinPolicy`, `allInManifest`, `allSse`, `allCompliance` and `allIsolationChecksTrue` but never refuses on them, and the proofs' `verificationResult` omits `measuredRpo.withinPolicy`; Story 4.1 validates the gate directly. |
| B2b | Gate `expiresAt` ignores the policy/manifest/cleanup ages it enforces | low | patch G2 | `gate.py` age-checks `preparedAt`/`generatedAt`/`checkedAt`, but `expiresAt` is only the earliest proof expiry. |
| B3 | Story 4.1 never consumes `validation.json` | medium | defer | Story 4.1's gate and the evidence contract check only `backup-gate.json` and the proofs; a failed validation blocks 4.1 only through the sprint status. The fix belongs to Story 4.1. |
| B4 | Dry-run result relies on blocked checks filed under `signature` | low | patch G9 | `validate.py` files skipped cleanup/gate/upload checks and the validator-identity check under category `signature`. |
| B5 | Bucket-policy digest recorded but never enforced; M2 covers one IAM policy | medium | patch G3 | `precheck.py` gate and `validate.py` never compare the observed digest with `2beba1ea…`; M2 names only policy `b64476cd…`. |
| B6 | API isolation relaxed with no automated replacement | medium | patch G5 | `iso_mem.py` drops `kube-api` from `allSourceProbesBlocked`; no tool checks anonymous API resource access, which Memories step 4 requires. |
| B7 | Memories tasks/stop conditions contradict D1/D3 | reject | — | The fix would edit this spec; D1/D3 are recorded as Administrator decisions in it. |
| B8 | Approved playbook still says DRAFT | low | patch G17b | Front matter says "not approved"; the file cannot change without breaking hash `61a7766e…`. |
| B9 | Superseded second-validator text left in evidence docs | low | patch G17a | `recovery-destination-setup.md:46` and the rehearsal summary's line 14 still require independent validation. The `epics.md` "independently proven" sub-claim is rejected: the spec records the Administrator's reading. |
| B10 | `sign.sh` partial failure dead-ends | medium | patch G6 | Upload runs after all signatures; a mid-batch failure leaves `.sig` files that a re-run refuses. |
| B11 | Manual-step results not captured in a signed record | low | reject | `validation.json` records each manual step's procedure and expected value, and the signature attests them; only an audit nicety is lost and the fix needs a new input path. |
| B12 | Upload chain never ran; doc's scratch-clone claim is inconsistent | medium | patch G10 | No `uploads.json` exists anywhere; `gate.py` step 5 refuses without uploads, yet the doc says the clone produced a complete gate without uploads. |
| B13 | Captures can run under an unsigned policy | medium | patch G21 | `final-run-order.sh` lets the policy be signed "with the proofs", while `policy.py` says nothing is approved until it verifies. |
| B14 | Manifest "covers validator objects" vs signing order | reject | — | Wording in this spec's pre-existing task; the fix edits the spec. |
| B15a | Residual-risk list incomplete | low | patch G18 | Single signer, co-located writer/validator keys and the validator account's `OrganizationManager` with MFA off are not listed. |
| B15b | CNPG uses the general writer key in the source namespace | medium | defer | Secret `keycloak/recovery-writer-s3` lets the source workload add versions under any prefix, including `evidence/`; pre-existing rehearsal setup. |
| B16 | "Does not rely on capture-recorded values" over-claims | medium | patch G8 | RPO timestamps, isolation, restore checks and Memories resume are taken from the signed proofs. |
| B17 | "RPO at validation time" undefined | false | — | `validate.py` `rpo.*.measuredWithinPolicy` re-proves at validation time that each cutoff meets the 30 m RPO; staleness is an extra check. |
| B18 | `cleanup_check.py` can pass when it should fail | low | patch G14 | By-name namespace check treats any error as absent; recorded-UID checks are vacuous when files are missing; PV deletion can race. |
| B19 | Readiness renumbering note incomplete | low | defer | Planning-doc maintenance from an earlier commit, outside this story's goal. |
| B20a | Spec context list omits playbook and summaries | reject | — | The fix edits this spec. |
| B20b | Staged tools not checked against committed digests | medium | patch G11 | Same as V4. |
| B20c | Manifest/validate read every bucket version | medium | patch G13 | Same root cause as E6/E7. |
| B20d | No time budget or latest start | low | patch G20 | The run must finish inside the 24 h expiry before the hop; nothing states it. |
| E1 | `sign.sh` mid-batch failure | medium | patch G6 | See B10. |
| E2 | `write_json` overwrites signed records on re-run | medium | patch G7 | `s40lib.write_json` has no `.sig` guard; re-running `policy`/`records` invalidates signatures and deadlocks `sign.sh`. |
| E3 | Cleanup re-run rewrites `cleanup-check.json` after signing | medium | patch G7 | The signed record's `cleanupCheckSha256` then no longer matches, and nothing checks it. |
| E4 | `precheck.py` exits 0 on failed gate | medium | patch G4 | `preCaptureGate` is written but never enforced; `policy.py` does not read it. |
| E5 | Bucket-policy digest not enforced | medium | patch G3 | See B5. |
| E6 | `retainUntilPassed` fails after 2026-10-29 | medium | patch G13 | The manifest holds the canary, rehearsal and old WAL versions, whose locks end from 2026-10-29. |
| E7 | Lifecycle delete markers fail checks from 2026-10-30 | medium | patch G13 | 31-day expiry in a versioned bucket adds markers; `noDeleteMarkers`, `preCaptureGate` and `allObjectsInRetentionSet` all require zero. |
| E8 | Gate hard-coded `pass` | high | patch G1 | See B2a. |
| E9 | Gate `expiresAt` | low | patch G2 | See B2b. |
| E10 | `proofs.py` uses the minimum max-age across systems | low | patch G15 | `validate.py` expects each system's own value; a direct correction. |
| E11 | Cleanup races OpenEBS PV deletion | low | patch G14 | Namespace deletion completes before the provisioner removes released PVs. |
| E12 | Retain-policy target PV leaves data | low | reject | Target PVs use `openebs-hostpath` with reclaim `Delete` (recorded in the cleanup residuals); unlikely, and the guard adds complexity. |
| E13 | No API resource-denial check | medium | patch G5 | See B6. |
| E14 | Status inconsistency | reject | — | See B1. |
| E15 | `recovery-destination-setup.md` open gap superseded | low | patch G17a | See B9. |
| E16 | Validation over-claim | medium | patch G8 | See B16. |
| E17 | Cleanup provider-object absence is inferred | low | patch G16 | `providerObjects` passes on PV deletion and bucket prefixes; the basis is not stated in the check. |
| E18 | Category masking | low | patch G9 | See B4. |
| V1 | Upload chain never exercised | medium | patch G10 | Pre-verified; see B12. |
| V2 | Bucket-policy digest never compared | medium | patch G3 | Pre-verified; see B5. |
| V3 | API-access-denied evidence missing | medium | patch G5 | Pre-verified; see B6. |
| V4 | `new` not tied to verified digests | medium | patch G11 | Pre-verified; `new` compares copies only with the staging directory. |
| V5 | Gate says `pass` on RPO breach | high | patch G1 | See B2a. |
| V6 | `processIncarnation` has zero clock tolerance | medium | patch G12 | API-server `creationTimestamp` vs workstation `capturedAt`; the dry run passed with 0 s margin. |
| V7 | `preCaptureGate` not enforced | medium | patch G4 | See E4. |
| V8 | No retry after failed upload | medium | patch G6 | See B10. |
| V9 | Status inconsistency | reject | — | See B1. |
