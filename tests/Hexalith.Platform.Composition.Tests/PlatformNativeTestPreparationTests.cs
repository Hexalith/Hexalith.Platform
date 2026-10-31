#if HEXALITH_PLATFORM_SOURCE_CATALOG
namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Tenants.Contracts.Identity;
using Hexalith.Tenants.Contracts.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

/// <summary>Exercises native owner preparation up to, but excluding, its first network call.</summary>
public sealed class PlatformNativeTestPreparationTests
{
    private const string ScopeRefusal = "The native consumer scope differs from this owning run.";

    private static readonly Lazy<(byte[] Bytes, string Root)> TenantsCatalog = new(() =>
    {
        // Mirrors the Platform catalog builder for the actual Tenants owner contracts, in process.
        const long generation = 1;
        IReadOnlyDictionary<string, string> methods = TenantCatalogDeclarations.ReceiverMethods;
        RouteCatalogEnvelope envelope = VersionedRouteCatalogBuilder.Build(new(generation, "Development",
            TenantCatalogDeclarations.Routes, TenantCatalogDeclarations.Operations,
            [.. TenantCatalogDeclarations.Routes.Select(route => new RouteCompositionBinding(route.RouteId, "tenants", methods[route.RouteId], route.ContractVersion, false, "*"))],
            [.. TenantCatalogDeclarations.Commands.Select(command => DevelopmentIdempotencyBindings.Bind(command, generation))]));
        return (RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope));
    });

    /// <summary>Refuses each foreign or broadened consumer scope before any state, token or credential channel exists.</summary>
    [Theory]
    [InlineData("foreign-run")]
    [InlineData("foreign-module")]
    [InlineData("global-administrators-domain")]
    [InlineData("missing-synthetic-tenant")]
    public async Task RefusesANativeScopeThatDiffersFromTheOwningRunAsync(string violation)
    {
        using var workspace = new PreparationWorkspace(DateTimeOffset.UtcNow.AddMinutes(10));
        CompositionNativeTestScope valid = workspace.Scope;
        workspace.WriteSource(violation switch
        {
            "foreign-run" => valid with { RunId = CompositionRunPlanFactory.NewRunId() },
            "foreign-module" => valid with { ModuleId = "memories" },
            "global-administrators-domain" => valid with { Domains = [.. valid.Domains, TenantIdentity.GlobalAdministratorsDomain] },
            "missing-synthetic-tenant" => valid with { Tenants = [workspace.Plan.ForeignTenantNamespace] },
            _ => throw new ArgumentOutOfRangeException(nameof(violation)),
        });
        workspace.WriteStaleCredentials();
        PlatformNativeTestPreparation preparation = workspace.Preparation();
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        using CancellationTokenSource bound = Bound();

        Exception failure = await CaptureAsync(preparation.PrepareAsync(services, bound.Token));

        failure.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe(ScopeRefusal);
        workspace.ShouldBeUnprepared(preparation);
    }

    /// <summary>Removes a credential channel that appears after entry cleanup when preparation is refused or cancelled.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedPreparationRemovesCredentialFilesWrittenWhileItRunsAsync(bool cancel)
    {
        using var workspace = new PreparationWorkspace(DateTimeOffset.UtcNow.AddMinutes(10));
        workspace.WriteStaleCredentials();
        PlatformNativeTestPreparation preparation = workspace.Preparation();
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        using CancellationTokenSource bound = Bound();

        // Entry cleanup runs synchronously; the owner then waits for its scope, which is not yet written.
        Task preparing = preparation.PrepareAsync(services, bound.Token);
        File.Exists(workspace.CredentialPath).ShouldBeFalse();
        File.Exists(workspace.CredentialPath + ".tmp").ShouldBeFalse();
        preparing.IsCompleted.ShouldBeFalse();
        workspace.WriteStaleCredentials();
        if (cancel) await bound.CancelAsync();
        else workspace.WriteSource(workspace.Scope with { RunId = CompositionRunPlanFactory.NewRunId() });

        Exception failure = await CaptureAsync(preparing);

        if (cancel) failure.ShouldBeAssignableTo<OperationCanceledException>();
        else failure.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe(ScopeRefusal);
        workspace.ShouldBeUnprepared(preparation);
    }

    /// <summary>Cancels, rather than faults, once the startup deadline elapsed before the bound token fired.</summary>
    [Fact]
    public async Task ElapsedStartupDeadlineCancelsBeforeMintingCredentialsAsync()
    {
        using var workspace = new PreparationWorkspace(DateTimeOffset.UtcNow.AddSeconds(-1));
        workspace.WriteSource(workspace.Scope);
        workspace.WriteStaleCredentials();
        PlatformNativeTestPreparation preparation = workspace.Preparation();
        // No application model: minting tokens and resolving the gateway would fault instead of cancelling.
        using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        using CancellationTokenSource bound = Bound();

        Task preparing = preparation.PrepareAsync(services, bound.Token);
        Exception failure = await CaptureAsync(preparing);

        bound.IsCancellationRequested.ShouldBeFalse();
        failure.ShouldBeOfType<OperationCanceledException>().Message.ShouldContain("startup deadline");
        // The Builds AppHost runner reports a cancelled (not faulted) owner preparation as the startup stall HXR021.
        preparing.IsCanceled.ShouldBeTrue();
        // The valid scope, catalog and closure passed every source check first.
        using (JsonDocument progress = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(workspace.Catalog, "preparation-progress.json"))))
            progress.RootElement.GetProperty("step").GetString().ShouldBe("source-validated");
        workspace.ShouldBeUnprepared(preparation);
    }

    /// <summary>Writes the private endpoint channel exactly as the independent Builds consumer reads it.</summary>
    [Fact]
    public async Task EndpointCredentialWriterProducesTheChannelTheBuildsReaderAcceptsAsync()
    {
        using var workspace = new PreparationWorkspace(null);
        const string apiToken = "synthetic-dapr-api-token.v1~/+=";
        var sidecar = new Uri("http://127.0.0.1:3500/");
        var endpoint = new CompositionNativeHttpEndpoint("memories", "memories", "dapr-invocation",
            new Uri("http://127.0.0.1:3500/v1.0/invoke/memories/method/api/"), "memories-dapr-cli");
        CompositionNativeHttpEndpoints.IsInvocationUri(endpoint.Uri).ShouldBeTrue();

        await PlatformNativeTestPreparation.WriteEndpointCredentialsAsync(workspace.CredentialPath, workspace.Plan, endpoint, apiToken, TestContext.Current.CancellationToken);

        File.Exists(workspace.CredentialPath + ".tmp").ShouldBeFalse();
        if (!OperatingSystem.IsWindows()) File.GetUnixFileMode(workspace.CredentialPath).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        using (JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(workspace.CredentialPath)))
        {
            JsonElement root = document.RootElement;
            root.EnumerateObject().Select(property => property.Name).ShouldBe(["schema", "runId", "syntheticTenantId", "dataIsolationId", "endpoints"]);
            root.GetProperty("schema").GetString().ShouldBe(NativeTestHandoff.EndpointCredentialsSchema);
            root.GetProperty("runId").GetString().ShouldBe(workspace.Plan.RunId);
            root.GetProperty("syntheticTenantId").GetString().ShouldBe(workspace.Plan.TenantNamespace);
            root.GetProperty("dataIsolationId").GetString().ShouldBe(workspace.Plan.ResourceNamespace);
            JsonElement entry = root.GetProperty("endpoints").EnumerateArray().ShouldHaveSingleItem();
            entry.EnumerateObject().Select(property => (property.Name, property.Value.GetString())).ShouldBe(
            [
                ("resourceId", "memories"), ("moduleId", "memories"), ("endpointName", "dapr-invocation"),
                ("uri", endpoint.Uri.AbsoluteUri), ("headerName", "dapr-api-token"), ("value", apiToken),
            ]);
        }

        // The runner's readiness binds the prepared metadata to the observed owner and sidecar allocation.
        CompositionResourceReadiness[] observed =
        [
            new("memories", "Container", "Healthy") { ModuleId = "memories" },
            new("memories-dapr-cli", "Executable", "Healthy") { ModuleId = "memories", Endpoints = [new("http", "http", sidecar)] },
        ];
        var readiness = new CompositionReadiness(CompositionReadiness.SupportedSchema, workspace.Plan.RunId, new Uri("http://127.0.0.1:3501/"), null,
            CompositionNativeHttpEndpoints.Bind(observed, [endpoint]))
        {
            NativeHttpEndpoints = [endpoint], NativeEndpointCredentialsPath = workspace.CredentialPath,
        };
        (await ReadBuildsSecretsAsync(workspace.Plan, readiness)).ShouldBe([apiToken]);
        // The independent reader binds the exact run isolation, so its acceptance above is not vacuous.
        (await ReadBuildsSecretsAsync(workspace.Plan with { TenantNamespace = workspace.Plan.ForeignTenantNamespace }, readiness)).ShouldBeNull();
    }

    private static CancellationTokenSource Bound()
    {
        // ReadRequiredAsync waits for missing owner files; a regression must fail, not hang.
        var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(30));
        return bound;
    }

    private static async Task<Exception> CaptureAsync(Task task)
    {
        try { await task; }
        catch (Exception exception) { return exception; }
        throw new ShouldAssertException("Native owner preparation completed without a network-backed application model.");
    }

    private static async Task<IReadOnlyList<string>?> ReadBuildsSecretsAsync(CompositionRunPlan plan, CompositionReadiness readiness)
    {
        // The Builds consumer is internal to its tooling assembly; bind it by name so a signature change fails here.
        MethodInfo reader = typeof(CompositionReadiness).Assembly
            .GetType("Hexalith.Builds.Tooling.Runtime.CompositionEndpointCredentials", throwOnError: true)!
            .GetMethod("ReadSecretsAsync", BindingFlags.NonPublic | BindingFlags.Static, [typeof(CompositionRunPlan), typeof(CompositionReadiness), typeof(CancellationToken)])
            ?? throw new MissingMethodException("CompositionEndpointCredentials", "ReadSecretsAsync");
        return await (Task<IReadOnlyList<string>?>)reader.Invoke(null, [plan, readiness, TestContext.Current.CancellationToken])!;
    }

    /// <summary>Owns one temporary run workspace with the actual owner source files native preparation reads first.</summary>
    private sealed class PreparationWorkspace : IDisposable
    {
        public PreparationWorkspace(DateTimeOffset? startupDeadlineUtc)
        {
            Root = Path.Combine(Path.GetTempPath(), "platform-native-preparation-" + Guid.NewGuid().ToString("N"));
            CompositionWorkspace.CreatePrivateDirectory(Root);
            Catalog = Path.Combine(Root, "platform-components", "catalog");
            CompositionWorkspace.CreatePrivateDirectory(Catalog);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), Root, Root, CompositionPortAllocator.Allocate(1), [], [],
                platformDeclarationPaths: [Path.Combine(Root, "parties.json")], platformActiveModule: "parties") with
            {
                SelectedTest = new("parties", "platform-source", "dotnet", "tests/native", new string('a', 64), new string('b', 64), 120, 10, now, now.AddMinutes(2), now.AddMinutes(2), "source"),
                StartupDeadlineUtc = startupDeadlineUtc,
            };
            CredentialPath = Path.Combine(Root, NativeTestHandoff.EndpointCredentialsFileName);
            Scope = new(Plan.RunId, "parties", TenantsCatalog.Value.Root, ["party"], [Plan.TenantNamespace]);
        }

        public string Root { get; }

        public string Catalog { get; }

        public CompositionRunPlan Plan { get; }

        public string CredentialPath { get; }

        /// <summary>Gets a scope every source check accepts.</summary>
        public CompositionNativeTestScope Scope { get; }

        // Neither delegated Tenants transport nor the Memories binding is reached before the network boundary.
        public PlatformNativeTestPreparation Preparation() => new(Plan, Catalog, CompositionSigningKey.Create(),
            new PlatformTenantBootstrapAccess("tenants", "tenants", "unused-bootstrap-key", null, "unused-issuer"), null!);

        public void WriteSource(CompositionNativeTestScope scope)
        {
            File.WriteAllBytes(Path.Combine(Catalog, "catalog.json"), TenantsCatalog.Value.Bytes);
            File.WriteAllText(Path.Combine(Catalog, "selected-closure.json"), """{"hosts":[]}""");
            // Publish the scope last and atomically: a waiting owner may poll for it concurrently.
            string staged = Path.Combine(Catalog, "native-scope.staged");
            File.WriteAllBytes(staged, JsonSerializer.SerializeToUtf8Bytes(scope));
            File.Move(staged, Path.Combine(Catalog, "native-scope.json"));
        }

        public void WriteStaleCredentials()
        {
            File.WriteAllText(CredentialPath, "{}");
            File.WriteAllText(CredentialPath + ".tmp", "{}");
        }

        public void ShouldBeUnprepared(PlatformNativeTestPreparation preparation)
        {
            preparation.NativeTestScope.ShouldBeNull();
            preparation.NativeHttpEndpoints.ShouldBeEmpty();
            preparation.NativeEndpointCredentialsPath.ShouldBeNull();
            File.Exists(CredentialPath).ShouldBeFalse();
            File.Exists(CredentialPath + ".tmp").ShouldBeFalse();
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
#endif
