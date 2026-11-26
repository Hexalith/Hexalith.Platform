# Epic 3 Context: Run the complete Hexalith system locally with agent access

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

One command starts EventStore, Tenants, Parties, Folders, Projects, McpCli and Memories with their required supporting components. Local runs use one composed EventStore gateway with Folders' adapters, a run-scoped routing catalog, the contract-generated local realm and the synthetic tenant. The run-scoped McpCli client discovers and executes eligible operations through its CLI and stdio MCP. CI evidence identifies the exact loaded artifacts. SM-1, SM-2 and SM-3 establish local MVP acceptance across the seven modules.

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

- Use module-owned readiness checks and require successful startup tasks. The default deadline is 10 minutes from the environment request. Complete composition uses the largest declared override and reports it as an override. Record actual duration and effective deadline, and identify unready resources on timeout. McpCli readiness is demonstrated through its local client.
- Resolve source from the active root's direct references. Local development uses Debug and project references for the active module and its declared source dependencies. CI/CD uses Release and NuGet references. Missing required source and duplicate identities fail explicitly.
- Both McpCli heads execute declared agent-eligible commands and queries with the caller's permission. Refuse UI-only, confirmation-required, disabled-module, digest-mismatched and unauthorized calls. Prove that each client is denied outside its class both directly and across one module hop.
- Use exact committed merge results for a given head/base pair or the exact release revision. Each decision requires a fresh run. Record the candidate policy, decision, revision and producing run. Bind the content identities of every candidate artifact to the identities actually loaded. Refuse uncommitted or substituted inputs and label baseline-only runs.
- Record the source revision, loaded identities, effective deadline, duration, result and diagnostics. SM-1 names the operations demonstrated. SM-2 proves active-checkout debugging and root-only dependency initialization. SM-3 proves module-local and CI integration, including lifecycle scenarios. Record outstanding adoption as open.

## Technical Decisions

- Platform assembles one `eventstore` gateway from EventStore.Server and enrolled extension packages. Reject unsupported extension API majors, duplicate registrations and module-published images named `eventstore`. Extensions declare no Dapr roles or secrets and reference no provider SDK or Dapr client except explicitly approved boundary exceptions. Verify the effective 1 MiB Kestrel limit and intended route exceptions in both the gateway and TenantRegistry.
- Generate the routing catalog deterministically from enrolled declarations and Contracts using the EventStore.Contracts schema. The digest covers the operation contract and surface eligibility. Preserve committed idempotency and key entries as non-executable retention entries. The local and CI descriptors identify the run-scoped catalog. The gateway enforces the active generation and serves only executable metadata.
- Generate the local realm from the shared value-free contract. Map each distinct client to its `ui`, `agent` or `service` class. Use authenticated `azp` and attested chain context to determine the surface. Configure explicit audiences and roles with full scope disabled. McpCli uses a public agent client without `offline_access`. Cross-module calls use downscoped token exchange. Service clients are limited to agent-eligible operations until EventStore supplies attestation.
- Tenants' idempotent task creates the Platform-owned synthetic tenant once per run-owned environment. Record its identifier in the descriptor and realm instance. Flag synthetic clients and limit them to that tenant without admin grants. Modules exclude its data by identifier.
- Bind each connected profile to one gateway, issuer, audience and token source. Use the eligibility served by the gateway and require a matching Contracts digest. Offline inspection leaves operations unavailable for execution. Derive the actor from the token. Build McpCli as a run-scoped client through the same source mapping as the gateway and tests. Stdio is the MVP MCP surface.
- Candidate artifacts from one Release build replace that module's catalog entries in the shared mapping. Other owners use catalog pins. Measure the files actually loaded and compare them with the expected identities from the verified lock for released packages.

## Cross-Story Dependencies

- Epic 2 supplies runner lifecycle, descriptor and candidate policy. Apply that policy in the runner contract before accepting CI candidate evidence. Repeat the reference lifecycle scenarios after changes.
- The composed host and catalog precede connected McpCli and candidate evidence. Epic 4 depends on the host, catalog and local-realm foundations. Folders' adapters require local proof here and hosted proof in Story 10.1.
- EventStore supplies the extension API, catalog schema, metadata endpoint, realm claims and actor/surface attestation. Tenants supplies the synthetic-tenant task. Folders and Projects must adopt Platform and publish declarations. McpCli supplies its contract amendments and adopts the test descriptor. Use the enrollment kit for workspace evidence.
- Changes in a module repository need an upstream merge, release, Platform pin and workspace verification before the corresponding story can close.
