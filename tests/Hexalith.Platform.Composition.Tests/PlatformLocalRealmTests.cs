namespace Hexalith.Platform.Composition.Tests;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Shouldly;

public sealed class PlatformLocalRealmTests
{
    internal static readonly Uri RealmUrl = new("http://127.0.0.1:19273/realms/hexalith-local");
    internal const string SyntheticTenant = "realm-test-tenant";

    [Fact]
    public void GeneratesTheSameImportFromTheSameInputsAndUsesOnlySecretPlaceholders()
    {
        var contract = PlatformRealmContract.Load();
        PlatformModuleDeclaration[] declarations = PreviewDeclarations();
        Dictionary<string, string> secrets = ClientSecrets();
        string first = PlatformLocalRealm.Render(contract, declarations, RealmUrl, SyntheticTenant, secrets);
        string second = PlatformLocalRealm.Render(contract, declarations.Reverse(), RealmUrl, SyntheticTenant,
            secrets.Reverse().ToDictionary(StringComparer.Ordinal));

        second.ShouldBe(first);
        foreach ((string client, string secret) in secrets)
        {
            first.ShouldNotContain(secret);
            first.ShouldContain("${" + PlatformLocalRealm.SecretEnvironmentName(client) + "}");
        }
        first.ShouldNotContain(PlatformTestRepository.Root);
        first.ShouldNotContain("timestamp");

        JsonObject realm = JsonNode.Parse(first)!.AsObject();
        realm["realm"]!.GetValue<string>().ShouldBe("hexalith-local");
        JsonObject attributes = realm["attributes"]!.AsObject();
        attributes["frontendUrl"]!.GetValue<string>().ShouldBe(RealmUrl.GetLeftPart(UriPartial.Authority));
        attributes["syntheticTenantId"]!.GetValue<string>().ShouldBe(SyntheticTenant);
        attributes["platform.surfaceClasses"]!.GetValue<string>().Split(',').ShouldBe(["agent", "service", "ui"]);
        realm["groups"]!.AsArray().Select(group => group!["name"]!.GetValue<string>())
            .ShouldBe(["hexalith-admission", "hexalith-synthetic-admission"]);
        realm["users"]!.AsArray().ShouldAllBe(user => user!["serviceAccountClientId"] != null && user["groups"] == null);
        realm["revokeRefreshToken"]!.GetValue<bool>().ShouldBeTrue();
        realm["refreshTokenMaxReuse"]!.GetValue<int>().ShouldBe(0);
        realm["ssoSessionIdleTimeout"]!.GetValue<int>().ShouldBe(1800);
        realm["ssoSessionMaxLifespan"]!.GetValue<int>().ShouldBe(36000);
        realm["eventsEnabled"]!.GetValue<bool>().ShouldBeTrue();
        realm["adminEventsEnabled"]!.GetValue<bool>().ShouldBeTrue();
        realm["adminEventsDetailsEnabled"]!.GetValue<bool>().ShouldBeTrue();
        realm["clientProfiles"]!["profiles"]![0]!["executors"]!.AsArray()
            .Select(executor => executor!["executor"]!.GetValue<string>())
            .ShouldBe(["platform-require-requester-audience", "downscope-assertion-grant-enforcer", "hexalith-workload-binding-v1"]);
        realm["clientPolicies"]!["policies"]![0]!["enabled"]!.GetValue<bool>().ShouldBeTrue();
        realm["clientScopeMappings"]!["works"]![0]!["roles"]![0]!.GetValue<string>().ShouldBe("invoker");

        JsonArray clients = realm["clients"]!.AsArray();
        clients.Select(client => client!["clientId"]!.GetValue<string>())
            .ShouldBe(["eventstore", "eventstore-admin", "mcpcli", "works"]);
        foreach (JsonNode client in clients.OfType<JsonNode>())
        {
            client["fullScopeAllowed"]!.GetValue<bool>().ShouldBeFalse();
            client["directAccessGrantsEnabled"]!.GetValue<bool>().ShouldBeFalse();
            client["implicitFlowEnabled"]!.GetValue<bool>().ShouldBeFalse();
            client["attributes"]!["oauth2.device.authorization.grant.enabled"]!.GetValue<string>().ShouldBe("false");
            client["attributes"]!["standard.token.exchange.enableRefreshRequestedTokenType"]!.GetValue<string>().ShouldBe("NO");
            JsonNode mapper = client["protocolMappers"]!.AsArray().OfType<JsonNode>()
                .Single(value => value["config"]!["claim.name"]?.GetValue<string>() == RouteCatalogSurfacePolicy.ClaimType);
            mapper["config"]!["claim.value"]!.GetValue<string>()
                .ShouldBe(client["clientId"]!.GetValue<string>() == "mcpcli" ? "agent" : "service");
        }

        JsonNode mcp = clients.Single(client => client!["clientId"]!.GetValue<string>() == "mcpcli")!;
        mcp["publicClient"]!.GetValue<bool>().ShouldBeTrue();
        mcp["standardFlowEnabled"]!.GetValue<bool>().ShouldBeTrue();
        mcp["serviceAccountsEnabled"]!.GetValue<bool>().ShouldBeFalse();
        mcp["optionalClientScopes"]!.AsArray().ShouldBeEmpty();
        mcp["secret"].ShouldBeNull();
        mcp["attributes"]!["pkce.code.challenge.method"]!.GetValue<string>().ShouldBe("S256");
        mcp["redirectUris"]![0]!.GetValue<string>().ShouldBe("http://127.0.0.1/*");
        mcp["protocolMappers"]!.AsArray().Count.ShouldBe(2);
        mcp["protocolMappers"]![1]!["config"]!["included.custom.audience"]!.GetValue<string>().ShouldBe("hexalith-eventstore");
        JsonNode works = clients.Single(client => client!["clientId"]!.GetValue<string>() == "works")!;
        works["attributes"]!["standard.token.exchange.enabled"]!.GetValue<string>().ShouldBe("false");
        works["attributes"]!["hexalith.workload-binding.grants"]!.GetValue<string>().ShouldBe("${HEXALITH_LOCAL_REALM_BINDINGS_WORKS}");
        JsonNode gateway = clients.Single(client => client!["clientId"]!.GetValue<string>() == "eventstore")!;
        gateway["attributes"]!["standard.token.exchange.enabled"]!.GetValue<string>().ShouldBe("true");
        gateway["attributes"]!["hexalith.workload-binding.grants"].ShouldBeNull();
        JsonNode admin = clients.Single(client => client!["clientId"]!.GetValue<string>() == "eventstore-admin")!;
        admin["attributes"]!["standard.token.exchange.enabled"]!.GetValue<string>().ShouldBe("false");
    }

