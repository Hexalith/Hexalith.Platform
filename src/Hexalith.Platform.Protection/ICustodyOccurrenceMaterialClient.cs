using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Narrow actual custody original/read material boundary; no sign, wrap, pin or destroy authority is exposed to the occurrence provider.</summary>
public interface ICustodyOccurrenceMaterialClient
{
    /// <summary>Authenticates the exact original value-free root registration.</summary>
    Task<CustodyKeyLifecycleOutcome> LookupAsync(CustodyKeyRegistration registration,CancellationToken cancellationToken);
    /// <summary>Transfers a root only under the separately pre-admitted immutable native use.</summary>
    Task<CustodyKeyUseResult> UnwrapAsync(CustodyKeyRegistration registration,string operationId,CustodyKeyUse use,CancellationToken cancellationToken);
    /// <summary>Closes only the exact admitted use after independently current holder-completion evidence; local zeroing is insufficient.</summary>
    Task<CustodyKeyUseResult> CompleteAsync(CustodyKeyRegistration registration,string operationId,CustodyKeyUse use,CancellationToken cancellationToken);
}
