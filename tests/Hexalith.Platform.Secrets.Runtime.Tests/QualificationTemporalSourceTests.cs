using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

// Fixture clock/material only. These tests establish source behavior, never installed timing or qualification.
public sealed class QualificationTemporalSourceTests
{
    [Theory]
    [InlineData("S2.expiry.before",-1,"Verified")]
    [InlineData("S2.expiry.at",0,"Expired")]
    [InlineData("S2.expiry.after",1,"Expired")]
    public async Task ActualAuthenticatorUsesExclusiveBoundaryAndHistoricalObservationDoesNotReevaluateAfterClockCrossing(string id,int seconds,string outcome)
    {
        var clock=new Clock();var profiles=Substitute.For<IPlatformSigningProfileProvider>();profiles.GetCurrent().Returns(new PlatformSigningProfile("v1","issuer","audience",clock.Now.AddDays(-1),clock.Now.AddDays(2),TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(600),TimeSpan.FromDays(1),TimeSpan.FromDays(7)));
        var keys=new Keys(clock);var auth=new TrustedEnvelopeAuthenticator(keys,profiles,clock);
        var identity=new TrustedEnvelopeIdentity(1,"v1","issuer",new(TrustedPrincipalKind.User,"tenant","actor","party",1,"roles",null,null,null,null),"Contracts.EditProposal","EditProposal","tenant","interaction","correlation","cause",["tenant","User","actor","EditProposal","client-key"],"fingerprint","digest-v1","audience","logical-command");
        var issuance=await auth.IssueAsync(identity,TimeSpan.FromSeconds(300),TestContext.Current.CancellationToken);issuance.Status.ShouldBe(CustodyStatus.Succeeded);
        var request=new JsonObject { ["caseId"]=id,["requestId"]=new string('a',64),["caseTargetId"]="logical",["enrolledScope"]=new JsonObject { ["tenantId"]="tenant",["logicalId"]="logical",["targetIds"]=new JsonArray(),["providerCopyIds"]=new JsonArray() } };
        clock.Now=issuance.Envelope!.ExpiresAt.AddSeconds(seconds);
        var originals=Substitute.For<IQualificationOriginalStore>();JsonObject? retained=null;
        originals.LookupResultAsync("executor",Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(_=>Task.FromResult(retained));
        var workflow=new QualificationTemporalCaseWorkflow(auth,keys,profiles,clock,originals,new Dictionary<string,QualificationTemporalCaseInput>{{id,new(issuance.Envelope,identity,QualificationProtocolSchema.Digest(request["enrolledScope"]!))}});
        (await workflow.PreflightAsync(request,TestContext.Current.CancellationToken)).ShouldContain("External.ImmutableNativeTemporalEvaluationWithActualTimestampProtocol");
        (await workflow.ExecuteAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
        // Independently supplied historical exact original remains readable; no source clock evaluation supplies it.
        var actual=new JsonObject { ["caseId"]=id,["requestId"]=request["requestId"]!.DeepClone(),["outcome"]=outcome,["checkedAt"]=clock.Now.ToUnixTimeMilliseconds(),["originalEnvelope"]=JsonSerializer.SerializeToNode(issuance.Envelope,CustodyBindingContract.Json) };retained=actual;
        var audit=Substitute.For<IQualificationNativeCaseAuditReader>();audit.ReadAsync(request,Arg.Any<CancellationToken>()).Returns(new QualificationNativeCaseAudit(1,[],[],[],[],new JsonObject { ["temporalOriginal"]=actual.DeepClone() }));
        clock.Now=issuance.Envelope.ExpiresAt.AddHours(1);
        (await auth.VerifyAsync(issuance.Envelope,identity,TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.Expired);
        (await workflow.PreflightAsync(request,TestContext.Current.CancellationToken)).ShouldBeEmpty();
        var registration=workflow.Registrations().Single();(await registration.Preflight(request,TestContext.Current.CancellationToken)).ShouldBeEmpty();
        (await registration.Lookup(request,TestContext.Current.CancellationToken))!.ToJsonString().ShouldBe(actual.ToJsonString());
        var observed=await new QualificationTemporalOriginalReader(originals,audit).ReadAsync(request,TestContext.Current.CancellationToken);
        observed!.Source["state"]!["outcome"]!.GetValue<string>().ShouldBe(outcome);
        var changed=(JsonObject)actual.DeepClone();changed["checkedAt"]=0;
        audit.ReadAsync(request,Arg.Any<CancellationToken>()).Returns(new QualificationNativeCaseAudit(1,[],[],[],[],new JsonObject { ["temporalOriginal"]=changed }));
        (await new QualificationTemporalOriginalReader(originals,audit).ReadAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();
    }
    [Fact]
    public async Task WrongActualBoundaryRefusesRatherThanUsingSuppliedClock()
    {
        var clock=new Clock();var profiles=Substitute.For<IPlatformSigningProfileProvider>();profiles.GetCurrent().Returns(new PlatformSigningProfile("v1","issuer","audience",clock.Now.AddDays(-1),clock.Now.AddDays(2),TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(600),TimeSpan.FromDays(1),TimeSpan.FromDays(7)));
        var keys=new Keys(clock);var auth=new TrustedEnvelopeAuthenticator(keys,profiles,clock);
        var identity=new TrustedEnvelopeIdentity(1,"v1","issuer",new(TrustedPrincipalKind.User,"tenant","actor","party",1,"roles",null,null,null,null),"Contracts.EditProposal","EditProposal","tenant","interaction","correlation","cause",["tenant","User","actor","EditProposal","client-key"],"fingerprint","digest-v1","audience","logical-command");
        var envelope=(await auth.IssueAsync(identity,TimeSpan.FromSeconds(300),TestContext.Current.CancellationToken)).Envelope!;
        var scope=new JsonObject { ["tenantId"]="tenant" };var request=new JsonObject { ["caseId"]="S2.expiry.at",["requestId"]="request",["enrolledScope"]=scope,["checkedAt"]=envelope.ExpiresAt.ToUnixTimeMilliseconds() };
        var originals=Substitute.For<IQualificationOriginalStore>();var workflow=new QualificationTemporalCaseWorkflow(auth,keys,profiles,clock,originals,new Dictionary<string,QualificationTemporalCaseInput>{{"S2.expiry.at",new(envelope,identity,QualificationProtocolSchema.Digest(scope))}});
        (await workflow.PreflightAsync(request,TestContext.Current.CancellationToken)).ShouldContain("External.ImmutableNativeTemporalEvaluationWithActualTimestampProtocol");
        (await workflow.ExecuteAsync(request,TestContext.Current.CancellationToken)).ShouldBeNull();await originals.DidNotReceive().BeginAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<JsonObject>(),Arg.Any<CancellationToken>());
    }
    private sealed class Clock:TimeProvider { public DateTimeOffset Now=new(2026,10,25,0,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now; }
    private sealed class Keys(Clock clock):IPlatformHmacKeyProvider
    { public ValueTask<PlatformHmacKeyResolution> ResolveAsync(PlatformHmacScope scope,string? version=null,CancellationToken cancellationToken=default)=>ValueTask.FromResult(new PlatformHmacKeyResolution(CustodyStatus.Succeeded,new(scope,new(version??"v1",PlatformHmacKeyState.Active,clock.Now.AddDays(-1),clock.Now.AddDays(2),null),new byte[32]))); }
}
