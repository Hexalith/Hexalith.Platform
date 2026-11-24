namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using System.Security.Cryptography;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using YamlDotNet.RepresentationModel;

// Existing workspace-cleanup tests inspect the shared temporary directory.
[Collection("Timesheets host binding")]
public sealed class TimesheetsHostBindingTests
{
    private static string Root => PlatformTestRepository.Root;

    private static string Candidate => Path.Combine(Root, "eng", "timesheets-deployment", "hexalith.module-manifest.v2.json");

    private static PlatformCompositionOptions Options(bool enabled = true, WorkspaceMode mode = WorkspaceMode.Source) => new()
    {
        PlatformRoot = Root,
        Mapping = new SourceMapping(mode, Root, "Hexalith.Platform", new[] { "EventStore", "Tenants", "Memories", "Timesheets" }
            .Select(owner => new SourceMappingEntry("Hexalith." + owner, mode == WorkspaceMode.Source ? "source" : "package", mode == WorkspaceMode.Source ? Path.Combine(Root, "references", "Hexalith." + owner) : null)).ToArray(), false),
        TimesheetsEnabled = enabled,
    };

    private static IDistributedApplicationBuilder Builder(string environment = "Development")
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, ProjectDirectory = Root,
            DisableDashboard = true, AllowUnsecuredTransport = true,
        });
        builder.Environment.EnvironmentName = environment;
        return builder;
    }

    private static void CleanWorkspace(IDistributedApplicationBuilder builder)
    {
        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        provider.GetService<PlatformCompositionWorkspace>()?.Dispose();
    }

    private static async Task<Dictionary<string, object>> EnvironmentAsync(IDistributedApplicationBuilder builder, IResource resource)
    {
        Dictionary<string, object> environment = new();
        foreach (EnvironmentCallbackAnnotation callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, resource, environment, TestContext.Current.CancellationToken)).ConfigureAwait(true);
        return environment;
    }

    private static IConfigurationRoot Configuration(IReadOnlyDictionary<string, string> environment) => new ConfigurationBuilder()
        .AddInMemoryCollection(environment.Select(pair => new KeyValuePair<string, string?>(pair.Key.Replace("__", ":", StringComparison.Ordinal), pair.Value))).Build();

    private static CompositionRunPlan Plan(string workspace) => CompositionRunPlanFactory.Create(
        CompositionRunPlanFactory.NewRunId(), workspace, workspace, CompositionPortAllocator.Allocate(1), [], [],
        Options().Mapping with { Entries = [.. Options().Mapping.Entries, new SourceMappingEntry("Hexalith.Platform", "source", Root)] },
        [Candidate], "timesheets", typeof(PlatformAppModel).Assembly.Location) with
        { PlatformDeclarationHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Candidate))) };

    [Fact]
    public void AbsentOptInKeepsCandidateInertAndReferenceGraphUnchanged()
    {
        IDistributedApplicationBuilder builder = Builder();
        try
        {
            PlatformAppModel.Compose(builder, Options(false) with { DeclarationPaths = [Candidate] });
            builder.Resources.Count(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).ShouldBe(7);
            builder.Resources.ShouldNotContain(resource => resource.Name.StartsWith("timesheets", StringComparison.Ordinal));
        }
        finally { CleanWorkspace(builder); }
    }

    [Fact]
    public void CandidateSelectionRefusesImplicitActivationBeforeAddingResources()
    {
        IDistributedApplicationBuilder builder = Builder();
        InvalidDataException failure = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, Options(false) with { ActiveModule = "timesheets", DeclarationPaths = [Candidate] }));
        failure.Message.ShouldContain("timesheets/host");
        failure.Message.ShouldContain("Platform:Timesheets:Enabled");
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void OptInSelectsActualMappedHostAndDomainMinimumEvenForTechnicalSelection()
    {
        IReadOnlyList<PlatformServerBinding> servers = PlatformAppModel.Prepare(Options() with { ActiveModule = "eventstore", DeclarationPaths = [Candidate] });
        servers.Select(server => server.ResourceId).Order(StringComparer.Ordinal).ShouldBe(
            ["eventstore", "memories", "memories-access-telemetry", "memories-access-telemetry-clock", "memories-domain-service", "tenants", "tenants-api", "timesheets"]);
        PlatformServerBinding host = servers.Single(server => server.ResourceId == "timesheets");
        host.ProjectPath.ShouldBe(Path.Combine(Root, "references", "Hexalith.Timesheets", PlatformTimesheetsBinding.Project));
        Path.GetFullPath(host.Module.Source).ShouldBe(Candidate);
        host.AppId.ShouldBe("timesheets");
        host.ReadinessEndpoint.ShouldBe("/ready");
        host.Tasks.ShouldBeEmpty();
        servers.Select(server => PlatformDeclarationSet.Id(server.Module.Declaration)).Distinct().Order().ShouldBe(["eventstore", "memories", "tenants", "timesheets"]);
        PlatformServerBindings.ReferenceProjects.ShouldNotContainKey("timesheets/host");
        PlatformServerBindings.ReferenceImages.ShouldNotContainKey("timesheets/host");
    }

    [Theory]
    [InlineData("mapping")]
    [InlineData("project")]
    [InlineData("outside-root")]
    public void MissingOrUnmappedSourceIsAnAtomicTimesheetsRefusal(string fault)
    {
        PlatformCompositionOptions options = Options();
        options = fault == "mapping"
            ? options with { Mapping = options.Mapping with { Entries = options.Mapping.Entries.Where(entry => entry.Identity != "Hexalith.Timesheets").ToArray() } }
            : options with { Projects = new Dictionary<string, string>(PlatformServerBindings.ReferenceProjects) { ["timesheets/host"] = fault == "project" ? "missing.csproj" : "../Hexalith.EventStore/src/Hexalith.EventStore/Hexalith.EventStore.csproj" } };
        IDistributedApplicationBuilder builder = Builder();
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options)).Message.ShouldContain("timesheets/host");
        builder.Resources.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CanonicalMappedHostMustExistPhysicallyInsideTheTimesheetsOwner(bool escapedLink)
    {
        string owner = Path.Combine(Path.GetTempPath(), "timesheets-mapped-owner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(owner);
        try
        {
            PlatformCompositionOptions options = Options();
            options = options with { Mapping = options.Mapping with { Entries = options.Mapping.Entries
                .Select(entry => entry.Identity == "Hexalith.Timesheets" ? entry with { Path = owner } : entry).ToArray() } };
            if (escapedLink)
            {
                string host = Path.Combine(owner, PlatformTimesheetsBinding.Project);
                Directory.CreateDirectory(Path.GetDirectoryName(host)!);
                File.CreateSymbolicLink(host, Path.Combine(Root, "references", "Hexalith.EventStore", "src", "Hexalith.EventStore", "Hexalith.EventStore.csproj"));
                options.Mapping.ContainsProject(host).ShouldBeTrue("the other mapped owner would admit this link without owner-scoped physical containment");
            }
            IDistributedApplicationBuilder builder = Builder();
            Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options)).Message.ShouldContain("physically outside its owner root");
            builder.Resources.ShouldBeEmpty();
        }
        finally { Directory.Delete(owner, true); }
    }

    [Fact]
    public void RelativePlatformRootDeduplicatesTheAbsoluteSuppliedCandidate()
    {
        PlatformCompositionOptions options = Options() with
        {
            PlatformRoot = Path.GetRelativePath(Directory.GetCurrentDirectory(), Root), DeclarationPaths = [Candidate],
        };
        IReadOnlyList<PlatformServerBinding> servers = PlatformAppModel.Prepare(options);
        servers.Count.ShouldBe(8);
        servers.Count(server => server.ResourceId == "timesheets").ShouldBe(1);
    }

    [Fact]
    public void MissingCandidateIsAtomicAndReportsItsOwnerFile()
    {
        string absent = Path.Combine(Path.GetTempPath(), "absent-timesheets-candidate-" + Guid.NewGuid().ToString("N"));
        IDistributedApplicationBuilder builder = Builder();
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, Options() with { PlatformRoot = absent })).Message.ShouldContain("timesheets-deployment");
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void PackageModeRefusesQualifiedReleaseBeforeReferenceResolution()
    {
        IDistributedApplicationBuilder builder = Builder();
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, Options(mode: WorkspaceMode.Package))).Message.ShouldContain("qualified release and production profile");
        builder.Resources.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void UnsupportedProfileRefusesBothEntryPathsBeforeAddingResources(string profile)
    {
        IDistributedApplicationBuilder builder = Builder(profile);
        Should.Throw<InvalidOperationException>(() => PlatformAppModel.Compose(builder, Options())).Message.ShouldContain("Development run");
        builder.Resources.ShouldBeEmpty();
        builder.Configuration["Platform:Timesheets:Enabled"] = "true";
        Should.Throw<InvalidOperationException>(() => PlatformAppModel.Compose(builder)).Message.ShouldContain("qualified release and production profile");
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void PublishModelIsRefusedBeforeAddingResources()
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
        {
            Args = ["--publisher", "manifest"], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name,
            ProjectDirectory = Root, DisableDashboard = true,
        });
        builder.Environment.EnvironmentName = Environments.Development;
        builder.ExecutionContext.IsPublishMode.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => PlatformAppModel.Compose(builder, Options())).Message.ShouldContain("qualified release and production profile");
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void NonlocalAuthenticationIsRefusedAtomically()
    {
        IDistributedApplicationBuilder builder = Builder();
        PlatformCompositionOptions options = Options() with
        {
            Configuration = new Dictionary<string, string> { ["eventstore/gateway/Authentication:JwtBearer:Authority"] = "https://authority.example" },
        };
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options)).Message.ShouldContain("qualified production binding");
        builder.Resources.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("SigningKey", "short")]
    [InlineData("SigningKey", " ")]
    [InlineData("Issuer", "")]
    [InlineData("AllowedAlgorithms", "[\"RS256\"]")]
    [InlineData("Audience", "")]
    public void InvalidSharedDevelopmentJwtContractsRefuseBeforeResources(string key, string value)
    {
        Dictionary<string, string> configuration = new() { ["eventstore/gateway/Authentication:JwtBearer:" + key] = value };
        if (key == "Audience") configuration["eventstore/gateway/Authentication:JwtBearer:ValidAudiences"] = "[]";
        IDistributedApplicationBuilder builder = Builder();
        InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, Options() with { Configuration = configuration }));
        refusal.Message.ShouldContain("Authentication:JwtBearer");
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void FileEntryRefusesMixedWorksAndTimesheetsBeforeSelectingEitherGraph()
    {
        IDistributedApplicationBuilder builder = Builder();
        builder.Configuration["Platform:Works:Enabled"] = "true";
        builder.Configuration["Platform:Timesheets:Enabled"] = "true";
        Should.Throw<InvalidOperationException>(() => PlatformAppModel.ComposeEntry(builder)).Message.ShouldContain("cannot be combined");
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void FileEntryPreservesAgentsUnavailableRefusal()
    {
        IDistributedApplicationBuilder builder = Builder();
        builder.Configuration["Platform:Agents:Enabled"] = "true";
        Should.Throw<InvalidOperationException>(() => PlatformAppModel.ComposeEntry(builder)).Message.ShouldContain("EXT-HOST-1");
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public async Task BuildsSourceRunBindsRunnerSigningMaterialAndRefusesUnenrolledCandidateAsync()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "timesheets-builds-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            CompositionRunPlan plan = Plan(workspace);
            await CompositionDaprComponentRenderer.WriteAsync(plan, TestContext.Current.CancellationToken);
            IDistributedApplicationBuilder builder = Builder();
            const string signing = "builds-runner-signing-material-256-bit";
            InvalidDataException failure = Should.Throw<InvalidDataException>(() =>
                PlatformAppModel.ComposeRun(builder, plan, signing, seedProject: null, options =>
                {
                    options.TimesheetsEnabled.ShouldBeTrue();
                    options.RunPlan.ShouldBeSameAs(plan);
                    PlatformServerBinding host = PlatformAppModel.Prepare(options).Single(server => server.ResourceId == "timesheets");
                    host.Environment["Authentication__JwtBearer__SigningKey"].ShouldBe(signing);
                    host.Environment["EventStore__Authentication__SigningKey"].ShouldBe(signing);
                    return options;
                }));
            failure.Message.ShouldContain("Selected caller 'timesheets'");
            failure.Message.ShouldContain("lacks catalog enrollment");
            builder.Resources.ShouldBeEmpty();
        }
        finally { Directory.Delete(workspace, true); }
    }

    [Theory]
    [InlineData("wrong-path")]
    [InlineData("missing-path")]
    [InlineData("extra-path")]
    [InlineData("missing-hash")]
    [InlineData("hash-drift")]
    [InlineData("missing-candidate")]
    public void BuildsTimesheetsSelectionRefusesWrongMissingOrDriftedCandidate(string fault)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "timesheets-run-selection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            CompositionRunPlan plan = Plan(workspace);
            if (fault == "wrong-path") plan = plan with { PlatformDeclarationPaths = [Path.Combine(Root, "references", "Hexalith.EventStore", "module", "hexalith.module-manifest.v2.json")] };
            if (fault == "missing-path") plan = plan with { PlatformDeclarationPaths = [] };
            if (fault == "extra-path") plan = plan with { PlatformDeclarationPaths = [Candidate, Candidate] };
            if (fault == "missing-hash") plan = plan with { PlatformDeclarationHash = null };
            if (fault is "hash-drift" or "missing-candidate")
            {
                string candidate = Path.Combine(workspace, "eng", "timesheets-deployment", "hexalith.module-manifest.v2.json");
                if (fault == "hash-drift")
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(candidate)!);
                    File.WriteAllText(candidate, File.ReadAllText(Candidate) + " ");
                }
                plan = plan with { PlatformDeclarationPaths = [candidate], SourceMapping = plan.SourceMapping! with
                    { Entries = plan.SourceMapping!.Entries.Select(entry => entry.Identity == "Hexalith.Platform" ? entry with { Path = workspace } : entry).ToArray() } };
            }
            IDistributedApplicationBuilder builder = Builder();
            Should.Throw<InvalidDataException>(() => PlatformAppModel.ComposeRun(builder, plan, new string('s', 48), null)).Message.ShouldContain("Hexalith.Timesheets");
            builder.Resources.ShouldBeEmpty();
        }
        finally { Directory.Delete(workspace, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BuildsReferenceSelectionRemainsUnexpandedAndRefusesAmbientOptInAsync(bool ambient)
    {
        string workspace = Path.Combine(Path.GetTempPath(), "timesheets-reference-selection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            string reference = Path.Combine(Root, "references", "Hexalith.EventStore", "module", "hexalith.module-manifest.v2.json");
            CompositionRunPlan plan = Plan(workspace) with { PlatformActiveModule = "eventstore", PlatformDeclarationPaths = [reference],
                PlatformDeclarationHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(reference))) };
            IDistributedApplicationBuilder builder = Builder();
            if (ambient)
            {
                builder.Configuration["Platform:Timesheets:Enabled"] = "true";
                Should.Throw<InvalidDataException>(() => PlatformAppModel.ComposeRun(builder, plan, new string('s', 48), null)).Message.ShouldContain("cannot expand another Builds plan");
                builder.Resources.ShouldBeEmpty();
            }
            else
            {
                await CompositionDaprComponentRenderer.WriteAsync(plan, TestContext.Current.CancellationToken);
                PlatformAppModel.ComposeRun(builder, plan, new string('s', 48), null);
                builder.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).Select(resource => resource.Name).ShouldBe(["eventstore"]);
            }
        }
        finally { Directory.Delete(workspace, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FileEntryHonorsExplicitOptInAndCatalogEnrollment(bool enabled)
    {
        string mappingPath = Path.Combine(Path.GetTempPath(), "timesheets-host-mapping-" + Guid.NewGuid().ToString("N") + ".json");
        SourceMapping mapping = Options().Mapping;
        File.WriteAllText(mappingPath, JsonSerializer.Serialize(mapping));
        IDistributedApplicationBuilder builder = Builder();
        try
        {
            builder.Configuration["Platform:SourceMapping:Path"] = mappingPath;
            builder.Configuration["Platform:SourceMapping:Hash"] = mapping.ContentHash;
            builder.Configuration["Platform:Timesheets:Enabled"] = enabled.ToString();
            if (enabled)
            {
                InvalidDataException failure = Should.Throw<InvalidDataException>(() => PlatformAppModel.ComposeEntry(builder));
                failure.Message.ShouldContain("Selected caller 'timesheets'");
                failure.Message.ShouldContain("lacks catalog enrollment");
                builder.Resources.ShouldBeEmpty();
            }
            else
            {
                PlatformAppModel.ComposeEntry(builder);
                builder.Resources.Count(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).ShouldBe(7);
                builder.Resources.ShouldNotContain(resource => resource.Name.StartsWith("timesheets", StringComparison.Ordinal));
            }
        }
        finally { CleanWorkspace(builder); File.Delete(mappingPath); }
    }

    [Fact]
    public void CandidateKeepsInternalRoutesAndReadModelTemplateScoped()
    {
        IReadOnlyList<PlatformModuleDeclaration> declarations = PlatformDeclarationSet.Load(Options());
        PlatformModuleDeclaration candidate = declarations.Single(module => PlatformDeclarationSet.Id(module.Declaration) == "timesheets");
        JsonElement[] internalSurfaces = candidate.Declaration.GetProperty("surfaces").GetProperty("interfaces").EnumerateArray()
            .Where(surface => surface.GetProperty("exposure").GetString() == "internal-only").ToArray();
        internalSurfaces.Select(surface => surface.GetProperty("routePrefix").GetString()).Order(StringComparer.Ordinal)
            .ShouldBe(["/admin", "/process", "/project", "/query", "/replay-state"]);
        foreach (JsonElement surface in internalSurfaces)
            surface.GetProperty("callers").EnumerateArray().Select(caller => caller.GetString()).ShouldBe(["eventstore"]);
        PlatformDaprBindings.Resolve(declarations).DeclaredInvocations("timesheets")
            .ShouldAllBe(grant => grant.CallerAppId == "eventstore");

        YamlStream yaml = new();
        using StreamReader reader = File.OpenText(Path.Combine(Root, "DaprComponents", "reference", "timesheets-readmodels.yaml"));
        yaml.Load(reader);
        YamlMappingNode root = (YamlMappingNode)yaml.Documents[0].RootNode;
        ((YamlMappingNode)root.Children[new YamlScalarNode("metadata")]).Children[new YamlScalarNode("name")].ToString().ShouldBe("timesheets-readmodels");
        ((YamlSequenceNode)root.Children[new YamlScalarNode("scopes")]).Children.Select(scope => scope.ToString()).ShouldBe(["timesheets"]);
        YamlMappingNode spec = (YamlMappingNode)root.Children[new YamlScalarNode("spec")];
        spec.Children[new YamlScalarNode("type")].ToString().ShouldBe("state.redis");
        Dictionary<string, string> metadata = ((YamlSequenceNode)spec.Children[new YamlScalarNode("metadata")]).Children.Cast<YamlMappingNode>()
            .ToDictionary(node => node.Children[new YamlScalarNode("name")].ToString(), node => node.Children[new YamlScalarNode("value")].ToString());
        metadata["redisDB"].ShouldBe("14");
        metadata["keyPrefix"].ShouldBe("none");
        metadata["redisHost"].ShouldBe("localhost:6379");
        metadata.ShouldNotContainKey("actorStateStore");
    }

    [Fact]
    public async Task PreparedLocalAuthenticationKeepsGatewayCallersAndFixtureDenialAsync()
    {
        const string signingKey = "runner-supplied-shared-signing-material";
        PlatformCompositionOptions options = Options() with
        {
            Configuration = new Dictionary<string, string> { ["eventstore/gateway/Authentication:JwtBearer:SigningKey"] = signingKey },
        };
        IReadOnlyList<PlatformServerBinding> servers = PlatformAppModel.Prepare(options);
        PlatformServerBinding host = servers.Single(server => server.ResourceId == "timesheets");
        IReadOnlyDictionary<string, string> hostEnvironment = host.Environment;
        IReadOnlyDictionary<string, string> gatewayEnvironment = servers.Single(server => server.ResourceId == "eventstore").Environment;

        foreach (string key in new[] { "Issuer", "Audience", "SigningKey" })
        {
            hostEnvironment["Authentication__JwtBearer__" + key].ShouldBe(gatewayEnvironment["Authentication__JwtBearer__" + key]);
            hostEnvironment["EventStore__Authentication__" + key].ShouldBe(gatewayEnvironment["Authentication__JwtBearer__" + key]);
        }
        hostEnvironment["Authentication__JwtBearer__SigningKey"].ShouldBe(signingKey);
        hostEnvironment["Authentication__JwtBearer__AllowedAlgorithms__0"].ShouldBe("HS256");
        hostEnvironment["Authentication__WorkloadIssuer__Workload"].ShouldBe("timesheets");
        hostEnvironment["Timesheets__InternalSurface__AllowOnAnyPort"].ShouldBe("false");
        hostEnvironment["Timesheets__LiveDaprFixture__Enabled"].ShouldBe("false");
        hostEnvironment["APP_API_TOKEN"].ShouldNotBe(gatewayEnvironment["APP_API_TOKEN"]);

        IConfigurationRoot hostConfiguration = Configuration(hostEnvironment);
        IConfigurationRoot gatewayConfiguration = Configuration(gatewayEnvironment);
        WorkloadAuthenticationOptions receiver = gatewayConfiguration.GetSection("Authentication:DaprInternal").Get<WorkloadAuthenticationOptions>()!;
        receiver.GetConfigurationFailure().ShouldBeNull();
        receiver.Audience.ShouldBe("eventstore");
        IReadOnlyDictionary<string, string> referenceGateway = PlatformAppModel.Prepare(options with { TimesheetsEnabled = false })
            .Single(server => server.ResourceId == "eventstore").Environment;
        string[] referenceCallers = Configuration(referenceGateway).GetSection("Authentication:DaprInternal:AllowedCallers").Get<string[]>()!;
        referenceCallers.ShouldContain("tenants");
        referenceCallers.ShouldContain("memories");
        receiver.AllowedCallers.Order(StringComparer.Ordinal).ShouldBe(referenceCallers.Append("timesheets").Order(StringComparer.Ordinal));

        WorkloadAuthenticationOptions hostReceiver = hostConfiguration.GetSection("Authentication:Workload").Get<WorkloadAuthenticationOptions>()!;
        hostReceiver.GetConfigurationFailure().ShouldBeNull();
        hostReceiver.Audience.ShouldBe("timesheets");
        hostReceiver.AllowedCallers.ShouldBe(["eventstore"]);

        IDistributedApplicationBuilder builder = Builder();
        foreach (IConfigurationRoot configuration in new[] { hostConfiguration, gatewayConfiguration })
            JwtBearerAuthenticationContract.Validate(configuration.GetSection("Authentication:JwtBearer").Get<JwtBearerAuthenticationOptions>()!, builder.Environment, "Authentication:JwtBearer").Succeeded.ShouldBeTrue();
        var owner = builder.AddExecutable("timesheets", "dotnet", Root);
        var sidecar = builder.AddExecutable("timesheets-dapr-cli", "dapr", Root).WithParentRelationship(owner)
            .WithEndpoint(port: 20101, targetPort: 20202, name: "http");
        PlatformDaprEndpointSubscriber.Apply(new DistributedApplicationModel(builder.Resources), [host]);
        Dictionary<string, object> cliEnvironment = await EnvironmentAsync(builder, sidecar.Resource);
        cliEnvironment["APP_API_TOKEN"].ShouldBe(hostEnvironment["APP_API_TOKEN"]);
    }
}
