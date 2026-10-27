using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Pre-effect wrap identity used to recover a native receipt without knowing that receipt or rereading the root.</summary>
public sealed record CustodyWrapOriginalIdentity(CustodyKeyObjectIdentity Identity,string OperationId,long ExpectedRevision,
    string KekVersion,string OpaqueWrappedKeyReference);
