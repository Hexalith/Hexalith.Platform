using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Hexalith.Platform.Custody;

/// <summary>Private candidate closed canonical detached ES256 verification codec; no public manifest wire/profile approval or signing API.</summary>
internal static class DecisionAuthorityManifestCodec
{
    /// <summary>Captures/validates the closed bounded immutable original manifest before verification.</summary>
    internal static DecisionAuthorityManifest Capture(DecisionAuthorityManifest m)
    {
        ArgumentNullException.ThrowIfNull(m);
        foreach (string s in new[] { m.Issuer, m.Audience, m.DecisionId, m.ContractId, m.SigningKeyVersion }) { Text(s); }
        if (m.ContractVersion is <= 0 or > 9007199254740991 || m.OutcomeVersion is <= 0 or > 9007199254740991
            || m.EffectivePredecessorVersion is <= 0 or > 9007199254740991 || (m.EffectivePredecessorVersion is null) == (m.RootPolicyId is null)
            || m.IssuedAt.Offset != TimeSpan.Zero || m.ExclusiveExpiry.Offset != TimeSpan.Zero || m.ExclusiveExpiry <= m.IssuedAt)
        { throw new ArgumentException("Malformed private decision-manifest context."); }
        if (m.RootPolicyId is not null) { Text(m.RootPolicyId); }
        var evaluations = Sorted(m.AffectedEvaluations); var approvers = new List<DecisionAuthorityApprover>(); string? previous = null;
        if (m.Approvers is null || m.Approvers.Count is < 1 or > 100) { throw new ArgumentException("Missing bounded approval evidence."); }
        foreach (var a in m.Approvers)
        {
            if (approvers.Count >= 100 || a is null) { throw new ArgumentException("Malformed approval evidence."); }
            foreach (string s in new[] { a.Role, a.ActorId, a.AuthoritySourceId, a.BindingVersion, a.EvidenceId }) { Text(s); }
            if (previous is not null && StringComparer.Ordinal.Compare(previous, a.Role) >= 0) { throw new ArgumentException("Unordered or duplicate approval roles."); }
            previous = a.Role; approvers.Add(a);
        }
        return m with { AffectedEvaluations = evaluations, Approvers = Array.AsReadOnly(approvers.ToArray()) };
    }
    /// <summary>Owns an exact sorted distinct nonempty bounded string set.</summary>
    internal static IReadOnlyList<string> Sorted(IReadOnlyList<string> source)
    {
        if (source is null || source.Count is < 1 or > 100) { throw new ArgumentException("Missing bounded decision set."); }
        var owned = new List<string>(); string? previous = null;
        foreach (string value in source) { if (owned.Count >= 100) { throw new ArgumentException("Malformed decision set."); } Text(value);
            if (previous is not null && StringComparer.Ordinal.Compare(previous, value) >= 0) { throw new ArgumentException("Unordered or duplicate decision set."); } previous = value; owned.Add(value); }
        if (owned.Count == 0) { throw new ArgumentException("Missing decision set."); } return Array.AsReadOnly(owned.ToArray());
    }
    /// <summary>Strict UTF-8 rejects invalid surrogate identities before JSON serialization can replace them.</summary>
    internal static void Text(string s)
    { if (string.IsNullOrWhiteSpace(s) || s.Length > 2048 || new UTF8Encoding(false, true).GetByteCount(s) > 2048) { throw new ArgumentException("Malformed decision identity."); } }
    /// <summary>Gets candidate RFC-8785-style closed canonical string/integer JSON, with explicit lexical field order and representable integral numbers.</summary>
    internal static byte[] Canonical(DecisionAuthorityManifest input)
    {
        var m = Capture(input); using var stream = new MemoryStream(); using var w = new Utf8JsonWriter(stream, new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        w.WriteStartObject(); w.WritePropertyName("AffectedEvaluations"); w.WriteStartArray(); foreach (string e in m.AffectedEvaluations) { w.WriteStringValue(e); } w.WriteEndArray();
        w.WritePropertyName("Approvers"); w.WriteStartArray(); foreach (var a in m.Approvers) { w.WriteStartObject(); w.WriteString("ActorId", a.ActorId); w.WriteString("AuthoritySourceId", a.AuthoritySourceId);
            w.WriteString("BindingVersion", a.BindingVersion); w.WriteString("EvidenceId", a.EvidenceId); w.WriteString("Role", a.Role); w.WriteEndObject(); } w.WriteEndArray();
        w.WriteString("Audience", m.Audience); w.WriteString("ContractId", m.ContractId); w.WriteNumber("ContractVersion", m.ContractVersion); w.WriteString("DecisionId", m.DecisionId);
        if (m.EffectivePredecessorVersion is { } predecessor) { w.WriteNumber("EffectivePredecessorVersion", predecessor); } else { w.WriteNull("EffectivePredecessorVersion"); }
        w.WriteString("ExclusiveExpiry", m.ExclusiveExpiry.ToString("O", System.Globalization.CultureInfo.InvariantCulture)); w.WriteString("IssuedAt", m.IssuedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        w.WriteString("Issuer", m.Issuer); w.WriteNumber("OutcomeVersion", m.OutcomeVersion); w.WriteString("RootPolicyId", m.RootPolicyId); w.WriteString("SigningKeyVersion", m.SigningKeyVersion); w.WriteEndObject(); w.Flush(); return stream.ToArray();
    }
    /// <summary>Gets an explicitly private candidate exact expected header; key-family authority comes from the independent profile.</summary>
    internal static string Header(DecisionAuthorityPublishedProfile profile)
    {
        foreach (string s in new[] { profile.PublicAnchorId, profile.PublicAnchorVersion, profile.SigningKeyVersion }) { Text(s); }
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(new { alg = "ES256", anchor = profile.PublicAnchorId, anchorVersion = profile.PublicAnchorVersion,
            keyVersion = profile.SigningKeyVersion, typ = "candidate-decision-authority-v1" });
        return Convert.ToBase64String(json).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
    /// <summary>Verifies only with exact independently published public anchor and candidate header, never manifest-supplied key material.</summary>
    internal static bool Verify(DecisionAuthorityManifest manifest, DecisionAuthorityPublishedProfile profile, string jws)
    {
        using var key = ECDsa.Create(); key.ImportSubjectPublicKeyInfo(profile.SubjectPublicKeyInfo, out int consumed);
        return consumed == profile.SubjectPublicKeyInfo.Length && DetachedEs256JwsCore.Verify(Header(profile), Canonical(manifest), jws, key);
    }
}
