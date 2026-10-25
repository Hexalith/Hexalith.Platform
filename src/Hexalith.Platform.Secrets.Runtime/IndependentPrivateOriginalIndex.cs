using System.Text.Json;

namespace Hexalith.Platform.Secrets.Runtime;

// Persistent AVL Merkle index: the single CAS head contains one root/count. Every body/status version remains in an immutable linked history.
internal sealed class IndependentPrivateOriginalIndex(IIndependentContentArchive archive)
{
    internal sealed record Entry(string Role,string RequestId,IndependentOriginal Original,string? PreviousValueSha256);
    private sealed record Node(string KeySha256,string ValueSha256,string? Left,string? Right,int Height);
    private readonly Dictionary<string,Node> _nodes=new(StringComparer.Ordinal);
    internal async Task<Entry?> ReadAsync(string? root,string role,string requestId,CancellationToken token)
    {
        var value=await FindAsync(root,Key(role,requestId),token).ConfigureAwait(false);if(value is null) return null;
        byte[] bytes=await archive.ReadAsync(value,token).ConfigureAwait(false);CustodyBindingContract.RejectDuplicateProperties(bytes);
        var entry=JsonSerializer.Deserialize<Entry>(bytes,CustodyBindingContract.Json)??throw new IOException("Exact private original required.");
        if(entry.Role!=role || entry.RequestId!=requestId) throw new IOException("Private original identity substitution.");return entry;
    }
    internal async Task<(string Root,bool First)> SetAsync(string? root,string role,string requestId,IndependentOriginal original,CancellationToken token)
    {
        string key=Key(role,requestId);string? priorHash=await FindAsync(root,key,token).ConfigureAwait(false);
        if(priorHash is not null)
        {
            var prior=await ReadAsync(root,role,requestId,token).ConfigureAwait(false)??throw new IOException("Original disappeared.");
            if(!Same(prior.Original.Request,original.Request) || prior.Original.Result is { } recorded && !Same(recorded,original.Result)) throw new IOException("Immutable original/result substitution.");
            if(Same(prior.Original,original)) return (root!,false);
        }
        string value=await archive.PublishAsync(JsonSerializer.SerializeToUtf8Bytes(new Entry(role,requestId,original,priorHash),CustodyBindingContract.Json),token).ConfigureAwait(false);
        return (await InsertAsync(root,key,value,token).ConfigureAwait(false),priorHash is null);
    }
    private async Task<string?> FindAsync(string? root,string key,CancellationToken token)
    {
        string? lower=null,upper=null;
        for(int depth=0;root is not null && depth<64;depth++)
        {
            var node=await LoadAsync(root,token).ConfigureAwait(false);
            if(lower is not null && string.CompareOrdinal(node.KeySha256,lower)<=0 || upper is not null && string.CompareOrdinal(node.KeySha256,upper)>=0) throw new IOException("Malformed original index ordering.");
            int comparison=string.CompareOrdinal(key,node.KeySha256);if(comparison==0) return node.ValueSha256;
            if(comparison<0) { upper=node.KeySha256;root=node.Left; } else { lower=node.KeySha256;root=node.Right; }
        }
        if(root is not null) throw new IOException("Bounded original index depth exceeded.");return null;
    }
    private async Task<string> InsertAsync(string? root,string key,string value,CancellationToken token)
    {
        if(root is null) return await SaveAsync(new(key,value,null,null,1),token).ConfigureAwait(false);
        var node=await LoadAsync(root,token).ConfigureAwait(false);int comparison=string.CompareOrdinal(key,node.KeySha256);
        if(comparison==0) return await SaveAsync(node with { ValueSha256=value },token).ConfigureAwait(false);
        node=comparison<0?node with { Left=await InsertAsync(node.Left,key,value,token).ConfigureAwait(false) }:node with { Right=await InsertAsync(node.Right,key,value,token).ConfigureAwait(false) };
        node=await HeightAsync(node,token).ConfigureAwait(false);
        int balance=await HeightOfAsync(node.Left,token).ConfigureAwait(false)-await HeightOfAsync(node.Right,token).ConfigureAwait(false);
        if(balance>1)
        {
            var left=await LoadAsync(node.Left!,token).ConfigureAwait(false);
            if(string.CompareOrdinal(key,left.KeySha256)>0) node=node with { Left=await RotateLeftAsync(left,token).ConfigureAwait(false) };
            return await RotateRightAsync(node,token).ConfigureAwait(false);
        }
        if(balance< -1)
        {
            var right=await LoadAsync(node.Right!,token).ConfigureAwait(false);
            if(string.CompareOrdinal(key,right.KeySha256)<0) node=node with { Right=await RotateRightAsync(right,token).ConfigureAwait(false) };
            return await RotateLeftAsync(node,token).ConfigureAwait(false);
        }
        return await SaveAsync(node,token).ConfigureAwait(false);
    }
    private async Task<string> RotateLeftAsync(Node node,CancellationToken token)
    {
        var right=await LoadAsync(node.Right!,token).ConfigureAwait(false);
        string left=await SaveAsync(await HeightAsync(node with { Right=right.Left },token).ConfigureAwait(false),token).ConfigureAwait(false);
        return await SaveAsync(await HeightAsync(right with { Left=left },token).ConfigureAwait(false),token).ConfigureAwait(false);
    }
    private async Task<string> RotateRightAsync(Node node,CancellationToken token)
    {
        var left=await LoadAsync(node.Left!,token).ConfigureAwait(false);
        string right=await SaveAsync(await HeightAsync(node with { Left=left.Right },token).ConfigureAwait(false),token).ConfigureAwait(false);
        return await SaveAsync(await HeightAsync(left with { Right=right },token).ConfigureAwait(false),token).ConfigureAwait(false);
    }
    private async Task<Node> HeightAsync(Node node,CancellationToken token)=>node with { Height=checked(Math.Max(await HeightOfAsync(node.Left,token).ConfigureAwait(false),await HeightOfAsync(node.Right,token).ConfigureAwait(false))+1) };
    private async Task<int> HeightOfAsync(string? root,CancellationToken token)=>root is null?0:(await LoadAsync(root,token).ConfigureAwait(false)).Height;
    private async Task<Node> LoadAsync(string hash,CancellationToken token)
    {
        if(_nodes.TryGetValue(hash,out var node)) return node;
        byte[] bytes=await archive.ReadAsync(hash,token).ConfigureAwait(false);CustodyBindingContract.RejectDuplicateProperties(bytes);
        node=JsonSerializer.Deserialize<Node>(bytes,CustodyBindingContract.Json)??throw new IOException("Exact index node required.");
        if(!CustodyBindingContract.Digest(node.KeySha256) || !CustodyBindingContract.Digest(node.ValueSha256) || node.Left is not null && !CustodyBindingContract.Digest(node.Left)
            || node.Right is not null && !CustodyBindingContract.Digest(node.Right) || node.Height is <1 or >64) throw new IOException("Malformed immutable index node.");
        _nodes.Add(hash,node);return node;
    }
    private async Task<string> SaveAsync(Node node,CancellationToken token)
    { string hash=await archive.PublishAsync(JsonSerializer.SerializeToUtf8Bytes(node,CustodyBindingContract.Json),token).ConfigureAwait(false);_nodes[hash]=node;return hash; }
    private static string Key(string role,string id)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { role,id },CustodyBindingContract.Json)));
    private static bool Same<T,U>(T left,U right)=>JsonSerializer.SerializeToUtf8Bytes(left,CustodyBindingContract.Json).AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right,CustodyBindingContract.Json));
}
