using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.Platform.Custody;

/// <summary>Actual private DAPR conditional durable spool/recovery source, disabled without independently qualified component/replicas/epoch/credentials.
/// The fixed reader bound is operational refusal, not ARCH-A-9 capacity qualification. No observations are purged and no Agents domain handler is fabricated.</summary>
public sealed class ReplicatedSecurityObservationSpool(DaprClient client, TimeProvider clock, IReplicatedSecuritySpoolAuthority? authority = null,
    ISecurityObservationRecorder? recorder = null)
{
    private const int RecordBound = 10000;
    private static string Key(ReplicatedSecuritySpoolTarget target) => "system/security-observations/" + target.InstallationEpoch;
    /// <summary>Retains an original safe observation before processed-denial can be reported; duplicates reuse stored first-seen/day/sequence.</summary>
    public async Task<SecurityObservationRecord?> ObserveAsync(SecurityObservationIntent intent, CancellationToken cancellationToken = default)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, cancellationToken);
        try
        {
            ValidateIntent(intent); var target = await CurrentAsync("Observe", intent, budget).ConfigureAwait(false); if (target is null) { return null; }
            var (state, etag) = await ReadAsync(target, budget).ConfigureAwait(false); if (state is null) { return null; }
            var prior = state.Records.SingleOrDefault(record => record.Intent.ObservationId == intent.ObservationId);
            if (prior is not null) { return prior.Intent == intent && await StillCurrentAsync(target, "Observe", intent, budget).ConfigureAwait(false) ? prior : null; }
            if (state.Records.Count >= RecordBound) { return null; }
            DateTimeOffset observed = clock.GetUtcNow(); var record = new SecurityObservationRecord(intent, observed, observed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), state.Records.Count + 1L, null);
            var next = state with { Revision = checked(state.Revision + 1), Records = state.Records.Append(record).ToArray() };
            await TryWriteAsync(target, state, next, etag, budget).ConfigureAwait(false);
            var (confirmed, _) = await ReadAsync(target, budget).ConfigureAwait(false); var original = confirmed?.Records.SingleOrDefault(r => r.Intent.ObservationId == intent.ObservationId);
            return original?.Intent == intent && await StillCurrentAsync(target, "Observe", intent, budget).ConfigureAwait(false) ? original : null;
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return null; }
    }
    /// <summary>Looks up the exact original observation without creating, recording or acknowledging it; current private lookup authority is reconfirmed.</summary>
    public async Task<SecurityObservationRecord?> LookupAsync(SecurityObservationIntent intent, CancellationToken cancellationToken = default)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, cancellationToken);
        try
        {
            ValidateIntent(intent); var target = await CurrentAsync("Lookup", intent, budget).ConfigureAwait(false); if (target is null) { return null; }
            var (state, _) = await ReadAsync(target, budget).ConfigureAwait(false); if (state is null) { return null; }
            var original = state.Records.SingleOrDefault(record => record.Intent.ObservationId == intent.ObservationId);
            return original?.Intent == intent && await StillCurrentAsync(target, "Lookup", intent, budget).ConfigureAwait(false) ? original : null;
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return null; }
    }
    /// <summary>Recovers only exact durable source evidence; unknown lookup/append/ack retains pending records and cannot pass readiness.</summary>
    public async Task<int> DrainAsync(int maximumCount, CancellationToken cancellationToken = default)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, cancellationToken); int acknowledged = 0;
        try
        {
            if (maximumCount is < 1 or > RecordBound) { throw new ArgumentOutOfRangeException(nameof(maximumCount)); }
            var target = await CurrentAsync("Drain", null, budget).ConfigureAwait(false); if (target is null || recorder is null) { return 0; }
            var (state, _) = await ReadAsync(target, budget).ConfigureAwait(false); if (state is null) { return 0; }
            foreach (var record in state.Records.Where(record => record.Receipt is null).Take(maximumCount))
            {
                budget.Check();
                if (!await StillCurrentAsync(target, "Drain", record.Intent, budget).ConfigureAwait(false)) { return acknowledged; }
                var found = await budget.ReadAsync(() => recorder.LookupAsync(record, CancellationToken.None)).ConfigureAwait(false);
                if (found.State == SecurityEventRecorderLookupState.NotRecorded && found.Receipt is null
                    && clock.GetUtcNow() < record.ObservedAt + PlatformAcceptedEnvelopeTiming.RecoveryHorizon)
                {
                    try { await budget.ReadAsync(async () => { await recorder.AppendAsync(record, CancellationToken.None).ConfigureAwait(false); return true; }).ConfigureAwait(false); }
                    catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); }
                    found = await budget.ReadAsync(() => recorder.LookupAsync(record, CancellationToken.None)).ConfigureAwait(false);
                }
                if (found.State != SecurityEventRecorderLookupState.Recorded || !Exact(record, found.Receipt)) { continue; }
                // Refresh the authoritative conditional owner before ack; concurrent observations are retained by the CAS.
                var (current, etag) = await ReadAsync(target, budget).ConfigureAwait(false); if (current is null) { return acknowledged; }
                var existing = current.Records.SingleOrDefault(r => r.Intent.ObservationId == record.Intent.ObservationId);
                if (existing is null || existing.Intent != record.Intent || existing.ObservedAt != record.ObservedAt) { return acknowledged; }
                if (existing.Receipt is not null) { if (existing.Receipt == found.Receipt) { continue; } return acknowledged; }
                var next = current with { Revision = checked(current.Revision + 1), Records = current.Records.Select(r => r.Intent.ObservationId == record.Intent.ObservationId ? r with { Receipt = found.Receipt } : r).ToArray() };
                await TryWriteAsync(target, current, next, etag, budget).ConfigureAwait(false);
                var (saved, _) = await ReadAsync(target, budget).ConfigureAwait(false);
                if (saved?.Records.SingleOrDefault(r => r.Intent.ObservationId == record.Intent.ObservationId)?.Receipt == found.Receipt
                    && await StillCurrentAsync(target, "Drain", record.Intent, budget).ConfigureAwait(false)) { acknowledged++; }
            }
            return acknowledged;
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return acknowledged; }
    }
    /// <summary>Readiness requires qualified current complete spool state with every original observation durably recorded; missing/stale/unknown state always blocks.</summary>
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, cancellationToken);
        try
        {
            var target = await CurrentAsync("Readiness", null, budget).ConfigureAwait(false); if (target is null) { return false; }
            var (state, _) = await ReadAsync(target, budget).ConfigureAwait(false);
            return state is not null && state.Records.All(record => record.Receipt is not null) && await StillCurrentAsync(target, "Readiness", null, budget).ConfigureAwait(false);
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return false; }
    }
    /// <summary>Computes only a digest of already keyed/content-free intent fields, never unkeyed untrusted content.</summary>
    public static string IntentDigest(SecurityObservationIntent intent) { ValidateIntent(intent); return Digest(intent); }
    /// <summary>Gets the exact source owner identity required by the restricted recorder credential.</summary>
    public static string SourceStream(SecurityObservationRecord record)
    { ArgumentNullException.ThrowIfNull(record); return new AggregateIdentity(record.Intent.RoutingTenantId, "agents", "SecurityEventLog-" + record.UtcDay).ActorId; }
    private async Task<ReplicatedSecuritySpoolTarget?> CurrentAsync(string method, SecurityObservationIntent? intent, PrivateOwnerOperationDeadline budget)
    {
        if (authority is null) { return null; } var target = await budget.ReadAsync(() => authority.GetCurrentAsync(CancellationToken.None)).ConfigureAwait(false);
        if (target is null || !ValidTarget(target) || !await budget.ReadAsync(() => authority.AuthorizeAsync(target, method, intent, CancellationToken.None)).ConfigureAwait(false)) { return null; } return target;
    }
    private async Task<bool> StillCurrentAsync(ReplicatedSecuritySpoolTarget original, string method, SecurityObservationIntent? intent, PrivateOwnerOperationDeadline budget)
        => await CurrentAsync(method, intent, budget).ConfigureAwait(false) == original;
    private bool ValidTarget(ReplicatedSecuritySpoolTarget target)
    { Text(target.ComponentName); Text(target.InstallationEpoch); Text(target.AuthorityRevision); return target.ValidUntil.Offset == TimeSpan.Zero && clock.GetUtcNow() < target.ValidUntil; }
    private async Task<(SecuritySpoolSnapshot? State, string Etag)> ReadAsync(ReplicatedSecuritySpoolTarget target, PrivateOwnerOperationDeadline budget)
    {
        var (read, etag) = await budget.ReadAsync(() => client.GetStateAndETagAsync<SecuritySpoolSnapshot>(target.ComponentName, Key(target), ConsistencyMode.Strong, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
        var state = read is null ? new(target.InstallationEpoch, 0, Array.Empty<SecurityObservationRecord>()) : Capture(read, budget);
        if (state.InstallationEpoch != target.InstallationEpoch || authority is null || !await budget.ReadAsync(() => authority.ValidateStateAsync(target, state.Revision, StateDigest(state), CancellationToken.None)).ConfigureAwait(false)) { return (null, etag); }
        return (state, etag);
    }
    private async Task TryWriteAsync(ReplicatedSecuritySpoolTarget target, SecuritySpoolSnapshot previous, SecuritySpoolSnapshot next, string etag, PrivateOwnerOperationDeadline budget)
    {
        _ = Capture(next, budget);
        if (authority is null || !await budget.ReadAsync(() => authority.RecordRevisionAsync(target, previous.Revision, next.Revision, StateDigest(next), CancellationToken.None)).ConfigureAwait(false)) { return; }
        try { await budget.ReadAsync(() => client.TrySaveStateAsync(target.ComponentName, Key(target), next, etag, new StateOptions { Concurrency = ConcurrencyMode.FirstWrite }, cancellationToken: CancellationToken.None)).ConfigureAwait(false); }
        catch (Exception) { budget.Check(); } // Read-back, never the save acknowledgement, decides durability.
    }
    private static SecuritySpoolSnapshot Capture(SecuritySpoolSnapshot source, PrivateOwnerOperationDeadline budget)
    {
        if (source.Revision < 0 || source.Records is null || source.Records.Count > RecordBound) { throw new InvalidOperationException("Malformed private spool state."); }
        Text(source.InstallationEpoch); var owned = new List<SecurityObservationRecord>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in source.Records)
        {
            budget.Check(); if (owned.Count >= RecordBound || record is null) { throw new InvalidOperationException("Malformed private spool state."); }
            ValidateIntent(record.Intent);
            if (!ids.Add(record.Intent.ObservationId) || record.Sequence != owned.Count + 1L || record.ObservedAt.Offset != TimeSpan.Zero
                || record.UtcDay != record.ObservedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) || record.Receipt is not null && !Exact(record, record.Receipt))
            { throw new InvalidOperationException("Malformed private spool record."); } owned.Add(record);
        }
        if (source.Revision < owned.Count || source.Revision > owned.Count * 2L || source.Revision != owned.Count + owned.Count(r => r.Receipt is not null))
        { throw new InvalidOperationException("Malformed private spool revision."); }
        return source with { Records = Array.AsReadOnly(owned.ToArray()) };
    }
    private static bool Exact(SecurityObservationRecord record, SecurityEventRecordReceipt? receipt) => receipt is not null
        && receipt.ObservationId == record.Intent.ObservationId && receipt.RoutingTenantId == record.Intent.RoutingTenantId && receipt.UtcDay == record.UtcDay
        && receipt.OriginalIntentDigest == IntentDigest(record.Intent) && receipt.SourceStreamId == SourceStream(record) && receipt.SourceRevision > 0 && !string.IsNullOrWhiteSpace(receipt.EventId);
    private static void ValidateIntent(SecurityObservationIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent); foreach (string field in new[] { intent.ObservationId, intent.RoutingTenantId, intent.DigestKeyVersion }) { Text(field); }
        if (intent.UntrustedFieldsHmac is not { Length: 64 } || intent.UntrustedFieldsHmac.Any(c => !char.IsAsciiHexDigit(c))
            || intent.ReasonCode is not ("invalid-tag" or "scope-mismatch" or "unknown-principal" or "stale-authority" or "replay-conflict" or "unavailable-owner"))
        { throw new ArgumentException("Malformed safe security observation."); }
    }
    private static void Text(string value)
    { if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || new System.Text.UTF8Encoding(false, true).GetByteCount(value) > 2048) { throw new ArgumentException("Malformed private spool identity."); } }
    /// <summary>Hashes the captured content-free/keyed state for exact independent restore-anchor binding.</summary>
    internal static string StateDigest(SecuritySpoolSnapshot state) => Digest(state);
    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
