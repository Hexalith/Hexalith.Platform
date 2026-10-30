namespace Hexalith.Platform.Protection;

/// <summary>Qualification-only controls around actual native ERC operations. Faults interrupt real calls; no native result or receipt is fabricated.</summary>
public sealed class ProtectionQualificationControlledErasureRootStore(IErasureRootStore native, ProtectionQualificationFaults controls) : IErasureRootStore
{
    private async Task<T> Call<T>(string tenant, string method, Func<Task<T>> operation, CancellationToken token)
    {
        string id = "native:" + tenant + ":" + method;
        await controls.CheckAsync(id, "before", token).ConfigureAwait(false);
        T result = await operation().ConfigureAwait(false);
        try { await controls.CheckAsync(id, "after", token).ConfigureAwait(false); return result; }
        catch { if (result is byte[] owned) { System.Security.Cryptography.CryptographicOperations.ZeroMemory(owned); } throw; }
    }
    private Task Call(string tenant, string method, Func<Task> operation, CancellationToken token)
        => Call(tenant, method, async () => { await operation().ConfigureAwait(false); return true; }, token);
    /// <inheritdoc />
    public Task<ErasureRootNativePolicy> ReadPolicyAsync(string tenantId, CancellationToken cancellationToken) => native.ReadPolicyAsync(tenantId, cancellationToken);
    /// <inheritdoc />
    public Task<byte[]?> ReadArchiveAsync(string tenantId, string archiveId, CancellationToken token) => native.ReadArchiveAsync(tenantId, archiveId, token);
    /// <inheritdoc />
    public Task<bool> CreateArchiveAsync(string tenantId, string archiveId, byte[] bytes, CancellationToken token) => native.CreateArchiveAsync(tenantId, archiveId, bytes, token);
    /// <inheritdoc />
    public Task<ErasureRootHeadSnapshot?> ReadHeadAsync(string tenantId, CancellationToken cancellationToken) => Call(tenantId, "head-read", () => native.ReadHeadAsync(tenantId, cancellationToken), cancellationToken);
    /// <inheritdoc />
    public async Task<bool> CompareExchangeHeadAsync(string tenantId, long version, byte[] head, CancellationToken cancellationToken)
    {
        string? owner = ErasureRootNativeOperationScope.Current;
        if (owner is not null) { await controls.CheckAsync(owner, "before", cancellationToken).ConfigureAwait(false); }
        bool result = await Call(tenantId, "head-cas", () => native.CompareExchangeHeadAsync(tenantId, version, head, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (owner is not null && result) { await controls.CheckAsync(owner, "after", cancellationToken).ConfigureAwait(false); }
        return result;
    }
    /// <inheritdoc />
    public Task<string> EncryptAsync(string tenantId, int epoch, byte[] root, string context, CancellationToken cancellationToken) => Call(tenantId, "wrap", () => native.EncryptAsync(tenantId, epoch, root, context, cancellationToken), cancellationToken);
    /// <inheritdoc />
    public Task<byte[]> DecryptAsync(string tenantId, ErasureRootCiphertext value, CancellationToken cancellationToken) => Call(tenantId, "unwrap", () => native.DecryptAsync(tenantId, value, cancellationToken), cancellationToken);
    /// <inheritdoc />
    public Task<int> ReadEpochAsync(string tenantId, CancellationToken cancellationToken) => Call(tenantId, "epoch-read", () => native.ReadEpochAsync(tenantId, cancellationToken), cancellationToken);
    /// <inheritdoc />
    public Task RotateAsync(string tenantId, CancellationToken cancellationToken) => Call(tenantId, "rotate", () => native.RotateAsync(tenantId, cancellationToken), cancellationToken);
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> RewrapAsync(string tenantId, int epoch, IReadOnlyList<ErasureRootCiphertext> values, CancellationToken cancellationToken) => Call(tenantId, "rewrap", () => native.RewrapAsync(tenantId, epoch, values, cancellationToken), cancellationToken);
    /// <inheritdoc />
    public Task TrimAsync(string tenantId, int epoch, CancellationToken cancellationToken) => Call(tenantId, "trim", () => native.TrimAsync(tenantId, epoch, cancellationToken), cancellationToken);
    /// <inheritdoc />
    public Task<bool> VerifyTrimAsync(string tenantId, int epoch, CancellationToken cancellationToken) => Call(tenantId, "trim-read", () => native.VerifyTrimAsync(tenantId, epoch, cancellationToken), cancellationToken);
}
