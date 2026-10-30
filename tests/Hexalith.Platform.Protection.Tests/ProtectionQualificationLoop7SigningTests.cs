using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Actors.Runtime;
using Dapr.Client;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Security;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Custody.Tests;
using Hexalith.Platform.Secrets.Runtime;
using NSubstitute;
using Shouldly;

namespace Hexalith.Platform.Protection.Tests;

/// <summary>Actual role entry gate, signed independent permission receiver, signing actor and provider/trust adapters; no speculative signatures.</summary>
public sealed class ProtectionQualificationLoop7SigningTests
{
    [Fact]
    public async Task MissingActualSigningInputsRefuseEnrollmentWithNoDurableEffectThenHealthyOwnerPasses()
    {
        var token = TestContext.Current.CancellationToken;
        using var engine = new ErasureRootEngineFixture(); using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); using var roleKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new DeletionCapabilitySigningReadinessRequest("tenant-a", "DeletionBatchCapabilitySigningKey", "key-1", "issuer", "audience", "anchor", "1",
            Convert.ToHexStringLower(SHA256.HashData(key.ExportSubjectPublicKeyInfo())), 1, new string('a', 64), "signer-app");
        using var receiver = new ReceivingAuthorityFixture("deletion-signing-owner", handlers: [new DeletionSigningAvailabilityOperationHandler(TimeProvider.System)]);
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|deletion-signing-availability", Methods = ["signing-readiness-permission"] }],
            QualificationBindings = new() { ["deletion-signing-availability:" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)))] = "deletion-signing-owner" } };
        var initialGrant = receiver.State.QualificationBindings;
        var configuration = new SecretsRuntimeConfiguration { CapabilitySignerReference = "signer-app", CapabilitySignerCredentialReference = "file:/independent-signer", PublicTrustReference = "trust-app", CallTimeout = TimeSpan.FromSeconds(10) };
        var transport = Substitute.For<ISecretsCredentialBoundDaprInvoker>(); string mode = "healthy"; var methods = new List<string>();
        transport.InvokeCredentialBoundAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            string method = call.ArgAt<string>(2); methods.Add(method);
            if (mode == "backend-missing") { return Task.FromResult<string?>(null); }
            var body = JsonNode.Parse(call.ArgAt<string>(3))!.AsObject(); var now = DateTimeOffset.UtcNow;
            body["authorized"] = true; body["status"] = mode == "backend-absent" ? "absent" : "present"; body["authorityRevision"] = "independent-provider-revision";
            body["observedAt"] = now.ToString("O"); body["validUntil"] = now.AddSeconds(20).ToString("O"); body["providerIdentity"] = "signer-app";
            if (mode == "operation-missing") { body["operations"] = new JsonArray("LookupDeletionCapability"); }
            return Task.FromResult<string?>(body.ToJsonString());
        });
        transport.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            methods.Add(call.ArgAt<string>(1));
            if (mode == "trust-missing") { return Task.FromResult<string?>(null); }
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult<string?>(new JsonObject { ["tenantId"] = "tenant-a", ["keyFamily"] = "DeletionBatchCapabilitySigningKey", ["capabilityKeyVersion"] = "key-1",
                ["issuer"] = "issuer", ["audience"] = "audience", ["publicAnchorId"] = "anchor", ["publicAnchorVersion"] = "1", ["subjectPublicKeyInfo"] = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()),
                ["trustProfileRevision"] = 1, ["isCurrentNonRevoked"] = mode is not ("required-key-revoked" or "required-key-noncurrent"), ["isRevoked"] = mode == "required-key-revoked", ["observedAt"] = now.ToString("O"), ["validUntil"] = now.AddSeconds(20).ToString("O") }.ToJsonString());
        });
        var authority = new DaprDeletionSigningAvailabilityAuthority(Substitute.For<IDeletionCapabilitySigningAuthority>(), receiver.Authority, "signer-app");
        var provider = new DaprSigningProviderAdapter(configuration, transport); var trust = new DaprSigningTrustAdapter(configuration, transport);
        var ownerState = new InMemoryStateManager(); IDeletionCapabilitySigningActor Owner(bool missingProvider = false, bool missingActor = false)
        {
            if (missingActor) { return Substitute.For<IDeletionCapabilitySigningActor>(); }
            var actor = new DeletionCapabilitySigningActor(ActorHost.CreateForTest<DeletionCapabilitySigningActor>(new ActorTestOptions { ActorId = new(DeletionCapabilitySigningActor.GetReadinessActorId(request)) }), authority, missingProvider ? null : provider, trust);
            typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, ownerState); return actor;
        }
        var args = ProtectionQualificationSourceCatalog.RequiredArguments().ToDictionary(pair => pair.Key, pair => pair.Value.StartsWith("Guard:", StringComparison.Ordinal)
            ? new JsonObject { ["transition"] = new JsonObject { ["Operation"] = (int)Enum.Parse<GovernanceGuardOperation>(pair.Value[6..]) } } : new JsonObject());
        var recipes = ProtectionQualificationSourceCatalog.Compile(args); var observations = recipes.Keys.ToDictionary(id => id, _ => new ProtectionQualificationObservationRecipe("tenant-a", new(), new()));
        var scope = new ProtectionQualificationServiceScope("enrollment", "independent-enrollment", "tenant-a", new string('a', 64), new("installation", "proof", new()), recipes, observations);
        using var native = Substitute.For<DaprClient>(); native.GetStateAsync<JsonElement>("installation", "proof", Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.SerializeToElement(new { agentsRuntimeEnabled = false, storeIdentity = "erc-test", deploymentIdentity = "component" }));
        foreach (string absent in new[] { "provider", "actor", "grant", "backend-missing", "backend-absent", "trust-missing", "operation-missing", "required-key-revoked", "required-key-noncurrent" })
        {
            mode = absent; receiver.State = receiver.State with { QualificationBindings = absent == "grant" ? new() : initialGrant };
            var owner = Owner(absent == "provider", absent == "actor");
            var service = new ProtectionQualificationRoleService(scope, new(engine.Custody, engine.Permissions), null, null, native, roleKey,
                signingPreflight: (_, ct) => ProtectionQualificationSigningPreflight.VerifyRequestsAsync([request], owner, ct));
            string before = ErasureRootCustody.Hash(await engine.Store.HeadAsync("tenant-a")), independentBefore = ErasureRootCustody.Hash(receiver.State with { Revision = 0 });
            await Should.ThrowAsync<InvalidOperationException>(() => service.HandleAsync(new() { ["operationId"] = absent, ["action"] = "EnrollSingleAttempt", ["attemptId"] = "single", ["bindingDigest"] = scope.BindingDigest }, token));
            (ErasureRootCustody.Hash(await engine.Store.HeadAsync("tenant-a")) == before).ShouldBeTrue("Native ERC head changed for missing input mode: " + absent);
            ErasureRootCustody.Hash(receiver.State with { Revision = 0 }).ShouldBe(independentBefore, "Only independent permission-result ordering may change: " + absent);
            ownerState.CommittedState.ShouldBeEmpty();
        }
        receiver.State = receiver.State with { QualificationBindings = initialGrant }; mode = "healthy";
        var host = new ErasureRootHostInstallation(engine.Custody.Installation, "https://native.invalid/", "env:UNUSED", new(), null!, null!, null!, null!, null!,
            [new("tenant-a", "DeletionBatchCapabilitySigningKey", "key-1", "issuer", "audience", "anchor", "1", key.ExportSubjectPublicKeyInfo(), 1, false),
             new("tenant-a", "DeletionBatchCapabilitySigningKey", "historical-revoked", "issuer", "audience", "anchor", "1", key.ExportSubjectPublicKeyInfo(), 1, true)], "", "", "", "");
        (await ProtectionQualificationSigningPreflight.VerifyAsync(host, "tenant-a", request.InstallationBinding, "signer-app", Owner(), token, new HashSet<string> { "key-1" })).ShouldBeTrue();
        ownerState.CommittedState.ShouldBeEmpty();
        var healthy = Owner();
        var admitted = new ProtectionQualificationRoleService(scope, new(engine.Custody, engine.Permissions), null, null, native, roleKey,
            signingPreflight: (_, ct) => ProtectionQualificationSigningPreflight.VerifyRequestsAsync([request], healthy, ct));
        (await admitted.HandleAsync(new() { ["operationId"] = "healthy", ["action"] = "EnrollSingleAttempt", ["attemptId"] = "single", ["bindingDigest"] = scope.BindingDigest }, token))["statement"]!["response"]!["status"]!.GetValue<string>().ShouldBe("Enrolled");
        methods.ShouldAllBe(method => method == "query-trust" || method == "query-signing-availability"); ownerState.CommittedState.ShouldBeEmpty();
    }

    [Fact]
    public async Task LaterCasePreparesWithItsHealthySignKeyAfterEarlierCaseRevokesAnotherKey()
    {
        var token = TestContext.Current.CancellationToken;
        using var fixture = new ProtectionQualificationPreparationFixture(); await fixture.InitializeAsync();
        using var roleKey = ECDsa.Create(ECCurve.NamedCurves.nistP256); using var publicKey = ECDsa.Create(); publicKey.ImportFromPem(fixture.PublicKeyPem);
        string binding = new('a', 64); string[] versions = ["healthy-key-a", "healthy-key-b"];
        var requests = versions.Select(version => new DeletionCapabilitySigningReadinessRequest("tenant-a", "DeletionBatchCapabilitySigningKey", version,
            "qualified-issuer", "protection-audience", "test-anchor", "1", Convert.ToHexStringLower(SHA256.HashData(publicKey.ExportSubjectPublicKeyInfo())), 1, binding, "signer-app")).ToArray();
        using var receiver = new ReceivingAuthorityFixture("deletion-signing-owner", handlers: [new DeletionSigningAvailabilityOperationHandler(TimeProvider.System)]);
        receiver.State = receiver.State with { Callers = [receiver.State.Callers[0] with { ScopeReference = "tenant-a|deletion-signing-availability", Methods = ["signing-readiness-permission"] }],
            QualificationBindings = requests.ToDictionary(request => "deletion-signing-availability:" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request))), _ => "deletion-signing-owner") };
        var config = new SecretsRuntimeConfiguration { CapabilitySignerReference = "signer-app", CapabilitySignerCredentialReference = "file:/independent-signer", PublicTrustReference = "trust-app", CallTimeout = TimeSpan.FromSeconds(10) };
        var transport = Substitute.For<ISecretsCredentialBoundDaprInvoker>();
        transport.InvokeCredentialBoundAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            call.ArgAt<string>(2).ShouldBe("query-signing-availability"); var body = JsonNode.Parse(call.ArgAt<string>(3))!.AsObject(); var now = DateTimeOffset.UtcNow;
            body["authorized"] = true; body["status"] = "present"; body["authorityRevision"] = "provider-current"; body["providerIdentity"] = "signer-app";
            body["observedAt"] = now.ToString("O"); body["validUntil"] = now.AddSeconds(20).ToString("O"); return Task.FromResult<string?>(body.ToJsonString());
        });
        transport.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            call.ArgAt<string>(1).ShouldBe("query-trust"); string version = JsonNode.Parse(call.ArgAt<string>(2))!["keyVersion"]!.GetValue<string>();
            bool revoked = fixture.Guard.State.CompromisedKeyVersions.Contains(version, StringComparer.Ordinal); var now = DateTimeOffset.UtcNow;
            return Task.FromResult<string?>(new JsonObject { ["tenantId"] = "tenant-a", ["keyFamily"] = "DeletionBatchCapabilitySigningKey", ["capabilityKeyVersion"] = version,
                ["issuer"] = "qualified-issuer", ["audience"] = "protection-audience", ["publicAnchorId"] = "test-anchor", ["publicAnchorVersion"] = "1", ["subjectPublicKeyInfo"] = Convert.ToBase64String(publicKey.ExportSubjectPublicKeyInfo()),
                ["trustProfileRevision"] = 1, ["isCurrentNonRevoked"] = !revoked, ["isRevoked"] = revoked, ["observedAt"] = now.ToString("O"), ["validUntil"] = now.AddSeconds(20).ToString("O") }.ToJsonString());
        });
        var authority = new DaprDeletionSigningAvailabilityAuthority(Substitute.For<IDeletionCapabilitySigningAuthority>(), receiver.Authority, "signer-app");
        var provider = new DaprSigningProviderAdapter(config, transport); var trust = new DaprSigningTrustAdapter(config, transport); var signingState = new InMemoryStateManager();
        var router = Substitute.For<IDeletionCapabilitySigningActor>();
        router.ReadAvailabilityAsync(Arg.Any<DeletionCapabilitySigningReadinessRequest>()).Returns(call =>
        {
            var request = call.Arg<DeletionCapabilitySigningReadinessRequest>();
            var actor = new DeletionCapabilitySigningActor(ActorHost.CreateForTest<DeletionCapabilitySigningActor>(new ActorTestOptions { ActorId = new(DeletionCapabilitySigningActor.GetReadinessActorId(request)) }), authority, provider, trust);
            typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, signingState); return actor.ReadAvailabilityAsync(request);
        });
        var arguments = ProtectionQualificationSourceCatalog.RequiredArguments().ToDictionary(pair => pair.Key, pair => pair.Value.StartsWith("Guard:", StringComparison.Ordinal)
            ? new JsonObject { ["transition"] = new JsonObject { ["Operation"] = (int)Enum.Parse<GovernanceGuardOperation>(pair.Value[6..]) } }
            : pair.Value == "Sign" ? new JsonObject { ["CapabilityKeyVersion"] = pair.Key.StartsWith("P26/", StringComparison.Ordinal) ? "healthy-key-a" : "healthy-key-b" } : new JsonObject());
        var recipes = ProtectionQualificationSourceCatalog.Compile(arguments); var observations = recipes.Keys.ToDictionary(id => id, _ => new ProtectionQualificationObservationRecipe("tenant-a", new(), new()));
        var scope = new ProtectionQualificationServiceScope("executor", "sequential-executor", "tenant-a", binding, new("source", "installation", new()), recipes, observations);
        var host = new ErasureRootHostInstallation(fixture.Consumption.Custody.Installation, "https://native.invalid/", "env:UNUSED", new(), null!, null!, null!, null!, null!,
            versions.Select(version => new PublishedDeletionCapabilityTrust("tenant-a", "DeletionBatchCapabilitySigningKey", version, "qualified-issuer", "protection-audience", "test-anchor", "1", publicKey.ExportSubjectPublicKeyInfo(), 1, false)).ToArray(), "", "", "", "");
        fixture.NativeState["installation"] = JsonSerializer.SerializeToElement(new { agentsRuntimeEnabled = false, storeIdentity = fixture.Consumption.Custody.Installation.StoreId, deploymentIdentity = "sequential-component" });
        Task<bool> Readiness(JsonObject request, CancellationToken ct) => ProtectionQualificationSigningPreflight.VerifyAsync(host, "tenant-a", binding, "signer-app", router, ct,
            ProtectionQualificationSigningPreflight.RequiredKeyVersions(scope, request["action"]!.GetValue<string>() == "PrepareFixture" ? request["caseId"]!.GetValue<string>() : null));
        var enrollment = new ProtectionQualificationRoleService(scope with { Role = "enrollment", Identity = "independent-enrollment" }, new(fixture.Consumption.Custody, fixture.Consumption.Permissions), null, null, fixture.Native, roleKey, signingPreflight: Readiness);
        var executor = new ProtectionQualificationRoleService(scope, new(fixture.Consumption.Custody, fixture.Consumption.Permissions), fixture.Runner, null, fixture.Native, roleKey, signingPreflight: Readiness);
        (await enrollment.HandleAsync(new() { ["operationId"] = "enroll", ["action"] = "EnrollSingleAttempt", ["bindingDigest"] = binding, ["attemptId"] = "single" }, token))["statement"]!["response"]!["status"]!.GetValue<string>().ShouldBe("Enrolled");
        async Task<JsonObject> RegisteredPreparation(string caseId, string operationId)
        {
            var request = fixture.Request(caseId, "PrepareFixture"); request["operationId"] = operationId;
            await enrollment.HandleAsync(new() { ["operationId"] = "register-" + operationId, ["action"] = "RegisterOriginalOperation", ["bindingDigest"] = binding,
                ["originalRole"] = "executor", ["originalOperationId"] = operationId, ["originalRequestSha256"] = ProtectionQualificationOperationJournal.Digest(request) }, token); return request;
        }
        (await executor.HandleAsync(await RegisteredPreparation("P26", "prepare-before"), token))["statement"]!["response"]!["status"]!.GetValue<string>().ShouldBe("Prepared");
        await fixture.RunAsync(fixture.Recipes("P26")["P26"]["CompromiseBeforeRegistration"], "P26", "CompromiseBeforeRegistration");
        fixture.Guard.State.CompromisedKeyVersions.ShouldContain("healthy-key-a");
        (await executor.HandleAsync(await RegisteredPreparation("P05", "prepare-healthy-b"), token))["statement"]!["response"]!["status"]!.GetValue<string>().ShouldBe("Prepared");
        var denied = await RegisteredPreparation("P26", "prepare-revoked-a"); string before = ErasureRootCustody.Hash(await fixture.Consumption.Store.HeadAsync("tenant-a"));
        await Should.ThrowAsync<InvalidOperationException>(() => executor.HandleAsync(denied, token));
        ErasureRootCustody.Hash(await fixture.Consumption.Store.HeadAsync("tenant-a")).ShouldBe(before); signingState.CommittedState.ShouldBeEmpty(); fixture.Consumption.Store.TrimCount.ShouldBe(0);
    }

}
