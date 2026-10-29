using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Owned independent observer. It uses only fresh native state reads and the surviving ERC head; executor responses and expected scenario values are never observation sources.</summary>
public sealed class ProtectionQualificationPersistedObserver(DaprClient readOnlyNative, ErasureRootCustody erasureRoot,
    InteractionPayloadProtectionService loadedEngine, IReadOnlySet<string> enrolledReadComponents)
{
    /// <summary>Reads actual persisted facts and projects the owner-verifier wire fields without performing a native mutation.</summary>
    public async Task<JsonObject> ObserveAsync(ProtectionQualificationObservationRecipe recipe, IReadOnlyList<ProtectionTarget> targets, CancellationToken token)
    {
        var current = await erasureRoot.ReadAsync(recipe.TenantId, token).ConfigureAwait(false);
        if (current is not { } head || head.Head.Pending is not null || targets.Count is < 1 or > 1000 || targets.Any(target => target.TenantId != recipe.TenantId))
        { throw new InvalidOperationException("DependencyNotAvailable: current complete ERC observation."); }
        var sources = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var reference in recipe.Sources)
        {
            var cell = reference.Value;
            if (!enrolledReadComponents.Contains(cell.Component) || reference.Key is "ERC" or "$item" || string.IsNullOrWhiteSpace(cell.Key))
            { throw new InvalidOperationException("Independent native read enrollment required."); }
            var value = await readOnlyNative.GetStateAsync<JsonElement>(cell.Component, cell.Key, ConsistencyMode.Strong, cell.Metadata, token).ConfigureAwait(false);
            if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) { throw new InvalidOperationException("Native observation unavailable."); }
            sources.Add(reference.Key, JsonNode.Parse(value.GetRawText())!);
            if (cell.AnchorScope is not null && (!head.Head.Anchors.TryGetValue(cell.AnchorScope, out var anchor)
                || ErasureRootCustody.Hash(value) != anchor.Digest))
            { throw new InvalidOperationException("Native observation is not independently current."); }
        }
        sources.Add("ERC", JsonSerializer.SerializeToNode(head.Head)!);
        var observed = new JsonObject();
        foreach (var field in recipe.Fields) { observed.Add(field.Key, Project(field.Value, sources, null)); }
        var snapshotOriginals = new JsonArray(); var seenSnapshots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in recipe.Sources)
        {
            var value = sources[source.Key];
            var carrierNode = value["State"] is JsonObject state ? state : value as JsonObject;
            if (carrierNode?["Format"]?.GetValue<string>() != "json+pdenc-v2-entries") { continue; }
            var carrier = carrierNode.Deserialize<ProtectedSnapshotEntriesV2>() ?? throw new InvalidOperationException("Native mixed snapshot unavailable.");
            string carrierHash = ErasureRootCustody.Hash(carrier);
            if (!seenSnapshots.Add(carrierHash)) { continue; }
            var location = source.Value;
            if (location.CompletionKey is null || !head.Head.Anchors.TryGetValue(ErasureRootOccurrenceAuthority.Scope(recipe.TenantId), out var registryAnchor))
            { throw new InvalidOperationException("Independent native composite completion and registry source required."); }
            var currentRegistry = registryAnchor.Completed.Values.SingleOrDefault(original => original.TargetRevision == registryAnchor.Revision && original.TargetDigest == registryAnchor.Digest);
            var registry = currentRegistry is null ? null : JsonSerializer.Deserialize<InteractionOccurrenceRegistrySnapshot>(currentRegistry.TargetBytes);
            if (registry is null) { throw new InvalidOperationException("Native current snapshot registry unavailable."); }
            var completion = await readOnlyNative.GetStateAsync<PayloadProtectionCompletionContext>(location.Component, location.CompletionKey, ConsistencyMode.Strong, location.Metadata, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Native original composite completion unavailable.");
            var entries = new JsonArray();
            foreach (var entry in carrier.Entries)
            {
                var original = head.Head.SourceOriginals.Values.SingleOrDefault(original => original.Binding.KeyReference == entry.KeyReference
                    && original.Binding.Identity.Target == entry.Target && original.SnapshotEntry?.Path == entry.Path
                    && original.SnapshotEntry.ContainerDigest == Convert.ToHexString(SHA256.HashData(carrier.SkeletonBytes)));
                var retained = original is null ? null : registry.Records.SingleOrDefault(record => record.Request.Identity == original.Binding.Identity && record.KeyReference == entry.KeyReference);
                if (original?.Writer is not { } writer || retained is null || !enrolledReadComponents.Contains(writer.Component))
                { throw new InvalidOperationException("Independent complete snapshot source original required."); }
                var proof = await readOnlyNative.GetStateAsync<ErasureRootWriterProof>(writer.Component, writer.Key, ConsistencyMode.Strong, writer.Metadata, token).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Native snapshot writer original unavailable.");
                entries.Add(new JsonObject { ["Path"] = entry.Path, ["SourceEnrollment"] = JsonSerializer.SerializeToNode(original),
                    ["RegistryOriginal"] = JsonSerializer.SerializeToNode(retained), ["WriterProof"] = JsonSerializer.SerializeToNode(proof) });
            }
            snapshotOriginals.Add(new JsonObject { ["CarrierSha256"] = carrierHash, ["Completion"] = JsonSerializer.SerializeToNode(completion), ["Entries"] = entries });
        }
        if (snapshotOriginals.Count != 0) { observed["snapshotOriginals"] = snapshotOriginals; }
        var identity = loadedEngine.GetIdentity();
        observed["engineSourceRevision"] = identity.SourceRevision;
        observed["engineBinarySha256"] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(InteractionPayloadProtectionService).Assembly.Location)));
        observed["agentsRuntimeEnabled"] = AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name?.StartsWith("Hexalith.Agents", StringComparison.Ordinal) == true);
        observed["roots"] = new JsonArray(targets.Select(target =>
        {
            string id = ErasureRootCustody.TargetId(target);
            string state = head.Head.Erased.ContainsKey(id) ? "Destroyed" : head.Head.Targets.TryGetValue(id, out var root) ? root.Pins.Length == 0 ? "Active" : "Pinned"
                : throw new InvalidOperationException("Unknown original root.");
            return (JsonNode)new JsonObject { ["target"] = JsonSerializer.SerializeToNode(target), ["state"] = state };
        }).ToArray());
        // A later native/owner mutation invalidates this observation, including after an EventStore restore or concurrent block delivery.
        var final = await erasureRoot.ReadAsync(recipe.TenantId, token).ConfigureAwait(false);
        if (final is not { } confirmed || confirmed.Version != head.Version || ErasureRootCustody.Hash(confirmed.Head) != ErasureRootCustody.Hash(head.Head))
        { throw new InvalidOperationException("Native observation changed during independent read."); }
        return observed;
    }

    private static JsonNode? Project(ProtectionQualificationObservationField field, IReadOnlyDictionary<string, JsonNode> sources, JsonNode? item)
    {
        JsonNode? input = field.Source == "$item" ? item : sources.TryGetValue(field.Source, out var source) ? source : throw new InvalidOperationException("Unenrolled native observation source.");
        input = Pointer(input, field.Pointer);
        if (field.Items is { } projections)
        {
            if (input is not JsonArray array || field.Transform != "Identity") { throw new InvalidOperationException("Actual native array required."); }
            return new JsonArray(array.Select(value => { var result = new JsonObject(); foreach (var projection in projections) { result[projection.Key] = Project(projection.Value, sources, value); } return (JsonNode)result; }).ToArray());
        }
        return field.Transform switch
        {
            "Identity" => input?.DeepClone(),
            "Count" when input is JsonArray values => JsonValue.Create(values.Count),
            "OwnerStatus" when input is JsonValue value && value.TryGetValue<int>(out int status) && Enum.IsDefined((DeletionConsumptionStatus)status)
                => JsonValue.Create(((DeletionConsumptionStatus)status).ToString()),
            "LifecycleStatus" when input is JsonValue value && value.TryGetValue<int>(out int status) && Enum.IsDefined((CustodyKeyLifecycleStatus)status)
                => JsonValue.Create(status == (int)CustodyKeyLifecycleStatus.BlockedByPin ? "HoldPinned" : ((CustodyKeyLifecycleStatus)status).ToString()),
            _ => throw new InvalidOperationException("Only actual native fact projections are permitted."),
        };
    }
    internal static JsonNode? Pointer(JsonNode? node, string pointer)
    {
        if (pointer.Length == 0) { return node; }
        if (!pointer.StartsWith("/", StringComparison.Ordinal)) { throw new InvalidOperationException("Native JSON pointer required."); }
        foreach (string encoded in pointer[1..].Split('/'))
        {
            string part = encoded.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            node = node switch { JsonObject obj => obj[part], JsonArray array when int.TryParse(part, out int index) && index >= 0 && index < array.Count => array[index], _ => null };
            if (node is null) { throw new InvalidOperationException("Native field unavailable."); }
        }
        return node;
    }
}
