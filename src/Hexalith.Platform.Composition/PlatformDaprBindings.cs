namespace Hexalith.Platform.Composition;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.Builds.Tooling.Manifest;
using YamlDotNet.RepresentationModel;

/// <summary>Resolves the local Dapr capability graph before resources exist.</summary>
public sealed class PlatformDaprBindings
{
    private PlatformRetentionProfile? _retentionProfile;
    private readonly Dictionary<string, (string Kind, string Recovery, string Template, HashSet<string> Consumers)> _components = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _environment = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<(string Caller, string Path, string[] Verbs)>> _rules = new(StringComparer.Ordinal);
    private readonly List<(string Name, string Component, string Topic, string Route, string AppId, string? DeadLetter)> _subscriptions = [];

    /// <summary>Gets resolved component names.</summary>
    public IEnumerable<string> ComponentNames => _components.Where(component => component.Value.Consumers.Count > 0).Select(component => component.Key);

    /// <summary>Resolves all selected declarations atomically.</summary>
    public static PlatformDaprBindings Resolve(IReadOnlyList<PlatformModuleDeclaration> declarations, PlatformRetentionProfile? retentionProfile = null)
    {
        PlatformDaprBindings result = new() { _retentionProfile = retentionProfile };
        foreach (PlatformModuleDeclaration module in declarations)
        {
            string owner = PlatformDeclarationSet.Id(module.Declaration);
            Dictionary<string, string> servers = module.Declaration.GetProperty("identity").GetProperty("servers").EnumerateArray()
                .Where(server => server.GetProperty("enabled").GetBoolean()).ToDictionary(server => server.GetProperty("id").GetString()!, server => server.GetProperty("appId").GetString()!, StringComparer.Ordinal);
            foreach (var server in servers)
            {
                result._environment.Add(owner + "/" + server.Key, new(StringComparer.OrdinalIgnoreCase));
                result._rules.Add(server.Value, []);
            }
            foreach (JsonElement capability in module.Declaration.GetProperty("runtime").GetProperty("dapr").EnumerateArray())
            {
                string role = capability.GetProperty("role").GetString()!;
                string kind = capability.GetProperty("capability").GetString()!;
                string recovery = capability.GetProperty("recoveryClass").GetString()!;
                string template = (kind, role) switch
                {
                    ("state", "state") => "statestore.yaml", ("pubsub", "events") => "pubsub.yaml",
                    ("configuration", "rate-limit-configuration") => "configstore.yaml",
                    ("state", "telemetry-state") => "telemetry-state.yaml",
                    ("configuration", "telemetry-configuration") => "telemetry-config.yaml",
                    ("secret", "secrets" or "telemetry-secrets") => "secretstore.yaml",
                    ("conversation", "conversation") => "llm.yaml",
                    _ => throw Failure(module, "runtime.dapr", $"unsupported capability '{kind}' with role '{role}'"),
                };
                string name = Name(kind, role);
                if (result._components.TryGetValue(name, out var existing) && (existing.Kind != kind || existing.Recovery != recovery))
                    throw Failure(module, "runtime.dapr", $"conflicting recovery class for component '{name}'");
                if (!result._components.ContainsKey(name)) result._components.Add(name, (kind, recovery, template, new(StringComparer.Ordinal)));
                IEnumerable<string> selected = capability.TryGetProperty("servers", out JsonElement scopes) ? scopes.EnumerateArray().Select(scope => scope.GetString()!) : servers.Keys;
                foreach (string server in selected.Where(servers.ContainsKey))
                {
                    result._components[name].Consumers.Add(servers[server]);
                    string key = capability.GetProperty("configurationKey").GetString()!.Replace(":", "__", StringComparison.Ordinal);
                    Dictionary<string, string> environment = result._environment[owner + "/" + server];
                    if (environment.TryGetValue(key, out string? previous) && previous != name) throw Failure(module, "runtime.dapr.configurationKey", $"conflicting binding for '{server}/{key}'");
                    environment[key] = name;
                }
            }
            foreach (JsonElement surface in module.Declaration.GetProperty("surfaces").GetProperty("interfaces").EnumerateArray())
            {
                if (!servers.TryGetValue(surface.GetProperty("server").GetString()!, out string? appId) || !surface.TryGetProperty("invocationRules", out JsonElement rules)) continue;
                foreach (JsonElement rule in rules.EnumerateArray())
                    result._rules[appId].Add((rule.GetProperty("caller").GetString()!, rule.GetProperty("path").GetString()!, rule.GetProperty("verbs").EnumerateArray().Select(verb => verb.GetString()!).ToArray()));
            }
            foreach (JsonElement topic in module.Declaration.GetProperty("integration").GetProperty("topics").EnumerateArray())
            {
                if (!topic.TryGetProperty("subscription", out JsonElement subscription)) continue;
                string server = subscription.GetProperty("server").GetString()!;
                if (!servers.TryGetValue(server, out string? appId)) continue;
                string component = Name("pubsub", topic.GetProperty("role").GetString()!);
                if (!result._components.TryGetValue(component, out var binding) || !binding.Consumers.Contains(appId)) throw Failure(module, "integration.topics.subscription.server", "receiver has no bound pubsub capability");
                string name = topic.GetProperty("name").GetString()!;
                JsonElement deadLetter = topic.GetProperty("deadLetter");
                string? target = deadLetter.GetProperty("strategy").GetString() == "topic"
                    ? PhysicalTopic(module.Declaration.GetProperty("integration").GetProperty("topics").EnumerateArray().First(candidate => candidate.GetProperty("name").GetString() == deadLetter.GetProperty("topic").GetString() && candidate.GetProperty("role").GetString() == topic.GetProperty("role").GetString())) : null;
                string identity = "platform-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { owner, server, name, component, PhysicalTopic(topic) }))));
                result._subscriptions.Add((identity, component, PhysicalTopic(topic), subscription.GetProperty("route").GetString()!, appId, target));
            }
        }
        return result;
    }

