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
        internal Fixture()
        {
            Proofs.IssueAsync(Arg.Any<BoundWorkloadCommandRequest>(), Arg.Any<CancellationToken>()).Returns(_ => ValueTask.FromResult<string?>(ProofDenied ? null : "isolated-exact-scope-proof"));
            ((IPrivateRecoveryCommandProofSource)Proofs).IssueRecoveryAsync(Arg.Any<BoundWorkloadCommandRequest>(),Arg.Any<SecurityObservationRecoveryGrant>(),Arg.Any<CancellationToken>()).Returns(_=>ValueTask.FromResult<string?>(ProofDenied?null:"isolated-current-recovery-proof"));
            ((IPrivateReplayRecoveryCommandProofSource)Proofs).IssueReplayRecoveryAsync(Arg.Any<BoundWorkloadCommandRequest>(),Arg.Any<PrivateReplayRecoveryProof>(),Arg.Any<CancellationToken>()).Returns(_=>ValueTask.FromResult<string?>(ProofDenied?null:"isolated-current-replay-recovery-proof"));
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
            Port = new(Gateway, Proofs, _originals.Authority, RecoveryAuthority, "private-worker", TimeProvider.System);
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
