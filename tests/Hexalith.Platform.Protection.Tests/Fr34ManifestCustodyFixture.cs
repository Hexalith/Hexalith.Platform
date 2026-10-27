using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;

namespace Hexalith.Platform.Protection.Tests;

// Disposable authenticated custody transport fixture. Its signed originals exercise source verification and establish no physical erasure or installed authority.
internal sealed class Fr34ManifestCustodyFixture:ICustodyLifecycleCallSource,IDisposable
{
    private readonly ECDsa _key=ECDsa.Create(ECCurve.NamedCurves.nistP256);private readonly DaprClient _dapr=Substitute.For<DaprClient>();
    private readonly ReceivingAuthorityFixture _locators=new("fr34-engine");
    private readonly AuthorityEnrollmentV1 _enrollment=new("fixture-custody","fixture-incarnation","store","profile","trust","anchors",new string('A',64));
    internal readonly Fr34NativeManifestBinding Binding;internal CustodyLifecycleClient Client { get; }internal DaprCustodyOriginalLocators Locators { get; }
    internal string Fault="normal";internal int Destroys,Reads;private byte[]? _original;
    internal readonly List<string> Operations=[];
    internal void SetFault(string fault)
    {
        Fault=fault;
        if(fault=="changed-manifest") _locators.State.Manifests[Binding.ReservationReceiptId]=Binding.Original with { Capability=Binding.Original.Capability with { BatchId="changed-existing-batch" } };
        if(fault=="changed-target") _locators.State.Manifests[Binding.ReservationReceiptId]=Binding.Original with { Targets=[new("tenant","foreign-interaction","foreign-alias")] };
    }
    internal Fr34ManifestCustodyFixture(ProtectionTarget target)
    {
        var payload=new DeletionBatchCapabilityV1("independent-fixture-issuer","fixture-protection","tenant","existing-deletion","existing-seal","accepted",1,"existing-batch",DeletionBatchCapabilityIdentity.TargetManifestDigest([target]),"existing-guard",1,1,1,"existing-key");
        Binding=new("independently-existing-reservation",new(payload,"fixture-original-detached-signature",2,"existing-dispatch",3,[target]));
        _locators.State=_locators.State with { Manifests=new(){{Binding.ReservationReceiptId,Binding.Original}},Callers=_locators.State.Callers.Select(value=>value with { ScopeReference="tenant",Methods=["custody-reserved-manifest-original"] }).ToArray() };
        Locators=new(_locators.Authority);Client=new(_dapr,new Dictionary<string,string>{{CustodyLifecycleClient.LogicalRole,"fixture-custody"}},"fixture-owner",JsonSerializer.Serialize(_enrollment,CustodyBindingContract.Json),_key.ExportSubjectPublicKeyInfoPem(),this);
        _dapr.InvokeBindingAsync(Arg.Any<BindingRequest>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            var binding=call.Arg<BindingRequest>();var request=CustodyBindingContract.Decode(binding.Data.Span)??throw new IOException("exact custody request required");var envelope=request.Envelope;
            Operations.Add(envelope.Operation);
            if(Fault=="outage") throw new IOException("isolated independent custody outage");
            CustodyBindingResponseV1 response;
            if(request.Manifest is { } manifest)
            {
                if(manifest.ProtectionReservationReceiptId!=Binding.ReservationReceiptId || JsonSerializer.Serialize(manifest.Original)!=JsonSerializer.Serialize(Binding.Original)) throw new IOException("existing native reservation changed");
                if(envelope.Operation=="destroy-manifest")
                {
                    Destroys++;
                    if(Fault!="unknown-native")
                    {
                        var original=new CustodyBindingOriginalV1(new("tenant",Binding.Original.Capability.BatchId,Binding.ReservationReceiptId,DeletionManifestProviderState.Consumed,[new(target,Binding.Original.Capability.BatchId,Fault=="wrong-receipt"?"":"existing-native-destruction-receipt")]),envelope.RequestDigest,Binding.Original.Capability.ManifestDigest,Fault=="missing-witness"?"":"existing-independent-complete-restore-barrier",[]);
                        original=original with { Signature=_key.SignData(JsonSerializer.SerializeToUtf8Bytes(original,CustodyBindingContract.Json),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence) };
                        if(Fault=="bad-signature") original=original with { Signature=new byte[64] };
                        _original=JsonSerializer.SerializeToUtf8Bytes(original,CustodyBindingContract.Json);
                    }
                    if(Fault=="lost-completion") throw new IOException("isolated native result acknowledgment loss after original retention");
                }
                else Reads++;
                if(_original is null)
                {
                    var status=new CustodyBindingManifestStatusV1(new("tenant",Binding.Original.Capability.BatchId,Binding.ReservationReceiptId,DeletionManifestProviderState.Unknown,[]),_enrollment,Binding.Original.Capability.ManifestDigest,[]);
                    status=status with { Signature=_key.SignData(JsonSerializer.SerializeToUtf8Bytes(status,CustodyBindingContract.Json),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence) };
                    response=new(CustodyBindingContract.Protocol,envelope.OperationId,envelope.RequestDigest,ManifestStatus:status);
                }
                else
                {
                    var header=new CustodyBindingOriginalHeaderV1(_enrollment,"existing-native-original","tenant",envelope.OperationId,envelope.RequestDigest,Binding.Original.Capability.BatchId,Binding.ReservationReceiptId,Binding.Original.Capability.ManifestDigest,_original.Length,1,CustodyBindingContract.Hash(_original));
                    response=new(CustodyBindingContract.Protocol,envelope.OperationId,envelope.RequestDigest,OriginalHeader:header);
                }
            }
            else if(request.Page is { } page && _original is not null)
            {
                byte[] bytes=_original.AsSpan(page.Offset,page.Length).ToArray();response=new(CustodyBindingContract.Protocol,envelope.OperationId,envelope.RequestDigest,Page:new(page.Header,page.Offset,page.Length,CustodyBindingContract.Hash(bytes),bytes));
            }
            else throw new IOException("closed original-only custody fixture");
            var proof=new CustodyBindingResultProofV1(envelope.RequestAttemptId,envelope.Challenge,_enrollment,CustodyBindingContract.Hash(JsonSerializer.SerializeToUtf8Bytes(response,CustodyBindingContract.Json)),[]);
            proof=proof with { Signature=_key.SignData(JsonSerializer.SerializeToUtf8Bytes(proof,CustodyBindingContract.Json),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence) };
            return new BindingResponse(binding,JsonSerializer.SerializeToUtf8Bytes(response with { ResultProof=proof },CustodyBindingContract.Json),new Dictionary<string,string>());
        });
    }
    public Task<CustodyPreparedCall?> PrepareAsync(CustodyCallIntent intent,CancellationToken token)
    {
        var proof=new ControlAdmissionProofV1("fixture-control","custody","fixture-original-proof",_enrollment.AuthorityIncarnation,"tenant",Binding.Original.Capability.ManifestDigest,intent.Operation,null,2,"existing-issue",null,null,3,"existing-dispatch",Binding.ReservationReceiptId,"fixture-independent-policy",1,new string('A',64),[1]);
        var envelope=new CustodyBindingEnvelopeV1(CustodyBindingContract.Protocol,intent.Operation,intent.OperationId,intent.RequestDigest,intent.TenantId,intent.OwnerPurposeScope,_enrollment,1,new string('A',64),intent.RequestAttemptId,intent.Challenge,proof);
        return Task.FromResult<CustodyPreparedCall?>(new(JsonSerializer.Serialize(envelope,CustodyBindingContract.Json),4*1024*1024,new string('A',64),null));
    }
    public Task<CustodyCallAuthorization?> AuthorizeAsync(CustodyCallIntent intent,CustodyPreparedCall prepared,string digest,CancellationToken token)=>Task.FromResult<CustodyCallAuthorization?>(new(digest,new Dictionary<string,string>{{"custody-operation-proof","isolated-exact-operation-proof"},{"custody-control-proof","isolated-exact-control-proof"}}));
    public void Dispose() { _key.Dispose();_dapr.Dispose();_locators.Dispose(); }
}
