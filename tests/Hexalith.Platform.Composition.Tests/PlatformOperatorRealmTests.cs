namespace Hexalith.Platform.Composition.Tests;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Routing;
using Shouldly;

/// <summary>Checks the instance-only local operator: realm rendering, single-grant tokens and the cutover token source.</summary>
public sealed class PlatformOperatorRealmTests
{
    private static readonly PlatformOperatorGrant[] Grants =
    [
        PlatformOperatorGrant.GlobalAdministrator,
        PlatformOperatorGrant.Permission("global-administrators:bootstrap"),
        PlatformOperatorGrant.Permission("global-administrators:read"),
        PlatformOperatorGrant.Permission("tenants:create"),
        PlatformOperatorGrant.Permission("tenants:read"),
    ];

    [Fact]
    public void GeneratedRealmRendersTheOperatorAsAConfidentialServiceAccountWithOneScopePerGrant()
    {
        PlatformModuleDeclaration[] declarations = CompleteDeclarations();
        var prepared = PlatformLocalRealm.PrepareDevelopment(declarations, PlatformLocalRealmTests.RealmUrl.Port, PlatformLocalRealmTests.SyntheticTenant, operatorGrants: Grants);
        JsonNode realm = JsonNode.Parse(prepared.Import)!;
        JsonNode client = realm["clients"]!.AsArray().Single(node => node!["clientId"]!.GetValue<string>() == PlatformRealmOperator.ClientId)!;

        client["publicClient"]!.GetValue<bool>().ShouldBeFalse();
        client["serviceAccountsEnabled"]!.GetValue<bool>().ShouldBeTrue();
        client["standardFlowEnabled"]!.GetValue<bool>().ShouldBeFalse();
        client["directAccessGrantsEnabled"]!.GetValue<bool>().ShouldBeFalse();
        client["implicitFlowEnabled"]!.GetValue<bool>().ShouldBeFalse();
        client["fullScopeAllowed"]!.GetValue<bool>().ShouldBeFalse();
        client["attributes"]!["access.token.signed.response.alg"]!.GetValue<string>().ShouldBe("RS256");
        client["attributes"]!["standard.token.exchange.enabled"]!.GetValue<string>().ShouldBe("false");
        client["defaultClientScopes"]!.AsArray().Select(scope => scope!.GetValue<string>()).ShouldBe(["basic"]);
        client["optionalClientScopes"]!.AsArray().Select(scope => scope!.GetValue<string>())
            .ShouldBe(Grants.Select(grant => grant.Scope).Order(StringComparer.Ordinal));
        client["secret"]!.GetValue<string>().ShouldBe("${" + PlatformLocalRealm.SecretEnvironmentName(PlatformRealmOperator.ClientId) + "}");
        JsonNode mapper = client["protocolMappers"]!.AsArray().ShouldHaveSingleItem()!;
        mapper["protocolMapper"]!.GetValue<string>().ShouldBe("oidc-audience-mapper");
        mapper["config"]!["included.custom.audience"]!.GetValue<string>().ShouldBe(PlatformRealmContract.GatewayUserAudience);

        // Each optional scope adds exactly one claim, so a token carries only the grants its request names.
        JsonArray scopes = realm["clientScopes"]!.AsArray();
        foreach (PlatformOperatorGrant grant in Grants)
        {
            JsonNode scope = scopes.Single(node => node!["name"]!.GetValue<string>() == grant.Scope)!;
            JsonNode claim = scope["protocolMappers"]!.AsArray().ShouldHaveSingleItem()!;
            claim["protocolMapper"]!.GetValue<string>().ShouldBe("oidc-hardcoded-claim-mapper");
            claim["config"]!["claim.name"]!.GetValue<string>().ShouldBe(grant.ClaimType);
            claim["config"]!["claim.value"]!.GetValue<string>().ShouldBe(grant.ClaimValue);
        }

        realm["users"]!.AsArray().ShouldContain(user => user!["serviceAccountClientId"]!.GetValue<string>() == PlatformRealmOperator.ClientId);
        string secret = prepared.Secret(PlatformRealmOperator.ClientId);
        secret.Length.ShouldBeGreaterThanOrEqualTo(32);
        prepared.Import.ShouldNotContain(secret);
        secret.ShouldNotBe(prepared.Secret("tenants"));

        // The operator exists only in this generated instance, never in the shared value-free realm contract.
        PlatformRealmContract.Load().Model.Clients.ShouldNotContain(contractClient => contractClient.Id == PlatformRealmOperator.ClientId);
        JsonNode.Parse(PlatformLocalRealm.PrepareDevelopment(declarations, PlatformLocalRealmTests.RealmUrl.Port, PlatformLocalRealmTests.SyntheticTenant).Import)!
            ["clients"]!.AsArray().ShouldNotContain(node => node!["clientId"]!.GetValue<string>() == PlatformRealmOperator.ClientId);
    }

