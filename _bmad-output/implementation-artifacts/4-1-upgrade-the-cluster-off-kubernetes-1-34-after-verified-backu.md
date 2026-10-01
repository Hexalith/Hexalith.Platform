---
title: 'Upgrade the cluster off Kubernetes 1.34 after verified backups'
type: 'story'
epic: 4
story: 1
created: '2026-09-28'
status: 'in-progress'
baseline_commit: '7c2f0f89f29c79f5d7ab4b731155e2cb07fc690f'
route: 'dispatch'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/evidence/epic-4/initial-cluster-inventory.md'
depends_on:
  - '4-0-prove-off-node-backups-and-isolated-restores'
  - '4-27-retire-kubesphere-without-changing-workload-data'
---

# Story 4.1: Upgrade the cluster off Kubernetes 1.34 after verified backups

As Administrator,
I want the designated cluster on a supported Kubernetes minor before staging exists,
So that the single-node outage occurs before new workloads depend on it and before Kubernetes 1.34 reaches end of life on 2026-10-27.

## Scope and current state

This story owns upgrade preparation, the sequential kubeadm mutation and post-upgrade verification. Story 4.0 exclusively owns the off-node backup and isolated-restore proofs.

At 2026-09-28T12:51:19Z, context `jpiquot@local` served Kubernetes v1.34.9 from control-plane/worker `node1` on Ubuntu 24.04.3 LTS with containerd 2.3.3. Kubeadm configuration names external etcd at `192.168.1.30:2379`. Keycloak, OpenBao, Memories and Forgejo all run on `node1`, so the upgrade is a planned full-workload outage.

Current supported branches are 1.35–1.37. Resolve each exact current patch and package/image digest immediately before the maintenance window; do not freeze a stale patch in this artifact. Traverse minor versions one at a time.

## Mutation gate

Preparation may start immediately, but no `kubeadm upgrade`, node drain, package mutation or reboot may occur until all conditions below pass:

1. Story `4-0-prove-off-node-backups-and-isolated-restores` is `done` in `sprint-status.yaml`.
2. `backup-gate.json`, all three Keycloak, OpenBao and Memories restore proofs, and the passing final Story 4.0 `validation/validation.json` have valid Administrator-approved signatures. Proof/gate timestamps are unexpired and final validation is fresh under the signed recovery policy. Final validation is bound to the exact backup-gate and evidence-manifest SHA-256, recovery ID and policy SHA-256; readable immutable off-node objects have matching checksums. A missing, stale, unsigned, failed or differently bound final validation closes the gate even when sprint status says `done`.
3. The maintenance window and single-node outage are approved, incident command and rollback owners are present, and all workload owners acknowledge the outage.
4. A fresh external-etcd snapshot for `192.168.1.30:2379` passes integrity/status checks and an isolated restore rehearsal. The restored target must reach member health and match the signed source revision, key-count/hash and approved non-secret canaries; credentials and key material are not recorded in Git.
5. An encrypted, access-controlled, off-node recovery bundle covers node-local kubeadm configuration, static-pod manifests, PKI/certificate/key material, package/repository state and kubelet configuration with owner/mode-preserving checksums and a tested recovery procedure.
6. Kubeadm's live preflight/plan reports a valid sequential path and the exact target patch remains supported; deprecated-API/resource scans and the compatibility matrix for CNI, CoreDNS, kube-proxy, CSI/storage, CRDs, admission webhooks and installed operators/add-ons show no blocker for that hop.
7. A disposable PVC/pod probe on every required dynamic `StorageClass` has provisioned, written, read and cleaned up successfully immediately before the hop.

Any failed condition closes the gate. Preparation evidence is not mutation authorization.

## Ordered tasks

