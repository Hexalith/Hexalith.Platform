# Epic 4 Context: Publish retained releases and run the isolated staging environment

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Publish attested, immutable releases (application Helm package, composed `platform/eventstore` image, McpCli candidate, signed release record) and deploy them by digest into an isolated staging environment on `hexalith.com`. Team members use the reference composition (Parties, EventStore, Tenants, Memories) and McpCli there with staging permissions only. Staging never claims production names, data or authority. Urgent cluster work comes first because staging cannot open without it: off-node backups, closing public exposure, the runner move, repository controls, KubeSphere retirement, the Kubernetes upgrade and private Rancher. The resulting publication, records, store, contracts and staging foundation underpin Epics 5 and 6. Folders and Projects join in Epic 10.

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

- Staging has its own data, credentials, realm, application and data namespaces, StorageClass, Gateway, OpenBao, tenant-key store and Dapr trust domain. Quotas and a lower PriorityClass protect production. The single shared node gives no node or site resilience.
- Route and advertise only declared public interfaces. Every required workload reaches the intended release and readiness within 10 minutes of deployment start.
- CI holds no hosted-environment credentials, only a read-only registry pull credential. No deployment credential is a repository secret.
- Prove isolation through recorded denials. These form the staging half of the NFR-3 matrix that Epic 7 completes.
- Missing, stale, unsigned, failed, mismatched, skipped or incomplete evidence never passes, and timeouts fail. A backup counts only with a verified isolated restore. Preparation or qualification never authorizes live mutation.
- Story 4.1 records the actual supported Kubernetes minor and current patch. Retain post-upgrade evidence that Keycloak, OpenBao, Memories and Forgejo are healthy or were restored. Story 4.3 removes the in-cluster privileged runner namespace and proves the relocated runner succeeds without access to the cluster API or namespaces, on a host shared with neither the cluster nor an executor.
- Every required critical-flow and smoke suite for the reference composition and McpCli needs an owner, non-empty checks and a resolved flow mapping. Otherwise publication blocks.
- The hosted broker is not Redis pub/sub. It must prove at-least-once delivery, message identity, deduplication, dead-lettering, retention and backlog recovery.
- Prove the staging in-place restore on the first, synthetic-data deployment before Epic 5 evidence. A module without hooks is reported as unqualified.

## Technical Decisions

