# Epic 4 Context: Publish retained releases and run the isolated staging environment

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Publish attested, immutable Helm, composed EventStore, McpCli and release-record artifacts; deploy them by digest into isolated staging under `hexalith.com`. Parties, EventStore, Tenants and Memories form the reference composition. Urgent recovery/security work precedes staging; Folders and Projects enroll later.

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
- Story 4.9: Ratify the hosted runtime and identity-event contracts
- Story 4.10: Define the recovery hook contract
- Story 4.11: Define the check-suite contract and own the reference suites
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
- Story 4.26: Qualify Rancher and the management migration
- Story 4.27: Retire KubeSphere without changing workload data
- Story 4.28: Deploy Rancher and register the existing cluster

## Requirements & Constraints

Staging and production have separate data, credentials, realms, namespaces, storage binding, Gateway, OpenBao and Dapr trust domains. Staging cannot claim production names or authority or exhaust production capacity. CI holds no hosted provider credentials. Prove denials; shared hardware establishes no site resilience.

Route and advertise only declared public interfaces. Every required workload must reach the intended release and readiness within ten minutes of deployment start.

Required suites need owners, nonempty check sets and stable identities. Missing, stale, unsigned, failed, mismatched, skipped or incomplete evidence cannot pass; timeouts fail. Bind results to release, suite, profile and configuration. Local/CI experiments grant no hosted qualification. Backups require verified isolated restore; preparation never authorizes live mutation.

The hosted broker must demonstrate durable delivery, identity, deduplication, dead-letter capture, retention and backlog recovery. Redis pub/sub is excluded. Authenticated Architecture/Platform decisions govern selection; EventStore owns requirements and result evaluation. Platform renders declared topic Subscriptions with bound components and dead-letter policy.

## Technical Decisions

Use one Aspire composition and retained chart. Shared export helpers supply Dapr annotations, restricted security contexts and HTTPRoutes referencing the environment Gateway. The chart contains no Gateway or Secret; use nonsecret endpoints and digest-pinned images. Duplicate topology or recurring patches trigger the maintained-chart fallback.

Publication verifies intake-pinned attestations through pinned Builds workflows on disposable runners. Signed records bind release invariants and writers. Deploy retained artifacts without regeneration. Create-only off-site retention lasts through the backup period or rollback-target lifetime, whichever is longer.

A compare-and-set store outside the target cluster and every executor host holds each environment's lock and monotonic epoch. Every mutation checks its epoch; terminal attempts are immutable.

Application workloads, services, HTTPRoutes, Dapr Configurations, WorkflowAccessPolicies, Subscriptions and Resiliency belong to the release. Data services, broker, OpenBao, tenant-key store, volumes, Gateway, Certificates, Dapr Components/HTTPEndpoints/MCPServers, network policies, quotas, limits and bootstrap Secrets are environment resources. Namespaces, environment-identity RBAC, storage/priority classes, control planes, Keycloak and Rancher are shared infrastructure. The environment layer is rendered from the baseline/candidate union and applied forward-only in its own attempt before application deployment. The application deploy identity cannot write it. Each tier has its own writer.

Enforce restricted Pod Security, proven default-deny networking, quotas and encrypted storage. Dapr denies by default, disables HotReload and drops sidecar capabilities. Per-app secret-token rotation restarts pods; consuming sidecars restart after policy/resource activation. Use native data-service TLS where supported.

Dedicated private executors enforce job allowlists and receive revocable, epoch-bound per-job credentials through OIDC claim checks. Each executor uses a separate host, sharing with neither a CI runner nor another executor. Module code runs sandboxed without deploy authority. Rancher stays private on a separate management VM; native administration/recovery remains independent, and Fleet never owns application releases.

## UX & Interaction Patterns

McpCli profiles bind one gateway, issuer and audience. Short-lived tokens reach only that gateway; refresh material stays in the OS credential store without `offline_access`. Reauthorize every call and refuse disabled, ineligible or unauthorized operations. Staging membership grants no management or production authority.

## Cross-Story Dependencies

Backups, public closure, runner relocation, repository controls and read-only management qualification proceed independently. Retirement requires ownership/recovery proof; the supported Kubernetes hop follows retirement, then private Rancher precedes staging foundations. Management procurement never blocks native retirement or upgrade.

Chart qualification can precede publication. Record, lock, recovery-hook and check-suite contracts precede bound release records. Intake provenance and EventStore host/profile ratification are prerequisites. Story 4.9's capture-lag decision precedes Story 4.19's identity-event export. Epic 3 supplies composition/catalog; hosted McpCli requires connected mode. Staging deployment follows its environment/security/routing/identity/executor/telemetry foundations; synthetic-data restore precedes promotion evidence.
