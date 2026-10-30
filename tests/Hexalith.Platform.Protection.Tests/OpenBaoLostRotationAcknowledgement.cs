namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual native calls with one deliberately lost acknowledgement after a real rotation with the original Pending still unprepared; no result is fabricated.</summary>
internal sealed class OpenBaoLostRotationAcknowledgement(IErasureRootStore native) : IErasureRootStore
{
    private bool _lost;
    public Task<ErasureRootNativePolicy> ReadPolicyAsync(string tenantId, CancellationToken token) => native.ReadPolicyAsync(tenantId, token);
    public Task<byte[]?> ReadArchiveAsync(string tenantId, string archiveId, CancellationToken token) => native.ReadArchiveAsync(tenantId, archiveId, token);
    public Task<bool> CreateArchiveAsync(string tenantId, string archiveId, byte[] bytes, CancellationToken token) => native.CreateArchiveAsync(tenantId, archiveId, bytes, token);
    public Task<ErasureRootHeadSnapshot?> ReadHeadAsync(string tenantId, CancellationToken token) => native.ReadHeadAsync(tenantId, token);
    public Task<bool> CompareExchangeHeadAsync(string tenantId, long version, byte[] head, CancellationToken token) => native.CompareExchangeHeadAsync(tenantId, version, head, token);
    public Task<string> EncryptAsync(string tenantId, int epoch, byte[] root, string context, CancellationToken token) => native.EncryptAsync(tenantId, epoch, root, context, token);
    public Task<byte[]> DecryptAsync(string tenantId, ErasureRootCiphertext value, CancellationToken token) => native.DecryptAsync(tenantId, value, token);
    public Task<int> ReadEpochAsync(string tenantId, CancellationToken token) => native.ReadEpochAsync(tenantId, token);
    public async Task RotateAsync(string tenantId, CancellationToken token)
    { await native.RotateAsync(tenantId, token); if (!_lost) { _lost = true; throw new IOException("Intentionally lost native rotation acknowledgement."); } }
    public Task<IReadOnlyList<string>> RewrapAsync(string tenantId, int epoch, IReadOnlyList<ErasureRootCiphertext> values, CancellationToken token) => native.RewrapAsync(tenantId, epoch, values, token);
    public Task TrimAsync(string tenantId, int epoch, CancellationToken token) => native.TrimAsync(tenantId, epoch, token);
    public Task<bool> VerifyTrimAsync(string tenantId, int epoch, CancellationToken token) => native.VerifyTrimAsync(tenantId, epoch, token);
}