- [x] Capture the sanitized baseline inventory and identify the single-node outage boundary.
- [x] Create the access-controlled attempt directory `evidence/epic-4/4-1/<attempt-id>/` and record operator, approver, window, incident channel, source version, intended minor hops and evidence hashes.
- [x] Validate the Story 4.0 gate exactly as described above and retain a signed validation result bound to this attempt ID.
- [ ] Prepare the upgrade without mutation:
  - [x] Record control-plane, kubelet, kubeadm, kubectl, container runtime, CNI, kube-proxy and API versions; capture node/PVC/workload health without Secret data.
  - [x] Resolve the Kubernetes release notes and kubeadm binary/image identities for the candidate 1.34 → 1.35 hop. Record the exact current patch, upstream source and digest/version; refresh at the actual hop.
  - [x] Run kubeadm planning/preflight appropriate to the current hop; record warnings and blockers. Never bypass a failed preflight.
  - [x] Confirm external etcd health, integrity-check a fresh snapshot, restore it into an isolated target, and record matching revision/key-count/hash/canary evidence plus signed cleanup of the target.
  - [x] Hash and transfer the node-local kubeadm/static-pod/PKI/kubelet/package recovery bundle to the approved encrypted off-node store, then independently read back and checksum it without exposing private material.
  - [ ] Inventory live/desired API versions and run deprecated/removed-API scans. Record a complete per-hop compatibility result for Calico/CNI, CoreDNS, kube-proxy, OpenEBS/storage, CloudNativePG, Dapr, Traefik, cert-manager and every installed CRD operator/admission webhook. Partial version-presence checks do not clear this task.
  - [x] Run the disposable dynamic-storage provision/write/read/delete probe against each required `StorageClass` and prove provider volumes are cleaned up; refresh immediately before any actual hop.
  - [ ] Record rollback/stop decisions under the accountable recovery procedure. Kubeadm downgrade is not a rollback; recovery means stopping, preserving diagnostics and following the approved control-plane/etcd/workload recovery procedures.
- [ ] Record pre-outage health for concrete workloads: `Cluster/keycloak-postgres` and Keycloak pods in `keycloak`; `StatefulSet/hexalith-keys` in `openbao`; `StatefulSet/redis-stack`, `StatefulSet/falkordb`, Memories deployments and `StatefulSet/access-telemetry-postgresql` in `hexalith-memories`; Forgejo and `Deployment/forgejo-runner` in their namespaces.
- [ ] Before every minor hop, sign a new per-hop mutation gate that revalidates Story 4.0 proof and final-validation signature/freshness/bindings, the fresh isolated-etcd-restore result, node recovery bundle, exact target patch, deprecated-API/add-on compatibility, dynamic-storage proof, workload health, owners and outage approval.
- [ ] After the per-hop mutation gate is signed open, quiesce applications with their approved procedures and require every tracked workflow terminal; cordon `node1` and observe `spec.unschedulable=true`; drain it to terminal success with only explicitly approved static-pod/DaemonSet/local-data exceptions recorded. Do not begin kubeadm while quiesce, cordon or drain is pending, failed or ambiguous.
- [ ] For that hop, upgrade the control plane first, then kubelet/kubectl packages as required; restart only the components called for by the selected Kubernetes instructions. Record every command version, exit result and resulting component version without credentials.
- [ ] Require API readiness, `node1` Ready, CNI/add-on/operator health, deprecated-API scan clearance, external-etcd health, version-skew compliance and a fresh dynamic-storage provision/write/read/delete proof before uncordoning. If another minor hop is required, repeat planning and the full gate for the next current patch; never skip a minor.
- [ ] Restore service and verify the pre-outage workload set:
  1. Keycloak deployment and `Cluster/keycloak-postgres` ready, login/OIDC smoke checks pass, and database health matches baseline.
  2. OpenBao raft peers healthy/unsealed through the custodian workflow and an approved read/write canary passes without exposing values.
  3. Memories Redis/FalkorDB persistence health, Memories API/MCP health and representative search checks pass; use Story 4.0 recovery only through the approved restore procedure if needed.
  4. Forgejo repository/UI health and runner status match the pre-outage expectation.
- [ ] Record final server, node, kubeadm, kubelet, kubectl, runtime and workload versions plus the complete health/restore outcome in `upgrade-result.json`; sign the result and a sanitized summary.

## Evidence outputs

