using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Security;
using Hexalith.Platform.Custody;
using Hexalith.Platform.Secrets.Qualification.Observer;
using Hexalith.Platform.Secrets.Runtime;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.Platform.Secrets.Runtime.Tests;

public sealed class SecretsRuntimeAdapterTests
{
    private readonly RecordingInvoker _invoker = new();
    private readonly FixedClock _clock = new(Fixtures.Now);
    private readonly SecretsRuntimeConfiguration _configuration = Fixtures.Ready();
    private readonly PlatformHmacScope _scope = new("tenant-a", PlatformHmacPurpose.ContentDigest);

    [Theory]
    [InlineData("store", "runtime.providerBinding")]
    [InlineData("credential", "runtime.hmacCredentialReference")]
    [InlineData("authority", "runtime.currentInventoryAuthorityReference")]
    [InlineData("profile", "runtime.profileReference")]
    [InlineData("timeout", "runtime.callTimeout")]
    [InlineData("issuer", "runtime.issuer")]
    public void AddExplicitHmac_NamesTheMissingInput(string missing, string expected)
    {
        SecretsRuntimeConfiguration configuration = missing switch
        {
            "store" => _configuration with { ProviderBinding = null },
            "credential" => _configuration with { HmacCredentialReference = null },
            "authority" => _configuration with { CurrentInventoryAuthorityReference = null },
            "profile" => _configuration with { ProfileReference = null },
            "timeout" => _configuration with { CallTimeout = null },
            _ => _configuration with { Issuer = null, ExpectedProfile = null },
        };
        new ServiceCollection().AddExplicitHmac(configuration, _invoker, _clock).ShouldBe(expected);
        _invoker.Calls.ShouldBeEmpty();
    }

    [Fact]
    public void AddExplicitHmac_NamesProfileNotBeforeWhenTheBoundProfileIsAbsent()
    {
        new ServiceCollection().AddExplicitHmac(_configuration with { ExpectedProfile = null }, _invoker, _clock).ShouldBe("runtime.profileNotBefore");
    }

