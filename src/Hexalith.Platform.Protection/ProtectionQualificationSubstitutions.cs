using System.Text;
using System.Text.Json;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.Platform.Protection;

/// <summary>Closed concrete negative inventory from the owner plan. Every variant is sent to the real production owner; no expected result is an input.</summary>
public static class ProtectionQualificationSubstitutions
{
    /// <summary>Builds all concrete canonical/header, immutable dispatch and manifest substitutions for the exact owner action.</summary>
    public static IReadOnlyList<ProtectionQualificationSubstitution> Create(string action, DeletionBatchConsumptionRequest original, string otherTenant)
    {
        var result = new List<ProtectionQualificationSubstitution>(); var cap = original.Capability;
        void Add(string label, DeletionBatchConsumptionRequest request) => result.Add(new(label, request));
        void Capability(string label, DeletionBatchCapabilityV1 changed) => Add(label, original with { Capability = changed });
        if (action == "RejectCanonicalHeaderPayloadSubstitutions")
        {
            Capability("Issuer", cap with { Issuer = cap.Issuer + "-substituted" }); Capability("Audience", cap with { Audience = cap.Audience + "-substituted" });
            Capability("TenantId", cap with { TenantId = otherTenant }); Capability("DeletionRequestId", cap with { DeletionRequestId = cap.DeletionRequestId + "-substituted" });
            Capability("DestructionSealId", cap with { DestructionSealId = cap.DestructionSealId + "-substituted" }); Capability("BatchKind", cap with { BatchKind = cap.BatchKind == "AcceptedSet" ? "Containment" : "AcceptedSet" });
            Capability("BatchOrdinal", cap with { BatchOrdinal = checked(cap.BatchOrdinal + 1) }); Capability("BatchId", cap with { BatchId = cap.BatchId + "-substituted" });
            Capability("ManifestDigest", cap with { ManifestDigest = new string(cap.ManifestDigest[0] == 'A' ? 'B' : 'A', 64) }); Capability("GuardStreamId", cap with { GuardStreamId = cap.GuardStreamId + "-substituted" });
            Capability("IntendedIssuedGuardRevision", cap with { IntendedIssuedGuardRevision = checked(cap.IntendedIssuedGuardRevision + 1) });
            Capability("AttestationOrdinal", cap with { AttestationOrdinal = checked(cap.AttestationOrdinal + 1) }); Capability("SigningAttemptOrdinal", cap with { SigningAttemptOrdinal = checked(cap.SigningAttemptOrdinal + 1) });
            Capability("CapabilityKeyVersion", cap with { CapabilityKeyVersion = cap.CapabilityKeyVersion + "-substituted" });
            string[] compact = original.DetachedJws.Split('.');
            if (compact.Length != 3 || compact[1].Length != 0) { throw new InvalidOperationException("Actual detached signed original required."); }
            foreach (var header in new[] { new { alg = "none", kid = cap.CapabilityKeyVersion }, new { alg = "HS256", kid = cap.CapabilityKeyVersion }, new { alg = "ES256", kid = cap.CapabilityKeyVersion + "-substituted" } })
            {
                string encoded = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(header)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                Add("ProtectedHeader-" + header.alg + "-" + (header.kid == cap.CapabilityKeyVersion ? "original-key" : "other-key"), original with { DetachedJws = encoded + ".." + compact[2] });
            }
            Add("SignatureBytes", original with { DetachedJws = compact[0] + ".." + (compact[2][0] == 'A' ? "B" : "A") + compact[2][1..] });
        }
        else if (action == "RejectTenantKeyAttestationDispatchRevisionSubstitutions")
        {
            Capability("TenantId", cap with { TenantId = otherTenant }); Capability("CapabilityKeyVersion", cap with { CapabilityKeyVersion = cap.CapabilityKeyVersion + "-substituted" });
            Capability("AttestationOrdinal", cap with { AttestationOrdinal = checked(cap.AttestationOrdinal + 1) }); Capability("GuardStreamId", cap with { GuardStreamId = cap.GuardStreamId + "-substituted" });
            Add("CommittedIssuedGuardRevision", original with { CommittedIssuedGuardRevision = checked(original.CommittedIssuedGuardRevision + 1) });
            Add("DispatchGuardRevision", original with { DispatchGuardRevision = checked(original.DispatchGuardRevision + 1) }); Add("DispatchReceiptId", original with { DispatchReceiptId = original.DispatchReceiptId + "-substituted" });
        }
        else if (action is "RejectChangedManifest" or "RejectCrossTenantManifest")
        {
            var targets = original.Targets.ToArray();
            if (action == "RejectCrossTenantManifest")
            {
                var foreign = targets.Select(target => target with { TenantId = otherTenant }).ToArray();
                Add("WholeForeignTenant", original with { Capability = cap with { TenantId = otherTenant, ManifestDigest = DeletionBatchCapabilityIdentity.TargetManifestDigest(foreign) }, Targets = foreign });
                Add("ForeignTarget", original with { Targets = targets.Select((target, index) => index == 0 ? target with { TenantId = otherTenant } : target).ToArray() });
            }
            else
            {
                Add("TargetAlias", original with { Targets = targets.Select((target, index) => index == 0 ? target with { TargetProtectionKeyAlias = target.TargetProtectionKeyAlias + "-substituted" } : target).ToArray() });
                Add("TargetIdentity", original with { Targets = targets.Select((target, index) => index == 0 ? target with { AgentInteractionId = target.AgentInteractionId + "-substituted" } : target).ToArray() });
                if (targets.Length > 1) { Add("TargetOrder", original with { Targets = targets.Reverse().ToArray() }); Add("MissingTarget", original with { Targets = targets[..^1] }); }
                Capability("ManifestDigest", cap with { ManifestDigest = new string(cap.ManifestDigest[0] == 'A' ? 'B' : 'A', 64) });
            }
        }
        else { throw new InvalidOperationException("Closed negative owner action required."); }
        if (result.Count == 0 || result.Any(value => ErasureRootCustody.Hash(value.Request) == ErasureRootCustody.Hash(original))) { throw new InvalidOperationException("Every negative variant must change the exact original."); }
        return result;
    }
}
