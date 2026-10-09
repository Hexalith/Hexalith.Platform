using System.Security.Cryptography;
using System.Text;

namespace Hexalith.Platform.Identity;

/// <summary>Pure offline P1 signature, exact-claim, scope and nonce-bound status verifier; supplies no enrollment or retrieval.</summary>
public static class P1ReceiptVerifier
{
    private const string P256CurveOid = "1.2.840.10045.3.1.7";

    /// <summary>Refuses unless the retained receipt and separately signed fresh status match independently supplied expectations.</summary>
    public static bool Verify(
        P1SignedDocument? receiptDocument, string? retrievalUri, P1SignedDocument? statusDocument, string? statusUri,
        P1ReceiptClaims? expected, byte[]? subjectBytes, P1ReceiptEnrollment? enrollment, string? requestNonce,
        DateTimeOffset authenticatedNowUtc)
    {
        try
        {
            if (receiptDocument?.Payload is null || receiptDocument.Signature is null
                || statusDocument?.Payload is null || statusDocument.Signature is null
                || expected is null || subjectBytes is null || enrollment is null || !Hex(requestNonce)
                || string.IsNullOrWhiteSpace(enrollment.Issuer)
                || string.IsNullOrWhiteSpace(enrollment.Audience)
                || string.IsNullOrWhiteSpace(enrollment.TrustRevision)
                || enrollment.ReceiptPublicKeySpki.AsSpan().SequenceEqual(enrollment.StatusPublicKeySpki)
                || authenticatedNowUtc.Offset != TimeSpan.Zero
                || !P1ReceiptWireV1.TryDecodeReceipt(receiptDocument.Payload, out P1ReceiptClaims? receipt)
                || receipt is null || !P1ReceiptWireV1.TryDecodeStatus(statusDocument.Payload, out P1StatusClaims? status)
                || status is null || !ReceiptValid(receipt, expected, subjectBytes, enrollment, retrievalUri, authenticatedNowUtc)
                || !StatusValid(status, receipt, enrollment, statusUri, requestNonce!, authenticatedNowUtc))
            {
                return false;
            }

            return VerifySignature(receiptDocument, enrollment.ReceiptPublicKeySpki, enrollment.ReceiptKeyFingerprint)
                && VerifySignature(statusDocument, enrollment.StatusPublicKeySpki, enrollment.StatusKeyFingerprint);
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException or InvalidOperationException or EncoderFallbackException)
        {
            return false;
        }
    }

    private static bool ReceiptValid(P1ReceiptClaims receipt, P1ReceiptClaims expected, byte[] subjectBytes, P1ReceiptEnrollment enrollment,
        string? retrievalUri, DateTimeOffset now)
    {
        if (!Hex(receipt.ReceiptId) || !Hex(receipt.KeyFingerprint) || !Hex(receipt.SubjectSha256)
            || !Hex(receipt.ProfileSha256) || !Hex(receipt.WorkloadSha256) || !Hex(receipt.TargetSha256)
            || receipt.SubjectLength <= 0 || receipt.SubjectLength != subjectBytes.LongLength
            || !string.Equals(receipt.SubjectSha256, Convert.ToHexStringLower(SHA256.HashData(subjectBytes)), StringComparison.Ordinal)
            || receipt.Issuer != enrollment.Issuer
            || receipt.KeyFingerprint != enrollment.ReceiptKeyFingerprint || receipt.Audience != enrollment.Audience
            || !((receipt.Kind, receipt.Action) is ("producer-execution", "capture") or ("grant", "grant-session")
                or ("target", "observe-target") or ("custody", "retain"))
            || !ValidPrincipal(receipt.Principal)
            || receipt.SubjectRef != "sha256:" + receipt.SubjectSha256 || string.IsNullOrWhiteSpace(receipt.TenantId)
            || string.IsNullOrWhiteSpace(receipt.ClusterId) || string.IsNullOrWhiteSpace(receipt.NamespaceUid)
            || string.IsNullOrWhiteSpace(receipt.SessionId) || string.IsNullOrWhiteSpace(receipt.SourceRevision)
            || string.IsNullOrWhiteSpace(receipt.RegistryRevision) || string.IsNullOrWhiteSpace(receipt.PolicyRevision)
            || string.IsNullOrWhiteSpace(receipt.GrantId) || string.IsNullOrWhiteSpace(receipt.Decision)
            || !P1ReceiptWireV1.Encode(expected).AsSpan().SequenceEqual(P1ReceiptWireV1.Encode(receipt)))
        {
            return false;
        }

        if (!ValidOrigin(enrollment.RetrievalOrigin))
        {
            return false;
        }

        return retrievalUri == enrollment.RetrievalOrigin + "/v1/receipts/" + receipt.ReceiptId
            && enrollment.EffectiveAtUtc.Offset == TimeSpan.Zero && enrollment.ExpiresAtUtc.Offset == TimeSpan.Zero
            && enrollment.EffectiveAtUtc <= receipt.IssuedAtUtc && enrollment.EffectiveAtUtc <= now
            && now < enrollment.ExpiresAtUtc && receipt.ExpiresAtUtc <= enrollment.ExpiresAtUtc
            && receipt.IssuedAtUtc <= now && now < receipt.ExpiresAtUtc
            && receipt.ExpiresAtUtc - receipt.IssuedAtUtc > TimeSpan.Zero
            && receipt.ExpiresAtUtc - receipt.IssuedAtUtc <= TimeSpan.FromDays(7);
    }

