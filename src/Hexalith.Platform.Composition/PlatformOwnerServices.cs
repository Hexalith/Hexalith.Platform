namespace Hexalith.Platform.Composition;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.EventStore.Aspire;

/// <summary>Enrolls dedicated owner REST clients without composing a Platform topology or granting SDK callers.</summary>
/// <remarks>Operation scopes and permission mappers describe the consumer's request/cache contract. Receivers keep
/// their native authorization: Memories enforces authenticated tenant membership, not these operation/permission
/// claims. Enrollment does not add a per-operation Memories authorization scheme or provision owner records.
/// Tokens carry <c>tenant_id</c> but never <c>eventstore:tenant</c>: an owner audience can also be accepted by EventStore,
/// whose tenant-scoped endpoints authorize on that claim alone. EventStore derives it from <c>tenant_id</c> only for
/// tokens without <c>eventstore:*</c> claims, and every owner token carries <c>eventstore:permission</c>.</remarks>
public static class PlatformOwnerServices
{
    /// <summary>Gets the dedicated audience scope prefix, separate from SDK workload scopes.</summary>
    public const string AudienceScopePrefix = "platform-owner-audience.";

    /// <summary>Gets the dedicated operation scope prefix, separate from SDK workload scopes.</summary>
    public const string OperationScopePrefix = "platform-owner-operation.";

