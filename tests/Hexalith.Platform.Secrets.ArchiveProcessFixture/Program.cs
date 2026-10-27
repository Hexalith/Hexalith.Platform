using Hexalith.Platform.Secrets.Runtime;
using System.Security.Cryptography;
using System.Text;

if(args.Length is <2 or >3) return 2;
if(args.Length==3)
{
    if(args[2]!="hold-interrupted-writer") return 2;
    string lockPath=Path.Combine(Path.GetDirectoryName(args[0])!,".publish-"+Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFileName(args[0])))));
    using var lease=await ArchiveFileLease.AcquireAsync(lockPath,CancellationToken.None);
    using var directory=HeldArchiveDirectory.Acquire(Path.GetDirectoryName(args[0])!);
    await using var partial=new FileStream(directory.OpenUnpublishedFile(),FileAccess.ReadWrite);
    await partial.WriteAsync(new byte[]{1,2});await partial.FlushAsync();
    Console.WriteLine("interrupted-writer-ready");
    await Task.Delay(Timeout.Infinite);
    return 2;
}
await ImmutableFileArchive.PublishAsync(args[0],await File.ReadAllBytesAsync(args[1]));
return 0;

/// <summary>Locates the disposable child-process archive fixture, never a production entrypoint.</summary>
public sealed class ArchiveProcessMarker;
