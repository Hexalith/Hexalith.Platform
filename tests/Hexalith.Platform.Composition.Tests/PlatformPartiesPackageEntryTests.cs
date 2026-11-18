namespace Hexalith.Platform.Composition.Tests;

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;
using Shouldly;

/// <summary>Authenticated parent/runner entry refusals before composition effects; grants no image admission.</summary>
public sealed class PlatformPartiesPackageEntryTests
{
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RegistrySelectedImageBindsTheLoadedManifestAndImageId(bool index)
    {
        var server = DependencyImage();
        var evidence = PlatformPartiesPackageFixture.ImageEvidence(server.Image!, index);
        List<string> requested = [];
        var options = ImageOptions((start, _, _) =>
        {
            requested.Add(start.ArgumentList[3]);
            return Task.FromResult(new CompositionProcessResult(true, 0, start.ArgumentList[0] == "buildx" ? evidence.Tag : evidence.Manifest, false));
        });
        var bound = PlatformReleasedImages.Resolve(options, server);
        requested.ShouldBe([server.Image!, evidence.Reference]);
        bound.ImmutableImage.ShouldBe(evidence.Reference);
        PlatformReleasedImages.MatchesLoadedImage(bound, evidence.Reference, evidence.ImageId).ShouldBeTrue();
        PlatformReleasedImages.MatchesLoadedImage(bound, evidence.Reference, "sha256:" + bound.ImageDigest).ShouldBeTrue();
        PlatformReleasedImages.MatchesLoadedImage(bound, server.Image!, evidence.ImageId).ShouldBeFalse();
        PlatformReleasedImages.MatchesLoadedImage(bound, evidence.Reference, "sha256:" + new string('0', 64)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("unavailable-tag")]
    [InlineData("invalid-digest")]
    [InlineData("unavailable-manifest")]
    [InlineData("wrong-repository")]
    [InlineData("substituted-manifest")]
    public void InvalidRegistryEvidenceNamesTheDependency(string fault)
    {
        var server = DependencyImage();
        var evidence = PlatformPartiesPackageFixture.ImageEvidence(server.Image!, index: true);
        var options = ImageOptions((start, _, _) =>
        {
            bool tag = start.ArgumentList[0] == "buildx";
            string output = tag ? evidence.Tag : evidence.Manifest;
            bool unavailable = fault == (tag ? "unavailable-tag" : "unavailable-manifest");
            if (tag && fault == "invalid-digest") output = "{\"digest\":\"sha256:not-hex\"}";
            if (!tag && fault == "wrong-repository") output = output.Replace("registry.hexalith.com", "foreign.example", StringComparison.Ordinal);
            if (!tag && fault == "substituted-manifest")
            {
                JsonObject json = JsonNode.Parse(output)!.AsObject();
                json["Raw"] = Convert.ToBase64String("{}"u8);
                output = json.ToJsonString();
            }
            return Task.FromResult(new CompositionProcessResult(true, unavailable ? 1 : 0, output, false));
        });
        Should.Throw<InvalidDataException>(() => PlatformReleasedImages.Resolve(options, server)).Message.ShouldContain(server.Image!);
    }

    private static PlatformServerBinding DependencyImage()
        => new(new PlatformModuleDeclaration("tenants.json", JsonSerializer.SerializeToElement(new { identity = new { moduleId = "tenants" } })),
            "domain", "tenants", "tenants", null, "registry.hexalith.com/tenants:7.0.7", "/ready", new Dictionary<string, string>(), []);

    private static PlatformCompositionOptions ImageOptions(Func<System.Diagnostics.ProcessStartInfo, TimeSpan, CancellationToken, Task<CompositionProcessResult>> probe)
        => new()
        {
            Mapping = new(Hexalith.Builds.Tooling.Workspace.WorkspaceMode.Package, PlatformTestRepository.Root, "Hexalith.Parties", [], false),
            PlatformRoot = PlatformTestRepository.Root,
            ImageProbeRunner = probe,
        };

}
