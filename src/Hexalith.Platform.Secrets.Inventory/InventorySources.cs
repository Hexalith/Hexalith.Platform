using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Server.Aggregates;

namespace Hexalith.Platform.Secrets.Inventory;

/// <summary>Reads the native EventStore source and OpenBao metadata using distinct restricted credentials. No secret data route exists.</summary>
public sealed class InventorySources : IInventorySources, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _native;
    private readonly HttpClient _provider;
    private readonly HttpClient _identity;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Creates verified transports from installed public CA references and mounted credential paths.</summary>
    public InventorySources(IConfiguration configuration)
    {
        _configuration = configuration;
        _native = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
        _provider = VerifiedClient(configuration["Inventory:ProviderCaFile"]!);
        _identity = VerifiedClient(configuration["Inventory:RegistryCaFile"]!);
    }

    /// <summary>Uses caller-owned transports for isolated transport verification.</summary>
    public InventorySources(IConfiguration configuration, HttpClient native, HttpClient identity, HttpClient provider)
    { _configuration = configuration; _native = native; _identity = identity; _provider = provider; }

    /// <inheritdoc/>
    public async Task<NativeEnrollment?> EnrollmentAsync(string tenantId, CancellationToken cancellationToken)
    {
        // Every read obtains a fresh scoped credential and native source; no enrollment or role cache exists.
        string secret = await File.ReadAllTextAsync(_configuration["Inventory:RegistryCredentialFile"]!, cancellationToken).ConfigureAwait(false);
        using var tokenBody = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials", ["client_id"] = "ext-secrets-1-inventory", ["client_secret"] = secret.Trim(),
        });
        using HttpResponseMessage tokenResponse = await _identity.PostAsync(_configuration["Inventory:RegistryTokenEndpoint"], tokenBody, cancellationToken).ConfigureAwait(false);
        JsonObject? tokenDocument = await ReadAsync(tokenResponse, cancellationToken).ConfigureAwait(false);
        if (tokenDocument?["access_token"]?.GetValue<string>() is not { } token) { return null; }
        JsonObject? first = await ReadStreamAsync(tenantId, token, cancellationToken).ConfigureAwait(false);
        JsonObject? second = await ReadStreamAsync(tenantId, token, cancellationToken).ConfigureAwait(false);
        return JsonNode.DeepEquals(first, second) ? Fold(first, tenantId) : null;
    }

    /// <inheritdoc/>
    public async Task<JsonObject?> MetadataAsync(string secretReference, CancellationToken cancellationToken)
    {
        const string mount = "ext-secrets-1-isolated/";
        if (!secretReference.StartsWith(mount, StringComparison.Ordinal)) { return null; }
        string[] segments = secretReference[mount.Length..].Split('/');
        if (segments.Length == 0 || segments.Any(part => part.Length == 0 || part is "." or ".." || part.Any(char.IsControl))) { return null; }
        string path = string.Join('/', segments.Select(Uri.EscapeDataString));
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://hexalith-keys.openbao.svc.cluster.local:8200/v1/" + mount + "metadata/" + path);
        string credential = await File.ReadAllTextAsync(_configuration["Inventory:ProviderCredentialFile"]!, cancellationToken).ConfigureAwait(false);
        request.Headers.Add("X-Vault-Token", credential.Trim());
        using HttpResponseMessage response = await _provider.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        return (await ReadAsync(response, cancellationToken).ConfigureAwait(false))?["data"] as JsonObject;
    }

    private async Task<JsonObject?> ReadStreamAsync(string tenantId, string token, CancellationToken cancellationToken)
    {
        long cursor = 0;
        long? head = null;
        JsonObject? complete = null;
        var events = new JsonArray();
        // The pinned native API continues with exclusive FromSequence and emits
        // no opaque token. Bound total pages/events; never accept a moving head.
        for (int pageNumber = 0; pageNumber < 100; pageNumber++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:3500/v1.0/invoke/eventstore/method/api/v1/streams/read")
            { Content = JsonContent.Create(new { tenant = "system", domain = "tenants", aggregateId = tenantId, fromSequence = cursor, pageSize = 100 }) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("dapr-api-token", Environment.GetEnvironmentVariable("DAPR_API_TOKEN"));
            using HttpResponseMessage response = await _native.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            JsonObject? page = await ReadAsync(response, cancellationToken).ConfigureAwait(false);
            if (page?["tenant"]?.GetValue<string>() != "system" || page["domain"]?.GetValue<string>() != "tenants"
                || page["aggregateId"]?.GetValue<string>() != tenantId || page["events"] is not JsonArray items
                || page["metadata"] is not JsonObject metadata || metadata["fromSequence"]?.GetValue<long>() != cursor
                || metadata["toSequence"] is not null || metadata["eventCount"]?.GetValue<int>() != items.Count
                || items.Count is 0 or > 100 || metadata["latestSequence"]?.GetValue<long>() is not { } latest
                || latest < 1 || latest > 10000 || head is not null && head != latest
                || metadata["nextContinuationToken"] is not null) { return null; }
            head = latest;
            complete ??= page.DeepClone().AsObject();
            foreach (JsonNode? item in items)
            {
                if (item?["sequenceNumber"]?.GetValue<long>() != ++cursor || cursor > head) { return null; }
                events.Add(item.DeepClone());
            }
            if (metadata["lastSequenceReturned"]?.GetValue<long>() != cursor || metadata["isTruncated"]?.GetValue<bool>() != (cursor < head)) { return null; }
            if (cursor == head)
            {
                complete["events"] = events;
                complete["metadata"] = new JsonObject { ["fromSequence"] = 0L, ["toSequence"] = null,
                    ["lastSequenceReturned"] = cursor, ["latestSequence"] = head, ["eventCount"] = events.Count,
                    ["isTruncated"] = false, ["nextContinuationToken"] = null };
                return complete;
            }
        }
        return null;
    }

    /// <summary>Folds complete native persisted events. Partial, unknown, foreign or malformed streams grant no enrollment.</summary>
    public static NativeEnrollment? Fold(JsonObject? page, string tenantId)
    {
        try
        {
            if (page?["tenant"]?.GetValue<string>() != "system" || page["domain"]?.GetValue<string>() != "tenants"
                || page["aggregateId"]?.GetValue<string>() != tenantId || page["events"] is not JsonArray events
                || page["metadata"] is not JsonObject metadata || metadata["isTruncated"]?.GetValue<bool>() != false
                || metadata["latestSequence"]?.GetValue<long>() != events.Count || events.Count == 0
                || metadata["fromSequence"]?.GetValue<long>() != 0 || metadata["eventCount"]?.GetValue<int>() != events.Count
                || metadata["nextContinuationToken"] is not null) { return null; }
            var methods = typeof(TenantState).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.Name == nameof(TenantState.Apply) && method.GetParameters().Length == 1)
                .ToDictionary(method => method.GetParameters()[0].ParameterType.FullName!, StringComparer.Ordinal);
            var state = new TenantState();
            long sequence = 0;
            foreach (JsonNode? node in events)
            {
                if (node is not JsonObject item || item["sequenceNumber"]?.GetValue<long>() != ++sequence
                    || item["serializationFormat"]?.GetValue<string>() != "json"
                    || item["eventTypeName"]?.GetValue<string>() is not { } eventType
                    || !methods.TryGetValue(eventType.Split(',', 2)[0].Trim(), out MethodInfo? apply)
                    || item["payload"]?.GetValue<string>() is not { } encoded) { return null; }
                byte[] bytes = Convert.FromBase64String(encoded);
                JsonObject? payload = ParseObject(Encoding.UTF8.GetString(bytes));
                string? eventTenant = (payload?["tenantId"] ?? payload?["TenantId"])?.GetValue<string>();
                if (eventTenant != tenantId) { return null; }
                object? value = JsonSerializer.Deserialize(bytes, apply.GetParameters()[0].ParameterType, JsonOptions);
                if (value is null) { return null; }
                apply.Invoke(state, [value]);
            }
            if (state.TenantId != tenantId) { return null; }
            string revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(page.ToJsonString())));
            return new(tenantId, revision, state.Status == TenantStatus.Active);
        }
        catch (Exception) { return null; }
    }

    /// <summary>Parses bounded public JSON and rejects duplicate properties at every depth.</summary>
    public static JsonObject? ParseObject(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) > 1048576) { return null; }
        try
        {
            using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 });
            if (!Unique(document.RootElement)) { return null; }
            return JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { MaxDepth = 32 }) as JsonObject;
        }
        catch (JsonException) { return null; }
    }

    private static bool Unique(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            return value.EnumerateObject().All(property => names.Add(property.Name) && Unique(property.Value));
        }
        return value.ValueKind != JsonValueKind.Array || value.EnumerateArray().All(Unique);
    }

    private static async Task<JsonObject?> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 1048576) { return null; }
        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        byte[] bytes = new byte[1048577];
        int used = 0;
        while (used < bytes.Length)
        {
            int count = await stream.ReadAsync(bytes.AsMemory(used), cancellationToken).ConfigureAwait(false);
            if (count == 0) { return ParseObject(new UTF8Encoding(false, true).GetString(bytes, 0, used)); }
            used += count;
        }
        return null;
    }

    /// <summary>Validates the supplied public CA and original HTTPS host, including with an isolated socket transport.</summary>
    public static HttpClient VerifiedClient(string caFile, Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>>? connect = null)
    {
        var policy = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck };
        var certificates = new X509Certificate2Collection();
        certificates.ImportFromPemFile(caFile);
        policy.CustomTrustStore.AddRange(certificates);
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        handler.SslOptions.CertificateChainPolicy = policy;
        if (connect is not null) { handler.ConnectCallback = connect; }
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _native.Dispose();
        _provider.Dispose();
        _identity.Dispose();
    }
}
