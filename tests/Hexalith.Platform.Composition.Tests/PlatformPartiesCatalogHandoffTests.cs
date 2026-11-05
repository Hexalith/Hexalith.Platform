namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Exercises the actual signed validation and ordinary catalog phases without building or running resources.</summary>
public sealed class PlatformPartiesCatalogHandoffTests
{
    /// <summary>The private reader must not change the command authority across validation, materialization and ordinary generation.</summary>
    /// <param name="releasedMaterialization">Whether to also exercise the released 1.4.0 unconditional materialization call.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishedPreflightAndOrdinaryCatalogKeepCommandInputsImmutableAsync(bool releasedMaterialization)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync();
        await fixture.PersistClaimStateAsync();
        string snapshot = Path.Combine(fixture.Workspace, "source-mapping", "catalog-versions.json");
        string[] inputs = [Path.Combine(fixture.Workspace, "plan.json"), .. Directory.GetFiles(
            Path.Combine(fixture.Workspace, "source-mapping"), "*", SearchOption.AllDirectories)];
        Dictionary<string, string> original = inputs.ToDictionary(path => path, Hash, StringComparer.Ordinal);
        Observe("command-cold", snapshot, null);

        // The genuine AppHost's --validate-platform path returns before builder.Build/Run.
        // Its only image executable is the existing exact, registry-free fixture probe.
        var validation = await fixture.RunPublishedValidationAsync(completeCatalog: true);
        Observe("published-preflight", snapshot, null);
        validation.ExitCode.ShouldBe(0);
        File.ReadAllLines(Path.Combine(fixture.Workspace, "validation-probes", "images.txt"))
            .Order(StringComparer.Ordinal).ShouldBe(PlatformPartiesPackageFixture.Images.Order(StringComparer.Ordinal));
        Directory.Exists(fixture.Plan.ResourceClaimStateDirectory!).ShouldBeFalse();

        var builder = PlatformPartiesPackageFixture.Builder();
        Exception outcome = Record.Exception(() => fixture.Compose(builder, options =>
        {
            options.PartiesRunProof!.Matches(options).ShouldBeTrue();
            Observe("ordinary-proof", snapshot, true);
            if (releasedMaterialization)
                SourceMappingMaterializer.WriteAsync(options.Mapping, fixture.Workspace, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
            else
                PlatformAppModel.MaterializeSourceMapping(options, fixture.Workspace);
            Observe("ordinary-materialized", snapshot, options.PartiesRunProof.Matches(options));
            PlatformRoutingCatalog? catalog = null;
            Exception? generation = Record.Exception(() => catalog = PlatformRoutingCatalog.Generate(options,
                PlatformAppModel.Prepare(options), Path.Combine(fixture.Workspace, "platform-components")));
            TestContext.Current.TestOutputHelper!.WriteLine("ordinaryGenerateExceptionType=" + (generation?.GetType().Name ?? "none"));
            generation.ShouldBeNull("The ordinary catalog must use the same admitted private parent input bytes after the actual signed preflight.");
            catalog.ShouldNotBeNull();
            options.PartiesRunProof.Matches(options).ShouldBeTrue();
            foreach (KeyValuePair<string, string> input in original)
                Hash(input.Key).ShouldBe(input.Value, "Command authority changed: " + Path.GetRelativePath(fixture.Workspace, input.Key));
            throw new InvalidOperationException("Expected ordinary catalog success; stop before composition effects.");
        })).ShouldNotBeNull();
        outcome.Message.ShouldBe("Expected ordinary catalog success; stop before composition effects.");
        builder.Resources.ShouldBeEmpty();
        Directory.Exists(fixture.Plan.ResourceClaimStateDirectory!).ShouldBeFalse();
    }

    private static void Observe(string phase, string snapshot, bool? proofMatches)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(snapshot));
        TestContext.Current.TestOutputHelper!.WriteLine(JsonSerializer.Serialize(new
        {
            phase,
            snapshotSha256 = Hash(snapshot),
            readerConsumerCached = document.RootElement.GetProperty("ProjectVersions").TryGetProperty("Hexalith.Platform.CatalogBuilder", out _),
            proofMatches,
        }));
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
