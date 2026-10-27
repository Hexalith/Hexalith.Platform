using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shouldly;
using NSubstitute;
using Hexalith.Platform.Custody;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

// Ephemeral fixture operations and signatures verify source composition only. No installed or physical qualification is claimed.
public sealed class QualificationReceivingProjectionTests
{
    [Fact]
    public Task ConcreteSecretsAdapterThroughPhaseCommandValidatesObserverOriginal()
        =>ConcreteReceivingDriverAndSignedNativeProjectionPassExistingPythonVerifierAndRejectSubstitution("S1.reject.missing-config","none");

    [Theory]
    [InlineData("S1.reject.missing-config","lost-reply")]
    [InlineData("S2.substitution.issuer","none")]
    [InlineData("S3.decision-reject.contract","none")]
    [InlineData("S3.exact-independent-decision","none")]
    [InlineData("S1.inventory.TenantKek","none")]
    [InlineData("S1.rotation-invalidation","none")]
    [InlineData("S1.rotation-invalidation","lost-reply")]
    [InlineData("S1.rotation-invalidation","rotation:install-original:admitted")]
    [InlineData("S1.rotation-invalidation","rotation:install-original:retained")]
    [InlineData("S1.rotation-invalidation","rotation:original-digest:retained")]
    [InlineData("S1.rotation-invalidation","rotation:install-successor:admitted")]
    [InlineData("S1.rotation-invalidation","rotation:install-successor:retained")]
    [InlineData("S1.rotation-invalidation","rotation:invalidate:admitted")]
    [InlineData("S1.rotation-invalidation","rotation:native-rotation-v1:retained")]
    [InlineData("S1.rotation-invalidation","rotation:case-result:before-retain")]
    [InlineData("S3.aes256gcm","none")]
    [InlineData("S3.es256-offline-anchor","none")]
    [InlineData("S3.crypto-substitution.tag","none")]
    [InlineData("S4.capability-canonical-14-fields","none")]
    [InlineData("S4.capability-substitution.issuer","none")]
    [InlineData("S4.capability-substitution.audience","none")]
    [InlineData("S4.capability-substitution.tenant","none")]
    [InlineData("S4.capability-substitution.request","none")]
    [InlineData("S4.capability-substitution.seal","none")]
    [InlineData("S4.capability-substitution.kind","none")]
    [InlineData("S4.capability-substitution.ordinal","none")]
    [InlineData("S4.capability-substitution.batch","none")]
    [InlineData("S4.capability-substitution.manifest","none")]
    [InlineData("S4.capability-substitution.guard","none")]
    [InlineData("S4.capability-substitution.intended-revision","none")]
    [InlineData("S4.capability-substitution.attestation","none")]
    [InlineData("S4.capability-substitution.signing-attempt","none")]
    [InlineData("S4.capability-substitution.key-version","none")]
    [InlineData("S4.capability-substitution.header","none")]
    [InlineData("S1.reject.missing-config","before-delivery")]
    [InlineData("S1.reject.missing-config","remote-unknown")]
    [InlineData("S1.reject.missing-config","next-restart-boundary")]
    [InlineData("S1.reject.missing-config","next-lookup-boundary")]
    [InlineData("S1.reject.missing-config","retained-next-boundary")]
    public Task ConcreteReceivingDriverAndSignedNativeProjectionPassExistingPythonVerifierAndRejectSubstitution(string caseId,string sourceFault)
        =>ReceivingAsync(caseId,sourceFault,null);
    [Theory]
    [InlineData("executor")][InlineData("observer")][InlineData("enrollment")]
    public Task ConcreteInstalledRoleCompositionServesVerifiedActualOwnerProof(string role)=>ReceivingAsync("S1.reject.missing-config","none",role);
    [Theory]
    [InlineData("genuine")][InlineData("invented-receipt")][InlineData("other-mutation")][InlineData("principal")][InlineData("method")][InlineData("tenant")][InlineData("target")]
    [InlineData("missing-target")][InlineData("stale")][InlineData("reused")][InlineData("cross-incarnation")][InlineData("legacy")][InlineData("same-tenant-foreign")]
    public Task ActualSignedMutationOriginalNativeReaderProjectionAndUnchangedPythonConsumerCorrelateEveryMutation(string defect)=>ReceivingAsync("S1.reject.missing-config","none",null,defect);
    private async Task ReceivingAsync(string caseId,string sourceFault,string? installedRole,string? mutationFault=null)
    {
        using var authority=new IndependentAuthorityReceiverTests.Fixture();
        using var executorKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var observerKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request=QualificationHostTests.Request();request["caseId"]=caseId;
        if(caseId=="S3.exact-independent-decision") { request["scenario"]!["outcomes"]=new JsonArray("Verified");var metrics=request["scenario"]!["expected"]!.AsObject();metrics.Remove("forbiddenEffects");metrics["exactIndependentDecision"]=1; }
        if(caseId.StartsWith("S1.inventory.",StringComparison.Ordinal)) { request["scenario"]!["outcomes"]=new JsonArray("Resolved");var metrics=request["scenario"]!["expected"]!.AsObject();metrics.Remove("forbiddenEffects");metrics["exactReferenceCount"]=1;metrics["independentlyCurrentInventory"]=1; }
        if(caseId is "S3.aes256gcm" or "S3.es256-offline-anchor") { request["scenario"]!["outcomes"]=new JsonArray("Verified");var metrics=request["scenario"]!["expected"]!.AsObject();metrics.Remove("forbiddenEffects");if(caseId=="S3.aes256gcm") { metrics["authenticatedWrap"]=1;metrics["ownedPlaintextCleared"]=1; } else { metrics["detachedEs256"]=1;metrics["publicAnchorRetained"]=1; } }
        if(caseId=="S4.capability-canonical-14-fields")
        {
            request["scenario"]!["outcomes"]=new JsonArray("Verified");var metrics=request["scenario"]!["expected"]!.AsObject();metrics.Remove("forbiddenEffects");
            metrics["canonicalFieldCount"]=14;metrics["detachedEs256"]=1;metrics["successfulRevisionInSignedBytes"]=0;metrics["capabilityExpiryFields"]=0;
        }
        if(caseId=="S1.rotation-invalidation") { request["scenario"]!["outcomes"]=new JsonArray("Invalidated");var metrics=request["scenario"]!["expected"]!.AsObject();metrics.Remove("forbiddenEffects");metrics["staleSafetyVerdicts"]=0;metrics["retainedDigestVerifiers"]=1; }
        request.Remove("requestId");request["requestId"]=QualificationProtocolSchema.Digest(request);var original=(JsonObject)request.DeepClone();
        authority.State=authority.State with {
            Callers=authority.State.Callers.Select(value=>value with { Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray(),
            QualificationBindings=new(new[]{"runId","targetCommit","isolatedTargetId","planSha256"}.Select(name=>new KeyValuePair<string,string>(name,original[name]!.ToJsonString()))),
            QualificationCases=new() { [caseId]=JsonSerializer.SerializeToElement(new { caseTargetId=original["caseTargetId"]!.GetValue<string>(),enrolledScope=original["enrolledScope"] },CustodyBindingContract.Json) } };
        IQualificationOriginalStore originals=new InstalledQualificationOriginalStore(authority.Client());
        var interruption=new InterruptOriginalStore(originals,sourceFault);originals=interruption;
        using var audit=new FileAuditFixture(originals,original);
        using var mutation=mutationFault is null?null:await QualificationMutationSourceFixture.CreateAsync(original,TestContext.Current.CancellationToken);
        var mutationTrust=mutation?.Trust;
        if(mutation is not null)
        {
            var row=mutation.Row();
            var evidence=row["authorizationEvidence"]!.AsObject();
            switch(mutationFault)
            {
                case "invented-receipt":row["authorizationReceiptId"]=new string('e',64);break;
                case "other-mutation":evidence["SourceRevision"]=2;break;
                case "principal":evidence["Authorization"]!["Response"]!["Payload"]!["DispatchBinding"]!["Principal"]="another-principal";break;
                case "method":evidence["Authorization"]!["Response"]!["Payload"]!["DispatchBinding"]!["Facts"]!["CommandType"]="AnotherMethod";break;
                case "tenant":row["targetTenantId"]="foreign-tenant";break;
                case "target":row["targetObjectId"]="foreign-object";break;
                case "missing-target":row.Remove("targetObjectId");break;
                case "stale":evidence["Source"]!["HeadSequence"]=0;break;
                case "reused":evidence["CaseRequestSha256"]=new string('f',64);break;
                case "cross-incarnation":evidence["Source"]!["SourceIncarnation"]="old-incarnation";break;
                case "legacy":row.Remove("authorizationEvidence");break;
                case "same-tenant-foreign":
                    string foreign=new('e',64);row["targetId"]=foreign;evidence["TargetId"]=foreign;
                    mutationTrust=mutation.Trust with { TargetMappings=mutation.Trust.TargetMappings.ToDictionary(pair=>pair.Key,_=>foreign) };break;
            }
            audit.Transform=value=>value with { TargetMutations=new JsonArray(row.DeepClone()),Facts=new JsonObject { ["nativeStageBoundary"]=value.Facts!["nativeStageBoundary"]!.DeepClone(),["nativeMutationSources"]=mutation.NativeSources.DeepClone() } };
        }
        var rotationFaults=new JsonObject();var envelope=Envelope(authority,caseId);var decision=Decision(caseId);var inventory=caseId=="S1.rotation-invalidation"?Rotation(originals,audit,original,rotationFaults):Inventory(originals,audit,caseId,original);
        using var exportFixture=new ExportQualificationFixture();var exportWorkflow=new QualificationExportCaseWorkflow(exportFixture.Engine(),originals,new Dictionary<string,ExportQualificationInput>{{caseId,exportFixture.Input(original["caseTargetId"]!.GetValue<string>())}});
        using var capabilityKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);var capability=Capability(caseId,original,originals,capabilityKey);
        IQualificationObservedOriginalReader rawReader=caseId=="S4.capability-canonical-14-fields"?new QualificationCapabilityOriginalReader(originals,audit,capability):caseId.StartsWith("S4.capability-substitution.",StringComparison.Ordinal)?new QualificationDeniedOriginalReader(originals,audit,capability.ReadVerifyAsync):caseId=="S1.reject.missing-config"?new QualificationMissingConfigurationOriginalReader(originals,audit):IsExport(caseId)?new QualificationExportOriginalReader(originals,audit,exportWorkflow):caseId.StartsWith("S3.",StringComparison.Ordinal)?new QualificationDecisionOriginalReader(originals,audit,decision):(caseId=="S1.rotation-invalidation" || caseId.StartsWith("S1.inventory.",StringComparison.Ordinal))?inventory.Reader:new QualificationDeniedOriginalReader(originals,audit,envelope.ReadVerifyAsync);
        var observerRole=new QualificationOwnerRole("observer","observer","fixture-v1",observerKey.ExportSubjectPublicKeyInfoPem());
        using var sourceAuthority=new IndependentAuthorityReceiverTests.Fixture();sourceAuthority.State=sourceAuthority.State with { Callers=sourceAuthority.State.Callers.Select(value=>value with { Role="observer",Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original","qualification-observation-original-preflight"] }).ToArray() };
        var sourceReceiver=new QualificationOwnerOperationHandler(observerRole,observerKey,new InstalledQualificationOriginalStore(sourceAuthority.Client()),new Dictionary<string,IQualificationOwnerCaseDriver>(),TimeProvider.System,rawReader);
        using var sourceHost=new IndependentAuthorityReceiverTests.Fixture(handlers:[sourceReceiver]);
        sourceHost.State=sourceHost.State with { QualificationBindings=authority.State.QualificationBindings,QualificationCases=authority.State.QualificationCases,
            Callers=sourceHost.State.Callers.Select(value=>value with { Role="observer-reader",Methods=["qualification-observer-source-preflight","qualification-observer-read-source","qualification-observer-lookup-source"] }).ToArray() };
        var sourceReader=new DaprQualificationObservedOriginalReader(sourceHost.Client());
        byte[] anchor=Encoding.ASCII.GetBytes(observerKey.ExportSubjectPublicKeyInfoPem());
        var trust=new QualificationProofBinding("observer","fixture-v1",anchor,Convert.ToHexStringLower(SHA256.HashData(anchor)));
        var workflows=new[]{caseId.StartsWith("S4.capability",StringComparison.Ordinal)?QualificationRuntimeWorkflowCatalog.Retained(caseId,originals,capability.ExecuteAsync,capability.LookupAsync):caseId=="S1.reject.missing-config"?new QualificationMissingConfigurationWorkflow(originals).Registration:IsExport(caseId)?exportWorkflow.Registrations().Single():caseId.StartsWith("S3.",StringComparison.Ordinal)?decision.Registrations(originals).Single():(caseId=="S1.rotation-invalidation" || caseId.StartsWith("S1.inventory.",StringComparison.Ordinal))?inventory.Workflow:QualificationRuntimeWorkflowCatalog.Retained(caseId,originals,envelope.ExecuteAsync)};
        var actualWorkflow=workflows.Single();workflows=[actualWorkflow with {
            Execute=async (value,token)=> { var result=await actualWorkflow.Execute(value,token);if(result is not null) await audit.RecordCaseBoundaryAsync(false,token);return result; },
            Lookup=async (value,token)=> { var result=await actualWorkflow.Lookup(value,token);if(result is not null) await audit.RecordCaseBoundaryAsync(true,token);return result; },
            Resume=actualWorkflow.Resume is null?null:async (value,token)=> { var result=await actualWorkflow.Resume(value,token);if(result is not null) await audit.RecordCaseBoundaryAsync(false,token);return result; } }];
        var drivers=QualificationDriverComposition.Create(workflows,originals,audit,sourceReader,trust,new Dictionary<string,IReadOnlyList<string>>{{caseId,[]}},mutationTrust:mutationTrust);
        (await drivers[caseId].PreflightAsync(original,TestContext.Current.CancellationToken)).ShouldBeEmpty();audit.Reads.ShouldBe(0);audit.Controls.ShouldBe(0);
        (await originals.LookupResultAsync("executor",original["requestId"]!.GetValue<string>()+":case-result",TestContext.Current.CancellationToken)).ShouldBeNull();
        sourceAuthority.MutationSaves.ShouldBe(0);sourceAuthority.OrderingSaves.ShouldBeGreaterThan(0);
        if(caseId=="S1.reject.missing-config" && mutationFault is null)
        {
            var enrolledCallers=sourceAuthority.State.Callers;sourceAuthority.State=sourceAuthority.State with { Callers=enrolledCallers.Select(value=>value with { Methods=value.Methods.Where(method=>method!="qualification-begin-original").ToArray() }).ToArray() };
            (await drivers[caseId].PreflightAsync(original,TestContext.Current.CancellationToken)).ShouldContain("InstalledIndependentObservationOriginalReadWriteAndArchiveCapabilities");
            (await drivers[caseId].ExecuteAsync(original,TestContext.Current.CancellationToken)).ShouldBeNull();audit.Effects.ShouldBe(0);sourceAuthority.MutationSaves.ShouldBe(0);sourceAuthority.OrderingSaves.ShouldBeGreaterThan(0);
            sourceAuthority.State=sourceAuthority.State with { Callers=enrolledCallers };
            audit.WithholdSurface("Logs");
            (await drivers[caseId].PreflightAsync(original,TestContext.Current.CancellationToken)).ShouldContain("SelectedIndependentCompleteNativeSurfaceSnapshotProtocol");
            (await drivers[caseId].ExecuteAsync(original,TestContext.Current.CancellationToken)).ShouldBeNull();audit.Effects.ShouldBe(0);audit.Reads.ShouldBe(0);audit.RestoreSurface("Logs");
        }
        var executor=new QualificationOwnerOperationHandler(new("executor","executor","fixture-v1",executorKey.ExportSubjectPublicKeyInfoPem()),executorKey,originals,drivers,TimeProvider.System);
        var observer=new QualificationOwnerOperationHandler(observerRole,observerKey,originals,drivers,TimeProvider.System);
        JsonObject? lostInner=null;
        if(sourceFault.StartsWith("rotation:",StringComparison.Ordinal))
        {
            await Should.ThrowAsync<InvalidOperationException>(()=>executor.HandleAsync("qualification-executor-execute",JsonSerializer.SerializeToElement(original),authority.State.Callers[0],authority.State,TestContext.Current.CancellationToken));
            interruption.Armed.ShouldBeFalse();
            int priorRotations=rotationFaults["nativeApplyCount"]?.GetValue<int>()??0,priorInvalidations=rotationFaults["invalidations"]?.GetValue<int>()??0;
            await Should.ThrowAsync<InvalidOperationException>(()=>executor.HandleAsync("qualification-executor-lookup",JsonSerializer.SerializeToElement(original),authority.State.Callers[0] with { Revoked=true },authority.State,TestContext.Current.CancellationToken));
            var changedCase=JsonNode.Parse(authority.State.QualificationCases![caseId].GetRawText())!.AsObject();changedCase["caseTargetId"]=new string('f',64);
            var changedPolicy=authority.State with { QualificationCases=new(authority.State.QualificationCases) { [caseId]=JsonSerializer.SerializeToElement(changedCase,CustodyBindingContract.Json) } };
            await Should.ThrowAsync<InvalidOperationException>(()=>executor.HandleAsync("qualification-executor-lookup",JsonSerializer.SerializeToElement(original),authority.State.Callers[0],changedPolicy,TestContext.Current.CancellationToken));
            (rotationFaults["nativeApplyCount"]?.GetValue<int>()??0).ShouldBe(priorRotations);(rotationFaults["invalidations"]?.GetValue<int>()??0).ShouldBe(priorInvalidations);
            // An admitted step with no retained native outcome remains pending; no guessed first dispatch.
            if(sourceFault.EndsWith(":admitted",StringComparison.Ordinal))
            {
                await Should.ThrowAsync<InvalidOperationException>(()=>executor.HandleAsync("qualification-executor-lookup",JsonSerializer.SerializeToElement(original),authority.State.Callers[0],authority.State,TestContext.Current.CancellationToken));
                rotationFaults["nativeApplyCount"]!.GetValue<int>().ShouldBe(sourceFault.Contains("install-original",StringComparison.Ordinal)?0:sourceFault.Contains("install-successor",StringComparison.Ordinal)?1:2);
                (rotationFaults["invalidations"]?.GetValue<int>()??0).ShouldBe(0);return;
            }
            // Actual owning-role lookup must reach the workflow's explicitly safe continuation.
            var recovered=await executor.HandleAsync("qualification-executor-lookup",JsonSerializer.SerializeToElement(original),authority.State.Callers[0],authority.State,TestContext.Current.CancellationToken);
            recovered.ShouldNotBeNull();
        }
        else if(sourceFault!="none")
        {
            if(sourceFault is "lost-reply" or "retained-next-boundary") sourceHost.DropNextResponseMethod="qualification-observer-read-source";
            else if(sourceFault=="remote-unknown") audit.FailNextRead=true;
            else { sourceHost.DropNextRequestMethod="qualification-observer-read-source";sourceHost.DropRequestStage=sourceFault=="next-lookup-boundary"?"AfterRestore":"AfterEffect"; }
            await Should.ThrowAsync<InvalidOperationException>(()=>executor.HandleAsync("qualification-executor-execute",JsonSerializer.SerializeToElement(original),authority.State.Callers[0],authority.State,TestContext.Current.CancellationToken));
            int measuredAtLoss=audit.Reads;audit.Effects.ShouldBe(1);
            if(sourceFault is "lost-reply" or "retained-next-boundary")
            {
                audit.Reads.ShouldBe(1);lostInner=await new InstalledQualificationOriginalStore(sourceAuthority.Client()).LookupResultAsync("observer",QualificationObservationStage.Locator(original["requestId"]!.GetValue<string>(),"AfterEffect"),TestContext.Current.CancellationToken);lostInner.ShouldNotBeNull();
            }
            else if(sourceFault is "before-delivery" or "next-restart-boundary") audit.Reads.ShouldBe(0);
            else if(sourceFault=="next-lookup-boundary") audit.Reads.ShouldBe(2);
            else audit.Reads.ShouldBe(1);
            if(sourceFault.Contains("boundary",StringComparison.Ordinal)) await originals.BeginAsync("executor",original["requestId"]!.GetValue<string>()+(sourceFault=="next-lookup-boundary"?":lookup-stage":":restart"),original,TestContext.Current.CancellationToken);
            if(sourceFault is "remote-unknown" or "next-restart-boundary" or "next-lookup-boundary" or "retained-next-boundary")
            {
                await Should.ThrowAsync<InvalidOperationException>(()=>executor.HandleAsync("qualification-executor-execute",JsonSerializer.SerializeToElement(original),authority.State.Callers[0],authority.State,TestContext.Current.CancellationToken));
                audit.Reads.ShouldBe(measuredAtLoss);audit.Effects.ShouldBe(1);
                if(sourceFault=="retained-next-boundary")
                {
                    var recoveredStage=await originals.LookupResultAsync("executor",original["requestId"]!.GetValue<string>()+":observation-stage-v2:AfterEffect",TestContext.Current.CancellationToken);
                    recoveredStage.ShouldNotBeNull();JsonNode.DeepEquals(recoveredStage["sourceProof"],lostInner).ShouldBeTrue();
                }
                return;
            }
        }
        if(mutationFault is not (null or "genuine" or "same-tenant-foreign"))
        {
            await Should.ThrowAsync<InvalidOperationException>(()=>executor.HandleAsync("qualification-executor-execute",JsonSerializer.SerializeToElement(original),authority.State.Callers[0],authority.State,TestContext.Current.CancellationToken));
            audit.Effects.ShouldBe(1);audit.Controls.ShouldBe(2);audit.Reads.ShouldBe(4);
            (await originals.LookupResultAsync("executor",original["requestId"]!.GetValue<string>()+":missing-config",TestContext.Current.CancellationToken)).ShouldNotBeNull();return;
        }
        JsonObject execution=(JsonObject)(await executor.HandleAsync("qualification-executor-execute",JsonSerializer.SerializeToElement(original),authority.State.Callers[0],authority.State,TestContext.Current.CancellationToken))!;
        authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { Role="observer",Methods=value.Methods.Concat(["qualification-read-original:executor"]).ToArray() }).ToArray() };
        JsonObject observation=(JsonObject)(await observer.HandleAsync("qualification-observer-observe",JsonSerializer.SerializeToElement(original),authority.State.Callers[0] with { Role="observer" },authority.State,TestContext.Current.CancellationToken))!;
        audit.Controls.ShouldBe(2);audit.Reads.ShouldBe(4);
        execution["payload"]!["outcome"]!.GetValue<string>().ShouldBe(caseId is "S3.exact-independent-decision" or "S3.aes256gcm" or "S3.es256-offline-anchor" or "S4.capability-canonical-14-fields"?"Verified":caseId=="S1.rotation-invalidation"?"Invalidated":caseId.StartsWith("S1.inventory.",StringComparison.Ordinal)?"Resolved":"Denied");
        observation["payload"]!["metrics"]!["persistedProofCount"]!.GetValue<long>().ShouldBe(4);
        var anchors=new Dictionary<string,QualificationProofBinding> { ["executor"]=Binding(executorKey,"executor"),["observer"]=trust };
        var verifier=new QualificationProofVerifier(anchors);
        verifier.Verify("executor",request,execution,TestContext.Current.CancellationToken).ShouldBeTrue();
        verifier.Verify("observer",request,observation,TestContext.Current.CancellationToken).ShouldBeTrue();
        if(mutationFault=="same-tenant-foreign") { observation["payload"]!["metrics"]!["foreignEffects"]!.GetValue<long>().ShouldBe(1);await VerifyPythonAsync(request,execution,observation,executorKey,observerKey,false);return; }
        if(installedRole is not null)
            await InstalledQualificationRoleServingFixture.VerifyAsync(installedRole,request,authority,originals,drivers,executorKey,observerKey,executor,observer,TestContext.Current.CancellationToken);
        var changed=(JsonObject)original.DeepClone();changed["targetCommit"]=new string('f',40);
        await Should.ThrowAsync<InvalidOperationException>(()=>executor.HandleAsync("qualification-executor-execute",JsonSerializer.SerializeToElement(changed),authority.State.Callers[0] with { Role="executor" },authority.State,TestContext.Current.CancellationToken));
        changed=(JsonObject)original.DeepClone();changed["requestId"]=new string('f',64);
        (await drivers[caseId].ObserveAsync(changed,TestContext.Current.CancellationToken)).ShouldBeNull();
        audit.Effects.ShouldBe(1);
        var retained=(await originals.LookupResultAsync("executor",original["requestId"]!.GetValue<string>()+":observations-v2",TestContext.Current.CancellationToken))!;
        if(lostInner is not null) JsonNode.DeepEquals(lostInner,retained["snapshots"]![0]!["sourceProof"]).ShouldBeTrue();
        var tampered=(JsonObject)retained.DeepClone();tampered["snapshots"]![0]!["sourceProof"]!["OriginalReceiptSha256"]=new string('f',64);
        new QualificationMeasuredCaseProjector(trust,audit,new Dictionary<string,IReadOnlyList<string>>{{caseId,[]}}).Project("observer",original,tampered).ShouldBeNull();
        if(caseId=="S1.reject.missing-config" && mutationFault is null)
        {
            var projector=new QualificationMeasuredCaseProjector(trust,audit,new Dictionary<string,IReadOnlyList<string>>{{caseId,[]}});
            var retainedStage=retained["snapshots"]![0]!["sourceProof"]!.Deserialize<QualificationObservedOriginal>(CustodyBindingContract.Json)!;
            var exactBoundary=retainedStage.StageEvidence!.Boundary;int sourceReads=audit.Reads;
            var repeated=(QualificationObservedOriginal)(await sourceReceiver.HandleAsync("qualification-observer-read-source",JsonSerializer.SerializeToElement(new { original,observationContext=exactBoundary },CustodyBindingContract.Json),authority.State.Callers[0] with { Role="observer-reader" },authority.State,TestContext.Current.CancellationToken))!;
            JsonNode.DeepEquals(JsonSerializer.SerializeToNode(repeated,CustodyBindingContract.Json),retained["snapshots"]![0]!["sourceProof"]).ShouldBeTrue();audit.Reads.ShouldBe(sourceReads);
            await Should.ThrowAsync<InvalidOperationException>(()=>sourceReceiver.HandleAsync("qualification-observer-read-source",JsonSerializer.SerializeToElement(new { original,observationContext=exactBoundary with { PredecessorSha256=new string('f',64) } },CustodyBindingContract.Json),authority.State.Callers[0] with { Role="observer-reader" },authority.State,TestContext.Current.CancellationToken));
            foreach(string defect in new[]{"stage","control","predecessor","incarnation","revision","same-proof","receipt-null","receipt-scalar","receipt-missing","receipt-typed","early-leak","early-forbidden","early-foreign","early-revoked"})
            {
                var counterexample=(JsonObject)retained.DeepClone();var snapshot=counterexample["snapshots"]![0]!.AsObject();
                if(defect=="revision") snapshot["authorityRevision"]="unsigned-substitution";
                else if(defect=="same-proof") { foreach(var node in counterexample["snapshots"]!.AsArray()) node!["sourceProof"]=snapshot["sourceProof"]!.DeepClone(); }
                else if(defect is "stage" or "control" or "predecessor" or "incarnation")
                {
                    if(defect=="control") snapshot=counterexample["snapshots"]![1]!.AsObject();
                    var inner=snapshot["sourceProof"]!.AsObject();string field=defect switch { "stage"=>"Stage","control"=>"ControlOperationId","predecessor"=>"PredecessorSha256",_=>"SourceIncarnation" };
                    inner["StageEvidence"]![defect=="incarnation"?"Native":"Boundary"]![field]="substituted";
                    if(defect is "incarnation" or "control")
                    {
                        snapshot["source"]!["nativeStageBoundary"]![field]="substituted";inner["StageEvidence"]!["Native"]![field]="substituted";
                        Resign(counterexample,observerKey); // Independently signed substituted native facts must still correlate with the actual control original.
                    }
                    else SignInner(inner,observerKey);
                }
                else
                {
                    var source=snapshot["source"]!.AsObject();
                    if(defect=="early-leak") source["surfaceScans"]![0]!["matchCount"]=1;
                    else if(defect.StartsWith("early-",StringComparison.Ordinal)) source["targetMutations"]!.AsArray().Add(new JsonObject { ["nativeMutationId"]="native-unsafe-effect",["authorizationReceiptId"]=defect=="early-forbidden"?null:new string('a',64),["targetTenantId"]=defect=="early-foreign"?"foreign":"tenant",["keyVersion"]="v1",["productDisposition"]="Closed",["epochId"]="epoch",["keyState"]=defect=="early-revoked"?"Revoked":"Active" });
                    else
                    {
                        var history=counterexample["snapshots"]![3]!["source"]!["receipts"]!.AsArray();
                        if(defect=="receipt-null") history.Add((JsonNode?)null);else if(defect=="receipt-scalar") history.Add(3);
                        else { var invalid=(JsonObject)history[0]!.DeepClone();if(defect=="receipt-missing") invalid.Remove("kind");else invalid["position"]="wrong";history.Add(invalid); }
                    }
                    Resign(counterexample,observerKey);
                }
                var projected=projector.Project("observer",original,counterexample);
                if(defect=="early-leak")
                {
                    string metric=defect switch { "early-leak"=>"leakCount","early-forbidden"=>"forbiddenEffects","early-foreign"=>"foreignEffects",_=>"revokedEffects" };
                    if(QualificationProtocolSchema.MetricNames(caseId).Contains(metric,StringComparer.Ordinal)) { projected.ShouldNotBeNull(defect);projected["metrics"]![metric]!.GetValue<long>().ShouldBeGreaterThan(0,defect); }
                    else projected.ShouldBeNull(defect);
                }
                else projected.ShouldBeNull(defect);
            }
        }
        await VerifyPythonAsync(request,execution,observation,executorKey,observerKey);
        if(caseId=="S1.reject.missing-config" && sourceFault=="none")
        {
            await ConcreteJointSecretsAdapterRegression.VerifyAsync(original,execution,observation,executorKey,observerKey);
            audit.Effects.ShouldBe(1);audit.Reads.ShouldBe(4);
        }
        if(caseId=="S1.rotation-invalidation")
        {
            foreach(string fault in new[]{"time","version","retention","anchor"})
            {
                rotationFaults["fault"]=fault;(await rawReader.ReadAsync(original,TestContext.Current.CancellationToken)).ShouldBeNull(fault);
                var changedObservation=(JsonObject)observation.DeepClone();var state=changedObservation["payload"]!["snapshots"]![0]!["state"]!;
                if(fault=="time") state["rotation"]!["rotatedAt"]=state["rotation"]!["rotatedAt"]!.GetValue<long>()+1;
                else if(fault=="version") state["rotation"]!["currentKeyVersion"]="changed-version";
                else if(fault=="retention") state["rotation"]!["oldVerifyUntil"]=state["rotation"]!["rotatedAt"]!.DeepClone();
                else state["rotation"]!["publicAnchorId"]="";
                foreach(var snapshot in changedObservation["payload"]!["snapshots"]!.AsArray()) { snapshot!["state"]=state.DeepClone();snapshot["stateSha256"]=QualificationProtocolSchema.Digest(state); }
                foreach(var trace in changedObservation["payload"]!["trace"]!.AsArray()) trace!["stateSha256"]=QualificationProtocolSchema.Digest(state);
                changedObservation["signature"]=Convert.ToBase64String(observerKey.SignData(QualificationProofVerifier.CanonicalBytes(changedObservation["payload"]!),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
                await VerifyPythonAsync(request,execution,changedObservation,executorKey,observerKey,false);
            }
        }
    }

    [Fact]
    public async Task MissingActualReadInputsRefuseBeforeOriginalAdmissionOrEffects()
    {
        using var authority=new IndependentAuthorityReceiverTests.Fixture();using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var originals=new InstalledQualificationOriginalStore(authority.Client());var original=QualificationHostTests.Request();
        using var audit=new FileAuditFixture(originals,original);
        var drivers=QualificationDriverComposition.Create([new QualificationMissingConfigurationWorkflow(originals).Registration],originals,audit,
            new QualificationMissingConfigurationOriginalReader(originals,audit),Binding(key,"observer"),new Dictionary<string,IReadOnlyList<string>>{{"S1.reject.missing-config",["InstalledIndependentEventOutboxSnapshotAndLeakageReaders"]}});
        (await drivers["S1.reject.missing-config"].PreflightAsync(original,TestContext.Current.CancellationToken)).ShouldContain("InstalledIndependentEventOutboxSnapshotAndLeakageReaders");
        (await drivers["S1.reject.missing-config"].ExecuteAsync(original,TestContext.Current.CancellationToken)).ShouldBeNull();
        authority.Saves.ShouldBe(0);audit.Reads.ShouldBe(0);audit.Controls.ShouldBe(0);
    }

    private static void SignInner(JsonObject proof,ECDsa key)
    { proof["Signature"]=Convert.ToBase64String([]);proof["Signature"]=Convert.ToBase64String(key.SignData(QualificationProofVerifier.CanonicalBytes(proof),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation)); }
    private static void Resign(JsonObject observations,ECDsa key)
    {
        var snapshots=observations["snapshots"]!.AsArray();
        for(int i=0;i<snapshots.Count;i++)
        {
            var snapshot=snapshots[i]!.AsObject();var proof=snapshot["sourceProof"]!.AsObject();proof["Source"]=snapshot["source"]!.DeepClone();
            if(i>0) proof["StageEvidence"]!["Boundary"]!["PredecessorSha256"]=QualificationProtocolSchema.Digest(snapshots[i-1]!["sourceProof"]!);
            if(proof["Source"]?["readonlyLookupWitness"]?["body"] is JsonObject witness)
            {
                witness["precedingObservationSha256"]=proof["StageEvidence"]!["Boundary"]!["PredecessorSha256"]!.DeepClone();
                var returned=(JsonObject)proof["Source"]!.DeepClone();returned.Remove("readonlyLookupWitness");returned.Remove("independentReceiptPositions");witness["returnedSourceSha256"]=QualificationProtocolSchema.Digest(returned);
                snapshot["source"]=proof["Source"]!.DeepClone();
            }
            SignInner(proof,key);snapshot["stateSha256"]=QualificationProtocolSchema.Digest(snapshot["source"]!);
        }
    }

    private sealed class InterruptOriginalStore(IQualificationOriginalStore inner,string fault):IQualificationOriginalStore
    {
        internal bool Armed=fault.StartsWith("rotation:",StringComparison.Ordinal);
        private void Check(string id,string point)
        { if(Armed && fault=="rotation:"+id[(id.LastIndexOf(':')+1)..]+":"+point) { Armed=false;throw new InvalidOperationException("Isolated durable inner-stage loss"); } }
        public async Task<bool> BeginAsync(string role,string id,JsonObject value,CancellationToken token)
        { bool result=await inner.BeginAsync(role,id,value,token);Check(id,"admitted");return result; }
        public async Task RetainAsync(string role,string id,JsonObject value,CancellationToken token)
        { Check(id,"before-retain");await inner.RetainAsync(role,id,value,token);Check(id,"retained"); }
        public Task<JsonObject?> LookupResultAsync(string role,string id,CancellationToken token)=>inner.LookupResultAsync(role,id,token);
        public Task<JsonObject?> LookupRequestAsync(string role,string id,CancellationToken token)=>inner.LookupRequestAsync(role,id,token);
        public Task<QualificationOriginalLookup> ReadOriginalAsync(string role,string id,CancellationToken token)=>inner.ReadOriginalAsync(role,id,token);
        public Task<QualificationOriginalAdmissionState> ReadAdmissionAsync(string role,string id,JsonObject value,CancellationToken token)=>inner.ReadAdmissionAsync(role,id,value,token);
    }

    private static bool IsExport(string caseId)=>caseId is "S3.aes256gcm" or "S3.es256-offline-anchor" || caseId.StartsWith("S3.crypto-substitution.",StringComparison.Ordinal);

    private static (QualificationSourceWorkflow Workflow,IQualificationObservedOriginalReader Reader) Inventory(IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit,string caseId,JsonObject original)
    {
        if(!caseId.StartsWith("S1.inventory.",StringComparison.Ordinal)) return (null!,null!);
        var key=new PlatformKeyVersion("tenant",PlatformKeyPurpose.TenantKek,"fixture-kek","v1","provider-target","outside-authority",null,null);
        var snapshot=new PlatformKeyInventorySnapshot("tenant",PlatformKeyPurpose.TenantKek,"fixture-kek",7,[new(key,PlatformHmacKeyState.Active,7)],[]);
        var actor=Substitute.For<IPlatformKeyInventoryActor>();actor.ReadAsync(key).Returns(snapshot);
        var authority=Substitute.For<IPlatformKeyInventoryAuthority>();
        authority.AuthorizeOperationAsync(Arg.Any<PlatformKeyVersion>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(call=>call.ArgAt<PlatformKeyVersion>(0)==key && call.ArgAt<string>(1)=="ReadKeyInventory");
        string digest=CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(snapshot));
        authority.ValidateStateAsync(Arg.Any<PlatformKeyVersion>(),Arg.Any<long>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(call=>call.ArgAt<PlatformKeyVersion>(0)==key && call.ArgAt<long>(1)==7 && call.ArgAt<string>(2)==digest);
        var inputs=new Dictionary<string,JsonElement>{{caseId,JsonSerializer.SerializeToElement(new { enrolledScope=original["enrolledScope"],key },CustodyBindingContract.Json)}};
        var source=new QualificationRuntimeSourceDriver(actor,authority,null!,null!,inputs);
        return (QualificationRuntimeWorkflowCatalog.Create(originals,[caseId],source).Single(),new QualificationInventoryOriginalReader(actor,authority,originals,audit,new Dictionary<string,PlatformKeyVersion>{{caseId,key}}));
    }

    private static (QualificationSourceWorkflow Workflow,IQualificationObservedOriginalReader Reader) Rotation(IQualificationOriginalStore originals,IQualificationNativeCaseAuditReader audit,JsonObject original,JsonObject faults)
    {
        var scope=original["enrolledScope"]!;string tenant=scope["tenantId"]!.GetValue<string>();
        var old=new PlatformKeyVersion(tenant,PlatformKeyPurpose.DigestKey,"fixture-digest",scope["initialKeyVersion"]!.GetValue<string>(),"native","outside-authority","rotation-anchor","anchor-v1");var next=old with { Version=scope["successorKeyVersion"]!.GetValue<string>() };
        var initial=new PlatformKeyInventoryChange("original",0,PlatformKeyInventoryAction.InstallCurrent,old,"native-original");var successor=new PlatformKeyInventoryChange("successor",1,PlatformKeyInventoryAction.InstallCurrent,next,"native-successor");
        PlatformKeyInventorySnapshot state=new(tenant,old.Purpose,old.KeyAlias,0,[],[]);var actor=Substitute.For<IPlatformKeyInventoryActor>();int effects=0;
        actor.ApplyAsync(Arg.Any<PlatformKeyInventoryChange>()).Returns(call=>
        {
            var change=call.Arg<PlatformKeyInventoryChange>();state.Revision.ShouldBe(change.ExpectedInventoryRevision);effects++;faults["nativeApplyCount"]=effects;effects.ShouldBeLessThanOrEqualTo(2);
            var signal=new PlatformKeyInventorySignal(change.OperationId,CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(change)),state.Revision+1,change.Action,change.Key);
            state=state with { Revision=signal.InventoryRevision,Signals=state.Signals.Append(signal).ToArray(),Versions=state.Versions.Select(value=>value with { State=PlatformHmacKeyState.Retained }).Append(new(change.Key,PlatformHmacKeyState.Active,signal.InventoryRevision)).ToArray() };return signal;
        });
        actor.ReadAsync(Arg.Any<PlatformKeyVersion>()).Returns(_=>state);
        var authority=Substitute.For<IPlatformKeyInventoryAuthority>();authority.AuthorizeOperationAsync(Arg.Any<PlatformKeyVersion>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);authority.ValidateStateAsync(Arg.Any<PlatformKeyVersion>(),Arg.Any<long>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(call=>call.Arg<long>()==state.Revision && call.Arg<string>()==CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(state)));
        var profiles=Substitute.For<IPlatformSigningProfileProvider>();DateTimeOffset now=DateTimeOffset.UtcNow;
        profiles.GetCurrent().Returns(new PlatformSigningProfile("fixture-r1","fixture-issuer","fixture-audience",now.AddHours(-1),now.AddHours(1),TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(600),TimeSpan.FromSeconds(86400),TimeSpan.FromSeconds(604800)));
        var keys=Substitute.For<IPlatformHmacKeyProvider>();keys.ResolveAsync(Arg.Any<PlatformHmacScope>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(call=>ValueTask.FromResult(new PlatformHmacKeyResolution(CustodyStatus.Succeeded,new(call.Arg<PlatformHmacScope>(),new(old.Version,PlatformHmacKeyState.Active,now.AddHours(-1),now.AddHours(1),null),new byte[32]))));
        var invalidation=Substitute.For<IQualificationSafetyInvalidationPort>();invalidation.PreflightAsync(Arg.Any<QualificationInventoryRotationInput>(),Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());invalidation.PreflightRotationOriginalAsync(Arg.Any<QualificationInventoryRotationInput>(),Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());
        string receipt=new('b',64);faults["nativeApplyCount"]=0;
        invalidation.PublishAsync(Arg.Any<PlatformKeyInventorySignal>(),Arg.Any<CancellationToken>()).Returns(_=> { faults["invalidations"]=(faults["invalidations"]?.GetValue<int>()??0)+1;faults["invalidations"]!.GetValue<int>().ShouldBe(1);return receipt; });
        invalidation.LookupAsync(Arg.Any<PlatformKeyInventorySignal>(),Arg.Any<CancellationToken>()).Returns(_=>faults["invalidations"]?.GetValue<int>()==1?receipt:null);
        invalidation.ReadRotationOriginalAsync(Arg.Any<PlatformKeyInventorySignal>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            var result=new QualificationRotationOriginal(call.Arg<PlatformKeyInventorySignal>(),1700000000000,1700604800000,next.PublicAnchorId!,next.PublicAnchorVersion!,receipt);
            return faults["fault"]?.GetValue<string>() switch { "time"=>result with { RotatedAt=result.RotatedAt+1 },"version"=>result with { Signal=result.Signal with { Key=next with { Version="changed" } } },"retention"=>result with { OldVerifyUntil=result.OldVerifyUntil+1 },"anchor"=>result with { PublicAnchorId="changed" },_=>result };
        });
        invalidation.ReadAsync(next,Arg.Any<CancellationToken>()).Returns(_=>new[]{new QualificationSafetyVerdict("verdict",tenant,state.Revision,"Current",receipt)});
        var workflow=new QualificationInventoryRotationWorkflow(actor,authority,new(keys,profiles,TimeProvider.System),originals,new(initial,successor,new(tenant,PlatformHmacPurpose.ContentDigest),["fixture"],QualificationProtocolSchema.Digest(scope)),invalidation);
        return (workflow.Registration,new QualificationInventoryRotationOriginalReader(originals,audit,workflow));
    }

    private static QualificationCapabilityCaseWorkflow Capability(string caseId,JsonObject original,IQualificationOriginalStore originals,ECDsa privateSigner)
    {
        var scope=original["enrolledScope"]!;
        var payload=new DeletionBatchCapabilityV1("capability-issuer","protection-owner",scope["tenantId"]!.GetValue<string>(),"request",scope["sealId"]!.GetValue<string>(),"accepted",1,
            scope["batchId"]!.GetValue<string>(),scope["manifestDigest"]!.GetValue<string>(),"guard",1,1,1,scope["initialKeyVersion"]!.GetValue<string>());
        var profile=new DeletionCapabilityTrustProfile(payload.Issuer,payload.Audience,payload.TenantId,payload.CapabilityKeyVersion,"public-capability-anchor","v1");
        var signed=new DeletionCapabilitySigningOutcome(DeletionBatchCapabilityCodec.SigningRequestId(payload),payload,DeletionCapabilitySigningState.Signed,
            DeletionBatchCapabilityCodec.Sign(payload,profile,privateSigner),profile.PublicAnchorId,profile.PublicAnchorVersion);
        var actor=Substitute.For<IDeletionCapabilitySigningActor>();actor.SignAsync(payload).Returns(signed);actor.LookupAsync(payload).Returns(signed);
        var trust=Substitute.For<IDeletionCapabilitySigningTrustProvider>();trust.ResolveAsync(payload.TenantId,"DeletionBatchCapabilitySigningKey",payload.CapabilityKeyVersion,Arg.Any<CancellationToken>())
            .Returns(new DeletionCapabilityPublishedTrust(payload.TenantId,"DeletionBatchCapabilitySigningKey",payload.CapabilityKeyVersion,payload.Issuer,payload.Audience,profile.PublicAnchorId,profile.PublicAnchorVersion,privateSigner.ExportSubjectPublicKeyInfo(),1,true));
        return new(actor,trust,originals,new Dictionary<string,DeletionBatchCapabilityV1>{{caseId,payload}});
    }

    private static QualificationDecisionCaseWorkflow Decision(string caseId)
    {
        // The original fixture approval is independently issued before the workflow is constructed; the workflow receives only public verification material.
        using var issuer=ECDsa.Create(ECCurve.NamedCurves.nistP256);DateTimeOffset now=DateTimeOffset.UtcNow.AddSeconds(-1);
        var manifest=new DecisionAuthorityManifest("fixture-independent-issuer","fixture-decision-audience","decision","contract",2,3,null,"root-policy",["evaluation"],[new("Product","independent-actor","independent-role-authority","binding-v1","original-evidence")],now,now.AddMinutes(1),"issuer-v1");
        var basis=new DecisionAuthorityExpectedBasis("decision","contract",2,3,null,"root-policy",["evaluation"],["Product"]);
        var profile=new DecisionAuthorityPublishedProfile(manifest.Issuer,manifest.Audience,1,"issuer-v1","independent-public-anchor","v1",issuer.ExportSubjectPublicKeyInfo(),false,true,"DecisionApprovalVerificationOnly",TimeSpan.FromMinutes(1),now.AddDays(-1),now.AddDays(1),["platform-custodian","recorder"]);
        string artifact=DetachedEs256JwsCore.Sign(DecisionAuthorityManifestCodec.Header(profile),DecisionAuthorityManifestCodec.Canonical(manifest),issuer);
        var authority=Substitute.For<IIndependentDecisionAuthority>();
        authority.ResolveAsync(Arg.Any<DecisionAuthorityExpectedBasis>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(profile);
        authority.VerifyExpectedBasisAsync(Arg.Any<DecisionAuthorityExpectedBasis>(),Arg.Any<DecisionAuthorityPublishedProfile>(),Arg.Any<CancellationToken>()).Returns(call=>(JsonSerializer.Serialize(call.ArgAt<DecisionAuthorityExpectedBasis>(0))==JsonSerializer.Serialize(basis) || caseId=="S3.decision-reject.contract" && JsonSerializer.Serialize(call.ArgAt<DecisionAuthorityExpectedBasis>(0))==JsonSerializer.Serialize(basis with { ContractId="other-independently-expected-contract" })));
        authority.VerifyApproverAsync(Arg.Any<DecisionAuthorityExpectedBasis>(),Arg.Any<DecisionAuthorityApprover>(),Arg.Any<DateTimeOffset>(),Arg.Any<CancellationToken>()).Returns(call=>call.ArgAt<DecisionAuthorityApprover>(1)==manifest.Approvers[0]);
        authority.VerifyAuthorityBoundaryAsync(Arg.Any<DecisionAuthorityExpectedBasis>(),Arg.Any<DecisionAuthorityManifest>(),Arg.Any<DecisionAuthorityPublishedProfile>(),Arg.Any<CancellationToken>()).Returns(call=>JsonSerializer.Serialize(call.ArgAt<DecisionAuthorityManifest>(1))==JsonSerializer.Serialize(manifest));
        return new(authority,TimeProvider.System,new Dictionary<string,QualificationDecisionCaseInput>{{caseId,new("tenant",manifest,artifact,caseId=="S3.decision-reject.contract"?basis with { ContractId="other-independently-expected-contract" }:basis,SignedOriginalBasis:basis)}});
    }

    private static QualificationEnvelopeCaseWorkflow Envelope(IndependentAuthorityReceiverTests.Fixture fixture,string caseId)
    {
        var profiles=Substitute.For<IPlatformSigningProfileProvider>();var keys=Substitute.For<IPlatformHmacKeyProvider>();DateTimeOffset now=DateTimeOffset.UtcNow;
        profiles.GetCurrent().Returns(new PlatformSigningProfile("fixture-r1","fixture-issuer","fixture-audience",now.AddHours(-1),now.AddHours(1),TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(600),TimeSpan.FromSeconds(86400),TimeSpan.FromSeconds(604800)));
        keys.ResolveAsync(Arg.Any<PlatformHmacScope>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(call=>ValueTask.FromResult(new PlatformHmacKeyResolution(CustodyStatus.Succeeded,new(call.ArgAt<PlatformHmacScope>(0),new("v1",PlatformHmacKeyState.Active,now.AddHours(-1),now.AddHours(1),null),Enumerable.Repeat((byte)42,32).ToArray()))));
        var authenticator=new TrustedEnvelopeAuthenticator(keys,profiles,TimeProvider.System);
        var replay=new TrustedEnvelopeReplayVerifier(authenticator,Substitute.For<ITrustedEnvelopeReplayRegistrar>(),profiles,TimeProvider.System);
        var identity=new TrustedEnvelopeIdentity(1,"fixture-r1","fixture-issuer",new(TrustedPrincipalKind.User,"tenant","actor","party",1,"roles",null,null,null,null),"Contracts.EditProposal","EditProposal","tenant","interaction","correlation","cause",["tenant","User","actor","EditProposal","client-key"],"fingerprint","digest-v1","fixture-audience","logical-command");
        return new(authenticator,replay,fixture.Client(),new Dictionary<string,TrustedEnvelopeIdentity>{{caseId,identity}});
    }

    private static QualificationProofBinding Binding(ECDsa key,string identity)
    { byte[] bytes=Encoding.ASCII.GetBytes(key.ExportSubjectPublicKeyInfoPem());return new(identity,"fixture-v1",bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))); }

    private static async Task VerifyPythonAsync(JsonObject request,JsonObject execution,JsonObject observation,ECDsa executor,ECDsa observer,bool expected=true)
    {
        string directory=Path.Combine(Path.GetTempPath(),"story54-cross-language-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            var config=new JsonObject { ["qualification"]=new JsonObject() };var binding=new JsonObject();
            foreach(var pair in new[]{("executor",executor),("observer",observer)})
            {
                string file=Path.Combine(directory,pair.Item1+".pem");byte[] pem=Encoding.ASCII.GetBytes(pair.Item2.ExportSubjectPublicKeyInfoPem());await File.WriteAllBytesAsync(file,pem);
                config["qualification"]![pair.Item1]=new JsonObject { ["identity"]=pair.Item1,["version"]="fixture-v1",["publicAnchorFile"]=file };
                binding[pair.Item1+"AnchorSha256"]=Convert.ToHexStringLower(SHA256.HashData(pem));
            }
            await File.WriteAllTextAsync(Path.Combine(directory,"fixture.json"),new JsonObject { ["request"]=request.DeepClone(),["execution"]=execution.DeepClone(),["observation"]=observation.DeepClone(),["config"]=config,["binding"]=binding }.ToJsonString());
            string repository=Repository();
            var start=new ProcessStartInfo("python3") { RedirectStandardError=true,RedirectStandardOutput=true,UseShellExecute=false };
            start.ArgumentList.Add(Path.Combine(repository,"eng/ext-secrets-1/verify_csharp_fixture.py"));start.ArgumentList.Add(Path.Combine(directory,"fixture.json"));
            using var process=Process.Start(start)!;string stdout=await process.StandardOutput.ReadToEndAsync();string stderr=await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);if(expected) { process.ExitCode.ShouldBe(0,stdout+stderr);stdout.ShouldContain("CSharpReceivingSourceFixture: passed"); }else process.ExitCode.ShouldNotBe(0);
        }
        finally { Directory.Delete(directory,true); }
    }
    private static string Repository([System.Runtime.CompilerServices.CallerFilePath]string source="")=>Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!,"../.."));

    // Actual fixture file reads and append-only audit. Restart/restore are isolated fixture operations, not physical evidence.
    private sealed class FileAuditFixture(IQualificationOriginalStore originals,JsonObject original):IQualificationNativeCaseAuditReader,IQualificationRecoveryController,IDisposable
    {
        private readonly string _directory=CreateDirectory();private readonly ECDsa _key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly Dictionary<string,QualificationRecoveryControlOriginal> _controls=[];
        private long _originalRevision;private string? _originalReceipt;private string? _stage;internal int Reads,Controls,Effects;internal bool FailNextRead;
        internal Func<QualificationNativeCaseAudit,QualificationNativeCaseAudit>? Transform;
        private static string CreateDirectory()
        { string path=Path.Combine(Path.GetTempPath(),"story54-native-audit-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);foreach(string surface in Surfaces) Directory.CreateDirectory(Path.Combine(path,surface));return path; }
        private static readonly string[] Surfaces=["Events","Projections","Responses","Logs","Traces","Evidence"];
        private JsonObject Append(string kind)
        {
            string journal=Path.Combine(_directory,"audit.jsonl");long position=File.Exists(journal)?File.ReadLines(journal).LongCount()+1:1;
            var row=new JsonObject { ["kind"]=kind,["position"]=position,["keyVersion"]=null,["guardRevision"]=0,["blockSetRevision"]=0,["occurredAt"]=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            row["requestId"]=original["requestId"]!.DeepClone();row["tenantId"]=original["enrolledScope"]!["tenantId"]!.DeepClone();row["originalReceiptId"]=_originalReceipt;
            row["receiptId"]=_originalReceipt??throw new InvalidOperationException("Fixture original must be durably read before audit publication.");File.AppendAllText(journal,row.ToJsonString()+"\n");return row;
        }
        internal async Task RecordCaseBoundaryAsync(bool lookup,CancellationToken token)
        {
            string suffix=original["caseId"]!.GetValue<string>()=="S1.reject.missing-config"?":missing-config":":case-result";
            var retained=await originals.LookupResultAsync("executor",original["requestId"]!.GetValue<string>()+suffix,token)??throw new InvalidOperationException("Fixture boundary requires the actual retained native case original");
            _originalReceipt=QualificationProtocolSchema.Digest(retained);
            if(!lookup) { Effects++;if(_originalRevision!=0) throw new InvalidOperationException("Case effect repeated");_originalRevision=Append("OriginalPersisted")["position"]!.GetValue<long>();_stage="AfterEffect"; }
            else { if(_originalRevision==0) throw new InvalidOperationException("Missing original"); } // Actual readonly lookups do not create backend read events.
        }
        public async Task<QualificationNativeCaseAudit?> ReadAsync(JsonObject request,CancellationToken token)
        {
            var raw=await originals.LookupResultAsync("executor",original["requestId"]!.GetValue<string>()+(original["caseId"]!.GetValue<string>()=="S1.reject.missing-config"?":missing-config":":case-result"),token);if(raw is null || request["requestId"]!.GetValue<string>()!=original["requestId"]!.GetValue<string>()) return null;
            _originalReceipt=QualificationProtocolSchema.Digest(raw);
            if(_originalRevision==0 || _stage is null) return null;
            Reads++;if(FailNextRead) { FailNextRead=false;throw new IOException("fixture native read interrupted after independent source admission"); }
            var receipts=new JsonArray(File.ReadLines(Path.Combine(_directory,"audit.jsonl")).Select(value=>JsonNode.Parse(value)).ToArray());
            var scans=new JsonArray();foreach(string surface in Surfaces)
            {
                var files=Directory.GetFiles(Path.Combine(_directory,surface));long matches=0;
                foreach(string file in files) matches+= (await File.ReadAllTextAsync(file,token)).Split("fixture-private-canary",StringSplitOptions.None).Length-1;
                scans.Add(new JsonObject { ["surface"]=surface,["matchCount"]=matches,["inspectedFiles"]=files.Length });
            }
            string stage=_stage;
            string incarnation=File.Exists(Path.Combine(_directory,"incarnation"))?File.ReadAllText(Path.Combine(_directory,"incarnation")):"fixture-original";
            var control=stage=="AfterRestart"?_controls.Values.Single(value=>value.Operation=="restart"):stage=="AfterRestore"?_controls.Values.Single(value=>value.Operation=="restore"):null;
            var nativeBoundary=new QualificationNativeStageBoundary(stage,incarnation,"audit:"+receipts.Count,
                stage=="AfterLookup"?original["requestId"]!.GetValue<string>()+":lookup-stage":control?.OperationId,
                control is null?null:QualificationProtocolSchema.Digest(JsonSerializer.SerializeToNode(control,CustodyBindingContract.Json)!));
            var result=new QualificationNativeCaseAudit(_originalRevision,receipts,new JsonArray(),scans,new JsonArray(),new JsonObject { ["nativeStageBoundary"]=JsonSerializer.SerializeToNode(nativeBoundary,CustodyBindingContract.Json) });return Transform is null?result:Transform(result);
        }
        public Task<IReadOnlyList<string>> PreflightStagesAsync(JsonObject original,CancellationToken token)=>Task.FromResult<IReadOnlyList<string>>(Surfaces.All(surface=>Directory.Exists(Path.Combine(_directory,surface)))?[]:["SelectedIndependentCompleteNativeSurfaceSnapshotProtocol"]);
        internal void WithholdSurface(string surface)=>Directory.Move(Path.Combine(_directory,surface),Path.Combine(_directory,"missing-"+surface));
        internal void RestoreSurface(string surface)=>Directory.Move(Path.Combine(_directory,"missing-"+surface),Path.Combine(_directory,surface));
        public Task<IReadOnlyList<string>> PreflightAsync(JsonObject request,CancellationToken token)=>Task.FromResult<IReadOnlyList<string>>([]);
        public Task<QualificationRecoveryControlOriginal?> ExecuteAsync(string operation,JsonObject request,CancellationToken token)
        {
            string id=request["requestId"]!.GetValue<string>()+":"+operation;
            if(_controls.ContainsKey(id)) throw new InvalidOperationException("Fixture control must not repeat.");
            string before=File.Exists(Path.Combine(_directory,"incarnation"))?File.ReadAllText(Path.Combine(_directory,"incarnation")):"fixture-original";
            string after="fixture-"+Guid.NewGuid().ToString("N");File.WriteAllText(Path.Combine(_directory,"incarnation"),after);
            var receipt=Append(operation=="restart"?"Restart":"Restore");Controls++;
            var control=new QualificationRecoveryControlOriginal(request["requestId"]!.GetValue<string>(),id,operation,request["targetCommit"]!.GetValue<string>(),request["isolatedTargetId"]!.GetValue<string>(),before,after,request["originalResultSha256"]!.GetValue<string>(),QualificationProtocolSchema.Digest(receipt),[]);
            control=control with { Signature=_key.SignData(QualificationProofVerifier.CanonicalBytes(JsonSerializer.SerializeToNode(control,CustodyBindingContract.Json)!),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation) };
            _controls.Add(id,control);_stage=operation=="restart"?"AfterRestart":"AfterRestore";return Task.FromResult<QualificationRecoveryControlOriginal?>(control);
        }
        public Task<QualificationRecoveryControlOriginal?> LookupAsync(string operation,JsonObject request,CancellationToken token)=>Task.FromResult(_controls.GetValueOrDefault(request["requestId"]!.GetValue<string>()+":"+operation));
        public bool Verify(QualificationRecoveryControlOriginal control,JsonObject request)=>control.RequestId==request["requestId"]!.GetValue<string>() && control.OriginalReceiptSha256==request["originalResultSha256"]!.GetValue<string>() && control.Signature is { Length:64 }
            && _key.VerifyData(QualificationProofVerifier.CanonicalBytes(JsonSerializer.SerializeToNode(control with { Signature=[] },CustodyBindingContract.Json)!),control.Signature,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        public void Dispose() { _key.Dispose();Directory.Delete(_directory,true); }
    }
}
