---
title: '3.6 slice 2: Adopt Projects in the local MVP composition'
type: 'feature'
created: '2026-11-26'
status: 'draft'
route: 'dispatch'
baseline_commit: '926ca9797ede0f704d1c7d9e233d5270d4e57f85'
story_key: '3-6-run-all-seven-mvp-modules-together-locally'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Projects already declares its domain server and has a direct Platform gitlink. Platform needs released Projects catalog and adapter artifacts, and the local profile needs a decision about the required Conversations runtime. Prepare a Projects release that the later Builds and Platform slices can adopt.

## Boundaries & Constraints

- Preserve Projects payload schemas, route and handler identities, eligibility, idempotency policies and authorization. Keep the existing `/ready` endpoint and authenticated compatibility surfaces in their current profiles.
- Expose the catalog through `Hexalith.Projects.Contracts` and the adapters through `Hexalith.Projects.EventStoreExtensions`. The extension declares EventStore API v1 with no Dapr roles or secrets. Its published dependency closure excludes Projects Server and provider SDKs.
- Resolve Platform from the direct gitlink or the explicit active-root mapping. A nearby checkout cannot satisfy a missing selected input.
- Work in an isolated Projects checkout from `4d740a889787bb095b16303ed749ec7f165e5683`. Keep Builds catalog adoption, Platform wiring, hosted deployment and legacy CLI/MCP enrollment outside this slice. Publication needs maintainer authorization and passing exact-source CI.

## I/O & Edge-Case Matrix

| Input | Expected behavior |
|---|---|
| Projects artifact consumed with EventStore extension API v1 | Packed declarations resolve and all 12 adapters register once. Extension readiness is healthy. |
| Configured extension major differs | Loader refuses Projects and the extension readiness gate fails. |
| Platform mapping or direct checkout is missing, with a sibling available | Build fails with a diagnostic identifying the missing Platform input. |

</frozen-after-approval>

## Open Questions

1. Should the local MVP run with a Conversations service? **Run Conversations as a supporting module** keeps the seven named MVP modules and preserves Projects' conversation-backed flows. The Platform slice must enroll the additional owner and verify its readiness and authorization. **Make Conversations optional for the MVP** keeps it out of the local runtime. `projects.trace-conversation` would be unavailable there, and requests that require Conversation membership would fail closed. Profiles with Conversations would keep the integration.

## Code Map

Projects paths use the standalone repository root at the pinned revision.

- `src/Hexalith.Projects.Server/ProjectsRuntimeCatalogDeclarations.cs` defines 12 commands, 10 queries and three projections. `ProjectsServerModule.cs`, the processor payloads and `ProjectCommandPayloadJson.cs` supply identities and strict serialization. Use the Folders 2.1.0 split as the packaging precedent.
- `ProjectsIntentAdapters.cs` defines the adapter source. `ProjectsDevelopmentCatalogExtensions.cs` passes the declaration assembly to the EventStore SDK. `tests/Hexalith.Projects.Server.Tests/ProjectsRuntimeCatalogTests.cs` exercises the installed catalog.
- `module/hexalith.module-manifest.v2.json` requires `conversations` and declares `project-advisory-resolution`. Builds `CompositionDeclarationClosure` follows every dependency. The 6.8.4 schema accepts the current manifest and has no optional dependency field.
- `ProjectsServerServiceCollectionExtensions.cs` installs the direct Conversations client and supplies unavailable implementations when it is absent. `ProjectConversationMembershipDirectory` and `DelegatedProjectAdvisoryConversationDirectory` also use the delegated gateway path. Projects Server separately references Conversations Client and Contracts, with explicit package mode available.
- `src/Hexalith.Projects.AppHost/Hexalith.Projects.AppHost.csproj`, `tests/Hexalith.Projects.Integration.Tests/Hexalith.Projects.Integration.Tests.csproj` and `tests/Hexalith.Projects.PackageFixtures.AppHost/Hexalith.Projects.PackageFixtures.AppHost.csproj` link Platform mTLS helpers and probe sibling paths.
- `tools/release-packages.json` defines the package inventory. `scripts/pack-release-packages.py` and `scripts/validate-consumer-package-references.py` drive package verification. Release preflight checks the exact-source G-6 gate and the five-package count.

## Tasks & Acceptance

- [ ] `module/hexalith.module-manifest.v2.json` and the Server Conversations registrations and delegated handlers — implement the approved dependency behavior and verify both the MVP profile and the existing connected profiles.
- [ ] The three `.csproj` Platform helper consumers in the Code Map — use the explicit mapping or direct gitlink and cover missing-input refusal.
- [ ] `src/Hexalith.Projects.Contracts`, `src/Hexalith.Projects.EventStoreExtensions` and Server catalog registration — relocate declarations and adapters, retain one payload serializer, preserve projection handler names and compare catalogs against the pinned source. Cover the existing development host.
- [ ] `module/hexalith.module-manifest.v2.json` and `tools/release-packages.json` — declare and pack the extension and the exact manifest. Exercise the packed candidate through EventStore and coordinate the package-count preflight with its CI owner.

**Acceptance Criteria**

- Given the approved runtime owner set, when Builds resolves the Projects declaration, then the closure resolves and the Projects domain still uses its declared `/ready` endpoint.
- Given package-only inputs, when the composed gateway loads the Projects extension and the catalog reader opens the Projects contracts, then all existing routes retain their schemas, eligibility and digests and the adapters register once.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

The pinned Projects manifest validates under Builds 6.8.4. A disposable source-mode probe called the published `CompositionDeclarationClosure.Load` with the Projects, Folders 2.1.0, Parties, EventStore, Tenants and Memories declarations. It failed with `conversations: identity.dependencies: owner declaration is unavailable.` Adding the existing Conversations declaration resolved the server-owner closure. This probe exercised declaration selection. The later Platform slice still owns live readiness and authorized cross-module calls.

## Verification

- Validate the candidate manifest with `hexalith-module validate --manifest module/hexalith.module-manifest.v2.json` using Builds 6.8.4. Verify the resulting owner closure separately.
- Build the Projects solution in Release package mode. Run Contracts, Server and Integration test projects individually. Verify explicit source mapping in Debug.
- Run the existing release package and consumer checks on a disposable feed. Verify the embedded manifest bytes, extension loading, wrong-major refusal and dependency closure.
