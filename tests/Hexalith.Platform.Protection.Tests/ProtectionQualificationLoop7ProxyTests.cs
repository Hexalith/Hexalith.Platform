using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.Platform.Custody;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Real ActorProxy remoting bytes cross an owned loopback HTTP boundary and deserialize the returned nested availability contract.</summary>
public sealed class ProtectionQualificationLoop7ProxyTests
{
    [Fact]
    public async Task ReadinessRequestAndResponseUseTheRealDaprActorWireContract()
    {
        using var port = new TcpListener(IPAddress.Loopback, 0); port.Start(); int chosen = ((IPEndPoint)port.LocalEndpoint).Port; port.Stop();
        using var listener = new HttpListener(); listener.Prefixes.Add($"http://127.0.0.1:{chosen}/"); listener.Start();
        var request = new DeletionCapabilitySigningReadinessRequest("tenant-a", "DeletionBatchCapabilitySigningKey", "key-1", "issuer", "audience", "anchor", "1", new string('a', 64), 1, new string('b', 64), "provider");
        var expected = new DeletionCapabilitySigningAvailability(request, "provider", "revision", ["LookupDeletionCapability"], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(20));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var serve = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync().WaitAsync(deadline.Token);
            context.Request.RawUrl!.ShouldContain("/actors/");
            // Dapr's own pinned serialization provider builds the wire response; no hand-shaped XML/JSON substitute.
            var providerType = typeof(ActorProxy).Assembly.GetType("Dapr.Actors.Communication.ActorMessageBodyDataContractSerializationProvider")!;
            object provider = Activator.CreateInstance(providerType)!;
            var generated = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.IsDynamic).SelectMany(assembly => assembly.GetTypes()).ToArray();
            var requestType = generated.Single(type => type.Name == "ReadAvailabilityAsyncReqBody");
            var responseType = generated.Single(type => type.Name == "ReadAvailabilityAsyncRespBody");
            object requestSerializer = providerType.GetMethod("CreateRequestMessageBodySerializer")!.Invoke(provider, [typeof(IDeletionCapabilitySigningActor), new[] { typeof(DeletionCapabilitySigningReadinessRequest) }, new[] { requestType }])!;
            using var requestBytes = new MemoryStream(); await context.Request.InputStream.CopyToAsync(requestBytes, deadline.Token); requestBytes.Position = 0;
            object pendingRead = typeof(ActorProxy).Assembly.GetType("Dapr.Actors.Communication.IActorRequestMessageBodySerializer")!.GetMethod("DeserializeAsync")!.Invoke(requestSerializer, [requestBytes])!;
            var readTask = (Task)pendingRead.GetType().GetMethod("AsTask")!.Invoke(pendingRead, null)!; await readTask.WaitAsync(deadline.Token);
            object incoming = readTask.GetType().GetProperty("Result")!.GetValue(readTask)!;
            object incomingWrapper = incoming.GetType().GetProperty("Value")?.GetValue(incoming) ?? incoming.GetType().GetField("Value")!.GetValue(incoming)!;
            requestType.GetFields().Single(field => field.FieldType == typeof(DeletionCapabilitySigningReadinessRequest)).GetValue(incomingWrapper).ShouldBe(request);
            object returned = Activator.CreateInstance(responseType)!;
            responseType.GetFields().Single(field => field.FieldType == typeof(DeletionCapabilitySigningAvailability)).SetValue(returned, expected);
            object factory = providerType.GetMethod("CreateMessageBodyFactory")!.Invoke(provider, null)!;
            object body = factory.GetType().GetMethod("CreateResponseMessageBody")!.Invoke(factory, [typeof(IDeletionCapabilitySigningActor).FullName!, nameof(IDeletionCapabilitySigningActor.ReadAvailabilityAsync), returned])!;
            object serializer = providerType.GetMethod("CreateResponseMessageBodySerializer")!.Invoke(provider, [typeof(IDeletionCapabilitySigningActor), new[] { typeof(DeletionCapabilitySigningAvailability) }, new[] { responseType }])!;
            byte[] response = (byte[])typeof(ActorProxy).Assembly.GetType("Dapr.Actors.Communication.IActorResponseMessageBodySerializer")!.GetMethod("Serialize")!.Invoke(serializer, [body])!;
            context.Response.StatusCode = 200; context.Response.ContentLength64 = response.Length;
            await context.Response.OutputStream.WriteAsync(response, deadline.Token); context.Response.Close();
        }, deadline.Token);
        var proxy = ActorProxy.Create<IDeletionCapabilitySigningActor>(new ActorId(DeletionCapabilitySigningActor.GetReadinessActorId(request)), DeletionCapabilitySigningActor.ActorTypeName,
            new ActorProxyOptions { HttpEndpoint = $"http://127.0.0.1:{chosen}", RequestTimeout = TimeSpan.FromSeconds(5) });
        var invocation = proxy.ReadAvailabilityAsync(request);
        await Task.WhenAny(serve, invocation).WaitAsync(deadline.Token);
        if (serve.IsFaulted) { await serve; }
        var actual = await invocation.WaitAsync(deadline.Token);
        await serve; actual.ShouldNotBeNull(); actual.Request.ShouldBe(request); actual.ProviderIdentity.ShouldBe(expected.ProviderIdentity); actual.Operations.ShouldBe(expected.Operations);
        actual.AuthorityRevision.ShouldBe(expected.AuthorityRevision); actual.ObservedAt.ShouldBe(expected.ObservedAt); actual.ValidUntil.ShouldBe(expected.ValidUntil);
    }
}
