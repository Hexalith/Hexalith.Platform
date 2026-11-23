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
| Bucket policy | `hexalith-staging-attempt-store-story-4-7`, current SHA-256 of canonical JSON from provider read-back `1292c94dc11c47e8f48397fc39616a8e4c31ef125e9f7f3763a932fc9e3003b6`. The original policy, before `VerificationCanaryRead` was added, had SHA-256 `7a2f5085cb0c5e3c7806a55002b6187002ee47e9a3b8638d25f385d651885cf8`. |

The bucket policy allows the writer to read staging objects, list with a staging prefix, create the head and records with `If-None-Match: *`, and replace the head with `If-Match`. The reader can also read versions, retention and bucket configuration, and the single production read-denial canary. The organization owner manages bucket configuration and can read objects for audit. Application credentials are in `~/.config/hexalith-attempt-store/{staging-writer,reader}.env`, each mode `0600`.

The policy read-back includes the operational and fixture paths in the same allow statements. Resource paths below are relative to the bucket.

| Statement | Resource paths | Required header |
| --- | --- | --- |
| `StagingCreateOnly` | `environments/staging/head.json`, `environments/staging/records/*`, `environments/staging/verification/*/head.json`, `environments/staging/verification/*/records/*` | `If-None-Match: *` |
| `StagingCompareAndSetHead` | `environments/staging/head.json`, `environments/staging/verification/*/head.json` | `If-Match` |
| `VerificationCanaryRead` | `environments/production/verification/read-denial-canary.json` (reader `GetObject` only) | None |

Both applications initially received `404 NoSuchKey` on a read of the uninitialized canonical staging head. At 22:42 UTC on 2026-11-23, the staging writer called `InitializeAsync("staging")` to create `environments/staging/head.json`. The reader verified an empty head at revision 0 and epoch 0. The provider listed one head version, `1795473767610891`, with zero delete markers and `COMPLIANCE` protection until `2026-12-23T22:42:47.610Z`. Bucket configuration returned `NoSuchLifecycleConfiguration`.

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
| Writer conditional PUT under the production prefix, unscoped list, reader conditional PUT | `403 AccessDenied` for each |
| Reader read the canary's retention | Version `1795454987405185` had `COMPLIANCE` retention until `2026-12-23T17:29:47.405Z`. |

## Signed .NET acquisition

`HostedWriterSerializesAcquisitionAndProviderEnforcesScope` passed against the original bucket policy under `environments/staging/verification/4675433a17c541548d16cbb7eeea794d/`. Both writers reached the head compare-and-set with epoch 1. One succeeded, the other returned `lock-held`, and the reader verified the winning signed attempt at head revision 1 and epoch 1. The losing attempt had no entry in the head. The test also passed stale ETag, duplicate create, unconditional write, cross-prefix write, object and version delete, reader write, and record overwrite probes. The original Release suite executed 24 cases, with zero failures and zero skips.

## Review verification

At 22:41 UTC on 2026-11-23, the organization owner created `environments/production/verification/read-denial-canary.json` with `If-None-Match: *`. The version is `1795473700441642` and is under `COMPLIANCE` retention until `2026-12-23T22:41:40.441Z`. The owner removed the temporary create grant after checking the canary. The final policy preserves the original nine statements and adds `VerificationCanaryRead`, which grants the reader `GetObject` on that one key. The reader returned HTTP 200 and the expected fixture body. The writer returned HTTP 403 `AccessDenied` for that exact existing object.

The updated credentialed Release suite passed all 33 cases with zero failures or skips. It initialized the fixture head under `environments/staging/verification/e59e088eb1a74dce85483e09d0bb2bd7/` before the race. One signed acquisition committed and the other returned `lock-held`. The reader found two head versions, initialization `1795473758296090` and the winner `1795473759842117`, with zero delete markers. The suite also verified the unconditional PUT, delete, version delete, record overwrite, reader write and existing production canary read denials.

A Release build with `CI=true` against the signature- and digest-verified Builds 6.7.0 Tooling assembly passed all 32 deterministic cases selected by the tuple profile. The 79 Python tuple and Builds checks also passed.

## Final verification

The final credentialed Release build passed all 40 tests. The signed concurrency test used `environments/staging/verification/8f255dc149ea4b01b211ed9342da4bf8/`. One attempt committed and the other returned `lock-held`. After the denial probes, the test listed the exact fixture head key and confirmed two versions, zero delete markers, and the winning version as current.

The CI-mode Release build and all 39 AttemptStore cases selected by the current tuple profile passed against the published Builds 6.7.0 Tooling assembly after package signature, SHA-512, release revision, and assembly SHA-256 verification. Both builds had zero warnings and errors. All 89 Python checks across the Builds binding, catalog and tuple verification modules passed. The full tuple workflow still requires the Builds 6.7.0 pin and catalog adoption.