    [Fact]
    public void OperatorGrantsComeFromTheCatalogDeclaredTenantsCommands()
    {
        RouteContractDeclaration[] declarations = [.. TenantCatalogDeclarations.Commands.Select(command => command.Route)];
        JsonElement schema = JsonSerializer.SerializeToElement(new { });
        RouteOperationContract[] operations = [.. declarations.Select(route => new RouteOperationContract(route.RouteId, "1", "internal", "eligible", schema, schema, new string('b', 64)))];
        var envelope = new RouteCatalogEnvelope(3, 1, "Development", false, declarations, [], [], "routes", "idempotency", operations);

        PlatformOperatorTaskBinding binding = PlatformOperatorTaskBinding.Resolve(envelope, "/catalog", new string('a', 64)).ShouldNotBeNull();

        binding.Bootstrap.ShouldBe(new PlatformOperatorOperation("global-administrators", BootstrapGlobalAdmin.CommandType,
            "platform:global-administrators", "global-administrators:bootstrap", "global-administrators:read"));
        binding.Create.ShouldBe(new PlatformOperatorOperation("tenants", CreateSyntheticTenant.CommandType, "platform:tenants", "tenants:create", "tenants:read"));
        binding.Grants.ShouldBe(Grants);
        binding.BoundDomains.ShouldBe(["global-administrators", "tenants"]);
        PlatformOperatorTaskBinding.Resolve(envelope with { Declarations = [.. declarations.Where(route => route.MessageType != CreateSyntheticTenant.CommandType)] },
            "/catalog", new string('a', 64)).ShouldBeNull();
        // Only a schema-3 catalog that declares both commands internal binds the operator.
        PlatformOperatorTaskBinding.Resolve(envelope with { SchemaVersion = 2 }, "/catalog", new string('a', 64)).ShouldBeNull();
        PlatformOperatorTaskBinding.Resolve(envelope with { Operations = [.. operations.Select(operation => operation with { Ingress = "public" })] },
            "/catalog", new string('a', 64)).ShouldBeNull();
        Should.Throw<InvalidDataException>(() => PlatformOperatorGrant.Permission("tenants:create tenants:read"));
    }

    [Fact]
    public async Task OperatorTokenRequestsOnlyTheNamedGrantsAndChecksTheIssuedClaimsAsync()
    {
        var handler = new OperatorTokenEndpointHandler(_ => OperatorTokenEndpointHandler.Issued(new
        {
            sub = "operator-subject", azp = PlatformRealmOperator.ClientId, aud = "hexalith-eventstore", role = "GlobalAdministrator",
        }), _ => OperatorTokenEndpointHandler.Issued(new Dictionary<string, object>
        {
            ["sub"] = "operator-subject", ["azp"] = PlatformRealmOperator.ClientId, ["role"] = "GlobalAdministrator", ["eventstore:permission"] = "tenants:create",
        }));
        var realmOperator = new PlatformRealmOperator(new Uri("http://127.0.0.1:1/realms/hexalith-local/protocol/openid-connect/token"), new string('s', 64), () => handler);

        string cutover = await new PlatformRealmOperatorTokenSource(realmOperator).CreateGlobalAdministratorTokenAsync(TestContext.Current.CancellationToken);
        string create = await realmOperator.TokenAsync([PlatformOperatorGrant.Permission("tenants:create"), PlatformOperatorGrant.GlobalAdministrator], TestContext.Current.CancellationToken);

        PlatformRealmOperator.Subject(cutover).ShouldBe("operator-subject");
        PlatformRealmOperator.Subject(create).ShouldBe("operator-subject");
        handler.Requests.Count.ShouldBe(2);
        handler.Requests[0]["grant_type"].ShouldBe("client_credentials");
        handler.Requests[0]["client_id"].ShouldBe(PlatformRealmOperator.ClientId);
        handler.Requests[0]["scope"].ShouldBe(PlatformOperatorGrant.GlobalAdministrator.Scope);
        handler.Requests[1]["scope"].ShouldBe("platform-operator.global-administrator platform-operator.tenants.create");
        new PlatformRealmOperatorTokenSource(realmOperator).OperatorName.ShouldBe(PlatformRealmOperator.ClientId);
    }

