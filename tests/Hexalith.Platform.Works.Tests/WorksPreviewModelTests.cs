namespace Hexalith.Platform.Works.Tests;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using System.Security.Cryptography;
using System.Text.Json;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.EventStore.Aspire;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Platform.Composition;
using Hexalith.Platform.Works.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;

/// <summary>Checks the Platform-owned target composition and its admission refusals.</summary>
public sealed class WorksPreviewModelTests : IDisposable
{
    private string? _profileRoot;

    /// <summary>A manifest cannot grant a production preview exception or fabricate qualified providers.</summary>
    [Fact]
    public void ProductionIsRefusedBeforeComposingAnyResource()
    {
        IDistributedApplicationBuilder builder = Builder("Production");
        Should.Throw<InvalidOperationException>(() => WorksPreviewModel.Compose(builder)).Message.ShouldContain("profile");
        builder.Resources.ShouldBeEmpty();
        string profile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(profile, WorksProductionProfileTests.Candidate().ToJsonString());
            builder.Configuration["Platform:Works:ProductionProfile"] = profile;
            Should.Throw<InvalidOperationException>(() => WorksPreviewModel.Compose(builder)).Message.ShouldContain("Maintainer");
            builder.Resources.ShouldBeEmpty();
        }
        finally { File.Delete(profile); }
    }

    /// <summary>Platform.Identity remains an explicitly selected synthetic source dependency.</summary>
    [Fact]
    public void IdentityRequiresExplicitSyntheticProvenance()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        builder.Configuration["Platform:Identity:Enabled"] = "true";
        Should.Throw<InvalidOperationException>(() => WorksPreviewModel.Compose(builder)).Message.ShouldContain("test-only");
        builder.Resources.ShouldBeEmpty();
    }

    /// <summary>Unavailable Agents qualification retains its closed contract.</summary>
    [Fact]
    public void AgentsCompositionStillRefusesUnavailableQualification()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        builder.Configuration["Platform:Agents:Enabled"] = "true";
        Should.Throw<InvalidOperationException>(() => PlatformAppModel.ComposeEntry(builder)).Message.ShouldContain("EXT-HOST-1");
        builder.Resources.ShouldBeEmpty();
    }

    /// <summary>The target consumes both SDK modes, readiness and named workload identity.</summary>
    [Fact]
    public async Task WorksConsumesSdkReadinessWithoutGatewayStartupCycle()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        WorksPreviewModel.Compose(builder);
        ProjectResource works = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "works");
        Dictionary<string, object> environment = new(StringComparer.Ordinal);
        await CaptureModelEnvironmentAsync(builder, works, environment);
        environment["Dapr__DeclarativeSubscriptions"].ShouldBe("true");
        IDaprSidecarResource sidecar = works.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
        DaprSidecarOptions options = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
        string subscription = File.ReadAllText(Path.Combine(options.ResourcesPaths!.Single(), "subscription.work.events.yaml"));
        subscription.ShouldContain("deadLetterTopic: deadletter.work.events");
        subscription.ShouldContain("default: /work/events");
        subscription.ShouldContain("  - works");
        environment["Works__Projection__UseSdkDelivery"].ShouldBe("true");
        environment["Works__Recovery__UseSdkRuntime"].ShouldBe("true");
        environment["Authentication__Workload__Audience"].ShouldBe("works");
        environment["Authentication__Workload__AllowedCallers__0"].ShouldBe("eventstore");
        environment["Authentication__Workload__AllowedCallers__1"].ShouldBe("eventstore-admin");
        environment.Keys.Where(key => key.StartsWith("Authentication__Workload__AllowedCallers__", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ShouldBe(["Authentication__Workload__AllowedCallers__0", "Authentication__Workload__AllowedCallers__1"]);
        // Platform's declared runtime and hosting startup are the target's own telemetry composition.
        environment["WORKS_PLATFORM_RUNTIME"].ShouldBe("1");
        environment.ShouldNotContainKey("DOTNET_STARTUP_HOOKS");
#pragma warning disable ASPIREEXTENSION001
        var launch = (ProjectLaunchConfiguration)await works.CreateLaunchConfigurationAsync(ExecutableLaunchMode.NoDebug, TestContext.Current.CancellationToken).ConfigureAwait(true);
#pragma warning restore ASPIREEXTENSION001
        launch.BuildEnvironment.ShouldNotBeNull()["PlatformWorksRuntimeRoot"].ShouldBe(WorksProductionProfileTests.Root);
        launch.ProjectPath.ShouldBe(Path.GetFullPath(Path.Combine(builder.Configuration["Platform:Works:SourceRoot"]
            ?? Path.Combine(builder.AppHostDirectory, "..", "works"), "src", "Hexalith.Works", "Hexalith.Works.csproj")));
        environment["ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"].ShouldBe("Hexalith.Platform.Works.Runtime");
        works.Annotations.OfType<HealthCheckAnnotation>().Single().Key.ShouldBe("works/ready");
        works.Annotations.OfType<WaitAnnotation>().ShouldNotContain(wait => wait.Resource.Name == "eventstore");
        ProjectResource gateway = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore");
        Dictionary<string, object> gatewayEnvironment = new(StringComparer.Ordinal);
        await CaptureModelEnvironmentAsync(builder, gateway, gatewayEnvironment);
        gatewayEnvironment["Dapr__DeclarativeSubscriptions"].ShouldBe("true");
        gatewayEnvironment["EventStore__ProjectionChanges__Transport"].ShouldBe("Direct");
        gateway.Annotations.OfType<HealthCheckAnnotation>().Single().Key.ShouldBe("eventstore/ready");
        // Both names resolve to Platform's SDK readiness check, which keeps Degraded distinct; a plain HTTP probe would not.
        await using ServiceProvider services = builder.Services.BuildServiceProvider();
        HealthCheckRegistration[] registrations = [.. services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations];
        foreach (string name in new[] { "works/ready", "eventstore/ready" })
            registrations.Single(registration => registration.Name == name).Factory(services).ShouldBeOfType<SdkReadinessHealthCheck>(name);
    }

    /// <summary>Every preview host validates the local realm and each issuer shares its credential only with Keycloak.</summary>
    [Fact]
    public async Task WorksPreviewUsesOnlyTheLocalRealmForJwtAuthentication()
    {
        IDistributedApplicationBuilder builder = Builder("Development", replicas: 2);
        builder.Configuration["Platform:Works:Enabled"] = "true";
        builder.Configuration["Platform:Works:SyntheticTenantId"] = "selected-preview-tenant";
        builder.Configuration["Authentication:WorkloadIssuer:TokenEndpoint"] = "http://127.0.0.1:1/inherited";
        builder.Configuration["Authentication:WorkloadIssuer:EnableResourceBindingRequests"] = "true";
        builder.Configuration["EventStore:Authentication:Authority"] = "https://inherited.invalid/";
        string[] sections = ["JwtBearer", "WorkloadJwtBearer"];
        foreach (string section in sections)
        {
            builder.Configuration[$"Authentication:{section}:Issuer"] = "https://inherited.invalid/";
            builder.Configuration[$"Authentication:{section}:SigningKey"] = "stale-development-key";
            builder.Configuration[$"Authentication:{section}:AllowInsecureSymmetricKey"] = "true";
            builder.Configuration[$"Authentication:{section}:AllowedAlgorithms:1"] = "HS256";
        }

        PlatformAppModel.ComposeEntry(builder);
        ContainerResource keycloak = builder.Resources.OfType<ContainerResource>().Single(resource => resource.Name == "platform-local-realm");
        ContainerImageAnnotation image = keycloak.Annotations.OfType<ContainerImageAnnotation>().Single();
        image.Registry.ShouldBe("quay.io");
        image.Image.ShouldBe("keycloak/keycloak");
        image.Tag.ShouldBe("26.6.4");
        Dictionary<string, object> keycloakEnvironment = new(StringComparer.Ordinal);
        foreach (EnvironmentCallbackAnnotation callback in keycloak.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, keycloak, keycloakEnvironment,
                TestContext.Current.CancellationToken)).ConfigureAwait(true);
        }
        string authority = keycloakEnvironment["KC_HOSTNAME"].ShouldBeOfType<string>() + "/realms/hexalith-local";
        EndpointAnnotation endpoint = keycloak.Annotations.OfType<EndpointAnnotation>().Single();
        endpoint.IsProxied.ShouldBeFalse();
        endpoint.IsExternal.ShouldBeFalse();
        endpoint.TargetHost.ShouldBe("127.0.0.1");
        endpoint.Port.ShouldBe(new Uri(authority).Port);
        endpoint.TargetPort.ShouldBe(8080);
        var mounts = keycloak.Annotations.OfType<ContainerMountAnnotation>().ToDictionary(mount => mount.Target, StringComparer.Ordinal);
        mounts.Keys.Order(StringComparer.Ordinal).ShouldBe([
            "/opt/keycloak/data/import/hexalith-local-realm.json",
            "/opt/keycloak/providers/platform-requester-audience.jar",
            "/opt/keycloak/providers/platform-workload-binding.jar",
        ]);
        mounts.Values.ShouldAllBe(mount => mount.Type == ContainerMountType.BindMount && mount.IsReadOnly && File.Exists(mount.Source));
        using var import = JsonDocument.Parse(File.ReadAllText(mounts["/opt/keycloak/data/import/hexalith-local-realm.json"].Source!));
        import.RootElement.GetProperty("attributes").GetProperty("syntheticTenantId").GetString().ShouldBe("selected-preview-tenant");
        string encodedGrants = keycloakEnvironment["HEXALITH_LOCAL_REALM_BINDINGS_WORKS"].ShouldBeOfType<string>();
        using var grants = JsonDocument.Parse(JsonSerializer.Deserialize<string>('"' + encodedGrants + '"').ShouldNotBeNull());
        JsonElement bound = grants.RootElement.EnumerateArray().Single(grant => !grant.GetProperty("allowUnbound").GetBoolean());
        bound.GetProperty("bindings").GetProperty("eventstore:bound-tenant")[0].GetString().ShouldBe("selected-preview-tenant");
        bound.GetProperty("bindings").GetProperty("eventstore:bound-domain")[0].GetString().ShouldBe("work");
        JsonElement unbound = grants.RootElement.EnumerateArray().Single(grant => grant.GetProperty("allowUnbound").GetBoolean());
        unbound.GetProperty("operations").EnumerateArray().Select(operation => operation.GetString()).ShouldBe(["eventstore:trusted-effect"]);
        unbound.GetProperty("bindings").EnumerateObject().ShouldBeEmpty();

        foreach (ProjectResource project in builder.Resources.OfType<ProjectResource>())
        {
            bool works = project.Name is "works" or "works-ha";
            Dictionary<string, object> environment = new(StringComparer.Ordinal);
            await CaptureModelEnvironmentAsync(builder, project, environment);
            foreach (string section in sections)
            {
                string prefix = $"Authentication__{section}__";
                environment[prefix + "Authority"].ShouldBe(authority, project.Name);
                environment[prefix + "Issuer"].ShouldBe(authority, project.Name);
                environment[prefix + "AllowedAlgorithms__0"].ShouldBe("RS256", project.Name);
                environment[prefix + "AllowedAlgorithms__1"].ShouldBe("RS256", project.Name);
                environment[prefix + "SigningKey"].ShouldBe(string.Empty, project.Name);
                environment[prefix + "AllowInsecureSymmetricKey"].ShouldBe("false", project.Name);
            }
            project.Annotations.OfType<WaitAnnotation>().ShouldContain(wait => ReferenceEquals(wait.Resource, keycloak));
            environment["Authentication__WorkloadIssuer__EnableResourceBindingRequests"].ShouldBe(works ? "true" : "false", project.Name);
            if (works) environment["EventStore__Authentication__Authority"].ShouldBe(string.Empty, project.Name);
            environment["Authentication__WorkloadIssuer__TokenEndpoint"].ShouldBe(authority + "/protocol/openid-connect/token", project.Name);
            if (project.Name == "eventstore-operations")
            {
                environment["Authentication__WorkloadIssuer__ClientSecret"].ShouldBe(string.Empty);
                continue;
            }

            string client = works ? "works" : project.Name;
            environment["Authentication__WorkloadIssuer__ClientId"].ShouldBe(client);
            ParameterResource secret = environment["Authentication__WorkloadIssuer__ClientSecret"].ShouldBeOfType<ParameterResource>();
            secret.Secret.ShouldBeTrue();
            string variable = "HEXALITH_LOCAL_REALM_SECRET_" + client.ToUpperInvariant().Replace('-', '_');
            keycloakEnvironment[variable].ShouldBeSameAs(secret);
        }
    }

    /// <summary>Only the gateway sidecar loads the configuration store, whose keys can override tenant rate limits at runtime.</summary>
    [Fact]
    public void OnlyTheGatewaySidecarReferencesTheConfigurationStore()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        WorksPreviewModel.Compose(builder);
        Dictionary<string, string[]> components = builder.Resources.OfType<ProjectResource>().ToDictionary(project => project.Name,
            project => project.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprComponentReferenceAnnotation>()
                .Select(reference => reference.Component.Name).ToArray(), StringComparer.Ordinal);
        components.Keys.Order(StringComparer.Ordinal).ShouldBe(["eventstore", "eventstore-admin", "eventstore-operations", "works"]);
        components.Where(pair => pair.Value.Contains("configstore")).Select(pair => pair.Key).ShouldBe(["eventstore"]);
    }

    /// <summary>Operations waits only for Works to start, not to become healthy, so it stays usable while Works recovery is degraded.</summary>
    [Fact]
    public void OperationsWaitsOnlyForWorksToStart()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        WorksPreviewModel.Compose(builder);
        ProjectResource operations = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore-operations");
        operations.Annotations.OfType<WaitAnnotation>().Where(wait => wait.Resource.Name == "works").ShouldHaveSingleItem()
            .WaitType.ShouldBe(WaitType.WaitUntilStarted);
    }

    /// <summary>The preview composes explicit, finite development/test EventStore rate limits.</summary>
    [Fact]
    public async Task GatewayComposesExplicitDevelopmentRateLimits()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        WorksPreviewModel.Compose(builder);
        ProjectResource gateway = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore");
        Dictionary<string, object> environment = new(StringComparer.Ordinal);
        await CaptureModelEnvironmentAsync(builder, gateway, environment);
        Dictionary<string, object> limits = environment.Where(pair => pair.Key.StartsWith("EventStore__RateLimiting__", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        limits.ShouldBe(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["EventStore__RateLimiting__PermitLimit"] = "8192",
            ["EventStore__RateLimiting__ConsumerPermitLimit"] = "8192",
            ["EventStore__RateLimiting__TenantPermitLimits__anonymous"] = "8192",
            ["EventStore__RateLimiting__ConsumerPermitLimits__anonymous"] = "8192",
        }, ignoreOrder: true);
        ProjectResource works = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "works");
        Dictionary<string, object> worksEnvironment = new(StringComparer.Ordinal);
        await CaptureModelEnvironmentAsync(builder, works, worksEnvironment);
        worksEnvironment.Keys.ShouldNotContain(key => key.StartsWith("EventStore__RateLimiting__", StringComparison.Ordinal));
    }

    /// <summary>Gateway and Admin advertise the v2 technical HTTP receiver profile required by the retained catalog.</summary>
    [Fact]
    public async Task GatewayAndAdminComposeTechnicalHttpReceiverProfile()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        WorksPreviewModel.Compose(builder);
        foreach (string name in new[] { "eventstore", "eventstore-admin" })
        {
            ProjectResource receiver = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == name);
            Dictionary<string, object> environment = new(StringComparer.Ordinal);
            await CaptureModelEnvironmentAsync(builder, receiver, environment);
            const string prefix = "EventStore__RouteCatalog__DevelopmentBootstrap__";
            environment[prefix + "ReceiverProfile"].ShouldBe(RouteCatalogTechnicalHttpPolicy.FenceVersion, name);
            environment[prefix + "BindingVersion"].ShouldBe("2", name);
        }
    }

    /// <summary>Every receiver uses the qualified runtime and a distinct secret app-channel credential, including the second Works replica.</summary>
    /// <param name="replicas">The independently composed Works source replicas.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task EachReceiverRequiresItsOwnSecretAppChannelToken(int replicas)
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        string profilePath = builder.Configuration["Platform:Works:DevelopmentCatalogProfile"]!;
        var profile = JsonSerializer.Deserialize<WorksDevelopmentCatalogProfile>(File.ReadAllBytes(profilePath))!;
        File.WriteAllBytes(profilePath, JsonSerializer.SerializeToUtf8Bytes(profile with { Replicas = replicas }));
        WorksPreviewModel.Compose(builder);
        HashSet<ParameterResource> tokens = [];
        ProjectResource[] projects = [.. builder.Resources.OfType<ProjectResource>()];
        projects.Length.ShouldBe(3 + replicas);
        foreach (ProjectResource project in projects)
        {
            Dictionary<string, object> environment = new(StringComparer.Ordinal);
            await CaptureModelEnvironmentAsync(builder, project, environment);
            environment["DOTNET_ROLL_FORWARD"].ShouldBe("LatestMajor", project.Name);
            environment.TryGetValue("APP_API_TOKEN", out object? value).ShouldBeTrue();
            ParameterResource token = value.ShouldBeOfType<ParameterResource>();
            token.Secret.ShouldBeTrue();
            tokens.Add(token).ShouldBeTrue("Each receiver needs its own credential.");
            builder.Resources.OfType<ParameterResource>().ShouldContain(parameter => ReferenceEquals(parameter, token));
        }
    }

    /// <summary>Generated credentials remain bound to the same receiver and sidecar on repeat registration.</summary>
    [Fact]
    public async Task GeneratedAppChannelBindingsReuseTheirCredential()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        WorksPreviewModel.Compose(builder);
        foreach (ProjectResource project in builder.Resources.OfType<ProjectResource>())
        {
            ParameterResource token = builder.Resources.OfType<ParameterResource>()
                .Single(parameter => parameter.Name == project.Name + "-app-api-token");
            IDaprSidecarResource sidecar = project.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
            int appCallbacks = project.Annotations.OfType<EnvironmentCallbackAnnotation>().Count();
            int sidecarCallbacks = sidecar.Annotations.OfType<EnvironmentCallbackAnnotation>().Count();
            _ = builder.CreateResourceBuilder(project).WithGeneratedEventStoreAppChannelToken();
            builder.Resources.OfType<ParameterResource>().Single(parameter => parameter.Name == token.Name).ShouldBeSameAs(token);
            project.Annotations.OfType<EnvironmentCallbackAnnotation>().Count().ShouldBe(appCallbacks);
            sidecar.Annotations.OfType<EnvironmentCallbackAnnotation>().Count().ShouldBe(sidecarCallbacks);
            Dictionary<string, object> environment = new(StringComparer.Ordinal);
            await CaptureModelEnvironmentAsync(builder, project, environment);
            environment["APP_API_TOKEN"].ShouldBeSameAs(token);
        }
    }

    /// <summary>An unrelated parameter with a reserved name cannot bypass receiver credential registration.</summary>
    /// <param name="receiver">The receiver whose reserved token name is occupied.</param>
    [Theory]
    [InlineData("works")]
    [InlineData("eventstore-operations")]
    public void UnrelatedAppChannelParameterNameIsRefused(string receiver)
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        _ = builder.AddParameter(receiver + "-app-api-token", () => "unrelated-fixture-secret", secret: true);
        Should.Throw<DistributedApplicationException>(() => WorksPreviewModel.Compose(builder))
            .Message.ShouldContain(receiver + "-app-api-token");
    }

    /// <summary>An explicit qualified producer directory controls every SDK executable entry point.</summary>
    [Fact]
    public void ExplicitSdkSourceRootSelectsEverySdkEntryProject()
    {
        string root = Path.Combine(Path.GetTempPath(), "platform-sdk-source-" + Guid.NewGuid().ToString("N"));
        string[] names = ["Hexalith.EventStore", "Hexalith.EventStore.Operations", "Hexalith.EventStore.Admin.Server.Host"];
        try
        {
            foreach (string name in names)
            {
                string directory = Path.Combine(root, "src", name);
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, name + ".csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
            }
            IDistributedApplicationBuilder builder = Builder("Development");
            string module = Path.Combine(root, "module");
            Directory.CreateDirectory(module);
            File.Copy(Path.Combine(WorksProductionProfileTests.Root, "src", "Hexalith.Platform.Composition", "Declarations", "eventstore.json"),
                Path.Combine(module, "hexalith.module-manifest.v2.json"));
            builder.Configuration["Platform:Works:EventStoreSourceRoot"] = root;
            WorksPreviewModel.Compose(builder);
            foreach (ProjectResource project in builder.Resources.OfType<ProjectResource>().Where(project => project.Name != "works"))
                project.GetProjectMetadata().ProjectPath.ShouldStartWith(root + Path.DirectorySeparatorChar);
        }
        finally { Directory.Delete(root, true); }
    }

    /// <summary>A missing explicit producer cannot silently fall back to the sibling checkout.</summary>
    [Fact]
    public void MissingExplicitSdkSourceRootRefusesBeforeResources()
    {
        IDistributedApplicationBuilder builder = Builder("Development");
        string root = Path.Combine(Path.GetTempPath(), "platform-sdk-missing-" + Guid.NewGuid().ToString("N"));
        builder.Configuration["Platform:Works:EventStoreSourceRoot"] = root;
        Should.Throw<FileNotFoundException>(() => WorksPreviewModel.Compose(builder)).FileName.ShouldStartWith(root);
        builder.Resources.ShouldBeEmpty();
    }

    private static async Task CaptureModelEnvironmentAsync(IDistributedApplicationBuilder builder, ProjectResource project,
        Dictionary<string, object> environment)
    {
        foreach (EnvironmentCallbackAnnotation callback in project.Annotations.OfType<EnvironmentCallbackAnnotation>())
        {
            try
            {
                await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, project, environment,
                    TestContext.Current.CancellationToken)).ConfigureAwait(true);
            }
            catch (InvalidOperationException failure) when (failure.Message == "The actual owner CLI has not been rendered.")
            {
                // These model assertions run before DCP renders sidecars. Live parity verifies the owner handoff.
            }
        }
    }

    /// <summary>Verifies certificate ownership and retention with the selected Works development profile.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WorksPreviewDisposalPreservesDefaultCredentialsWithFailedWorkspaceAsync(bool configuredParent, bool failedStartup)
    {
        var builder = Builder("Development");
        string parent = Path.Combine(_profileRoot!, "external-certificate-parent");
        builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(_profileRoot!, "test-dcp-not-started");
        builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(_profileRoot!, "test-dashboard-not-started");
        Directory.CreateDirectory(parent);
        string foreign = Path.Combine(parent, "foreign.txt");
        File.WriteAllText(foreign, "untouched");
        string? workspace = null;
        try
        {
            builder.Configuration["Dapr:Mtls:CertificateDirectory"] = configuredParent ? parent : null;
            WorksPreviewModel.Compose(builder);
            string credentials = builder.Resources.Single(resource => resource.Name == "dapr-sentry").Annotations.OfType<ContainerMountAnnotation>()
                .Single(mount => mount.Target == "/var/run/dapr/credentials").Source!;
            File.WriteAllText(Path.Combine(credentials, "issuer.key"), "owned-works-issuer");
            await using var app = builder.Build();
            PlatformCompositionWorkspace[] owned = app.Services.GetServices<IHostedService>().OfType<PlatformCompositionWorkspace>().ToArray();
            owned.Length.ShouldBe(configuredParent ? 2 : 1);
            workspace = owned.Single(service => service.Path != credentials).Path;
            Path.GetDirectoryName(credentials).ShouldBe(configuredParent ? parent : workspace);
            if (failedStartup) PlatformCompositionWorkspace.MarkFailed(workspace, "fixture startup failure");
            await app.DisposeAsync();
            Directory.Exists(workspace).ShouldBe(failedStartup);
            Directory.Exists(credentials).ShouldBe(failedStartup && !configuredParent);
            if (failedStartup)
            {
                File.ReadAllText(Path.Combine(workspace, PlatformCompositionWorkspace.FailureMarker)).ShouldBe("fixture startup failure");
                if (!configuredParent) File.ReadAllText(Path.Combine(credentials, "issuer.key")).ShouldBe("owned-works-issuer");
            }
            File.ReadAllText(foreign).ShouldBe("untouched");
        }
        finally
        {
            if (workspace is not null && Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        }
    }

    /// <summary>Checks the actual Works file entry and its managed Dapr runtime wiring.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorksPreviewComposesItsPreservedGraphWithoutDeclarationServers(bool defaultRuntime)
    {
        string directory = Path.Combine(Path.GetTempPath(), "works-graph-test-" + Guid.NewGuid().ToString("N"));
        string platform = WorksProductionProfileTests.Root;
        string? previousDaprHome = Environment.GetEnvironmentVariable("HEXALITH_DAPR_HOME");
        string selectedDaprHome = defaultRuntime
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : Path.Combine(directory, "selected-runtime");
        Directory.CreateDirectory(directory);
        try
        {
            Environment.SetEnvironmentVariable("HEXALITH_DAPR_HOME", defaultRuntime ? null : selectedDaprHome);
            IDistributedApplicationBuilder builder = Builder("Development");
            builder.Configuration["Dapr:Mtls:CertificateDirectory"] = Path.Combine(directory, "certificates");
            builder.Configuration["Platform:Works:Enabled"] = "true";
            builder.Configuration["Platform:Timesheets:Enabled"] = "false";
            PlatformAppModel.ComposeEntry(builder);
            builder.Resources.OfType<ProjectResource>().Select(resource => resource.Name).Order().ShouldBe(new[] { "eventstore", "eventstore-admin", "eventstore-operations", "works" }.Order());
            builder.Resources.ShouldNotContain(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any() || resource.Name.StartsWith("tenants", StringComparison.Ordinal) || resource.Name.StartsWith("memories", StringComparison.Ordinal));
            builder.Resources.OfType<ProjectResource>().ShouldAllBe(resource => resource.Annotations.OfType<DaprSidecarAnnotation>().Count() == 1);
            ProjectResource works = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "works");
            DaprSidecarOptions worksOptions = works.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
            worksOptions.ResourcesPaths!.ShouldContain(Path.Combine(platform, "DaprComponents", "works"));
            string subscription = File.ReadAllText(Path.Combine(platform, "DaprComponents", "works", "subscription.work.events.yaml"));
            subscription.ShouldContain("deadLetterTopic: deadletter.work.events");
            subscription.ShouldContain("default: /work/events");
            subscription.ShouldContain("  - works");
            builder.Resources.ShouldContain(resource => resource.Name == "dapr-sentry");
            builder.Resources.ShouldContain(resource => resource.Name == "dapr-placement-mtls");
            builder.Resources.ShouldContain(resource => resource.Name == "dapr-scheduler-mtls");
            int placementPort = builder.Resources.Single(resource => resource.Name == "dapr-placement-mtls").Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc").Port!.Value;
            int schedulerPort = builder.Resources.Single(resource => resource.Name == "dapr-scheduler-mtls").Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc").Port!.Value;
            foreach (var project in builder.Resources.OfType<ProjectResource>())
            {
                var selected = project.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
                selected.RuntimePath.ShouldBe(selectedDaprHome, project.Name);
                selected.PlacementHostAddress.ShouldBe("localhost:" + placementPort, project.Name);
                selected.SchedulerHostAddress.ShouldBe("localhost:" + schedulerPort, project.Name);
            }
            string home = Path.Combine(directory, "qualified-runtime");
            Directory.CreateDirectory(Path.Combine(home, ".dapr", "bin"));
            File.WriteAllText(Path.Combine(home, ".dapr", "bin", OperatingSystem.IsWindows() ? "daprd.exe" : "daprd"), "model-only runtime");
            int currentSentry = CompositionPortAllocator.Allocate(1).PlacementHealth;
            foreach (var project in builder.Resources.OfType<ProjectResource>())
            {
                var sidecar = project.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
                var old = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
                sidecar.Annotations[sidecar.Annotations.IndexOf(old)] = new DaprSidecarOptionsAnnotation(old.Options with { RuntimePath = home });
                foreach (var endpoint in project.Annotations.OfType<EndpointAnnotation>())
                    endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", 32000);
            }
            builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(directory, "test-dcp-not-started");
            builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(directory, "test-dashboard-not-started");
            builder.AddDapr(options => options.EnableTelemetry = false);
            await using var application = builder.Build();
            var model = application.Services.GetRequiredService<DistributedApplicationModel>();
            var eventing = application.Services.GetRequiredService<IDistributedApplicationEventing>();
            foreach (var subscriber in application.Services.GetServices<IDistributedApplicationEventingSubscriber>())
                await subscriber.SubscribeAsync(eventing, builder.ExecutionContext, TestContext.Current.CancellationToken);
            await eventing.PublishAsync(new BeforeStartEvent(application.Services, model), TestContext.Current.CancellationToken);
            builder.Resources.Single(resource => resource.Name == "dapr-sentry").Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "grpc").Port = currentSentry;
            List<string[]> nativeSentryArguments = [];
            foreach (var sidecar in model.Resources.OfType<ExecutableResource>().Where(resource => resource.Name.EndsWith("-dapr-cli", StringComparison.Ordinal)))
            {
                List<object> arguments = [];
                foreach (var callback in sidecar.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
                    await callback.Callback(new CommandLineArgsCallbackContext(arguments, TestContext.Current.CancellationToken));
                string endpoint = arguments[arguments.IndexOf("--sentry-address") + 1].ShouldBeOfType<string>();
                endpoint.ShouldBe("127.0.0.1:" + currentSentry);
                arguments.ShouldContain("--enable-mtls");
                nativeSentryArguments.Add(["--enable-mtls", "--sentry-address", endpoint]);
                sidecar.Command.ShouldBe(Path.Combine(home, ".dapr", "bin", OperatingSystem.IsWindows() ? "daprd.exe" : "daprd"));
            }
            nativeSentryArguments.Count.ShouldBe(4);
            // Exercise the adapter contract with the actual Works-generated endpoints.
            CompositionProcessResult adapter = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("python3",
                ["-c", "import json,sys,isolated_daprd; arguments=json.loads(sys.argv[1]); assert all(isolated_daprd.translated(value,sys.argv[2]) == value for value in arguments)",
                    JsonSerializer.Serialize(nativeSentryArguments), "127.0.0.1:" + currentSentry], Path.Combine(platform, "eng"), null), TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            adapter.TimedOut.ShouldBeFalse();
            adapter.ExitCode.ShouldBe(0, adapter.Output + adapter.ErrorOutput);

        }
        finally
        {
            Environment.SetEnvironmentVariable("HEXALITH_DAPR_HOME", previousDaprHome);
            Directory.Delete(directory, true);
        }
    }

    private IDistributedApplicationBuilder Builder(string environment, int replicas = 1)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--environment=" + environment], ProjectDirectory = WorksProductionProfileTests.Root,
            AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true,
        });
        if (environment == "Development") builder.Configuration["Platform:Works:DevelopmentCatalogProfile"] = CreateTestProfile(replicas);
        return builder;
    }

    private string CreateTestProfile(int replicas)
    {
        _profileRoot = Path.Combine(Path.GetTempPath(), "hexalith-works-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_profileRoot);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(_profileRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string Component(string name)
        {
            string path = Path.Combine(_profileRoot, name + ".yaml");
            // These tests inspect composition only. The unreachable endpoint prevents accidental execution.
            File.WriteAllText(path, File.ReadAllText(Path.Combine(WorksProductionProfileTests.Root, "DaprComponents", name + ".yaml"))
                .Replace("localhost:6379", "127.0.0.1:1", StringComparison.Ordinal));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return path;
        }
        var profile = new WorksDevelopmentCatalogProfile(Component("statestore"),
            new DevelopmentRouteCatalogCompositionOptions(Path.Combine(_profileRoot, "catalog-custody"),
                "routing-model-test", "works-model-test", "test-v1",
                new Dictionary<string, string> { ["test-v1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }, "test-v1"),
            Replicas: replicas, PubSubComponentPath: Component("pubsub"), ConfigStoreComponentPath: Component("configstore"));
        string selected = Path.Combine(_profileRoot, "profile.json");
        File.WriteAllText(selected, JsonSerializer.Serialize(profile));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(selected, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return selected;
    }

    public void Dispose()
    {
        if (_profileRoot is not null && Directory.Exists(_profileRoot)) Directory.Delete(_profileRoot, recursive: true);
    }
}
