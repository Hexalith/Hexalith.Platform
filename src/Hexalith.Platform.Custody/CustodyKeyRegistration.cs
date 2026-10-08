namespace Hexalith.Platform.Custody;

/// <summary>Independently verified exact wrap/store outcome; never contains plaintext/root/KEK bytes.</summary>
/// <param name="Identity">Complete key object.</param><param name="OperationId">Original wrap/store operation.</param><param name="ExpectedRevision">Conditional tenant lifecycle revision.</param>
/// <param name="KekVersion">Original purpose-bound wrapping version.</param><param name="OpaqueWrappedKeyReference">Original encrypted provider object reference.</param><param name="WrapReceiptId">Authenticated exact durable wrap/store proof.</param>
public sealed record CustodyKeyRegistration(CustodyKeyObjectIdentity Identity, string OperationId, long ExpectedRevision, string KekVersion,
    string OpaqueWrappedKeyReference, string WrapReceiptId);
