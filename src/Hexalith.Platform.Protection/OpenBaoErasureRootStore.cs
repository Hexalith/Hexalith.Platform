using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.Platform.Protection;

/// <summary>Native OpenBao 2.6.2 transit and KV v2 CAS transport for the dedicated A1 installation. Contains no provisioning, snapshot, export or automatic mutation retry.</summary>
public sealed class OpenBaoErasureRootStore : IErasureRootStore
{
    private const int MaximumResponseBytes = 32 * 1024 * 1024;
    internal const int MaximumHeadBytes = 30 * 1024 * 1024;
    private readonly HttpClient _http;
    private readonly ErasureRootInstallation _installation;
    private readonly string _credentialEnvironmentVariable;

    /// <summary>Creates an enrolled native transport. Cleartext transport is accepted only for explicitly requested loopback disposable tests.</summary>
    /// <param name="http">Dedicated client with a bounded timeout and fixed base address.</param>
    /// <param name="installation">Installed reference-only configuration.</param>
    /// <param name="credentialReference">Environment reference of the dedicated native credential, never its value.</param>
    /// <param name="allowDisposableLoopback">Allows HTTP only at a loopback address for disposable component integration.</param>
    public OpenBaoErasureRootStore(HttpClient http, ErasureRootInstallation installation, string credentialReference,
        bool allowDisposableLoopback = false)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(installation);
        if (http.BaseAddress is not { } address || !string.IsNullOrEmpty(address.UserInfo)
            || !string.IsNullOrEmpty(address.Query) || !string.IsNullOrEmpty(address.Fragment)
            || address.AbsolutePath != "/" || address.Scheme != "https" && !(allowDisposableLoopback && address.Scheme == "http" && address.IsLoopback)
            || http.Timeout <= TimeSpan.Zero || http.Timeout > TimeSpan.FromSeconds(30)
            || !Regex.IsMatch(credentialReference, "^env:[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)
            || installation.DurabilityPolicy != ErasureRootDurabilityPolicy.None || installation.NativeKeys.Count == 0)
        {
            throw new ArgumentException("Complete dedicated ERC transport enrollment required.");
        }
        Segment(installation.HeadMount);
        Segment(installation.TransitMount);
        foreach (var entry in installation.NativeKeys)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Key);
            Segment(entry.Value);
        }
        if (installation.NativeKeys.Values.Distinct(StringComparer.Ordinal).Count() != installation.NativeKeys.Count)
        {
            throw new ArgumentException("Each tenant requires a distinct native epoch key.");
        }
        _http = http;
        _installation = installation;
        _credentialEnvironmentVariable = credentialReference[4..];
    }

    /// <inheritdoc />
    public async Task<ErasureRootHeadSnapshot?> ReadHeadAsync(string tenantId, CancellationToken cancellationToken)
    {
        Key(tenantId);
        byte[]? bytes = await SendAsync(HttpMethod.Get, HeadPath(tenantId), null, true, cancellationToken).ConfigureAwait(false);
        if (bytes is null) { return null; }
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var data = document.RootElement.GetProperty("data");
            long version = data.GetProperty("metadata").GetProperty("version").GetInt64();
            if (version <= 0) { throw new InvalidOperationException("Invalid native head version."); }
            return new(version, JsonSerializer.SerializeToUtf8Bytes(data.GetProperty("data")));
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    /// <inheritdoc />
    public async Task<bool> CompareExchangeHeadAsync(string tenantId, long version, byte[] head, CancellationToken cancellationToken)
    {
        Key(tenantId);
        if (version <= 0 || head.Length is <= 0 or > MaximumHeadBytes) { throw new ArgumentException("Existing bounded head required."); }
        using var document = JsonDocument.Parse(head);
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(new { options = new { cas = version }, data = document.RootElement });
        try
        {
            byte[]? response = await SendAsync(HttpMethod.Post, HeadPath(tenantId), body, false, cancellationToken, true).ConfigureAwait(false);
            if (response is null) { return false; }
            CryptographicOperations.ZeroMemory(response);
            return true;
        }
        finally { CryptographicOperations.ZeroMemory(body); }
    }

    /// <inheritdoc />
    public async Task<byte[]?> ReadArchiveAsync(string tenantId, string archiveId, CancellationToken cancellationToken)
    {
        byte[]? bytes = await SendAsync(HttpMethod.Get, ArchivePath(tenantId, archiveId), null, true, cancellationToken).ConfigureAwait(false);
        if (bytes is null) { return null; }
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var data = document.RootElement.GetProperty("data");
            if (data.GetProperty("metadata").GetProperty("version").GetInt64() != 1) { throw new InvalidOperationException("Immutable ERC archive version required."); }
            return data.GetProperty("data").GetProperty("bytes").GetBytesFromBase64();
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    /// <inheritdoc />
    public async Task<bool> CreateArchiveAsync(string tenantId, string archiveId, byte[] bytes, CancellationToken cancellationToken)
    {
        if (bytes.Length is <= 0 or > 12 * 1024 * 1024) { throw new ArgumentException("Bounded immutable archive required."); }
        byte[]? original = await ReadArchiveAsync(tenantId, archiveId, cancellationToken).ConfigureAwait(false);
        if (original is not null) { return original.AsSpan().SequenceEqual(bytes); }
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(new { options = new { cas = 0 }, data = new { bytes = Convert.ToBase64String(bytes) } });
        byte[]? response = await SendAsync(HttpMethod.Post, ArchivePath(tenantId, archiveId), body, false, cancellationToken, true).ConfigureAwait(false);
        if (response is not null) { return true; }
        original = await ReadArchiveAsync(tenantId, archiveId, cancellationToken).ConfigureAwait(false);
        return original is not null && original.AsSpan().SequenceEqual(bytes);
    }
    private string ArchivePath(string tenant, string archiveId)
    {
        Key(tenant);
        if (archiveId is not { Length: 64 } || archiveId.Any(value => !char.IsAsciiHexDigit(value))) { throw new ArgumentException("Exact archive identity required."); }
        return "v1/" + _installation.HeadMount + "/data/archives/" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(tenant))) + "/" + archiveId;
    }

    /// <inheritdoc />
    public async Task<string> EncryptAsync(string tenantId, int epoch, byte[] root, string context, CancellationToken cancellationToken)
    {
        if (root.Length != 32 || epoch <= 0) { throw new ArgumentException("Exact AES-256 root and epoch required."); }
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("key_version", epoch);
            writer.WriteString("context", context);
            writer.WriteBase64String("plaintext", root);
            writer.WriteEndObject();
        }
        byte[] body = buffer.ToArray();
        try
        {
            byte[] response = (await SendAsync(HttpMethod.Post, TransitPath(tenantId, "encrypt"), body, false, cancellationToken).ConfigureAwait(false))!;
            try
            {
                using var document = JsonDocument.Parse(response);
                string ciphertext = document.RootElement.GetProperty("data").GetProperty("ciphertext").GetString()!;
                if (!ciphertext.StartsWith("vault:v" + epoch.ToString(CultureInfo.InvariantCulture) + ":", StringComparison.Ordinal))
                { throw new InvalidOperationException("Native encryption epoch mismatch."); }
                return ciphertext;
            }
            finally { CryptographicOperations.ZeroMemory(response); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(body);
            CryptographicOperations.ZeroMemory(buffer.GetBuffer());
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> DecryptAsync(string tenantId, ErasureRootCiphertext value, CancellationToken cancellationToken)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(new { ciphertext = value.Ciphertext, context = value.Context });
        byte[] response = (await SendAsync(HttpMethod.Post, TransitPath(tenantId, "decrypt"), body, false, cancellationToken).ConfigureAwait(false))!;
        try
        {
            using var document = JsonDocument.Parse(response);
            byte[] root = document.RootElement.GetProperty("data").GetProperty("plaintext").GetBytesFromBase64();
            if (root.Length == 32) { return root; }
            CryptographicOperations.ZeroMemory(root);
            throw new InvalidOperationException("Native root length mismatch.");
        }
        finally { CryptographicOperations.ZeroMemory(response); }
    }

    /// <inheritdoc />
    public async Task<int> ReadEpochAsync(string tenantId, CancellationToken cancellationToken)
    {
        using var metadata = await ReadKeyAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return metadata.RootElement.GetProperty("data").GetProperty("latest_version").GetInt32();
    }

    /// <inheritdoc />
    public async Task RotateAsync(string tenantId, CancellationToken cancellationToken)
    {
        byte[] response = (await SendAsync(HttpMethod.Post, KeyPath(tenantId) + "/rotate", "{}"u8.ToArray(), false, cancellationToken).ConfigureAwait(false))!;
        CryptographicOperations.ZeroMemory(response);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> RewrapAsync(string tenantId, int epoch, IReadOnlyList<ErasureRootCiphertext> values, CancellationToken cancellationToken)
    {
        if (epoch <= 0 || values.Count > 1000) { throw new ArgumentException("Bounded native rewrap required."); }
        if (values.Count == 0) { return Array.Empty<string>(); }
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(new { key_version = epoch,
            batch_input = values.Select(value => new { ciphertext = value.Ciphertext, context = value.Context, key_version = epoch }).ToArray() });
        byte[] response = (await SendAsync(HttpMethod.Post, TransitPath(tenantId, "rewrap"), body, false, cancellationToken).ConfigureAwait(false))!;
        using var document = JsonDocument.Parse(response);
        var results = document.RootElement.GetProperty("data").GetProperty("batch_results");
        if (results.GetArrayLength() != values.Count) { throw new InvalidOperationException("Incomplete native rewrap."); }
        string prefix = "vault:v" + epoch.ToString(CultureInfo.InvariantCulture) + ":";
        return results.EnumerateArray().Select(result =>
        {
            if (result.TryGetProperty("error", out var error) && !string.IsNullOrEmpty(error.GetString()))
            { throw new InvalidOperationException("Native survivor rewrap refused."); }
            string value = result.GetProperty("ciphertext").GetString()!;
            return value.StartsWith(prefix, StringComparison.Ordinal) ? value : throw new InvalidOperationException("Survivor epoch mismatch.");
        }).ToArray();
    }

    /// <inheritdoc />
    public async Task TrimAsync(string tenantId, int epoch, CancellationToken cancellationToken)
    {
        if (epoch <= 1) { throw new ArgumentException("Successor epoch required."); }
        byte[] config = JsonSerializer.SerializeToUtf8Bytes(new { min_decryption_version = epoch, min_encryption_version = epoch });
        _ = await SendAsync(HttpMethod.Post, KeyPath(tenantId) + "/config", config, false, cancellationToken).ConfigureAwait(false);
        byte[] trim = JsonSerializer.SerializeToUtf8Bytes(new { min_available_version = epoch });
        _ = await SendAsync(HttpMethod.Post, KeyPath(tenantId) + "/trim", trim, false, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> VerifyTrimAsync(string tenantId, int epoch, CancellationToken cancellationToken)
    {
        using var metadata = await ReadKeyAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var data = metadata.RootElement.GetProperty("data");
        return data.GetProperty("min_available_version").GetInt32() == epoch
            && data.GetProperty("min_decryption_version").GetInt32() == epoch
            && data.GetProperty("min_encryption_version").GetInt32() == epoch
            && data.GetProperty("latest_version").GetInt32() >= epoch
            && data.GetProperty("keys").EnumerateObject().All(entry => int.Parse(entry.Name, CultureInfo.InvariantCulture) >= epoch);
    }

    private async Task<JsonDocument> ReadKeyAsync(string tenantId, CancellationToken token)
    {
        byte[] health = (await SendAsync(HttpMethod.Get, "v1/sys/health", null, false, token).ConfigureAwait(false))!;
        using (var status = JsonDocument.Parse(health))
        {
            if (status.RootElement.GetProperty("version").GetString() != "2.6.2"
                || !status.RootElement.GetProperty("initialized").GetBoolean() || status.RootElement.GetProperty("sealed").GetBoolean())
            { throw new InvalidOperationException("Exact unsealed OpenBao 2.6.2 ERC required."); }
        }
        byte[] response = (await SendAsync(HttpMethod.Get, KeyPath(tenantId), null, false, token).ConfigureAwait(false))!;
        var document = JsonDocument.Parse(response);
        var data = document.RootElement.GetProperty("data");
        if (data.GetProperty("type").GetString() != "aes256-gcm96" || !data.GetProperty("derived").GetBoolean()
            || data.GetProperty("exportable").GetBoolean() || data.GetProperty("allow_plaintext_backup").GetBoolean()
            || data.GetProperty("deletion_allowed").GetBoolean())
        {
            document.Dispose();
            throw new InvalidOperationException("Native key does not match the dedicated ERC enrollment.");
        }
        return document;
    }

    private string Key(string tenant)
        => _installation.NativeKeys.TryGetValue(tenant, out var key) ? key : throw new ArgumentException("Unenrolled tenant.");
    private string HeadPath(string tenant) => "v1/" + _installation.HeadMount + "/data/tenants/" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(tenant)));
    private string KeyPath(string tenant) => "v1/" + _installation.TransitMount + "/keys/" + Key(tenant);
    private string TransitPath(string tenant, string operation) => "v1/" + _installation.TransitMount + "/" + operation + "/" + Key(tenant);
    private static void Segment(string value)
    {
        if (!Regex.IsMatch(value, "^[a-zA-Z0-9_-]{1,128}$", RegexOptions.CultureInvariant))
        { throw new ArgumentException("Exact native mount/key segment required."); }
    }

    private async Task<byte[]?> SendAsync(HttpMethod method, string path, byte[]? body, bool allowMissing, CancellationToken token, bool allowCasConflict = false)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(_http.Timeout);
        token = deadline.Token;
        string? credential = Environment.GetEnvironmentVariable(_credentialEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(credential)) { throw new InvalidOperationException("DependencyNotAvailable: ERC credential reference."); }
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Vault-Token", credential);
        request.Headers.Add("X-Vault-Request", "true");
        if (body is not null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        if (allowMissing && response.StatusCode == HttpStatusCode.NotFound) { return null; }
        if (response.Content.Headers.ContentLength > MaximumResponseBytes) { throw new InvalidOperationException("Oversized native ERC response."); }
        byte[] rented = ArrayPool<byte>.Shared.Rent(8192);
        using var output = new MemoryStream();
        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            int count;
            while ((count = await stream.ReadAsync(rented.AsMemory(0, 8192), token).ConfigureAwait(false)) > 0)
            {
                if (output.Length + count > MaximumResponseBytes) { throw new InvalidOperationException("Oversized native ERC response."); }
                output.Write(rented, 0, count);
            }
            if (allowCasConflict && response.StatusCode == HttpStatusCode.BadRequest)
            {
                using var error = JsonDocument.Parse(output.GetBuffer().AsMemory(0, checked((int)output.Length)));
                if (error.RootElement.TryGetProperty("errors", out var errors)
                    && errors.EnumerateArray().Any(value => value.GetString() == "check-and-set parameter did not match the current version"))
                { return null; }
            }
            if (!response.IsSuccessStatusCode) { throw new InvalidOperationException("Native ERC request refused or unavailable."); }
            return output.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rented);
            ArrayPool<byte>.Shared.Return(rented);
            CryptographicOperations.ZeroMemory(output.GetBuffer());
        }
    }
}
