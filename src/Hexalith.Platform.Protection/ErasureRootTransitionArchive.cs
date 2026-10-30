using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Immutable ERC transition node. Only a current tenant CAS pointer and the complete predecessor chain establish committed ancestry.</summary>
internal sealed record ErasureRootTransitionArchive(string ScopeId, string? Previous, AnchoredStateTransition Original);
