---
captured_at: '2026-11-23T17:30:09Z'
classification: 'sanitized provider setup and live read-back'
---

# Story 4.7 staging store on Scaleway

Scaleway Object Storage hosts the staging store in `fr-par`, outside the cluster and executor hosts. The Administrator's organization-owner API key was read from the approved mode-`0600` file to create the dedicated project, bucket and access policies.

| Resource | Read-back |
| --- | --- |
| Project | `hexalith-attempt-store` (`f8600260-9be3-4a04-a52d-a328473a5458`) |
| Bucket | `hexalith-staging-attempt-store` at `https://s3.fr-par.scw.cloud`, versioning `Enabled`, default object lock `COMPLIANCE` for 30 days, encryption `AES256` |
| Staging writer | Application `e12a245d-56d3-4588-9b87-c9418ee9cf93`, IAM policy `942f07da-d52d-4615-9af6-24a315ffec93`. Grants project-scoped `ObjectStorageBucketsRead`, `ObjectStorageObjectsRead` and `ObjectStorageObjectsWrite`. |
| Staging reader | Application `73572ce2-5911-4e67-b2be-0b2673b296d1`, IAM policy `4bc9a96e-ca90-4ffa-9327-794f8eea3fe3`. Grants project-scoped `ObjectStorageReadOnly`. |
| Bucket policy | `hexalith-staging-attempt-store-story-4-7`, SHA-256 of canonical JSON from provider read-back `7a2f5085cb0c5e3c7806a55002b6187002ee47e9a3b8638d25f385d651885cf8` |

The bucket policy allows the writer to read staging objects, list with a staging prefix, create the head and records with `If-None-Match: *`, and replace the head with `If-Match`. The reader can also read versions, retention and bucket configuration. The organization owner manages bucket configuration and can read objects for audit. Application credentials are in `~/.config/hexalith-attempt-store/{staging-writer,reader}.env`, each mode `0600`.

The policy read-back includes the operational and fixture paths in the same allow statements. Resource paths below are relative to the bucket.

| Statement | Resource paths | Required header |
| --- | --- | --- |
| `StagingCreateOnly` | `environments/staging/head.json`, `environments/staging/records/*`, `environments/staging/verification/*/head.json`, `environments/staging/verification/*/records/*` | `If-None-Match: *` |
| `StagingCompareAndSetHead` | `environments/staging/head.json`, `environments/staging/verification/*/head.json` | `If-Match` |

Both applications received `404 NoSuchKey` on a read of the uninitialized canonical staging head. The signed acquisition proof uses the fixture path so the first operational acquisition can initialize the canonical head.

## Provider probes

The probes used disposable objects under `environments/staging/verification/provider-6ab3aed33f4140a7bdc8069cc62292da/` and fresh connections for each request.

| Probe | Result |
| --- | --- |
| Read a missing head with the staging writer | `404 NoSuchKey` |
| Create the head and a record with `If-None-Match: *`, then repeat each PUT | Both creates succeeded. Both repeats returned `412 PreconditionFailed`. |
| Race two head replacements using the same ETag | One succeeded (version `1795454988603405`). The other returned `412 PreconditionFailed`. The reader saw the winning body and two versions with zero delete markers. |
| Unconditional head and record PUTs, matching-ETag record overwrite | `403 AccessDenied` |
| Head PUT with wildcard `If-Match` | `405 NotImplemented`. Scaleway refused the wildcard value. |
| Writer add a delete marker or delete a specific record version | `403 AccessDenied` for both |
| Writer GET and conditional PUT under the production prefix, unscoped list, reader conditional PUT | `403 AccessDenied` for each |
| Reader read the canary's retention | Version `1795454987405185` had `COMPLIANCE` retention until `2026-12-23T17:29:47.405Z`. |

## Signed .NET acquisition

`HostedWriterSerializesAcquisitionAndProviderEnforcesScope` passed against the same bucket and policy under `environments/staging/verification/4675433a17c541548d16cbb7eeea794d/`. Both writers reached the head compare-and-set with epoch 1. One succeeded, the other returned `lock-held`, and the reader verified the winning signed attempt at head revision 1 and epoch 1. The losing attempt had no entry in the head. The test also passed stale ETag, duplicate create, unconditional write, cross-prefix read and write, object and version delete, reader write, and record overwrite probes. The reviewed Release suite executed 24 cases, with zero failures and zero skips.
