using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Protection;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

// Called with actual signed executor/observer receiving proofs from the real workflow/reader fixture.
internal static class ConcreteJointSecretsAdapterRegression
{
    internal static async Task VerifyAsync(JsonObject full,JsonObject execution,JsonObject observation,ECDsa executorKey,ECDsa observerKey)
    {
        full=JsonNode.Parse(full.ToJsonString())!.AsObject();
        QualificationProtocolSchema.ValidRequest("executor","execute",full).ShouldBeTrue("fixture JSON numeric normalization");
        var token=TestContext.Current.CancellationToken;string caseId="secrets:"+full["caseId"]!.GetValue<string>();
        using var enrollmentKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);using var ownerKey=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var accepted=new JointPhaseCommandScope("fixture-accepted-binding",full["targetCommit"]!.GetValue<string>(),1,[caseId]);
        var recipe=(JsonObject)full.DeepClone();recipe.Remove("runId");recipe.Remove("requestId");
        var phase=new JsonObject { ["requestId"]=new string('7',64),["bindingDigest"]=accepted.BindingDigest,["phase"]=1,["attemptId"]=full["runId"]!.DeepClone(),["cases"]=new JsonArray(caseId) };
        var enrollRequest=new JsonObject();foreach(string name in new[]{"protocol","runId","targetCommit","isolatedTargetId","planSha256"}) enrollRequest[name]=full[name]!.DeepClone();enrollRequest["operation"]="enroll";enrollRequest["requestId"]=QualificationProtocolSchema.Digest(enrollRequest);
        var enrolled=QualificationHostTests.RolePayload("enrollment");foreach(string name in new[]{"protocol","runId","targetCommit","isolatedTargetId","planSha256","requestId"}) enrolled[name]=enrollRequest[name]!.DeepClone();
        enrolled["executorIdentity"]="executor";enrolled["executorVersion"]="fixture-v1";enrolled["observerIdentity"]="observer";enrolled["observerVersion"]="fixture-v1";
        enrolled["caseScopes"]![full["caseId"]!.GetValue<string>()]=full["enrolledScope"]!.DeepClone();enrolled["caseTargetIds"]![full["caseId"]!.GetValue<string>()]=full["caseTargetId"]!.DeepClone();
        enrolled["observedAt"]=DateTimeOffset.UtcNow.ToUnixTimeSeconds();enrolled["validUntil"]=DateTimeOffset.UtcNow.AddSeconds(25).ToUnixTimeSeconds();
        var enrollment=Sign(enrolled,enrollmentKey);
        var verifier=new QualificationProofVerifier(new Dictionary<string,QualificationProofBinding> { ["executor"]=Binding(executorKey,"executor","fixture-v1"),["observer"]=Binding(observerKey,"observer","fixture-v1"),["enrollment"]=Binding(enrollmentKey,"enrollment","v1") });
        verifier.VerifyOriginal("executor",full,execution,execution["payload"]!["observedAt"]!.GetValue<long>(),token).ShouldBeTrue();
        verifier.VerifyOriginal("observer",full,observation,observation["payload"]!["observedAt"]!.GetValue<long>(),token).ShouldBeTrue();
        verifier.VerifyOriginal("enrollment",enrollRequest,enrollment,enrollment["payload"]!["observedAt"]!.GetValue<long>(),token).ShouldBeTrue();
        foreach(string fault in new[]{"none","signature","another-exact-request"})
        {
            using var executor=new Provider("executor",full,execution);using var observer=new Provider("observer",full,observation);using var enroller=new Provider("enrollment",full,enrollment);
            if(fault=="signature") observer.Proof["signature"]=Convert.ToBase64String(new byte[64]);
            if(fault=="another-exact-request")
            {
                var other=(JsonObject)full.DeepClone();other["runId"]="another-fixture-run";other.Remove("requestId");other["requestId"]=QualificationProtocolSchema.Digest(other);
                var body=observer.Proof["payload"]!.AsObject();body["runId"]=other["runId"]!.DeepClone();body["requestId"]=other["requestId"]!.DeepClone();
                foreach(var snapshot in body["snapshots"]!.AsArray())
                {
                    snapshot!["originalRequestId"]=other["requestId"]!.DeepClone();snapshot["state"]!["requestId"]=other["requestId"]!.DeepClone();snapshot["stateSha256"]=QualificationProtocolSchema.Digest(snapshot["state"]!);
                }
                for(int i=0;i<body["trace"]!.AsArray().Count;i++) { body["trace"]![i]!["requestId"]=other["requestId"]!.DeepClone();body["trace"]![i]!["stateSha256"]=body["snapshots"]![i]!["stateSha256"]!.DeepClone(); }
                observer.Proof=Sign(body,observerKey);
                verifier.VerifyOriginal("observer",other,observer.Proof,body["observedAt"]!.GetValue<long>(),token).ShouldBeTrue();
            }
            using var secretAuthority=StoreAuthority("joint-phase-secret");using var commandAuthority=StoreAuthority("joint-phase-command");
            var adapter=new JointSecretsCaseExecutor(accepted,executor,observer,enroller,verifier,new InstalledQualificationOriginalStore(secretAuthority.Client()),new("worker","scope"),new Dictionary<string,JsonObject>{{caseId,recipe}});
            JsonNode.DeepEquals(JointPhaseRequestRecipe.Bind(recipe,phase,accepted),full).ShouldBeTrue("exact fixture recipe");
            (await executor.AuthorizeAsync(full,new("worker","scope"),token)).ShouldBeTrue("executor current scope");
            (await observer.AuthorizeAsync(full,new("worker","scope"),token)).ShouldBeTrue("observer current scope");
            (await enroller.AuthorizeAsync(enrollRequest,new("worker","scope"),token)).ShouldBeTrue("enrollment current scope");
            (await adapter.PreflightAsync(caseId,phase,token)).ShouldBeEmpty("concrete adapter preflight");
            string directory=Path.Combine(Path.GetTempPath(),"concrete-secrets-command-"+Guid.NewGuid().ToString("N"));
            try
            {
                var command=new JointPhaseCommand(accepted,new InstalledQualificationOriginalStore(commandAuthority.Client()),new Dictionary<string,IReadOnlyList<(QualificationSourceWorkflow,JsonObject)>>(),null,directory,ownerKey,adapter,phaseAdmission:(_,_)=>Task.FromResult(true));
                var result=await command.ExecuteAsync(phase,token);
                if(fault=="none")
                {
                    result.ShouldNotBeNull();JsonNode.DeepEquals(result,await command.ExecuteAsync(phase,token)).ShouldBeTrue();
                    executor.Executions.ShouldBe(1);File.Exists(Path.Combine(directory,new string('7',64),"owner-original.json")).ShouldBeTrue();
                }
                else
                {
                    result.ShouldBeNull();(await adapter.ExecuteAsync(caseId,phase,token)).ShouldBeNull();
                    Directory.Exists(directory).ShouldBeFalse();executor.Executions.ShouldBe(1);
                    (await new InstalledQualificationOriginalStore(commandAuthority.Client()).LookupResultAsync("joint-phase-command",new string('7',64),token)).ShouldBeNull();
                }
                observer.Scope.RevokeCaller();(await adapter.ExecuteAsync(caseId,phase,token)).ShouldBeNull();executor.Executions.ShouldBe(1);
            }
            finally { if(Directory.Exists(directory)) Directory.Delete(directory,true); }
        }
    }
    private static IndependentAuthorityReceiverTests.Fixture StoreAuthority(string role)
    {
        var fixture=new IndependentAuthorityReceiverTests.Fixture();fixture.State=fixture.State with { Callers=fixture.State.Callers.Select(value=>value with { Role=role,Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original"] }).ToArray() };return fixture;
    }
    private static JsonObject Sign(JsonObject payload,ECDsa key)=>new() { ["payload"]=payload.DeepClone(),["signature"]=Convert.ToBase64String(key.SignData(QualificationProofVerifier.CanonicalBytes(payload),HashAlgorithmName.SHA256,DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) };
    private static QualificationProofBinding Binding(ECDsa key,string role,string version)
    { byte[] bytes=Encoding.ASCII.GetBytes(key.ExportSubjectPublicKeyInfoPem());return new(role,version,bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))); }
    private sealed class Provider:IQualificationExecutorProvider,IQualificationObserverProvider,IQualificationEnrollmentProvider,IDisposable
    {
        internal readonly IndependentAuthorityReceiverTests.Fixture Scope;internal JsonObject Proof;internal int Executions;public string Role { get; }
        internal Provider(string role,JsonObject request,JsonObject proof)
        {
            Role=role;Proof=(JsonObject)proof.DeepClone();Scope=StoreAuthority(role);Scope.State=Scope.State with {
                QualificationBindings=new(new[]{"runId","targetCommit","isolatedTargetId","planSha256"}.Select(name=>new KeyValuePair<string,string>(name,request[name]!.ToJsonString()))),
                QualificationCases=new(){{request["caseId"]!.GetValue<string>(),JsonSerializer.SerializeToElement(new { caseId=request["caseId"],caseTargetId=request["caseTargetId"],enrolledScope=request["enrolledScope"] },CustodyBindingContract.Json)}},
                Callers=Scope.State.Callers.Select(value=>value with { Methods=value.Methods.Append("qualification-authorize").ToArray() }).ToArray() };
        }
        public async Task<bool> AuthorizeAsync(JsonObject request,QualificationCallerContext caller,CancellationToken token)
        {
            var permission=await Scope.Client().InvokeAsync<JsonElement>("qualification-authorize",new { role=Role,request,caller },token);
            return permission.ValueKind==JsonValueKind.Object && permission.GetProperty("Allowed").GetBoolean();
        }
        public async Task<JsonObject?> InvokeAsync(string operation,JsonObject original,CancellationToken token)
        {
            var store=new InstalledQualificationOriginalStore(Scope.Client());string id=original["requestId"]!.GetValue<string>();
            bool first=await store.BeginAsync(Role,id,original,token);
            if(first) { if(operation=="execute") Executions++;await store.RetainAsync(Role,id,Proof,token); }
            return await store.LookupResultAsync(Role,id,token);
        }
        public void Dispose()=>Scope.Dispose();
    }
}
