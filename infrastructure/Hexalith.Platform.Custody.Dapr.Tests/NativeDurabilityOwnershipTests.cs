using Hexalith.Platform.Tests;
using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Secrets.Runtime;

// Deterministic faults around actual native metadata publication, without remote/native actions.
internal static class NativeDurabilityOwnershipTests
{
    internal static async Task Run()
    {
        using var fixture=new Fixture();
        string root=Path.Combine(Path.GetTempPath(),"native-metadata-held-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            foreach(bool swap in new[]{false,true})
            {
                string directory=Path.Combine(root,swap?"swap":"stall"),outside=directory+"-outside",parked=directory+"-parked";Directory.CreateDirectory(directory);Directory.CreateDirectory(outside);
                await File.WriteAllTextAsync(Path.Combine(directory,"enrollment.json"),"isolated-enrollment");
                var installed=new NativeInstallation("OpenBaoTransit","independent","head",directory,directory,CustodyJson.Hash("isolated-enrollment"u8),"https://unused.invalid","transit","/unused",[],Fixture.Digest,
                    ArchiveCapacity:new("LinuxExclusiveLocalArchiveCapacityV1",directory,4L*1024*1024*1024,10L*CustodyBindingContract.MaximumOriginalBytes,VolumeFenceIdentity:await ArchiveVolumeFence.ReadIdentityAsync(directory,default)));
                installed=installed with { ArchiveCapacity=installed.ArchiveCapacity! with { ProtectedAllocations=ProtectedArchiveFixture.Enroll(directory,new[]{directory},10L*CustodyBindingContract.MaximumOriginalBytes+32*1024*1024L,NativeArchiveCapacity.MaximumLedgerBytes) } };
                var profile=fixture.Profile with { Native=installed };byte[] initial=CustodyJson.Bytes(new NativeArchiveCapacityState(profile.EnrollmentDigest,installed.ArchiveEnrollmentSha256,0,[],[]));
                await File.WriteAllBytesAsync(Path.Combine(directory,"capacity.json"),initial);
                ProtectedArchiveFixture.InitializeMetadata(installed.ArchiveCapacity!.ProtectedAllocations!,initial);
                var checkpoint=SemanticProvider.Clone(fixture.Store.State);checkpoint.NativeIntents.Add("pending",new("tenant","operation","pending",Fixture.Digest,"producer","e1","e2",Fixture.Digest,checkpoint.Revision,CustodyBindingIntentPhase.SendCommitted));
                using var release=new ManualResetEventSlim();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);using var cancellation=new CancellationTokenSource();
                bool once=false;
                var capacity=new NativeArchiveCapacity(profile,installed) { FaultPoint=point=>
                {
                    if(point!="metadata-reserve-transferred" || once) return;once=true;
                    if(swap) { Directory.Move(directory,parked);Directory.CreateSymbolicLink(directory,outside); }
                    else { entered.TrySetResult();release.Wait(); }
                } };
                async Task Write()=>await capacity.WithFenceAsync(async state=> { await capacity.EnsurePendingAsync(state,checkpoint,default);return true; },cancellation.Token);
                if(swap)
                {
                    await Refuse(Write);if(Directory.GetFiles(outside).Length!=0) throw new Exception("Native metadata escaped enrolled held directory.");
                    Directory.Delete(directory);Directory.Move(parked,directory);
                }
                else
                {
                    var pending=Write();await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));cancellation.Cancel();
                    await Refuse(()=>pending.WaitAsync(TimeSpan.FromSeconds(1)));
                    using var contender=new CancellationTokenSource(TimeSpan.FromMilliseconds(100));await Refuse(async()=>{ using var lease=await ArchiveFileLease.AcquireAsync(Path.Combine(directory,"capacity.lock"),contender.Token); });
                    release.Set();
                }
                // A timeout/swap cannot commit a new metadata authority or release the held fencing early.
                if(!File.ReadAllBytes(Path.Combine(directory,"capacity.json")).AsSpan().SequenceEqual(initial)) throw new Exception("Failed metadata publication advanced its ledger.");
                var retry=new NativeArchiveCapacity(profile,installed);
                await retry.WithFenceAsync(async state=> { await retry.EnsurePendingAsync(state,checkpoint,default);return true; },default);
                var final=CustodyJson.Read<NativeArchiveCapacityState>(ProtectedArchiveFixture.Ledger(directory,installed.ArchiveCapacity!.ProtectedAllocations));
                if(final.Pending.Count!=1 || Directory.GetFiles(outside).Length!=0) throw new Exception("Exact native metadata retry failed.");
            }
        }
        finally { Directory.Delete(root,true); }
    }
    private static async Task Refuse(Func<Task> action)
    { try { await action(); }catch(Exception error) when(error is IOException or OperationCanceledException or AuthorityUnavailableException) { return; }throw new Exception("Unsafe native metadata accepted."); }
}
