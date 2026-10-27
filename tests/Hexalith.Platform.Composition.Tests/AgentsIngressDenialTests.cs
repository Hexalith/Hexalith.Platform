namespace Hexalith.Platform.Composition.Tests;

using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

public sealed class AgentsIngressDenialTests
{
    [Fact]
    public async Task ClassifiedDenialRequiresActualDurableObservationBeforeResultAndMissingCaptureRemainsUnavailable()
    {
        var harness=new AgentsSpoolHarness();var clock=harness.Clock;
        var profile=new PlatformSigningProfile("profile","issuer","audience",clock.Now.AddDays(-1),clock.Now.AddDays(1),TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(600),TimeSpan.FromDays(1),TimeSpan.FromDays(7));
        var profiles=Substitute.For<IPlatformSigningProfileProvider>();profiles.GetCurrent().Returns(profile);
        var keys=Substitute.For<IPlatformHmacKeyProvider>();keys.ResolveAsync(Arg.Any<PlatformHmacScope>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(call=>new PlatformHmacKeyResolution(CustodyStatus.Succeeded,new(call.Arg<PlatformHmacScope>(),new("key-v1",PlatformHmacKeyState.Active,clock.Now.AddDays(-1),clock.Now.AddDays(1),null),Enumerable.Repeat((byte)42,32).ToArray())));
        var authenticator=new TrustedEnvelopeAuthenticator(keys,profiles,clock);var registrar=Substitute.For<ITrustedEnvelopeReplayRegistrar>();var verifier=new TrustedEnvelopeReplayVerifier(authenticator,registrar,profiles,clock);
        var identity=new TrustedEnvelopeIdentity(1,"profile","issuer",new(TrustedPrincipalKind.User,"tenant","actor","party",1,"roles",null,null,null,null),"Contract","Edit","tenant","resource","correlation","cause",["tenant"],"fingerprint","digest-v1","audience","logical");
        var invalid=new TrustedEnvelope(identity,clock.Now,clock.Now.AddMinutes(2),"nonce","key-v1",new string('0',64));int constructed=0;
        var missing=await AgentsHostComposition.AdmitAsync(verifier,harness.Spool,invalid,identity,_=> { constructed++;return "command"; },TestContext.Current.CancellationToken);
        missing.Command.ShouldBe("SecurityAuditUnavailable");missing.DenialOriginal.ShouldBeNull();if(harness.Read() is { } initial) initial.Records.ShouldBeEmpty();
        var recorder=new PlatformSecurityDenialRecorder(authenticator,new PlatformHmacService(keys,profiles,clock),harness.Spool,clock);
        var context=new AgentsIngressDenialContext(recorder,"original-server-receipt","key-v1",["fixture-untrusted-field"]);
        var denied=await AgentsHostComposition.AdmitAsync(verifier,harness.Spool,invalid,identity,_=> { constructed++;return "command"; },TestContext.Current.CancellationToken,context);
        denied.Command.ShouldBe("InvalidTag");denied.CommandConstructed.ShouldBeFalse();constructed.ShouldBe(0);denied.DenialOriginal.ShouldNotBeNull();
        var stored=harness.Read()!.Records.Single();stored.Intent.ShouldBe(denied.DenialOriginal.Intent);stored.ObservedAt.ShouldBe(denied.DenialOriginal.ObservedAt);stored.Intent.RoutingTenantId.ShouldBe("system");
        registrar.ReceivedCalls().ShouldBeEmpty();
    }
}
