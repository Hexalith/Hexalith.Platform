using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual role HTTP bearer and host-lifetime boundary; no installed role, cluster or Live execution.</summary>
public sealed class ProtectionQualificationHttpBoundaryTests
{
    [Fact]
    public async Task WrongHttpBearerCannotConsumeAttemptThenCurrentBearerEnrollsAndIdleHostCompletesOwnHolders()
    {
        var token = TestContext.Current.CancellationToken;
        using var fixture = new ErasureRootEngineFixture(); using var native = Substitute.For<DaprClient>(); using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        native.GetStateAsync<JsonElement>("installation", "proof", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.SerializeToElement(new { agentsRuntimeEnabled = false, storeIdentity = "erc-test", deploymentIdentity = "component-http-server" }));
        var arguments = ProtectionQualificationSourceCatalog.RequiredArguments().ToDictionary(pair => pair.Key,
            pair => pair.Value.StartsWith("Guard:", StringComparison.Ordinal) ? new JsonObject { ["transition"] = new JsonObject { ["Operation"] = (int)Enum.Parse<GovernanceGuardOperation>(pair.Value[6..]) } } : new JsonObject(), StringComparer.Ordinal);
        var recipes = ProtectionQualificationSourceCatalog.Compile(arguments);
        var scope = new ProtectionQualificationServiceScope("enrollment", "component-enrollment", "tenant-a", new string('a', 64), new("installation", "proof", new()), recipes,
            recipes.Keys.ToDictionary(id => id, id => new ProtectionQualificationObservationRecipe("tenant-a", new(), new()) { CaseId = id }));
        var service = new ProtectionQualificationRoleService(scope, new(fixture.Custody, fixture.Permissions), null, null, native, signer);
        string variable = "ERC_ROLE_HTTP_TEST_" + Guid.NewGuid().ToString("N").ToUpperInvariant(); Environment.SetEnvironmentVariable(variable, "current-component-bearer");
        using var worker = new ErasureRootHolderCompletionWorker(fixture.Keys, fixture.Custody);
        var owningServices = new ServiceCollection(); owningServices.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(worker);
        await using var owningProvider = owningServices.BuildServiceProvider();
        await using var app = ProtectionQualificationServerExecutable.BuildApplication(owningProvider, service, "env:" + variable);
        app.Urls.Add("http://127.0.0.1:0");
        try
        {
            await app.StartAsync(token);
            string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var http = new HttpClient { BaseAddress = new(address) };
            async Task<HttpResponseMessage> Send(string bearer)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/platform-protection/v1/role");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
                request.Headers.Add("X-Hexalith-Protection-Role-Contract", "ext-protection-1-role-program/v1");
                request.Content = new StringContent(JsonSerializer.Serialize(new { operationId = "one-attempt", action = "EnrollSingleAttempt", bindingDigest = scope.BindingDigest, attemptId = "attempt" }), Encoding.UTF8, "application/json");
                return await http.SendAsync(request, token);
            }
            string before = ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a"));
            using (var denied = await Send("wrong-bearer")) { denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized); }
            ErasureRootCustody.Hash(await fixture.Store.HeadAsync("tenant-a")).ShouldBe(before);
            using (var accepted = await Send("current-component-bearer"))
            { accepted.StatusCode.ShouldBe(HttpStatusCode.OK); JsonNode.Parse(await accepted.Content.ReadAsStringAsync(token))!["statement"]!["response"]!["status"]!.GetValue<string>().ShouldBe("Enrolled"); }
            var root = (await fixture.Custody.WrapAsync(new("tenant-a", "idle-owner", Hexalith.Platform.Custody.PlatformKeyPurpose.InteractionRootDek, "idle-alias", "root-v1", "lifecycle", "erc-test", "custody-1"), "wrap-idle", 0, token))!;
            using var holder = await fixture.Custody.UnwrapAsync(root, "idle-host-holder", token); holder.ShouldNotBeNull();
            fixture.Permissions.Allowed = false; holder.Dispose(); fixture.Custody.RetainDisposedHolder(root, holder);
            (await fixture.Custody.CompleteDisposedHoldersAsync(token)).ShouldBe(0);
            (await fixture.Store.HeadAsync("tenant-a")).Targets.Values.Single().Holders.ShouldNotBeEmpty();
            fixture.Permissions.Allowed = true;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(10));
            while ((await fixture.Store.HeadAsync("tenant-a")).Targets.Values.Single().Holders.Length != 0) { await Task.Delay(50, deadline.Token); }
            var target = new ProtectionTarget("tenant-a", root.Identity.ObjectId, root.Identity.KeyAlias);
            var request = new DeletionBatchConsumptionRequest(new("issuer", "audience", "tenant-a", "request", "seal", "AcceptedSet", 0, "idle-batch", DeletionBatchCapabilityIdentity.TargetManifestDigest([target]), "guard", 1, 1, 1, "key"), "component-signature", 1, "dispatch", 2, [target]);
            var secondOwner = new ErasureRootCustody(fixture.Store, fixture.Custody.Installation, fixture.Permissions);
            (await secondOwner.ConsumeAsync(request, "original-reservation", token)).State.ShouldBe(DeletionManifestProviderState.Consumed);
        }
        finally { await app.StopAsync(CancellationToken.None); Environment.SetEnvironmentVariable(variable, null); }
    }
}
