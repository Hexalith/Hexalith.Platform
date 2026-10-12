---
title: '1.6 Compose environments from declarations with one Platform Aspire model'
type: 'feature'
epic: 1
story: 6
created: '2026-10-12'
status: 'done'
baseline_commit: '977f2e01347226ac727b707aee4f140f5dc8b010'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Platform's `apphost.cs` has only the opt-in Works preview; Builds.Module.AppHost constructs a separate fixed topology. Neither composes a v2 declaration's minimum environment.

**Approach:** Put declaration selection and Aspire resources in one Platform C# library, invoked by the file AppHost and Builds runner. Compose enabled servers with declared readiness and task metadata.

## Boundaries & Constraints

**Always:** Validate the complete v2 set before adding resources; report source, field and reason on refusal. A domain module gets its servers plus EventStore, Tenants and Memories and their declared dependency closure; a technical module gets only its closure. With no active module, use the six-server reference composition. Source mode uses the single Story 1.3 mapping and Debug projects; package mode uses catalog-versioned released images and a Platform release snapshot of owner declarations, without initializing nested references. Missing source, snapshot, image or project fails by identity; no fallback. Run exactly one `eventstore` gateway, using the mapped EventStore server in source mode and Builds.Module.EventStoreHost as the package-mode seed, with no extension packages. Apply each server's declared `/ready` check and attach executable task metadata; current owner task lists are empty. Bind configuration by full key and server scope, including Story 1.5's logical token-to-runtime mappings. Share local Dapr components under existing literal names until 1.7. Keep Builds' signed plan, stop/readiness protocol and run-scoped UiHost. Align AppHost SDK, EventStore.Aspire and Dapr toolkit pins with the Builds catalog.

**Never:** Compose unrelated modules, optional EventStore admin/UI, legacy Memories MCP or a second gateway. Preserve owner AppHosts, v2 semantics, Works, Agents refusal and v1 runs. Defer EventStore extensions (Epic 3), Dapr name rebinding (1.7) and deadline policy (1.8).

**Decisions (2026-10-12):** Keep full source and package scope. Keep `apphost.cs` as a thin file entry with `#:project` to the Platform model; test the model directly and smoke-test the file entry. A file AppHost has no generated `Projects` type for `DistributedApplicationTestingBuilder`; do not claim otherwise. Retain Builds.Module.EventStoreHost as the package-mode composed-host seed, preserving its pack contract. It is never a second running `eventstore`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Domain | Domain fixture and unrelated module | Domain plus six owner servers; unrelated absent | No partial graph |
| Technical/default | EventStore, Memories, or no active module | Declared closure, or six-server reference default | No invented dependency |
| Invalid or unavailable | Duplicate/invalid declaration; missing binding, mapped project, release snapshot or image | No resources start | File/field or module/server/path and reason |
| Works | Flag false / true | Declaration model / unchanged Works preview, exclusively | Existing missing-sibling error preserved |
| Version drift | AppHost pin differs from catalog | Build/check refuses | Names package and both versions |

</frozen-after-approval>

## Code Map

- `apphost.cs`, `DaprSelfHostedMtls.cs`, `DaprComponents/` — Works graph; keep the file entry.
- `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs` — atomic v2 validator. `Workspace/SourceMapping.cs` and `Runtime/CompositionRunPlan.cs` carry origin and Platform path.
- `references/{Hexalith.EventStore,Hexalith.Tenants,Hexalith.Memories}/module/hexalith.module-manifest.v2.json` — six `/ready` servers, empty task lists; Tenants repeats input references.
- `references/Hexalith.EventStore/src/Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs` — gateway-only wiring accepts a pre-added project.
- `references/Hexalith.Builds/src/hosts/Hexalith.Builds.Module.AppHost/{AppHostRunner,RunTopology,RunReadiness}.cs` — runner and old graph. `Hexalith.Builds.Module.Cli.csproj` packs EventStoreHost/UiHost; `Props/Directory.Packages.props` pins versions.

## Tasks & Acceptance

