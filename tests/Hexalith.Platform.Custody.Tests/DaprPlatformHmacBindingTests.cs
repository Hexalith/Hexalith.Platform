using Dapr.Client;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Platform.Custody.Tests;

/// <summary>Exercises the real Dapr adapter with controlled transport and independent public authority ports.</summary>
public sealed class DaprPlatformHmacBindingTests
{
    private static readonly PlatformHmacScope Scope = new("tenant-a", PlatformHmacPurpose.ContentDigest);
    private readonly DaprClient _client = Substitute.For<DaprClient>();
    private readonly Authority _authority = new();
    private readonly DaprPlatformHmacOptions _options = Options();

    private static DaprPlatformHmacOptions Options() => new("store", "credential-ref", "authority", "profile-ref",
        PlatformAcceptedEnvelopeTiming.Bind("profile-v1", "issuer", "audience", DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow.AddHours(1)), TimeSpan.FromMilliseconds(200));

    /// <summary>Only explicit valid references and independent authority replace the unavailable defaults.</summary>
    [Theory]
    [InlineData("missing-options")][InlineData("missing-authority")][InlineData("missing-reference")][InlineData("timing")]
    public async Task MissingInputs_KeepUnavailableDefaultsAndMakeNoSecretCall(string missing)
    {
        var services = new ServiceCollection();
        DaprPlatformHmacOptions? options = missing == "missing-options" ? null : _options;
        if (missing == "missing-reference") { options = options! with { CredentialReference = "" }; }
        if (missing == "timing") { options = options! with { ExpectedProfile = options.ExpectedProfile with { ClockSkew = TimeSpan.Zero } }; }
        services.AddDaprPlatformHmac(options, missing == "missing-authority" ? null : _authority);
        using ServiceProvider provider = services.BuildServiceProvider();
        (await provider.GetRequiredService<IPlatformHmacKeyProvider>().ResolveAsync(Scope,
            cancellationToken: TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.Unavailable);
        _client.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Exact field, version and opaque reference resolve short-lived owned bytes and clear the Dapr dictionary.</summary>
    [Fact]
    public async Task ExactReference_ResolvesAndClearsResponse()
    {
        _authority.Profile = _options.ExpectedProfile;
        var response = new Dictionary<string, string> { ["key"] = Convert.ToBase64String(new byte[32]) };
        _client.GetSecretAsync("store", "tenant-a/digest/v1", Arg.Is<IReadOnlyDictionary<string, string>>(m => m["version_id"] == "7" && !m.ContainsKey("version")),
            Arg.Any<CancellationToken>()).Returns(response);
        PlatformHmacKeyResolution result = await Provider().ResolveAsync(Scope, "v1", TestContext.Current.CancellationToken);
        result.Status.ShouldBe(CustodyStatus.Succeeded);
        response.ShouldBeEmpty();
        result.Key!.Scope.ShouldBe(Scope);
        result.Key.Metadata.Version.ShouldBe("v1");
        result.Key.Dispose();
        result.Key.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Invalid current authority denies before Dapr, without public diagnostics or fallback.</summary>
    [Theory]
    [InlineData("revoked")][InlineData("tenant")][InlineData("purpose")][InlineData("version")]
    [InlineData("credential")][InlineData("stale")][InlineData("profile")][InlineData("missing")][InlineData("backend-version")]
    public async Task InvalidAuthority_MakesNoSecretCall(string condition)
    {
        _authority.Profile = _options.ExpectedProfile;
        _authority.Transform = (_, p) => condition switch
        {
            "revoked" => p with { Metadata = p.Metadata with { State = PlatformHmacKeyState.Revoked } },
            "tenant" => p with { Scope = Scope with { TenantId = "tenant-b" } },
            "purpose" => p with { Scope = Scope with { Purpose = PlatformHmacPurpose.TrustedEnvelope } },
            "version" => p with { Metadata = p.Metadata with { Version = "v2" } },
            "credential" => p with { CredentialReference = "foreign" },
            "backend-version" => p with { SecretVersion = "v1" },
            "stale" => p with { ValidUntil = DateTimeOffset.UtcNow.AddMinutes(-1) },
            "missing" => null,
            _ => p,
        };
        if (condition == "profile") { _authority.Profile = _options.ExpectedProfile with { Version = "foreign" }; }
        (await Provider().ResolveAsync(Scope, "v1", TestContext.Current.CancellationToken)).Status.ShouldNotBe(CustodyStatus.Succeeded);
        _client.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Current revocation or changed enrollment after Dapr prevents releasing the already retrieved key.</summary>
    [Theory]
    [InlineData("revocation")][InlineData("revision")][InlineData("reference")][InlineData("profile")][InlineData("backend-version")]
    public async Task AuthorityChangedDuringResolution_RejectsRelease(string condition)
    {
        _authority.Profile = _options.ExpectedProfile;
        _authority.Transform = (n, p) => n == 1 ? p : condition switch
        {
            "revocation" => p with { Metadata = p.Metadata with { State = PlatformHmacKeyState.Revoked } },
            "revision" => p with { Revision = "r2" },
            "reference" => p with { SecretReference = "other/v1" },
            "backend-version" => p with { SecretVersion = "8" },
            _ => p,
        };
        var response = new Dictionary<string, string> { ["key"] = Convert.ToBase64String(new byte[32]) };
        _client.GetSecretAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => { if (condition == "profile") { _authority.Profile = null; } return Task.FromResult(response); });
        (await Provider().ResolveAsync(Scope, "v1", TestContext.Current.CancellationToken)).Status.ShouldNotBe(CustodyStatus.Succeeded);
        response.ShouldBeEmpty();
    }

    /// <summary>Extra fields, wrong field, malformed encoding and unexpected size cannot become a key.</summary>
    [Theory]
    [InlineData("extra")][InlineData("field")][InlineData("encoding")][InlineData("size")][InlineData("outage")]
    public async Task UnexpectedSecretResponse_FailsContentFree(string condition)
    {
        _authority.Profile = _options.ExpectedProfile;
        var response = new Dictionary<string, string> { [condition == "field" ? "foreign" : "key"] =
            condition == "encoding" ? new string('!', 44) : Convert.ToBase64String(new byte[condition == "size" ? 64 : 32]) };
        if (condition == "extra") { response["diagnostic"] = "sensitive-marker"; }
        _client.GetSecretAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => condition == "outage" ? Task.FromException<Dictionary<string, string>>(new InvalidOperationException("sensitive-marker")) : Task.FromResult(response));
        PlatformHmacKeyResolution result = await Provider().ResolveAsync(Scope, "v1", TestContext.Current.CancellationToken);
        result.Status.ShouldBe(CustodyStatus.Unavailable);
        result.ToString().ShouldNotContain("sensitive-marker");
        if (condition != "outage") { response.ShouldBeEmpty(); }
    }

    /// <summary>A noncooperative Dapr call is bounded; late secret response is cleared and never released.</summary>
    [Fact]
    public async Task SuspendedDaprCall_IsBoundedAndLateResponseCleared()
    {
        _authority.Profile = _options.ExpectedProfile;
        var pending = new TaskCompletionSource<Dictionary<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _client.GetSecretAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(pending.Task);
        PlatformHmacKeyResolution result = await Provider().ResolveAsync(Scope, "v1", TestContext.Current.CancellationToken);
        result.Status.ShouldBe(CustodyStatus.Unavailable);
        var late = new Dictionary<string, string> { ["key"] = Convert.ToBase64String(new byte[32]) };
        pending.SetResult(late);
        for (int i = 0; i < 100 && late.Count != 0; i++) { await Task.Delay(10, TestContext.Current.CancellationToken); }
        late.ShouldBeEmpty();
        _authority.Calls.ShouldBe(1);
    }

    /// <summary>A current profile never falls back to an earlier healthy result after authority loss.</summary>
    [Fact]
    public void ProfileBinding_DoesNotCacheOrRevive()
    {
        _authority.Profile = _options.ExpectedProfile;
        var profiles = new DaprPlatformSigningProfileProvider(_options, _authority, TimeProvider.System);
        profiles.GetCurrent().ShouldBe(_options.ExpectedProfile);
        _authority.Profile = null;
        profiles.GetCurrent().ShouldBeNull();
    }

    /// <summary>Fresh current observation windows can advance while exact enrolled authority facts remain unchanged.</summary>
    [Fact]
    public async Task RefreshedObservationWindow_ResolvesHealthyUnchangedVersion()
    {
        _authority.Profile = _options.ExpectedProfile;
        _authority.Transform = (n, p) => p with { ObservedAt = DateTimeOffset.UtcNow.AddSeconds(-1), ValidUntil = DateTimeOffset.UtcNow.AddMinutes(n) };
        _client.GetSecretAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new Dictionary<string, string> { ["key"] = Convert.ToBase64String(new byte[32]) }));
        PlatformHmacKeyResolution result = await Provider().ResolveAsync(Scope, "v1", TestContext.Current.CancellationToken);
        result.Status.ShouldBe(CustodyStatus.Succeeded);
        result.Key!.Dispose();
    }

