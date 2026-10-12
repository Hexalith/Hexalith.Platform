namespace Hexalith.Platform.Composition;

using System.Text.Json;
using Hexalith.Builds.Tooling.Manifest;

/// <summary>Explicit local configuration bindings keyed by owner, server and complete configuration key.</summary>
public static class PlatformLocalProfile
{
    /// <summary>Resolves all required inputs and converts structured sections to .NET environment keys.</summary>
    public static Dictionary<string, string> Bind(PlatformCompositionOptions options, PlatformModuleDeclaration module, string server)
    {
        string id = PlatformDeclarationSet.Id(module.Declaration);
        string directory = options.ComponentsDirectory ?? Path.Combine(options.PlatformRoot, "DaprComponents", "reference");
        string profile = Path.Combine(options.PlatformRoot, "src", "Hexalith.Platform.Composition", "Declarations", "local-profile.json");
        Dictionary<string, string> defaults = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(profile))!;
        Dictionary<string, string> environment = new(StringComparer.Ordinal);
        foreach (JsonElement extension in module.Declaration.GetProperty("runtime").GetProperty("extensions").EnumerateArray())
        foreach (JsonElement input in extension.GetProperty("configurationInputs").EnumerateArray())
        {
            string key = input.GetProperty("key").GetString()!;
            string scoped = id + "/" + server + "/" + key;
            if (!options.Configuration.TryGetValue(scoped, out string? value) && !defaults.TryGetValue(scoped, out value)) throw new InvalidDataException($"{module.Source}: {scoped}: runtime.extensions.configurationInputs: binding is unavailable.");
            string? component = key switch
            {
                "EventStore:DaprConfigPath" => "accesscontrol.eventstore.yaml", "Tenants:DaprConfigPath" => "accesscontrol.tenants.yaml",
                "EventStore:StateStoreComponentPath" => "statestore.yaml", "EventStore:PubSubComponentPath" => "pubsub.yaml",
                "Memories:LlmComponentPath" => "llm.yaml", "AccessTelemetryLifecycle:StateStoreComponentPath" => "telemetry-state.yaml",
                "AccessTelemetryLifecycle:ConfigurationStoreComponentPath" => "telemetry-config.yaml", _ => null,
            };
            if (component is not null)
            {
                value = Path.Combine(directory, component);
                if (!File.Exists(value)) throw new FileNotFoundException($"{module.Source}: {scoped}: local component is unavailable.", value);
            }
            if (value.StartsWith('[') || value.StartsWith('{'))
            {
                using JsonDocument structured = JsonDocument.Parse(value);
                Flatten(environment, key, structured.RootElement);
            }
            else environment[key.Replace(":", "__", StringComparison.Ordinal)] = value;
            string? runtime = (id, server, key) switch
            {
                ("eventstore", "gateway", "EventStore:AppChannelToken") => "APP_API_TOKEN",
                ("tenants", "domain", "Tenants:AppChannelToken") => "APP_API_TOKEN",
                ("tenants", "api", "Tenants:ApiDaprToken") => "DAPR_API_TOKEN",
                ("memories", _, "Memories:AppChannelToken") => "APP_API_TOKEN",
                ("memories", _, "Memories:DaprApiToken") => "DAPR_API_TOKEN", _ => null,
            };
            if (runtime is not null) environment[runtime] = value;
        }
        foreach (JsonElement capability in module.Declaration.GetProperty("runtime").GetProperty("dapr").EnumerateArray())
        {
            string key = capability.GetProperty("configurationKey").GetString()!;
            string role = capability.GetProperty("role").GetString()!;
            string kind = capability.GetProperty("capability").GetString()!;
            environment[key.Replace(":", "__", StringComparison.Ordinal)] = role switch
            {
                "telemetry-state" => "access-telemetry-store", "telemetry-secrets" => "access-telemetry-secrets",
                _ => kind switch { "pubsub" => "pubsub", "configuration" => "access-telemetry-config", "secret" => "secretstore", "conversation" => "llm", _ => "statestore" },
            };
        }
        return environment;
    }

    private static void Flatten(Dictionary<string, string> environment, string key, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object) foreach (JsonProperty property in value.EnumerateObject()) Flatten(environment, key + ":" + property.Name, property.Value);
        else if (value.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in value.EnumerateArray()) Flatten(environment, key + ":" + index++, item);
        }
        else environment[key.Replace(":", "__", StringComparison.Ordinal)] = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
    }
}