**Execution:**
- [x] `src/Hexalith.Platform.Composition/{Hexalith.Platform.Composition.csproj,PlatformAppModel.cs,PlatformDeclarationSet.cs,PlatformServerBindings.cs,PlatformLocalProfile.cs,Declarations/{eventstore,tenants,memories,provenance}.json}`, `Hexalith.Platform.slnx` — select/validate declarations; resolve mapped projects or catalog images; add services, readiness and tasks. Store owner snapshots with source provenance for package mode; fail before resource creation on missing inputs.
- [x] `DaprComponents/reference/{statestore,pubsub,secretstore,llm,telemetry-state,telemetry-config,accesscontrol.eventstore,accesscontrol.tenants}.yaml` — supply shared local components from owner examples; preserve Works files.
- [x] `apphost.cs`, `eng/verify-agents-host.sh` — reference the library, route Agents/Works/declarations, and reject SDK or package pin drift.
- [x] `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`, `src/libraries/Hexalith.Builds.Tooling/Runtime/{ModuleCommandExecutionService,CompositionRunPlanFactory,CompositionRunPlan,CompositionEngine}.cs`, `src/hosts/Hexalith.Builds.Module.AppHost/{AppHostRunner,RunTopology,RunReadiness}.cs` — hand v2 selection/mapping to Platform; preserve v1, stop/readiness and UiHost; seed package-mode EventStoreHost.
- [x] `tests/Hexalith.Platform.Composition.Tests/PlatformAppModelTests.cs`, `references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/Workspace/WorkspaceAppHostTests.cs` — cover matrix, both modes, task metadata, pack contract and one gated live start.
- [x] `docs/platform-apphost-form.md` — record file form and testing-builder limitation.

**Acceptance Criteria:**
- Given a v2 plan, When Builds.Module.AppHost starts it, Then Platform owns the graph, one `eventstore` runs without extensions, and Builds retains run controls and UiHost scope.
- Given a composed server, When the graph is inspected, Then readiness and task metadata match its declaration.

## Implementation Notes

