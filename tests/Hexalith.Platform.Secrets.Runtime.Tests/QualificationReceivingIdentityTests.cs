using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationReceivingIdentityTests
{
    [Theory]
    [InlineData("executor","qualification-executor-execute")]
    [InlineData("observer","qualification-observer-observe")]
    [InlineData("enrollment","qualification-enrollment-enroll")]
    public async Task ActualReceivingRoleRejectsArbitraryIdsAndChangedCompletePublicInputBeforeOriginalOrDriverAdmission(string role,string method)
    {
        using var fixture=new IndependentAuthorityReceiverTests.Fixture();using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request=QualificationHostTests.Request(role);var source=QualificationHostTests.Request();
        fixture.State=fixture.State with { QualificationBindings=new Dictionary<string,string>(new[]{"runId","targetCommit","isolatedTargetId","planSha256"}.Select(name=>new KeyValuePair<string,string>(name,request[name]!.ToJsonString()))),
            QualificationCases=new(){{source["caseId"]!.GetValue<string>(),JsonSerializer.SerializeToElement(new { caseTargetId=source["caseTargetId"]!.GetValue<string>(),enrolledScope=source["enrolledScope"] },CustodyBindingContract.Json)}} };
        var originals=Substitute.For<IQualificationOriginalStore>();var driver=Substitute.For<IQualificationOwnerCaseDriver>();driver.CaseId.Returns(source["caseId"]!.GetValue<string>());
        var receiving=new QualificationOwnerOperationHandler(new(role,"fixture-"+role,"v1",key.ExportSubjectPublicKeyInfoPem()),key,originals,new Dictionary<string,IQualificationOwnerCaseDriver>{{driver.CaseId,driver}},TimeProvider.System);
        var caller=fixture.State.Callers[0] with { Role=role };
        var changed=(JsonObject)request.DeepClone();changed["requestId"]=new string('f',64);
        await Should.ThrowAsync<InvalidOperationException>(()=>receiving.HandleAsync(method,JsonSerializer.SerializeToElement(changed),caller,fixture.State,TestContext.Current.CancellationToken));
        changed=(JsonObject)request.DeepClone();
        if(role=="enrollment") changed["isolatedTargetId"]="substituted-installation";
        else changed["scenario"]!["expected"]!["immutableIdentity"]=0;
        changed.Remove("requestId");changed["requestId"]=QualificationProtocolSchema.Digest(changed);
        await Should.ThrowAsync<InvalidOperationException>(()=>receiving.HandleAsync(method,JsonSerializer.SerializeToElement(changed),caller,fixture.State,TestContext.Current.CancellationToken));
        originals.ReceivedCalls().ShouldBeEmpty();driver.ReceivedCalls().Count(call=>call.GetMethodInfo().Name!="get_CaseId").ShouldBe(0);fixture.Saves.ShouldBe(0);
    }
}
