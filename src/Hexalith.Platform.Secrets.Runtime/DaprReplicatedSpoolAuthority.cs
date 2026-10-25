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
