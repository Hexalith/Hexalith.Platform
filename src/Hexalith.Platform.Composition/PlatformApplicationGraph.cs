namespace Hexalith.Platform.Composition;

using System.Text.Json;
using Hexalith.Builds.Tooling.Manifest;

/// <summary>Declaration-derived workload metadata, resolved without local providers or credentials.</summary>
public sealed record PlatformApplicationGraph(IReadOnlyList<PlatformWorkload> Workloads, PlatformDaprBindings Dapr)
{
    /// <summary>Resolves the same enabled server and dependency closure for every profile.</summary>
    public static PlatformApplicationGraph Resolve(PlatformCompositionOptions options) => Resolve(PlatformDeclarationSet.Load(options));

    internal static PlatformApplicationGraph Resolve(IReadOnlyList<PlatformModuleDeclaration> declarations, PlatformDaprBindings? dapr = null)
    {
        List<PlatformWorkload> workloads = [];
        foreach (PlatformModuleDeclaration module in declarations)
        foreach (JsonElement server in module.Declaration.GetProperty("identity").GetProperty("servers").EnumerateArray().Where(s => s.GetProperty("enabled").GetBoolean()))
        {
            string serverId = server.GetProperty("id").GetString()!;
            string identity = PlatformDeclarationSet.Id(module.Declaration) + "/" + serverId;
            JsonElement[] probes = module.Declaration.GetProperty("lifecycle").GetProperty("readiness").EnumerateArray().Where(r => r.GetProperty("server").GetString() == serverId).ToArray();
            if (probes.Length != 1)
                throw new InvalidDataException($"{module.Source}: {identity}: lifecycle.readiness: this Platform model requires exactly one readiness check per enabled server.");
            if (probes[0].GetProperty("kind").GetString() != "http")
                throw new InvalidDataException($"{module.Source}: {identity}: lifecycle.readiness: this Platform model requires a declared HTTP readiness check.");
            workloads.Add(new(module, identity, serverId, server.GetProperty("resourceId").GetString()!, server.GetProperty("appId").GetString()!, probes[0].GetProperty("endpoint").GetString()!,
                module.Declaration.GetProperty("lifecycle").GetProperty("tasks").EnumerateArray().Select(t => t.Clone()).ToArray()));
        }
        if (workloads.Count(w => w.ResourceId == "eventstore") > 1) throw new InvalidDataException("eventstore: identity.servers: exactly one gateway is permitted.");
        return new(workloads, dapr ?? PlatformDaprBindings.Resolve(declarations));
    }
}

/// <summary>A server identity and lifecycle metadata before environment-specific binding.</summary>
public sealed record PlatformWorkload(PlatformModuleDeclaration Module, string Identity, string ServerId, string ResourceId, string AppId, string ReadinessEndpoint, IReadOnlyList<JsonElement> Tasks);
