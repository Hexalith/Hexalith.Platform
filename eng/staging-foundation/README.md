# Staging foundation (Story 4.14) - BLOCKED, offline only

Status: `BLOCKED_NOT_FOR_EXECUTION`. `approvalGranted=false`, no executable dispatcher exists, and nothing here touches the cluster. Story 4.14 stays in-progress. Required gates 4.1, 4.2, 4.3, 4.27 (in-progress) and 4.28 (backlog) lack full dependency acceptance. Story 4.2 has accepted scoped exposure/registry results; its full workflow review remains pending. See the evidence-backed [approval package](APPROVAL-PACKAGE.md) and [gate assessment](gates.assessed.json).

The additive [current unblocking handoff](UNBLOCKING-REPORT.md) and
[source-bound assessment](unblocking.assessed.json) retain the exact 37-field
inventory (two observed DNS fields, 35 nulls), four pending owner decision
groups, definition gaps and phase-separated requirements for later approval.
The historical package and all six foundation files pinned by Story 4.15 remain
unchanged. [Offline verification and receipts](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-14/offline-unblocking-2026-10-29/verification.json)
establish consistency only; approval and live acceptance remain false.

## Contents

| File | Purpose |
|---|---|
| `inputs.example.json` | Required-input schema, all `null`. The renderer refuses it. Provisioner, encryption attestation, custody holders, production StorageClass, deploy-identity names, quotas and DNS selectors are never defaulted. |
| `inputs.resolved.json` | Same input schema, with two cited DNS observations and 35 null fields; `_provenance` records source paths, JSON pointers and hashes. Still refused. |
| `MISSING-INPUTS.md` / `INPUT-EVIDENCE.md` | Exact question for every null field, additional live prerequisites and reasons historical observations cannot become defaults. |
| `APPROVAL-PACKAGE.md` / `gates.assessed.json` | Current approval boundary, per-stage refusal, target, effects, read-only checks, forward recovery and dependency acceptance audit. |
| `render.py` | `render` writes `shared-infrastructure.yaml` and `environment-layer.yaml`; `plan` prints the concrete apply plan. Refuses (exit 2, no output) on missing/placeholder input or a target other than `jpiquot@local` / `b5860674-1d1e-4a3b-b413-e1bb3b9a4557`. |
| `cni_probe.py` | Calico enforcement probe: manifests, kubectl argv sequence, and `evaluate`. Passes only if the allowed connection works and the denied peer is blocked; always emits cleanup argv. |
| `apply-plan.json` | Static blocked package: target, unmet dependencies, effects per stage, job-identity denial checks, forward recovery. |
| `test_staging_foundation.py` | Unit tests (no cluster call; sources cannot spawn processes). |

## Layers (AR-32)

- Shared-infrastructure identity: Namespaces `hexalith-staging` and `hexalith-staging-data` (Pod Security `restricted` enforce/audit/warn), staging StorageClass, PriorityClass `hexalith-staging` (value strictly below the production class).
- Environment-layer identity: ResourceQuota (denies the production StorageClass by zeroing its PVC count and storage), LimitRange (including PVC min/max), `default-deny-all` (Ingress+Egress), DNS egress, application-to-data TLS-port-only policies, and the data-service TLS contract ConfigMap. Certificates and keys belong to 4.15/4.16.
- Workload Gateway/egress allowances remain the workload layer's job. The foundation is module-agnostic; see [DEPENDENCY-DIRECTION.md](DEPENDENCY-DIRECTION.md).

## Offline use

```
python3 -B -m unittest discover -s eng/staging-foundation -v
python3 -B eng/staging-foundation/render.py render --inputs <real-inputs.json> --output <dir>
python3 -B eng/staging-foundation/render.py plan --inputs <real-inputs.json> --observed-context <ctx> --observed-cluster-uid <uid> --manifest-dir <dir>
```

The `plan` output lists, per identity, dry-run argv (`--dry-run=server`) and apply argv (marked `executable: false`), read-only verification `get` argv, and `auth can-i ... --as <job>` checks that must all answer `no` (exit 1). Run nothing until every dependency has accepted evidence and the owner approves the exact bytes.

## Recovery

Forward-only: re-run dry-run then re-apply the same reviewed manifests. Never `helm rollback`, delete namespaces/PVCs/PVs, or rewind data or credentials. StorageClass provisioner/parameters/reclaimPolicy are immutable; change them only through a new class name. Data recovery needs a separate exact owner-approved operation.

Notes: the PriorityClass `value` is immutable, so a changed value needs a new class name. The environment-layer server dry-run only works after the shared-infrastructure apply has created the namespaces. The first verification command reads the `kube-system` namespace UID and must equal the target cluster UID before and after every stage. A second quota (`deny-production-priority`) sets `pods=0` for the production PriorityClass name, since a quota scoped to the staging class could not stop use of the production one.

## CNI probe

`cni_probe.py manifests` writes `probe.yaml` and `client.yaml` for namespace `hexalith-cni-probe` (restricted, default-deny) and prints the argv sequence ending in cleanup. The probe image must be digest pinned and busybox-compatible (`httpd`, `nc`); none is defaulted. `cni_probe.py evaluate --log-file <client log>` exits 1 and reports `CNI does not enforce NetworkPolicy` if the denied peer connected.
