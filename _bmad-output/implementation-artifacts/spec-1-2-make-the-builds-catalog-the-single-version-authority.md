---
title: '1.2 Make the Builds catalog the single version authority'
type: 'refactor'
epic: 1
story: 2
created: '2026-10-08'
status: 'in-progress'
route: 'dispatch'
review_loop_iteration: 2
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


### Review-derived implementation requirements

- `T/Hexalith.Builds.Tooling.csproj` and `Tools/write-platform-version-catalog.ps1` — use the effective controlled build selections or reject inconsistent build overrides; use the same resolved resource path for relative/absolute intermediate directories. Test both paths and controlled override behavior.
- `Tools/write-platform-version-catalog.ps1` — detect duplicate unconditional declarations of required selection properties without rejecting intentional conditional exceptions. Restrict dependency extraction to actual metadata/dependencies and choose the framework group applicable to net10.0 using NuGet framework semantics; reject absent or invalid effective metadata. Test duplicate properties, misplaced dependency elements and valid differing framework groups.
- `T/Manifest/PlatformVersionCatalog.cs` and the generator — use consistent NuGet version/range semantics so every generated snapshot loads offline; accept valid wildcard/short ranges and reject inverted ranges, malformed versions and numeric prerelease leading zeros. Keep data-record semantics and existing facade names; add only the dependency needed to share semantics if required, without upgrading existing selections.
- Generator and snapshot reader — enforce identical case-sensitive Redis repository validation, including nonempty path components, and cover uppercase/empty-component cases.
- `Tools/g6_current.py`, `Tools/runtime_toolchain_v2.py` — derive current controlled properties/items through MSBuild evaluation, respecting imports, property-group conditions and nested references. Keep retained historical evidence unchanged and add hermetic evaluated-property regressions.
- `Tools/validate-package-version-exceptions.ps1` — parse active SDK XML declarations independent of quote style and ignore comments; keep root/element/import support and test active single-quoted/commented imports.
- `Tools/test-runtime-toolchain-evidence-validator.py` — invoke the new SDK-import controls from the existing CI wrapper separately from retained 103-control evidence counts.
- `M/CompositionTestFiles.cs`, affected test callers and `Tools/test-platform-version-catalog.ps1` — keep shell-specific tests guarded on Windows, strip existing SDK build metadata before constructing fake output, and create fixed synthetic regression catalogs independent of the live authoritative pin values. Test real current metadata separately. Do not rewrite any bound fixtures.

- Catalog evaluation/generation — remove inherited controlled selection properties before neutral catalog evaluation; reject differing build selections, including environment overrides, rather than embedding values from outside the catalog. Carry Configuration/TargetFramework context into standalone generation and reject only inconsistent controlled selections. Add environment and conditional-build regressions.
- SDK scanners in Python/PowerShell — use the consuming project's effective properties and conditions for controlled Aspire SDK declarations, including root/element/import forms and XML namespaces. Respect inactive imports; ignore unrelated SDK properties. The PowerShell validator must compare consuming selections with catalog authority free of inherited selection values, or fail closed on those inherited values; add an environment-injected SDK regression. Do not silently replace consuming-project overrides with neutral catalog values; report real drift. Capture SDK import version/condition at its MSBuild property/import evaluation point, before later project property assignments can overwrite it; include a later-assignment regression. Evaluate without launching an application and use hermetic projects for regressions.
- `Tools/g6-current-policy.json` — include the newly introduced evaluation helper in material inputs; preserve approval/tuple and all retained captures. Reuse one catalog observation per G6 direct-pin audit and register aligned/drifting consumer coverage in the normal CI controls.
- Offline parser and Aspire tests — validate the serialized selected hosting pair against its range; make fake Aspire executables require exactly `--version`.
- `Tools/package-version-audit.json` and `Tools/README.md` — the existing deterministic audit gate must remain strict. Prepare the exact owning-repository local-commit and audit-refresh sequence needed for new NuGet.Versioning/catalog/consumer declarations, preserving selections, historical observations and all bound qualification evidence. The existing generator rejects incremental refresh when a new package family is added, so a new NuGet.Versioning family requires complete refresh with the prior audit retained as historical context; do not weaken that contract. Do not commit, stage, fabricate revision/hash provenance, substitute synthetic evidence, or rewrite the audit before the user's separate authorization for a local commit. Run the validator and record its precise pending commit prerequisite; finish all other code and verification first. This remaining gate is a completion blocker, not a successful validation or a deferral.

## Tasks & Acceptance

**Execution:**