- The file `#:project` requires `IsAspireProjectResource=false` and `ReferenceOutputAssembly=true`; Platform's `Directory.Build.targets` supplies that library metadata without changing the file entry form.
- Source smoke exposed the Web SDK's automatically generated `GlobalPropertiesToRemove=;WebPublishProfileFile`. The Builds mapping guard permits only this harmless publish-profile removal; configuration and mixed property removals remain refused and have regression coverage. Tenants also supplies a source `AdditionalProperties=Version=...` assignment; only a single Version equal to the target central `PackageVersion` is permitted, with drift and mixed-configuration regression coverage.
- Memories' existing `CheckSubmodules` guard is supplied its `RequiredRootSubmodule.ResolvedRoot` from the one mapping, including the active Platform identity; no owner AppHost/build files or nested checkouts are changed.
- Package image preflight found `registry.hexalith.com/tenants-api:5.8.0` unavailable (`no such manifest`). The other five reference executable inputs are present; no image fallback is permitted.
- Live source verification required applying allocated listener ports and channel tokens to the actual generated Dapr CLI resources. The shared model owns this setup for both entry points; the declaration's Aspire `/ready` probe remains authoritative. Dapr application health does not probe `/ready`, which otherwise cycles with Memories' sidecar readiness check.
- A fresh local Redis store requires EventStore's existing writer-protocol operator activation. Its bounded implementation is shared in Builds.Tooling: the file model starts it concurrently before DCP startup, resolves the allocated runtime model endpoint, and signed Builds plans retain the existing runner operation. The paths are exclusive, and regression tests cover delayed endpoint allocation and refusal while another dependency is unhealthy. No deadline policy or owner AppHost changes were made.
- The explicit local profile supplies the owner's numeric/time and embedding-provider defaults. Tests bind the actual EventStore and prebuilt Memories option types, including the provider validation which rejects an empty Google API URL. Gateway settings are rendered in a composition-owned content root with registrations restricted to selected app IDs, preserving owner files.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Verified evidence |
|---|---|---|---|
| B1 | medium | patch | The v2 sidecars ignore the rendered per-run SQLite registry: the gateway/domain use ACL-only configs and others use null. Two permitted runner sessions reuse app IDs, so discovery can select a different run. |
| B2 | medium | patch | CompositionPortAllocator remembers ports only in its process. Child composition allocates again while persisted plan ports are unbound, so its selected ports can overlap the plan. |
| B3 | false | reject | The current enrolled set is EventStore, Tenants and Memories; all have registered executable and local-profile bindings. The unregistered Parties fixture supplies explicit bindings through the public model options. Refusing a declaration with no registered binding is the required missing-input behavior; actual Parties enrollment is Story 1.12. |
| B4 | false | reject | The v2 schema declares UI surfaces but carries no FrontComposer assembly/type markers, and none of the selected owner declarations has such markers. V1 marker resolution is unchanged; ComposePlatform preserves run-scoped UiHost when a plan supplies markers. No dropped existing v2 marker input was demonstrated. |
| B5 | high | patch | The owner statestore and existing Builds renderer specify keyPrefix:none for shared state. The new component omits it, partitioning regular keys by app ID and breaking cross-service reads despite healthy startup. |
| B6 | high | patch | The v2 helper owns a ready session outside the outer cleanup scope and passes cancellation to DownAsync, whose first action throws for a cancelled token. Cancellation after readiness can leave the session running. |
| B7 | high | patch | The v2 test branch discards DownAsync diagnostics and overwrites its result without RunId, hiding failed teardown and the identity required to retry cleanup. |
| B8 | medium | patch | The v2 down path returns HXR024 when a previously stopped run has no state. The existing public v1 down path explicitly returns successful idempotent cleanup in this case. |
| B9 | false | reject | PackagedHostProject throws on every partial packaged layout. Its false return identifies a source AppHost layout; that layout uses the same Builds.Module.EventStoreHost project with released package references, as the retained seed contract permits. It does not substitute the owner source EventStore server or initialize nested references. |
| B10 | medium | patch | The v2 branch skips invocation checks and its Modules list is empty, so simply removing the conditional would still probe nothing. Direct HTTP readiness does not verify the selected Dapr name-resolution/invocation boundary. |
| B11 | high | patch | Actual generated directories are 0755 and secrets.json files are 0644, confirmed with filesystem metadata. Clock/marker private keys are readable to other local users, and file-entry composition leaves its owned directory after shutdown. |
| B12 | high | patch | Package loading independently validates and then discards any supplied reference-only declaration. A valid conflicting owner document can be ignored instead of being checked against the authoritative release snapshot. |
| E1 | medium | patch | IsV2 calls JsonElement.TryGetProperty without an object-kind check. Valid array, scalar and null roots throw before the normal manifest loader can report file/field diagnostics. |
| E2 | high | patch | The private v2 session is invisible to the outer finally. Cancellation or a writer failure after readiness therefore cannot trigger the existing bounded cleanup of that session. |
| E3 | high | patch | The v2 test teardown result is discarded, dropping cleanup diagnostics and the run identity on a failure with remaining resources. |
| E4 | medium | patch | The public parser accepts --evidence, but the v2 early return neither forwards evidencePath nor calls the shared evidence writer. The requested document is silently absent. |
| E5 | medium | patch | Schema dispatch precedes recorded-gitlink resolution. When a direct-reference checkout changes from stale v1 to selected v2, the existing branch continues through ModuleManifestLoader and rejects the newly selected schema. |
| E6 | medium | patch | The v2 schema permits up to 256 modules, while plan creation calls Single. A validated multi-module document throws after model compilation instead of applying the no-active-module reference default. |
| E7 | medium | patch | For a valid two-server technical fixture without EventStore, Allocate(servers.Count-1) creates one sidecar slot and the second selected server indexes beyond it after resources are added. |
| E8 | medium | patch | Child-local port allocation does not exclude the signed plan ports; a selected unbound plan port can be reused by a secondary sidecar or vector endpoint. |
| E9 | medium | patch | The secrets path is interpolated into a double-quoted YAML value without escaping Windows backslashes, producing invalid or altered paths on that supported platform. |
| E10 | medium | patch | The validator allows enabled optional servers without readiness and multiple usable probes. Resolve calls Single before producing its supported-probe refusal, leaking an unscoped LINQ exception for those valid inputs. |
| E11 | medium | patch | The v2 readiness branch has no invocation probe even after all direct health checks pass. The existing readiness protocol explicitly checks that additional boundary for legacy modules. |
| E12 | false | reject | No enrolled non-reference domain currently supplies an executable/config binding through either entry point. Missing enrollment bindings must fail by identity. The library accepts explicit bindings for the domain fixture; Parties entry-point adoption remains Story 1.12. |
| V1 | medium | patch | Pre-verified gap: successful fake inspections and missing image mappings never execute the failed/timed-out registry-inspection rejection. Add atomic refusal coverage for both results. |
| V2 | medium | patch | Pre-verified gap: existing signed graph tests do not assert Tenants API full EventStore:Authentication keys. Removing those ComposeRun assignments leaves graph tests green while run bearer validation breaks. |
| V3 | high | patch | DownAsync rejects an already-cancelled caller token before teardown, and the v2 helper session is inaccessible to the outer cancellation/finally handlers. |

