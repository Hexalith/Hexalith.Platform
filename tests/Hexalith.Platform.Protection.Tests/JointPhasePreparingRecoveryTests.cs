using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;
using Hexalith.Platform.Tests;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

// Disposable enrolled archive and actual command/finalizer entrypoints; no installed owner or cohort executes.
public sealed class JointPhasePreparingRecoveryTests
{
    [Theory]
    [InlineData(false,"exact")][InlineData(true,"exact")]
    [InlineData(false,"damaged-body")][InlineData(true,"damaged-body")]
    [InlineData(false,"incomplete-chain")][InlineData(true,"incomplete-chain")]
    public async Task RestartUsesOnlyTheExactSignedPreparingIndexAndNeverRerunsEffects(bool metadataOnly,string state)
    {
        var token=TestContext.Current.CancellationToken;
        string root=Path.Combine(Path.GetTempPath(),"preparing-owner-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var publicOnly=ECDsa.Create(key.ExportParameters(false));
        using var authority=new ReceivingAuthorityFixture("joint-phase-command");
        try
        {
            var allocation=ProtectedArchiveFixture.Enroll(root,[root],1024*1024,metadata:65536);
            string id=new string('a',64),index=Path.Combine(root,id,"owner-original.json");
            var request=new JsonObject { ["requestId"]=id,["bindingDigest"]="binding",["phase"]=2,["attemptId"]="exact-attempt",["cases"]=new JsonArray("host:H1") };
            var scope=new JointPhaseCommandScope("binding",new string('b',40),2,["host:H1"]);
            IQualificationOriginalStore Store()=>new InstalledQualificationOriginalStore(authority.Authority);
            var publisher=Substitute.For<IJointPhaseNativeSourcePublisher>();int effects=0;
            publisher.PreflightAsync("host:H1",Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<string>>([]));
            JsonObject Evidence()=>new() { ["caseId"]="host:H1",["requestSha256"]=QualificationProtocolSchema.Digest(request),["sourceRecordsSha256"]=new string('c',64) };
            publisher.PublishAsync("host:H1",Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(_=> { effects++;return Evidence(); });
            var workflow=new QualificationSourceWorkflow("H1",(_,_)=>Task.FromResult<JsonObject?>(new()),(_,_)=>Task.FromResult<JsonObject?>(new()),(_,_)=>Task.FromResult<IReadOnlyList<string>>([]));
            var original=new JsonObject { ["requestId"]="step",["runId"]="exact-attempt",["targetCommit"]=scope.PlatformCommit,["caseId"]="H1" };
            var executor=Substitute.For<IQualificationOriginalStore>();executor.ReadAdmissionAsync("executor","step:case-result",original,Arg.Any<CancellationToken>()).Returns(QualificationOriginalAdmissionState.Absent);
            var mapped=new Dictionary<string,IReadOnlyList<(QualificationSourceWorkflow,JsonObject)>> { ["host:H1"]=[(workflow,original)] };
            Task<JsonObject?> Run(ECDsa signer)=>metadataOnly
                ?new JointPhaseMetadataFinalizer(scope,Store(),root,signer,(_,_)=>Task.FromResult(true)).FinalizeAsync(request,token)
                :new JointPhaseCommand(scope,Store(),mapped,publisher,root,signer,executorOriginals:executor,phaseAdmission:(_,_)=>Task.FromResult(true)).ExecuteAsync(request,token);
            if(metadataOnly)
            {
                await Store().BeginAsync("joint-phase-command",id,request,token);
                string name=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("host:H1")))+".json";
                await ImmutableFileArchive.PublishAsync(Path.Combine(root,id,name),QualificationProofVerifier.CanonicalBytes(Evidence()),token);
            }
            int publications=0;ProtectedArchiveStore.PublicationFault=()=> { if(++publications==(metadataOnly?1:2)) throw new IOException("interruption after exact data flush, before Published head"); };
            await Should.ThrowAsync<IOException>(()=>Run(key));ProtectedArchiveStore.PublicationFault=null;
            (await ProtectedArchiveStore.ExistsAsync(index,token)).ShouldBeFalse();
            byte[] exact=(await ImmutableFileArchive.ReadRetainedCandidateAsync(index,1024*1024,token)).ShouldNotBeNull();
            int originalEffects=effects;long charged;using(var store=new ProtectedArchiveStore(allocation)) charged=store.ReservedBytes("ordinary:"+Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(index))));
            charged.ShouldBeGreaterThan(0);
            if(state!="exact") Damage(root,index,allocation,state);
            // New stores and a public-only signer make regeneration of time/signature impossible.
            if(state=="exact")
            {
                var recovered=(await Run(publicOnly)).ShouldNotBeNull();QualificationProofVerifier.CanonicalBytes(recovered).ShouldBe(exact);
                (await BoundedRegularFile.ReadAsync(index,1024*1024,token)).ShouldBe(exact);
                QualificationProofVerifier.CanonicalBytes((await Run(publicOnly)).ShouldNotBeNull()).ShouldBe(exact);
            }
            else
            {
                await Should.ThrowAsync<IOException>(()=>Run(publicOnly));await Should.ThrowAsync<IOException>(()=>Run(publicOnly));
                (await Store().LookupResultAsync("joint-phase-command",id,token)).ShouldBeNull();
                using var store=new ProtectedArchiveStore(allocation);store.ReservedBytes("ordinary:"+Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(index)))).ShouldBe(charged);
            }
            effects.ShouldBe(originalEffects);effects.ShouldBe(metadataOnly?0:1);
        }
        finally { ProtectedArchiveStore.PublicationFault=null;Directory.Delete(root,true); }
    }
    private static void Damage(string root,string index,ProtectedArchiveAllocationInstallation config,string state)
    {
        using var file=new FileStream(Path.Combine(root,"allocation-data.bin"),FileMode.Open,FileAccess.ReadWrite);
        string expected=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(index)));
        for(int slot=0;slot<config.ChunkCount;slot++) for(int copy=0;copy<2;copy++)
        {
            long offset=8192+slot*(16384L+config.ChunkBytes)+copy*8192;byte[] block=new byte[8192];file.Position=offset;file.ReadExactly(block);
            int length=BinaryPrimitives.ReadInt32LittleEndian(block);var frame=JsonNode.Parse(block.AsSpan(40,length))!.AsObject();
            if(frame["Path"]?.GetValue<string>()!=expected || frame["State"]?.GetValue<string>()!="Preparing") continue;
            if(state=="damaged-body") { file.Position=8192+slot*(16384L+config.ChunkBytes)+16384;file.WriteByte(0xff); }
            else
            {
                frame["Count"]=2;byte[] raw=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(frame,CustodyBindingContract.Json);Array.Clear(block);
                BinaryPrimitives.WriteInt32LittleEndian(block,raw.Length);SHA256.HashData(raw).CopyTo(block,8);raw.CopyTo(block,40);file.Position=offset;file.Write(block);
            }
            file.Flush(true);return;
        }
        throw new InvalidOperationException("Fixture Preparing head required.");
    }
}
