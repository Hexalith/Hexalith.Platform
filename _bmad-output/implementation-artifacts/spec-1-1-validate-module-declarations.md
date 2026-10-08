---
title: '1.1 Validate module declarations against the Platform declaration schema'
type: 'feature'
epic: 1
story: 1
created: '2026-10-07'
status: 'in-review'
baseline_commit: 'f043a2f242762233091abdaa5bbe1ab777bd0f12'
builds_baseline_commit: '520abb5898ad44b30c0744e707b53cd94741e6b1'
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

- [x] `schemas/hexalith.module-manifest.v2.json` — define the complete strict Draft 2020-12 contract and defaults; avoid later structural additions.
- [x] `src/libraries/Hexalith.Builds.Tooling/Manifest/PlatformManifestValidator.cs`, `PlatformManifestValidationResult.cs`, `SupportedPlatformManifestSchemas.cs` — implement bounded parsing, schema/semantic validation, current/previous eligibility and atomic multi-file results; reuse existing path/secret safeguards where applicable.
- [x] `src/libraries/Hexalith.Builds.Tooling/Hexalith.Builds.Tooling.csproj` — embed v2 schema so validation works outside the source checkout.
- [x] `src/libraries/Hexalith.Builds.Module.Cli/ModuleCommandApplication.cs` — add `validate --manifest <file>` with repeatable manifests and `--output human|json`; use existing diagnostics and cancellation contracts.
- [x] `src/libraries/Hexalith.Builds.Module.Cli/Hexalith.Builds.Module.Cli.csproj` — pack the schema under `tools/net10.0/any/schemas/`.
- [x] `test/fixtures/module/platform/valid.json`, `test/Hexalith.Builds.Module.Tests/PlatformManifestValidationTests.cs`, `PlatformManifestCommandTests.cs` — cover the matrix, nested paths, multi-error aggregation, cross-file duplicates, schema/runtime parity and no lifecycle side effects.
- [x] `Tools/test-g4-tool-package-contracts.ps1` — add packaged validate/schema probes alongside existing v1 contracts.
- [x] `README.md` — document the v2 command, complete example, diagnostic fields and v1/enrollment boundary.

**Acceptance Criteria:**

- Given the fixture exercising every group, when schema and local enrollment validation run, then both accept it and omitted replicas mean one.
- Given each invalid matrix case, when validation runs in human and JSON modes, then errors identify source and full field path and expose no usable partial set.
- Given several invalid files, when validated together, then recoverable field errors aggregate deterministically, including duplicate identities across files.
- Given existing v1 fixtures, when the qualification tests run, then their behavior and retained bytes remain unchanged.
- Given the tool's packaged dependencies, when validation runs outside Builds, then it uses the shipped schema without fetching remote schemas or starting resources.

## Implementation Notes

- Tooling embeds the shipped Draft 2020-12 schema; an isolated registry refuses remote fetches. The tool also packs the published schema.
- Atomic results carry immutable JSON declarations with replicas/startup defaults applied. Diagnostics aggregate source, full indexed field path and reason across files.
- The standalone command preserves cancellation/output contracts. Existing v1 loader, schema, runtime command parser, pins and retained fixtures are unchanged.
- Updated the package-gate test double for the new schema and validation probes; its original cleanup/failure assertions still pass.

