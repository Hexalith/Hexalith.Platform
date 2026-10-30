# Story 3.3 investigation and proposed prerequisite design

Planning evidence, 2026-10-30. This records repository observations and a proposed implementation direction; it grants no operation eligibility or activation authority.

## Confirmed owner boundaries

The Platform epics' External Prerequisites Register assigns schema, per-operation digests, surface eligibility and retention entries to EventStore.Contracts and requires delivery before the generator. The user selected implementing this prerequisite in this run. Reference module enrollment is a separate pending scope question.

At EventStore revision `6e0280bf489171326e2ce1ba3dde361712b7c977`:

- `src/Hexalith.EventStore.Contracts/Routing/RouteCatalogEnvelope.cs` supports schemas 1 and 2. `RouteCatalogEntry.DeclarationDigest` covers routing declaration metadata, not an operation's serialized request/response schema or calling-surface eligibility.
- `RouteCatalogCodec.cs` rejects unknown properties. `RouteCatalogValidator.cs` requires declarations and executable bindings to join, and every idempotency entry to join an active command. Adding Platform-only fields or retaining a removed command in these arrays would be incorrect.
- `src/Hexalith.EventStore.ServiceDefaults/Routing/RouteCatalogActivationBinding.cs` currently admits schema 2 only. Existing authority uses prepare, independent host readiness and commit, with durable dispatch leases preventing generation changes during effects.
- `RouteCatalogHostObservation.cs` and `ObservedRouteCatalogHost.cs` compare installed declarations, targets, admission policies, topology, authentication, projections and authority coordinates. Candidate fields cannot substitute for independently observed host facts.
- `src/Hexalith.EventStore/Pipeline/AuthorizationBehavior.cs` supplies the existing authenticated tenant/RBAC boundary. Command/query dispatch and generated MVC endpoints already use catalog leases. There is no implemented surface-class/confirmation/step-up claim contract or executable-operation metadata endpoint.

## Proposed compatible owner changes

1. Add schema 3 operation contracts keyed by existing route IDs, containing authoritative serialized schemas, exact eligibility and a digest covering identity/version, schemas and eligibility. Route entries carry and validate the corresponding digest and eligibility. Keep existing declaration identities and all v1/v2 canonical bytes unchanged.
2. Add an independent retained-idempotency collection. Each record preserves the original complete entry, its generation/content digest, descriptor and logical key readers plus source-root identity. Carry prior executable and retained history forward, deduplicate by immutable content identity, and preserve multiple historical policies sharing a route ID. Retained entries never enter executable declarations, bindings or public metadata. Authorized contraction remains separate.
3. Reuse the assembly-level `RouteCatalogDeclarationSourceAttribute` source convention. Read owner-authored operation metadata beside its existing `Routes` and `Commands`; no arbitrary assembly scan, inferred eligibility or request-shape-only substitute for response linkage.
4. Extend snapshot immutability, activation validation and independent observations to schema 3. Eligibility checks hold the dispatch generation through effects. Authenticated executable-only metadata reads that same authority, with no duplicate operation list or caller-selected generation.
5. Define the missing surface claim contract in EventStore, with exactly one authenticated value from `ui`, `agent` or `service`. Missing/ambiguous claims refuse. UI admits agent-eligible, UI-only and confirmation-required classes; agents admit agent-eligible only; no class admits step-up in the MVP. Downstream confirmation requires verified originating confirmation, not a self-asserted claim. This class gate never replaces existing confirmation, tenant or RBAC checks. Exact implementation constants must be stated in the approval spec.

## Platform integration seams

`PlatformDeclarationSet.Load` and `PlatformServerBindings.Resolve` already supply validated enrollment closure and actual mapped targets. Resolve Contracts project/package identities through that same mapping, without maintaining operation names in Platform. A private generation reader can use mapped Debug projects locally and catalog-selected Release packages in CI.

`PlatformAppModel.Compose` establishes a private temporary workspace or `RunPlan.Workspace/platform-components` before resources start. `PlatformGatewayConfiguration.Create` currently copies/filter registrations only. Generate and bind catalog bytes there, then use real authority bootstrap and host observations rather than treating file presence as activation.

Builds' `TestEnvironmentDescriptorWriter.cs` names the instance `EnvironmentId + "-catalog"`. Reuse that name for the actual run-scoped catalog. Preserve `catalog.commit`, which identifies the Builds package catalog revision. Descriptor schema expansion is unnecessary merely to name the instance.

## Remaining enrollment evidence

Tenants has `src/Hexalith.Tenants.Contracts/Routing/TenantCatalogDeclarations.cs`, with route/command policies but no assembly declaration-source attribute or explicit four-class eligibility/response-schema inventory. Parties and Memories have no complete compiled catalog inventory. McpCli command/query annotations describe agent-facing shapes; they do not decide all operation policies. Each actual gateway operation needs an explicit owner declaration. Non-gateway HTTP and internal operations must not be invented as gateway routes.

## Required verification

Preserve v1/v2 golden bytes. Prove schema 3 input-order determinism, schema/eligibility tampering refusal, retained historical-policy coexistence and key-reader preservation. Exercise actual HTTP metadata and command/query/generated ingress with every surface class, missing/conflicting claims and zero downstream effects on refusal. Verify independent run identities, active-generation consistency and real host readiness/activation. Source fixtures alone cannot establish published package acceptance.
