namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.EventStore.Contracts.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

public sealed partial class PlatformAppModelTests
{
    [Fact]
    public void PackageCatalogBindsExplicitOwnerPackagePrerequisites()
    {
        PlatformCompositionOptions options = PackageOptions();
        IReadOnlyList<PlatformServerBinding> servers = PlatformAppModel.Prepare(options);
        IReadOnlyList<PlatformCatalogContractBinding> owners = PlatformCatalogContractBinding.Resolve(options, servers);
        owners.ShouldNotBeEmpty();
        owners.ShouldAllBe(owner => owner.PackageVersion != null && !Path.IsPathRooted(owner.ContractReference) && !Path.IsPathRooted(owner.AdapterReference));
        owners.Select(owner => owner.ContractReference).ShouldContain("Hexalith.Tenants.Contracts");
        owners.Select(owner => owner.AdapterReference).ShouldContain("Hexalith.Memories.EventStoreExtensions");
        owners.Select(owner => owner.AdapterReference).ShouldContain("Hexalith.Tenants.Server");
    }

    [Fact]
    public void SelectedTimesheetsCallerRefusesBeforeCatalogGeneration()
    {
        var options = Options() with { TimesheetsEnabled = true };
        var builder = Builder();
        builder.Environment.EnvironmentName = Environments.Development;
        var failure = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, options));
        failure.Message.ShouldContain("timesheets");
        failure.Message.ShouldContain("lacks catalog enrollment");
        builder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public async Task CatalogBootstrapBindsActualKeysPoliciesFilesAndCohortStartWaitsAsync()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "catalog-bootstrap-contract-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = await PlatformRoutingCatalogTests.OptionsAsync(includeParties: true);
            var mapping = options.Mapping with { Entries = [.. options.Mapping.Entries, new SourceMappingEntry("Hexalith.Platform", "source", Root)] };
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), workspace, workspace, CompositionPortAllocator.Allocate(1), [], [], mapping,
                options.DeclarationPaths, "parties", typeof(PlatformAppModel).Assembly.Location) with
            {
                SelectedTest = new("parties", "platform-source", "dotnet", "tests/native", new string('a', 64), new string('b', 64), 120, 10, now, now.AddMinutes(2), now.AddMinutes(2), "source"),
            };
            await CompositionDaprComponentRenderer.WriteAsync(plan, TestContext.Current.CancellationToken);
            var builder = Builder();
            PlatformAppModel.ComposeRun(builder, plan, CompositionSigningKey.Create(), Path.Combine(Root, "references", "Hexalith.Builds", "src", "hosts", "Hexalith.Builds.Module.EventStoreHost", "Hexalith.Builds.Module.EventStoreHost.csproj"));
            var resources = builder.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).ToArray();
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
            string catalogDirectory = (string)gateway[prefix + "Directory"];
            var envelope = RouteCatalogCodec.Decode(File.ReadAllBytes((string)gateway[prefix + "CatalogPath"]), (string)gateway[prefix + "RootDigest"]);
            string[] participants = resources.Where(resource => environments[resource.Name].ContainsKey(prefix + "HostId")).Select(resource => resource.Name).Order().ToArray();
            participants.ShouldBe(new[] { "eventstore", "memories", "memories-domain-service", "parties", "tenants", "tenants-api" });
            foreach (var resource in resources.Where(resource => participants.Contains(resource.Name)))
            {
                var environment = environments[resource.Name];
                environment.Where(pair => pair.Key.StartsWith(prefix + "RequiredHostIds__", StringComparison.Ordinal)).Select(pair => (string)pair.Value).Order().ShouldBe(participants);
                environment[prefix + "InstanceId"].ShouldBe(plan.EnvironmentId + "-catalog");
                environment[prefix + "RootDigest"].ShouldBe(gateway[prefix + "RootDigest"]);
                foreach (var wait in resource.Annotations.OfType<WaitAnnotation>().Where(wait => resources.Contains(wait.Resource)))
                {
                    participants.ShouldContain(wait.Resource.Name, resource.Name);
                    wait.WaitType.ShouldBe(WaitType.WaitUntilStarted, resource.Name + " -> " + wait.Resource.Name);
                }
            }
            resources.Single(resource => resource.Name == "tenants").Annotations.OfType<WaitAnnotation>()
                .ShouldContain(wait => wait.Resource.Name == "eventstore" && wait.WaitType == WaitType.WaitUntilStarted);
            string authority = PlatformRoutingCatalogBootstrap.AuthorityName(plan.EnvironmentId + "-catalog");
            using var component = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(workspace, "platform-components", "routing-catalog.yaml")));
            component.RootElement.GetProperty("metadata").GetProperty("name").GetString().ShouldBe(authority);
            component.RootElement.GetProperty("scopes").EnumerateArray().Select(value => value.GetString()).Order()
                .ShouldBe(resources.Where(resource => participants.Contains(resource.Name)).Select(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Single().Binding.AppId).Order());
            component.RootElement.GetProperty("spec").GetProperty("type").GetString().ShouldBe("state.redis");
            component.RootElement.GetProperty("spec").GetProperty("metadata").EnumerateArray().Single(value => value.GetProperty("name").GetString() == "keyPrefix")
                .GetProperty("value").GetString().ShouldBe("none");
            gateway["EventStore__RouteCatalog__StateStore"].ShouldBe(authority);
            gateway["EventStore__RouteCatalog__StateKey"].ShouldBe(plan.EnvironmentId + "-catalog-authority");
            gateway["EventStore__IdempotencyAdmission__Enabled"].ShouldBe("true");
            gateway["EventStore__IdempotencyAdmission__ActiveDigestKeyVersion"].ShouldBe("development-v1");
            gateway["EventStore__IdempotencyAdmission__DigestKeySource"].ShouldBe("Configuration");
            gateway["EventStore__IdempotencyAdmission__DigestKeySecretStoreName"].ShouldBe("secretstore");
            gateway["EventStore__IdempotencyAdmission__DigestKeySecretName"].ShouldBe("eventstore-idempotency-ring");
            gateway["EventStore__IdempotencyAdmission__DigestKeySecretGeneration"].ShouldBe("development-v1");
            Convert.FromBase64String((string)gateway["EventStore__IdempotencyAdmission__DigestKeys__development-v1"]).Length.ShouldBe(32);
            using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalogDirectory, "receipt.json")));
            foreach (string list in new[] { "OwnerAssemblies", "AdapterAssemblies" })
            {
                string receiptName = char.ToLowerInvariant(list[0]) + list[1..];
                gateway.Where(pair => pair.Key.StartsWith(prefix + list + "__", StringComparison.Ordinal)).Select(pair => (string)pair.Value).Order()
                    .ShouldBe(receipt.RootElement.GetProperty(receiptName).EnumerateArray().Select(value => value.GetString()).Order());
            }
            foreach (var (resource, appId, keyName, allowedPrefix) in new[]
            {
                ("tenants", "tenants", "Authentication__BoundStreamRead__PrivateKey", "Authentication__BoundStreamRead__AllowedDomains__"),
                ("memories", "memories", "EventStore__CommandGateway__BoundCommand__PrivateKeyPkcs8Base64", "Authentication__BoundCommand__AllowedDomains__"),
            })
            {
                using var key = RSA.Create();
                key.ImportPkcs8PrivateKey(Convert.FromBase64String((string)environments[resource][keyName]), out _);
                Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()).ShouldBe((string)gateway["Authentication__BoundStreamRead__PublicKeys__" + appId]);
                var domains = envelope.Declarations.Where(declaration => envelope.Routes.Any(route => route.RouteId == declaration.RouteId
                    && (route.AppId == appId || appId == "memories" && route.AppId.StartsWith("memories-", StringComparison.Ordinal))))
                    .Select(declaration => declaration.Domain).Distinct().Order();
                ((string)gateway[allowedPrefix + appId]).Split(',').Order().ShouldBe(domains);
            }
            var scope = JsonSerializer.Deserialize<CompositionNativeTestScope>(File.ReadAllBytes(Path.Combine(catalogDirectory, "native-scope.json")))!;
            scope.RunId.ShouldBe(plan.RunId); scope.ModuleId.ShouldBe("parties");
            scope.RoutingCatalogRoot.ShouldBe((string)gateway[prefix + "RootDigest"]);
            scope.Domains.ShouldBe(["party"]);
            scope.Tenants.ShouldBe([plan.TenantNamespace, "system"]);
            using var closure = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalogDirectory, "selected-closure.json")));
            closure.RootElement.GetProperty("RunId").GetString().ShouldBe(plan.RunId);
            closure.RootElement.GetProperty("hosts").EnumerateArray().Select(host => host.GetProperty("ResourceId").GetString()).Order().ShouldBe(participants);
            closure.RootElement.GetProperty("callerClosure").EnumerateArray().Select(value => value.GetString()).ShouldBe(plan.Modules.Select(module => module.ModuleId));
#if HEXALITH_PLATFORM_SOURCE_CATALOG
            string firstHost = closure.RootElement.GetProperty("hosts")[0].GetProperty("ResourceId").GetString()!;
            File.WriteAllBytes(Path.Combine(catalogDirectory, firstHost + ".committed.json"), JsonSerializer.SerializeToUtf8Bytes(new
            { Serving = true, RootDigest = new string('0', 64), Generation = envelope.Generation, Epoch = 1 }));
            using var provider = builder.Services.BuildServiceProvider();
            var preparation = provider.GetServices<ICompositionRunPreparation>().OfType<PlatformNativeTestPreparation>().Single();
            var refusal = await Should.ThrowAsync<InvalidOperationException>(() => preparation.PrepareAsync(provider, TestContext.Current.CancellationToken));
            refusal.Message.ShouldBe("A real catalog participant did not commit the selected native scope.");
            preparation.NativeTestScope.ShouldBeNull();
            File.Exists(Path.Combine(workspace, NativeTestHandoff.EndpointCredentialsFileName)).ShouldBeFalse();
#endif
        }
        finally { if (Directory.Exists(workspace)) Directory.Delete(workspace, true); }
    }
}
