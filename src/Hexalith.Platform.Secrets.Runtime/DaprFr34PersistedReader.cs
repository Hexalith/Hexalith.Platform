using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independent persisted reader. It never accepts the engine's supplied seal buffer as storage evidence.</summary>
public sealed class DaprFr34PersistedReader(IndependentDaprAuthority observer) : IFr34PersistedCanaryReader
{
    /// <inheritdoc />
    public Task<Fr34PersistedCanary?> ReadAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default)
        => observer.InvokeAsync<Fr34PersistedCanary>("fr34-read-persisted", new { reference }, cancellationToken);
}
