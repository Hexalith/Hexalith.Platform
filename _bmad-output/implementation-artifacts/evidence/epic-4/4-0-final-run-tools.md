---
prepared_at: '2026-10-01T04:52:00Z'
classification: 'sanitized tool inventory and dry-run summary; the tools and full outputs stay outside Git'
acceptance_state: 'Final-run tools staged, revised after review, self-tested and dry-run tested. Nothing was captured, mutated or signed. Story 4.0 is not done, and the Story 4.1 gate stays closed.'
---

# Story 4.0 final-run tools

On 2026-10-01 the Administrator decided to prepare the final run now and to run it only when the Story 4.1 window is scheduled. This note lists the staged tool set and what changed from the rehearsal tools. It contains no credentials, keys, tokens or data values.

The preparation took no capture and caused no outage. It mutated nothing in the cluster or the bucket and signed nothing. Against the real systems it made only:

- read-only Kubernetes GETs;
- non-persisted SubjectAccessReviews, which are evaluated and not stored;
- read-only bucket reads with the validator key.

The rehearsal bundle `~/hexalith-recovery-evidence/4-0/20260929t124806z/` is unchanged: all 76 files had the same SHA-256 before and after the work.

## Staging

- **Location:** `~/hexalith-recovery-evidence/4-0/final-run-tools/` on the Administrator's workstation, outside Git. It holds 38 files.
- **Modes:** the directory is `0700`, files are `0600`, and the three shell scripts are `0700`.
- **`SHA256SUMS`** lists every other staged file. Its table is below.
- **No `RID` file.** Every tool that writes into a bundle refuses to run from the staging directory. This includes `s40lib.py` importers, `sign.sh`, `gate.py`, `validate.py` and every `final-run-order.sh` step except `new`.
- **`./final-run-order.sh new`** runs only where no `RID` exists. It then:
  1. runs `sha256sum --check --strict SHA256SUMS` and refuses on any mismatch, or on any file that is not listed (or listed and missing);
  2. checks that `allowed_signers.template` holds exactly one key, the Administrator's (`jpiquot@itaneo.com`, `SHA256:8XlNQvE3ucPf/e509wU4qtNgiyWA+TKmLei7F7+TCvk`, `namespaces="hexalith-recovery"`);
  3. creates `~/hexalith-recovery-evidence/4-0/<recovery-id>/` with mode `0700`;
  4. copies every tool file and `SHA256SUMS`, then checks the copies against `SHA256SUMS`;
  5. writes `tools/RID`;
  6. seeds `signatures/allowed_signers` from the template.

  This was tested with a scratch `HOME`. The fresh bundle had 38 tool files plus `RID`, and the copies verified against `SHA256SUMS`.

## Staged files

The rows below are generated from `SHA256SUMS`, after all review fixes.

