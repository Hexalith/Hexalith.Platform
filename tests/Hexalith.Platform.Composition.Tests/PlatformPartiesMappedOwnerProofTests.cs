namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Tests the actual outer-root/active-owner and package-catalog representations using real local Git identities; no public cohort or CI acceptance.</summary>
public sealed class PlatformPartiesMappedOwnerProofTests
{
    /// <summary>The normal producer resolves the actual reviewed direct owner and catalog without a fabricated consumer Platform tag or mapping.</summary>
    [Fact]
    public async Task ActualPlatformOwnedResolutionBindsTheReviewedLifecycleSourceAsync()
    {
        string root = PlatformTestRepository.Root;
        string active = Path.Combine(root, "references", "Hexalith.Parties");
        string manifest = Path.Combine(active, "module", "hexalith.module-manifest.v2.json");
        PlatformPartiesProofFixture.Git(root, "rev-parse", "HEAD:references/Hexalith.Parties").ShouldBe("f1f33f8de655d9aa9307bafba128b0c7ea5c2570");
        PlatformPartiesProofFixture.Git(active, "rev-parse", "HEAD").ShouldBe("f1f33f8de655d9aa9307bafba128b0c7ea5c2570");
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(manifest))).ShouldBe(PlatformPartiesPackageCatalogQualification.ActiveDeclarationHash);
        SourceMapping mapping = await WorkspaceRootResolver.ResolveAsync(manifest, WorkspaceMode.Package, TestContext.Current.CancellationToken);
        mapping.Root.ShouldBe(root);
        mapping.ActiveModule.ShouldBe("Hexalith.Parties");
        mapping.PlatformIdentity!.IsPlatformRepository.ShouldBeTrue();
        mapping.PlatformIdentity.CheckoutCommit.ShouldBe(PlatformPartiesProofFixture.Git(root, "rev-parse", "HEAD"));
        mapping.Entries.Single(entry => entry.Identity == "Hexalith.Parties").Path.ShouldBe(active);
        mapping.Entries.Single(entry => entry.Identity == "Hexalith.Builds").Origin.ShouldBe("package");
        mapping.Entries.Single(entry => entry.Identity == "Hexalith.Builds").Path.ShouldBeNull();
        mapping.PlatformIdentity.CatalogCommit.ShouldBe(PlatformPartiesProofFixture.Git(root, "rev-parse", "HEAD:references/Hexalith.Builds"));
    }

    /// <summary>The proof binds the actual direct Parties owner rather than assuming it equals the outer Platform root.</summary>
    [Fact]
    public void PlatformOwnedMappingBindsItsActiveOwnerAndDirectCatalog()
    {
        using PlatformPartiesProofFixture fixture = new(platformOwned: true);
        var options = fixture.Options;
        options.Mapping.Root.ShouldBe(options.PlatformRoot);
        options.Mapping.Entries.Single(entry => entry.Identity == "Hexalith.Parties").Path.ShouldNotBe(options.Mapping.Root);
        options.Mapping.Entries.Single(entry => entry.Identity == "Hexalith.Builds").Origin.ShouldBe("package");
        options.Mapping.Entries.Single(entry => entry.Identity == "Hexalith.Builds").Path.ShouldBeNull();
        options.PartiesRunProof!.Matches(options).ShouldBeTrue();
    }

    /// <summary>A direct owner revision that no longer matches the actual outer Git tree invalidates the proof.</summary>
    [Fact]
    public void PlatformOwnedMappingRejectsChangedDirectOwnerGitlink()
    {
        using PlatformPartiesProofFixture fixture = new(platformOwned: true);
        string active = fixture.Options.Mapping.Entries.Single(entry => entry.Identity == "Hexalith.Parties").Path!;
        File.AppendAllText(Path.Combine(active, "Native.cs"), "// A different real local source revision.\n");
        PlatformPartiesProofFixture.Commit(active);
        PlatformPartiesProofFixture.Git(active, "status", "--porcelain").ShouldBeEmpty();
        fixture.Options.PartiesRunProof!.Matches(fixture.Options).ShouldBeFalse();
    }
}
