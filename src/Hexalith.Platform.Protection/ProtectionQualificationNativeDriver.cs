using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Owned executable native fixture driver. Every call goes through the shipped engine/owner/custody/signing ports or actual scoped native source storage.</summary>
public sealed class ProtectionQualificationNativeDriver(InteractionPayloadProtectionService engine, ErasureRootCustody custody, ErasureRootOccurrenceKeyProvider keys,
    IErasureRootOccurrenceSource source, DaprClient native, IGovernanceScopeGuard guard, IDeletionProtectionOwner protection,
    IDeletionConsumptionActor consumption, IDeletionCapabilitySigningActor signer, DeletionCapabilityRevocationSubscriber compromise,
    ProtectionQualificationFaults faults, IReadOnlySet<string> fixtureSourceComponents, IErasureRootAuthorization authorization, ProtectionQualificationProducerIdentity? producer = null)
{
    private ProtectionQualificationProducerIdentity? _producer = producer;
    internal ProtectionQualificationProducerIdentity Producer
    {
        get => _producer ??= ProtectionQualificationProducerIdentity.Capture(engine, custody.Installation);
        set => _producer = value;
    }
    /// <summary>Checks every enrollment and required plaintext/seed input without retaining operation, source, root or call state.</summary>
    public async Task ValidateInputsAsync(ProtectionQualificationActionRecipe recipe, JsonObject request, CancellationToken token)
    {
        string preflightTenant = request["targets"]![0]!["TenantId"]!.GetValue<string>();
        var priorHead = await custody.ReadAsync(preflightTenant, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Current recipe enrollment required.");
        await custody.LoadQualificationAsync(priorHead.Head, request["bindingDigest"]?.GetValue<string>() ?? Producer.BindingDigest, token).ConfigureAwait(false);
        var earlier = priorHead.Head.QualificationOperations.Values.Where(value => value.SafeRequest["fixtureId"]?.GetValue<string>() == request["fixtureId"]?.GetValue<string>())
            .SelectMany(value => value.Calls.Where(call => call.Value.Result is not null)).ToDictionary(call => call.Key, call => call.Value.Result, StringComparer.Ordinal);
        ProtectionQualificationInputSchema.Validate(recipe, request, earlier);
        foreach (var planned in recipe.Before.Concat([recipe.Effect]).Concat(recipe.Race is { } plannedRace ? [plannedRace] : []).Concat(recipe.After))
        {
            if (!await authorization.AuthorizeAsync(preflightTenant, planned.Id, "PreflightNative" + planned.Kind,
                ErasureRootCustody.Hash(new { planned, Request = ProtectionQualificationOperationJournal.Digest(request), Phase = "Preflight" }), token).ConfigureAwait(false))
            { throw new InvalidOperationException("Complete native recipe permission required before effects."); }
        }
        if (request["canaryBase64"] is { } canary)
        {
            byte[] bytes = Convert.FromBase64String(canary.GetValue<string>());
            try { if (bytes.Length == 0) { throw new InvalidOperationException("Exact nonempty canary required."); } }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        var producedCells = new HashSet<string>(StringComparer.Ordinal); var beforeEffectCells = new HashSet<string>(StringComparer.Ordinal);
        bool earlierOwnerWrite = false, beforeEffectOwnerWrite = false;
        bool FutureOriginal(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                if (obj["$original"] is JsonObject original) { return !earlier.ContainsKey(original["id"]!.GetValue<string>()); }
                return obj.Any(item => FutureOriginal(item.Value));
            }
            return node is JsonArray array && array.Any(FutureOriginal);
        }
        ProtectionQualificationNativeCell? KnownCell(JsonNode? node)
        {
            if (node is not JsonObject obj || FutureOriginal(obj)) { return null; }
            var cellCall = ProtectionQualificationRecipeBinding.Bind(new("preflight-cell", ProtectionQualificationNativeCallKind.Backup, obj), request, earlier);
            return cellCall.Payload.Deserialize<ProtectionQualificationNativeCell>(ErasureRootCustody.Json)!;
        }
        void Produced(ProtectionQualificationNativeCell? cell, HashSet<string>? transaction = null)
        {
            if (cell is null) { return; } Check(cell); string location = ProtectionQualificationNativeCell.LocationId(cell);
            if (transaction is not null && !transaction.Add(location)) { throw new InvalidOperationException("Distinct atomic source/producer/completion/writer cells required before effects."); }
            producedCells.Add(location);
        }
        foreach (var template in recipe.Before.Concat([recipe.Effect]).Concat(recipe.Race is { } race ? [race] : []).Concat(recipe.After))
        {
            if (template.Id == recipe.Effect.Id) { beforeEffectCells = new(producedCells, StringComparer.Ordinal); beforeEffectOwnerWrite = earlierOwnerWrite; }
            bool isCompetingRace = recipe.Race?.Id == template.Id;
            var availableCells = isCompetingRace ? beforeEffectCells : producedCells;
            JsonNode? readCell = template.Kind switch
            {
                ProtectionQualificationNativeCallKind.UnsealEvent or ProtectionQualificationNativeCallKind.ReplayEvent or ProtectionQualificationNativeCallKind.ReplayCopy
                    or ProtectionQualificationNativeCallKind.Backup or ProtectionQualificationNativeCallKind.Restore => template.Payload,
                ProtectionQualificationNativeCallKind.ReplaySnapshot => template.Payload["Source"],
                _ => null,
            };
            if (KnownCell(readCell) is { } requiredCell)
            {
                Check(requiredCell);
                var actualCell = template.Kind == ProtectionQualificationNativeCallKind.Restore ? requiredCell with { Key = requiredCell.Key + ":backup" } : requiredCell;
                // Owner state produced by an earlier owner transition is covered by the exact independently
                // granted preflight phase above. No future body or result digest is invented here.
                bool futureOwnerState = (isCompetingRace ? beforeEffectOwnerWrite : earlierOwnerWrite) && actualCell.AnchorScope is not null && priorHead.Head.Anchors.ContainsKey(actualCell.AnchorScope);
                if (!availableCells.Contains(ProtectionQualificationNativeCell.LocationId(actualCell)) && !futureOwnerState)
                {
                    var available = await native.GetStateAsync<JsonElement>(actualCell.Component, actualCell.Key, ConsistencyMode.Strong, actualCell.Metadata, token).ConfigureAwait(false);
                    if (available.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) { throw new InvalidOperationException("Exact existing native input cell required before effects."); }
                }
            }
            if (template.Kind is ProtectionQualificationNativeCallKind.SealEvent or ProtectionQualificationNativeCallKind.SealSnapshot)
            {
                var writes = new HashSet<string>(StringComparer.Ordinal);
                var output = KnownCell(template.Payload["Source"] ?? template.Payload["source"]); Produced(output, writes);
                if (output is not null)
                {
                    Produced(output with { Key = output.Key + ":producer" }, writes); Produced(output with { Key = output.CompletionKey ?? output.Key + ":completion" }, writes);
                    if (template.Kind == ProtectionQualificationNativeCallKind.SealSnapshot) { Produced(output with { Key = output.Key + ":cache" }, writes); }
                }
                if (template.Payload["Enrollment"] is JsonObject || template.Payload["enrollment"] is JsonObject)
                { var original = (template.Payload["Enrollment"] ?? template.Payload["enrollment"])!; Produced(KnownCell(original["Writer"]), writes); }
                if (template.Payload["Entries"] is JsonArray outputEntries) { foreach (var entry in outputEntries) { Produced(KnownCell(entry?["Writer"]), writes); } }
            }
            else if (KnownCell(readCell) is { } copying && template.Kind is ProtectionQualificationNativeCallKind.Backup or ProtectionQualificationNativeCallKind.Restore
                or ProtectionQualificationNativeCallKind.ReplayCopy or ProtectionQualificationNativeCallKind.ReplayEvent or ProtectionQualificationNativeCallKind.ReplaySnapshot)
            {
                string key = template.Kind == ProtectionQualificationNativeCallKind.Backup ? copying.Key + ":backup"
                    : template.Kind == ProtectionQualificationNativeCallKind.Restore ? copying.AnchorScope is null ? copying.Key + ":restored" : copying.Key : copying.Key + ":replay";
                Produced(copying with { Key = key });
            }
            earlierOwnerWrite |= template.Kind is ProtectionQualificationNativeCallKind.Guard or ProtectionQualificationNativeCallKind.GuardOwnersEffective
                or ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch or ProtectionQualificationNativeCallKind.Register or ProtectionQualificationNativeCallKind.RegisterIfDispatched
                or ProtectionQualificationNativeCallKind.Reserve or ProtectionQualificationNativeCallKind.ReserveIfDispatched or ProtectionQualificationNativeCallKind.Block or ProtectionQualificationNativeCallKind.Compromise
                or ProtectionQualificationNativeCallKind.Activate or ProtectionQualificationNativeCallKind.Lifecycle;

            void CheckInstalledCells(JsonNode? node)
            {
                if (node is JsonObject obj)
                {
                    if (obj["Component"] is JsonValue && obj["Key"] is JsonValue && obj["Metadata"] is JsonObject)
                    { Check(obj.Deserialize<ProtectionQualificationNativeCell>(ErasureRootCustody.Json)!); }
                    foreach (var item in obj) { CheckInstalledCells(item.Value); }
                }
                else if (node is JsonArray values) { foreach (var item in values) { CheckInstalledCells(item); } }
            }
            CheckInstalledCells(template.Payload);
            if (template.Kind == ProtectionQualificationNativeCallKind.Fault)
            {
                var faultInput = ProtectionQualificationRecipeBinding.Bind(template, request, earlier);
                if (!await faults.PreflightAsync(faultInput.Payload["stepId"]!.GetValue<string>(), faultInput.Payload["boundary"]!.GetValue<string>(), token).ConfigureAwait(false))
                { throw new InvalidOperationException("Exact installed fault-control enrollment required before effects."); }
            }
            if (template.Kind == ProtectionQualificationNativeCallKind.Compromise)
            {
                var known = ProtectionQualificationRecipeBinding.Bind(template, request, earlier);
                if (compromise is null || !await compromise.PreflightAsync(known.Payload["envelope"]!.Deserialize<DeletionCapabilityRevocationEnvelope>()!, known.Payload["signedEvidence"]!.GetValue<string>(), token).ConfigureAwait(false))
                { throw new InvalidOperationException("Exact independently verified compromise inputs required before effects."); }
            }
            if (template.Kind is ProtectionQualificationNativeCallKind.Reserve or ProtectionQualificationNativeCallKind.ReserveIfDispatched)
            {
                string tenant = request["targets"]![0]!["TenantId"]!.GetValue<string>();
                if (!await authorization.AuthorizeAsync(tenant, template.Id, "RetainReserveRefusalOriginal", ErasureRootCustody.Hash(new { Template = template, RequestDigest = ProtectionQualificationOperationJournal.Digest(request), Phase = "Preflight" }), token).ConfigureAwait(false))
                { throw new InvalidOperationException("DependencyNotAvailable: exact reserve refusal retention permission required before admission."); }
            }
            if (request["action"]?.GetValue<string>() == "ActivateReattestedDeletionBatch" && template.Kind == ProtectionQualificationNativeCallKind.Guard
                && template.Payload["transition"]?["Batch"]?["Capability"] is null)
            { throw new InvalidOperationException("Exact activation mirror inputs required before admission."); }
            if (template.Kind is not (ProtectionQualificationNativeCallKind.SealEvent or ProtectionQualificationNativeCallKind.SealSnapshot)) { continue; }
            var call = ProtectionQualificationRecipeBinding.Bind(template, request, new Dictionary<string, JsonNode?>());
            bool mixed = call.Kind == ProtectionQualificationNativeCallKind.SealSnapshot && call.Payload.ContainsKey("Entries");
            ErasureRootOccurrenceEnrollment[] entries;
            if (mixed)
            {
                var input = call.Payload.Deserialize<ProtectionQualificationNativeSnapshot>()!;
                entries = DescribeMixedSnapshot(input, request["targets"]!.Deserialize<ProtectionTarget[]>()!, request["canaryBase64"]?.GetValue<string>() ?? "preflight", token).Originals;
            }
            else
            {
                var input = ReadSeal(call.Payload); Check(input.Source);
                if (input.Enrollment.Writer is not { } writer || writer.Component != input.Source.Component
                    || ErasureRootCustody.Hash(writer.Metadata) != ErasureRootCustody.Hash(input.Source.Metadata))
                { throw new InvalidOperationException("Exact source writer partition required before admission."); }
                entries = [input.Enrollment];
            }
            foreach (var entry in entries)
            {
                if (!ErasureRootOccurrenceBindings.ValidEnrollment(entry, entry.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(entry.Binding.Identity), entry.Binding.Identity.Target)
                    || !await keys.PreflightEnrollmentAsync(entry, token).ConfigureAwait(false)) { throw new InvalidOperationException("Complete source inputs required before admission."); }
            }
            foreach (var entry in entries)
            {
                if (!await authorization.AuthorizeAsync(entry.Binding.Identity.Target.TenantId, entry.Binding.OperationId, "RetainQualificationProducerOriginal",
                    ErasureRootCustody.Hash(new { entry.Binding, Source = call.Payload }), token).ConfigureAwait(false))
                { throw new InvalidOperationException("DependencyNotAvailable: exact producer retention permission required before admission."); }
            }
            if (call.Kind == ProtectionQualificationNativeCallKind.SealEvent)
            {
                var seedProducer = ReadSeal(call.Payload);
                if (!await authorization.AuthorizeAsync(seedProducer.Enrollment.Binding.Identity.Target.TenantId,
                    seedProducer.Enrollment.Binding.OperationId, "RetainQualificationSeedOriginal",
                    ErasureRootCustody.Hash(new { seedProducer.Enrollment.Binding, SourceIdentity = ProtectionQualificationNativeCell.LocationId(seedProducer.Source), Phase = "Preflight" }), token).ConfigureAwait(false))
                { throw new InvalidOperationException("DependencyNotAvailable: exact seed producer retention permission required before admission."); }
            }
            if (request["canaryBase64"] is null && (mixed || !ReadSeal(call.Payload).ChangePayload))
            {
                var seed = mixed ? call.Payload.Deserialize<ProtectionQualificationNativeSnapshot>()!.SeedSource : ReadSeal(call.Payload).SeedSource;
                if (seed is null) { throw new InvalidOperationException("Exact usable sealed seed required before admission."); }
                var original = await ReadEventAsync(seed, token).ConfigureAwait(false);
                var head = await custody.ReadAsync(original.Binding.Identity.Target.TenantId, token).ConfigureAwait(false);
                string id = ErasureRootOccurrenceBindings.Id(original.Binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity));
                if (head is not { } current || current.Head.Pending is not null
                    || (await custody.ReadSourceAsync(current.Head, id, token).ConfigureAwait(false) is not { } enrollment) || enrollment.Binding != original.Binding
                    || (await custody.ReadOccurrenceAsync(current.Head, id, token).ConfigureAwait(false) is not { } occurrence) || occurrence.EncryptionRequestDigest is null
                    || await custody.ReadSealedAsync(current.Head, id, token).ConfigureAwait(false) is null || !current.Head.Targets.ContainsKey(ErasureRootCustody.TargetId(original.Binding.Identity.Target))
                    || enrollment.Writer is not { } writer
                    || await custody.ReadRetainedAsync(current.Head, "QualificationSeedOriginals", current.Head.QualificationSeedOriginals, ProtectionQualificationNativeCell.LocationId(seed), token).ConfigureAwait(false) is not { } seedOriginal
                    || seedOriginal.Binding != original.Binding || seedOriginal.SealedPayloadSha256 != Convert.ToHexString(SHA256.HashData(original.Protected.PayloadBytes)))
                { throw new InvalidOperationException("Exact persisted readable sealed seed required before admission."); }
                Check(new(writer.Component, writer.Key, writer.Metadata));
                var proof = await native.GetStateAsync<ErasureRootWriterProof>(writer.Component, writer.Key, ConsistencyMode.Strong, writer.Metadata, token).ConfigureAwait(false);
                if (proof is not { Persisted: true, WriterRetired: true } || string.IsNullOrWhiteSpace(proof.ReceiptId) || proof.Binding != original.Binding
                    || proof.SealedPayloadSha256 != Convert.ToHexString(SHA256.HashData(original.Protected.PayloadBytes)))
                { throw new InvalidOperationException("Exact sealed seed source receipt required before admission."); }
            }
        }
    }
    /// <summary>Captures the actual source-owned compare/census before the durable native-call admission. Execution never refreshes an admitted compare.</summary>
    public async Task<ProtectionQualificationNativeCall> PrepareCallAsync(ProtectionQualificationNativeCall input, CancellationToken token)
    {
        var payload = (JsonObject)input.Payload.DeepClone();
        if (input.Kind is ProtectionQualificationNativeCallKind.Reserve or ProtectionQualificationNativeCallKind.ReserveIfDispatched)
        {
            var request = payload.Deserialize<DeletionBatchConsumptionRequest>()!;
            if (!await authorization.AuthorizeAsync(request.Capability.TenantId, input.Id, "RetainReserveRefusalOriginal", ErasureRootCustody.Hash(input), token).ConfigureAwait(false))
            { throw new InvalidOperationException("Exact reserve refusal retention permission required before native-call admission."); }
        }
        if (input.Kind == ProtectionQualificationNativeCallKind.Sign)
        {
            var capability = payload.Deserialize<DeletionBatchCapabilityV1>()!;
            var current = await guard.ReadAsync(capability.TenantId, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Native signing compare unavailable.");
            var deletion = current.Deletions.Single(value => value.RequestId == capability.DeletionRequestId);
            var batch = deletion.Batches.Single(value => value.BatchId == capability.BatchId);
            if (deletion.SealId != capability.DestructionSealId || batch.ManifestDigest != capability.ManifestDigest || batch.Kind != capability.BatchKind || batch.Ordinal != capability.BatchOrdinal)
            { throw new InvalidOperationException("Exact native sealed batch required before signing."); }
            payload = (JsonObject)JsonSerializer.SerializeToNode(capability with { IntendedIssuedGuardRevision = current.Revision })!;
        }
        if (input.Kind == ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch)
        {
            var transition = payload["transition"]!.Deserialize<GovernanceGuardTransition>()!;
            if (transition.Operation != GovernanceGuardOperation.RegisterHold) { throw new InvalidOperationException("Exact hold continuation required."); }
            var state = await guard.ReadAsync(transition.TenantId, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Current hold continuation compare unavailable.");
            payload["transition"] = JsonSerializer.SerializeToNode(transition with { OperationId = transition.OperationId + ":dispatch-won-continuation", ExpectedGuardRevision = state.Revision });
        }
        if (input.Kind == ProtectionQualificationNativeCallKind.Guard)
        {
            var transition = payload["transition"]!.Deserialize<GovernanceGuardTransition>()!;
            if (payload["UseCurrentGuardRevision"]?.GetValue<bool>() is true)
            { transition = transition with { ExpectedGuardRevision = (await guard.ReadAsync(transition.TenantId, token).ConfigureAwait(false))?.Revision ?? throw new InvalidOperationException("Native guard compare unavailable.") }; }
            else if (transition.Operation is GovernanceGuardOperation.RecordBatchIssued or GovernanceGuardOperation.ReplaceAttestation or GovernanceGuardOperation.RecordBatchIssuanceStale)
            { transition = transition with { ExpectedGuardRevision = transition.Batch?.Capability?.IntendedIssuedGuardRevision ?? throw new InvalidOperationException("Exact signed intended compare required.") }; }
            if (transition.Operation is GovernanceGuardOperation.RecordBatchIssued or GovernanceGuardOperation.ReplaceAttestation)
            {
                var batch = transition.Batch ?? throw new InvalidOperationException("Actual signed batch required.");
                string signingId = DeletionBatchCapabilityCodec.SigningRequestId(batch.Capability!);
                string signedDigest = DeletionBatchCapabilityIdentity.SignedArtifactDigest(batch.DetachedJws);
                if (batch.SigningRequestId != signingId || batch.AttestationDigest != signedDigest) { throw new InvalidOperationException("Actual canonical signed artifact required."); }
                transition = transition with { OperationId = DeletionBatchCapabilityIdentity.IssueOperationId(signingId, signedDigest) };
            }
            payload["transition"] = JsonSerializer.SerializeToNode(transition); payload.Remove("UseCurrentGuardRevision");
        }
        if (input.Kind == ProtectionQualificationNativeCallKind.GuardOwnersEffective)
        {
            var transition = payload["transition"]!.Deserialize<GovernanceGuardTransition>()!;
            var state = await guard.ReadAsync(transition.TenantId, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Native owner census unavailable.");
            var deletion = state.Deletions.Single(value => value.RequestId == transition.DeletionRequestId);
            payload["transition"] = JsonSerializer.SerializeToNode(transition with { ExpectedGuardRevision = state.Revision });
            payload["ownerIds"] = JsonSerializer.SerializeToNode(deletion.RequiredOwnerIds.Order(StringComparer.Ordinal).ToArray());
            payload["expectedOrdinal"] = deletion.Ordinal; payload["obligationDigest"] = GovernanceScopeGuardReducer.ObligationDigest(deletion.ObligationIds);
        }
        if (input.Kind == ProtectionQualificationNativeCallKind.LookupProvider)
        {
            var request = payload["request"]!.Deserialize<DeletionBatchConsumptionRequest>()!;
            var original = await consumption.ReadReservationOriginalAsync(request).WaitAsync(token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Actual original reservation required for provider lookup.");
            payload["reservationReceiptId"] = original.Outcome.ReceiptId ?? throw new InvalidOperationException("Actual reserved receipt required.");
        }
        input = input with { Payload = payload };
        if (CopyCell(input) is { } cell)
        {
            Check(cell);
            var value = await native.GetStateAsync<JsonElement>(cell.Component, cell.Key + (input.Kind == ProtectionQualificationNativeCallKind.Restore ? ":backup" : ""), ConsistencyMode.Strong, cell.Metadata, token).ConfigureAwait(false);
            if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) { throw new InvalidOperationException("Exact native source required before admission."); }
            input = input with { SourceDigest = ErasureRootCustody.Hash(value) };
        }
        if (input.Kind is ProtectionQualificationNativeCallKind.RegisterIfDispatched or ProtectionQualificationNativeCallKind.ReserveIfDispatched
            && await IsHoldPreventedAsync(payload.Deserialize<DeletionBatchConsumptionRequest>()!, token).ConfigureAwait(false))
        { input = input with { HoldBlockedOriginal = await guard.ReadAsync(payload.Deserialize<DeletionBatchConsumptionRequest>()!.Capability.TenantId, token).ConfigureAwait(false) }; }
        return input;
    }
    /// <summary>Captures both competing hold/dispatch commands at one actual owner revision before either is durably admitted or dispatched.</summary>
    public async Task<(ProtectionQualificationNativeCall Effect, ProtectionQualificationNativeCall Race)> PrepareRaceAsync(
        ProtectionQualificationNativeCall effect, ProtectionQualificationNativeCall race, CancellationToken token)
    {
        if (effect.Kind == ProtectionQualificationNativeCallKind.Guard && race.Kind == ProtectionQualificationNativeCallKind.Guard
            && effect.Payload["transition"]!.Deserialize<GovernanceGuardTransition>() is { Operation: GovernanceGuardOperation.CommitDispatch } dispatch
            && race.Payload["transition"]!.Deserialize<GovernanceGuardTransition>() is { Operation: GovernanceGuardOperation.RegisterHold } hold)
        {
            if (hold.TenantId != dispatch.TenantId || hold.DeletionRequestId != dispatch.DeletionRequestId) { throw new InvalidOperationException("Exact same-owner race required."); }
            var current = await guard.ReadAsync(dispatch.TenantId, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Actual race comparison unavailable.");
            var left = (JsonObject)effect.Payload.DeepClone(); var right = (JsonObject)race.Payload.DeepClone();
            left["transition"] = JsonSerializer.SerializeToNode(dispatch with { ExpectedGuardRevision = current.Revision });
            right["transition"] = JsonSerializer.SerializeToNode(hold with { ExpectedGuardRevision = current.Revision });
            effect = effect with { Payload = left }; race = race with { Payload = right };
        }
        return (await PrepareCallAsync(effect, token).ConfigureAwait(false), await PrepareCallAsync(race, token).ConfigureAwait(false));
    }
    /// <summary>Identifies only a previously admitted native reservation. Fixture preparation may complete that original; it cannot authorize a new manifest.</summary>
    public async Task<ProtectionQualificationNativeCall?> PendingRecoveryAsync(string tenant, string fixtureId, CancellationToken token)
    {
        var current = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
        if (current is not { } read || read.Head.Pending is not { } pending) { return null; }
        return new(fixtureId + ":prior-reservation-original", ProtectionQualificationNativeCallKind.LookupProvider,
            new JsonObject { ["request"] = JsonSerializer.SerializeToNode(pending.Request), ["reservationReceiptId"] = pending.ReservationReceiptId });
    }
    /// <summary>Resolves only actual retained original outcomes. It never repeats a native effect, signs, reserves, restores, unwraps or arms another fault.</summary>
    public async Task<JsonNode?> RecoverAsync(ProtectionQualificationNativeCall step, CancellationToken token)
    {
        T Read<T>() => step.Payload.Deserialize<T>(ErasureRootCustody.Json) ?? throw new InvalidOperationException("Exact original native input required.");
        object? result = null;
        if (step.Kind == ProtectionQualificationNativeCallKind.Fault) { return await faults.LookupAsync(step, token).ConfigureAwait(false); }
        if (RetainsExactResult(step.Kind))
        {
            string? callTenant = step.Kind switch
            {
                ProtectionQualificationNativeCallKind.Lookup => step.Payload["tenantId"]?.GetValue<string>(),
                ProtectionQualificationNativeCallKind.LookupProvider => step.Payload["request"]?.Deserialize<DeletionBatchConsumptionRequest>()?.Capability.TenantId,
                ProtectionQualificationNativeCallKind.AuthorizeLifecycle => step.Payload.Deserialize<CustodyKeyLifecycleRequest>()?.Identity.TenantId,
                ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch => step.Payload["transition"]?.Deserialize<GovernanceGuardTransition>()?.TenantId,
                _ => null,
            };
            if (callTenant is null) { return null; }
            var retained = await custody.ReadNativeCallResultAsync(callTenant, step, token).ConfigureAwait(false);
            if (retained is not null) { return retained.DeepClone(); }
            if (step.Kind == ProtectionQualificationNativeCallKind.LookupProvider
                && await authorization.AuthorizeAsync(callTenant, step.Id, "ReadNativeCallOriginal", ErasureRootCustody.Hash(step), token).ConfigureAwait(false))
            {
                var request = step.Payload["request"]!.Deserialize<DeletionBatchConsumptionRequest>()!;
                var observed = await custody.ReadAsync(callTenant, token).ConfigureAwait(false);
                var manifest = observed is { } current ? await custody.ReadManifestOriginalAsync(callTenant, current.Head, request.Capability.BatchId, token).ConfigureAwait(false) : null;
                string reservation = step.Payload["reservationReceiptId"]!.GetValue<string>();
                if (manifest?.CompletingQualificationCallDigest == ErasureRootCustody.Hash(step) && manifest.RequestDigest == ErasureRootCustody.Hash(new { request, reservation }))
                { return Result(new DeletionManifestProviderResult(callTenant, request.Capability.BatchId, reservation, DeletionManifestProviderState.Consumed, manifest.Receipts)); }
            }
            if (step.Kind == ProtectionQualificationNativeCallKind.Fault) { return await faults.LookupAsync(step, token).ConfigureAwait(false); }
            return null;
        }
        if (step.HoldBlockedOriginal is not null && step.Kind is ProtectionQualificationNativeCallKind.RegisterIfDispatched or ProtectionQualificationNativeCallKind.ReserveIfDispatched)
        {
            string tenant = step.Payload.Deserialize<DeletionBatchConsumptionRequest>()!.Capability.TenantId;
            if (!await authorization.AuthorizeAsync(tenant, step.Id, "LookupConditionalHoldOriginal", ErasureRootCustody.Hash(step), token).ConfigureAwait(false)) { return null; }
            var original = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
            return original is { } head && await custody.ReadConditionalHoldOriginalAsync(tenant, head.Head, ErasureRootCustody.Hash(step), token).ConfigureAwait(false) is { } retainedResult ? retainedResult.DeepClone() : null;
        }
        if (CopyCell(step) is { } originalCell)
        {
            if (step.OperationId is null || step.RequestSha256 is null || step.SourceDigest is null) { return null; }
            var raw = await native.GetStateAsync<JsonElement>(originalCell.Component, OriginalKey(originalCell, step), ConsistencyMode.Strong, originalCell.Metadata, token).ConfigureAwait(false);
            var original = raw.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : raw.Deserialize<ProtectionQualificationCopyOriginal>();
            return original?.CallDigest == ErasureRootCustody.Hash(step) && original.SourceIdentity == ProtectionQualificationNativeCell.LocationId(originalCell)
                && original.SourceDigest == step.SourceDigest ? original.Result.DeepClone() : null;
        }
        switch (step.Kind)
        {
            case ProtectionQualificationNativeCallKind.WrapFixtureRoot:
                var wrappedRoot = Read<ProtectionQualificationNativeRoot>();
                var retainedRoot = await custody.ReadAsync(wrappedRoot.Identity.TenantId, token).ConfigureAwait(false);
                if (retainedRoot is { } rootHead)
                {
                    string rootId = ErasureRootCustody.TargetId(wrappedRoot.Identity);
                    var registration = rootHead.Head.Targets.GetValueOrDefault(rootId)?.Registration ?? (await custody.ReadErasedAsync(rootHead.Head, rootId, token).ConfigureAwait(false))?.Registration;
                    if (registration?.Identity == wrappedRoot.Identity && registration.OperationId == wrappedRoot.OperationId
                        && registration.ExpectedRevision == wrappedRoot.ExpectedRevision) { result = registration; }
                }
                break;
            case ProtectionQualificationNativeCallKind.RegisterIfDispatched:
            case ProtectionQualificationNativeCallKind.Register:
                result = (await consumption.ReadRegistrationOriginalAsync(Read<DeletionBatchConsumptionRequest>()).WaitAsync(token).ConfigureAwait(false))?.Outcome; break;
            case ProtectionQualificationNativeCallKind.ReserveIfDispatched:
            case ProtectionQualificationNativeCallKind.Reserve:
                var request = Read<DeletionBatchConsumptionRequest>();
                if (await authorization.AuthorizeAsync(request.Capability.TenantId, step.Id, "LookupReserveRefusalOriginal", ErasureRootCustody.Hash(step), token).ConfigureAwait(false))
                {
                    var refusedHead = await custody.ReadAsync(request.Capability.TenantId, token).ConfigureAwait(false);
                    if (refusedHead is { } retained && await custody.ReadConditionalHoldOriginalAsync(request.Capability.TenantId, retained.Head, "reserve:" + ErasureRootCustody.Hash(step), token).ConfigureAwait(false) is { } refusal)
                    { return refusal.DeepClone(); }
                }
                var reserved = await consumption.ReadReservationOriginalAsync(request).WaitAsync(token).ConfigureAwait(false);
                var read = await custody.ReadAsync(request.Capability.TenantId, token).ConfigureAwait(false);
                if (reserved is not null && read is { } nativeHead && nativeHead.Head.Anchors.TryGetValue(DeletionConsumptionActor.GetOwnerScope(request.Capability.TenantId), out var anchor))
                {
                    var latest = anchor.Completed.Values.SingleOrDefault(value => value.TargetRevision == anchor.Revision && value.TargetDigest == anchor.Digest);
                    if (latest is not null)
                    {
                        using var ledger = JsonDocument.Parse(latest.TargetBytes);
                        var batch = ledger.RootElement.GetProperty("Batches").EnumerateArray().SingleOrDefault(value => value.GetProperty("Current").GetProperty("Capability").GetProperty("BatchId").GetString() == request.Capability.BatchId);
                        if (batch.ValueKind == JsonValueKind.Object && ErasureRootCustody.Hash(batch.GetProperty("Current").Deserialize<DeletionBatchConsumptionRequest>()) == ErasureRootCustody.Hash(request))
                        { result = batch.GetProperty("Outcome").Deserialize<DeletionConsumptionOutcome>(); }
                    }
                }
                break;
            case ProtectionQualificationNativeCallKind.Block:
            case ProtectionQualificationNativeCallKind.LookupBlock:
                result = (await consumption.ReadBlockOriginalAsync(Read<DeletionBatchBlockRequest>()).WaitAsync(token).ConfigureAwait(false))?.Outcome; break;
            case ProtectionQualificationNativeCallKind.Activate:
            case ProtectionQualificationNativeCallKind.LookupActivation:
                result = (await consumption.ReadActivationOriginalAsync(Read<DeletionReattestationActivation>()).WaitAsync(token).ConfigureAwait(false))?.Outcome; break;
            case ProtectionQualificationNativeCallKind.ReconcileBlockedReplacement:
                var reconciliation = Read<DeletionBlockedReplacementReconciliation>();
                var phase = await protection.ReadBlockedReplacementAsync(reconciliation.Capability, token).ConfigureAwait(false);
                result = phase?.Original.OperationId == reconciliation.OperationId && phase.Original.SigningRequestId == reconciliation.SigningRequestId
                    && phase.Original.DetachedJws == reconciliation.DetachedJws ? phase.Outcome : null; break;
            case ProtectionQualificationNativeCallKind.Sign:
            case ProtectionQualificationNativeCallKind.ObsoleteUnissued:
                result = await signer.LookupAsync(Read<DeletionBatchCapabilityV1>()).WaitAsync(token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Lifecycle:
                var lifecycle = step.Payload["request"]!.Deserialize<CustodyKeyLifecycleRequest>()!;
                result = await custody.LookupAsync(step.Payload["registration"]!.Deserialize<CustodyKeyRegistration>()!, lifecycle, ErasureRootCustody.Hash(lifecycle), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch:
            case ProtectionQualificationNativeCallKind.Guard:
                var transition = step.Payload["transition"]!.Deserialize<GovernanceGuardTransition>()!;
                var targets = step.Payload["targets"]!.Deserialize<GuardedStateMutation[]>()!;
                string intent = ErasureRootCustody.Hash(new { Transition = transition, TargetMutationDigest = ErasureRootCustody.Hash(targets) });
                var guardState = await guard.ReadAsync(transition.TenantId, token).ConfigureAwait(false);
                result = guardState?.Receipts.SingleOrDefault(receipt => receipt.OperationId == transition.OperationId && receipt.IntentDigest == intent);
                if (result is null) { result = await guard.LookupOriginalAsync(transition, targets, token).ConfigureAwait(false); }
                if (result is null)
                {
                    var retainedGuard = await custody.ReadAsync(transition.TenantId, token).ConfigureAwait(false);
                    if (retainedGuard is { } originalHead && await custody.ReadGuardAttemptAsync(transition.TenantId, originalHead.Head, intent, token).ConfigureAwait(false) is { } originalAttempt
                        && ErasureRootCustody.Hash(originalAttempt.Transition) == ErasureRootCustody.Hash(transition)
                        && originalAttempt.TargetMutationDigest == ErasureRootCustody.Hash(targets)) { result = originalAttempt.Receipt; }
                }
                break;
            case ProtectionQualificationNativeCallKind.GuardOwnersEffective:
                var template = step.Payload["transition"]!.Deserialize<GovernanceGuardTransition>()!;
                var currentOwners = await guard.ReadAsync(template.TenantId, token).ConfigureAwait(false);
                if (currentOwners is null) { break; }
                var originals = step.Payload["ownerIds"]!.Deserialize<string[]>()!.Select((owner, index) =>
                {
                    var command = OwnerCycleCommand(step.Payload, owner, index);
                    string intent = ErasureRootCustody.Hash(new { Transition = command, TargetMutationDigest = ErasureRootCustody.Hash(Array.Empty<GuardedStateMutation>()) });
                    return currentOwners.Receipts.SingleOrDefault(receipt => receipt.OperationId == command.OperationId && receipt.IntentDigest == intent);
                }).ToArray();
                if (originals.All(original => original?.Status == "Committed")) { result = new { status = "Completed", outcomes = originals }; }
                break;
            case ProtectionQualificationNativeCallKind.Compromise:
                var envelope = step.Payload["envelope"]!.Deserialize<DeletionCapabilityRevocationEnvelope>()!;
                var revocation = await consumption.LookupRevocationAsync(envelope).WaitAsync(token).ConfigureAwait(false);
                var mirrored = await guard.ReadAsync(envelope.TenantId, token).ConfigureAwait(false);
                if (revocation is not null && mirrored is not null && mirrored.Revocations.Any(value => ErasureRootCustody.Hash(value) == ErasureRootCustody.Hash(revocation)))
                { result = new { status = "Completed", receipt = revocation }; }
                break;
            case ProtectionQualificationNativeCallKind.SealEvent:
                var sealedInput = ReadSeal(step.Payload);
                var stored = await ReadEventAsync(sealedInput.Source, token).ConfigureAwait(false);
                if (stored.Binding == sealedInput.Enrollment.Binding && stored.Protected.Metadata is { State: PayloadProtectionState.Protected, Scheme: "hexalith-pdenc-v2" }
                    && stored.Protected.SerializationFormat == "json+pdenc-v2"
                    && await RecoverProducerAsync(step, [sealedInput.Enrollment.Binding], sealedInput.Source, stored.Protected.PayloadBytes, true, token).ConfigureAwait(false) is { } eventCompletion)
                { result = new PayloadProtectionWriteResult(stored.Protected, eventCompletion); }
                break;
            case ProtectionQualificationNativeCallKind.SealSnapshot:
                var snapshotCell = (step.Payload["Source"] ?? step.Payload["source"])!.Deserialize<ProtectionQualificationNativeCell>()!; Check(snapshotCell);
                var snapshot = await native.GetStateAsync<SnapshotProtectionResult>(snapshotCell.Component, snapshotCell.Key, ConsistencyMode.Strong, snapshotCell.Metadata, token).ConfigureAwait(false);
                if (snapshot?.Metadata is { State: PayloadProtectionState.Protected, Scheme: "hexalith-pdenc-v2" })
                {
                    var currentSnapshot = await custody.ReadAsync(step.Payload.ContainsKey("Entries") ? step.Payload.Deserialize<ProtectionQualificationNativeSnapshot>()!.Entries[0].Binding.Identity.Target.TenantId : ReadSeal(step.Payload).Enrollment.Binding.Identity.Target.TenantId, token).ConfigureAwait(false);
                    if (currentSnapshot is not { } snapshotHead) { break; }
                    InteractionProtectionBinding[] bindings;
                    var snapshotState = JsonSerializer.SerializeToNode(snapshot.State) as JsonObject;
                    if (snapshotState?["Format"]?.GetValue<string>() == "json+pdenc-v2-entries")
                    {
                        var carrier = snapshotState.Deserialize<ProtectedSnapshotEntriesV2>()!;
                        await ProtectionQualificationPersistedObserver.HydrateReferencesAsync(custody, snapshotHead.Head, snapshotState, token).ConfigureAwait(false);
                        bindings = carrier.Entries.Select(entry => snapshotHead.Head.SourceOriginals.Values.Single(original => original.Binding.KeyReference == entry.KeyReference
                            && original.Binding.Identity.Target == entry.Target && original.SnapshotEntry?.Path == entry.Path).Binding).ToArray();
                    }
                    else { bindings = [ReadSeal(step.Payload).Enrollment.Binding]; }
                    if (await RecoverProducerAsync(step, bindings, snapshotCell, JsonSerializer.SerializeToUtf8Bytes(snapshot.State), false, token).ConfigureAwait(false) is { } snapshotCompletion)
                    { result = new SnapshotProtectionWriteResult(snapshot, snapshotCompletion); }
                }
                break;
        }
        return Result(result);
    }
    /// <summary>Runs an exact native operation once. Native failures remain unknown and are never converted into a passing label.</summary>
    public async Task<JsonNode?> ExecuteAsync(ProtectionQualificationNativeCall step, JsonObject request, CancellationToken token)
    {
        if (step.Kind is ProtectionQualificationNativeCallKind.SealEvent or ProtectionQualificationNativeCallKind.SealSnapshot)
        { await ValidateInputsAsync(new([], step, null, []), request, token).ConfigureAwait(false); }
        await faults.CheckAsync(step.Id, "before", token).ConfigureAwait(false);
        ProtectionQualificationNativeCell? originalCopyCell = CopyCell(step);
        if (originalCopyCell is not null && step.SourceDigest is null) { step = await PrepareCallAsync(step, token).ConfigureAwait(false); }
        if ((originalCopyCell is not null || step.Kind is ProtectionQualificationNativeCallKind.SealEvent or ProtectionQualificationNativeCallKind.SealSnapshot) && step.OperationId is null)
        { step = step with { OperationId = request["operationId"]?.GetValue<string>() ?? ProtectionQualificationOperationJournal.Digest(request), RequestSha256 = ProtectionQualificationOperationJournal.Digest(request) }; }
        JsonElement? copyValue = null; string? copyKey = null;
        string tenant = request["targets"]![0]!["TenantId"]!.GetValue<string>();
        if (RetainsExactResult(step.Kind) && (step.OperationId is null || step.RequestSha256 is null))
        { step = step with { OperationId = request["operationId"]!.GetValue<string>(), RequestSha256 = ProtectionQualificationOperationJournal.Digest(request) }; }
        string? effectBefore = request["action"]!.GetValue<string>().StartsWith("Reject", StringComparison.Ordinal) ? await EffectAsync(tenant, step, "before", token).ConfigureAwait(false) : null;
        T Read<T>() => step.Payload.Deserialize<T>(ErasureRootCustody.Json) ?? throw new InvalidOperationException("Exact typed native input required.");
        object? result;
        switch (step.Kind)
        {
            case ProtectionQualificationNativeCallKind.WrapFixtureRoot:
                var control = Read<ProtectionQualificationNativeRoot>();
                var expected = request["targets"]!.Deserialize<ProtectionTarget[]>()!;
                if (request["caseId"]?.GetValue<string>() != "P13" || control.Identity.TenantId != tenant
                    || control.Identity.ObjectId != request["fixtureId"]!.GetValue<string>() + "-accepted-control"
                    || expected.Any(value => ErasureRootCustody.TargetId(value) == ErasureRootCustody.TargetId(control.Identity)))
                { throw new InvalidOperationException("Distinct source-owned accepted-set control target required."); }
                result = await custody.WrapAsync(control.Identity, control.OperationId, control.ExpectedRevision, token).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Actual accepted-set control root unavailable.");
                break;
            case ProtectionQualificationNativeCallKind.SealEvent:
                var sealing = ReadSeal(step.Payload);
                try { result = await SealAsync(step, sealing, request, sealing.ChangePayload ? Convert.ToBase64String("different-synthetic-owner-payload"u8) : null, token).ConfigureAwait(false); }
                catch (PayloadProtectionOccurrenceConflictException) when (sealing.ChangePayload)
                { result = new { status = "Conflict" }; }
                break;
            case ProtectionQualificationNativeCallKind.UnsealEvent:
                var opening = await ReadEventAsync(Read<ProtectionQualificationNativeCell>(), token).ConfigureAwait(false);
                var opened = await engine.TryUnprotectEventPayloadAsync(opening.Binding.Identity.Owner, opening.Binding.Identity.PayloadTypeId, opening.Protected.PayloadBytes,
                    opening.Protected.SerializationFormat, opening.Protected.Metadata, ErasureRootOccurrenceBindings.Occurrence(opening.Binding.Identity), token).ConfigureAwait(false);
                if (!opened.IsReadable) { throw new InvalidOperationException("Actual native payload unreadable."); }
                try
                {
                    using var clear = JsonDocument.Parse(opened.PayloadBytes!);
                    result = new { canaryBase64 = clear.RootElement.GetProperty("ProtectedContent").GetProperty("Value").GetProperty("CanaryBase64").GetString() };
                }
                finally { CryptographicOperations.ZeroMemory(opened.PayloadBytes!); }
                break;
            case ProtectionQualificationNativeCallKind.ReplayCopy:
                var copyCell = Read<ProtectionQualificationNativeCell>(); Check(copyCell);
                var copy = await native.GetStateAsync<JsonElement>(copyCell.Component, copyCell.Key, ConsistencyMode.Strong, copyCell.Metadata, token).ConfigureAwait(false);
                if (copy.TryGetProperty("Binding", out _))
                { result = await ReplayEventValueAsync(step, request, token).ConfigureAwait(false); }
                else
                {
                    var copySnapshot = copy.Deserialize<SnapshotProtectionResult>() ?? throw new InvalidOperationException("Actual snapshot copy required.");
                    var rootHead = await custody.ReadAsync(tenant, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Current copy originals unavailable.");
                    var snapshotState = JsonSerializer.SerializeToElement(copySnapshot.State);
                    string reference = snapshotState.TryGetProperty("Entries", out var entries) ? entries[0].GetProperty("KeyReference").GetString()! : copySnapshot.Metadata!.KeyAlias!;
                    var original = await custody.ReadSourceReferenceAsync(rootHead.Head, reference, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Exact snapshot source unavailable.");
                    result = await ReplaySnapshotAsync(new(original.Binding.Identity.Owner, original.SnapshotEntry?.Parent ?? ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity), copyCell), token).ConfigureAwait(false);
                }
                break;
            case ProtectionQualificationNativeCallKind.ReplayEvent:
                result = await ReplayEventValueAsync(step, request, token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Lifecycle:
                var lifecycle = step.Payload["request"]!.Deserialize<CustodyKeyLifecycleRequest>()!;
                var registration = step.Payload["registration"]!.Deserialize<CustodyKeyRegistration>()!;
                result = await custody.ExecuteAsync(registration, lifecycle, ErasureRootCustody.Hash(lifecycle), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.ReplaySnapshot:
                result = await ReplaySnapshotAsync(Read<ProtectionQualificationNativeSnapshotRead>(), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.AuthorizeLifecycle:
                var exactFence = Read<CustodyKeyLifecycleRequest>();
                var lifecycleEvidence = await authorization.ReadLifecycleEvidenceAsync(exactFence, token).ConfigureAwait(false);
                result = new { status = lifecycleEvidence is not null ? "Authorized" : "Denied", requestDigest = ErasureRootCustody.Hash(exactFence), fenceTokenId = exactFence.FenceTokenId,
                    operationId = exactFence.OperationId, receiptId = lifecycleEvidence?.ReceiptId, target = new ProtectionTarget(exactFence.Identity.TenantId, exactFence.Identity.ObjectId, exactFence.Identity.KeyAlias) }; break;
            case ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch:
                var holdContinuation = step.Payload["transition"]!.Deserialize<GovernanceGuardTransition>()!;
                var currentGuard = await guard.ReadAsync(holdContinuation.TenantId, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Actual race originals unavailable.");
                var held = currentGuard.Holds.SingleOrDefault(value => value.HoldId == holdContinuation.Hold!.HoldId && value.Active);
                if (held is not null) { result = new { status = "AlreadyCommitted", holdId = held.HoldId }; }
                else
                {
                    var dispatched = currentGuard.Deletions.Single(value => value.RequestId == holdContinuation.DeletionRequestId).Batches.Any(value => !string.IsNullOrEmpty(value.DispatchReceiptId));
                    if (!dispatched) { throw new InvalidOperationException("Only actual dispatch-winning race permits a fresh hold continuation."); }
                    result = await guard.ExecuteAsync(holdContinuation, step.Payload["targets"]!.Deserialize<GuardedStateMutation[]>()!, token).ConfigureAwait(false);
                }
                break;
            case ProtectionQualificationNativeCallKind.Guard:
                var transition = step.Payload["transition"]!.Deserialize<GovernanceGuardTransition>()!;
                result = await guard.ExecuteAsync(transition, step.Payload["targets"]!.Deserialize<GuardedStateMutation[]>()!, token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.GuardOwnersEffective:
                result = await MakeOwnersEffectiveAsync(step.Payload, token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.RegisterIfDispatched:
            case ProtectionQualificationNativeCallKind.ReserveIfDispatched:
                var conditional = Read<DeletionBatchConsumptionRequest>();
                if (step.HoldBlockedOriginal is not null) { result = HoldBlockedResult(step); break; }
                if (await IsHoldPreventedAsync(conditional, token).ConfigureAwait(false)) { throw new InvalidOperationException("Hold won after call admission; exact refusal preparation required."); }
                result = step.Kind == ProtectionQualificationNativeCallKind.RegisterIfDispatched ? await protection.RegisterAsync(conditional, token).ConfigureAwait(false)
                    : await protection.ReserveAndConsumeAsync(conditional, token).ConfigureAwait(false);
                if (step.Kind == ProtectionQualificationNativeCallKind.ReserveIfDispatched && result is DeletionConsumptionOutcome { Status: DeletionConsumptionStatus.ConsumptionBlocked } conditionalRefusal)
                { await RetainReserveRefusalAsync(step, conditional, conditionalRefusal, token).ConfigureAwait(false); }
                break;
            case ProtectionQualificationNativeCallKind.Register: result = await protection.RegisterAsync(Read<DeletionBatchConsumptionRequest>(), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Reserve:
                var reserveRequest = Read<DeletionBatchConsumptionRequest>();
                var reserveResult = await protection.ReserveAndConsumeAsync(reserveRequest, token).ConfigureAwait(false);
                if (reserveResult.Status == DeletionConsumptionStatus.ConsumptionBlocked)
                { await RetainReserveRefusalAsync(step, reserveRequest, reserveResult, token).ConfigureAwait(false); }
                result = reserveResult; break;
            case ProtectionQualificationNativeCallKind.RejectSubstitutions:
                var denied = new JsonArray(); bool accepted = false;
                foreach (var variant in ProtectionQualificationSubstitutions.Create(request["action"]!.GetValue<string>(), Read<DeletionBatchConsumptionRequest>(), request["otherTenantReference"]!.GetValue<string>()))
                {
                    JsonNode? outcome;
                    try
                    {
                        var nativeOutcome = await protection.ReserveAndConsumeAsync(variant.Request, token).ConfigureAwait(false);
                        accepted |= nativeOutcome.Status is DeletionConsumptionStatus.ConsumptionReserved or DeletionConsumptionStatus.Consumed or DeletionConsumptionStatus.AlreadyDestroyedByBatch;
                        outcome = Result(nativeOutcome);
                    }
                    catch (ArgumentException) { outcome = new JsonObject { ["status"] = "Denied", ["validation"] = "ArgumentRejected" }; }
                    denied.Add(new JsonObject { ["mutation"] = variant.Mutation, ["outcome"] = outcome });
                }
                result = new JsonObject { ["status"] = accepted ? "UnexpectedAccepted" : "Denied", ["outcomes"] = denied }; break;
            case ProtectionQualificationNativeCallKind.RejectForeignRoot:
                var root = Read<CustodyKeyRegistration>();
                string other = request["otherTenantReference"]!.GetValue<string>();
                if (root.Identity.TenantId != tenant || other == tenant || !request["targets"]!.Deserialize<ProtectionTarget[]>()!.Any(target => ErasureRootCustody.TargetId(target) == ErasureRootCustody.TargetId(root.Identity)))
                { throw new InvalidOperationException("Exact original same-alias tenant-isolation fixture required."); }
                var foreign = await custody.WrapAsync(root.Identity with { TenantId = other }, request["operationId"]!.GetValue<string>(), 0, token).ConfigureAwait(false);
                result = new { status = foreign is null ? "Denied" : "UnexpectedAccepted" }; break;
            case ProtectionQualificationNativeCallKind.Block: result = await consumption.BlockAsync(Read<DeletionBatchBlockRequest>()).WaitAsync(token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Activate: result = await protection.ActivateAsync(Read<DeletionReattestationActivation>(), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.ReconcileBlockedReplacement:
                var reconciliation = Read<DeletionBlockedReplacementReconciliation>();
                if (request["caseId"]!.GetValue<string>() == "P30")
                {
                    if (reconciliation.UnissuedProof is not null) { throw new InvalidOperationException("Rejected issue evidence must come from the actual private guard port."); }
                    var noIssue = await guard.ReadRejectedIssueAsync(reconciliation.Capability, reconciliation.SigningRequestId,
                        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(reconciliation.DetachedJws))), token).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("Exact independently retained rejected-issue original unavailable.");
                    reconciliation = reconciliation with { UnissuedProof = noIssue, CommittedIssuedGuardRevision = 0, GuardReplacementReceiptId = noIssue.Refusal.ReceiptId };
                }
                result = await protection.ReconcileBlockedReplacementAsync(reconciliation, token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Sign: result = await signer.SignAsync(Read<DeletionBatchCapabilityV1>()).WaitAsync(token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.ObsoleteUnissued: result = await signer.ObsoleteUnissuedAsync(Read<DeletionBatchCapabilityV1>()).WaitAsync(token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.Compromise:
                var envelope = step.Payload["envelope"]!.Deserialize<DeletionCapabilityRevocationEnvelope>()!;
                if (!await compromise.ReceiveAsync(envelope, step.Payload["signedEvidence"]!.GetValue<string>(), token).ConfigureAwait(false))
                { throw new InvalidOperationException("Exact original compromise or guard mirror remains unresolved."); }
                result = new { status = "Completed", receipt = await consumption.LookupRevocationAsync(envelope).WaitAsync(token).ConfigureAwait(false) }; break;
            case ProtectionQualificationNativeCallKind.Lookup:
                result = await protection.LookupAsync(step.Payload["tenantId"]!.GetValue<string>(), step.Payload["batchId"]!.GetValue<string>(), token).ConfigureAwait(false); break;
            case ProtectionQualificationNativeCallKind.LookupBlock:
                result = (await consumption.ReadBlockOriginalAsync(Read<DeletionBatchBlockRequest>()).WaitAsync(token).ConfigureAwait(false))?.Outcome
                    ?? throw new InvalidOperationException("Exact production block original unavailable."); break;
            case ProtectionQualificationNativeCallKind.LookupActivation:
                result = (await consumption.ReadActivationOriginalAsync(Read<DeletionReattestationActivation>()).WaitAsync(token).ConfigureAwait(false))?.Outcome
                    ?? throw new InvalidOperationException("Exact production activation original unavailable."); break;
            case ProtectionQualificationNativeCallKind.LookupProvider:
                using (var originalCall = new ErasureRootQualificationCallScope(ErasureRootCustody.Hash(step)))
                { result = await custody.LookupAsync(step.Payload["request"]!.Deserialize<DeletionBatchConsumptionRequest>()!, step.Payload["reservationReceiptId"]!.GetValue<string>(), token).ConfigureAwait(false); }
                break;
            case ProtectionQualificationNativeCallKind.Backup:
                var backup = Read<ProtectionQualificationNativeCell>(); Check(backup);
                var actual = await native.GetStateAsync<JsonElement>(backup.Component, backup.Key, ConsistencyMode.Strong, backup.Metadata, token).ConfigureAwait(false);
                if (actual.ValueKind == JsonValueKind.Undefined) { throw new InvalidOperationException("Missing native backup source."); }
                copyValue = actual; copyKey = backup.Key + ":backup";
                result = new { receiptId = CopyReceipt(step.Kind, backup, actual) }; break;
            case ProtectionQualificationNativeCallKind.Restore:
                var restore = Read<ProtectionQualificationNativeCell>(); Check(restore);
                var saved = await native.GetStateAsync<JsonElement>(restore.Component, restore.Key + ":backup", ConsistencyMode.Strong, restore.Metadata, token).ConfigureAwait(false);
                if (saved.ValueKind == JsonValueKind.Undefined) { throw new InvalidOperationException("Missing exact original backup."); }
                copyValue = saved; copyKey = restore.AnchorScope is null ? restore.Key + ":restored" : restore.Key;
                result = new { receiptId = CopyReceipt(step.Kind, restore, saved) }; break;
            case ProtectionQualificationNativeCallKind.Fault:
                await faults.ArmCallAsync(step, request["operationId"]!.GetValue<string>(), token).ConfigureAwait(false);
                result = new { originalStepId = step.Payload["stepId"]!.GetValue<string>(), armed = true }; break;
            case ProtectionQualificationNativeCallKind.SealSnapshot:
                if (step.Payload.ContainsKey("Entries"))
                { result = await SealMixedSnapshotAsync(step, Read<ProtectionQualificationNativeSnapshot>(), request, token).ConfigureAwait(false); break; }
                var snapshot = ReadSeal(step.Payload); Check(snapshot.Source);
                string snapshotCanary = request["canaryBase64"]?.GetValue<string>() ?? await ReadSeedAsync(snapshot.SeedSource, token).ConfigureAwait(false);
                if (!await keys.PreflightEnrollmentAsync(snapshot.Enrollment, token).ConfigureAwait(false) || !await source.RegisterOriginalAsync(snapshot.Enrollment, token).ConfigureAwait(false)) { throw new InvalidOperationException("Independent snapshot original unavailable."); }
                var state = JsonSerializer.SerializeToElement(new { ProtectedContent = new { Target = snapshot.Enrollment.Binding.Identity.Target, Value = new { CanaryBase64 = snapshotCanary } } });
                var type = (JsonTypeInfo)new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }.GetTypeInfo(typeof(JsonElement));
                var protectedSnapshot = await engine.ProtectSnapshotAsync(snapshot.Enrollment.Binding.Identity.Owner, state, type,
                    ErasureRootOccurrenceBindings.Occurrence(snapshot.Enrollment.Binding.Identity), token).ConfigureAwait(false);
                await engine.AcquirePayloadProtectionCompletionLeaseAsync(protectedSnapshot.CompletionContext!, token).ConfigureAwait(false);
                var snapshotWriter = snapshot.Enrollment.Writer ?? throw new InvalidOperationException("Original snapshot writer unavailable.");
                if (snapshotWriter.Component != snapshot.Source.Component) { throw new InvalidOperationException("Atomic source partition required."); }
                var snapshotProof = new ErasureRootWriterProof(snapshot.Enrollment.Binding, Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(protectedSnapshot.ProtectionResult.State))),
                    true, true, "snapshot-source:" + ErasureRootCustody.Hash(snapshot.Source));
                var snapshotProducer = ProducerOriginal(step, [snapshot.Enrollment.Binding], snapshot.Source, JsonSerializer.SerializeToUtf8Bytes(protectedSnapshot.ProtectionResult.State), protectedSnapshot.CompletionContext!);
                await native.ExecuteStateTransactionAsync(snapshot.Source.Component,
                    [new(snapshot.Source.Key, JsonSerializer.SerializeToUtf8Bytes(protectedSnapshot.ProtectionResult), StateOperationType.Upsert),
                     new(snapshot.Source.Key + ":cache", JsonSerializer.SerializeToUtf8Bytes(protectedSnapshot.ProtectionResult), StateOperationType.Upsert),
                     new(snapshotWriter.Key, JsonSerializer.SerializeToUtf8Bytes(snapshotProof), StateOperationType.Upsert),
                     new(snapshot.Source.Key + ":producer", JsonSerializer.SerializeToUtf8Bytes(snapshotProducer), StateOperationType.Upsert),
                     new(snapshot.Source.CompletionKey ?? snapshot.Source.Key + ":completion", JsonSerializer.SerializeToUtf8Bytes(protectedSnapshot.CompletionContext), StateOperationType.Upsert)], snapshot.Source.Metadata, token).ConfigureAwait(false);
                await engine.CompletePayloadProtectionAsync(protectedSnapshot.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token).ConfigureAwait(false);
                await RetainProducerOriginalAsync(snapshotProducer, token).ConfigureAwait(false);
                result = protectedSnapshot; break;
            default: throw new InvalidOperationException("Unsupported native operation.");
        }
        await keys.CompleteDisposedUsesAsync(token).ConfigureAwait(false);
        if (originalCopyCell is not null)
        {
            var sourceValue = await native.GetStateAsync<JsonElement>(originalCopyCell.Component, originalCopyCell.Key + (step.Kind == ProtectionQualificationNativeCallKind.Restore ? ":backup" : ""), ConsistencyMode.Strong, originalCopyCell.Metadata, token).ConfigureAwait(false);
            if (sourceValue.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null || ErasureRootCustody.Hash(sourceValue) != step.SourceDigest)
            { throw new InvalidOperationException("Admitted native source fingerprint changed."); }
            JsonNode actualResult = Result(result) ?? throw new InvalidOperationException("Exact native result required.");
            var original = new ProtectionQualificationCopyOriginal(ErasureRootCustody.Hash(step), ProtectionQualificationNativeCell.LocationId(originalCopyCell), step.SourceDigest!, actualResult);
            await native.ExecuteStateTransactionAsync(originalCopyCell.Component,
                [new(copyKey ?? originalCopyCell.Key + ":replay", JsonSerializer.SerializeToUtf8Bytes(copyValue ?? JsonSerializer.SerializeToElement(result)), StateOperationType.Upsert),
                 new(OriginalKey(originalCopyCell, step), JsonSerializer.SerializeToUtf8Bytes(original), StateOperationType.Upsert)], originalCopyCell.Metadata, token).ConfigureAwait(false);
        }
        bool negative = request["action"]!.GetValue<string>().StartsWith("Reject", StringComparison.Ordinal);
        if (!negative && result is DeletionConsumptionOutcome { Status: DeletionConsumptionStatus.Unavailable })
        { throw new InvalidOperationException("Original native owner result unresolved; read-only original recovery required."); }
        var captured = Result(result);
        if (request["action"]!.GetValue<string>() == "RejectStaleRevokedDispatch" && result is DeletionConsumptionOutcome { Status: DeletionConsumptionStatus.ConsumptionBlocked } && captured is JsonObject stale)
        { stale["status"] = "Denied"; }
        if (negative && step.Kind == ProtectionQualificationNativeCallKind.Reserve && result is DeletionConsumptionOutcome { Status: DeletionConsumptionStatus.Unavailable or DeletionConsumptionStatus.ConsumptionReserved }
            && captured is JsonObject refusedManifest)
        {
            var proof = await custody.CapturePinnedManifestRefusalAsync(Read<DeletionBatchConsumptionRequest>(), request["operationId"]!.GetValue<string>() + ":" + step.Id, token).ConfigureAwait(false);
            if (proof is not null) { refusedManifest["status"] = "HoldPinned"; refusedManifest["pinRefusals"] = JsonSerializer.SerializeToNode(proof); }
            else if (await IsHoldPreventedAsync(Read<DeletionBatchConsumptionRequest>(), token).ConfigureAwait(false)) { refusedManifest["status"] = "HoldBlocked"; }
        }
        if (negative && result is DeletionConsumptionOutcome { Status: DeletionConsumptionStatus.ConsumptionReserved } && captured is JsonObject pendingResult && pendingResult["status"]?.GetValue<string>() != "HoldPinned")
        { throw new InvalidOperationException("Admitted native work remains Unknown; no zero-effect refusal can be certified."); }
        if (effectBefore is not null && captured is JsonObject audit)
        { audit["effectBefore"] = effectBefore; audit["effectAfter"] = await EffectAsync(tenant, step, "after", token).ConfigureAwait(false); }
        if (step.HoldBlockedOriginal is not null && captured is JsonObject refused)
        { await RetainConditionalHoldOriginalAsync(tenant, step, refused, token).ConfigureAwait(false); }
        if (RetainsExactResult(step.Kind) && captured is JsonObject exactResult)
        { await custody.RetainNativeCallResultAsync(tenant, step, exactResult, token).ConfigureAwait(false); }
        await faults.CheckAsync(step.Id, "after", token).ConfigureAwait(false);
        return captured;
    }
    private async Task RetainConditionalHoldOriginalAsync(string tenant, ProtectionQualificationNativeCall call, JsonObject result, CancellationToken token)
    {
        string id = ErasureRootCustody.Hash(call);
        for (int attempt = 0; attempt < 16; attempt++)
        {
            if (!await authorization.AuthorizeAsync(tenant, call.Id, "RetainConditionalHoldOriginal", id, token).ConfigureAwait(false)) { throw new InvalidOperationException("Current conditional refusal retention permission unavailable."); }
            var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
            if (read is not { } head || head.Head.Pending is not null) { throw new InvalidOperationException("Exact conditional refusal original unavailable."); }
            if (await custody.ReadConditionalHoldOriginalAsync(tenant, head.Head, id, token).ConfigureAwait(false) is { } prior)
            { if (!JsonNode.DeepEquals(prior, result)) { throw new InvalidOperationException("Changed conditional hold original."); } return; }
            head.Head.ConditionalHoldOriginals.Add(id, (JsonObject)result.DeepClone());
            if (await custody.SaveAsync(tenant, head.Version, head.Head, token).ConfigureAwait(false)) { return; }
        }
        throw new InvalidOperationException("Conditional refusal original retention unresolved.");
    }
    private static ProtectionQualificationNativeSeal ReadSeal(JsonObject payload)
        => new((payload["Enrollment"] ?? payload["enrollment"])!.Deserialize<ErasureRootOccurrenceEnrollment>()!, (payload["Source"] ?? payload["source"])!.Deserialize<ProtectionQualificationNativeCell>()!)
        { SeedSource = (payload["SeedSource"] ?? payload["seedSource"])?.Deserialize<ProtectionQualificationNativeCell>(), ChangePayload = (payload["ChangePayload"] ?? payload["changePayload"])?.GetValue<bool>() ?? false };
    private static ProtectionQualificationNativeCell? CopyCell(ProtectionQualificationNativeCall step)
        => step.Kind is ProtectionQualificationNativeCallKind.ReplayEvent or ProtectionQualificationNativeCallKind.ReplayCopy or ProtectionQualificationNativeCallKind.Backup or ProtectionQualificationNativeCallKind.Restore
            ? step.Payload.Deserialize<ProtectionQualificationNativeCell>()
            : step.Kind == ProtectionQualificationNativeCallKind.ReplaySnapshot ? step.Payload.Deserialize<ProtectionQualificationNativeSnapshotRead>()!.Source : null;
    private static string OriginalKey(ProtectionQualificationNativeCell cell, ProtectionQualificationNativeCall call) => cell.Key + ":native-original:" + ErasureRootCustody.Hash(call);
    private static JsonObject HoldBlockedResult(ProtectionQualificationNativeCall step)
    {
        var request = step.Payload.Deserialize<DeletionBatchConsumptionRequest>()!;
        var current = step.HoldBlockedOriginal ?? throw new InvalidOperationException("Exact held original required.");
        var deletion = current.Deletions.SingleOrDefault(value => value.RequestId == request.Capability.DeletionRequestId);
        var batch = deletion?.Batches.SingleOrDefault(value => value.BatchId == request.Capability.BatchId);
        if (current.TenantId != request.Capability.TenantId || deletion is null || batch?.Capability != request.Capability || batch.DetachedJws != request.DetachedJws
            || batch.DispatchReceiptId.Length != 0 || !current.Holds.Any(hold => hold.Active && ErasureRootCustody.Hash(hold.Scope) == ErasureRootCustody.Hash(deletion.Scope)))
        { throw new InvalidOperationException("Substituted hold refusal original."); }
        return new JsonObject { ["status"] = "HoldBlocked", ["receiptId"] = "conditional-hold:" + ErasureRootCustody.Hash(new { step.Id, step.OperationId, step.RequestSha256, request, current }) };
    }
    private async Task<JsonNode?> ReplayEventValueAsync(ProtectionQualificationNativeCall step, JsonObject request, CancellationToken token)
    {
                var replayCell = step.Payload.Deserialize<ProtectionQualificationNativeCell>()!; var replaying = await ReadEventAsync(replayCell, token).ConfigureAwait(false);
                var replay = await engine.TryReplayEventPayloadAsync(replaying.Binding.Identity.Owner, replaying.Binding.Identity.PayloadTypeId, replaying.Protected.PayloadBytes,
                    replaying.Protected.SerializationFormat, replaying.Protected.Metadata, ErasureRootOccurrenceBindings.Occurrence(replaying.Binding.Identity), token).ConfigureAwait(false);
                try
                {
                    JsonElement? erased = null;
                    if (replay.HasErasedContent && replay.Outcome.PayloadBytes is { } replayBytes)
                    {
                        using var replayJson = JsonDocument.Parse(replayBytes);
                        var field = replayJson.RootElement.GetProperty("ProtectedContent").GetProperty("Value");
                        if (field.GetProperty("State").GetString() != "Erased") { throw new InvalidOperationException("Actual typed erased replay required."); }
                        erased = field.Clone();
                    }
                    var external = await engine.TryUnprotectEventPayloadAsync(replaying.Binding.Identity.Owner, replaying.Binding.Identity.PayloadTypeId, replaying.Protected.PayloadBytes,
                        replaying.Protected.SerializationFormat, replaying.Protected.Metadata, ErasureRootOccurrenceBindings.Occurrence(replaying.Binding.Identity), token).ConfigureAwait(false);
                    try
                    {
                        var projection = new { target = replaying.Binding.Identity.Target, field = erased, externalOutcome = external.IsReadable ? "Readable" : "Unreadable", sourceIdentity = ProtectionQualificationNativeCell.LocationId(replayCell), sourceDigest = ErasureRootCustody.Hash(replaying) };
                        return JsonSerializer.SerializeToNode(projection);
                    }
                    finally { if (external.PayloadBytes is { } openedBytes) { CryptographicOperations.ZeroMemory(openedBytes); } }
                }
                finally { if (replay.Outcome.PayloadBytes is { } replayBytes) { CryptographicOperations.ZeroMemory(replayBytes); } }
    }
    private static JsonNode? Result(object? result)
    {
        var node = JsonSerializer.SerializeToNode(result);
        if (node is JsonObject outcome)
        {
            string? status = result switch
            {
                DeletionConsumptionOutcome { Status: DeletionConsumptionStatus.Unavailable } => "ProviderUnavailable",
                DeletionConsumptionOutcome actual => actual.Status.ToString(),
                CustodyKeyLifecycleOutcome { Status: CustodyKeyLifecycleStatus.BlockedByPin } => "HoldPinned",
                CustodyKeyLifecycleOutcome actual => actual.Status.ToString(),
                DeletionCapabilitySigningOutcome actual => actual.State.ToString(),
                _ => null,
            };
            if (status is not null) { outcome["status"] = status; }
        }
        return node;
    }
    private static bool RetainsExactResult(ProtectionQualificationNativeCallKind kind)
        => kind is ProtectionQualificationNativeCallKind.Lookup or ProtectionQualificationNativeCallKind.LookupProvider or ProtectionQualificationNativeCallKind.Fault
            or ProtectionQualificationNativeCallKind.AuthorizeLifecycle or ProtectionQualificationNativeCallKind.ContinueHoldAfterDispatch;
    private async Task<string> EffectAsync(string tenant, ProtectionQualificationNativeCall call, string boundary, CancellationToken token)
    {
        var evidence = await authorization.ReadEffectBoundaryAsync(tenant, ErasureRootCustody.Hash(call), boundary, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Independent current native effect original unavailable.");
        if (evidence.TenantId != tenant || evidence.StoreId != custody.Installation.StoreId || evidence.EnrollmentId != custody.Installation.EnrollmentId
            || evidence.CallDigest != ErasureRootCustody.Hash(call) || evidence.Boundary != boundary
            || evidence.Fingerprint != ErasureRootCustody.Hash(new { evidence.CustodyStateSha256, evidence.NativePolicy }))
        { throw new InvalidOperationException("Exact independent effect boundary required."); }
        return evidence.Fingerprint;
    }
    private async Task<JsonObject> MakeOwnersEffectiveAsync(JsonObject input, CancellationToken token)
    {
        var template = input["transition"]!.Deserialize<GovernanceGuardTransition>()!;
        if (template.Operation != GovernanceGuardOperation.RecordOwnerCycleEffective) { throw new InvalidOperationException("Source-owned owner-cycle operation required."); }
        var state = await guard.ReadAsync(template.TenantId, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Current native owner census unavailable.");
        var deletion = state.Deletions.Single(value => value.RequestId == template.DeletionRequestId);
        var owners = input["ownerIds"]!.Deserialize<string[]>()!;
        if (deletion.Ordinal != input["expectedOrdinal"]!.GetValue<long>() || GovernanceScopeGuardReducer.ObligationDigest(deletion.ObligationIds) != input["obligationDigest"]!.GetValue<string>())
        { throw new InvalidOperationException("Admitted native owner ordinal or obligations changed."); }
        if (owners.Length is < 1 or > 1000 || owners.Distinct(StringComparer.Ordinal).Count() != owners.Length) { throw new InvalidOperationException("Complete finite native owner census required."); }
        var outcomes = new JsonArray();
        for (int index = 0; index < owners.Length; index++)
        {
            string owner = owners[index];
            state = await guard.ReadAsync(template.TenantId, token).ConfigureAwait(false) ?? throw new InvalidOperationException("Native owner-cycle compare unavailable.");
            var current = state.Deletions.Single(value => value.RequestId == template.DeletionRequestId);
            if (!current.RequiredOwnerIds.Order(StringComparer.Ordinal).SequenceEqual(owners) || current.Ordinal != deletion.Ordinal) { throw new InvalidOperationException("Captured native owner census changed."); }
            var command = OwnerCycleCommand(input, owner, index);
            var actual = await guard.ExecuteAsync(command, [], token).ConfigureAwait(false);
            if (actual?.Status != "Committed") { throw new InvalidOperationException("Actual owner Effective evidence unavailable."); }
            outcomes.Add(JsonSerializer.SerializeToNode(actual));
        }
        return new JsonObject { ["status"] = "Completed", ["outcomes"] = outcomes };
    }
    private static GovernanceGuardTransition OwnerCycleCommand(JsonObject input, string owner, int index)
    {
        var template = input["transition"]!.Deserialize<GovernanceGuardTransition>()!;
        var ordinal = new GovernanceOrdinalCommand(input["expectedOrdinal"]!.GetValue<long>(), "", "", owner, input["obligationDigest"]!.GetValue<string>(), "", "", 0, 0, "", [], "");
        return template with { OperationId = template.OperationId + ":" + ErasureRootCustody.Hash(owner), ExpectedGuardRevision = checked(template.ExpectedGuardRevision + index), Ordinal = ordinal };
    }
    private async Task<bool> IsHoldPreventedAsync(DeletionBatchConsumptionRequest request, CancellationToken token)
    {
        var current = await guard.ReadAsync(request.Capability.TenantId, token).ConfigureAwait(false);
        if (current is null) { return false; }
        var deletion = current.Deletions.SingleOrDefault(value => value.RequestId == request.Capability.DeletionRequestId);
        var batch = deletion?.Batches.SingleOrDefault(value => value.BatchId == request.Capability.BatchId);
        return deletion is not null && batch?.Capability == request.Capability && batch.DetachedJws == request.DetachedJws && batch.DispatchReceiptId.Length == 0
            && current.Holds.Any(hold => hold.Active && ErasureRootCustody.Hash(hold.Scope) == ErasureRootCustody.Hash(deletion.Scope));
    }

    private async Task<PayloadProtectionWriteResult> SealAsync(ProtectionQualificationNativeCall call, ProtectionQualificationNativeSeal input, JsonObject request, string? changedCanary, CancellationToken token)
    {
        Check(input.Source);
        var binding = input.Enrollment.Binding;
        byte[] canary = Convert.FromBase64String(changedCanary ?? request["canaryBase64"]?.GetValue<string>() ?? await ReadSeedAsync(input.SeedSource, token).ConfigureAwait(false));
        byte[]? payload = null;
        try
        {
        if (canary.Length == 0) { throw new InvalidOperationException("Exact nonempty canary required."); }
        if (!await keys.PreflightEnrollmentAsync(input.Enrollment, token).ConfigureAwait(false)) { throw new InvalidOperationException("Native digest/current source inputs unavailable."); }
        if (input.Enrollment.Writer is not { } writer || writer.Component != input.Source.Component
            || !await source.RegisterOriginalAsync(input.Enrollment, token).ConfigureAwait(false)) { throw new InvalidOperationException("Independent native source original unavailable."); }
            payload = JsonSerializer.SerializeToUtf8Bytes(new { ProtectedContent = new { Target = binding.Identity.Target, Value = new { CanaryBase64 = Convert.ToBase64String(canary) } } });
            var sealedResult = await engine.ProtectEventPayloadAsync(binding.Identity.Owner, new ProtectionQualificationEventPayload(), binding.Identity.PayloadTypeId, payload, "json",
                ErasureRootOccurrenceBindings.Occurrence(binding.Identity), token).ConfigureAwait(false);
            await engine.AcquirePayloadProtectionCompletionLeaseAsync(sealedResult.CompletionContext!, token).ConfigureAwait(false);
            var sourceEvent = new ProtectionQualificationPersistedEvent(binding, sealedResult.ProtectionResult);
            var proof = new ErasureRootWriterProof(binding, Convert.ToHexString(SHA256.HashData(sealedResult.ProtectionResult.PayloadBytes)), true, true,
                "source:" + ErasureRootCustody.Hash(new { binding, input.Source }));
            var producerOriginal = ProducerOriginal(call, [binding], input.Source, sealedResult.ProtectionResult.PayloadBytes, sealedResult.CompletionContext!);
            await native.ExecuteStateTransactionAsync(input.Source.Component,
                [new(input.Source.Key, JsonSerializer.SerializeToUtf8Bytes(sourceEvent), StateOperationType.Upsert), new(writer.Key, JsonSerializer.SerializeToUtf8Bytes(proof), StateOperationType.Upsert),
                 new(input.Source.CompletionKey ?? input.Source.Key + ":completion", JsonSerializer.SerializeToUtf8Bytes(sealedResult.CompletionContext), StateOperationType.Upsert),
                 new(input.Source.Key + ":producer", JsonSerializer.SerializeToUtf8Bytes(producerOriginal), StateOperationType.Upsert)],
                input.Source.Metadata, token).ConfigureAwait(false);
            await engine.CompletePayloadProtectionAsync(sealedResult.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token).ConfigureAwait(false);
            await RetainSeedOriginalAsync(binding, input.Source, sealedResult.ProtectionResult.PayloadBytes, token).ConfigureAwait(false);
            await RetainProducerOriginalAsync(producerOriginal, token).ConfigureAwait(false);
            return sealedResult;
        }
        finally { CryptographicOperations.ZeroMemory(canary); if (payload is not null) { CryptographicOperations.ZeroMemory(payload); } }
    }
    private (JsonElement State, ErasureRootOccurrenceEnrollment[] Originals) DescribeMixedSnapshot(ProtectionQualificationNativeSnapshot input, ProtectionTarget[] targets, string canary, CancellationToken token)
    {
        Check(input.Source);
        if (string.IsNullOrWhiteSpace(input.Source.CompletionKey) || targets.Length is < 2 or > 1000 || input.Parent.PayloadKind != PayloadProtectionPayloadKind.Snapshot)
        { throw new InvalidOperationException("Exact native mixed snapshot source and completion journal required."); }
        var state = JsonSerializer.SerializeToElement(new { Entries = targets.Select(target => new { ProtectedContent = new { Target = target,
            Value = new { CanaryBase64 = canary } } }).ToArray() });
        var layout = InteractionSnapshotEntries.Describe(JsonSerializer.SerializeToUtf8Bytes(state), targets[0].TenantId, token);
        if (input.Entries.Length != layout.Entries.Count) { throw new InvalidOperationException("Complete mixed-root native source manifest required."); }
        var originals = new ErasureRootOccurrenceEnrollment[input.Entries.Length];
        for (int index = 0; index < input.Entries.Length; index++)
        {
            var original = input.Entries[index]; var described = layout.Entries[index];
            if (original.Writer is not { } writer || writer.Component != input.Source.Component || original.Binding.Identity.Target != described.Target
                || original.Binding.Identity.Owner != input.Entries[0].Binding.Identity.Owner
                || ErasureRootCustody.Hash(writer.Metadata) != ErasureRootCustody.Hash(input.Source.Metadata)
                || ErasureRootOccurrenceBindings.Occurrence(original.Binding.Identity) != InteractionSnapshotEntries.EntryOccurrence(input.Parent, described.Path))
            { throw new InvalidOperationException("Exact entry owner, target, source partition and sub-occurrence required."); }
            originals[index] = original with { SnapshotEntry = new(input.Parent, described.Path, layout.ContainerDigest, layout.Entries.Count) };
        }
        return (state, originals);
    }
    private async Task<SnapshotProtectionWriteResult> SealMixedSnapshotAsync(ProtectionQualificationNativeCall call, ProtectionQualificationNativeSnapshot input, JsonObject request, CancellationToken token)
    {
        string canary = request["canaryBase64"]?.GetValue<string>() ?? await ReadSeedAsync(input.SeedSource, token).ConfigureAwait(false);
        var (state, originals) = DescribeMixedSnapshot(input, request["targets"]!.Deserialize<ProtectionTarget[]>()!, canary, token);
        foreach (var original in originals)
        { if (!await keys.PreflightEnrollmentAsync(original, token).ConfigureAwait(false)) { throw new InvalidOperationException("Complete mixed-entry native inputs required."); } }
        foreach (var original in originals)
        { if (!await source.RegisterOriginalAsync(original, token).ConfigureAwait(false)) { throw new InvalidOperationException("Independent mixed snapshot original unavailable."); } }
        var type = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }.GetTypeInfo(typeof(JsonElement));
        var write = await engine.ProtectSnapshotAsync(originals[0].Binding.Identity.Owner, state, type, input.Parent, token).ConfigureAwait(false);
        await engine.AcquirePayloadProtectionCompletionLeaseAsync(write.CompletionContext!, token).ConfigureAwait(false);
        var carrier = write.ProtectionResult.State as ProtectedSnapshotEntriesV2 ?? throw new InvalidOperationException("Actual mixed-root snapshot carrier required.");
        var producerOriginal = ProducerOriginal(call, originals.Select(value => value.Binding).ToArray(), input.Source, JsonSerializer.SerializeToUtf8Bytes(write.ProtectionResult.State), write.CompletionContext!);
        var operations = new List<StateTransactionRequest>
        {
            new(input.Source.Key, JsonSerializer.SerializeToUtf8Bytes(write.ProtectionResult), StateOperationType.Upsert),
            new(input.Source.Key + ":cache", JsonSerializer.SerializeToUtf8Bytes(write.ProtectionResult), StateOperationType.Upsert),
            new(input.Source.Key + ":producer", JsonSerializer.SerializeToUtf8Bytes(producerOriginal), StateOperationType.Upsert),
            new(input.Source.CompletionKey!, JsonSerializer.SerializeToUtf8Bytes(write.CompletionContext), StateOperationType.Upsert),
        };
        for (int index = 0; index < originals.Length; index++)
        {
            byte[] sealedBytes = JsonSerializer.SerializeToUtf8Bytes(carrier.Entries[index].Protected);
            var proof = new ErasureRootWriterProof(originals[index].Binding, Convert.ToHexString(SHA256.HashData(sealedBytes)), true, true,
                "mixed-snapshot-source:" + ErasureRootCustody.Hash(new { input.Source, path = carrier.Entries[index].Path }));
            operations.Add(new(originals[index].Writer!.Key, JsonSerializer.SerializeToUtf8Bytes(proof), StateOperationType.Upsert));
        }
        await native.ExecuteStateTransactionAsync(input.Source.Component, operations, input.Source.Metadata, token).ConfigureAwait(false);
        await engine.CompletePayloadProtectionAsync(write.CompletionContext!, PayloadProtectionPersistenceOutcome.Persisted, token).ConfigureAwait(false);
        await RetainProducerOriginalAsync(producerOriginal, token).ConfigureAwait(false);
        return write;
    }
    internal static string CopyReceipt(ProtectionQualificationNativeCallKind kind, ProtectionQualificationNativeCell cell, JsonElement saved)
        => kind == ProtectionQualificationNativeCallKind.Backup ? ErasureRootCustody.Hash(new { backup = cell, actual = saved })
            : ErasureRootCustody.Hash(new { restore = cell, saved });
    private async Task<JsonArray> ReplaySnapshotAsync(ProtectionQualificationNativeSnapshotRead input, CancellationToken token)
    {
        Check(input.Source);
        var snapshot = await native.GetStateAsync<SnapshotProtectionResult>(input.Source.Component, input.Source.Key, ConsistencyMode.Strong, input.Source.Metadata, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Exact original sealed snapshot unavailable.");
        var type = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }.GetTypeInfo(typeof(JsonElement));
        var trusted = await engine.TryReplaySnapshotAsync(input.Owner, snapshot.State, snapshot.Metadata, type, input.Parent, token).ConfigureAwait(false);
        var external = await engine.TryUnprotectSnapshotAsync(input.Owner, snapshot.State, snapshot.Metadata, type, input.Parent, token).ConfigureAwait(false);
        if (!trusted.IsReadable || trusted.State is not JsonElement restored) { throw new InvalidOperationException("Actual trusted snapshot replay unavailable."); }
        var projected = new JsonArray();
        void Visit(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                if (value.TryGetProperty("ProtectedContent", out var content) && content.ValueKind == JsonValueKind.Object
                    && content.TryGetProperty("Target", out var target) && content.TryGetProperty("Value", out var field))
                {
                    JsonNode? erased = field.ValueKind == JsonValueKind.Object && field.EnumerateObject().Count() == 1
                        && field.TryGetProperty("State", out var state) && state.GetString() == "Erased" ? JsonNode.Parse(field.GetRawText()) : null;
                    projected.Add(new JsonObject { ["target"] = JsonNode.Parse(target.GetRawText()), ["field"] = erased, ["externalOutcome"] = external.IsReadable ? "Readable" : "Unreadable" });
                    return;
                }
                foreach (var property in value.EnumerateObject()) { Visit(property.Value); }
            }
            else if (value.ValueKind == JsonValueKind.Array) { foreach (var child in value.EnumerateArray()) { Visit(child); } }
        }
        Visit(restored);
        if (projected.Count == 0 && restored.ValueKind == JsonValueKind.Object && restored.EnumerateObject().Count() == 1
            && restored.TryGetProperty("State", out var state) && state.GetString() == "Erased")
        {
            var binding = await source.ResolveAsync(input.Owner, input.Parent, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Exact opaque snapshot source original required.");
            projected.Add(new JsonObject { ["target"] = JsonSerializer.SerializeToNode(binding.Binding.Identity.Target),
                ["field"] = JsonNode.Parse(restored.GetRawText()), ["externalOutcome"] = external.IsReadable ? "Readable" : "Unreadable" });
        }
        if (projected.Count == 0) { throw new InvalidOperationException("Actual typed per-entry snapshot replay required."); }
        foreach (var item in projected.OfType<JsonObject>()) { item["sourceIdentity"] = ProtectionQualificationNativeCell.LocationId(input.Source); item["sourceDigest"] = ErasureRootCustody.Hash(snapshot); }
        return projected;
    }
    private async Task<PayloadProtectionCompletionContext?> RecoverProducerAsync(ProtectionQualificationNativeCall call, InteractionProtectionBinding[] bindings, ProtectionQualificationNativeCell cell, byte[] payload, bool seed, CancellationToken token)
    {
        if (call.OperationId is null || call.RequestSha256 is null) { return null; }
        Check(cell); string tenant = bindings[0].Identity.Target.TenantId;
        var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
        if (read is not { } current || await custody.ReadRetainedAsync(current.Head, "QualificationOperations", current.Head.QualificationOperations, ProtectionQualificationOperationJournal.Id("executor", call.OperationId), token).ConfigureAwait(false) is not { } operation
            || operation.RequestSha256 != call.RequestSha256 || operation.Producer is not { } originalProducer
            || !operation.Calls.TryGetValue(call.Id, out var admitted) || ErasureRootCustody.Hash(admitted.Call) != ErasureRootCustody.Hash(call)
            || !current.Head.Anchors.TryGetValue(ErasureRootOccurrenceAuthority.Scope(tenant), out var anchor)) { return null; }
        var registryOriginal = anchor.Completed.Values.SingleOrDefault(value => value.TargetRevision == anchor.Revision && value.TargetDigest == anchor.Digest);
        var registry = registryOriginal is null ? null : JsonSerializer.Deserialize<InteractionOccurrenceRegistrySnapshot>(registryOriginal.TargetBytes);
        if (registry is null) { return null; }
        var completion = await native.GetStateAsync<PayloadProtectionCompletionContext>(cell.Component, cell.CompletionKey ?? cell.Key + ":completion", ConsistencyMode.Strong, cell.Metadata, token).ConfigureAwait(false);
        if (completion is null || completion.Identity != bindings[0].Identity.Owner) { return null; }
        var producerOriginal = await native.GetStateAsync<ProtectionQualificationProducerOriginal>(cell.Component, cell.Key + ":producer", ConsistencyMode.Strong, cell.Metadata, token).ConfigureAwait(false);
        if (producerOriginal?.Call is not { } originalCall || ErasureRootCustody.Hash(originalCall) != ErasureRootCustody.Hash(call)
            || producerOriginal.SourceIdentity != ProtectionQualificationNativeCell.LocationId(cell)
            || ErasureRootCustody.Hash(producerOriginal.Bindings) != ErasureRootCustody.Hash(bindings)
            || producerOriginal.SealedPayloadSha256 != Convert.ToHexString(SHA256.HashData(payload))
            || producerOriginal.CompletionDigest != ErasureRootCustody.Hash(completion)
            || ErasureRootCustody.Hash(producerOriginal.Completion) != ErasureRootCustody.Hash(completion)
            || ErasureRootCustody.Hash(producerOriginal.Producer) != ErasureRootCustody.Hash(originalProducer)) { return null; }
        foreach (var binding in bindings)
        {
            var enrollment = await custody.ReadSourceAsync(current.Head, ErasureRootOccurrenceBindings.Id(binding.Identity.Owner, ErasureRootOccurrenceBindings.Occurrence(binding.Identity)), token).ConfigureAwait(false);
            var retained = registry.Records.SingleOrDefault(value => value.Request.Identity == binding.Identity && value.KeyReference == binding.KeyReference);
            if (enrollment?.Writer is not { } writer || retained is not { WriterState: InteractionOccurrenceWriterState.Active, Sealed: { } sealedOutput }
                || retained.WriterProofId != binding.OperationId) { return null; }
            Check(new(writer.Component, writer.Key, writer.Metadata));
            var proof = await native.GetStateAsync<ErasureRootWriterProof>(writer.Component, writer.Key, ConsistencyMode.Strong, writer.Metadata, token).ConfigureAwait(false);
            if (proof is not { Persisted: true, WriterRetired: true } || proof.Binding != binding || string.IsNullOrWhiteSpace(proof.ReceiptId)
                || proof.SealedPayloadSha256 != Convert.ToHexString(SHA256.HashData(sealedOutput.PayloadBytes))) { return null; }
            if (bindings.Length == 1 && !sealedOutput.PayloadBytes.AsSpan().SequenceEqual(payload)) { return null; }
        }
        if (bindings.Length == 1 && (completion.DekVersion != 1 || completion.SnapshotEntries is not null || completion.SnapshotContainerDigest is not null || completion.SnapshotEntryManifest is not null
            || completion.OperationId != bindings[0].OperationId || completion.KeyReference != bindings[0].KeyReference
            || completion.Occurrence != ErasureRootOccurrenceBindings.Occurrence(bindings[0].Identity) || completion.LifecycleEpoch != bindings[0].LifecycleEpoch)) { return null; }
        if (bindings.Length > 1)
        {
            var carrier = JsonSerializer.Deserialize<ProtectedSnapshotEntriesV2>(payload)!;
            if (completion.SnapshotEntries is not { } entryCompletions || entryCompletions.Length != bindings.Length || carrier.Entries.Count != bindings.Length
                || completion.SnapshotContainerDigest != Convert.ToHexString(SHA256.HashData(carrier.SkeletonBytes))) { return null; }
            var manifest = carrier.Entries.Select(entry => new SnapshotProtectionEntry(entry.Path, entry.Target)).ToArray();
            if (completion.SnapshotEntryManifest is null || !completion.SnapshotEntryManifest.SequenceEqual(manifest)) { return null; }
            for (int index = 0; index < bindings.Length; index++)
            {
                var retained = registry.Records.Single(value => value.Request.Identity == bindings[index].Identity);
                if (!retained.Sealed!.PayloadBytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(carrier.Entries[index].Protected))
                    || entryCompletions[index].KeyReference != bindings[index].KeyReference || entryCompletions[index].OperationId != bindings[index].OperationId
                    || entryCompletions[index].Identity != bindings[index].Identity.Owner || entryCompletions[index].Occurrence != ErasureRootOccurrenceBindings.Occurrence(bindings[index].Identity)
                    || entryCompletions[index].LifecycleEpoch != bindings[index].LifecycleEpoch || entryCompletions[index].DekVersion != 1) { return null; }
            }
        }
        if (seed) { await RetainSeedOriginalAsync(bindings[0], cell, payload, token).ConfigureAwait(false); }
        await RetainProducerOriginalAsync(producerOriginal, token).ConfigureAwait(false);
        return completion;
    }
    private ProtectionQualificationProducerOriginal ProducerOriginal(ProtectionQualificationNativeCall call, InteractionProtectionBinding[] bindings, ProtectionQualificationNativeCell cell, byte[] payload, PayloadProtectionCompletionContext completion)
    {
        if (string.IsNullOrWhiteSpace(call.OperationId) || string.IsNullOrWhiteSpace(call.RequestSha256)) { throw new InvalidOperationException("Exact admitted source execution identity required."); }
        string location = ProtectionQualificationNativeCell.LocationId(cell);
        var copies = new Dictionary<string, string>(StringComparer.Ordinal);
        if (call.Kind == ProtectionQualificationNativeCallKind.SealSnapshot)
        {
            copies.Add(location, "snapshot"); copies.Add(ProtectionQualificationNativeCell.LocationId(cell with { Key = cell.Key + ":cache" }), "cache");
        }
        else { copies.Add(location, bindings[0].Identity.Owner.AggregateId == bindings[0].Identity.Target.AgentInteractionId ? "target-event" : "source-outbox"); }
        return new(location, bindings, Convert.ToHexString(SHA256.HashData(payload)), ErasureRootCustody.Hash(completion), completion, Producer) { Call = call, CopyPurposes = copies };
    }
    private async Task RetainProducerOriginalAsync(ProtectionQualificationProducerOriginal proof, CancellationToken token)
    {
        var bindings = proof.Bindings;
        string tenant = bindings[0].Identity.Target.TenantId, id = proof.SourceIdentity;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            if (!await authorization.AuthorizeAsync(tenant, bindings[0].OperationId, "RetainQualificationProducerOriginal", ErasureRootCustody.Hash(proof), token).ConfigureAwait(false))
            { throw new InvalidOperationException("Authenticated actual producer retention unavailable."); }
            var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
            if (read is not { } current || current.Head.Pending is not null) { throw new InvalidOperationException("Actual producer retention unavailable."); }
            if (await custody.ReadRetainedAsync(current.Head, "QualificationProducerOriginals", current.Head.QualificationProducerOriginals, id, token).ConfigureAwait(false) is { } prior)
            { if (ErasureRootCustody.Hash(prior) != ErasureRootCustody.Hash(proof)) { throw new InvalidOperationException("Changed actual producer original."); } return; }
            current.Head.QualificationProducerOriginals.Add(id, proof);
            if (await custody.SaveAsync(tenant, current.Version, current.Head, token).ConfigureAwait(false)) { return; }
        }
        throw new InvalidOperationException("Actual producer retention unresolved.");
    }
    private async Task RetainReserveRefusalAsync(ProtectionQualificationNativeCall call, DeletionBatchConsumptionRequest request, DeletionConsumptionOutcome outcome, CancellationToken token)
    {
        string tenant = request.Capability.TenantId, digest = ErasureRootCustody.Hash(call), id = "reserve:" + digest;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            if (!await authorization.AuthorizeAsync(tenant, call.Id, "RetainReserveRefusalOriginal", digest, token).ConfigureAwait(false))
            { throw new InvalidOperationException("Exact reserve refusal retention permission unavailable."); }
            var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
            if (read is not { } current || !current.Head.Anchors.TryGetValue(DeletionConsumptionActor.GetOwnerScope(tenant), out var anchor))
            { throw new InvalidOperationException("Independent current reserve refusal owner unavailable."); }
            var retained = await custody.ReadConditionalHoldOriginalAsync(tenant, current.Head, id, token).ConfigureAwait(false);
            var result = (JsonObject)JsonSerializer.SerializeToNode(outcome)!;
            if (retained is not null)
            { if (!JsonNode.DeepEquals(retained, result)) { throw new InvalidOperationException("Changed reserve refusal original."); } return; }
            var original = anchor.Completed.Values.SingleOrDefault(value => value.TargetRevision == anchor.Revision && value.TargetDigest == anchor.Digest);
            using var ledger = original is null ? null : JsonDocument.Parse(original.TargetBytes);
            if (ledger is null || !ledger.RootElement.GetProperty("Batches").EnumerateArray().Any(batch =>
                ErasureRootCustody.Hash(batch.GetProperty("Current").Deserialize<DeletionBatchConsumptionRequest>()) == ErasureRootCustody.Hash(request)
                && ErasureRootCustody.Hash(batch.GetProperty("Outcome").Deserialize<DeletionConsumptionOutcome>()) == ErasureRootCustody.Hash(outcome)))
            { throw new InvalidOperationException("Returned refusal does not match the exact current retained owner original."); }
            current.Head.ConditionalHoldOriginals.Add(id, result);
            if (await custody.SaveAsync(tenant, current.Version, current.Head, token).ConfigureAwait(false)) { return; }
        }
        throw new InvalidOperationException("Exact reserve refusal retention unresolved.");
    }
    private async Task RetainSeedOriginalAsync(InteractionProtectionBinding binding, ProtectionQualificationNativeCell cell, byte[] ciphertext, CancellationToken token)
    {
        string tenant = binding.Identity.Target.TenantId, id = ProtectionQualificationNativeCell.LocationId(cell);
        var proof = new ProtectionQualificationSeedOriginal(binding, id, Convert.ToHexString(SHA256.HashData(ciphertext)));
        for (int attempt = 0; attempt < 16; attempt++)
        {
            if (!await authorization.AuthorizeAsync(tenant, binding.OperationId, "RetainQualificationSeedOriginal", ErasureRootCustody.Hash(proof), token).ConfigureAwait(false)) { throw new InvalidOperationException("Canonical seed producer permission unavailable."); }
            var read = await custody.ReadAsync(tenant, token).ConfigureAwait(false);
            if (read is not { } current || current.Head.Pending is not null) { throw new InvalidOperationException("Canonical seed original unavailable."); }
            if (await custody.ReadRetainedAsync(current.Head, "QualificationSeedOriginals", current.Head.QualificationSeedOriginals, id, token).ConfigureAwait(false) is { } prior)
            { if (prior != proof) { throw new InvalidOperationException("Changed canonical seed original."); } return; }
            current.Head.QualificationSeedOriginals.Add(id, proof);
            if (await custody.SaveAsync(tenant, current.Version, current.Head, token).ConfigureAwait(false)) { return; }
        }
        throw new InvalidOperationException("Canonical seed original retention unresolved.");
    }
    private async Task<string> ReadSeedAsync(ProtectionQualificationNativeCell? cell, CancellationToken token)
    {
        if (cell is null) { throw new InvalidOperationException("Explicit sealed native seed required for a content-free backup request."); }
        var sourceEvent = await ReadEventAsync(cell, token).ConfigureAwait(false);
        var opened = await engine.TryUnprotectEventPayloadAsync(sourceEvent.Binding.Identity.Owner, sourceEvent.Binding.Identity.PayloadTypeId,
            sourceEvent.Protected.PayloadBytes, sourceEvent.Protected.SerializationFormat, sourceEvent.Protected.Metadata,
            ErasureRootOccurrenceBindings.Occurrence(sourceEvent.Binding.Identity), token).ConfigureAwait(false);
        if (!opened.IsReadable || opened.PayloadBytes is null) { throw new InvalidOperationException("Actual sealed seed unreadable."); }
        try { using var clear = JsonDocument.Parse(opened.PayloadBytes); return clear.RootElement.GetProperty("ProtectedContent").GetProperty("Value").GetProperty("CanaryBase64").GetString()!; }
        finally { CryptographicOperations.ZeroMemory(opened.PayloadBytes); }
    }
    private async Task<ProtectionQualificationPersistedEvent> ReadEventAsync(ProtectionQualificationNativeCell cell, CancellationToken token)
    {
        Check(cell);
        return await native.GetStateAsync<ProtectionQualificationPersistedEvent>(cell.Component, cell.Key, ConsistencyMode.Strong, cell.Metadata, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Original native source unavailable.");
    }
    private void Check(ProtectionQualificationNativeCell cell)
    {
        if (!fixtureSourceComponents.Contains(cell.Component) || string.IsNullOrWhiteSpace(cell.Key)) { throw new InvalidOperationException("Only isolated non-ERC source cells are permitted."); }
    }
}