    /// <summary>Creates a per-run client secret and an unpublished secret Aspire parameter.</summary>
    /// <param name="builder">The retained application builder.</param>
    /// <param name="clientId">A dedicated owner client identifier.</param>
    /// <param name="audience">The exact receiving audience.</param>
    /// <param name="operation">The exact token request/cache operation; receiver enforcement remains owner-defined.</param>
    /// <param name="tenantId">The single enrolled tenant.</param>
    /// <param name="permission">The permission claim; only receivers that natively consume it enforce it.</param>
    /// <returns>The identity and its secret parameter.</returns>
    public static PlatformOwnerClientBinding AddPlatformOwnerClient(this IDistributedApplicationBuilder builder,
        string clientId, string audience, string operation, string tenantId, string permission)
    {
        ArgumentNullException.ThrowIfNull(builder);
        foreach (string value in new[] { clientId, audience, operation, tenantId, permission })
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            if (value.Any(char.IsWhiteSpace)) throw new ArgumentException("Owner enrollment values must be single tokens.");
        }
        string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        IResourceBuilder<ParameterResource> parameter = builder.AddParameter(clientId + "-secret", () => secret,
            publishValueAsDefault: false, secret: true);
        return new(clientId, audience, operation, tenantId, permission, secret, parameter);
    }

    /// <summary>Appends only dedicated clients and scopes to an already validated rendered realm.</summary>
    /// <param name="realm">The existing realm JSON after its placeholder checks.</param>
    /// <param name="bindings">The separate owner identities.</param>
    /// <returns>The rendered realm with additive enrollments.</returns>
    public static string AppendOwnerClientsToRealm(string realm, params PlatformOwnerClientBinding[] bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        JsonObject root = JsonNode.Parse(realm)?.AsObject() ?? throw new ArgumentException("A realm object is required.", nameof(realm));
        JsonArray clients = root["clients"]?.AsArray() ?? throw new ArgumentException("Realm clients are required.", nameof(realm));
        JsonArray scopes = root["clientScopes"]?.AsArray() ?? throw new ArgumentException("Realm client scopes are required.", nameof(realm));
        HashSet<string> identities = clients.Select(client => client!["clientId"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        HashSet<string> names = scopes.Select(scope => scope!["name"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        foreach (PlatformOwnerClientBinding binding in bindings)
        {
            if (!identities.Add(binding.ClientId)) throw new ArgumentException("Owner client identities must be distinct and new.", nameof(bindings));
            string audienceScope = AudienceScopePrefix + binding.Audience;
            string operationScope = OperationScopePrefix + binding.Operation.Replace(':', '.');
            if (!names.Add(audienceScope) || !names.Add(operationScope))
                throw new ArgumentException("Owner scopes must be distinct and new.", nameof(bindings));
            scopes.Add(Scope(audienceScope, Mapper("owner-audience", "oidc-audience-mapper", new()
            {
                ["included.custom.audience"] = binding.Audience,
                ["access.token.claim"] = "true",
                ["id.token.claim"] = "false",
            })));
            scopes.Add(Scope(operationScope, Claim("eventstore:operation", binding.Operation)));
            clients.Add(new JsonObject
            {
                ["clientId"] = binding.ClientId,
                ["protocol"] = "openid-connect",
                ["enabled"] = true,
                ["publicClient"] = false,
                ["clientAuthenticatorType"] = "client-secret",
                ["secret"] = binding.Secret,
                ["serviceAccountsEnabled"] = true,
                ["standardFlowEnabled"] = false,
                ["directAccessGrantsEnabled"] = false,
                ["fullScopeAllowed"] = false,
                ["defaultClientScopes"] = new JsonArray(),
                ["optionalClientScopes"] = new JsonArray(audienceScope, operationScope),
                ["protocolMappers"] = new JsonArray(
                    Mapper("owner-subject", "oidc-sub-mapper", new() { ["access.token.claim"] = "true" }),
                    Claim("tenant_id", binding.TenantId),
                    Claim("eventstore:permission", binding.Permission)),
            });
        }
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Binds one dedicated identity and its existing owner endpoint to a consumer.</summary>
    /// <param name="resource">The consuming resource.</param>
    /// <param name="section">The consumer's configuration section.</param>
    /// <param name="ownerEndpoint">The existing owner resource endpoint.</param>
    /// <param name="security">The existing issuer resource.</param>
    /// <param name="binding">The dedicated identity.</param>
    /// <returns>The consuming resource.</returns>
    public static IResourceBuilder<ProjectResource> WithPlatformOwnerService(this IResourceBuilder<ProjectResource> resource, string section,
        EndpointReference ownerEndpoint, HexalithEventStoreSecurityResources security, PlatformOwnerClientBinding binding)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(ownerEndpoint);
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        string prefix = section.Replace(":", "__", StringComparison.Ordinal);
        return resource.WithSecurityDependency(security)
            .WithEnvironment(prefix + "__Endpoint", ownerEndpoint)
            .WithEnvironment(prefix + "__OwnerAuthentication__Authority", security.RealmUrl)
            .WithEnvironment(prefix + "__OwnerAuthentication__Issuer", security.RealmUrl)
            .WithEnvironment(prefix + "__OwnerAuthentication__TokenEndpoint", ReferenceExpression.Create($"{security.RealmUrl}/protocol/openid-connect/token"))
            .WithEnvironment(prefix + "__OwnerAuthentication__ClientId", binding.ClientId)
            .WithEnvironment(prefix + "__OwnerAuthentication__ClientSecret", binding.SecretParameter)
            .WithEnvironment(prefix + "__OwnerAuthentication__TenantId", binding.TenantId)
            .WithEnvironment(prefix + "__OwnerAuthentication__Audience", binding.Audience)
            .WithEnvironment(prefix + "__OwnerAuthentication__Operation", binding.Operation);
    }

    private static JsonObject Scope(string name, JsonObject mapper) => new()
    {
        ["name"] = name, ["protocol"] = "openid-connect",
        ["attributes"] = new JsonObject { ["include.in.token.scope"] = "true" },
        ["protocolMappers"] = new JsonArray(mapper),
    };

    private static JsonObject Claim(string name, string value) => Mapper(name, "oidc-hardcoded-claim-mapper", new()
    {
        ["claim.name"] = name, ["claim.value"] = value, ["jsonType.label"] = "String",
        ["access.token.claim"] = "true", ["id.token.claim"] = "false", ["userinfo.token.claim"] = "false",
    });

    private static JsonObject Mapper(string name, string type, JsonObject configuration) => new()
    {
        ["name"] = name, ["protocol"] = "openid-connect", ["protocolMapper"] = type,
        ["consentRequired"] = false, ["config"] = configuration,
    };
}
