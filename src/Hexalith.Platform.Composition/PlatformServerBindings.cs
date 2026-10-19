namespace Hexalith.Platform.Composition;

using System.Text.Json;
using Hexalith.Builds.Tooling.Manifest;
using Hexalith.Builds.Tooling.Workspace;

/// <summary>Resolves every executable and configuration binding before resources are added.</summary>
public static class PlatformServerBindings
{
    /// <summary>Gets the owner project paths; these are executable identities, not another source mapping.</summary>
    public static IReadOnlyDictionary<string, string> ReferenceProjects { get; } = new Dictionary<string, string>
    {
        ["eventstore/gateway"] = "src/Hexalith.EventStore/Hexalith.EventStore.csproj",
        ["tenants/domain"] = "src/Hexalith.Tenants/Hexalith.Tenants.csproj",
        ["tenants/api"] = "src/Hexalith.Tenants.Api/Hexalith.Tenants.Api.csproj",
        ["memories/server"] = "src/Hexalith.Memories.Server/Hexalith.Memories.Server.csproj",
        ["memories/clock"] = "src/Hexalith.Memories.AccessTelemetry.Clock/Hexalith.Memories.AccessTelemetry.Clock.csproj",
        ["memories/telemetry"] = "src/Hexalith.Memories.AccessTelemetry/Hexalith.Memories.AccessTelemetry.csproj",
    };
    /// <summary>Gets the released owner image identities, with versions resolved solely from Builds.</summary>
    public static IReadOnlyDictionary<string, string> ReferenceImages { get; } = new Dictionary<string, string>
    {
        ["tenants/domain"] = "registry.hexalith.com/tenants",
        ["tenants/api"] = "registry.hexalith.com/tenants-api",
        ["memories/server"] = "registry.hexalith.com/memories",
        ["memories/clock"] = "registry.hexalith.com/memories-access-telemetry-clock",
        ["memories/telemetry"] = "registry.hexalith.com/memories-access-telemetry",
    };
    /// <summary>Gets the repository identity owning a declaration.</summary>
    public static string Repository(string module) => module switch
    {
        "eventstore" => "Hexalith.EventStore", "tenants" => "Hexalith.Tenants", "memories" => "Hexalith.Memories",
        "mcpcli" => "Hexalith.McpCli",
        _ => "Hexalith." + char.ToUpperInvariant(module[0]) + module[1..],
    };

