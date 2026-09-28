---
captured_at: '2026-09-28T12:51:19Z'
kubernetes_context: 'jpiquot@local'
classification: 'sanitized discovery evidence'
acceptance_state: 'observations only; no operational story is complete'
---

# Epic 4 initial cluster inventory

This file records read-only discovery for Stories 4.0–4.3. It contains no Secret data, credentials, tokens, cookies, certificates, database rows, OpenBao values, registry response bodies or tenant payloads. Secret objects were not queried. Resource names, versions, public routes, storage classes and HTTP status/location results are retained because they are needed to plan remediation.

## Collection method

The operator used context `jpiquot@local` and read-only `kubectl get`, API discovery and impersonated `kubectl auth can-i --list` queries. External HTTPS probes were anonymous, sent no authorization header or cookie, and retained only status code and redirect location. Registry repository names and response bodies were not retained.

Representative commands:

```text
kubectl version -o yaml
kubectl get nodes,namespaces,pods,ingress,statefulset,deployment,pvc,pv,cronjob -o <sanitized projections>
kubectl api-resources --api-group=postgresql.cnpg.io -o name
kubectl get backups.postgresql.cnpg.io,scheduledbackups.postgresql.cnpg.io -A -o json
kubectl api-resources --api-group=snapshot.storage.k8s.io -o name
kubectl auth can-i --list --as=system:serviceaccount:forgejo-runner:forgejo-runner
curl <public URL>  # anonymous; status and redirect only
```

## Observed topology and versions

| Observation | Sanitized result | Acceptance meaning |
| --- | --- | --- |
| Kubernetes API | v1.34.9 | Unsupported-date risk remains; Story 4.1 is not complete. |
| Node | `node1`, control-plane and worker, kubelet v1.34.9 | The designated cluster is one failure/outage domain. |
| Host/runtime | Ubuntu 24.04.3 LTS, containerd 2.3.3 | Baseline only. |
| Kubeadm etcd mode | External endpoint at `192.168.1.30:2379` | Story 4.1 requires the etcd owner's fresh recovery point and health gate. |
| Protected workloads | Keycloak, OpenBao, Memories and Forgejo pods all scheduled on `node1` | An in-place node upgrade interrupts every listed workload. |

The client was v1.36.1 and warned that its two-minor difference from the v1.34 server exceeds supported client/server skew. That client warning is a preflight issue to resolve; it is not proof of an upgrade target.

## Story 4.0 — backup and restore gaps

### Keycloak PostgreSQL

- The CloudNativePG APIs serve `Backup` and `ScheduledBackup` kinds, but cluster-wide queries returned **0 Backup resources and 0 ScheduledBackup resources**.
- `Cluster/keycloak-postgres` in namespace `keycloak` reported 2 desired and 2 ready instances using PostgreSQL 15.15.
- PVCs `keycloak-postgres-1` and `keycloak-postgres-2` are Bound on the node-hosted OpenEBS storage class.

Observed state: no CloudNativePG recovery point or isolated restore proof was found. Ready database pods do not satisfy Story 4.0.

### OpenBao

- `StatefulSet/hexalith-keys` in namespace `openbao` reported 3 desired and 3 ready replicas.
- `CronJob/openbao-raft-snapshot` runs daily and writes to PVC `openbao-snapshots`.
- `openbao-snapshots` uses `openebs-hostpath-retain`; its PV is a local volume with node affinity to `node1`.
- Recent snapshot Job pods had succeeded.

Observed state: snapshots exist only in the same node failure domain. Job success is neither off-node retention nor an isolated restore proof.

### Memories

- `StatefulSet/redis-stack` and `StatefulSet/falkordb` each reported one ready replica in namespace `hexalith-memories`.
- PVC `data-redis-stack-0` is Bound at 20 GiB and PVC `data-falkordb-0` is Bound at 10 GiB on `openebs-hostpath-retain`.
- Kubernetes API discovery returned no resources for API group `snapshot.storage.k8s.io`; no `VolumeSnapshot`, `VolumeSnapshotContent` or `VolumeSnapshotClass` API is available.

Observed state: the cluster cannot currently execute the CSI snapshot path in the module-owned backup contract. No paired off-node physical recovery point or verifier-backed isolated restore proof was found.

## Story 4.2 — public administration and registry reads

| Public surface | Cluster route | Anonymous observation |
| --- | --- | --- |
| Keycloak administration | `keycloak/keycloak-ingress` routes `/` and `keycloak/keycloak-admin-rate-limit` routes `/admin` on `auth.tache.ai` through `nginx-public` | `/admin/` returned 302 to the master admin console. |
| Keycloak master realm | Catch-all public ingress plus `keycloak-master-token-rate-limit` for the token path | `/realms/master` returned 200. |
| KubeSphere console | `kubesphere-system/kubesphere-console` on `kube.hexalith.com` through `nginx-public` | `/` returned 302 to `/login`. |
| Zot distribution registry | `registry-distribution/distribution-registry-ingress` on `registry.hexalith.com` through `nginx-public` | `/v2/`, catalog and a repository tag-list request returned 200. Catalog count was 9; names were not retained. |

Observed state: authentication pages, redirects and rate limits do not constitute closed exposure. The Administrator path is not yet declared/tested, registry consumers have not yet proved authenticated pulls, and no retained-digest GC proof exists.

The initially attempted hostname `keycloak.hexalith.com` did not resolve; live ingress discovery identified `auth.tache.ai` as the deployed public Keycloak hostname. This distinction is retained so closure tests target the observed route rather than an assumed name.

## Story 4.3 — runner placement and authority

- Namespace `forgejo-runner` enforces Pod Security `privileged`.
- `Deployment/forgejo-runner` reported one desired and one ready pod on `node1`.
- The pod includes a privileged `dind` container and persistent Docker/action cache PVCs.
- The runner ServiceAccount can discover standard Kubernetes API health/version endpoints and can create, update, patch and delete Calico `networkpolicies` and `globalnetworkpolicies`.

Observed state: the runner is a privileged in-cluster workload on the same physical node as the protected applications and has Kubernetes API authority. No external runner placement, job-pass evidence, cluster-denial evidence or source-removal evidence exists yet.

## Explicit blockers and next evidence

1. **Story 4.0:** procure/approve off-node immutable destinations and isolated restore targets; add Memories snapshot/copy capability; execute and sign all three restore proofs.
2. **Story 4.1:** mutation remains closed until Story 4.0 is done and all signed proofs validate. Maintenance approval, exact current target patches, kubeadm preflight and external-etcd recovery evidence are also outstanding.
3. **Story 4.2:** declare and prove the Administrator path before closing routes; inventory and credential every registry consumer before disabling anonymous reads; establish retained-digest protection before GC.
4. **Story 4.3:** provide a separate host/VM, prove jobs there without cluster credentials or reachability, revoke the old runner and only then remove namespace `forgejo-runner`.

No backup, restore, route change, credential change, garbage collection, runner cutover, node drain, reboot or Kubernetes mutation was performed during this inventory.
