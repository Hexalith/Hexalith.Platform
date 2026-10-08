namespace Hexalith.Platform.Custody;

/// <summary>Independent current requester/commit-ack/lifecycle/store/purpose/audience authority. A recorder or custodian claim is insufficient.</summary>
public interface IExportKeyDeliveryAuthority
{
    /// <summary>Authenticates every complete field and current actor/lifecycle revocation immediately before release preparation.</summary>
    Task<bool> AuthorizeAsync(ExportKeyDeliveryIdentity identity, CancellationToken cancellationToken = default);
}
