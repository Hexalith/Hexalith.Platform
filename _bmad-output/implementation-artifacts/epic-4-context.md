# Epic 4 Context: Publish retained releases and run the isolated staging environment

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Publish an immutable, attested Helm package, composed image, McpCli candidate and release record, then deploy exact digests into isolated staging on `hexalith.com`. First secure/upgrade the single-node installation, preserve workloads through KubeSphere retirement and qualify private Rancher with independent native access.

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

- Leave Kubernetes 1.34 before its 2026-10-27 end of life and before staging. The in-place hop interrupts all workloads; delay blocks staging. Preparation and the approved October 1 topology decision do not authorize live deletion, installation or upgrade.
- Preserve completed 4.0 encrypted immutable off-node backups and signed isolated restores for Keycloak PostgreSQL, OpenBao and Memories. Historical missing-proof observations are superseded; mutations require current signatures, recovery-point validation and independent off-node readability. Missing/stale/failed proof closes the gate.
- Preserve namespaces, PVC identities/bindings and shared dependencies. Unknown ownership/deletion effects block retirement. Afterwards, refresh external-etcd/node/configuration recovery, remaining compatibility/admission/consistency checks, authenticated smokes and the exact approved upgrade procedure. Preserve historical signed evidence/digests; retirement alone never opens a hop gate.
- Close public Keycloak admin/master-realm and management routes independently of console replacement; prove private administration/public denial while public OIDC works. Disable anonymous registry reads after consumer credentials work; preserve retained digests through garbage collection.
- Move the privileged Forgejo runner onto a separate machine/VM shared with no executor. Publication/operations repositories and release tags require the approved named-writer controls.
- Staging cannot claim production data, secrets, trust, hostnames or administration. Prove negative isolation across users, pods, identities, networking, storage, restored copies and management UI/API/proxy credentials.
- Data-service connections use native TLS where the provider supports it, including CloudNativePG PostgreSQL; other data traffic stays within the environment data namespace under default-deny network policy.

## Technical Decisions

- One Aspire model exports Parties, EventStore, Tenants and Memories. Recurring hand modelling/patches, duplicate topology or a custom compiler trigger the maintained-chart fallback. Charts contain digest-pinned workloads/HTTPRoutes, no Gateway or Secret values; environment-layer writers own infrastructure/bindings.
- Build/attest once; deploy verified retained artifacts without rebuilding. Replicate off the primary failure domain. Versioned signed records enforce entitled writers. An external compare-and-set store serializes each environment with monotonic epochs checked before every mutation.
- Separate application/data namespaces, per-environment OpenBao, tenant-key custody, data instances, Keycloak realms, Gateway hostnames/certificates and Dapr trust domains enforce isolation. Use Pod Security `restricted`, default-deny networking, scoped Dapr callers, disabled HotReload and node-level volume encryption. Shared single-node/kernel availability limits remain explicit.
- Staging has a separate private-network executor. Module code runs in a credential-isolated sandbox; staging/test code never shares a host holding production authority. Suites bind release, suite, profile and configuration identities.
- Private Rancher community on a dedicated single-node K3s VM registers existing kubeadm. Qualify exact chart/images/licenses, current management/workload pairing, capacity/placement and management installation/recovery tools independently of application tooling. Generic import does not certify hosting; same-host VMs provide no site resilience.
- Management is shared infrastructure outside Aspire and the retained application package. Inventory required agents, narrowly approved privileges and recovery inputs. Preserve native maintenance/external-etcd access; Fleet and unattended workload upgrades must not become competing writers.
- Administrator alone changes management grants. MFA, least privilege, independently retained grant/revocation lineage and native kubeconfig/key custody preserve the deputy's scoped recovery role. Missing/conflicting authority fails closed. Management backup is distinct from downstream business-data/external-etcd and full management-cluster recovery; restore in isolation, fence stale manager/agent authority and reapply revocations before reconnecting.

## UX & Interaction Patterns

Operators use the tested private native path during transition, then the qualified private Rancher endpoint with explicit roles and a manager-unavailable procedure. Staging membership or application admission conveys no management authority. McpCli selects one staging profile and exposes only operations enabled and authorized by that environment.

## Cross-Story Dependencies

- 4.0, 4.2, 4.3, 4.4 and read-only 4.26 preparation proceed independently. Execution follows **4.26 qualification and accepted 4.2 console closure → 4.27 retirement with fresh production recovery through 4.0 → 4.1 supported native hop → 4.28 private Rancher/access/initial restore qualification → 4.14 staging foundations**. Current validated 4.0 proofs gate retirement and the hop; every existing per-hop gate remains. Rancher procurement/availability does not block native retirement or upgrade.
- Record/store, intake, runtime/identity, recovery and check-suite contracts precede retained publication and deployment. Early Helm qualification excludes future publication/replication checks. Environment isolation, secrets, Dapr, broker, Gateway, realm, executor and telemetry precede reference deployment; data-restore and McpCli/isolation verification precede accepted candidate evidence.
- Rancher qualification enters the shared profile inventory and G1 prerequisites. Epic 8 extends initial management restore proof into integrated retention, fencing/revocation and measured RPO/RTO; native workload recovery remains manager-independent. Epic 9 adds the management stack to infrastructure currency checks.
