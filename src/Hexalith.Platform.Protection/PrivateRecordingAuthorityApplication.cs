using Hexalith.EventStore.Client.Gateway;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Receiving-process composition for the actual restricted EventStore replay and security recorder.</summary>
public static class PrivateRecordingAuthorityApplication
{
    /// <summary>Runs the authenticated authority endpoints with native source reads and bound-command ACLs. Captures all credentials privately.</summary>
    public static Task<int> RunAsync(string independentServiceInstallationPath, IEventStoreGatewayClient privateWorkloadGateway,
        IEventStoreGatewayBoundCommandProofSource restrictedCommandProofs, IndependentDaprAuthority independentOriginals,
        IReplicatedSecuritySpoolAuthority recoveryAuthority, string privatePrincipal, TimeProvider? clock = null)
    {
        var source = new EventStorePrivateRecordingPort(privateWorkloadGateway, restrictedCommandProofs, independentOriginals,
            recoveryAuthority, privatePrincipal, clock ?? TimeProvider.System);
        return IndependentAuthorityHttpApplication.RunAsync(independentServiceInstallationPath, source);
    }
}
