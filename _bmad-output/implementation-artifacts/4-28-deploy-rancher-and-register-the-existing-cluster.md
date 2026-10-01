---
title: Deploy Rancher and register the existing cluster
type: story
epic: 4
story: 28
created: 2026-10-01
status: backlog
route: dispatch
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/specs/spec-kubesphere-to-rancher/SPEC.md'
depends_on:
  - 4-26-qualify-rancher-and-the-management-migration
  - 4-27-retire-kubesphere-without-changing-workload-data
  - 4-1-upgrade-the-cluster-off-kubernetes-1-34-after-verified-backu
---

# Story 4.28: Deploy Rancher and register the existing cluster

As Administrator, I want private Rancher management of the existing supported cluster with independently tested recovery, so that cluster visibility and administration use the chosen open-source platform without becoming a workload-recovery dependency.

**Repo:** Administrator operations and Platform operations definitions. **Covers:** FR-9, FR-10, NFR-3, AR-59. **Dependencies:** 4.26 target qualification, 4.27 accepted retirement and 4.1 accepted supported workload-cluster upgrade; precedes staging foundations and G1.

Acceptance criteria:

1. Provision the approved dedicated management VM/K3s cluster and deploy the exact qualified Rancher Helm release, required certificates, agents and backup operator from retained identities. Record actual versions, placement, resource use, licensing and owner. Use a Kubernetes installation with declared single-node limitations.
2. Configure the declared private TLS UI/API endpoint, verified trust chain and required agent connectivity. Demonstrate private authorized access and public/unauthorized denial. Inspect the registration manifest and its authority before applying it; do not use an insecure unverified download as the installation procedure.
3. Register the existing kubeadm cluster identity as generic/imported and prove a healthy agent connection and representative read/admin operations. Preserve native maintenance and external-etcd recovery; do not recreate the workload cluster or enable unattended Kubernetes upgrades.
4. Qualify administrator authentication/MFA, narrowly scoped deputy recovery access, token custody/rotation and revocation. Prove staging users, workload/service identities and executor credentials cannot obtain global/cluster-owner authority, production proxy access or usable production credentials through Rancher.
5. Inventory required agent namespaces, service accounts, cluster roles, CRDs and admission endpoints. Bound any necessary system-namespace security exception to those resources. Preserve application namespace restrictions, network policy and executor authority. Do not enable optional app/platform stacks or Fleet application reconciliation as part of registration.
6. Prove existing workload/namespace/PVC preservation and smoke outcomes against the post-upgrade baseline. Re-run affected access checks. Record any new reconcilers and demonstrate that application deployment remains owned by the existing qualified workflow.
7. Create encrypted immutable off-node management backups and separately retain required encryption configuration/keys, TLS material, definitions and management-cluster recovery inputs. Restore into an isolated non-production-connected target and verify the manager's configuration and authorized state. State clearly that this backup does not recover downstream business data or external etcd. Native workload backup coverage continues.
8. Demonstrate direct native administration and a representative authorized maintenance/recovery operation while Rancher is unreachable. Rehearse stale manager/agent fencing and deny a revoked management credential after isolated restore before reconnection. Preserve the deputy's prohibition on approving releases or granting admission.
9. Deliver pinned deployment/upgrade/backup/restore runbooks and actual profile inventory with currency, renewal, monitoring and recovery owners. Initial evidence is required before staging/G1; later integrated FR-9 retention, monitoring and RPO/RTO evidence follows Epic 8.

## Implementation handoff

Administrator approved the [course correction](../planning-artifacts/sprint-change-proposal-2026-10-01.md). This scoped input is backlog, not an executed qualification. Use the [migration spec](../specs/spec-kubesphere-to-rancher/SPEC.md) and [operations handoff](../../eng/cluster-management/README.md). Retain exact identities, sanitized attempt records and encrypted private inventories; require acceptance evidence and applicable exact procedure approvals before mutation or completion. No live inspection, deletion or installation is recorded by this file.
