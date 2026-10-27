using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Held Linux directory identity; all mutation is relative to its no-follow descriptor.</summary>
public sealed class HeldArchiveDirectory:IDisposable
{
    private static readonly AsyncLocal<Action<string>?> Allocations=new();
    internal static Action<string>? AllocationAttempt { get=>Allocations.Value;set=>Allocations.Value=value; }
    private readonly SafeFileHandle _handle;
    private readonly Status _identity;
    private readonly string _path;
    /// <summary>Actual device and inode; independent storage enrollment pins this identity across participating namespaces.</summary>
    public string Identity=>_identity.DeviceMajor+":"+_identity.DeviceMinor+":"+_identity.Inode;
    internal string PathName=>_path;
    /// <summary>Held public directory descriptor for immutable launch namespace validation; owns no credential.</summary>
    public int NativeDescriptor=>Descriptor;
    /// <summary>Process-anchored existing directory path valid only while this lease remains alive.</summary>
    public string HeldPath=>"/proc/"+Environment.ProcessId+"/fd/"+Descriptor;
    internal int Descriptor=>_handle.DangerousGetHandle().ToInt32();
    private HeldArchiveDirectory(string path,SafeFileHandle handle) { _path=path;_handle=handle;_identity=Inspect(Descriptor); }
    /// <summary>Acquires every component through its held parent; optional creation flushes both parent and child.</summary>
    public static HeldArchiveDirectory Acquire(string path,bool create=false)
    {
        var token=BoundedDurableFileOperation.CurrentToken;token.ThrowIfCancellationRequested();
        if(!OperatingSystem.IsLinux() || !Path.IsPathFullyQualified(path) || Path.GetFullPath(path)!=path || path.Length>4096) throw new IOException("Exact Linux archive directory required.");
        int descriptor=Open("/",0x10000|0x80000,0);if(descriptor<0) throw new IOException("Archive root unavailable.");
        SafeFileHandle? parent=new((IntPtr)descriptor,true);
        try
        {
            token.ThrowIfCancellationRequested();
            foreach(string component in path.Split('/',StringSplitOptions.RemoveEmptyEntries))
            {
                token.ThrowIfCancellationRequested();
                int next=OpenAt(parent.DangerousGetHandle().ToInt32(),component,0x10000|0x20000|0x80000,0);
                using var acquired=next>=0?new SafeFileHandle((IntPtr)next,true):null;
                int error=next<0?Marshal.GetLastPInvokeError():0;token.ThrowIfCancellationRequested();
                SafeFileHandle? child=null;
                try
                {
                    if(next<0 && create && error==2)
                    {
                        Allocations.Value?.Invoke(Path.Combine(path,component));token.ThrowIfCancellationRequested();
                        if(MkdirAt(parent.DangerousGetHandle().ToInt32(),component,0x1c0)!=0 && Marshal.GetLastPInvokeError()!=17) throw new IOException("Archive directory creation failed.");
                        token.ThrowIfCancellationRequested();Flush(parent.DangerousGetHandle().ToInt32());token.ThrowIfCancellationRequested();
                        next=OpenAt(parent.DangerousGetHandle().ToInt32(),component,0x10000|0x20000|0x80000,0);
                        if(next>=0) child=new SafeFileHandle((IntPtr)next,true);
                        token.ThrowIfCancellationRequested();if(child is not null) { Flush(next);token.ThrowIfCancellationRequested(); }
                    }
                    else if(acquired is not null) { child=acquired; }
                    if(child is null) throw new IOException("Archive symbolic or unavailable directory component.");
                    // Transfer an acquired descriptor immediately; every cancellation path retains a cleanup owner.
                    var transferred=new SafeFileHandle(child.DangerousGetHandle(),true);child.SetHandleAsInvalid();parent.Dispose();parent=transferred;
                }
                finally { child?.Dispose(); }
            }
            token.ThrowIfCancellationRequested();var held=new HeldArchiveDirectory(path,parent);parent=null;return held;
        }
        finally { parent?.Dispose(); }
    }
    internal static HeldArchiveDirectory AcquireExistingAncestor(string path)
    {
        if(!OperatingSystem.IsLinux() || !Path.IsPathFullyQualified(path) || Path.GetFullPath(path)!=path || path.Length>4096) throw new IOException("Exact Linux archive directory required.");
        int descriptor=Open("/",0x10000|0x80000,0);if(descriptor<0) throw new IOException("Archive root unavailable.");
        SafeFileHandle? parent=new((IntPtr)descriptor,true);string current="/";
        try
        {
            foreach(string component in path.Split('/',StringSplitOptions.RemoveEmptyEntries))
            {
                BoundedDurableFileOperation.CurrentToken.ThrowIfCancellationRequested();
                int next=OpenAt(parent.DangerousGetHandle().ToInt32(),component,0x10000|0x20000|0x80000,0);
                if(next<0)
                {
                    if(Marshal.GetLastPInvokeError()==2) break;
                    throw new IOException("Archive symbolic or unavailable ancestor component.");
                }
                parent.Dispose();parent=new((IntPtr)next,true);current=Path.Combine(current,component);
            }
            var held=new HeldArchiveDirectory(current,parent);parent=null;return held;
        }
        finally { parent?.Dispose(); }
    }
    /// <summary>Preserves the explicit read-only mount refusal even for a privileged fixture process.</summary>
    public void RequireWritable()
    { if((_identity.Mode&0x92)==0) throw new IOException("Archive is read-only."); }
    /// <summary>Rejects a renamed or replaced enrolled locator, including symbolic parent substitution.</summary>
    public void VerifyLocator()
    {
        using var current=Acquire(_path);
        if(_identity.Inode!=current._identity.Inode || _identity.DeviceMajor!=current._identity.DeviceMajor || _identity.DeviceMinor!=current._identity.DeviceMinor) throw new IOException("Enrolled archive directory identity changed.");
    }
    /// <summary>Opens a relative no-follow nonblocking inode. No caller supplies a nested path.</summary>
    public SafeFileHandle OpenFile(string name,int flags,int mode=0x180)
    {
        Name(name);int descriptor=-1;
        if((flags&0x40)!=0 && (flags&0x80)==0) descriptor=OpenAt(Descriptor,name,(flags&~0x40)|0x20000|0x800|0x80000,mode);
        if(descriptor<0)
        {
            if((flags&0x40)!=0) Allocations.Value?.Invoke(Path.Combine(_path,name));
            descriptor=OpenAt(Descriptor,name,flags|0x20000|0x800|0x80000,mode);
        }
        if(descriptor<0) throw new IOException("Archive relative file unavailable.");
        var handle=new SafeFileHandle((IntPtr)descriptor,true);
        if((Inspect(descriptor).Mode&0xf000)!=0x8000) { handle.Dispose();throw new IOException("Archive regular inode required."); }
        return handle;
    }
    /// <summary>Allocates an unnamed staging inode owned only by its descriptor. No final filename can alias it.</summary>
    public SafeFileHandle OpenUnpublishedFile()
    {
        Allocations.Value?.Invoke(Path.Combine(_path,"<unnamed>"));
        int fd=OpenAt(Descriptor,".",0x410000|2|0x80000,0x180);
        if(fd<0) throw new IOException("External.UnnamedAtomicArchivePublicationV1");
        return new SafeFileHandle((IntPtr)fd,true);
    }
    /// <summary>Atomically links a flushed unnamed inode. Disposal reclaims it only while it has no final link.</summary>
    public void PublishUnpublishedFile(SafeFileHandle file,string final)
    {
        Name(final);
        var staged=Inspect(file.DangerousGetHandle().ToInt32());
        if(staged.LinkCount!=0 || (staged.Mode&0xf000)!=0x8000 || staged.DeviceMajor!=_identity.DeviceMajor || staged.DeviceMinor!=_identity.DeviceMinor)
            throw new IOException("Exact unnamed same-volume unpublished inode required.");
        string source="/proc/self/fd/"+file.DangerousGetHandle().ToInt32();
        if(LinkAt(-100,source,Descriptor,final,0x400)!=0) throw new IOException("Atomic unnamed archive publication failed.");
    }
    /// <summary>Reads exact existing originals through the anchored descriptor, checking metadata after reading.</summary>
    public byte[]? Read(string name,int maximumBytes)
    {
        Name(name);int fd=OpenAt(Descriptor,name,0x20000|0x800|0x80000,0);
        if(fd<0) { if(Marshal.GetLastPInvokeError()==2) return null;throw new IOException("Archive original acquisition failed."); }
        using var handle=new SafeFileHandle((IntPtr)fd,true);var before=Inspect(fd);
        if((before.Mode&0xf000)!=0x8000 || before.Size==0 || before.Size>(ulong)maximumBytes) throw new IOException("Bounded immutable original required.");
        using var stream=new FileStream(handle,FileAccess.Read);byte[] bytes=new byte[(int)before.Size];stream.ReadExactly(bytes);
        var after=Inspect(fd);
        if(stream.ReadByte()!=-1 || before.Size!=after.Size || before.Modified!=after.Modified || before.ModifiedNanos!=after.ModifiedNanos || before.Changed!=after.Changed || before.ChangedNanos!=after.ChangedNanos) throw new IOException("Immutable original changed.");
        return bytes;
    }
    /// <summary>Unlinks only an unpublished relative temporary, never a permanent lock or original.</summary>
    public void DeleteTemporary(string name)
    { Name(name);throw new IOException("Named temporary ownership is unproven; immutable original preserved."); }
    /// <summary>Exclusive creation grants this capability ownership of one unpublished inode; an existing candidate is preserved.</summary>
    public OwnedArchiveTemporaryFile CreateUnpublishedFile(string name)=>new(this,name);
    internal void DeleteOwnedTemporary(string name,SafeFileHandle owner)
    {
        Name(name);int fd=OpenAt(Descriptor,name,0x20000|0x800|0x80000,0);
        if(fd<0) { if(Marshal.GetLastPInvokeError()==2) return;throw new IOException("Owned temporary identity unavailable."); }
        using var current=new SafeFileHandle((IntPtr)fd,true);var expected=Inspect(owner.DangerousGetHandle().ToInt32());var actual=Inspect(fd);
        if(expected.Inode!=actual.Inode || expected.DeviceMajor!=actual.DeviceMajor || expected.DeviceMinor!=actual.DeviceMinor)
            throw new IOException("Temporary ownership changed; candidate preserved.");
        if(UnlinkAt(Descriptor,name,0)!=0) throw new IOException("Owned unpublished cleanup failed.");
    }
    /// <summary>Publishes relative bytes atomically without following a mutable parent locator.</summary>
    public void Publish(string temporary,string final,bool replace=false)
    { Name(temporary);Name(final);if(RenameAt(Descriptor,temporary,Descriptor,final,replace?0u:1u)!=0) throw new IOException("Atomic archive publication failed."); }
    /// <summary>Flushes the held directory.</summary>
    public void Flush()=>Flush(Descriptor);
    private static void Flush(int fd) { if(Fsync(fd)!=0) throw new IOException("Archive directory flush failed."); }
    private static void Name(string name) { if(name.Length==0 || name is "." or ".." || name.Contains('/') || name.Contains('\0')) throw new IOException("Single archive filename required."); }
    private static Status Inspect(int fd) { if(Statx(fd,"",0x1000,0x7ff,out var s)!=0) throw new IOException("Archive descriptor metadata unavailable.");return s; }
    /// <inheritdoc />
    public void Dispose()=>_handle.Dispose();
    [DllImport("libc",EntryPoint="open",SetLastError=true)] private static extern int Open(string path,int flags,int mode);
    [DllImport("libc",EntryPoint="openat",SetLastError=true)] private static extern int OpenAt(int parent,string path,int flags,int mode);
    [DllImport("libc",EntryPoint="mkdirat",SetLastError=true)] private static extern int MkdirAt(int parent,string path,int mode);
    [DllImport("libc",EntryPoint="unlinkat",SetLastError=true)] private static extern int UnlinkAt(int parent,string path,int flags);
    [DllImport("libc",EntryPoint="renameat2",SetLastError=true)] private static extern int RenameAt(int oldParent,string oldPath,int newParent,string newPath,uint flags);
    [DllImport("libc",EntryPoint="linkat",SetLastError=true)] private static extern int LinkAt(int oldParent,string oldPath,int newParent,string newPath,int flags);
    [DllImport("libc",EntryPoint="fsync",SetLastError=true)] private static extern int Fsync(int fd);
    [DllImport("libc",EntryPoint="statx",SetLastError=true)] private static extern int Statx(int fd,string path,int flags,uint mask,out Status status);
    [StructLayout(LayoutKind.Explicit,Size=256)] private struct Status
    {
        [FieldOffset(16)] internal uint LinkCount;
        [FieldOffset(28)] internal ushort Mode;[FieldOffset(32)] internal ulong Inode;[FieldOffset(40)] internal ulong Size;
        [FieldOffset(96)] internal long Changed;[FieldOffset(104)] internal uint ChangedNanos;[FieldOffset(112)] internal long Modified;[FieldOffset(120)] internal uint ModifiedNanos;
        [FieldOffset(136)] internal uint DeviceMajor;[FieldOffset(140)] internal uint DeviceMinor;
    }
}
