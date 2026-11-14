# Story 4.17 — packet preservation preparation

2026-11-14, Europe/Paris. Applies the owner's **“apply recommended”** direction: prefer existing
off-site storage, retain original packets and keep the preservation work small.

**Four exact original ZIPs are locally staged; off-site custody remains unaccepted.** Both parent
stories remain in-progress, all five historical guarantees REJECTED/open, technical qualification
FAIL (20/24), broker selection null and production eligibility FAIL.

## Completed local preparation

Staging directory, covered by the repository's existing `artifacts/` ignore rule:

`artifacts/story-4-17-custody-staging-2026-11-14/`

It contains four ZIP names by artifact ID and one `manifest.json`. Every archive matches its
retained SHA-256 commitment. The manifest inventories each entry's path, byte count and SHA-256;
packet manifests were checked separately using their original schema and root. No qualification
verifier or broker capture was rerun, and original cached archive paths remain present.

| Capture / retained authority | ZIP file | Bytes | ZIP entries | Packet schema / manifest artifacts | Recorded artifact expiry (UTC) |
| --- | --- | --- | --- | --- | --- |
| Historical standalone failure | `13638347236.zip` | 1,515,070 | 495 | 2 / 494 | 2026-12-08T23:05:01Z |
| Historical fixture failure | `13638293057.zip` | 1,946,018 | 601 | 2 / 494 | 2026-12-08T23:08:16Z |
| Candidate-01, diagnostic-only; original qualification integrity rejected | `13995674927.zip` | 2,666,335 | 575 | 3 / 551 | 2026-12-13T00:11:32Z |
| Candidate-02, original verified integrity; technical FAIL (20/24) | `14018214306.zip` | 1,650,434 | 544 | 3 / 520 | 2026-12-13T07:40:43Z |

Total original ZIP bytes: **7,777,857**. An independent read-only reviewer matched all four
commitments, **2,215** entry paths/sizes/hashes and **2,059** packet artifact hashes. No failures,
unsafe/duplicate paths, symlinks or encrypted entries were found. Candidate-01's original
qualification rejection label remains. Byte-level checks grant no new qualification authority.

| File | Expected SHA-256 |
| --- | --- |
| `13638347236.zip` | `20a1719b753c7379207bbeb92cc73f70af99b7fa7e71734fd4a58ec7427cf223` |
| `13638293057.zip` | `20643dbd7d3702f3f504b0a4ce5fd3fe627ff32493d00624d37dca40e81b8a27` |
| `13995674927.zip` | `906c0d943e7a0ee8393bb83b96c4c917477fe18820afe37fe6ba7274afe7aeb7` |
| `14018214306.zip` | `a26b848840ca8061a00d8ea7598b52b6a91b5331822645c23bc527278e40949a` |
| `manifest.json` | `ea4b7666ae5b77b3f00b24b6ee32c35c98da6bbe5004ebc24fb5f9c1bfdd183f` |

The manifest records `LOCAL_STAGING_ONLY`, `offSiteCustodyAccepted=false` and null destination,
transfer operator and off-site verifier. It records a hold while guarantees remain open, followed
by deletion review against applicable policy; there is no automatic deletion date.

The filesystem filled during a documentation update. To recover only this task's redundant
allocations, the staged ZIPs were changed to **hard links to the verified original cached ZIPs**,
preserving both paths and exact bytes. The original cache and staging now share file inodes;
editing either linked file would affect both. The new staging directory is owner-restricted,
but this is one local filesystem, not independent storage or a backup. Ordinary artifact cleanup
can remove these names; arrange off-site transfer rather than relying on this staging folder.
The affected new preparation document was restored, and subsequent checks cover the final files.

## Existing-archive search and remaining dependency

The search covered Story 4.13's acceptance criteria/status, the referenced
[D2 resource resolution](story-5-4-coupled-delivery-2026-10-25/installation-evidence/d2-resource-resolution-2026-10-28.json),
[D2 verification](story-5-4-coupled-delivery-2026-10-25/installation-evidence/d2-resource-verification-2026-10-28.json)
and [PG2 archive feasibility](pg2-p1-v1-feasibility-2026-10-12.md). Story 4.13 remains backlog.
These handoffs identify no accepted archive endpoint/allocation and usable scoped access for
this transfer. This is a scoped documentation finding, not a global resource census.
Proposed GCS/HSM/lifecycle resources in other stories are not an installed archive to adopt.

**Remaining input:** the actual existing off-site archive location/allocation and its approved
access reference. Bind the real transfer operator and independent verifier there, with create-only
or equivalent protected-original controls and the approved retention hold. No storage service,
custody platform, credentials or infrastructure were provisioned to fill this gap.

## Transfer and acceptance once the archive is identified

