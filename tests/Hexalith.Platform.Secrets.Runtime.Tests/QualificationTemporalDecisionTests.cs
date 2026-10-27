using System.Text.Json.Nodes;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationTemporalDecisionTests
{
    [Theory]
    [InlineData("S2.future-skew")][InlineData("S2.maximum-lifetime")][InlineData("S2.emergency-redispatch")]
    public async Task RetainsTheActualSelectedAuthenticatorDecisionIncludingAwaitedReplacement(string id)
    {
        var clock=new Clock();var initial=clock.Now;var profile=new PlatformSigningProfile("profile","issuer","audience",initial.AddDays(-1),initial.AddDays(2),TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(600),TimeSpan.FromDays(1),TimeSpan.FromDays(7));
        var profiles=Substitute.For<IPlatformSigningProfileProvider>();profiles.GetCurrent().Returns(profile);bool revoke=false,advance=false;int reads=0;
        var keys=Substitute.For<IPlatformHmacKeyProvider>();keys.ResolveAsync(Arg.Any<PlatformHmacScope>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(call=>new ValueTask<PlatformHmacKeyResolution>(Read(call)));
        async Task<PlatformHmacKeyResolution> Read(NSubstitute.Core.CallInfo call)
        {
            await Task.Yield();if(advance) clock.Now=clock.Now.AddSeconds(1);reads++;string? version=call.ArgAt<string?>(1);
            if(revoke && version=="v1") return new PlatformHmacKeyResolution(CustodyStatus.RevokedKey);
            return new PlatformHmacKeyResolution(CustodyStatus.Succeeded,new(call.ArgAt<PlatformHmacScope>(0),new(revoke?"v2":"v1",PlatformHmacKeyState.Active,initial.AddDays(-1),initial.AddDays(2),null),new byte[32]));
        }
        var auth=new TrustedEnvelopeAuthenticator(keys,profiles,clock);var identity=new TrustedEnvelopeIdentity(1,"profile","issuer",new(TrustedPrincipalKind.Administrator,"tenant","actor",null,null,"roles",null,null,null,null),"contract","operation","tenant","resource","correlation","cause",["tuple"],new string('a',64),"digest","audience","logical");
        var envelope=(await auth.IssueAsync(identity,TimeSpan.FromSeconds(300),TestContext.Current.CancellationToken)).Envelope.ShouldNotBeNull();
        if(id=="S2.future-skew") clock.Now=initial.AddSeconds(-60);
        if(id=="S2.maximum-lifetime") envelope=envelope with { ExpiresAt=envelope.ExpiresAt.AddSeconds(1) };
        if(id=="S2.emergency-redispatch") { revoke=true;advance=true; }
        var scope=new JsonObject { ["tenantId"]="tenant" };var request=new JsonObject { ["caseId"]=id,["requestId"]=new string('a',64),["enrolledScope"]=scope };
        var originals=new MemoryOriginals();var workflow=new QualificationTemporalCaseWorkflow(auth,keys,profiles,clock,originals,new Dictionary<string,QualificationTemporalCaseInput>{{id,new(envelope,identity,QualificationProtocolSchema.Digest(scope))}});
        int before=reads;var result=(await workflow.ExecuteAsync(request,TestContext.Current.CancellationToken)).ShouldNotBeNull();result["temporalEvidenceVersion"]!.GetValue<int>().ShouldBe(2);
        result["checkedAt"]!.GetValue<long>().ShouldBe(clock.Now.ToUnixTimeMilliseconds());
        if(id=="S2.emergency-redispatch") { clock.Now.ShouldBeGreaterThan(initial);reads.ShouldBeGreaterThan(before+2);result["replacementEnvelope"].ShouldNotBeNull();result["status"]!.GetValue<string>().ShouldBe("Succeeded"); }
    }
    private sealed class Clock:TimeProvider { internal DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now; }
    private sealed class MemoryOriginals:IQualificationOriginalStore
    {
        private readonly Dictionary<string,JsonObject> _results=[];private readonly HashSet<string> _admitted=[];
        public Task<bool> BeginAsync(string role,string id,JsonObject original,CancellationToken token)=>Task.FromResult(_admitted.Add(id));
        public Task<JsonObject?> LookupResultAsync(string role,string id,CancellationToken token)=>Task.FromResult(_results.GetValueOrDefault(id));
        public Task RetainAsync(string role,string id,JsonObject result,CancellationToken token) { _results.Add(id,(JsonObject)result.DeepClone());return Task.CompletedTask; }
    }
}
