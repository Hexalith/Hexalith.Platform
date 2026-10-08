using Hexalith.EventStore.Contracts.Security;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Hexalith.Platform.Custody;

/// <summary>Narrow RFC8785 codec and detached ES256 byte primitive for exactly the register's fourteen V1 fields.</summary>
/// <remarks>Private candidate header; key-family/current authority, recorded issue/dispatch, compromise and backend guarantees remain mandatory separate gates.</remarks>
internal static class DeletionBatchCapabilityCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    internal static byte[] CanonicalPayload(DeletionBatchCapabilityV1 payload) => DeletionBatchCapabilityIdentity.CanonicalPayload(payload);
    internal static string SigningRequestId(DeletionBatchCapabilityV1 payload) => DeletionBatchCapabilityIdentity.SigningRequestId(payload);
    internal static string Sign(DeletionBatchCapabilityV1 payload, DeletionCapabilityTrustProfile profile, ECDsa key)
        => DetachedEs256JwsCore.Sign(Header(payload, profile), CanonicalPayload(payload), key);
    internal static bool Verify(DeletionBatchCapabilityV1 payload, DeletionCapabilityTrustProfile profile, string signature, ECDsa publicAnchor)
    {
        try { return DetachedEs256JwsCore.Verify(Header(payload, profile), CanonicalPayload(payload), signature, publicAnchor); }
        catch (ArgumentException) { return false; }
    }
    private static string Header(DeletionBatchCapabilityV1 payload, DeletionCapabilityTrustProfile profile)
    {
        ArgumentNullException.ThrowIfNull(payload); ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.PublicAnchorId); ArgumentException.ThrowIfNullOrWhiteSpace(profile.PublicAnchorVersion);
        if (payload.Issuer != profile.Issuer || payload.Audience != profile.Audience || payload.TenantId != profile.TenantId
            || payload.CapabilityKeyVersion != profile.CapabilityKeyVersion)
        { throw new ArgumentException("Deletion capability trust context mismatch."); }
        var text = new StringBuilder("{\"alg\":\"ES256\",\"anchorVersion\":"); String(text, profile.PublicAnchorVersion);
        text.Append(",\"keyVersion\":"); String(text, profile.CapabilityKeyVersion); text.Append(",\"kid\":"); String(text, profile.PublicAnchorId);
        text.Append(",\"typ\":\"DeletionBatchCapabilityV1\"}");
        return Convert.ToBase64String(StrictUtf8.GetBytes(text.ToString())).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
    private static void String(StringBuilder text, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length > 2048) { throw new ArgumentException("Capability string exceeds bound."); }
        // Strict encoding rejects isolated surrogates before a serializer can replace them. JCS preserves Unicode without normalization.
        _ = StrictUtf8.GetByteCount(value); text.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"': text.Append("\\\""); break;
                case '\\': text.Append("\\\\"); break;
                case '\b': text.Append("\\b"); break;
                case '\t': text.Append("\\t"); break;
                case '\n': text.Append("\\n"); break;
                case '\f': text.Append("\\f"); break;
                case '\r': text.Append("\\r"); break;
                default:
                    if (character < 0x20) { text.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture)); }
                    else { text.Append(character); }
                    break;
            }
        }
        text.Append('"');
    }
}