The attempt must produce signed or checksummed records for `gate-validation.json`, one `hop-gate-<from>-to-<to>.json` per hop, `preflight.json`, `external-etcd-recovery-point.json`, `external-etcd-isolated-restore.json`, `node-recovery-bundle.json`, `api-addon-compatibility.json`, `dynamic-storage-proof.json`, `maintenance-transition.json`, `pre-upgrade-health.json`, one `minor-hop-<from>-to-<to>.json` per hop, `post-upgrade-health.json` and `upgrade-result.json`. Full logs and node/PKI recovery material remain encrypted and access controlled. The committed summary contains no kubeconfig, certificates, private keys, tokens, Secret values, database rows or OpenBao data.

## Stop conditions

- Stop before mutation if any Story 4.0 proof or its final validation is absent, stale, unsigned, unreadable, checksum-invalid, differently bound or failed.
- Stop if the exact target patch is unsupported, version skew is invalid, a kubeadm preflight fails, isolated etcd restore/integrity evidence or the node recovery bundle is incomplete, any deprecated API/add-on is incompatible, the functional storage probe fails, external etcd is unhealthy, the outage lacks approval, or an accountable owner is unavailable.
- Stop unless quiesce, cordon and drain have each reached and recorded their defined terminal-success state.
- Stop after any hop if the API, node, CNI, DNS, storage, external etcd or a protected workload is unhealthy. Do not begin another minor hop.
- Do not improvise a kubeadm downgrade or replace a PVC. Preserve diagnostics and invoke the approved recovery path.
- Do not claim completion until actual supported versions and every workload outcome are recorded.

## Acceptance criteria

**Given** a request to mutate the cluster
**When** Story 4.0 is not done or any signed restore proof fails validation
**Then** no kubeadm mutation, drain or reboot occurs

**Given** the v1.34.9 single-node cluster and a signed-open mutation gate
**When** it is upgraded
**Then** each minor is traversed sequentially to a supported minor at the current patch
**And** each hop starts only after terminal quiesce/cordon/drain plus isolated-etcd-restore, node-recovery-bundle, add-on/API compatibility and dynamic-storage gates pass
**And** the actual component versions and evidence are recorded

**Given** Keycloak, OpenBao, Memories and Forgejo existed before the outage
**When** the node returns
**Then** each is verified healthy or restored through its approved recovery procedure
**And** each outcome is included in the signed upgrade result

## Preparation record — 2026-10-01

Local-only preparation is retained in attempt `20261001t060500z-preparation`; see the [sanitized attempt summary](evidence/epic-4/4-1/20261001t060500z-preparation/summary.md) and [maintenance handoff](../../eng/kubernetes-upgrade/README.md). The diagnostic recorder creates owner-only, checksummed preparation records outside Git and cannot open a mutation gate. During that phase, the recovery rehearsal was inventoried without modification; no cluster, SSH, provider, package, service or storage operation was performed, and no Administrator signature was created.

At that preparation stage, Story 4.0 was `in-progress`. Its rehearsal had no signed final backup gate or final validation, and its three proof signatures/freshness did not establish an open gate. The actual live source version, maintenance approval and accountable owners, fresh isolated-etcd restore, encrypted node recovery bundle, current target patch/package/image identities, API/add-on compatibility, functional storage probes and fresh workload health were outstanding. Upgrade quiesce, cordon, drain, all upgrade hops and post-upgrade checks were `not-run`.

The mutation gate also requires the passing, signed final Story 4.0 validation with exact backup-gate/manifest/policy/recovery-ID bindings, resolving deferred review finding B3 for this story. Preparation checks cannot replace that signed operational validation.

## Immediate maintenance request — 2026-10-01

The Administrator requested the window start `now`, observed at approximately `2026-10-01T06:15:14Z` (08:15 Europe/Paris). Started the prerequisite final Story 4.0 run `20261001t061620z`: copied and verified the staged tools, passed the live destination precheck, and prepared/uploaded the unsigned recovery policy. At this initial snapshot, the required policy signature was pending and no capture, isolated restore or upgrade had started. End time, incident/recovery owners and workload acknowledgements remained unrecorded.

