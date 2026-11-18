#if HEXALITH_PLATFORM_EXTENSION_SDK && !HEXALITH_PLATFORM_SOURCE_CATALOG
namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Requires the actual selected signed tool, catalog, seed and owner handoff for SDK4.2; no services are started.</summary>
public sealed class PlatformSdk42PackageEntryTests
{
    /// <summary>The real private reader must consume the pinned SDK4.2 snapshot and retain its six public bodies.</summary>
    [Fact]
    public async Task PrivateEventStoreReaderUsesTheActualPinnedSdk42CatalogAndSeedAsync()
    {
        using PlatformPackageCatalogReaderFixture fixture = new(currentTest: true, sdk42: true);
        await fixture.MaterializeAsync().ConfigureAwait(true);
        CatalogVersionSnapshot snapshot = JsonSerializer.Deserialize<CatalogVersionSnapshot>(File.ReadAllBytes(Path.Combine(fixture.Plan.Workspace, "source-mapping", "catalog-versions.json")))!;
        snapshot.Hash.ShouldBe(CatalogVersionSnapshot.ComputeHash(snapshot.MappingHash, snapshot.CatalogCommit, snapshot.CatalogFileHash, snapshot.PinnedFiles, snapshot.Versions, snapshot.ProjectVersions));
        snapshot.ProjectVersions["Hexalith.Platform.EventStoreHost"]["Hexalith.EventStore.Server"].ShouldBe("4.2.0");
        snapshot.ProjectVersions["Hexalith.Platform.EventStoreHost"]["Hexalith.EventStore.DomainService"].ShouldBe("4.2.0");
        PlatformRoutingCatalog catalog = fixture.Generate();
        PlatformPackageCatalogQualification.SelectTestCohort(fixture.Options).ShouldBe(PublishedCatalogArtifactCohort.EventStorePackageTestCurrentName);
        var servers = PlatformAppModel.Prepare(fixture.Options);
        servers.Single(server => server.ResourceId == "eventstore").ProjectPath.ShouldBe(fixture.Seed);
        AssertReader(fixture.Reader, PublishedCatalogArtifactCohort.EventStorePackageTestCurrentName, 23, 12, catalog.CatalogPath);
    }

    /// <summary>The existing parent proof builds and seals the real source owner and package-only SDK4.2 references.</summary>
    [Fact]
    public async Task CurrentSourceOwnerAndReaderRetainTheirActualSdk42BodiesAsync()
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile, sdk42: true).ConfigureAwait(true);
        PlatformCompositionOptions options = fixture.CaptureCurrent();
        PlatformCurrentOwnerProof proof = options.CurrentOwnerProof!;
        proof.Cohort.ShouldBe(PublishedCatalogArtifactCohort.CurrentOwnerMemories327Name);
        proof.Matches(options).ShouldBeTrue();
        var declarations = PlatformDeclarationSet.Load(options);
        var servers = PlatformAppModel.Prepare(options);
        PlatformPartiesPackageFixture.AssertCatalogSelectedDependencies(
            PlatformPartiesPackageCatalogQualification.Bind(options, declarations, servers, developmentRun: true));
        servers.Single(server => server.ResourceId == "eventstore").ProjectPath.ShouldBe(fixture.Seed);
        servers.Single(server => server.ResourceId == "parties").ProjectPath.ShouldBe(PlatformCurrentOwnerProof.OwnerProject(options));
        servers.Count(server => server.Image is not null && server.ImageDigest is not null).ShouldBe(0);
        PlatformRoutingCatalog catalog;
        try
        {
            catalog = PlatformRoutingCatalog.Generate(options, servers, Path.Combine(fixture.Workspace, "platform-components"));
        }
        catch
        {
            fixture.RetainReaderDiagnostics();
            throw;
        }
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.Directory, "current-reader-receipt.json")));
        proof.VerifyReader(receipt.RootElement);
        proof.Matches(options).ShouldBeTrue();
        foreach (string name in new[] { "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions" })
            Hash(Path.Combine(fixture.Reader, "output", name + ".dll")).ShouldBe(proof.Bodies[name]);
        AssertReader(fixture.Reader, proof.Cohort, 70, 46, catalog.CatalogPath);
    }

    private static void AssertReader(string reader, string cohort, int routes, int policies, string catalog)
    {
        using JsonDocument request = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(reader, "request.json")));
        request.RootElement.GetProperty("qualificationCohort").GetString().ShouldBe(cohort);
        using JsonDocument content = JsonDocument.Parse(File.ReadAllBytes(catalog));
        foreach (string key in new[] { "routes", "operations", "declarations" }) content.RootElement.GetProperty(key).GetArrayLength().ShouldBe(routes);
        content.RootElement.GetProperty("idempotency").GetArrayLength().ShouldBe(policies);
        foreach (string name in new[] { "Hexalith.EventStore.Contracts", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService", "Hexalith.EventStore.ServiceDefaults" })
            PublishedCatalogArtifactCohort.VerifyCurrentSdkFile(Path.Combine(reader, "output", name + ".dll"), Hash(Path.Combine(reader, "output", name + ".dll")), PublishedCatalogArtifactCohort.CurrentOwnerMemories327Name);
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}

#endif
