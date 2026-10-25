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
