---
title: 'Relocate the privileged Forgejo runner'
type: 'story'
epic: 4
story: 3
created: '2026-09-28'
status: 'in-progress'
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
- [ ] Provision and harden the external host:
  1. Prove it is neither `node1` nor a node in the designated cluster and hosts no staging/production executor or sandbox.
  2. Enable host encryption, patching, audit logs and the approved container isolation mode. Restrict administrative access to the named operators.
  3. Apply outbound allow rules for required services and explicit denies for the Kubernetes API endpoint, node management addresses, pod/service CIDRs and namespace-facing routes.
- [ ] Install the pinned Forgejo runner and container runtime on the external host. Enroll it with a new secret-store-issued registration credential, the approved labels and no kubeconfig, ServiceAccount token, cluster certificate, cloud cluster credential or mounted Kubernetes Secret.
- [ ] Keep the in-cluster runner available but prevent duplicate scheduling while validating the new runner. Route a non-production validation label exclusively to the external runner.
- [ ] Execute the approved job matrix on the external runner:
  1. Repository checkout, dependency restore, build, unit tests, container build and push/pull by digest pass for the selected repositories.
  2. Job evidence identifies the external host and runner instance.
  3. From inside a job container, attempts to reach Kubernetes discovery, list namespaces and reach a cluster Service are refused by network and by absence of credentials. A DNS or timeout-only result is supplemented by firewall/audit evidence tied to the attempt.
  4. Prove the job environment has no Kubernetes token/kubeconfig mounts and no variables or files containing cluster credentials.
- [ ] Cut over production runner labels only after the entire validation matrix passes. Quiesce the in-cluster runner, wait for active jobs to finish, disable new jobs there and run the matrix again through normal Forgejo triggers.
- [ ] Rotate/revoke the in-cluster registration credential and any runner-specific registry/job credentials. Verify the old runner cannot accept a job or authenticate.
- [ ] Remove `Deployment/forgejo-runner`, its ServiceAccount/RBAC and runner-only PVCs, ConfigMaps and Secrets through the approved operations change, then delete namespace `forgejo-runner`.
- [ ] Verify no namespace/resource named for the old runner remains, no pod runs its image/digest, the old credential is revoked, normal jobs still pass on the external host, and cluster/API/network negative probes still fail.
- [ ] Sign `runner-relocation-result.json` and commit a sanitized summary.

## Evidence outputs

- `target-host-attestation.json`: host identity, ownership, non-co-residency checks, hardening baseline and network-policy/firewall configuration digest.
- `external-runner-job-matrix.json`: job IDs/commits, runner instance, required job outcomes and artifact digests.
- `cluster-isolation-proof.json`: attempted API/namespace/service accesses, refusal outcomes, absent-credential checks and matching network/audit events.
- `source-removal.json`: quiescence, credential revocation, deleted resource identities, namespace absence and post-removal job results.

Evidence must not contain runner registration tokens, job secrets, registry credentials, kubeconfig content, ServiceAccount tokens or Secret values.

## Stop conditions

- Stop provisioning if the target shares a host with the application cluster, any staging/production executor or a module-code sandbox.
- Stop cutover if any required job fails, the runner identity is ambiguous, or the target can reach the cluster API/workload networks.
- Do not run both runners on the same production labels concurrently.
- Do not remove the in-cluster runner until active jobs finish and the external runner passes the full matrix through normal triggers.
- Stop removal if credentials cannot be rotated/revoked or if any required configuration exists only in the namespace.
- Reopening the old in-cluster runner is not an accepted rollback. Repair the isolated external runner or provision another isolated host.

## Acceptance criteria

**Given** the external runner target
**When** its placement and network controls are verified
**Then** it shares no host with the designated cluster, an executor or a sandbox
**And** it has neither credentials nor network access to the designated cluster API or namespaces

**Given** the approved Forgejo job matrix
**When** normal jobs run after cutover
**Then** they pass on the external runner and identify that runner in evidence
**And** in-job cluster API, namespace and service probes are refused

**Given** the external runner passes
**When** source removal completes
**Then** the old credentials are revoked and namespace `forgejo-runner` plus its runner resources are absent
**And** no privileged CI workload remains on `node1`
