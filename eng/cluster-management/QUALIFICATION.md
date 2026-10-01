# Story 4.26: management qualification

Qualification remains **in progress**. Production census is read-only, proposed targets are unaccepted, and retirement/upgrade/deployment gates are closed. The [criterion ledger](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t173902z-census/criteria.json) keeps all eight acceptance criteria incomplete. No grants, approvals, role mappings or signed acceptances were manufactured.

## Observed native evidence

The [2026-10-01 census](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t173902z-census/inventory.json) binds explicit context `jpiquot@local` to the direct TLS API `https://192.168.1.30:6443`, Kubernetes `v1.34.9`, and retained kubectl `v1.34.12`. The default workstation kubectl `v1.36.1` exceeds permitted minor skew and was excluded from capture. Native API metadata reports node runtime/topology; effective host kubeadm, external-etcd, virtualization and spare capacity still require the operator's working SSH login. Historical host observations are not current capacity proof.

The completed API capture has 2,594 unique native UIDs, 23 namespaces, 27 PVCs, 28 PVs, 137 CRDs, eight mutating and twelve validating admission configurations. It explicitly discovers non-preferred served CRD versions. The earlier attempt is retained with ten coverage gaps; it is superseded for API coverage, not erased. Capture is sequential rather than an atomic etcd cut: compare fresh UIDs/resourceVersions before any later action.

`ks-core` chart `1.2.4` / application `v4.2.1` and `ks-console-embed` chart `1.2.0` are deployed. Fifty-three `Extension` and 73 `ExtensionVersion` objects are catalog evidence; one `InstallPlan` is observed. The separate [capability proposal](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t181200z-capability-review/capability-dispositions.json) confirms that plan is Installed and proposes concrete replacements/retirements/preservation for core, console, catalog, IAM/workspace, application catalog and shared DNS/CNI/storage/ingress/identity. Each proposal lists consumers, exact observed members and deletion effects requiring review; none is approved. Catalog availability does not establish installation or unused functionality. [Object entries](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t173902z-census/capabilities.json) retain exact identities, ownership and finalizers. Owner references cover only one dependency mechanism; config references, subjects, admission backends, route consumers and operator decisions need review. Accepted dispositions/deletion effects remain unresolved. The production [allowlist](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t173902z-census/retirement-allowlist.json) therefore contains **zero removal actions** and records exact protected namespace/storage identities.

Raw API lists (including Secrets), kubeadm configuration, Helm manifests/hooks/values and error output are encrypted before filesystem writes. Private attempts are under `~/hexalith-management-evidence/qualification/`; attempt directories are 0700 and files 0600. [Export digests](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t173902z-census/encrypted-exports.json) link plaintext/ciphertext hashes without revealing values. Independent off-node ciphertext readback/decryption and retention signatures are unverified; a local checksum is insufficient. The Administrator's encryption identity requires an interactive passphrase, so unattended decryption was refused. Historical 4.0/4.1 recovery directories and signed records were not modified.

| Capability | Proposed disposition/consumer | Deletion effect requiring review |
| --- | --- | --- |
| Core API/controllers | Replaced by independent native operations, then private Rancher; existing operators and tenant/extension reconciliation | Core release, controller-created instances and admission dependencies require exact dependency order |
| Console/installed embed | Replaced by private native CLI/Rancher; management users and routes | Retire exact installed plan/extension release and console routes; preserve shared ingress |
| Extension catalog | Proposed unused/retired without an optional replacement; marketplace and installed-plan references | Catalog availability is not installation or unused proof; check non-owner/config consumers |
| Workspace/accounts/IAM | Replaced by approved explicit native/Rancher roles; Administrator/deputy and tenant owners | Preserve namespaces; reconcile independent current grants/revocations before losing IAM controllers |
| Application catalog/releases | Proposed unused/retired subject to consumer review; existing release writers and catalog users | Preserve native Helm/application identities/data and identify live release consumers |
| CNI/DNS | Native-owned, preserved for every workload | Preserve Calico/native DNS/kube-proxy owners and APIs |
| Storage/backup | Native-owned, preserved for databases/workloads/recovery | Preserve classes/provisioners, PVC/PV UIDs/bindings, Velero/CNPG/Barman and every recovery point |
| Ingress/TLS/identity | Native-owned, preserved for public OIDC/apps and private administration | Preserve Traefik/cert-manager/Keycloak/OpenBao and shared routes/TLS/storage |

