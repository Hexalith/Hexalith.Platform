using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Inventory;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.Platform.Secrets.Authority.Tests;

public sealed class AuthorityHttpTests
{
    [Fact]
    public async Task UnchangedAdaptersReadProfileAndExactRevocationOverAuthenticatedLoopbackHttp()
    {
        await using var server = await AuthorityHttpFixture.Start();
        var invoker = new AuthenticatedSecretsInvoker(server.Client, new WorkloadToken(server.Token()), () => "offline-sidecar-channel");
        var profileReader = new DaprHmacAuthorityAdapter(server.State.Runtime, invoker, server.State.Clock);
        profileReader.GetCurrentProfile(AuthorityTestState.AppId).ShouldBe(server.State.Runtime.ExpectedProfile);
        var trust = new DaprHmacCurrentTrust(server.State.Runtime, invoker, server.State.Clock);
        CurrentHmacTrust first = (await trust.ReadAsync(new PlatformHmacScope("tenant-a", PlatformHmacPurpose.TrustedEnvelope), "opaque-version-v1", TestContext.Current.CancellationToken))!;
        first.ShouldNotBeNull();
        CurrentHmacTrust second = (await trust.ReadAsync(new PlatformHmacScope("tenant-a", PlatformHmacPurpose.TrustedEnvelope), "opaque-version-v1", TestContext.Current.CancellationToken))!;
        first.Revision.ShouldBe(second.Revision);
        (await trust.ReadAsync(new PlatformHmacScope("tenant-b", PlatformHmacPurpose.TrustedEnvelope), "opaque-version-v1", TestContext.Current.CancellationToken)).ShouldBeNull();
        await server.State.Apply("SetKeyStatus", key: server.State.Key with { Revoked = true });
        (await trust.ReadAsync(new PlatformHmacScope("tenant-a", PlatformHmacPurpose.TrustedEnvelope), "opaque-version-v1", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Theory]
    [InlineData("missing-bearer", 401)]
    [InlineData("missing-channel", 401)]
    [InlineData("foreign-caller", 503)]
    [InlineData("missing-operation", 403)]
    [InlineData("wrong-operation", 403)]
    [InlineData("wrong-audience", 401)]
    [InlineData("audience-trailing-slash", 401)]
    [InlineData("wrong-issuer", 401)]
    [InlineData("expired", 401)]
    [InlineData("wrong-signature", 401)]
    [InlineData("admin-token", 401)]
    [InlineData("allowed", 200)]
    public async Task QueryRequiresValidJwtExactOperationCurrentEnrollmentAndAppChannel(string scenario, int expected)
    {
        await using var server = await AuthorityHttpFixture.Start();
        string? token = scenario switch
        {
            "missing-bearer" => null, "foreign-caller" => server.Token("foreign"), "missing-operation" => server.Token(operation: null),
            "wrong-operation" => server.Token(operation: "registry:query"), "wrong-audience" => server.Token(audience: "registry"),
            "audience-trailing-slash" => server.Token(audience: "ext-secrets-1-hmac-authority/"),
            "wrong-issuer" => server.Token(issuer: "https://foreign.example"), "expired" => server.Token(expired: true),
            "wrong-signature" => server.Token(wrongSignature: true), "admin-token" => server.AdministratorToken, _ => server.Token(),
        };
        using var response = await Send(server, "query-profile", new { profileReference = AuthorityTestState.AppId }, token,
            scenario == "missing-channel" ? null : "offline-receiver-channel");
        ((int)response.StatusCode).ShouldBe(expected);
    }

    [Theory]
    [InlineData("query-identity", 401)]
    [InlineData("audience-trailing-slash", 401)]
    [InlineData("foreign-administrator", 403)]
    [InlineData("missing-operation", 403)]
    [InlineData("query-channel", 401)]
    [InlineData("allowed", 200)]
    public async Task AdministrationRequiresSeparateAdministratorIdentityAudienceOperationAndChannel(string scenario, int expected)
    {
        await using var server = await AuthorityHttpFixture.Start(seeded: false);
        AuthorityState state = await server.State.Repository.ReadAsync(TestContext.Current.CancellationToken);
        var operation = new AuthorityAdministration("owner-operation", state.Revision, "PublishProfile", Profile: server.State.Profile);
        string token = scenario switch
        {
            "query-identity" => server.Token(), "foreign-administrator" => server.Token("foreign", "hmac:administer", "ext-secrets-1-hmac-authority-admin"),
            "missing-operation" => server.Token(AuthorityTestState.Administrator, null, "ext-secrets-1-hmac-authority-admin"),
            "audience-trailing-slash" => server.Token(AuthorityTestState.Administrator, "hmac:administer", "ext-secrets-1-hmac-authority-admin/"),
            _ => server.AdministratorToken,
        };
        using var response = await Send(server, "admin/operations", operation, token,
            scenario == "query-channel" ? "offline-receiver-channel" : "offline-administrator-channel", administrator: true);
        ((int)response.StatusCode).ShouldBe(expected);
        (await server.State.Repository.ReadAsync(TestContext.Current.CancellationToken)).Audit.Length.ShouldBe(expected == 200 ? 1 : 0);
    }

    [Fact]
    public async Task HttpLostResponseRetryReturnsOriginalReceiptAndChangedPayloadConflicts()
    {
        await using var server = await AuthorityHttpFixture.Start(seeded: false);
        AuthorityState state = await server.State.Repository.ReadAsync(TestContext.Current.CancellationToken);
        var operation = new AuthorityAdministration("owner-original", state.Revision, "PublishProfile", Profile: server.State.Profile);
        using var first = await Send(server, "admin/operations", operation, server.AdministratorToken, "offline-administrator-channel", administrator: true);
        string original = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var retried = await Send(server, "admin/operations", operation, server.AdministratorToken, "offline-administrator-channel", administrator: true);
        (await retried.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(original);
        using var conflict = await Send(server, "admin/operations", operation with { Profile = server.State.Profile with { Version = "changed" } }, server.AdministratorToken, "offline-administrator-channel", administrator: true);
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("{\"profileReference\":\"one\",\"profileReference\":\"two\"}")]
    [InlineData("{\"profileReference\":\"one\",\"extra\":true}")]
    [InlineData("{\"profileReference\":123}")]
    [InlineData("[]")]
    public async Task MalformedOrAmbiguousQueriesCannotReturnProfile(string json)
    {
        await using var server = await AuthorityHttpFixture.Start();
        using var request = new HttpRequestMessage(HttpMethod.Post, "query-profile") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", server.Token());
        request.Headers.Add("dapr-api-token", "offline-receiver-channel");
        using var response = await server.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    [Theory]
    [InlineData("missing-revocation")]
    [InlineData("case-ambiguous")]
    [InlineData("unknown-field")]
    [InlineData("oversized")]
    public async Task IncompleteAmbiguousOrOversizedAdministrationCannotCreateCurrentState(string scenario)
    {
        await using var server = await AuthorityHttpFixture.Start(seeded: false);
        AuthorityState state = await server.State.Repository.ReadAsync(TestContext.Current.CancellationToken);
        var operation = new AuthorityAdministration("original", state.Revision, "PublishProfile", Profile: server.State.Profile);
        var body = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(operation, AuthorityJson.Options))!.AsObject();
        if (scenario == "missing-revocation") { body["profile"]!.AsObject().Remove("revoked"); }
        if (scenario == "case-ambiguous") { body["OperationId"] = "different"; }
        if (scenario == "unknown-field") { body["extra"] = true; }
        if (scenario == "oversized") { body["operationId"] = new string('x', 70 * 1024); }
        using var request = new HttpRequestMessage(HttpMethod.Post, "admin/operations") { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", server.AdministratorToken);
        request.Headers.Add("authority-admin-token", "offline-administrator-channel");
        using var response = await server.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await server.State.Repository.ReadAsync(TestContext.Current.CancellationToken)).Audit.ShouldBeEmpty();
    }

    [Fact]
    public async Task StorageFailureDeniesHttpQueriesAdministrationAndReadiness()
    {
        await using var server = await AuthorityHttpFixture.Start();
        await File.WriteAllTextAsync(Path.Combine(server.State.DirectoryPath, "authority.json"), "broken", TestContext.Current.CancellationToken);
        using var query = await Send(server, "query-profile", new { profileReference = AuthorityTestState.AppId }, server.Token(), "offline-receiver-channel");
        query.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        using var readiness = await server.Client.GetAsync("ready", TestContext.Current.CancellationToken);
        readiness.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        using var admin = await Send(server, "admin/operations", new AuthorityAdministration("new", "old", "PublishProfile", Profile: server.State.Profile),
            server.AdministratorToken, "offline-administrator-channel", administrator: true);
        admin.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public void HttpCompositionRejectsInsecureIssuer()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Authority:CallerIssuer"] = "http://insecure.example" }).Build();
        Should.Throw<ArgumentException>(() => AuthorityHttp.AddAuthentication(new ServiceCollection(), configuration));
    }

    private static Task<HttpResponseMessage> Send(AuthorityHttpFixture server, string path, object body, string? bearer, string? channel, bool administrator = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: AuthorityJson.Options) };
        if (bearer is not null) { request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer); }
        if (channel is not null) { request.Headers.Add(administrator ? "authority-admin-token" : "dapr-api-token", channel); }
        return server.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }
    private sealed class WorkloadToken(string token) : IInventoryWorkloadToken
    { public bool Configured => true; public Task<string?> GetAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(token); }
}
