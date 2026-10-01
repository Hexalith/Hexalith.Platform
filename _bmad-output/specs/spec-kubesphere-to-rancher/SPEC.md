---
id: SPEC-kubesphere-to-rancher
status: approved
created: 2026-10-01
scope: Epic 4 cluster-management migration and downstream qualification
source: ../../planning-artifacts/sprint-change-proposal-2026-10-01.md
---

# KubeSphere to Rancher migration

Administrator approved the complete course correction with “I approve” on 2026-10-01. This spec carries that approved intent into the three migration stories. Exact deployment identities, resource ownership, capacity and execution evidence remain qualification work.

## Intent and authority

Replace KubeSphere 4.2.1 with Rancher community because the desired open-source baseline excludes its current additional license restrictions and the installed version's documented Kubernetes boundary blocks the proposed supported hop. Use the [Platform spine](../../planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md)'s Cluster management convention under existing AD-1/3/6/7/8/12; retain AD-1 through AD-15 and all product capability IDs.

Rancher runs on a dedicated private single-node K3s management VM and imports the existing kubeadm workload cluster. It is shared infrastructure outside the Aspire application composition and retained application Helm package. Native kubeadm, external-etcd recovery and independently retained private native access remain authoritative. Required agents are trusted shared administration with inventoried privileges; application deployment writers and staging/production isolation remain enforced. Single-node management and same-host placement establish no HA or site resilience.

## Work order

| Stage | Scoped input | Completion condition |
| --- | --- | --- |
| Qualification | [4.26](../../implementation-artifacts/4-26-qualify-rancher-and-the-management-migration.md) | Exact supported version direction, current ownership/dependency census, tested native access, isolated retirement rehearsal and reviewable removal scope. VM procurement may remain scheduled. |
| Retirement | [4.27](../../implementation-artifacts/4-27-retire-kubesphere-without-changing-workload-data.md) | Exact approved retirement attempt, signed workload/PVC preservation, private native access and no remaining KubeSphere runtime/admission blocker. |
| Native Kubernetes hop | [4.1](../../implementation-artifacts/4-1-upgrade-the-cluster-off-kubernetes-1-34-after-verified-backu.md) | Fresh post-retirement recovery point and every current signed per-hop gate; actual supported patch and protected-workload verification. |
| Replacement manager | [4.28](../../implementation-artifacts/4-28-deploy-rancher-and-register-the-existing-cluster.md) | Qualified private deployment/import, access/authority tests, management backup/isolated restore and native operations with manager unavailable. |
| Hosted gates | 4.14, 6.6/6.23, 7.6, Epic 8 and 9.2 | Rancher qualification before staging; management included in G1 currency/access, G2 recovery and G3 recurring checks. |

Story 4.2 closes existing public administration independently; it can use its approved private native-CLI option. Story 4.3 and all other urgent work retain their current requirements. Console installation/procurement does not block retirement or the native hop. Delayed upgrade delays staging; the old staging-on-1.34-after-end-of-life waiver is superseded.

## Execution boundaries

- Preserve the workload-cluster identity/distribution, native namespaces, workloads, PVC/PV identities and bindings, shared storage/network/identity dependencies and protected application data. Map only required management capabilities; do not presume a workspace/project/account schema conversion.
- Complete current recoverability, signed/fresh native proof, private-access, ownership and isolated-rehearsal gates before retirement. Bind approval to its exact resource allowlist, outage/stop/recovery procedure and current attempt. A namespace/CRD wildcard or blanket finalizer removal is not an approved resource decision.
- After retirement, refresh external-etcd and matching node/configuration recovery evidence. Revalidate Story 4.0's proof policy and remote readability; its `done` status does not extend evidence validity. Keep every remaining API/operator/admission, Forgejo source consistency, authenticated workload and exact maintenance approval gate.
- Preserve the existing hashed upgrade procedure and signed evidence. Prepare its successor from the actual post-retirement census and obtain its exact procedure/outage approval. No downgrade or destructive restore is implied as rollback.
- Rancher proxy credentials do not replace independent native access. Private UI/API/proxy, administrative authentication/MFA, credential custody/revocation and scoped deputy recovery remain required. Recovery never grants admission, promotes staging principals or expands deputy authority.
- Retain pinned owner-controlled management definitions, chart/image/license identities, management-cluster recovery inputs and separately held backup encryption configuration/keys. Management backups do not restore downstream workload data or external etcd.
- Do not introduce Fleet as an application release writer, unattended workload-cluster upgrades or optional infrastructure stacks. Bound required system-namespace/agent exceptions without changing application namespace restrictions.

## Qualification inputs still to resolve

Story 4.26 owns the live inventory, actual relied-on extensions, deletion propagation/finalizers, current native access/custody, supported and security-current community release/chart/image pairing, management VM host/OS/resource budget/cost, private endpoint/TLS/connectivity, admin authentication/MFA, role mapping and isolated retirement rehearsal. Rancher 2.14.4 and Kubernetes 1.35.9 are previously checked candidates, not immutable execution pins. Resolve current identities and source evidence per attempt.

The earlier backup gate expires at **2026-10-02T06:16:52Z**, before the recorded window ends at **2026-10-02T12:00:00Z**. Neither this approval nor that window extends freshness. Timing, source health and all identities must be revalidated before execution.

## Evidence and handoff

Use the [operator package](../../../eng/cluster-management/README.md). Preserve private exports/recovery material outside Git under encrypted restricted custody; commit only sanitized identifiers, checksums, outcomes and links to access-controlled evidence. An attempt records operator/approver, exact resource/version identities, procedure digest, window/outage owners, source/recovery cut, signed gate result and observed before/after outcomes. Missing, drifted, stale, unsigned or failed mandatory evidence leaves the relevant story incomplete and closes mutation.

PO/Developer owns the synchronized backlog and scoped inputs; Architect owns the canonical convention and consistency review; Administrator owns access/capacity and exact execution decisions; QA/dependency owners verify preservation, access and restore outcomes. The course-correction handoff is complete when these inputs and their checks are delivered; live migration completion requires the actual story evidence.

## Consistency review clarifications

Management installation Helm and native recovery tools are separate qualified owner-procedure inputs; the application-executor Helm floor is unchanged. Administrator-approved management grant/revocation records and their complete lineage remain independently available off-site; restored roles/tokens fail closed on missing, conflicting or gapped authority before reconnecting. Manager restoration is independent of native workload recovery/verification, not a mandatory earlier stage. Integrated drills account for every necessary management step within the applicable RTO. These clarify the approved pinning, revocation, native-access and recovery requirements without creating an additional release writer or deputy power.
