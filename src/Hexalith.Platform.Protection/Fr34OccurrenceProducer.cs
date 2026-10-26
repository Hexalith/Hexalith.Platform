using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Actual receiving producer over independently enrolled finite native inputs and indexed immutable originals. It generates no key, native admission, closure witness or genesis.</summary>
public sealed class Fr34OccurrenceProducer(Fr34OccurrenceRecipe recipe,IQualificationOriginalStore claims,IQualificationOriginalStore readUses,ICustodyOccurrenceMaterialClient native,
    CustodyLifecycleClient? custody=null,ICustodyReservedManifestSource? manifests=null):IFr34OccurrenceEnrollment
{
    private static JsonObject Node<T>(T value)=>(JsonObject)JsonSerializer.SerializeToNode(value,CustodyBindingContract.Json)!;
    private static string Id<T>(T value)=>value is null?"null":QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(value,CustodyBindingContract.Json)!);
    private bool Valid()=>SecretsProvisioning.IsReference(recipe.RecipeId) && recipe.Slots.Count is >0 and <=128 && recipe.LifecycleEpoch>0
        && recipe.Slots.Select(value=>value.SlotId).Distinct(StringComparer.Ordinal).Count()==recipe.Slots.Count
        && recipe.Slots.Select(value=>value.Write.Registration.OpaqueWrappedKeyReference).Distinct(StringComparer.Ordinal).Count()==recipe.Slots.Count
        && (recipe.PayloadTypeId==typeof(Fr34CanaryPersisted).FullName || recipe.ExactOccurrences is { Count:>0 and <=128 }) && recipe.DigestVersion.Length>0 && recipe.ProtectionProfileVersion=="candidate-hkdf-sha256-v1"
        && recipe.Slots.All(slot=>SecretsProvisioning.IsReference(slot.SlotId) && (slot.KeyReference is { Length:26 } && slot.KeyReference[0]<='7' && slot.KeyReference.All(value=>"0123456789ABCDEFGHJKMNPQRSTVWXYZ".Contains(value,StringComparison.Ordinal))) && slot.Reads.Count is >0 and <=32 && slot.Write.Registration.Identity.TenantId==recipe.Target.TenantId
            && slot.Write.Registration.Identity.Purpose==PlatformKeyPurpose.InteractionRootDek && slot.Write.Registration.Identity.KeyVersion.Length>0
            && slot.Reads.All(read=>read.Registration==slot.Write.Registration && read.DigestReference==slot.Write.DigestReference)
            && slot.Reads.Append(slot.Write).All(profile=>profile.KeyUse.OriginalRootReference==profile.Registration.OpaqueWrappedKeyReference)
            && slot.Reads.Append(slot.Write).Select(value=>Id(value.KeyUse)).Distinct(StringComparer.Ordinal).Count()==slot.Reads.Count+1);
    /// <inheritdoc />
    public async Task<Fr34OccurrenceClaim?> ResolveAsync(AggregateIdentity owner,PayloadProtectionOccurrenceContext occurrence,CancellationToken token)
    {
        if(!Valid() || !ValidOwner(owner,occurrence)) return null;string canary=owner.AggregateId;
        var request=Node(new { recipe.RecipeId,owner,occurrence });
        var exact=ExactBinding(owner,occurrence);
        foreach(var slot in recipe.Slots)
        {
            if(exact is not null && !MatchesSlot(exact,slot)) continue;
            string id=recipe.RecipeId+":"+slot.SlotId;
            var retained=await claims.LookupResultAsync("fr34-recipes",id,token).ConfigureAwait(false);
            if(retained is not null)
            {
                var old=retained.Deserialize<Fr34OccurrenceClaim>(CustodyBindingContract.Json);
                if(old?.CanaryId==canary) return Exact(old,owner,occurrence)?old:null;
                continue;
            }
            var admittedRequest=await claims.LookupRequestAsync("fr34-recipes",id,token).ConfigureAwait(false);
            AggregateIdentity originalOwner=owner;PayloadProtectionOccurrenceContext originalOccurrence=occurrence;
            if(admittedRequest is not null)
            {
                if(admittedRequest.Count!=3 || admittedRequest["RecipeId"]?.GetValue<string>()!=recipe.RecipeId
                    || admittedRequest["owner"]?.Deserialize<AggregateIdentity>(CustodyBindingContract.Json) is not { } retainedOwner
                    || admittedRequest["occurrence"]?.Deserialize<PayloadProtectionOccurrenceContext>(CustodyBindingContract.Json) is not { } retainedOccurrence
                    || !ValidOwner(retainedOwner,retainedOccurrence)) return null;
                originalOwner=retainedOwner;originalOccurrence=retainedOccurrence;
            }
            else
            {
                var admission=await claims.ReadAdmissionAsync("fr34-recipes",id,request,token).ConfigureAwait(false);
                if(admission==QualificationOriginalAdmissionState.Unknown) return null;
                if(admission==QualificationOriginalAdmissionState.Absent)
                {
                    try { await claims.BeginAsync("fr34-recipes",id,request,token).ConfigureAwait(false); }
                    catch(InvalidOperationException) { return null; }
                }
                admittedRequest=request;
            }
            // Reconstruct only exact claim metadata after a lost Retain acknowledgement. No native registration or release is repeated.
            var key=slot.Write.Registration.Identity;
            var identity=new InteractionOccurrenceIdentity(new(key.TenantId,key.ObjectId,key.KeyAlias),originalOwner,originalOccurrence.PayloadKind,originalOccurrence.RecordSequence,originalOccurrence.PayloadTypeId,key.KeyVersion,recipe.ProtectionProfileVersion);
            var originalExact=ExactBinding(originalOwner,originalOccurrence);
            if(originalExact is not null && !MatchesSlot(originalExact,slot)) return null;
            var claim=new Fr34OccurrenceClaim(recipe.RecipeId,slot.SlotId,originalOwner.AggregateId,originalExact??new(identity,recipe.DigestVersion,1,slot.KeyReference,EventStorePrivateRecordingPort.StableUlid("fr34-occurrence",recipe.RecipeId,originalOwner.AggregateId),recipe.LifecycleEpoch),slot.Write,slot.Reads,slot.NativeManifest);
            if(await native.LookupAsync(slot.Write.Registration,token).ConfigureAwait(false) is not { Status:CustodyKeyLifecycleStatus.Wrapped } original || original.ReceiptId!=slot.Write.Registration.WrapReceiptId) return null;
            await claims.RetainAsync("fr34-recipes",id,Node(claim),token).ConfigureAwait(false);if(originalOwner==owner && originalOccurrence==occurrence) return claim;
        }
        return null;
    }
    private InteractionProtectionBinding? ExactBinding(AggregateIdentity owner,PayloadProtectionOccurrenceContext occurrence)=>recipe.ExactOccurrences?.GetValueOrDefault(CustodyInteractionProtectionBindingSource.ResourceId(owner,occurrence));
    private bool MatchesSlot(InteractionProtectionBinding binding,Fr34OccurrenceSlot slot)=>binding.Identity.Target==new ProtectionTarget(slot.Write.Registration.Identity.TenantId,slot.Write.Registration.Identity.ObjectId,slot.Write.Registration.Identity.KeyAlias)
        && binding.Identity.RootKeyVersion==slot.Write.Registration.Identity.KeyVersion && binding.Identity.DerivationProfileVersion==recipe.ProtectionProfileVersion
        && binding.KeyReference==slot.KeyReference && binding.DigestKeyVersion==recipe.DigestVersion && binding.LifecycleEpoch==recipe.LifecycleEpoch;
    private bool ValidOwner(AggregateIdentity owner,PayloadProtectionOccurrenceContext occurrence)=>owner.TenantId==recipe.Target.TenantId
        && (ExactBinding(owner,occurrence) is { } exact?exact.Identity.Owner==owner && exact.Identity.Sequence==occurrence.RecordSequence && exact.Identity.Kind==occurrence.PayloadKind
            && exact.Identity.PayloadTypeId==occurrence.PayloadTypeId && recipe.Slots.Any(slot=>MatchesSlot(exact,slot)):
            owner.Domain=="fr34-canary" && owner.AggregateId is { Length:64 } canary && canary.All(char.IsAsciiHexDigit) && occurrence.RecordSequence==1
                && occurrence.PayloadKind==PayloadProtectionPayloadKind.Event && occurrence.PayloadTypeId==recipe.PayloadTypeId);
    private bool Exact(Fr34OccurrenceClaim claim,AggregateIdentity owner,PayloadProtectionOccurrenceContext occurrence)
    {
        var slot=recipe.Slots.SingleOrDefault(value=>value.SlotId==claim.SlotId);var identity=claim.Binding.Identity;
        if(slot is null || Id(claim.NativeManifest)!=Id(slot.NativeManifest)) return false;
        if(ExactBinding(owner,occurrence) is { } exact) return claim.RecipeId==recipe.RecipeId && claim.Binding==exact && MatchesSlot(exact,slot)
            && claim.Write==slot.Write && JsonSerializer.Serialize(claim.Reads)==JsonSerializer.Serialize(slot.Reads);
        return claim.RecipeId==recipe.RecipeId && slot is not null && claim.Write==slot.Write && JsonSerializer.Serialize(claim.Reads)==JsonSerializer.Serialize(slot.Reads)
            && identity.Owner==owner && identity.Sequence==occurrence.RecordSequence && identity.Kind==occurrence.PayloadKind && identity.PayloadTypeId==occurrence.PayloadTypeId
            && identity.Target==new ProtectionTarget(slot.Write.Registration.Identity.TenantId,slot.Write.Registration.Identity.ObjectId,slot.Write.Registration.Identity.KeyAlias)
            && identity.RootKeyVersion==slot.Write.Registration.Identity.KeyVersion && identity.DerivationProfileVersion==recipe.ProtectionProfileVersion
            && claim.Binding.KeyReference==slot.KeyReference && claim.Binding.DigestKeyVersion==recipe.DigestVersion && claim.Binding.LifecycleEpoch==recipe.LifecycleEpoch
            && claim.Binding.ReservationAttemptOrdinal==1 && claim.Binding.OperationId==EventStorePrivateRecordingPort.StableUlid("fr34-occurrence",recipe.RecipeId,owner.AggregateId);
    }
    private Task<Fr34OccurrenceClaim?> Claim(InteractionOccurrenceIdentity identity,CancellationToken token)=>ResolveAsync(identity.Owner,new(identity.Sequence,identity.Kind,identity.PayloadTypeId),token);
    /// <inheritdoc />
    public async Task<bool> AcquireLeaseAsync(InteractionProtectionBinding binding,bool completion,CancellationToken token)
    {
        if(await Claim(binding.Identity,token).ConfigureAwait(false) is not { } claim || claim.Binding!=binding) return false;
        return await claims.BeginFinalAsync("fr34-recipes",recipe.RecipeId+":"+claim.SlotId+(completion?":completion":":encryption"),Node(binding),token).ConfigureAwait(false);
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
            var completed=await native.CompleteAsync(profile.Registration,"holder-completion:"+CustodyMaterialUseIdentity.Digest(profile.KeyUse),profile.KeyUse,token).ConfigureAwait(false);
            completed.Key?.Dispose();if(!completed.HoldersCompleted) return profile;
            await readUses.RetainAsync("occurrence-read-use",id,new JsonObject { ["completedUseSha256"]=Id(profile.KeyUse) },token).ConfigureAwait(false);
        }
        return null;
    }
    /// <inheritdoc />
    public async Task<bool> AdmitReleaseAsync(InteractionOccurrenceIdentity identity,CustodyInteractionMaterialProfile profile,CancellationToken token)
    {
        if(await Claim(identity,token).ConfigureAwait(false) is not { } claim || claim.Binding.Identity!=identity || profile!=claim.Write && !claim.Reads.Contains(profile)) return false;
        return await readUses.BeginFinalAsync("occurrence-read-use",recipe.RecipeId+":"+claim.SlotId+":release:"+Id(profile.KeyUse),Node(new { identity,profile }),token).ConfigureAwait(false);
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
    /// <summary>Read-only command claim verification. It can neither allocate a claim nor accept an arbitrary caller target.</summary>
    public async Task<bool> VerifyCommandAsync(Hexalith.EventStore.Contracts.Commands.SubmitCommandRequest command,CancellationToken token)
    {
        if(!Valid() || command.Domain!="fr34-canary" || command.Tenant!=recipe.Target.TenantId || command.CommandType!=typeof(PersistFr34Canary).FullName
            || command.AggregateId is not { Length:64 } || !command.AggregateId.All(char.IsAsciiHexDigit)
            || command.MessageId!=EventStorePrivateRecordingPort.StableUlid("fr34",command.Tenant,command.AggregateId) || command.IdempotencyKey!=command.MessageId || command.Extensions is { Count:>0 }) return false;
        var owner=new AggregateIdentity(command.Tenant,command.Domain,command.AggregateId);var occurrence=new PayloadProtectionOccurrenceContext(1,PayloadProtectionPayloadKind.Event,typeof(Fr34CanaryPersisted).FullName!);
        var payload=command.Payload.Deserialize<PersistFr34Canary>(new JsonSerializerOptions(JsonSerializerDefaults.Web));if(payload is null) return false;
        foreach(var slot in recipe.Slots)
        {
            var retained=await claims.LookupResultAsync("fr34-recipes",recipe.RecipeId+":"+slot.SlotId,token).ConfigureAwait(false);
            var claim=retained?.Deserialize<Fr34OccurrenceClaim>(CustodyBindingContract.Json);
            if(claim is not null && claim.CanaryId==command.AggregateId && Exact(claim,owner,occurrence)) return claim.Binding.Identity.Target==payload.ProtectedContent.Target;
        }
        return false;
    }
    /// <inheritdoc />
    public async Task<InteractionProtectionErasureWitness?> ReadErasureAsync(InteractionProtectionBinding binding,CancellationToken token)
    {
        if(custody is null || manifests is null || await Claim(binding.Identity,token).ConfigureAwait(false) is not { } claim || claim.Binding!=binding
            || claim.NativeManifest is not { } expected || expected.Original.Targets.Count!=1 || expected.Original.Targets[0]!=binding.Identity.Target
            || await manifests.FindAsync(expected.ReservationReceiptId,token).ConfigureAwait(false) is not { } found || Id(found)!=Id(expected.Original)) return null;
        // This custody read verifies every original page, its signature and independently current full-vector/restore proof. Status-only responses carry no barrier and cannot qualify.
        var original=await custody.ExecuteManifestOriginalAsync(found,expected.ReservationReceiptId,true,token).ConfigureAwait(false);
        var result=original.Result;
        if(original.RestoreBarrierReceiptId is null || result.State!=DeletionManifestProviderState.Consumed || result.ReservationReceiptId!=expected.ReservationReceiptId
            || result.TenantId!=binding.Identity.Target.TenantId || result.BatchId!=found.Capability.BatchId || result.TargetReceipts.Count!=1
            || result.TargetReceipts[0].Target!=binding.Identity.Target || result.TargetReceipts[0].OriginalBatchId!=result.BatchId) return null;
        string[] targets=[InteractionProtectionBindingProvider.TargetId(binding.Identity.Target)],receipts=[result.TargetReceipts[0].ReceiptId];
        return new(InteractionProtectionBindingProvider.ManifestDigest(binding),InteractionProtectionBindingProvider.ReceiptOrderDigest(targets,receipts),targets,receipts,true,false,null,false,null);
    }
}
