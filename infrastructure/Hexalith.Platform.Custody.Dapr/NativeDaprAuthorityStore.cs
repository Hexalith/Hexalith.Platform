using Dapr.Client;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Custody.Dapr;

// Independent enrolled current head and append-only originals. No genesis, repair, restore or automatic retry API.
internal sealed class NativeDaprAuthorityStore(ComponentProfile profile, NativeInstallation installation, DaprClient dapr, TimeProvider clock) : IAuthorityStore
{
    private string Key(string tenant) => installation.AuthorityKeyPrefix + CustodyJson.Hash(System.Text.Encoding.UTF8.GetBytes(tenant));
    public async Task<CurrentState> ReadAsync(string tenant, CancellationToken token)
    {
        if (!profile.Tenants.Contains(tenant, StringComparer.Ordinal)) throw new AuthorityUnavailableException("Tenant not enrolled.");
        ValidateArchiveEnrollment();
        var (head, etag) = await dapr.GetStateAndETagAsync<AuthorityHead>(installation.AuthorityComponent, Key(tenant), ConsistencyMode.Strong, cancellationToken: token);
        if (head is null || string.IsNullOrWhiteSpace(etag)) throw new AuthorityUnavailableException("Independent current authority/genesis unavailable.");
        byte[] bytes = await ReadImmutableAsync(head.Checkpoint, token);
        var checkpoint = await CheckpointArchive.DecodeAsync(bytes, ReadImmutableAsync, token);
        if (head.EnrollmentDigest != profile.EnrollmentDigest || checkpoint.EnrollmentDigest != profile.EnrollmentDigest
            || checkpoint.Revision != head.Revision || checkpoint.TenantId != tenant || head.Checkpoint.Sha256 != head.Digest)
            throw new AuthorityUnavailableException("Current enrolled checkpoint mismatch.");
        return new(head, checkpoint, etag, CustodyJson.Read<CheckpointArchiveV1>(bytes));
    }
    public Task<ObjectVersion> PutImmutableAsync(ReadOnlyMemory<byte> bytes, string category, CancellationToken token)
        => PutAtAsync(bytes, category + "/" + Guid.NewGuid().ToString("N") + "/" + CustodyJson.Hash(bytes.Span), token);
    public Task<ObjectVersion> PutIntentResultAsync(ReadOnlyMemory<byte> bytes, string locator, CancellationToken token)
        => locator.StartsWith("journal/native-result/", StringComparison.Ordinal) ? PutAtAsync(bytes, locator, token)
            : throw new AuthorityUnavailableException("Invalid native original locator.");
    public async Task<ObjectVersion?> FindIntentResultAsync(string locator, CancellationToken token)
    {
        if (!locator.StartsWith("journal/native-result/", StringComparison.Ordinal)) throw new AuthorityUnavailableException("Invalid native original locator.");
        string path = Resolve(locator);
        if (!File.Exists(path)) return null;
        byte[] bytes = await ReadBoundedAsync(path, token);
        return new(locator, CustodyJson.Hash(bytes), CustodyJson.Hash(bytes), bytes.Length, profile.RetainUntil);
    }
    private async Task<ObjectVersion> PutAtAsync(ReadOnlyMemory<byte> bytes, string key, CancellationToken token)
    {
        ValidateArchiveEnrollment();
        if (bytes.Length is 0 or > CustodyBindingContract.MaximumOriginalBytes || profile.RetainUntil <= clock.GetUtcNow())
            throw new AuthorityUnavailableException("Original capacity/retention unavailable.");
        string path = Resolve(key);
        await ImmutableFileArchive.PublishAsync(path, bytes, token);
        return new(key, CustodyJson.Hash(bytes.Span), CustodyJson.Hash(bytes.Span), bytes.Length, profile.RetainUntil);
    }
    public async Task<byte[]> ReadImmutableAsync(ObjectVersion version, CancellationToken token)
    {
        ValidateArchiveEnrollment();
        if (version.VersionId != version.Sha256 || version.RetainUntil > profile.RetainUntil || version.RetainUntil <= clock.GetUtcNow())
            throw new AuthorityUnavailableException("Pinned retained original required.");
        byte[] bytes = await ReadBoundedAsync(Resolve(version.Key), token);
        if (bytes.Length != version.Bytes || CustodyJson.Hash(bytes) != version.Sha256) throw new AuthorityUnavailableException("Original bytes substituted.");
        return bytes;
    }
    public async Task<CurrentState> CommitAsync(CurrentState expected, Checkpoint next, CancellationToken token)
    {
        if (next.TenantId != expected.Checkpoint.TenantId || next.EnrollmentDigest != profile.EnrollmentDigest
            || next.Revision != expected.Head.Revision + 1 || next.PredecessorDigest != expected.Head.Digest
            || expected.Archive is null || string.IsNullOrWhiteSpace(expected.ExactHeadAttribute))
            throw new AuthorityUnavailableException("Exact forward authority transition required.");
        bool admission = next.Originals.Count > expected.Checkpoint.Originals.Count || next.NativeIntents.Count > expected.Checkpoint.NativeIntents.Count
            || next.Deliveries.Count > expected.Checkpoint.Deliveries.Count || next.AllocationExclusions.Count > expected.Checkpoint.AllocationExclusions.Count;
        var plan = CheckpointArchive.Prepare(next, expected.Archive, 32 * 1024 * 1024 - (admission ? profile.CompletionReserveBytes : 0));
        byte[] bytes = await CheckpointArchive.WriteAsync(plan, PutImmutableAsync, token);
        var version = await PutImmutableAsync(bytes, "checkpoint", token);
        var head = new AuthorityHead(next.Revision, version.Sha256, version, profile.EnrollmentDigest);
        if (!await dapr.TrySaveStateAsync(installation.AuthorityComponent, Key(next.TenantId), head, expected.ExactHeadAttribute,
            new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong }, cancellationToken: token))
            throw new AuthorityConflictException();
        // Re-read after success; uncertainty retains all originals and must be looked up rather than resent.
        return await ReadAsync(next.TenantId, token);
    }
    private void ValidateArchiveEnrollment()
    {
        foreach (string directory in new[] { installation.CheckpointArchiveDirectory, installation.OriginalArchiveDirectory })
        {
            var info = new DirectoryInfo(directory);
            var enrollment = new FileInfo(Path.Combine(directory, "enrollment.json"));
            if (!info.Exists || info.LinkTarget is not null || !enrollment.Exists || enrollment.LinkTarget is not null || enrollment.Length is <= 0 or > 65536
                || CustodyJson.Hash(File.ReadAllBytes(enrollment.FullName)) != installation.ArchiveEnrollmentSha256)
                throw new AuthorityUnavailableException("Independent archive enrollment/placement unavailable.");
        }
    }
    private string Resolve(string key)
    {
        if (key.Length > 2048 || key.Contains("..", StringComparison.Ordinal) || key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '-')))
            throw new AuthorityUnavailableException("Invalid immutable locator.");
        string root = key.StartsWith("checkpoint/", StringComparison.Ordinal) ? installation.CheckpointArchiveDirectory : installation.OriginalArchiveDirectory;
        string path = Path.GetFullPath(Path.Combine(root, key));
        if (!path.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new AuthorityUnavailableException("Archive boundary denied.");
        for (DirectoryInfo? parent = new(Path.GetDirectoryName(path)!); parent is not null && parent.FullName.StartsWith(Path.GetFullPath(root), StringComparison.Ordinal); parent = parent.Parent)
            if (parent.Exists && parent.LinkTarget is not null) throw new AuthorityUnavailableException("Archive symbolic link denied.");
        if (File.Exists(path) && new FileInfo(path).LinkTarget is not null) throw new AuthorityUnavailableException("Immutable object symbolic link denied.");
        return path;
    }
    private static async Task<byte[]> ReadBoundedAsync(string path, CancellationToken token)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        if (input.Length is <= 0 or > CustodyBindingContract.MaximumOriginalBytes) throw new AuthorityUnavailableException("Original exceeds bound.");
        byte[] bytes = new byte[(int)input.Length]; await input.ReadExactlyAsync(bytes, token); return bytes;
    }
}
