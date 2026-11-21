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

### Review Findings

Code review iteration 1 (2026-11-08), of the merged result: Platform `0f25d669..5af62642`, EventStore `b0ea2fce1..b13271ba1` (#398), Builds `3c809618..27145e52` (#176). Layers: Blind Hunter (BH), Edge Case Hunter (ECH), Verification Gap (VG), Acceptance Auditor (AA). Triage-discovered items are marked (TR).

- [x] [Review][Decision] Platform main landed ahead of the Builds release, and tuple CI is red — high (AA1+AA2+BH3+BH4a+ECH14+VG-O1). The Platform gitlink records Builds `27145e52`, which no tag contains. Builds releases are `workflow_dispatch` and none was dispatched after #176; the newest tag is still `v4.44.13` = `3c809618`. Tuple CI preflight run `42499154243` on `5af62642` fails at `test "$(git rev-parse HEAD:references/Hexalith.Builds)" = "$builds_source"`, and the three "Qualified Platform source release" runs on it fail at "Require both latest exact-source qualifications". `14c1ba13` also changed `DomainEnrollmentKitTests.ReleasedPinnedToolValidatesDisposableWorkspaceWithoutWrites` to 11 rows, and `deferred-work.md` now says the pin landed. The pinned tool is still `4.44.13`, which reports 9 rows, and its source differs from the gitlink. This breaks the Landing decision: EventStore, then the Builds releases, then the Platform gitlinks through the L/M tuple PR. Resolved 2026-11-08 (maintainer adopted the forward fix): Builds v4.45.0 was released from `27145e52` (publisher `42501966448`, after main CI `42496435539`). Platform pins it (tool, range floor, `eng/builds-tuple.json`) through this review's L/M tuple PR, which also mirrors the direct story 2.8 commits into a new legacy source. The 11-row expectation and its deferred note now hold.
- [x] [Review][Decision] EventStore main was force-pushed and no longer carries #397 or #398 — high (TR, with the AA3 consequence). The EventStore repo activity shows `refs/heads/main force_push b13271ba1->e19805609` at 2026-11-08T16:29:06Z. That drops `b0ea2fce1` (#397) and `b13271ba1` (#398, the extension API) from every remote ref. Platform's EventStore gitlink now points at an unreferenced commit, which can still be fetched by SHA today but is subject to GC. No EventStore release from main would carry the extension API. Any source build of Platform against EventStore main fails to compile under `HEXALITH_PLATFORM_EXTENSION_SDK`. `e19805609` is an ancestor of `b13271ba1`, so main can be fast-forwarded back to it without losing anything. Resolved 2026-11-08 (maintainer adopted the restore): EventStore main was fast-forwarded `e19805609..b13271ba1`, restoring #397 and #398 with their original SHAs, so the Platform gitlink is reachable again. The standalone `eventstore` checkout's local main still sits at `e19805609`; never force-push it.
- [x] [Review][Defer] Adapters are registered twice once a module declares a package that 3.3's bootstrap already loads — medium (AA4). `RunScopedRouteCatalogGateway.cs:61-68` registers every `AdapterAssemblies` adapter as `IIdempotencyIntentAdapter`, and the catalog cohorts list `Hexalith.Parties.EventStoreExtensions` and `Hexalith.Memories.EventStoreExtensions` there. `AddEventStoreExtensions` registers the same adapters again, and the shared registry refuses overlapping adapters at host start. Nobody declares an extension today. Which path owns registration is a design choice. Resolved 2026-11-08 (maintainer adopted the recommendation): deferred — fails loudly at host start, nobody declares an extension today, and the registration owner is decided with the Parties/Memories adoption.
- [x] [Review][Patch] Test that extension inputs bind from `local-profile.json`, the only source real runs use (VG1) [tests/Hexalith.Platform.Composition.Tests/EventStoreExtensionCompositionTests.cs:282]
- [x] [Review][Patch] Test structured (JSON object/array) extension inputs flatten, a flattened key overlapping a gateway key is refused, and malformed JSON is refused (VG2+BH14 part) [src/Hexalith.Platform.Composition/PlatformEventStoreExtensionBindings.cs:120]
- [x] [Review][Patch] Give four refusals a stable `eventstore-extension-*` condition: the host-unavailable and no-gateway refusals, unbound input and invalid structured input (AA8) [src/Hexalith.Platform.Composition/PlatformServerBindings.cs:110]
- [x] [Review][Patch] The enrollment doc says inputs are "`configuration` only", then admits AD-9 secret inputs in the next bullet (BH4b) [docs/domain-module-enrollment.md:322]
- [x] [Review][Patch] The enrollment doc claims every key "already bound on the gateway" is refused. The check sees only the declared, profile and Dapr keys present when `Bind` runs; it does not see `ConfigureEnvironment`, catalog or Timesheets keys or content-root appsettings (ECH15) [docs/domain-module-enrollment.md:326]
- [x] [Review][Patch] Reconcile the story status: spec `done` vs `sprint-status.yaml` `review` (AA9) [_bmad-output/implementation-artifacts/sprint-status.yaml:75]
- [x] [Review][Defer] `selected-closure.json` records the composed gateway as the host csproj hash only, without the extension set or `bindings.props` (BH10+ECH5) [src/Hexalith.Platform.Composition/PlatformRoutingCatalogBootstrap.cs:118] — deferred: hashing only the csproj is the existing identity rule for every mapped project; Story 3.9 owns candidate artifact identity
- [x] [Review][Defer] Nothing tests that an extension's generated AD-9 secret equals the owner servers' `<module>/<key>` secret (VG3) [src/Hexalith.Platform.Composition/PlatformEventStoreExtensionBindings.cs:112] — deferred: only Memories can hold an admitted exception and it has not adopted; add the joint test with that adoption
- [x] [Review][Defer] The package-mode path is still unproven, and the package qualification gate would refuse the composed host (BH8+AA5+BH11+ECH13) [src/Hexalith.Platform.Composition/PlatformPackageCatalogQualification.cs:47] — deferred: already recorded as the package-mode qualification and enablement entries; this adds that `PlatformPackageCatalogQualification` accepts a gateway only when `ProjectPath == EventStoreSeedProject`

Patches applied 2026-11-08: `EventStoreExtensionCompositionTests` + `GatewaySeedSubscriptionTests` 33/33 in Debug (source mode), build 0 warnings/0 errors.

Rejected:
- BH1+ECH1 sibling keys under a bound collection (e.g. `AllowedCallers:2`) pass the overlap check — low. The value must come from Platform-owned configuration or `local-profile.json` (iteration 0, BH4/ECH2), and a reserved-root guard is a new rule.
- BH2+ECH2 keys bound after `Bind` (`ConfigureEnvironment`, catalog, Timesheets, appsettings) are not compared — low; same rationale as iteration-0 BH4/ECH2. The doc overclaim is patched separately (ECH15).
- BH5+AA7+ECH8 `eventstore-image`/`eventstore-extensions` rows are wrong when the declaration fails — low; the `declaration` row already fails the report (iteration-0 VG-O3).
- BH6+ECH9 the direct-reference audit misses props, imports, transitive references and `$(Prop)` — low; bounded audit by design (iteration-0 BH14/ECH12).
- BH7+ECH10+ECH11 the image rule misses indirection and Dockerfiles, and its exemption keys on `moduleId: eventstore` — low. The bypass repeats iteration-0 BH13/ECH13. The exemption also needs the exact path `src/Hexalith.EventStore/Hexalith.EventStore.csproj`.
- BH9+ECH3 `UsePrebuiltProjects` with declared extensions — low; iteration-0 BH8 rationale (Development+source opt-in, fails loudly with `eventstore-extension-unavailable`).
- BH12 `Bind` duplicates `PlatformLocalProfile.Bind` — low; the fix is a refactor, and the drift found in iteration 0 is now patched.
- BH13+ECH6 a zero-adapter extension reports Healthy — low; iteration-0 BH12 (no adapter minimum in the spec).
- BH14+AA6 AC2 is proven piecewise — low. The composition test asserts the exact `EventStore__Extensions__Packages__0__*` keys that the readiness test feeds the host, and the bindings-props build test covers the reference. A full Aspire run belongs to 3.6.
- AA3 source mode defines `HEXALITH_PLATFORM_EXTENSION_SDK` even without extensions — low as code: it follows the existing `HEXALITH_PLATFORM_SOURCE_CATALOG`/`CATALOG_SDK` source-mode switches. Its live harm comes from the EventStore force-push decision.
- ECH4 in source mode, a package-origin owner falls back to the catalog pin — false. The mapping entry's origin decides source or catalog (AD-4), and the same module's servers already refuse a non-source owner.
- ECH7 an unresolvable adapter-source attribute type or a throwing enumerator escapes without a named condition — low; the host still stops before serving, and the wrap adds branches.
- ECH12 HXP026 matches only an exact Dapr `configurationKey`, not a parent/child key — low; the fix adds a new segment-overlap rule for an unlikely declaration.

#### Review iteration 2 — full review, 2026-11-08

Initial triage: **3 decision-needed, 3 patch, 6 defer, 3 rejected**. After D1 and the approved D2/D3 implementations: **0 decision-needed, 6 patch (P5/P6 locally fixed), 6 defer, 3 rejected**. At review completion P1–P4 remained open; they subsequently landed through upstream PR #37 and are checked below. Four independent reviewers completed: Blind Hunter (BH), Edge Case Hunter (ECH), Verification Gap (VG), Acceptance Auditor (AA). The numbered source findings below belong to this iteration only.

Scope: Platform story paths from baseline f7c68398 through local HEAD 769507aa; EventStore extension feature b0ea2fce1..b13271ba1; Builds feature 3c809618..27145e52. Expanded diff: 44 files, +2605/-31, 3196 lines. Unrelated Story 2.8 changes were excluded except incidental changes in shared story paths. The immutable diff is /tmp/story-3-2-review-9_0ajpa_.diff. The checkout was clean before review.

Remote-state correction: the earlier iterations describe historical observations. At this review, Builds v4.45.0 resolves to 27145e52ffab5623198ba5efdab5cdb71aa42d28 and is published. EventStore main f14d56ea917dd33a552742226d6d7ee65f5e1236 contains b13271ba1; the earlier unreferenced-commit finding is resolved. The local Platform tool/tuple remains 4.44.13/3c809618, so its mismatch with the Builds gitlink remains open. No remote refs, pins, packages or implementation code were changed by this review.

- [x] [Review][Patch] P1: Add stable eventstore-extension-* conditions to the unbound-input, malformed-structured-input, unavailable-host and absent-gateway refusals — low (BH3+AA4). These four paths currently use prose only; direct condition additions let callers identify the refusal consistently. [src/Hexalith.Platform.Composition/PlatformEventStoreExtensionBindings.cs:109] [src/Hexalith.Platform.Composition/PlatformEventStoreExtensionBindings.cs:130] [src/Hexalith.Platform.Composition/PlatformServerBindings.cs:110] [src/Hexalith.Platform.Composition/PlatformServerBindings.cs:129]
- [x] [Review][Patch] P2: Cover profile fallback and structured extension inputs — medium (BH8+VG1). Positive fixtures inject scalar options.Configuration values, bypassing the profile and JSON branches. Add isolated local-profile.json fallback, object/array flattening, a flattened leaf conflicting with a gateway key, and malformed JSON cases. Deleting the fallback would leave the current 24 tests green even though normal Compose/ComposeRun depend on it. [tests/Hexalith.Platform.Composition.Tests/EventStoreExtensionCompositionTests.cs:282]
- [x] [Review][Patch] P3: Correct the enrollment guidance's input and collision guarantees — low (BH11). Qualify configuration-only inputs with the admitted AD-9 secret exception, and limit the collision guarantee to environment bindings present when Bind runs. Later configuration/catalog bindings and content-root settings are not inspected. [docs/domain-module-enrollment.md:322]
- [x] [Review][Patch] P4 (D1 direction resolved): Align the Builds gitlink with an admitted published tuple — high (BH1+AA1). The gitlink is 27145e52, while .config/dotnet-tools.json and eng/builds-tuple.json select 4.44.13/3c809618. DomainEnrollmentKitTests.AcceptedToolPinEqualsSupportedRangeFloorAsync fails with those exact SHAs. Tuple preflight run 42502023209 on reviewed HEAD 769507aa fails its exact-source initialization comparison. The changed 11-row expectation also assumes a newer tool than the current pin. User selected option 1 on 2026-11-08: prepare alignment with published Builds 4.45.0 through tuple qualification. Existing PR #37 at ab38224e contains that candidate; preparation and artifact/source-pair checks are verified below. Subsequently resolved by PR #37, merged as e39a9a2587ec150d550510763ab69db305d13fdb after its exact-source tuple and legacy checks passed; that main is now integrated into this branch. [tests/Hexalith.Platform.Composition.Tests/DomainEnrollmentKitTests.cs:123]
- [x] [Review][Patch] P5 (D2 resolved): Give declared extensions ownership of their adapter registration — medium (BH2+AA2). Program invokes AddEventStoreRunScopedCatalogGateway at line 33 and AddEventStoreExtensions at line 41. Both register every adapter from a shared package; IdempotencyIntentAdapterRegistry rejects the duplicate during hosted-service startup. This is reachable when Parties or Memories adopts the new declaration, since their packages already participate in AdapterAssemblies. User approved option 1's recommended implementation on 2026-11-08. Declared packages now register before AddEventStoreServer; bootstrap reuses their verified registrations and continues registering undeclared legacy packages. EventStore source commit 5ec3436778a498612b08ae354f8b2dad95d6093a and the accompanying Platform source changes passed 59 focused tests and independent review. SDK publication and Platform dependency adoption remain separate. Details: spec-3-2-extension-registration-owner.md. [src/Hexalith.Platform.EventStoreHost/Program.cs:41]
- [x] [Review][Patch] P6 (D3 resolved): Preserve the declaring module in runtime extension diagnostics — medium (AA3). EventStoreExtensionDeclaration and the generated environment carry only PackageId, ExtensionId and ApiMajor. Loader failures for drift, missing assemblies and unsupported majors therefore cannot name the declaring module, contrary to the frozen diagnostic requirement. User selected option 1 on 2026-11-08. Optional ModuleId metadata now travels from Platform's declaring-module identity into loader refusals, with both owners associated with their packages in duplicate errors. The existing three-argument constructor/deconstruction and legacy declarations/messages are preserved. EventStore source commit a74e05b5419863e749ce72dd24d9288f713f90af and the Platform companion passed 82 focused tests and independent review. Package rollout still requires the normal owner SDK release and subsequent adoption. Details: spec-3-2-extension-module-diagnostics.md. [references/Hexalith.EventStore/src/Hexalith.EventStore.Server/Extensions/EventStoreExtensionDeclaration.cs:7]
- [x] [Review][Defer] W1: Qualify condition/import/property evaluation in catalog package lookup — medium, unverified (BH4). ReadCatalogPackageVersion reads the first raw XML item/property rather than an evaluated catalog. Current extension pins are unconditional; no failing active extension cohort was demonstrated. Settle with a supported package-mode host build using conditioned/repeated/imported pins. [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionDeclarationOptions.cs:133] — deferred: extends the existing package qualification follow-up.
- [x] [Review][Defer] W2: Qualify generated PackageVersion Update against the host's central items — medium, unverified (BH5). Update creates no missing item, but a reachable restore failure with the selected snapshot and host catalog has not been demonstrated. Settle with a snapshot-pinned package-mode restore, including a package absent from the live catalog. [src/Hexalith.Platform.Composition/PlatformEventStoreExtensionBindings.cs:160] — deferred: extends the existing package qualification follow-up.
- [x] [Review][Defer] W3: Check the composed host's project-specific snapshot package view — medium, unverified (BH6). Lookup reads snapshot.Versions and does not select ProjectVersions, unlike CatalogVersionValidationTask. Current extension pins do not override the composed host's project view. Settle with a valid host-specific conditioned snapshot and resolved-version assertion. [references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionDeclarationOptions.cs:130] — deferred: extends package qualification with this specific boundary.
- [x] [Review][Defer] W4: Execute extension composition/readiness tests in CI — medium (BH7+VG2). The source-only feature tests are compiled out by tuple CI's package-mode build and are absent from its suite profile. Removing the host loader would escape that gate. [tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj:10] — deferred: already tracked; qualify a released package cohort and select its feature tests. A source CI exception would require a separate decision because repository guidance requires package references in CI.
- [x] [Review][Defer] W5: Exercise actual owner/extension generated-secret sharing — medium (BH9). The current test seeds the shared dictionary directly and never invokes the owner binder. [tests/Hexalith.Platform.Composition.Tests/EventStoreExtensionCompositionTests.cs:224] — deferred: retain the existing Memories-adoption follow-up; run both binders with empty admitted secrets and compare their outputs when Memories adopts.
- [x] [Review][Defer] W6: Verify snapshot-selected package versions through an actual restore — medium (VG3). The package fixture has no RunPlan and checks XML only. Ignoring Workspace would leave that test green. [tests/Hexalith.Platform.Composition.Tests/EventStoreExtensionCompositionTests.cs:116] — deferred: retain the existing released package-mode qualification follow-up; restore/build with a snapshot version different from the live catalog and assert the resolved version.

Decision D1 preparation — user selected option 1, 2026-11-08:

- Existing [PR #37](https://github.com/Hexalith/Hexalith.Platform/pull/37), current source ab38224ef7f212e3b060ce4a6923e5893d34ad4a, prepares tool 4.45.0, its range floor, producer/publication binding and complete signed validation pair. It also carries earlier review patches; those are existing external work, not changes applied by this review run. The local main checkout and its two review documents were preserved.
- Verified official producer source 27145e52 against successful push CI [42496435539](https://github.com/Hexalith/Hexalith.Builds/actions/runs/42496435539), successful publisher [42501966448](https://github.com/Hexalith/Hexalith.Builds/actions/runs/42501966448), and release v4.45.0. The candidate tool pin, range floor, committed Builds gitlink, catalog blob and catalog SHA-256 agree.
- Ran the candidate's unchanged eng/prepare_builds_artifacts.py against a new temporary output directory. Official NuGet archive SHA-256 dd99ad612367d6b0ffba69e24f93f16dcae649fb8edeb213f226f6c8864bc498, NuGet repository signature, Tooling hash 0c8c00e5563730f953f24b19b777dedd1887f4d186e92347145e55783d4d4981, and executable identity 4.45.0+27145e52ffab5623198ba5efdab5cdb71aa42d28 all passed. The official release inventory's SHA-256 is a428251a7546bb483ef5cdf8295adbd76df9b9e12bc487b0d391ac853f8ec08e and matches the candidate binding.
- Measured the actual archive's AppHost body as 0afbfd9bfe10a6d28cce6b09e375e1cfbbd581bcc6d501157eccacd3e3cc2954; its pair with the measured Tooling body appears in both the candidate admission code and its cross-product regression matrix.
- The candidate legacy source is 77a04b18b5e19c89482fb308010390fce1c624de. All 10 eng/test_legacy_tuple_source.py controls passed. Running eng/legacy_tuple_source.py --current ab38224ef7f212e3b060ce4a6923e5893d34ad4a --verify from the actual detached legacy worktree reports sourceEquivalence: verified, only the eight admitted metadata differences, requiredCount: 349 and qualificationCredit: false.
- Read-only hosted observation: all five [SDK consumer-binding gates](https://github.com/Hexalith/Hexalith.Platform/actions/runs/42505359931) passed for ab38224e. [Tuple preflight 42505359934](https://github.com/Hexalith/Hexalith.Platform/actions/runs/42505359934) passed exact-source initialization and remains in progress in its package-only Release verification step. This does not yet establish complete tuple qualification or landing.
- Temporary verification worktrees and receipts are retained at /tmp/story-3-2-tuple-forward-apbijkxq (current, legacy, artifacts/artifact-identity.json, artifacts/signature.stdout, source-pair-verification.json and release-inventory.json). No nested submodules were initialized. No push, PR mutation, merge, CI dispatch or publication was performed.

Source-finding verdicts, rendered before grouping:

| Source | Verdict | Evidence and disposition |
| --- | --- | --- |
| BH1 | high | Exact tool/gitlink source mismatch reproduced by the pin test; D1, now P4 after the user selected the forward tuple alignment. |
| BH2 | medium | Both registration paths add the same adapter and the startup registry rejects duplicate command identities; D2, now locally fixed as P5. |
| BH3 | low | Four reachable refusal messages omit a stable condition; a direct diagnostic correction; P1. |
| BH4 | maybe-false | Raw XML lookup ignores evaluation, but current extension pins do not exercise a conflicting condition; supported conditional package restore would settle it; W1, medium unverified. |
| BH5 | maybe-false | PackageVersion Update needs an existing item; whether the selected host/snapshot can lack it needs a real restore; W2, medium unverified. |
| BH6 | maybe-false | ProjectVersions is unused here, but no current extension package has a differing composed-host view; host-conditioned snapshot restore would settle it; W3, medium unverified. |
| BH7 | medium | Source feature tests are compiled out and unselected by tuple CI; W4. |
| BH8 | medium | Scalar configuration overrides bypass successful profile/structured branches in all positive fixtures; P2. |
| BH9 | medium | Shared-secret test uses a manually seeded dictionary rather than the owner binder; W5. |
| BH10 | low | A deferred iterator can throw outside the invocation wrapper, but the host still fails closed; rare adapter bug, extra catch branches; rejected. |
| BH11 | low | Documentation promises a broader collision check than the actual dictionary and contradicts its own secret exception; direct prose corrections; P3. |
| ECH1 | low | A sibling flattened collection key can extend gateway configuration, but its value is Platform-owned profile/configuration, not an untrusted module payload; another reserved-root guard is not justified; rejected. |
| ECH2 | low | A manually supplied scalar Packages section has no children and is treated as empty; Platform's binder always emits indexed children, so everyday composition cannot produce this shape; an extra guard is not justified; rejected. |
| VG1 | medium | Filed verification evidence shows profile fallback can be deleted without failing the checked tests; P2. |
| VG2 | medium | Filed verification evidence confirms missing CI execution of the Platform entry-point test; W4. |
| VG3 | medium | Filed verification evidence confirms no snapshot-selected restore/build test; W6. |
| AA1 | high | Existing no-extension qualification fails on the unchanged tool/tuple pin versus the new gitlink; D1, now P4. |
| AA2 | medium | Combined catalog/extension enrollment registers the shared package twice; D2, now locally fixed as P5. |
| AA3 | medium | Runtime declaration and generated configuration have no module field, making the required owner diagnostic impossible; D3, now locally fixed as P6. |
| AA4 | low | Same four prose-only Platform refusals as BH3; P1. |

Verification on the reviewed local checkout:

- Platform composition test project, Builds module test project and EventStore server test project built in Debug with zero warnings/errors. Root-declared dependency paths were used; no nested submodules were initialized. Builds and EventStore build logs: /tmp/story-3-2-builds-review-build.log and /tmp/story-3-2-eventstore-review-build.log.
- dotnet exec tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -class Hexalith.Platform.Composition.Tests.EventStoreExtensionCompositionTests — 24 passed, including generated host build and HTTP extension readiness. This does not establish full live-service readiness or live package-mode qualification.
- dotnet exec references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -class Hexalith.Builds.ModuleTool.Tests.PlatformManifestValidationTests — 200 passed.
- dotnet exec references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -method 'Hexalith.Builds.ModuleTool.Tests.PlatformAdoptionValidationTests.EventStore*' -method Hexalith.Builds.ModuleTool.Tests.PlatformAdoptionValidationTests.UntrackedEventStoreExtensionProjectIsReportedAsync — 19 passed.
- dotnet exec references/Hexalith.EventStore/tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.Tests.dll -class Hexalith.EventStore.Server.Tests.Extensions.EventStoreExtensionRegistrationTests — 24 passed.
- dotnet exec tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -method Hexalith.Platform.Composition.Tests.DomainEnrollmentKitTests.AcceptedToolPinEqualsSupportedRangeFloorAsync — 1 failed, expected release source 3c809618, actual gitlink 27145e52. Remote CI: https://github.com/Hexalith/Hexalith.Platform/actions/runs/42502023209.
- Total focused checks: 267 passed, 1 failed. Owner test assemblies were rebuilt before the counted runs; stale pre-existing binaries were excluded. No full suite or live package-mode qualification is claimed.

#### Review completion — 2026-11-08 (historical)

The user selected **Leave as action items** for P1–P4. Retain all four as unchecked patch items; no deferral reason is required because these remain open actions rather than deferred findings. All three decision directions are resolved. P5 and P6 are implemented and verified locally; P4's forward tuple candidate is prepared, with adoption still pending for this checkout. Story status and its sprint entry are both **in-progress** because unresolved action items remain.

Review outcome: **2 issues fixed locally, 4 open action items, 6 deferred, 3 rejected**. The implementation follow-ups retain their 82-test focused verification and separate SDK release/adoption requirements. The full story is not marked complete.

#### Merge preparation update — 2026-11-08

Upstream Platform PR #37 merged as e39a9a2587ec150d550510763ab69db305d13fdb and was integrated into this branch. It independently resolves P1–P4: stable refusal conditions, profile/structured-input coverage, bounded enrollment guidance, and the published Builds 4.45.0 tuple. The historical review-completion decision above records the earlier state; those four actions are now complete without duplicating the upstream changes.

P5/P6 landed in EventStore PR #399 as cab8363c2b6e2c0588efc8ca9c2e09fab0006841. Its resulting tree was verified against the reviewed fixes combined with main at merge time. The Platform source gitlink now records that accepted main revision, and the legacy/current source pair is refreshed for these exact inputs. On the user's renewed instruction to merge all branches, the available administrator merge option was used despite EventStore's existing pre-compilation prerequisite failure (HXW011 untagged Platform identity and HXW009 mismatched Builds sources). The dependency admission checks and branch protection configuration were retained; this records an administrative merge, not successful hosted qualification.

Story status remains **in-progress** while the recorded package-mode/CI qualification follow-ups remain outstanding. This preparation does not publish an SDK or enable package-mode extensions.

Rejected appendix:

- BH10 — low: throwing deferred adapter inventory still stops startup; wrapping another uncommon failure adds catch branches without changing fail-closed behavior.
- ECH1 — low: sibling configuration leaves require a Platform-owned value; a new reserved-section policy is not a direct correction and repeats the earlier rejected finding.
- ECH2 — low: scalar-only raw Packages configuration is not emitted by the composer; adding a guard for this manually malformed shape is not warranted.

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


### Resumed review — 2026-11-09

The three independent review layers examined the pre-fix diff staged at `/tmp/story-3-2-acceptance-392yu4by.diff` against the preserved baseline. The workflow regenerated that file after the corrections. Every finding is classified below before grouping. Existing findings retain their prior verdict/route and are not patched or deferred again.

| Source | Verdict | Evidence and route |
| --- | --- | --- |
| BH1 | maybe-false | Ordinary source launches share host output paths, but no supported concurrent run with distinct extension inventories was demonstrated to consume incorrect artifacts. Defer as medium, unverified; a bounded concurrent two-composition startup/build and loaded-inventory assertion would settle it. |
| BH2 | low | The fixed Platform-owned host path is checked for existence rather than physical containment. The described substitution requires an explicitly modified local Platform project symlink; this is unlikely in ordinary use and another trust guard adds complexity. Reject. |
| BH3 | maybe-false | carried: generated PackageVersion Update against an absent central item is already W2/BH5. Preserve its deferred medium-unverified package-restore qualification; do not append it again. |
| BH4 | maybe-false | carried: raw conditioned/imported catalog lookup is already W1/BH4. Preserve its existing deferred medium-unverified route. |
| BH5 | maybe-false | carried: host-specific snapshot ProjectVersions lookup is already W3/BH6. Preserve its existing deferred medium-unverified route. |
| BH6 | medium | carried: the missing snapshot-selected package restore assertion is already W6/VG3. Preserve the existing deferral. |
| BH7 | medium | carried: package-only tuple CI omits the source feature suites, already W4/BH7/VG2. Preserve the existing deferral. |
| BH8 | medium | carried: the owner/extension secret test bypasses the actual owner binder, already W5/BH9. Preserve the existing Memories-adoption deferral. |
| BH9 | medium | carried: host csproj-only artifact identity is already deferred to Story 3.9 in review iteration 1. Preserve that route. |
| BH10 | medium | Initial searches confirmed no tests asserted the named host-unavailable and gateway-unavailable branches. The new missing-host test also exposed the omitted declaring module. Fixed with two atomic-refusal tests and a one-line owner-aware diagnostic correction, preserving the condition. |
| ECH1 | false | carried: package startup remains unavailable, as already recorded by iteration-0 ECH16 and the later explicit deferral. This run makes no package-qualification/completion claim and retains in-progress status; the frozen no-claims constraint is respected. |
| VG1 | medium | Successful Platform composition cases enroll one extension; second-module cases only reject duplicates. Truncating Resolve to one entry would escape them. Patch a successful two-extension composition, generated references and both indexed configuration entries. |
| VG2 | medium | carried: the missing normal CI execution is the same W4 finding; preserve the existing deferral. |
| VG3 | medium | carried: snapshot-selected restore is the same W6 finding; preserve the existing deferral. |
| VG4 | medium | carried: actual owner/extension generated-secret sharing is the same W5 finding; preserve the existing deferral. |

Grouped routes: two patch entries (BH10 and VG1), one new medium-unverified deferral (BH1), eleven carried decisions, one new low rejection (BH2). No intent or specification loopback is required.

- [x] [Review][Patch] R3-P1: Add successful composition coverage for two distinct enrolled extensions, both generated references and both host configuration entries.
- [x] [Review][Patch] R3-P2: Add missing-host and missing-gateway refusals, including stable conditions, declaring module and resource atomicity.


### Resumed build review — 2026-11-09, broad-verification continuation

Three independent reviewers examined the Platform story implementation and the EventStore/Builds owner diffs. The complete Platform baseline diff is retained separately from the focused implementation review artifact; unrelated Story 2.8 changes were excluded from feature review. Each source finding is classified before grouping. Prior routes are preserved under the resumed-review rule.

| Source | Verdict | Evidence and route |
| --- | --- | --- |
| BH1 | low | carried: content-root and later-bound gateway settings are the previously rejected iteration-0 BH4/ECH2 and iteration-1 ECH15 boundary. Values require Platform-owned configuration/profile input. Preserve rejection; guidance already states the bounded comparison. |
| BH2 | low | `ConfigureEnvironment` sets `DOTNET_ENVIRONMENT` before applying the binding dictionary, so an explicitly supplied scoped value could replace it. The declaration alone supplies no value and unbound inputs are refused. No existing extension uses process-control keys; adding a reserved-runtime-key policy adds new guard state for deliberate operator configuration. Reject as uncommon and nontrivial. |
| BH3 | low | carried: `UsePrebuiltProjects` with newly generated bindings is iteration-0 BH8/VG-O4 and iteration-1 BH9. Preserve rejection: Development/source opt-in and missing extensions fail loudly at host startup. |
| BH4 | low | carried: the unescaped semicolon in an MSBuild property path is iteration-0 ECH8. Preserve rejection; this follows the existing Platform-controlled workspace/property path convention. |
| BH5 | low | carried: inherited/imported direct references are iteration-0 BH14/ECH12 and iteration-1 BH6. Preserve the bounded source-audit rejection. |
| BH6 | low | carried: direct assembly/HintPath reference forms are the same iteration-0 BH14/ECH12 bounded audit finding. Preserve rejection. |
| BH7 | maybe-false | carried: raw conditional/import/property catalog lookup is W1/BH4. Preserve its medium-unverified package-restore qualification; do not append a duplicate deferral. |
| BH8 | low | carried: lazy adapter inventory/reflection failures escaping the named wrapper are iteration-1 ECH7 and iteration-2 BH10. The host still fails before serving. Preserve rejection. |
| BH9 | low | carried: scalar-only Packages configuration is iteration-2 ECH2. Platform emits indexed scalar identities; malformed hand-written sections are outside that normal path. Preserve rejection. |
| BH10 | low | Structured JSON can introduce an invalid environment name such as `Bad=Name`, which is not checked by the existing flattener. This requires malformed Platform-owned configuration/profile data; current declaration keys and values do not exercise it. Adding platform environment-name validation is a new guard for an uncommon operator error. Reject. |
| BH11 | low | carried: generated-reference compilation and host readiness are distinct tests, already iteration-1 BH14/AA6. Preserve rejection and the existing whole-environment proof in Story 3.6. |
| ECH1 | false | carried: package startup is an explicit deferred qualification prerequisite, as recorded by iteration-0 ECH16 and the earlier resumed ECH1. No package-startup qualification or story-completion claim is made. Preserve that existing route. |
| VG1 | medium | carried: automatic tuple verification excludes source-only extension tests, exactly W4/BH7/VG2. Preserve the existing released-cohort/CI follow-up; no duplicate deferral or unapproved source-CI exception. |

Grouped outcome: eleven carried routes and two new low rejections; no new patch, intent gap, specification loopback or deferred ledger entry. Review completion does not satisfy the still-running broad-verification gate.


### Review iteration 2026-11-21

Three independent layers reviewed the Story 3.2 changes in the composite original-baseline diff. The isolated follow-up head `cf002bace462b681179723b4bb65adfd6e034a98` passed 465 hosted package cases, all 37 extension source cases and the ordinary native MTP readiness case. The focused follow-up diff and the hash of the full original-baseline diff are retained with the verification evidence. BH is Blind Hunter, EC is Edge Case Hunter, and VG is Verification Gap.

| Finding | Verdict | Evidence and route |
| --- | --- | --- |
| BH1 | low | The normal entry reads the local profile and the programmatic entry accepts `PlatformCompositionOptions.Configuration`. The guide can be read as promising forwarding of AppHost configuration. Clarify the two entry points without inventing an override contract. Patch docs. |
| BH2 | low | Carried. The explicit Development source prebuilt path can contain older generated bindings. Preserve the iteration-0 BH8/VG-O4 and resumed review disposition. |
| BH3 | low | Carried. Catalog and appsettings bindings occur after the documented collision boundary. Preserve iteration-1 BH2/ECH2 rejection and existing guidance. |
| BH4 | medium | Structured leaf names differing only by case or nested key syntax can collapse before the overlap check. Reject the duplicate within extension-bound structured input and preserve the owner-profile flattening behavior. Patch with a regression case. |
| BH5 | low | Package reference generation and runtime admission are separate stages. The generic mapping paragraph needs to state that the verified package runtime path admits the signed fixture while ordinary enrollment still needs its own approved cohort. Patch guidance. |
| BH6 | low | Carried. Source project-reference compilation and source host readiness are distinct tests. Preserve iteration-1 BH14/AA6 and the broader source-environment proof in Story 3.6. The signed package path has its own ordinary native proof. |
| BH7 | medium | The selected closure's project path is the composed host while the artifact kind says published seed. Correct the artifact kind and retain the original seed path and hash for a composed host. Patch. |
| BH8 | low | The individual provisioner can no longer inspect a container while Docker is unreachable. The ordinary runner independently discovers labeled resources and reports unverified cleanup instead of granting an empty disposition. Exact SIGKILL recovery was exercised. No standalone provisioner contract is introduced. Reject. |
| BH9 | medium | A push can finish before feed indexing or signature verification times out. Retry currently stops on the existing immutable version before authenticating it. Allow the duplicate response to proceed only to the same independent signed public byte checks. Patch. |
| BH10 | medium | The verifier compares the bodies it sees but accepts other package payload members. Bound the fixture archive layout to its known documents, binary, documentation and NuGet metadata and keep the full byte comparison. Patch with an unexpected payload control. |
| BH11 | low | README and enrollment guide advertise stale CI test counts. Patch to the enforced current inventories. |
| EC1 | low | Carried. The module workspace punctuation restriction is iteration-0 ECH8 and the later resumed review. The current reproduction confirms the same MSBuild behavior. Preserve the existing disposition. |
| EC2 | low | The new protection Docker mount has its own comma field syntax. CSV-encode the supported mount specification to preserve the source path rather than changing the older MSBuild workspace contract. Patch. |
| EC3 | medium | Same structured-input overwrite as BH4. Patch in the shared root-cause group. |
| EC4 | medium | Same immutable publication retry as BH9. Patch in the shared root-cause group. |
| VG1 | medium | The public hosted startup checks component readiness, so they do not cover the ordinary model's protection settings. The real ordinary signed runner remains a required local acceptance check and its protected exact-head execution is recorded. Add a focused configuration check where it fits the existing fixture controls. Patch. |
| VG2 | medium | The host-written observation alone cannot demonstrate the parent's retention and authentication hook. Require the exact parent's retained observation in native acceptance and add a deterministic refusal control for a foreign run receipt. Patch. |
| VG3 | medium | The positive reader case cannot exercise the rejection of a differently encoded route facet. Add a targeted refusal control to the required fixture inventory while retaining the actual positive reader execution. Patch. |

## Design Notes

A new field, not `runtime.extensions[]`: every current manifest uses that field for owner server packages that carry secrets (Parties, Memories, Tenants, Conversations), so applying AD-13 to it would reject all of them. Declared majors are checked at composition time, so the failure comes before startup. Host startup verifies the actual assembly attribute, so a declaration cannot drift from the binary.

## Verification

**Commands:**
- `dotnet build -nodeReuse:false` in each touched repo — expected: 0 warnings, 0 errors (analyzers are errors).
- `dotnet exec tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -class Hexalith.Platform.Composition.Tests.EventStoreExtensionCompositionTests` — expected: all pass. Then a full run compared against the baseline failures.
- Builds `dotnet exec …/Hexalith.Builds.Module.Tests.dll -class …PlatformManifestValidationTests`, and the EventStore Server tests for the extension classes — expected: pass.


### Resumed verification and review fixes — 2026-11-09

The original workspace was verified at Platform `6381fd6f4d36eeef03e0b947b7fe701d1e4ffbb2` with EventStore `cab8363c2b6e2c0588efc8ca9c2e09fab0006841` and Builds `27145e52ffab5623198ba5efdab5cdb71aa42d28`. The review corrections are prepared in scratch worktree `/tmp/story-3-2-verification-20261109`, branch `docs/story-3-2-verification-20261109`, from that same cached Platform revision. Ten root-declared dependency worktrees are checked out at the exact recorded gitlinks; no remote or nested-submodule operation was used.

Changes: three regression tests cover successful composition across two distinct owner modules/roots and both named unavailable-host/gateway refusals. The missing-host case exposed a missing declaring-module diagnostic; a one-line correction adds that module while preserving `eventstore-extension-host-unavailable`. Both review patch entries are complete. The frozen block and preserved baseline are unchanged.

Final focused evidence: **708 unique tests passed**, with no failures, skips or not-run cases: 33 extension composition, 7 gateway registration/seed, 1 tool-pin alignment, 50 EventStore registration/diagnostic and 617 Builds manifest/adoption cases. The scratch Platform test project and the original owning EventStore/Builds test projects have final builds with **zero warnings and zero errors**. The EventStore/Builds sources were not changed by these corrections; their existing focused evidence remains applicable. Parent verification independently read the final source diff and 33-test XML, then ran the 7 gateway cases and pin check against the scratch binary.

The original-workspace broad Composition run did not finish with a test summary or result XML. Its runner and wrapper are no longer present; its tool session no longer exists. The retained log contains **163 observed failed rows and 12 skipped checks**, including package-byte admission, catalog, Timesheets and fixture cases. These are not final suite totals, and no exact comparison with the historical failure set is claimed. Log: `/tmp/story-3-2-implementation-platform-full.log`; observed failure list: `/tmp/story-3-2-full-failures.txt`. This remains a broad-verification limitation; the failed broad gate was not weakened or treated as passing.

All frozen matrix rows have passing focused coverage at their approved scope. The source startup/readiness path and generated package-reference path are distinct proofs; released package startup and CI qualification remain outstanding. The existing snapshot/restore, owner-secret and artifact-identity follow-ups retain their earlier routes. The new concurrent source-output isolation concern is medium, unverified, and is recorded once in the scratch deferred-work ledger.

Status remains **in-progress**, matching sprint tracking. No SDK publication, dependency-pin change, module-manifest adoption, commit or remote mutation was performed. The build workflow stops at the unresolved broad-verification result rather than marking the story done.

Receipt: [local verification](evidence/3-2/local-verification-20261109/verification.json). Exact scratch build/test commands: `/tmp/story-3-2-review-fix-commands.txt`. Final result XML files: `/tmp/story-3-2-review-fix-tests.xml`, `/tmp/story-3-2-final-gateways.xml`, `/tmp/story-3-2-final-pin.xml`.


### Completed broad comparison — 2026-11-09

The previous missing-summary limitation is resolved. Both complete class inventories finished with result XML and no class timeouts: current Platform `59fb32933d76a7e71d9479e1903087a73ea66f2b` has **955 cases: 782 passed, 161 failed, 12 skipped, 0 not run**; the preserved baseline `f7c68398cdcb494d535e789e3d9577af9e8d5d36` has **919 cases: 744 passed, 163 failed, 12 skipped, 0 not run**. Each revision used all 17 direct gitlinks recorded at that revision, with no nested initialization, pin change or remote Git operation. Both Debug builds have zero warnings and errors.

The comparison retains all cases, including four truncated-display-name groups with equal all-pass multiplicities and unchanged test source. It finds **160 common failures, 36 added passing tests, no removed tests, three resolved tool-pin failures and one regression**. The regression is `PackageCatalogParticipantsBindBeforeContainersOrSidecarsAreAdded`, strengthened in Story 2.8 commit `6381fd6f4d36eeef03e0b947b7fe701d1e4ffbb2` to use the real package fixture; it now reaches the existing genuine-published-Memories-Contracts admission prerequisite. It is attributed explicitly rather than described as an identical failure set. The existing package/cohort fixture qualification route remains; no guard or assertion was weakened.

Fresh focused Platform proof is **45/45**: 33 extension cases, 7 gateway registration/seed cases and 5 enrollment/tool-pin cases. The initially missing locally pinned tool was restored and the rerun retained alongside the initial result. The unchanged EventStore/Builds owner commits retain their previously passing 50/617 focused cases, whose logs and hashes were checked. Generated package-reference proof remains distinct from released package startup qualification.

All three independent reviews finished; the table above records eleven carried routes and two new low rejections, with no new patch or duplicate deferral. This continuation changes verification evidence and review records only. **Status remains in-progress**, matching sprint tracking: the broad gate is red and selected-package extension enablement/startup/CI qualification remains pending. The workflow cannot truthfully mark the story done. The frozen intent and original baseline remain unchanged; publication, SDK adoption and main-checkout changes are outside this continuation.

Durable receipt: [broad verification](evidence/3-2/broad-verification-20261109/verification.json), [comparison](evidence/3-2/broad-verification-20261109/comparison.json), and [regression attribution](evidence/3-2/broad-verification-20261109/regression-attribution.json). Raw class XML/logs and exact replay commands remain in `/tmp/story-3-2-broad-20261109-_z_wy84q`. Changes are prepared on scratch branch `docs/story-3-2-broad-verification-20261109`; concurrent Story 2.8 work in the main checkout was preserved.


### Authorized package preparation continuation — 2026-11-09

User intent: continue from `27a1638e0e7d66b672708507d95a44fe31050718` on the existing scratch branch `docs/story-3-2-broad-verification-20261109`, worktree `/tmp/story-3-2-broad-20261109-_z_wy84q/current`. Keep the original baseline and frozen block unchanged. Work only in that scratch worktree. Main checkout and concurrent Story 2.8 work are outside the change. Read the retained broad-verification receipt before work.

The user authorizes concrete local preparation, tests, read-only published-release inspection and disposable local package experiments. Publication, release dispatch, remote mutation and changing dependency pins/gitlinks still require explicit scope approval. No module manifest adoption. Local candidate NuGet archives are disposable test artifacts, never a published release or qualified package cohort. Do not weaken signed-body, provenance, source, image, declaration or host admission checks to obtain a pass. Do not mark this story done.

Current verified release discovery: official NuGet flat-container indexes list EventStore DomainService/Server through 4.1.0; `dotnet-inspect type '*EventStoreExtension*' --package Hexalith.EventStore.DomainService@4.1.0 --json` finds no type. GitHub latest EventStore release is v4.1.0 (2026-11-04). Builds.Module.Cli releases include 4.45.0 and 4.45.1; selected 4.45.0 source is `27145e52ffab5623198ba5efdab5cdb71aa42d28`. Verify actual archive contents/APIs and retain URLs, hashes and results rather than assuming a source release contains them.

Preparation tasks (additional to the original implementation):
- [x] Under `evidence/3-2/package-preparation-20261109/`, record official published release/API inspection and the exact EventStore source/release prerequisite. Avoid retaining large binaries in Git.
- [x] In `PlatformEventStoreExtensionBindings.cs` and focused tests, prepare valid central package references including an extension absent from the live catalog; prove selected snapshot versions through a real disposable restore/build, including a host-specific package view if supported. Preserve validated snapshot authority; do not invent version authority. Where an owner SDK correction needs a new release, prepare a reviewable owner patch rather than moving a gitlink.
- [x] Prepare a concrete forward adoption patch and release/adoption document in that evidence folder for package SDK feature constants (Composition, EventStoreHost, tests, fixture), finite host admission and automatic CI suite selection/counts. Keep it unapplied if it needs an unreleased SDK, new pins or expanded cohort authority. Never add a generic host-path bypass. Explain exact release/tuple/publication identity work still required.
- [x] Provide a reproducible local candidate package probe in that evidence folder: locally pack the accepted EventStore extension API source and Platform fixture at an unmistakable nonpublic test version, restore/build the actual Platform host through generated PackageReferences, assert resolved versions/deps package type, start that host, and record HTTP extension readiness id/major. The host must load the packaged fixture rather than a copied source output. Preserve process cleanup and bounded timeouts. Label this candidate evidence, not released package qualification. If a narrower supported probe is required, record its precise limit and failed prerequisite.
- [x] Run relevant focused source and unchanged-pin package tests, preserve named failures and compare them to the retained baseline receipt. The Story 2.8 `PackageCatalogParticipantsBindBeforeContainersOrSidecarsAreAdded` genuine Memories Contracts fixture failure remains separately attributed. Retain all admission checks. Record exact commands, counts, results and source identity.

Acceptance for this preparation: Given unchanged selected dependency pins and the retained baseline, when local preparation completes, then the release gap, reviewable adoption diff, generated-reference tests and real candidate package restore/startup evidence are independently inspectable. Released-package/automatic-CI qualification remains pending until approval and successful owner release/adoption; no successful preparation substitutes for the remaining original Story 3.2 acceptance requirements.


Preparation result: [release/adoption proposal](evidence/3-2/package-preparation-20261109/release-and-adoption.md), [verification](evidence/3-2/package-preparation-20261109/verification.json), [published API inspection](evidence/3-2/package-preparation-20261109/published-inspection.json), and [retained comparison](evidence/3-2/package-preparation-20261109/retained-comparison.json). The generated reference correction and two real snapshot restore/build rows are applied locally; owner SDK, feature enablement, finite admission and seven-case automatic CI changes remain unapplied proposals. Parent final source verification is 47/47, owner snapshot verification 6/6, and unchanged-pin package verification 77/82. All five package-class failures already fail in the retained baseline; Story 2.8's modified-tree guard observation remains distinct from its retained genuine Memories Contracts prerequisite. The real candidate-package host reports `platform-test@1` Healthy over HTTP, with overall readiness 503 without Dapr. No released-package or CI qualification is claimed. The failed EventStore exact-source CI remains a release prerequisite. Frozen intent, original baseline and all dependency pins remain unchanged; concurrent main-checkout changes were preserved. **Status remains in-progress**, pending explicit release/adoption scope approval and successful remaining original acceptance requirements. Workflow remains at the unresolved implementation/acceptance boundary; no completion transition is taken.


## Approved release and adoption scope amendment — 2026-11-09

The maintainer explicitly approves expanding the frozen scope to **land, push and release the EventStore extension SDK and proposed Builds consumer-view correction, then qualify and adopt their measured dependency pins through the existing tuple process**. This approval supersedes the original no-publication/no-release/no-push/no-pin-change and scratch-only landing restrictions, and the workflow's no-remote-operations restriction, only for this Story 3.2 release/adoption work. Original frozen wording and all historical evidence remain intact as history. No additional approval is required for these authorized operations. **No production deployment or module-manifest adoption is authorized.** Preserve every source, signature, provenance, declaration, image and host admission check; no administrative qualification bypasses.

Resume the existing worktree `/tmp/story-3-2-broad-20261109-_z_wy84q/current`, branch `docs/story-3-2-broad-verification-20261109`, starting at `27a1638e0e7d66b672708507d95a44fe31050718`, including its prepared uncommitted changes. This worktree is the operative `{project-root}` for implementation and context files. Main checkout `/home/administrator/projects/hexalith/platform` has separate concurrent Story 2.8 and other work; do not change it, its checked-out branches, local files or dependency checkouts. Use additional isolated owner worktrees from rechecked upstream when needed, preserving unrelated changes and current upstream history. The preserved original story baseline remains `f7c68398cdcb494d535e789e3d9577af9e8d5d36`; never replace it with this resumption head.

Read these receipts first and retain them unchanged: `evidence/3-2/broad-verification-20261109/verification.json`, `evidence/3-2/package-preparation-20261109/verification.json`, and `evidence/3-2/package-preparation-20261109/release-and-adoption.md` (paths relative to this spec). The backup and per-file prepared hashes are recorded in [scope receipt](evidence/3-2/release-adoption-20261109/scope-amendment.json). Append fresh measurements and commands under `evidence/3-2/release-adoption-20261109/`; never overwrite historical receipts to imply later success.

Additional implementation tasks, ordered by dependency:

- [x] Recheck actual current upstream revisions and official published releases for EventStore, Builds and Platform. Read owner AGENTS.md and normal CI/release workflows. Record exact full revisions and GitHub run/release URLs. Current cached sources and historical observations are not sufficient.
- [x] Diagnose and resolve failed EventStore release-source qualification (historically `cab8363c2b6e2c0588efc8ca9c2e09fab0006841`, CI run `42519687843`, HXW011 untagged Platform identity / HXW009 mismatched Builds sources). Preserve exact-source and dependency admission; make any necessary reviewed prerequisite alignment through normal owner/tuple controls. Do not dispatch its release before owner preflight succeeds for the exact approved release source.
- [x] Review, verify, land, push and release the required EventStore extension SDK and Builds consumer-view correction through their normal release gates. Existing approved owner proposal is `evidence/3-2/package-preparation-20261109/builds-consumer-snapshot.patch`; adapt to current upstream rather than overwriting it. Retain the two-argument ABI; named consumer selection must fail closed for missing snapshot/view or tampering, and must read Builds-materialized consumer views. Preserve registration ownership and module diagnostics. Record actual release source, producer/publisher records, signed archive measurements and versions.
- [x] Complete the intended Platform adoption (proposal `evidence/3-2/package-preparation-20261109/platform-forward-adoption.patch`) with real measured versions and artifact identities. Update catalog, tool, source gitlinks and complete tuple metadata together through the exact-source current tuple, preserving concurrent upstream work. Until Story 2.13 lands, the existing same-source legacy L/M process still gates CI; after it lands, no L/M companions are required ([2026-11-15 correction](../planning-artifacts/sprint-change-proposal-2026-11-15.md)). No placeholder values or generic host-path admission bypasses. Finite host admission must bind the exact approved declaration/extension, Platform host, seed, signed SDK bodies, publication and image identity; add required finite registry/cohort selection and negative controls.
- [x] Complete automatic package-mode CI selection with strict expected counts and all required tests actually running (the proposed initial group is seven cases; update counts if tests change). Prove signed published-package restore/build and actual Platform host startup reporting declared extension id/major in the `eventstore-extensions` readiness result. Assert package assets/deps identities and that the host loads the packaged extension, not copied source output. Retain bounded subprocess cleanup. Candidate-only/local unsigned package proof cannot substitute for published qualification. Distinguish extension health from overall distributed readiness.
- [x] Run relevant source/package and owner tests, compare against retained full inventories and record exact revisions, commands, results and unresolved failures. Keep the Story 2.8 package-fixture failure separately attributed, including its genuine Memories Contracts prerequisite versus modified-input guard observation. Preserve all source/signature/provenance/declaration/image/host checks.

Amended acceptance: Given reviewed exact-source owner releases, when their measured catalog/tool/SDK pins are qualified and adopted through the existing tuple process, then source and signed published-package composition each assemble exactly one EventStore gateway, the actual host reports the declared extension in readiness, automatic package-mode CI executes its required inventory, and no-extension gateway/cohort behavior retains its existing admission guarantees. Do not mark this story done until every remaining original and amended acceptance requirement is satisfied. If an external gate cannot be resolved within the authorized work, retain in-progress status and exact evidence; do not substitute a partial local probe for release qualification.


### Release/adoption progress — 2026-11-09, before Platform pin adoption

EventStore 4.2.0 was published through the normal exact-source gates from
`aa16227bd07f0564a3026d619a5abd973e02656f` (publisher 42623038544). Eight official
NuGet archives passed signature/source/body verification. Its unchanged released
v2 declaration hashes to 21eda6da824d5ae6135ff342dd8a5aae950b02420319b953a836dddeb00ce495.
Builds' reviewed named-consumer API landed normally in PR #179 at 2cc24dbb9d65654b696b54d49c16afe1f3f67b4d;
the canonical SDK4.2 catalog/host-view update remains in PR #180, with its normal
CI gates pending on 5a49eea. The first five direct-catalog unit failures were
attributed to synthetic fixture 4.1 provenance/version mutations and corrected;
all 19 direct-catalog/consumer-view controls pass locally. The owner release is
still required before Platform adoption.

The SDK4.2 default is being qualified separately from extension admission.
Two generated-reader runs passed 14 positive/refusal cases each, first on the
old catalog's MCP2.3.1 and then on upstream's preserved MCP2.4.0. They verify
actual signed archive/source/body identities and retain 23 routes/12 policies
for the private EventStore reader and 70/46 for public Parties. The 92 finite
component controls pass with no skips; 256 full-project capability evaluations
pass; all seven existing owner OCI indexes were remeasured. Five actual
signed-tool/seed/source-owner/ordinary-reader/model cases still fail closed on
the unchanged Builds4.45/SDK4.1 tuple and must pass after its real replacement.
The automatic draft requires 97 default-cohort cases plus the original nine
extension component cases, while preserving all 412 and 349 historical cases in
separate authenticated same-code tuple histories. Those histories and the
complete new tuple still require the existing L/M refresh; no new default or
Platform source release has been adopted from local component evidence.

The fixture publisher now has a reviewable exact-source path and an explicitly
Platform-owned pure-extension declaration/producer body inside its package.
It grants no present owner/host admission. All 14 Node and 7 Python publisher
controls run automatically in the draft tuple workflow and before publication.
GitHub production/main publisher controls were configured with explicit user
authorization. Creating the exact NuGet account-side Trusted Publisher still
requires an authenticated NuGet account session unavailable through the current
tools. Signed publication, its measured finite host cohort, actual packaged
host startup, the real named-consumer rollout and remaining original acceptance
remain incomplete. The original broad inventory and Story 2.8 genuine Memories
Contracts prerequisite versus modified-input guard observation remain separately
attributed; none of their retained evidence was rewritten.

Append-only evidence and exact controls/refusals:
[evidence](evidence/3-2/release-adoption-20261109/sdk42-no-extension-preparation/receipt.json)
and [revised stage plan](evidence/3-2/release-adoption-20261109/revised-stage-one-and-fixture-plan.json).
Status remains **in-progress**.


Source Debug recheck before adoption: 36 extension cases and 12 enrollment/seed/registration
companions pass, preserving all 47 earlier cases and adding the no-extension seed
control. The actual SDK4.2/Tenants7.0.5 consumer-binding executable also passes
all 15 cases against the refreshed owning catalog. The new historical replay
controls pass 8 closed-metadata, 12 legacy-transformation and 7 SDK/profile/pin
cases; parent independently inspected and ran these controls. These remain
preparation evidence, as recorded in
[pre-tuple verification](evidence/3-2/release-adoption-20261109/verification-before-published-tuple.json).
Concurrent Builds main adopted the SDK4.2 defaults independently (7564c96/a8a8716);
PR #180 is narrowed to the actual host consumer marker/view correction and its
refreshed audit. Its previous green head could not merge because that upstream
adoption conflicted; the normal merge returned HTTP 405 and the isolated rebase
preserves the adopted upstream result. A fresh normal CI/release is required
for the combined source. No Platform pin has changed.


### Measured first-stage candidate after upstream SDK4.2 integration — 2026-11-09

Both approved owner releases are now published through their normal gates: EventStore4.2.0 from `aa16227bd07f0564a3026d619a5abd973e02656f`, and Builds4.47.0 from `5ec1779040606be89306129a7d2b245258c9acd2`. Official signed archives, exact producer/publisher records, catalog, Tooling, seed and body identities were measured independently. The actual named EventStoreHost snapshot lookup and atomic4.47/4.2 tuple are integrated locally, preserving upstream081178aa and every canonical finite admission rule.

At clean merged source `53dbd485baa9842886b1d615b7f56fa441cf4738`, the complete current package inventory passes562 executions across559 unique cases: the preserved upstream456 profile,97 additional finite default-cohort controls, and9 required extension component cases. Exactly three methods execute in two profiles and are named in the receipt. No cases fail or skip; source and measured output snapshots remain identical. The first external-output attempt's560 passes and two DomainEnrollment repository-root lookup failures remain retained; the full rerun uses ignored in-repository outputs, matching normal CI, without changing assertions. Source36 extension plus12 companion cases and all9 real MSBuild metadata controls pass. The unchanged actual Story2.8 fixture now passes1/1 under the merged actual-default SDK selection; its original M23 Contracts failure and earlier8a9 SDK guard refusal remain separate historical evidence.

The normally published upstream Platformv1.9.0 binds081178aa and does not include this fixture/native publisher work. Our exact source still requires refreshed real same-code349/412 replay sources, all five tuple and six catalog jobs, normal landing and its own qualified source release. The reviewed metadata-only replay assembler is prepared but has not run. The fixture publisher automatically requires14 Node and30 Python controls, immutable declaration/producer/native entry bodies, the exact qualified source release and normal NuGet OIDC identity. Signed fixture publication, a subsequent owning Builds-materialized actual fixture pin, the finite expanded producer/reader/host cohort and signed packaged-host startup remain incomplete. This local proof grants no hosted qualification, fixture admission or signed-fixture credit.

Append-only [merged local controls](evidence/3-2/release-adoption-20261109/integrated-sdk42-local-controls-53dbd485/verification.json) retain raw commands, XML, source/output guards, builds, failures and history checks. The original baseline, frozen block,23 preparation files and19 broad evidence files remain byte-preserved. Status remains **in-progress**.


First frozen tuple observation: the real N source `baafebd8` passes all412 local historical cases with unchanged source/output guards. The L source `87bf914e` executes all349 but retains57 exact Parties-source/declaration refusals (292 pass): the original349 cohort requires sourceBB278312/40cee declaration, whereas N/current requireEBE/d9c. The next finite replay preparation records that exact typed dependency transformation and adds negative histories plus post-initialization owner/body checks; no349 assertions or production admission rules change. All20 legacy/checkout,9 pre-extension and19 tuple-binding controls pass locally.

Normal hosted run42684861205 refused all three lanes before xUnit because a synthetic tuple-binding control still supplied an empty catalog to the new required4.1 catalog check. The isolated fixture now supplies its single4.1.0 row; the real catalog guard is unchanged. All six catalog rows in42684861123 passed. GitHub also refused the new fixture workflow's two job-level runner contexts in42684848458; proof paths now bind through first runtime GITHUB_ENV steps with identical RUNNER_TEMP values. Actual actionlint1.7.12 passes all four changed workflows, and all29 automatic source-release controls pass. Original frozen refs, raw failures and parent verification remain retained in [first frozen tuple evidence](evidence/3-2/release-adoption-20261109/first-frozen-tuple-refusals-and-repair/receipt.json). A fresh real same-code freeze and normal hosted qualification are still required; no old frozen source is adopted and status remains **in-progress**.


### Qualified first-stage source and exact fixture publication refusal — 2026-11-10

The first-stage Platform adoption landed normally in PR #39 as `6e43e042347358e522bfbb2f909986c95fadc6ec`, with reviewed tree `680eb05194dac6e443e677743c87875539d2e7ad` and preserved real L/N/M ancestry. EventStore 4.2.0/AA and Builds 4.47.0/5ec remain the measured signed owner inputs. All five exact-main tuple jobs in run42711758062 and six catalog jobs in run42711757909 passed; normal source publisher42716608651 produced stable **v1.10.0**, whose Git tag directly names 6e43. The earlier successful callback with skipped publish remains a separate deferral. Normal landing used the documented ordinary REST merge under the existing explicitly named maintainer exception, with exact head SHA and merge ancestry; no admin/bypass request field, policy change or source-qualification override was used.

The final current inventory passes **562 executions/559 unique cases**, including the complete upstream 456, additional 97 and required 9 with exactly three named duplicate methods. Genuine same-code L349/N412 and all source/output guards pass. The 25 actual MSBuild/restore controls, cold plain Debug project/solution builds (zero warnings/errors, empty independent caches, only normal tool path), actual source SDK 4.2 identities and 48 source extension/companion cases pass. The full default Debug broad inventory remains 958=786 Pass/160 Fail/12 retained skips, with every identity/outcome/multiplicity exactly unchanged from both preceding default and explicit-source observations. Original 919/retained 955 attribution remains intact. The actual unchanged Story2.8 package fixture at frozen dca passes 1/1; its genuine M23 Contracts **source** refusal and older modified-input/SDK-guard observations remain separate.

The authorized normal fixture workflow42716926527 qualified version 1.10.0 at exact 6e43: current source/tag/gates, all 14 Node/30 Python controls, signed SDK identities, native project compilation and immutable package/native payload checks passed. The normal eligible production review was approved. Its real NuGet OIDC exchange then failed **HTTP401: policy 'Hexalith.Platform' expects 'release.yml', actual 'test-extension-release.yml'**. Push and signed-public verification were skipped; the official package URL returned404. Qualified/publication artifacts are byte-identical **unsigned** candidates (archive SHA256 d09ad59f348b429090dea3b790758348f1875a9746b745901d5f47b6f607703c; DLL 1b360ea49f77216e8fcb100f06e54221c7ae828c137fd1dd88e8d3f7e71533f4). They grant no fixture publication or native host admission.

Remaining external input is an authenticated NuGet account session or a separately configured dedicated policy for creator jpiquot, repository Hexalith/Hexalith.Platform, workflow test-extension-release.yml and environment production. Preserve the existing release.yml policy. GitHub controls already read back correctly; no alternative identity, workflow rename or unsigned substitution is permitted. User authorization remains effective and no renewed release consent is needed. The [exact resume handoff](evidence/3-2/release-adoption-20261109/qualified-source-and-fixture-policy-refusal-20261110/resume.md) records the conditional normal full-workflow rerun, source/tag/latest-gate checks, dedicated policy fields, raw evidence, identities and bounded next SDK/Platform work.

Signed fixture publication, an actual owning Builds-materialized fixture pin, authenticated finite producer/root/native-entry and expanded four-module reader/host admission, then real signed-package host/native startup remain incomplete. No runtime authority or pin is added by this evidence-only branch. Frozen intent, original baseline, starting 27a and all 23 preparation+19 broad evidence bodies remain unchanged. Concurrent main checkout/staging/dependency changes are preserved. **Status remains in-progress; no completion transition is taken.**

### Resumption workspace and current observations — 2026-11-15

The user invoked bmad-build 3.2 again. The previous temporary worktree no longer exists. Resume from the current clean Platform main `695463c5eed801e96dbd9efc6a8cef4b70e900f9` in the new isolated implementation worktree `/home/administrator/projects/hexalith/platform-story-3-2-20261115`, branch `build/story-3-2-resume-20261115`; this is the operative project root for this continuation. Preserve the original baseline and all historical evidence. The main checkout and its dependency checkouts remain outside the change.

Fresh read-only observations: remote main also names `695463c5eed801e96dbd9efc6a8cef4b70e900f9`; latest Platform release is v2.1.5, published 2026-11-15. Fixture workflow42716926527 still has only its original failed attempt and the official TestExtension flat-container index returns404. Current exact-main tuple43704364292, catalog43704364241 and source-release43704677909 are failed. Follow the handoff's advanced-main path; do not retry the old6e43 workflow or reuse its version/qualification. Current main's commit message attributes a default package-build failure to an accidentally selected legacy Builds4.44.3/net10 manifest; investigate the actual current inputs rather than accepting that attribution without evidence. The later spec-update-and-release-workspace.md says fixture Trusted Publishing is configured, but that statement is not a fresh account-side verification. A clarification about its present state is pending.

Continue the already approved release/adoption scope through normal exact-source gates, preserving concurrent upstream work and every admission check. Append new evidence under `evidence/3-2/release-adoption-20261109/resume-20261115/`. Verification jobs that create disposable workspaces must use `run-with-temp COMMAND [ARGUMENT ...]`, place temporary repositories, feeds and outputs beneath its TMPDIR, retain useful logs/results outside that directory, and wait for children before returning. This durable implementation worktree is retained for review.

### Reviewed owner correction and local preparation — 2026-11-15

The current-main tuple failure was reproduced before test execution: the selected historical profile no longer matched its sealed 412 controls. Five accidentally replaced metadata bodies were restored locally from measured Platform source `266f643fad6c679df4fac213802150257a19df62`; their Builds6.0.3 signed archive, producer/source identity and three-owner provenance were independently remeasured. This is a draft restoration for preparing the next atomic tuple, not a newly adopted release. The fresh plain Debug build then exposed real OpenAPI and EventStore.Server downgrades against the advanced source owners. No dependency constraint, runtime admission check or test assertion was relaxed.

The reviewed Builds correction is pushed as three source/audit commits in draft [PR #186](https://github.com/Hexalith/Hexalith.Builds/pull/186), head `eeb42f165ec8eafb85269fb0e3e5585120a7090c`, based on official main `73e60af8a1fcdf3f06726d5d1b6e1199cce778ee`. Current source graphs select the owning SDK5.1/OpenAPI11 view; finite SDK4.2 package consumers preserve their OpenAPI2/MCP2.0.1 view, including package owners in mixed mappings. Pure catalog readers explicitly evaluate their package-purpose snapshot. Verified model common props establish authenticated mode and owner-origin flags before the first pinned-catalog import; package snapshots, public ABI and override/tamper controls retain their existing semantics. The refreshed audit authenticates source `f49a2021d757a0fe8d7344d7baa67e721155afa6`, which must remain in the normal merge ancestry. Platform's reviewed local propagation correction also preserves the neutral Tooling edge after SDK transitive-reference promotion and respects explicit edge assignments and property-removal metadata.

All bounded local checks pass: 99 owning source/package/mixed model and snapshot cases, 352 actual catalog probes plus17 independent authority controls, four reader-mode controls, 43 source extension/registration/seed cases and28 real source-reference controls, with no skips. Plain Debug source and published-tool Release component builds report zero warnings/errors. All nine package component cases pass using the independently verified published Builds6.0.3 tool; this grants no new-owner or signed-fixture qualification. All127 tuple/history Python controls pass after committing the exact catalog source; the earlier real-checkout refusal against an uncommitted body is retained. Audit generation, strict audit validation and pinned commitlint pass. The 99-case temporary owner copy's recorded source diff exactly matches committed source f49a2021; its build and all child tests finished before cleanup. Earlier red assertions, nested source-build refusals, external-output fixture lookup failures and the first expired test envelope remain append-only evidence.

Normal hosted [owner CI43711614703](https://github.com/Hexalith/Hexalith.Builds/actions/runs/43711614703) has started; no owner merge or publication has occurred. Final local patches, exact commands, counts and remaining acceptance work are recorded in the [pre-release checkpoint](evidence/3-2/release-adoption-20261109/resume-20261115/final-local-owner-checkpoint.json). Required next work remains normal hosted owner qualification/release, independently measured atomic Platform tuple adoption, genuine current same-code historical replays and exact-source Platform qualification/release, dedicated fixture publisher verification and signed publication, then owning finite admission and actual signed native-host startup. Earlier broad-inventory attribution and frozen evidence remain unchanged. **Status remains in-progress.**

Parent review subsequently identified a CI-only assertion mismatch in the Tooling metadata control: a verified published DLL correctly removes the project edge. The control now asserts the exact real assembly binding in that mode and retains direct/promoted neutral project-edge assertions locally. All28 controls pass without skips in each mode against the independently verified signed6.0.3 tool, using graph restore and metadata evaluation without compiling source. The [binding-mode supplement](evidence/3-2/release-adoption-20261109/resume-20261115/propagation-binding-modes-summary.json) preserves both results and the updated patch; production bindings are unchanged.

The first owner PR qualification passed the catalog, both Python matrices, lint/API and Linux/Windows sample gates. Its unchanged30-minute .NET job limit cancelled Module.Tests without a failed assertion after a zero-warning/error build and191/191 Evidence.Tests. That incomplete execution earns no full-suite credit; one unchanged retry of the cancelled job is running. A local exact-source full diagnostic completed all3115 cases in887.902s:3110 passed, one failed because the diagnostic artifacts path omitted standard CLI assets required by the test's internal `--no-restore` build, and the same four standalone-checkout cases skipped as in the preceding official CI. The unchanged failed case passes1/1 after a normal-layout build in a fresh temporary owner copy. No assertion, production code, test selection or time limit changed. The [full diagnostic supplement](evidence/3-2/release-adoption-20261109/resume-20261115/owner-full-suite-diagnostic-summary.json) preserves that attribution and cleanup; hosted qualification remains required.

The unchanged hosted retry completed normally: all seven mandatory PR gates passed; Module.Tests reported3115 total/3111 passed/zero failed/four unchanged standalone skips, Evidence.Tests191/191, and integration13 total/one passed/12 existing opt-in skips. Skips confer no native acceptance. PR #186 was marked ready and normally merged with exact expected head as `e2bbbd0893f586e4d447ef8358ff747ea3d34435`. The [merge verification](evidence/3-2/release-adoption-20261109/resume-20261115/owner-merged-source-verification.json) confirms both parents, an identical reviewed tree, preserved audit-source ancestry and the exact pinned-commitlint-validated message; no admin/bypass request was used. Connected write operations lacked permission, so the ordinary maintainer CLI/API performed the documented ready and merge actions. Exact-main [CI43717004492](https://github.com/Hexalith/Hexalith.Builds/actions/runs/43717004492) is running before normal owner release. Platform pins remain draft preparation inputs.

### Measured Builds6.0.5 and local atomic adoption — 2026-11-15

All seven exact-main owner gates passed in43717004492. The existing normal release workflow43719900160 then passed source resolution, CodeQL, versioning, audited build, original-artifact authentication and publication, producing **Builds6.0.5** at exact e2bb. Official release446092346 and the direct Git tag agree. Original artifact14164446873 matches its published digest; both actual public NuGet archives pass `dotnet nuget verify --all`, and every original payload body remains identical. The published executable reports6.0.5+e2bb; Tooling, seed and embedded catalog identities are independently measured. Initial public-feed404 observations remain retained separately from the subsequent actual signed archive proof in the [publication receipt](evidence/3-2/release-adoption-20261109/resume-20261115/owner605-published-release.json) and [signed package measurements](evidence/3-2/release-adoption-20261109/resume-20261115/owner605-signed-package-measurement.json).

The uncommitted atomic Platform adoption binds the measured source, catalog, published tool and range floor. Its finite6.0.5 catalog-context entry preserves all earlier admissions, rejects unreleased reviewed/audit sources, neighbors and crossed source/range/catalog pairs, and independently checks named MCP2.0.1 versus generic MCP3 package pins. No new runtime SDK, MCP cohort or extension owner is admitted. All27 catalog controls pass; the tuple/history scripts pass130 cases, preserving the original127 and adding three catalog cases. The standalone catalog run repeats27 cases also executed by the SDK-suite script and is not counted as additional unique coverage.

Actual6.0.5 plain Debug source and default package Release builds report zero warnings/errors without SDK or family overrides. All43 source extension/registration/seed cases and all28 metadata controls in each local-project and actual published-assembly mode pass without skips. All nine required package component cases and three existing consumer-selection controls pass. An initial combined class/method filter selected zero cases; its XML is retained with zero credit, and the corrected separate runs assert exact9+3 totals. The [complete local adoption checkpoint](evidence/3-2/release-adoption-20261109/resume-20261115/platform605-local-review-checkpoint.json) records the source patch, raw commands, XML, counts and cleanup. Parent review, fresh immutable identical-code historical companions, full current/retained qualification and normal Platform landing/source release remain required. Signed fixture publication and actual native-host acceptance remain incomplete; **status remains in-progress**.

## Course correction — 2026-11-15

The [2026-11-15 sprint correction](../planning-artifacts/sprint-change-proposal-2026-11-15.md) retires the historical tuple-replay lanes through Story 2.13, and version-only pins follow in Story 2.14. Until Story 2.13 lands, the existing same-code 349/412 replay sources and L/M companions still gate tuple CI, so in-flight adoption work is unaffected. After it lands, the remaining Platform adoption needs only the current-tuple lane, with no companion commits or `qualification/*` branches. Correction finding: `/pushall` merged this story's `qualification/story-3-2-20261115-*` siblings into main, and origin/main `0c3f55d1` now carries historical pins. The last tree with current pins is `3bda4d79`. The user deferred the main repair, and Story 2.13 owns it if it is still needed. Status is unchanged.

### Current fixture publisher preparation — 2026-11-21

The isolated continuation starts at exact Platform main `25887836c2c7a63edbff7a4e0426e078a188b98c` after the current-tuple and version-only pin changes. Its catalog selects Builds 6.5.0 and EventStore 5.4.0. The fixture publisher now verifies the SDK from the exact Platform source's validated package provenance and owner lock, including the actual compiler and runtime files. The local package-mode fixture and native test project build with the four genuine signed EventStore SDK packages. The fixture itself is an unsigned local candidate. The [current checkpoint](evidence/3-2/release-adoption-20261109/resume-20261121/checkpoint.md) records the receipts and hosted gate observations.

The official fixture feed is still empty and the historical publisher was refused by NuGet OIDC. The dedicated Trusted Publishing policy could not be verified from this session. Signed publication, an owning Builds-materialized fixture pin, finite producer and composed-host admission, and signed package host/native startup remain open. **Status remains in-progress.**

The later authenticated NuGet readback confirmed the dedicated active policy and narrowed it to the exact fixture package. Current tuple replay exposed a Builds 6.5.0 catalog materialization failure after the historical test literals were corrected against the current signed lock. The exact owner source fix passed the independent published-tool reproduction and focused owner tests. The [owner runtime and current tuple checkpoint](evidence/3-2/release-adoption-20261109/resume-20261121/owner-runtime-and-tuple-checkpoint.md) records the current replay counts, normal owner release, and Platform approval prerequisite. **Status remains in-progress.**

### Signed fixture publication and component startup — 2026-11-21

Platform main `0aacf13244fa6b1d9e08d627149d72cf12229167` passed the exact hosted current tuple with 456 passing cases and the catalog gates. The normal source publisher created v2.2.5 at that commit. Fixture run `44902658431` passed qualification, the required environment review, the dedicated NuGet OIDC exchange, and signed public archive verification. The [publication and startup checkpoint](evidence/3-2/release-adoption-20261109/resume-20261121/signed-fixture-checkpoint.md) records the original and published artifact identities and the exact archive, assembly, declaration and native bodies.

The real Platform EventStoreHost built from the same commit with its generated package reference to the signed fixture. Its own process loaded the exact packaged DLL and reported `platform-test@1` Healthy in `eventstore-extensions` over loopback with a verified local Dapr 1.19.2 sidecar. The overall endpoint returned HTTP 503. The owning Builds release, finite Platform reader and host admission, full owned distributed MTP run and mandatory CI selection remain open. **Status remains in-progress.**

### Owning admission and the full gateway prerequisite — 2026-11-21

The finite Builds admission passed exact hosted PR CI and landed at `d532b1e9b1f595e2c9867bfa9780dccd78c9a2d9` with the reviewed tree and catalog audit intact. Main CI and the normal owner release run from that exact source. The pending Platform receiver retains the package cohort checks, authenticates the signed fixture and measures its loaded assembly in the gateway process. Its isolated verification passed 18 extension cases, 33 tuple controls and three source topology cases. [Owning admission checkpoint](evidence/3-2/release-adoption-20261109/resume-20261121/signed-fixture-checkpoint.md)

The unchanged released gateway reproduced `DependencyNotAvailable:EXT-PROTECTION-1:ExplicitProtectionInstallationRequired` with the signed fixture and pinned Dapr when no protection installation was supplied. The ordinary runner has no reviewed way to bind an authenticated disposable installation to this gateway. The component probe uses the existing test installation solely for component startup. Full native acceptance remains pending. [Measured prerequisite refusal](evidence/3-2/release-adoption-20261109/resume-20261121/fixture225-protection-prerequisite.json)

### Protected package fixture in the ordinary runner — 2026-11-21

Builds 6.6.0 published from the reviewed owning admission. Platform adopts its signed CLI, exact catalog and scoped test-extension pin together. The maintainer authorized fresh disposable protection provisioning for the owned package fixture. The ordinary Aspire graph creates a run-owned loopback OpenBao 2.6.2 instance, uses the shipped protection bootstrap, and gives the gateway an authenticated observer credential limited to the fixture tenant head and key metadata. The provisioner verifies those reads, denies custody mutations, decryption and key export, and confirms the bootstrap root was revoked. Story 3.2 verifies extension readiness. Custody-operation qualification uses its own enrolled authority and evidence.

The first protected run reached the EventStore 5.4.0 receiver and reproduced its rejection of the custody actor owned by Platform. With the permitted read-only observer role, the installed SDK retained its closed actor set. Run `89d092269a1452ecd067a39267eca2af` on `9edbf1956a13f10a1d14068014fd9668fb72bc36` loaded exactly the signed extension, preserved all six released workload image identities, reported all 21 resources Healthy and passed the ordinary native MTP readiness test, one of one. Cleanup reported an empty inventory of resources, processes, workspaces and claims. [Protected native receipt](evidence/3-2/release-adoption-20261109/resume-20261121/fixture225-protected-native.json) · [Owning Builds release](https://github.com/Hexalith/Hexalith.Builds/actions/runs/44909014255)

The protected follow-up isolates OpenBao from Docker bridge peers. It uses Docker's built-in `none` network, confirms the actual native sockets bind only to container loopback, and publishes no Docker ports. A run-owned host loopback socket forwards the pinned TLS connection through the local Docker Unix socket. The standard run label still identifies the only Docker resource for cleanup.

The prior candidate `794625df85332f90994931e941699872efc3fd27` passed the catalog gate and 465 hosted tuple executions, including 18 extension cases and the separate signed component startup. The protected candidate requires its own exact hosted qualification before landing. **Status remains in-progress.** [Catalog gate](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44913632623) · [Tuple gate](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44913632631)

### Reviewed acceptance and landing — 2026-11-21

The dedicated NuGet Trusted Publishing policy was narrowed in Windows Chrome to `Hexalith.Platform.TestExtension`, workflow `test-extension-release.yml`, and environment `production`. Its normal publication produced signed fixture 2.2.5. The hardened archive verifier independently rechecked its repository signature, NuGet registration hash and original qualified payload. [Fixture publication](https://github.com/Hexalith/Hexalith.Platform/actions/runs/44902658431) · [Signed fixture](https://www.nuget.org/packages/Hexalith.Platform.TestExtension/2.2.5) · [Owning Builds 6.6.0 release](https://github.com/Hexalith/Hexalith.Builds/actions/runs/44909014255)

Three independent reviewers examined the change. The patches refuse colliding structured extension keys, attest the composed project and published seed separately, and enforce the fixture archive layout on immutable publication retries. Three targeted controls cover the required protection digest, cross-run host observations and reader route-facet changes. No new work was deferred. The complete source extension class passed all 39 cases. Both broader package runs reported all 70 Composition classes. The reviewed revision added five passing cases, with the same 35 failures, 12 skips and one unselected opt-in case as the preceding revision. All 35 failure identities also occur in the original baseline. [Full inventory comparison](evidence/3-2/release-adoption-20261109/resume-20261121/platform-reviewed-full-inventory-comparison.json)

The normal signed Builds 6.6.0 run `4c0a576646c6ff0d32b6d68b39c527b4` at exact reviewed Platform `196f43100d1292fee7110b7861348facf24c2442` reached 21 Healthy resources, matched all six released owner images and passed the one native MTP readiness test. The package assets, dependency manifest and live gateway agree on the fixture DLL. The parent retained the exact gateway observation, and the selected closure independently identifies the composed project and published seed. Cleanup left no resources for this run. A first exact attempt timed out when the local Docker proxy refused the Memories graph connection. The normal `down` emptied its retained resources, and the unchanged retry above passed. [Verified native receipt](evidence/3-2/release-adoption-20261109/resume-20261121/fixture225-protected-native-reviewed-196f4310.json)

The exact reviewed PR passed both hosted checks and all 468 required xUnit executions with zero failures, skips or errors. The component started the signed host and reported `platform-test@1`. PR #55 merged as `37e6bcc95ae2a38ab9342ac2ee484ba0f686d294`; the merge retains the reviewed tree and both verified parents. [Platform PR #55](https://github.com/Hexalith/Hexalith.Platform/pull/55) · [Hosted qualification receipt](evidence/3-2/release-adoption-20261109/resume-20261121/hosted-platform-fixture-196f4310-receipt.json) · [Merge verification](evidence/3-2/release-adoption-20261109/resume-20261121/platform-pr55-merge-verification.json)

The main-branch catalog and tuple passed for that exact merge. The automatic publisher created Platform v2.4.0, and the official Git tag resolves to the verified merge. Story verification is complete. [Exact-main qualification](evidence/3-2/release-adoption-20261109/resume-20261121/hosted-platform-fixture-37e6bcc9-receipt.json) · [Source release verification](evidence/3-2/release-adoption-20261109/resume-20261121/platform-source-release-37e6bcc9-verification.json) · [Platform v2.4.0](https://github.com/Hexalith/Hexalith.Platform/releases/tag/v2.4.0)