Shared-root groups: B2/E8 (plan-port overlap); B6/E2/V3 (v2 session ownership); B7/E3 (lost teardown outcome); B10/E11 (missing v2 invocation probe); B3/E12 (unregistered domain binding claim, rejected). Other findings remain separate. All surviving corrections use existing public contracts and demonstrated inputs; they route to patch.

**Patch resolution (2026-10-12):** All 23 findings routed to patch are corrected; the four rejected findings retain their individual refutations above.

| Findings | Correction and regression evidence |
|---|---|
| B1 | Signed sidecars retain the plan's SQLite name resolution, including merged EventStore/Tenants ACLs; `SignedCompositionKeepsRunDiscoveryPortsSharedStateAndApiCredentialsAsync`. |
| B2, E8 | Secondary allocation excludes every persisted plan port and retries collisions; `SecondaryPortAllocationRetriesPersistedCollisions`. |
| B5 | Shared statestore retains `keyPrefix: none`; signed graph test asserts the rendered component. |
| B6, E2, V3 | The v2 helper owns its session until successful run output and cleans up with an independent token; `SessionRemainsOwnedUntilSuccessfulRunOutputAsync` covers cancellation, output and evidence failures. |
| B7, E3 | Failed teardown diagnostics and RunId survive the test refusal; `TestRefusalRetainsFailedCleanupAndRunIdentityAsync`. |
| B8 | Repeated down succeeds while malformed/ambiguous identities still refuse; `DownIsIdempotentButRefusesMalformedOrAmbiguousIdentityAsync`. |
| B10, E11 | V2 invocation probes derive app IDs and readiness endpoints from the shared model annotations; the focused runner checks cover all selected non-gateway servers, with the real source runner smoke recorded below. |
| B11 | Generated workspaces and secret files are private; file-host disposal and failed composition delete only their owned directory; both workspace lifetime tests pass. |
| B12 | Supplied owner declarations must semantically match the package snapshot before deduplication; `PackageModeRefusesAConflictingSuppliedOwnerBeforeDeduplication`. |
| E1 | Non-object JSON roots reach the manifest diagnostic path; `NonObjectRootsReachManifestDiagnosticsAsync`. |
| E4 | V2 forwards the requested evidence path to the shared writer; command evidence and evidence-failure cleanup tests pass. |
| E5 | Recorded direct-reference resolution precedes schema dispatch and is reused; `MissingDirectV2ManifestIsRestoredBeforeDispatchAsync`. |
| E6 | Documents without one active declaration use the reference default; `MultiModuleDocumentsUseTheReferenceDefault`. |
| E7 | Sidecar slot count follows the selected non-gateway servers; `TwoServerTechnicalSelectionWithoutEventStoreHasDistinctSidecarsAsync`. |
| E9 | YAML quoting round-trips Windows secret paths; `SecretsYamlRoundTripsWindowsPaths`. |
| E10 | Unsupported readiness cardinalities refuse atomically with owner/server context; `UnsupportedOptionalReadinessIsAnAtomicScopedDiagnostic`. |
| V1 | Failed and timed-out registry probes refuse before adding resources and identify the image; `RegistryRefusalIsAtomicAndIdentifiesTheOwnerServerImage`. |
| V2 | The actual signed graph asserts Tenants API's complete EventStore authentication environment; signed graph regression above. |

## Verification

**Commands:**
- `dotnet build Hexalith.Platform.slnx -c Debug`; focused Platform and Builds AppHost tests — pass.
- `eng/verify-agents-host.sh --mode LocalScaffold`; `python3 scripts/test-reference-module-declarations.py` — pass.
- Gated Docker/Dapr smoke test — six reference servers reach declared `/ready`.

