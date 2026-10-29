using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection;

/// <summary>Exact native accepted-set control root for the containment fixture. It is distinct from both expected uncovered targets and contains no payload.</summary>
public sealed record ProtectionQualificationNativeRoot(CustodyKeyObjectIdentity Identity, string OperationId, long ExpectedRevision);
