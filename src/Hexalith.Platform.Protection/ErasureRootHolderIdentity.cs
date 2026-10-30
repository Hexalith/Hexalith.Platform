using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Immutable restrictive holder identity index. Existence forbids redelivery; only current committed ancestry proves completed retirement.</summary>
internal sealed record ErasureRootHolderIdentity(CustodyKeyRegistration Registration, string HolderId);
