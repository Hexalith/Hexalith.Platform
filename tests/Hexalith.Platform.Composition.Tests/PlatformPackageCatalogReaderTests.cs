namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Aspire.Hosting;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.ApplicationModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.Platform.CatalogArtifacts;
using Shouldly;

/// <summary>Executes the actual private reader in the finite public context; synthetic active-SDK metadata is never runtime acceptance.</summary>
/// <param name="output">Retains executed public artifact and reader log observations.</param>
public sealed class PlatformPackageCatalogReaderTests(ITestOutputHelper output)
{
    /// <summary>The authenticated reader keeps public package bytes and produces the real nonempty Tenant catalog despite an active SDK source identity.</summary>
    [Fact]
    public async Task AuthenticatedPackageReaderBuildsAndExecutesWithPublicArtifactsAsync()
    {
        if (!RequirePublicContext()) return;
        using PlatformPackageCatalogReaderFixture fixture = new();
        await fixture.MaterializeAsync();
        PlatformRoutingCatalog catalog = fixture.Generate();
        catalog.RootDigest.ShouldBe("1756c78fa0cdca5eaf1a712265a54ca9cd7a489afb49d8f0edfb6f84b74588cb");
        catalog.Envelope.Operations!.ShouldNotBeEmpty();
        output.WriteLine("catalog-root=" + catalog.RootDigest);
        string directory = Path.Combine(fixture.Reader, "output");
        foreach (string name in new[] { "Hexalith.EventStore.Contracts", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService", "Hexalith.EventStore.ServiceDefaults", "Hexalith.Tenants.Contracts" })
        {
            Assembly loaded = Assembly.Load(name);
            byte[] reader = File.ReadAllBytes(Path.Combine(directory, name + ".dll"));
            reader.ShouldBe(File.ReadAllBytes(PublishedCatalogArtifactCohort.Verify(loaded, PublishedCatalogArtifactCohort.CurrentName)));
            using var pe = new PEReader(new MemoryStream(reader));
            MetadataReader metadata = pe.GetMetadataReader();
            metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ShouldBe(loaded.ManifestModule.ModuleVersionId);
            output.WriteLine(name + " sha256=" + Convert.ToHexStringLower(SHA256.HashData(reader)) + " mvid=" + loaded.ManifestModule.ModuleVersionId);
        }
        string adapterHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, "Hexalith.Tenants.Server.dll"))));
        adapterHash.ShouldBe("70eca75b01bc7cc8dc056b6babe97529fffec4f78ce81fecc58fbdb1c482003b");
        output.WriteLine("Hexalith.Tenants.Server sha256=" + adapterHash);
        JsonNode assets = JsonNode.Parse(File.ReadAllText(Path.Combine(fixture.Reader, "obj", "project.assets.json")))!;
        assets["libraries"]!.AsObject().Where(pair => pair.Key.StartsWith("Hexalith.EventStore.", StringComparison.Ordinal))
            .ShouldAllBe(pair => pair.Key.EndsWith("/4.1.0", StringComparison.Ordinal) && pair.Value!["type"]!.GetValue<string>() == "package");
        WriteLogs(fixture);
    }

    /// <summary>Changed or unavailable run authority refuses before the reader restores packages or publishes catalog bytes.</summary>
    /// <param name="fault">The private copied authority alteration.</param>
    [Theory]
    [InlineData("mapping")]
    [InlineData("snapshot")]
    [InlineData("catalog")]
    [InlineData("missing-snapshot")]
    public async Task ChangedRunAuthorityRefusesBeforeReaderBuildAsync(string fault)
    {
        if (!RequirePublicContext()) return;
        using PlatformPackageCatalogReaderFixture fixture = new();
        await fixture.MaterializeAsync();
        string root = Path.Combine(fixture.Plan.Workspace, "source-mapping");
        if (fault == "mapping")
        {
            string file = Path.Combine(root, "mapping.json");
            JsonNode changed = JsonNode.Parse(File.ReadAllText(file))!;
            changed["ActiveModule"] = "Hexalith.Foreign";
            File.WriteAllText(file, changed.ToJsonString());
        }
        else if (fault == "catalog") File.AppendAllText(Path.Combine(root, "pinned-tree", "Props", "Directory.Packages.props"), "<!-- changed pinned bytes -->");
        else if (fault == "missing-snapshot") File.Delete(Path.Combine(root, "catalog-versions.json"));
        else
        {
            string file = Path.Combine(root, "catalog-versions.json");
            JsonNode changed = JsonNode.Parse(File.ReadAllText(file))!;
            changed["MappingHash"] = new string('0', 64);
            File.WriteAllText(file, changed.ToJsonString());
        }
        Directory.CreateDirectory(fixture.Reader);
        XDocument reader = XDocument.Load(Path.Combine(fixture.Options.PlatformRoot, "src", "Hexalith.Platform.CatalogBuilder", "Hexalith.Platform.CatalogBuilder.csproj"));
        var refusal = Should.Throw<InvalidDataException>(() => PlatformPackageCatalogReader.Prepare(reader, fixture.Options, fixture.Plan.Workspace, fixture.Reader, DateTimeOffset.UtcNow.AddMinutes(5)));
        if (fault == "mapping") refusal.Message.ShouldContain("differs from the command-resolved hash");
        else
        {
            refusal.Message.ShouldContain("pinned catalog validation failed");
            string log = File.ReadAllText(Path.Combine(fixture.Reader, "catalog-validation.log"));
            log.ShouldContain("HXW012");
            log.ShouldContain(fault == "missing-snapshot" ? "catalog-versions.json" : "snapshot differs from the command-resolved identity");
            if (fault == "missing-snapshot") log.ShouldContain("Could not find file");
        }
        output.WriteLine("authority-refusal=" + fault + " " + refusal.Message);
        Directory.Exists(Path.Combine(fixture.Reader, "output")).ShouldBeFalse();
        Directory.GetFiles(fixture.Plan.Workspace, "catalog.json", SearchOption.AllDirectories).ShouldBeEmpty();
        WriteLogs(fixture);
    }

    /// <summary>The effective-version and actual-assets checks independently reject a changed private reader while preserving the preceding catalog.</summary>
    /// <param name="fault">The direct version or restored target alteration.</param>
    [Theory]
    [InlineData("version")]
    [InlineData("assets")]
    public async Task ReaderChecksEffectiveVersionsAndRestoredAssetsAsync(string fault)
    {
        if (!RequirePublicContext()) return;
        using PlatformPackageCatalogReaderFixture fixture = new();
        await fixture.MaterializeAsync();
        PlatformRoutingCatalog catalog = fixture.Generate();
        byte[] previous = File.ReadAllBytes(catalog.CatalogPath);
        string project = Path.Combine(fixture.Reader, "Hexalith.Platform.CatalogBuilder.csproj");
        XDocument reader = XDocument.Load(project);
        string target;
        if (fault == "version")
        {
            reader.Root!.Add(new XElement("ItemGroup", new XElement("PackageVersion", new XAttribute("Update", "Hexalith.EventStore.Client"), new XAttribute("Version", "4.0.3"))));
            target = "ValidatePrivateReaderCatalogBeforeRestore";
        }
        else
        {
            string assetsFile = Path.Combine(fixture.Reader, "obj", "project.assets.json");
            JsonNode assets = JsonNode.Parse(File.ReadAllText(assetsFile))!;
            JsonObject libraries = assets["targets"]!.AsObject().Single().Value!.AsObject();
            JsonNode changed = libraries["Hexalith.EventStore.Client/4.1.0"]!.DeepClone();
            libraries.Remove("Hexalith.EventStore.Client/4.1.0");
            libraries["Hexalith.EventStore.Client/4.0.3"] = changed;
            File.WriteAllText(assetsFile, assets.ToJsonString());
            // Execute the production-generated task with its actual items/assets, independently of SDK asset-resolution errors.
            XElement production = reader.Root!.Elements("Target").Single(element => (string?)element.Attribute("Name") == "ValidatePrivateReaderCatalogAfterRestore");
            production.Attribute("DependsOnTargets")!.Remove();
            target = "ValidatePrivateReaderCatalogAfterRestore";
        }
        reader.Save(project);
        var result = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
            ["msbuild", project, "-target:" + target, "-nologo", "-m:1", "-nodeReuse:false", "-p:AlternateCommonProps=" + Path.Combine(fixture.Reader, "reader-common.props"),
             "-p:CentralPackageVersionsFileImported=true", "-p:ImportDirectoryPackagesProps=false", "-p:UseHexalithProjectReferences=false", "-p:UseNuGetDeps=true",
             "-p:PlatformCatalogBindings=" + Path.Combine(fixture.Reader, "bindings.props")], fixture.Options.PlatformRoot, null), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        result.ExitCode.ShouldNotBe(0);
        (result.Output + result.ErrorOutput).ShouldContain("requires 4.1.0");
        File.ReadAllBytes(catalog.CatalogPath).ShouldBe(previous);
        output.WriteLine(result.Output + result.ErrorOutput);
        WriteLogs(fixture);
    }

    /// <summary>The general source/package duplicate guard remains active outside this one private reader child.</summary>
    [Fact]
    public async Task OrdinaryConsumerStillRejectsMixedActiveSdkSourceAndPackagesAsync()
    {
        if (!RequirePublicContext()) return;
        using PlatformPackageCatalogReaderFixture fixture = new();
        await fixture.MaterializeAsync();
        string project = Path.Combine(fixture.Plan.Workspace, "ordinary.csproj");
        File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><Import Project=\"" + Path.Combine(fixture.Options.PlatformRoot, "Directory.Build.props") + "\"/><Import Project=\"" + Path.Combine(fixture.Options.PlatformRoot, "Directory.Packages.props") + "\"/><ItemGroup><PackageReference Include=\"Hexalith.EventStore.Client\"/><PackageReference Include=\"Hexalith.Tenants.Server\"/></ItemGroup></Project>");
        var result = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet", ["build", project, "-c", "Release", "-m:1", "-nodeReuse:false", "--disable-build-servers", "-p:CentralPackageVersionsFileImported=true", "--nologo"],
            fixture.Options.PlatformRoot, SourceMappingMaterializer.Environment(fixture.Options.Mapping, fixture.Plan.Workspace)), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
        result.ExitCode.ShouldNotBe(0);
        (result.Output + result.ErrorOutput).ShouldContain("HXW005");
        Directory.GetFiles(fixture.Plan.Workspace, "catalog.json", SearchOption.AllDirectories).ShouldBeEmpty();
        output.WriteLine(result.Output + result.ErrorOutput);
    }

    /// <summary>Completes the authenticated SDK/Tenants production model and real Dapr callbacks with distinct receiver credentials, without starting resources.</summary>
    [Fact]
    public async Task QualifiedTenantsModelForwardsDistinctReceiverTokensAsync()
    {
        PlatformPackageCatalogQualification.HasQualifiedSdkBytes().ShouldBeTrue("This positive case requires the genuine public SDK 4.1.0 cohort.");
        PublishedCatalogArtifactCohort.Matches("Hexalith.Tenants.Contracts", PublishedCatalogArtifactCohort.CurrentName).ShouldBeTrue();
        using PlatformPackageCatalogReaderFixture fixture = new();
        await fixture.MaterializeAsync().ConfigureAwait(true);
        await CompositionDaprComponentRenderer.WriteAsync(fixture.Plan, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var builder = Aspire.Hosting.DistributedApplication.CreateBuilder(new Aspire.Hosting.DistributedApplicationOptions
        {
            Args = ["--environment=Development"], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true,
        });
        string domainToken = ChannelFixtureToken();
        string apiChannelToken = ChannelFixtureToken();
        string apiDaprToken = ChannelFixtureToken();
        using PlatformCompositionWorkspace modelTools = PrepareChannelModelTools(builder, fixture);
        fixture.ComposeRun(builder, options => ChannelModelOptions(options, domainToken, apiChannelToken, apiDaprToken));
        string credentials = builder.Resources.Single(resource => resource.Name == "dapr-sentry").Annotations.OfType<ContainerMountAnnotation>()
            .Single(mount => mount.Target == "/var/run/dapr/credentials").Source!;
        foreach (string file in new[] { "ca.crt", "issuer.crt", "issuer.key" }) File.WriteAllText(Path.Combine(credentials, file), "fixture-" + file);
        await AssertChannelModelCallbacksAsync(builder, domainToken, apiChannelToken, apiDaprToken).ConfigureAwait(true);
        AssertChannelModelPublicReader(fixture);
        output.WriteLine("qualified-tenant-channel-forwarding=domain-and-api; no resources started");
    }

    private static PlatformCompositionOptions ChannelModelOptions(PlatformCompositionOptions options, string domainToken, string apiChannelToken, string apiDaprToken)
    {
        var configuration = new Dictionary<string, string>(options.Configuration);
        configuration["tenants/domain/Tenants:AppChannelToken"] = domainToken;
        configuration["tenants/api/Tenants:AppChannelToken"] = apiChannelToken;
        configuration["tenants/api/Tenants:ApiDaprToken"] = apiDaprToken;
        return options with { Configuration = configuration, ImageProbeRunner = (_, _, _) => Task.FromResult(new CompositionProcessResult(true, 0, "{}", false)) };
    }

    private static PlatformCompositionWorkspace PrepareChannelModelTools(Aspire.Hosting.IDistributedApplicationBuilder builder, PlatformPackageCatalogReaderFixture fixture)
    {
        string root = Path.Combine(Path.GetTempPath(), "platform-channel-model-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(root, "dcp-not-started");
        builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(root, "dashboard-not-started");
        builder.Configuration["Dapr:Mtls:CertificateDirectory"] = root;
        foreach (string file in new[] { "ca.crt", "issuer.crt", "issuer.key" }) File.WriteAllText(Path.Combine(root, file), "fixture-" + file);
        string runtime = Path.Combine(fixture.Plan.DaprHome, ".dapr", "bin");
        Directory.CreateDirectory(runtime);
        File.WriteAllText(Path.Combine(runtime, OperatingSystem.IsWindows() ? "daprd.exe" : "daprd"), "model-only runtime; never executed");
        builder.AddDapr(options => { options.EnableTelemetry = false; options.DaprPath = Path.Combine(root, "dapr-not-started"); });
        return new PlatformCompositionWorkspace(root);
    }

    private static async Task AssertChannelModelCallbacksAsync(Aspire.Hosting.IDistributedApplicationBuilder builder, string domainToken, string apiChannelToken, string apiDaprToken)
    {
        Aspire.Hosting.DistributedApplication app = builder.Build();
        await using var disposal = app.ConfigureAwait(false);
        var model = app.Services.GetRequiredService<Aspire.Hosting.ApplicationModel.DistributedApplicationModel>();
        var servers = model.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).ToArray();
        servers.Select(server => server.Name).Order(StringComparer.Ordinal).ShouldBe(["eventstore", "tenants", "tenants-api"]);
        var eventing = app.Services.GetRequiredService<Aspire.Hosting.Eventing.IDistributedApplicationEventing>();
        foreach (var subscriber in app.Services.GetServices<IDistributedApplicationEventingSubscriber>())
            await subscriber.SubscribeAsync(eventing, builder.ExecutionContext, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await eventing.PublishAsync(new BeforeStartEvent(app.Services, model), TestContext.Current.CancellationToken).ConfigureAwait(false);
        foreach (var resource in model.Resources)
        foreach (var endpoint in resource.Annotations.OfType<EndpointAnnotation>())
            endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", endpoint.TargetPort ?? endpoint.Port ?? 32000);
        foreach (var server in servers.Where(server => server.Name is "tenants" or "tenants-api"))
        {
            string own = server.Name == "tenants" ? domainToken : apiChannelToken;
            string foreign = server.Name == "tenants" ? apiChannelToken : domainToken;
            foreach (var resource in new Aspire.Hosting.ApplicationModel.IResource[] { server, model.Resources.Single(resource => resource.Name == server.Name + "-dapr-cli") })
            {
                Dictionary<string, object> environment = [];
                foreach (var callback in resource.Annotations.OfType<Aspire.Hosting.ApplicationModel.EnvironmentCallbackAnnotation>())
                    await callback.Callback(new Aspire.Hosting.ApplicationModel.EnvironmentCallbackContext(builder.ExecutionContext, resource, environment, TestContext.Current.CancellationToken)).ConfigureAwait(false);
                environment["APP_API_TOKEN"].ShouldBe(own);
                environment["APP_API_TOKEN"].ShouldNotBe(foreign);
                if (server.Name == "tenants-api")
                {
                    environment["DAPR_API_TOKEN"].ShouldBe(apiDaprToken);
                    environment["APP_API_TOKEN"].ShouldNotBe(apiDaprToken);
                }
                else environment.ShouldNotContainKey("DAPR_API_TOKEN");
            }
        }
    }

    private void AssertChannelModelPublicReader(PlatformPackageCatalogReaderFixture fixture)
    {
        string directory = Path.Combine(fixture.Reader, "output");
        foreach (string name in new[] { "Hexalith.EventStore.Contracts", "Hexalith.EventStore.Client", "Hexalith.EventStore.DomainService", "Hexalith.EventStore.ServiceDefaults", "Hexalith.Tenants.Contracts" })
        {
            Assembly loaded = Assembly.Load(name);
            byte[] reader = File.ReadAllBytes(Path.Combine(directory, name + ".dll"));
            reader.ShouldBe(File.ReadAllBytes(PublishedCatalogArtifactCohort.Verify(loaded, PublishedCatalogArtifactCohort.CurrentName)));
            using var pe = new PEReader(new MemoryStream(reader));
            MetadataReader metadata = pe.GetMetadataReader();
            metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ShouldBe(loaded.ManifestModule.ModuleVersionId);
            output.WriteLine(name + " sha256=" + Convert.ToHexStringLower(SHA256.HashData(reader)) + " mvid=" + loaded.ManifestModule.ModuleVersionId);
        }
        string adapterHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, "Hexalith.Tenants.Server.dll"))));
        adapterHash.ShouldBe("70eca75b01bc7cc8dc056b6babe97529fffec4f78ce81fecc58fbdb1c482003b");
        output.WriteLine("Hexalith.Tenants.Server sha256=" + adapterHash);
        string receipt = Directory.GetFiles(Path.Combine(fixture.Plan.Workspace, "platform-components"), "receipt.json", SearchOption.AllDirectories).Single();
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(receipt));
        string root = document.RootElement.GetProperty("rootDigest").GetString()!;
        root.ShouldBe("1756c78fa0cdca5eaf1a712265a54ca9cd7a489afb49d8f0edfb6f84b74588cb");
        output.WriteLine("catalog-root=" + root);
    }

    private static string ChannelFixtureToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static bool RequirePublicContext()
    {
        if (PlatformPackageCatalogQualification.HasQualifiedSdkBytes() && PublishedCatalogArtifactCohort.Matches("Hexalith.Tenants.Contracts", PublishedCatalogArtifactCohort.CurrentName)) return true;
        using PlatformQualificationPackageFixture fixture = new();
        var declarations = PlatformDeclarationSet.Load(fixture.Options);
        var servers = PlatformServerBindings.Resolve(fixture.Options, declarations);
        Should.Throw<InvalidDataException>(() => PlatformPackageCatalogQualification.Bind(fixture.Options, declarations, servers, true)).Message.ShouldContain("owner SDK");
        return false;
    }

    private void WriteLogs(PlatformPackageCatalogReaderFixture fixture)
    {
        if (!Directory.Exists(fixture.Reader)) return;
        foreach (string log in Directory.GetFiles(fixture.Reader, "*.log")) output.WriteLine(Path.GetFileName(log) + ": " + File.ReadAllText(log));
    }
}
