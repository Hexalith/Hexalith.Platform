using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Owned independent observer. It uses only fresh native state reads and the surviving ERC head; executor responses and expected scenario values are never observation sources.</summary>
public sealed class ProtectionQualificationPersistedObserver(DaprClient readOnlyNative, ErasureRootCustody erasureRoot,
    InteractionPayloadProtectionService loadedEngine, IReadOnlySet<string> enrolledReadComponents)
{
    /// <summary>Reads actual persisted facts and projects the owner-verifier wire fields without performing a native mutation.</summary>
    public async Task<JsonObject> ObserveAsync(ProtectionQualificationObservationRecipe recipe, IReadOnlyList<ProtectionTarget> targets, CancellationToken token, ProtectionQualificationProducerIdentity? expectedProducer = null, IReadOnlySet<string>? expectedOperationIds = null)
    {
        _ = loadedEngine;
        var before = await erasureRoot.ReadObservationVersionAsync(recipe.TenantId, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Current ERC observation unavailable.");
        var scopes = recipe.Sources.Values.Where(cell => cell.AnchorScope is not null).Select(cell => cell.AnchorScope!).ToHashSet(StringComparer.Ordinal);
        scopes.Add(DeletionConsumptionActor.GetOwnerScope(recipe.TenantId)); scopes.Add(ErasureRootOccurrenceAuthority.Scope(recipe.TenantId));
        var current = await erasureRoot.ReadAsync(recipe.TenantId, token, false, scopes).ConfigureAwait(false);
        if (current is not { } head || head.Head.Pending is { Survivors: not null } || targets.Count is < 1 or > 1000 || targets.Any(target => target.TenantId != recipe.TenantId))
        { throw new InvalidOperationException("DependencyNotAvailable: current complete ERC observation."); }
        if (before.Version != head.Version) { throw new InvalidOperationException("Native observation changed before original selection."); }
        await ErasureRootObservationSelection.SelectAsync(erasureRoot, head.Head, expectedOperationIds, recipe.CaseId, targets, token).ConfigureAwait(false);
        var sources = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var reference in recipe.Sources)
        {
            var cell = reference.Value;
            if (!enrolledReadComponents.Contains(cell.Component) || reference.Key is "ERC" or "$item" || string.IsNullOrWhiteSpace(cell.Key))
            { throw new InvalidOperationException("Independent native read enrollment required."); }
            var value = await readOnlyNative.GetStateAsync<JsonElement>(cell.Component, cell.Key, ConsistencyMode.Strong, cell.Metadata, token).ConfigureAwait(false);
            if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) { throw new InvalidOperationException("Native observation unavailable."); }
            sources.Add(reference.Key, JsonNode.Parse(value.GetRawText())!);
            var backup = await readOnlyNative.GetStateAsync<JsonElement>(cell.Component, cell.Key + ":backup", ConsistencyMode.Strong, cell.Metadata, token).ConfigureAwait(false);
            if (backup.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null)) { sources.Add(reference.Key + ":backup", JsonNode.Parse(backup.GetRawText())!); }
            if (cell.CopyKind is not null)
            {
                var replay = await readOnlyNative.GetStateAsync<JsonElement>(cell.Component, cell.Key + ":replay", ConsistencyMode.Strong, cell.Metadata, token).ConfigureAwait(false);
                if (replay.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null)) { sources.Add(reference.Key + ":replay", JsonNode.Parse(replay.GetRawText())!); }
            }
            if (cell.AnchorScope is not null && (!head.Head.Anchors.TryGetValue(cell.AnchorScope, out var anchor)
                || ErasureRootCustody.Hash(value) != anchor.Digest))
            { throw new InvalidOperationException("Native observation is not independently current."); }
        }
        sources.Add("ERC", JsonSerializer.SerializeToNode(head.Head)!);
        var observed = recipe.CaseId is { } sourceCase ? ProtectionQualificationNativeProjection.Project(sourceCase, head.Head, sources, recipe.Sources, targets) : new JsonObject();
        if (recipe.CaseId is null) { foreach (var field in recipe.Fields) { observed.Add(field.Key, Project(field.Value, sources, null)); } }
        var copyOriginals = new JsonArray();
        var producers = new Dictionary<string, ProtectionQualificationProducerIdentity>(StringComparer.Ordinal);
        InteractionOccurrenceRegistrySnapshot Registry()
        {
            if (!head.Head.Anchors.TryGetValue(ErasureRootOccurrenceAuthority.Scope(recipe.TenantId), out var anchor))
            { throw new InvalidOperationException("Independent current occurrence registry required."); }
            var currentRegistry = anchor.Completed.Values.SingleOrDefault(value => value.TargetRevision == anchor.Revision && value.TargetDigest == anchor.Digest);
            return currentRegistry is null ? throw new InvalidOperationException("Current registry original unavailable.")
                : JsonSerializer.Deserialize<InteractionOccurrenceRegistrySnapshot>(currentRegistry.TargetBytes) ?? throw new InvalidOperationException("Invalid registry original.");
        }
        ProtectionQualificationProducerOriginal Producer(byte[] bytes, IReadOnlyList<InteractionProtectionBinding> bindings)
        {
            string digest = Convert.ToHexString(SHA256.HashData(bytes));
            var matches = head.Head.QualificationProducerOriginals.Values.Where(value => value.SealedPayloadSha256 == digest
                && ErasureRootCustody.Hash(value.Bindings) == ErasureRootCustody.Hash(bindings)).ToArray();
            if (matches.Length != 1 || matches[0].Producer.StoreId != head.Head.StoreId || matches[0].Producer.EnrollmentId != head.Head.EnrollmentId
                || matches[0].CompletionDigest != ErasureRootCustody.Hash(matches[0].Completion))
            { throw new InvalidOperationException("Authenticated exact executor/source producer original required."); }
            if (matches[0].Call is not null || expectedOperationIds is not null)
            {
                var call = matches[0].Call;
                if (call?.OperationId is not { } operationId || call.RequestSha256 is not { } requestDigest
                    || expectedOperationIds is not null && !expectedOperationIds.Contains(operationId)
                    || !head.Head.QualificationOperations.TryGetValue(ProtectionQualificationOperationJournal.Id("executor", operationId), out var operation)
                    || operation.RequestSha256 != requestDigest || operation.Producer is null
                    || ErasureRootCustody.Hash(operation.Producer) != ErasureRootCustody.Hash(matches[0].Producer)
                    || !operation.Calls.TryGetValue(call.Id, out var admitted) || ErasureRootCustody.Hash(admitted.Call) != ErasureRootCustody.Hash(call)
                    || expectedOperationIds is not null && (operation.Result is null || admitted.Result is null))
                { throw new InvalidOperationException("Exact executed source role/request/native-call original required."); }
            }
            producers.TryAdd(ErasureRootCustody.Hash(matches[0].Producer), matches[0].Producer);
            return matches[0];
        }
        foreach (var source in recipe.Sources.Where(entry => entry.Value.CopyKind is not null))
        {
            var value = sources[source.Key];
            var node = value is JsonObject document && document["State"] is JsonObject state ? state : value as JsonObject;
            if (node?["Format"]?.GetValue<string>() == "json+pdenc-v2-entries")
            {
                var carrier = node.Deserialize<ProtectedSnapshotEntriesV2>()!;
                var bindings = carrier.Entries.Select(entry => head.Head.SourceOriginals.Values.Single(original => original.Binding.KeyReference == entry.KeyReference
                    && original.Binding.Identity.Target == entry.Target && original.SnapshotEntry?.Path == entry.Path).Binding).ToArray();
                _ = Producer(JsonSerializer.SerializeToUtf8Bytes(carrier), bindings);
                continue;
            }
            InteractionProtectionBinding binding; byte[] payload;
            if (source.Value.CopyKind is "source-outbox" or "target-event")
            {
                var persisted = value.Deserialize<ProtectionQualificationPersistedEvent>()!;
                binding = persisted.Binding; payload = persisted.Protected.PayloadBytes;
            }
            else
            {
                var snapshot = value.Deserialize<SnapshotProtectionResult>()!;
                binding = head.Head.SourceOriginals.Values.Single(original => original.Binding.KeyReference == snapshot.Metadata!.KeyAlias).Binding;
                payload = JsonSerializer.SerializeToUtf8Bytes(snapshot.State);
            }
            var enrollment = head.Head.SourceOriginals.Values.SingleOrDefault(original => original.Binding == binding);
            var registry = Registry().Records.SingleOrDefault(record => record.Request.Identity == binding.Identity && record.KeyReference == binding.KeyReference);
            if (enrollment?.Writer is not { } writer || !enrolledReadComponents.Contains(writer.Component) || registry?.Sealed is not { } sealedOriginal
                || !sealedOriginal.PayloadBytes.AsSpan().SequenceEqual(payload) || sealedOriginal.SerializationFormat != "json+pdenc-v2"
                || registry.WriterState != InteractionOccurrenceWriterState.Active || registry.WriterProofId != binding.OperationId)
            { throw new InvalidOperationException("Exact persisted ciphertext and complete source/registry writer original required."); }
            var proof = await readOnlyNative.GetStateAsync<ErasureRootWriterProof>(writer.Component, writer.Key, ConsistencyMode.Strong, writer.Metadata, token).ConfigureAwait(false);
            if (proof is not { Persisted: true, WriterRetired: true } || proof.Binding != binding || string.IsNullOrWhiteSpace(proof.ReceiptId)
                || proof.SealedPayloadSha256 != Convert.ToHexString(SHA256.HashData(payload)))
            { throw new InvalidOperationException("Exact native retired writer proof required."); }
            var producer = Producer(payload, new[] { binding });
            var completion = producer.Completion;
            if (completion.DekVersion != 1 || completion.SnapshotEntries is not null || completion.SnapshotContainerDigest is not null || completion.SnapshotEntryManifest is not null
                || completion.Identity != binding.Identity.Owner || completion.OperationId != binding.OperationId || completion.KeyReference != binding.KeyReference
                || completion.Occurrence != ErasureRootOccurrenceBindings.Occurrence(binding.Identity) || completion.LifecycleEpoch != binding.LifecycleEpoch)
            { throw new InvalidOperationException("Exact original producer completion required."); }
            copyOriginals.Add(new JsonObject { ["CopyId"] = source.Key + ":" + ErasureRootCustody.TargetId(binding.Identity.Target), ["SourceEnrollment"] = JsonSerializer.SerializeToNode(enrollment),
                ["RegistryOriginal"] = JsonSerializer.SerializeToNode(registry), ["WriterProof"] = JsonSerializer.SerializeToNode(proof), ["ProducerOriginal"] = JsonSerializer.SerializeToNode(producer) });
        }
        if (copyOriginals.Count != 0) { observed["copyOriginals"] = copyOriginals; }
        var snapshotOriginals = new JsonArray(); var seenSnapshots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in recipe.Sources)
        {
            var value = sources[source.Key];
            var carrierNode = value is JsonObject document && document["State"] is JsonObject state ? state : value as JsonObject;
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
            var producer = Producer(JsonSerializer.SerializeToUtf8Bytes(carrier), carrier.Entries.Select(entry => head.Head.SourceOriginals.Values.Single(original => original.Binding.KeyReference == entry.KeyReference
                && original.Binding.Identity.Target == entry.Target && original.SnapshotEntry?.Path == entry.Path).Binding).ToArray());
            snapshotOriginals.Add(new JsonObject { ["CarrierSha256"] = carrierHash, ["Completion"] = JsonSerializer.SerializeToNode(completion), ["Entries"] = entries,
                ["ProducerOriginal"] = JsonSerializer.SerializeToNode(producer) });
        }
        if (snapshotOriginals.Count != 0) { observed["snapshotOriginals"] = snapshotOriginals; }
        if (producers.Count == 0 && expectedProducer is not null)
        { producers.Add(ErasureRootCustody.Hash(expectedProducer), expectedProducer); }
        if (producers.Count != 1) { throw new InvalidOperationException("One exact selected executor producer identity required."); }
        var identity = producers.Values.Single();
        if (expectedProducer is not null && ErasureRootCustody.Hash(identity) != ErasureRootCustody.Hash(expectedProducer))
        { throw new InvalidOperationException("Source producer must match the exact current authenticated operation originals."); }
        observed["engineSourceRevision"] = identity.EngineSourceRevision;
        observed["engineBinarySha256"] = identity.EngineBinarySha256;
        observed["producerIdentity"] = JsonSerializer.SerializeToNode(identity);
        observed["agentsRuntimeEnabled"] = AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name?.StartsWith("Hexalith.Agents", StringComparison.Ordinal) == true);
        observed["roots"] = new JsonArray(targets.Select(target =>
        {
            string id = ErasureRootCustody.TargetId(target);
            string state = head.Head.Erased.ContainsKey(id) ? "Destroyed" : head.Head.Targets.TryGetValue(id, out var root) ? root.Pins.Length == 0 ? "Active" : "Pinned"
                : throw new InvalidOperationException("Unknown original root.");
            return (JsonNode)new JsonObject { ["target"] = JsonSerializer.SerializeToNode(target), ["state"] = state };
        }).ToArray());
        // A later native/owner mutation invalidates this observation, including after an EventStore restore or concurrent block delivery.
        var final = await erasureRoot.ReadObservationVersionAsync(recipe.TenantId, token).ConfigureAwait(false);
        if (final is null || final.Version != before.Version || !final.Bytes.AsSpan().SequenceEqual(before.Bytes))
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