A fresh read-only baseline using a separately downloaded, upstream-checksummed v1.34.12 client confirms the API/kubelet v1.34.9, `node1` Ready and schedulable, all 16 running protected pods Ready, five completed job pods and 17 protected PVCs Bound. Native health, smoke tests, kubeadm preflight, external-etcd restore, node recovery, compatibility and functional storage gates remain outstanding. See the [maintenance-request evidence](evidence/epic-4/4-1/20261001t061620z-maintenance-request/summary.md). The mutation gate remains closed and the story remains `in-progress`.

## Fresh backup prerequisite result — 2026-10-01

The Administrator explicitly delegated signing after the initial maintenance-request snapshot. Final Story 4.0 run `20261001t061620z` now has signed passing policy, cleanup, manifest, Keycloak/OpenBao/Memories proofs, backup gate and final validation. The validator independently read all 553 manifest object versions: 648 automated checks and eight manual checks passed, with zero failed/skipped checks. All eight required signed records and signatures were uploaded and cryptographically verified on readback, and final validation binds the exact gate/manifest/policy digests and recovery ID. Story 4.0 is now `done`; see its [sanitized final evidence](evidence/epic-4/4-0/20261001t061620z/summary.md). The backup gate expires at `2026-10-02T06:16:52Z` and must be revalidated immediately before a hop.

After the backup run, all 16 running protected pods are Ready, five job pods are Succeeded and all 17 protected PVCs are Bound. Memories intake resumed after successful restore/isolation/drain checks. The API and kubelet remain v1.34.9, and `node1` remains Ready and schedulable.

At that backup-only stage, the remaining required inputs were the node1 SSH target/login and trusted access, approved external-etcd/node recovery procedure and accountable recovery owner, window end/incident channel, named present incident commander and rollback owner, and workload-owner outage acknowledgements. External-etcd snapshot/isolated restore, encrypted node recovery, kubeadm planning, compatibility and storage probes had not yet run. The subsequent preparation results below supersede those missing-access and unperformed-preparation observations. No upgrade operation has run; Story 4.1 remains `in-progress`.

Operational attempt `20261001t075120z-backup-verified` now retains signed `gate-validation.json` bound to this attempt. It directly verifies local/uploaded signatures, immutable encrypted record readback, exact final-validation input hashes, recovery ID, policy/manifest/gate digests and proof/gate/validation freshness. All 12 backup-prerequisite checks passed; the record and signature were uploaded and verified on readback. See the [signed backup-prerequisite summary](evidence/epic-4/4-1/20261001t075120z-backup-verified/summary.md). This completes the backup-validation task only: pending maintenance metadata and all other hop prerequisites keep mutation unauthorized.

## Verified recovery and storage preparation — 2026-10-01

Under the Administrator's delegated request, existing root SSH access to node1 was discovered and tested with its trusted host key without changing node authentication. Preparation remains bound to operational attempt `20261001t075120z-backup-verified`. Its new records have 12 Administrator signatures and 43 immutable evidence object versions, all cryptographically verified on independent validator readback. Two additional AGE-encrypted recovery archives passed independent immutable readback and decryption. See the [sanitized recovery/preflight evidence](evidence/epic-4/4-1/20261001t095344z-recovery-preparation/summary.md) and its exact [hash/version ledger](evidence/epic-4/4-1/20261001t095344z-recovery-preparation/evidence-hashes.json).

The isolated node file-recovery rehearsal passed content, numeric owner/group and mode checks for all 128 entries, eight certificate/key pairs and SSH access using the independently recovered key. It tests file/key recovery, not complete production node reconstruction. The external-etcd 3.6.5 snapshot passed integrity checking and restored onto a distinct healthy member with no network path to the source. Source and target matched revision `25283476`, 2,495 keys, MVCC hash `3992780285` and all three non-secret canaries. Both isolated targets and the temporary node capture/binary directories were removed. Source-node entries remained unchanged. Production recovery still requires its accountable procedure, fencing and a suitable revision bump/watch-cache invalidation decision.

