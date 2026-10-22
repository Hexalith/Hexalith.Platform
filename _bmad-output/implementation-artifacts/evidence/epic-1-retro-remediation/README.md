# Epic 1 retrospective remediation verification

## Final verification after review

This section supersedes the historical runs below. All accepted review patches
are implemented. Three independent reviewers returned eleven findings: three
were patched, one release-coordination issue was deferred, and the rest were
individually rejected with evidence in the spec's Review Triage Log.

Concurrent unfinished source changes prevented a fresh composition build in the
shared workspace: [final-composition-build.txt](final-composition-build.txt)
records the ReleaseRecords/TestEnvironment and Dapr renderer compile errors.
Those changes were preserved. Verification therefore used a detached checkout
of Platform `e39d59b9e7f93c5880eff423e24b8a0f274560ed`, with all seventeen
root-declared references at their recorded commits, including Builds
`2210aaaf54c779b8763dbf1e8767bdc1b422bc6d`. The complete checkout inventory is
in [fixed-checkout.json](fixed-checkout.json); no nested references were initialized.

The final checkout also includes exactly the two test-diagnostic assertion
corrections in [final-reviewed-source.patch](final-reviewed-source.patch). These
match the shared workspace files and accept the existing relative diagnostic
paths while retaining the exact source/field, schema-reason and atomic-refusal
assertions. Per-command commits, source hashes and assembly hashes are retained
in the metadata linked below; the source remained stable during the fixed runs.

| Verification | Final result | Evidence |
| --- | --- | --- |
| Debug Builds test-project build | Exit 0; 0 warnings/errors | [fixed-builds-build.txt](fixed-builds-build.txt) |
| Full resolver and MSBuild test classes | 183 passed: 84 resolver and 99 MSBuild; 0 failures/errors/skips | [fixed-workspace-tests.xml](fixed-workspace-tests.xml), [command metadata](fixed-verification.json) |
| Debug composition test-project rebuild | Exit 0; 0 warnings/errors | [review-file-entry-build.txt](review-file-entry-build.txt) |
| Full composition assembly after both assertion corrections | 229 total: 218 passed, 4 failed, 7 opt-in live skips, 0 errors | [fixed-final-composition.xml](fixed-final-composition.xml), [command/source/assembly metadata](fixed-final-composition.json) |
| Complete declaration matrix | Exit 0; all 42 PASS rows | [review-declaration-matrix.txt](review-declaration-matrix.txt), [command metadata](review-path-assertions-verification.json) |
| Approved edge-case matrix | All 29 named cases across ten covering methods passed; no skips | [final-matrix-coverage.json](final-matrix-coverage.json) |

The exact commands are recorded as argument arrays in those metadata files.
They use `dotnet build <test-project> -c Debug -m:1`, the built xUnit assemblies
with single-dash `-class`/`-result-xml` arguments, and
`python3 scripts/test-reference-module-declarations.py`. The initial fixed runs
precede the two assertion corrections; their superseded five-failure and
38-PASS results remain available for traceability.

### Remaining failures and accountable owners

| Enabled failing test(s) | Exact blocker | Owner |
| --- | --- | --- |
| `DomainEnrollmentKitTests.AcceptedToolPinEqualsSupportedRangeFloorAsync` | Recorded Builds `2210aaaf54c779b8763dbf1e8767bdc1b422bc6d` differs from pinned `4.35.2` release-tag commit `4248b8c70cb35ba79fdd7e4dd63049d4c73211a0` | Platform/Builds release coordination |
| `DomainEnrollmentKitTests.ReleasedPinnedToolValidatesDisposableWorkspaceWithoutWrites`, both `human` and `json` | Restored tool metadata identifies `4248b8c70cb35ba79fdd7e4dd63049d4c73211a0`; recorded Builds identifies `2210aaaf54c779b8763dbf1e8767bdc1b422bc6d` | Platform/Builds release coordination |
| `PlatformAppModelTests.WorksPreviewComposesItsPreservedGraphWithoutDeclarationServers` | Duplicate `ParameterResource` named `eventstore-app-api-token` | Platform Works / EventStore Aspire integration |

The historical seed content-root failure did not reproduce in this fixed
checkout; no fix for it is claimed. The seven live skips and their required
opt-ins are listed below. The full composition suite remains red. No pin,
production snapshot, release guard or integration expectation was changed to
hide these results. This is local synthetic/source verification; real-owner
ordinary CLI acceptance, published-release acceptance and live SM-2 remain
pending, and the retrospective verdict and historical Dapr timeout are unchanged.

All eighteen temporary worktrees were removed after verification; the cleanup
preflight confirmed only the two retained assertion corrections were dirty.
[Cleanup commands/results](fixed-checkout-cleanup.json) are retained.

## Historical runs

