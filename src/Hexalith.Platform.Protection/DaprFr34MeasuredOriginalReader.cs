using System.Text.Json;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Independent authenticated read of the actual original canary comparison. This source cannot invoke an engine, renew a key use, or create a missing comparison.</summary>
public sealed class DaprFr34MeasuredOriginalReader(IndependentDaprAuthority originals)
{
    /// <summary>Reads the immutable engine/native comparison and checks its complete original reference.</summary>
    public async Task<Fr34MeasuredCanaryOriginal?> ReadAsync(Fr34CanaryReference reference,CancellationToken token)
    {
        string id=EventStorePrivateRecordingPort.StableUlid("fr34",reference.Target.TenantId,reference.CanaryId)+":native-comparison";
        var original=await originals.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="fr34-observer",requestId=id },token).ConfigureAwait(false);
        if(original?.Result is not { } result || original.Request.GetProperty("reference").Deserialize<Fr34CanaryReference>(CustodyBindingContract.Json)!=reference) return null;
        var measured=result.Deserialize<Fr34MeasuredCanaryOriginal>(CustodyBindingContract.Json);
        if(measured is not null && measured.CommandOriginalProtocol!=Fr34CommandFingerprint.Protocol) throw new InvalidOperationException("External.LegacyFr34RetainedPlaintextOriginalCleanupUnproven");
        return measured?.Reference==reference && measured.EngineName==reference.Target.EngineId && measured.EngineRevision==reference.Target.EngineVersion
            && measured.SourceRevision==1 && measured.NativeEventDigest.Length==64?measured:null;
    }
}
