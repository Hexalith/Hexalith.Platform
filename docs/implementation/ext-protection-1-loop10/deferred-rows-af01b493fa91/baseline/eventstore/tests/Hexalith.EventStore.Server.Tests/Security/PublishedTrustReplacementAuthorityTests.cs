using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>Real ephemeral ES256 and persisted actor component checks. Installed currentness, bootstrap, physical custody and Live qualification remain outside this fixture.</summary>
public sealed class PublishedTrustReplacementAuthorityTests
{
    /// <summary>Exact dispatched activation authenticates even after successor compromise; ordinary dispatch remains denied.</summary>
    [Fact]
    public async Task RetainedCompromisedSuccessorAuthenticatesWithoutConsumptionPermission()
    {
        var f = new PublishedTrustReplacementFixture();
        var authority = new PublishedTrustDeletionConsumptionAuthority(f.Trust.Select(t => t with { IsRevoked = true }).ToArray(), f.Guard, f.Journal, f.Evidence);
        (await authority.VerifyActivationAsync(f.Activation, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await authority.VerifyDispatchAsync(f.Replacement, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await authority.VerifyBlockedReplacementAsync(f.Blocked, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await f.Guard.Received(2).ReadAsync("tenant-a", Arg.Any<CancellationToken>());
        await f.Guard.DidNotReceive().ExecuteAsync(Arg.Any<GovernanceGuardTransition>(), Arg.Any<IReadOnlyList<GuardedStateMutation>>(), Arg.Any<CancellationToken>());
        await f.Journal.DidNotReceive().RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
        await f.Journal.Received().VerifyTransitionAsync(Arg.Is<AnchoredStateTransition>(proof =>
            System.Text.Encoding.UTF8.GetString(proof.TargetBytes).Contains(DeletionConsumptionIdentity.Digest(f.Blocked), StringComparison.Ordinal)), Arg.Any<CancellationToken>());
    }

    /// <summary>Issued-but-blocked successors require only their original exact issue, never a fabricated dispatch.</summary>
    [Fact]
    public async Task ExactBlockedSuccessorPassesBeforeAnyDispatchAndRechecksIndependentCurrentness()
    {
        var f = new PublishedTrustReplacementFixture();
        f.SetBatch(f.Batch with { DispatchReceiptId = "", DispatchGuardRevision = 0 });
        f.State = f.State with { Receipts = [f.State.Receipts[0]] };
        f.RetainCurrentGuard();
        (await f.Authority.VerifyBlockedReplacementAsync(f.Blocked, TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await f.Authority.VerifyBlockedReplacementAsync(f.Blocked, TestContext.Current.CancellationToken)).ShouldBeTrue();
        await f.Evidence.Received(2).VerifyCurrentBlockedReplacementAsync(Arg.Is<DeletionBlockedReplacementReconciliation>(r =>
            DeletionConsumptionIdentity.Digest(r) == DeletionConsumptionIdentity.Digest(f.Blocked)), f.State.Revision,
            DeletionConsumptionIdentity.Digest(f.State), Arg.Any<CancellationToken>());
        (await f.Authority.VerifyActivationAsync(f.Activation, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>Local signature, manifest, exact issue and distinct dispatch checks reject substitutions even if an external evidence predicate is permissive.</summary>
    [Theory]
    [InlineData("tenant")]
    [InlineData("key")]
    [InlineData("manifest")]
    [InlineData("target")]
    [InlineData("signature")]
    [InlineData("guard-signature")]
    [InlineData("guard-capability")]
    [InlineData("attestation")]
    [InlineData("signing-id")]
    [InlineData("actual-issue")]
    [InlineData("issue-capability")]
    [InlineData("issue-signing-id")]
    [InlineData("issue-jws")]
    [InlineData("issue-operation")]
    [InlineData("pending-issue")]
    [InlineData("missing-issue")]
    [InlineData("duplicate-issue")]
    [InlineData("wrong-replacement-receipt")]
    [InlineData("wrong-block-receipt")]
    [InlineData("dispatch")]
    [InlineData("dispatch-revision")]
    [InlineData("dispatch-capability")]
    [InlineData("dispatch-signing-id")]
    [InlineData("dispatch-jws")]
    [InlineData("same-operation")]
    [InlineData("same-receipt")]
    [InlineData("pending-dispatch")]
    [InlineData("missing-dispatch")]
    [InlineData("future-dispatch")]
    [InlineData("admission-blocked")]
    [InlineData("wrong-phase")]
    [InlineData("integrity")]
    [InlineData("foreign-scope")]
    [InlineData("duplicate-batch")]
    public async Task ActivationRejectsSubstitution(string mutation)
    {
        var f = new PublishedTrustReplacementFixture();
        (await f.Authority.VerifyActivationAsync(f.Activation, TestContext.Current.CancellationToken)).ShouldBeTrue();
        f.Evidence.MatchesActivation(Arg.Any<DeletionReattestationActivation>()).Returns(true);
        DeletionReattestationActivation activation = f.Activation;
        GovernanceBatchState batch = f.Batch;
        GovernanceProtocolReceipt issue = f.State.Receipts[0];
        GovernanceProtocolReceipt dispatch = f.State.Receipts[1];
        switch (mutation)
        {
            case "tenant": activation = activation with { Replacement = f.Replacement with { Capability = f.Replacement.Capability with { TenantId = "tenant-b" } } }; break;
            case "key": activation = activation with { Replacement = f.Replacement with { Capability = f.Replacement.Capability with { CapabilityKeyVersion = "key-other" } } }; break;
            case "manifest": activation = activation with { Replacement = f.Replacement with { Capability = f.Replacement.Capability with { ManifestDigest = new string('A', 64) } } }; break;
            case "target": activation = activation with { Replacement = f.Replacement with { Targets = [f.Replacement.Targets[0] with { TargetProtectionKeyAlias = "other" }, f.Replacement.Targets[1]] } }; break;
            case "signature": activation = activation with { Replacement = f.Replacement with { DetachedJws = "tampered" } }; break;
            case "guard-signature": batch = batch with { DetachedJws = "tampered" }; break;
            case "guard-capability": batch = batch with { Capability = batch.Capability! with { SigningAttemptOrdinal = 2 } }; break;
            case "attestation": batch = batch with { AttestationOrdinal = 3 }; break;
            case "signing-id": batch = batch with { SigningRequestId = "substituted" }; break;
            case "actual-issue": activation = activation with { Replacement = f.Replacement with { CommittedIssuedGuardRevision = f.Replacement.CommittedIssuedGuardRevision + 1 } }; batch = batch with { IssuedGuardRevision = activation.Replacement.CommittedIssuedGuardRevision }; issue = issue with { GuardHighWater = batch.IssuedGuardRevision }; break;
            case "issue-capability": issue = issue with { Capability = issue.Capability! with { SigningAttemptOrdinal = 2 } }; break;
            case "issue-signing-id": issue = issue with { SigningRequestId = "other" }; break;
            case "issue-jws": issue = issue with { DetachedJwsDigest = new string('A', 64) }; break;
            case "issue-operation": issue = issue with { OperationId = "" }; break;
            case "pending-issue": issue = issue with { Status = "Pending" }; break;
            case "wrong-replacement-receipt": activation = activation with { GuardReplacementReceiptId = "other" }; break;
            case "wrong-block-receipt": activation = activation with { CompromiseBlockReceiptId = "other" }; break;
            case "dispatch": activation = activation with { Replacement = f.Replacement with { DispatchReceiptId = "other" } }; break;
            case "dispatch-revision": activation = activation with { Replacement = f.Replacement with { DispatchGuardRevision = f.Replacement.DispatchGuardRevision - 1 } }; break;
            case "dispatch-capability": dispatch = dispatch with { Capability = dispatch.Capability! with { SigningAttemptOrdinal = 2 } }; break;
            case "dispatch-signing-id": dispatch = dispatch with { SigningRequestId = "other" }; break;
            case "dispatch-jws": dispatch = dispatch with { DetachedJwsDigest = new string('A', 64) }; break;
            case "same-operation": dispatch = dispatch with { OperationId = issue.OperationId }; break;
            case "same-receipt": dispatch = dispatch with { ReceiptId = issue.ReceiptId }; batch = batch with { DispatchReceiptId = issue.ReceiptId }; activation = activation with { Replacement = f.Replacement with { DispatchReceiptId = issue.ReceiptId } }; break;
            case "pending-dispatch": dispatch = dispatch with { Status = "Pending" }; break;
            case "future-dispatch": f.State = f.State with { Revision = dispatch.GuardHighWater - 1 }; break;
            case "admission-blocked": batch = batch with { ProtectionOutcome = "AdmissionBlocked" }; break;
            case "wrong-phase": batch = batch with { ProtectionOutcome = "Unconsumed" }; break;
            case "integrity": f.State = f.State with { Deletions = [f.State.Deletions[0] with { IntegrityCompromised = true }] }; break;
            case "foreign-scope": f.State = f.State with { Deletions = [f.State.Deletions[0] with { Scope = f.State.Deletions[0].Scope with { TenantId = "tenant-b" } }] }; break;
        }
        f.SetBatch(batch);
        f.State = f.State with { Receipts = mutation switch
        {
            "missing-issue" => [dispatch], "missing-dispatch" => [issue], "duplicate-issue" => [issue, issue, dispatch], _ => [issue, dispatch],
        } };
        if (mutation == "duplicate-batch") { f.State = f.State with { Deletions = [f.State.Deletions[0] with { Batches = [batch, batch] }] }; }
        (await f.Authority.VerifyActivationAsync(activation, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>Permissive independent evidence cannot mask local canonical issue, receipt, manifest and revocation checks.</summary>
    [Theory]
    [InlineData("tenant")]
    [InlineData("key")]
    [InlineData("manifest")]
    [InlineData("target")]
    [InlineData("target-order")]
    [InlineData("attestation")]
    [InlineData("signing-attempt")]
    [InlineData("signing-id")]
    [InlineData("actual-issue")]
    [InlineData("replacement-receipt")]
    [InlineData("block-receipt")]
    [InlineData("issue-capability")]
    [InlineData("issue-signing-id")]
    [InlineData("issue-jws")]
    [InlineData("issue-operation")]
    [InlineData("missing-issue")]
    [InlineData("duplicate-issue")]
    [InlineData("pending-issue")]
    [InlineData("guard-jws")]
    [InlineData("guard-capability")]
    [InlineData("guard-target")]
    [InlineData("revocation-receipt")]
    [InlineData("revocation-key")]
    [InlineData("revocation-tenant")]
    [InlineData("revocation-revision")]
    [InlineData("revocation-owner-revision")]
    [InlineData("missing-revocation")]
    [InlineData("duplicate-revocation")]
    [InlineData("missing-compromise")]
    public async Task BlockedSuccessorRejectsLocalSubstitutionWithPermissiveIndependentEvidence(string mutation)
    {
        var f = new PublishedTrustReplacementFixture();
        MakeBlockedEvidencePermissive(f);
        (await f.Authority.VerifyBlockedReplacementAsync(f.Blocked, TestContext.Current.CancellationToken)).ShouldBeTrue();
        DeletionBlockedReplacementReconciliation request = f.Blocked;
        GovernanceProtocolReceipt issue = f.State.Receipts[0];
        switch (mutation)
        {
            case "tenant": request = f.SignedSubstitution(request.Capability with { TenantId = "tenant-b" }, request.Targets.Select(t => t with { TenantId = "tenant-b" }).ToArray()); break;
            case "key": request = f.SignedSubstitution(request.Capability with { CapabilityKeyVersion = "key-other" }, request.Targets); break;
            case "manifest": request = f.SignedSubstitution(request.Capability with { ManifestDigest = new string('A', 64) }, request.Targets); break;
            case "target":
                ProtectionTarget[] targets = [request.Targets[0] with { TargetProtectionKeyAlias = "other" }, request.Targets[1]];
                request = f.SignedSubstitution(request.Capability with { ManifestDigest = DeletionBatchCapabilityIdentity.TargetManifestDigest(targets) }, targets);
                break;
            case "target-order":
                ProtectionTarget[] reversed = request.Targets.Reverse().ToArray();
                request = f.SignedSubstitution(request.Capability with { ManifestDigest = DeletionBatchCapabilityIdentity.TargetManifestDigest(reversed) }, reversed);
                break;
            case "attestation": request = f.SignedSubstitution(request.Capability with { AttestationOrdinal = 3 }, request.Targets); break;
            case "signing-attempt": request = f.SignedSubstitution(request.Capability with { SigningAttemptOrdinal = 2 }, request.Targets); break;
            case "signing-id": request = request with { SigningRequestId = "other" }; break;
            case "actual-issue":
                request = request with { CommittedIssuedGuardRevision = request.CommittedIssuedGuardRevision + 1 };
                f.SetBatch(f.Batch with { IssuedGuardRevision = request.CommittedIssuedGuardRevision });
                issue = issue with { GuardHighWater = request.CommittedIssuedGuardRevision };
                break;
            case "replacement-receipt": request = request with { GuardReplacementReceiptId = "other" }; break;
            case "block-receipt": request = request with { CompromiseBlockReceiptId = "other" }; break;
            case "issue-capability": issue = issue with { Capability = issue.Capability! with { SigningAttemptOrdinal = 2 } }; break;
            case "issue-signing-id": issue = issue with { SigningRequestId = "other" }; break;
            case "issue-jws": issue = issue with { DetachedJwsDigest = new string('A', 64) }; break;
            case "issue-operation": issue = issue with { OperationId = "" }; break;
            case "pending-issue": issue = issue with { Status = "Pending" }; break;
            case "guard-jws": f.SetBatch(f.Batch with { DetachedJws = "other" }); break;
            case "guard-capability": f.SetBatch(f.Batch with { Capability = f.Batch.Capability! with { SigningAttemptOrdinal = 2 } }); break;
            case "guard-target": f.SetBatch(f.Batch with { Targets = [f.Batch.Targets[0] with { TargetProtectionKeyAlias = "other" }, f.Batch.Targets[1]] }); break;
            case "revocation-receipt": request = request with { RevocationReceipt = request.RevocationReceipt with { ReceiptId = "other" } }; break;
            case "revocation-key": request = request with { RevocationReceipt = request.RevocationReceipt with { Envelope = request.RevocationReceipt.Envelope with { KeyVersion = "other" } } }; break;
            case "revocation-tenant": request = request with { RevocationReceipt = request.RevocationReceipt with { Envelope = request.RevocationReceipt.Envelope with { TenantId = "tenant-b" } } }; break;
            case "revocation-revision": request = request with { RevocationReceipt = request.RevocationReceipt with { KeyBlockSetRevision = request.ExpectedKeyBlockSetRevision + 1 } }; break;
            case "revocation-owner-revision": request = request with { RevocationReceipt = request.RevocationReceipt with { OwnerRevision = 0 } }; break;
            case "missing-revocation": f.State = f.State with { Revocations = [] }; break;
            case "duplicate-revocation": f.State = f.State with { Revocations = [request.RevocationReceipt, request.RevocationReceipt] }; break;
            case "missing-compromise": f.State = f.State with { CompromisedKeyVersions = [] }; break;
        }
        if (mutation is "tenant" or "key")
        {
            DeletionCapabilityRevocationEnvelope envelope = request.RevocationReceipt.Envelope with { TenantId = request.Capability.TenantId, KeyVersion = request.Capability.CapabilityKeyVersion };
            request = request with { RevocationReceipt = request.RevocationReceipt with { Envelope = envelope, ReceiptId = DeletionConsumptionIdentity.Digest(envelope) } };
        }
        f.State = f.State with { Receipts = mutation switch
        {
            "missing-issue" => [f.State.Receipts[1]], "duplicate-issue" => [issue, issue, f.State.Receipts[1]], _ => [issue, f.State.Receipts[1]],
        } };
        (await f.Authority.VerifyBlockedReplacementAsync(request, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>Exact independently installed originals and nonpending currentness reject identity and block comparison substitutions separately from local correlation.</summary>
    [Theory]
    [InlineData("block-revision")]
    [InlineData("operation")]
    [InlineData("absent-original")]
    [InlineData("pending")]
    [InlineData("revocation-pending")]
    [InlineData("absent-currentness")]
    public async Task BlockedSuccessorRequiresExactIndependentEvidence(string mutation)
    {
        var f = new PublishedTrustReplacementFixture();
        (await f.Authority.VerifyBlockedReplacementAsync(f.Blocked, TestContext.Current.CancellationToken)).ShouldBeTrue();
        DeletionBlockedReplacementReconciliation request = f.Blocked;
        switch (mutation)
        {
            case "block-revision": request = request with { ExpectedKeyBlockSetRevision = request.ExpectedKeyBlockSetRevision + 1 }; break;
            case "operation": request = request with { OperationId = "other-original" }; break;
            case "absent-original": f.Evidence.MatchesBlockedReplacement(Arg.Any<DeletionBlockedReplacementReconciliation>()).Returns(false); break;
            case "pending": f.Evidence.PendingBlockedReplacement(Arg.Any<DeletionBlockedReplacementReconciliation>()).Returns(true); break;
            case "revocation-pending": f.Evidence.Pending(Arg.Any<DeletionCapabilityRevocationEnvelope>()).Returns(true); break;
            case "absent-currentness": f.Evidence.VerifyCurrentBlockedReplacementAsync(Arg.Any<DeletionBlockedReplacementReconciliation>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false); break;
        }
        (await f.Authority.VerifyBlockedReplacementAsync(request, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await f.Authority.VerifyActivationAsync(f.Activation with { ExpectedKeyBlockSetRevision = f.Activation.ExpectedKeyBlockSetRevision + 1 }, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>Malformed signatures retain canonical detached shape and every installed artifact correlation; only actual cryptographic verification can reject.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task SuccessorRequiresActualEs256Verification(bool activation, bool wrongPublicKey)
    {
        var f = new PublishedTrustReplacementFixture();
        (await VerifySuccessorAsync(f, activation)).ShouldBeTrue();
        PublishedTrustDeletionConsumptionAuthority authority = f.Authority;
        if (wrongPublicKey)
        {
            PublishedDeletionCapabilityTrust[] wrongTrust = [f.Trust[0], f.Trust[1] with { SubjectPublicKeyInfo = f.Trust[0].SubjectPublicKeyInfo }];
            authority = new(wrongTrust, f.Guard, f.Journal, f.Evidence);
        }
        else
        {
            string[] parts = f.Replacement.DetachedJws.Split('.');
            byte[] signature = Convert.FromBase64String(parts[2].Replace('-', '+').Replace('_', '/') + "==");
            signature.Length.ShouldBe(64);
            signature[0] ^= 1;
            string corrupted = parts[0] + ".." + Convert.ToBase64String(signature).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            corrupted.Length.ShouldBe(f.Replacement.DetachedJws.Length);
            f.InstallReplacementArtifact(corrupted);
        }
        f.Evidence.MatchesActivation(f.Activation).ShouldBeTrue();
        f.Evidence.MatchesBlockedReplacement(f.Blocked).ShouldBeTrue();
        (await f.Evidence.VerifyCurrentBlockedReplacementAsync(f.Blocked, f.State.Revision, DeletionConsumptionIdentity.Digest(f.State), TestContext.Current.CancellationToken)).ShouldBeTrue();
        f.Batch.DetachedJws.ShouldBe(f.Blocked.DetachedJws);
        f.State.Receipts.ShouldAllBe(r => r.DetachedJwsDigest == f.Batch.AttestationDigest && r.SigningRequestId == f.Blocked.SigningRequestId);
        f.Journal.ClearReceivedCalls();
        (await VerifySuccessorAsync(f, activation, authority)).ShouldBeFalse();
        await f.Journal.DidNotReceive().VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Independent currentness retains its own revision and exact bytes instead of following changes made by the guard reader.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BlockedCurrentnessRejectsStaleOrDivergentGuardSnapshot(bool olderRevision)
    {
        var f = new PublishedTrustReplacementFixture();
        TenantGovernanceGuardState installed = f.State;
        long retainedRevision = f.CurrentGuardRevision;
        string retainedDigest = f.CurrentGuardDigest;
        (await f.Authority.VerifyBlockedReplacementAsync(f.Blocked, TestContext.Current.CancellationToken)).ShouldBeTrue();
        f.State = olderRevision ? installed with { Revision = installed.Revision - 1 } : installed with { InstallationId = "different-bytes-same-revision" };
        f.CurrentGuardRevision.ShouldBe(retainedRevision);
        f.CurrentGuardDigest.ShouldBe(retainedDigest);
        f.Journal.ClearReceivedCalls();
        (await f.Authority.VerifyBlockedReplacementAsync(f.Blocked, TestContext.Current.CancellationToken)).ShouldBeFalse();
        await f.Evidence.Received().VerifyCurrentBlockedReplacementAsync(Arg.Any<DeletionBlockedReplacementReconciliation>(), f.State.Revision,
            DeletionConsumptionIdentity.Digest(f.State), Arg.Any<CancellationToken>());
        await f.Journal.DidNotReceive().VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
        f.State = installed;
        (await f.Authority.VerifyBlockedReplacementAsync(f.Blocked, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    /// <summary>Both source paths reach the independent final journal only after all other checks, deny rejection or failure, and propagate caller cancellation.</summary>
    [Theory]
    [InlineData(true, "reject")]
    [InlineData(false, "reject")]
    [InlineData(true, "throw")]
    [InlineData(false, "throw")]
    [InlineData(true, "cancel")]
    [InlineData(false, "cancel")]
    public async Task FinalJournalBoundaryFailsClosed(bool activation, string outcome)
    {
        var f = new PublishedTrustReplacementFixture();
        (await VerifySuccessorAsync(f, activation)).ShouldBeTrue();
        f.Journal.ClearReceivedCalls();
        using var cancellation = new CancellationTokenSource();
        f.Journal.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (outcome == "throw") { throw new HttpRequestException("Independent journal unavailable."); }
            if (outcome == "cancel") { await cancellation.CancelAsync(); call.Arg<CancellationToken>().ThrowIfCancellationRequested(); }
            return false;
        });
        if (outcome == "cancel")
        {
            await Should.ThrowAsync<OperationCanceledException>(() => VerifySuccessorAsync(f, activation, cancellationToken: cancellation.Token));
        }
        else { (await VerifySuccessorAsync(f, activation, cancellationToken: cancellation.Token)).ShouldBeFalse(); }
        await f.Journal.Received(1).VerifyTransitionAsync(Arg.Is<AnchoredStateTransition>(proof => Encoding.UTF8.GetString(proof.TargetBytes)
            .Contains(activation ? DeletionConsumptionIdentity.Digest(f.Activation) : DeletionConsumptionIdentity.Digest(f.Blocked), StringComparison.Ordinal)), cancellation.Token);
    }

    /// <summary>Neither source path may choose among absent, duplicated active/revoked, or conflicting retained tenant/key anchors.</summary>
    [Theory]
    [InlineData(true, "absent")]
    [InlineData(false, "absent")]
    [InlineData(true, "active-and-revoked")]
    [InlineData(false, "active-and-revoked")]
    [InlineData(true, "conflicting-anchors")]
    [InlineData(false, "conflicting-anchors")]
    public async Task RetainedVerifierRequiresOneUnambiguousPublishedRecord(bool activation, string mutation)
    {
        var f = new PublishedTrustReplacementFixture();
        PublishedDeletionCapabilityTrust retained = f.Trust[1] with { IsRevoked = true };
        var unique = new PublishedTrustDeletionConsumptionAuthority([f.Trust[0], retained], f.Guard, f.Journal, f.Evidence);
        (await VerifySuccessorAsync(f, activation, unique)).ShouldBeTrue();
        (await unique.VerifyDispatchAsync(f.Replacement, TestContext.Current.CancellationToken)).ShouldBeFalse();
        PublishedDeletionCapabilityTrust[] trust = mutation switch
        {
            "absent" => [f.Trust[0]],
            "active-and-revoked" => [f.Trust[0], retained, retained with { IsRevoked = false }],
            _ => [f.Trust[0], retained, retained with { PublicAnchorId = "conflicting-anchor", PublicAnchorVersion = "conflicting-version", SubjectPublicKeyInfo = f.Trust[0].SubjectPublicKeyInfo }],
        };
        (await VerifySuccessorAsync(f, activation, new(trust, f.Guard, f.Journal, f.Evidence))).ShouldBeFalse();
    }

    /// <summary>Absent installation and throwing readers/evidence deny; caller cancellation propagates.</summary>
    [Fact]
    public async Task MissingThrowingAndCanceledEvidenceFailsClosed()
    {
        var f = new PublishedTrustReplacementFixture();
        (await new PublishedTrustDeletionConsumptionAuthority(f.Trust, f.Guard, f.Journal)
            .VerifyBlockedReplacementAsync(f.Blocked, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await new PublishedTrustDeletionConsumptionAuthority(f.Trust, f.Guard, f.Journal, Substitute.For<IPublishedTrustRevocationEvidence>())
            .VerifyBlockedReplacementAsync(f.Blocked, TestContext.Current.CancellationToken)).ShouldBeFalse();
        f.Evidence.MatchesActivation(Arg.Any<DeletionReattestationActivation>()).Returns(_ => throw new InvalidOperationException("Unavailable installation."));
        (await f.Authority.VerifyActivationAsync(f.Activation, TestContext.Current.CancellationToken)).ShouldBeFalse();
        f.Evidence.MatchesBlockedReplacement(Arg.Any<DeletionBlockedReplacementReconciliation>()).Returns(_ => throw new InvalidOperationException("Unavailable installation."));
        (await f.Authority.VerifyBlockedReplacementAsync(f.Blocked, TestContext.Current.CancellationToken)).ShouldBeFalse();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => f.Authority.VerifyActivationAsync(f.Activation, cancellation.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => f.Authority.VerifyBlockedReplacementAsync(f.Blocked, cancellation.Token));
        var guardFailure = new PublishedTrustReplacementFixture();
        guardFailure.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<TenantGovernanceGuardState?>(new HttpRequestException("Unavailable guard.")));
        (await guardFailure.Authority.VerifyActivationAsync(guardFailure.Activation, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await guardFailure.Authority.VerifyBlockedReplacementAsync(guardFailure.Blocked, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    /// <summary>Currentness binds detached guard bytes even if the reader's mutable collections change during independent verification.</summary>
    [Fact]
    public async Task BlockedCurrentnessUsesOwnedSnapshotAndPropagatesCancellation()
    {
        var f = new PublishedTrustReplacementFixture();
        var receipts = f.State.Receipts.ToList();
        f.State = f.State with { Receipts = receipts };
        string capturedDigest = DeletionConsumptionIdentity.Digest(f.State);
        f.Evidence.VerifyCurrentBlockedReplacementAsync(Arg.Any<DeletionBlockedReplacementReconciliation>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => { receipts.Clear(); return call.Arg<string>() == capturedDigest; });
        (await f.Authority.VerifyBlockedReplacementAsync(f.Blocked, TestContext.Current.CancellationToken)).ShouldBeTrue();
        receipts.ShouldBeEmpty();
        var canceled = new PublishedTrustReplacementFixture();
        using var cancellation = new CancellationTokenSource();
        canceled.Evidence.VerifyCurrentBlockedReplacementAsync(Arg.Any<DeletionBlockedReplacementReconciliation>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async call => { await cancellation.CancelAsync(); call.Arg<CancellationToken>().ThrowIfCancellationRequested(); return false; });
        await Should.ThrowAsync<OperationCanceledException>(() => canceled.Authority.VerifyBlockedReplacementAsync(canceled.Blocked, cancellation.Token));
    }

    /// <summary>Activation retains complete captured targets when caller-owned arrays change during an evidence callback or awaited guard read.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActivationOwnsCallerTargetsAcrossEvidenceAndAwait(bool duringAwait)
    {
        var f = new PublishedTrustReplacementFixture();
        ProtectionTarget[] callerTargets = f.Activation.Replacement.Targets.ToArray();
        f.Activation = f.Activation with { Replacement = f.Activation.Replacement with { Targets = callerTargets } };
        DeletionReattestationActivation expected = f.Activation with { Replacement = DeletionConsumptionIdentity.Capture(f.Activation.Replacement) };
        string expectedDigest = DeletionConsumptionIdentity.Digest(expected);
        DeletionReattestationActivation? verifiedOriginal = null;
        AnchoredStateTransition? verifiedProof = null;
        void MutateCaller() => callerTargets[0] = callerTargets[0] with { TenantId = "tenant-other", TargetProtectionKeyAlias = "caller-mutated" };
        f.Evidence.MatchesActivation(Arg.Any<DeletionReattestationActivation>()).Returns(call =>
        {
            verifiedOriginal = call.Arg<DeletionReattestationActivation>();
            if (!duringAwait) { MutateCaller(); }
            return DeletionConsumptionIdentity.Digest(verifiedOriginal) == expectedDigest;
        });
        f.Guard.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            await Task.Yield();
            if (duringAwait) { MutateCaller(); }
            return (TenantGovernanceGuardState?)f.State;
        });
        f.Journal.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call =>
        { verifiedProof = call.Arg<AnchoredStateTransition>(); return true; });
        (await f.Authority.VerifyActivationAsync(f.Activation, TestContext.Current.CancellationToken)).ShouldBeTrue();
        callerTargets[0].TargetProtectionKeyAlias.ShouldBe("caller-mutated");
        verifiedOriginal.ShouldNotBeNull();
        DeletionConsumptionIdentity.Digest(verifiedOriginal).ShouldBe(expectedDigest);
        verifiedOriginal.Replacement.Targets.ShouldBe(expected.Replacement.Targets);
        verifiedProof.ShouldNotBeNull();
        Encoding.UTF8.GetString(verifiedProof.TargetBytes).ShouldContain(expectedDigest);
        DeletionConsumptionIdentity.Digest(f.Activation).ShouldNotBe(expectedDigest);
    }

    /// <summary>Blocked reconciliation owns target and affected-batch collections across synchronous evidence and awaited currentness without changing its verified original or proof.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlockedSuccessorOwnsCallerTargetsAndRevocationBatches(bool duringAwait)
    {
        var f = new PublishedTrustReplacementFixture();
        ProtectionTarget[] callerTargets = f.Blocked.Targets.ToArray();
        string[] callerAffected = ["original-affected-batch"];
        f.Blocked = f.Blocked with { Targets = callerTargets, RevocationReceipt = f.Blocked.RevocationReceipt with { AffectedBatchIds = callerAffected } };
        DeletionBlockedReplacementReconciliation expected = DeletionConsumptionIdentity.Capture(f.Blocked);
        string expectedDigest = DeletionConsumptionIdentity.Digest(expected);
        // The synthetic independent guard and currentness retain their own original bytes.
        f.State = f.State with { Revocations = [expected.RevocationReceipt] };
        f.RetainCurrentGuard();
        long retainedRevision = f.CurrentGuardRevision;
        string retainedStateDigest = f.CurrentGuardDigest;
        DeletionBlockedReplacementReconciliation? verifiedOriginal = null;
        AnchoredStateTransition? verifiedProof = null;
        void MutateCaller()
        {
            callerTargets[0] = callerTargets[0] with { TenantId = "tenant-other", TargetProtectionKeyAlias = "caller-mutated" };
            callerAffected[0] = "caller-mutated-batch";
        }
        f.Evidence.MatchesBlockedReplacement(Arg.Any<DeletionBlockedReplacementReconciliation>()).Returns(call =>
        {
            verifiedOriginal = call.Arg<DeletionBlockedReplacementReconciliation>();
            if (!duringAwait) { MutateCaller(); }
            return DeletionConsumptionIdentity.Digest(verifiedOriginal) == expectedDigest;
        });
        f.Evidence.VerifyCurrentBlockedReplacementAsync(Arg.Any<DeletionBlockedReplacementReconciliation>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await Task.Yield();
                if (duringAwait) { MutateCaller(); }
                return DeletionConsumptionIdentity.Digest(call.Arg<DeletionBlockedReplacementReconciliation>()) == expectedDigest
                    && call.Arg<long>() == retainedRevision && call.Arg<string>() == retainedStateDigest;
            });
        f.Journal.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call =>
        { verifiedProof = call.Arg<AnchoredStateTransition>(); return true; });
        (await f.Authority.VerifyBlockedReplacementAsync(f.Blocked, TestContext.Current.CancellationToken)).ShouldBeTrue();
        callerTargets[0].TargetProtectionKeyAlias.ShouldBe("caller-mutated");
        callerAffected[0].ShouldBe("caller-mutated-batch");
        verifiedOriginal.ShouldNotBeNull();
        DeletionConsumptionIdentity.Digest(verifiedOriginal).ShouldBe(expectedDigest);
        verifiedOriginal.Targets.ShouldBe(expected.Targets);
        verifiedOriginal.RevocationReceipt.AffectedBatchIds.ShouldBe(["original-affected-batch"]);
        verifiedProof.ShouldNotBeNull();
        Encoding.UTF8.GetString(verifiedProof.TargetBytes).ShouldContain(expectedDigest);
        DeletionConsumptionIdentity.Digest(f.Blocked).ShouldNotBe(expectedDigest);
    }

    /// <summary>The real verifier reconciles an issued successor through response loss, restart, exact lookup and retry while preserving the original batch and zero provider effects.</summary>
    [Fact]
    public async Task PersistedBlockedSuccessorRecoversExactOriginalAfterResponseLoss()
    {
        var f = new PublishedTrustReplacementFixture();
        var owner = new DeletionConsumptionFixture();
        await owner.Actor.RegisterAsync(f.Original);
        await owner.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation());
        DeletionConsumptionOutcome originalBlock = await owner.Actor.LookupAsync("tenant-a", "batch-1");
        DeletionCapabilityRevocationReceipt revocation = (await owner.Actor.RegisterRevocationAsync(f.Blocked.RevocationReceipt.Envelope))!;
        f.InstallOwnerEvidence(originalBlock.ReceiptId!, revocation);
        f.SetBatch(f.Batch with { DispatchReceiptId = "", DispatchGuardRevision = 0 });
        f.State = f.State with { Receipts = [f.State.Receipts[0]] };
        f.RetainCurrentGuard();
        owner.Authority.VerifyBlockedReplacementAsync(Arg.Any<DeletionBlockedReplacementReconciliation>(), Arg.Any<CancellationToken>())
            .Returns(call => f.Authority.VerifyBlockedReplacementAsync(call.Arg<DeletionBlockedReplacementReconciliation>(), call.Arg<CancellationToken>()));
        bool loseResponse = true;
        owner.Authority.AuthorizeOperationAsync("tenant-a", "batch-1", "ReconcileBlockedDeletionReplacement", Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (loseResponse && owner.Backend.CommittedState.Values.OfType<DeletionConsumptionLedger>().Any(s => s.Operations.Any(o => o.OperationId == f.Blocked.OperationId)))
            { loseResponse = false; return false; }
            return true;
        });
        (await owner.Actor.ReconcileBlockedReplacementAsync(f.Blocked)).Status.ShouldBe(DeletionConsumptionStatus.Unavailable);
        DeletionConsumptionLedger saved = owner.Backend.CommittedState.Values.OfType<DeletionConsumptionLedger>().Single();
        DeletionConsumptionOperation original = saved.Operations.Single(o => o.OperationId == f.Blocked.OperationId);
        DeletionConsumptionIdentity.Digest(original.BlockedReplacementOriginal).ShouldBe(DeletionConsumptionIdentity.Digest(f.Blocked));
        DeletionConsumptionIdentity.Digest(saved.Batches.Single().Original).ShouldBe(DeletionConsumptionIdentity.Digest(f.Original));
        DeletionConsumptionIdentity.Digest(saved.Batches.Single().Current).ShouldBe(DeletionConsumptionIdentity.Digest(f.Original));
        saved.Batches.Single().Outcome.Status.ShouldBe(DeletionConsumptionStatus.ConsumptionBlocked);
        saved.Batches.Single().Outcome.TargetReceipts.ShouldBeEmpty();
        var restarted = DeletionConsumptionFixture.Create(owner.Backend, owner.Authority, owner.Provider);
        DeletionConsumptionIdentity.Digest(await restarted.ReconcileBlockedReplacementAsync(f.Blocked)).ShouldBe(DeletionConsumptionIdentity.Digest(original.Outcome));
        DeletionBlockedReplacementResult retained = (await restarted.ReadBlockedReplacementAsync(f.Blocked.Capability))!;
        DeletionConsumptionIdentity.Digest(retained.Original).ShouldBe(DeletionConsumptionIdentity.Digest(f.Blocked));
        DeletionConsumptionIdentity.Digest(retained.Outcome).ShouldBe(DeletionConsumptionIdentity.Digest(original.Outcome));
        (await restarted.ReconcileBlockedReplacementAsync(f.Blocked with { GuardReplacementReceiptId = "other" })).Status.ShouldBe(DeletionConsumptionStatus.Conflict);
        (await restarted.ReserveAndConsumeAsync(f.Original)).Status.ShouldBe(DeletionConsumptionStatus.ConsumptionBlocked);
        (await restarted.ReserveAndConsumeAsync(f.Replacement)).Status.ShouldBe(DeletionConsumptionStatus.Conflict);
        await owner.Provider.DidNotReceive().ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await owner.Provider.DidNotReceive().LookupAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Actual successor authentication lets the owner's atomic compare retain a second compromise before or after activation and prevents reservation effects.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SecondCompromiseIsOrderedAtOwnerWithActualSuccessorVerifier(bool beforeActivation)
    {
        var f = new PublishedTrustReplacementFixture();
        var owner = new DeletionConsumptionFixture();
        await owner.Actor.RegisterAsync(f.Original);
        await owner.Actor.RegisterRevocationAsync(DeletionConsumptionFixture.Revocation());
        DeletionConsumptionOutcome blocked = await owner.Actor.LookupAsync("tenant-a", "batch-1");
        f.Activation = f.Activation with { CompromiseBlockReceiptId = blocked.ReceiptId!, ExpectedKeyBlockSetRevision = blocked.KeyBlockSetRevision };
        f.SetBatch(f.Batch with { ProtectionReceiptId = blocked.ReceiptId! });
        owner.Authority.VerifyActivationAsync(Arg.Any<DeletionReattestationActivation>(), Arg.Any<CancellationToken>())
            .Returns(call => f.Authority.VerifyActivationAsync(call.Arg<DeletionReattestationActivation>(), call.Arg<CancellationToken>()));
        if (beforeActivation) { await owner.Actor.RegisterRevocationAsync(f.Blocked.RevocationReceipt.Envelope); }
        DeletionConsumptionOutcome activation = await owner.Actor.ActivateAsync(f.Activation);
        activation.Status.ShouldBe(beforeActivation ? DeletionConsumptionStatus.ActivationBlockedByReplacementKeyCompromise : DeletionConsumptionStatus.Unconsumed);
        DeletionConsumptionIdentity.Digest(await owner.Actor.ActivateAsync(f.Activation)).ShouldBe(DeletionConsumptionIdentity.Digest(activation));
        if (!beforeActivation) { await owner.Actor.RegisterRevocationAsync(f.Blocked.RevocationReceipt.Envelope); }
        DeletionConsumptionOutcome final = await owner.Actor.LookupAsync("tenant-a", "batch-1");
        final.Status.ShouldBe(DeletionConsumptionStatus.ConsumptionBlocked);
        final.BlockedKeyVersion.ShouldBe("key-v2");
        (await owner.Actor.ReserveAndConsumeAsync(f.Replacement)).Status.ShouldBe(DeletionConsumptionStatus.ConsumptionBlocked);
        DeletionConsumptionLedger saved = owner.Backend.CommittedState.Values.OfType<DeletionConsumptionLedger>().Single();
        DeletionConsumptionIdentity.Digest(saved.Batches.Single().Original).ShouldBe(DeletionConsumptionIdentity.Digest(f.Original));
        saved.Revocations.Count.ShouldBe(2);
        saved.Batches.Single().Outcome.TargetReceipts.ShouldBeEmpty();
        DeletionConsumptionIdentity.Digest(saved.Operations.Single(o => o.OperationId == f.Activation.OperationId).ActivationOriginal).ShouldBe(DeletionConsumptionIdentity.Digest(f.Activation));
        await owner.Provider.DidNotReceive().ConsumeAsync(Arg.Any<DeletionBatchConsumptionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task UnissuedProofAcceptsMatchingGuardAndRejectsMismatchedGuard()
    {
        var f = new PublishedTrustReplacementFixture();
        var token = TestContext.Current.CancellationToken;
        f.SetBatch(f.Batch with { ProtectionOutcome = "ConsumptionBlocked:CapabilityKeyCompromise" });
        var successor = f.Replacement.Capability with { AttestationOrdinal = f.Batch.AttestationOrdinal + 1 };
        var signed = f.SignedSubstitution(successor, f.Replacement.Targets);
        byte[] stateBytes = JsonSerializer.SerializeToUtf8Bytes(f.State);
        string stateDigest = Convert.ToHexString(SHA256.HashData(stateBytes));
        stateDigest.ShouldBe(DeletionConsumptionIdentity.Digest(f.State));
        string mutation = DeletionConsumptionIdentity.Digest(Array.Empty<GuardedStateMutation>());
        var transition = new GovernanceGuardTransition(successor.TenantId, "replace-unissued", GovernanceGuardOperation.ReplaceAttestation,
            f.State.Revision, f.State.EpochId, successor.DeletionRequestId, null, null, null, null, null,
            new GovernanceBatchCommand(successor.BatchId, successor.BatchKind, successor.BatchOrdinal,
                signed.Targets.Select(target => new GovernanceProtectionTarget(target.TenantId, target.AgentInteractionId, target.TargetProtectionKeyAlias)).ToArray(),
                successor.ManifestDigest, successor.AttestationOrdinal, successor.CapabilityKeyVersion,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signed.DetachedJws))),
                "blocked", "unissued-refusal", [], signed.ExpectedKeyBlockSetRevision)
            { Capability = successor, DetachedJws = signed.DetachedJws, SigningRequestId = signed.SigningRequestId },
            "authorization", "reference");
        string intent = DeletionConsumptionIdentity.Digest(new { Transition = transition, TargetMutationDigest = mutation });
        var receipt = new GovernanceProtocolReceipt("replace-unissued", intent, "Stale", f.State.Revision, 1, "reference", "unissued-refusal")
        {
            Capability = successor,
            SigningRequestId = signed.SigningRequestId,
            DetachedJwsDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signed.DetachedJws))),
        };
        var proof = new GovernanceRejectedIssueOriginal(transition, mutation, receipt, f.State.Revision, stateDigest) { CurrentGuardStateBytes = stateBytes };
        var request = signed with { CommittedIssuedGuardRevision = 0, GuardReplacementReceiptId = receipt.ReceiptId, UnissuedProof = proof };
        DeletionConsumptionIdentity.Digest(DeletionConsumptionIdentity.Capture(request)).ShouldBe(DeletionConsumptionIdentity.Digest(request));
        f.Blocked = request;
        bool acceptBlockedProof = true;
        string material = DeletionConsumptionIdentity.Digest(request);
        f.Journal.VerifyTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            AnchoredStateTransition proof = call.Arg<AnchoredStateTransition>();
            string expected = string.Join('\n', "blocked-replacement", successor.TenantId, successor.BatchId,
                f.State.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture), material);
            bool blocked = proof.ScopeId == "published-trust|blocked-replacement|" + successor.TenantId + "|" + successor.BatchId
                && Encoding.UTF8.GetString(proof.TargetBytes) == expected;
            return blocked && acceptBlockedProof;
        });
        (await f.Authority.VerifyBlockedReplacementAsync(request, token)).ShouldBeTrue();
        acceptBlockedProof = false;
        (await f.Authority.VerifyBlockedReplacementAsync(request, token)).ShouldBeFalse();
        acceptBlockedProof = true;
        f.State = f.State with { Revision = f.State.Revision + 1 };
        (await f.Authority.VerifyBlockedReplacementAsync(request, token)).ShouldBeFalse();
        f.State = f.State with { Revision = proof.CurrentGuardRevision, EpochId = "epoch-mutated" };
        (await f.Authority.VerifyBlockedReplacementAsync(request, token)).ShouldBeFalse();
    }

    private static Task<bool> VerifySuccessorAsync(PublishedTrustReplacementFixture fixture, bool activation,
        PublishedTrustDeletionConsumptionAuthority? authority = null, CancellationToken cancellationToken = default)
    {
        authority ??= fixture.Authority;
        return activation ? authority.VerifyActivationAsync(fixture.Activation, cancellationToken)
            : authority.VerifyBlockedReplacementAsync(fixture.Blocked, cancellationToken);
    }

    private static void MakeBlockedEvidencePermissive(PublishedTrustReplacementFixture fixture)
    {
        fixture.Evidence.MatchesBlockedReplacement(Arg.Any<DeletionBlockedReplacementReconciliation>()).Returns(true);
        fixture.Evidence.Matches(Arg.Any<DeletionCapabilityRevocationEnvelope>()).Returns(true);
        fixture.Evidence.VerifyCurrentBlockedReplacementAsync(Arg.Any<DeletionBlockedReplacementReconciliation>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
    }

}
