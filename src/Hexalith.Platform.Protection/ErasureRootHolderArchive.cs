using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Immutable completed-holder chain, bound to the exact root registration and current tenant CAS pointer.</summary>
internal sealed record ErasureRootHolderArchive(CustodyKeyRegistration Registration, string HolderId, string? Previous);
