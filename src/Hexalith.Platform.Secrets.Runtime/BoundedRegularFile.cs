using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Acquires exact absolute regular-file bytes through a nonblocking, no-follow descriptor and one total deadline.</summary>
public static class BoundedRegularFile
{
    private static readonly SemaphoreSlim Acquisitions=new(4,4);
    /// <summary>Rejects nonregular/symbolic/oversized or changing files before parsing. Linux immutable files or exact subPath mounts are required.</summary>
    public static Task<byte[]> ReadAsync(string path,int maximumBytes,CancellationToken cancellationToken=default,TimeSpan? timeout=null)
        =>ReadWithAcquisitionAsync(path,maximumBytes,cancellationToken,timeout,null);
    internal static Task<byte[]> ReadWithAcquisitionAsync(string path,int maximumBytes,CancellationToken cancellationToken,TimeSpan? timeout,Action? beforeAcquire,Action? afterInspection=null,Action<byte[],byte[]>? inspectOwnedBuffers=null)
    {
        if(!OperatingSystem.IsLinux() || !Path.IsPathFullyQualified(path) || Path.GetFullPath(path)!=path || path.Length>4096 || maximumBytes is <=0 or >CustodyBindingContract.MaximumOriginalBytes)
            throw new IOException("Exact bounded absolute regular file required.");
        return RunAcquisitionAsync(token=> { beforeAcquire?.Invoke();return ReadCoreAsync(path,maximumBytes,token,afterInspection,inspectOwnedBuffers:inspectOwnedBuffers); },cancellationToken,timeout);
    }
    // Synchronous kernel calls retain their worker and descriptors until return; callers and queued callers have one finite deadline.
    internal static async Task<byte[]> RunAcquisitionAsync(Func<CancellationToken,Task<byte[]>> acquire,CancellationToken cancellationToken,TimeSpan? timeout=null)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(timeout??TimeSpan.FromSeconds(30));var token=deadline.Token;
        await Acquisitions.WaitAsync(token).ConfigureAwait(false);
        var work=Task.Run(async()=> { try { token.ThrowIfCancellationRequested();return await acquire(token).ConfigureAwait(false); } finally { Acquisitions.Release(); } },CancellationToken.None);
        IndependentReceivingOwnership.Current.Value?.Track(work);
        try { return await work.WaitAsync(token).ConfigureAwait(false); }
        catch
        {
            _=work.ContinueWith(task=> { if(task.Status==TaskStatus.RanToCompletion) System.Security.Cryptography.CryptographicOperations.ZeroMemory(task.Result);else _=task.Exception; },CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
            throw;
        }
    }
    /// <summary>Uses bounded UTF-8 text while clearing the owned input and decode buffers after the callback.</summary>
    public static async Task<T> UseUtf8Async<T>(string path,int maximumBytes,Func<string,T> use,CancellationToken token=default)
    {
        byte[] bytes=await ReadAsync(path,maximumBytes,token).ConfigureAwait(false);char[]? chars=null;
        try { chars=System.Text.Encoding.UTF8.GetChars(bytes);token.ThrowIfCancellationRequested();return use(new string(chars).Trim()); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);if(chars is not null) Array.Clear(chars); }
    }
    /// <summary>Consumes private UTF8 through owned clearable buffers. The callback must not retain the span.</summary>
    public static Task<T> UseUtf8SpanAsync<T>(string path,int maximumBytes,Utf8SpanConsumer<T> use,CancellationToken token=default)
        =>UseUtf8SpanWithInspectionAsync(path,maximumBytes,use,token,null);
    internal static async Task<T> UseUtf8SpanWithInspectionAsync<T>(string path,int maximumBytes,Utf8SpanConsumer<T> use,CancellationToken token,Action<byte[],char[]>? inspected,Action? afterInspection=null,Action<byte[],byte[]>? inspectAcquired=null)
    {
        byte[] bytes=await ReadWithAcquisitionAsync(path,maximumBytes,token,null,null,afterInspection,inspectAcquired).ConfigureAwait(false);char[]? chars=null;
        try { chars=new System.Text.UTF8Encoding(false,true).GetChars(bytes);inspected?.Invoke(bytes,chars);token.ThrowIfCancellationRequested();return use(chars.AsSpan().Trim()); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);if(chars is not null) Array.Clear(chars); }
    }
    /// <summary>Missing or ineligible private mounts deny issuance; caller cancellation remains observable.</summary>
    public static async Task<string?> TryUseUtf8SpanAsync(string path,int maximumBytes,Utf8SpanConsumer<string?> use,CancellationToken token=default)
    {
        try { return await UseUtf8SpanAsync(path,maximumBytes,use,token).ConfigureAwait(false); }
        catch(IOException) { return null; }
    }
    /// <summary>Missing or ineligible private mounts deny proof issuance; cancellation remains observable.</summary>
    public static async Task<string?> TryUseUtf8Async(string path,int maximumBytes,Func<string,string?> use,CancellationToken token=default)
    {
        try { return await UseUtf8Async(path,maximumBytes,use,token).ConfigureAwait(false); }
        catch(IOException) { return null; }
    }
    internal static Task<byte[]> ReadLiteralInOwnedWorkerAsync(string path,int maximumBytes,CancellationToken token)=>ReadCoreAsync(path,maximumBytes,token,null,false);
    private static Task<byte[]> ReadCoreAsync(string path,int maximumBytes,CancellationToken token,Action? afterInspection,bool allowProtected=true,Action<byte[],byte[]>? inspectOwnedBuffers=null)
    {
        int fd;
        try { fd=Acquire(path,token); }
        catch(FileNotFoundException) when(allowProtected) { if(ProtectedArchiveStore.ReadEnrolled(path,maximumBytes,token) is { } fixedOriginal) return Task.FromResult(fixedOriginal);throw; }
        // Every path component is opened relative to its held, no-follow parent descriptor.
        if(fd<0) throw new IOException("Regular file acquisition failed.");
        using var handle=new SafeFileHandle((IntPtr)fd,true);
        var before=Inspect(fd);
        if((before.Mode&0xf000)!=0x8000 || before.Size is 0 || before.Size>(ulong)maximumBytes) throw new IOException("Bounded regular file required.");
        afterInspection?.Invoke();
        // Already inside the bounded acquisition worker. Direct positional reads
        // write only caller-owned regions; no FileStream read-ahead copy survives.
        byte[] bytes=new byte[(int)before.Size];byte[] excess=new byte[1];bool transferred=false;
        try
        {
        inspectOwnedBuffers?.Invoke(bytes,excess);
        int used=0;
        while(used<bytes.Length) { token.ThrowIfCancellationRequested();int read=RandomAccess.Read(handle,bytes.AsSpan(used),used);if(read==0) throw new IOException("Regular file changed during acquisition.");used+=read; }
        token.ThrowIfCancellationRequested();if(RandomAccess.Read(handle,excess,bytes.Length)!=0) throw new IOException("Regular file grew beyond its acquired bound.");
        var after=Inspect(fd);token.ThrowIfCancellationRequested();
        if(before.Size!=after.Size || before.Modified!=after.Modified || before.ModifiedNanos!=after.ModifiedNanos || before.Changed!=after.Changed || before.ChangedNanos!=after.ChangedNanos)
            throw new IOException("Regular file changed during acquisition.");
        transferred=true;return Task.FromResult(bytes);
        }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(excess);if(!transferred) System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
    }
    private static int Acquire(string path,CancellationToken token)
    {
        int descriptor=Open("/",0x10000|0x80000);if(descriptor<0) throw new IOException("Exact file root unavailable.");
        using var root=new SafeFileHandle((IntPtr)descriptor,true);SafeFileHandle parent=root;
        try
        {
            var components=path.Split('/',StringSplitOptions.RemoveEmptyEntries);
            for(int index=0;index<components.Length;index++)
            {
                token.ThrowIfCancellationRequested();bool last=index==components.Length-1;
                int next=OpenAt(parent.DangerousGetHandle().ToInt32(),components[index],0x800|0x20000|0x80000|(last?0:0x10000));
                if(next<0) { if(Marshal.GetLastPInvokeError()==2) throw new FileNotFoundException("Exact existing regular file unavailable.");throw new IOException("Symbolic or unavailable file path component."); }
                if(last) return next;
                var held=new SafeFileHandle((IntPtr)next,true);if(parent!=root) parent.Dispose();parent=held;
            }
            throw new IOException("Regular file locator required.");
        }
        finally { if(parent!=root) parent.Dispose(); }
    }
    private static Status Inspect(int descriptor)
    { if(Statx(descriptor,string.Empty,0x1000,0x7ff,out var status)!=0) throw new IOException("Regular file descriptor metadata unavailable.");return status; }
    [DllImport("libc",EntryPoint="open",SetLastError=true)] private static extern int Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path,int flags);
    [DllImport("libc",EntryPoint="openat",SetLastError=true)] private static extern int OpenAt(int parent,[MarshalAs(UnmanagedType.LPUTF8Str)] string path,int flags);
    [DllImport("libc",EntryPoint="statx",SetLastError=true)] private static extern int Statx(int descriptor,[MarshalAs(UnmanagedType.LPUTF8Str)] string path,int flags,uint mask,out Status status);
    [StructLayout(LayoutKind.Explicit,Size=256)] private struct Status
    {
        [FieldOffset(28)] internal ushort Mode;
        [FieldOffset(40)] internal ulong Size;
        [FieldOffset(96)] internal long Changed;
        [FieldOffset(104)] internal uint ChangedNanos;
        [FieldOffset(112)] internal long Modified;
        [FieldOffset(120)] internal uint ModifiedNanos;
    }
}
