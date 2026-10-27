using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace Hexalith.Platform.Secrets.Custody.Tests;

public sealed class CustodyTests
{
    private const string Issuer = "https://offline.example/authority";
    private static SecretsRuntimeConfiguration Runtime() => new() { ProviderBinding = "store", HmacCredentialReference = "credential",
        CurrentInventoryAuthorityReference = "inventory", ProfileReference = "authority", Issuer = Issuer, Audience = "ext-secrets-1-custody",
        ProfileVersion = "current", ProfileNotBefore = DateTimeOffset.UtcNow.AddHours(-1), ProfileValidUntil = DateTimeOffset.UtcNow.AddHours(1),
        CallTimeout = TimeSpan.FromSeconds(30), ExpectedProfile = PlatformAcceptedEnvelopeTiming.Bind("current", Issuer, "ext-secrets-1-custody",
            DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1)) };
    private static JsonArray Allowed() => new(new JsonObject { ["tenantId"] = "tenant-a", ["purpose"] = "ContentDigest" });

    [Fact]
    public void MissingInputKeepsUnavailableDefaults()
    {
        var services = new ServiceCollection();
        CustodyBinding binding = CustodyBinding.Compose(services, new(), new DenyingInvoker(), Allowed(), () => true);
        using ServiceProvider provider = services.BuildServiceProvider();
        binding.MissingInput.ShouldBe("runtime.providerBinding");
        provider.GetRequiredService<IPlatformHmacKeyProvider>().ShouldBeOfType<UnavailablePlatformHmacKeyProvider>();
        provider.GetRequiredService<IPlatformSigningProfileProvider>().ShouldBeOfType<UnavailablePlatformSigningProfileProvider>();
        provider.GetRequiredService<TrustedEnvelopeAuthenticator>().ShouldNotBeNull();
        provider.GetService<ICustodyKeyLifecycleProvider>().ShouldBeNull();
        provider.GetService<IDeletionCapabilitySigningProvider>().ShouldBeNull();
    }

    [Fact]
    public void CompleteReferencesInstallTheShippedBinding()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new Dapr.Client.DaprClientBuilder().Build());
        CustodyBinding binding = CustodyBinding.Compose(services, Runtime(), new DenyingInvoker(), Allowed(), () => true);
        using ServiceProvider provider = services.BuildServiceProvider();
        binding.MissingInput.ShouldBeNull();
        provider.GetRequiredService<IPlatformHmacKeyProvider>().ShouldBeOfType<DaprPlatformHmacKeyProvider>();
        provider.GetRequiredService<IPlatformSigningProfileProvider>().ShouldBeOfType<DaprPlatformSigningProfileProvider>();
        provider.GetRequiredService<IDaprPlatformHmacAuthority>().ShouldBeOfType<DaprHmacAuthorityAdapter>();
    }

    [Theory]
    [InlineData("current", true)]
    [InlineData("missing", false)]
    [InlineData("revoked", false)]
    [InlineData("stale", false)]
    [InlineData("foreign-store", false)]
    [InlineData("foreign-scope", false)]
    public async Task ReadinessRequiresAllFourCurrentExactProofs(string scenario, bool expected)
    {
        SecretsRuntimeConfiguration runtime = Runtime();
        JsonArray allowed = [];
        foreach (string tenant in new[] { "tenant-a", "tenant-b" })
        foreach (string purpose in new[] { "TrustedEnvelope", "ContentDigest" })
        { allowed.Add(new JsonObject { ["tenantId"] = tenant, ["purpose"] = purpose }); }
        var binding = new CustodyBinding(runtime, new ProofAuthority(runtime.ExpectedProfile!, scenario), allowed, null, () => true);
        (await binding.ReadyAsync(TestContext.Current.CancellationToken)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ReadinessDeniesAbsentOrUntrustedCurrentFacts(bool transport, bool fullAllowlist)
    {
        JsonArray allowed = Allowed();
        if (fullAllowlist)
        {
            allowed.Add(new JsonObject { ["tenantId"] = "tenant-a", ["purpose"] = "TrustedEnvelope" });
            allowed.Add(new JsonObject { ["tenantId"] = "tenant-b", ["purpose"] = "TrustedEnvelope" });
            allowed.Add(new JsonObject { ["tenantId"] = "tenant-b", ["purpose"] = "ContentDigest" });
        }
        var services = new ServiceCollection();
        var invoker = new DenyingInvoker();
        CustodyBinding binding = CustodyBinding.Compose(services, Runtime(), invoker, allowed, () => transport);
        (await binding.ReadyAsync(TestContext.Current.CancellationToken)).ShouldBeFalse();
        invoker.Methods.ShouldNotContain("query-inventory");
        invoker.Methods.ShouldAllBe(method => method == "query-profile");
    }

    [Theory]
    [InlineData("missing-channel", 401)]
    [InlineData("wrong-channel", 401)]
    [InlineData("missing-bearer", 401)]
    [InlineData("wrong-issuer", 401)]
    [InlineData("wrong-audience", 401)]
    [InlineData("audience-trailing-slash", 401)]
    [InlineData("expired", 401)]
    [InlineData("wrong-signature", 401)]
    [InlineData("foreign-caller", 403)]
    [InlineData("missing-operation", 403)]
    [InlineData("wrong-tenant", 403)]
    [InlineData("valid-unbound", 503)]
    [InlineData("duplicate-fields", 400)]
    [InlineData("unknown-field", 400)]
    public async Task ActualReceiverAuthenticatesAndFailsClosed(string scenario, int status)
    {
        await using Fixture fixture = await Fixture.Start();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/digest");
        if (scenario != "missing-channel") { request.Headers.Add("dapr-api-token", scenario == "wrong-channel" ? "wrong" : "offline-channel"); }
        if (scenario != "missing-bearer") { request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(scenario)); }
        string json = scenario switch { "duplicate-fields" => "{\"tenantId\":\"tenant-a\",\"tenantId\":\"tenant-b\",\"components\":[]}",
            "unknown-field" => "{\"tenantId\":\"tenant-a\",\"components\":[],\"key\":\"forbidden\"}", _ => "{\"tenantId\":\"tenant-a\",\"components\":[\"public-test\"]}" };
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        ((int)response.StatusCode).ShouldBe(status);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldNotContain("public-test");
    }

    [Theory]
    [InlineData("[\"source-fixture-only\"]", "\u000119:source-fixture-only\u001f")]
    [InlineData("[\"different\",null,\"\"]", "\u00019:different\u001f\u00000:\u001f\u00010:\u001f")]
    public async Task AuthenticatedReceiverUsesExistingHmacServiceWithSourceFixtureKeys(string componentsJson, string canonicalSuffix)
    {
        await using Fixture fixture = await Fixture.Start(bound: true);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/digest");
        request.Headers.Add("dapr-api-token", "offline-channel");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token("valid"));
        request.Content = new StringContent("{\"tenantId\":\"tenant-a\",\"components\":" + componentsJson + "}", Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonObject body = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();
        body["keyVersion"]!.GetValue<string>().ShouldBe("offline-v1");
        // Independent AD-29 bytes: present marker, UTF-16 length, colon, text, U+001F.
        byte[] expectedBytes = Encoding.UTF8.GetBytes("\u000113:ContentDigest\u001f\u00018:tenant-a\u001f" + canonicalSuffix);
        string expected = Convert.ToHexStringLower(HMACSHA256.HashData(Enumerable.Repeat((byte)0x42, 32).ToArray(), expectedBytes));
        body["digest"]!.GetValue<string>().ShouldBe(expected);
    }

    [Fact]
    public async Task HealthIsMetadataOnlyAndCannotClaimAvailability()
    {
        await using Fixture fixture = await Fixture.Start();
        string health = await fixture.Client.GetStringAsync("/health", TestContext.Current.CancellationToken);
        health.ShouldContain("\"dependencyAvailable\":false");
        health.ShouldContain("\"decisionSignerInstalled\":false");
        (await fixture.Client.GetAsync("/ready", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    private sealed class DenyingInvoker : ISecretsDaprInvoker
    {
        public List<string> Methods { get; } = [];
        public Task<string?> InvokeAsync(string appId, string method, string jsonBody, CancellationToken cancellationToken)
        { Methods.Add(method); return Task.FromResult<string?>(null); }
    }

    private sealed class ProofAuthority(PlatformSigningProfile profile, string scenario) : IDaprPlatformHmacAuthority
    {
        public PlatformSigningProfile? GetCurrentProfile(string reference) => profile;
        public ValueTask<DaprPlatformHmacInventory?> ResolveAsync(string reference, PlatformHmacScope scope, string? version, CancellationToken token)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return ValueTask.FromResult<DaprPlatformHmacInventory?>(scenario == "missing" ? null : new(reference, "stable-revision",
                scenario == "foreign-scope" ? new("foreign", scope.Purpose) : scope,
                new("offline-v1", scenario == "revoked" ? PlatformHmacKeyState.Revoked : PlatformHmacKeyState.Active, now.AddHours(-1), now.AddHours(1), null),
                scenario == "foreign-store" ? "foreign" : "store", "credential", "exact-reference", "2", "hmacKey", now.AddSeconds(-1),
                scenario == "stale" ? now.AddSeconds(-1) : now.AddSeconds(20)));
        }
    }

    private sealed class FixtureKeys : IPlatformHmacKeyProvider
    {
        public ValueTask<PlatformHmacKeyResolution> ResolveAsync(PlatformHmacScope scope, string? version = null, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new PlatformHmacKeyResolution(CustodyStatus.Succeeded, new(scope,
                new("offline-v1", PlatformHmacKeyState.Active, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), null),
                Enumerable.Repeat((byte)0x42, 32).ToArray())));
    }

    private sealed class FixtureProfile(PlatformSigningProfile profile) : IPlatformSigningProfileProvider
    { public PlatformSigningProfile? GetCurrent() => profile; }

    private sealed class Fixture(WebApplication app, RSA signing, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public string Token(string scenario)
        {
            using RSA other = RSA.Create(2048);
            List<Claim> claims = [new("azp", scenario == "foreign-caller" ? "foreign" : "offline-caller"),
                new("eventstore:tenant", scenario == "wrong-tenant" ? "tenant-b" : "tenant-a")];
            if (scenario != "missing-operation") { claims.Add(new("eventstore:operation", "custody:digest")); }
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(scenario == "wrong-issuer" ? "https://foreign.example" : Issuer,
                scenario == "wrong-audience" ? "foreign" : scenario == "audience-trailing-slash" ? "ext-secrets-1-custody/" : "ext-secrets-1-custody", claims,
                DateTime.UtcNow.AddMinutes(-2), scenario == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(1),
                new SigningCredentials(new RsaSecurityKey(scenario == "wrong-signature" ? other : signing), SecurityAlgorithms.RsaSha256)));
        }
        public static async Task<Fixture> Start(bool bound = false)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Custody:CallerIssuer"] = Issuer,
                ["Custody:EnrolledCallerIds:0"] = "offline-caller", ["APP_API_TOKEN"] = "offline-channel" });
            CustodyHttp.AddAuthentication(builder.Services, builder.Configuration);
            RSA signing = RSA.Create(2048);
            builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                configuration.SigningKeys.Add(new RsaSecurityKey(signing));
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
            CustodyBinding binding = CustodyBinding.Compose(builder.Services, new(), new DenyingInvoker(), Allowed(), () => false);
            if (bound)
            {
                builder.Services.Replace(ServiceDescriptor.Singleton<IPlatformHmacKeyProvider>(new FixtureKeys()));
                builder.Services.Replace(ServiceDescriptor.Singleton<IPlatformSigningProfileProvider>(new FixtureProfile(Runtime().ExpectedProfile!)));
            }
            WebApplication app = builder.Build();
            CustodyHttp.Map(app, binding);
            await app.StartAsync(TestContext.Current.CancellationToken);
            return new(app, signing, new HttpClient { BaseAddress = new Uri(app.Urls.Single()) });
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); signing.Dispose(); await app.DisposeAsync(); }
    }
}
