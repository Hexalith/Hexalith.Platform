# Epic 4 Context: Publish retained releases and run the isolated staging environment

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Publish an attested immutable application Helm package, composed EventStore image, McpCli candidate and signed release record. A dedicated executor deploys those artifacts by digest into isolated staging on `hexalith.com`, where the reference composition is usable without production authority. Verified recovery, urgent infrastructure corrections and private management precede staging. This foundation supplies exact-release evidence for later promotion gates.

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

Staging and production share infrastructure while retaining distinct data, credentials, namespaces, realms, Gateways, OpenBao instances and Dapr trust domains. Staging cannot claim production names, administer production or exhaust its capacity. CI receives no hosted-environment credentials. Only declared public interfaces are routed; required workloads must become ready within ten minutes.

McpCli requires the selected environment's matching gateway operation digest and eligibility. Disabled, mismatched, unauthorized and ineligible operations are refused. Hosted tokens are short-lived and environment-specific; refresh material belongs in the OS credential store without `offline_access`.

Missing, stale, unsigned or mismatched evidence blocks acceptance. Required suites have accountable owners, stable checks and immutable identities; skipped or incomplete checks cannot establish success. Backup-job success is insufficient without verified isolated restoration. Infrastructure preparation and qualification alone never authorize mutation.

## Technical Decisions

- **One deployment definition:** Platform's Aspire model exports the application chart. A shared helper supplies Dapr annotations, restricted pod security and HTTPRoutes pointing to the environment Gateway. The chart contains neither Gateway nor Secret; data-service parameters carry endpoints only. Images are digest-bound, renders deterministic and normal/recovery/workload-removal modes are values. Recurring generated patches, a custom compiler or duplicate topology trigger the maintained-chart fallback.
- **Retained publication:** SHA-pinned Builds workflows run on disposable hosted workers. Publication verifies intake-pinned module attestations, attests artifacts and signs the release record. Executors apply retained Helm artifacts by digest without regeneration. Create-only writers and an independent off-site replica preserve artifacts and evidence for the backup period or rollback-target lifetime, whichever is longer.
- **Records and serialization:** Versioned records verify against their named writer. Terminal attempts are immutable. Administrator signing authority is unavailable to executors, workflow tokens, monitors and deputies. An external compare-and-set store provides one environment lock and monotonic epoch; every mutation checks it.
- **Ownership tiers:** Namespaces, RBAC, storage classes and control planes are shared infrastructure. Data services, broker, secrets, volumes, Gateway and Dapr Components are environment-layer objects. Workloads, HTTPRoutes, Configurations, Subscriptions and Resiliency are application-package objects. Environment-layer changes apply forward before release attempts; application rollback preserves persistent state. Operations definitions live in the private operations repository. Effective Dapr resource changes restart consuming sidecars before qualification.
- **Staging isolation:** Enforce restricted pods, default-deny networking, quotas and lower priority; use staging storage, volume encryption and native data-service TLS. Dapr denies by default with HotReload disabled. OpenBao uses per-app tokens and a separate tenant-key store. Gateway API uses Traefik and exact environment FQDNs. Identity provisioning has staging-only management authority.
- **Broker evidence:** The production broker must be durable; Redis pub/sub is excluded. Platform owns configuration and bounded hosted-worker evidence; EventStore owns its pinned producer/verifier and full broker matrix. Local/CI experiment acceptance confers no production selection, exception or staging qualification. Historical rejected guarantees remain rejected until qualifying evidence changes their disposition.
- **Executors and management:** Dedicated executors accept allowlisted jobs, acquire short-lived epoch-bound credentials through OIDC and sandbox module code. Rancher runs privately on a separate management VM; native Kubernetes administration and recovery remain independent. Fleet does not reconcile application releases.

## Cross-Story Dependencies

Urgent backup, exposure, runner and repository controls proceed independently with read-only management qualification. Retirement requires qualified management migration plus current recovery proofs. The supported Kubernetes hop additionally requires accepted retirement and all per-hop gates; private Rancher deployment then precedes staging foundations. Rancher procurement never delays public closure or native retirement.

Publication needs record encoding, lock/store, intake and check/recovery contracts; chart export qualification can precede publication. EventStore must supply the hosted profile split, attestation ratification and admission contract; EventStore and Memories supply image attestations. Builds owns shared record and check-suite contracts.

Staging foundations and environment services precede the first deployment. Synthetic staging restore follows deployment before release evidence is accepted. Isolation proofs feed production qualification. The capture-lag decision precedes staging identity export; connected McpCli is required for its hosted acceptance. Epic 3 supplies composition, catalog generation and local realm; Epic 10 later adds Folders and Projects to the reference release.
