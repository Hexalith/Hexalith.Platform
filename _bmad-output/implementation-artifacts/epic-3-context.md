# Epic 3 Context: Run the complete Hexalith system locally with agent access

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Provide one-command local operation of EventStore, Tenants, Parties, Folders, Projects, McpCli and Memories with their supporting resources. The environment uses one Platform-composed EventStore gateway, enrolled extensions, a generated routing catalog, a contract-derived identity realm and synthetic data. Demonstrate permitted commands and queries through CLI and stdio MCP, record refusals, and prove source debugging and real-service integration across the complete MVP set. Candidate acceptance must identify the exact artifacts actually loaded.

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

Platform supplies composition and operational integration; modules own Contracts, business behavior, authorization, projections and service-specific readiness. One Aspire model consumes declarations in both source and package modes. Domain modules retain thin hosts and extension packages, without introducing another AppHost or shared hosting framework. Extensions execute inside the composed `eventstore` host and cannot acquire provider, Dapr or secret authority except through an explicitly named architecture exception.

Integration environments run real services after isolated tests pass. Readiness includes required resources and successful startup tasks. The default startup deadline is ten minutes; justified finite overrides and actual duration are recorded. Process startup alone is insufficient; diagnostics identify unready resources.

Each suite or compatible batch receives a fresh run-owned environment with isolated resources, ports, catalog and data, or a refusal naming the conflicting owner. The runner owns provisioning, readiness and cleanup; tests consume its versioned descriptor. Local failures, including attachment failures, retain resources; local success cleans after holds end, and CI always cleans. Attachment requires compatible composition, mode and artifact identities, readiness and isolation, with a finite hold limit. Withdrawal supplies no integration evidence.

## Technical Decisions

Resolve the active root once and initialize only its directly declared `references/` submodules. Never substitute other checkouts or silently fall back to packages. Local root-declared dependencies build from source in Debug; other dependencies use the Builds catalog. CI uses Release and package boundaries. EventStore, Memories and McpCli qualify from Platform's workspace. The pinned Platform identity and Builds catalog govern composition and tool compatibility.

For candidate acceptance, all candidate-module artifacts come from its own Release build of the declared committed revision, including Contracts, server and extensions, services and tools. One mapping supplies the host, run-scoped McpCli and tests; other modules stay catalog-pinned. Record expected and loaded content identities. Missing identities, substitutions and uncommitted inputs invalidate evidence. Label baseline-only runs; they cannot establish candidate acceptance. The shared policy governs candidate selection and evidence reuse.

EventStore.Contracts owns the routing schema, codec and eligibility vocabulary: agent-eligible, UI-only, confirmation-required and step-up. One deterministic Platform generator consumes enrolled declarations and Contracts and the currently committed generation's idempotency and key entries. Those committed entries survive as non-executable retention entries until an authorized contraction retires them. Each route's contract-schema digest covers its schema and eligibility. Local and CI instances are run-scoped and identified in the environment descriptor; hosted generation uses the committed deployment catalog. The gateway enforces the active generation and serves only executable entries through metadata. No separate hand-maintained operation list is allowed.

McpCli has one shared CLI/MCP core and statically enrolled Contracts. Platform builds run-scoped tools through the same artifact mapping without publishing them. Connected execution requires gateway-served executable metadata, a matching contract-schema digest, served eligibility admitting McpCli and current authorization. Offline inspection never establishes availability. A profile binds exactly one gateway, issuer, audience and token source without cross-environment fallback or implicit module enablement. The authenticated token determines actor and surface; caller-supplied identity overrides are refused, and bearer tokens are never persisted or logged. UI-only and confirmation-required operations remain UI operations.

Local realms derive from the hosted value-free realm contract. UI, agent and service classes have distinct clients. Public McpCli has explicit audiences and roles without full scope or offline access. Platform owns a synthetic tenant identifier per environment in its descriptor and realm; Tenants supplies idempotent creation. Synthetic identities lack administrative or cross-tenant grants, and modules exclude synthetic data from real-tenant views.

## Cross-Story Dependencies

Candidate-policy implementation consumes the decision from Story 2.1; evidence qualification depends on that policy. Runner descriptors and lifecycle ownership come from Epic 2. EventStore must supply extension API majors, catalog Contracts, gateway metadata, realm claim semantics and actor/surface attestation. Tenants supplies synthetic-tenant creation. Folders and Projects adoption and Folders' extensions precede full seven-module qualification. McpCli requires owner-reviewed contract amendments and descriptor-based fixtures. The complete-environment and workspace acceptance records must name demonstrated operations and retain any unadopted module as open; partial or empty demonstrations cannot close the success measures.
