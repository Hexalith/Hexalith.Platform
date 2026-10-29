using System.Text.Json.Nodes;

namespace Hexalith.Platform.Protection;

/// <summary>Immutable role admission identity and retained actual native outcomes, independent of restored EventStore state.</summary>
/// <param name="Role">Admitted private role.</param><param name="BindingDigest">Complete target/profile/plan/anchor binding.</param>
/// <param name="AttemptId">Once-only independently admitted attempt.</param><param name="RequestSha256">Digest of the complete original wire request.</param>
/// <param name="SafeRequest">Original request with synthetic plaintext omitted.</param><param name="Result">Actual terminal response if retained.</param>
public sealed record ProtectionQualificationOperation(string Role, string BindingDigest, string AttemptId, string RequestSha256,
    JsonObject SafeRequest, JsonObject? Result = null)
{
    /// <summary>Actual admitted native calls and immutable native results. Unresolved entries can only be looked up.</summary>
    public SortedDictionary<string, ProtectionQualificationNativeOriginal> Calls { get; init; } = new(StringComparer.Ordinal);
    /// <summary>Whether the enrollment role retained this executor identity before dispatch. Read-only round trips have separate audit entries.</summary>
    public bool IndependentlyRegistered { get; init; } = true;
}
