using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Owning-process FR-34 composition. The real engine, disjoint archive reader, retained manifests and independent binding source are mandatory installed dependencies.</summary>
public static class Fr34AuthorityApplication
{
    /// <summary>Serves the concrete Platform-owned source composition; no default candidate delivery provider is used.</summary>
    public static Task<int> RunAsync(string installation,Fr34ProductionPorts production,Fr34EventStoreCanaryReader reader,
        Hexalith.EventStore.Client.Gateway.IEventStoreGatewayClient privateWriteGateway,Hexalith.EventStore.Client.Gateway.IEventStoreGatewayBoundCommandProofSource proof,
        IndependentDaprAuthority originals,string principal,CustodyLifecycleClient custody,ICustodyReservedManifestSource manifests,TimeProvider? clock=null,IndependentDaprAuthority? independentMeasurementOriginals=null,IFr34OccurrenceEnrollment? dynamicEnrollment=null)
        =>RunAsync(installation,production.Engine,reader,production.Bindings,privateWriteGateway,proof,originals,principal,custody,manifests,clock,independentMeasurementOriginals,new CustodyOccurrenceHolderCompletionWorker(production.Keys),dynamicEnrollment??production.DynamicEnrollment);
    /// <summary>Serves authenticated current engine and observer requests without startup enrollment or a no-op fallback.</summary>
    public static Task<int> RunAsync(string independentServiceInstallationPath, InteractionPayloadProtectionService actualEngine,
        Fr34EventStoreCanaryReader independentReader, IInteractionProtectionBindingProvider independentBindings,
        Hexalith.EventStore.Client.Gateway.IEventStoreGatewayClient privateWriteGateway,
        Hexalith.EventStore.Client.Gateway.IEventStoreGatewayBoundCommandProofSource canaryProofs, IndependentDaprAuthority originals, string privatePrincipal,
        CustodyLifecycleClient custody, ICustodyReservedManifestSource manifests, TimeProvider? clock = null,IndependentDaprAuthority? independentMeasurementOriginals=null,Microsoft.Extensions.Hosting.IHostedService? holderCompletionWorker=null,IFr34OccurrenceEnrollment? dynamicEnrollment=null)
    {
        var handler = new Fr34OwnerOperationHandler(new(actualEngine, independentReader, privateWriteGateway, canaryProofs, originals, privatePrincipal, custody, manifests,dynamicEnrollment), independentReader, independentBindings, clock ?? TimeProvider.System,independentMeasurementOriginals is null?null:new Fr34IndependentOriginalCollector(new(actualEngine, independentReader, privateWriteGateway, canaryProofs, originals, privatePrincipal, custody, manifests,dynamicEnrollment),independentReader,independentMeasurementOriginals));
        return IndependentAuthorityHttpApplication.RunAsync(independentServiceInstallationPath, ownerHandlers: [handler],holderCompletionWorker:holderCompletionWorker);
    }
}
