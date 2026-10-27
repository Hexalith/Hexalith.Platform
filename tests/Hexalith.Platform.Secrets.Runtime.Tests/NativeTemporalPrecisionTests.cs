using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class NativeTemporalPrecisionTests
{
    [Theory]
    [InlineData("S2.expiry.before",-1,false)][InlineData("S2.expiry.at",1,false)][InlineData("S2.expiry.after",1,false)]
    [InlineData("S2.expiry.before",-10000,true)][InlineData("S2.expiry.at",0,true)][InlineData("S2.expiry.after",10000,true)]
    [InlineData("S2.overlap-before",-1,false)][InlineData("S2.overlap-at",1,false)][InlineData("S2.overlap-after",1,false)]
    [InlineData("S2.overlap-before",-10000,true)][InlineData("S2.overlap-at",0,true)][InlineData("S2.overlap-after",10000,true)]
    public async Task ActualRetainedReaderPreservesNativeFractionalSideAndRefusesLossyMillisecondClassification(string id,long ticks,bool supported)
    {
        var edge=new DateTimeOffset(2026,10,25,12,0,0,TimeSpan.Zero).AddTicks(5000);var evaluated=edge.AddTicks(ticks);
        bool overlap=id.StartsWith("S2.overlap-",StringComparison.Ordinal);var status=evaluated<edge?CustodyStatus.Succeeded:overlap?CustodyStatus.OutsideKeyWindow:CustodyStatus.Expired;
        var request=QualificationHostTests.Request();request["caseId"]=id;
        var original=new JsonObject { ["caseId"]=id,["requestId"]=request["requestId"]!.DeepClone(),["outcome"]=status==CustodyStatus.Succeeded?"Verified":status==CustodyStatus.Expired?"Expired":"Denied",["status"]=status.ToString(),
            ["temporalEvidenceVersion"]=2,["checkedAt"]=evaluated.ToUnixTimeMilliseconds(),["evaluatedAtExact"]=JsonSerializer.SerializeToNode(evaluated,CustodyBindingContract.Json),
            ["authenticationDecision"]=JsonSerializer.SerializeToNode(new TrustedEnvelopeResult(status,EvaluatedAt:evaluated),CustodyBindingContract.Json),
            ["originalEnvelope"]=new JsonObject { ["ExpiresAt"]=JsonSerializer.SerializeToNode(edge,CustodyBindingContract.Json),["SigningKeyVersion"]="v1" },
            ["keyMetadata"]=JsonSerializer.SerializeToNode(new PlatformHmacKeyMetadata("v1",PlatformHmacKeyState.Retained,edge.AddDays(-1),edge.AddDays(1),edge.AddSeconds(-600)),CustodyBindingContract.Json),["rotationOverlapMilliseconds"]=600000L };
        var originals=Substitute.For<IQualificationOriginalStore>();originals.LookupResultAsync("executor",Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(original);
        var facts=new JsonObject { ["temporalOriginal"]=original.DeepClone(),["rotationOriginal"]=new JsonObject { ["rotatedAt"]=edge.AddSeconds(-600).ToUnixTimeMilliseconds(),["rotatedAtExact"]=JsonSerializer.SerializeToNode(edge.AddSeconds(-600),CustodyBindingContract.Json) } };
        var audit=Substitute.For<IQualificationNativeCaseAuditReader>();audit.ReadAsync(request,Arg.Any<CancellationToken>()).Returns(_=>new QualificationNativeCaseAudit(1,[],[],[],[],(JsonObject)facts.DeepClone()));
        string exact=original.ToJsonString();var reader=new QualificationTemporalOriginalReader(originals,audit);var actual=await reader.ReadAsync(request,TestContext.Current.CancellationToken);
        if(supported)
        {
            actual.ShouldNotBeNull();actual.Source["nativeTemporalPrecision"]!["evaluatedAtExact"]!.Deserialize<DateTimeOffset>().ShouldBe(evaluated);
            long measured=actual.Source["state"]!["timing"]!["checkedAt"]!.GetValue<long>();Math.Sign(measured-edge.ToUnixTimeMilliseconds()).ShouldBe(Math.Sign(ticks));
        }
        else actual.ShouldBeNull();
        original.ToJsonString().ShouldBe(exact);
        if(overlap)
        {
            // Historical ms-only originals stay stored/readable but do not prove exact overlap timing.
            original.Remove("evaluatedAtExact");facts["temporalOriginal"]=original.DeepClone();(await reader.ReadAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
        }
    }
}
