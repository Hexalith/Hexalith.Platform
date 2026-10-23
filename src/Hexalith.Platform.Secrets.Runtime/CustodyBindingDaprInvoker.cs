using System.Text;
using System.Text.Json;
using System.Diagnostics;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Secrets.Runtime;

internal sealed record CustodyBindingCurrentReadV1(string RequestAttemptId, string Challenge, AuthorityEnrollmentV1 Authority,
    long ObservedRevision, string CheckpointDigest, byte[] Signature, long? OwnerRevision = null, string? OwnerDigest = null,
    string? TenantId = null, string? OwnerPurposeScope = null);
internal sealed record CustodyBindingTransitionProofV1(string RequestAttemptId, string Challenge, AuthorityEnrollmentV1 Authority,
    string Operation, string ScopeId, long OwnerRevision, string OwnerDigest, string TransitionDigest, string State, byte[] Signature);
internal sealed record CustodyBindingManifestStatusV1(DeletionManifestProviderResult Result, AuthorityEnrollmentV1 Authority,
    string ManifestDigest, byte[] Signature);

internal sealed record CustodyBindingResponseV1(string ProtocolVersion, string OperationId, string RequestDigest,
    CustodyBindingOriginalHeaderV1? OriginalHeader = null, CustodyKeyLifecycleOutcome? Lifecycle = null,
    CustodyBindingCurrentReadV1? Current = null, bool? TransitionVerified = null, CustodyBindingOriginalPageV1? Page = null,
    CustodyBindingManifestStatusV1? ManifestStatus = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] CustodyKeyRegistration? WrappedRegistration = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] CustodyBindingKeyDeliveryV1? KeyDelivery = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] CustodyBindingDeliveryV1? Delivery = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] CustodyBindingTransitionProofV1? TransitionProof = null);

internal sealed record CustodyBindingKeyDeliveryV1(CustodyBindingKeyUseV1 KeyUse, byte[] RootKey);

internal enum CustodyBindingTransportState { Unknown, Refused, ReceivedUnverified }
internal sealed record CustodyBindingTransportResult(CustodyBindingTransportState State, CustodyBindingResponseV1? Response = null);

// Installed implementation must verify the signed complete transport/holder profile and issue exact
// private-operation/control statements. Merely configuring a larger SDK channel grants no capability.
internal sealed record CustodyBindingTransportAuthorization(IReadOnlyDictionary<string, string> Metadata,
    int MaximumRequestMessageBytes, string TransportProfileDigest, string? KeyClosureProfileDigest);
internal interface ICustodyBindingTransportAuthority : ICustodyBindingCurrentAuthority
{
    Task<CustodyBindingTransportAuthorization?> PrepareTransportAsync(CustodyBindingRequestV1 exact, CancellationToken token);
}

/// <summary>Unbound one-attempt raw SDK binding call. Transport receipt never establishes physical success.</summary>
internal sealed class CustodyBindingDaprInvoker
{
    private readonly DaprClient _client;
    private readonly string? _binding;
    private readonly ICustodyBindingCurrentAuthority _authority;

    internal CustodyBindingDaprInvoker(DaprClient client, IReadOnlyDictionary<string, string> logicalBindings,
        ICustodyBindingCurrentAuthority authority)
    {
        _client = client;
        _authority = authority;
        // Resolve exactly once; no production DI registration or default physical binding is installed.
        _binding = logicalBindings.TryGetValue(CustodyBindingContract.LogicalRole, out string? name)
            && CustodyBindingContract.Text(name) ? name : null;
    }

