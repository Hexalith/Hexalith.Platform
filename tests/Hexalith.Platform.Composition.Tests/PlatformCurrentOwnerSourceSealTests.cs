namespace Hexalith.Platform.Composition.Tests;

using Shouldly;

/// <summary>Uses the actual standalone owning Git root to exercise the final production source seal.</summary>
public sealed class PlatformCurrentOwnerSourceSealTests
{
    /// <summary>Neither unchanged HEAD nor unchanged earlier files can hide changed bodies or additional tracked inputs.</summary>
    /// <param name="change">The actual owning-root input mutation made at the preparation boundary.</param>
    [Theory]
    [InlineData("changed-body")]
    [InlineData("new-tracked-input")]
    [InlineData("committed-new-input")]
    public void PreparedStageRefusesChangedCompleteOwningInputs(string change)
    {
        using PlatformPartiesProofFixture fixture = new();
        string root = fixture.Options.Mapping.Root;
        string revision = PlatformPartiesProofFixture.Git(root, "rev-parse", "HEAD");
        Action? previous = PlatformCurrentOwnerProof.PreparedObserver.Value;
        int observed = 0;
        try
        {
            PlatformCurrentOwnerProof.PreparedObserver.Value = () =>
            {
                observed++;
                if (change == "changed-body") File.AppendAllText(Path.Combine(root, "Native.cs"), "// changed after reader preparation");
                else
                {
                    File.WriteAllText(Path.Combine(root, "Later.props"), "<Project />");
                    PlatformPartiesProofFixture.Git(root, "add", "--", "Later.props");
                    if (change == "committed-new-input") PlatformPartiesProofFixture.Commit(root);
                }
            };
            Should.Throw<InvalidDataException>(() => PlatformCurrentOwnerProof.VerifyPreparedRevision(root, revision));
            observed.ShouldBe(1);
            if (change != "committed-new-input") PlatformPartiesProofFixture.Git(root, "rev-parse", "HEAD").ShouldBe(revision);
            fixture.Options.CurrentOwnerProof.ShouldBeNull();
        }
        finally { PlatformCurrentOwnerProof.PreparedObserver.Value = previous; }
    }

    /// <summary>The real final guard permits an unchanged standalone source without creating any private artifact authority.</summary>
    [Fact]
    public void PreparedStagePreservesUnchangedOwningInputs()
    {
        using PlatformPartiesProofFixture fixture = new();
        string root = fixture.Options.Mapping.Root;
        string revision = PlatformPartiesProofFixture.Git(root, "rev-parse", "HEAD");
        PlatformCurrentOwnerProof.VerifyPreparedRevision(root, revision);
        PlatformPartiesProofFixture.Git(root, "rev-parse", "HEAD").ShouldBe(revision);
        PlatformPartiesProofFixture.Git(root, "status", "--porcelain").ShouldBeEmpty();
        fixture.Options.CurrentOwnerProof.ShouldBeNull();
    }
}