    [Fact]
    public async Task MissingReference_NamesItselfAndMakesNoCall()
    {
        var adapter = new DaprHmacAuthorityAdapter(_configuration with { ProviderBinding = null }, _invoker, _clock);
        SecretsDependencyNotAvailableException error = await Should.ThrowAsync<SecretsDependencyNotAvailableException>(
            () => adapter.ResolveAsync("inventory-app", _scope, "k1", TestContext.Current.CancellationToken).AsTask());
        error.Input.ShouldBe("runtime.providerBinding");
        error.Message.ShouldContain("DependencyNotAvailable: EXT-SECRETS-1");
        error.Message.ShouldContain(error.ProvisioningAction);
        _invoker.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task HmacResolve_ReturnsCurrentAndRevokedProofsAndRejectsStaleCrossTenantWideWindowAndSecretNames()
    {
        var adapter = new DaprHmacAuthorityAdapter(_configuration, _invoker, _clock);
        _invoker.Reply = (_, _, _, _) => Fixtures.InventoryProof("tenant-a", "Active", 21);
        DaprPlatformHmacInventory? current = await adapter.ResolveAsync("inventory-app", _scope, "k1", TestContext.Current.CancellationToken);
        current.ShouldNotBeNull();
        current.Metadata.State.ShouldBe(PlatformHmacKeyState.Active);
        current.Metadata.Version.ShouldBe("k1");

        _invoker.Reply = (_, _, _, _) => Fixtures.InventoryProof("tenant-a", "Revoked", 21);
        (await adapter.ResolveAsync("inventory-app", _scope, "k1", TestContext.Current.CancellationToken))!.Metadata.State.ShouldBe(PlatformHmacKeyState.Revoked);

        _invoker.Reply = (_, _, _, _) => Fixtures.InventoryProof("other-tenant", "Active", 21);
        (await adapter.ResolveAsync("inventory-app", _scope, "k1", TestContext.Current.CancellationToken)).ShouldBeNull();

        _invoker.Reply = (_, _, _, _) => Fixtures.InventoryProof("tenant-a", "Active", 31);
        (await adapter.ResolveAsync("inventory-app", _scope, "k1", TestContext.Current.CancellationToken)).ShouldBeNull();

        _invoker.Reply = (_, _, _, _) => Fixtures.InventoryProof("tenant-a", "Active", 21, ",\"token\":\"raw\"");
        (await adapter.ResolveAsync("inventory-app", _scope, "k1", TestContext.Current.CancellationToken)).ShouldBeNull();
        _invoker.Reply = (_, _, _, _) => Fixtures.InventoryProof("tenant-a", "0", 21);
        (await adapter.ResolveAsync("inventory-app", _scope, "k1", TestContext.Current.CancellationToken)).ShouldBeNull();
        _invoker.Calls.Count.ShouldBe(6);
    }

    [Fact]
    public async Task HmacCheck_RemainsTheSecretReadGate()
    {
        var client = Substitute.For<DaprClient>();
        var options = new DaprPlatformHmacOptions(_configuration.ProviderBinding!, _configuration.HmacCredentialReference!,
            _configuration.CurrentInventoryAuthorityReference!, _configuration.ProfileReference!, _configuration.ExpectedProfile!, TimeSpan.FromSeconds(30));
        var adapter = new DaprHmacAuthorityAdapter(_configuration, _invoker, _clock);
        var provider = new DaprPlatformHmacKeyProvider(client, options, adapter, _clock);
        _invoker.Reply = (_, method, _, _) => method == "query-profile" ? ProfileProof() : Fixtures.InventoryProof("tenant-a", "Revoked", 21);
        (await provider.ResolveAsync(_scope, "k1", TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.RevokedKey);
        client.ReceivedCalls().ShouldBeEmpty();

        _invoker.Reply = (_, method, _, _) => method == "query-profile" ? ProfileProof() : Fixtures.InventoryProof("tenant-a", "Active", 21);
        client.GetSecretAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { ["value"] = Convert.ToBase64String(new byte[32]) });
        (await provider.ResolveAsync(_scope, "k1", TestContext.Current.CancellationToken)).Status.ShouldBe(CustodyStatus.Succeeded);
        client.ReceivedCalls().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task CallerCancellationPropagates_AndBudgetTimeoutIsUnavailable()
    {
        var adapter = new DaprInventoryAuthorityAdapter(_configuration with { CallTimeout = TimeSpan.FromMilliseconds(50) }, _invoker, _clock);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => adapter.AuthorizeOperationAsync(Key(), "ApplyKeyInventory", canceled.Token));
        _invoker.Calls.ShouldBeEmpty();

        _invoker.ThrowBudgetCancellation = true;
        (await adapter.AuthorizeOperationAsync(Key(), "ApplyKeyInventory", TestContext.Current.CancellationToken)).ShouldBeFalse();
        _invoker.Calls.Count.ShouldBe(1);
    }

    [Fact]
    public async Task InventoryAuthorization_ComparesAliasAuthorityAndOperation()
    {
        var adapter = new DaprInventoryAuthorityAdapter(_configuration, _invoker, _clock);
        _invoker.Reply = (_, _, body, _) => WithWindow(body);
        (await adapter.AuthorizeOperationAsync(Key(), "ApplyKeyInventory", TestContext.Current.CancellationToken)).ShouldBeTrue();
        _invoker.Reply = (_, _, body, _) => body.Replace("\"ApplyKeyInventory\"", "\"ReadKeyInventory\"", StringComparison.Ordinal);
        (await adapter.AuthorizeOperationAsync(Key(), "ApplyKeyInventory", TestContext.Current.CancellationToken)).ShouldBeFalse();
        _invoker.Reply = (_, _, body, _) => body.Replace("tenant-a", "tenant-b", StringComparison.Ordinal);
        (await adapter.AuthorizeOperationAsync(Key(), "ReadKeyInventory", TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Transition_VerifyHasNoEffect_AndRecoverNamesMissingJournal()
    {
        var adapter = new DaprInventoryAuthorityAdapter(_configuration, _invoker, _clock);
        var transition = new AnchoredStateTransition("scope", 1, 2, "a".PadLeft(64, 'a'), Convert.ToHexString(SHA256.HashData(new byte[] { 1 })), [1]);
        _invoker.Reply = (_, _, body, _) => WithWindow(body);
        (await adapter.VerifyTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeTrue();
        _invoker.Calls.ShouldAllBe(call => call.Method == "query-transition");
        _invoker.Calls[0].Body.ShouldContain("predecessorDigest");
        _invoker.Calls[0].Body.ShouldContain("targetDigest");
        _invoker.Calls[0].Body.ShouldContain("VerifyTransition");

        _invoker.Calls.Clear();
        _invoker.Reply = (_, method, body, _) => method == "query-transition" ? Absent(body, "inventory-app", method) : WithWindow(body);
        (await adapter.RecordTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeTrue();
        _invoker.Calls.Select(call => call.Method).ShouldBe(["query-transition", "effect-transition"]);
        _invoker.Calls[1].Body.ShouldContain("predecessorDigest");

        _invoker.Calls.Clear();
        _invoker.Reply = (_, method, body, _) => method == "query-transition" ? body.Replace("\"scope\"", "\"other\"", StringComparison.Ordinal) : body;
        (await adapter.RecordTransitionAsync(transition, TestContext.Current.CancellationToken)).ShouldBeFalse();
        _invoker.Calls.Select(call => call.Method).ShouldBe(["query-transition"]);

        _invoker.Calls.Clear();
        _invoker.Reply = (_, _, body, _) => WithWindow(body);
        (await adapter.RecordRevisionAsync(Key(), 1, 2, new string('c', 64), TestContext.Current.CancellationToken)).ShouldBeTrue();
        _invoker.Calls.ShouldHaveSingleItem().Body.ShouldContain("targetDigest");
        _invoker.Calls[0].Body.ShouldNotContain("predecessorDigest");

        var blocked = new DaprInventoryAuthorityAdapter(_configuration with { NonrollbackJournalReference = null }, _invoker, _clock);
        _invoker.Calls.Clear();
        SecretsDependencyNotAvailableException error = await Should.ThrowAsync<SecretsDependencyNotAvailableException>(
            () => blocked.RecoverTransitionAsync(transition, TestContext.Current.CancellationToken));
        error.Input.ShouldBe("runtime.nonrollbackJournalReference");
        _invoker.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task DecisionResolution_RejectsNullBasisOverflowLifetimeAndEmptySpki()
    {
        var adapter = new DaprDecisionAuthorityAdapter(_configuration, _invoker, _clock);
        (await adapter.ResolveAsync(null!, "k1", TestContext.Current.CancellationToken)).ShouldBeNull();
        _invoker.Calls.ShouldBeEmpty();
        typeof(IIndependentDecisionAuthority).GetMethods().Select(method => method.Name).ShouldNotContain("Sign");

        _invoker.Reply = (_, _, _, _) => DecisionProof(long.MaxValue, "AQ==");
        (await adapter.ResolveAsync(Basis(), "k1", TestContext.Current.CancellationToken)).ShouldBeNull();
        _invoker.Reply = (_, _, _, _) => DecisionProof(0, "AQ==");
        (await adapter.ResolveAsync(Basis(), "k1", TestContext.Current.CancellationToken)).ShouldBeNull();
        _invoker.Reply = (_, _, _, _) => DecisionProof(300, "");
        (await adapter.ResolveAsync(Basis(), "k1", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task DecisionVerification_ComparesBasisApproverAndBoundary()
    {
        var adapter = new DaprDecisionAuthorityAdapter(_configuration, _invoker, _clock);
        _invoker.Reply = (_, _, body, _) => WithWindow(body);
        DecisionAuthorityPublishedProfile profile = Profile();
        (await adapter.VerifyExpectedBasisAsync(Basis(), profile, TestContext.Current.CancellationToken)).ShouldBeTrue();
        _invoker.Reply = (_, _, body, _) => body.Replace("\"eval\"", "\"other\"", StringComparison.Ordinal);
        (await adapter.VerifyExpectedBasisAsync(Basis(), profile, TestContext.Current.CancellationToken)).ShouldBeFalse();

        var approver = new DecisionAuthorityApprover("Product", "actor", "source", "binding", "evidence");
        _invoker.Reply = (_, _, body, _) => WithWindow(body);
        (await adapter.VerifyApproverAsync(Basis(), approver, Fixtures.Now, TestContext.Current.CancellationToken)).ShouldBeTrue();
        _invoker.Reply = (_, _, body, _) =>
        {
            JsonNode? node = JsonNode.Parse(body);
            node!["issuedAt"] = Fixtures.Now.AddMinutes(1).ToString("O");
            return node.ToJsonString();
        };
        (await adapter.VerifyApproverAsync(Basis(), approver, Fixtures.Now, TestContext.Current.CancellationToken)).ShouldBeFalse();

        _invoker.Calls.Clear();
        var manifest = Manifest("other");
        (await adapter.VerifyAuthorityBoundaryAsync(Basis(), manifest, profile, TestContext.Current.CancellationToken)).ShouldBeFalse();
        _invoker.Calls.ShouldBeEmpty();
        _invoker.Reply = (_, _, body, _) => WithWindow(body);
        (await adapter.VerifyAuthorityBoundaryAsync(Basis(), Manifest("decision"), profile, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task TrustAndRevocation_EchoEvidenceAndRejectEmptySpki()
    {
        var trust = new DaprSigningTrustAdapter(_configuration, _invoker, _clock);
        _invoker.Reply = (_, _, _, _) => TrustProof("");
        (await trust.ResolveAsync("tenant-a", "DeletionBatchCapabilitySigningKey", "k1", TestContext.Current.CancellationToken)).ShouldBeNull();
        _invoker.Reply = (_, _, _, _) => TrustProof("AQ==");
        DeletionCapabilityPublishedTrust? resolved = await trust.ResolveAsync("tenant-a", "DeletionBatchCapabilitySigningKey", "k1", TestContext.Current.CancellationToken);
        resolved.ShouldNotBeNull();
        resolved.SubjectPublicKeyInfo.ShouldBe([1]);

        var revocation = new DaprRevocationAuthenticatorAdapter(_configuration, _invoker, _clock);
        var envelope = Envelope();
        string evidence = "signed-evidence";
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))).ToLowerInvariant();
        _invoker.Reply = (_, _, body, _) => WithWindow(body);
        DeletionCapabilityRevocationAuthorization? authorization = await revocation.VerifyAsync(envelope, evidence, TestContext.Current.CancellationToken);
        authorization.ShouldNotBeNull();
        authorization.Envelope.EventIdentity.ShouldBe(envelope.EventIdentity);
        _invoker.Calls.Last().Body.ShouldContain(digest);
        _invoker.Reply = (_, _, body, _) => WithWindow(body).Replace(digest, new string('a', 64), StringComparison.Ordinal);
        (await revocation.VerifyAsync(envelope, evidence, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Signing_EffectsOnlyWhenTheCurrentProofIsNotTheSameSignedOutcome()
    {
        var adapter = new DaprSigningProviderAdapter(_configuration, _invoker, _clock);
        DeletionBatchCapabilityV1 payload = Payload();
        _invoker.Reply = (_, method, body, _) => method == "query-sign" ? Absent(body, "signer-app", method) : SignedProof(body);
        DeletionCapabilitySigningResult signed = await adapter.SignAsync(payload, "request-1", TestContext.Current.CancellationToken);
        signed.Outcome.State.ShouldBe(DeletionCapabilitySigningState.Signed);
        _invoker.Calls.Select(call => call.Method).ShouldBe(["query-sign", "effect-sign"]);
        _invoker.Calls[1].Body.ShouldContain("capabilityDigest");
        _invoker.Calls[1].Body.ShouldContain("request-1");

        string echoed = _invoker.Calls[1].Body;
        _invoker.Calls.Clear();
        _invoker.Reply = (_, _, _, _) => SignedProof(echoed);
        (await adapter.SignAsync(payload, "request-1", TestContext.Current.CancellationToken)).Outcome.State.ShouldBe(DeletionCapabilitySigningState.Signed);
        _invoker.Calls.Select(call => call.Method).ShouldBe(["query-sign"]);

        _invoker.Reply = (_, _, body, _) => SignedProof(body).Replace("request-1", "other", StringComparison.Ordinal);
        (await adapter.SignAsync(payload, "request-1", TestContext.Current.CancellationToken)).Outcome.State.ShouldBe(DeletionCapabilitySigningState.Unavailable);
    }

    [Fact]
    public async Task Replay_FirstSightingUsesCallerRetentionAndDoesNotRepeatAnExactReceipt()
    {
        var adapter = new DaprReplayRegistrarAdapter(_configuration, _invoker, _clock);
        var intent = new TrustedEnvelopeReplayIntent("issuer", "nonce", "tenant-a", "command", "k1", new string('c', 64));
        TimeSpan retention = TimeSpan.FromDays(7);
        _invoker.Reply = (_, method, body, _) => method == "query-replay" ? Absent(body, "replay-app", method) : ReplayProof(intent, Fixtures.Now, retention, 1);
        TrustedEnvelopeReplayReceipt? receipt = await adapter.RegisterAsync(intent, Fixtures.Now, retention, TestContext.Current.CancellationToken);
        receipt.ShouldNotBeNull();
        receipt.SourceRevision.ShouldBe(1);
        receipt.FirstSeenAt.Offset.ShouldBe(TimeSpan.Zero);
        receipt.RetainUntil.ShouldBe(receipt.FirstSeenAt.Add(retention));
        receipt.FirstSeenAt.ShouldBeLessThanOrEqualTo(Fixtures.Now);
        _invoker.Calls.Select(call => call.Method).ShouldBe(["query-replay", "effect-replay"]);
        _invoker.Calls[1].Body.ShouldContain("retentionTicks");

        string exact = ReplayProof(intent, Fixtures.Now, retention, 1);
        _invoker.Calls.Clear();
        _invoker.Reply = (_, _, _, _) => exact;
        (await adapter.RegisterAsync(intent, Fixtures.Now.AddMinutes(1), retention, TestContext.Current.CancellationToken)).ShouldBe(receipt);
        _invoker.Calls.Select(call => call.Method).ShouldBe(["query-replay"]);

        _invoker.Reply = (_, _, _, _) => ReplayProof(intent, Fixtures.Now, retention, 4);
        (await adapter.RegisterAsync(intent, Fixtures.Now, retention, TestContext.Current.CancellationToken)).ShouldBeNull();
        _invoker.Reply = (_, _, _, _) => ReplayProof(intent, Fixtures.Now, TimeSpan.FromSeconds(1), 1);
        (await adapter.RegisterAsync(intent, Fixtures.Now, retention, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task Compromise_NamesMirrorOrSubscriptionRatherThanTheCredential()
    {
        SecretsDependencyNotAvailableException subscription = Should.Throw<SecretsDependencyNotAvailableException>(() =>
            new DaprCompromiseRegistrarAdapter(_configuration with { CompromiseSubscriptionReference = null }, _invoker, _clock)
                .LookupAsync(Envelope(), TestContext.Current.CancellationToken).GetAwaiter().GetResult());
        subscription.Input.ShouldBe("runtime.compromiseSubscriptionReference");
        var mirror = new DaprCompromiseRegistrarAdapter(_configuration with { CompromiseGuardMirrorReference = null }, _invoker, _clock);
        Should.Throw<SecretsDependencyNotAvailableException>(mirror.RequireMirror).Input.ShouldBe("runtime.compromiseGuardMirrorReference");
        Should.Throw<SecretsDependencyNotAvailableException>(() => mirror.LookupAsync(Envelope(), CancellationToken.None).GetAwaiter().GetResult())
            .Input.ShouldBe("runtime.compromiseGuardMirrorReference");
        Should.Throw<SecretsDependencyNotAvailableException>(() => mirror.RegisterAsync(Envelope(), CancellationToken.None).GetAwaiter().GetResult())
            .Input.ShouldBe("runtime.compromiseGuardMirrorReference");
        _invoker.Calls.ShouldBeEmpty();

        var present = new DaprCompromiseRegistrarAdapter(_configuration, _invoker, _clock);
        _invoker.Reply = (_, _, body, _) => CompromiseProof(body, "[]");
        (await present.RegisterAsync(Envelope(), TestContext.Current.CancellationToken)).ShouldNotBeNull();
        _invoker.Calls.Select(call => call.Method).ShouldBe(["query-compromise"]);
        _invoker.Calls.Clear();
        _invoker.Reply = (_, _, body, _) => CompromiseProof(body, "[\"batch-1\"]").Replace("\"issuer\":\"issuer\"", "\"issuer\":\"other\"", StringComparison.Ordinal);
        (await present.LookupAsync(Envelope(), TestContext.Current.CancellationToken)).ShouldBeNull();
        _invoker.Calls.Clear();
        _invoker.Reply = (_, _, body, _) => CompromiseProof(body, "[\"batch-1\"]");
        (await present.LookupAsync(Envelope(), TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    [Fact]
    public void ReplayAndCompromise_StayOutOfGeneralResolution()
    {
        var services = new ServiceCollection();
        services.AddExplicitSecretsRuntime(_configuration, _invoker, _clock);
        services.AddPrivateReplay(_configuration, _invoker, _clock);
        services.AddPrivateCompromise(_configuration, _invoker, _clock).ShouldBeNull();
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(ITrustedEnvelopeReplayRegistrar));
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(IDeletionCapabilityCompromiseRegistrar));
    }

    [Fact]
    public void ProofAndLoad_RejectDuplicatesAndUnauthorizedReadersWithoutCopyingSecrets()
    {
        SecretsProof.Parse("""{"keyState":"Revoked","keyState":"Active"}""").ShouldBeNull();
        SecretsRuntimeConfiguration? loaded = SecretsRuntimeConfiguration.Parse("""{"runtime":{"providerBinding":"store","token":"raw"}}""");
        loaded.ShouldNotBeNull();
        loaded.ProviderBinding.ShouldBe("store");
        SecretsRuntimeConfiguration.Parse("""{"runtime":{"providerBinding":"store","providerBinding":"other"}}""").ShouldBeNull();
        SecretsRuntimeConfiguration.Load(() => throw new UnauthorizedAccessException("denied")).ShouldBeNull();
    }

    [Fact]
    public void Qualification_StaysUnavailableWithoutAllCopyOrNonrollback_AndObserverDoesNotAppend()
    {
        QualificationDenial denial = QualificationDenial.Evaluate(new SecretsRuntimeConfiguration());
        denial.StatusCode.ShouldBe(503);
        denial.MissingInputs.ShouldContain("runtime.allCopyLifecycleAuthorityReference");
        denial.MissingInputs.ShouldContain("runtime.nonrollbackJournalReference");
        denial.MissingInputs.ShouldNotContain("runtime.profileReference");
        denial.MissingInputs.ShouldNotContain("runtime.tenantInventoryReference");
        denial.QualificationEffects.ShouldBe(0);

        var journal = new ContentFreeObserverJournal();
        ObserverEndpoint.Observe(new SecretsRuntimeConfiguration(), journal).ShouldBe(503);
        journal.Read().ShouldBeEmpty();

        var enrolled = new SecretsRuntimeConfiguration
        {
            AllCopyLifecycleAuthorityReference = "all-copy",
            NonrollbackJournalReference = "journal",
            AcceptedReplacementCommit = new string('a', 40),
        };
        QualificationDenial ready = QualificationDenial.Evaluate(enrolled);
        ready.MissingInputs.ShouldBeEmpty();
        ready.ProductionMutationAllowed.ShouldBeFalse();
        ready.QualificationEffects.ShouldBe(0);
        ObserverEndpoint.Observe(enrolled, journal).ShouldBe(503);
        journal.Read().ShouldBeEmpty();
        typeof(Hexalith.Platform.Secrets.Qualification.Enrollment.QualificationProcessIdentity).Assembly.GetName().Name
            .ShouldNotBe(typeof(Hexalith.Platform.Secrets.Qualification.Executor.QualificationProcessIdentity).Assembly.GetName().Name);
        typeof(Hexalith.Platform.Secrets.Qualification.Executor.QualificationProcessIdentity).Assembly.GetName().Name
            .ShouldNotBe(typeof(QualificationProcessIdentity).Assembly.GetName().Name);
        Hexalith.Platform.Secrets.Qualification.Enrollment.QualificationProcessIdentity.Role.ShouldBe("enrollment");
        Hexalith.Platform.Secrets.Qualification.Executor.QualificationProcessIdentity.Role.ShouldBe("executor");
        QualificationProcessIdentity.Role.ShouldBe("observer");
    }

    [Fact]
    public void ObserverJournal_SerializesAppendAndRead()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var journal = new ContentFreeObserverJournal(directory);
        Parallel.For(0, 100, index => journal.Append(index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        journal.Read().Count.ShouldBe(100);
        new ContentFreeObserverJournal(directory).Read().Count.ShouldBe(100);
        Directory.Delete(directory, recursive: true);
    }

    private static PlatformKeyVersion Key() => new("tenant-a", PlatformKeyPurpose.TenantKek, "alias", "v1", "provider", "inventory-app", null, null);

    private static DecisionAuthorityExpectedBasis Basis() => new("decision", "contract", 1, 2, null, "root", ["eval"], ["Product"]);

    private static DecisionAuthorityPublishedProfile Profile() => new("issuer", "audience", 3, "k1", "anchor", "1", [1], false, true,
        "DecisionApprovalVerificationOnly", TimeSpan.FromMinutes(5), Fixtures.Now.AddHours(-1), Fixtures.Now.AddHours(1), ["recorder"]);

    private static DecisionAuthorityManifest Manifest(string decision) => new("issuer", "audience", decision, "contract", 1, 2, null, "root",
        ["eval"], [new DecisionAuthorityApprover("Product", "actor", "source", "binding", "evidence")], Fixtures.Now, Fixtures.Now.AddMinutes(5), "k1");

    private string ProfileProof() => $$"""
        {"issuer":"{{_configuration.Issuer}}","audience":"{{_configuration.Audience}}","version":"{{_configuration.ProfileVersion}}","notBefore":"{{_configuration.ProfileNotBefore:O}}","validUntil":"{{_configuration.ProfileValidUntil:O}}"}
        """;

    private static string WithWindow(string body)
    {
        JsonNode? node = JsonNode.Parse(body);
        node!["observedAt"] = Fixtures.Now.ToString("O");
        node["validUntil"] = Fixtures.Now.AddSeconds(21).ToString("O");
        node["authorityRevision"] = "rev-1";
        node["authorized"] = true;
        return node.ToJsonString();
    }

    private static string DecisionProof(long lifetime, string spki) => $$"""
        {"issuer":"issuer","audience":"audience","profileRevision":3,"signingKeyVersion":"k1","publicAnchorId":"anchor","publicAnchorVersion":"1","subjectPublicKeyInfo":"{{spki}}","isRevoked":false,"isIndependentlyGoverned":true,"keyPurpose":"DecisionApprovalVerificationOnly","maximumManifestLifetimeSeconds":{{lifetime}},"validFrom":"{{Fixtures.Now.AddHours(-1):O}}","validUntil":"{{Fixtures.Now.AddSeconds(21):O}}","forbiddenApprovalActors":["recorder"]}
        """;

    private static string TrustProof(string spki) => $$"""
        {"tenantId":"tenant-a","keyFamily":"DeletionBatchCapabilitySigningKey","capabilityKeyVersion":"k1","issuer":"issuer","audience":"audience","publicAnchorId":"anchor","publicAnchorVersion":"1","subjectPublicKeyInfo":"{{spki}}","trustProfileRevision":4,"isCurrentNonRevoked":true,"isRevoked":false,"observedAt":"{{Fixtures.Now:O}}","validUntil":"{{Fixtures.Now.AddSeconds(21):O}}"}
        """;

    private static DeletionCapabilityRevocationEnvelope Envelope()
        => new("issuer", "audience", "tenant-a", "DeletionBatchCapabilitySigningKey", "k1", 2, 4, "event-1", new string('a', 64));

    private static DeletionBatchCapabilityV1 Payload() => new("issuer", "audience", "tenant-a", "deletion", "seal", "batch", 1, "batch-1",
        new string('d', 64), "guard", 1, 1, 1, "k1");

    private static string Absent(string body, string authority, string method)
    {
        JsonNode node = JsonNode.Parse(WithWindow(body))!;
        node["status"] = "absent";
        node["proofType"] = "absence";
        node["authorityReference"] = authority;
        node["queryMethod"] = method;
        return node.ToJsonString();
    }

    private static string SignedProof(string body)
    {
        JsonNode? node = JsonNode.Parse(body);
        node!["state"] = "Signed";
        node["detachedJws"] = "detached";
        node["originalIssuanceReceiptId"] = "original-1";
        node["publicAnchorId"] = "anchor";
        node["publicAnchorVersion"] = "1";
        node["subjectPublicKeyInfo"] = "AQ==";
        node["observedAt"] = Fixtures.Now.ToString("O");
        node["validUntil"] = Fixtures.Now.AddSeconds(21).ToString("O");
        return node.ToJsonString();
    }

    private static string CompromiseProof(string body, string batches)
    {
        JsonNode? node = JsonNode.Parse(body);
        node!["ownerRevision"] = 1;
        node["keyBlockSetRevision"] = 1;
        node["receiptId"] = "receipt-1";
        node["affectedBatchIds"] = JsonNode.Parse(batches);
        return node.ToJsonString();
    }

    private static string ReplayProof(TrustedEnvelopeReplayIntent intent, DateTimeOffset firstSeen, TimeSpan retention, long revision) => $$"""
        {"issuer":"{{intent.Issuer}}","deliveryNonce":"{{intent.DeliveryNonce}}","targetTenantId":"{{intent.TargetTenantId}}","logicalCommandId":"{{intent.LogicalCommandId}}","signingKeyVersion":"{{intent.SigningKeyVersion}}","canonicalAndTagDigest":"{{intent.CanonicalAndTagDigest}}","firstSeenAt":"{{firstSeen:O}}","retainUntil":"{{firstSeen.Add(retention):O}}","sourceRevision":{{revision}},"receiptId":"receipt-1"}
        """;
}