    internal async Task<CustodyBindingTransportResult> InvokeAsync(CustodyBindingRequestV1 original, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (_binding is null || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30))
        { return new(CustodyBindingTransportState.Refused); }
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(timeout);
        long started = Stopwatch.GetTimestamp();
        void CheckBudget()
        {
            budget.Token.ThrowIfCancellationRequested();
            if (Stopwatch.GetElapsedTime(started) >= timeout) { throw new OperationCanceledException(budget.Token); }
        }
        byte[] data = [];
        CustodyBindingRequestV1? request = null;
        CustodyBindingResponseV1? response = null;
        BindingResponse? bindingResponse = null;
        bool transferred = false;
        try
        {
            CheckBudget();
            data = CustodyBindingContract.Encode(original);
            request = CustodyBindingContract.Decode(data);
            var transport = request is not null && _authority is ICustodyBindingTransportAuthority installed
                ? await installed.PrepareTransportAsync(request, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false) : null;
            int limit = transport?.MaximumRequestMessageBytes ?? CustodyBindingContract.DefaultMessageBytes;
            bool keys = request?.Envelope.Operation is "wrap" or "unwrap";
            if (request is null || limit is < CustodyBindingContract.DefaultMessageBytes or > CustodyBindingContract.CandidateRequestMessageBytes
                || transport is not null && (!CustodyBindingContract.Digest(transport.TransportProfileDigest)
                    || !transport.Metadata.ContainsKey("custody-operation-proof") || !transport.Metadata.ContainsKey("custody-control-proof")
                    || transport.Metadata.Count > MaximumForwardedMetadataEntries
                    || transport.Metadata.Sum(p => Encoding.UTF8.GetByteCount(p.Key) + Encoding.UTF8.GetByteCount(p.Value)) > MaximumForwardedMetadataBytes)
                || keys && (transport is null || !CustodyBindingContract.Digest(transport.KeyClosureProfileDigest)
                    || !transport.Metadata.ContainsKey("custody-key-admission"))
                || ApiRequestSize(_binding, request.Envelope.Operation, data.Length, transport?.Metadata) > limit
                || ComponentRequestUpperBound(request.Envelope.Operation, data.Length) > limit)
            { return new(CustodyBindingTransportState.Refused); }
            CheckBudget();
            // Key delivery is unsupported until SDK/sidecar/component holder/buffer closure can be qualified.
            // Current authority must also be consumed by a native comparison inside the eventual component.
            string method = "custody-lifecycle/" + request.Envelope.Operation;
            bool authorized = await _authority.AuthorizeAsync(request, method, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            CheckBudget();
            if (!authorized)
            { return new(CustodyBindingTransportState.Unknown); }
            if (request.Envelope.Operation == "recover-transition")
            {
                bool recovery = await _authority.AuthorizeRecoveryAsync(request, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
                CheckBudget();
                if (!recovery) { return new(CustodyBindingTransportState.Unknown); }
            }
            CheckBudget();
            var bindingRequest = new BindingRequest(_binding, request.Envelope.Operation) { Data = data };
            if (transport is not null) foreach (var pair in transport.Metadata) bindingRequest.Metadata.Add(pair.Key, pair.Value);
            // No retry loop. Runtime/proxy/component retry disablement still requires deployment evidence.
            BindingResponse result = await _client.InvokeBindingAsync(bindingRequest, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            bindingResponse = result;
            CheckBudget();
            if (result.Data.Length == 0 || ResponseWireSize(result.Data.Length, result.Metadata) >= CustodyBindingContract.MaximumPageFrameBytes)
            { return new(CustodyBindingTransportState.Unknown); }
            CustodyBindingContract.RejectDuplicateProperties(result.Data.Span);
            response = JsonSerializer.Deserialize<CustodyBindingResponseV1>(result.Data.Span, CustodyBindingContract.Json);
            if (response is null || response.ProtocolVersion != CustodyBindingContract.Protocol
                || response.OperationId != request.Envelope.OperationId || response.RequestDigest != request.Envelope.RequestDigest)
            { return new(CustodyBindingTransportState.Unknown); }
            int branches = (response.OriginalHeader is null ? 0 : 1) + (response.Lifecycle is null ? 0 : 1)
                + (response.Current is null ? 0 : 1) + (response.TransitionVerified is null ? 0 : 1) + (response.Page is null ? 0 : 1)
                + (response.ManifestStatus is null ? 0 : 1) + (response.WrappedRegistration is null ? 0 : 1)
                + (response.KeyDelivery is null ? 0 : 1) + (response.Delivery is null ? 0 : 1);
            if (branches != 1) { return new(CustodyBindingTransportState.Unknown); }
            if (request.Manifest is { } manifest)
            {
                if (response.ManifestStatus is { } status)
                {
                    var nonterminal = status.Result;
                    if (nonterminal is null || nonterminal.State is not (DeletionManifestProviderState.NotStarted or DeletionManifestProviderState.Unknown)
                        || nonterminal.TenantId != request.Envelope.TenantId || nonterminal.BatchId != manifest.Original.Capability.BatchId
                        || nonterminal.ReservationReceiptId != manifest.ProtectionReservationReceiptId || nonterminal.TargetReceipts is not { Count: 0 }
                        || status.Authority != request.Envelope.Enrollment || status.ManifestDigest != manifest.Original.Capability.ManifestDigest
                        || status.Signature is not { Length: > 0 and <= 8192 }) { return new(CustodyBindingTransportState.Unknown); }
                }
                else if (response.OriginalHeader is not { } h || !h.Valid() || h.Authority != request.Envelope.Enrollment
                    || h.TenantId != request.Envelope.TenantId || h.OperationId != request.Envelope.OperationId
                    || h.RequestDigest != request.Envelope.RequestDigest || h.BatchId != manifest.Original.Capability.BatchId
                    || h.ReservationReceiptId != manifest.ProtectionReservationReceiptId || h.ManifestDigest != manifest.Original.Capability.ManifestDigest
                    || h.TargetCount != manifest.Original.Targets.Count) { return new(CustodyBindingTransportState.Unknown); }
            }
            else if (keys)
            {
                var use = request.WrapInput?.KeyUse ?? request.KeyUse;
                bool valid = request.Envelope.Operation == "wrap"
                    ? response.WrappedRegistration is { } wrapped && request.WrapInput is { } input
                        && wrapped.Identity == input.Identity && wrapped.OperationId == input.OperationId
                        && wrapped.ExpectedRevision == input.ExpectedRevision && wrapped.KekVersion == input.KekVersion
                        && wrapped.OpaqueWrappedKeyReference == input.OpaqueWrappedKeyReference && CustodyBindingContract.Text(wrapped.WrapReceiptId)
                    : response.KeyDelivery is { } delivery && delivery.KeyUse == use && delivery.RootKey is { Length: 32 }
                        || response.Delivery is { } status && status.KeyUse == use;
                if (!valid) { if (response.KeyDelivery is { } rejected) System.Security.Cryptography.CryptographicOperations.ZeroMemory(rejected.RootKey);
                    return new(CustodyBindingTransportState.Unknown); }
            }
            else if (request.Envelope.Operation == "complete-key-use")
            {
                if (response.Delivery is not { } completed || completed.KeyUse != request.KeyUse) return new(CustodyBindingTransportState.Unknown);
            }
            else if (request.Registration is { } registration)
            {
                if (response.Lifecycle is not { } lifecycle || lifecycle.Identity != registration.Identity
                    || lifecycle.OperationId != request.Envelope.OperationId || lifecycle.RequestDigest != request.Envelope.RequestDigest
                    || !ValidLifecycleResult(request, lifecycle))
                { return new(CustodyBindingTransportState.Unknown); }
            }
            else if (request.Transition is not null && response.TransitionVerified is null)
            { return new(CustodyBindingTransportState.Unknown); }
            if (request.Envelope.Operation == "read-original-page"
                && (response.Page is not { } page || request.Page is not { } read || page.Header != read.Header
                    || page.Offset != read.Offset || page.Length != read.Length || page.Data is null || page.Data.Length != read.Length
                    || page.PageSha256 != CustodyBindingContract.Hash(page.Data)
                    || JsonSerializer.SerializeToUtf8Bytes(page, CustodyBindingContract.Json).Length >= CustodyBindingContract.MaximumPageFrameBytes))
            { return new(CustodyBindingTransportState.Unknown); }
            if (request.Envelope.Operation == "read-current"
                && (response.Current is not { } current || current.RequestAttemptId != request.Envelope.RequestAttemptId
                    || current.Challenge != request.Envelope.Challenge || current.Authority != request.Envelope.Enrollment
                    || current.ObservedRevision < 0 || !CustodyBindingContract.Digest(current.CheckpointDigest)
                    || current.Signature is not { Length: > 0 and <= 8192 }))
            { return new(CustodyBindingTransportState.Unknown); }
            // Consumers still need independently current signed result verification. No bool here implements an authority port.
            CheckBudget();
            transferred = true;
            return new(CustodyBindingTransportState.ReceivedUnverified, response);
        }
        catch (ArgumentException) { return new(CustodyBindingTransportState.Refused); }
        catch (Exception) { return new(CustodyBindingTransportState.Unknown); }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(data);
            if (request?.WrapInput is { } input) System.Security.Cryptography.CryptographicOperations.ZeroMemory(input.RootKey);
            if (!transferred && response?.KeyDelivery is { } rejected) System.Security.Cryptography.CryptographicOperations.ZeroMemory(rejected.RootKey);
            if (bindingResponse is not null && System.Runtime.InteropServices.MemoryMarshal.TryGetArray(bindingResponse.Data, out var array))
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(array.AsSpan());
        }
    }

    private static bool ValidLifecycleResult(CustodyBindingRequestV1 request, CustodyKeyLifecycleOutcome result)
    {
        if (result.Status is CustodyKeyLifecycleStatus.Unknown or CustodyKeyLifecycleStatus.Unavailable or CustodyKeyLifecycleStatus.Conflict)
        { return result.ReceiptId is null && result.RestoreBarrierReceiptId is null; }
        if (result.RestoreBarrierReceiptId is not null || !CustodyBindingContract.CustodyText(result.ReceiptId)) { return false; }
        if (request.Lifecycle is { } original)
        {
            var expected = original.Action == CustodyKeyLifecycleAction.Pin ? CustodyKeyLifecycleStatus.Pinned : CustodyKeyLifecycleStatus.Unpinned;
            return result.Status == expected || result.Status == CustodyKeyLifecycleStatus.NotPerformed;
        }
        return request.Envelope.Operation == "lookup-original" && result.Status == CustodyKeyLifecycleStatus.Wrapped
            && result.ReceiptId == request.Registration!.WrapReceiptId;
    }

    internal const int MaximumForwardedMetadataEntries = 64;
    internal const int MaximumForwardedMetadataBytes = 64 * 1024;
    // Includes map-entry tags/varints, sanitized header prefixes and generated trace metadata.
    // This is a candidate admission budget, not evidence of an installed header limiter.
    internal const int MaximumForwardedMetadataWireBytes = MaximumForwardedMetadataBytes + MaximumForwardedMetadataEntries * 16;

    // Exact protobuf message size for pinned InvokeBindingRequest: name=1,data=2,operation=4; metadata=3.
    // gRPC's five-byte stream prefix is separate from its protobuf message-size limit.
    internal static int ApiRequestSize(string name, string operation, int dataLength, IReadOnlyDictionary<string, string>? metadata = null)
        => checked(Field(Encoding.UTF8.GetByteCount(name)) + Field(dataLength) + Field(Encoding.UTF8.GetByteCount(operation)) + MetadataSize(metadata));

    internal static int ComponentRequestSize(string operation, int dataLength, IReadOnlyDictionary<string, string>? forwardedMetadata = null)
        => checked(Field(dataLength) + Field(Encoding.UTF8.GetByteCount(operation)) + MetadataSize(forwardedMetadata));

    internal static int ComponentRequestUpperBound(string operation, int dataLength)
        => checked(ComponentRequestSize(operation, dataLength) + MaximumForwardedMetadataWireBytes);

    private static int MetadataSize(IReadOnlyDictionary<string, string>? metadata)
    {
        int total = 0;
        if (metadata is not null)
        {
            foreach (var pair in metadata)
            { total = checked(total + Field(checked(Field(Encoding.UTF8.GetByteCount(pair.Key)) + Field(Encoding.UTF8.GetByteCount(pair.Value))))); }
        }
        return total;
    }

    // SDK-visible-field size only: actual data (including whitespace), visible metadata and a gRPC prefix.
    // Unknown protobuf fields/content_type may already be discarded. Full frame enforcement requires
    // the missing component/transport limit and independent observer; this is not that physical proof.
    internal static int ResponseWireSize(int dataLength, IReadOnlyDictionary<string, string> metadata)
    {
        int total = checked(5 + Field(dataLength));
        foreach (var pair in metadata)
        { total = checked(total + Field(checked(Field(Encoding.UTF8.GetByteCount(pair.Key)) + Field(Encoding.UTF8.GetByteCount(pair.Value))))); }
        return total;
    }

    private static int Field(int bytes)
    {
        if (bytes < 0) { throw new ArgumentOutOfRangeException(nameof(bytes)); }
        int prefix = 1; uint value = (uint)bytes;
        while ((value >>= 7) != 0) { prefix++; }
        return checked(1 + prefix + bytes);
    }
}

// The request factory must obtain a new exact current private lookup authorization for each page.
// It cannot synthesize signatures from a prior destroy-manifest proof by changing its Action.
internal sealed class CustodyBindingDaprPageSource(CustodyBindingDaprInvoker invoker,
    Func<CustodyBindingPageReadV1, CancellationToken, Task<CustodyBindingRequestV1?>> currentAuthorizedRequest,
    TimeSpan callBudget) : ICustodyBindingOriginalPageSource
{
    public async Task<ReadOnlyMemory<byte>?> ReadAsync(CustodyBindingPageReadV1 exactRead, CancellationToken cancellationToken)
    {
        if (callBudget <= TimeSpan.Zero || callBudget > TimeSpan.FromSeconds(30)) { return null; }
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(callBudget);
        long started = Stopwatch.GetTimestamp();
        void CheckBudget()
        {
            budget.Token.ThrowIfCancellationRequested();
            if (Stopwatch.GetElapsedTime(started) >= callBudget) { throw new OperationCanceledException(budget.Token); }
        }
        try
        {
            CheckBudget();
            var request = await currentAuthorizedRequest(exactRead, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            CheckBudget();
            if (request is null || request.Envelope.Operation != "read-original-page" || request.Page != exactRead) { return null; }
            var response = await invoker.InvokeAsync(request, callBudget, budget.Token).ConfigureAwait(false);
            CheckBudget();
            ReadOnlyMemory<byte>? frame = response.State == CustodyBindingTransportState.ReceivedUnverified && response.Response?.Page is { } page
                ? JsonSerializer.SerializeToUtf8Bytes(page, CustodyBindingContract.Json) : null;
            CheckBudget();
            return frame;
        }
        catch (OperationCanceledException) { return null; }
    }
}

// Each phase maps to a distinct request and private authorization method. This factory supplies no authority implementation.
internal static class CustodyBindingTransitionRequests
{
    internal static CustodyBindingRequestV1 Create(CustodyBindingEnvelopeV1 exactEnvelope, AnchoredStateTransition transition, string method)
    {
        if (method is not ("admit-transition" or "record-transition" or "recover-transition" or "verify-transition" or "lookup-transition")
            || exactEnvelope.Operation != method) { throw new ArgumentException("Distinct exact transition phase required."); }
        var request = new CustodyBindingRequestV1(exactEnvelope, Transition: transition);
        _ = CustodyBindingContract.Encode(request);
        return request;
    }
}
