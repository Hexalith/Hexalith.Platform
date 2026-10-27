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
    /// <summary>Checks the selected exact native read schema and scope before its future case original exists. It dispatches no case or lookup effect.</summary>
    public IReadOnlyList<string> PreflightScope(JsonObject request)
    {
        if(SecretsProof.Text(request,"caseId") is not { } id || !enrolledScopes.TryGetValue(id,out var scope) || request["enrolledScope"] is not JsonObject enrolled
            || QualificationProtocolSchema.Digest(enrolled)!=scope.EnrolledScopeSha256 || SecretsProof.Text(enrolled,"tenantId")!=scope.TenantId
            || scope.OriginalSuffixes.Count is 0 or >32 || scope.OriginalSuffixes.Distinct(StringComparer.Ordinal).Count()!=scope.OriginalSuffixes.Count
            || scope.OriginalSuffixes.Any(suffix=>suffix is not (":case-result" or ":case-terminal-v2" or ":missing-config" or ":observations" or ":observations-v2" or ":case-delivery-denial-v3" or ":case-delivery-rejection-v4"))
            || scope.WorkloadMessageIds.Count>32 || scope.WorkloadMessageIds.Distinct(StringComparer.Ordinal).Count()!=scope.WorkloadMessageIds.Count
            || scope.NativeStreams.Count>32 || scope.NativeStreams.Any(value=>value.TenantId!=scope.TenantId) || scope.ProjectionQueries.Count>32
            || scope.ProjectionQueries.Any(value=>value.Tenant!=scope.TenantId || value.AdditionalProperties is { Count:>0 })) return ["InstalledExactNativeAuditOriginalStreamAndProjectionReadScope"];
        return scope.ProjectionQueries.Count>0 && projectionQueries is null?["InstalledRestrictedProjectionQueryTransportAndCurrentReadAuthority"]:[];
    }
    /// <summary>Checks the additional exact authorization protocol required for a complete mutation census before a case effect.</summary>
    public IReadOnlyList<string> PreflightMutationScope(JsonObject request)
    {
        var basic=PreflightScope(request);if(basic.Count!=0) return basic;
        var scope=enrolledScopes[SecretsProof.Text(request,"caseId")!];
        if((scope.WorkloadMessageIds.Count>0 || scope.NativeStreams.Count>0) && (scope.DispatchOriginals is null || scope.DispatchOriginals.Count!=scope.WorkloadMessageIds.Count
            || !scope.DispatchOriginals.Select(value=>value.MessageId).ToHashSet(StringComparer.Ordinal).SetEquals(scope.WorkloadMessageIds)
            || scope.DispatchOriginals.Any(value=>value.Role is not ("private-replay" or "private-security-observation" or "fr34-engine") || string.IsNullOrWhiteSpace(value.TargetId)
                || !scope.NativeStreams.Any(stream=>stream.Domain==value.Domain && stream.AggregateId==value.AggregateId))))
            return ["InstalledExactImmutableDispatchOriginalLocatorsAndNativeTargetMappingsV2"];
        return [];
    }
    /// <summary>Reads actual bytes/state only. It never turns a source buffer or missing log/trace/restore protocol into persistence, receipt positions, or a zero-effects assertion.</summary>
    public async Task<QualificationEventStoreAuditSnapshot?> ReadAsync(JsonObject request,CancellationToken cancellationToken)
    {
        if(PreflightScope(request).Count!=0) return null;
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
            if(suffix is not (":case-result" or ":case-terminal-v2" or ":missing-config" or ":observations" or ":observations-v2" or ":case-delivery-denial-v3" or ":case-delivery-rejection-v4")) return null;
            var original=await originalAuthority.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="executor",requestId=requestId+suffix },budget.Token).ConfigureAwait(false);
            bool lifecycle=QualificationLifecycleCaseWorkflow.Supported(id),delivery=QualificationExportDeliveryWorkflow.Supported(id);
            if(suffix==":case-result" && (lifecycle || delivery))
            {
                original=await originalAuthority.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="executor",requestId=requestId+":case-terminal-v2" },budget.Token).ConfigureAwait(false)??original;
                bool denial=delivery && id.StartsWith("S3.delivery-substitution.",StringComparison.Ordinal);
                if(denial) original=await originalAuthority.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="executor",requestId=requestId+":case-delivery-rejection-v4" },budget.Token).ConfigureAwait(false)
                    ??await originalAuthority.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="executor",requestId=requestId+":case-delivery-denial-v3" },budget.Token).ConfigureAwait(false)??original;
                if(original?.Result is not { } body || JsonNode.Parse(body.GetRawText()) is not JsonObject value
                    || !(lifecycle?QualificationLifecycleCaseWorkflow.TerminalBody(value):denial?QualificationExportDeliveryWorkflow.TerminalDenialBody(value):QualificationExportDeliveryWorkflow.TerminalBody(value))) return null;
            }
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
        List<QualificationNativeMutationEvidence>? mutations=null;
        if(scope.DispatchOriginals is { } dispatchReferences)
        {
            var authorizations=new Dictionary<string,IndependentAuthorityReadEvidence>(StringComparer.Ordinal);
            foreach(var reference in dispatchReferences)
            {
                var evidence=await originalAuthority.ReadOriginalEvidenceAsync(reference.Role,reference.MessageId,budget.Token).ConfigureAwait(false);
                if(evidence is null || !authorizations.TryAdd(reference.MessageId,evidence)) return null;
            }
            mutations=[];
            foreach(var stream in streams)
                foreach(var item in stream.Events)
                {
                    var matches=statuses.Where(status=>status.TenantId==stream.Identity.TenantId && status.Domain==stream.Identity.Domain && status.AggregateId==stream.Identity.AggregateId
                        && status.EventCount is >0 && status.CommittedEventSequence is { } end && item.SourceRevision<=end && item.SourceRevision>end-status.EventCount).ToArray();
                    if(matches.Length!=1 || matches[0].MessageId is not { } message || !authorizations.TryGetValue(message,out var authorization)) return null;
                    var reference=dispatchReferences.Single(value=>value.MessageId==message);
                    if(reference.Domain!=stream.Identity.Domain || reference.AggregateId!=stream.Identity.AggregateId) return null;
                    if(string.IsNullOrWhiteSpace(item.PrincipalId) || item.OccurredAt is not { } occurredAt) return null;
                    var mutation=new QualificationNativeMutationEvidence("EXT-SECRETS-1.native-mutation.v2",QualificationProtocolSchema.Digest(request),reference.TargetId,stream.Proof,item.SourceRevision,item.EventId,item.NativeEventDigest,matches[0],authorization,item.PrincipalId,occurredAt.ToUnixTimeMilliseconds());
                    if(!mutation.Verify(request,originalAuthority.PublicBinding,clock.GetUtcNow().ToUnixTimeSeconds())) return null;
                    mutations.Add(mutation);
                }
        }
        budget.Token.ThrowIfCancellationRequested();
        return new(requestId,originals,statuses,streams,projections,["SelectedNativeRecoveryControlReceiptHistory","CompleteInstalledLogAndTraceAccessAndCensus","NativeOutboxSnapshotAndAllHolderPhysicalCopyReadProtocols"],mutations);
    }
}
