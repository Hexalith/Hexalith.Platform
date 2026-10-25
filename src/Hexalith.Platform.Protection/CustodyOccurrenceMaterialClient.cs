using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Production narrow capture of the reviewed authenticated Dapr custody lifecycle client.</summary>
public sealed class CustodyOccurrenceMaterialClient(CustodyLifecycleClient client):ICustodyOccurrenceMaterialClient
{
    /// <inheritdoc />
    public Task<CustodyKeyLifecycleOutcome> LookupAsync(CustodyKeyRegistration registration,CancellationToken token)=>client.LookupOriginalAsync(registration,null,token);
    /// <inheritdoc />
    public Task<CustodyKeyUseResult> UnwrapAsync(CustodyKeyRegistration registration,string operationId,CustodyKeyUse use,CancellationToken token)=>client.UnwrapAsync(registration,operationId,use,token);
    /// <inheritdoc />
    public Task<CustodyKeyUseResult> CompleteAsync(CustodyKeyRegistration registration,string operationId,CustodyKeyUse use,CancellationToken token)=>client.CompleteKeyUseAsync(registration,operationId,use,token);
}
