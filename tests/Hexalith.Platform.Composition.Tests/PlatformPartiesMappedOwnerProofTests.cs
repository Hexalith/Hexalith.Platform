namespace Hexalith.Platform.Composition.Tests;

using Shouldly;

/// <summary>Tests the Platform-owned outer-root/direct-owner mapping with fixture-owned local Git histories; no public cohort, publication or CI acceptance.</summary>
/// <remarks>The retired fact that pinned an archived Parties gitlink stays retired; these facts own their complete histories.</remarks>
public sealed class PlatformPartiesMappedOwnerProofTests
{
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
