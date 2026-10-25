using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Exact encrypted persisted canary and occurrence; no plaintext or key bytes.</summary>
public sealed record Fr34StoredCanary(Fr34CanaryReference Reference, AggregateIdentity Owner, ProtectionTarget ProtectionTarget,
    PayloadProtectionOccurrenceContext Occurrence, PayloadProtectionResult Protected);

/// <summary>Separately mounted independently enrolled immutable canary persistence. Reader instances retain no engine buffers.</summary>
public sealed class Fr34CanaryArchive(string directory, string enrollmentSha256) : IFr34PersistedCanaryReader
{
    private string PathFor(string id)
    {
        if (id.Length != 64 || !id.All(char.IsAsciiHexDigit)) throw new ArgumentException("Exact canary locator required.");
        var enrollment = new FileInfo(Path.Combine(directory, "enrollment.json"));
        if (!Path.IsPathFullyQualified(directory) || new DirectoryInfo(directory).LinkTarget is not null || !enrollment.Exists
            || enrollment.LinkTarget is not null || enrollment.Length is 0 or > 65536
            || Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(enrollment.FullName))) != enrollmentSha256)
            throw new InvalidOperationException("Independent canary archive enrollment required.");
        return Path.Combine(directory, id + ".json");
    }
    /// <summary>Flushes and publishes the ciphertext original before source completion.</summary>
    public Task PublishAsync(Fr34StoredCanary original, CancellationToken token)
        => ImmutableFileArchive.PublishAsync(PathFor(original.Reference.CanaryId), JsonSerializer.SerializeToUtf8Bytes(original), token);
    /// <summary>Reads exact independently persisted bytes and verifies the full reference.</summary>
    public async Task<Fr34StoredCanary?> ReadOriginalAsync(Fr34CanaryReference reference, CancellationToken token)
    {
        string path = PathFor(reference.CanaryId); var info = new FileInfo(path);
        if (!info.Exists) return null;
        if (info.LinkTarget is not null || info.Length is <= 0 or > 65536) throw new InvalidOperationException("Bounded immutable canary required.");
        byte[] bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
        CustodyBindingContract.RejectDuplicateProperties(bytes);
        var original = JsonSerializer.Deserialize<Fr34StoredCanary>(bytes, CustodyBindingContract.Json);
        return original?.Reference == reference && original.Owner.TenantId == reference.Target.TenantId
            && original.ProtectionTarget.TenantId == reference.Target.TenantId && original.ProtectionTarget.AgentInteractionId == reference.CanaryId
            && original.Protected.Metadata.KeyAlias == reference.KeyReference ? original : null;
    }
    /// <inheritdoc />
    public async Task<Fr34PersistedCanary?> ReadAsync(Fr34CanaryReference reference, CancellationToken cancellationToken = default)
        => await ReadOriginalAsync(reference, cancellationToken).ConfigureAwait(false) is { } value
            ? new(reference, value.Protected.PayloadBytes.ToArray(), value.Protected.Metadata) : null;
}