    /// <summary>Resolves all enabled servers, including their declared readiness and tasks.</summary>
    public static IReadOnlyList<PlatformServerBinding> Resolve(PlatformCompositionOptions options, IReadOnlyList<PlatformModuleDeclaration> declarations, PlatformDaprBindings? dapr = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(declarations);
        dapr ??= PlatformDaprBindings.Resolve(declarations);
        List<PlatformServerBinding> result = [];
        Dictionary<string, string> secretValues = new(StringComparer.Ordinal);
        foreach (PlatformModuleDeclaration module in declarations)
        {
            string id = PlatformDeclarationSet.Id(module.Declaration);
            if (id == "timesheets" && !options.TimesheetsEnabled)
                throw Failure(module, "timesheets/host", "Platform:Timesheets:Enabled", "explicit Development/source opt-in is required");
            foreach (JsonElement server in module.Declaration.GetProperty("identity").GetProperty("servers").EnumerateArray().Where(s => s.GetProperty("enabled").GetBoolean()))
            {
                string serverId = server.GetProperty("id").GetString()!;
                string identity = id + "/" + serverId;
                string? project = null;
                string? image = null;
                SourceMappingEntry? source = options.Mapping.Entries.SingleOrDefault(e => e.Identity.Equals(Repository(id), StringComparison.OrdinalIgnoreCase));
                if (options.Mapping.Mode == WorkspaceMode.Source)
                {
                    string? relative = options.Projects.GetValueOrDefault(identity);
                    if (identity == "timesheets/host" && relative is null) relative = PlatformTimesheetsBinding.Project;
                    if (source?.Origin != "source" || source.Path is null || relative is null)
                        throw Failure(module, identity, "project", "mapped source project is unavailable");
                    project = Path.GetFullPath(Path.Combine(source.Path, relative));
                    if (identity == "timesheets/host" && project != Path.GetFullPath(Path.Combine(source.Path, PlatformTimesheetsBinding.Project)))
                        throw Failure(module, identity, project, "only the actual mapped Timesheets host project is supported");
                    // ContainsProject uses Builds' RepositoryPathResolver. Limit this validation view to
                    // the selected owner so a link into another mapped repository cannot pass containment.
                    if (!(options.Mapping with { Entries = [source] }).ContainsProject(project))
                        throw Failure(module, identity, project, "mapped project is missing or physically outside its owner root");
                }
                else if (identity == "eventstore/gateway")
                {
                    project = options.EventStoreSeedProject;
                    if (project is null || !File.Exists(project)) throw Failure(module, identity, project ?? "EventStoreHost", "package-mode EventStoreHost seed is unavailable");
                }
                else
                {
                    if (!options.Images.TryGetValue(identity, out string? repository) || string.IsNullOrWhiteSpace(repository)) throw Failure(module, identity, "image", "released image is unavailable");
                    image = repository + ":" + Version(options, id, module.Source);
                }
                JsonElement[] probes = module.Declaration.GetProperty("lifecycle").GetProperty("readiness").EnumerateArray().Where(r => r.GetProperty("server").GetString() == serverId).ToArray();
                if (probes.Length != 1) throw Failure(module, identity, "lifecycle.readiness", "this Platform model requires exactly one readiness check per enabled server");
                JsonElement readiness = probes[0];
                if (readiness.GetProperty("kind").GetString() != "http") throw Failure(module, identity, "lifecycle.readiness", "this Platform model requires a declared HTTP readiness check");
                Dictionary<string, string> environment = PlatformLocalProfile.Bind(options, module, serverId, secretValues);
                foreach (var pair in dapr.Environment(id, serverId)) environment[pair.Key] = pair.Value;
                environment["Dapr__DeclarativeSubscriptions"] = "true";
                JsonElement[] tasks = module.Declaration.GetProperty("lifecycle").GetProperty("tasks").EnumerateArray().Select(t => t.Clone()).ToArray();
                foreach (JsonElement task in tasks)
                {
                    string executable = task.GetProperty("executable").GetString()!;
                    string ownerRoot = Hexalith.Builds.Tooling.Runtime.CompositionDeclarationClosure.TaskRepositoryRoot(options.Mapping, module);
                    if (!File.Exists(Path.Combine(ownerRoot, executable))) throw Failure(module, identity, "lifecycle.tasks.executable", $"task '{task.GetProperty("name").GetString()}' executable '{executable}' is unavailable");
                }
                result.Add(new PlatformServerBinding(module, serverId, server.GetProperty("resourceId").GetString()!, server.GetProperty("appId").GetString()!, project, image, readiness.GetProperty("endpoint").GetString()!, environment, tasks));
            }
        }
        if (result.Count(s => s.ResourceId == "eventstore") > 1) throw new InvalidDataException("eventstore: identity.servers: exactly one gateway is permitted.");
        if (options.TimesheetsEnabled) PlatformTimesheetsBinding.BindLocalAuthentication(result);
        return result;
    }

    private static InvalidDataException Failure(PlatformModuleDeclaration module, string identity, string field, string reason) => new($"{module.Source}: {identity}: {field}: {reason}.");
    internal static string Version(PlatformCompositionOptions options, string module, string source) =>
        new Hexalith.Builds.Tooling.Runtime.CompositionDeclarationOptions(options.Mapping, options.DeclarationPaths)
        {
            PlatformRoot = options.PlatformRoot,
            Workspace = options.RunPlan?.Workspace,
        }.CatalogVersion(module, source);
}
