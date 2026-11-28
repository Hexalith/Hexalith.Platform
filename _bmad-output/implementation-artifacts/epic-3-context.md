# Epic 3 Context: Run the complete Hexalith system locally with agent access

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

A single command starts all seven MVP modules (EventStore, Tenants, Parties, Folders, Projects, McpCli and Memories) with their supporting components. They run through:

- one Platform-composed `eventstore` gateway, which loads extension packages including Folders' adapters
- a run-scoped routing catalog
- a contract-generated local realm
- the synthetic tenant

Developers and agents discover and execute agent-eligible operations through the run-scoped McpCli CLI and stdio MCP. Everything else is refused. CI evidence proves exactly which candidate artifacts were loaded. SM-1, SM-2 and SM-3 evidence across all seven modules closes local MVP acceptance.

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

- **Readiness:** a service is ready when its module-defined check passes and its startup tasks succeed. A required service without a readiness declaration is a configuration error. The deadline defaults to 10 minutes; the complete environment uses the largest declared override and reports it as one. Record the actual duration, and name unready resources and reasons on timeout. A raised timeout is never an improvement.
- **Operation access:** only agent-eligible operations of enabled modules execute, within the caller's permissions. Refuse UI-only and confirmation-required operations, even for users permitted elsewhere. Never enable or reroute a disabled module. Unknown or digest-mismatched contracts are non-executable, and offline inspection never establishes availability. Caller-supplied actor or surface values grant nothing. No hosted McpCli service is involved.
- **Candidate evidence:** a merge uses the committed merge result of its exact head/base pair; a release uses its exact committed revision. Each decision needs a fresh run, with no reuse across decisions even when the bytes match. Record the policy identifier, revision, decision and producing run. Identify artifacts by content, and refuse a missing identity, another build or revision, an uncommitted input or a cache-substituted package. A run without a candidate is labelled baseline-only and never counts for acceptance.
- **Evidence records** hold the source revision, loaded identities, effective deadline, duration, result and diagnostics. SM-1 names every operation and refusal it demonstrates, so an empty catalog fails. SM-2 covers the four domain workspaces, plus EventStore, Memories and McpCli from the Platform workspace. SM-3 runs isolated tests first and repeats the lifecycle and attachment scenarios after any lifecycle change. A module that has not adopted stays open, never passed.

## Technical Decisions

- **Composed host:** each environment has exactly one `eventstore` app, the gateway, built from the EventStore server plus extension packages through the active-root mapping in both modes. The build fails, naming the module, on an extension-API major outside current/previous or on a duplicate registration. Extensions declare no Dapr roles or secrets and use no provider SDK or Dapr client, except named AD-9 exceptions. No module may publish an `eventstore` image. `Folders.EventStore` currently does, so its adapters must become extension packages.
- **Routing catalog:** generated deterministically from declarations and Contracts (EventStore.Contracts schema), never maintained by hand. Each route's contract-schema digest covers its surface eligibility (agent-eligible, UI-only, confirmation-required or step-up). Committed idempotency and key entries carry over as non-executable retention entries. Each run gets its own catalog instance, named in the descriptor. The gateway enforces the active generation, and its metadata endpoint serves only executable entries.
- **Identity:**
  - Every realm comes from one versioned, value-free realm contract. EventStore owns the claims. Platform owns the instance: the client-to-surface map, token exchange, admission groups, refresh settings and the synthetic tenant ID.
  - `ui` clients are confidential and admit UI-only and confirmation-required operations as well. `agent` clients are public and admit agent-eligible operations only. `service` clients are confidential and admit agent-eligible operations plus one downstream step that consumes a confirmation. Step-up never executes in the MVP.
  - The surface comes from `azp` plus attested chain context, never from headers or audience.
  - McpCli's two heads form one `agent` client without `offline_access`. Full scope is off.
  - Synchronous chains use downscoped token exchange. Asynchronous steps use the EventStore-attested original actor. Raw token forwarding is forbidden.
  - Until EventStore ships attestation, service clients may call only agent-eligible operations.
- **Synthetic tenant:**
  - Tenants' idempotent task creates it once per run-owned environment, and the runner supplies its identifier.
  - Synthetic clients carry no admin or cross-tenant grant.
  - Modules exclude the tenant by its identifier.
- **McpCli:**
  - The client is a run-scoped, never-published build. Its Contracts resolve through the same mapping as the host and tests.
  - A profile binds one gateway, issuer, audience and token source, with no fallback.
  - An operation executes only when the gateway serves it as executable with a matching digest and McpCli-admitting eligibility. The served eligibility wins over the bundled copy.
  - The actor comes from the token.
  - Stdio is the only MVP MCP surface. Legacy module MCP/CLI hosts are never enrolled.
- **CI:**
  - CI runs in package mode on disposable runners with no hosted credentials.
  - The candidate's own Release artifacts replace its catalog entries in the one mapping. Other modules stay catalog-pinned.
  - Owner packages are checked against the owner package lock.
  - Evidence compares expected identities with loaded ones, including those inside the host and McpCli. Builds owns the encoding.

## Cross-Story Dependencies

- **From Epic 2:** Epic 2 supplies the runner, the descriptor and the Story 2.1 policy.
  - Story 3.1 must apply that policy before Stories 3.9 and 3.12 accept candidate evidence.
  - Story 3.12 repeats the Story 2.10 lifecycle scenarios.
  - The composition uses the released tuple from Story 2.14.
- **Order within this epic:**
  1. Host and catalog (3.2, 3.3).
  2. Realm and synthetic tenant (3.4, 3.5).
  3. McpCli (3.7, 3.8).
  4. Candidate evidence (3.9).
  5. SM stories (3.10 to 3.12).

  Story 3.6 also needs Folders and Projects adoption. Story 3.2 uses a Platform test extension. Folders' adapters are proven locally in 3.6 and on hosted environments in 10.1.
- **Downstream:**
  - Epic 4 needs only Stories 3.2 to 3.4, plus 3.7 and 3.8 for its staging McpCli story.
  - Epics 11 and 12 build on this epic.
- **External prerequisites:**
  - EventStore: the extension API, catalog schema, metadata endpoint, realm-contract claims, and actor and surface attestation.
  - McpCli: contract amendments, and migration of its test AppHost to the descriptor.
  - Tenants: the synthetic tenant task.
  - Tenants, Folders and Projects: adoption through the enrollment kit.

  A story cannot close before its prerequisite is delivered.
- **Work in other repositories:** it is done only after it is merged upstream, released, pinned in Platform and verified from the Platform workspace. Stories 3.7 and 3.8 need McpCli owner review.
