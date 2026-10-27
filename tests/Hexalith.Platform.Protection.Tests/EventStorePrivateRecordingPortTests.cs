using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

public sealed class EventStorePrivateRecordingPortTests
{
    [Theory]
    [InlineData("changed-fields",false)][InlineData("cross-tenant",false)][InlineData("foreign-issuer",false)]
    [InlineData("changed-fields",true)][InlineData("cross-tenant",true)][InlineData("foreign-issuer",true)]
    public async Task ActualEnvelopeWorkflowSeedsAuthenticFirstSeenThenChallengesActualReceivingBoundary(string field,bool loseResultAcknowledgement)
    {
        using var native=new Fixture();using var receiving=new ReceivingAuthorityFixture("replay-registrar",native.Port);using var evaluations=new OriginalTransport();var token=TestContext.Current.CancellationToken;
        receiving.State=receiving.State with { Callers=receiving.State.Callers.Select(value=>value with { Methods=["replay-register-exact","replay-lookup-exact","replay-lookup-first-seen"] }).ToArray() };
        int keyReads=0;var keys=Substitute.For<IPlatformHmacKeyProvider>();
        keys.ResolveAsync(Arg.Any<PlatformHmacScope>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(call=> { keyReads++;return ValueTask.FromResult(new PlatformHmacKeyResolution(CustodyStatus.Succeeded,new(call.ArgAt<PlatformHmacScope>(0),new("v1",PlatformHmacKeyState.Active,native.Now.AddHours(-1),native.Now.AddHours(1),null),Enumerable.Repeat((byte)42,32).ToArray()))); });
        var profiles=Substitute.For<IPlatformSigningProfileProvider>();var profile=new PlatformSigningProfile("profile","issuer","audience",native.Now.AddHours(-1),native.Now.AddHours(1),TimeSpan.FromSeconds(300),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(600),TimeSpan.FromDays(1),TimeSpan.FromDays(7));profiles.GetCurrent().Returns(profile);
        var auth=new TrustedEnvelopeAuthenticator(keys,profiles,TimeProvider.System);var identity=new TrustedEnvelopeIdentity(1,"profile","issuer",new(TrustedPrincipalKind.Administrator,"tenant-a","actor",null,null,"roles",null,null,null,null),"contract","operation","tenant-a","resource","correlation","cause",["tuple"],new string('a',64),"digest-v1","audience","logical");
        var baseline=(await auth.IssueAsync(identity,TimeSpan.FromSeconds(300),token)).Envelope.ShouldNotBeNull();
        var changed=field=="changed-fields"?identity with { PayloadFingerprint=new string('b',64) }:field=="cross-tenant"?identity with { TargetTenantId="tenant-b",Principal=identity.Principal with { ActorTenantId="tenant-b" } }:identity with { Issuer="foreign-issuer" };
        var foreignProfiles=Substitute.For<IPlatformSigningProfileProvider>();foreignProfiles.GetCurrent().Returns(profile with { Issuer="foreign-issuer" });var foreign=new TrustedEnvelopeAuthenticator(keys,foreignProfiles,TimeProvider.System);
        var changedEnvelope=(await (field=="foreign-issuer"?foreign:auth).IssueNonceBoundChallengeAsync(changed,TimeSpan.FromSeconds(300),baseline.DeliveryNonce,token)).Envelope.ShouldNotBeNull();
        (await (field=="foreign-issuer"?foreign:auth).VerifyAsync(changedEnvelope,changed,token)).Status.ShouldBe(CustodyStatus.Succeeded);
        var registrar=new DaprIndependentReplayRegistrar(receiving.Authority);var replay=new TrustedEnvelopeReplayVerifier(auth,registrar,profiles,TimeProvider.System);string id="S2.replay-reject."+field;
        var request=new System.Text.Json.Nodes.JsonObject { ["caseId"]=id,["requestId"]=new string('a',64),["enrolledScope"]=new System.Text.Json.Nodes.JsonObject { ["tenantId"]="tenant-a" } };
        var originalStore=new ReplayEvaluationLoss(new InstalledQualificationOriginalStore(evaluations.Authority),loseResultAcknowledgement);
        QualificationEnvelopeCaseWorkflow Workflow()=>new(auth,replay,evaluations.Authority,new Dictionary<string,TrustedEnvelopeIdentity>{{id,identity}},registrar,originalStore,new Dictionary<string,QualificationReplayChallengeInput>{{id,loseResultAcknowledgement?new(ChangedIdentity:changed):new(baseline,changedEnvelope)}},new Dictionary<string,TrustedEnvelopeAuthenticator>{{id,foreign}});
        (await Workflow().PreflightAsync(request,token)).ShouldBeEmpty();
        System.Text.Json.Nodes.JsonObject result;
        if(loseResultAcknowledgement) { await Should.ThrowAsync<IOException>(()=>Workflow().ExecuteAsync(request,token));result=originalStore.Attempted.ShouldNotBeNull(); }
        else result=(await Workflow().ExecuteAsync(request,token)).ShouldNotBeNull();
        result["originalAdmissionStatus"]!.GetValue<string>().ShouldBe("Succeeded");result["changedAuthenticationStatus"]!.GetValue<string>().ShouldBe("Succeeded");
        result["firstStatus"]!.GetValue<string>().ShouldBe(field=="foreign-issuer"?"Invalid":"Denied");result["replayOriginal"].ShouldNotBeNull();native.Commands.Count.ShouldBe(1);
        int reads=keyReads;var recovered=(await Workflow().LookupAsync(request,token)).ShouldNotBeNull();recovered.ToJsonString().ShouldBe(result.ToJsonString());keyReads.ShouldBe(reads);native.Commands.Count.ShouldBe(1);
        (await Workflow().ExecuteAsync(request,token)).ShouldNotBeNull().ToJsonString().ShouldBe(result.ToJsonString());keyReads.ShouldBe(reads);native.Commands.Count.ShouldBe(1);
        using var restarted=new ReceivingAuthorityFixture("replay-registrar",native.ReopenPort());restarted.State=restarted.State with { Callers=restarted.State.Callers.Select(value=>value with { Methods=["replay-lookup-exact","replay-lookup-first-seen"] }).ToArray() };
        var firstSeen=result["replayOriginal"]!.Deserialize<TrustedEnvelopeReplayReceipt>(CustodyBindingContract.Json).ShouldNotBeNull();
        (await new DaprIndependentReplayRegistrar(restarted.Authority).LookupAsync(firstSeen.Intent,token)).ShouldBe(firstSeen);native.Commands.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ActualVersionTwoObservationPersistsAcrossRestartAndDrainsToTheExactNativeHashedReceiptStream()
    {
        using var native=new Fixture();var source=new Hexalith.Platform.Custody.Tests.SecuritySpoolFixture(sourceVersion:2,now:native.Now);
        var spool=new ReplicatedSecurityObservationSpool(source.Client,source.Clock,source.Authority,native.Port);
        var record=await spool.ObserveAsync(Hexalith.Platform.Custody.Tests.SecuritySpoolFixture.Intent("version-two-observe"),TestContext.Current.CancellationToken);
        record.ShouldNotBeNull();record.SourceVersion.ShouldBe(2);source.Read()!.Records.Single().SourceVersion.ShouldBe(2);
        var restarted=new ReplicatedSecurityObservationSpool(source.Client,source.Clock,source.Authority,native.Port);
        await restarted.DrainAsync(10,TestContext.Current.CancellationToken);
        var persisted=source.Read()!.Records.Single();persisted.SourceVersion.ShouldBe(2);persisted.Receipt.ShouldNotBeNull();
        string aggregate=Convert.ToHexStringLower(SHA256.HashData(PlatformCanonicalBytes.Identity(["security-event-log",record.Intent.RoutingTenantId,record.UtcDay])));
        string expected=new AggregateIdentity(record.Intent.RoutingTenantId,"security-event-log",aggregate).ActorId;
        persisted.Receipt.SourceStreamId.ShouldBe(expected);native.Commands.Single().Command.Domain.ShouldBe("security-event-log");native.Commands.Single().Command.AggregateId.ShouldBe(aggregate);
    }

    [Fact]
    public async Task ReplayUsesActualNoStreamSourceAndRejectsCrossTenantNonceReuse()
    {
        using var fixture = new Fixture();
        var intent = new TrustedEnvelopeReplayIntent("issuer", "nonce", "tenant-a", "logical", "v1", new string('A', 64));
        var first = await fixture.Port.RegisterAsync(intent, fixture.Now, TimeSpan.FromSeconds(604800), TestContext.Current.CancellationToken);
        first.ShouldNotBeNull(); first.SourceRevision.ShouldBe(1); first.ReceiptId.ShouldBe(fixture.Commands.Single().Command.MessageId);
        var command = fixture.Commands.Single().Command;
        command.Tenant.ShouldBe("system"); command.Domain.ShouldBe("trusted-envelope-replay");
        command.CommandType.ShouldBe("Hexalith.Agents.Contracts.TrustedEnvelopeReplay.RegisterTrustedEnvelopeFirstSeen");
        command.AggregateId.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(PlatformCanonicalBytes.Identity(["trusted-envelope-replay", "issuer", "nonce"]))));
        var retry = await fixture.Port.RegisterAsync(intent, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(604800), TestContext.Current.CancellationToken);
        retry.ShouldBe(first); fixture.Commands.Count.ShouldBe(1);
        (await fixture.Port.RegisterAsync(intent with { TargetTenantId = "tenant-b" }, fixture.Now, TimeSpan.FromSeconds(604800), TestContext.Current.CancellationToken)).ShouldBeNull();
        fixture.Commands.Count.ShouldBe(1);
    }

