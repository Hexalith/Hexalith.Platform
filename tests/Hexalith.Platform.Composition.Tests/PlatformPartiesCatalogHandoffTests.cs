namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Exercises the actual signed validation and ordinary catalog phases without building or running resources.</summary>
public sealed class PlatformPartiesCatalogHandoffTests
{
    /// <summary>Only genuine derived consumer cache enrichment is permitted; ordinary execution preserves it and old rematerialization refuses.</summary>
    /// <param name="releasedMaterialization">Whether to also exercise the released 1.4.0 unconditional materialization call.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishedPreflightAndOrdinaryCatalogRespectVerifiedDerivationAsync(bool releasedMaterialization)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync();
        await fixture.PersistClaimStateAsync();
        await CompositionDaprComponentRenderer.WriteAsync(fixture.Plan, TestContext.Current.CancellationToken);
        string snapshot = Path.Combine(fixture.Workspace, "source-mapping", "catalog-versions.json");
        string[] inputs = [Path.Combine(fixture.Workspace, "plan.json"), .. Directory.GetFiles(
            Path.Combine(fixture.Workspace, "source-mapping"), "*", SearchOption.AllDirectories)];
        Dictionary<string, string> original = inputs.ToDictionary(path => path, Hash, StringComparer.Ordinal);
        byte[] cold = File.ReadAllBytes(snapshot);
        string tree = Path.Combine(fixture.Workspace, "source-mapping", "pinned-tree");
        string[] pinnedFiles = Directory.GetFiles(tree, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToArray();
        Observe("command-cold", snapshot, null);

        // The genuine AppHost's --validate-platform path returns before builder.Build/Run.
        // Its only image executable is the existing exact, registry-free fixture probe.
        var validation = await fixture.RunPublishedValidationAsync(completeCatalog: true);
        Observe("published-preflight", snapshot, null);
        validation.ExitCode.ShouldBe(0);
        byte[] warm = File.ReadAllBytes(snapshot);
        AssertDerivedConsumerEnrichment(cold, warm);
        string preflightCatalog = Path.Combine(fixture.Workspace, "platform-components", fixture.Plan.EnvironmentId + "-catalog", "catalog.json");
        byte[] preflightBytes = File.ReadAllBytes(preflightCatalog);
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
            File.ReadAllBytes(snapshot).ShouldBe(releasedMaterialization ? cold : warm);
            options.PartiesRunProof.Matches(options).ShouldBe(!releasedMaterialization);
            PlatformRoutingCatalog? catalog = null;
            Exception? generation = Record.Exception(() => catalog = PlatformRoutingCatalog.Generate(options,
                PlatformAppModel.Prepare(options), Path.Combine(fixture.Workspace, "platform-components")));
            TestContext.Current.TestOutputHelper!.WriteLine("ordinaryGenerateExceptionType=" + (generation?.GetType().Name ?? "none"));
            if (releasedMaterialization)
            {
                generation.ShouldBeOfType<InvalidDataException>().Message.ShouldBe(
                    "The private package reader requires the unchanged qualified runner proof.");
                catalog.ShouldBeNull();
                File.ReadAllBytes(preflightCatalog).ShouldBe(preflightBytes);
            }
            else
            {
                generation.ShouldBeNull("The ordinary catalog must retain the genuine preflight-derived consumer cache and its fresh proof.");
                catalog.ShouldNotBeNull();
                File.ReadAllBytes(catalog.CatalogPath).ShouldBe(preflightBytes);
                options.PartiesRunProof.Matches(options).ShouldBeTrue();
                File.ReadAllBytes(snapshot).ShouldBe(warm);
            }
            foreach (KeyValuePair<string, string> input in original.Where(input => input.Key != snapshot))
                Hash(input.Key).ShouldBe(input.Value, "Command authority changed: " + Path.GetRelativePath(fixture.Workspace, input.Key));
            Directory.GetFiles(tree, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ShouldBe(pinnedFiles);
            throw new InvalidOperationException(releasedMaterialization
                ? "Expected old materialization refusal; stop before composition effects."
                : "Expected ordinary catalog success; stop before composition effects.");
        })).ShouldNotBeNull();
        outcome.Message.ShouldBe(releasedMaterialization
            ? "Expected old materialization refusal; stop before composition effects."
            : "Expected ordinary catalog success; stop before composition effects.");
        builder.Resources.ShouldBeEmpty();
        Directory.Exists(fixture.Plan.ResourceClaimStateDirectory!).ShouldBeFalse();
    }

    private static void AssertDerivedConsumerEnrichment(byte[] cold, byte[] warm)
    {
        const string consumer = "Hexalith.Platform.CatalogBuilder";
        JsonNode original = JsonNode.Parse(cold)!;
        JsonNode derived = JsonNode.Parse(warm)!;
        original["ProjectVersions"]!.AsObject().ContainsKey(consumer).ShouldBeFalse();
        JsonObject versions = derived["ProjectVersions"]!.AsObject();
        versions.Count.ShouldBe(original["ProjectVersions"]!.AsObject().Count + 1);
        versions[consumer]!["Hexalith.Memories.EventStore"]!.GetValue<string>().ShouldBe("3.0.23");
        versions[consumer]!["Hexalith.EventStore.Server"]!.GetValue<string>().ShouldBe("4.1.0");
        CatalogVersionSnapshot snapshot = JsonSerializer.Deserialize<CatalogVersionSnapshot>(warm)!;
        snapshot.Hash.ShouldBe(CatalogVersionSnapshot.ComputeHash(snapshot.MappingHash, snapshot.CatalogCommit,
            snapshot.CatalogFileHash, snapshot.PinnedFiles, snapshot.Versions, snapshot.ProjectVersions));
        // Compare every original property, entry and binding, including any unrecognized field.
        // This only normalizes an in-memory comparison; no authority file is repaired or rewritten.
        versions.Remove(consumer).ShouldBeTrue();
        derived["Hash"] = original["Hash"]!.DeepClone();
        JsonNode.DeepEquals(original, derived).ShouldBeTrue("Only the actual consumer entry and its derived hash may change.");
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
