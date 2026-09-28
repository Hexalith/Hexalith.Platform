# Epic 4 Context: Publish retained releases and run the isolated staging environment

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Publish an immutable, attested release containing the application Helm package, composed EventStore image, McpCli candidate, and signed release record, then deploy that exact release by digest into an isolated staging environment on `hexalith.com`. Before staging is introduced, remove the date-bound and live security risks on the designated single-node cluster: protect the three existing data systems, leave Kubernetes 1.34 before its 2026-10-27 end of life, close public administration and anonymous registry access, and relocate the privileged Forgejo runner.

## Stories

- Story 4.0: Prove off-node backups and isolated restores
- Story 4.1: Upgrade the cluster off Kubernetes 1.34 after verified backups
- Story 4.2: Close public admin exposure and anonymous registry reads
- Story 4.3: Relocate the privileged Forgejo runner
- Story 4.4: Apply publication and operations repository controls
- Story 4.5: Qualify the Aspire-to-Helm export with the reference composition
- Story 4.6: Encode release, attempt, qualification and Administrator records
- Story 4.7: Provide the staging attempt, lock and promotion-stop store
- Story 4.8: Maintain the module intake manifest
- Story 4.9: Ratify the production profile and the hosted catalog and secret contracts
- Story 4.10: Define the recovery hook contract
- Story 4.11: Define the check-suite invocation and result contract
- Story 4.12: Publish retained releases with provenance
- Story 4.13: Retain artifacts off the primary failure domain
- Story 4.14: Build staging namespaces, storage and pod and network isolation
- Story 4.15: Provide staging secrets through a per-environment OpenBao and tenant-key store
- Story 4.16: Apply the staging Dapr trust domain and runtime policy
- Story 4.17: Select and qualify the durable production broker
- Story 4.18: Serve staging through its own Gateway, hostnames and certificates
- Story 4.19: Generate the staging realm and register staging clients
- Story 4.20: Run the staging executor and the module-code sandbox
- Story 4.21: Send staging telemetry to a per-environment sink
- Story 4.22: Deploy the reference release to staging
- Story 4.23: Restore staging data in place
- Story 4.24: Use McpCli against staging
- Story 4.25: Prove staging-side isolation controls

## Requirements & Constraints

- Stories 4.0–4.3 are urgent work and precede creation of any staging namespace. Stories 4.0, 4.2 and 4.3 can execute independently; Story 4.1 preparation can proceed in parallel, but its in-place upgrade mutation is gated by completion of Story 4.0. The single-node upgrade interrupts every workload.
- Story 4.0 owns encrypted, immutable off-node recovery points and signed isolated-restore proofs for the existing Keycloak PostgreSQL cluster, shared OpenBao raft data, and Memories data. No CloudNativePG backups currently exist, OpenBao snapshots remain on node-local storage, the cluster has no CSI snapshot APIs, and no qualifying recovery point for these systems is yet demonstrated.
- Story 4.1 owns only kubeadm/workload preflights, the date-bound sequential minor upgrade and post-upgrade health or restore checks. A missing, stale, unsigned or failed Story 4.0 proof keeps its mutation gate closed.
- After the upgrade, the actual supported Kubernetes minor and current patch must be recorded. Keycloak, OpenBao, Memories, and Forgejo must each be verified healthy or restored, with evidence retained.
- Keycloak administration, the master realm, and the cluster console must be reachable only through the declared Administrator path and fail an external public probe. Anonymous registry reads must be refused only after all consumers have working pull credentials. Registry garbage collection must preserve retained digests.
- The Forgejo runner must move to a separate machine or VM shared with neither the application cluster nor an executor. Its in-cluster privileged namespace must be removed, and jobs must succeed without access to cluster APIs or namespaces.
- Staging and production use separate data, credentials, trust domains, namespaces, routes, and environment-layer state. Staging must not claim production names or authority, and negative isolation tests must cover identity, network, pod, data, secret, and administration boundaries.
- Publication builds and attests artifacts once. Deployment verifies and deploys retained digests without rebuilding. Release, attempt, qualification, promotion-stop, and Administrator records are versioned, signed by their entitled writers, and stored outside the target cluster and executor hosts.
- Staging uses the production profile template with environment-specific bindings. Hosted workloads use Pod Security `restricted`, default-deny networking, scoped Dapr policy, per-environment OpenBao and tenant-key storage, native data-service TLS where supported, and node-level volume encryption.

## Technical Decisions

- The designated installation remains a single-node, shared-kernel topology; an in-place Kubernetes minor upgrade taking all workloads down is an accepted constraint, not a high-availability operation.
- The representative hosted composition is Parties, EventStore, Tenants, and Memories. Aspire-to-Helm export is qualified early; recurring hand modelling, generated-file patches, a custom compiler, or duplicate topology triggers the maintained Helm chart fallback.
- Retained artifacts live in `registry.hexalith.com`, are immutable by digest, use authenticated per-environment pull credentials, and replicate off-site under a separate writer. No writer may update or delete retained content.
- Gateway API on Traefik is the hosted ingress model. Each environment owns exact hostnames and certificates; application namespaces hold constrained HTTPRoutes, while Gateways and listener certificates are environment-layer state.
- Staging deployment runs from an isolated private-network executor. Module-supplied code runs in a sandbox without job credentials, and no staging or test code runs on a host that holds production credentials.
- The staging attempt store serializes changes with compare-and-set locks and monotonic epochs. Every mutation verifies the current epoch, and staging promotion-stop state is distinct from production.

## Cross-Story Dependencies

- Story 4.0's three signed backup-and-isolated-restore proofs gate Story 4.1's Kubernetes mutation. The completed upgrade, exposure closure, and runner relocation all gate introduction of staging data and workloads.
- Repository controls, record encoding, the attempt store, intake manifest, profile/contracts, recovery contract, and check-suite contract precede the first retained publication and staging deployment.
- The Helm qualification depends on retained package publication and rollback support. Staging isolation, secrets, Dapr policy, broker, Gateway, realm, executor, and telemetry must exist before the reference release is deployed.
- Staging data restore follows the first staging deployment and must complete before candidate evidence is accepted. McpCli and the full isolation matrix are verified against the deployed staging release.
