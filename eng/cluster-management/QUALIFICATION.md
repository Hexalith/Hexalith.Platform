# Story 4.26: qualify the management migration

This procedure prepares the retirement and Rancher deployment decisions. Read-only qualification can start independently. Production changes use their own exact procedures/gates; this input does not authorize them.

## Collect the current census

1. Confirm private native API/SSH access, actual cluster/node identities, effective kubeadm/kubelet/kubectl/runtime versions and external-etcd identity. Prove authorized access and public/unauthorized denial without publishing credentials.
2. Inventory KubeSphere chart/release/image/configuration identities and installed extensions. Archive sensitive configuration/metadata encrypted outside Git. The earlier `ks-core` 1.2.4/app 4.2.1 observation must be rechecked.
3. Capture native namespaces, workloads, controllers/replicas, admissions, API services, CRDs/instances, RBAC, routes, owner references/finalizers, PVC/PV UIDs/bindings and protected-workload expectations.
4. For each managed capability, record owner, consumers, current writer, replacement/retirement disposition, deletion effects and acceptance assertion. Classify shared dependencies separately from KubeSphere-only resources. Unknown ownership or required functionality blocks removal.

## Qualify the target and access

1. Select a supported, security-current Rancher community release with exact chart/image/license/dependency identities and version-specific evidence for both the K3s hosting cluster and imported workload target. Recheck candidates; do not infer local hosting certification from generic downstream support.
2. Specify the dedicated VM's host, supported OS, CPU/RAM/storage budget, private endpoint, DNS/TLS/CA custody, firewall/agent connectivity, backup destination and ownership/cost. Document single-node and any shared-host failure boundary. Provisioning may follow the native hop.
3. Map needed operator capabilities to approved native/Rancher roles, with MFA and separate Administrator/deputy powers. A staging account/workspace does not imply cluster/global-admin authority. Qualify independently retained native kubeconfig/SSH custody; Rancher proxy credentials do not satisfy it.
4. Verify the management-backup operator and management-cluster recovery inputs, including separately retained encryption configuration/key material. Define private access/denial and revoked-token checks, manager-unavailable native operations and isolated restore/re-registration assertions.

## Rehearse retirement and produce its exact scope

1. Build an isolated representative target from retained chart/configuration and native ownership/dependency inventory. Demonstrate source isolation and keep production secrets/license/data outside the fixture unless independently approved and quarantined.
2. Test the version-specific native uninstall order. Prove that required retirement steps do not depend on licensed KubeSphere application writes. Inspect deletion propagation and finalizer behavior; preserve application/data namespaces, PVCs and shared services.
3. Produce the exact resource allowlist: API kind, namespace/name, UID/resourceVersion, owner, removal/rehome action, dependency order, expected propagation and verification. Any specific finalizer intervention is a separate named resource decision.
4. Define fresh recovery evidence, workload-owner acknowledgements, outage boundaries, incident/recovery roles, window, drift/stop conditions and source-preserving recovery decisions. Deliver a concrete retirement procedure/digest for review under 4.27 and a post-retirement upgrade-revision plan.

Completion requires the eight [4.26 acceptance criteria](../../_bmad-output/implementation-artifacts/4-26-qualify-rancher-and-the-management-migration.md). Record unresolved VM procurement separately for 4.28. Inventory or rehearsal success alone does not authorize live removal or clear the Kubernetes hop.

## Required authority and tool outputs

The exact owner-controlled manager procedure includes qualified Helm CLI and native recovery-tool identities; do not infer compatibility from the application-executor Helm floor. Establish Administrator-approved management grant/revocation records, independent off-site custody and complete current lineage. Test a post-cut management revocation whose source instance is lost; missing/conflicting/gapped authority leaves restored management disconnected and denied. Recovery may reissue only already entitled scoped operational credentials and cannot create new roles/admission.
