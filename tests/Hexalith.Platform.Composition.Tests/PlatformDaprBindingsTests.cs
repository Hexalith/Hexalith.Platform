namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Shouldly;
using YamlDotNet.RepresentationModel;

public sealed partial class PlatformAppModelTests
{
    [Fact]
    public async Task FullPackageFixtureKeepsGatewaySeedAndContainerSubscriptionBindingsAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-package-bindings-" + Guid.NewGuid().ToString("N"));
        string declarations = Path.Combine(directory, "src", "Hexalith.Platform.Composition", "Declarations");
        string catalog = Path.Combine(directory, "references", "Hexalith.Builds", "Props");
        Directory.CreateDirectory(declarations);
        Directory.CreateDirectory(catalog);
        IDistributedApplicationBuilder builder = Builder();
        try
        {
            string originals = Path.Combine(Root, "src", "Hexalith.Platform.Composition", "Declarations");
            File.Copy(Path.Combine(originals, "local-profile.json"), Path.Combine(declarations, "local-profile.json"));
            File.Copy(Path.Combine(Root, "references", "Hexalith.Builds", "Props", "Directory.Packages.props"), Path.Combine(catalog, "Directory.Packages.props"));
            JsonNode provenance = JsonNode.Parse(File.ReadAllText(Path.Combine(originals, "provenance.json")))!;
            Dictionary<string, string> hashes = [];
            foreach (string owner in new[] { "eventstore", "tenants", "memories" })
            {
                string snapshot = Path.Combine(declarations, owner + ".json");
                File.Copy(Path.Combine(Root, "references", PlatformServerBindings.Repository(owner), "module", "hexalith.module-manifest.v2.json"), snapshot);
                string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(snapshot)));
                provenance[owner]!["sha256"] = hash;
                hashes[owner + "/" + provenance[owner]!["version"]!.GetValue<string>()] = hash;
            }
            File.WriteAllText(Path.Combine(declarations, "provenance.json"), provenance.ToJsonString());
            PlatformCompositionOptions options = Options(WorkspaceMode.Package) with
            {
                PlatformRoot = directory, ComponentsDirectory = Path.Combine(Root, "DaprComponents", "reference"), ReleaseSnapshotHashes = hashes,
                ImageProbeRunner = (_, _, _) => Task.FromResult(new CompositionProcessResult(true, 0, "{}", false)),
            };
            PlatformAppModel.Compose(builder, options);
            IResource[] servers = builder.Resources.Where(resource => resource.Annotations.OfType<PlatformServerAnnotation>().Any()).ToArray();
            servers.Length.ShouldBe(6);
            ProjectResource gateway = servers.OfType<ProjectResource>().Single();
            gateway.Name.ShouldBe("eventstore");
            gateway.GetProjectMetadata().ProjectPath.ShouldBe(options.EventStoreSeedProject);
            servers.OfType<ContainerResource>().Count().ShouldBe(5);
            foreach (IResource server in servers)
            {
                var sidecar = server.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
                string subscriptions = sidecar.ResourcesPaths.Single();
                Directory.GetFiles(subscriptions, "*.yaml").Length.ShouldBe(2);
                using JsonDocument acl = JsonDocument.Parse(File.ReadAllText(sidecar.Config!));
                acl.RootElement.GetProperty("spec").GetProperty("mtls").GetProperty("enabled").GetBoolean().ShouldBeTrue();
                Dictionary<string, object> environment = [];
                foreach (EnvironmentCallbackAnnotation callback in server.Annotations.OfType<EnvironmentCallbackAnnotation>())
                    await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, server, environment, TestContext.Current.CancellationToken));
                if (server.Name != "eventstore") environment["EventStore__CommandGateway__BaseAddress"].ShouldBeOfType<EndpointReference>().Resource.ShouldBe(gateway);
                if (server.Name == "tenants-api") environment["EventStore__GatewayReadiness__BaseAddress"].ShouldBeOfType<EndpointReference>().Resource.ShouldBe(gateway);
            }
        }
        finally
        {
            string? policy = builder.Resources.SelectMany(resource => resource.Annotations.OfType<DaprSidecarAnnotation>())
                .Select(sidecar => sidecar.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.Config).FirstOrDefault();
            if (policy is not null) await CompositionWorkspace.TryDeleteAsync(Path.GetDirectoryName(policy)!, CancellationToken.None);
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public Task LiveLocalDaprEnforcesInvocationAndDeclarativeDeadLetterDeliveryAsync() => RunLiveDaprSecurityFixtureAsync(false);

    [Fact]
    public Task LiveTimesheetsDaprAllowsEstablishedCallerAndDeniesUnlistedCallerAsync() => RunLiveDaprSecurityFixtureAsync(true);

    private static async Task RunLiveDaprSecurityFixtureAsync(bool timesheetsOnly)
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE") != "1") Assert.Skip("Set HEXALITH_PLATFORM_BUILDS_LIVE_SMOKE=1 and HEXALITH_DAPR_HOME for live Dapr security fixtures.");
        var module = PlatformDeclarationSet.Load(Options(active: "memories")).Single(module => PlatformDeclarationSet.Id(module.Declaration) == "memories");
        var declaration = JsonNode.Parse(module.Declaration.GetRawText())!;
        var topics = declaration["integration"]!["topics"]!.AsArray();
        topics[0]!["subscription"] = JsonNode.Parse("{\"server\":\"server\",\"route\":\"/events/ingest\"}");
        topics[0]!["deadLetter"] = JsonNode.Parse("{\"strategy\":\"topic\",\"topic\":\"failed-events\"}");
        topics.Add(JsonNode.Parse("{\"name\":\"failed-events\",\"role\":\"events\",\"deadLetter\":{\"strategy\":\"none\"},\"subscription\":{\"server\":\"server\",\"route\":\"/dead-letter\",\"topic\":\"failed.physical\"}}"));
        string timesheetsSource = Path.Combine(Root, "eng", "timesheets-deployment", "hexalith.module-manifest.v2.json");
        using JsonDocument timesheets = JsonDocument.Parse(File.ReadAllText(timesheetsSource));
        var declarations = PlatformDeclarationSet.Load(Options()).Select(item => PlatformDeclarationSet.Id(item.Declaration) == "memories" ? item with { Declaration = JsonSerializer.SerializeToElement(declaration) } : item)
            .Append(new PlatformModuleDeclaration(timesheetsSource, timesheets.RootElement.GetProperty("modules")[0].Clone())).ToArray();
        var bindings = PlatformDaprBindings.Resolve(declarations);
        string directory = Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_SECURITY_EVIDENCE") ?? Path.Combine(Path.GetTempPath(), "platform-security-evidence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        int redisPort = Hexalith.Builds.Tooling.Runtime.CompositionPortAllocator.Allocate(1).Redis;
        foreach (var file in bindings.Render(Path.Combine(Root, "DaprComponents", "reference"), redisPort, redisPort, "/unused", null).Where(file => file.Key == "pubsub.yaml" || file.Key.StartsWith("accesscontrol.", StringComparison.Ordinal) || file.Key.StartsWith("subscription.", StringComparison.Ordinal))) File.WriteAllText(Path.Combine(directory, file.Key), file.Value);
        File.WriteAllText(Path.Combine(directory, "redis-port"), redisPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var result = await Hexalith.Builds.Tooling.Runtime.CompositionProcess.RunAsync(Hexalith.Builds.Tooling.Runtime.CompositionProcess.CreateStartInfo("python3", [Path.Combine(Root, "eng", "test-local-dapr-security.py"), directory, Environment.GetEnvironmentVariable("HEXALITH_DAPR_HOME")!, Hexalith.Builds.Tooling.Runtime.CompositionToolchainPins.DaprRuntimeVersion, Hexalith.Builds.Tooling.Runtime.CompositionToolchainPins.DaprCliVersion, Hexalith.Builds.Tooling.Runtime.CompositionToolchainPins.RedisImageReference], Root, new Dictionary<string, string> { ["HEXALITH_PLATFORM_SECURITY_SCOPE"] = timesheetsOnly ? "timesheets-invocation" : "full" }), TimeSpan.FromMinutes(3), TestContext.Current.CancellationToken);
        File.WriteAllText(Path.Combine(directory, "result.log"), result.Output + result.ErrorOutput);
        result.ExitCode.ShouldBe(0, result.Output + result.ErrorOutput + " Evidence: " + directory);
        result.Output.ShouldContain(timesheetsOnly ? "\"scope\": \"timesheets-invocation\"" : "\"declarativeDelivery\": true");
        result.Output.ShouldContain("no leftovers");
    }

    [Fact]
    public void TimesheetsReceiverGrantsOnlyEstablishedEventStorePostOperations()
    {
        string source = Path.Combine(Root, "eng", "timesheets-deployment", "hexalith.module-manifest.v2.json");
        var result = PlatformManifestValidator.Validate([source], TestContext.Current.CancellationToken);
        result.IsValid.ShouldBeTrue(string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var files = PlatformDaprBindings.Resolve(result.Declarations!).Render(Path.Combine(Root, "DaprComponents", "reference"), 16379, 16380, "/unused", null);
        using JsonDocument policy = JsonDocument.Parse(files["accesscontrol.timesheets.yaml"]);
        var acl = policy.RootElement.GetProperty("spec").GetProperty("accessControl");
        acl.GetProperty("defaultAction").GetString().ShouldBe("deny");
        var grants = acl.GetProperty("policies").EnumerateArray().ToArray();
        grants.Length.ShouldBe(1);
        grants[0].GetProperty("appId").GetString().ShouldBe("eventstore");
        var operations = grants[0].GetProperty("operations").EnumerateArray().ToArray();
        operations.Select(operation => operation.GetProperty("name").GetString()).Order().ShouldBe(new[] { "/process", "/query", "/project", "/replay-state", "/admin/operational-index-metadata" }.Order());
        operations.ShouldAllBe(operation => operation.GetProperty("httpVerb").EnumerateArray().Select(verb => verb.GetString()).SequenceEqual(new[] { "POST" }));
    }

    [Theory]
    [InlineData("eventstore")]
    [InlineData("memories")]
    [InlineData("tenants")]
    public void ResolvedComponentsHaveOnlySelectedConsumersAndEveryReceiverDeniesByDefault(string active)
    {
        var declarations = PlatformDeclarationSet.Load(Options(active: active));
        var bindings = PlatformDaprBindings.Resolve(declarations);
        var servers = PlatformAppModel.Prepare(Options(active: active));
        var files = bindings.Render(Path.Combine(Root, "DaprComponents", "reference"), 16379, 16380, "/tmp/local-secrets.json", null);
        var selected = servers.Select(server => server.AppId).ToHashSet(StringComparer.Ordinal);
        foreach (var file in files)
        {
            YamlStream yaml = new();
            yaml.Load(new StringReader(file.Value));
            var root = (YamlMappingNode)yaml.Documents[0].RootNode;
            if (((YamlScalarNode)root.Children[new YamlScalarNode("kind")]).Value == "Component")
            {
                var scopes = (YamlSequenceNode)root.Children[new YamlScalarNode("scopes")];
                scopes.Children.OfType<YamlScalarNode>().ShouldAllBe(scope => selected.Contains(scope.Value!));
                scopes.Children.ShouldNotBeEmpty();
            }
        }
        foreach (var server in servers)
        {
            using JsonDocument policy = JsonDocument.Parse(files["accesscontrol." + server.AppId + ".yaml"]);
            var spec = policy.RootElement.GetProperty("spec");
            spec.GetProperty("mtls").GetProperty("enabled").GetBoolean().ShouldBeTrue();
            spec.GetProperty("accessControl").GetProperty("defaultAction").GetString().ShouldBe("deny");
            foreach (var grant in spec.GetProperty("accessControl").GetProperty("policies").EnumerateArray())
            {
                grant.GetProperty("defaultAction").GetString().ShouldBe("deny");
                grant.GetProperty("trustDomain").GetString().ShouldBe("public");
                foreach (var operation in grant.GetProperty("operations").EnumerateArray()) operation.GetProperty("httpVerb").GetArrayLength().ShouldBeGreaterThan(0);
            }
        }
        var subscriptions = files.Where(file => file.Key.StartsWith("subscription.", StringComparison.Ordinal))
            .Select(file => JsonSerializer.Deserialize<JsonElement>(file.Value)).ToArray();
        subscriptions.Length.ShouldBe(active == "eventstore" ? 1 : 2);
        subscriptions.ShouldContain(subscription => subscription.GetProperty("scopes")[0].GetString() == "eventstore" && subscription.GetProperty("spec").GetProperty("topic").GetString() == "*.*.projection-changed" && subscription.GetProperty("spec").GetProperty("routes").GetProperty("default").GetString() == "/projections/changed");
        if (active != "eventstore") subscriptions.ShouldContain(subscription => subscription.GetProperty("scopes")[0].GetString() == "memories" && subscription.GetProperty("spec").GetProperty("topic").GetString() == "memories-events" && subscription.GetProperty("spec").GetProperty("routes").GetProperty("default").GetString() == "/events/ingest");
        subscriptions.ShouldAllBe(subscription => selected.Contains(subscription.GetProperty("scopes")[0].GetString()!));
    }

    [Fact]
    public void PublisherInventoriesAloneCreateNoSubscriptions()
    {
        var declarations = PlatformDeclarationSet.Load(Options()).Select(module =>
        {
            var declaration = JsonNode.Parse(module.Declaration.GetRawText())!;
            foreach (var topic in declaration["integration"]!["topics"]!.AsArray()) topic!.AsObject().Remove("subscription");
            return module with { Declaration = JsonSerializer.SerializeToElement(declaration) };
        }).ToArray();
        PlatformDaprBindings.Resolve(declarations).Render(Path.Combine(Root, "DaprComponents", "reference"), 16379, 16380, "/tmp/local-secrets.json", null)
            .Keys.ShouldNotContain(name => name.StartsWith("subscription.", StringComparison.Ordinal));
    }

    [Fact]
    public void GatewayAllowsOnlyEstablishedTenantsApiAndTimesheetsOperations()
    {
        var files = PlatformDaprBindings.Resolve(PlatformDeclarationSet.Load(Options())).Render(Path.Combine(Root, "DaprComponents", "reference"), 16379, 16380, "/tmp/local-secrets.json", null);
        using JsonDocument policy = JsonDocument.Parse(files["accesscontrol.eventstore.yaml"]);
        var grants = policy.RootElement.GetProperty("spec").GetProperty("accessControl").GetProperty("policies").EnumerateArray().ToArray();
        grants.Select(grant => grant.GetProperty("appId").GetString()).ShouldBe(["tenants-api", "timesheets"]);
        var operations = grants.Single(grant => grant.GetProperty("appId").GetString() == "tenants-api").GetProperty("operations").EnumerateArray().ToArray();
        operations.Select(operation => operation.GetProperty("name").GetString()).ShouldBe(["/api/v1/commands", "/api/v1/queries"]);
        operations.ShouldAllBe(operation => operation.GetProperty("httpVerb").EnumerateArray().Select(verb => verb.GetString()).SequenceEqual(new[] { "POST" }));
        var timesheets = grants.Single(grant => grant.GetProperty("appId").GetString() == "timesheets");
        timesheets.GetProperty("operations").EnumerateArray()
            .SelectMany(operation => operation.GetProperty("httpVerb").EnumerateArray().Select(verb => $"{verb.GetString()} {operation.GetProperty("name").GetString()}"))
            .Order(StringComparer.Ordinal).ShouldBe(new[]
            {
                "POST /api/v1/commands/*", "POST /api/v1/commands/workload", "POST /api/v1/queries",
                "GET /api/v1/commands/status/*", "GET /api/v1/commands/status/workload",
            }.Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("tenants", new[] { "eventstore POST /process", "eventstore POST /query", "eventstore POST /project", "eventstore POST /replay-state", "eventstore POST /admin/operational-index-metadata" })]
    [InlineData("tenants-api", new string[0])]
    [InlineData("memories", new string[0])]
    [InlineData("memories-access-telemetry-clock", new[] { "memories POST /v1/time/attest", "memories-access-telemetry POST /v1/time/attest" })]
    [InlineData("memories-access-telemetry", new[] { "memories POST /v1/access-telemetry/write", "memories POST /v1/access-telemetry/heartbeat", "memories POST /v1/access-telemetry/validate", "memories-access-telemetry-inspector GET /v1/access-telemetry/inspect", "access-telemetry-adapter POST /v1/access-telemetry/physical-reclamation-evidence" })]
    public void TenantsAndMemoriesReceiversHaveExactlyTheirDeclaredInvocationGrants(string appId, string[] expected)
    {
        var files = PlatformDaprBindings.Resolve(PlatformDeclarationSet.Load(Options())).Render(Path.Combine(Root, "DaprComponents", "reference"), 16379, 16380, "/tmp/local-secrets.json", null);
        using JsonDocument policy = JsonDocument.Parse(files["accesscontrol." + appId + ".yaml"]);
        JsonElement acl = policy.RootElement.GetProperty("spec").GetProperty("accessControl");
        acl.GetProperty("defaultAction").GetString().ShouldBe("deny");
        acl.GetProperty("trustDomain").GetString().ShouldBe("public");
        JsonElement[] grants = acl.GetProperty("policies").EnumerateArray().ToArray();
        grants.Select(grant => grant.GetProperty("appId").GetString()).Order(StringComparer.Ordinal)
            .ShouldBe(expected.Select(rule => rule.Split(' ')[0]).Distinct().Order(StringComparer.Ordinal));
        List<string> actual = [];
        foreach (JsonElement grant in grants)
        {
            grant.GetProperty("namespace").GetString().ShouldBe("default");
            grant.GetProperty("trustDomain").GetString().ShouldBe("public");
            grant.GetProperty("defaultAction").GetString().ShouldBe("deny");
            JsonElement[] operations = grant.GetProperty("operations").EnumerateArray().ToArray();
            operations.ShouldNotBeEmpty();
            foreach (JsonElement operation in operations)
            {
                operation.GetProperty("action").GetString().ShouldBe("allow");
                JsonElement[] verbs = operation.GetProperty("httpVerb").EnumerateArray().ToArray();
                verbs.ShouldNotBeEmpty();
                foreach (JsonElement verb in verbs)
                    actual.Add($"{grant.GetProperty("appId").GetString()} {verb.GetString()} {operation.GetProperty("name").GetString()}");
            }
        }
        actual.Order(StringComparer.Ordinal).ShouldBe(expected.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ActualOwnerOptionsShareResolvedNamesAndTelemetryStateIsIsolated()
    {
        var servers = PlatformAppModel.Prepare(Options());
        var gateway = servers.Single(server => server.AppId == "eventstore").Environment;
        gateway["EventStore__Publisher__PubSubName"].ShouldBe(PlatformDaprBindings.Name("pubsub", "events"));
        gateway["EventStore__ProjectionChanges__PubSubName"].ShouldBe(gateway["EventStore__Publisher__PubSubName"]);
        gateway["EventStore__Projections__ReadModelStateStoreName"].ShouldBe(gateway["EventStore__CommandStatus__StateStoreName"]);
        var telemetry = servers.Single(server => server.AppId == "memories-access-telemetry").Environment;
        telemetry["AccessTelemetryLifecycle__StateStoreName"].ShouldNotBe(gateway["EventStore__CommandStatus__StateStoreName"]);
        telemetry.ShouldNotContainKey("EventStoreIntegration__StateStore__StateStoreName");
        var bindings = PlatformDaprBindings.Resolve(PlatformDeclarationSet.Load(Options()));
        bindings.Consumes("statestore", "tenants-api").ShouldBeTrue();
        bindings.Consumes("statestore", "memories-access-telemetry").ShouldBeFalse();
        bindings.Consumes("access-telemetry-store", "memories-access-telemetry").ShouldBeTrue();
        bindings.Consumes("access-telemetry-store", "memories").ShouldBeFalse();
    }

    [Fact]
    public void ExplicitSubscriptionRendersDeliveryAndDeclaredDeadLetterWithoutPublisherSubscriptions()
    {
        var module = PlatformDeclarationSet.Load(Options(active: "memories")).Single(module => PlatformDeclarationSet.Id(module.Declaration) == "memories");
        var declaration = JsonNode.Parse(module.Declaration.GetRawText())!.AsObject();
        var topics = declaration["integration"]!["topics"]!.AsArray();
        topics[0]!["subscription"] = JsonNode.Parse("{\"server\":\"server\",\"route\":\"/events/ingest\"}");
        topics[0]!["deadLetter"] = JsonNode.Parse("{\"strategy\":\"topic\",\"topic\":\"failed-events\"}");
        topics.Add(JsonNode.Parse("{\"name\":\"failed-events\",\"role\":\"events\",\"deadLetter\":{\"strategy\":\"none\"},\"subscription\":{\"server\":\"server\",\"route\":\"/dead-letter\",\"topic\":\"failed.physical\"}}"));
        var bindings = PlatformDaprBindings.Resolve([module with { Declaration = JsonSerializer.SerializeToElement(declaration) }]);
        var files = bindings.Render(Path.Combine(Root, "DaprComponents", "reference"), 16379, 16380, "/tmp/local-secrets.json", null);
        var subscription = files.Single(file => file.Key.StartsWith("subscription.", StringComparison.Ordinal) && JsonDocument.Parse(file.Value).RootElement.GetProperty("spec").GetProperty("topic").GetString() == "memories-events");
        using JsonDocument json = JsonDocument.Parse(subscription.Value);
        json.RootElement.GetProperty("scopes")[0].GetString().ShouldBe("memories");
        var spec = json.RootElement.GetProperty("spec");
        spec.GetProperty("pubsubname").GetString().ShouldBe(PlatformDaprBindings.Name("pubsub", "events"));
        spec.GetProperty("routes").GetProperty("default").GetString().ShouldBe("/events/ingest");
        spec.GetProperty("deadLetterTopic").GetString().ShouldBe("failed.physical");
        using JsonDocument target = JsonDocument.Parse(files.Single(file => file.Key.StartsWith("subscription.", StringComparison.Ordinal) && JsonDocument.Parse(file.Value).RootElement.GetProperty("spec").GetProperty("topic").GetString() == "failed.physical").Value);
        target.RootElement.GetProperty("spec").GetProperty("topic").GetString().ShouldBe("failed.physical");
    }

    [Theory]
    [InlineData("scope", "runtime.dapr[0].servers")]
    [InlineData("receiver", "subscription.server")]
    [InlineData("route", "subscription.route")]
    [InlineData("dead-letter", "deadLetter.topic")]
    [InlineData("verb", "invocationRules[0].verbs")]
    [InlineData("key-conflict", "runtime.dapr[2].configurationKey")]
    [InlineData("key-case-conflict", "runtime.dapr[2].configurationKey")]
    [InlineData("recovery-conflict", "runtime.dapr[1].role")]
    [InlineData("physical-topic", "subscription.topic")]
    [InlineData("physical-self", "deadLetter.topic")]
    [InlineData("physical-wildcard-target", "deadLetter.topic")]
    [InlineData("physical-duplicate", "subscription")]
    [InlineData("physical-ambiguous", "deadLetter.topic")]
    public void InvalidDaprBindingsReturnSourceAndFieldWithoutDeclarations(string scenario, string field)
    {
        var document = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "references", "Hexalith.Memories", "module", "hexalith.module-manifest.v2.json")))!;
        var module = document["modules"]![0]!;
        if (scenario == "scope") module["runtime"]!["dapr"]![0]!["servers"] = JsonNode.Parse("[\"unknown\"]");
        else if (scenario == "verb") module["surfaces"]!["interfaces"]![1]!["invocationRules"]![0]!["verbs"] = JsonNode.Parse("[\"*\"]");
        else if (scenario == "key-conflict") module["runtime"]!["dapr"]![2]!["configurationKey"] = module["runtime"]!["dapr"]![0]!["configurationKey"]!.GetValue<string>();
        else if (scenario == "key-case-conflict") module["runtime"]!["dapr"]![2]!["configurationKey"] = module["runtime"]!["dapr"]![0]!["configurationKey"]!.GetValue<string>().ToUpperInvariant();
        else if (scenario == "recovery-conflict") module["runtime"]!["dapr"]![1]!["recoveryClass"] = "rebuild-only";
        else
        {
            var topic = module["integration"]!["topics"]![0]!;
            topic["subscription"] = scenario == "route" ? JsonNode.Parse("{\"server\":\"server\"}") : JsonNode.Parse("{\"server\":\"server\",\"route\":\"/events/ingest\"}");
            if (scenario == "receiver") topic["subscription"]!["server"] = "unknown";
            if (scenario == "dead-letter") topic["deadLetter"] = JsonNode.Parse("{\"strategy\":\"topic\",\"topic\":\"memories-events\"}");
            if (scenario == "physical-topic") topic["subscription"]!["topic"] = "";
            if (scenario is "physical-self" or "physical-wildcard-target" or "physical-duplicate" or "physical-ambiguous")
            {
                topic["subscription"]!["topic"] = "source.events";
                var target = JsonNode.Parse("{\"name\":\"failed-events\",\"role\":\"events\",\"deadLetter\":{\"strategy\":\"none\"},\"subscription\":{\"server\":\"server\",\"route\":\"/dead-letter\",\"topic\":\"source.events\"}}")!;
                if (scenario == "physical-wildcard-target") target["subscription"]!["topic"] = "*.failed";
                if (scenario == "physical-ambiguous") target["subscription"]!["topic"] = "failed.one";
                module["integration"]!["topics"]!.AsArray().Add(target);
                if (scenario == "physical-ambiguous")
                {
                    var alias = target.DeepClone();
                    alias["subscription"]!["topic"] = "failed.two";
                    module["integration"]!["topics"]!.AsArray().Add(alias);
                }
                if (scenario != "physical-duplicate") topic["deadLetter"] = JsonNode.Parse("{\"strategy\":\"topic\",\"topic\":\"failed-events\"}");
            }
        }
        string path = Path.Combine(Path.GetTempPath(), "platform-dapr-validation-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, document.ToJsonString());
            var result = PlatformManifestValidator.Validate([path], TestContext.Current.CancellationToken);
            result.IsValid.ShouldBeFalse();
            result.Declarations.ShouldBeNull();
            result.Diagnostics.ShouldContain(diagnostic => diagnostic.Source!.Contains(Path.GetFileName(path), StringComparison.Ordinal) && diagnostic.Field!.Contains(field, StringComparison.Ordinal));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ResolvedOptionKeysRejectCaseInsensitiveComponentConflicts()
    {
        var module = PlatformDeclarationSet.Load(Options(active: "memories")).Single(module => PlatformDeclarationSet.Id(module.Declaration) == "memories");
        var declaration = JsonNode.Parse(module.Declaration.GetRawText())!;
        declaration["runtime"]!["dapr"]![2]!["configurationKey"] = declaration["runtime"]!["dapr"]![0]!["configurationKey"]!.GetValue<string>().ToUpperInvariant();
        var failure = Should.Throw<InvalidDataException>(() => PlatformDaprBindings.Resolve([module with { Declaration = JsonSerializer.SerializeToElement(declaration) }]));
        failure.Message.ShouldContain(module.Source);
        failure.Message.ShouldContain("runtime.dapr.configurationKey");
        failure.Message.ShouldContain("conflicting binding");
    }

    [Theory]
    [InlineData("/api/v1/commands")]
    [InlineData("/api/v1/COMMANDS")]
    public void ReceiverMergesDuplicateAndCaseVariedPathsWithoutLosingVerbs(string path)
    {
        string source = Path.Combine(Path.GetTempPath(), "platform-acl-verbs-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var document = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "references", "Hexalith.EventStore", "module", "hexalith.module-manifest.v2.json")))!;
            var rules = document["modules"]![0]!["surfaces"]!["interfaces"]![0]!["invocationRules"]!.AsArray();
            rules.Add(JsonSerializer.SerializeToNode(new { caller = "tenants-api", path, verbs = new[] { "GET" } }));
            rules.Add(JsonSerializer.SerializeToNode(new { caller = "tenants-api", path, verbs = new[] { "POST", "GET" } }));
            File.WriteAllText(source, document.ToJsonString());
            var validated = PlatformManifestValidator.Validate([source], TestContext.Current.CancellationToken);
            validated.IsValid.ShouldBeTrue(string.Join(Environment.NewLine, validated.Diagnostics.Select(diagnostic => diagnostic.Message)));
            var files = PlatformDaprBindings.Resolve(validated.Declarations!).Render(Path.Combine(Root, "DaprComponents", "reference"), 16379, 16380, "/unused", null);
            using JsonDocument policy = JsonDocument.Parse(files["accesscontrol.eventstore.yaml"]);
            var caller = policy.RootElement.GetProperty("spec").GetProperty("accessControl").GetProperty("policies").EnumerateArray().Single(grant => grant.GetProperty("appId").GetString() == "tenants-api");
            var operations = caller.GetProperty("operations").EnumerateArray().ToArray();
            operations.Length.ShouldBe(2);
            var command = operations.Single(operation => operation.GetProperty("name").GetString() == "/api/v1/commands");
            command.GetProperty("httpVerb").EnumerateArray().Select(verb => verb.GetString()).ShouldBe(["GET", "POST"]);
            command.GetProperty("action").GetString().ShouldBe("allow");
        }
        finally { File.Delete(source); }
    }

    [Fact]
    public void ReceiverRendersDeclaredDescendantsBeforeParentFallbacks()
    {
        var module = PlatformDeclarationSet.Load(Options(active: "eventstore")).Single();
        var declaration = JsonNode.Parse(module.Declaration.GetRawText())!;
        declaration["surfaces"]!["interfaces"]![0]!["invocationRules"] = JsonSerializer.SerializeToNode(new[]
        {
            new { caller = "tenants-api", path = "/api/v1/commands", verbs = new[] { "POST" } },
            new { caller = "tenants-api", path = "/api/v1/commands/status", verbs = new[] { "GET" } },
            new { caller = "tenants-api", path = "/api/v1/commands/status/workload", verbs = new[] { "PUT" } },
        });
        var files = PlatformDaprBindings.Resolve([module with { Declaration = JsonSerializer.SerializeToElement(declaration) }])
            .Render(Path.Combine(Root, "DaprComponents", "reference"), 16379, 16380, "/unused", null);
        using JsonDocument policy = JsonDocument.Parse(files["accesscontrol.eventstore.yaml"]);
        var operations = policy.RootElement.GetProperty("spec").GetProperty("accessControl").GetProperty("policies")[0].GetProperty("operations").EnumerateArray().ToArray();
        operations.Select(operation => operation.GetProperty("name").GetString())
            .ShouldBe(["/api/v1/commands/status/workload", "/api/v1/commands/status/*", "/api/v1/commands/*"]);
        operations.Select(operation => operation.GetProperty("httpVerb")[0].GetString()).ShouldBe(["PUT", "GET", "POST"]);
    }

    [Fact]
    public void IndividuallyValidOwnersWithConflictingSharedRecoveryClassesRefuseBeforeResources()
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-recovery-conflict-" + Guid.NewGuid().ToString("N"));
        try
        {
            List<SourceMappingEntry> entries = [];
            string? conflictingSource = null;
            foreach (string owner in new[] { "EventStore", "Tenants", "Memories" })
            {
                string ownerRoot = Path.Combine(directory, "Hexalith." + owner);
                string manifest = Path.Combine(ownerRoot, "module", "hexalith.module-manifest.v2.json");
                Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
                var document = JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "references", "Hexalith." + owner, "module", "hexalith.module-manifest.v2.json")))!;
                if (owner == "Tenants")
                    foreach (var capability in document["modules"]![0]!["runtime"]!["dapr"]!.AsArray().Where(capability => capability!["role"]!.GetValue<string>() == "state"))
                        capability!["recoveryClass"] = "rebuild-only";
                File.WriteAllText(manifest, document.ToJsonString());
                var validated = PlatformManifestValidator.Validate([manifest], TestContext.Current.CancellationToken);
                validated.IsValid.ShouldBeTrue(string.Join(Environment.NewLine, validated.Diagnostics.Select(diagnostic => diagnostic.Message)));
                if (owner == "Tenants") conflictingSource = validated.Declarations!.Single().Source;
                entries.Add(new SourceMappingEntry("Hexalith." + owner, "source", ownerRoot));
            }
            IDistributedApplicationBuilder builder = Builder();
            InvalidDataException failure = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, Options() with
            {
                Mapping = new SourceMapping(WorkspaceMode.Source, Root, "Hexalith.Platform", entries, false),
            }));
            failure.Message.ShouldContain(conflictingSource!);
            failure.Message.ShouldContain("runtime.dapr");
            failure.Message.ShouldContain("conflicting recovery class for component 'statestore'");
            builder.Resources.ShouldBeEmpty();
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SubscriptionIdentitiesDistinguishConcatenationAndPhysicalAliases(bool splitTuple)
    {
        var original = PlatformDeclarationSet.Load(Options(active: "memories")).Single(module => PlatformDeclarationSet.Id(module.Declaration) == "memories");
        var declarations = new[] { (Owner: "a-b", Server: "c", App: "first-app", Topic: "first.events"), (Owner: splitTuple ? "a" : "a-b", Server: splitTuple ? "b-c" : "c", App: splitTuple ? "second-app" : "first-app", Topic: "second.events") }
            .GroupBy(item => (item.Owner, item.Server, item.App)).Select(group =>
            {
                var declaration = JsonNode.Parse(original.Declaration.GetRawText())!;
                declaration["identity"]!["moduleId"] = group.Key.Owner;
                declaration["identity"]!["servers"] = JsonSerializer.SerializeToNode(new[] { new { id = group.Key.Server, appId = group.Key.App, resourceId = group.Key.App, enabled = true, required = true } });
                declaration["runtime"]!["dapr"] = JsonSerializer.SerializeToNode(new[] { new { role = "events", capability = "pubsub", recoveryClass = "rebuild-only", configurationKey = "Dapr:Events", servers = new[] { group.Key.Server } } });
                declaration["surfaces"]!["interfaces"] = new JsonArray();
                declaration["integration"]!["topics"] = JsonSerializer.SerializeToNode(group.Select(item => new { name = "d", role = "events", deadLetter = new { strategy = "none" }, subscription = new { server = item.Server, route = "/events", topic = item.Topic } }));
                return original with { Declaration = JsonSerializer.SerializeToElement(declaration) };
            }).ToArray();
        var files = PlatformDaprBindings.Resolve(declarations).Render(Path.Combine(Root, "DaprComponents", "reference"), 16379, 16380, "/unused", null)
            .Where(file => file.Key.StartsWith("subscription.", StringComparison.Ordinal)).ToDictionary();
        files.Count.ShouldBe(2);
        files.Values.Select(content => JsonDocument.Parse(content).RootElement.GetProperty("metadata").GetProperty("name").GetString()).Distinct().Count().ShouldBe(2);
        var repeated = PlatformDaprBindings.Resolve(declarations.Reverse().ToArray()).Render(Path.Combine(Root, "DaprComponents", "reference"), 16379, 16380, "/unused", null);
        foreach (var file in files) repeated[file.Key].ShouldBe(file.Value);
    }

    [Theory]
    [InlineData("pubsub.yaml", "eventstore", "", "expected one YAML mapping document")]
    [InlineData("pubsub.yaml", "eventstore", "apiVersion: dapr.io/v1alpha1\nkind: Component\nspec:\n  type: pubsub.redis\n  version: v1\n", "metadata must be a mapping")]
    [InlineData("secretstore.yaml", "memories", "apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: secretstore\nspec:\n  type: secretstores.local.file\n  version: v1\n  metadata: []\n", "spec.metadata must contain a secretsFile entry")]
    [InlineData("secretstore.yaml", "memories", "apiVersion: dapr.io/v1alpha1\nkind: Component\nmetadata:\n  name: secretstore\nspec:\n  type: secretstores.local.file\n  version: v1\n  metadata:\n    - name: nestedSeparator\n      value: ':'\n", "spec.metadata must contain a secretsFile entry")]
    public void InvalidComponentTemplatesRefuseWithScopedDiagnosticsBeforeResources(string template, string active, string content, string reason)
    {
        string directory = Path.Combine(Path.GetTempPath(), "platform-invalid-template-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (string source in Directory.GetFiles(Path.Combine(Root, "DaprComponents", "reference"), "*.yaml")) File.Copy(source, Path.Combine(directory, Path.GetFileName(source)));
            string sourcePath = Path.Combine(directory, template);
            File.WriteAllText(sourcePath, content);
            IDistributedApplicationBuilder builder = Builder();
            InvalidDataException failure = Should.Throw<InvalidDataException>(() => PlatformAppModel.Compose(builder, Options(active: active) with { ComponentsDirectory = directory }));
            failure.Message.ShouldContain(sourcePath);
            failure.Message.ShouldContain("runtime.dapr");
            failure.Message.ShouldContain(reason);
            builder.Resources.ShouldBeEmpty();
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void UnsafeCertificateDirectoryRefusesBeforeAddingResources()
    {
        IDistributedApplicationBuilder builder = Builder();
        string certificates = Path.Combine(Root, "unsafe-certificates-" + Guid.NewGuid().ToString("N"));
        builder.Configuration["Dapr:Mtls:CertificateDirectory"] = certificates;

        InvalidOperationException failure = Should.Throw<InvalidOperationException>(() => PlatformAppModel.Compose(builder, Options(active: "eventstore")));

        failure.Message.ShouldContain("Dapr:Mtls:CertificateDirectory");
        failure.Message.ShouldContain(certificates);
        failure.Message.ShouldContain("outside the repository");
        builder.Resources.ShouldBeEmpty();
        Directory.Exists(certificates).ShouldBeFalse();
    }

    [Fact]
    public void ExternalAppHostDirectoryUsesResolvedPlatformCertificateBoundary()
    {
        string directory = Path.Combine(Path.GetTempPath(), "external-builds-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = [], ProjectDirectory = directory, AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true });
        builder.Configuration["Dapr:Mtls:CertificateDirectory"] = Path.Combine(directory, "certificates");
        try
        {
            string link = Path.Combine(directory, "inside-repository");
            Directory.CreateSymbolicLink(link, Root);
            PlatformCompositionOptions options = Options(active: "eventstore") with { PlatformRoot = link };
            PlatformAppModel.Compose(builder, options);
            builder.Resources.ShouldContain(resource => resource.Name == "dapr-sentry");
            IDistributedApplicationBuilder refused = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { Args = [], ProjectDirectory = directory, AssemblyName = typeof(PlatformAppModel).Assembly.GetName().Name, DisableDashboard = true });
            refused.Configuration["Dapr:Mtls:CertificateDirectory"] = Path.Combine(link, "certificates");
            InvalidOperationException failure = Should.Throw<InvalidOperationException>(() => PlatformAppModel.Compose(refused, options));
            failure.Message.ShouldContain("Dapr:Mtls:CertificateDirectory");
            failure.Message.ShouldContain(Path.Combine(Root, "certificates"));
            failure.Message.ShouldContain("outside the repository");
            refused.Resources.ShouldBeEmpty();
        }
        finally
        {
            foreach (var resource in builder.Resources.SelectMany(resource => resource.Annotations.OfType<DaprSidecarAnnotation>()))
            {
                string? config = resource.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.Config;
                if (config is not null) Directory.Delete(Path.GetDirectoryName(config)!, true);
            }
            Directory.Delete(directory, true);
        }
    }
}