- **One definition:** the Platform Aspire model feeds the maintained Helm chart, chosen because native export dropped the Dapr annotations. The retained package is the only hosted deployment definition. A shared helper adds Dapr annotations and restricted security contexts, and HTTPRoutes reference the environment's namespaced Gateway. The chart contains no Gateway and no Secret, pins images by digest and renders deterministically. Normal, recovery and remove-workloads are chart values. Application renders contain zero Dapr Components, and the environment layer owns all eight.
- **Tiers:** the application package holds workloads, services, HTTPRoutes, Dapr Configurations, WorkflowAccessPolicies, Subscriptions and Resiliency. The environment layer holds data services, broker, OpenBao, the key store, volumes, Gateway and Certificates, Dapr Components and endpoints, NetworkPolicies, quotas and bootstrap Secrets. It is rendered from the baseline/candidate union and applied forward-only before the release attempt. Shared infrastructure covers namespaces, RBAC, storage and priority classes, Kubernetes, Rancher, CNI, Traefik, cert-manager, the Dapr control plane and Keycloak. Each tier has its own writer, and the application deploy identity cannot write environment-layer objects. Definitions live in the private operations repository.
- **Publish and deploy:** a public workflow on hosted runners uses SHA-pinned Builds workflows. It attests every artifact and refuses unverified module attestations before writing a record. The signed record binds every release-invariant value, including contract versions and suite digests. Classification is the chain maximum since the working baseline. Executors run `helm upgrade` by digest, never `aspire publish`/`deploy`, and Helm history is never the record.
- **Retention:** `registry.hexalith.com` serves immutable, digest-addressed artifacts to authenticated, per-environment pull identities. Create-only writers cannot overwrite or delete retained content. Garbage collection preserves retained digests, and the off-site replica has a separate writer. Retain each artifact for the backup period or its rollback-target life, whichever is longer. `registry.tache.ai` is never a retained store.
- **Records and store:** records are versioned, name their writer and are accepted only on that writer's signature. Terminal attempts are immutable. A compare-and-set store outside the cluster and executors holds one lock per environment, with a monotonic epoch checked on every mutation. Staging's stop never touches production. Staging-evidence policy v2 reservations and conditional transitions apply only after explicit adoption.
- **Runtime:** staging uses the production profile template with its own environment bindings. The profile inventory pins providers, the exact Helm version and the full Dapr identity: chart, CRDs, images, embedded components and configuration. The profile digest covers only the template and those pins. Dapr 1.19.2 is the hosted candidate. Stories 4.5, 4.16 and 4.17 must qualify that same inventory. The hosted catalog is generated, and the secret contract is value-free with an environment dimension. Every expiring credential has a renewal owner.
- **Dapr and secrets:** each environment has its own trust domain, deny-by-default Configurations, callers allowed by trust domain, namespace and app ID, HotReload off and scoped WorkflowAccessPolicies. Sidecars drop all capabilities. Any Dapr resource change restarts consuming sidecars before readiness. Each app reads secrets with a per-app OpenBao token mounted per pod, and rotation restarts the pod. A missing secret generation fails readiness. Pod Security is `restricted`, with proven default-deny networking, encrypted volumes and native data-service TLS where the provider supports it, including CloudNativePG.
- **Ingress and identity:** each environment's Traefik Gateway and listener Certificates belong to its environment layer. The Gateway carries exact-FQDN listeners and admits application HTTPRoutes only from its own application namespace, with same-namespace backends. Its port-80 listener also admits ACME solver routes. Staging Certificates use the staging HTTP-01 Issuer, with no DNS-01, no ClusterIssuer and nothing on `nginx-public`. User-ingress changes happen only within an attempt. A staging-only client generates the staging realm forward-only from the realm contract. Synthetic clients are flagged with no admin grants, and staging has no Keycloak backup access.
- **Executor:** a dedicated host shared with no runner or executor enforces job allowlists. It issues revocable, epoch-bound per-job credentials through OIDC claim checks. Module code runs sandboxed with only the verification endpoint and short-lived synthetic tokens. A takeover fences every affected authority before mutating.
- **Management:** Rancher runs on a private K3s VM and imports the kubeadm cluster. Native administration and etcd recovery stay independent, and Fleet never owns application releases.

## UX & Interaction Patterns

- A McpCli staging profile binds one gateway, issuer and audience. Short-lived tokens go only to that gateway, refresh material stays in the OS credential store, and there is no `offline_access`.
- McpCli refuses ineligible, disabled, mismatched and unauthorized operations exactly as it does locally. The gateway re-authorizes every call and never logs tokens.

## Cross-Story Dependencies

- **Urgent track:** 4.0, 4.2, 4.3, 4.4 and read-only 4.26 can start independently. Before 4.27 executes, accept 4.26 qualification and 4.2 public-console closure and complete a fresh production external-etcd recovery point and fenced isolated restore under the 4.1 contract and 4.0 proof and custody policy. After accepted 4.27 retirement, refresh recovery inputs and clear each 4.1 hop gate before the supported native upgrade. Story 4.28 follows 4.1. Rancher procurement never blocks retirement or the upgrade. [Operations sequence](../../eng/cluster-management/README.md).
- **Staging:** 4.14 needs accepted 4.1, 4.2, 4.3, 4.27 and 4.28. 4.22 follows 4.14–4.21, and 4.23 needs 4.22 and 4.10.
- **Publication:** 4.12 needs 4.6, 4.7, 4.8, 4.10 and 4.11. It also needs EventStore and Memories image attestation, EventStore AD-13 ratification and issuer registration. 4.5 can run earlier. 4.13 retrieves 4.12's digest off site.
- **Runtime:** 4.9 needs EventStore's profile template and binding split. Its capture-lag bound precedes 4.19 and feeds the G1 bounds. 4.19 needs EventStore's staging admission predicate. 4.17 is shared with EventStore 3.21.
- **Other epics:** Epic 3 supplies the composed host, the catalog generator and the realm foundations. 4.24 needs connected McpCli. Epic 5 consumes the suites, the contract and the restore. Epic 7 extends 4.25, and 6.7 needs the accepted staging foundation.
