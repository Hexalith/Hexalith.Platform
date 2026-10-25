using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Shipped typed guard/protection/signing workflow and exact native owner reader. Independently enrolled case requests and recovery-controller protocols remain installation inputs.</summary>
public sealed class QualificationGuardCaseWorkflow(IGovernanceScopeGuard guard, IDeletionProtectionOwner protection,
    IDeletionConsumptionActor consumption, IDeletionCapabilitySigningActor signer, IQualificationOriginalStore originals,
    IReadOnlyDictionary<string,QualificationGuardCaseDefinition> definitions, DeletionCapabilityRevocationSubscriber compromiseSubscriber)
{
    /// <summary>Runs the exact enrolled operation recipe. An uncertain retained step uses only exact owner lookup after restart.</summary>
    public async Task<JsonObject?> ExecuteAsync(JsonObject original,CancellationToken token)
    {
        string? id=original["caseId"]?.GetValue<string>();
        if (id is null || !id.StartsWith("S4.",StringComparison.Ordinal) && !id.StartsWith("v23.",StringComparison.Ordinal)
            || !definitions.TryGetValue(id,out var definition) || definition.CaseId!=id || original["enrolledScope"] is not JsonObject scope
            || QualificationProtocolDigest(scope)!=definition.EnrolledScopeSha256 || scope["tenantId"]?.GetValue<string>()!=definition.TenantId
            || scope["batchId"]?.GetValue<string>()!=definition.BatchId || definition.Boundary is not ("before" or "at" or "after" or "none")) return null;
        string requestId=original["requestId"]!.GetValue<string>();
        await originals.BeginAsync("executor",requestId+":case-result",original,token).ConfigureAwait(false);
        if(await originals.LookupResultAsync("executor",requestId+":case-result",token).ConfigureAwait(false) is { } completed) return completed;
        var observations=new JsonArray();
        int ordinal=0;
        async Task<JsonNode?> Step(QualificationGuardStep step)
        {
            int index=Interlocked.Increment(ref ordinal);
            if (!Scoped(step,definition)) throw new InvalidOperationException("Exact enrolled owner target required.");
            string stepId=requestId+":"+index;
            var input=(JsonObject)JsonSerializer.SerializeToNode(new { requestId,stepId,step=step with { SignedRevocationEvidence=null } },CustodyBindingContract.Json)!;
            bool first=await originals.BeginAsync("executor",stepId,input,token).ConfigureAwait(false);
            if(!first && await originals.LookupResultAsync("executor",stepId,token).ConfigureAwait(false) is { } retained) return retained;
            object? result=step.Kind switch {
                QualificationGuardStepKind.Guard => first ? await guard.ExecuteAsync(step.Transition!,step.Targets??[],token).ConfigureAwait(false)
                    : (await guard.ReadAsync(definition.TenantId,token).ConfigureAwait(false))?.Receipts.SingleOrDefault(r=>r.OperationId==step.Transition!.OperationId),
                QualificationGuardStepKind.Register => first ? await protection.RegisterAsync(step.Batch!,token).ConfigureAwait(false) : await protection.LookupAsync(definition.TenantId,definition.BatchId,token).ConfigureAwait(false),
                QualificationGuardStepKind.Reserve => first ? await protection.ReserveAndConsumeAsync(step.Batch!,token).ConfigureAwait(false) : await protection.LookupAsync(definition.TenantId,definition.BatchId,token).ConfigureAwait(false),
                QualificationGuardStepKind.Block => first ? await consumption.BlockAsync(step.Block!).WaitAsync(token).ConfigureAwait(false) : await consumption.LookupAsync(definition.TenantId,definition.BatchId).WaitAsync(token).ConfigureAwait(false),
                QualificationGuardStepKind.Compromise => first ? await compromiseSubscriber.ReceiveAsync(step.Compromise!,step.SignedRevocationEvidence!,token).ConfigureAwait(false)
                    : await consumption.LookupRevocationAsync(step.Compromise!).WaitAsync(token).ConfigureAwait(false) is not null,
                QualificationGuardStepKind.Activate => first ? await protection.ActivateAsync(step.Activation!,token).ConfigureAwait(false) : await protection.LookupAsync(definition.TenantId,definition.BatchId,token).ConfigureAwait(false),
                QualificationGuardStepKind.Sign => first ? await signer.SignAsync(step.Signing!).WaitAsync(token).ConfigureAwait(false) : await signer.LookupAsync(step.Signing!).WaitAsync(token).ConfigureAwait(false),
                QualificationGuardStepKind.ObsoleteUnissued => first ? await signer.ObsoleteUnissuedAsync(step.Signing!).WaitAsync(token).ConfigureAwait(false) : await signer.LookupAsync(step.Signing!).WaitAsync(token).ConfigureAwait(false),
                _ => null };
            if(result is null) throw new InvalidOperationException("Exact original owner result unknown.");
            if(result is DeletionCapabilitySigningOutcome { State:DeletionCapabilitySigningState.Unknown or DeletionCapabilitySigningState.Unavailable }
                || result is DeletionConsumptionOutcome { Status:DeletionConsumptionStatus.Unavailable or DeletionConsumptionStatus.ConsumptionReserved }
                || step.Kind==QualificationGuardStepKind.Compromise && result is false
                || result is GovernanceProtocolReceipt { Status:"Unknown" or "Unavailable" })
                throw new InvalidOperationException("Exact original remains pending; later steps cannot dispatch.");
            var encoded=(JsonObject)JsonSerializer.SerializeToNode(new { stepId,step.Kind,result },CustodyBindingContract.Json)!;
            await originals.RetainAsync("executor",stepId,encoded,token).ConfigureAwait(false);
            return encoded;
        }
        foreach(var step in definition.Before) observations.Add(await Step(step).ConfigureAwait(false));
        if(definition.Trigger is not null && definition.Boundary=="before") observations.Add(await Step(definition.Trigger).ConfigureAwait(false));
        if(definition.Trigger is not null && definition.Boundary=="at")
        {
            var effect=Step(definition.Effect); var trigger=Step(definition.Trigger);
            observations.Add(await effect.ConfigureAwait(false)); observations.Add(await trigger.ConfigureAwait(false));
        }
        else observations.Add(await Step(definition.Effect).ConfigureAwait(false));
        if(definition.Trigger is not null && definition.Boundary=="after") observations.Add(await Step(definition.Trigger).ConfigureAwait(false));
        foreach(var step in definition.After) observations.Add(await Step(step).ConfigureAwait(false));
        // These are typed current native facts. They are not four-stage recovery evidence or a qualification pass.
        var output=new JsonObject { ["caseId"]=id,["requestId"]=requestId,["operations"]=observations,
            ["guard"]=JsonSerializer.SerializeToNode(await guard.ReadAsync(definition.TenantId,token).ConfigureAwait(false),CustodyBindingContract.Json),
            ["protection"]=JsonSerializer.SerializeToNode(await protection.LookupAsync(definition.TenantId,definition.BatchId,token).ConfigureAwait(false),CustodyBindingContract.Json) };
        await originals.RetainAsync("executor",requestId+":case-result",output,token).ConfigureAwait(false);
        return output;
    }
    private static string QualificationProtocolDigest(JsonNode value)=>Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(QualificationProofVerifier.CanonicalBytes(value)));
    private static bool Scoped(QualificationGuardStep step,QualificationGuardCaseDefinition definition)
    {
        int branches=new object?[]{step.Transition,step.Batch,step.Block,step.Compromise,step.Activation,step.Signing}.Count(value=>value is not null);
        if(branches!=1 || step.Kind!=QualificationGuardStepKind.Guard && step.Targets is not null
            || step.Kind==QualificationGuardStepKind.Compromise && string.IsNullOrWhiteSpace(step.SignedRevocationEvidence)
            || step.Kind!=QualificationGuardStepKind.Compromise && step.SignedRevocationEvidence is not null) return false;
        return step.Kind switch {
            QualificationGuardStepKind.Guard => step.Transition?.TenantId==definition.TenantId && (step.Targets??[]).Count<=32,
            QualificationGuardStepKind.Register or QualificationGuardStepKind.Reserve => step.Batch?.Capability.TenantId==definition.TenantId && step.Batch.Capability.BatchId==definition.BatchId,
            QualificationGuardStepKind.Block => step.Block?.TenantId==definition.TenantId && step.Block.BatchId==definition.BatchId,
            QualificationGuardStepKind.Compromise => step.Compromise?.TenantId==definition.TenantId,
            QualificationGuardStepKind.Activate => step.Activation?.Replacement.Capability.TenantId==definition.TenantId && step.Activation.Replacement.Capability.BatchId==definition.BatchId,
            QualificationGuardStepKind.Sign or QualificationGuardStepKind.ObsoleteUnissued => step.Signing?.TenantId==definition.TenantId && step.Signing.BatchId==definition.BatchId,
            _=>false };
    }
}
