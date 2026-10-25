using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Current exact native root metadata adapter. Material release deliberately refuses the published consumer contract's unowned candidate-copy/multiple-call boundary until an independently ordered holder/ownership protocol is supplied.</summary>
public sealed class CustodyInteractionOccurrenceKeyDelivery(CustodyLifecycleClient custody,ICustodyKeyRegistrationSource registrations,
    IInteractionProtectionBindingSource bindings):IInteractionOccurrenceKeyDelivery
{
    /// <summary>Exact source-contract inputs still required for byte delivery. No installation flag can waive them.</summary>
    public IReadOnlyList<string> MissingMaterialInputs { get; }=["External.EventStoreOccurrenceCandidateKeyOwnershipAndRetirementContract",
        "External.IndependentlyOrderedAcquireResolveCurrentMaterialCopyLeasesAndNativeHolderCompletionProtocol"];
    /// <inheritdoc />
    public async Task<InteractionOccurrenceKeyDeliveryResult?> DeliverAsync(InteractionOccurrenceIdentity identity,string purpose,string version,string? keyReference,bool writing,CancellationToken token)
    {
        if(purpose is not ("interaction-root" or "tenant-digest") || purpose=="interaction-root" && version!=identity.RootKeyVersion) return null;
        var binding=await bindings.FindRetainedAsync(identity.Owner,new(identity.Sequence,identity.Kind,identity.PayloadTypeId),token).ConfigureAwait(false);
        if(binding?.Identity!=identity || keyReference is not null && binding.KeyReference!=keyReference) return null;
        // The existing consumer passes null reference for IsCurrent. This branch is value-free and causes no native allocation, release or copy.
        if(purpose!="interaction-root" || keyReference is not null) return null;
        // Exact root registration fields are independently retained; caller fields cannot invent the lifecycle/store/contract identity.
        if(registrations is not IInteractionRootRegistrationSource exact) return null;
        var registration=await exact.FindRootAsync(identity.Target,version,token).ConfigureAwait(false);
        if(registration is null || registration.Identity.TenantId!=identity.Target.TenantId || registration.Identity.ObjectId!=identity.Target.AgentInteractionId
            || registration.Identity.KeyAlias!=identity.Target.TargetProtectionKeyAlias || registration.Identity.KeyVersion!=version || registration.Identity.Purpose!=PlatformKeyPurpose.InteractionRootDek) return null;
        var original=await custody.LookupOriginalAsync(registration,null,token).ConfigureAwait(false);
        if(original.Status!=CustodyKeyLifecycleStatus.Wrapped || original.ReceiptId!=registration.WrapReceiptId) return null;
        if(await bindings.ReadErasureAsync(binding,token).ConfigureAwait(false) is not null) return null;
        return new(true,false,false,null);
    }
}
