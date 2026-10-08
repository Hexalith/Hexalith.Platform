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
        var authority = Substitute.For<IDeletionCapabilitySigningAuthority>(); string anchor = Digest((DeletionCapabilitySigningOutcome?)null);
        authority.ValidateStateAsync(Arg.Any<DeletionBatchCapabilityV1>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => call.ArgAt<string>(2) == anchor);
        authority.RecordStateAsync(Arg.Any<DeletionBatchCapabilityV1>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => {
            if (call.ArgAt<string>(2) != anchor) { return false; } anchor = call.ArgAt<string>(3); return true;
        });
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedIntentSaveCannotInvokeSigner(bool commitBeforeFault)
    {
        var payload = Payload(); var backend = new InMemoryStateManager(); var manager = Substitute.For<IActorStateManager>();
        var provider = Substitute.For<IDeletionCapabilitySigningProvider>(); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        manager.ClearCacheAsync(Arg.Any<CancellationToken>()).Returns(call => backend.ClearCacheAsync(call.Arg<CancellationToken>()));
        manager.TryGetStateAsync<DeletionCapabilitySigningOutcome>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => backend.TryGetStateAsync<DeletionCapabilitySigningOutcome>(call.Arg<string>(), call.Arg<CancellationToken>()));
        manager.SetStateAsync(Arg.Any<string>(), Arg.Any<DeletionCapabilitySigningOutcome>(), Arg.Any<CancellationToken>()).Returns(call => backend.SetStateAsync(call.Arg<string>(), call.Arg<DeletionCapabilitySigningOutcome>(), call.Arg<CancellationToken>()));
        manager.SaveStateAsync(Arg.Any<CancellationToken>()).Returns(async call =>
        { if (commitBeforeFault) { await backend.SaveStateAsync(call.Arg<CancellationToken>()); } throw new HttpRequestException("Controlled signing intent save fault."); });
        await Should.ThrowAsync<HttpRequestException>(() => Actor(payload, manager, Authority(), provider, Trust(payload, key)).SignAsync(payload));
        if (commitBeforeFault) { backend.CommittedState.Single().Value.ShouldBeOfType<DeletionCapabilitySigningOutcome>().State.ShouldBe(DeletionCapabilitySigningState.Unknown); }
        else { backend.CommittedState.ShouldBeEmpty(); }
        (await Actor(payload, backend).LookupAsync(payload)).State.ShouldBe(DeletionCapabilitySigningState.Unavailable);
        await provider.DidNotReceive().SignAsync(Arg.Any<DeletionBatchCapabilityV1>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
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
