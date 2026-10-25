using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Concrete exact original reader over the published real guarded transaction lookup. It reports selected originals only, never claims that an unenumerated native history is complete.</summary>
public sealed class QualificationGuardedOriginalReader(DaprGuardedStateTransaction transaction)
{
    /// <summary>Reads immutable exact physical commit positions, including stale/no-effect commits, and authenticates decoded logical receipt correlation.</summary>
    public async Task<IReadOnlyList<QualificationGuardedOriginal>?> ReadAsync(string tenant,IReadOnlyList<QualificationGuardStep> enrolledOperations,CancellationToken token)
    {
        if(enrolledOperations.Count>1000 || enrolledOperations.Any(value=>value.Kind!=QualificationGuardStepKind.Guard || value.Transition?.TenantId!=tenant)
            || enrolledOperations.Select(value=>value.Transition!.OperationId).Distinct(StringComparer.Ordinal).Count()!=enrolledOperations.Count) return null;
        var result=new List<QualificationGuardedOriginal>();
        foreach(var step in enrolledOperations)
        {
            string target=Hash(step.Targets??[]);string intent=Hash(new { Transition=step.Transition,TargetMutationDigest=target });
            var native=await transaction.LookupByIntentAsync(tenant,step.Transition!.OperationId,intent,token).ConfigureAwait(false);
            if(native.Status!=GuardedStateCommitStatus.Committed || native.Receipt is not { } receipt || receipt.TenantId!=tenant || receipt.OperationId!=step.Transition.OperationId
                || receipt.LogicalIntentDigest!=intent || receipt.GuardRevision<=0) return null;
            var logical=JsonSerializer.Deserialize<GovernanceProtocolReceipt>(receipt.Outcome);
            if(logical is null || logical.OperationId!=receipt.OperationId || logical.IntentDigest!=intent || logical.GuardHighWater!=receipt.LogicalGuardHighWater) return null;
            result.Add(new(step.Transition,target,receipt,logical));
        }
        return result;
    }
    private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
