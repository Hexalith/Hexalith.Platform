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
- [x] `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Routing/{RouteCatalogEnvelope,RouteCatalogEntry,RouteCatalogCodec,RouteCatalogValidator,RouteOperationContract,RetainedIdempotencyCatalogEntry}.cs` -- add schema 3 operation schemas/eligibility/digests and a separate retained-idempotency collection. Preserve original historical entry generation/content digests and key references; carry prior retained and executable history, deduplicating immutable identity. Retention never enters dispatch arrays.
- [x] `references/Hexalith.EventStore/src/Hexalith.EventStore.Client/Routing/{RouteCatalogGenerationInput,VersionedRouteCatalogBuilder}.cs` -- build canonical schema 3 generations from explicit compiled owner inputs, bindings and optional committed predecessor; validate before output.
- [x] `references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Routing/{CompiledRouteCatalogDeclarations,CommittedRouteCatalogSnapshot,RouteCatalogActivationBinding,RouteCatalogHostObservation,ObservedRouteCatalogHost,GeneratedRouteCatalogFilter}.cs` -- read owner operation schemas beside Routes/Commands; freeze metadata; independently verify installed operation identities during activation; preserve lease, trust and topology checks.
- [x] `references/Hexalith.EventStore/src/Hexalith.EventStore/Pipeline/RouteCatalogEligibilityBehavior.cs`, `Extensions/ServiceCollectionExtensions.cs`, `Controllers/RoutingCatalogMetadataController.cs` and `../Hexalith.EventStore.ServiceDefaults/Authentication/RouteCatalogSurfacePolicy.cs` -- enforce policy after tenant/RBAC authorization under a held dispatch lease, including generated ingress. Serve authenticated executable public command/query metadata at `GET /api/v1/routing/catalog`, with generation/root/schema/eligibility and no keys, retention or internal endpoints.
- [x] `references/Hexalith.Tenants/src/Hexalith.Tenants.Contracts/Routing/TenantCatalogDeclarations.cs` -- attribute its assembly source; add explicit request/result schemas and policy above. Preserve existing intent policies, system-envelope tenancy, aliases and actual computed/projection targets.
- [x] `references/Hexalith.Parties/src/Hexalith.Parties.Contracts/Routing/PartiesCatalogDeclarations.cs` and `src/Hexalith.Parties.EventStoreExtensions/{PartiesCatalogAdapters.cs,Hexalith.Parties.EventStoreExtensions.csproj}` within that owner -- declare complete actual routes/results and explicit local intent adapters; add missing legacy query request contracts while preserving wire names. Keep identity/custody and retry paths intact.
- [x] `references/Hexalith.Memories/src/Hexalith.Memories.EventStore/Routing/MemoriesCatalogDeclarations.cs`, `src/Hexalith.Memories.EventStoreExtensions/{MemoriesCatalogAdapters.cs,Hexalith.Memories.EventStoreExtensions.csproj}` and `module/hexalith.module-manifest.v2.json` within that owner -- preserve command CLR identities, declare schemas/internal workload policies and enroll the existing DomainService receiver; add its runtime adapters without creating new query transports.
- [x] `src/Hexalith.Platform.Composition/{PlatformRoutingCatalog,PlatformCatalogContractBinding,PlatformCompositionOptions,PlatformAppModel,PlatformGatewayConfiguration}.cs` and `Hexalith.Platform.Composition.csproj` -- resolve only mapped Contracts projects/packages, generate atomically in the private run workspace, reuse descriptor instance identity and wire exact bytes. Add the private reader project `src/Hexalith.Platform.CatalogBuilder/Hexalith.Platform.CatalogBuilder.csproj`; no operation-name list in Platform.
- [x] `src/Hexalith.Platform.Composition/{PlatformRoutingCatalogBootstrap,PlatformRoutingCatalogComponentRenderer}.cs` and `src/Hexalith.Platform.EventStoreHost/Program.cs` -- provision disposable digest keys, exact adapter registrations, run-specific authority component/key and actual host observers. Prepare, ready-validate every required host and commit using existing authority; readiness waits for agreement. CI uses Release packages with an explicitly Development runtime, never production fixture trust.
- [x] `references/Hexalith.Builds/src/hosts/Hexalith.Builds.Module.EventStoreHost/Program.cs` -- bind the same owner runtime in the package seed without changing the descriptor format or its package-catalog meaning.
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
- Schema 3 gateway submissions without an HTTP context now fail closed under the production MediatR registration before command/query terminal effects. Internal dispatch still requires the existing authenticated workload/delegation boundary; no background surface or workload grant is synthesized.
- Catalog authority scoping preserves every existing owner-declared Dapr invocation rule. The selected application closure and exact declared caller/method/verb inventory are independently captured and included in observed topology identity. A selected business caller into a participant must itself enroll; existing exact GET readiness probes need no authority scope. Absent declared callers retain their transport rules in this fixed run.
- Private Development bootstrap attempts are immutable. Restarting a participant against the same completed attempt is unsupported and refuses the existing observation file without changing the authority epoch or committed root. Start a fresh run for a fresh participant cohort; this MVP does not claim same-run hot restart support.
- Native source run `6f1e6db8095797772cedbe0380577a5d` established a genuine ready descriptor in 218.596 seconds, with 22 healthy resources and six committed catalog participants. The descriptor names environment `c9c058580d1bbc9536e85d9affb808f0` plus `-catalog`; `catalog.commit` remains the Builds catalog revision. Its [source/native receipt](evidence/3-3/verification/resume-20261030/native-6f1e6db8095797772cedbe0380577a5d/source-readiness-receipt.json) identifies the actual tested source and binaries. Supported `down` removed every owned resource.
- Necessary runtime prerequisites preserve existing policies: finite HTTP observers cache the actual endpoints used for routing; Tenants API installs its shared human credential policy; Tenants metadata reads use the existing bound workload gateway; preparation presents the existing Memories app-channel credential. No new credential grant was added by story 3.3. Concurrent story 2.9 handoff changes are preserved and separately identified by source hashes.
- Ordinary Release/package activation is established in a private committed authority with real next owner revisions, unchanged declaration bytes, 23 freshly built packages and seven local runtime images. [Authority](evidence/3-3/verification/resume-20261030/package/private-authority.json), [packages](evidence/3-3/verification/resume-20261030/package/fresh-package-receipt.json) and [images](evidence/3-3/verification/resume-20261030/package/image-results.json) record exact identities. Production catalog pins are unchanged; all artifact pushes stayed in the owned loopback registry. These are local Development results and grant no hosted acceptance.
- Final package run `bbe72c24968a7d674910976671cc656b` reached readiness in 84.068 seconds with all 22 resources healthy and six serving readbacks. Its [ready receipt](evidence/3-3/verification/resume-20261030/package/native-bbe72c24968a7d674910976671cc656b/readiness-receipt.json) binds the genuine descriptor to the final admitted private authority. The earlier matching package run's [HTTP/schema checks](evidence/3-3/verification/resume-20261030/package/native-dd8da429233af5aa3d6217b447cc03db/binding-verification.json) verify all 44 public schemas against the same 69-operation root and unchanged immutable owner artifacts. The final run's processes were already absent when refreshing HTTP, so that earlier result is reused explicitly. Supported cleanup is complete.
- The broader native consumer suite remains non-passing and belongs to story 2.9 migration. The source run isolated its bearer-only Memories request boundary; the same valid human token succeeds only through the existing authorized private channel. Story 3.3 establishes the real catalog-bound ready descriptor and preserves the owner policies. The package seed is the ordinary Builds EventStoreHost with unconditional SDK binding; source-only native owner preparation is distinct from package cohort readiness.

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

