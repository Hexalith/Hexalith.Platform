using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hexalith.Platform.Custody;

/// <summary>Private stateless detached ES256 JWS primitive; persistence and key/anchor authorization remain separate.</summary>
internal static class DetachedEs256JwsCore
{
    private const string CurveOid = "1.2.840.10045.3.1.7";

    /// <summary>Signs supplied canonical bytes under the exact encoded protected header.</summary>
    internal static string Sign(string header, ReadOnlySpan<byte> manifest, ECDsa signingKey)
    {
        ArgumentNullException.ThrowIfNull(signingKey);
        if (!IsP256(signingKey))
        {
            throw new CryptographicException("Export manifest signatures require P-256.");
        }

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

    /// <summary>Verifies offline with an independently supplied public anchor and exact expected encoded protected header.</summary>
    internal static bool Verify(string header, ReadOnlySpan<byte> manifest, string detachedJws, ECDsa publicAnchor)
    {
        ArgumentNullException.ThrowIfNull(publicAnchor);
        byte[]? input = null;
        try
        {
            if (detachedJws is null || !IsP256(publicAnchor))
            {
                return false;
            }

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

    private static byte[] SigningInput(string header, ReadOnlySpan<byte> manifest)
        => Encoding.ASCII.GetBytes(header + "." + Base64Url(manifest));

    private static string Base64Url(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DecodeBase64Url(string value)
        => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
}
