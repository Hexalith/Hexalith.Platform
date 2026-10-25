using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

// Isolated source fixtures. They run no live/cohort command and establish no installed protocol or phase qualification.
public sealed class JointPhaseCommandTests
{
    [Fact]
    public void ShippedPhaseOneCompositionDoesNotRequireOrConstructAbsentHostGuardOrNativeSourcePorts()
    {
        using var dapr=NSubstitute.Substitute.For<Dapr.Client.DaprClient>();
        var scope=new JointPhaseCommandScope("binding",new string('a',40),1,["secrets:S1.reject.missing-config","secrets:S2.canonical-hmac"]);
        JointPhaseCommandExecutable.CreateSelectedWorkflows(scope,dapr,new SecretsRuntimeConfiguration(),null,Substitute.For<IQualificationOriginalStore>(),TimeProvider.System).ShouldBeEmpty();
        dapr.ReceivedCalls().ShouldBeEmpty();
        Should.Throw<InvalidOperationException>(()=>JointPhaseCommandExecutable.CreateSelectedWorkflows(scope with { Cases=["host:H1"] },dapr,new SecretsRuntimeConfiguration(),null,Substitute.For<IQualificationOriginalStore>(),TimeProvider.System));
    }
    [Fact]
    public void ImmutableRecipesBindOnlyTheReturnedAttemptAndChangedAcceptedBindingsRefuse()
    {
        var accepted=new JointPhaseCommandScope(new string('a',64),new string('b',40),1,["secrets:S1.reject.missing-config"]);
        var recipe=new JsonObject { ["protocol"]="EXT-SECRETS-1.qualification.v1",["targetCommit"]=accepted.PlatformCommit,["caseId"]="S1.reject.missing-config",["planSha256"]=new string('c',64),["enrolledScope"]=new JsonObject { ["tenantId"]="fixture" } };
        var phase=new JsonObject { ["attemptId"]=Guid.NewGuid().ToString(),["phase"]=1,["bindingDigest"]=accepted.BindingDigest };
        string unchanged=recipe.ToJsonString();var first=JointPhaseRequestRecipe.Bind(recipe,phase,accepted)!;
        first["runId"]!.GetValue<string>().ShouldBe(phase["attemptId"]!.GetValue<string>());
        JsonNode.DeepEquals(first,JointPhaseRequestRecipe.Bind(recipe,phase,accepted)).ShouldBeTrue();recipe.ToJsonString().ShouldBe(unchanged);
        var changed=(JsonObject)phase.DeepClone();changed["attemptId"]=Guid.NewGuid().ToString();JointPhaseRequestRecipe.Bind(recipe,changed,accepted)!["requestId"]!.GetValue<string>().ShouldNotBe(first["requestId"]!.GetValue<string>());
        changed["bindingDigest"]=new string('d',64);JointPhaseRequestRecipe.Bind(recipe,changed,accepted).ShouldBeNull();
        var legacy=(JsonObject)recipe.DeepClone();legacy["runId"]="predicted-attempt";JointPhaseRequestRecipe.Bind(legacy,phase,accepted).ShouldBeNull();
        legacy=(JsonObject)recipe.DeepClone();legacy["requestId"]=new string('f',64);JointPhaseRequestRecipe.Bind(legacy,phase,accepted).ShouldBeNull();
    }
    [Theory]
    [InlineData("before-first")][InlineData("between-steps")][InlineData("unknown-first")]
    public async Task ParentOriginalRecoveryDistinguishesUnadmittedStepsFromUnknownEffects(string crash)
    {
        using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);string directory=Path.Combine(Path.GetTempPath(),"fixture-joint-command-"+Guid.NewGuid().ToString("N"));
        try
        {
            var originals=Substitute.For<IQualificationOriginalStore>();originals.BeginAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(false);
            int firstEffects=0,secondEffects=0,lookups=0;var state=crash=="before-first"?QualificationOriginalAdmissionState.Absent:QualificationOriginalAdmissionState.Admitted;
            originals.ReadAdmissionAsync("executor","first:case-result",Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(_=>state);
            originals.ReadAdmissionAsync("executor","second:case-result",Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(QualificationOriginalAdmissionState.Absent);
            var first=new QualificationSourceWorkflow("v23.no-false-replacement",(_,_)=> { firstEffects++;state=QualificationOriginalAdmissionState.Admitted;return Task.FromResult<JsonObject?>(new()); },(_,_)=> { lookups++;return Task.FromResult<JsonObject?>(crash=="unknown-first"?null:new()); },(_,_)=>Task.FromResult<IReadOnlyList<string>>([]));
            var second=new QualificationSourceWorkflow("v23.admission-block-never-reopens",(_,_)=> { secondEffects++;return Task.FromResult<JsonObject?>(new()); },(_,_)=>Task.FromResult<JsonObject?>(null),(_,_)=>Task.FromResult<IReadOnlyList<string>>([]));
            JsonObject Original(string id,string requestId)=>new() { ["requestId"]=requestId,["runId"]="attempt",["targetCommit"]=new string('a',40),["caseId"]=id };
            var publisher=Substitute.For<IJointPhaseNativeSourcePublisher>();publisher.PreflightAsync(Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<string>>([]));
            publisher.PublishAsync(Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(new JsonObject { ["actualFixtureNativeReceipt"]=new string('b',64) });
            var command=new JointPhaseCommand(new("binding",new string('a',40),2,["host:v23"]),originals,
                new Dictionary<string,IReadOnlyList<(QualificationSourceWorkflow,JsonObject)>>{{"host:v23",new[]{(first,Original(first.CaseId,"first")),(second,Original(second.CaseId,"second"))}}},publisher,directory,key);
            var request=new JsonObject { ["requestId"]=new string('b',64),["bindingDigest"]="binding",["phase"]=2,["attemptId"]="attempt",["cases"]=new JsonArray("host:v23") };
            var result=await command.ExecuteAsync(request,TestContext.Current.CancellationToken);
            if(crash=="unknown-first") { result.ShouldBeNull();firstEffects.ShouldBe(0);secondEffects.ShouldBe(0);lookups.ShouldBe(1);await publisher.DidNotReceive().PublishAsync(Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()); }
            else { result.ShouldNotBeNull();firstEffects.ShouldBe(crash=="before-first"?1:0);secondEffects.ShouldBe(1); }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory,true); }
    }
    [Theory]
    [InlineData("complete")][InlineData("missing")][InlineData("changed")][InlineData("foreign-admission")]
    public async Task MetadataOnlyFinalizerReadsImmutableCasesAndRecoversMissingIndexWithoutAnyWorkflowOrNativePort(string state)
    {
        using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var authority=new ReceivingAuthorityFixture("joint-phase-command");
        string directory=Path.Combine(Path.GetTempPath(),"fixture-phase-finalization-"+Guid.NewGuid().ToString("N"));
        try
        {
            string id=new string('a',64);var request=new JsonObject { ["requestId"]=id,["bindingDigest"]="binding",["attemptId"]="retained-attempt",["phase"]=2,["cases"]=new JsonArray("host:H1") };
            var store=new InstalledQualificationOriginalStore(authority.Authority);await store.BeginAsync("joint-phase-command",id,request,TestContext.Current.CancellationToken);
            if(state=="foreign-admission") request["attemptId"]="changed-attempt";
            Directory.CreateDirectory(Path.Combine(directory,id));
            if(state!="missing")
            {
                var evidence=new JsonObject { ["requestSha256"]=state=="changed"?new string('f',64):QualificationProtocolSchema.Digest(request),["caseId"]="host:H1",["sourceRecordsSha256"]=new string('b',64) };
                string name=Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("host:H1")))+".json";
                await ImmutableFileArchive.PublishAsync(Path.Combine(directory,id,name),QualificationProofVerifier.CanonicalBytes(evidence),TestContext.Current.CancellationToken);
            }
            var finalizer=new JointPhaseMetadataFinalizer(new("binding",new string('c',40),2,["host:H1"]),store,directory,key);
            if(state=="missing") await Should.ThrowAsync<IOException>(()=>finalizer.FinalizeAsync(request,TestContext.Current.CancellationToken));
            else
            {
                var result=await finalizer.FinalizeAsync(request,TestContext.Current.CancellationToken);
                if(state!="complete") result.ShouldBeNull();
                else
                {
                    result.ShouldNotBeNull();var bytes=await File.ReadAllBytesAsync(Path.Combine(directory,id,"owner-original.json"),TestContext.Current.CancellationToken);
                    JsonNode.DeepEquals(result,await finalizer.FinalizeAsync(request,TestContext.Current.CancellationToken)).ShouldBeTrue();
                    (await File.ReadAllBytesAsync(Path.Combine(directory,id,"owner-original.json"),TestContext.Current.CancellationToken)).ShouldBe(bytes);
                    key.VerifyData(QualificationProofVerifier.CanonicalBytes(result["payload"]!),Convert.FromBase64String(result["signature"]!.GetValue<string>()),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence).ShouldBeTrue();
                }
            }
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory,true); }
    }
    [Fact]
    public async Task MissingIndependentSourceReadinessBlocksEntirePartitionBeforeOriginalAdmissionOrEffects()
    {
        using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);var originals=Substitute.For<IQualificationOriginalStore>();var publisher=Substitute.For<IJointPhaseNativeSourcePublisher>();
        publisher.PreflightAsync(Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<string>>(["ExactIndependentNativeSourceCaseScope"]));int effects=0;
        var workflow=new QualificationSourceWorkflow("v23.no-false-replacement",(_,_)=> { effects++;return Task.FromResult<JsonObject?>(new()); },(_,_)=>Task.FromResult<JsonObject?>(null),(_,_)=>Task.FromResult<IReadOnlyList<string>>([]));
        var original=new JsonObject { ["requestId"]="step",["runId"]="attempt",["targetCommit"]=new string('a',40),["caseId"]=workflow.CaseId };
        var command=new JointPhaseCommand(new("binding",new string('a',40),2,["host:v23"]),originals,new Dictionary<string,IReadOnlyList<(QualificationSourceWorkflow,JsonObject)>>{{"host:v23",new[]{(workflow,original)}}},publisher,Path.GetTempPath(),key);
        var request=new JsonObject { ["requestId"]=new string('b',64),["bindingDigest"]="binding",["phase"]=2,["attemptId"]="attempt",["cases"]=new JsonArray("host:v23") };
        (await command.ExecuteAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();effects.ShouldBe(0);originals.ReceivedCalls().ShouldBeEmpty();
    }
}
