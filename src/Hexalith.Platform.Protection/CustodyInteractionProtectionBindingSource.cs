using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;

namespace Hexalith.Platform.Protection;

/// <summary>Production retained-binding/erasure reader and durable once-only completion lease using exact Dapr ETag and outside-restore custody anchors. Missing installed original state always refuses.</summary>
public sealed class CustodyInteractionProtectionBindingSource(DaprClient dapr,IReadOnlyDictionary<string,CustodyInteractionBindingResource> enrolledResources):IInteractionProtectionBindingSource
{
    /// <summary>Gets the unambiguous exact occurrence locator used by independently installed enrollment.</summary>
    public static string ResourceId(AggregateIdentity owner,PayloadProtectionOccurrenceContext occurrence)=>Hash(new { owner,occurrence });
    /// <inheritdoc />
    public async Task<InteractionProtectionBinding?> FindRetainedAsync(AggregateIdentity owner,PayloadProtectionOccurrenceContext occurrence,CancellationToken token)
    { var read=await Read(owner,occurrence,token).ConfigureAwait(false);return read?.Record.Binding; }
    private async Task<(CustodyInteractionBindingResource Resource,CustodyInteractionBindingRecord Record,string ETag)?> Read(AggregateIdentity owner,PayloadProtectionOccurrenceContext occurrence,CancellationToken token)
    {
        string id=ResourceId(owner,occurrence);if(!enrolledResources.TryGetValue(id,out var resource)
            || !await resource.Authority.AuthorizeOperationAsync(id,"ReadInteractionProtectionBinding",token).ConfigureAwait(false)) return null;
        var (record,etag)=await dapr.GetStateAndETagAsync<CustodyInteractionBindingRecord>(resource.Component,resource.Key,ConsistencyMode.Strong,resource.Metadata,token).WaitAsync(token).ConfigureAwait(false);
        if(record is null || string.IsNullOrWhiteSpace(etag) || record.Revision<=0 || record.Binding.Identity.Owner!=owner || record.Binding.Identity.Sequence!=occurrence.RecordSequence
            || record.Binding.Identity.Kind!=occurrence.PayloadKind || record.Binding.Identity.PayloadTypeId!=occurrence.PayloadTypeId
            || !await resource.Authority.ValidateStateAsync(record.Revision,Hash(record),token).ConfigureAwait(false)
            || !await resource.Authority.AuthorizeOperationAsync(id,"ReadInteractionProtectionBinding",token).ConfigureAwait(false)) return null;
        return (resource,record,etag);
    }
    /// <inheritdoc />
    public async Task<bool> TryAcquireCompletionLeaseAsync(InteractionProtectionBinding binding,CancellationToken token)
    {
        var occurrence=new PayloadProtectionOccurrenceContext(binding.Identity.Sequence,binding.Identity.Kind,binding.Identity.PayloadTypeId);
        var read=await Read(binding.Identity.Owner,occurrence,token).ConfigureAwait(false);if(read is not { } observed || observed.Record.Binding!=binding || observed.Record.CompletionLeaseId is not null) return false;
        string id=ResourceId(binding.Identity.Owner,occurrence);string lease=Hash(new { schema="interaction-completion-lease-v1",binding });
        var next=observed.Record with { Revision=checked(observed.Record.Revision+1),CompletionLeaseId=lease };
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(next);var transition=new AnchoredStateTransition(observed.Resource.OwnerScope,observed.Record.Revision,next.Revision,Hash(observed.Record),Hash(next),bytes);
        if(!await observed.Resource.Authority.AuthorizeOperationAsync(id,"AcquireInteractionProtectionCompletionLease",token).ConfigureAwait(false)
            || !await observed.Resource.Authority.AdmitTransitionAsync(transition,token).ConfigureAwait(false)) return false;
        // A lost save/record acknowledgement returns false. It never creates another lease or rewrites a restored original.
        if(!await dapr.TrySaveStateAsync(observed.Resource.Component,observed.Resource.Key,next,observed.ETag,metadata:observed.Resource.Metadata,cancellationToken:token).WaitAsync(token).ConfigureAwait(false)) return false;
        return await observed.Resource.Authority.RecordTransitionAsync(transition,token).ConfigureAwait(false)
            && await observed.Resource.Authority.ValidateStateAsync(next.Revision,Hash(next),token).ConfigureAwait(false);
    }
    /// <summary>Acquires the one independently anchored encryption lease for the exact registry original. A restored/repeated reservation never obtains another lease.</summary>
    public async Task<bool> TryAcquireEncryptionLeaseAsync(InteractionOccurrenceRecord record,IInteractionOccurrenceRegistry registry,CancellationToken token)
    {
        var identity=record.Request.Identity;var occurrence=new PayloadProtectionOccurrenceContext(identity.Sequence,identity.Kind,identity.PayloadTypeId);
        var read=await Read(identity.Owner,occurrence,token).ConfigureAwait(false);
        var current=await registry.LookupAsync(identity,token).ConfigureAwait(false);
        if(read is not { } observed || observed.Record.Binding.Identity!=identity || observed.Record.Binding.KeyReference!=record.KeyReference
            || observed.Record.EncryptionLeaseId is not null || current.Status!=InteractionOccurrenceReservationStatus.Reserved || current.Record is not { } original || JsonSerializer.Serialize(original)!=JsonSerializer.Serialize(record)) return false;
        string id=ResourceId(identity.Owner,occurrence);string lease=Hash(new { schema="interaction-encryption-lease-v1",identity,record.KeyReference,record.RegistryRevision });
        var next=observed.Record with { Revision=checked(observed.Record.Revision+1),EncryptionLeaseId=lease,EncryptionKeyReference=record.KeyReference };
        var transition=new AnchoredStateTransition(observed.Resource.OwnerScope,observed.Record.Revision,next.Revision,Hash(observed.Record),Hash(next),JsonSerializer.SerializeToUtf8Bytes(next));
        if(!await observed.Resource.Authority.AuthorizeOperationAsync(id,"AcquireInteractionProtectionEncryptionLease",token).ConfigureAwait(false)
            || !await observed.Resource.Authority.AdmitTransitionAsync(transition,token).ConfigureAwait(false)) return false;
        if(!await dapr.TrySaveStateAsync(observed.Resource.Component,observed.Resource.Key,next,observed.ETag,metadata:observed.Resource.Metadata,cancellationToken:token).WaitAsync(token).ConfigureAwait(false)) return false;
        return await observed.Resource.Authority.RecordTransitionAsync(transition,token).ConfigureAwait(false)
            && await observed.Resource.Authority.ValidateStateAsync(next.Revision,Hash(next),token).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task<InteractionProtectionErasureWitness?> ReadErasureAsync(InteractionProtectionBinding binding,CancellationToken token)
    {
        var read=await Read(binding.Identity.Owner,new(binding.Identity.Sequence,binding.Identity.Kind,binding.Identity.PayloadTypeId),token).ConfigureAwait(false);
        return read?.Record.Binding==binding?read.Value.Record.Erasure:null;
    }
    private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
