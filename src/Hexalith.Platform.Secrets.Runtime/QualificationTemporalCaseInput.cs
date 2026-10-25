using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independently retained original delivery and exact case scope. Production clocks are never replaced by an input timestamp.</summary>
public sealed record QualificationTemporalCaseInput(TrustedEnvelope Envelope,TrustedEnvelopeIdentity Identity,string EnrolledScopeSha256);
