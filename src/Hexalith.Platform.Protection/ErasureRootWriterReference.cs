namespace Hexalith.Platform.Protection;

/// <summary>Independently enrolled native source proof cell, committed atomically with the protected source event/outbox. Absence is never NeverWritten.</summary>
/// <param name="Component">Private source state component.</param><param name="Key">Exact original proof key.</param><param name="Metadata">Original partition metadata.</param>
public sealed record ErasureRootWriterReference(string Component, string Key, Dictionary<string, string> Metadata);