    /// <summary>Returns the deterministic name of a logical component.</summary>
    public static string Name(string kind, string role) => (kind, role) switch
    {
        ("state", "state") => "statestore", ("pubsub", "events") => "pubsub",
        ("configuration", "rate-limit-configuration") => "configstore",
        ("state", "telemetry-state") => "access-telemetry-store",
        ("secret", "telemetry-secrets") => "access-telemetry-secrets",
        ("configuration", "telemetry-configuration") => "access-telemetry-config",
        ("secret", "secrets") => "secretstore", ("conversation", "conversation") => "llm",
        _ => "platform-" + kind + "-" + role,
    };

    /// <summary>Gets configuration consumed by one selected owner server.</summary>
    internal IReadOnlyDictionary<string, string> Environment(string owner, string server) => _environment[owner + "/" + server];

    /// <summary>Gets the Dapr component implementation type.</summary>
    internal string Type(string name) => _retentionProfile is not null && name == Name("state", "telemetry-state") ? "state.postgresql" : _components[name].Kind switch { "state" => "state.redis", "pubsub" => "pubsub.redis", "secret" => "secretstores.local.file", "configuration" => "configuration.redis", "conversation" => "conversation.echo", _ => throw new InvalidOperationException(name) };

    /// <summary>Gets the rendered filename for a resolved component.</summary>
    internal string FileName(string name) => name == Name("secret", "telemetry-secrets") ? "telemetry-secrets.yaml" : _components[name].Template;

    /// <summary>Checks whether an app consumes a component.</summary>
    internal bool Consumes(string name, string appId) => _components.TryGetValue(name, out var binding) && binding.Consumers.Contains(appId);

