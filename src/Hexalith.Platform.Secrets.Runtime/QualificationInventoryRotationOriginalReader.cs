using System.Text.Json.Nodes;
using System.Text.Json;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Independent current inventory/retained digest and real invalidation consumer reader for the exact rotation original.</summary>
public sealed class QualificationInventoryRotationOriginalReader(IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit,QualificationInventoryRotationWorkflow rotation):IQualificationObservedOriginalReader
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PreflightStagesAsync(JsonObject original,CancellationToken token)
        =>(await rotation.PreflightAsync(original,token).ConfigureAwait(false)).Concat(await audit.PreflightStagesAsync(original,token).ConfigureAwait(false)).Distinct(StringComparer.Ordinal).ToArray();
    /// <inheritdoc />
    public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject request,CancellationToken token)
    {
        string? requestId=SecretsProof.Text(request,"requestId");if(requestId is null) return null;
        var retained=await originals.LookupResultAsync("executor",requestId+":case-result",token).ConfigureAwait(false);var current=await rotation.ObserveAsync(request,token).ConfigureAwait(false);
        var native=await audit.ReadAsync(request,token).ConfigureAwait(false);
        if(retained is null || current is null || native is null || current["inventory"] is not JsonObject inventory || current["safetyVerdicts"] is not JsonArray verdicts
            || current["retainedDigest"] is not JsonObject digest || retained["originalDigest"] is not JsonObject old) return null;
        var rotated=retained["nativeRotationOriginal"]?.Deserialize<QualificationRotationOriginal>(CustodyBindingContract.Json);
        var previous=retained["previousKey"]?.Deserialize<PlatformKeyVersion>(CustodyBindingContract.Json);
        var actualInventory=inventory.Deserialize<PlatformKeyInventorySnapshot>(CustodyBindingContract.Json);
        if(rotated is null || await rotation.ReadNativeOriginalAsync(rotated.Signal,token).ConfigureAwait(false)!=rotated || previous is null || actualInventory is null || rotated.RotatedAt<=0 || rotated.OldVerifyUntil<=rotated.RotatedAt
            || !actualInventory.Signals.Contains(rotated.Signal) || rotated.Signal.Action!=PlatformKeyInventoryAction.InstallCurrent
            || rotated.InvalidationReceiptSha256!=retained["invalidationReceiptSha256"]?.GetValue<string>()
            || rotated.PublicAnchorId!=rotated.Signal.Key.PublicAnchorId || rotated.PublicAnchorVersion!=rotated.Signal.Key.PublicAnchorVersion
            || actualInventory.Versions.Count(value=>value.State==PlatformHmacKeyState.Active)!=1
            || actualInventory.Versions.Single(value=>value.State==PlatformHmacKeyState.Active).Key!=rotated.Signal.Key
            || !actualInventory.Versions.Any(value=>value.Key==previous && value.State==PlatformHmacKeyState.Retained)
            || previous.Version==rotated.Signal.Key.Version) return null;
        string receipt=QualificationProtocolSchema.Digest(retained);var state=QualificationDeniedCaseState.Create(request,native.OriginalRevision,receipt);state["outcome"]="Invalidated";state["status"]="Invalidated";
        state["keyVersion"]=rotated.Signal.Key.Version;
        state["rotation"]=new JsonObject { ["previousKeyVersion"]=previous.Version,["currentKeyVersion"]=rotated.Signal.Key.Version,["rotatedAt"]=rotated.RotatedAt,["oldVerifyUntil"]=rotated.OldVerifyUntil,["publicAnchorId"]=rotated.PublicAnchorId,["publicAnchorVersion"]=rotated.PublicAnchorVersion };
        state["timing"]!["rotatedAt"]=rotated.RotatedAt;
        return new(requestId,receipt,"rotation:"+inventory["Revision"]!.GetValue<long>(),new JsonObject { ["state"]=state,["nativeStageBoundary"]=native.Facts?["nativeStageBoundary"]?.DeepClone(),["receipts"]=native.Receipts.DeepClone(),["nativeMutationSources"]=native.Facts?["nativeMutationSources"]?.DeepClone(),["targetMutations"]=native.TargetMutations.DeepClone(),
            ["surfaceScans"]=native.SurfaceScans.DeepClone(),["copyCensus"]=native.CopyCensus.DeepClone(),["linearization"]=new JsonObject(),["rotationInventory"]=inventory.DeepClone(),
            ["rotationVerdicts"]=verdicts.DeepClone(),["retainedDigestOriginal"]=old.DeepClone(),["retainedDigestCurrent"]=digest.DeepClone() });
    }
}
