using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Hexalith.Folders.AppHost.Tests;
string directory = Path.Combine(Path.GetTempPath(), "story-2-12-pem-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
string? previous = Environment.GetEnvironmentVariable("SSL_CERT_FILE");
try
{
    using RSA key = RSA.Create(2048);
    CertificateRequest request = new("CN=Inherited Test Root", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    using X509Certificate2 inherited = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
    string trust = Path.Combine(directory, "inherited.pem");
    await File.WriteAllTextAsync(trust, inherited.ExportCertificatePem().TrimEnd());
    Environment.SetEnvironmentVariable("SSL_CERT_FILE", trust);
    OperationalProviderFixture fixture = new(directory, "pem-control");
    try { await fixture.StartAsync(); throw new InvalidOperationException("docker-boundary-not-reached"); }
    catch (InvalidOperationException error) when (error.Message == "stop-before-docker") { }
    X509Certificate2Collection parsed = new();
    parsed.ImportFromPemFile(fixture.TrustFile);
    if (parsed.Count != 2 || !parsed.Cast<X509Certificate2>().Any(item => item.Thumbprint == inherited.Thumbprint))
        throw new InvalidOperationException("merged-roots-not-preserved");
    foreach (X509Certificate2 certificate in parsed) certificate.Dispose();
    Console.WriteLine("ACTUAL_PROVIDER_FIXTURE_TRUST inheritedWithoutFinalNewline=true certificates=2 inheritedRootPreserved=true dockerStarted=false");
}
finally
{
    Environment.SetEnvironmentVariable("SSL_CERT_FILE", previous);
    Directory.Delete(directory, recursive: true);
}