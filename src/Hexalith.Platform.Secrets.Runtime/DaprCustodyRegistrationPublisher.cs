using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Authenticated exact publication to the independently enrolled authority. No retry or local fallback.</summary>
public sealed class DaprCustodyRegistrationPublisher(IndependentDaprAuthority authority) : ICustodyRegistrationPublisher
{
    /// <inheritdoc />
    public async Task<CustodyKeyRegistration?> PublishAsync(CustodyKeyRegistration original, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(original);
        var retained = await authority.InvokeAsync<CustodyKeyRegistration>("custody-publish-registration-original", new { original }, cancellationToken).ConfigureAwait(false);
        return retained == original ? retained : null;
    }
}