- [x] `Props/Directory.Packages.props` — add non-NuGet/SDK properties; preserve BOM/CRLF and selections.
- [x] `Tools/write-platform-version-catalog.ps1`, `Tools/validate-platform-version-catalog.ps1`, `T/Hexalith.Builds.Tooling.csproj` — generate and embed deterministic evaluated selections and resolved dependency requirements; validate the hosting pair without duplicated thresholds.
- [x] `T/Manifest/PlatformVersionCatalog.cs`, `SupportedPlatformPins.cs`, `T/Runtime/CompositionToolchainPins.cs`, `RuntimePrerequisiteGate.cs` — load the strict snapshot, replace constants and derive hints; retain facade names and data-record semantics.
- [x] `A/Hexalith.Builds.Module.AppHost.csproj`, `Tools/validate-package-version-exceptions.ps1`, `Tools/g6_current.py`, `Tools/runtime_toolchain_v2.py` — consume the SDK property and recognize explicit imports in current scanners, without changing retained evidence.
- [x] `T/Runtime/CompositionPrerequisiteProbe.cs`, `CompositionEngineOptions.cs`, `CompositionEngine.cs` — add injectable, bounded Aspire version probing before composition side effects.
- [x] `Tools/validate-dapr-package-versions.ps1`, `Tools/test-dapr-package-version-validator.ps1`, `.github/workflows/ci.yml`, `.github/workflows/build-release.yml` — derive SDK validation from the catalog and run the new compatibility gate before release.
- [x] `M/SupportedPlatformPinsCatalogTests.cs`, `CompositionPrerequisiteProbeTests.cs`, `CompositionEngineTests.cs`, `CompositionTestFiles.cs`, `Tools/test-platform-version-catalog.ps1`, `Tools/test-g4-tool-package-contracts.ps1` — cover the matrix, catalog mutation/rebuild, SDK imports, both pair changes, installed tools and affected scanners.
- [x] `README.md` — state tools are published and versions up to and including 4.27.4 are not Platform-accepted; describe catalog authority and startup diagnostics.

- [x] `Tools/package-version-audit.json` — after separately authorized local Builds commits, refresh the real revision-bound audit and pass its deterministic gate/fixtures before final review and completion.

**Acceptance Criteria:**

- Given a changed catalog selection, when tooling is rebuilt, then its consumers and hints reflect that value without editing C# pins, and a tooling-source search finds no selected version literals.
- Given either hosting pin changes, when catalog validation runs, then the selected nuspec determines compatibility and an incompatible pair fails naming both.
- Given incompatible Aspire CLI output, when run/test startup is requested, then human/JSON diagnostics name CLI and SDK versions and no descriptor or run resource starts.
- Given current selections and unchanged qualification fixtures, when existing suites and installed-tool contracts run, then they pass without granting new acceptance.

### Review Findings

Code review 2026-10-09 of Builds `fef0318..560fbaa`, with the generated `Tools/package-version-audit.json` excluded. Layers: blind-hunter, edge-case-hunter, verification-gap and acceptance-auditor; none failed. Paths are relative to `references/Hexalith.Builds`.

