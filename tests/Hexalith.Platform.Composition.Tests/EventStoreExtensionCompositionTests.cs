extern alias platformseed;

namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
#if HEXALITH_PLATFORM_EXTENSION_SDK
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Extensions;
using Hexalith.Platform.TestExtension;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
#endif

/// <summary>Story 3.2: one Platform-composed eventstore host assembled from the EventStore server plus declared extension packages.</summary>
[Collection("Platform file entry")]
public sealed class EventStoreExtensionCompositionTests
{
    private const string Module = "extensionprobe";
    private const string Package = "Hexalith.Platform.TestExtension";

    private static string Root => PlatformTestRepository.Root;

    private static string HostProject => Path.Combine(Root, "src", "Hexalith.Platform.EventStoreHost", "Hexalith.Platform.EventStoreHost.csproj");

    [Fact]
    public void CompositionWithoutExtensionsKeepsTheServerGateway()
    {
        using PlatformOwnerFixture owner = Owner(extensions: null);
        IReadOnlyList<PlatformServerBinding> servers = PlatformAppModel.Prepare(SourceOptions(owner));
        PlatformServerBinding gateway = servers.Single(server => server.ResourceId == "eventstore");
        gateway.ProjectPath.ShouldBe(Path.GetFullPath(Path.Combine(PlatformTestRepository.ReferenceRoot(Root, "Hexalith.EventStore"), "src", "Hexalith.EventStore", "Hexalith.EventStore.csproj")));
        gateway.EventStoreHost.ShouldBeNull();
        gateway.Environment.Keys.ShouldNotContain(key => key.StartsWith("EventStore__Extensions", StringComparison.Ordinal));
        JsonSerializer.Serialize(gateway).ShouldNotContain("EventStoreHost");
    }

#if HEXALITH_PLATFORM_EXTENSION_SDK
    [Fact]
    public async Task SourceCompositionAssemblesOneEventStoreHostFromTheServerAndTheMappedExtensionAsync()
    {
        using PlatformOwnerFixture owner = Owner(Extension());
        IDistributedApplicationBuilder builder = Builder();
        PlatformAppModel.Compose(builder, SourceOptions(owner));
        using ServiceProvider cleanup = builder.Services.BuildServiceProvider();
        _ = cleanup.GetRequiredService<PlatformCompositionWorkspace>();

        ProjectResource gateway = builder.Resources.OfType<ProjectResource>().Where(resource => resource.Name == "eventstore").ShouldHaveSingleItem();
        builder.Resources.Count(resource => resource.Name == "eventstore").ShouldBe(1);
        PlatformServerBinding binding = gateway.Annotations.OfType<PlatformServerAnnotation>().Single().Binding;
        binding.ProjectPath.ShouldBe(HostProject);
        PlatformEventStoreHostBinding host = binding.EventStoreHost.ShouldNotBeNull();
        host.ServerProject.ShouldBe(Path.GetFullPath(Path.Combine(PlatformTestRepository.ReferenceRoot(Root, "Hexalith.EventStore"), "src", "Hexalith.EventStore", "Hexalith.EventStore.csproj")));
        PlatformEventStoreExtensionBinding extension = host.Extensions.ShouldHaveSingleItem();
        string project = Path.Combine(owner.Root, "src", Package, Package + ".csproj");
        extension.ShouldSatisfyAllConditions(
            () => extension.Module.ShouldBe(Module),
            () => extension.ExtensionId.ShouldBe(PlatformTestExtensionAdapters.ExtensionId),
            () => extension.ApiMajor.ShouldBe(EventStoreExtensionApi.CurrentMajor),
            () => extension.Reference.ShouldBe(project),
            () => extension.PackageVersion.ShouldBeNull());
        binding.Environment["EventStore__Extensions__Packages__0__PackageId"].ShouldBe(Package);
        binding.Environment["EventStore__Extensions__Packages__0__ExtensionId"].ShouldBe("platform-test");
        binding.Environment["EventStore__Extensions__Packages__0__ApiMajor"].ShouldBe("1");
        binding.Environment["EventStore__Extensions__Packages__0__ModuleId"].ShouldBe(Module);
        binding.Environment["PlatformTestExtension__Marker"].ShouldBe("bound-marker");

        string[] arguments = await ArgumentsAsync(gateway);
        string props = arguments.Single(argument => argument.StartsWith("-p:PlatformEventStoreExtensionBindings=", StringComparison.Ordinal))["-p:PlatformEventStoreExtensionBindings=".Length..];
        XElement reference = XDocument.Load(props).Descendants("ProjectReference").ShouldHaveSingleItem();
        reference.Attribute("Include")!.Value.ShouldBe(project);
        XDocument.Load(props).Descendants("PackageReference").ShouldBeEmpty();
        arguments.ShouldContain(HostProject);

        // The composed host runs with the EventStore server's own settings, not the Platform host directory's.
        Dictionary<string, object> environment = new(StringComparer.Ordinal);
        foreach (EnvironmentCallbackAnnotation callback in gateway.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, gateway, environment, TestContext.Current.CancellationToken));
        string settings = Path.Combine(environment["ASPNETCORE_CONTENTROOT"].ShouldBeOfType<string>(), "appsettings.json");
        File.Exists(settings).ShouldBeTrue();
        JsonNode.Parse(File.ReadAllText(settings))!["EventStore"]!["DomainServices"].ShouldNotBeNull();
    }

    [Fact]
    public async Task SourceBindingsBuildTheComposedHostWithTheMappedExtensionAsync()
    {
        using PlatformOwnerFixture owner = Owner(Extension());
        PlatformCompositionOptions options = SourceOptions(owner);
        PlatformServerBinding gateway = PlatformAppModel.Prepare(options).Single(server => server.ResourceId == "eventstore");
        string work = Path.Combine(owner.Root, "host-build");
        string props = PlatformEventStoreExtensionBindings.Write(gateway.EventStoreHost.ShouldNotBeNull(), Path.Combine(work, "bindings"));
        CompositionProcessResult build = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
            ["build", HostProject, "-c", "Debug", "--artifacts-path", Path.Combine(work, "artifacts"), "-nodeReuse:false", "--disable-build-servers",
                "-p:GeneratePackageOnBuild=false", "-p:PlatformEventStoreExtensionBindings=" + props, "--nologo"], Root, null),
            TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);
        build.ExitCode.ShouldBe(0, build.Output + build.ErrorOutput);
        string output = Path.Combine(work, "artifacts", "bin", "Hexalith.Platform.EventStoreHost", "debug");
        File.Exists(Path.Combine(output, Package + ".dll")).ShouldBeTrue();
        using JsonDocument dependencies = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, "Hexalith.Platform.EventStoreHost.deps.json"), TestContext.Current.CancellationToken));
        dependencies.RootElement.GetProperty("libraries").EnumerateObject().ShouldContain(library => library.Name.StartsWith(Package + "/", StringComparison.Ordinal)
            && library.Value.GetProperty("type").GetString() == "project");
    }

    [Fact]
    public void PackageModeComposesThePlatformHostWithACatalogPinnedPackageReference()
    {
        using PlatformPackageFixture fixture = new(Root);
        using PlatformOwnerFixture owner = Owner(Extension());
        PlatformCompositionOptions options = PackageOptions(fixture, owner, pinned: true);
        PlatformServerBinding gateway = PlatformAppModel.Prepare(options).Where(server => server.ResourceId == "eventstore").ShouldHaveSingleItem();
        gateway.ProjectPath.ShouldBe(Path.GetFullPath(Path.Combine(fixture.Root, "src", "Hexalith.Platform.EventStoreHost", "Hexalith.Platform.EventStoreHost.csproj")));
        gateway.Image.ShouldBeNull();
        PlatformEventStoreHostBinding host = gateway.EventStoreHost.ShouldNotBeNull();
        host.ServerProject.ShouldBe(options.EventStoreSeedProject);
        PlatformEventStoreExtensionBinding extension = host.Extensions.ShouldHaveSingleItem();
        extension.Reference.ShouldBe(Package);
        extension.PackageVersion.ShouldBe("9.8.7");
        string props = PlatformEventStoreExtensionBindings.Write(host, Path.Combine(owner.Root, "package-bindings"));
        XDocument document = XDocument.Load(props);
        document.Descendants("ProjectReference").ShouldBeEmpty();
        document.Descendants("PackageReference").ShouldHaveSingleItem().Attribute("Include")!.Value.ShouldBe(Package);
        XElement version = document.Descendants("PackageVersion").ShouldHaveSingleItem();
        version.Attribute("Update")!.Value.ShouldBe(Package);
        version.Attribute("Version")!.Value.ShouldBe("9.8.7");
    }

    [Fact]
    public void UnpinnedPackageIsRefusedBeforeAnyResourceIsAdded()
    {
        using PlatformPackageFixture fixture = new(Root);
        using PlatformOwnerFixture owner = Owner(Extension());
        InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Prepare(PackageOptions(fixture, owner, pinned: false)));
        refusal.Message.ShouldContain("eventstore-extension-unpinned");
        refusal.Message.ShouldContain(Module);
        refusal.Message.ShouldContain("'" + Package + "'");
    }

    [Fact]
    public void UnsupportedMajorFailsBeforeAnyResourceIsAdded()
    {
        using PlatformOwnerFixture owner = Owner(Extension(major: EventStoreExtensionApi.CurrentMajor + 1));
        IDistributedApplicationBuilder builder = Builder();
        InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, SourceOptions(owner)));
        refusal.Message.ShouldContain("eventstore-extension-unsupported-major");
        refusal.Message.ShouldContain(Module);
        refusal.Message.ShouldContain("'" + Package + "'");
        refusal.Message.ShouldContain("major " + (EventStoreExtensionApi.CurrentMajor + 1));
        refusal.Message.ShouldContain("supports majors " + string.Join(", ", EventStoreExtensionApi.SupportedMajors));
        builder.Resources.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("extensionId")]
    [InlineData("packageId")]
    public void DuplicateExtensionNamesBothModulesAndTheDuplicate(string field)
    {
        using PlatformOwnerFixture owner = Owner(Extension());
        PlatformModuleDeclaration first = Declarations(owner).Single();
        JsonObject copy = JsonNode.Parse(first.Declaration.GetRawText())!.AsObject();
        copy["identity"]!["moduleId"] = "secondprobe";
        JsonObject entry = copy["runtime"]!["eventStoreExtensions"]![0]!.AsObject();
        if (field == "extensionId") entry["packageId"] = "Hexalith.Secondprobe.EventStoreExtensions";
        else entry["extensionId"] = "second-probe";
        PlatformModuleDeclaration second = new("second.json", JsonSerializer.SerializeToElement(copy));
        InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformEventStoreExtensionBindings.Resolve(SourceOptions(owner), [first, second]));
        refusal.Message.ShouldContain("eventstore-extension-duplicate");
        refusal.Message.ShouldContain("modules '" + Module + "' and 'secondprobe'");
        refusal.Message.ShouldContain(field == "extensionId" ? "extension id 'platform-test'" : "extension package '" + Package + "'");
    }

    [Fact]
    public void DuplicateAcrossDeclarationFilesIsRefusedBeforeAnyResourceIsAdded()
    {
        using PlatformOwnerFixture owner = Owner(Extension());
        JsonNode document = JsonNode.Parse(File.ReadAllText(owner.Manifest))!;
        JsonNode module = document["modules"]![0]!;
        module["identity"]!["moduleId"] = "secondprobe";
        module["identity"]!["servers"]![0]!["appId"] = "secondprobe-api";
        module["identity"]!["servers"]![0]!["resourceId"] = "secondprobe-api-host";
        module["runtime"]!["eventStoreExtensions"]![0]!["packageId"] = "Hexalith.Secondprobe.EventStoreExtensions";
        string second = Path.Combine(owner.Root, "second.json");
        File.WriteAllText(second, document.ToJsonString());
        IDistributedApplicationBuilder builder = Builder();
        PlatformCompositionOptions options = SourceOptions(owner) with { DeclarationPaths = [owner.Manifest, second] };
        InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options));
        refusal.Message.ShouldContain("eventstore-extension-duplicate");
        refusal.Message.ShouldContain("'platform-test'");
        refusal.Message.ShouldContain("'" + Module + "' and 'secondprobe'");
        builder.Resources.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("secret", "eventstore-extension-secret", "'PlatformTestExtension:Marker'")]
    [InlineData("dynamic-secret", "eventstore-extension-secret", "'PlatformTestExtension:Marker'")]
    [InlineData("dapr-role", "eventstore-extension-dapr-role", "Dapr role 'events'")]
    public void SecretOrDaprRoleInputIsRefusedBeforeAnyResourceIsAdded(string mutation, string condition, string named)
    {
        JsonObject extension = Extension();
        JsonObject input = extension["configurationInputs"]![0]!.AsObject();
        if (mutation == "dapr-role") input["key"] = "Dapr:EventsComponent";
        else input["source"] = mutation;
        using PlatformOwnerFixture owner = Owner(extension);
        IDistributedApplicationBuilder builder = Builder();
        InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, SourceOptions(owner)));
        refusal.Message.ShouldContain(condition);
        refusal.Message.ShouldContain("Module '" + Module + "'");
        refusal.Message.ShouldContain("'" + Package + "'");
        refusal.Message.ShouldContain(named);
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void AdmittedAd9ExceptionBindsOnlyItsOwnSharedSecret()
    {
        JsonObject extension = Extension();
        extension["providerException"] = "memories-falkordb-graph";
        JsonObject input = extension["configurationInputs"]![0]!.AsObject();
        input["source"] = "secret";
        input["reference"] = "memories-falkordb-graph-credential";
        using PlatformOwnerFixture owner = Owner(extension, ownedException: "memories-falkordb-graph", module: "memories");
        PlatformValidationSucceeds(owner);
        // Only the Memories owner is mapped; an empty secret takes the composition-wide value its own servers receive.
        PlatformCompositionOptions options = SourceOptions(owner) with
        {
            Mapping = new SourceMapping(WorkspaceMode.Source, owner.Root, "Hexalith.Memories", [new SourceMappingEntry("Hexalith.Memories", "source", owner.Root)], false),
            Configuration = new Dictionary<string, string> { ["memories/platform-test/PlatformTestExtension:Marker"] = string.Empty },
        };
        IReadOnlyList<PlatformEventStoreExtensionBinding> bindings = PlatformEventStoreExtensionBindings.Resolve(options, Declarations(owner));
        Dictionary<string, string> shared = new(StringComparer.Ordinal) { ["memories/PlatformTestExtension:Marker"] = "memories-owner-secret" };
        Dictionary<string, string> environment = new(StringComparer.Ordinal);
        PlatformEventStoreExtensionBindings.Bind(environment, options, bindings, shared);
        environment["PlatformTestExtension__Marker"].ShouldBe("memories-owner-secret");
        Dictionary<string, string> generated = new(StringComparer.Ordinal);
        environment.Clear();
        PlatformEventStoreExtensionBindings.Bind(environment, options, bindings, generated);
        environment["PlatformTestExtension__Marker"].ShouldBe(generated.ShouldHaveSingleItem().Value);
        generated.Single().Key.ShouldBe("memories/PlatformTestExtension:Marker");

        input["reference"] = "unrelated-secret";
        using PlatformOwnerFixture foreign = Owner(extension, ownedException: "memories-falkordb-graph", module: "memories");
        PlatformManifestValidator.Validate([foreign.Manifest], TestContext.Current.CancellationToken).Diagnostics.ShouldHaveSingleItem().RuleId.ShouldBe("HXP025");

        // A module other than Memories cannot claim the Memories exception by declaring itself its owner.
        input["reference"] = "memories-falkordb-graph-credential";
        using PlatformOwnerFixture claimant = Owner(extension, ownedException: "memories-falkordb-graph");
        PlatformManifestValidator.Validate([claimant.Manifest], TestContext.Current.CancellationToken).Diagnostics
            .ShouldContain(diagnostic => diagnostic.RuleId == "HXP027" && diagnostic.Message.Contains("Module '" + Module + "'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Authentication:JwtBearer:Issuer")]
    [InlineData("authentication:jwtBearer:issuer")]
    [InlineData("Authentication:JwtBearer")]
    [InlineData("Authentication:JwtBearer:Issuer:Override")]
    [InlineData("EventStore:Extensions:Packages:Forged:PackageId")]
    public void ExtensionInputCannotRebindAGatewayKey(string key)
    {
        JsonObject extension = Extension();
        extension["configurationInputs"]![0]!["key"] = key;
        using PlatformOwnerFixture owner = Owner(extension);
        IDistributedApplicationBuilder builder = Builder();
        PlatformCompositionOptions options = SourceOptions(owner);
        options = options with { Configuration = new Dictionary<string, string>(options.Configuration) { [Module + "/platform-test/" + key] = "forged" } };
        InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options));
        refusal.Message.ShouldContain("eventstore-extension-configuration-conflict");
        refusal.Message.ShouldContain(Module + "/platform-test/" + key);
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void UnboundExtensionInputIsRefusedBeforeAnyResourceIsAdded()
    {
        using PlatformOwnerFixture owner = Owner(Extension());
        IDistributedApplicationBuilder builder = Builder();
        PlatformCompositionOptions options = SourceOptions(owner);
        options = options with { Configuration = options.Configuration.Where(pair => !pair.Key.EndsWith("PlatformTestExtension:Marker", StringComparison.Ordinal)).ToDictionary() };
        InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options));
        refusal.Message.ShouldContain(Module + "/platform-test/PlatformTestExtension:Marker");
        refusal.Message.ShouldContain("binding is unavailable");
        builder.Resources.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("unmapped", "eventstore-extension-unmapped")]
    [InlineData("missing", "eventstore-extension-unavailable")]
    [InlineData("symlink", "eventstore-extension-unavailable")]
    public void SourceMappingRefusesAnUnmappedMissingOrEscapingExtensionProject(string fault, string condition)
    {
        using PlatformOwnerFixture owner = Owner(Extension());
        PlatformCompositionOptions options = SourceOptions(owner);
        string package = Path.Combine(owner.Root, "src", Package);
        string other = Path.Combine(Path.GetTempPath(), "platform-extension-foreign-" + Guid.NewGuid().ToString("N"));
        try
        {
            switch (fault)
            {
                case "unmapped":
                    options = options with { Mapping = options.Mapping with { Entries = [.. options.Mapping.Entries.Where(entry => entry.Identity != "Hexalith.Extensionprobe")] } };
                    break;
                case "missing":
                    File.Delete(Path.Combine(package, Package + ".csproj"));
                    break;
                default:
                    // The physical project lives in another mapped repository; only a link sits under the owner root.
                    Directory.CreateDirectory(Path.Combine(other, "linked"));
                    foreach (string file in Directory.GetFiles(package)) File.Copy(file, Path.Combine(other, "linked", Path.GetFileName(file)));
                    Directory.Delete(package, true);
                    Directory.CreateSymbolicLink(package, Path.Combine(other, "linked"));
                    options = options with { Mapping = options.Mapping with { Entries = [.. options.Mapping.Entries, new SourceMappingEntry("Hexalith.Foreignprobe", "source", other)] } };
                    break;
            }
            IDistributedApplicationBuilder builder = Builder();
            InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options));
            refusal.Message.ShouldContain(condition);
            refusal.Message.ShouldContain(Module);
            builder.Resources.ShouldBeEmpty();
        }
        finally
        {
            if (Directory.Exists(other)) Directory.Delete(other, true);
        }
    }

    [Fact]
    public async Task PlatformHostRegistersExtensionsBeforeTheServerStartsItsCatalogBootstrapAsync()
    {
        using PlatformOwnerFixture owner = Owner(Extension());
        string directory = Directory.CreateDirectory(Path.Combine(owner.Root, "bootstrap")).FullName;
        byte[] bytes = File.ReadAllBytes(Path.Combine(PlatformTestRepository.ReferenceRoot(Root, "Hexalith.EventStore"),
            "deploy", "dapr", "eventstore-routing-catalog.json"));
        string digest = RouteCatalogCodec.ComputeRootDigest(RouteCatalogCodec.Decode(bytes));
        File.WriteAllBytes(Path.Combine(directory, "catalog.json"), bytes);
        File.WriteAllText(Path.Combine(directory, "receipt.json"), JsonSerializer.Serialize(new { instanceId = "registration-test", rootDigest = digest }));
        const string section = "EventStore__RouteCatalog__DevelopmentBootstrap__";
        Dictionary<string, string?> configuration = ExtensionEnvironment(PlatformTestExtensionAdapters.ExtensionId);
        configuration[section + "Directory"] = directory;
        configuration[section + "CatalogPath"] = Path.Combine(directory, "catalog.json");
        configuration[section + "RootDigest"] = digest;
        configuration[section + "InstanceId"] = "registration-test";
        configuration[section + "HostId"] = "eventstore";
        configuration[section + "CoordinatorHostId"] = "eventstore";
        configuration[section + "AttemptId"] = "registration-test";
        configuration[section + "RequiredHostIds__0"] = "eventstore";
        configuration[section + "OwnerAssemblies__0"] = typeof(PlatformTestExtensionAdapters).Assembly.Location;
        configuration[section + "AdapterAssemblies__0"] = typeof(PlatformTestExtensionAdapters).Assembly.Location;
        configuration["EventStore__DomainService__AppId"] = "eventstore";
        using GatewaySeedProtectionInstallation installation = new();
        using EnvironmentScope environment = new(configuration);
        bool inspected = false;
        WebApplicationFactory<platformseed::Program> factory = Factory(installation).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.Where(descriptor => descriptor.ServiceType == typeof(IIdempotencyIntentAdapter))
                .Select(descriptor => descriptor.ImplementationInstance).OfType<IIdempotencyIntentAdapter>()
                .Where(adapter => adapter.AdapterId == PlatformTestExtensionAdapters.AdapterId).ShouldHaveSingleItem();
            inspected = true;
            // This check covers the actual Program registration order before distributed catalog activation starts.
            throw new OperationCanceledException("gateway-registration-inspected");
        }));
        await using var factoryLifetime = factory.ConfigureAwait(true);
        Exception stopped = Should.Throw<Exception>(() => factory.CreateClient());
        inspected.ShouldBeTrue();
        Chain(stopped).ShouldContain(exception => exception is OperationCanceledException && exception.Message == "gateway-registration-inspected");
    }

    [Fact]
    public async Task PlatformHostLoadsTheTestExtensionAndReportsItInReadinessAsync()
    {
        using GatewaySeedProtectionInstallation installation = new();
        using EnvironmentScope extensions = new(ExtensionEnvironment(PlatformTestExtensionAdapters.ExtensionId));
        WebApplicationFactory<platformseed::Program> factory = Factory(installation);
        await using var factoryLifetime = factory.ConfigureAwait(true);
        using HttpClient client = factory.CreateClient();

        EventStoreLoadedExtension loaded = factory.Services.GetRequiredService<EventStoreExtensionCatalog>().Extensions.ShouldHaveSingleItem();
        loaded.ShouldBe(new EventStoreLoadedExtension(Package, "platform-test", 1, 1));
        factory.Services.GetServices<IIdempotencyIntentAdapter>().ShouldContain(adapter => adapter.AdapterId == PlatformTestExtensionAdapters.AdapterId);
        factory.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Single(registration => registration.Name == "eventstore-extensions").Tags.ShouldContain("ready");
        using HttpResponseMessage readiness = await client.GetAsync("/ready", TestContext.Current.CancellationToken).ConfigureAwait(true);
        using JsonDocument report = JsonDocument.Parse(await readiness.Content.ReadAsStringAsync(TestContext.Current.CancellationToken).ConfigureAwait(true));
        JsonElement entry = report.RootElement.GetProperty("results").GetProperty("eventstore-extensions");
        entry.GetProperty("status").GetString().ShouldBe("Healthy");
        entry.GetProperty("data").GetProperty("platform-test").GetInt32().ShouldBe(1);
        entry.GetProperty("description").GetString().ShouldNotBeNull().ShouldContain("platform-test@1 (" + Package + ")");
    }

    [Fact]
    public async Task DeclarationDriftStopsTheHostNamingDeclaredAndActualValuesAsync()
    {
        using GatewaySeedProtectionInstallation installation = new();
        using EnvironmentScope extensions = new(ExtensionEnvironment("drifted-id"));
        WebApplicationFactory<platformseed::Program> factory = Factory(installation);
        await using var factoryLifetime = factory.ConfigureAwait(true);
        Exception failure = Should.Throw<Exception>(() => factory.CreateClient());
        EventStoreExtensionException refusal = Chain(failure).OfType<EventStoreExtensionException>().ShouldHaveSingleItem();
        refusal.Condition.ShouldBe(EventStoreExtensionConditions.DeclarationDrift);
        refusal.Message.ShouldContain("Module '" + Module + "'");
        refusal.Message.ShouldContain("'" + Package + "' is declared as 'drifted-id' major 1");
        refusal.Message.ShouldContain("declares 'platform-test' major 1");
    }

