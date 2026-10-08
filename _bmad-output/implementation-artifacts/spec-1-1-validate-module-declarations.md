---
title: '1.1 Validate module declarations against the Platform declaration schema'
type: 'feature'
epic: 1
story: 1
created: '2026-10-07'
status: 'ready-for-dev'
route: 'dispatch'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Builds' v1 manifest cannot express the complete Platform declaration. Module developers need actionable validation before their declarations enter a composition.

**Approach:** Publish `hexalith.module-manifest.v2` in Builds and expose local enrollment validation through `hexalith-module validate`. Define all five declaration groups now; later stories add stage-specific rules within this major.

## Boundaries & Constraints

**Always:** Platform owns semantics; implementation belongs to `references/Hexalith.Builds`. Use strict camelCase JSON: root `schema` and nonempty `modules`; each module has `identity`, `runtime`, `surfaces`, `integration`, and `lifecycle`. Publish explicit nested properties and types, allowing empty collections for unused capabilities. Define:

- Identity: module, app/resource identities, enabled servers and dependencies.
- Runtime: logical Dapr roles, recovery classes and configuration bindings; extension packages/inputs; requests, limits, replicas defaulting to one, volumes.
- Surfaces: logical interfaces, route prefixes, exposure, surface class, authorization, callers and operations.
- Integration: topics/dead-letter policy, logical secrets/dynamic namespaces, identity needs, egress, provider tenancy, workers and disable controls.
- Lifecycle: readiness, scoped tasks/authority, recovery/fence hooks, reopening/replay behavior, startup override, critical flows/smoke surfaces, classification and recovery inventory.

Initially accept only v2: the previous-major window is bounded by the first Platform major, 2. Never count v1 as eligible enrollment. Validate all supplied files together for duplicate module, app and resource identities; on any error return diagnostics and no usable declaration set. Keep ordering deterministic and include source file, full field path and reason. Require usable readiness for required servers and positive finite startup overrides with nonblank justification. Support ten-minute default startup metadata.

**Never:** Change legacy v1 qualification behavior, run/down/test contracts, evidence fixture bytes or platform pins. Do not launch resources, compose environments, execute tasks, migrate module declarations, initialize nested submodules or publish packages. Preserve unrelated workspace edits, including story 4.3.

## I/O & Edge-Case Matrix

| Input/state | Output/behavior |
|---|---|
| Complete local v2 declarations | Validated set; CLI exits 0 |
| Unknown/missing fields, invalid types, duplicate keys or identities | File/field/reason diagnostics; no validated set; exit 1 |
| v1 or unsupported major | Explain required major and eligible list; exit 1 |
| Required server without readiness; invalid override | Local configuration failure naming offending field |
| Invalid scope or missing authority class | Reject task; allow per-start-verify, once-per-environment-creation, recovery, operator-only scopes |
| Dapr literal component name or missing role/recovery/binding | Reject; recovery class must be authoritative-restore, rebuild-only or live-authority-only |
| Enrolled host maps MCP endpoint | Reject under AD-11, regardless of exposure |

</frozen-after-approval>

## Code Map

Paths below are relative to `references/Hexalith.Builds`.

- `src/libraries/Hexalith.Builds.Tooling/Manifest/ModuleManifestLoader.cs`: v1-only loader; preserve it and its models. Runtime consumers depend on these contracts.
- `src/libraries/Hexalith.Builds.Tooling/Diagnostics/ToolDiagnostic.cs`: reuse Source/Field/Message, formatter and stable exit codes. Existing duplicate detection lacks complete paths.
- `src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs`: command registration; validation must bypass runtime execution.
- `src/libraries/Hexalith.Builds.Module.Cli/Hexalith.Builds.Module.Cli.csproj`: tool packaging currently omits schemas.
- Platform's `_bmad-output/planning-artifacts/architecture/architecture-platform-2026-09-27/ARCHITECTURE-SPINE.md`, Consistency Conventions and AD-9/AD-11: authoritative concepts. JSON spelling is an implementation decision.

## Tasks & Acceptance

**Execution:**

- [ ] `schemas/hexalith.module-manifest.v2.json` — define the complete strict Draft 2020-12 contract and defaults; avoid later structural additions.
- [ ] `src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs`, `PlatformManifestValidationResult.cs`, `SupportedPlatformManifestSchemas.cs` — implement bounded parsing, schema/semantic validation, current/previous eligibility and atomic multi-file results; reuse existing path/secret safeguards where applicable.
- [ ] `src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj` — embed v2 schema so validation works outside the source checkout.
- [ ] `src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs` — add `validate --manifest <file>` with repeatable manifests and `--output human|json`; use existing diagnostics and cancellation contracts.
- [ ] `src/libraries/Hexalith.Builds.Module.Cli/Hexalith.Builds.Module.Cli.csproj` — pack the schema under `tools/net10.0/any/schemas/`.
- [ ] `test/fixtures/module/platform/valid.json`, `test/Hexalith.Builds.Module.Tests/PlatformManifestValidationTests.cs`, `PlatformManifestCommandTests.cs` — cover the matrix, nested paths, multi-error aggregation, cross-file duplicates, schema/runtime parity and no lifecycle side effects.
- [ ] `Tools/test-g4-tool-package-contracts.ps1` — add packaged validate/schema probes alongside existing v1 contracts.
- [ ] `README.md` — document the v2 command, complete example, diagnostic fields and v1/enrollment boundary.

**Acceptance Criteria:**

- Given the fixture exercising every group, when schema and local enrollment validation run, then both accept it and omitted replicas mean one.
- Given each invalid matrix case, when validation runs in human and JSON modes, then errors identify source and full field path and expose no usable partial set.
- Given several invalid files, when validated together, then recoverable field errors aggregate deterministically, including duplicate identities across files.
- Given existing v1 fixtures, when the qualification tests run, then their behavior and retained bytes remain unchanged.
- Given the tool's packaged dependencies, when validation runs outside Builds, then it uses the shipped schema without fetching remote schemas or starting resources.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

Run from Builds:

- `dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1` — successful build with existing analyzer gates.
- `dotnet test --project test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug --no-build` — new validation and existing module suites pass. Use the documented direct xUnit assembly fallback if the runner blocks.
- Exercise package contract probes with locally packed artifacts; record their exact command and result. Package qualification does not publish or grant Platform tool acceptance (story 1.9).
