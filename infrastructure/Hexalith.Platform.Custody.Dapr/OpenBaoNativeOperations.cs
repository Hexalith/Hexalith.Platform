using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hexalith.Platform.Custody.Dapr;

// Existing non-exportable transit keys only. No create, import, export, backup, restore, or key configuration API.
// OpenBao deletion is only native send completion. It never proves all-copy physical destruction.
internal sealed class OpenBaoNativeOperations(ComponentProfile profile, NativeInstallation installation, IAuthorityStore store, global::Dapr.Client.DaprClient dapr,HttpMessageHandler? transport=null,TimeSpan? operationTimeout=null,Hexalith.Platform.Secrets.Runtime.ISecretsCredentialBoundDaprInvoker? originalTransport=null) : IEpochOperations, IKeyAccess, IDisposable
{
    private readonly HttpClient _http = new(transport??new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = TimeSpan.FromSeconds(30) };
    private async Task<JsonElement> CallAsync(HttpMethod method, string path, object? body, CancellationToken token)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(operationTimeout is { } timeout && timeout>TimeSpan.Zero && timeout<=TimeSpan.FromSeconds(30)?timeout:TimeSpan.FromSeconds(30));
        token=deadline.Token;
        if (!Uri.TryCreate(installation.OpenBaoHttpsUrl, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https"
            || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment) || endpoint.AbsolutePath!="/"
            || installation.TransitMount.Length is 0 or >128 || installation.TransitMount.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '-' or '_')))
            throw new AuthorityUnavailableException("Enrolled HTTPS native endpoint required.");
        string credential = await Hexalith.Platform.Secrets.Runtime.DaprSecretsInvoker.ReadMountedBearerAsync("file:" + installation.CredentialFile, token)
            ?? throw new AuthorityUnavailableException("Scoped native credential missing.");
        if (credential.Length is 0 or > 8192 || credential.Any(c => c < 33 || c > 126)) throw new AuthorityUnavailableException("Scoped native credential missing.");
        using var request = new HttpRequestMessage(method, new Uri(endpoint, "/v1/" + path));
        request.Headers.Add("X-Vault-Token", credential);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).WaitAsync(token);
        response.EnsureSuccessStatusCode();
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return JsonSerializer.SerializeToElement(new { });
        using var input = await response.Content.ReadAsStreamAsync(token).WaitAsync(token);
        string? json = await Hexalith.Platform.Secrets.Runtime.DaprSecretsInvoker.ReadBoundedAsync(input, token).WaitAsync(token);
        if (json is null) throw new AuthorityUnavailableException("Bounded native response required.");
        return CustodyJson.Read<JsonElement>(System.Text.Encoding.UTF8.GetBytes(json));
    }
    private async Task<string> KeyAsync(string identity, CancellationToken token)
    {
        var binding=BindingProfile(identity);
        if (!identity.StartsWith("openbao-transit:", StringComparison.Ordinal)) throw new AuthorityUnavailableException("Native resource not enrolled.");
        string name = identity[16..];
        if (name.Length is 0 or > 128 || name.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new AuthorityUnavailableException("Native key path rejected.");
        var response = await CallAsync(HttpMethod.Get, installation.TransitMount + "/keys/" + name, null, token);
        bool Boolean(JsonElement value,string property,bool expected)=>value.TryGetProperty(property,out var item) && item.ValueKind==(expected?JsonValueKind.True:JsonValueKind.False);
        if(!response.TryGetProperty("data",out var data) || data.ValueKind!=JsonValueKind.Object) throw new AuthorityUnavailableException("Exact native metadata required.");
        if (!data.TryGetProperty("type",out var type) || type.ValueKind!=JsonValueKind.String || type.GetString() != "aes256-gcm96" || !Boolean(data,"exportable",false)
            || !Boolean(data,"allow_plaintext_backup",false) || !Boolean(data,"derived",binding.Derived) || !installation.NativeKeyMetadataSha256.TryGetValue(identity, out string? pin)
            || CustodyJson.Hash(CustodyJson.Bytes(data)) != pin) throw new AuthorityUnavailableException("Native identity/nonexportability substituted.");
        return name;
    }
    private OpenBaoTransitBindingProfile BindingProfile(string resource)=>installation.TransitBindingProfiles is { } bindings && bindings.TryGetValue(resource,out var binding)
        && binding.Schema=="OpenBaoTransit.AeadAssociatedData.V1"?binding:throw new AuthorityUnavailableException("Explicit supported native derivation/AEAD binding profile required; legacy context mappings cannot be guessed.");
    private Dictionary<string,string> Body(string resource,string tenant,string purpose,string identity,string field,string value)
    {
        var binding=BindingProfile(resource);
        // Canonical framed fields make tenant, purpose and epoch/version boundaries unambiguous. context derives a key; associated_data authenticates these fields.
        byte[] bound=CustodyJson.Bytes(new { schema="custody-native-aad-v1",authority=profile.Enrollment.AuthorityId,incarnation=profile.Enrollment.AuthorityIncarnation,tenant,purpose,identity });
        var result=new Dictionary<string,string> { [field]=value,["associated_data"]=Convert.ToBase64String(bound) };
        if(binding.Derived) result["context"]=Convert.ToBase64String(bound);
        return result;
    }
    private NativeEpoch Epoch(string epoch, string tenant) => profile.Epochs.SingleOrDefault(e => e.OpaqueId == epoch && e.TenantId == tenant)
        ?? throw new AuthorityUnavailableException("Original native epoch not enrolled.");
    public async Task<byte[]> EncryptOuterKeyAsync(string epoch, string tenant, byte[] key, CancellationToken token)
    {
        if (key.Length != 32) throw new AuthorityUnavailableException("Exact key length required.");
        var enrolled = Epoch(epoch, tenant); string name = await KeyAsync(enrolled.KeyArn, token);
        var result = await CallAsync(HttpMethod.Post, installation.TransitMount + "/encrypt/" + name,
            Body(enrolled.KeyArn,tenant,"custody-outer-key-v1",epoch,"plaintext",Convert.ToBase64String(key)), token);
        return System.Text.Encoding.UTF8.GetBytes(result.GetProperty("data").GetProperty("ciphertext").GetString()!);
    }
    public async Task<byte[]> ReencryptOuterKeyAsync(string source, string destination, string tenant, byte[] encryptedKey, CancellationToken token)
    {
        // Transit rewrap is same-key only. Cross-key migration uses explicitly admitted additional native plaintext holders.
        var admission = new NativeReencryptionAdmission(tenant, source, destination, CustodyJson.Hash(encryptedKey),
            installation.ProducerIncarnation, installation.TransportIncarnation, profile.ProfileDigest, "", "");
        string subject = CustodyJson.Hash(CustodyJson.Bytes(admission));
        var current = await store.ReadAsync(tenant, token);
        if (installation.NativeOriginalAuthority is null || string.IsNullOrWhiteSpace(installation.ProducerIncarnation) || string.IsNullOrWhiteSpace(installation.TransportIncarnation))
            throw new AuthorityUnavailableException("Exact independently admitted additional native plaintext holder originals required.");
        var originalAuthority = new Hexalith.Platform.Secrets.Runtime.IndependentDaprAuthority(originalTransport??new Hexalith.Platform.Secrets.Runtime.DaprSecretsInvoker(dapr), installation.NativeOriginalAuthority, TimeProvider.System);
        var statement = await originalAuthority.InvokeAsync<SignedStatement>("custody-native-observation-original", new { tenant, scope = "native-reencryption", operationId = subject,
            revision = current.Head.Revision, digest = current.Head.Digest }, token)
            ?? throw new AuthorityUnavailableException("Current independent native holder original missing.");
        var actual = CustodyJson.Read<NativeReencryptionAdmission>(CustodyJson.Bytes(statement.Payload));
        if (!(new StatementVerifier(profile, TimeProvider.System)).Verify(statement, "independent-observer", current, subject, TimeSpan.FromSeconds(30))
            || (actual with { ProducerHolderOriginalSha256 = "", NativeHolderOriginalSha256 = "" }) != admission
            || !Hexalith.Platform.Secrets.Runtime.CustodyBindingContract.Digest(actual.ProducerHolderOriginalSha256)
            || !Hexalith.Platform.Secrets.Runtime.CustodyBindingContract.Digest(actual.NativeHolderOriginalSha256))
            throw new AuthorityUnavailableException("Native holder admission is stale, substituted or incomplete.");
        var from = Epoch(source, tenant); _ = Epoch(destination, tenant);
        byte[] plaintext = await DecryptAsync(from.KeyArn, encryptedKey, tenant,"custody-outer-key-v1",source, token);
        try { return await EncryptOuterKeyAsync(destination, tenant, plaintext, token); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
        // This cleanup is not physical closure. Final publication still requires independent complete holder/native/restore evidence.
    }
    public async Task ScheduleSourceCutAsync(string epoch, string tenant, CancellationToken token)
    {
        var enrolled = Epoch(epoch, tenant); string name = await KeyAsync(enrolled.KeyArn, token);
        // Reviewed worker commits its native send intent first. Lost result must use exact original lookup, never resend.
        _ = await CallAsync(HttpMethod.Delete, installation.TransitMount + "/keys/" + name, null, token);
    }
    public async Task<byte[]> ReadTenantKekAsync(string tenant, string version, CancellationToken token)
    {
        var key = profile.Keks.SingleOrDefault(k => k.TenantId == tenant && k.Version == version)
            ?? throw new AuthorityUnavailableException("Original KEK mapping missing.");
        return await DecryptAsync(key.ProtectorArn, key.EncryptedKey, tenant,"custody-tenant-kek-v1",version, token);
    }
    public Task<byte[]> ReadOuterKeyAsync(StoredRegistration registration, CancellationToken token)
    {
        var epoch = Epoch(registration.Epoch, registration.Registration.Identity.TenantId);
        return DecryptAsync(epoch.KeyArn, registration.EncryptedOuterKey, epoch.TenantId,"custody-outer-key-v1",epoch.OpaqueId, token);
    }
    private async Task<byte[]> DecryptAsync(string resource, byte[] ciphertext, string tenant,string purpose,string identity, CancellationToken token)
    {
        string name = await KeyAsync(resource, token);
        var response = await CallAsync(HttpMethod.Post, installation.TransitMount + "/decrypt/" + name,
            Body(resource,tenant,purpose,identity,"ciphertext",System.Text.Encoding.UTF8.GetString(ciphertext)), token);
        byte[] key = Convert.FromBase64String(response.GetProperty("data").GetProperty("plaintext").GetString()!);
        if (key.Length == 32) return key;
        CryptographicOperations.ZeroMemory(key); throw new AuthorityUnavailableException("Native key length mismatch.");
    }
    public void Dispose() => _http.Dispose();
}
