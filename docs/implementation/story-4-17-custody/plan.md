# Story 4.17 — accepted one-off packet preservation

Owner decision: 2026-11-14, **“apply recommended”**, recorded in the
[approved spec](../../../_bmad-output/implementation-artifacts/spec-4-17-preserve-broker-qualification-packets-off-site.md).
This plan executes only the five admitted original files below. Preservation confers no broker acceptance.

Destination: existing Scaleway bucket `hexalith-recovery-points`, region `fr-par`,
explicit HTTPS endpoint `https://s3.fr-par.scw.cloud`, fresh prefix
`evidence/4-17/20261114t010116z-5574db84/`. The failure domain is off-site Scaleway
object storage, separate from this host and the expiring GitHub artifact copies.

Protection: recheck existing versioning Enabled, server-side AES256, default 30-day
COMPLIANCE object retention, private bucket ACL and original bucket-policy role statements.
The three existing lifecycle rules apply only to `keycloak/`, `openbao/`, `memories/`;
`evidence/` has no expiration rule. The Administrator owns a **no-deletion hold until the
guarantees close**, followed by deletion review against policy. Thirty-day retention is an
initial enforced minimum, not permission to delete after thirty days. No object legal hold,
bucket/IAM/lifecycle change, new allocation or provisioned service is authorized.

The accountable owner is the Administrator. `/root/preserve_packets_impl` executes for the
Administrator through `~/.config/hexalith-recovery/writer.env`, expected existing application
`hexalith-recovery-writer` / `9a7cff8d-cffc-4554-93fe-01ed8e0d0d04`.
The separate agent `/root/custody_verifier` performs validator-only exact-version retrieval
through `~/.config/hexalith-recovery/validator.env`, expected existing application
`hexalith-recovery-validator` / `406da2fa-5d02-4a49-bcbc-de0218c6460d`.
Both references remain Administrator-owned. The agents execute separately on this shared host;
this is automated verification, not independent human accountability or a security boundary.
Current bucket-policy grants will be observed; effective IAM grants and API-key-to-application
identity are historical/reference bindings, not newly enumerated IAM evidence. No denial probes
or additional mutation will be performed to infer those grants.

## Exact source commitments

Source directory: `artifacts/story-4-17-custody-staging-2026-11-14/`, owner-restricted.
The ZIP names are hard links to original cache files. Both names and all bytes remain intact.
The original manifest remains `LOCAL_STAGING_ONLY`, off-site custody false and destination/
operator/verifier null. Separate receipts record the actual later transfer; no original field changes.

| File | Bytes | SHA-256 | Entries / packet artifacts | Retained authority / own expiry UTC |
| --- | ---: | --- | --- | --- |
| `13638347236.zip` | 1515070 | `20a1719b753c7379207bbeb92cc73f70af99b7fa7e71734fd4a58ec7427cf223` | 495 / 494, schema 2 | historical failure; no new qualification verification / 2026-12-08T23:05:01Z |
| `13638293057.zip` | 1946018 | `20643dbd7d3702f3f504b0a4ce5fd3fe627ff32493d00624d37dca40e81b8a27` | 601 / 494, schema 2 | historical failure; no new qualification verification / 2026-12-08T23:08:16Z |
| `13995674927.zip` | 2666335 | `906c0d943e7a0ee8393bb83b96c4c917477fe18820afe37fe6ba7274afe7aeb7` | 575 / 551, schema 3 | diagnostic-only; original qualification integrity rejected / 2026-12-13T00:11:32Z |
| `14018214306.zip` | 1650434 | `a26b848840ca8061a00d8ea7598b52b6a91b5331822645c23bc527278e40949a` | 544 / 520, schema 3 | original independently verified packet; technical FAIL 20/24 / 2026-12-13T07:40:43Z |
| `manifest.json` | 574728 | `ea4b7666ae5b77b3f00b24b6ee32c35c98da6bbe5004ebc24fb5f9c1bfdd183f` | complete original inventory | unchanged original local preparation record |

