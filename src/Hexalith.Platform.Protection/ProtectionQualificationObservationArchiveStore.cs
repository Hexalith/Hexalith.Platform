namespace Hexalith.Platform.Protection;

/// <summary>One read-only observation's bounded reuse of immutable native archive bytes while resolving its exact historical owner revisions.</summary>
internal sealed class ProtectionQualificationObservationArchiveStore(IErasureRootStore native) : IErasureRootStore
{
    private readonly Dictionary<(string Tenant, string Archive), byte[]> _archives = new();
    private int _bytes;
    public async Task<byte[]?> ReadArchiveAsync(string tenantId, string archiveId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_archives.TryGetValue((tenantId, archiveId), out var retained)) { return retained; }
        var bytes = await native.ReadArchiveAsync(tenantId, archiveId, cancellationToken).ConfigureAwait(false);
        if (bytes is not null && bytes.Length <= 4 * 1024 * 1024)
        {
            if (_archives.Count >= 1024 || _bytes + bytes.Length > 4 * 1024 * 1024) { _archives.Clear(); _bytes = 0; }
            _archives.Add((tenantId, archiveId), bytes); _bytes += bytes.Length;
        }
        return bytes;
    }
    public Task<ErasureRootHeadSnapshot?> ReadHeadAsync(string tenantId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> CompareExchangeHeadAsync(string tenantId, long version, byte[] head, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> CreateArchiveAsync(string tenantId, string archiveId, byte[] bytes, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<string> EncryptAsync(string tenantId, int epoch, byte[] root, string context, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<byte[]> DecryptAsync(string tenantId, ErasureRootCiphertext value, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<int> ReadEpochAsync(string tenantId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task RotateAsync(string tenantId, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<IReadOnlyList<string>> RewrapAsync(string tenantId, int epoch, IReadOnlyList<ErasureRootCiphertext> values, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task TrimAsync(string tenantId, int epoch, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> VerifyTrimAsync(string tenantId, int epoch, CancellationToken cancellationToken) => throw new NotSupportedException();
}
