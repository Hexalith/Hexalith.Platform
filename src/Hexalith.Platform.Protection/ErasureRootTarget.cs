using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Custody-only root record, including immutable registration, context and current hold identities; never contains plaintext.</summary>
internal sealed record ErasureRootTarget(CustodyKeyRegistration Registration, ErasureRootCiphertext Wrapped, string[] Pins,
    string[] Holders, string[] CompletedHolders)
{
    /// <summary>Current completed-holder ancestry pointer. Active holders remain inside the CAS head.</summary>
    public string? CompletedHolderArchive { get; init; }
    /// <summary>Exact bounded-depth completed-holder index; the legacy ancestry is migrated atomically with this pointer.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CompletedHolderIndexRoot { get; init; }
}
