using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Descriptor-bound exclusive inode and private written extent checks for the selected in-place archive protocol.</summary>
public static class ArchiveAllocationProof
{
    private static readonly AsyncLocal<Func<int,byte[],int>?> Probes=new();
    internal static Func<int,byte[],int>? ExtentProbeOverride { get=>Probes.Value;set=>Probes.Value=value; }
    /// <summary>Requires a single-link regular inode and returns its physical identity without reopening a pathname.</summary>
    public static string Identity(SafeFileHandle handle)
    {
        int fd=handle.DangerousGetHandle().ToInt32();
        if(Statx(fd,"",0x1000,0x7ff,out var info)!=0 || (info.Mode&0xf000)!=0x8000 || info.Links!=1)
            throw new IOException("Exclusive single-link physical archive allocation required.");
        return info.Major+":"+info.Minor+":"+info.Inode;
    }
    /// <summary>Checks complete private written allocation. This protocol refuses COW filesystems and needs independently enrolled future snapshot exclusion.</summary>
    public static void RequirePrivateWritten(SafeFileHandle handle,long bytes,string? expectedIdentity=null,CancellationToken token=default)
    {
        string identity=Identity(handle);int fd=handle.DangerousGetHandle().ToInt32();
        if(expectedIdentity is not null && expectedIdentity!=identity) throw new IOException("Enrolled physical allocation inode changed.");
        if(bytes<0 || Fstatfs(fd,out var fs)!=0 || fs.Type!=0xef53)
            throw new IOException("External.Ext4PrivateWrittenInPlaceArchiveStorageProtocol: XFS/Btrfs/COW/snapshot protocols are not enrolled by this source.");
        long covered=0;int total=0;
        while(covered<bytes)
        {
            token.ThrowIfCancellationRequested();BoundedDurableFileOperation.CurrentToken.ThrowIfCancellationRequested();
            byte[] frame=new byte[32+128*56];BitConverter.TryWriteBytes(frame.AsSpan(0),covered);BitConverter.TryWriteBytes(frame.AsSpan(8),bytes-covered);BitConverter.TryWriteBytes(frame.AsSpan(24),128u);
            if((Probes.Value?.Invoke(fd,frame)??Ioctl(fd,0xc020660b,frame))!=0) throw new IOException("External.PrivateWrittenArchiveExtentEvidenceUnavailable");
            uint mapped=BitConverter.ToUInt32(frame,20);if(mapped is <1 or >128) throw new IOException("Sparse or unknown archive allocation.");
            for(int index=0;index<mapped;index++)
            {
                int offset=32+56*index;ulong logical=BitConverter.ToUInt64(frame,offset),physical=BitConverter.ToUInt64(frame,offset+8),length=BitConverter.ToUInt64(frame,offset+16);uint flags=BitConverter.ToUInt32(frame,offset+40);
                if(++total>65536 || physical==0 || length==0 || logical>(ulong)covered || logical+length<=(ulong)covered || (flags&~0x1001u)!=0)
                    throw new IOException("Sparse/shared/unknown/delayed/unwritten archive extents cannot fund completion.");
                covered=checked((long)(logical+length));
                if((flags&1)!=0 && covered<bytes) throw new IOException("Incomplete physical archive allocation.");
            }
        }
        if(Identity(handle)!=identity) throw new IOException("Physical allocation ownership changed.");
    }
    [DllImport("libc",EntryPoint="statx",SetLastError=true)] private static extern int Statx(int fd,string path,int flags,uint mask,out Status status);
    [DllImport("libc",EntryPoint="fstatfs",SetLastError=true)] private static extern int Fstatfs(int fd,out Filesystem status);
    [DllImport("libc",EntryPoint="ioctl",SetLastError=true)] private static extern int Ioctl(int fd,uint request,[In,Out] byte[] frame);
    [StructLayout(LayoutKind.Explicit,Size=120)] private struct Filesystem { [FieldOffset(0)] internal long Type; }
    [StructLayout(LayoutKind.Explicit,Size=256)] private struct Status { [FieldOffset(16)] internal uint Links;[FieldOffset(28)] internal ushort Mode;[FieldOffset(32)] internal ulong Inode;[FieldOffset(136)] internal uint Major;[FieldOffset(140)] internal uint Minor; }
}
