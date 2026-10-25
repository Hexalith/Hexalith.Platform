using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Security.Claims;

using Microsoft.AspNetCore.Http;

using Hexalith.Platform.Secrets.Runtime;

using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class QualificationHostTests
{
    [Fact]
    public async Task RequiredInputsRejectNullWhileMalformedPublicJsonReturnsNoConfiguration()
    {
        Should.Throw<ArgumentNullException>(() => SecretsRuntimeConfiguration.Parse(null!)).ParamName.ShouldBe("json");
        SecretsRuntimeConfiguration.Parse("{").ShouldBeNull();
        (await Should.ThrowAsync<ArgumentNullException>(() => QualificationHost.ReadRequestAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("stream");
        (await Should.ThrowAsync<ArgumentNullException>(() => QualificationHttpHost.HandleAsync("executor", "execute", null!, Context("role-channel", true),
            "role-channel", null, null, null, null, TestContext.Current.CancellationToken))).ParamName.ShouldBe("configuration");
        (await Should.ThrowAsync<ArgumentNullException>(() => QualificationHttpHost.HandleAsync("executor", "execute", Configuration(), null!,
            "role-channel", null, null, null, null, TestContext.Current.CancellationToken))).ParamName.ShouldBe("context");
    }

    [Theory]
    [InlineData("executor", "execute")]
    [InlineData("observer", "observe")]
    [InlineData("enrollment", "enroll")]
    public async Task ReferencesWithoutInstalledProvidersStorageAndSignersDenyWithZeroEffects(string role, string operation)
    {
        QualificationHostResult result = await QualificationHost.HandleAsync(role, operation, Configuration(), Request(), null, null, null, TestContext.Current.CancellationToken, Caller());
        result.StatusCode.ShouldBe(503);
        result.Body["qualificationEffects"]!.GetValue<int>().ShouldBe(0);
        result.Body.ToJsonString().ShouldContain("installedProviderSignerAndScopeAuthority");
        result.Body.ToJsonString().ShouldContain("independentDurableOriginalStore");
        result.Body.ToJsonString().ShouldContain("acceptedPublicProofVerifier");
    }

    [Theory]
    [InlineData("provider", "qualification.executor.installedProviderSignerAndScopeAuthority")]
    [InlineData("store", "qualification.executor.independentDurableOriginalStore")]
    [InlineData("verifier", "qualification.executor.acceptedPublicProofVerifier")]
    public async Task EachMissingDependencyDeniesBeforeAuthorizationOrOriginalRetention(string missing, string expected)
    {
        var provider = new TestProvider();
        var store = new TestStore();
        QualificationHostResult result = await QualificationHost.HandleAsync("executor", "execute", Configuration(), Request(),
            missing == "provider" ? null : provider, missing == "store" ? null : store,
            missing == "verifier" ? null : new TestVerifier(), TestContext.Current.CancellationToken, Caller());
        result.StatusCode.ShouldBe(503);
        result.Body["missingInputs"]!.AsArray().Select(value => value!.GetValue<string>()).ShouldBe([expected]);
        result.Body["qualificationEffects"]!.GetValue<int>().ShouldBe(0);
        result.Body["productionMutationAllowed"]!.GetValue<bool>().ShouldBeFalse();
        provider.AuthorizedRequests.ShouldBeEmpty();
        provider.Operations.ShouldBeEmpty();
        store.Original.ShouldBeNull();
        store.Result.ShouldBeNull();
    }

    [Fact]
    public async Task LostResultRetainsOriginalAndNewHostOnlyLooksUpItWithoutExpectedOutcomes()
    {
        var provider = new TestProvider();
        var store = new TestStore();
        var verifier = new TestVerifier();
        provider.Lose = true;
        QualificationHostResult first = await QualificationHost.HandleAsync("executor", "execute", Configuration(), Request(), provider, store, verifier, TestContext.Current.CancellationToken, Caller());
        first.StatusCode.ShouldBe(503);
        first.Body["qualificationEffects"]!.GetValue<string>().ShouldBe("Unknown");
        store.Original.ShouldNotBeNull();
        provider.Lose = false;
        // A newly composed dispatcher receives the same external durable store after restart or restore.
        QualificationHostResult recovered = await QualificationHost.HandleAsync("executor", "execute", Configuration(), Request(), provider, store, verifier, TestContext.Current.CancellationToken, Caller());
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
        (await QualificationHost.HandleAsync("executor", "execute", Configuration(), Request(), provider, store, verifier, TestContext.Current.CancellationToken, Caller())).StatusCode.ShouldBe(503);
        provider.Operations.ShouldBeEmpty();
        store.Original.ShouldBeNull();
        provider.Allowed = true;
        await QualificationHost.HandleAsync("executor", "execute", Configuration(), Request(), provider, store, verifier, TestContext.Current.CancellationToken, Caller());
        JsonObject changed = Request();
        changed["caseId"] = "other";
        (await QualificationHost.HandleAsync("executor", "execute", Configuration(), changed, provider, store, verifier, TestContext.Current.CancellationToken, Caller())).StatusCode.ShouldBe(503);
        provider.Operations.ShouldBe(["execute"]);
    }

    [Theory]
    [InlineData("execute")]
    [InlineData("lookup")]
    public async Task FailedRecoveryRemainsUnknownAndRequiresLookup(string retry)
    {
        var provider = new TestProvider { Lose = true };
        var store = new TestStore();
        await QualificationHost.HandleAsync("executor", "execute", Configuration(), Request(), provider, store, new TestVerifier(), TestContext.Current.CancellationToken, Caller());
        QualificationHostResult result = await QualificationHost.HandleAsync("executor", retry, Configuration(), Request(), provider, store, new TestVerifier(), TestContext.Current.CancellationToken, Caller());
        result.Body["qualificationEffects"]!.GetValue<string>().ShouldBe("Unknown");
        result.Body["originalLookupRequired"]!.GetValue<bool>().ShouldBeTrue();
        provider.Operations.ShouldBe(["execute", "lookup"]);
        provider.AuthorizedRequests.ShouldAllBe(r => !r.ContainsKey("scenario"));
        store.Original!.ContainsKey("scenario").ShouldBeTrue();
    }

    [Theory]
    [InlineData("missing-outcome")]
    [InlineData("missing-digest")]
    [InlineData("extra")]
    [InlineData("typed")]
    [InlineData("substituted")]
    [InlineData("expired")]
    [InlineData("untrusted")]
    public async Task ConcreteVerifierRefusesIndependentlySignedInvalidProofBeforeRetention(string defect)
    {
        using var accepted = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var foreign = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var provider = new TestProvider
        {
            Response = original =>
            {
                JsonObject payload = Payload(original);
                switch (defect)
                {
                    case "missing-outcome": payload.Remove("outcome"); break;
                    case "missing-digest": payload.Remove("resultSha256"); break;
                    case "extra": payload["unrecognized"] = "field"; break;
                    case "typed": payload["outcome"] = 7; break;
                    case "substituted": payload["caseTargetId"] = new string('f', 64); break;
                    case "expired": payload["validUntil"] = Fixtures.Now.ToUnixTimeSeconds(); break;
                }
                return Sign(payload, defect == "untrusted" ? foreign : accepted);
            },
        };
        var store = new TestStore();
        QualificationHostResult result = await QualificationHost.HandleAsync("executor", "execute", Configuration(), Request(), provider, store, Verifier(accepted), TestContext.Current.CancellationToken, Caller());
        result.StatusCode.ShouldBe(503);
        result.Body["qualificationEffects"]!.GetValue<string>().ShouldBe("Unknown");
        store.Original.ShouldNotBeNull();
        store.Result.ShouldBeNull();
    }

    [Fact]
    public void PublicAnchorsAreBoundedImmutableMemoryAndVerificationHonorsCancellation()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] bytes = Encoding.ASCII.GetBytes(key.ExportSubjectPublicKeyInfoPem());
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var binding = new QualificationProofBinding("executor", "v1", bytes, hash);
        Array.Fill(bytes, (byte)0);
        var verifier = new QualificationProofVerifier(new Dictionary<string, QualificationProofBinding> { ["executor"] = binding }, new FixedClock(Fixtures.Now));
        JsonObject envelope = Sign(Payload(Request()), key);
        verifier.Verify("executor", Request(), envelope, TestContext.Current.CancellationToken).ShouldBeTrue();
        verifier.Verify("executor", Request(), null!, TestContext.Current.CancellationToken).ShouldBeFalse();
        Should.Throw<ArgumentException>(() => new QualificationProofBinding("executor", "v1", new byte[4097], hash));
        // File paths (including FIFO names) cannot be opened by this API: only validated PEM bytes are accepted.
        Should.Throw<ArgumentException>(() => new QualificationProofBinding("executor", "v1", Encoding.ASCII.GetBytes("/tmp/untrusted-public-anchor.fifo"), hash));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        verifier.Verify("executor", Request(), envelope, cancellation.Token).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("wrong", true)]
    [InlineData("role-channel", false)]
    public async Task DirectUnsignedOrUnscopedHttpRequestsCannotCallAnyProvider(string? token, bool scoped)
    {
        var provider = new TestProvider();
        var store = new TestStore();
        var authenticator = new CallerAuthenticator();
        DefaultHttpContext context = Context(token, scoped);
        QualificationHostResult result = await QualificationHttpHost.HandleAsync("executor", "execute", Configuration(), context, "role-channel", authenticator, provider, store, new TestVerifier(), TestContext.Current.CancellationToken);
        result.StatusCode.ShouldBe(503);
        result.Body["qualificationEffects"]!.GetValue<int>().ShouldBe(0);
        provider.AuthorizedRequests.ShouldBeEmpty();
        provider.Operations.ShouldBeEmpty();
        store.Original.ShouldBeNull();
    }

    [Fact]
    public async Task AuthenticatedChannelAndIndependentlyAuthenticatedScopeReachRealVerification()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var provider = new TestProvider { Response = original => Sign(Payload(original), key) };
        var store = new TestStore();
        QualificationHostResult result = await QualificationHttpHost.HandleAsync("executor", "execute", Configuration(), Context("role-channel", true), "role-channel", new CallerAuthenticator(), provider, store, Verifier(key), TestContext.Current.CancellationToken);
        result.StatusCode.ShouldBe(200);
        provider.Caller.ShouldBe(Caller());
        provider.AuthorizedRequests.ShouldHaveSingleItem().ContainsKey("scenario").ShouldBeFalse();
        provider.Requests.ShouldHaveSingleItem().ContainsKey("scenario").ShouldBeFalse();
        store.Result.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("executor")]
    [InlineData("observer")]
    [InlineData("enrollment")]
    public void EveryRoleRejectsMissingExtraAndIncorrectlyTypedSignedPublicFields(string role)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        JsonObject payload = RolePayload(role);
        QualificationProofVerifier verifier = Verifier(key, role);
        verifier.Verify(role, Request(role), Sign((JsonObject)payload.DeepClone(), key), TestContext.Current.CancellationToken).ShouldBeTrue();
        foreach (string field in payload.Select(item => item.Key))
        {
            var missing = (JsonObject)payload.DeepClone();
            missing.Remove(field);
            verifier.Verify(role, Request(role), Sign(missing, key), TestContext.Current.CancellationToken).ShouldBeFalse(field);
            var malformed = (JsonObject)payload.DeepClone();
            malformed[field] = new JsonArray("invalid-field-type");
            verifier.Verify(role, Request(role), Sign(malformed, key), TestContext.Current.CancellationToken).ShouldBeFalse(field);
        }
        payload["unexpected"] = "field";
        verifier.Verify(role, Request(role), Sign(payload, key), TestContext.Current.CancellationToken).ShouldBeFalse();
    }

    [Fact]
    public void ObserverRejectsSignedMalformedNestedObservationsRatherThanUsingClientExpectations()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        QualificationProofVerifier verifier = Verifier(key, "observer");
        foreach (string field in new[] { "metrics", "snapshot", "state", "timing", "trace", "receipts" })
        {
            JsonObject payload = RolePayload("observer");
            JsonObject target = field switch
            {
                "metrics" => (JsonObject)payload["metrics"]!,
                "snapshot" => (JsonObject)payload["snapshots"]![0]!,
                "state" => (JsonObject)payload["snapshots"]![0]!["state"]!,
                "timing" => (JsonObject)payload["snapshots"]![0]!["state"]!["timing"]!,
                "trace" => (JsonObject)payload["trace"]![0]!,
                _ => (JsonObject)payload["retainedReceipts"]![0]!,
            };
            target["unexpected"] = true;
            verifier.Verify("observer", Request(), Sign(payload, key), TestContext.Current.CancellationToken).ShouldBeFalse(field);
        }
    }

    private static JsonObject RolePayload(string role)
    {
        JsonObject payload = Payload(Request(role));
        payload["identity"] = role;
        if (role == "executor") { return payload; }
        if (role == "enrollment")
        {
            foreach (string field in new[] { "caseId", "caseTargetId", "outcome", "resultSha256" }) { payload.Remove(field); }
            payload["targetClass"] = "IsolatedQualification"; payload["productionMutationAllowed"] = false;
            payload["executorIdentity"] = "executor"; payload["executorVersion"] = "v1";
            payload["observerIdentity"] = "observer"; payload["observerVersion"] = "v1";
            payload["qualifiedReferencesSha256"] = new string('e', 64); payload["currentProfileSha256"] = new string('f', 64); payload["currentProfileStatus"] = "Current";
            using Stream stream = typeof(QualificationProofVerifier).Assembly.GetManifestResourceStream("ExtSecretsPublicCaseSchemas")!;
            var cases = (JsonArray)JsonNode.Parse(stream)!["cases"]!;
            var ids = new JsonArray(); var targets = new JsonObject(); var scopes = new JsonObject();
            int position = 1;
            foreach (JsonNode? item in cases)
            {
                string id = item!["id"]!.GetValue<string>(); string hash = position++.ToString("x64", System.Globalization.CultureInfo.InvariantCulture);
                ids.Add(id); targets[id] = hash;
                scopes[id] = new JsonObject
                {
                    ["tenantId"] = "tenant", ["targetIds"] = new JsonArray(hash), ["providerCopyIds"] = new JsonArray(), ["logicalId"] = hash,
                    ["sealId"] = "seal", ["batchId"] = "batch", ["manifestDigest"] = new string('e', 64), ["initialAttestationOrdinal"] = 1,
                    ["initialKeyVersion"] = "v1", ["successorKeyVersion"] = "v2", ["exclusiveExpiry"] = 1, ["issuer"] = "issuer", ["nonceDigest"] = new string('f', 64),
                };
            }
            payload["completeCaseIds"] = ids; payload["caseTargetIds"] = targets; payload["caseScopes"] = scopes;
            return payload;
        }
        var metrics = new JsonObject();
        foreach (string field in new[] { "leakCount", "foreignEffects", "allCopyRevival", "immutableIdentity", "immutableOutcome", "persistedProofCount", "forbiddenEffects" }) { metrics[field] = 0; }
        payload["metrics"] = metrics;
        payload["linearization"] = new JsonObject();
        payload["proofReferences"] = new JsonArray(Enumerable.Range(1, 4).Select(i => (JsonNode?)JsonValue.Create(i.ToString("x64", System.Globalization.CultureInfo.InvariantCulture))).ToArray());
        var state = new JsonObject();
        foreach (string field in new[] { "sealId", "batchId", "manifestDigest", "attestationOrdinal", "keyVersion", "deliveredAt", "exclusiveExpiry", "replayNamespace", "replayTenant", "nonceDigest", "firstSeenAt", "retainUntil", "actualIssuedGuardRevision", "replayIssuer", "rotation" }) { state[field] = null; }
        foreach (string field in new[] { "targetIds", "destroyedTargetIds", "pinnedTargetIds", "activeCredentialIds", "providerCopyIds", "erasedCopyIds", "attempts", "attestationHistory" }) { state[field] = new JsonArray(); }
        state["requestId"] = payload["requestId"]!.DeepClone(); state["outcome"] = "Denied"; state["status"] = "Denied"; state["receiptId"] = payload["resultSha256"]!.DeepClone();
        state["tenantId"] = "tenant"; state["logicalId"] = new string('d', 64); state["targetIds"] = new JsonArray(new string('a', 64)); state["journalRevision"] = 1;
        var timing = new JsonObject(); foreach (string field in new[] { "checkedAt", "exclusiveExpiry", "releasedAt", "authorityChangedAt", "rotatedAt", "overlapEndsAt" }) { timing[field] = null; }
        state["timing"] = timing;
        var snapshots = new JsonArray();
        foreach (string stage in new[] { "AfterEffect", "AfterRestart", "AfterRestore", "AfterLookup" })
        {
            snapshots.Add(new JsonObject { ["stage"] = stage, ["originalRequestId"] = payload["requestId"]!.DeepClone(), ["originalResultSha256"] = payload["resultSha256"]!.DeepClone(), ["stateSha256"] = Convert.ToHexString(SHA256.HashData(Canonical(state))).ToLowerInvariant(), ["authorityRevision"] = "1", ["state"] = state.DeepClone() });
        }
        payload["snapshots"] = snapshots;
        var trace = new JsonArray(); var receipts = new JsonArray();
        for (int i = 0; i < 4; i++)
        {
            var receipt = new JsonObject { ["kind"] = "Lookup", ["position"] = i, ["receiptId"] = new string('e', 64), ["keyVersion"] = null, ["guardRevision"] = 1, ["blockSetRevision"] = 1, ["occurredAt"] = 1 };
            receipts.Add(receipt.DeepClone());
            receipt["requestId"] = payload["requestId"]!.DeepClone(); receipt["tenantId"] = "tenant"; receipt["batchId"] = null;
            receipt["originalReceiptId"] = new string('e', 64); receipt["stateSha256"] = snapshots[0]!["stateSha256"]!.DeepClone(); trace.Add(receipt);
        }
        payload["trace"] = trace; payload["retainedReceipts"] = receipts;
        return payload;
    }

    [Fact]
    public async Task HttpDeadlineAfterAppliedExecutionRetainsUnknownAndLookupRequirement()
    {
        var provider = new TestProvider { WaitAfterApply = true };
        var store = new TestStore();
        QualificationHostResult result = await QualificationHttpHost.HandleAsync("executor", "execute", Configuration() with { CallTimeout = TimeSpan.FromMilliseconds(100) },
            Context("role-channel", true), "role-channel", new CallerAuthenticator(), provider, store, new TestVerifier(), TestContext.Current.CancellationToken);
        provider.Operations.ShouldBe(["execute"]);
        provider.Applied.ShouldBeTrue();
        result.Body["qualificationEffects"]!.GetValue<string>().ShouldBe("Unknown");
        result.Body["originalLookupRequired"]!.GetValue<bool>().ShouldBeTrue();
        store.Original.ShouldNotBeNull(); store.Result.ShouldBeNull();
    }

    [Theory]
    [InlineData("lookup")]
    [InlineData("execute")]
    public async Task AuthorizationRefusalCannotClearAnUncertainOriginal(string retry)
    {
        var provider = new TestProvider { Lose = true };
        var store = new TestStore();
        await QualificationHost.HandleAsync("executor", "execute", Configuration(), Request(), provider, store, new TestVerifier(), TestContext.Current.CancellationToken, Caller());
        string original = store.Original!.ToJsonString();
        provider.Allowed = false;
        QualificationHostResult result = await QualificationHost.HandleAsync("executor", retry, Configuration(), Request(), provider, store, new TestVerifier(), TestContext.Current.CancellationToken, Caller());
        result.Body["qualificationEffects"]!.GetValue<string>().ShouldBe("Unknown");
        result.Body["originalLookupRequired"]!.GetValue<bool>().ShouldBeTrue();
        provider.Operations.ShouldBe(["execute"]);
        store.Original.ToJsonString().ShouldBe(original); store.Result.ShouldBeNull();
    }

    [Theory]
    [InlineData("missing-case")]
    [InlineData("missing-case-target")]
    [InlineData("missing-scope")]
    [InlineData("arbitrary-plan")]
    [InlineData("unknown-root")]
    [InlineData("unknown-scenario-root")]
    [InlineData("unknown-scope-root")]
    [InlineData("case-target-scope")]
    [InlineData("wrong-request-digest")]
    [InlineData("enrollment-extra")]
    [InlineData("enrollment-operation")]
    public async Task MalformedActualProtocolRequestsCannotReachAnyProviderCallback(string defect)
    {
        ArgumentNullException.ThrowIfNull(defect);
        string role = defect.StartsWith("enrollment", StringComparison.Ordinal) ? "enrollment" : "executor";
        var provider = new TestProvider { RoleName = role };
        var store = new TestStore();
        JsonObject request = Request(role);
        switch (defect)
        {
            case "missing-case": request.Remove("caseId"); break;
            case "missing-case-target": request.Remove("caseTargetId"); break;
            case "missing-scope": request.Remove("enrolledScope"); break;
            case "arbitrary-plan": request["planSha256"] = "arbitrary"; break;
            case "unknown-root": request["expectedOutcome"] = "invented"; break;
            case "unknown-scenario-root": request["scenario"]!["unrecognized"] = 1; break;
            case "unknown-scope-root": request["enrolledScope"]!["unrecognized"] = 1; break;
            case "case-target-scope": request["caseTargetId"] = new string('e', 64); break;
            case "enrollment-extra": request["caseId"] = "S1.reject.missing-config"; break;
            case "enrollment-operation": request["operation"] = "execute"; break;
        }
        BindRequestId(request);
        if (defect == "wrong-request-digest") { request["requestId"] = new string('f', 64); }
        QualificationHostResult result = await QualificationHost.HandleAsync(role, role == "enrollment" ? "enroll" : "execute", Configuration(), request, provider, store, new TestVerifier(), TestContext.Current.CancellationToken, Caller());
        result.StatusCode.ShouldBe(503); provider.AuthorizedRequests.ShouldBeEmpty(); provider.Operations.ShouldBeEmpty(); store.Original.ShouldBeNull();
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("request")]
    [InlineData("outcome")]
    [InlineData("status")]
    [InlineData("receipt")]
    [InlineData("logical")]
    [InlineData("tenant")]
    [InlineData("identity-alphabet")]
    [InlineData("receipt-kind")]
    [InlineData("integer-range")]
    public void IndependentlySignedObserverContradictionsFailBeforeRetention(string defect)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        QualificationProofVerifier verifier = Verifier(key, "observer");
        JsonObject payload = RolePayload("observer");
        foreach (JsonObject snapshot in ((JsonArray)payload["snapshots"]!).OfType<JsonObject>())
        {
            var state = (JsonObject)snapshot["state"]!;
            switch (defect)
            {
                case "hash": state["journalRevision"] = 2; break;
                case "request": state["requestId"] = new string('f', 64); break;
                case "outcome": state["outcome"] = "Unknown"; break;
                case "status": state["status"] = "Unknown"; break;
                case "receipt": state["receiptId"] = new string('f', 64); break;
                case "logical": state["logicalId"] = new string('f', 64); break;
                case "tenant": state["tenantId"] = "foreign-tenant"; break;
                case "identity-alphabet": snapshot["authorityRevision"] = "invalid$identity"; break;
            }
            if (defect != "hash") { snapshot["stateSha256"] = Convert.ToHexString(SHA256.HashData(Canonical(state))).ToLowerInvariant(); }
        }
        if (defect == "receipt-kind") { payload["trace"]![0]!["kind"] = "Invented"; payload["retainedReceipts"]![0]!["kind"] = "Invented"; }
        if (defect == "integer-range") { payload["metrics"]!["leakCount"] = 9007199254740992L; }
        foreach (JsonObject item in ((JsonArray)payload["trace"]!).OfType<JsonObject>()) { item["stateSha256"] = payload["snapshots"]![0]!["stateSha256"]!.DeepClone(); }
        verifier.Verify("observer", Request(), Sign(payload, key), TestContext.Current.CancellationToken).ShouldBeFalse();
    }

    private static QualificationCallerContext Caller() => new("independent-client", "isolated-scope");

    private static DefaultHttpContext Context(string? token, bool scoped)
    {
        var context = new DefaultHttpContext();
        if (token is not null) { context.Request.Headers["dapr-api-token"] = token; }
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(Request().ToJsonString()));
        if (scoped) { context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "independent-client"), new Claim("qualification_scope", "isolated-scope")], "independently-validated-test-workload")); }
        return context;
    }

    private sealed class CallerAuthenticator : IQualificationCallerAuthenticator
    {
        public Task<QualificationCallerContext?> AuthenticateAsync(HttpContext context, string role, CancellationToken cancellationToken)
            => Task.FromResult(context.User.Identity?.IsAuthenticated == true && context.User.FindFirst("qualification_scope") is { } scope
                ? new QualificationCallerContext(context.User.FindFirst("sub")!.Value, scope.Value) : null);
    }

    private static QualificationProofVerifier Verifier(ECDsa key, string role = "executor")
    {
        byte[] bytes = Encoding.ASCII.GetBytes(key.ExportSubjectPublicKeyInfoPem());
        return new QualificationProofVerifier(new Dictionary<string, QualificationProofBinding> { [role] = new(role, "v1", bytes, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()) }, new FixedClock(Fixtures.Now));
    }

    private static JsonObject Payload(JsonObject request)
    {
        var payload = new JsonObject();
        foreach (string field in new[] { "protocol", "runId", "targetCommit", "isolatedTargetId", "planSha256", "requestId", "caseId", "caseTargetId" })
        { if (request[field] is not null) { payload[field] = request[field]!.DeepClone(); } }
        payload["identity"] = "executor";
        payload["version"] = "v1";
        payload["observedAt"] = Fixtures.Now.ToUnixTimeSeconds();
        payload["validUntil"] = Fixtures.Now.AddSeconds(20).ToUnixTimeSeconds();
        payload["outcome"] = "Denied";
        payload["resultSha256"] = new string('e', 64);
        return payload;
    }

    private static byte[] Canonical(JsonNode value)
    {
        JsonNode Sort(JsonNode node) => node is JsonObject obj
            ? new JsonObject(obj.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new KeyValuePair<string, JsonNode?>(p.Key, p.Value is null ? null : Sort(p.Value))))
            : node is JsonArray array ? new JsonArray(array.Select(p => p is null ? null : Sort(p)).ToArray()) : node.DeepClone();
        return Encoding.UTF8.GetBytes(Sort(value).ToJsonString());
    }

    private static JsonObject Sign(JsonObject payload, ECDsa key)
    {
        byte[] signature = key.SignData(Canonical(payload), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new JsonObject { ["payload"] = payload, ["signature"] = Convert.ToBase64String(signature) };
    }

    internal static SecretsRuntimeConfiguration Configuration() => Fixtures.Ready() with { AcceptedReplacementCommit = new string('a', 40) };

    internal static JsonObject Request(string role = "executor")
    {
        var request = new JsonObject
        {
            ["protocol"] = "EXT-SECRETS-1.qualification.v1", ["runId"] = "run", ["targetCommit"] = new string('a', 40),
            ["isolatedTargetId"] = "isolated", ["planSha256"] = QualificationProtocolSchema.PlanSha256,
        };
        if (role == "enrollment") { request["operation"] = "enroll"; }
        else
        {
            request["caseId"] = "S1.reject.missing-config"; request["caseTargetId"] = new string('d', 64);
            request["enrolledScope"] = new JsonObject
            {
                ["tenantId"] = "tenant", ["targetIds"] = new JsonArray(new string('a', 64)), ["providerCopyIds"] = new JsonArray(),
                ["logicalId"] = new string('d', 64), ["sealId"] = "seal", ["batchId"] = "batch", ["manifestDigest"] = new string('e', 64),
                ["initialAttestationOrdinal"] = 1, ["initialKeyVersion"] = "v1", ["successorKeyVersion"] = "v2", ["exclusiveExpiry"] = 1000,
                ["issuer"] = "issuer", ["nonceDigest"] = new string('f', 64),
            };
            request["scenario"] = new JsonObject
            {
                ["outcomes"] = new JsonArray("Denied"), ["boundary"] = "none",
                ["persistedStages"] = new JsonArray("AfterEffect", "AfterRestart", "AfterRestore", "AfterLookup"),
                ["expected"] = new JsonObject { ["leakCount"] = 0, ["foreignEffects"] = 0, ["allCopyRevival"] = 0,
                    ["immutableIdentity"] = 1, ["immutableOutcome"] = 1, ["persistedProofCount"] = 4, ["forbiddenEffects"] = 0 },
            };
        }
        BindRequestId(request);
        return request;
    }

    private static void BindRequestId(JsonObject request)
    {
        request.Remove("requestId");
        request["requestId"] = Convert.ToHexString(SHA256.HashData(Canonical(request))).ToLowerInvariant();
    }

    private sealed class TestProvider : IQualificationExecutorProvider
    {
        public string Role => RoleName;
        public string RoleName { get; set; } = "executor";
        public bool Allowed { get; set; } = true;
        public bool Lose { get; set; }
        public bool WaitAfterApply { get; set; }
        public bool Applied { get; private set; }
        public List<string> Operations { get; } = [];
        public List<JsonObject> Requests { get; } = [];
        public List<JsonObject> AuthorizedRequests { get; } = [];
        public QualificationCallerContext? Caller { get; private set; }
        public Func<JsonObject, JsonObject>? Response { get; set; }
        public Task<bool> AuthorizeAsync(JsonObject request, QualificationCallerContext caller, CancellationToken cancellationToken)
        { AuthorizedRequests.Add(request); Caller = caller; return Task.FromResult(Allowed); }
        public async Task<JsonObject?> InvokeAsync(string operation, JsonObject original, CancellationToken cancellationToken)
        {
            Operations.Add(operation);
            Requests.Add(original);
            Applied = true;
            if (WaitAfterApply) { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false); }
            if (Lose) { throw new IOException("lost result"); }
            if (Response is not null) { return Response(original); }
            return new JsonObject { ["payload"] = new JsonObject { ["outcome"] = "Denied", ["observedAt"] = 1, ["validUntil"] = 2 }, ["signature"] = "offline-test" };
        }
    }

    private sealed class TestVerifier : IQualificationPublicProofVerifier
    {
        public bool Verify(string role, JsonObject request, JsonObject envelope, CancellationToken cancellationToken = default) => true;
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