    [Theory]
    [InlineData("scopes", "[\"offline_access\"]", "offline_access")]
    [InlineData("fullScopeAllowed", "true", "fullScopeAllowed=false")]
    [InlineData("classes", "[]", "exactly one class")]
    [InlineData("classes", "[\"agent\",\"ui\"]", "exactly one class")]
    [InlineData("classes", "[\"unknown\"]", "unknown class")]
    [InlineData("classes", "[\"Invalid Class\"]", "unknown class")]
    [InlineData("claims", "[\"eventstore:surface\",\"eventstore:unknown\"]", "unknown claim")]
    [InlineData("claims", "[\"Invalid Claim\"]", "unknown claim")]
    [InlineData("audiences", "[\"realm-management\"]", "admin audience")]
    [InlineData("audiences", "[\"eventstore-admin\"]", "admin audience")]
    [InlineData("audiences", "[\"eventstore/admin\"]", "admin audience")]
    public void RejectsInvalidClientContractsByClientAndRule(string property, string replacement, string rule)
    {
        string path = Path.Combine(PlatformTestRepository.Root, "src", "Hexalith.Platform.Composition", "Declarations", "realm-contract.v1.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        JsonNode mcp = json["clients"]!.AsArray().Single(node => node!["id"]!.GetValue<string>() == "mcpcli")!;
        mcp[property] = JsonNode.Parse(replacement);
        InvalidDataException error = Should.Throw<InvalidDataException>(() => PlatformRealmContract.Parse(json.ToJsonString()));
        error.Message.ShouldContain("mcpcli");
        error.Message.ShouldContain(rule);
    }

    [Theory]
    [InlineData("eventstore", "[\"offline_access\"]")]
    [InlineData("works", "[\"offline_access\"]")]
    [InlineData("mcpcli", "[\"profile\"]")]
    public void RejectsOptionalScopesThatV1DoesNotDefine(string client, string scopes)
    {
        JsonNode contract = ReadContract();
        contract["clients"]!.AsArray().Single(node => node!["id"]!.GetValue<string>() == client)!["scopes"] = JsonNode.Parse(scopes);
        InvalidDataException error = Should.Throw<InvalidDataException>(() => PlatformRealmContract.Parse(contract.ToJsonString()));
        error.Message.ShouldContain("'" + client + "'");
        error.Message.ShouldContain("optional scope");
    }

    [Theory]
    [InlineData("tokenExchange", "true")]
    [InlineData("audiences", "[\"eventstore\",\"works\"]")]
    [InlineData("boundOperations", "[\"eventstore:command-status\",\"eventstore:command-submit\"]")]
    [InlineData("boundOperations", "[\"eventstore:command-status\",\"eventstore:command-submit\",\"eventstore:stream-read\",\"eventstore:trusted-effect\"]")]
    [InlineData("boundOperations", null)]
    [InlineData("operations", "[\"eventstore:command-status\",\"eventstore:command-submit\",\"eventstore:stream-read\"]")]
    [InlineData("operations", "[\"eventstore:command-status\",\"eventstore:command-submit\",\"eventstore:stream-read\",\"eventstore:trusted-effect\",\"domain-service:process\"]")]
    public void RejectsWorksClientBeyondItsBoundGatewayGrant(string property, string? replacement)
    {
        JsonNode contract = ReadContract();
        JsonObject works = contract["clients"]!.AsArray().Single(node => node!["id"]!.GetValue<string>() == "works")!.AsObject();
        if (replacement is null)
        {
            works.Remove(property).ShouldBeTrue();
        }
        else
        {
            works[property] = JsonNode.Parse(replacement);
        }

        InvalidDataException error = Should.Throw<InvalidDataException>(() => PlatformRealmContract.Parse(contract.ToJsonString()));
        error.Message.ShouldContain("'works'");
        error.Message.ShouldContain("only the three bound gateway operations and unbound trusted-effect, with token exchange disabled");
    }

    [Theory]
    [InlineData("Retained-Tenant")]
    [InlineData("system")]
    [InlineData("SYSTEM")]
    [InlineData("-leading-hyphen")]
    [InlineData("tenant_underscore")]
    [InlineData("")]
    public void RejectsNonCanonicalOrReservedSyntheticTenant(string tenant)
    {
        Should.Throw<ArgumentException>(() => PlatformLocalRealm.Render(PlatformRealmContract.Load(), PreviewDeclarations(),
            RealmUrl, tenant, ClientSecrets())).ParamName.ShouldBe("syntheticTenantId");
        Should.Throw<ArgumentException>(() => PlatformLocalRealm.PrepareWorksPreview(PreviewDeclarations()[0].Source, RealmUrl.Port, tenant))
            .ParamName.ShouldBe("syntheticTenantId");
    }

    [Theory]
    [InlineData("account")]
    [InlineData("account-console")]
    [InlineData("admin-cli")]
    [InlineData("broker")]
    [InlineData("realm-management")]
    [InlineData("security-admin-console")]
    public void RejectsBuiltInKeycloakClientIds(string reserved)
    {
        JsonNode contract = ReadContract();
        contract["clients"]![0]!["id"] = reserved;
        InvalidDataException error = Should.Throw<InvalidDataException>(() => PlatformRealmContract.Parse(contract.ToJsonString()));
        error.Message.ShouldContain(reserved);
        error.Message.ShouldContain("reserved Keycloak client id");
    }

    [Fact]
    public void RejectsRefreshTokenReuseThroughTheSchema()
    {
        JsonNode contract = ReadContract();
        contract["refresh"]!["maxReuse"] = 1;

        Should.Throw<InvalidDataException>(() => PlatformRealmContract.Parse(contract.ToJsonString()))
            .Message.ShouldContain("does not satisfy the v1 schema");
    }

    [Theory]
    [InlineData("unknown", "unknown service audience")]
    [InlineData("eventstore", "declared audience")]
    public void RejectsUndeclaredServiceAudience(string audience, string rule)
    {
        JsonNode contract = ReadContract();
        JsonNode admin = contract["clients"]!.AsArray().Single(node => node!["id"]!.GetValue<string>() == "eventstore-admin")!;
        admin["audiences"]!.AsArray().Add(audience);
        InvalidDataException error = Should.Throw<InvalidDataException>(() => PlatformLocalRealm.Render(
            PlatformRealmContract.Parse(contract.ToJsonString()), PreviewDeclarations(), RealmUrl, SyntheticTenant, ClientSecrets()));
        error.Message.ShouldContain("eventstore-admin");
        error.Message.ShouldContain(rule);
    }

    [Fact]
    public void OnlySelectsServiceClientsWithDeclaredApplicationPrincipals()
    {
        PlatformModuleDeclaration[] declarations = PreviewDeclarations();
        string rendered = PlatformLocalRealm.Render(PlatformRealmContract.Load(), [declarations[0]], RealmUrl, SyntheticTenant,
            new Dictionary<string, string> { ["eventstore"] = ClientSecrets()["eventstore"] });
        JsonArray clients = JsonNode.Parse(rendered)!["clients"]!.AsArray();
        clients.Select(client => client!["clientId"]!.GetValue<string>()).ShouldBe(["eventstore", "mcpcli"]);

        var changed = JsonNode.Parse(declarations[0].Declaration.GetRawText())!;
        changed["integration"]!["identityNeeds"]![0]!["audience"] = "different-audience";
        var incompatible = new PlatformModuleDeclaration(declarations[0].Source, JsonSerializer.SerializeToElement(changed));
        Should.Throw<InvalidDataException>(() => PlatformLocalRealm.Render(PlatformRealmContract.Load(), [incompatible], RealmUrl, SyntheticTenant,
            new Dictionary<string, string> { ["eventstore"] = ClientSecrets()["eventstore"] })).Message.ShouldContain("eventstore");
    }

    [Fact]
    public void RejectsDirectPrincipalWhoseReceiverIsOutsideTheSelection()
    {
        PlatformModuleDeclaration[] declarations = PreviewDeclarations();
        JsonNode preview = JsonNode.Parse(declarations[1].Declaration.GetRawText())!;
        JsonArray principals = preview["integration"]!["identityNeeds"]!.AsArray();
        principals.Remove(principals.Single(principal => principal!["name"]!.GetValue<string>() == "works-workload"));
        var adminOnly = new PlatformModuleDeclaration(declarations[1].Source, JsonSerializer.SerializeToElement(preview));
        var secrets = ClientSecrets();
        secrets.Remove("works");

        Should.Throw<InvalidDataException>(() => PlatformLocalRealm.Render(PlatformRealmContract.Load(),
            [declarations[0], adminOnly], RealmUrl, SyntheticTenant, secrets))
            .Message.ShouldContain("Realm client 'eventstore-admin' requires selected audience 'works'");
    }

    [Fact]
    public void BothWorkloadGrantsAndTheSignerHaveBoundedDeadlines()
    {
        var now = new DateTimeOffset(2026, 11, 24, 12, 30, 0, TimeSpan.Zero);
        var prepared = PlatformLocalRealm.PrepareWorksPreview(PreviewDeclarations()[0].Source, RealmUrl.Port, SyntheticTenant);
        IReadOnlyDictionary<string, string> environment = prepared.WorkloadBindingEnvironment(now);
        string encodedGrants = environment["HEXALITH_LOCAL_REALM_BINDINGS_WORKS"];
        using JsonDocument grants = JsonDocument.Parse(JsonSerializer.Deserialize<string>('"' + encodedGrants + '"').ShouldNotBeNull());

        grants.RootElement.EnumerateArray().Select(grant => grant.GetProperty("grantId").GetString())
            .ShouldBe(["works-bound-gateway", "works-trusted-effect"]);
        foreach (JsonElement grant in grants.RootElement.EnumerateArray())
        {
            grant.GetProperty("notBefore").GetInt64().ShouldBe(now.AddMinutes(-1).ToUnixTimeSeconds());
            grant.GetProperty("expiresAt").GetInt64().ShouldBe(now.AddDays(1).ToUnixTimeSeconds());
            grant.GetProperty("clientId").GetString().ShouldBe("works");
            grant.GetProperty("workload").GetString().ShouldBe("works");
            grant.GetProperty("audience").GetString().ShouldBe("eventstore");
            grant.GetProperty("revision").GetInt32().ShouldBe(1);
            grant.GetProperty("allowQueryAuthorityDigest").GetBoolean().ShouldBeFalse();
            grant.GetProperty("revoked").GetBoolean().ShouldBeFalse();
        }

        // The tenant-bound grant carries exactly the three gateway operations; trusted-effect stays unbound and separate.
        JsonElement bound = grants.RootElement[0];
        bound.GetProperty("operations").EnumerateArray().Select(operation => operation.GetString())
            .ShouldBe(["eventstore:command-status", "eventstore:command-submit", "eventstore:stream-read"]);
        bound.GetProperty("allowUnbound").GetBoolean().ShouldBeFalse();
        bound.GetProperty("bindings").EnumerateObject().Select(binding => binding.Name)
            .ShouldBe(["eventstore:bound-tenant", "eventstore:bound-domain"]);
        bound.GetProperty("bindings").GetProperty("eventstore:bound-tenant").EnumerateArray().Select(value => value.GetString())
            .ShouldBe([SyntheticTenant]);
        bound.GetProperty("bindings").GetProperty("eventstore:bound-domain").EnumerateArray().Select(value => value.GetString())
            .ShouldBe(["work"]);
        JsonElement trustedEffect = grants.RootElement[1];
        trustedEffect.GetProperty("operations").EnumerateArray().Select(operation => operation.GetString())
            .ShouldBe(["eventstore:trusted-effect"]);
        trustedEffect.GetProperty("allowUnbound").GetBoolean().ShouldBeTrue();
        trustedEffect.GetProperty("bindings").EnumerateObject().ShouldBeEmpty();
        long.Parse(environment["HEXALITH_LOCAL_REALM_BINDING_SIGNER_END"], CultureInfo.InvariantCulture)
            .ShouldBe(now.AddDays(1).ToUnixTimeSeconds());
    }

    [Fact]
    public void RejectsMissingOrExtraSecretBindings()
    {
        var contract = PlatformRealmContract.Load();
        PlatformModuleDeclaration[] declarations = PreviewDeclarations();
        Dictionary<string, string> secrets = ClientSecrets();
        secrets.Remove("works").ShouldBeTrue();
        Should.Throw<ArgumentException>(() => PlatformLocalRealm.Render(contract, declarations, RealmUrl, SyntheticTenant, secrets))
            .Message.ShouldContain("works");
        secrets = ClientSecrets();
        secrets.Add("mcpcli", new string('D', 32));
        Should.Throw<ArgumentException>(() => PlatformLocalRealm.Render(contract, declarations, RealmUrl, SyntheticTenant, secrets));
    }

    internal static PlatformModuleDeclaration[] PreviewDeclarations()
    {
        string path = Path.Combine(PlatformTestRepository.Root, "src", "Hexalith.Platform.Composition", "Declarations", "eventstore.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return [
            new(path, document.RootElement.GetProperty("modules")[0].Clone()),
            new("works-preview-principals.json", PlatformRealmContract.ReadWorksPreviewPrincipals()),
        ];
    }

    internal static Dictionary<string, string> ClientSecrets() => new(StringComparer.Ordinal)
    {
        ["eventstore"] = new string('A', 48),
        ["eventstore-admin"] = new string('B', 48),
        ["works"] = new string('C', 48),
    };

    private static JsonNode ReadContract() => JsonNode.Parse(File.ReadAllText(Path.Combine(PlatformTestRepository.Root,
        "src", "Hexalith.Platform.Composition", "Declarations", "realm-contract.v1.json")))!;
}
