using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Exact source-owned denial originals on the explicitly enrolled independent immutable-original service. It creates neither enrollment nor actor state.</summary>
public sealed class InstalledExportKeyDeliveryDenialOriginalStore(IndependentDaprAuthority authority):IExportKeyDeliveryDenialOriginalStore
{
    private const string Role="private-export-delivery-denial-v1";
    private static string Id(ExportKeyDeliveryIdentity requested)=>"receiving-denial-v1:"+QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(requested,CustodyBindingContract.Json)!);
    private static string ReceivedId(ExportKeyDeliveryReceivingRequest request)=>"receiving-rejection-v2:"+QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(request,CustodyBindingContract.Json)!);
    /// <inheritdoc />
    public async Task<ExportKeyDeliveryReceivingDenialOriginal?> LookupReceivedAsync(ExportKeyDeliveryReceivingRequest request,CancellationToken token=default)
    {
        request.ValidateBounds();
        var original=await authority.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role=Role,requestId=ReceivedId(request) },token).ConfigureAwait(false);
        if(original is not { FinalAtAdmission:true,RetainedRevision:>0 }) return null;
        var denial=original.Request.Deserialize<ExportKeyDeliveryReceivingDenialOriginal>(CustodyBindingContract.Json);return denial is not null && denial.IsExact(request)?denial:null;
    }
    /// <inheritdoc />
    public async Task<ExportKeyDeliveryReceivingDenialOriginal?> RetainReceivedAsync(ExportKeyDeliveryReceivingDenialOriginal original,CancellationToken token=default)
    {
        var request=new ExportKeyDeliveryReceivingRequest(original.Target,original.Requested);request.ValidateBounds();if(!original.IsExact(request)) throw new ArgumentException("Actual exact receiving rejection required.");
        if(await LookupReceivedAsync(request,token).ConfigureAwait(false) is { } old) return old;
        await new InstalledQualificationOriginalStore(authority).BeginFinalAsync(Role,ReceivedId(request),(JsonObject)JsonSerializer.SerializeToNode(original,CustodyBindingContract.Json)!,token).ConfigureAwait(false);
        return await LookupReceivedAsync(request,token).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task<ExportKeyDeliveryDenialOriginal?> LookupAsync(ExportKeyDeliveryIdentity requested,CancellationToken cancellationToken=default)
    {
        requested.Validate();
        var original=await authority.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role=Role,requestId=Id(requested) },cancellationToken).ConfigureAwait(false);
        if(original is not { FinalAtAdmission:true,RetainedRevision:>0 }) return null;
        var denial=original.Request.Deserialize<ExportKeyDeliveryDenialOriginal>(CustodyBindingContract.Json);
        return denial is not null && denial.IsExact(requested)?denial:null;
    }
    /// <inheritdoc />
    public async Task<ExportKeyDeliveryDenialOriginal?> RetainAsync(ExportKeyDeliveryDenialOriginal original,CancellationToken cancellationToken=default)
    {
        if(!original.IsExact(original.RequestedIdentity)) throw new ArgumentException("Exact actual receiving denial required.");
        if(await LookupAsync(original.RequestedIdentity,cancellationToken).ConfigureAwait(false) is { } retained) return retained;
        // The complete denial is terminal at this admission: the durable original itself is the result.
        await new InstalledQualificationOriginalStore(authority).BeginFinalAsync(Role,Id(original.RequestedIdentity),
            (JsonObject)JsonSerializer.SerializeToNode(original,CustodyBindingContract.Json)!,cancellationToken).ConfigureAwait(false);
        return await LookupAsync(original.RequestedIdentity,cancellationToken).ConfigureAwait(false);
    }
}
