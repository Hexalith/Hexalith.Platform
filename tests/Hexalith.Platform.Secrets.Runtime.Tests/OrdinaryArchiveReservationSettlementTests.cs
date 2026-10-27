using System.Security.Cryptography;
using System.Text;
using Hexalith.Platform.Tests;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class OrdinaryArchiveReservationSettlementTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ExactDurablePublicationAndInterruptedRetryReleaseOnlyUnusedOrdinaryChunks(bool interrupt)
    {
        string root=Path.Combine(Path.GetTempPath(),"ordinary-settlement-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var config=ProtectedArchiveFixture.Enroll(root,[root],3*65536);string first=Path.Combine(root,"first"),second=Path.Combine(root,"second");
            string Ticket(string path)=>"ordinary:"+Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(path)));
            Task<long> Charge(string path)=>ArchiveVolumeFence.RunAsync(root,_=> { using var store=new ProtectedArchiveStore(config);return Task.FromResult(store.ReservedBytes(Ticket(path))); },TestContext.Current.CancellationToken);
            if(interrupt)
            {
                await Should.ThrowAsync<IOException>(()=>ImmutableFileArchive.PublishWithHooksAsync(first,new byte[]{1},TestContext.Current.CancellationToken,beforeDurability:()=>throw new IOException("fixture interrupted after durable publication")));
                (await Charge(first)).ShouldBe(2*65536);
            }
            else await ImmutableFileArchive.PublishAsync(first,new byte[]{1},TestContext.Current.CancellationToken);
            // A mismatching retry must retain the exact original and every remaining liability.
            long before=await Charge(first);
            await Should.ThrowAsync<IOException>(()=>ImmutableFileArchive.PublishAsync(first,new byte[]{9},TestContext.Current.CancellationToken));
            (await Charge(first)).ShouldBe(before);
            await ImmutableFileArchive.PublishAsync(first,new byte[]{1},TestContext.Current.CancellationToken);
            (await Charge(first)).ShouldBe(65536);
            // Reopening the actual fixed store and exact retry perform the same safe cleanup.
            await ImmutableFileArchive.PublishAsync(first,new byte[]{1},TestContext.Current.CancellationToken);
            await ImmutableFileArchive.PublishAsync(second,new byte[]{2},TestContext.Current.CancellationToken);
            (await Charge(second)).ShouldBe(65536);
            (await BoundedRegularFile.ReadAsync(first,1,TestContext.Current.CancellationToken)).ShouldBe(new byte[]{1});
            (await BoundedRegularFile.ReadAsync(second,1,TestContext.Current.CancellationToken)).ShouldBe(new byte[]{2});
            File.Exists(first).ShouldBeFalse();File.Exists(second).ShouldBeFalse();
        }
        finally { Directory.Delete(root,true); }
    }
}
