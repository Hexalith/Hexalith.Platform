namespace Hexalith.Platform.Composition.Tests;

using Hexalith.Builds.Tooling.Runtime;
using Shouldly;

/// <summary>Exercises the published AppHost's persisted parent-plan, private-key and validation-projection transport before effects.</summary>
public sealed class PlatformPartiesValidationHandoffTests
{
    /// <summary>The same signed validation transport without a parent claim directory retains its existing image-boundary behavior.</summary>
    [Fact]
    public async Task PublishedValidationWithoutClaimProjectionReachesBoundaryAsync()
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync();
        var result = await fixture.RunPublishedValidationAsync();
        fixture.AssertPublishedValidationBoundary(result);
    }

    /// <summary>The actual signed host must project only claim allocation during validation and reach the immutable image boundary.</summary>
    /// <param name="profile">The actual declared suite.</param>
    [Theory]
    [InlineData("platform-package")]
    [InlineData("lifecycle-package")]
    public async Task PublishedValidationRoundTripPreservesCanonicalClaimBindingAsync(string profile)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(profile);
        await fixture.PersistClaimStateAsync();
        var result = await fixture.RunPublishedValidationAsync();
        fixture.AssertPublishedValidationBoundary(result);
    }

    /// <summary>Ordinary execution retains its original non-null directory through the same trusted entry and full image boundary.</summary>
    /// <param name="profile">The actual declared suite.</param>
    [Theory]
    [InlineData("platform-package")]
    [InlineData("lifecycle-package")]
    public async Task OrdinaryEntryPreservesItsClaimDirectoryAsync(string profile)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(profile);
        await fixture.PersistClaimStateAsync();
        fixture.AssertPositiveEntry();
        fixture.Options.RunPlan!.ResourceClaimStateDirectory.ShouldBe(fixture.Plan.ResourceClaimStateDirectory);
        PlatformPartiesValidationContext.IsCurrentPublishedValidation(fixture.Plan).ShouldBeFalse();
    }

    /// <summary>A projected or substituted directory cannot acquire validation authority in an ordinary caller process.</summary>
    /// <param name="fault">The claim mutation.</param>
    [Theory]
    [InlineData("removed")]
    [InlineData("different")]
    [InlineData("empty")]
    public async Task OrdinaryEntryRefusesClaimSuppressionOrReplacementAsync(string fault)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync();
        await fixture.PersistClaimStateAsync();
        fixture.AssertPositiveEntry();
        var builder = PlatformPartiesPackageFixture.Builder();
        int configured = 0;
        string? replacement = fault switch { "removed" => null, "different" => Path.Combine(fixture.Workspace, "foreign"), "empty" => "", _ => throw new ArgumentOutOfRangeException(nameof(fault)) };
        CompositionRunPlan supplied = fixture.Plan with { ResourceClaimStateDirectory = replacement };
        Should.Throw<InvalidDataException>(() => fixture.Compose(builder, options => { configured++; return options; }, supplied));
        configured.ShouldBe(0);
        fixture.Options.PartiesRunProof!.Matches(fixture.Options with { RunPlan = supplied }).ShouldBeFalse();
        fixture.AssertNoEffects(builder);
    }

    /// <summary>Even in the validation representation, every other canonical field and the original absolute claim directory remain bound.</summary>
    /// <param name="fault">The mutation outside the permitted exact projection.</param>
    [Theory]
    [InlineData("run")]
    [InlineData("attempt")]
    [InlineData("environment")]
    [InlineData("workspace")]
    [InlineData("mapping")]
    [InlineData("selected-test")]
    [InlineData("selected-tests")]
    [InlineData("model")]
    [InlineData("deadline")]
    [InlineData("retention")]
    [InlineData("alternate-claim")]
    [InlineData("canonical-null")]
    [InlineData("canonical-relative")]
    public async Task ValidationProjectionRefusesEveryOtherChangedFieldAsync(string fault)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync();
        await fixture.PersistClaimStateAsync();
        CompositionRunPlan recorded = (await CompositionDocumentStore.TryReadAsync<CompositionRunPlan>(Path.Combine(fixture.Workspace, "plan.json"), TestContext.Current.CancellationToken))!;
        CompositionRunPlan supplied = recorded with { ResourceClaimStateDirectory = null };
        PlatformPartiesValidationContext.IsExactClaimProjection(recorded, supplied).ShouldBeTrue();
        supplied = fault switch
        {
            "run" => supplied with { RunId = CompositionRunPlanFactory.NewRunId() },
            "attempt" => supplied with { AttemptId = CompositionRunPlanFactory.NewRunId() },
            "environment" => supplied with { EnvironmentId = CompositionRunPlanFactory.NewRunId() },
            "workspace" => supplied with { Workspace = Path.Combine(fixture.Workspace, "foreign") },
            "mapping" => supplied with { SourceMapping = supplied.SourceMapping! with { ActiveModule = "Hexalith.Foreign" } },
            "selected-test" => supplied with { SelectedTest = null },
            "selected-tests" => supplied with { SelectedTests = [] },
            "model" => supplied with { PlatformModelAssemblyPath = null },
            "deadline" => supplied with { StartupDeadlineUtc = null },
            "retention" => supplied with { RetainOnFailure = !supplied.RetainOnFailure },
            "alternate-claim" => supplied with { ResourceClaimStateDirectory = Path.Combine(fixture.Workspace, "foreign") },
            "canonical-null" or "canonical-relative" => supplied,
            _ => throw new ArgumentOutOfRangeException(nameof(fault)),
        };
        if (fault == "canonical-null") recorded = recorded with { ResourceClaimStateDirectory = null };
        if (fault == "canonical-relative") recorded = recorded with { ResourceClaimStateDirectory = "relative-state" };
        PlatformPartiesValidationContext.IsExactClaimProjection(recorded, supplied).ShouldBeFalse();
        Directory.Exists(Path.Combine(fixture.Workspace, "platform-components")).ShouldBeFalse();
    }

    /// <summary>The original canonical disk plan is hashed after admission; editing even the projected-away field invalidates its proof.</summary>
    [Fact]
    public async Task ChangedCanonicalClaimFileCannotReplayAnAdmittedProofAsync()
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync();
        await fixture.PersistClaimStateAsync();
        fixture.AssertPositiveEntry();
        await CompositionDocumentStore.WriteAsync(Path.Combine(fixture.Workspace, "plan.json"), fixture.Plan with { ResourceClaimStateDirectory = null }, TestContext.Current.CancellationToken);
        fixture.Options.PartiesRunProof!.Matches(fixture.Options).ShouldBeFalse();
        fixture.AssertNoEffects(PlatformPartiesPackageFixture.Builder());
    }
}
