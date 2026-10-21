using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>In-process nonrollback head. A refused call must not increment <see cref="Writes"/>.</summary>
internal interface ICustodyNonrollbackJournal
{
    /// <summary>Gets the current revision. Admit and recover leave it unchanged.</summary>
    long Head { get; }

    /// <summary>Gets the digest of the state at <see cref="Head"/>.</summary>
    string HeadDigest { get; }

    /// <summary>Gets the retained admitted original, or null before the first admit.</summary>
    AnchoredStateTransition? Admitted { get; }

    /// <summary>Gets the number of admits, records, and recovers that changed the journal.</summary>
    int Writes { get; }

    /// <summary>Retains an exact original at the current head. An exact retry does not write again. Admission does not advance the head.</summary>
    bool TryAdmit(AnchoredStateTransition transition);

    /// <summary>Advances one revision when the transition is the admitted original at the current head.</summary>
    bool TryRecord(AnchoredStateTransition transition);

    /// <summary>Marks the admitted original at the unchanged predecessor. This does not restore a destroyed key and does not move the head.</summary>
    bool TryRecover(AnchoredStateTransition transition);

    /// <summary>Reads the admitted original after recover or record. It does not write.</summary>
    bool Verify(AnchoredStateTransition transition);
}
