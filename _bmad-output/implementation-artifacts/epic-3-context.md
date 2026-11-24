# Epic 3 Context: Run the complete Hexalith system locally with agent access

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

A developer starts all seven MVP modules (EventStore, Tenants, Parties, Folders, Projects, McpCli, Memories) with one command, ready within the effective deadline. They run through one Platform-composed `eventstore` gateway host with extension packages, a run-scoped catalog, a local realm generated from the realm contract and the synthetic tenant. The local McpCli CLI and stdio MCP discover and execute agent-eligible operations and refuse everything else. CI candidate evidence proves exactly which artifacts were loaded. The epic closes with SM-1, SM-2 and SM-3 evidence across all seven modules.

## Stories

- Story 3.1: Apply the candidate-selection policy to the runner contract
- Story 3.2: Compose the eventstore host with enrolled extension packages
- Story 3.3: Generate the run-scoped routing catalog
- Story 3.4: Generate the local realm from the realm contract
- Story 3.5: Provide the synthetic tenant in local and CI environments
- Story 3.6: Run all seven MVP modules together locally
- Story 3.7: Discover enabled operations through connected McpCli
- Story 3.8: Execute and refuse operations through local McpCli CLI and MCP
- Story 3.9: Prove candidate artifact identity in CI evidence
- Story 3.10: Record SM-1 evidence for the complete local environment
- Story 3.11: Record SM-2 evidence across every workspace
- Story 3.12: Record SM-3 evidence across every MVP module

## Requirements & Constraints

- **Readiness:** module-defined readiness and startup tasks; a running process is not ready. Default deadline 10 minutes; the complete environment uses the largest declared override, reported as an override, never as an improvement. Record actual duration. Timeouts name unready resources.
- **McpCli:** executes only enabled modules' agent-eligible commands and queries. UI-only, confirmation-required, disabled-module, digest-mismatched and unauthorized calls are refused, even for users permitted elsewhere. Offline inspection never makes anything executable. Caller-supplied actor or surface values grant nothing; the server checks identity, surface and permissions on every call.
- **Candidate evidence:** every candidate-module artifact (Contracts, server and extension packages, services, tools) comes from its own Release build of the committed revision, identified by content. Evidence binds expected to actually loaded identities, including inside the composed host and run-scoped McpCli. Refuse missing identities, other builds or revisions, uncommitted inputs and cache substitutes. Baseline-only runs are labelled and never count.
- **Success evidence:** records revision, loaded identities, environment, result and diagnostics. SM-1 names every demonstrated operation (an empty catalog fails). A module that has not adopted stays open, never passed.

## Technical Decisions

- **Composed host:** exactly one `eventstore` app per environment, the gateway, assembled by Platform from the EventStore server plus enrolled extension packages against EventStore's versioned extension API, through the active-root mapping. Unsupported extension-API majors (current and previous are supported) and duplicate registrations fail the build, naming the module. Extensions declare no Dapr roles or secrets and reference no provider SDK or Dapr client, except named Dapr-boundary exceptions. No module image may be named `eventstore`. Configure the 1 MiB global Kestrel limit before `Build()` and verify it, with intended route exceptions, in the composed host and TenantRegistry.
- **Routing catalog:** EventStore.Contracts owns the schema and eligibility vocabulary (agent-eligible, UI-only, confirmation-required, step-up). One deterministic Platform generator builds it from declarations and Contracts. Each route carries a contract-schema digest covering its eligibility. Committed idempotency and key entries carry over as non-executable retention entries. Local and CI catalogs are run-scoped and named in the environment descriptor. The gateway enforces eligibility; its metadata endpoint lists only executable entries. No hand-maintained list.
- **Identity:** the local realm uses the same value-free realm contract as hosted realms; EventStore owns claims, Platform the instance. Classes are `ui` (confidential), `agent` (public, agent-eligible only) and `service` (confidential); no step-up in the MVP. The surface comes from the authenticated client (`azp`) and attested chain context, never headers or audience. McpCli is an `agent` client without `offline_access` or full scope, and its CLI and MCP heads are one surface. Cross-module calls use downscoped token exchange; raw token forwarding is forbidden.
- **Synthetic tenant:** one Platform-owned identifier per environment, in the descriptor and realm instance, created once by Tenants' idempotent task. Synthetic clients are flagged, with no admin or cross-tenant grant; modules exclude the tenant by identifier.
- **Connected McpCli:** a profile binds one gateway, issuer, audience and token source, with no fallback. An operation is executable only when gateway metadata serves it with a matching digest and McpCli-admitting eligibility; McpCli uses the served eligibility. The actor comes from the token. Platform builds a run-scoped, never-published McpCli through the same mapping.
- **Candidate policy:** candidate artifacts replace that module's catalog artifacts in the one mapping shared by host, McpCli and tests; other modules stay catalog-pinned. Under candidate-and-attachment policy v1, a merge uses the committed merge result for its exact head/base pair and a release its exact revision. Each decision needs a fresh run; descriptors carry policy, revision, decision and producing run, with reuse disallowed. Builds owns the encoding.

## Cross-Story Dependencies

- **Order:** host and catalog, then local realm and McpCli connected mode, then candidate evidence, then SM-1 to SM-3. Story 3.1 implements Story 2.1's decision; Story 3.9 builds on 3.1.
- **Other epics:** runner, descriptor and lifecycle come from Epic 2; Story 3.12 repeats Story 2.10's scenarios. Epic 4 needs only the host, catalog and local-realm foundations. Folders' adapters are proven in Story 3.6 (local) and 10.1 (hosted).
- **External prerequisites:** EventStore's extension API, catalog schema, gateway metadata endpoint and actor/surface attestation; realm-contract claims; Tenants' synthetic tenant task; Folders and Projects adoption and Folders' extension packages; McpCli contract amendments and test-AppHost migration to the descriptor.
- **Done:** a story tagged with another repository is done only when merged upstream, released, pinned in Platform and verified from the Platform workspace.