- [x] [Review][Decision] The v1 manifest schema still hard-codes the selected platform pins — resolved 2026-10-09: keep v1 as a deliberately co-edited legacy contract, enforced by the existing parity test, and document it (patch below). `schemas/hexalith.module-manifest.v1.json:65-68` fixes EventStore 3.117.1, Dapr runtime 1.18.2, Dapr SDK 1.18.10 and FrontComposer 4.6.0 as `const`. The unchanged `ManifestValidationTests.PlatformPinSchemaAndRuntimeValidationRemainInParity` asserts those values equal `SupportedPlatformPins.*`, which now come from the catalog. A catalog-only change therefore fails the Module suite until someone edits the published v1 schema by hand. That leaves a second version authority, contrary to matrix row 1 and the README's "changing a selection requires editing the catalog". Options: generate the v1 consts from the catalog at build, keep the schema as a deliberate co-edited contract and document it, or drop the consts from v1. (acceptance-auditor, medium)
- [ ] [Review][Patch] Document that a catalog pin change must also update the v1 schema consts [README.md:243] — from the resolved decision above. In the catalog section, state that `schemas/hexalith.module-manifest.v1.json` is a fixed legacy contract whose EventStore/Dapr runtime/Dapr SDK/FrontComposer consts must be edited alongside the catalog, and that `PlatformPinSchemaAndRuntimeValidationRemainInParity` enforces this. (acceptance-auditor, medium)
- [ ] [Review][Patch] Two existing `run` tests depend on the host's real Aspire CLI being exactly 13.6.0 [test/Hexalith.Builds.Module.Tests/PublicCompositionCommandTests.cs:62] — `ExecutableRunReportsMissingDaprWithoutCreatingStateAsync` and `ExecutableDescriptorLoaderTests.PublicRunWithApprovedDescriptorChecksLivePrerequisites` (`ExecutableDescriptorLoaderTests.cs:230`) leave `AspireCommand` at its default `aspire`. With Aspire absent from PATH, both get HXR015 instead of HXR011 and fail; this was reproduced locally with the built Debug assembly (2 of 2 failed). Builds CI installs no Aspire CLI, so AC4 breaks in CI. Fix: set `AspireCommand = CompositionTestFiles.CreateAspire(root)` in both. (acceptance-auditor+edge-case-hunter, high)
- [ ] [Review][Patch] The exceptions validator no longer records non-Aspire versioned SDK pins [Tools/validate-package-version-exceptions.ps1:108] — `Get-ProjectSdkVersionPins` returns `@()` unless `Aspire.AppHost.Sdk` is declared. An unlisted `Sdk="Microsoft.Build.NoTargets/3.7.0"` now exits 0 (the prior validator exits 1 with "unlisted version exception"), contrary to the README's closed-allowlist contract. It also drops case-variant Aspire IDs. Fix: keep the prior literal root-attribute/`<Sdk>` collection for non-Aspire IDs alongside the evaluated Aspire path, and add failing scenarios for both forms. (verification-gap+blind-hunter+edge-case-hunter, medium)
- [ ] [Review][Patch] Duplicate declarations of conditional-default selections are accepted silently [Tools/write-platform-version-catalog.ps1:93] — the duplicate check counts only declarations without a `Condition`. The real catalog declares `HexalithEventStoreVersion`/`HexalithFrontComposerVersion` with `Condition="'$(X)' == ''"`, so a second default, or a later unconditional redeclaration that wins (reproduced: FrontComposer 4.6.9, exit 0), passes without error. Meanwhile `runtime_toolchain_v2.inventory` dropped its former `len(defaults) <= 1` ambiguity check. Fix: count self-default-conditioned declarations as unconditional in the duplicate check, keeping the Folders `MSBuildProjectName` exception, and add real-form duplicate scenarios. (acceptance-auditor+edge-case-hunter, medium)
- [ ] [Review][Patch] No test ties the HXR015 expected version to catalog `HexalithAspireAppHostSdkVersion` [test/Hexalith.Builds.Module.Tests/SupportedPlatformPinsCatalogTests.cs:22] — every probe test reads the expected value back from the facade under test. Passing `aspireHostingVersion` (13.6.1) as the 6th constructor argument would demand the wrong CLI on every run/test with all tests green. Fix: extend `CatalogDefaultsMatchSupportedPlatformPins` to assert `HexalithAspireAppHostSdkVersion`, `HexalithDaprRuntimeVersion` and `HexalithDaprCliVersion` against their facades. (verification-gap, medium)
- [ ] [Review][Patch] The `python-tests` CI job runs `dotnet msbuild` without initializing .NET [.github/workflows/ci.yml:183] — `test-runtime-toolchain-evidence-validator.py` now calls `run_evaluated_catalog_controls`, which needs the `global.json` SDK (10.0.401, latestPatch). Unlike the other jobs, this one relies on whichever SDK the runner image ships, and will break when the image moves to another feature band. Fix: add the `./Github/initialize-dotnet` step. B3-5 rejected only current availability. (verification-gap, low)
- [ ] [Review][Patch] HXR015 prints `(exit -1)` when the Aspire CLI is missing or timed out [src/libraries/Hexalith.Builds.Tooling/Runtime/CompositionPrerequisiteProbe.cs:120] — include the exit code only when the process started and completed. (blind-hunter, low)
- [ ] [Review][Patch] Catalog test cleanup fails on Windows after loading rebuilt assemblies [Tools/test-platform-version-catalog.ps1:236] — `Assembly.LoadFrom` locks the DLLs, so `Remove-Item $temporaryRoot` in `finally` throws after every scenario has passed. Fix: make the cleanup tolerant, or inspect the assemblies in a child process. (edge-case-hunter, low)
- [ ] [Review][Patch] The README omits the new build/validation prerequisites [README.md:243] — building Tooling now needs `pwsh` on PATH and, when the selected nuspec is not cached, feed access. `validate-package-version-exceptions.ps1` now needs `python3`. (blind-hunter, low)
- [x] [Review][Defer] Workflow and action CLI pins are not derived from or checked against the catalog's new CLI/runtime fields [Github/workflows/domain-ci.yml:28] — deferred: pre-existing duplication outside this story's Code Map. `domain-ci.yml`/`domain-release.yml`/`Github/dapr-init/action.yml` default Dapr CLI 1.18.0/runtime 1.18.2, and G6 reads Aspire/Dapr CLI versions from the Projects `ci.yml`; runtime HXR012/HXR015 still surface mismatches. (blind-hunter+acceptance-auditor, low)
- [x] [Review][Defer] Python SDK scanners match `Aspire.AppHost.Sdk` case-sensitively [Tools/evaluated_catalog.py:72] — deferred: pre-existing regex behavior in the G6/runtime scanners. The PowerShell side is closed by the non-Aspire SDK-pin patch. (blind-hunter+edge-case-hunter, low)
- [x] [Review][Defer] Aspire SDK declarations supplied only through imported files are not observed [Tools/evaluated_catalog.py:88] — deferred: already recorded as B3-1/E3-1 in `deferred-work.md`, so no new ledger entry. (edge-case-hunter, medium)

Rejected:

