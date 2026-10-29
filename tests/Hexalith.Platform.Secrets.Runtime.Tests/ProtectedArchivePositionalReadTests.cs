using System.Text;
using Hexalith.Platform.Tests;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class ProtectedArchivePositionalReadTests
{
    [Theory]
    [InlineData(1)][InlineData(17)][InlineData(4093)]
    public async Task EveryGeometryHeaderBodyAndMetadataReadLoopsThroughLegalShortReads(int maximumRead)
    {
        string root=Path.Combine(Path.GetTempPath(),"short-archive-read-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var config=ProtectedArchiveFixture.Enroll(root,[root],4*65536,metadata:65536);
            string path=Path.Combine(root,"original");byte[] body=Enumerable.Range(0,70003).Select(i=>(byte)(i%251)).ToArray();
            await ImmutableFileArchive.PublishAsync(path,body,TestContext.Current.CancellationToken);
            ProtectedArchiveStore.MaximumReadBytes=maximumRead;
            for(int restart=0;restart<2;restart++)
            {
                using var reopened=new ProtectedArchiveStore(config);reopened.Read(path,body.Length).ShouldBe(body);
                Encoding.UTF8.GetString(reopened.ReadMetadata()!).ShouldBe("{}");
            }
            await ArchiveVolumeFence.RunAsync(root,_=> { using var reopened=new ProtectedArchiveStore(config);reopened.WriteMetadata("actual metadata"u8);return Task.FromResult(true); },TestContext.Current.CancellationToken);
            using(var reopened=new ProtectedArchiveStore(config)) Encoding.UTF8.GetString(reopened.ReadMetadata()!).ShouldBe("actual metadata");
            using(var file=new FileStream(Path.Combine(root,"allocation-data.bin"),FileMode.Open,FileAccess.Write)) file.SetLength(8192);
            Should.Throw<IOException>(()=>new ProtectedArchiveStore(config));
        }
        finally { ProtectedArchiveStore.MaximumReadBytes=null;Directory.Delete(root,true); }
    }
    [Theory]
    [InlineData(4096,true)][InlineData(8192,true)][InlineData(16384,true)][InlineData(32768,true)][InlineData(65536,true)]
    [InlineData(12288,false)][InlineData(20480,false)][InlineData(24576,false)][InlineData(49152,false)][InlineData(69632,false)][InlineData(4097,false)]
    public async Task ActualArchiveReaderRequiresTheSameDividingGeometryAsIndependentPython(int chunk,bool valid)
    {
        string root=Path.Combine(Path.GetTempPath(),"archive-geometry-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            _=ProtectedArchiveFixture.Enroll(root,[root],chunk*20,metadata:65536,chunkBytes:chunk);
            string path=Path.Combine(root,"original");
            if(valid)
            {
                await ImmutableFileArchive.PublishAsync(path,"geometry original"u8.ToArray(),TestContext.Current.CancellationToken);
                (await BoundedRegularFile.ReadAsync(path,100,TestContext.Current.CancellationToken)).ShouldBe("geometry original"u8.ToArray());
            }
            else await Should.ThrowAsync<IOException>(()=>BoundedRegularFile.ReadAsync(path,100,TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root,true); }
    }
}
