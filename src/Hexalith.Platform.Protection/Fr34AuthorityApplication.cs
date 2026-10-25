using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Owning-process FR-34 composition. The real engine, disjoint archive reader, retained manifests and independent binding source are mandatory installed dependencies.</summary>
public static class Fr34AuthorityApplication
{
    /// <summary>Serves authenticated current engine and observer requests without startup enrollment or a no-op fallback.</summary>
    public static Task<int> RunAsync(string independentServiceInstallationPath, InteractionPayloadProtectionService actualEngine,
        Fr34EventStoreCanaryReader independentReader, IInteractionProtectionBindingProvider independentBindings,
        Hexalith.EventStore.Client.Gateway.IEventStoreGatewayClient privateWriteGateway,
        Hexalith.EventStore.Client.Gateway.IEventStoreGatewayBoundCommandProofSource canaryProofs, IndependentDaprAuthority originals, string privatePrincipal,
        CustodyLifecycleClient custody, ICustodyReservedManifestSource manifests, TimeProvider? clock = null)
    {
        var handler = new Fr34OwnerOperationHandler(new(actualEngine, independentReader, privateWriteGateway, canaryProofs, originals, privatePrincipal, custody, manifests), independentReader, independentBindings, clock ?? TimeProvider.System);
        return IndependentAuthorityHttpApplication.RunAsync(independentServiceInstallationPath, ownerHandlers: [handler]);
    }
}
