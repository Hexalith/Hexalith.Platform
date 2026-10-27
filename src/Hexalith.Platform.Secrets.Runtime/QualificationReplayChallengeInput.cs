using Hexalith.Platform.Custody;
namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Actual preissued authentic deliveries, or an independently enrolled changed identity for fresh owned issuance sharing one original nonce. Foreign verification uses its separately installed current runtime authority.</summary>
public sealed record QualificationReplayChallengeInput(TrustedEnvelope? OriginalDelivery=null,TrustedEnvelope? ChangedDelivery=null,string? ForeignRuntimeConfigurationFile=null,TrustedEnvelopeIdentity? ChangedIdentity=null);