#else
    [Fact]
    public void PackageSdkWithoutTheExtensionApiRefusesDeclaredExtensions()
    {
        using PlatformOwnerFixture owner = Owner(Extension());
        InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformEventStoreExtensionBindings.Resolve(SourceOptions(owner), Declarations(owner)));
        refusal.Message.ShouldContain("eventstore-extension-api-unavailable");
    }

    [Fact]
    public async Task PackageSdkHostRefusesDeclaredExtensionsAtStartupAsync()
    {
        using GatewaySeedProtectionInstallation installation = new();
        using EnvironmentScope extensions = new(ExtensionEnvironment("platform-test"));
        WebApplicationFactory<platformseed::Program> factory = Factory(installation);
        await using var factoryLifetime = factory.ConfigureAwait(true);
        Exception failure = Should.Throw<Exception>(() => factory.CreateClient());
        Chain(failure).ShouldContain(exception => exception is InvalidOperationException && exception.Message.StartsWith("eventstore-extension-api-unavailable:", StringComparison.Ordinal));
    }
#endif

    private static Dictionary<string, string?> ExtensionEnvironment(string extensionId) => new(StringComparer.Ordinal)
    {
        ["EventStore__Extensions__Packages__0__PackageId"] = Package,
        ["EventStore__Extensions__Packages__0__ExtensionId"] = extensionId,
        ["EventStore__Extensions__Packages__0__ApiMajor"] = "1",
        ["EventStore__Extensions__Packages__0__ModuleId"] = Module,
    };

    private static WebApplicationFactory<platformseed::Program> Factory(GatewaySeedProtectionInstallation installation) => new WebApplicationFactory<platformseed::Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(installation.Configuration).AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APP_API_TOKEN"] = "extension-channel-token",
                ["Dapr:DeclarativeSubscriptions"] = "true",
                ["Authentication:JwtBearer:Issuer"] = "hexalith-test",
                ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
                ["Authentication:JwtBearer:SigningKey"] = "test-only-extension-readiness-signing-key-at-least-32-bytes",
                ["Authentication:JwtBearer:AllowedAlgorithms:0"] = "HS256",
                ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            }));
        builder.ConfigureServices(services =>
        {
            // As in the seed subscription fixture, this HTTP host has no sidecar; remove only the startup index writer's hosted alias.
            int index = services.ToList().FindIndex(service => service.ServiceType == typeof(Hexalith.EventStore.Indexes.AdminOperationalIndexHostedService));
            index.ShouldBeGreaterThanOrEqualTo(0);
            ServiceDescriptor hosted = services[index + 2];
            hosted.ServiceType.ShouldBe(typeof(IHostedService));
            hosted.ImplementationFactory.ShouldNotBeNull();
            services.Remove(hosted);
        });
    });

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
            if (current is AggregateException aggregate)
                foreach (Exception inner in aggregate.InnerExceptions.SelectMany(Chain)) yield return inner;
        }
    }


    private static JsonObject Extension(int major = 1) => new()
    {
        ["packageId"] = Package,
        ["extensionId"] = "platform-test",
        ["extensionApiMajor"] = major,
        ["configurationInputs"] = new JsonArray(new JsonObject
        {
            ["key"] = "PlatformTestExtension:Marker",
            ["source"] = "configuration",
            ["reference"] = "platform-test-marker",
        }),
    };

    /// <summary>Creates the synthetic test module that declares the Platform test extension and holds its project at the mapped path.</summary>
    private static PlatformOwnerFixture Owner(JsonObject? extensions, string? ownedException = null, string module = Module)
    {
        PlatformOwnerFixture owner = PlatformOwnerFixture.CreateSample(Root, module);
        JsonNode document = JsonNode.Parse(File.ReadAllText(owner.Manifest))!;
        JsonNode runtime = document["modules"]![0]!["runtime"]!;
        if (extensions is not null) runtime["eventStoreExtensions"] = new JsonArray(extensions.DeepClone());
        if (ownedException is not null)
        {
            runtime["providerExceptions"] = new JsonArray(new JsonObject
            {
                ["name"] = ownedException, ["capability"] = "named provider capability", ["owner"] = module, ["surface"] = module + "-api", ["transitional"] = false,
            });
        }
        File.WriteAllText(owner.Manifest, document.ToJsonString());
        string fixture = Path.Combine(Root, "tests", "fixtures", Package);
        string target = Path.Combine(owner.Root, "src", Package);
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(fixture).Where(file => Path.GetExtension(file) is ".cs" or ".csproj"))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        // The synthetic owner builds with the Platform workspace's own build and package authority.
        File.WriteAllText(Path.Combine(owner.Root, "Directory.Build.props"), new XDocument(new XElement("Project",
            new XElement("Import", new XAttribute("Project", Path.Combine(Root, "Directory.Build.props"))))).ToString());
        File.WriteAllText(Path.Combine(owner.Root, "Directory.Packages.props"), new XDocument(new XElement("Project",
            new XElement("Import", new XAttribute("Project", Path.Combine(Root, "Directory.Packages.props"))))).ToString());
        return owner;
    }

    private static void PlatformValidationSucceeds(PlatformOwnerFixture owner)
    {
        PlatformManifestValidationResult validation = PlatformManifestValidator.Validate([owner.Manifest], TestContext.Current.CancellationToken);
        validation.IsValid.ShouldBeTrue(string.Join("; ", validation.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    private static IReadOnlyList<PlatformModuleDeclaration> Declarations(PlatformOwnerFixture owner)
    {
        PlatformManifestValidationResult validation = PlatformManifestValidator.Validate([owner.Manifest], TestContext.Current.CancellationToken);
        return validation.Declarations ?? throw new InvalidDataException(string.Join("; ", validation.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    private static PlatformCompositionOptions SourceOptions(PlatformOwnerFixture owner)
    {
        SourceMappingEntry[] entries = new[] { "EventStore", "Tenants", "Memories" }
            .Select(name => new SourceMappingEntry("Hexalith." + name, "source", PlatformTestRepository.ReferenceRoot(Root, "Hexalith." + name)))
            .Append(new SourceMappingEntry("Hexalith.Extensionprobe", "source", owner.Root)).ToArray();
        return new PlatformCompositionOptions
        {
            PlatformRoot = Root, Mapping = new SourceMapping(WorkspaceMode.Source, owner.Root, "Hexalith.Extensionprobe", entries, false),
            DeclarationPaths = [owner.Manifest], ActiveModule = Module,
            Projects = new Dictionary<string, string>(PlatformServerBindings.ReferenceProjects) { [Module + "/api"] = "server.csproj" },
            Configuration = new Dictionary<string, string>
            {
                [Module + "/api/Sample:Setting"] = "owner-scoped-value",
                [Module + "/platform-test/PlatformTestExtension:Marker"] = "bound-marker",
            },
        };
    }

    /// <summary>Extends the synthetic release fixture with the test module, its image and, optionally, the extension catalog pin.</summary>
    private static PlatformCompositionOptions PackageOptions(PlatformPackageFixture fixture, PlatformOwnerFixture owner, bool pinned)
    {
        string catalog = Path.Combine(fixture.Root, "references", "Hexalith.Builds", "Props", "Directory.Packages.props");
        XDocument document = XDocument.Load(catalog);
        document.Root!.Add(new XElement("PropertyGroup", new XElement("HexalithExtensionprobeVersion", "1.2.3"), new XElement("HexalithPlatformTestExtensionVersion", "9.8.7")));
        if (pinned)
            document.Root.Add(new XElement("ItemGroup", new XElement("PackageVersion", new XAttribute("Include", Package), new XAttribute("Version", "$(HexalithPlatformTestExtensionVersion)"))));
        document.Save(catalog);
        string host = Path.Combine(fixture.Root, "src", "Hexalith.Platform.EventStoreHost");
        Directory.CreateDirectory(host);
        File.Copy(HostProject, Path.Combine(host, "Hexalith.Platform.EventStoreHost.csproj"), true);
        PlatformCompositionOptions options = fixture.Options;
        return options with
        {
            Mapping = options.Mapping with { Entries = [.. options.Mapping.Entries, new SourceMappingEntry("Hexalith.Extensionprobe", "package", null)] },
            DeclarationPaths = [owner.Manifest], ActiveModule = Module,
            Images = new Dictionary<string, string>(options.Images) { [Module + "/api"] = "registry.example/extensionprobe" },
            Configuration = new Dictionary<string, string>
            {
                [Module + "/api/Sample:Setting"] = "owner-scoped-value",
                [Module + "/platform-test/PlatformTestExtension:Marker"] = "bound-marker",
            },
        };
    }

    private static async Task<string[]> ArgumentsAsync(ProjectResource project)
    {
        List<object> arguments = [];
        foreach (CommandLineArgsCallbackAnnotation callback in project.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            await callback.Callback(new CommandLineArgsCallbackContext(arguments, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        return [.. arguments.Select(argument => argument.ToString()!)];
    }

    private static IDistributedApplicationBuilder Builder() => DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true, AllowUnsecuredTransport = true });
}
