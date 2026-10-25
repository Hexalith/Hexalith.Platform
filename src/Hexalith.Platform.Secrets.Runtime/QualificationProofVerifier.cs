using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.Platform.Secrets.Runtime;

/// <summary>ES256 public verifier. Bindings must come from authoritative accepted configuration and enrollment.</summary>
public sealed class QualificationProofVerifier(IReadOnlyDictionary<string, QualificationProofBinding> bindings, TimeProvider? clock = null) : IQualificationPublicProofVerifier
{
    private readonly Dictionary<string, QualificationProofBinding> _bindings = new(bindings, StringComparer.Ordinal);

    /// <inheritdoc/>
    public bool Verify(string role, JsonObject request, JsonObject envelope, CancellationToken cancellationToken = default)
        =>VerifyAt(role,request,envelope,null,cancellationToken);
    /// <inheritdoc />
    public bool VerifyOriginal(string role,JsonObject request,JsonObject envelope,long originalTime,CancellationToken cancellationToken=default)
        =>VerifyAt(role,request,envelope,originalTime,cancellationToken);
    private bool VerifyAt(string role,JsonObject request,JsonObject envelope,long? originalTime,CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (envelope is null) { return false; }
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
            long current=(clock ?? TimeProvider.System).GetUtcNow().ToUnixTimeSeconds();
            long now=originalTime??current;
            if(now>current) return false;
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
