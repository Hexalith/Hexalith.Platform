using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

namespace Hexalith.Platform.Secrets.Authority.Tests;

internal sealed class AuthorityTestState : IDisposable
{
    public const string AppId = "independent-hmac-authority";
    public const string Caller = "independent-inventory-caller";
    public const string Administrator = "independent-owner-administrator";
    public const string Issuer = "https://offline-authority.example/realms/authority";
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "hmac-authority-test-" + Guid.NewGuid().ToString("N"));
    public TestClock Clock { get; } = new();
    public AuthorityRepository Repository { get; }
    public AuthorityEvaluator Evaluator { get; }
    public AuthorityProfile Profile => new(AppId, "public-trust", "owner-profile-v1", Issuer, "custody-envelopes", Clock.Now.AddHours(-1), Clock.Now.AddHours(1), false);
    public AuthorityCaller Enrollment => new(Caller, Clock.Now.AddHours(-1), Clock.Now.AddHours(1), true);
    public AuthorityInventoryHost Host => new("inventory", Caller, "Deployment/ext-secrets-1-isolated/inventory@actual-offline-uid", Clock.Now.AddHours(-1), Clock.Now.AddHours(1), true);
    public AuthorityKeyStatus Key => new("tenant-a", "TrustedEnvelope", "opaque-version-v1", "owner-profile-v1", Clock.Now.AddHours(-1), Clock.Now.AddHours(1), false);
    public AuthorityRevocationQuery Query => new(AppId, "public-trust", AppId, "inventory", "owner-profile-v1", Issuer, "custody-envelopes", "tenant-a", "TrustedEnvelope", "opaque-version-v1");
    public SecretsRuntimeConfiguration Runtime => new()
    {
        ProfileReference = AppId, CurrentRevocationReference = AppId, PublicTrustReference = "public-trust", CurrentInventoryAuthorityReference = "inventory",
        Issuer = Issuer, Audience = "custody-envelopes", ProfileVersion = "owner-profile-v1", ProfileNotBefore = Profile.NotBefore, ProfileValidUntil = Profile.ValidUntil,
        ExpectedProfile = PlatformAcceptedEnvelopeTiming.Bind("owner-profile-v1", Issuer, "custody-envelopes", Profile.NotBefore, Profile.ValidUntil), CallTimeout = TimeSpan.FromSeconds(30),
    };

    public AuthorityTestState()
    { Repository = new AuthorityRepository(DirectoryPath); Evaluator = new AuthorityEvaluator(AppId, Repository, Clock); }

    public async Task<AuthorityReceipt> Apply(string kind, AuthorityProfile? profile = null, AuthorityCaller? caller = null, AuthorityInventoryHost? host = null, AuthorityKeyStatus? key = null)
    {
        AuthorityState state = await Repository.ReadAsync(TestContext.Current.CancellationToken);
        return await Repository.AdministerAsync(Administrator, new(Guid.NewGuid().ToString("N"), state.Revision, kind, profile, caller, host, key), Clock.Now, TestContext.Current.CancellationToken);
    }

    public async Task Seed()
    {
        await Apply("PublishProfile", profile: Profile);
        await Apply("SetCaller", caller: Enrollment);
        await Apply("SetInventoryHost", host: Host);
        await Apply("SetKeyStatus", key: Key);
    }
    public void Dispose() { Repository.Dispose(); if (Directory.Exists(DirectoryPath)) { Directory.Delete(DirectoryPath, recursive: true); } }
}

internal sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}