## Spec Change Log

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH01 | medium | patch: server IDs | Two module-local api IDs with distinct app/resource IDs enroll and share readiness; the required-server check needs unique IDs. |
| BH02 | medium | defer: stage references | Resource/interface/readiness target resolution is absent. The frozen intent explicitly reserves stage-specific rules for later stories; composition binding belongs there. |
| BH03 | medium | defer: named collections | Task/interface/hook/operation names can collide; execution and catalog stages will need uniqueness. Current intent reserves their stage-specific rules. |
| BH04 | medium | defer: stage references | Hook references are retained without local resolution; the later recovery-hook contract owns which targets and authorities are admissible. |
| BH05 | maybe-false | defer: restore policy | The schema permits authoritative-restore without a local restoreHook. Whether a module-local hook is mandatory requires the future recovery contract; native/operator restore may supply it. |
| BH06 | medium | defer: stage references | Critical/smoke references are not resolved; the frozen intent reserves stage rules and story 5.3 explicitly owns critical-flow and smoke validation. |
| BH07 | maybe-false | defer: stage references | A secret reference is preserved without binding resolution. The current input lacks environment bindings, so local-only resolution is not established; later composition must settle it. |
| BH08 | medium | defer: stage references | Topic role resolution/capability matching is absent and belongs to the explicitly deferred Dapr binding/composition stage. |
| BH09 | medium | defer: allocation policy | Requests above limits pass the structural schema. The frozen intent reserves stage-specific scheduling rules; later composition must reject unprovisionable allocations. |
| BH10 | maybe-false | defer: quantity conversion | An exponent such as 1e999 passes a JSON number field, but this validator retains JsonElement and never converts resources to double. Downstream target types/bounds must be settled at composition. |
| BH11 | medium | defer: dependency graph | Self-dependencies pass identifier validation. Graph construction and cycle rules belong to the later composition stage explicitly reserved by the intent. |
| BH12 | medium | patch: readiness syntax | The built CLI accepts /health with an embedded newline as usable HTTP readiness; reject control characters in the v2 endpoint schema. |
| EH01 | high | patch: Unicode | Reproduced an escaped unpaired surrogate causing InvalidOperationException, empty CLI output and abandonment of a later invalid file. Return structured diagnostics and continue the set. |
| EH02 | medium | patch: anchored strings | Reproduced both moduleId and configurationKey trailing newline acceptance. Their format patterns must match the entire string. |
| EH03 | medium | patch: server IDs | Reproduced the same duplicate-server-ID readiness ambiguity as BH01; group under the identical root cause. |
| EH04 | medium | defer: stage references | The implementation preserves unresolved symbolic references; binding/flow/recovery target rules are explicitly reserved for later stages, as in BH02/BH04/BH06/BH08. |
| EH05 | maybe-false | defer: quantity conversion | The reported infinity requires downstream GetDouble, which this code never calls for resource quantities. Future composition must define representable target types and limits. |
| EH06 | medium | defer: allocation policy | Requests above limits are accepted structurally, as in BH09; resource scheduling validation is a reserved later-stage rule. |
| EH07 | low | patch: empty field path | Reproduced an unknown empty root key yielding field=""; provide an explicit nonblank escaped property path. |
| VG01 | medium | patch: enablement coverage | Pre-verified gap: removing required:true/enabled:false rejection would leave existing tests passing. Add assertions in both CLI formats. |
| VG02 | medium | patch: command readiness coverage | Pre-verified gap: removing readiness executable existence checks would leave tests passing. Cover a canonical nonexistent command probe in both formats. |

| R2-BH01 | medium | defer: stage references | carried from BH02: server references are preserved; the frozen intent reserves composition binding rules for later stories. |
| R2-BH02 | maybe-false | defer: stage references | carried from BH07: secret references require environment binding policy that this local declaration input does not supply. |
| R2-BH03 | medium | defer: stage references | carried from BH08: topic/Dapr role resolution and capability matching belong to the reserved Dapr composition stage. |
| R2-BH04 | medium | defer: stage references | carried from BH04: hook target resolution is unchanged and belongs to the future recovery contract. |
| R2-BH05 | medium | defer: stage references | carried from BH06: critical-flow and smoke references remain deferred to story 5.3. |
| R2-BH06 | medium | defer: named collections | carried from BH03: collection-name uniqueness beyond server IDs is reserved for execution and catalog stages. |
| R2-BH07 | maybe-false | defer: quantity conversion | carried from BH10: the validator retains JsonElement quantities; representable target types and conversion bounds remain a downstream policy question. |
| R2-BH08 | medium | defer: allocation policy | carried from BH09: requests-versus-limits checks belong to the reserved scheduling stage. |
| R2-BH09 | maybe-false | reject: reserved recovery policy | Empty inventory and unresolved restore-hook metadata are accepted, but the frozen intent permits empty collections and reserves recovery-stage rules. The future recovery contract must establish which volume inventory entries and local hooks are mandatory. |
| R2-BH10 | medium | patch: path controls | Reproduced CLI success for a route prefix with a newline and a mount path with NUL. Reject controls in both v2 path patterns, using the existing readiness restriction. |
| R2-BH11 | medium | patch: URI credentials | Reproduced a usable declaration containing HTTPS userinfo credentials. Extend secret detection privately within Platform validation so the legacy v1 detector and behavior remain unchanged. |
| R2-BH12 | medium | patch: property paths | Reproduced numeric keys yielding both object and array paths, and dotted keys yielding fictitious nesting. Resolve pointer segments using their actual container and escape unusual property names. |
| R2-EH01 | medium | defer: stage references | carried from BH02/EH04: undeclared server-reference checks are reserved for composition binding. |
| R2-EH02 | medium | defer: stage references | carried from BH04/EH04: hook-to-task resolution remains reserved for the recovery contract. |
| R2-EH03 | maybe-false | defer: quantity conversion | carried from BH10/EH05: CPU conversion can overflow or underflow downstream, but this validator never converts resource quantities to double. |
| R2-EH04 | medium | defer: allocation policy | carried from BH09/EH06: request/limit scheduling checks are explicitly reserved for later stages. |
| R2-EH05 | medium | patch: path controls | Confirmed the same route-control acceptance as R2-BH10; one schema correction addresses this shared defect. |
| R2-EH06 | medium | patch: property paths | Confirmed the same numeric and punctuation path ambiguity as R2-BH12; one path-formatting correction addresses this shared defect. |
| R2-VG01 | medium | patch: readiness coverage | Pre-verified gap: rejecting valid gRPC or command readiness leaves existing tests passing. Cover successful required-server enrollment and empty diagnostics in both CLI formats. |
| R2-VG02 | medium | patch: file-bound coverage | Pre-verified gap: increasing or removing the 256-file bound leaves existing tests passing. Cover acceptance at 256, atomic rejection at 257 and the CLI overflow diagnostic. |

