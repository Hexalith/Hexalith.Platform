using System.Text.Json;
using Hexalith.EventStore.Contracts.Commands;
using Shouldly;
using NSubstitute;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class DirectNestedResultAuthorityTests
{
    [Theory]
    [InlineData("unchanged")][InlineData("nested-revoked")][InlineData("original-dispatched")][InlineData("nested-expired-during-save")][InlineData("legacy-exact")][InlineData("legacy-unmapped")]
    public async Task RecordingAuthorizationKeepsItsExactNestedWorkloadAndOriginalThroughTheFinalHead(string change)
    {
        var clock=new Clock();using var fixture=new IndependentAuthorityReceiverTests.Fixture(clock:clock);const string method="recording-command-authorize";
        string domain=change.StartsWith("legacy-",StringComparison.Ordinal)?"agents":"trusted-envelope-replay",role=domain=="agents"?"private-security-observation":"private-replay";
        var outer=fixture.State.Callers[0] with { Role=role,Methods=[method] };
        var nested=outer with { Identity="actual-native-worker",BearerSha256=new string('b',64),ScopeReference="principal",Methods=["recording-source:"+(domain=="agents"?(change=="legacy-exact"?"legacy-security-event-log":"agents"):domain)],ValidUntil=clock.GetUtcNow().AddSeconds(5) };
        var command=new SubmitCommandRequest("original-message","system",domain,"aggregate","fixture-command",JsonSerializer.SerializeToElement(new { exactOriginal=true }),IdempotencyKey:"original-message");
        var original=new IndependentOriginal(command.Payload,null);string retained=JsonSerializer.Serialize(original,CustodyBindingContract.Json);
        fixture.State=fixture.State with { Callers=[outer,nested],Originals=new(){{role+":original-message",original}} };
        int reads=0;fixture.BeforeRead=()=>
        {
            if(++reads==3)
            {
                if(change=="nested-revoked") fixture.State=fixture.State with { Callers=[outer,nested with { Revoked=true }] };
                if(change=="original-dispatched") fixture.State=fixture.State with { Originals=new(){{role+":original-message",original with { SubmissionState="Dispatched" }}} };
            }
            return Task.CompletedTask;
        };
        if(change=="nested-expired-during-save") fixture.HeadSaveOverride=(_,_)=> { clock.Advance();return null; };
        var result=await fixture.Client().InvokeAsync<JsonElement>(method,new { caller=nested.Identity,principal=nested.ScopeReference,command },TestContext.Current.CancellationToken);
        if(change is "unchanged" or "legacy-exact") { result.ValueKind.ShouldBe(JsonValueKind.Object);result.GetProperty("Allowed").GetBoolean().ShouldBeTrue(); }
        else if(change=="legacy-unmapped") { result.ValueKind.ShouldBe(JsonValueKind.Object);result.GetProperty("Allowed").GetBoolean().ShouldBeFalse(); }
        else result.ValueKind.ShouldBe(JsonValueKind.Undefined);
        fixture.RecordingSource.ReceivedCalls().ShouldBeEmpty();JsonSerializer.Serialize(original,CustodyBindingContract.Json).ShouldBe(retained);
    }
    private sealed class Clock:TimeProvider
    {
        private DateTimeOffset _now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>_now;
        internal void Advance()=>_now=_now.AddSeconds(6);
    }
}
