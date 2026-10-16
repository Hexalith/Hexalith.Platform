namespace Hexalith.Platform.Composition;

using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>Negotiates PostgreSQL TLS and validates the run CA and host; an open port is insufficient.</summary>
internal sealed class PlatformPostgresqlHealthCheck(int port, string authorityPath) : IHealthCheck
{
    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using CancellationTokenSource bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            bound.CancelAfter(TimeSpan.FromSeconds(5));
            using TcpClient client = new();
            await client.ConnectAsync("127.0.0.1", port, bound.Token).ConfigureAwait(false);
            NetworkStream stream = client.GetStream();
            await stream.WriteAsync(new byte[] { 0, 0, 0, 8, 4, 210, 22, 47 }, bound.Token).ConfigureAwait(false);
            byte[] response = new byte[1];
            await stream.ReadExactlyAsync(response, bound.Token).ConfigureAwait(false);
            if (response[0] != (byte)'S') return HealthCheckResult.Unhealthy("PostgreSQL TLS is unavailable.");
            using X509Certificate2 authority = X509Certificate2.CreateFromPemFile(authorityPath);
            using SslStream tls = new(stream, false);
            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "localhost", EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateChainPolicy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, CustomTrustStore = { authority }, RevocationMode = X509RevocationMode.NoCheck },
            }, bound.Token).ConfigureAwait(false);
            return HealthCheckResult.Healthy("PostgreSQL run-CA and hostname verified; backend version still requires SQL observation.");
        }
        catch (Exception error) when (error is IOException or SocketException or AuthenticationException or OperationCanceledException or System.Security.Cryptography.CryptographicException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL verify-full readiness failed.");
        }
    }
}