- BH1+EH6 (low): Root/`<Sdk>`-form property versions are reported as aligned although real MSBuild resolves them before project properties. No consumer uses those forms, the real build fails loudly, and the fix means restructuring capture order.
- BH3+EH1 (low): An inherited `HexalithAspireAppHostSdkVersion` environment variable reaches the consumer SDK evaluation. Unlikely, and the correct fix is a new fail-closed guard.
- BH4 (low): The evaluators use different contexts. No catalog condition depends on Configuration, TargetFramework or UseHexalithProjectReferences today.
- BH5 (false): Catalog-derived G-6/Dapr targets are spec-mandated, and G6 `direct_pin_issues` still compares catalog packages with the owner-approved tuple.
- BH7 (rejected): The spec change log explicitly retains the duplicate public/engine Aspire probes (B2-10).
- BH8 (low/false): Probing Aspire before Dapr/Docker follows the spec ordering. `AspireProbeTimeout` is not user-configurable, so its throw is a programmer guard.
- BH9 banner/prefix (false/low): A fresh-HOME `aspire --version` writes only the version to stdout, and the probe discards stderr. Prefixed output forms do not occur with the current CLI.
- BH10 (low): The per-build generator costs about 2.6 s (measured). The restore scratch folder sits under the catalog directory on purpose, so it inherits the repository's NuGet.config hierarchy.
- BH11+VG-O2 (false): The scenario passing `PlatformVersionCatalogSource` guards the B3-6 removal against reintroduction. The no-op `Replace` is harmless.
- BH12 (low): The installed-tool probe's NuGet.Versioning load order works today, and a failure would be loud.
- BH13 (false): Tooling is `IsPackable=false` with no consumers outside Builds. Properties and record semantics are spec-mandated.
- BH14 `2>&1` (low): Only Python warnings could corrupt the JSON, and that is unlikely.
- BH15 (low): CI evaluation cost only.
- BH16 (false): Module-loading style issue with no bad outcome.
- BH17 (low): Builds' `Directory.Build.props` overrides no catalog property.
- BH18 hard-coded values (false): The 4.27.4 statement is spec-mandated, and "304 selections" is a historical record.
- VG2 (low): The Builds-owner catalog-selected branch is untested but works (demonstrated). A regression is unlikely, and the fix adds three scenarios.
- AA5+EH9 (low): No workspace consumer uses `$(Hexalith*Version)` in package items, and a fix would need consumer-project evaluation.
- AA6 (low): The 7.9.0 reader re-parses generated snapshots in tests, so a semantics divergence is unlikely.
- EH4 (false): A versionless Aspire SDK fails loudly, and the workspace has no such declaration.
- EH5 (false): Imports using SDK-defined paths were never shown in any consumer, and they fail loudly.
- EH7 (low): A case-variant property name fails loudly as an unlisted exception.
- EH8 (false): Empty evaluated versions fail downstream (writer, tuple comparison, restore).
- EH12 (low): The embedded catalog is always validated at build. A `JsonException` comes only from malformed JSON passed to public `Parse`.
- EH13 (low): A malformed project aborts the run loudly instead of being aggregated.
- EH14 (low): There are no Configuration-conditional selections, and a mismatch would be loud.
- EH18 (low): The commit-message claim is too broad, but the underlying defects are tracked individually and committed history is not rewritten.

## Implementation Notes

- Catalog generation reads evaluated CPM items and non-NuGet properties, resolves the selected EventStore.Aspire nuspec through the NuGet cache/configured sources, and uses NuGet range semantics before embedding the deterministic snapshot.
- Both the composition engine and public executable run/test path probe Aspire before descriptor discovery. Version/build metadata handling and unavailable outcomes are covered with injected executable paths and finite bounds.
- AppHost SDK imports and their scanners consume the catalog property. Existing selections, Folders exception, catalog BOM/CRLF and bound evidence bytes are preserved.
- Implementation began at Builds revision `fef031806321793c9effb17235c2465118984432`; the preserved spec baseline is `fc2e186fcd9dc29434e9e3448a8c775eb83ac9ff`. The intervening CI shard change predates this implementation. Concurrent Platform custody edits and commits are outside this story.

## Spec Change Log

- 2026-10-08, review loop 1: BH1/BH3/BH7/BH8/EH6 exposed missing technical requirements outside the frozen intent. Added the review-derived implementation rules above to prevent neutral/build evaluation drift, ambiguous declarations, wrong framework metadata and tests tied to live pins. KEEP: deterministic embedded snapshot and pin facades; bounded Aspire probing in both engine/public paths before descriptors; all passing lifecycle/no-artifact tests; Folders exception; existing pins, BOM/CRLF, bound fixtures and evidence; catalog/Dapr/SDK CI gates, README acceptance status, and installed-tool probe behavior. Re-derive from the archived implementation at `/tmp/bmad-build-1-2-implementation-87qjkoql.diff`, preserving these successful parts while correcting every confirmed review defect. The lower patch entries remain applicable during re-derivation.

