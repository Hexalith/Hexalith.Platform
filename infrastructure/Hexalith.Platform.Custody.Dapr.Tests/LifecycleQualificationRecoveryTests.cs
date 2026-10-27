using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Grpc.Net.Client;
using Hexalith.Platform.Custody;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Secrets.Runtime;

internal static class LifecycleQualificationRecoveryTests
{
    internal static async Task Run()
    {
        using var f=new Fixture();var transport=new SdkBridge(f);using var sdk=new DaprClientBuilder().UseGrpcEndpoint("http://127.0.0.1:1").UseGrpcChannelOptions(new GrpcChannelOptions { HttpHandler=transport }).Build();
        var source=new LifecycleAuthorizer(f);var client=PublicLifecycleTests.Client(f,sdk,source);
        var registration=(await f.Call(f.Wrap("qualification-original"))).WrappedRegistration!;
        var scope=new JsonObject { ["tenantId"]="tenant" };string hash=QualificationProtocolSchema.Digest(scope);
        var originals=new Originals();var wrap=new JsonObject { ["caseId"]="S3.lifecycle.wrap",["requestId"]="wrap-case",["enrolledScope"]=scope.DeepClone() };
        var use=new CustodyKeyUse("lease","attempt","process","transport",registration.OpaqueWrappedKeyReference);
        var wrapper=new QualificationLifecycleCaseWorkflow(client,originals,new Dictionary<string,QualificationLifecycleCaseInput>{{"S3.lifecycle.wrap",new(registration,hash,use)}});
        await originals.BeginAsync("executor","wrap-case:case-result",wrap,default);
        var recovered=await wrapper.LookupAsync(wrap,default)??throw new Exception("Complete original registration did not recover.");
        Check(JsonNode.DeepEquals(recovered["actual"],JsonSerializer.SerializeToNode(registration,CustodyBindingContract.Json)),"Recovered wrap representation changed from complete registration.");
        foreach(var changed in new[]{registration with { WrapReceiptId="changed" },registration with { OpaqueWrappedKeyReference="changed" },registration with { KekVersion="changed" },registration with { ExpectedRevision=registration.ExpectedRevision+1 }})
            Check(await client.LookupRegistrationOriginalAsync(changed)==null,"Substituted wrap registration recovered.");
        var pin=new CustodyKeyLifecycleRequest(registration.Identity,"pin-original",CustodyKeyLifecycleAction.Pin,f.Store.State.Head.Revision,"hold","fence",2,"decision");
        var request=new JsonObject { ["caseId"]="S3.lifecycle.pin",["requestId"]="pin-case",["enrolledScope"]=scope.DeepClone() };
        wrapper=new(client,originals,new Dictionary<string,QualificationLifecycleCaseInput>{{"S3.lifecycle.pin",new(registration,hash,Lifecycle:pin)}});
        source.Mode="refuse";Check(await wrapper.ExecuteAsync(request,default)==null,"Unsettled lifecycle became terminal.");Check(await originals.LookupResultAsync("executor","pin-case:case-result",default)==null,"Unknown lifecycle was sealed.");
        source.Mode="normal";Check(await wrapper.LookupAsync(request,default)==null,"Missing native original became terminal.");
        await f.Call(f.Request("pin",registration,lifecycle:pin));var before=f.Store.State.Head;
        var terminal=await wrapper.LookupAsync(request,default)??throw new Exception("Native terminal original could not settle pending case.");
        Check(terminal["actual"]!["Status"]!.GetValue<string>()=="Pinned" && f.Store.State.Head==before,"Settlement repeated lifecycle effect.");
        // Existing immutable uncertainty remains readable history; terminal settlement gets a distinct v2 original.
        var legacy=(JsonObject)request.DeepClone();legacy["requestId"]="legacy-case";await originals.BeginAsync("executor","legacy-case:case-result",legacy,default);
        var uncertain=new JsonObject { ["caseId"]="S3.lifecycle.pin",["requestId"]="legacy-case",["actual"]=JsonSerializer.SerializeToNode(new CustodyKeyLifecycleOutcome(registration.Identity,pin.OperationId,"digest",CustodyKeyLifecycleStatus.Unknown),CustodyBindingContract.Json) };
        await originals.RetainAsync("executor","legacy-case:case-result",uncertain,default);
        Check(await wrapper.LookupAsync(legacy,default)!=null,"Legacy uncertainty stranded exact terminal native recovery.");
        Check(JsonNode.DeepEquals(await originals.LookupResultAsync("executor","legacy-case:case-result",default),uncertain),"Legacy uncertain observation was overwritten.");
        Check(await originals.LookupResultAsync("executor","legacy-case:case-terminal-v2",default)!=null,"Versioned settlement original missing.");
        var manifest=f.Manifest([registration]).Manifest!;
        var holdScope=new JsonObject { ["tenantId"]="tenant",["logicalId"]="logical",["targetIds"]=new JsonArray("target"),["providerCopyIds"]=new JsonArray() };
        var holdRequest=new JsonObject { ["caseId"]="S3.hold-pin",["requestId"]="held-case",["enrolledScope"]=holdScope };
        var heldInput=new QualificationLifecycleCaseInput(registration,QualificationProtocolSchema.Digest(holdScope),Manifest:manifest.Original,ReservationReceiptId:manifest.ProtectionReservationReceiptId);
        var heldInputs=new Dictionary<string,QualificationLifecycleCaseInput>{{"S3.hold-pin",heldInput}};
        var heldWorkflow=new QualificationLifecycleCaseWorkflow(client,originals,heldInputs);
        source.Mode="refuse";Check(await heldWorkflow.ExecuteAsync(holdRequest,default)==null,"Unavailable held-manifest observation became terminal.");
        source.Mode="normal";Check(await heldWorkflow.LookupAsync(holdRequest,default)==null,"Never-started manifest was inferred to be a denial.");
        // First admission failed before native send; independently initiate the exact controlled native denial.
        var denied=await client.ExecuteManifestOriginalAsync(manifest.Original,manifest.ProtectionReservationReceiptId,false);
        Check(denied.Denial is not null && denied.Result.State==DeletionManifestProviderState.NotStarted,"Authenticated definitive held denial missing.");
        var heldTerminal=await heldWorkflow.LookupAsync(holdRequest,default)??throw new Exception("Definitive held denial remained pending.");
        var facts=new JsonObject { ["lifecycleOriginal"]=heldTerminal.DeepClone(),["lifecycleReservationOriginal"]=new JsonObject {
            ["operationId"]=CustodyLifecycleClient.ManifestOperationId(manifest.Original,manifest.ProtectionReservationReceiptId),["requestId"]="held-case",["receiptId"]=manifest.ProtectionReservationReceiptId },
            ["retainedPins"]=JsonSerializer.SerializeToNode(denied.Denial!.Pins,CustodyBindingContract.Json),["destroyedTargetIds"]=new JsonArray(),["pinnedTargetIds"]=new JsonArray("target") };
        var audit=new HeldAudit(new(1,[],[],[],[],facts));var reader=new QualificationLifecycleOriginalReader(originals,audit,heldWorkflow,heldInputs);
        Check((await reader.ReadAsync(holdRequest,default))?.Source["state"]?["outcome"]?.GetValue<string>()=="Denied","Real lifecycle original reader discarded definitive held denial.");
        await client.ExecuteLifecycleAsync(registration,pin with { OperationId="release-after-denial",Action=CustodyKeyLifecycleAction.Unpin,ExpectedRevision=f.Store.State.Head.Revision });
        Check(f.Store.State.Checkpoint.Pins.Count==0,"Controlled later pin state did not change.");
        Check(JsonNode.DeepEquals(await heldWorkflow.LookupAsync(holdRequest,default),heldTerminal) && await reader.ReadAsync(holdRequest,default) is not null,"Later pin state rewrote the held-manifest original.");
        facts["lifecycleOriginal"]!["actual"]!["Denial"]!["RequestAttemptId"]="substituted";
        Check(await reader.ReadAsync(holdRequest,default)==null,"Changed exact denial original passed independent reader.");
        Check(f.Native.Encrypts==1 && f.Native.Decrypts==0,"Qualification original lookup repeated root access or wrap.");
    }
    private static void Check(bool condition,string reason) { if(!condition) throw new Exception(reason); }
    private sealed class HeldAudit(QualificationNativeCaseAudit value):IQualificationNativeCaseAuditReader
    { public Task<QualificationNativeCaseAudit?> ReadAsync(JsonObject request,CancellationToken token)=>Task.FromResult<QualificationNativeCaseAudit?>(value); }
    private sealed class Originals:IQualificationOriginalStore
    {
        private readonly Dictionary<string,(JsonObject Request,JsonObject? Result)> _values=[];
        public Task<bool> BeginAsync(string role,string id,JsonObject request,CancellationToken token)
        { if(_values.TryGetValue(id,out var existing)) { Check(JsonNode.DeepEquals(existing.Request,request),"Changed exact request.");return Task.FromResult(false); }_values.Add(id,((JsonObject)request.DeepClone(),null));return Task.FromResult(true); }
        public Task RetainAsync(string role,string id,JsonObject result,CancellationToken token)
        { var existing=_values[id];Check(existing.Result is null || JsonNode.DeepEquals(existing.Result,result),"Changed immutable terminal.");_values[id]=(existing.Request,(JsonObject)result.DeepClone());return Task.CompletedTask; }
        public Task<JsonObject?> LookupResultAsync(string role,string id,CancellationToken token)=>Task.FromResult(_values.GetValueOrDefault(id).Result);
    }
}
