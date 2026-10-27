using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.Platform.Protection;

/// <summary>Distinct private recovery-proof capability; it cannot grant a new observation or a replay retry.</summary>
public interface IPrivateRecoveryCommandProofSource
{
    /// <summary>Requires independent current exact-original recovery authorization before signing the unchanged security command.</summary>
    ValueTask<string?> IssueRecoveryAsync(BoundWorkloadCommandRequest request,SecurityObservationRecoveryGrant grant,CancellationToken cancellationToken);
}
