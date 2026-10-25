using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independent exact engine enrollment, carrier, ownership and irreversible closure observer.</summary>
public sealed class DaprFr34CanaryAuthority(IndependentDaprAuthority observer) : IFr34CanaryAuthority
{
    /// <inheritdoc />
    public Task<Fr34CanaryAuthorization?> ObserveAsync(Fr34ProtectionTarget expected, CancellationToken cancellationToken = default)
        => observer.InvokeAsync<Fr34CanaryAuthorization>("fr34-observe-current", new { expected }, cancellationToken);
    /// <inheritdoc />
    public async Task<bool> VerifyOwnershipAsync(Fr34ProtectionTarget expected, string originalCanaryId, Fr34CanaryReference reference,
        Fr34CanaryAuthorization admitted, CancellationToken cancellationToken = default)
        => await observer.InvokeAsync<Decision>("fr34-verify-ownership", new { expected, originalCanaryId, reference, admitted }, cancellationToken).ConfigureAwait(false) is { Allowed: true };
    /// <inheritdoc />
    public async Task<bool> VerifyCarrierAsync(Fr34ProtectionTarget expected, string originalCanaryId, Fr34CanaryReference reference,
        Fr34PersistedCanary observation, Fr34CanaryAuthorization admitted, CancellationToken cancellationToken = default)
        => await observer.InvokeAsync<Decision>("fr34-verify-carrier", new { expected, originalCanaryId, reference, observation, admitted }, cancellationToken).ConfigureAwait(false) is { Allowed: true };
    /// <inheritdoc />
    public async Task<bool> ConfirmDestroyedAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default)
        => await observer.InvokeAsync<Decision>("fr34-confirm-destroyed", new { reference }, cancellationToken).ConfigureAwait(false) is { Allowed: true };
    private sealed record Decision(bool Allowed);
}
