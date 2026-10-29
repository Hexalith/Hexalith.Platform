# Missing inputs for Story 4.14

Status: `BLOCKED_NOT_FOR_EXECUTION`. Of 37 required schema fields, 2 are supported by cited observations and **35 remain null** in `inputs.resolved.json`. No example, fixture, conventional port, current operator identity or existing class was promoted into a deployment decision.

Answer each numbered question with the exact value and a repository evidence path or an explicit owner decision to record. Resource quantities use Kubernetes quantity strings; pod/PVC counts use integer strings; priority values use integers. Do not send passwords, tokens, private keys or kubeconfig contents.

| # | Null field | Exact question |
|---|---|---|
| 1 | `storage.provisioner` | Which exact provisioner is approved for staging's node-encrypted volumes on this cluster, and which evidence records that selection? |
| 2 | `storage.parameters` | What exact string-to-string provisioner parameters are approved for the staging StorageClass, and where is that configuration evidenced? |
| 3 | `storage.reclaimPolicy` | Which reclaimPolicy, Retain or Delete, do you approve for staging's new StorageClass? |
| 4 | `storage.stagingStorageClassName` | What exact new staging StorageClass name do you approve? |
| 5 | `storage.productionStorageClassName` | Which exact production StorageClass name must staging quotas deny? |
| 6 | `storage.encryptionAttestation.evidenceRef` | What is the path/reference to current node-level volume-encryption evidence covering the staging backing devices and independent key custody? |
| 7 | `storage.encryptionAttestation.attestedBy` | Who is the named attestor for that node-encryption evidence? |
| 8 | `custody.holders` | Who are the named Administrator and deputy holders of the staging node-volume encryption keys, and where is their independent custody attested? |
| 9 | `priority.stagingValue` | What exact integer value do you approve for PriorityClass hexalith-staging? |
| 10 | `priority.productionValue` | What exact production PriorityClass integer value will exceed staging's value, and where is it evidenced? |
| 11 | `priority.productionClassName` | What exact production PriorityClass name must the staging denial quota prohibit? |
| 12 | `deployIdentities.sharedInfrastructure` | What exact Kubernetes principal is approved to apply the shared-infrastructure layer? |
| 13 | `deployIdentities.environmentLayer` | What exact distinct Kubernetes principal is approved to apply the environment layer? |
| 14 | `deployIdentities.jobs` | What is the complete list of per-job Kubernetes deploy principals whose denial checks must pass? |
| 15 | `network.dataServices` | What complete module-agnostic list of staging data-service names and native-TLS TCP ports is approved, with evidence that each selected port terminates TLS? |
| 16 | `limitRange.defaultRequestCpu` | What exact default container CPU request do you approve for both staging namespaces? |
| 17 | `limitRange.defaultRequestMemory` | What exact default container memory request do you approve for both staging namespaces? |
| 18 | `limitRange.defaultLimitCpu` | What exact default container CPU limit do you approve for both staging namespaces? |
| 19 | `limitRange.defaultLimitMemory` | What exact default container memory limit do you approve for both staging namespaces? |
| 20 | `limitRange.pvcMinStorage` | What exact minimum PVC storage quantity do you approve for both staging namespaces? |
| 21 | `limitRange.pvcMaxStorage` | What exact maximum PVC storage quantity do you approve for both staging namespaces? |
| 22 | `quotas.application.requestsCpu` | What exact aggregate CPU request quota do you approve for hexalith-staging? |
| 23 | `quotas.application.requestsMemory` | What exact aggregate memory request quota do you approve for hexalith-staging? |
| 24 | `quotas.application.limitsCpu` | What exact aggregate CPU limit quota do you approve for hexalith-staging? |
| 25 | `quotas.application.limitsMemory` | What exact aggregate memory limit quota do you approve for hexalith-staging? |
| 26 | `quotas.application.pods` | What exact pod-count quota do you approve for hexalith-staging? |
| 27 | `quotas.application.persistentVolumeClaims` | What exact PVC-count quota do you approve for hexalith-staging? |
| 28 | `quotas.application.requestsStorage` | What exact aggregate requested-storage quota do you approve for hexalith-staging? |
| 29 | `quotas.data.requestsCpu` | What exact aggregate CPU request quota do you approve for hexalith-staging-data? |
| 30 | `quotas.data.requestsMemory` | What exact aggregate memory request quota do you approve for hexalith-staging-data? |
| 31 | `quotas.data.limitsCpu` | What exact aggregate CPU limit quota do you approve for hexalith-staging-data? |
| 32 | `quotas.data.limitsMemory` | What exact aggregate memory limit quota do you approve for hexalith-staging-data? |
| 33 | `quotas.data.pods` | What exact pod-count quota do you approve for hexalith-staging-data? |
| 34 | `quotas.data.persistentVolumeClaims` | What exact PVC-count quota do you approve for hexalith-staging-data? |
| 35 | `quotas.data.requestsStorage` | What exact aggregate requested-storage quota do you approve for hexalith-staging-data? |

## Additional prerequisites outside the input schema

- Which exact digest-pinned, busybox-compatible image (`httpd`, `nc`) is approved for the future CNI probe? No image or live enforcement result is supplied. Probe creation/cleanup requires a separate exact live authorization and was not run.
- Which evidence proves the actual pod DNS resolver path, including the observed NodeLocal DNS daemon, is permitted by the reviewed DNS policy? A CoreDNS Service selector alone does not establish this.
- Which reviewed RBAC definitions and effective-permission evidence prove that the shared-infrastructure and environment-layer principals can perform their complete intended operations, while remaining separated from each other and every job principal?
- Which reviewed enforcement and evidence prove that staging claims bind only through the staging StorageClass, including denial of other classes and unintended default/static bindings? The existing quota only denies the named production class.
- Which complete permission checks prove that job principals cannot write arbitrary data-namespace workloads/PVCs or environment-owned ConfigMaps, in addition to the generated denial list? The current generated checks do not establish that full contract.
- Supply accepted completion evidence for 4.1, 4.2, 4.3, 4.27 and 4.28; see `gates.assessed.json`. Scoped 4.2 results and original-scope 4.26 qualification remain credited.
- After those inputs and gates are settled, approve the exact manifest bytes, identities, target, ordered server dry-runs/apply operations and verification/recovery package. No such operational approval is recorded here.

`storage.parameters` currently must be non-empty to pass the existing renderer. If the selected provisioner legitimately requires an empty map, record that fact and correct the offline validator under review; do not invent a parameter to satisfy it. `network.dataServices` must be the complete service inventory with native-TLS TCP ports; this schema does not encode per-service pod selectors or replication topology. Any required extra policy belongs in a reviewed forward definition before live use.

See `INPUT-EVIDENCE.md` for investigated sources and rejected substitutions. These questions concern Platform infrastructure only.
