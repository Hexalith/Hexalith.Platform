namespace Hexalith.Platform.Composition;

using System.Security.Cryptography;
using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

using Hexalith.Builds.Tooling.Runtime;
using Hexalith.Builds.Tooling.Workspace;

/// <summary>Private source-only catalog and asymmetric owner proof bindings selected by one native test runner.</summary>
internal sealed class PlatformDevelopmentCatalog
{
    private readonly Dictionary<string, Dictionary<string, string>> _environment = new(StringComparer.Ordinal);

    private PlatformDevelopmentCatalog(PlatformCompositionOptions options, IReadOnlyList<PlatformServerBinding> participants, string rendered)
    {
        var plan = options.RunPlan!;
        Directory = Path.Combine(plan.Workspace, "development-route-catalog");
        CompositionWorkspace.CreatePrivateDirectory(Directory);
        string[] hosts = participants.Select(server => server.ResourceId).Order(StringComparer.Ordinal).ToArray();
        foreach (var server in participants)
        {
            var environment = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["EventStore__RouteCatalog__DevelopmentBootstrap__Directory"] = Directory,
                ["EventStore__RouteCatalog__DevelopmentBootstrap__HostId"] = server.ResourceId,
                ["EventStore__RouteCatalog__DevelopmentBootstrap__CoordinatorHostId"] = "eventstore",
                ["EventStore__RouteCatalog__DevelopmentBootstrap__AttemptId"] = plan.RunId,
                ["EventStore__RouteCatalog__DevelopmentBootstrap__TenantNamespace"] = plan.TenantNamespace,
                ["EventStore__RouteCatalog__DevelopmentBootstrap__NativeModuleId"] = plan.SelectedTest!.ModuleId,
                ["EventStore__RouteCatalog__DevelopmentBootstrap__ComponentPath"] = Path.Combine(rendered, "route-catalog.yaml"),
                ["EventStore__RouteCatalog__DevelopmentBootstrap__AccessControlPath"] = Path.Combine(rendered, "accesscontrol." + server.AppId + ".yaml"),
                ["EventStore__RouteCatalog__StateStore"] = "route-catalog",
                ["EventStore__RouteCatalog__StateKey"] = "route-catalog-" + plan.RunId,
                ["EventStore__DomainService__AppId"] = server.AppId,
                ["EventStore__DomainService__ServiceVersion"] = "v1",
                ["Authentication__Workload__Audience"] = server.AppId,
            };
            for (int index = 0; index < hosts.Length; index++) environment["EventStore__RouteCatalog__DevelopmentBootstrap__RequiredHostIds__" + index] = hosts[index];
            _environment.Add(server.ResourceId, environment);
        }
        var gateway = _environment["eventstore"];
        gateway["EventStore__IdempotencyAdmission__Enabled"] = "true";
        gateway["EventStore__IdempotencyAdmission__DigestKeySource"] = "Configuration";
        gateway["EventStore__IdempotencyAdmission__ActiveDigestKeyVersion"] = "development-v1";
        gateway["EventStore__IdempotencyAdmission__DigestKeys__development-v1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        gateway["EventStore__IdempotencyAdmission__DigestKeySecretStoreName"] = "development-configuration";
        gateway["EventStore__IdempotencyAdmission__DigestKeySecretName"] = "run-idempotency-ring";
        gateway["EventStore__IdempotencyAdmission__DigestKeySecretGeneration"] = plan.RunId;
        BindProof("tenants", _environment["tenants"], gateway, "Authentication__BoundStreamRead__PrivateKey");
        BindProof("memories", _environment["memories"], gateway, "EventStore__CommandGateway__BoundCommand__PrivateKeyPkcs8Base64");
        File.WriteAllText(Path.Combine(Directory, "selected-closure.json"), JsonSerializer.Serialize(new
        {
            plan.RunId, hosts = participants.Select(server => new { server.ResourceId, server.AppId, server.ProjectPath }).ToArray(),
            callerClosure = plan.Modules.Select(module => module.ModuleId).ToArray(),
            purpose = "Development source qualification only; no immutable package receipt",
        }));
    }

    /// <summary>Gets the private run coordination directory, containing no credential material.</summary>
    public string Directory { get; }

    /// <summary>Selects the exact six actual catalog participants for the declared Parties native suite.</summary>
    public static PlatformDevelopmentCatalog? Create(PlatformCompositionOptions options, IReadOnlyList<PlatformServerBinding> servers,
        PlatformDaprBindings dapr, string rendered)
    {
        if (options.RunPlan?.SelectedTest?.ModuleId != "parties") return null;
        if (options.Mapping.Mode != WorkspaceMode.Source)
            throw new InvalidDataException("The Development native catalog requires authoritative published owner adapters before package qualification.");
        string[] roles = ["eventstore/gateway", "parties/domain", "tenants/domain", "tenants/api", "memories/server", "memories/domain"];
        var participants = roles.Select(role => servers.Single(server => PlatformDeclarationSet.Id(server.Module.Declaration) + "/" + server.ServerId == role)).ToArray();
        dapr.BindDevelopmentCatalog(participants.Select(server => server.AppId).ToArray(), servers.Select(server => server.AppId).ToArray(),
            participants[2].AppId, participants[1].AppId, participants[2].ReadinessEndpoint);
        return new(options, participants, rendered);
    }

    /// <summary>Identifies participants whose startup cannot wait for the mutually dependent ready catalog.</summary>
    public bool Participates(PlatformServerBinding server) => _environment.ContainsKey(server.ResourceId);

    /// <summary>Passes private runner bindings directly to the actual Aspire-managed host.</summary>
    public void Configure<T>(IResourceBuilder<T> resource, PlatformServerBinding server) where T : IResourceWithEnvironment
    {
        if (!_environment.TryGetValue(server.ResourceId, out var environment)) return;
        foreach (var pair in environment) resource.WithEnvironment(pair.Key, pair.Value);
    }

    private static void BindProof(string caller, Dictionary<string, string> owner, Dictionary<string, string> gateway,
        string privateKeySetting)
    {
        using var rsa = RSA.Create(2048);
        owner[privateKeySetting] = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
        gateway["Authentication__BoundStreamRead__PublicKeys__" + caller] = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
    }
}
