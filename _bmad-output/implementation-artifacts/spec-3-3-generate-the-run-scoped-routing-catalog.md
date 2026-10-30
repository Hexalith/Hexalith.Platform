---
title: '3.3 Generate the run-scoped routing catalog'
type: 'feature'
created: '2026-10-30'
status: 'in-progress'
baseline_commit: '1316523edfab050e03539a4e6425605245070de9'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Platform has no declaration-driven routing catalog. Gateway settings and descriptor names do not establish schemas, eligibility or retention.

**Approach:** Generate deterministic catalog bytes from the selected enrollment closure and its mapped Contracts, using EventStore's versioned schema and codec. Bind each local/CI gateway and environment descriptor to that run's catalog instance, retaining committed idempotency and key metadata without retaining executable routes.

**Decision (2026-10-30):** Implement the EventStore-owned contract and gateway prerequisites first, then Platform integration; approve the revised cross-repository spec before implementation.

**Scope decision (2026-10-30):** Include necessary Tenants, Parties and Memories Contracts, adapter and enrollment changes. Preserve the public agent operations documented by each owner; retain internal service boundaries.

**Operation policy:**
- Tenants' nine documented McpCli commands and five queries remain agent-eligible. Global-administrator listing is UI-only; set/remove grants require confirmation. Bootstrap, delegated authority queries and projection dispatch remain internal.
- Parties' four decorated commands (CreatePartyComposite, UpdatePartyComposite, DeactivateParty, ReactivateParty) and three queries (GetParty, ListParties, SearchParties) remain agent-eligible. EraseParty requires confirmation. Other ordinary public operations are UI-only; identity/custody/delegated operations remain internal.
- Memories' six existing EventStore commands remain internal services. Enroll their existing DomainService receiver; do not turn REST search, ingestion or compatibility MCP tools into new gateway operations.
- Add EventStore-owned `eventstore:surface`: exactly one authenticated `ui`, `agent` or `service` value. UI admits agent-eligible/UI-only/confirmation-required, agents admit agent-eligible, and services admit agent-eligible plus confirmation only with existing verified originating proof. Missing/ambiguous claims refuse; step-up refuses every class in the MVP. Internal workload calls retain their existing explicit workload/delegation checks and cannot become human/agent calls.
- Preserve Tenants' existing idempotency policies. Newly declared Parties/Memories local/CI policies use Mutation (24-hour replay), canonical target plus complete canonical-payload SHA-256, and trusted acting subject for human operations. Keep payload values out of catalogs. These local policies grant no hosted acceptance.

## Boundaries & Constraints

**Always:** EventStore.Contracts owns schema, canonicalization, eligibility vocabulary and digest semantics. Eligibility is agent-eligible, UI-only, confirmation-required or step-up, covered together with the operation schema by its digest. Consume the command's single source/package mapping. Generate before starting resources; reject invalid inputs without publishing partial output. Preserve supplied committed history and unrelated workspace changes.

**Never:** Invent a Platform-only catalog format, equate a routing declaration digest with a contract-schema digest, infer eligibility from authorization/surface class, add another hand-maintained operation list, or claim acceptance while the owner prerequisite is absent. Hosted activation, contraction/retirement authorization, McpCli implementation remains outside this story.

## I/O & Edge-Case Matrix

| Scenario | Input / state | Expected behavior | Failure handling |
|---|---|---|---|
| Repeat generation | Same closure, Contracts, committed input and generation | Identical canonical bytes and digests | No timestamps or filesystem paths in semantic content |
| Separate runs | Same composition, different environment IDs | Separate owned files; each uses its descriptor instance ID | Refuse cross-run binding |
| Retained history | Previous committed idempotency/key entries, removed operation | Preserve history as non-executable retention entries | Never expose or dispatch retained routes |
| Invalid enrollment | Missing schema/eligibility, duplicate/conflicting operation or incompatible Contracts | Identify module/operation and reject | Preserve prior valid output |
| Tampering | Schema, eligibility or supplied committed digest changes | Reject mismatched content | No partial generation or gateway startup |
| Invocation/discovery | Gateway has active generation | Enforce its eligibility; metadata lists executable entries only | Owner-defined refusal for ineligible/retained operations |

</frozen-after-approval>

## Code Map

- `evidence/3-3/investigation.md`: prior investigation; module scope is now included by the decision above.
- EventStore `Routing` code in Contracts/Client/ServiceDefaults owns codec, declarations, leases and activation. Preserve v1/v2 bytes and existing declaration identities; extend schema 3 instead.
- `PlatformDeclarationSet.Load`, `PlatformServerBindings.Resolve` and `PlatformCompositionOptions.Mapping` supply the selected closure and targets. `PlatformAppModel.Compose` is the shared local/CI seam.
- Builds `TestEnvironmentDescriptorWriter` already names `EnvironmentId + "-catalog"`; preserve `catalog.commit` as the Builds package-catalog revision.
- Tenants `TenantCatalogDeclarations` has policies/route inventories; Parties `PartyDomainProcessor` has 28 command payloads and 11 query handlers, including retry/identity special cases. Memories has six commands across three domains, with an unenrolled receiver. Contracts contain the result DTOs; use actual serializers and existing wire aliases.

