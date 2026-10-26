using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Concrete common audit portion using real authenticated originals, native source-v1 streams, workload command status and exact enrolled projection queries.</summary>
public sealed class QualificationEventStoreAuditReader(IndependentDaprAuthority originalAuthority,IEventStoreGatewayClient privateGateway,
    TimeProvider clock,IReadOnlyDictionary<string,QualificationEventStoreAuditScope> enrolledScopes,QualificationOwnerQueryClient? projectionQueries=null)
{
    /// <summary>Reads actual bytes/state only. It never turns a source buffer or missing log/trace/restore protocol into persistence, receipt positions, or a zero-effects assertion.</summary>
    public async Task<QualificationEventStoreAuditSnapshot?> ReadAsync(JsonObject request,CancellationToken cancellationToken)
    {
        string? id=SecretsProof.Text(request,"caseId"),requestId=SecretsProof.Text(request,"requestId");
        if(id is null || requestId is null || !enrolledScopes.TryGetValue(id,out var scope) || request["enrolledScope"] is not JsonObject enrolled
            || QualificationProtocolSchema.Digest(enrolled)!=scope.EnrolledScopeSha256 || SecretsProof.Text(enrolled,"tenantId")!=scope.TenantId
            || scope.OriginalSuffixes.Count is 0 or >32 || scope.OriginalSuffixes.Distinct(StringComparer.Ordinal).Count()!=scope.OriginalSuffixes.Count
            || scope.WorkloadMessageIds.Count>32 || scope.WorkloadMessageIds.Distinct(StringComparer.Ordinal).Count()!=scope.WorkloadMessageIds.Count
            || scope.NativeStreams.Count>32 || scope.NativeStreams.Any(value=>value.TenantId!=scope.TenantId) || scope.ProjectionQueries.Count>32
            || scope.ProjectionQueries.Any(value=>value.Tenant!=scope.TenantId || value.AdditionalProperties is { Count:>0 })) return null;
        using var budget=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);budget.CancelAfter(TimeSpan.FromSeconds(30));
        var originals=new Dictionary<string,IndependentOriginal>(StringComparer.Ordinal);
        foreach(string suffix in scope.OriginalSuffixes)
        {
            if(suffix is not (":case-result" or ":missing-config" or ":observations")) return null;
            var original=await originalAuthority.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="executor",requestId=requestId+suffix },budget.Token).ConfigureAwait(false);
            if(original is null || original.RetainedRevision<1 || original.ResultRetainedRevision<1 || original.Result is null
                || !JsonNode.DeepEquals(JsonNode.Parse(original.Request.GetRawText()),request)) return null;
            originals.Add(suffix,original);
        }
        var statuses=new List<CommandStatusQueryResponse>();
        foreach(string messageId in scope.WorkloadMessageIds)
        {
            var status=await privateGateway.GetWorkloadCommandStatusAsync(scope.TenantId,messageId,budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            if(status is null || status.MessageId!=messageId || status.TenantId!=scope.TenantId || string.IsNullOrWhiteSpace(status.Domain) || string.IsNullOrWhiteSpace(status.AggregateId)) return null;
            statuses.Add(status);
        }
        var nativeReader=new QualificationNativeSourceReader(new QualificationWorkloadReadGateway(privateGateway),clock);
        IReadOnlyList<QualificationNativeStream>? streams=scope.NativeStreams.Count==0?[]:await nativeReader.ReadAsync(scope.NativeStreams,budget.Token).ConfigureAwait(false);if(streams is null) return null;
        var projections=new List<QualificationProjectionAudit>();
        foreach(var query in scope.ProjectionQueries)
        {
            if(projectionQueries is null) return null;
            var result=await projectionQueries.ReadAsync(query,budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            if(result is null || result.IsNotModified || result.Payload is not { } payload || payload.GetRawText().Length>1024*1024 || result.Metadata is not { Provenance:QueryResponseProvenance.ProjectionBacked,Lifecycle:ProjectionLifecycleState.Current,IsStale:false,IsDegraded:false } metadata
                || metadata.ServedAt is not { } served || served>clock.GetUtcNow() || served<clock.GetUtcNow().AddSeconds(-30) || string.IsNullOrWhiteSpace(metadata.ProjectionVersion)) return null;
            projections.Add(new(query.Tenant,query.Domain,query.AggregateId,query.QueryType,CustodyBindingContract.Hash(System.Text.Encoding.UTF8.GetBytes(payload.GetRawText())),metadata));
        }
        // Current native source must survive the other reads unchanged, rather than accepting a mixed head/restore incarnation.
        if(scope.NativeStreams.Count>0)
        {
            var after=await nativeReader.ReadAsync(scope.NativeStreams,budget.Token).ConfigureAwait(false);
            if(after is null || JsonSerializer.Serialize(streams,CustodyBindingContract.Json)!=JsonSerializer.Serialize(after,CustodyBindingContract.Json)) return null;
        }
        budget.Token.ThrowIfCancellationRequested();
        return new(requestId,originals,statuses,streams,projections,["SelectedNativeRecoveryControlReceiptHistory","CompleteInstalledLogAndTraceAccessAndCensus","NativeOutboxSnapshotAndAllHolderPhysicalCopyReadProtocols"]);
    }
}
