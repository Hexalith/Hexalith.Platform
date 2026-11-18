namespace Hexalith.Platform.Composition.Tests;

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;
using Shouldly;

/// <summary>Exercises authenticated parent/runner entry and exact image preflight before composition effects.</summary>
public sealed class PlatformPartiesPackageEntryTests
{
    /// <summary>The canonical caller reaches all seven immutable image probes through each ordinary fresh profile.</summary>
    [Theory]
    [InlineData("platform-package")]
    [InlineData("lifecycle-package")]
    public async Task CanonicalDeclarationBindsThePublicCohortAsync(string profile)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateFromCandidateAsync(profile);
        var builder = PlatformPartiesPackageFixture.Builder();
        HashSet<string> expected = new(PlatformPartiesPackageFixture.CurrentImages, StringComparer.Ordinal)
        {
            "registry.hexalith.com/parties@sha256:205e374548d3cac33457551f9c6f7b5f213db804ca58ae185332522833fe83c8",
        };
        Should.Throw<InvalidDataException>(() => fixture.Compose(builder, options => options with
        {
            ImageProbeRunner = (start, _, _) =>
            {
                start.ArgumentList.Take(2).ShouldBe(["manifest", "inspect"]);
                expected.Remove(start.ArgumentList[2]).ShouldBeTrue(start.ArgumentList[2]);
                return Task.FromResult(new CompositionProcessResult(true, expected.Count == 0 ? 83 : 0, "{}", false));
            },
        })).Message.ShouldContain("is unavailable from its released registry");
        expected.ShouldBeEmpty();
        fixture.AssertNoEffects(builder);
    }

    /// <summary>Each mutation starts from a genuine parent proof or measured Development entry and refuses before composition effects.</summary>
    /// <param name="fault">The altered authority input.</param>
    [Theory]
    [InlineData("run")]
    [InlineData("attempt")]
    [InlineData("environment")]
    [InlineData("mapping")]
    [InlineData("source")]
    [InlineData("declaration-path")]
    [InlineData("selected-test")]
    [InlineData("expanded-owner")]
    [InlineData("serialized")]
    [InlineData("production")]
    [InlineData("catalog-bytes")]
    public async Task ChangedFullEntryRefusesBeforeEffectsAsync(string fault)
    {
        if (fault == "production")
        {
            using var measured = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile);
            PlatformCompositionOptions actual = measured.CaptureCurrent();
            actual.CurrentOwnerProof!.Matches(actual).ShouldBeTrue();
            var production = PlatformPartiesPackageFixture.Builder("Production");
            int configured = 0;
            Should.Throw<InvalidDataException>(() => measured.Compose(production, options => { configured++; return options; }));
            configured.ShouldBe(0, "Production must refuse at the real runner entry before the configuration callback.");
            measured.AssertNoEffects(production);
            return;
        }
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync();
        fixture.CaptureOrdinary();
        var builder = PlatformPartiesPackageFixture.Builder();
        int probes = 0;
        Should.Throw<InvalidDataException>(() => fixture.Compose(builder, original =>
        {
            if (fault == "catalog-bytes") File.AppendAllText(Path.Combine(fixture.Workspace, "source-mapping", "catalog-versions.json"), " ");
            var plan = original.RunPlan!;
            var options = fault switch
            {
                "run" => original with { RunPlan = plan with { RunId = CompositionRunPlanFactory.NewRunId() } },
                "attempt" => original with { RunPlan = plan with { AttemptId = CompositionRunPlanFactory.NewRunId() } },
                "environment" => original with { EnvironmentId = CompositionRunPlanFactory.NewRunId() },
                "mapping" => original with { Mapping = original.Mapping with { ActiveModule = "Hexalith.Foreign" } },
                "source" => original with { Mapping = original.Mapping with { PlatformIdentity = original.Mapping.PlatformIdentity! with { CheckoutCommit = new string('0', 40) } } },
                "declaration-path" => original with { DeclarationPaths = [] },
                "selected-test" => original with { RunPlan = plan with { SelectedTest = plan.SelectedTest! with { EntryHash = new string('0', 64) } } },
                "expanded-owner" => original with { QualificationOwners = ["tenants"] },
                "serialized" => JsonSerializer.Deserialize<PlatformCompositionOptions>(JsonSerializer.Serialize(original))!,
                "catalog-bytes" => original,
                _ => throw new ArgumentOutOfRangeException(nameof(fault)),
            };
            if (fault == "serialized") options.PartiesRunProof.ShouldBeNull();
            else original.PartiesRunProof!.Matches(options).ShouldBeFalse("The changed parent input itself must lose proof authority before model loading.");
            return options with { ImageProbeRunner = (_, _, _) => { probes++; throw new InvalidOperationException("Unexpected image probe."); } };
        }));
        probes.ShouldBe(0);
        fixture.AssertNoEffects(builder);
    }

}
