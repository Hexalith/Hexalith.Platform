---
title: Qualify Rancher and the management migration
type: story
epic: 4
story: 26
created: 2026-10-01
status: backlog
route: dispatch
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/specs/spec-kubesphere-to-rancher/SPEC.md'
depends_on: []
---

# Story 4.26: Qualify Rancher and the management migration

As Administrator, I want an exact supported Rancher target, independent native access and an ownership-based migration plan, so that replacing management does not change workload data or silently expand authority.

**Repo:** Administrator operations and Platform planning. **Covers:** FR-9, FR-10, NFR-3, AR-59, AR-63. **Dependencies:** none for read-only preparation; current qualifying recovery evidence is required before any production mutation.

Acceptance criteria:

1. Record the live workload-cluster identity/topology, effective kubeadm/client versions, KubeSphere chart/images/configuration, extensions, CRDs and instances, owners/finalizers, API services, webhooks, RBAC, routes, namespaces and native persistent-state inventory. Retain sensitive exports encrypted outside Git.
2. Classify every KubeSphere-managed capability as unused/retired, native-owned or replaced. Identify all consumers and approved replacements. Unknown ownership, deletion effects or required functionality blocks retirement.
3. Select exact Rancher community chart/image identities, required dependency licenses and a security-current version pairing for the management cluster and target imported workload cluster. Retain dated version-specific matrices and release notes. Qualify the management installation Helm CLI and native recovery-tool identities separately from the application executor tool floor. No unqualified tag or claim that a generic import certifies manager hosting is accepted.
4. Specify the dedicated K3s VM's host, supported OS, CPU/RAM/storage budget, private DNS/TLS endpoint, route/firewall/CA requirements, independent backups and owner. Record costs, provisioning dependencies and single-node/shared-host limitations. Actual provisioning and console rollout may follow the workload upgrade.
5. Independently demonstrate authorized native Kubernetes administration and deny unauthorized/public access. Retain recovery kubeconfig/key custody independent of KubeSphere and Rancher. Do not substitute Rancher-proxy credentials for this path.
6. Map required operator accounts/roles to approved native/Rancher authority. Preserve the Administrator/deputy division, MFA requirement and least privilege; no automatic workspace-to-project, account-to-admin or staging-membership mapping is accepted. Define Administrator-approved management grant/revocation records and their independent off-site custody/complete lineage so restored authority can be reconciled after the cut; missing or conflicting authority fails closed.
7. Rehearse the version-specific uninstall against a representative isolated inventory. Produce an exact proposed resource allowlist, ownership/deletion-propagation result, dependency order, workload-preservation assertions and stop/recovery procedure. Demonstrate that licensed KubeSphere application writes are not needed to complete the qualified native retirement procedure.
8. Deliver a separately reviewable retirement runbook, revised upgrade ordering and 4.28 installation/access/backup plan. Qualification evidence alone neither authorizes live deletion nor clears Story 4.1. Story 4.26's retirement deliverable may complete while management-VM procurement remains scheduled for 4.28.

## Implementation handoff

Administrator approved the [course correction](../planning-artifacts/sprint-change-proposal-2026-10-01.md). This scoped input is backlog, not an executed qualification. Use the [migration spec](../specs/spec-kubesphere-to-rancher/SPEC.md) and [operations handoff](../../eng/cluster-management/README.md). Retain exact identities, sanitized attempt records and encrypted private inventories; require acceptance evidence and applicable exact procedure approvals before mutation or completion. No live inspection, deletion or installation is recorded by this file.
