using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Hexalith.Platform.Identity.Tests;

/// <summary>Loopback-only test server; every accepted connection has one GET and one response.</summary>
internal sealed class P1LoopbackReceiptServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly List<Task> _connections = [];
    private readonly Task _accept;
    private readonly X509Certificate2 _client;
    private readonly X509Certificate2 _server;
    private readonly X509Certificate2? _serverIntermediate;
    private X509Certificate2? _requiredClientIntermediate;
    private readonly Func<string, CancellationToken, Task<byte[]>> _body;

    internal P1LoopbackReceiptServer(X509Certificate2 server, X509Certificate2 client,
        Func<string, CancellationToken, Task<byte[]>> body, string mediaType, X509Certificate2? serverIntermediate = null)
    {
        _server = server;
        _serverIntermediate = serverIntermediate;
        _client = client;
        _body = body;
        MediaType = mediaType;
        _listener.Start();
        Origin = "https://localhost:" + ((IPEndPoint)_listener.LocalEndpoint).Port;
        _accept = Task.Run(AcceptAsync);
    }

    internal string Origin { get; }
    internal string MediaType { get; set; }
    internal string? CacheControl { get; set; } = "no-store";
    internal string ExtraHeaders { get; set; } = "";
    internal int? AdvertisedLength { get; set; }
    internal int? BodyBytesToSend { get; set; }
    internal byte[] BodySuffix { get; set; } = [];
    internal bool RequestClientCertificate { get; set; } = true;
    internal int StatusCode { get; set; } = 200;
    internal string? StatusLine { get; set; }
    internal Func<CancellationToken, Task>? BeforeTlsHandshake { get; set; }
    internal Func<CancellationToken, Task>? BeforeResponse { get; set; }
    internal Func<CancellationToken, Task>? AfterPartialBody { get; set; }
    internal ConcurrentQueue<(string Method, string Path, string Host, bool NoStore, bool Mutual)> Requests { get; } = new();

    internal void RequireClientChainThrough(X509Certificate2 intermediate) => _requiredClientIntermediate = intermediate;

    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
                lock (_connections)
                {
                    _connections.Add(Task.Run(() => ServeAsync(client)));
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException) when (_stop.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (_stop.IsCancellationRequested)
        {
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        using (var tls = new SslStream(client.GetStream(), false,
            (sender, certificate, chain, errors) => certificate is not null
                && certificate.GetRawCertData().AsSpan().SequenceEqual(_client.RawData)
                && (_requiredClientIntermediate is null || chain?.ChainElements.Cast<X509ChainElement>()
                    .Any(element => element.Certificate.RawData.AsSpan().SequenceEqual(_requiredClientIntermediate.RawData)) == true)))
        {
            try
            {
                if (BeforeTlsHandshake is not null) await BeforeTlsHandshake(_stop.Token).ConfigureAwait(false);
                SslStreamCertificateContext context = SslStreamCertificateContext.Create(_server,
                    _serverIntermediate is null ? null : new X509Certificate2Collection(_serverIntermediate), offline: true);
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificateContext = context,
                    ClientCertificateRequired = RequestClientCertificate,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                }, _stop.Token).ConfigureAwait(false);

                byte[] header = new byte[16_384];
                byte[] one = new byte[1];
                int count = 0;
                while (count < header.Length)
                {
                    int read = await tls.ReadAsync(one, _stop.Token).ConfigureAwait(false);
                    if (read != 1) return;
                    header[count++] = one[0];
                    if (count >= 4 && header.AsSpan(count - 4, 4).SequenceEqual("\r\n\r\n"u8)) break;
                }

                if (count >= header.Length) return;
                string request = Encoding.ASCII.GetString(header, 0, count);
                string[] requestLine = request.Split("\r\n", StringSplitOptions.None)[0].Split(' ');
                string method = requestLine[0];
                string path = requestLine[1];
                string host = request.Split("\r\n", StringSplitOptions.None)
                    .Single(line => line.StartsWith("Host: ", StringComparison.Ordinal))[6..];
                bool noStore = request.Contains("\r\nCache-Control: no-store\r\n", StringComparison.Ordinal);
                Requests.Enqueue((method, path, host, noStore, tls.IsMutuallyAuthenticated));
                if (BeforeResponse is not null) await BeforeResponse(_stop.Token).ConfigureAwait(false);
                byte[] body = await _body(path, _stop.Token).ConfigureAwait(false);
                string response = (StatusLine ?? "HTTP/1.1 " + StatusCode + " " + (StatusCode == 200 ? "OK" : "Other")) + "\r\n"
                    + "Content-Type: " + MediaType + "\r\n"
                    + (CacheControl is null ? "" : "Cache-Control: " + CacheControl + "\r\n")
                    + "Content-Length: " + (AdvertisedLength ?? body.Length)
                    + "\r\nConnection: close\r\n" + ExtraHeaders + "\r\n";
                await tls.WriteAsync(Encoding.ASCII.GetBytes(response), _stop.Token).ConfigureAwait(false);
                await tls.WriteAsync(body.AsMemory(0, BodyBytesToSend ?? body.Length), _stop.Token).ConfigureAwait(false);
                await tls.FlushAsync(_stop.Token).ConfigureAwait(false);
                if (AfterPartialBody is not null) await AfterPartialBody(_stop.Token).ConfigureAwait(false);
                if (BodySuffix.Length > 0) await tls.WriteAsync(BodySuffix, _stop.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or AuthenticationException or OperationCanceledException
                or ObjectDisposedException or SocketException)
            {
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        await _accept.ConfigureAwait(false);
        Task[] connections;
        lock (_connections)
        {
            connections = [.. _connections];
        }

        await Task.WhenAll(connections).ConfigureAwait(false);
        _stop.Dispose();
    }
}
