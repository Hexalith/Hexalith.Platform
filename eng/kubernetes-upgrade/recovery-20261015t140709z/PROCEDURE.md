# Guarded recovery preparation for attempt 20261015t140709z

This is the reviewable offline successor to private prerequisite packet
`20261015t112841z-post-40-recovery-prerequisite`. It authorizes no operation.
The original 4.0 recovery `20261014t112353z` was accepted at 11:27:34 UTC on
October 15 and expired at **2026-10-15T11:40:42Z**, from signed
`policy.preparedAt + 86400 seconds`. Its signatures and historical acceptance
remain valid evidence of that completed run. They supply no current freshness.
Do not extend dates or repeat completed captures, restores, resume or cleanup.

The isolated closeout packet is
`/home/administrator/hexalith-retirement-investigation-evidence/20261015t140709z-s427-expired-recovery-preparation/closeout-20261015t143451z-4fbeb896`.
Its `attempt-bindings.json` pins this procedure, the guard, local tools, exact
historical sources and proposed topology. Every unknown field has a named
closed blocker. No proposed container, directory, member or grant was created
or observed. Earlier receipts, failed OIDC evidence and four October 1 scripts
remain unchanged; those scripts must never execute for this attempt.

## Exact source and proposed target

| Identity | Historical expected pin; require fresh matching evidence |
| --- | --- |
| Native source | `https://192.168.1.30:6443`; kube-system UID `b5860674-1d1e-4a3b-b413-e1bb3b9a4557`; node `node1`; server v1.34.9 |
| External etcd | `https://192.168.1.30:2379`, peer `https://192.168.1.30:2380`, member `node1`; cluster ID `5441947248925166366`, member ID `3140057840123254396`; version 3.6.5; data directory `/var/lib/etcd` |
| Independent native authority | Existing node `/etc/kubernetes/admin.conf`, subject `CN=kubernetes-admin,O=kubeadm:cluster-admins`; unowned `kubeadm:cluster-admins` ClusterRoleBinding UID `8224bcb7-57c6-437d-a27c-1120d03c8b4b`, roleRef `rbac.authorization.k8s.io/ClusterRole/cluster-admin` |
| SSH source | `root@192.168.1.30`; existing private key path `/home/administrator/hexalith-upgrade-evidence/access/node1-existing-id_rsa`; historical public fingerprint `SHA256:05DywFqxl6wwcIU//1IiOMWTEDE60R2SkC0WVC9bOlo` |
| SSH host | Historical pin `SHA256:cC09u9n08cO/+GKfx4TL6xMuFHQUiQXm3UFaF3TWFGw`; independent current trust/readback remains unproven |

The transport `/home/administrator/.kube/hexalith-production`, context
`jpiquot@local`, is the retained KubeSphere authority. A successful read through
it does not prove recovered native authority. The independent client public
fingerprint, encrypted off-node location/version, current binding/subject and
revocation lineage, node UID, source CA/tool fingerprints and sealed break-glass
identity/custody remain unmeasured. Do not issue a new client or grant to fill
these fields. Missing independent authority closes readiness.

The existing recovered client is retained at
`/home/administrator/hexalith-management-evidence/qualification/20261004t110223z-archive-unlock-readback/existing-break-glass-config.age`.
Its exact ciphertext and October 4 independent-use/readback records are pinned
privately. This resolves its historical location; recipient decryption,
current identity and independent custody acceptance remain closed.

The proposed isolated etcd container is `s41-etcd-20261015t140709z`; the separate
file-recovery container is `s41-node-20261015t140709z`. Proposed member name and
initial cluster token are `s41-isolated-20261015t140709z`. Inside the etcd
container only, client/peer URLs are `http://127.0.0.1:2379` and
`http://127.0.0.1:2380`, with restored data `/restore/etcd`. Node extraction is
`/restore/root` inside its separate container. Both require `--network none`,
read-only root, no published ports, no host PID/IPC/network, no privilege or
devices, all capabilities dropped and `no-new-privileges`. `/restore` is a
private `tmpfs` with `nosuid,nodev,noexec,mode=0700`, proposed size 384 MiB for
etcd and 768 MiB for node extraction. Each container has a separate `/input`
private `tmpfs` with those same flags and a proposed 512 MiB bound. There are
**zero host mounts**, including read-only input or tool mounts. An explicitly
approved bounded stdin adapter must deliver only the exact validator-decrypted
snapshot to `/input/snapshot.db` or archive to `/input/node-recovery.tar.gz`,
verify byte length/digest inside the named container and expose no plaintext
through stdout or diagnostics. Input delivery and measured capacity remain
closed blockers; oversize input stops without resizing or fallback. Never mount
production `/var/lib/etcd`, kubelet, PKI, the Docker socket or node root.

