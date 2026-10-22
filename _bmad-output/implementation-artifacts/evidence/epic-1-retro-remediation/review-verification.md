# Review-fix narrow verification

Executed sequentially from `/home/administrator/projects/hexalith/platform` after the review fixes on 2026-10-22. No full test class or suite was rerun in this pass. Earlier class/full-suite counts in [README.md](README.md) are historical and predate these fixes; the parent agent will perform the current-source full matrix audit.

The fixes preserve committed Platform workspace recognition for embedded single-owner declarations, correct the independent production package mapping, and independently exercise trusted hash/commit/catalog-version refusal. The duplicate-package regression now uses a real Git resolver mapping and a private package cache; diagnostic source assertions normalize relative paths. Mechanical async I/O and formatting fixes satisfy the current analyzers.

## Exact commands and results

| Command | Result | Output |
| --- | --- | --- |
| `dotnet build references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/Hexalith.Builds.Module.Tests.csproj -c Debug -m:1` | Exit 0; zero warnings/errors | [review-builds-build.txt](review-builds-build.txt) |
| `dotnet references/Hexalith.Builds/test/Hexalith.Builds.Module.Tests/bin/Debug/net10.0/Hexalith.Builds.Module.Tests.dll -method '*PlatformOwnedSingleModuleDeclarationPreservesWorkspaceIdentityAsync' -method '*StandaloneV2UsesValidatedOwnerIdentityAsync' -method '*StandaloneWithoutUniqueV2OwnerKeepsDirectoryIdentityAsync' -method '*InvalidOrCollidingStandaloneV2RefusesBeforeCheckoutAsync' -method '*NonGitStandaloneV2UsesDeclaredIdentityAsync' -method '*ResolvedStandaloneIdentityRejectsPackageCopiesAsync' -result-xml _bmad-output/implementation-artifacts/evidence/epic-1-retro-remediation/review-resolver-tests.xml` | Exit 0; 22 passed, zero failures/errors/skips | [review-resolver-tests.txt](review-resolver-tests.txt) |
| `dotnet build tests/Hexalith.Platform.Composition.Tests/Hexalith.Platform.Composition.Tests.csproj -c Debug -m:1` | Exit 0; zero warnings/errors | [review-composition-build.txt](review-composition-build.txt) |
| `dotnet tests/Hexalith.Platform.Composition.Tests/bin/Debug/net10.0/Hexalith.Platform.Composition.Tests.dll -method '*PackageModeRefusesUnreleasedOwnerSnapshotBeforeAddingResources' -method '*PackageSnapshotRejectsChangedBytesAndRepositoryBeforeResources' -method '*PackageSnapshotRejectsMismatchedTrustedReleaseValuesBeforeResources' -result-xml _bmad-output/implementation-artifacts/evidence/epic-1-retro-remediation/review-provenance-tests.xml` | Exit 0; 5 passed, zero failures/errors/skips | [review-provenance-tests.txt](review-provenance-tests.txt) |

## Named final cases

The XML reports retain each executed case and its outcome. These remain local synthetic regression results, without published-release or real-owner/live acceptance.

[review-resolver-tests.xml](review-resolver-tests.xml): 22 passed cases.

- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.StandaloneWithoutUniqueV2OwnerKeepsDirectoryIdentityAsync(mode: Source, multiple: False)` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.StandaloneWithoutUniqueV2OwnerKeepsDirectoryIdentityAsync(mode: Package, multiple: False)` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.StandaloneWithoutUniqueV2OwnerKeepsDirectoryIdentityAsync(mode: Source, multiple: True)` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.StandaloneWithoutUniqueV2OwnerKeepsDirectoryIdentityAsync(mode: Package, multiple: True)` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.PlatformOwnedSingleModuleDeclarationPreservesWorkspaceIdentityAsync(mode: Source)` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.PlatformOwnedSingleModuleDeclarationPreservesWorkspaceIdentityAsync(mode: Package)` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.NonGitStandaloneV2UsesDeclaredIdentityAsync(name: "Hexalith.Parties")` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.NonGitStandaloneV2UsesDeclaredIdentityAsync(name: "renamed-owner")` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.InvalidOrCollidingStandaloneV2RefusesBeforeCheckoutAsync(mode: Source, collision: False)` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.InvalidOrCollidingStandaloneV2RefusesBeforeCheckoutAsync(mode: Package, collision: False)` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.InvalidOrCollidingStandaloneV2RefusesBeforeCheckoutAsync(mode: Source, collision: True)` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.InvalidOrCollidingStandaloneV2RefusesBeforeCheckoutAsync(mode: Package, collision: True)` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.StandaloneV2UsesValidatedOwnerIdentityAsync(mode: Source, name: "Hexalith.Parties")` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.StandaloneV2UsesValidatedOwnerIdentityAsync(mode: Package, name: "Hexalith.Parties")` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.StandaloneV2UsesValidatedOwnerIdentityAsync(mode: Source, name: "renamed-owner")` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.StandaloneV2UsesValidatedOwnerIdentityAsync(mode: Package, name: "renamed-owner")` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.StandaloneV2UsesValidatedOwnerIdentityAsync(mode: Source, name: "Hexalith")` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceRootResolverTests.StandaloneV2UsesValidatedOwnerIdentityAsync(mode: Package, name: "Hexalith")` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceMsBuildTests.ResolvedStandaloneIdentityRejectsPackageCopiesAsync(name: "renamed-owner", transitive: False)` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceMsBuildTests.ResolvedStandaloneIdentityRejectsPackageCopiesAsync(name: "renamed-owner", transitive: True)` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceMsBuildTests.ResolvedStandaloneIdentityRejectsPackageCopiesAsync(name: "Hexalith", transitive: False)` — Pass
- `Hexalith.Builds.ModuleTool.Tests.Workspace.WorkspaceMsBuildTests.ResolvedStandaloneIdentityRejectsPackageCopiesAsync(name: "Hexalith", transitive: True)` — Pass

[review-provenance-tests.xml](review-provenance-tests.xml): 5 passed cases.

- `Hexalith.Platform.Composition.Tests.PlatformAppModelTests.PackageSnapshotRejectsMismatchedTrustedReleaseValuesBeforeResources(value: "hash")` — Pass
- `Hexalith.Platform.Composition.Tests.PlatformAppModelTests.PackageSnapshotRejectsMismatchedTrustedReleaseValuesBeforeResources(value: "commit")` — Pass
- `Hexalith.Platform.Composition.Tests.PlatformAppModelTests.PackageSnapshotRejectsMismatchedTrustedReleaseValuesBeforeResources(value: "catalog-version")` — Pass
- `Hexalith.Platform.Composition.Tests.PlatformAppModelTests.PackageModeRefusesUnreleasedOwnerSnapshotBeforeAddingResources` — Pass
- `Hexalith.Platform.Composition.Tests.PlatformAppModelTests.PackageSnapshotRejectsChangedBytesAndRepositoryBeforeResources` — Pass

