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

    private sealed class Fixture : IDisposable
    {
        internal readonly IEventStoreGatewayClient Gateway = Substitute.For<IEventStoreGatewayClient>();
        internal readonly IEventStoreGatewayBoundCommandProofSource Proofs = Substitute.For<IEventStoreGatewayBoundCommandProofSource>();
        internal readonly List<BoundWorkloadCommandRequest> Commands = [];
        private readonly Dictionary<string, List<StreamReadEvent>> _sources = [];
        private readonly OriginalTransport _originals = new();
        internal readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
        internal bool LoseAcknowledgement, ProofDenied, MissingNativeProof,QueueWithoutEvent;
        internal CommandStatus? OriginalStatus;
        internal bool? Retryable=false;
        internal readonly IReplicatedSecuritySpoolAuthority RecoveryAuthority=Substitute.For<IReplicatedSecuritySpoolAuthority>();
        internal EventStorePrivateRecordingPort Port { get; }
        internal SecurityObservationRecord Record => new(new("security-observation-" + new string('a', 64), "tenant-a", "invalid-tag", new string('B', 64), "v1"), Now, Now.ToString("yyyy-MM-dd"), 1, null);
        internal Fixture()
        {
            Proofs.IssueAsync(Arg.Any<BoundWorkloadCommandRequest>(), Arg.Any<CancellationToken>()).Returns(_ => ValueTask.FromResult<string?>(ProofDenied ? null : "isolated-exact-scope-proof"));
            Gateway.ReadStreamAsync(Arg.Any<StreamReadRequest>(), Arg.Any<CancellationToken>()).Returns(call => Task.FromResult(Read(call.ArgAt<StreamReadRequest>(0))));
            Gateway.SubmitBoundWorkloadCommandAsync(Arg.Any<BoundWorkloadCommandRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var request = call.ArgAt<BoundWorkloadCommandRequest>(0); Commands.Add(request); var command = request.Command;
                var identity = new AggregateIdentity(command.Tenant, command.Domain, command.AggregateId);
                if(QueueWithoutEvent) return Task.FromResult(new SubmitCommandResponse(command.MessageId) { MessageId=command.MessageId });
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
                var command=Commands.Single(value=>value.Command.MessageId==call.ArgAt<string>(1)).Command;var status=OriginalStatus??CommandStatus.Processing;
                return new CommandStatusQueryResponse(command.CorrelationId??command.MessageId,status.ToString(),(int)status,MessageId:command.MessageId) { TenantId=command.Tenant,Domain=command.Domain,AggregateId=command.AggregateId,CommittedEventSequence=0,EventCount=0,Retryable=OriginalStatus is null?true:Retryable };
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
            string id = payload.GetProperty("requestId").GetString()!;
            object? result;
            if (method.EndsWith("qualification-lookup-original",StringComparison.Ordinal)) result = _originals.GetValueOrDefault(id);
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
