namespace Hexalith.Platform.Protection.Tests;

/// <summary>Forwards a real store and refuses only encrypt, so a metadata-only policy read cannot admit a root.</summary>
internal sealed class ErasureRootEncryptDeniedStore(IErasureRootStore inner) : IErasureRootStore
{
    public Task<ErasureRootHeadSnapshot?> ReadHeadAsync(string tenantId, CancellationToken cancellationToken) => inner.ReadHeadAsync(tenantId, cancellationToken);
    public Task<bool> CompareExchangeHeadAsync(string tenantId, long version, byte[] head, CancellationToken cancellationToken) => inner.CompareExchangeHeadAsync(tenantId, version, head, cancellationToken);
    public Task<byte[]?> ReadArchiveAsync(string tenantId, string archiveId, CancellationToken cancellationToken) => inner.ReadArchiveAsync(tenantId, archiveId, cancellationToken);
    public Task<bool> CreateArchiveAsync(string tenantId, string archiveId, byte[] bytes, CancellationToken cancellationToken) => inner.CreateArchiveAsync(tenantId, archiveId, bytes, cancellationToken);
    public Task<string> EncryptAsync(string tenantId, int epoch, byte[] root, string context, CancellationToken cancellationToken) => throw new IOException("Encrypt denied.");
    public Task<byte[]> DecryptAsync(string tenantId, ErasureRootCiphertext value, CancellationToken cancellationToken) => inner.DecryptAsync(tenantId, value, cancellationToken);
    public Task<int> ReadEpochAsync(string tenantId, CancellationToken cancellationToken) => inner.ReadEpochAsync(tenantId, cancellationToken);
    public Task<ErasureRootNativePolicy> ReadPolicyAsync(string tenantId, CancellationToken cancellationToken) => inner.ReadPolicyAsync(tenantId, cancellationToken);
    public Task RotateAsync(string tenantId, CancellationToken cancellationToken) => inner.RotateAsync(tenantId, cancellationToken);
    public Task<IReadOnlyList<string>> RewrapAsync(string tenantId, int epoch, IReadOnlyList<ErasureRootCiphertext> values, CancellationToken cancellationToken) => inner.RewrapAsync(tenantId, epoch, values, cancellationToken);
    public Task TrimAsync(string tenantId, int epoch, CancellationToken cancellationToken) => inner.TrimAsync(tenantId, epoch, cancellationToken);
    public Task<bool> VerifyTrimAsync(string tenantId, int epoch, CancellationToken cancellationToken) => inner.VerifyTrimAsync(tenantId, epoch, cancellationToken);
}