Installed kubeadm v1.34.9 and the independently checksummed standalone v1.35.9 binary both passed the target plan. A first standalone execution under noexec `/run` failed with exit 126; its evidence is retained alongside the successful executable-location retry. Installed binaries/packages were not replaced and preflight errors were not skipped. Exact target binary sources/checksums and seven official image identities are retained. The NodeLocal DNS warning is explained by the existing topology, with successful actual service-name resolution.

Both `openebs-hostpath` and `openebs-hostpath-retain` passed scheduler-managed provision/write/independent-read/delete probes and direct provider-directory absence checks. The Retain probe verified retention before changing only its UID-bound disposable Released PV to Delete for normal cleanup. Both source StorageClasses and all 17 protected PVC identities/bindings remained unchanged. The namespace, claims, PVs and provider directories are absent; fresh probes remain mandatory immediately before and after an actual hop.

API presence/version checks passed for all 87 Helm references, all 20 admission configurations accepting v1, and all 137 CRDs' stored/storage version definitions. The signed compatibility record is nevertheless `incomplete`: full field/schema/non-Helm desired-resource and target-runtime clearance is not established. Exact KubeSphere 4.2.1 compatibility with 1.35 remains unproven, and other component/resource checks remain pending. Retrieved older-version support tables are not applied to the installed version.

Closeout at `2026-10-01T09:50:05Z` confirmed API/kubelet v1.34.9, node1 Ready and schedulable, all 16 running protected pods Ready, five completed job pods, all 17 protected PVCs Bound with unchanged identities/bindings, and healthy external etcd. Partial native health passed for CNPG, all three unsealed OpenBao voters, Forgejo health/version/UI and Keycloak OIDC discovery. Complete workload smoke coverage remains pending. Temporary plaintext recovery files, copied access keys and the temporary signing agent were removed; encrypted recovery material remains retained off-node.

The gate remains closed: full API/add-on compatibility, complete workload smoke expectations, accountable incident/recovery owners, window end/incident channel and all workload-owner acknowledgements are still unrecorded. No signed-open hop gate, upgrade quiesce, cordon, drain, installed package/binary mutation, reboot, kubeadm apply or post-upgrade result exists. Story 4.1 remains `in-progress` and every prerequisite must be revalidated for freshness before any actual hop.

## Approved metadata and compatibility continuation — 2026-10-01

The Administrator confirmed present incident/recovery/outage ownership for Keycloak, OpenBao, Memories, Forgejo and its runner, acknowledging the outage, and approved immediate start with this conversation as incident channel and end `2026-10-01T14:55:00+02:00` (`12:55:00Z`). Append-only attempt metadata now records operator, approver, source, candidate minor hop and evidence hashes. The [continuation evidence](evidence/epic-4/4-1/20261001t114842z-maintenance-continuation/summary.md) preserves the exact quoted authorizations. This supersedes the earlier missing-owner/window observations for those five services; it does not approve the additional discovered workload outage scope or the concrete [maintenance/recovery proposal](../../eng/kubernetes-upgrade/MAINTENANCE.md).

At `2026-10-01T11:44:19Z`, all eight local Story 4.0 signatures and exact final-validation input/recovery/gate/manifest/policy bindings passed again. Final validation was 14,707 seconds old against the conservative minimum signed proof-age bound of 86,400 seconds; the earlier proof/gate expiry remains `2026-10-02T06:16:52Z`. No separate final-validation TTL is defined by the policy. No new remote readback or signature was produced in this continuation. All 12 earlier signed preparation records remain byte-for-byte unchanged.

The extended structural scan checked 2,340 live documents, 378 last-applied documents and 283 non-sensitive Helm manifest documents. Its 28 findings also occur against source schemas, but target semantic handling, 19 Go regex constraints, sensitive/configuration documents, hooks, other served-version clients, complete external desired sources and full installed operator/admission runtime clearance remain incomplete. The exact KubeSphere 4.2.1 primary upgrade guide limits Kubernetes support to 1.23.x–1.34.x.

