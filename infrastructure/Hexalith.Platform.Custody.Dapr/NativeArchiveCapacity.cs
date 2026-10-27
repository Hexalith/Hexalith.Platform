using System.Runtime.InteropServices;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Custody.Dapr;

// Independently placed mutable capacity authority, fenced across all writers/processes. Startup never creates its genesis.

internal sealed class NativeArchiveCapacity(ComponentProfile profile,NativeInstallation installation)
{
    private const long Overhead=65536;
    internal const int MaximumLedgerBytes=32*1024*1024;
    internal const long MetadataReserveBytes=2L*MaximumLedgerBytes+Overhead;
    private const int MaximumPublications=131072,CompletionPublicationSlots=8;
    private NativeArchiveCapacityInstallation Config=>installation.ArchiveCapacity is { Protocol:"LinuxExclusiveLocalArchiveCapacityV1" } configured
        && Path.IsPathFullyQualified(configured.Directory) && configured.QuotaBytes>0 && configured.CompletionBytes>=10L*CustodyBindingContract.MaximumOriginalBytes
        && configured.VolumeFenceIdentity is { Length:>0 } && configured.MaximumPending is >0 and <=30000 ?configured:throw new AuthorityUnavailableException("External.EnrolledAtomicNativeArchivePhysicalCapacityAndFencingProtocol");
    private string Ledger=>Path.Combine(Config.Directory,"capacity.json");
    private readonly AsyncLocal<HeldArchiveDirectory?> _held=new();
    private readonly AsyncLocal<ProtectedArchiveStore?> _protected=new();
    private readonly AsyncLocal<CancellationToken> _budget=new();
    internal Task<T> WithFenceAsync<T>(Func<NativeArchiveCapacityState,Task<T>> action,CancellationToken token)
        =>ArchiveVolumeFence.RunAsync(Config.Directory,ct=>WithFenceCoreAsync(action,ct),token,Config.VolumeFenceIdentity);
    private async Task<T> WithFenceCoreAsync<T>(Func<NativeArchiveCapacityState,Task<T>> action,CancellationToken token)
    {
        _budget.Value=token;
        using var held=HeldArchiveDirectory.Acquire(Config.Directory);_held.Value=held;
        try {
        var config=Config;var directory=new DirectoryInfo(config.Directory);
        if(!directory.Exists || directory.LinkTarget is not null || CustodyJson.Hash(await BoundedRegularFile.ReadAsync(Path.Combine(config.Directory,"enrollment.json"),65536,token))!=installation.ArchiveEnrollmentSha256)
            throw new AuthorityUnavailableException("Independent native capacity enrollment unavailable.");
        string mount=ArchiveFileLease.FilesystemIdentity(config.Directory);
        if(ArchiveFileLease.FilesystemIdentity(installation.CheckpointArchiveDirectory)!=mount || ArchiveFileLease.FilesystemIdentity(installation.OriginalArchiveDirectory)!=mount)
            throw new AuthorityUnavailableException("Native physical completion reserves require the exact same enrolled local filesystem.");
        using var fence=await (config.ProtectedAllocations is null?ArchiveFileLease.AcquireAtAsync(held,"capacity.lock",token):ArchiveFileLease.AcquireExistingAtAsync(held,"capacity.lock",token)).ConfigureAwait(false);held.VerifyLocator();
        if(config.ProtectedAllocations is { } declared && declared.MaximumMetadataBytes<MaximumLedgerBytes) throw new IOException("Complete preallocated capacity metadata bound required.");
            using var allocations=config.ProtectedAllocations is { } protectedConfig?new ProtectedArchiveStore(protectedConfig):null;_protected.Value=allocations;
        var protectedRaw=allocations?.ReadMetadata();var raw=protectedRaw??await BoundedRegularFile.ReadAsync(Ledger,32*1024*1024,token).ConfigureAwait(false);
        var state=CustodyJson.Read<NativeArchiveCapacityState>(raw);
        if(state.EnrollmentDigest!=profile.EnrollmentDigest || state.ArchiveEnrollmentSha256!=installation.ArchiveEnrollmentSha256 || state.RetainedBytes<0
            || state.Pending.Count>config.MaximumPending || state.Publications.Count>MaximumPublications || state.Pending.Values.Any(value=>value.RemainingBytes<0))
            throw new AuthorityUnavailableException("Native capacity authority requires bounded independent legacy reconciliation.");
        if(allocations is not null && protectedRaw is null) allocations.WriteMetadata(raw);
        // Existing named rewrite candidates remain unproven history. Only a newly created owner may clean up its inode.
        Allocate("metadata",MetadataReserveBytes-held.Read("capacity.json",MaximumLedgerBytes)!.Length);
        // Recover interrupted transfers conservatively from the surviving ledger. Never infer completion from local file disposal.
        foreach(var pair in state.Pending) Allocate(pair.Key,pair.Value.RemainingBytes);
        T result=await action(state).ConfigureAwait(false);held.VerifyLocator();token.ThrowIfCancellationRequested();return result;
        } finally { _held.Value=null;_protected.Value=null;_budget.Value=default; }
    }
    internal async Task EnsurePendingAsync(NativeArchiveCapacityState state,Checkpoint checkpoint,CancellationToken token)
    {
        token=_budget.Value;token.ThrowIfCancellationRequested();
        var operations=checkpoint.NativeIntents.Values.Where(value=>value.Phase!=CustodyBindingIntentPhase.Closed).Select(value=>(Id:"intent:"+value.IntentId,Operation:value.OperationId))
            .Concat(checkpoint.Originals.Where(pair=>pair.Value.Response is null).Select(pair=>(Id:"original:"+pair.Key,Operation:pair.Key)));
        foreach(var operation in operations)
        {
            string key=Key(checkpoint.TenantId,operation.Id);if(state.Pending.ContainsKey(key)) continue;
            if(state.Pending.Count>=Config.MaximumPending || state.Publications.Count+CompletionPublicationSlots*(state.Pending.Count+1)>MaximumPublications) throw new AuthorityUnavailableException("Native pending completion capacity exhausted.");
            if(_protected.Value is null) throw new AuthorityUnavailableException("External.EnrolledProtectedArchiveAllocationV1: native completion requires existing inode/entry/private-written resources.");
            Require(state,Config.CompletionBytes);
            state.Pending.Add(key,new(checkpoint.TenantId,operation.Operation,Config.CompletionBytes));await SaveAsync(state,token,true).ConfigureAwait(false);Allocate(key,Config.CompletionBytes);
        }
    }
    internal async Task SettleAsync(NativeArchiveCapacityState state,Checkpoint checkpoint,CancellationToken token)
    {
        token=_budget.Value;token.ThrowIfCancellationRequested();
        var active=checkpoint.NativeIntents.Values.Where(value=>value.Phase!=CustodyBindingIntentPhase.Closed).Select(value=>Key(checkpoint.TenantId,"intent:"+value.IntentId))
            .Concat(checkpoint.Originals.Where(pair=>pair.Value.Response is null).Select(pair=>Key(checkpoint.TenantId,"original:"+pair.Key))).ToHashSet(StringComparer.Ordinal);
        foreach(var pair in state.Pending.Where(pair=>pair.Value.Tenant==checkpoint.TenantId && !active.Contains(pair.Key)).ToArray())
        {
            // Only an exact committed head may settle. Keep a zero ledger row until its physical transfer is durable.
            state.Pending[pair.Key]=pair.Value with { RemainingBytes=0 };await SaveAsync(state,token).ConfigureAwait(false);Allocate(pair.Key,0);
            state.Pending.Remove(pair.Key);await SaveAsync(state,token).ConfigureAwait(false);
        }
    }
    internal async Task PublishAsync(NativeArchiveCapacityState state,string path,string key,ReadOnlyMemory<byte> bytes,string? completionKey,CancellationToken token)
    {
        token=_budget.Value;token.ThrowIfCancellationRequested();
        if(ProtectedArchiveStore.Exists(path))
        {
            if(!(await BoundedRegularFile.ReadAsync(path,bytes.Length,token).ConfigureAwait(false)).AsSpan().SequenceEqual(bytes.Span)) throw new AuthorityUnavailableException("Native immutable publication conflict.");
            if(state.Publications.Remove(key)) await SaveAsync(state,token).ConfigureAwait(false);return;
        }
        if(key.Length>128) throw new AuthorityUnavailableException("Bounded exact native publication locator required.");
        long charge=checked(bytes.Length+Overhead);
        if(state.Publications.TryGetValue(key,out var retained))
        { if(retained.Bytes!=charge || retained.CompletionKey!=completionKey) throw new AuthorityUnavailableException("Native publication reservation conflict."); }
        else
        {
            if(completionKey is not null)
            {
                if(!state.Pending.TryGetValue(completionKey,out var reserved) || reserved.RemainingBytes<charge) throw new AuthorityUnavailableException("Exact native completion reserve unavailable.");
                state.Pending[completionKey]=reserved with { RemainingBytes=reserved.RemainingBytes-charge };
            }
            else Require(state,charge);
            if(state.Publications.Count>=MaximumPublications || completionKey is null && state.Publications.Count+CompletionPublicationSlots*state.Pending.Count+1>MaximumPublications) throw new AuthorityUnavailableException("Native in-flight publication capacity exhausted.");
            state.Publications.Add(key,new(charge,completionKey));state=state with { RetainedBytes=checked(state.RetainedBytes+charge) };
            await SaveAsync(state,token,completionKey is null).ConfigureAwait(false);
        }
        if(completionKey is not null) Allocate(completionKey,state.Pending[completionKey].RemainingBytes);
        // The global physical fence covers the transfer from allocated completion blocks through atomic exact publication.
        // Failed head comparisons retain the same content locator and permanently charged body.
        BeforePublication?.Invoke(path);
        if(_protected.Value is { } fixedStore)
        {
            string resource=Config.Directory+"|"+(completionKey??"ordinary:"+key);
            if(completionKey is null) fixedStore.Reserve(resource,charge);
            fixedStore.Publish(resource,path,bytes.Span);
        }
        else if(completionKey is not null) throw new AuthorityUnavailableException("External.EnrolledProtectedArchiveAllocationV1: legacy completion resources require exact independent reconciliation.");
        else await ImmutableFileArchive.PublishAsync(path,bytes,token,()=>
        { if(AvailableBytes()<charge) throw new AuthorityUnavailableException("Native archive physical publication capacity unavailable."); }).ConfigureAwait(false);
        state.Publications.Remove(key);await SaveAsync(state,token).ConfigureAwait(false);
    }
    internal static string Key(string tenant,string operation)=>CustodyJson.Hash(PlatformCanonicalBytes.Components([tenant,operation]));
    private void Require(NativeArchiveCapacityState state,long extra)
    {
        long pending=state.Pending.Values.Sum(value=>value.RemainingBytes);
        if(checked(state.RetainedBytes+pending+extra+MetadataReserveBytes)>Config.QuotaBytes || AvailableBytes()<checked(extra+Overhead))
            throw new AuthorityUnavailableException("Native physical archive admission/completion reserve exhausted.");
    }
    internal Func<long>? AvailableBytesOverride { get; init; }
    internal Action<string>? FaultPoint { get; init; }
    internal Action<string>? BeforePublication { get; init; }
    internal Func<int,long,int>? AllocationResultOverride { get; init; }
    private long AvailableBytes()=>AvailableBytesOverride?.Invoke()??new DriveInfo(Config.Directory).AvailableFreeSpace;
    private void Allocate(string key,long bytes)
    {
        if(_protected.Value is { } fixedStore)
        { if(key!="metadata") { if(bytes==0) fixedStore.ReleaseUnused(Config.Directory+"|"+key);else fixedStore.Reserve(Config.Directory+"|"+key,bytes); }return; }
        string path=Path.Combine(Config.Directory,"reserve-"+key);
        _budget.Value.ThrowIfCancellationRequested();var held=_held.Value??throw new AuthorityUnavailableException("Held capacity directory required.");held.VerifyLocator();
        using var handle=held.OpenFile(Path.GetFileName(path),0x40|2);using var file=new FileStream(handle,FileAccess.ReadWrite);
        ArchiveAllocationProof.Identity(file.SafeFileHandle);if(file.Length>0) ArchiveAllocationProof.RequirePrivateWritten(file.SafeFileHandle,file.Length);
        if(bytes>0 && (AllocationResultOverride?.Invoke(file.SafeFileHandle.DangerousGetHandle().ToInt32(),bytes)??Fallocate(file.SafeFileHandle.DangerousGetHandle().ToInt32(),0,bytes))!=0) throw new AuthorityUnavailableException("External.NativeArchiveAtomicPhysicalAllocationProtocol");
        file.SetLength(bytes);file.Flush(true);held.Flush();held.VerifyLocator();_budget.Value.ThrowIfCancellationRequested();
    }
    private async Task SaveAsync(NativeArchiveCapacityState state,CancellationToken token,bool admission=false)
    {
        byte[] bytes=CustodyJson.Bytes(state);if(bytes.Length+(admission?checked(CompletionPublicationSlots*512L*state.Pending.Count+1024*1024):0)>32L*1024*1024) throw new AuthorityUnavailableException("Native capacity authority bound exhausted.");
        token=_budget.Value;token.ThrowIfCancellationRequested();var held=_held.Value??throw new AuthorityUnavailableException("Held capacity directory required.");held.VerifyLocator();
        if(_protected.Value is { } fixedStore) { FaultPoint?.Invoke("metadata-reserve-transferred");fixedStore.WriteMetadata(bytes);FaultPoint?.Invoke("metadata-ledger-replaced");return; }
        string temporary="capacity.json.pending";
        long previous=held.Read("capacity.json",MaximumLedgerBytes)!.Length;
        // The fixed physical budget covers both complete ledger versions, including all bounded growth.
        // Persisted reserve shrink precedes allocation; interruption is recovered under this same fence.
        Allocate("metadata",checked(MetadataReserveBytes-previous-bytes.Length-Overhead));
        FaultPoint?.Invoke("metadata-reserve-transferred");
        using var pending=held.CreateUnpublishedFile(temporary);
        var file=pending.Stream;
        {
            if(Fallocate(file.SafeFileHandle.DangerousGetHandle().ToInt32(),0,bytes.Length)!=0) throw new AuthorityUnavailableException("Native metadata rewrite physical allocation failed.");
            await file.WriteAsync(bytes,token).ConfigureAwait(false);await file.FlushAsync(token).ConfigureAwait(false);file.Flush(true);
        }
        token.ThrowIfCancellationRequested();held.VerifyLocator();pending.Publish("capacity.json",true);held.Flush();held.VerifyLocator();token.ThrowIfCancellationRequested();
        FaultPoint?.Invoke("metadata-ledger-replaced");
        Allocate("metadata",MetadataReserveBytes-bytes.Length);
    }
    [DllImport("libc",EntryPoint="open",SetLastError=true)] private static extern int Open(string path,int flags,int mode);
    [DllImport("libc",EntryPoint="posix_fallocate",SetLastError=true)] private static extern int Fallocate(int descriptor,long offset,long length);
}