    [Fact]
    public async Task LostRecorderAcknowledgementUsesExactNativeSourceAndNeverRepeatsAppend()
    {
        using var fixture = new Fixture { LoseAcknowledgement = true };
        var record = fixture.Record;
        await Should.ThrowAsync<IOException>(() => fixture.Port.AppendAsync(record, TestContext.Current.CancellationToken));
        var original = await fixture.Port.LookupAsync(record, TestContext.Current.CancellationToken);
        original.State.ShouldBe(SecurityEventRecorderLookupState.Recorded);
        original.Receipt!.SourceStreamId.ShouldBe(ReplicatedSecurityObservationSpool.SourceStream(record));
        await fixture.Port.AppendAsync(record, TestContext.Current.CancellationToken);
        fixture.Commands.Count.ShouldBe(1);
        var command = fixture.Commands.Single().Command;
        command.Tenant.ShouldBe(record.Intent.RoutingTenantId); command.Domain.ShouldBe("security-event-log");
        command.CommandType.ShouldBe("Hexalith.Agents.Contracts.SecurityEventLog.RecordSecurityEvent");
    }

    [Theory]
    [InlineData("duplicate")][InlineData("conflicting")][InlineData("unsupported")][InlineData("malformed")][InlineData("unrelated")]
    public async Task CompleteAuthenticatedNativeHistoryMustValidateBeforeRecorderCompletion(string tail)
    {
        using var authority=new ReceivingAuthorityFixture("private-security-observation");using var fixture=new Fixture(authority.Authority,authority.ProofKey) { LoseAcknowledgement=true };var token=TestContext.Current.CancellationToken;
        await Should.ThrowAsync<IOException>(()=>fixture.Port.AppendAsync(fixture.Record,token));fixture.AddSecurityTail(tail);
        var lookup=await fixture.Port.LookupAsync(fixture.Record,token);
        lookup.State.ShouldBe(tail=="unrelated"?SecurityEventRecorderLookupState.Recorded:SecurityEventRecorderLookupState.Unknown);
        var original=await authority.Authority.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="private-security-observation",requestId=PrivateRecordingIdentity.SecurityOriginalMessageId(fixture.Record.Intent.ObservationId) },token);
        original.ShouldNotBeNull();if(tail=="unrelated") original.Result.ShouldNotBeNull();else original.Result.ShouldBeNull();fixture.Commands.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ScopeProofDenialPreventsGatewayEffectAndUnknownNativeReadPreventsAppend()
    {
        using var fixture = new Fixture { ProofDenied = true };
        await Should.ThrowAsync<InvalidOperationException>(() => fixture.Port.AppendAsync(fixture.Record, TestContext.Current.CancellationToken));
        fixture.Commands.ShouldBeEmpty();
        fixture.MissingNativeProof = true;
        (await fixture.Port.LookupAsync(fixture.Record, TestContext.Current.CancellationToken)).State.ShouldBe(SecurityEventRecorderLookupState.Unknown);
    }

    [Fact]
    public async Task ProvenUnsentReplayAfterProofDenialRetainsOriginalTimeAndCanProgress()
    {
        using var fixture = new Fixture { ProofDenied=true };
        var intent = new TrustedEnvelopeReplayIntent("issuer","unsent-nonce","tenant-a","logical","v1",new string('A',64));
        await Should.ThrowAsync<InvalidOperationException>(()=>fixture.Port.RegisterAsync(intent,fixture.Now,TimeSpan.FromSeconds(604800),TestContext.Current.CancellationToken));
        fixture.Commands.ShouldBeEmpty();
        fixture.ProofDenied=false;
        DateTimeOffset retryAt=DateTimeOffset.UtcNow;
        retryAt.ShouldBeGreaterThan(fixture.Now);
        var receipt = await fixture.Port.RegisterAsync(intent,retryAt,TimeSpan.FromSeconds(604800),TestContext.Current.CancellationToken);
        receipt.ShouldNotBeNull(); receipt.FirstSeenAt.ShouldBe(fixture.Now);
        fixture.Commands.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(CommandStatus.Received)]
    [InlineData(CommandStatus.Processing)]
    [InlineData(CommandStatus.EventsStored)]
    [InlineData(CommandStatus.TimedOut)]
    public async Task QueuedOrUncertainDispatchedCommandWithEmptyNativeSourceRemainsLookupOnly(CommandStatus status)
    {
        using var fixture=new Fixture { QueueWithoutEvent=true,OriginalStatus=status };
        var record=fixture.Record;await fixture.Port.AppendAsync(record,TestContext.Current.CancellationToken);fixture.Commands.Count.ShouldBe(1);
        (await fixture.Port.LookupAsync(record,TestContext.Current.CancellationToken)).State.ShouldBe(SecurityEventRecorderLookupState.Unknown);
        var grant=new SecurityObservationRecoveryGrant(new("spool","epoch","authority",DateTimeOffset.UtcNow.AddHours(1)),record,new(SecurityEventRecorderLookupState.NotRecorded),"revision",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddMinutes(5));
        await Should.ThrowAsync<InvalidOperationException>(()=>fixture.Port.RecoverOriginalAsync(record,grant,TestContext.Current.CancellationToken));
        fixture.Commands.Count.ShouldBe(1);fixture.RecoveryAuthority.ReceivedCalls().ShouldBeEmpty();
    }
    [Theory]
    [InlineData(CommandStatus.Completed)]
    [InlineData(CommandStatus.Rejected)]
    public async Task ActualTerminalNonretryableNoEventAndNativeProofCanEstablishNotRecorded(CommandStatus status)
    {
        using var fixture=new Fixture { QueueWithoutEvent=true,OriginalStatus=status };
        await fixture.Port.AppendAsync(fixture.Record,TestContext.Current.CancellationToken);
        (await fixture.Port.LookupAsync(fixture.Record,TestContext.Current.CancellationToken)).State.ShouldBe(SecurityEventRecorderLookupState.NotRecorded);
        fixture.Retryable=true;(await fixture.Port.LookupAsync(fixture.Record,TestContext.Current.CancellationToken)).State.ShouldBe(SecurityEventRecorderLookupState.Unknown);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefinitiveRejectedOriginalUsesOneRetainedNewTransportWithoutChangingLogicalObservationOrResendingUnknown(bool queueRecovery)
    {
        using var fixture=new Fixture { RejectOrdinary=true,QueueRecovery=queueRecovery };
        var record=fixture.Record;await fixture.Port.AppendAsync(record,TestContext.Current.CancellationToken);
        var proof=await fixture.Port.LookupAsync(record,TestContext.Current.CancellationToken);proof.State.ShouldBe(SecurityEventRecorderLookupState.NotRecorded);proof.NoRecordProof!.Basis.ShouldBe("TerminalNoEvent");
        var grant=new SecurityObservationRecoveryGrant(new("spool","epoch","authority",DateTimeOffset.UtcNow.AddHours(1)),record,proof,"revision",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddMinutes(5));
        fixture.RecoveryAuthority.VerifyAgedOriginalRecoveryAsync(grant,Arg.Any<SecurityEventRecorderLookup>(),Arg.Any<CancellationToken>()).Returns(true);
        await fixture.Port.RecoverOriginalAsync(record,grant,TestContext.Current.CancellationToken);fixture.Commands.Count.ShouldBe(2);
        var original=fixture.Commands[0].Command;var recovery=fixture.Commands[1].Command;
        recovery.MessageId.ShouldNotBe(original.MessageId);recovery.MessageId.ShouldBe(PrivateRecordingIdentity.SecurityRecoveryMessageId(grant));recovery.IdempotencyKey.ShouldBe(recovery.MessageId);
        recovery.Payload.GetRawText().ShouldBe(original.Payload.GetRawText());recovery.AggregateId.ShouldBe(original.AggregateId);
        var actual=await fixture.Port.LookupAsync(record,TestContext.Current.CancellationToken);
        if(queueRecovery)
        {
            actual.State.ShouldBe(SecurityEventRecorderLookupState.Unknown);
            await Should.ThrowAsync<InvalidOperationException>(()=>fixture.Port.RecoverOriginalAsync(record,grant,TestContext.Current.CancellationToken));
        }
        else
        {
            actual.State.ShouldBe(SecurityEventRecorderLookupState.Recorded);actual.Receipt!.ObservationId.ShouldBe(record.Intent.ObservationId);
            await fixture.Port.RecoverOriginalAsync(record,grant,TestContext.Current.CancellationToken);
        }
        fixture.Commands.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ReplayTerminalRejectionUsesDeterministicNewTransportAndKeepsOriginalFirstSeenWhileUncertainRecoveryStaysLookupOnly(bool uncertain)
    {
        using var fixture=new Fixture { RejectReplayOrdinary=true,QueueReplayRecovery=uncertain };
        var intent=new TrustedEnvelopeReplayIntent("issuer","rejected-replay","tenant-a","logical","v1",new string('A',64));
        (await fixture.Port.RegisterAsync(intent,fixture.Now,TimeSpan.FromSeconds(604800),TestContext.Current.CancellationToken)).ShouldBeNull();fixture.Commands.Count.ShouldBe(1);
        var native=await fixture.Port.ReadReplayRecoveryProofAsync(intent,TestContext.Current.CancellationToken);native.ShouldNotBeNull();native.NativeProof.Basis.ShouldBe("TerminalNoEvent");
        var result=await fixture.Port.RegisterAsync(intent,DateTimeOffset.UtcNow,TimeSpan.FromSeconds(604800),TestContext.Current.CancellationToken);fixture.Commands.Count.ShouldBe(2);
        var first=fixture.Commands[0].Command;var recovery=fixture.Commands[1].Command;recovery.MessageId.ShouldBe(PrivateRecordingIdentity.ReplayRecoveryMessageId(native));recovery.MessageId.ShouldNotBe(first.MessageId);
        recovery.Payload.GetRawText().ShouldBe(first.Payload.GetRawText());
        if(uncertain) { result.ShouldBeNull();(await fixture.Port.ReadReplayRecoveryProofAsync(intent,TestContext.Current.CancellationToken)).ShouldBeNull(); }
        else { result.ShouldNotBeNull();result.FirstSeenAt.ShouldBe(fixture.Now);result.RetainUntil.ShouldBe(fixture.Now.AddSeconds(604800)); }
        await fixture.Port.RegisterAsync(intent,DateTimeOffset.UtcNow,TimeSpan.FromSeconds(604800),TestContext.Current.CancellationToken);fixture.Commands.Count.ShouldBe(2);
    }
    [Fact]
    public async Task LegacyUnversionedSecurityOriginalKeepsExactAgentsDayLocatorAndVersionTwoUsesItsEnrolledHash()
    {
        using var fixture=new Fixture();var legacy=fixture.Record with { SourceVersion=0 };
        string expected=new AggregateIdentity(legacy.Intent.RoutingTenantId,"agents","SecurityEventLog-"+legacy.UtcDay).ActorId;
        ReplicatedSecurityObservationSpool.SourceStream(legacy).ShouldBe(expected);
        await fixture.Port.AppendAsync(legacy,TestContext.Current.CancellationToken);var receipt=(await fixture.Port.LookupAsync(legacy,TestContext.Current.CancellationToken)).Receipt!;
        receipt.SourceStreamId.ShouldBe(expected);fixture.Commands.Single().Command.Domain.ShouldBe("agents");fixture.Commands.Single().Command.AggregateId.ShouldBe("SecurityEventLog-"+legacy.UtcDay);
        JsonSerializer.Serialize(legacy).ShouldNotContain("SourceVersion");
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task NativeAndCompletionAcknowledgmentLossRecoverTheExactResultAndSettleOnlyOnce(bool loseRetention)
    {
        using var authority=new ReceivingAuthorityFixture("private-replay");using var fixture=new Fixture(authority.Authority,authority.ProofKey) { LoseAcknowledgement=!loseRetention };
        authority.LoseRetentionResponse=loseRetention;
        var intent=new TrustedEnvelopeReplayIntent("issuer","lost-completion","tenant-a","logical","v1",new string('A',64));
        await Should.ThrowAsync<IOException>(()=>fixture.Port.RegisterAsync(intent,fixture.Now,TimeSpan.FromSeconds(604800),TestContext.Current.CancellationToken));
        if(loseRetention) authority.State.PendingOriginalCompletionBytes.ShouldBe(0);else authority.State.PendingOriginalCompletionBytes.ShouldNotBeNull().ShouldBeGreaterThan(0L);
        var result=await fixture.Port.LookupAsync(intent,TestContext.Current.CancellationToken);result.ShouldNotBeNull();authority.State.PendingOriginalCompletionBytes.ShouldBe(0);
        (await fixture.Port.RegisterAsync(intent,fixture.Now,TimeSpan.FromSeconds(604800),TestContext.Current.CancellationToken)).ShouldBe(result);
        authority.State.PendingOriginalCompletionBytes.ShouldBe(0);authority.State.PrivateOriginalsCount.ShouldBe(1);fixture.Commands.Count.ShouldBe(1);
    }
    [Fact]
    public async Task SustainedRecordingBeyondLifetimeCapSettlesEveryNativeCompletionWithoutDeletingOriginalHistory()
    {
        var storage=Substitute.For<IIndependentArchiveStorage>();storage.AvailableBytes(Arg.Any<string>()).Returns(512L*1024*1024);
        var archive=new CompletionArchive();using var authority=new ReceivingAuthorityFixture("private-replay",storage:storage,originalArchive:archive);
        using var fixture=new Fixture(authority.Authority,authority.ProofKey);
        const int count=10025;
        for(int index=0;index<count;index++)
        {
            var intent=new TrustedEnvelopeReplayIntent("issuer","sustained-"+index,"tenant-a","logical-"+index,"v1",new string('A',64));
            var receipt=await fixture.Port.RegisterAsync(intent,fixture.Now,TimeSpan.FromSeconds(604800),TestContext.Current.CancellationToken);
            receipt.ShouldNotBeNull();authority.State.PendingOriginalCompletionBytes.ShouldBe(0);
        }
        authority.State.PrivateOriginalsCount.ShouldBe(count);fixture.Commands.Count.ShouldBe(count);authority.State.Originals.ShouldBeEmpty();
        foreach(int index in new[]{0,5000,count-1})
        {
            string id=fixture.Commands[index].Command.MessageId;
            var original=await authority.Authority.InvokeAsync<IndependentOriginal>("qualification-lookup-original",new { role="private-replay",requestId=id },TestContext.Current.CancellationToken);
            original.ShouldNotBeNull();original.Result.ShouldNotBeNull();original.Request.GetProperty("deliveryNonce").GetString().ShouldBe("sustained-"+index);
            var indexed=await new IndependentPrivateOriginalIndex(archive).ReadAsync(authority.State.PrivateOriginalsRoot,"private-replay",id,TestContext.Current.CancellationToken);
            indexed!.PreviousValueSha256.ShouldNotBeNull();
        }
    }
    [Theory]
    [InlineData("unsent",1)][InlineData("rejected",2)][InlineData("queued",1)][InlineData("native-loss",1)][InlineData("retention-loss",1)]
    public async Task OuterReplayReceiverReachesOnlyRegistrarGuardedExactRecovery(string boundary,int expectedCommands)
    {
        var nativeRecovery=Substitute.For<IPrivateRecordingEventStore,IPrivateReplayRecoverySource>();
        using var sourceAuthority=new ReceivingAuthorityFixture("private-replay",nativeRecovery);using var native=new Fixture(sourceAuthority.Authority,sourceAuthority.ProofKey);
        ((IPrivateReplayRecoverySource)nativeRecovery).ReadReplayRecoveryProofAsync(Arg.Any<TrustedEnvelopeReplayIntent>(),Arg.Any<CancellationToken>()).Returns(call=>native.Port.ReadReplayRecoveryProofAsync(call.Arg<TrustedEnvelopeReplayIntent>(),call.Arg<CancellationToken>()));
        using var outer=new ReceivingAuthorityFixture("replay-registrar",native.Port);
        outer.State=outer.State with { Callers=outer.State.Callers.Select(value=>value with { Methods=["replay-register-exact","replay-lookup-exact"] }).ToArray() };
        native.ProofDenied=boundary=="unsent";native.RejectReplayOrdinary=boundary=="rejected";native.QueueWithoutEvent=boundary=="queued";native.LoseAcknowledgement=boundary=="native-loss";sourceAuthority.LoseRetentionResponse=boundary=="retention-loss";
        var registrar=new DaprIndependentReplayRegistrar(outer.Authority);var intent=new TrustedEnvelopeReplayIntent("issuer","outer-"+boundary,"tenant-a","logical","v1",new string('a',64));
        var first=await registrar.RegisterAsync(intent,native.Now,TimeSpan.FromSeconds(604800),TestContext.Current.CancellationToken);first.ShouldBeNull();
        native.ProofDenied=false;native.LoseAcknowledgement=false;
        var recovered=await registrar.RegisterAsync(intent,native.Now,TimeSpan.FromSeconds(604800),TestContext.Current.CancellationToken);
        native.Commands.Count.ShouldBe(expectedCommands);
        if(boundary=="queued") recovered.ShouldBeNull();else { recovered.ShouldNotBeNull();recovered.FirstSeenAt.ShouldBe(native.Now);recovered.RetainUntil.ShouldBe(native.Now.AddSeconds(604800)); }
        (await registrar.RegisterAsync(intent,native.Now,TimeSpan.FromSeconds(604800),TestContext.Current.CancellationToken)).ShouldBe(recovered);native.Commands.Count.ShouldBe(expectedCommands);
        outer.RevokeWorkload("recording-owner");
        (await registrar.RegisterAsync(intent,native.Now,TimeSpan.FromSeconds(604800),TestContext.Current.CancellationToken)).ShouldBeNull();native.Commands.Count.ShouldBe(expectedCommands);
    }
    [Theory]
    [InlineData("before-begin",1)][InlineData("unsent",1)][InlineData("queued",1)][InlineData("native-loss",1)][InlineData("retention-loss",1)][InlineData("rejected",1)]
    public async Task ExistingOuterRecorderAdmissionReachesActualDurableInnerBarriersWithoutRepeatingUnknownDispatch(string boundary,int expectedCommands)
    {
        using var sourceAuthority=new ReceivingAuthorityFixture("private-security-observation");using var native=new Fixture(sourceAuthority.Authority,sourceAuthority.ProofKey);
        using var outer=new ReceivingAuthorityFixture("security-recorder",native.Port);
        outer.State=outer.State with { Callers=outer.State.Callers.Select(value=>value with { Methods=["security-observation-append","security-observation-lookup"] }).ToArray() };
        native.MissingNativeProof=boundary=="before-begin";native.ProofDenied=boundary=="unsent";native.QueueWithoutEvent=boundary=="queued";native.LoseAcknowledgement=boundary=="native-loss";sourceAuthority.LoseRetentionResponse=boundary=="retention-loss";native.RejectOrdinary=boundary=="rejected";
        var recorder=new DaprSecurityObservationRecorder(outer.Authority);var record=native.Record;
        if(boundary is "before-begin" or "unsent" or "queued" or "native-loss" or "retention-loss") await Should.ThrowAsync<InvalidOperationException>(()=>recorder.AppendAsync(record,TestContext.Current.CancellationToken));
        else await recorder.AppendAsync(record,TestContext.Current.CancellationToken);
        native.MissingNativeProof=false;native.ProofDenied=false;native.LoseAcknowledgement=false;
        if(boundary is "queued" or "rejected") await Should.ThrowAsync<InvalidOperationException>(()=>recorder.AppendAsync(record,TestContext.Current.CancellationToken));
        else { await recorder.AppendAsync(record,TestContext.Current.CancellationToken);await recorder.AppendAsync(record,TestContext.Current.CancellationToken); }
        native.Commands.Count.ShouldBe(expectedCommands);
        (await recorder.LookupAsync(record,TestContext.Current.CancellationToken)).State.ShouldBe(boundary=="queued"?SecurityEventRecorderLookupState.Unknown:boundary=="rejected"?SecurityEventRecorderLookupState.NotRecorded:SecurityEventRecorderLookupState.Recorded);
        outer.RevokeWorkload("recording-owner");await Should.ThrowAsync<InvalidOperationException>(()=>recorder.AppendAsync(record,TestContext.Current.CancellationToken));native.Commands.Count.ShouldBe(expectedCommands);
    }
    [Fact]
    public async Task ActualReplayCaseRegisteredLookupRecoversNativeFirstSeenAfterCaseResultRetentionLoss()
    {
        using var source=new ReceivingAuthorityFixture("private-replay");using var native=new Fixture(source.Authority,source.ProofKey);
        using var receiving=new ReceivingAuthorityFixture("replay-registrar",native.Port);using var caseAuthority=new ReceivingAuthorityFixture("executor");var token=TestContext.Current.CancellationToken;
        receiving.State=receiving.State with { Callers=receiving.State.Callers.Select(value=>value with { Methods=["replay-register-exact","replay-lookup-exact"] }).ToArray() };
        var registrar=new DaprIndependentReplayRegistrar(receiving.Authority);var originals=new InstalledQualificationOriginalStore(caseAuthority.Authority);
        var scope=new System.Text.Json.Nodes.JsonObject { ["tenantId"]="tenant-a",["targetIds"]=new System.Text.Json.Nodes.JsonArray(new string('a',64)),["providerCopyIds"]=new System.Text.Json.Nodes.JsonArray(),["logicalId"]=new string('d',64),["sealId"]="seal",["batchId"]="batch",["manifestDigest"]=new string('e',64),["initialAttestationOrdinal"]=1,["initialKeyVersion"]="v1",["successorKeyVersion"]="v2",["exclusiveExpiry"]=1000,["issuer"]="issuer",["nonceDigest"]=new string('f',64) };
        string caseId="S2.replay-retention-recovery";
        using var schemaStream=typeof(QualificationProofVerifier).Assembly.GetManifestResourceStream("ExtSecretsPublicCaseSchemas")!;
        var schema=System.Text.Json.Nodes.JsonNode.Parse(schemaStream)!["cases"]!.AsArray().Single(value=>value!["id"]!.GetValue<string>()==caseId)!;
        var request=new System.Text.Json.Nodes.JsonObject { ["protocol"]="EXT-SECRETS-1.qualification.v1",["runId"]="fixture-run",["targetCommit"]=new string('a',40),["isolatedTargetId"]="isolated",["planSha256"]=QualificationProtocolSchema.PlanSha256,["caseId"]=caseId,["caseTargetId"]=new string('d',64),["enrolledScope"]=scope,
            ["scenario"]=new System.Text.Json.Nodes.JsonObject { ["outcomes"]=schema["outcomes"]!.DeepClone(),["boundary"]="none",["persistedStages"]=new System.Text.Json.Nodes.JsonArray("AfterEffect","AfterRestart","AfterRestore","AfterLookup"),["expected"]=schema["expected"]!.DeepClone() } };
        request["requestId"]=QualificationProtocolSchema.Digest(request);request=SecretsProof.Parse(request.ToJsonString())!;QualificationProtocolSchema.ValidOwnerRequest("executor",request).ShouldBeTrue();
        var intent=new TrustedEnvelopeReplayIntent("issuer","fixture-case-result-loss","tenant-a","logical","v1",new string('a',64));
        var inputs=new Dictionary<string,QualificationReplayCaseInput>{{caseId,new(intent,native.Now,QualificationProtocolSchema.Digest(scope))}};
        var loss=new ReplayCaseResultLoss(originals);var first=new QualificationReplayCaseWorkflow(registrar,loss,inputs,TimeProvider.System).Registrations().Single();
        await Should.ThrowAsync<IOException>(()=>first.Execute(request,token));native.Commands.Count.ShouldBe(1);loss.Attempted.ShouldNotBeNull();
        string requestId=request["requestId"]!.GetValue<string>();(await originals.LookupResultAsync("executor",requestId+":case-result",token)).ShouldBeNull();
        string nativeBytes=native.Commands[0].Command.Payload.GetRawText();var nativeOriginal=await registrar.LookupAsync(intent,token);nativeOriginal.ShouldNotBeNull();
        // Recreate the workflow and recover through its registered callback in the actual bound driver.
        var recreated=new QualificationReplayCaseWorkflow(registrar,originals,inputs,TimeProvider.System).Registrations().Single();
        var recovery=Substitute.For<IQualificationRecoveryController>();recovery.PreflightAsync(Arg.Any<System.Text.Json.Nodes.JsonObject>(),Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());
        var reader=Substitute.For<IQualificationObservedOriginalReader>();reader.PreflightStagesAsync(Arg.Any<System.Text.Json.Nodes.JsonObject>(),Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());
        reader.ReadStageAsync(Arg.Any<System.Text.Json.Nodes.JsonObject>(),Arg.Any<QualificationObservationStageRequest>(),Arg.Any<CancellationToken>()).Returns((QualificationObservedOriginal?)null);
        var projector=Substitute.For<IQualificationMeasuredCaseProjector>();projector.MissingInputs(caseId).Returns(Array.Empty<string>());
        var driver=new QualificationBoundSourceCaseDriver(caseId,recreated.Preflight,recreated.Execute,recreated.Lookup,new(originals,recovery,reader),projector,originals);
        // No complete observation protocol is fabricated: source recovery succeeds and observation remains unavailable.
        (await driver.LookupAsync(request,token)).ShouldBeNull();
        System.Text.Json.Nodes.JsonNode.DeepEquals(await originals.LookupResultAsync("executor",requestId+":case-result",token),loss.Attempted).ShouldBeTrue();
        native.Commands.Count.ShouldBe(1);native.Commands[0].Command.Payload.GetRawText().ShouldBe(nativeBytes);(await registrar.LookupAsync(intent,token)).ShouldBe(nativeOriginal);
        receiving.Methods.Count(value=>value=="replay-register-exact").ShouldBe(1);receiving.Methods.ShouldContain("replay-lookup-exact");
        receiving.State=receiving.State with { Callers=receiving.State.Callers.Select(value=>value with { Methods=["replay-register-exact"] }).ToArray() };
        (await recreated.Lookup(request,token)).ShouldBeNull();native.Commands.Count.ShouldBe(1);
        System.Text.Json.Nodes.JsonNode.DeepEquals(await originals.LookupResultAsync("executor",requestId+":case-result",token),loss.Attempted).ShouldBeTrue();
    }
    private sealed class ReplayCaseResultLoss(IQualificationOriginalStore inner):IQualificationOriginalStore
    {
        internal System.Text.Json.Nodes.JsonObject? Attempted;
        public Task<bool> BeginAsync(string role,string id,System.Text.Json.Nodes.JsonObject request,CancellationToken token)=>inner.BeginAsync(role,id,request,token);
        public Task<System.Text.Json.Nodes.JsonObject?> LookupResultAsync(string role,string id,CancellationToken token)=>inner.LookupResultAsync(role,id,token);
        public Task RetainAsync(string role,string id,System.Text.Json.Nodes.JsonObject result,CancellationToken token)
        { Attempted=(System.Text.Json.Nodes.JsonObject)result.DeepClone();throw new IOException("fixture case-result retention loss"); }
    }
    private sealed class ReplayEvaluationLoss(IQualificationOriginalStore inner,bool lose):IQualificationOriginalStore
    {
        internal System.Text.Json.Nodes.JsonObject? Attempted;private bool _lost;
        public Task<bool> BeginAsync(string role,string id,System.Text.Json.Nodes.JsonObject request,CancellationToken token)=>inner.BeginAsync(role,id,request,token);
        public Task<QualificationOriginalAdmissionState> ReadAdmissionAsync(string role,string id,System.Text.Json.Nodes.JsonObject request,CancellationToken token)=>inner.ReadAdmissionAsync(role,id,request,token);
        public Task<System.Text.Json.Nodes.JsonObject?> LookupResultAsync(string role,string id,CancellationToken token)=>inner.LookupResultAsync(role,id,token);
        public async Task RetainAsync(string role,string id,System.Text.Json.Nodes.JsonObject result,CancellationToken token)
        {
            await inner.RetainAsync(role,id,result,token);
            if(id.EndsWith(":envelope-evaluation-result",StringComparison.Ordinal))
            { Attempted=(System.Text.Json.Nodes.JsonObject)result.DeepClone();if(lose && !_lost) { _lost=true;throw new IOException("fixture lost retained evaluation acknowledgement"); } }
        }
    }
    private sealed class CompletionArchive:IIndependentContentArchive
    {
        private readonly Dictionary<string,byte[]> _objects=[];
        public Task<string> PublishAsync(ReadOnlyMemory<byte> bytes,CancellationToken token) { string hash=Convert.ToHexString(SHA256.HashData(bytes.Span));_objects.TryAdd(hash,bytes.ToArray());return Task.FromResult(hash); }
        public Task<byte[]> ReadAsync(string hash,CancellationToken token)=>Task.FromResult(_objects[hash].ToArray());
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly IEventStoreGatewayClient Gateway = Substitute.For<IEventStoreGatewayClient>();
        internal readonly IEventStoreGatewayBoundCommandProofSource Proofs = Substitute.For<IEventStoreGatewayBoundCommandProofSource,IPrivateRecoveryCommandProofSource,IPrivateReplayRecoveryCommandProofSource>();
        internal readonly List<BoundWorkloadCommandRequest> Commands = [];
        private readonly Dictionary<string, List<StreamReadEvent>> _sources = [];
        private readonly OriginalTransport _originals = new();
        internal readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
        internal bool LoseAcknowledgement, ProofDenied, MissingNativeProof,QueueWithoutEvent,RejectOrdinary,QueueRecovery,RejectReplayOrdinary,QueueReplayRecovery;
        private readonly Dictionary<string,SubmitCommandResponse> _terminalCache=[];
        private readonly Dictionary<string,CommandStatus> _statuses=[];
        internal CommandStatus? OriginalStatus;
        internal bool? Retryable=false;
        internal readonly IReplicatedSecuritySpoolAuthority RecoveryAuthority=Substitute.For<IReplicatedSecuritySpoolAuthority>();
        internal EventStorePrivateRecordingPort Port { get; }
        internal SecurityObservationRecord Record => new(new("security-observation-" + new string('a', 64), "tenant-a", "invalid-tag", new string('B', 64), "v1"), Now, Now.ToString("yyyy-MM-dd"), 1, null,2);
        internal Fixture(IndependentDaprAuthority? sourceAuthority=null,RSA? proofKey=null)
        {
            Proofs.IssueAsync(Arg.Any<BoundWorkloadCommandRequest>(), Arg.Any<CancellationToken>()).Returns(call => ValueTask.FromResult<string?>(ProofDenied ? null : proofKey is null?"isolated-exact-scope-proof":BoundWorkloadCommandProof.Issue(call.Arg<BoundWorkloadCommandRequest>(),"recording-owner",Convert.ToBase64String(proofKey.ExportPkcs8PrivateKey()),DateTimeOffset.UtcNow)));
            ((IPrivateRecoveryCommandProofSource)Proofs).IssueRecoveryAsync(Arg.Any<BoundWorkloadCommandRequest>(),Arg.Any<SecurityObservationRecoveryGrant>(),Arg.Any<CancellationToken>()).Returns(call=>ValueTask.FromResult<string?>(ProofDenied?null:proofKey is null?"isolated-current-recovery-proof":BoundWorkloadCommandProof.Issue(call.Arg<BoundWorkloadCommandRequest>(),"recording-owner",Convert.ToBase64String(proofKey.ExportPkcs8PrivateKey()),DateTimeOffset.UtcNow)));
            ((IPrivateReplayRecoveryCommandProofSource)Proofs).IssueReplayRecoveryAsync(Arg.Any<BoundWorkloadCommandRequest>(),Arg.Any<PrivateReplayRecoveryProof>(),Arg.Any<CancellationToken>()).Returns(call=>ValueTask.FromResult<string?>(ProofDenied?null:proofKey is null?"isolated-current-replay-recovery-proof":BoundWorkloadCommandProof.Issue(call.Arg<BoundWorkloadCommandRequest>(),"recording-owner",Convert.ToBase64String(proofKey.ExportPkcs8PrivateKey()),DateTimeOffset.UtcNow)));
            Gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call => Task.FromResult(Read(call.ArgAt<StreamReadRequest>(0))));
            Gateway.SubmitBoundWorkloadCommandAsync(Arg.Any<BoundWorkloadCommandRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var request = call.ArgAt<BoundWorkloadCommandRequest>(0); Commands.Add(request); var command = request.Command;
                var identity = new AggregateIdentity(command.Tenant, command.Domain, command.AggregateId);
                if(_terminalCache.TryGetValue(command.MessageId,out var cached)) return Task.FromResult(cached);
                string replayOriginal=command.Domain=="trusted-envelope-replay"?PrivateRecordingIdentity.StableUlid("replay",identity.ActorId):string.Empty;
                if(RejectReplayOrdinary && command.Domain=="trusted-envelope-replay" && command.MessageId==replayOriginal)
                { var rejected=new SubmitCommandResponse(command.MessageId) { MessageId=command.MessageId };_statuses[command.MessageId]=CommandStatus.Rejected;_terminalCache.Add(command.MessageId,rejected);return Task.FromResult(rejected); }
                if(QueueReplayRecovery && command.Domain=="trusted-envelope-replay" && command.MessageId!=replayOriginal)
                { _statuses[command.MessageId]=CommandStatus.Processing;return Task.FromResult(new SubmitCommandResponse(command.MessageId) { MessageId=command.MessageId }); }
                if(RejectOrdinary && command.Domain=="security-event-log" && command.MessageId==PrivateRecordingIdentity.SecurityOriginalMessageId(command.Payload.GetProperty("securityObservationId").GetString()!))
                { var rejected=new SubmitCommandResponse(command.MessageId) { MessageId=command.MessageId };_statuses[command.MessageId]=CommandStatus.Rejected;_terminalCache.Add(command.MessageId,rejected);return Task.FromResult(rejected); }
                if(QueueWithoutEvent || QueueRecovery && command.Domain=="security-event-log" && command.MessageId!=PrivateRecordingIdentity.SecurityOriginalMessageId(command.Payload.GetProperty("securityObservationId").GetString()!))
                { _statuses[command.MessageId]=OriginalStatus??CommandStatus.Processing;return Task.FromResult(new SubmitCommandResponse(command.MessageId) { MessageId=command.MessageId }); }
                if (!_sources.TryGetValue(identity.ActorId, out var events)) _sources.Add(identity.ActorId, events = []);
                string eventType = command.Domain == "trusted-envelope-replay" ? "Hexalith.Agents.Contracts.TrustedEnvelopeReplay.TrustedEnvelopeFirstSeen" : "Hexalith.Agents.Contracts.SecurityEventLog.SecurityEventRecorded";
                object payload = command.Domain == "trusted-envelope-replay" ? new { Registration = command.Payload } : new { Observation = command.Payload };
                var item = new StreamReadEvent(events.Count + 1, eventType, JsonSerializer.SerializeToUtf8Bytes(payload), "json", 1,
                    command.MessageId, command.MessageId, command.MessageId, Now, "private-worker") { NativeEventDigest = new string('a', 64) };
                events.Add(item);
                if (LoseAcknowledgement) throw new IOException("isolated lost source acknowledgement");
                return Task.FromResult(new SubmitCommandResponse(command.MessageId) { MessageId = command.MessageId });
            });
            Gateway.GetWorkloadCommandStatusAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(call=>
            {
                var command=Commands.Last(value=>value.Command.MessageId==call.ArgAt<string>(1)).Command;var status=_statuses.GetValueOrDefault(command.MessageId,OriginalStatus??CommandStatus.Processing);
                return new CommandStatusQueryResponse(command.CorrelationId??command.MessageId,status.ToString(),(int)status,MessageId:command.MessageId) { TenantId=command.Tenant,Domain=command.Domain,AggregateId=command.AggregateId,CommittedEventSequence=0,EventCount=0,Retryable=status is CommandStatus.Received or CommandStatus.Processing?true:Retryable };
            });
            Port = new(Gateway, Proofs, sourceAuthority??_originals.Authority, RecoveryAuthority, "private-worker", TimeProvider.System);
        }
        internal EventStorePrivateRecordingPort ReopenPort()=>new(Gateway,Proofs,_originals.Authority,RecoveryAuthority,"private-worker",TimeProvider.System);
        internal void AddSecurityTail(string kind)
        {
            var events=_sources.Values.Single();var first=events[0];var body=System.Text.Json.Nodes.JsonNode.Parse(first.Payload)!.AsObject();
            if(kind=="conflicting") body["Observation"]!["untrustedFieldsHmac"]=new string('C',64);
            if(kind=="unrelated") body["Observation"]!["securityObservationId"]="security-observation-"+new string('c',64);
            byte[] bytes=kind=="malformed"?"{}"u8.ToArray():JsonSerializer.SerializeToUtf8Bytes(body);
            events.Add(first with { SequenceNumber=2,MessageId=PrivateRecordingIdentity.StableUlid("fixture-tail",kind),EventTypeName=kind=="unsupported"?"Hexalith.Agents.Contracts.SecurityEventLog.UnexpectedEvent":first.EventTypeName,Payload=bytes,NativeEventDigest=new string('b',64) });
        }
        private StreamReadPage Read(StreamReadRequest request)
        {
            var identity = new AggregateIdentity(request.Tenant, request.Domain, request.AggregateId!);
            var events = _sources.GetValueOrDefault(identity.ActorId) ?? [];
            NativeSourceProof proof = NativeSourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
            foreach (var item in events) proof = NativeSourceProofCodec.ExtendDigest(proof, identity, item.SequenceNumber, item.MessageId, item.NativeEventDigest!);
            var selected = events.Where(e => e.SequenceNumber > request.FromSequence && (request.ToSequence is null || e.SequenceNumber <= request.ToSequence)).ToArray();
            return new(request.Tenant, request.Domain, request.AggregateId, selected, new(request.FromSequence, request.ToSequence,
                selected.Length == 0 ? null : selected[^1].SequenceNumber, events.Count, selected.Length, false, null) { NativeProof = MissingNativeProof ? null : proof });
        }
        public void Dispose() => _originals.Dispose();
    }
    private sealed class OriginalTransport : ISecretsCredentialBoundDaprInvoker, IDisposable
    {
        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly Dictionary<string, IndependentOriginal> _originals = [];
        internal IndependentDaprAuthority Authority => new(this, new("original-owner", "file:/isolated", _key.ExportSubjectPublicKeyInfoPem(), "enrollment", "originals", "trust-1"), TimeProvider.System);
        public Task<string?> InvokeAsync(string appId, string method, string jsonBody, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<string?> InvokeCredentialBoundAsync(string appId, string credentialReference, string method, string jsonBody, CancellationToken cancellationToken)
        {
            using var document = JsonDocument.Parse(jsonBody); var request = document.RootElement; var payload = request.GetProperty("Payload");
            string id = payload.TryGetProperty("requestId",out var requestId)?requestId.GetString()!:payload.GetProperty("command").GetProperty("MessageId").GetString()!;
            object? result;
            if(method.EndsWith("recording-replay-recovery-dispatch-admit",StringComparison.Ordinal))
            {
                var command=payload.GetProperty("command");var proof=payload.GetProperty("proof").Deserialize<PrivateReplayRecoveryProof>(CustodyBindingContract.Json)!;
                bool allowed=id==PrivateRecordingIdentity.ReplayRecoveryMessageId(proof) && _originals.TryGetValue(id,out var attempt) && attempt.SubmissionState=="Unsent"
                    && attempt.Request.GetRawText()==command.GetProperty("Payload").GetRawText() && _originals.TryGetValue(proof.OriginalRequestId,out var main) && (main.ActiveTransportMessageId??proof.OriginalRequestId)==proof.PreviousTransportMessageId;
                if(allowed) { _originals[id]=_originals[id] with { SubmissionState="Dispatched" };_originals[proof.OriginalRequestId]=_originals[proof.OriginalRequestId] with { ActiveTransportMessageId=id }; }
                result=new { Allowed=allowed };
            }
            else if(method.EndsWith("recording-recovery-dispatch-admit",StringComparison.Ordinal))
            {
                var command=payload.GetProperty("command");var grant=payload.GetProperty("grant").Deserialize<SecurityObservationRecoveryGrant>(CustodyBindingContract.Json)!;string mainId=PrivateRecordingIdentity.SecurityOriginalMessageId(grant.Original.Intent.ObservationId);
                bool allowed=id==PrivateRecordingIdentity.SecurityRecoveryMessageId(grant) && _originals.TryGetValue(id,out var attempt) && attempt.SubmissionState=="Unsent" && attempt.Request.GetRawText()==command.GetProperty("Payload").GetRawText();
                if(allowed) { _originals[id]=_originals[id] with { SubmissionState="Dispatched" };var main=_originals.GetValueOrDefault(mainId)??new IndependentOriginal(command.GetProperty("Payload").Clone(),null);_originals[mainId]=main with { SubmissionState="Dispatched",ActiveTransportMessageId=id }; }
                result=new { Allowed=allowed };
            }
            else if(method.EndsWith("qualification-retain-result",StringComparison.Ordinal))
            {
                var body=payload.GetProperty("result").Clone();bool exact=_originals.TryGetValue(id,out var stored) && (stored.Result is null || stored.Result.Value.GetRawText()==body.GetRawText());
                if(exact) _originals[id]=stored! with { Result=body };result=new { Exact=exact,First=exact };
            }
            else if (method.EndsWith("qualification-lookup-original",StringComparison.Ordinal)) result = _originals.GetValueOrDefault(id);
            else if (method.EndsWith("recording-dispatch-admit",StringComparison.Ordinal))
            {
                bool allowed = _originals.TryGetValue(id,out var stored) && stored.Request.GetRawText() == payload.GetProperty("original").GetRawText() && stored.SubmissionState == "Unsent";
                if (allowed) _originals[id] = stored! with { SubmissionState="Dispatched" };
                result = new { Allowed=allowed };
            }
            else
            {
                var original = payload.GetProperty("original").Clone();
                bool first = !_originals.TryGetValue(id,out var existing);
                if (first) _originals.Add(id,new(original,null));
                result = new { Exact=first || existing!.Request.GetRawText()==original.GetRawText(),First=first };
            }
            var outcome = JsonSerializer.SerializeToElement(result);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var response = new Response("enrollment", "originals", "trust-1", request.GetProperty("Method").GetString()!, request.GetProperty("Nonce").GetString()!,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(jsonBody))), now, now + 30, outcome, []);
            response = response with { Signature = _key.SignData(JsonSerializer.SerializeToUtf8Bytes(response), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence) };
            return Task.FromResult<string?>(JsonSerializer.Serialize(response));
        }
        public void Dispose() => _key.Dispose();
        private sealed record Response(string EnrollmentId, string Scope, string TrustRevision, string Method, string Nonce, string RequestSha256,
            long ObservedAt, long ValidUntil, JsonElement Payload, byte[] Signature);
    }
}
