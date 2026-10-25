using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Concrete native host/protection source reader and publisher for PersistedPhaseObserver's signed-file protocol. It publishes actual typed owner/native facts, never expected pass flags.</summary>
public sealed class JointPhaseNativeSourcePublisher(QualificationNativeSourceReader native,IGovernanceScopeGuard guard,IDeletionProtectionOwner protection,
    ICustodyOwnerStateAuthority currentAuthority,IReadOnlyDictionary<string,JointPhaseNativeSourceScope> enrolledScopes,string archiveDirectory,ECDsa independentlyEnrolledSigner)
{
    /// <summary>Reads all current native source facts twice, checks exact current read scope before/after, and publishes immutable signed bytes. Missing facts stay missing; this method does not certify all H1-H4/G1-G7 predicates.</summary>
    public async Task<JsonObject?> PublishAsync(string caseId,JsonObject phaseRequest,CancellationToken token)
    {
        if(!enrolledScopes.TryGetValue(caseId,out var scope) || phaseRequest["cases"] is not JsonArray cases || !cases.Any(value=>value?.GetValue<string>()==caseId)
            || phaseRequest["bindingDigest"]?.GetValue<string>()!=scope.BindingDigest || phaseRequest["phase"]?.GetValue<int>()!=scope.Phase
            || phaseRequest["requestId"]?.GetValue<string>() is not { Length:64 } requestId || !requestId.All(char.IsAsciiHexDigit)
            || !Path.IsPathFullyQualified(archiveDirectory)) return null;
        string authorization=QualificationProtocolSchema.Digest(new JsonObject { ["request"]=phaseRequest.DeepClone(),["caseId"]=caseId });
        if(!await currentAuthority.AuthorizeOperationAsync(authorization,"PublishIndependentJointPhaseNativeSource",token).ConfigureAwait(false)) return null;
        string directory=Path.Combine(archiveDirectory,requestId);
        string path=Path.Combine(directory,Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(caseId)))+".json");
        if(File.Exists(path))
        {
            var info=new FileInfo(path);if(info.LinkTarget is not null || info.Length is <=0 or >1024*1024) return null;
            var existing=JsonNode.Parse(await File.ReadAllBytesAsync(path,token).ConfigureAwait(false))!.AsObject();var retained=existing["payload"]!.AsObject();
            if(retained["requestSha256"]?.GetValue<string>()!=QualificationProtocolSchema.Digest(phaseRequest) || retained["bindingDigest"]?.GetValue<string>()!=scope.BindingDigest || retained["caseId"]?.GetValue<string>()!=caseId
                || !independentlyEnrolledSigner.VerifyData(QualificationProofVerifier.CanonicalBytes(retained),Convert.FromBase64String(existing["signature"]!.GetValue<string>()),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence)) return null;
            return new JsonObject { ["requestSha256"]=retained["requestSha256"]!.DeepClone(),["caseId"]=caseId,["sourceRecordsSha256"]=QualificationProtocolSchema.Digest(retained["records"]!) };
        }
        var state=await guard.ReadAsync(scope.TenantId,token).ConfigureAwait(false);var batch=await protection.LookupAsync(scope.TenantId,scope.BatchId,token).ConfigureAwait(false);
        var streams=await native.ReadAsync(scope.NativeStreams,token).ConfigureAwait(false);
        if(state is null || state.TenantId!=scope.TenantId || state.Revision<1 || batch.TenantId!=scope.TenantId || batch.BatchId!=scope.BatchId
            || batch.Status==DeletionConsumptionStatus.Unavailable || streams is null || streams.Count==0) return null;
        var after=await native.ReadAsync(scope.NativeStreams,token).ConfigureAwait(false);var final=await guard.ReadAsync(scope.TenantId,token).ConfigureAwait(false);
        if(JsonSerializer.Serialize(streams,CustodyBindingContract.Json)!=JsonSerializer.Serialize(after,CustodyBindingContract.Json)
            || JsonSerializer.Serialize(state)!=JsonSerializer.Serialize(final) || !await currentAuthority.AuthorizeOperationAsync(authorization,"PublishIndependentJointPhaseNativeSource",token).ConfigureAwait(false)) return null;
        var records=new JsonArray();
        // The actual independently authenticated guard revision/receipt digest identifies this typed source record; it is not an invented event stream position.
        var ownerPayload=JsonSerializer.SerializeToNode(new { guard=state,protection=batch },CustodyBindingContract.Json)!;
        records.Add(new JsonObject { ["sourceRevision"]=state.Revision,["eventId"]=QualificationProtocolSchema.Digest(ownerPayload),["payload"]=ownerPayload });
        foreach(var stream in streams)
            foreach(var item in stream.Events)
                records.Add(new JsonObject { ["sourceRevision"]=item.SourceRevision,["eventId"]=item.EventId,["payload"]=JsonSerializer.SerializeToNode(new { stream.Identity,stream.Proof,eventRecord=item },CustodyBindingContract.Json) });
        var source=new JsonObject { ["requestSha256"]=QualificationProtocolSchema.Digest(phaseRequest),["bindingDigest"]=scope.BindingDigest,["caseId"]=caseId,["records"]=records };
        byte[] bytes=QualificationProofVerifier.CanonicalBytes(source);byte[] signature;
        lock(independentlyEnrolledSigner) signature=independentlyEnrolledSigner.SignData(bytes,HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence);
        var envelope=new JsonObject { ["payload"]=source,["signature"]=Convert.ToBase64String(signature) };
        await ImmutableFileArchive.PublishAsync(path,QualificationProofVerifier.CanonicalBytes(envelope),token).ConfigureAwait(false);
        return new JsonObject { ["requestSha256"]=QualificationProtocolSchema.Digest(phaseRequest),["caseId"]=caseId,["sourceRecordsSha256"]=QualificationProtocolSchema.Digest(records) };
    }
}
