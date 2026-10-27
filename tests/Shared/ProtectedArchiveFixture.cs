using System.Security.Cryptography;
using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Tests;

// Disposable externally provisioned source fixtures only; production has no genesis/provisioning fallback.
internal static class ProtectedArchiveFixture
{
    internal static ProtectedArchiveAllocationInstallation Enroll(string pool,IEnumerable<string> roots,long bytes,int metadata=4*1024*1024,int chunkBytes=65536)
    {
        foreach(string name in new[]{"pool.lock","capacity.lock"}) if(!File.Exists(Path.Combine(pool,name))) using(var file=File.Create(Path.Combine(pool,name))) file.Flush(true);
        int chunk=chunkBytes;int count=checked((int)((bytes+chunk-1)/chunk));
        string data=Path.Combine(pool,"allocation-data.bin"),m0=Path.Combine(pool,"allocation-metadata-0.bin"),m1=Path.Combine(pool,"allocation-metadata-1.bin");
        string Make(string path,long size)
        {
            using var output=new FileStream(path,FileMode.CreateNew,FileAccess.ReadWrite);byte[] block=new byte[1024*1024];long remaining=size;
            while(remaining>0) { int n=(int)Math.Min(remaining,block.Length);output.Write(block,0,n);remaining-=n; }output.Flush(true);return ArchiveAllocationProof.Identity(output.SafeFileHandle);
        }
        var identities=roots.Distinct(StringComparer.Ordinal).ToDictionary(root=>root,root=> { using var held=HeldArchiveDirectory.Acquire(root);return held.Identity; },StringComparer.Ordinal);
        var config=new ProtectedArchiveAllocationInstallation(pool,"",Make(data,8192+count*(2L*8192+chunk)),Make(m0,metadata+8192L),Make(m1,metadata+8192L),chunk,count,metadata,identities);
        byte[] geometry=JsonSerializer.SerializeToUtf8Bytes(new { Protocol="ProtectedArchiveGeometryV1",config.DataIdentity,config.ChunkBytes,config.ChunkCount },CustodyBindingContract.Json);using(var file=new FileStream(data,FileMode.Open,FileAccess.ReadWrite)) { file.Write(geometry);file.Flush(true); }
        // Independently provision both initial banks in the disposable fixture. Production never initializes missing state.
        using(var file=new FileStream(data,FileMode.Open,FileAccess.ReadWrite))
        {
            for(int slot=0;slot<count;slot++) for(int copy=0;copy<2;copy++)
            {
                long generation=copy==0?2:1;
                byte[] raw=JsonSerializer.SerializeToUtf8Bytes(new { Generation=generation,State="Free",Ticket=(string?)null,Publication=(string?)null,Path=(string?)null,Next=-1,Length=0,Sha256=(string?)null,Count=0 },CustodyBindingContract.Json),block=new byte[8192];
                System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(block,raw.Length);SHA256.HashData(raw).CopyTo(block,8);raw.CopyTo(block,40);
                file.Position=8192+slot*(2L*8192+chunk)+copy*8192;file.Write(block);
            }
            file.Flush(true);
        }
        byte[] initial=File.Exists(Path.Combine(pool,"capacity.json"))?File.ReadAllBytes(Path.Combine(pool,"capacity.json")):"{}"u8.ToArray();
        InitializeMetadata(config,initial);
        byte[] enrollment=JsonSerializer.SerializeToUtf8Bytes(config,CustodyBindingContract.Json);File.WriteAllBytes(Path.Combine(pool,"allocation-enrollment.json"),enrollment);
        config=config with { EnrollmentSha256=Convert.ToHexStringLower(SHA256.HashData(enrollment)) };
        foreach(string root in identities.Keys) File.WriteAllBytes(Path.Combine(root,".archive-allocation.json"),JsonSerializer.SerializeToUtf8Bytes(config,CustodyBindingContract.Json));
        return config;
    }
    // Explicit initial fixture enrollment only, before any operation may use this capacity.
    internal static void InitializeMetadata(ProtectedArchiveAllocationInstallation config,byte[] initial)
    {
        for(int copy=0;copy<2;copy++)
        {
            using var file=new FileStream(Path.Combine(config.Directory,"allocation-metadata-"+copy+".bin"),FileMode.Open,FileAccess.ReadWrite);byte[] block=new byte[8192];
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(block,copy+1);System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(8),initial.Length);System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(12),2);SHA256.HashData(initial).CopyTo(block,16);file.Write(block);file.Write(initial);file.Flush(true);
        }
    }
    internal static byte[] Ledger(string pool,ProtectedArchiveAllocationInstallation? config)
    { using var store=config is null?null:new ProtectedArchiveStore(config);return store?.ReadMetadata()??File.ReadAllBytes(Path.Combine(pool,"capacity.json")); }
}
