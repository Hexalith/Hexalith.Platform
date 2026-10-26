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
    [Theory]
    [InlineData("S1.reject.missing-config")]
    [InlineData("S2.substitution.issuer")]
    [InlineData("S3.decision-reject.contract")]
    [InlineData("S3.exact-independent-decision")]
    [InlineData("S1.inventory.TenantKek")]
    [InlineData("S3.aes256gcm")]
    [InlineData("S3.es256-offline-anchor")]
    [InlineData("S3.crypto-substitution.tag")]
    [InlineData("S4.capability-canonical-14-fields")]
    [InlineData("S4.capability-substitution.issuer")]
    [InlineData("S4.capability-substitution.audience")]
    [InlineData("S4.capability-substitution.tenant")]
    [InlineData("S4.capability-substitution.request")]
    [InlineData("S4.capability-substitution.seal")]
    [InlineData("S4.capability-substitution.kind")]
    [InlineData("S4.capability-substitution.ordinal")]
    [InlineData("S4.capability-substitution.batch")]
    [InlineData("S4.capability-substitution.manifest")]
    [InlineData("S4.capability-substitution.guard")]
    [InlineData("S4.capability-substitution.intended-revision")]
    [InlineData("S4.capability-substitution.attestation")]
    [InlineData("S4.capability-substitution.signing-attempt")]
    [InlineData("S4.capability-substitution.key-version")]
    [InlineData("S4.capability-substitution.header")]
    public async Task ConcreteReceivingDriverAndSignedNativeProjectionPassExistingPythonVerifierAndRejectSubstitution(string caseId)
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
        request.Remove("requestId");request["requestId"]=QualificationProtocolSchema.Digest(request);var original=(JsonObject)request.DeepClone();
        authority.State=authority.State with {
            Callers=authority.State.Callers.Select(value=>value with { Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray(),
            QualificationBindings=new(new[]{"runId","targetCommit","isolatedTargetId","planSha256"}.Select(name=>new KeyValuePair<string,string>(name,original[name]!.ToJsonString()))),
            QualificationCases=new() { [caseId]=JsonSerializer.SerializeToElement(new { caseTargetId=original["caseTargetId"]!.GetValue<string>(),enrolledScope=original["enrolledScope"] },CustodyBindingContract.Json) } };
        var originals=new InstalledQualificationOriginalStore(authority.Client());
        using var audit=new FileAuditFixture(originals,original);
        var envelope=Envelope(authority,caseId);var decision=Decision(caseId);var inventory=Inventory(originals,audit,caseId,original);
        using var exportFixture=new ExportQualificationFixture();var exportWorkflow=new QualificationExportCaseWorkflow(exportFixture.Engine(),originals,new Dictionary<string,ExportQualificationInput>{{caseId,exportFixture.Input(original["caseTargetId"]!.GetValue<string>())}});
        using var capabilityKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);var capability=Capability(caseId,original,originals,capabilityKey);
        IQualificationObservedOriginalReader rawReader=caseId=="S4.capability-canonical-14-fields"?new QualificationCapabilityOriginalReader(originals,audit,capability):caseId.StartsWith("S4.capability-substitution.",StringComparison.Ordinal)?new QualificationDeniedOriginalReader(originals,audit,capability.ReadVerifyAsync):caseId=="S1.reject.missing-config"?new QualificationMissingConfigurationOriginalReader(originals,audit):IsExport(caseId)?new QualificationExportOriginalReader(originals,audit,exportWorkflow):caseId.StartsWith("S3.",StringComparison.Ordinal)?new QualificationDecisionOriginalReader(originals,audit,decision):caseId.StartsWith("S1.inventory.",StringComparison.Ordinal)?inventory.Reader:new QualificationDeniedOriginalReader(originals,audit,envelope.ReadVerifyAsync);
        var observerRole=new QualificationOwnerRole("observer","observer","fixture-v1",observerKey.ExportSubjectPublicKeyInfoPem());
        var sourceReceiver=new QualificationOwnerOperationHandler(observerRole,observerKey,originals,new Dictionary<string,IQualificationOwnerCaseDriver>(),TimeProvider.System,rawReader);
        var sourceReader=new ReceivingReader(sourceReceiver,()=>authority.State,authority.State.Callers[0] with { Role="observer-reader" });
        byte[] anchor=Encoding.ASCII.GetBytes(observerKey.ExportSubjectPublicKeyInfoPem());
        var trust=new QualificationProofBinding("observer","fixture-v1",anchor,Convert.ToHexStringLower(SHA256.HashData(anchor)));
        var workflows=new[]{caseId.StartsWith("S4.capability",StringComparison.Ordinal)?QualificationRuntimeWorkflowCatalog.Retained(caseId,originals,capability.ExecuteAsync,capability.LookupAsync):caseId=="S1.reject.missing-config"?new QualificationMissingConfigurationWorkflow(originals).Registration:IsExport(caseId)?exportWorkflow.Registrations().Single():caseId.StartsWith("S3.",StringComparison.Ordinal)?decision.Registrations(originals).Single():caseId.StartsWith("S1.inventory.",StringComparison.Ordinal)?inventory.Workflow:QualificationRuntimeWorkflowCatalog.Retained(caseId,originals,envelope.ExecuteAsync)};
        var drivers=QualificationDriverComposition.Create(workflows,originals,audit,sourceReader,trust,new Dictionary<string,IReadOnlyList<string>>{{caseId,[]}});
        var executor=new QualificationOwnerOperationHandler(new("executor","executor","fixture-v1",executorKey.ExportSubjectPublicKeyInfoPem()),executorKey,originals,drivers,TimeProvider.System);
        var observer=new QualificationOwnerOperationHandler(observerRole,observerKey,originals,drivers,TimeProvider.System);
        JsonObject execution=(JsonObject)(await executor.HandleAsync("qualification-executor-execute",JsonSerializer.SerializeToElement(original),authority.State.Callers[0],authority.State,TestContext.Current.CancellationToken))!;
        authority.State=authority.State with { Callers=authority.State.Callers.Select(value=>value with { Role="observer",Methods=value.Methods.Concat(["qualification-read-original:executor"]).ToArray() }).ToArray() };
        JsonObject observation=(JsonObject)(await observer.HandleAsync("qualification-observer-observe",JsonSerializer.SerializeToElement(original),authority.State.Callers[0] with { Role="observer" },authority.State,TestContext.Current.CancellationToken))!;
        audit.Controls.ShouldBe(2);audit.Reads.ShouldBe(4);
        execution["payload"]!["outcome"]!.GetValue<string>().ShouldBe(caseId is "S3.exact-independent-decision" or "S3.aes256gcm" or "S3.es256-offline-anchor" or "S4.capability-canonical-14-fields"?"Verified":caseId.StartsWith("S1.inventory.",StringComparison.Ordinal)?"Resolved":"Denied");
        observation["payload"]!["metrics"]!["persistedProofCount"]!.GetValue<long>().ShouldBe(4);
        var anchors=new Dictionary<string,QualificationProofBinding> { ["executor"]=Binding(executorKey,"executor"),["observer"]=trust };
        var verifier=new QualificationProofVerifier(anchors);
        verifier.Verify("executor",request,execution,TestContext.Current.CancellationToken).ShouldBeTrue();
        verifier.Verify("observer",request,observation,TestContext.Current.CancellationToken).ShouldBeTrue();
        var changed=(JsonObject)original.DeepClone();changed["targetCommit"]=new string('f',40);
        await Should.ThrowAsync<InvalidOperationException>(()=>executor.HandleAsync("qualification-executor-execute",JsonSerializer.SerializeToElement(changed),authority.State.Callers[0] with { Role="executor" },authority.State,TestContext.Current.CancellationToken));
        changed=(JsonObject)original.DeepClone();changed["requestId"]=new string('f',64);
        (await drivers[caseId].ObserveAsync(changed,TestContext.Current.CancellationToken)).ShouldBeNull();
        var retained=(await originals.LookupResultAsync("executor",original["requestId"]!.GetValue<string>()+":observations",TestContext.Current.CancellationToken))!;
        var tampered=(JsonObject)retained.DeepClone();tampered["snapshots"]![0]!["sourceProof"]!["OriginalReceiptSha256"]=new string('f',64);
        new QualificationMeasuredCaseProjector(trust,audit,new Dictionary<string,IReadOnlyList<string>>{{caseId,[]}}).Project("observer",original,tampered).ShouldBeNull();
        await VerifyPythonAsync(request,execution,observation,executorKey,observerKey);
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
        authority.VerifyExpectedBasisAsync(Arg.Any<DecisionAuthorityExpectedBasis>(),Arg.Any<DecisionAuthorityPublishedProfile>(),Arg.Any<CancellationToken>()).Returns(call=>JsonSerializer.Serialize(call.ArgAt<DecisionAuthorityExpectedBasis>(0))==JsonSerializer.Serialize(basis));
        authority.VerifyApproverAsync(Arg.Any<DecisionAuthorityExpectedBasis>(),Arg.Any<DecisionAuthorityApprover>(),Arg.Any<DateTimeOffset>(),Arg.Any<CancellationToken>()).Returns(call=>call.ArgAt<DecisionAuthorityApprover>(1)==manifest.Approvers[0]);
        authority.VerifyAuthorityBoundaryAsync(Arg.Any<DecisionAuthorityExpectedBasis>(),Arg.Any<DecisionAuthorityManifest>(),Arg.Any<DecisionAuthorityPublishedProfile>(),Arg.Any<CancellationToken>()).Returns(call=>JsonSerializer.Serialize(call.ArgAt<DecisionAuthorityManifest>(1))==JsonSerializer.Serialize(manifest));
        return new(authority,TimeProvider.System,new Dictionary<string,QualificationDecisionCaseInput>{{caseId,new("tenant",manifest,artifact,basis)}});
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

    private static async Task VerifyPythonAsync(JsonObject request,JsonObject execution,JsonObject observation,ECDsa executor,ECDsa observer)
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
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);process.ExitCode.ShouldBe(0,stdout+stderr);stdout.ShouldContain("CSharpReceivingSourceFixture: passed");
        }
        finally { Directory.Delete(directory,true); }
    }
    private static string Repository([System.Runtime.CompilerServices.CallerFilePath]string source="")=>Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!,"../.."));

    private sealed class ReceivingReader(QualificationOwnerOperationHandler receiver,Func<IndependentAuthorityState> current,IndependentCallerGrant caller):IQualificationObservedOriginalReader
    {
        public async Task<QualificationObservedOriginal?> ReadAsync(JsonObject original,CancellationToken token)=>(QualificationObservedOriginal?)await receiver.HandleAsync("qualification-observer-read-source",JsonSerializer.SerializeToElement(original),caller,current(),token);
    }

    // Actual fixture file reads and append-only audit. Restart/restore are isolated fixture operations, not physical evidence.
    private sealed class FileAuditFixture(IQualificationOriginalStore originals,JsonObject original):IQualificationNativeCaseAuditReader,IQualificationRecoveryController,IDisposable
    {
        private readonly string _directory=CreateDirectory();private readonly ECDsa _key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly Dictionary<string,QualificationRecoveryControlOriginal> _controls=[];
        private long _originalRevision;private string? _originalReceipt;internal int Reads,Controls;
        private static string CreateDirectory()
        { string path=Path.Combine(Path.GetTempPath(),"story54-native-audit-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);foreach(string surface in Surfaces) Directory.CreateDirectory(Path.Combine(path,surface));return path; }
        private static readonly string[] Surfaces=["Events","Projections","Responses","Logs","Traces","Evidence"];
        private JsonObject Append(string kind)
        {
            string journal=Path.Combine(_directory,"audit.jsonl");long position=File.Exists(journal)?File.ReadLines(journal).LongCount()+1:1;
            var row=new JsonObject { ["kind"]=kind,["position"]=position,["keyVersion"]=null,["guardRevision"]=0,["blockSetRevision"]=0,["occurredAt"]=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            row["receiptId"]=_originalReceipt??throw new InvalidOperationException("Fixture original must be durably read before audit publication.");File.AppendAllText(journal,row.ToJsonString()+"\n");return row;
        }
        public async Task<QualificationNativeCaseAudit?> ReadAsync(JsonObject request,CancellationToken token)
        {
            var raw=await originals.LookupResultAsync("executor",original["requestId"]!.GetValue<string>()+(original["caseId"]!.GetValue<string>()=="S1.reject.missing-config"?":missing-config":":case-result"),token);if(raw is null || request["requestId"]!.GetValue<string>()!=original["requestId"]!.GetValue<string>()) return null;
            _originalReceipt=QualificationProtocolSchema.Digest(raw);
            if(_originalRevision==0) _originalRevision=Append("OriginalPersisted")["position"]!.GetValue<long>();
            Append("Lookup");Reads++;
            var receipts=new JsonArray(File.ReadLines(Path.Combine(_directory,"audit.jsonl")).Select(value=>JsonNode.Parse(value)).ToArray());
            var scans=new JsonArray();foreach(string surface in Surfaces)
            {
                var files=Directory.GetFiles(Path.Combine(_directory,surface));long matches=0;
                foreach(string file in files) matches+= (await File.ReadAllTextAsync(file,token)).Split("fixture-private-canary",StringSplitOptions.None).Length-1;
                scans.Add(new JsonObject { ["surface"]=surface,["matchCount"]=matches,["inspectedFiles"]=files.Length });
            }
            return new(_originalRevision,receipts,new JsonArray(),scans,new JsonArray());
        }
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
            _controls.Add(id,control);return Task.FromResult<QualificationRecoveryControlOriginal?>(control);
        }
        public Task<QualificationRecoveryControlOriginal?> LookupAsync(string operation,JsonObject request,CancellationToken token)=>Task.FromResult(_controls.GetValueOrDefault(request["requestId"]!.GetValue<string>()+":"+operation));
        public bool Verify(QualificationRecoveryControlOriginal control,JsonObject request)=>control.RequestId==request["requestId"]!.GetValue<string>() && control.OriginalReceiptSha256==request["originalResultSha256"]!.GetValue<string>() && control.Signature is { Length:64 }
            && _key.VerifyData(QualificationProofVerifier.CanonicalBytes(JsonSerializer.SerializeToNode(control with { Signature=[] },CustodyBindingContract.Json)!),control.Signature,HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        public void Dispose() { _key.Dispose();Directory.Delete(_directory,true); }
    }
}
