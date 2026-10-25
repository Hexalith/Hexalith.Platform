using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Concrete owning-process FR-34 engine composition with native custody material and independently anchored binding/encryption/completion sources.</summary>
public static class Fr34ProductionComposition
{
    /// <summary>Captures all material ports privately. Supplied installed originals, current authority and native holder profiles must already exist; no enrollment or effects occur here.</summary>
    public static Fr34ProductionPorts Create(DaprClient dapr,CustodyLifecycleClient custody,IInteractionOccurrenceRegistry actualRegistry,
        IPlatformKeyInventoryActor inventory,IPlatformKeyInventoryAuthority inventoryAuthority,IReadOnlyDictionary<string,CustodyInteractionBindingResource> bindings,
        IReadOnlyDictionary<string,CustodyInteractionMaterialProfile> materials,TimeProvider? clock=null)
    {
        var source=new CustodyInteractionProtectionBindingSource(dapr,bindings);var provider=new InteractionProtectionBindingProvider(source);
        var keys=new CustodyInteractionOccurrenceKeyProvider(dapr,new CustodyOccurrenceMaterialClient(custody),source,actualRegistry,inventory,inventoryAuthority,materials);
        return new(new InteractionPayloadProtectionService(actualRegistry,keys,provider,clock??TimeProvider.System),provider,keys,source);
    }
}
