# Epic 3 Context: Run the complete Hexalith system locally with agent access

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

One command starts all seven MVP modules (EventStore, Tenants, Parties, Folders, Projects, McpCli, Memories), and they become ready within the effective deadline. They run through one Platform-composed `eventstore` gateway host with extension packages, a run-scoped catalog, a local realm generated from the realm contract, and the synthetic tenant. The local McpCli CLI and stdio MCP execute enabled modules' agent-eligible operations and refuse everything else. CI evidence proves exactly which candidate artifacts were loaded. The epic closes with SM-1, SM-2 and SM-3 evidence across all seven modules, which is local MVP acceptance.

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

- **Readiness:** readiness is module-defined, and required startup tasks must succeed; a running process is not ready. The default deadline is 10 minutes. The complete environment uses the largest declared override, which is reported as an override, never as an improvement. Record the actual duration. A timeout names the unready resources.
- **McpCli access:** only enabled modules' agent-eligible commands and queries execute. Refuse UI-only, confirmation-required, disabled-module, digest-mismatched and unauthorized calls, even for users permitted elsewhere. Offline inspection never makes an operation executable. Caller-supplied actor or surface values grant nothing. Each client in the surface map has a negative test, directly and through one cross-module hop.
- **Candidate evidence:** every artifact of the candidate module comes from its own Release build of the committed revision, identified by content. Evidence binds the expected identities to those actually loaded. It refuses missing identities, other builds or revisions, uncommitted inputs and cache substitutes. Baseline-only runs are labelled and never count. Results are never reused across merge or release decisions.
- **Success evidence:** a compact summary of the revision, loaded identities, environment, result and diagnostics, with links to the runs. SM-1 names every operation it demonstrates, so an empty catalog fails. A module that has not adopted stays open, never passed.

## Technical Decisions

- **Composed host:** one `eventstore` app per environment, which is the gateway. Platform assembles it from the EventStore server plus enrolled extension packages, against EventStore's versioned extension API, through the active-root mapping. The current and previous majors are supported. An unsupported major or a duplicate registration fails the build and names the module. Extensions declare no Dapr roles or secrets and reference no provider SDK or Dapr client, except for named Dapr-boundary exceptions. No module artifact may be named `eventstore`. Set the 1,048,576-byte global Kestrel limit before `Build()`, and verify it with the intended route exceptions in both the composed host and TenantRegistry.
- **Routing catalog:** EventStore.Contracts owns the schema and the eligibility vocabulary (agent-eligible, UI-only, confirmation-required, step-up). One deterministic Platform generator builds the catalog from declarations and Contracts; nobody maintains a list by hand. Each route carries a contract-schema digest that also covers its eligibility. Committed idempotency and key entries carry over as non-executable retention entries. Local and CI catalogs are run-scoped and named in the environment descriptor. The gateway enforces eligibility, and its metadata endpoint lists only executable entries.
- **Identity:** the local realm uses the same value-free realm contract as the hosted realms. EventStore owns the claims; Platform owns the instance. The surface classes are `ui` (confidential), `agent` (public, agent-eligible only) and `service` (confidential), with no step-up in the MVP. The surface comes from the authenticated client (`azp`) and attested chain context, never from headers or audience. Clients have full scope off, with explicit audiences and roles. McpCli is an `agent` client without `offline_access`, and its CLI and MCP heads count as one surface. Cross-module calls use downscoped token exchange, and raw token forwarding is forbidden. Until EventStore ships attestation, service clients may call only agent-eligible operations.
- **Synthetic tenant:** one Platform-owned identifier per environment, stored in the descriptor and the realm instance. Tenants' idempotent task creates it once per environment. Synthetic clients are flagged and hold no admin or cross-tenant grant. Modules exclude the tenant by its identifier.
- **Connected McpCli:** a profile binds one gateway, issuer, audience and token source, with no fallback. An operation is executable only when gateway metadata serves it with a matching digest and an eligibility that admits McpCli; McpCli uses the served eligibility. The actor comes from the token. Platform builds a run-scoped, never-published McpCli through the same mapping. Stdio is the only MVP MCP surface, and legacy module MCP/CLI hosts are never enrolled.
- **Candidate policy v1:** a merge uses the committed merge result for its exact head/base pair, and a release uses its exact revision. Each decision needs a fresh run. Descriptors carry the policy, revision, decision and producing run, and disallow reuse. Candidate artifacts replace that module's catalog artifacts in the one mapping shared by the host, McpCli and tests; other modules stay catalog-pinned. For released packages, expected identities come from the lock-verified artifacts, and the files actually loaded are measured. Builds owns the encoding.

## Cross-Story Dependencies

- **Order:** host and catalog first, then the local realm and McpCli connected mode, then candidate evidence, then SM-1 to SM-3. Story 3.1 applies Story 2.1's policy, and 3.9 builds on 3.1.
- **Other epics:** Epic 2 supplies the runner, the descriptor and the lifecycle. Policy v1 must be in the runner contract before CI candidate evidence is accepted. Story 3.11 uses Story 1.13's enrollment kit, and 3.12 repeats Story 2.10's scenarios. Epic 4 needs only the host, catalog and local-realm foundations. Folders' adapters are proven in 3.6, and hosted in 10.1.
- **External prerequisites:** from EventStore, the extension API, catalog schema, gateway metadata endpoint, actor/surface attestation and realm-contract claims. Tenants supplies the synthetic tenant task, and Folders its extension packages. Tenants, Folders and Projects must each adopt. McpCli supplies its contract amendments and migrates its test AppHost to the descriptor.
- **Done:** a story tagged with another repository is done only when it is merged upstream, released, pinned in Platform and verified from the Platform workspace.
