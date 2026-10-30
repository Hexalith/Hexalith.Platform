namespace Hexalith.Platform.Protection;

/// <summary>Explicit independently installed role scope. All native references and typed recipe inputs must be supplied; no runtime provisioning or invented evidence exists.</summary>
/// <param name="Role">Private role served by this process.</param><param name="Identity">Published signing identity.</param><param name="TenantId">Exact fixture tenant.</param>
/// <param name="BindingDigest">Accepted complete binding digest.</param><param name="InstallationProof">Independent persisted native installation/runtime census original.</param>
/// <param name="Recipes">Exact native request templates for all 41 cases.</param><param name="Observations">Independent persisted-state projections for all 41 cases.</param>
public sealed record ProtectionQualificationServiceScope(string Role, string Identity, string TenantId, string BindingDigest,
    ProtectionQualificationNativeCell InstallationProof,
    Dictionary<string, Dictionary<string, ProtectionQualificationActionRecipe>> Recipes,
    Dictionary<string, ProtectionQualificationObservationRecipe> Observations)
{
    /// <summary>Complete exact native argument values for the closed source catalog. They cannot provide executable recipes or expected observations.</summary>
    public Dictionary<string, System.Text.Json.Nodes.JsonObject>? NativeArguments { get; init; }
}