1. Use a fresh dedicated folder/object prefix at that destination. Copy the four ZIPs and
   `manifest.json` without repacking or overwriting existing objects. Preserve original
   source/run/artifact identities and evidence-authority labels in the manifest.
2. Have the independent verifier retrieve the five files from that destination. Check each
   whole-file digest against the table above, every ZIP entry against the full inventory and
   each packet's internal artifact map. Before accepting transfer, repeat the original archive
   safety and approved private-material exclusion checks for each archive and confirm the
   destination is permitted to receive those sanitized bytes. Equality of hashes alone does not
   establish that permission. Keep candidate-01's original rejection label; unexpected private
   material blocks transfer acceptance rather than silently repacking the original packet.
3. Record one off-site receipt with destination/failure domain, storage object/version identities,
   access/retention/delete controls, actual operator/verifier and completion time. Keep the local
   manifest unchanged; the transfer receipt is separate. Local readback cannot substitute for
   independent retrieval from that destination. The receipt must have a separate result for
   **each archive**: artifact/run identity, exact ZIP digest, original authority label, received
   object/version, independent verification outcome, private-material check, acceptance outcome
   and completion time. A combined receipt is sufficient only if it retains all those per-archive
   results; one successful archive cannot stand in for a missing or failed one.
4. Complete the historical-packet transfer before **2026-12-08T23:05:01Z** and
   **2026-12-08T23:08:16Z**, and candidate transfers before their respective December 13 expiries.

This small transfer does not require completion of Story 4.13's broader artifact-retention
platform. Existing qualification, deployment and production prerequisites remain unchanged.

## Reproduce the completed local byte checks

Independent local reviewer: `/root/successor_review`, read-only verification on 2026-11-14
(Europe/Paris); retained result: four archive commitments, 2,215 entry inventories and 2,059
packet artifact hashes PASS, original labels unchanged, off-site custody false. The following
stdlib-only procedure reproduces the inventory/packet-map checks from the directory containing
the four ZIPs and `manifest.json`; it performs no extraction, network or qualification action:

```bash
python3 -B - <<'PY'
from pathlib import Path, PurePosixPath
import hashlib, json, stat, zipfile
def check(ok, message):
    if not ok:
        raise SystemExit(message)
root = Path.cwd()
raw = (root / 'manifest.json').read_bytes()
check(hashlib.sha256(raw).hexdigest() ==
      'ea4b7666ae5b77b3f00b24b6ee32c35c98da6bbe5004ebc24fb5f9c1bfdd183f', 'manifest differs')
m = json.loads(raw)
check(len(m['archives']) == 4 and m['offSiteCustodyAccepted'] is False, 'scope/custody differs')
entries = artifacts = 0
for row in m['archives']:
    raw_zip = (root / row['file']).read_bytes()
    check(len(raw_zip) == row['bytes'] and hashlib.sha256(raw_zip).hexdigest() == row['sha256'],
          'ZIP differs: ' + row['file'])
    with zipfile.ZipFile(root / row['file']) as z:
        names = z.namelist()
        check(len(names) == len(set(names)) == row['entryCount'] and
              set(names) == {x['path'] for x in row['entries']}, 'inventory differs')
        for item in row['entries']:
            name = item['path']; info = z.getinfo(name)
            check(not PurePosixPath(name).is_absolute() and '..' not in PurePosixPath(name).parts
                  and '\\' not in name and not stat.S_ISLNK(info.external_attr >> 16)
                  and not info.flag_bits & 1, 'unsafe/encrypted entry')
            data = z.read(name)
            check(len(data) == item['bytes'] and hashlib.sha256(data).hexdigest() == item['sha256'],
                  'entry differs: ' + name)
        packet = json.loads(z.read(row['packetPath']))
        check(packet['schemaVersion'] == row['packetSchemaVersion'] and
              len(packet['artifacts']) == row['packetArtifactCount'], 'packet schema/inventory differs')
        prefix = row['packetPath'][:-len('packet.json')]
        for name, expected in packet['artifacts'].items():
            check(hashlib.sha256(z.read(prefix + name)).hexdigest() == expected, 'packet artifact differs')
        entries += len(names); artifacts += len(packet['artifacts'])
check((entries, artifacts) == (2215, 2059), 'denominators differ')
print('PASS: four ZIP commitments; 2215 entries; 2059 packet artifacts; local staging only')
PY
```

A post-recovery independent reread also passed all four digests, 2,215 entry checks and 2,059
artifact hashes, and confirmed that each staged archive shares its original cache inode with
exactly two hard links. The manifest and original evidence labels remained unchanged.

This byte-check procedure does not perform the separate private-material/destination-admission
check or prove off-site storage, retention, authority or qualification. Its recorded result covers
only the stated local preparation scope.