Executed from `/home/administrator/projects/hexalith/platform` on 2026-10-22.
These are local Debug regressions and synthetic composition/static declaration
observations. They confer no published-release, ordinary CLI real-owner, or live
SM-2 acceptance. The historical Dapr timeout remains unresolved without new live
evidence. Production snapshots/provenance, catalog changes and gitlinks were not
changed by this implementation; concurrent Memories work was preserved.

## Historical implementation verification before review fixes

The class/full-suite counts and matrix below predate the review fixes. They are
historical observations and do not establish the current source matrix. See
[review-verification.md](review-verification.md) for current narrow per-case
results; the parent agent's subsequent rebuilt full audit supersedes this history.

### Commands and retained results

| Exact command | Result | Retained output |
| --- | --- | --- |
| `dotnet build references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1` | Exit 0; 0 warnings/errors | [builds-tests-build.txt](builds-tests-build.txt) |
| `dotnet references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -class '*WorkspaceRootResolverTests'` | Exit 0; 66 passed, 0 failures/errors/skips | [resolver-tests.txt](resolver-tests.txt) |
| `dotnet references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -class '*WorkspaceMsBuildTests'` | Exit 0; 95 passed, 0 failures/errors/skips | [msbuild-tests.txt](msbuild-tests.txt) |
| `python3 scripts/test-reference-module-declarations.py` | Exit 0; 42 PASS rows, including unmodified temporary baseline and final success row | [declaration-matrix.txt](declaration-matrix.txt) |
| `dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -m:1` | Exit 0; 0 warnings/errors | [composition-build.txt](composition-build.txt) |
| `dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -class '*PlatformAppModelTests' -class '*PartiesAdoptionTests' -class '*DomainEnrollmentBindingTests'` | Exit 1; 138 total, 134 passed, 1 failure, 3 opt-in live skips, 0 errors | [composition-focused.txt](composition-focused.txt) |
| `dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll` | Exit 1; 226 total, 214 passed, 5 failures, 7 opt-in live skips, 0 errors | [composition-full.txt](composition-full.txt) |

All repaired package/owner cases and new resolver/Parties/enrollment assertions
passed. Remaining full-suite failures are listed below. Resolver/MSBuild/matrix
commands initially overlapped; the final composition rebuild, focused run and full
suite were executed sequentially.

## Spec matrix coverage

| Matrix row | Covering regression/evidence |
| --- | --- |
| Valid single-owner v2 canonical/renamed standalone root | `StandaloneV2UsesValidatedOwnerIdentityAsync`: Git source/package, canonical/renamed/Hexalith names; `NonGitStandaloneV2UsesDeclaredIdentityAsync`: supported non-Git source and preserved package refusal; `ResolvedStandaloneRootsBindTheirActualPartiesProjectAsync`: resolver output binds the project beneath canonical/renamed Parties roots |
| Legacy/multi-module identity | `StandaloneWithoutUniqueV2OwnerKeepsDirectoryIdentityAsync`, both modes |
| Invalid v2/collision before initialization | `InvalidOrCollidingStandaloneV2RefusesBeforeCheckoutAsync`, both modes; invalid non-Git source row in `NonGitStandaloneV2UsesDeclaredIdentityAsync` |
| Canonical duplicate identity and broad root name | `ResolvedStandaloneIdentityRejectsPackageCopiesAsync`: renamed-owner and Hexalith root, direct/transitive duplicates; transitive wrapper passes direct guard before restored duplicate refuses |
| Unregistered new owner | `StaticValidityRequiresExplicitProjectAndScopedConfigurationBeforeComposition`: static validation succeeds, default project table refuses atomically, explicit project binding reaches missing configuration refusal, scoped configuration composes; routing tuple preservation/filtering asserted; composition-only synthetic evidence |
| Package/owner negative fixtures reach assertions | `PlatformPackageFixture` isolates catalog, declaration bytes, commits/hashes and physical entries; `PlatformOwnerFixture` provides root markers/declared files; provenance byte/repository, conflicting supplied owner, symlink containment, recovery conflict, failed private workspace and registry tests retain their intended assertions |
| Preserved checkout/symlink behavior | Complete resolver and MSBuild test classes, including recorded-gitlink, direct-only initialization, nested-reference and physical-containment regressions |
| Production provenance remains fail-closed | `PackageModeRefusesUnreleasedOwnerSnapshotBeforeAddingResources` reads production catalog/snapshots/default provenance independently of synthetic fixture options |

The bounded `PlatformManifestCommand.IsV2` classifier remains unchanged. For root
manifests, `ModuleCommandExecutionService` routes malformed/unsupported input to
`ModuleManifestLoader.Load` and returns invalid diagnostics before resolving the
root mapping. Recognizable invalid v2 input is validated before identity extraction.
Direct-reference pre-resolution still checks out recorded bytes before parsing.

## Historical remaining integration blocker

`PlatformAppModelTests.WorksPreviewComposesItsPreservedGraphWithoutDeclarationServers`
reaches an existing production integration failure:

