extern alias buildsseed;

namespace Hexalith.Platform.Composition.Tests;

using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.EventStore.Contracts.Routing;
using Microsoft.Extensions.Hosting;
using Shouldly;

/// <summary>Opt-in source verification using the real private catalog reader and an unstarted Platform resource model.</summary>
public sealed partial class ConversationsEnrollmentTests
{
    /// <summary>Generates the real compiled owner catalog and composes every ordinary resource without building or starting the application.</summary>
    [Fact]
    public async Task SelectedConversationsOwnerGeneratesCatalogAndComposesAllOrdinaryResourcesWithoutStart()
    {
        string inputsPath = Environment.GetEnvironmentVariable("HEXALITH_CONVERSATIONS_CATALOG_INPUTS")
            ?? throw new InvalidOperationException("This opt-in source verification requires explicit HEXALITH_CONVERSATIONS_CATALOG_INPUTS.");
        using var inputs = JsonDocument.Parse(File.ReadAllBytes(inputsPath));
        string Input(string name) => inputs.RootElement.GetProperty(name).GetString()!;
        SourceMapping producer = SourceMapping.LoadVerified(Input("producerMappingPath"), Input("producerMappingHash"));
        SourceMapping mapping = SourceMapping.LoadVerified(Input("mappingPath"), Input("mappingHash"));
        SourceMapping identity = await WorkspaceRootResolver.ResolveAsync(Path.Combine(Root, "hexalith-platform.json"), WorkspaceMode.Source, TestContext.Current.CancellationToken);
        mapping.PlatformIdentity.ShouldBe(identity.PlatformIdentity);
        mapping.Root.ShouldBe(Root);
        mapping.Entries.Count.ShouldBe(producer.Entries.Count);
        foreach (var entry in producer.Entries)
            mapping.Entries.Single(candidate => candidate.Identity == entry.Identity).ShouldBe(
                entry.Identity == "Hexalith.Platform" ? entry with { Path = Root } : entry);
        string conversation = mapping.Entries.Single(entry => entry.Identity == "Hexalith.Conversations").Path!;
        string workspace = Path.Combine(Path.GetTempPath(), "conversations-source-catalog-" + Guid.NewGuid().ToString("N"));
        string evidence = Path.Combine(Input("evidenceRoot"), DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidence);
        File.Copy(inputsPath, Path.Combine(evidence, "inputs.json"));
        bool composed = false;
        try
        {
            await SourceMappingMaterializer.WriteAsync(mapping, workspace, TestContext.Current.CancellationToken);
            var declarations = new[] { Path.Combine(conversation, "module", "hexalith.module-manifest.v2.json") };
            var plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), workspace, workspace,
                CompositionPortAllocator.Allocate(1), [], [], mapping, declarations, "conversations", typeof(PlatformAppModel).Assembly.Location);
            await CompositionDaprComponentRenderer.WriteAsync(plan, TestContext.Current.CancellationToken);
            var options = new PlatformCompositionOptions
            {
                PlatformRoot = Root, Mapping = mapping, ActiveModule = "conversations", DeclarationPaths = declarations,
                Configuration = Configuration(), RunPlan = plan,
                EventStoreSeedProject = Path.Combine(mapping.Entries.Single(entry => entry.Identity == "Hexalith.Builds").Path!,
                    "src", "hosts", "Hexalith.Builds.Module.EventStoreHost", "Hexalith.Builds.Module.EventStoreHost.csproj"),
            };
            options.Configuration.Count.ShouldBe(28);
            var builder = Builder();
            builder.Environment.EnvironmentName = Environments.Development;
            PlatformAppModel.Compose(builder, options);
            var resources = builder.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).ToArray();
            resources.Select(resource => resource.Name).Order().ShouldBe(new[] { "eventstore", "tenants", "tenants-api", "memories",
                "memories-domain-service", "memories-access-telemetry-clock", "memories-access-telemetry", "conversations" }.Order());
            var environments = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            foreach (var resource in resources)
            {
                Dictionary<string, object> environment = [];
                foreach (var callback in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
                    await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, resource, environment, TestContext.Current.CancellationToken));
                environments[resource.Name] = environment;
            }
            const string prefix = "EventStore__RouteCatalog__DevelopmentBootstrap__";
            var gateway = environments["eventstore"];
            string[] participants = resources.Where(resource => environments[resource.Name].ContainsKey(prefix + "HostId")).Select(resource => resource.Name).Order().ToArray();
            participants.ShouldBe(new[] { "conversations", "eventstore", "memories", "memories-domain-service", "tenants", "tenants-api" });
            string catalogPath = (string)gateway[prefix + "CatalogPath"];
            string digest = (string)gateway[prefix + "RootDigest"];
            var catalog = RouteCatalogCodec.Decode(File.ReadAllBytes(catalogPath), digest);
            var routes = catalog.Declarations.Where(route => route.Domain is "conversation" or "conversations").ToArray();
            routes.Length.ShouldBe(22);
            routes.Count(route => route.Kind == "command").ShouldBe(13);
            routes.Count(route => route.Kind == "query").ShouldBe(7);
            routes.Count(route => route.Kind == "projection").ShouldBe(2);
            foreach (var route in routes)
            {
                var target = catalog.Routes.Single(target => target.RouteId == route.RouteId);
                target.AppId.ShouldBe("conversation");
                target.Method.ShouldBe(route.Kind switch { "command" => "process", "query" => "query", _ => route.ProjectionType == "conversation" ? "project" : "project/v2" });
                route.CredentialKind.ShouldBe("workload"); route.PlatformAdmission.ShouldBeNull();
                catalog.Operations!.Single(operation => operation.RouteId == route.RouteId).Ingress.ShouldBe("internal");
            }
            foreach (string participant in participants)
            {
                environments[participant][prefix + "RootDigest"].ShouldBe(digest);
                environments[participant].Where(pair => pair.Key.StartsWith(prefix + "RequiredHostIds__", StringComparison.Ordinal))
                    .Select(pair => (string)pair.Value).Order().ShouldBe(participants);
            }
            var conversationBinding = resources.Single(resource => resource.Name == "conversations")
                .Annotations.OfType<PlatformServerAnnotation>().Single().Binding;
            conversationBinding.AppId.ShouldBe("conversation");
            conversationBinding.ProjectPath.ShouldBe(Path.Combine(conversation, "src/Hexalith.Conversations.Server/Hexalith.Conversations.Server.csproj"));
            using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(catalogPath)!, "receipt.json")));
            receipt.RootElement.GetProperty("sourceMappingHash").GetString().ShouldBe(mapping.ContentHash);
            receipt.RootElement.GetProperty("rootDigest").GetString().ShouldBe(digest);
            foreach (string owner in new[] { "Hexalith.Conversations.Contracts", "Hexalith.Conversations.EventStoreExtensions" })
            {
                var artifact = receipt.RootElement.GetProperty("loadedArtifacts").EnumerateArray()
                    .Single(artifact => AssemblyName.GetAssemblyName(artifact.GetProperty("path").GetString()!).Name == owner);
                artifact.GetProperty("sha256").GetString().ShouldBe(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(artifact.GetProperty("path").GetString()!))));
            }
            string reader = Path.Combine(workspace, "platform-components", "catalog-reader");
            foreach (var (label, wrongAdapter) in new[] { ("missing-adapter", "Hexalith.Tenants.Contracts"),
                ("foreign-adapter", "Hexalith.Conversations.EventStoreExtensions") })
            {
                JsonObject request = JsonNode.Parse(File.ReadAllBytes(Path.Combine(reader, "request.json")))!.AsObject();
                var selectedOwner = request["owners"]!.AsArray().Single(owner => owner!["assembly"]!.GetValue<string>() == "Hexalith.Tenants.Contracts")!;
                selectedOwner["adapterAssembly"] = wrongAdapter;
                string output = Path.Combine(workspace, label);
                Directory.CreateDirectory(output);
                request["outputDirectory"] = output;
                string negativeInput = Path.Combine(evidence, label + "-request.json");
                File.WriteAllText(negativeInput, request.ToJsonString());
                var refusal = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
                    [Path.Combine(reader, "output", "Hexalith.Platform.CatalogBuilder.dll"), negativeInput], reader, null),
                    TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
                File.WriteAllText(Path.Combine(evidence, label + ".log"), refusal.Output + refusal.ErrorOutput);
                refusal.ExitCode.ShouldNotBe(0);
                (refusal.Output + refusal.ErrorOutput).ShouldContain("adapter inventory differs");
                File.Exists(Path.Combine(output, "catalog.json")).ShouldBeFalse();
            }
            foreach (var (label, wrongAdapter) in new[] { ("conversations-missing-adapter", "Hexalith.Conversations.Contracts"),
                ("conversations-foreign-adapter", "Hexalith.Tenants.Server") })
            {
                JsonObject request = JsonNode.Parse(File.ReadAllBytes(Path.Combine(reader, "request.json")))!.AsObject();
                request["owners"]!.AsArray().Single(owner => owner!["assembly"]!.GetValue<string>() == "Hexalith.Conversations.Contracts")!["adapterAssembly"] = wrongAdapter;
                string output = Path.Combine(workspace, label); Directory.CreateDirectory(output); request["outputDirectory"] = output;
                string negativeInput = Path.Combine(evidence, label + "-request.json"); File.WriteAllText(negativeInput, request.ToJsonString());
                var refusal = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
                    [Path.Combine(reader, "output", "Hexalith.Platform.CatalogBuilder.dll"), negativeInput], reader, null), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
                File.WriteAllText(Path.Combine(evidence, label + ".log"), refusal.Output + refusal.ErrorOutput);
                refusal.ExitCode.ShouldNotBe(0);
                (refusal.Output + refusal.ErrorOutput).ShouldContain("selected Conversations adapter schema owner is absent");
                Directory.EnumerateFiles(output).ShouldBeEmpty();
            }
            var domainArtifact = receipt.RootElement.GetProperty("loadedArtifacts").EnumerateArray().Single(artifact =>
                AssemblyName.GetAssemblyName(artifact.GetProperty("path").GetString()!).Name == "Hexalith.Conversations");
            string domainPath = domainArtifact.GetProperty("path").GetString()!;
            domainArtifact.GetProperty("sha256").GetString().ShouldBe(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(domainPath))));
            string alteredOutput = Path.Combine(workspace, "altered-domain"); Directory.CreateDirectory(alteredOutput);
            foreach (string file in Directory.EnumerateFiles(Path.GetDirectoryName(domainPath)!)) File.Copy(file, Path.Combine(alteredOutput, Path.GetFileName(file)));
            File.WriteAllBytes(Path.Combine(alteredOutput, "Hexalith.Conversations.dll"), "invalid-selected-domain"u8.ToArray());
            JsonObject alteredRequest = JsonNode.Parse(File.ReadAllBytes(Path.Combine(reader, "request.json")))!.AsObject();
            string rejected = Path.Combine(workspace, "altered-domain-rejected"); Directory.CreateDirectory(rejected); alteredRequest["outputDirectory"] = rejected;
            string alteredInput = Path.Combine(evidence, "altered-domain-request.json"); File.WriteAllText(alteredInput, alteredRequest.ToJsonString());
            var alteredRefusal = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
                [Path.Combine(alteredOutput, "Hexalith.Platform.CatalogBuilder.dll"), alteredInput], alteredOutput, null), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
            File.WriteAllText(Path.Combine(evidence, "altered-domain.log"), alteredRefusal.Output + alteredRefusal.ErrorOutput);
            alteredRefusal.ExitCode.ShouldNotBe(0); Directory.EnumerateFiles(rejected).ShouldBeEmpty();
            // Execute the actual gateway Program registration. Background workers are removed so this
            // in-process host cannot contact unavailable sidecars; no adapter is registered by this test.
            var gatewaySettings = gateway.Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal)
                || pair.Key.StartsWith("EventStore__IdempotencyAdmission__", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key.Replace("__", ":", StringComparison.Ordinal), pair => (string?)pair.Value.ToString(), StringComparer.Ordinal);
            gatewaySettings["EventStore:DomainService:AppId"] = "eventstore";
            gatewaySettings["APP_API_TOKEN"] = "in-process-registration-channel";
            gatewaySettings["Authentication:JwtBearer:Issuer"] = "in-process-registration";
            gatewaySettings["Authentication:JwtBearer:Audience"] = "eventstore";
            gatewaySettings["Authentication:JwtBearer:SigningKey"] = "in-process-registration-signing-key-at-least-thirty-two-bytes";
            var previousEnvironment = gatewaySettings.Keys.ToDictionary(key => key, key => Environment.GetEnvironmentVariable(key.Replace(":", "__", StringComparison.Ordinal)));
            string[] installedAdmissionAdapters;
            try
            {
                // Minimal Program registrations execute before deferred test configuration callbacks.
                // Supply only the same composed gateway settings at CreateBuilder time, then restore them.
                foreach (var (key, value) in gatewaySettings) Environment.SetEnvironmentVariable(key.Replace(":", "__", StringComparison.Ordinal), value);
                using var gatewayFactory = new WebApplicationFactory<buildsseed::Program>().WithWebHostBuilder(host =>
                {
                    host.UseEnvironment("Development");
                    host.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(gatewaySettings));
                    host.ConfigureServices(services => services.RemoveAll<IHostedService>());
                });
                using var gatewayClient = gatewayFactory.CreateClient();
                var installed = gatewayFactory.Services.GetServices<IIdempotencyIntentAdapter>().OfType<IDeclaredIdempotencyIntentAdapter>().ToArray();
                installed.Count(adapter => adapter.Declaration.Route.Domain == "conversation").ShouldBe(13);
                _ = gatewayFactory.Services.GetRequiredService<IIdempotencyIntentAdapterRegistry>();
                var actualAdmission = await RouteCatalogAdmissionObservation.CaptureAsync(gatewayFactory.Services, "eventstore", TestContext.Current.CancellationToken);
                actualAdmission.Select(entry => entry.ContentDigest).Order().ShouldBe(catalog.Idempotency.Select(entry => entry.ContentDigest).Order());
                installedAdmissionAdapters = installed.Select(adapter => adapter.Declaration.Route.RouteId).Order().ToArray();
            }
            finally
            {
                foreach (var (key, value) in previousEnvironment) Environment.SetEnvironmentVariable(key.Replace(":", "__", StringComparison.Ordinal), value);
            }
            File.WriteAllBytes(Path.Combine(evidence, "model-observations.json"), JsonSerializer.SerializeToUtf8Bytes(new
            {
                sourceMappingHash = mapping.ContentHash, rootDigest = digest, ordinaryResources = resources.Select(resource => new
                { resource.Name, appId = resource.Annotations.OfType<PlatformServerAnnotation>().Single().Binding.AppId }).ToArray(),
                participants, installedAdmissionAdapters, configurationKeys = options.Configuration.Keys.Order().ToArray(),
                conversations = routes, resourceLifecycle = "Compose only; no DistributedApplication Build, Start or Run was invoked.",
            }));
            composed = true;
        }
        finally
        {
            if (Directory.Exists(workspace))
            {
                string[] names = ["catalog.json", "receipt.json", "request.json", "build.log", "generation.log", "bindings.props", "Program.cs", "Hexalith.Platform.CatalogBuilder.csproj"];
                foreach (string path in Directory.EnumerateFiles(workspace, "*", SearchOption.AllDirectories)
                    .Where(path => names.Contains(Path.GetFileName(path), StringComparer.Ordinal)
                        || Path.GetRelativePath(workspace, path).StartsWith("source-mapping" + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
                {
                    string target = Path.Combine(evidence, "artifacts", Path.GetRelativePath(workspace, path));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(path, target);
                }
                File.WriteAllBytes(Path.Combine(evidence, "runtime-output-hashes.json"), JsonSerializer.SerializeToUtf8Bytes(
                    Directory.EnumerateFiles(workspace, "*.dll", SearchOption.AllDirectories).Select(path => new
                    { path = Path.GetRelativePath(workspace, path), sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) }).ToArray()));
                Directory.Delete(workspace, true);
            }
            File.WriteAllText(Path.Combine(evidence, "status.txt"), composed ? "passed; composition only; runtime availability not measured" : "failed; retain surrounding test output");
            Console.WriteLine("Conversations source catalog evidence: " + evidence);
        }
    }
}