**Implementation evidence (2026-10-12):**
- `dotnet build Hexalith.Platform.slnx -c Debug -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` — passed, 0 warnings/errors (`/tmp/story-1-6-solution-final.log`).
- `dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -noLogo` — 21 total, 20 passed, 0 failed, 1 ordinary gated-smoke skip (`/tmp/story-1-6-platform-test-run.log`). The .NET 10 SDK's VSTest-target refusal is handled by the documented build/direct xUnit executable fallback.
- Builds Module.Tests Debug compilation passed with 0 warnings/errors (`/tmp/story-1-6-builds-tests-build.log`). Direct xUnit execution with `-class '*WorkspaceAppHostTests' -class '*CompositionRunPlanFactoryTests' -class '*PackagedHostProjectTests' -class '*CompositionEngineTests'` passed 36/36 (`/tmp/story-1-6-builds-focused-tests.log`); `-method '*RetainedProject*' -method '*OwnerSubmoduleGuard*'` passed 10/10 (`/tmp/story-1-6-mapper-tests.log`).
- `eng/verify-agents-host.sh --mode LocalScaffold` — passed, including canonical pin checks (`/tmp/story-1-6-file-entry.log`).
- `python3 scripts/test-reference-module-declarations.py` — passed the complete mutation/readiness/route/inventory/empty-task matrix (`/tmp/story-1-6-declaration-matrix.log`). All six mapped owner projects were also prebuilt serially in Debug without nested-reference initialization; build logs are `/tmp/story-1-6-source-prebuild.log`, `/tmp/story-1-6-source-prebuild-fresh.log`, and `/tmp/story-1-6-memories-prebuild.log`.
- `HEXALITH_PLATFORM_LIVE_SMOKE=1 dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -method '*ReferenceServersReachDeclaredReadinessInTheFileEntryAsync' -noLogo` — passed, 1 test, 0 failures/skips, 166.591s. All six declared server waits passed at the existing 120-second bounds; final shutdown passed. Log: `/tmp/story-1-6-live-final.log`.
- Earlier source-smoke failures are retained in `/tmp/story-1-6-live-test.log`, `/tmp/story-1-6-live-test-retry.log`, `/tmp/story-1-6-live-diagnostics.log`, `/tmp/story-1-6-live-fixed-profile.log`, `/tmp/story-1-6-live-shared-protocol.log`, `/tmp/story-1-6-live-concurrent-startup.log`, `/tmp/story-1-6-live-startup-order.log`, `/tmp/story-1-6-live-model-endpoint.log`, and `/tmp/story-1-6-live-owner-defaults.log`.
- Package live composition remains incomplete because the catalog's released Tenants API image is unavailable. Final `docker manifest inspect registry.hexalith.com/tenants-api:5.8.0` returned exit 1 and `no such manifest` (`/tmp/story-1-6-package-image.log`). Snapshot/catalog/seed graph preparation is covered by tests; it is not evidence of a successful package startup. Package startup is not qualified.

**Step-03 verification additions:**
- The image process boundary is internal to the model/test assembly. Production still runs the same bounded `docker manifest inspect`; tests can simulate successful lookup to compose and inspect the package graph without claiming released-image availability.
- `dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -m:1 -p:UseSharedCompilation=false -p:NuGetAudit=false -p:MinVerVersionOverride=1.0.0` — passed, 0 warnings/errors (`/tmp/story-1-6-audit-regression-build.log`).
- Direct xUnit execution with `-method '*PackageCompositionAddsCatalogContainers*' -method '*WorksPreviewComposesItsPreservedGraph*'` — passed 2/2, 0 failures/skips (`/tmp/story-1-6-audit-graph-tests.log`). Temporary Works sibling projects/components produce exactly the preserved four-project preview and exclude declaration servers. Package composition checks exact image probes and image versions, the sole seed, HTTP target ports, distinct Dapr listener ports, readiness, computed infrastructure values and scoped configuration/token annotations.
- `HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE=1 HEXALITH_DAPR_HOME=/tmp/story-1-6-builds-toolchain HEXALITH_PLATFORM_BUILDS_SMOKE_LOG=/tmp/story-1-6-builds-v2-apphost.log dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -class '*BuildsSourceStartupTests' -noLogo` — passed 1/1 in 54.058s (`/tmp/story-1-6-builds-v2-live.log`). This resolves the EventStore source mapping once and executes the real model build, runner preflight, signed startup/readiness and asserted teardown with the catalog's isolated Dapr CLI 1.18.0/runtime 1.18.2. Existing installed binaries and owner sources are unchanged.
- The six-server gated file smoke now asserts stop exit 0 and `TimedOut=false`; its rerun passed 1/1 in 161.561s (`/tmp/story-1-6-live-cleanup-asserted.log`). Readiness and stop bounds are unchanged.