## Verification

Run from Builds:

- `dotnet build test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1` — successful build with existing analyzer gates.
- `dotnet test --project test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug --no-build` — new validation and existing module suites pass. Use the documented direct xUnit assembly fallback if the runner blocks.
- Exercise package contract probes with locally packed artifacts; record their exact command and result. Package qualification does not publish or grant Platform tool acceptance (story 1.9).

### Initial execution evidence (2026-10-08, before review fixes)

Commands run from Builds. [Verification record](../../references/Hexalith.Builds/artifacts/story-1-1-validation/verification.json) retains exact commands, source hashes and log paths.

- Required Debug build: exit 0, zero warnings/errors.
- `dotnet test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -class '*PlatformManifest*' -result-xml artifacts/story-1-1-validation/platform-tests.xml`: 55 passed, zero failed/skipped/not-run. [Executed cases](../../references/Hexalith.Builds/artifacts/story-1-1-validation/platform-tests.xml).
- Required broad test command: 268 passed, one failed, exit 2. `SupportedPlatformPinsCatalogTests.CatalogDefaultsMatchSupportedPlatformPins` also fails at untouched HEAD: catalog EventStore 3.115.0 versus legacy runner 3.110.0; FrontComposer also differs (4.6.0 versus 4.5.0). Pins remain unchanged as required. The broad gate is not green. [Broad output](../../references/Hexalith.Builds/artifacts/story-1-1-validation/dotnet-test.log).
- `pwsh -NoProfile -File Tools/test-g4-tool-package-contract-gate.ps1`: passed.
- `pwsh -NoProfile -File Tools/test-g4-tool-package-contracts.ps1 -Version 0.0.0-story11.20261008.3 -PackageDirectory artifacts/story-1-1-validation/packages-0.0.0-story11.20261008.3 -SkipSourceValidation -RetainPackageDirectory`: passed before review fixes. Installed tools validate outside Builds; schema bytes, both output formats, duplicate identities, v1 rejection and unchanged input bytes pass. No publication eligibility or Platform acceptance is granted.

### Final execution evidence (2026-10-08, after review fixes)

The parent re-ran every required check after the implementation agent applied the review patches. [Final verification record](../../references/Hexalith.Builds/artifacts/story-1-1-validation/review-final-verification.json) retains exact commands, final source hashes, protected legacy paths, executed test methods and log hashes.

- Required Debug build: exit 0, zero warnings/errors. [Build output](../../references/Hexalith.Builds/artifacts/story-1-1-validation/review-final-build.log).
- Focused declaration validation and CLI tests: 85 passed, zero failed/skipped/not-run/errors. [Executed cases](../../references/Hexalith.Builds/artifacts/story-1-1-validation/review-final-tests.xml).
- Required full module suite: 298 passed, one failed, zero skipped, exit 2. The sole failure remains `SupportedPlatformPinsCatalogTests.CatalogDefaultsMatchSupportedPlatformPins`, caused by the unchanged baseline catalog/pin mismatch described above. [Full suite output](../../references/Hexalith.Builds/artifacts/story-1-1-validation/review-final-module-tests.log).
- Package-gate tests: exit 0; all 34 artifact-validator scenarios and the existing failure/cleanup checks passed. [Gate output](../../references/Hexalith.Builds/artifacts/story-1-1-validation/review-final-package-gate.log).
- Fresh local package probes: exit 0 using the recorded command with version `0.0.0-story11.20261008.4`, `-SkipSourceValidation` and `-RetainPackageDirectory`. The installed tools passed the shipped-schema, outside-checkout validation, both-format diagnostics, repeated-file duplicates, v1 rejection and unchanged-input probes. [Package output](../../references/Hexalith.Builds/artifacts/story-1-1-validation/review-final-package-contracts.log), [inventory](../../references/Hexalith.Builds/artifacts/story-1-1-validation/packages-0.0.0-story11.20261008.4/g4-tool-package-inventory.json). Publication eligibility and Platform acceptance remain false.
- Both repository diffs pass `git diff --check`. All 294 baseline-tracked schemas, fixtures, catalog, legacy loader, pins and pin-test files are unchanged.

