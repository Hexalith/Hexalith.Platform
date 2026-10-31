namespace Hexalith.Platform.Composition;

using System.Security.Cryptography;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Hexalith.Builds.Tooling.Workspace;

/// <summary>Binds independently observing hosts to the already generated catalog and existing EventStore activation authority.</summary>
internal sealed class PlatformRoutingCatalogBootstrap
{
    private readonly Dictionary<string, Dictionary<string, string>> _environment = new(StringComparer.Ordinal);
#if HEXALITH_PLATFORM_SOURCE_CATALOG
    private PlatformTenantBootstrapAccess? _tenantBootstrap;

    /// <summary>Returns only the actual run-owned Tenants transport delegation required for native setup.</summary>
    internal PlatformTenantBootstrapAccess TenantBootstrapAccess => _tenantBootstrap
        ?? throw new InvalidOperationException("The actual Tenants bootstrap delegation is unavailable.");
#endif
    internal string Directory { get; }

    private PlatformRoutingCatalogBootstrap(PlatformCompositionOptions options, IReadOnlyList<PlatformServerBinding> participants,
        PlatformRoutingCatalog catalog, string rendered, PlatformDaprBindings dapr, IReadOnlyList<PlatformServerBinding> servers)
    {
        Directory = catalog.Directory;
        string[] hosts = participants.Select(server => server.ResourceId).Order(StringComparer.Ordinal).ToArray();
        foreach (var server in participants)
        {
            var environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["EventStore__RouteCatalog__DevelopmentBootstrap__Directory"] = catalog.Directory,
                ["EventStore__RouteCatalog__DevelopmentBootstrap__HostId"] = server.ResourceId,
                ["EventStore__RouteCatalog__DevelopmentBootstrap__CoordinatorHostId"] = "eventstore",
                ["EventStore__RouteCatalog__DevelopmentBootstrap__AttemptId"] = options.RunPlan?.AttemptId ?? catalog.InstanceId,
                ["EventStore__RouteCatalog__DevelopmentBootstrap__InstanceId"] = catalog.InstanceId,
                ["EventStore__RouteCatalog__DevelopmentBootstrap__CatalogPath"] = catalog.CatalogPath,
                ["EventStore__RouteCatalog__DevelopmentBootstrap__RootDigest"] = catalog.RootDigest,
                ["EventStore__RouteCatalog__DevelopmentBootstrap__ComponentPath"] = Path.Combine(rendered, "routing-catalog.yaml"),
                ["EventStore__RouteCatalog__DevelopmentBootstrap__AccessControlPath"] = Path.Combine(rendered, "accesscontrol." + server.AppId + ".yaml"),
                ["EventStore__RouteCatalog__StateStore"] = AuthorityName(catalog.InstanceId),
                ["EventStore__RouteCatalog__StateKey"] = catalog.InstanceId + "-authority",
                ["EventStore__DomainService__AppId"] = server.AppId,
                ["EventStore__DomainService__ServiceVersion"] = "v1",
                ["Authentication__Workload__Audience"] = server.AppId,
            };
            for (int index = 0; index < hosts.Length; index++) environment["EventStore__RouteCatalog__DevelopmentBootstrap__RequiredHostIds__" + index] = hosts[index];
            string[] selected = servers.Select(item => item.AppId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            for (int index = 0; index < selected.Length; index++) environment["EventStore__RouteCatalog__DevelopmentBootstrap__SelectedAppIds__" + index] = selected[index];
            environment["EventStore__RouteCatalog__DevelopmentBootstrap__DeclaredInvocations"] = JsonSerializer.Serialize(dapr.DeclaredInvocations(server.AppId));
            _environment.Add(server.ResourceId, environment);
        }
        var gateway = _environment["eventstore"];
        gateway["Authentication__DaprInternal__Audience"] = participants.Single(server => server.ResourceId == "eventstore").AppId;
        if (_environment.TryGetValue("memories", out var memories)) memories["EventStore__CommandGateway__Audience"] = gateway["Authentication__DaprInternal__Audience"];
        gateway["EventStore__IdempotencyAdmission__Enabled"] = "true";
        gateway["EventStore__IdempotencyAdmission__DigestKeySource"] = "Configuration";
        gateway["EventStore__IdempotencyAdmission__ActiveDigestKeyVersion"] = "development-v1";
        gateway["EventStore__IdempotencyAdmission__DigestKeys__development-v1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        gateway["EventStore__IdempotencyAdmission__DigestKeySecretStoreName"] = "secretstore";
        gateway["EventStore__IdempotencyAdmission__DigestKeySecretName"] = "eventstore-idempotency-ring";
        gateway["EventStore__IdempotencyAdmission__DigestKeySecretGeneration"] = "development-v1";
        for (int index = 0; index < catalog.OwnerAssemblies.Count; index++) gateway["EventStore__RouteCatalog__DevelopmentBootstrap__OwnerAssemblies__" + index] = catalog.OwnerAssemblies[index];
        for (int index = 0; index < catalog.AdapterAssemblies.Count; index++) gateway["EventStore__RouteCatalog__DevelopmentBootstrap__AdapterAssemblies__" + index] = catalog.AdapterAssemblies[index];
        foreach (var participant in participants.Where(server => server.AppId is "tenants" or "memories"))
        {
            using var proofKey = RSA.Create(2048);
            _environment[participant.ResourceId][participant.AppId == "tenants" ? "Authentication__BoundStreamRead__PrivateKey"
                : "EventStore__CommandGateway__BoundCommand__PrivateKeyPkcs8Base64"] = Convert.ToBase64String(proofKey.ExportPkcs8PrivateKey());
            gateway["Authentication__BoundStreamRead__PublicKeys__" + participant.AppId] = Convert.ToBase64String(proofKey.ExportSubjectPublicKeyInfo());
            var domains = catalog.Envelope.Declarations.Where(declaration => catalog.Envelope.Routes.Any(route => route.RouteId == declaration.RouteId
                && (route.AppId == participant.AppId || participant.AppId == "memories" && route.AppId.StartsWith("memories-", StringComparison.Ordinal))))
                .Select(declaration => declaration.Domain).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
            gateway[(participant.AppId == "tenants" ? "Authentication__BoundStreamRead__AllowedDomains__" : "Authentication__BoundCommand__AllowedDomains__") + participant.AppId] = string.Join(',', domains);
#if HEXALITH_PLATFORM_SOURCE_CATALOG
            if (participant.AppId == "tenants" && options.RunPlan?.SelectedTest?.ModuleId == "parties")
            {
                string bootstrapDomain = catalog.Envelope.Declarations.Single(declaration => declaration.Execution?.HandlerType
                    == typeof(Hexalith.Tenants.Contracts.Commands.BootstrapGlobalAdmin).FullName).Domain;
                gateway["Authentication__BoundCommand__AllowedDomains__" + participant.AppId] = bootstrapDomain;
                _tenantBootstrap = new(participant.ResourceId, participant.AppId,
                    _environment[participant.ResourceId]["Authentication__BoundStreamRead__PrivateKey"],
                    participant.Environment.GetValueOrDefault("DAPR_API_TOKEN"), participant.Environment["Authentication__JwtBearer__Issuer"]);
            }
#endif
        }
        foreach (var group in catalog.Envelope.Declarations.Where(d => d.Kind == "command").GroupBy(d => (d.Domain, d.ContractVersion)))
        {
            var target = catalog.Envelope.Routes.Single(r => r.RouteId == group.First().RouteId);
            string key = "EventStore__DomainServices__Registrations__wildcard_" + group.Key.Domain + "_" + group.Key.ContractVersion + "__";
            gateway[key + "TenantId"] = "*"; gateway[key + "Domain"] = group.Key.Domain; gateway[key + "Version"] = group.Key.ContractVersion;
            gateway[key + "AppId"] = target.AppId; gateway[key + "MethodName"] = target.Method;
        }
        if (options.RunPlan is { SelectedTest: { } suite } plan)
        {
            var selected = participants.SingleOrDefault(server => PlatformDeclarationSet.Id(server.Module.Declaration) == suite.ModuleId && server.ServerId == "domain");
            if (selected is not null)
            {
                var domains = catalog.Envelope.Declarations.Where(declaration => declaration.Kind is "command" or "query"
                    && catalog.Envelope.Operations!.Any(operation => operation.RouteId == declaration.RouteId && operation.Ingress == "public")
                    && catalog.Envelope.Routes.Any(route => route.RouteId == declaration.RouteId && route.AppId == selected.AppId))
                    .Select(declaration => declaration.Domain).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                File.WriteAllBytes(Path.Combine(catalog.Directory, "native-scope.json"), JsonSerializer.SerializeToUtf8Bytes(new
                {
                    plan.RunId, ModuleId = suite.ModuleId, RoutingCatalogRoot = catalog.RootDigest,
                    Domains = domains, Tenants = new[] { plan.TenantNamespace, "system" },
                }));
            }
            File.WriteAllBytes(Path.Combine(catalog.Directory, "selected-closure.json"), JsonSerializer.SerializeToUtf8Bytes(new
            {
                plan.RunId, hosts = participants.Select(server => new { server.ResourceId, server.AppId }).ToArray(),
                callerClosure = plan.Modules.Select(module => module.ModuleId).ToArray(),
            }));
        }
    }

    internal static string AuthorityName(string instance) => "routing-" + instance.ToLowerInvariant();

    internal static PlatformRoutingCatalogBootstrap? Create(PlatformCompositionOptions options, IReadOnlyList<PlatformServerBinding> servers,
        PlatformDaprBindings dapr, string rendered)
    {
        // A gateway-only infrastructure graph has no enrolled business operations.
        if (!servers.Any(server => server.ResourceId == "eventstore") || !servers.Any(server => server.ServerId == "domain")) return null;
        if (options.Mapping.Mode != WorkspaceMode.Source)
            throw new InvalidDataException("Run-scoped catalog package/CI composition is unavailable until the EventStore owner SDK and adapters ship and the Builds catalog pins are updated. Use the mapped Debug/source lane for local verification.");
        var participants = servers.Where(server => server.ResourceId == "eventstore" || server.ServerId == "domain"
            || server.ResourceId is "tenants-api" or "memories").ToArray();
        var selectedApps = servers.Select(server => server.AppId).ToHashSet(StringComparer.Ordinal);
        var enrolledApps = participants.Select(server => server.AppId).ToHashSet(StringComparer.Ordinal);
        foreach (var receiver in participants)
        foreach (var grant in dapr.DeclaredInvocations(receiver.AppId))
            if (selectedApps.Contains(grant.CallerAppId) && !enrolledApps.Contains(grant.CallerAppId)
                && !(grant.Method.Trim('/') is "ready" or "health" or "alive" && grant.HttpMethods.SequenceEqual(["GET"], StringComparer.Ordinal)))
                throw new InvalidDataException($"Selected caller '{grant.CallerAppId}' has a gateway/participant grant to '{receiver.AppId}/{grant.Method}' but lacks catalog enrollment; declaration-driven participant discovery is required by story 3.6.");
        var catalog = PlatformRoutingCatalog.Generate(options, servers, rendered);
        dapr.BindRoutingCatalog(AuthorityName(catalog.InstanceId), participants.Select(server => server.AppId).ToArray());
        return new(options, participants, catalog, rendered, dapr, servers);
    }

    internal bool Participates(PlatformServerBinding server) => _environment.ContainsKey(server.ResourceId);

    internal void Configure<T>(IResourceBuilder<T> resource, PlatformServerBinding server) where T : IResourceWithEnvironment
    {
        if (_environment.TryGetValue(server.ResourceId, out var settings))
            foreach (var pair in settings) resource.WithEnvironment(pair.Key, pair.Value);
    }
}
