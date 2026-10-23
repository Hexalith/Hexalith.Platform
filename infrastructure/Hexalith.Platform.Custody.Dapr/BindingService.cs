using Dapr.Proto.Components.V1;
using PingRequest = Dapr.Client.Autogen.Grpc.v1.PingRequest;
using PingResponse = Dapr.Client.Autogen.Grpc.v1.PingResponse;
using Google.Protobuf;
using Grpc.Core;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed class BindingService(ComponentProfile profile, SemanticProvider provider, IAuthorityStore store) : OutputBinding.OutputBindingBase
{
    private int _initialized;
    public override async Task<OutputBindingInitResponse> Init(OutputBindingInitRequest request, ServerCallContext context)
    {
        if (request.Metadata?.Properties is not { Count: 1 } values || !values.TryGetValue("profileDigest", out string? digest)
            || digest != profile.ProfileDigest) throw Closed("Init profile mismatch.");
        try { foreach (string tenant in profile.Tenants) _ = await store.ReadAsync(tenant, context.CancellationToken); }
        catch { throw Closed("Surviving authority unavailable."); }
        Interlocked.Exchange(ref _initialized, 1); // Same-profile re-Init does not clear any durable state.
        return new();
    }
    public override Task<PingResponse> Ping(PingRequest request, ServerCallContext context) => Task.FromResult(new PingResponse());
    public override Task<ListOperationsResponse> ListOperations(ListOperationsRequest request, ServerCallContext context)
    {
        var response = new ListOperationsResponse(); response.Operations.AddRange(CustodyBindingContract.Operations); return Task.FromResult(response);
    }
    public override async Task<InvokeResponse> Invoke(InvokeRequest request, ServerCallContext context)
    {
        if (Volatile.Read(ref _initialized) == 0 || !FrameAllowed(request)
            || !request.Metadata.TryGetValue("custody-operation-proof", out string? operation)
            || !request.Metadata.TryGetValue("custody-control-proof", out string? control)) throw Closed("Exact operation proofs and Init required.");
        byte[] data = [];
        byte[] responseBytes = [];
        CustodyBindingRequestV1? decoded = null;
        CustodyBindingResponseV1? result = null;
        try
        {
            data = request.Data.ToByteArray();
            decoded = CustodyBindingContract.Decode(data);
            if (decoded?.Envelope.Operation != request.Operation) throw Closed("Operation frame mismatch.");
            result = await provider.InvokeAsync(data, CustodyJson.Read<SignedStatement>(Convert.FromBase64String(operation)),
                CustodyJson.Read<SignedStatement>(Convert.FromBase64String(control)), context.CancellationToken,
                request.Metadata.TryGetValue("custody-key-admission", out string? keyAdmission)
                    ? CustodyJson.Read<SignedStatement>(Convert.FromBase64String(keyAdmission)) : null);
            responseBytes = CustodyJson.Bytes(result);
            var response = new InvokeResponse { Data = ByteString.CopyFrom(responseBytes), ContentType = "application/json" };
            if (response.CalculateSize() + 5 >= CustodyBindingContract.MaximumPageFrameBytes) throw Closed("Complete response frame exceeds bound.");
            return response;
        }
        catch (RpcException) { throw; }
        catch { throw Closed("Authority/effect unknown; recover the original without resending native intent."); }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(data);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(responseBytes);
            if (decoded?.WrapInput is { } input) System.Security.Cryptography.CryptographicOperations.ZeroMemory(input.RootKey);
            if (result?.KeyDelivery is { } delivery) System.Security.Cryptography.CryptographicOperations.ZeroMemory(delivery.RootKey);
            // Immutable protobuf ByteString and gRPC/native transport copies remain enrolled holders;
            // managed array cleanup cannot establish their completion or physical qualification.
        }
    }
    internal static bool FrameAllowed(InvokeRequest request) => request.CalculateSize() <= CustodyBindingContract.CandidateRequestMessageBytes
        && request.Data.Length <= CustodyBindingContract.MaximumRequestBytes
        && request.Metadata.Count <= CustodyBindingDaprInvoker.MaximumForwardedMetadataEntries
        && request.Metadata.Sum(pair => System.Text.Encoding.UTF8.GetByteCount(pair.Key) + System.Text.Encoding.UTF8.GetByteCount(pair.Value))
            <= CustodyBindingDaprInvoker.MaximumForwardedMetadataBytes;
    private static RpcException Closed(string message) => new(new Status(StatusCode.FailedPrecondition, message));
}
