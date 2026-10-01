# Cluster-management migration operations

The [approved migration spec](../../_bmad-output/specs/spec-kubesphere-to-rancher/SPEC.md) governs this package. Story4.26 now has executed **read-only native census**, dated chart/image/license/source observations and isolated-fixture tooling. Qualification remains incomplete; no live retirement, roles, Rancher provisioning or Kubernetes upgrade is authorized.

| Procedure | Story | Current handoff |
| --- | --- | --- |
| [Qualification](QUALIFICATION.md) / [collector](qualify.py) |4.26| Explicit native context, encrypted exports, sanitized UID/version/ownership/storage census; unknown ownership/access/authority/tool evidence stays incomplete |
| [KubeSphere retirement](RETIRE-KUBESPHERE.md) / [fixture](rehearse.py) |4.27| Version-specific hooks inspected; exact production removal set stays empty pending controller/consumer/propagation and recovery acceptance |
| [Native Kubernetes upgrade](../kubernetes-upgrade/README.md) |4.1| Fresh post-retirement recovery point and every existing signed hop gate; historical hashed proposal preserved |
| [Rancher installation/recovery](RANCHER.md) |4.28| Available community2.15.2 chart/image candidates, K3s/native-hop direction, concrete VM/private DNS/TLS/firewall/cost/custody and independent restore/authority plan |

Order is **qualification → retirement → supported native hop → private Rancher/access/initial restore → staging**. Accepted Story4.2 public-exposure closure and Story4.3 runner relocation also gate staging under the approved epics. Public administration closure remains independently urgent under4.2. Rancher availability and VM procurement do not block native retirement/upgrade. The dedicated single-node K3s manager imports the existing kubeadm cluster; it is shared infrastructure outside Aspire/application releases, with no HA/site-resilience or workload-distro migration claim.

Private immutable attempts live in `~/hexalith-management-evidence/`; raw API/Helm/configuration exports are encrypted before filesystem writes. Git retains only explicit projections, public-source identities, digests, outcomes and incomplete criteria under [4.26 evidence](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26). Existing recovery custody is refused as an output location. No signatures, off-node readability, authority, approval or acceptance are inferred from file presence/checksums.

The collector requires explicit `--context`, `--kubeconfig`, compatible `--kubectl`, separate management `--helm`, `--age` and the Administrator's public `--recipient`. Every attempt is unique. Secret data, arbitrary specs/annotations and full error output remain encrypted. Inaccessible discovery is a coverage gap; catalog entries are distinct from installed plans/controllers. [Qualification instructions](QUALIFICATION.md) give a repeatable command and trace unresolved criteria.

`rehearse.py` accepts a **sanitized source inventory**, never a production kubeconfig. It creates a uniquely named internal Docker network and fresh kind node, verifies source API/etcd/SSH/HTTPS and external egress refusal, and compares source/fixture UIDs. A pinned existing Docker image is required. Stock kind's internal-network DNS rewrite is adapted without adding a default route; native commands run exclusively through `docker exec` into the new fixture. The optional `--ks-chart`, `--ks-chart-sha256` and `--helm` mode loads retained public vendor images, installs actual core1.2.4/application4.2.1 with fresh synthetic credentials, and tests native Helm uninstall with broad cleanup hooks disabled. Installed-extension/production-value differences remain unaccepted. Exact named fixture cleanup removes its cluster/network/credentials/image aliases; global pruning is prohibited.

```bash
python3 -m unittest discover -s eng/cluster-management -p 'test_*.py'
git diff --check
```

Forty-five tests cover projection confidentiality/dependencies, paginated native/Helm discovery, failed schema/preflight receipts, destination custody, skew/native-path refusal, ownership/pins/authority, propagation, protected storage/lifecycle drift, native delete request races, mutation ownership and bounded exact cleanup. Docker daemon/API errors cannot prove source refusal or cleanup absence. The [fresh native fixture](../../_bmad-output/implementation-artifacts/evidence/epic-4/4-26/20261001t195601z-rehearsal/native-result.json) additionally verifies real stale-UID/stale-resourceVersion409 rejection, preservation/canary and explicit node/network/volume/image-alias cleanup. A test pass is not acceptance of a production uninstall. The [actual core diagnosis](RETIRE-KUBESPHERE.md#version-specific-procedure-findings) records core deployments removed while custom-resource finalizers and an unreviewed extension-job deletion leave retirement incomplete; no-hooks uninstall is unqualified.

Preserve signed4.0 proof sets and the exact [MAINTENANCE.md](../kubernetes-upgrade/MAINTENANCE.md) digest. After actual accepted retirement, derive a separately approved upgrade successor from the new census. Management grants remain Administrator-only; deputy recovery never acquires admission or expands entitlement, and missing current independent grant/revocation lineage fails closed.
