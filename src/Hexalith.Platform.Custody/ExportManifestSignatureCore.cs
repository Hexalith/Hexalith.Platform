using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.Platform.Custody;

/// <summary>Private export-specific context over the shared stateless ES256 byte primitive; never decision/deletion authority.</summary>
internal static class ExportManifestSignatureCore
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    internal static string Sign(ExportManifestSignatureContext context, ReadOnlySpan<byte> manifest, ECDsa signingKey)
        => DetachedEs256JwsCore.Sign(Header(context), manifest, signingKey);
    internal static bool Verify(ExportManifestSignatureContext expected, ReadOnlySpan<byte> manifest, string detachedJws, ECDsa publicAnchor)
    {
        ArgumentNullException.ThrowIfNull(publicAnchor);
        try { return DetachedEs256JwsCore.Verify(Header(expected), manifest, detachedJws, publicAnchor); }
        catch (ArgumentException) { return false; }
    }
    private static string Header(ExportManifestSignatureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.TenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.ExportId);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.SigningKeyVersion);
        if (context.ManifestVersion <= 0) { throw new ArgumentException("Export manifest version must be positive."); }
        // JsonSerializer replaces malformed UTF-16; reject it before distinct identities can collapse.
        _ = StrictUtf8.GetByteCount(context.TenantId);
        _ = StrictUtf8.GetByteCount(context.ExportId);
        _ = StrictUtf8.GetByteCount(context.SigningKeyVersion);
        // This private candidate signature format is not an accepted production trust profile.
        // Its fixed type does not authorize a provider key family or confer decision/deletion authority.
        return Base64Url(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["alg"] = "ES256", ["typ"] = "hexalith-export-manifest-v1", ["kid"] = context.SigningKeyVersion,
            ["tenant"] = context.TenantId, ["export"] = context.ExportId, ["manifestVersion"] = context.ManifestVersion,
        }));
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
