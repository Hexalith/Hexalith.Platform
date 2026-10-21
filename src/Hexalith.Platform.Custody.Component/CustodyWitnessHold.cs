using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Custody.Component;

/// <summary>Exact pin retained by the outside witness.</summary>
/// <param name="HoldId">Exact hold.</param>
/// <param name="Identity">Pinned key.</param>
internal readonly record struct CustodyWitnessHold(string HoldId, CustodyKeyObjectIdentity Identity);
