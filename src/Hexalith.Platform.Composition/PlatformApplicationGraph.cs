namespace Hexalith.Platform.Composition;

using System.Globalization;
using System.Numerics;
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
        {
            string moduleId = PlatformDeclarationSet.Id(module.Declaration);
            JsonElement lifecycle = module.Declaration.GetProperty("lifecycle");
            ValidateNames(module, moduleId, lifecycle);
            ValidateResourceQuantities(module, moduleId);

            foreach (JsonElement server in module.Declaration.GetProperty("identity").GetProperty("servers").EnumerateArray().Where(s => s.GetProperty("enabled").GetBoolean()))
            {
                string serverId = server.GetProperty("id").GetString()!;
                string identity = moduleId + "/" + serverId;
                JsonElement[] probes = lifecycle.GetProperty("readiness").EnumerateArray().Where(r => r.GetProperty("server").GetString() == serverId).ToArray();
                if (probes.Length != 1)
                    throw new InvalidDataException($"{module.Source}: {identity}: lifecycle.readiness: this Platform model requires exactly one readiness check per enabled server.");
                if (probes[0].GetProperty("kind").GetString() != "http")
                    throw new InvalidDataException($"{module.Source}: {identity}: lifecycle.readiness: this Platform model requires a declared HTTP readiness check.");
                workloads.Add(new(module, identity, serverId, server.GetProperty("resourceId").GetString()!, server.GetProperty("appId").GetString()!, probes[0].GetProperty("endpoint").GetString()!,
                    lifecycle.GetProperty("tasks").EnumerateArray().Select(t => t.Clone()).ToArray()));
            }
        }
        if (workloads.Count(w => w.ResourceId == "eventstore") > 1) throw new InvalidDataException("eventstore: identity.servers: exactly one gateway is permitted.");
        return new(workloads, dapr ?? PlatformDaprBindings.Resolve(declarations));
    }

    private static void ValidateNames(PlatformModuleDeclaration module, string moduleId, JsonElement lifecycle)
    {
        foreach (string collection in new[] { "tasks", "recoveryHooks", "fenceHooks" })
        {
            RequireUniqueNames(lifecycle.GetProperty(collection), $"lifecycle.{collection}", module.Source, moduleId);
        }

        JsonElement interfaces = module.Declaration.GetProperty("surfaces").GetProperty("interfaces");
        RequireUniqueNames(interfaces, "surfaces.interfaces", module.Source, moduleId);
        foreach (JsonElement surface in interfaces.EnumerateArray())
        {
            string? name = surface.GetProperty("name").GetString();
            RequireUniqueNames(surface.GetProperty("operations"), $"surfaces.interfaces[{name}].operations", module.Source, moduleId);
        }
    }

    private static void ValidateResourceQuantities(PlatformModuleDeclaration module, string moduleId)
    {
        foreach (JsonElement resource in module.Declaration.GetProperty("runtime").GetProperty("resources").EnumerateArray())
        {
            JsonElement requests = resource.GetProperty("requests");
            JsonElement limits = resource.GetProperty("limits");
            foreach (string quantity in new[] { "cpu", "memoryMiB" })
            {
                if (requests.TryGetProperty(quantity, out JsonElement request)
                    && limits.TryGetProperty(quantity, out JsonElement limit)
                    && CompareResourceQuantity(request, limit) > 0)
                {
                    string? serverId = resource.GetProperty("server").GetString();
                    throw new InvalidDataException($"{module.Source}: {moduleId}/{serverId}: runtime.resources.requests.{quantity}: request exceeds the declared limit.");
                }
            }
        }
    }

    private static int CompareResourceQuantity(JsonElement request, JsonElement limit)
    {
        var left = NormalizeResourceQuantity(request);
        var right = NormalizeResourceQuantity(limit);
        if (left.Sign != right.Sign)
        {
            return left.Sign.CompareTo(right.Sign);
        }

        int magnitude = left.DecimalPosition.CompareTo(right.DecimalPosition);
        return left.Sign * (magnitude != 0 ? magnitude : string.CompareOrdinal(left.Digits, right.Digits));
    }

    private static (int Sign, BigInteger DecimalPosition, string Digits) NormalizeResourceQuantity(JsonElement value)
    {
        ReadOnlySpan<char> text = value.GetRawText();
        bool negative = text[0] == '-';
        if (negative)
        {
            text = text[1..];
        }

        int exponentIndex = text.IndexOfAny('e', 'E');
        ReadOnlySpan<char> significand = exponentIndex < 0 ? text : text[..exponentIndex];
        BigInteger exponent = exponentIndex < 0 ? BigInteger.Zero
            : BigInteger.Parse(text[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        int decimalPoint = significand.IndexOf('.');
        int fractionalDigits = decimalPoint < 0 ? 0 : significand.Length - decimalPoint - 1;
        string digits = significand.ToString().Replace(".", string.Empty, StringComparison.Ordinal).TrimStart('0');

        // Compare the decimal positions first, then the significant digits, without expanding the exponent or rounding.
        return digits.Length == 0 ? (0, BigInteger.Zero, string.Empty)
            : (negative ? -1 : 1, exponent + digits.Length - fractionalDigits, digits.TrimEnd('0'));
    }

    private static void RequireUniqueNames(JsonElement collection, string field, string source, string moduleId)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonElement entry in collection.EnumerateArray())
        {
            string name = entry.GetProperty("name").GetString()!;
            if (!names.Add(name))
            {
                throw new InvalidDataException($"{source}: {moduleId}: {field}: duplicate name '{name}'.");
            }
        }
    }
}

/// <summary>A server identity and lifecycle metadata before environment-specific binding.</summary>
public sealed record PlatformWorkload(PlatformModuleDeclaration Module, string Identity, string ServerId, string ResourceId, string AppId, string ReadinessEndpoint, IReadOnlyList<JsonElement> Tasks);
