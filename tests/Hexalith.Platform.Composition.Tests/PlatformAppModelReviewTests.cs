namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json.Nodes;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using YamlDotNet.RepresentationModel;

public sealed partial class PlatformAppModelTests
{
    [Fact]
    public void EventStoreOnlyUsesItsComponentsAndRegistersConfigStore()
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-eventstore-components-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (string file in new[] { "statestore.yaml", "pubsub.yaml", "configstore.yaml", "accesscontrol.eventstore.yaml" })
                File.Copy(Path.Combine(Root, "DaprComponents", "reference", file), Path.Combine(directory, file));
            IDistributedApplicationBuilder builder = Builder();
            PlatformAppModel.Compose(builder, Options(active: "eventstore") with { ComponentsDirectory = directory });
            builder.Resources.ShouldContain(resource => resource.Name == "configstore");
            builder.Resources.ShouldNotContain(resource => resource.Name == "llm" || resource.Name == "access-telemetry-secrets" || resource.Name == "access-telemetry-store" || resource.Name == "access-telemetry-config");
            IDaprSidecarResource sidecar = builder.Resources.Single(resource => resource.Name == "eventstore").Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
            sidecar.Annotations.OfType<DaprComponentReferenceAnnotation>().Select(reference => reference.Component.Name).ShouldContain("configstore");
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void MemoriesSidecarsReceiveConversationAndSecretComponentsOnly()
    {
        IDistributedApplicationBuilder builder = Builder();
        PlatformAppModel.Compose(builder, Options());
        foreach (IResource server in builder.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()))
        {
            IDaprSidecarResource sidecar = server.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
            string[] components = sidecar.Annotations.OfType<DaprComponentReferenceAnnotation>().Select(reference => reference.Component.Name).ToArray();
            if (server.Name == "memories")
            {
                components.ShouldContain("llm");
                components.ShouldContain("secretstore");
                components.ShouldContain("access-telemetry-secrets");
            }
            else if (server.Name.StartsWith("memories-", StringComparison.Ordinal))
            {
                components.ShouldNotContain("llm");
                components.ShouldNotContain("secretstore");
                components.ShouldContain("access-telemetry-secrets");
            }
            else
            {
                components.ShouldNotContain("llm");
                components.ShouldNotContain("secretstore");
                components.ShouldNotContain("access-telemetry-secrets");
            }
        }
        string secretStore = File.ReadAllText(Path.Combine(Root, "DaprComponents", "reference", "secretstore.yaml"));
        secretStore.ShouldContain("scopes:");
        secretStore.ShouldNotContain("  - eventstore");
        secretStore.ShouldNotContain("  - tenants");
        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        string rendered = provider.GetRequiredService<PlatformCompositionWorkspace>().Path;
        string[] Scopes(string file) => ((YamlSequenceNode)((YamlMappingNode)Yaml(File.ReadAllText(Path.Combine(rendered, file))).Documents[0].RootNode)
            .Children[new YamlScalarNode("scopes")]).Children.Select(node => node.ToString()).ToArray();
        string[] stateScopes = Scopes("statestore.yaml");
        stateScopes.ShouldBe(["eventstore", "memories", "tenants", "tenants-api"]);
        stateScopes.ShouldNotContain("memories-access-telemetry");
        Scopes("pubsub.yaml").ShouldBe(["eventstore", "memories", "tenants"]);
        Scopes("telemetry-state.yaml").ShouldBe(["memories-access-telemetry"]);
    }

    [Fact]
    public void OpaqueSecretsStayWholeAndMalformedStructuredInputsAreScoped()
    {
        PlatformCompositionOptions secret = Options(active: "eventstore") with { Configuration = new Dictionary<string, string> { ["eventstore/gateway/EventStore:AppChannelToken"] = "{\"bearer\":\"opaque\"}" } };
        PlatformAppModel.Prepare(secret).Single().Environment["EventStore__AppChannelToken"].ShouldBe("{\"bearer\":\"opaque\"}");
        PlatformCompositionOptions malformed = Options(active: "eventstore") with { Configuration = new Dictionary<string, string> { ["eventstore/gateway/Authentication:JwtBearer:ValidAudiences"] = " \n\t[oops" } };
        InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Prepare(malformed));
        refusal.Message.ShouldContain("eventstore/gateway/Authentication:JwtBearer:ValidAudiences");
        refusal.Message.ShouldContain("hexalith.module-manifest.v2.json");
    }

    [Theory]
    [InlineData(" \n\t[\"audience\"]")]
    [InlineData(" \r\n{\"0\":\"audience\"}")]
    public void StructuredConfigurationIgnoresLeadingWhitespaceAndOpaqueSecretsStayWhole(string structured)
    {
        const string opaque = " \n\t{\"bearer\":\"opaque\"}";
        PlatformCompositionOptions options = Options(active: "eventstore") with
        {
            Configuration = new Dictionary<string, string>
            {
                ["eventstore/gateway/Authentication:JwtBearer:ValidAudiences"] = structured,
                ["eventstore/gateway/EventStore:AppChannelToken"] = opaque,
            },
        };
        IReadOnlyDictionary<string, string> environment = PlatformAppModel.Prepare(options).Single().Environment;
        environment["Authentication__JwtBearer__ValidAudiences__0"].ShouldBe("audience");
        environment.ShouldNotContainKey("Authentication__JwtBearer__ValidAudiences");
        environment["EventStore__AppChannelToken"].ShouldBe(opaque);
        environment["APP_API_TOKEN"].ShouldBe(opaque);
    }

    [Theory]
    [InlineData("eventstore", "gateway", "tenants")]
    [InlineData("tenants", "domain", "eventstore")]
    [InlineData("memories", "server", "eventstore")]
    public void SourceProjectSymlinkIntoAnotherMappedOwnerRefusesAtomically(string module, string server, string targetOwner)
    {
        using PlatformOwnerFixture fixture = PlatformOwnerFixture.Copy(Root, module);
        string owner = fixture.Root;
        try
        {
            PlatformCompositionOptions options = Options(active: module);
            string repository = PlatformServerBindings.Repository(module);
            string project = Path.Combine(owner, PlatformServerBindings.ReferenceProjects[module + "/" + server]);
            Directory.CreateDirectory(Path.GetDirectoryName(project)!);
            string targetServer = targetOwner == "tenants" ? "domain" : "gateway";
            File.Delete(project);
            File.CreateSymbolicLink(project, Path.Combine(Root, "references", PlatformServerBindings.Repository(targetOwner), PlatformServerBindings.ReferenceProjects[targetOwner + "/" + targetServer]));
            options = options with { Mapping = options.Mapping with { Entries = options.Mapping.Entries.Select(entry => entry.Identity == repository ? entry with { Path = owner } : entry).ToArray() } };
            options.Mapping.ContainsProject(project).ShouldBeTrue("the complete mapping admits the other owner's physical project");
            IDistributedApplicationBuilder builder = Builder();
            InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options));
            refusal.Message.ShouldContain(module + "/" + server);
            refusal.Message.ShouldContain(project);
            refusal.Message.ShouldContain("physically outside its owner root");
            builder.Resources.ShouldBeEmpty();
        }
        finally { File.Delete(Path.Combine(owner, PlatformServerBindings.ReferenceProjects[module + "/" + server])); }
    }

    [Fact]
    public void EmptyFileSelectionRefusesWithItsConfigurationKey()
    {
        IDistributedApplicationBuilder builder = Builder();
        builder.Environment.EnvironmentName = Environments.Development;
        builder.Configuration["Platform:ActiveModule"] = "";
        Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder)).Message.ShouldContain("Platform:ActiveModule");
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void DependencyCycleRefusesBeforeResourceCreation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-cycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Fixture(directory, "parties", tasks: false);
            JsonNode document = JsonNode.Parse(File.ReadAllText(path))!;
            document["modules"]![0]!["identity"]!["dependencies"] = new JsonArray("parties");
            File.WriteAllText(path, document.ToJsonString());
            IDistributedApplicationBuilder builder = Builder();
            InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, Options(active: "parties") with { DeclarationPaths = [path] }));
            refusal.Message.ShouldContain("identity.dependencies");
            refusal.Message.ShouldContain("cycle");
            builder.Resources.ShouldBeEmpty();
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void UnsupportedDaprCapabilityRefusesInsteadOfUsingStateStore()
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-capability-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Fixture(directory, "parties", tasks: false);
            JsonNode document = JsonNode.Parse(File.ReadAllText(path))!;
            document["modules"]![0]!["runtime"]!["dapr"]![0]!["capability"] = "workflow";
            document["modules"]![0]!["integration"]!["topics"] = new JsonArray();
            File.WriteAllText(path, document.ToJsonString());
            Hexalith.Builds.Tooling.Manifest.PlatformManifestValidationResult validation = Hexalith.Builds.Tooling.Manifest.PlatformManifestValidator.Validate([path], TestContext.Current.CancellationToken);
            validation.IsValid.ShouldBeTrue(string.Join("; ", validation.Diagnostics.Select(diagnostic => diagnostic.Message)));
            PlatformCompositionOptions options = Options() with { Configuration = new Dictionary<string, string> { ["parties/api/Sample:Setting"] = "bound" } };
            InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformLocalProfile.Bind(options, validation.Declarations!.Single(), "api"));
            refusal.Message.ShouldContain("workflow");
            refusal.Message.ShouldContain("runtime.dapr");
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void LocalSecretsAreIndependentPerInputAndFreshPerComposition()
    {
        IReadOnlyList<PlatformServerBinding> first = PlatformAppModel.Prepare(Options());
        IReadOnlyList<PlatformServerBinding> second = PlatformAppModel.Prepare(Options());
        IReadOnlyDictionary<string, string> gateway = first.Single(server => server.ResourceId == "eventstore").Environment;
        IReadOnlyDictionary<string, string> domain = first.Single(server => server.ResourceId == "tenants").Environment;
        IReadOnlyDictionary<string, string> api = first.Single(server => server.ResourceId == "tenants-api").Environment;
        gateway["Authentication__JwtBearer__SigningKey"].ShouldBe(domain["Authentication__JwtBearer__SigningKey"]);
        gateway["Authentication__JwtBearer__SigningKey"].ShouldBe(api["EventStore__Authentication__SigningKey"]);
        gateway["Authentication__JwtBearer__SigningKey"].ShouldNotBe(gateway["EventStore__AppChannelToken"]);
        domain["Tenants__AppChannelToken"].ShouldNotBe(api["Tenants__ApiDaprToken"]);
        domain["EventStore__Authentication__Password"].ShouldBe(api["EventStore__Authentication__Password"]);
        first.Single(server => server.ResourceId == "memories").Environment["Memories__AppChannelToken"]
            .ShouldNotBe(first.Single(server => server.ResourceId == "memories").Environment["Memories__DaprApiToken"]);
        gateway["Authentication__JwtBearer__SigningKey"].ShouldNotBe(second.Single(server => server.ResourceId == "eventstore").Environment["Authentication__JwtBearer__SigningKey"]);
        gateway["EventStore__AppChannelToken"].ShouldNotBe(second.Single(server => server.ResourceId == "eventstore").Environment["EventStore__AppChannelToken"]);
    }

    [Fact]
    public void ClockTokensBindBySourceIdAndRejectUnknownOrMissingIds()
    {
        PlatformCompositionOptions options = Options(active: "memories") with
        {
            Configuration = new Dictionary<string, string>
            {
                ["memories/clock/Clock:Sources"] = "[{\"Id\":\"second\",\"Endpoint\":\"https://second.example\"},{\"Id\":\"first\",\"Endpoint\":\"https://first.example\"}]",
                ["memories/clock/Clock:SourceAuthenticationTokens"] = "{\"first\":\"first-token\",\"second\":\"second-token\"}",
            },
        };
        IReadOnlyDictionary<string, string> environment = PlatformAppModel.Prepare(options).Single(server => server.ResourceId == "memories-access-telemetry-clock").Environment;
        environment["Clock__Sources__0__AuthenticationToken"].ShouldBe("second-token");
        environment["Clock__Sources__1__AuthenticationToken"].ShouldBe("first-token");
        environment.ShouldNotContainKey("Clock__SourceAuthenticationTokens__first");
        string sources = options.Configuration["memories/clock/Clock:Sources"];
        JsonArray inlineSources = JsonNode.Parse(sources)!.AsArray();
        inlineSources[0]!["AuthenticationToken"] = "inline-token";
        foreach ((string clockSources, string tokens, string field, string reason) in new[]
        {
            (sources, "{\"missing\":\"token\"}", "Clock:SourceAuthenticationTokens:missing", "no source has this ID"),
            (sources, "{\"first\":\"token\"}", "Clock:Sources:second", "authentication token is unavailable"),
            (inlineSources.ToJsonString(), "{\"first\":\"token\"}", "Clock:Sources:second", "authentication token is unavailable"),
            (sources, "{\"first\":\"token\",\"second\":\"\"}", "Clock:SourceAuthenticationTokens:second", "token is unavailable"),
            (sources, JsonSerializer.Serialize(new { first = "token", second = " \t " }), "Clock:SourceAuthenticationTokens:second", "token is unavailable"),
        })
        {
            IDistributedApplicationBuilder builder = Builder();
            InvalidDataException refusal = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options with
            {
                Configuration = new Dictionary<string, string>(options.Configuration)
                {
                    ["memories/clock/Clock:Sources"] = clockSources,
                    ["memories/clock/Clock:SourceAuthenticationTokens"] = tokens,
                },
            }));
            refusal.Message.ShouldContain("hexalith.module-manifest.v2.json");
            refusal.Message.ShouldContain("memories/clock/" + field);
            refusal.Message.ShouldContain(reason);
            builder.Resources.ShouldBeEmpty();
        }
    }

    [Fact]
    public void PackageSnapshotRejectsChangedBytesAndRepositoryBeforeResources()
    {
        PlatformCompositionOptions options = PackageOptions();
        PlatformDeclarationSet.Load(options).Count.ShouldBe(3);
        string declarations = Path.Combine(options.PlatformRoot, "src", "Hexalith.Platform.Composition", "Declarations");
        string tenants = Path.Combine(declarations, "tenants.json");
        byte[] original = File.ReadAllBytes(tenants);
        File.AppendAllText(tenants, " ");
        Should.Throw<InvalidDataException>(() => PlatformDeclarationSet.Load(options)).Message.ShouldContain("differs from its source provenance");
        File.WriteAllBytes(tenants, original);
        JsonNode provenance = JsonNode.Parse(File.ReadAllText(Path.Combine(declarations, "provenance.json")))!;
        provenance["eventstore"]!["repository"] = "Hexalith.Other";
        File.WriteAllText(Path.Combine(declarations, "provenance.json"), provenance.ToJsonString());
        Should.Throw<InvalidDataException>(() => PlatformDeclarationSet.Load(options)).Message.ShouldContain("differs from its source provenance");
    }

    [Fact]
    public void ComposedDaprCapabilitiesKeepOwnerLiteralNames()
    {
        Dictionary<string, string> names = new(StringComparer.Ordinal)
        {
            ["rate-limit-configuration"] = "configstore", ["telemetry-state"] = "access-telemetry-store",
            ["telemetry-secrets"] = "access-telemetry-secrets",
        };
        foreach (PlatformServerBinding server in PlatformAppModel.Prepare(Options()))
        foreach (JsonElement capability in server.Module.Declaration.GetProperty("runtime").GetProperty("dapr").EnumerateArray())
        {
            if (capability.TryGetProperty("servers", out var scopes) && !scopes.EnumerateArray().Any(scope => scope.GetString() == server.ServerId)) continue;
            string role = capability.GetProperty("role").GetString()!;
            string kind = capability.GetProperty("capability").GetString()!;
            string expected = names.TryGetValue(role, out string? special) ? special : kind switch
            {
                "pubsub" => "pubsub", "configuration" => "access-telemetry-config", "secret" => "secretstore",
                "conversation" => "llm", _ => "statestore",
            };
            server.Environment[capability.GetProperty("configurationKey").GetString()!.Replace(":", "__", StringComparison.Ordinal)].ShouldBe(expected);
        }
    }

    [Fact]
    public void FileEntryHonorsActiveModuleAndRefusesChangedMappingHash()
    {
        string mappingPath = Path.Combine(Path.GetTempPath(), "platform-file-mapping-" + Guid.NewGuid().ToString("N") + ".json");
        SourceMapping mapping = Options().Mapping;
        File.WriteAllText(mappingPath, JsonSerializer.Serialize(mapping));
        try
        {
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
            {
                Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name,
                ProjectDirectory = Root, DisableDashboard = true, AllowUnsecuredTransport = true,
            });
            builder.Environment.EnvironmentName = Environments.Development;
            builder.Configuration["Platform:ActiveModule"] = "eventstore";
            builder.Configuration["Platform:SourceMapping:Path"] = mappingPath;
            builder.Configuration["Platform:SourceMapping:Hash"] = mapping.ContentHash;
            PlatformAppModel.Compose(builder);
            builder.Resources.Count(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).ShouldBe(1);
            builder.Services.ShouldContain(service => service.ServiceType == typeof(Aspire.Hosting.Lifecycle.IDistributedApplicationEventingSubscriber)
                && service.ImplementationInstance is PlatformDaprEndpointSubscriber);
            using (ServiceProvider provider = builder.Services.BuildServiceProvider())
                provider.GetRequiredService<PlatformCompositionWorkspace>().Dispose();

            IDistributedApplicationBuilder refused = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
            {
                Args = [], AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name,
                ProjectDirectory = Root, DisableDashboard = true, AllowUnsecuredTransport = true,
            });
            refused.Environment.EnvironmentName = Environments.Development;
            refused.Configuration["Platform:SourceMapping:Path"] = mappingPath;
            refused.Configuration["Platform:SourceMapping:Hash"] = "different";
            Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(refused)).Message.ShouldContain("differs from the command-resolved hash");
            refused.Resources.ShouldBeEmpty();
        }
        finally { File.Delete(mappingPath); }
    }

    [Fact]
    public void FileEntryRefusesNonDevelopmentBeforeComposing()
    {
        IDistributedApplicationBuilder builder = Builder();
        builder.Environment.EnvironmentName = Environments.Production;
        Should.Throw<InvalidOperationException>(() => PlatformAppModel.Compose(builder)).Message
            .ShouldBe("Platform declaration composition requires a Development host. A production profile is unavailable.");
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void BuildsReflectionContractNamesAndSignatureRemainResolvable()
    {
        typeof(PlatformAppModel).GetMethod("ComposeRun", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            [typeof(IDistributedApplicationBuilder), typeof(CompositionRunPlan), typeof(string), typeof(string)])
            .ShouldNotBeNull();
        typeof(PlatformServerAnnotation).Name.ShouldBe("PlatformServerAnnotation");
        typeof(PlatformServerAnnotation).GetProperty("Binding")!.PropertyType.ShouldBe(typeof(PlatformServerBinding));
        typeof(PlatformServerBinding).GetProperty("ReadinessEndpoint")!.PropertyType.ShouldBe(typeof(string));
    }

    [Fact]
    public async Task SignedPackageCompositionUsesSeedAndRefusesCatalogMappingDriftAsync()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "platform-package-plan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(workspace, "source-mapping"));
        try
        {
            PlatformCompositionOptions release = PackageOptions();
            SourceMapping mapping = release.Mapping with
            {
                Entries = [.. release.Mapping.Entries, new SourceMappingEntry("Hexalith.Platform", "source", release.PlatformRoot)],
            };
            CompositionRunPlan plan = CompositionRunPlanFactory.Create(
                CompositionRunPlanFactory.NewRunId(), workspace, workspace, CompositionPortAllocator.Allocate(1), [], [],
                mapping, [Path.Combine(release.PlatformRoot, "src", "Hexalith.Platform.Composition", "Declarations", "eventstore.json")],
                null, typeof(PlatformAppModel).Assembly.Location);
            await CompositionDaprComponentRenderer.WriteAsync(plan, TestContext.Current.CancellationToken);
            Dictionary<string, string> versions = new(StringComparer.Ordinal)
            {
                ["Hexalith.EventStore.Aspire"] = "3.120.0", ["Hexalith.Tenants.Server"] = "5.8.0",
                ["Hexalith.Memories.Aspire"] = "2.27.1",
            };
            CatalogVersionSnapshot Snapshot(string hash) => new(hash, "test-pin", "test-catalog", new Dictionary<string, string>(),
                versions, new Dictionary<string, IReadOnlyDictionary<string, string>>(),
                CatalogVersionSnapshot.ComputeHash(hash, "test-pin", "test-catalog", new Dictionary<string, string>(), versions, new Dictionary<string, IReadOnlyDictionary<string, string>>()));
            string catalogPath = Path.Combine(workspace, "source-mapping", "catalog-versions.json");
            File.WriteAllText(catalogPath, JsonSerializer.Serialize(Snapshot(mapping.ContentHash)));
            PlatformCompositionOptions Configure(PlatformCompositionOptions options) => options with
            {
                ReleaseCommits = release.ReleaseCommits,
                ReleaseSnapshotHashes = release.ReleaseSnapshotHashes,
                ComponentsDirectory = release.ComponentsDirectory,
                ImageProbeRunner = (_, _, _) => Task.FromResult(new CompositionProcessResult(true, 0, "{}", false)),
            };
            IDistributedApplicationBuilder builder = Builder();
            PlatformAppModel.ComposeRun(builder, plan, CompositionSigningKey.Create(), release.EventStoreSeedProject, Configure);
            builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore")
                .GetProjectMetadata().ProjectPath.ShouldBe(release.EventStoreSeedProject);
            builder.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).Count().ShouldBe(6);
            File.WriteAllText(catalogPath, JsonSerializer.Serialize(Snapshot("wrong-mapping")));
            IDistributedApplicationBuilder refused = Builder();
            Should.Throw<InvalidDataException>(() => PlatformAppModel.ComposeRun(refused, plan, CompositionSigningKey.Create(), release.EventStoreSeedProject, Configure))
                .Message.ShouldContain("catalog snapshot differs from the selected mapping");
            refused.Resources.ShouldBeEmpty();
        }
        finally { Directory.Delete(workspace, true); }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(0, true)]
    public void RegistryRefusalIsAtomicAndIdentifiesTheOwnerServerImage(int exit, bool timedOut)
    {
        PlatformCompositionOptions options = PackageOptions() with { ImageProbeRunner = (_, _, _) => Task.FromResult(new CompositionProcessResult(true, exit, "unavailable", timedOut)) };
        IDistributedApplicationBuilder builder = Builder();
        InvalidDataException failure = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options));
        failure.Message.ShouldBe($"{Path.GetRelativePath(Directory.GetCurrentDirectory(), Path.Combine(options.PlatformRoot, "src", "Hexalith.Platform.Composition", "Declarations", "tenants.json"))}: tenants/domain: image 'registry.hexalith.com/tenants:5.8.0' is unavailable from its released registry.");
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public async Task SignedCompositionKeepsRunDiscoveryPortsSharedStateAndApiCredentialsAsync()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "platform-signed-review-" + Guid.NewGuid().ToString("N"));
        try
        {
            PlatformCompositionOptions options = Options();
            SourceMapping mapping = options.Mapping with { Entries = [.. options.Mapping.Entries, new SourceMappingEntry("Hexalith.Platform", "source", Root)] };
            CompositionRunPlan plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), workspace, workspace, CompositionPortAllocator.Allocate(1), [], [], mapping, [Path.Combine(Root, "references", "Hexalith.EventStore", "module", "hexalith.module-manifest.v2.json")], null, typeof(PlatformAppModel).Assembly.Location);
            await CompositionDaprComponentRenderer.WriteAsync(plan, TestContext.Current.CancellationToken);
            IDistributedApplicationBuilder builder = Builder();
            int hostedServicesBeforeCompose = builder.Services.Count(service => service.ServiceType == typeof(IHostedService));
            string key = CompositionSigningKey.Create();
            PlatformAppModel.ComposeRun(builder, plan, key, null);
            builder.Services.ShouldNotContain(service => service.ServiceType == typeof(PlatformCompositionWorkspace));
            builder.Services.Count(service => service.ServiceType == typeof(IHostedService)).ShouldBe(hostedServicesBeforeCompose);
            foreach (IResource server in builder.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()))
            {
                Dictionary<string, object> runtime = new();
                foreach (EnvironmentCallbackAnnotation callback in server.Annotations.OfType<EnvironmentCallbackAnnotation>())
                    await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, server, runtime, TestContext.Current.CancellationToken));
                runtime["Dapr__DeclarativeSubscriptions"].ShouldBe("true", server.Name);
                runtime["Authentication__JwtBearer__SigningKey"].ShouldBe(key);
                runtime["Authentication__JwtBearer__Issuer"].ShouldBe(CompositionEnvironment.TokenIssuer);
                runtime["Authentication__JwtBearer__Audience"].ShouldBe(CompositionEnvironment.TokenAudience);
                if (server.Name is "tenants" or "tenants-api")
                {
                    runtime["EventStore__Authentication__SigningKey"].ShouldBe(key);
                    runtime["Jwt__SigningKey"].ShouldBe(key);
                }
                DaprSidecarOptions sidecar = server.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
                YamlMappingNode spec = Spec(File.ReadAllText(sidecar.Config!));
                YamlMappingNode resolution = (YamlMappingNode)spec.Children[new YamlScalarNode("nameResolution")];
                resolution.Children[new YamlScalarNode("component")].ToString().ShouldBe("sqlite");
                ((YamlMappingNode)resolution.Children[new YamlScalarNode("configuration")]).Children[new YamlScalarNode("connectionString")].ToString().ShouldBe(Path.Combine(Path.GetDirectoryName(plan.DaprConfigPath)!, "name-resolution.db").Replace('\\', '/'));
                spec.Children.ShouldContainKey(new YamlScalarNode("accessControl"));
                Path.GetFileName(sidecar.Config).ShouldBe("accesscontrol." + server.Annotations.OfType<PlatformServerAnnotation>().Single().Binding.AppId + ".yaml");
                if (server.Name != "eventstore")
                    new[] { sidecar.DaprHttpPort!.Value, sidecar.DaprGrpcPort!.Value, sidecar.DaprInternalGrpcPort!.Value, sidecar.MetricsPort!.Value }.ShouldAllBe(port => !plan.Ports.ToList().Contains(port));
            }
            string runLabel = $"{CompositionEnvironment.ContainerRunLabel}={plan.RunId}";
            foreach (ContainerResource container in builder.Resources.OfType<ContainerResource>().Where(resource => resource.Name is "redis" or "memories-vectors" or "memories-graph" or "dapr-sentry" or "dapr-placement-mtls" or "dapr-scheduler-mtls"))
            {
                List<object> runArguments = [];
                foreach (ContainerRuntimeArgsCallbackAnnotation callback in container.Annotations.OfType<ContainerRuntimeArgsCallbackAnnotation>())
                    await callback.Callback(new ContainerRuntimeArgsCallbackContext(runArguments, TestContext.Current.CancellationToken));
                runArguments.Select(argument => argument.ToString()).ShouldContain(runLabel, container.Name);
            }
            builder.Resources.OfType<ContainerResource>().Single(resource => resource.Name == "redis").Annotations.OfType<ContainerNameAnnotation>().Single().Name.ShouldBe(plan.RedisContainerName);
            ContainerMountAnnotation schedulerData = builder.Resources.Single(resource => resource.Name == "dapr-scheduler-mtls")
                .Annotations.OfType<ContainerMountAnnotation>().Single(mount => mount.Target == "/var/lock");
            schedulerData.Type.ShouldBe(ContainerMountType.BindMount);
            schedulerData.Source.ShouldBe(Path.Combine(workspace, "platform-components", "scheduler-data"));
            schedulerData.IsReadOnly.ShouldBeFalse();
            Directory.Exists(schedulerData.Source).ShouldBeTrue();
            ProjectResource gatewayProject = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "eventstore");
            Dictionary<string, object> gatewayEnvironment = new();
            foreach (EnvironmentCallbackAnnotation callback in gatewayProject.Annotations.OfType<EnvironmentCallbackAnnotation>())
                await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, gatewayProject, gatewayEnvironment, TestContext.Current.CancellationToken));
            gatewayEnvironment["EventStore__DomainServices__Registrations__wildcard_tenants_v1__AppId"].ShouldBe("tenants");
            gatewayEnvironment["EventStore__DomainServices__Registrations__wildcard_tenants_v1__MethodName"].ShouldBe("process");
            gatewayEnvironment["EventStore__DomainServices__Registrations__wildcard_tenants_v1__TenantId"].ShouldBe("*");
            gatewayEnvironment["EventStore__DomainServices__Registrations__wildcard_tenants_v1__Domain"].ShouldBe("tenants");
            gatewayEnvironment["EventStore__DomainServices__Registrations__wildcard_tenants_v1__Version"].ShouldBe("v1");
            gatewayEnvironment.ShouldNotContainKey("EventStore__CommandGateway__BaseAddress");
            foreach (string dependent in new[] { "tenants", "tenants-api" })
            {
                ProjectResource project = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == dependent);
                project.Annotations.OfType<EnvironmentCallbackAnnotation>().Count().ShouldBeGreaterThan(0);
                project.Annotations.OfType<WaitAnnotation>().Select(wait => wait.Resource.Name).ShouldContain("eventstore", dependent);
                Dictionary<string, object> dependentEnvironment = new();
                foreach (EnvironmentCallbackAnnotation callback in project.Annotations.OfType<EnvironmentCallbackAnnotation>())
                    await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, project, dependentEnvironment, TestContext.Current.CancellationToken));
                dependentEnvironment.ShouldContainKey("EventStore__CommandGateway__BaseAddress");
            }
            ProjectResource api = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "tenants-api");
            Dictionary<string, object> environment = new();
            foreach (EnvironmentCallbackAnnotation callback in api.Annotations.OfType<EnvironmentCallbackAnnotation>())
                await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, api, environment, TestContext.Current.CancellationToken));
            environment["EventStore__GatewayReadiness__BaseAddress"].ShouldBeOfType<EndpointReference>().Resource.ShouldBe(gatewayProject);
            environment["EventStore__Authentication__SigningKey"].ShouldBe(key);
            environment["EventStore__Authentication__Issuer"].ShouldBe(CompositionEnvironment.TokenIssuer);
            environment["EventStore__Authentication__Audience"].ShouldBe(CompositionEnvironment.TokenAudience);
            string stateStore = Path.Combine(workspace, "platform-components", "statestore.yaml");
            YamlSequenceNode metadata = (YamlSequenceNode)Spec(File.ReadAllText(stateStore)).Children[new YamlScalarNode("metadata")];
            metadata.Children.OfType<YamlMappingNode>().Single(node => node.Children[new YamlScalarNode("name")].ToString() == "keyPrefix").Children[new YamlScalarNode("value")].ToString().ShouldBe("none");
            ((YamlSequenceNode)((YamlMappingNode)Yaml(File.ReadAllText(stateStore)).Documents[0].RootNode).Children[new YamlScalarNode("scopes")]).Children.Select(node => node.ToString()).ShouldContain("tenants");
            string[] shared = ["statestore.yaml", "pubsub.yaml", "telemetry-config.yaml"];
            foreach (string file in shared)
                File.ReadAllText(Path.Combine(workspace, "platform-components", file)).ShouldContain("127.0.0.1:" + plan.Ports.Redis);
            int? vectorEndpoint = builder.Resources.OfType<ContainerResource>().Single(resource => resource.Name == "memories-vectors")
                .Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "tcp").Port;
            File.ReadAllText(Path.Combine(workspace, "platform-components", "telemetry-state.yaml")).ShouldContain("127.0.0.1:" + vectorEndpoint);
        }
        finally { if (Directory.Exists(workspace)) Directory.Delete(workspace, true); }
    }

    [Fact]
    public void SecondaryPortAllocationRetriesPersistedCollisions()
    {
        CompositionRunPorts persisted = CompositionPortAllocator.Allocate(2);
        CompositionRunPorts independent = CompositionPortAllocator.Allocate(2);
        int attempts = 0;
        PlatformAppModel.AllocateSecondaryPorts(2, persisted, count => { count.ShouldBe(2); return ++attempts == 1 ? independent with { Redis = persisted.Scheduler } : independent; }).ShouldBe(independent);
        attempts.ShouldBe(2);
    }

    [Fact]
    public async Task TwoServerTechnicalSelectionWithoutEventStoreHasDistinctSidecarsAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-technical-review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string manifest = Fixture(directory, "technical", tasks: false);
            JsonNode document = JsonNode.Parse(File.ReadAllText(manifest))!;
            JsonNode module = document["modules"]![0]!;
            JsonNode second = module["identity"]!["servers"]![0]!.DeepClone();
            second["id"] = "worker"; second["resourceId"] = "technical-worker"; second["appId"] = "technical-worker";
            module["identity"]!["servers"]!.AsArray().Add(second);
            JsonNode ready = module["lifecycle"]!["readiness"]![0]!.DeepClone(); ready["server"] = "worker";
            module["lifecycle"]!["readiness"]!.AsArray().Add(ready);
            File.WriteAllText(manifest, document.ToJsonString());
            File.WriteAllText(Path.Combine(directory, "server.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
            string components = Path.Combine(directory, "components");
            Directory.CreateDirectory(components);
            File.Copy(Path.Combine(Root, "DaprComponents", "reference", "pubsub.yaml"), Path.Combine(components, "pubsub.yaml"));
            PlatformCompositionOptions original = Options();
            PlatformCompositionOptions options = original with
            {
                Mapping = original.Mapping with { Entries = [.. original.Mapping.Entries, new SourceMappingEntry("Hexalith.Technical", "source", directory)] },
                ActiveModule = "technical", DomainModule = false, DeclarationPaths = [manifest],
                ComponentsDirectory = components,
                Projects = new Dictionary<string, string>(original.Projects) { ["technical/api"] = "server.csproj", ["technical/worker"] = "server.csproj" },
                Configuration = new Dictionary<string, string> { ["technical/api/Sample:Setting"] = "value", ["technical/worker/Sample:Setting"] = "value" },
            };
            IDistributedApplicationBuilder builder = Builder();
            PlatformAppModel.Compose(builder, options);
            IResource[] servers = builder.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).ToArray();
            servers.Length.ShouldBe(2); servers.ShouldNotContain(server => server.Name == "eventstore");
            builder.Resources.ShouldContain(resource => resource.Name == "pubsub");
            builder.Resources.ShouldNotContain(resource => resource.Name == "statestore");
            foreach (IResource server in servers)
                server.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprComponentReferenceAnnotation>()
                    .Select(reference => reference.Component.Name).ShouldBe(["pubsub"]);
            servers.SelectMany(server => { DaprSidecarOptions sidecar = server.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options; return new[] { sidecar.DaprHttpPort, sidecar.DaprGrpcPort, sidecar.DaprInternalGrpcPort, sidecar.MetricsPort }; }).Distinct().Count().ShouldBe(8);
            await using ServiceProvider provider = builder.Services.BuildServiceProvider();
            PlatformCompositionWorkspace workspace = provider.GetRequiredService<PlatformCompositionWorkspace>();
            YamlMappingNode pubSub = (YamlMappingNode)Yaml(File.ReadAllText(Path.Combine(workspace.Path, "pubsub.yaml"))).Documents[0].RootNode;
            string[] scopes = ((YamlSequenceNode)pubSub.Children[new YamlScalarNode("scopes")]).Children.Select(node => node.ToString()).ToArray();
            scopes.ShouldContain("technical-api");
            scopes.ShouldContain("technical-worker");
            workspace.Dispose();
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void UnsupportedOptionalReadinessIsAnAtomicScopedDiagnostic(int count)
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-readiness-review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string manifest = Fixture(directory, "probe", tasks: false);
            JsonNode document = JsonNode.Parse(File.ReadAllText(manifest))!;
            JsonNode module = document["modules"]![0]!;
            module["identity"]!["servers"]![0]!["required"] = false;
            JsonNode ready = module["lifecycle"]!["readiness"]![0]!.DeepClone();
            module["lifecycle"]!["readiness"] = new JsonArray(Enumerable.Range(0, count).Select(_ => ready.DeepClone()).ToArray());
            File.WriteAllText(manifest, document.ToJsonString());
            File.WriteAllText(Path.Combine(directory, "server.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
            PlatformCompositionOptions original = Options();
            PlatformCompositionOptions options = original with { Mapping = original.Mapping with { Entries = [.. original.Mapping.Entries, new SourceMappingEntry("Hexalith.Probe", "source", directory)] }, ActiveModule = "probe", DomainModule = false, DeclarationPaths = [manifest], Projects = new Dictionary<string, string>(original.Projects) { ["probe/api"] = "server.csproj" } };
            IDistributedApplicationBuilder builder = Builder();
            Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options)).Message.ShouldBe($"{Path.GetRelativePath(Directory.GetCurrentDirectory(), manifest)}: probe/api: lifecycle.readiness: this Platform model requires exactly one readiness check per enabled server.");
            builder.Resources.ShouldBeEmpty();
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void PackageModeRefusesAConflictingSuppliedOwnerBeforeDeduplication()
    {
        PlatformCompositionOptions options = PackageOptions();
        PlatformAppModel.Prepare(options).Count.ShouldBe(6);
        string file = Path.Combine(options.PlatformRoot, "supplied-eventstore.json");
        string original = File.ReadAllText(Path.Combine(options.PlatformRoot, "src", "Hexalith.Platform.Composition", "Declarations", "eventstore.json"));
        JsonNode document = JsonNode.Parse(original)!;
        document["modules"]![0]!["lifecycle"]!["readiness"]![0]!["endpoint"] = "/different-ready";
        File.WriteAllText(file, document.ToJsonString());
        IDistributedApplicationBuilder builder = Builder();
        InvalidDataException failure = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options with { DeclarationPaths = [file] }));
        failure.Message.ShouldContain(file + ": eventstore: modules[0]:");
        failure.Message.ShouldContain("authoritative release snapshot"); builder.Resources.ShouldBeEmpty();
        File.WriteAllText(file, original);
        PlatformAppModel.Prepare(options with { DeclarationPaths = [file] }).Count.ShouldBe(6);
    }

    [Fact]
    public void SecretsYamlRoundTripsWindowsPaths()
    {
        string windows = "C:\\Users\\Owner Name\\keys\\quoted\"path\\secrets.json";
        string yaml = PlatformAppModel.RenderSecretStore(File.ReadAllText(Path.Combine(Root, "DaprComponents", "reference", "secretstore.yaml")), windows);
        YamlSequenceNode metadata = (YamlSequenceNode)Spec(yaml).Children[new YamlScalarNode("metadata")];
        metadata.Children.OfType<YamlMappingNode>().Single(node => node.Children[new YamlScalarNode("name")].ToString() == "secretsFile").Children[new YamlScalarNode("value")].ToString().ShouldBe(windows);
    }

    [Fact]
    public async Task FileCompositionOwnsPrivateSecretsAndDeletesItsWorkspaceOnHostDisposalAsync()
    {
        IDistributedApplicationBuilder builder = Builder();
        PlatformAppModel.Compose(builder, Options(active: "eventstore"));
        await using ServiceProvider provider = builder.Services.BuildServiceProvider();
        PlatformCompositionWorkspace owned = provider.GetRequiredService<PlatformCompositionWorkspace>();
        string path = owned.Path;
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(path).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.GetUnixFileMode(Path.Combine(path, "gateway")).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.GetUnixFileMode(Path.Combine(path, "secrets.json")).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        await provider.DisposeAsync();
        Directory.Exists(path).ShouldBeFalse();
    }

    [Fact]
    public void FailedFileCompositionDeletesItsPrivateWorkspace()
    {
        using PlatformOwnerFixture fixture = PlatformOwnerFixture.Copy(Root, "eventstore");
        string directory = fixture.Root;
        try
        {
            File.WriteAllText(Path.Combine(directory, "gateway.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
            File.WriteAllText(Path.Combine(directory, "appsettings.json"), "invalid JSON");
            PlatformCompositionOptions original = Options(active: "eventstore");
            PlatformCompositionOptions options = original with { Mapping = original.Mapping with { Entries = original.Mapping.Entries.Select(entry => entry.Identity == "Hexalith.EventStore" ? entry with { Path = directory } : entry).ToArray() }, Projects = new Dictionary<string, string>(original.Projects) { ["eventstore/gateway"] = "gateway.csproj" } };
            string tempRoot = Path.Combine(Path.GetTempPath(), "hexalith-platform");
            string[] before = Directory.Exists(tempRoot) ? Directory.GetDirectories(tempRoot).Order(StringComparer.Ordinal).ToArray() : [];
            IDistributedApplicationBuilder builder = Builder();
            InvalidDataException failure = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options));
            failure.Message.ShouldContain("appsettings: JSON is invalid");
            builder.Resources.ShouldBeEmpty();
            Directory.GetDirectories(tempRoot).Order(StringComparer.Ordinal).ShouldBe(before);
        }
        finally { File.Delete(Path.Combine(directory, "appsettings.json")); }
    }

    private static YamlStream Yaml(string text) { YamlStream yaml = new(); yaml.Load(new StringReader(text)); return yaml; }
    private static YamlMappingNode Spec(string text) => (YamlMappingNode)((YamlMappingNode)Yaml(text).Documents[0].RootNode).Children[new YamlScalarNode("spec")];
}
