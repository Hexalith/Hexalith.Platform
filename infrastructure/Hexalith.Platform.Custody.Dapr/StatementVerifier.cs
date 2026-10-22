using System.Security.Cryptography;
using Hexalith.Platform.Secrets.Runtime;
namespace Hexalith.Platform.Custody.Dapr;

internal sealed class StatementVerifier(ComponentProfile profile, TimeProvider clock)
{
    internal bool Verify(SignedStatement statement, string purpose, CurrentState state, string subject, TimeSpan maximumAge)
    {
        DateTimeOffset now = clock.GetUtcNow();
        if (statement.Purpose != purpose || statement.AuthorityId != profile.Enrollment.AuthorityId
            || statement.AuthorityIncarnation != profile.Enrollment.AuthorityIncarnation
            || statement.TenantId != state.Checkpoint.TenantId || statement.Revision != state.Head.Revision
            || statement.CheckpointDigest != state.Head.Digest || statement.SubjectSha256 != subject
            || statement.IssuedAt > now || statement.ExpiresAt <= now || statement.ExpiresAt - statement.IssuedAt > maximumAge
            || now - statement.IssuedAt > maximumAge || state.Checkpoint.RevokedSignerIds.Contains(statement.SignerId)
            || statement.Signature.Length is 0 or > 8192) return false;
        TrustedSigner? signer = profile.Trust.SingleOrDefault(t => t.KeyId == statement.SignerId && t.Purpose == purpose);
        if (signer is null) return false;
        try
        {
            using var key = ECDsa.Create(); key.ImportFromPem(signer.PublicKeyPem);
            return key.VerifyData(CustodyJson.Bytes(statement with { Signature = [] }), statement.Signature,
                HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (CryptographicException) { return false; }
    }
    internal bool Authorize(CustodyBindingRequestV1 request, ReadOnlySpan<byte> exactBytes, SignedStatement operation,
        SignedStatement control, CurrentState state)
    {
        if (request.Envelope.Enrollment != profile.Enrollment
            || !Verify(operation, "private-operation", state, CustodyJson.Hash(exactBytes), TimeSpan.FromSeconds(30))
            || !Verify(control, "control", state, CustodyJson.Hash(CustodyJson.Bytes(request.Envelope.Proof)), TimeSpan.FromSeconds(30))) return false;
        var permission = CustodyJson.Read<OperationPermission>(CustodyJson.Bytes(operation.Payload));
        var e = request.Envelope;
        if (permission != new OperationPermission(e.Operation, e.OperationId, e.RequestAttemptId, e.Challenge, e.OwnerPurposeScope, profile.ProfileDigest)) return false;
        var facts = CustodyJson.Read<ControlClaim>(CustodyJson.Bytes(control.Payload));
        if (facts.ControlProofSha256 != control.SubjectSha256 || !CustodyBindingContract.Digest(facts.GuardOriginalSha256)
            || !CustodyBindingContract.Digest(facts.CurrentRevocationSha256) || facts.CurrentGuardRevision < e.Proof.DispatchRevision) return false;
        // A later hold is denied even if custody has not yet seen the protection reservation.
        if (e.Operation == "pin") return false; // No installed guard/custody atomic handoff; snapshot freshness is insufficient.
        if (e.Operation == "destroy-manifest" && (facts.EffectiveDispatchRevision != e.Proof.DispatchRevision
            || facts.EffectiveDispatchReceiptId != e.Proof.DispatchReceiptId || !CustodyBindingContract.Digest(facts.ProtectionOriginalSha256))) return false;
        return true;
    }
    internal bool Qualified(CurrentState state)
    {
        if (!File.Exists(profile.QualificationStatementPath)) return false;
        try
        {
            var signed = CustodyJson.Read<SignedStatement>(File.ReadAllBytes(profile.QualificationStatementPath));
            if (!Verify(signed, "independent-observer", state, profile.ProfileDigest, TimeSpan.FromSeconds(30))) return false;
            var q = CustodyJson.Read<QualificationClaim>(CustodyJson.Bytes(signed.Payload));
            return q.ProfileDigest == profile.ProfileDigest && q.ArtifactDigest == profile.Enrollment.ArtifactProfileDigest
                && q.CompletionReserveBytes == profile.CompletionReserveBytes
                && new[] { q.TransportProfileDigest, q.ClosureProfileDigest, q.CapacityMeasurementDigest,
                    q.IndependentPlacementEvidenceDigest, q.NativeAuthorizationDigest }.All(CustodyBindingContract.Digest);
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or CryptographicException) { return false; }
    }
}
