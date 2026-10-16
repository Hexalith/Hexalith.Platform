using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using Hexalith.Platform.Secrets.Runtime;

using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationHostTests
{
    [Theory]
    [InlineData("executor", "execute")]
    [InlineData("observer", "observe")]
    [InlineData("enrollment", "enroll")]
    public async Task ReferencesWithoutInstalledProvidersStorageAndSignersDenyWithZeroEffects(string role, string operation)
    {
        QualificationHostResult result = await QualificationHost.HandleAsync(role, operation, Configuration(), Request(), null, null, null, TestContext.Current.CancellationToken);
        result.StatusCode.ShouldBe(503);
        result.Body["qualificationEffects"]!.GetValue<int>().ShouldBe(0);
        result.Body.ToJsonString().ShouldContain("installedProviderSignerAndScopeAuthority");
        result.Body.ToJsonString().ShouldContain("independentDurableOriginalStore");
        result.Body.ToJsonString().ShouldContain("acceptedPublicProofVerifier");
    }

    [Fact]
    public async Task LostResultRetainsOriginalAndNewHostOnlyLooksUpItWithoutExpectedOutcomes()
    {
        var provider = new TestProvider();
        var store = new TestStore();
        var verifier = new TestVerifier();
        provider.Lose = true;
        QualificationHostResult first = await QualificationHost.HandleAsync("executor", "execute", Configuration(), Request(), provider, store, verifier, TestContext.Current.CancellationToken);
        first.StatusCode.ShouldBe(503);
        first.Body["qualificationEffects"]!.GetValue<string>().ShouldBe("Unknown");
        store.Original.ShouldNotBeNull();
        provider.Lose = false;
        // A newly composed dispatcher receives the same external durable store after restart or restore.
        QualificationHostResult recovered = await QualificationHost.HandleAsync("executor", "execute", Configuration(), Request(), provider, store, verifier, TestContext.Current.CancellationToken);
        recovered.StatusCode.ShouldBe(200);
        provider.Operations.ShouldBe(["execute", "lookup"]);
        provider.Requests.ShouldAllBe(request => !request.ContainsKey("scenario"));
        store.Original.ContainsKey("scenario").ShouldBeTrue();
        store.Result.ShouldNotBeNull();
        store.Result.ContainsKey("observedAt").ShouldBeFalse();
    }

    [Fact]
    public async Task DeniedEnrollmentAndChangedOriginalCannotReachOwnerEffect()
    {
        var provider = new TestProvider { Allowed = false };
        var store = new TestStore();
        var verifier = new TestVerifier();
        (await QualificationHost.HandleAsync("executor", "execute", Configuration(), Request(), provider, store, verifier, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(503);
        provider.Operations.ShouldBeEmpty();
        store.Original.ShouldBeNull();
        provider.Allowed = true;
        await QualificationHost.HandleAsync("executor", "execute", Configuration(), Request(), provider, store, verifier, TestContext.Current.CancellationToken);
        JsonObject changed = Request();
        changed["caseId"] = "other";
        (await QualificationHost.HandleAsync("executor", "execute", Configuration(), changed, provider, store, verifier, TestContext.Current.CancellationToken)).StatusCode.ShouldBe(503);
        provider.Operations.ShouldBe(["execute"]);
    }

    [Fact]
    public void PublicVerifierRejectsForgedSubstitutedExpiredAndChangedAnchorProofs()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "public.pem");
        File.WriteAllText(path, key.ExportSubjectPublicKeyInfoPem());
        try
        {
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            var verifier = new QualificationProofVerifier(new Dictionary<string, QualificationProofBinding> { ["executor"] = new("executor", "v1", path, hash) }, new FixedClock(Fixtures.Now));
            JsonObject request = Request();
            var payload = (JsonObject)request.DeepClone();
            payload.Remove("scenario");
            payload["identity"] = "executor";
            payload["version"] = "v1";
            payload["observedAt"] = Fixtures.Now.ToUnixTimeSeconds();
            payload["validUntil"] = Fixtures.Now.AddSeconds(20).ToUnixTimeSeconds();
            // Match the Python canonical ASCII protocol used by the installed public proof signer.
            var sorted = new JsonObject();
            foreach (var field in payload.OrderBy(field => field.Key, StringComparer.Ordinal)) { sorted[field.Key] = field.Value?.DeepClone(); }
            byte[] signature = key.SignData(Encoding.UTF8.GetBytes(sorted.ToJsonString()), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            var envelope = new JsonObject { ["payload"] = payload, ["signature"] = Convert.ToBase64String(signature) };
            verifier.Verify("executor", request, envelope).ShouldBeTrue();
            payload["caseId"] = "substitution";
            verifier.Verify("executor", request, envelope).ShouldBeFalse();
            payload["caseId"] = request["caseId"]!.GetValue<string>();
            payload["validUntil"] = Fixtures.Now.ToUnixTimeSeconds();
            verifier.Verify("executor", request, envelope).ShouldBeFalse();
            payload["validUntil"] = Fixtures.Now.AddSeconds(20).ToUnixTimeSeconds();
            File.AppendAllText(path, "\n");
            verifier.Verify("executor", request, envelope).ShouldBeFalse();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static SecretsRuntimeConfiguration Configuration() => Fixtures.Ready() with { AcceptedReplacementCommit = new string('a', 40) };

    private static JsonObject Request() => new()
    {
        ["protocol"] = "EXT-SECRETS-1.qualification.v1", ["requestId"] = new string('b', 64), ["runId"] = "run",
        ["targetCommit"] = new string('a', 40), ["isolatedTargetId"] = "isolated", ["planSha256"] = new string('c', 64),
        ["caseId"] = "case", ["caseTargetId"] = new string('d', 64), ["scenario"] = new JsonObject { ["expected"] = new JsonObject { ["success"] = 1 } },
    };

    private sealed class TestProvider : IQualificationExecutorProvider
    {
        public string Role => "executor";
        public bool Allowed { get; set; } = true;
        public bool Lose { get; set; }
        public List<string> Operations { get; } = [];
        public List<JsonObject> Requests { get; } = [];
        public Task<bool> AuthorizeAsync(JsonObject request, CancellationToken cancellationToken) => Task.FromResult(Allowed);
        public Task<JsonObject?> InvokeAsync(string operation, JsonObject original, CancellationToken cancellationToken)
        {
            Operations.Add(operation);
            Requests.Add(original);
            if (Lose) { throw new IOException("lost result"); }
            return Task.FromResult<JsonObject?>(new JsonObject { ["payload"] = new JsonObject { ["outcome"] = "Denied", ["observedAt"] = 1, ["validUntil"] = 2 }, ["signature"] = "offline-test" });
        }
    }

    private sealed class TestVerifier : IQualificationPublicProofVerifier
    {
        public bool Verify(string role, JsonObject request, JsonObject envelope) => true;
    }

    private sealed class TestStore : IQualificationOriginalStore
    {
        public JsonObject? Original { get; private set; }
        public JsonObject? Result { get; private set; }
        public Task<bool> BeginAsync(string role, string requestId, JsonObject original, CancellationToken cancellationToken)
        {
            if (Original is not null && !JsonNode.DeepEquals(Original, original)) { throw new IOException("identity conflict"); }
            bool first = Original is null;
            Original ??= original;
            return Task.FromResult(first);
        }
        public Task RetainAsync(string role, string requestId, JsonObject result, CancellationToken cancellationToken) { Result = result; return Task.CompletedTask; }
    }
}
