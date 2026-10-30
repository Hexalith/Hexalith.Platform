using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>In-memory native-operation fault fixture. This is component evidence only and supplies no physical OpenBao/copy guarantee.</summary>
internal sealed class ErasureRootStoreFixture : IErasureRootStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, (long Version, byte[] Bytes)> _heads = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedDictionary<int, byte[]>> _epochs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _archives = new(StringComparer.Ordinal);
    internal long ArchiveCreatedBytes { get; private set; }
    internal int ArchiveWrites { get; private set; }
    internal int ArchiveReads { get; private set; }
    internal long ArchiveReadBytes { get; private set; }
    internal Action? ReadObserved { get; set; }
    internal Func<Task>? BeforeTrim { get; set; }
    internal int TrimCount { get; private set; }
    internal int RotateCount { get; private set; }
    internal int RotateAttempts { get; private set; }
    internal int CasCount { get; private set; }
    internal string? Failure { get; set; }
    internal TaskCompletionSource? RotationEntered { get; set; }
    internal TaskCompletionSource? RotationContinue { get; set; }

    internal void Enroll(ErasureRootInstallation installation, string tenant, string? scope = null, string? digest = null)
    {
        var anchors = new SortedDictionary<string, ErasureRootAnchor>(StringComparer.Ordinal);
        if (scope is not null) { anchors.Add(scope, new(0, digest!, [], [])); }
        var head = new ErasureRootHead(tenant, installation.StoreId, installation.EnrollmentId, 1, [], [], [], [], anchors);
        _heads.Add(tenant, (1, JsonSerializer.SerializeToUtf8Bytes(head, ErasureRootCustody.Json)));
        _epochs.Add(tenant, new() { [1] = RandomNumberGenerator.GetBytes(32) });
    }
    internal async Task<ErasureRootHead> HeadAsync(string tenant)
        => JsonSerializer.Deserialize<ErasureRootHead>((await ReadHeadAsync(tenant, default))!.Bytes, ErasureRootCustody.Json)!;
    internal void ReplaceHead(string tenant, ErasureRootHead head)
    {
        lock (_gate)
        {
            var prior = _heads[tenant];
            _heads[tenant] = (prior.Version + 1, JsonSerializer.SerializeToUtf8Bytes(head, ErasureRootCustody.Json));
        }
    }
    internal void LoseVolume(string tenant)
    {
        lock (_gate) { _heads.Remove(tenant); if (_epochs.Remove(tenant, out var epochs)) { foreach (var key in epochs.Values) { CryptographicOperations.ZeroMemory(key); } } }
    }
    private void Fail(string point)
    {
        if (Failure != point) { return; }
        Failure = null;
        throw new IOException("Injected native acknowledgement/failure: " + point);
    }
    public Task<ErasureRootHeadSnapshot?> ReadHeadAsync(string tenantId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadObserved?.Invoke(); cancellationToken.ThrowIfCancellationRequested();
        Fail("read");
        lock (_gate) { return Task.FromResult(_heads.TryGetValue(tenantId, out var head) ? new ErasureRootHeadSnapshot(head.Version, head.Bytes.ToArray()) : null); }
    }
    public async Task<bool> CompareExchangeHeadAsync(string tenantId, long version, byte[] head, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        Fail("cas-before");
        lock (_gate)
        {
            if (!_heads.TryGetValue(tenantId, out var current) || current.Version != version) { return false; }
            _heads[tenantId] = (version + 1, head.ToArray());
            CasCount++;
        }
        Fail("cas-after");
        return true;
    }
    public Task<byte[]?> ReadArchiveAsync(string tenantId, string archiveId, CancellationToken cancellationToken)
    { lock (_gate) { ArchiveReads++; byte[]? value = _archives.GetValueOrDefault(tenantId + ":" + archiveId); ArchiveReadBytes += value?.Length ?? 0; return Task.FromResult(value?.ToArray()); } }
    public Task<bool> CreateArchiveAsync(string tenantId, string archiveId, byte[] bytes, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            string id = tenantId + ":" + archiveId;
            if (_archives.TryGetValue(id, out var original)) { return Task.FromResult(original.AsSpan().SequenceEqual(bytes)); }
            _archives.Add(id, bytes.ToArray()); ArchiveWrites++; ArchiveCreatedBytes += bytes.Length; return Task.FromResult(true);
        }
    }
    public Task<string> EncryptAsync(string tenantId, int epoch, byte[] root, string context, CancellationToken cancellationToken)
    {
        byte[] key = Derived(tenantId, epoch, context);
        byte[] bytes = new byte[60];
        try
        {
            RandomNumberGenerator.Fill(bytes.AsSpan(0, 12));
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(bytes.AsSpan(0, 12), root, bytes.AsSpan(12, 32), bytes.AsSpan(44, 16));
            return Task.FromResult("vault:v" + epoch + ":" + Convert.ToBase64String(bytes));
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    public Task<byte[]> DecryptAsync(string tenantId, ErasureRootCiphertext value, CancellationToken cancellationToken)
    {
        string[] parts = value.Ciphertext.Split(':');
        int epoch = int.Parse(parts[1][1..], System.Globalization.CultureInfo.InvariantCulture);
        byte[] key = Derived(tenantId, epoch, value.Context);
        byte[] bytes = Convert.FromBase64String(parts[2]);
        byte[] root = new byte[32];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(12, 32), bytes.AsSpan(44, 16), root);
            return Task.FromResult(root);
        }
        catch { CryptographicOperations.ZeroMemory(root); throw; }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
    private byte[] Derived(string tenant, int epoch, string context)
    {
        lock (_gate) { return HMACSHA256.HashData(_epochs[tenant][epoch], Encoding.UTF8.GetBytes(context)); }
    }
    public Task<ErasureRootNativePolicy> ReadPolicyAsync(string tenantId, CancellationToken cancellationToken)
    { lock (_gate) { int min = _epochs[tenantId].Keys.Min(); return Task.FromResult(new ErasureRootNativePolicy(_epochs[tenantId].Keys.Max(), min, min, min, true, false, false, false)); } }
    public Task<int> ReadEpochAsync(string tenantId, CancellationToken cancellationToken)
    { lock (_gate) { return Task.FromResult(_epochs[tenantId].Keys.Max()); } }
    public async Task RotateAsync(string tenantId, CancellationToken cancellationToken)
    {
        lock (_gate) { RotateAttempts++; }
        RotationEntered?.TrySetResult();
        if (RotationContinue is not null) { await RotationContinue.Task.WaitAsync(cancellationToken); }
        Fail("rotate-before");
        lock (_gate)
        {
            _epochs[tenantId].Add(_epochs[tenantId].Keys.Max() + 1, RandomNumberGenerator.GetBytes(32));
            RotateCount++;
        }
        Fail("rotate-after");
    }
    public async Task<IReadOnlyList<string>> RewrapAsync(string tenantId, int epoch, IReadOnlyList<ErasureRootCiphertext> values, CancellationToken cancellationToken)
    {
        Fail("rewrap");
        var result = new List<string>();
        foreach (var value in values)
        {
            byte[] root = await DecryptAsync(tenantId, value, cancellationToken);
            try { result.Add(await EncryptAsync(tenantId, epoch, root, value.Context, cancellationToken)); }
            finally { CryptographicOperations.ZeroMemory(root); }
        }
        return result;
    }
    public async Task TrimAsync(string tenantId, int epoch, CancellationToken cancellationToken)
    {
        if (BeforeTrim is { } beforeTrim) { await beforeTrim(); }
        Fail("trim-before");
        lock (_gate)
        {
            var removed = _epochs[tenantId].Keys.Where(value => value < epoch).ToArray();
            foreach (int prior in removed)
            { CryptographicOperations.ZeroMemory(_epochs[tenantId][prior]); _epochs[tenantId].Remove(prior); }
            if (removed.Length > 0) { TrimCount++; }
        }
        Fail("trim-after");
    }
    public Task<bool> VerifyTrimAsync(string tenantId, int epoch, CancellationToken cancellationToken)
    { lock (_gate) { return Task.FromResult(_epochs[tenantId].Keys.Min() == epoch && _epochs[tenantId].Keys.Max() >= epoch); } }
}
