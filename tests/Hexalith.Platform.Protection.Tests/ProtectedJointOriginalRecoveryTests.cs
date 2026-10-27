using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;
using Hexalith.Platform.Tests;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class ProtectedJointOriginalRecoveryTests
{
    [Theory]
    [InlineData("finalizer")][InlineData("command")]
    public async Task LostIndexRetentionRecoversExactProtectedOriginalWithoutEffectsOrNewSignature(string entrypoint)
    {
        using var signer=ECDsa.Create(ECCurve.NamedCurves.nistP256);string directory=Path.Combine(Path.GetTempPath(),"protected-joint-index-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            var token=TestContext.Current.CancellationToken;var config=ProtectedArchiveFixture.Enroll(directory,[directory],4*1024*1024);
            string id=new string('a',64),target=new string('b',40);var request=new JsonObject { ["requestId"]=id,["bindingDigest"]="binding",["attemptId"]="attempt",["phase"]=2,["cases"]=new JsonArray("host:H1") };
            var store=Substitute.For<IQualificationOriginalStore>();store.ReadAdmissionAsync("joint-phase-command",id,request,Arg.Any<CancellationToken>()).Returns(QualificationOriginalAdmissionState.Admitted);
            var evidence=new JsonObject { ["requestSha256"]=QualificationProtocolSchema.Digest(request),["caseId"]="host:H1",["sourceRecordsSha256"]=new string('c',64) };
            byte[] body=QualificationProofVerifier.CanonicalBytes(evidence);string caseName=Convert.ToHexStringLower(SHA256.HashData("host:H1"u8))+".json",casePath=Path.Combine(directory,id,caseName),index=Path.Combine(directory,id,"owner-original.json");
            await ImmutableFileArchive.PublishAsync(casePath,body,token);
            var observations=new JsonArray(new JsonObject { ["caseId"]="host:H1",["evidenceSha256"]=Convert.ToHexStringLower(SHA256.HashData(body)) });
            var payload=new JsonObject { ["requestSha256"]=QualificationProtocolSchema.Digest(request),["bindingDigest"]="binding",["observations"]=observations,["completedAt"]=1234567 };
            var original=new JsonObject { ["payload"]=payload,["signature"]=Convert.ToBase64String(signer.SignData(QualificationProofVerifier.CanonicalBytes(payload),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence)) };
            byte[] exact=QualificationProofVerifier.CanonicalBytes(original);await ImmutableFileArchive.PublishAsync(index,exact,token);File.Exists(index).ShouldBeFalse();File.Exists(casePath).ShouldBeFalse();
            int effects=0;var workflow=new QualificationSourceWorkflow("fixture-operation",(_,_)=> { effects++;throw new InvalidOperationException("No effect during exact recovery"); },(_,_)=> { effects++;throw new InvalidOperationException("No native lookup during index recovery"); },(_,_)=>Task.FromResult<IReadOnlyList<string>>([]));
            var source=Substitute.For<IJointPhaseNativeSourcePublisher>();source.PreflightAsync(Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<string>>([]));
            var scope=new JointPhaseCommandScope("binding",target,2,["host:H1"]);JsonObject? result;
            if(entrypoint=="finalizer") result=await new JointPhaseMetadataFinalizer(scope,store,directory,signer,(_,_)=>Task.FromResult(true)).FinalizeAsync(request,token);
            else
            {
                var operation=new JsonObject { ["requestId"]="operation",["runId"]="attempt",["targetCommit"]=target,["caseId"]=workflow.CaseId };
                result=await new JointPhaseCommand(scope,store,new Dictionary<string,IReadOnlyList<(QualificationSourceWorkflow,JsonObject)>>{{"host:H1",new[]{(workflow,operation)}}},source,directory,signer,phaseAdmission:(_,_)=>Task.FromResult(true)).ExecuteAsync(request,token);
            }
            result.ShouldNotBeNull();QualificationProofVerifier.CanonicalBytes(result).ShouldBe(exact);(await BoundedRegularFile.ReadAsync(index,65536,token)).ShouldBe(exact);effects.ShouldBe(0);
            await store.Received(1).RetainAsync("joint-phase-command",id,Arg.Is<JsonObject>(value=>JsonNode.DeepEquals(value,original)),token);
            await source.DidNotReceive().PublishAsync(Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>());
            using var reopened=new ProtectedArchiveStore(config);reopened.PublishedOriginals.ShouldBe(2);
        }
        finally { Directory.Delete(directory,true); }
    }
}
