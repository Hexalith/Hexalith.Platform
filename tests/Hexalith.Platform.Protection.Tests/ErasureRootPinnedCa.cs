using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Parseable dedicated CA pin for production-client construction. It is not an installed trust anchor.</summary>
internal static class ErasureRootPinnedCa
{
    internal static string Pem { get; } = Create();

    private static string Create()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=erasure-root-tls-test", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
        return certificate.ExportCertificatePem();
    }
}
