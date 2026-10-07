using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Synthetic retained-unit inventory and receipts; never a production crypto or restore provider.</summary>
public sealed class IdentityHistoryCleanupFixture(CustodyFixtureClock clock) : IIdentityHistoryCustody
{
    private readonly Dictionary<(string Identity, string EvidenceId), IdentityHistoryCustodyEvidence> _inventory = [];
    private readonly HashSet<(string Identity, string EvidenceId)> _destroyed = [];

    /// <summary>Gets the original identity/evidence supplied by every cleanup attempt.</summary>
    public List<(AggregateIdentity Identity, IdentityHistoryCustodyEvidence Evidence)> Attempts { get; } = [];

    /// <summary>Gets the fresh read check count.</summary>
    public int ReadChecks { get; private set; }

    /// <summary>Gets or sets a provider-failure hook.</summary>
    public Func<AggregateIdentity, IdentityHistoryCustodyEvidence, CancellationToken, Task<bool>>? DestructionHook { get; set; }

    /// <summary>Gets or sets a fresh lifecycle-failure hook.</summary>
    public Func<CancellationToken, Task<bool>>? ReadHook { get; set; }

    /// <summary>Registers a synthetic exact scope and immutable receipt.</summary>
    public void Register(AggregateIdentity identity, IdentityHistoryCustodyEvidence evidence)
        => _inventory.Add((identity.ActorId, evidence.EvidenceId), evidence);

    /// <summary>Gets synthetic inventory end-state for isolation and lost-acknowledgement assertions.</summary>
    public bool IsDestroyed(AggregateIdentity identity, IdentityHistoryCustodyEvidence evidence)
        => _destroyed.Contains((identity.ActorId, evidence.EvidenceId));

    /// <summary>Completes an exact synthetic unit once; repeated calls recover its same receipt.</summary>
    public bool CompleteDestruction(AggregateIdentity identity, IdentityHistoryCustodyEvidence evidence)
    {
        var key = (identity.ActorId, evidence.EvidenceId);
        if (!_inventory.TryGetValue(key, out IdentityHistoryCustodyEvidence? expected)
            || expected != evidence || clock.Now < expected.ExpiresAt)
        {
            return false;
        }

        _destroyed.Add(key);
        return true;
    }

    /// <inheritdoc/>
    public Task<bool> DestroyExpiredAsync(AggregateIdentity identity, IdentityHistoryCustodyEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        Attempts.Add((identity, evidence));
        return DestructionHook is { } hook ? hook(identity, evidence, cancellationToken)
            : Task.FromResult(CompleteDestruction(identity, evidence));
    }

    /// <inheritdoc/>
    public Task<bool> CanReadAsync(AggregateIdentity identity, IdentityHistoryCustodyEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ReadChecks++;
        var key = (identity.ActorId, evidence.EvidenceId);
        return ReadHook is { } hook ? hook(cancellationToken) : Task.FromResult(
            _inventory.TryGetValue(key, out IdentityHistoryCustodyEvidence? expected) && expected == evidence
            && !_destroyed.Contains(key) && clock.Now < expected.ExpiresAt);
    }

    /// <inheritdoc/>
    public Task<IdentityHistoryCustodyEvidence?> AdmitAsync(AggregateIdentity identity, IdentityHistoryPolicy policy,
        DateTimeOffset effectiveAt, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<PayloadProtectionResult> ProtectEventAsync(AggregateIdentity identity, string eventType, byte[] payload,
        string format, IdentityHistoryCustodyEvidence evidence, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<PayloadProtectionResult> UnprotectEventAsync(AggregateIdentity identity, string eventType, byte[] payload,
        string format, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<object> ProtectSnapshotAsync(AggregateIdentity identity, object snapshot,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<object?> UnprotectSnapshotAsync(AggregateIdentity identity, object snapshot,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
