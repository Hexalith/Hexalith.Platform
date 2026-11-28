extern alias platformseed;

namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.DomainServices;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using YamlDotNet.RepresentationModel;

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
        var memories = ReadManifest(Path.Combine(Declarations, "memories.json"));
        var other = new PlatformModuleDeclaration("other.json", JsonSerializer.SerializeToElement(new
        {
            identity = new { moduleId = "other", servers = new[] { new { id = serverId, appId, enabled = true } } },
            runtime = new { dapr = Array.Empty<object>() },
            surfaces = new { interfaces = Array.Empty<object>() },
            integration = new { topics = Array.Empty<object>() },
        }));

        PlatformDaprBindings dapr = PlatformDaprBindings.Resolve([gateway, memories, other]);
        dapr.DeclaredCallers("eventstore").ShouldNotContain(appId);
        dapr.DeclaredCallers("memories").ShouldNotContain(appId);
    }

    [Fact]
    public void ProjectsAndFoldersShareReceiptKeepDistinctChannelsAndBindGatewayAndMemoriesAccess()
    {
        var gateway = ReadManifest(Path.Combine(Declarations, "eventstore.json"));
        var memories = ReadManifest(Path.Combine(Declarations, "memories.json"));
        var folders = ReadManifest(Path.Combine(PlatformTestRepository.ReferenceRoot(PlatformTestRepository.Root, "Hexalith.Folders"), "module", "hexalith.module-manifest.v2.json"));
        var projects = ReadManifest(Path.Combine(PlatformTestRepository.ReferenceRoot(PlatformTestRepository.Root, "Hexalith.Projects"), "module", "hexalith.module-manifest.v2.json"));
        List<PlatformServerBinding> servers =
        [
            Server(gateway, "gateway", "eventstore"),
            Server(memories, "server", "memories"),
            Server(folders, "domain", "folders"),
            Server(folders, "workers", "folders-workers"),
            Server(projects, "domain", "projects"),
        ];

        PlatformLocalProfile.BindFolders(PlatformTestRepository.Root, servers);
        PlatformLocalProfile.BindProjects(servers);

        string receipt = servers[0].Environment["Authentication__HumanAuthorityReceipt__SigningKey"];
        receipt.ShouldNotBeNullOrWhiteSpace();
        servers[2].Environment["Authentication__HumanAuthorityReceipt__SigningKey"].ShouldBe(receipt);
        servers[3].Environment.ContainsKey("Authentication__HumanAuthorityReceipt__SigningKey").ShouldBeFalse();
        servers[4].Environment["Authentication__HumanAuthorityReceipt__SigningKey"].ShouldBe(receipt);

        servers[4].Environment["APP_API_TOKEN"].ShouldNotBeNullOrWhiteSpace();
        servers[4].Environment["APP_API_TOKEN"].ShouldBe(servers[4].Environment["EventStore__AppChannelToken"]);
        servers[4].Environment["APP_API_TOKEN"].ShouldNotBe(servers[2].Environment["APP_API_TOKEN"]);
        servers[4].Environment["APP_API_TOKEN"].ShouldNotBe(servers[3].Environment["APP_API_TOKEN"]);

        PlatformDaprBindings dapr = PlatformDaprBindings.Resolve([gateway, memories, folders, projects]);
        var gatewayInvocations = dapr.DeclaredInvocations("eventstore");
        gatewayInvocations.ShouldContain(grant => grant.CallerAppId == "projects" && grant.Method == "/api/v1/commands" && grant.HttpMethods.SequenceEqual(new[] { "POST" }));
        gatewayInvocations.ShouldContain(grant => grant.CallerAppId == "projects" && grant.Method == "/api/v1/queries/owner-authority/workload" && grant.HttpMethods.SequenceEqual(new[] { "POST" }));
        gatewayInvocations.ShouldContain(grant => grant.CallerAppId == "projects" && grant.Method == "/api/v1/streams/read/workload" && grant.HttpMethods.SequenceEqual(new[] { "POST" }));
        dapr.DeclaredInvocations("memories").ShouldContain(grant => grant.CallerAppId == "folders" && grant.Method == "/api/search" && grant.HttpMethods.SequenceEqual(new[] { "GET" }));
        dapr.DeclaredCallers("memories").ShouldNotContain("folders-workers");

        // Projects standalone also binds its shared receipt, anonymous-bypass refusal, and channel token without Folders.
        List<PlatformServerBinding> projectsOnly =
        [
            Server(gateway, "gateway", "eventstore"),
            Server(projects, "domain", "projects"),
        ];
        PlatformLocalProfile.BindProjects(projectsOnly);
        projectsOnly[1].Environment["Authentication__HumanAuthorityReceipt__SigningKey"]
            .ShouldBe(projectsOnly[0].Environment["Authentication__HumanAuthorityReceipt__SigningKey"]);
        projectsOnly[1].Environment["Authentication__JwtBearer__AllowAnonymousDevelopment"].ShouldBe("false");
        projectsOnly[1].Environment["APP_API_TOKEN"].ShouldBe(projectsOnly[1].Environment["EventStore__AppChannelToken"]);

        // Calling BindProjects before BindFolders still produces one shared receipt across gateway, Projects, and Folders,
        // and replaces any whitespace-only pre-populated receipt value.
        List<PlatformServerBinding> projectsFirst =
        [
            new(gateway, "gateway", "eventstore", "eventstore", null, null, "/ready",
                new Dictionary<string, string>(StringComparer.Ordinal) { ["Authentication__HumanAuthorityReceipt__SigningKey"] = "   " }, []),
            Server(memories, "server", "memories"),
            Server(folders, "domain", "folders"),
            Server(folders, "workers", "folders-workers"),
            Server(projects, "domain", "projects"),
        ];
        PlatformLocalProfile.BindProjects(projectsFirst);
        PlatformLocalProfile.BindFolders(PlatformTestRepository.Root, projectsFirst);
        string reversedReceipt = projectsFirst[0].Environment["Authentication__HumanAuthorityReceipt__SigningKey"];
        string.IsNullOrWhiteSpace(reversedReceipt).ShouldBeFalse();
        projectsFirst[4].Environment["Authentication__HumanAuthorityReceipt__SigningKey"].ShouldBe(reversedReceipt);
        projectsFirst[2].Environment["Authentication__HumanAuthorityReceipt__SigningKey"].ShouldBe(reversedReceipt);

        // Missing gateway fails closed naming Projects and eventstore.
        List<PlatformServerBinding> missingGateway = [Server(projects, "domain", "projects")];
        InvalidDataException noGateway = Should.Throw<InvalidDataException>(() => PlatformLocalProfile.BindProjects(missingGateway));
        noGateway.Message.ShouldContain("projects");
        noGateway.Message.ShouldContain("eventstore");
    }

    [Fact]
    public async Task CompleteSourceCompositionBindsAllSevenMvpModulesConversationsRealmCatalogAndExtensions()
    {
        string root = PlatformTestRepository.Root;
        string manifest = Path.Combine(root, PlatformDeclarationSet.CompleteManifest);
        string? renderedDirectory = null;
        try
        {
            SourceMapping resolved = await WorkspaceRootResolver.ResolveAsync(manifest, WorkspaceMode.Source, TestContext.Current.CancellationToken);
            resolved.IdentityFindings.ShouldAllBe(finding => finding.RuleId == "HXW010");
            SourceMapping mapping = resolved with { IdentityFindings = [] };
            IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions
            {
                Args = ["--environment", "Development"],
                DisableDashboard = true,
            });
            builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(root, "test-dcp-not-started");
            builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(root, "test-dashboard-not-started");
            PlatformCompositionOptions options = new()
            {
                PlatformRoot = root,
                Mapping = mapping,
                ActiveModule = PlatformDeclarationSet.CompleteModule,
                DeclarationPaths = [manifest],
                UseGeneratedRealm = true,
                EnvironmentId = "complete-source-test",
            };

            PlatformAppModel.Compose(builder, options);

            // Capture rendered workspace immediately so finally cleans it up even if any assertion fails.
            ContainerResource keycloak = builder.Resources.OfType<ContainerResource>().Single(resource => resource.Name == "platform-local-realm");
            string realmMount = keycloak.Annotations.OfType<ContainerMountAnnotation>()
                .Single(mount => mount.Target == PlatformLocalRealm.ImportContainerDirectory + "hexalith-local-realm.json").Source!;
            renderedDirectory = Path.GetDirectoryName(realmMount)!;

            PlatformServerBinding[] bindings = builder.Resources
                .Select(resource => resource.Annotations.OfType<PlatformServerAnnotation>().SingleOrDefault()?.Binding)
                .OfType<PlatformServerBinding>()
                .ToArray();
            bindings.Select(binding => PlatformDeclarationSet.Id(binding.Module.Declaration)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                .ShouldBe(["conversations", "eventstore", "folders", "memories", "parties", "projects", "tenants"]);
            bindings.Select(binding => binding.ResourceId).Order(StringComparer.Ordinal).ShouldBe(
            [
                "conversations", "conversations-api", "eventstore", "folders", "folders-workers",
                "memories", "memories-access-telemetry", "memories-access-telemetry-clock", "memories-domain-service",
                "parties", "projects", "tenants", "tenants-api",
            ]);

            PlatformServerBinding gateway = bindings.Single(binding => binding.ResourceId == "eventstore");
            gateway.EventStoreHost.ShouldNotBeNull();
            gateway.EventStoreHost!.Extensions.Select(extension => extension.PackageId).Order(StringComparer.Ordinal)
                .ShouldBe(["Hexalith.Folders.EventStoreExtensions", "Hexalith.Projects.EventStoreExtensions"]);
            gateway.ProjectPath.ShouldBe(PlatformEventStoreExtensionBindings.HostProjectPath(options));

            // Projects fixed Conversation-start state component is rendered with matching Redis port/scope, owned by projects, and attached only to Projects.
            IDaprComponentResource setupComponent = builder.Resources.OfType<IDaprComponentResource>()
                .Single(component => component.Name == "projects-conversation-start-setup");
            setupComponent.Annotations.OfType<PlatformResourceOwnerAnnotation>().Single().ModuleId.ShouldBe("projects");
            ContainerResource redis = builder.Resources.OfType<ContainerResource>().Single(resource => resource.Name == "redis");
            int redisPort = redis.Annotations.OfType<EndpointAnnotation>().Single(endpoint => endpoint.Name == "tcp").Port!.Value;
            string setupYamlPath = Path.Combine(renderedDirectory, "projects-conversation-start-setup.yaml");
            File.Exists(setupYamlPath).ShouldBeTrue();
            YamlStream setupYaml = new();
            setupYaml.Load(new StringReader(await File.ReadAllTextAsync(setupYamlPath, TestContext.Current.CancellationToken)));
            YamlMappingNode setupRoot = (YamlMappingNode)setupYaml.Documents[0].RootNode;
            ((YamlScalarNode)((YamlMappingNode)setupRoot["metadata"])["name"]).Value.ShouldBe("projects-conversation-start-setup");
            YamlMappingNode setupSpec = (YamlMappingNode)setupRoot["spec"];
            ((YamlScalarNode)setupSpec["type"]).Value.ShouldBe("state.redis");
            Dictionary<string, string?> setupMetadata = ((YamlSequenceNode)setupSpec["metadata"]).Children
                .Cast<YamlMappingNode>()
                .ToDictionary(item => ((YamlScalarNode)item["name"]).Value!, item => ((YamlScalarNode)item["value"]).Value, StringComparer.Ordinal);
            setupMetadata["redisHost"].ShouldBe("127.0.0.1:" + redisPort);
            setupMetadata["keyPrefix"].ShouldBe("name");
            ((YamlSequenceNode)setupRoot["scopes"]).Children.Cast<YamlScalarNode>().Select(node => node.Value).ToArray()
                .ShouldBe([bindings.Single(binding => binding.ResourceId == "projects").AppId]);

            IResource projectsResource = builder.Resources.Single(resource => resource.Name == "projects");
            IDaprSidecarResource projectsSidecar = projectsResource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
            projectsSidecar.Annotations.OfType<DaprComponentReferenceAnnotation>()
                .Select(reference => reference.Component.Name).ShouldContain("projects-conversation-start-setup");
            foreach (string otherResource in new[] { "eventstore", "folders", "conversations" })
            {
                IDaprSidecarResource otherSidecar = builder.Resources.Single(resource => resource.Name == otherResource)
                    .Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
                otherSidecar.Annotations.OfType<DaprComponentReferenceAnnotation>()
                    .Select(reference => reference.Component.Name).ShouldNotContain("projects-conversation-start-setup");
            }

            // Local realm container renders all expected workload clients, audiences, and bound query grants.
            JsonNode realm = JsonNode.Parse(await File.ReadAllTextAsync(realmMount, TestContext.Current.CancellationToken))!;
            JsonArray clients = realm["clients"]!.AsArray();
            clients.Select(client => client!["clientId"]!.GetValue<string>()).Order(StringComparer.Ordinal)
                .ShouldBe(["conversations-tenant-access", "eventstore", "folders", "folders-workers", "mcpcli", "memories", PlatformRealmOperator.ClientId, "projects", "tenants"]);
            // The instance-only operator can request the cutover role and the relay's four permissions, one per scope.
            clients.Single(client => client!["clientId"]!.GetValue<string>() == PlatformRealmOperator.ClientId)!["optionalClientScopes"]!.AsArray()
                .Select(scope => scope!.GetValue<string>()).ShouldBe(
                [
                    "platform-operator.global-administrator", "platform-operator.global-administrators.bootstrap", "platform-operator.global-administrators.read",
                    "platform-operator.tenants.create", "platform-operator.tenants.read",
                ]);
            JsonNode projectsClient = clients.Single(client => client!["clientId"]!.GetValue<string>() == "projects")!;
            string[] projectsScopes = projectsClient["optionalClientScopes"]!.AsArray().Select(scope => scope!.GetValue<string>()).ToArray();
            projectsScopes.ShouldContain("eventstore-audience.eventstore");
            projectsScopes.ShouldContain("eventstore-operation.eventstore.owner-authority-query");
            projectsScopes.ShouldContain("eventstore-operation.eventstore.stream-read");

            JsonNode realmInstance = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(renderedDirectory, "realm-instance.json"), TestContext.Current.CancellationToken))!;
            string syntheticTenantId = realmInstance["syntheticTenantId"]!.GetValue<string>();
            syntheticTenantId.ShouldNotBeNullOrWhiteSpace();
            string issuer = realmInstance["issuer"]!.GetValue<string>();
            issuer.ShouldEndWith("/realms/hexalith-local");

            Dictionary<string, object> keycloakEnv = await EvaluateEnvironmentAsync(keycloak);
            string encodedGrants = keycloakEnv["HEXALITH_LOCAL_REALM_BINDINGS_EVENTSTORE"].ToString()!;
            string decodedGrants = JsonSerializer.Deserialize<string>('"' + encodedGrants + '"')!;
            JsonNode[] boundQueryGrants = JsonNode.Parse(decodedGrants)!.AsArray()
                .Where(grant => grant!["allowQueryAuthorityDigest"]!.GetValue<bool>())
                .Select(grant => grant!)
                .ToArray();
            boundQueryGrants.Select(grant => grant["audience"]!.GetValue<string>()).Order(StringComparer.Ordinal)
                .ShouldBe(["conversation", "folders", "parties", "projects", "tenants"]);
            foreach ((string audience, string expectedDomain, string[] expectedTenants) in new (string, string, string[])[]
            {
                ("projects", "projects", [syntheticTenantId]),
                ("conversation", "conversations", [syntheticTenantId]),
                ("folders", "folders", [syntheticTenantId]),
                ("tenants", "tenants", [syntheticTenantId, "system"]),
                ("parties", "party", [syntheticTenantId]),
            })
            {
                JsonNode grant = boundQueryGrants.Single(item => item["audience"]!.GetValue<string>() == audience);
                grant["allowUnbound"]!.GetValue<bool>().ShouldBeFalse();
                grant["bindings"]!["eventstore:bound-domain"]!.AsArray().Select(node => node!.GetValue<string>()).ShouldContain(expectedDomain);
                grant["bindings"]!["eventstore:bound-tenant"]!.AsArray().Select(node => node!.GetValue<string>())
                    .Order(StringComparer.Ordinal).ShouldBe(expectedTenants.Order(StringComparer.Ordinal));
            }

            // Generated catalog and Projects/Folders/Conversations runtime environment bindings.
            Dictionary<string, object> gatewayEnv = await EvaluateEnvironmentAsync(builder.Resources.Single(resource => resource.Name == "eventstore"));
            gatewayEnv["Authentication__BoundStreamRead__AllowedDomains__projects"].ShouldBe("projects");
            string allowedProjectsQueries = gatewayEnv["Authentication__BoundOwnerQuery__AllowedQueries__projects"].ToString()!;
            allowedProjectsQueries.Split(',').ShouldBe(
            [
                "conversation-detail",
                "Hexalith.Conversations.Queries.GetConversationAdvisoryMetadata.v1",
                "Hexalith.Folders.Queries.AuthorizeFolderRead.v1",
                "Hexalith.Tenants.Contracts.Queries.ResolveTenantOwnerAuthority",
                "Hexalith.Parties.Contracts.Queries.ResolveChatBotPartyAuthority",
            ]);
            gatewayEnv["EventStore__Publisher__TopicOverrides__folders"].ShouldBe("folders.events");
            gatewayEnv["Authentication__DaprInternal__Audience"].ShouldBe("eventstore");
            string[] allowedCallers = gatewayEnv.Where(pair => pair.Key.StartsWith("Authentication__DaprInternal__AllowedCallers__", StringComparison.Ordinal))
                .Select(pair => pair.Value.ToString()!).Order(StringComparer.Ordinal).ToArray();
            allowedCallers.ShouldBe(["folders", "folders-workers", "memories", "projects", "tenants"]);

            Dictionary<string, object> projectsEnv = await EvaluateEnvironmentAsync(projectsResource);
            projectsEnv["Authentication__JwtBearer__AllowAnonymousDevelopment"].ShouldBe("false");
            projectsEnv["Projects__DevelopmentRouteCatalog__Enabled"].ShouldBe("true");
            projectsEnv["APP_API_TOKEN"].ShouldBe(projectsEnv["EventStore__AppChannelToken"]);
            projectsEnv["Authentication__HumanAuthorityReceipt__SigningKey"].ShouldBe(gatewayEnv["Authentication__HumanAuthorityReceipt__SigningKey"]);
            projectsEnv["Authentication__WorkloadIssuer__ClientId"].ShouldBe("projects");
            using (RSA projectsProof = RSA.Create())
            {
                projectsProof.ImportPkcs8PrivateKey(Convert.FromBase64String(projectsEnv["Authentication__BoundStreamRead__PrivateKey"].ToString()!), out _);
                Convert.ToBase64String(projectsProof.ExportSubjectPublicKeyInfo())
                    .ShouldBe(gatewayEnv["Authentication__BoundStreamRead__PublicKeys__projects"].ToString());
            }

            Dictionary<string, object> conversationsEnv = await EvaluateEnvironmentAsync(builder.Resources.Single(resource => resource.Name == "conversations"));
            conversationsEnv["Authentication__WorkloadJwtBearer__Authority"].ShouldBe(issuer);
            conversationsEnv["Authentication__WorkloadJwtBearer__Issuer"].ShouldBe(issuer);
            conversationsEnv["Authentication__WorkloadJwtBearer__RequireHttpsMetadata"].ShouldBe("false");
            conversationsEnv["EventStore__Authentication__Authority"].ShouldBe(issuer);
            conversationsEnv["EventStore__Authentication__TokenEndpoint"].ShouldBe(issuer + "/protocol/openid-connect/token");
            conversationsEnv["EventStore__Authentication__GrantType"].ShouldBe("client_credentials");
            conversationsEnv["EventStore__Authentication__ClientId"].ShouldBe(PlatformRealmContract.ConversationsReader);
            conversationsEnv.ContainsKey("EventStore__Authentication__ClientSecret").ShouldBeTrue();

            string catalogPath = gatewayEnv["EventStore__RouteCatalog__DevelopmentBootstrap__CatalogPath"].ToString()!;
            File.Exists(catalogPath).ShouldBeTrue();
            string receiptPath = Path.Combine(Path.GetDirectoryName(catalogPath)!, "receipt.json");
            JsonNode receipt = JsonNode.Parse(await File.ReadAllTextAsync(receiptPath, TestContext.Current.CancellationToken))!;
            receipt["ownerAssemblies"]!.AsArray().Count.ShouldBe(6);
            receipt["adapterAssemblies"]!.AsArray().Count.ShouldBe(6);

            // Direct Development host with generated realm never schedules symmetric-key gateway startup (verify resolved factory services).
            await using ServiceProvider provider = builder.Services.BuildServiceProvider();
            provider.GetServices<IHostedService>().OfType<PlatformGatewayStartup>().ShouldBeEmpty();
            // The realm-signed operator token source replaces the runner key for the Builds cutover; without a run plan no task relay exists.
            provider.GetServices<ICompositionOperatorTokenSource>().ShouldHaveSingleItem().OperatorName.ShouldBe(PlatformRealmOperator.ClientId);
            provider.GetServices<ICompositionRunPreparation>().ShouldBeEmpty();
        }
        finally
        {
            if (renderedDirectory is not null && Directory.Exists(renderedDirectory)) Directory.Delete(renderedDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("conversations", "conversation", "conversation-detail", false)]
    [InlineData("conversations", "conversation", "Hexalith.Conversations.Queries.GetConversationAdvisoryMetadata.v1", false)]
    [InlineData("folders", "folders", "Hexalith.Folders.Queries.AuthorizeFolderRead.v1", false)]
    [InlineData("tenants", "tenants", "Hexalith.Tenants.Contracts.Queries.ResolveTenantOwnerAuthority", false)]
    [InlineData("parties", "parties", "Hexalith.Parties.Contracts.Queries.ResolveChatBotPartyAuthority", false)]
    [InlineData("conversations", "conversation", "conversation-detail", true)]
    [InlineData("folders", "folders", "Hexalith.Folders.Queries.AuthorizeFolderRead.v1", true)]
    [InlineData("tenants", "tenants", "Hexalith.Tenants.Contracts.Queries.ResolveTenantOwnerAuthority", true)]
    [InlineData("parties", "parties", "Hexalith.Parties.Contracts.Queries.ResolveChatBotPartyAuthority", true)]
    public void ProjectsOwnerQueryResolutionRefusesMissingOrMismatchedRouteAppId(string owner, string expectedAppId, string query, bool wrongAppId)
    {
        (string RouteId, string Domain, string MessageType, string AppId)[] entries =
        [
            ("r-conv-detail", "conversations", "conversation-detail", "conversation"),
            ("r-conv-advisory", "conversations", "Hexalith.Conversations.Queries.GetConversationAdvisoryMetadata.v1", "conversation"),
            ("r-folders-read", "folders", "Hexalith.Folders.Queries.AuthorizeFolderRead.v1", "folders"),
            ("r-tenants-auth", "tenants", "Hexalith.Tenants.Contracts.Queries.ResolveTenantOwnerAuthority", "tenants"),
            ("r-parties-auth", "party", "Hexalith.Parties.Contracts.Queries.ResolveChatBotPartyAuthority", "parties"),
        ];
        HashSet<string> enrolled = new(["conversations", "folders", "tenants", "parties"], StringComparer.Ordinal);
        RouteCatalogEnvelope BuildEnvelope(IEnumerable<(string RouteId, string Domain, string MessageType, string AppId)> selected)
        {
            var rows = selected.ToArray();
            return new RouteCatalogEnvelope(
                3, 1, "Development", false,
                rows.Select(row => new RouteContractDeclaration(row.RouteId, "query", row.Domain, row.MessageType, null, "v1", null, "workload", "domain-service:query", null, [])).ToArray(),
                rows.Select(row => new RouteCatalogEntry(row.RouteId, 1, "digest-" + row.RouteId, row.AppId, "query", "v1", "workload", "domain-service:query", false)).ToArray(),
                [], "route-digest", "idempotency-digest");
        }

        PlatformRoutingCatalogBootstrap.ResolveProjectsAllowedOwnerQueries(BuildEnvelope(entries), enrolled)
            .ShouldBe(entries.Select(row => row.MessageType).ToArray());

        IEnumerable<(string RouteId, string Domain, string MessageType, string AppId)> mutated = wrongAppId
            ? entries.Select(row => row.MessageType == query ? row with { AppId = "wrong-app" } : row)
            : entries.Where(row => row.MessageType != query);
        InvalidDataException refusal = Should.Throw<InvalidDataException>(() =>
            PlatformRoutingCatalogBootstrap.ResolveProjectsAllowedOwnerQueries(BuildEnvelope(mutated), enrolled));
        refusal.Message.ShouldContain(owner);
        refusal.Message.ShouldContain(query);
        refusal.Message.ShouldContain(expectedAppId);
    }

    [Fact]
    public async Task CompleteCompositionRefusesMissingMappedOwnerOrExtensionBeforeAddingResources()
    {
        string root = PlatformTestRepository.Root;
        string manifest = Path.Combine(root, PlatformDeclarationSet.CompleteManifest);
        SourceMapping resolved = await WorkspaceRootResolver.ResolveAsync(manifest, WorkspaceMode.Source, TestContext.Current.CancellationToken);
        resolved.IdentityFindings.ShouldAllBe(finding => finding.RuleId == "HXW010");
        SourceMapping full = resolved with { IdentityFindings = [] };

        SourceMapping missingFolders = full with
        {
            Entries = full.Entries.Where(entry => !entry.Identity.Equals("Hexalith.Folders", StringComparison.OrdinalIgnoreCase)).ToArray(),
        };
        IDistributedApplicationBuilder missingOwnerBuilder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        InvalidDataException ownerError = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(missingOwnerBuilder, new PlatformCompositionOptions
        {
            PlatformRoot = root,
            Mapping = missingFolders,
            ActiveModule = PlatformDeclarationSet.CompleteModule,
            DeclarationPaths = [manifest],
        }));
        ownerError.Message.ShouldContain("Hexalith.Folders");
        missingOwnerBuilder.Resources.ShouldBeEmpty();

        IDistributedApplicationBuilder missingProjectBuilder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        InvalidDataException projectError = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(missingProjectBuilder, new PlatformCompositionOptions
        {
            PlatformRoot = root,
            Mapping = full,
            ActiveModule = PlatformDeclarationSet.CompleteModule,
            DeclarationPaths = [manifest],
            Projects = PlatformServerBindings.ReferenceProjects.Where(pair => pair.Key != "projects/domain").ToDictionary(StringComparer.Ordinal),
        }));
        projectError.Message.ShouldContain("projects/domain");
        projectError.Message.ShouldContain("Hexalith.Projects");
        missingProjectBuilder.Resources.ShouldBeEmpty();

        // Missing declared EventStore extension project refuses before adding any resource.
        using PlatformOwnerFixture foldersFixture = PlatformOwnerFixture.Copy(root, "folders");
        SourceMapping missingExtension = full with
        {
            Entries = full.Entries.Select(entry => entry.Identity.Equals("Hexalith.Folders", StringComparison.OrdinalIgnoreCase)
                ? entry with { Path = foldersFixture.Root }
                : entry).ToArray(),
        };
        IDistributedApplicationBuilder missingExtensionBuilder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        InvalidDataException extensionError = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(missingExtensionBuilder, new PlatformCompositionOptions
        {
            PlatformRoot = root,
            Mapping = missingExtension,
            ActiveModule = "folders",
            DeclarationPaths = [Path.Combine(foldersFixture.Root, "module", "hexalith.module-manifest.v2.json")],
        }));
        extensionError.Message.ShouldContain("Hexalith.Folders.EventStoreExtensions");
        extensionError.Message.ShouldContain("eventstore-extension-unavailable");
        missingExtensionBuilder.Resources.ShouldBeEmpty();

        // Generated local realm without an eventstore gateway refuses before creating directories or adding resources.
        string gatewaylessManifest = Path.Combine(foldersFixture.Root, "gatewayless.v2.json");
        await File.WriteAllTextAsync(gatewaylessManifest, """
            {
              "schema": "hexalith.module-manifest.v2",
              "modules": [{
                "identity": { "moduleId": "gatewayless", "servers": [], "dependencies": [] },
                "runtime": { "dapr": [], "extensions": [], "resources": [] },
                "surfaces": { "interfaces": [] },
                "integration": { "topics": [], "secrets": [], "dynamicSecretNamespaces": [], "identityNeeds": [], "egress": [], "providers": [], "workers": [] },
                "lifecycle": { "readiness": [], "tasks": [], "recoveryHooks": [], "fenceHooks": [], "replay": { "reopenBeforeReplay": false }, "startup": {}, "criticalFlows": [], "smokeSurfaces": [], "classification": { "changeClass": "additive", "reason": "test" }, "recoveryInventory": [] }
              }]
            }
            """, TestContext.Current.CancellationToken);
        IDistributedApplicationBuilder noGatewayBuilder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        InvalidDataException realmError = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(noGatewayBuilder, new PlatformCompositionOptions
        {
            PlatformRoot = root,
            Mapping = full,
            ActiveModule = "gatewayless",
            DomainModule = false,
            DeclarationPaths = [gatewaylessManifest],
            UseGeneratedRealm = true,
        }));
        realmError.Message.ShouldContain("eventstore");
        realmError.Message.ShouldContain("generated local realm");
        noGatewayBuilder.Resources.ShouldBeEmpty();
    }

    [Fact]
    public void MultipleDeclaredDeadlinesSelectLargestFiniteOverrideWithOwnerAndReason()
    {
        PlatformModuleDeclaration[] declarations =
        [
            new("a.json", JsonSerializer.SerializeToElement(new
            {
                identity = new { moduleId = "folders" },
                lifecycle = new { startup = new { @override = new { timeoutSeconds = 720, justification = "Folders index warmup" } } },
            })),
            new("b.json", JsonSerializer.SerializeToElement(new
            {
                identity = new { moduleId = "projects" },
                lifecycle = new { startup = new { @override = new { timeoutSeconds = 900, justification = "Projects cold catalog and projections" } } },
            })),
            new("c.json", JsonSerializer.SerializeToElement(new
            {
                identity = new { moduleId = "memories" },
                lifecycle = new { startup = new { } },
            })),
        ];

        CompositionEffectiveDeadline largest = CompositionStartupDeadlineResolver.Resolve(declarations, TimeSpan.FromMinutes(10));
        largest.IsOverride.ShouldBeTrue();
        largest.TimeoutSeconds.ShouldBe(900);
        largest.ModuleId.ShouldBe("projects");
        largest.Justification.ShouldBe("Projects cold catalog and projections");
        string described = CompositionStartupDeadlineResolver.Describe(largest);
        described.ShouldContain("900s");
        described.ShouldContain("projects");
        described.ShouldContain("Projects cold catalog and projections");

        CompositionEffectiveDeadline defaultDeadline = CompositionStartupDeadlineResolver.Resolve([declarations[2]], TimeSpan.FromMinutes(10));
        defaultDeadline.IsOverride.ShouldBeFalse();
        defaultDeadline.TimeoutSeconds.ShouldBe(600);
        defaultDeadline.ModuleId.ShouldBeNull();
    }

    [Fact]
    public async Task StartupExpiryOrFailureRetainsDiagnosticsAndCleansUpByExactRunIdentityAsync()
    {
        string stateDirectory = Path.Combine(Path.GetTempPath(), "platform-complete-expiry-" + Guid.NewGuid().ToString("N"));
        string workspaceRoot = Path.Combine(stateDirectory, "runs");
        string failedWorkspace = Path.Combine(stateDirectory, "failed-direct-workspace");
        string cleanWorkspace = Path.Combine(stateDirectory, "clean-direct-workspace");
        try
        {
            CompositionWorkspace.CreatePrivateDirectory(stateDirectory);
            CompositionWorkspace.CreatePrivateDirectory(workspaceRoot);
            Directory.CreateDirectory(failedWorkspace);
            Directory.CreateDirectory(cleanWorkspace);

            // Failed direct workspace keeps its failure marker on disposal; clean workspace is removed.
            PlatformCompositionWorkspace.MarkFailed(failedWorkspace, "projects: /ready timed out");
            using (new PlatformCompositionWorkspace(failedWorkspace)) { }
            File.ReadAllText(Path.Combine(failedWorkspace, PlatformCompositionWorkspace.FailureMarker)).ShouldContain("projects: /ready timed out");
            using (new PlatformCompositionWorkspace(cleanWorkspace)) { }
            Directory.Exists(cleanWorkspace).ShouldBeFalse();

            // Exact-run teardown removes only the targeted run identity while preserving diagnostics and sibling runs.
            string fakeDocker = Path.Combine(stateDirectory, OperatingSystem.IsWindows() ? "fake-docker.cmd" : "fake-docker.sh");
            await File.WriteAllTextAsync(fakeDocker, OperatingSystem.IsWindows() ? "@echo off\r\nexit /b 0\r\n" : "#!/bin/sh\nexit 0\n", TestContext.Current.CancellationToken);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(fakeDocker, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            CompositionEngine engine = new(new CompositionEngineOptions(typeof(PlatformAppModel).Assembly.Location, typeof(PlatformAppModel).Assembly.Location)
            {
                StateDirectory = stateDirectory,
                WorkspaceRoot = workspaceRoot,
                DockerCommand = fakeDocker,
            });
            string targetRunId = CompositionRunPlanFactory.NewRunId();
            string siblingRunId = CompositionRunPlanFactory.NewRunId();
            string targetWorkspace = Path.Combine(workspaceRoot, targetRunId);
            string siblingWorkspace = Path.Combine(workspaceRoot, siblingRunId);
            string diagnosticsDirectory = Path.Combine(stateDirectory, "diagnostics", targetRunId);
            CompositionWorkspace.CreatePrivateDirectory(targetWorkspace);
            CompositionWorkspace.CreatePrivateDirectory(siblingWorkspace);
            CompositionWorkspace.CreatePrivateDirectory(diagnosticsDirectory);

            CompositionResourceReadiness unready = new("projects", "project", "Unhealthy") { Detail = "timed out", ModuleId = "projects" };
            string summaryPath = Path.Combine(diagnosticsDirectory, "summary.json");
            await CompositionDocumentStore.WriteAsync(
                summaryPath,
                new CompositionStartupReport(CompositionStartupReport.SupportedSchema, targetRunId, "HXR021", 612.4, 600, false, null, null, null, [unready]),
                TestContext.Current.CancellationToken);
            await engine.StateStore.WriteAsync(new CompositionRunState(CompositionRunState.SupportedSchema, targetRunId, CompositionRunStatus.Failed, "hash-a", targetWorkspace, null, null, "HXR021", DateTimeOffset.UtcNow)
            {
                StartupDurationSeconds = 612.4,
                EffectiveDeadlineSeconds = 600,
                ReadinessResults = [unready],
            }, TestContext.Current.CancellationToken);
            await engine.StateStore.WriteAsync(new CompositionRunState(CompositionRunState.SupportedSchema, siblingRunId, CompositionRunStatus.Ready, "hash-b", siblingWorkspace, null, null, null, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

            CompositionDownResult badId = await engine.DownAsync("not-a-run-id", TestContext.Current.CancellationToken);
            badId.Result.Diagnostics.ShouldContain(diagnostic => diagnostic.RuleId == "HXR024");

            CompositionDownResult down = await engine.DownAsync(targetRunId, TestContext.Current.CancellationToken);
            down.Result.Status.ShouldNotBe("failed");
            Directory.Exists(targetWorkspace).ShouldBeFalse();
            (await engine.StateStore.TryReadAsync(targetRunId, TestContext.Current.CancellationToken)).ShouldBeNull();
            Directory.Exists(siblingWorkspace).ShouldBeTrue();
            (await engine.StateStore.TryReadAsync(siblingRunId, TestContext.Current.CancellationToken)).ShouldNotBeNull();

            // Preserved failure diagnostics survive exact-run teardown with actual duration and unready resource names.
            CompositionStartupReport? preserved = await CompositionDocumentStore.TryReadAsync<CompositionStartupReport>(summaryPath, TestContext.Current.CancellationToken);
            preserved.ShouldNotBeNull();
            preserved!.RuleId.ShouldBe("HXR021");
            preserved.DurationSeconds.ShouldBe(612.4);
            preserved.Resources.ShouldContain(resource => resource.Name == "projects" && resource.State == "Unhealthy");
        }
        finally
        {
            if (Directory.Exists(stateDirectory)) Directory.Delete(stateDirectory, recursive: true);
        }
    }

    private static async Task<Dictionary<string, object>> EvaluateEnvironmentAsync(IResource resource)
    {
        Dictionary<string, object> values = new(StringComparer.Ordinal);
        EnvironmentCallbackContext context = new(new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run), resource, values);
        foreach (EnvironmentCallbackAnnotation annotation in resource.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await annotation.Callback(context).ConfigureAwait(false);
        return values;
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