    private DaprPlatformHmacKeyProvider Provider() => new(_client, _options, _authority, TimeProvider.System);

    /// <summary>The registered current-version consumer computes the independent nonzero-key golden digest.</summary>
    [Fact]
    public async Task RegisteredActiveVersion_DigestMatchesGoldenCaller()
    {
        _authority.Profile = _options.ExpectedProfile;
        byte[] nonzero = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();
        _client.GetSecretAsync("store", "tenant-a/digest/v1", Arg.Is<IReadOnlyDictionary<string, string>>(m => m["version_id"] == "7"), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new Dictionary<string, string> { ["key"] = Convert.ToBase64String(nonzero) }));
        var services = new ServiceCollection();
        services.AddSingleton(_client);
        services.AddDaprPlatformHmac(_options, _authority);
        using ServiceProvider registered = services.BuildServiceProvider();
        PlatformHmacResult result = await registered.GetRequiredService<PlatformHmacService>().DigestAsync(Scope, ["golden"], cancellationToken: TestContext.Current.CancellationToken);
        result.Status.ShouldBe(CustodyStatus.Succeeded);
        result.KeyVersion.ShouldBe("v1");
        result.Digest.ShouldBe("d5ceefdd842369d4465962370875d9e97d2758f36f8cf64b50357bdf4919a3d4");
    }

    /// <summary>A suspended post-fetch public read clears the response immediately and cannot retain or release a decoded key.</summary>
    [Fact]
    public async Task SuspendedPostFetchAuthority_ClearsResponseBeforeBoundedWait()
    {
        _authority.Profile = _options.ExpectedProfile;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<DaprPlatformHmacInventory?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _authority.Wait = n =>
        {
            if (n == 2) { entered.SetResult(); return pending.Task; }
            return Task.FromResult<DaprPlatformHmacInventory?>(new("authority", "r1", Scope,
                new("v1", PlatformHmacKeyState.Active, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), null),
                "store", "credential-ref", "tenant-a/digest/v1", "7", "key", DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow.AddHours(1)));
        };
        var response = new Dictionary<string, string> { ["key"] = Convert.ToBase64String(Enumerable.Repeat((byte)17, 32).ToArray()) };
        _client.GetSecretAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(response);
        Task<PlatformHmacKeyResolution> result = Provider().ResolveAsync(Scope, "v1", TestContext.Current.CancellationToken).AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        response.ShouldBeEmpty();
        PlatformHmacKeyResolution denied = await result.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        denied.Status.ShouldBe(CustodyStatus.Unavailable);
        denied.Key.ShouldBeNull();
        pending.SetResult(null);
    }

    private sealed class Authority : IDaprPlatformHmacAuthority
    {
        private readonly DaprPlatformHmacInventory _proof = new("authority", "r1", Scope,
            new("v1", PlatformHmacKeyState.Active, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1), null),
            "store", "credential-ref", "tenant-a/digest/v1", "7", "key", DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        public PlatformSigningProfile? Profile { get; set; }
        public int Calls { get; private set; }
        public Func<int, DaprPlatformHmacInventory, DaprPlatformHmacInventory?>? Transform { get; set; }
        public Func<int, Task<DaprPlatformHmacInventory?>>? Wait { get; set; }
        public PlatformSigningProfile? GetCurrentProfile(string profileReference) => Profile;
        public ValueTask<DaprPlatformHmacInventory?> ResolveAsync(string authorityReference, PlatformHmacScope scope, string? version, CancellationToken cancellationToken)
        {
            Calls++;
            if (Wait is not null) { return new(Wait(Calls)); }
            return ValueTask.FromResult(Transform is null ? _proof : Transform(Calls, _proof));
        }
    }
}
