---
title: '2.14 Close out version-only pins and retire residual SDK 4.2 artifacts'
type: 'refactor'
created: '2026-11-27'
status: 'done'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'e51cf194ac1caac65feb233c8c42491a7895518f'
story_key: '2-14-pin-builds-and-owner-modules-by-released-version'
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Builds 6.9.1, Parties 4.0.1, and Platform `v2.9.0` already retired the Builds `Sdk42CatalogView` selectors and moved locks and tuple CI onto the unified catalog, but Platform still holds dead SDK 4.2 catalog-view code, a 39-test duplicate `Sdk42` class, misleading `sdk42`/`current42`/`base42` references, stale current-state docs, and a superseded `in-progress` view-slice spec (`spec-2-14-retire-sdk42-catalog-views.md`), leaving Story 2.14 open.

**Approach:** Remove the dead SDK 4.2 code, obsolete fixture, duplicate test class, and stale operational docs; rename the tuple suites and profile files to `current`/`base`; align active tests and control scripts with the unified Builds catalog; verify Story 2.14's acceptance criteria on `HEAD`; and mark the stale view-slice spec superseded.

**Decisions (owner, 2026-11-27):**
- **Suite & profile naming:** Rename `--suite current42`/`base42` to `current`/`base` and `eng/tuple-ci-sdk42-{current,base}-suites.json` to `eng/tuple-ci-{current,base}-suites.json` across `.github/workflows/tuple-ci.yml`, `eng/run_tuple_ci.py`, Python tests, and current operational docs.
- **Story 3.2 exclusion:** Keep `src/Hexalith.Platform.Composition/PlatformFixturePackageQualification.cs` and `PlatformFixtureToolRelease.cs` untouched as a scoped Story 3.2 signed-fixture exclusion (alongside Story 1.17's `PlatformHostedReferenceMapping`).
- **Closure boundary:** Close Story 2.14 on verified Platform controls, local Release package suites, and recorded Builds 6.9.1 evidence (HXR021 and CS1061 resolved), leaving the `g4-attached-tests:<runId>` HXT006 runtime auth fix to Story 2.10.
- **Spec size:** Keep one full closeout spec (exceeds the 1,600-token guideline due to the Code Map file inventory).

## Boundaries & Constraints

**Always:**
- Preserve all Story 2.14 invariants: schema-2 `eng/builds-tuple.json` matching `.config/dotnet-tools.json` and `hexalith-platform.json`; `eng/builds-tool.lock.json` and `eng/owner-packages.lock.json` checks before run effects (carrying forward the 2026-11-19 owner decision that Platform-generated locks satisfy AC2); version-range Builds admission; no ordinary-run allowlists; and AR-12 expected/loaded identities.
- Keep every unique live test (`PlatformOwnerPackageLockTests`, the 2 live current-package entry tests), every live `eng/current_catalog_view.py` export (`PLATFORM_CONSUMERS`, `require_catalog_body`, `evaluate_views`, `catalog_cohort`, `verify_unified_catalog`), `eng/fixtures/builds-6.0.0-Directory.Packages.props`, `.csproj` `>= 4.2.0` API floors, `verify_sdk_gates.py`, negative test literals, and clearly labelled historical doc sections.
- Preserve `provenance.json` and `eng/current-tuple-source.json` checks; no push, no remote ops, and no edits inside `references/`.

**Never:**
- Drop unique test coverage or weaken zero-skip / exact required-count checks in `eng/run_tuple_ci.py`.
- Alter clearly labelled historical records in `README.md`, `docs/`, or `eng/tuple-source-preparation.md`.
- Edit `PlatformHostedReferenceMapping` (Story 1.17), Story 3.2's `PlatformFixturePackageQualification.cs`/`PlatformFixtureToolRelease.cs`, or pinned infrastructure image digests (`redis`, `falkordb`, `postgres`, `daprd`).
- Pull Story 2.10's `g4-attached-tests:<runId>` HXT006 runtime auth fix or live Docker/Dapr runs into this Story 2.14 closeout.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Unified catalog check | Authenticated Builds 6.9.1 gitlink catalog | `verify_unified_catalog` admits all consumers on one stable cohort (`EventStore 5.6.0`, `Memories 4.1.4`, `Tenants 8.1.1`, `Parties 4.0.1`, `McpCli 3.0.0`) | Fail closed on split or SHA mismatch |
| Pin alignment | `verify-platform-pins.py` with authenticated catalog + binding | Checks SHA-256 and `apphost.cs` directly against the unified catalog with no `Sdk42CatalogView` branch | Non-zero exit on drift |
| NU1109 downgrade control | `eng/test_tuple_collection_graph.py` + `eng/fixtures/builds-6.0.0-Directory.Packages.props` | Downgrade negative control executes and passes | Fails if fixture/control breaks |
| Tuple suite inventories | `eng/run_tuple_ci.py` + `eng/test_tuple_sdk_suites.py` | Every group passes with exact measured count and zero skips; no duplicate cohort runs twice | Fail closed on count drift or skip |
| Works host probe | `evaluate_views(raw, consumer_names=("Unqualified.Consumer",))` | Returns `Identity` and `Sdk` matching the unified catalog default | Fails if `evaluate_views` contract breaks |

</frozen-after-approval>

## Code Map

Aliases: `E = eng`, `T = tests/Hexalith.Platform.Composition.Tests`.

- `E/current_catalog_view.py` -- Delete dead SDK 4.2 verifiers (`require_finite_selector`:58-69, `verify_observations`:134-150, `verify_memories_observations`:153-160, `verify_tenants_observations`:163-171, `verify_mcp_observations`:174-182, `verify_current_owner_observations`:185-210, `generic_defaults`:213-229, `verify_named_platform_catalog`:232-259, `verify_extension_catalog_pin`:297-310) and `PlatformView`/`PartiesView` (:103-104). Keep :18-49, `require_catalog_body`, `evaluate_views`, `catalog_cohort`, `verify_unified_catalog` (used by `E/run_tuple_ci.py:15`, `E/test_tuple_collection_graph.py:16`, `tests/catalog-builder/verify_catalog_builder.py:341`, `E/verify_works_host.py:23,35,185`, `E/test_source_sdk_propagation.py:13`, `E/verify-platform-pins.py:9`).
- `E/test_current_catalog_view.py`, `E/fixtures/builds-before-unified-catalog.props` -- Delete `HistoricalCatalogViewTests` (:112-225), `CurrentCatalogViewTests` (:227-360), and the obsolete fixture (`.gitattributes:219`). Keep `builds6_catalog_body`, `gitlink_catalog`, `UnifiedGitlinkCatalogTests` (:96-109), and add focused `verify_unified_catalog`/`catalog_cohort` fault tests.
- `E/verify-platform-pins.py:9,30-33`, `E/test_verify_platform_pins.py:76-121` -- Remove dead `HexalithPlatformSdk42CatalogView` branch and synthetic SDK 4.2 tests; keep SHA-256 and unified pin checks.
- `E/run_tuple_ci.py:21,24,56,233-236,365`, `.github/workflows/tuple-ci.yml:281-283,331-333`, `E/tuple-ci-sdk42-{current,base}-suites.json` → `E/tuple-ci-{current,base}-suites.json`, `E/test_tuple_{sdk_suites,runtime_context,collectors,collection_graph}.py`, `E/test_builds_{tuple,publication}.py` -- Remove dead local `sdk_suite`/`sdk42_collecting_cohort`/`verify_extension_catalog_pin` tests from `E/test_tuple_sdk_suites.py`, update base count (`67 → 28` after removing the duplicate class), rename suites `current42`/`base42` → `current`/`base` and profile files across all synced callers, and fix `E/test_tuple_collection_graph.py:1,30` labels.
- `T/PlatformSdk42EventStorePackageTestCohortTests.cs` -- Delete exact 39-test duplicate of `T/PlatformEventStorePackageTestCohortTests.cs`.
- `T/PlatformSdk42PackageEntryTests.cs`, `T/PlatformPackageCatalogReaderFixture.cs:18-20`, `T/PlatformPartiesPackageFixture.cs:341` -- Rename the 2 unique live entry tests to `T/PlatformCurrentPackageEntryTests.cs`, drop unused `sdk42` flag, rename `hexalith-sdk42-reader-diagnostics`.
- `T/PlatformQualificationPackageFixture.cs:22-23,119,121`, `T/PlatformProductionModelClosureTests.cs:65-68,97-105`, `.gitattributes:116-118,219` -- Remove `ExtensionSdkTest()`, `"eventstore/4.2.0"` overrides, dead `tenantsVersion == "7.0.7"` branch, and stale `.gitattributes` lines.
- `docs/source-releases.md:12-13,31-33`, `README.md:27`, `docs/current-tuple-preparation.md:20`, `docs/domain-module-enrollment.md:20`, `E/tuple-source-preparation.md:28,38`, `_bmad-output/implementation-artifacts/spec-2-14-retire-sdk42-catalog-views.md` -- Update current operational sections only (keep historical sections) and mark the 2026-11-20 view-slice spec superseded.

## Tasks & Acceptance

**Execution:**
- [x] `E/current_catalog_view.py`, `E/test_current_catalog_view.py`, `E/fixtures/builds-before-unified-catalog.props`, `E/verify-platform-pins.py`, `E/test_verify_platform_pins.py` -- remove dead SDK 4.2 catalog-view code, fixture, and pin branch; cover `verify_unified_catalog`/`catalog_cohort` fault cases -- keeps active callers green without dead SDK 4.2 paths.
- [x] `T/PlatformSdk42EventStorePackageTestCohortTests.cs`, `T/PlatformSdk42PackageEntryTests.cs`, `T/PlatformPackageCatalogReaderFixture.cs`, `T/PlatformPartiesPackageFixture.cs`, `T/PlatformQualificationPackageFixture.cs`, `T/PlatformProductionModelClosureTests.cs`, `.gitattributes` -- delete the 39-test duplicate class, rename the 2 unique live entry tests to `PlatformCurrentPackageEntryTests.cs`, and strip obsolete `sdk42`/`4.2.0`/`7.0.7` helpers and stale attributes -- eliminates duplicate runs and misleading names while preserving all unique coverage.
- [x] `E/run_tuple_ci.py`, `.github/workflows/tuple-ci.yml`, `E/tuple-ci-sdk42-{current,base}-suites.json` → `E/tuple-ci-{current,base}-suites.json`, `E/test_tuple_{sdk_suites,runtime_context,collectors,collection_graph}.py`, `E/test_builds_{tuple,publication}.py` -- drop dead SDK 4.2 test helpers, rename suites `current42`/`base42` → `current`/`base` and profile files across all synced callers, update base count (`67 → 28`), and clean `test_tuple_collection_graph.py` labels -- aligns CI profiles and controls with the unified catalog.
- [x] `docs/source-releases.md`, `README.md`, `docs/current-tuple-preparation.md`, `docs/domain-module-enrollment.md`, `E/tuple-source-preparation.md`, `_bmad-output/implementation-artifacts/spec-2-14-retire-sdk42-catalog-views.md` -- update current operational docs for `current`/`base`/`extensions` suites and unified counts (430/28/27) and mark the stale view-slice spec superseded -- keeps docs and tracking accurate.
- [x] Run all Verification commands locally and record compact results in `## Implementation Notes`.

**Acceptance Criteria:**
- Given `eng/`, `.github/workflows/tuple-ci.yml`, and `tests/Hexalith.Platform.Composition.Tests/`, when scanned for dead SDK 4.2 view code or duplicate `PlatformSdk42` test classes, then none remain while genuine historical fixtures and `.csproj` `>= 4.2.0` floors remain intact.
- Given current `HEAD` pins (`Builds 6.9.1`, unified owner cohort), when all Python/Node controls, lock/pin checks, `run_tuple_ci.py --audit-only`, and the three Release package-mode suite profiles run locally, then every check passes with exact measured totals and zero skips.
- Given Story 2.14's six acceptance criteria and settled owner decisions, when audited on the final tree, then every in-scope criterion holds and `spec-2-14-retire-sdk42-catalog-views.md` is no longer `in-progress`.

### Review Findings

BMAD review of `e51cf194..03aff5d3` on 2026-11-28, including Builds 6.9.2 and Parties 4.0.2.

- [ ] [Review][Patch] Validate the effective EventStore catalog default [eng/verify-platform-pins.py:34]. An authenticated body with the only declaration inside an MSBuild target passes the verifier even though the evaluated property is unset. An inactive parent group, a space inside the condition's quoted value, and a differently cased override produce related false positives. Cover these cases with regression checks.
- [ ] [Review][Patch] Verify all three tuple invocations and their evidence paths [.github/workflows/tuple-ci.yml:282]. All 85 source-release tests still pass after removing the `base` invocation and upload path, which would omit 28 required executions and their receipt.
- [ ] [Review][Patch] Exercise the current suite's control dispatch before restore [eng/run_tuple_ci.py:379]. Normal-path dispatch can be removed with all 58 related Python tests still passing. Assert that both controls execute and that a failing control stops the runner before restore.
- [ ] [Review][Patch] Make the ambient SDK override control detect loss of environment isolation [eng/test_current_catalog_view.py:197]. Passing the unrestricted process environment to MSBuild still passes the test. The synthetic catalog assigns the SDK property unconditionally. Use a conditional catalog default and assert that the ambient SDK value would change the result without isolation.
- [ ] [Review][Patch] Run each offline catalog control once in combined verification [eng/test_tuple_sdk_suites.py:8]. Importing the six-case `TestCase` causes the documented combined invocation to discover it in both modules. Schedule the class explicitly in the workflow and keep its local discovery in its own module.
- [x] [Review][Defer] Complete exact Release verification for Builds 6.9.2 and Parties 4.0.2 [eng/builds-tuple.json:3]. The full 485-execution result covers Builds 6.9.1 and Parties 4.0.1. The later tuple needs a receipt with all 430 current, 28 base, and 27 extension executions and zero skips. The prior run timed out on seven current cases.
- [x] [Review][Defer] Reconcile the three cases shared by `current` and `extensions` [eng/tuple-ci-current-suites.json:294]. The two profiles ran the same methods against the same collector before the rename. Reconcile the overlap with the approved suite counts when changing the inventories.
- [x] [Review][Defer] Verify the effective Parties package pins in the catalog audit [eng/current_catalog_view.py:131]. The existing audit tests the Parties default property. A consumer-specific package override with the default still intact can pass the graph audit. The full tuple path checks the restored Parties package versions separately.
- [x] [Review][Defer] Reconcile the generated Epic 2 context [_bmad-output/implementation-artifacts/epic-2-context.md:32]. Restore the explicit attachment hold fields and distinguish the required host-evidence job, generated package-lock checks, and the scoped NuGet locked-mode restore. This extends the existing agent-context follow-up.
- [x] [Review][Defer] Clarify owner-release provenance updates under AC5 [eng/current_tuple_source.py:66]. The Builds and Parties bump demonstrates a pin and lock update. EventStore, Tenants, and Memories also have existing version-bound declaration provenance and source binding requirements.

At `03aff5d3`, the review passed the pin and committed owner-source checks, 212 Python test executions, 85 source-release controls, 14 extension gate controls, and 37 extension-release controls. All three local graph audits resolved EventStore 5.6.0 across 33 projects. The cached Builds 6.9.2 and both changed Parties 4.0.2 archives matched their recorded lock hashes and passed NuGet signature verification in offline mode. Release builds of the Composition and AttemptStore test projects succeeded. Direct xUnit runs passed 28/28 `base`, 27/27 `extensions`, and 423/430 `current` with zero skips across completed groups. The local run timed out in `PlatformCurrentOwnerPackageTests` (2), `PlatformPartiesCatalogHandoffTests` (2), and `PlatformCurrentPackageArtifactTests` (3). The passed totals include reruns after correcting loopback proxy configuration and the published tool archive layout. The full 485-execution result below covers the earlier Builds 6.9.1 tuple.

The follow-up review of the same range independently passed 86 Python tests, 85 source-release tests, the pin check, and the source and declaration identity checks for EventStore, Parties, Tenants, and Memories. It reproduced the EventStore pin false positive with real MSBuild, the suite and dispatch coverage gaps, and the duplicate discovery of all six offline catalog tests. The affected workflow and engineering files match `76535674`. The follow-up used the recorded Release results above.

#### Rejected

- Low. The verification command needs `PYTHONPATH=eng` when run from the repository root. Its correction is in the spec under review, which the review leaves as authored. The review ran the controls with that setting.
- Low. The pre-setup MSBuild call and exact consumer inventory both predate the closeout. The current pre-dependency tests passed with SDK 10.0.302. No new failure was reproduced from their placement or inventory rule.
- False. The separately committed 6.9.2 adoption brings in the upstream attached-auth change. The closeout commit contains no runtime authentication change, and its verification grants no Story 2.10 lifecycle qualification.

## Implementation Notes

- **Catalog & pin cleanup:** Removed dead SDK 4.2 catalog-view verifiers and `PlatformView`/`PartiesView` from `eng/current_catalog_view.py` (retaining `PLATFORM_CONSUMERS`, `require_catalog_body`, `evaluate_views`, `catalog_cohort`, and `verify_unified_catalog`), deleted `eng/fixtures/builds-before-unified-catalog.props` and its `.gitattributes` entry, replaced deleted SDK 4.2 test classes in `eng/test_current_catalog_view.py` with `UnifiedCatalogFaultTests` alongside `UnifiedGitlinkCatalogTests`, and removed the dead `HexalithPlatformSdk42CatalogView` branch and synthetic SDK 4.2 tests from `eng/verify-platform-pins.py` / `eng/test_verify_platform_pins.py`.
- **C# test & attribute cleanup:** Deleted the 39-test duplicate `PlatformSdk42EventStorePackageTestCohortTests.cs`, renamed `PlatformSdk42PackageEntryTests.cs` → `PlatformCurrentPackageEntryTests.cs` (retaining both unique live entry tests and the `#if HEXALITH_PLATFORM_EXTENSION_SDK && !HEXALITH_PLATFORM_SOURCE_CATALOG` guard), dropped the unused `sdk42` flag in `PlatformPackageCatalogReaderFixture.cs`, renamed `hexalith-sdk42-reader-diagnostics` → `hexalith-current-reader-diagnostics` in `PlatformPartiesPackageFixture.cs`, removed `ExtensionSdkTest()` and `"eventstore/4.2.0"` overrides from `PlatformQualificationPackageFixture.cs`, removed the dead `tenantsVersion == "7.0.7"` branch from `PlatformProductionModelClosureTests.cs`, and synced `.gitattributes`.
- **Tuple CI suites & controls:** Renamed `eng/tuple-ci-sdk42-{current,base}-suites.json` → `eng/tuple-ci-{current,base}-suites.json` and `--suite current42`/`base42` → `current`/`base` across `eng/run_tuple_ci.py`, `.github/workflows/tuple-ci.yml` (including `artifacts/tuple-ci-base`), `eng/test_tuple_{sdk_suites,runtime_context,collectors,collection_graph}.py`, and `eng/test_builds_{tuple,publication}.py`; updated `base` required total `67 → 28` (`26 + 2`); dropped dead local `sdk_suite`/`sdk42_collecting_cohort`/`verify_extension_catalog_pin` tests from `eng/test_tuple_sdk_suites.py` (importing `UnifiedCatalogFaultTests` for pre-submodule CI coverage).
- **Docs & superseded spec:** Updated current operational sections in `docs/source-releases.md` (`430 current, 28 base, 27 extension: 485 executions`, unified catalog/lock wording), `README.md`, `docs/current-tuple-preparation.md`, `docs/domain-module-enrollment.md`, and `eng/tuple-source-preparation.md` without modifying historical sections; marked `_bmad-output/implementation-artifacts/spec-2-14-retire-sdk42-catalog-views.md` `status: 'superseded'`.
- **Verification results (2026-11-28, Builds `6.9.1`, gitlink `91cb7cba990ffdf82930f9bf8cb68aef41baba76`, unified cohort `EventStore 5.6.0`, `Memories 4.1.4`, `Tenants 8.1.1`, `Parties 4.0.1`, `McpCli 3.0.0`):**
  - Python unittest controls (11 modules with `PYTHONPATH=eng`): `Ran 207 tests — OK` (0 skips, 0 failures).
  - Pin & lock checks (`eng/builds_tuple.py`, `eng/owner_packages_lock.py --check`, `eng/verify-platform-pins.py`): all passed.
  - Node & extension-release controls: `npm run test:source-release` (`85/85`), `eng/test-extension-release/gate.test.mjs` (`14/14`), `eng/test-extension-release/run-controls.py` (`37/37`).
  - `eng/test_tuple_collection_graph.py`: passed (`1/1`), confirming both the `builds-6.0.0-Directory.Packages.props` NU1109 downgrade negative control and locked unified cohort restore/build.
  - `eng/run_tuple_ci.py --audit-only` against verified Builds 6.9.1 tool artifacts for all 3 suites (`current`, `base`, `extensions`): all `graph-verified` (`eventStoreSdk=5.6.0`, 33 projects).
  - Release package-mode build (`0 Error(s)`) and direct xUnit execution of all 3 suite profiles in a clean CI-equivalent local clone (all direct submodules initialized non-recursively, zero edits in `references/`): `current` `430/430`, `base` `28/28`, `extensions` `27/27` (`485/485` total, 0 failures, 0 skips). Note: in the parent checkout itself, 18 `PlatformPartiesPackageFixture.CreateFromCandidateAsync` tests fail on pre-existing local nested submodule `references/Hexalith.ChatBot/references/Hexalith.Builds` (`WorkspaceMappingException`); all other 467/467 tests also passed directly in the parent checkout.
  - `git diff --check`: clean (0 whitespace issues); residual SDK 4.2 scan confirmed zero remaining dead SDK 4.2 code or duplicate `PlatformSdk42` test classes (only historical `eng/fixtures/builds-6.0.0-Directory.Packages.props`, historical doc sections, and negative test literals remain).
- **Post-review patches & rerun verification (2026-11-28):** Applied all 7 review patches (suite-control helper & tests in `eng/run_tuple_ci.py`/`eng/test_builds_tuple.py`; stray word removal in `eng/tuple-source-preparation.md`; authenticate-before-parse & single unconditioned `HexalithEventStoreVersion` check in `eng/verify-platform-pins.py`; removed dead `sdk_version`/`OPENAPI_PACKAGES` in `eng/current_catalog_view.py` and added offline real-MSBuild `verify_unified_catalog` test; added retired-file and disjoint-selector guards in `eng/test_tuple_collectors.py`/`eng/test_tuple_sdk_suites.py`; cleaned stale comments/synthetic `4.2.0` literals) and deferred 1 agent-context entry (`epic-2-context.md`) to `deferred-work.md`. Reran full verification: Python controls `212/212`, pins/locks pass, Node/ext `85/14/37`, collection-graph `1/1`, `--audit-only` `3/3` (`graph-verified 5.6.0 33`), Release build `0 Error(s)`, `PlatformProductionModelClosureTests` `1/1`, `git diff --check` clean.

## Spec Change Log

## Review Triage Log

Iteration 0 (2026-11-28). Layers: blind-hunter (14), verification-gap (1), edge-case-hunter (0).

| ID | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| VGap-1 | medium | patch | Pre-verified gap: all callers of `run_tuple_ci.main()` pass `--audit-only`, returning at `eng/run_tuple_ci.py:363` before the renamed `if args.suite == "current":` guard at line 365; a typo silently skips `historical-security-package-controls` and `UnifiedGitlinkCatalogTests`. |
| Blind-1 | low | defer | Step 1 regenerated `epic-2-context.md` from `epics.md` (whose line 1724 still carries the 2026-11-19 audit follow-up); agent-context file edits route to `defer` per Step 4. |
| Blind-2 | low | patch | `eng/tuple-source-preparation.md:29-30` leaves a stray `"Explicit"` at the end of line 29 before `The \`--suite\` argument` inside the edited paragraph. |
| Blind-3 | medium | patch | `eng/verify-platform-pins.py:25-31` parses XML before `require_catalog_body` and, after removing the `Sdk42CatalogView` branch, takes the last `HexalithEventStoreVersion` in XML order without checking `Condition` attributes or single-entry uniqueness. |
| Blind-4 | medium | patch | `eng/test_current_catalog_view.py` `UnifiedCatalogFaultTests` only calls `verify_unified_catalog` with mocked `evaluate_views`, leaving pre-submodule CI without an offline real-MSBuild `verify_unified_catalog` check. |
| Blind-5 | low | patch | `eng/current_catalog_view.py:54,96-97` leaves `sdk_version` on `evaluate_views` with zero callers, and `:40,70-71` collects `OPENAPI_PACKAGES` only for `verify_unified_catalog:140` to discard them. |
| Blind-6 | low | reject | Pre-existing `verify_unified_catalog` family selection untouched by this diff; broadening `Memories`/`McpCli` to prefix filters or adding Parties package checks adds new validation rules beyond a direct correction. |
| Blind-7 | low | reject | Pre-existing untouched `catalog_cohort` function; Builds catalogs use empty or `'$(Key)' == ''` conditions, `verify_unified_catalog` already catches per-consumer conditions via real MSBuild, and adding a guard is more than a direct correction. |
| Blind-8 | low | patch | Adding guards in `eng/test_tuple_collectors.py` and `eng/test_tuple_sdk_suites.py` for retired `builds-before-unified-catalog.props`/`PlatformSdk42*` files, `current42`/`base42` absence, and `current`/`base` selector disjointness directly protects this diff's invariants. |
| Blind-9 | false | reject | The deleted block in `PlatformProductionModelClosureTests.cs:94-105` was gated by `if (tenantsVersion == "7.0.7")`, which has been unreachable dead code on Tenants 8.x; removing it dropped zero executed assertions. |
| Blind-10 | low | patch | Files directly touched by this diff retain stale SDK 4.2/named-view comments (`PlatformProductionModelClosureTests.cs:45`, `.gitattributes:105`) and mismatched `4.2.0` vs mocked `5.4.0` synthetic catalogs (`eng/test_builds_tuple.py:186`, `eng/test_builds_publication.py:192`). |
| Blind-11 | low | reject | `PlatformPackageCatalogReaderFixture` `currentTest = false` / `net10.0` negative fixture and `PlatformQualificationPackageFixture.SelectedSdkTest()` (7 active callers in 3 untouched test files) are pre-existing; removing them adds churn with no functional gain. |
| Blind-12 | false | reject | `Hexalith.Platform.TestExtension.csproj:27` already admits any catalog-selected EventStore version via `HexalithCatalogEventStoreVersion`, and the `4.2.0` clause is the extension-release gate tested by `verify_sdk_gates.py`, explicitly preserved by Boundaries & Constraints. |
| Blind-13 | low | reject | Fix would edit this build's spec (`## Verification` missing `PYTHONPATH=eng`, already noted in Implementation Notes); rejected per Step 4 rule. |
| Blind-14 | false | reject | `sprint-status.yaml` updates in Step 5 per workflow (`false`); `result["extensionCatalogView"]` (`eng/run_tuple_ci.py:303`) is a pre-existing receipt schema key and `README.md:13` is pre-existing untouched prose (`low`, not caused by this diff). |

## Verification

**Commands** (`export PATH="$HOME/.dotnet/tools:$PATH"`):
- `python3 -B -m unittest eng/test_current_catalog_view.py eng/test_verify_platform_pins.py eng/test_tuple_sdk_suites.py eng/test_tuple_runtime_context.py eng/test_tuple_collectors.py eng/test_builds_tuple.py eng/test_builds_publication.py eng/test_builds_tool_lock.py eng/test_owner_packages_lock.py eng/test_current_tuple_source.py eng/test_verify_works_host.py` -- expected: all pass, zero skips.
- `python3 -B eng/builds_tuple.py && python3 -B eng/owner_packages_lock.py --check && python3 -B eng/verify-platform-pins.py` -- expected: all pass.
- `npm run test:source-release && node --test eng/test-extension-release/gate.test.mjs && python3 -B eng/test-extension-release/run-controls.py` -- expected: all pass.
- `NO_COLOR=1 TERM=dumb python3 -B eng/run_tuple_ci.py --audit-only --suite <suite> --evidence <tmp-dir>` for all 3 suites -- expected: `graph-verified`.
- Release package-mode build (`-c Release -p:UseHexalithProjectReferences=false -p:HexalithBuildsPublishedToolRoot=<verified-tool-root> -nodeReuse:false`) and direct xUnit execution of every group in the 3 suite profiles -- expected: exact measured totals, zero skips, zero failures.
