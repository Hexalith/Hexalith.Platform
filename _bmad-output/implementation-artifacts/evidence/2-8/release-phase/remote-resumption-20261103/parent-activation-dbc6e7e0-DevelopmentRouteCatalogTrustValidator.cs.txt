using Hexalith.EventStore.Contracts.Routing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Explicit Development-only trust fixture. No default production trust is manufactured.</summary>
public sealed class DevelopmentRouteCatalogTrustValidator(IOptions<RouteCatalogHostOptions> options,
    IHostEnvironment? environment = null) : IRouteCatalogTrustValidator
{
    /// <inheritdoc/>
    public Task ValidateAsync(RouteCatalogActivation activation, string bindingDigest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activation);
        cancellationToken.ThrowIfCancellationRequested();
        if (environment?.IsDevelopment() != true || !options.Value.DevelopmentFixture
            || RouteCatalogCodec.Decode(activation.CanonicalCatalog, activation.RootDigest).Profile != "Development"
            || activation.Owner != "Development-fixture" || activation.TrustProof != bindingDigest)
        {
            throw new RouteCatalogUnavailableException("An activated owner-bound production catalog is required; Development fixtures supply no production authority.", RouteCatalogRefusalReason.Trust);
        }
        return Task.CompletedTask;
    }
}
