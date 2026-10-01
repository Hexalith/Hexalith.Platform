# Cluster-management migration operations

The [approved migration spec](../../_bmad-output/specs/spec-kubesphere-to-rancher/SPEC.md) governs this package. Administrator approved the course correction on 2026-10-01. These documents are implementation inputs; they contain no executed live inventory or destructive command set.

| Procedure | Owner/story | Required output |
| --- | --- | --- |
| [Qualification](QUALIFICATION.md) | Administrator/delegated operator, 4.26 | Current inventory, supported target direction, native access, isolated rehearsal and exact reviewable resource scope. |
| [KubeSphere retirement](RETIRE-KUBESPHERE.md) | Administrator/delegated operator, 4.27 | Separately approved attempt and signed removal/preservation result. |
| [Native Kubernetes upgrade](../kubernetes-upgrade/README.md) | Administrator/delegated operator, 4.1 | Post-retirement recovery evidence, complete existing gate and supported-hop outcome. |
| [Rancher deployment/recovery](RANCHER.md) | Administrator/delegated operator, 4.28 | Private manager/import, authority tests, backup/restore and independent native access. |

Order is qualification → retirement → supported native hop → private Rancher → staging. Public closure remains independently urgent under 4.2. Rancher availability and VM procurement do not block native retirement/upgrade.

Retain each private attempt outside Git. Use encrypted restricted storage for sensitive exports, kubeconfigs, tokens, keys and full recovery logs. Sanitized records bind the operator, approver, runbook digest, exact resource UIDs/versions, chart/image/source identities, evidence cut/checksums/signatures, window/outage owners and verified outcomes. A successful inventory command or checksum alone is not acceptance or authorization.

Preserve existing Story 4.0 signed proofs and the hashed `eng/kubernetes-upgrade/MAINTENANCE.md` proposal. After retirement, prepare a new upgrade revision from the remaining controller/admission/outage census and bind approval to the new exact digest. Do not silently overwrite the earlier recorded procedure or call its approval complete.

The replacement is a dedicated private single-node K3s manager importing the existing kubeadm cluster. It is not application failover capacity, a workload-distro migration or a source of HA. Existing Helm/executor release ownership remains; required management agents and their privileges are inventoried shared infrastructure.
