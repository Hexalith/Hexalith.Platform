using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.Platform.Secrets.Runtime;

// Source-owned surviving physical reservations. Configuration/genesis/enrollment are existing-only.
internal sealed class IndependentArchiveCapacity(IndependentArchiveCapacityInstallation configuration,string archiveDirectory,string owner)
{
    private readonly AsyncLocal<ProtectedArchiveStore?> _protected=new();
    private const int MaximumLedger=4*1024*1024;
    internal const long MetadataBytes=2L*MaximumLedger+65536;
    private const long Overhead=65536;
    internal Func<long>? AvailableBytesOverride { get; init; }
    internal Action<string,long>? AllocationOverride { get; init; }
    internal Action<string>? Fault { get; init; }
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal string Ticket(string role,string id)=>Hash(owner+"|"+role+"|"+id);
    private async Task<T> WithFence<T>(Func<HeldArchiveDirectory,Ledger,CancellationToken,Task<T>> action,CancellationToken token)
        =>await BoundedDurableFileOperation.RunAsync(async ct=>
        {
            if(configuration.Protocol!="LinuxSharedPhysicalArchiveCapacityV1" || configuration.QuotaBytes<=MetadataBytes || configuration.MaximumTickets is <=0 or >30000 || string.IsNullOrWhiteSpace(configuration.VolumeFenceIdentity))
                throw new IOException("External.EnrolledSharedIndependentArchivePhysicalCapacityV1");
            return await ArchiveVolumeFence.RunAsync(configuration.Directory,async volumeToken=>
            {
            ct=volumeToken;
            using var held=HeldArchiveDirectory.Acquire(configuration.Directory);
            if(ArchiveFileLease.FilesystemIdentity(configuration.Directory)!=ArchiveFileLease.FilesystemIdentity(archiveDirectory)
                || Convert.ToHexString(SHA256.HashData(held.Read("enrollment.json",65536)??throw new IOException("Existing physical pool enrollment required.")))!=configuration.EnrollmentSha256)
                throw new IOException("Existing same-volume physical capacity enrollment required.");
            using var fence=await (configuration.ProtectedAllocations is null?ArchiveFileLease.AcquireAtAsync(held,"pool.lock",ct):ArchiveFileLease.AcquireExistingAtAsync(held,"pool.lock",ct)).ConfigureAwait(false);
            if(configuration.ProtectedAllocations is { } declared && declared.MaximumMetadataBytes<MaximumLedger) throw new IOException("Complete preallocated capacity metadata bound required.");
            using var allocations=configuration.ProtectedAllocations is { } protectedConfig?new ProtectedArchiveStore(protectedConfig):null;_protected.Value=allocations;
            var protectedRaw=allocations?.ReadMetadata();
            var raw=protectedRaw??held.Read("capacity.json",MaximumLedger)??throw new IOException("Independently reconciled capacity genesis required.");
            CustodyBindingContract.RejectDuplicateProperties(raw);
            var ledger=JsonSerializer.Deserialize<Ledger>(raw,CustodyBindingContract.Json)??throw new IOException("Existing physical capacity ledger required.");
            if(ledger.EnrollmentSha256!=configuration.EnrollmentSha256 || ledger.RetainedBytes<0 || ledger.Tickets.Count>configuration.MaximumTickets
                || ledger.Tickets.Any(pair=>!CustodyBindingContract.Digest(pair.Key) || pair.Value.Remaining<0 || pair.Value.Completion<0)
                || ledger.Transfers.Count>4096 || ledger.Transfers.Any(value=>!CustodyBindingContract.Digest(value)) || ledger.HeadRevisions.Values.Any(value=>value<0)) throw new IOException("Physical capacity ledger requires independent reconciliation.");
            if(allocations is not null && protectedRaw is null) allocations.WriteMetadata(raw); // Exact existing genesis migration precedes any reserved chunk/effect.
            if(ledger.LegacyTransfer is { } transfer)
            {
                if(allocations is null || !ledger.Tickets.TryGetValue(transfer.Source,out var source) || !ledger.Tickets.TryGetValue(transfer.Target,out var target)
                    || source.Owner!=target.Owner || source.Kind!="legacy" || target.Kind!="original" || target.CommitmentSha256!=transfer.CommitmentSha256)
                    throw new IOException("Exact surviving funded legacy transfer reconciliation required.");
                allocations.TransferReserved(configuration.Directory+"|"+transfer.Source,configuration.Directory+"|"+transfer.Target,transfer.Chunks,()=>Fault?.Invoke("legacy-chunk-transferred"));
                Fault?.Invoke("legacy-transfer-durable");ledger.LegacyTransfer=null;await Save(held,ledger,ct).ConfigureAwait(false);Fault?.Invoke("legacy-transfer-completed");
            }
            Allocate(held,"metadata",MetadataBytes-raw.Length);
            foreach(var pair in ledger.Tickets) Allocate(held,pair.Key,pair.Value.Remaining);
            try { var result=await action(held,ledger,ct).ConfigureAwait(false);held.VerifyLocator();ct.ThrowIfCancellationRequested();return result; }
            finally { _protected.Value=null; }
            },ct,configuration.VolumeFenceIdentity).ConfigureAwait(false);
        },token).ConfigureAwait(false);
    internal Task EnsureLegacyAsync(long pending,long revision,CancellationToken token,IReadOnlySet<string>? exactPendingTickets=null)=>WithFence(async (held,state,ct)=>
    {
        if(state.HeadRevisions.GetValueOrDefault(owner)>revision) return true;
        long covered=state.Tickets.Where(pair=>pair.Value.Owner==owner && pair.Value.Kind=="original" && pair.Value.AdmittedRevision>0 && pair.Value.AdmittedRevision<=revision && exactPendingTickets?.Contains(pair.Key)==true).Sum(pair=>pair.Value.Completion);
        state.HeadRevisions[owner]=revision;
        long required=Math.Max(0,pending-covered);string key=Ticket("legacy","pending");
        long old=state.Tickets.GetValueOrDefault(key)?.Remaining??0;
        if(required>old) await Ensure(held,state,key,required,0,"legacy",ct).ConfigureAwait(false);
        else if(required<old) { state.Tickets[key]=state.Tickets[key] with { Remaining=required };await Save(held,state,ct).ConfigureAwait(false);Allocate(held,key,required); }
        else await Save(held,state,ct).ConfigureAwait(false);
        return true;
    },token);
    internal Task CommitAsync(string ticket,long revision,CancellationToken token)=>WithFence(async (held,state,ct)=>
    { if(state.Tickets.TryGetValue(ticket,out var reserved)) { state.Tickets[ticket]=reserved with { AdmittedRevision=revision };await Save(held,state,ct).ConfigureAwait(false); }return true; },token);
    internal Task<long> AvailableForOwnerAsync(CancellationToken token)=>WithFence((held,state,ct)=>
    { long own=state.Tickets.Values.Where(value=>value.Owner==owner).Sum(value=>value.Remaining),available=Available();return Task.FromResult(available>long.MaxValue-own?long.MaxValue:checked(available+own)); },token);
    internal Task ClaimLegacyAsync(string ticket,long liability,long revision,CancellationToken token,string? requestCommitment=null,string? role=null,string? requestId=null)=>WithFence(async (held,state,ct)=>
    {
        if(state.Tickets.TryGetValue(ticket,out var existing))
        {
            if(existing.Owner!=owner || existing.CommitmentSha256 is null || requestCommitment is not null && existing.CommitmentSha256!=requestCommitment) throw new IOException("Exact legacy physical ticket reconciliation required.");
            return true;
        }
        string key=Ticket("legacy","pending");
        if(!state.Tickets.TryGetValue(key,out var legacy) || legacy.Remaining<liability) throw new IOException("Exact legacy physical completion reconciliation required.");
        if(liability<=0) throw new IOException("Positive exact legacy completion liability required.");
        var chunks=_protected.Value?.PlanTransfer(configuration.Directory+"|"+key,liability);
        state.Tickets[key]=legacy with { Remaining=legacy.Remaining-liability };
        if(state.Transfers.Count+state.Tickets.Values.Sum(value=>value.TransferSlotsRemaining)+768>4096) throw new IOException("Legacy completion metadata requires exact reconciliation.");
        state.Tickets[ticket]=new(owner,"original",liability,liability,revision,liability,liability,requestCommitment??Hash(ticket+"|legacy"),role,requestId,768,768,384);
        if(chunks is not null) state.LegacyTransfer=new(key,ticket,state.Tickets[ticket].CommitmentSha256!,chunks);
        await Save(held,state,ct).ConfigureAwait(false);Fault?.Invoke("legacy-claim-ledger-durable");
        if(chunks is not null)
        {
            _protected.Value!.TransferReserved(configuration.Directory+"|"+key,configuration.Directory+"|"+ticket,chunks,()=>Fault?.Invoke("legacy-chunk-transferred"));
            Fault?.Invoke("legacy-transfer-durable");state.LegacyTransfer=null;await Save(held,state,ct).ConfigureAwait(false);Fault?.Invoke("legacy-transfer-completed");
        }
        Allocate(held,key,legacy.Remaining-liability);Allocate(held,ticket,liability);return true;
    },token);
    internal Task<IReadOnlyDictionary<string,Reservation>> OwnerTicketsAsync(CancellationToken token)=>WithFence((held,state,ct)=>
        Task.FromResult<IReadOnlyDictionary<string,Reservation>>(state.Tickets.Where(pair=>pair.Value.Owner==owner && pair.Value.Kind=="original").ToDictionary()),token);
    internal Task ReserveAsync(string ticket,long bytes,long completion,CancellationToken token,string? requestCommitment=null,string? role=null,string? requestId=null,int transferSlots=768)=>WithFence(async (held,state,ct)=>
    {
        string commitment=requestCommitment??Hash(ticket+"|"+bytes+"|"+completion);
        if(!CustodyBindingContract.Digest(commitment) || bytes<0 || completion<0 || completion>bytes || transferSlots is <1 or >4096) throw new IOException("Exact immutable physical reservation commitment required.");
        if(state.Tickets.TryGetValue(ticket,out var existing))
        {
            if(existing.Owner!=owner || existing.Kind!="original" || existing.InitialBytes!=bytes || existing.InitialCompletion!=completion || existing.CommitmentSha256!=commitment
                || existing.Role!=role || existing.RequestId!=requestId || existing.InitialTransferSlots!=transferSlots)
                throw new IOException("Conflicting or legacy unknown physical reservation commitment.");
        }
        else await Ensure(held,state,ticket,bytes,completion,"original",ct,commitment,role,requestId,transferSlots).ConfigureAwait(false);
        return true;
    },token);
    // All source admission writers and nonadmission reconciliation hold this same pool/volume fence.
    // A previously prepared writer cannot publish a head after another writer reclaimed its exact ticket.
    internal Task<bool> AdmitHeadAsync(string ticket,Func<CancellationToken,Task<(bool Admitted,bool Absent,long Revision)>> admission,CancellationToken token)=>WithFence(async (held,state,ct)=>
    {
        if(!state.Tickets.TryGetValue(ticket,out var original) || original.Owner!=owner) return false;
        var result=await admission(ct).ConfigureAwait(false);
        if(result.Admitted) { state.Tickets[ticket]=original with { AdmittedRevision=result.Revision };await Save(held,state,ct).ConfigureAwait(false);return true; }
        if(result.Absent && original.AdmittedRevision==0 && !state.TransferOwners.Values.Contains(ticket,StringComparer.Ordinal))
        { state.Tickets.Remove(ticket);await Save(held,state,ct).ConfigureAwait(false);Allocate(held,ticket,0); }
        return false;
    },token);
    private async Task Ensure(HeldArchiveDirectory held,Ledger state,string ticket,long bytes,long completion,string kind,CancellationToken token,string? commitment=null,string? role=null,string? requestId=null,int transferSlots=1)
    {
        if(_protected.Value is null && AllocationOverride is null) throw new IOException("External.EnrolledProtectedArchiveAllocationV1: completion needs pre-existing inode/directory-entry/private-written resources.");
        long old=state.Tickets.GetValueOrDefault(ticket)?.Remaining??0,extra=bytes-old;
        if(extra<0) return;
        long allocated=state.Tickets.Values.Sum(value=>value.Remaining);
        if(state.Tickets.Count>=configuration.MaximumTickets || checked(state.RetainedBytes+allocated+extra+MetadataBytes)>configuration.QuotaBytes || Available()<extra+Overhead)
            throw new IOException("Shared physical archive completion pool exhausted.");
        // The durable charge precedes allocation; every subsequent operation recovers all charged extents before admission.
        if(state.Tickets.Values.Any(value=>value.InitialBytes is null) || state.Transfers.Count+state.Tickets.Values.Sum(value=>value.TransferSlotsRemaining)+transferSlots>4096)
            throw new IOException("Shared completion transfer metadata unavailable; exact legacy reconciliation required.");
        state.Tickets[ticket]=new(owner,kind,bytes,completion,InitialBytes:bytes,InitialCompletion:completion,CommitmentSha256:commitment??Hash(ticket+"|"+bytes),Role:role,RequestId:requestId,InitialTransferSlots:transferSlots,TransferSlotsRemaining:transferSlots,CompletionTransferSlots:completion>0?384:0);
        await Save(held,state,token).ConfigureAwait(false);Allocate(held,ticket,bytes);Fault?.Invoke("reserved");
    }
    internal Task PublishAsync(string ticket,string path,ReadOnlyMemory<byte> bytes,CancellationToken token,bool completing=true,string? stableRecoveryCommitment=null)
        =>BoundedDurableFileOperation.RunWithInputAsync(bytes,(owned,ct)=>PublishCoreAsync(ticket,path,owned,ct,completing,stableRecoveryCommitment),token);
    private Task<bool> PublishCoreAsync(string ticket,string path,ReadOnlyMemory<byte> bytes,CancellationToken token,bool completing,string? stableRecoveryCommitment)=>WithFence(async (held,state,ct)=>
    {
        if(!Path.GetFullPath(path).StartsWith(archiveDirectory.TrimEnd('/')+"/",StringComparison.Ordinal)) throw new IOException("Enrolled archive publication required.");
        string publication=Hash(path+"|"+(stableRecoveryCommitment is null?Convert.ToHexString(SHA256.HashData(bytes.Span)):"committed-recovery-v1:"+stableRecoveryCommitment));
        string legacy=Ticket("legacy","pending");
        if(!state.Tickets.TryGetValue(ticket,out var reservation))
        { ticket=legacy;reservation=state.Tickets.GetValueOrDefault(ticket); }
        if(reservation is not null && reservation.Owner!=owner) throw new IOException("Exact physical publication owner required.");
        if(stableRecoveryCommitment is not null && (bytes.Length>1024*1024 || reservation is null || reservation.Role!="recovery-grant"
            || reservation.RequestId!=stableRecoveryCommitment || reservation.CommitmentSha256!=stableRecoveryCommitment || reservation.InitialCompletion!=0
            || reservation.InitialBytes!=1024*1024+2L*65536 || reservation.InitialTransferSlots!=1))
            throw new IOException("Exact committed recovery publication reservation required.");
        if(_protected.Value is { } fixedStore)
        {
            byte[]? original=fixedStore.Read(path,bytes.Length);
            if(original is not null)
            { if(!original.AsSpan().SequenceEqual(bytes.Span)) throw new IOException("Conflicting protected immutable original.");if(RemoveTransfer(state,publication)) await Save(held,state,ct).ConfigureAwait(false);return true; }
            long protectedCharge=checked((stableRecoveryCommitment is null?bytes.Length:1024*1024)+Overhead);
            if(!state.Transfers.Contains(publication))
            {
                if(reservation is null || reservation.Remaining<protectedCharge || reservation.TransferSlotsRemaining<1 || !completing && (reservation.Remaining-protectedCharge<reservation.Completion || reservation.TransferSlotsRemaining<=reservation.CompletionTransferSlots)) throw new IOException("Exact protected completion reservation required.");
                state.Tickets[ticket]=reservation with { Remaining=reservation.Remaining-protectedCharge,TransferSlotsRemaining=reservation.TransferSlotsRemaining-1 };
                state.Transfers.Add(publication);state.TransferOwners.Add(publication,ticket);state.RetainedBytes=checked(state.RetainedBytes+protectedCharge);await Save(held,state,ct).ConfigureAwait(false);
            }
            Fault?.Invoke("transferred");fixedStore.Publish(configuration.Directory+"|"+ticket,path,bytes.Span,stableRecoveryCommitment);Fault?.Invoke("published");
            RemoveTransfer(state,publication);await Save(held,state,ct).ConfigureAwait(false);return true;
        }
        if(AllocationOverride is null) throw new IOException("External.EnrolledProtectedArchiveAllocationV1: legacy admitted liability remains unknown without independently reconciled protected resources.");
        // Fence the immutable namespace without named staging or lock allocations.
        using var destination=HeldArchiveDirectory.Acquire(Path.GetDirectoryName(path)!,true);
        string name=Path.GetFileName(path);
        using var writer=await ArchiveFileLease.AcquirePublicationAsync(destination,name,ct).ConfigureAwait(false);
        byte[]? existing=destination.Read(name,bytes.Length);
        if(existing is not null) { if(!existing.AsSpan().SequenceEqual(bytes.Span)) throw new IOException("Conflicting physical-pool original.");if(RemoveTransfer(state,publication)) await Save(held,state,ct).ConfigureAwait(false);return true; }
        // Only recovery archives may regenerate a signature before any immutable file exists.
        // Their transfer identity is the immutable admitted grant, and its one reserved maximum
        // frame remains charged across interruption; existing conflicting bytes still deny.
        long charge=checked((stableRecoveryCommitment is null?bytes.Length:1024*1024)+Overhead);
        if(!state.Transfers.Contains(publication))
        {
            if(reservation is null || reservation.Remaining<charge || reservation.TransferSlotsRemaining<1 || !completing && (reservation.Remaining-charge<reservation.Completion || reservation.TransferSlotsRemaining<=reservation.CompletionTransferSlots)) throw new IOException("Exact physical completion ticket required.");
            state.Tickets[ticket]=reservation with { Remaining=reservation.Remaining-charge,TransferSlotsRemaining=reservation.TransferSlotsRemaining-1 };
            state.Transfers.Add(publication);state.TransferOwners.Add(publication,ticket);state.RetainedBytes=checked(state.RetainedBytes+charge);
            await Save(held,state,ct).ConfigureAwait(false);Allocate(held,ticket,reservation.Remaining-charge);
        }
        Fault?.Invoke("transferred");
        using(var output=new FileStream(destination.OpenUnpublishedFile(),FileAccess.ReadWrite))
        {
                if(Fallocate(output.SafeFileHandle.DangerousGetHandle().ToInt32(),0,bytes.Length)!=0) throw new IOException("Physical archive publication allocation unavailable.");
                await output.WriteAsync(bytes,ct).ConfigureAwait(false);await output.FlushAsync(ct).ConfigureAwait(false);output.Flush(true);
            destination.VerifyLocator();ct.ThrowIfCancellationRequested();destination.PublishUnpublishedFile(output.SafeFileHandle,name);destination.Flush();destination.VerifyLocator();Fault?.Invoke("published");
            RemoveTransfer(state,publication);await Save(held,state,ct).ConfigureAwait(false);return true;
        }
    },token);
    internal Task SettleAsync(string ticket,CancellationToken token)=>WithFence(async (held,state,ct)=>
    {
        Fault?.Invoke("before-settle");
        // A completion for an exact retained original may release only its own unused allocation.
        if(state.Tickets.TryGetValue(ticket,out var pending))
        {
            if(state.TransferOwners.Values.Contains(ticket,StringComparer.Ordinal)) throw new IOException("Outstanding exact publication transfers preserve completion ownership.");
            state.Tickets[ticket]=pending with { Remaining=0,Completion=0 };await Save(held,state,ct).ConfigureAwait(false);Allocate(held,ticket,0);Fault?.Invoke("settled-zero");
            state.Tickets.Remove(ticket);await Save(held,state,ct).ConfigureAwait(false);
        }
        return true;
    },token);
    private long Available()=>AvailableBytesOverride?.Invoke()??new DriveInfo(configuration.Directory).AvailableFreeSpace;
    private void Allocate(HeldArchiveDirectory held,string key,long bytes)
    {
        if(bytes<0) throw new IOException("Physical archive reserve underflow.");
        if(_protected.Value is { } fixedStore)
        { if(key!="metadata") { if(bytes==0) fixedStore.ReleaseUnused(configuration.Directory+"|"+key);else fixedStore.Reserve(configuration.Directory+"|"+key,bytes); }return; }
        if(AllocationOverride is not null) { AllocationOverride(key,bytes);return; }
        using var output=new FileStream(held.OpenFile("reserve-"+key,0x40|2),FileAccess.ReadWrite);
        ArchiveAllocationProof.Identity(output.SafeFileHandle);if(output.Length>0) ArchiveAllocationProof.RequirePrivateWritten(output.SafeFileHandle,output.Length);
        if(bytes>0 && Fallocate(output.SafeFileHandle.DangerousGetHandle().ToInt32(),0,bytes)!=0) throw new IOException("External.IndependentArchiveAtomicPhysicalAllocationProtocol");
        output.SetLength(bytes);output.Flush(true);held.Flush();
    }
    private async Task Save(HeldArchiveDirectory held,Ledger state,CancellationToken token)
    {
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(state,CustodyBindingContract.Json);
        long future=state.Tickets.Values.Sum(value=>checked(value.TransferSlotsRemaining*256L+256));
        if(bytes.Length+future>MaximumLedger || state.Transfers.Count+state.Tickets.Values.Sum(value=>value.TransferSlotsRemaining)>4096) throw new IOException("Bounded shared capacity metadata exhausted.");
        if(_protected.Value is { } fixedStore) { fixedStore.WriteMetadata(bytes);return; }
        int old=held.Read("capacity.json",MaximumLedger)!.Length;
        Allocate(held,"metadata",MetadataBytes-old-bytes.Length-Overhead);
        using var pending=held.CreateUnpublishedFile("capacity.json.pending");
        var output=pending.Stream;
        {
            if(Fallocate(output.SafeFileHandle.DangerousGetHandle().ToInt32(),0,bytes.Length)!=0) throw new IOException("Physical capacity metadata allocation unavailable.");
            await output.WriteAsync(bytes,token).ConfigureAwait(false);await output.FlushAsync(token).ConfigureAwait(false);output.Flush(true);
        }
        held.VerifyLocator();token.ThrowIfCancellationRequested();pending.Publish("capacity.json",true);held.Flush();Allocate(held,"metadata",MetadataBytes-bytes.Length);
    }
    internal sealed class Ledger
    {
        public string EnrollmentSha256 { get; set; }="";
        public long RetainedBytes { get; set; }
        public Dictionary<string,Reservation> Tickets { get; set; }=[];
        public HashSet<string> Transfers { get; set; }=[];
        public Dictionary<string,string> TransferOwners { get; set; }=[];
        public Dictionary<string,long> HeadRevisions { get; set; }=[];
        public LegacyChunkTransfer? LegacyTransfer { get; set; }
    }
    internal sealed record LegacyChunkTransfer(string Source,string Target,string CommitmentSha256,ProtectedArchiveStore.TransferChunk[] Chunks);
    private static bool RemoveTransfer(Ledger state,string publication)
    {
        if(!state.Transfers.Remove(publication)) return false;
        if(state.TransferOwners.Remove(publication,out var ticket) && state.Tickets.TryGetValue(ticket,out var reservation))
            state.Tickets[ticket]=reservation with { TransferSlotsRemaining=checked(reservation.TransferSlotsRemaining+1) };
        return true;
    }
    internal sealed record Reservation(string Owner,string Kind,long Remaining,long Completion,long AdmittedRevision=0,long? InitialBytes=null,long? InitialCompletion=null,
        string? CommitmentSha256=null,string? Role=null,string? RequestId=null,int InitialTransferSlots=0,int TransferSlotsRemaining=0,int CompletionTransferSlots=0);
    [DllImport("libc",EntryPoint="posix_fallocate")] private static extern int Fallocate(int fd,long offset,long length);
}
