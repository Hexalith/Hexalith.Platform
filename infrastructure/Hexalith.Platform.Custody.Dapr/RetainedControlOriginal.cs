using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

// Retained independently signed guard/protection original. Receipt identifiers or caller booleans cannot replace it.
internal sealed record RetainedControlOriginal(ControlAdmissionProofV1 Proof, SignedStatement GuardAndProtectionOriginal, SignedStatement? ControlStatement = null);
