using System.Security.Claims;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Actual private candidate credential canonical HMAC/current machine boundary with synthetic independent enrollment/custody; no installed ACL or live identity proof.</summary>
public sealed class PrivateOwnerOperationAuthenticatorTests
{
    private static PrivateOwnerOperationScope Scope() => new("system", "replay/issuer/nonce", "RegisterTrustedEnvelopeFirstSeen", "TrustedEnvelopeFirstSeenV1", new string('A', 64), "digest-v1", "tenant-a");
    private static ClaimsPrincipal Caller(string subject = "dedicated-machine", string client = "private-verifier", string audience = "private-owner")
        => new(new ClaimsIdentity(new[] { new Claim("iss", "machine-issuer"), new Claim("sub", subject), new Claim("azp", client), new Claim("aud", audience) }, "independently-authenticated-jwt"));
    private static PrivateOwnerOperationGrant Grant(CustodyFixtureClock clock) => new("machine-issuer", "dedicated-machine", "private-verifier", "private-owner", Scope(),
        "independent-enrollment/acl", 4, true, clock.Now.AddDays(-1), clock.Now.AddDays(1));

    /// <summary>Actual custody tag permits only the authenticated dedicated exact machine and immutable owner operation; it is a separate candidate format.</summary>
    [Fact]
    public async Task ExactPrivateCredentialBindsAuthenticatedMachineAndOperation()
    {
        var clock = new CustodyFixtureClock(); var profiles = new CustodyFixtureProfileProvider(clock); var keys = new CustodyFixtureKeyProvider(clock);
        var grants = Substitute.For<IPrivateOwnerOperationGrantSource>(); grants.ResolveCurrentAsync("machine-issuer", "dedicated-machine", "private-verifier", Scope(), Arg.Any<CancellationToken>()).Returns(Grant(clock));
        var authorizer = new PrivateOwnerOperationAuthenticator(keys, profiles, clock, grants);
        var credential = (await authorizer.IssueAsync(Caller(), Scope(), TestContext.Current.CancellationToken))!;
        credential.ShouldNotBeNull(); (await authorizer.AuthorizeAsync(Caller(), Scope(), credential, TestContext.Current.CancellationToken)).ShouldBeTrue();
        var second = (await authorizer.IssueAsync(Caller(), Scope(), TestContext.Current.CancellationToken))!; second.DeliveryNonce.ShouldNotBe(credential.DeliveryNonce);
        keys.Snapshots.ShouldAllBe(key => key.IsDisposed);
    }
    /// <summary>Wrong service/subject/audience, public unauthenticated caller, human classification, or operation substitution cannot release owner evidence.</summary>
    [Theory]
    [InlineData("subject")][InlineData("client")][InlineData("audience")][InlineData("unauthenticated")][InlineData("duplicate-sub")][InlineData("mixed-identities")]
    [InlineData("human")][InlineData("tenant")][InlineData("target-tenant")][InlineData("resource")][InlineData("method")][InlineData("contract")][InlineData("payload")]
    public async Task WrongCallerOrExactRequestAlwaysDenies(string vector)
    {
        var clock = new CustodyFixtureClock(); var profiles = new CustodyFixtureProfileProvider(clock); var keys = new CustodyFixtureKeyProvider(clock);
        var grants = Substitute.For<IPrivateOwnerOperationGrantSource>();
        grants.ResolveCurrentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PrivateOwnerOperationScope>(), Arg.Any<CancellationToken>()).Returns(Grant(clock));
        var authorizer = new PrivateOwnerOperationAuthenticator(keys, profiles, clock, grants); var credential = (await authorizer.IssueAsync(Caller(), Scope(), TestContext.Current.CancellationToken))!;
        var caller = vector switch { "subject" => Caller("human-or-other-service"), "client" => Caller(client: "workflow-or-public"), "audience" => Caller(audience: "public-api"),
            "unauthenticated" => new ClaimsPrincipal(new ClaimsIdentity()), _ => Caller() };
        if (vector == "mixed-identities") { caller = new ClaimsPrincipal(new[] { new ClaimsIdentity(authenticationType: "authenticated-unrelated-machine"), new ClaimsIdentity(Caller().Claims) }); }
        if (vector == "duplicate-sub") { ((ClaimsIdentity)caller.Identity!).AddClaim(new("sub", "other")); }
        if (vector == "human") { grants.ResolveCurrentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PrivateOwnerOperationScope>(), Arg.Any<CancellationToken>()).Returns(Grant(clock) with { IsDedicatedServiceAccount = false }); }
        var scope = vector switch { "tenant" => Scope() with { TenantId = "tenant-a" }, "target-tenant" => Scope() with { AuthenticatedTargetTenantId = "tenant-b" },
            "resource" => Scope() with { ResourceId = "target/general-command" }, "method" => Scope() with { Method = "ExecuteTargetCommand" },
            "contract" => Scope() with { Contract = "GeneralCommandV1" }, "payload" => Scope() with { PayloadFingerprint = new string('B', 64) }, _ => Scope() };
        (await authorizer.AuthorizeAsync(caller, scope, credential, TestContext.Current.CancellationToken)).ShouldBeFalse(); keys.Snapshots.ShouldAllBe(key => key.IsDisposed);
    }
    /// <summary>Changed tag/header/profile or current enrollment/key revocation and exclusive expiry deny; no credential manufactures private grant authority.</summary>
    [Theory]
    [InlineData("tag")][InlineData("profile")][InlineData("issuer")][InlineData("nonce")][InlineData("binding")][InlineData("revocation")][InlineData("expiry")]
    public async Task ForgedOrStaleCredentialCannotAuthorize(string vector)
    {
        var clock = new CustodyFixtureClock(); var profiles = new CustodyFixtureProfileProvider(clock); var keys = new CustodyFixtureKeyProvider(clock);
        var grants = Substitute.For<IPrivateOwnerOperationGrantSource>(); grants.ResolveCurrentAsync("machine-issuer", "dedicated-machine", "private-verifier", Scope(), Arg.Any<CancellationToken>()).Returns(Grant(clock));
        var authorizer = new PrivateOwnerOperationAuthenticator(keys, profiles, clock, grants); var credential = (await authorizer.IssueAsync(Caller(), Scope(), TestContext.Current.CancellationToken))!;
        credential = vector switch { "tag" => credential with { Tag = new string('0', 64) }, "profile" => credential with { ProfileVersion = "changed" },
            "issuer" => credential with { Issuer = "self-issued" }, "nonce" => credential with { DeliveryNonce = new string('0', 64) }, "binding" => credential with { BindingRevision = 5 }, _ => credential };
        if (vector == "revocation") { keys.RevokedVersion = credential.SigningKeyVersion; }
        if (vector == "expiry") { clock.Now = credential.ExclusiveExpiry; }
        (await authorizer.AuthorizeAsync(Caller(), Scope(), credential, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }
    /// <summary>Missing configuration/current grant denies before key resolution; withdrawal during final grant read withholds an otherwise valid tag.</summary>
    [Fact]
    public async Task MissingOrWithdrawnCurrentGrantFailsClosed()
    {
        var clock = new CustodyFixtureClock(); var profiles = new CustodyFixtureProfileProvider(clock); var keys = new CustodyFixtureKeyProvider(clock);
        (await new PrivateOwnerOperationAuthenticator(keys, profiles, clock).IssueAsync(Caller(), Scope(), TestContext.Current.CancellationToken)).ShouldBeNull(); keys.Calls.ShouldBe(0);
        var grants = Substitute.For<IPrivateOwnerOperationGrantSource>(); int reads = 0;
        grants.ResolveCurrentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PrivateOwnerOperationScope>(), Arg.Any<CancellationToken>()).Returns(_ => ++reads == 1 ? Grant(clock) : null);
        (await new PrivateOwnerOperationAuthenticator(keys, profiles, clock, grants).IssueAsync(Caller(), Scope(), TestContext.Current.CancellationToken)).ShouldBeNull(); keys.Snapshots.ShouldAllBe(key => key.IsDisposed);
    }
    /// <summary>Cancellation after private provider entry completes independently and disposes the actual late owned key.</summary>
    [Fact]
    public async Task CancellationDuringPrivateKeyReadPreservesOriginalTokenAndClearsLateKey()
    {
        var clock = new CustodyFixtureClock(); var profiles = new CustodyFixtureProfileProvider(clock); var keys = new CustodyFixtureKeyProvider(clock);
        var grants = Substitute.For<IPrivateOwnerOperationGrantSource>(); grants.ResolveCurrentAsync("machine-issuer", "dedicated-machine", "private-verifier", Scope(), Arg.Any<CancellationToken>()).Returns(Grant(clock));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var pending = new TaskCompletionSource<PlatformHmacKeyResolution>(TaskCreationOptions.RunContinuationsAsynchronously);
        keys.Hook = (_, _, _, _) => { entered.TrySetResult(); return new(pending.Task); }; using var cancellation = new CancellationTokenSource();
        var read = new PrivateOwnerOperationAuthenticator(keys, profiles, clock, grants).IssueAsync(Caller(), Scope(), cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); cancellation.Cancel();
        var exception = await Should.ThrowAsync<OperationCanceledException>(() => read.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)); exception.CancellationToken.ShouldBe(cancellation.Token);
        var late = keys.Create(new("system", PlatformHmacPurpose.TrustedEnvelope), null); pending.SetResult(late);
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(5)); while (!late.Key!.IsDisposed) { await Task.Delay(10, watchdog.Token); }
        late.Key.IsDisposed.ShouldBeTrue();
    }
    /// <summary>Both synchronous profile observations stay within the original caller/budget and cannot release a late credential.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SuspendedProfileReadIsBounded(bool finalRead, bool deadlineExpires)
    {
        var (clock, advance) = PrivateOwnerDeadlineTestClock.Create();
        var fixture = new CustodyFixtureClock { Now = clock.GetUtcNow() };
        var keys = new CustodyFixtureKeyProvider(fixture);
        var profile = new CustodyFixtureProfileProvider(fixture).Profile;
        var profiles = Substitute.For<IPlatformSigningProfileProvider>();
        var grants = Substitute.For<IPrivateOwnerOperationGrantSource>();
        grants.ResolveCurrentAsync("machine-issuer", "dedicated-machine", "private-verifier", Scope(), Arg.Any<CancellationToken>()).Returns(Grant(fixture));
        using var release = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        profiles.GetCurrent().Returns(_ =>
        {
            if (Interlocked.Increment(ref reads) != (finalRead ? 2 : 1)) { return profile; }
            entered.SetResult(); release.Wait(); finished.SetResult(); return profile;
        });
        var authenticator = new PrivateOwnerOperationAuthenticator(keys, profiles, clock, grants);
        var operation = Task.Run(() => authenticator.IssueAsync(Caller(), Scope(), cancellation.Token), TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        try
        {
            if (deadlineExpires)
            {
                advance(TimeSpan.FromSeconds(30));
                (await operation.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken)).ShouldBeNull();
            }
            else
            {
                cancellation.Cancel();
                var error = await Should.ThrowAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
                error.CancellationToken.ShouldBe(cancellation.Token);
            }
            keys.Snapshots.ShouldAllBe(key => key.IsDisposed);
            int calls = keys.Calls;
            release.Set(); await finished.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            keys.Calls.ShouldBe(calls);
            if (!finalRead) { calls.ShouldBe(0); }
        }
        finally { release.Set(); }
    }

    /// <summary>A final completed profile cannot release credentials after elapsed time exhausts the original budget.</summary>
    [Fact]
    public async Task FinalProfileCompletingAfterBudgetCannotReleaseCredential()
    {
        var (clock, advance) = PrivateOwnerDeadlineTestClock.Create();
        var fixture = new CustodyFixtureClock { Now = clock.GetUtcNow() };
        var keys = new CustodyFixtureKeyProvider(fixture);
        var profile = new CustodyFixtureProfileProvider(fixture).Profile;
        var profiles = Substitute.For<IPlatformSigningProfileProvider>(); int reads = 0;
        profiles.GetCurrent().Returns(_ => { if (++reads == 2) { advance(TimeSpan.FromSeconds(30)); } return profile; });
        var grants = Substitute.For<IPrivateOwnerOperationGrantSource>();
        grants.ResolveCurrentAsync("machine-issuer", "dedicated-machine", "private-verifier", Scope(), Arg.Any<CancellationToken>()).Returns(Grant(fixture));
        (await new PrivateOwnerOperationAuthenticator(keys, profiles, clock, grants).IssueAsync(Caller(), Scope(), TestContext.Current.CancellationToken)).ShouldBeNull();
        keys.Snapshots.ShouldAllBe(key => key.IsDisposed);
    }

    /// <summary>Every caller identity carrier rejects strict malformed UTF-8 or more than 2048 encoded bytes before grant or key invocation.</summary>
    [Theory]
    [InlineData("tenant", false)][InlineData("tenant", true)]
    [InlineData("resource", false)][InlineData("resource", true)]
    [InlineData("method", false)][InlineData("method", true)]
    [InlineData("contract", false)][InlineData("contract", true)]
    [InlineData("digest", false)][InlineData("digest", true)]
    [InlineData("target", false)][InlineData("target", true)]
    [InlineData("iss", false)][InlineData("iss", true)]
    [InlineData("sub", false)][InlineData("sub", true)]
    [InlineData("azp", false)][InlineData("azp", true)]
    [InlineData("aud", false)][InlineData("aud", true)]
    public async Task InvalidIdentityCarrierDeniesBeforeGrantAndKey(string field, bool malformed)
    {
        string text = malformed ? "\uD800" : new string('é', 1025); var scope = Scope(); var caller = Caller();
        scope = field switch { "tenant" => scope with { TenantId = text }, "resource" => scope with { ResourceId = text }, "method" => scope with { Method = text },
            "contract" => scope with { Contract = text }, "digest" => scope with { DigestKeyVersion = text }, "target" => scope with { AuthenticatedTargetTenantId = text }, _ => scope };
        if (field is "iss" or "sub" or "azp" or "aud")
        { var identity = (ClaimsIdentity)caller.Identity!; identity.RemoveClaim(identity.FindFirst(field)!); identity.AddClaim(new(field, text)); }
        var clock = new CustodyFixtureClock(); var keys = new CustodyFixtureKeyProvider(clock); var grants = Substitute.For<IPrivateOwnerOperationGrantSource>();
        grants.ResolveCurrentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PrivateOwnerOperationScope>(), Arg.Any<CancellationToken>())
            .Returns(call => Grant(clock) with { Scope = call.Arg<PrivateOwnerOperationScope>(), MachineIssuer = caller.FindFirst("iss")!.Value,
                MachineSubject = caller.FindFirst("sub")!.Value, MachineClient = caller.FindFirst("azp")!.Value, MachineAudience = caller.FindFirst("aud")!.Value });
        (await new PrivateOwnerOperationAuthenticator(keys, new CustodyFixtureProfileProvider(clock), clock, grants).IssueAsync(caller, scope, TestContext.Current.CancellationToken)).ShouldBeNull();
        grants.ReceivedCalls().ShouldBeEmpty(); keys.Calls.ShouldBe(0);
    }

    /// <summary>Returned profile/grant/key identifiers are validated before canonical credential construction, and every acquired key is retired.</summary>
    [Theory]
    [InlineData("profile", false)][InlineData("profile", true)][InlineData("issuer", false)][InlineData("issuer", true)]
    [InlineData("audience", false)][InlineData("audience", true)][InlineData("grant", false)][InlineData("grant", true)][InlineData("key", false)][InlineData("key", true)]
    public async Task InvalidReturnedIdentityCannotReleaseCredential(string field, bool malformed)
    {
        string text = malformed ? "\uD800" : new string('é', 1025); var clock = new CustodyFixtureClock(); var profiles = new CustodyFixtureProfileProvider(clock);
        profiles.Profile = field switch { "profile" => profiles.Profile! with { Version = text }, "issuer" => profiles.Profile! with { Issuer = text }, "audience" => profiles.Profile! with { Audience = text }, _ => profiles.Profile };
        var grants = Substitute.For<IPrivateOwnerOperationGrantSource>(); grants.ResolveCurrentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<PrivateOwnerOperationScope>(), Arg.Any<CancellationToken>())
            .Returns(Grant(clock) with { AuthorityReference = field == "grant" ? text : Grant(clock).AuthorityReference });
        var keys = Substitute.For<IPlatformHmacKeyProvider>(); var snapshots = new List<PlatformHmacKeySnapshot>();
        keys.ResolveAsync(Arg.Any<PlatformHmacScope>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var snapshot = new PlatformHmacKeySnapshot(call.Arg<PlatformHmacScope>(), new(field == "key" ? text : "key-v1", PlatformHmacKeyState.Active, clock.Now.AddDays(-1), clock.Now.AddDays(1), null), new byte[32]);
            snapshots.Add(snapshot); return ValueTask.FromResult(new PlatformHmacKeyResolution(CustodyStatus.Succeeded, snapshot));
        });
        (await new PrivateOwnerOperationAuthenticator(keys, profiles, clock, grants).IssueAsync(Caller(), Scope(), TestContext.Current.CancellationToken)).ShouldBeNull();
        if (field != "key") { keys.ReceivedCalls().ShouldBeEmpty(); }
        snapshots.ShouldAllBe(key => key.IsDisposed);
    }

    /// <summary>Exactly 2048 strict UTF-8 bytes remain usable across scope, machine, profile, independent grant and signing-key carriers.</summary>
    [Fact]
    public async Task ValidMaximumIdentityCarrierStillIssuesAndAuthorizes()
    {
        string text = new('é', 1024); var clock = new CustodyFixtureClock(); var profiles = new CustodyFixtureProfileProvider(clock);
        profiles.Profile = profiles.Profile! with { Version = text, Issuer = text, Audience = text };
        var scope = new PrivateOwnerOperationScope(text, text, text, text, new string('A', 64), text, text);
        var caller = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("iss", text), new Claim("sub", text), new Claim("azp", text), new Claim("aud", text) }, "independent-machine"));
        var grants = Substitute.For<IPrivateOwnerOperationGrantSource>(); grants.ResolveCurrentAsync(text, text, text, scope, Arg.Any<CancellationToken>())
            .Returns(new PrivateOwnerOperationGrant(text, text, text, text, scope, text, 1, true, clock.Now.AddDays(-1), clock.Now.AddDays(1)));
        var keys = Substitute.For<IPlatformHmacKeyProvider>(); var snapshots = new List<PlatformHmacKeySnapshot>();
        keys.ResolveAsync(Arg.Any<PlatformHmacScope>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(call =>
        { var key = new PlatformHmacKeySnapshot(call.Arg<PlatformHmacScope>(), new(text, PlatformHmacKeyState.Active, clock.Now.AddDays(-1), clock.Now.AddDays(1), null), new byte[32]); snapshots.Add(key); return ValueTask.FromResult(new PlatformHmacKeyResolution(CustodyStatus.Succeeded, key)); });
        var authenticator = new PrivateOwnerOperationAuthenticator(keys, profiles, clock, grants);
        var credential = (await authenticator.IssueAsync(caller, scope, TestContext.Current.CancellationToken))!; credential.ShouldNotBeNull();
        (await authenticator.AuthorizeAsync(caller, scope, credential, TestContext.Current.CancellationToken)).ShouldBeTrue(); snapshots.ShouldAllBe(key => key.IsDisposed);
    }

}
