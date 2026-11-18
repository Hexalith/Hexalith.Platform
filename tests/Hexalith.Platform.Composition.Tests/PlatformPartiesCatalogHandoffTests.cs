namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Exercises measured current preparation and ordinary catalog phases without composing resources.</summary>
public sealed class PlatformPartiesCatalogHandoffTests
{
    /// <summary>Only genuine derived consumer cache enrichment is permitted; ordinary execution preserves it and old rematerialization refuses.</summary>
    /// <param name="releasedMaterialization">Whether to exercise unconditional rematerialization instead of sealed-snapshot reuse.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishedPreflightAndOrdinaryCatalogRespectVerifiedDerivationAsync(bool releasedMaterialization)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile);
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

        // The genuine current entry and production reader prepare the consumer cache before ordinary entry.
        var measured = fixture.CaptureCurrent();
        var preflight = fixture.Generate();
        byte[] warm = File.ReadAllBytes(snapshot);
        await AssertDerivedConsumerEnrichmentAsync(cold, warm, snapshot, fixture.Seed);
        string preflightCatalog = preflight.CatalogPath;
        byte[] preflightBytes = File.ReadAllBytes(preflightCatalog);
        measured.CurrentOwnerProof!.Matches(measured).ShouldBeTrue();
        Directory.Exists(fixture.Plan.ResourceClaimStateDirectory!).ShouldBeFalse();

        var builder = PlatformPartiesPackageFixture.Builder();
        Exception outcome = Record.Exception(() => fixture.Compose(builder, options =>
        {
            options.CurrentOwnerProof!.Matches(options).ShouldBeTrue();
            Observe("ordinary-proof", snapshot, true);
            if (releasedMaterialization)
                SourceMappingMaterializer.WriteAsync(options.Mapping, fixture.Workspace, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
            else
                PlatformAppModel.MaterializeSourceMapping(options, fixture.Workspace);
            Observe("ordinary-materialized", snapshot, options.CurrentOwnerProof.Matches(options));
            File.ReadAllBytes(snapshot).ShouldBe(releasedMaterialization ? cold : warm);
            options.CurrentOwnerProof.Matches(options).ShouldBe(!releasedMaterialization);
            PlatformRoutingCatalog? catalog = null;
            Exception? generation = Record.Exception(() => catalog = PlatformRoutingCatalog.Generate(options,
                PlatformAppModel.Prepare(options), Path.Combine(fixture.Workspace, "platform-components")));
            TestContext.Current.TestOutputHelper!.WriteLine("ordinaryGenerateExceptionType=" + (generation?.GetType().Name ?? "none"));
            if (releasedMaterialization)
            {
                generation.ShouldBeOfType<InvalidDataException>().Message.ShouldContain("unchanged authenticated runner proof");
                catalog.ShouldBeNull();
                File.ReadAllBytes(preflightCatalog).ShouldBe(preflightBytes);
            }
            else
            {
                generation.ShouldBeNull("The ordinary catalog must retain the genuine preflight-derived consumer cache and its fresh proof.");
                catalog.ShouldNotBeNull();
                File.ReadAllBytes(catalog.CatalogPath).ShouldBe(preflightBytes);
                options.CurrentOwnerProof.Matches(options).ShouldBeTrue();
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

    /// <summary>Only genuine consumer-cache additions and their derived hash may change; warm entries and all other JSON remain immutable.</summary>
    internal static async Task AssertDerivedConsumerEnrichmentAsync(byte[] before, byte[] after, string snapshotPath, string seed)
    {
        JsonNode original = JsonNode.Parse(before)!;
        JsonNode derived = JsonNode.Parse(after)!;
        foreach (byte[] bytes in new[] { before, after })
        {
            CatalogVersionSnapshot snapshot = JsonSerializer.Deserialize<CatalogVersionSnapshot>(bytes)!;
            snapshot.Hash.ShouldBe(CatalogVersionSnapshot.ComputeHash(snapshot.MappingHash, snapshot.CatalogCommit,
                snapshot.CatalogFileHash, snapshot.PinnedFiles, snapshot.Versions, snapshot.ProjectVersions));
        }
        JsonObject originalVersions = original["ProjectVersions"]!.AsObject();
        JsonObject versions = derived["ProjectVersions"]!.AsObject();
        var allowed = new HashSet<string>(["Hexalith.Platform.CatalogBuilder", Path.GetFileNameWithoutExtension(seed)], StringComparer.Ordinal);
        foreach (string consumer in allowed.Where(versions.ContainsKey))
        {
            string directory = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(snapshotPath))!, "cache-assertion-" + Guid.NewGuid().ToString("N"));
            CompositionWorkspace.CreatePrivateDirectory(directory);
            try
            {
                string project = Path.Combine(directory, consumer + ".proj");
                string catalog = Path.Combine(Path.GetDirectoryName(snapshotPath)!, "pinned-tree", "Props", "Directory.Packages.props");
                string selected = Path.Combine(directory, "versions.txt");
                new XDocument(new XElement("Project", new XElement("Import", new XAttribute("Project", catalog)),
                    new XElement("Target", new XAttribute("Name", "RecordConsumerVersions"),
                        new XElement("WriteLinesToFile", new XAttribute("File", selected),
                            new XAttribute("Lines", "@(PackageVersion->'%(Identity)=%(Version)')"), new XAttribute("Overwrite", "true"))))).Save(project);
                var evaluated = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
                    ["msbuild", project, "-nologo", "-noAutoResponse", "-target:RecordConsumerVersions", "-verbosity:quiet"], directory,
                    new Dictionary<string, string> { ["DOTNET_CLI_USE_MSBUILD_SERVER"] = "false", ["MSBUILDUSESERVER"] = "0" }),
                    TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken).ConfigureAwait(true);
                evaluated.Started.ShouldBeTrue();
                evaluated.ExitCode.ShouldBe(0, evaluated.Output + evaluated.ErrorOutput);
                evaluated.TimedOut.ShouldBeFalse();
                evaluated.OutputTruncated.ShouldBeFalse();
                string[] lines = File.ReadAllLines(selected);
                lines.Length.ShouldBeGreaterThan(0);
                var expected = lines.Select(line => line.Split('=', 2)).ToDictionary(pair => pair[0], pair => pair[1]);
                JsonNode.DeepEquals(versions[consumer], JsonSerializer.SerializeToNode(expected)).ShouldBeTrue(
                    "The consumer cache must equal an independent evaluation of the unchanged pinned catalog: " + consumer);
            }
            finally { Directory.Delete(directory, recursive: true); }
        }
        foreach (string added in versions.Select(pair => pair.Key).Where(key => !originalVersions.ContainsKey(key)).ToArray())
        {
            allowed.ShouldContain(added);
            versions.Remove(added).ShouldBeTrue();
        }
        derived["Hash"] = original["Hash"]!.DeepClone();
        JsonNode.DeepEquals(original, derived).ShouldBeTrue("Only genuine added consumer entries and the correctly recomputed hash may change; existing warm entries remain unchanged.");
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
