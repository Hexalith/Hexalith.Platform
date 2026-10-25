namespace Hexalith.Platform.Custody;

/// <summary>Actual independent cryptographic observations of an existing export original; no expected metric values are inputs.</summary>
public sealed record ExportQualificationObservation(string Outcome,bool? AuthenticatedWrap,bool? DetachedEs256,bool? PublicAnchorRetained,bool? OwnedPlaintextCleared);
