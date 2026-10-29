using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Existing fixed inode/entry/extent resources. Admission reserves disjoint chunks; completion only overwrites held private blocks, with no create/rename/truncate/journal allocation.</summary>
internal sealed class ProtectedArchiveStore:IDisposable
{
    private const int Header=8192;
    // Cross-process owners serialize on the volume inode; inherited nested borrowers also serialize these local atomic updates.
    private static readonly object Mutation=new();
    private static readonly AsyncLocal<Action<string>?> MetadataFaults=new();
    private static readonly AsyncLocal<Action<string>?> Finds=new();
    private static readonly AsyncLocal<Action?> Publications=new();
    private static readonly AsyncLocal<int?> ReadLimits=new();
    internal static Action? PublicationFault { get=>Publications.Value;set=>Publications.Value=value; }
    internal static int? MaximumReadBytes { get=>ReadLimits.Value;set=>ReadLimits.Value=value; }
    internal static Action<string>? FindFault { get=>Finds.Value;set=>Finds.Value=value; }
    internal static Action<string>? MetadataFault { get=>MetadataFaults.Value;set=>MetadataFaults.Value=value; }
    private readonly ProtectedArchiveAllocationInstallation _config;
    private readonly CancellationToken _token;
    private readonly HeldArchiveDirectory _directory;
    private readonly FileStream _data,_meta0,_meta1;
    private long Stride=>2L*Header+_config.ChunkBytes;
    private long Offset(int slot)=>Header+slot*Stride;
    private static readonly JsonSerializerOptions Json=CustodyBindingContract.Json;
    private static string Digest(ReadOnlySpan<byte> value)=>Convert.ToHexStringLower(SHA256.HashData(value));
    /// <summary>Opens only independently enrolled resources, checks single-link distinct identities and private written allocation before they count.</summary>
    public ProtectedArchiveStore(ProtectedArchiveAllocationInstallation config,CancellationToken token=default)
    {
        _token=token.CanBeCanceled?token:BoundedDurableFileOperation.CurrentToken;_token.ThrowIfCancellationRequested();
        config=config with { ArchiveRoots=new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(config.ArchiveRoots.ToDictionary()) };
        _config=config;_directory=HeldArchiveDirectory.Acquire(config.Directory);
        try
        {
            if(config.ChunkBytes is >=4096 and <=65536 && 65536%config.ChunkBytes!=0) throw new IOException("External.EnrolledProtectedArchiveChunkGeometryDividing64KiBLiabilityQuantum");
            if(config.StorageProtocol!="Ext4PrivateWrittenNoSnapshotsV1" || config.ChunkBytes is <4096 or >65536 || config.ChunkBytes%4096!=0 || config.ChunkCount is <1 or >65536 || config.MaximumMetadataBytes is <65536 or >32*1024*1024
                || config.ArchiveRoots.Count is <1 or >64 || Digest(_directory.Read("allocation-enrollment.json",65536)??throw new IOException("Existing protected allocation enrollment required."))!=config.EnrollmentSha256.ToLowerInvariant())
                throw new IOException("External.EnrolledProtectedArchiveAllocationV1");
            var enrollment=JsonSerializer.Deserialize<ProtectedArchiveAllocationInstallation>(_directory.Read("allocation-enrollment.json",65536)!,Json)!;
            // Enrollment omits only its self hash; all physical/resources/policy/root identities must match the accepted configuration.
            if(!System.Text.Json.Nodes.JsonNode.DeepEquals(JsonSerializer.SerializeToNode(enrollment with { EnrollmentSha256=config.EnrollmentSha256 },Json),JsonSerializer.SerializeToNode(config,Json))) throw new IOException("Exact protected archive allocation enrollment changed.");
            _data=Open("allocation-data.bin",config.DataIdentity,checked(Header+Stride*config.ChunkCount));
            _meta0=Open("allocation-metadata-0.bin",config.Metadata0Identity,config.MaximumMetadataBytes+Header);
            _meta1=Open("allocation-metadata-1.bin",config.Metadata1Identity,config.MaximumMetadataBytes+Header);
            string device=_directory.Identity[.._directory.Identity.LastIndexOf(':')]+":";
            if(new[]{config.DataIdentity,config.Metadata0Identity,config.Metadata1Identity}.Any(identity=>!identity.StartsWith(device,StringComparison.Ordinal))) throw new IOException("Protected allocation resources must occupy the actual fenced filesystem device.");
            byte[] geometry=new byte[Header];ReadExactly(_data.SafeFileHandle,geometry,0);
            byte[] expectedGeometry=JsonSerializer.SerializeToUtf8Bytes(new { Protocol="ProtectedArchiveGeometryV1",config.DataIdentity,config.ChunkBytes,config.ChunkCount },Json);
            if(!geometry.AsSpan(0,expectedGeometry.Length).SequenceEqual(expectedGeometry) || geometry.AsSpan(expectedGeometry.Length).IndexOfAnyExcept((byte)0)>=0) throw new IOException("Exact independently enrolled allocation geometry required; aliases cannot reinterpret one physical inode.");
            if(new[]{config.DataIdentity,config.Metadata0Identity,config.Metadata1Identity}.Distinct(StringComparer.Ordinal).Count()!=3) throw new IOException("Distinct enrolled protected archive inodes required.");
            foreach(var root in config.ArchiveRoots) { using var held=HeldArchiveDirectory.Acquire(root.Key);if(held.Identity!=root.Value || ArchiveFileLease.FilesystemIdentity(root.Key)!=ArchiveFileLease.FilesystemIdentity(config.Directory)) throw new IOException("Enrolled protected archive root identity changed."); }
        }
        catch { Dispose();throw; }
    }
    private FileStream Open(string name,string identity,long size)
    {
        var stream=new FileStream(_directory.OpenFile(name,2),FileAccess.ReadWrite);
        try { if(stream.Length!=size) throw new IOException("Exact preallocated archive resource size required.");ArchiveAllocationProof.RequirePrivateWritten(stream.SafeFileHandle,size,identity,_token);return stream; }
        catch { stream.Dispose();throw; }
    }
    private sealed record Frame(long Generation,string State,string? Ticket=null,string? Publication=null,string? Path=null,int Next=-1,int Length=0,string? Sha256=null,int Count=0);
    private Frame? ReadHeader(int index)
    {
        Frame? selected=null;var frames=new Frame[2];
        for(int copy=0;copy<2;copy++)
        {
            byte[] block=new byte[Header];ReadExactly(_data.SafeFileHandle,block,Offset(index)+copy*Header);int length=BinaryPrimitives.ReadInt32LittleEndian(block);
            if(length==0) throw new IOException("Unknown missing initialized protected archive header; independently enrolled dual initial frames required.");
            if(length<1 || length>Header-40 || !SHA256.HashData(block.AsSpan(40,length)).AsSpan().SequenceEqual(block.AsSpan(8,32))) throw new IOException("Preserve unknown damaged protected archive header.");
            var value=JsonSerializer.Deserialize<Frame>(block.AsSpan(40,length),Json);
            if(value is null || value.Generation<1 || value.Generation%2!=copy || value.State is not ("Free" or "Reserved" or "Preparing" or "Published")) throw new IOException("Preserve unknown malformed protected archive header.");
            frames[copy]=value;if(selected is null || value.Generation>selected.Generation) selected=value;
        }
        if(Math.Abs(frames[0].Generation-frames[1].Generation)!=1) throw new IOException("Preserve unknown nonconsecutive protected archive headers.");return selected;
    }
    private void ReadExactly(Microsoft.Win32.SafeHandles.SafeFileHandle handle,Span<byte> bytes,long offset)
    {
        int used=0;
        while(used<bytes.Length)
        {
            _token.ThrowIfCancellationRequested();BoundedDurableFileOperation.CurrentToken.ThrowIfCancellationRequested();
            int count=Math.Min(bytes.Length-used,ReadLimits.Value??int.MaxValue);
            if(count<1) throw new IOException("Positive positional read bound required.");
            int read=RandomAccess.Read(handle,bytes.Slice(used,count),checked(offset+used));
            if(read==0) throw new IOException("Preserve unknown truncated protected archive input.");
            used+=read;
        }
    }
    private void WriteHeader(int slot,Frame frame,bool flush=true)
    {
        byte[] raw=JsonSerializer.SerializeToUtf8Bytes(frame,Json),block=new byte[Header];if(raw.Length>Header-40) throw new IOException("Bounded protected archive header required.");
        BinaryPrimitives.WriteInt32LittleEndian(block,raw.Length);SHA256.HashData(raw).CopyTo(block,8);raw.CopyTo(block,40);
        RandomAccess.Write(_data.SafeFileHandle,block,Offset(slot)+(frame.Generation%2)*Header);if(flush) _data.Flush(true);
    }
    private List<(int Index,Frame? Frame)> Scan()
    {
        _directory.VerifyLocator();if(_data.Length!=Header+Stride*_config.ChunkCount) throw new IOException("Protected data extent length changed.");ArchiveAllocationProof.RequirePrivateWritten(_data.SafeFileHandle,_data.Length,_config.DataIdentity,_token);
        var result=new List<(int,Frame?)>(_config.ChunkCount);for(int i=0;i<_config.ChunkCount;i++) { _token.ThrowIfCancellationRequested();BoundedDurableFileOperation.CurrentToken.ThrowIfCancellationRequested();result.Add((i,ReadHeader(i))); }return result;
    }
    /// <summary>Reserves actual disjoint fixed chunks before effect admission. Partial reservations survive crashes and exact retry completes them.</summary>
    public void Reserve(string ticket,long bytes)
    { lock(Mutation) {
        ArchiveVolumeFence.RequireCurrentDestination(_config.Directory);
        if(string.IsNullOrWhiteSpace(ticket) || ticket.Length>256 || bytes<0) throw new IOException("Exact protected archive ticket required.");
        var worst=new Frame(long.MaxValue,"Published",ticket,new string('f',64),new string('f',64),int.MaxValue,CustodyBindingContract.MaximumOriginalBytes,new string('f',64),_config.ChunkCount);
        if(JsonSerializer.SerializeToUtf8Bytes(worst,Json).Length>Header-40) throw new IOException("Pre-admitted complete archive header bound required.");
        var all=Scan();long requested=(bytes+_config.ChunkBytes-1)/_config.ChunkBytes;var prior=all.Where(v=>v.Frame?.Ticket==ticket).ToArray();
        if(prior.LongLength>=requested) return;
        var free=all.Where(v=>v.Frame is null || v.Frame.State=="Free").Take(checked((int)(requested-prior.Length))).ToArray();if(free.LongLength<requested-prior.Length) throw new IOException("Protected archive inode/entry/private-block capacity exhausted before admission.");
        foreach(var item in free) WriteHeader(item.Index,new((item.Frame?.Generation??0)+1,"Reserved",ticket),false);
        _data.Flush(true);
     } }
    /// <summary>Releases only unused chunks of this exact settled/nonadmitted ticket. Published and uncertain preparations remain charged.</summary>
    public void ReleaseUnused(string ticket)
    { lock(Mutation) {  ArchiveVolumeFence.RequireCurrentDestination(_config.Directory);foreach(var item in Scan().Where(v=>v.Frame?.Ticket==ticket && v.Frame.State=="Reserved" && v.Frame.Publication is null)) WriteHeader(item.Index,new(item.Frame!.Generation+1,"Free"),false);_data.Flush(true);  } }
    internal sealed record TransferChunk(int Index,long Generation);
    internal void RequireReservable(string ticket,long bytes)
    { lock(Mutation) {
        ArchiveVolumeFence.RequireCurrentDestination(_config.Directory);
        if(bytes<0) throw new IOException("Positive fixed reservation required.");var all=Scan();
        long needed=checked((bytes+_config.ChunkBytes-1)/_config.ChunkBytes);
        if(all.LongCount(value=>value.Frame?.Ticket==ticket || value.Frame?.State=="Free")<needed) throw new IOException("Protected archive inode/entry/private-block capacity exhausted before admission.");
    } }
    internal TransferChunk[] PlanTransfer(string source,long bytes,long remaining)
    { lock(Mutation) {
        ArchiveVolumeFence.RequireCurrentDestination(_config.Directory);
        if(bytes<=0 || remaining<0) throw new IOException("Positive exact funded legacy transfer required.");
        int count=checked((int)((bytes+_config.ChunkBytes-1)/_config.ChunkBytes));
        var eligible=Scan().Where(v=>v.Frame?.Ticket==source && v.Frame.State=="Reserved" && v.Frame.Publication is null).ToArray();
        if(eligible.LongLength<count+checked((remaining+_config.ChunkBytes-1)/_config.ChunkBytes)) throw new IOException("Exact disjoint funded legacy split chunks unavailable; preserve all remaining/published/preparing liabilities.");
        var selected=eligible.Take(count).ToArray();
        return selected.Select(v=>new TransferChunk(v.Index,v.Frame!.Generation)).ToArray();
    } }
    internal void TransferReserved(string source,string target,TransferChunk[] chunks,Action? boundary=null)
    { lock(Mutation) {
        ArchiveVolumeFence.RequireCurrentDestination(_config.Directory);
        if(source==target || chunks.Length<1 || chunks.Select(v=>v.Index).Distinct().Count()!=chunks.Length) throw new IOException("Exact disjoint funded transfer required.");
        var all=Scan();
        foreach(var chunk in chunks)
        {
            _token.ThrowIfCancellationRequested();BoundedDurableFileOperation.CurrentToken.ThrowIfCancellationRequested();
            if(chunk.Index<0 || chunk.Index>=all.Count) throw new IOException("Unknown protected transfer chunk.");
            var frame=all[chunk.Index].Frame;
            if(frame?.State!="Reserved" || frame.Publication is not null) throw new IOException("Unknown funded transfer ownership; preserve liability.");
            if(frame.Ticket==target && frame.Generation==chunk.Generation+1) continue;
            if(frame.Ticket!=source || frame.Generation!=chunk.Generation) throw new IOException("Funded transfer ownership changed.");
            WriteHeader(chunk.Index,frame with { Generation=checked(frame.Generation+1),Ticket=target });
            _token.ThrowIfCancellationRequested();BoundedDurableFileOperation.CurrentToken.ThrowIfCancellationRequested();boundary?.Invoke();
            _token.ThrowIfCancellationRequested();BoundedDurableFileOperation.CurrentToken.ThrowIfCancellationRequested();
        }
    } }
    /// <summary>Publishes into reserved fixed blocks. Commit header follows durable data; lost acknowledgements recover exact original bytes.</summary>
    public void Publish(string ticket,string path,ReadOnlySpan<byte> bytes,string? stableCommitment=null)
    { lock(Mutation) {
        ArchiveVolumeFence.RequireCurrentDestination(_config.Directory);RequirePath(path);if(bytes.Length is <1 or >CustodyBindingContract.MaximumOriginalBytes) throw new IOException("Bounded protected archive original required.");
        var all=Scan();var existing=ReadLegacy(path,bytes.Length)??ReadFrom(all,path,bytes.Length);if(existing is not null) { if(!existing.AsSpan().SequenceEqual(bytes)) throw new IOException("Conflicting protected immutable original.");return; }
        string publication=Digest(System.Text.Encoding.UTF8.GetBytes(path+"|"+(stableCommitment??Digest(bytes))));
        string pathDigest=Digest(System.Text.Encoding.UTF8.GetBytes(path));
        if(all.Any(value=>value.Frame?.Path==pathDigest && value.Frame.State=="Preparing" && value.Frame.Publication!=publication)) throw new IOException("Exact unresolved protected original bytes cannot be replaced.");
        var selected=all.Where(v=>v.Frame?.Ticket==ticket && (v.Frame.Publication==publication || v.Frame.State=="Reserved" && v.Frame.Publication is null)).OrderBy(v=>v.Frame!.Publication==publication?0:1).ThenBy(v=>v.Index).Take((bytes.Length+_config.ChunkBytes-1)/_config.ChunkBytes).ToArray();
        if(selected.Length!=(bytes.Length+_config.ChunkBytes-1)/_config.ChunkBytes) throw new IOException("Exact reserved protected completion resources unavailable.");
        int used=0;
        for(int i=0;i<selected.Length;i++)
        {
            _token.ThrowIfCancellationRequested();var item=selected[i];int count=Math.Min(_config.ChunkBytes,bytes.Length-used);
            WriteHeader(item.Index,new(item.Frame!.Generation+1,"Preparing",ticket,publication,i==0?Digest(System.Text.Encoding.UTF8.GetBytes(path)):null,i+1<selected.Length?selected[i+1].Index:-1,i==0?bytes.Length:count,i==0?Digest(bytes):null,i==0?selected.Length:0),false);
            RandomAccess.Write(_data.SafeFileHandle,bytes.Slice(used,count),Offset(item.Index)+2*Header);used+=count;
        }
        _data.Flush(true);_directory.VerifyLocator();ArchiveAllocationProof.RequirePrivateWritten(_data.SafeFileHandle,_data.Length,_config.DataIdentity,_token);
        Publications.Value?.Invoke();var head=ReadHeader(selected[0].Index)!;WriteHeader(selected[0].Index,head with { Generation=head.Generation+1,State="Published" });
     } }
    /// <summary>Reads only a complete digest-verified committed original, never a damaged/preparing body relabeled as a later result.</summary>
    public byte[]? Read(string path,int maximum)=>ReadLegacy(path,maximum)??ReadFrom(Scan(),path,maximum);
    // Candidate bytes never establish publication. Only a caller validating the exact signed request/case pins may republish these same bytes.
    internal byte[]? ReadCandidate(string path,int maximum)=>ReadLegacy(path,maximum)??ReadFrom(Scan(),path,maximum,true);
    private byte[]? ReadLegacy(string path,int maximum)
    {
        RequirePath(path);_token.ThrowIfCancellationRequested();
        try { return BoundedRegularFile.ReadLiteralInOwnedWorkerAsync(path,maximum,_token).ConfigureAwait(false).GetAwaiter().GetResult(); }
        catch(FileNotFoundException) { return null; }
    }
    private byte[]? ReadFrom(List<(int Index,Frame? Frame)> all,string path,int maximum,bool includePreparing=false)
    {
        RequirePath(path);var matches=all.Where(v=>v.Frame?.Path==Digest(System.Text.Encoding.UTF8.GetBytes(path)) && (v.Frame.State=="Published" || includePreparing && v.Frame.State=="Preparing")).ToArray();if(matches.Length==0) return null;if(matches.Length!=1) throw new IOException("Conflicting protected original locators.");
        var head=matches[0];if(head.Frame!.Length is <1 || head.Frame.Length>maximum || head.Frame.Count<1 || head.Frame.Count>_config.ChunkCount) throw new IOException("Bounded protected original frame required.");
        byte[] raw=new byte[head.Frame.Length];int used=0,next=head.Index;var visited=new HashSet<int>();
        while(next>=0)
        {
            if(next>=all.Count || !visited.Add(next)) throw new IOException("Damaged protected archive chain.");var item=all[next];
            if(item.Frame?.Ticket!=head.Frame.Ticket || item.Frame?.Publication!=head.Frame.Publication || visited.Count>head.Frame.Count) throw new IOException("Protected original ownership changed.");
            int count=Math.Min(_config.ChunkBytes,raw.Length-used);if(count<=0) throw new IOException("Damaged protected archive original.");ReadExactly(_data.SafeFileHandle,raw.AsSpan(used,count),Offset(next)+2*Header);used+=count;next=item.Frame!.Next;
        }
        if(used!=raw.Length || visited.Count!=head.Frame.Count || Digest(raw)!=head.Frame.Sha256) throw new IOException("Preserve unknown damaged protected completion original.");return raw;
    }
    private void RequirePath(string path)
    { if(!Path.IsPathFullyQualified(path) || path!=Path.GetFullPath(path) || path.Length>4096 || !_config.ArchiveRoots.Keys.Any(root=>path.StartsWith(root.TrimEnd('/')+"/",StringComparison.Ordinal))) throw new IOException("Exact enrolled protected archive locator required."); }
    /// <summary>Reads the latest valid fixed metadata bank; the legacy genesis remains untouched.</summary>
    public byte[]? ReadMetadata()
    { lock(Mutation) {
        var a=Metadata(_meta0);var b=Metadata(_meta1);
        if(a.Empty || b.Empty || Math.Abs(a.Generation-b.Generation)!=1)
            throw new IOException("Unknown missing initialized archive metadata; independently enrolled dual initial metadata required.");
        var selected=a.Bytes is not null && (b.Bytes is null || a.Generation>=b.Generation)?a:b;
        if(selected.Bytes is null) throw new IOException("Preserve unknown damaged/nonempty protected archive metadata; legacy genesis is not recovery.");
        if(selected.InProgress)
        {
            ArchiveVolumeFence.RequireCurrentDestination(_config.Directory);
            var output=a.Generation==selected.Generation?_meta0:_meta1;output.Flush(true);
            CommitMetadataHeader(output,selected.Generation,selected.Bytes,2);
        }
        return selected.Bytes;
     } }
    private (long Generation,byte[]? Bytes,bool InProgress,bool Empty) Metadata(FileStream file)
    {
        if(file.Length!=_config.MaximumMetadataBytes+Header) throw new IOException("Protected metadata extent length changed.");
        byte[] header=new byte[Header];ReadExactly(file.SafeFileHandle,header,0);
        if(header.All(value=>value==0))
        {
            byte[] buffer=new byte[65536];long offset=Header;
            while(offset<file.Length)
            { _token.ThrowIfCancellationRequested();int count=(int)Math.Min(buffer.Length,file.Length-offset);ReadExactly(file.SafeFileHandle,buffer.AsSpan(0,count),offset);if(buffer.AsSpan(0,count).IndexOfAnyExcept((byte)0)>=0) throw new IOException("Nonempty protected metadata is not initial genesis.");offset+=count; }
            return (0,null,false,true);
        }
        long generation=BinaryPrimitives.ReadInt64LittleEndian(header);int length=BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8)),phase=BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(12));
        if(generation<1 || length<1 || length>_config.MaximumMetadataBytes || phase is not (1 or 2)) throw new IOException("Preserve unknown damaged protected metadata header.");
        byte[] raw=new byte[length];ReadExactly(file.SafeFileHandle,raw,Header);bool exact=SHA256.HashData(raw).AsSpan().SequenceEqual(header.AsSpan(16,32));
        if(!exact && phase==2) throw new IOException("Preserve unknown damaged committed protected metadata; no legacy fallback.");
        return (generation,exact?raw:null,phase==1,false);
    }
    private static void CommitMetadataHeader(FileStream output,long generation,ReadOnlySpan<byte> bytes,int phase)
    {
        byte[] header=new byte[Header];BinaryPrimitives.WriteInt64LittleEndian(header,generation);BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8),bytes.Length);BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12),phase);SHA256.HashData(bytes).CopyTo(header,16);
        BoundedDurableFileOperation.CurrentToken.ThrowIfCancellationRequested();RandomAccess.Write(output.SafeFileHandle,header,0);output.Flush(true);
    }
    /// <summary>Durably updates one existing bank with explicit uncommitted intent; a damaged committed frame never becomes legacy genesis.</summary>
    public void WriteMetadata(ReadOnlySpan<byte> bytes)
    { lock(Mutation) {
        ArchiveVolumeFence.RequireCurrentDestination(_config.Directory);_directory.VerifyLocator();BoundedDurableFileOperation.CurrentToken.ThrowIfCancellationRequested();
        _=ReadMetadata(); // Validate both banks and finish a data-durable intent before selecting the next generation.
        var a=Metadata(_meta0);var b=Metadata(_meta1);
        if(bytes.Length<1 || bytes.Length>_config.MaximumMetadataBytes) throw new IOException("Bounded recoverable protected archive metadata required.");
        var output=a.InProgress && a.Bytes is null?_meta0:b.InProgress && b.Bytes is null?_meta1:a.Generation<=b.Generation?_meta0:_meta1;
        // An intent-only interruption already owns the next generation. Reuse it: skipping it
        // would leave the surviving committed bank two generations behind on reopening.
        var selected=ReferenceEquals(output,_meta0)?a:b;
        long generation=selected.InProgress && selected.Bytes is null?selected.Generation:checked(Math.Max(a.Generation,b.Generation)+1);
        string identity=ReferenceEquals(output,_meta0)?_config.Metadata0Identity:_config.Metadata1Identity;if(output.Length!=_config.MaximumMetadataBytes+Header) throw new IOException("Protected metadata allocation length changed.");ArchiveAllocationProof.RequirePrivateWritten(output.SafeFileHandle,output.Length,identity);
        CommitMetadataHeader(output,generation,bytes,1);MetadataFaults.Value?.Invoke("intent-durable");RandomAccess.Write(output.SafeFileHandle,bytes,Header);output.Flush(true);MetadataFaults.Value?.Invoke("data-durable");CommitMetadataHeader(output,generation,bytes,2);_directory.VerifyLocator();
     } }
    internal int PublishedOriginals=>Scan().Count(value=>value.Frame?.State=="Published");
    internal long ReservedBytes(string ticket)=>checked((long)Scan().Count(value=>value.Frame?.Ticket==ticket)*_config.ChunkBytes);
    /// <summary>Resolves only an existing enrolled archive marker. It allocates no filesystem resources and follows no symbolic path component.</summary>
    public static ProtectedArchiveStore? Find(string path,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();Finds.Value?.Invoke(path);token.ThrowIfCancellationRequested();
        string? parent=Path.GetDirectoryName(path);while(parent is not null)
        {
            token.ThrowIfCancellationRequested();BoundedDurableFileOperation.CurrentToken.ThrowIfCancellationRequested();
            using var ancestor=HeldArchiveDirectory.AcquireExistingAncestor(parent);var raw=ancestor.Read(".archive-allocation.json",65536);
            if(raw is not null)
            {
                CustodyBindingContract.RejectDuplicateProperties(raw);var config=JsonSerializer.Deserialize<ProtectedArchiveAllocationInstallation>(raw,Json)??throw new IOException("Protected archive marker malformed.");
                if(!config.ArchiveRoots.TryGetValue(ancestor.PathName,out string? expected) || expected!=ancestor.Identity) throw new IOException("Protected archive marker/root ownership changed.");return new(config,token);
            }
            parent=Path.GetDirectoryName(ancestor.PathName);if(ancestor.PathName=="/") break;
        }
        return null;
    }
    /// <summary>Reads an enrolled fixed original through its preserved logical locator.</summary>
    public static byte[]? ReadEnrolled(string path,int maximum,CancellationToken token=default) { using var store=Find(path,token);return store?.Read(path,maximum); }
    /// <summary>Checks existing ordinary or protected original presence without creating a directory/file.</summary>
    public static bool Exists(string path)=>File.Exists(path) || ReadEnrolled(path,CustodyBindingContract.MaximumOriginalBytes) is not null;
    /// <summary>Bounds the complete logical lookup, retaining late filesystem ownership through cleanup.</summary>
    public static Task<bool> ExistsAsync(string path,CancellationToken token)=>BoundedDurableFileOperation.RunAsync(ct=> { ct.ThrowIfCancellationRequested();bool exists=Exists(path);ct.ThrowIfCancellationRequested();return Task.FromResult(exists); },token);
    /// <inheritdoc />
    public void Dispose() { _data?.Dispose();_meta0?.Dispose();_meta1?.Dispose();_directory?.Dispose(); }
}