    [Theory]
    [InlineData("extra-permission")]
    [InlineData("missing-role")]
    [InlineData("foreign-client")]
    [InlineData("tenant-grant")]
    [InlineData("workload")]
    public void TokenWithOtherGrantsIsRefused(string variant)
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = "operator-subject", ["azp"] = PlatformRealmOperator.ClientId, ["role"] = "GlobalAdministrator", ["eventstore:permission"] = "tenants:create",
        };
        switch (variant)
        {
            case "extra-permission": claims["eventstore:permission"] = new[] { "tenants:create", "tenants:read" }; break;
            case "missing-role": claims.Remove("role"); break;
            case "foreign-client": claims["azp"] = "tenants"; break;
            case "tenant-grant": claims["eventstore:tenant"] = "other"; break;
            case "workload": claims["eventstore:operation"] = "eventstore:command-submit"; break;
        }

        Should.Throw<InvalidOperationException>(() => PlatformRealmOperator.RequireExactGrants(OperatorTokenEndpointHandler.Token(claims),
            [PlatformOperatorGrant.GlobalAdministrator, PlatformOperatorGrant.Permission("tenants:create")])).Message.ShouldContain(PlatformRealmOperator.ClientId);
    }

    [Fact]
    public async Task RefusedOperatorClientFailsNamedWithoutRetryingAsync()
    {
        var handler = new OperatorTokenEndpointHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var realmOperator = new PlatformRealmOperator(new Uri("http://127.0.0.1:1/token"), new string('s', 64), () => handler);

        var refusal = await Should.ThrowAsync<InvalidOperationException>(() => realmOperator.TokenAsync([PlatformOperatorGrant.GlobalAdministrator], TestContext.Current.CancellationToken));

        refusal.Message.ShouldContain("'platform-operator'");
        refusal.Message.ShouldContain("401");
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task UnavailableRealmIsRetriedOnlyWithinTheCallerBoundAsync()
    {
        var recovering = new OperatorTokenEndpointHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            _ => OperatorTokenEndpointHandler.Issued(new { sub = "operator-subject", azp = PlatformRealmOperator.ClientId, role = "GlobalAdministrator" }));
        string token = await new PlatformRealmOperator(new Uri("http://127.0.0.1:1/token"), new string('s', 64), () => recovering)
            .TokenAsync([PlatformOperatorGrant.GlobalAdministrator], TestContext.Current.CancellationToken);
        PlatformRealmOperator.Subject(token).ShouldBe("operator-subject");
        recovering.Requests.Count.ShouldBe(2);

        // Nothing listens on the closed port: the caller's deadline ends the wait, with no HS256 fallback.
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(2));
        var unreachable = new PlatformRealmOperator(new Uri("http://127.0.0.1:" + ClosedPort() + "/token"), new string('s', 64));
        await Should.ThrowAsync<OperationCanceledException>(() => unreachable.TokenAsync([PlatformOperatorGrant.GlobalAdministrator], bound.Token));
    }

    private static int ClosedPort()
    {
        using var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private static PlatformModuleDeclaration[] CompleteDeclarations()
    {
        string declarations = Path.Combine(PlatformTestRepository.Root, "src", "Hexalith.Platform.Composition", "Declarations");
        return [.. new[] { Path.Combine(declarations, "eventstore.json"), Path.Combine(declarations, "tenants.json"),
            Path.Combine(PlatformTestRepository.Root, PlatformDeclarationSet.CompleteManifest) }.Select(path =>
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(path));
            return new PlatformModuleDeclaration(path, document.RootElement.GetProperty("modules")[0].Clone());
        })];
    }
}
