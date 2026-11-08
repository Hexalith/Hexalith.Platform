namespace Hexalith.Platform.Composition;

/// <summary>The Platform-composed eventstore host: the EventStore server plus the declared extension packages.</summary>
/// <param name="ServerProject">The EventStore server executable the composition would otherwise run; it supplies the gateway settings.</param>
/// <param name="Extensions">The declared extension packages, in declaration order.</param>
public sealed record PlatformEventStoreHostBinding(string ServerProject, IReadOnlyList<PlatformEventStoreExtensionBinding> Extensions);
