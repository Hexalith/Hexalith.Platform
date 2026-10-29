using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Owned executable receiving composition for ERC private/source/guard/registrar methods, reusing the authenticated durable receiver.</summary>
public static class ErasureRootAuthorityApplication
{
    /// <summary>Serves actual installed owner ports; missing receiver genesis, grants, policy or signing inputs refuse rather than enroll.</summary>
    public static Task<int> RunAsync(string receiverInstallationFile, ErasureRootCustody custody, ErasureRootNativeOccurrenceSource source,
        IGovernanceScopeGuard actualGuard, IDeletionCapabilityRevocationAuthenticator actualRevocationAuthenticator, string acceptedCopyPolicySha256,
        CancellationToken cancellationToken = default)
        => IndependentAuthorityHttpApplication.RunAsync(receiverInstallationFile, ownerHandlers:
            [new ErasureRootAuthorityOperationHandler(custody, source, actualGuard, actualRevocationAuthenticator, acceptedCopyPolicySha256, TimeProvider.System)], cancellationToken: cancellationToken);
}
