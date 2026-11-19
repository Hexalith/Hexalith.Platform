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
    throw new InvalidDataException("This SDK can bind only an explicitly requested gatewayAppId of eventstore; scoped consumers require a qualified SDK inside the scoped-consumer binding range.");
#endif
var requestedOwners = input.GetProperty("owners").EnumerateArray().ToArray();
if (requestedOwners.Length == 0
    || requestedOwners.Select(owner => owner.GetProperty("assembly").GetString()).Distinct(StringComparer.Ordinal).Count() != requestedOwners.Length
    || requestedOwners.Select(owner => owner.GetProperty("adapterAssembly").GetString()).Distinct(StringComparer.Ordinal).Count() != requestedOwners.Length)
    throw new InvalidDataException("The routing catalog requires a nonempty owner set without duplicate owner or adapter assemblies.");
// Package mode names the generated owner package lock; every owner package the reader loads must match it (AR-12).
bool verified = input.TryGetProperty("ownerPackageLock", out var lockPath) && lockPath.ValueKind != JsonValueKind.Null;
OwnerPackageLock? ownerLock = verified ? OwnerPackageLock.Load(lockPath.GetString() ?? throw new InvalidDataException("The owner package lock path is invalid.")) : null;
// Run-built Parties bodies keep their run-bound authority; they are measured by the current-owner proof, never locked.
var runBound = input.TryGetProperty("currentOwnerArtifacts", out var measured) && measured.ValueKind == JsonValueKind.Object
    ? measured.EnumerateObject().ToDictionary(pair => pair.Name, pair => pair.Value.GetString(), StringComparer.Ordinal)
    : new Dictionary<string, string?>(StringComparer.Ordinal);
if (runBound.Count > 0 && (!verified || runBound.Keys.Any(name => !name.StartsWith("Hexalith.Parties.", StringComparison.Ordinal))))
    throw new InvalidDataException("Only package-mode Parties bodies can keep run-bound reader authority.");
Dictionary<string, OwnerPackageIdentity> ownerPackages = new(StringComparer.Ordinal);
if (ownerLock is not null)
{
    string[] packageFolders = input.GetProperty("packageFolders").EnumerateArray().Select(folder => folder.GetString()
        ?? throw new InvalidDataException("The reader package folders are invalid.")).ToArray();
    if (packageFolders.Length == 0) throw new InvalidDataException("The reader package folders are absent.");
    using var graph = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, typeof(OwnerPackageLock).Assembly.GetName().Name + ".deps.json")));
    // Check every owner package in the reader closure before any owner attribute or factory is read.
    foreach (var identity in ownerLock.VerifyDirectory(graph.RootElement, packageFolders, AppContext.BaseDirectory, runBound.Keys.ToHashSet(StringComparer.Ordinal)))
        ownerPackages.Add(identity.Assembly, identity);
    if (ownerPackages.Count == 0) throw new InvalidDataException("The package reader loaded no lock-verified owner package.");
}
void RequireAdmitted(Assembly artifact)
{
    string name = artifact.GetName().Name!;
    if (runBound.TryGetValue(name, out string? expected))
    {
        if (expected != Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(artifact.Location))))
            throw new InvalidDataException($"The private reader loaded different measured bytes for '{name}'.");
        return;
    }
    if (!ownerPackages.TryGetValue(name, out var identity) || identity.LoadedPath != artifact.Location
        || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(artifact.Location))) != identity.ExpectedSha256)
        throw new InvalidDataException($"The loaded assembly '{name}' differs from its lock-verified package member.");
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
    if (verified)
    {
        foreach (Assembly artifact in new[] { assembly, adapterAssembly }) RequireAdmitted(artifact);
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
                throw new InvalidDataException($"Owner {assembly.GetName().Name}, route {route.RouteId} has no supported installed transport binding (targetKind={route.Execution.TargetKind}, actorType={route.Execution.ActorType}).");
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
        || installed.Select(adapter => adapter.Declaration.Route.RouteId).Distinct(StringComparer.Ordinal).Count() != installed.Length
        || !installed.Select(adapter => adapter.Declaration.Route.RouteId).Order(StringComparer.Ordinal)
            .SequenceEqual(declaredCommands.Select(command => command.Route.RouteId).Order(StringComparer.Ordinal), StringComparer.Ordinal))
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
var loadedOwnerArtifacts = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && assembly.Location.Length > 0
    && Path.GetDirectoryName(assembly.Location) == Path.GetDirectoryName(typeof(RouteCatalogCodec).Assembly.Location)).ToArray();
if (verified)
{
    // A substituted DLL refuses even after a successful load, naming the assembly.
    foreach (Assembly artifact in loadedOwnerArtifacts.Where(artifact => OwnerPackageLock.IsOwnerPackage(artifact.GetName().Name))) RequireAdmitted(artifact);
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
    loadedArtifacts = loadedOwnerArtifacts
        .Select(assembly => assembly.Location).Concat(owners).Concat(adapters).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(path => new
    {
        path, sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
        assemblyIdentity = AssemblyName.GetAssemblyName(path).FullName,
        informationalVersion = AppDomain.CurrentDomain.GetAssemblies().Single(assembly => !assembly.IsDynamic && assembly.Location == path)
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    }).ToArray(),
    // AR-12: the lock entry, the expected identity from the lock-verified archive member and the loaded identity.
    ownerPackageLock = ownerLock is null ? null : new { path = ownerLock.Path, sha256 = ownerLock.Sha256 },
    ownerPackages = ownerPackages.Values.OrderBy(identity => identity.Assembly, StringComparer.Ordinal).Select(identity => new
    {
        assembly = identity.Assembly, packageId = identity.PackageId, version = identity.Version, contentHash = identity.ContentHash,
        archive = identity.Archive, archiveMember = identity.ArchiveMember, expectedSha256 = identity.ExpectedSha256,
        loadedPath = identity.LoadedPath, loadedSha256 = identity.LoadedSha256,
    }).ToArray(),
}));
