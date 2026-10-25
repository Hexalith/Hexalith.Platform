using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed record ComponentProfile(AuthorityEnrollmentV1 Enrollment, string Region, string TableArn, string TableId,
    BucketIdentity Checkpoints, BucketIdentity Originals, DateTimeOffset RetainUntil, long CompletionReserveBytes,
    string SocketPath, string ComponentSigningKeyPath, string ComponentPublicKeyPem, TrustedSigner[] Trust,
    NativeEpoch[] Epochs, string[] Tenants, string QualificationStatementPath, TenantKek[] Keks, NativeInstallation? Native = null)
{
    internal string EnrollmentDigest => CustodyJson.Hash(CustodyJson.Bytes(Enrollment));
    // This includes private resource pins and trust, so a restored config cannot silently change them through Init.
    internal string ProfileDigest => CustodyJson.Hash(CustodyJson.Bytes(this));
    internal void Validate()
    {
        bool native = Native is not null;
        if (native && (Native!.Backend != "OpenBaoTransit" || !Path.IsPathFullyQualified(Native.CheckpointArchiveDirectory)
            || !Path.IsPathFullyQualified(Native.OriginalArchiveDirectory) || Native.CheckpointArchiveDirectory == Native.OriginalArchiveDirectory
            || !CustodyBindingContract.Digest(Native.ArchiveEnrollmentSha256) || !CustodyBindingContract.Digest(Native.PhysicalQualificationEvidenceSha256)
            || !CustodyBindingContract.Text(Native.AuthorityComponent) || !CustodyBindingContract.Text(Native.AuthorityKeyPrefix)
            || !Path.IsPathFullyQualified(Native.CredentialFile) || Native.TransitMount.Length is 0 or > 128
            || Native.TransitMount.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))))
            throw new AuthorityUnavailableException("Incomplete independently enrolled non-AWS installation; startup cannot select or qualify resources.");
        bool Resource(string value) => native ? value.StartsWith("openbao-transit:", StringComparison.Ordinal) : value.StartsWith("arn:aws:kms:", StringComparison.Ordinal);
        if (!CustodyBindingContract.ValidEnrollment(Enrollment) || !native && (string.IsNullOrWhiteSpace(Region)
            || !TableArn.StartsWith("arn:aws:dynamodb:", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(TableId))
            || Tenants.Length != 2 || Tenants.Distinct(StringComparer.Ordinal).Count() != 2
            || Epochs.Length > 5 || Epochs.Select(e => e.OpaqueId).Distinct(StringComparer.Ordinal).Count() != Epochs.Length
            || Epochs.Any(e => !Tenants.Contains(e.TenantId) || !Resource(e.KeyArn))
            || Keks.Length != 2 || Keks.Select(k => k.ProtectorArn).Distinct().Count() != 2
            || Keks.Any(k => !Tenants.Contains(k.TenantId) || k.EncryptedKey.Length == 0)
            || !native && (Checkpoints.Name == Originals.Name || Checkpoints.OwnerAccountId.Length != 12 || Originals.OwnerAccountId.Length != 12
            || !CustodyBindingContract.Digest(Checkpoints.EnrollmentSha256) || !CustodyBindingContract.Digest(Originals.EnrollmentSha256))
            || CompletionReserveBytes <= 0 || CompletionReserveBytes >= 32 * 1024 * 1024 || !Path.IsPathFullyQualified(SocketPath)
            || Trust.Length < 3 || Trust.Select(t => t.KeyId).Distinct(StringComparer.Ordinal).Count() != Trust.Length)
        { throw new AuthorityUnavailableException("Incomplete pre-enrolled component profile; startup cannot enroll resources."); }
        if (Tenants.Any(tenant => Epochs.Count(e => e.TenantId == tenant) < 2 || Keks.Count(k => k.TenantId == tenant) != 1))
            throw new AuthorityUnavailableException("Each tenant requires independent source/successor epochs and exactly one KEK mapping.");
        var nativeKeys = Epochs.Select(e => (Arn: e.KeyArn, Id: e.KeyId)).Concat(Keks.Select(k => (Arn: k.ProtectorArn, Id: k.ProtectorKeyId))).ToArray();
        if (nativeKeys.Any(k => string.IsNullOrWhiteSpace(k.Id) || !Resource(k.Arn))
            || nativeKeys.Select(k => k.Arn).Distinct(StringComparer.Ordinal).Count() != nativeKeys.Length
            || nativeKeys.Select(k => k.Id).Distinct(StringComparer.Ordinal).Count() != nativeKeys.Length)
            throw new AuthorityUnavailableException("Native epoch and KEK identities must be disjoint across every lane.");
        foreach (string purpose in new[] { "private-operation", "control", "independent-observer" })
            if (Trust.Count(t => t.Purpose == purpose) != 1) throw new AuthorityUnavailableException("Independent trust role missing.");
        string Fingerprint(string pem)
        {
            using var key = System.Security.Cryptography.ECDsa.Create(); key.ImportFromPem(pem);
            if (key.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7") throw new AuthorityUnavailableException("P-256 public trust required.");
            return CustodyJson.Hash(key.ExportSubjectPublicKeyInfo());
        }
        if (Trust.Select(t => Fingerprint(t.PublicKeyPem)).Append(Fingerprint(ComponentPublicKeyPem)).Distinct(StringComparer.Ordinal).Count() != Trust.Length + 1)
            throw new AuthorityUnavailableException("Signing roles must be independent.");
    }
    internal static ComponentProfile Load(string path) => CustodyJson.Read<ComponentProfile>(File.ReadAllBytes(path));
}