Review patches cover invalid Unicode without aborting later files, complete string anchoring, duplicate server IDs, readiness endpoint controls, visible empty-property paths, required-disabled servers and missing command readiness. Every patched behavior has passing regression coverage; both CLI formats are exercised where applicable.

The full verification gate cannot pass within this story's frozen prohibition on pin changes. The bmad-build review step therefore halts with the spec `in-review` and sprint story `in-progress`; completion is not recorded. The baseline catalog/pin inconsistency requires a separately scoped resolution before the build workflow can complete.

### Matrix audit

All covering tests below ran and passed in the final focused XML:

| Matrix row | Covering tests |
| --- | --- |
| Complete v2 / defaults / exit 0 | `CompleteFixturePassesSchemaAndEnrollmentWithDefaults`, `DistinctValidFilesReturnAllModules`, `ValidationSucceedsWithoutExecutingLifecycleAsync` (human/json) |
| Unknown/missing fields, invalid types, duplicate keys/identities | `InvalidFieldsMatchSchemaAndReportCompletePaths`, `DuplicateKeysFailWithNestedLocation`, `CrossFileDuplicatesAndRecoverableErrorsAggregateDeterministically`, CLI matrix/repeated-manifest tests (human/json) |
| Unsupported/v1 major | `UnsupportedSchemaExplainsEligibility`, CLI matrix (human/json) |
| Readiness / finite justified override | `RequiredServerMustHaveUsableReadiness`, `NonFiniteStartupOverrideFailsLocally`, `PositiveFiniteStartupOverridesPass`, CLI matrix (human/json) |
| Task scope / authority | `SupportedLifecycleScopesPass`, invalid fields, CLI matrix (human/json) |
| Dapr role / recovery / binding | `SupportedRecoveryClassesPass`, invalid fields, CLI matrix (human/json) |
| Enrolled MCP endpoint | `EnrolledHostCannotMapMcpForAnyExposure`, `McpEndpointsFailUnderEveryExposureAsync` (all exposures, both formats) |

Parent diff audit confirms preservation of v1 schemas/loaders, existing qualification fixture bytes and pins. The confirmed baseline failure remains a verification limitation.

### Resumed review and final verification (2026-10-08)

The resumed three-layer review identified three local defects and two verification gaps. The v2 schema now rejects controls in route prefixes and mount paths; Platform validation rejects URI userinfo credentials without changing the shared legacy detector; diagnostic paths preserve numeric and punctuation-containing object keys. New tests cover both output formats, valid gRPC/command readiness without execution, and the 256-file acceptance/257-file atomic rejection boundary. All findings are recorded individually above; earlier deferred findings were carried without duplicate ledger entries.

[Resumed verification record](../../references/Hexalith.Builds/artifacts/story-1-1-validation/resumed-20261008/final-verification.json) retains exact commands, current revisions, final source/log hashes and all executed test methods. This record supersedes the earlier final evidence for the current patched source.

- Required Debug build: exit 0, zero warnings/errors.
- Focused declaration validation and CLI tests: 112 passed, zero failed/skipped/not-run/errors. [Executed cases](../../references/Hexalith.Builds/artifacts/story-1-1-validation/resumed-20261008/final-tests.xml).
- Package-gate tests: exit 0, all 34 artifact-validator scenarios and existing failure/cleanup assertions passed.
- Fresh local package contract probes: exit 0, version `0.0.0-story11.20261008.5`, with `-SkipSourceValidation -RetainPackageDirectory`; shipped-schema and outside-checkout validation probes passed. No publication eligibility or Platform acceptance is granted.
- Required complete module suite: 325 passed, one failed, zero skipped, exit 2. The sole failure remains `SupportedPlatformPinsCatalogTests.CatalogDefaultsMatchSupportedPlatformPins`. [Full suite output](../../references/Hexalith.Builds/artifacts/story-1-1-validation/resumed-20261008/final-module-tests.log).
- Both staged and unstaged diffs pass `git diff --check`. All 293 protected legacy paths other than the externally updated catalog remain unchanged from the recorded baseline.

During this run, external work advanced the Builds checkout and changed the catalog EventStore version from 3.115.0 to 3.117.0; the unrelated catalog/audit changes are preserved. The frozen legacy runner still pins EventStore 3.110.0 and FrontComposer 4.5.0, while the catalog pins FrontComposer 4.6.0. The failing consistency test and legacy pins are untouched.

The review step requires a halt when required verification cannot be fixed within scope. Story 1.1 remains `in-review`, and its sprint entry remains `in-progress`. A separate catalog/legacy-pin resolution is required before completion; the failure is neither skipped nor treated as a passing gate.