These are unapproved proposals; no cohort or lack of ownerReferences is a removal scope.

## Access and authority

[Access observations](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t173902z-census/access.json) demonstrate authorized native reads and a credential-free, CA-verified native request denied with HTTP 403. This is a private-network observation; public denial, a distinct unauthorized authenticated principal, authorized maintenance capabilities, independent recovery kubeconfig/key custody and MFA have not been demonstrated. The selected kubeconfig symlink reports mode 0777; its resolved Linux file is operator-owned mode 0600. A symlink mode does not establish effective public access. Independent custody/ACL acceptance remains unverified.

| Principal | Proposed authority | Required evidence before use |
| --- | --- | --- |
| Administrator | Sole management-grant/revocation writer; native maintenance and approved Rancher administration | Approved identity/MFA, exact native rights, private/public/unauthorized access tests and independent signing/custody |
| Deputy | Existing scoped operational recovery only; no grant, admission or membership expansion | Administrator-approved existing scope, separate credentials/MFA, negative grant/global-role tests and independently readable current lineage |
| Staging member/executor/application principal | No shared management or production proxy authority | UI/API/proxy/kubeconfig denial and isolation tests under 4.28/4.25 |

No KubeSphere workspace/project/account is automatically mapped to a Rancher role. Retain Administrator-signed grant/revocation records off-site independently of manager backups, ordered by sequence and predecessor digest, with principal, exact scope, decision/cut time and current head. Verify detached signatures cryptographically against the established Administrator trust root; file/signature presence or boolean assertions are not verification. Metadata validators in `qualify.py` do not authenticate records or approve grants.

Restore testing must lose the source manager, revoke an existing entitlement after the backup cut, and reconcile the complete independent current lineage before any restored UI/API/proxy/agent reconnects. Reject stale credentials, gapped/conflicting heads and any deputy scope expansion. Missing authority keeps the restored manager disconnected and denied; only an already entitled scoped credential can be reissued. Native workload recovery can proceed while manager restoration is pending. These tests remain unexecuted because no approved independent management lineage/MFA/role evidence was supplied.

## Repeatable collection and validation

Run from the repository root using explicit, requalified binaries and the Administrator's public encryption recipient. Supply a new attempt ID each time; reuse, symlink evidence custody, Git-local private exports and writes into existing recovery custody are refused.

```bash
python3 eng/cluster-management/qualify.py \
  --attempt-id 20261001t180000z-census --operator Codex \
  --context jpiquot@local --kubeconfig /home/administrator/.kube/config \
  --kubectl /home/administrator/hexalith-upgrade-evidence/tools/v1.34.12/kubectl \
  --helm /home/administrator/hexalith-upgrade-evidence/tools/helm-v3.22.0/helm \
  --age /home/administrator/hexalith-upgrade-evidence/tools/age-v1.3.2/age \
  --recipient '<Administrator public age or SSH recipient>'
python3 -m unittest discover -s eng/cluster-management -p 'test_*.py'
```

The collector permits direct verified TLS native contexts, rejects embedded URL credentials/query/fragment before projecting an endpoint, reads API discovery and every listable preferred resource plus actual served CRD versions, paginates, preserves inaccessible/malformed/timed-out discovery as explicit gaps, and encrypts Helm exports. Native access and current tool hashes are observations; release authenticity and management procedure compatibility need independent checks. It does not SSH into the node, take an etcd snapshot, alter roles, sign evidence, accept criteria or open a mutation gate.

Current [target/source records](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t175000z-current-pins/pins.json) retain concrete Rancher chart/image candidates, K3s asset identity, license sources and limits. The [retirement](RETIRE-KUBESPHERE.md) and [Rancher](RANCHER.md) runbooks describe remaining qualification and separate 4.27/4.28 execution decisions.
