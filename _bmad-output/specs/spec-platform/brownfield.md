# Brownfield

These are current-state facts that affect implementation. All were observed read-only on 2026-09-27. They describe observations, not capability, so re-verify them before relying on them. Infrastructure pins and currency gaps are listed in the spine's Stack, Structural Seed and Deferred sections and are not repeated here.

## Platform repository

- The root `apphost.cs` is a file-based Aspire host. It holds an opt-in, Development-only Works preview that resolves a sibling Works checkout and nested EventStore paths (EventStore/admin/Operations, Works, Dapr). It is not the MVP composition and does not provide general module selection.
- The Works preview remains the rollback composition until Works passes its migration parity gate; the Works lane must move off hard-coded sibling paths (spine Owned work).
- Platform declares sixteen `references/`: AI.Tools, Agents, Builds, ChatBot, Commons, Conversations, EventStore, Folders, FrontComposer, Memories, Parties, PolymorphicSerializations, Projects, Tenants, Timesheets and Works. A declaration does not make a component a running service.
- No hosted Kubernetes or domain configuration exists in the root, and there is no root Platform runtime workflow.
- `DaprSelfHostedMtls.cs` fixes host ports 50001, 51005 and 51006 and the scheduler volume `hexalith-platform-dapr-scheduler`, so concurrent test runs are not safe yet (AD-10 reconciliation).
- File-based AppHosts cannot use `DistributedApplicationTestingBuilder` through generated `Projects` types. AD-10's thin lifecycle adapter must account for this.

## Module repositories

- None of EventStore, Memories, Parties, Tenants, Folders or Projects declares Platform as a submodule yet. Adding these direct declarations is owned work in the spine.
- McpCli's canonical repository is the sibling `../mcpcli`, which Platform does not declare.
  - It declares `references/Hexalith.Platform`, added only to align SDK pins; the spine removes it as unused.
  - Its own spine keeps a separate test AppHost (EventStore, Tenants, Parties), which conflicts with AD-10 (see the SPEC open question).
  - It offers stdio MCP only; HTTP returns `unsupported_transport`.
  - Profiles use a static bearer token.
  - No production Contracts package is enrolled, so the catalog is empty.
- Source resolution varies by module:
  - EventStore's `RepositoryProjectPaths` probes current, ancestor, sibling and reference paths.
  - The Parties `Directory.Build.props` infers per-dependency source switches from file existence.
  - AD-4 removes both behaviors.
- AppHosts exist in FrontComposer, Parties, Projects, Folders, Tenants, Memories, EventStore, ChatBot, Conversations, Timesheets, Works and Builds.Module, plus Commons.Aspire/ServiceDefaults. Domain-module AppHosts are frozen (spine Migration and coexistence).
- `Folders.EventStore` publishes an image named `eventstore` and registers 13 in-process idempotency-intent adapters. AD-13 replaces this with extension packages.
- Hosted HTTP MCP exists in Memories.Mcp (gated) and Parties.Mcp. None of it is routed in Platform compositions (AD-11).
- Memories imports Redis clients across its server. The migration boundary is AD-9.

## Cluster (`192.168.1.30`)

- There is one Ready node on Kubernetes v1.34.9, with OpenEBS local hostpath storage. This is a single failure domain.
- A `hexalith-memories` namespace exists. No staging/production namespace pair exists.
- One shared OpenBao instance, the Keycloak PostgreSQL cluster and the Memories data workloads all need an environment classification or migration plan before reuse.
- Backup coverage is limited:
  - The only Velero schedule is `forgejo-hourly`, backed up to Scaleway S3.
  - No CloudNativePG backups are configured.
  - OpenBao takes a daily raft snapshot whose coverage has not been verified.
  - Platform, Keycloak and OpenBao have no demonstrated recovery points.