**Parent verification after review patches (2026-10-12):**
- Root Debug solution build passed with 0 warnings/errors (`/tmp/story-1-6-parent-solution.log`). Builds Module.Tests Debug build passed with 0 warnings/errors (`/tmp/story-1-6-parent-builds-build-retry.log`). An overlapping validator build caused one file-lock failure; the sequential retry passed.
- Final Platform test-project Debug build passed with 0 warnings/errors (`/tmp/story-1-6-parent-platform-final-build.log`). Direct xUnit suite passed: 36 total, 33 passed, 0 failed, three ordinary live-test skips (`/tmp/story-1-6-parent-platform-final-tests.log`). The gated tests are run independently below.
- Six-server file-entry live smoke passed 1/1 in 137.779s, including asserted stop exit 0 and no timeout (`/tmp/story-1-6-parent-file-live.log`). The real Builds EventStore source smoke passed 1/1 in 50.159s, including exact selected project, source mapping, signed readiness and empty teardown/workspace (`/tmp/story-1-6-parent-builds-live.log`). Both use the existing startup and cleanup bounds.
- Real Builds Tenants domain smoke passed 1/1 in 107.753s (`/tmp/story-1-6-parent-tenants-live-final.log`). It asserts the exact six-server reference environment, validates the actual run-scoped Dapr invocation boundary before signed readiness, and asserts successful down with no remaining resources or run workspace. The EventStore and Tenants smokes use the isolated catalog-pinned Dapr CLI 1.18.0/runtime 1.18.2; installed tooling and all existing bounds remain unchanged.
- Expanded focused Builds suite, including public commands and v1 behavior, passed 120/120 (`/tmp/story-1-6-parent-builds-tests-rerun.log`). The unsupported-schema fixture now uses v3 because v2 dispatch is implemented; its original deterministic HXM001/HXM005/HXM010 assertions remain. Mapping guards passed 10/10 (`/tmp/story-1-6-parent-mapper-tests.log`).
- LocalScaffold file-entry build and pin checks passed (`/tmp/story-1-6-parent-file-entry.log`). The declaration mutation/readiness/route/inventory/task matrix passed (`/tmp/story-1-6-parent-declaration-matrix.log`).
- Released package image availability was rechecked: `docker manifest inspect registry.hexalith.com/tenants-api:5.8.0` returned exit 1 and `no such manifest` (`/tmp/story-1-6-parent-package-image.log`). Package graph/refusal tests pass; this remains an external limitation on live package verification, with no substitute image used.

**Matrix coverage:**
| Matrix row | Evidence |
|------------|----------|
| Domain | `DomainAddsReferenceOwnersAndExcludesUnrelatedModule`: seven servers, unrelated module absent, executable task metadata, missing binding refuses before resources. The real Builds Tenants domain smoke verifies all six reference servers and Dapr invocation. |
| Technical/default | `SelectsOnlyTheDeclaredDependencyClosure`: EventStore, Memories, Tenants and default selections; `GraphHasSixDeclaredServersAndNoOptionalOrLegacyHosts`; gated six-server file smoke. |
| Invalid or unavailable | Atomic duplicate, missing project/image/snapshot/source tests; full-key/token-scope test; reference-declaration mutation script. Actual registry preflight refuses the unavailable Tenants API release. |
| Works | Default gated file composition, exclusive file routing, `WorksPreviewPreservesItsMissingSiblingRefusal` and positive `WorksPreviewComposesItsPreservedGraphWithoutDeclarationServers` with temporary sibling/component fixtures; optional full Works deployment is outside this story. |
| Version drift | `eng/verify-platform-pins.py`, invoked by LocalScaffold; three isolated SDK/EventStore.Aspire/Dapr mutations each refuse and name observed/catalog versions (`/tmp/story-1-6-pin-drift.log`). |
