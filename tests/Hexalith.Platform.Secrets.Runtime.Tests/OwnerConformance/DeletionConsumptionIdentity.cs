using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Narrow private bounded identity/manifest capture; no caller classification or signature grants authority.</summary>
internal static class DeletionConsumptionIdentity
{
    internal static void Text(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || new UTF8Encoding(false, true).GetByteCount(value) > 2048)
        { throw new ArgumentException("Invalid protection identity."); }
    }
    internal static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    internal static string TargetDigest(IReadOnlyList<ProtectionTarget> targets) => DeletionBatchCapabilityIdentity.TargetManifestDigest(targets);
    internal static DeletionBatchConsumptionRequest Capture(DeletionBatchConsumptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); var c = request.Capability; ArgumentNullException.ThrowIfNull(c);
        foreach (string s in new[] { c.Issuer, c.Audience, c.TenantId, c.DeletionRequestId, c.DestructionSealId, c.BatchKind, c.BatchId,
            c.ManifestDigest, c.GuardStreamId, c.CapabilityKeyVersion, request.DispatchReceiptId }) { Text(s); }
        _ = new AggregateIdentity(c.TenantId, "protection", c.BatchId);
        const long max = 9007199254740991;
        if (c.BatchOrdinal < 0 || c.BatchOrdinal > max || c.IntendedIssuedGuardRevision is <= 0 or > max
            || c.AttestationOrdinal is <= 0 or > max || c.SigningAttemptOrdinal is <= 0 or > max
            || request.CommittedIssuedGuardRevision <= 0 || request.DispatchGuardRevision < request.CommittedIssuedGuardRevision
            || request.DetachedJws is not { Length: > 0 and <= 16384 } || request.Targets is null || request.Targets.Count is < 1 or > 1000)
        { throw new ArgumentException("Invalid protection request."); }
        var targets = new List<ProtectionTarget>(); ProtectionTarget? previous = null;
        foreach (var target in request.Targets)
        {
            if (targets.Count >= 1000 || target is null || target.TenantId != c.TenantId) { throw new ArgumentException("Invalid protection target."); }
            Text(target.TenantId); Text(target.AgentInteractionId); Text(target.TargetProtectionKeyAlias);
            if (previous is not null)
            {
                int interactionOrder = StringComparer.Ordinal.Compare(previous.AgentInteractionId, target.AgentInteractionId);
                if (interactionOrder > 0 || interactionOrder == 0 && StringComparer.Ordinal.Compare(previous.TargetProtectionKeyAlias, target.TargetProtectionKeyAlias) >= 0)
                { throw new ArgumentException("Unsorted or duplicate protection target."); }
            }
            previous = target; targets.Add(target);
        }
        var owned = Array.AsReadOnly(targets.ToArray());
        if (c.ManifestDigest != TargetDigest(owned)) { throw new ArgumentException("Protection manifest mismatch."); }
        return request with { Targets = owned };
    }
    internal static bool Same(DeletionBatchConsumptionRequest a, DeletionBatchConsumptionRequest b) => Digest(a) == Digest(b);
    internal static bool SameBatch(DeletionBatchConsumptionRequest a, DeletionBatchConsumptionRequest b)
    {
        var x = a.Capability; var y = b.Capability;
        return x.TenantId == y.TenantId && x.Issuer == y.Issuer && x.Audience == y.Audience && x.DeletionRequestId == y.DeletionRequestId
            && x.DestructionSealId == y.DestructionSealId && x.BatchKind == y.BatchKind && x.BatchOrdinal == y.BatchOrdinal
            && x.BatchId == y.BatchId && x.ManifestDigest == y.ManifestDigest && x.GuardStreamId == y.GuardStreamId && a.Targets.SequenceEqual(b.Targets);
    }
    internal static DeletionBlockedReplacementReconciliation Capture(DeletionBlockedReplacementReconciliation request)
    {
        ArgumentNullException.ThrowIfNull(request); var c = request.Capability;
        if (request.SigningRequestId != DeletionBatchCapabilityIdentity.SigningRequestId(c) || request.DetachedJws is not { Length: > 0 and <= 16384 }
            || request.CommittedIssuedGuardRevision <= 0 || request.ExpectedKeyBlockSetRevision <= 0 || request.Targets is null || request.Targets.Count is < 1 or > 1000)
        { throw new ArgumentException("Malformed blocked replacement."); }
        foreach (string text in new[] { request.OperationId, request.CompromiseBlockReceiptId, request.GuardReplacementReceiptId }) { Text(text); }
        var targets = new List<ProtectionTarget>();
        foreach (var target in request.Targets)
        {
            if (targets.Count >= 1000 || target is null || target.TenantId != c.TenantId) { throw new ArgumentException("Malformed blocked replacement target."); }
            Text(target.TenantId); Text(target.AgentInteractionId); Text(target.TargetProtectionKeyAlias); targets.Add(target);
        }
        if (TargetDigest(targets) != c.ManifestDigest) { throw new ArgumentException("Changed blocked replacement manifest."); }
        Revocation(request.RevocationReceipt.Envelope);
        var revocation = request.RevocationReceipt;
        if (revocation.Envelope.TenantId != c.TenantId || revocation.Envelope.KeyVersion != c.CapabilityKeyVersion
            || revocation.ReceiptId != Digest(revocation.Envelope) || revocation.KeyBlockSetRevision <= 0 || revocation.OwnerRevision <= 0
            || revocation.KeyBlockSetRevision > request.ExpectedKeyBlockSetRevision || revocation.AffectedBatchIds.Count > 1000)
        { throw new ArgumentException("Changed blocked replacement revocation."); }
        return request with { Targets = targets.AsReadOnly(), RevocationReceipt = revocation with { AffectedBatchIds = Array.AsReadOnly(revocation.AffectedBatchIds.ToArray()) } };
    }
    internal static bool SameBatch(DeletionBatchConsumptionRequest original, DeletionBlockedReplacementReconciliation replacement)
    {
        var x = original.Capability; var y = replacement.Capability;
        return x.TenantId == y.TenantId && x.Issuer == y.Issuer && x.Audience == y.Audience && x.DeletionRequestId == y.DeletionRequestId
            && x.DestructionSealId == y.DestructionSealId && x.BatchKind == y.BatchKind && x.BatchOrdinal == y.BatchOrdinal
            && x.BatchId == y.BatchId && x.ManifestDigest == y.ManifestDigest && x.GuardStreamId == y.GuardStreamId && original.Targets.SequenceEqual(replacement.Targets);
    }
    internal static void Revocation(DeletionCapabilityRevocationEnvelope e)
    {
        ArgumentNullException.ThrowIfNull(e);
        foreach (string s in new[] { e.Issuer, e.Audience, e.TenantId, e.KeyFamily, e.KeyVersion, e.EventIdentity, e.SignatureDigest }) { Text(s); }
        if (e.KeyFamily != "DeletionBatchCapabilitySigningKey" || e.RevocationRevision <= 0 || e.TrustProfileRevision <= 0
            || e.SignatureDigest.Length != 64 || e.SignatureDigest.Any(c => !char.IsAsciiHexDigit(c))) { throw new ArgumentException("Invalid protection revocation."); }
    }
}
