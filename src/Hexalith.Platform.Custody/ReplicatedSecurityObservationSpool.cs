using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Actual private DAPR conditional durable spool/recovery source, disabled without independently qualified component/replicas/epoch/credentials.
/// The fixed reader bound is operational refusal, not ARCH-A-9 capacity qualification. No observations are purged and no Agents domain handler is fabricated.</summary>
public sealed class ReplicatedSecurityObservationSpool(DaprClient client, TimeProvider clock, IReplicatedSecuritySpoolAuthority? authority = null,
    ISecurityObservationRecorder? recorder = null)
{
    private const int RecordBound = 10000;
    private const int ArchivePageBound = 16;
    private const int ReceiptEventIdByteBound = 2048;
    // This exceeds the serialized maximum of one bounded intent and its worst escaped receipt,
    // including repeated IDs, source identity, numeric fields and JSON framing.
    private const int WorstCaseObservationAndReceiptBytes = 262144;
    private static readonly System.Text.UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string MaximumEscapedEventId = new('\0', ReceiptEventIdByteBound);
    private static string Key(ReplicatedSecuritySpoolTarget target) => "system/security-observations/" + target.InstallationEpoch;
    /// <summary>Retains an original safe observation before processed-denial can be reported; duplicates reuse stored first-seen/day/sequence.</summary>
    public async Task<SecurityObservationRecord?> ObserveAsync(SecurityObservationIntent intent, CancellationToken cancellationToken = default)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, cancellationToken);
        try
        {
            ValidateIntent(intent); var target = await CurrentAsync("Observe", intent, budget).ConfigureAwait(false); if (target is null) { return null; }
            var (state, etag) = await ReadAsync(target, budget, true).ConfigureAwait(false); if (state is null) { return null; }
            var prior = state.Records.SingleOrDefault(record => record.Intent.ObservationId == intent.ObservationId
                || intent.RetainedServerReceiptKey is not null && record.Intent.RetainedServerReceiptKey == intent.RetainedServerReceiptKey)
                ?? await FindArchivedAsync(target, state, record => record.Intent.ObservationId == intent.ObservationId
                    || intent.RetainedServerReceiptKey is not null && record.Intent.RetainedServerReceiptKey == intent.RetainedServerReceiptKey, budget).ConfigureAwait(false);
            if (prior is not null) { return prior.Intent == intent && await StillCurrentAsync(target, "Observe", intent, budget).ConfigureAwait(false) ? prior : null; }
            DateTimeOffset observed = clock.GetUtcNow();
            SecurityObservationRecord record = new(intent, observed, observed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), checked(state.ArchivedObservedCount + state.Records.Count + 1L), null);
            var next = state with { Revision = checked(state.Revision + 1), Records = state.Records.Append(record).ToArray() };
            budget.Check();
            if (!FitsWithReceiptReserve(next, budget))
            {
                if (!await ArchiveAcknowledgedPageAsync(target, state, etag, budget).ConfigureAwait(false)) { return null; }
                (state, etag) = await ReadAsync(target, budget, true).ConfigureAwait(false);
                if (state is null) { return null; }
                record = record with { Sequence = checked(state.ArchivedObservedCount + state.Records.Count + 1L) };
                next = state with { Revision = checked(state.Revision + 1), Records = state.Records.Append(record).ToArray() };
                budget.Check();
                if (!FitsWithReceiptReserve(next, budget)) { return null; }
            }
            var (retainedStage, _) = await budget.ReadAsync(() => client.GetStateAndETagAsync<AnchoredStateTransition>(target.ComponentName, PendingKey(target), ConsistencyMode.Strong, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
            if (retainedStage is not null)
            {
                // An admitted original retry retains its first-seen facts. Another intent cannot replace this slot or advance its anchor.
                if (retainedStage.TargetBytes is not { Length: > 0 and <= RecoverableAnchoredState.MaximumPendingBytes }) { return null; }
                var staged = Capture(JsonSerializer.Deserialize<SecuritySpoolSnapshot>(retainedStage.TargetBytes) ?? throw new InvalidOperationException("Missing original spool stage."), budget);
                if (staged.InstallationEpoch != state.InstallationEpoch || staged.Revision != state.Revision + 1 || staged.Records.Count != state.Records.Count + 1
                    || staged.Records[^1].Intent != intent || staged.Records[^1].Receipt is not null || staged.Records[^1].ObservedAt > clock.GetUtcNow()
                    || StateDigest(state with { Records = staged.Records.Take(state.Records.Count).ToArray() }) != StateDigest(state)) { return null; }
                var exact = RecoverableAnchoredState.Prepare(PendingScope(target), state.Revision, staged.Revision, state, staged);
                if (JsonSerializer.Serialize(exact) != JsonSerializer.Serialize(retainedStage)) { return null; }
                next = staged;
            }
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
            var original = state.Records.SingleOrDefault(record => record.Intent.ObservationId == intent.ObservationId)
                ?? await FindArchivedAsync(target, state, record => record.Intent.ObservationId == intent.ObservationId, budget).ConfigureAwait(false);
            return original?.Intent == intent && await StillCurrentAsync(target, "Lookup", intent, budget).ConfigureAwait(false) ? original : null;
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return null; }
    }
    /// <summary>Recovers original authenticated routing by exact opaque retained-server-receipt identity, without observing or acknowledging anything. Unknown lookup never certifies absence.</summary>
    public async Task<SecurityObservationOriginalLookupResult> LookupOriginalAsync(SecurityObservationOriginalLookup lookup, CancellationToken cancellationToken = default)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, cancellationToken);
        try
        {
            ValidateIntent(new(lookup.RetainedServerReceiptKey, "system", lookup.ReasonCode, lookup.UntrustedFieldsHmac, lookup.DigestKeyVersion));
            if (authority is null) { return new(false); }
            var target = await CurrentLookupAsync().ConfigureAwait(false); if (target is null) { return new(false); }
            var (state, _) = await ReadAsync(target, budget).ConfigureAwait(false); if (state is null) { return new(false); }
            var original = state.Records.SingleOrDefault(value => value.Intent.RetainedServerReceiptKey == lookup.RetainedServerReceiptKey)
                ?? await FindArchivedAsync(target, state, value => value.Intent.RetainedServerReceiptKey == lookup.RetainedServerReceiptKey, budget).ConfigureAwait(false);
            if (original is not null && (original.Intent.ReasonCode != lookup.ReasonCode || original.Intent.UntrustedFieldsHmac != lookup.UntrustedFieldsHmac
                || original.Intent.DigestKeyVersion != lookup.DigestKeyVersion)) { return new(false); }
            var final = await CurrentLookupAsync().ConfigureAwait(false); budget.Check();
            return final == target && ValidTarget(target) ? new(true, original) : new(false);

            async Task<ReplicatedSecuritySpoolTarget?> CurrentLookupAsync()
            {
                var current = await budget.ReadAsync(() => authority.GetCurrentAsync(CancellationToken.None)).ConfigureAwait(false);
                if (current is null || !ValidTarget(current) || !await budget.ReadAsync(() => authority.AuthorizeOriginalLookupAsync(current, lookup, CancellationToken.None)).ConfigureAwait(false)) { return null; }
                budget.Check(); return ValidTarget(current) ? current : null;
            }
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return new(false); }
    }
    /// <summary>Recovers only exact durable source evidence; unknown lookup/append/ack retains pending records and cannot pass readiness.</summary>
    public async Task<int> DrainAsync(int maximumCount, CancellationToken cancellationToken = default)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, cancellationToken); int acknowledged = 0;
        try
        {
            if (maximumCount is < 1 or > RecordBound) { throw new ArgumentOutOfRangeException(nameof(maximumCount)); }
            var target = await CurrentAsync("Drain", null, budget).ConfigureAwait(false); if (target is null || recorder is null) { return 0; }
            var (state, _) = await ReadAsync(target, budget, true).ConfigureAwait(false); if (state is null) { return 0; }
            for (int selected = 0; selected < maximumCount; selected++)
            {
                budget.Check();
                var (scheduledState, scheduledEtag) = await ReadAsync(target, budget).ConfigureAwait(false); if (scheduledState is null) { return acknowledged; }
                // Only each source's earliest unresolved original is routable; rotate across those streams without skipping their order.
                var candidates = scheduledState.Records.Where(r => r.Receipt is null).GroupBy(SourceStream, StringComparer.Ordinal).Select(group => group.First()).OrderBy(r => r.Sequence).ToArray();
                if (candidates.Length == 0)
                {
                    if (scheduledState.Records.Count == RecordBound && scheduledState.PageIndex < ArchivePageBound && scheduledState.Records.All(record => record.Receipt is not null))
                    { _ = await ArchiveAcknowledgedPageAsync(target, scheduledState, scheduledEtag, budget).ConfigureAwait(false); }
                    return acknowledged;
                }
                var record = candidates.FirstOrDefault(r => r.Sequence > scheduledState.DrainAfterSequence) ?? candidates[0];
                if (!await StillCurrentAsync(target, "Drain", record.Intent, budget).ConfigureAwait(false)) { return acknowledged; }
                var scheduled = scheduledState with { Revision = checked(scheduledState.Revision + 1), DrainRevision = checked(scheduledState.DrainRevision + 1), DrainAfterSequence = record.Sequence };
                await TryWriteAsync(target, scheduledState, scheduled, scheduledEtag, budget).ConfigureAwait(false);
                var (confirmedSchedule, _) = await ReadAsync(target, budget).ConfigureAwait(false);
                if (confirmedSchedule is null || StateDigest(confirmedSchedule) != StateDigest(scheduled)) { return acknowledged; }
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
                if (saved is not null && saved.Records.SingleOrDefault(r => r.Intent.ObservationId == record.Intent.ObservationId)?.Receipt == found.Receipt
                    && await StillCurrentAsync(target, "Drain", record.Intent, budget).ConfigureAwait(false))
                {
                    acknowledged++;
                    if (saved.Records.Count == RecordBound && saved.PageIndex < ArchivePageBound && saved.Records.All(value => value.Receipt is not null))
                    {
                        var (full, fullEtag) = await ReadAsync(target, budget).ConfigureAwait(false);
                        if (full is not null && StateDigest(full) == StateDigest(saved))
                        { _ = await ArchiveAcknowledgedPageAsync(target, full, fullEtag, budget).ConfigureAwait(false); }
                    }
                }
            }
            return acknowledged;
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return acknowledged; }
    }
    /// <summary>Separately authorized recovery of one retained aged original. Automatic drain remains bounded by H; missing fresh independent permission or uncertain source proof denies every new effect.</summary>
    public async Task<SecurityObservationRecord?> RecoverOriginalAsync(SecurityObservationIntent intent, CancellationToken cancellationToken = default)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, cancellationToken);
        try
        {
            ValidateIntent(intent); var target = await CurrentAsync("RecoverOriginal", intent, budget).ConfigureAwait(false);
            if (target is null || recorder is null || authority is null) { return null; }
            // Ordinary lookup cannot elevate an unanchored stage before separate exact-original mutation admission.
            var (state, _) = await ReadAsync(target, budget).ConfigureAwait(false);
            var retained = state?.Records.SingleOrDefault(record => record.Intent.ObservationId == intent.ObservationId);
            if (retained?.Intent != intent || clock.GetUtcNow() < retained.ObservedAt + PlatformAcceptedEnvelopeTiming.RecoveryHorizon
                || state!.Records.Any(record => record.Receipt is null && record.Sequence < retained.Sequence && SourceStream(record) == SourceStream(retained))) { return null; }
            var original = retained with { Receipt = null };
            var found = await budget.ReadAsync(() => recorder.LookupAsync(original, CancellationToken.None)).ConfigureAwait(false);
            if (!OriginalProof(original, found)) { return null; }
            var grant = await budget.ReadAsync(() => authority.AuthorizeAgedOriginalRecoveryAsync(target, original, found, CancellationToken.None)).ConfigureAwait(false);
            if (!ValidRecoveryGrant(grant, target, original) || grant!.OriginalProof != found || !await StillGrantedAsync(found).ConfigureAwait(false)) { return null; }
            // The original source may have recorded between admission and this lookup; never issue a second effect from stale absence.
            found = await budget.ReadAsync(() => recorder.LookupAsync(original, CancellationToken.None)).ConfigureAwait(false);
            if (!OriginalProof(original, found) || !await StillGrantedAsync(found).ConfigureAwait(false)) { return null; }
            if (found.State == SecurityEventRecorderLookupState.NotRecorded)
            {
                try { await budget.ReadAsync(async () => { await recorder.RecoverOriginalAsync(original, grant, CancellationToken.None).ConfigureAwait(false); return true; }).ConfigureAwait(false); }
                catch (Exception) { budget.Check(); }
                found = await budget.ReadAsync(() => recorder.LookupAsync(original, CancellationToken.None)).ConfigureAwait(false);
            }
            if (found.State != SecurityEventRecorderLookupState.Recorded || !Exact(original, found.Receipt) || !await StillGrantedAsync(found).ConfigureAwait(false)) { return null; }
            var (current, etag) = await ReadAsync(target, budget, true).ConfigureAwait(false);
            var existing = current?.Records.SingleOrDefault(record => record.Intent.ObservationId == intent.ObservationId);
            if (existing is null || existing with { Receipt = null } != original || existing.Receipt is not null && existing.Receipt != found.Receipt) { return null; }
            if (existing.Receipt is null)
            {
                if (!await StillGrantedAsync(found).ConfigureAwait(false)) { return null; }
                var next = current! with { Revision = checked(current.Revision + 1), Records = current.Records.Select(record => record.Sequence == original.Sequence ? record with { Receipt = found.Receipt } : record).ToArray() };
                await TryWriteAsync(target, current, next, etag, budget).ConfigureAwait(false);
            }
            var (saved, _) = await ReadAsync(target, budget).ConfigureAwait(false);
            var confirmed = saved?.Records.SingleOrDefault(record => record.Sequence == original.Sequence);
            return confirmed is not null && confirmed with { Receipt = null } == original && confirmed.Receipt == found.Receipt
                && await StillGrantedAsync(found).ConfigureAwait(false) ? confirmed : null;

            async Task<bool> StillGrantedAsync(SecurityEventRecorderLookup proof)
            {
                if (!ValidRecoveryGrant(grant, target, original) || !OriginalProof(original, proof)) { return false; }
                if (!await budget.ReadAsync(() => authority.VerifyAgedOriginalRecoveryAsync(grant!, proof, CancellationToken.None)).ConfigureAwait(false)
                    || !await StillCurrentAsync(target, "RecoverOriginal", intent, budget).ConfigureAwait(false)) { return false; }
                bool verified = await budget.ReadAsync(() => authority.VerifyAgedOriginalRecoveryAsync(grant!, proof, CancellationToken.None)).ConfigureAwait(false);
                budget.Check(); return verified && ValidRecoveryGrant(grant, target, original);
            }
        }
        catch (Exception) { cancellationToken.ThrowIfCancellationRequested(); return null; }
    }
    private bool ValidRecoveryGrant(SecurityObservationRecoveryGrant? grant, ReplicatedSecuritySpoolTarget target, SecurityObservationRecord original)
        => grant is not null && grant.Target == target && grant.Original == original && grant.Original.Receipt is null
            && OriginalProof(original, grant.OriginalProof) && !string.IsNullOrWhiteSpace(grant.RecoveryAuthorityRevision) && grant.RecoveryAuthorityRevision.Length <= 256
            && grant.ObservedAt.Offset == TimeSpan.Zero && grant.ObservedAt != default && grant.ObservedAt <= clock.GetUtcNow()
            && grant.ValidUntil.Offset == TimeSpan.Zero && grant.ValidUntil > clock.GetUtcNow() && grant.ValidUntil <= target.ValidUntil && ValidTarget(target);
    private static bool OriginalProof(SecurityObservationRecord original, SecurityEventRecorderLookup? proof)
        => proof is not null && (proof.State == SecurityEventRecorderLookupState.NotRecorded && proof.Receipt is null
            || proof.State == SecurityEventRecorderLookupState.Recorded && Exact(original, proof.Receipt));

    /// <summary>Readiness requires a recorder, remaining admission capacity and qualified current complete acknowledged state; missing/stale/unknown state always blocks.</summary>
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        var budget = new PrivateOwnerOperationDeadline(clock, cancellationToken);
        try
        {
            budget.Check(); if (recorder is null) { return false; }
            var target = await CurrentAsync("Readiness", null, budget).ConfigureAwait(false); if (target is null) { return false; }
            var (state, _) = await ReadAsync(target, budget).ConfigureAwait(false);
            var (pending, _) = await budget.ReadAsync(() => client.GetStateAndETagAsync<AnchoredStateTransition>(target.ComponentName, PendingKey(target), ConsistencyMode.Strong, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
            return state is not null && pending is null && (state.Records.Count < RecordBound || state.PageIndex < ArchivePageBound)
                && state.Records.All(record => record.Receipt is not null)
                && await ArchivesCompleteAsync(target, state, budget).ConfigureAwait(false)
                && (state.PageIndex < ArchivePageBound
                    ? await NextArchiveSlotAvailableAsync(target, state, budget).ConfigureAwait(false)
                    : state.Records.Count < RecordBound && FitsWithReceiptReserve(state, budget, WorstCaseObservationAndReceiptBytes))
                && await StillCurrentAsync(target, "Readiness", null, budget).ConfigureAwait(false);
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
        if (target is null || !ValidTarget(target) || !await budget.ReadAsync(() => authority.AuthorizeAsync(target, method, intent, CancellationToken.None)).ConfigureAwait(false)) { return null; }
        budget.Check(); return ValidTarget(target) ? target : null;
    }
    private async Task<bool> StillCurrentAsync(ReplicatedSecuritySpoolTarget original, string method, SecurityObservationIntent? intent, PrivateOwnerOperationDeadline budget)
        => await CurrentAsync(method, intent, budget).ConfigureAwait(false) == original;
    private bool ValidTarget(ReplicatedSecuritySpoolTarget target)
    { Text(target.ComponentName); Text(target.InstallationEpoch); Text(target.AuthorityRevision); return target.ValidUntil.Offset == TimeSpan.Zero && clock.GetUtcNow() < target.ValidUntil; }
    private static string PendingKey(ReplicatedSecuritySpoolTarget target) => Key(target) + "-pending-transition-v1";
    private static string ArchiveKey(ReplicatedSecuritySpoolTarget target, long page, string digest)
        => Key(target) + "/archive/" + page.ToString(CultureInfo.InvariantCulture) + "/" + digest;
    private static string PendingScope(ReplicatedSecuritySpoolTarget target) => target.ComponentName + "|" + Key(target);
    private async Task<bool> ArchiveAcknowledgedPageAsync(ReplicatedSecuritySpoolTarget target, SecuritySpoolSnapshot state, string etag, PrivateOwnerOperationDeadline budget)
    {
        if (state.PageIndex >= ArchivePageBound || state.Records.Count is < 1 or > RecordBound
            || state.Records.Any(record => record.Receipt is null) || !ValidTarget(target)) { return false; }
        var page = new SecuritySpoolArchivePage(state.PageIndex, state, state.ArchiveHeadDigest);
        string digest = Digest(page); string key = ArchiveKey(target, state.PageIndex, digest);
        var (existing, archiveEtag) = await budget.ReadAsync(() => client.GetStateAndETagAsync<SecuritySpoolArchivePage>(target.ComponentName, key, ConsistencyMode.Strong, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
        if (existing is null)
        {
            _ = await budget.ReadAsync(() => client.TrySaveStateAsync(target.ComponentName, key, page, archiveEtag,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite }, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
        }
        var (confirmed, _) = await budget.ReadAsync(() => client.GetStateAndETagAsync<SecuritySpoolArchivePage>(target.ComponentName, key, ConsistencyMode.Strong, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
        if (confirmed is null || Digest(confirmed) != digest) { return false; }
        // A competing rollover may have advanced the head while this caller confirmed the same immutable page.
        var (current, currentEtag) = await ReadAsync(target, budget).ConfigureAwait(false);
        if (current is null || currentEtag != etag || StateDigest(current) != StateDigest(state)) { return false; }
        var next = state with { Revision = checked(state.Revision + 1), PageIndex = checked(state.PageIndex + 1),
            ArchivedObservedCount = checked(state.ArchivedObservedCount + state.Records.Count),
            ArchivedAcknowledgedCount = checked(state.ArchivedAcknowledgedCount + state.Records.Count),
            ArchiveHeadDigest = digest, Records = Array.Empty<SecurityObservationRecord>() };
        await TryWriteAsync(target, state, next, etag, budget).ConfigureAwait(false);
        var (saved, _) = await ReadAsync(target, budget).ConfigureAwait(false);
        return saved is not null && StateDigest(saved) == StateDigest(next);
    }
    private async Task<SecurityObservationRecord?> FindArchivedAsync(ReplicatedSecuritySpoolTarget target, SecuritySpoolSnapshot head,
        Func<SecurityObservationRecord, bool> predicate, PrivateOwnerOperationDeadline budget)
    {
        string? expected = head.ArchiveHeadDigest;
        long expectedObserved = head.ArchivedObservedCount;
        SecurityObservationRecord? match = null;
        for (long index = head.PageIndex; index > 0;)
        {
            index--; budget.Check();
            var (page, _) = await budget.ReadAsync(() => client.GetStateAndETagAsync<SecuritySpoolArchivePage>(target.ComponentName,
                ArchiveKey(target, index, expected ?? throw new InvalidOperationException("Missing archive head digest.")), ConsistencyMode.Strong, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
            if (!ValidArchive(page, head, index, expected, expectedObserved, budget)) { throw new InvalidOperationException("Spool archive chain is missing or divergent."); }
            var found = page!.Snapshot.Records.SingleOrDefault(predicate);
            if (found is not null)
            {
                if (match is not null) { throw new InvalidOperationException("Duplicate original in spool archive."); }
                match = found;
            }
            expected = page.PreviousDigest;
            expectedObserved = page.Snapshot.ArchivedObservedCount;
        }
        if (expected is not null || expectedObserved != 0) { throw new InvalidOperationException("Spool archive chain has an unexpected prefix."); }
        return match;
    }
    private async Task<bool> ArchivesCompleteAsync(ReplicatedSecuritySpoolTarget target, SecuritySpoolSnapshot head, PrivateOwnerOperationDeadline budget)
    {
        string? expected = head.ArchiveHeadDigest;
        long expectedObserved = head.ArchivedObservedCount;
        for (long index = head.PageIndex; index > 0;)
        {
            index--; budget.Check();
            var (page, _) = await budget.ReadAsync(() => client.GetStateAndETagAsync<SecuritySpoolArchivePage>(target.ComponentName,
                ArchiveKey(target, index, expected ?? throw new InvalidOperationException("Missing archive head digest.")), ConsistencyMode.Strong, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
            if (!ValidArchive(page, head, index, expected, expectedObserved, budget)) { return false; }
            expected = page!.PreviousDigest;
            expectedObserved = page.Snapshot.ArchivedObservedCount;
        }
        return expected is null && expectedObserved == 0;
    }
    private static bool ValidArchive(SecuritySpoolArchivePage? page, SecuritySpoolSnapshot head, long index, string? expected,
        long expectedObserved, PrivateOwnerOperationDeadline budget)
    {
        if (page is null || page.PageIndex != index || page.Snapshot.InstallationEpoch != head.InstallationEpoch
            || page.Snapshot.PageIndex != index || page.PreviousDigest != page.Snapshot.ArchiveHeadDigest
            || page.Snapshot.Records.Count is < 1 or > RecordBound || page.Snapshot.Records.Any(record => record.Receipt is null)
            || checked(page.Snapshot.ArchivedObservedCount + page.Snapshot.Records.Count) != expectedObserved
            || Digest(page) != expected) { return false; }
        _ = Capture(page.Snapshot, budget); return true;
    }
    private async Task<bool> NextArchiveSlotAvailableAsync(ReplicatedSecuritySpoolTarget target, SecuritySpoolSnapshot head, PrivateOwnerOperationDeadline budget)
    {
        if (head.Records.Count == 0) { return true; }
        var page = new SecuritySpoolArchivePage(head.PageIndex, head, head.ArchiveHeadDigest);
        string digest = Digest(page);
        var (existing, _) = await budget.ReadAsync(() => client.GetStateAndETagAsync<SecuritySpoolArchivePage>(target.ComponentName,
            ArchiveKey(target, head.PageIndex, digest), ConsistencyMode.Strong, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
        return existing is null || Digest(existing) == digest;
    }
    private async Task<(SecuritySpoolSnapshot? State, string Etag)> ReadAsync(ReplicatedSecuritySpoolTarget target, PrivateOwnerOperationDeadline budget, bool recoverAdmittedOriginal = false)
    {
        var (read, etag) = await budget.ReadAsync(() => client.GetStateAndETagAsync<SecuritySpoolSnapshot>(target.ComponentName, Key(target), ConsistencyMode.Strong, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
        var state = read is null ? new(target.InstallationEpoch, 0, Array.Empty<SecurityObservationRecord>()) : Capture(read, budget);
        if (state.InstallationEpoch != target.InstallationEpoch || authority is null) { return (null, etag); }
        var (pending, pendingEtag) = await budget.ReadAsync(() => client.GetStateAndETagAsync<AnchoredStateTransition>(target.ComponentName, PendingKey(target), ConsistencyMode.Strong, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
        var reconciled = await RecoverableAnchoredState.ReconcileAsync(PendingScope(target), state, pending, value => Capture(value, budget),
            value => budget.ReadAsync(() => authority.ValidateStateAsync(target, value.Revision, StateDigest(value), CancellationToken.None)), new DeadlineAnchoredStateAuthority(authority, budget), async value =>
            {
                // CAS keeps both the retained stage and the main predecessor owned by the admitted original operation.
                if (StateDigest(state) != StateDigest(value))
                { _ = await budget.ReadAsync(() => client.TrySaveStateAsync(target.ComponentName, Key(target), value, etag, new StateOptions { Concurrency = ConcurrencyMode.FirstWrite }, cancellationToken: CancellationToken.None)).ConfigureAwait(false); }
                var (durable, durableEtag) = await budget.ReadAsync(() => client.GetStateAndETagAsync<SecuritySpoolSnapshot>(target.ComponentName, Key(target), ConsistencyMode.Strong, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
                if (durable is null || StateDigest(Capture(durable, budget)) != StateDigest(value)) { throw new InvalidOperationException("Prospective spool target is not durable."); }
                etag = durableEtag;
                // Conditional tombstone cannot remove a stage installed by another caller.
                _ = await budget.ReadAsync(() => client.TrySaveStateAsync<AnchoredStateTransition?>(target.ComponentName, PendingKey(target), null, pendingEtag,
                    new StateOptions { Concurrency = ConcurrencyMode.FirstWrite }, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
                // Read the main bytes again after the final persistence await.
                var (final, finalEtag) = await budget.ReadAsync(() => client.GetStateAndETagAsync<SecuritySpoolSnapshot>(target.ComponentName, Key(target), ConsistencyMode.Strong, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
                etag = finalEtag; return final ?? throw new InvalidOperationException("Spool target is missing.");
            }, recoverAdmittedOriginal).ConfigureAwait(false);
        budget.Check(); return ValidTarget(target) ? (reconciled, etag) : (null, etag);
    }
    private async Task TryWriteAsync(ReplicatedSecuritySpoolTarget target, SecuritySpoolSnapshot previous, SecuritySpoolSnapshot next, string etag, PrivateOwnerOperationDeadline budget)
    {
        next = Capture(next, budget); if (authority is null || !ValidTarget(target)) { return; }
        var transition = RecoverableAnchoredState.Prepare(PendingScope(target), previous.Revision, next.Revision, previous, next);
        string pendingEtag = string.Empty;
        _ = await RecoverableAnchoredState.CommitAsync(transition, new DeadlineAnchoredStateAuthority(authority, budget), async () =>
        {
            var (pending, freshEtag) = await budget.ReadAsync(() => client.GetStateAndETagAsync<AnchoredStateTransition>(target.ComponentName, PendingKey(target), ConsistencyMode.Strong, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
            pendingEtag = freshEtag; return pending;
        }, async value =>
        {
            var (latest, latestEtag) = await budget.ReadAsync(() => client.GetStateAndETagAsync<SecuritySpoolSnapshot>(target.ComponentName,
                Key(target), ConsistencyMode.Strong, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
            var predecessor = latest is null ? new SecuritySpoolSnapshot(target.InstallationEpoch, 0, Array.Empty<SecurityObservationRecord>()) : Capture(latest, budget);
            if (latestEtag != etag || StateDigest(predecessor) != StateDigest(previous))
            { throw new InvalidOperationException("Spool predecessor advanced before pending stage."); }
            bool saved = await budget.ReadAsync(() => client.TrySaveStateAsync(target.ComponentName, PendingKey(target), value, pendingEtag,
                new StateOptions { Concurrency = ConcurrencyMode.FirstWrite }, cancellationToken: CancellationToken.None)).ConfigureAwait(false);
            if (!saved) { throw new InvalidOperationException("Another original spool stage won the conditional write."); }
        }).ConfigureAwait(false);
        budget.Check();
    }
    private static SecuritySpoolSnapshot Capture(SecuritySpoolSnapshot source, PrivateOwnerOperationDeadline budget)
    {
        if (source.Revision < 0 || source.Records is null || source.Records.Count > RecordBound) { throw new InvalidOperationException("Malformed private spool state."); }
        Text(source.InstallationEpoch); var owned = new List<SecurityObservationRecord>(); var ids = new HashSet<string>(StringComparer.Ordinal); var receiptKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in source.Records)
        {
            budget.Check(); if (owned.Count >= RecordBound || record is null) { throw new InvalidOperationException("Malformed private spool state."); }
            ValidateIntent(record.Intent);
            if (!ids.Add(record.Intent.ObservationId) || record.Intent.RetainedServerReceiptKey is { } key && !receiptKeys.Add(key) || record.Sequence != checked(source.ArchivedObservedCount + owned.Count + 1L) || record.ObservedAt.Offset != TimeSpan.Zero
                || record.UtcDay != record.ObservedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) || record.Receipt is not null && !Exact(record, record.Receipt))
            { throw new InvalidOperationException("Malformed private spool record."); } owned.Add(record);
        }
        if (source.PageIndex is < 0 or > ArchivePageBound || source.ArchivedObservedCount < source.PageIndex
            || source.ArchivedObservedCount > checked(source.PageIndex * RecordBound)
            || source.ArchivedAcknowledgedCount != source.ArchivedObservedCount
            || source.PageIndex == 0 && source.ArchiveHeadDigest is not null
            || source.PageIndex > 0 && (source.ArchiveHeadDigest is not { Length: 64 } || source.ArchiveHeadDigest.Any(c => !char.IsAsciiHexDigit(c)))
            || source.DrainRevision < 0 || source.DrainAfterSequence < 0 || source.DrainAfterSequence > source.ArchivedObservedCount + owned.Count
            || source.DrainRevision == 0 && source.DrainAfterSequence != 0
            || source.Revision != checked(source.ArchivedObservedCount + owned.Count + source.ArchivedAcknowledgedCount
                + owned.Count(r => r.Receipt is not null) + source.DrainRevision + source.PageIndex))
        { throw new InvalidOperationException("Malformed private spool revision."); }
        return source with { Records = Array.AsReadOnly(owned.ToArray()) };
    }
    // Reserve the worst JSON representation of every future exact receipt before admitting a pending original.
    // The authoritative recorder must return an EventId within the same UTF-8 bound; larger proofs fail closed.
    private static bool FitsWithReceiptReserve(SecuritySpoolSnapshot snapshot, PrivateOwnerOperationDeadline budget, int additionalBytes = 0)
    {
        if (snapshot.Records.Count > RecordBound) { return false; }
        var records = snapshot.Records.Select(record =>
        {
            budget.Check();
            return record.Receipt is not null ? record : record with { Receipt = new SecurityEventRecordReceipt(
                record.Intent.ObservationId, record.Intent.RoutingTenantId, record.UtcDay, IntentDigest(record.Intent),
                SourceStream(record), long.MaxValue, MaximumEscapedEventId) };
        }).ToArray();
        var upper = snapshot with { Revision = long.MaxValue, DrainRevision = long.MaxValue, DrainAfterSequence = long.MaxValue, Records = records };
        budget.Check();
        return JsonSerializer.SerializeToUtf8Bytes(upper).Length + additionalBytes <= RecoverableAnchoredState.MaximumPendingBytes;
    }
    private static bool ValidReceiptEventId(string? eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId) || eventId.Length > ReceiptEventIdByteBound) { return false; }
        try { return StrictUtf8.GetByteCount(eventId) <= ReceiptEventIdByteBound; }
        catch (System.Text.EncoderFallbackException) { return false; }
    }
    private static bool Exact(SecurityObservationRecord record, SecurityEventRecordReceipt? receipt) => receipt is not null
        && receipt.ObservationId == record.Intent.ObservationId && receipt.RoutingTenantId == record.Intent.RoutingTenantId && receipt.UtcDay == record.UtcDay
        && receipt.OriginalIntentDigest == IntentDigest(record.Intent) && receipt.SourceStreamId == SourceStream(record) && receipt.SourceRevision > 0 && ValidReceiptEventId(receipt.EventId);
    private static void ValidateIntent(SecurityObservationIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent); foreach (string field in new[] { intent.ObservationId, intent.RoutingTenantId, intent.DigestKeyVersion }) { Text(field); }
        if (intent.RetainedServerReceiptKey is not null) { Text(intent.RetainedServerReceiptKey); }
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