The historically used image
`ubuntu@sha256:008173c23f95b170204355c12626cb5a965d779a7e1283b09e9cffbb1bf33ca3`
is a historical base candidate, not a current accepted recovery image; no
`/tools` contents are inferred from it. A separately qualified digest-pinned
recovery image must provide measured `/tools/etcd`, `/tools/etcdctl`,
`/tools/etcdutl` and the exact archive/hash/input-delivery tools on its read-only
image filesystem. These executables never come from either noexec tmpfs.
The recovery image digest, every executable/dependency hash, stdin adapter,
current local image ID, engine/context/daemon identity and capacity require
measurement and exact approval and remain closed blockers. No image build,
pull or Docker discovery ran. Target cluster/member IDs and
container IDs are unknown and must be distinct from source and each other.
`--network none` plus inspected network/mount/port/capability state and a
bounded failed probe to the source endpoint are required fencing evidence.
Different member names and loopback URLs alone do not establish isolation.
This is same-host recovery qualification, with no site-resilience claim.

## Ordered conditional procedure

These argv descriptions are inert review inputs. No live adapter is supplied
or invoked by the guard. An eventual reviewed adapter must bind the exact
inputs below, enforce create-only custody, stream raw results into encryption,
check semantic output and preserve each failure before returning. It must
never use shell interpolation, executable authentication hooks, `assert` as a
safety guard, automatic retry, fallback endpoint or automatic production restore.
The four old Python scripts cannot serve as that adapter.

1. **Current prerequisite and authorization.** Independently establish a new
   fresh signed 4.0 gate through a separately covered exact action; this expired
   RID cannot be refreshed. Retain the existing October 7–31 window, this
   conversation incident channel and Administrator roles within their scope.
   Obtain exact 4.1 approval for this procedure/bindings/tool hashes, current
   source, proposed targets, capture/export, recipient decryption/readback,
   isolated operations, recovered credential use, target cleanup, signer and
   stop/recovery conditions. The existing 4.0 approval and four-record signing
   delegation do not cover those actions. No renewed selection of old roles
   or dates is required. An uncovered action remains blocked.
2. **Current static transport, source and tools.** Require regular owner-only
   static native configuration, private credentials and a pinned known-host
   entry, no exec/auth-provider/token-file hooks, validated TLS/SSH trust,
   explicit context/endpoint and independently recovered authority. Bind
   source node UID, API CA and all member URLs/IDs, health and alarms. Through
   an approved bounded SSH adapter use `BatchMode=yes`, `IdentitiesOnly=yes`,
   `StrictHostKeyChecking=yes`, `ConnectTimeout=8` and the exact private key and
   pinned known-host path from the private bindings. Do not accept host-key
   discovery as trust. Recheck source versions and SHA-256 of node-side
   `/usr/local/bin/{etcd,etcdctl,etcdutl,kubeadm,kubelet,kubectl}` and archive
   tools; local copied tools are not proof of node bytes. Version or source
   drift stops before capture. Kubernetes patch/upgrade targets are not chosen.
3. **Health/member baseline and source configuration.** Source etcd argv
   begins `/usr/local/bin/etcdctl`,
   `--endpoints=https://192.168.1.30:2379`,
   `--cacert=/etc/kubernetes/pki/etcd/ca.crt`,
   `--cert=/etc/kubernetes/pki/etcd/client.crt`,
   `--key=/etc/kubernetes/pki/etcd/client.key`, `--dial-timeout=5s`,
   `--command-timeout=30s`. These client TLS paths come from line 74 of the
   retained October 1 capture script and its node-file inventory; the service
   itself historically uses `/etc/ssl/etcd/ssl/{ca.crt,server.crt,server.key}`.
   Current effective client/service paths, certificate/CA fingerprints,
   key/certificate pairing and validity must match newly reviewed native
   configuration before this proposal can run. No present identity or accepted
   path is inferred from those old bytes. Use exact sequential suffixes
   `endpoint health -w json`, `endpoint status -w json`,
   `member list -w json`, `alarm list -w json`; require a single healthy
   matching member/cluster, positive revision, known leader, expected 3.6.5
   version and no alarms. Bind current kubeadm/static configuration inventory
   SHA-256. Preserve raw JSON only under encrypted custody.
