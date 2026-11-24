namespace Hexalith.Platform.Composition.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.EventStore.Aspire;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

[Collection("Platform file entry")]
public sealed partial class PlatformLocalRealmLiveTests
{
    private const string _loopback = "http://127.0.0.1:45678/callback";
    private const string _testUser = "realm-live-user";
    private const string _testPasswordName = "HEXALITH_LOCAL_REALM_LIVE_TEST_PASSWORD";
    private const string _exchangeGrant = "urn:ietf:params:oauth:grant-type:token-exchange";
    private const string _accessType = "urn:ietf:params:oauth:token-type:access_token";
    private const string _refreshType = "urn:ietf:params:oauth:token-type:refresh_token";

    [Fact]
    public async Task LiveRealmEnforcesMcpCliGrantsAndExchangeBoundaries()
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_LIVE_SMOKE") != "1")
        {
            Assert.Skip("Set HEXALITH_PLATFORM_LIVE_SMOKE=1 to start Keycloak 26.6.4.");
        }

        CancellationToken token = TestContext.Current.CancellationToken;
        int port = CompositionPortAllocator.Allocate(1).UiHttp;
        var authority = new Uri($"http://127.0.0.1:{port}/realms/hexalith-local");
        string workspace = Directory.CreateTempSubdirectory("platform-realm-live-").FullName;
        string containerName = "platform-realm-live-" + Guid.NewGuid().ToString("N");
        Dictionary<string, string> secrets = PlatformLocalRealmTests.ClientSecrets();
        string password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        try
        {
            string import = Path.Combine(workspace, "hexalith-local-realm.json");
            var realm = JsonNode.Parse(PlatformLocalRealm.Render(PlatformRealmContract.Load(), PlatformLocalRealmTests.PreviewDeclarations(),
                authority, PlatformLocalRealmTests.SyntheticTenant, secrets))!;
            AddFixtureUser(realm);
            AddExchangeFixtures(realm, secrets);
            File.WriteAllText(import, realm.ToJsonString());
            PlatformLocalRealm.AllowContainerRead(import);
            string policy = Path.Combine(workspace, "platform-requester-audience.jar");
            PlatformLocalRealm.WriteAudiencePolicy(policy);
            string binding = Path.Combine(workspace, "platform-workload-binding.jar");
            PlatformLocalRealm.WriteWorkloadBindingPolicy(binding);
            Dictionary<string, string> environment = secrets.ToDictionary(
                pair => PlatformLocalRealm.SecretEnvironmentName(pair.Key), pair => pair.Value, StringComparer.Ordinal);
            var prepared = PlatformLocalRealm.PrepareWorksPreview(Path.Combine(
                PlatformTestRepository.ReferenceRoot(PlatformTestRepository.Root, "Hexalith.EventStore"),
                "module", "hexalith.module-manifest.v2.json"), port, PlatformLocalRealmTests.SyntheticTenant);
            foreach ((string key, string value) in prepared.WorkloadBindingEnvironment(DateTimeOffset.UtcNow))
            {
                environment.Add(key, value);
            }
            environment.Add(_testPasswordName, password);
            environment.Add("KC_HOSTNAME", authority.GetLeftPart(UriPartial.Authority));
            List<string> arguments =
            [
                "run", "--detach", "--name", containerName, "--publish", $"127.0.0.1:{port}:8080",
                "--volume", $"{import}:{PlatformLocalRealm.ImportContainerDirectory}{Path.GetFileName(import)}:ro",
                "--volume", $"{policy}:{PlatformLocalRealm.PolicyContainerPath}:ro",
                "--volume", $"{binding}:{PlatformLocalRealm.BindingContainerPath}:ro",
            ];
            foreach (string variable in environment.Keys)
            {
                arguments.Add("--env");
                arguments.Add(variable);
            }
            arguments.Add(PlatformLocalRealm.Image);
            arguments.AddRange(PlatformLocalRealm.StartupArguments);
            CompositionProcessResult start = await DockerAsync(arguments, environment, token);
            start.ExitCode.ShouldBe(0, start.ErrorOutput);

            using HttpClient browser = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
            {
                BaseAddress = new Uri(authority.AbsoluteUri + "/protocol/openid-connect/"),
                Timeout = TimeSpan.FromSeconds(10),
            };
            await WaitForDiscoveryAsync(browser, authority, containerName, token);
            string agent = await VerifyMcpCliAsync(browser, password, token);
            await VerifyExchangeAsync(browser, agent, secrets, token);
            var issuerSettings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:WorkloadJwtBearer:Authority"] = authority.AbsoluteUri,
                ["Authentication:WorkloadJwtBearer:Issuer"] = authority.AbsoluteUri,
                ["Authentication:WorkloadJwtBearer:Audience"] = "eventstore",
                ["Authentication:WorkloadJwtBearer:AllowedAlgorithms:0"] = "RS256",
                ["Authentication:WorkloadJwtBearer:RequireHttpsMetadata"] = "false",
                ["Authentication:WorkloadIssuer:ClientId"] = "works",
                ["Authentication:WorkloadIssuer:Workload"] = "works",
                ["Authentication:WorkloadIssuer:ClientSecret"] = secrets["works"],
                ["Authentication:WorkloadIssuer:EnableResourceBindingRequests"] = "true",
            }).Build();
            var sdk = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
            sdk.Services.AddEventStoreWorkloadAssertionIssuer(issuerSettings);
            using var issuerHost = sdk.Build();
            await VerifyWorksGrantsAsync(issuerHost.Services.GetRequiredService<IWorkloadAssertionIssuer>(), PlatformLocalRealmTests.SyntheticTenant, token);
        }
        finally
        {
            try
            {
                await RemoveOwnedContainerAsync(containerName);
            }
            finally
            {
                Directory.Delete(workspace, recursive: true);
            }
        }
    }

    private static void AddFixtureUser(JsonNode realm) => realm["users"]!.AsArray().Add(new JsonObject
    {
        ["username"] = _testUser,
        ["firstName"] = "Realm",
        ["lastName"] = "Test",
        ["email"] = "realm-test@example.test",
        ["emailVerified"] = true,
        ["enabled"] = true,
        ["credentials"] = new JsonArray(new JsonObject
        {
            ["type"] = "password",
            ["value"] = "${" + _testPasswordName + "}",
        }),
    });

    private static void AddExchangeFixtures(JsonNode realm, Dictionary<string, string> secrets)
    {
        JsonArray clients = realm["clients"]!.AsArray();
        JsonNode gateway = clients.Single(client => client!["clientId"]!.GetValue<string>() == "eventstore")!;
        foreach ((string id, bool worksRole) in new[] { ("exchange-admitted", true), ("exchange-refused", false) })
        {
            secrets.Add(id, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            JsonNode subject = gateway.DeepClone();
            subject["clientId"] = id;
            subject["secret"] = "${" + PlatformLocalRealm.SecretEnvironmentName(id) + "}";
            subject["attributes"]!["standard.token.exchange.enabled"] = "false";
            clients.Add(subject);
            var roles = new JsonObject { ["eventstore"] = new JsonArray("invoker") };
            if (worksRole)
            {
                roles["works"] = new JsonArray("invoker");
            }
            realm["users"]!.AsArray().Add(new JsonObject
            {
                ["username"] = "service-account-" + id,
                ["enabled"] = true,
                ["serviceAccountClientId"] = id,
                ["clientRoles"] = roles,
            });
        }
    }

    private static async Task WaitForDiscoveryAsync(HttpClient browser, Uri authority, string container, CancellationToken cancellation)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(2);
        var discovery = new Uri(authority.AbsoluteUri + "/.well-known/openid-configuration");
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using HttpResponseMessage response = await browser.GetAsync(discovery, cancellation);
                if (response.IsSuccessStatusCode)
                {
                    JsonNode metadata = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellation))!;
                    metadata["issuer"]!.GetValue<string>().ShouldBe(authority.AbsoluteUri);
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // The container is still starting.
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                // The discovery request timed out before the startup deadline.
            }

            CompositionProcessResult state = await DockerAsync(["inspect", "--format", "{{.State.Running}}", container], null, cancellation);
            if (state.ExitCode != 0 || state.Output.Trim() != "true") break;

            await Task.Delay(TimeSpan.FromSeconds(1), cancellation);
        }

        CompositionProcessResult logs = await DockerAsync(["logs", container], null, cancellation);
        throw new InvalidOperationException("The local realm never became ready.\n" + logs.Output + logs.ErrorOutput);
    }

    private static async Task<string> VerifyMcpCliAsync(HttpClient browser, string password, CancellationToken cancellation)
    {
        string verifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        string challenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        string AuthorizationPath(string scope) => QueryHelpers.AddQueryString("auth", new Dictionary<string, string?>
        {
            ["client_id"] = "mcpcli",
            ["redirect_uri"] = _loopback,
            ["response_type"] = "code",
            ["scope"] = scope,
            ["state"] = "realm-live-state",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        });

        using (HttpResponseMessage offline = await browser.GetAsync(AuthorizationPath("openid offline_access"), cancellation))
        {
            offline.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            QueryHelpers.ParseQuery(offline.Headers.Location!.Query)["error"].ToString().ShouldBe("invalid_scope");
        }
        using (HttpResponseMessage plain = await browser.GetAsync(
            AuthorizationPath("openid").Replace("code_challenge_method=S256", "code_challenge_method=plain", StringComparison.Ordinal), cancellation))
        {
            plain.StatusCode.ShouldBe(HttpStatusCode.Redirect);
            QueryHelpers.ParseQuery(plain.Headers.Location!.Query)["error"].ToString().ShouldBe("invalid_request");
        }
        await RequireOAuthRefusalAsync(browser, "token", new()
        {
            ["grant_type"] = "password", ["client_id"] = "mcpcli", ["username"] = _testUser, ["password"] = password,
        }, "unauthorized_client", cancellation);
        await RequireOAuthRefusalAsync(browser, "auth/device", new()
        {
            ["client_id"] = "mcpcli",
        }, "unauthorized_client", cancellation);

        using HttpResponseMessage pageResponse = await browser.GetAsync(AuthorizationPath("openid"), cancellation);
        pageResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        string page = await pageResponse.Content.ReadAsStringAsync(cancellation);
        Match action = LoginAction().Match(page);
        action.Success.ShouldBeTrue("Keycloak returned a login form.");
        string login = WebUtility.HtmlDecode(action.Groups[1].Value);
        using HttpRequestMessage loginRequest = new(HttpMethod.Post, login);
        // Browsers accept Keycloak's Secure cookies on a trusted loopback origin. CookieContainer does not.
        string cookies = string.Join("; ", pageResponse.Headers.GetValues("Set-Cookie").Select(value => value.Split(';', 2)[0]));
        loginRequest.Headers.TryAddWithoutValidation("Cookie", cookies);
        loginRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = _testUser, ["password"] = password,
        });
        using HttpResponseMessage response = await browser.SendAsync(loginRequest, cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        Uri redirect = response.Headers.Location!;
        redirect.GetLeftPart(UriPartial.Path).ShouldBe(_loopback);
        var query = QueryHelpers.ParseQuery(redirect.Query);
        query["state"].ToString().ShouldBe("realm-live-state");
        query["code"].ToString().ShouldNotBeNullOrEmpty();

        string jwt = await AcquireAccessTokenAsync(browser, new()
        {
            ["grant_type"] = "authorization_code", ["client_id"] = "mcpcli", ["redirect_uri"] = _loopback,
            ["code"] = query["code"].ToString(), ["code_verifier"] = verifier,
        }, cancellation);
        JsonWebToken access = new(jwt);
        access.Alg.ShouldBe("RS256");
        access.GetClaim("sub").Value.ShouldNotBeNullOrEmpty();
        access.GetClaim("azp").Value.ShouldBe("mcpcli");
        access.Audiences.ShouldBe(["hexalith-eventstore"]);
        access.Claims.Where(claim => claim.Type == RouteCatalogSurfacePolicy.ClaimType).Select(claim => claim.Value)
            .ShouldBe(["agent"]);
        access.Claims.Select(claim => claim.Type).Intersect(["realm_access", "resource_access", "roles", "role"]).ShouldBeEmpty();
        return jwt;
    }

    private static async Task VerifyExchangeAsync(HttpClient browser, string agent, Dictionary<string, string> secrets, CancellationToken cancellation)
    {
        Dictionary<string, string> Exchange(string requester, string subject, string audience, string scope,
            string tokenType = _accessType) => new()
        {
            ["grant_type"] = _exchangeGrant,
            ["client_id"] = requester,
            ["client_secret"] = secrets[requester],
            ["subject_token"] = subject,
            ["subject_token_type"] = _accessType,
            ["requested_token_type"] = tokenType,
            ["audience"] = audience,
            ["scope"] = scope,
        };
        string workAudience = "eventstore-audience.works";
        string gatewayAudience = "eventstore-audience.eventstore";
        string project = "eventstore-operation.domain-service.project";
        string notify = "eventstore-operation.projection.notify";
        string CredentialScope(string audience, string operation) => audience + " " + operation;
        async Task<string> ServiceToken(string client, string scope) => await AcquireAccessTokenAsync(browser, new()
        {
            ["grant_type"] = "client_credentials", ["client_id"] = client, ["client_secret"] = secrets[client], ["scope"] = scope,
        }, cancellation);

        string gatewayForWorks = await ServiceToken("eventstore", CredentialScope(workAudience, project));
        JwtWorkloadAssertionIssuer.GetTokenScopeFailure(gatewayForWorks, "works", EventStoreWorkloadOperations.DomainServiceProject)
            .ShouldBeNull();
        var workload = new JsonWebToken(gatewayForWorks);
        workload.GetClaim("azp").Value.ShouldBe("eventstore");
        workload.GetClaim(RouteCatalogSurfacePolicy.ClaimType).Value.ShouldBe("service");

        JsonNode noRequester = await RequireOAuthRefusalAsync(browser, "token",
            Exchange("eventstore", agent, "works", workAudience), "invalid_request", cancellation);
        noRequester["error_description"]!.GetValue<string>().ShouldContain("Requester is not an audience");

        // A token acquired by this requester is still denied when it names only another audience.
        JsonNode sameRequester = await RequireOAuthRefusalAsync(browser, "token",
            Exchange("eventstore", gatewayForWorks, "works", workAudience), "invalid_request", cancellation);
        sameRequester["error_description"]!.GetValue<string>().ShouldContain("Requester is not an audience");

        string gatewayForGateway = await ServiceToken("eventstore", CredentialScope(gatewayAudience, notify));
        string exchanged = await AcquireAccessTokenAsync(browser,
            Exchange("eventstore", gatewayForGateway, "eventstore", CredentialScope(gatewayAudience, notify)), cancellation);
        JwtWorkloadAssertionIssuer.GetTokenScopeFailure(exchanged, "eventstore", EventStoreWorkloadOperations.ProjectionNotify)
            .ShouldBeNull();

        string subject = await ServiceToken("exchange-admitted", $"{gatewayAudience} {workAudience} {project}");
        var crossClientSubject = new JsonWebToken(subject);
        crossClientSubject.Audiences.Order(StringComparer.Ordinal).ShouldBe(["eventstore", "works"]);
        crossClientSubject.GetClaim("azp").Value.ShouldBe("exchange-admitted");
        string narrowed = await AcquireAccessTokenAsync(browser,
            Exchange("eventstore", subject, "works", CredentialScope(workAudience, project)), cancellation);
        var crossClientExchange = new JsonWebToken(narrowed);
        crossClientExchange.GetClaim("azp").Value.ShouldBe("eventstore");
        crossClientExchange.Subject.ShouldBe(crossClientSubject.Subject);
        JwtWorkloadAssertionIssuer.GetTokenScopeFailure(narrowed, "works", EventStoreWorkloadOperations.DomainServiceProject).ShouldBeNull();

        // Both subject clients have the target scope assigned, but only one subject's role can activate it.
        string withoutRole = await ServiceToken("exchange-refused", $"{gatewayAudience} {workAudience} {project}");
        new JsonWebToken(withoutRole).Audiences.ShouldBe(["eventstore"]);
        await RequireOAuthRefusalAsync(browser, "token",
            Exchange("eventstore", withoutRole, "works", CredentialScope(workAudience, project)), "invalid_scope", cancellation);

        await RequireOAuthRefusalAsync(browser, "token",
            Exchange("eventstore", gatewayForGateway, "account", CredentialScope(gatewayAudience, notify)),
            "invalid_request", cancellation);
        await RequireOAuthRefusalAsync(browser, "token",
            Exchange("eventstore", gatewayForGateway, "eventstore", CredentialScope(gatewayAudience, project)),
            "invalid_scope", cancellation);
        await RequireOAuthRefusalAsync(browser, "token",
            Exchange("eventstore", gatewayForGateway, "eventstore", CredentialScope(gatewayAudience, notify), _refreshType),
            "invalid_request", cancellation);
    }

    [Fact]
    public async Task LiveWorksPreviewAuthorizesOnlyTheBoundSdkStreamReadAndRejectsTheLegacyKey()
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_LIVE_SMOKE") != "1")
        {
            Assert.Skip("Set HEXALITH_PLATFORM_LIVE_SMOKE=1 to start the Works preview with Aspire and Dapr.");
        }

        string platform = PlatformTestRepository.Root;
        string eventStore = PlatformTestRepository.ReferenceRoot(platform, "Hexalith.EventStore");
        string builds = PlatformTestRepository.ReferenceRoot(platform, "Hexalith.Builds");
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        string workspace = Directory.CreateTempSubdirectory("platform-realm-preview-").FullName;
        string redis = "platform-realm-redis-" + Guid.NewGuid().ToString("N");
        string syntheticTenant = "realm-preview-" + Guid.NewGuid().ToString("N");
        string? previousArtifacts = Environment.GetEnvironmentVariable("ArtifactsPath");
        string? previousSourceMode = Environment.GetEnvironmentVariable("UseHexalithProjectReferences");
        string? previousPackageMode = Environment.GetEnvironmentVariable("UseNuGetDeps");
        string? previousSdkVersion = Environment.GetEnvironmentVariable("HexalithEventStoreVersion");
        string? previousTargets = Environment.GetEnvironmentVariable("CustomAfterMicrosoftCommonTargets");
        string appHost = string.IsNullOrWhiteSpace(previousArtifacts) ? Path.Combine(workspace, "apphost") : Path.GetFullPath(previousArtifacts);
        var sdkMetadata = typeof(PlatformLocalRealmLiveTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
        bool sourceMode = bool.TryParse(sdkMetadata.Single(attribute => attribute.Key == "PlatformTestSdkSourceMode").Value, out bool useSource)
            && useSource;
        string? sdkVersion = sdkMetadata.Single(attribute => attribute.Key == "PlatformTestSdkVersion").Value;
        try
        {
            // The file AppHost, owner preparation and Aspire launch must resolve the same SDK and output paths.
            Environment.SetEnvironmentVariable("ArtifactsPath", appHost);
            Environment.SetEnvironmentVariable("UseHexalithProjectReferences", sourceMode ? "true" : "false");
            Environment.SetEnvironmentVariable("UseNuGetDeps", sourceMode ? "false" : "true");
            Environment.SetEnvironmentVariable("HexalithEventStoreVersion", sdkVersion);
            CompositionProcessResult build = await CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("dotnet",
                ["build", "./apphost.cs", "--artifacts-path", appHost, "-p:AspireUseCliBundle=true",
                    "-p:HexalithEventStoreRoot=" + eventStore, "-p:HexalithBuildsRoot=" + builds], platform, null),
                TimeSpan.FromMinutes(8), cancellation);
            build.ExitCode.ShouldBe(0, build.Output + build.ErrorOutput);
            string assembly = Path.Combine(appHost, "bin", "apphost.cs", "debug", "apphost.dll");
            Type entryPoint = Assembly.LoadFrom(assembly)
                .EntryPoint!.DeclaringType!;

            string targets = Path.Combine(workspace, "RealmProbe.targets");
            var proofReference = new XElement("Project",
                new XElement("ItemGroup", new XAttribute("Condition", "'$(MSBuildProjectName)' == 'Hexalith.Works'"),
                    new XElement("Reference", new XAttribute("Include", "Hexalith.Platform.Composition.Tests"),
                        new XElement("HintPath", typeof(PlatformRealmProbeHostingStartup).Assembly.Location), new XElement("Private", true))));
            if (!string.IsNullOrEmpty(previousTargets)) proofReference.AddFirst(new XElement("Import", new XAttribute("Project", previousTargets)));
            WritePrivateFile(targets, proofReference.ToString());
            Environment.SetEnvironmentVariable("CustomAfterMicrosoftCommonTargets", targets);

            CompositionProcessResult started = await DockerAsync(["run", "--detach", "--rm", "--name", redis,
                "--publish", "127.0.0.1::6379", "redis:7.4-alpine", "redis-server", "--save", "", "--appendonly", "no"],
                null, cancellation);
            started.ExitCode.ShouldBe(0, started.ErrorOutput);
            // Seed the SDK's writer readiness marker in the disposable store before the preview starts.
            string activation = JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                writerProtocolVersion = 2,
                cutoverCommit = "realm-preview-disposable-synthetic",
                activatedAt = DateTimeOffset.UtcNow,
            });
            using var redisStartup = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            redisStartup.CancelAfter(TimeSpan.FromSeconds(20));
            CompositionProcessResult seeded;
            do
            {
                seeded = await DockerAsync(["exec", redis, "redis-cli", "HSET",
                    "eventstore||projection-delivery-writer-protocol", "data", activation, "version", "1"], null, redisStartup.Token);
                if (seeded.ExitCode != 0) await Task.Delay(100, redisStartup.Token);
            }
            while (seeded.ExitCode != 0);
            seeded.ExitCode.ShouldBe(0, seeded.ErrorOutput);
            seeded.Output.Trim().ShouldBe("2");
            CompositionProcessResult port = await DockerAsync(["port", redis, "6379/tcp"], null, cancellation);
            port.ExitCode.ShouldBe(0, port.ErrorOutput);
            IPEndPoint endpoint = IPEndPoint.Parse(port.Output.Trim());
            endpoint.Address.ShouldBe(IPAddress.Loopback);
            string profile = WritePreviewProfile(platform, workspace, endpoint.Port);
            string testPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            string inheritedEndpoint = "http://127.0.0.1:1/inherited-token";
            await using var builder = await DistributedApplicationTestingBuilder.CreateAsync(entryPoint,
                ["--environment=Development", "--Platform:Works:Enabled=true", "--DcpPublisher:RandomizePorts=false",
                    "--Platform:Works:DevelopmentCatalogProfile=" + profile, "--Platform:Works:EventStoreSourceRoot=" + eventStore,
                    "--Platform:Works:SyntheticTenantId=" + syntheticTenant, "--Authentication:WorkloadIssuer:TokenEndpoint=" + inheritedEndpoint],
                cancellation);
            ContainerResource keycloak = builder.Resources.OfType<ContainerResource>().Single(resource => resource.Name == "platform-local-realm");
            ContainerMountAnnotation import = keycloak.Annotations.OfType<ContainerMountAnnotation>()
                .Single(mount => mount.Target.StartsWith(PlatformLocalRealm.ImportContainerDirectory, StringComparison.Ordinal));
            JsonNode realm = JsonNode.Parse(File.ReadAllText(import.Source!))!;
            AddFixtureUser(realm);
            File.WriteAllText(import.Source!, realm.ToJsonString());
            builder.CreateResourceBuilder(keycloak).WithEnvironment(_testPasswordName, testPassword);
            var works = builder.CreateResourceBuilder(builder.Resources.OfType<ProjectResource>().Single(resource => resource.Name == "works"));
            works.WithEnvironment("HEXALITH_REALM_PROBE_DIRECTORY", workspace)
                .WithEnvironment(context =>
                {
                    const string key = "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES";
                    context.EnvironmentVariables[key] = context.EnvironmentVariables[key] + ";" + typeof(PlatformRealmProbeHostingStartup).Assembly.GetName().Name;
                });
            IsolateDapr(builder, workspace);

            await using DistributedApplication app = await builder.BuildAsync(cancellation);
            try
            {
                using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                startup.CancelAfter(TimeSpan.FromMinutes(8));
                await app.StartAsync(startup.Token);
                foreach (string resource in new[] { keycloak.Name, "eventstore", "works", "eventstore-admin", "eventstore-operations" })
                {
                    await app.ResourceNotifications.WaitForResourceHealthyAsync(resource, WaitBehavior.StopOnResourceUnavailable, startup.Token);
                }
                var expectedIssuer = new Uri(realm["attributes"]!["frontendUrl"]!.GetValue<string>() + "/realms/hexalith-local");
                app.GetEndpoint(keycloak.Name, "http").Port.ShouldBe(expectedIssuer.Port);
                using var discovery = new HttpClient();
                JsonNode? metadata = await discovery.GetFromJsonAsync<JsonNode>(
                    expectedIssuer.AbsoluteUri + "/.well-known/openid-configuration", startup.Token);
                metadata!["issuer"]!.GetValue<string>().ShouldBe(expectedIssuer.AbsoluteUri);
                await VerifyRunningPreviewAsync(app, workspace, testPassword, syntheticTenant, cancellation);
            }
            finally
            {
                using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(50));
                await app.StopAsync(stop.Token);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("ArtifactsPath", previousArtifacts);
            Environment.SetEnvironmentVariable("UseHexalithProjectReferences", previousSourceMode);
            Environment.SetEnvironmentVariable("UseNuGetDeps", previousPackageMode);
            Environment.SetEnvironmentVariable("HexalithEventStoreVersion", previousSdkVersion);
            Environment.SetEnvironmentVariable("CustomAfterMicrosoftCommonTargets", previousTargets);
            try
            {
                await RemoveOwnedContainerAsync(redis);
            }
            finally
            {
                Directory.Delete(workspace, recursive: true);
            }
        }
    }

    private static void IsolateDapr(IDistributedApplicationTestingBuilder builder, string workspace)
    {
        string discovery = CompositionDaprComponentRenderer.RenderConfiguration("realm-smoke", workspace);
        var projects = builder.Resources.OfType<ProjectResource>()
            .Select(project => (Project: project, Sidecar: project.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar))
            .ToArray();
        var excluded = builder.Resources.SelectMany(resource => resource.Annotations.OfType<EndpointAnnotation>())
            .SelectMany(endpoint => new[] { endpoint.Port, endpoint.TargetPort }).OfType<int>().ToHashSet();
        foreach (var entry in projects)
        {
            DaprSidecarOptions options = entry.Sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options;
            if (options.DaprHttpPort is int port) excluded.Add(port);
        }

        var allocated = CompositionPortAllocator.Allocate(projects.Length, includeSecondEventStore: false, excluded);
        foreach (var (entry, ports) in projects.Zip(allocated.ModuleDapr))
        {
            var (project, sidecar) = entry;
            foreach (EndpointAnnotation endpoint in project.Annotations.OfType<EndpointAnnotation>()) endpoint.Port = null;
            DaprSidecarOptionsAnnotation annotation = sidecar.Annotations.OfType<DaprSidecarOptionsAnnotation>().Single();
            string configuration = annotation.Options.Config.ShouldNotBeNull();
            var isolated = JsonNode.Parse(PlatformAppModel.MergeRunDiscovery(File.ReadAllText(configuration), discovery))!;
            JsonNode registry = isolated["spec"]!["nameResolution"]!["configuration"]!;
            registry["busyTimeout"] = "5s";
            registry["timeout"] = "2s";
            registry["disableWAL"] = "true";
            // The catalog observes the same SDK-generated receiver path for the app and its sidecar.
            FileInfo file = new(configuration);
            file.Name.ShouldBe(project.Name + ".configuration.json");
            file.Directory!.Name.ShouldStartWith("hexalith-route-catalog-");
            file.Directory.Parent!.FullName.ShouldBe(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar));
            WritePrivateFile(configuration, isolated.ToJsonString());
            sidecar.Annotations[sidecar.Annotations.IndexOf(annotation)] = new DaprSidecarOptionsAnnotation(annotation.Options with
            {
                AppPort = null,
                DaprHttpPort = project.Name == "eventstore" ? annotation.Options.DaprHttpPort : null,
                DaprGrpcPort = null,
                DaprInternalGrpcPort = ports.InternalGrpc,
                MetricsPort = null,
            });
            builder.CreateResourceBuilder(project).WithEnvironment(context =>
            {
                ExecutableResource cli = builder.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == sidecar.Name + "-cli");
                var http = new EndpointReference(cli, "http");
                var grpc = new EndpointReference(cli, "grpc");
                context.EnvironmentVariables["DAPR_HTTP_ENDPOINT"] = http;
                context.EnvironmentVariables["DAPR_GRPC_ENDPOINT"] = grpc;
                context.EnvironmentVariables["DAPR_HTTP_PORT"] = http.Property(EndpointProperty.Port);
                context.EnvironmentVariables["DAPR_GRPC_PORT"] = grpc.Property(EndpointProperty.Port);
                if (project.Name == "eventstore-admin")
                {
                    ExecutableResource gateway = builder.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == "eventstore-dapr-cli");
                    context.EnvironmentVariables["AdminServer__EventStoreDaprHttpEndpoint"] = new EndpointReference(gateway, "http");
                }
            });
        }
    }

    private static string WritePreviewProfile(string platform, string workspace, int redisPort)
    {
        string Component(string name)
        {
            string path = Path.Combine(workspace, name + ".yaml");
            string template = File.ReadAllText(Path.Combine(platform, "DaprComponents", name + ".yaml"));
            WritePrivateFile(path, template.Replace("localhost:6379", $"127.0.0.1:{redisPort}", StringComparison.Ordinal));
            return path;
        }

        var profile = new WorksDevelopmentCatalogProfile(Component("statestore"),
            new DevelopmentRouteCatalogCompositionOptions(Path.Combine(workspace, "custody"),
                "routing-realm-smoke", "works-realm-smoke", "realm-v1",
                new Dictionary<string, string> { ["realm-v1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }, "realm-v1"),
            PubSubComponentPath: Component("pubsub"), ConfigStoreComponentPath: Component("configstore"));
        string path = Path.Combine(workspace, "profile.json");
        WritePrivateFile(path, JsonSerializer.Serialize(profile));
        return path;
    }

    private static void WritePrivateFile(string path, string contents)
    {
        File.WriteAllText(path, contents);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static async Task VerifyRunningPreviewAsync(DistributedApplication app, string workspace, string password, string tenant,
        CancellationToken cancellation)
    {
        app.ResourceNotifications.TryGetCurrentState("works", out ResourceEvent? works).ShouldBeTrue();
        var settings = works!.Snapshot.EnvironmentVariables
            .GroupBy(variable => variable.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (group.FirstOrDefault(variable => variable.IsFromSpec) ?? group.First()).Value, StringComparer.Ordinal);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings.Select(pair =>
            new KeyValuePair<string, string?>(pair.Key.Replace("__", ":", StringComparison.Ordinal), pair.Value))).Build();
        string authority = configuration["Authentication:WorkloadJwtBearer:Authority"].ShouldNotBeNull();
        configuration["Authentication:WorkloadIssuer:TokenEndpoint"].ShouldBe(authority + "/protocol/openid-connect/token");
        configuration["Authentication:WorkloadIssuer:EnableResourceBindingRequests"].ShouldBe("true");
        var allowed = new StreamReadRequest(tenant, "work", "missing-realm-stream");
        string kickoff = Path.Combine(workspace, "read-request");
        WritePrivateFile(kickoff + ".tmp", JsonSerializer.Serialize(allowed));
        File.Move(kickoff + ".tmp", kickoff);
        string result = Path.Combine(workspace, "read-result");
        using (var probe = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
        {
            probe.CancelAfter(TimeSpan.FromMinutes(2));
            while (!File.Exists(result)) await Task.Delay(100, probe.Token);
        }
        RealmProbeResult[] reads = JsonSerializer.Deserialize<RealmProbeResult[]>(await File.ReadAllTextAsync(result, cancellation)).ShouldNotBeNull();
        reads.Length.ShouldBe(3);
        reads[0].StatusCode.ShouldBe((int)HttpStatusCode.NotFound, reads[0].Detail);
        reads[0].Detail.ShouldBe("Event stream does not exist.");
        foreach (RealmProbeResult refusal in reads.Skip(1))
        {
            refusal.StatusCode.ShouldBeNull(refusal.Detail);
            refusal.Detail.ShouldBe("EventStore workload credentials are unavailable.");
        }

        using var browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        {
            BaseAddress = new Uri(authority + "/protocol/openid-connect/"),
        };
        string agent = await VerifyMcpCliAsync(browser, password, cancellation);
        using HttpClient publicGateway = app.CreateHttpClient("eventstore", "http");
        async Task<HttpStatusCode> ReadAsUser(string jwt)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/streams/read") { Content = JsonContent.Create(allowed) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            using HttpResponseMessage response = await publicGateway.SendAsync(request, cancellation);
            return response.StatusCode;
        }

        // Authentication succeeds for the agent, but it has no tenant grant for this human route.
        (await ReadAsUser(agent)).ShouldBe(HttpStatusCode.Forbidden);
        foreach (string oldIssuer in new[] { "hexalith-dev", authority })
        {
            string old = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = oldIssuer,
                Audience = "hexalith-eventstore",
                Claims = new Dictionary<string, object> { ["sub"] = _testUser, [RouteCatalogSurfacePolicy.ClaimType] = "agent" },
                Expires = DateTime.UtcNow.AddMinutes(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes("DevOnlySigningKey-AtLeast32Chars!")), SecurityAlgorithms.HmacSha256),
            });
            (await ReadAsUser(old)).ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    private static async Task VerifyWorksGrantsAsync(IWorkloadAssertionIssuer issuer, string tenant, CancellationToken cancellation)
    {
        issuer.CanBindResources.ShouldBeTrue();
        foreach (string operation in new[] { EventStoreWorkloadOperations.GatewayStreamRead,
            EventStoreWorkloadOperations.GatewayCommandSubmit, EventStoreWorkloadOperations.GatewayCommandStatus })
        {
            string jwt = (await issuer.IssueAsync(WorkloadRequest(operation, tenant, "work"), cancellation)).ShouldNotBeNull();
            var assertion = new JsonWebToken(jwt);
            assertion.Alg.ShouldBe("RS256");
            assertion.GetClaim("azp").Value.ShouldBe("works");
            assertion.GetClaim(RouteCatalogSurfacePolicy.ClaimType).Value.ShouldBe("service");
            assertion.GetClaim(EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType).Value.ShouldBe(tenant);
            assertion.GetClaim(EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType).Value.ShouldBe("work");
            assertion.GetClaim("eventstore:grant-id").Value.ShouldBe("works-bound-gateway");
            assertion.GetPayloadValue<int>("eventstore:grant-revision").ShouldBe(1);
            JwtWorkloadAssertionIssuer.GetTokenScopeFailure(jwt, "eventstore", operation).ShouldBeNull();
        }

        string effect = (await issuer.IssueAsync(new("eventstore", EventStoreWorkloadOperations.TrustedEffect), cancellation)).ShouldNotBeNull();
        JwtWorkloadAssertionIssuer.GetTokenScopeFailure(effect, "eventstore", EventStoreWorkloadOperations.TrustedEffect).ShouldBeNull();
        var effectAssertion = new JsonWebToken(effect);
        effectAssertion.GetClaim("eventstore:grant-id").Value.ShouldBe("works-trusted-effect");
        effectAssertion.GetPayloadValue<int>("eventstore:grant-revision").ShouldBe(1);
        effectAssertion.Claims.ShouldNotContain(claim => claim.Type.StartsWith(EventStoreWorkloadAuthenticationDefaults.BindingClaimPrefix, StringComparison.Ordinal));
        (await issuer.IssueAsync(WorkloadRequest(EventStoreWorkloadOperations.TrustedEffect, tenant, "work"), cancellation)).ShouldBeNull();
        (await issuer.IssueAsync(new("eventstore", EventStoreWorkloadOperations.GatewayStreamRead), cancellation)).ShouldBeNull();
        (await issuer.IssueAsync(WorkloadRequest(EventStoreWorkloadOperations.DomainServiceMetadata, tenant, "work"), cancellation)).ShouldBeNull();
        (await issuer.IssueAsync(WorkloadRequest(EventStoreWorkloadOperations.GatewayStreamRead, "other-realm-tenant", "work"), cancellation)).ShouldBeNull();
        (await issuer.IssueAsync(WorkloadRequest(EventStoreWorkloadOperations.GatewayStreamRead, tenant, "other-domain"), cancellation)).ShouldBeNull();
    }

    private static WorkloadAssertionRequest WorkloadRequest(string operation, string tenant, string domain) =>
        new("eventstore", operation, new Dictionary<string, string>
        {
            [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = tenant,
            [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = domain,
        });

    private static async Task RemoveOwnedContainerAsync(string name)
    {
        var found = await DockerAsync(["ps", "--all", "--quiet", "--filter", "name=^/" + name + "$"], null, CancellationToken.None);
        found.ExitCode.ShouldBe(0, found.ErrorOutput);
        if (string.IsNullOrWhiteSpace(found.Output)) return;
        var removed = await DockerAsync(["rm", "--force", found.Output.Trim()], null, CancellationToken.None);
        removed.ExitCode.ShouldBe(0, removed.ErrorOutput);
    }

    private static async Task<string> AcquireAccessTokenAsync(HttpClient client, Dictionary<string, string> fields, CancellationToken cancellation)
    {
        using HttpResponseMessage response = await PostAsync(client, "token", fields, cancellation);
        string body = await response.Content.ReadAsStringAsync(cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, response.IsSuccessStatusCode ? null : body);
        return JsonNode.Parse(body)!["access_token"]!.GetValue<string>();
    }

    private static async Task<JsonNode> RequireOAuthRefusalAsync(HttpClient client, string path, Dictionary<string, string> fields,
        string error, CancellationToken cancellation)
    {
        using HttpResponseMessage response = await PostAsync(client, path, fields, cancellation);
        response.IsSuccessStatusCode.ShouldBeFalse();
        JsonNode failure = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellation))!;
        failure["error"]!.GetValue<string>().ShouldBe(error, failure["error_description"]?.GetValue<string>());
        return failure;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, Dictionary<string, string> fields, CancellationToken cancellation)
    {
        using FormUrlEncodedContent content = new(fields);
        return await client.PostAsync(path, content, cancellation);
    }

    private static Task<CompositionProcessResult> DockerAsync(IEnumerable<string> arguments, IReadOnlyDictionary<string, string>? environment,
        CancellationToken cancellation) =>
        CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("docker", arguments, null, environment), TimeSpan.FromMinutes(2), cancellation);

    [GeneratedRegex("<form[^>]+id=\"kc-form-login\"[^>]+action=\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex LoginAction();
}
