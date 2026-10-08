---
title: '1.2 Make the Builds catalog the single version authority'
type: 'refactor'
epic: 1
story: 2
created: '2026-10-08'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 0
baseline_commit: 'fe25d0fda681cdde4fc4ecfab76bc32e9ead5132'
builds_baseline_commit: 'fc2e186fcd9dc29434e9e3448a8c775eb83ac9ff'
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Builds duplicates catalog versions and lacks Aspire CLI/SDK equality and hosting-pair compatibility checks.

**Approach:** Make `Props/Directory.Packages.props` the authority for tooling selections. Embed its evaluated values in installed tools, validate the hosting pair against package dependency metadata, and refuse incompatible composition startup.

## Boundaries & Constraints

**Always:** Implement in `references/Hexalith.Builds`. Preserve EventStore 3.117.1, FrontComposer 4.6.0, Dapr SDK 1.18.10, runtime 1.18.2, CLI 1.18.0, Builds AppHost SDK 13.6.0, and Redis image/tag/digest. Aspire CLI must match that SDK; Aspire.Hosting remains 13.6.1. Read evaluated CPM items; move non-NuGet selections into catalog properties. Preserve the Folders.Aspire exception; validate the Platform composition pair.

Use the selected EventStore.Aspire nuspec's Toolkit dependency range and NuGet version semantics. Cover historical 3.109.0/beta.767 in regressions. Build/validation may resolve metadata through cache/configured restore sources; installed tools work offline outside a checkout. Missing/invalid metadata fails without fallback pins.

Check Aspire before descriptors, run state, workspaces or resources. Compare release/prerelease versions, ignoring build metadata. Keep declaration/evidence validation, help and cleanup independent of Aspire.

**Never:** Upgrade selections, migrate historical baselines, rewrite bound fixtures or qualification evidence, grant package publication or Platform acceptance, launch a live environment, edit the Platform AppHost or other modules, or initialize nested submodules. Story 1.9 owns first Platform tool acceptance.

## I/O & Edge-Case Matrix

| State | Expected behavior |
| --- | --- |
| Complete evaluated catalog | All pin consumers and hints use its values |
| Missing, duplicate or malformed required selection | Actionable failure naming the catalog field; no fallback |
| Compatible EventStore.Aspire/Toolkit pair | Validation succeeds |
| Toolkit outside selected package's dependency range | Validation fails naming both packages, versions and required range |
| Aspire CLI matches SDK, including a build hash | Startup prerequisite succeeds |
| Different, missing, malformed, failed or timed-out CLI | Prerequisite failure identifies observed CLI and expected SDK; no run artifacts |
| Packaged tool outside source checkout | Embedded catalog remains available offline |

</frozen-after-approval>

## Code Map

Paths are relative to Builds. `T` = `src/libraries/Hexalith.Builds.Tooling`; `A` = `src/hosts/Hexalith.Builds.Module.AppHost`; `M` = `test/Hexalith.Builds.Module.Tests`. Grouped bare filenames share the preceding directory.

- `T/Manifest/SupportedPlatformPins.cs` feeds `ModuleManifestLoader`, `RuntimePrerequisiteGate` and `G4P0AcceptanceValidator`; keep the facade, replace constants with properties.
- `T/Runtime/CompositionEngine.cs:64` probes before descriptors/state; reuse `CompositionProcess` bounds/cancellation.
- `T/Hexalith.Builds.Tooling.csproj` already embeds the v2 schema. Register generated resources before `AssignTargetPaths`, including on incremental builds.
- `A/Hexalith.Builds.Module.AppHost.csproj` has a literal SDK pin; SDK scanners currently miss explicit imports.
- `Tools/validate-dapr-package-versions.ps1` hard-codes its target; derive it from evaluated Dapr.Client. Leave Dapr package rows in their existing form to preserve historical parser compatibility.

## Tasks & Acceptance

**Execution:**

- [ ] `Props/Directory.Packages.props` — add non-NuGet/SDK properties; preserve BOM/CRLF and selections.
- [ ] `Tools/write-platform-version-catalog.ps1`, `Tools/validate-platform-version-catalog.ps1`, `T/Hexalith.Builds.Tooling.csproj` — generate and embed deterministic evaluated selections and resolved dependency requirements; validate the hosting pair without duplicated thresholds.
- [ ] `T/Manifest/PlatformVersionCatalog.cs`, `SupportedPlatformPins.cs`, `T/Runtime/CompositionToolchainPins.cs`, `RuntimePrerequisiteGate.cs` — load the strict snapshot, replace constants and derive hints; retain facade names and data-record semantics.
- [ ] `A/Hexalith.Builds.Module.AppHost.csproj`, `Tools/validate-package-version-exceptions.ps1`, `Tools/g6_current.py`, `Tools/runtime_toolchain_v2.py` — consume the SDK property and recognize explicit imports in current scanners, without changing retained evidence.
- [ ] `T/Runtime/CompositionPrerequisiteProbe.cs`, `CompositionEngineOptions.cs`, `CompositionEngine.cs` — add injectable, bounded Aspire version probing before composition side effects.
- [ ] `Tools/validate-dapr-package-versions.ps1`, `Tools/test-dapr-package-version-validator.ps1`, `.github/workflows/ci.yml`, `.github/workflows/build-release.yml` — derive SDK validation from the catalog and run the new compatibility gate before release.
- [ ] `M/SupportedPlatformPinsCatalogTests.cs`, `CompositionPrerequisiteProbeTests.cs`, `CompositionEngineTests.cs`, `CompositionTestFiles.cs`, `Tools/test-platform-version-catalog.ps1`, `Tools/test-g4-tool-package-contracts.ps1` — cover the matrix, catalog mutation/rebuild, SDK imports, both pair changes, installed tools and affected scanners.
- [ ] `README.md` — state tools are published and versions up to and including 4.27.4 are not Platform-accepted; describe catalog authority and startup diagnostics.

**Acceptance Criteria:**

- Given a changed catalog selection, when tooling is rebuilt, then its consumers and hints reflect that value without editing C# pins, and a tooling-source search finds no selected version literals.
- Given either hosting pin changes, when catalog validation runs, then the selected nuspec determines compatibility and an incompatible pair fails naming both.
- Given incompatible Aspire CLI output, when run/test startup is requested, then human/JSON diagnostics name CLI and SDK versions and no descriptor or run resource starts.
- Given current selections and unchanged qualification fixtures, when existing suites and installed-tool contracts run, then they pass without granting new acceptance.

## Implementation Notes

## Spec Change Log

## Review Triage Log

## Verification

From Builds: `dotnet build test/Hexalith.Builds.Module.Tests -c Debug -m:1` and `dotnet build test/Hexalith.Builds.Evidence.Tests -c Debug -m:1`; run both built xUnit assemblies directly. Run catalog, Dapr, SDK-exception and affected Python fixture suites. Run fresh local `Tools/test-g4-tool-package-contracts.ps1 -SkipSourceValidation -RetainPackageDirectory` probes; retain commands/results. Check diffs and evidence hashes. Probes grant no acceptance.
