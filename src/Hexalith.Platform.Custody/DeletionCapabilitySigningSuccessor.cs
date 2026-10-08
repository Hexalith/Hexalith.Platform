using System.Security.Cryptography;
using System.Text;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Custody;

/// <summary>Closed pre-issue successor construction from a durably obsolete original; it grants no signing/issue authority or renewal.</summary>
public static class DeletionCapabilitySigningSuccessor
{
    /// <summary>Retains stable seal/batch/manifest/attestation identity; changes only attempt, intended guard revision and current healthy key.</summary>
    /// <param name="original">Original independently confirmed obsolete signer outcome.</param><param name="evaluatedAt">Current consuming clock.</param>
    /// <returns>The deterministic next payload, or null if proof is missing, stale or inconsistent.</returns>
    public static DeletionBatchCapabilityV1? Create(DeletionCapabilitySigningOutcome original, DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (original.State != DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued || original.NoIssueProof is not { } proof
            || !Valid(original, proof, evaluatedAt) || original.Payload.SigningAttemptOrdinal == long.MaxValue) { return null; }
        return original.Payload with { SigningAttemptOrdinal = original.Payload.SigningAttemptOrdinal + 1,
            IntendedIssuedGuardRevision = proof.CurrentGuardRevision, CapabilityKeyVersion = proof.CurrentHealthyKeyVersion };
    }

    /// <summary>Validates exact immutable artifact/payload scope and exclusive current no-issue authority, without asserting cryptographic source authenticity.</summary>
    internal static bool Valid(DeletionCapabilitySigningOutcome original, DeletionCapabilityNoIssueProof proof, DateTimeOffset now)
        => proof.Payload == original.Payload && proof.SigningRequestId == original.SigningRequestId
            && original.SigningRequestId == DeletionBatchCapabilityCodec.SigningRequestId(original.Payload)
            && original.DetachedJws is { Length: > 0 and <= 16384 }
            && proof.DetachedJwsDigest == Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(original.DetachedJws)))
            && proof.CurrentGuardRevision > original.Payload.IntendedIssuedGuardRevision && Text(proof.CurrentHealthyKeyVersion)
            && Text(proof.ProofId) && Text(proof.AuthorityRevision) && proof.ObservedAt != default && proof.ObservedAt <= now && proof.ValidUntil > now;
    private static bool Text(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 2048;
}
