namespace Hexalith.Platform.Custody;

/// <summary>Exact authenticated compare-bound inventory change; no caller can create raw key material.</summary>
/// <param name="OperationId">Deterministic complete change identity.</param>
/// <param name="ExpectedInventoryRevision">Expected conditional inventory revision.</param>
/// <param name="Action">Exact installation or revocation.</param>
/// <param name="Key">Immutable target key identity.</param>
/// <param name="AuthenticatedEvidenceId">Independent provision/revocation evidence.</param>
public sealed record PlatformKeyInventoryChange(string OperationId, long ExpectedInventoryRevision, PlatformKeyInventoryAction Action, PlatformKeyVersion Key, string AuthenticatedEvidenceId);
