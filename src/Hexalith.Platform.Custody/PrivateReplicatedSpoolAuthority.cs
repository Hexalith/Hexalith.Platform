using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Supplied spool credential for observe, drain, and aged recovery. A null, endpoint-only, or stale credential is not current authority.</summary>
public sealed class PrivateReplicatedSpoolAuthority : IReplicatedSecuritySpoolAuthority
{
    private readonly string _credential;
    private readonly ReplicatedSecuritySpoolTarget _target;
    private readonly TimeProvider _clock;
    private readonly bool _current;
    private readonly Dictionary<string, string> _admissions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _proofs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _grants = new(StringComparer.Ordinal);
    private long _anchorRevision;
    private string _anchorDigest;

    private PrivateReplicatedSpoolAuthority(string credential, ReplicatedSecuritySpoolTarget target, TimeProvider clock, string anchorDigest, bool current)
    {
        _credential = credential;
        _target = target;
        _clock = clock;
        _anchorDigest = anchorDigest;
        _current = current;
    }

    /// <summary>Captures one credential beside one current installation. An absent credential, an endpoint, or a non-positive binding captures nothing.</summary>
    public static PrivateReplicatedSpoolAuthority? Create(string? credential, ReplicatedSecuritySpoolTarget? target, TimeProvider? clock)
    {
        if (target is null || clock is null || !Credential(credential) || string.Equals(credential, target.ComponentName, StringComparison.Ordinal)
            || IsEndpoint(credential!) || target.ValidUntil.Offset != TimeSpan.Zero || clock.GetUtcNow() >= target.ValidUntil
            || string.IsNullOrWhiteSpace(target.AuthorityRevision)) { return null; }
        string digest = ReplicatedSecurityObservationSpool.StateDigest(new(target.InstallationEpoch, 0, []));
        return new(credential!, target, clock, digest, true);
    }

    /// <summary>Same installation with current permission withdrawn. It admits no observation, drain, or recovery.</summary>
    public PrivateReplicatedSpoolAuthority AsStale() => new(_credential, _target, _clock, _anchorDigest, false);

