using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;

namespace Hexalith.Platform.Protection;

/// <summary>Production retained-binding/erasure reader and durable once-only completion lease using exact Dapr ETag and outside-restore custody anchors. Missing installed original state always refuses.</summary>
public sealed class CustodyInteractionProtectionBindingSource(DaprClient dapr,IReadOnlyDictionary<string,CustodyInteractionBindingResource> enrolledResources,IFr34OccurrenceEnrollment? dynamicEnrollment=null):IInteractionProtectionBindingSource
{
    /// <summary>Gets the unambiguous exact occurrence locator used by independently installed enrollment.</summary>
    public static string ResourceId(AggregateIdentity owner,PayloadProtectionOccurrenceContext occurrence)=>Hash(new { owner,occurrence });
    /// <inheritdoc />
    public async Task<InteractionProtectionBinding?> FindRetainedAsync(AggregateIdentity owner,PayloadProtectionOccurrenceContext occurrence,CancellationToken token)
    { var read=await Read(owner,occurrence,token).ConfigureAwait(false);return read?.Record.Binding ?? (enrolledResources.ContainsKey(ResourceId(owner,occurrence)) || dynamicEnrollment is null?null:(await dynamicEnrollment.ResolveAsync(owner,occurrence,token).ConfigureAwait(false))?.Binding); }
    private async Task<(CustodyInteractionBindingResource Resource,CustodyInteractionBindingRecord Record,string ETag)?> Read(AggregateIdentity owner,PayloadProtectionOccurrenceContext occurrence,CancellationToken token)
    {
        string id=ResourceId(owner,occurrence);if(!enrolledResources.TryGetValue(id,out var resource)
            || !await resource.Authority.AuthorizeOperationAsync(id,"ReadInteractionProtectionBinding",token).ConfigureAwait(false)) return null;
        var (record,etag)=await dapr.GetStateAndETagAsync<CustodyInteractionBindingRecord>(resource.Component,resource.Key,ConsistencyMode.Strong,resource.Metadata,token).WaitAsync(token).ConfigureAwait(false);
        if(record is null || string.IsNullOrWhiteSpace(etag) || record.Revision<=0 || record.Binding.Identity.Owner!=owner || record.Binding.Identity.Sequence!=occurrence.RecordSequence
            || record.Binding.Identity.Kind!=occurrence.PayloadKind || record.Binding.Identity.PayloadTypeId!=occurrence.PayloadTypeId) return null;
        try
        {
            var observed=record;AnchoredStateTransition? finalTransition=null;
            // A restore may cross both original leases. Each step must be an immutable independently verified link; only the current final target is persisted.
            for(int step=0;step<2;step++)
            {
                var pending=await dapr.GetStateAsync<AnchoredStateTransition>(resource.Component,PendingKey(resource,record.Revision+1),ConsistencyMode.Strong,resource.Metadata,token).WaitAsync(token).ConfigureAwait(false);
                if(pending is null) break;
                if(pending.ScopeId!=resource.OwnerScope || pending.ExpectedRevision!=record.Revision || pending.TargetRevision!=record.Revision+1
                    || pending.PredecessorDigest!=Hash(record) || pending.TargetBytes.Length is <=0 or >RecoverableAnchoredState.MaximumPendingBytes
                    || Convert.ToHexString(SHA256.HashData(pending.TargetBytes))!=pending.TargetDigest) return null;
                var prospective=JsonSerializer.Deserialize<CustodyInteractionBindingRecord>(pending.TargetBytes);
                if(prospective is null || prospective.Binding!=record.Binding || prospective.Revision!=pending.TargetRevision || Hash(prospective)!=pending.TargetDigest) return null;
                bool verified=await resource.Authority.VerifyTransitionAsync(pending,token).ConfigureAwait(false);
                if(!verified && await resource.Authority.ValidateStateAsync(record.Revision,Hash(record),token).ConfigureAwait(false)
                    && await resource.Authority.RecoverTransitionAsync(pending,token).ConfigureAwait(false)) verified=await resource.Authority.VerifyTransitionAsync(pending,token).ConfigureAwait(false);
                if(!verified) return null;
                record=prospective;finalTransition=pending;
            }
            if(!await resource.Authority.ValidateStateAsync(record.Revision,Hash(record),token).ConfigureAwait(false))
            {
                // Earlier source saved a lease before the authority acknowledgement and retained no local pending slot.
                bool recovered=false;
                foreach(var predecessor in Predecessors(record))
                {
                    var exact=RecoverableAnchoredState.Prepare(resource.OwnerScope,predecessor.Revision,record.Revision,predecessor,record);
                    if(await resource.Authority.RecoverTransitionAsync(exact,token).ConfigureAwait(false)
                        && await resource.Authority.VerifyTransitionAsync(exact,token).ConfigureAwait(false)
                        && await resource.Authority.ValidateStateAsync(record.Revision,Hash(record),token).ConfigureAwait(false)) { recovered=true;break; }
                }
                if(!recovered) return null;
            }
            if(Hash(observed)!=Hash(record))
            {
                if(!await dapr.TrySaveStateAsync(resource.Component,resource.Key,record,etag,metadata:resource.Metadata,cancellationToken:token).WaitAsync(token).ConfigureAwait(false)) return null;
                var fresh=await dapr.GetStateAndETagAsync<CustodyInteractionBindingRecord>(resource.Component,resource.Key,ConsistencyMode.Strong,resource.Metadata,token).WaitAsync(token).ConfigureAwait(false);
                if(fresh.value is null || Hash(fresh.value)!=Hash(record)) return null;record=fresh.value;etag=fresh.etag;
            }
            return (finalTransition is null || await resource.Authority.VerifyTransitionAsync(finalTransition,token).ConfigureAwait(false))
                && await resource.Authority.ValidateStateAsync(record.Revision,Hash(record),token).ConfigureAwait(false)
                && await resource.Authority.AuthorizeOperationAsync(id,"ReadInteractionProtectionBinding",token).ConfigureAwait(false)?(resource,record,etag):null;
        }
        catch(Exception) { token.ThrowIfCancellationRequested();return null; }
    }
    private static IEnumerable<CustodyInteractionBindingRecord> Predecessors(CustodyInteractionBindingRecord record)
    {
        if(record.Revision<=1) yield break;
        if(record.CompletionLeaseId is not null) yield return record with { Revision=record.Revision-1,CompletionLeaseId=null };
        if(record.EncryptionLeaseId is not null) yield return record with { Revision=record.Revision-1,EncryptionLeaseId=null,EncryptionKeyReference=null };
    }
    private static string PendingKey(CustodyInteractionBindingResource resource,long revision)=>resource.Key+":lease-pending:"+revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private async Task<bool> Commit(CustodyInteractionBindingResource resource,AnchoredStateTransition transition,CancellationToken token)
    {
        string key=PendingKey(resource,transition.TargetRevision);
        return await RecoverableAnchoredState.CommitAsync(transition,resource.Authority,
            async ()=>await dapr.GetStateAsync<AnchoredStateTransition>(resource.Component,key,ConsistencyMode.Strong,resource.Metadata,token).WaitAsync(token).ConfigureAwait(false),async pending=>
            {
                var read=await dapr.GetStateAndETagAsync<AnchoredStateTransition>(resource.Component,key,ConsistencyMode.Strong,resource.Metadata,token).WaitAsync(token).ConfigureAwait(false);
                if(read.value is not null)
                {
                    if(JsonSerializer.Serialize(read.value)!=JsonSerializer.Serialize(pending)) throw new InvalidOperationException("Conflicting retained lease original.");
                    return;
                }
                if(!await dapr.TrySaveStateAsync(resource.Component,key,pending,read.etag,metadata:resource.Metadata,cancellationToken:token).WaitAsync(token).ConfigureAwait(false))
                    throw new InvalidOperationException("Retained lease original publication unknown.");
            }).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task<bool> TryAcquireCompletionLeaseAsync(InteractionProtectionBinding binding,CancellationToken token)
    {
        var occurrence=new PayloadProtectionOccurrenceContext(binding.Identity.Sequence,binding.Identity.Kind,binding.Identity.PayloadTypeId);
        if(!enrolledResources.ContainsKey(ResourceId(binding.Identity.Owner,occurrence))) return dynamicEnrollment is not null && await dynamicEnrollment.AcquireLeaseAsync(binding,true,token).ConfigureAwait(false);
        var read=await Read(binding.Identity.Owner,occurrence,token).ConfigureAwait(false);if(read is not { } observed || observed.Record.Binding!=binding || observed.Record.CompletionLeaseId is not null) return false;
        string id=ResourceId(binding.Identity.Owner,occurrence);string lease=Hash(new { schema="interaction-completion-lease-v1",binding });
        var next=observed.Record with { Revision=checked(observed.Record.Revision+1),CompletionLeaseId=lease };
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(next);var transition=new AnchoredStateTransition(observed.Resource.OwnerScope,observed.Record.Revision,next.Revision,Hash(observed.Record),Hash(next),bytes);
        if(!await observed.Resource.Authority.AuthorizeOperationAsync(id,"AcquireInteractionProtectionCompletionLease",token).ConfigureAwait(false)
            || !await Commit(observed.Resource,transition,token).ConfigureAwait(false)) return false;
        // The immutable pending original and independent anchor precede the primary save; reads reconcile that same lease after loss.
        if(!await dapr.TrySaveStateAsync(observed.Resource.Component,observed.Resource.Key,next,observed.ETag,metadata:observed.Resource.Metadata,cancellationToken:token).WaitAsync(token).ConfigureAwait(false)) return false;
        return await observed.Resource.Authority.ValidateStateAsync(next.Revision,Hash(next),token).ConfigureAwait(false);
    }
    /// <summary>Acquires the one independently anchored encryption lease for the exact registry original. A restored/repeated reservation never obtains another lease.</summary>
    public async Task<bool> TryAcquireEncryptionLeaseAsync(InteractionOccurrenceRecord record,IInteractionOccurrenceRegistry registry,CancellationToken token)
    {
        var identity=record.Request.Identity;var occurrence=new PayloadProtectionOccurrenceContext(identity.Sequence,identity.Kind,identity.PayloadTypeId);
        var current=await registry.LookupAsync(identity,token).ConfigureAwait(false);
        if(!enrolledResources.ContainsKey(ResourceId(identity.Owner,occurrence)))
        {
            var claim=dynamicEnrollment is null?null:await dynamicEnrollment.ResolveAsync(identity.Owner,occurrence,token).ConfigureAwait(false);
            return claim?.Binding.Identity==identity && claim.Binding.KeyReference==record.KeyReference && current.Status==InteractionOccurrenceReservationStatus.Reserved
                && JsonSerializer.Serialize(current.Record)==JsonSerializer.Serialize(record) && await dynamicEnrollment!.AcquireLeaseAsync(claim.Binding,false,token).ConfigureAwait(false);
        }
        var read=await Read(identity.Owner,occurrence,token).ConfigureAwait(false);
        if(read is not { } observed || observed.Record.Binding.Identity!=identity || observed.Record.Binding.KeyReference!=record.KeyReference
            || observed.Record.EncryptionLeaseId is not null || current.Status!=InteractionOccurrenceReservationStatus.Reserved || current.Record is not { } original || JsonSerializer.Serialize(original)!=JsonSerializer.Serialize(record)) return false;
        string id=ResourceId(identity.Owner,occurrence);string lease=Hash(new { schema="interaction-encryption-lease-v1",identity,record.KeyReference,record.RegistryRevision });
        var next=observed.Record with { Revision=checked(observed.Record.Revision+1),EncryptionLeaseId=lease,EncryptionKeyReference=record.KeyReference };
        var transition=new AnchoredStateTransition(observed.Resource.OwnerScope,observed.Record.Revision,next.Revision,Hash(observed.Record),Hash(next),JsonSerializer.SerializeToUtf8Bytes(next));
        if(!await observed.Resource.Authority.AuthorizeOperationAsync(id,"AcquireInteractionProtectionEncryptionLease",token).ConfigureAwait(false)
            || !await Commit(observed.Resource,transition,token).ConfigureAwait(false)) return false;
        if(!await dapr.TrySaveStateAsync(observed.Resource.Component,observed.Resource.Key,next,observed.ETag,metadata:observed.Resource.Metadata,cancellationToken:token).WaitAsync(token).ConfigureAwait(false)) return false;
        return await observed.Resource.Authority.ValidateStateAsync(next.Revision,Hash(next),token).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task<InteractionProtectionErasureWitness?> ReadErasureAsync(InteractionProtectionBinding binding,CancellationToken token)
    {
        var read=await Read(binding.Identity.Owner,new(binding.Identity.Sequence,binding.Identity.Kind,binding.Identity.PayloadTypeId),token).ConfigureAwait(false);
        return read?.Record.Binding==binding?read.Value.Record.Erasure:null;
    }
    private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