Totals: 7,777,857 ZIP bytes, 2,215 entries and 2,059 packet artifact hashes.
The manifest adds 574,728 bytes; all five objects total 8,352,585 bytes.

## Execution and stop rules

1. Retain [transfer_once.py](transfer_once.py), [test_transfer_once.py](test_transfer_once.py),
   this plan and [offline-checks.json](offline-checks.json). Run the offline negative matrix.
   Independently inspect the exact script/plan before mutation and retain their SHA-256 digests
   in `inspection.json`. Any changed script or plan requires renewed inspection.
2. Parse the named credential files privately as data, never shell-source or print them. Explicit
   credentials, verified TLS, fixed endpoint/region, signature v4 and SDK total maximum attempts
   1 prevent ambient credential selection or automatic SDK retries. SDK is boto3, not the
   encrypted Story 4.0 publication helper; that helper and all its guards remain unchanged.
   Concrete SDK inputs are boto3 **1.44.3** and botocore **1.44.3**, enforced by the script.
   Run `uv run --quiet --no-project --with boto3==1.44.3 --with botocore==1.44.3 python -B docs/implementation/story-4-17-custody/transfer_once.py preflight`
   and, only after the independent inspection, the same command with final argument `upload`.
3. Read all five original files once through no-follow descriptors; compare identity, size,
   mtime, ctime and hard-link count before/after. Verify all whole-file commitments, complete
   inventories and original packet schemas/artifact maps, path safety, duplicates, links,
   special/encrypted entries and bounded private-material exclusions. Inspect nested public
   chart archives without extraction. Reject any source, inventory, private-material or scope drift.
   The exclusion scan is bounded; it is not a claim to detect every conceivable secret. The
   owner's admission covers only these exact previously sanitized bytes.
4. Retain sanitized current configuration/access/preflight evidence in `preflight.json`. Require
   the dedicated prefix to have no versions or delete markers; a collision blocks all PUTs.
   Immediately before upload, repeat source/protection/prefix checks. Missing permissions or
   changed protection stops without changing any control or probing with writes.
5. Persist `upload-started.json` exclusively, making this attempt non-restartable. Persist a
   per-file intent before each of the five conditional creations; send `If-None-Match: *`,
   exact length, Content-MD5 and AES256. Snapshot bodies come from the verified descriptors.
   Persist each returned VersionId before checking source stability again. No multipart upload,
   overwrite, repack, automatic retry or new transfer attempt is authorized.
6. Any PUT exception, collision or absent/malformed version is uncertain/unaccepted: stop all
   further PUTs, retain the sanitized incident and original evidence. Only read-only version
   reconciliation is allowed. Successful other files do not credit that file or accept the whole
   transfer. Upload results alone explicitly withhold off-site custody acceptance.
7. After all five versions return, dispatch the separate read-only agent. It opens only validator
   credentials, downloads those **exact VersionIds**, retains original downloads under an
   owner-only fresh directory in ignored `artifacts/`, verifies version-bound HEAD/GET/retention,
   AES256, at least thirty days COMPLIANCE from creation, and current non-expiry protection.
   Repeat the preparation document's full inventory procedure and private-material/safety
   checks. Retain independent download evidence and a separate outcome for each archive plus
   manifest in `receipt.json`, preserving all original repository/run/artifact identities, labels,
   expiries, operator/verifier and completion times. Overall acceptance requires all five PASS.
8. Append actual outcomes and receipt links to preparation, intake and deferred-work records.
   Close only demonstrated preservation work. Preserve parent status/qualification input files.

The parent agent performs the independent technical inspection. That is a code-review gate;
the owner has already authorized this bounded execution. Do not commit, push, dispatch
qualification or modify qualification assessments. Platform 4.17 and EventStore 3.21 stay
in-progress; all five guarantees REJECTED/open, technical FAIL (20/24), selection null and
production FAIL. Staging, Subscription and engineering dependencies remain open.
