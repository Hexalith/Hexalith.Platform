using Dapr.Proto.Components.V1;
using Google.Protobuf;
using Grpc.Core;
using Hexalith.Platform.Custody.Dapr;
using Hexalith.Platform.Secrets.Runtime;

internal static class ServiceBoundaryTests
{
    internal static async Task Run()
    {
        using var f = new Fixture(); var service = new BindingService(f.Profile, f.Provider, f.Store); var context = new FixtureCallContext();
        var request = f.Request("read-current"); var invoke = new InvokeRequest { Operation = "read-current", Data = ByteString.CopyFrom(CustodyBindingContract.Encode(request)) };
        foreach (var pair in f.OwnerMetadata(request)) invoke.Metadata.Add(pair.Key, pair.Value);
        await Refused(() => service.Invoke(invoke, context));
        await Refused(() => service.Init(Init("wrong-profile"), context));
        await service.Init(Init(f.Profile.ProfileDigest), context);
        var before = f.Store.State.Head; var response = await service.Invoke(invoke, context);
        if (CustodyJson.Read<CustodyBindingResponseV1>(response.Data.Span).Current is null) throw new Exception("Service invocation missing signed current head.");
        await service.Init(Init(f.Profile.ProfileDigest), context);
        await service.Invoke(invoke, context);
        var noProof = invoke.Clone(); noProof.Metadata.Remove("custody-control-proof"); await Refused(() => service.Invoke(noProof, context));
        var changed = invoke.Clone(); changed.Operation = "destroy-manifest"; await Refused(() => service.Invoke(changed, context));
        if (f.Store.State.Head != before) throw new Exception("Init/refused service calls mutated authority.");
    }
    internal static OutputBindingInitRequest Init(string digest) {
        var request = new OutputBindingInitRequest { Metadata = new() };
        request.Metadata.Properties.Add("profileDigest", digest); return request;
    }
    private static async Task Refused(Func<Task> action) { try { await action(); } catch (RpcException e) when (e.StatusCode == StatusCode.FailedPrecondition) { return; } throw new Exception("Service boundary did not refuse."); }
}
internal sealed class FixtureCallContext : ServerCallContext
{
    protected override string MethodCore => "component-test";
    protected override string HostCore => "fixture";
    protected override string PeerCore => "fixture";
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override Metadata RequestHeadersCore { get; } = new();
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;
    protected override Metadata ResponseTrailersCore { get; } = new();
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore => new("fixture", new Dictionary<string, List<AuthProperty>>());
    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => throw new NotSupportedException();
    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
}
