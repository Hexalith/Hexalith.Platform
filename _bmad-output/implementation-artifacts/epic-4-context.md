# Epic 4 Context: Publish retained releases and run the isolated staging environment

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Publish an immutable, attested application Helm package, composed image, McpCli candidate and release record, then deploy their exact digests into isolated staging on `hexalith.com`. Support Parties, EventStore, Tenants and Memories without granting production authority; Folders and Projects join later.

## Stories

- Story 4.0: Prove off-node backups and isolated restores
- Story 4.1: Upgrade Kubernetes after verified backups
- Story 4.2: Close public administration and anonymous registry reads
- Story 4.3: Relocate the privileged Forgejo runner
- Story 4.4: Apply publication and repository controls
- Story 4.5: Qualify the Aspire-to-Helm export
- Story 4.6: Encode signed release and operational records
- Story 4.7: Provide staging locks, attempts and promotion stops
- Story 4.8: Maintain the module intake manifest
- Story 4.9: Ratify runtime and identity-event contracts
- Story 4.10: Define the recovery hook contract
- Story 4.11: Define check contracts and own reference suites
- Story 4.12: Publish retained releases with provenance
- Story 4.13: Retain artifacts off the primary failure domain
- Story 4.14: Build staging namespace, storage and network isolation
- Story 4.15: Provide staging OpenBao and tenant-key custody
- Story 4.16: Apply staging Dapr trust and runtime policy
- Story 4.17: Qualify the durable hosted broker
- Story 4.18: Provide staging Gateway, hostnames and certificates
- Story 4.19: Generate the staging realm and clients
- Story 4.20: Run the staging executor and module sandbox
- Story 4.21: Send staging telemetry to its own sink
- Story 4.22: Deploy the reference release to staging
- Story 4.23: Restore staging data in place
- Story 4.24: Use McpCli against staging
- Story 4.25: Prove staging-side isolation
- Story 4.26: Qualify Rancher and the management migration
- Story 4.27: Retire KubeSphere while preserving workloads
- Story 4.28: Deploy private Rancher and register the cluster

## Requirements & Constraints

- Leave Kubernetes 1.34 before 2026-10-27 and before staging. Preparation authorizes no mutation.
- Require current signed off-node backups and isolated restores for Keycloak PostgreSQL, OpenBao and Memories. Preserve workload/PVC ownership; refresh external-etcd/node recovery after retirement. Invalid proof blocks mutation.
- Close public administration while preserving OIDC; authenticate registry consumers, protect retained digests, and relocate privileged CI away from cluster/executor hosts.
- Prove staging denial across production data, secrets, names, identities, management and restored copies. Encrypt volumes; use native TLS and default-deny networking.

## Technical Decisions

- One Aspire model defines composition. Charts pin digests and contain HTTPRoutes, no Gateway or secret values. Environment-layer writers own infrastructure. Recurring patches, duplicate topology or custom compilation trigger the maintained-chart fallback.
- Build/attest once; deploy without rebuilding. Retain artifacts, records and evidence off the primary failure domain for backup retention or rollback-target lifetime, whichever is longer.
- Accept versioned records only through entitled-writer signatures, never store access. Publication signs release and post-staging release-available records. Environment executors sign attempts; the production executor handles in-place recovery, the recovery executor replacement-capacity recovery. Terminal attempts are immutable; only the taking-over attempt's writer terminalizes superseded attempts. Issuing attempts sign qualification and production-promoted records. A working terminal attempt's writer advances its environment's latest-working pointer only at a higher epoch.
- Environment-bound stops may be set by executors, monitor, Administrator or deputy; clearance requires an Administrator record. That signing identity is held by no executor, workflow token, monitor or deputy. Admission records are create-only, predecessor-linked authority independent of restored identity data; gaps fail closed.
- Release records bind invariant artifacts, contracts, suites and qualified sets. Attempts bind environment-current configuration/profile identities, epoch, timing, observed stop and outcome. Latest-working names the working baseline's terminal attempt. Qualification extends environment-specific permitted sets.
- An external CAS store outside cluster/executor hosts serializes mutations under per-environment locks and monotonic epochs checked before commit. Separate namespaces, data services, OpenBao, tenant-key custody, realms, Gateways and Dapr trust domains; use restricted pods, scoped callers and credential-isolated module sandboxes. Separate staging/production executor hosts.
- Private Rancher/K3s is shared infrastructure outside application packages. Preserve native recovery; Fleet cannot compete with application writers. Administrator alone changes management grants; independent lineage, MFA and stale-authority fencing govern restoration.

## UX & Interaction Patterns

Private native/Rancher access uses scoped roles and a manager-unavailable procedure. Staging grants no management authority. McpCli advertises enabled, authorized profile operations.

## Cross-Story Dependencies

- Backups, exposure closure, runner relocation, repository controls and read-only management qualification can proceed independently. Qualified retirement and refreshed recovery precede every upgrade hop gate; private Rancher qualification precedes staging foundations. Rancher availability cannot block native retirement/upgrade.
- Record/store, intake, runtime/identity, recovery and suite contracts underpin publication. Environment foundations precede reference deployment; restore/isolation proofs underpin accepted staging evidence consumed by Epic 5.
