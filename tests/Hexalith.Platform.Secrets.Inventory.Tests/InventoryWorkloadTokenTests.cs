using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Hexalith.Platform.Secrets.Inventory.Tests;

public sealed class InventoryWorkloadTokenTests
{
    [Fact]
    public async Task GetAsyncReturnsAccessTokenAndRefusesUnconfiguredOrUntrustedEndpoints()
    {
        using RSA rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=Offline workload CA", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        using RSA leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest("CN=offline-workload", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        leafRequest.CertificateExtensions.Add(names.Build());
        using var publicLeaf = leafRequest.Create(root, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(30), RandomNumberGenerator.GetBytes(16));
        using var leaf = publicLeaf.CopyWithPrivateKey(leafKey);
        int httpsRequests = 0;
        var https = WebApplication.CreateBuilder();
        https.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(leaf)));
        await using var secure = https.Build();
        secure.MapPost("/token", async (HttpContext context) =>
        {
            httpsRequests++;
            using var reader = new StreamReader(context.Request.Body);
            (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldContain("client_secret=offline-workload-secret");
            return Results.Json(new { access_token = "offline-access-token" });
        });
        await secure.StartAsync(TestContext.Current.CancellationToken);
        int httpRequests = 0;
        var plain = WebApplication.CreateBuilder();
        plain.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        await using var insecure = plain.Build();
        insecure.MapPost("/token", () => { httpRequests++; return Results.Json(new { access_token = "sent" }); });
        await insecure.StartAsync(TestContext.Current.CancellationToken);
        string caFile = Path.GetTempFileName(), otherFile = Path.GetTempFileName(), secretFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(caFile, root.ExportCertificatePem(), TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(secretFile, "offline-workload-secret", TestContext.Current.CancellationToken);
            using RSA otherKey = RSA.Create(2048);
            using var other = new CertificateRequest("CN=Untrusted workload CA", otherKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            await File.WriteAllTextAsync(otherFile, other.ExportCertificatePem(), TestContext.Current.CancellationToken);
            string httpsEndpoint = secure.Urls.Single() + "/token";
            var trusted = new InventoryWorkloadToken(Config(httpsEndpoint, caFile, secretFile));
            (await trusted.GetAsync(TestContext.Current.CancellationToken)).ShouldBe("offline-access-token");
            httpsRequests.ShouldBe(1);
            var http = new InventoryWorkloadToken(Config(insecure.Urls.Single() + "/token", caFile, secretFile));
            http.Configured.ShouldBeFalse();
            (await http.GetAsync(TestContext.Current.CancellationToken)).ShouldBeNull();
            httpRequests.ShouldBe(0);
            int before = httpsRequests;
            var untrusted = new InventoryWorkloadToken(Config(httpsEndpoint, otherFile, secretFile));
            await Should.ThrowAsync<HttpRequestException>(() => untrusted.GetAsync(TestContext.Current.CancellationToken));
            httpsRequests.ShouldBe(before);
        }
        finally
        {
            File.Delete(caFile);
            File.Delete(otherFile);
            File.Delete(secretFile);
        }
    }

    private static IConfiguration Config(string endpoint, string caFile, string secretFile) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Inventory:WorkloadTokenEndpoint"] = endpoint,
            ["Inventory:WorkloadClientId"] = "offline-workload",
            ["Inventory:WorkloadCredentialFile"] = secretFile,
            ["Inventory:WorkloadCaFile"] = caFile,
        }).Build();
}
