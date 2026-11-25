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

        Dictionary<string, string> secrets = clients.Where(client => client.Surface != "agent")
            .ToDictionary(client => client.Id, _ => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), StringComparer.Ordinal);
        var realmUrl = new UriBuilder(Uri.UriSchemeHttp, "127.0.0.1", port, "/realms/" + contract.Model.Realm).Uri;
        return Prepare(contract, clients, realmUrl, syntheticTenantId, secrets);
    }

    private static PreparedRealm Prepare(PlatformRealmContract contract, IReadOnlyList<Client> clients,
        Uri realmUrl, string syntheticTenantId, IReadOnlyDictionary<string, string> clientSecrets)
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
        Dictionary<string, string> secrets = new(StringComparer.Ordinal);
        HashSet<string> values = new(StringComparer.Ordinal);
        foreach (Client client in confidential)
        {
            if (!clientSecrets.TryGetValue(client.Id, out string? secret) || secret is not { Length: >= 32 }
                || secret.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.' or '~'))
                || !values.Add(secret))
            {
                throw new ArgumentException($"Realm client '{client.Id}' requires a distinct run secret of at least 32 URL-safe characters.", nameof(clientSecrets));
            }

            secrets.Add(client.Id, secret);
        }

        if (clientSecrets.Count != secrets.Count)
        {
            throw new ArgumentException("Run secrets must match the selected confidential realm clients.", nameof(clientSecrets));
        }

        string import = RenderImport(realm, clients, realmUrl, syntheticTenantId);
        return new PreparedRealm(realmUrl, realm.Realm, import, secrets,
            clients.SingleOrDefault(client => client.BoundOperations is not null), syntheticTenantId);
    }

    private static string RenderImport(RealmContract realm, IReadOnlyList<Client> clients, Uri realmUrl, string tenant)
    {
        HashSet<string> selectedServices = clients.Where(client => client.Surface == "service")
            .Select(client => client.Id).ToHashSet(StringComparer.Ordinal);
        var serviceAudiences = clients.Where(client => client.Surface == "service")
            .ToDictionary(client => client.Id, client => client.Audiences.Where(selectedServices.Contains).Order(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        SortedDictionary<string, SortedSet<string>> audienceRoles = new(StringComparer.Ordinal);
        foreach (Client client in clients.Where(client => client.Surface == "service"))
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
            scopes.Add(PlatformOwnerServices.Scope(scope, AudienceMapper(audience, clientAudience: true)));
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
            if (service)
            {
                string[] audiences = serviceAudiences[client.Id];
                optionalScopes = optionalScopes.Concat(audiences.Select(AudienceScope)).Concat(client.Operations.Select(OperationScope));
                JsonObject assignedRoles = new();
                foreach (string audience in audiences)
                {
                    assignedRoles.Add(audience, Strings(client.Roles));
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
        Client? boundClient, string syntheticTenantId)
    {
        internal string Import => import;

        internal IReadOnlyDictionary<string, string> WorkloadBindingEnvironment(DateTimeOffset now)
        {
            if (boundClient?.BoundOperations is not { } boundOperations)
            {
                return new Dictionary<string, string>();
            }

            long notBefore = now.AddMinutes(-1).ToUnixTimeSeconds();
            long expiresAt = now.AddDays(1).ToUnixTimeSeconds();
            var bindings = new JsonObject
            {
                [EventStoreWorkloadAuthenticationDefaults.TenantBindingClaimType] = new JsonArray(syntheticTenantId),
                [EventStoreWorkloadAuthenticationDefaults.DomainBindingClaimType] = new JsonArray("work"),
            };

            JsonArray grants = new(
                Grant("works-bound-gateway", boundOperations, bindings, allowUnbound: false),
                Grant("works-trusted-effect", [EventStoreWorkloadOperations.TrustedEffect], new JsonObject(), allowUnbound: true));
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [_bindingSignerEnd] = expiresAt.ToString(System.Globalization.CultureInfo.InvariantCulture),
                // Keycloak substitutes the environment value into the JSON stream before parsing the attribute.
                [BindingGrantsEnvironmentName(boundClient.Id)] = JsonEncodedText.Encode(grants.ToJsonString()).ToString(),
            };

            JsonObject Grant(string id, string[] operations, JsonObject permittedBindings, bool allowUnbound) => new()
            {
                ["grantId"] = id,
                ["revision"] = 1,
                ["clientId"] = boundClient.Id,
                ["workload"] = boundClient.Id,
                ["audience"] = boundClient.Audiences.Single(),
                ["operations"] = Strings(operations),
                ["bindings"] = permittedBindings,
                ["allowUnbound"] = allowUnbound,
                ["allowQueryAuthorityDigest"] = false,
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

            return new RealmResource(keycloak, url.AbsoluteUri, credentials);
        }
    }

    internal sealed record RealmResource(IResourceBuilder<ContainerResource> Keycloak, string Authority,
        IReadOnlyDictionary<string, IResourceBuilder<ParameterResource>> Credentials)
    {
        internal IResourceBuilder<ProjectResource> ConfigureValidator(IResourceBuilder<ProjectResource> resource, IConfiguration configuration,
            string userAudience, string workloadAudience)
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
                    .WithEnvironment(prefix + "AllowedAlgorithms__0", "RS256");
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

        internal void ConfigureWorkloadIssuer(IResourceBuilder<ProjectResource> resource, string client, bool allowBindings = false) =>
            resource
                .WithEnvironment("Authentication__WorkloadIssuer__Workload", client)
                .WithEnvironment("Authentication__WorkloadIssuer__ClientId", client)
                .WithEnvironment("Authentication__WorkloadIssuer__AudienceScopePrefix", WorkloadAssertionIssuerOptions.DefaultAudienceScopePrefix)
                .WithEnvironment("Authentication__WorkloadIssuer__OperationScopePrefix", WorkloadAssertionIssuerOptions.DefaultOperationScopePrefix)
                .WithEnvironment("Authentication__WorkloadIssuer__EnableResourceBindingRequests", allowBindings ? "true" : "false")
                .WithEnvironment("Authentication__WorkloadIssuer__ClientSecret", Credentials[client]);
    }
}
