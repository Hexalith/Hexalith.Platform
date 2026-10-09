using Hexalith.EventStore.Contracts.Security;
namespace Hexalith.Platform.Custody;

/// <summary>Independent current requester/commit-ack/lifecycle/store/purpose/audience authority. A recorder or custodian claim is insufficient.</summary>
public interface IExportKeyDeliveryAuthority : IAnchoredStateTransitionAuthority
{
    /// <summary>Authenticates the current private caller/credential for the exact complete identity and named DeliverExportKey or LookupExportKey method; independent from physical release reauthorization.</summary>
    Task<bool> AuthorizeOperationAsync(ExportKeyDeliveryIdentity identity, string operation, CancellationToken cancellationToken = default);
    /// <summary>Authenticates every complete field and current actor/lifecycle revocation immediately before release preparation.</summary>
    Task<bool> AuthorizeAsync(ExportKeyDeliveryIdentity identity, CancellationToken cancellationToken = default);
    /// <summary>Authenticates the independently installed exact original operation owner and captured safe outcome digest, including initial absence. Restored missing, unknown or divergent terminal state denies lookup and effects; private caller credentials do not prove durable history.</summary>
    Task<bool> ValidateStateAsync(ExportKeyDeliveryIdentity identity, string exactStateDigest, CancellationToken cancellationToken = default);
    /// <summary>Deprecated compatibility-only legacy anchor hook; current recoverable actors do not invoke it.
    /// Qualified implementations must implement the mandatory inherited IAnchoredStateTransitionAuthority admitted-original admission/recovery
    /// and conditional exact transition journal, including independent staging ownership, current permission and final durable-state/anchor confirmation.
    /// Implementing this legacy hook alone never enables an actor; omitted inherited proof defaults deny.</summary>
    Task<bool> RecordStateAsync(ExportKeyDeliveryIdentity identity, string expectedStateDigest, string nextStateDigest, CancellationToken cancellationToken = default);

}
