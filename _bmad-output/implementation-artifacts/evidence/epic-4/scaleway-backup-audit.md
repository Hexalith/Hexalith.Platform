---
captured_at: '2026-09-28T14:38:40Z'
kubernetes_context: 'jpiquot@local'
classification: 'sanitized read-only provider discovery evidence'
acceptance_state: 'hard gate failed; not backup or restore proof'
---

# Story 4.0 Scaleway backup audit

This follow-up records the read-only investigation prompted by the Administrator's statement that existing backups use Scaleway. It contains no access keys, secret values, tokens, backup payloads or database rows. Kubernetes Secret inspection was limited to metadata, data-key names and the referenced credentials file; authenticated provider calls used the existing Velero credential without printing or retaining its values.

## Discovered destination

| Property | Sanitized observation | Story 4.0 assessment |
| --- | --- | --- |
| Velero location | `BackupStorageLocation/velero/scaleway-kube-green`, UID `ece66776-b06a-4889-8a8c-d54260f47825`, phase `Available` | Existing provider configuration, not proof for the protected systems. |
| Provider identity | S3-compatible provider `aws`; endpoint `https://s3.fr-par.scw.cloud`; bucket `hexalith-velero-backups`; region `fr-par` | Off `node1` and in a named provider region. |
| Provider owner | Canonical owner `473492ff-94c0-44dc-bc87-4f0bb3a463b7:473492ff-94c0-44dc-bc87-4f0bb3a463b7` | Records the discoverable provider account identity; no separate custodian or validator identity was found. |
| Credential reference | `Secret/velero/velero-credentials`, UID `647d3685-1804-4aef-bd26-4b0fb45ef7d7`, data key `cloud` | A reference exists. Its values were not printed or committed. The Secret is mounted by both the Velero server and node agent. |
| Writer access | Backup storage location access mode `ReadWrite`; the Velero server uses it as its default location | Fails the requirement that retained evidence cannot be mutated by the source backup workload. |
| Encryption | Bucket default server-side encryption is `AES256`; sampled object metadata also reported `AES256` | Encryption at rest is present, but key custody evidence and an approved custody identity are absent. |
| Immutability | Bucket versioning status is empty; `GetObjectLockConfiguration` returned `ObjectLockConfigurationNotFoundError`; sampled object had no version ID, lock mode or retain-until date | Hard-gate failure: no immutable/object-lock recovery point can be proved. |
| Retention | No bucket lifecycle configuration exists. The only active schedule has a seven-day TTL (`168h0m0s`), not the approved `30d` policy | Hard-gate failure: approved retention is not implemented or evidenced. |
| Access checks | No bucket policy exists; the provider does not implement `GetPublicAccessBlock`; anonymous bucket-list and sampled-object HEAD requests both returned `403` | Anonymous denial was observed, but it does not repair the missing immutability, retention or custody controls. |

The bucket ACL grants `FULL_CONTROL` to its canonical owner. This records ownership, not an independently validated least-privilege or deletion-denial policy.

## Current contents and recovery evidence

- Provider inventory returned 4,440 objects totaling 10,079,440,314 bytes under `backups/` and `kopia/`.
- No object key matched `keycloak`, `openbao` or `memories`.
- The only active Velero schedule is `forgejo-hourly`, covering namespace `forgejo`. All 170 retained Kubernetes `Backup` records belong to that schedule; the only current `BackupRepository` is `forgejo-scaleway-kube-green-kopia`.
- Historical completed repository-maintenance Jobs from 2026-08-20 name deleted Keycloak and OpenBao Kopia repositories. There is no current Keycloak or OpenBao `BackupRepository`, retained `Backup` record, matching provider object, or restore record. A maintenance Job is not a recovery point or restore proof and is stale beyond the approved 24-hour maximum proof age.
- The cluster contains zero Velero `Restore` records. No local `backup-gate.json`, protected-system restore proof, independent validation, cleanup proof, recovery policy or Story 4.0 evidence manifest was found outside unrelated qualification fixtures.

One small Forgejo maintenance object was read back through the existing Velero identity to test discoverability:

| Object identity | Bytes | Provider metadata | Read-back SHA-256 |
| --- | ---: | --- | --- |
| `kopia/forgejo/kopia.maintenance` | 92,793 | Last modified `2026-09-28T14:04:33Z`; ETag `ea6a51e0c025f29ed82cbf10ff27ba8a`; `AES256`; no provider SHA-256, version or object lock | `797273cbd3add9f171f711b0b3e47652ecbf594df0347594d8c49afce98ad171` |

This sampled read-back proves only that the source Velero credential can read one Forgejo object. There is no pre-recorded trusted SHA-256 for comparison, it was not performed by an independent validator, and it provides no evidence for Keycloak, OpenBao or Memories.

## Gate result

No backup, restore or workload mutation was authorized after this audit. The configured bucket fails Story 4.0's immutability, approved-retention, source-writer separation and independent-read-back gates, and it contains no current protected-system recovery point. It must not be used for a Story 4.0 capture unless those properties are changed and independently proved, or the Administrator approves a different qualifying Scaleway destination and credential boundary.
