namespace Hexalith.Platform.Composition.Tests;

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

/// <summary>A loopback stand-in for both the Tenants sidecar invoke API and the gateway status endpoint.</summary>
internal sealed class OperatorRelayUpstream : IAsyncDisposable
{
    internal const string BoundPath = "/v1.0/invoke/eventstore/method/api/v1/commands/bound/workload";
    internal const string StatusPrefix = "/api/v1/commands/status/";

    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    private OperatorRelayUpstream(HttpListener listener, Uri endpoint)
    {
        _listener = listener;
        Endpoint = endpoint;
        _loop = Task.Run(ServeAsync, CancellationToken.None);
    }

    internal Uri Endpoint { get; }

    internal ConcurrentQueue<OperatorRelayUpstreamRequest> Requests { get; } = new();

    /// <summary>Gets or sets the scripted answer; the default accepts every submission and completes every status.</summary>
    internal Func<OperatorRelayUpstreamRequest, (int Status, string Body)> Respond { get; set; } = Default;

    internal static OperatorRelayUpstream Start()
    {
        for (int attempt = 0; ; attempt++)
        {
            int port;
            using (var probe = new TcpListener(IPAddress.Loopback, 0))
            {
                probe.Start();
                port = ((IPEndPoint)probe.LocalEndpoint).Port;
            }

            var endpoint = new Uri("http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/");
            var listener = new HttpListener();
            listener.Prefixes.Add(endpoint.AbsoluteUri);
            try
            {
                listener.Start();
                return new OperatorRelayUpstream(listener, endpoint);
            }
            catch (HttpListenerException) when (attempt < 8)
            {
                listener.Close();
            }
        }
    }

    internal static string CommandType(OperatorRelayUpstreamRequest request)
    {
        using var document = JsonDocument.Parse(request.Body);
        return document.RootElement.GetProperty("command").GetProperty("commandType").GetString()!;
    }

    internal static (int Status, string Body) Default(OperatorRelayUpstreamRequest request)
    {
        if (request.Method == "POST" && request.Path == BoundPath)
        {
            using var document = JsonDocument.Parse(request.Body);
            string id = document.RootElement.GetProperty("command").GetProperty("messageId").GetString()!;
            return (202, JsonSerializer.Serialize(new { correlationId = id, messageId = id }));
        }

        if (request.Method == "GET" && request.Path.StartsWith(StatusPrefix, StringComparison.Ordinal))
        {
            string id = request.Path[StatusPrefix.Length..];
            return (200, JsonSerializer.Serialize(new { messageId = id, correlationId = id, statusCode = 4, status = "Completed", retryable = false }));
        }

        return (404, "{}");
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Close();
        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ObjectDisposedException or HttpListenerException or OperationCanceledException)
        {
            // Closed by design.
        }

        _stop.Dispose();
    }

    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is ObjectDisposedException or HttpListenerException or InvalidOperationException)
            {
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            string body = await reader.ReadToEndAsync(_stop.Token).ConfigureAwait(false);
            var headers = context.Request.Headers.AllKeys.OfType<string>()
                .ToDictionary(name => name.ToLowerInvariant(), name => context.Request.Headers[name]!, StringComparer.Ordinal);
            var request = new OperatorRelayUpstreamRequest(context.Request.HttpMethod, context.Request.Url!.AbsolutePath, headers, body);
            Requests.Enqueue(request);
            (int status, string answer) = Respond(request);
            byte[] bytes = Encoding.UTF8.GetBytes(answer);
            context.Response.StatusCode = status;
            context.Response.ContentType = status >= 400 ? "application/problem+json" : "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, _stop.Token).ConfigureAwait(false);
            context.Response.Close();
        }
    }
}
