using System.Security.Cryptography;
using System.Text.Json;
using System.Globalization;
using Dapr.Client;

namespace Hexalith.Platform.Custody;

// Narrow exact GetSecret binding only. No list, default version, codec detection, key creation, or key-family fallback exists.
internal sealed class DaprExportQualificationReferenceReader(DaprClient client,IPlatformKeyInventoryActor inventory,IPlatformKeyInventoryAuthority authority)
{
    internal async Task<byte[]?> ReadAsync(DaprExportQualificationReference reference,bool active,Func<string,byte[]> decode,CancellationToken token)
    {
        if(new[]{reference.Component,reference.Key,reference.Version,reference.Field}.Any(value=>string.IsNullOrWhiteSpace(value) || value.Length>2048 || value.Any(char.IsControl))) return null;
        if(!ulong.TryParse(reference.Version,NumberStyles.None,CultureInfo.InvariantCulture,out ulong nativeVersion) || nativeVersion==0
            || nativeVersion.ToString(CultureInfo.InvariantCulture)!=reference.Version) return null;
        var before=await CurrentAsync(reference,active,token).ConfigureAwait(false);if(before is null) return null;
        Dictionary<string,string>? response=null;byte[]? decoded=null;bool transferred=false;
        try
        {
            var pending=client.GetSecretAsync(reference.Component,reference.Key,new Dictionary<string,string>{{"version_id",reference.Version}},token);
            response=await pending.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if(response.Count!=1 || !response.TryGetValue(reference.Field,out string? field) || field.Length is 0 or >16384) return null;
            decoded=decode(field);response.Clear();response=null;
            var after=await CurrentAsync(reference,active,token).ConfigureAwait(false);
            if(after is null || !Same(before,after)) return null;
            transferred=true;return decoded;
        }
        finally { response?.Clear();if(!transferred && decoded is not null) CryptographicOperations.ZeroMemory(decoded); }
    }
    internal async Task<PlatformKeyInventorySnapshot?> CurrentAsync(DaprExportQualificationReference reference,bool active,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var key=reference.InventoryKey;
        if(!await authority.AuthorizeOperationAsync(key,"ResolveExportQualificationMaterial",token).ConfigureAwait(false)) return null;
        token.ThrowIfCancellationRequested();var state=await inventory.ReadAsync(key).ConfigureAwait(false);token.ThrowIfCancellationRequested();
        if(state.TenantId!=key.TenantId || state.Purpose!=key.Purpose || state.KeyAlias!=key.KeyAlias || state.Revision<1
            || state.Versions.Count(value=>value.Key==key && (value.State==PlatformHmacKeyState.Active || !active && value.State==PlatformHmacKeyState.Retained))!=1
            || !await authority.ValidateStateAsync(key,state.Revision,Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state))),token).ConfigureAwait(false)) return null;
        return state;
    }
    internal static bool Same(PlatformKeyInventorySnapshot left,PlatformKeyInventorySnapshot right)=>JsonSerializer.Serialize(left)==JsonSerializer.Serialize(right);
    internal static byte[] Base64(string value,int? length=null)
    {
        byte[] bytes=Convert.FromBase64String(value);
        if(Convert.ToBase64String(bytes)!=value || length is { } count && bytes.Length!=count) { CryptographicOperations.ZeroMemory(bytes);throw new CryptographicException("Exact configured export asset codec required."); }
        return bytes;
    }
    internal static byte[] Pem(string value,string label)
    {
        ReadOnlySpan<char> text=value.AsSpan().Trim();
        if(!PemEncoding.TryFind(text,out var fields) || !text[fields.Label].SequenceEqual(label) || fields.Location.GetOffsetAndLength(text.Length)!=(0,text.Length)) throw new CryptographicException("Exact configured PEM asset required.");
        byte[] bytes=new byte[fields.DecodedDataLength];
        if(!Convert.TryFromBase64Chars(text[fields.Base64Data],bytes,out int written) || written!=bytes.Length) { CryptographicOperations.ZeroMemory(bytes);throw new CryptographicException("Exact PEM encoding required."); }
        return bytes;
    }
}
