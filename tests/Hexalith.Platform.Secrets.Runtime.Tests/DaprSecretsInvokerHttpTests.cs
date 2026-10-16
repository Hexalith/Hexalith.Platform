using System.Net;
using System.Net.Sockets;
using System.Text;

using Dapr.Client;

using Hexalith.Platform.Secrets.Runtime;

using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class DaprSecretsInvokerHttpTests
{
    [Fact]
    public async Task ChunkedOversizeIsRejectedBeforeTheHttpBodyCompletes()
    {
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var oversizedSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBody = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool bodyCompleted = false;
        Task server = Task.Run(async () =>
        {
            using TcpClient socket = await listener.AcceptTcpClientAsync(cancellation);
            using NetworkStream stream = socket.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            while (await reader.ReadLineAsync(cancellation) is { Length: > 0 }) { }
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Type: application/json\r\nConnection: close\r\n\r\n100001\r\n"), cancellation);
            await stream.WriteAsync(new byte[SecretsProof.MaximumBytes + 1], cancellation);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"), cancellation);
            await stream.FlushAsync(cancellation);
            oversizedSent.SetResult();
            await releaseBody.Task.WaitAsync(cancellation);
            try { await stream.WriteAsync(Encoding.ASCII.GetBytes("0\r\n\r\n"), cancellation); bodyCompleted = true; }
            catch (IOException) { } // The bounded invoker may already have closed the oversized response.
        }, cancellation);
        try
        {
            using DaprClient client = new DaprClientBuilder().UseHttpEndpoint($"http://127.0.0.1:{port}").Build();
            var invoker = new DaprSecretsInvoker(client);
            Task<string?> response = invoker.InvokeAsync("isolated-test-app", "query", "{}", cancellation);
            await oversizedSent.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellation);
            (await response.WaitAsync(TimeSpan.FromSeconds(5), cancellation)).ShouldBeNull();
            bodyCompleted.ShouldBeFalse();
        }
        finally
        {
            releaseBody.TrySetResult();
            await server.WaitAsync(TimeSpan.FromSeconds(5), cancellation);
        }
    }
}
