using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

// Concrete read/publish fixtures only. The public receiving handler still refuses complete H4 semantics.
public sealed class JointPhaseNativePublisherTests
{
    [Theory]
    [InlineData("exact")][InlineData("reference")][InlineData("digest")][InlineData("native-proof")]
    public async Task EnrolledCanaryPublicationReadsTheActualReceivingComparisonAndRefusesNativeSubstitution(string mutation)
    {
        using var signer=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var receiving=new ReceivingAuthorityFixture("fr34-observer");
        string archive=Path.Combine(Path.GetTempPath(),"enrolled-native-canary-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(archive);
        try
        {
            var token=TestContext.Current.CancellationToken;var gateway=Substitute.For<IEventStoreGatewayClient>();var guard=Substitute.For<IGovernanceScopeGuard>();var protection=Substitute.For<IDeletionProtectionOwner>();var authority=Substitute.For<ICustodyOwnerStateAuthority>();
            authority.AuthorizeOperationAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);
            string canary=new string('a',64),message="01ARZ3NDEKTSV4RRFFQ69G5FAV";var identity=new AggregateIdentity("tenant","fr34-canary",canary);
            var reference=new Hexalith.Platform.Custody.Fr34CanaryReference(new("engine",new string('b',40),"custody","tenant"),canary,identity.ActorId,message);
            var item=new StreamReadEvent(1,"FixtureNativeCanary","content-free"u8.ToArray(),"json",1,message,null,null,DateTimeOffset.UtcNow,"fixture",null) { NativeEventDigest=new string('c',64) };
            var proof=NativeSourceProofCodec.ExtendDigest(NativeSourceProofCodec.CreateSeed(identity,message),identity,1,message,item.NativeEventDigest!);
            gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(),Arg.Any<CancellationToken>()).Returns(call=> { var read=call.Arg<StreamReadRequest>();var events=read.FromSequence==0 && read.ToSequence!=0?new[]{item}:Array.Empty<StreamReadEvent>();return new StreamReadPage(identity.TenantId,identity.Domain,identity.AggregateId,events,new(read.FromSequence,read.ToSequence,events.Length==0?null:1,1,events.Length,false,null) { NativeProof=proof }); });
            var measured=new Fr34MeasuredCanaryOriginal(reference,reference.Target.EngineId,reference.Target.EngineVersion,mutation=="native-proof"?proof with { CumulativeDigest=new string('e',64) }:proof,1,mutation=="digest"?new string('d',64):item.NativeEventDigest!,12,10,true,true,Fr34CommandFingerprint.Protocol);
            string originalId=EventStorePrivateRecordingPort.StableUlid("fr34","tenant",canary)+":native-comparison";
            var originals=new InstalledQualificationOriginalStore(receiving.Authority);
            await originals.BeginAsync("fr34-observer",originalId,new JsonObject { ["reference"]=JsonSerializer.SerializeToNode(mutation=="reference"?reference with { CanaryId=new string('f',64) }:reference,CustodyBindingContract.Json) },token);
            await originals.RetainAsync("fr34-observer",originalId,JsonSerializer.SerializeToNode(measured,CustodyBindingContract.Json)!.AsObject(),token);
            guard.ReadAsync("tenant",Arg.Any<CancellationToken>()).Returns(new TenantGovernanceGuardState("tenant","installation",1,"epoch","legacy","writers",null,[],[],[],[],[],[]));
            protection.LookupAsync("tenant","batch",Arg.Any<CancellationToken>()).Returns(new DeletionConsumptionOutcome("tenant","batch",DeletionConsumptionStatus.Unconsumed,1,0,"receipt",null,null,null,[]));
            string bindingDigest=QualificationProtocolSchema.Digest(new JsonObject { ["fixtureOnly"]=true });
            var request=new JsonObject { ["requestId"]=new string('a',64),["bindingDigest"]=bindingDigest,["phase"]=3,["cases"]=new JsonArray("host:H4") };
            var fixedArchive=Hexalith.Platform.Tests.ProtectedArchiveFixture.Enroll(archive,[archive],4*1024*1024);
            string nativeArchive=Path.Combine(archive,"native");
            var scopes=new Dictionary<string,JointPhaseNativeSourceScope>{{"host:H4",new(bindingDigest,new string('b',40),3,"tenant","batch",[identity],[reference])}};
            var publisher=new JointPhaseNativeSourcePublisher(new(gateway,TimeProvider.System),guard,protection,authority,scopes,nativeArchive,signer,new DaprFr34MeasuredOriginalReader(receiving.Authority));
            var result=await publisher.PublishAsync("host:H4",request,token);
            if(mutation!="exact") { result.ShouldBeNull();using var failed=new ProtectedArchiveStore(fixedArchive);failed.PublishedOriginals.ShouldBe(0);return; }
            result.ShouldNotBeNull();string path=Path.Combine(nativeArchive,new string('a',64),Convert.ToHexStringLower(SHA256.HashData("host:H4"u8))+".json");
            byte[] exact=await BoundedRegularFile.ReadAsync(path,65536,token);File.Exists(path).ShouldBeFalse();
            var envelope=JsonNode.Parse(exact)!;var payload=envelope["payload"]!;signer.VerifyData(QualificationProofVerifier.CanonicalBytes(payload),Convert.FromBase64String(envelope["signature"]!.GetValue<string>()),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence).ShouldBeTrue();
            JsonNode.DeepEquals(payload["records"]![0]!["payload"]!["canaryOriginals"]![0],JsonSerializer.SerializeToNode(measured,CustodyBindingContract.Json)).ShouldBeTrue();
            int reads=gateway.ReceivedCalls().Count();JsonNode.DeepEquals(result,await publisher.LookupAsync("host:H4",request,token)).ShouldBeTrue();gateway.ReceivedCalls().Count().ShouldBe(reads);(await BoundedRegularFile.ReadAsync(path,65536,token)).ShouldBe(exact);
            string lostArchive=Path.Combine(archive,"measurement-only"),lostPath=Path.Combine(lostArchive,new string('a',64),Path.GetFileName(path));
            await ImmutableFileArchive.PublishAsync(lostPath+".measurement",exact,token);
            var recovery=new JointPhaseNativeSourcePublisher(new(gateway,TimeProvider.System),guard,protection,authority,scopes,lostArchive,signer);
            JsonNode.DeepEquals(result,await recovery.LookupAsync("host:H4",request,token)).ShouldBeTrue();gateway.ReceivedCalls().Count().ShouldBe(reads);(await BoundedRegularFile.ReadAsync(lostPath,65536,token)).ShouldBe(exact);
            using var ownerKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);string ownerDirectory=Path.Combine(archive,"owner");
            await ImmutableFileArchive.PublishAsync(Path.Combine(ownerDirectory,new string('a',64),Path.GetFileName(path)),QualificationProofVerifier.CanonicalBytes(result),token);
            var ownerStore=Substitute.For<IQualificationOriginalStore>();ownerStore.ReadAdmissionAsync("joint-phase-command",new string('a',64),request,Arg.Any<CancellationToken>()).Returns(QualificationOriginalAdmissionState.Admitted);
            (await new JointPhaseMetadataFinalizer(new(bindingDigest,new string('b',40),3,["host:H4"]),ownerStore,ownerDirectory,ownerKey,(_,_)=>Task.FromResult(true)).FinalizeAsync(request,token)).ShouldNotBeNull();
            File.WriteAllText(Path.Combine(archive,"owner.pem"),ownerKey.ExportSubjectPublicKeyInfoPem());File.WriteAllText(Path.Combine(archive,"source.pem"),signer.ExportSubjectPublicKeyInfoPem());
            File.WriteAllText(Path.Combine(archive,"cross-language-input.json"),new JsonObject { ["request"]=request.DeepClone(),["binding"]=new JsonObject { ["fixtureOnly"]=true },["allocation"]=JsonSerializer.SerializeToNode(fixedArchive,CustodyBindingContract.Json),["sourceDirectory"]=nativeArchive,["ownerDirectory"]=ownerDirectory }.ToJsonString());
            var start=new System.Diagnostics.ProcessStartInfo("python3") { RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Repository() };start.ArgumentList.Add("eng/ext-secrets-1/protected_archive_observer_fixture.py");start.ArgumentList.Add(archive);
            using var process=System.Diagnostics.Process.Start(start)!;string stdout=await process.StandardOutput.ReadToEndAsync(token),stderr=await process.StandardError.ReadToEndAsync(token);await process.WaitForExitAsync(token);process.ExitCode.ShouldBe(0,stdout+stderr);
            foreach(string changed in new[]{"unchanged","revoked","scope","expired","cas-lost"})
            {
                authority.AuthorizeOperationAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);
                using var sourceReceiver=new ReceivingAuthorityFixture("joint-phase-source-reader",handlers:[new JointPhaseSourceOperationHandler(publisher,TimeProvider.System)]);
                sourceReceiver.State=sourceReceiver.State with { Callers=sourceReceiver.State.Callers.Select(value=>value with { ScopeReference="joint-phase:3",Methods=["joint-phase-publish-native-source"] }).ToArray() };
                var input=new { caseId="host:H4",phaseRequest=request };
                // The first admitted source operation refuses incomplete live semantics. Recovery alone may read its exact retained snapshot.
                (await sourceReceiver.Authority.InvokeAsync<JsonObject>("joint-phase-publish-native-source",input,token)).ShouldBeNull();
                var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                authority.AuthorizeOperationAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(async _=> { entered.TrySetResult();await release.Task;return true; });
                var pending=sourceReceiver.Authority.InvokeAsync<JsonObject>("joint-phase-publish-native-source",input,token);await entered.Task.WaitAsync(TimeSpan.FromSeconds(3),token);
                if(changed=="revoked") sourceReceiver.RevokeWorkload("recording-owner");
                if(changed is "scope" or "expired") sourceReceiver.State=sourceReceiver.State with { Callers=sourceReceiver.State.Callers.Select(value=>changed=="scope"?value with { ScopeReference="joint-phase:2" }:value with { ValidUntil=DateTimeOffset.UtcNow.AddSeconds(-1) }).ToArray() };
                if(changed=="cas-lost") sourceReceiver.BeforeConditionalSave=()=>sourceReceiver.RevokeWorkload("unmatched-fixture-identity");
                release.TrySetResult();var recovered=await pending;
                if(changed=="unchanged") JsonNode.DeepEquals(result,recovered).ShouldBeTrue();else recovered.ShouldBeNull();
                (await BoundedRegularFile.ReadAsync(path,65536,token)).ShouldBe(exact);gateway.ReceivedCalls().Count().ShouldBe(reads);
            }
            receiving.Methods.ShouldContain("qualification-lookup-original");
        }
        finally { Directory.Delete(archive,true); }
    }
    [Fact]
    public async Task AggregateNativeStreamsLargerThanFormerLimitRecoverExactBytesAndReachRealPythonObserverWithoutRemeasurement()
    {
        using var signer=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var owner=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string archive=Path.Combine(Path.GetTempPath(),"aggregate-native-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(archive);var token=TestContext.Current.CancellationToken;
        try
        {
            var gateway=Substitute.For<IEventStoreGatewayClient>();var guard=Substitute.For<IGovernanceScopeGuard>();var protection=Substitute.For<IDeletionProtectionOwner>();var authority=Substitute.For<ICustodyOwnerStateAuthority>();
            authority.AuthorizeOperationAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);
            var streams=Enumerable.Range(0,4).Select(index=>new AggregateIdentity("tenant","fixture-domain","stream-"+index)).ToArray();
            var originals=streams.ToDictionary(identity=>identity,identity=>Enumerable.Range(1,800).Select(index=>new StreamReadEvent(index,"FixtureNativeEvent","content-free-source-measurement"u8.ToArray(),"json",1,
                EventStorePrivateRecordingPort.StableUlid("native",identity.AggregateId,index.ToString()),null,null,DateTimeOffset.UtcNow,"fixture-writer",null) { NativeEventDigest=new string('b',64) }).ToArray());
            var proofs=originals.ToDictionary(pair=>pair.Key,pair=>pair.Value.Aggregate(NativeSourceProofCodec.CreateSeed(pair.Key,"01ARZ3NDEKTSV4RRFFQ69G5FAV"),(proof,item)=>NativeSourceProofCodec.ExtendDigest(proof,pair.Key,item.SequenceNumber,item.MessageId,item.NativeEventDigest!)));
            gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(),Arg.Any<CancellationToken>()).Returns(call=>
            {
                var read=call.Arg<StreamReadRequest>();var identity=new AggregateIdentity(read.Tenant,read.Domain,read.AggregateId!);var all=originals[identity];
                var events=read.ToSequence==0?Array.Empty<StreamReadEvent>():all.Where(item=>item.SequenceNumber>read.FromSequence).Take(read.PageSize).ToArray();
                bool more=events.Length>0 && events[^1].SequenceNumber<all.Length;
                return new StreamReadPage(identity.TenantId,identity.Domain,identity.AggregateId,events,new(read.FromSequence,read.ToSequence,events.Length==0?null:events[^1].SequenceNumber,all.Length,events.Length,more,more?new ReplayContinuationToken("fixture-page-"+events[^1].SequenceNumber):null) { NativeProof=proofs[identity] });
            });
            guard.ReadAsync("tenant",Arg.Any<CancellationToken>()).Returns(new TenantGovernanceGuardState("tenant","installation",1,"epoch","legacy","writers",null,[],[],[],[],[],[]));
            protection.LookupAsync("tenant","batch",Arg.Any<CancellationToken>()).Returns(new DeletionConsumptionOutcome("tenant","batch",DeletionConsumptionStatus.Unconsumed,1,0,"receipt",null,null,null,[]));
            var request=new JsonObject { ["requestId"]=new string('a',64),["bindingDigest"]=QualificationProtocolSchema.Digest(new JsonObject { ["fixtureOnly"]=true }),["phase"]=3,["cases"]=new JsonArray("host:H4") };
            var scope=new Dictionary<string,JointPhaseNativeSourceScope>{{"host:H4",new(request["bindingDigest"]!.GetValue<string>(),new string('b',40),3,"tenant","batch",streams)}};
            var publisher=new JointPhaseNativeSourcePublisher(new(gateway,TimeProvider.System),guard,protection,authority,scope,archive,signer);
            var evidence=await publisher.PublishAsync("host:H4",request,token);evidence.ShouldNotBeNull();
            string path=Directory.GetFiles(Path.Combine(archive,new string('a',64)),"*.json").Single();byte[] exact=await File.ReadAllBytesAsync(path,token);exact.Length.ShouldBeGreaterThan(1024*1024);
            // Actual aggregate bound denies before either immutable publication or its index; no truncation is admitted.
            string refused=Path.Combine(archive,"refused");var bounded=new JointPhaseNativeSourcePublisher(new(gateway,TimeProvider.System),guard,protection,authority,scope,refused,signer);
            await Should.ThrowAsync<InvalidDataException>(()=>bounded.PublishBoundedAsync("host:H4",request,exact.Length-100,token));Directory.Exists(refused).ShouldBeFalse();
            File.Delete(path);int reads=gateway.ReceivedCalls().Count();JsonNode.DeepEquals(evidence,await publisher.LookupAsync("host:H4",request,token)).ShouldBeTrue();gateway.ReceivedCalls().Count().ShouldBe(reads);
            (await File.ReadAllBytesAsync(path,token)).ShouldBe(exact);
            // A previously valid signed original at the aggregate bound remains exactly recoverable, including its raw whitespace.
            string boundary=Path.Combine(archive,"boundary"),boundaryPath=Path.Combine(boundary,new string('a',64),Path.GetFileName(path));
            byte[] padded=new byte[JointPhaseNativeSourcePublisher.MaximumEnvelopeBytes+1];exact.CopyTo(padded,0);Array.Fill(padded,(byte)' ',exact.Length,padded.Length-exact.Length);
            await ImmutableFileArchive.PublishAsync(boundaryPath+".measurement",padded.AsMemory(0,JointPhaseNativeSourcePublisher.MaximumEnvelopeBytes),token);
            var boundaryPublisher=new JointPhaseNativeSourcePublisher(new(gateway,TimeProvider.System),guard,protection,authority,scope,boundary,signer);
            JsonNode.DeepEquals(evidence,await boundaryPublisher.LookupAsync("host:H4",request,token)).ShouldBeTrue();new FileInfo(boundaryPath).Length.ShouldBe(JointPhaseNativeSourcePublisher.MaximumEnvelopeBytes);
            string oversize=Path.Combine(archive,"oversize"),oversizePath=Path.Combine(oversize,new string('a',64),Path.GetFileName(path));Directory.CreateDirectory(Path.GetDirectoryName(oversizePath)!);await File.WriteAllBytesAsync(oversizePath+".measurement",padded,token);
            var oversizedPublisher=new JointPhaseNativeSourcePublisher(new(gateway,TimeProvider.System),guard,protection,authority,scope,oversize,signer);
            await Should.ThrowAsync<IOException>(()=>oversizedPublisher.LookupAsync("host:H4",request,token));File.Exists(oversizePath).ShouldBeFalse();gateway.ReceivedCalls().Count().ShouldBe(reads);
            await File.WriteAllTextAsync(Path.Combine(archive,"request.json"),request.ToJsonString(),token);await File.WriteAllTextAsync(Path.Combine(archive,"evidence.json"),evidence.ToJsonString(),token);
            await File.WriteAllTextAsync(Path.Combine(archive,"source.pem"),signer.ExportSubjectPublicKeyInfoPem(),token);await File.WriteAllTextAsync(Path.Combine(archive,"owner.pem"),owner.ExportSubjectPublicKeyInfoPem(),token);
            var start=new System.Diagnostics.ProcessStartInfo("python3") { RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Repository() };
            start.ArgumentList.Add("-c");start.ArgumentList.Add("import sys,pathlib,hashlib\nsys.path.insert(0,'eng/ext-secrets-1')\nimport qualify as q\nfrom joint_authority_service import PersistedPhaseObserver\nfrom joint_live_phase_runner import Blocked\nr=pathlib.Path(sys.argv[1]);pin=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()\nprofile={'ownerOriginalAnchorFile':str(r/'owner.pem'),'ownerOriginalAnchorSha256':pin(r/'owner.pem'),'sourceEvidence':{'host:H4':{'anchorFile':str(r/'source.pem'),'anchorSha256':pin(r/'source.pem'),'directory':str(r/'boundary'),'invariants':dict.fromkeys(['CurrentMigrationEpoch','RepairFenceAtomicity','ExactBridgeCohort','SuccessorPreservesAllOwnerFacts'],{})}}}\no=PersistedPhaseObserver(profile,{'fixtureOnly':True},None)\ntry:o._verify_source_case(q.read_json(r/'request.json'),'host:H4',q.read_json(r/'evidence.json'))\nexcept Blocked as e: assert 'ClosedNativePhaseSemanticsUnavailable' in e.missing,e.missing\nelse:raise AssertionError('Missing external protocols must never produce a phase pass')\nprint('Actual aggregate source acquired, signature and exact binding verified; external complete semantics remain blocked')");start.ArgumentList.Add(archive);
            using var process=System.Diagnostics.Process.Start(start)!;string output=await process.StandardOutput.ReadToEndAsync(token),error=await process.StandardError.ReadToEndAsync(token);await process.WaitForExitAsync(token);process.ExitCode.ShouldBe(0,output+error);
            gateway.ReceivedCalls().Count().ShouldBe(reads);
        }
        finally { Directory.Delete(archive,true); }
    }
    private static string Repository([System.Runtime.CompilerServices.CallerFilePath]string source="")=>Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!,"../.."));
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ExactNativeReadsProduceImmutableSignedBytesAndChangingProtectionSourceRefusesPublication(bool changeBatch)
    {
        using var signer=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string archive=Path.Combine(Path.GetTempPath(),"fixture-native-publisher-"+Guid.NewGuid().ToString("N"));
        try
        {
            var gateway=Substitute.For<IEventStoreGatewayClient>();var guard=Substitute.For<IGovernanceScopeGuard>();var protection=Substitute.For<IDeletionProtectionOwner>();var authority=Substitute.For<ICustodyOwnerStateAuthority>();
            authority.AuthorizeOperationAsync(Arg.Any<string>(),"PublishIndependentJointPhaseNativeSource",Arg.Any<CancellationToken>()).Returns(true);
            var identity=new AggregateIdentity("tenant","fixture-domain","canary");string message="01ARZ3NDEKTSV4RRFFQ69G5FAV";
            var item=new StreamReadEvent(1,"FixtureNativeEvent","private-native-bytes"u8.ToArray(),"json",1,message,null,null,DateTimeOffset.UtcNow,"writer",null) { NativeEventDigest=new string('b',64) };
            gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(),Arg.Any<CancellationToken>()).Returns(call=>
            {
                var read=call.Arg<StreamReadRequest>();var proof=NativeSourceProofCodec.ExtendDigest(NativeSourceProofCodec.CreateSeed(identity,message),identity,1,message,item.NativeEventDigest!);
                var events=read.FromSequence==0 && read.ToSequence!=0?new[]{item}:Array.Empty<StreamReadEvent>();
                return new StreamReadPage(identity.TenantId,identity.Domain,identity.AggregateId,events,new(read.FromSequence,read.ToSequence,events.Length==0?null:1,1,events.Length,false,null) { NativeProof=proof });
            });
            var state=new TenantGovernanceGuardState("tenant","installation",1,"epoch","legacy","writers",null,[],[],[],[],[],[]);
            guard.ReadAsync("tenant",Arg.Any<CancellationToken>()).Returns(state);
            var originalBatch=new DeletionConsumptionOutcome("tenant","batch",DeletionConsumptionStatus.Unconsumed,1,0,"native-batch-receipt",null,null,null,[]);int reads=0;
            protection.LookupAsync("tenant","batch",Arg.Any<CancellationToken>()).Returns(_=>++reads>1 && changeBatch?originalBatch with { OwnerRevision=2 }:originalBatch);
            var request=new JsonObject { ["requestId"]=new string('a',64),["bindingDigest"]="binding",["phase"]=3,["cases"]=new JsonArray("host:H4") };
            var publisher=new JointPhaseNativeSourcePublisher(new(gateway,TimeProvider.System),guard,protection,authority,
                new Dictionary<string,JointPhaseNativeSourceScope>{{"host:H4",new("binding",new string('c',40),3,"tenant","batch",[identity])}},archive,signer);
            var output=await publisher.PublishAsync("host:H4",request,TestContext.Current.CancellationToken);
            if(changeBatch) { output.ShouldBeNull();Directory.Exists(archive).ShouldBeFalse();return; }
            output.ShouldNotBeNull();string path=Directory.GetFiles(Path.Combine(archive,new string('a',64)),"*.json").Single();byte[] bytes=await File.ReadAllBytesAsync(path,TestContext.Current.CancellationToken);
            var envelope=JsonNode.Parse(bytes)!.AsObject();var payload=envelope["payload"]!;
            signer.VerifyData(QualificationProofVerifier.CanonicalBytes(payload),Convert.FromBase64String(envelope["signature"]!.GetValue<string>()),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence).ShouldBeTrue();
            output["sourceRecordsSha256"]!.GetValue<string>().ShouldBe(QualificationProtocolSchema.Digest(payload["records"]!));
            payload["records"]![0]!["payload"]!["protection"]!["OwnerRevision"]!.GetValue<long>().ShouldBe(1);
            System.Text.Encoding.UTF8.GetString(bytes).ShouldNotContain("private-native-bytes");
            // Drop the outer publication and change every live native fact. Exact recovery uses the retained first signed body.
            File.Delete(path);guard.ReadAsync("tenant",Arg.Any<CancellationToken>()).Returns(state with { Revision=900 });
            protection.LookupAsync("tenant","batch",Arg.Any<CancellationToken>()).Returns(originalBatch with { OwnerRevision=900 });
            int beforeReads=gateway.ReceivedCalls().Count();
            JsonNode.DeepEquals(output,await publisher.LookupAsync("host:H4",request,TestContext.Current.CancellationToken)).ShouldBeTrue();
            (await File.ReadAllBytesAsync(path,TestContext.Current.CancellationToken)).ShouldBe(bytes);gateway.ReceivedCalls().Count().ShouldBe(beforeReads);
            File.Delete(path);File.Delete(path+".measurement");
            (await publisher.LookupAsync("host:H4",request,TestContext.Current.CancellationToken)).ShouldBeNull();gateway.ReceivedCalls().Count().ShouldBe(beforeReads);
            (await publisher.PreflightAsync("host:H4",request,TestContext.Current.CancellationToken)).ShouldNotBeEmpty();
            gateway.ReceivedCalls().ShouldAllBe(call=>call.GetMethodInfo().Name==nameof(IEventStoreGatewayClient.ReadStreamAsync));
        }
        finally { if(Directory.Exists(archive)) Directory.Delete(archive,true); }
    }
}
