#if HEXALITH_PLATFORM_CANONICAL_SCOPES
namespace Hexalith.Platform.Composition.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.Tenants.Contracts.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Shouldly;

/// <summary>Checks the AppHost operator relay against a fake gateway: the I/O matrix of the synthetic-tenant task.</summary>
public sealed class PlatformOperatorTaskRelayTests
{
    private const string SigningKey = "relay-test-signing-key-0123456789abcdef0123456789";
    private const string SidecarToken = "relay-test-sidecar-api-token";
    private static readonly string Root = new('c', 64);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private static readonly PlatformOperatorTaskBinding Binding = new(string.Empty, Root,
        new("global-administrators", BootstrapGlobalAdmin.CommandType, "platform:global-administrators", "global-administrators:bootstrap", "global-administrators:read"),
        new("tenants", CreateSyntheticTenant.CommandType, "platform:tenants", "tenants:create", "tenants:read"));

    [Fact]
    public async Task FreshRunBootstrapsTheOperatorThenRelaysTheExactTaskCallsAsync()
    {
        await using OperatorRelayUpstream upstream = OperatorRelayUpstream.Start();
        string workspace = Workspace();
        using RSA proofKey = RSA.Create(2048);
        try
        {
            (PlatformOperatorTaskRelay relay, CompositionRunPlan plan, CompositionStartupTask task) = await PrepareAsync(workspace, upstream, proofKey);
            await using (relay)
            {
                // The private invocation holds only the per-run relay credential.
                string invocation = await File.ReadAllTextAsync(task.InvocationPath!, TestContext.Current.CancellationToken);
                if (!OperatingSystem.IsWindows())
                    File.GetUnixFileMode(task.InvocationPath!).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
                using JsonDocument document = JsonDocument.Parse(invocation);
                JsonElement root = document.RootElement;
                root.EnumerateObject().Select(property => property.Name).ShouldBe(["syntheticTenantId", "gatewayUrl", "commandTenant", "headers"]);
                root.GetProperty("syntheticTenantId").GetString().ShouldBe(plan.TenantNamespace);
                root.GetProperty("gatewayUrl").GetString().ShouldBe(relay.Endpoint!.GetLeftPart(UriPartial.Authority));
                new Uri(root.GetProperty("gatewayUrl").GetString()!).IsLoopback.ShouldBeTrue();
                root.GetProperty("commandTenant").GetString().ShouldBe("platform:tenants");
                JsonProperty header = root.GetProperty("headers").EnumerateObject().ShouldHaveSingleItem();
                header.Name.ShouldBe("Authorization");
                string credential = header.Value.GetString()!;
                credential.ShouldStartWith("Bearer ");
                foreach (string secret in new[] { SigningKey, SidecarToken, Convert.ToBase64String(proofKey.ExportPkcs8PrivateKey()) })
                    invocation.ShouldNotContain(secret);

                using HttpClient client = TaskClient(relay, credential);
                string messageId = "01JBRELAYTESTMESSAGE00001A";
                using HttpResponseMessage accepted = await client.PostAsync("api/v1/commands", TaskCommand(messageId, plan.TenantNamespace), TestContext.Current.CancellationToken);
                accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted, await accepted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                using JsonDocument acceptedBody = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                acceptedBody.RootElement.GetProperty("messageId").GetString().ShouldBe(messageId);
                using HttpResponseMessage status = await client.GetAsync("api/v1/commands/status/" + messageId, TestContext.Current.CancellationToken);
                status.StatusCode.ShouldBe(HttpStatusCode.OK);
                (await status.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("\"statusCode\":4");

                OperatorRelayUpstreamRequest[] sent = [.. upstream.Requests];
                sent.Select(request => request.Method + " " + (request.Path.StartsWith(OperatorRelayUpstream.StatusPrefix, StringComparison.Ordinal) ? "status" : request.Path))
                    .ShouldBe(["POST " + OperatorRelayUpstream.BoundPath, "GET status", "POST " + OperatorRelayUpstream.BoundPath, "GET status"]);
                sent.ShouldAllBe(request => request.Headers["authorization"] != credential);

                // Bootstrap: the bound operator principal with only the bootstrap permission, through the Tenants sidecar.
                OperatorRelayUpstreamRequest bootstrap = sent[0];
                BoundWorkloadCommandRequest bootstrapCommand = JsonSerializer.Deserialize<BoundWorkloadCommandRequest>(bootstrap.Body, Web)!;
                bootstrapCommand.PrincipalId.ShouldBe("g4-operator-" + plan.RunId);
                bootstrapCommand.Command.CommandType.ShouldBe(BootstrapGlobalAdmin.CommandType);
                bootstrapCommand.Command.Tenant.ShouldBe("platform:global-administrators");
                bootstrap.Headers["x-hexalith-scope"].ShouldBe("platform:global-administrators");
                bootstrap.Headers["dapr-api-token"].ShouldBe(SidecarToken);
                bootstrap.Headers.ShouldContainKey(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName.ToLowerInvariant());
                BoundWorkloadCommandProof.Verify(bootstrapCommand, bootstrap.Headers[BoundWorkloadCommandProof.HeaderName.ToLowerInvariant()], "tenants",
                    Convert.ToBase64String(proofKey.ExportSubjectPublicKeyInfo()), DateTimeOffset.UtcNow).ShouldBeTrue();
                AssertGrants(bootstrap, ["global-administrators:bootstrap"], false, "g4-operator-" + plan.RunId);
                bootstrap.Headers[RouteCatalogInvocationHandler.RootHeader.ToLowerInvariant()].ShouldBe(Root);
                AssertGrants(sent[1], ["global-administrators:read"], false, "g4-operator-" + plan.RunId);
                sent[1].Headers["x-hexalith-scope"].ShouldBe("platform:global-administrators");

                // Creation: the task's exact command, the same principal, one permission plus the verified administrator role.
                OperatorRelayUpstreamRequest create = sent[2];
                BoundWorkloadCommandRequest createCommand = JsonSerializer.Deserialize<BoundWorkloadCommandRequest>(create.Body, Web)!;
                createCommand.PrincipalId.ShouldBe(bootstrapCommand.PrincipalId);
                createCommand.Command.MessageId.ShouldBe(messageId);
                createCommand.Command.Tenant.ShouldBe("platform:tenants");
                createCommand.Command.Domain.ShouldBe("tenants");
                createCommand.Command.AggregateId.ShouldBe(plan.TenantNamespace);
                createCommand.Command.CommandType.ShouldBe(CreateSyntheticTenant.CommandType);
                createCommand.Command.Payload.GetProperty("tenantId").GetString().ShouldBe(plan.TenantNamespace);
                createCommand.Command.CorrelationId.ShouldBeNull();
                createCommand.Command.Extensions.ShouldBeNull();
                create.Headers["x-hexalith-scope"].ShouldBe("platform:tenants");
                AssertGrants(create, ["tenants:create"], true, "g4-operator-" + plan.RunId);
                AssertGrants(sent[3], ["tenants:read"], false, "g4-operator-" + plan.RunId);
                sent[3].Headers["x-hexalith-scope"].ShouldBe("platform:tenants");
            }

            File.Exists(CompositionStartupTasks.InvocationPathFor(workspace, "tenants", "create-synthetic-tenant")).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task BadRelayRequestsAreRefusedAndNothingIsForwardedAsync()
    {
        await using OperatorRelayUpstream upstream = OperatorRelayUpstream.Start();
        string workspace = Workspace();
        using RSA proofKey = RSA.Create(2048);
        try
        {
            (PlatformOperatorTaskRelay relay, CompositionRunPlan plan, CompositionStartupTask task) = await PrepareAsync(workspace, upstream, proofKey);
            await using (relay)
            {
                string credential = Credential(task);
                using HttpClient anonymous = TaskClient(relay, null);
                using HttpClient wrong = TaskClient(relay, "Bearer " + new string('0', 64));
                using HttpClient owner = TaskClient(relay, credential);
                (await Status(anonymous.PostAsync("api/v1/commands", TaskCommand("01JBRELAYTESTMESSAGE00002A", plan.TenantNamespace), TestContext.Current.CancellationToken))).ShouldBe(HttpStatusCode.Unauthorized);
                (await Status(wrong.PostAsync("api/v1/commands", TaskCommand("01JBRELAYTESTMESSAGE00002A", plan.TenantNamespace), TestContext.Current.CancellationToken))).ShouldBe(HttpStatusCode.Unauthorized);
                (await Status(wrong.GetAsync("api/v1/commands/status/01JBRELAYTESTMESSAGE00002A", TestContext.Current.CancellationToken))).ShouldBe(HttpStatusCode.Unauthorized);
                (await Status(owner.GetAsync("api/v1/queries", TestContext.Current.CancellationToken))).ShouldBe(HttpStatusCode.NotFound);
                (await Status(owner.GetAsync("api/v1/commands", TestContext.Current.CancellationToken))).ShouldBe(HttpStatusCode.NotFound);
                (await Status(owner.PostAsync("api/v1/commands/bound/workload", TaskCommand("01JBRELAYTESTMESSAGE00002A", plan.TenantNamespace), TestContext.Current.CancellationToken))).ShouldBe(HttpStatusCode.NotFound);
                (await Status(owner.GetAsync("api/v1/commands/status/01JBRELAYTESTMESSAGE00002A", TestContext.Current.CancellationToken))).ShouldBe(HttpStatusCode.NotFound);
                (await Status(owner.PostAsync("api/v1/commands", TaskCommand("01JBRELAYTESTMESSAGE00002A", "another-tenant"), TestContext.Current.CancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
                (await Status(owner.PostAsync("api/v1/commands", new StringContent(TaskCommandJson("01JBRELAYTESTMESSAGE00002A", plan.TenantNamespace)
                    .Replace(CreateSyntheticTenant.CommandType, "create-tenant", StringComparison.Ordinal), Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken))).ShouldBe(HttpStatusCode.Forbidden);
                (await Status(owner.PostAsync("api/v1/commands", new StringContent("not json", Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken))).ShouldBe(HttpStatusCode.BadRequest);
                upstream.Requests.ShouldBeEmpty();
            }
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Theory]
    [InlineData(403)]
    [InlineData(409)]
    [InlineData(422)]
    public async Task GatewayRefusalOrRejectionIsReturnedUnchangedAsync(int refusal)
    {
        await using OperatorRelayUpstream upstream = OperatorRelayUpstream.Start();
        string body = "{\"type\":\"https://hexalith.test/refusal\",\"status\":" + refusal + ",\"rejectionType\":\"Hexalith.Tenants.Contracts.Events.Rejections.TenantAlreadyExistsRejection\"}";
        upstream.Respond = request => request.Path == OperatorRelayUpstream.BoundPath && OperatorRelayUpstream.CommandType(request) == CreateSyntheticTenant.CommandType
            ? (refusal, body) : OperatorRelayUpstream.Default(request);
        string workspace = Workspace();
        using RSA proofKey = RSA.Create(2048);
        try
        {
            (PlatformOperatorTaskRelay relay, CompositionRunPlan plan, CompositionStartupTask task) = await PrepareAsync(workspace, upstream, proofKey);
            await using (relay)
            {
                using HttpClient owner = TaskClient(relay, Credential(task));
                using HttpResponseMessage response = await owner.PostAsync("api/v1/commands", TaskCommand("01JBRELAYTESTMESSAGE00003A", plan.TenantNamespace), TestContext.Current.CancellationToken);
                ((int)response.StatusCode).ShouldBe(refusal);
                (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(body);
                response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
                (await Status(owner.GetAsync("api/v1/commands/status/01JBRELAYTESTMESSAGE00003A", TestContext.Current.CancellationToken))).ShouldBe(HttpStatusCode.NotFound);
            }
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Theory]
    [InlineData("refused")]
    [InlineData("already-bootstrapped")]
    public async Task BootstrapOutcomeGatesTheCreationAsync(string outcome)
    {
        await using OperatorRelayUpstream upstream = OperatorRelayUpstream.Start();
        upstream.Respond = request =>
        {
            if (request.Path == OperatorRelayUpstream.BoundPath && OperatorRelayUpstream.CommandType(request) == BootstrapGlobalAdmin.CommandType && outcome == "refused")
                return (403, "{\"status\":403}");
            if (request.Method == "GET" && request.Headers["x-hexalith-scope"] == "platform:global-administrators")
            {
                string id = request.Path[OperatorRelayUpstream.StatusPrefix.Length..];
                return (200, JsonSerializer.Serialize(new { messageId = id, correlationId = id, statusCode = 5, status = "Rejected",
                    rejectionEventType = "Hexalith.Tenants.Contracts.Events.Rejections.GlobalAdminAlreadyBootstrappedRejection, Hexalith.Tenants.Contracts" }));
            }

            return OperatorRelayUpstream.Default(request);
        };
        string workspace = Workspace();
        using RSA proofKey = RSA.Create(2048);
        try
        {
            (PlatformOperatorTaskRelay relay, CompositionRunPlan plan, CompositionStartupTask task) = await PrepareAsync(workspace, upstream, proofKey);
            await using (relay)
            {
                using HttpClient owner = TaskClient(relay, Credential(task));
                using HttpResponseMessage response = await owner.PostAsync("api/v1/commands", TaskCommand("01JBRELAYTESTMESSAGE00004A", plan.TenantNamespace), TestContext.Current.CancellationToken);
                if (outcome == "refused")
                {
                    response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
                    upstream.Requests.ShouldHaveSingleItem();
                }
                else
                {
                    // A run whose native preparation already bootstrapped this operator still creates the synthetic tenant.
                    response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
                    upstream.Requests.Count(request => request.Path == OperatorRelayUpstream.BoundPath).ShouldBe(2);
                }
            }
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task UnavailableOperatorTokenFailsTheCallWithoutForwardingAsync()
    {
        await using OperatorRelayUpstream upstream = OperatorRelayUpstream.Start();
        string workspace = Workspace();
        using RSA proofKey = RSA.Create(2048);
        try
        {
            (PlatformOperatorTaskRelay relay, CompositionRunPlan plan, CompositionStartupTask task) = await PrepareAsync(workspace, upstream, proofKey, new UnavailableOperatorCredentials());
            await using (relay)
            {
                using HttpClient owner = TaskClient(relay, Credential(task));
                (await Status(owner.PostAsync("api/v1/commands", TaskCommand("01JBRELAYTESTMESSAGE00005A", plan.TenantNamespace), TestContext.Current.CancellationToken))).ShouldBe(HttpStatusCode.BadGateway);
                upstream.Requests.ShouldBeEmpty();
            }
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task InvocationOutsideThePrivateTaskDirectoryIsRefusedAsync()
    {
        await using OperatorRelayUpstream upstream = OperatorRelayUpstream.Start();
        string workspace = Workspace();
        using RSA proofKey = RSA.Create(2048);
        try
        {
            CompositionRunPlan plan = Plan(workspace);
            var task = new CompositionStartupTask("tenants", "create-synthetic-tenant", "/unused", [])
            {
                Scope = CompositionStartupTasks.EnvironmentCreationScope,
                InvocationPath = Path.Combine(workspace, "invocation.json"),
            };
            await using var relay = new PlatformOperatorTaskRelay(plan, task, Binding with { CatalogDirectory = workspace }, Access(proofKey),
                Credentials(plan), _ => new PlatformOperatorUpstream(upstream.Endpoint, SidecarToken, upstream.Endpoint, "eventstore"));
            await Should.ThrowAsync<InvalidDataException>(() => relay.PrepareAsync(Services(workspace), TestContext.Current.CancellationToken));
            File.Exists(task.InvocationPath).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public void OnlyTheScheduledTenantsEnvironmentTaskSelectsTheRelay()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "relay-select");
        CompositionRunPlan plan = Plan(workspace);
        var scheduled = new CompositionStartupTask("tenants", "create-synthetic-tenant", "/unused", [])
        {
            Scope = CompositionStartupTasks.EnvironmentCreationScope,
            InvocationPath = CompositionStartupTasks.InvocationPathFor(workspace, "tenants", "create-synthetic-tenant"),
        };

        PlatformOperatorTaskRelay.SelectTask(null).ShouldBeNull();
        PlatformOperatorTaskRelay.SelectTask(plan).ShouldBeNull();
        PlatformOperatorTaskRelay.SelectTask(plan with { StartupTasks = [scheduled with { InvocationPath = null }] }).ShouldBeNull();
        PlatformOperatorTaskRelay.SelectTask(plan with { StartupTasks = [scheduled with { ModuleId = "parties" }] }).ShouldBeNull();
        PlatformOperatorTaskRelay.SelectTask(plan with { StartupTasks = [scheduled] }).ShouldBe(scheduled);
        Should.Throw<InvalidDataException>(() => PlatformOperatorTaskRelay.SelectTask(plan with { StartupTasks = [scheduled, scheduled] }));
    }

    private static async Task<(PlatformOperatorTaskRelay Relay, CompositionRunPlan Plan, CompositionStartupTask Task)> PrepareAsync(
        string workspace, OperatorRelayUpstream upstream, RSA proofKey, IPlatformOperatorCredentials? credentials = null)
    {
        CompositionRunPlan plan = Plan(workspace);
        var task = new CompositionStartupTask("tenants", "create-synthetic-tenant", "/unused", [])
        {
            Scope = CompositionStartupTasks.EnvironmentCreationScope,
            InvocationPath = CompositionStartupTasks.InvocationPathFor(workspace, "tenants", "create-synthetic-tenant"),
        };
        string catalog = Path.Combine(workspace, "catalog");
        Directory.CreateDirectory(catalog);
        await File.WriteAllBytesAsync(Path.Combine(catalog, "eventstore.committed.json"), JsonSerializer.SerializeToUtf8Bytes(
            new RouteCatalogGenerationReadback(3, 1, Root, new string('d', 64), new string('e', 64), "Development", [], true) { ActivationBindingDigest = new string('f', 64) }),
            TestContext.Current.CancellationToken);
        var relay = new PlatformOperatorTaskRelay(plan, task, Binding with { CatalogDirectory = catalog }, Access(proofKey),
            credentials ?? Credentials(plan), _ => new PlatformOperatorUpstream(upstream.Endpoint, SidecarToken, upstream.Endpoint, "eventstore"));
        await relay.PrepareAsync(Services(workspace), TestContext.Current.CancellationToken);
        return (relay, plan, task);
    }

    private static CompositionRunPlan Plan(string workspace) =>
        CompositionRunPlanFactory.Create(CompositionRunPlanFactory.NewRunId(), workspace, workspace, CompositionPortAllocator.Allocate(1), [], [],
            new SourceMapping(WorkspaceMode.Source, workspace, "Hexalith.Platform", [], false), [Path.Combine(workspace, "module.json")], "tenants");

    private static PlatformTenantBootstrapAccess Access(RSA proofKey) =>
        new("tenants", "tenants", Convert.ToBase64String(proofKey.ExportPkcs8PrivateKey()), SidecarToken, CompositionEnvironment.TokenIssuer);

    private static PlatformRunnerOperatorCredentials Credentials(CompositionRunPlan plan) =>
        new(SigningKey, "g4-operator-" + plan.RunId, CompositionEnvironment.TokenIssuer, "tenants");

    private static ServiceProvider Services(string workspace) => new ServiceCollection()
        .AddSingleton<IHostEnvironment>(new HostingEnvironment { EnvironmentName = Environments.Development, ApplicationName = "relay-tests", ContentRootPath = workspace })
        .BuildServiceProvider();

    private static string Workspace()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "operator-relay-" + Guid.NewGuid().ToString("N"));
        CompositionWorkspace.CreatePrivateDirectory(workspace);
        return workspace;
    }

    private static string Credential(CompositionStartupTask task)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(task.InvocationPath!));
        return document.RootElement.GetProperty("headers").GetProperty("Authorization").GetString()!;
    }

    private static HttpClient TaskClient(PlatformOperatorTaskRelay relay, string? authorization)
    {
        var client = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) { BaseAddress = relay.Endpoint, Timeout = TimeSpan.FromSeconds(30) };
        if (authorization is not null) client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);
        return client;
    }

    private static string TaskCommandJson(string messageId, string tenant) => JsonSerializer.Serialize(new
    {
        messageId, tenant = "platform:tenants", domain = "tenants", aggregateId = tenant, commandType = CreateSyntheticTenant.CommandType,
        payload = new { tenantId = tenant },
    });

    private static StringContent TaskCommand(string messageId, string tenant) => new(TaskCommandJson(messageId, tenant), Encoding.UTF8, "application/json");

    private static async Task<HttpStatusCode> Status(Task<HttpResponseMessage> call)
    {
        using HttpResponseMessage response = await call;
        return response.StatusCode;
    }

    private static void AssertGrants(OperatorRelayUpstreamRequest request, string[] permissions, bool globalAdministrator, string subject)
    {
        (string[] actualPermissions, bool actualAdministrator, string actualSubject) = Grants(request);
        actualPermissions.ShouldBe(permissions);
        actualAdministrator.ShouldBe(globalAdministrator);
        actualSubject.ShouldBe(subject);
    }

    private static (string[] Permissions, bool GlobalAdministrator, string Subject) Grants(OperatorRelayUpstreamRequest request)
    {
        string token = request.Headers["authorization"]["Bearer ".Length..];
        string payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        using JsonDocument claims = JsonDocument.Parse(Convert.FromBase64String(payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=')));
        JsonElement root = claims.RootElement;
        string[] permissions = root.TryGetProperty("eventstore:permission", out JsonElement values)
            ? values.ValueKind == JsonValueKind.Array ? [.. values.EnumerateArray().Select(value => value.GetString()!)] : [values.GetString()!] : [];
        root.TryGetProperty("eventstore:tenant", out _).ShouldBeFalse();
        root.TryGetProperty("eventstore:domain", out _).ShouldBeFalse();
        return (permissions, root.TryGetProperty("role", out JsonElement role) && role.GetString() == "GlobalAdministrator", root.GetProperty("sub").GetString()!);
    }
}
#endif
