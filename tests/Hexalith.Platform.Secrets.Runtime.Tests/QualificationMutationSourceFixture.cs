using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Protection;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

// Actual proof/dispatch receiver, signed immutable-original read and production native audit reader.
// Raw native stream pages are a disposable source fixture, never an installed qualification.
internal sealed class QualificationMutationSourceFixture:IDisposable
{
    private readonly IndependentAuthorityReceiverTests.Fixture _authority=new();private readonly RSA _workload=RSA.Create(2048);private readonly string _key=Path.GetTempFileName();
    internal QualificationNativeMutationEvidence Evidence=null!;internal QualificationMutationTrust Trust=null!;internal JsonArray NativeSources=null!;
    internal static async Task<QualificationMutationSourceFixture> CreateAsync(JsonObject request,CancellationToken token)
    {
        var fixture=new QualificationMutationSourceFixture();try { await fixture.Initialize(request,token);return fixture; }catch { fixture.Dispose();throw; }
    }
    private async Task Initialize(JsonObject request,CancellationToken token)
    {
        string tenant=request["enrolledScope"]!["tenantId"]!.GetValue<string>(),target=request["enrolledScope"]!["targetIds"]![0]!.GetValue<string>();
        _authority.State=_authority.State with { Callers=_authority.State.Callers.Select(value=>value with { Role="private-security-observation",Methods=["qualification-begin-original","qualification-retain-result","qualification-lookup-original","qualification-read-original:executor","recording-dispatch-admit","recording-command-authorize","recording-source:security-event-log"],CommandProofPublicKeySpkiBase64=Convert.ToBase64String(_workload.ExportSubjectPublicKeyInfo()) }).ToArray() };
        DateTimeOffset now=DateTimeOffset.UtcNow;string day=now.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);string message="01ARZ3NDEKTSV4RRFFQ69G5FAV";
        var payload=JsonSerializer.SerializeToElement(new { securityObservationId="source-only-observation",routingTenantId=tenant,utcDay=day,observedAt=now,reasonCode="denied",untrustedFieldsHmac=new string('a',64),digestKeyVersion="v1" });
        string aggregate=Convert.ToHexStringLower(SHA256.HashData(PlatformCanonicalBytes.Identity(["security-event-log",tenant,day])));
        var command=new SubmitCommandRequest(message,tenant,"security-event-log",aggregate,"Hexalith.Agents.Contracts.SecurityEventLog.RecordSecurityEvent",payload,IdempotencyKey:message);var bound=new BoundWorkloadCommandRequest(command,"scope");
        await _authority.Client().InvokeAsync<JsonElement>("qualification-begin-original",new { role="private-security-observation",requestId=message,original=payload },token);
        await File.WriteAllTextAsync(_key,Convert.ToBase64String(_workload.ExportPkcs8PrivateKey()),token);
        string? proof=await new RestrictedPrivateCommandProofSource("worker","scope",_key,_authority.Client(),TimeProvider.System).IssueAsync(bound,token);proof.ShouldNotBeNull();
        var dispatched=await _authority.Client().InvokeAsync<JsonElement>("recording-dispatch-admit",new { role="private-security-observation",requestId=message,original=payload,boundRequest=bound,workloadProof=proof },token);dispatched.GetProperty("Allowed").GetBoolean().ShouldBeTrue();
        string requestId=request["requestId"]!.GetValue<string>();_authority.State=_authority.State with { Originals=new(_authority.State.Originals) { ["executor:"+requestId+":missing-config"]=new(JsonSerializer.SerializeToElement(request),JsonSerializer.SerializeToElement(new { outcome="Denied" }),RetainedRevision:1,ResultRetainedRevision:2) } };
        var identity=new AggregateIdentity(tenant,command.Domain,aggregate);var gateway=Substitute.For<IEventStoreGatewayClient>();string incarnation="01ARZ3NDEKTSV4RRFFQ69G5FAY";
        var item=new StreamReadEvent(1,"RecordedFixtureEvent","{}"u8.ToArray(),"json",1,message,null,null,now,"scope",null) { NativeEventDigest=new string('b',64) };
        var nativeProof=NativeSourceProofCodec.ExtendDigest(NativeSourceProofCodec.CreateSeed(identity,incarnation),identity,1,message,item.NativeEventDigest!);
        gateway.ReadWorkloadStreamAsync(Arg.Any<StreamReadRequest>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            var read=call.ArgAt<StreamReadRequest>(0);var events=read.FromSequence==0 && read.ToSequence!=0?new[]{item}:Array.Empty<StreamReadEvent>();
            return new StreamReadPage(identity.TenantId,identity.Domain,identity.AggregateId,events,new(read.FromSequence,read.ToSequence,events.Length==0?null:1,1,events.Length,false,null) { NativeProof=nativeProof });
        });
        gateway.GetWorkloadCommandStatusAsync(tenant,message,Arg.Any<CancellationToken>()).Returns(new CommandStatusQueryResponse("correlation","Completed",(int)CommandStatus.Completed,MessageId:message) { TenantId=tenant,Domain=identity.Domain,AggregateId=identity.AggregateId,CommittedEventSequence=1,EventCount=1 });
        var scope=new QualificationEventStoreAuditScope(tenant,QualificationProtocolSchema.Digest(request["enrolledScope"]!),[":missing-config"],[message],[identity],[],[new("private-security-observation",message,target,identity.Domain,identity.AggregateId)]);
        var native=new QualificationEventStoreAuditReader(_authority.Client(),gateway,TimeProvider.System,new Dictionary<string,QualificationEventStoreAuditScope>{{request["caseId"]!.GetValue<string>(),scope}});
        native.PreflightMutationScope(request).ShouldBeEmpty();var read=await native.ReadAsync(request,token);read.ShouldNotBeNull();Evidence=read.MutationEvidence!.Single();Evidence.Verify(request,_authority.PublicBinding,DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ShouldBeTrue();
        NativeSources=JsonSerializer.SerializeToNode(read.NativeStreams,CustodyBindingContract.Json)!.AsArray();Evidence.MatchesNative(NativeSources).ShouldBeTrue();Trust=new(_authority.PublicBinding,new Dictionary<string,string>{{QualificationMutationTrust.TargetKey(tenant,identity.Domain,identity.AggregateId),target}});
        var supplement=NSubstitute.Substitute.For<IQualificationNativeCaseAuditReader>();
        supplement.ReadAsync(Arg.Any<JsonObject>(),Arg.Any<CancellationToken>()).Returns(new QualificationNativeCaseAudit(1,new(),new(Row()),new(),new(),new JsonObject { ["nativeSourceSnapshot"]=JsonSerializer.SerializeToNode(read with { MutationEvidence=null },CustodyBindingContract.Json) }));
        var assembled=await new QualificationEventStoreCaseAuditReader(native,supplement).ReadAsync(request,token);assembled.ShouldNotBeNull();assembled.TargetMutations.Single()!["authorizationEvidence"].ShouldNotBeNull();
        // The authority can subsequently be revoked without rewriting the admitted historical dispatch fact.
        _authority.RevokeCaller();Evidence.Verify(request,_authority.PublicBinding,DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ShouldBeTrue();
    }
    internal JsonObject Row()=>new() { ["nativeMutationId"]=Evidence.MutationId,["authorizationReceiptId"]=Evidence.AuthorizationId,["targetTenantId"]=Evidence.Source.TenantId,["targetDomain"]=Evidence.Source.Domain,["targetObjectId"]=Evidence.Source.AggregateId,["targetId"]=Evidence.TargetId,
        ["keyVersion"]="v1",["keyState"]="Active",["epochId"]="epoch",["productDisposition"]="Closed",["authorizationEvidence"]=JsonSerializer.SerializeToNode(Evidence,CustodyBindingContract.Json) };
    public void Dispose() { _authority.Dispose();_workload.Dispose();File.Delete(_key); }
}
