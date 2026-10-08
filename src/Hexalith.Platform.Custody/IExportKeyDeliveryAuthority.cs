namespace Hexalith.Platform.Custody;

/// <summary>Independent current requester/commit-ack/lifecycle/store/purpose/audience authority. A recorder or custodian claim is insufficient.</summary>
public interface IExportKeyDeliveryAuthority
{
    /// <summary>Authenticates the current private caller/credential for the exact complete identity and named DeliverExportKey or LookupExportKey method; independent from physical release reauthorization.</summary>
    Task<bool> AuthorizeOperationAsync(ExportKeyDeliveryIdentity identity, string operation, CancellationToken cancellationToken = default);
    /// <summary>Authenticates every complete field and current actor/lifecycle revocation immediately before release preparation.</summary>
    Task<bool> AuthorizeAsync(ExportKeyDeliveryIdentity identity, CancellationToken cancellationToken = default);
    /// <summary>Authenticates the independently installed exact original operation owner and captured safe outcome digest, including initial absence. Restored missing, unknown or divergent terminal state denies lookup and effects; private caller credentials do not prove durable history.</summary>
    Task<bool> ValidateStateAsync(ExportKeyDeliveryIdentity identity, string exactStateDigest, CancellationToken cancellationToken = default);
    /// <summary>Conditionally advances the independently durable original-operation anchor before state persistence, matching the exact expected prior digest. Only absence to reserved/negative and reserved to immutable terminal transitions are permitted. Failed or unknown persistence remains unavailable until independent reconciliation.</summary>
    Task<bool> RecordStateAsync(ExportKeyDeliveryIdentity identity, string expectedStateDigest, string nextStateDigest, CancellationToken cancellationToken = default);

}
