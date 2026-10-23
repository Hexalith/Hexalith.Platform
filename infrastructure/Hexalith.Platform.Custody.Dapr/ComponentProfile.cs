using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed record TrustedSigner(string KeyId, string Purpose, string PublicKeyPem);
internal sealed record BucketIdentity(string Name, string OwnerAccountId, string EnrollmentKey, string EnrollmentVersion,
    string EnrollmentSha256);
internal sealed record NativeEpoch(string OpaqueId, string KeyArn, string KeyId, string TenantId);
internal sealed record TenantKek(string TenantId, string Version, string ProtectorArn, string ProtectorKeyId, byte[] EncryptedKey);
internal sealed record ComponentProfile(AuthorityEnrollmentV1 Enrollment, string Region, string TableArn, string TableId,
    BucketIdentity Checkpoints, BucketIdentity Originals, DateTimeOffset RetainUntil, long CompletionReserveBytes,
    string SocketPath, string ComponentSigningKeyPath, string ComponentPublicKeyPem, TrustedSigner[] Trust,
    NativeEpoch[] Epochs, string[] Tenants, string QualificationStatementPath, TenantKek[] Keks)
{
    internal string EnrollmentDigest => CustodyJson.Hash(CustodyJson.Bytes(Enrollment));
    // This includes private resource pins and trust, so a restored config cannot silently change them through Init.
    internal string ProfileDigest => CustodyJson.Hash(CustodyJson.Bytes(this));
    internal void Validate()
    {
        if (!CustodyBindingContract.ValidEnrollment(Enrollment) || string.IsNullOrWhiteSpace(Region)
            || !TableArn.StartsWith("arn:aws:dynamodb:", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(TableId)
            || Tenants.Length != 2 || Tenants.Distinct(StringComparer.Ordinal).Count() != 2
            || Epochs.Length > 5 || Epochs.Select(e => e.OpaqueId).Distinct(StringComparer.Ordinal).Count() != Epochs.Length
            || Epochs.Any(e => !Tenants.Contains(e.TenantId) || !e.KeyArn.StartsWith("arn:aws:kms:", StringComparison.Ordinal))
            || Keks.Length != 2 || Keks.Select(k => k.ProtectorArn).Distinct().Count() != 2
            || Keks.Any(k => !Tenants.Contains(k.TenantId) || k.EncryptedKey.Length == 0)
            || Checkpoints.Name == Originals.Name || Checkpoints.OwnerAccountId.Length != 12 || Originals.OwnerAccountId.Length != 12
            || !CustodyBindingContract.Digest(Checkpoints.EnrollmentSha256) || !CustodyBindingContract.Digest(Originals.EnrollmentSha256)
            || CompletionReserveBytes <= 0 || CompletionReserveBytes >= 32 * 1024 * 1024 || !Path.IsPathFullyQualified(SocketPath)
            || Trust.Length < 3 || Trust.Select(t => t.KeyId).Distinct(StringComparer.Ordinal).Count() != Trust.Length)
        { throw new AuthorityUnavailableException("Incomplete pre-enrolled component profile; startup cannot enroll resources."); }
        if (Tenants.Any(tenant => Epochs.Count(e => e.TenantId == tenant) < 2 || Keks.Count(k => k.TenantId == tenant) != 1))
            throw new AuthorityUnavailableException("Each tenant requires independent source/successor epochs and exactly one KEK mapping.");
        var nativeKeys = Epochs.Select(e => (Arn: e.KeyArn, Id: e.KeyId)).Concat(Keks.Select(k => (Arn: k.ProtectorArn, Id: k.ProtectorKeyId))).ToArray();
        if (nativeKeys.Any(k => string.IsNullOrWhiteSpace(k.Id) || !k.Arn.StartsWith("arn:aws:kms:", StringComparison.Ordinal))
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

// The payload is a closed typed claim per purpose, never a caller 'verified=true' field.
internal sealed record SignedStatement(string Purpose, string SignerId, string AuthorityId, string AuthorityIncarnation,
    string TenantId, long Revision, string CheckpointDigest, string SubjectSha256, DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt, JsonElement Payload, byte[] Signature);
internal sealed record OperationPermission(string Operation, string OperationId, string RequestAttemptId, string Challenge,
    string OwnerPurposeScope, string ProfileDigest);
internal sealed record QualificationClaim(string ProfileDigest, string ArtifactDigest, string TransportProfileDigest,
    string ClosureProfileDigest, string CapacityMeasurementDigest, long CompletionReserveBytes,
    string IndependentPlacementEvidenceDigest, string NativeAuthorizationDigest);
internal sealed record ControlClaim(string ControlProofSha256, string GuardOriginalSha256, string ProtectionOriginalSha256,
    string CurrentRevocationSha256, long CurrentGuardRevision, long? EffectiveDispatchRevision, string? EffectiveDispatchReceiptId,
    HoldReservation? HoldReservation = null);
// The control owner durably excludes dispatch until this exact reservation is reconciled. It cannot
// expire on a timer or be canceled from a local app restore. Its signed current claim is not a read-then-write check.
internal sealed record HoldReservation(string ReservationId, string HoldId, string TargetDigest, long RegisteredGuardRevision,
    string RegisteredGuardReceiptId, string State);
internal sealed record PreparationClaim(string ManifestDigest, string SourceEpoch, string SuccessorEpoch,
    string InventoryDigest, string SurvivorCheckpointDigest, string ProducerClosureDigest, string HolderClosureDigest,
    string[] PermanentlyDeletedAlternateEpochs, string[] SurvivorOpaqueReferences, string[] TargetOpaqueReferences,
    string NativeIntegrityEvidenceDigest, string RestoreEvidenceDigest);
internal sealed record FinalClaim(string ManifestDigest, string SourceEpoch, string SuccessorEpoch, string PreparationDigest,
    string[] PermanentlyDeletedEpochs, string InventoryDigest, string HolderClosureDigest, string SurvivorRecoveryDigest,
    string RestoreBarrierReceiptId, string NativeFinalEvidenceDigest, string OriginalSha256);
