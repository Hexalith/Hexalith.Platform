---
title: 'Relocate the privileged Forgejo runner'
type: 'story'
epic: 4
story: 3
created: '2026-09-28'
status: 'in-progress'
baseline_commit: '04422aaa53a4c82098c4aaca298b464c4c888dbf'
route: 'dispatch'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-4-context.md'
  - '{project-root}/_bmad-output/implementation-artifacts/evidence/epic-4/initial-cluster-inventory.md'
depends_on: []
---

# Story 4.3: Relocate the privileged Forgejo runner

As Administrator,
I want the privileged Forgejo runner moved off the application cluster node,
So that CI jobs share neither a host nor cluster authority with staging, production or an executor.

## Scope and current state

On 2026-09-28, namespace `forgejo-runner` enforced Pod Security `privileged`. `Deployment/forgejo-runner` ran one ready pod on `node1`; its `dind` container was privileged and used persistent Docker/action caches. The runner ServiceAccount could discover the Kubernetes API and mutate Calico network-policy resources. This is observed exposure, not a completed migration.

The target must be a separate machine or VM that is not `node1`, is not a Kubernetes node for the designated application cluster, and is shared with no staging/production deployment executor or module-code sandbox.

## Administrator decisions and external dependencies

Before cutover, the Administrator must provide and approve:

1. The external runner host/VM owner, hardened operating-system baseline, patching/encryption/backup policy and network zone.
2. Network egress needed for Forgejo, source/dependency endpoints and authenticated registries, with an explicit deny for the designated cluster API and workload networks.
3. Runner labels/capabilities and the repository/job set that must pass before cutover.
4. A secret-store enrollment/rotation procedure for the Forgejo runner registration credential and job credentials. No credential may be copied from a Kubernetes Secret into Git or evidence.
5. Cache migration policy. Caches are disposable by default; no PVC may be mounted or copied to the external host unless explicitly approved and scanned.

## Ordered tasks

- [x] Record the sanitized namespace, deployment, node, privileged-container and ServiceAccount baseline.
- [ ] Create `evidence/epic-4/4-3/<attempt-id>/` in the access-controlled evidence store and record source deployment UID/config digest, target host identity/attestation, operator and cutover window.
- [ ] Immediately before disabling scheduling, revoking credentials, deleting runner resources and deleting the namespace, re-read the source Deployment/namespace UID, resourceVersion, pod-template/config digest, registered runner identity and active-job set. Abort and re-inventory/re-approve if any identity or digest differs, so a replacement runner cannot be mistaken for the recorded source.
- [ ] Provision and harden the external host:
  1. Prove it is neither `node1` nor a node in the designated cluster and hosts no staging/production executor or sandbox.
  2. Enable host encryption, patching, audit logs and the approved container isolation mode. Restrict administrative access to the named operators.
  3. Enumerate every route to the designated cluster: configured control-plane endpoint; all DNS aliases and A/AAAA answers; IPv4/IPv6 API, load-balancer/VIP and node addresses; service/pod CIDRs; management/bastion/tunnel routes; and HTTP(S)/SOCKS proxy paths. Apply outbound denies for every direct and proxied path while allowing only required non-cluster services.
- [ ] Install the pinned Forgejo runner and container runtime on the external host. Enroll it with a new secret-store-issued registration credential, the approved labels and no kubeconfig, ServiceAccount token, cluster certificate, cloud cluster credential or mounted Kubernetes Secret.
- [ ] Keep the in-cluster runner available but prevent duplicate scheduling while validating the new runner. Route a non-production validation label exclusively to the external runner.
- [ ] Execute the approved job matrix on the external runner:
  1. Repository checkout, dependency restore, build, unit tests, container build and push/pull by digest pass for the selected repositories.
  2. Job evidence identifies the external host and runner instance.
  3. From inside a job container, probe every enumerated DNS alias, IPv4/IPv6 endpoint, direct-IP/SNI variant, proxy/tunnel path and representative service/pod address; Kubernetes discovery, namespace listing and cluster Service access must be refused by network and by absence of credentials. A DNS or timeout-only result is supplemented by firewall/proxy/audit evidence tied to the attempt.
  4. Prove the job environment has no Kubernetes token/kubeconfig mounts and no variables or files containing cluster credentials.
