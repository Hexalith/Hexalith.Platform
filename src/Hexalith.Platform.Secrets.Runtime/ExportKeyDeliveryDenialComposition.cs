using Dapr.Actors.Runtime;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Owning actor-process composition. The credential and immutable writer stay inside the actor activator closure.</summary>
public static class ExportKeyDeliveryDenialComposition
{
    /// <summary>Registers the real actor with its privately captured optional exact denial store. Absent installation grants no historical denial recovery.</summary>
    public static void RegisterInstalledPrivateExportKeyDeliveryOwner(this PrivateExportKeyDeliveryOwner owner,ISecretsCredentialBoundDaprInvoker transport,IndependentDaprAuthorityBinding? installed,TimeProvider? clock=null)
    {
        var actualClock=clock??TimeProvider.System;
        IExportKeyDeliveryDenialOriginalStore? originals=installed is null?null:new InstalledExportKeyDeliveryDenialOriginalStore(new(transport,installed,actualClock));
        owner.Actors.RegisterActor<ExportKeyDeliveryActor>(ExportKeyDeliveryActor.ActorTypeName,registration=>registration.Activator=new PrivateActivator(actualClock,owner.Authority,owner.Provider,originals));
    }
    private sealed class PrivateActivator(TimeProvider clock,IExportKeyDeliveryAuthority authority,IExportKeyDirectDeliveryProvider? provider,IExportKeyDeliveryDenialOriginalStore? originals):ActorActivator
    {
        public override Task<ActorActivatorState> CreateAsync(ActorHost host)=>Task.FromResult(new ActorActivatorState(new ExportKeyDeliveryActor(host,clock,authority,provider,originals)));
        public override Task DeleteAsync(ActorActivatorState state)=>Task.CompletedTask;
    }
}
