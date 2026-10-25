using System.Security.Cryptography;

namespace Hexalith.Platform.Custody;

/// <summary>Dedicated owned ephemeral export material, never serialized or returned by a qualification operation.</summary>
public sealed class ExportQualificationMaterial:IDisposable
{
    private readonly byte[] _exportKey,_kek;
    private readonly ECDsa? _signer;
    private bool _disposed;
    /// <summary>Takes ownership of dedicated export-family buffers and optional export signer; the resolver owns independent native holder admission.</summary>
    public ExportQualificationMaterial(byte[]? exportKey,byte[]? kek,ECDsa? exportSigner=null)
    {
        if(exportKey is not null && exportKey.Length!=32 || kek is not null && kek.Length!=32 || exportSigner is null && (exportKey is null || kek is null)) throw new ArgumentException("Exact dedicated export material required.");
        _exportKey=exportKey??[];_kek=kek??[];_signer=exportSigner;
    }
    internal ReadOnlySpan<byte> ExportKey { get { ObjectDisposedException.ThrowIf(_disposed,this);return _exportKey; } }
    internal ReadOnlySpan<byte> Kek { get { ObjectDisposedException.ThrowIf(_disposed,this);return _kek; } }
    internal ECDsa Signer { get { ObjectDisposedException.ThrowIf(_disposed,this);return _signer??throw new InvalidOperationException("Dedicated export signing material unavailable."); } }
    internal bool Cleared=>_disposed && _exportKey.AsSpan().IndexOfAnyExcept((byte)0)<0 && _kek.AsSpan().IndexOfAnyExcept((byte)0)<0;
    /// <inheritdoc />
    public void Dispose() { if(_disposed) return;CryptographicOperations.ZeroMemory(_exportKey);CryptographicOperations.ZeroMemory(_kek);_signer?.Dispose();_disposed=true; }
}
