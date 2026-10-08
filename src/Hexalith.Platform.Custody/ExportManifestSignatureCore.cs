using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.Platform.Custody;

/// <summary>Private stateless detached ES256 JWS primitive; persistence and key/anchor authorization remain separate.</summary>
internal static class ExportManifestSignatureCore
{
    private const string CurveOid = "1.2.840.10045.3.1.7";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Signs supplied owner manifest bytes under an export-only, exact-identity protected header.</summary>
    internal static string Sign(ExportManifestSignatureContext context, ReadOnlySpan<byte> manifest, ECDsa signingKey)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        if (!IsP256(signingKey))
        {
            throw new CryptographicException("Export manifest signatures require P-256.");
        }

        string header = Header(context);
        byte[] input = SigningInput(header, manifest);
        try
        {
            byte[] signature = signingKey.SignData(input, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return header + ".." + Base64Url(signature);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
        }
    }

    /// <summary>Verifies offline with an independently supplied public anchor and exact expected manifest identity.</summary>
    internal static bool Verify(ExportManifestSignatureContext expected, ReadOnlySpan<byte> manifest, string detachedJws, ECDsa publicAnchor)
    {
        ArgumentNullException.ThrowIfNull(publicAnchor);
        byte[]? input = null;
        try
        {
            if (detachedJws is null || !IsP256(publicAnchor))
            {
                return false;
            }

            string header = Header(expected);
            // A detached ES256 JWS has this exact header, two dots and an unpadded
            // 86-character signature. Reject size/framing before splitting or decoding.
            if (detachedJws.Length != (long)header.Length + 88
                || !detachedJws.AsSpan(0, header.Length).SequenceEqual(header)
                || detachedJws[header.Length] != '.' || detachedJws[header.Length + 1] != '.')
            {
                return false;
            }

            ReadOnlySpan<char> signatureText = detachedJws.AsSpan(header.Length + 2);
            byte[] signature = DecodeBase64Url(signatureText.ToString());
            if (signature.Length != 64 || !Base64Url(signature).AsSpan().SequenceEqual(signatureText))
            {
                return false;
            }

            input = SigningInput(header, manifest);
            return publicAnchor.VerifyData(input, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException or ArgumentException)
        {
            return false;
        }
        finally
        {
            if (input is not null) { CryptographicOperations.ZeroMemory(input); }
        }
    }

    private static bool IsP256(ECDsa key) => key.KeySize == 256 && key.ExportParameters(false).Curve.Oid.Value == CurveOid;

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

    private static byte[] SigningInput(string header, ReadOnlySpan<byte> manifest)
        => Encoding.ASCII.GetBytes(header + "." + Base64Url(manifest));

    private static string Base64Url(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DecodeBase64Url(string value)
        => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
}
