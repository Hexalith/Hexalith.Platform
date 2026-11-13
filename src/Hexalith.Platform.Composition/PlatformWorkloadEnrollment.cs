using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CommunityToolkit.Aspire.Hosting.Dapr;

namespace Hexalith.Platform.Composition;

/// <summary>Consumes protected owner enrollment; creates no grants, signer, authority or TLS trust.</summary>
public static class PlatformWorkloadEnrollment
{
    /// <summary>The protected reference configuration prefix.</summary>
    public const string ConfigurationPrefix = "Folders:WorkloadEnrollment:";
    private const string Profile = "https://hexalith.com/security/workload-binding/v1";
    private const string BindingPrefix = "eventstore:bound-";

    /// <summary>Disables profile capability while a fixture is still selecting its final cohort.</summary>
    /// <param name="builder">The application model.</param>
    public static void Disable(IDistributedApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        foreach (ProjectResource resource in PlatformWorkloadEnrollmentInventory.Cohort(builder))
            builder.CreateResourceBuilder(resource).WithEnvironment("Authentication__WorkloadIssuer__EnableResourceBindingRequests", "false");
    }

    /// <summary>Verifies the complete final cohort and fresh issuer probes before applying dedicated SDK configuration.</summary>
    /// <param name="builder">The final application model, before catalog capture.</param>
    /// <param name="cancellationToken">The caller cancellation.</param>
    /// <param name="selectedTenant">The actual selected fixture tenant, when one is known before startup.</param>
    /// <returns>Whether protected enrollment was verified and applied.</returns>
    public static async Task<bool> ConfigureAsync(IDistributedApplicationBuilder builder, CancellationToken cancellationToken = default, string? selectedTenant = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ProjectResource[] cohort = PlatformWorkloadEnrollmentInventory.Cohort(builder);
        Disable(builder);
        string? path = builder.Configuration[ConfigurationPrefix + "ManifestPath"];
        string? digest = builder.Configuration[ConfigurationPrefix + "ManifestSha256"];
        if (string.IsNullOrWhiteSpace(path) && string.IsNullOrWhiteSpace(digest))
        {
            Console.WriteLine("WORKLOAD_ENROLLMENT_DISABLED missing=Folders:WorkloadEnrollment:ManifestPath,Folders:WorkloadEnrollment:ManifestSha256");
            return false;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !Hex(digest)) throw Refused();
            byte[] bytes = await ReadProtectedAsync(path, 65536, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), Convert.FromHexString(digest!))) throw Refused();
            using JsonDocument manifest = Parse(bytes);
            JsonElement root = manifest.RootElement;
            Fields(root, "schema", "authority", "issuer", "tokenEndpoint", "notBefore", "expiresAt", "signingKeyId", "signingPublicKeySha256", "receivers", "clients");
            if (Text(root, "schema") != "hexalith.workload-enrollment/v1") throw Refused();
            string issuer = Text(root, "issuer"), authority = Text(root, "authority"), endpoint = Text(root, "tokenEndpoint");
            if (issuer != authority || !Https(authority) || !Https(endpoint) || endpoint != issuer + "/protocol/openid-connect/token") throw Refused();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long start = root.GetProperty("notBefore").GetInt64(), end = root.GetProperty("expiresAt").GetInt64();
            if (start < 0 || start > now || end <= now || end <= start || end > start + 2592000) throw Refused();
            string kid = Text(root, "signingKeyId"), signerDigest = Text(root, "signingPublicKeySha256");
            if (!Hex(signerDigest)) throw Refused();
            string[] receivers = [.. root.GetProperty("receivers").EnumerateArray().Select(item => item.GetString() ?? string.Empty)];
            if (receivers.Length != cohort.Length || receivers.Distinct(StringComparer.Ordinal).Count() != receivers.Length
                || !receivers.Order(StringComparer.Ordinal).SequenceEqual(cohort.Select(item => item.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal)) throw Refused();
            JsonElement[] clients = [.. root.GetProperty("clients").EnumerateArray()];
            ProjectResource[] callers = cohort;
            if (clients.Length != callers.Length || clients.Select(item => Text(item, "resource")).Distinct(StringComparer.Ordinal).Count() != callers.Length
                || !clients.Select(item => Text(item, "resource")).Order(StringComparer.Ordinal).SequenceEqual(callers.Select(item => item.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal)) throw Refused();
            PlatformWorkloadEnrollmentInventory.Validate(cohort, clients, selectedTenant);

            // Default HttpClient TLS validation is mandatory, including prelaunch run-owned trust.
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
            using HttpClient http = new(handler) { Timeout = TimeSpan.FromSeconds(30) };
            using JsonDocument metadata = await GetAsync(http, authority + "/.well-known/openid-configuration", cancellationToken).ConfigureAwait(false);
            string jwksUri = Text(metadata.RootElement, "jwks_uri");
            if (Text(metadata.RootElement, "issuer") != issuer || Text(metadata.RootElement, "token_endpoint") != endpoint
                || !Https(jwksUri) || new Uri(jwksUri).GetLeftPart(UriPartial.Authority) != new Uri(authority).GetLeftPart(UriPartial.Authority)) throw Refused();
            using JsonDocument jwks = await GetAsync(http, jwksUri, cancellationToken).ConfigureAwait(false);
            JsonElement[] keys = [.. jwks.RootElement.GetProperty("keys").EnumerateArray().Where(key => Text(key, "kid") == kid)];
            if (keys.Length != 1 || Text(keys[0], "kty") != "RSA" || Text(keys[0], "alg") != "RS256" || Text(keys[0], "use") != "sig") throw Refused();
            using RSA signer = RSA.Create();
            signer.ImportParameters(new RSAParameters { Modulus = Decode(Text(keys[0], "n")), Exponent = Decode(Text(keys[0], "e")) });
            if (signer.KeySize < 2048 || Convert.ToHexString(SHA256.HashData(signer.ExportSubjectPublicKeyInfo())) != signerDigest) throw Refused();
            using var signerCertificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(keys[0].GetProperty("x5c")[0].GetString()!));
            if (signerCertificate.NotBefore.ToUniversalTime() > DateTime.UtcNow || signerCertificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow) throw Refused();
            using RSA? certifiedSigner = signerCertificate.GetRSAPublicKey();
            if (certifiedSigner is null || !certifiedSigner.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(signer.ExportSubjectPublicKeyInfo())) throw Refused();

            var credentials = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonElement client in clients)
            {
                Fields(client, "resource", "clientId", "workload", "secretFile", "probes");
                string resource = Text(client, "resource"), clientId = Text(client, "clientId"), workload = Text(client, "workload");
                ProjectResource host = callers.Single(item => item.Name == resource);
                string actualCaller = host.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar
                    .Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.AppId ?? throw Refused();
                if (workload != actualCaller || string.IsNullOrWhiteSpace(clientId)) throw Refused();
                string secretPath = Text(client, "secretFile");
                if (!Path.IsPathFullyQualified(secretPath)) throw Refused();
                string secret = Encoding.UTF8.GetString(await ReadProtectedAsync(secretPath, 4096, cancellationToken).ConfigureAwait(false)).TrimEnd('\r', '\n');
                if (secret.Length < 32 || secret.Any(char.IsWhiteSpace)) throw Refused();
                JsonElement[] probes = [.. client.GetProperty("probes").EnumerateArray()];
                if (probes.Length is < 1 or > 64) throw Refused();
                foreach (JsonElement probe in probes)
                {
                    Fields(probe, "grantId", "revision", "audience", "operation", "bindings");
                    string audience = Text(probe, "audience"), operation = Text(probe, "operation");
                    if (probe.GetProperty("revision").GetInt64() <= 0 || string.IsNullOrWhiteSpace(Text(probe, "grantId"))) throw Refused();
                    JsonElement bindings = probe.GetProperty("bindings");
                    if (bindings.ValueKind != JsonValueKind.Object || bindings.EnumerateObject().Count() > 16) throw Refused();
                    var requested = new[] { new { type = Profile, audience, operation, bindings } };
                    using var form = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["grant_type"] = "client_credentials", ["client_id"] = clientId, ["client_secret"] = secret,
                        ["scope"] = "eventstore-audience." + audience + " eventstore-operation." + operation.Replace(':', '.'),
                        ["authorization_details"] = JsonSerializer.Serialize(requested),
                    });
                    using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = form };
                    using HttpResponseMessage response = await http.SendAsync(tokenRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    using JsonDocument issued = await ReadAsync(response.Content, cancellationToken).ConfigureAwait(false);
                    VerifyToken(Text(issued.RootElement, "access_token"), signer, kid, issuer, workload, probe, Math.Min(end,
                        new DateTimeOffset(signerCertificate.NotAfter.ToUniversalTime()).ToUnixTimeSeconds()));
                }
                credentials.Add(resource, secret);
            }

            // No model mutation enables the profile until every owner probe has succeeded.
            foreach (ProjectResource receiver in cohort)
                builder.CreateResourceBuilder(receiver).WithEnvironment("Authentication__WorkloadJwtBearer__Authority", authority)
                    .WithEnvironment("Authentication__WorkloadJwtBearer__Issuer", issuer)
                    .WithEnvironment("Authentication__WorkloadJwtBearer__Audience", receiver.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar
                        .Annotations.OfType<DaprSidecarOptionsAnnotation>().Single().Options.AppId!)
                    .WithEnvironment("Authentication__WorkloadJwtBearer__RequireHttpsMetadata", "true")
                    .WithEnvironment("Authentication__WorkloadJwtBearer__SigningKey", string.Empty)
                    .WithEnvironment("Authentication__WorkloadJwtBearer__AllowInsecureSymmetricKey", "false")
                    .WithEnvironment("Authentication__WorkloadJwtBearer__AllowedAlgorithms__0", "RS256");
            foreach (JsonElement client in clients)
            {
                string resource = Text(client, "resource"), secret = credentials[resource];
                var credential = builder.AddParameter("enrolled-" + resource + "-workload-secret", () => secret, secret: true);
                builder.CreateResourceBuilder(callers.Single(item => item.Name == resource))
                    .WithEnvironment("Authentication__WorkloadIssuer__ClientId", Text(client, "clientId"))
                    .WithEnvironment("Authentication__WorkloadIssuer__Workload", Text(client, "workload"))
                    .WithEnvironment("Authentication__WorkloadIssuer__ClientSecret", credential)
                    .WithEnvironment("Authentication__WorkloadIssuer__TokenEndpoint", endpoint)
                    .WithEnvironment("Authentication__WorkloadIssuer__EnableResourceBindingRequests", "true");
            }
            Console.WriteLine("WORKLOAD_ENROLLMENT_VERIFIED manifestSha256=" + digest + " receivers=" + cohort.Length + " clients=" + clients.Length);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or HttpRequestException
            or InvalidOperationException or ArgumentException or CryptographicException or KeyNotFoundException or FormatException or TaskCanceledException)
        {
            throw Refused();
        }
    }

    private static void VerifyToken(string token, RSA signer, string kid, string issuer, string workload, JsonElement probe, long maximumExpiry)
    {
        if (token.Length > 16384) throw Refused();
        string[] parts = token.Split('.');
        if (parts.Length != 3) throw Refused();
        using JsonDocument header = Parse(Decode(parts[0])), payload = Parse(Decode(parts[1]));
        if (Text(header.RootElement, "alg") != "RS256" || Text(header.RootElement, "kid") != kid
            || !signer.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Decode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) throw Refused();
        JsonElement value = payload.RootElement;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), issued = value.GetProperty("iat").GetInt64(), expiry = value.GetProperty("exp").GetInt64();
        JsonElement audience = value.GetProperty("aud");
        string? exactAudience = audience.ValueKind == JsonValueKind.String ? audience.GetString()
            : audience.ValueKind == JsonValueKind.Array && audience.GetArrayLength() == 1 ? audience[0].GetString() : null;
        if (Text(value, "iss") != issuer || Text(value, "azp") != workload || exactAudience != Text(probe, "audience")
            || Text(value, "eventstore:operation") != Text(probe, "operation") || Text(value, "eventstore:surface") != "service"
            || Text(value, "eventstore:grant-id") != Text(probe, "grantId") || value.GetProperty("eventstore:grant-revision").GetInt64() != probe.GetProperty("revision").GetInt64()
            || issued > now || expiry <= now || expiry > maximumExpiry || expiry <= issued || expiry - issued > 120
            || value.GetProperty("nbf").GetInt64() > now) throw Refused();
        JsonProperty[] actual = [.. value.EnumerateObject().Where(property => property.Name.StartsWith(BindingPrefix, StringComparison.Ordinal))];
        JsonProperty[] expected = [.. probe.GetProperty("bindings").EnumerateObject()];
        if (actual.Length != expected.Length || expected.Any(binding => !binding.Name.StartsWith(BindingPrefix, StringComparison.Ordinal)
            || !value.TryGetProperty(binding.Name, out JsonElement found) || found.GetString() != binding.Value.GetString())) throw Refused();
    }

    private static async Task<byte[]> ReadProtectedAsync(string path, int maximum, CancellationToken token)
    {
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite
            | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0) throw Refused();
        using FileStream stream = File.OpenRead(path);
        if (stream.Length > maximum) throw Refused();
        using var memory = new MemoryStream();
        byte[] buffer = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            if (memory.Length + read > maximum) throw Refused();
            memory.Write(buffer, 0, read);
        }
        return memory.ToArray();
    }
    private static async Task<JsonDocument> GetAsync(HttpClient http, string uri, CancellationToken token)
    {
        using HttpResponseMessage response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await ReadAsync(response.Content, token).ConfigureAwait(false);
    }
    private static async Task<JsonDocument> ReadAsync(HttpContent content, CancellationToken token)
    {
        using Stream stream = await content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var memory = new MemoryStream();
        byte[] buffer = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            if (memory.Length + read > 65536) throw Refused();
            memory.Write(buffer, 0, read);
        }
        return Parse(memory.ToArray());
    }
    private static JsonDocument Parse(byte[] bytes)
    {
        JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
        try { Unique(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }
    private static void Unique(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject()) { if (!names.Add(property.Name)) throw Refused(); Unique(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (JsonElement item in value.EnumerateArray()) Unique(item);
    }
    private static void Fields(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
            .SequenceEqual(names.Order(StringComparer.Ordinal), StringComparer.Ordinal)) throw Refused();
    }
    private static string Text(JsonElement value, string name)
    {
        string result = value.GetProperty(name).GetString() ?? throw Refused();
        return result.Length is > 0 and <= 16384 ? result : throw Refused();
    }
    private static bool Https(string value) => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
    private static bool Hex(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
    private static InvalidOperationException Refused() => new("Protected workload enrollment verification refused. Required: current owner-approved manifest/digest, exact complete cohort, protected own credentials, TLS and pinned signer, and successful exact grant probes.");
}
