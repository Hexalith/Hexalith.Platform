using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Runtime;

using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class SecretsAuthorityFailureTests
{
    private readonly RecordingInvoker _invoker = new();
    private readonly FixedClock _clock = new(Fixtures.Now);
    private readonly SecretsRuntimeConfiguration _configuration = Fixtures.Ready();

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"status\":\"absent\"}")]
    [InlineData("{\"authorized\":false}")]
    public async Task UnboundAbsenceAndDenialNeverCauseSigningReplayOrCompromiseEffects(string proof)
    {
        _invoker.Reply = (_, _, _, _) => proof;
        var payload = new DeletionBatchCapabilityV1("issuer", "audience", "tenant", "request", "seal", "batch", 1, "batch-1", new string('a', 64), "guard", 1, 1, 1, "k1");
        (await new DaprSigningProviderAdapter(_configuration, _invoker, _clock).SignAsync(payload, "original", TestContext.Current.CancellationToken)).Outcome.State.ShouldBe(DeletionCapabilitySigningState.Unavailable);
        (await new DaprReplayRegistrarAdapter(_configuration, _invoker, _clock).RegisterAsync(new("issuer", "nonce", "tenant", "command", "k1", new string('a', 64)), Fixtures.Now, TimeSpan.FromDays(7), TestContext.Current.CancellationToken)).ShouldBeNull();
        (await new DaprCompromiseRegistrarAdapter(_configuration, _invoker, _clock).RegisterAsync(Envelope(), TestContext.Current.CancellationToken)).ShouldBeNull();
        _invoker.Calls.ShouldAllBe(call => call.Method.StartsWith("query-", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("fresh")]
    [InlineData("stale")]
    [InlineData("cross-request")]
    [InlineData("wrong-authority")]
    [InlineData("wrong-method")]
    [InlineData("denied")]
    public async Task OnlyFreshTypedExactAbsenceCanAdmitATransitionEffect(string kind)
    {
        byte[] bytes = [1];
        var transition = new AnchoredStateTransition("scope", 1, 2, new string('a', 64), Convert.ToHexString(SHA256.HashData(bytes)), bytes);
        _invoker.Reply = (_, method, body, _) =>
        {
            JsonObject proof = Authorized(body);
            if (method == "query-transition")
            {
                proof["status"] = "absent";
                proof["proofType"] = "absence";
                proof["authorityReference"] = kind == "wrong-authority" ? "other" : "inventory-app";
                proof["queryMethod"] = kind == "wrong-method" ? "query-sign" : method;
                if (kind == "stale") { proof["observedAt"] = Fixtures.Now.AddMinutes(-1).ToString("O"); }
                if (kind == "cross-request") { proof["scopeId"] = "other"; }
                if (kind == "denied") { proof["authorized"] = false; }
            }
            return proof.ToJsonString();
        };
        (await new DaprInventoryAuthorityAdapter(_configuration, _invoker, _clock).RecordTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBe(kind == "fresh");
        _invoker.Calls.Count(call => call.Method.StartsWith("effect-", StringComparison.Ordinal)).ShouldBe(kind == "fresh" ? 1 : 0);
    }

    [Theory]
    [InlineData("authorized", "deny")]
    [InlineData("observedAt", "stale")]
    [InlineData("tenantId", "other")]
    [InlineData("providerTarget", "other")]
    [InlineData("publicAnchorVersion", "other")]
    [InlineData("authorityRevision", "missing")]
    public async Task InventoryRejectsDenialStalenessAndEverySubstitutedIdentity(string field, string mutation)
    {
        var adapter = new DaprInventoryAuthorityAdapter(_configuration, _invoker, _clock);
        _invoker.Reply = (_, _, body, _) => Mutate(Authorized(body), field, mutation);
        var key = new PlatformKeyVersion("tenant", PlatformKeyPurpose.TenantKek, "alias", "v1", "provider", "inventory-app", "anchor", "1");
        (await adapter.AuthorizeOperationAsync(key, "ApplyKeyInventory", TestContext.Current.CancellationToken)).ShouldBeFalse();
        _invoker.Calls.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task EchoOnlyAuthorizationIsRejectedAndCanonicalBytesAreBound()
    {
        var key = new PlatformKeyVersion("tenant", PlatformKeyPurpose.TenantKek, "alias", "v1", "provider", "inventory-app", null, null);
        _invoker.Reply = (_, _, body, _) => body;
        (await new DaprInventoryAuthorityAdapter(_configuration, _invoker, _clock).AuthorizeOperationAsync(key, "ApplyKeyInventory", TestContext.Current.CancellationToken)).ShouldBeFalse();
        var adapter = new DaprInventoryAuthorityAdapter(_configuration, _invoker, _clock);
        byte[] bytes = [1, 2, 3];
        var transition = new AnchoredStateTransition("scope", 1, 2, new string('a', 64), Convert.ToHexString(SHA256.HashData(bytes)), bytes);
        _invoker.Reply = (_, _, body, _) => Authorized(body).ToJsonString();
        (await adapter.VerifyTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeTrue();
        _invoker.Calls.Last().Body.ShouldContain(Convert.ToBase64String(bytes));
        _invoker.Calls.Clear();
        (await adapter.RecordTransitionAsync(transition with { TargetBytes = [9] }, TestContext.Current.CancellationToken)).ShouldBeFalse();
        _invoker.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("keyFamily")]
    [InlineData("keyVersion")]
    [InlineData("revocationRevision")]
    [InlineData("trustProfileRevision")]
    public async Task RevocationBindsCompleteEnvelopeAndCompromiseReceipt(string field)
    {
        _invoker.Reply = (_, _, body, _) =>
        {
            JsonObject proof = Authorized(body);
            if (field.EndsWith("Revision", StringComparison.Ordinal)) { proof[field] = 99; } else { proof[field] = "other"; }
            proof["ownerRevision"] = 1;
            proof["keyBlockSetRevision"] = 1;
            proof["receiptId"] = "receipt";
            proof["affectedBatchIds"] = new JsonArray();
            return proof.ToJsonString();
        };
        (await new DaprRevocationAuthenticatorAdapter(_configuration, _invoker, _clock).VerifyAsync(Envelope(), "evidence", TestContext.Current.CancellationToken)).ShouldBeNull();
        (await new DaprCompromiseRegistrarAdapter(_configuration, _invoker, _clock).RegisterAsync(Envelope(), TestContext.Current.CancellationToken)).ShouldBeNull();
        _invoker.Calls.ShouldAllBe(call => call.Method.StartsWith("query-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FinalDecisionCheckBindsCompleteManifestCohortAndProfile()
    {
        var basis = new DecisionAuthorityExpectedBasis("decision", "contract", 1, 2, null, "root", ["eval"], ["Product"]);
        var profile = new DecisionAuthorityPublishedProfile("issuer", "audience", 3, "k1", "anchor", "1", [1], false, true, "DecisionApprovalVerificationOnly", TimeSpan.FromMinutes(5), Fixtures.Now.AddMinutes(-1), Fixtures.Now.AddMinutes(5), ["recorder"]);
        var manifest = new DecisionAuthorityManifest("issuer", "audience", "decision", "contract", 1, 2, null, "root", ["eval"], [new("Product", "actor", "authority", "1", "evidence")], Fixtures.Now, Fixtures.Now.AddMinutes(5), "k1");
        var adapter = new DaprDecisionAuthorityAdapter(_configuration, _invoker, _clock);
        _invoker.Reply = (_, _, body, _) => Authorized(body).ToJsonString();
        (await adapter.VerifyAuthorityBoundaryAsync(basis, manifest, profile, TestContext.Current.CancellationToken)).ShouldBeTrue();
        string original = _invoker.Calls.Last().Body;
        _invoker.Reply = (_, _, _, _) => Authorized(original).ToJsonString();
        (await adapter.VerifyAuthorityBoundaryAsync(basis, manifest with { Approvers = [new("Product", "substituted", "authority", "1", "evidence")] }, profile, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await adapter.VerifyAuthorityBoundaryAsync(basis, manifest with { ExclusiveExpiry = Fixtures.Now.AddMinutes(6) }, profile, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await adapter.VerifyAuthorityBoundaryAsync(basis, manifest, profile with { IsRevoked = true }, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await adapter.VerifyAuthorityBoundaryAsync(basis with { RequiredRoles = ["Security"] }, manifest, profile, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task DecisionProfileFreshnessDoesNotTruncatePublishedProfileValidity()
    {
        var basis = new DecisionAuthorityExpectedBasis("decision", "contract", 1, 2, null, "root", ["eval"], ["Product"]);
        _invoker.Reply = (_, _, body, _) =>
        {
            JsonObject proof = Authorized(body);
            proof["proofValidUntil"] = Fixtures.Now.AddSeconds(20).ToString("O");
            proof["validFrom"] = Fixtures.Now.AddHours(-1).ToString("O");
            proof["validUntil"] = Fixtures.Now.AddDays(1).ToString("O");
            proof["issuer"] = "issuer";
            proof["audience"] = "audience";
            proof["profileRevision"] = 3;
            proof["publicAnchorId"] = "anchor";
            proof["publicAnchorVersion"] = "1";
            proof["subjectPublicKeyInfo"] = "AQ==";
            proof["isRevoked"] = false;
            proof["isIndependentlyGoverned"] = true;
            proof["keyPurpose"] = "DecisionApprovalVerificationOnly";
            proof["maximumManifestLifetimeSeconds"] = 300;
            proof["forbiddenApprovalActors"] = new JsonArray("recorder");
            return proof.ToJsonString();
        };
        DecisionAuthorityPublishedProfile? profile = await new DaprDecisionAuthorityAdapter(_configuration, _invoker, _clock).ResolveAsync(basis, "k1", TestContext.Current.CancellationToken);
        profile.ShouldNotBeNull();
        profile.ValidUntil.ShouldBe(Fixtures.Now.AddDays(1));
    }

    [Fact]
    public async Task SuccessorRevisionOverflowIsRejectedBeforeAuthorityCall()
    {
        byte[] bytes = [1];
        var transition = new AnchoredStateTransition("scope", long.MaxValue, long.MinValue, new string('a', 64), Convert.ToHexString(SHA256.HashData(bytes)), bytes);
        _invoker.Reply = (_, _, body, _) => Authorized(body).ToJsonString();
        (await new DaprInventoryAuthorityAdapter(_configuration, _invoker, _clock).VerifyTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeFalse();
        _invoker.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("integer")]
    [InlineData("null")]
    [InlineData("boolean")]
    public async Task FreshBoundRevocationCannotAuthorizeAbsentOrMalformedStatus(string status)
    {
        _invoker.Reply = (_, _, body, _) =>
        {
            JsonObject proof = Authorized(body);
            proof["status"] = status switch { "integer" => JsonValue.Create(1), "boolean" => JsonValue.Create(true), "null" => null, _ => JsonValue.Create("absent") };
            return proof.ToJsonString();
        };
        (await new DaprRevocationAuthenticatorAdapter(_configuration, _invoker, _clock).VerifyAsync(Envelope(), "evidence", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Theory]
    [InlineData("isRevoked", "true")]
    [InlineData("isIndependentlyGoverned", "false")]
    [InlineData("isRevoked", "missing")]
    [InlineData("isIndependentlyGoverned", "missing")]
    public async Task OtherwiseFreshAuthorizedDecisionProfileCannotNormalizeUnsafeOrMissingFlags(string field, string mutation)
    {
        var basis = new DecisionAuthorityExpectedBasis("decision", "contract", 1, 2, null, "root", ["eval"], ["Product"]);
        _invoker.Reply = (_, _, body, _) =>
        {
            JsonObject proof = Authorized(body);
            proof["proofValidUntil"] = Fixtures.Now.AddSeconds(20).ToString("O");
            proof["validFrom"] = Fixtures.Now.AddHours(-1).ToString("O");
            proof["validUntil"] = Fixtures.Now.AddDays(1).ToString("O");
            proof["issuer"] = "issuer"; proof["audience"] = "audience"; proof["profileRevision"] = 3;
            proof["publicAnchorId"] = "anchor"; proof["publicAnchorVersion"] = "1"; proof["subjectPublicKeyInfo"] = "AQ==";
            proof["isRevoked"] = false; proof["isIndependentlyGoverned"] = true;
            proof["keyPurpose"] = "DecisionApprovalVerificationOnly"; proof["maximumManifestLifetimeSeconds"] = 300;
            proof["forbiddenApprovalActors"] = new JsonArray("recorder");
            if (mutation == "missing") { proof.Remove(field); } else { proof[field] = mutation == "true"; }
            return proof.ToJsonString();
        };
        (await new DaprDecisionAuthorityAdapter(_configuration, _invoker, _clock).ResolveAsync(basis, "k1", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task OtherwiseCompleteFreshAuthorizedAbsentDecisionProfileIsRejected()
    {
        var basis = new DecisionAuthorityExpectedBasis("decision", "contract", 1, 2, null, "root", ["eval"], ["Product"]);
        _invoker.Reply = (_, _, body, _) =>
        {
            JsonObject proof = Authorized(body);
            proof["status"] = "absent"; proof["proofValidUntil"] = Fixtures.Now.AddSeconds(20).ToString("O");
            proof["validFrom"] = Fixtures.Now.AddHours(-1).ToString("O"); proof["validUntil"] = Fixtures.Now.AddDays(1).ToString("O");
            proof["issuer"] = "issuer"; proof["audience"] = "audience"; proof["profileRevision"] = 3;
            proof["publicAnchorId"] = "anchor"; proof["publicAnchorVersion"] = "1"; proof["subjectPublicKeyInfo"] = "AQ==";
            proof["isRevoked"] = false; proof["isIndependentlyGoverned"] = true; proof["keyPurpose"] = "DecisionApprovalVerificationOnly";
            proof["maximumManifestLifetimeSeconds"] = 300; proof["forbiddenApprovalActors"] = new JsonArray("recorder");
            return proof.ToJsonString();
        };
        (await new DaprDecisionAuthorityAdapter(_configuration, _invoker, _clock).ResolveAsync(basis, "k1", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task OversizedAndInvalidUtf8ResponsesAreRejectedBeforeParsing()
    {
        SecretsProof.Parse("{\"large\":\"" + new string('a', SecretsProof.MaximumBytes) + "\"}").ShouldBeNull();
        using var oversize = new MemoryStream(new byte[SecretsProof.MaximumBytes + 1]);
        (await DaprSecretsInvoker.ReadBoundedAsync(oversize, TestContext.Current.CancellationToken)).ShouldBeNull();
        using var invalid = new MemoryStream(new byte[] { 0xff });
        (await DaprSecretsInvoker.ReadBoundedAsync(invalid, TestContext.Current.CancellationToken)).ShouldBeNull();
        using var valid = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        (await DaprSecretsInvoker.ReadBoundedAsync(valid, TestContext.Current.CancellationToken)).ShouldBe("{}");
    }

    internal static JsonObject Authorized(string body)
    {
        var proof = (JsonObject)JsonNode.Parse(body)!;
        proof["authorized"] = true;
        proof["authorityRevision"] = "current-1";
        proof["observedAt"] = Fixtures.Now.ToString("O");
        proof["validUntil"] = Fixtures.Now.AddSeconds(20).ToString("O");
        return proof;
    }

    private static string Mutate(JsonObject proof, string field, string mutation)
    {
        if (mutation == "deny") { proof[field] = false; }
        else if (mutation == "stale") { proof[field] = Fixtures.Now.AddMinutes(-1).ToString("O"); }
        else if (mutation == "missing") { proof.Remove(field); }
        else { proof[field] = mutation; }
        return proof.ToJsonString();
    }

    private static DeletionCapabilityRevocationEnvelope Envelope() => new("issuer", "audience", "tenant", "DeletionBatchCapabilitySigningKey", "k1", 2, 4, "event", new string('a', 64));
}