    /// <summary>Renders all components, receiver configurations and explicit subscriptions in memory.</summary>
    internal IReadOnlyDictionary<string, string> Render(string directory, int redisPort, int telemetryPort, string secretsPath, string? discovery)
    {
        Dictionary<string, string> files = new(StringComparer.Ordinal);
        foreach (var (name, binding) in _components.Where(pair => pair.Value.Consumers.Count > 0))
        {
            if (_retentionProfile is not null && name == Name("state", "telemetry-state"))
            {
                files.Add(binding.Template, _retentionProfile.RenderComponent(binding.Consumers));
                continue;
            }
            string source = Path.Combine(directory, binding.Template);
            if (!File.Exists(source)) throw new FileNotFoundException($"{source}: runtime.dapr: Platform shared local Dapr component template is unavailable.", source);
            YamlMappingNode component;
            try
            {
                component = Parse(File.ReadAllText(source));
                if (Scalar(component, "apiVersion") != "dapr.io/v1alpha1" || Scalar(component, "kind") != "Component") throw new InvalidDataException("expected a dapr.io/v1alpha1 Component");
                YamlMappingNode metadata = Mapping(component, "metadata");
                _ = Scalar(metadata, "name");
                YamlMappingNode spec = Mapping(component, "spec");
                if (Scalar(spec, "type") != Type(name)) throw new InvalidDataException("component type differs from the bound capability");
                _ = Scalar(spec, "version");
                if (binding.Template == "secretstore.yaml")
                {
                    if (!spec.Children.TryGetValue(new YamlScalarNode("metadata"), out YamlNode? entries) || entries is not YamlSequenceNode sequence) throw new InvalidDataException("spec.metadata must be a sequence");
                    bool boundSecretsFile = false;
                    foreach (YamlNode entry in sequence.Children)
                    {
                        if (entry is not YamlMappingNode item) throw new InvalidDataException("spec.metadata entries must be mappings");
                        if (Scalar(item, "name") == "secretsFile")
                        {
                            item.Children[new YamlScalarNode("value")] = new YamlScalarNode(secretsPath);
                            boundSecretsFile = true;
                        }
                    }
                    if (!boundSecretsFile) throw new InvalidDataException("spec.metadata must contain a secretsFile entry");
                }
                metadata.Children[new YamlScalarNode("name")] = new YamlScalarNode(name);
                component.Children[new YamlScalarNode("scopes")] = new YamlSequenceNode(binding.Consumers.Order(StringComparer.Ordinal).Select(app => new YamlScalarNode(app)));
            }
            catch (Exception exception) when (exception is YamlDotNet.Core.YamlException or InvalidCastException or KeyNotFoundException or InvalidDataException)
            {
                throw new InvalidDataException($"{source}: runtime.dapr: invalid local component template: {exception.Message}", exception);
            }
            string content = Write(component).Replace("localhost:6379", "127.0.0.1:" + (binding.Template == "telemetry-state.yaml" ? telemetryPort : redisPort), StringComparison.Ordinal)
                .Replace("127.0.0.1:6379", "127.0.0.1:" + (binding.Template == "telemetry-state.yaml" ? telemetryPort : redisPort), StringComparison.Ordinal);
            files.Add(name == "access-telemetry-secrets" ? "telemetry-secrets.yaml" : binding.Template, content);
        }
        foreach (var (appId, rules) in _rules)
        {
            var policies = rules.GroupBy(rule => rule.Caller, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal).Select(group =>
            {
                var operations = group.GroupBy(rule => rule.Path, StringComparer.OrdinalIgnoreCase).Select(path => new
                {
                    Path = path.Key.ToLowerInvariant(),
                    Verbs = path.SelectMany(rule => rule.Verbs).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                }).ToArray();
                return new
                {
                    appId = group.Key, @namespace = "default", trustDomain = "public", defaultAction = "deny",
                    // Dapr returns the first populated prefix node. Its single-stage fallback preserves
                    // a parent's base invocation while letting explicit descendants retain their verbs.
                    operations = operations.OrderByDescending(operation => operation.Path.Length).ThenBy(operation => operation.Path, StringComparer.Ordinal).Select(operation => new
                    {
                        name = operations.Any(other => other.Path != operation.Path && other.Path.StartsWith(operation.Path.TrimEnd('/') + "/", StringComparison.Ordinal)) ? operation.Path.TrimEnd('/') + "/*" : operation.Path,
                        httpVerb = operation.Verbs, action = "allow",
                    }).ToArray(),
                };
            }).ToArray();
            string content = JsonSerializer.Serialize(new { apiVersion = "dapr.io/v1alpha1", kind = "Configuration", metadata = new { name = "platform-" + appId }, spec = new { mtls = new { enabled = true, controlPlaneTrustDomain = "localhost", sentryAddress = "localhost:50001" }, features = new[] { new { name = "HotReload", enabled = false } }, accessControl = new { defaultAction = "deny", trustDomain = "public", policies } } });
            if (discovery is not null) content = PlatformAppModel.MergeRunDiscovery(content, discovery);
            files.Add("accesscontrol." + appId + ".yaml", content);
        }
        foreach (var subscription in _subscriptions)
            files.Add("subscription." + subscription.Name + ".yaml", JsonSerializer.Serialize(new { apiVersion = "dapr.io/v2alpha1", kind = "Subscription", metadata = new { name = subscription.Name }, spec = new { pubsubname = subscription.Component, topic = subscription.Topic, routes = new { @default = subscription.Route }, deadLetterTopic = subscription.DeadLetter }, scopes = new[] { subscription.AppId } }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
        return files;
    }

    private static YamlMappingNode Parse(string content)
    {
        YamlStream yaml = new();
        yaml.Load(new StringReader(content));
        if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root) throw new InvalidDataException("expected one YAML mapping document");
        return root;
    }
    private static YamlMappingNode Mapping(YamlMappingNode parent, string key) => parent.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value) && value is YamlMappingNode mapping ? mapping : throw new InvalidDataException($"{key} must be a mapping");
    private static string Scalar(YamlMappingNode parent, string key) => parent.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value) && value is YamlScalarNode { Value: { Length: > 0 } text } ? text : throw new InvalidDataException($"{key} must be a nonempty scalar");
    private static string PhysicalTopic(JsonElement topic) => topic.TryGetProperty("subscription", out JsonElement subscription) && subscription.TryGetProperty("topic", out JsonElement physical) ? physical.GetString()! : topic.GetProperty("name").GetString()!;
    private static string Write(YamlMappingNode root) { StringWriter writer = new(CultureInfo.InvariantCulture); new YamlStream(new YamlDocument(root)).Save(writer, false); return writer.ToString(); }
    private static InvalidDataException Failure(PlatformModuleDeclaration module, string field, string reason) => new($"{module.Source}: {field}: {reason}.");
}
