namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Security.Cryptography;
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
        PlatformRoutingCatalog catalog = PlatformRoutingCatalog.Generate(fixture.Options, servers, Path.Combine(fixture.Plan.Workspace, "platform-components"));
        using JsonDocument request = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Reader, "request.json")));
        request.RootElement.GetProperty("gatewayAppId").GetString().ShouldBe(gatewayAppId);
        request.RootElement.GetProperty("qualificationCohort").GetString().ShouldBe(PublishedCatalogArtifactCohort.EventStorePackageTestName);
        request.RootElement.GetProperty("owners").GetArrayLength().ShouldBe(1);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(catalog.CatalogPath));
        foreach (string key in new[] { "routes", "operations", "declarations" }) document.RootElement.GetProperty(key).GetArrayLength().ShouldBe(23);
        document.RootElement.GetProperty("idempotency").GetArrayLength().ShouldBe(12);
        catalog.Envelope.Idempotency.ShouldAllBe(policy => policy.ConsumerAppId == gatewayAppId);
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.Directory, "receipt.json")));
        foreach (string name in new[] { "Hexalith.EventStore.Contracts", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService",
            "Hexalith.EventStore.ServiceDefaults", "Hexalith.Tenants.Contracts", "Hexalith.Tenants.Server" })
        {
            string original = PublishedCatalogArtifactCohort.Verify(Assembly.Load(name), PublishedCatalogArtifactCohort.EventStorePackageTestName);
            string output = Path.Combine(fixture.Reader, "output", name + ".dll");
            File.ReadAllBytes(output).ShouldBe(File.ReadAllBytes(original));
            var actual = receipt.RootElement.GetProperty("loadedArtifacts").EnumerateArray().Single(item => item.GetProperty("path").GetString() == output);
            actual.GetProperty("sha256").GetString().ShouldBe(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(original))));
        }
    }
}
