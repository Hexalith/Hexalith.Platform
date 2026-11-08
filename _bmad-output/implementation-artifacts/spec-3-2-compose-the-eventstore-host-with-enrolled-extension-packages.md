---
title: '3.2 Compose the eventstore host with enrolled extension packages'
type: 'feature'
created: '2026-11-08'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'f7c68398cdcb494d535e789e3d9577af9e8d5d36'
story_key: '3-2-compose-the-eventstore-host-with-enrolled-extension-packages'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Platform composes one `eventstore` gateway, but no declaration lets an enrolled module contribute EventStore extension packages to it. Nothing rejects incompatible, duplicate or over-privileged extensions, or module-built `eventstore` images (AD-13, AR-7, FR-1). Module `*.EventStoreExtensions` packages load today only through 3.3's Development bootstrap.

**Approach:** Add a declared EventStore-extension field, validated in Builds. Have Platform assemble the `eventstore` host from the EventStore server plus the declared packages through the AD-4 mapping: ProjectReference in source mode, catalog-pinned PackageReference in package mode. Prove it with a Platform-owned test extension that the host loads and reports in readiness.

## Boundaries & Constraints

**Always:**
- EventStore owns the extension API and its supported majors (current and previous). Platform and Builds only consume them; no Platform copy of the majors.
- Failures are stable, named diagnostics that name the module and the offending major, duplicate, reference, secret or image.
- Extension entries take `configuration` inputs only. Secret inputs and direct provider-SDK or Dapr-client references are admitted only for an entry naming a spine-admitted AD-9 exception, and only for that exception's own SDK and secret.
- Only direct references are checked: Dapr that is reached through the EventStore extension API packages is the API itself.
- With no extension declared, current gateway selection, 3.3's run-scoped catalog wiring and the qualified package cohorts stay unchanged.

**Never:**
- No publication, release, push, pin change, or migration of module-owned manifests. Parties, Memories, Conversations and Folders adopt later; Folders' real adapters are proven in 3.6 and 10.1.
- No second AppHost, no module-built host, no hand-maintained extension list, and no enrollment except through declarations.
- Do not reinterpret `runtime.extensions[]`, which names owner server and hosting packages that carry secrets.
- No claims of live package-mode qualification before the EventStore and Builds packages carrying this work are released.

**Decisions (2026-11-08, maintainer adopted all recommendations):**
- *Extension API:* EventStore gets a minimal API it owns: `[assembly: EventStoreExtension(id, major)]`, `EventStoreExtensionApi` current and supported majors (current 1), and host registration that verifies loaded extensions and reports them in an `eventstore-extensions` readiness check. Package-mode proof waits for the EventStore and Builds releases.
- *Landing:* EventStore, Builds and Platform changes are made on scratch worktree branches from each `origin/main`. Platform is proven in a scratch worktree whose EventStore and Builds submodules point at those branches. Main checkouts, pins and tuple CI stay untouched. The maintainer's later "push all" lands EventStore, then Builds (releases), then Platform (gitlinks through the L/M tuple PR).
- *Image rule:* only the EventStore server owner's own server project is exempt. Every other module project publishing `eventstore`, including today's `Hexalith.Folders.EventStore`, is rejected.
- *Scope:* the spec stays whole (about 2,000 tokens) because the acceptance criteria cannot be met piecemeal.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Source happy | Test module declares the Platform test extension at a supported major | Host references the extension project, starts, and readiness lists the extension id and major | N/A |
| Package happy | Same, package mode | Generated binding is a PackageReference at the Builds-catalog version | Refuse if the package is unpinned |
| Unsupported major | Declared major is outside the server's supported set | Composition fails before any resource starts | Names module, package, major and supported set |
| Duplicate | Same extension id or package declared twice (same or different modules) | Fails | Names both modules and the duplicate |
| Declaration drift | Assembly's id or major differs from the declaration | Host startup and readiness fail | Names package, declared and actual values |
| Secret / Dapr | Entry has a `secret` or `dynamic-secret` input, or Dapr roles | Rejected unless AD-9 | Names module, package and key or role |
| Provider / Dapr client | Extension directly references a provider SDK or Dapr client | Rejected unless AD-9 | Names module, package and dependency |
| AD-9 admitted | Entry names an admitted Memories exception, with its SDK and own secret | Admitted | N/A |
| Image `eventstore` | A module project publishes the image or ContainerRepository `eventstore` | Rejected | Names module and project |

</frozen-after-approval>

## Code Map

Prefixes: `ES/` = `references/Hexalith.EventStore/src/`, `B/` = `references/Hexalith.Builds/`, `BT/` = `B/src/libraries/Hexalith.Builds.Tooling/`.

