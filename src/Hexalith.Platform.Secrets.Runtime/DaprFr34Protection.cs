using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Real independently enrolled engine service adapter; no no-op or process-memory protection fallback.</summary>
public sealed class DaprFr34ProtectionEngine(IndependentDaprAuthority engine) : IFr34ProtectionCanaryEngine
{
    /// <inheritdoc />
    public Task<Fr34CanaryReference?> SealAndPersistAsync(Fr34ProtectionTarget target, string canaryId, byte[] plaintext, CancellationToken cancellationToken = default)
        => engine.InvokeAsync<Fr34CanaryReference>("fr34-seal-persist", new { target, canaryId, plaintext }, cancellationToken);
    /// <inheritdoc />
    public Task<byte[]?> UnsealAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default)
        => engine.InvokeAsync<byte[]>("fr34-unseal", new { reference }, cancellationToken);
    /// <inheritdoc />
    public async Task<bool> DestroyAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default)
        => await engine.InvokeAsync<Decision>("fr34-destroy", new { reference }, cancellationToken).ConfigureAwait(false) is { Allowed: true };
    /// <inheritdoc />
    public Task<PayloadUnprotectionOutcome?> ReplayAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default)
        => engine.InvokeAsync<PayloadUnprotectionOutcome>("fr34-replay", new { reference }, cancellationToken);
    private sealed record Decision(bool Allowed);
}

/// <summary>Independent persisted reader. It never accepts the engine's supplied seal buffer as storage evidence.</summary>
public sealed class DaprFr34PersistedReader(IndependentDaprAuthority observer) : IFr34PersistedCanaryReader
{
    /// <inheritdoc />
    public Task<Fr34PersistedCanary?> ReadAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default)
        => observer.InvokeAsync<Fr34PersistedCanary>("fr34-read-persisted", new { reference }, cancellationToken);
}

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
