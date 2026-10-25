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
        var command = new ReplayCommand(intent.Issuer, intent.DeliveryNonce, intent.TargetTenantId, intent.LogicalCommandId,
            intent.SigningKeyVersion, intent.CanonicalAndTagDigest, evaluatedAt, evaluatedAt.Add(retention));
        string id = StableUlid("replay", identity.ActorId);
        bool first = await BeginAsync("private-replay", id, command, cancellationToken);
        if (!first) return await LookupAsync(intent, cancellationToken); // Unknown original never re-executes.
        await SubmitAsync(identity, "Hexalith.Agents.Contracts.TrustedEnvelopeReplay.RegisterTrustedEnvelopeFirstSeen", id, command, cancellationToken);
        return await LookupAsync(intent, cancellationToken);
    }
    /// <inheritdoc />
    public async Task<SecurityEventRecorderLookup> LookupAsync(SecurityObservationRecord record, CancellationToken cancellationToken = default)
    {
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
        return new(SecurityEventRecorderLookupState.NotRecorded);
    }
    /// <inheritdoc />
    public async Task AppendAsync(SecurityObservationRecord record, CancellationToken cancellationToken = default)
    {
        var lookup = await LookupAsync(record, cancellationToken);
        if (lookup.State == SecurityEventRecorderLookupState.Recorded) return;
        if (lookup.State == SecurityEventRecorderLookupState.Unknown) throw new InvalidOperationException("Original source unknown.");
        var command = SecurityCommand(record); string id = StableUlid("security", record.Intent.ObservationId);
        if (!await BeginAsync("private-security-observation", id, command, cancellationToken))
            throw new InvalidOperationException("Started original requires separately authenticated exact recovery.");
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
        await SubmitAsync(SecurityIdentity(original), "Hexalith.Agents.Contracts.SecurityEventLog.RecordSecurityEvent",
            StableUlid("security", original.Intent.ObservationId), SecurityCommand(original), cancellationToken);
    }
    private async Task<AuthoritativeEventStream?> ReadAsync(AggregateIdentity identity, CancellationToken token)
    {
        var read = await _reader.ReadAsync(identity, token);
        return read.IsAuthoritative && read.Stream?.NativeProof is { CodecVersion: 1, Erased: false, RetainedFloor: 1 } ? read.Stream : null;
    }
    private async Task<bool> BeginAsync(string role, string requestId, object original, CancellationToken token)
    {
        var result = await originals.InvokeAsync<Admission>("qualification-begin-original", new { role, requestId, original = JsonSerializer.SerializeToElement(original, Json) }, token);
        if (result is null || !result.Exact) throw new InvalidOperationException("Durable exact source original unavailable.");
        return result.First;
    }
    private async Task SubmitAsync(AggregateIdentity identity, string commandType, string messageId, object payload, CancellationToken token)
    {
        // No generic user/principal/Workflow dispatch. Scope and command are constructed here from immutable authenticated originals.
        var command = new SubmitCommandRequest(messageId, identity.TenantId, identity.Domain, identity.AggregateId, commandType,
            JsonSerializer.SerializeToElement(payload, Json), IdempotencyKey: messageId);
        var request = new BoundWorkloadCommandRequest(command, principal);
        string? proof = await scopedProofs.IssueAsync(request, token);
        if (proof is null) throw new InvalidOperationException("Exact stream/event workload ACL denied.");
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
    private static string StableUlid(params string[] components)
    {
        byte[] hash = SHA256.HashData(PlatformCanonicalBytes.Identity(components));
        var value = new BigInteger(hash.AsSpan(0, 16), isUnsigned: true, isBigEndian: true);
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; var output = new char[26];
        for (int index = 25; index >= 0; index--) { output[index] = alphabet[(int)(value & 31)]; value >>= 5; }
        return new(output);
    }
    private sealed record Admission(bool Exact, bool First);
    private sealed record ReplayCommand(string Issuer, string DeliveryNonce, string TargetTenantId, string LogicalCommandId, string SigningKeyVersion, string CanonicalAndTagDigest, DateTimeOffset FirstSeenAt, DateTimeOffset RetainUntil);
    private sealed record ReplayEvent(ReplayCommand Registration);
    private sealed record SecurityObservationCommand(string SecurityObservationId, string RoutingTenantId, string UtcDay, DateTimeOffset ObservedAt, string ReasonCode, string UntrustedFieldsHmac, string DigestKeyVersion);
    private sealed record SecurityEvent(SecurityObservationCommand Observation);
}
