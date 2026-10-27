using System.Runtime.InteropServices;
using NSubstitute;
using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Secrets.Runtime;
using Hexalith.Platform.Tests;

internal static class NativeReopenedReserveTests
{
    internal static async Task Run()
    {
        using var fixture=new Fixture();string root=Path.Combine(Path.GetTempPath(),"native-reopen-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            string checkpoints=Path.Combine(root,"checkpoints"),originals=Path.Combine(root,"originals"),capacity=Path.Combine(root,"capacity");
            foreach(string path in new[]{checkpoints,originals,capacity}) { Directory.CreateDirectory(path);await File.WriteAllTextAsync(Path.Combine(path,"enrollment.json"),"fixture-enrollment"); }
            var allocations=ProtectedArchiveFixture.Enroll(capacity,[checkpoints,originals],1024*1024,NativeArchiveCapacity.MaximumLedgerBytes);
            var install=new NativeInstallation("OpenBaoTransit","store","head",checkpoints,originals,CustodyJson.Hash("fixture-enrollment"u8),"https://unused.invalid","transit","/unused",[],Fixture.Digest,
                ArchiveCapacity:new("LinuxExclusiveLocalArchiveCapacityV1",capacity,1024L*1024*1024,10L*CustodyBindingContract.MaximumOriginalBytes,VolumeFenceIdentity:await ArchiveVolumeFence.ReadIdentityAsync(capacity,default),ProtectedAllocations:allocations));
            var profile=fixture.Profile with { Native=install };var state=new NativeArchiveCapacityState(profile.EnrollmentDigest,install.ArchiveEnrollmentSha256,0,new(){{"pending",new("tenant","operation",8192)}},[]);
            await File.WriteAllBytesAsync(Path.Combine(capacity,"capacity.json"),CustodyJson.Bytes(state));
            ProtectedArchiveFixture.InitializeMetadata(allocations,CustodyJson.Bytes(state));
            byte[] legacyPending="retained ambiguous metadata candidate"u8.ToArray();await File.WriteAllBytesAsync(Path.Combine(capacity,"capacity.json.pending"),legacyPending);
            var pool=new NativeArchiveCapacity(profile,install);await pool.WithFenceAsync(_=>Task.FromResult(true),default);
            await pool.WithFenceAsync(_=>Task.FromResult(true),default); // unchanged exact enrolled allocation supports ordinary restart.
            Check(File.ReadAllBytes(Path.Combine(capacity,"capacity.json.pending")).AsSpan().SequenceEqual(legacyPending),"Native restart deleted an unproven named candidate.");
            string legacy=Path.Combine(originals,"legacy");byte[] exact="legacy exact original"u8.ToArray();await File.WriteAllBytesAsync(legacy,exact);
            bool conflict=false;try { await pool.WithFenceAsync(async retained=> { await pool.PublishAsync(retained,legacy,"legacy","different new bytes"u8.ToArray(),"pending",default);return true; },default); } catch(IOException) { conflict=true; }
            Check(conflict && File.ReadAllBytes(legacy).AsSpan().SequenceEqual(exact),"Native writer replaced or acknowledged conflicting legacy bytes.");
            await pool.WithFenceAsync(async retained=> { await pool.PublishAsync(retained,legacy,"legacy",exact,"pending",default);return true; },default);
            // The actual missing-original caller bounds marker/extent acquisition and retains the
            // occupied file worker until the stalled kernel/acquisition boundary genuinely returns.
            using(var dapr=Substitute.For<Dapr.Client.DaprClient>())
            using(var release=new ManualResetEventSlim())
            using(var caller=new CancellationTokenSource())
            {
                var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var exited=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                ProtectedArchiveStore.FindFault=_=> { entered.TrySetResult();try { release.Wait(); } finally { exited.TrySetResult(); } };
                var store=new NativeDaprAuthorityStore(profile,install,dapr,TimeProvider.System);var lookup=store.FindIntentResultAsync("journal/native-result/missing",caller.Token);
                try
                {
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));caller.Cancel();bool cancelled=false;
                    try { await lookup.WaitAsync(TimeSpan.FromSeconds(1)); }catch(OperationCanceledException) { cancelled=true; }
                    Check(cancelled && !exited.Task.IsCompleted,"Fixed-store discovery escaped the actual caller IO budget or abandoned its live worker.");
                }
                finally { ProtectedArchiveStore.FindFault=null;release.Set();await exited.Task.WaitAsync(TimeSpan.FromSeconds(3)); }
                Check(await store.FindIntentResultAsync("journal/native-result/missing",default) is null,"Missing exact fixed original was fabricated after acquisition cleanup.");
            }
            // An existing logical length/fallocate result cannot stand in for private written extents.
            string data=Path.Combine(capacity,"allocation-data.bin");using(var punched=new FileStream(data,FileMode.Open,FileAccess.ReadWrite))
                Check(Fallocate(punched.SafeFileHandle.DangerousGetHandle().ToInt32(),3,0,punched.Length)==0,"Fixture hole punch unsupported.");
            bool admitted=false,denied=false;
            try { await pool.WithFenceAsync(_=> { admitted=true;return Task.FromResult(true); },default); } catch(IOException) { denied=true; }
            Check(denied && !admitted,"Same-length sparse/punched protected reserve was counted or destructively repaired.");
            Check(new FileInfo(data).Length==8192+allocations.ChunkCount*(2L*8192+allocations.ChunkBytes),"Legacy sparse evidence was resized.");
        }
        finally { Directory.Delete(root,true); }
    }
    private static void Check(bool condition,string text) { if(!condition) throw new Exception(text); }
    [DllImport("libc",EntryPoint="fallocate")] private static extern int Fallocate(int fd,int mode,long offset,long length);
}
