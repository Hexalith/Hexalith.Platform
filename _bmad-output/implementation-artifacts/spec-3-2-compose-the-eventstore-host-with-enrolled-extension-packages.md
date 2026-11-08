---
title: '3.2 Compose the eventstore host with enrolled extension packages'
type: 'feature'
created: '2026-11-08'
status: 'done'
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
- [x] `ES/Hexalith.EventStore.DomainService/EventStoreExtensionAttribute.cs`, `EventStoreExtensionApi.cs` — the id and major attribute, plus current and supported majors.
- [x] `ES/Hexalith.EventStore.Server/Extensions/` — load the named extension assemblies, verify each attribute against its declaration, reject unsupported or duplicate extensions, register adapters through the existing registry, and add an `eventstore-extensions` readiness check listing the loaded ids and majors. Tests in `tests/Hexalith.EventStore.Server.Tests`.
- [x] `B/schemas/…v2.json`, `BT/Manifest/PlatformManifestValidator.cs`, `PlatformAdoptionSourceAudit.cs`, `B/README.md` — the new field and rules, with named diagnostics. Exempt only the EventStore server owner from the image rule.
- [x] `src/Hexalith.Platform.Composition/PlatformEventStoreExtensionBindings.cs` (new) — resolve the declared packages (`src/<packageId>/<packageId>.csproj` under the owner's mapped root, or the catalog pin), check majors against the server's supported set and check duplicates before any resource starts, then write the bindings props. Wire it in `PlatformServerBindings.cs`.
- [x] `src/Hexalith.Platform.EventStoreHost/` — import the bindings and call the EventStore registration.
- [x] `tests/fixtures/Hexalith.Platform.TestExtension/` (new) — a Platform-owned extension with one adapter at the supported major.
- [x] `tests/Hexalith.Platform.Composition.Tests/EventStoreExtensionCompositionTests.cs` (new) — the matrix rows, source and package bindings, and host readiness with the test extension loaded.
- [x] `docs/domain-module-enrollment.md`, `_bmad-output/implementation-artifacts/sprint-status.yaml` — enrollment guidance and story status.

**Acceptance Criteria:**
- Given a composition declaring the test extension, when it builds in source or package mode, then exactly one `eventstore` host is assembled from the server plus that package, through the mapping.
- Given the Platform test extension at the supported major, when composed, then the host starts and its readiness reports the extension loaded.
- Given a composition without extensions, when it is composed, then the existing tests for gateway selection and the qualified cohorts pass unchanged.

## Implementation Notes

- Scratch branches (local commits, not pushed; main checkouts, pins and tuple CI untouched):
  - EventStore `feat/story-3-2-eventstore-extension-api` @ `f8664c257` (from origin/main `e19805609`).
  - Builds `feat/story-3-2-eventstore-extensions` @ `24bc0eab` (from origin/main `3c809618` = v4.44.13).
  - Platform `feat/story-3-2-eventstore-extension-host` @ `fa7cd0b1` (from origin/main `0f4e27f1`); its EventStore and Builds gitlinks point at the two commits above. Proven in the scratch worktree `/tmp/claude-1000/-home-administrator-projects-hexalith-platform/ab4f8cbf-6ab3-4a4c-b779-9d872359c4c5/scratchpad/platform` (submodules are worktrees of the main checkout's submodule repos).
- EventStore: `EventStoreExtensionAttribute(id, major)` and `EventStoreExtensionApi` (current 1, supported `[1]`) in DomainService. `Server/Extensions/AddEventStoreExtensions` reads `EventStore:Extensions:Packages:{n}:{PackageId,ExtensionId,ApiMajor}`, refuses unsupported majors and duplicate ids/packages before loading, loads by assembly name, refuses a missing or drifted attribute, requires each `RouteCatalogAdapterSource` to belong to the package, registers adapters as `IIdempotencyIntentAdapter` singletons (the existing registry guard rejects overlaps at host start), and adds the `eventstore-extensions` ready check (data: id → major). Named conditions live in `EventStoreExtensionConditions`. No declaration leaves services unchanged.
- Builds: optional closed `runtime.eventStoreExtensions[]`; HXP025 secret, HXP026 Dapr role (input key equals a `runtime.dapr` configurationKey or reference equals a role), HXP027 AD-9 exception not admitted for the module (each exception has a fixed spine owner, `memories`; self-declared ownership grants nothing), HXP028 duplicate id/package (messages name each declaring module once). "Own secret" = reference equal to the exception name or `<exception>-<purpose>`; own SDKs: falkordb-graph → NFalkorDB; redis-search-vector → NRedisStack, StackExchange.Redis; set-nx-preflight-dedup and redis-coordination → StackExchange.Redis. Adoption adds requirements `eventstore-image` (exempts only `src/Hexalith.EventStore/Hexalith.EventStore.csproj` in module `eventstore`) and `eventstore-extensions` (bounded direct Dapr-client/provider-SDK list); rows stay `HXA001`. New public `CompositionDeclarationOptions.CatalogPackageVersion` reads one package pin from the same snapshot/catalog authority.
- Platform: `PlatformEventStoreExtensionBindings` resolves and checks before resources (majors from `EventStoreExtensionApi` under `HEXALITH_PLATFORM_EXTENSION_SDK`, defined only in source mode; a package-mode build refuses with `eventstore-extension-api-unavailable`). With ≥1 extension the gateway runs `src/Hexalith.Platform.EventStoreHost` with the server project's appsettings, gets `-p:PlatformEventStoreExtensionBindings=<run>/eventstore-extensions/bindings.props`, and env `EventStore__Extensions__Packages__{n}__*` plus inputs bound from `<module>/<extensionId>/<key>`; keys that equal, contain or are contained in a gateway-bound key (case-insensitive) are refused; inputs bind like `PlatformLocalProfile` (unbound refused, empty secret generated once per `<module>/<key>` in the shared map, structured values flattened). `PlatformServerBinding.EventStoreHost` is null (and not serialized) without extensions.
- Not changed: `DomainEnrollmentKitTests.ReleasedPinnedToolValidatesDisposableWorkspaceWithoutWrites` still expects 9 adoption rows because it runs the released pinned tool; update it to the 11 requirements when the Builds release carrying this work is pinned.
- Verification (2026-11-08): all three solutions `dotnet build -nodeReuse:false` Debug 0 warnings/0 errors; Platform fixture, host, Composition and Composition.Tests also build in Release (package mode). EventStore `EventStoreExtensionRegistrationTests` 23/23. Builds `PlatformManifestValidationTests` + `PlatformAdoptionValidationTests` 609/609; full Module.Tests 2898, 1 failure (`WorkspaceAppHostTests.PlatformPlanDelegatesToThePinnedModelAsync`, 7 vs 6 server annotations) that reproduces identically with the Platform change removed. Platform `EventStoreExtensionCompositionTests` 16/16 (Release build: 2/2); full Composition run 935 tests, 162 failed — the identical set to the pre-change baseline (919 tests, 162 failed).
- Live package-mode qualification is not claimed: it waits for the EventStore and Builds releases.
- Post-review verification (2026-11-08, after the iteration-0 patches):
  - Builds: all three solutions build with `-nodeReuse:false` in Debug with 0 warnings and 0 errors, and Composition.Tests builds in Release with 0/0.
  - Targeted tests:
    - EventStore `EventStoreExtensionRegistrationTests` 24/24.
    - Builds `PlatformManifestValidationTests` + `PlatformAdoptionValidationTests` + `PlatformManifestCommandTests` 696/696.
    - Platform `EventStoreExtensionCompositionTests` + `GatewaySeedSubscriptionTests` 28/28.
  - Release (package mode), extension + cohort + gateway-seed classes: 148 tests, 8 failed. These are the same 8 as before the patch: signed-body checks for Tenants.Contracts 7.0.5 / McpCli.Abstractions 2.0.1 versus the expected 7.0.2 / 2.0.0 bodies. This work changes no package versions.
  - Full Debug Composition run: 943 tests, 164 failed. The baseline had 162, all of which still fail, plus two:
    - `DomainEnrollmentKitTests.AcceptedToolPinEqualsSupportedRangeFloorAsync` fails because the recorded Builds gitlink (`24bc0eab`) differs from the released v4.44.13 pin. This is expected until the Builds release is pinned.
    - `AcceptedLaunchDirectoryCensusTests(10000, true)` is a process-wide fd-count flake. It passes alone and twice alongside `EventStoreExtensionCompositionTests`.

## Spec Change Log

## Review Triage Log

Iteration 0 (2026-11-08). Layers: Blind Hunter (BH), Edge Case Hunter (ECH), Verification Gap (VG).

| # | Finding | Verdict | Evidence | Route |
|---|---------|---------|----------|-------|
| BH1/VG8 | The within-module HXP028 test is uncommitted; the Platform gitlink records a Builds commit without it | medium | `git status` in Builds shows the only within-module duplicate test as a working-tree edit | patch |
| BH2 | HXP028 names the same module twice ("'sample' and 'sample'") | low | `PlatformManifestValidator` joins every occurrence's module without `Distinct` | patch |
| BH3 | Any module can claim a Memories AD-9 exception and gain its SDK and secret | medium | The schema's `providerException.owner` accepts any identifier; HXP027 only checks `owner == moduleId`. AD-9 in the spine fixes Memories as the owner | patch |
| BH4/ECH2 | Extension inputs can override gateway settings that come from appsettings or are set later | low | A value can only come from operator-supplied Platform configuration or `local-profile.json` (an unbound input throws), so a module cannot inject one alone; a deny-list is a new rule | reject |
| BH5/VG-O2/ECH1 | The conflict check is case-sensitive and ignores parent/child keys | low | The environment uses `StringComparer.Ordinal`; the `configurationKey` pattern allows any case; `ContainsKey` is exact | patch |
| BH6/ECH3/ECH4/ECH5 | Extension `Bind` diverges from `PlatformLocalProfile.Bind` | medium | A missing secret is generated rather than refused; an empty secret is bound empty; generated secrets bypass the shared `secretValues` (`<module>/<key>`); JSON values are not flattened | patch |
| BH7/ECH6 | An `extensionId` equal to a server id reads that server's scoped values | low | Needs deliberate or coincidental equality of id and key in one module's own declaration; the fix is a new guard | reject |
| BH8/VG-O4/ECH7 | `UsePrebuiltProjects` skips building the bindings into the host | low | A Development+source opt-in (`PlatformAppModel.cs:135`); a host built by an earlier run still has the extension; otherwise startup fails loudly with `eventstore-extension-unavailable` | reject |
| BH9/ECH15 | `SupportedMajors` comes from the Composition-compiled API, not the selected server | low | In source mode both come from the mapped EventStore checkout; a mismatched mapping is refused loudly at host startup by the loader | reject |
| BH10 | Enrollment doc promises trusted-command/coordination policies | low | `EventStoreExtensionLoader` registers only `IDeclaredIdempotencyIntentAdapter` | patch |
| BH11 | Enrollment doc omits the adapter-source `Create()` contract, where values come from, and secret handling | low | The doc section names neither the public static `Create()` nor the source of `<moduleId>/<extensionId>/<key>` values | patch |
| BH12 | Zero-adapter extensions report Healthy; data omits package and adapter count | low | The spec requires no adapter minimum; refusals are fail-closed at startup; the description lists the package | reject |
| BH13/ECH13 | The `eventstore-image` audit misses indirection, pubxml, AssemblyName defaults and digests | low | The bounded audit catches today's literal `Hexalith.Folders.EventStore` `ContainerRepository`; only the `@digest` strip is a direct correction | patch (`@digest` only), rest reject |
| BH14/ECH12 | The direct-reference audit misses props, GlobalPackageReference, HintPath and `;` lists | low | The spec checks direct references; the bounded audit reads the declared project; only splitting `Include` on `;` is direct | patch (`;` split only), rest reject |
| BH15 | `PackageIdPattern` ends with `$`, which accepts a trailing newline | low | In .NET, `$` matches before a final `\n`; the schema uses `(?![\s\S])` | patch |
| BH16/VG2/VG3/VG4 | No tests for unmapped/unavailable/symlink refusals, the `EventStore:Extensions` key refusal, or a missing configuration value | medium | The condition names appear only at their throw sites; every fixture maps the owner and supplies the marker | patch |
| BH17 | Known follow-ups live only in Implementation Notes | low | `deferred-work.md` has no entry for the 9→11 adoption-row update or package-mode enablement | defer |
| VG1 | The composed host's content root (EventStore server settings) is not asserted | medium | Tests read `binding.Environment` and the args, never `ASPNETCORE_CONTENTROOT` | patch |
| VG5 | No test for the package-mode host refusing declared extensions | medium | `eventstore-extension-api-unavailable` is asserted only on `Resolve`, never on host startup | patch |
| VG6 | The snapshot (`catalog-versions.json`) pin lookup is untested | medium | The package tests set no `RunPlan` | defer |
| VG7 | The `eventstore-image` rule is tested only on `.csproj` | medium | Every theory case writes a `.csproj`, though the code scans `.props`/`.targets` | patch |
| VG9 | No CI path compiles or runs the new Platform tests | medium | Tuple CI builds package mode and selects no `EventStoreExtension` suite | defer |
| VG-O1 | Tuple CI fails on the unreleased Builds gitlink and `CatalogPackageVersion` | false | The Decisions sequence the EventStore and Builds releases before the Platform L/M tuple PR; pins stay untouched now | reject |
| VG-O3/ECH14 | Adoption rows are partial when the declaration fails | low | The report already fails on the `declaration` row; the fix is a new branch | reject |
| ECH8 | A `;` or `,` in the bindings path splits the `-p:` property | low | It follows the existing unescaped `-p:HexalithEventStoreRoot=` pattern under a Platform-controlled workspace path | reject |
| ECH9 | A parties selected-test run with extensions swaps the Builds runner seed for the Platform host | maybe-false | The code swaps it (`PlatformServerBindings.cs:105-111`); whether the runner should keep its seed is undecided until Parties adopts | defer (medium, unverified) |
| ECH10/ECH11 | `PackageVersion Update` may match nothing under the host's CPM; conditioned catalog items | maybe-false | Cannot be exercised before the package releases | defer (medium, unverified) |
| ECH16 | Package mode never yields a host with the extension | false | The Decisions defer package-mode proof to the releases, and the Boundaries forbid claiming it; the enablement follow-up is recorded with BH17 | reject |

## Design Notes

A new field, not `runtime.extensions[]`: every current manifest uses that field for owner server packages that carry secrets (Parties, Memories, Tenants, Conversations), so applying AD-13 to it would reject all of them. Declared majors are checked at composition time, so the failure comes before startup. Host startup verifies the actual assembly attribute, so a declaration cannot drift from the binary.

## Verification

**Commands:**
- `dotnet build -nodeReuse:false` in each touched repo — expected: 0 warnings, 0 errors (analyzers are errors).
- `dotnet exec tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -class Hexalith.Platform.Composition.Tests.EventStoreExtensionCompositionTests` — expected: all pass. Then a full run compared against the baseline failures.
- Builds `dotnet exec …/Hexalith.Builds.Module.Tests.dll -class …PlatformManifestValidationTests`, and the EventStore Server tests for the extension classes — expected: pass.
