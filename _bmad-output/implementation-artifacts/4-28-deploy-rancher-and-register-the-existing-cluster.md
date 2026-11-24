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
4. Qualify the Administrator's Rancher authentication/MFA and token custody, rotation and revocation. Verify that the Administrator is the sole holder of native cluster-admin and Rancher admin authority and that deputy authority is absent until a deputy is named. The 2026-10-03 sole-Administrator decision supersedes the earlier migration specification's scoped deputy requirement. A later named deputy requires separate approval of narrowly scoped recovery access and negative grant and global-role tests. Prove staging users, workload/service identities and executor credentials cannot obtain global/cluster-owner authority, production proxy access or usable production credentials through Rancher.
5. Inventory required agent namespaces, service accounts, cluster roles, CRDs and admission endpoints. Bound any necessary system-namespace security exception to those resources. Preserve application namespace restrictions, network policy and executor authority. Do not enable optional app/platform stacks or Fleet application reconciliation as part of registration.
6. Prove existing workload/namespace/PVC preservation and smoke outcomes against the post-upgrade baseline. Re-run affected access checks. Record any new reconcilers and demonstrate that application deployment remains owned by the existing qualified workflow.
7. Create encrypted immutable off-node management backups and separately retain required encryption configuration/keys, TLS material, definitions and management-cluster recovery inputs. Restore into an isolated non-production-connected target and verify the manager's configuration and authorized state. State clearly that this backup does not recover downstream business data or external etcd. Native workload backup coverage continues.
8. Demonstrate direct native administration and a representative authorized maintenance/recovery operation while Rancher is unreachable. Rehearse stale manager/agent fencing and deny a revoked management credential after isolated restore before reconnection. Verify that isolated restore grants no deputy authority. Any later approved deputy recovery scope prohibits approving releases or granting admission and must remain unchanged by restore.
9. Deliver pinned deployment/upgrade/backup/restore runbooks and actual profile inventory with currency, renewal, monitoring and recovery owners. Initial evidence is required before staging/G1; later integrated FR-9 retention, monitoring and RPO/RTO evidence follows Epic 8.
10. Before installing or registering Rancher, the Administrator owns and records publisher-signature verification for the exact K3s and Helm artifacts and a digest-bound SBOM and vulnerability scan for each deployed image, including agents and add-ons. Missing or failed evidence blocks execution until resolved or covered by an explicit Administrator decision for the exact digest.
11. Qualify the named one-year Administrator native client certificate with a unique, never-reused CN and no organization (O) group, including no `system:masters` or `kubeadm:cluster-admins` grant. Verify its own unowned cluster-admin ClusterRoleBinding supplies its sole administrative grant and that deleting that binding revokes the grant.
12. Before removing the retained `jpiquot` path, read back the final ClusterRoleBinding through the native API and prove the named certificate can administer the cluster directly. Remove the five retained archive objects and the dangling ServiceAccount grant through an exact approved Story 4.28 scope, with explicit approval for handling the named finalizers. After removal, use native readback to verify the objects and grant are absent and the replacement Administrator access still works. The Administrator owns the 2026-11-03 review of those retained objects.
13. Before accepting Rancher access, record the Administrator's decision to disable Rancher's local admin or seal it as MFA-less break-glass under independent custody. The sealed option requires a recorded amendment to the 2026-10-03 Rancher Keycloak MFA decision. With workload-cluster Keycloak unavailable, verify the selected local-admin policy, the approved access/recovery path, and public and unauthorized access denial. Keep account and custody specifics encrypted privately and publish only the decision and outcomes.

## Implementation handoff

Administrator approved the [course correction](../planning-artifacts/sprint-change-proposal-2026-10-01.md). This scoped input is backlog, not an executed qualification. Use the [migration spec](../specs/spec-kubesphere-to-rancher/SPEC.md) and [operations handoff](../../eng/cluster-management/README.md). The 2026-10-03 sole-Administrator decision in the [Story 4.26 qualification](spec-4-26-qualify-rancher-and-the-management-migration.md) supersedes the migration specification's scoped deputy requirement. Retain exact identities, sanitized attempt records and encrypted private inventories; require acceptance evidence and applicable exact procedure approvals before mutation or completion. No live inspection, deletion or installation is recorded by this file.

The Story 4.26 review handoff requires the named one-year native certificate to use a unique, never-reused CN and **no organization (O) group**. This excludes group grants such as `system:masters` or `kubeadm:cluster-admins`. Verify its sole administrative grant comes from its own unowned ClusterRoleBinding and that deleting that binding removes that grant. Before removing the retained `jpiquot` path, read back the final binding through the native API and demonstrate direct administration with the named certificate. Obtain approval for the exact 4.28 removal scope and handling of the named finalizers. Afterward, confirm through native readback that the five objects and dangling ServiceAccount grant are absent and the named credential still has direct access. The Administrator owns the 2026-11-03 review date ([retained-object table](../../eng/cluster-management/RETIRE-KUBESPHERE.md#approved-retirement-decisions-2026-10-04)).

Before accepting Rancher access, the Administrator must decide whether to disable the local admin or seal it as MFA-less break-glass with independent custody. Choosing the sealed option amends the 2026-10-03 Administrator decision to enforce Rancher MFA through Keycloak and requires a recorded amendment. Test the selected policy, unauthorized/public refusal, and the approved access/recovery path with workload-cluster Keycloak unavailable; external Keycloak MFA alone does not settle the local-admin path. Keep custody and access specifics encrypted privately, and publish only decisions and outcomes.
