using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Uses the authenticated component lookup that compares every field to its immutable wrap response.</summary>
public sealed class DaprCustodyRegistrationOriginalVerifier(CustodyLifecycleClient client) : ICustodyRegistrationOriginalVerifier
{
    /// <inheritdoc />
    public async Task<bool> VerifyAsync(CustodyKeyRegistration original, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(original);
        var verified = await client.LookupOriginalAsync(original, cancellationToken: cancellationToken).ConfigureAwait(false);
        return verified.Identity == original.Identity && verified.OperationId == original.OperationId
            && verified.Status == CustodyKeyLifecycleStatus.Wrapped && verified.ReceiptId == original.WrapReceiptId;
    }
}
