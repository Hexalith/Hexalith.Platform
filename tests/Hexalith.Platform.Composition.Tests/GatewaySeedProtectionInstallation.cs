namespace Hexalith.Platform.Composition.Tests;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
using Hexalith.Platform.Protection;

/// <summary>Admits the real host protection registration with a pinned component installation; no native operation is invoked by HTTP metadata tests.</summary>
internal sealed class GatewaySeedProtectionInstallation : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gateway-seed-installation-" + Guid.NewGuid().ToString("N"));
    private readonly string _variable = "HEXALITH_SEED_NATIVE_" + Guid.NewGuid().ToString("N").ToUpperInvariant();

    private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);

    internal IReadOnlyDictionary<string, string?> Configuration { get; }

    internal GatewaySeedProtectionInstallation()
    {
        Directory.CreateDirectory(_directory);
        using var signing = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var caKey = RSA.Create(2048);
        var request = new CertificateRequest("CN=Gateway Seed Component CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var ca = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var inventory = Write("inventory.json", "{\"runtime\":{\"currentInventoryAuthorityReference\":\"inventory-authority\"}}"u8.ToArray());
        var copy = Write("copy.json", "{}"u8.ToArray());
        string credential = Path.Combine(_directory, "authority-bearer");
        File.WriteAllText(credential, "component-private-bearer");
        var authority = new IndependentDaprAuthorityBinding("private-authority", "file:" + credential, signing.ExportSubjectPublicKeyInfoPem(), "component", "tenant-a", "1");
        var installation = new ErasureRootHostInstallation(new ErasureRootInstallation("seed-component", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant-a"] = "epoch" })
            { TrustedCaCertificatePem = ca.ExportCertificatePem() }, "https://seed-native.invalid/", "env:" + _variable,
            new() { ["tenant-a"] = "registry-1" }, authority, authority, authority, authority, authority,
            [new("tenant-a", "DeletionBatchCapabilitySigningKey", "key-1", "issuer", "audience", "anchor", "1", signing.ExportSubjectPublicKeyInfo(), 1, false)],
            inventory.Path, inventory.Hash, copy.Path, copy.Hash);
        var pinned = Write("installation.json", JsonSerializer.SerializeToUtf8Bytes(installation));
        Environment.SetEnvironmentVariable(_variable, "component-native-bearer");
        Configuration = new Dictionary<string, string?>
        {
            ["EventStore:PayloadProtection:Enabled"] = "true",
            ["EventStore:PayloadProtection:InstallationFile"] = pinned.Path,
            ["EventStore:PayloadProtection:InstallationSha256"] = pinned.Hash,
            ["EventStore:PayloadProtection:QualificationObserver"] = "true",
        };
        // The production entry point validates this installation before deferred WebApplicationFactory
        // configuration callbacks run. Supply the same pinned inputs at CreateBuilder time.
        foreach (var (name, value) in Configuration)
        {
            string variable = name.Replace(":", "__", StringComparison.Ordinal);
            _previous[variable] = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, value);
        }
    }

    private (string Path, string Hash) Write(string name, byte[] bytes)
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, bytes);
        return (path, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    public void Dispose()
    {
        foreach (var (name, value) in _previous) Environment.SetEnvironmentVariable(name, value);
        Environment.SetEnvironmentVariable(_variable, null);
        Directory.Delete(_directory, true);
    }
}
