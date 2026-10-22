using System.Text.Json;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed record TrustedSigner(string KeyId, string Purpose, string PublicKeyPem);
internal sealed record BucketIdentity(string Name, string OwnerAccountId, string EnrollmentKey, string EnrollmentVersion,
    string EnrollmentSha256);
internal sealed record NativeEpoch(string OpaqueId, string KeyArn, string KeyId, string TenantId);
internal sealed record ComponentProfile(AuthorityEnrollmentV1 Enrollment, string Region, string TableArn, string TableId,
    BucketIdentity Checkpoints, BucketIdentity Originals, DateTimeOffset RetainUntil, long CompletionReserveBytes,
    string SocketPath, string ComponentSigningKeyPath, string ComponentPublicKeyPem, TrustedSigner[] Trust,
    NativeEpoch[] Epochs, string[] Tenants, string QualificationStatementPath)
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
            || Checkpoints.Name == Originals.Name || Checkpoints.OwnerAccountId.Length != 12 || Originals.OwnerAccountId.Length != 12
            || !CustodyBindingContract.Digest(Checkpoints.EnrollmentSha256) || !CustodyBindingContract.Digest(Originals.EnrollmentSha256)
            || CompletionReserveBytes <= 0 || CompletionReserveBytes >= 32 * 1024 * 1024 || !Path.IsPathFullyQualified(SocketPath)
            || Trust.Length < 3 || Trust.Select(t => t.KeyId).Distinct(StringComparer.Ordinal).Count() != Trust.Length)
        { throw new AuthorityUnavailableException("Incomplete pre-enrolled component profile; startup cannot enroll resources."); }
        foreach (string purpose in new[] { "private-operation", "control", "independent-observer" })
            if (Trust.Count(t => t.Purpose == purpose) != 1) throw new AuthorityUnavailableException("Independent trust role missing.");
        if (Trust.Select(t => t.PublicKeyPem).Append(ComponentPublicKeyPem).Distinct(StringComparer.Ordinal).Count() != Trust.Length + 1)
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
    string CurrentRevocationSha256, long CurrentGuardRevision, long? EffectiveDispatchRevision, string? EffectiveDispatchReceiptId);
internal sealed record PreparationClaim(string ManifestDigest, string SourceEpoch, string SuccessorEpoch,
    string InventoryDigest, string SurvivorCheckpointDigest, string ProducerClosureDigest, string HolderClosureDigest,
    string[] PermanentlyDeletedAlternateEpochs, string[] SurvivorOpaqueReferences, string[] TargetOpaqueReferences,
    string NativeIntegrityEvidenceDigest, string RestoreEvidenceDigest);
internal sealed record FinalClaim(string ManifestDigest, string SourceEpoch, string SuccessorEpoch, string PreparationDigest,
    string[] PermanentlyDeletedEpochs, string InventoryDigest, string HolderClosureDigest, string SurvivorRecoveryDigest,
    string RestoreBarrierReceiptId, string NativeFinalEvidenceDigest, string OriginalSha256);
