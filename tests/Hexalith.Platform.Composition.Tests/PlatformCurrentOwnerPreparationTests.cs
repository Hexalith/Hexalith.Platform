namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Tenants.Contracts.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

/// <summary>Connects real measured private bodies and inert process mappings to the actual native preparation caller, without starting services.</summary>
public sealed class PlatformCurrentOwnerPreparationTests
{
    /// <summary>Actual preparation refuses absent/foreign mappings before later effects; valid inert mappings publish only component observation identity.</summary>
    [Fact]
    public async Task PreparationRequiresActualOwnerMappingsBeforeAnyReadinessPublicationAsync()
    {
        using var fixture = await PlatformPartiesPackageFixture.CreateAsync(PlatformCurrentOwnerProof.Profile);
        PlatformCompositionOptions options = fixture.CaptureCurrent();
        PlatformCurrentOwnerProof proof = options.CurrentOwnerProof!;
        string output = Path.Combine(proof.Artifacts, "bin", "Hexalith.Parties", "release");
        string[] owners = proof.Bodies.Keys.Where(name => name != "Hexalith.Parties.EventStoreExtensions")
            .Select(name => Path.Combine(output, name + ".dll")).ToArray();
        string structural = Path.Combine(fixture.Workspace, "observer-preparation-component");
        Directory.CreateDirectory(structural);
        var expectedHosts = WriteStructuralCatalog(fixture.Plan, structural);
        var builder = PlatformPartiesPackageFixture.Builder();
        builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(fixture.Workspace, "never-start-dcp");
        builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(fixture.Workspace, "never-start-dashboard");
        ExecutableResource parties = builder.AddExecutable("parties", "never-executed", fixture.Workspace).Resource;
        await using DistributedApplication app = builder.Build();
        ResourceNotificationService notifications = app.Services.GetRequiredService<ResourceNotificationService>();
        string credentials = Path.Combine(fixture.Workspace, NativeTestHandoff.EndpointCredentialsFileName);
        foreach (string fault in new[] { "absent-main", "foreign-contracts" })
        {
            string[] mapped = owners.Where(path => Path.GetFileName(path) != (fault == "absent-main" ? "Hexalith.Parties.dll" : "Hexalith.Parties.Contracts.dll")).ToArray();
            if (fault == "foreign-contracts")
            {
                string foreign = Path.Combine(fixture.Workspace, "Hexalith.Parties.Contracts.dll");
                File.WriteAllText(foreign, "inert foreign body; never used as measured private authority");
                mapped = [.. mapped, foreign];
            }
            await using var process = await PlatformMappedOwnerProcess.StartAsync(mapped);
            await notifications.PublishUpdateAsync(parties, snapshot => snapshot with
            { State = new("Running", null), Properties = [new("executable.pid", process.Id)] });
            proof.Matches(options).ShouldBeTrue("No expected body, source or proof is forged to produce this mapping refusal.");
            File.WriteAllText(credentials, "stale component credential");
            File.WriteAllText(credentials + ".tmp", "stale component credential");
            PlatformNativeTestPreparation preparation = new(fixture.Plan, structural, CompositionSigningKey.Create(),
                new PlatformTenantBootstrapAccess("tenants", "tenants", "unused-component-key", null, "unused-component-issuer"), null!, expectedHosts, options);
            using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            bound.CancelAfter(TimeSpan.FromSeconds(30));
            var refusal = await Should.ThrowAsync<InvalidDataException>(() => preparation.PrepareAsync(app.Services, bound.Token));
            refusal.Message.ShouldContain(fault == "absent-main" ? "not uniquely mapped" : "differs from its measured Release body");
            foreach (string file in new[] { "current-owner-observation.json", "preparation-progress.json", "preparation.json" })
                File.Exists(Path.Combine(structural, file)).ShouldBeFalse();
            File.Exists(credentials).ShouldBeFalse();
            File.Exists(credentials + ".tmp").ShouldBeFalse();
            preparation.NativeEndpointCredentialsPath.ShouldBeNull();
            preparation.NativeHttpEndpoints.ShouldBeEmpty();
            preparation.NativeTestScope.ShouldBeNull();
            proof.Matches(options).ShouldBeTrue();
            TestContext.Current.TestOutputHelper!.WriteLine("actualPreparationRefusal=" + fault + "; observation/credentials/readiness/progress absent; no application started");
        }

        // This positive is a component observation of inert mappings, not serving/catalog/native acceptance.
        PlatformRoutingCatalog catalog = fixture.Generate();
        string implementation = Path.Combine(output, "Hexalith.Parties.dll");
        File.WriteAllBytes(Path.Combine(catalog.Directory, "parties.observation.json"), JsonSerializer.SerializeToUtf8Bytes(new
        {
            HostId = "parties", ExecutableOwners = new Dictionary<string, string>
            { [AssemblyName.GetAssemblyName(implementation).FullName] = proof.Bodies["Hexalith.Parties"] },
        }));
        string[] sdk = new[] { "Hexalith.EventStore.Contracts", "Hexalith.EventStore.ServiceDefaults", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService" }
            .Select(name => Path.Combine(output, name + ".dll")).ToArray();
        await using var positive = await PlatformMappedOwnerProcess.StartAsync(owners.Concat(sdk));
        await notifications.PublishUpdateAsync(parties, snapshot => snapshot with
        { State = new("Running", null), Properties = [new("executable.pid", positive.Id)] });
        await PlatformCurrentOwnerObservation.CaptureAsync(app.Services, options, catalog.Directory, TestContext.Current.CancellationToken);
        using JsonDocument observation = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.Directory, "current-owner-observation.json")));
        observation.RootElement.GetProperty("revision").GetString().ShouldBe(proof.SourceRevision);
        observation.RootElement.GetProperty("qualificationCredit").GetBoolean().ShouldBeFalse();
        observation.RootElement.GetProperty("runtimeBodies").EnumerateObject().Count().ShouldBe(5);
        observation.RootElement.GetProperty("sdkBodies").EnumerateObject().Count().ShouldBe(4);
        File.Exists(credentials).ShouldBeFalse();
    }

    private static IReadOnlyDictionary<string, string> WriteStructuralCatalog(CompositionRunPlan plan, string directory)
    {
        const long generation = 1;
        var envelope = VersionedRouteCatalogBuilder.Build(new(generation, "Development", TenantCatalogDeclarations.Routes,
            TenantCatalogDeclarations.Operations,
            TenantCatalogDeclarations.Routes.Select(route => new RouteCompositionBinding(route.RouteId, "tenants", TenantCatalogDeclarations.ReceiverMethods[route.RouteId], route.ContractVersion, false, "*")).ToArray(),
            TenantCatalogDeclarations.Commands.Select(command => DevelopmentIdempotencyBindings.Bind(command, generation)).ToArray()));
        byte[] canonical = RouteCatalogCodec.Encode(envelope);
        string root = RouteCatalogCodec.ComputeRootDigest(envelope);
        var gateway = new RouteCatalogHostIdentity("eventstore", "eventstore", new string('a', 64), new string('b', 64), new string('c', 64));
        var tenants = gateway with { HostId = "fixture-tenants", AppId = "tenants" };
        RouteCatalogHostIdentity[] hosts = [gateway, tenants];
        var activation = new RouteCatalogActivation(plan.AttemptId, canonical, root, null, hosts, "Development-component", "")
        {
            TechnicalHttpReceivers = [new("eventstore", RouteCatalogTechnicalHttpPolicy.FenceVersion,
                [new("/unit/structural", ["GET"], "component-only-no-runtime-acceptance", new string('a', 64), new string('a', 64),
                    ["EventStoreHuman"], new string('a', 64), new string('a', 64))])],
        };
        string binding = RouteCatalogActivationBinding.ComputeDigest(activation);
        activation = activation with { TrustProof = binding };
        var readback = new RouteCatalogGenerationReadback(1, generation, root, envelope.RouteFacetDigest, envelope.IdempotencyFacetDigest, "Development", hosts, true)
        { AttemptId = plan.AttemptId, ActivationBindingDigest = binding };
        File.WriteAllBytes(Path.Combine(directory, "catalog.json"), canonical);
        File.WriteAllBytes(Path.Combine(directory, "native-scope.json"), JsonSerializer.SerializeToUtf8Bytes(new CompositionNativeTestScope(plan.RunId, "parties", root, ["party"], [plan.TenantNamespace])));
        File.WriteAllBytes(Path.Combine(directory, "activation.json"), JsonSerializer.SerializeToUtf8Bytes(activation));
        File.WriteAllBytes(Path.Combine(directory, "selected-closure.json"), JsonSerializer.SerializeToUtf8Bytes(new { hosts = hosts.Select(host => new { ResourceId = host.HostId, host.AppId }) }));
        foreach (var host in hosts) File.WriteAllBytes(Path.Combine(directory, host.HostId + ".committed.json"), JsonSerializer.SerializeToUtf8Bytes(readback));
        return hosts.ToDictionary(host => host.HostId, host => host.AppId, StringComparer.Ordinal);
    }
}