## Tasks & Acceptance

**Execution, in dependency order:**
- [ ] `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Routing/{RouteCatalogEnvelope,RouteCatalogEntry,RouteCatalogCodec,RouteCatalogValidator,RouteOperationContract,RetainedIdempotencyCatalogEntry}.cs` -- add schema 3 operation schemas/eligibility/digests and a separate retained-idempotency collection. Preserve original historical entry generation/content digests and key references; carry prior retained and executable history, deduplicating immutable identity. Retention never enters dispatch arrays.
- [ ] `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Routing/{RouteCatalogGenerationInput,VersionedRouteCatalogBuilder}.cs` -- build canonical schema 3 generations from explicit compiled owner inputs, bindings and optional committed predecessor; validate before output.
- [ ] `references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Routing/{CompiledRouteCatalogDeclarations,CommittedRouteCatalogSnapshot,RouteCatalogActivationBinding,RouteCatalogHostObservation,ObservedRouteCatalogHost,GeneratedRouteCatalogFilter}.cs` -- read owner operation schemas beside Routes/Commands; freeze metadata; independently verify installed operation identities during activation; preserve lease, trust and topology checks.
- [ ] `references/Hexalith.EventStore/src/Hexalith.EventStore/Pipeline/RouteCatalogEligibilityBehavior.cs`, `Extensions/ServiceCollectionExtensions.cs`, `Controllers/RoutingCatalogMetadataController.cs` and `../Hexalith.EventStore.ServiceDefaults/Authentication/RouteCatalogSurfacePolicy.cs` -- enforce policy after tenant/RBAC authorization under a held dispatch lease, including generated ingress. Serve authenticated executable public command/query metadata at `GET /api/v1/routing/catalog`, with generation/root/schema/eligibility and no keys, retention or internal endpoints.
- [ ] `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Routing/TenantCatalogDeclarations.cs` -- attribute its assembly source; add explicit request/result schemas and policy above. Preserve existing intent policies, system-envelope tenancy, aliases and actual computed/projection targets.
- [ ] `references/Hexalith.Parties/src/Hexalith.Parties.Contracts/Routing/PartiesCatalogDeclarations.cs` and `src/Hexalith.Parties.EventStoreExtensions/{PartiesCatalogAdapters.cs,Hexalith.Parties.EventStoreExtensions.csproj}` within that owner -- declare complete actual routes/results and explicit local intent adapters; add missing legacy query request contracts while preserving wire names. Keep identity/custody and retry paths intact.
- [ ] `references/Hexalith.Memories/src/Hexalith.Memories.EventStore/Routing/MemoriesCatalogDeclarations.cs`, `src/Hexalith.Memories.EventStoreExtensions/{MemoriesCatalogAdapters.cs,Hexalith.Memories.EventStoreExtensions.csproj}` and `module/hexalith.module-manifest.v2.json` within that owner -- preserve command CLR identities, declare schemas/internal workload policies and enroll the existing DomainService receiver; add its runtime adapters without creating new query transports.
- [ ] `src/Hexalith.Platform.Composition/{PlatformRoutingCatalog,PlatformCatalogContractBinding,PlatformCompositionOptions,PlatformAppModel,PlatformGatewayConfiguration}.cs` and `Hexalith.Platform.Composition.csproj` -- resolve only mapped Contracts projects/packages, generate atomically in the private run workspace, reuse descriptor instance identity and wire exact bytes. Add the private reader project `src/Hexalith.Platform.CatalogBuilder/Hexalith.Platform.CatalogBuilder.csproj`; no operation-name list in Platform.
- [ ] `src/Hexalith.Platform.Composition/{PlatformRoutingCatalogBootstrap,PlatformRoutingCatalogComponentRenderer}.cs` and `src/Hexalith.Platform.EventStoreHost/Program.cs` -- provision disposable digest keys, exact adapter registrations, run-specific authority component/key and actual host observers. Prepare, ready-validate every required host and commit using existing authority; readiness waits for agreement. CI uses Release packages with an explicitly Development runtime, never production fixture trust.
- [ ] `references/Hexalith.Builds/src/hosts/Hexalith.Builds.Module.EventStoreHost/Program.cs` -- bind the same owner runtime in the package seed without changing the descriptor format or its package-catalog meaning.
- [ ] `references/Hexalith.EventStore/tests/Hexalith.EventStore.Contracts.Tests/Routing/RouteCatalogV3Tests.cs`, `references/Hexalith.Tenants/tests/Hexalith.Tenants.Contracts.Tests/TenantCatalogTests.cs`, `references/Hexalith.Parties/tests/Hexalith.Parties.Contracts.Tests/PartiesCatalogTests.cs`, `references/Hexalith.Memories/tests/Hexalith.Memories.EventStore.Tests/MemoriesCatalogTests.cs` and `tests/Hexalith.Platform.Composition.Tests/{PlatformRoutingCatalogTests,PlatformRoutingCatalogIntegrationTests}.cs` -- cover the matrix, v1/v2 golden bytes, policy/schema tampering, historical coexistence, surfaces, generated/command/query ingress, independent runs and actual sidecar activation. Refusals precede effects.

