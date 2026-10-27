using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Shipped owning-process construction of the existing actual guarded transaction, governance owner, same-owner protection and private revocation/signing ports.</summary>
public static class QualificationGuardProductionComposition
{
    /// <summary>Captures explicit already enrolled authorities. Missing source/policy/restore proofs refuse at their real ports; no genesis or native binding is inferred.</summary>
    public static QualificationGuardProductionPorts Create(DaprClient dapr,SecretsRuntimeConfiguration runtime,QualificationProtectionInstallation installed,IQualificationOriginalStore originals,TimeProvider? clock=null)
    {
        var time=clock??TimeProvider.System;var transport=new DaprSecretsInvoker(dapr);
        // Exact independently installed original/cell/receipt scopes are captured privately; missing inputs keep new commits denied.
        if(installed is not { GuardTarget:{ } target,GuardTargetAuthority:{ } targetOwner,GuardCellAuthorities:{ } cells,GuardReceiptAuthorities:{ } receipts,
            IndependentEvidenceComponent:{ } component,EvidenceResources:{ } resources,EvidenceAuthorities:{ } evidenceOwners,GuardArguments:{ } arguments,GuardDefinitions:{ } definitions,RevocationSubscription:{ } subscription })
            throw new InvalidOperationException("ExactExistingGuardSourcePolicyCellReceiptAndRevocationEnrollment");
        ICustodyOwnerStateAuthority Owner(CustodyLifecycleInstallation value)=>new CustodyOwnerAuthority(dapr,value.BindingName,value.TenantId,value.OwnerScope,value.EnrollmentJson,value.PublicKeyPem,
            new DaprCustodyOwnerCallSource(new(transport,value.OwnerAuthority,time)));
        var transactionAnchors=installed.GuardTransactionOriginals is { } originalBinding?new CustodyGuardedTransactionAnchors(dapr,new InstalledQualificationOriginalStore(new(transport,originalBinding,time)),cells.ToDictionary(pair=>pair.Key,pair=>pair.Value.OwnerScope),receipts.ToDictionary(pair=>pair.Key,pair=>pair.Value.OwnerScope)):null;
        var cellSource=new CustodyGuardedStateInstallation(target,installed.GuardTargetRevision,Owner(targetOwner),cells.ToDictionary(pair=>pair.Key,pair=>Owner(pair.Value)),receipts.ToDictionary(pair=>pair.Key,pair=>Owner(pair.Value)),time,transactionAnchors);
        var evidence=new CustodyGovernanceGuardInstallation(dapr,component,resources,evidenceOwners.ToDictionary(pair=>pair.Key,pair=>Owner(pair.Value)),time);
        var guarded=new DaprGuardedStateTransaction(dapr,time,new GuardedStateTransactionAuthority(cellSource));
        var guard=new GovernanceScopeGuardOwner(guarded,time,new GovernanceGuardAuthority(evidence));var proxies=ActorProxy.DefaultProxyFactory;
        var protection=new DaprDeletionProtectionOwner(target.TenantId,proxies,time);
        var consumption=ActorProxy.Create<IDeletionConsumptionActor>(new ActorId(DeletionConsumptionActor.GetActorId(target.TenantId)),DeletionConsumptionActor.ActorTypeName);
        var signer=new QualificationSigningActorRouter();var subscriber=new DeletionCapabilityRevocationSubscriber(subscription,new DaprRevocationAuthenticatorAdapter(runtime,transport,time),
            new DaprDeletionCapabilityCompromiseRegistrar(proxies),new EventStoreDeletionCapabilityGuardRevocationMirror(guard,time),time);
        var compiled=definitions.ToDictionary(pair=>pair.Key,pair=>QualificationGuardRecipeCatalog.Compile(pair.Key,pair.Value.TenantId,pair.Value.BatchId,pair.Value.EnrolledScopeSha256,arguments[pair.Key]),StringComparer.Ordinal);
        return new(guard,protection,signer,new(guard,protection,consumption,signer,originals,compiled,subscriber),compiled,new QualificationGuardedOriginalReader(guarded));
    }
}
