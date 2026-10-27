using System.Security.Cryptography;
using System.Text;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class ArchiveNamespaceOwnershipTests
{
    [Theory]
    [InlineData("read")][InlineData("open")][InlineData("delete")][InlineData("rename-source")][InlineData("rename-final")][InlineData("link-final")]
    public void NativeRelativeNamesRejectNulBeforeAnyNativeOperation(string operation)
    {
        string root=Path.Combine(Path.GetTempPath(),"nul-original-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            byte[] original="accepted immutable original"u8.ToArray();File.WriteAllBytes(Path.Combine(root,"accepted-original.json"),original);
            using var held=HeldArchiveDirectory.Acquire(root);string bad="accepted-original.json\0.pending";
            using var unnamed=held.OpenUnpublishedFile();
            Should.Throw<IOException>(()=>
            {
                switch(operation)
                {
                    case "read":held.Read(bad,1024);break;
                    case "open":using(held.OpenFile(bad,1)) {} break;
                    case "delete":held.DeleteTemporary(bad);break;
                    case "rename-source":held.Publish(bad,"changed");break;
                    case "rename-final":held.Publish("accepted-original.json",bad);break;
                    case "link-final":held.PublishUnpublishedFile(unnamed,bad);break;
                }
            });
            held.Read("accepted-original.json",1024).ShouldBe(original);Directory.GetFiles(root).Length.ShouldBe(1);
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task ActualUnmarkedWritersNeverDeleteOrConfuseAcceptedPendingProbeOrLockNames()
    {
        string root=Path.Combine(Path.GetTempPath(),"owned-stage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            string name="original",lockName=".publish-"+Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(name)));
            var names=new[]{"original.pending",lockName,".write-probe-accepted",name,"other",".publish-"+Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("other")))};
            foreach(string value in names) await ImmutableFileArchive.PublishAsync(Path.Combine(root,value),Encoding.UTF8.GetBytes(value),TestContext.Current.CancellationToken);
            // Independent restart opens preserve both legacy-looking bytes and ordinary exact retries.
            await new IndependentArchiveStorage().ProbeWritableAsync(root,TestContext.Current.CancellationToken);
            await Task.WhenAll(names.Select(value=>ImmutableFileArchive.PublishAsync(Path.Combine(root,value),Encoding.UTF8.GetBytes(value),TestContext.Current.CancellationToken)));
            await Task.WhenAll(new[]{"simultaneous","simultaneous.pending"}.Select(value=>ImmutableFileArchive.PublishAsync(Path.Combine(root,value),Encoding.UTF8.GetBytes(value),TestContext.Current.CancellationToken)));
            names=[..names,"simultaneous","simultaneous.pending"];
            foreach(string value in names) File.ReadAllText(Path.Combine(root,value)).ShouldBe(value);
            await Should.ThrowAsync<IOException>(()=>ImmutableFileArchive.PublishAsync(Path.Combine(root,name),"conflict"u8.ToArray(),TestContext.Current.CancellationToken));
            Directory.GetFiles(root).Select(Path.GetFileName).Order().ShouldBe(names.Order());
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task PreexistingAmbiguousLegacyCandidateSurvivesPublicationAndLostAcknowledgement()
    {
        string root=Path.Combine(Path.GetTempPath(),"legacy-stage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            string path=Path.Combine(root,"legacy");byte[] legacy="ambiguous historical pending bytes"u8.ToArray();File.WriteAllBytes(path+".pending",legacy);
            await Should.ThrowAsync<IOException>(()=>ImmutableFileArchive.PublishWithHooksAsync(path,"retained"u8.ToArray(),TestContext.Current.CancellationToken,beforeDurability:()=>throw new IOException("lost acknowledgement")));
            await ImmutableFileArchive.PublishAsync(path,"retained"u8.ToArray(),TestContext.Current.CancellationToken);
            File.ReadAllBytes(path+".pending").ShouldBe(legacy);File.ReadAllText(path).ShouldBe("retained");
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task NamedMetadataCleanupRequiresItsOwnExclusiveCreationAndPreservesAmbiguousLegacyBytes()
    {
        string root=Path.Combine(Path.GetTempPath(),"owned-metadata-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            byte[] legacy="unknown original identity"u8.ToArray();File.WriteAllBytes(Path.Combine(root,"capacity.json.pending"),legacy);
            await ArchiveVolumeFence.RunAsync(root,_=>
            {
                using var directory=HeldArchiveDirectory.Acquire(root);
                Should.Throw<IOException>(()=>directory.CreateUnpublishedFile("capacity.json.pending"));
                Should.Throw<IOException>(()=>directory.DeleteTemporary("capacity.json.pending"));
                using(var owned=directory.CreateUnpublishedFile("fresh.pending")) { owned.Stream.WriteByte(1);owned.Stream.Flush(true); }
                File.Exists(Path.Combine(root,"fresh.pending")).ShouldBeFalse();
                using(var owned=directory.CreateUnpublishedFile("published.pending")) { owned.Stream.WriteByte(2);owned.Stream.Flush(true);owned.Publish("metadata"); }
                File.ReadAllBytes(Path.Combine(root,"metadata")).ShouldBe(new byte[]{2});return Task.FromResult(true);
            },TestContext.Current.CancellationToken);
            File.ReadAllBytes(Path.Combine(root,"capacity.json.pending")).ShouldBe(legacy);
        }
        finally { Directory.Delete(root,true); }
    }

}
