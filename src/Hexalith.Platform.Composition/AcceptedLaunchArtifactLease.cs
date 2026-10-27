using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Composition;

// A production launch consumes complete accepted, published bytes from an existing immutable namespace.
// No production build can reopen a mutable sibling checkout under this acceptance.
internal sealed class AcceptedLaunchArtifactLease:IDisposable,Aspire.Hosting.ApplicationModel.IResourceAnnotation
{
    internal bool IsDisposed { get; private set; }
    private readonly List<HeldArchiveDirectory> _directories=[];
    private readonly List<FileStream> _files=[];
    internal string Root { get; private set; }="";
    internal string EntryPoint { get; private set; }="";
    private static readonly AsyncLocal<Func<int,bool,bool>?> KernelProof=new();
    internal static Func<int,bool,bool>? KernelProofOverride { get=>KernelProof.Value;set=>KernelProof.Value=value; } // Isolated tests only; production has no configuration fallback.
    private static void RequireKernel(int descriptor,bool directory)
    {
        if(KernelProofOverride is { } fixture) { if(!fixture(descriptor,directory)) throw new IOException("Fixture immutable release proof denied.");return; }
        if(!OperatingSystem.IsLinux() || GetFlags(descriptor,0x80086601,out int flags)!=0 || (flags&0x10)==0)
            throw new IOException("External.AcceptedImmutableLaunchNamespaceV1");
        if(!directory && (flags&0x100000)==0) throw new IOException("External.AcceptedFsVerityLaunchFileV1");
    }
    internal static AcceptedLaunchArtifactLease Acquire(JsonElement inventory,string resource,string expectedRoot,CancellationToken token)
        =>BoundedDurableFileOperation.RunAsync(ct=>Task.FromResult(AcquireCore(inventory.Clone(),resource,expectedRoot,ct)),token).GetAwaiter().GetResult();
    private static AcceptedLaunchArtifactLease AcquireCore(JsonElement inventory,string resource,string expectedRoot,CancellationToken token)
    {
        var result=new AcceptedLaunchArtifactLease();
        try
        {
            if(inventory.GetProperty("protocol").GetString()!="AcceptedCompleteImmutableLaunchGraphV1") throw new InvalidDataException("Accepted complete launch graph protocol required.");
            var entries=inventory.GetProperty("resources").EnumerateArray().Where(value=>value.GetProperty("resource").GetString()==resource).ToArray();
            if(entries.Length!=1) throw new InvalidDataException("Every selected launched sibling needs one accepted artifact graph.");
            var graph=entries[0];string root=graph.GetProperty("root").GetString()!;
            if(root!=expectedRoot || !Path.IsPathFullyQualified(root) || root!=Path.GetFullPath(root)) throw new InvalidDataException("Exact accepted release root required.");
            var held=HeldArchiveDirectory.Acquire(root);result._directories.Add(held);
            if(held.Identity!=graph.GetProperty("directoryIdentity").GetString()) throw new IOException("Accepted launch directory identity changed.");
            RequireKernel(held.NativeDescriptor,true);
            string entryPoint=graph.GetProperty("entryPoint").GetString()!;
            if(graph.GetProperty("executionProtocol").GetString()!="SelfContainedStaticElfV1"
                || graph.GetProperty("wholeImageNativeLoaderClosure").GetProperty("protocol").GetString()!="IndependentlyAcceptedStaticElfKernelClosureV1"
                || !System.Text.RegularExpressions.Regex.IsMatch(graph.GetProperty("wholeImageNativeLoaderClosure").GetProperty("evidenceSha256").GetString()??"","^[0-9a-f]{64}$"))
                throw new IOException("External.AcceptedWholeImageNativeLoaderClosureV1");
            var files=graph.GetProperty("files").EnumerateArray().ToDictionary(value=>value.GetProperty("path").GetString()!,StringComparer.Ordinal);
            if(files.Count is <1 or >10000 || !files.ContainsKey(entryPoint)) throw new InvalidDataException("Complete bounded release inventory and entry point required.");
            var found=new HashSet<string>(StringComparer.Ordinal);
            int directories=0,entriesSeen=0;
            void Walk(string relative,HeldArchiveDirectory directory,int depth)
            {
                if(depth>64) throw new IOException("Bounded accepted release depth required.");
                token.ThrowIfCancellationRequested();RequireKernel(directory.NativeDescriptor,true);
                foreach(string path in Directory.EnumerateFileSystemEntries(directory.HeldPath))
                {
                    token.ThrowIfCancellationRequested();if(++entriesSeen>20000) throw new IOException("Bounded accepted release entry census required.");string name=Path.GetFileName(path),key=relative+name;
                    var attributes=File.GetAttributes(path);if((attributes&FileAttributes.ReparsePoint)!=0) throw new IOException("No symbolic accepted release member.");
                    if((attributes&FileAttributes.Directory)!=0)
                    {
                        if(++directories>10000) throw new IOException("Bounded accepted release directory census required.");
                        var child=HeldArchiveDirectory.Acquire(Path.Combine(root,key));result._directories.Add(child);Walk(key+"/",child,depth+1);continue;
                    }
                    if(!files.TryGetValue(key,out var pin) || !found.Add(key)) throw new IOException("Unaccepted release graph member.");
                    var stream=new FileStream(directory.OpenFile(name,0),FileAccess.Read);result._files.Add(stream);
                    RequireKernel(stream.SafeFileHandle.DangerousGetHandle().ToInt32(),false);
                    if(stream.Length!=pin.GetProperty("bytes").GetInt64() || stream.Length is <1 or >1024L*1024*1024) throw new IOException("Bounded exact accepted release file required.");
                    using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[65536];int count;
                    while((count=stream.Read(buffer))!=0) { token.ThrowIfCancellationRequested();digest.AppendData(buffer,0,count); }
                    if(Convert.ToHexStringLower(digest.GetHashAndReset())!=pin.GetProperty("sha256").GetString()) throw new IOException("Accepted launched file bytes changed.");
                    if(resource=="agents-ui" && graph.TryGetProperty("listener",out var uiListener) && uiListener.TryGetProperty("uiHost",out var uiHost) && key==uiHost.GetProperty("artifact").GetString())
                    {
                        if(stream.Length>65536) throw new InvalidDataException(AcceptedListenerBindings.UiHostRequirement);
                        byte[] contractBytes=new byte[(int)stream.Length];stream.Position=0;stream.ReadExactly(contractBytes);
                        AcceptedListenerBindings.ValidateUiHostArtifact(uiHost,contractBytes);
                    }
                    if(key==entryPoint)
                    {
                        // Effective local process access includes mode/ACL and mount noexec; bytes alone confer no execute permission.
                        if(AccessAt(-100,"/proc/self/fd/"+stream.SafeFileHandle.DangerousGetHandle().ToInt32(),1,0x200)!=0)
                            throw new IOException("External.AcceptedExecutableEntrypointAccessV1");
                        ValidateStaticElf(stream);
                    }
                    token.ThrowIfCancellationRequested();RequireKernel(stream.SafeFileHandle.DangerousGetHandle().ToInt32(),false);
                }
                directory.VerifyLocator();
            }
            Walk("",held,0);if(!found.SetEquals(files.Keys)) throw new IOException("Accepted release graph is incomplete.");
            foreach(var directory in result._directories) { directory.VerifyLocator();RequireKernel(directory.NativeDescriptor,true); }
            // Held descriptor path survives replacement of an ancestor name. Immutable nested names and verity files preserve launch inputs.
            result.Root=held.HeldPath;result.EntryPoint=Path.Combine(result.Root,entryPoint);return result;
        }
        catch { result.Dispose();throw; }
    }
    private static void ValidateStaticElf(FileStream file)
    {
        file.Position=0;Span<byte> header=stackalloc byte[64];file.ReadExactly(header);
        int machine=System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch { Architecture.X64=>62,Architecture.Arm64=>183,_=>-1 };
        if(!header[..4].SequenceEqual(new byte[]{127,69,76,70}) || header[4]!=2 || header[5]!=1 || header[6]!=1
            || System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header[16..])!=2
            || System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header[18..])!=machine)
            throw new IOException("External.AcceptedWholeImageNativeLoaderClosureV1: only the selected static ELF kernel-load protocol is implemented; scripts/.NET/dynamic loaders require their selected complete closure protocol.");
        ulong entry=System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(header[24..]);
        ulong offset=System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(header[32..]);int size=System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header[54..]),count=System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header[56..]);
        if(file.Length is <64 or >1024L*1024*1024 || System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header[20..])!=1
            || System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header[52..])!=64
            || size!=56 || count is <1 or >128 || offset<64 || offset>(ulong)file.Length || (ulong)(size*count)>(ulong)file.Length-offset) throw new IOException("Bounded exact ELF load map required.");
        file.Position=(long)offset;Span<byte> program=stackalloc byte[56];var loads=new List<(ulong Start,ulong End)>();bool executableEntry=false;
        for(int index=0;index<count;index++)
        {
            file.ReadExactly(program);uint type=System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(program);
            if(type is 2 or 3) throw new IOException("External.AcceptedWholeImageNativeLoaderClosureV1: selected dynamic interpreter/runtime input protocol is unavailable.");
            if(type!=1) continue;
            uint flags=System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(program[4..]);
            ulong position=System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(program[8..]),address=System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(program[16..]),
                filesz=System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(program[32..]),memsz=System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(program[40..]),alignment=System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(program[48..]);
            if((flags&~7u)!=0 || memsz==0 || filesz>memsz || position>(ulong)file.Length || filesz>(ulong)file.Length-position
                || address>long.MaxValue || memsz>long.MaxValue-address || address%(ulong)Environment.SystemPageSize!=position%(ulong)Environment.SystemPageSize
                || alignment>1 && ((alignment&(alignment-1))!=0 || address%alignment!=position%alignment)
                || loads.Any(other=>address<other.End && other.Start<address+memsz)) throw new IOException("Incoherent bounded ELF load segment.");
            loads.Add((address,address+memsz));if((flags&1)!=0 && address<=entry && entry-address<filesz) executableEntry=true;
        }
        if(loads.Count==0 || !executableEntry) throw new IOException("Executable file-backed ELF entrypoint required.");
    }
    public void Dispose() { IsDisposed=true;foreach(var file in _files) file.Dispose();foreach(var directory in _directories) directory.Dispose();_files.Clear();_directories.Clear(); }
    [DllImport("libc",EntryPoint="ioctl",SetLastError=true)] private static extern int GetFlags(int fd,uint request,out int flags);
    [DllImport("libc",EntryPoint="faccessat",SetLastError=true)] private static extern int AccessAt(int fd,string path,int mode,int flags);
}
