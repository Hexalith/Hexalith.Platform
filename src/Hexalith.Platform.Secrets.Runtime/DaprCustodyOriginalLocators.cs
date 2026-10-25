using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Bounded authenticated locators. Returned complete originals remain subject to custody verification.</summary>
public sealed class DaprCustodyOriginalLocators(IndependentDaprAuthority authority) : ICustodyKeyRegistrationSource, ICustodyReservedManifestSource
{
    /// <inheritdoc />
    public async Task<CustodyKeyRegistration?> FindAsync(CustodyKeyObjectIdentity identity, CancellationToken cancellationToken)
    {
        var original = await authority.InvokeAsync<CustodyKeyRegistration>("custody-registration-original", new { identity }, cancellationToken).ConfigureAwait(false);
        return original?.Identity == identity ? original : null;
    }
    /// <inheritdoc />
    public async Task<DeletionBatchConsumptionRequest?> FindAsync(string reservationReceiptId, CancellationToken cancellationToken)
    {
        if (!CustodyBindingContract.Text(reservationReceiptId)) return null;
        return await authority.InvokeAsync<DeletionBatchConsumptionRequest>("custody-reserved-manifest-original", new { reservationReceiptId }, cancellationToken).ConfigureAwait(false);
    }
}
