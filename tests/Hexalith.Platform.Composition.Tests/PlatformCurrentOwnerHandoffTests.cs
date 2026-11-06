namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;

/// <summary>Executes the genuine signed public transport and fresh ordinary current reader without starting any application resources.</summary>
public sealed class PlatformCurrentOwnerHandoffTests
{
    /// <summary>Preflight can enrich only derived consumer cache; a fresh ordinary proof and reader preserve the same signed command inputs.</summary>
    [Fact]
    public async Task PublishedCurrentValidationThenFreshOrdinaryGeneratePreservesMeasuredAuthorityAsync()
    {
        await AssertPublishedCurrentValidationThenFreshOrdinaryGenerateAsync();
    }

    /// <summary>Wrong key encoding and mixed/stale command, entry-body or source bindings refuse through the actual public host.</summary>
    /// <param name="fault">The single persisted command or transport mutation.</param>
    [Theory]
    [InlineData("wrong-key-encoding")]
    [InlineData("mixed-plan")]
    [InlineData("stale-run")]
    [InlineData("entry-body")]
    [InlineData("source-binding")]
    public async Task PublishedCurrentValidationRefusesChangedAuthorityBeforeEffectsAsync(string fault)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile);
        await fixture.PersistClaimStateAsync();
        CompositionRunPlan changed = fixture.Plan;
        if (fault == "mixed-plan") changed = changed with { SelectedTests = [changed.SelectedTest!, changed.SelectedTest! with { Name = "platform-package" }] };
        if (fault == "stale-run") changed = changed with { RunId = CompositionRunPlanFactory.NewRunId() };
        if (fault == "entry-body") changed = changed with { SelectedTest = changed.SelectedTest! with { EntryHash = new string('a', 64) } };
        if (fault == "entry-body") changed = changed with { SelectedTests = [changed.SelectedTest!] };
        if (fault == "source-binding") changed = changed with { SourceMapping = changed.SourceMapping! with
        { PlatformIdentity = changed.SourceMapping!.PlatformIdentity! with { CheckoutCommit = new string('a', 40) } } };
        await CompositionDocumentStore.WriteAsync(Path.Combine(fixture.Workspace, "plan.json"), changed, TestContext.Current.CancellationToken);
        var result = await fixture.RunPublishedValidationAsync(completeCatalog: true, signingKey: fault == "wrong-key-encoding" ? "not-a-key" : null);
        result.ExitCode.ShouldNotBe(0);
        Directory.Exists(Path.Combine(fixture.Workspace, "current-owner")).ShouldBeFalse();
        Directory.Exists(Path.Combine(fixture.Workspace, "platform-components")).ShouldBeFalse();
        Directory.Exists(fixture.Plan.ResourceClaimStateDirectory!).ShouldBeFalse();
        File.Exists(Path.Combine(fixture.Workspace, "validation-probes", "images.txt")).ShouldBeFalse();
        File.Exists(Path.Combine(fixture.Workspace, NativeTestHandoff.EndpointCredentialsFileName)).ShouldBeFalse();
    }

    /// <summary>An ordinary plan has no prior key anchor; a fresh well-formed initial key retains the complete measured handoff.</summary>
    [Fact]
    public async Task PublishedCurrentValidationAcceptsFreshWellFormedInitialKeyWithoutPriorAnchorAsync()
    {
        string initialSigningKey = CompositionSigningKey.Create();
        Convert.FromBase64String(initialSigningKey).Length.ShouldBe(48);
        await AssertPublishedCurrentValidationThenFreshOrdinaryGenerateAsync(initialSigningKey);
    }

    private static async Task AssertPublishedCurrentValidationThenFreshOrdinaryGenerateAsync(string? initialSigningKey = null)
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile);
        if (initialSigningKey is not null) fixture.UsesSigningKey(initialSigningKey).ShouldBeFalse();
        await fixture.PersistClaimStateAsync();
        await CompositionDaprComponentRenderer.WriteAsync(fixture.Plan, TestContext.Current.CancellationToken);
        string mapping = Path.Combine(fixture.Workspace, "source-mapping");
        string snapshot = Path.Combine(mapping, "catalog-versions.json");
        string[] authority = [Path.Combine(fixture.Workspace, "plan.json"), .. Directory.GetFiles(mapping, "*", SearchOption.AllDirectories)];
        var original = authority.ToDictionary(path => path, Hash, StringComparer.Ordinal);
        byte[] cold = File.ReadAllBytes(snapshot);
        var validation = await fixture.RunPublishedValidationAsync(completeCatalog: true, signingKey: initialSigningKey);
        validation.ExitCode.ShouldBe(0);
        byte[] warm = File.ReadAllBytes(snapshot);
        AssertDerivedEnrichment(cold, warm);
        File.ReadAllLines(Path.Combine(fixture.Workspace, "validation-probes", "images.txt")).Order(StringComparer.Ordinal)
            .ShouldBe(PlatformPartiesPackageFixture.CurrentImages.Order(StringComparer.Ordinal));
        string catalogPath = Path.Combine(fixture.Workspace, "platform-components", fixture.Plan.EnvironmentId + "-catalog", "catalog.json");
        byte[] validatedCatalog = File.ReadAllBytes(catalogPath);
        string originalReceiptPath = Path.Combine(Path.GetDirectoryName(catalogPath)!, "receipt.json");
        byte[] originalReceipt = File.ReadAllBytes(originalReceiptPath);
        Directory.Exists(fixture.Plan.ResourceClaimStateDirectory!).ShouldBeFalse();
        var builder = PlatformPartiesPackageFixture.Builder();
        Should.Throw<InvalidOperationException>(() => fixture.Compose(builder, options =>
        {
            PlatformCurrentOwnerProof proof = options.CurrentOwnerProof!;
            proof.Matches(options).ShouldBeTrue();
            options.PartiesRunProof.ShouldBeNull();
            options.RunPlan!.ResourceClaimStateDirectory.ShouldBe(fixture.Plan.ResourceClaimStateDirectory);
            PlatformAppModel.MaterializeSourceMapping(options, fixture.Workspace);
            File.ReadAllBytes(snapshot).ShouldBe(warm);
            var catalog = PlatformRoutingCatalog.Generate(options, PlatformAppModel.Prepare(options), Path.Combine(fixture.Workspace, "platform-components"));
            File.ReadAllBytes(catalog.CatalogPath).ShouldBe(validatedCatalog);
            File.ReadAllBytes(originalReceiptPath).ShouldBe(originalReceipt, "Retain the initial catalog receipt separately from the fresh current execution receipt.");
            using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.Directory, "current-reader-receipt.json")));
            proof.VerifyReader(receipt.RootElement);
            TestContext.Current.TestOutputHelper!.WriteLine(JsonSerializer.Serialize(new
            {
                phase = "genuine-signed-current-validation-and-fresh-ordinary-reader", proof.SourceRevision,
                initialKeySource = initialSigningKey is null ? "original-fixture-key" : "fresh-independent-key",
                priorPlanKeyAnchor = false,
                qualificationCredit = false, runtimeBodies = proof.Bodies, packageArchives = proof.PackageArchives,
                loadedArtifacts = receipt.RootElement.GetProperty("loadedArtifacts"),
            }));
            proof.Matches(options).ShouldBeTrue();
            foreach (var input in original.Where(input => input.Key != snapshot)) Hash(input.Key).ShouldBe(input.Value);
            File.ReadAllBytes(snapshot).ShouldBe(warm);
            throw new InvalidOperationException("Expected real signed current validation and ordinary catalog; stop before resources.");
        }, signingKey: initialSigningKey)).Message.ShouldBe("Expected real signed current validation and ordinary catalog; stop before resources.");
        builder.Resources.ShouldBeEmpty();
        Directory.Exists(fixture.Plan.ResourceClaimStateDirectory!).ShouldBeFalse();
        File.Exists(Path.Combine(fixture.Workspace, NativeTestHandoff.EndpointCredentialsFileName)).ShouldBeFalse();
    }

    private static void AssertDerivedEnrichment(byte[] cold, byte[] warm)
    {
        const string consumer = "Hexalith.Platform.CatalogBuilder";
        JsonNode original = JsonNode.Parse(cold)!;
        JsonNode derived = JsonNode.Parse(warm)!;
        original["ProjectVersions"]!.AsObject().ContainsKey(consumer).ShouldBeFalse();
        JsonObject versions = derived["ProjectVersions"]!.AsObject();
        versions.Count.ShouldBe(original["ProjectVersions"]!.AsObject().Count + 1);
        versions[consumer]!["Hexalith.EventStore.Server"]!.GetValue<string>().ShouldBe("4.1.0");
        versions[consumer]!["Hexalith.Tenants.Contracts"]!.GetValue<string>().ShouldBe("7.0.4");
        versions[consumer]!["Hexalith.Memories.Contracts"]!.GetValue<string>().ShouldBe("3.0.25");
        CatalogVersionSnapshot snapshot = JsonSerializer.Deserialize<CatalogVersionSnapshot>(warm)!;
        snapshot.Hash.ShouldBe(CatalogVersionSnapshot.ComputeHash(snapshot.MappingHash, snapshot.CatalogCommit,
            snapshot.CatalogFileHash, snapshot.PinnedFiles, snapshot.Versions, snapshot.ProjectVersions));
        versions.Remove(consumer).ShouldBeTrue();
        derived["Hash"] = original["Hash"]!.DeepClone();
        JsonNode.DeepEquals(original, derived).ShouldBeTrue();
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
