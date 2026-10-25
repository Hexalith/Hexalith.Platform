using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.PayloadProtection;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Protection;

/// <summary>Platform-owned occurrence provider with value-free current/lease checks and a single ownership transfer for native root bytes. It avoids the concrete candidate-delivery adapter's unowned copies; actual native holder admission remains mandatory.</summary>
public sealed class CustodyInteractionOccurrenceKeyProvider(DaprClient dapr,ICustodyOccurrenceMaterialClient custody,CustodyInteractionProtectionBindingSource bindings,
    IInteractionOccurrenceRegistry registry,IPlatformKeyInventoryActor inventory,IPlatformKeyInventoryAuthority inventoryAuthority,
    IReadOnlyDictionary<string,CustodyInteractionMaterialProfile> enrolledProfiles,IFr34OccurrenceEnrollment? dynamicEnrollment=null):IInteractionOccurrenceKeyProvider
{
    private readonly HashSet<string> _leases=new(StringComparer.Ordinal),_materialAttempts=new(StringComparer.Ordinal);private readonly object _gate=new();
    private readonly Dictionary<string,byte[]> _ownedRoots=new(StringComparer.Ordinal);
    private readonly HashSet<string> _completedUses=new(StringComparer.Ordinal);
    /// <summary>Gets the exact independently enrolled material profile identity.</summary>
    public static string ProfileId(InteractionOccurrenceIdentity identity,bool writing)=>CustodyInteractionProtectionBindingSource.ResourceId(identity.Owner,new(identity.Sequence,identity.Kind,identity.PayloadTypeId))+"|"+(writing?"write":"read");
    private async Task<(InteractionProtectionBinding Binding,CustodyInteractionMaterialProfile Profile)?> Current(InteractionOccurrenceIdentity identity,bool writing,CancellationToken token,bool allocate=false)
    {
        enrolledProfiles.TryGetValue(ProfileId(identity,writing),out var profile);
        if(allocate && !writing && dynamicEnrollment is not null) profile=await dynamicEnrollment.MaterialAsync(identity,false,token).ConfigureAwait(false);
        else if(profile is null && dynamicEnrollment is not null)
        {
            if(allocate) profile=await dynamicEnrollment.MaterialAsync(identity,writing,token).ConfigureAwait(false);
            else profile=(await dynamicEnrollment.ResolveAsync(identity.Owner,new(identity.Sequence,identity.Kind,identity.PayloadTypeId),token).ConfigureAwait(false))?.Write;
        }
        if(profile is null || profile.Registration.Identity.TenantId!=identity.Target.TenantId
            || profile.Registration.Identity.ObjectId!=identity.Target.AgentInteractionId || profile.Registration.Identity.KeyAlias!=identity.Target.TargetProtectionKeyAlias
            || profile.Registration.Identity.KeyVersion!=identity.RootKeyVersion || profile.Registration.Identity.Purpose!=PlatformKeyPurpose.InteractionRootDek
            || profile.KeyUse.OriginalRootReference!=profile.Registration.OpaqueWrappedKeyReference) return null;
        var binding=await bindings.FindRetainedAsync(identity.Owner,new(identity.Sequence,identity.Kind,identity.PayloadTypeId),token).ConfigureAwait(false);
        if(binding?.Identity!=identity || await bindings.ReadErasureAsync(binding,token).ConfigureAwait(false) is not null) return null;
        var wrapped=await custody.LookupAsync(profile.Registration,token).ConfigureAwait(false);
        return wrapped.Status==CustodyKeyLifecycleStatus.Wrapped && wrapped.ReceiptId==profile.Registration.WrapReceiptId?(binding,profile):null;
    }
    /// <inheritdoc />
    public async Task<bool> AcquireEncryptionLeaseAsync(InteractionOccurrenceRecord record,CancellationToken token)
    {
        if(await Current(record.Request.Identity,true,token).ConfigureAwait(false) is not { } current || current.Binding.KeyReference!=record.KeyReference) return false;
        if(!await bindings.TryAcquireEncryptionLeaseAsync(record,registry,token).ConfigureAwait(false)) return false;
        lock(_gate) return _leases.Add(ProfileId(record.Request.Identity,true)+"|"+record.KeyReference);
    }
    /// <inheritdoc />
    public async Task<InteractionOccurrenceOwnedKey?> ResolveRootAsync(InteractionOccurrenceIdentity identity,string keyReference,bool writing,CancellationToken token)
    {
        if(writing) { lock(_gate) if(!_leases.Contains(ProfileId(identity,true)+"|"+keyReference)) return null; }
        if(await Current(identity,writing,token,true).ConfigureAwait(false) is not { } current || current.Binding.KeyReference!=keyReference) return null;
        string materialAttempt=MaterialId(current.Profile.KeyUse);
        if(dynamicEnrollment is not null && (!writing || !enrolledProfiles.ContainsKey(ProfileId(identity,writing))) && !await dynamicEnrollment.AdmitReleaseAsync(identity,current.Profile,token).ConfigureAwait(false)) return null;
        lock(_gate) if(!_materialAttempts.Add(materialAttempt)) return null;
        var released=await custody.UnwrapAsync(current.Profile.Registration,current.Binding.OperationId+"|"+(writing?"write":"read")+(dynamicEnrollment is not null && (!writing || !enrolledProfiles.ContainsKey(ProfileId(identity,writing)))?"|"+materialAttempt:""),current.Profile.KeyUse,token).ConfigureAwait(false);
        if(released.Key is not { } key) return null;
        using(key)
        {
            if(key.Bytes.Length!=32 || await bindings.FindRetainedAsync(identity.Owner,new(identity.Sequence,identity.Kind,identity.PayloadTypeId),token).ConfigureAwait(false)!=current.Binding
                || await custody.LookupAsync(current.Profile.Registration,token).ConfigureAwait(false) is not { Status:CustodyKeyLifecycleStatus.Wrapped }) return null;
            byte[] transferred=key.TransferOwnership();lock(_gate) _ownedRoots[materialAttempt]=transferred;
            return new(identity.Target,"interaction-root",identity.RootKeyVersion,transferred);
        }
    }
    /// <summary>Recovers exact native holder completion, including after process restart. Buffer disposal merely permits asking the independently witnessed native boundary; it never supplies completion evidence.</summary>
    public async Task<int> CompleteDisposedUsesAsync(CancellationToken token)
    {
        int completed=0;
        var pending=dynamicEnrollment is null?[]:await dynamicEnrollment.PendingAsync(token).ConfigureAwait(false);
        foreach(var profile in enrolledProfiles.Values.Concat(pending).DistinctBy(value=>MaterialId(value.KeyUse)))
        {
            string id=MaterialId(profile.KeyUse);
            lock(_gate)
            {
                if(_completedUses.Contains(id) || _ownedRoots.TryGetValue(id,out var bytes) && bytes.AsSpan().IndexOfAnyExcept((byte)0)>=0) continue;
            }
            var result=await custody.CompleteAsync(profile.Registration,"holder-completion:"+id,profile.KeyUse,token).ConfigureAwait(false);
            if(result.Key is { } unexpected) { unexpected.Dispose();continue; }
            if(result.HoldersCompleted)
            {
                lock(_gate) { _completedUses.Add(id);_ownedRoots.Remove(id); }completed++;
            }
        }
        return completed;
    }
    private static string MaterialId(CustodyKeyUse use)=>CustodyMaterialUseIdentity.Digest(use);
    /// <inheritdoc />
    public async Task<bool> IsCurrentAsync(InteractionOccurrenceIdentity identity,string operation,CancellationToken token)
    {
        if(operation is not ("Read" or "Write")) return false;
        if(operation=="Write") { lock(_gate) if(!_leases.Any(value=>value.StartsWith(ProfileId(identity,true)+"|",StringComparison.Ordinal))) return false; }
        return await Current(identity,operation=="Write",token).ConfigureAwait(false) is not null;
    }
    /// <inheritdoc />
    public async Task<InteractionOccurrenceOwnedKey?> ResolveDigestAsync(InteractionOccurrenceIdentity identity,string version,CancellationToken token)
    {
        var current=await Current(identity,true,token).ConfigureAwait(false)??await Current(identity,false,token).ConfigureAwait(false);if(current is not { } exact) return null;
        var reference=exact.Profile.DigestReference;var key=reference.InventoryKey;
        if(key.TenantId!=identity.Target.TenantId || key.Purpose!=PlatformKeyPurpose.DigestKey || key.Version!=version || exact.Binding.DigestKeyVersion!=version) return null;
        async Task<bool> Authorized()
        {
            if(!await inventoryAuthority.AuthorizeOperationAsync(key,"ResolveInteractionOccurrenceDigest",token).ConfigureAwait(false)) return false;
            var state=await inventory.ReadAsync(key).WaitAsync(token).ConfigureAwait(false);
            return state.Versions.Count(value=>value.Key==key && value.State!=PlatformHmacKeyState.Revoked)==1
                && await inventoryAuthority.ValidateStateAsync(key,state.Revision,Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state))),token).ConfigureAwait(false);
        }
        if(!await Authorized().ConfigureAwait(false)) return null;
        Dictionary<string,string>? response=null;byte[]? material=null;bool transferred=false;
        var pending=dapr.GetSecretAsync(reference.ComponentName,reference.Key, new Dictionary<string,string>{{"version_id",reference.NativeVersion}},token);
        try
        {
            response=await pending.WaitAsync(token).ConfigureAwait(false);if(response.Count!=1 || !response.TryGetValue(reference.Field,out var value)) return null;
            material=Convert.FromBase64String(value);if(material.Length!=32 || Convert.ToBase64String(material)!=value || !await Authorized().ConfigureAwait(false)) return null;
            token.ThrowIfCancellationRequested();transferred=true;return new(identity.Target,"tenant-digest",version,material);
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested) { _=ClearLate(pending);throw; }
        catch(Exception) { token.ThrowIfCancellationRequested();return null; }
        finally { response?.Clear();if(!transferred && material is not null) CryptographicOperations.ZeroMemory(material); }
    }
    private static async Task ClearLate(Task<Dictionary<string,string>> pending) { try { (await pending.ConfigureAwait(false)).Clear(); } catch(Exception) { } }
}