Recorded commands and outputs are retained in [evidence/3-3/verification/resume-20261030](evidence/3-3/verification/resume-20261030). The [existing reference readiness lane](evidence/3-3/verification/resume-20261030/ReferenceReadiness-timeout-retry.txt) passed in 114.478 seconds after explicit sequential owner builds and exact AppHost cleanup. Its explicit CLI start allowance accommodates the real cold catalog-reader build. The [actual source-sidecar activation](evidence/3-3/verification/resume-20261030/PlatformRoutingCatalogIntegrationTests-repaired.txt) passed with all eight hosts ready, six committed readbacks and signed public HTTP schema metadata. The genuine source descriptor and matching Release/package descriptor above establish the previously missing handoff and package boundaries; earlier failed attempts remain retained as historical evidence.

Owner verification passed: EventStore schema/history/goldens 15, Tenants/Parties/Memories catalogs one each, real finite receiver 12 and actual Tenants API security two. The final isolated canonical checks passed two; strict loopback registry policy checks passed 15. Earlier unchanged matrix checks retain passing surface policy 18, registered ingress four, bootstrap four, topology isolation three, writer startup/health three, ACL preservation one and actual package-admission refusal one. The [matrix receipt](evidence/3-3/verification/resume-20261030/matrix-receipt.json) maps each boundary to its evidence. Final affected-class verification is in progress; the [source receipt](evidence/3-3/verification/resume-20261030/final-source-receipt.json) distinguishes current shared content from exact historical native and immutable private package snapshots.

The initial broad AppModel run exposed catalog setup and stale receiver-count expectations. Repairs keep real catalog construction and execution: explicit source mappings, actual reader inputs in synthetic graph fixtures, and the seventh host's component scopes. Synthetic graph fixtures remain explicitly separate from the admitted all-package runtime cohort. Gateway-only infrastructure has no enrolled business operations and does not claim a business catalog; nonempty owner closures retain fail-closed generation and activation.

The approved frozen block and original baseline are unchanged. Tasks 8 and 9 now have genuine source/native and admitted package evidence. Task 11's final affected-class verification and the parent's independent diff/matrix audit and formal review remain pending; the workflow has not claimed completion. Same-attempt participant restart remains an explicit tested MVP limitation.
