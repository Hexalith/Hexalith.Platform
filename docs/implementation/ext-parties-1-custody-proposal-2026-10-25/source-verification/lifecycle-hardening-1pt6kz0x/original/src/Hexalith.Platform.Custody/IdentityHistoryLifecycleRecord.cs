namespace Hexalith.Platform.Custody;

/// <summary>
/// Forward-only lifecycle authority for one immutable unit. Lifecycle revision, epoch, and fence stay separate from admission.
/// </summary>
public sealed class IdentityHistoryLifecycleRecord : IEquatable<IdentityHistoryLifecycleRecord>
{
    /// <summary>Creates a consistent lifecycle snapshot for one unit.</summary>
    /// <param name="unit">Immutable custody unit.</param>
    /// <param name="state">Lifecycle state.</param>
    /// <param name="lifecycleRevision">Current lifecycle revision.</param>
    /// <param name="epoch">Current authority epoch.</param>
    /// <param name="fence">Current authority fence.</param>
    /// <param name="operationId">Destruction operation identifier, present only from pending destruction onward.</param>
    /// <param name="receiptReference">Original receipt reference, present only at receipt final.</param>
    /// <param name="generations">Key generations already grown on this unit.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="unit"/> or <paramref name="generations"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the state, authority, operation, receipt, or generations are inconsistent.</exception>
    public IdentityHistoryLifecycleRecord(
        IdentityHistoryCustodyUnit unit,
        IdentityHistoryLifecycleState state,
        long lifecycleRevision,
        long epoch,
        long fence,
        string? operationId,
        string? receiptReference,
        IReadOnlyList<long> generations)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(generations);
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentException("Actor-history lifecycle state is unknown.", nameof(state));
        }

        if (lifecycleRevision <= 0)
        {
            throw new ArgumentException("Lifecycle revision must be a positive current revision.", nameof(lifecycleRevision));
        }

        if (epoch < 0)
        {
            throw new ArgumentException("Authority epoch cannot be negative.", nameof(epoch));
        }

        if (fence < 0)
        {
            throw new ArgumentException("Authority fence cannot be negative.", nameof(fence));
        }

        bool destructionStarted = state is IdentityHistoryLifecycleState.PendingDestruction
            or IdentityHistoryLifecycleState.Destroyed
            or IdentityHistoryLifecycleState.ReceiptFinal;
        if (destructionStarted)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        }
        else if (operationId is not null)
        {
            throw new ArgumentException("Operation id starts at PendingDestruction.", nameof(operationId));
        }

        if (state == IdentityHistoryLifecycleState.ReceiptFinal)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(receiptReference);
        }
        else if (receiptReference is not null)
        {
            throw new ArgumentException("A receipt reference exists only at ReceiptFinal.", nameof(receiptReference));
        }

        long[] generationCopy = new long[generations.Count];
        long previous = -1;
        for (int index = 0; index < generations.Count; index++)
        {
            long generation = generations[index];
            if (generation < 0 || generation <= previous)
            {
                throw new ArgumentException("Generations grow only on the same unit and deadline.", nameof(generations));
            }

            generationCopy[index] = generation;
            previous = generation;
        }

        Unit = unit;
        State = state;
        LifecycleRevision = lifecycleRevision;
        Epoch = epoch;
        Fence = fence;
        OperationId = operationId;
        ReceiptReference = receiptReference;
        Generations = Array.AsReadOnly(generationCopy);
    }

    /// <summary>Gets the immutable unit. Its evidence, admission revision, and deadline stay fixed.</summary>
    public IdentityHistoryCustodyUnit Unit { get; }

    /// <summary>Gets the lifecycle state.</summary>
    public IdentityHistoryLifecycleState State { get; }

    /// <summary>Gets the current lifecycle revision.</summary>
    public long LifecycleRevision { get; }

    /// <summary>Gets the current authority epoch.</summary>
    public long Epoch { get; }

    /// <summary>Gets the current authority fence.</summary>
    public long Fence { get; }

    /// <summary>Gets the destruction operation identifier.</summary>
    public string? OperationId { get; }

    /// <summary>Gets the original receipt reference.</summary>
    public string? ReceiptReference { get; }

    /// <summary>Gets generations grown on this unit.</summary>
    public IReadOnlyList<long> Generations { get; }

    /// <summary>Moves one legal step forward at a higher lifecycle revision.</summary>
    /// <param name="state">Next lifecycle state.</param>
    /// <param name="lifecycleRevision">Higher current lifecycle revision.</param>
    /// <param name="epoch">Authority epoch for the next snapshot.</param>
    /// <param name="fence">Authority fence for the next snapshot.</param>
    /// <param name="operationId">Original destruction operation identifier when destruction has started.</param>
    /// <param name="receiptReference">Original receipt reference when the state is receipt final.</param>
    /// <returns>The next snapshot of the same unit and deadline.</returns>
    /// <exception cref="ArgumentException">Thrown when the move is reverse, skipped, or not a higher revision.</exception>
    public IdentityHistoryLifecycleRecord Move(
        IdentityHistoryLifecycleState state,
        long lifecycleRevision,
        long epoch,
        long fence,
        string? operationId,
        string? receiptReference)
    {
        if (lifecycleRevision <= LifecycleRevision)
        {
            throw new ArgumentException("A forward lifecycle move requires a higher revision.", nameof(lifecycleRevision));
        }

        if (!CanMove(State, state))
        {
            throw new ArgumentException("Actor-history lifecycle cannot make that transition.", nameof(state));
        }

        if (OperationId is not null && !string.Equals(operationId, OperationId, StringComparison.Ordinal))
        {
            throw new ArgumentException("The original destruction operation id cannot be replaced.", nameof(operationId));
        }

        return new IdentityHistoryLifecycleRecord(
            Unit,
            state,
            lifecycleRevision,
            epoch,
            fence,
            operationId,
            receiptReference,
            Generations);
    }

    /// <summary>Grows one generation on the same unit and deadline. A repeated generation does not mint a new revision.</summary>
    /// <param name="unit">Unit that must be the original unit.</param>
    /// <param name="generation">Generation number to append.</param>
    /// <returns>The same snapshot when the generation is already present; otherwise the grown snapshot.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="unit"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the generation is on a different unit or does not grow.</exception>
    public IdentityHistoryLifecycleRecord AddGeneration(IdentityHistoryCustodyUnit unit, long generation)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (!Unit.Equals(unit))
        {
            throw new ArgumentException("Generations grow only on the same unit and deadline.", nameof(unit));
        }

        if (generation < 0)
        {
            throw new ArgumentException("Generations grow only on the same unit and deadline.", nameof(generation));
        }

        if (Generations.Contains(generation))
        {
            return this;
        }

        if (Generations.Count > 0 && generation <= Generations[^1])
        {
            throw new ArgumentException("Generations grow only on the same unit and deadline.", nameof(generation));
        }

        long[] grown = new long[Generations.Count + 1];
        for (int index = 0; index < Generations.Count; index++)
        {
            grown[index] = Generations[index];
        }

        grown[^1] = generation;
        return new IdentityHistoryLifecycleRecord(Unit, State, LifecycleRevision, Epoch, Fence, OperationId, ReceiptReference, grown);
    }

    /// <summary>Compares lifecycle facts, authority, and generation order.</summary>
    /// <param name="other">Other lifecycle snapshot.</param>
    /// <returns><see langword="true"/> when every fact matches.</returns>
    public bool Equals(IdentityHistoryLifecycleRecord? other)
    {
        if (other is null)
        {
            return false;
        }

        return State == other.State
            && LifecycleRevision == other.LifecycleRevision
            && Epoch == other.Epoch
            && Fence == other.Fence
            && OperationId == other.OperationId
            && ReceiptReference == other.ReceiptReference
            && Unit.Equals(other.Unit)
            && Generations.SequenceEqual(other.Generations);
    }

    /// <summary>Compares lifecycle facts, authority, and generation order.</summary>
    /// <param name="obj">Other object.</param>
    /// <returns><see langword="true"/> when the object is an equal lifecycle snapshot.</returns>
    public override bool Equals(object? obj) => obj is IdentityHistoryLifecycleRecord other && Equals(other);

    /// <summary>Gets a hash code for the lifecycle facts.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(State);
        hash.Add(LifecycleRevision);
        hash.Add(Epoch);
        hash.Add(Fence);
        hash.Add(OperationId);
        hash.Add(ReceiptReference);
        hash.Add(Unit);
        foreach (long generation in Generations)
        {
            hash.Add(generation);
        }

        return hash.ToHashCode();
    }

    private static bool CanMove(IdentityHistoryLifecycleState from, IdentityHistoryLifecycleState to)
        => (from, to) switch
        {
            (IdentityHistoryLifecycleState.Active, IdentityHistoryLifecycleState.Expired) => true,
            (IdentityHistoryLifecycleState.Active, IdentityHistoryLifecycleState.PendingDestruction) => true,
            (IdentityHistoryLifecycleState.Expired, IdentityHistoryLifecycleState.PendingDestruction) => true,
            (IdentityHistoryLifecycleState.PendingDestruction, IdentityHistoryLifecycleState.Destroyed) => true,
            (IdentityHistoryLifecycleState.Destroyed, IdentityHistoryLifecycleState.ReceiptFinal) => true,
            _ => false,
        };
}
