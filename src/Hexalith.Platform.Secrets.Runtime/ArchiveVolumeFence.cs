using System.Runtime.InteropServices;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>All supported archive allocation shares a live, enrolled filesystem-root fence, including nested publication.</summary>
public static class ArchiveVolumeFence
{
    private static readonly AsyncLocal<Scope?> Current=new();
    private static readonly AsyncLocal<Func<string,string>?> NamespaceRoot=new();
    internal static Func<string,string>? NamespaceRootOverride { get=>NamespaceRoot.Value;set=>NamespaceRoot.Value=value; }
    private static string MountedRoot(string directory)
    {
        if(NamespaceRoot.Value is { } fixture) return fixture(directory);
        string? selected=null;int longest=-1,lines=0;
        using var input=new StreamReader("/proc/self/mountinfo");
        while(input.ReadLine() is { } line)
        {
            BoundedDurableFileOperation.CurrentToken.ThrowIfCancellationRequested();
            if(++lines>65536 || line.Length>65536) throw new IOException("Bounded archive mount namespace required.");
            var fields=line.Split(' ');if(fields.Length<7) throw new IOException("Archive mount namespace unavailable.");
            string point=fields[4].Replace("\\040"," ").Replace("\\011","\t").Replace("\\012","\n").Replace("\\134","\\");
            if((directory==point || point=="/" || directory.StartsWith(point.TrimEnd('/')+"/",StringComparison.Ordinal)) && point.Length>longest)
            { longest=point.Length;selected=fields[3]; }
        }
        return selected??throw new IOException("Archive mount namespace unavailable.");
    }
    private static void RequireCanonicalNamespace(string directory)
    {
        // A full-filesystem root has one physical root inode even through full-root aliases.
        // Subtree binds expose another inode as a pathname root and cannot establish this fence.
        if(MountedRoot(directory)!="/") throw new IOException("External.CanonicalEnrolledArchiveVolumeFenceNamespace: subtree aliases require the independently enrolled canonical full-filesystem view.");
    }
    internal static string ResolveRoot(string directory,Func<string,string?> identity,CancellationToken token)
    {
        string root=directory,mount=identity(root)??throw new IOException("Persistent archive filesystem required.");
        while(Path.GetDirectoryName(root) is { } parent && parent!=root)
        {
            token.ThrowIfCancellationRequested();string? parentIdentity=identity(parent);
            if(parentIdentity is null || parentIdentity!=mount) break;root=parent;
        }
        return root;
    }
    /// <summary>Reads the actual root device/inode identity for independent enrollment; it creates no enrollment.</summary>
    public static Task<string> ReadIdentityAsync(string archiveDirectory,CancellationToken token)
        =>BoundedDurableFileOperation.RunAsync(ct=>
        {
            RequireCanonicalNamespace(archiveDirectory);
            string root=ResolveRoot(archiveDirectory,ArchiveFileLease.FilesystemIdentityOrNull,ct);
            using var held=HeldArchiveDirectory.Acquire(root);held.VerifyLocator();return Task.FromResult(held.Identity);
        },token);
    /// <summary>Acquires or borrows an opaque live fence, verifying its actual current destination-root identity.</summary>
    public static Task<IDisposable> AcquireAsync(string archiveDirectory,CancellationToken token,string? expectedIdentity=null)
        =>BoundedDurableFileOperation.RunAsync(async ct=>
        {
            RequireCanonicalNamespace(archiveDirectory);
            string root=ResolveRoot(archiveDirectory,ArchiveFileLease.FilesystemIdentityOrNull,ct);
            var held=HeldArchiveDirectory.Acquire(root);
            try
            {
                if(expectedIdentity is not null && held.Identity!=expectedIdentity) throw new IOException("Enrolled shared volume fence identity changed.");
                for(var scope=Current.Value;scope is not null;scope=scope.Previous)
                    if(scope.Owner.Identity==held.Identity && scope.Owner.TryRetain())
                    {
                        var borrowed=new Borrowed(scope.Owner);
                        try { held.VerifyLocator();ct.ThrowIfCancellationRequested();held.Dispose();return (IDisposable)borrowed; }
                        catch { borrowed.Dispose();throw; }
                    }
                while(Flock(held.Descriptor,2|4)!=0)
                { if(Marshal.GetLastPInvokeError() is not (11 or 4)) throw new IOException("Shared archive volume fence unavailable.");await Task.Delay(10,ct).ConfigureAwait(false); }
                held.VerifyLocator();ct.ThrowIfCancellationRequested();return (IDisposable)new Lease(held);
            }
            catch { held.Dispose();throw; }
        },token);
    /// <summary>Retains verified ownership through the actual callback and cleanup, including work outliving caller cancellation.</summary>
    public static Task<T> RunAsync<T>(string directory,Func<CancellationToken,Task<T>> action,CancellationToken token,string? expectedIdentity=null)
        =>RunWithLeaseAsync(directory,(_,ct)=>action(ct),token,expectedIdentity);
    internal static Task<T> RunWithLeaseAsync<T>(string directory,Func<IDisposable,CancellationToken,Task<T>> action,CancellationToken token,string? expectedIdentity=null)
        =>BoundedDurableFileOperation.RunAsync(async ct=>
        {
            using var lease=await AcquireAsync(directory,ct,expectedIdentity).ConfigureAwait(false);
            var owner=lease is Lease direct?direct:((Borrowed)lease).Owner;
            var previous=Current.Value;Current.Value=new(owner,previous);
            try { return await action(lease,ct).ConfigureAwait(false); }
            finally { Current.Value=previous; }
        },token);
    // Hold a no-follow existing ancestor before any descendant/lock/temp creation. Unsupported filesystems
    // retain the generic caller's existing behavior and cannot host an enrolled physical completion pool.
    /// <summary>Fences allocating publication from a held existing ancestor through actual cleanup; preserves non-pool filesystem behavior.</summary>
    public static Task<T> RunForPublicationAsync<T>(string directory,Func<CancellationToken,Task<T>> action,CancellationToken token)
        =>BoundedDurableFileOperation.RunAsync(async ct=>
        {
            using var ancestor=HeldArchiveDirectory.AcquireExistingAncestor(directory);
            ancestor.VerifyLocator();
            if(ArchiveFileLease.FilesystemIdentityOrNull(ancestor.PathName) is null) return await action(ct).ConfigureAwait(false);
            return await RunAsync(ancestor.PathName,async ownedToken=>
            {
                ancestor.VerifyLocator();T result=await action(ownedToken).ConfigureAwait(false);ancestor.VerifyLocator();return result;
            },ct).ConfigureAwait(false);
        },token);
    /// <summary>Checks that a persistent destination is protected by the actual live held volume capability.</summary>
    public static void RequireCurrentDestination(string directory)
    {
        string? filesystem=ArchiveFileLease.FilesystemIdentityOrNull(directory);if(filesystem is null) return;
        RequireCanonicalNamespace(directory);
        string root=ResolveRoot(directory,ArchiveFileLease.FilesystemIdentityOrNull,BoundedDurableFileOperation.CurrentToken);
        using var held=HeldArchiveDirectory.Acquire(root);held.VerifyLocator();
        for(var scope=Current.Value;scope is not null;scope=scope.Previous)
            if(scope.Owner.Identity==held.Identity && scope.Owner.IsAlive) return;
        throw new IOException("Actual destination volume differs from the live publication fence.");
    }
    private sealed record Scope(Lease Owner,Scope? Previous);
    private sealed class Lease(HeldArchiveDirectory directory):IDisposable
    {
        private int _references=1,_disposed;
        internal string Identity { get; }=directory.Identity;
        internal bool IsAlive=>Volatile.Read(ref _references)>0;
        internal bool TryRetain()
        {
            int count;
            do { count=Volatile.Read(ref _references);if(count<=0) return false; }
            while(Interlocked.CompareExchange(ref _references,count+1,count)!=count);
            return true;
        }
        internal void Release() { if(Interlocked.Decrement(ref _references)==0) directory.Dispose(); }
        public void Dispose() { if(Interlocked.Exchange(ref _disposed,1)==0) Release(); }
    }
    private sealed class Borrowed(Lease owner):IDisposable
    {
        private int _disposed;
        internal Lease Owner=>owner;
        public void Dispose() { if(Interlocked.Exchange(ref _disposed,1)==0) owner.Release(); }
    }
    [DllImport("libc",EntryPoint="flock",SetLastError=true)] private static extern int Flock(int descriptor,int operation);
}
