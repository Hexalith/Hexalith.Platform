namespace Hexalith.Platform.Custody;

/// <summary>Opaque copy-class outcome. The text is not payload, key, or actor content.</summary>
public sealed record IdentityHistoryClassOutcome
{
    /// <summary>Creates an opaque class outcome.</summary>
    /// <param name="copyClass">Opaque class reference.</param>
    /// <param name="outcome">Opaque outcome text.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required string is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a required string is blank.</exception>
    public IdentityHistoryClassOutcome(string copyClass, string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(copyClass);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        CopyClass = copyClass;
        Outcome = outcome;
    }

    /// <summary>Gets the opaque class reference.</summary>
    public string CopyClass { get; }

    /// <summary>Gets the opaque outcome text.</summary>
    public string Outcome { get; }
}
