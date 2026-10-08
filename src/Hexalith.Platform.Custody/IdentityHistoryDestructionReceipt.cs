namespace Hexalith.Platform.Custody;

/// <summary>Content-free receipt of the original unit, authority, inventory generation, and class outcomes.</summary>
public sealed class IdentityHistoryDestructionReceipt : IEquatable<IdentityHistoryDestructionReceipt>
{
    /// <summary>Creates a receipt that keeps the original facts and expiry.</summary>
    /// <param name="unit">Original custody unit.</param>
    /// <param name="operationId">Stable destruction operation identifier.</param>
    /// <param name="lifecycleRevision">Terminal lifecycle revision.</param>
    /// <param name="epoch">Terminal authority epoch.</param>
    /// <param name="fence">Terminal authority fence.</param>
    /// <param name="inventoryGeneration">Covered inventory generation.</param>
    /// <param name="outcomes">Unique opaque class outcomes.</param>
    /// <param name="confirmedAt">Confirmation instant.</param>
    /// <param name="expiresAt">Deadline that must equal the original unit deadline.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="unit"/>, <paramref name="outcomes"/>, or an outcome is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the receipt facts, outcomes, or deadline are invalid.</exception>
    public IdentityHistoryDestructionReceipt(
        IdentityHistoryCustodyUnit unit,
        string operationId,
        long lifecycleRevision,
        long epoch,
        long fence,
        long inventoryGeneration,
        IReadOnlyList<IdentityHistoryClassOutcome> outcomes,
        DateTimeOffset confirmedAt,
        DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(outcomes);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (lifecycleRevision <= 0)
        {
            throw new ArgumentException("Receipt lifecycle revision must be positive.", nameof(lifecycleRevision));
        }

        if (epoch < 0)
        {
            throw new ArgumentException("Receipt authority epoch cannot be negative.", nameof(epoch));
        }

        if (fence < 0)
        {
            throw new ArgumentException("Receipt authority fence cannot be negative.", nameof(fence));
        }

        if (inventoryGeneration < 0)
        {
            throw new ArgumentException("Inventory generation must be zero or positive.", nameof(inventoryGeneration));
        }

        if (outcomes.Count == 0)
        {
            throw new ArgumentException("A receipt needs at least one class outcome.", nameof(outcomes));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var copy = new IdentityHistoryClassOutcome[outcomes.Count];
        for (int index = 0; index < outcomes.Count; index++)
        {
            IdentityHistoryClassOutcome outcome = outcomes[index] ?? throw new ArgumentNullException(nameof(outcomes));
            if (!seen.Add(outcome.CopyClass))
            {
                throw new ArgumentException("Receipt class outcomes must be unique.", nameof(outcomes));
            }

            copy[index] = outcome;
        }

        DateTimeOffset deadline = expiresAt.ToUniversalTime();
        if (deadline != unit.ExpiresAt)
        {
            throw new ArgumentException("A receipt keeps the original expiry.", nameof(expiresAt));
        }

        Unit = unit;
        OperationId = operationId;
        LifecycleRevision = lifecycleRevision;
        Epoch = epoch;
        Fence = fence;
        InventoryGeneration = inventoryGeneration;
        Outcomes = Array.AsReadOnly(copy);
        ConfirmedAt = confirmedAt.ToUniversalTime();
        ExpiresAt = deadline;
    }

    /// <summary>Gets the original unit facts.</summary>
    public IdentityHistoryCustodyUnit Unit { get; }

    /// <summary>Gets the stable destruction operation identifier.</summary>
    public string OperationId { get; }

    /// <summary>Gets the terminal lifecycle revision.</summary>
    public long LifecycleRevision { get; }

    /// <summary>Gets the terminal authority epoch.</summary>
    public long Epoch { get; }

    /// <summary>Gets the terminal authority fence.</summary>
    public long Fence { get; }

    /// <summary>Gets the covered inventory generation.</summary>
    public long InventoryGeneration { get; }

    /// <summary>Gets the unique opaque class outcomes.</summary>
    public IReadOnlyList<IdentityHistoryClassOutcome> Outcomes { get; }

    /// <summary>Gets the UTC confirmation instant.</summary>
    public DateTimeOffset ConfirmedAt { get; }

    /// <summary>Gets the original UTC expiry.</summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>Compares the original facts, outcomes, and expiry.</summary>
    /// <param name="other">Other receipt.</param>
    /// <returns><see langword="true"/> when every fact matches.</returns>
    public bool Equals(IdentityHistoryDestructionReceipt? other)
    {
        if (other is null)
        {
            return false;
        }

        return OperationId == other.OperationId
            && LifecycleRevision == other.LifecycleRevision
            && Epoch == other.Epoch
            && Fence == other.Fence
            && InventoryGeneration == other.InventoryGeneration
            && ConfirmedAt == other.ConfirmedAt
            && ExpiresAt == other.ExpiresAt
            && Unit.Equals(other.Unit)
            && Outcomes.SequenceEqual(other.Outcomes);
    }

    /// <summary>Compares the original facts, outcomes, and expiry.</summary>
    /// <param name="obj">Other object.</param>
    /// <returns><see langword="true"/> when the object is an equal receipt.</returns>
    public override bool Equals(object? obj) => obj is IdentityHistoryDestructionReceipt other && Equals(other);

    /// <summary>Gets a hash code for the receipt facts.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(OperationId);
        hash.Add(LifecycleRevision);
        hash.Add(Epoch);
        hash.Add(Fence);
        hash.Add(InventoryGeneration);
        hash.Add(ConfirmedAt);
        hash.Add(ExpiresAt);
        hash.Add(Unit);
        foreach (IdentityHistoryClassOutcome outcome in Outcomes)
        {
            hash.Add(outcome);
        }

        return hash.ToHashCode();
    }
}
