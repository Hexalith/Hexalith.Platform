using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Concrete native host/protection source reader and publisher for PersistedPhaseObserver's signed-file protocol. It publishes actual typed owner/native facts, never expected pass flags.</summary>
public sealed class JointPhaseNativeSourcePublisher(QualificationNativeSourceReader native,IGovernanceScopeGuard guard,IDeletionProtectionOwner protection,
    ICustodyOwnerStateAuthority currentAuthority,IReadOnlyDictionary<string,JointPhaseNativeSourceScope> enrolledScopes,string archiveDirectory,ECDsa independentlyEnrolledSigner,DaprFr34MeasuredOriginalReader? canaryOriginals=null,Hexalith.Platform.Custody.IDeletionCapabilitySigningActor? signingOriginals=null,QualificationGuardedOriginalReader? guardedOriginals=null,QualificationDirectoryBoundaryOriginalReader? directoryOriginals=null):IJointPhaseNativeSourcePublisher
{
    /// <summary>One bound for new publication, legacy exact recovery and the persisted observer (including aggregate streams).</summary>
    public const int MaximumEnvelopeBytes=64*1024*1024;
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PreflightAsync(string caseId,JsonObject request,CancellationToken token)
    {
        var incomplete=JointPhaseSemanticReadiness.MissingInputs(caseId);if(incomplete.Count!=0) return incomplete;
        if(!enrolledScopes.TryGetValue(caseId,out var scope) || request["bindingDigest"]?.GetValue<string>()!=scope.BindingDigest || JointPhaseRequestRecipe.Phase(request)!=scope.Phase)
            return ["ExactIndependentNativeSourceCaseScope"];
        string authorization=QualificationProtocolSchema.Digest(new JsonObject { ["request"]=request.DeepClone(),["caseId"]=caseId });
        if(!await currentAuthority.AuthorizeOperationAsync(authorization,"PublishIndependentJointPhaseNativeSource",token).ConfigureAwait(false)) return ["CurrentIndependentNativeSourceReadGrant"];
        var state=await guard.ReadAsync(scope.TenantId,token).ConfigureAwait(false);var batch=await protection.LookupAsync(scope.TenantId,scope.BatchId,token).ConfigureAwait(false);
        var streams=await native.ReadAsync(scope.NativeStreams,token).ConfigureAwait(false);
        return state is null || state.TenantId!=scope.TenantId || batch.Status==DeletionConsumptionStatus.Unavailable || streams is null
            ?["External.CurrentNativeGuardProtectionAndStreamReadProtocols"]:[];
    }
    /// <summary>Reads all current native source facts twice, checks exact current read scope before/after, and publishes immutable signed bytes. Missing facts stay missing; this method does not certify all H1-H4/G1-G7 predicates.</summary>
    public Task<JsonObject?> PublishAsync(string caseId,JsonObject phaseRequest,CancellationToken token)=>PublishBoundedAsync(caseId,phaseRequest,MaximumEnvelopeBytes,token);
    internal async Task<JsonObject?> PublishBoundedAsync(string caseId,JsonObject phaseRequest,int maximumEnvelopeBytes,CancellationToken token)
    {
        if(maximumEnvelopeBytes is <1 or >MaximumEnvelopeBytes) throw new ArgumentOutOfRangeException(nameof(maximumEnvelopeBytes));
        if(!enrolledScopes.TryGetValue(caseId,out var scope) || phaseRequest["cases"] is not JsonArray cases || !cases.Any(value=>value?.GetValue<string>()==caseId)
            || phaseRequest["bindingDigest"]?.GetValue<string>()!=scope.BindingDigest || JointPhaseRequestRecipe.Phase(phaseRequest)!=scope.Phase
            || phaseRequest["requestId"]?.GetValue<string>() is not { Length:64 } requestId || !requestId.All(char.IsAsciiHexDigit)
            || !Path.IsPathFullyQualified(archiveDirectory)) return null;
        string authorization=QualificationProtocolSchema.Digest(new JsonObject { ["request"]=phaseRequest.DeepClone(),["caseId"]=caseId });
        if(!await currentAuthority.AuthorizeOperationAsync(authorization,"PublishIndependentJointPhaseNativeSource",token).ConfigureAwait(false)) return null;
        string directory=Path.Combine(archiveDirectory,requestId);
        string path=Path.Combine(directory,Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(caseId)))+".json");
        if(await ProtectedArchiveStore.ExistsAsync(path,token).ConfigureAwait(false) || await ProtectedArchiveStore.ExistsAsync(path+".measurement",token).ConfigureAwait(false)) return await LookupAsync(caseId,phaseRequest,token).ConfigureAwait(false);
        var state=await guard.ReadAsync(scope.TenantId,token).ConfigureAwait(false);var batch=await protection.LookupAsync(scope.TenantId,scope.BatchId,token).ConfigureAwait(false);
        var streams=await native.ReadAsync(scope.NativeStreams,token).ConfigureAwait(false);
        if(state is null || state.TenantId!=scope.TenantId || state.Revision<1 || batch.TenantId!=scope.TenantId || batch.BatchId!=scope.BatchId
            || batch.Status==DeletionConsumptionStatus.Unavailable || streams is null || streams.Count==0) return null;
        var measuredCanaries=new List<Fr34MeasuredCanaryOriginal>();
        foreach(var reference in scope.Fr34Canaries??[])
        {
            if(canaryOriginals is null) return null;var measured=await canaryOriginals.ReadAsync(reference,token).ConfigureAwait(false);if(measured is null) return null;
            var nativeCanary=streams.SingleOrDefault(value=>value.Identity.TenantId==reference.Target.TenantId && value.Identity.Domain=="fr34-canary" && value.Identity.AggregateId==reference.CanaryId);
            if(nativeCanary is null || nativeCanary.Events.Count!=1 || nativeCanary.Events[0].NativeEventDigest!=measured.NativeEventDigest
                || JsonSerializer.Serialize(nativeCanary.Proof)!=JsonSerializer.Serialize(measured.NativeProof)) return null;
            measuredCanaries.Add(measured);
        }
        var signatures=new List<Hexalith.Platform.Custody.DeletionCapabilitySigningOutcome>();
        foreach(var payload in scope.SigningPayloads??[])
        {
            if(signingOriginals is null) return null;var outcome=await signingOriginals.LookupAsync(payload).WaitAsync(token).ConfigureAwait(false);
            if(outcome.Payload!=payload) return null;signatures.Add(outcome);
        }
        IReadOnlyList<QualificationGuardedOriginal>? physicalOriginals=[];
        if(scope.GuardOperations is { Count:>0 }) { if(guardedOriginals is null) return null;physicalOriginals=await guardedOriginals.ReadAsync(scope.TenantId,scope.GuardOperations,token).ConfigureAwait(false);if(physicalOriginals is null) return null; }
        var boundaryHistory=directoryOriginals is null?null:await directoryOriginals.ReadAsync(scope.TenantId,token).ConfigureAwait(false);
        // Every optional exact-original read is inside the same stable current-source interval.
        var after=await native.ReadAsync(scope.NativeStreams,token).ConfigureAwait(false);var final=await guard.ReadAsync(scope.TenantId,token).ConfigureAwait(false);var finalBatch=await protection.LookupAsync(scope.TenantId,scope.BatchId,token).ConfigureAwait(false);
        if(JsonSerializer.Serialize(streams,CustodyBindingContract.Json)!=JsonSerializer.Serialize(after,CustodyBindingContract.Json)
            || JsonSerializer.Serialize(batch)!=JsonSerializer.Serialize(finalBatch) || JsonSerializer.Serialize(state)!=JsonSerializer.Serialize(final) || !await currentAuthority.AuthorizeOperationAsync(authorization,"PublishIndependentJointPhaseNativeSource",token).ConfigureAwait(false)) return null;
        var records=new JsonArray();
        // The actual independently authenticated guard revision/receipt digest identifies this typed source record; it is not an invented event stream position.
        var ownerPayload=JsonSerializer.SerializeToNode(new { guard=state,protection=batch,canaryOriginals=measuredCanaries,signingOriginals=signatures,guardedOriginals=physicalOriginals,directoryOriginals=boundaryHistory },CustodyBindingContract.Json)!;
        records.Add(new JsonObject { ["sourceRevision"]=state.Revision,["eventId"]=QualificationProtocolSchema.Digest(ownerPayload),["payload"]=ownerPayload });
        foreach(var stream in streams)
            foreach(var item in stream.Events)
                records.Add(new JsonObject { ["sourceRevision"]=item.SourceRevision,["eventId"]=item.EventId,["payload"]=JsonSerializer.SerializeToNode(new { stream.Identity,stream.Proof,eventRecord=item },CustodyBindingContract.Json) });
        var source=new JsonObject { ["requestSha256"]=QualificationProtocolSchema.Digest(phaseRequest),["bindingDigest"]=scope.BindingDigest,["caseId"]=caseId,["records"]=records };
        byte[] bytes=QualificationProofVerifier.CanonicalBytes(source);byte[] signature;
        lock(independentlyEnrolledSigner) signature=independentlyEnrolledSigner.SignData(bytes,HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence);
        var envelope=new JsonObject { ["payload"]=source,["signature"]=Convert.ToBase64String(signature) };
        byte[] exact=QualificationProofVerifier.CanonicalBytes(envelope);
        if(exact.Length>maximumEnvelopeBytes) throw new InvalidDataException("NativeSourceAggregateEnvelopeExceeds64MiB");
        await ImmutableFileArchive.PublishAsync(path+".measurement",exact,token).ConfigureAwait(false);
        await ImmutableFileArchive.PublishAsync(path,exact,token).ConfigureAwait(false);
        return new JsonObject { ["requestSha256"]=QualificationProtocolSchema.Digest(phaseRequest),["caseId"]=caseId,["sourceRecordsSha256"]=QualificationProtocolSchema.Digest(records) };
    }
    /// <summary>Recovers exact first signed measurement bytes only. Missing inner retention requires a native historical snapshot protocol.</summary>
    public async Task<JsonObject?> LookupAsync(string caseId,JsonObject phaseRequest,CancellationToken token)
    {
        if(!enrolledScopes.TryGetValue(caseId,out var scope) || phaseRequest["requestId"]?.GetValue<string>() is not { Length:64 } requestId || !requestId.All(char.IsAsciiHexDigit)
            || phaseRequest["bindingDigest"]?.GetValue<string>()!=scope.BindingDigest || JointPhaseRequestRecipe.Phase(phaseRequest)!=scope.Phase || !Path.IsPathFullyQualified(archiveDirectory)) return null;
        string authorization=QualificationProtocolSchema.Digest(new JsonObject { ["request"]=phaseRequest.DeepClone(),["caseId"]=caseId });
        if(!await currentAuthority.AuthorizeOperationAsync(authorization,"PublishIndependentJointPhaseNativeSource",token).ConfigureAwait(false)) return null;
        string path=Path.Combine(archiveDirectory,requestId,Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(caseId)))+".json");
        string retainedPath=await ProtectedArchiveStore.ExistsAsync(path,token).ConfigureAwait(false)?path:path+".measurement";
        if(!await ProtectedArchiveStore.ExistsAsync(retainedPath,token).ConfigureAwait(false)) return null; // External.ExactHistoricalNativePhaseSourceSnapshotProtocol
        byte[] bytes=await BoundedRegularFile.ReadAsync(retainedPath,MaximumEnvelopeBytes,token).ConfigureAwait(false);
        CustodyBindingContract.RejectDuplicateProperties(bytes);
        var existing=JsonNode.Parse(bytes)!.AsObject();var retained=existing["payload"]!.AsObject();
        if(retained["requestSha256"]?.GetValue<string>()!=QualificationProtocolSchema.Digest(phaseRequest) || retained["bindingDigest"]?.GetValue<string>()!=scope.BindingDigest || retained["caseId"]?.GetValue<string>()!=caseId
            || !independentlyEnrolledSigner.VerifyData(QualificationProofVerifier.CanonicalBytes(retained),Convert.FromBase64String(existing["signature"]!.GetValue<string>()),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence)) return null;
        await ImmutableFileArchive.PublishAsync(path,bytes,token).ConfigureAwait(false);
        return new JsonObject { ["requestSha256"]=retained["requestSha256"]!.DeepClone(),["caseId"]=caseId,["sourceRecordsSha256"]=QualificationProtocolSchema.Digest(retained["records"]!) };
    }

}
