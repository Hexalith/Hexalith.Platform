using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Dapr.Client;

using Google.Protobuf;

using Grpc.Net.Client;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Secrets.Runtime;
using Hexalith.Platform.Custody;

using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class CustodyBindingDaprInvokerTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task PinUnpinAndLookupUseExactOriginalPayloadWithCompatibleUnverifiedResult(bool unpin, bool lookup)
    {
        var request = CustodyBindingFixture.Lifecycle(unpin, lookup);
        var handler = new CaptureHandler { ResponseChange = (_, response) => response with { Lifecycle = response.Lifecycle! with
            { Status = unpin ? CustodyKeyLifecycleStatus.Unpinned : CustodyKeyLifecycleStatus.Pinned, ReceiptId = "original-receipt" } } };
        using DaprClient client = Client(handler); var authority = new CustodyBindingFixture.Authority();
        var result = await Invoker(client, authority).InvokeAsync(request, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.State.ShouldBe(CustodyBindingTransportState.ReceivedUnverified);
        handler.Operation.ShouldBe(lookup ? "lookup-original" : unpin ? "unpin" : "pin");
        CustodyBindingContract.Decode(handler.Data)!.Lifecycle.ShouldBe(request.Lifecycle);
        authority.Methods.Single().ShouldBe("custody-lifecycle/" + handler.Operation);
        var changed = request with { Lifecycle = request.Lifecycle! with { OperationId = "replacement" } };
        (await Invoker(client, authority).InvokeAsync(changed, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).State.ShouldBe(CustodyBindingTransportState.Refused);
        handler.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData("wrong-action")]
    [InlineData("destroyed")]
    [InlineData("missing-receipt")]
    [InlineData("unknown-receipt")]
    [InlineData("unexpected-restore")]
    [InlineData("lookup-action")]
    public async Task LifecycleResultsRejectIncompatibleStatusAndReceiptShapes(string fault)
    {
        var request = CustodyBindingFixture.Lifecycle(unpin: fault == "lookup-action", lookup: fault == "lookup-action");
        var handler = new CaptureHandler { ResponseChange = (_, response) => response with { Lifecycle = response.Lifecycle! with
        {
            Status = fault switch { "destroyed" => CustodyKeyLifecycleStatus.Destroyed, "wrong-action" => CustodyKeyLifecycleStatus.Unpinned,
                "unknown-receipt" => CustodyKeyLifecycleStatus.Unknown, _ => CustodyKeyLifecycleStatus.Pinned },
            ReceiptId = fault == "missing-receipt" ? null : "receipt",
            RestoreBarrierReceiptId = fault is "destroyed" or "unexpected-restore" ? "restore" : null,
        } } };
        using DaprClient client = Client(handler);
        (await Invoker(client, new CustodyBindingFixture.Authority()).InvokeAsync(request, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))
            .State.ShouldBe(CustodyBindingTransportState.Unknown);
    }

    [Theory]
    [InlineData("NotStarted")]
    [InlineData("Unknown")]
    [InlineData("Consumed")]
    [InlineData("vector")]
    [InlineData("identity")]
    [InlineData("reservation")]
    [InlineData("manifest")]
    [InlineData("authority")]
    [InlineData("signature")]
    public async Task ManifestNonterminalStatusProofIsClosedExactAndAlwaysUnverified(string vector)
    {
        var request = CustodyBindingFixture.Manifest(); var m = request.Manifest!;
        var status = new CustodyBindingManifestStatusV1(new(request.Envelope.TenantId, m.Original.Capability.BatchId, m.ProtectionReservationReceiptId,
            vector == "Unknown" ? DeletionManifestProviderState.Unknown : vector == "Consumed" ? DeletionManifestProviderState.Consumed : DeletionManifestProviderState.NotStarted,
            vector == "vector" ? CustodyBindingFixture.Original(request).Result.TargetReceipts : []), request.Envelope.Enrollment, m.Original.Capability.ManifestDigest, [1]);
        status = vector switch
        {
            "identity" => status with { Result = status.Result with { TenantId = "other" } },
            "reservation" => status with { Result = status.Result with { ReservationReceiptId = "other" } },
            "manifest" => status with { ManifestDigest = new string('B', 64) },
            "authority" => status with { Authority = status.Authority with { AuthorityIncarnation = "other" } },
            "signature" => status with { Signature = [] }, _ => status,
        };
        var handler = new CaptureHandler { ResponseChange = (_, response) => response with { OriginalHeader = null, ManifestStatus = status } };
        using DaprClient client = Client(handler);
        var result = await Invoker(client, new CustodyBindingFixture.Authority()).InvokeAsync(request, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.State.ShouldBe(vector is "NotStarted" or "Unknown" ? CustodyBindingTransportState.ReceivedUnverified : CustodyBindingTransportState.Unknown);
    }

    [Theory]
    [InlineData("pre-cancelled")]
    [InlineData("authorization-completed")]
    [InlineData("sdk-return")]
    public async Task CancellationAtCompletedSendBoundariesCannotExposeAResult(string fault)
    {
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var authority = new CustodyBindingFixture.Authority();
        var handler = new CaptureHandler();
        if (fault == "pre-cancelled") { cancelled.Cancel(); }
        if (fault == "authorization-completed") { authority.OnAuthorize = cancelled.Cancel; }
        if (fault == "sdk-return") { handler.BeforeReturn = cancelled.Cancel; }
        using DaprClient client = Client(handler);
        var result = await Invoker(client, authority).InvokeAsync(CustodyBindingFixture.Manifest(), TimeSpan.FromSeconds(5), cancelled.Token);
        result.State.ShouldBe(CustodyBindingTransportState.Unknown); result.Response.ShouldBeNull();
        handler.Calls.ShouldBe(fault == "sdk-return" ? 1 : 0);
    }

    [Fact]
    public async Task PageSourceChecksCancellationBeforeFactoryAndAfterCompletedFactory()
    {
        var original = CustodyBindingFixture.Manifest(); var (header, _) = CustodyBindingFixture.Header(original);
        var read = new CustodyBindingPageReadV1(header, 0, Math.Min(header.TotalBytes, CustodyBindingContract.MaximumPageBytes));
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var handler = new CaptureHandler(); using DaprClient client = Client(handler); int factories = 0;
        var source = new CustodyBindingDaprPageSource(Invoker(client, new CustodyBindingFixture.Authority()), (_, _) =>
        {
            factories++; cancelled.Cancel();
            var e = original.Envelope with { Operation = "read-original-page", Proof = original.Envelope.Proof with { Action = "read-original-page" } };
            return Task.FromResult<CustodyBindingRequestV1?>(new(e, Page: read));
        }, TimeSpan.FromSeconds(5));
        (await source.ReadAsync(read, cancelled.Token)).ShouldBeNull(); factories.ShouldBe(1); handler.Calls.ShouldBe(0);
        (await source.ReadAsync(read, cancelled.Token)).ShouldBeNull(); factories.ShouldBe(1);
    }

    [Fact]
    public async Task ActualPinnedSdkSerializesRawBytesOnceWithoutJsonByteArrayBase64Wrapping()
    {
        var request = CustodyBindingFixture.Manifest();
        var handler = new CaptureHandler();
        using DaprClient client = Client(handler);
        var invoker = Invoker(client, new CustodyBindingFixture.Authority());
        var result = await invoker.InvokeAsync(request, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.State.ShouldBe(CustodyBindingTransportState.ReceivedUnverified);
        handler.Calls.ShouldBe(1);
        handler.Name.ShouldBe("synthetic-configured-role"); handler.Operation.ShouldBe("destroy-manifest");
        handler.Data.ShouldBe(CustodyBindingContract.Encode(request));
        handler.ProtobufBytes.ShouldBe(CustodyBindingDaprInvoker.ApiRequestSize(handler.Name, handler.Operation, handler.Data.Length));
    }

    [Theory]
    [InlineData("lost-result")]
    [InlineData("cancellation")]
    [InlineData("oversized-response")]
    [InlineData("malformed")]
    [InlineData("identity")]
    [InlineData("key-response")]
    [InlineData("mixed-response")]
    [InlineData("header-substitution")]
    public async Task LostCancelledOrAmbiguousSdkResultIsUnknownWithOneAttempt(string fault)
    {
        var handler = new CaptureHandler { Fault = fault };
        using DaprClient client = Client(handler);
        var request = CustodyBindingFixture.Manifest();
        var result = await Invoker(client, new CustodyBindingFixture.Authority()).InvokeAsync(request,
            fault == "cancellation" ? TimeSpan.FromMilliseconds(50) : TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.State.ShouldBe(CustodyBindingTransportState.Unknown);
        result.Response.ShouldBeNull(); handler.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task MissingCurrentAuthorityUnsupportedKeyDeliveryAndOversizeRefuseBeforeSdkSend()
    {
        var handler = new CaptureHandler();
        using DaprClient client = Client(handler);
        var request = CustodyBindingFixture.Manifest();
        var denied = Invoker(client, new DenyAuthority());
        (await denied.InvokeAsync(request, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).State.ShouldBe(CustodyBindingTransportState.Unknown);
        handler.Calls.ShouldBe(0);
        var invoker = Invoker(client, new CustodyBindingFixture.Authority());
        var keyUse = new CustodyBindingKeyUseV1("lease", "delivery", "recipient", "transport", "opaque-wrap");
        var unwrap = CustodyBindingFixture.Bind(new(CustodyBindingFixture.Envelope("unwrap"), CustodyBindingFixture.Registration, KeyUse: keyUse));
        CustodyBindingContract.Validate(unwrap).ShouldBeTrue();
        (await invoker.InvokeAsync(unwrap, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).State.ShouldBe(CustodyBindingTransportState.Refused);
        var wrapEnvelope = CustodyBindingFixture.Envelope("wrap") with { OperationId = CustodyBindingFixture.Registration.OperationId };
        var wrap = CustodyBindingFixture.Bind(new(wrapEnvelope, CustodyBindingFixture.Registration));
        (await invoker.InvokeAsync(wrap, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).State.ShouldBe(CustodyBindingTransportState.Refused);
        var big = CustodyBindingFixture.Manifest(1000, new string('x', 2048), maximal: true);
        (await invoker.InvokeAsync(big, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).State.ShouldBe(CustodyBindingTransportState.Refused);
        (await invoker.InvokeAsync(request, TimeSpan.FromSeconds(31), TestContext.Current.CancellationToken)).State.ShouldBe(CustodyBindingTransportState.Refused);
        (await new CustodyBindingDaprInvoker(client, new Dictionary<string, string>(), new CustodyBindingFixture.Authority())
            .InvokeAsync(request, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).State.ShouldBe(CustodyBindingTransportState.Refused);
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task DistinctAnchorPhasesBindExactBytesAndRequireSeparateRecoveryAuthority()
    {
        byte[] target = [1, 2, 3];
        var transition = new AnchoredStateTransition("owner|purpose", 1, 2, CustodyBindingFixture.Digest, CustodyBindingContract.Hash(target), target);
        var handler = new CaptureHandler();
        var authority = new CustodyBindingFixture.Authority();
        using DaprClient client = Client(handler);
        var invoker = Invoker(client, authority);
        foreach (string phase in new[] { "admit-transition", "record-transition", "recover-transition", "verify-transition", "lookup-transition" })
        {
            var candidate = CustodyBindingFixture.Bind(new(CustodyBindingFixture.Envelope(phase, target: transition.TargetDigest), Transition: transition));
            var request = CustodyBindingTransitionRequests.Create(candidate.Envelope, transition, phase);
            (await invoker.InvokeAsync(request, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).State.ShouldBe(CustodyBindingTransportState.ReceivedUnverified);
            handler.Operation.ShouldBe(phase);
            authority.Methods.Last().ShouldBe("custody-lifecycle/" + phase);
            CustodyBindingContract.Decode(handler.Data)!.Transition!.TargetBytes.ShouldBe(target);
            CustodyBindingContract.Validate(CustodyBindingFixture.Bind(request with { Transition = transition with { ScopeId = "other|purpose" } })).ShouldBeFalse();
            CustodyBindingContract.Validate(CustodyBindingFixture.Bind(request with { Transition = transition with { TargetBytes = [4, 5, 6] } })).ShouldBeFalse();
        }
        int calls = handler.Calls;
        authority.RecoveryAllowed = false;
        var recovery = CustodyBindingFixture.Bind(new(CustodyBindingFixture.Envelope("recover-transition", target: transition.TargetDigest), Transition: transition));
        (await invoker.InvokeAsync(recovery, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).State.ShouldBe(CustodyBindingTransportState.Unknown);
        handler.Calls.ShouldBe(calls);
        Should.Throw<ArgumentException>(() => CustodyBindingTransitionRequests.Create(recovery.Envelope, transition, "record-transition"));
    }

    [Fact]
    public async Task ReadCurrentMakesFreshCallsAndRejectsStaleAttemptChallenge()
    {
        var request = CustodyBindingFixture.Bind(new(CustodyBindingFixture.Envelope("read-current")));
        var handler = new CaptureHandler();
        using DaprClient client = Client(handler);
        var authority = new CustodyBindingFixture.Authority();
        var invoker = Invoker(client, authority);
        (await invoker.InvokeAsync(request, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).State.ShouldBe(CustodyBindingTransportState.ReceivedUnverified);
        var next = request with { Envelope = request.Envelope with { RequestAttemptId = "attempt-2", Challenge = "challenge-2" } };
        (await invoker.InvokeAsync(next, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).State.ShouldBe(CustodyBindingTransportState.ReceivedUnverified);
        handler.Calls.ShouldBe(2); authority.Methods.Count.ShouldBe(2);
        handler.Fault = "stale-current";
        (await invoker.InvokeAsync(next, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).State.ShouldBe(CustodyBindingTransportState.Unknown);
        handler.Calls.ShouldBe(3);
    }

    [Fact]
    public async Task OriginalPagesUseSameRawSdkFramingWithFreshExactLookupAuthorization()
    {
        var original = CustodyBindingFixture.Manifest(1000);
        var (header, bytes) = CustodyBindingFixture.Header(original, CustodyBindingFixture.Original(original, 400));
        var handler = new CaptureHandler { PageBytes = bytes };
        using DaprClient client = Client(handler);
        var authority = new CustodyBindingFixture.Authority();
        var source = new CustodyBindingDaprPageSource(Invoker(client, authority), (read, ct) =>
        {
            var e = original.Envelope with { Operation = "read-original-page", RequestAttemptId = "page-" + read.Offset,
                Challenge = "page-challenge-" + read.Offset, Proof = original.Envelope.Proof with { Action = "read-original-page" } };
            // Synthetic authority issues this exact lookup proof; no production proof conversion is supplied.
            return Task.FromResult<CustodyBindingRequestV1?>(new(e, Page: read));
        }, TimeSpan.FromSeconds(5));
        var result = await CustodyBindingOriginalAssembler.AssembleAsync(original, header, source, authority, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.State.ShouldBe(DeletionManifestProviderState.Consumed); result.ExactOriginalBytes.ShouldBe(bytes);
        handler.Calls.ShouldBeGreaterThan(1);
        authority.Methods.ShouldAllBe(m => m == "custody-lifecycle/read-original-page");
        handler.Fault = "padded-page";
        result = await CustodyBindingOriginalAssembler.AssembleAsync(original, header, source, authority, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        result.State.ShouldBe(DeletionManifestProviderState.Unknown); result.ExactOriginalBytes.ShouldBeNull();
    }

    [Theory]
    [InlineData("operation")]
    [InlineData("lease")]
    [InlineData("recipient")]
    [InlineData("transport")]
    [InlineData("proof")]
    public async Task ExactPrivateAuthenticatorDeniesSubstitutionDespiteStableOriginalDigest(string substitution)
    {
        var use = new CustodyBindingKeyUseV1("lease", "delivery", "recipient", "transport", "opaque-wrap");
        var request = CustodyBindingFixture.Bind(new(CustodyBindingFixture.Envelope("complete-key-use"), CustodyBindingFixture.Registration, KeyUse: use));
        var exactAuthority = new ExactAuthority(CustodyBindingContract.Encode(request));
        var changed = substitution switch
        {
            "operation" => request with { Envelope = request.Envelope with { OperationId = "substituted" } },
            "lease" => request with { KeyUse = use with { KeyUseLeaseId = "substituted" } },
            "recipient" => request with { KeyUse = use with { RecipientProcessIncarnation = "substituted" } },
            "transport" => request with { KeyUse = use with { TransportIncarnation = "substituted" } },
            _ => request with { Envelope = request.Envelope with { Proof = request.Envelope.Proof with { Signature = [4, 5, 6] } } },
        };
        CustodyBindingContract.Validate(changed).ShouldBeTrue();
        changed.Envelope.RequestDigest.ShouldBe(request.Envelope.RequestDigest);
        using DaprClient client = Client(new CaptureHandler());
        (await Invoker(client, exactAuthority).InvokeAsync(changed, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).State.ShouldBe(CustodyBindingTransportState.Unknown);
    }

    private sealed class ExactAuthority(byte[] enrolledExactBytes) : ICustodyBindingCurrentAuthority
    {
        public Task<bool> AuthorizeAsync(CustodyBindingRequestV1 exact, string method, CancellationToken cancellationToken)
            => Task.FromResult(method == "custody-lifecycle/complete-key-use" && CustodyBindingContract.Encode(exact).AsSpan().SequenceEqual(enrolledExactBytes));
    }
    private sealed class DenyAuthority : ICustodyBindingCurrentAuthority;

    internal static DaprClient Client(HttpMessageHandler handler) => new DaprClientBuilder().UseGrpcEndpoint("http://127.0.0.1:1")
        .UseGrpcChannelOptions(new GrpcChannelOptions { HttpHandler = handler }).Build();
    private static CustodyBindingDaprInvoker Invoker(DaprClient client, ICustodyBindingCurrentAuthority authority)
        => new(client, new Dictionary<string, string> { [CustodyBindingContract.LogicalRole] = "synthetic-configured-role" }, authority);

    internal sealed class CaptureHandler : HttpMessageHandler
    {
        internal int Calls { get; private set; }
        internal string Name { get; private set; } = "";
        internal string Operation { get; private set; } = "";
        internal byte[] Data { get; private set; } = [];
        internal int ProtobufBytes { get; private set; }
        internal string? Fault { get; set; }
        internal byte[]? PageBytes { get; set; }
        internal Action? BeforeReturn { get; set; }
        internal Func<CustodyBindingRequestV1, CustodyBindingResponseV1, CustodyBindingResponseV1>? ResponseChange { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Fault == "cancellation") { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            if (Fault == "lost-result") { throw new HttpRequestException("Synthetic lost result."); }
            byte[] frame = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            frame[0].ShouldBe((byte)0);
            ProtobufBytes = BinaryPrimitives.ReadInt32BigEndian(frame.AsSpan(1, 4));
            ProtobufBytes.ShouldBe(frame.Length - 5);
            using var input = new CodedInputStream(frame, 5, frame.Length - 5);
            uint tag;
            while ((tag = input.ReadTag()) != 0)
            {
                switch (WireFormat.GetTagFieldNumber(tag))
                {
                    case 1: Name = input.ReadString(); break;
                    case 2: Data = input.ReadBytes().ToByteArray(); break;
                    case 4: Operation = input.ReadString(); break;
                    default: input.SkipLastField(); break;
                }
            }
            var decoded = CustodyBindingContract.Decode(Data)!;
            var envelope = decoded.Envelope;
            var current = Operation == "read-current" ? new CustodyBindingCurrentReadV1(envelope.RequestAttemptId,
                Fault == "stale-current" ? "old-challenge" : envelope.Challenge, envelope.Enrollment, 1, envelope.CheckpointDigest, [1]) : null;
            CustodyBindingOriginalPageV1? page = null;
            if (decoded.Page is { } read && PageBytes is not null)
            {
                byte[] part = PageBytes.AsSpan(read.Offset, read.Length).ToArray();
                page = new(read.Header, read.Offset, read.Length, CustodyBindingContract.Hash(part), part);
            }
            var header = decoded.Manifest is not null ? CustodyBindingFixture.Header(decoded).Header : null;
            if (Fault == "header-substitution" && header is not null) { header = header with { ReservationReceiptId = "other" }; }
            var lifecycle = decoded.Registration is null ? null : new Hexalith.Platform.Custody.CustodyKeyLifecycleOutcome(decoded.Registration.Identity,
                envelope.OperationId, envelope.RequestDigest, Hexalith.Platform.Custody.CustodyKeyLifecycleStatus.Unknown);
            var responseObject = new CustodyBindingResponseV1(CustodyBindingContract.Protocol,
                Fault == "identity" ? "other" : envelope.OperationId, envelope.RequestDigest, OriginalHeader: header, Lifecycle: lifecycle,
                Current: current, Page: page, TransitionVerified: decoded.Transition is not null || Fault == "mixed-response" ? true : null);
            byte[] response = JsonSerializer.SerializeToUtf8Bytes(ResponseChange?.Invoke(decoded, responseObject) ?? responseObject, CustodyBindingContract.Json);
            if (Fault == "oversized-response") { response = new byte[CustodyBindingContract.DefaultMessageBytes + 1]; }
            if (Fault == "malformed") { response = [0, 1]; }
            if (Fault == "key-response") { response = "{\"RawKey\":\"forbidden\"}"u8.ToArray(); }
            if (Fault == "padded-page") { response = Enumerable.Repeat((byte)' ', CustodyBindingContract.MaximumPageFrameBytes).Concat(response).ToArray(); }
            using var protobuf = new MemoryStream();
            using (var output = new CodedOutputStream(protobuf, leaveOpen: true))
            { output.WriteTag(1, WireFormat.WireType.LengthDelimited); output.WriteBytes(ByteString.CopyFrom(response)); }
            byte[] responseFrame = new byte[protobuf.Length + 5];
            BinaryPrimitives.WriteInt32BigEndian(responseFrame.AsSpan(1, 4), (int)protobuf.Length);
            protobuf.ToArray().CopyTo(responseFrame, 5);
            var message = new HttpResponseMessage(HttpStatusCode.OK) { Version = new(2, 0), Content = new ByteArrayContent(responseFrame) };
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc");
            message.TrailingHeaders.Add("grpc-status", "0");
            BeforeReturn?.Invoke();
            return message;
        }
    }
}
