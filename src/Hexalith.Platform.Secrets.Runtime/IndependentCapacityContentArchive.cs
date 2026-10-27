using System.Security.Cryptography;

namespace Hexalith.Platform.Secrets.Runtime;

internal sealed class IndependentCapacityContentArchive(string directory,IndependentArchiveCapacity capacity,string ticket,bool completing=false):IIndependentContentArchive
{
    private readonly IndependentFileContentArchive _reader=new(directory);
    public async Task<string> PublishAsync(ReadOnlyMemory<byte> bytes,CancellationToken token)
    {
        string hash=Convert.ToHexString(SHA256.HashData(bytes.Span));
        await capacity.PublishAsync(ticket,Path.Combine(directory,hash[..2],hash+".json"),bytes,token,completing).ConfigureAwait(false);return hash;
    }
    public Task<byte[]> ReadAsync(string hash,CancellationToken token)=>_reader.ReadAsync(hash,token);
}
