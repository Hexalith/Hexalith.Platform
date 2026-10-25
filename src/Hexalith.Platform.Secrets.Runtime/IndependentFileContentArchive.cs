using System.Security.Cryptography;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Durable immutable files on the exact independently enrolled archive volume; the current authority head selects only a content hash.</summary>
public sealed class IndependentFileContentArchive(string directory):IIndependentContentArchive
{
    /// <inheritdoc />
    public async Task<string> PublishAsync(ReadOnlyMemory<byte> bytes,CancellationToken token)
    {
        string hash=Convert.ToHexString(SHA256.HashData(bytes.Span));await ImmutableFileArchive.PublishAsync(PathFor(hash),bytes,token).ConfigureAwait(false);return hash;
    }
    /// <inheritdoc />
    public async Task<byte[]> ReadAsync(string sha256,CancellationToken token)
    {
        string path=PathFor(sha256);var info=new FileInfo(path);
        if(!info.Exists || info.LinkTarget is not null || info.Length is 0 or >CustodyBindingContract.MaximumOriginalBytes) throw new IOException("Exact immutable private original unavailable.");
        byte[] bytes=await File.ReadAllBytesAsync(path,token).ConfigureAwait(false);
        if(Convert.ToHexString(SHA256.HashData(bytes))!=sha256) throw new IOException("Private original digest mismatch.");return bytes;
    }
    private string PathFor(string hash)=>CustodyBindingContract.Digest(hash)?Path.Combine(directory,hash[..2],hash+".json"):throw new IOException("Exact archive digest required.");
}
