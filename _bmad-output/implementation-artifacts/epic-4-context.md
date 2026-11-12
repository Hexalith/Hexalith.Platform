# Epic 4 Context: Publish retained releases and run the isolated staging environment

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Lay the release-and-staging foundation that every later promotion gate relies on. One publication workflow produces an attested, immutable release: the application Helm package, the composed `platform/eventstore` image, the McpCli candidate and a signed release record. A dedicated executor deploys that release by digest into an isolated staging environment on `hexalith.com`. There, team members use the reference composition (Parties, EventStore, Tenants, Memories) and McpCli with staging-only permissions, without any production names or authority. Urgent security and infrastructure work runs first and precedes staging foundations: verified backups, closing public exposure, runner relocation, repository controls, KubeSphere retirement, the Kubernetes upgrade and private Rancher. Folders and Projects join in Epic 10.

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

- **Separation:** staging has its own data, credentials, namespaces, realm, Gateway, OpenBao and Dapr trust domain. It cannot claim production hostnames, administer production or exhaust production capacity. CI holds no hosted credentials.
- **Interfaces:** only declared public interfaces are routed and advertised. Required workloads must be ready within 10 minutes of attempt start.
- **McpCli:**
  - A profile binds exactly one environment's gateway, issuer and audience.
  - Tokens are short-lived. Refresh material is stored only in the OS credential store, and `offline_access` is never granted.
  - The gateway re-authorizes every call and never logs tokens.
  - Ineligible, disabled, mismatched and unauthorized operations are refused.
- **Evidence:**
  - Any missing, stale, unsigned or failed proof blocks the step that depends on it.
  - A successful backup job is not proof without a verified isolated restore.
  - Preparation or qualification never authorizes a change to live infrastructure.
  - Timeouts count as failures. An ownerless or empty suite blocks publication.
  - Skipped or incomplete checks cannot establish success.
- **Isolation evidence:** staging-side denial results become the staging half of the isolation matrix that Epic 7 completes.

## Technical Decisions

- **One deployment definition:** the Aspire model exports the chart.
  - A shared helper adds Dapr annotations, restricted security contexts and HTTPRoutes that point at the environment Gateway.
  - The chart contains no Gateway and no Secret. Images are digest-pinned, renders are deterministic, and render modes are chart values.
  - If export needs hand-modelling, patches or a duplicate topology, the maintained-chart fallback applies.
- **Publication:**
  - SHA-pinned Builds workflows run on disposable hosted runners and verify the intake-pinned module attestations.
  - The signed release record binds every release-invariant value.
  - Executors run `helm upgrade` on the retained package by digest and never regenerate it.
  - Create-only writers and an off-site replica retain artifacts for the backup period or the rollback-target lifetime, whichever is longer.
- **Records:** a record is accepted only when signed by its named writer. Terminal attempts are immutable. A compare-and-set store outside the cluster and executor hosts holds one lock per environment with a monotonic epoch, and every mutation checks it.
- **Tiers:** every object belongs to exactly one tier.
  - Shared infrastructure covers namespaces, environment-identity RBAC, StorageClasses, PriorityClasses, control planes, Keycloak and Rancher.
  - The environment layer covers data services, broker, OpenBao, the tenant-key store, volumes, Gateway and Certificates, Dapr Components/HTTPEndpoints/MCPServers, NetworkPolicies, quotas, limits and bootstrap Secrets. It is rendered from the union of baseline and candidate and applied forward-only by its own attempt before the release attempt.
  - The application package holds workloads, services, HTTPRoutes, Dapr Configurations, WorkflowAccessPolicies, Subscriptions and Resiliency.
  - Dapr activation restarts consuming sidecars on a resource change.
  - The per-job deploy identity cannot write environment-layer objects.
- **Staging runtime:**
  - Pod Security is `restricted`, with default-deny networking and quotas.
  - Volumes are encrypted and data services use native TLS.
  - Dapr is deny-by-default, with HotReload off and all sidecar capabilities dropped.
  - Each app gets its own OpenBao token, and a rotation restarts the pod.
  - The Traefik Gateway API serves exact FQDNs only, with HTTP-01 certificates.
  - A staging-only management client generates the realm.
- **Executor:** a dedicated host accepts only allowlisted jobs. Its credentials are per-job, issued through OIDC and bound to the epoch. Module test and fence code runs sandboxed.
- **Broker:** it must be durable, so Redis pub/sub is excluded. Bounded local or CI experiments never select or qualify it. The five historical rejected guarantees stay REJECTED; bounded experiments do not change their dated dispositions.
- **Management:** Rancher runs privately on a separate management VM. Native administration and recovery stay independent. Fleet never reconciles releases.

## Cross-Story Dependencies

- **Urgent track:**
  - 4.0, 4.2, 4.3, 4.4 and read-only 4.26 proceed independently.
  - 4.27 needs 4.26 and current 4.0 proofs.
  - 4.1 also needs 4.27 and every per-hop gate.
  - 4.28 follows them.
  - Staging foundations (4.14) wait for 4.1–4.3, 4.27 and 4.28.
  - Rancher procurement or availability does not block native retirement or upgrade; public closure remains independently urgent.
- **Before the first publication:** 4.6, 4.7, 4.10 and 4.11 come first, because the release record binds their versions. Publication also runs from 4.8's intake manifest. Chart export qualification (4.5) can precede publication.
- **Staging order:**
  - 4.9's capture-lag bound precedes 4.19.
  - 4.22 needs 4.14–4.21.
  - 4.23 follows on synthetic data, before Epic 5 accepts evidence.
- **External:**
  - Epic 3 supplies the composed host, catalog and local realm. Only 4.24 also waits for McpCli connected mode.
  - EventStore supplies the profile split, composed-host ratification, admission predicate and broker matrix. EventStore and Memories supply image attestations.
