# Epic 4 Context: Publish retained releases and run the isolated staging environment

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Publish one attested, immutable release: the application Helm package, the composed `platform/eventstore` image, the McpCli candidate and a signed release record. A dedicated staging executor deploys it by digest into an isolated staging environment on `hexalith.com`. Team members use the reference composition (Parties, EventStore, Tenants, Memories) and McpCli there with staging-only permissions. Urgent infrastructure work comes first, because staging cannot be built on the current cluster: verified backups, KubeSphere retirement, the upgrade off Kubernetes 1.34, closing public admin exposure, runner relocation, repository controls and private Rancher. The epic's contracts, staging restore and isolation proofs underpin Epic 5's exact-release evidence. Folders and Projects join in Epic 10.

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

- **Separation:** staging and the later production environment (`tache.ai`) share one installation. Each keeps its own data, credentials, namespaces, realm, OpenBao, Gateway and Dapr trust domain. Staging never claims production names, administers production or exhausts its capacity. CI holds no hosted-environment credentials.
- **Exposure and readiness:** only declared public interfaces are routed at staging FQDNs, and required workloads are ready within 10 minutes of attempt start.
- **McpCli:** staging behaves as it does locally. An operation is executable only when the staging gateway serves it with a matching digest and eligibility. Ineligible, disabled, mismatched and unauthorized calls are refused. Tokens are short-lived and per environment. Refresh material stays in the OS credential store, and there is no `offline_access`.
- **Kubernetes upgrade:** Kubernetes 1.34 reached end of life on 2026-10-27, and staging waits for the supported upgrade. The mutation needs current signed off-node, restore-verified proofs for Keycloak PostgreSQL, OpenBao and Memories, accepted KubeSphere retirement and a fresh etcd recovery point. Preparation or qualification never authorizes it, and a successful backup job is not proof.
- **Admin exposure:** close the public admin and cluster-management routes now, proven by external probes. Do not break public OIDC or wait for Rancher.
- **Fail closed:** missing, stale, unsigned or mismatched evidence or attestations block progress, as do ownerless or empty suites and unmapped flows. A module without recovery hooks is reported as unqualified.

## Technical Decisions

- **Build once, deploy by digest.**
  - The one Platform Aspire model exports the chart. A shared helper adds Dapr annotations, `restricted` security contexts and HTTPRoutes parented to the environment's Gateway. Render modes (normal, recovery, remove-workloads) are chart values.
  - The chart has no Gateway (work around Aspire 13.5.4 `AddGateway`) and no Secret. Images are digest-pinned, and renders are deterministic.
  - Hand-modelled Dapr, a custom compiler, recurring patches or a duplicate topology trigger the maintained-chart fallback.
  - Executors `helm upgrade` the retained package by OCI digest, never with `aspire publish` or `aspire deploy`. Helm history is never the record.
- **Publication.** A public workflow runs on hosted runners through SHA-pinned Builds workflows. It attests every artifact, verifies the intake-pinned module attestations and signs the release record, which binds every release-invariant value. Retained artifacts are create-only and replicated off-site by a separate writer. They are kept for the backup retention period or their life as a rollback target, whichever is longer.
- **Records.**
  - Every record has a versioned schema. It is accepted only when signed by its named writer, never on the strength of store access.
  - Terminal attempts are immutable.
  - Administrator records use an identity that no executor, workflow token, monitor or deputy holds.
  - Values are release-invariant, environment-current (passed as chart values at deploy) or attempt-bound.
  - A compare-and-set store outside the cluster and the executor hosts holds one lock per environment. Its monotonic epoch is checked before every commit.
- **Tiers.** Each object belongs to exactly one tier:
  - *Shared infrastructure:* the cluster, Rancher, namespaces, RBAC, StorageClasses, CNI, Gateway API, cert-manager, the Dapr control plane and Keycloak.
  - *Environment layer:* data services, broker, OpenBao, volumes, Gateway, Dapr Components, NetworkPolicies and quotas. Its own identity applies it forward-only, in its own attempt before the release attempt, from the union of the baseline and candidate records.
  - *Application package:* deployed by a per-job identity that can write neither of the other tiers.

  Definitions live in the private operations repository. Any Dapr resource change restarts the consuming sidecars before readiness.
- **Staging baseline.**
  - Pods: Pod Security `restricted`, default-deny networking enforced by the CNI, quotas and a lower priority.
  - Storage: a staging-only StorageClass, volume encryption and native TLS to data services.
  - Secrets: staging's own OpenBao through Dapr `secretstores.hashicorp.vault` with per-app tokens, plus a separate tenant-key store.
  - Dapr: its own trust domain, deny-by-default policy and HotReload off.
  - Broker: durable, never Redis pub/sub.
  - Ingress: Gateway API on Traefik with exact staging FQDNs. Nothing is rendered on `nginx-public`.
  - Telemetry: a per-environment sink rendered by Platform.
- **Identity.** A staging-only management client generates the realm from the versioned, value-free realm contract. Each surface class has its own client. Synthetic clients hold no admin or cross-tenant grant. Staging has no Keycloak backup or restore access.
- **Executor.**
  - It runs on a dedicated host and accepts allowlisted jobs only.
  - Credentials are per job, revocable and epoch-bound, issued through GitHub OIDC and never stored as repository secrets.
  - Module smoke, E2E and fence code runs sandboxed. It gets only synthetic tokens and the verification endpoint.
  - A takeover fences before it mutates.
- **Rancher.** It runs on a private single-node K3s management VM and imports the kubeadm cluster. Native administration and etcd recovery stay authoritative and independent of it. Fleet never reconciles application releases. Only Administrator changes management grants.

## Cross-Story Dependencies

- **Urgent order:**
  1. 4.0, 4.2, 4.3, 4.4 and the read-only part of 4.26 run first, independently.
  2. 4.27, which needs 4.26 and current 4.0 proofs.
  3. The 4.1 mutation.
  4. 4.28.
  5. 4.14.

  Rancher availability never blocks the retirement, the upgrade or closing the public routes.
- **First publication:** 4.6, 4.7, 4.8, 4.10 and 4.11 come before 4.12. 4.5 needs no published artifact; 4.12 proves attestation and 4.13 proves restore from the replica.
- **Staging build-out:**
  - 4.9's capture-lag bound comes before 4.19.
  - 4.14–4.21 come before 4.22.
  - 4.23 follows 4.22 on synthetic data, before Epic 5 accepts any evidence.
  - 4.25 feeds Epic 7.
  - The deputy's key custody in 4.14 assumes Story 6.3.
- **Epic 3:** Epic 4 needs the composed host, the catalog generator (3.3) and the local realm. Only 4.24 waits for connected McpCli.
- **External prerequisites:**
  - From EventStore:
    - the AD-26 profile split, for 4.9;
    - AD-13 ratification and issuer registration, for 4.12;
    - the admission predicate with its staging clause, for 4.19.
  - EventStore and Memories image attestation, for 4.8 and 4.12.
  - Builds implements 4.6 and 4.11.
  - EventStore's 4.16 qualification is unrelated to Platform 4.16.
  - Module recovery hooks are not needed until Epics 5 and 8.