A fully isolated, disposable fixture actually served API/kubelet v1.35.9 and ran all five current KubeSphere component image contents. Fresh admin login, API/UI/listing, service DNS, synthetic native target admission/controller/workload tests and cleanup passed. Licensed KubeSphere writes returned `403` with an empty-license denial, so this narrower rehearsal does not establish full compatibility or override the documented support range. No production license, credential or data entered the fixture. Its container, network, volumes, custom node images and fresh credential files are absent; logs remain encrypted outside Git.

Fresh native checks passed Keycloak machine-client OIDC signature/issuer/expiry, CNPG streaming replication, Memories HTTP/persistence/telemetry-database checks and three matching unsealed OpenBao statuses. Memories still has the signed zero-tenant baseline and lacks an approved representative authenticated search expectation. Forgejo's native database consistency check reports **five orphaned objects despite exit 0**; an append-only correction records incomplete consistency without fixing data. Authenticated custodian/canary, complete login/search/repository/runner expectations and job census remain pending.

Production closeout at `2026-10-01T11:44:20Z` confirms API/kubelet v1.34.9, node1 Ready/schedulable, all 16 running protected pods Ready, five completed job pods and all 17 protected PVC identities/bindings unchanged and Bound. Full compatibility, complete workload smoke coverage and approval of the additional outage scope/accountable stop/recovery procedure remain unresolved; fresh per-hop evidence and a signed-open gate are still required. The tested bundle recovery scope is file/key restoration; full OS reconstruction/replacement capacity is a conditional disaster-recovery limitation, not an added Story 4.1 gate. No production outage or upgrade step occurred, and Story 4.1 remains `in-progress`.

## Maintenance end-time amendment — 2026-10-01

The Administrator corrected the maintenance end with “end time is tomorrow 2pm.” The effective end is **2026-10-02 14:00 Europe/Paris** (`2026-10-02T12:00:00Z`). The immediate start, this conversation as incident channel and acknowledged roles remain recorded; the prior end-time record is preserved and superseded by the [amendment metadata](evidence/epic-4/4-1/20261001t114842z-maintenance-continuation/maintenance-metadata.json). The existing backup gate expires at **2026-10-02 08:16:52 Europe/Paris** (`06:16:52Z`), before the extended window ends. A hop at or after expiry requires fresh signed backup/restore validation, and all other technical gates remain required. No production mutation is authorized by this time amendment; the story remains `in-progress`.

## Approved course correction — 2026-10-01

Administrator approved the [complete Rancher proposal](../planning-artifacts/sprint-change-proposal-2026-10-01.md) with “I approve.” The [migration spec](../specs/spec-kubesphere-to-rancher/SPEC.md) supersedes retaining/licensing KubeSphere as the upgrade path: 4.26 qualifies retirement, 4.27 retires it, this story performs the supported native hop, then 4.28 qualifies private Rancher. Historical observations and signed preparation records above remain unchanged.

**Given** Story 4.27's signed retirement and workload-preservation result
**When** the upgrade mutation gate is evaluated
**Then** KubeSphere's runtime, owned blocking admission dependencies and public route are absent, and any retained/replaced dependency has a named qualified owner
**And** a fresh post-retirement external-etcd recovery point and matching node/configuration inventory are independently verified
**And** the complete remaining API/add-on/admission compatibility assessment, source workload consistency findings, authenticated smoke expectations and exact revised maintenance procedure are cleared for the selected supported target patch
**And** removing KubeSphere alone never opens the hop gate or extends evidence validity.

- [ ] Require accepted 4.27 signed retirement/preservation and private native-access proof.
- [ ] Obtain fresh post-retirement etcd and matching node/configuration recovery evidence; revalidate 4.0 proofs and independent remote readability.
- [ ] Complete remaining API/operator/admission checks, Forgejo consistency disposition and authenticated workload expectations.
- [ ] Revise the exact maintenance procedure from the remaining controller/admission/outage census; preserve its old digest and bind exact revised procedure/outage approval and signed hop gate.

Status stays `in-progress`; planning approval neither opens the gate nor extends the 2026-10-02T06:16:52Z backup expiry. The disclosed full-OS reconstruction limitation is not added as a new immediate prerequisite.