- 2026-10-08, review loop 2: B2-1/B2-2/B2-3/B2-4/B2-6/E2-2/E2-4 exposed missing effective-project SDK evaluation, environment isolation, build context and revision-bound audit requirements. Added technical rules above to avoid false SDK drift, hidden overrides, environment-selected pins and a broken preexisting CI gate. KEEP: all review-loop-1 catalog/resource/metadata fixes, NuGet semantics, strict images, historical controls, positive startup/no-artifact coverage, fixed synthetic catalogs and Windows skip diagnostics; all original selections, Folders exception, BOM/CRLF, 515 bound evidence/fixture files and unrelated user changes. Re-derive the passing implementation from `/tmp/bmad-build-1-2-review2-msm246wm.diff`; apply all surviving loop-2 patches. Retain duplicate public/engine prerequisite checks. The package audit remains unchanged pending separately authorized local commit; no audit or acceptance claim may be fabricated to make this gate green.

## Review Triage Log

| Finding | Verdict | Route | Evidence |
| --- | --- | --- | --- |
| BH1 | medium | bad_spec | Tooling.csproj invokes a neutral evaluation without the build globals; the catalog defaults explicitly permit controlled property overrides, so the AppHost graph can select values unlike the embedded snapshot. Align or consistently reject effective build selections. |
| BH2 | medium | patch | The Exec output concatenates MSBuildProjectDirectory with IntermediateOutputPath while EmbeddedResource uses IntermediateOutputPath alone; absolute intermediate paths therefore diverge. |
| BH3 | medium | bad_spec | The duplicate-property reproduction generated a successful snapshot because MSBuild collapsed duplicate unconditional SDK declarations. The matrix requires ambiguous required selections to fail. |
| BH4 | medium | patch | The parent reran the wildcard-range reproduction: NuGet generation succeeds but Parse rejects it. The range regex also accepts nonnumeric and inverted bounds, unlike NuGet validation. |
| BH5 | medium | patch | The parent reran the uppercase-image snapshot: generation accepts Docker.io/library/redis through case-insensitive matching while Parse rejects it. |
| BH6 | medium | patch | The parent reran the bad-image snapshot: docker.io//library/redis is accepted; empty repository path components cause the composed image to be unusable. |
| BH7 | medium | bad_spec | The writer aggregates all matching dependencies across framework groups, so compatible net10.0 metadata is rejected when another framework legitimately selects a different range. |
| BH8 | medium | bad_spec | Both new Python property maps read raw XML and expand once; imports, group conditions and nested references can resolve correctly through MSBuild yet remain unresolved or incorrect in SDK audits. |
| BH9 | medium | patch | The parent reran the scanner reproduction: active single-quoted imports yield zero pins, while a commented import yields one. XML parsing is required for the newly added import path. |
| BH10 | medium | patch | CreateAspire writes a POSIX script. MissingPrerequisitesStopBeforeAnyResourceAsync and RunWithInvalidExecutableDescriptorFailsBeforeRuntimeAsync have no Windows guard, so the new probe preempts their asserted diagnostics on Windows. |
| EH1 | medium | patch | Duplicate root cause of BH4: the generated wildcard NuGet range is rejected by the offline parser. |
| EH2 | medium | patch | The writer XPath selects matching dependency elements anywhere in the nuspec; a dependency under description is consequently accepted despite missing actual metadata/dependencies requirements. |
| EH3 | medium | patch | Duplicate root cause of BH2: generator and resource paths diverge for absolute intermediate paths. |
| EH4 | medium | bad_spec | Duplicate root cause of BH1: effective build overrides are absent from the separate snapshot evaluation. |
| EH5 | low | patch | CreateAspire appends +test-build to the full selected SDK string; a selected version containing metadata emits two plus separators. Strip existing metadata before appending test metadata. |
| EH6 | medium | bad_spec | Contract scenarios use the live catalog plus replacements keyed to 3.117.1 and the current Toolkit value; future catalog-only changes leave synthetic nuspec identities inconsistent. Separate fixed historical/current regression inputs from the actual catalog probe. |
| EH7 | medium | patch | The snapshot regex accepts inverted bounds and numeric prerelease identifiers with leading zeros. NuGet parsing rejects those values, so generated and parsed strictness diverge. |
| VG1 | medium | patch | Pre-verified: normal CI invokes the historical wrapper, which calls only run_controls()==103; mutation evidence confirms explicit import recognition can regress while all 103 controls pass. Call the three new controls separately without changing retained counts. |
| VG2 | medium | patch | Verified by parent wildcard reproduction and parser inspection: valid NuGet ranges can be rejected by the offline reader; same cause as BH4/EH1/EH7. |
| B2-1 | medium | bad_spec | The unchanged revision-bound package audit rejects the new catalog dependency and changed AppHost/Tooling declarations (11 failures); both CI workflows run this gate. The plan omitted the commit-bound audit refresh, which requires a real committed declaration revision and cannot be replaced with fabricated hashes. |
| B2-2 | medium | bad_spec | The generator inherits MSBuild selection properties from its environment; setting HexalithFrontComposerVersion changes the embedded version while the catalog bytes remain unchanged. Matching inherited build/evaluation values does not establish catalog authority. |
| B2-3 | medium | bad_spec | Python SDK scanners expand version expressions against neutral catalog properties; a consuming project override before its SDK import is ignored, concealing actual SDK drift from direct_pin_issues and runtime inventory. |
| B2-4 | medium | bad_spec | Python and PowerShell SDK import scans do not evaluate Import conditions. A false-condition import is reported as an active selection, creating false drift and spurious runtime required packages. |
| B2-5 | medium | patch | PowerShell scans unrelated SDK imports but resolves only the Aspire property; an unrelated property-based SDK version throws and prevents the controlled Aspire audit. Restrict controlled validation to Aspire declarations. |
| B2-6 | medium | bad_spec | The generator's MSBuild evaluation omits Configuration and TargetFramework; a legitimate conditional catalog selection in the consuming build disagrees with neutral generation. Pass the defined build evaluation context while retaining rejection of selection overrides outside catalog authority. |
| B2-7 | medium | patch | PlatformVersionCatalog.Parse validates range grammar but never checks that the selected Toolkit version satisfies it; an incompatible serialized snapshot is accepted by the advertised strict offline parser. |
| B2-8 | medium | patch | The newly imported evaluated_catalog.py is outside G6 policy materialFiles and the existing Builds/src prefix. Changing current selection evaluation leaves the material fingerprint unchanged; include the helper without altering approval or retained captures. |
| B2-9 | low | patch | direct_pin_issues independently runs the same two-process catalog evaluation for every resolved project, yielding 24 launches for the current project list. Reuse one properties/items result within that call. |
| B2-10 | low | rejected | Public execution and direct engine execution both probe Aspire, adding a second short version call. Both protect supported entry points; eliminating duplication requires coordination state/branches for an uncommon changing-executable case, beyond a direct correction. |
| E2-1 | medium | patch | Python matches raw XML tags for SDK imports, so MSBuild's supported XML namespace hides active Aspire imports and drift. Normalize local tag names. |
| E2-2 | medium | bad_spec | Same demonstrated inactive-import defect as B2-4: runtime required_packages includes a false-condition SDK import and demands a package that the project does not use. |
| E2-3 | medium | patch | PowerShell expands the catalog SDK property only on element/import declarations; a root Project Sdk expression is returned unresolved and incorrectly treated as a literal exception. Apply the same controlled resolution/catalog-selection handling to the root form. |
| E2-4 | medium | bad_spec | Same demonstrated inherited-environment authority bypass as B2-2: a FrontComposer environment variable changes the generated snapshot without editing the catalog. |
| V2-1 | medium | patch | Pre-verified: positive fake Aspire scripts ignore arguments, so omitting or misspelling --version survives the passing version tests. Make the fakes require exactly --version and retain positive assertions. |
| V2-2 | medium | patch | Pre-verified: normal evaluated-catalog controls exercise runtime inventory but do not cover the G6 direct_pin_issues consumer. Add aligned and changed-SDK scenarios through that actual consumer in the registered controls. |
| P2-1 | medium | patch | Parent reproduction: HexalithAspireAppHostSdkVersion=99.0.0 followed by validate-package-version-exceptions.ps1 exits 0 and validates 15 exceptions. Its direct neutral catalog evaluation still inherits the same injected SDK value as the consumer. Compare against environment-free authority or reject this demonstrated inherited selection. |
| P2-2 | medium | patch | Parent reproduction sets SDK property 99.0.0 before an Aspire SDK import and 13.6.0 after it; project_sdk_selections returns 13.6.0. SDK imports resolve during the property/import pass, while the transformed items see final properties, so actual SDK drift is hidden. Capture the value/activation at the declaration point before emitting observation items. |
| P2-3 | medium | patch | Parent reproduction of a false-condition ImportGroup containing an Aspire SDK import raises MSB4018 during transformed XML evaluation. The helper inserts PropertyGroup/ItemGroup children beneath ImportGroup, where MSBuild permits only Import nodes. Flatten transformed groups into valid siblings while preserving declaration-point group/child activation; cover active and inactive groups. |


