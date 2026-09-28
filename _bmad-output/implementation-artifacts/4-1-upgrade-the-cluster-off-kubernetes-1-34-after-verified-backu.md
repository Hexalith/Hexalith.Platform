---
title: 'Upgrade the cluster off Kubernetes 1.34 after verified backups'
type: 'story'
epic: 4
story: 1
created: '2026-09-28'
status: 'in-progress'
route: 'dispatch'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/evidence/epic-4/initial-cluster-inventory.md'
depends_on:
  - '4-0-prove-off-node-backups-and-isolated-restores'
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
2. `backup-gate.json` and all three Keycloak, OpenBao and Memories restore proofs have valid Administrator-approved signatures, unexpired timestamps, readable immutable off-node objects and matching checksums.
3. The maintenance window and single-node outage are approved, incident command and rollback owners are present, and all workload owners acknowledge the outage.
4. A fresh external-etcd snapshot for `192.168.1.30:2379` passes integrity/status checks and an isolated restore rehearsal. The restored target must reach member health and match the signed source revision, key-count/hash and approved non-secret canaries; credentials and key material are not recorded in Git.
5. An encrypted, access-controlled, off-node recovery bundle covers node-local kubeadm configuration, static-pod manifests, PKI/certificate/key material, package/repository state and kubelet configuration with owner/mode-preserving checksums and a tested recovery procedure.
6. Kubeadm's live preflight/plan reports a valid sequential path and the exact target patch remains supported; deprecated-API/resource scans and the compatibility matrix for CNI, CoreDNS, kube-proxy, CSI/storage, CRDs, admission webhooks and installed operators/add-ons show no blocker for that hop.
7. A disposable PVC/pod probe on every required dynamic `StorageClass` has provisioned, written, read and cleaned up successfully immediately before the hop.

Any failed condition closes the gate. Preparation evidence is not mutation authorization.

## Ordered tasks

- [x] Capture the sanitized baseline inventory and identify the single-node outage boundary.
- [ ] Create the access-controlled attempt directory `evidence/epic-4/4-1/<attempt-id>/` and record operator, approver, window, incident channel, source version, intended minor hops and evidence hashes.
- [ ] Validate the Story 4.0 gate exactly as described above and retain a signed validation result bound to this attempt ID.
- [ ] Prepare the upgrade without mutation:
  1. Record control-plane, kubelet, kubeadm, kubectl, container runtime, CNI, kube-proxy and API versions; capture node/PVC/workload health without Secret data.
  2. Resolve the Kubernetes release notes and kubeadm package/image compatibility for each required minor hop. Record the exact current patch, repository source and digest/version at window start.
  3. Run kubeadm planning/preflight appropriate to the current hop; record warnings and blockers. Never bypass a failed preflight.
  4. Confirm external etcd health, integrity-check a fresh snapshot, restore it into an isolated target, and record matching revision/key-count/hash/canary evidence plus signed cleanup of the target.
  5. Hash and transfer the node-local kubeadm/static-pod/PKI/kubelet/package recovery bundle to the approved encrypted off-node store, then independently read back and checksum it without exposing private material.
  6. Inventory live/desired API versions and run deprecated/removed-API scans. Record a per-hop compatibility result for Calico/CNI, CoreDNS, kube-proxy, OpenEBS/storage, CloudNativePG, Dapr, Traefik, cert-manager and every installed CRD operator/admission webhook.
  7. Run the disposable dynamic-storage provision/write/read/delete probe against each required `StorageClass` and prove provider volumes are cleaned up.
  8. Record rollback/stop decisions. Kubeadm downgrade is not a rollback; recovery means stopping, preserving diagnostics and following the approved control-plane/etcd/workload recovery procedures.
- [ ] Record pre-outage health for concrete workloads: `Cluster/keycloak-postgres` and Keycloak pods in `keycloak`; `StatefulSet/hexalith-keys` in `openbao`; `StatefulSet/redis-stack`, `StatefulSet/falkordb`, Memories deployments and `StatefulSet/access-telemetry-postgresql` in `hexalith-memories`; Forgejo and `Deployment/forgejo-runner` in their namespaces.
- [ ] Before every minor hop, sign a new per-hop mutation gate that revalidates Story 4.0 proof freshness, the fresh isolated-etcd-restore result, node recovery bundle, exact target patch, deprecated-API/add-on compatibility, dynamic-storage proof, workload health, owners and outage approval.
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

- Stop before mutation if any Story 4.0 proof is absent, stale, unsigned, unreadable, checksum-invalid or failed.
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
