namespace Hexalith.Platform.Composition;

using System.Text.Json;
using System.Security.Cryptography;
using Hexalith.Builds.Tooling.Manifest;

/// <summary>Explicit local configuration bindings keyed by owner, server and complete configuration key.</summary>
public static class PlatformLocalProfile
{
    /// <summary>Resolves all required inputs and converts structured sections to .NET environment keys.</summary>
    public static Dictionary<string, string> Bind(PlatformCompositionOptions options, PlatformModuleDeclaration module, string server)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(module);
        Dictionary<string, string> environment = Bind(options, module, server, new Dictionary<string, string>(StringComparer.Ordinal));
        foreach (var pair in PlatformDaprBindings.Resolve([module]).Environment(PlatformDeclarationSet.Id(module.Declaration), server)) environment[pair.Key] = pair.Value;
        return environment;
    }

    internal static Dictionary<string, string> Bind(PlatformCompositionOptions options, PlatformModuleDeclaration module, string server, Dictionary<string, string> secretValues)
    {
        string id = PlatformDeclarationSet.Id(module.Declaration);
        string directory = options.ComponentsDirectory ?? Path.Combine(options.PlatformRoot, "DaprComponents", "reference");
        string profile = Path.Combine(options.PlatformRoot, "src", "Hexalith.Platform.Composition", "Declarations", "local-profile.json");
        Dictionary<string, string> defaults = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(profile))!;
        Dictionary<string, string> environment = new(StringComparer.Ordinal);
        string? clockSources = null;
        string? clockTokens = null;
        foreach (JsonElement extension in module.Declaration.GetProperty("runtime").GetProperty("extensions").EnumerateArray())
        foreach (JsonElement input in extension.GetProperty("configurationInputs").EnumerateArray())
        {
            string key = input.GetProperty("key").GetString()!;
            string scoped = id + "/" + server + "/" + key;
            if (!options.Configuration.TryGetValue(scoped, out string? value) && !defaults.TryGetValue(scoped, out value)) throw new InvalidDataException($"{module.Source}: {scoped}: runtime.extensions.configurationInputs: binding is unavailable.");
            string source = input.GetProperty("source").GetString()!;
            if (source == "secret" && string.IsNullOrEmpty(value))
            {
                string identity = key is "Authentication:JwtBearer:SigningKey" or "EventStore:Authentication:SigningKey" or "Jwt:SigningKey"
                    ? "platform/authentication-signing"
                    : id + "/" + key;
                if (!secretValues.TryGetValue(identity, out value))
                {
                    value = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
                    secretValues.Add(identity, value);
                }
            }
            if (key == "Clock:Sources") clockSources = value;
            if (key == "Clock:SourceAuthenticationTokens")
            {
                clockTokens = value;
                continue;
            }
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
                if (!File.Exists(value)) throw new FileNotFoundException($"{module.Source}: {scoped}: local component '{value}' is unavailable.", value);
            }
            ReadOnlySpan<char> structuredValue = value.AsSpan().TrimStart();
            if (source is not ("secret" or "dynamic-secret") && !structuredValue.IsEmpty && structuredValue[0] is '[' or '{')
            {
                try
                {
                    using JsonDocument structured = JsonDocument.Parse(value);
                    Flatten(environment, key, structured.RootElement);
                }
                catch (JsonException exception)
                {
                    throw new InvalidDataException($"{module.Source}: {scoped}: structured configuration is invalid: {exception.Message}", exception);
                }
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
        if (clockSources is not null || clockTokens is not null)
        {
            try { MapClockTokens(module.Source, id, server, clockSources, clockTokens, environment); }
            catch (JsonException exception) { throw new InvalidDataException($"{module.Source}: {id}/{server}/Clock:SourceAuthenticationTokens: structured configuration is invalid: {exception.Message}", exception); }
        }
        if (id == "memories" && options.RetentionProfile is { } retention) retention.Configure(environment, server);
        return environment;
    }

    private static void MapClockTokens(string source, string module, string server, string? sources, string? tokens, Dictionary<string, string> environment)
    {
        if (sources is null || tokens is null) throw new InvalidDataException($"{source}: {module}/{server}/Clock:SourceAuthenticationTokens: source or token binding is unavailable.");
        using JsonDocument sourceDocument = JsonDocument.Parse(sources);
        using JsonDocument tokenDocument = JsonDocument.Parse(tokens);
        if (sourceDocument.RootElement.ValueKind != JsonValueKind.Array || tokenDocument.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"{source}: {module}/{server}/Clock:SourceAuthenticationTokens: sources must be an array and tokens must be an object.");
        Dictionary<string, int> indices = new(StringComparer.Ordinal);
        int index = 0;
        foreach (JsonElement clockSource in sourceDocument.RootElement.EnumerateArray())
        {
            string? id = clockSource.TryGetProperty("Id", out JsonElement upper) ? upper.GetString() : clockSource.TryGetProperty("id", out JsonElement lower) ? lower.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || !indices.TryAdd(id, index++))
                throw new InvalidDataException($"{source}: {module}/{server}/Clock:Sources: source ID is missing or duplicated.");
        }
        foreach (JsonProperty token in tokenDocument.RootElement.EnumerateObject())
        {
            if (!indices.TryGetValue(token.Name, out int sourceIndex))
                throw new InvalidDataException($"{source}: {module}/{server}/Clock:SourceAuthenticationTokens:{token.Name}: no source has this ID.");
            if (token.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(token.Value.GetString()))
                throw new InvalidDataException($"{source}: {module}/{server}/Clock:SourceAuthenticationTokens:{token.Name}: token is unavailable.");
            environment[$"Clock__Sources__{sourceIndex}__AuthenticationToken"] = token.Value.GetString()!;
        }
        foreach (string id in indices.Keys)
            if (!tokenDocument.RootElement.TryGetProperty(id, out _))
                throw new InvalidDataException($"{source}: {module}/{server}/Clock:Sources:{id}: authentication token is unavailable.");
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
