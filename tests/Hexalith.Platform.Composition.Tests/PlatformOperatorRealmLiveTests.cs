namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Runtime;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

/// <summary>Opt-in Keycloak check of the generated realm's instance-only operator and the relay's realm workload assertion.</summary>
public sealed class PlatformOperatorRealmLiveTests
{
    [Fact]
    public async Task LiveGeneratedRealmIssuesSingleGrantOperatorTokensAndTheTenantsAssertionAsync()
    {
        if (Environment.GetEnvironmentVariable("HEXALITH_PLATFORM_LIVE_SMOKE") != "1")
        {
            Assert.Skip("Set HEXALITH_PLATFORM_LIVE_SMOKE=1 to start Keycloak 26.6.4.");
        }

        CancellationToken token = TestContext.Current.CancellationToken;
        int port = CompositionPortAllocator.Allocate(1).UiHttp;
        string workspace = Directory.CreateTempSubdirectory("platform-operator-realm-live-").FullName;
        string container = "platform-operator-realm-live-" + Guid.NewGuid().ToString("N");
        try
        {
            string declarations = Path.Combine(PlatformTestRepository.Root, "src", "Hexalith.Platform.Composition", "Declarations");
            PlatformModuleDeclaration[] selected = [.. new[] { Path.Combine(declarations, "eventstore.json"), Path.Combine(declarations, "tenants.json"),
                Path.Combine(PlatformTestRepository.Root, PlatformDeclarationSet.CompleteManifest) }.Select(path =>
            {
                using var document = JsonDocument.Parse(File.ReadAllBytes(path));
                return new PlatformModuleDeclaration(path, document.RootElement.GetProperty("modules")[0].Clone());
            })];
            var binding = new PlatformOperatorTaskBinding(workspace, new string('a', 64),
                new("global-administrators", "bootstrap-global-admin", "platform:global-administrators", "global-administrators:bootstrap", "global-administrators:read"),
                new("tenants", "create-synthetic-tenant", "platform:tenants", "tenants:create", "tenants:read"));
            var prepared = PlatformLocalRealm.PrepareDevelopment(selected, port, PlatformLocalRealmTests.SyntheticTenant, operatorGrants: binding.Grants);
            string import = Path.Combine(workspace, "hexalith-local-realm.json");
            File.WriteAllText(import, prepared.Import);
            PlatformLocalRealm.AllowContainerRead(import);
            string policy = Path.Combine(workspace, "platform-requester-audience.jar");
            PlatformLocalRealm.WriteAudiencePolicy(policy);
            var authority = new Uri($"http://127.0.0.1:{port}/realms/hexalith-local");
            Dictionary<string, string> environment = new(StringComparer.Ordinal) { ["KC_HOSTNAME"] = authority.GetLeftPart(UriPartial.Authority) };
            foreach (string client in JsonNode.Parse(prepared.Import)!["clients"]!.AsArray().Where(node => node!["publicClient"]!.GetValue<bool>() == false)
                .Select(node => node!["clientId"]!.GetValue<string>()))
                environment[PlatformLocalRealm.SecretEnvironmentName(client)] = prepared.Secret(client);
            List<string> arguments =
            [
                "run", "--detach", "--name", container, "--publish", $"127.0.0.1:{port}:8080",
                "--volume", $"{import}:{PlatformLocalRealm.ImportContainerDirectory}{Path.GetFileName(import)}:ro",
                "--volume", $"{policy}:{PlatformLocalRealm.PolicyContainerPath}:ro",
            ];
            foreach (string variable in environment.Keys) arguments.AddRange(["--env", variable]);
            arguments.Add(PlatformLocalRealm.Image);
            arguments.AddRange(PlatformLocalRealm.StartupArguments);
            CompositionProcessResult start = await DockerAsync(arguments, environment, token);
            start.ExitCode.ShouldBe(0, start.ErrorOutput);

            var tokenEndpoint = new Uri(authority.AbsoluteUri + "/protocol/openid-connect/token");
            var realmOperator = new PlatformRealmOperator(tokenEndpoint, prepared.Secret(PlatformRealmOperator.ClientId));
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(token);
            startup.CancelAfter(TimeSpan.FromMinutes(3));
            // The first request waits for the realm import like the cutover does; every token carries only its requested grants.
            string cutover = await new PlatformRealmOperatorTokenSource(realmOperator).CreateGlobalAdministratorTokenAsync(startup.Token);
            JsonWebKeySet keys = new(await new HttpClient().GetStringAsync(authority.AbsoluteUri + "/protocol/openid-connect/certs", token));
            string subject = await RequireRealmSignedAsync(cutover, authority, keys);
            foreach (PlatformOperatorGrant grant in binding.Grants.Where(grant => !grant.IsGlobalAdministrator))
                (await RequireRealmSignedAsync(await realmOperator.TokenAsync([grant], token), authority, keys)).ShouldBe(subject);
            string create = await realmOperator.TokenAsync([PlatformOperatorGrant.GlobalAdministrator, PlatformOperatorGrant.Permission("tenants:create")], token);
            (await RequireRealmSignedAsync(create, authority, keys)).ShouldBe(subject);

            // A refused operator fails at once and names the client; it never degrades to another credential.
            var refused = new PlatformRealmOperator(tokenEndpoint, new string('0', 64));
            (await Should.ThrowAsync<InvalidOperationException>(() => refused.TokenAsync([PlatformOperatorGrant.GlobalAdministrator], token)))
                .Message.ShouldContain("'" + PlatformRealmOperator.ClientId + "'");

            // The relay's realm workload assertion is the Tenants client's exact audience and command-submit operation.
            var credentials = new PlatformRealmOperatorCredentials(realmOperator, authority.AbsoluteUri, "tenants", prepared.Secret("tenants"), PlatformRealmContract.GatewayUserAudience);
            string? assertion = await credentials.WorkloadAssertionAsync(new HostingEnvironment { EnvironmentName = Environments.Development, ApplicationName = "operator-realm-live" },
                "eventstore", "tenants", token);
            assertion.ShouldNotBeNull();
            JwtWorkloadAssertionIssuer.GetTokenScopeFailure(assertion, "eventstore", "eventstore:command-submit").ShouldBeNull();
            var workload = new JsonWebToken(assertion);
            workload.GetClaim("azp").Value.ShouldBe("tenants");
            workload.GetClaim("eventstore:surface").Value.ShouldBe("service");
        }
        finally
        {
            await DockerAsync(["rm", "--force", container], null, CancellationToken.None);
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static async Task<string> RequireRealmSignedAsync(string token, Uri authority, JsonWebKeySet keys)
    {
        TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = authority.AbsoluteUri,
            ValidAudience = PlatformRealmContract.GatewayUserAudience,
            IssuerSigningKeys = keys.GetSigningKeys(),
            ValidAlgorithms = ["RS256"],
        });
        result.IsValid.ShouldBeTrue(result.Exception?.Message);
        var parsed = new JsonWebToken(token);
        parsed.GetClaim("azp").Value.ShouldBe(PlatformRealmOperator.ClientId);
        parsed.TryGetClaim("eventstore:tenant", out _).ShouldBeFalse();
        parsed.TryGetClaim("eventstore:workload", out _).ShouldBeFalse();
        return parsed.Subject;
    }

    private static Task<CompositionProcessResult> DockerAsync(IEnumerable<string> arguments, IReadOnlyDictionary<string, string>? environment, CancellationToken cancellation) =>
        CompositionProcess.RunAsync(CompositionProcess.CreateStartInfo("docker", arguments, null, environment), TimeSpan.FromMinutes(2), cancellation);
}
