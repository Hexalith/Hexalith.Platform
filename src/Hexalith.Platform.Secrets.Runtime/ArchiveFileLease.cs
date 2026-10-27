using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Linux local-filesystem cross-process fencing on one permanent, never-unlinked regular inode.</summary>
public sealed class ArchiveFileLease:IDisposable
{
    private readonly SafeFileHandle _handle;
    private readonly ArchiveFileLease? _legacy;
    private ArchiveFileLease(SafeFileHandle handle,ArchiveFileLease? legacy=null) { _handle=handle;_legacy=legacy; }
    /// <summary>Acquires an exact inode within the caller's finite budget. Unsupported/network filesystem semantics refuse.</summary>
    public static async Task<ArchiveFileLease> AcquireAsync(string path,CancellationToken token)
    {
        return await ArchiveVolumeFence.RunForPublicationAsync(Path.GetDirectoryName(path)!,async ct=>
        {
            using var directory=HeldArchiveDirectory.Acquire(Path.GetDirectoryName(path)!);ArchiveVolumeFence.RequireCurrentDestination(Path.GetDirectoryName(path)!);
            var lease=await AcquireAtAsync(directory,Path.GetFileName(path),ct).ConfigureAwait(false);
            try { directory.VerifyLocator();ct.ThrowIfCancellationRequested();return lease; } catch { lease.Dispose();throw; }
        },token).ConfigureAwait(false);
    }
    /// <summary>Acquires the permanent inode relative to the held enrolled directory; caller owns the total durable-operation budget.</summary>
    public static async Task<ArchiveFileLease> AcquireAtAsync(HeldArchiveDirectory directory,string name,CancellationToken token)
        =>await AcquireAtCoreAsync(directory,name,token,0x40).ConfigureAwait(false);
    /// <summary>Opens an already enrolled lock inode; protected completion cannot allocate a missing replacement.</summary>
    public static Task<ArchiveFileLease> AcquireExistingAtAsync(HeldArchiveDirectory directory,string name,CancellationToken token)=>AcquireAtCoreAsync(directory,name,token,0);
    /// <summary>Fences every final name without allocating a lock name in the immutable namespace.</summary>
    public static async Task<ArchiveFileLease> AcquirePublicationAsync(HeldArchiveDirectory directory,string name,CancellationToken token)
    {
        int fd=Open(directory.HeldPath,0x10000|0x80000,0);
        if(fd<0) throw new IOException("Archive publication directory unavailable.");
        var handle=new SafeFileHandle((IntPtr)fd,true);
        try
        {
            while(Flock(fd,2|4)!=0)
            { if(Marshal.GetLastPInvokeError() is not (11 or 4)) throw new IOException("Atomic archive fencing unsupported.");await Task.Delay(10,token).ConfigureAwait(false); }
            token.ThrowIfCancellationRequested();directory.VerifyLocator();
            // Old permanent per-file locks remain authoritative. Never create, unlink or rewrite them.
            string legacy=".publish-"+Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name)));
            var old=File.Exists(Path.Combine(directory.HeldPath,legacy))?await AcquireExistingAtAsync(directory,legacy,token).ConfigureAwait(false):null;
            return new(handle,old);
        }
        catch { handle.Dispose();throw; }
    }
    private static async Task<ArchiveFileLease> AcquireAtCoreAsync(HeldArchiveDirectory directory,string name,CancellationToken token,int flags)
    {
        var handle=directory.OpenFile(name,flags);int fd=handle.DangerousGetHandle().ToInt32();
        try
        {
            string identity=ArchiveAllocationProof.Identity(handle);
            if(Statx(fd,string.Empty,0x1000,0x7ff,out var status)!=0 || (status.Mode&0xf000)!=0x8000 || Fstatfs(fd,out var fs)!=0
                || fs.Type is not (0xef53 or 0x58465342 or 0x9123683e)) throw new IOException("External.PersistentAtomicLocalArchiveFencingAndCapacityProtocol");
            while(Flock(fd,2|4)!=0)
            { if(Marshal.GetLastPInvokeError() is not (11 or 4)) throw new IOException("Atomic archive fencing unsupported.");await Task.Delay(10,token).ConfigureAwait(false); }
            token.ThrowIfCancellationRequested();if(ArchiveAllocationProof.Identity(handle)!=identity) throw new IOException("Archive lock ownership changed.");return new(handle);
        }
        catch { handle.Dispose();throw; }
    }
    /// <summary>Identifies the held local filesystem used by a capacity pool and all of its enrolled archive roots.</summary>
    public static string FilesystemIdentity(string directory)=>FilesystemIdentityOrNull(directory)??throw new IOException("External.IndependentlyEnrolledPersistentArchiveBackingAndReplicationProtocol");
    internal static string? FilesystemIdentityOrNull(string directory)
        =>BoundedDurableFileOperation.RunAsync(_=>
        {
            using var held=HeldArchiveDirectory.Acquire(directory);
            if(Fstatfs(held.Descriptor,out var fs)!=0) throw new IOException("Archive filesystem identity unavailable.");
            held.VerifyLocator();return Task.FromResult<string?>(fs.Type is 0xef53 or 0x58465342 or 0x9123683e?fs.Type+":"+fs.Identity:null);
        }).GetAwaiter().GetResult();
    /// <inheritdoc />
    public void Dispose() { _legacy?.Dispose();_handle.Dispose(); }
    [DllImport("libc",EntryPoint="open",SetLastError=true)] private static extern int Open(string path,int flags,int mode);
    [DllImport("libc",EntryPoint="flock",SetLastError=true)] private static extern int Flock(int descriptor,int operation);
    [DllImport("libc",EntryPoint="fstatfs",SetLastError=true)] private static extern int Fstatfs(int descriptor,out FileSystem status);
    [DllImport("libc",EntryPoint="statx",SetLastError=true)] private static extern int Statx(int descriptor,string path,int flags,uint mask,out Status status);
    [StructLayout(LayoutKind.Explicit,Size=256)] private struct Status { [FieldOffset(28)] internal ushort Mode; }
    [StructLayout(LayoutKind.Explicit,Size=120)] private struct FileSystem { [FieldOffset(0)] internal long Type; [FieldOffset(56)] internal ulong Identity; }
}
