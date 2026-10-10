using System.Net;
using System.Net.Sockets;

namespace Hexalith.Platform.Identity.Tests;

/// <summary>Counts any attempted off-origin certificate-distribution connection on loopback.</summary>
internal sealed class P1NetworkTrap : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _accept;
    private int _hits;

    internal P1NetworkTrap()
    {
        _listener.Start();
        Url = "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port + "/issuer.crl";
        _accept = Task.Run(AcceptAsync);
    }

    internal string Url { get; }
    internal int Hits => Volatile.Read(ref _hits);

    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
                Interlocked.Increment(ref _hits);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException) when (_stop.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        await _accept.ConfigureAwait(false);
        _stop.Dispose();
    }
}
