namespace Hexalith.Platform.Composition;

using System.Reflection;
using System.Text.Json;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Json.Schema;

/// <summary>The validated, value-free contract for the Platform local realm.</summary>
internal sealed class PlatformRealmContract
{
    private static readonly JsonSchema Schema = ReadSchema("PlatformRealmContractSchema");
    private static readonly Lazy<JsonSchema> ModuleManifestSchema = new(() => ReadSchema(
        "Hexalith.Builds.Tooling.Manifest.hexalith.module-manifest.v2.json", typeof(PlatformManifestValidator).Assembly));

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> EventStoreClaims = new(StringComparer.Ordinal)
    {
        RouteCatalogSurfacePolicy.ClaimType,
        "eventstore:tenant", "eventstore:domain", "eventstore:permission",
        EventStoreWorkloadAuthenticationDefaults.CallerClaimType,
        EventStoreWorkloadAuthenticationDefaults.ChannelClaimType,
        EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType,
        EventStoreWorkloadAuthenticationDefaults.OperationClaimType,
        EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType,
        EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType,
        "eventstore:grant-id", "eventstore:grant-revision",
    };

    private static readonly HashSet<string> ReservedClientIds = new(StringComparer.Ordinal)
    {
        "account", "account-console", "admin-cli", "broker", "realm-management", "security-admin-console",
    };

    private static readonly HashSet<string> WorksBoundOperations = new(StringComparer.Ordinal)
    {
        EventStoreWorkloadOperations.GatewayCommandStatus,
        EventStoreWorkloadOperations.GatewayCommandSubmit,
        EventStoreWorkloadOperations.GatewayStreamRead,
    };

    private static readonly HashSet<string> WorkloadOperations = new(StringComparer.Ordinal)
    {
        EventStoreWorkloadOperations.DomainServiceMetadata, EventStoreWorkloadOperations.DomainServiceProcess,
        EventStoreWorkloadOperations.DomainServiceProject, EventStoreWorkloadOperations.DomainServiceQuery,
        EventStoreWorkloadOperations.DomainServiceReplayState, EventStoreWorkloadOperations.ProjectionNotify,
        EventStoreWorkloadOperations.GatewayCommandStatus, EventStoreWorkloadOperations.GatewayCommandSubmit,
        EventStoreWorkloadOperations.GatewayStreamRead,
        EventStoreWorkloadOperations.TrustedEffect, EventStoreWorkloadOperations.RouteCatalogRead,
        EventStoreWorkloadOperations.GatewayOwnerAuthorityQuery, "eventstore:stream-scan-native",
    };

    internal const string ConversationsReader = "conversations-tenant-access";
    internal const string GatewayUserAudience = "hexalith-eventstore";

    private PlatformRealmContract(Declaration declaration) => Model = declaration;

    internal Declaration Model { get; }

    /// <summary>Loads the versioned Platform declaration embedded with the realm schema.</summary>
    internal static PlatformRealmContract Load() => Parse(ReadEmbedded("PlatformRealmContractDeclaration"));

    internal static JsonElement ReadEventStoreDeclaration(string path)
    {
        using JsonDocument document = ReadOwnerManifest(path);
        // Validate the Builds schema without requiring the owner's test projects in a runtime SDK export.
        var validation = ModuleManifestSchema.Value.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (!validation.IsValid)
        {
            string details = string.Join(Environment.NewLine, (validation.Details ?? []).Prepend(validation)
                .Where(result => result.Errors is { Count: > 0 })
                .Select(result => $"'{result.InstanceLocation}': {string.Join(" ", result.Errors!.Values)}"));
            throw new InvalidDataException($"EventStore manifest '{path}' violates the v2 schema.{Environment.NewLine}{details}");
        }

        JsonElement[] modules = [.. document.RootElement.GetProperty("modules").EnumerateArray()
            .Where(module => module.GetProperty("identity").GetProperty("moduleId").GetString() == "eventstore")];
        if (modules.Length != 1)
        {
            throw new InvalidDataException($"EventStore manifest '{path}' must declare exactly one 'eventstore' module in '/modules'.");
        }

        return modules[0].Clone();
    }

