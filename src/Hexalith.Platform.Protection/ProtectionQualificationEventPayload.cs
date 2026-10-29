using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.Platform.Protection;

/// <summary>Technical canary-only payload marker for the owner fixture; contains no domain behavior.</summary>
public sealed record ProtectionQualificationEventPayload : IEventPayload;
