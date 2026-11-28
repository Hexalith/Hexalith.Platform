namespace Hexalith.Platform.Composition;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.Extensions.Configuration;

using Client = PlatformRealmContract.Client;
using RealmContract = PlatformRealmContract.Declaration;

/// <summary>Renders and composes the Platform-owned local Keycloak realm.</summary>
internal static class PlatformLocalRealm
{
    /// <summary>The exact authorization server used by the local realm.</summary>
    internal const string Image = _registry + "/" + _repository + ":" + _tag;

    private const string _registry = "quay.io";
    private const string _repository = "keycloak/keycloak";
    private const string _tag = "26.6.4";
    private const string _resourceName = "platform-local-realm";
    private const string _policyName = "platform-downscope-token-exchange";
    private const string _bindingPolicy = "hexalith-workload-binding-v1";
    private const string _bindingSignerEnd = "HEXALITH_LOCAL_REALM_BINDING_SIGNER_END";
    private const string _bindingEnabledAttribute = "hexalith.workload-binding.enabled";
    private const string _bindingSignerEndAttribute = "hexalith.workload-binding.signer-not-after";

    internal const string PolicyContainerPath = "/opt/keycloak/providers/platform-requester-audience.jar";
    internal const string BindingContainerPath = "/opt/keycloak/providers/platform-workload-binding.jar";
    internal const string ImportContainerDirectory = "/opt/keycloak/data/import/";
    internal static readonly string[] StartupArguments = ["start-dev", "--import-realm", "--features-disabled=token-exchange"];

    private static readonly JsonSerializerOptions RealmJson = new() { WriteIndented = true };