**Acceptance Criteria:**
- Given enrolled declarations and Contracts, when generation repeats with identical inputs, then canonical bytes match and every public operation carries digest-covered schema and eligibility.
- Given local or CI composition, when readiness succeeds, then every participating host serves the run-owned catalog named in its descriptor.
- Given a committed predecessor, when generating a successor, then its idempotency/key history survives non-executably, including removed or revised operations.
- Given the active generation, when invoking/discovering operations, then its eligibility governs execution and metadata excludes retention/internal entries.

## Implementation Notes

- EventStore now owns schema 3 operation schemas, explicit eligibility, independent operation digests and non-executable retained idempotency history. The v1/v2 golden cases remain unchanged. Owner declarations and adapters cover Tenants, Parties and the existing Memories DomainService receiver; Memories REST/MCP search gained no gateway operations.
- Platform generates the canonical catalog before resource startup and binds independent host observations to its private instance and shared authority. The selected Parties closure produces 69 operations (44 public, 25 internal). A real source cohort committed the same generation/root on all six participants and all eight hosts passed readiness; signed agent metadata returned the matching public request/result schemas.
- Source verification can explicitly launch sequentially built owner outputs with `Platform:UsePrebuiltProjects`. Normal source Debug builds remain available. The run-plan writer cutover uses the remaining declared startup deadline; callers without a run plan retain the existing two-minute default and the existing API.
- Package acceptance remains undelivered. The authoritative catalog selects EventStore 3.128.4, Tenants 5.11.1, Parties 2.0.0 and Memories 3.0.10. The isolated matching packages are the distinct `3.128.5-story33.0`, `5.11.2-story33.0`, `2.0.1-story33.0` and `3.0.11-story33.0` families. The full Builds host rejects that preview override against its controlled catalog. The released v2 declaration-hash map is empty and release-commit admission only records older EventStore/Tenants/Memories releases. No release provenance or admission rule was overridden.
- Full package readiness needs approved matching owner version selections, released v2 declaration hashes/commits, and runtime image receipts containing the new observers/adapters, including `registry.hexalith.com/memories-domain-service` and `registry.hexalith.com/parties`. The Parties image repository binding follows its existing owner publisher. Local package-reader generation and package-seed compilation do not establish package-cohort or hosted acceptance. Keep this story open.

## Spec Change Log

## Review Triage Log

## Design Notes

Schema 3 keeps executable declarations/routes separate from retained historical policies. Its digest covers operation identity/version, actual serialized request/result shapes, ingress classification and eligibility; a routing declaration digest alone is insufficient. Internal dispatchers stay workload-only. Real owner schemas and installed adapters, rather than fixture inventories or candidate-supplied observations, establish readiness. No production deployment or data migration is included.

## Verification

- Before code edits, run and inspect the exact isolated AppHost using the Aspire lifecycle skill; retain any baseline failure.
- Build owner test projects individually in mapped Debug/source mode; invoke their built xUnit assemblies with `-class`.
- `dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -p:HexalithEventStoreRoot="$PWD/references/Hexalith.EventStore" -p:HexalithBuildsRoot="$PWD/references/Hexalith.Builds"`; run the built assembly's two new classes. Use the same explicit mapping for other owner dependencies.
- Run the existing `HEXALITH_PLATFORM_LIVE_SMOKE=1` reference readiness lane and real-sidecar catalog activation tests; retain generation/readback, HTTP and descriptor evidence.
- Qualify Release/package consumption using matching owner packages in an isolated feed/mapping. Old published packages, source substitutes or synthetic fixtures cannot establish package acceptance; record any undelivered boundary and keep the story open.

Recorded commands and outputs are retained in [evidence/3-3/verification](evidence/3-3/verification). The passing [real-sidecar test](evidence/3-3/verification/Platform-final-PlatformRoutingCatalogIntegrationTests.txt) saved six committed readbacks and [signed HTTP schema evidence](evidence/3-3/verification/catalog-live-1dcd2c373508486ebb451fabf7700e9f/metadata-http.json); the [existing reference readiness lane](evidence/3-3/verification/Platform-final-ReferenceReadiness.txt) also passed. The [matching package receipt](evidence/3-3/verification/package-receipt.json) records all 15 package hashes and full owner revisions. The [package-cohort boundary](evidence/3-3/verification/package-cohort-boundary.json) and [actual controlled-version refusal](evidence/3-3/verification/run-plan-host-build.txt) distinguish these results from package activation.
