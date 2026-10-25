using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Actual receiving producer over independently enrolled finite native inputs and indexed immutable originals. It generates no key, native admission, closure witness or genesis.</summary>
public sealed class Fr34OccurrenceProducer(Fr34OccurrenceRecipe recipe,IQualificationOriginalStore claims,IQualificationOriginalStore readUses,ICustodyOccurrenceMaterialClient native):IFr34OccurrenceEnrollment
{
    private static JsonObject Node<T>(T value)=>(JsonObject)JsonSerializer.SerializeToNode(value,CustodyBindingContract.Json)!;
    private static string Id<T>(T value)=>QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(value,CustodyBindingContract.Json)!);
    private bool Valid()=>SecretsProvisioning.IsReference(recipe.RecipeId) && recipe.Slots.Count is >0 and <=128 && recipe.LifecycleEpoch>0
        && recipe.Slots.Select(value=>value.SlotId).Distinct(StringComparer.Ordinal).Count()==recipe.Slots.Count
        && recipe.Slots.Select(value=>value.Write.Registration.OpaqueWrappedKeyReference).Distinct(StringComparer.Ordinal).Count()==recipe.Slots.Count
        && recipe.PayloadTypeId==typeof(Fr34CanaryPersisted).FullName && recipe.DigestVersion.Length>0 && recipe.ProtectionProfileVersion.Length>0
        && recipe.Slots.All(slot=>SecretsProvisioning.IsReference(slot.SlotId) && SecretsProvisioning.IsReference(slot.KeyReference) && slot.Reads.Count is >0 and <=32 && slot.Write.Registration.Identity.TenantId==recipe.Target.TenantId
            && slot.Write.Registration.Identity.Purpose==PlatformKeyPurpose.InteractionRootDek && slot.Write.Registration.Identity.KeyVersion.Length>0
            && slot.Reads.All(read=>read.Registration==slot.Write.Registration && read.DigestReference==slot.Write.DigestReference)
            && slot.Reads.Append(slot.Write).All(profile=>profile.KeyUse.OriginalRootReference==profile.Registration.OpaqueWrappedKeyReference)
            && slot.Reads.Append(slot.Write).Select(value=>Id(value.KeyUse)).Distinct(StringComparer.Ordinal).Count()==slot.Reads.Count+1);
    /// <inheritdoc />
    public async Task<Fr34OccurrenceClaim?> ResolveAsync(AggregateIdentity owner,PayloadProtectionOccurrenceContext occurrence,CancellationToken token)
    {
        if(!Valid() || owner.TenantId!=recipe.Target.TenantId || owner.Domain!="fr34-canary" || owner.AggregateId is not { Length:64 } canary
            || !canary.All(char.IsAsciiHexDigit) || occurrence.RecordSequence!=1 || occurrence.PayloadKind!=PayloadProtectionPayloadKind.Event || occurrence.PayloadTypeId!=recipe.PayloadTypeId) return null;
        var request=Node(new { recipe.RecipeId,owner,occurrence });
        foreach(var slot in recipe.Slots)
        {
            string id=recipe.RecipeId+":"+slot.SlotId;
            var retained=await claims.LookupResultAsync("fr34-recipes",id,token).ConfigureAwait(false);
            if(retained is not null)
            {
                var old=retained.Deserialize<Fr34OccurrenceClaim>(CustodyBindingContract.Json);
                if(old?.CanaryId==canary) return Exact(old,owner,occurrence)?old:null;
                continue;
            }
            var admission=await claims.ReadAdmissionAsync("fr34-recipes",id,request,token).ConfigureAwait(false);
            if(admission==QualificationOriginalAdmissionState.Unknown) return null; // Foreign/unknown unfinished claim never permits another slot.
            if(admission==QualificationOriginalAdmissionState.Absent)
            {
                try { await claims.BeginAsync("fr34-recipes",id,request,token).ConfigureAwait(false); }
                catch(InvalidOperationException) { return null; }
            }
            var key=slot.Write.Registration.Identity;
            var identity=new InteractionOccurrenceIdentity(new(key.TenantId,key.ObjectId,key.KeyAlias),owner,occurrence.PayloadKind,occurrence.RecordSequence,occurrence.PayloadTypeId,key.KeyVersion,recipe.ProtectionProfileVersion);
            var claim=new Fr34OccurrenceClaim(recipe.RecipeId,slot.SlotId,canary,new(identity,recipe.DigestVersion,1,slot.KeyReference,Id(request),recipe.LifecycleEpoch),slot.Write,slot.Reads);
            if(await native.LookupAsync(slot.Write.Registration,token).ConfigureAwait(false) is not { Status:CustodyKeyLifecycleStatus.Wrapped } original || original.ReceiptId!=slot.Write.Registration.WrapReceiptId) return null;
            await claims.RetainAsync("fr34-recipes",id,Node(claim),token).ConfigureAwait(false);return claim;
        }
        return null;
    }
    private bool Exact(Fr34OccurrenceClaim claim,AggregateIdentity owner,PayloadProtectionOccurrenceContext occurrence)
    {
        var slot=recipe.Slots.SingleOrDefault(value=>value.SlotId==claim.SlotId);var identity=claim.Binding.Identity;
        return claim.RecipeId==recipe.RecipeId && slot is not null && claim.Write==slot.Write && JsonSerializer.Serialize(claim.Reads)==JsonSerializer.Serialize(slot.Reads)
            && identity.Owner==owner && identity.Sequence==occurrence.RecordSequence && identity.Kind==occurrence.PayloadKind && identity.PayloadTypeId==occurrence.PayloadTypeId
            && identity.Target==new ProtectionTarget(slot.Write.Registration.Identity.TenantId,slot.Write.Registration.Identity.ObjectId,slot.Write.Registration.Identity.KeyAlias)
            && identity.RootKeyVersion==slot.Write.Registration.Identity.KeyVersion && identity.DerivationProfileVersion==recipe.ProtectionProfileVersion
            && claim.Binding.KeyReference==slot.KeyReference && claim.Binding.DigestKeyVersion==recipe.DigestVersion && claim.Binding.LifecycleEpoch==recipe.LifecycleEpoch
            && claim.Binding.ReservationAttemptOrdinal==1 && claim.Binding.OperationId==Id(Node(new { recipe.RecipeId,owner,occurrence }));
    }
    private Task<Fr34OccurrenceClaim?> Claim(InteractionOccurrenceIdentity identity,CancellationToken token)=>ResolveAsync(identity.Owner,new(identity.Sequence,identity.Kind,identity.PayloadTypeId),token);
    /// <inheritdoc />
    public async Task<bool> AcquireLeaseAsync(InteractionProtectionBinding binding,bool completion,CancellationToken token)
    {
        if(await Claim(binding.Identity,token).ConfigureAwait(false) is not { } claim || claim.Binding!=binding) return false;
        return await claims.BeginAsync("fr34-recipes",recipe.RecipeId+":"+claim.SlotId+(completion?":completion":":encryption"),Node(binding),token).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task<CustodyInteractionMaterialProfile?> MaterialAsync(InteractionOccurrenceIdentity identity,bool writing,CancellationToken token)
    {
        if(await Claim(identity,token).ConfigureAwait(false) is not { } claim || claim.Binding.Identity!=identity) return null;
        if(writing) return claim.Write;
        for(int ordinal=0;ordinal<claim.Reads.Count;ordinal++)
        {
            var profile=claim.Reads[ordinal];string id=recipe.RecipeId+":"+claim.SlotId+":read:"+ordinal;
            var input=Node(new { identity,profile });
            if(await readUses.LookupResultAsync("occurrence-read-use",id,token).ConfigureAwait(false) is not null) continue;
            var state=await readUses.ReadAdmissionAsync("occurrence-read-use",id,input,token).ConfigureAwait(false);
            if(state==QualificationOriginalAdmissionState.Unknown) return null;
            if(state==QualificationOriginalAdmissionState.Absent) { await readUses.BeginAsync("occurrence-read-use",id,input,token).ConfigureAwait(false);return profile; }
            var completed=await native.CompleteAsync(profile.Registration,"holder-completion:"+Id(profile.KeyUse),profile.KeyUse,token).ConfigureAwait(false);
            completed.Key?.Dispose();if(!completed.HoldersCompleted) return profile;
            await readUses.RetainAsync("occurrence-read-use",id,new JsonObject { ["completedUseSha256"]=Id(profile.KeyUse) },token).ConfigureAwait(false);
        }
        return null;
    }
    /// <inheritdoc />
    public async Task<bool> AdmitReleaseAsync(InteractionOccurrenceIdentity identity,CustodyInteractionMaterialProfile profile,CancellationToken token)
    {
        if(await Claim(identity,token).ConfigureAwait(false) is not { } claim || claim.Binding.Identity!=identity || profile!=claim.Write && !claim.Reads.Contains(profile)) return false;
        return await readUses.BeginAsync("occurrence-read-use",recipe.RecipeId+":"+claim.SlotId+":release:"+Id(profile.KeyUse),Node(new { identity,profile }),token).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task<IReadOnlyList<CustodyInteractionMaterialProfile>> PendingAsync(CancellationToken token)
    {
        var result=new List<CustodyInteractionMaterialProfile>();if(!Valid()) return result;
        foreach(var slot in recipe.Slots)
        {
            if(await claims.LookupResultAsync("fr34-recipes",recipe.RecipeId+":"+slot.SlotId,token).ConfigureAwait(false) is not { } retained) continue;
            var claim=retained.Deserialize<Fr34OccurrenceClaim>(CustodyBindingContract.Json);if(claim is null) return [];
            foreach(var profile in claim.Reads.Prepend(claim.Write))
                if(await readUses.ReadAdmissionAsync("occurrence-read-use",recipe.RecipeId+":"+slot.SlotId+":release:"+Id(profile.KeyUse),Node(new { identity=claim.Binding.Identity,profile }),token).ConfigureAwait(false)==QualificationOriginalAdmissionState.Admitted) result.Add(profile);
        }
        return result;
    }
}