- [ ] Cut over production runner labels only after the entire validation matrix passes. Quiesce the in-cluster runner, wait for active jobs to finish, disable new jobs there and run the matrix again through normal Forgejo triggers.
- [ ] After the source-identity revalidation passes, rotate/revoke the in-cluster registration credential and any runner-specific registry/job credentials. Verify that exact old runner identity cannot accept a job or authenticate.
- [ ] Inventory every runner PVC/PV, `persistentVolumeReclaimPolicy`, VolumeSnapshot/provider snapshot and backing-volume identity. Before deleting the namespace, securely erase or cryptographically destroy Docker/action-cache and workspace data under the approved storage procedure; then delete `Deployment/forgejo-runner`, its ServiceAccount/RBAC, runner-only PVCs, ConfigMaps and Secrets and namespace `forgejo-runner`.
- [ ] Verify no namespace/resource named for the old runner remains, no pod runs its image/digest, the old credential is revoked, normal jobs still pass on the external host, and all enumerated direct/proxied IPv4/IPv6 cluster probes still fail. Prove no retained PV, released volume, snapshot or provider object contains recoverable runner/cache/workspace data; record provider-side residual checks and erase/delete receipts.
- [ ] Sign `runner-relocation-result.json` and commit a sanitized summary.

## Evidence outputs

- `target-host-attestation.json`: host identity, ownership, non-co-residency checks, hardening baseline and network-policy/firewall configuration digest.
- `external-runner-job-matrix.json`: job IDs/commits, runner instance, required job outcomes and artifact digests.
- `cluster-isolation-proof.json`: the complete endpoint/alias/proxy/address-family inventory, attempted API/namespace/service accesses, refusal outcomes, absent-credential checks and matching network/proxy/audit events.
- `source-removal.json`: repeated source UID/config validations, quiescence, credential revocation, deleted resource identities, namespace absence, PV/reclaim/snapshot inventory, secure-erasure and provider residual checks, and post-removal job results.

Evidence must not contain runner registration tokens, job secrets, registry credentials, kubeconfig content, ServiceAccount tokens or Secret values.

## Stop conditions

- Stop provisioning if the target shares a host with the application cluster, any staging/production executor or a module-code sandbox.
- Stop cutover if any required job fails, the runner identity is ambiguous, or the target can reach the cluster API/workload networks.
- Do not run both runners on the same production labels concurrently.
- Do not remove the in-cluster runner until active jobs finish and the external runner passes the full matrix through normal triggers.
- Stop revocation/deletion on source UID/config/registration drift, if credentials cannot be rotated/revoked, if any required configuration exists only in the namespace, or if retained/released storage cannot be securely erased and independently checked for residual data.
- Stop cutover if any DNS alias, IPv4/IPv6 address, direct-IP/SNI route, proxy, tunnel, node, service or pod path can still reach the cluster.
- Reopening the old in-cluster runner is not an accepted rollback. Repair the isolated external runner or provision another isolated host.

## Acceptance criteria

**Given** the external runner target
**When** its placement and network controls are verified
**Then** it shares no host with the designated cluster, an executor or a sandbox
**And** it has neither credentials nor network access to the designated cluster API or namespaces

**Given** the approved Forgejo job matrix
**When** normal jobs run after cutover
**Then** they pass on the external runner and identify that runner in evidence
**And** in-job probes of every DNS alias, proxy/tunnel and direct IPv4/IPv6 cluster path are refused

**Given** the external runner passes
**When** source removal completes
**Then** the old credentials are revoked and namespace `forgejo-runner` plus its runner resources are absent
**And** revalidated source identity, secure-erasure receipts and provider residual checks prove no replacement runner or recoverable retained runner data was removed or left behind
**And** no privileged CI workload remains on `node1`

## Implementation progress — 2026-10-07

The user-directed [fresh source observation](evidence/epic-4/4-3/20261007t153645z-source-observation/source-observation.json) confirms that `192.168.1.30` is the designated application node `node1`, with the privileged `dind` runner still running there. Its current pod template disables ServiceAccount token automount. This is unsigned read-only discovery, not external-host qualification, credential-absence proof or a cutover baseline. Both runner cache PVs are Bound local OpenEBS volumes with `Delete` reclaim policy; secure erasure and independent provider residual checks are unverified.

[Local preparation tooling and procedure](../../eng/runner-relocation/README.md) create owner-only pending evidence attempts outside Git and verify signed source checkpoints for identity/configuration drift, expiry, approval chronology, current active jobs and exact source-deletion lineage. The [local validation summary](evidence/epic-4/4-3/20261007t153936z-local-preparation/summary.md) records 15 passing focused tests, including actual SSH signature verification and failure cases. Local checks authorize no mutation and satisfy no operational acceptance criterion.

The separate destination host, Administrator policy/job/credential/storage decisions, registered source runner identity, active-job census and complete cluster-path inventory remain outstanding. No host was provisioned, runner enrolled, job matrix executed, labels cut over, credential revoked, storage erased or source resource removed. All operational tasks above remain open and this story remains `in-progress`.
