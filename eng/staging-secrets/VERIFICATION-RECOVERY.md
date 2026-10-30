# Future verification and recovery contract

**No operation is authorized now.** These are evidence requirements for a later
exact-byte approved procedure, not runnable instructions. Hashes and source
ranges are in `source-evidence.json`. Architecture AD-7/8, AD-12, Secrets, Release
tiers and Recovery sequence remain authoritative. Provider/tool versions,
backend/topology, listener ports, CA references, real principals, custody names,
response coverage, retention implementation and recovery authority remain null.

Native access qualification must verify the selected context, API server,
cluster UID, authenticated principal, CA chain and endpoint SAN immediately
before and after each stage, without insecure TLS, proxy, alternate-server-name
or credential fallback. The dated native observation only proves its own cut.
Verify each selected OpenBao/key-store endpoint terminates provider-native TLS
with the approved CA/SAN; reject incorrect CA, SAN and plaintext paths. The
parameterized Component sets `tlsServerName` from the owner-selected server name
and `skipVerify=false`. Owner evidence in `nativeTls.caBundleRef` must select
exactly one of `caCert` mounted public-CA file delivery or `caPem` inline public-CA
delivery, identifying the CA source and sidecar directory/file mapping or inline
metadata mapping. These unknown paths and mode are not selected here. Cover
replication, monitoring, custody and real pod DNS dependencies in exact policies,
including the actual resolver datapath; a CoreDNS selector does not prove it.

Before workloads gain authority, qualify pinned stores and sidecars against
restricted Pod Security: non-root identity, seccomp, capability removal, privilege
and host-resource restrictions, selected encrypted volumes and read-only mounts.
Record effective permissions, not just intended labels. Prove the environment
identity's positive rights and full negative job/cross-layer isolation, no
bind/escalate/impersonate, no arbitrary data-namespace workload/PVC or owned-object
writes by application jobs, no cross-app token reads, and least-privilege native
policies. Complete AD-8 isolation includes staging users, pods, service accounts,
credentials, automation, restores and a matching app ID against production:
data, secrets, OpenBao/key custody, app ports/sidecars/workflows, volume binding,
hostnames/certificates, identity administration, registry/records/evidence/backup
prefixes, verification/recovery endpoints, old-epoch and revoked credentials.
Neither this schema nor metadata readiness implements these live proofs.
Source/destination fields reference exact reviewed namespace/selector or address
definitions, rather than parsed selectors. `policyEvidenceRef` must establish
coverage or explicit non-applicability for DNS, ordinary-store, tenant-key-store,
replication, monitoring and custody flows. The assessor does not validate these
reference semantics or coverage, and a synthetic fixture cannot prove either.

Prove each app Component scope and policy, readable per-pod token mount, absence
of values/Secrets in application charts, external owner renewal/expiry alarms,
replacement delivery followed by restart and complete replica acknowledgment,
and old-token denial after authorized revocation. Required generation refusal
must be exercised for absence, stale or unexpected generation, wrong environment,
app or contract, duplicates, malformed records and stale delivery revision.
Successful metadata consistency checks one supplied pod observation per app,
with distinct pod identities across apps. It cannot establish all-replica
acknowledgment, restart ordering or pod newness from supplied booleans. The
future authenticated verifier must bind the complete expected pod inventory and
coverage to `restartEvidenceRef`, along with actual delivery/restart evidence.
Future app code and live qualification must connect that evidence to readiness.

Snapshot qualification must enumerate all persisted state of each selected
backend and every native whole-state snapshot member. Separate logical mounts,
KV paths or policies within one instance cannot exclude tenant keys from that
instance's snapshot. Prove distinct instances, physical state/volumes and snapshot
domains; ordinary snapshot inspection/restore contains no tenant-key state.
Raw volume copies alone never constitute an architectural recovery point.

Ordinary-secret backups and tenant-key backups use distinct custody classes,
destinations, read principals and independent decryption custody; tenant keys
never accompany ciphertext backups. Both have encrypted restricted immutable
off-site copies, per-environment-instance prefixes and independent operator
access. The architecture sets 30-minute starts, seven-day frequent and 30-day
daily complete-chain retention; selected storage, enforcement, pruning and
owners still need evidence. Non-erased key generations live at least as long as
referencing data backups. Preserve points referenced by open/named recovery or
unadopted staging-reset candidates. Monitor at least every 15 minutes, warn below
one hour, and notify on failure/age over one hour; demonstrate independent
key-coverage metadata checks without monitor key access. Coverage/time zone,
primary/deputy acknowledgment bounds and actual monitor deployment remain open.

An erased tenant makes all earlier key backups unusable for that tenant, while
a fresh key backup preserves recovery for other tenants. Bind backup cut,
release/profile/contract identities, checksums, complete chains and independent
test decryption to metadata. Reconcile current surviving tombstone authority and
known lineage as later erasures occur. Unknown lineage or unexplained missing
non-erased generation makes the point unusable. No copied register or restored
snapshot becomes current erasure or credential authority.

Any future recovery needs independently usable Administrator/deputy custody,
manual unseal within declared coverage, authorized source/destination and exact
operation bytes. Manual unseal/root-material use alerts both owners; any root
token is revoked at recovery end. On partial installation, stop on target drift,
keep application authority withheld, retain observations and apply only reviewed
forward corrections. Never remove default deny, rewind credentials or required
generations, roll back data, or delete namespaces/volumes to force success.

For a separately approved replacement-capacity recovery: fence and reissue at
surviving authorities and prove old access denied; isolate old capacity; rebuild
the accepted environment definitions; restore ordinary OpenBao into quarantine;
restore tenant keys separately and reapply surviving tombstone destruction before
application use. Stop on unknown lineage and never resurrect an erased key.
Restore data without running application workloads, then run the accepted
recovery hooks and rotate every restored credential/signing key under the proper
owner. Current issuer authority and newly delivered per-app credentials replace
restored bootstrap tokens; prove new access works and old access fails. Maintain
quarantine/worker restrictions and least privilege until integrity, generation,
key coverage, access denials and monitoring pass. Reopening is separately
controlled by the accepted recovery procedure, never by this assessor.

For in-place data recovery, keep the live tenant-key store, verify coverage of
every referenced non-erased key generation, and rotate only credentials restored
with data. Do not restore OpenBao or rewind live custody. A missing generation
is exempted only by current tombstone authority and known intentional destruction.
Record measured full recovery duration, coverage and lost-window exceptions;
independent access, erasure continuity and credential-denial proof cannot be
substituted by a passing offline fixture.
