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
    [InlineData("malformed-manifest")]
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
            if (!tag && fault == "malformed-manifest") output = "{";
            if (!tag && fault == "wrong-repository") output = output.Replace("registry.hexalith.com", "foreign.example", StringComparison.Ordinal);
            if (!tag && fault == "substituted-manifest")
            {
                JsonObject json = JsonNode.Parse(output)!.AsObject();
                json["Raw"] = Convert.ToBase64String("{}"u8);
                output = json.ToJsonString();
            }
            return Task.FromResult(new CompositionProcessResult(true, unavailable ? 1 : 0, output, false));
        });
        string attempted = fault is "unavailable-tag" or "invalid-digest" ? server.Image! : evidence.Reference;
        Should.Throw<InvalidDataException>(() => PlatformReleasedImages.Resolve(options, server)).Message.ShouldContain(attempted);
    }

    /// <summary>The prepared run records the loaded container of each resolved dependency, or names the substituted one.</summary>
    /// <param name="fault">The loaded container observation to substitute, if any.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("manifest-image-id")]
    [InlineData("foreign-image-id")]
    [InlineData("foreign-reference")]
    [InlineData("stopped")]
    [InlineData("ambiguous")]
    public async Task PreparedRunRecordsTheLoadedDependencyIdentityAsync(string? fault)
    {
        var server = DependencyImage();
        var evidence = PlatformPartiesPackageFixture.ImageEvidence(server.Image!, index: true);
        var resolved = PlatformReleasedImages.Resolve(ImageOptions((start, _, _) => Task.FromResult(new CompositionProcessResult(true, 0,
            start.ArgumentList[0] == "buildx" ? evidence.Tag : evidence.Manifest, false))), server);
        string directory = Directory.CreateTempSubdirectory("platform-released-images-").FullName;
        try
        {
            CompositionRunPlan plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), directory, directory, CompositionPortAllocator.Allocate(1), [], [],
                platformDeclarationPaths: [Path.Combine(directory, "manifest.json")]);
            string container = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData("tenants-container"u8));
            string imageId = fault switch
            {
                "manifest-image-id" => "sha256:" + resolved.ImageDigest,
                "foreign-image-id" => "sha256:" + new string('0', 64),
                _ => evidence.ImageId,
            };
            string reference = fault == "foreign-reference" ? server.Image! : resolved.ImmutableImage!;
            List<string[]> calls = [];
            var options = ImageOptions((start, _, _) =>
            {
                string[] arguments = [.. start.ArgumentList];
                calls.Add(arguments);
                string output = arguments[0] switch
                {
                    "ps" => fault == "ambiguous" ? container + "\n" + new string('b', 64) : container,
                    "container" => $"{container} {imageId} {reference} {(fault == "stopped" ? "false" : "true")}",
                    _ => throw new InvalidOperationException("Unexpected docker command."),
                };
                return Task.FromResult(new CompositionProcessResult(true, 0, output, false));
            }) with { RunPlan = plan };
            var preparation = new PlatformReleasedImages(options, [resolved], directory);
            string receipt = Path.Combine(directory, "released-images.json");
            if (fault is not (null or "manifest-image-id"))
            {
                (await Should.ThrowAsync<InvalidDataException>(() => preparation.PrepareAsync(null!, TestContext.Current.CancellationToken)))
                    .Message.ShouldContain(resolved.ResourceId);
                File.Exists(receipt).ShouldBeFalse();
                return;
            }
            await preparation.PrepareAsync(null!, TestContext.Current.CancellationToken);
            // The run label and the resource label the composition applies select exactly this dependency's container.
            calls[0].ShouldContain($"label={CompositionEnvironment.ContainerRunLabel}={plan.RunId}");
            calls[0].ShouldContain($"label={PlatformReleasedImages.ResourceLabel}={resolved.ResourceId}");
            using JsonDocument written = JsonDocument.Parse(File.ReadAllBytes(receipt));
            JsonElement root = written.RootElement;
            root.GetProperty("runId").GetString().ShouldBe(plan.RunId);
            root.GetProperty("attemptId").GetString().ShouldBe(plan.AttemptId);
            root.GetProperty("environmentId").GetString().ShouldBe(plan.EnvironmentId);
            root.GetProperty("mappingHash").GetString().ShouldBe(options.Mapping.ContentHash);
            root.GetProperty("qualificationCredit").GetBoolean().ShouldBeFalse();
            JsonElement image = root.GetProperty("images").EnumerateArray().Single();
            image.GetProperty("resourceId").GetString().ShouldBe(resolved.ResourceId);
            image.GetProperty("catalogImage").GetString().ShouldBe(server.Image);
            image.GetProperty("expectedReference").GetString().ShouldBe(evidence.Reference);
            image.GetProperty("expectedManifestDigest").GetString().ShouldBe("sha256:" + resolved.ImageDigest);
            image.GetProperty("expectedConfigDigest").GetString().ShouldBe(evidence.ImageId);
            image.GetProperty("observedReference").GetString().ShouldBe(reference);
            image.GetProperty("observedImageId").GetString().ShouldBe(imageId);
            image.GetProperty("containerId").GetString().ShouldBe(container);
        }
        finally { Directory.Delete(directory, recursive: true); }
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
