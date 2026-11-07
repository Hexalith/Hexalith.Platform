using System.Security.Cryptography;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.Platform.CatalogArtifacts;

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
List<string> qualifiedArtifacts = [];
bool qualified = input.TryGetProperty("qualificationCohort", out var cohort) && cohort.ValueKind != JsonValueKind.Null;
string? qualificationName = qualified && cohort.ValueKind == JsonValueKind.String ? cohort.GetString() : null;
if (qualified)
{
    var requestedOwners = input.GetProperty("owners").EnumerateArray().ToArray();
    string[] expected = qualificationName switch
    {
        PublishedCatalogArtifactCohort.Name or PublishedCatalogArtifactCohort.CurrentName or PublishedCatalogArtifactCohort.EventStorePackageTestName => ["Hexalith.Tenants.Contracts/Hexalith.Tenants.Server"],
        PublishedCatalogArtifactCohort.FullPartiesName or PublishedCatalogArtifactCohort.CurrentFullPartiesName or PublishedCatalogArtifactCohort.PublishedPartiesName or PublishedCatalogArtifactCohort.CurrentOwnerName => ["Hexalith.Memories.EventStore/Hexalith.Memories.EventStoreExtensions", "Hexalith.Parties.Contracts/Hexalith.Parties.EventStoreExtensions", "Hexalith.Tenants.Contracts/Hexalith.Tenants.Server"],
        _ => throw new InvalidDataException("The requested package catalog cohort is not qualified."),
    };
    if (!requestedOwners.Select(owner => owner.GetProperty("assembly").GetString() + "/" + owner.GetProperty("adapterAssembly").GetString())
        .Order(StringComparer.Ordinal).SequenceEqual(expected, StringComparer.Ordinal))
        throw new InvalidDataException("The requested package catalog cohort or compiled owner set is not qualified.");
    qualifiedArtifacts.AddRange(qualificationName == PublishedCatalogArtifactCohort.CurrentOwnerName ? PublishedCatalogArtifactCohort.VerifyCurrentReferences() : qualificationName is PublishedCatalogArtifactCohort.FullPartiesName or PublishedCatalogArtifactCohort.CurrentFullPartiesName or PublishedCatalogArtifactCohort.PublishedPartiesName
        ? PublishedCatalogArtifactCohort.VerifyFullParties(qualificationName) : PublishedCatalogArtifactCohort.VerifySdk(qualificationName!));
}
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
    var adapterAssembly = Assembly.Load(new AssemblyName(owner.GetProperty("adapterAssembly").GetString()!));
    adapters.Add(adapterAssembly.Location);
    if (qualified)
    {
        foreach (Assembly artifact in new[] { assembly, adapterAssembly })
        {
            if (qualificationName == PublishedCatalogArtifactCohort.CurrentOwnerName && artifact.GetName().Name is "Hexalith.Parties.Contracts" or "Hexalith.Parties.EventStoreExtensions")
            {
                var measured = input.GetProperty("currentOwnerArtifacts");
                if (measured.EnumerateObject().Count() != 2
                    || !measured.TryGetProperty(artifact.GetName().Name!, out var expected)
                    || expected.GetString() != Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(artifact.Location))))
                    throw new InvalidDataException("The private current owner reader loaded different measured artifacts.");
                qualifiedArtifacts.Add(artifact.Location);
            }
            else qualifiedArtifacts.Add(PublishedCatalogArtifactCohort.Verify(artifact, qualificationName!));
        }
    }
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
    var declaredCommands = new List<CommandIntentDeclaration>();
    foreach (var source in sources)
    {
        if (source.SourceType.GetProperty("Commands", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is not IEnumerable<CommandIntentDeclaration> commands)
            throw new InvalidDataException($"Owner {source.SourceType.FullName} has no declared command policies.");
        declaredCommands.AddRange(commands);
#if HEXALITH_SCOPED_CONSUMER_BINDING
        policies.AddRange(commands.Select(command => DevelopmentIdempotencyBindings.Bind(command, generation, gatewayAppId)));
#else
        policies.AddRange(commands.Select(command => DevelopmentIdempotencyBindings.Bind(command, generation)));
#endif
    }
    var installed = adapterAssembly.GetCustomAttributes<RouteCatalogAdapterSourceAttribute>().SelectMany(source =>
    {
        if (source.SourceType.Assembly != adapterAssembly
            || source.SourceType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null)
                is not IEnumerable<IDeclaredIdempotencyIntentAdapter> factoryAdapters)
            throw new InvalidDataException("The selected owner adapter factory is absent or foreign.");
        return factoryAdapters;
    }).ToArray();
    if (installed.Length != declaredCommands.Count
        || installed.Select(adapter => adapter.Declaration.Route.RouteId).Distinct(StringComparer.Ordinal).Count() != installed.Length)
        throw new InvalidDataException("The selected owner adapter inventory differs from its declared commands.");
    foreach (var adapter in installed)
    {
        var declaration = adapter.Declaration;
        var expected = declaredCommands.SingleOrDefault(command => command.Route.RouteId == declaration.Route.RouteId)
            ?? throw new InvalidDataException("The selected owner adapter has no declared command.");
#if HEXALITH_SCOPED_CONSUMER_BINDING
        string actualPolicy = DevelopmentIdempotencyBindings.Bind(declaration, generation, gatewayAppId).ContentDigest;
        string expectedPolicy = DevelopmentIdempotencyBindings.Bind(expected, generation, gatewayAppId).ContentDigest;
#else
        string actualPolicy = DevelopmentIdempotencyBindings.Bind(declaration, generation).ContentDigest;
        string expectedPolicy = DevelopmentIdempotencyBindings.Bind(expected, generation).ContentDigest;
#endif
        if (RouteCatalogCodec.ComputeDeclarationDigest(declaration.Route) != RouteCatalogCodec.ComputeDeclarationDigest(expected.Route)
            || actualPolicy != expectedPolicy || adapter.CommandType != declaration.Route.MessageType
            || adapter.AdapterId != declaration.AdapterId || adapter.OperationId != declaration.OperationId
            || adapter.DescriptorVersion != declaration.DescriptorSchema.Version || adapter.RetentionTier != declaration.RetentionTier)
            throw new InvalidDataException("The selected owner adapter differs from its declared route or semantic policy.");
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
    // This records actual reader-output dependencies, not an installed execution image measurement.
    loadedArtifacts = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && assembly.Location.Length > 0
        && Path.GetDirectoryName(assembly.Location) == Path.GetDirectoryName(typeof(RouteCatalogCodec).Assembly.Location))
        .Select(assembly => assembly.Location).Concat(owners).Concat(adapters).Concat(qualifiedArtifacts).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(path => new
    {
        path, sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
        assemblyIdentity = AssemblyName.GetAssemblyName(path).FullName,
        informationalVersion = AppDomain.CurrentDomain.GetAssemblies().Single(assembly => !assembly.IsDynamic && assembly.Location == path)
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    }).ToArray(),
}));
