using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Exact current private workload read proof adapter. Only independently enrolled streams and explicitly mounted issuer assertions are usable; command submission is never authorized.</summary>
public sealed class QualificationGatewayReadProofs(QualificationMountedReadCredentials credentials,ICustodyOwnerStateAuthority currentAuthority,
    IReadOnlyList<Hexalith.EventStore.Contracts.Identity.AggregateIdentity> streams,TimeProvider clock):IEventStoreGatewayWorkloadAssertionSource,IEventStoreGatewayStreamReadProofSource
{
    /// <inheritdoc />
    public ValueTask<string?> IssueAsync(string operation,string tenant,CancellationToken token)=>IssueAsync(operation,tenant,string.Empty,token);
    /// <inheritdoc />
    public async ValueTask<string?> IssueAsync(string operation,string tenant,string domain,CancellationToken token)
    {
        if(operation is not (EventStoreGatewayWorkloadOperations.CommandStatus or EventStoreGatewayWorkloadOperations.StreamRead)
            || !streams.Any(value=>value.TenantId==tenant && (domain.Length==0 || value.Domain==domain))) return null;
        string id=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { operation,tenant,domain })));
        if(!credentials.AssertionFiles.TryGetValue(id,out var file) || !Path.IsPathFullyQualified(file)
            || !await currentAuthority.AuthorizeOperationAsync(id,"ReadQualificationWorkloadAssertion",token).ConfigureAwait(false)) return null;
        var info=new FileInfo(file);if(!info.Exists || info.Length is <=0 or >16384) return null;
        string assertion=(await File.ReadAllTextAsync(file,token).ConfigureAwait(false)).Trim();
        // The EventStore gateway independently verifies issuer signature, audience, operation, resource and freshness. No token claim is treated as native evidence here.
        return await currentAuthority.AuthorizeOperationAsync(id,"ReadQualificationWorkloadAssertion",token).ConfigureAwait(false)?assertion:null;
    }
    /// <inheritdoc />
    public async ValueTask<string?> IssueAsync(StreamReadRequest request,CancellationToken token)
    {
        if(!streams.Any(value=>value.TenantId==request.Tenant && value.Domain==request.Domain && value.AggregateId==request.AggregateId)
            || !Path.IsPathFullyQualified(credentials.RsaPkcs8Base64File)) return null;
        string id=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
        if(!await currentAuthority.AuthorizeOperationAsync(id,"ReadQualificationNativeStream",token).ConfigureAwait(false)) return null;
        var info=new FileInfo(credentials.RsaPkcs8Base64File);if(!info.Exists || info.Length is <=0 or >16384) return null;
        string material=(await File.ReadAllTextAsync(credentials.RsaPkcs8Base64File,token).ConfigureAwait(false)).Trim();
        string proof=BoundStreamReadProof.Issue(request,credentials.Caller,material,clock.GetUtcNow());
        return await currentAuthority.AuthorizeOperationAsync(id,"ReadQualificationNativeStream",token).ConfigureAwait(false)?proof:null;
    }
}
