using System.Security.Cryptography;

using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Admit retains, record advances one revision, recover marks that original without moving the head, and verify reads.
/// This is not a local restore of a destroyed key. A refused call does not increment the write counter.</summary>
internal sealed class CustodyNonrollbackJournal : ICustodyNonrollbackJournal
{
    private long _head;
    private string _headDigest;
    private AnchoredStateTransition? _admitted;
    private bool _recovered;
    private bool _recorded;
    private int _writes;

    /// <summary>Starts at an exact predecessor. Nothing has been admitted.</summary>
    /// <exception cref="ArgumentException">The head revision or digest is not exact.</exception>
    internal CustodyNonrollbackJournal(long head, string headDigest)
    {
        if (head < 0 || !CustodyBindingContract.Digest(headDigest))
        {
            throw new ArgumentException("Custody journal head is not an exact revision.");
        }

        _head = head;
        _headDigest = headDigest;
    }

    /// <inheritdoc/>
    public long Head => _head;

    /// <inheritdoc/>
    public string HeadDigest => _headDigest;

    /// <inheritdoc/>
    public AnchoredStateTransition? Admitted => _admitted is null ? null : Copy(_admitted);

    /// <inheritdoc/>
    public int Writes => _writes;

    /// <inheritdoc/>
    public bool TryAdmit(AnchoredStateTransition transition)
    {
        if (!Current(transition) || !WellFormed(transition)) { return false; }
        if (_admitted is not null) { return Same(_admitted, transition); }
        _admitted = Copy(transition);
        _writes++;
        return true;
    }

    /// <inheritdoc/>
    public bool TryRecord(AnchoredStateTransition transition)
    {
        if (!Current(transition) || _admitted is null || !Same(_admitted, transition)) { return false; }
        _head = transition.TargetRevision;
        _headDigest = transition.TargetDigest;
        _recorded = true;
        _writes++;
        return true;
    }

    /// <inheritdoc/>
    public bool TryRecover(AnchoredStateTransition transition)
    {
        if (!Current(transition) || _admitted is null || !Same(_admitted, transition)) { return false; }
        if (_recovered) { return true; }
        _recovered = true;
        _writes++;
        return true;
    }

    /// <inheritdoc/>
    public bool Verify(AnchoredStateTransition transition)
        => _admitted is not null && Same(_admitted, transition) && (_recovered || _recorded);

    private bool Current(AnchoredStateTransition? transition)
        => transition is not null && transition.ExpectedRevision == _head && transition.PredecessorDigest == _headDigest;

    private static bool WellFormed(AnchoredStateTransition transition)
    {
        if (!CustodyBindingContract.Text(transition.ScopeId, 4096) || transition.ExpectedRevision < 0 || transition.ExpectedRevision == long.MaxValue
            || transition.TargetRevision != transition.ExpectedRevision + 1 || transition.TargetBytes is not { Length: > 0 }
            || !CustodyBindingContract.Digest(transition.PredecessorDigest) || !CustodyBindingContract.Digest(transition.TargetDigest))
        {
            return false;
        }

        return Convert.ToHexString(SHA256.HashData(transition.TargetBytes)) == transition.TargetDigest;
    }

    private static bool Same(AnchoredStateTransition left, AnchoredStateTransition right)
        => left.ScopeId == right.ScopeId && left.ExpectedRevision == right.ExpectedRevision && left.TargetRevision == right.TargetRevision
            && left.PredecessorDigest == right.PredecessorDigest && left.TargetDigest == right.TargetDigest
            && left.TargetBytes.AsSpan().SequenceEqual(right.TargetBytes);

    private static AnchoredStateTransition Copy(AnchoredStateTransition transition)
        => transition with { TargetBytes = transition.TargetBytes.ToArray() };
}
