using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independent actual actor inventory read and current outside-authority validation for the nine closed inventory cases.</summary>
public sealed class QualificationInventoryOriginalReader(IPlatformKeyInventoryActor inventory,IPlatformKeyInventoryAuthority authority,
    IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit,IReadOnlyDictionary<string,PlatformKeyVersion> enrolledKeys):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> PreflightStagesAsync(JsonObject original,CancellationToken token)=>audit.PreflightStagesAsync(original,token);
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject request,CancellationToken token)
    {
        string? id=SecretsProof.Text(request,"caseId"),requestId=SecretsProof.Text(request,"requestId");
        if(id is null || requestId is null || !id.StartsWith("S1.inventory.",StringComparison.Ordinal) || !enrolledKeys.TryGetValue(id,out var key)
            || request["enrolledScope"]?["tenantId"]?.GetValue<string>()!=key.TenantId || Purpose(id)!=key.Purpose) return null;
        var retained=await originals.LookupResultAsync("executor",requestId+":case-result",token).ConfigureAwait(false);
        if(retained?["inventory"] is not JsonObject recorded || recorded.Deserialize<PlatformKeyInventorySnapshot>(CustodyBindingContract.Json) is not { } original
            || !original.Versions.Any(value=>value.Key==key && value.State==PlatformHmacKeyState.Active)
            || !await authority.AuthorizeOperationAsync(key,"ReadKeyInventory",token).ConfigureAwait(false)) return null;
        var current=await inventory.ReadAsync(key).WaitAsync(token).ConfigureAwait(false);
        string digest=CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(current));
        if(current.TenantId!=key.TenantId || current.Purpose!=key.Purpose || current.KeyAlias!=key.KeyAlias || current.Revision<1
            || !await authority.ValidateStateAsync(key,current.Revision,digest,token).ConfigureAwait(false)) return null;
        var native=await audit.ReadAsync(request,token).ConfigureAwait(false);if(native is null || native.OriginalRevision<1) return null;
        string receipt=QualificationProtocolSchema.Digest(retained!);var state=QualificationDeniedCaseState.Create(request,native.OriginalRevision,receipt);
        state["outcome"]="Resolved";state["status"]="Resolved";state["keyVersion"]=key.Version;
        return new(requestId,receipt,"inventory:"+current.Revision,new JsonObject { ["state"]=state,["nativeStageBoundary"]=native.Facts?["nativeStageBoundary"]?.DeepClone(),["receipts"]=native.Receipts.DeepClone(),["nativeMutationSources"]=native.Facts?["nativeMutationSources"]?.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=new JsonObject(),
            ["inventoryState"]=JsonSerializer.SerializeToNode(current,CustodyBindingContract.Json),["inventoryKey"]=JsonSerializer.SerializeToNode(key,CustodyBindingContract.Json),["inventoryAuthorityRevision"]=current.Revision });
    }
    private static PlatformKeyPurpose? Purpose(string id)=>id["S1.inventory.".Length..] switch {
        "TenantKek"=>PlatformKeyPurpose.TenantKek,"ContentDigest"=>PlatformKeyPurpose.DigestKey,"SecurityObservation"=>PlatformKeyPurpose.SecurityObservationDigestKey,
        "ExportEnvelope"=>PlatformKeyPurpose.ExportEnvelopeKey,"ManifestSigning"=>PlatformKeyPurpose.ExportManifestSigningKey,"TrustedEnvelope"=>PlatformKeyPurpose.TrustedEnvelopeHmacKey,
        "MigrationEpoch" or "DirectoryEffect"=>PlatformKeyPurpose.MigrationDirectoryCapabilitySigningKey,"DeletionBatchCapability"=>PlatformKeyPurpose.DeletionBatchCapabilitySigningKey,_=>null };
}
