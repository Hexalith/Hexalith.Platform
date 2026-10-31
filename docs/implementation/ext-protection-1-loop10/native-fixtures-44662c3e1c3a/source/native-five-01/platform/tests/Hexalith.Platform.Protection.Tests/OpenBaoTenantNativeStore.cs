namespace Hexalith.Platform.Protection.Tests;

/// <summary>Routes each test operation to a real native store with that tenant's exclusive rendered-policy token.</summary>
internal sealed class OpenBaoTenantNativeStore(IReadOnlyDictionary<string, OpenBaoErasureRootStore> stores) : IErasureRootStore
{
    public Task<ErasureRootHeadSnapshot?> ReadHeadAsync(string tenantId, CancellationToken cancellationToken) => stores[tenantId].ReadHeadAsync(tenantId, cancellationToken);
    public Task<bool> CompareExchangeHeadAsync(string tenantId, long version, byte[] head, CancellationToken cancellationToken) => stores[tenantId].CompareExchangeHeadAsync(tenantId, version, head, cancellationToken);
    public Task<byte[]?> ReadArchiveAsync(string tenantId, string archiveId, CancellationToken cancellationToken) => stores[tenantId].ReadArchiveAsync(tenantId, archiveId, cancellationToken);
    public Task<bool> CreateArchiveAsync(string tenantId, string archiveId, byte[] bytes, CancellationToken cancellationToken) => stores[tenantId].CreateArchiveAsync(tenantId, archiveId, bytes, cancellationToken);
    public Task<string> EncryptAsync(string tenantId, int epoch, byte[] root, string context, CancellationToken cancellationToken) => stores[tenantId].EncryptAsync(tenantId, epoch, root, context, cancellationToken);
    public Task<byte[]> DecryptAsync(string tenantId, ErasureRootCiphertext value, CancellationToken cancellationToken) => stores[tenantId].DecryptAsync(tenantId, value, cancellationToken);
    public Task<int> ReadEpochAsync(string tenantId, CancellationToken cancellationToken) => stores[tenantId].ReadEpochAsync(tenantId, cancellationToken);
    public Task<ErasureRootNativePolicy> ReadPolicyAsync(string tenantId, CancellationToken cancellationToken) => stores[tenantId].ReadPolicyAsync(tenantId, cancellationToken);
    public Task RotateAsync(string tenantId, CancellationToken cancellationToken) => stores[tenantId].RotateAsync(tenantId, cancellationToken);
    public Task<IReadOnlyList<string>> RewrapAsync(string tenantId, int epoch, IReadOnlyList<ErasureRootCiphertext> values, CancellationToken cancellationToken) => stores[tenantId].RewrapAsync(tenantId, epoch, values, cancellationToken);
    public Task TrimAsync(string tenantId, int epoch, CancellationToken cancellationToken) => stores[tenantId].TrimAsync(tenantId, epoch, cancellationToken);
    public Task<bool> VerifyTrimAsync(string tenantId, int epoch, CancellationToken cancellationToken) => stores[tenantId].VerifyTrimAsync(tenantId, epoch, cancellationToken);
}
