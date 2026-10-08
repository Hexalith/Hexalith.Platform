namespace Hexalith.Platform.Custody;

/// <summary>Independent current requester/commit-ack/lifecycle/store/purpose/audience authority. A recorder or custodian claim is insufficient.</summary>
public interface IExportKeyDeliveryAuthority
{
    /// <summary>Authenticates the current private caller/credential for the exact complete identity and named DeliverExportKey or LookupExportKey method; independent from physical release reauthorization.</summary>
    Task<bool> AuthorizeOperationAsync(ExportKeyDeliveryIdentity identity, string operation, CancellationToken cancellationToken = default);
    /// <summary>Authenticates every complete field and current actor/lifecycle revocation immediately before release preparation.</summary>
    Task<bool> AuthorizeAsync(ExportKeyDeliveryIdentity identity, CancellationToken cancellationToken = default);
}
