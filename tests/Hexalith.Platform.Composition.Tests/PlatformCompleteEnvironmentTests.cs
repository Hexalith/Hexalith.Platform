extern alias platformseed;

namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.EventStore.Server.DomainServices;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

public sealed class PlatformCompleteEnvironmentTests
{
    private static readonly string Declarations = Path.Combine(PlatformTestRepository.Root, "src",
        "Hexalith.Platform.Composition", "Declarations");

    [Fact]
    public void SelectorDeclaresOnlyTheSevenMvpOwners()
    {
        var validation = PlatformManifestValidator.Validate([Path.Combine(PlatformTestRepository.Root, PlatformDeclarationSet.CompleteManifest)],
            TestContext.Current.CancellationToken);
        validation.IsValid.ShouldBeTrue(string.Join(Environment.NewLine, validation.Diagnostics.Select(diagnostic => diagnostic.Message)));
        PlatformModuleDeclaration selector = validation.Declarations!.ShouldHaveSingleItem();
        var identity = selector.Declaration.GetProperty("identity");
        PlatformDeclarationSet.Id(selector.Declaration).ShouldBe(PlatformDeclarationSet.CompleteModule);
        identity.GetProperty("servers").GetArrayLength().ShouldBe(0);
        identity.GetProperty("dependencies").EnumerateArray().Select(owner => owner.GetString()).ToArray()
            .ShouldBe(["eventstore", "tenants", "parties", "folders", "projects", "mcpcli", "memories"], ignoreOrder: true);
    }