| File | Status | SHA-256 |
| --- | --- | --- |
| `allowed_signers.template` | new | `77df53775f890a393578cb5480512b532ef730bcc854a2bf1d4cdd9bb1d77247` |
| `bao_caps.py` | unchanged | `8d8888ad19f1a58c3874ba671a017c04d027623da15806d31b4e1cb141ba5fa8` |
| `bao_capture.py` | unchanged | `87590e86630fe87588bde2e17e7f64078c26b73280f2ceb4ded576138cfa96cf` |
| `bao_inventory.py` | unchanged | `d427367dbe5ab76e3545de37dac4701c3a70e0599f1b4cb05b7773c094e66d67` |
| `bao_restore.py` | unchanged | `e9472515ac0817c6c7067d746e42c73f59decbd3ddf5ab8996ceaf3d97ef835f` |
| `bao_target.py` | unchanged | `21e24b9f35258254d38dffc371556d00980861ce74ef770bd1982d2bf69ea7cb` |
| `baolib.py` | unchanged | `6ee1613f74d8652c1553a51c0a382fa5e6372fd75598b68c79c04df6efb6ef3e` |
| `barman-cloud-v0.15.0-pinned.yaml` | unchanged | `484a27e278f5f39d8ab922fe2b0fa7f5dfabf96e60e7587a8c34599322283423` |
| `cleanup_check.py` | changed | `36179c9f50267a8e0c76b39c10d50100bd6b5383114bdb07ac9c66789293bec8` |
| `cleanup_record.py` | new | `0168faadf97229a003a69a1d86fe5c4542c2022d7d57d07b9a469bb09387c28a` |
| `final-run-order.sh` | changed | `660f660bdd09d5b51217c5ec3ed79a960c0824424395226308a88007581c778c` |
| `gate.py` | new | `b8adfe86198e3dcb7eeea5a9e7dcb46093e5d20ea0b238db2bc3e688f3028113` |
| `iso.py` | changed | `4dbd30581ddbe0141258007fdd62f87ec7e04ac471aa274c8599d4880ae17340` |
| `iso_mem.py` | changed | `6f9f9b558733f8ad36e898ab08df110ac1c2143726ad37139b2a9697aee6e4d2` |
| `k8s40.py` | unchanged | `92f17f55879bdfaa13a289ed0352cede361a142de738c7f4c262f5747311797e` |
| `kc_app.py` | unchanged | `1de3fc94f60c9cce974d8a309b08ef75bf0ab6b604e2943f572710d6c6bde43e` |
| `kc_capture.py` | unchanged | `6e029770a50b4048c86ee660a716486ebf87ff491c2ee38ae9eff4d41b7d4167` |
| `kc_inventory.sql` | unchanged | `4d3f057d66220552ed0b0047cd196622e1e784764e9444a1556f214b843a9bf6` |
| `kc_live_enable.py` | unchanged | `3c37fce6e1b429f0a05a00883dec99a2751919deeb3cadd8c4a2b568a5bc5aa7` |
| `kc_oidc.py` | unchanged | `3bd662e7badaba7f9a8c0dec8cc37cd1d7a087b62dc587dae88491ac1ca90376` |
| `kc_restore.py` | unchanged | `daaae4aa916982e3b025b5e1f63e3b234deca0e5401cca2362f623fa71d49bc8` |
| `kc_verifier_bootstrap.py` | unchanged | `918e01eefe368f81d55230b33689167d10fc41b27fcdd5b24a0126d44db4d3e4` |
| `kc_verify_db.py` | unchanged | `55ed018a46f87b9638b86a016ced5608f9c585e76e47c719eb989a94c97e0c75` |
| `manifest.py` | changed | `55c5e24f2662c258e9b5736d9fce0f133cb16a9df37d363adc9b1221599f8e99` |
| `mem.py` | changed | `30aa570758d2c74c8818dae5f0fe49e7df5b7e659be25fea3485e8c6024f6cae` |
| `openbao-source-ca.crt` | unchanged | `01231c6395314bcea87f77f4f0af07e1ced943688969cb4cc39b781c79da8e98` |
| `policy.py` | changed | `eb68fcf5ae4251e0fcceaf56c138027a3c46ce06d435a0fc3d82ff80b575fde6` |
| `precheck.py` | changed | `f949c9eea2e87ed3f66aa7d156ffc59652fd23107db747b9d4d72c45ad77d6fe` |
| `preflight.py` | unchanged | `634e426300350fabbcd4b2756650e98348cf704c7fc4876891fdb0157521dab3` |
| `proofs.py` | changed | `1d7ec524fe3250382371ee2e549aee9945224e77d695bfb5443c151316d38e6c` |
| `s40lib.py` | changed | `9b4ac17788eb61b23b9b6543b1dc31ab8dc37a524a4d2926fd2a60ec63356729` |
| `s40verify.py` | new | `1273b2e3936ab9e69318f35776e1badab90c4670089a3ebea5f975d6fe20e3a1` |
| `selftest.sh` | new | `c919b8f37627d380b5f1a371d531785074aa52eec2429406babf62aaf8e3e5ac` |
| `sign.sh` | changed | `e38f8cdf44b81ca64f72e5eb576a683c0a3a92db0c364b1d41c93b4d0684ea7d` |
| `target_identity.py` | unchanged | `985473d2d934e0771b5e3fe302172977a297f21eee0099e8e91111ac27fb7b58` |
| `upload_signed.py` | changed | `2013b2d610c8bf9f35e6dffd25a095925d7d093a4dc757582b4ee6943f33b794` |
| `validate.py` | new | `8324354d306cc0346e1b6c63452ddaca2defd8074699abeb6d86f4110ac35a5e` |
| `SHA256SUMS` | new | `29228eca2d14a73048caaa6eddf597ecac52cd9620c151bb4e824e6b2a082669` (not listed in itself) |

