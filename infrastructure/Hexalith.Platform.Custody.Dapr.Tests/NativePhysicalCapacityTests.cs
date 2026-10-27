using Hexalith.Platform.Tests;
using Dapr.Client;
using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;

// Real source filesystem IO, real preallocated blocks and the actual NativeDaprAuthorityStore, with only the conditional Dapr transport isolated.
internal static class NativePhysicalCapacityTests
{
    internal static async Task Run()
    {
        using var fixture=new Fixture();using var dapr=Substitute.For<DaprClient>();
        string directory=Path.Combine(Path.GetTempPath(),"native-physical-capacity-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            string checkpoints=Path.Combine(directory,"checkpoints"),originals=Path.Combine(directory,"originals"),capacity=Path.Combine(directory,"capacity");
            foreach(string root in new[]{checkpoints,originals,capacity}) { Directory.CreateDirectory(root);await File.WriteAllTextAsync(Path.Combine(root,"enrollment.json"),"fixture-only-enrollment"); }
            long completion=10L*CustodyBindingContract.MaximumOriginalBytes;
            var install=new NativeInstallation("OpenBaoTransit","independent-store","head-",checkpoints,originals,CustodyJson.Hash("fixture-only-enrollment"u8),"https://unused.invalid","transit","/unused",[],Fixture.Digest,
                ArchiveCapacity:new("LinuxExclusiveLocalArchiveCapacityV1",capacity,completion+NativeArchiveCapacity.MetadataReserveBytes+16*1024*1024,completion,VolumeFenceIdentity:await ArchiveVolumeFence.ReadIdentityAsync(capacity,default)));
            install=install with { ArchiveCapacity=install.ArchiveCapacity! with { ProtectedAllocations=ProtectedArchiveFixture.Enroll(capacity,new[]{checkpoints,originals},completion+32*1024*1024L,NativeArchiveCapacity.MaximumLedgerBytes) } };
            var profile=fixture.Profile with { Native=install };
            await File.WriteAllBytesAsync(Path.Combine(capacity,"capacity.json"),CustodyJson.Bytes(new NativeArchiveCapacityState(profile.EnrollmentDigest,install.ArchiveEnrollmentSha256,0,[],[])));
            ProtectedArchiveFixture.InitializeMetadata(install.ArchiveCapacity!.ProtectedAllocations!,File.ReadAllBytes(Path.Combine(capacity,"capacity.json")));
            byte[] initial=await CheckpointArchive.EncodeAsync(fixture.Store.State.Checkpoint,(_,_,_)=>throw new Exception("Unexpected genesis payload."),default);
            Directory.CreateDirectory(Path.Combine(checkpoints,"checkpoint"));await File.WriteAllBytesAsync(Path.Combine(checkpoints,"checkpoint","seed"),initial);
            var version=new ObjectVersion("checkpoint/seed",CustodyJson.Hash(initial),CustodyJson.Hash(initial),initial.Length,profile.RetainUntil);
            var head=new AuthorityHead(fixture.Store.State.Head.Revision,version.Sha256,version,profile.EnrollmentDigest);int revision=0;bool reject=false,loseHeadAck=false,inspectNextCommit=false,inspectFenceOnRead=false;
            dapr.GetStateAndETagAsync<AuthorityHead>(install.AuthorityComponent,Arg.Any<string>(),Arg.Any<ConsistencyMode?>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(async _=>
            {
                var snapshot=(head,revision.ToString());
                if(inspectFenceOnRead)
                {
                    inspectFenceOnRead=false;
                    // A competing receiving instance must not advance capacity/admission while this exact
                    // post-commit snapshot is awaiting settlement; otherwise its new reserve can be deleted.
                    using var contender=new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
                    try
                    {
                        using var acquired=await ArchiveFileLease.AcquireAsync(Path.Combine(capacity,"capacity.lock"),contender.Token);
                        throw new Exception("Native head snapshot and reserve settlement permit a competing admission.");
                    }
                    catch(OperationCanceledException) when(contender.IsCancellationRequested) { }
                }
                return snapshot;
            });
            dapr.TrySaveStateAsync(install.AuthorityComponent,Arg.Any<string>(),Arg.Any<AuthorityHead>(),Arg.Any<string>(),Arg.Any<StateOptions>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(call=>
            { if(reject || call.ArgAt<string>(3)!=revision.ToString()) return false;head=call.ArgAt<AuthorityHead>(2);revision++;if(inspectNextCommit) { inspectNextCommit=false;inspectFenceOnRead=true; }if(loseHeadAck) { loseHeadAck=false;throw new IOException("fixture lost native head acknowledgement"); }return true; });
            var store=new NativeDaprAuthorityStore(profile,install,dapr,TimeProvider.System);
            var current=await store.ReadAsync("tenant",default);var next=SemanticProvider.Clone(current);
            next.NativeIntents.Add("pending",new("tenant","operation","pending",Fixture.Digest,"producer","e1","e2",Fixture.Digest,current.Head.Revision,CustodyBindingIntentPhase.SendCommitted));
            reject=true;await Refuse(()=>store.CommitAsync(current,next,default));
            int published=FinalBodies(checkpoints)+FinalBodies(originals);
            for(int i=0;i<4;i++) await Refuse(()=>new NativeDaprAuthorityStore(profile,install,dapr,TimeProvider.System).CommitAsync(current,next,default));
            Check(FinalBodies(checkpoints)+FinalBodies(originals)==published,"Failed native CAS created duplicate archive bodies.");
            using(var protectedStore=new ProtectedArchiveStore(install.ArchiveCapacity!.ProtectedAllocations!)) Check(protectedStore.ReservedBytes(capacity+"|"+NativeArchiveCapacity.Key("tenant","intent:pending"))>=completion,"Pending native completion lacks exact disjoint private written allocation.");
            reject=false;loseHeadAck=true;await Refuse(()=>store.CommitAsync(current,next,default));
            current=await store.ReadAsync("tenant",default);Check(current.Checkpoint.NativeIntents.ContainsKey("pending"),"Lost native head acknowledgement lost admitted identity.");
            // Independently constructed receiving instances share one durable capacity fence and exact body charge.
            var parallel=await Task.WhenAll(Enumerable.Range(0,6).Select(_=>new NativeDaprAuthorityStore(profile,install,dapr,TimeProvider.System).PutImmutableAsync(new byte[1024*1024],"journal",default)));
            Check(parallel.Distinct().Count()==1,"Parallel native receiving instances allocated different exact body locators.");
            // Outstanding allocated completion survives new-admission quota pressure and exact-store restart.
            await Refuse(()=>store.PutImmutableAsync(new byte[20*1024*1024],"journal",default));
            var second=SemanticProvider.Clone(current);second.NativeIntents.Add("second",current.Checkpoint.NativeIntents["pending"] with { IntentId="second",OperationId="second" });
            await Refuse(()=>store.CommitAsync(current,second,default));
            // A lost publication acknowledgement retains the exact charged transfer. Restart completes
            // using only enrolled fixed inodes/entries/blocks after all allocating filesystem calls refuse.
            bool interrupted=false;string interruptedPath=Path.Combine(originals,"journal","interrupted");byte[] interruptedBody=new byte[1024*1024];int allocations=0;
            HeldArchiveDirectory.AllocationAttempt=_=> { allocations++;throw new IOException("fixture exhausted inode/metadata allocation after admission"); };
            var interruptedCapacity=new NativeArchiveCapacity(profile,install) { AvailableBytesOverride=()=>0,BeforePublication=_=> { if(!interrupted) { interrupted=true;throw new IOException("fixture lost publication acknowledgement"); } } };
            await Refuse(()=>interruptedCapacity.WithFenceAsync(async state=> { await interruptedCapacity.PublishAsync(state,interruptedPath,"journal/interrupted",interruptedBody,NativeArchiveCapacity.Key("tenant","intent:pending"),default);return true; },default));
            await interruptedCapacity.WithFenceAsync(async state=> { await interruptedCapacity.PublishAsync(state,interruptedPath,"journal/interrupted",interruptedBody,NativeArchiveCapacity.Key("tenant","intent:pending"),default);return true; },default);
            Check(interrupted && ProtectedArchiveStore.Exists(interruptedPath) && allocations==0,"Fixed native completion allocated new filesystem resources or lost its exact result.");
            // Metadata rename loss preserves the durable liability and the fixed actual ledger+rewrite allocation.
            var faultCheckpoint=SemanticProvider.Clone(current);faultCheckpoint.NativeIntents.Add("metadata-fault",next.NativeIntents["pending"] with { IntentId="metadata-fault",OperationId="metadata-fault" });
            // Use the existing completion publication to exercise metadata ENOSPC without a second large reservation.
            var faultCapacity=new NativeArchiveCapacity(profile,install) { FaultPoint=point=> { if(point=="metadata-ledger-replaced") throw new IOException("fixture ENOSPC after metadata rename"); } };
            string faultPath=Path.Combine(originals,"journal","metadata-fault");
            await Refuse(()=>faultCapacity.WithFenceAsync(async state=> { await faultCapacity.PublishAsync(state,faultPath,"journal/metadata-fault",new byte[4096],NativeArchiveCapacity.Key("tenant","intent:pending"),default);return true; },default));
            var recoveredCapacity=new NativeArchiveCapacity(profile,install);
            await recoveredCapacity.WithFenceAsync(async state=> { await recoveredCapacity.PublishAsync(state,faultPath,"journal/metadata-fault",new byte[4096],NativeArchiveCapacity.Key("tenant","intent:pending"),default);return true; },default);
            Check(new FileInfo(Path.Combine(capacity,"allocation-metadata-0.bin")).Length+new FileInfo(Path.Combine(capacity,"allocation-metadata-1.bin")).Length==2L*(NativeArchiveCapacity.MaximumLedgerBytes+8192),"Fixed metadata banks lost their full preallocated growth coverage.");
            var result=await store.PutCompletionImmutableAsync(new byte[8*1024*1024],"original","tenant","intent:pending",default);
            var retry=await new NativeDaprAuthorityStore(profile,install,dapr,TimeProvider.System).PutCompletionImmutableAsync(new byte[8*1024*1024],"original","tenant","intent:pending",default);
            Check(result==retry,"Exact native completion retry changed stable locator.");
            var completed=SemanticProvider.Clone(current);completed.NativeIntents["pending"]=completed.NativeIntents["pending"] with { Phase=CustodyBindingIntentPhase.Closed };completed.NativeResults["pending"]=result;
            inspectNextCommit=true;
            await store.CommitAsync(current,completed,default);
            var ledger=CustodyJson.Read<NativeArchiveCapacityState>(ProtectedArchiveFixture.Ledger(capacity,install.ArchiveCapacity!.ProtectedAllocations));
            Check(ledger.Pending.Count==0 && ledger.Publications.Count==0,"Exact native completion did not settle its reserved blocks.");
            Check(allocations==0,"Settlement allocated an unprotected filesystem resource.");HeldArchiveDirectory.AllocationAttempt=null;
            // A final head acknowledgement can be lost before settlement. New ordinary admission must
            // reconcile that exact terminal head first, even when two reserves cannot fit the quota.
            current=await store.ReadAsync("tenant",default);
            var pendingAgain=SemanticProvider.Clone(current);pendingAgain.NativeIntents.Add("lost-completion",next.NativeIntents["pending"] with { IntentId="lost-completion",OperationId="lost-completion" });
            current=await store.CommitAsync(current,pendingAgain,default);
            var lostCompletion=SemanticProvider.Clone(current);lostCompletion.NativeIntents["lost-completion"]=lostCompletion.NativeIntents["lost-completion"] with { Phase=CustodyBindingIntentPhase.Closed };
            loseHeadAck=true;await Refuse(()=>store.CommitAsync(current,lostCompletion,default));
            ledger=CustodyJson.Read<NativeArchiveCapacityState>(ProtectedArchiveFixture.Ledger(capacity,install.ArchiveCapacity!.ProtectedAllocations));
            Check(ledger.Pending.Count==1,"Lost completion acknowledgement must conservatively preserve its reserve.");
            current=await store.ReadAsync("tenant",default);
            var successor=SemanticProvider.Clone(current);successor.NativeIntents.Add("after-loss",next.NativeIntents["pending"] with { IntentId="after-loss",OperationId="after-loss" });
            await store.CommitAsync(current,successor,default);
            ledger=CustodyJson.Read<NativeArchiveCapacityState>(ProtectedArchiveFixture.Ledger(capacity,install.ArchiveCapacity!.ProtectedAllocations));
            Check(ledger.Pending.Count==1 && ledger.Pending.Values.Single().OperationId=="after-loss","Exact completed reserve stranded a subsequent admission after acknowledgement loss.");
            var missing=new NativeDaprAuthorityStore(profile,install with { ArchiveCapacity=null },dapr,TimeProvider.System);await Refuse(()=>missing.PutImmutableAsync(new byte[]{1},"journal",default));
            // Legacy pending/native GUID/object locators are preserved; a missing independent capacity genesis never self-enrolls one.
            File.Delete(Path.Combine(capacity,"allocation-metadata-0.bin"));await Refuse(()=>store.PutImmutableAsync(new byte[]{2},"journal",default));
        }
        finally { HeldArchiveDirectory.AllocationAttempt=null;Directory.Delete(directory,true); }
    }
    private static int FinalBodies(string root) { using var store=ProtectedArchiveStore.Find(Path.Combine(root,"logical-original"));return store?.PublishedOriginals??Directory.GetFiles(root,"*",SearchOption.AllDirectories).Length; }
    private static void Check(bool allowed,string reason) { if(!allowed) throw new Exception(reason); }
    private static async Task Refuse(Func<Task> action) { try { await action(); }catch(Exception error) when(error is AuthorityUnavailableException or AuthorityConflictException or IOException) { return; }throw new Exception("Native physical capacity did not refuse."); }
}
