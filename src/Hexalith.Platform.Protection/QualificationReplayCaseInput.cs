using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Exact independently enrolled replay original and its first evaluation. Retrying never changes first-seen or retention.</summary>
public sealed record QualificationReplayCaseInput(TrustedEnvelopeReplayIntent Intent,DateTimeOffset FirstSeenAt,string EnrolledScopeSha256);