    private static JsonDocument ReadOwnerManifest(string path)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowDuplicateProperties = false });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"EventStore manifest '{path}' has invalid JSON at '{exception.Path ?? "$"}'.", exception);
        }
    }

    /// <summary>Validates a realm contract against v1 and the EventStore claim and grant invariants.</summary>
    internal static PlatformRealmContract Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("clients", out JsonElement clients)
            || clients.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Realm contract v1 requires a clients array.");
        }

        // Report malformed client rules before the full schema check so the refusal identifies the client.
        foreach (JsonElement client in clients.EnumerateArray())
        {
            if (client.ValueKind != JsonValueKind.Object || !client.TryGetProperty("id", out JsonElement id)
                || id.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("Realm contract v1 requires an id on every client.");
            }

            string name = id.GetString()!;
            if (ReservedClientIds.Contains(name))
            {
                throw ClientError(name, "uses a reserved Keycloak client id");
            }

            if (!client.TryGetProperty("classes", out JsonElement classes) || classes.ValueKind != JsonValueKind.Array
                || classes.GetArrayLength() != 1)
            {
                throw ClientError(name, "must map to exactly one class");
            }

            JsonElement surface = classes[0];
            if (surface.ValueKind != JsonValueKind.String || surface.GetString() is not ("ui" or "agent" or "service"))
            {
                throw ClientError(name, $"references unknown class {surface.GetRawText()}");
            }

            if (client.TryGetProperty("claims", out JsonElement mappedClaims) && mappedClaims.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement claim in mappedClaims.EnumerateArray())
                {
                    if (claim.ValueKind != JsonValueKind.String || !EventStoreClaims.Contains(claim.GetString()!))
                    {
                        throw ClientError(name, $"references unknown claim {claim.GetRawText()}");
                    }
                }
            }

            if (!client.TryGetProperty("fullScopeAllowed", out JsonElement fullScope) || fullScope.ValueKind != JsonValueKind.False)
            {
                throw ClientError(name, "must set fullScopeAllowed=false");
            }
        }

        if (!Schema.Evaluate(root).IsValid)
        {
            throw new InvalidDataException("Realm contract does not satisfy the v1 schema.");
        }

        Declaration declaration = JsonSerializer.Deserialize<Declaration>(json, JsonOptions)!;
        var claims = declaration.Claims.ToHashSet(StringComparer.Ordinal);
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> principals = new(StringComparer.Ordinal);
        foreach (Client client in declaration.Clients)
        {
            if (!ids.Add(client.Id))
            {
                throw ClientError(client.Id, "must have a unique client id");
            }

            ValidateClient(declaration, claims, client);
            if (client.Principal is { } principal && !principals.Add(principal))
            {
                throw ClientError(client.Id, "must have a unique application principal");
            }
        }

        if (!ids.Contains("mcpcli"))
        {
            throw new InvalidDataException("Realm contract v1 requires the mcpcli client.");
        }

        HashSet<string> services = declaration.Clients.Where(client => client.Surface == "service")
            .Select(client => client.Id).ToHashSet(StringComparer.Ordinal);
        foreach (Client client in declaration.Clients.Where(client => client.Surface == "service"))
        {
            if (client.Audiences.FirstOrDefault(audience => !services.Contains(audience)
                && !(client.Id == ConversationsReader && audience == GatewayUserAudience)) is { } unknownAudience)
            {
                throw ClientError(client.Id, $"references unknown service audience '{unknownAudience}'");
            }
        }

        if (claims.FirstOrDefault(claim => !EventStoreClaims.Contains(claim)) is { } unknown)
        {
            throw new InvalidDataException($"Realm contract references unknown EventStore claim '{unknown}'.");
        }

        if (!claims.SetEquals(EventStoreClaims))
        {
            throw new InvalidDataException("Realm contract v1 requires all listed EventStore surface, authorization and workload claim references.");
        }

        return new PlatformRealmContract(declaration);
    }

    internal IReadOnlyList<Client> SelectClients(IEnumerable<JsonElement> declarations)
    {
        Dictionary<string, Principal> principals = new(StringComparer.Ordinal);
        HashSet<string> gatewayReceivers = new(StringComparer.Ordinal);
        foreach (JsonElement declaration in declarations)
        {
            JsonElement identity = declaration.GetProperty("identity");
            string moduleId = identity.GetProperty("moduleId").GetString()!;
            if (identity.TryGetProperty("servers", out var servers)
                && declaration.TryGetProperty("surfaces", out var surfaces))
            {
                var approved = surfaces.GetProperty("interfaces").EnumerateArray()
                    .Where(surface => surface.TryGetProperty("invocationRules", out var rules)
                        && rules.EnumerateArray().Any(rule => rule.GetProperty("caller").GetString() == "eventstore"))
                    .Select(surface => surface.GetProperty("server").GetString()).ToHashSet(StringComparer.Ordinal);
                foreach (var server in servers.EnumerateArray())
                    if (server.GetProperty("enabled").GetBoolean() && approved.Contains(server.GetProperty("id").GetString()))
                        gatewayReceivers.Add(server.GetProperty("appId").GetString()!);
            }
            if (!declaration.TryGetProperty("integration", out JsonElement integration)
                || !integration.TryGetProperty("identityNeeds", out JsonElement needs))
            {
                continue;
            }

            foreach (JsonElement need in needs.EnumerateArray())
            {
                Principal principal = need.Deserialize<Principal>(JsonOptions)!;
                if (principal.ApplicationPrincipal && !principals.TryAdd(moduleId + "/" + principal.Name, principal))
                {
                    throw new InvalidDataException($"Realm application principal '{moduleId}/{principal.Name}' was declared more than once.");
                }
            }
        }

        List<Client> selected = [];
        foreach (Client client in Model.Clients.OrderBy(client => client.Id, StringComparer.Ordinal))
        {
            if (client.Principal is null)
            {
                selected.Add(client);
                continue;
            }

            if (!principals.Remove(client.Principal, out Principal? principal))
            {
                continue;
            }

            string[] audiences = client.Principal == "eventstore/gateway-workload" && principal.Audience == "domain-services"
                ? ["eventstore", "works"]
                : [principal.Audience];
            if (client.PrincipalAudience != principal.Audience
                || !client.Audiences.ToHashSet(StringComparer.Ordinal).SetEquals(audiences)
                || !client.Roles.ToHashSet(StringComparer.Ordinal).SetEquals(principal.Roles)
                || !(principal.Claims ?? []).All(claim => client.Claims.Contains(claim, StringComparer.Ordinal)))
            {
                throw ClientError(client.Id, $"must match the roles, claims and declared audience of application principal '{client.Principal}'");
            }

            selected.Add(client);
        }

        if (principals.Count != 0)
        {
            throw new InvalidDataException($"Realm application principal '{principals.Keys.Order(StringComparer.Ordinal).First()}' has no client mapping.");
        }

        int gateway = selected.FindIndex(client => client.Id == "eventstore");
        if (gateway >= 0)
        {
            gatewayReceivers.Add("eventstore");
            if (selected.Any(client => client.Id == "works")) gatewayReceivers.Add("works");
            selected[gateway] = selected[gateway] with { Audiences = gatewayReceivers.Order(StringComparer.Ordinal).ToArray() };
        }
        return selected;
    }

    internal static JsonElement ReadWorksPreviewPrincipals() =>
        JsonSerializer.Deserialize<JsonElement>(ReadEmbedded("PlatformWorksPreviewPrincipals"));

    internal static IEnumerable<JsonElement> SelectDeclarations(IEnumerable<PlatformModuleDeclaration> declarations) =>
        declarations.Select(declaration => declaration.Declaration);

    private static void ValidateClient(Declaration declaration, HashSet<string> claims, Client client)
    {
        if (!declaration.Classes.TryGetValue(client.Classes[0], out string? access))
        {
            throw ClientError(client.Id, $"references unknown class '{client.Classes[0]}'");
        }

        if (client.Claims.FirstOrDefault(claim => !claims.Contains(claim) || !EventStoreClaims.Contains(claim)) is { } unknown)
        {
            throw ClientError(client.Id, $"references unknown claim '{unknown}'");
        }

        if (!client.Claims.Contains(RouteCatalogSurfacePolicy.ClaimType, StringComparer.Ordinal))
        {
            throw ClientError(client.Id, $"must emit {RouteCatalogSurfacePolicy.ClaimType}");
        }

        if (access == "public" && client.Scopes.Contains("offline_access", StringComparer.Ordinal))
        {
            throw ClientError(client.Id, "public clients may not receive offline_access");
        }

        // V1 accepts no contract-declared optional scope: the generator derives the service audience and
        // operation scopes, and Keycloak would silently drop a declared one.
        if (client.Scopes.Length != 0)
        {
            throw ClientError(client.Id, "declares an optional scope, which realm contract v1 does not define");
        }

        if (client.Surface == "agent" && client.Audiences.Any(audience => audience == "realm-management"
            || audience.Split(['-', '.', ':', '_', '/']).Any(segment => segment is "admin" or "administrator")))
        {
            throw ClientError(client.Id, "agent clients may not receive the realm-management or an admin audience");
        }

        if (client.Id == "mcpcli" && (client.Surface != "agent" || client.Audiences is not ["hexalith-eventstore"]
            || client.Roles.Length != 0 || client.Claims is not [RouteCatalogSurfacePolicy.ClaimType]
            || !client.StandardFlow || client.Pkce != "S256" || client.RedirectUri != "http://127.0.0.1/*"
            || client.TokenExchange))
        {
            throw ClientError(client.Id, "must use only the agent PKCE S256 loopback code flow for hexalith-eventstore with no roles");
        }

        if (client.Id == ConversationsReader)
        {
            if (client.Surface != "service" || client.Principal != "conversations/tenant-access-history-reader"
                || client.PrincipalAudience != GatewayUserAudience || client.Audiences is not [GatewayUserAudience]
                || client.Operations.Length != 0 || client.Roles.Length != 0 || client.StandardFlow || client.TokenExchange
                || !client.Claims.ToHashSet(StringComparer.Ordinal).SetEquals([RouteCatalogSurfacePolicy.ClaimType, "eventstore:permission"]))
            {
                throw ClientError(client.Id, "must have only the Conversations tenant-history reader grant");
            }
        }
        else if (client.Surface == "service")
        {
            string[] mappedClaims = client.Id == "works"
                ? [RouteCatalogSurfacePolicy.ClaimType, EventStoreWorkloadAuthenticationDefaults.OperationClaimType,
                    EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType, EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType,
                    "eventstore:grant-id", "eventstore:grant-revision"]
                : [RouteCatalogSurfacePolicy.ClaimType, EventStoreWorkloadAuthenticationDefaults.OperationClaimType];
            if (client.Principal is null || client.PrincipalAudience is null || client.StandardFlow
                || client.Operations.Length == 0 || client.Roles.Length == 0
                || !client.Claims.ToHashSet(StringComparer.Ordinal).SetEquals(mappedClaims))
            {
                throw ClientError(client.Id, "must map an application principal and explicit workload claims, roles and operations");
            }

            if (client.Operations.FirstOrDefault(operation => !WorkloadOperations.Contains(operation)) is { } operation)
            {
                throw ClientError(client.Id, $"references unknown workload operation '{operation}'");
            }

            if (client.Id == "works" && (client.TokenExchange || client.Audiences is not ["eventstore"]
                || !WorksBoundOperations.SetEquals(client.BoundOperations ?? [])
                || !client.Operations.ToHashSet(StringComparer.Ordinal).SetEquals(WorksBoundOperations.Append(EventStoreWorkloadOperations.TrustedEffect))))
            {
                throw ClientError(client.Id, "must allow only the three bound gateway operations and unbound trusted-effect, with token exchange disabled");
            }
        }
        else if (client.Principal is not null || client.PrincipalAudience is not null || client.Operations.Length != 0
            || client.TokenExchange || client.Roles.Length != 0 || client.Claims.Length != 1)
        {
            throw ClientError(client.Id, "human clients in v1 map only the surface claim and receive no workload grants");
        }

        if (client.BoundOperations is not null && client.Id != "works")
        {
            throw ClientError(client.Id, "is not a Works workload client and cannot request resource binding");
        }
    }

    private static InvalidDataException ClientError(string client, string rule) =>
        new($"Realm client '{client}' {rule}.");

    private static JsonSchema ReadSchema(string name, Assembly? assembly = null) => JsonSchema.FromText(ReadEmbedded(name, assembly), new BuildOptions
    {
        SchemaRegistry = new SchemaRegistry
        {
            Fetch = (_, _) => throw new InvalidOperationException("Realm contract validation cannot fetch remote schemas."),
        },
    });

    private static string ReadEmbedded(string name, Assembly? assembly = null)
    {
        using Stream stream = (assembly ?? typeof(PlatformRealmContract).Assembly).GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing embedded realm resource '{name}'.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    internal sealed record Declaration(string Realm, string[] Claims, Dictionary<string, string> Classes,
        Client[] Clients, Exchange Exchange, string[] AdmissionGroups, Events Events, Refresh Refresh, string SyntheticTenantSlot);

    internal sealed record Client(string Id, string[] Classes, string? Principal, string? PrincipalAudience, string[] Scopes,
        string[] Claims, string[] Audiences, string[] Roles, string[] Operations, bool StandardFlow, bool TokenExchange,
        string? Pkce, string? RedirectUri, string[]? BoundOperations)
    {
        internal string Surface => Classes[0];
    }

    internal sealed record Exchange(bool Standard, bool RequireRequesterAudience, string DownscopePolicy, bool AllowRefreshTokens);

    internal sealed record Events(bool User, bool Admin, bool AdminDetails);

    internal sealed record Refresh(bool RevokeOnUse, int MaxReuse, int IdleSeconds, int MaxSeconds);

    private sealed record Principal(string Name, string Audience, string[] Roles, string[]? Claims, bool ApplicationPrincipal);
}
