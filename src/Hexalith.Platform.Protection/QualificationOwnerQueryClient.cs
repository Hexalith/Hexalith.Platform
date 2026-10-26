using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Actual restricted owner-authority query transport. It uses an enrolled issuer assertion and exact signed requester/query proof under independently current read permission.</summary>
public sealed class QualificationOwnerQueryClient(HttpClient http,QualificationMountedReadCredentials credentials,ICustodyOwnerStateAuthority authority,
    IReadOnlyList<SubmitQueryRequest> enrolledQueries,TimeProvider clock)
{
    /// <summary>Computes the exact current authorization identity, including the complete query payload and private principal.</summary>
    public static string ScopeId(BoundOwnerQueryRequest request)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request,new JsonSerializerOptions(JsonSerializerDefaults.Web))));
    /// <summary>Reads only explicitly enrolled closed queries through the published native receiving proof route.</summary>
    public async Task<EventStoreQueryResult?> ReadAsync(SubmitQueryRequest query,CancellationToken token)
    {
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(token);budget.CancelAfter(TimeSpan.FromSeconds(30));
        if(http.BaseAddress is not { IsAbsoluteUri:true,Scheme:"https" } endpoint || endpoint.AbsolutePath!="/" || endpoint.UserInfo.Length!=0
            || string.IsNullOrWhiteSpace(credentials.OwnerQueryPrincipal) || !Closed(query)
            || !enrolledQueries.Any(value=>JsonSerializer.Serialize(value)==JsonSerializer.Serialize(query))) return null;
        var request=new BoundOwnerQueryRequest(query,credentials.OwnerQueryPrincipal);string scope=ScopeId(request);
        if(!await authority.AuthorizeOperationAsync(scope,"ReadQualificationOwnerQuery",budget.Token).ConfigureAwait(false)) return null;
        string assertionId=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { operation="eventstore:owner-authority-query",tenant=query.Tenant,domain=query.Domain })));
        if(!credentials.AssertionFiles.TryGetValue(assertionId,out var assertionFile)) return null;
        string? assertion=await Mounted(assertionFile,budget.Token).ConfigureAwait(false),key=await Mounted(credentials.RsaPkcs8Base64File,budget.Token).ConfigureAwait(false);
        if(assertion is null || key is null) return null;
        byte[] body=JsonSerializer.SerializeToUtf8Bytes(request,new JsonSerializerOptions(JsonSerializerDefaults.Web));if(body.Length>1024*1024) return null;
        using var message=new HttpRequestMessage(HttpMethod.Post,"api/v1/queries/owner-authority/workload") { Content=new ByteArrayContent(body) };
        message.Content.Headers.ContentType=new("application/json");message.Headers.Add("X-Hexalith-Workload-Assertion",assertion);
        message.Headers.Add(BoundOwnerQueryProof.HeaderName,BoundOwnerQueryProof.Issue(request,credentials.Caller,key,clock.GetUtcNow()));
        if(!await authority.AuthorizeOperationAsync(scope,"ReadQualificationOwnerQuery",budget.Token).ConfigureAwait(false)) return null;
        using var response=await http.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,budget.Token).ConfigureAwait(false);
        if(response.StatusCode!=System.Net.HttpStatusCode.OK || response.RequestMessage?.RequestUri is { } actual && actual!=new Uri(endpoint,"api/v1/queries/owner-authority/workload")) return null;
        await using var input=await response.Content.ReadAsStreamAsync(budget.Token).ConfigureAwait(false);
        string? json=await DaprSecretsInvoker.ReadBoundedAsync(input,budget.Token,1024*1024).ConfigureAwait(false);if(json is null) return null;
        CustodyBindingContract.RejectDuplicateProperties(Encoding.UTF8.GetBytes(json));var result=JsonSerializer.Deserialize<SubmitQueryResponse>(json,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if(result is not { Success:true } || !await authority.AuthorizeOperationAsync(scope,"ReadQualificationOwnerQuery",budget.Token).ConfigureAwait(false)) return null;
        budget.Token.ThrowIfCancellationRequested();return new(result.CorrelationId,result.Payload.Clone(),false,response.Headers.ETag?.Tag) { Metadata=result.Metadata };
    }
    private static bool Closed(SubmitQueryRequest query)=>query.Payload is not null && query.AdditionalProperties is not { Count:>0 }
        && query.EntityId is null && query.ProjectionType is null && query.ProjectionActorType is null && query.Paging is null
        && query.Search is null && query.Filters is null && query.OrderBy is null && query.Freshness is null;
    private static async Task<string?> Mounted(string file,CancellationToken token)
    {
        if(!Path.IsPathFullyQualified(file)) return null;var info=new FileInfo(file);
        if(!info.Exists || info.LinkTarget is not null || info.Length is <=0 or >16384) return null;
        string value=(await File.ReadAllTextAsync(file,token).ConfigureAwait(false)).Trim();return value.Length>0?value:null;
    }
}
