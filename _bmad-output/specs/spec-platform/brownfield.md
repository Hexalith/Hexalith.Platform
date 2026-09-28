# Brownfield

These are current-state facts that affect implementation, observed read-only on 2026-09-27 and 2026-09-28 (spine memlog and this spec's memlog). They describe observations, not capability, so re-verify them before relying on them. The spine's Stack, Structural Seed and Owned work sections carry infrastructure pins and remediation; they are not repeated here.

## Platform repository

- The root `apphost.cs` is a file-based Aspire host on Aspire SDK 13.5.4. It holds an opt-in, Development-only Works preview that resolves a sibling Works checkout and nested EventStore paths (EventStore/admin/Operations, Works, Dapr). It is not the MVP composition and does not provide general module selection. `global.json` pins .NET SDK 10.0.401; architecture update 4 selected no version upgrade.
- The Works preview remains the rollback composition until Works passes its migration parity gate; the Works lane must move off hard-coded sibling paths (spine Owned work, Module adoption).
- Platform declares seventeen `references/`: AI.Tools, Agents, Builds, ChatBot, Commons, Conversations, EventStore, Folders, FrontComposer, McpCli, Memories, Parties, PolymorphicSerializations, Projects, Tenants, Timesheets and Works. `references/Hexalith.McpCli` was added in commit `decb37e`. A declaration does not make a component a running service.
- No hosted Kubernetes or domain configuration exists in the root, and there is no root Platform runtime workflow.
- `DaprSelfHostedMtls.cs` fixes host ports 50001, 51005 and 51006 and the scheduler volume `hexalith-platform-dapr-scheduler`, so concurrent test runs are not safe yet (AD-10; Owned work runner row).
- File-based AppHosts cannot use `DistributedApplicationTestingBuilder` through generated `Projects` types. The runner's Aspire testing-builder compatibility is part of tool ratification (Owned work, First Platform-accepted tool version).

## Builds tooling

- `hexalith-module` (`Hexalith.Builds.Module.Cli`) and `Hexalith.Builds.Evidence.Cli` are already on NuGet, versions 4.20.0 to 4.27.4, built from Builds `main`. None is Platform-accepted, and no workspace pins them. The Builds README still says they are unpublished.
- `SupportedPlatformPins.cs` hard-codes EventStore 3.109.0, Dapr runtime 1.18.2, Dapr SDK 1.18.10 and FrontComposer 4.5.0; the catalog must become the single version authority.
- `hexalith.module-manifest.v1` identifies itself through its `schema` value, forbids additional properties and lacks most Platform declaration fields, so the Platform declaration ships as its next major.
- Builds.Module.AppHost composes its own environment and Builds.Module.EventStoreHost exists; both change or go under tool ratification.
- `CompositionRunState` and its store are metadata-only v1 state. `ModuleCommandExecutionService` still reports `HXR003` on the unqualified descriptor path. The AD-4/AD-5/AD-10 candidate-artifact identity, actual-loaded identity, attachment-hold and withdrawal contracts are therefore target work under tool and runner qualification, not current runner capability.

## Module repositories

- None of EventStore, Memories, Parties, Tenants, Folders or Projects declares Platform as a submodule yet. Adding these direct declarations is owned work in the spine.
- McpCli is declared in Platform at `references/Hexalith.McpCli`. A sibling `../mcpcli` checkout also exists on disk but is never a resolution path (AD-4).
  - McpCli declares `references/Hexalith.Platform`, originally to align SDK pins; the spine keeps it as McpCli's Platform identity.
  - Its own test AppHost (EventStore, Tenants, Parties) migrates to the AD-10 environment descriptor (spine McpCli input row).
  - It offers stdio MCP only; HTTP returns `unsupported_transport`.
  - Profiles use a static bearer token.
  - No production Contracts package is enrolled, so the catalog is empty.
- Source resolution varies by module:
  - EventStore's `RepositoryProjectPaths` probes current, ancestor, sibling and reference paths.
  - The Parties `Directory.Build.props` infers per-dependency source switches from file existence.
  - AD-4 removes both behaviors.
- AppHosts exist in FrontComposer, Parties, Projects, Folders, Tenants, Memories, EventStore, ChatBot, Conversations, Timesheets, Works and Builds.Module, plus Commons.Aspire/ServiceDefaults. Domain-module AppHosts are frozen (spine Migration and coexistence).
- `Folders.EventStore` publishes an image named `eventstore` and registers 13 in-process idempotency-intent adapters. AD-13 replaces this with extension packages and the Platform-published `platform/eventstore` image.
- Hosted HTTP MCP exists in Memories.Mcp (gated) and Parties.Mcp, and EventStore ships Admin.Cli and Admin.Mcp. All are frozen legacy surfaces, never enrolled, deployed or routed in a Platform composition (AD-11).
- Memories imports Redis clients across its server. The migration boundary is AD-9.

## Cluster (`192.168.1.30`)

- There is one Ready node on Kubernetes v1.34.9, with OpenEBS local hostpath storage. This is a single failure domain.
- A `hexalith-memories` namespace exists. No staging/production namespace pair exists.
- One shared OpenBao instance, the Keycloak PostgreSQL cluster and the Memories data workloads all need an environment classification or migration plan before reuse. Keycloak resolves to 26.7.4.
- **Ingress:** no ingress-nginx controller runs. The `nginx-public` IngressClass is served by the Traefik v3.7.13 DaemonSet through its ingress-nginx compatibility provider. Gateway API CRDs are not installed. cert-manager v1.21.2 runs without Gateway API support, and its HTTP-01 ClusterIssuers solve through `nginx-public`.
- **Exposure:** Keycloak admin and master-realm routes and the KubeSphere 4.2.1 console (`kube.hexalith.com`) are publicly reachable.
- **Registry:** Zot 2.1.20 on `registry.hexalith.com` allows anonymous read, and its garbage collection deletes untagged manifests with no retention block.
- **Runtime:** the Dapr sidecar injector does not drop all capabilities. Calico 3.31.3, CloudNativePG 1.30.0 and Velero 1.18.2 are behind their latest releases. The `forgejo-runner` namespace enforces Pod Security `privileged`.
- **Backups:**
  - The only Velero schedule is `forgejo-hourly`, backed up to Scaleway S3.
  - No CloudNativePG backups are configured.
  - OpenBao takes a daily raft snapshot whose coverage has not been verified.
  - Platform, Keycloak and OpenBao have no demonstrated recovery points.

## GitHub

- The Hexalith organization has two owners, and its default repository permission is write.
- The Builds ruleset bypass actors are organization admins and one named member; organization owners can edit rulesets. The spine's First publication row tightens this.
