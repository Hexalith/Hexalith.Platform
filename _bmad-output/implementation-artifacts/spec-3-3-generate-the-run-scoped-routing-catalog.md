---
title: '3.3 Generate the run-scoped routing catalog'
type: 'feature'
created: '2026-10-30'
status: 'draft'
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

## Boundaries & Constraints

**Always:** EventStore.Contracts owns schema, canonicalization, eligibility vocabulary and digest semantics. Eligibility is agent-eligible, UI-only, confirmation-required or step-up, covered together with the operation schema by its digest. Consume the command's single source/package mapping. Generate before starting resources; reject invalid inputs without publishing partial output. Preserve supplied committed history and unrelated workspace changes.

**Never:** Invent a Platform-only catalog format, equate a routing declaration digest with a contract-schema digest, infer eligibility from authorization/surface class, add another hand-maintained operation list, or claim acceptance while the owner prerequisite is absent. Hosted activation, contraction/retirement authorization, McpCli implementation and real-module eligibility decisions remain outside this story's Platform implementation.

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

## Open Questions

1. **Module enrollment:** Further investigation found missing complete schema/eligibility inventories in the reference module Contracts. Choose: **include module Contracts and review their explicit per-operation policies in this run**, or **implement EventStore prerequisites first and leave Story 3.3 open pending owner enrollment**. Missing policies cannot be inferred from McpCli annotations or interface surface classes. This question has been presented asynchronously; its answer is pending.

## Code Map

- `src/Hexalith.Platform.Composition/PlatformDeclarationSet.cs` / `PlatformServerBindings.cs`: reuse validated closure and mapped enabled targets. Current manifest operations do not identify Contracts artifacts or carry eligibility/schema metadata.
- `src/Hexalith.Platform.Composition/PlatformAppModel.cs`: shared local/runner composition seam; owned output is a fresh temporary workspace or `RunPlan.Workspace/platform-components`.
- `src/Hexalith.Platform.Composition/PlatformGatewayConfiguration.cs`: copies/filter gateway registration settings; currently binds no catalog.
- `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/TestEnvironment/TestEnvironmentDescriptorWriter.cs`: reuse `EnvironmentId + "-catalog"`; preserve the Builds package meaning of `catalog.commit`.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Routing/{RouteCatalogEnvelope,RouteContractDeclaration,RouteCatalogEntry,RouteCatalogCodec,RouteCatalogValidator}.cs`: authoritative closed v1/v2 schema; unknown fields fail. Validator requires active command joins for idempotency, preventing removed-command retention.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.ServiceDefaults/Routing/{CompiledRouteCatalogDeclarations,CommittedRouteCatalogSnapshot,RouteCatalogEndpointExtensions}.cs`: compiled declaration reader, runtime resolver and lifecycle endpoints; required eligibility/metadata behavior is absent.
- `tests/Hexalith.Platform.Composition.Tests/{PlatformRunIsolationTests,PlatformTestEnvironmentProvisioningTests,GatewaySeedSubscriptionTests}.cs`: existing isolation, descriptor and actual host HTTP patterns.

## Tasks & Acceptance

**Execution:**
- [ ] `references/Hexalith.EventStore/src/Hexalith.EventStore.Contracts/Routing/RouteCatalogEnvelope.cs` and owner routing APIs -- verify delivered schema/digest/retention plus gateway behavior before Platform implementation; resolve the open question and finalize Contracts artifact enrollment first.
- [ ] `src/Hexalith.Platform.Composition/PlatformRoutingCatalog.cs` -- implement generation from authoritative declarations/Contracts and optional committed generation, using the delivered owner codec and deterministic validation.
- [ ] `src/Hexalith.Platform.Composition/PlatformCompositionOptions.cs` -- expose explicit committed-generation and Contracts inputs through the existing mapping; never scan arbitrary assemblies/checkouts.
- [ ] `src/Hexalith.Platform.Composition/Hexalith.Platform.Composition.csproj` -- reference owner Contracts in both modes.
- [ ] `src/Hexalith.Platform.Composition/PlatformAppModel.cs` and `PlatformGatewayConfiguration.cs` -- create private run-owned output, use the existing descriptor instance name and bind the gateway to those exact bytes.
- [ ] `tests/Hexalith.Platform.Composition.Tests/PlatformRoutingCatalogTests.cs` -- cover the matrix, input-order determinism, schema/eligibility changes and preservation of retained key readers.
- [ ] `tests/Hexalith.Platform.Composition.Tests/PlatformRoutingCatalogIntegrationTests.cs` -- exercise real gateway metadata/refusals and descriptor identity across independent local/CI-mode runs.

**Acceptance Criteria:**
- Given enrolled declarations and Contracts, when generation repeats with identical inputs, then bytes match and every executable operation has digest-covered schema and eligibility.
- Given local or CI composition, when the gateway starts, then its catalog is run-scoped and named in the validated environment descriptor.
- Given a supplied committed generation, when generating its successor, then committed idempotency/key history survives as non-executable retention entries.
- Given the active gateway generation, when invoking or discovering operations, then eligibility is enforced and metadata excludes all non-executable entries.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

EventStore revision `6e0280bf489171326e2ce1ba3dde361712b7c977` lacks AR-62 delivery. Footprint: owner contract/gateway, generator, composition and tests. No irreversible actions or runtime changes during planning. Detailed observations and proposed design: `evidence/3-3/investigation.md`.

## Verification

- Build `tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj` in mapped Debug/source mode; run the focused xUnit classes directly.
- Verify generation and descriptor handoff in Release/package CI with matching owner versions; source fixtures cannot establish package acceptance.
- Retain canonical byte comparisons and gateway HTTP evidence. Finalize runnable commands against delivered APIs before approval.
