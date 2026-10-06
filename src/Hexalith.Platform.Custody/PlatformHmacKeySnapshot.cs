using System.Security.Cryptography;

namespace Hexalith.Platform.Custody;

/// <summary>Owns a short-lived key copy which cannot be exported or serialized.</summary>
public sealed class PlatformHmacKeySnapshot : IDisposable
{
    private readonly byte[] _key;
    private bool _disposed;

    /// <summary>Copies provider material; the provider must clear its input after construction.</summary>
    public PlatformHmacKeySnapshot(PlatformHmacScope scope, PlatformHmacKeyMetadata metadata,
        ReadOnlySpan<byte> key)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(metadata);
        if (!scope.IsValid || string.IsNullOrWhiteSpace(metadata.Version) || key.Length < 32
            || !Enum.IsDefined(metadata.State) || metadata.VerifyUntil <= metadata.NotBefore
            || (metadata.State == PlatformHmacKeyState.Retained && metadata.RetiredAt is null))
        {
            throw new ArgumentException("Custody key snapshot is invalid.");
        }

        Scope = scope;
        Metadata = metadata;
        _key = key.ToArray();
    }

    /// <summary>Gets the exact scope authenticated by the provider.</summary>
    public PlatformHmacScope Scope { get; }

    /// <summary>Gets content-free lifecycle facts.</summary>
    public PlatformHmacKeyMetadata Metadata { get; }

    /// <summary>Computes a tag without exporting the key.</summary>
    internal byte[] ComputeTag(ReadOnlySpan<byte> canonicalBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return HMACSHA256.HashData(_key, canonicalBytes);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            CryptographicOperations.ZeroMemory(_key);
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public override string ToString() => "PlatformHmacKeySnapshot";
}