    private static bool StatusValid(P1StatusClaims status, P1ReceiptClaims receipt, P1ReceiptEnrollment enrollment,
        string? statusUri,
        string nonce, DateTimeOffset now)
        => ValidOrigin(enrollment.StatusOrigin)
            && statusUri == enrollment.StatusOrigin + "/v1/status/" + receipt.ReceiptId + "?nonce=" + nonce
            && status.Issuer == enrollment.Issuer && status.TrustRevision == enrollment.TrustRevision
            && status.ReceiptId == receipt.ReceiptId && status.KeyFingerprint == receipt.KeyFingerprint
            && status.GrantId == receipt.GrantId && status.SessionId == receipt.SessionId
            && status.PolicyRevision == receipt.PolicyRevision
            && status.Nonce == nonce && status.ReceiptState == "active" && status.KeyState == "active"
            && status.GrantState == "active" && status.PolicyState == "active" && status.SessionState == "active"
            && enrollment.EffectiveAtUtc <= status.ObservedAtUtc && status.ExpiresAtUtc <= enrollment.ExpiresAtUtc
            && status.ObservedAtUtc <= now && now < status.ExpiresAtUtc
            && now - status.ObservedAtUtc <= TimeSpan.FromSeconds(300)
            && status.ExpiresAtUtc - status.ObservedAtUtc > TimeSpan.Zero
            && status.ExpiresAtUtc - status.ObservedAtUtc <= TimeSpan.FromSeconds(300);

    private static bool VerifySignature(P1SignedDocument document, byte[] spki, string fingerprint)
    {
        if (spki is not { Length: > 0 and <= 512 } || document.Signature.Length != 64
            || !Hex(fingerprint) || !string.Equals(Convert.ToHexStringLower(SHA256.HashData(spki)), fingerprint, StringComparison.Ordinal))
        {
            return false;
        }

        using ECDsa key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(spki, out int read);
        return read == spki.Length && key.KeySize == 256 && key.ExportParameters(false).Curve.Oid.Value == P256CurveOid
            && key.VerifyData(document.Payload, document.Signature, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static bool Hex(string? value)
        => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool ValidPrincipal(string? value)
        => value is not null && ((value.StartsWith("platform:actor:", StringComparison.Ordinal) && value.Length > 15)
            || (value.StartsWith("platform:workload:", StringComparison.Ordinal) && value.Length > 18));

    private static bool ValidOrigin(string? value)
        => value is not null && Uri.TryCreate(value, UriKind.Absolute, out Uri? origin)
            && origin.Scheme == Uri.UriSchemeHttps && origin.AbsolutePath == "/"
            && string.IsNullOrEmpty(origin.UserInfo) && string.IsNullOrEmpty(origin.Query)
            && string.IsNullOrEmpty(origin.Fragment) && !value.EndsWith('/');
}
