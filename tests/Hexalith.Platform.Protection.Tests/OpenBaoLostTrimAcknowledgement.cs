namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual native calls with one deliberately lost acknowledgement after the real irreversible trim; no result is fabricated.</summary>
internal sealed class OpenBaoLostTrimAcknowledgement(IErasureRootStore native) : IErasureRootStore
{
    private bool _lost;
    public Task<ErasureRootHeadSnapshot?> ReadHeadAsync(string tenantId, CancellationToken token) => native.ReadHeadAsync(tenantId, token);
    public Task<bool> CompareExchangeHeadAsync(string tenantId, long version, byte[] head, CancellationToken token) => native.CompareExchangeHeadAsync(tenantId, version, head, token);
    public Task<string> EncryptAsync(string tenantId, int epoch, byte[] root, string context, CancellationToken token) => native.EncryptAsync(tenantId, epoch, root, context, token);
    public Task<byte[]> DecryptAsync(string tenantId, ErasureRootCiphertext value, CancellationToken token) => native.DecryptAsync(tenantId, value, token);
    public Task<int> ReadEpochAsync(string tenantId, CancellationToken token) => native.ReadEpochAsync(tenantId, token);
    public Task RotateAsync(string tenantId, CancellationToken token) => native.RotateAsync(tenantId, token);
    public Task<IReadOnlyList<string>> RewrapAsync(string tenantId, int epoch, IReadOnlyList<ErasureRootCiphertext> values, CancellationToken token) => native.RewrapAsync(tenantId, epoch, values, token);
    public async Task TrimAsync(string tenantId, int epoch, CancellationToken token)
    { await native.TrimAsync(tenantId, epoch, token); if (!_lost) { _lost = true; throw new IOException("Intentionally lost native trim acknowledgement."); } }
    public Task<bool> VerifyTrimAsync(string tenantId, int epoch, CancellationToken token) => native.VerifyTrimAsync(tenantId, epoch, token);
}
