namespace Hexalith.Platform.Protection;

/// <summary>Bounded reverse-reference discriminator. Reused references remain ambiguous; exact owner/occurrence originals never disappear.</summary>
internal sealed record ErasureRootOccurrenceReference(string OccurrenceId, bool Ambiguous);
