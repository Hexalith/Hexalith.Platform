using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Durable independent spool authority. Every transition/grant/original lives at the enrolled owner outside the app restore cut.</summary>
public sealed class DaprReplicatedSpoolAuthority(IndependentDaprAuthority owner) : IReplicatedSecuritySpoolAuthority
{
    private async Task<bool> Check(string method, object request, CancellationToken token)
        => await owner.InvokeAsync<Decision>(method, request, token).ConfigureAwait(false) is { Allowed: true };
    /// <inheritdoc />
    public Task<ReplicatedSecuritySpoolTarget?> GetCurrentAsync(CancellationToken cancellationToken = default)
        => owner.InvokeAsync<ReplicatedSecuritySpoolTarget>("spool-current", new { }, cancellationToken);
    /// <inheritdoc />
    public Task<bool> AuthorizeAsync(ReplicatedSecuritySpoolTarget target, string method, SecurityObservationIntent? intent, CancellationToken cancellationToken = default)
        => method is "Observe" or "Lookup" or "Drain" or "Readiness" or "RecoverOriginal"
            ? Check("spool-authorize", new { target, method, intent }, cancellationToken) : Task.FromResult(false);
    /// <inheritdoc />
    public Task<bool> AuthorizeOriginalLookupAsync(ReplicatedSecuritySpoolTarget target, SecurityObservationOriginalLookup lookup, CancellationToken cancellationToken = default)
        => Check("spool-authorize-original", new { target, lookup }, cancellationToken);
    /// <inheritdoc />
    public Task<SecurityObservationRecoveryGrant?> AuthorizeAgedOriginalRecoveryAsync(ReplicatedSecuritySpoolTarget target, SecurityObservationRecord original, SecurityEventRecorderLookup proof, CancellationToken cancellationToken = default)
        => proof.State == SecurityEventRecorderLookupState.Unknown ? Task.FromResult<SecurityObservationRecoveryGrant?>(null)
            : owner.InvokeAsync<SecurityObservationRecoveryGrant>("spool-admit-recovery", new { target, original, proof }, cancellationToken);
    /// <inheritdoc />
    public Task<bool> VerifyAgedOriginalRecoveryAsync(SecurityObservationRecoveryGrant grant, SecurityEventRecorderLookup currentProof, CancellationToken cancellationToken = default)
        => currentProof.State == SecurityEventRecorderLookupState.Unknown ? Task.FromResult(false)
            : Check("spool-verify-recovery", new { grant, currentProof }, cancellationToken);
    /// <inheritdoc />
    public Task<bool> ValidateStateAsync(ReplicatedSecuritySpoolTarget target, long revision, string exactStateDigest, CancellationToken cancellationToken = default)
        => Check("spool-validate", new { target, revision, exactStateDigest }, cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecordRevisionAsync(ReplicatedSecuritySpoolTarget target, long expectedRevision, long nextRevision, string safeStateDigest, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
    /// <inheritdoc />
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => Check("spool-admit-transition", transition, cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => Check("spool-record-transition", transition, cancellationToken);
    /// <inheritdoc />
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => Check("spool-recover-transition", transition, cancellationToken);
    /// <inheritdoc />
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => Check("spool-verify-transition", transition, cancellationToken);
    private sealed record Decision(bool Allowed);
}

/// <summary>Exact private recorder transport. The independent receiver owns stream/event ACL enforcement and authoritative lookup.</summary>
public sealed class DaprSecurityObservationRecorder(IndependentDaprAuthority recorder) : ISecurityObservationRecorder
{
    /// <inheritdoc />
    public async Task<SecurityEventRecorderLookup> LookupAsync(SecurityObservationRecord record, CancellationToken cancellationToken = default)
    {
        var result = await recorder.InvokeAsync<OriginalResult>("security-observation-lookup", new { record }, cancellationToken).ConfigureAwait(false);
        return result is not null && result.OriginalSha256 == Digest(record) ? result.Lookup : new(SecurityEventRecorderLookupState.Unknown);
    }
    /// <inheritdoc />
    public async Task AppendAsync(SecurityObservationRecord record, CancellationToken cancellationToken = default)
    {
        var result = await recorder.InvokeAsync<OriginalResult>("security-observation-append", new { record }, cancellationToken).ConfigureAwait(false);
        if (result is null || result.OriginalSha256 != Digest(record) || result.Lookup.State == SecurityEventRecorderLookupState.Unknown)
            throw new InvalidOperationException("Exact recorder outcome unknown; lookup original before recovery.");
    }
    /// <inheritdoc />
    public async Task RecoverOriginalAsync(SecurityObservationRecord original, SecurityObservationRecoveryGrant grant, CancellationToken cancellationToken = default)
    {
        if (grant.Original != original) throw new InvalidOperationException("Changed original recovery denied.");
        var result = await recorder.InvokeAsync<OriginalResult>("security-observation-recover-original", new { original, grant }, cancellationToken).ConfigureAwait(false);
        if (result is null || result.OriginalSha256 != Digest(original) || result.Lookup.State == SecurityEventRecorderLookupState.Unknown)
            throw new InvalidOperationException("Exact original recovery remains unknown.");
    }
    private static string Digest(SecurityObservationRecord record) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(record, CustodyBindingContract.Json)));
    private sealed record OriginalResult(string OriginalSha256, SecurityEventRecorderLookup Lookup);
}
