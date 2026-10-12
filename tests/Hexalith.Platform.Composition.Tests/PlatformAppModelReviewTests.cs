namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json.Nodes;
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
    [Theory]
    [InlineData(1, false)]
    [InlineData(0, true)]
    public void RegistryRefusalIsAtomicAndIdentifiesTheOwnerServerImage(int exit, bool timedOut)
    {
        PlatformCompositionOptions options = Options(WorkspaceMode.Package) with { ImageProbeRunner = (_, _, _) => Task.FromResult(new CompositionProcessResult(true, exit, "unavailable", timedOut)) };
        IDistributedApplicationBuilder builder = Builder();
        InvalidDataException failure = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options));
        failure.Message.ShouldBe($"{Path.GetRelativePath(Directory.GetCurrentDirectory(), Path.Combine(Root, "src", "Hexalith.Platform.Composition", "Declarations", "tenants.json"))}: tenants/domain: image 'registry.hexalith.com/tenants:5.8.0' is unavailable from its released registry.");
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
            string key = CompositionSigningKey.Create();
            PlatformAppModel.ComposeRun(builder, plan, key, null);
            foreach (IResource server in builder.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()))
            {
                DaprSidecarOptions sidecar = server.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
                YamlMappingNode spec = Spec(File.ReadAllText(sidecar.Config!));
                YamlMappingNode resolution = (YamlMappingNode)spec.Children[new YamlScalarNode("nameResolution")];
                resolution.Children[new YamlScalarNode("component")].ToString().ShouldBe("sqlite");
                ((YamlMappingNode)resolution.Children[new YamlScalarNode("configuration")]).Children[new YamlScalarNode("connectionString")].ToString().ShouldBe(Path.Combine(Path.GetDirectoryName(plan.DaprConfigPath)!, "name-resolution.db").Replace('\\', '/'));
                if (server.Name is "eventstore" or "tenants") spec.Children.ShouldContainKey(new YamlScalarNode("accessControl"));
                else sidecar.Config.ShouldBe(plan.DaprConfigPath);
                if (server.Name != "eventstore")
                    new[] { sidecar.DaprHttpPort!.Value, sidecar.DaprGrpcPort!.Value, sidecar.DaprInternalGrpcPort!.Value, sidecar.MetricsPort!.Value }.ShouldAllBe(port => !plan.Ports.ToList().Contains(port));
            }
            ProjectResource api = builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "tenants-api");
            Dictionary<string, object> environment = new();
            foreach (EnvironmentCallbackAnnotation callback in api.Annotations.OfType<EnvironmentCallbackAnnotation>())
                await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, api, environment, TestContext.Current.CancellationToken));
            environment["EventStore__Authentication__SigningKey"].ShouldBe(key);
            environment["EventStore__Authentication__Issuer"].ShouldBe(CompositionEnvironment.TokenIssuer);
            environment["EventStore__Authentication__Audience"].ShouldBe(CompositionEnvironment.TokenAudience);
            string stateStore = Path.Combine(workspace, "platform-components", "statestore.yaml");
            YamlSequenceNode metadata = (YamlSequenceNode)Spec(File.ReadAllText(stateStore)).Children[new YamlScalarNode("metadata")];
            metadata.Children.OfType<YamlMappingNode>().Single(node => node.Children[new YamlScalarNode("name")].ToString() == "keyPrefix").Children[new YamlScalarNode("value")].ToString().ShouldBe("none");
            ((YamlSequenceNode)((YamlMappingNode)Yaml(File.ReadAllText(stateStore)).Documents[0].RootNode).Children[new YamlScalarNode("scopes")]).Children.Select(node => node.ToString()).ShouldContain("tenants");
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
            PlatformCompositionOptions original = Options();
            PlatformCompositionOptions options = original with
            {
                Mapping = original.Mapping with { Entries = [.. original.Mapping.Entries, new SourceMappingEntry("Hexalith.Technical", "source", directory)] },
                ActiveModule = "technical", DomainModule = false, DeclarationPaths = [manifest],
                Projects = new Dictionary<string, string>(original.Projects) { ["technical/api"] = "server.csproj", ["technical/worker"] = "server.csproj" },
                Configuration = new Dictionary<string, string> { ["technical/api/Sample:Setting"] = "value", ["technical/worker/Sample:Setting"] = "value" },
            };
            IDistributedApplicationBuilder builder = Builder();
            PlatformAppModel.Compose(builder, options);
            IResource[] servers = builder.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).ToArray();
            servers.Length.ShouldBe(2); servers.ShouldNotContain(server => server.Name == "eventstore");
            servers.SelectMany(server => { DaprSidecarOptions sidecar = server.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options; return new[] { sidecar.DaprHttpPort, sidecar.DaprGrpcPort, sidecar.DaprInternalGrpcPort, sidecar.MetricsPort }; }).Distinct().Count().ShouldBe(8);
            await using ServiceProvider provider = builder.Services.BuildServiceProvider();
            provider.GetRequiredService<PlatformCompositionWorkspace>().Dispose();
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
        string file = Path.Combine(Path.GetTempPath(), "platform-owner-review-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            JsonNode document = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "src", "Hexalith.Platform.Composition", "Declarations", "eventstore.json")))!;
            document["modules"]![0]!["lifecycle"]!["readiness"]![0]!["endpoint"] = "/different-ready";
            File.WriteAllText(file, document.ToJsonString());
            IDistributedApplicationBuilder builder = Builder();
            InvalidDataException failure = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, Options(WorkspaceMode.Package) with { DeclarationPaths = [file] }));
            failure.Message.ShouldContain(file + ": eventstore: modules[0]:");
            failure.Message.ShouldContain("authoritative release snapshot"); builder.Resources.ShouldBeEmpty();
            document["modules"]![0]!["lifecycle"]!["readiness"]![0]!["endpoint"] = "/ready";
            File.WriteAllText(file, document.ToJsonString());
            PlatformAppModel.Prepare(Options(WorkspaceMode.Package) with { DeclarationPaths = [file] }).Count.ShouldBe(6);
        }
        finally { File.Delete(file); }
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
        string directory = Path.Combine(Path.GetTempPath(), "platform-failed-review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "module"));
        try
        {
            File.Copy(Path.Combine(Root, "references", "Hexalith.EventStore", "module", "hexalith.module-manifest.v2.json"), Path.Combine(directory, "module", "hexalith.module-manifest.v2.json"));
            File.WriteAllText(Path.Combine(directory, "gateway.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
            File.WriteAllText(Path.Combine(directory, "appsettings.json"), "invalid JSON");
            PlatformCompositionOptions original = Options(active: "eventstore");
            PlatformCompositionOptions options = original with { Mapping = original.Mapping with { Entries = original.Mapping.Entries.Select(entry => entry.Identity == "Hexalith.EventStore" ? entry with { Path = directory } : entry).ToArray() }, Projects = new Dictionary<string, string>(original.Projects) { ["eventstore/gateway"] = "gateway.csproj" } };
            string tempRoot = Path.Combine(Path.GetTempPath(), "hexalith-platform");
            string[] before = Directory.Exists(tempRoot) ? Directory.GetDirectories(tempRoot).Order(StringComparer.Ordinal).ToArray() : [];
            Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(Builder(), options));
            Directory.GetDirectories(tempRoot).Order(StringComparer.Ordinal).ShouldBe(before);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static YamlStream Yaml(string text) { YamlStream yaml = new(); yaml.Load(new StringReader(text)); return yaml; }
    private static YamlMappingNode Spec(string text) => (YamlMappingNode)((YamlMappingNode)Yaml(text).Documents[0].RootNode).Children[new YamlScalarNode("spec")];
}