- `ES/Hexalith.EventStore.DomainService/` — `IDeclaredIdempotencyIntentAdapter` and `RouteCatalogAdapterSourceAttribute.cs` are today's extension surface; extension packages already reference this package. Add the API here. The package depends on `Dapr.AspNetCore`, so only direct references can be checked.
- `ES/Hexalith.EventStore.Server/Commands/IdempotencyIntentAdapterRegistry.cs:86,103` — runtime duplicate guard; reuse it. `Server/Routing/RunScopedRouteCatalogGateway.cs:46-69,152` — Development `AdapterAssemblies` loading, owned by 3.3; do not change.
- `B/schemas/hexalith.module-manifest.v2.json:142-172,224-262,771` — the `runtime`, `extension` and `providerException` definitions. Add an optional, closed `runtime.eventStoreExtensions[]` entry {`packageId`, `extensionId`, `extensionApiMajor`, `configurationInputs`, optional `providerException`}.
- `BT/Manifest/PlatformManifestValidator.cs` — `ValidateModule` l.547, `ValidateDaprBindings` l.639 and HXP003 l.103 hold the cross-module duplicate pattern. Add the new HXP diagnostics. `BT/Manifest/PlatformAdoptionSourceAudit.cs` gets the module `eventstore` image check and the extension direct-reference checks. Tests go in `B/test/Hexalith.Builds.Module.Tests/PlatformManifestValidationTests.cs`.
- `src/Hexalith.Platform.Composition/PlatformServerBindings.cs:11-20,70-97,117` — gateway selection: EventStore's project in source mode, the runner seed in package mode, one gateway. Select `src/Hexalith.Platform.EventStoreHost` when at least one extension is declared.
- `src/Hexalith.Platform.Composition/PlatformRoutingCatalog.cs:61-74` with `src/Hexalith.Platform.CatalogBuilder/*.csproj` (`$(PlatformCatalogBindings)`) — the precedent for generated source/package reference props; mirror it. `PlatformDeclarationSet.Load` feeds the declarations.
- `src/Hexalith.Platform.EventStoreHost/{Program.cs,*.csproj}` — the composed host (image `eventstore`). Import the generated bindings and register the extensions; keep 3.3's `#if` catalog wiring.
- `tests/Hexalith.Platform.Composition.Tests/GatewaySeedSubscriptionTests.cs:40` (`WebApplicationFactory<platformseed::Program>`) and `PlatformAppModelTests.cs:467` (duplicate) — test patterns.
- `references/Hexalith.Parties/src/Hexalith.Parties.EventStoreExtensions/` — shape of a real extension package (adapter factory, conditional EventStore refs).
- Architecture: `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md` AD-9 l.150-158, AD-13 l.197-204, register l.470.

## Tasks & Acceptance

**Execution:**
- [ ] `ES/Hexalith.EventStore.DomainService/EventStoreExtensionAttribute.cs`, `EventStoreExtensionApi.cs` — the id and major attribute, plus current and supported majors.
- [ ] `ES/Hexalith.EventStore.Server/Extensions/` — load the named extension assemblies, verify each attribute against its declaration, reject unsupported or duplicate extensions, register adapters through the existing registry, and add an `eventstore-extensions` readiness check listing the loaded ids and majors. Tests in `tests/Hexalith.EventStore.Server.Tests`.
- [ ] `B/schemas/…v2.json`, `BT/Manifest/PlatformManifestValidator.cs`, `PlatformAdoptionSourceAudit.cs`, `B/README.md` — the new field and rules, with named diagnostics. Exempt only the EventStore server owner from the image rule.
- [ ] `src/Hexalith.Platform.Composition/PlatformEventStoreExtensionBindings.cs` (new) — resolve the declared packages (`src/<packageId>/<packageId>.csproj` under the owner's mapped root, or the catalog pin), check majors against the server's supported set and check duplicates before any resource starts, then write the bindings props. Wire it in `PlatformServerBindings.cs`.
- [ ] `src/Hexalith.Platform.EventStoreHost/` — import the bindings and call the EventStore registration.
- [ ] `tests/fixtures/Hexalith.Platform.TestExtension/` (new) — a Platform-owned extension with one adapter at the supported major.
- [ ] `tests/Hexalith.Platform.Composition.Tests/EventStoreExtensionCompositionTests.cs` (new) — the matrix rows, source and package bindings, and host readiness with the test extension loaded.
- [ ] `docs/domain-module-enrollment.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml` — enrollment guidance and story status.

**Acceptance Criteria:**
- Given a composition declaring the test extension, when it builds in source or package mode, then exactly one `eventstore` host is assembled from the server plus that package, through the mapping.
- Given the Platform test extension at the supported major, when composed, then the host starts and its readiness reports the extension loaded.
- Given a composition without extensions, when it is composed, then the existing tests for gateway selection and the qualified cohorts pass unchanged.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Design Notes

A new field, not `runtime.extensions[]`: every current manifest uses that field for owner server packages that carry secrets (Parties, Memories, Tenants, Conversations), so applying AD-13 to it would reject all of them. Declared majors are checked at composition time, so the failure comes before startup. Host startup verifies the actual assembly attribute, so a declaration cannot drift from the binary.

## Verification

**Commands:**
- `dotnet build -nodeReuse:false` in each touched repo — expected: 0 warnings, 0 errors (analyzers are errors).
- `dotnet exec tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -class Hexalith.Platform.Composition.Tests.EventStoreExtensionCompositionTests` — expected: all pass. Then a full run compared against the baseline failures.
- Builds `dotnet exec …/Hexalith.Builds.Module.Tests.dll -class …PlatformManifestValidationTests`, and the EventStore Server tests for the extension classes — expected: pass.