    /// <inheritdoc/>
    public Task<ReplicatedSecuritySpoolTarget?> GetCurrentAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_current && _clock.GetUtcNow() < _target.ValidUntil ? _target : null);

    /// <inheritdoc/>
    public Task<bool> AuthorizeAsync(ReplicatedSecuritySpoolTarget target, string method, SecurityObservationIntent? intent, CancellationToken cancellationToken = default)
    {
        if (!Matches(target) || method is not ("Observe" or "Lookup" or "Drain" or "Readiness" or "RecoverOriginal")) { return Task.FromResult(false); }
        if (method is "Observe" or "Lookup" or "RecoverOriginal" && intent is null) { return Task.FromResult(false); }
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> AuthorizeOriginalLookupAsync(ReplicatedSecuritySpoolTarget target, SecurityObservationOriginalLookup lookup, CancellationToken cancellationToken = default)
        => Task.FromResult(Matches(target) && lookup is not null);

    /// <inheritdoc/>
    public Task<SecurityObservationRecoveryGrant?> AuthorizeAgedOriginalRecoveryAsync(ReplicatedSecuritySpoolTarget target, SecurityObservationRecord original, SecurityEventRecorderLookup proof, CancellationToken cancellationToken = default)
    {
        if (!Matches(target) || original is null || original.Receipt is not null || proof is null || proof.State == SecurityEventRecorderLookupState.Unknown) { return Task.FromResult<SecurityObservationRecoveryGrant?>(null); }
        if (proof.State == SecurityEventRecorderLookupState.NotRecorded && proof.Receipt is not null) { return Task.FromResult<SecurityObservationRecoveryGrant?>(null); }
        if (proof.State == SecurityEventRecorderLookupState.Recorded && proof.Receipt is null) { return Task.FromResult<SecurityObservationRecoveryGrant?>(null); }
        DateTimeOffset now = _clock.GetUtcNow();
        if (now.Offset != TimeSpan.Zero || now >= target.ValidUntil) { return Task.FromResult<SecurityObservationRecoveryGrant?>(null); }
        DateTimeOffset until = now.AddMinutes(5);
        if (until > target.ValidUntil) { until = target.ValidUntil; }
        if (until <= now) { return Task.FromResult<SecurityObservationRecoveryGrant?>(null); }
        var grant = new SecurityObservationRecoveryGrant(target, original, proof, _target.AuthorityRevision, now, until);
        _grants.Add(JsonSerializer.Serialize(grant));
        return Task.FromResult<SecurityObservationRecoveryGrant?>(grant);
    }

    /// <inheritdoc/>
    public Task<bool> VerifyAgedOriginalRecoveryAsync(SecurityObservationRecoveryGrant grant, SecurityEventRecorderLookup currentProof, CancellationToken cancellationToken = default)
    {
        if (!_current || grant is null || currentProof is null || currentProof.State == SecurityEventRecorderLookupState.Unknown || grant.Target != _target
            || grant.RecoveryAuthorityRevision != _target.AuthorityRevision || grant.ValidUntil <= _clock.GetUtcNow() || !_grants.Contains(JsonSerializer.Serialize(grant)))
        { return Task.FromResult(false); }
        bool proof = currentProof.State == SecurityEventRecorderLookupState.NotRecorded && currentProof.Receipt is null
            || currentProof.State == SecurityEventRecorderLookupState.Recorded && currentProof.Receipt is not null;
        bool progressed = grant.OriginalProof.State == SecurityEventRecorderLookupState.NotRecorded && currentProof.State == SecurityEventRecorderLookupState.Recorded;
        return Task.FromResult(proof && (currentProof.State == grant.OriginalProof.State || progressed));
    }

    /// <inheritdoc/>
    public Task<bool> RecordRevisionAsync(ReplicatedSecuritySpoolTarget target, long expectedRevision, long nextRevision, string safeStateDigest, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<bool> ValidateStateAsync(ReplicatedSecuritySpoolTarget target, long revision, string exactStateDigest, CancellationToken cancellationToken = default)
        => Task.FromResult(Matches(target) && revision == _anchorRevision && exactStateDigest == _anchorDigest);

    /// <inheritdoc/>
    public Task<bool> AdmitTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        if (!Matches(_target) || transition is null) { return Task.FromResult(false); }
        string exact = JsonSerializer.Serialize(transition);
        if (_admissions.TryGetValue(transition.TargetDigest, out string? admitted)) { return Task.FromResult(admitted == exact); }
        if (!Predecessor(transition)) { return Task.FromResult(false); }
        _admissions.Add(transition.TargetDigest, exact);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> RecoverTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        if (transition is null || !_admissions.TryGetValue(transition.TargetDigest, out string? admitted) || admitted != JsonSerializer.Serialize(transition))
        { return Task.FromResult(false); }
        return RecordTransitionAsync(transition, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<bool> RecordTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
    {
        if (!Matches(_target) || transition is null || !Predecessor(transition) || Convert.ToHexString(SHA256.HashData(transition.TargetBytes)) != transition.TargetDigest)
        { return Task.FromResult(false); }
        _proofs[transition.TargetDigest] = JsonSerializer.SerializeToUtf8Bytes(transition);
        _anchorRevision = transition.TargetRevision;
        _anchorDigest = transition.TargetDigest;
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> VerifyTransitionAsync(AnchoredStateTransition transition, CancellationToken cancellationToken = default)
        => Task.FromResult(transition is not null && transition.ScopeId == Scope && _proofs.TryGetValue(transition.TargetDigest, out byte[]? proof)
            && proof.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(transition)));

    private bool Matches(ReplicatedSecuritySpoolTarget target)
        => _current && target == _target && target.AuthorityRevision == _target.AuthorityRevision && _clock.GetUtcNow() < _target.ValidUntil;

    private bool Predecessor(AnchoredStateTransition transition)
        => transition.ScopeId == Scope && transition.ExpectedRevision == _anchorRevision && transition.TargetRevision == _anchorRevision + 1 && transition.PredecessorDigest == _anchorDigest;

    private string Scope => _target.ComponentName + "|system/security-observations/" + _target.InstallationEpoch;

    private static bool Credential(string? value)
    {
        try { return !string.IsNullOrWhiteSpace(value) && value.Length <= 2048 && new UTF8Encoding(false, true).GetByteCount(value) <= 2048; }
        catch (EncoderFallbackException) { return false; }
    }

    private static bool IsEndpoint(string value)
    {
        if (value.Contains("://", StringComparison.Ordinal)) { return true; }
        if (value.StartsWith("file:/", StringComparison.Ordinal)) { return false; }
        int colon = value.IndexOf(':');
        return colon > 0 && colon < value.Length - 1 && value[(colon + 1)..].All(char.IsDigit);
    }
}