    [Fact]
    public void SelectedReceiversAndTheHistoryReaderReceiveOnlyTheirLocalGrants()
    {
        PlatformModuleDeclaration gateway = ReadManifest(Path.Combine(Declarations, "eventstore.json"));
        PlatformModuleDeclaration tenants = ReadManifest(Path.Combine(Declarations, "tenants.json"));
        PlatformModuleDeclaration selector = ReadManifest(Path.Combine(PlatformTestRepository.Root, PlatformDeclarationSet.CompleteManifest));
        var conversations = new PlatformModuleDeclaration("conversations.json", JsonDocument.Parse("""
            {
              "identity": {
                "moduleId": "conversations",
                "servers": [{"id":"domain","appId":"conversation","enabled":true}]
              },
              "surfaces": {
                "interfaces": [{"server":"domain","invocationRules":[{"caller":"eventstore"}]}]
              },
              "integration": {
                "identityNeeds": [{
                  "name":"tenant-access-history-reader","audience":"hexalith-eventstore",
                  "roles":[],"claims":["eventstore:permission"],"applicationPrincipal":true
                }]
              }
            }
            """).RootElement.Clone());

        var queryDomains = new Dictionary<string, string[]> { ["conversation"] = ["conversations"] };
        var prepared = PlatformLocalRealm.PrepareDevelopment([gateway, tenants, selector, conversations],
            PlatformLocalRealmTests.RealmUrl.Port, PlatformLocalRealmTests.SyntheticTenant, queryDomains);
        JsonNode realm = JsonNode.Parse(prepared.Import)!;
        var clients = realm["clients"]!.AsArray();
        JsonNode gatewayClient = clients.Single(client => client!["clientId"]!.GetValue<string>() == "eventstore")!;
        var scopes = gatewayClient["optionalClientScopes"]!.AsArray();
        scopes.Any(scope => scope!.GetValue<string>() == "eventstore-audience.conversation").ShouldBeTrue();
        scopes.Any(scope => scope!.GetValue<string>() == "eventstore-audience.tenants").ShouldBeTrue();
        scopes.Any(scope => scope!.GetValue<string>() == "eventstore-audience.works").ShouldBeFalse();
        JsonNode reader = clients.Single(client => client!["clientId"]!.GetValue<string>() == PlatformRealmContract.ConversationsReader)!;
        reader["fullScopeAllowed"]!.GetValue<bool>().ShouldBeFalse();
        reader["optionalClientScopes"]!.AsArray().ShouldBeEmpty();
        var mappers = reader["protocolMappers"]!.AsArray().Select(mapper => mapper!["config"]!).ToArray();
        mappers.Single(mapper => mapper["claim.name"]?.GetValue<string>() == "eventstore:permission")
            ["claim.value"]!.GetValue<string>().ShouldBe("tenants:read");
        mappers.Any(mapper => mapper["claim.name"]?.GetValue<string>() == "eventstore:tenant").ShouldBeFalse();
        realm["attributes"]!["syntheticTenantId"]!.GetValue<string>().ShouldBe(PlatformLocalRealmTests.SyntheticTenant);
        var grants = prepared.WorkloadBindingEnvironment(DateTimeOffset.UtcNow);
        string boundGrants = JsonSerializer.Deserialize<string>('"' + grants["HEXALITH_LOCAL_REALM_BINDINGS_EVENTSTORE"] + '"')!;
        JsonNode queryGrant = JsonNode.Parse(boundGrants)!.AsArray()
            .Single(grant => grant!["allowQueryAuthorityDigest"]!.GetValue<bool>())!;
        queryGrant["audience"]!.GetValue<string>().ShouldBe("conversation");
        queryGrant["bindings"]!["eventstore:bound-domain"]![0]!.GetValue<string>().ShouldBe("conversations");
        queryGrant["bindings"]!["eventstore:bound-tenant"]![0]!.GetValue<string>().ShouldBe(PlatformLocalRealmTests.SyntheticTenant);
        queryGrant["allowUnbound"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public void FoldersBindingsKeepTheReceiptSharedAndTheProofsAndBrokerRolesDistinct()
    {
        var gateway = ReadManifest(Path.Combine(Declarations, "eventstore.json"));
        var memories = ReadManifest(Path.Combine(Declarations, "memories.json"));
        using var workerDeclaration = JsonDocument.Parse("""
            {
              "identity": {
                "moduleId": "folders",
                "servers": [{"id":"domain","appId":"folders","enabled":true},
                            {"id":"workers","appId":"folders-workers","enabled":true}]
              },
              "runtime": {"dapr":[
                {"role":"worker-inbound-events","capability":"pubsub","recoveryClass":"rebuild-only",
                 "configurationKey":"Folders:Workers:PubSubName","servers":["workers"]},
                {"role":"events","capability":"pubsub","recoveryClass":"rebuild-only",
                 "configurationKey":"Folders:Workers:MemoriesPubSubName","servers":["workers"]}
              ]},
              "surfaces":{"interfaces":[]},
              "integration":{"topics":[]}
            }
            """);
        var folders = new PlatformModuleDeclaration("folders.json", workerDeclaration.RootElement);
        List<PlatformServerBinding> servers =
        [
            Server(gateway, "gateway", "eventstore"),
            Server(memories, "server", "memories"),
            Server(folders, "domain", "folders"),
            Server(folders, "workers", "folders-workers"),
        ];

        PlatformLocalProfile.BindFolders(PlatformTestRepository.Root, servers);
        var gatewayEnvironment = servers[0].Environment;
        var foldersEnvironment = servers[2].Environment;
        var workersEnvironment = servers[3].Environment;
        gatewayEnvironment["Authentication__HumanAuthorityReceipt__SigningKey"]
            .ShouldBe(foldersEnvironment["Authentication__HumanAuthorityReceipt__SigningKey"]);
        foldersEnvironment["APP_API_TOKEN"].ShouldBe(foldersEnvironment["EventStore__AppChannelToken"]);
        workersEnvironment["APP_API_TOKEN"].ShouldBe(workersEnvironment["EventStore__AppChannelToken"]);
        workersEnvironment["APP_API_TOKEN"].ShouldNotBe(foldersEnvironment["APP_API_TOKEN"]);
        gatewayEnvironment["EventStore__Publisher__TopicOverrides__folders"].ShouldBe("folders.events");
        servers[1].Environment["EventStoreIntegration__Routing__SourceToTenantMap__hexalith-folders"].ShouldBe("folders-index");
        foreach (PlatformServerBinding server in servers.Skip(2))
        {
            var publicKeys = new List<string>();
            foreach (string section in new[] { "BoundStreamRead", "NativeSourceScan" })
            {
                string prefix = "Authentication__" + section + "__";
                using RSA proof = RSA.Create();
                proof.ImportPkcs8PrivateKey(Convert.FromBase64String(server.Environment[prefix + "PrivateKey"]), out _);
                string expected = gatewayEnvironment[prefix + "PublicKeys__" + server.AppId];
                Convert.ToBase64String(proof.ExportSubjectPublicKeyInfo()).ShouldBe(expected);
                publicKeys.Add(expected);
            }
            publicKeys.Distinct().Count().ShouldBe(2);
        }

        PlatformDaprBindings dapr = PlatformDaprBindings.Resolve([gateway, folders, memories]);
        dapr.Environment("folders", "workers")["Folders__Workers__PubSubName"].ShouldBe("folders-workers-pubsub");
        dapr.Environment("folders", "workers")["Folders__Workers__MemoriesPubSubName"].ShouldBe("pubsub");
        dapr.Consumes("folders-workers-pubsub", "eventstore").ShouldBeFalse();
        var rendered = dapr.Render(Path.Combine(PlatformTestRepository.Root, "DaprComponents", "reference"),
            16379, 16380, "/run/secrets.json", null);
        rendered["pubsub.workers.yaml"].ShouldContain("folders-workers=tenants.events,folders.events");
        rendered["pubsub.workers.yaml"].ShouldContain("folders-workers=deadletter.folders.events,deadletter.tenants.events");
        rendered["pubsub.workers.yaml"].ShouldContain("127.0.0.1:16379");
        rendered["pubsub.yaml"].ShouldContain("127.0.0.1:16379");
        rendered["pubsub.yaml"].ShouldContain("folders-workers=memories-events");
        dapr.DeclaredCallers("eventstore").ShouldContain("folders-workers");
    }

    [Fact]
    public void UnrecordedPackageImageIdentifiesTheExactWorkloadAndImage()
    {
        var root = PlatformTestRepository.Root;
        var options = new PlatformCompositionOptions
        {
            PlatformRoot = root,
            Mapping = new SourceMapping(WorkspaceMode.Package, root, "Hexalith.Platform", [], false),
        };
        var error = Should.Throw<InvalidDataException>(() => PlatformReleasedImages.RequireIntakeDigest(options,
            "projects", "projects/domain", "registry.hexalith.com/projects", "0.0.0"));
        error.Message.ShouldContain("projects/domain");
        error.Message.ShouldContain("registry.hexalith.com/projects:0.0.0");
        error.Message.ShouldContain("eng/module-intake.json");
    }

    [Theory]
    [InlineData("domain", "folders")]
    [InlineData("workers", "folders-workers")]
    [InlineData("domain", "projects")]
    public void ReservedAppIdsAloneDoNotGrantOwnerInvocation(string serverId, string appId)
    {
        var gateway = ReadManifest(Path.Combine(Declarations, "eventstore.json"));
        var other = new PlatformModuleDeclaration("other.json", JsonSerializer.SerializeToElement(new
        {
            identity = new { moduleId = "other", servers = new[] { new { id = serverId, appId, enabled = true } } },
            runtime = new { dapr = Array.Empty<object>() },
            surfaces = new { interfaces = Array.Empty<object>() },
            integration = new { topics = Array.Empty<object>() },
        }));

        PlatformDaprBindings.Resolve([gateway, other]).DeclaredCallers("eventstore").ShouldNotContain(appId);
    }

    [Fact]
    public void CompleteRunnerRequiresTheDeclaredOperatorTaskExactlyOnce()
    {
        string root = PlatformTestRepository.Root;
        var mapping = new SourceMapping(WorkspaceMode.Source, root, "Hexalith.Platform",
            [new SourceMappingEntry("Hexalith.Tenants", "source", Declarations)], false);
        string manifest = Path.Combine(root, PlatformDeclarationSet.CompleteManifest);
        var plan = CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), root, root,
            CompositionPortAllocator.Allocate(1), [], [], mapping, [manifest], PlatformDeclarationSet.CompleteModule);
        var options = new PlatformCompositionOptions { PlatformRoot = root, Mapping = mapping, RunPlan = plan };
        var selector = ReadManifest(manifest);
        var owners = selector.Declaration.GetProperty("identity").GetProperty("dependencies").EnumerateArray()
            .Select(owner => owner.GetString()!).Append("conversations").Where(owner => owner != "tenants");
        PlatformModuleDeclaration[] closure =
        [
            selector, ReadManifest(Path.Combine(Declarations, "tenants.json")),
            .. owners.Select(owner => new PlatformModuleDeclaration(owner + ".json",
                JsonSerializer.SerializeToElement(new { identity = new { moduleId = owner } }))),
        ];

        var refusal = Should.Throw<InvalidDataException>(() => PlatformDeclarationSet.ValidateCompleteRunner(options, closure));
        refusal.Message.ShouldContain("create-synthetic-tenant");
        string executable = Path.Combine(Declarations, "module", "tasks", "create-synthetic-tenant.py");
        refusal.Message.ShouldContain(executable);
        var task = new CompositionStartupTask("tenants", "create-synthetic-tenant", executable, []);
        PlatformDeclarationSet.ValidateCompleteRunner(options with { RunPlan = plan with { StartupTasks = [task] } }, closure);
        Should.Throw<InvalidDataException>(() => PlatformDeclarationSet.ValidateCompleteRunner(options with
        {
            RunPlan = plan with { StartupTasks = [task, task with { ExecutablePath = executable + ".other" }] },
        }, closure));
    }

#if HEXALITH_PLATFORM_EXTENSION_SDK
    [Theory]
    [InlineData(401, "authentication_failure", false)]
    [InlineData(403, "authorization_denied", false)]
    [InlineData(404, "not_found_to_caller", false)]
    [InlineData(503, "read_model_unavailable", true)]
    public async Task GatewayPreservesTheFoldersDenialShape(int status, string code, bool retryable)
    {
        Type handlerType = typeof(platformseed::Program).Assembly.GetType("FoldersSafeDenialHandler", throwOnError: true)!;
        var handler = (IExceptionHandler)Activator.CreateInstance(handlerType, nonPublic: true)!;
        await using var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        using var response = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = response;
        bool handled = await handler.TryHandleAsync(context, new DomainServiceSafeDenialException(status), TestContext.Current.CancellationToken);
        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(status);
        context.Response.ContentType.ShouldStartWith("application/problem+json");
        using var problem = JsonDocument.Parse(response.ToArray());
        var body = problem.RootElement;
        body.GetProperty("code").GetString().ShouldBe(code);
        body.GetProperty("category").GetString().ShouldBe(code);
        body.GetProperty("retryable").GetBoolean().ShouldBe(retryable);
        body.GetProperty("details").EnumerateObject().ShouldHaveSingleItem().Value.GetString().ShouldBe("metadata_only");
        body.TryGetProperty("detail", out _).ShouldBeFalse();
    }
#endif

    private static PlatformModuleDeclaration ReadManifest(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        return new(path, document.RootElement.GetProperty("modules")[0].Clone());
    }

    private static PlatformServerBinding Server(PlatformModuleDeclaration module, string server, string appId) =>
        new(module, server, appId, appId, null, null, "/ready", new Dictionary<string, string>(), []);
}
