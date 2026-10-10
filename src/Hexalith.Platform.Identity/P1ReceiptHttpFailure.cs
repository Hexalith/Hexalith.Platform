namespace Hexalith.Platform.Identity;

/// <summary>Fail-closed outcome of an attempted P1 HTTP observation.</summary>
public enum P1ReceiptHttpFailure
{
    /// <summary>The complete observation verified.</summary>
    None,
    /// <summary>A required input or authenticated time source was unavailable.</summary>
    InvalidInput,
    /// <summary>Trust, TLS, HTTP framing or transport validation refused.</summary>
    Transport,
    /// <summary>The signed receipt or current status did not verify.</summary>
    Authority,
    /// <summary>A request or whole-chain deadline or cancellation was reached.</summary>
    Deadline,
    /// <summary>A verified receipt ID was observed with different complete framing.</summary>
    ImmutableIdIncident,
    /// <summary>The bounded local history cannot admit another ID.</summary>
    HistoryFull,
}
