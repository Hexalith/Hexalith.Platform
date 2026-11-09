#if HEXALITH_PLATFORM_EXTENSION_SDK && !HEXALITH_PLATFORM_SOURCE_CATALOG
namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
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
        PlatformRoutingCatalog catalog = fixture.Generate();
        PlatformPackageCatalogQualification.SelectTestCohort(fixture.Options).ShouldBe(PublishedCatalogArtifactCohort.Sdk42EventStorePackageTestName);
        var servers = PlatformAppModel.Prepare(fixture.Options);
        servers.Single(server => server.ResourceId == "eventstore").ProjectPath.ShouldBe(fixture.Seed);
        AssertReader(fixture.Reader, PublishedCatalogArtifactCohort.Sdk42EventStorePackageTestName, 23, 12, catalog.CatalogPath);
    }

    /// <summary>The existing parent proof builds and seals the real source owner and package-only SDK4.2 references.</summary>
    [Fact]
    public async Task CurrentSourceOwnerAndReaderRetainTheirActualSdk42BodiesAsync()
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile, sdk42: true).ConfigureAwait(true);
        PlatformCompositionOptions options = fixture.CaptureCurrent();
        PlatformCurrentOwnerProof proof = options.CurrentOwnerProof!;
        proof.Cohort.ShouldBe(PublishedCatalogArtifactCohort.Sdk42CurrentOwnerName);
        proof.Matches(options).ShouldBeTrue();
        var declarations = PlatformDeclarationSet.Load(options);
        var servers = PlatformPartiesPackageCatalogQualification.Bind(options, declarations, PlatformAppModel.Prepare(options), developmentRun: true);
        servers.Single(server => server.ResourceId == "eventstore").ProjectPath.ShouldBe(fixture.Seed);
        servers.Single(server => server.ResourceId == "parties").ProjectPath.ShouldBe(PlatformCurrentOwnerProof.OwnerProject(options));
        servers.Count(server => server.Image is not null && server.ImageDigest is not null).ShouldBe(6);
        PlatformRoutingCatalog catalog = PlatformRoutingCatalog.Generate(options, servers, Path.Combine(fixture.Workspace, "platform-components"));
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.Directory, "current-reader-receipt.json")));
        proof.VerifyReader(receipt.RootElement);
        proof.Matches(options).ShouldBeTrue();
        foreach (string name in new[] { "Hexalith.Parties.Contracts", "Hexalith.Parties.EventStoreExtensions" })
            Hash(Path.Combine(fixture.Reader, "output", name + ".dll")).ShouldBe(proof.Bodies[name]);
        AssertReader(fixture.Reader, proof.Cohort, 70, 46, catalog.CatalogPath);
        PlatformPackageCatalogQualification.SelectCohort(options, fullParties: true).ShouldBeNull();
    }

    /// <summary>The signed validation-only host and ordinary parent use the same immutable public reader and seed.</summary>
    [Fact]
    public async Task PublishedPartiesValidationAndOrdinaryReaderRetainTheSdk42DefaultAsync()
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(sdk42: true).ConfigureAwait(true);
        await fixture.PersistClaimStateAsync().ConfigureAwait(true);
        await Hexalith.Builds.Tooling.Runtime.CompositionDaprComponentRenderer.WriteAsync(fixture.Plan, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var validation = await fixture.RunPublishedValidationAsync(completeCatalog: true).ConfigureAwait(true);
        validation.ExitCode.ShouldBe(0, validation.Output + validation.ErrorOutput);
        string snapshot = Path.Combine(fixture.Workspace, "source-mapping", "catalog-versions.json");
        byte[] sealedSnapshot = File.ReadAllBytes(snapshot);
        var builder = PlatformPartiesPackageFixture.Builder();
        Should.Throw<InvalidOperationException>(() => fixture.Compose(builder, options =>
        {
            options.PartiesRunProof!.Matches(options).ShouldBeTrue();
            PlatformPackageCatalogQualification.SelectCohort(options, fullParties: true).ShouldBe(PublishedCatalogArtifactCohort.Sdk42PublishedPartiesName);
            var servers = PlatformAppModel.Prepare(options);
            servers.Single(server => server.ResourceId == "eventstore").ProjectPath.ShouldBe(fixture.Seed);
            PlatformRoutingCatalog catalog = PlatformRoutingCatalog.Generate(options, servers, Path.Combine(fixture.Workspace, "platform-components"));
            AssertReader(fixture.Reader, PublishedCatalogArtifactCohort.Sdk42PublishedPartiesName, 70, 46, catalog.CatalogPath);
            File.ReadAllBytes(snapshot).ShouldBe(sealedSnapshot);
            options.PartiesRunProof.Matches(options).ShouldBeTrue();
            throw new InvalidOperationException("Expected SDK4.2 ordinary reader success before resource allocation.");
        })).Message.ShouldBe("Expected SDK4.2 ordinary reader success before resource allocation.");
        builder.Resources.ShouldBeEmpty();
    }

    private static void AssertReader(string reader, string cohort, int routes, int policies, string catalog)
    {
        using JsonDocument request = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(reader, "request.json")));
        request.RootElement.GetProperty("qualificationCohort").GetString().ShouldBe(cohort);
        using JsonDocument content = JsonDocument.Parse(File.ReadAllBytes(catalog));
        foreach (string key in new[] { "routes", "operations", "declarations" }) content.RootElement.GetProperty(key).GetArrayLength().ShouldBe(routes);
        content.RootElement.GetProperty("idempotency").GetArrayLength().ShouldBe(policies);
        foreach (string name in new[] { "Hexalith.EventStore.Contracts", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService", "Hexalith.EventStore.ServiceDefaults" })
            PublishedCatalogArtifactCohort.VerifyCurrentSdkFile(Path.Combine(reader, "output", name + ".dll"), Hash(Path.Combine(reader, "output", name + ".dll")), PublishedCatalogArtifactCohort.Sdk42CurrentOwnerName);
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}

#endif