4. **One fresh capture.** Proposed source temporary directory is exactly
   `/run/hexalith-s41-20261015t140709z-source`, owner root, mode 0700, created
   exclusively after absence and mount/capacity checks. Source snapshot suffix
   is `snapshot save /run/hexalith-s41-20261015t140709z-source/snapshot.db`.
   Override the inner command timeout to 90s only for that snapshot command.
   Snapshot integrity uses `/usr/local/bin/etcdutl snapshot status` on that
   exact file with `-w json`, never `--skip-hash-check`. Independently hash and
   count the exact plaintext stream. At its actual snapshot revision `R`,
   record source `endpoint hashkv --rev=R -w json` and
   `get "" --from-key --count-only --rev=R -w fields`. `R` must be parsed from
   that snapshot; a compacted/unavailable revision stops, with no recapture.
   Preserve original numeric native hash/status plus SHA-256 of canonical
   comparison JSON. The approved non-secret canary keys are exactly
   `/registry/namespaces/default`, `/registry/namespaces/kube-system` and
   `/registry/services/specs/default/kubernetes`; use `get KEY --rev=R -w json`
   for each, require exactly one KV, and retain bytes/modRevision/readRevision
   and value SHA-256. Never publish values or use tenant/Secret keys.
5. **Matching node archive.** In the same capture lineage inventory existing
   `/etc/kubernetes`, `/var/lib/kubelet/{config.yaml,kubeadm-flags.env,instance-config.yaml,pki}`,
   kubelet/containerd/etcd systemd units and drop-ins, `/etc/default/kubelet`,
   `/etc/containerd`, `/etc/cni/net.d`, `/etc/etcd.env`, `/etc/ssl/etcd/ssl`,
   `/etc/apt`, `/var/lib/dpkg/status`, OS release files, `/etc/{passwd,group,ssh}`,
   `/root/.ssh` and the exact installed runtime/Kubernetes/etcd binaries named
   in private `requiredArchivePaths`. Record absent optional paths explicitly;
   absent required PKI/configuration/authority fails. Inventory each regular
   file's SHA-256/length/numeric UID/GID/mode, directory metadata and symlink
   target without following outside the explicit inventory; refuse devices,
   sockets, traversal and unexpected hardlinks. Capture native versions,
   binary hashes, package/repository state and active service configuration.
   Build a validated unique null-delimited list of explicit relative members,
   including directory entries and every inventoried child separately. Stream
   `tar --format=pax --numeric-owner --acls --xattrs --gzip --create
   --file=- --directory=/ --no-recursion --null --verbatim-files-from
   --files-from=/run/hexalith-s41-20261015t140709z-source/archive-members.nul`
   to encryption. Refuse duplicate list members and duplicate archive entries;
   the archive member set must equal the inventory exactly, with no recursive
   discovery, traversal or extra entries. Never archive the live etcd data directory. Compare per-file
   inventory before/after streaming; concurrent configuration drift stops.
6. **Encrypted publication and distinct immutable readback.** Stream snapshot
   and node archive to the pinned Administrator public recipient using the
   pinned `age`; plaintext stays in source root-only temporary custody or
   approved bounded memory/tmpfs, never Git or regular local disk. Ciphertext
   names are `external-etcd-snapshot.db.age` and `node-recovery.tar.gz.age`
   beneath proposed prefix
   `evidence/4-1/20261015t140709z-s427-expired-recovery-preparation/` in
   `hexalith-recovery-points`, `fr-par`. This is **uncovered** by current exact
   4.0 approval/IAM scope. Require explicit provider permission for that prefix
   before any upload; do not broaden IAM automatically. Distinct validator
   application `406da2fa-5d02-4a49-bcbc-de0218c6460d` must GET each exact
   writer-produced version, verify length/SHA-256, AES256, COMPLIANCE and both
   retention APIs with at least 30 days from object creation. Writer application
   is `9a7cff8d-cffc-4554-93fe-01ed8e0d0d04`; writer self-read is insufficient.
   Authorized recipient decryption of those validator copies must produce
   matching plaintext hashes, snapshot status and file inventory. Key access
   and independent recipient custody/readback remain unsupplied; no plaintext
   decryption or existing provider credential loading occurred here.
