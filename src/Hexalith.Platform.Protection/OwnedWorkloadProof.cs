using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Contracts.Queries;

namespace Hexalith.Platform.Protection;

// Exact wire compatibility with current EventStore BoundStreamReadProof/BoundWorkloadCommandProof.
// Only the intentionally returned assertion has transport lifetime; private key/DER never becomes a string.
internal static class OwnedWorkloadProof
{
    private static readonly AsyncLocal<Action<byte[]>?> Inspection=new();
    internal static Action<byte[]>? DecodedPrivateKeyInspection { get=>Inspection.Value;set=>Inspection.Value=value; }
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    internal static string Issue(BoundOwnerQueryRequest request,string caller,ReadOnlySpan<char> privateKey,DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);ArgumentNullException.ThrowIfNull(request.Query);
        if(string.IsNullOrWhiteSpace(request.PrincipalId) || request.PrincipalId.Length>200 || string.IsNullOrWhiteSpace(request.Query.AggregateId)
            || request.Query.AggregateId.Length>256 || string.IsNullOrWhiteSpace(request.Query.QueryType) || request.Query.QueryType.Length>300)
            throw new ArgumentException("Invalid owner query binding.");
        byte[] canonical=JsonSerializer.SerializeToUtf8Bytes(request,Json);
        try { return Issue(new StreamReadRequest(request.Query.Tenant,request.Query.Domain,Convert.ToHexString(SHA256.HashData(canonical))),caller,privateKey,now); }
        finally { CryptographicOperations.ZeroMemory(canonical); }
    }
    internal static string Issue(BoundWorkloadCommandRequest request,string caller,ReadOnlySpan<char> privateKey,DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);ArgumentNullException.ThrowIfNull(request.Command);
        if(string.IsNullOrWhiteSpace(request.PrincipalId) || request.PrincipalId.Length>200 || string.IsNullOrWhiteSpace(request.Command.MessageId)
            || request.Command.MessageId.Length>200 || string.IsNullOrWhiteSpace(request.Command.CommandType) || request.Command.CommandType.Length>300
            || request.Command.Payload.ValueKind!=JsonValueKind.Object) throw new ArgumentException("Invalid bound workload command.");
        byte[] canonical=JsonSerializer.SerializeToUtf8Bytes(request,Json);
        try { return Issue(new StreamReadRequest(request.Command.Tenant,request.Command.Domain,Convert.ToHexString(SHA256.HashData(canonical))),caller,privateKey,now); }
        finally { CryptographicOperations.ZeroMemory(canonical); }
    }
    internal static string Issue(StreamReadRequest request,string caller,ReadOnlySpan<char> privateKey,DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);ArgumentException.ThrowIfNullOrWhiteSpace(caller);
        if(privateKey.IsWhiteSpace() || privateKey.Length>16384 || now.Offset!=TimeSpan.Zero || string.IsNullOrWhiteSpace(request.Tenant)
            || string.IsNullOrWhiteSpace(request.Domain) || string.IsNullOrWhiteSpace(request.AggregateId)
            || request.Tenant.Length>200 || request.Domain.Length>200 || request.AggregateId.Length>256) throw new ArgumentException("Invalid bound stream read scope.");
        byte[] der=new byte[checked((privateKey.Length+3)/4*3)];
        try
        {
            Inspection.Value?.Invoke(der);
            if(!Convert.TryFromBase64Chars(privateKey,der,out int used)) throw new FormatException("Invalid private key encoding.");
            using var rsa=RSA.Create();rsa.ImportPkcs8PrivateKey(der.AsSpan(0,used),out _);
            if(rsa.KeySize<2048) throw new ArgumentException("Bound workload RSA key must be at least2048bits.");
            var payload=new { version=1,caller,tenant=request.Tenant,domain=request.Domain,aggregateId=request.AggregateId,
                issuedAt=now.ToUnixTimeSeconds(),expiresAt=now.AddSeconds(30).ToUnixTimeSeconds(),nonce=Convert.ToHexString(RandomNumberGenerator.GetBytes(16)) };
            string body=Encode(JsonSerializer.SerializeToUtf8Bytes(payload,Json));
            return body+"."+Encode(rsa.SignData(Encoding.ASCII.GetBytes(body),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1));
        }
        finally { CryptographicOperations.ZeroMemory(der); }
    }
    private static string Encode(byte[] bytes)=>Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
}
