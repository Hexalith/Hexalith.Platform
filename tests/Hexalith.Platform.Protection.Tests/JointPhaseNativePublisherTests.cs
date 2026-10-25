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
            output.ShouldNotBeNull();string path=Directory.GetFiles(Path.Combine(archive,new string('a',64))).Single();byte[] bytes=await File.ReadAllBytesAsync(path,TestContext.Current.CancellationToken);
            var envelope=JsonNode.Parse(bytes)!.AsObject();var payload=envelope["payload"]!;
            signer.VerifyData(QualificationProofVerifier.CanonicalBytes(payload),Convert.FromBase64String(envelope["signature"]!.GetValue<string>()),HashAlgorithmName.SHA256,DSASignatureFormat.Rfc3279DerSequence).ShouldBeTrue();
            output["sourceRecordsSha256"]!.GetValue<string>().ShouldBe(QualificationProtocolSchema.Digest(payload["records"]!));
            payload["records"]![0]!["payload"]!["protection"]!["OwnerRevision"]!.GetValue<long>().ShouldBe(1);
            System.Text.Encoding.UTF8.GetString(bytes).ShouldNotContain("private-native-bytes");
            JsonNode.DeepEquals(output,await publisher.PublishAsync("host:H4",request,TestContext.Current.CancellationToken)).ShouldBeTrue();
            (await File.ReadAllBytesAsync(path,TestContext.Current.CancellationToken)).ShouldBe(bytes);
            (await publisher.PreflightAsync("host:H4",request,TestContext.Current.CancellationToken)).ShouldNotBeEmpty();
            gateway.ReceivedCalls().ShouldAllBe(call=>call.GetMethodInfo().Name==nameof(IEventStoreGatewayClient.ReadStreamAsync));
        }
        finally { if(Directory.Exists(archive)) Directory.Delete(archive,true); }
    }
}
