# Story 4.15 offline preparation package — 2026-10-29

**BLOCKED_NOT_FOR_EXECUTION. `approvalGranted=false`, `liveAcceptance=false`.**
This package prepares a reviewable proposal and missing-input inventory. It
performs no cluster/network calls, API dry-runs, Helm actions, installation,
credential issuance, key generation, unseal, secret writes or recovery. The
assessor has no live adapter, manifest renderer, mutation commands or dispatcher.
Complete metadata, including synthetic fixtures, never grants approval.

Story 4.14 remains in-progress. Its unchanged
[approval package](../staging-foundation/APPROVAL-PACKAGE.md) reports absent target
namespaces, 35 unresolved foundation fields, incomplete isolation qualification
and unaccepted prerequisite gates. No existing credential or historical
observation authorizes staging installation. Consumer acceptance is never a
foundation gate; the unchanged
[dependency direction](../staging-foundation/DEPENDENCY-DIRECTION.md) applies.

`inputs.resolved.json` resolves only the recorded target, candidate CoreDNS
selector and the two architectural assertions identifying the historical shared
token and its planning expiry. Its nulls request owner decisions. The schema
contains exact owner-role questions for all nullable fields and all members of
aggregate choices, including each future app. See [MISSING-INPUTS.md](MISSING-INPUTS.md)
and [INPUT-EVIDENCE.md](INPUT-EVIDENCE.md) for questions, source hashes, JSON
pointers and line citations. Current observed provider pins, replica counts,
service ports, operator identity and any existing backend are not staging choices.

`definitions.json` describes separate physical state, volumes, instances and
whole-state snapshot domains. Ordinary OpenBao snapshots must contain no tenant
keys. A KV path exclusion cannot filter a whole-instance snapshot. Tenant-key
backups have separate custody, access, destination and decryption material, never
with ciphertext backups. No backend, storage allocation or Kubernetes resource
is fabricated. Explicit nulls and symbolic input bindings make the definitions
non-deployable.

Each declared app requires its own scoped `secretstores.hashicorp.vault`
Component, reviewed least-privilege policy and per-pod token mount using
`vaultTokenMountPath` and `dapr.io/volume-mounts`. Exact component versions,
identities, paths, volumes, policies, lifetimes and owners remain unknown.
Directory mounts, the owner-supplied projected Secret-key/file mapping and
the full `vaultTokenMountPath` are separate parameters; the assessor checks that
the token file equals the mounted directory plus projected filename. Validate
injector and mount behavior against the future pinned runtime. The bootstrap
reference is an unqualified Secret name in the application namespace; its data
key is separate. CA wiring proposes explicit `tlsServerName`, `skipVerify=false`
and exactly one owner-selected `caCert` file or `caPem` inline-public-CA delivery
mode. The exact CA source, sidecar mount/file path or public-CA mapping remain
unknown in `nativeTls.caBundleRef` evidence. Dapr
reads the token at sidecar startup. A named owner renews outside Dapr or replaces
it when issuer metadata requires replacement; rotation delivers the token,
restarts every affected pod and sidecar, and collects acknowledgments before
old-token revocation. A file update alone cannot satisfy rotation.

`prepare.py readiness` implements a **proposed metadata contract**, not deployed
app readiness. It binds environment, app ID, exact contract digest, each required
logical secret generation and token delivery revision, plus pod identity and
restart acknowledgment. Missing, stale, wrong, duplicate, malformed or extra
observations fail closed. The referenced `deploy/dapr/openbao-secret-contract.yaml`
is absent; no app ID, static entry, dynamic namespace or required generation is
invented. The checker accepts exactly one supplied pod observation per app and refuses
reused pod identities across apps. `metadataReady` cannot establish all-replica
coverage. The future authenticated verifier must bind the complete expected pod
inventory and coverage to `restartEvidenceRef`; supplied booleans alone do not
prove restart ordering or a new pod. Observations are not authenticated here.

The bounded sequence for a later operation is:

1. Accept 4.14's live apply and complete isolation verification, including its
   own prerequisite gates; preserve all current evidence and statuses meanwhile.
2. Resolve every null and inherited missing input with owner decisions and
   evidence, select supported provider/tool pins and qualify their compatibility,
   and create the value-free environment/app secret-contract instance.
3. Review exact future installation definitions and bytes, target identity,
   toolchain, principals, bootstrap exceptions, network flows, manual unseal,
   custody, renewal, monitoring, backup membership, cutover and forward recovery.
4. Obtain a separate Administrator record approving those exact bytes and bounded
   effects, source/destination and verification/recovery plan. No such approval
   exists; this package emits no operational argv, even with complete inputs.
5. Under that later authority, revalidate native API CA/SAN, context/server/UID,
   qualify restricted pods, install with environment-layer ownership, and verify
   complete positive/negative RBAC, native TLS and AD-8 isolation before enabling
   application deployment authority. Perform controlled provisioning and manual
   unseal within independently proved custody and coverage.
6. Collect sanitized evidence of per-app policy and delivery, owner renewal
   before expiry, replacement delivery followed by pod/sidecar restart and full
   acknowledgment, required-generation refusal/success and old-token denial.
   Prove separate native backup membership, restore integrity, erasure/lineage,
   key coverage and independent recovery access without credential rewind.
7. Administrator and Platform assess actual 4.15 criteria against those future
   observations. Offline tests and owner approval alone cannot establish live
   acceptance. Scope, preparation and live flags must stay explicit.

[BOOTSTRAP-TOKEN.md](BOOTSTRAP-TOKEN.md) defines evidence-only classification and
replacement. [VERIFICATION-RECOVERY.md](VERIFICATION-RECOVERY.md) defines future
qualification and safety boundaries. Offline receipts are retained under
`_bmad-output/implementation-artifacts/evidence/epic-4/4-15/offline-preparation-2026-10-29/`.
The parent records integration preservation; no push is authorized.

Network source/destination identity fields are references to exact reviewed
namespace/selector or address definitions. The owner must evidence every DNS,
ordinary-store, tenant-key-store, replication, monitoring and custody flow or
explicit non-applicability through `policyEvidenceRef`. The assessor does not
parse selectors or validate dependency coverage; a synthetic fixture proves
metadata consistency only.

## Offline verification result

The final scoped suite passed **48 tests** after three independent review passes;
all 18 review entries are triaged in the scoped preparation spec. The unchanged
foundation suite passed **34 tests**, and every foundation source byte was
rechecked against the recorded baseline. Current resolution refuses with exit 2,
empty stdout and exactly **43 current missing-input questions**. Matching synthetic
readiness returns metadata consistency only, with approval and live acceptance
false. Source hashes, exact argv, exit codes and retained initial/final transcripts
are in the [verification receipt](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-15/offline-preparation-2026-10-29/verification.json).
No live observation, provisioning, recovery or acceptance was performed.
