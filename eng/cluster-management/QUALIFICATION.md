# Story 4.26: management qualification

Qualification remains **in progress**. Production census is read-only, proposed targets are unaccepted, and retirement/upgrade/deployment gates are closed. The [criterion ledger](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t173902z-census/criteria.json) keeps all eight acceptance criteria incomplete. No grants, approvals, role mappings or signed acceptances were manufactured.

## Observed native evidence

The [2026-10-01 census](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t173902z-census/inventory.json) binds explicit context `jpiquot@local` to the direct TLS API `https://192.168.1.30:6443`, Kubernetes `v1.34.9`, and retained kubectl `v1.34.12`. The default workstation kubectl `v1.36.1` exceeds permitted minor skew and was excluded from capture. Native API metadata reports node runtime/topology; effective host kubeadm, external-etcd, virtualization and spare capacity still require the operator's working SSH login. Historical host observations are not current capacity proof.

The completed API capture has 2,594 unique native UIDs, 23 namespaces, 27 PVCs, 28 PVs, 137 CRDs, eight mutating and twelve validating admission configurations. It explicitly discovers non-preferred served CRD versions. The earlier attempt is retained with ten coverage gaps; it is superseded for API coverage, not erased. **This capture is itself superseded:** it predates the malformed-list (G5) fix, so its zero-failure result is not current. The [post-review census](#post-review-observations) finalized `failed-closed` with 70 identity-coverage gaps. Capture is sequential rather than an atomic etcd cut: compare fresh UIDs/resourceVersions before any later action.

`ks-core` chart `1.2.4` / application `v4.2.1` and `ks-console-embed` chart `1.2.0` are deployed. Fifty-three `Extension` and 73 `ExtensionVersion` objects are catalog evidence; one `InstallPlan` is observed. The separate [capability proposal](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t181200z-capability-review/capability-dispositions.json) confirms that plan is Installed and proposes concrete replacements/retirements/preservation for core, console, catalog, IAM/workspace, application catalog and shared DNS/CNI/storage/ingress/identity. Each proposal lists consumers, exact observed members and deletion effects requiring review; none is approved. Catalog availability does not establish installation or unused functionality. [Object entries](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t173902z-census/capabilities.json) retain exact identities, ownership and finalizers. Owner references cover only one dependency mechanism; config references, subjects, admission backends, route consumers and operator decisions need review. Accepted dispositions/deletion effects remain unresolved. The production [allowlist](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t173902z-census/retirement-allowlist.json) therefore contains **zero removal actions** and records exact protected namespace/storage identities.

Raw API lists (including Secrets), kubeadm configuration, Helm manifests/hooks/values and error output are encrypted before filesystem writes. Private attempts are under `~/hexalith-management-evidence/qualification/`; attempt directories are 0700 and files 0600. Historical [export digests](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t173902z-census/encrypted-exports.json) publish plaintext and ciphertext hashes. A published plaintext hash of a short or predictable export can confirm a guessed value offline, so since review loop 2 attempts publish only ciphertext identities and keep plaintext digests in private custody. Independent off-node ciphertext readback/decryption and retention signatures are unverified; a local checksum is insufficient. The Administrator's encryption identity requires an interactive passphrase, so unattended decryption was refused. Historical 4.0/4.1 recovery directories and signed records were not modified.

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

Review loop 2 (2026-10-03) changed the collector and its custody helpers. These changes have unit verification only, and no new census was taken:

- Unlabeled webhook, APIService and CRD-conversion registrations whose backend Service runs in a KubeSphere namespace are classified as management state. Replaying the committed census adds exactly `validator.license.kubesphere.io`, giving 597 capability entries instead of 596.
- A successful credential-free request is recorded as `anonymousReadAllowed: true`, not left as a missing field.
- Published export records carry only ciphertext identities.
- The authority validator hashes each event's content before trusting its declared digest.

The dated 2026-10-01 [target/source records](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t175000z-current-pins/pins.json) retain concrete Rancher chart/image candidates, K3s asset identity, license sources and limits. Despite the `current-pins` attempt name, they are historical candidates: under the 24-hour review rule in `validate_pins` they are stale and need a fresh dated review before any use. The [retirement](RETIRE-KUBESPHERE.md) and [Rancher](RANCHER.md) runbooks describe remaining qualification and separate 4.27/4.28 execution decisions.

The [interim local verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t185518z-closeout/verification.json) retains the earlier artifact digests and actual-chart failure. Core deployment absence is distinguished from 17 Terminating chart CRs and incomplete/unreviewed propagation.

## Post-review observations

The [fresh read-only census](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t195141z-census/inventory.json) captures 2,593 unique UIDs, nine Helm releases across all states and expanded sanitized configuration/route dependencies. Its [capability entries](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t195141z-census/capabilities.json) add 27 previously omitted KubeSphere-marked resources to unresolved review, including preserved namespaces/RBAC. The prior eight-cohort proposal needs these additional owner decisions; nothing becomes a removal action.

The capture finalized `failed-closed` with 70 identity-coverage entries rather than silently dropping objects without native deletion preconditions. Three ComponentStatus records, one node/65 pod metric views and Calico `projectcalico-default-allow` lacked UID or resourceVersion. These were successful native responses; the collector's `invalid-schema` entries reflect its complete deletion-identity requirement. [Targeted identity observations](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t195456z-native-identities/identity-observations.json) retain exact names and absent metadata without projecting values. [ComponentStatus](https://kubernetes.io/docs/reference/kubernetes-api/core/component-status-v1/) is a deprecated cluster-validation view; Calico documents the [default-allow profile](https://docs.tigera.io/calico/latest/network-policy/hosts/kubernetes-nodes). Do not treat these views as UID-addressable retirement objects or infer complete ownership from API request success. Protected namespaces/PV/PVC/storage classes preserve all 80 earlier UIDs and previously projected bindings/properties; newly collected fields have no earlier comparison proof.

All 45 unit tests pass. The [new standalone native fixture](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t195601z-rehearsal/native-result.json) verifies real stale-UID/stale-resourceVersion 409 conflicts, unchanged current content, synthetic protected storage/canary and explicit cleanup. The [post-review verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t200342z-post-review/verification.json) binds current outputs and retained limits. These local checks do not accept independent off-node decryption, signatures, authority, actual KubeSphere retirement or any original criterion.

## Recorded procedure replay

The [fresh standalone result](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t204952z-rehearsal/native-result.json) executes recorded procedure SHA-256 `a78aa7904801e589a9aa14fd0816503b3fcae5de4d128f34916f6c0aaa7f4ee0`, including the malformed-mount guard. All six source/external refusal probes, actual native 409 conflicts, synthetic child-first/named-finalizer deletion, protected identities/storage properties and canary checks pass. [Cleanup](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t204952z-rehearsal/cleanup.json) proves absence of the exact node/network/volume/image aliases and fresh credential file. The 102 encrypted exports use the existing synthetic fixture recipient; they do not establish Administrator key custody, independent decryption or off-node acceptance. [Recorded verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t205700z-current-procedure/verification.json) retains exact procedure attribution. This run repeats no actual KubeSphere runtime and accepts no original criterion.

The earlier `20261001t195601z-rehearsal` run executed procedure SHA-256 `6277bf2ceb290e276405780016968eb83b0a3bd1e36694f35e311262faf94f19`. The subsequent procedure SHA-256 `a78aa7904801e589a9aa14fd0816503b3fcae5de4d128f34916f6c0aaa7f4ee0` added a three-line malformed-mount schema guard. Both exact revisions remain encrypted with their original attribution. The recorded procedure replay above resolves that earlier execution gap; it does not qualify actual KubeSphere retirement.

The 2026-10-02 schema-failure handling revision has SHA-256 `9f84f36382efecec137f2dad936421c12e48e6afbbb956330470e12df169716b` and [48 passing unit cases](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t051917z-resume-verification/verification.json), with no full fixture rerun. The prior standalone execution and failed actual KubeSphere uninstall remain attributed to their original bytes; all eight criteria remain incomplete.

## User-authorized native access follow-up

The user supplied SSH username `jpiquot`, approved creating evidence, confirmed the discovered local key path and requested the host key. The [authorization record](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t205847z-native-access/authorization.json) preserves those statements as collection authorization. It establishes no signed grant, approved deputy scope, MFA result or retirement acceptance.

[Native observations](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t210315z-native-authority/native-access.json) revalidate direct CA-verified access to the same cluster UID with kubectl `v1.34.12` and Kubernetes `v1.34.9`. The authenticated principal is `jpiquot`. Eight native SelfSubjectAccessReview checks allow the sampled reads, deployment changes, binding creation and namespace/PVC deletion. These are policy observations; no grant or persistent resource write was performed. Credential-free requests receive HTTP 403 and an invalid bearer receives HTTP 401 from the private operator network. A distinct authenticated unauthorized principal, public denial, MFA, current independent authority lineage and effective recovery custody remain unverified.

The requested public SSH host key has observed ED25519 fingerprint `SHA256:cC09u9n08cO/+GKfx4TL6xMuFHQUiQXm3UFaF3TWFGw`; independent host-key trust is unverified. Subsequent SSH requires that exact retrieved key. The host rejected `hexalith-kube-c1` for `jpiquot`. Two additional existing Windows SSH identities also failed; the [final diagnostic](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t210432z-ssh-identity-check/host-access.json) verifies that their public keys were offered and not accepted. Temporary operational copies were removed. The [earlier memory-file attempts](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t210126z-host-access/host-access.json) prove no successful login and make no key-offering claim. Effective host kubeadm/etcd, capacity and VM decisions remain unobserved pending an authorized SSH identity.

Sensitive command results are encrypted outside Git to the same recipient as the retained production census. Local integrity does not establish independent decryption, off-node custody or signatures. All original criteria remain incomplete.

## Web-login clarification

On 2026-10-02 the operator clarified that their login is through `kube.hexalith.com` with a Keycloak account and that a kubectl console is available. This is self-reported web/Kubernetes access; it does not establish a Linux username, an authorized SSH key/password, MFA, or independent host access. The `jpiquot` username used in the retained SSH trials remains unconfirmed as the actual Linux account. Historical trials and native Kubernetes observations are unchanged. Obtain the host account and existing authorized access from the server administrator or physical/VM console; do not derive SSH identity or permissions from the Keycloak account. Host qualification remains incomplete.

The [targeted native read](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t054119z-native-node-read/native-node-observation.json) independently revalidates the same source cluster UID and direct CA-verified endpoint using the existing explicit `jpiquot@local` Kubernetes context. Node `node1` reports Ubuntu 24.04.3 LTS, kubelet v1.34.9, 32 CPUs and approximately 126 GiB RAM. The kubeadm ConfigMap configures Kubernetes v1.34.9 and one external-etcd endpoint; configured values establish neither effective host binary versions nor an OS login. Five command outputs, including the local native credential/configuration export, remain encrypted outside Git. API capacity/allocatable and pressure observations establish no hypervisor reservation or safe management-VM capacity. No persistent resource or role changed, and no original criterion is accepted.

The operator next supplied `jpiquot@itaneo.com` as the username. The [retained local-key check](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t062759z-domain-ssh-check/host-access.json) and [two existing Windows-key checks](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t062946z-domain-existing-keys/host-access.json) use that exact login name through `ssh -l`, with the unchanged pinned ED25519 host key. All three public keys were offered and rejected. The latter checks observe the server advertising `publickey,password`; no password authentication was attempted. Failed authentication does not confirm a Linux account or a Keycloak-to-host mapping. Temporary key copies/known-host files were removed; diagnostic outputs remain encrypted outside Git. The next host-access evidence requires an operator-confirmed password login or an existing working authorized key. No host account, key authorization, role or production resource changed; host/MFA/custody qualification remains incomplete.

The operator subsequently supplied a successful password-SSH transcript for `quentindv@192.168.1.30`, with the remote prompt `quentindv@node1` and `whoami` returning `quentindv`. The [operator report](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t065855z-operator-password-login/operator-access.json) records that host-account evidence separately from independent agent authentication, MFA and approved role lineage. A [fresh three-key check](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t065350z-reported-shell-ssh/host-access.json) still rejects the available identities for `quentindv`; it retains the same host key and exact cleanup. The local `hexalith-kube-c1.pub` matches its private key, with fingerprint `SHA256:yYjuKNqAYexlvmjRcq5mBuUYILzW2oijvf4PZHKTNyk`; local key use requires no passphrase. No key was authorized by the agent. Automated host capture still needs a working authorized credential; the operator may instead supply read-only observations manually. The reported password login accepts no management role/MFA/custody or original criterion.

## Authorized host observations, 2026-10-02

The operator subsequently ran `ssh-copy-id` and reported adding one public key for `quentindv`. [Independent key authentication](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t072113z-authorized-host-login/host-access.json) now succeeds as `quentindv` on `node1`, with the unchanged ED25519 host pin. This supersedes the pending SSH credential input. Enrollment was performed by the operator; the agent's subsequent commands were read-only. The OS account, native Kubernetes principal `jpiquot` and web Keycloak account remain distinct identities without an inferred management-role mapping.

[Host facts](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t072401z-host-facts/host-observation.json) retain installed kubeadm v1.34.9 and executable digests from the running kubelet v1.34.9, host containerd v2.3.3 and etcd v3.6.5. [Service/process bindings](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t072631z-host-services/host-services.json) distinguish the native host containerd from a second container-scoped process reporting the same version with different executable bytes. Version equality alone is insufficient binary attribution. Existing noninteractive sudo rights permitted the necessary reads; no permissions were granted or expanded.

[Native datastore reads](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t072944z-native-datastore-host/datastore-host-observation.json) use the existing API-server etcd client certificate and verified TLS endpoint. Runtime environment confirms client/peer certificate authentication. The external datastore has one current member, etcd 3.6.5, no reported status errors or alarms, and equal sampled applied/current raft indexes. Its sampled database size is 62,332,928 bytes. Endpoint status, member list and alarm list are reads; no health-write probe, snapshot, defragmentation or restore occurred. These observations establish neither recovery acceptance nor redundancy.

The host reports no detected virtualization, AMD Threadripper 2950X with 32 logical CPUs/16 cores, AMD-V, loaded KVM modules, approximately 125.66 GiB total/113.84 GiB available RAM and 791.16 GiB available on the root filesystem. QEMU 8.2.2 and libvirt 10.0.0 are installed. Libvirt is inactive with listening activation sockets; no QEMU process or offline system guest definition was observed. Live libvirt queries were deliberately omitted because a connection could activate the daemon, as described in the [libvirt daemon documentation](https://libvirt.org/daemons.html). This is an installed virtualization facility, not a tested guest allocation or a complete reservation census.

[Current native scheduling budget](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t073151z-node-resource-budget/node-resource-budget.json) revalidates the same direct CA-verified cluster/node UIDs. Node1 is Ready without sampled memory/disk/PID pressure. Allocatable is 31.6 CPUs/~118.89 GiB RAM; pod requests total 12.3 CPUs/20,702 MiB, with limits 49.7 CPUs/80,484 MiB. Kubelet reserves only 200m/250Mi each for Kubernetes and system processes, plus a 5% hard memory-eviction threshold. Current utilization and requests leave apparent room, but do not reserve a management VM against future scheduling or shared-storage growth. See the [concrete reservation proposal](RANCHER.md#observed-host-and-reservation-proposal).

All command outputs, including configuration and certificate-path observations, were encrypted before filesystem writes outside Git. Only allowlisted facts, digests and outcomes are projected. Independent custody/decryption/signatures, host trust/MFA, approved ownership/authority and representative retirement remain unaccepted; all eight original criteria and story/sprint remain `in-progress`.

## Native authorization dependency, 2026-10-02

The committed [2026-10-01 census](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t195141z-census/inventory.json) shows that the only binding granting the native principal `jpiquot` is `ClusterRoleBinding/jpiquot-cluster-admin` (roleRef `cluster-admin`, subjects User `jpiquot` and ServiceAccount `kubesphere-system/kubesphere.users.jpiquot`). Its ownerReference is KubeSphere `GlobalRoleBinding/jpiquot-platform-admin`. The local `jpiquot@local` credential is a cluster-CA client certificate with subject `CN=jpiquot`, no group, valid until 2036-07-13. A KubeSphere-managed `Secret/kubesphere-system/kubeconfig-jpiquot` also exists. These facts come from the sanitized census and the local kubeconfig's certificate metadata only. No new production read, grant or change was made.

Authentication does not depend on KubeSphere, but authorization does. Garbage collection removes the binding when the owning GlobalRoleBinding is deleted. The KubeSphere user controller deletes GlobalRoleBindings that carry the user's `iam.kubesphere.io/user-ref` label, as the fixture showed for `admin`. Deleting `User/jpiquot` or `GlobalRoleBinding/jpiquot-platform-admin`, or later cleaning up their CRDs, would therefore revoke the operator's only cluster-admin path. A client certificate also has no revocation path short of rotating the cluster CA. Criterion 5's "independent of KubeSphere" condition is therefore **not met**.

Before 4.27, the Administrator needs to approve, and someone other than the agent needs to make, a KubeSphere-independent native administration binding/credential with its own custody and revocation lineage. Options include a separately held kubeadm admin credential with custody, or a new unowned binding for an approved identity. That change must then be verified by a fresh census showing no KubeSphere ownerReference on the approved path. The dependency-first plan keeps `User/jpiquot`, `GlobalRoleBinding/jpiquot-platform-admin` and `jpiquot-cluster-admin` out of scope.

**kubeadm admin credential, 2026-10-03.** With Administrator approval ([authorization record](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261003t070135z-administrator-authorization/authorization.json)), a [read-only node1 check](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261003t062816z-kubeadm-admin-credential/admin-credential-observation.json) ran as `quentindv` with the unchanged ED25519 host pin. `sudo -n` worked without a password. `/etc/kubernetes/admin.conf` exists, owned root:root with mode 600, and holds one embedded client certificate:

- subject `CN=kubernetes-admin, O=kubeadm:cluster-admins`;
- issuer `CN=kubernetes`;
- valid from 2026-09-24 15:14:32 to 2027-09-24 15:19:32 GMT.

`/etc/kubernetes/super-admin.conf` does not exist. Only certificate metadata was extracted on the host. The key and the file were never printed, copied, transferred or used to authenticate.

The committed census shows ClusterRoleBinding `kubeadm:cluster-admins` (Group `kubeadm:cluster-admins`) and ClusterRoleBinding `cluster-admin` (Group `system:masters`), both bound to `cluster-admin` and neither with an ownerReference. By configuration, this credential's authorization therefore does not depend on KubeSphere.

`kubeadm certs check-expiration` was run under `sudo -n` with a nonexistent `--kubeconfig`, so it could not authenticate with `admin.conf`. Without the cluster's external-etcd configuration it falls back to stacked-etcd defaults and exits 1 on the absent `apiserver-etcd-client.key` ([diagnosis](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261003t062921z-check-expiration-diagnosis/check-expiration-diagnosis.json)). The first record's `fellBackToDefaultLocalConfiguration: false` reflects only a missing log line, and the diagnosis supersedes it. Raw outputs are encrypted to the Administrator recipient (`8XlNQg`).

Criterion 5 remains **unmet**:

- The Administrator has not yet decided who holds this credential or where its encrypted off-node copy lives; no off-node copy is known.
- Working use from that custody is not proven.
- The certificate expires on 2027-09-24, and the CA expiry was not observed.

## Access risk: workstation key reaches root on node1 (high severity)

The workstation key `hexalith-kube-c1` has no passphrase. It is authorized for `quentindv` on node1, and that account's `sudo -n` runs without a password (observed 2026-10-02 and again on 2026-10-03). Anyone who compromises this workstation, or copies that key file, therefore gets non-interactive root on the production node and its external etcd, kubeadm PKI and `admin.conf`. This is an access risk to the production host, not only a weakness in export custody.

Administrator actions, none of which the agent has taken:

1. Add a passphrase to `hexalith-kube-c1` (or replace it with a hardware-backed key).
2. Restrict the authorized key on node1 (for example `from=` source limits and a forced read-only command), or remove it once qualification no longer needs host access.
3. Review whether `quentindv` needs passwordless sudo at all.

No host, key or sudoers change was made.

**Update, 2026-10-03.** At the Administrator's request, the agent added a passphrase to the workstation's private key file `~/.ssh/hexalith-kube-c1`, which completes action 1.
- Verification: an empty passphrase is now rejected, and the new passphrase unlocks the same public key (`SHA256:yYjuKNqAYexlvmjRcq5mBuUYILzW2oijvf4PZHKTNyk`).
- No ssh-agent held the key.
- The Administrator supplied the passphrase in the session conversation, so it should be rotated privately.
- Actions 2 and 3 are still open, and no host or sudoers change was made.
- The original `yYjuKA` exports now need that passphrase to decrypt; nobody has yet decided whether to retire them.

## Private-export recipient observation

Qualification attempts from `20261001t173028z-census` through `20261001t210432z-ssh-identity-check` were encrypted to the original production census recipient. That is SSH recipient tag `8XlNQg`, the Administrator signing public key whose identity needs an interactive passphrase. The exception is `20261001t205700z-current-procedure`, which used the synthetic fixture recipient. The 12 attempts from `20261002t051917z-resume-verification` through `20261002t074902z-host-proposal-verification`, including the encrypted local native credential/configuration export in `20261002t054119z`, use tag `yYjuKA`. That tag is the workstation's `hexalith-kube-c1` key. Its private half had no passphrase when those exports were written (one was added on 2026-10-03; see the [access-risk update](#access-risk-workstation-key-reaches-root-on-node1-high-severity)), and the key authenticates to node1. Those exports are not readable by Git viewers, but they give no protection against compromise of this workstation and do not represent Administrator custody. Historical attempts are immutable, and their original files are unchanged. The [2026-10-02 dependency-first verification](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261002t125604z-dependency-first-verification/verification.json) returns to the Administrator recipient.

**Re-encryption, 2026-10-03.** With Administrator approval ([authorization record](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261003t070135z-administrator-authorization/authorization.json)), all 33 private exports of the 12 affected attempts were streamed from `age --decrypt` (identity `hexalith-kube-c1`) into `age --encrypt` (recipient `8XlNQg`). The output went into the new immutable private attempt `20261003t063011z-recipient-reencryption`, and no plaintext was written to disk. The [manifest](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261003t063011z-recipient-reencryption/reencryption-manifest.json) maps each original path and ciphertext SHA-256 to the new ciphertext SHA-256 and size, and publishes no plaintext digests.

- There are 33 new files, and every header carries tag `8XlNQg`.
- Each decrypted stream matched its original export record.
- The originals were not modified, moved or deleted, and their `SHA256SUMS` still verify.

Administrator readback is pending because the Administrator identity needs an interactive passphrase. The originals stay decryptable with the workstation key, which now requires its passphrase, until the Administrator decides whether to retire the original ciphertexts.
