using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>Public proof pins returned by the accepted independent enrollment; no private material.</summary>
public sealed record QualificationProofBinding(string Identity, string Version, string PublicAnchorFile, string PublicAnchorSha256);

/// <summary>Verifies the accepted independently pinned public role proof before journaling or returning it.</summary>
public interface IQualificationPublicProofVerifier
{
    /// <summary>Checks signature, current role identity and complete request binding.</summary>
    bool Verify(string role, JsonObject request, JsonObject envelope);
}

/// <summary>ES256 public verifier. Bindings must come from authoritative accepted configuration and enrollment.</summary>
public sealed class QualificationProofVerifier(IReadOnlyDictionary<string, QualificationProofBinding> bindings, TimeProvider? clock = null) : IQualificationPublicProofVerifier
{
    /// <inheritdoc/>
    public bool Verify(string role, JsonObject request, JsonObject envelope)
    {
        try
        {
            if (!bindings.TryGetValue(role, out QualificationProofBinding? binding) || envelope.Count != 2
                || envelope["payload"] is not JsonObject payload || SecretsProof.Text(envelope, "signature") is not { } encoded
                || !SecretsProof.SameText(payload, "identity", binding.Identity) || !SecretsProof.SameText(payload, "version", binding.Version)) { return false; }
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
            byte[] publicBytes = File.ReadAllBytes(binding.PublicAnchorFile);
            if (publicBytes.Length > 4096 || Convert.ToHexString(SHA256.HashData(publicBytes)).ToLowerInvariant() != binding.PublicAnchorSha256) { return false; }
            byte[] signature = Convert.FromBase64String(encoded);
            if (signature.Length != 64 || Convert.ToBase64String(signature) != encoded) { return false; }
            using var key = ECDsa.Create();
            key.ImportFromPem(System.Text.Encoding.ASCII.GetString(publicBytes));
            if (key.ExportParameters(false).Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value) { return false; }
            using var bytes = new MemoryStream();
            using (var writer = new Utf8JsonWriter(bytes, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            { Sorted(payload).WriteTo(writer); }
            return key.VerifyData(bytes.ToArray(), signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception) { return false; }
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