    /// <summary>Renders an import from validated selected module declarations and caller-supplied run values.</summary>
    /// <remarks>Only secret environment variable placeholders are written into the import.</remarks>
    internal static string Render(PlatformRealmContract contract, IEnumerable<PlatformModuleDeclaration> declarations,
        Uri realmUrl, string syntheticTenantId, IReadOnlyDictionary<string, string> clientSecrets)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(declarations);
        return Prepare(contract, contract.SelectClients(PlatformRealmContract.SelectDeclarations(declarations)),
            realmUrl, syntheticTenantId, clientSecrets).Import;
    }

    /// <summary>Writes the pinned Keycloak client-policy provider required by the generated realm.</summary>
    internal static void WriteAudiencePolicy(string path) => WriteProvider(path, "PlatformRealmRequesterAudiencePolicy");

    internal static void WriteWorkloadBindingPolicy(string path) => WriteProvider(path, "PlatformRealmWorkloadBinding");

    private static void WriteProvider(string path, string resource)
    {
        using Stream provider = typeof(PlatformLocalRealm).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded Keycloak provider '{resource}'.");
        using (FileStream target = File.Create(path))
        {
            provider.CopyTo(target);
        }
        AllowContainerRead(path);
    }

    internal static PreparedRealm PrepareWorksPreview(string eventStoreDeclarationPath, int port, string syntheticTenantId)
    {
        PlatformRealmContract contract = PlatformRealmContract.Load();
        JsonElement eventStore = PlatformRealmContract.ReadEventStoreDeclaration(eventStoreDeclarationPath);
        IReadOnlyList<Client> clients = contract.SelectClients([eventStore, PlatformRealmContract.ReadWorksPreviewPrincipals()]);
        foreach (string issuer in new[] { "eventstore", "eventstore-admin", "works" })
        {
            if (!clients.Any(client => client.Id == issuer && client.Surface == "service"))
            {
                throw new InvalidDataException($"Works preview realm requires the declared workload client '{issuer}'.");
            }
        }

        return PrepareLocal(contract, clients, port, syntheticTenantId);
    }

    /// <summary>Prepares the Development realm instance, adding the instance-only local operator when grants are supplied.</summary>
    /// <param name="declarations">The selected module declarations.</param>
    /// <param name="port">The realm's loopback port.</param>
    /// <param name="syntheticTenantId">The run's synthetic tenant identifier.</param>
    /// <param name="gatewayQueryDomains">The gateway's bound query domains by audience.</param>
    /// <param name="operatorGrants">The single grants the local operator may request; null renders no operator.</param>
    internal static PreparedRealm PrepareDevelopment(IReadOnlyList<PlatformModuleDeclaration> declarations, int port, string syntheticTenantId,
        IReadOnlyDictionary<string, string[]>? gatewayQueryDomains = null, IReadOnlyList<PlatformOperatorGrant>? operatorGrants = null)
    {
        var contract = PlatformRealmContract.Load();
        var clients = contract.SelectClients(PlatformRealmContract.SelectDeclarations(declarations));
        if (gatewayQueryDomains is { Count: > 0 })
            clients = clients.Select(client => client.Id == "eventstore" ? client with { BoundOperations = [EventStoreWorkloadOperations.DomainServiceQuery] } : client).ToArray();
        return PrepareLocal(contract, clients, port, syntheticTenantId, gatewayQueryDomains, operatorGrants);
    }

    private static PreparedRealm PrepareLocal(PlatformRealmContract contract, IReadOnlyList<Client> clients, int port, string syntheticTenantId,
        IReadOnlyDictionary<string, string[]>? gatewayQueryDomains = null, IReadOnlyList<PlatformOperatorGrant>? operatorGrants = null)
    {
        var secrets = clients.Where(client => contract.Model.Classes[client.Surface] == "confidential")
            .ToDictionary(client => client.Id, _ => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), StringComparer.Ordinal);
        if (operatorGrants is not null) secrets.Add(PlatformRealmOperator.ClientId, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        var realmUrl = new UriBuilder(Uri.UriSchemeHttp, "127.0.0.1", port, "/realms/" + contract.Model.Realm).Uri;
        return Prepare(contract, clients, realmUrl, syntheticTenantId, secrets, gatewayQueryDomains, operatorGrants);
    }

    private static PreparedRealm Prepare(PlatformRealmContract contract, IReadOnlyList<Client> clients,
        Uri realmUrl, string syntheticTenantId, IReadOnlyDictionary<string, string> clientSecrets,
        IReadOnlyDictionary<string, string[]>? gatewayQueryDomains = null, IReadOnlyList<PlatformOperatorGrant>? operatorGrants = null)
    {
        ArgumentNullException.ThrowIfNull(realmUrl);
        ArgumentNullException.ThrowIfNull(clientSecrets);
        RealmContract realm = contract.Model;
        if (!realmUrl.IsAbsoluteUri || !realmUrl.IsLoopback || realmUrl.Scheme is not ("http" or "https")
            || realmUrl.AbsolutePath != "/realms/" + realm.Realm || realmUrl.Query.Length != 0
            || realmUrl.Fragment.Length != 0 || realmUrl.UserInfo.Length != 0)
        {
            throw new ArgumentException($"The realm URL must identify /realms/{realm.Realm} on a loopback HTTP(S) endpoint.", nameof(realmUrl));
        }

        if (!TenantCanonicalizer.TryCanonicalize(syntheticTenantId, out string canonicalTenant) || canonicalTenant != syntheticTenantId)
        {
            throw new ArgumentException("The synthetic tenant must be a canonical tenant identifier.", nameof(syntheticTenantId));
        }

        Client[] confidential = [.. clients.Where(client => realm.Classes[client.Surface] == "confidential")];
        if (operatorGrants is not null && (clients.Any(client => client.Id == PlatformRealmOperator.ClientId) || operatorGrants.Count == 0
            || operatorGrants.Select(grant => grant.Scope).Distinct(StringComparer.Ordinal).Count() != operatorGrants.Count))
        {
            throw new ArgumentException($"The instance-only operator '{PlatformRealmOperator.ClientId}' needs distinct grants and no contract client of that name.", nameof(operatorGrants));
        }

        Dictionary<string, string> secrets = new(StringComparer.Ordinal);
        HashSet<string> values = new(StringComparer.Ordinal);
        foreach (string clientId in confidential.Select(client => client.Id).Concat(operatorGrants is null ? [] : [PlatformRealmOperator.ClientId]))
        {
            if (!clientSecrets.TryGetValue(clientId, out string? secret) || secret is not { Length: >= 32 }
                || secret.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.' or '~'))
                || !values.Add(secret))
            {
                throw new ArgumentException($"Realm client '{clientId}' requires a distinct run secret of at least 32 URL-safe characters.", nameof(clientSecrets));
            }

            secrets.Add(clientId, secret);
        }

        if (clientSecrets.Count != secrets.Count)
        {
            throw new ArgumentException("Run secrets must match the selected confidential realm clients.", nameof(clientSecrets));
        }

        string import = RenderImport(realm, clients, realmUrl, syntheticTenantId, operatorGrants);
        return new PreparedRealm(realmUrl, realm.Realm, import, secrets,
            clients.SingleOrDefault(client => client.BoundOperations is not null), syntheticTenantId, gatewayQueryDomains);
    }

    private static string RenderImport(RealmContract realm, IReadOnlyList<Client> clients, Uri realmUrl, string tenant,
        IReadOnlyList<PlatformOperatorGrant>? operatorGrants = null)
    {
        HashSet<string> selectedServices = clients.Where(client => client.Surface == "service")
            .Select(client => client.Id).ToHashSet(StringComparer.Ordinal);
        var workloadClients = clients.Where(client => client.Surface == "service" && client.Id != PlatformRealmContract.ConversationsReader).ToArray();
        var serviceAudiences = workloadClients
            .ToDictionary(client => client.Id, client => client.Audiences
                .Where(audience => client.Id == "eventstore" || selectedServices.Contains(audience)).Order(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        SortedDictionary<string, SortedSet<string>> audienceRoles = new(StringComparer.Ordinal);
        foreach (Client client in workloadClients)
        {
            string[] selectedAudiences = serviceAudiences[client.Id];
            if (selectedAudiences.Length == 0)
            {
                throw new InvalidDataException($"Realm client '{client.Id}' requires selected audience '{client.PrincipalAudience}'.");
            }

            foreach (string audience in selectedAudiences)
            {
                if (!audienceRoles.TryGetValue(audience, out SortedSet<string>? roles))
                {
                    roles = new SortedSet<string>(StringComparer.Ordinal);
                    audienceRoles.Add(audience, roles);
                }

                roles.UnionWith(client.Roles);
            }
        }

        var basicScope = PlatformOwnerServices.Scope("basic", PlatformOwnerServices.Mapper("subject", "oidc-sub-mapper",
            new JsonObject { ["access.token.claim"] = "true" }));
        basicScope["attributes"]!["include.in.token.scope"] = "false";
        JsonArray scopes = new(basicScope);
        JsonObject roleDefinitions = new();
        JsonObject roleScopes = new();
        foreach ((string audience, SortedSet<string> roles) in audienceRoles)
        {
            string scope = AudienceScope(audience);
            scopes.Add(PlatformOwnerServices.Scope(scope, AudienceMapper(audience, clientAudience: selectedServices.Contains(audience))));
            if (!selectedServices.Contains(audience)) continue;
            roleDefinitions.Add(audience, new JsonArray(roles.Select(role => (JsonNode)new JsonObject { ["name"] = role }).ToArray()));
            roleScopes.Add(audience, new JsonArray(new JsonObject { ["clientScope"] = scope, ["roles"] = Strings(roles) }));
        }

        foreach (string operation in clients.SelectMany(client => client.Operations).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            scopes.Add(PlatformOwnerServices.Scope(OperationScope(operation),
                PlatformOwnerServices.Claim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, operation)));
        }

        JsonArray users = new();
        JsonArray renderedClients = new();
        foreach (Client client in clients)
        {
            bool service = client.Surface == "service";
            bool publicClient = realm.Classes[client.Surface] == "public";
            bool exchange = realm.Exchange.Standard && client.TokenExchange;
            JsonObject attributes = new()
            {
                ["oauth2.device.authorization.grant.enabled"] = "false",
                ["oidc.ciba.grant.enabled"] = "false",
                ["standard.token.exchange.enabled"] = exchange ? "true" : "false",
                ["standard.token.exchange.enableRefreshRequestedTokenType"] = realm.Exchange.AllowRefreshTokens ? "SAME_SESSION" : "NO",
                ["access.token.signed.response.alg"] = "RS256",
            };
            if (client.Pkce is { } pkce)
            {
                attributes["pkce.code.challenge.method"] = pkce;
            }

            JsonArray mappers = new(PlatformOwnerServices.Claim(RouteCatalogSurfacePolicy.ClaimType, client.Surface));
            IEnumerable<string> optionalScopes = client.Scopes;
            if (client.Id == PlatformRealmContract.ConversationsReader)
            {
                mappers.Add(AudienceMapper(PlatformRealmContract.GatewayUserAudience, clientAudience: false));
                mappers.Add(PlatformOwnerServices.Claim("eventstore:permission", "tenants:read"));
            }
            else if (service)
            {
                string[] audiences = serviceAudiences[client.Id];
                optionalScopes = optionalScopes.Concat(audiences.Select(AudienceScope)).Concat(client.Operations.Select(OperationScope));
                JsonObject assignedRoles = new();
                foreach (string audience in audiences)
                {
                    if (selectedServices.Contains(audience)) assignedRoles.Add(audience, Strings(client.Roles));
                }

                users.Add(new JsonObject
                {
                    ["username"] = "service-account-" + client.Id,
                    ["enabled"] = true,
                    ["serviceAccountClientId"] = client.Id,
                    ["clientRoles"] = assignedRoles,
                });
                if (client.BoundOperations is not null)
                {
                    attributes["hexalith.workload-binding.workload"] = client.Id;
                    attributes["hexalith.workload-binding.grants"] = Placeholder(BindingGrantsEnvironmentName(client.Id));
                    mappers.Add(new JsonObject
                    {
                        ["name"] = "workload-binding",
                        ["protocol"] = "openid-connect",
                        ["protocolMapper"] = _bindingPolicy,
                        ["config"] = new JsonObject(),
                    });
                }
            }
            else
            {
                foreach (string audience in client.Audiences.Order(StringComparer.Ordinal))
                {
                    mappers.Add(AudienceMapper(audience, clientAudience: false));
                }
            }

            JsonObject clientJson = new()
            {
                ["clientId"] = client.Id,
                ["enabled"] = true,
                ["protocol"] = "openid-connect",
                ["publicClient"] = publicClient,
                ["serviceAccountsEnabled"] = service,
                ["standardFlowEnabled"] = client.StandardFlow,
                ["directAccessGrantsEnabled"] = false,
                ["implicitFlowEnabled"] = false,
                ["fullScopeAllowed"] = false,
                ["redirectUris"] = client.RedirectUri is { } redirect ? new JsonArray(redirect) : new JsonArray(),
                ["attributes"] = attributes,
                ["defaultClientScopes"] = new JsonArray("basic"),
                ["optionalClientScopes"] = Strings(optionalScopes),
                ["protocolMappers"] = mappers,
            };
            if (!publicClient)
            {
                clientJson["clientAuthenticatorType"] = "client-secret";
                clientJson["secret"] = Placeholder(SecretEnvironmentName(client.Id));
            }

            renderedClients.Add(clientJson);
        }

        if (operatorGrants is not null) RenderOperator(operatorGrants, scopes, renderedClients, users);

        JsonArray executors = new(
            new JsonObject { ["executor"] = "platform-require-requester-audience", ["configuration"] = new JsonObject() },
            new JsonObject { ["executor"] = realm.Exchange.DownscopePolicy, ["configuration"] = new JsonObject() });
        bool workloadBinding = clients.Any(client => client.BoundOperations is not null);
        if (workloadBinding)
        {
            executors.Add(new JsonObject { ["executor"] = _bindingPolicy, ["configuration"] = new JsonObject() });
        }

        JsonObject root = new()
        {
            ["realm"] = realm.Realm,
            ["enabled"] = true,
            ["sslRequired"] = "none",
            ["registrationAllowed"] = false,
            ["defaultSignatureAlgorithm"] = "RS256",
            ["attributes"] = new JsonObject
            {
                ["frontendUrl"] = realmUrl.GetLeftPart(UriPartial.Authority),
                [realm.SyntheticTenantSlot] = tenant,
                ["platform.surfaceClasses"] = string.Join(',', realm.Classes.Keys.Order(StringComparer.Ordinal)),
            },
            ["eventsEnabled"] = realm.Events.User,
            ["adminEventsEnabled"] = realm.Events.Admin,
            ["adminEventsDetailsEnabled"] = realm.Events.AdminDetails,
            ["eventsListeners"] = new JsonArray(),
            ["revokeRefreshToken"] = realm.Refresh.RevokeOnUse,
            ["refreshTokenMaxReuse"] = realm.Refresh.MaxReuse,
            ["ssoSessionIdleTimeout"] = realm.Refresh.IdleSeconds,
            ["ssoSessionMaxLifespan"] = realm.Refresh.MaxSeconds,
            ["groups"] = new JsonArray(realm.AdmissionGroups.Select(name => (JsonNode)new JsonObject { ["name"] = name }).ToArray()),
            ["defaultDefaultClientScopes"] = new JsonArray(),
            ["defaultOptionalClientScopes"] = new JsonArray(),
            ["roles"] = new JsonObject { ["client"] = roleDefinitions },
            ["clientScopeMappings"] = roleScopes,
            ["clientScopes"] = scopes,
            ["clients"] = renderedClients,
            ["users"] = users,
            ["clientProfiles"] = new JsonObject
            {
                ["profiles"] = new JsonArray(new JsonObject
                {
                    ["name"] = _policyName,
                    ["executors"] = executors,
                }),
            },
            ["clientPolicies"] = new JsonObject
            {
                ["policies"] = new JsonArray(new JsonObject
                {
                    ["name"] = _policyName,
                    ["enabled"] = realm.Exchange.RequireRequesterAudience,
                    ["conditions"] = new JsonArray(new JsonObject { ["condition"] = "any-client", ["configuration"] = new JsonObject() }),
                    ["profiles"] = new JsonArray(_policyName),
                }),
            },
        };
        if (workloadBinding)
        {
            root["attributes"]![_bindingEnabledAttribute] = "true";
            root["attributes"]![_bindingSignerEndAttribute] = Placeholder(_bindingSignerEnd);
        }

        return root.ToJsonString(RealmJson) + "\n";
    }

    /// <summary>Renders the instance-only local operator: a confidential service account with one optional scope per grant.</summary>
    /// <remarks>Its tokens carry the gateway user audience and only the grants each request names; it has no workload, tenant or surface claim.</remarks>
    private static void RenderOperator(IReadOnlyList<PlatformOperatorGrant> grants, JsonArray scopes, JsonArray clients, JsonArray users)
    {
        foreach (PlatformOperatorGrant grant in grants)
        {
            scopes.Add(PlatformOwnerServices.Scope(grant.Scope, PlatformOwnerServices.Claim(grant.ClaimType, grant.ClaimValue)));
        }

        clients.Add(new JsonObject
        {
            ["clientId"] = PlatformRealmOperator.ClientId,
            ["enabled"] = true,
            ["protocol"] = "openid-connect",
            ["publicClient"] = false,
            ["serviceAccountsEnabled"] = true,
            ["standardFlowEnabled"] = false,
            ["directAccessGrantsEnabled"] = false,
            ["implicitFlowEnabled"] = false,
            ["fullScopeAllowed"] = false,
            ["redirectUris"] = new JsonArray(),
            ["attributes"] = new JsonObject
            {
                ["oauth2.device.authorization.grant.enabled"] = "false",
                ["oidc.ciba.grant.enabled"] = "false",
                ["standard.token.exchange.enabled"] = "false",
                ["standard.token.exchange.enableRefreshRequestedTokenType"] = "NO",
                ["access.token.signed.response.alg"] = "RS256",
            },
            ["defaultClientScopes"] = new JsonArray("basic"),
            ["optionalClientScopes"] = Strings(grants.Select(grant => grant.Scope)),
            ["protocolMappers"] = new JsonArray(AudienceMapper(PlatformRealmContract.GatewayUserAudience, clientAudience: false)),
            ["clientAuthenticatorType"] = "client-secret",
            ["secret"] = Placeholder(SecretEnvironmentName(PlatformRealmOperator.ClientId)),
        });
        users.Add(new JsonObject
        {
            ["username"] = "service-account-" + PlatformRealmOperator.ClientId,
            ["enabled"] = true,
            ["serviceAccountClientId"] = PlatformRealmOperator.ClientId,
            ["clientRoles"] = new JsonObject(),
        });
    }

    private static JsonObject AudienceMapper(string audience, bool clientAudience) =>
        PlatformOwnerServices.Mapper("audience-" + audience, "oidc-audience-mapper", new()
        {
            [clientAudience ? "included.client.audience" : "included.custom.audience"] = audience,
            ["access.token.claim"] = "true",
            ["id.token.claim"] = "false",
        });

    private static JsonArray Strings(IEnumerable<string> values) =>
        new(values.Order(StringComparer.Ordinal).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());

    private static string AudienceScope(string audience) => WorkloadAssertionIssuerOptions.DefaultAudienceScopePrefix + audience;

    private static string OperationScope(string operation) =>
        WorkloadAssertionIssuerOptions.DefaultOperationScopePrefix + operation.Replace(':', '.');

    internal static string SecretEnvironmentName(string client) =>
        "HEXALITH_LOCAL_REALM_SECRET_" + client.ToUpperInvariant().Replace('-', '_');

    private static string BindingGrantsEnvironmentName(string client) =>
        "HEXALITH_LOCAL_REALM_BINDINGS_" + client.ToUpperInvariant().Replace('-', '_');

    private static string Placeholder(string name) => "${" + name + "}";

    internal static void AllowContainerRead(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            // The private workspace contains only placeholders and the policy. Keycloak reads the mounts as uid 1000.
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite
                | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }
    }

    internal sealed class PreparedRealm(Uri url, string name, string import, IReadOnlyDictionary<string, string> secrets,
        Client? boundClient, string syntheticTenantId, IReadOnlyDictionary<string, string[]>? gatewayQueryDomains)
    {
        internal string Import => import;

        /// <summary>Returns one run-generated confidential client secret. It stays in the AppHost and is never written outside the realm placeholders.</summary>
        internal string Secret(string client) => secrets.TryGetValue(client, out string? secret)
            ? secret : throw new InvalidOperationException($"Realm client '{client}' has no run secret.");

        internal IReadOnlyDictionary<string, string> WorkloadBindingEnvironment(DateTimeOffset now)
        {
            if (boundClient?.BoundOperations is not { } boundOperations)
            {
                return new Dictionary<string, string>();
            }

            long notBefore = now.AddMinutes(-1).ToUnixTimeSeconds();
            long expiresAt = now.AddDays(1).ToUnixTimeSeconds();
            JsonObject Bindings(string[] domains, params string[] tenants) => new()
            {
                [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = Strings(tenants),
                [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = Strings(domains),
            };

            JsonArray grants = new();
            if (boundClient.Id == "works")
            {
                string gateway = boundClient.Audiences.Single();
                grants.Add(Grant("works-bound-gateway", gateway, boundOperations, Bindings(["work"], syntheticTenantId), allowUnbound: false));
                grants.Add(Grant("works-trusted-effect", gateway, [EventStoreWorkloadOperations.TrustedEffect], new JsonObject(), allowUnbound: true));
            }
            else
            {
                foreach (string audience in boundClient.Audiences)
                {
                    grants.Add(Grant("eventstore-unbound-" + audience, audience, boundClient.Operations, new JsonObject(), allowUnbound: true));
                    if (gatewayQueryDomains?.TryGetValue(audience, out string[]? domains) == true)
                    {
                        string[] tenants = audience == "tenants" ? [syntheticTenantId, "system"] : [syntheticTenantId];
                        grants.Add(Grant("eventstore-bound-query-" + audience, audience, boundOperations,
                            Bindings(domains, tenants), allowUnbound: false, allowQueryAuthorityDigest: true));
                    }
                }
            }
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [_bindingSignerEnd] = expiresAt.ToString(System.Globalization.CultureInfo.InvariantCulture),
                // Keycloak substitutes the environment value into the JSON stream before parsing the attribute.
                [BindingGrantsEnvironmentName(boundClient.Id)] = JsonEncodedText.Encode(grants.ToJsonString()).ToString(),
            };

            JsonObject Grant(string id, string audience, string[] operations, JsonObject permittedBindings, bool allowUnbound,
                bool allowQueryAuthorityDigest = false) => new()
            {
                ["grantId"] = id,
                ["revision"] = 1,
                ["clientId"] = boundClient.Id,
                ["workload"] = boundClient.Id,
                ["audience"] = audience,
                ["operations"] = Strings(operations),
                ["bindings"] = permittedBindings,
                ["allowUnbound"] = allowUnbound,
                ["allowQueryAuthorityDigest"] = allowQueryAuthorityDigest,
                ["notBefore"] = notBefore,
                ["expiresAt"] = expiresAt,
                ["revoked"] = false,
            };
        }

        internal RealmResource AddResource(IDistributedApplicationBuilder builder, string workspace)
        {
            string path = Path.Combine(workspace, name + "-realm.json");
            File.WriteAllText(path, import);
            AllowContainerRead(path);
            string policyPath = Path.Combine(workspace, Path.GetFileName(PolicyContainerPath));
            WriteAudiencePolicy(policyPath);

            var keycloak = builder.AddContainer(_resourceName, _repository, _tag)
                .WithImageRegistry(_registry)
                .WithHttpEndpoint(port: url.Port, targetPort: 8080, name: "http", isProxied: false)
                .WithEndpoint("http", endpoint => endpoint.TargetHost = url.Host)
                .WithArgs(StartupArguments)
                .WithBindMount(path, ImportContainerDirectory + Path.GetFileName(path), isReadOnly: true)
                .WithBindMount(policyPath, PolicyContainerPath, isReadOnly: true)
                .WithEnvironment("KC_HOSTNAME", url.GetLeftPart(UriPartial.Authority))
                .WithHttpHealthCheck(url.AbsolutePath + "/.well-known/openid-configuration");
            if (boundClient is not null)
            {
                string bindingPath = Path.Combine(workspace, Path.GetFileName(BindingContainerPath));
                WriteWorkloadBindingPolicy(bindingPath);
                keycloak.WithBindMount(bindingPath, BindingContainerPath, isReadOnly: true);
                foreach ((string key, string value) in WorkloadBindingEnvironment(DateTimeOffset.UtcNow))
                {
                    keycloak.WithEnvironment(key, value);
                }
            }

            Dictionary<string, IResourceBuilder<ParameterResource>> credentials = new(StringComparer.Ordinal);
            foreach ((string client, string secret) in secrets)
            {
                var parameter = builder.AddParameter("realm-" + client + "-secret", () => secret,
                    publishValueAsDefault: false, secret: true);
                keycloak.WithEnvironment(SecretEnvironmentName(client), parameter);
                credentials.Add(client, parameter);
            }

            return new RealmResource(keycloak, url.AbsoluteUri, credentials, boundClient?.Id);
        }
    }

    internal sealed record RealmResource(IResourceBuilder<ContainerResource> Keycloak, string Authority,
        IReadOnlyDictionary<string, IResourceBuilder<ParameterResource>> Credentials, string? BoundClient)
    {
        internal void ConfigureDevelopment<T>(IResourceBuilder<T> resource, PlatformServerBinding server, IConfiguration configuration)
            where T : IResourceWithEnvironment, IResourceWithWaitSupport
        {
            ConfigureValidator(resource, configuration, PlatformRealmContract.GatewayUserAudience, server.AppId);
            resource.WithEnvironment("Authentication__JwtBearer__ValidAlgorithms__0", "RS256")
                .WithEnvironment("Authentication__Workload__Audience", server.AppId)
                .WithEnvironment("Authentication__Workload__AllowedCallers__0", "eventstore");

            if (Credentials.ContainsKey(server.AppId)) ConfigureWorkloadIssuer(resource, server.AppId, allowBindings: BoundClient == server.AppId);
            if (server.ResourceId == "eventstore")
            {
                string[] callers = ["tenants", "memories", .. Credentials.Keys.Where(name => name is "folders" or "folders-workers" or "projects")];
                for (int index = 0; index < callers.Length; index++)
                    resource.WithEnvironment("Authentication__DaprInternal__AllowedCallers__" + index, callers[index]);
                resource.WithEnvironment("Authentication__DaprInternal__Audience", server.AppId);
            }

            if (server.ResourceId == "conversations")
            {
                const string reader = PlatformRealmContract.ConversationsReader;
                resource.WithEnvironment("EventStore__Authentication__Authority", Authority)
                    .WithEnvironment("EventStore__Authentication__TokenEndpoint", Authority + "/protocol/openid-connect/token")
                    .WithEnvironment("EventStore__Authentication__GrantType", "client_credentials")
                    .WithEnvironment("EventStore__Authentication__ClientId", reader)
                    .WithEnvironment("EventStore__Authentication__ClientSecret", Credentials[reader]);
            }

            if (server.ResourceId is "tenants" or "tenants-api")
            {
                resource.WithEnvironment("Oidc__Authority", Authority)
                    .WithEnvironment("Jwt__SigningKey", string.Empty)
                    .WithEnvironment("EventStore__Authentication__SigningKey", string.Empty)
                    .WithEnvironment("EventStore__Authentication__Issuer", Authority)
                    .WithEnvironment("EventStore__Authentication__Authority", Authority);
            }

            if (server.ResourceId == "projects")
                resource.WithEnvironment("Authentication__JwtBearer__AllowAnonymousDevelopment", "false");
        }

        internal IResourceBuilder<T> ConfigureValidator<T>(IResourceBuilder<T> resource, IConfiguration configuration,
            string userAudience, string workloadAudience)
            where T : IResourceWithEnvironment, IResourceWithWaitSupport
        {
            ConfigureSection(EventStoreWorkloadAuthenticationDefaults.JwtContractSection, userAudience);
            ConfigureSection(EventStoreWorkloadAuthenticationDefaults.DedicatedJwtContractSection, workloadAudience);
            return resource.WaitFor(Keycloak)
                .WithEnvironment("Authentication__WorkloadIssuer__TokenEndpoint", Authority + "/protocol/openid-connect/token");

            void ConfigureSection(string section, string audience)
            {
                string prefix = section.Replace(":", "__", StringComparison.Ordinal) + "__";
                resource.WithEnvironment(prefix + "Authority", Authority)
                    .WithEnvironment(prefix + "Issuer", Authority)
                    .WithEnvironment(prefix + "Audience", audience)
                    .WithEnvironment(prefix + "SigningKey", string.Empty)
                    .WithEnvironment(prefix + "AllowInsecureSymmetricKey", "false")
                    .WithEnvironment(prefix + "RequireHttpsMetadata", "false")
                    .WithEnvironment(prefix + "AllowedAlgorithms__0", "RS256")
                    .WithEnvironment(prefix + "ValidAudiences__0", audience);
                foreach (IConfigurationSection entry in configuration.GetSection(section + ":AllowedAlgorithms").GetChildren())
                {
                    resource.WithEnvironment(prefix + "AllowedAlgorithms__" + entry.Key, "RS256");
                }

                foreach (IConfigurationSection entry in configuration.GetSection(section + ":ValidAudiences").GetChildren())
                {
                    resource.WithEnvironment(prefix + "ValidAudiences__" + entry.Key, audience);
                }
            }
        }

        internal void ConfigureWorkloadIssuer<T>(IResourceBuilder<T> resource, string client, bool allowBindings = false)
            where T : IResourceWithEnvironment =>
            resource
                .WithEnvironment("Authentication__WorkloadIssuer__Workload", client)
                .WithEnvironment("Authentication__WorkloadIssuer__ClientId", client)
                .WithEnvironment("Authentication__WorkloadIssuer__AudienceScopePrefix", WorkloadAssertionIssuerOptions.DefaultAudienceScopePrefix)
                .WithEnvironment("Authentication__WorkloadIssuer__OperationScopePrefix", WorkloadAssertionIssuerOptions.DefaultOperationScopePrefix)
                .WithEnvironment("Authentication__WorkloadIssuer__EnableResourceBindingRequests", allowBindings ? "true" : "false")
                .WithEnvironment("Authentication__WorkloadIssuer__ClientSecret", Credentials[client]);
    }
}
