namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Text.Json;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Requires the authentic prepared producer and actual generated private reader; grants no native lifecycle acceptance.</summary>
public sealed class PlatformEventStorePackageCatalogReaderTests
{
    /// <summary>The real reader preserves the selected gateway AppId, authenticates six signed bodies, and retains the measured catalog shape.</summary>
    [Fact]
    public async Task AuthenticatedCurrentReaderRetainsTheSixExactBodiesAndCatalogAsync()
    {
        using PlatformPackageCatalogReaderFixture fixture = new(currentTest: true);
        await fixture.MaterializeAsync().ConfigureAwait(true);
        const string gatewayAppId = "eventstore-query-binding-proof";
        var servers = PlatformAppModel.Prepare(fixture.Options).Select(server => server.ResourceId == "eventstore"
            ? server with { AppId = gatewayAppId } : server).ToArray();
        servers.Where(server => server.ResourceId != "eventstore").ShouldAllBe(server => server.AppId != gatewayAppId);
        PlatformRoutingCatalog catalog = fixture.Generate(servers);
        using JsonDocument request = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Reader, "request.json")));
        request.RootElement.GetProperty("gatewayAppId").GetString().ShouldBe(gatewayAppId);
        OwnerPackageLock owners = OwnerPackageLock.Load(OwnerPackageLock.PathFor(fixture.Options.PlatformRoot));
        request.RootElement.GetProperty("ownerPackageLock").GetString().ShouldBe(owners.Path);
        request.RootElement.GetProperty("owners").GetArrayLength().ShouldBe(1);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(catalog.CatalogPath));
        foreach (string key in new[] { "routes", "operations", "declarations" }) document.RootElement.GetProperty(key).GetArrayLength().ShouldBe(25);
        document.RootElement.GetProperty("idempotency").GetArrayLength().ShouldBe(14);
        catalog.Envelope.Idempotency.ShouldAllBe(policy => policy.ConsumerAppId == gatewayAppId);
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.Directory, "receipt.json")));
        // AR-12: the reader records each owner body against its lock-verified archive member; Composition loads the same bodies.
        var evidence = owners.VerifyReceipt(receipt.RootElement, new HashSet<string>()).ToDictionary(identity => identity.Assembly, StringComparer.Ordinal);
        var composition = owners.VerifyLoaded(PlatformPackageCatalogQualification.LoadedPackages("eventstore").Concat(PlatformPackageCatalogQualification.LoadedPackages("tenants")),
            PlatformPartiesPackageFixture.PackageFolders).ToDictionary(identity => identity.Assembly, StringComparer.Ordinal);
        composition.Count.ShouldBe(6);
        foreach ((string name, OwnerPackageIdentity loaded) in composition)
        {
            string output = Path.Combine(fixture.Reader, "output", name + ".dll");
            File.ReadAllBytes(output).ShouldBe(File.ReadAllBytes(Assembly.Load(name).Location));
            evidence[name].LoadedPath.ShouldBe(output);
            evidence[name].ExpectedSha256.ShouldBe(loaded.ExpectedSha256);
            evidence[name].ContentHash.ShouldBe(owners.Require(loaded.PackageId, loaded.Version).ContentHash);
            var actual = receipt.RootElement.GetProperty("loadedArtifacts").EnumerateArray().Single(item => item.GetProperty("path").GetString() == output);
            actual.GetProperty("sha256").GetString().ShouldBe(loaded.LoadedSha256);
        }
    }
}
