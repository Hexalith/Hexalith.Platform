---
title: '4.26: Qualify Rancher and the management migration'
type: 'chore'
epic: 4
story: 26
created: '2026-10-01'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: '98436a4cb884f8d11b4b5f3e6ae70e95ce687463'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/specs/spec-kubesphere-to-rancher/SPEC.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The approved replacement lacks an ownership census, retirement rehearsal and supported target. Installation tests do not establish safe removal.

**Approach:** Execute Story 4.26 qualification: collect protected native evidence, classify management dependencies, qualify exact Rancher/K3s/tool identities and independently usable access, rehearse native retirement in isolation, and deliver reviewable retirement and management plans.

**Decision:** Evaluate the Administrator's host `192.168.1.30` first; VM capability, capacity, endpoint and cost require qualification.

## Boundaries & Constraints

**Always:** Preserve cluster/namespace/workload/PVC identities, bindings and shared dependencies. Encrypt private exports outside Git; commit sanitized evidence. Revalidate skew, identity and currency. Preserve Administrator/deputy separation, MFA and independent grant/revocation lineage; missing authority fails closed. Distinguish observation, proposal, approval and acceptance.

**Never:** Mutate production, uninstall KubeSphere, grant roles, provision/install Rancher, upgrade Kubernetes, overwrite recovery evidence or the hashed upgrade proposal, introduce Fleet application writers, or import production credentials/data into a fixture. Namespace/CRD wildcards and blanket finalizer removal cannot constitute retirement scope. Qualification does not authorize 4.27 or open 4.1's gate.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
| --- | --- | --- | --- |
| Census | Exact native context and compatible tools | Timestamped UID/version/ownership/storage census; encrypted private exports and sanitized digest ledger | Failed discovery or inaccessible API is an explicit coverage gap |
| Ownership | Catalog entries, installed controllers, consumers and finalizers | Distinguish available extensions from installations; classify each capability and deletion effect | Unknown owner/consumer/propagation blocks retirement acceptance |
| Target | Version-specific hosting/import matrices and stable releases | Exact separate management/workload/chart/image/tool/license identities | Unsupported, prerelease or security-unqualified identity remains unaccepted |
| Rehearsal | Isolated representative inventory and native procedure | Exact proposed allowlist, preservation assertions and cleanup evidence | Source reachability, drift or unexpected deletion stops the fixture |
| Authority | Native credentials and independent grant/revocation lineage | Authorized native access, unauthorized/public denial and scoped management-role proposal | Proxy-only access or missing/conflicting lineage fails closed |

</frozen-after-approval>

## Code Map

- `_bmad-output/implementation-artifacts/4-26-qualify-rancher-and-the-management-migration.md` — authoritative criteria.
- `eng/cluster-management/*.md` — existing contracts; add evidence-bound outputs.
- `eng/kubernetes-upgrade/prepare.py` — reuse private immutable-attempt/projection patterns; presence is not signature verification.
- `~/hexalith-upgrade-evidence/operations/story41-*.py` — adapt capture/fixture/recovery patterns; top-level actions prohibit direct imports.
- `~/hexalith-upgrade-evidence/tools/` — requalify retained kubectl/Helm/age/etcd identities.
- `~/hexalith-recovery-evidence/4-0/20261001t061620z/tools/` — reuse accepted signature/readback verification.
- `eng/kubernetes-upgrade/MAINTENANCE.md` — preserve digest; derive successor after retirement.

## Tasks & Acceptance

**Execution:**
- [x] `eng/cluster-management/qualify.py` — collect explicit-context read-only evidence: tools/runtime, chart/images/config, installed extensions, CRDs/instances, ownership/finalizers, admissions/API services, RBAC/routes/namespaces and persistent state; protect each attempt.
- [x] `eng/cluster-management/test_qualify.py` — test secret exclusion and failed discovery/ownership/pin/authority validation.
- [x] `_bmad-output/implementation-artifacts/evidence/epic-4/4-26/<attempt-id>/` — retain inventory, capability/disposition map, dated sources, current pins/licenses/tools and encrypted-export digests.
- [x] `eng/cluster-management/QUALIFICATION.md` — evidence native access/denial/custody, MFA/roles, independent authority lineage and fail-closed restoration; manufacture no grants/approvals.
- [x] `eng/cluster-management/rehearse.py` — rehearse isolated native uninstall with representative ownership/config and synthetic state; test propagation/finalizers, namespace/PVC preservation, license independence, stop/recovery and cleanup.
- [x] `eng/cluster-management/test_rehearse.py` — test source refusal, allowlist enforcement and unexpected deletion.
- [x] `eng/cluster-management/RETIRE-KUBESPHERE.md` — deliver ordered UID/resourceVersion-bound actions, allowlist/procedure digests, preservation assertions, currency/drift stops and 4.27 approval/recovery requirements.
- [x] `eng/cluster-management/RANCHER.md` and `README.md` — deliver concrete VM/OS/resource/storage/cost/private DNS/TLS/firewall/CA/backup/owner plan, scoped access and independent backup/restore tests; retain single-node/shared-host limitations and qualification → retirement → hop → Rancher → staging order.
- [x] `_bmad-output/implementation-artifacts/4-26-qualify-rancher-and-the-management-migration.md` and `sprint-status.yaml` — retain unmet criteria; complete only with mandatory evidence. Procurement may wait for 4.28.

**Acceptance Criteria:**
- Given qualification results, when assessed, then all eight criteria have traceable evidence or remain incomplete; proposals/checksums alone cannot pass.
- Given the proposed retirement scope, when reviewed, then exact ownership, native uninstall, propagation, workload preservation and recovery are demonstrated without licensed KubeSphere writes or production mutation.
- Given qualified plans, when handed to 4.28, then hosting/import support, tools/licenses, capacity, private/MFA access and independent recovery/authority are evidenced.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

Prefer a separate VM on existing safe capacity. If node1 is a guest, use a sibling VM on its hypervisor. A different physical host reduces correlated outages but adds cost/connectivity work. Neither provides HA. Propose 4 vCPU/16 GiB RAM/80 GiB SSD; CPU/RAM follows Rancher's [small-tier guidance](https://ranchermanager.docs.rancher.com/v2.14/getting-started/installation-and-upgrade/installation-requirements/), disk is estimated. Node1 reports 32 CPUs/~126 GiB RAM; spare capacity/virtualization remain unverified. Available SSH keys were rejected for root; the operator's working login is pending. No allocation/procurement is approved.

## Verification

- `python3 -m unittest discover -s eng/cluster-management -p 'test_*.py'` — meaningful evidence/isolation failure cases pass.
- `git diff --check` — clean whitespace.
- Inspect digests, custody, isolation/cleanup, criterion evidence and preserved identities; retain failures/limitations.