Unchanged files are byte-identical to the rehearsal `tools/` copies, whose digests are in the rehearsal manifest. `RID` is left out on purpose.

## Changes

### Single-signer decision (2026-10-01)

- **`policy.py`:**
  - The accountable operator, cleanup owner, cleanup signer and validator are all the Administrator. The old `independentValidator` field is replaced by `validator`.
  - The 2026-09-29 second-validator decision stays in `approvalSources`, marked superseded by the 2026-10-01 decision. The two 2026-10-01 decisions are added.
  - Validator-credential custody stays with the Administrator. The optional read-only bucket access of the former validator is recorded as a note that the gate does not require.
  - The Administrator is the only signer. The signing order puts the policy first, before any capture.
  - The cleanup signer is the Administrator.
  - The bucket-policy digest is the 2026-09-30 revision (`2beba1ea…`), with its digest method. The digest observed at pre-check is recorded beside it.
  - The `validation` rule names exactly what validation re-establishes and what it accepts from the signed proofs.
- **`proofs.py`:**
  - `validator` is the Administrator.
  - The tool writes final proofs only. It exits when `cleanup/restore-target-cleanup.json` is absent, with no fallback.
  - `expiresAt` uses each system's own maximum proof age.
  - A proof is `fail` when its `measuredRpo.withinPolicy` is false.
- **`sign.sh`:**
  - Signs only as the Administrator, after checking that the trust root holds only the Administrator key and that the signing key has the pinned fingerprint.
  - Refuses a record whose `verificationResult` is not `pass` unless `SIGN_ALLOW_FAIL=1` is set deliberately.
  - A record whose existing `.sig` verifies is not re-signed but is uploaded again. This recovers from an interrupted upload. An existing `.sig` that does not verify is refused.
  - Each record is verified and then uploaded with `upload_signed.py`.
- **Read-back identity strings:** `manifest.py` (`status`, `readBackIdentity`) and `precheck.py` (`checkedBy`) now describe the read-only validator key in the Administrator's custody.

`grep -rn 'pduong\|independent-validation'` over the staged tools matches only two lines in `policy.py`: the superseded record and the optional read-only access note.

### Signing order

`final-run-order.sh` enforces this order:

1. **Policy:** the Administrator signs and uploads `policy/recovery-policy.json`. `watch-on`, `keycloak`, `openbao` and `memories` each refuse until the policy verifies and is uploaded.
2. **Cleanup:** `restore-target-cleanup.json`.
3. **Records:** the manifest and the three proofs.
4. **Gate:** `backup-gate.json`.
5. **Validation:** `validation.json`.

`records`, `gate` and `validate` each refuse until the earlier records are signed and uploaded. `s40lib.write_json` refuses to overwrite any record whose `.sig` exists. `cleanup_check.py` refuses to run once the cleanup record is signed, because that record carries the digest of `cleanup-check.json`. A re-run therefore cannot silently invalidate a signature.

### New steps and tools

- **`cleanup_check.py`** records absence checks by category:
  - namespaces, by name and by the UID recorded before cleanup. A namespace counts as absent only on `NotFound`; any other error raises. Each of Keycloak, OpenBao and Memories must have a recorded UID.
  - workloads;
  - RBAC;
  - network objects;
  - ConfigMaps, Secrets and cert-manager objects. Each system must have recorded target Secrets.
  - PVCs and PVs. Before checking, it waits up to 300 s for the OpenEBS provisioner to delete PVs that are still claimed from a target namespace.
  - VolumeSnapshots;
  - the bucket's retention set;
  - the temporary OpenBao seal-key copy.

  Rehearsal-only inputs are optional, and label queries fail loudly. Delete markers follow the lifecycle rule below.
