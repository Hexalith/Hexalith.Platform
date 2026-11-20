using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using JsonDocument request = JsonDocument.Parse(File.ReadAllBytes(args[0]));
JsonElement input = request.RootElement;
string gateway = input.GetProperty("gatewayAppId").GetString()!;
string output = input.GetProperty("outputDirectory").GetString()!;
using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "receipt.json")));
string root = receipt.RootElement.GetProperty("rootDigest").GetString()!;
byte[] bytes = File.ReadAllBytes(Path.Combine(output, "catalog.json"));
RouteCatalogEnvelope envelope = RouteCatalogCodec.Decode(bytes, root);
string[] appIds = input.GetProperty("owners").EnumerateArray().Select(owner => owner.GetProperty("appId").GetString()!)
    .Append(gateway).Distinct(StringComparer.Ordinal).ToArray();

// These are unactivated Development fixture identities, not observations or trust grants.
string Digest(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
RouteCatalogHostIdentity[] hosts = appIds.Select(appId => new RouteCatalogHostIdentity(appId, appId,
    Digest("fixture-inventory:" + appId), Digest("fixture-topology:" + appId), "fixture-authentication")).ToArray();
var activation = new RouteCatalogActivation("catalog-builder-fixture", bytes, root, null, hosts,
    "unactivated-development-fixture", "no-authority-registration");
bool expectRefusal = args[3] == "refuse";
try
{
    _ = RouteCatalogActivationBinding.ComputeDigest(activation);
}
catch (RouteCatalogUnavailableException) when (expectRefusal)
{
    if (envelope.Routes.Any(route => !appIds.Contains(route.AppId, StringComparer.Ordinal))
        || envelope.Idempotency.All(policy => appIds.Contains(policy.ConsumerAppId, StringComparer.Ordinal)))
        throw new InvalidDataException("The negative control must isolate a foreign policy consumer with valid routes.");
    Console.WriteLine(JsonSerializer.Serialize(new { activationRefused = true, routesRemainEnrolled = true,
        consumers = envelope.Idempotency.Select(policy => policy.ConsumerAppId).Distinct().ToArray() }));
    return;
}

if (expectRefusal)
    throw new InvalidDataException("The SDK accepted an unenrolled idempotency consumer.");
string artifactDirectory = Path.GetFullPath(args[5]);
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    string path = Path.Combine(artifactDirectory, name.Name + ".dll");
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};
var ownerRoutes = input.GetProperty("owners").EnumerateArray().SelectMany(owner =>
    CompiledRouteCatalogDeclarations.Read([Assembly.Load(new AssemblyName(owner.GetProperty("assembly").GetString()!))])).ToArray();
string[] expectedCommands = ownerRoutes.Where(route => route.Kind == "command")
    .Select(route => route.RouteId).Order(StringComparer.Ordinal).ToArray();
if (!expectedCommands.SequenceEqual(envelope.Idempotency.Select(policy => policy.RouteId).Order(StringComparer.Ordinal)))
    throw new InvalidDataException("The generated command policies differ from the owner assemblies.");
int Count(string argument, int declared) => argument == "owner" ? declared : int.Parse(argument, System.Globalization.CultureInfo.InvariantCulture);
int expectedPolicies = Count(args[1], expectedCommands.Length);
int expectedQueries = Count(args[2], ownerRoutes.Count(route => route.Kind == "query" && route.Execution is { TargetKind: "projection-actor", ActorType: "ProjectionActor" }
    && route.Operation == EventStoreOperationIdentifiers.ProjectionQuery));
int expectedSampleRoutes = Count(args[4], ownerRoutes.Count(route => route.Domain is "counter" or "greeting"));
if (args[4] == "owner" && (expectedSampleRoutes == 0 || expectedQueries == 0))
    throw new InvalidDataException("The selected sample must exercise scoped routes and projection queries.");
RouteContractDeclaration[] queries = envelope.Declarations.Where(declaration => declaration.Kind == "query"
    && declaration.Execution is { TargetKind: "projection-actor", ActorType: "ProjectionActor" }
    && declaration.Operation == EventStoreOperationIdentifiers.ProjectionQuery).ToArray();
if (envelope.Idempotency.Count != expectedPolicies || envelope.Idempotency.Any(policy => policy.ConsumerAppId != gateway)
    || queries.Length != expectedQueries || queries.Any(query => !envelope.Routes.Any(route => route.RouteId == query.RouteId
        && route.AppId == gateway && route.Method == "QueryAsync")))
    throw new InvalidDataException($"Actual command policies or projection query bindings differ from the selected gateway: "
        + $"{envelope.Idempotency.Count}/{expectedPolicies} policies, {queries.Length}/{expectedQueries} projection queries.");
int sampleRoutes = envelope.Declarations.Count(declaration => declaration.Domain is "counter" or "greeting");
if (expectedSampleRoutes != 0 && sampleRoutes != expectedSampleRoutes)
    throw new InvalidDataException($"The Sample owner route inventory differs: {sampleRoutes}/{expectedSampleRoutes}.");
Console.WriteLine(JsonSerializer.Serialize(new { activationBindingAccepted = true, authorityRegistered = false,
    policies = envelope.Idempotency.Count, projectionQueries = queries.Length,
    domainServiceQueries = envelope.Declarations.Count(declaration => declaration.Kind == "query"
        && declaration.Execution?.TargetKind == "domain-service"), sampleRoutes, consumer = gateway, rootDigest = root }));
