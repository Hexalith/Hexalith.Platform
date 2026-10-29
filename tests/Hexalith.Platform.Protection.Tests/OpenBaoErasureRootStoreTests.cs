using System.Net;
using System.Text.Json;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Native HTTP shape, strict CAS classification and missing-input component checks.</summary>
public sealed class OpenBaoErasureRootStoreTests
{
    private readonly ErasureRootInstallation _installation = new("erc", "enrollment", "transit", "heads", new Dictionary<string, string> { ["tenant"] = "epoch" });
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MissingCredentialRefusesBeforeAnyTransportEffect()
    {
        string reference = "ERC_MISSING_TEST_" + Guid.NewGuid().ToString("N");
        using var handler = new ErasureRootTransportHandler((_, _) => throw new InvalidOperationException());
        using var http = new HttpClient(handler) { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
        var store = new OpenBaoErasureRootStore(http, _installation, "env:" + reference);
        await Should.ThrowAsync<InvalidOperationException>(() => store.ReadHeadAsync("tenant", Token));
        handler.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task TransitUsesAuthenticatedContextExplicitEpochAndOrderedBatchWithoutAssociatedData()
    {
        string variable = "ERC_PROTOCOL_TEST_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "component-test-credential");
        try
        {
            using var handler = new ErasureRootTransportHandler((request, body) =>
            {
                request.Headers.GetValues("X-Vault-Token").Single().ShouldBe("component-test-credential");
                using var json = JsonDocument.Parse(body!);
                json.RootElement.TryGetProperty("associated_data", out _).ShouldBeFalse();
                json.RootElement.GetProperty("key_version").GetInt32().ShouldBe(2);
                if (request.RequestUri!.AbsolutePath.EndsWith("/rewrap/epoch", StringComparison.Ordinal))
                {
                    var batch = json.RootElement.GetProperty("batch_input");
                    batch.GetArrayLength().ShouldBe(2);
                    batch[0].GetProperty("context").GetString().ShouldBe("YQ==");
                    batch[1].GetProperty("context").GetString().ShouldBe("Yg==");
                    return ErasureRootTransportHandler.Json("{\"data\":{\"batch_results\":[{\"ciphertext\":\"vault:v2:a\"},{\"ciphertext\":\"vault:v2:b\"}]}}");
                }
                json.RootElement.GetProperty("plaintext").GetBytesFromBase64().Length.ShouldBe(32);
                json.RootElement.GetProperty("context").GetString().ShouldBe("YQ==");
                return ErasureRootTransportHandler.Json("{\"data\":{\"ciphertext\":\"vault:v2:a\"}}");
            });
            using var http = new HttpClient(handler) { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
            var store = new OpenBaoErasureRootStore(http, _installation, "env:" + variable);
            (await store.EncryptAsync("tenant", 2, new byte[32], "YQ==", Token)).ShouldBe("vault:v2:a");
            (await store.RewrapAsync("tenant", 2, [new("vault:v1:a", "YQ=="), new("vault:v1:b", "Yg==")], Token))
                .ShouldBe(["vault:v2:a", "vault:v2:b"]);
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Theory]
    [InlineData("check-and-set parameter did not match the current version", true)]
    [InlineData("permission denied", false)]
    public async Task OnlyExactNativeCasConflictIsRetryable(string error, bool conflict)
    {
        string variable = "ERC_CAS_TEST_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "component-test-credential");
        try
        {
            using var handler = new ErasureRootTransportHandler((_, _) => ErasureRootTransportHandler.Json(JsonSerializer.Serialize(new { errors = new[] { error } }), HttpStatusCode.BadRequest));
            using var http = new HttpClient(handler) { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
            var store = new OpenBaoErasureRootStore(http, _installation, "env:" + variable);
            if (conflict) { (await store.CompareExchangeHeadAsync("tenant", 1, "{}"u8.ToArray(), Token)).ShouldBeFalse(); }
            else { await Should.ThrowAsync<InvalidOperationException>(() => store.CompareExchangeHeadAsync("tenant", 1, "{}"u8.ToArray(), Token)); }
            handler.Calls.ShouldBe(1);
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task TrimVerificationAllowsUnusedNewerVersionsButRequiresExactIrreversibleFloor(int latest)
    {
        string variable = "ERC_TRIM_TEST_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "component-test-credential");
        try
        {
            using var handler = new ErasureRootTransportHandler((request, _) =>
            {
                if (request.RequestUri!.AbsolutePath == "/v1/sys/health")
                { return ErasureRootTransportHandler.Json("{\"version\":\"2.6.2\",\"initialized\":true,\"sealed\":false}"); }
                return ErasureRootTransportHandler.Json(JsonSerializer.Serialize(new { data = new {
                    type = "aes256-gcm96", derived = true, exportable = false, allow_plaintext_backup = false, deletion_allowed = false,
                    latest_version = latest, min_available_version = 2, min_decryption_version = 2, min_encryption_version = 2,
                    keys = latest == 2 ? new Dictionary<string, int> { ["2"] = 123 } : new Dictionary<string, int> { ["2"] = 123, ["3"] = 456 } } }));
            });
            using var http = new HttpClient(handler) { BaseAddress = new("https://erc.invalid/"), Timeout = TimeSpan.FromSeconds(5) };
            var store = new OpenBaoErasureRootStore(http, _installation, "env:" + variable);
            (await store.VerifyTrimAsync("tenant", 2, Token)).ShouldBeTrue();
            (await store.VerifyTrimAsync("tenant", 3, Token)).ShouldBeFalse();
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Theory]
    [InlineData("http://erc.invalid/")]
    [InlineData("https://erc.invalid/not-a-root/")]
    public void UnsafeTransportEnrollmentCannotBeConstructed(string endpoint)
    {
        using var http = new HttpClient { BaseAddress = new(endpoint), Timeout = TimeSpan.FromSeconds(5) };
        Should.Throw<ArgumentException>(() => new OpenBaoErasureRootStore(http, _installation, "env:ERC_TEST"));
    }
}
