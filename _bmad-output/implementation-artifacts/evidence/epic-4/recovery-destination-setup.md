---
captured_at: '2026-09-29T11:45:33Z'
classification: 'sanitized provider setup and read-back evidence'
acceptance_state: 'destination qualified for capture; not backup or restore proof'
---

# Story 4.0 recovery destination setup

The Administrator chose a new dedicated bucket on 2026-09-29 and asked for it to be set up on their behalf. All changes used the Administrator's own organization-owner API key. No key value was printed, logged or committed. The Velero key was used only for the denial probes below.

## Created resources

| Resource | Identity | Settings |
| --- | --- | --- |
| Scaleway project | `hexalith-recovery`, `bed84924-0e48-4275-8b7c-5d9a1f57a8dd` | Holds only this bucket, so project-scoped IAM rules cannot reach other buckets. |
| Bucket | `hexalith-recovery-points`, region `fr-par`, endpoint `https://s3.fr-par.scw.cloud` | Created with object lock enabled; versioning `Enabled`; default retention `COMPLIANCE` 30 days; default encryption `AES256`. |
| Lifecycle | Rules `expire-keycloak`, `expire-openbao`, `expire-memories` on prefixes `keycloak/`, `openbao/`, `memories/` | Current versions expire after 31 days, noncurrent versions 1 day later, incomplete multipart uploads after 1 day. `evidence/` has no expiry rule. |
| Writer application | `hexalith-recovery-writer`, `9a7cff8d-cffc-4554-93fe-01ed8e0d0d04` | IAM policy `b64476cd-5d66-4891-b33f-2375a432d028`: `ObjectStorageBucketsRead`, `ObjectStorageObjectsRead`, `ObjectStorageObjectsWrite` on project `hexalith-recovery` only. No delete permission. |
| Validator application | `hexalith-recovery-validator`, `406da2fa-5d02-4a49-bcbc-de0218c6460d` | IAM policy `99328167-0409-43f8-90c1-76876ab73960`: `ObjectStorageReadOnly` on project `hexalith-recovery` only. |
| Bucket policy | Id `hexalith-recovery-points-story-4-0`, SHA-256 `18342e51af2cdf3586f732f31e978f9f2a6a12f54add10a9b3016cb4c47858b5` | Allows only the organization owner user `31877e57-5937-4688-b018-a741852bf03f` (`s3:*`), the writer (list, location, get, put, multipart) and the validator (read-only bucket configuration, versions, objects, retention and legal hold). |

Application keys are stored on the Administrator's workstation in `~/.config/hexalith-recovery/writer.env` and `validator.env` (mode `0600`, directory `0700`). Neither key has an expiry date.

An empty first bucket created in project `Notariat AI` was deleted before any object was written, because Scaleway grants application access only when a project IAM permission and the bucket policy both allow it. Scoping those IAM rules to `Notariat AI` would have reached `hexalith-velero-backups` and every other bucket there.

## Read-back and access probes

All results below come from the validator key unless another principal is named.

| Check | Result |
| --- | --- |
| Versioning, object lock, encryption, lifecycle | Read back as configured above. |
| Writer puts `evidence/setup/lock-canary-2026-09-29.txt` | Version `1790682329554262`, 124 bytes, SHA-256 `a7353ccf49de280ce0ff6984aa3d9b84eb58eb18474eba3855e48ffc6b38f0d8`, written `2026-09-29T11:45:29Z`. |
| Canary retention and read-back | `COMPLIANCE` until `2026-10-29T11:45:29Z`; server-side encryption `AES256`; read-back SHA-256 matches. |
| Writer deletes the locked version / adds a delete marker | Both `AccessDenied`. |
| Organization owner deletes the locked version / shortens its retention | Both `AccessDenied`. |
| Validator writes an object | `AccessDenied`. |
| Writer / validator list `hexalith-velero-backups` | `AccessDenied` / `MethodNotAllowed`. |
| Velero key lists the new bucket / reads the canary | Both `AccessDenied`; the bucket is absent from the Velero project's bucket listing. |
| Anonymous list | `AccessDenied`. |
| Canary after probes | One version, still latest; no delete markers. |

## Open gaps

- Resolved 2026-09-29: Velero now runs on scoped application `hexalith-velero` (see [Velero key replacement](#velero-key-replacement)). The previous personal organization-admin key is no longer mounted in the cluster but has not been revoked in Scaleway; its owner should revoke it.
- The validator key is in the Administrator's custody. It must be handed to, or re-issued under, the independent second operator before independent validation.
- This setup was not performed or validated by an independent operator and proves only the destination. It is not a recovery point or restore proof.

## Velero key replacement

On 2026-09-29, with the Administrator's approval, Velero was moved off the personal organization-admin key.

| Step | Result |
| --- | --- |
| Pre-checks | `Secret/velero/velero-credentials` has no Helm, Flux or Argo ownership, and no tracked Hexalith repository recreates it. No other cluster Secret contained the previous key. No backup, pod-volume backup or maintenance job was running. |
| New principal | Application `hexalith-velero`, `4a02d719-515d-46a4-8328-c10894d338b8`; IAM policy `82df79cb-ccc9-4ef5-abd3-30816131dd53` grants `ObjectStorageBucketsRead`, `ObjectStorageObjectsRead`, `ObjectStorageObjectsWrite` and `ObjectStorageObjectsDelete` on project `Notariat AI` only. No expiry. |
| New key before swap | Listed `backups/`, read `kopia/forgejo/kopia.maintenance`, wrote and deleted probe `kopia/.hexalith-velero-key-probe-20260929`. Denied: listing `hexalith-recovery-points` (`AccessDenied`), creating a bucket (`AccessDenied`), IAM API (`403`). |
| Swap | Secret key `cloud` patched in place at about `2026-09-29T12:14Z` with the new key; the value was never printed. `Deployment/velero` and `DaemonSet/node-agent` restarted and became ready at `12:14:57Z`. |
| Post-swap proof | `BackupStorageLocation/scaleway-kube-green` `Available`, validated `2026-09-29T12:17:19Z`. On-demand `Backup/velero/forgejo-key-rotation-check-20260929`, built from the `forgejo-hourly` template, `Completed` `12:15:41Z` to `12:16:00Z` with 23/23 items; its pod-volume backup `Completed`. No authentication errors in Velero logs. |

The new key is stored only in the cluster Secret. Velero can no longer administer IAM or other Scaleway products, and cannot reach the Story 4.0 bucket.
