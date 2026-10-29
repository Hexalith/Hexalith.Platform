using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Bootstrap only for a verified owned tmpfs disposable server. Actual production custody/native code runs unchanged; private permission evidence is explicitly synthetic test input.</summary>
internal sealed class OpenBaoNativeCustodyFixture : IDisposable
{
    internal required HttpClient Http { get; init; }
    internal required OpenBaoErasureRootStore Native { get; init; }
    internal required ErasureRootCustody Custody { get; init; }
    internal required ErasureRootInstallation Installation { get; init; }
    internal required OpenBaoDisposableNativeHandler Transport { get; init; }
    internal static async Task<OpenBaoNativeCustodyFixture> CreateAsync(CancellationToken token)
    {
        var transport = await OpenBaoDisposableNativeHandler.OpenAsync(token);
        var http = new HttpClient(transport) { BaseAddress = new("https://disposable.invalid/"), Timeout = TimeSpan.FromSeconds(30) };
        string suffix = Guid.NewGuid().ToString("N");
        var installed = new ErasureRootInstallation("disposable-" + suffix, "owned-" + suffix, "transit-" + suffix, "heads-" + suffix,
            new Dictionary<string, string> { ["tenant-a"] = "epoch-a", ["tenant-b"] = "epoch-b" });
        async Task Write(string path, object payload)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path); request.Headers.Add("X-Vault-Token", Environment.GetEnvironmentVariable("ERC_NATIVE_DISPOSABLE_TOKEN"));
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var result = await http.SendAsync(request, token); if (!result.IsSuccessStatusCode) { throw new InvalidOperationException("Disposable native bootstrap refused."); }
        }
        await Write("v1/sys/mounts/" + installed.TransitMount, new { type = "transit" });
        await Write("v1/sys/mounts/" + installed.HeadMount, new { type = "kv", options = new { version = "2" } });
        await Write("v1/" + installed.HeadMount + "/config", new { max_versions = 1, cas_required = true });
        foreach (var pair in installed.NativeKeys)
        {
            await Write("v1/" + installed.TransitMount + "/keys/" + pair.Value, new { type = "aes256-gcm96", derived = true, exportable = false, allow_plaintext_backup = false, deletion_allowed = false });
            var head = new ErasureRootHead(pair.Key, installed.StoreId, installed.EnrollmentId, 1, [], [], [], [], []);
            string key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(pair.Key)));
            await Write("v1/" + installed.HeadMount + "/data/tenants/" + key, new { options = new { cas = 0 }, data = head });
        }
        var native = new OpenBaoErasureRootStore(http, installed, "env:ERC_NATIVE_DISPOSABLE_TOKEN");
        return new() { Http = http, Native = native, Installation = installed, Custody = new(native, installed, new ErasureRootAuthorizationFixture()), Transport = transport };
    }
    internal Task<CustodyKeyRegistration?> RootAsync(string id, string tenant = "tenant-a", CancellationToken token = default)
        => Custody.WrapAsync(new(tenant, id, PlatformKeyPurpose.InteractionRootDek, "explicit-alias", "root-v1", "lifecycle", Installation.StoreId, "custody"), "wrap-" + id, 0, token);
    internal static DeletionBatchConsumptionRequest Manifest(IEnumerable<CustodyKeyRegistration> originals, string batch = "native-batch")
    {
        var targets = originals.Select(root => new ProtectionTarget(root.Identity.TenantId, root.Identity.ObjectId, root.Identity.KeyAlias)).OrderBy(target => target.AgentInteractionId, StringComparer.Ordinal).ToArray();
        return new(new("fixture", "audience", targets[0].TenantId, "request", "seal", "accepted", 0, batch, DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key"),
            "synthetic-private-eligibility-only", 1, "dispatch", 2, targets);
    }
    public void Dispose() => Http.Dispose();
}
