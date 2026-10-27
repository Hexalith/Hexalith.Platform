using System.Text.Json;
using Dapr.Client;
using Hexalith.Platform.Custody;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Composition.Tests;

public sealed class AgentsAdmissionCurrentBoundaryTests
{
    [Theory]
    [InlineData("expiry")][InlineData("revocation")][InlineData("profile")][InlineData("unavailable")][InlineData("valid")]
    public async Task ActualAdmitAuthenticatesAfterLastReadinessWithoutRenewingFirstSeen(string mutation)
    {
        var f=new Fixture();var original=await f.Issue();bool changed=false;f.Registrar.Confirmed=()=>f.Armed=true;
        f.Spool.Authority.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(_=>
        {
            if(f.Armed && !changed)
            {
                changed=true;
                if(mutation=="expiry")f.Spool.Clock.Now=original.ExpiresAt;
                if(mutation=="revocation")f.KeyStatus=CustodyStatus.RevokedKey;
                if(mutation=="profile")f.Current=null;
            }
            return mutation=="unavailable" && changed?null:f.Spool.Target;
        });
        int commands=0;var result=await AgentsHostComposition.AdmitAsync(f.Verifier,f.Spool.Spool,original,f.Identity,_=> { commands++;return "command"; },TestContext.Current.CancellationToken);
        changed.ShouldBeTrue();commands.ShouldBe(mutation=="valid"?1:0);result.CommandConstructed.ShouldBe(mutation=="valid");
        f.Registrar.First.ShouldNotBeNull();f.Registrar.Created.ShouldBe(1);var exact=JsonSerializer.Serialize(f.Registrar.First);
        f.Registrar.First.FirstSeenAt.ShouldBe(original.IssuedAt);f.Registrar.First.RetainUntil.ShouldBe(original.IssuedAt+f.Profile.ReplayRetention);
        if(mutation=="valid")
        {
            await AgentsHostComposition.AdmitAsync(f.Verifier,f.Spool.Spool,original,f.Identity,_=>"retry",TestContext.Current.CancellationToken);
            JsonSerializer.Serialize(f.Registrar.First).ShouldBe(exact);f.Registrar.Created.ShouldBe(1);
        }
    }
    [Theory]
    [InlineData("conflict","replay-conflict","tenant",false)][InlineData("provider-denied","unavailable-owner","system",false)]
    [InlineData("unavailable","unavailable-owner","tenant",false)][InlineData("invalid-tag","invalid-tag","system",false)]
    [InlineData("conflict","replay-conflict","tenant",true)][InlineData("provider-denied","unavailable-owner","system",true)]
    [InlineData("unavailable","unavailable-owner","tenant",true)][InlineData("invalid-tag","invalid-tag","system",true)]
    public async Task ActualVerifierAdmissionRecorderAndSpoolPreserveExactDenialCauseRoutingAndOriginal(string scenario,string reason,string route,bool lostAcknowledgment)
    {
        var f=new Fixture();var envelope=await f.Issue();f.Registrar.LoseFirstAcknowledgment=true;
        (await f.Verifier.VerifyAsync(envelope,f.Identity,TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.Succeeded);
        string first=JsonSerializer.Serialize(f.Registrar.First);
        if(scenario=="conflict")
        {
            f.Identity=f.Identity with { LogicalCommandId="different-command" };
            envelope=(await f.Authenticator.IssueNonceBoundChallengeAsync(f.Identity,TimeSpan.FromMinutes(1),envelope.DeliveryNonce,TestContext.Current.CancellationToken)).Envelope!;
            (await f.Authenticator.VerifyAsync(envelope,f.Identity,TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.Succeeded);
        }
        if(scenario=="provider-denied")f.KeyStatus=CustodyStatus.Denied;
        if(scenario=="unavailable")f.Registrar.Available=false;
        if(scenario=="invalid-tag")envelope=envelope with { Tag=new string('0',64) };
        var verifierResult=await f.Verifier.VerifyAsync(envelope,f.Identity,TestContext.Current.CancellationToken);
        verifierResult.DenialCause.ShouldBe(scenario=="conflict"?TrustedEnvelopeDenialCause.ReplayConflictV1:TrustedEnvelopeDenialCause.None);
        var recorder=new PlatformSecurityDenialRecorder(f.Authenticator,new PlatformHmacService(f.Keys,f.Profiles,f.Spool.Clock),f.Spool.Spool,f.Spool.Clock);
        var context=new AgentsIngressDenialContext(recorder,"retained-server-receipt","key-v1",["untrusted-fixture-field"]);
        bool lose=lostAcknowledgment;
        f.Spool.Client.TrySaveStateAsync(f.Spool.Target.ComponentName,Arg.Any<string>(),Arg.Any<SecuritySpoolSnapshot>(),Arg.Any<string>(),Arg.Any<StateOptions>(),Arg.Any<IReadOnlyDictionary<string,string>>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            f.Spool.Persisted=JsonSerializer.SerializeToUtf8Bytes(call.Arg<SecuritySpoolSnapshot>());
            if(lose) { lose=false;return Task.FromException<bool>(new IOException("controlled lost spool acknowledgement")); }
            return Task.FromResult(true);
        });
        int commands=0;var denied=await AgentsHostComposition.AdmitAsync(f.Verifier,f.Spool.Spool,envelope,f.Identity,_=> { commands++;return "forbidden"; },TestContext.Current.CancellationToken,context);
        commands.ShouldBe(0);denied.CommandConstructed.ShouldBeFalse();
        if(!lostAcknowledgment) denied.DenialOriginal.ShouldNotBeNull();
        var recovered=await recorder.ObserveAsync(context.RetainedServerReceiptId,reason,context.UntrustedFields,context.OriginalDigestKeyVersion,envelope,f.Identity,TestContext.Current.CancellationToken);
        recovered.ShouldNotBeNull();recovered.Intent.ReasonCode.ShouldBe(reason);recovered.Intent.RoutingTenantId.ShouldBe(route);
        string retained=JsonSerializer.Serialize(recovered);f.Spool.Clock.Now=f.Spool.Clock.Now.AddSeconds(1);
        var retry=await AgentsHostComposition.AdmitAsync(f.Verifier,f.Spool.Spool,envelope,f.Identity,_=> { commands++;return "forbidden"; },TestContext.Current.CancellationToken,context);
        retry.Command.ShouldBe("SecurityAuditUnavailable");commands.ShouldBe(0); // Pending audit evidence blocks ingress until its exact drain.
        var reread=await recorder.ObserveAsync(context.RetainedServerReceiptId,reason,context.UntrustedFields,context.OriginalDigestKeyVersion,envelope,f.Identity,TestContext.Current.CancellationToken);
        JsonSerializer.Serialize(reread).ShouldBe(retained);JsonSerializer.Serialize(f.Registrar.First).ShouldBe(first);f.Registrar.Created.ShouldBe(1);f.Spool.Read()!.Records.Count.ShouldBe(1);
    }
    private sealed class Fixture
    {
        internal readonly AgentsSpoolHarness Spool=new();internal readonly PlatformSigningProfile Profile;internal PlatformSigningProfile? Current;
        internal readonly IPlatformSigningProfileProvider Profiles=Substitute.For<IPlatformSigningProfileProvider>();internal readonly IPlatformHmacKeyProvider Keys=Substitute.For<IPlatformHmacKeyProvider>();
        internal readonly Registrar Registrar=new();internal readonly TrustedEnvelopeAuthenticator Authenticator;internal readonly TrustedEnvelopeReplayVerifier Verifier;internal CustodyStatus KeyStatus=CustodyStatus.Succeeded;internal bool Armed;
        internal TrustedEnvelopeIdentity Identity=new(1,"profile","issuer",new(TrustedPrincipalKind.User,"tenant","actor","party",1,"roles",null,null,null,null),"Contract","Edit","tenant","resource","correlation","cause",["tenant"],"fingerprint","digest-v1","audience","logical");
        internal Fixture()
        {
            var now=Spool.Clock.Now;Profile=new("profile","issuer","audience",now.AddDays(-1),now.AddDays(1),TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(600),TimeSpan.FromDays(1),TimeSpan.FromDays(7));Current=Profile;Profiles.GetCurrent().Returns(_=>Current);
            Keys.ResolveAsync(Arg.Any<PlatformHmacScope>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(call=>
            {
                var scope=call.Arg<PlatformHmacScope>();return scope.Purpose==PlatformHmacPurpose.TrustedEnvelope && KeyStatus!=CustodyStatus.Succeeded?new PlatformHmacKeyResolution(KeyStatus):new PlatformHmacKeyResolution(CustodyStatus.Succeeded,new(scope,new("key-v1",PlatformHmacKeyState.Active,now.AddDays(-1),now.AddDays(1),null),Enumerable.Repeat((byte)42,32).ToArray()));
            });
            Authenticator=new(Keys,Profiles,Spool.Clock);Verifier=new(Authenticator,Registrar,Profiles,Spool.Clock);
        }
        internal async Task<TrustedEnvelope> Issue()=>(await Authenticator.IssueAsync(Identity,TimeSpan.FromMinutes(1),TestContext.Current.CancellationToken)).Envelope!;
    }
    private sealed class Registrar:ITrustedEnvelopeReplayRegistrar
    {
        internal TrustedEnvelopeReplayReceipt? First;internal int Created;internal bool Available=true,LoseFirstAcknowledgment;internal Action? Confirmed;
        public Task<TrustedEnvelopeReplayReceipt?> RegisterAsync(TrustedEnvelopeReplayIntent intent,DateTimeOffset at,TimeSpan retention,CancellationToken token)
        {
            if(!Available)return Task.FromResult<TrustedEnvelopeReplayReceipt?>(null);
            if(First is null) { First=new(intent,at,at+retention,1,"native-first-seen");Created++;if(LoseFirstAcknowledgment) { LoseFirstAcknowledgment=false;return Task.FromException<TrustedEnvelopeReplayReceipt?>(new IOException("controlled lost first-seen acknowledgement")); } }
            return Task.FromResult(First.Intent==intent?First:null);
        }
        public Task<TrustedEnvelopeReplayReceipt?> LookupAsync(TrustedEnvelopeReplayIntent intent,CancellationToken token) { Confirmed?.Invoke();return Task.FromResult(Available && First?.Intent==intent?First:null); }
        public Task<TrustedEnvelopeReplayReceipt?> LookupFirstSeenAsync(TrustedEnvelopeReplayIntent intent,CancellationToken token)=>Task.FromResult(Available && First?.Intent.Issuer==intent.Issuer && First.Intent.DeliveryNonce==intent.DeliveryNonce?First:null);
    }
}
