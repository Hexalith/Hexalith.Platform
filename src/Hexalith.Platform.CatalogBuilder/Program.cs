using System.Security.Cryptography;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using var document = JsonDocument.Parse(File.ReadAllBytes(args.Single()));
var input = document.RootElement;
long generation = input.GetProperty("generation").GetInt64();
if (!input.TryGetProperty("gatewayAppId", out var gateway) || gateway.ValueKind != JsonValueKind.String
    || string.IsNullOrWhiteSpace(gateway.GetString()))
    throw new InvalidDataException("The routing catalog requires its actual gatewayAppId binding.");
string gatewayAppId = gateway.GetString()!;
#if !HEXALITH_SCOPED_CONSUMER_BINDING
if (gatewayAppId != "eventstore")
    throw new InvalidDataException("This SDK can bind only an explicitly requested gatewayAppId of eventstore; scoped consumers require a qualified SDK 3.130.0, 3.130.1, 3.130.2, exact 3.132.1 or exact 3.133.0.");
#endif
List<RouteContractDeclaration> declarations = [];
List<RouteOperationContract> operations = [];
List<RouteCompositionBinding> bindings = [];
List<IdempotencyCatalogEntry> policies = [];
List<string> owners = [];
List<string> adapters = [];
foreach (var owner in input.GetProperty("owners").EnumerateArray())
{
    var assembly = Assembly.Load(new AssemblyName(owner.GetProperty("assembly").GetString()!));
    owners.Add(assembly.Location);
    adapters.Add(Assembly.Load(new AssemblyName(owner.GetProperty("adapterAssembly").GetString()!)).Location);
    var routes = CompiledRouteCatalogDeclarations.Read([assembly]);
    declarations.AddRange(routes);
    operations.AddRange(CompiledRouteCatalogDeclarations.ReadOperations([assembly]));
    var sources = assembly.GetCustomAttributes<RouteCatalogDeclarationSourceAttribute>().ToArray();
    var methods = sources.SelectMany(source => source.SourceType.GetProperty("ReceiverMethods", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
        as IReadOnlyDictionary<string, string> ?? throw new InvalidDataException($"Owner {source.SourceType.FullName} has no actual receiver method inventory."))
        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    foreach (var route in routes)
    {
        string appId = owner.GetProperty("appId").GetString()!;
        string method;
        if (route.Kind == "query" && route.Execution is { TargetKind: "projection-actor", ActorType: "ProjectionActor" }
            && route.CredentialKind == "workload"
            && route.Operation == Hexalith.EventStore.Contracts.Authorization.EventStoreOperationIdentifiers.ProjectionQuery)
        {
            appId = gatewayAppId;
            method = "QueryAsync";
        }
        else
        {
            if (route.Execution?.TargetKind is not null and not "domain-service")
                throw new InvalidDataException("The owner declaration has no supported installed transport binding.");
            method = methods[route.RouteId];
        }
        bindings.Add(new(route.RouteId, appId, method, route.ContractVersion, false, "*"));
    }
    foreach (var source in sources)
    {
        if (source.SourceType.GetProperty("Commands", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is not IEnumerable<CommandIntentDeclaration> commands)
            throw new InvalidDataException($"Owner {source.SourceType.FullName} has no declared command policies.");
#if HEXALITH_SCOPED_CONSUMER_BINDING
        policies.AddRange(commands.Select(command => DevelopmentIdempotencyBindings.Bind(command, generation, gatewayAppId)));
#else
        policies.AddRange(commands.Select(command => DevelopmentIdempotencyBindings.Bind(command, generation)));
#endif
    }
}
byte[]? predecessor = input.TryGetProperty("committedPredecessor", out var previous) && previous.ValueKind == JsonValueKind.String
    ? File.ReadAllBytes(previous.GetString()!) : null;
string? predecessorRoot = input.TryGetProperty("committedPredecessorRootDigest", out var previousRoot) && previousRoot.ValueKind == JsonValueKind.String
    ? previousRoot.GetString() : null;
var catalog = VersionedRouteCatalogBuilder.Build(new(generation, "Development", declarations, operations, bindings, policies, predecessor, predecessorRoot));
string directory = input.GetProperty("outputDirectory").GetString()!;
File.WriteAllBytes(Path.Combine(directory, "catalog.json"), RouteCatalogCodec.Encode(catalog));
File.WriteAllBytes(Path.Combine(directory, "receipt.json"), JsonSerializer.SerializeToUtf8Bytes(new
{
    instanceId = input.GetProperty("instanceId").GetString(), rootDigest = RouteCatalogCodec.ComputeRootDigest(catalog),
    ownerAssemblies = owners, adapterAssemblies = adapters,
    sourceMappingHash = input.TryGetProperty("sourceMappingHash", out var sourceMappingHash) ? sourceMappingHash.GetString() : null,
    mode = input.TryGetProperty("mode", out var mode) ? mode.GetString() : null,
    loadedArtifacts = owners.Concat(adapters).Append(typeof(RouteCatalogCodec).Assembly.Location).Distinct(StringComparer.Ordinal).Select(path => new
    {
        path, sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
        assemblyIdentity = AssemblyName.GetAssemblyName(path).FullName,
        informationalVersion = AppDomain.CurrentDomain.GetAssemblies().Single(assembly => !assembly.IsDynamic && assembly.Location == path)
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    }).ToArray(),
}));
