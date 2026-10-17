using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hexalith.Tenants.Contracts.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Hexalith.Platform.Secrets.Inventory.Tests;

public sealed class InventorySourceTransportTests
{
    private static readonly SemaphoreSlim ChannelLock = new(1, 1);

    [Fact]
    public async Task ActiveProviderCallbackKeepsTheCertificateHostAndConnectsToTheLeader()
    {
        FieldInfo field = typeof(InventorySources).GetField("OpenProviderSocket", BindingFlags.NonPublic | BindingFlags.Static)!;
        var original = (Func<string, int, CancellationToken, ValueTask<Socket>>)field.GetValue(null)!;
        string? host = null;
        int port = 0;
        field.SetValue(null, new Func<string, int, CancellationToken, ValueTask<Socket>>((seenHost, seenPort, _) =>
        {
            host = seenHost;
            port = seenPort;
            throw new IOException("offline-socket");
        }));
        try
        {
            var connect = (Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>>)Delegate.CreateDelegate(
                typeof(Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>>),
                typeof(InventorySources).GetMethod("ConnectActiveProviderAsync", BindingFlags.NonPublic | BindingFlags.Static)!);
            SocketsHttpConnectionContext Context(string host) => (SocketsHttpConnectionContext)Activator.CreateInstance(typeof(SocketsHttpConnectionContext), BindingFlags.Instance | BindingFlags.NonPublic, null, [new DnsEndPoint(host, 8200), new HttpRequestMessage()], null)!;
            await Should.ThrowAsync<HttpRequestException>(() => connect(Context("other.example"), CancellationToken.None).AsTask());
            await Should.ThrowAsync<IOException>(() => connect(Context("hexalith-keys.openbao.svc.cluster.local"), CancellationToken.None).AsTask());
            host.ShouldBe("hexalith-keys-active.openbao.svc");
            port.ShouldBe(8200);
        }
        finally { field.SetValue(null, original); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeEnrollmentFollowsBoundedPagesAndRejectsChangedHead(bool changeHead)
    {
        string tenant = InventoryBoundaryTests.Scope.TenantId;
        var page = InventoryAuthorityTests.Page(new TenantCreated(tenant, "Offline", null, DateTimeOffset.UtcNow));
        for (int i = 0; i < 100; i++) InventoryAuthorityTests.AddEvent(page, new TenantDisabled(tenant, DateTimeOffset.UtcNow));
        var cursors = new List<long>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        await using var app = builder.Build();
        app.MapPost("/token", () => Results.Json(new { access_token = "offline-native-bearer" }));
        app.MapPost("/v1.0/invoke/eventstore/method/api/v1/streams/read", async (HttpContext context) =>
        {
            context.Request.Headers.Authorization.ToString().ShouldBe("Bearer offline-native-bearer");
            context.Request.Headers["dapr-api-token"].ToString().ShouldBe("offline-exact-channel-token");
            JsonObject body = (await JsonNode.ParseAsync(context.Request.Body))!.AsObject();
            long from = body["fromSequence"]!.GetValue<long>();
            cursors.Add(from);
            var events = new JsonArray(page["events"]!.AsArray().Skip((int)from).Take(100).Select(item => item!.DeepClone()).ToArray());
            long last = from + events.Count;
            return Results.Json(new JsonObject { ["tenant"] = "system", ["domain"] = "tenants", ["aggregateId"] = tenant, ["events"] = events,
                ["metadata"] = new JsonObject { ["fromSequence"] = from, ["toSequence"] = null, ["lastSequenceReturned"] = last,
                    ["latestSequence"] = changeHead && from > 0 ? 102 : 101, ["eventCount"] = events.Count, ["isTruncated"] = last < 101, ["nextContinuationToken"] = null } });
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, "offline-registry-credential", TestContext.Current.CancellationToken);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Inventory:RegistryCredentialFile"] = file, ["Inventory:RegistryTokenEndpoint"] = app.Urls.Single() + "/token" }).Build();
            using var native = new HttpClient(new OfflineRouteHandler(new Uri(app.Urls.Single())));
            using var identity = new HttpClient();
            using var provider = new HttpClient();
            using var source = new InventorySources(config, native, identity, provider);
            await ChannelLock.WaitAsync(TestContext.Current.CancellationToken);
            NativeEnrollment? result;
            string? previous = Environment.GetEnvironmentVariable("DAPR_API_TOKEN");
            Environment.SetEnvironmentVariable("DAPR_API_TOKEN", "offline-exact-channel-token");
            try { result = await source.EnrollmentAsync(tenant, TestContext.Current.CancellationToken); }
            finally
            {
                Environment.SetEnvironmentVariable("DAPR_API_TOKEN", previous);
                ChannelLock.Release();
            }
            if (changeHead) result.ShouldBeNull(); else { result.ShouldNotBeNull(); result.Active.ShouldBeFalse(); cursors.ShouldBe([0L, 100L, 0L, 100L]); }
        }
        finally { File.Delete(file); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ProductionMetadataRouteUsesMountedCredentialAndValidatesTls(bool denied, bool untrusted)
    {
        using RSA rootKey = RSA.Create(2048);
        var rootRequest = new CertificateRequest("CN=Offline inventory test CA", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        using var root = rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        using RSA leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest("CN=hexalith-keys.openbao.svc.cluster.local", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("hexalith-keys.openbao.svc.cluster.local");
        leafRequest.CertificateExtensions.Add(names.Build());
        using var publicLeaf = leafRequest.Create(root, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(30), RandomNumberGenerator.GetBytes(16));
        using var leaf = publicLeaf.CopyWithPrivateKey(leafKey);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(leaf)));
        await using var app = builder.Build();
        int requests = 0;
        app.MapGet("/v1/ext-secrets-1-isolated/metadata/tenant/key", (HttpContext context) =>
        { requests++; context.Request.Headers["X-Vault-Token"].ToString().ShouldBe(denied ? "offline-denied" : "offline-provider-token"); return denied ? Results.StatusCode(403) : Results.Json(new { data = new { current_version = 1 } }); });
        await app.StartAsync(TestContext.Current.CancellationToken);
        string caFile = Path.GetTempFileName(), credentialFile = Path.GetTempFileName();
        try
        {
            using RSA otherKey = RSA.Create(2048);
            using var other = new CertificateRequest("CN=Untrusted offline CA", otherKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            await File.WriteAllTextAsync(caFile, (untrusted ? other : root).ExportCertificatePem(), TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(credentialFile, denied ? "offline-denied" : "offline-provider-token", TestContext.Current.CancellationToken);
            int port = new Uri(app.Urls.Single()).Port;
            async ValueTask<Stream> Connect(SocketsHttpConnectionContext context, CancellationToken token)
            {
                context.DnsEndPoint.Host.ShouldBe("hexalith-keys.openbao.svc.cluster.local");
                context.DnsEndPoint.Port.ShouldBe(8200);
                context.InitialRequestMessage.Headers.ConnectionClose.ShouldBe(true);
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(IPAddress.Loopback, port, token);
                return new NetworkStream(socket, true);
            }
            using var provider = InventorySources.VerifiedClient(caFile, Connect);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Inventory:ProviderCredentialFile"] = credentialFile }).Build();
            using var source = new InventorySources(config, new HttpClient(), new HttpClient(), provider);
            if (untrusted) { await Should.ThrowAsync<HttpRequestException>(() => source.MetadataAsync("ext-secrets-1-isolated/tenant/key", TestContext.Current.CancellationToken)); requests.ShouldBe(0); }
            else { var metadata = await source.MetadataAsync("ext-secrets-1-isolated/tenant/key", TestContext.Current.CancellationToken); if (denied) metadata.ShouldBeNull(); else metadata!["current_version"]!.GetValue<int>().ShouldBe(1); requests.ShouldBe(1); }
        }
        finally { File.Delete(caFile); File.Delete(credentialFile); }
    }
}
