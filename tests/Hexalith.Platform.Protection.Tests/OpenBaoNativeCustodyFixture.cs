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
    internal required IErasureRootStore Native { get; init; }
    internal required ErasureRootCustody Custody { get; init; }
    internal required ErasureRootInstallation Installation { get; init; }
    internal required OpenBaoDisposableNativeHandler Transport { get; init; }
    internal required string RuntimeCredentialReference { get; init; }
    internal required IReadOnlyDictionary<string, string> RuntimeCredentialReferences { get; init; }
    internal static async Task<OpenBaoNativeCustodyFixture> CreateAsync(CancellationToken token)
    {
        var transport = await OpenBaoDisposableNativeHandler.OpenAsync(token);
        var http = new HttpClient(transport) { BaseAddress = new("https://disposable.invalid/"), Timeout = TimeSpan.FromSeconds(30) };
        string suffix = Guid.NewGuid().ToString("N");
        var installed = new ErasureRootInstallation("disposable-" + suffix, "owned-" + suffix, "transit-" + suffix, "heads-" + suffix,
            new Dictionary<string, string> { ["tenant-a"] = "epoch-a", ["tenant-b"] = "epoch-b" });
        async Task<JsonElement> Write(string path, object payload)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path); request.Headers.Add("X-Vault-Token", Environment.GetEnvironmentVariable("ERC_NATIVE_DISPOSABLE_TOKEN"));
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var result = await http.SendAsync(request, token); if (!result.IsSuccessStatusCode) { throw new InvalidOperationException("Disposable native bootstrap refused."); }
            string json = await result.Content.ReadAsStringAsync(token);
            return string.IsNullOrWhiteSpace(json) ? default : JsonDocument.Parse(json).RootElement.Clone();
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
        string directory = Path.Combine(Path.GetTempPath(), "erc-native-renderer-" + suffix); Directory.CreateDirectory(directory);
        var references = new Dictionary<string, string>(StringComparer.Ordinal);
        var stores = new Dictionary<string, OpenBaoErasureRootStore>(StringComparer.Ordinal);
        try
        {
            await OpenBaoShippedPolicyRenderer.ExtractAsync(directory, token);
            var scopes = installed.NativeKeys.Keys.ToDictionary(tenant => tenant, tenant => CustodyKeyLifecycleActor.GetActorId(tenant) + "|custody-key-lifecycle-candidate-v1");
            var rendered = await OpenBaoShippedPolicyRenderer.RenderAsync(directory, installed, scopes, token);
            string fragment = await File.ReadAllTextAsync(Path.Combine(directory, "custody-policy.hcl"), token);
            foreach (var pair in rendered)
            {
                string policy = pair.Value.GetProperty("policy").GetString()!, policyName = pair.Value.GetProperty("name").GetString()!;
                await Write("v1/sys/policies/acl/" + policyName, new { policy });
                var tokenResponse = await Write("v1/auth/token/create", new { policies = new[] { policyName }, no_default_policy = true, ttl = "1h", renewable = false });
                if (!tokenResponse.GetProperty("auth").GetProperty("policies").EnumerateArray().Select(value => value.GetString()).SequenceEqual([policyName]))
                { throw new InvalidOperationException("Exclusive shipped tenant runtime policy token required."); }
                string reference = "env:ERC_NATIVE_RUNTIME_" + suffix.ToUpperInvariant() + "_" + pair.Key.Replace('-', '_').ToUpperInvariant();
                references.Add(pair.Key, reference);
                Environment.SetEnvironmentVariable(reference[4..], tokenResponse.GetProperty("auth").GetProperty("client_token").GetString());
                stores.Add(pair.Key, new OpenBaoErasureRootStore(http, installed, reference));
                if (Environment.GetEnvironmentVariable("ERC_NATIVE_DIAGNOSTIC_DIRECTORY") is { } diagnostic && Path.IsPathFullyQualified(diagnostic)
                    && diagnostic.StartsWith("/tmp/erc-native-diagnostic-", StringComparison.Ordinal) && new DirectoryInfo(diagnostic).LinkTarget is null)
                {
                    using var output = new FileStream(Path.Combine(diagnostic, "policy-" + suffix + "-" + pair.Key + ".json"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    JsonSerializer.Serialize(output, new { shippedPolicySha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(fragment))),
                        renderedPolicySha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(policy))), transitMount = installed.TransitMount,
                        headMount = installed.HeadMount, runtimePolicyName = policyName, tenantId = pair.Key, scope = scopes[pair.Key], rootUsedOnlyForBootstrap = true });
                }
            }
        }
        catch { foreach (string reference in references.Values) { Environment.SetEnvironmentVariable(reference[4..], null); } http.Dispose(); throw; }
        finally { Directory.Delete(directory, true); }
        var native = new OpenBaoTenantNativeStore(stores);
        return new() { Http = http, Native = native, Installation = installed, Custody = new(native, installed, new ErasureRootAuthorizationFixture()), Transport = transport,
            RuntimeCredentialReference = references["tenant-a"], RuntimeCredentialReferences = references };
    }
    internal Task<CustodyKeyRegistration?> RootAsync(string id, string tenant = "tenant-a", CancellationToken token = default)
        => Custody.WrapAsync(new(tenant, id, PlatformKeyPurpose.InteractionRootDek, "explicit-alias", "root-v1", "lifecycle", Installation.StoreId, "custody"), "wrap-" + id, 0, token);
    internal static DeletionBatchConsumptionRequest Manifest(IEnumerable<CustodyKeyRegistration> originals, string batch = "native-batch")
    {
        var targets = originals.Select(root => new ProtectionTarget(root.Identity.TenantId, root.Identity.ObjectId, root.Identity.KeyAlias)).OrderBy(target => target.AgentInteractionId, StringComparer.Ordinal).ToArray();
        return new(new("fixture", "audience", targets[0].TenantId, "request", "seal", "accepted", 0, batch, DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "guard", 1, 1, 1, "key"),
            "synthetic-private-eligibility-only", 1, "dispatch", 2, targets);
    }
    internal async Task AssertForbiddenPolicyOperationsAsync(CancellationToken token)
    {
        string transit = "v1/" + Installation.TransitMount, heads = "v1/" + Installation.HeadMount;
        string archive = heads + "/data/archives/" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("tenant-a"))) + "/" + new string('A', 64);
        async Task<HttpResponseMessage> Call(HttpMethod method, string path, object? payload)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Add("X-Vault-Token", Environment.GetEnvironmentVariable(RuntimeCredentialReference[4..]));
            if (payload is not null) { request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"); }
            return await Http.SendAsync(request, token);
        }
        using (var created = await Call(HttpMethod.Post, archive, new { options = new { cas = 0 }, data = new { fixture = "original" } }))
        { if (!created.IsSuccessStatusCode) { throw new InvalidOperationException("Shipped archive create policy refused."); } }
        var forbidden = new (HttpMethod Method, string Path, object? Payload)[]
        {
            (HttpMethod.Post, archive, new { options = new { cas = 1 }, data = new { fixture = "overwrite" } }),
            (HttpMethod.Post, heads + "/metadata/archives/forbidden", new { max_versions = 2 }),
            (HttpMethod.Post, transit + "/keys/epoch-a/config", new { exportable = true }),
            (HttpMethod.Post, transit + "/keys/epoch-a/config", new { deletion_allowed = true }),
            (HttpMethod.Get, transit + "/export/encryption-key/epoch-a", null),
            (HttpMethod.Get, transit + "/backup/epoch-a", null),
            (HttpMethod.Get, "v1/sys/storage/raft/snapshot", null),
            (HttpMethod.Delete, transit + "/keys/epoch-a", null),
        };
        foreach (var call in forbidden)
        {
            using var result = await Call(call.Method, call.Path, call.Payload);
            if (result.StatusCode != System.Net.HttpStatusCode.Forbidden) { throw new InvalidOperationException("Shipped policy did not deny the exact forbidden capability."); }
        }
        foreach (var pair in RuntimeCredentialReferences)
        {
            string other = pair.Key == "tenant-a" ? "tenant-b" : "tenant-a";
            foreach (string path in new[] { heads + "/data/tenants/" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(other))),
                heads + "/data/archives/" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(other))) + "/" + new string('A', 64),
                transit + "/keys/" + Installation.NativeKeys[other] })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, path);
                request.Headers.Add("X-Vault-Token", Environment.GetEnvironmentVariable(pair.Value[4..]));
                using var denied = await Http.SendAsync(request, token);
                if (denied.StatusCode != System.Net.HttpStatusCode.Forbidden) { throw new InvalidOperationException("Rendered tenant policy crossed the enrolled tenant boundary."); }
            }
        }
    }
    public void Dispose()
    {
        if (Environment.GetEnvironmentVariable("ERC_NATIVE_DIAGNOSTIC_DIRECTORY") is { } diagnostic && Path.IsPathFullyQualified(diagnostic)
            && diagnostic.StartsWith("/tmp/erc-native-diagnostic-", StringComparison.Ordinal) && new DirectoryInfo(diagnostic).LinkTarget is null)
        {
            using var output = new FileStream(Path.Combine(diagnostic, "fixture-" + Installation.TransitMount + ".json"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(output, new { transitMount = Installation.TransitMount, headMount = Installation.HeadMount, stages = Transport.PublicStages.TakeLast(30).ToArray() });
        }
        foreach (string reference in RuntimeCredentialReferences.Values) { Environment.SetEnvironmentVariable(reference[4..], null); }
        Http.Dispose();
    }
}
