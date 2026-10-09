using System.Security.Cryptography;

namespace Hexalith.Platform.Identity;

/// <summary>Pure offline verification of a canonical bootstrap against independently supplied root and scope pins.</summary>
public static class P1BootstrapVerifier
{
    private const int MaxPayloadLength = 1024 * 1024;
    private const string P256CurveOid = "1.2.840.10045.3.1.7";

    /// <summary>Returns an authenticated offline enrollment only for an exact root-signed, current bootstrap.</summary>
    public static bool TryVerify(P1SignedDocument? document, P1BootstrapPin? pin,
        DateTimeOffset authenticatedNowUtc, out P1AuthenticatedEnrollment? enrollment)
    {
        enrollment = null;
        try
        {
            byte[]? sourcePayload = document?.Payload;
            byte[]? sourceSignature = document?.Signature;
            byte[]? sourceRootSpki = pin?.RootPublicKeySpki;
            if (sourcePayload is not { Length: > 0 and <= MaxPayloadLength }
                || sourceSignature is not { Length: 64 }
                || sourceRootSpki is not { Length: > 0 and <= 512 } || pin is null)
            {
                return false;
            }

            byte[] payload = sourcePayload.ToArray();
            byte[] signature = sourceSignature.ToArray();
            byte[] rootSpki = sourceRootSpki.ToArray();
            if (authenticatedNowUtc.Offset != TimeSpan.Zero || !Hex(pin.RootKeyFingerprint)
                || !Hex(pin.BootstrapSha256) || !Hex(pin.ReceiptKeyFingerprint)
                || !Hex(pin.StatusKeyFingerprint) || !Required(pin.Revision) || !Required(pin.Issuer)
                || !Required(pin.Audience) || !Required(pin.PrincipalMappingRevision)
                || !ValidOrigin(pin.RetrievalOrigin) || !ValidOrigin(pin.StatusOrigin)
                || !string.Equals(Convert.ToHexStringLower(SHA256.HashData(payload)),
                    pin.BootstrapSha256, StringComparison.Ordinal)
                || !P1BootstrapWireV1.TryDecode(payload, out P1BootstrapClaims? claims)
                || claims is null || !ClaimsValid(claims, pin, rootSpki, authenticatedNowUtc)
                || !VerifySignature(payload, signature, rootSpki,
                    pin.RootKeyFingerprint))
            {
                return false;
            }

            var verified = new P1ReceiptEnrollment(claims.Issuer, claims.RetrievalOrigin,
                claims.StatusOrigin, claims.Audience, claims.Revision, claims.ReceiptPublicKeySpki,
                claims.ReceiptKeyFingerprint, claims.StatusPublicKeySpki, claims.StatusKeyFingerprint,
                claims.EffectiveAtUtc, claims.ExpiresAtUtc);
            enrollment = new(verified, pin.BootstrapSha256, pin.RootKeyFingerprint,
                claims.PrincipalMappingRevision);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool ClaimsValid(P1BootstrapClaims claims, P1BootstrapPin pin, byte[] rootSpki, DateTimeOffset now)
        => claims.Revision == pin.Revision && claims.Issuer == pin.Issuer
            && claims.Audience == pin.Audience && claims.RetrievalOrigin == pin.RetrievalOrigin
            && claims.StatusOrigin == pin.StatusOrigin
            && claims.PrincipalMappingRevision == pin.PrincipalMappingRevision
            && Required(claims.Revision) && Required(claims.Issuer) && Required(claims.Audience)
            && Required(claims.PrincipalMappingRevision)
            && claims.ReceiptKeyFingerprint == pin.ReceiptKeyFingerprint
            && claims.StatusKeyFingerprint == pin.StatusKeyFingerprint
            && !claims.ReceiptPublicKeySpki.AsSpan().SequenceEqual(claims.StatusPublicKeySpki)
            && !claims.ReceiptPublicKeySpki.AsSpan().SequenceEqual(rootSpki)
            && !claims.StatusPublicKeySpki.AsSpan().SequenceEqual(rootSpki)
            && ValidKey(claims.ReceiptPublicKeySpki, claims.ReceiptKeyFingerprint)
            && ValidKey(claims.StatusPublicKeySpki, claims.StatusKeyFingerprint)
            && claims.EffectiveAtUtc.Offset == TimeSpan.Zero && claims.ExpiresAtUtc.Offset == TimeSpan.Zero
            && claims.EffectiveAtUtc < claims.ExpiresAtUtc
            && claims.EffectiveAtUtc <= now && now < claims.ExpiresAtUtc;

    private static bool VerifySignature(byte[] payload, byte[] signature, byte[] rootSpki, string fingerprint)
    {
        if (signature.Length != 64 || !ValidKey(rootSpki, fingerprint))
        {
            return false;
        }

        using ECDsa key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(rootSpki, out int read);
        return read == rootSpki.Length
            && key.VerifyData(payload, signature, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static bool ValidKey(byte[]? spki, string? fingerprint)
    {
        if (spki is not { Length: > 0 and <= 512 } || !Hex(fingerprint)
            || !string.Equals(Convert.ToHexStringLower(SHA256.HashData(spki)), fingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        using ECDsa key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(spki, out int read);
        return read == spki.Length && key.KeySize == 256
            && key.ExportParameters(false).Curve.Oid.Value == P256CurveOid;
    }

    private static bool Hex(string? value)
        => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool Required(string? value)
        => !string.IsNullOrWhiteSpace(value) && !value.StartsWith("NO_", StringComparison.Ordinal);

    private static bool ValidOrigin(string? value)
        => Required(value) && Uri.TryCreate(value, UriKind.Absolute, out Uri? origin)
            && origin.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(origin.Host)
            && string.IsNullOrEmpty(origin.UserInfo) && string.IsNullOrEmpty(origin.Query)
            && string.IsNullOrEmpty(origin.Fragment) && origin.AbsolutePath == "/"
            && string.Equals(origin.GetLeftPart(UriPartial.Authority), value, StringComparison.Ordinal);
}
