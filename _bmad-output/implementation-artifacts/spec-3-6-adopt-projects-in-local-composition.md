---
title: '3.6 slice 2: Adopt Projects in the local MVP composition'
type: 'feature'
created: '2026-11-26'
status: 'in-review'
route: 'dispatch'
baseline_commit: '926ca9797ede0f704d1c7d9e233d5270d4e57f85'
story_key: '3-6-run-all-seven-mvp-modules-together-locally'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

Projects already declares its domain server and has a direct Platform gitlink. Prepare the Projects catalog and adapter release that the later Builds and Platform slices can adopt.

**Decision (2026-11-26).** The local runtime includes Conversations as a supporting service for the seven named MVP modules. Projects retains its required Conversations dependency and existing conversation-backed behavior. The Platform slice enrolls the Conversations owner and verifies its declared readiness and the authorized Projects advisory and membership calls.

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
| Projects selected without a Conversations declaration | Builds refuses the incomplete dependency closure and identifies Conversations. |

</frozen-after-approval>

## Code Map

Projects paths use the standalone repository root at the pinned revision.

- `src/Hexalith.Projects.Server/ProjectsRuntimeCatalogDeclarations.cs` defines 12 commands, 10 queries and three projections. `ProjectsServerModule.cs`, the processor payloads and `ProjectCommandPayloadJson.cs` supply identities and strict serialization. Use the Folders 2.1.0 split as the packaging precedent.
- `ProjectsIntentAdapters.cs` defines the adapter source. `ProjectsDevelopmentCatalogExtensions.cs` passes the declaration assembly to the EventStore SDK. `tests/Hexalith.Projects.Server.Tests/ProjectsRuntimeCatalogTests.cs` exercises the installed catalog.
- `module/hexalith.module-manifest.v2.json` requires `conversations` and declares `project-advisory-resolution`. Builds `CompositionDeclarationClosure` follows every dependency. The 6.8.4 schema accepts the current manifest and has no optional dependency field.
- `ProjectsServerServiceCollectionExtensions.cs` installs the direct Conversations client. `ProjectConversationMembershipDirectory` and `DelegatedProjectAdvisoryConversationDirectory` use the delegated gateway path. Keep both transports and the existing Client and Contracts references. Platform already maps the Conversations domain and API servers in `PlatformServerBindings.cs`.
- `src/Hexalith.Projects.AppHost/Hexalith.Projects.AppHost.csproj`, `tests/Hexalith.Projects.Integration.Tests/Hexalith.Projects.Integration.Tests.csproj` and `tests/Hexalith.Projects.PackageFixtures.AppHost/Hexalith.Projects.PackageFixtures.AppHost.csproj` link Platform mTLS helpers and probe sibling paths.
- `tools/release-packages.json` defines the package inventory. `scripts/pack-release-packages.py` and `scripts/validate-consumer-package-references.py` drive package verification. Release preflight checks the exact-source G-6 gate and the five-package count.

## Tasks & Acceptance

- [x] The three `.csproj` Platform helper consumers in the Code Map — use the explicit mapping or direct gitlink and cover missing-input refusal.
- [x] `src/Hexalith.Projects.Contracts`, `src/Hexalith.Projects.EventStoreExtensions` and Server catalog registration — relocate declarations and adapters, retain one payload serializer, preserve projection handler names and compare catalogs against the pinned source. Cover the existing development host.
- [x] `module/hexalith.module-manifest.v2.json` and `tools/release-packages.json` — declare and pack the extension and the exact manifest. Preserve the required Conversations edge and verify closure selection with and without its declaration. Exercise the packed candidate through EventStore and coordinate the package-count preflight with its CI owner.

**Acceptance Criteria**

- Given the Projects declaration and its runtime dependencies including Conversations, when Builds selects Projects, then the closure includes Conversations and the Projects domain retains its declared `/ready` endpoint.
- Given package-only inputs, when the composed gateway loads the Projects extension and the catalog reader opens the Projects contracts, then all existing routes retain their schemas, eligibility and digests and the adapters register once.

## Implementation Notes

- The [isolated Projects worktree](../../../projects-story-3-6) is detached at `4d740a889787bb095b16303ed749ec7f165e5683`. The [complete patch](../../../projects-story-3-6-evidence/projects-implementation.patch) includes new and moved files. The Projects release workflow is caller-owned and its exact-source G-6 preflight now checks six package IDs.
- The final Release solution build and six isolated package consumers passed. Contracts, Server and Integration passed 208, 1,653 and 217 tests respectively, with one existing opt-in Dapr restart test skipped. The package and workflow suites passed all 77 Python tests. The packed EventStore 5.6.0 probe loaded 12 distinct adapters and reported healthy. It refused API major 2. The 25 declarations, schemas and digests matched the baseline catalog byte for byte, SHA-256 `3b965f3684fa380a581e373bfa584425151166809a67bfeed1b9b2f4ae9aa6e9`. A clean extension-only restore omitted provider SDKs and Projects Server. The embedded manifest matched its source bytes. [Release and package-load log](../../../projects-story-3-6-evidence/release-final-targets.log)
- Builds 6.8.4 accepted the candidate manifest. The source closure retained Projects `/ready` and refused the missing Conversations declaration. All three MSBuild consumers accepted direct or explicitly mapped Platform input and rejected a missing input with a sibling checkout present. The final mapped Debug build and all seven ordinary development-host model tests passed. The initial unmapped Aspire startup stopped before launching because Conversations requires the selected mapping. [Debug and manifest checks](../../../projects-story-3-6-evidence/debug-final-targets.log) [Missing-input checks](../../../projects-story-3-6-evidence/platform-input-refusal.log)
- Landing and first package publication still require maintainer authorization and exact-source hosted CI. Live seven-module readiness and the authorized Conversations calls remain with the Platform slice.

## Spec Change Log

## Review Triage Log

## Design Notes

The pinned Projects manifest validates under Builds 6.8.4. A disposable source-mode probe called the published `CompositionDeclarationClosure.Load` with the Projects, Folders 2.1.0, Parties, EventStore, Tenants and Memories declarations. It failed with `conversations: identity.dependencies: owner declaration is unavailable.` Adding the existing Conversations declaration resolved the server-owner closure. This probe exercised declaration selection. The later Platform slice still owns live readiness and authorized cross-module calls.

## Verification

- Use `run-with-temp` for disposable verification. Keep scratch copies, feeds and outputs beneath `$TMPDIR`. Retain useful logs outside it and wait for child processes.
- Validate the candidate manifest with `hexalith-module validate --manifest module/hexalith.module-manifest.v2.json` using Builds 6.8.4. Verify the resulting owner closure separately.
- Build the Projects solution in Release package mode. Run Contracts, Server and Integration test projects individually. Verify explicit source mapping in Debug.
- Run the existing release package and consumer checks on a disposable feed. Verify the embedded manifest bytes, extension loading, wrong-major refusal and dependency closure.
