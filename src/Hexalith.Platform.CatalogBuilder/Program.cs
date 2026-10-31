using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using var document = JsonDocument.Parse(File.ReadAllBytes(args.Single()));
var input = document.RootElement;
long generation = input.GetProperty("generation").GetInt64();
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
            if (!input.TryGetProperty("gatewayAppId", out var gateway) || gateway.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(gateway.GetString()))
                throw new InvalidDataException("The declared gateway projection query requires its actual gatewayAppId binding.");
            appId = gateway.GetString()!;
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
        policies.AddRange(commands.Select(command => DevelopmentIdempotencyBindings.Bind(command, generation)));
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
}));
