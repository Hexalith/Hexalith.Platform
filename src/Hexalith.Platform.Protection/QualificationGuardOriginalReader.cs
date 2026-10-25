using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Concrete independent projection from current typed owners, exact original signer lookups and actual native audit history. No public expected metric input is accepted.</summary>
public sealed class QualificationGuardOriginalReader(IGovernanceScopeGuard guard,IDeletionProtectionOwner protection,IDeletionCapabilitySigningActor signer,
    IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit,IReadOnlyDictionary<string,QualificationGuardCaseDefinition> definitions):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject request,CancellationToken token)
    {
        string? id=SecretsProof.Text(request,"caseId"),requestId=SecretsProof.Text(request,"requestId");
        if(id is null || requestId is null || !definitions.TryGetValue(id,out var definition) || request["enrolledScope"] is not JsonObject scope
            || QualificationProtocolSchema.Digest(scope)!=definition.EnrolledScopeSha256) return null;
        var retained=await originals.LookupResultAsync("executor",requestId+":case-result",token).ConfigureAwait(false);
        var state=await guard.ReadAsync(definition.TenantId,token).ConfigureAwait(false);var batch=await protection.LookupAsync(definition.TenantId,definition.BatchId,token).ConfigureAwait(false);
        var native=await audit.ReadAsync(request,token).ConfigureAwait(false);
        if(retained is null || state is null || state.TenantId!=definition.TenantId || batch.TenantId!=definition.TenantId || batch.BatchId!=definition.BatchId
            || batch.Status is DeletionConsumptionStatus.Unavailable or DeletionConsumptionStatus.ConsumptionReserved || native is null || native.OriginalRevision<1) return null;
        var signing=new List<DeletionCapabilitySigningOutcome>();
        var steps=definition.Before.Concat([definition.Effect]).Concat(definition.Trigger is { } trigger?[trigger]:[]).Concat(definition.After).ToArray();
        foreach(var payload in steps.Where(value=>value.Signing is not null).Select(value=>value.Signing!).Distinct())
        {
            var actual=await signer.LookupAsync(payload).WaitAsync(token).ConfigureAwait(false);
            if(actual.Payload!=payload || actual.State is DeletionCapabilitySigningState.Unknown or DeletionCapabilitySigningState.Unavailable) return null;
            signing.Add(actual);
        }
        string? deletionId=steps.Select(value=>value.Transition?.DeletionRequestId??value.Signing?.DeletionRequestId??value.Batch?.Capability.DeletionRequestId).FirstOrDefault(value=>!string.IsNullOrWhiteSpace(value));
        var deletion=state.Deletions.SingleOrDefault(value=>value.RequestId==deletionId || value.Batches.Any(item=>item.BatchId==definition.BatchId));
        var issued=deletion?.Batches.SingleOrDefault(value=>value.BatchId==definition.BatchId);if(deletion is null || issued is null) return null;
        var projected=QualificationGuardInvariantProjection.Project(definition.TenantId,deletion.RequestId,definition.BatchId,state,batch,signing,QualificationProtocolSchema.MetricNames(id));
        string receipt=QualificationProtocolSchema.Digest(retained);var publicState=QualificationDeniedCaseState.Create(request,native.OriginalRevision,receipt);
        string outcome=batch.Status switch { DeletionConsumptionStatus.Consumed=>"Consumed",DeletionConsumptionStatus.AlreadyDestroyedByBatch=>"AlreadyDestroyed",DeletionConsumptionStatus.Unconsumed=>"Unconsumed",DeletionConsumptionStatus.ConsumptionBlocked=>"Blocked",_=>batch.Status.ToString() };
        if(retained["operations"] is JsonArray operations && operations.LastOrDefault()?["result"]?["Status"] is JsonValue status && status.TryGetValue<string>(out string? actualStatus)) outcome=actualStatus;
        publicState["outcome"]=outcome;publicState["status"]=outcome;publicState["sealId"]=deletion.SealId;publicState["batchId"]=issued.BatchId;
        publicState["manifestDigest"]=issued.ManifestDigest;publicState["keyVersion"]=issued.CapabilityKeyVersion;publicState["actualIssuedGuardRevision"]=issued.IssuedGuardRevision;
        publicState["attestationOrdinal"]=issued.Capability?.AttestationOrdinal;publicState["attempts"]=JsonSerializer.SerializeToNode(signing,CustodyBindingContract.Json);
        // The audit supplement must supply independently authenticated exact target-ID mappings; input labels cannot classify physical destruction.
        if(native.Facts?["destroyedTargetIds"] is JsonArray destroyed) publicState["destroyedTargetIds"]=destroyed.DeepClone();
        else if(batch.TargetReceipts.Count!=0) return null;
        if(native.Facts?["activeCredentialIds"] is JsonArray active) publicState["activeCredentialIds"]=active.DeepClone();else return null;
        return new(requestId,receipt,state.InstallationId+":"+state.Revision,new JsonObject { ["state"]=publicState,["receipts"]=native.Receipts.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=native.Facts?["linearization"]?.DeepClone()??new JsonObject(),
            ["independentOwnerInvariants"]=JsonSerializer.SerializeToNode(projected,CustodyBindingContract.Json),["guardState"]=JsonSerializer.SerializeToNode(state,CustodyBindingContract.Json),
            ["protectionState"]=JsonSerializer.SerializeToNode(batch,CustodyBindingContract.Json),["signingOriginals"]=JsonSerializer.SerializeToNode(signing,CustodyBindingContract.Json) });
    }
}
