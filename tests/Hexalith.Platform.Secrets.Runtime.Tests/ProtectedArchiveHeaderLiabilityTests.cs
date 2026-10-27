using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.Platform.Tests;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class ProtectedArchiveHeaderLiabilityTests
{
    [Theory]
    [InlineData("Reserved",0)][InlineData("Reserved",1)][InlineData("Reserved",2)]
    [InlineData("Preparing",0)][InlineData("Preparing",1)][InlineData("Preparing",2)]
    [InlineData("Published",0)][InlineData("Published",1)][InlineData("Published",2)]
    [InlineData("Free",0)][InlineData("Free",1)][InlineData("Free",2)]
    public async Task MissingInitializedBanksCannotReassignAnAdmittedTicket(string state,int damage)
    {
        string root=Path.Combine(Path.GetTempPath(),"protected-header-liability-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var config=ProtectedArchiveFixture.Enroll(root,[root],65536);string path=Path.Combine(root,"exact-original");var token=TestContext.Current.CancellationToken;
            await ArchiveVolumeFence.RunAsync(root,_=>
            {
                using var store=new ProtectedArchiveStore(config);store.Reserve("A",65536);
                if(state is "Published" or "Preparing") store.Publish("A",path,"exact admitted original"u8);
                if(state=="Free") store.ReleaseUnused("A");return Task.FromResult(true);
            },token);
            string file=Path.Combine(root,"allocation-data.bin");
            using(var output=new FileStream(file,FileMode.Open,FileAccess.ReadWrite))
            {
                var banks=new byte[2][];var generations=new long[2];
                for(int copy=0;copy<2;copy++)
                {
                    banks[copy]=new byte[8192];output.Position=8192+copy*8192;output.ReadExactly(banks[copy]);
                    var frame=JsonNode.Parse(banks[copy].AsSpan(40,BinaryPrimitives.ReadInt32LittleEndian(banks[copy])))!;generations[copy]=frame["Generation"]!.GetValue<long>();
                    if(state=="Preparing" && frame["State"]!.GetValue<string>()=="Published")
                    {
                        frame["State"]="Preparing";byte[] raw=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(frame,CustodyBindingContract.Json);Array.Clear(banks[copy]);BinaryPrimitives.WriteInt32LittleEndian(banks[copy],raw.Length);SHA256.HashData(raw).CopyTo(banks[copy],8);raw.CopyTo(banks[copy],40);
                        output.Position=8192+copy*8192;output.Write(banks[copy]);
                    }
                }
                int newest=generations[0]>generations[1]?0:1;
                foreach(int copy in damage==2?new[]{0,1}:new[]{damage==0?newest:1-newest}) { output.Position=8192+copy*8192;output.Write(new byte[8192]); }
                output.Flush(true);
            }
            byte[] damaged=File.ReadAllBytes(file);
            await Should.ThrowAsync<IOException>(()=>ArchiveVolumeFence.RunAsync(root,_=> { using var store=new ProtectedArchiveStore(config);store.Reserve("B",65536);return Task.FromResult(true); },token));
            File.ReadAllBytes(file).ShouldBe(damaged);
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task IndependentlyInitializedUnusedFramesReservePublishAndReopenExactly()
    {
        string root=Path.Combine(Path.GetTempPath(),"protected-initial-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var config=ProtectedArchiveFixture.Enroll(root,[root],131072);string path=Path.Combine(root,"uncreated","original");
            await ArchiveVolumeFence.RunAsync(root,_=> { using var store=new ProtectedArchiveStore(config);store.Reserve("A",65536);store.Publish("A",path,"original"u8);return Task.FromResult(true); },TestContext.Current.CancellationToken);
            using var reopened=new ProtectedArchiveStore(config);reopened.Read(path,65536).ShouldBe("original"u8.ToArray());reopened.ReservedBytes("A").ShouldBe(65536);File.Exists(path).ShouldBeFalse();
            // Both missing metadata banks cannot become the old external genesis after use.
            foreach(string name in new[]{"allocation-metadata-0.bin","allocation-metadata-1.bin"}) using(var output=new FileStream(Path.Combine(root,name),FileMode.Open,FileAccess.ReadWrite)) { output.Write(new byte[8192]);output.Flush(true); }
            Should.Throw<IOException>(()=>reopened.ReadMetadata());
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task CancellationAtTheExistingAllocationBoundaryCreatesNoDirectoryOrOriginal()
    {
        string root=Path.Combine(Path.GetTempPath(),"cancel-archive-traversal-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            string path=Path.Combine(root,"not-created","original");using var cancellation=new CancellationTokenSource();int allocations=0;
            HeldArchiveDirectory.AllocationAttempt=_=> { allocations++;cancellation.Cancel(); };
            await Should.ThrowAsync<OperationCanceledException>(()=>ImmutableFileArchive.PublishAsync(path,"original"u8.ToArray(),cancellation.Token));
            allocations.ShouldBe(1);Directory.Exists(Path.GetDirectoryName(path)).ShouldBeFalse();File.Exists(path).ShouldBeFalse();
        }
        finally { HeldArchiveDirectory.AllocationAttempt=null;Directory.Delete(root,true); }
    }
}
