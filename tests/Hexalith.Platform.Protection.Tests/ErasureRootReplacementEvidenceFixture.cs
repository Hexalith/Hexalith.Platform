using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;

using NSubstitute;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Ephemeral ES256 successor evidence; synthetic independent currentness and owner bootstrap are component evidence only.</summary>
internal sealed class ErasureRootReplacementEvidenceFixture
{
    /// <summary>Creates genuine signed originals with synthetic independent installed evidence.</summary>
    internal ErasureRootReplacementEvidenceFixture()
    {
        Original = Signed("key-v1", 1, 7, out PublishedDeletionCapabilityTrust originalTrust);
        Replacement = Signed("key-v2", 2, 17, out PublishedDeletionCapabilityTrust replacementTrust);
        Trust = [originalTrust, replacementTrust];
        var capability = Replacement.Capability;
        var envelope = new DeletionCapabilityRevocationEnvelope(capability.Issuer, "registrar-v1", capability.TenantId,
            "DeletionBatchCapabilitySigningKey", capability.CapabilityKeyVersion, 1, 3, "compromise-key-v2", new string('A', 64));
        var revocation = new DeletionCapabilityRevocationReceipt(envelope, 3, 2, ErasureRootCustody.Hash(envelope), []);
        Activation = new("activate-exact", "old-owner-block", 1, "replacement-issue", Replacement);
        Blocked = new("reconcile-exact", Activation.CompromiseBlockReceiptId, 2, Activation.GuardReplacementReceiptId,
            capability, Replacement.DetachedJws, DeletionBatchCapabilityIdentity.SigningRequestId(capability), Replacement.CommittedIssuedGuardRevision, Replacement.Targets, revocation);
        string jwsDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Replacement.DetachedJws)));
        var batch = new GovernanceBatchState(capability.BatchId, capability.BatchKind, capability.BatchOrdinal,
            Replacement.Targets.Select(t => new GovernanceProtectionTarget(t.TenantId, t.AgentInteractionId, t.TargetProtectionKeyAlias)).ToArray(),
            capability.ManifestDigest, capability.AttestationOrdinal, capability.CapabilityKeyVersion, jwsDigest,
            Activation.GuardReplacementReceiptId, Replacement.DispatchReceiptId, "ReplacementAwaitingActivation", Activation.CompromiseBlockReceiptId, [], 1, [])
        {
            Capability = capability, DetachedJws = Replacement.DetachedJws, SigningRequestId = Blocked.SigningRequestId,
            IssuedGuardRevision = Replacement.CommittedIssuedGuardRevision, DispatchGuardRevision = Replacement.DispatchGuardRevision,
        };
        var deletion = new GovernanceDeletionState(capability.DeletionRequestId, new(capability.TenantId, "ExactInteraction", "interaction-a", ""),
            "predicate", 1, ["owner-a"], ["obligation-a"], [], [], [], capability.DestructionSealId, [batch], false, false);
        State = new(capability.TenantId, "synthetic-independent-installation", 21, "epoch-a", "legacy", "writer", null, [], [deletion],
            [], [],
            [Receipt(batch.IssueReceiptId, "replace-operation", batch.IssuedGuardRevision), Receipt(batch.DispatchReceiptId, "dispatch-operation", batch.DispatchGuardRevision)], [capability.CapabilityKeyVersion])
        { Revocations = [revocation] };
        Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => State);
        Journal.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(true);
        Evidence.Matches(Arg.Any<DeletionCapabilityRevocationEnvelope>()).Returns(call => call.Arg<DeletionCapabilityRevocationEnvelope>() == Blocked.RevocationReceipt.Envelope);
        Evidence.MatchesActivation(Arg.Any<DeletionReattestationActivation>()).Returns(call => Exact(call.Arg<DeletionReattestationActivation>(), Activation));
        Evidence.MatchesBlockedReplacement(Arg.Any<DeletionBlockedReplacementReconciliation>()).Returns(call => Exact(call.Arg<DeletionBlockedReplacementReconciliation>(), Blocked));
        Evidence.PendingBlockedReplacement(Arg.Any<DeletionBlockedReplacementReconciliation>()).Returns(false);
        Evidence.VerifyCurrentBlockedReplacementAsync(Arg.Any<DeletionBlockedReplacementReconciliation>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Exact(call.Arg<DeletionBlockedReplacementReconciliation>(), Blocked)
                && call.Arg<long>() == CurrentGuardRevision && call.Arg<string>() == CurrentGuardDigest);
        RetainCurrentGuard();
        Authority = new(Trust, Guard, Journal, Evidence);
    }

    /// <summary>Original immutable batch credential.</summary>
    internal DeletionBatchConsumptionRequest Original { get; }
    /// <summary>Current genuine signed successor artifact.</summary>
    internal DeletionBatchConsumptionRequest Replacement { get; private set; }
    /// <summary>Published tenant/key public verifier records.</summary>
    internal PublishedDeletionCapabilityTrust[] Trust { get; }
    /// <summary>Exact independently installed activation original.</summary>
    internal DeletionReattestationActivation Activation { get; set; }
    /// <summary>Exact independently installed blocked successor original.</summary>
    internal DeletionBlockedReplacementReconciliation Blocked { get; set; }
    /// <summary>Mutable guard observation returned by the synthetic reader.</summary>
    internal TenantGovernanceGuardState State { get; set; }
    /// <summary>Sole batch in the current observed guard state.</summary>
    internal GovernanceBatchState Batch => State.Deletions.Single().Batches.Single();
    /// <summary>Synthetic fresh guard reader.</summary>
    internal IGovernanceScopeGuard Guard { get; } = Substitute.For<IGovernanceScopeGuard>();
    /// <summary>Synthetic independent transition evidence.</summary>
    internal IAnchoredStateTransitionAuthority Journal { get; } = Substitute.For<IAnchoredStateTransitionAuthority>();
    /// <summary>Synthetic independently installed revocation and successor evidence.</summary>
    internal IPublishedTrustRevocationEvidence Evidence { get; } = Substitute.For<IPublishedTrustRevocationEvidence>();
    /// <summary>Actual source verifier under test.</summary>
    internal PublishedTrustDeletionConsumptionAuthority Authority { get; private set; }

    /// <summary>Changes only the observed guard batch, without advancing independent currentness.</summary>
    internal void SetBatch(GovernanceBatchState batch)
        => State = State with { Deletions = [State.Deletions.Single() with { Batches = [batch] }] };

    /// <summary>Installs actual synthetic actor block/revocation originals for source-verifier delegation.</summary>
    internal void InstallOwnerEvidence(string blockReceipt, DeletionCapabilityRevocationReceipt revocation)
    {
        Activation = Activation with { CompromiseBlockReceiptId = blockReceipt };
        Blocked = Blocked with { CompromiseBlockReceiptId = blockReceipt, RevocationReceipt = revocation, ExpectedKeyBlockSetRevision = revocation.KeyBlockSetRevision };
        SetBatch(Batch with { ProtectionReceiptId = blockReceipt });
        State = State with { Revocations = [revocation] };
        RetainCurrentGuard();
    }

    /// <summary>Independently retained guard high water; reader mutations never advance it.</summary>
    internal long CurrentGuardRevision { get; private set; }

    /// <summary>Independently retained exact guard digest; reader mutations never replace it.</summary>
    internal string CurrentGuardDigest { get; private set; } = "";

    /// <summary>Explicitly installs the current observation into the synthetic independent evidence store.</summary>
    internal void RetainCurrentGuard()
    {
        CurrentGuardRevision = State.Revision;
        CurrentGuardDigest = ErasureRootCustody.Hash(State);
    }

    /// <summary>Installs one signature artifact consistently in the request, guard batch and both correlated receipts.</summary>
    internal void InstallReplacementArtifact(string detachedJws)
    {
        Replacement = Replacement with { DetachedJws = detachedJws };
        Activation = Activation with { Replacement = Replacement };
        Blocked = Blocked with { DetachedJws = detachedJws };
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(detachedJws)));
        SetBatch(Batch with { DetachedJws = detachedJws, AttestationDigest = digest });
        State = State with { Receipts = State.Receipts.Select(receipt => receipt with { DetachedJwsDigest = digest }).ToArray() };
        RetainCurrentGuard();
    }

    /// <summary>Signs substituted metadata with a genuine new retained verifier, without changing the already committed guard records.</summary>
    internal DeletionBlockedReplacementReconciliation SignedSubstitution(DeletionBatchCapabilityV1 capability, IReadOnlyList<ProtectionTarget> targets)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        PublishedDeletionCapabilityTrust trust = Trust[1] with { SubjectPublicKeyInfo = key.ExportSubjectPublicKeyInfo(),
            TenantId = capability.TenantId, CapabilityKeyVersion = capability.CapabilityKeyVersion, Issuer = capability.Issuer, Audience = capability.Audience };
        Trust[1] = trust;
        string jws = Sign(capability, key, trust);
        Authority = new(Trust, Guard, Journal, Evidence);
        return Blocked with { Capability = capability, Targets = targets, DetachedJws = jws,
            SigningRequestId = DeletionBatchCapabilityIdentity.SigningRequestId(capability) };
    }

    private GovernanceProtocolReceipt Receipt(string id, string operation, long revision)
        => new(operation, new string('D', 64), "Committed", revision, 1, Replacement.Capability.BatchId, id)
        {
            Capability = Replacement.Capability, SigningRequestId = Blocked.SigningRequestId,
            DetachedJwsDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Replacement.DetachedJws))),
        };

    private static bool Exact<T>(T left, T right) => ErasureRootCustody.Hash(left) == ErasureRootCustody.Hash(right);

    private static DeletionBatchConsumptionRequest Signed(string version, long attestation, long intendedRevision, out PublishedDeletionCapabilityTrust trust)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ProtectionTarget[] targets = [new("tenant-a", "interaction-a", "alias-a"), new("tenant-a", "interaction-b", "alias-b")];
        var capability = new DeletionBatchCapabilityV1("issuer", "protection-v1", "tenant-a", "request-1", "seal-1", "AcceptedSet", 0, "batch-1",
            DeletionBatchCapabilityIdentity.TargetManifestDigest(targets), "tenant-a:guard", intendedRevision, attestation, 1, version);
        trust = new("tenant-a", "DeletionBatchCapabilitySigningKey", version, "issuer", "protection-v1", "anchor-" + version, "anchor-v1", key.ExportSubjectPublicKeyInfo(), 3, false);
        string jws = Sign(capability, key, trust);
        return new(capability, jws, intendedRevision + 1, "dispatch-" + attestation, intendedRevision + 3, targets);
    }

    private static string Sign(DeletionBatchCapabilityV1 capability, ECDsa key, PublishedDeletionCapabilityTrust trust)
    {
        string header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"ES256\",\"anchorVersion\":\"" + trust.PublicAnchorVersion
            + "\",\"keyVersion\":\"" + trust.CapabilityKeyVersion + "\",\"kid\":\"" + trust.PublicAnchorId + "\",\"typ\":\"DeletionBatchCapabilityV1\"}"));
        byte[] input = Encoding.ASCII.GetBytes(header + "." + Base64Url(DeletionBatchCapabilityIdentity.CanonicalPayload(capability)));
        return header + ".." + Base64Url(key.SignData(input, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