- **`cleanup_record.py`** writes the unsigned `restore-target-cleanup.json`. The `providerObjects` entry states its basis: "inferred from PV deletion by the OpenEBS provisioner; host directories not listed".
- **`gate.py`** writes `gate/backup-gate.json` with every field the story lists. It refuses to write when any of these holds:
  - an input record has no valid Administrator signature;
  - a `verificationResult` is not exactly `pass`;
  - an input record is older than the maximum proof age;
  - a digest or identity binding disagrees;
  - a signed record or its signature was not uploaded and read back;
  - any of its own computed flags is false: RPO within policy, off-node `allInManifest`/`allSse`/`allCompliance`, or `allIsolationChecksTrue`.

  `expiresAt` is the earliest age-checked timestamp (policy `preparedAt`, manifest `generatedAt`, cleanup `checkedAt`, each proof's `capturedAt`) plus its maximum proof age. It is never later than any proof's `expiresAt`, and the gate records which timestamp set it.
- **`validate.py`** loads only `~/.config/hexalith-recovery/validator.env`. It makes only read-only Kubernetes GETs and writes the unsigned `validation/validation.json`.
  - **Re-established without capture-script values:**
    - **Objects:** every manifest object and every uploaded signed record is re-read and re-hashed, and uploaded signatures are verified on the downloaded bytes.
    - **Storage properties:**
      - bucket region, versioning, `COMPLIANCE` default retention, encryption and lifecycle;
      - the bucket-policy digest (`storage.bucket.policyDigest`), checked against the policy's pinned value with the same digest method;
      - delete markers;
      - per object: version ID, `COMPLIANCE` lock (head and retention API), retain-until at least the policy's 30 days, and AES256.
    - **Signatures**, against the single-key trust root.
    - **Live source identities:**
      - Keycloak: UID, system identifier and timeline.
      - OpenBao: UID; `cluster_id`/`cluster_name` through the API service proxy; unsealed; applied index at or above the snapshot index.
      - Memories: StatefulSet and PVC UIDs. The store pods must have been created inside the approved restart window, with ±120 s clock-skew tolerance on both bounds (recorded in the check), and no container restart since.
  - **Re-checked from the signed proofs' timestamps:** RPO and proof age.
  - **Taken as signed, not re-verified:** `validation.json` lists these under `notReverified`:
    - capture timestamps and source indexes;
    - isolation evidence;
    - restore verification;
    - Memories quiescence, resume and reconciliation;
    - the cleanup record's checks, apart from the live absence re-check;
    - the manual steps.
  - **Retain-until and delete markers:**
    - `retainUntilPassed` applies only to versions referenced by this run's proofs or stored under `evidence/4-0/<recovery-id>/`.
    - A delete marker is tolerated only under `keycloak/`, `openbao/` or `memories/`, and only when it is at least that rule's `expirationDays` after the newest version of its key. Tolerated markers are reported; any other marker fails.

    The same rule applies in `precheck.py` and `cleanup_check.py`.
  - **Multipart allowance:** a multipart object takes its default retention from the start of its upload, while `LastModified` records the end. Up to one hour between the two is therefore allowed for multipart objects only. The rehearsal's Keycloak preflight `data.tar.gz` is 6 s short of 30 days for this reason.
  - **Categories:** checks skipped because a record is absent keep their own category (`binding`, `cleanup`, `gate-binding`, `storage`). The validator-identity check is `identity`.
  - **Manual steps:**
    - **M1:** the Memories `run_id`.
    - **M2:** every IAM policy attached to `hexalith-recovery-writer`, plus the writer's statement in the bucket policy.

    The Administrator's signature on `validation.json` attests both.
- **`s40verify.py`** holds read-only helpers shared by `gate.py`, `validate.py`, `precheck.py` and `cleanup_check.py`: the gate expiry, the bucket-policy digest and the delete-marker rule. It loads no credentials and has no `RID` side effect.
- **`selftest.sh`** is described under Verification.

### Fixes the final run would otherwise have hit

- **`precheck.py`:**
  - `preCaptureGate` now also requires that the bucket policy was read and that its digest equals the pinned `2beba1ea…`.
  - It applies the delete-marker rule.
  - It exits non-zero after writing the record when the gate fails, so the `policy` step stops before any capture.
  - It paginates the version listing.
- **`iso.py` and `iso_mem.py`:**
  - The counted check `kubernetesApiResourceAccessDenied` now requires a non-persisted SubjectAccessReview for `system:anonymous`/`system:unauthenticated` to be denied `get` and `list` on Secrets, Pods and ConfigMaps in `keycloak`, `openbao`, `hexalith-memories` and the target namespace. `kubectl auth can-i --as=system:anonymous` cannot be used, because anonymous may not create its own access review.
  - The TCP reachability of the API Service VIP stays a recorded finding, not a check.
- **`proofs.py`:**
  - The Memories `verificationResult` is now exactly `pass` or `fail`, and the finding moves to a note.
  - The tool no longer crashes without the hand-added probe block.
  - It records `runIdsAfterCopyRestart`, which step M1 compares.
- **`s40lib.py` and `manifest.py`:** one cached S3 client per role and eight parallel read-backs. In testing, 498 versions took 45 s.
- **`mem.py`:** the C3 pause attestation no longer says "rehearsal".
- **`upload_signed.py`:** refuses to upload a record whose signature does not verify.

## The approved Memories playbook

`memories-quiescence-resume-playbook.md` (SHA-256 `61a7766e4d21a411a02f12edf38cc662d1950b54036a7dc3c09ddc7623ca20d5`) is approved per the story's 2026-09-29 decisions D1–D5, even though its front matter still says DRAFT. The file is not edited, because its hash is bound in the policy and in the rehearsal.

Its own D3 section describes a logical import that needs `memories`, a Dapr sidecar and an embedding provider. That section is superseded by decision D3: the isolated restore covers only the paired physical copies.

## Verification

| Check | Result |
| --- | --- |
| `python3 -m py_compile` (cache redirected outside the staging directory) | 30 of 30 Python files pass |
| `bash -n` | `final-run-order.sh`, `sign.sh` and `selftest.sh` pass |
| `grep -rn 'pduong\|independent-validation'` | 2 matches, both in `policy.py`: the superseded record and the optional read-only access note |
| `selftest.sh` | 32 of 32 assertions pass. Details below. |
| `gate.py --dry-run` on the rehearsal bundle | Refused with 35 reasons, including the unsigned records. Nothing was written under the bundle. |
| `validate.py --dry-run` on the rehearsal bundle (validator key only) | `fail`. Details below. |
| `cleanup_check.py` on a scratch copy of the rehearsal's target records, live cluster, read-only | Every check is true. With the cleanup record signed, it refuses to run. |
| New anonymous-access check, live, read-only | 24 of 24 SubjectAccessReviews denied |
| Rehearsal bundle SHA-256, before and after | 76 of 76 files identical |

### `selftest.sh`

`selftest.sh` copies the staged tools into a scratch directory. It swaps three things in those copies only: the pinned fingerprint, the S3 endpoint and the pinned bucket-policy digest. In the `validate.py` copy it also points kubectl at the stub by absolute path, because `uv run` puts its interpreter's directory ahead of `PATH`.

It then runs everything against a local moto S3 server with versioning, `COMPLIANCE` object lock, AES256, lifecycle and a bucket policy. moto 5 does not implement `GetObjectRetention`, so a shim answers it from each object's lock fields. Signing uses a throwaway key under a scratch `HOME`, with `KUBECONFIG=/dev/null`.

- **Stubbed:**
  - the capture and restore steps, replaced by fixture records plus fixture objects uploaded to moto by the writer;
  - `cleanup_check.py`, replaced by a fixture `cleanup-check.json`;
  - `precheck.py`, replaced by a fixture read from moto;
  - every live Kubernetes answer, given by a kubectl stub that rejects any call it does not know.
- **Run for real:**
  - `new` with `SHA256SUMS`;
  - `policy.py`;
  - `sign.sh` → `upload_signed.py` → `uploads.json`;
  - `cleanup_record.py`;
  - the `records`, `gate` and `validate` steps.
- **Asserted:**
  - every proof, the gate and all 109 validation checks pass on the untampered bundle;
  - a tampered newer upload of the Keycloak proof fails exactly `signature.uploaded:keycloak/keycloak-restore-proof.json`, and `sign.sh` then refuses `validation.json`;
  - each refusal path: an unlisted staged file, captures under an unsigned policy, rewriting signed records, steps out of order, a signed but un-uploaded record (recovered by `sign.sh`), a non-verifying `.sig`, and a missing upload in the gate.

The test never touches the real bucket or the cluster.

The earlier scratch clone run on 2026-10-01 is replaced by this self-test. It fabricated `signatures/uploads.json` entries instead of uploading, which is why `gate.py`'s upload check passed there. Its `validate.py` run failed on the re-read of uploaded records, as expected.

### `validate.py` dry run on the rehearsal

The result is `fail`: 129 checks, 93 pass, 24 fail and 12 skipped. All 39 manifest objects were re-read and match their checksums and storage properties.

Failures:

- **`signature` (14):** seven local signatures and seven uploaded signed records are absent.
- **`identity` (3):** the rehearsal proofs name the superseded validator, who has no key in the trust root.
- **`proof-age` (3) and `rpo-staleness` (3):** the rehearsal is two days old.
- **`storage` (1):** `storage.bucket.policyDigest`. The rehearsal policy pins the pre-2026-09-30 digest `18342e51…`, while the bucket holds the approved revision `2beba1ea…`.

Skipped, because the signed cleanup and gate records and the uploads are absent: `storage` (7), `binding` (3), `cleanup` (1) and `gate-binding` (1).

The `identity` and `storage` failures are true properties of the rehearsal records, so the dry run now fails on more than missing signatures, expired proof age and RPO staleness.

## For the final run

1. **Policy.** From the staging directory: `./final-run-order.sh new`, then from the new bundle's `tools/`: `policy`, then `./sign.sh policy/recovery-policy.json`.
2. **Captures.** `watch-on`, `keycloak`, `openbao`, `memories`, then `mem.py resume` if every check passed.
3. **Cleanup.** `cleanup`, then `./sign.sh cleanup/restore-target-cleanup.json`.
4. **Records.** `records`, then sign the manifest and the three proofs.
5. **Gate.** `gate`, then `./sign.sh gate/backup-gate.json`.
6. **Validation.** `validate`, perform M1 and M2, then `./sign.sh validation/validation.json`.

**Latest-start rule:**

- The gate expires 24 h after its earliest age-checked timestamp, which in practice is the policy's `preparedAt` written at step 1.
- Budget about 3.5 h for the run. The rehearsal bundle's own records span 12:48Z–14:20Z on 2026-09-29; the final run adds signing, the manual steps and validation.
- Start no earlier than about 20 h before the planned kubeadm hop, and no later than about 4 h before it.

Residual items:

- **Single person.** The Administrator is accountable operator, cleanup owner and signer, validator and sole signer. The only separation is that validation re-reads with the read-only validator key instead of the writer.
- **Co-located keys.** The writer and validator keys sit side by side in `~/.config/hexalith-recovery/`, next to `admin.env`, on the same workstation.
- **Optional validator account.** `pduong@itaneo.com`'s Scaleway account is in group `Administrators` (`OrganizationManager`, `AllProductsFullAccess`), and MFA was off on 2026-09-30. Its read-only access to the bucket therefore rests on the bucket policy alone.
- **Kubernetes API VIP.** Its TCP reachability stays a recorded finding. Resource access by anonymous requests is now a counted check.
- **Keys.** Encryption keys are provider-managed. The former Velero personal key is not yet revoked.
- **Read-back time.** `manifest.py` and `validate.py` read back every version in the bucket, including the continuous WAL archive and daily base backups. Expect several minutes at a few thousand versions.
