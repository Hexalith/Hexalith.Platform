#if HEXALITH_PLATFORM_EXTENSION_SDK && !HEXALITH_PLATFORM_SOURCE_CATALOG
namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Requires the actual selected signed tool, catalog, seed and owner handoff for the current catalog; no services are started.</summary>
public sealed class PlatformSdk42PackageEntryTests
{
    /// <summary>The real private reader must consume the pinned catalog snapshot and retain its six public bodies.</summary>
    [Fact]
    public async Task PrivateEventStoreReaderUsesTheActualPinnedSdk42CatalogAndSeedAsync()
    {
        using PlatformPackageCatalogReaderFixture fixture = new(currentTest: true, sdk42: true);
        await fixture.MaterializeAsync().ConfigureAwait(true);
        CatalogVersionSnapshot snapshot = JsonSerializer.Deserialize<CatalogVersionSnapshot>(File.ReadAllBytes(Path.Combine(fixture.Plan.Workspace, "source-mapping", "catalog-versions.json")))!;
        snapshot.Hash.ShouldBe(CatalogVersionSnapshot.ComputeHash(snapshot.MappingHash, snapshot.CatalogCommit, snapshot.CatalogFileHash, snapshot.PinnedFiles, snapshot.Versions, snapshot.ProjectVersions));
        snapshot.ProjectVersions["Hexalith.Platform.EventStoreHost"]["Hexalith.EventStore.Server"].ShouldBe(PlatformTestPins.CatalogVersion("EventStore"));
        snapshot.ProjectVersions["Hexalith.Platform.EventStoreHost"]["Hexalith.EventStore.DomainService"].ShouldBe(PlatformTestPins.CatalogVersion("EventStore"));
        PlatformRoutingCatalog catalog = fixture.Generate();
        OwnerPackageLock owners = PlatformPackageCatalogQualification.RequireLockedPackages(fixture.Options);
        owners.Path.ShouldBe(OwnerPackageLock.PathFor(fixture.Options.PlatformRoot));
        var servers = PlatformAppModel.Prepare(fixture.Options);
        servers.Single(server => server.ResourceId == "eventstore").ProjectPath.ShouldBe(fixture.Seed);
        AssertReader(fixture.Reader, owners, new HashSet<string>(), 24, 13, catalog);
    }

    /// <summary>The existing parent proof builds and seals the real source owner and package-only references from the current catalog.</summary>
    [Fact]
    public async Task CurrentSourceOwnerAndReaderRetainTheirActualSdk42BodiesAsync()
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile).ConfigureAwait(true);
        PlatformCompositionOptions options = fixture.CaptureCurrent();
        PlatformCurrentOwnerProof proof = options.CurrentOwnerProof!;
        proof.OwnerPackages.Path.ShouldBe(OwnerPackageLock.PathFor(options.PlatformRoot));
        PlatformCurrentOwnerProof.ObservedSdk.ShouldAllBe(name => proof.OwnerBodies.ContainsKey(name));
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
        AssertReader(fixture.Reader, proof.OwnerPackages, PlatformCurrentOwnerProof.RunBuiltBodies, 71, 47, catalog);
    }

    private static void AssertReader(string reader, OwnerPackageLock owners, IReadOnlySet<string> runBound, int routes, int policies, PlatformRoutingCatalog catalog)
    {
        using JsonDocument request = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(reader, "request.json")));
        request.RootElement.GetProperty("ownerPackageLock").GetString().ShouldBe(owners.Path);
        request.RootElement.GetProperty("packageFolders").GetArrayLength().ShouldBeGreaterThan(0);
        using JsonDocument content = JsonDocument.Parse(File.ReadAllBytes(catalog.CatalogPath));
        foreach (string key in new[] { "routes", "operations", "declarations" }) content.RootElement.GetProperty(key).GetArrayLength().ShouldBe(routes);
        content.RootElement.GetProperty("idempotency").GetArrayLength().ShouldBe(policies);
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.Directory, "receipt.json")));
        var evidence = owners.VerifyReceipt(receipt.RootElement, runBound).ToDictionary(identity => identity.Assembly, StringComparer.Ordinal);
        foreach (string name in PlatformCurrentOwnerProof.ObservedSdk)
        {
            string output = Path.Combine(reader, "output", name + ".dll");
            evidence[name].LoadedPath.ShouldBe(output);
            evidence[name].ExpectedSha256.ShouldBe(Hash(output));
        }
        evidence.Keys.ShouldNotContain("Hexalith.Parties.Contracts", runBound.Count > 0 ? "Run-built Parties bodies keep their run-bound authority." : null);
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}

#endif
