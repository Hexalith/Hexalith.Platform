using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Independent original canary collector over the actual production engine, native persisted reader and exact original command. Lost measurements are lookup-only; plaintext is never retained in its measurement.</summary>
public sealed class Fr34IndependentOriginalCollector(EventStoreFr34CanaryEngine actualEngine,Fr34EventStoreCanaryReader nativeReader,IndependentDaprAuthority originals):IFr34UnsealMeasurement
{
    /// <summary>Records actual original engine identity, native bytes/plaintext absence and the concrete handler's unseal result before erasure. This method issues no second unseal or key delivery.</summary>
    public async Task<Fr34MeasuredCanaryOriginal?> RecordUnsealAsync(Fr34CanaryReference reference,ReadOnlyMemory<byte> actualUnsealedBytes,CancellationToken token)
    {
        if(!actualEngine.IsActualTarget(reference.Target)) return null;
        string id=EventStorePrivateRecordingPort.StableUlid("fr34",reference.Target.TenantId,reference.CanaryId);
        var original=await originals.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="fr34-engine",requestId=id },token).ConfigureAwait(false);
        var persisted=await nativeReader.ReadOriginalAsync(reference,token).ConfigureAwait(false);
        if(original is null || persisted?.NativeProof is null || persisted.NativeEventDigest is null || original.SubmissionState!="Dispatched") return null;
        string measurementId=id+":native-comparison";
        if(await LookupAsync(reference,token).ConfigureAwait(false) is { } retained) return retained;
        var input=JsonSerializer.SerializeToElement(new { reference },CustodyBindingContract.Json);
        var admission=await originals.InvokeAsync<Admission>("qualification-begin-original",new { role="fr34-observer",requestId=measurementId,original=input },token).ConfigureAwait(false);
        if(admission is not { Exact:true,First:true }) return null;
        try
        {
            if(!Fr34CommandFingerprint.IsDescriptor(original.Request)) throw new InvalidOperationException("External.LegacyFr34RetainedPlaintextOriginalCleanupUnproven");
            if(actualUnsealedBytes.Length is 0 or >8192) return null;
            var inputPayload=new PersistFr34Canary(new(Convert.ToBase64String(actualUnsealedBytes.Span),persisted.ProtectionTarget));
            var command=new Hexalith.EventStore.Contracts.Commands.SubmitCommandRequest(id,reference.Target.TenantId,"fr34-canary",reference.CanaryId,
                typeof(PersistFr34Canary).FullName!,JsonSerializer.SerializeToElement(inputPayload,new JsonSerializerOptions(JsonSerializerDefaults.Web)),IdempotencyKey:id);
            bool equal=Fr34CommandFingerprint.MatchesCommand(original.Request,command);
            byte[] encoded=System.Text.Encoding.ASCII.GetBytes(inputPayload.ProtectedContent.Value);
            byte[] encodedJson=JsonSerializer.SerializeToUtf8Bytes(inputPayload.ProtectedContent.Value,new JsonSerializerOptions(JsonSerializerDefaults.Web));
            bool absent;
            try { absent=persisted.Protected.PayloadBytes.AsSpan().IndexOf(actualUnsealedBytes.Span)<0 && persisted.Protected.PayloadBytes.AsSpan().IndexOf(encoded)<0 && persisted.Protected.PayloadBytes.AsSpan().IndexOf(encodedJson)<0; }
            finally { CryptographicOperations.ZeroMemory(encoded);CryptographicOperations.ZeroMemory(encodedJson); }
            var identity=actualEngine.ReadEngineIdentity();
            var result=new Fr34MeasuredCanaryOriginal(reference,identity.Name,identity.SourceRevision,persisted.NativeProof,1,persisted.NativeEventDigest,
                persisted.Protected.PayloadBytes.Length,actualUnsealedBytes.Length,equal && absent,equal,Fr34CommandFingerprint.Protocol);
            return await originals.InvokeAsync<Admission>("qualification-retain-result",new { role="fr34-observer",requestId=measurementId,result },token).ConfigureAwait(false) is { Exact:true }?result:null;
        }
        finally { CryptographicOperations.ZeroMemory(persisted.Protected.PayloadBytes); }
    }

    /// <summary>Reads the exact retained measured comparison only; it cannot unseal or reproduce a missing original.</summary>
    public async Task<Fr34MeasuredCanaryOriginal?> LookupAsync(Fr34CanaryReference reference,CancellationToken token)
    {
        string id=EventStorePrivateRecordingPort.StableUlid("fr34",reference.Target.TenantId,reference.CanaryId)+":native-comparison";
        var original=await originals.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="fr34-observer",requestId=id },token).ConfigureAwait(false);
        var result=original?.Result?.Deserialize<Fr34MeasuredCanaryOriginal>(CustodyBindingContract.Json);
        if(result is not null && result.CommandOriginalProtocol!=Fr34CommandFingerprint.Protocol) throw new InvalidOperationException("External.LegacyFr34RetainedPlaintextOriginalCleanupUnproven");
        return result?.Reference==reference && result.EngineName==reference.Target.EngineId && result.EngineRevision==reference.Target.EngineVersion && result.NativeProof is not null?result:null;
    }
    private sealed record Admission(bool Exact,bool First);
}
