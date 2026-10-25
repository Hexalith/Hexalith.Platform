using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Actual native EventStore first-seen and security-event recording under exact private bound-command credentials.</summary>
public sealed class EventStorePrivateRecordingPort(IEventStoreGatewayClient gateway, IEventStoreGatewayBoundCommandProofSource scopedProofs,
    IndependentDaprAuthority originals, IReplicatedSecuritySpoolAuthority recoveryAuthority, string principal, TimeProvider clock) : IPrivateRecordingEventStore
{
    private readonly AuthoritativeEventStreamReader _reader = new(gateway, clock, requireNativeProof: true);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    /// <inheritdoc />
    public async Task<TrustedEnvelopeReplayReceipt?> LookupAsync(TrustedEnvelopeReplayIntent intent, CancellationToken cancellationToken = default)
    {
        var source = await ReadAsync(ReplayIdentity(intent), cancellationToken);
        return ReadReplay(source, intent);
    }
    /// <inheritdoc />
    public async Task<TrustedEnvelopeReplayReceipt?> RegisterAsync(TrustedEnvelopeReplayIntent intent, DateTimeOffset evaluatedAt, TimeSpan retention, CancellationToken cancellationToken = default)
    {
        if (retention != TimeSpan.FromSeconds(604800) || evaluatedAt.Offset != TimeSpan.Zero || evaluatedAt > clock.GetUtcNow()) return null;
        var identity = ReplayIdentity(intent);
        var source = await ReadAsync(identity, cancellationToken);
        if (source is null) return null;
        if (source.Head > 0) return ReadReplay(source, intent);
        string id = StableUlid("replay", identity.ActorId);
        var retained = await FindOriginalAsync("private-replay", id, cancellationToken);
        var command = retained?.Request.Deserialize<ReplayCommand>(Json) ?? new ReplayCommand(intent.Issuer, intent.DeliveryNonce, intent.TargetTenantId, intent.LogicalCommandId,
            intent.SigningKeyVersion, intent.CanonicalAndTagDigest, evaluatedAt, evaluatedAt.Add(retention));
        if (command.Issuer != intent.Issuer || command.DeliveryNonce != intent.DeliveryNonce || command.TargetTenantId != intent.TargetTenantId
            || command.LogicalCommandId != intent.LogicalCommandId || command.SigningKeyVersion != intent.SigningKeyVersion
            || command.CanonicalAndTagDigest != intent.CanonicalAndTagDigest || command.RetainUntil - command.FirstSeenAt != retention) return null;
        if (retained is null)
        {
            await BeginAsync("private-replay", id, command, cancellationToken);
            retained = await FindOriginalAsync("private-replay", id, cancellationToken);
        }
        if (retained?.SubmissionState != "Unsent") return await LookupAsync(intent, cancellationToken);
        await SubmitAsync(identity, "Hexalith.Agents.Contracts.TrustedEnvelopeReplay.RegisterTrustedEnvelopeFirstSeen", id, command, cancellationToken);
        return await LookupAsync(intent, cancellationToken);
    }
    /// <inheritdoc />
    public async Task<SecurityEventRecorderLookup> LookupAsync(SecurityObservationRecord record, CancellationToken cancellationToken = default)
    {
        _=SecurityCommand(record);
        var source = await ReadAsync(SecurityIdentity(record), cancellationToken);
        if (source is null) return new(SecurityEventRecorderLookupState.Unknown);
        foreach (var item in source.Events)
        {
            if (!EventType(item.EventTypeName, "SecurityEventLog", "SecurityEventRecorded")) return new(SecurityEventRecorderLookupState.Unknown);
            var value = Decode<SecurityEvent>(item.Payload)?.Observation;
            if (value?.SecurityObservationId != record.Intent.ObservationId) continue;
            if (value != SecurityCommand(record)) return new(SecurityEventRecorderLookupState.Unknown);
            return new(SecurityEventRecorderLookupState.Recorded, new(record.Intent.ObservationId, record.Intent.RoutingTenantId, record.UtcDay,
                ReplicatedSecurityObservationSpool.IntentDigest(record.Intent), source.Identity.ActorId, item.SequenceNumber, item.MessageId));
        }
        string id=StableUlid("security",record.Intent.ObservationId);
        var retained=await ReadOriginalAsync("private-security-observation",id,cancellationToken).ConfigureAwait(false);
        if(!retained.Authoritative) return new(SecurityEventRecorderLookupState.Unknown);
        SecurityEventRecorderLookup NoRecord(string basis,string transport,IndependentOriginal? value,CommandStatusQueryResponse? terminal=null)=>new(SecurityEventRecorderLookupState.NotRecorded,NoRecordProof:
            new(id,transport,CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(value,CustodyBindingContract.Json)),source.NativeProof!.SourceIncarnation,source.Head,
                source.NativeProof.CumulativeDigest,terminal is null?null:CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(terminal,CustodyBindingContract.Json)),basis));
        if(retained.Original is null) return NoRecord("DefinitiveAbsent",id,null);
        if(!Same(retained.Original.Request,JsonSerializer.SerializeToElement(SecurityCommand(record),Json))) return new(SecurityEventRecorderLookupState.Unknown);
        if(retained.Original.SubmissionState=="Unsent" && retained.Original.ActiveTransportMessageId is null) return NoRecord("RetainedUnsent",id,retained.Original);
        if(retained.Original.SubmissionState!="Dispatched") return new(SecurityEventRecorderLookupState.Unknown);
        string transportId=retained.Original.ActiveTransportMessageId??id;
        if(transportId!=id)
        {
            var active=await ReadOriginalAsync("private-security-observation",transportId,cancellationToken).ConfigureAwait(false);
            if(!active.Authoritative || active.Original is not { SubmissionState:"Dispatched" } || !Same(active.Original.Request,retained.Original.Request)) return new(SecurityEventRecorderLookupState.Unknown);
        }
        CommandStatusQueryResponse? status;
        try { status=await gateway.GetWorkloadCommandStatusAsync(source.Identity.TenantId,transportId,cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch(Exception) { cancellationToken.ThrowIfCancellationRequested();return new(SecurityEventRecorderLookupState.Unknown); }
        // Empty native source is not evidence that a dispatched command is absent: a queued/processing command can still append later.
        bool terminalNoEvent=status is { EventCount:0,Retryable:false,CommittedEventSequence:>=0 } && status.CommittedEventSequence<=source.Head
            && status.MessageId==transportId && status.TenantId==source.Identity.TenantId && status.Domain==source.Identity.Domain && status.AggregateId==source.Identity.AggregateId
            && (status.StatusCode==(int)CommandStatus.Completed && status.Status==nameof(CommandStatus.Completed)
                || status.StatusCode==(int)CommandStatus.Rejected && status.Status==nameof(CommandStatus.Rejected));
        if(!terminalNoEvent) return new(SecurityEventRecorderLookupState.Unknown);
        var confirmed=await ReadAsync(source.Identity,cancellationToken).ConfigureAwait(false);
        return confirmed is not null && Same(source.NativeProof,confirmed.NativeProof)?NoRecord("TerminalNoEvent",transportId,retained.Original,status):new(SecurityEventRecorderLookupState.Unknown);
    }
    /// <inheritdoc />
    public async Task AppendAsync(SecurityObservationRecord record, CancellationToken cancellationToken = default)
    {
        var lookup = await LookupAsync(record, cancellationToken);
        if (lookup.State == SecurityEventRecorderLookupState.Recorded) return;
        if (lookup.State == SecurityEventRecorderLookupState.Unknown) throw new InvalidOperationException("Original source unknown.");
        var command = SecurityCommand(record); string id = StableUlid("security", record.Intent.ObservationId);
        var retained = await FindOriginalAsync("private-security-observation", id, cancellationToken);
        if (retained is null) { await BeginAsync("private-security-observation", id, command, cancellationToken); retained = await FindOriginalAsync("private-security-observation", id, cancellationToken); }
        if (retained is null || !Same(retained.Request, JsonSerializer.SerializeToElement(command, Json)) || retained.SubmissionState != "Unsent")
            throw new InvalidOperationException("Dispatched original requires separately authenticated exact recovery.");
        await SubmitAsync(SecurityIdentity(record), "Hexalith.Agents.Contracts.SecurityEventLog.RecordSecurityEvent", id, command, cancellationToken);
    }
    /// <inheritdoc />
    public async Task RecoverOriginalAsync(SecurityObservationRecord original, SecurityObservationRecoveryGrant grant, CancellationToken cancellationToken = default)
    {
        if (!Same(original, grant.Original)) throw new InvalidOperationException("Changed recovery original.");
        var proof = await LookupAsync(original, cancellationToken);
        if (proof.State == SecurityEventRecorderLookupState.Unknown || !await recoveryAuthority.VerifyAgedOriginalRecoveryAsync(grant, proof, cancellationToken))
            throw new InvalidOperationException("Current restricted exact-original recovery denied.");
        if (proof.State == SecurityEventRecorderLookupState.Recorded) return;
        string transportId=PrivateRecordingIdentity.SecurityRecoveryMessageId(grant);var command=SecurityCommand(original);
        var attempt=await ReadOriginalAsync("private-security-observation",transportId,cancellationToken).ConfigureAwait(false);
        if(!attempt.Authoritative) throw new InvalidOperationException("Recovery transport lookup unknown.");
        if(attempt.Original is null) { await BeginAsync("private-security-observation",transportId,command,cancellationToken).ConfigureAwait(false);attempt=await ReadOriginalAsync("private-security-observation",transportId,cancellationToken).ConfigureAwait(false); }
        if(!attempt.Authoritative || attempt.Original is not { SubmissionState:"Unsent" } || !Same(attempt.Original.Request,JsonSerializer.SerializeToElement(command,Json)))
            throw new InvalidOperationException("Recovery transport already dispatched or unknown; exact lookup only.");
        await SubmitAsync(SecurityIdentity(original), "Hexalith.Agents.Contracts.SecurityEventLog.RecordSecurityEvent",transportId,command,cancellationToken,recovery:grant);
    }
    private async Task<AuthoritativeEventStream?> ReadAsync(AggregateIdentity identity, CancellationToken token)
    {
        var read = await _reader.ReadAsync(identity, token);
        return read.IsAuthoritative && read.Stream?.NativeProof is { CodecVersion: 1, Erased: false, RetainedFloor: 1 } ? read.Stream : null;
    }
    private async Task<IndependentOriginal?> FindOriginalAsync(string role, string requestId, CancellationToken token)
        => (await ReadOriginalAsync(role,requestId,token).ConfigureAwait(false)).Original;
    private async Task<(bool Authoritative,IndependentOriginal? Original)> ReadOriginalAsync(string role,string requestId,CancellationToken token)
    {
        // A signed JSON null is a definite absence. Transport/default Undefined is unknown and must never authorize recovery.
        var value=await originals.InvokeAsync<JsonElement>("qualification-lookup-original",new { role,requestId },token).ConfigureAwait(false);
        if(value.ValueKind==JsonValueKind.Null) return (true,null);
        return value.ValueKind==JsonValueKind.Object?(true,value.Deserialize<IndependentOriginal>(CustodyBindingContract.Json)):(false,null);
    }
    private async Task<bool> BeginAsync(string role, string requestId, object original, CancellationToken token)
    {
        var result = await originals.InvokeAsync<Admission>("qualification-begin-original", new { role, requestId, original = JsonSerializer.SerializeToElement(original, Json) }, token);
        if (result is null || !result.Exact) throw new InvalidOperationException("Durable exact source original unavailable.");
        return result.First;
    }
    private async Task SubmitAsync(AggregateIdentity identity, string commandType, string messageId, object payload, CancellationToken token, SecurityObservationRecoveryGrant? recovery = null)
    {
        // No generic user/principal/Workflow dispatch. Scope and command are constructed here from immutable authenticated originals.
        var command = new SubmitCommandRequest(messageId, identity.TenantId, identity.Domain, identity.AggregateId, commandType,
            JsonSerializer.SerializeToElement(payload, Json), IdempotencyKey: messageId);
        var request = new BoundWorkloadCommandRequest(command, principal);
        string? proof = recovery is null ? await scopedProofs.IssueAsync(request, token)
            : scopedProofs is IPrivateRecoveryCommandProofSource recoveryProofs ? await recoveryProofs.IssueRecoveryAsync(request,recovery,token) : null;
        if (proof is null) throw new InvalidOperationException("Exact stream/event workload ACL denied.");
        string role = identity.Domain == "trusted-envelope-replay" ? "private-replay" : "private-security-observation";
        if (recovery is null && await originals.InvokeAsync<Permission>("recording-dispatch-admit", new { role, requestId = messageId, original = command.Payload }, token) is not { Allowed: true })
            throw new InvalidOperationException("Dispatch is unknown or already admitted; exact lookup required.");
        if(recovery is not null && await originals.InvokeAsync<Permission>("recording-recovery-dispatch-admit",new { command,grant=recovery },token).ConfigureAwait(false) is not { Allowed:true })
            throw new InvalidOperationException("Recovery transport or current original dispatch barrier is unknown; lookup only.");
        _ = await gateway.SubmitBoundWorkloadCommandAsync(request, proof, token); // Acceptance is never an authoritative source receipt.
    }
    private static TrustedEnvelopeReplayReceipt? ReadReplay(AuthoritativeEventStream? source, TrustedEnvelopeReplayIntent intent)
    {
        if (source is null || source.Head != 1 || source.Events.Count != 1) return null;
        var item = source.Events[0];
        if (item.SequenceNumber != 1 || !EventType(item.EventTypeName, "TrustedEnvelopeReplay", "TrustedEnvelopeFirstSeen")) return null;
        var value = Decode<ReplayEvent>(item.Payload)?.Registration;
        return value is not null && value.Issuer == intent.Issuer && value.DeliveryNonce == intent.DeliveryNonce
            && value.TargetTenantId == intent.TargetTenantId && value.LogicalCommandId == intent.LogicalCommandId
            && value.SigningKeyVersion == intent.SigningKeyVersion && value.CanonicalAndTagDigest == intent.CanonicalAndTagDigest
            && value.RetainUntil - value.FirstSeenAt == TimeSpan.FromSeconds(604800)
            ? new(intent, value.FirstSeenAt, value.RetainUntil, item.SequenceNumber, item.MessageId) : null;
    }
    private static bool EventType(string type, string area, string name) => type == "Hexalith.Agents.Contracts." + area + "." + name;
    private static T? Decode<T>(byte[] bytes)
    {
        try { CustodyBindingContract.RejectDuplicateProperties(bytes); return JsonSerializer.Deserialize<T>(bytes, Json); }
        catch (Exception) { return default; }
    }
    private static bool Same<T>(T first, T second) => JsonSerializer.SerializeToUtf8Bytes(first, Json).AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(second, Json));
    private static AggregateIdentity ReplayIdentity(TrustedEnvelopeReplayIntent intent) => new("system", "trusted-envelope-replay", Derive("trusted-envelope-replay", intent.Issuer, intent.DeliveryNonce));
    private static AggregateIdentity SecurityIdentity(SecurityObservationRecord record) => new(record.Intent.RoutingTenantId, "security-event-log", Derive("security-event-log", record.Intent.RoutingTenantId, record.UtcDay));
    private static SecurityObservationCommand SecurityCommand(SecurityObservationRecord record)
    {
        _ = ReplicatedSecurityObservationSpool.IntentDigest(record.Intent);
        if (record.Receipt is not null || record.ObservedAt.Offset != TimeSpan.Zero || record.UtcDay != record.ObservedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) || record.Sequence <= 0)
            throw new ArgumentException("Unchanged content-free original required.");
        return new(record.Intent.ObservationId, record.Intent.RoutingTenantId, record.UtcDay, record.ObservedAt,
            record.Intent.ReasonCode, record.Intent.UntrustedFieldsHmac, record.Intent.DigestKeyVersion);
    }
    private static string Derive(params string[] components) => Convert.ToHexStringLower(SHA256.HashData(PlatformCanonicalBytes.Identity(components)));
    internal static string StableUlid(params string[] components)
    {
        return PrivateRecordingIdentity.StableUlid(components);
    }
    private sealed record Admission(bool Exact, bool First);
    private sealed record Permission(bool Allowed);
    private sealed record ReplayCommand(string Issuer, string DeliveryNonce, string TargetTenantId, string LogicalCommandId, string SigningKeyVersion, string CanonicalAndTagDigest, DateTimeOffset FirstSeenAt, DateTimeOffset RetainUntil);
    private sealed record ReplayEvent(ReplayCommand Registration);
    private sealed record SecurityObservationCommand(string SecurityObservationId, string RoutingTenantId, string UtcDay, DateTimeOffset ObservedAt, string ReasonCode, string UntrustedFieldsHmac, string DigestKeyVersion);
    private sealed record SecurityEvent(SecurityObservationCommand Observation);
}