| B3-1 | medium | defer | Parent reproduced an imported-only SDK returning no pins. The source-start G6/runtime/exception scanners read only the project's XML, so the same transitive-import gap predates this story; no existing consumer was changed to declare its SDK indirectly. Record one shared deferred import-closure issue with E3-1. |
| B3-2 | low | rejected | Parent reproduced a second SDK import conditioned on SDK-provided IsAspireHost being omitted. Current Builds/Folders declarations do not depend on SDK side effects to choose another SDK; this requires uncommon mixed SDK declarations, and preserving arbitrary SDK effects requires resolver/property simulation beyond a direct correction. Keep the SDK-free observation contract. |
| B3-3 | low | rejected | Parent reproduced an inactive Release-only SDK import producing no Debug observation. Runtime inventory expressly captures minimum Debug/source declarations; no current controlled consumer has a Release-specific SDK selection. Extending the exception scanner to multiple configurations needs configuration APIs and consumer routing, beyond a direct correction for an uncommon alternate-SDK declaration. |
| B3-4 | medium | patch | Parent put an unavailable 111.0.100 global.json beside the consumer: its actual dotnet selection exits 155, while the helper succeeds from its temporary runner directory. Keep the temporary runner but launch it from the consumer directory so the repository's SDK selection applies. |
| B3-5 | false | rejected | The registered Python controls use temporary SDK-free fixture projects. The official [Ubuntu 24.04 runner inventory](https://raw.githubusercontent.com/actions/runner-images/main/images/ubuntu/Ubuntu2404-Readme.md) includes .NET SDK 10.0.401 and other MSBuild/Roslyn-capable SDKs; neither an unavailable toolchain nor a required feature-band mismatch occurs in this job. The verification reviewer independently withdrew this candidate. |
| B3-6 | medium | patch | Parent ran GeneratePlatformVersionCatalog with an alternate catalog and matching global Dapr runtime override: it succeeded and emitted 2.3.4 while the owning catalog selects 1.18.2. Remove the unnecessary overridable production catalog-source property and pass the owning path directly; retain standalone fixture CatalogPath support. |
| B3-7 | medium | patch | Parent passed TargetFramework=net8.0 with distinct net8/net10 metadata ranges: the writer selected net10 [13,14) and succeeded despite net8 [12,13) rejecting the Toolkit. Use the already-supplied TargetFramework for restore/group selection/diagnostics; default net10 remains unchanged. |
| B3-8 | medium | patch | Parent generated a snapshot for docker.io/library/redis..bad, which both existing image regexes accept. The [Docker reference component grammar](https://raw.githubusercontent.com/distribution/reference/main/regexp.go) permits a single dot separator between alphanumeric components, so this name is malformed. Correct both build/offline regexes and cover the demonstrated components. |
| B3-9 | medium | patch | Parent supplied selected and Wrong.Package ID nodes in one metadata element; generation accepted the ambiguous identity. Require one metadata element and exactly one ID/version before existing identity and dependency checks. |
| B3-10 | medium | patch | Parent set all six Dapr packages to 1.18.10-beta.01; the newly catalog-derived validator succeeded. Correct its existing version predicate to reject numeric prerelease leading zeros and retain valid release/prerelease/build forms. |
| E3-1 | medium | defer | Parent reproduced the same imported-only SDK omission as B3-1 and confirmed it in the source-start scanners. Shared pre-existing import-closure cause; append only one deferred entry for the group. |
| E3-2 | medium | patch | Parent reproduced the same non-net10 dependency-group selection as B3-7. One shared literal-framework correction preserves the selected net10 matrix and fixes the supplied-framework path. |

## Verification

From Builds: `dotnet build test/Hexalith.Builds.Module.Tests -c Debug -m:1` and `dotnet build test/Hexalith.Builds.Evidence.Tests -c Debug -m:1`; run both built xUnit assemblies directly. Run catalog, Dapr, SDK-exception and affected Python fixture suites. Run fresh local `Tools/test-g4-tool-package-contracts.ps1 -SkipSourceValidation -RetainPackageDirectory` probes; retain commands/results. Check diffs and evidence hashes. Probes grant no acceptance.


Verification after review loop 1: both Debug builds succeeded without warnings/errors; module xUnit ran 466 tests and evidence xUnit ran 107, with no failures, skips or unrun tests on Linux. Final Windows skip diagnostics were then rebuilt and both affected tests passed. Catalog (64), Dapr (30), SDK exceptions (13), package artifacts (34), historical controls, retained 103 current controls and the separate SDK-import/evaluated-catalog controls passed. Two fresh installed-tool probes passed, including required controls; both remain release-ineligible. All 515 tracked evidence/fixture files match their baseline bytes under tracked checkout filters, and catalog BOM/CRLF are preserved.

Commands, logs, package inventories and matrix coverage are retained in [review1-verification.json](../../references/Hexalith.Builds/artifacts/story-1-2-verification-20261008/review1-verification.json). The implementation review diff is `/tmp/bmad-build-1-2-review2-msm246wm.diff`.

Environmental limitation: `python3 Tools/test_g6_current.py` retains the three pre-existing missing-path errors because the suite assumes a Projects workspace and retained attempt-16 logs absent here. Both focused changed tests and the normal CI wrapper's self-contained current/historical controls pass. Its failure log is retained without weakening the broad gate.


Verification after review loop 2: both Debug builds pass with zero warnings/errors; Module tests pass 467/467 and Evidence tests 107/107 without failures or skips. Catalog (70), Dapr (30), SDK exceptions (25), artifact (34), retained 103 current controls, 3 SDK-import controls and 37 evaluated-project controls pass, including all six active/inactive ImportGroup scenarios. Both fresh installed-tool probes pass, one including required qualification controls; both remain release-ineligible. Every frozen matrix row remains covered by its passing full-assembly/script/installed-tool tests. All 515 tracked evidence/fixture files remain identical under checkout filters, and catalog BOM/CRLF and G6 approval/tuple are preserved. Parent verified all final log/inventory hashes and actual AppHost SDK/CPM evaluation, and whitespace checks pass with CRLF handling.

Final commands, precise audit failures, intermediate logs, package inventories and preservation proofs are retained in [verification.json](../../references/Hexalith.Builds/artifacts/story-1-2-verification-20261008-v3/verification.json); the final review diff is `/tmp/bmad-build-1-2-review3-audit-b0klw2fn.diff`. The final independent review and all six direct corrections are complete.

Audit prerequisite resolved after the user's authorization: source commit `9566bafbc93118bd717cc610e11fc6584362ad18` passed pinned commitlint 21.2.3. Complete real-feed refresh records that exact revision for 305 packages and 147 families; the deterministic gate passes, generator fixtures pass 115 scenarios, and validator fixtures pass 103 scenarios. All 304 prior selections, 2,135 existing family-history entries and 4,960 existing package-history entries are preserved; only NuGet.Versioning 7.9.0 is newly selected. The same seven prior unresolved feed results remain explicitly unresolved. The completed refresh and updated [procedure](../../references/Hexalith.Builds/Tools/README.md#revision-bound-audit-refresh) are included in the final audit commit recorded below. The broad G6 command retains its three pre-existing missing Projects-workspace/attempt-16 paths; focused changed tests and the normal CI wrapper pass.

Parent final verification: the normal CI wrapper passed after P2-3 with 37 evaluated-project controls and retained historical/103 current controls. The SDK exception suite passed 25 scenarios. Parent verified the complete final command/log inventory, both package inventory hashes, all 515 fixture/evidence files, whitespace handling and matrix coverage before the separate commit authorization; no remote operation was performed. The final implementation diff and SHA-256 are recorded in the current verification manifest.

Authorization recorded: on 2026-10-08 the user approved the two local Builds commits and real package audit refresh needed to finish this story. This authorization covers the prepared source/audit sequence and necessary review corrections. Preserve unrelated Platform work and existing selections; no push, package publication or Platform acceptance is authorized.

Final-review provenance: `/tmp/bmad-build-1-2-all-audit-hwri47av.diff` captures all changes since the preserved root/Builds baselines, including unrelated concurrent Platform work. The reviewers receive `/tmp/bmad-build-1-2-review3-audit-b0klw2fn.diff`, containing this story’s complete Builds implementation and audit against its actual source-start revision, so unrelated Platform custody edits and the preceding CI shard fix remain outside the story review.

Final independent review: all three layers completed. Verification-gap reviewer reported no gaps. Each blind/edge finding was verified and recorded individually above before grouping; six direct corrections were applied and verified, with one shared pre-existing import-closure limitation deferred and two uncommon low-impact boundaries rejected under the workflow’s complexity rule.

Final verification after review corrections (2026-10-09): correction commit `200a01334b97bc1f254be0be22575b52960abac5` passed pinned commitlint. Both Debug builds have zero warnings/errors; Module tests pass 475/475 and Evidence tests 107/107 with no failures/skips. Catalog (89), Dapr (36), SDK exceptions (25), artifact (34), 43 evaluated-project controls, 3 SDK-import controls and all retained current/historical controls pass. Audit fixtures pass 115 generator and 103 validator scenarios again. The incremental real-feed audit refreshes the four families bound to the corrected Tooling declaration and preserves the other 143 records exactly; its deterministic validator passes for all 305 packages/147 families. All 305 selections and every existing historical entry are preserved.

Both fresh installed-tool probes pass, including one with required qualification controls; neither is release-eligible or grants Platform acceptance. The second probe initially failed during its Release build without retaining underlying builder output. Serial and exact-command diagnostic builds both passed without source changes, and a fresh second probe then passed; the original failure, diagnostics and successful retry are retained separately. Parent verified all final log/inventory hashes, all seven frozen matrix rows, all 515 unchanged evidence/fixture files, catalog BOM/CRLF and the unchanged G6 approval/tuple/exceptions. The only deferred review group is the pre-existing imported-only SDK declaration scanner gap (B3-1/E3-1). The broad G6 suite's previously documented missing Projects-workspace/attempt-16 paths remain an environmental limitation; focused tests and the normal CI wrapper pass.

The final patched diff is `/tmp/bmad-build-1-2-review3-audit-b0klw2fn.diff`; the pre-patch reviewed bytes and final/full-baseline hashes are retained in the verification manifest. Preserve all unrelated Platform work and leave root tracking/gitlink changes uncommitted under the user's local Builds-only authorization.

Local completion commits: implementation `9566bafbc93118bd717cc610e11fc6584362ad18`, review corrections `200a01334b97bc1f254be0be22575b52960abac5`, and final audit/documentation `560fbaaa8547d8467f19173279f36887361712e4`. The extra correction commit preserves ancestry for the first complete refresh's observed provenance; the subsequent four-family refresh binds the corrected declaration. Every message passed pinned commitlint 21.2.3. Builds is clean; no remote operation, publication or Platform acceptance occurred. Workflow spec status is done and sprint status is review. Root spec/sprint/deferred-work and the Builds gitlink remain uncommitted under the Builds-only authorization; unrelated Platform custody work is preserved.

Final committed result: the deterministic audit validator passes again against `560fbaaa8547d8467f19173279f36887361712e4` (305 packages, 147 families, one source). The completed verification manifest records every successful final command, both installed inventories, the retained initial probe failure/retry, all review dispositions and preservation proofs.
