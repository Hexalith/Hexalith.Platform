using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Owned ephemeral root. Dispose clears its backing array; completion still requires independent holder evidence.</summary>
public sealed class CustodyReleasedKey : IDisposable
{
    private byte[]? _bytes;
    internal CustodyReleasedKey(byte[] bytes) => _bytes = bytes;
    /// <summary>Borrowed key bytes, valid only before disposal. Consumers must register every additional copy.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ReadOnlyMemory<byte> Bytes => _bytes ?? throw new ObjectDisposedException(nameof(CustodyReleasedKey));
    // Transfers the sole owned native response buffer to a private occurrence owner; no additional plaintext copy.
    internal byte[] TransferOwnership() => Interlocked.Exchange(ref _bytes,null) ?? throw new ObjectDisposedException(nameof(CustodyReleasedKey));
    /// <inheritdoc />
    public void Dispose()
    {
        byte[]? bytes = Interlocked.Exchange(ref _bytes, null);
        if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
    }
}
