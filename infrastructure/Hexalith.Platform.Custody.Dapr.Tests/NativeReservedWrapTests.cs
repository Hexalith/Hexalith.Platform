using Hexalith.Platform.Tests;
using System.Security.Cryptography;
using Dapr.Client;
using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;

// Native effects use only disposable fixture keys; capacity and publications use the actual source store and filesystem.
internal static class NativeReservedWrapTests
{
    internal static async Task Run()
    {
        using var fixture=new Fixture();using var dapr=Substitute.For<DaprClient>();using var responseKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string directory=Path.Combine(Path.GetTempPath(),"native-reserved-wrap-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            string checkpoints=Path.Combine(directory,"checkpoints"),originals=Path.Combine(directory,"originals"),capacity=Path.Combine(directory,"capacity");
            foreach(string root in new[]{checkpoints,originals,capacity}) { Directory.CreateDirectory(root);await File.WriteAllTextAsync(Path.Combine(root,"enrollment.json"),"fixture-only-enrollment"); }
            long completion=10L*CustodyBindingContract.MaximumOriginalBytes;
            var install=new NativeInstallation("OpenBaoTransit","independent-store","head-",checkpoints,originals,CustodyJson.Hash("fixture-only-enrollment"u8),"https://unused.invalid","transit","/unused",[],Fixture.Digest,
                ArchiveCapacity:new("LinuxExclusiveLocalArchiveCapacityV1",capacity,2*completion+NativeArchiveCapacity.MetadataReserveBytes+24*1024*1024,completion,VolumeFenceIdentity:await ArchiveVolumeFence.ReadIdentityAsync(capacity,default)));
            install=install with { ArchiveCapacity=install.ArchiveCapacity! with { ProtectedAllocations=ProtectedArchiveFixture.Enroll(capacity,new[]{checkpoints,originals},2*completion+32*1024*1024L,NativeArchiveCapacity.MaximumLedgerBytes) } };
            var profile=fixture.Profile with { Native=install };fixture.Qualify();
            await File.WriteAllBytesAsync(Path.Combine(capacity,"capacity.json"),CustodyJson.Bytes(new NativeArchiveCapacityState(profile.EnrollmentDigest,install.ArchiveEnrollmentSha256,0,[],[])));
            ProtectedArchiveFixture.InitializeMetadata(install.ArchiveCapacity!.ProtectedAllocations!,File.ReadAllBytes(Path.Combine(capacity,"capacity.json")));
            byte[] initial=await CheckpointArchive.EncodeAsync(fixture.Store.State.Checkpoint,(_,_,_)=>throw new Exception("Unexpected genesis payload."),default);
            Directory.CreateDirectory(Path.Combine(checkpoints,"checkpoint"));await File.WriteAllBytesAsync(Path.Combine(checkpoints,"checkpoint","seed"),initial);
            var version=new ObjectVersion("checkpoint/seed",CustodyJson.Hash(initial),CustodyJson.Hash(initial),initial.Length,profile.RetainUntil);
            var head=new AuthorityHead(fixture.Store.State.Head.Revision,version.Sha256,version,profile.EnrollmentDigest);int revision=0;
            dapr.GetStateAndETagAsync<AuthorityHead>(install.AuthorityComponent,Arg.Any<string>(),Arg.Any<ConsistencyMode?>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(_=>(head,revision.ToString()));
            dapr.TrySaveStateAsync(install.AuthorityComponent,Arg.Any<string>(),Arg.Any<AuthorityHead>(),Arg.Any<string>(),Arg.Any<StateOptions>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(call=>
            { if(call.ArgAt<string>(3)!=revision.ToString()) return false;head=call.ArgAt<AuthorityHead>(2);revision++;return true; });
            var actual=new NativeDaprAuthorityStore(profile,install,dapr,TimeProvider.System);var store=new CounterfactualStore(actual) { LoseWrapAcknowledgement=true };
            var native=new PressureEpochs(fixture.Native,async()=>
            {
                var ledger=CustodyJson.Read<NativeArchiveCapacityState>(ProtectedArchiveFixture.Ledger(capacity,install.ArchiveCapacity!.ProtectedAllocations));
                int fill=checked((int)(install.ArchiveCapacity!.QuotaBytes-ledger.RetainedBytes-ledger.Pending.Values.Sum(value=>value.RemainingBytes)-NativeArchiveCapacity.MetadataReserveBytes-65536));
                if(fill<=0 || fill>CustodyBindingContract.MaximumOriginalBytes) throw new Exception("Controlled ordinary headroom fixture invalid.");
                await actual.PutImmutableAsync(new byte[fill],"pressure",default);
                store.Exhausted=true;
            });
            var current=await actual.ReadAsync("tenant",default);fixture.Store.State=current;
            var qualification=new QualificationClaim(profile.ProfileDigest,profile.Enrollment.ArtifactProfileDigest,Fixture.Digest,Fixture.Digest,Fixture.Digest,profile.CompletionReserveBytes,Fixture.Digest,Fixture.Digest);
            await File.WriteAllBytesAsync(profile.QualificationStatementPath,CustodyJson.Bytes(fixture.Sign("independent-observer",profile.ProfileDigest,qualification)));
            var request=fixture.Wrap("reserved-wrap");var input=request.WrapInput!;
            var admission=new KeyAdmission(input.KeyUse,request.Envelope.RequestDigest,profile.ProfileDigest,
                new[]{"caller","sdk","sidecar","uds","grpc","tls","component","native"}.Select(role=>new KeyHolder("copy-"+role,role,input.KeyUse.RecipientProcessIncarnation,input.KeyUse.TransportIncarnation,"owner",Fixture.Digest)).ToArray());
            var exchange=new KeyExchange(profile,store,native,fixture.Native,new StatementVerifier(profile,TimeProvider.System));
            try { await exchange.ExecuteAsync(request,current,fixture.Sign("independent-observer",CustodyJson.Hash(CustodyJson.Bytes(admission)),admission),default);throw new Exception("First wrap response loss was not exercised."); }
            catch(IOException error) when(error.Message=="fixture lost first wrap response") { }
            if(store.OrdinaryRefusals<2 || fixture.Native.Encrypts!=1) throw new Exception("Admitted wrap did not use reserved exact completion after ordinary publication refused.");
            fixture.Store.State=await actual.ReadAsync("tenant",default);var lookup=fixture.Request("lookup-original");
            lookup=lookup with { WrapOriginal=new(input.Identity,input.OperationId,input.ExpectedRevision,input.KekVersion,input.OpaqueWrappedKeyReference),Envelope=lookup.Envelope with { OperationId=input.OperationId } };
            lookup=lookup with { Envelope=lookup.Envelope with { RequestDigest=CustodyBindingContract.OriginalRequestDigest(lookup) } };
            byte[] bytes=CustodyBindingContract.Encode(lookup);var envelope=lookup.Envelope;
            var permission=new OperationPermission(envelope.Operation,envelope.OperationId,envelope.RequestAttemptId,envelope.Challenge,envelope.OwnerPurposeScope,profile.ProfileDigest);
            var control=new ControlClaim(CustodyJson.Hash(CustodyJson.Bytes(envelope.Proof)),Fixture.Digest,Fixture.Digest,Fixture.Digest,100,0,null);
            var provider=new SemanticProvider(profile,new NativeDaprAuthorityStore(profile,install,dapr,TimeProvider.System),new StatementVerifier(profile,TimeProvider.System),responseKey);
            var recovered=await provider.InvokeAsync(bytes,fixture.Sign("private-operation",CustodyJson.Hash(bytes),permission),fixture.Sign("control",CustodyJson.Hash(CustodyJson.Bytes(envelope.Proof)),control),default);
            var original=fixture.Store.State.Checkpoint.Originals[input.OperationId].Response??throw new Exception("Original wrap response missing.");
            var retainedResponse=CustodyJson.Read<CustodyBindingResponseV1>(await actual.ReadImmutableAsync(original,default));
            if(recovered.WrappedRegistration is null || recovered.WrappedRegistration!=retainedResponse.WrappedRegistration || fixture.Native.Encrypts!=1 || fixture.Native.Decrypts!=0) throw new Exception("Native reserved wrap recovery substituted registration or repeated native key work.");
            // The retirement completion must not charge the first enumerated, already-closed copy.
            current=await actual.ReadAsync("tenant",default);var closed=SemanticProvider.Clone(current);
            foreach(var pair in closed.NativeIntents.ToArray()) closed.NativeIntents[pair.Key]=pair.Value with { Phase=CustodyBindingIntentPhase.Closed };
            current=await actual.CommitAsync(current,closed,default);
            var extendedInstall=install with { ArchiveCapacity=install.ArchiveCapacity! with { QuotaBytes=install.ArchiveCapacity!.QuotaBytes+1024*1024 } };
            var retirementProfile=profile with { Native=extendedInstall };var retirementStore=new NativeDaprAuthorityStore(retirementProfile,extendedInstall,dapr,TimeProvider.System);
            var registration=current.Checkpoint.Registrations[input.OpaqueWrappedKeyReference];
            var source=await retirementStore.PutImmutableAsync(CustodyJson.Bytes(registration),"journal",default);
            var manifest=fixture.Manifest([TargetRegistration(input.Identity)]); // a distinct target leaves this source registration a survivor
            var proposal=SemanticProvider.Clone(current) with { Cycle=new("destroy",manifest,"e1","e2",CyclePhase.RetiringSuccessor,[],[],null,null) };
            proposal.Originals.Add("destroy",new(manifest.Envelope.RequestDigest,"destroy-manifest",null,manifest));
            var copy=new NativeIntentV1("tenant","destroy","closed-copy",Fixture.Digest,"producer","e1","e2",Fixture.Digest,current.Head.Revision,CustodyBindingIntentPhase.Closed);
            proposal.NativeIntents.Add(copy.IntentId,copy);proposal.NativeResults.Add(copy.IntentId+":source",source);
            var retirement=copy with { IntentId="retirement",SourceGeneration="e2",DestinationGeneration="retired",Phase=CustodyBindingIntentPhase.ResultDurable };
            proposal.NativeIntents.Add(retirement.IntentId,retirement);proposal.NativeResults.Add(retirement.IntentId,source);
            current=await retirementStore.CommitAsync(current,proposal,default);fixture.Store.State=current;
            var retirementQualification=qualification with { ProfileDigest=retirementProfile.ProfileDigest };
            await File.WriteAllBytesAsync(profile.QualificationStatementPath,CustodyJson.Bytes(fixture.Sign("independent-observer",retirementProfile.ProfileDigest,retirementQualification)));
            var abandonment=new AbandonedSuccessor("e2",["closed-copy","retirement"],Fixture.Digest,Fixture.Digest,Fixture.Digest);
            var worker=new RecoverableLifecycle(retirementProfile,retirementStore,fixture.Native,new StatementVerifier(retirementProfile,TimeProvider.System));
            await worker.AbandonSuccessorAsync("tenant",fixture.Sign("independent-observer",CustodyJson.Hash(CustodyJson.Bytes(abandonment)),abandonment),default);
            var abandoned=await retirementStore.ReadAsync("tenant",default);
            if(abandoned.Checkpoint.Cycle?.Phase!=CyclePhase.Reserved || abandoned.Checkpoint.NativeIntents["closed-copy"].Phase!=CustodyBindingIntentPhase.Closed
                || abandoned.Checkpoint.NativeIntents["retirement"].Phase!=CustodyBindingIntentPhase.Closed || abandoned.Checkpoint.NativeResults["closed-copy:source"]!=source
                || !CustodyJson.Bytes(abandoned.Checkpoint.Registrations[input.OpaqueWrappedKeyReference]).AsSpan().SequenceEqual(CustodyJson.Bytes(registration)) || fixture.Native.Encrypts!=1 || fixture.Native.Schedules!=0)
                throw new Exception("Native successor abandonment lost closed history, charged a closed reservation or repeated a native effect.");
        }
        finally { Directory.Delete(directory,true); }
    }
    private static Hexalith.Platform.Custody.CustodyKeyRegistration TargetRegistration(Hexalith.Platform.Custody.CustodyKeyObjectIdentity identity)
        =>new(identity with { ObjectId="separate-manifest-target",KeyAlias="separate-alias" },"target-wrap",0,"kek","target-reference","target-receipt");
    private sealed class PressureEpochs(IEpochOperations inner,Func<Task> pressure):IEpochOperations
    {
        public async Task<byte[]> EncryptOuterKeyAsync(string epoch,string tenant,byte[] bytes,CancellationToken token) { await pressure();return await inner.EncryptOuterKeyAsync(epoch,tenant,bytes,token); }
        public Task<byte[]> ReencryptOuterKeyAsync(string source,string destination,string tenant,byte[] bytes,CancellationToken token)=>inner.ReencryptOuterKeyAsync(source,destination,tenant,bytes,token);
        public Task ScheduleSourceCutAsync(string epoch,string tenant,CancellationToken token)=>inner.ScheduleSourceCutAsync(epoch,tenant,token);
    }
    private sealed class CounterfactualStore(NativeDaprAuthorityStore inner):IAuthorityStore
    {
        internal bool Exhausted,LoseWrapAcknowledgement;internal int OrdinaryRefusals;
        public Task<CurrentState> ReadAsync(string tenant,CancellationToken token)=>inner.ReadAsync(tenant,token);
        public async Task<CurrentState> CommitAsync(CurrentState expected,Checkpoint next,CancellationToken token)
        {
            var committed=await inner.CommitAsync(expected,next,token);
            if(LoseWrapAcknowledgement && next.Originals.Values.Any(value=>value.Operation=="wrap" && value.Response is not null)) { LoseWrapAcknowledgement=false;throw new IOException("fixture lost first wrap response"); }
            return committed;
        }
        public Task<byte[]> ReadImmutableAsync(ObjectVersion version,CancellationToken token)=>inner.ReadImmutableAsync(version,token);
        public Task<ObjectVersion> PutImmutableAsync(ReadOnlyMemory<byte> bytes,string category,CancellationToken token)=>inner.PutImmutableAsync(bytes,category,token);
        public Task<ObjectVersion?> FindIntentResultAsync(string locator,CancellationToken token)=>inner.FindIntentResultAsync(locator,token);
        public Task<ObjectVersion> PutIntentResultAsync(ReadOnlyMemory<byte> bytes,string locator,CancellationToken token)=>inner.PutIntentResultAsync(bytes,locator,token);
        private async Task RefuseOrdinary(ReadOnlyMemory<byte> bytes,CancellationToken token)
        {
            if(!Exhausted) throw new Exception("Completion pressure did not follow native admission.");
            try { await inner.PutImmutableAsync(bytes,"counterfactual",token); }
            catch(AuthorityUnavailableException) { OrdinaryRefusals++;return; }
            throw new Exception("Equivalent ordinary publication of exact completion bytes was not refused.");
        }
        public async Task<ObjectVersion> PutCompletionImmutableAsync(ReadOnlyMemory<byte> bytes,string category,string tenant,string operation,CancellationToken token)
        { await RefuseOrdinary(bytes,token);return await inner.PutCompletionImmutableAsync(bytes,category,tenant,operation,token); }
        public async Task<ObjectVersion> PutIntentCompletionAsync(ReadOnlyMemory<byte> bytes,string locator,string tenant,string operation,CancellationToken token)
        { await RefuseOrdinary(bytes,token);return await inner.PutIntentCompletionAsync(bytes,locator,tenant,operation,token); }
    }
}
