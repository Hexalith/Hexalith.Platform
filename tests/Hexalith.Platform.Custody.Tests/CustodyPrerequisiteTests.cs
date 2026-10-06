using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Local cryptographic and refusal evidence, never live S1–S4 acceptance.</summary>
public sealed class CustodyPrerequisiteTests
{
    private readonly CustodyFixtureClock _clock = new();
    private readonly CustodyFixtureProfileProvider _profiles;
    private readonly CustodyFixtureKeyProvider _keys;
    private readonly TrustedEnvelopeAuthenticator _envelopes;
    private readonly PlatformHmacService _digests;
    /// <summary>Creates independently scoped fixture providers.</summary>
    public CustodyPrerequisiteTests()
    {
        _profiles = new(_clock);
        _keys = new(_clock);
        _envelopes = new(_keys, _profiles, _clock);
        _digests = new(_keys, _profiles, _clock);
    }
    private TrustedEnvelopeIdentity Identity() => new(1, "fixture-r1", "fixture-issuer",
        new(TrustedPrincipalKind.User, "tenant-a", "actor-1", "party-1", 1, "tenant-role-r1", null, null, null, null),
        "Contracts.EditProposal", "EditProposal", "tenant-a", "interaction-1", "correlation-1", "cause-1",
        new[] { "tenant-a", "User", "actor-1", "EditProposal", "client-key" }, "fingerprint", "digest-v1", "fixture-audience", "logical-id");
    private Task<TrustedEnvelopeResult> IssueAsync(TrustedEnvelopeIdentity? identity = null)
        => _envelopes.IssueAsync(identity ?? Identity(), TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);
    /// <summary>Verifies canonical golden bytes  preserve utf16 lengths and absent versus empty.</summary>
    [Fact]
    public void CanonicalGoldenBytes_PreserveUtf16LengthsAndAbsentVersusEmpty()
    {
        Convert.ToHexStringLower(PlatformCanonicalBytes.Identity(["é", "😀"])).ShouldBe("313ac3a91f323af09f98801f");
        Convert.ToHexStringLower(PlatformCanonicalBytes.Components([null, "", "A"])).ShouldBe("00303a1f01303a1f01313a411f");
        Should.Throw<System.Text.EncoderFallbackException>(() => PlatformCanonicalBytes.Identity(["\ud800"]));
    }
    /// <summary>Verifies content digest  golden hmac and purpose isolation.</summary>
    [Fact]
    public async Task ContentDigest_GoldenHmacAndPurposeIsolation()
    {
        PlatformHmacResult result = await _digests.DigestAsync(new("tenant-a", PlatformHmacPurpose.ContentDigest), ["hello"], cancellationToken: TestContext.Current.CancellationToken);
        result.Status.ShouldBe(CustodyStatus.Succeeded);
        result.Digest.ShouldBe("443a225ab96a6e5af3558e9c83fef0be9f9f303ab71efbad9d4323208837bae1");
        result.KeyVersion.ShouldBe("key-v1");
        PlatformHmacResult observation = await _digests.DigestAsync(new("system", PlatformHmacPurpose.SecurityObservation), ["hello"], cancellationToken: TestContext.Current.CancellationToken);
        observation.Status.ShouldBe(CustodyStatus.Succeeded);
        observation.Digest.ShouldNotBe(result.Digest);
        (await _digests.DigestAsync(new("tenant-a", PlatformHmacPurpose.SecurityObservation), ["hello"], cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.Invalid);
        _keys.Snapshots.ShouldAllBe(k => k.IsDisposed);
    }
    /// <summary>Verifies issue verify and redispatch  retain logical identity but refresh delivery.</summary>
    [Fact]
    public async Task IssueVerifyAndRedispatch_RetainLogicalIdentityButRefreshDelivery()
    {
        TrustedEnvelope first = (await IssueAsync()).Envelope!;
        (await _envelopes.VerifyAsync(first, Identity(), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.Succeeded);
        _keys.RetiredAt = _clock.Now;
        _keys.CurrentVersion = "key-v2";
        _clock.Now += TimeSpan.FromSeconds(1);
        TrustedEnvelope next = (await IssueAsync()).Envelope!;
        next.Identity.LogicalCommandId.ShouldBe(first.Identity.LogicalCommandId);
        next.Identity.IdempotencyTuple.ShouldBe(first.Identity.IdempotencyTuple);
        next.Identity.PayloadFingerprint.ShouldBe(first.Identity.PayloadFingerprint);
        next.Identity.DigestKeyVersion.ShouldBe(first.Identity.DigestKeyVersion);
        next.DeliveryNonce.ShouldNotBe(first.DeliveryNonce);
        next.SigningKeyVersion.ShouldBe("key-v2");
        next.Tag.ShouldNotBe(first.Tag);
        next.IssuedAt.ShouldBeGreaterThan(first.IssuedAt);
        _keys.Snapshots.ShouldAllBe(k => k.IsDisposed);
    }
    /// <summary>Verifies changed authenticated field  is rejected.</summary>
    [Theory]
    [InlineData("schema")][InlineData("profile")][InlineData("issuer")][InlineData("audience")]
    [InlineData("kind")][InlineData("actor-tenant")][InlineData("actor")][InlineData("party")]
    [InlineData("binding")][InlineData("role")][InlineData("workflow-kind")][InlineData("workflow-instance")]
    [InlineData("activity")][InlineData("on-behalf")][InlineData("contract")][InlineData("operation")]
    [InlineData("tenant")][InlineData("resource")][InlineData("correlation")][InlineData("causation")]
    [InlineData("tuple")][InlineData("fingerprint")][InlineData("digest-version")][InlineData("logical")]
    [InlineData("issued")][InlineData("expires")][InlineData("nonce")][InlineData("signing-version")][InlineData("tag")]
    public async Task ChangedAuthenticatedField_IsRejected(string field)
    {
        TrustedEnvelope valid = (await IssueAsync()).Envelope!;
        TrustedEnvelopeIdentity i = valid.Identity;
        TrustedPrincipal p = i.Principal;
        TrustedEnvelope altered = field switch
        {
            "schema" => valid with { Identity = i with { SchemaVersion = 2 } },
            "profile" => valid with { Identity = i with { ProfileVersion = "r2" } },
            "issuer" => valid with { Identity = i with { Issuer = "wrong" } },
            "audience" => valid with { Identity = i with { Audience = "wrong" } },
            "kind" => valid with { Identity = i with { Principal = p with { Kind = TrustedPrincipalKind.Administrator } } },
            "actor-tenant" => valid with { Identity = i with { Principal = p with { ActorTenantId = "foreign" } } },
            "actor" => valid with { Identity = i with { Principal = p with { HumanActorId = "actor-2" } } },
            "party" => valid with { Identity = i with { Principal = p with { PartyId = "party-2" } } },
            "binding" => valid with { Identity = i with { Principal = p with { BindingVersion = 2 } } },
            "role" => valid with { Identity = i with { Principal = p with { RoleBasis = "r2" } } },
            "workflow-kind" => valid with { Identity = i with { Principal = p with { WorkflowKind = "Interaction" } } },
            "workflow-instance" => valid with { Identity = i with { Principal = p with { WorkflowInstanceId = "other" } } },
            "activity" => valid with { Identity = i with { Principal = p with { Activity = "other" } } },
            "on-behalf" => valid with { Identity = i with { Principal = p with { OnBehalfOfPartyId = "party-2" } } },
            "contract" => valid with { Identity = i with { CommandContract = "Contracts.Delete" } },
            "operation" => valid with { Identity = i with { OperationFamily = "Delete" } },
            "tenant" => valid with { Identity = i with { TargetTenantId = "tenant-b" } },
            "resource" => valid with { Identity = i with { TargetResource = "interaction-2" } },
            "correlation" => valid with { Identity = i with { CorrelationId = "other" } },
            "causation" => valid with { Identity = i with { CausationId = "other" } },
            "tuple" => valid with { Identity = i with { IdempotencyTuple = new[] { "changed" } } },
            "fingerprint" => valid with { Identity = i with { PayloadFingerprint = "changed" } },
            "digest-version" => valid with { Identity = i with { DigestKeyVersion = "changed" } },
            "logical" => valid with { Identity = i with { LogicalCommandId = "changed" } },
            "issued" => valid with { IssuedAt = valid.IssuedAt.AddTicks(1) },
            "expires" => valid with { ExpiresAt = valid.ExpiresAt.AddTicks(1) },
            "nonce" => valid with { DeliveryNonce = "changed" },
            "signing-version" => valid with { SigningKeyVersion = "key-v2" },
            _ => valid with { Tag = new string('0', 64) },
        };
        // Matching the altered expected identity forces tag coverage instead of relying on identity mismatch.
        (await _envelopes.VerifyAsync(altered, altered.Identity, TestContext.Current.CancellationToken)).Status.ShouldNotBe(CustodyStatus.Succeeded);
    }
    /// <summary>Verifies independently expected concrete contract  is required.</summary>
    [Fact]
    public async Task IndependentlyExpectedConcreteContract_IsRequired()
    {
        TrustedEnvelope valid = (await IssueAsync()).Envelope!;
        (await _envelopes.VerifyAsync(valid, Identity() with { CommandContract = "Contracts.Other" }, TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.ScopeMismatch);
    }
    /// <summary>Verifies party free human shape  requires fresh authority basis.</summary>
    [Theory]
    [InlineData(TrustedPrincipalKind.Administrator)][InlineData(TrustedPrincipalKind.Platform)]
    public async Task PartyFreeHumanShape_RequiresFreshAuthorityBasis(TrustedPrincipalKind kind)
    {
        TrustedPrincipal p = new(kind, kind == TrustedPrincipalKind.Platform ? "system" : "tenant-a", "actor-1", null, null, "authority-r1", null, null, null, null);
        (await IssueAsync(Identity() with { Principal = p })).Status.ShouldBe(CustodyStatus.Succeeded);
        (await IssueAsync(Identity() with { Principal = p with { RoleBasis = null } })).Status.ShouldBe(CustodyStatus.Invalid);
        (await IssueAsync(Identity() with { Principal = p with { PartyId = "invented" } })).Status.ShouldBe(CustodyStatus.Invalid);
    }
    /// <summary>Verifies closed workflow shape  cannot carry human authority.</summary>
    [Theory]
    [InlineData("Interaction")][InlineData("SystemTimer")][InlineData("GovernanceProtection")]
    [InlineData("InteractionDirectoryMigration")][InlineData("ConversationDeletionPropagation")]
    public async Task ClosedWorkflowShape_CannotCarryHumanAuthority(string kind)
    {
        TrustedPrincipal p = new(TrustedPrincipalKind.Workflow, null, null, null, null, null, kind, "instance", "activity", kind == "Interaction" ? "party-1" : null);
        (await IssueAsync(Identity() with { Principal = p })).Status.ShouldBe(CustodyStatus.Succeeded);
        (await IssueAsync(Identity() with { Principal = p with { HumanActorId = "borrowed-human" } })).Status.ShouldBe(CustodyStatus.Invalid);
        (await IssueAsync(Identity() with { Principal = p with { WorkflowKind = "Unknown" } })).Status.ShouldBe(CustodyStatus.Invalid);
    }
    /// <summary>Verifies timing boundaries  are exclusive and configured.</summary>
    [Fact]
    public async Task TimingBoundaries_AreExclusiveAndConfigured()
    {
        TrustedEnvelope valid = (await IssueAsync()).Envelope!;
        _clock.Now = valid.IssuedAt - _profiles.Profile!.ClockSkew;
        (await _envelopes.VerifyAsync(valid, Identity(), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.Succeeded);
        _clock.Now -= TimeSpan.FromTicks(1);
        (await _envelopes.VerifyAsync(valid, Identity(), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.FutureIssued);
        _clock.Now = valid.ExpiresAt;
        (await _envelopes.VerifyAsync(valid, Identity(), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.Expired);
        (await _envelopes.IssueAsync(Identity(), _profiles.Profile.MaximumLifetime + TimeSpan.FromTicks(1), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.Invalid);
    }
    /// <summary>Verifies missing or invalid profile  never resolves keys.</summary>
    [Theory]
    [InlineData("missing")][InlineData("expired")][InlineData("zero-lifetime")][InlineData("negative-skew")]
    [InlineData("negative-overlap")][InlineData("zero-horizon")][InlineData("short-retention")][InlineData("overflow")]
    public async Task MissingOrInvalidProfile_NeverResolvesKeys(string condition)
    {
        PlatformSigningProfile p = _profiles.Profile!;
        _profiles.Profile = condition switch
        {
            "missing" => null, "expired" => p with { ValidUntil = _clock.Now },
            "zero-lifetime" => p with { MaximumLifetime = TimeSpan.Zero },
            "negative-skew" => p with { ClockSkew = TimeSpan.FromTicks(-1) },
            "negative-overlap" => p with { RotationOverlap = TimeSpan.FromTicks(-1) },
            "zero-horizon" => p with { RecoveryHorizon = TimeSpan.Zero },
            "short-retention" => p with { ReplayRetention = TimeSpan.FromSeconds(1) },
            _ => p with { MaximumLifetime = TimeSpan.MaxValue, ClockSkew = TimeSpan.MaxValue },
        };
        (await IssueAsync()).Status.ShouldBe(CustodyStatus.StaleProfile);
        _keys.Calls.ShouldBe(0);
    }
    /// <summary>Verifies rotation overlap  ends for envelope but not recorded digest.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RotationOverlap_EndsForEnvelopeButNotRecordedDigest(bool observation)
    {
        var scope = observation ? new PlatformHmacScope("system", PlatformHmacPurpose.SecurityObservation)
            : new PlatformHmacScope("tenant-a", PlatformHmacPurpose.ContentDigest);
        TrustedEnvelope valid = (await IssueAsync()).Envelope!;
        PlatformHmacResult original = await _digests.DigestAsync(scope, ["same"], cancellationToken: TestContext.Current.CancellationToken);
        _keys.CurrentVersion = "key-v2";
        _keys.RetiredAt = _clock.Now;
        _clock.Now += _profiles.Profile!.RotationOverlap - TimeSpan.FromTicks(1);
        (await _envelopes.VerifyAsync(valid, Identity(), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.Succeeded);
        _clock.Now += TimeSpan.FromTicks(1);
        (await _envelopes.VerifyAsync(valid, Identity(), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.OutsideKeyWindow);
        PlatformHmacResult retained = await _digests.DigestAsync(scope, ["same"], original.KeyVersion, TestContext.Current.CancellationToken);
        retained.Digest.ShouldBe(original.Digest);
        (await _digests.DigestAsync(scope, ["changed"], original.KeyVersion, TestContext.Current.CancellationToken)).Digest.ShouldNotBe(original.Digest);
        (await _digests.DigestAsync(scope, ["same"], cancellationToken: TestContext.Current.CancellationToken)).Digest.ShouldNotBe(original.Digest);
    }
    /// <summary>Verifies fresh provider rejects scope version revocation and outage.</summary>
    [Theory]
    [InlineData("tenant")][InlineData("purpose")][InlineData("version")][InlineData("revoked")][InlineData("outage")]
    public async Task FreshProviderRejectsScopeVersionRevocationAndOutage(string condition)
    {
        _keys.Hook = (count, scope, version, token) =>
        {
            if (count == 2)
            {
                if (condition == "outage")
                {
                    throw new InvalidOperationException("fixture-sensitive-provider-error");
                }
                if (condition == "revoked")
                {
                    _keys.RevokedVersion = "key-v1";
                }
                return ValueTask.FromResult(_keys.Create(condition == "tenant" ? scope with { TenantId = "tenant-b" }
                    : condition == "purpose" ? scope with { Purpose = PlatformHmacPurpose.ContentDigest } : scope,
                    condition == "version" ? "key-v2" : version));
            }
            return ValueTask.FromResult(_keys.Create(scope, version));
        };
        TrustedEnvelopeResult result = await IssueAsync();
        result.Status.ShouldNotBe(CustodyStatus.Succeeded);
        result.ToString().ShouldNotContain("fixture-sensitive");
        _keys.Snapshots.ShouldAllBe(k => k.IsDisposed);
    }
    /// <summary>Verifies validity expires during final resolution  rejects release.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidityExpiresDuringFinalResolution_RejectsRelease(bool profileExpiry)
    {
        TrustedEnvelope valid = (await IssueAsync()).Envelope!;
        int initialCalls = _keys.Calls;
        _keys.Hook = (count, scope, version, token) =>
        {
            if (count == initialCalls + 2)
            {
                _clock.Now = profileExpiry ? _profiles.Profile!.ValidUntil : valid.ExpiresAt;
            }
            return ValueTask.FromResult(_keys.Create(scope, version));
        };
        (await _envelopes.VerifyAsync(valid, Identity(), TestContext.Current.CancellationToken)).Status.ShouldBe(
            profileExpiry ? CustodyStatus.StaleProfile : CustodyStatus.Expired);
        _keys.Snapshots.ShouldAllBe(k => k.IsDisposed);
    }

    /// <summary>Verifies profile change during resolution  rejects old revision.</summary>
    [Fact]
    public async Task ProfileChangeDuringResolution_RejectsOldRevision()
    {
        _keys.Hook = (_, scope, version, token) =>
        {
            _profiles.Profile = _profiles.Profile! with { Version = "fixture-r2" };
            return ValueTask.FromResult(_keys.Create(scope, version));
        };
        (await IssueAsync()).Status.ShouldBe(CustodyStatus.StaleProfile);
    }
    /// <summary>Verifies caller components mutation while provider suspended  does not change authenticated input.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerComponentsMutationWhileProviderSuspended_DoesNotChangeAuthenticatedInput(bool digest)
    {
        var components = new List<string> { "original" };
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<PlatformHmacKeyResolution>(TaskCreationOptions.RunContinuationsAsynchronously);
        PlatformHmacScope? requested = null;
        _keys.Hook = (count, scope, version, token) =>
        {
            if (count == 1)
            {
                requested = scope;
                entered.TrySetResult();
                return new(pending.Task);
            }
            return ValueTask.FromResult(_keys.Create(scope, version));
        };
        TrustedEnvelopeIdentity identity = Identity() with { IdempotencyTuple = components };
        Task<TrustedEnvelopeResult>? envelopeTask = digest ? null : IssueAsync(identity);
        Task<PlatformHmacResult>? digestTask = digest ? _digests.DigestAsync(new("tenant-a", PlatformHmacPurpose.ContentDigest), components, cancellationToken: TestContext.Current.CancellationToken) : null;
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        components[0] = "substituted";
        pending.SetResult(_keys.Create(requested!, null));
        if (digest)
        {
            PlatformHmacResult result = await digestTask!;
            PlatformHmacResult original = await _digests.DigestAsync(requested!, ["original"], result.KeyVersion, TestContext.Current.CancellationToken);
            result.Digest.ShouldBe(original.Digest);
        }
        else
        {
            TrustedEnvelope result = (await envelopeTask!).Envelope!;
            result.Identity.IdempotencyTuple.Single().ShouldBe("original");
            (await _envelopes.VerifyAsync(result, identity with { IdempotencyTuple = new[] { "original" } }, TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.Succeeded);
            Should.Throw<NotSupportedException>(() => ((IList<string>)result.Identity.IdempotencyTuple)[0] = "changed");
        }
    }
    /// <summary>Verifies cancellation bounds non cooperative provider and disposes late key.</summary>
    [Theory]
    [InlineData(false, false)][InlineData(true, false)][InlineData(false, true)][InlineData(true, true)]
    public async Task CancellationBoundsNonCooperativeProviderAndDisposesLateKey(bool secondResolution, bool digest)
    {
        using var caller = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<PlatformHmacKeyResolution>(TaskCreationOptions.RunContinuationsAsynchronously);
        PlatformHmacScope? requested = null;
        _keys.Hook = (count, scope, version, token) =>
        {
            if (count == (secondResolution ? 2 : 1))
            {
                requested = scope;
                entered.TrySetResult();
                return new(pending.Task);
            }
            return ValueTask.FromResult(_keys.Create(scope, version));
        };
        Task result = digest ? _digests.DigestAsync(new("tenant-a", PlatformHmacPurpose.ContentDigest), ["value"], cancellationToken: caller.Token)
            : _envelopes.IssueAsync(Identity(), TimeSpan.FromMinutes(2), caller.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        await caller.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken));
        PlatformHmacKeyResolution late = _keys.Create(requested!, null);
        pending.SetResult(late);
        // No provider continuation is needed to cancel; allow the late disposal observer one scheduling turn.
        await Task.Yield();
        for (int i = 0; i < 100 && !late.Key!.IsDisposed; i++)
        {
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }
        late.Key!.IsDisposed.ShouldBeTrue();
        _keys.Snapshots.ShouldAllBe(k => k.IsDisposed);
    }
    /// <summary>Verifies pre cancelled and synchronous cancelled provider  do not release success.</summary>
    [Fact]
    public async Task PreCancelledAndSynchronousCancelledProvider_DoNotReleaseSuccess()
    {
        using var caller = new CancellationTokenSource();
        await caller.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => _envelopes.IssueAsync(Identity(), TimeSpan.FromMinutes(2), caller.Token));
        _keys.Calls.ShouldBe(0);
        using var during = new CancellationTokenSource();
        _keys.Hook = (_, scope, version, token) =>
        {
            during.Cancel();
            return ValueTask.FromResult(_keys.Create(scope, version));
        };
        await Should.ThrowAsync<OperationCanceledException>(() => _envelopes.IssueAsync(Identity(), TimeSpan.FromMinutes(2), during.Token));
        _keys.Snapshots.ShouldAllBe(k => k.IsDisposed);
    }
    /// <summary>Verifies default dependency injection  fails closed without production policy.</summary>
    [Fact]
    public async Task DefaultDependencyInjection_FailsClosedWithoutProductionPolicy()
    {
        var services = new ServiceCollection();
        services.AddPlatformCustody();
        using ServiceProvider provider = services.BuildServiceProvider();
        (await provider.GetRequiredService<TrustedEnvelopeAuthenticator>().IssueAsync(Identity(), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.StaleProfile);
        (await provider.GetRequiredService<IPlatformHmacKeyProvider>().ResolveAsync(new("tenant-a", PlatformHmacPurpose.ContentDigest), cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.Unavailable);
    }
    /// <summary>Verifies snapshot copies material and disposal is safe to repeat.</summary>
    [Fact]
    public void SnapshotCopiesMaterialAndDisposalIsSafeToRepeat()
    {
        byte[] material = Enumerable.Repeat((byte)11, 32).ToArray();
        using var snapshot = new PlatformHmacKeySnapshot(new("tenant-a", PlatformHmacPurpose.ContentDigest),
            new("v1", PlatformHmacKeyState.Active, _clock.Now.AddDays(-1), _clock.Now.AddDays(1), null), material);
        Array.Clear(material);
        snapshot.ToString().ShouldBe("PlatformHmacKeySnapshot");
        Should.Throw<InvalidOperationException>(() => System.Text.Json.JsonSerializer.Serialize(snapshot));
        snapshot.Dispose();
        snapshot.Dispose();
        snapshot.IsDisposed.ShouldBeTrue();
    }
}
