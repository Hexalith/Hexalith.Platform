using System.Security.Cryptography;

namespace Hexalith.Platform.Custody;

/// <summary>Private short-lived owned key copy; no raw property, serialization or diagnostic export.</summary>
internal sealed class OwnedExportKey : IDisposable
{
    private readonly byte[] _bytes;
    private bool _disposed;

    /// <summary>Takes ownership of an exact decrypted key buffer.</summary>
    internal OwnedExportKey(byte[] bytes) => _bytes = bytes;

    /// <summary>Copies only inside a future authenticated custody backend; never a public response.</summary>
    internal void CopyTo(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _bytes.CopyTo(destination);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            CryptographicOperations.ZeroMemory(_bytes);
            _disposed = true;
        }
    }

    /// <inheritdoc/>
    public override string ToString() => nameof(OwnedExportKey);
}
