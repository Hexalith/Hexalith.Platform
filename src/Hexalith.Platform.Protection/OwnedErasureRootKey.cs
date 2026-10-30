using System.Security.Cryptography;

namespace Hexalith.Platform.Protection;

/// <summary>One bounded owned plaintext root. Disposal clears its bytes; the durable holder remains restrictive until independently completed.</summary>
/// <param name="Bytes">Exact AES-256 root owned by this holder.</param>
/// <param name="HolderId">Original durable holder identity.</param>
public sealed class OwnedErasureRootKey(byte[] Bytes, string HolderId) : IDisposable
{
    /// <summary>Gets the one owned buffer; it must not be copied into domain records, logs or evidence.</summary>
    public byte[] Bytes { get; } = Bytes;
    /// <summary>Gets the exact original holder identity required by completion.</summary>
    public string HolderId { get; } = HolderId;
    private int _disposed;
    /// <summary>Gets whether the actual issued instance completed disposal; zero bytes alone do not establish quiescence.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
    /// <inheritdoc />
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(Bytes);
        Volatile.Write(ref _disposed, 1);
    }
    /// <inheritdoc />
    public override string ToString() => nameof(OwnedErasureRootKey);
}
