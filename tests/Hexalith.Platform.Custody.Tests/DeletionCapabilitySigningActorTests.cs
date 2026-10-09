using Hexalith.EventStore.Contracts.Security;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Testing.Fakes;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Actual signer intent/outcome durability with synthetic qualified guard/key ports and real BCL signatures.</summary>
public sealed class DeletionCapabilitySigningActorTests
{
    /// <summary>Trust loss after durable intent is an exact independently retained noninvoked disposition, actionable by the coordinator.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostIntentTrustLossRetainsKnownDeniedOriginal(bool providerThrows)
    {
        var payload = Payload(); string id = DeletionBatchCapabilityCodec.SigningRequestId(payload);
        var backend = new InMemoryStateManager(); var authority = Authority();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var trust = Trust(payload, key);
        var current = await trust.ResolveAsync(payload.TenantId, "DeletionBatchCapabilitySigningKey", payload.CapabilityKeyVersion, TestContext.Current.CancellationToken);
        int reads = 0;
        trust.ResolveAsync(payload.TenantId, "DeletionBatchCapabilitySigningKey", payload.CapabilityKeyVersion, Arg.Any<CancellationToken>())
            .Returns(_ => ++reads == 1 ? Task.FromResult(current)
                : providerThrows ? Task.FromException<DeletionCapabilityPublishedTrust?>(new IOException("Post-intent trust unavailable"))
                    : Task.FromResult<DeletionCapabilityPublishedTrust?>(null));
        var provider = Substitute.For<IDeletionCapabilitySigningProvider>();
        var actor = Actor(payload, backend, authority, provider, trust);
        var guard = Substitute.For<IDeletionBatchGuardPort>();
        var protection = Substitute.For<IDeletionProtectionOwner>();
        var coordinator = new DeletionBatchExecutionCoordinator(new CustodyFixtureClock(), guard, _ => actor, _ => protection);

        var result = await coordinator.ExecuteAsync(payload, cancellationToken: TestContext.Current.CancellationToken);
        result.Status.ShouldBe("Denied");
        var original = result.Signing!;
        original.ShouldBe(new DeletionCapabilitySigningOutcome(id, payload, DeletionCapabilitySigningState.Denied));
        original.DetachedJws.ShouldBeNull(); original.NoIssueProof.ShouldBeNull(); result.NextAttempt.ShouldBeNull();
        var persisted = backend.CommittedState.Single();
        persisted.Value.ShouldBe(original);
        var restored = new InMemoryStateManager();
        await restored.SetStateAsync(persisted.Key, JsonSerializer.Deserialize<DeletionCapabilitySigningOutcome>(JsonSerializer.SerializeToUtf8Bytes(persisted.Value))!, TestContext.Current.CancellationToken);
        await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        var restarted = Actor(payload, restored, authority, provider, trust);
        (await restarted.LookupAsync(payload)).ShouldBe(original);
        (await new DeletionBatchExecutionCoordinator(new CustodyFixtureClock(), guard, _ => restarted, _ => protection)
            .ExecuteAsync(payload, cancellationToken: TestContext.Current.CancellationToken)).Signing.ShouldBe(original);
        JsonSerializer.SerializeToUtf8Bytes(restored.CommittedState.Single().Value).ShouldBe(JsonSerializer.SerializeToUtf8Bytes(original));
        provider.ReceivedCalls().ShouldBeEmpty(); guard.ReceivedCalls().ShouldBeEmpty(); protection.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>A possibly executed signature never borrows an unproven negative backend lookup to issue a new attempt.</summary>
    [Fact]
    public async Task UncertainSignatureDoesNotBorrowProviderDeniedAbsence()
    {
        var payload = Payload(); string id = DeletionBatchCapabilityCodec.SigningRequestId(payload);
        var backend = new InMemoryStateManager(); var authority = Authority();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var provider = Substitute.For<IDeletionCapabilitySigningProvider>();
        provider.SignAsync(payload, id, Arg.Any<CancellationToken>()).Returns(Task.FromException<DeletionCapabilitySigningResult>(new IOException("Possibly signed")));
        provider.LookupAsync(payload, id, Arg.Any<CancellationToken>()).Returns(new DeletionCapabilitySigningResult(new(id, payload, DeletionCapabilitySigningState.Denied), null));
        var actor = Actor(payload, backend, authority, provider, Trust(payload, key));
        (await actor.SignAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Unknown);
        byte[] original = JsonSerializer.SerializeToUtf8Bytes(backend.CommittedState.Single().Value);
        var restarted = Actor(payload, backend, authority, provider, Trust(payload, key));
        (await restarted.LookupAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Unknown);
        (await restarted.SignAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Unknown);
        JsonSerializer.SerializeToUtf8Bytes(backend.CommittedState.Single().Value).ShouldBe(original);
        await provider.Received(1).SignAsync(payload, id, Arg.Any<CancellationToken>());
        await provider.Received(2).LookupAsync(payload, id, Arg.Any<CancellationToken>());
    }

    /// <summary>A separately authorized mutation recovers the independently admitted exact stage after restart without the original caller; read-only evidence cannot advance it and physical effects are not duplicated.</summary>
    [Fact]
    public async Task LaterMutationRecoversPreJournalOriginalWithoutItsCaller()
    {
        var payload = Payload(); string id = DeletionBatchCapabilityCodec.SigningRequestId(payload); var clock = new CustodyFixtureClock(); var backend = new InMemoryStateManager(); var authority = Authority();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var original = Signed(payload, key); var provider = Substitute.For<IDeletionCapabilitySigningProvider>();
        provider.SignAsync(payload, id, Arg.Any<CancellationToken>()).Returns(_ => { AnchoredFixtureJournal.SetAvailable(authority, false); return original; });
        provider.LookupAsync(payload, id, Arg.Any<CancellationToken>()).Returns(new DeletionCapabilitySigningResult(new(id, payload, DeletionCapabilitySigningState.Unknown), null));
        var noIssue = Substitute.For<IDeletionCapabilityNoIssueAuthority>(); string digest = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(original.Outcome!.DetachedJws!)));
        var proof = new DeletionCapabilityNoIssueProof(payload, id, digest, "original-no-issue-proof", 9, "new-healthy-key", "independent-current-guard", clock.Now, clock.Now.AddMinutes(1));
        noIssue.ReadAsync(payload, id, digest, Arg.Any<CancellationToken>()).Returns(proof);
        (await Actor(payload, backend, authority, provider, Trust(payload, key)).SignAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Unknown);
        var stage = backend.CommittedState.Values.OfType<AnchoredStateTransition>().Single(); authority.ClearReceivedCalls();
        (await Actor(payload, backend, authority, provider, Trust(payload, key)).LookupAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Unknown);
        await authority.DidNotReceive().RecordTransitionAsync(Arg.Any<AnchoredStateTransition>(), Arg.Any<CancellationToken>());
        backend.CommittedState.Values.OfType<AnchoredStateTransition>().Single().ShouldBe(stage); AnchoredFixtureJournal.SetAvailable(authority, true);
        var recovered = await Actor(payload, backend, authority, provider, Trust(payload, key), noIssueAuthority: noIssue, clock: clock).ObsoleteUnissuedAsync(payload);
        recovered.State.ShouldBe(DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued); recovered.DetachedJws.ShouldBe(original.Outcome!.DetachedJws);
        backend.CommittedState.Single().Value.ShouldBeOfType<DeletionCapabilitySigningOutcome>().ShouldBe(recovered);
        await provider.Received(1).SignAsync(payload, id, Arg.Any<CancellationToken>());
    }

    private static DeletionBatchCapabilityV1 Payload() => new("issuer", "protection-v1", "tenant-a", "request-1", "seal-1", "accepted", 0,
        "batch-1", new string('A', 64), "tenant-a:governance:guard-1", 7, 1, 1, "key-v1");
    private static DeletionCapabilitySigningResult Signed(DeletionBatchCapabilityV1 payload, ECDsa key)
    {
        var profile = new DeletionCapabilityTrustProfile(payload.Issuer, payload.Audience, payload.TenantId, payload.CapabilityKeyVersion, "anchor-1", "anchor-v1");
        return new(new(DeletionBatchCapabilityCodec.SigningRequestId(payload), payload, DeletionCapabilitySigningState.Signed,
            DeletionBatchCapabilityCodec.Sign(payload, profile, key), profile.PublicAnchorId, profile.PublicAnchorVersion), key.ExportSubjectPublicKeyInfo());
    }
    private static DeletionCapabilitySigningActor Actor(DeletionBatchCapabilityV1 payload, IActorStateManager backend,
        IDeletionCapabilitySigningAuthority? authority = null, IDeletionCapabilitySigningProvider? provider = null,
        IDeletionCapabilitySigningTrustProvider? trust = null, IDeletionCapabilityOriginalSigningReceiptAuthority? originalAuthority = null,
        IDeletionCapabilityNoIssueAuthority? noIssueAuthority = null, TimeProvider? clock = null)
    {
        var actor = new DeletionCapabilitySigningActor(ActorHost.CreateForTest<DeletionCapabilitySigningActor>(new ActorTestOptions
            { ActorId = new(DeletionCapabilitySigningActor.GetActorId(payload)) }), authority, provider, trust, originalAuthority, noIssueAuthority, clock);
        typeof(Dapr.Actors.Runtime.Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, backend); return actor;
    }
    private static IDeletionCapabilitySigningTrustProvider Trust(DeletionBatchCapabilityV1 payload, ECDsa key)
    {
        var trust = Substitute.For<IDeletionCapabilitySigningTrustProvider>();
        trust.ResolveAsync(payload.TenantId, "DeletionBatchCapabilitySigningKey", payload.CapabilityKeyVersion, Arg.Any<CancellationToken>())
            .Returns(new DeletionCapabilityPublishedTrust(payload.TenantId, "DeletionBatchCapabilitySigningKey", payload.CapabilityKeyVersion,
                payload.Issuer, payload.Audience, "anchor-1", "anchor-v1", key.ExportSubjectPublicKeyInfo(), 1, true));
        return trust;
    }
    private static IDeletionCapabilitySigningAuthority Authority()
    {
        var authority = Substitute.For<IDeletionCapabilitySigningAuthority>(); long revision = 0; string anchor = Digest((DeletionCapabilitySigningOutcome?)null);
        authority.ValidateStateAsync(Arg.Any<DeletionBatchCapabilityV1>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => call.ArgAt<string>(2) == anchor);
        authority.RecordStateAsync(Arg.Any<DeletionBatchCapabilityV1>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => {
            if (call.ArgAt<string>(2) != anchor) { return false; } anchor = call.ArgAt<string>(3); return true;
        });
        AnchoredFixtureJournal.Attach(authority, DeletionCapabilitySigningActor.GetActorId(Payload()) + "|deletion-capability-signing-v1", () => (revision, anchor), (next, digest) => { revision = next; anchor = digest; });
        authority.AuthorizeOperationAsync(Arg.Any<DeletionBatchCapabilityV1>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        authority.AuthorizeAsync(Arg.Any<DeletionBatchCapabilityV1>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true); return authority;
    }

    /// <summary>Lost signing response is recovered only through exact result lookup; stored signature remains identical through serialized restart.</summary>
    [Fact]
    public async Task LostSigningResponseReusesDurableExactSignatureWithoutResigning()
    {
        var payload = Payload(); string id = DeletionBatchCapabilityCodec.SigningRequestId(payload); var backend = new InMemoryStateManager();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var signed = Signed(payload, key);
        var provider = Substitute.For<IDeletionCapabilitySigningProvider>();
        provider.SignAsync(payload, id, Arg.Any<CancellationToken>()).Returns(Task.FromException<DeletionCapabilitySigningResult>(new HttpRequestException("Controlled response lost after backend signature retention.")));
        provider.LookupAsync(payload, id, Arg.Any<CancellationToken>()).Returns(signed);
        var authority = Authority(); var actor = Actor(payload, backend, authority, provider, Trust(payload, key));
        (await actor.SignAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Unknown);
        backend.CommittedState.Single().Value.ShouldBeOfType<DeletionCapabilitySigningOutcome>().State.ShouldBe(DeletionCapabilitySigningState.Unknown);
        (await actor.SignAsync(payload)).ShouldBe(signed.Outcome);
        var saved = backend.CommittedState.Single(); var restored = new InMemoryStateManager();
        await restored.SetStateAsync(saved.Key, JsonSerializer.Deserialize<DeletionCapabilitySigningOutcome>(JsonSerializer.SerializeToUtf8Bytes(saved.Value))!, TestContext.Current.CancellationToken);
        await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        (await Actor(payload, restored, authority).LookupAsync(payload)).ShouldBe(signed.Outcome);
        await provider.Received(1).SignAsync(payload, id, Arg.Any<CancellationToken>()); await provider.Received(1).LookupAsync(payload, id, Arg.Any<CancellationToken>());
        JsonSerializer.Serialize(saved.Value).ShouldNotContain("CommittedIssuedGuardRevision");
    }

    /// <summary>Healthy routine rotation cannot strand an original lost signing result; retained verifier recovers it without fresh signing authority.</summary>
    [Fact]
    public async Task LostSigningResultAfterHealthyRotationUsesRetainedIndependentAnchor()
    {
        var payload = Payload(); string id = DeletionBatchCapabilityCodec.SigningRequestId(payload); var backend = new InMemoryStateManager();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var signed = Signed(payload, key); var trust = Trust(payload, key);
        var current = await trust.ResolveAsync(payload.TenantId, "DeletionBatchCapabilitySigningKey", payload.CapabilityKeyVersion, TestContext.Current.CancellationToken);
        var provider = Substitute.For<IDeletionCapabilitySigningProvider>();
        provider.SignAsync(payload, id, Arg.Any<CancellationToken>()).Returns(Task.FromException<DeletionCapabilitySigningResult>(new HttpRequestException("Controlled lost signature response.")));
        provider.LookupAsync(payload, id, Arg.Any<CancellationToken>()).Returns(signed);
        var actor = Actor(payload, backend, Authority(), provider, trust); (await actor.SignAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Unknown);
        trust.ResolveAsync(payload.TenantId, "DeletionBatchCapabilitySigningKey", payload.CapabilityKeyVersion, Arg.Any<CancellationToken>())
            .Returns(current! with { IsCurrentNonRevoked = false, IsRevoked = false, TrustProfileRevision = 2 });
        (await actor.LookupAsync(payload)).ShouldBe(signed.Outcome);
        backend.CommittedState.Single().Value.ShouldBeOfType<DeletionCapabilitySigningOutcome>().ShouldBe(signed.Outcome);
        await provider.Received(1).SignAsync(payload, id, Arg.Any<CancellationToken>()); await provider.Received(1).LookupAsync(payload, id, Arg.Any<CancellationToken>());
        // A fresh request under that healthy retained version still cannot call the signer.
        var fresh = payload with { SigningAttemptOrdinal = 2 }; var freshBackend = new InMemoryStateManager();
        (await Actor(fresh, freshBackend, Authority(), provider, trust).SignAsync(fresh)).State.ShouldBe(DeletionCapabilitySigningState.Unavailable);
        freshBackend.CommittedState.ShouldBeEmpty(); await provider.DidNotReceive().SignAsync(fresh, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Missing or denied independent recorded authorization never invokes signing; healthy key configuration alone grants nothing.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrDeniedAuthorityCannotSign(bool denied)
    {
        var payload = Payload(); var backend = new InMemoryStateManager(); var provider = Substitute.For<IDeletionCapabilitySigningProvider>();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var authority = Authority(); authority.AuthorizeAsync(Arg.Any<DeletionBatchCapabilityV1>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        (await Actor(payload, backend, denied ? authority : null, provider, Trust(payload, key)).SignAsync(payload)).State
            .ShouldBe(denied ? DeletionCapabilitySigningState.Denied : DeletionCapabilitySigningState.Unavailable);
        await provider.DidNotReceive().SignAsync(Arg.Any<DeletionBatchCapabilityV1>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        if (denied) { backend.CommittedState.Single().Value.ShouldBeOfType<DeletionCapabilitySigningOutcome>().State.ShouldBe(DeletionCapabilitySigningState.Denied); }
        else { backend.CommittedState.ShouldBeEmpty(); }
    }

    /// <summary>Provider payload/header/public-key substitution or oversized public anchor cannot release a Signed artifact.</summary>
    [Theory]
    [InlineData("payload")]
    [InlineData("anchor")]
    [InlineData("header")]
    [InlineData("anchor-length")]
    public async Task MalformedProviderResultCannotBeRetainedAsSigned(string vector)
    {
        var payload = Payload(); var backend = new InMemoryStateManager(); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256); var result = Signed(payload, key);
        result = vector switch {
            "payload" => result with { Outcome = result.Outcome with { Payload = payload with { DestructionSealId = "changed" } } },
            "anchor" => result with { PublicAnchorSubjectPublicKeyInfo = other.ExportSubjectPublicKeyInfo() },
            "header" => result with { Outcome = result.Outcome with { DetachedJws = ExportManifestSignatureCore.Sign(new("tenant-a", "export-1", 1, "key-v1"),
                DeletionBatchCapabilityCodec.CanonicalPayload(payload), key) } },
            _ => result with { PublicAnchorSubjectPublicKeyInfo = new byte[513] }
        };
        var provider = Substitute.For<IDeletionCapabilitySigningProvider>();
        provider.SignAsync(payload, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(result);
        var outcome = await Actor(payload, backend, Authority(), provider, Trust(payload, key)).SignAsync(payload);
        outcome.State.ShouldBe(vector == "payload" ? DeletionCapabilitySigningState.Conflict : DeletionCapabilitySigningState.Unknown);
        outcome.DetachedJws.ShouldBeNull(); backend.CommittedState.Single().Value.ShouldBeOfType<DeletionCapabilitySigningOutcome>().DetachedJws.ShouldBeNull();
    }

    /// <summary>Failed or lost-ack reservation persistence prevents signing and never certifies cached staged state.</summary>
    [Theory]
    [InlineData(1, false)][InlineData(1, true)][InlineData(2, false)][InlineData(2, true)]
    public async Task FailedIntentSaveCannotInvokeSigner(int failSave, bool committed)
    {
        var payload = Payload(); var backend = new InMemoryStateManager(); var authority = Authority();
        var provider = Substitute.For<IDeletionCapabilitySigningProvider>(); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        provider.SignAsync(payload, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Signed(payload, key));
        provider.LookupAsync(payload, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Signed(payload, key));
        var manager = ActorPendingFixture.Faulting<DeletionCapabilitySigningOutcome>(backend, failSave, committed);
        await Should.ThrowAsync<HttpRequestException>(() => Actor(payload, manager, authority, provider, Trust(payload, key)).SignAsync(payload));
        await provider.DidNotReceive().SignAsync(payload, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await backend.ClearCacheAsync(TestContext.Current.CancellationToken); var restarted = Actor(payload, backend, authority, provider, Trust(payload, key));
        (await restarted.SignAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Signed);
        (await restarted.LookupAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Signed);
        await provider.Received(failSave == 1 && !committed ? 1 : 0).SignAsync(payload, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await provider.Received(failSave == 1 && !committed ? 0 : 1).LookupAsync(payload, Arg.Any<string>(), Arg.Any<CancellationToken>());
        backend.CommittedState.Single().Value.ShouldBeOfType<DeletionCapabilitySigningOutcome>().State.ShouldBe(DeletionCapabilitySigningState.Signed);
    }

    /// <summary>Changed tenant/seal/compare/attempt is another exact request address and cannot overwrite the original request actor.</summary>
    [Fact]
    public async Task ChangedRequestScopeCannotOverwriteStoredResult()
    {
        var payload = Payload(); var backend = new InMemoryStateManager(); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var result = Signed(payload, key); var provider = Substitute.For<IDeletionCapabilitySigningProvider>();
        provider.SignAsync(payload, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(result);
        var actor = Actor(payload, backend, Authority(), provider, Trust(payload, key)); (await actor.SignAsync(payload)).ShouldBe(result.Outcome);
        foreach (var changed in new[] { payload with { TenantId = "tenant-b" }, payload with { DestructionSealId = "new-seal" },
            payload with { IntendedIssuedGuardRevision = 8 }, payload with { SigningAttemptOrdinal = 2 } })
        { await Should.ThrowAsync<ArgumentException>(() => actor.SignAsync(changed)); }
        backend.CommittedState.Single().Value.ShouldBeOfType<DeletionCapabilitySigningOutcome>().ShouldBe(result.Outcome);
    }

    /// <summary>Independently revoked/wrong version/anchor cannot be substituted by an internally consistent signer artifact.</summary>
    [Theory]
    [InlineData("revoked")]
    [InlineData("version")]
    [InlineData("tenant")]
    [InlineData("family")]
    [InlineData("anchor")]
    [InlineData("key")]
    public async Task IndependentTrustPreventsSelfSuppliedSignerAuthority(string vector)
    {
        var payload = Payload(); var backend = new InMemoryStateManager(); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256); var signed = Signed(payload, key);
        var expected = new DeletionCapabilityPublishedTrust(payload.TenantId, "DeletionBatchCapabilitySigningKey", payload.CapabilityKeyVersion,
            payload.Issuer, payload.Audience, "anchor-1", "anchor-v1", key.ExportSubjectPublicKeyInfo(), 1, true);
        expected = vector switch {
            "revoked" => expected with { IsCurrentNonRevoked = false, IsRevoked = true }, "version" => expected with { CapabilityKeyVersion = "v2" },
            "tenant" => expected with { TenantId = "tenant-b" }, "family" => expected with { KeyFamily = "ManifestSigningKey" },
            "anchor" => expected with { PublicAnchorId = "independent-anchor" }, _ => expected with { SubjectPublicKeyInfo = other.ExportSubjectPublicKeyInfo() }
        };
        var trust = Substitute.For<IDeletionCapabilitySigningTrustProvider>();
        trust.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(expected);
        var provider = Substitute.For<IDeletionCapabilitySigningProvider>(); provider.SignAsync(payload, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(signed);
        var outcome = await Actor(payload, backend, Authority(), provider, trust).SignAsync(payload);
        outcome.State.ShouldBe(vector is "anchor" or "key" ? DeletionCapabilitySigningState.Unknown : DeletionCapabilitySigningState.Unavailable);
        outcome.DetachedJws.ShouldBeNull();
        if (vector is "anchor" or "key") { backend.CommittedState.Single().Value.ShouldBeOfType<DeletionCapabilitySigningOutcome>().State.ShouldBe(DeletionCapabilitySigningState.Unknown); }
        else { backend.CommittedState.ShouldBeEmpty(); await provider.DidNotReceive().SignAsync(Arg.Any<DeletionBatchCapabilityV1>(), Arg.Any<string>(), Arg.Any<CancellationToken>()); }
    }

    /// <summary>A profile revoked while signing is pending cannot release or durably retain the returned signature.</summary>
    [Fact]
    public async Task RevocationAfterSignerInvocationPreventsResultRelease()
    {
        var payload = Payload(); var backend = new InMemoryStateManager(); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var trust = Trust(payload, key); var current = await trust.ResolveAsync(payload.TenantId, "DeletionBatchCapabilitySigningKey", payload.CapabilityKeyVersion, TestContext.Current.CancellationToken);
        var provider = Substitute.For<IDeletionCapabilitySigningProvider>();
        provider.SignAsync(payload, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => {
            trust.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(current! with { IsCurrentNonRevoked = false, IsRevoked = true });
            return Signed(payload, key);
        });
        (await Actor(payload, backend, Authority(), provider, trust).SignAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Unknown);
        backend.CommittedState.Single().Value.ShouldBeOfType<DeletionCapabilitySigningOutcome>().DetachedJws.ShouldBeNull();
    }

    /// <summary>Retained immutable signature cannot be read through a guessed actor address or withdrawn exact operation credential.</summary>
    [Fact]
    public async Task SignedOutcomeRequiresIndependentCurrentExactReadCredential()
    {
        var payload = Payload(); var backend = new InMemoryStateManager(); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var authority = Authority();
        var result = Signed(payload, key); var provider = Substitute.For<IDeletionCapabilitySigningProvider>(); provider.SignAsync(payload, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(result);
        var actor = Actor(payload, backend, authority, provider, Trust(payload, key)); (await actor.SignAsync(payload)).ShouldBe(result.Outcome);
        (await Actor(payload, backend).LookupAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Unavailable);
        authority.AuthorizeOperationAsync(payload, Arg.Any<string>(), "LookupDeletionCapability", Arg.Any<CancellationToken>()).Returns(false);
        (await actor.LookupAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Unavailable);
        backend.CommittedState.Single().Value.ShouldBeOfType<DeletionCapabilitySigningOutcome>().ShouldBe(result.Outcome);
        await provider.Received(1).SignAsync(payload, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
    /// <summary>After revocation only independent proof of the original committed issuance can recover a lost result; its revoked signature alone grants nothing.</summary>
    [Theory]
    [InlineData("valid")][InlineData("missing")][InlineData("wrong-receipt")]
    public async Task RevokedOriginalLostResultRequiresIndependentHistoricIssuanceProof(string vector)
    {
        var payload = Payload(); string id = DeletionBatchCapabilityCodec.SigningRequestId(payload); var backend = new InMemoryStateManager();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var signed = Signed(payload, key) with { OriginalIssuanceReceiptId = "original-committed-provider-receipt" };
        var trust = Trust(payload, key); var current = await trust.ResolveAsync(payload.TenantId, "DeletionBatchCapabilitySigningKey", payload.CapabilityKeyVersion, TestContext.Current.CancellationToken);
        var provider = Substitute.For<IDeletionCapabilitySigningProvider>();
        provider.SignAsync(payload, id, Arg.Any<CancellationToken>()).Returns(Task.FromException<DeletionCapabilitySigningResult>(new HttpRequestException("Controlled original response loss.")));
        provider.LookupAsync(payload, id, Arg.Any<CancellationToken>()).Returns(vector == "wrong-receipt" ? signed with { OriginalIssuanceReceiptId = "different-receipt" } : signed);
        var history = Substitute.For<IDeletionCapabilityOriginalSigningReceiptAuthority>();
        history.VerifyOriginalIssuanceAsync(payload, id, Arg.Is<DeletionCapabilitySigningResult>(r => r.OriginalIssuanceReceiptId == signed.OriginalIssuanceReceiptId),
            Arg.Any<DeletionCapabilityPublishedTrust>(), Arg.Any<CancellationToken>()).Returns(true);
        var actor = Actor(payload, backend, Authority(), provider, trust, vector == "missing" ? null : history);
        (await actor.SignAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Unknown);
        trust.ResolveAsync(payload.TenantId, "DeletionBatchCapabilitySigningKey", payload.CapabilityKeyVersion, Arg.Any<CancellationToken>())
            .Returns(current! with { IsCurrentNonRevoked = false, IsRevoked = true, TrustProfileRevision = 2 });
        var recovered = await actor.LookupAsync(payload); recovered.State.ShouldBe(vector == "valid" ? DeletionCapabilitySigningState.Signed : DeletionCapabilitySigningState.Unknown);
        var persisted = backend.CommittedState.Single().Value.ShouldBeOfType<DeletionCapabilitySigningOutcome>(); persisted.State.ShouldBe(recovered.State);
        if (vector == "valid") { recovered.ShouldBe(signed.Outcome); (await actor.LookupAsync(payload)).ShouldBe(signed.Outcome); }
        await provider.Received(1).SignAsync(payload, id, Arg.Any<CancellationToken>());
        var fresh = payload with { SigningAttemptOrdinal = 2 }; var freshBackend = new InMemoryStateManager();
        (await Actor(fresh, freshBackend, Authority(), provider, trust, history).SignAsync(fresh)).State.ShouldBe(DeletionCapabilitySigningState.Unavailable);
        freshBackend.CommittedState.ShouldBeEmpty(); await provider.DidNotReceive().SignAsync(fresh, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
    /// <summary>Missing, unknown or forged terminal restoration cannot replace independently anchored original signing history or restart signing.</summary>
    [Theory]
    [InlineData("absent")][InlineData("unknown")][InlineData("divergent")]
    public async Task RestoredSigningStateCannotReplaceOriginalArtifact(string restore)
    {
        var payload = Payload(); string id = DeletionBatchCapabilityCodec.SigningRequestId(payload); var backend = new InMemoryStateManager(); var authority = Authority();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var original = Signed(payload, key); var provider = Substitute.For<IDeletionCapabilitySigningProvider>();
        provider.SignAsync(payload, id, Arg.Any<CancellationToken>()).Returns(Task.FromException<DeletionCapabilitySigningResult>(new HttpRequestException("Controlled original signing loss.")));
        provider.LookupAsync(payload, id, Arg.Any<CancellationToken>()).Returns(original); var actor = Actor(payload, backend, authority, provider, Trust(payload, key));
        await actor.SignAsync(payload); var pending = backend.CommittedState.Single(); await actor.LookupAsync(payload); var latest = backend.CommittedState.Single(); var restored = new InMemoryStateManager();
        if (restore != "absent") { await restored.SetStateAsync(latest.Key, restore == "unknown" ? pending.Value : original.Outcome with { DetachedJws = "forged-terminal-artifact" }, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken); }
        var restarted = Actor(payload, restored, authority, provider, Trust(payload, key)); await Should.ThrowAsync<InvalidOperationException>(() => restarted.LookupAsync(payload));
        await restored.SetStateAsync(latest.Key, JsonSerializer.Deserialize<DeletionCapabilitySigningOutcome>(JsonSerializer.Serialize(latest.Value))!, TestContext.Current.CancellationToken); await restored.SaveStateAsync(TestContext.Current.CancellationToken);
        (await restarted.LookupAsync(payload)).ShouldBe(original.Outcome); await provider.Received(1).SignAsync(payload, id, Arg.Any<CancellationToken>());
    }

    /// <summary>Only exact irreversible current no-issue proof terminalizes the original artifact; restart retains it and successor identity never renews the batch.</summary>
    [Theory]
    [InlineData("valid")][InlineData("missing")][InlineData("foreign")][InlineData("expired")][InlineData("stale-revision")][InlineData("changed")]
    public async Task ExactNoIssueProofIsRequiredForStableSuccessor(string vector)
    {
        var payload = Payload(); string id = DeletionBatchCapabilityCodec.SigningRequestId(payload);
        var clock = new CustodyFixtureClock(); var backend = new InMemoryStateManager(); var authority = Authority();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var signed = Signed(payload, key);
        var provider = Substitute.For<IDeletionCapabilitySigningProvider>(); provider.SignAsync(payload, id, Arg.Any<CancellationToken>()).Returns(signed);
        var noIssue = Substitute.For<IDeletionCapabilityNoIssueAuthority>();
        string digest = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(signed.Outcome!.DetachedJws!)));
        var proof = new DeletionCapabilityNoIssueProof(payload, id, digest, "original-terminal-no-issue", 9, "current-key-v2",
            "independent-current-guard", clock.Now, clock.Now.AddMinutes(1));
        proof = vector switch { "foreign" => proof with { Payload = payload with { TenantId = "tenant-foreign" } },
            "expired" => proof with { ValidUntil = clock.Now }, "stale-revision" => proof with { CurrentGuardRevision = payload.IntendedIssuedGuardRevision }, _ => proof };
        int reads = 0;
        noIssue.ReadAsync(payload, id, digest, Arg.Any<CancellationToken>()).Returns(_ => vector == "missing" ? null
            : vector == "changed" && ++reads > 1 ? proof with { ProofId = "different-proof" } : proof);
        var actor = Actor(payload, backend, authority, provider, Trust(payload, key), noIssueAuthority: noIssue, clock: clock);
        (await actor.SignAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Signed);
        var result = await actor.ObsoleteUnissuedAsync(payload);
        result.State.ShouldBe(vector == "valid" ? DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued : DeletionCapabilitySigningState.Unavailable);
        if (vector == "valid")
        {
            var restarted = Actor(payload, backend, authority, provider, Trust(payload, key), noIssueAuthority: noIssue, clock: clock);
            (await restarted.LookupAsync(payload)).ShouldBe(result); (await restarted.ObsoleteUnissuedAsync(payload)).ShouldBe(result);
            var successor = DeletionCapabilitySigningSuccessor.Create(result, clock.Now)!;
            successor.ShouldBe(payload with { SigningAttemptOrdinal = 2, IntendedIssuedGuardRevision = 9, CapabilityKeyVersion = "current-key-v2" });
            DeletionBatchCapabilityCodec.SigningRequestId(successor).ShouldNotBe(id);
            DeletionCapabilitySigningSuccessor.Create(result, proof.ValidUntil).ShouldBeNull();
            result.DetachedJws.ShouldBe(signed.Outcome!.DetachedJws);
        }
        else { (await actor.LookupAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Signed); }
        await provider.Received(1).SignAsync(payload, id, Arg.Any<CancellationToken>());
    }

    /// <summary>Suspended sign/lookup releases the modeled turn without late persistence; serialized original lookup after compromise requires the independent historic receipt and never signs again.</summary>
    [Theory]
    [InlineData(false, false)][InlineData(false, true)][InlineData(true, false)][InlineData(true, true)]
    public async Task SuspendedPhysicalSigningReleasesTurnWithoutLateArtifact(bool lookup, bool invocation)
    {
        var (clock, advance) = PrivateOwnerDeadlineTestClock.Create(); var payload = Payload(); string id = DeletionBatchCapabilityCodec.SigningRequestId(payload);
        var backend = new InMemoryStateManager(); var authority = Authority();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var original = Signed(payload, key) with { OriginalIssuanceReceiptId = "retained-original-issuance" };
        var trust = Trust(payload, key); var current = await trust.ResolveAsync(payload.TenantId, "DeletionBatchCapabilitySigningKey", payload.CapabilityKeyVersion, TestContext.Current.CancellationToken);
        var receipt = Substitute.For<IDeletionCapabilityOriginalSigningReceiptAuthority>();
        receipt.VerifyOriginalIssuanceAsync(payload, id, Arg.Is<DeletionCapabilitySigningResult>(r => r.OriginalIssuanceReceiptId == original.OriginalIssuanceReceiptId),
            Arg.Any<DeletionCapabilityPublishedTrust>(), Arg.Any<CancellationToken>()).Returns(true);
        var provider = Substitute.For<IDeletionCapabilitySigningProvider>();
        if (lookup)
        {
            provider.SignAsync(payload, id, Arg.Any<CancellationToken>()).Returns(Task.FromException<DeletionCapabilitySigningResult>(new HttpRequestException("Original signing acknowledgement lost.")));
            (await Actor(payload, backend, authority, provider, trust, receipt, clock: clock).SignAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Unknown);
        }
        using var release = new ManualResetEventSlim(); using var turn = new SemaphoreSlim(1);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<DeletionCapabilitySigningResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<DeletionCapabilitySigningResult> Suspend()
        {
            entered.TrySetResult(); if (!invocation) { return pending.Task; }
            release.Wait(); returned.TrySetResult(); return Task.FromResult(original);
        }
        if (lookup) { provider.LookupAsync(payload, id, Arg.Any<CancellationToken>()).Returns(_ => Suspend()); }
        else { provider.SignAsync(payload, id, Arg.Any<CancellationToken>()).Returns(_ => Suspend()); }
        var actor = Actor(payload, backend, authority, provider, trust, receipt, clock: clock);
        async Task<T> Turn<T>(Func<Task<T>> operation)
        { await turn.WaitAsync(TestContext.Current.CancellationToken); try { return await operation(); } finally { turn.Release(); } }
        var waiting = Task.Run(() => Turn(() => lookup ? actor.LookupAsync(payload) : actor.SignAsync(payload)), TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); advance(TimeSpan.FromSeconds(30));
            (await waiting.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).State.ShouldBe(DeletionCapabilitySigningState.Unknown);
            var saved = backend.CommittedState.Single(); string before = JsonSerializer.Serialize(saved.Value);
            ((DeletionCapabilitySigningOutcome)saved.Value).Payload.ShouldBe(payload); ((DeletionCapabilitySigningOutcome)saved.Value).DetachedJws.ShouldBeNull();
            authority.AuthorizeOperationAsync(payload, id, "SignDeletionCapability", Arg.Any<CancellationToken>()).Returns(false);
            (await Turn(() => actor.SignAsync(payload)).WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).State.ShouldBe(DeletionCapabilitySigningState.Unavailable);
            if (invocation) { release.Set(); await returned.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); }
            else { pending.TrySetResult(original); }
            JsonSerializer.Serialize(backend.CommittedState.Single().Value).ShouldBe(before);
            var restored = new InMemoryStateManager();
            await restored.SetStateAsync(saved.Key, JsonSerializer.Deserialize<DeletionCapabilitySigningOutcome>(before)!, TestContext.Current.CancellationToken);
            await restored.SaveStateAsync(TestContext.Current.CancellationToken);
            authority.AuthorizeAsync(payload, id, Arg.Any<CancellationToken>()).Returns(false);
            trust.ResolveAsync(payload.TenantId, "DeletionBatchCapabilitySigningKey", payload.CapabilityKeyVersion, Arg.Any<CancellationToken>())
                .Returns(current! with { IsCurrentNonRevoked = false, IsRevoked = true, TrustProfileRevision = 2 });
            provider.LookupAsync(payload, id, Arg.Any<CancellationToken>()).Returns(original);
            var restarted = Actor(payload, restored, authority, provider, trust, receipt, clock: clock);
            (await restarted.LookupAsync(payload)).ShouldBe(original.Outcome); (await restarted.LookupAsync(payload)).ShouldBe(original.Outcome);
            restored.CommittedState.Single().Value.ShouldBe(original.Outcome);
            await provider.Received(1).SignAsync(payload, id, Arg.Any<CancellationToken>());
            await receipt.Received(1).VerifyOriginalIssuanceAsync(payload, id, Arg.Any<DeletionCapabilitySigningResult>(), Arg.Any<DeletionCapabilityPublishedTrust>(), Arg.Any<CancellationToken>());
        }
        finally { release.Set(); pending.TrySetResult(original); }
    }

    /// <summary>The last I-JSON attempt remains canonical and signable; exhaustion produces no unrepresentable successor and leaves the original artifact/proof unchanged.</summary>
    [Theory]
    [InlineData(9007199254740990L)][InlineData(9007199254740991L)]
    public void SuccessorStopsAtCanonicalAttemptLimit(long attempt)
    {
        var clock = new CustodyFixtureClock(); var payload = Payload() with { SigningAttemptOrdinal = attempt };
        using var originalKey = ECDsa.Create(ECCurve.NamedCurves.nistP256); var signed = Signed(payload, originalKey).Outcome!;
        string digest = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(signed.DetachedJws!)));
        var proof = new DeletionCapabilityNoIssueProof(payload, signed.SigningRequestId, digest, "exact-original-no-issue", 9,
            "independent-healthy-key", "current-independent-guard", clock.Now, clock.Now.AddMinutes(1));
        var original = signed with { State = DeletionCapabilitySigningState.SignedAttestationObsoleteUnissued, NoIssueProof = proof };
        string before = JsonSerializer.Serialize(original);
        var successor = DeletionCapabilitySigningSuccessor.Create(original, proof, clock.Now);
        if (attempt == 9007199254740991L) { successor.ShouldBeNull(); }
        else
        {
            successor.ShouldBe(payload with { SigningAttemptOrdinal = 9007199254740991L, IntendedIssuedGuardRevision = 9, CapabilityKeyVersion = proof.CurrentHealthyKeyVersion });
            DeletionBatchCapabilityCodec.CanonicalPayload(successor!).Length.ShouldBeGreaterThan(0);
            using var currentKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var profile = new DeletionCapabilityTrustProfile(successor!.Issuer, successor.Audience, successor.TenantId,
                successor.CapabilityKeyVersion, "current-independent-anchor", "version-2");
            string jws = DeletionBatchCapabilityCodec.Sign(successor, profile, currentKey);
            DeletionBatchCapabilityCodec.Verify(successor, profile, jws, currentKey).ShouldBeTrue();
            DeletionBatchCapabilityCodec.SigningRequestId(successor).ShouldNotBe(original.SigningRequestId);
        }
        JsonSerializer.Serialize(original).ShouldBe(before);
    }

    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    /// <summary>Restart after the original proof lease expires retains that original artifact/proof and recovers only a fresh independent exact successor basis.</summary>
    [Theory]
    [InlineData(true)][InlineData(false)]
    public async Task ObsoleteRestartReadsCurrentSuccessorWithoutChangingOriginalOutcome(bool currentAvailable)
    {
        var payload = Payload(); string id = DeletionBatchCapabilityCodec.SigningRequestId(payload); var clock = new CustodyFixtureClock(); var backend = new InMemoryStateManager(); var authority = Authority();
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var signed = Signed(payload, key); var provider = Substitute.For<IDeletionCapabilitySigningProvider>();
        provider.SignAsync(payload, id, Arg.Any<CancellationToken>()).Returns(signed); var noIssue = Substitute.For<IDeletionCapabilityNoIssueAuthority>();
        string digest = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(signed.Outcome!.DetachedJws!)));
        var proof = new DeletionCapabilityNoIssueProof(payload, id, digest, "original-no-issue", 9, "key-b", "authority-a", clock.Now, clock.Now.AddMinutes(1));
        noIssue.ReadAsync(payload, id, digest, Arg.Any<CancellationToken>()).Returns(_ => proof);
        var actor = Actor(payload, backend, authority, provider, Trust(payload, key), noIssueAuthority: noIssue, clock: clock);
        await actor.SignAsync(payload); var original = await actor.ObsoleteUnissuedAsync(payload); var persisted = backend.CommittedState.Single();
        clock.Now += TimeSpan.FromMinutes(2); proof = proof with { CurrentGuardRevision = 17, CurrentHealthyKeyVersion = "key-c", AuthorityRevision = "authority-b", ObservedAt = clock.Now, ValidUntil = clock.Now.AddMinutes(1) };
        noIssue.ReadAsync(payload, id, digest, Arg.Any<CancellationToken>()).Returns(_ => currentAvailable ? proof : null);
        var restarted = Actor(payload, backend, authority, provider, Trust(payload, key), noIssueAuthority: noIssue, clock: clock);
        (await restarted.SignAsync(payload)).ShouldBe(original); var current = await restarted.ReadSuccessorProofAsync(payload);
        var successor = DeletionCapabilitySigningSuccessor.Create(original, current, clock.Now);
        if (currentAvailable) { successor.ShouldBe(payload with { SigningAttemptOrdinal = 2, IntendedIssuedGuardRevision = 17, CapabilityKeyVersion = "key-c" }); }
        else { successor.ShouldBeNull(); }
        backend.CommittedState.Single().ShouldBe(persisted); (await restarted.LookupAsync(payload)).ShouldBe(original);
        await provider.Received(1).SignAsync(payload, id, Arg.Any<CancellationToken>());
    }

}
