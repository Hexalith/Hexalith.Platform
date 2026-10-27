using System.Security.Cryptography;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Publishes flushed originals through a held enrolled directory and permanent cross-process fence, under one finite caller budget.</summary>
public static class ImmutableFileArchive
{
    /// <summary>Timeout leaves kernel work and its bytes/fence owned; exact retry recovers any late original without advancing caller authority.</summary>
    public static Task PublishAsync(string path,ReadOnlyMemory<byte> bytes,CancellationToken token=default,Action? beforeWrite=null)
        =>PublishCoreAsync(path,bytes,token,beforeWrite,null,null);
    internal static Task PublishWithHooksAsync(string path,ReadOnlyMemory<byte> bytes,CancellationToken token,Action? beforeAcquire=null,Action? beforeDurability=null)
        =>PublishCoreAsync(path,bytes,token,null,beforeAcquire,beforeDurability);
    private static async Task PublishCoreAsync(string path,ReadOnlyMemory<byte> bytes,CancellationToken token,Action? beforeWrite,Action? beforeAcquire,Action? beforeDurability)
    {
        if(!Path.IsPathFullyQualified(path) || Path.GetFullPath(path)!=path || bytes.Length is 0 or >CustodyBindingContract.MaximumOriginalBytes) throw new ArgumentException("Bounded absolute immutable original required.");
        // Worker capacity bounds allocations; the owned copy is complete before a cancellable worker handoff.
        await BoundedDurableFileOperation.RunWithInputAsync(bytes,async (owned,ct)=>
        {
                beforeAcquire?.Invoke();ct.ThrowIfCancellationRequested();
                return await ArchiveVolumeFence.RunForPublicationAsync(Path.GetDirectoryName(path)!,async volumeToken=>
                {
                ct=volumeToken;
                using var protectedStore=ProtectedArchiveStore.Find(path,ct);
                if(protectedStore is not null)
                {
                    byte[]? legacyOrRetained=protectedStore.Read(path,owned.Length);
                    if(legacyOrRetained is not null) { if(!legacyOrRetained.AsSpan().SequenceEqual(owned)) throw new IOException("Conflicting exact legacy/protected immutable original.");return true; }
                    string ticket="ordinary:"+Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path)));
                    protectedStore.Reserve(ticket,owned.Length+65536L);beforeWrite?.Invoke();ct.ThrowIfCancellationRequested();protectedStore.Publish(ticket,path,owned);beforeDurability?.Invoke();return true;
                }
                using var directory=HeldArchiveDirectory.Acquire(Path.GetDirectoryName(path)!,true);
                ArchiveVolumeFence.RequireCurrentDestination(Path.GetDirectoryName(path)!);
                string name=Path.GetFileName(path);
                using var ownership=await ArchiveFileLease.AcquirePublicationAsync(directory,name,ct).ConfigureAwait(false);
                directory.VerifyLocator();
                byte[]? existing=directory.Read(name,owned.Length);
                if(existing is not null)
                { if(!existing.AsSpan().SequenceEqual(owned)) throw new IOException("Conflicting immutable original.");directory.Flush();directory.VerifyLocator();ct.ThrowIfCancellationRequested();return true; }
                    beforeWrite?.Invoke();ct.ThrowIfCancellationRequested();directory.VerifyLocator();
                    using var output=new FileStream(directory.OpenUnpublishedFile(),FileAccess.ReadWrite);
                    await output.WriteAsync(owned,ct).ConfigureAwait(false);await output.FlushAsync(ct).ConfigureAwait(false);output.Flush(true);
                    ct.ThrowIfCancellationRequested();directory.VerifyLocator();directory.PublishUnpublishedFile(output.SafeFileHandle,name);beforeDurability?.Invoke();directory.Flush();directory.VerifyLocator();ct.ThrowIfCancellationRequested();return true;
                },ct).ConfigureAwait(false);
        },token).ConfigureAwait(false);
    }
    /// <summary>Flushes only a descriptor acquired without following any symbolic component.</summary>
    public static void FlushDirectory(string directory)
    { using var held=HeldArchiveDirectory.Acquire(directory);held.Flush();held.VerifyLocator(); }
}