```
Aspire.Hosting.DistributedApplicationException: Cannot add resource of type
'Aspire.Hosting.ApplicationModel.ParameterResource' with name
'eventstore-app-api-token' because resource of type
'Aspire.Hosting.ApplicationModel.ParameterResource' with that name already exists.
Resource names are case-insensitive.
```

`Hexalith.EventStore.Aspire/HexalithEventStoreExtensions.cs:275` already generates
the gateway token, and `WorksPreviewModel.cs:244` generates it again unconditionally.
Owner: Platform Works composition maintainer / EventStore Aspire integration
maintainer. Production Works composition is outside this spec's one-resolver
footprint. Its assertion was neither weakened nor skipped.

The initial focused run is retained in
[composition-focused-initial.txt](composition-focused-initial.txt): 138 total,
4 failures and 3 opt-in live skips. Three fixture migration failures (signed
component directory and two registry diagnostic paths) were corrected before the
final verification. The remaining Works failure is tracked above.


## Historical full-suite blockers and opt-in skips

| Failing row(s) | Exact observed failure | Accountable owning area |
| --- | --- | --- |
| `GatewaySeedSubscriptionTests.PlatformSeedHonorsConfiguredAndDeclarativeSubscriptionsAsync(declarative: false)` | `System.IO.DirectoryNotFoundException: /home/administrator/projects/hexalith/platform/Hexalith.Platform.EventStoreHost/`; inferred content root does not exist; `Program.cs:15` via `GatewaySeedSubscriptionTests.cs:59` | Platform EventStore seed/test-host integration maintainer |
| `DomainEnrollmentKitTests.AcceptedToolPinEqualsSupportedRangeFloorAsync` | Recorded Builds gitlink `2d653b611c8efcffb4d27bd8715d5dbf24eaebfd` differs from release-tag commit `4248b8c70cb35ba79fdd7e4dd63049d4c73211a0`; assertion: "The Platform gitlink must identify the exact pinned published Builds release." | Platform/Builds release coordination maintainers |
| `DomainEnrollmentKitTests.ReleasedPinnedToolValidatesDisposableWorkspaceWithoutWrites(format: "human")` and `(format: "json")` | Restored tool metadata commit `4248b8c70cb35ba79fdd7e4dd63049d4c73211a0` differs from recorded Builds gitlink `2d653b611c8efcffb4d27bd8715d5dbf24eaebfd` at `DomainEnrollmentKitTests.cs:116` | Platform/Builds release coordination maintainers |
| `PlatformAppModelTests.WorksPreviewComposesItsPreservedGraphWithoutDeclarationServers` | Duplicate `ParameterResource` named `eventstore-app-api-token`, detailed above | Platform Works / EventStore Aspire integration maintainers |

Seven live rows remain opt-in and were not executed:

- Four `BuildsSourceStartupTests` rows: `TechnicalEventStoreStartsThroughTheRealBuildsEngineAsync`, `DomainTenantsStartsThroughTheRealBuildsEngineAsync`, `TechnicalMcpCliStartsThroughTheRealBuildsEngineAsync`, `TechnicalMemoriesStartsThroughTheRealBuildsEngineAsync`; require `HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE=1` and `HEXALITH_DAPR_HOME`.
- `LiveLocalDaprEnforcesInvocationAndDeclarativeDeadLetterDeliveryAsync` and `LiveTimesheetsDaprAllowsEstablishedCallerAndDeniesUnlistedCallerAsync`; require the same Builds live-smoke opt-in and Dapr home.
- `ReferenceServersReachDeclaredReadinessInTheFileEntryAsync`; requires `HEXALITH_PLATFORM_LIVE_SMOKE=1`.

The release checks and seed/Works integration assertions remain enabled. No
production refusal, pin, or test expectation was relaxed to hide these blockers.

## Historical concurrent source tuple integration

The spec's baseline remains unchanged:

- Platform: `3beff28eede537eb36e84cdb93900af2556202d7`.
- Builds: `781a4b648c2bd5fdd95884387bb2ababc59ffe31`.

During implementation/verification, the user integrated a new tuple:

- Platform intermediate HEAD: `e02fa5bf43ba9c2561dc2da940264de1c9da3242`.
- Recorded and checked-out Builds: `2d653b611c8efcffb4d27bd8715d5dbf24eaebfd`.
- Published/restored `4.35.2` tool/tag metadata observed by the release checks:
  `4248b8c70cb35ba79fdd7e4dd63049d4c73211a0`.

Commands exercised the working source tuple available at their execution time;
full-suite release-pin failures reflect this concurrent integration. Both the
integrated tuple and spec baseline frontmatter were preserved. This agent made no
staging, commit, branch, pin, or submodule-update operation. Earlier successful
synthetic checks do not qualify the newer tuple as a published release.
