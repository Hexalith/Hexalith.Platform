using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact independently provisioned routine rotation changes and retained-digest scope. Key values/native versions are never created by this workflow.</summary>
public sealed record QualificationInventoryRotationInput(PlatformKeyInventoryChange InstallOriginal,PlatformKeyInventoryChange InstallSuccessor,
    PlatformHmacScope DigestScope,IReadOnlyList<string?> DigestComponents,string EnrolledScopeSha256);