7. **Fenced etcd restore and comparisons.** After proving proposed target
   absence and exact image/engine/tools, inspect the isolation configuration
   above before startup. Deliver and verify the exact snapshot through the
   approved stdin adapter into `/input`; no host bind mount or tool copy into
   noexec storage is permitted. In that container only, restore argv is
   `/tools/etcdutl snapshot restore /input/snapshot.db
   --data-dir=/restore/etcd --name=s41-isolated-20261015t140709z
   --initial-cluster=s41-isolated-20261015t140709z=http://127.0.0.1:2380
   --initial-cluster-token=s41-isolated-20261015t140709z
   --initial-advertise-peer-urls=http://127.0.0.1:2380`.
   Start `/tools/etcd` with exactly that name, data directory, cluster/token
   and loopback client/peer URLs, no external listener. Do not bump revision
   in this comparison rehearsal. Require healthy member, absent alarms and
   different actual cluster/member IDs. Read revision `R` using the same
   hashkv/count/canary suffixes and require exact revision/native hash/count,
   bytes/modRevision and canary digest agreement. Record any current-revision
   lease-expiry delta separately; it cannot excuse mismatch at `R`. A
   source-endpoint probe from the networkless container must fail within
   dial 1s/command 2s, and inspected source fencing must remain in force.
8. **File and recovered-access rehearsal.** In the separate networkless
   container deliver the validator-decrypted archive through the same approved
   bounded stdin contract to `/input/node-recovery.tar.gz`, verify it, then
   restore it to `/restore/root`,
   preserving numeric ownership/modes/ACLs/xattrs and comparing every inventory
   entry and certificate/public-key pair plus validity. The all-capabilities-
   dropped target is a proposed fence, not demonstrated extraction capacity:
   changing numeric ownership or restoring privileged xattrs may require
   additional capabilities. **Extraction capability and user-namespace mapping
   qualification is an explicit closed blocker.** Before approval, demonstrate
   the exact inventory's UID/GID/ACL/xattr preservation in a confined user
   namespace, or revise the exact topology with narrowly approved internal
   capabilities and zero host mounts. Do not claim success, silently
   add privileges or weaken an inventory comparison. Use neither restored
   SSH daemon nor control-plane services. After this comparison, any approved
   recovered SSH/native proof is a separate narrowly scoped private-source
   read action using only the recovered credential in protected memory/tmpfs:
   require exact source host/CA/cluster UID, actual identity, unowned native
   binding UID/roleRef/subjects, complete grant/revocation lineage independent
   of KubeSphere and break-glass custody/recovery. Bind both to the matching
   node archive. `jpiquot`, a fresh grant, node-file presence or a claimed
   `recovered=true` cannot substitute. Public/unauthorized denial and MFA/
   deputy requirements retain their existing scope; no new grants are implied.
9. **Target-only cleanup, absence and signing.** Record exact owned container
   IDs before any stop/removal. Stop only those IDs, then remove only those
   IDs and verify expected NotFound, zero associated volumes/mounts/ports,
   destroyed tmpfs, stopped children and absent recovered/plaintext/temporary
   credential material. Remove only the exact owned source temporary directory
   after matching file identities and custody/readback preservation; never
   use a glob or touch `/var/lib/etcd`. Verify source/member/node/configuration
   identities, workload namespace/PVC/PV bindings and health unchanged. Failed
   cleanup is a retained incident and closes acceptance, not a pass with caveat.

Per-command outer bounds are SSH/native reads 30s (native request 15s), snapshot
120s (inner etcd 90s), archive/encryption/transfer 300s, provider requests 60s,
restore/start/readiness 120s total, target queries 20s, stop/removal/absence 30s
each. The reviewed adapter must terminate/reap process groups and preserve
partial encrypted diagnostics on timeout; a timeout or unknown result permits
zero dependent dispatches and no retry. Do not restore production, reboot,
drain, change binaries/grants, reopen public routes or resume other operations.
Administrator incident/recovery owner decides retained partial-state cleanup.

## Signed records, acceptance order and remaining adapter gap

