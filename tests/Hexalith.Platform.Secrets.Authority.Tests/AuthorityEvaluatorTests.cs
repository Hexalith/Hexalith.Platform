using System.Text.Json.Nodes;
using Shouldly;

namespace Hexalith.Platform.Secrets.Authority.Tests;

public sealed class AuthorityEvaluatorTests
{
    [Fact]
    public async Task CoherentCurrentProfileAndExactVersionHaveStableRevisionAndBoundedProof()
    {
        using var fixture = new AuthorityTestState();
        await fixture.Seed();
        JsonObject profile = (await fixture.Evaluator.ProfileAsync(AuthorityTestState.Caller, AuthorityTestState.AppId, TestContext.Current.CancellationToken))!;
        profile.Count.ShouldBe(5);
        profile["version"]!.GetValue<string>().ShouldBe(fixture.Profile.Version);
        JsonObject first = (await fixture.Evaluator.RevocationAsync(AuthorityTestState.Caller, fixture.Query, TestContext.Current.CancellationToken))!;
        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(1);
        JsonObject second = (await fixture.Evaluator.RevocationAsync(AuthorityTestState.Caller, fixture.Query, TestContext.Current.CancellationToken))!;
        second["revision"]!.GetValue<string>().ShouldBe(first["revision"]!.GetValue<string>());
        (second["validUntil"]!.GetValue<DateTimeOffset>() - second["observedAt"]!.GetValue<DateTimeOffset>()).ShouldBe(TimeSpan.FromSeconds(30));
        second["revoked"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Theory]
    [InlineData("missing-profile")]
    [InlineData("revoked-profile")]
    [InlineData("expired-profile")]
    [InlineData("future-profile")]
    [InlineData("disabled-caller")]
    [InlineData("expired-caller")]
    [InlineData("unenrolled-host")]
    [InlineData("expired-host")]
    [InlineData("foreign-host")]
    [InlineData("revoked-version")]
    [InlineData("expired-version")]
    [InlineData("future-version")]
    [InlineData("stale-profile-version")]
    public async Task IncompleteRevokedExpiredForeignAndUnenrolledRecordsDeny(string scenario)
    {
        using var fixture = new AuthorityTestState();
        if (scenario != "missing-profile") { await fixture.Seed(); }
        switch (scenario)
        {
            case "revoked-profile": await fixture.Apply("PublishProfile", profile: fixture.Profile with { Revoked = true }); break;
            case "expired-profile": await fixture.Apply("PublishProfile", profile: fixture.Profile with { ValidUntil = fixture.Clock.Now }); break;
            case "future-profile": await fixture.Apply("PublishProfile", profile: fixture.Profile with { NotBefore = fixture.Clock.Now.AddMinutes(1) }); break;
            case "disabled-caller": await fixture.Apply("SetCaller", caller: fixture.Enrollment with { Enabled = false }); break;
            case "expired-caller": await fixture.Apply("SetCaller", caller: fixture.Enrollment with { ValidUntil = fixture.Clock.Now }); break;
            case "unenrolled-host": await fixture.Apply("SetInventoryHost", host: fixture.Host with { Enrolled = false }); break;
            case "expired-host": await fixture.Apply("SetInventoryHost", host: fixture.Host with { ValidUntil = fixture.Clock.Now }); break;
            case "foreign-host": await fixture.Apply("SetInventoryHost", host: fixture.Host with { CallerId = "foreign" }); break;
            case "revoked-version": await fixture.Apply("SetKeyStatus", key: fixture.Key with { Revoked = true }); break;
            case "expired-version": await fixture.Apply("SetKeyStatus", key: fixture.Key with { ValidUntil = fixture.Clock.Now }); break;
            case "future-version": await fixture.Apply("SetKeyStatus", key: fixture.Key with { NotBefore = fixture.Clock.Now.AddMinutes(1) }); break;
            case "stale-profile-version": await fixture.Apply("SetKeyStatus", key: fixture.Key with { ProfileVersion = "old-profile" }); break;
        }
        (await fixture.Evaluator.RevocationAsync(AuthorityTestState.Caller, fixture.Query, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Theory]
    [InlineData("authority")]
    [InlineData("trust")]
    [InlineData("profile")]
    [InlineData("inventory")]
    [InlineData("profile-version")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("tenant")]
    [InlineData("purpose")]
    [InlineData("version")]
    [InlineData("partial-scope")]
    [InlineData("foreign-caller")]
    [InlineData("recursive-inventory")]
    public async Task EveryExactQueryBindingMustMatch(string mismatch)
    {
        using var fixture = new AuthorityTestState();
        await fixture.Seed();
        AuthorityRevocationQuery query = mismatch switch
        {
            "authority" => fixture.Query with { AuthorityReference = "foreign" }, "trust" => fixture.Query with { PublicTrustReference = "foreign" },
            "profile" => fixture.Query with { ProfileReference = "foreign" }, "inventory" => fixture.Query with { InventoryAuthorityReference = "foreign" },
            "profile-version" => fixture.Query with { ProfileVersion = "foreign" }, "issuer" => fixture.Query with { Issuer = "foreign" },
            "audience" => fixture.Query with { Audience = "foreign" }, "tenant" => fixture.Query with { TenantId = "tenant-b" },
            "purpose" => fixture.Query with { Purpose = "ContentDigest" }, "version" => fixture.Query with { Version = "unknown" },
            "partial-scope" => fixture.Query with { Version = null }, "recursive-inventory" => fixture.Query with { InventoryAuthorityReference = AuthorityTestState.AppId }, _ => fixture.Query,
        };
        (await fixture.Evaluator.RevocationAsync(mismatch == "foreign-caller" ? "foreign" : AuthorityTestState.Caller, query, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task ExplicitHostAllowsProfileOnlyTrustButUnknownVersionRemainsDeniedAndTtlClampsToEarliestBoundary()
    {
        using var fixture = new AuthorityTestState();
        await fixture.Seed();
        var profileQuery = fixture.Query with { TenantId = null, Purpose = null, Version = null };
        (await fixture.Evaluator.RevocationAsync(AuthorityTestState.Caller, profileQuery, TestContext.Current.CancellationToken)).ShouldNotBeNull();
        await fixture.Apply("SetKeyStatus", key: fixture.Key with { ValidUntil = fixture.Clock.Now.AddSeconds(5) });
        var proof = await fixture.Evaluator.RevocationAsync(AuthorityTestState.Caller, fixture.Query, TestContext.Current.CancellationToken);
        proof!["validUntil"]!.GetValue<DateTimeOffset>().ShouldBe(fixture.Clock.Now.AddSeconds(5));
        await fixture.Apply("SetInventoryHost", host: fixture.Host with { ValidUntil = fixture.Clock.Now.AddSeconds(2) });
        proof = await fixture.Evaluator.RevocationAsync(AuthorityTestState.Caller, fixture.Query, TestContext.Current.CancellationToken);
        proof!["validUntil"]!.GetValue<DateTimeOffset>().ShouldBe(fixture.Clock.Now.AddSeconds(2));
    }

    [Fact]
    public async Task ProfileQueriesUseOnlyCurrentPersistedStateAndEnrolledCallers()
    {
        using var fixture = new AuthorityTestState();
        await fixture.Seed();
        (await fixture.Evaluator.ProfileAsync("foreign", AuthorityTestState.AppId, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await fixture.Evaluator.ProfileAsync(AuthorityTestState.Caller, "inventory", TestContext.Current.CancellationToken)).ShouldBeNull();
        await fixture.Apply("PublishProfile", profile: fixture.Profile with { Revoked = true });
        (await fixture.Evaluator.ProfileAsync(AuthorityTestState.Caller, AuthorityTestState.AppId, TestContext.Current.CancellationToken)).ShouldBeNull();
    }
}
