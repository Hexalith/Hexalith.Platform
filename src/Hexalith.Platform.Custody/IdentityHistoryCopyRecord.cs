namespace Hexalith.Platform.Custody;

/// <summary>One inventoried copy bound to the original unit, generation, and deadline.</summary>
public sealed record IdentityHistoryCopyRecord
{
    /// <summary>Binds a copy to the original unit and deadline.</summary>
    /// <param name="unit">Immutable custody unit.</param>
    /// <param name="generation">Generation covered by this copy.</param>
    /// <param name="copyClass">Opaque copy class.</param>
    /// <param name="location">Opaque location reference.</param>
    /// <param name="owner">Opaque owner reference.</param>
    /// <param name="registration">Copy registration state.</param>
    /// <param name="deadline">Deadline that must equal the unit deadline.</param>
    /// <param name="completionReference">Completion reference, present only when completed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="unit"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the copy is not bound to the original deadline or registration.</exception>
    public IdentityHistoryCopyRecord(
        IdentityHistoryCustodyUnit unit,
        long generation,
        string copyClass,
        string location,
        string owner,
        IdentityHistoryCopyRegistration registration,
        DateTimeOffset deadline,
        string? completionReference)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(copyClass);
        ArgumentException.ThrowIfNullOrWhiteSpace(location);
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if (!Enum.IsDefined(registration))
        {
            throw new ArgumentException("Copy registration is unknown.", nameof(registration));
        }

        if (generation < 0)
        {
            throw new ArgumentException("Copy generation cannot be negative.", nameof(generation));
        }

        DateTimeOffset normalizedDeadline = deadline.ToUniversalTime();
        if (normalizedDeadline != unit.ExpiresAt)
        {
            throw new ArgumentException("A copy deadline must equal the original unit deadline.", nameof(deadline));
        }

        if (registration == IdentityHistoryCopyRegistration.Completed)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(completionReference);
        }
        else if (completionReference is not null)
        {
            throw new ArgumentException("Only a completed copy has a completion reference.", nameof(completionReference));
        }

        Unit = unit;
        Generation = generation;
        CopyClass = copyClass;
        Location = location;
        Owner = owner;
        Registration = registration;
        Deadline = normalizedDeadline;
        CompletionReference = completionReference;
    }

    /// <summary>Gets the original unit.</summary>
    public IdentityHistoryCustodyUnit Unit { get; }

    /// <summary>Gets the bound generation.</summary>
    public long Generation { get; }

    /// <summary>Gets the opaque copy class.</summary>
    public string CopyClass { get; }

    /// <summary>Gets the opaque location reference.</summary>
    public string Location { get; }

    /// <summary>Gets the opaque owner reference.</summary>
    public string Owner { get; }

    /// <summary>Gets the registration state.</summary>
    public IdentityHistoryCopyRegistration Registration { get; }

    /// <summary>Gets the original UTC deadline.</summary>
    public DateTimeOffset Deadline { get; }

    /// <summary>Gets the completion reference.</summary>
    public string? CompletionReference { get; }
}
