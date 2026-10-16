using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Public proof pins returned by the accepted independent enrollment; no private material.</summary>
public sealed class QualificationProofBinding
{
    private readonly byte[] _publicAnchor;

    /// <summary>Installs bounded immutable public PEM bytes after checking the accepted hash and ES256 key type. No path is opened.</summary>
    public QualificationProofBinding(string identity, string version, ReadOnlySpan<byte> publicAnchor, string publicAnchorSha256)
    {
        if (publicAnchor.Length is 0 or > 4096) { throw new ArgumentException("Public anchor exceeds the accepted bound.", nameof(publicAnchor)); }
        byte[] copy = publicAnchor.ToArray();
        string pem = System.Text.Encoding.ASCII.GetString(copy);
        if (!pem.StartsWith("-----BEGIN PUBLIC KEY-----", StringComparison.Ordinal)
            || Convert.ToHexString(SHA256.HashData(copy)).ToLowerInvariant() != publicAnchorSha256)
        { throw new ArgumentException("Public anchor does not match the accepted public pin.", nameof(publicAnchor)); }
        using var key = ECDsa.Create();
        key.ImportFromPem(pem);
        if (key.ExportParameters(false).Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
        { throw new ArgumentException("An ES256 public anchor is required.", nameof(publicAnchor)); }
        Identity = identity;
        Version = version;
        _publicAnchor = copy;
    }

    /// <summary>Gets the accepted role identity.</summary>
    public string Identity { get; }

    /// <summary>Gets the accepted provider version.</summary>
    public string Version { get; }

    internal ECDsa OpenPublicKey()
    {
        var key = ECDsa.Create();
        key.ImportFromPem(System.Text.Encoding.ASCII.GetString(_publicAnchor));
        return key;
    }
}

/// <summary>Verifies the accepted independently pinned public role proof before journaling or returning it.</summary>
public interface IQualificationPublicProofVerifier
{
    /// <summary>Checks signature, current role identity and complete request binding.</summary>
    bool Verify(string role, JsonObject request, JsonObject envelope, CancellationToken cancellationToken = default);
}

/// <summary>ES256 public verifier. Bindings must come from authoritative accepted configuration and enrollment.</summary>
public sealed class QualificationProofVerifier(IReadOnlyDictionary<string, QualificationProofBinding> bindings, TimeProvider? clock = null) : IQualificationPublicProofVerifier
{
    private readonly Dictionary<string, QualificationProofBinding> _bindings = new(bindings, StringComparer.Ordinal);

    /// <inheritdoc/>
    public bool Verify(string role, JsonObject request, JsonObject envelope, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Normalize protocol JSON integers and reject malformed/secret-bearing envelopes before schema checks.
            if (SecretsProof.Parse(envelope.ToJsonString()) is not { } parsed) { return false; }
            envelope = parsed;
            if (!_bindings.TryGetValue(role, out QualificationProofBinding? binding) || envelope.Count != 2
                || envelope["payload"] is not JsonObject payload || SecretsProof.Text(envelope, "signature") is not { } encoded
                || !SecretsProof.SameText(payload, "identity", binding.Identity) || !SecretsProof.SameText(payload, "version", binding.Version)) { return false; }
            if (!QualificationProtocolSchema.Valid(role, payload, request)) { return false; }
            foreach (string field in new[] { "protocol", "runId", "targetCommit", "isolatedTargetId", "planSha256", "requestId" })
            { if (!SecretsProof.SameText(payload, field, SecretsProof.Text(request, field))) { return false; } }
            if (role != "enrollment")
            {
                foreach (string field in new[] { "caseId", "caseTargetId" })
                { if (!SecretsProof.SameText(payload, field, SecretsProof.Text(request, field))) { return false; } }
            }
            long now = (clock ?? TimeProvider.System).GetUtcNow().ToUnixTimeSeconds();
            if (SecretsProof.Integer(payload, "observedAt") is not { } observed || SecretsProof.Integer(payload, "validUntil") is not { } until
                || observed > now || observed < now - 30 || until <= now || until <= observed || until > observed + 30) { return false; }
            if (encoded.Length != 88) { return false; }
            byte[] signature = Convert.FromBase64String(encoded);
            if (signature.Length != 64 || Convert.ToBase64String(signature) != encoded) { return false; }
            using var key = binding.OpenPublicKey();
            byte[] bytes = CanonicalBytes(payload);
            if (bytes.Length > SecretsProof.MaximumBytes) { return false; }
            cancellationToken.ThrowIfCancellationRequested();
            bool valid = key.VerifyData(bytes, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            cancellationToken.ThrowIfCancellationRequested();
            return valid;
        }
        catch (Exception) { return false; }
    }

    internal static byte[] CanonicalBytes(JsonNode node)
    {
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        { Sorted(node).WriteTo(writer); }
        return bytes.ToArray();
    }

    private static JsonNode Sorted(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var result = new JsonObject();
            foreach (var item in obj.OrderBy(item => item.Key, StringComparer.Ordinal))
            { result[item.Key] = item.Value is null ? null : Sorted(item.Value); }
            return result;
        }
        if (node is JsonArray array) { return new JsonArray(array.Select(item => item is null ? null : Sorted(item)).ToArray()); }
        if (node is JsonValue value && value.TryGetValue<string>(out string? text) && text.Any(character => character < 32 || character > 126))
        { throw new ArgumentException("Qualification protocol identities must be ASCII."); }
        return node.DeepClone();
    }
}