The owner is `jpiquot@itaneo.com`, pinned key fingerprint
`SHA256:8XlNQvE3ucPf/e509wU4qtNgiyWA+TKmLei7F7+TCvk`.
Propose namespace `hexalith-recovery` with the exact retained allowed-signers
bytes pinned privately. Existing recovery trust identifies that key, but new
4.1 signing/execution delegation is uncovered. Never reuse the October 1
signer or the four-record delegation. Sign only actual final bytes and verify
original signatures with pinned `/usr/bin/ssh-keygen -Y verify`, principal,
namespace and trust root; no JSON `signed`/`verified` flag is authentication.
Each record/signature needs its own exact immutable validator readback.

Order: fresh signed 4.0 validation/readback → exact 4.1 action approval/current
source and independent access → snapshot/configuration capture and distinct
readback → fenced restore/file/access rehearsals → target-only cleanup/absence
→ actual signed `external-etcd-recovery-point.json`,
`external-etcd-isolated-restore.json`, `node-recovery-bundle.json`,
`native-access.json`, cleanup and acceptance receipt → independent signature,
binding/freshness/readback validation. Bind the snapshot/capture root digest
before restore; then sign final point and bind its exact digest from restore
and node records, avoiding cyclic hashes. Preserve the original native etcd
status/hashes and explicit normalized projections required by `retire.py`'s
recovery-point/preflight contracts. No existing consumer automatically accepts
this offline packet; the exact current-attempt adapter, source evidence,
operational policy/IAM, decryption, authority and signing remain blockers.

The isolated offline guard `readiness.py` checks a deliberately smaller local metadata contract:

```bash
bash /home/administrator/hexalith-retirement-investigation-evidence/20261015t140709z-s427-expired-recovery-preparation/closeout-20261015t143451z-4fbeb896/CHECK-READINESS.sh
```

The private `CHECK-READINESS.sh` contains the actual exact digest. It is the
only executable proposal in this preparation and reads local files only.
Preserve that original helper as historical preparation: its Python startup
permits environment or site customization. A separate final reviewed helper
must invoke the pinned interpreter with `-I -S -B`, verify the exact reviewed
interpreter/guard/bindings source hashes and use only its fixed local inputs.
Do not execute the original helper as a trusted guard, rewrite it or inherit
`PYTHONPATH`, user site packages or `sitecustomize` into the reviewed check.
The guard's `approval` input describes final acceptance metadata binding the
completed records. It is distinct from the pre-run execution-scope approval
in step 1, which must exist before those future records can be produced.
The bindings' 86400-second metadata ceiling is a proposal; it does not accept
or inherit a new 4.1 operational freshness policy. That policy's exact scope,
age and signature must be approved separately for actual operations.

The declared local tool inventory must name kubectl, age, etcd/etcdctl/etcdutl,
SSH/ssh-keygen, Docker, tar, timeout and Python. Separate source and recovery
role inventories bind their exact declared executable hashes; unknown runtime
bytes stay blocked. Metadata must declare the zero-host-mount isolation
profile, extraction/user-namespace qualification, matching source node and
native subject/binding, independent complete authority lineage, and ordered
capture/publication/readback/rehearsal/cleanup/final-acceptance times. Both
object-retention API results must agree exactly and cover thirty days from
creation, with retention still current; thirty days remaining is not required.

Exit 0 means structurally consistent **untrusted metadata**. It never verifies
cryptography, contacts a provider, loads credentials, executes tools, writes
evidence or opens a gate. Its synthetic tests intentionally use fake signatures
and versions, proving that metadata consistency conveys no authorization.
The actual attempt refuses with the unchanged earliest expiry. Unknown or
expired/future prerequisites, mismatched approval/bindings, unsafe custody,
tool drift, unsigned/unverified/readback mismatch, target collision/fencing,
wrong authority and incomplete cleanup refuse. There is no `--now`, live,
force, signing, capture or execution mode.

Preparation keeps **4.27 in-progress, 4.1's operational gate closed and
Timesheets 3.6 open/FAIL**. Accepted retirement, console/health/census/plan,
post-retirement recovery, per-hop approvals, Rancher and all Timesheets
release/provider/source/deployed-acceptance/default-deny gates remain required.

This isolated implementation lives in `eng/kubernetes-upgrade/recovery-20261015t140709z/`. Generic files modified by another session and staged Docker copies are unaccepted concurrent inputs. The repository checkpoint is `20261015t143451z-s427-isolated-offline-closeout`; the logical recovery proposal/targets remain `20261015t140709z`, with preparation lineage `20261015t112841z-post-40-recovery-prerequisite`. No current operational observation or permission is supplied by isolation.
