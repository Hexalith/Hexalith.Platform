---
title: '1.6 Compose environments from declarations with one Platform Aspire model'
type: 'feature'
epic: 1
story: 6
created: '2026-10-12'
status: 'ready-for-dev'
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
- [ ] `src/Hexalith.Platform.Composition/{Hexalith.Platform.Composition.csproj,PlatformAppModel.cs,PlatformDeclarationSet.cs,PlatformServerBindings.cs,PlatformLocalProfile.cs,Declarations/{eventstore,tenants,memories,provenance}.json}`, `Hexalith.Platform.slnx` — select/validate declarations; resolve mapped projects or catalog images; add services, readiness and tasks. Store owner snapshots with source provenance for package mode; fail before resource creation on missing inputs.
- [ ] `DaprComponents/reference/{statestore,pubsub,secretstore,llm,telemetry-state,telemetry-config,accesscontrol.eventstore,accesscontrol.tenants}.yaml` — supply shared local components from owner examples; preserve Works files.
- [ ] `apphost.cs`, `eng/verify-agents-host.sh` — reference the library, route Agents/Works/declarations, and reject SDK or package pin drift.
- [ ] `references/Hexalith.Builds/src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`, `src/libraries/Hexalith.Builds.Tooling/Runtime/{ModuleCommandExecutionService,CompositionRunPlanFactory,CompositionRunPlan,CompositionEngine}.cs`, `src/hosts/Hexalith.Builds.Module.AppHost/{AppHostRunner,RunTopology,RunReadiness}.cs` — hand v2 selection/mapping to Platform; preserve v1, stop/readiness and UiHost; seed package-mode EventStoreHost.
- [ ] `tests/Hexalith.Platform.Composition.Tests/PlatformAppModelTests.cs`, `references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/Workspace/WorkspaceAppHostTests.cs` — cover matrix, both modes, task metadata, pack contract and one gated live start.
- [ ] `docs/platform-apphost-form.md` — record file form and testing-builder limitation.

**Acceptance Criteria:**
- Given a v2 plan, When Builds.Module.AppHost starts it, Then Platform owns the graph, one `eventstore` runs without extensions, and Builds retains run controls and UiHost scope.
- Given a composed server, When the graph is inspected, Then readiness and task metadata match its declaration.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

**Commands:**
- `dotnet build Hexalith.Platform.slnx -c Debug`; focused Platform and Builds AppHost tests — pass.
- `eng/verify-agents-host.sh --mode LocalScaffold`; `python3 scripts/test-reference-module-declarations.py` — pass.
- Gated Docker/Dapr smoke test — six reference servers reach declared `/ready`.
